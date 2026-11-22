// The licence: Karpo Pro, sold on the Microsoft Store (docs\widgets.md §138). The
// person, 2026-10-11: "when there is no licence the amount of widgets a user can place will be
// limited to lets say 20 and there should only be the possiblity to create 2 layouts. other than
// that there should not be any other limits enforced. now, the number of widgets should just be the
// ones that are actually shown in the layouts, if there are hidden slots these wont count."
//
// Everything here is pure, so the tests hold every rule; the Store itself is asked by StoreLicense
// (Karpo.Dock\StoreLicense.cs, compiled into both apps), which only gathers the facts these rules
// decide on. Ported from Themia's licensing (Themia.Core\Licensing\LicenseRules.cs).
//
// - The tier. A copy without package identity — every build that is not the Store's MSIX, the
//   person's own artifacts\publish among them — has no Store to ask and is unrestricted: a
//   "Developer build". KARPO_LICENSE=free makes such a copy act as the free version, to try the
//   limits by hand. A packaged copy is Pro while the Store says the Pro add-on is owned, or while a
//   Pro confirmation of the last 7 days stands in for a Store that cannot be reached; else Free.
// - The limits only ever refuse to ADD. A copy over them (Pro lapsed, an import, a layout changed
//   to a type that shows a row more) keeps everything it has; moving, editing and removing are never
//   refused, and nothing anywhere is deleted because of a limit.
// - What counts (LicenseCount): per layout, every entry on its bars in a slot its type shows, once
//   each — a folder once, whatever it holds. Pinned apps and Windows' own taskbar buttons
//   are not widgets and are free.

using System.Globalization;
using System.Runtime.InteropServices;

namespace Karpo.Core;

/// <summary>What the running copy may do.</summary>
public enum LicenseTier
{
    /// <summary>The free version: at most <see cref="LicenseRules.FreeWidgets"/> widgets on a layout and <see cref="LicenseRules.FreeLayouts"/> layouts.</summary>
    Free,

    /// <summary>The Pro add-on is owned: no limits.</summary>
    Pro,

    /// <summary>A developer build (no package identity, so no Store to ask): no limits, and it says so.</summary>
    Unrestricted,
}

/// <summary>Why the tier is what it is: what the License page explains.</summary>
public enum LicenseBasis
{
    /// <summary>Run without package identity (a build, artifacts\publish). The Store is never asked.</summary>
    DeveloperBuild,

    /// <summary>A developer build told to act as the free version (<see cref="LicenseRules.Variable"/>=free).</summary>
    DeveloperFree,

    /// <summary>Packaged, the Store has not answered yet, and no recent Pro confirmation stands in.</summary>
    Checking,

    /// <summary>The Store answered: the Pro add-on is not owned.</summary>
    StoreFree,

    /// <summary>The Store answered: the Pro add-on is owned.</summary>
    StorePro,

    /// <summary>The Store can't be reached, and Pro was confirmed within <see cref="LicenseRules.OfflineGrace"/>.</summary>
    OfflineGrace,

    /// <summary>The Store can't be reached, and no Pro confirmation is recent enough to trust.</summary>
    StoreUnavailable,

    /// <summary>
    /// A copy built for the Store (<see cref="LicenseRules.StoreChannel"/>) run without its package: the exe
    /// taken out of it, or started some way that lost its identity. There is no Store to ask, and it is
    /// the free version: were it a developer build, anybody's copy of the Store's exe would be Pro.
    /// </summary>
    StoreMissing,
}

/// <summary>The Store's last answer about the Pro add-on.</summary>
public enum StoreAnswer
{
    /// <summary>Not asked yet in this process.</summary>
    NotAsked,

    /// <summary>The add-on's licence is there and active.</summary>
    Owned,

    /// <summary>The Store answered, and the add-on is not among the active licences.</summary>
    NotOwned,

    /// <summary>The Store could not be asked or did not answer (offline, its service broken).</summary>
    Unavailable,
}

