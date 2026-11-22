// edit: the errors Karpo knows of, on the edit bar (docs\widgets.md §394; the person, 2026-10-29: "show the errors
// that happen in the main edit topbar and make them clickable so that they can be shown"). One list (Faults.cs),
// which the dock fills from where each failure is kept:
//
//   * the widgets' failed reads — every widget of the bars and of their folders that reads what it shows from
//     somewhere and could not (IRefreshable.Problem: its stale mark) —, read again
//     whenever a widget has news (IslandReady), the bars are built (Rebuild) and edit mode begins;
//   * the accounts' failed syncs, as the account layer last said them (Karpo.Accounts' SyncReport, which ⚙ ›
//     Accounts shows) — read again whenever a store says what it synced (SyncReport.Told) and edit mode begins;
//   * config.json: a file that does not read while it is being edited (ConfigReload.Broken), one that could not
//     be saved, what reading it at the start had to do (Config.Recovery) — said here as it happens, and over as
//     the file reads or saves well again. Before, all three went to a console a windowed Karpo does not have.
//
// The edit bar shows how many (its indicator before "Add", the critical colour for its mark alone, only there with
// errors), and a click on it opens their page under it (§689: PanelPage.Errors, styled as every settings page), newest
// first: each a card with its icon, what failed, why, and since when. A row opens what it is of: a widget selected, its settings beside it as a click on it opens them
// (§407) — a line of a folder with its folder opened arranged —; an account ⚙ › Accounts, where it signs in again;
// the settings ⚙. An error resolved goes from the list and the count by itself.

