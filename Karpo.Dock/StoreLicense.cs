// The licence as the Microsoft Store has it (docs\widgets.md §138): the dock asks the Store itself,
// refuses an add on the bar past the free version's limit (edit mode, the add list, Copy, Ctrl+V) and
// sells Pro from the edit bar's ⚙ › Karpo Pro (§339). Nothing is written for the dock to trust: a
// state in a file would be a file anybody could edit to unlock Pro. Windows keeps the Store's licences
// per user and package, and StoreContext.OfflineLicensesChanged says when a purchase (from ⚙, in the
// Store app) or a refund changes it. What is kept is the offline grace's record (LicenseCache),
// DPAPI-sealed for this Windows user: the Store's last "owned", which carries Pro for 7 days while
// the Store can't be reached.
//
// A copy without package identity never touches the Store: it is a developer build, unrestricted
// (LicenseRules.Decide), or the free version with KARPO_LICENSE=free. Unless it was built for the Store
// (scripts\package.ps1 marks it, LicenseRules.ChannelMetadata): the Store's exe run outside its package
// is the free version too, so taking it out of the package unlocks nothing.

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Karpo.Core;
using Karpo.Dock.Native;
using Windows.Services.Store;

namespace Karpo.Dock;

/// <summary>
/// Karpo's licence, from the Microsoft Store and nowhere else. One per process. <see cref="Status"/>
/// may be read from any thread; <see cref="Changed"/> is raised on whichever thread the Store
/// answered on, and the settings app takes it to its UI thread.
/// </summary>
public sealed class StoreLicense : IDisposable
{
    private static readonly byte[] Entropy = "Karpo.License.v1"u8.ToArray();

    private readonly bool packaged;
    private readonly bool storeBuild;
    private readonly bool actsFree;
    private readonly object gate = new();
    private StoreContext? store;
    private Task? refreshing;
    private StoreAnswer answer = StoreAnswer.NotAsked;
    private DateTimeOffset? confirmedPro;
    private volatile LicenseStatus status;
    private bool disposed;

    /// <param name="packaged">Whether the process has package identity (<see cref="PackageIdentity.IsPackaged"/>).</param>
    /// <param name="actsFree">A developer build told to act as the free version (<see cref="LicenseRules.Variable"/>).</param>
    /// <param name="storeBuild">Whether this copy was built for the Store's package (<see cref="LicenseRules.IsStoreBuild"/>).</param>
    public StoreLicense(bool packaged, bool actsFree, bool storeBuild = false)
    {
        this.packaged = packaged;
        this.storeBuild = storeBuild;
        this.actsFree = actsFree;
        confirmedPro = packaged ? ReadCache() : null;
        status = LicenseRules.Decide(packaged, storeBuild, actsFree, answer, confirmedPro, Clock.RealOffsetUtc());
    }

    /// <summary>The licence of this process: packaged or not, built for the Store or not, and KARPO_LICENSE.</summary>
    public static StoreLicense ForThisProcess() =>
        new(PackageIdentity.IsPackaged, LicenseRules.ActsFree(Environment.GetEnvironmentVariable(LicenseRules.Variable)), LicenseRules.IsStoreBuild(Channel()));

    /// <summary>Whom this copy was built for: the assembly's <see cref="LicenseRules.ChannelMetadata"/>, or null for a build of the source.</summary>
    internal static string? Channel() =>
        typeof(StoreLicense).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(mark => mark.Key == LicenseRules.ChannelMetadata)?.Value;

    /// <summary>The tier now, and why.</summary>
    public LicenseStatus Status => status;

    /// <summary>Whether there is a Store to ask: the copy is packaged.</summary>
    public bool HasStore => packaged;

    /// <summary>Whether Pro can be bought here: packaged, and the add-on's Store id is a real one.</summary>
    public bool CanPurchase => packaged && LicenseRules.IsStoreId(StoreIds.ProAddOn);

    /// <summary>Raised whenever <see cref="Status"/> may have changed, on any thread.</summary>
    public event Action? Changed;

    /// <summary>Asks the Store once, and listens for its licences changing. Nothing for a developer build.</summary>
    public void Start()
    {
        if (!packaged)
        {
            return;
        }
        _ = RefreshAsync();
    }

    /// <summary>Asks the Store again ("Check again"); joins a question already asked rather than asking twice.</summary>
    public Task RefreshAsync()
    {
        if (!packaged)
        {
            return Task.CompletedTask;
        }
        lock (gate)
        {
            if (refreshing is { IsCompleted: false } running)
            {
                return running;
            }
            // On the pool: the caller may be a window's thread, and the Store's answer may come at once.
            return refreshing = Task.Run(() => Troubles.Guard("license: refresh", async () => Apply(await AskAsync().ConfigureAwait(false))));
        }
    }