/// <param name="Tier">What the copy may do.</param>
/// <param name="Basis">Why.</param>
/// <param name="GraceUntil">With <see cref="LicenseBasis.OfflineGrace"/>: when Pro lapses unless the Store confirms it again.</param>
public sealed record LicenseStatus(LicenseTier Tier, LicenseBasis Basis, DateTimeOffset? GraceUntil = null)
{
    /// <summary>A developer build's: nothing limited. What a process has before it knows better, where it has no package.</summary>
    public static LicenseStatus Developer { get; } = new(LicenseTier.Unrestricted, LicenseBasis.DeveloperBuild);

    /// <summary>Whether the free limits apply.</summary>
    public bool IsLimited => Tier == LicenseTier.Free;
}

/// <summary>How a Store purchase ended, as the License page says it.</summary>
public enum PurchaseOutcome
{
    Purchased,
    AlreadyOwned,
    Cancelled,
    NetworkError,
    ServerError,

    /// <summary>The add-on's Store id is still the placeholder (<see cref="StoreIds.ProAddOn"/>).</summary>
    NotConfigured,

    /// <summary>No Store here (a developer build) or the Store could not start the purchase.</summary>
    Unavailable,
}

/// <summary>
/// Karpo's ids in the Microsoft Store.
/// <para>
/// The app is Partner Center's "Karpo" (Store ID <c>9MXKV2VWR2CP</c>, package identity
/// <c>NathanWalser.Karpo</c>); its add-on was made there as Karpo › Add-ons › New add-on, product type
/// "Durable", lifetime "Forever". A text that is not a real Store id (<see cref="LicenseRules.IsStoreId"/>)
/// would sell nothing and match no Store licence, which is what kept the placeholder this once was from
/// ever unlocking Pro (docs\widgets.md §138, "Store setup").
/// </para>
/// </summary>
public static class StoreIds
{
    /// <summary>The durable "Karpo Pro" add-on (product id <c>karpo-pro</c>), as Partner Center names it since 2026-11-22.</summary>
    public const string ProAddOn = "9NT4RFR5R3BN";
}

/// <summary>The licence's rules, kept free of the Store's API so the tests can hold them.</summary>
public static class LicenseRules
{
    /// <summary>Widgets the free version shows on one layout (<see cref="LicenseCount"/>).</summary>
    public const int FreeWidgets = 20;

    /// <summary>Layouts the free version may have.</summary>
    public const int FreeLayouts = 2;

    /// <summary>
    /// How long a Pro confirmation carries the copy while the Store cannot be asked. Windows keeps
    /// the Store's licences itself, so this is for a broken Store service rather than every
    /// offline start.
    /// </summary>
    public static readonly TimeSpan OfflineGrace = TimeSpan.FromDays(7);

    /// <summary>How far in the future a kept confirmation may lie before it is taken as tampered with.</summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(10);

    /// <summary>The variable that makes a developer build act as the free version: <c>KARPO_LICENSE=free</c>.</summary>
    public const string Variable = "KARPO_LICENSE";

    /// <summary>
    /// The name of the assembly's mark that says whom the copy was built for (AssemblyMetadata, written by
    /// scripts\publish.ps1 -Channel): none for a build of the source, <see cref="StoreChannel"/> for the
    /// Store's package.
    /// </summary>
    public const string ChannelMetadata = "KarpoChannel";

    /// <summary>The mark of a copy built for the Microsoft Store's package (scripts\package.ps1).</summary>
    public const string StoreChannel = "store";