using Karpo.Accounts;
using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§394: the errors' list's rows' commands, one a row.</summary>
    private const uint CMD_FAULT = 3000;

    /// <summary>§394: the most rows the errors' list shows, newest first.</summary>
    private const int MostFaultRows = 50;

    /// <summary>§394: every error Karpo knows of now (Faults.cs): what the edit bar's indicator counts and its list shows.</summary>
    private readonly Faults faults = new();

    /// <summary>§394: what posts the account layer's news (SyncReport.Told, on a store's thread) to the dock's; null until hooked.</summary>
    private Action<SyncReport>? toldFaults;

    /// <summary>
    /// §394: the errors hooked: the account layer's news heard on the dock's thread; what reading config.json at
    /// the start had to do, an error of the settings' until the file is next saved well.
    /// </summary>
    private void HookFaults()
    {
        if (config.Recovery is { } recovery)
        {
            SettingsFault(FaultSources.Recovered, recovery);
        }
        var queue = Windows.System.DispatcherQueue.GetForCurrentThread();
        if (queue is null || toldFaults is not null)
        {
            return;
        }
        toldFaults = _ => queue.TryEnqueue(() => Troubles.Guard("Errors: an account's sync", () =>
        {
            HearAccountFaults();
            HearAccountParts();
        }));
        SyncReport.Told += toldFaults;
    }

    /// <summary>§394: the account layer's news no longer heard: the dock is closing.</summary>
    private void UnhookFaults()
    {
        if (toldFaults is not null)
        {
            SyncReport.Told -= toldFaults;
            toldFaults = null;
        }
    }

    /// <summary>
    /// §394: the widgets' errors read again: every widget the dock made and every one its folders hold, each that
    /// could not read what it shows (FaultSources.Widgets), named as the config has it.
    /// </summary>
    private void HearWidgetFaults()
    {
        if (disposed)
        {
            return;
        }
        var widgets = new List<(string Key, object Widget)>();
        foreach (var (key, island) in islands)
        {
            widgets.Add((key, island));
            if (island is FolderIsland folder)
            {
                widgets.AddRange(folder.Widgets.Select(line => (line.Key, (object)line)));
            }
        }
        FaultsSet(FaultKind.Widget, FaultSources.Widgets(widgets, key =>
            config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is { } island && SubjectOf(island) is var named ? (named.Name, named.Glyph) : null));
    }

    /// <summary>§394: the accounts' errors read again from what the account layer last said (none on a run that reads no account).</summary>
    private void HearAccountFaults()
    {
        if (disposed)
        {
            return;
        }
        var report = SyncReport.ForDock();
        FaultsSet(FaultKind.Account, report is null ? [] : FaultSources.Accounts(AccountBook.Load(), report.Read()));
    }

    /// <summary>
    /// §644: a widget's page standing open built again from what the account layer last said: its Show from's parts as now
    /// synced (a folder picked there says "Syncing…" until a read has it, then no more).
    /// </summary>
    private void HearAccountParts()
    {
        if (disposed || editor is null || panelOn is null || panelMemory.Page != PanelPage.Widget)
        {
            return;
        }
        StirChrome();
        PresentChrome();
    }

    /// <summary>§394: <paramref name="kind"/>'s errors are <paramref name="now"/>: the edit bars follow where the count changed.</summary>
    private void FaultsSet(FaultKind kind, IEnumerable<FaultNow> now)
    {
        if (faults.Set(kind, now, Clock.RealUtc()) && editor is not null)
        {
            StirChrome();
        }
    }

    /// <summary>§394: an error of Karpo's settings (config.json), keyed <paramref name="key"/>, saying <paramref name="message"/>.</summary>
    private void SettingsFault(string key, string message)
    {
        if (faults.Raise(new FaultNow(key, FaultKind.Settings, S.DockEditSettings, OptionGlyphs.Settings, message), Clock.RealUtc()) && editor is not null)
        {
            StirChrome();
        }
    }

    /// <summary>§394: the settings' error keyed <paramref name="key"/> is over.</summary>
    private void SettingsFine(string key)
    {
        if (faults.Clear(key) && editor is not null)
        {
            StirChrome();
        }
    }

    /// <summary>
    /// §689: the errors' page's rows, <paramref name="shown"/> newest first: each a link (its id the list's row's), its icon,
    /// what failed, why its second line, since when at its end — a card as every page's row.
    /// </summary>
    internal static List<FlyoutItem> FaultItems(IReadOnlyList<Fault> shown, DateTime now) =>
        [.. shown.Select((fault, index) => new FlyoutItem(CMD_FAULT + (uint)index, fault.Name, Glyph(fault.Glyph), Detail: fault.Message,
            Value: new FlyoutValue(FaultSince(fault.SinceUtc, now)), Opens: FaultOpens) { DetailUnder = true })];

    /// <summary>§689: what an error's row opens, as a link's says (its page's own handling: <see cref="OpenFault"/>).</summary>
    private const string FaultOpens = "fault";

    /// <summary>§394: since when an error has been, as its row says it: the time today, the day before.</summary>
    internal static string FaultSince(DateTime sinceUtc, DateTime now)
    {
        var at = sinceUtc.ToLocalTime();
        return at.Date == now.Date ? Panels.Nls.Time(at) : Panels.Nls.MonthDay(DateOnly.FromDateTime(at));
    }

    /// <summary>§394, §689: a row of the errors' page followed: what it is of opens.</summary>
    private void FaultChosen(Pending what, uint id)
    {
        if (id < CMD_FAULT || id - CMD_FAULT >= (uint)what.Faults.Count)
        {
            return;
        }
        var fault = what.Faults[(int)(id - CMD_FAULT)];
        var display = what.Target;
        Later(() => OpenFault(fault, display));
    }

    /// <summary>
    /// §394: what <paramref name="fault"/> is of, opened from display <paramref name="displayId"/>'s edit bar: a
    /// widget's settings, beside it (<see cref="ShowWidget"/>); an account's ⚙ › Accounts, where it signs in
    /// again and its parts say what went wrong; the settings' ⚙.
    /// </summary>
    private void OpenFault(Fault fault, string displayId)
    {
        if (editor is null)
        {
            return;
        }
        switch (fault.Kind)
        {
            case FaultKind.Widget:
                ShowWidget(fault.Of);
                break;
            case FaultKind.Account:
                OpenEditMenu(BarOf(displayId) ?? editor, EditEntry.ManageAccounts);
                break;
            default:
                OpenPanel(displayId, PanelPage.Karpo);
                break;
        }
    }

    /// <summary>
    /// §394: the widget keyed <paramref name="key"/> shown: a widget of a bar selected (<see cref="EditPick"/>), the
    /// folder open closing first, which locks the bars; a line of a folder of a bar with its folder opened arranged,
    /// selected; §520 its settings the settings panel's Widget page, open on its display. Nothing for a widget no bar shows.
    /// </summary>
    private void ShowWidget(string key)
    {
        if (windows.Find(window => Arranging(window) && window.Layout.PartIndex(key) >= 0) is { } bar)
        {
            if (ArrangedFolder() is not null)
            {
                EscapeTo(EditEscape.CloseFolder);
            }
            EditPick(key);
            OpenPanel(bar.Id, PanelPage.Widget);
            return;
        }
        foreach (var window in windows.Where(Arranging).ToList())
        {
            if (window.Layout.Specs.Select(spec => spec.Key).FirstOrDefault(face => key.StartsWith(face + "/", StringComparison.OrdinalIgnoreCase)) is not { } folder)
            {
                continue;
            }
            if (ArrangedFolder() is { } other && !string.Equals(other.Key, folder, StringComparison.OrdinalIgnoreCase))
            {
                EscapeTo(EditEscape.CloseFolder);
            }
            OpenArranged(window, folder);
            if (Opened(folder) is { } opened && opened.Flyout.LineOf(key) is not null)
            {
                LineSelected(opened, () => EditPick(key));
                OpenPanel(window.Id, PanelPage.Widget);
            }
            return;
        }
    }
}
