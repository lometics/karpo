// licence: the free version's limit on the bar (docs\widgets.md §138). The dock asks the Microsoft
// Store itself (StoreLicense.cs), from its start, and every add on the bar asks LicenseGate: the add
// list greys what would count (LicenseRows.Limited), and an add made anyway — a pick, the edit bar's
// Folder, Copy, Ctrl+V — is held against the count before it is applied (LicenseAllows). A
// refusal adds nothing, removes nothing, and says why with a way to ⚙ › Karpo Pro (§340). §339: the edit bar's
// ⚙ › Karpo Pro shows the same licence (MenuLicence), sells Pro over the bar and asks the Store again.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>licence: "Get Karpo Pro" in the add list and in a refusal: ⚙ › Karpo Pro (§340).</summary>
    internal const uint CMD_GET_PRO = 5010;

    /// <summary>This process's licence, asked of the Store from the start (<see cref="StartLicense"/>).</summary>
    private readonly StoreLicense license = StoreLicense.ForThisProcess();

    /// <summary>
    /// Asks the Store, at the dock's start: an add in the first seconds already knows. §545: and the settings panel's footer
    /// follows the licence as the Store answers (from its own thread), on the dock's.
    /// </summary>
    private void StartLicense()
    {
        if (Windows.System.DispatcherQueue.GetForCurrentThread() is { } queue)
        {
            license.Changed += () => queue.TryEnqueue(() => Troubles.Guard("license: the panel's footer", LicenseChanged));
        }
        license.Start();
    }

    /// <summary>§545: the licence changed: the open settings panel's footer (and its Karpo Pro rows) say it now.</summary>
    private void LicenseChanged()
    {
        if (!disposed && panelOn is not null)
        {
            StirChrome();
            PresentChrome();
        }
    }

    /// <summary>
    /// Whether the change from <see cref="config"/> to <paramref name="next"/> in the list of
    /// <paramref name="owner"/> (a display's) is within the licence: it may not take a layout the
    /// free version holds past its widgets. When it would, the refusal is shown at
    /// <paramref name="window"/> and false comes back; nothing is applied.
    /// </summary>
    private bool LicenseAllows(Config next, string owner, DockWindow? window)
    {
        var before = LicenseCount.Widgets(config.LayoutOf(owner));
        if (LicenseGate.Widgets(license.Status, before, LicenseCount.Widgets(next.LayoutOf(owner))) is null)
        {
            return true;
        }
        Troubles.Write($"license: an add refused: {before} widgets on the layout, free version");
        ShowLicenseRefusal(window, before);
        return false;
    }

    /// <summary>Why an add was refused, at the pointer, with the way to the License page.</summary>
    private void ShowLicenseRefusal(DockWindow? window, int count)
    {
        window ??= lastWindow ?? windows.FirstOrDefault();
        if (window is null)
        {
            return;
        }
        ShowFlyout(new Pending(Topic.Missing, window), LicenseRows.Refusal(count, CMD_GET_PRO), FlyoutLayout.List, window.PointerAnchor(), S.DockLicenseTitle);
    }

    /// <summary>
    /// The add list's rows for <paramref name="spot"/> as the licence has them: as they are, but for
    /// the bar of a layout the free version holds no more widgets on (<see cref="LicenseRows.Limited"/>).
    /// A licence not known to be Pro is asked again meanwhile, for the next add.
    /// </summary>
    private List<FlyoutItem> Licensed(List<FlyoutItem> rows, List<CatalogueEntry> entries, List<Preset> presets, AddSpot spot)
    {
        var status = license.Status;
        if (status.IsLimited)
        {
            _ = license.RefreshAsync();
        }
        var layout = config.LayoutOf(spot.Owner);
        if (spot.FolderId is not null || LicenseGate.Add(status, layout, BarKind.FolderList) is null)
        {
            return rows;
        }
        return LicenseRows.Limited(rows, entries, presets, CMD_ADD_WIDGET, S.DockLicenseRowLimit, LicenseRows.Used(LicenseCount.Widgets(layout)), CMD_GET_PRO);
    }

    /// <summary>§339: Pro's price as the Store shows it, once asked (<see cref="MenuLicence"/>); null for not known.</summary>
    private volatile string? proPrice;

    /// <summary>§339: Pro's price asked of the Store already.</summary>
    private bool proPriceAsked;

    /// <summary>§339: how ⚙'s Get Karpo Pro last ended, in words; null for not since the dock started.</summary>
    private volatile string? proPurchased;

    /// <summary>§339: how ⚙'s Restore a purchase last went, in words; null for not asked.</summary>
    private volatile string? proAsked;

    /// <summary>
    /// §339: ⚙ › Karpo Pro's licence: the Store's as this process has it now (<see cref="license"/>, which
    /// LicenseGate refuses adds by), what buys Pro and asks again, and how each last went. Pro's price is
    /// asked of the Store the first time, for the next showing.
    /// </summary>
    private KarpoLicence MenuLicence()
    {
        if (license.CanPurchase && !proPriceAsked)
        {
            proPriceAsked = true;
            _ = license.ProPriceAsync().ContinueWith(asked => proPrice = asked.Result, TaskContinuationOptions.OnlyOnRanToCompletion);
        }
        return new KarpoLicence(license.Status, license.HasStore, license.CanPurchase, BuyPro, AskStoreAgain)
        {
            Price = proPrice,
            Purchased = proPurchased,
            Asked = proAsked,
        };
    }

    /// <summary>
    /// §339: Get Karpo Pro from ⚙: once the menu has gone, the Store's own purchase dialog over ⚙'s bar
    /// (StoreLicense.PurchaseProAsync hands it the window), how it ended kept for the next showing; the
    /// Store is asked again after it, whatever came of it, and the licence follows.
    /// </summary>
    private void BuyPro() => Later(() =>
    {
        CloseFlyout();
        // Asked from this thread, the dock's (the Store's dialog wants the window's); it answers on another.
        _ = license.PurchaseProAsync((nint)SettingsOwner).ContinueWith(ended => proPurchased = LicenseView.Purchase(ended.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
    });

    /// <summary>§339: Restore a purchase from ⚙: the Store asked again; "Checking the license" until it answers, then what this copy is.</summary>
    private void AskStoreAgain()
    {
        proAsked = S.SettingsLicenseCheckingTitle;
        _ = license.RefreshAsync().ContinueWith(_ => proAsked = LicenseView.Title(license.Status), TaskScheduler.Default);
    }

    /// <summary>
    /// licence: "Get Karpo Pro" chosen in the add list or a refusal at <paramref name="from"/>: edit mode, or
    /// staying in it, with ⚙ › Karpo Pro open on that display (§340, Dock.EditEntries.cs). True when it was.
    /// </summary>
    private bool LicenseChosen(uint id, DockWindow? from)
    {
        if (id != CMD_GET_PRO)
        {
            return false;
        }
        OpenEditMenuLater(from, EditEntry.GetPro);
        return true;
    }
}