    /// <summary>Whether <paramref name="channel"/> (the assembly's <see cref="ChannelMetadata"/>) is the Store's.</summary>
    public static bool IsStoreBuild(string? channel) => string.Equals(channel?.Trim(), StoreChannel, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <see cref="Variable"/>'s value asks a developer build to act as the free version.</summary>
    public static bool ActsFree(string? value) => string.Equals(value?.Trim(), "free", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The tier from what is known. Anything that is not a clear, current "owned" (or a
    /// confirmation inside the grace) is Free: an unknown state never unlocks Pro.
    /// </summary>
    /// <param name="packaged">Whether the process has package identity. Without it there is no Store and nothing is limited.</param>
    /// <param name="actsFree">A developer build told to act as the free version (<see cref="Variable"/>); a packaged copy ignores it.</param>
    /// <param name="answer">The Store's last answer.</param>
    /// <param name="confirmedPro">When the Store last said "owned" on this PC (the kept confirmation); null where there is none.</param>
    /// <param name="now">The time now.</param>
    public static LicenseStatus Decide(bool packaged, bool actsFree, StoreAnswer answer, DateTimeOffset? confirmedPro, DateTimeOffset now) =>
        Decide(packaged, storeBuild: false, actsFree, answer, confirmedPro, now);

    /// <summary>
    /// The same for a copy that may have been built for the Store (<paramref name="storeBuild"/>,
    /// <see cref="IsStoreBuild"/>): such a copy without its package is never a developer build. It is the
    /// free version (<see cref="LicenseBasis.StoreMissing"/>), whatever <see cref="Variable"/> says.
    /// </summary>
    public static LicenseStatus Decide(bool packaged, bool storeBuild, bool actsFree, StoreAnswer answer, DateTimeOffset? confirmedPro, DateTimeOffset now)
    {
        if (!packaged && storeBuild)
        {
            return new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreMissing);
        }
        if (!packaged)
        {
            return actsFree ? new LicenseStatus(LicenseTier.Free, LicenseBasis.DeveloperFree) : LicenseStatus.Developer;
        }
        switch (answer)
        {
            case StoreAnswer.Owned:
                return new LicenseStatus(LicenseTier.Pro, LicenseBasis.StorePro);
            case StoreAnswer.NotOwned:
                return new LicenseStatus(LicenseTier.Free, LicenseBasis.StoreFree);
        }
        if (GraceUntil(confirmedPro, now) is { } until)
        {
            return new LicenseStatus(LicenseTier.Pro, LicenseBasis.OfflineGrace, until);
        }
        return new LicenseStatus(LicenseTier.Free, answer == StoreAnswer.NotAsked ? LicenseBasis.Checking : LicenseBasis.StoreUnavailable);
    }

    /// <summary>
    /// The end of the grace a confirmation gives, or null where it gives none: none kept, expired,
    /// or dated in the future (a clock set back, or the file edited).
    /// </summary>
    public static DateTimeOffset? GraceUntil(DateTimeOffset? confirmedPro, DateTimeOffset now)
    {
        if (confirmedPro is not { } confirmed || confirmed > now + ClockSkew)
        {
            return null;
        }
        var until = confirmed + OfflineGrace;
        return now <= until ? until : null;
    }

    /// <summary>
    /// Whether the Store's add-on licences hold an active one for <paramref name="productStoreId"/>.
    /// Their keys are SKU ids (<c>9ABCDEFGHIJK/0010</c>), so the product id is matched as what comes
    /// before the slash, as Microsoft's own samples do.
    /// </summary>
    public static bool OwnsAddOn(IEnumerable<(string SkuStoreId, bool IsActive)> licences, string productStoreId)
    {
        if (!IsStoreId(productStoreId))
        {
            return false;
        }
        foreach (var (sku, active) in licences)
        {
            if (active && sku is not null
                && sku.StartsWith(productStoreId, StringComparison.OrdinalIgnoreCase)
                && (sku.Length == productStoreId.Length || sku[productStoreId.Length] == '/'))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether a text is a real Store product id (12 capital letters and digits, as Partner Center
    /// gives them) rather than the placeholder the source carries until the add-on exists.
    /// </summary>
    public static bool IsStoreId(string? productStoreId) =>
        productStoreId is { Length: 12 } id && id.All(c => c is (>= '0' and <= '9') or (>= 'A' and <= 'Z'));
}

/// <summary>
/// What counts against the free version's widgets: per layout, every entry on its bar, once each.
/// <list type="bullet">
/// <item>A folder counts once, whatever it holds: what a layout shows is the folder on its bar.
/// The ready-made folders (the clock, Wi-Fi, sound, …) are made of many widgets inside, and a new
/// bar has a dozen of them; counted, a new install would be over the limit before it was touched.
/// So adding to a folder is never refused.</item>
/// <item>Pinned apps and Windows' own taskbar buttons (<see cref="Free"/>) are not
/// widgets: free, as on Windows' taskbar.</item>
/// </list>
/// Per layout rather than across them: a new layout starts as a copy of Windows' taskbar, and two
/// such layouts together would already be most of one allowance.
/// </summary>
public static class LicenseCount
{
    /// <summary>The kinds that are not widgets and never count: pinned apps, and what Windows' own taskbar has.</summary>
    public static readonly IReadOnlySet<BarKind> Free = new HashSet<BarKind>
    {
        BarKind.Pin,
        BarKind.WidgetsBoard,
        BarKind.Start,
        BarKind.Search,
        BarKind.TaskView,
        BarKind.Apps,
        BarKind.Tray,
        BarKind.Indicators,
        BarKind.PenMenu,
        BarKind.TouchKeyboard,
        BarKind.VirtualTouchpad,
        BarKind.QuickLink,
        BarKind.WindowsNotifications,
        BarKind.WindowsQuickSettings,
        BarKind.WindowsSound,
        BarKind.WindowsNetworks,
        BarKind.WindowsCast,
        BarKind.WindowsClipboard,
        BarKind.EmojiPanel,
        BarKind.ShowDesktop,
    };

    /// <summary>Whether an entry of <paramref name="kind"/> counts as a widget.</summary>
    public static bool Counts(BarKind kind) => !Free.Contains(kind);

    /// <summary>How many widgets <paramref name="layout"/> shows, as the free version counts them.</summary>
    public static int Widgets(TaskbarLayout? layout) =>
        layout?.Islands is { } islands ? islands.Count(island => island is not null && Counts(island.Kind)) : 0;
}

/// <summary>
/// Where every add asks the licence: the dock's add list, its edit bar's buttons, Copy, Ctrl+V
/// and the settings app's new layouts. Each answer is null where the add may go ahead,
/// else the words that say why it can't — which every refusal shows, with a way to the License page:
/// the person must know why the limit was reached.
/// </summary>
public static class LicenseGate
{
    /// <summary>
    /// Why an add that takes a layout from <paramref name="before"/> widgets to <paramref name="after"/>
    /// is refused, or null where it is not: only a free copy, only an add that raises the count, and
    /// only past <see cref="LicenseRules.FreeWidgets"/>. A copy already over the limit can still move,
    /// change and remove.
    /// </summary>
    public static string? Widgets(LicenseStatus status, int before, int after) =>
        status.IsLimited && after > before && after > LicenseRules.FreeWidgets
            ? S.CoreLicenseWidgetLimit(limit: LicenseRules.FreeWidgets, count: before)
            : null;

    /// <summary>
    /// The same for a change from the layout <paramref name="before"/> to <paramref name="after"/>
    /// (a config cloned and edited): the counts of the two (<see cref="LicenseCount.Widgets"/>).
    /// </summary>
    public static string? Widgets(LicenseStatus status, TaskbarLayout? before, TaskbarLayout? after) =>
        Widgets(status, LicenseCount.Widgets(before), LicenseCount.Widgets(after));

    /// <summary>
    /// Why adding one entry of <paramref name="kind"/> to <paramref name="layout"/> would be refused, or
    /// null: what the add list greys its rows by. An entry that would not count
    /// (<see cref="LicenseCount.Counts(BarKind)"/>) is never refused. A ready-made folder is
    /// <see cref="BarKind.FolderList"/>.
    /// </summary>
    public static string? Add(LicenseStatus status, TaskbarLayout? layout, BarKind kind)
    {
        if (layout is null || !LicenseCount.Counts(kind))
        {
            return null;
        }
        var count = LicenseCount.Widgets(layout);
        return Widgets(status, count, count + 1);
    }

    /// <summary>Why making one layout more than <paramref name="layouts"/> is refused, or null where it is not.</summary>
    public static string? Layouts(LicenseStatus status, int layouts) =>
        status.IsLimited && layouts + 1 > LicenseRules.FreeLayouts
            ? S.CoreLicenseLayoutLimit(limit: LicenseRules.FreeLayouts, count: layouts)
            : null;
}

/// <summary>
/// The kept record of the Store's last "owned", for the offline grace: <c>karpo-license:v1:&lt;unix
/// seconds&gt;</c>, sealed with DPAPI for this Windows user before it touches the disk
/// (StoreLicense). Anything else — another version, a damaged number — reads as
/// <see cref="LicenseCacheState.Corrupt"/> and gives no grace: a broken record never unlocks Pro.
/// </summary>
public static class LicenseCache
{
    public const string Prefix = "karpo-license:v1:";

    /// <summary>The file it is kept in, beside Karpo's other local files; both apps read and write it.</summary>
    public static string Path => System.IO.Path.Combine(Config.LocalFolder, "license.dat");

    public static string Format(DateTimeOffset confirmedPro) =>
        Prefix + confirmedPro.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    public static (LicenseCacheState State, DateTimeOffset? ConfirmedPro) Parse(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return (LicenseCacheState.Absent, null);
        }
        if (!payload.StartsWith(Prefix, StringComparison.Ordinal)
            || !long.TryParse(payload.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0
            || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
        {
            return (LicenseCacheState.Corrupt, null);
        }
        return (LicenseCacheState.Valid, DateTimeOffset.FromUnixTimeSeconds(seconds));
    }
}

/// <summary>What reading the kept confirmation found.</summary>
public enum LicenseCacheState
{
    Absent,
    Valid,
    Corrupt,
}

/// <summary>Whether this process runs with package identity: installed from the Store's MSIX, or not.</summary>
public static partial class PackageIdentity
{
    private const int AppModelErrorNoPackage = 15700;

    private static readonly Lazy<bool> Packaged = new(Read);

    /// <summary>
    /// True for a packaged process. Every other build — <c>dotnet run</c>, artifacts\publish, the
    /// tests — has none, has no Store to ask, and is a developer build (<see cref="LicenseRules.Decide"/>).
    /// </summary>
    public static bool IsPackaged => Packaged.Value;

    /// <summary>The application's id in package\AppxManifest.xml (Application Id).</summary>
    public const string ApplicationId = "Karpo";

    private const int ErrorInsufficientBuffer = 122;

    private static readonly Lazy<string?> Family = new(ReadFamily);

    /// <summary>The package's family name ("NathanWalser.Karpo_s8mm2zcbg0yjw"), or null without a package.</summary>
    public static string? FamilyName => Family.Value;

    /// <summary>
    /// What Windows starts this app by (its AppUserModelID, <c>family!Karpo</c>), or null without a
    /// package: how the guard starts a packaged dock again, inside its package (§710, DockLauncher).
    /// </summary>
    public static string? AppUserModelId => FamilyName is { Length: > 0 } family ? family + "!" + ApplicationId : null;

    private static unsafe string? ReadFamily()
    {
        try
        {
            uint length = 0;
            if (GetCurrentPackageFamilyName(ref length, null) != ErrorInsufficientBuffer || length <= 1)
            {
                return null;
            }
            var name = new char[length];
            fixed (char* text = name)
            {
                if (GetCurrentPackageFamilyName(ref length, text) != 0)
                {
                    return null;
                }
            }
            // The length counts the terminating null.
            return new string(name, 0, (int)length - 1);
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return null;
        }
    }

    private static unsafe bool Read()
    {
        try
        {
            uint length = 0;
            // With no buffer: ERROR_INSUFFICIENT_BUFFER for a packaged process, APPMODEL_ERROR_NO_PACKAGE otherwise.
            return GetCurrentPackageFullName(ref length, null) != AppModelErrorNoPackage;
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    [LibraryImport("kernel32.dll")]
    private static unsafe partial int GetCurrentPackageFullName(ref uint packageFullNameLength, char* packageFullName);

    [LibraryImport("kernel32.dll")]
    private static unsafe partial int GetCurrentPackageFamilyName(ref uint packageFamilyNameLength, char* packageFamilyName);
}