    /// <summary>
    /// The Store's purchase dialog for Pro, owned by the window <paramref name="owner"/>: a desktop app
    /// hands the Store a window before it shows anything (IInitializeWithWindow). The Store is asked
    /// again afterwards, whatever came of it: it is the judge.
    /// </summary>
    public async Task<PurchaseOutcome> PurchaseProAsync(nint owner)
    {
        if (!packaged)
        {
            return PurchaseOutcome.Unavailable;
        }
        if (!LicenseRules.IsStoreId(StoreIds.ProAddOn))
        {
            Troubles.Write("license: the Pro add-on's Store id is still the placeholder; nothing offered");
            return PurchaseOutcome.NotConfigured;
        }
        try
        {
            var context = Context();
            WinRT.Interop.InitializeWithWindow.Initialize(context, owner);
            var result = await context.RequestPurchaseAsync(StoreIds.ProAddOn);
            var outcome = result.Status switch
            {
                StorePurchaseStatus.Succeeded => PurchaseOutcome.Purchased,
                StorePurchaseStatus.AlreadyPurchased => PurchaseOutcome.AlreadyOwned,
                StorePurchaseStatus.NotPurchased => PurchaseOutcome.Cancelled,
                StorePurchaseStatus.NetworkError => PurchaseOutcome.NetworkError,
                _ => PurchaseOutcome.ServerError,
            };
            Troubles.Write(result.ExtendedError is { } error
                ? $"license: purchase ended {result.Status}: {Troubles.Describe(error)}"
                : $"license: purchase ended {result.Status}");
            await RefreshAsync().ConfigureAwait(false);
            return outcome;
        }
        catch (Exception error)
        {
            Troubles.Report("license: purchase", error);
            return PurchaseOutcome.Unavailable;
        }
    }

    /// <summary>Pro's price as the Store shows it to this person ("CHF 9.00"), or null where it can't say.</summary>
    public async Task<string?> ProPriceAsync()
    {
        if (!CanPurchase)
        {
            return null;
        }
        try
        {
            // docs\widgets.md section 10, §715: nothing managed crosses to Windows in the native build, not a
            // collection expression, an array or a List (each throws InvalidCastException, no CCW, and the price
            // never came). What is handed over are lists Windows made itself.
            var result = await Context().GetStoreProductsAsync(WindowsStrings.Of("Durable"), WindowsStrings.Of(StoreIds.ProAddOn));
            if (result.ExtendedError is { } error)
            {
                Troubles.Note("license: the Store listed no Pro add-on", error);
                return null;
            }
            return result.Products.TryGetValue(StoreIds.ProAddOn, out var product) ? product.Price?.FormattedPrice : null;
        }
        catch (Exception error)
        {
            Troubles.Note("license: Pro's price", error);
            return null;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            if (store is { } context)
            {
                context.OfflineLicensesChanged -= OnLicencesChanged;
            }
        }
    }

    private StoreContext Context()
    {
        lock (gate)
        {
            if (store is null)
            {
                store = StoreContext.GetDefault();
                store.OfflineLicensesChanged += OnLicencesChanged;
            }
            return store;
        }
    }

    private void OnLicencesChanged(StoreContext sender, object args) =>
        // A WinRT event's callback: nothing may leave it.
        Troubles.Guard("license: change", () => _ = RefreshAsync());

    private async Task<StoreAnswer> AskAsync()
    {
        try
        {
            var licence = await Context().GetAppLicenseAsync();
            if (licence is null)
            {
                Troubles.Write("license: the Store gave no app licence");
                return StoreAnswer.Unavailable;
            }
            var addOns = licence.AddOnLicenses.Values.Select(each => (each.SkuStoreId, each.IsActive)).ToList();
            var owned = LicenseRules.OwnsAddOn(addOns, StoreIds.ProAddOn);
            Troubles.Write($"license: app active {licence.IsActive}, {addOns.Count} add-on licence(s), Pro {(owned ? "owned" : "not owned")}");
            return owned ? StoreAnswer.Owned : StoreAnswer.NotOwned;
        }
        catch (Exception error)
        {
            Troubles.Note("license: asking the Store", error);
            return StoreAnswer.Unavailable;
        }
    }

    private void Apply(StoreAnswer answered)
    {
        var now = Clock.RealOffsetUtc();
        LicenseStatus next;
        lock (gate)
        {
            answer = answered;
            // The record follows the Store's clear answers only; "unavailable" leaves it as it is.
            if (answered == StoreAnswer.Owned)
            {
                confirmedPro = now;
                WriteCache(now);
            }
            else if (answered == StoreAnswer.NotOwned && confirmedPro is not null)
            {
                confirmedPro = null;
                ClearCache();
            }
            next = LicenseRules.Decide(packaged, storeBuild, actsFree, answer, confirmedPro, now);
        }
        if (next != status)
        {
            Troubles.Write($"license: now {next.Tier} ({next.Basis})");
        }
        status = next;
        Troubles.Guard("license: a listener", () => Changed?.Invoke());
    }

    private static DateTimeOffset? ReadCache()
    {
        try
        {
            var sealedBytes = File.ReadAllBytes(LicenseCache.Path);
            var payload = Encoding.UTF8.GetString(ProtectedData.Unprotect(sealedBytes, Entropy, DataProtectionScope.CurrentUser));
            var (state, confirmed) = LicenseCache.Parse(payload);
            if (state == LicenseCacheState.Corrupt)
            {
                Troubles.Write("license: the kept confirmation is damaged; no offline grace");
            }
            return confirmed;
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Troubles.Note("license: reading the kept confirmation; no offline grace", error);
            return null;
        }
    }

    private static void WriteCache(DateTimeOffset confirmed)
    {
        var path = LicenseCache.Path;
        // Unique per process: both apps may write it at once, and each move is whole.
        var temporary = $"{path}.{Environment.ProcessId}.tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(temporary, ProtectedData.Protect(Encoding.UTF8.GetBytes(LicenseCache.Format(confirmed)), Entropy, DataProtectionScope.CurrentUser));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Only the offline grace is lost; the Store answers again at the next start.
            Troubles.Note("license: keeping the confirmation", error);
        }
    }

    private static void ClearCache()
    {
        try
        {
            File.Delete(LicenseCache.Path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Harmless: a stale confirmation only matters while the Store can't be reached, and it expires.
            Troubles.Note("license: removing the kept confirmation", error);
        }
    }
}
