// edit: every way into Karpo's settings from outside the edit bar (docs\widgets.md §340; the settings-in-
// the-dock plan's P6). Nothing starts a settings app any more: each way opens edit mode, the edit bar's
// menu that holds what it asked for open under its button, at the level it asked for:
//
//   Win+Alt+Enter on a bar's button          Taskbar ▾, on that bar's display (§352, §517)
//   the bar's menu's "Taskbar settings"       the same, on that bar's display (§581)
//   the Layouts face's "Layout settings"     §585 the Layouts page
//   a face's "Manage accounts"               §535 the Accounts page
//   "Get Karpo Pro" (add list, refusal)      ⚙ › Karpo Pro
//   a second start of Karpo.exe              ⚙, on the main display
//   the layouts line's "Add" (one layout)     §628 the Add a layout page, the presets (§485 to §627 the Layouts
//                                            page's "Add a layout" dropdown)
//   the Layouts page's "Add a layout"         §628 the same: a link to its page
//   an account's "Mail folders"              §646 its page under the Accounts entry, its inbox and every folder
//
// §351: and every link row of a menu (OptionRowKind.Link, Karpo.Core\OptionLinks.cs), to the one level that
// owns its setting (EditEntries.Of):
//
//   "Manage accounts" (a widget's, a face's) §535 the Accounts page, as the face's at rest
//   "Public holidays" (a calendar's)         ⚙ › Public holidays
//   "Appearance" (a folder room's)           §567 the Appearance page (§517 to §566 "Look"; §423 to §513 "Layout")
//
// §507: no styles, and no link to them (§485 to §506 "Styles" under each choice of a style, to ⚙ › Styles).
//
// Where each lands is one table (EditEntries.Landing), which the tests read against the menus' own rows. A
// widget waiting to be set up opens its own configuration menu instead, over its folder, or where it needs
// an account ⚙ › Accounts (Dock.Folders.cs).
//
// A second start (the Start menu, Explorer, a pin) finds the running dock's host, gives it its right to
// come to the front, posts it "karpo.settings" (DockHost.SettingsMessage) and ends (Program.cs). An
// automatic start — Windows' Run entry, the logon task, the guard's restart — asks nothing.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>edit: a way into Karpo's settings from outside the edit bar (§340, see the top of the file).</summary>
internal enum EditEntry
{
    /// <summary>The taskbar's settings: Win+Alt+Enter on a bar's button (§362); the bar's menu's "Taskbar settings" (gone in §366, back in §581).</summary>
    TaskbarSettings,

    /// <summary>The Layouts face's "Layout settings": §521 the Layouts page, §585 (§624) Layouts.</summary>
    LayoutSettings,

    /// <summary>A face's "Manage accounts".</summary>
    ManageAccounts,

    /// <summary>"Get Karpo Pro", the add list's and a refusal's.</summary>
    GetPro,

    /// <summary>A second start of Karpo.exe.</summary>
    SecondStart,

    /// <summary>§351: a calendar's "Public holidays".</summary>
    Holidays,

    /// <summary>§485: the layouts line's "Add" (one layout), §628 the Layouts page's "Add a layout": its page of the presets.</summary>
    AddLayout,

    /// <summary>§567: an "Appearance" link (a folder room's right click): the Appearance page.</summary>
    AppearanceSettings,

    /// <summary>§646: an account's "Mail folders" on the Accounts page: its page of the inbox and every folder.</summary>
    MailFolders,
}

/// <param name="Page">§520: the settings panel's page it opens on.</param>
/// <param name="Level">The key of the row it lands at (its menu level's, as OptionPick keys it); null for the page itself.</param>
internal readonly record struct EditLanding(PanelPage Page, string? Level = null);

/// <summary>edit: where each way in lands (§340).</summary>
internal static class EditEntries
{
    public static EditLanding Landing(EditEntry entry) => entry switch
    {
        // §517: the taskbar's own menu; the layouts are Display ▾'s own rows (§361).
        EditEntry.TaskbarSettings => new(PanelPage.Taskbar),
        EditEntry.AppearanceSettings => new(PanelPage.Appearance),
        EditEntry.LayoutSettings => new(PanelPage.Layouts),
        EditEntry.AddLayout => new(PanelPage.AddLayout),
        EditEntry.ManageAccounts => new(PanelPage.Accounts),
        EditEntry.MailFolders => new(PanelPage.MailFolders),
        // Karpo Pro's rows are one group, whose row is keyed by its first, the licence's state.
        EditEntry.GetPro => new(PanelPage.Karpo, KarpoMenu.ProStatusRow),
        EditEntry.Holidays => new(PanelPage.Karpo, OptionHolidays.Calendars),
        _ => new(PanelPage.Karpo),
    };

    /// <summary>§351: where a link row to <paramref name="link"/> goes.</summary>
    public static EditEntry Of(OptionLink link) => link switch
    {
        OptionLink.Accounts => EditEntry.ManageAccounts,
        OptionLink.Holidays => EditEntry.Holidays,
        OptionLink.AddLayout => EditEntry.AddLayout,
        OptionLink.MailFolders => EditEntry.MailFolders,
        // §567: the folder room's "Appearance ↗": the Appearance page itself.
        _ => EditEntry.AppearanceSettings,
    };
}

/// <summary>
/// edit: a second start of Karpo.exe while a dock runs (§340): it asks that dock for ⚙ Settings and ends.
/// </summary>
internal static unsafe class SecondStart
{
    /// <summary>The starts made by Windows or by Karpo itself, which ask nothing: the Run entry's, the logon task's, the guard's.</summary>
    internal static readonly string[] Automatic = [StartupRegistration.Argument, StartupRegistration.TaskArgument, Restarts.Switch, DockGuard.Switch];

    /// <summary>"karpo.settings", registered: what the dock's host hears a second start by.</summary>
    public static uint Message
    {
        get
        {
            fixed (char* name = DockHost.SettingsMessage)
            {
                return RegisterWindowMessageW(name);
            }
        }
    }

    /// <summary>
    /// Whether a second start with <paramref name="args"/> asks the running dock for ⚙: a person's start
    /// (the Start menu, Explorer, a pin), not an automatic one (<see cref="Automatic"/>), nor one on a
    /// config of its own (<paramref name="isolated"/>: a test's), which the running dock is not.
    /// </summary>
    public static bool Asks(IReadOnlyList<string> args, bool isolated) =>
        !isolated && !args.Any(arg => Automatic.Contains(arg, StringComparer.OrdinalIgnoreCase));

    /// <summary>Asks the running dock's host for ⚙, passing on this start's right to come to the front. Nothing where no dock runs.</summary>
    public static void Ask()
    {
        HWND host;
        fixed (char* name = DockHost.ClassName)
        {
            host = FindWindowW(name, null);
        }
        if (host == HWND.NULL)
        {
            return;
        }
        uint pid = 0;
        _ = GetWindowThreadProcessId(host, &pid);
        if (pid != 0)
        {
            _ = AllowSetForegroundWindow(pid);
        }
        _ = PostMessageW(host, Message, 0, 0);
    }
}

public sealed unsafe partial class Dock
{
    /// <summary>edit: <paramref name="entry"/>, picked in a menu of <paramref name="from"/>'s: once the menu has gone (Later).</summary>
    private void OpenEditMenuLater(DockWindow? from, EditEntry entry) => Later(() => OpenEditMenu(from, entry));

    /// <summary>
    /// edit: where <paramref name="entry"/> lands (<see cref="EditEntries.Landing"/>): edit mode, on the display
    /// of <paramref name="from"/> (a bar or a place of it), else the main one, and that display's edit bar's
    /// menu open under its button, at its level. A display that shows None has its ⚙ too, owned by the bar
    /// that holds edit mode; nothing where Karpo shows no bar at all.
    /// </summary>
    private void OpenEditMenu(DockWindow? from, EditEntry entry) => OpenEditLanding(from, EditEntries.Landing(entry));

    /// <summary>
    /// edit: <paramref name="landing"/> opened — edit mode on the display of <paramref name="from"/> (else the main one), its page's
    /// flyout open at its row —; the display it opened on, null where nothing opened (§652: Windows search's settings found).
    /// </summary>
    private string? OpenEditLanding(DockWindow? from, EditLanding landing)
    {
        var own = from is not null && windows.Contains(from) ? from : null;
        var display = own?.Id ?? (arranged.Find(monitor => monitor.Primary) ?? arranged.FirstOrDefault())?.Id;
        if (display is null || ((BarOf(display) ?? own ?? editor ?? windows.Find(window => window.Place is null)) is not { } bar))
        {
            return null;
        }
        BeginEdit(bar, display);
        if (!Arranging(bar))
        {
            return null;
        }
        OpenPanel(display, landing.Page, landing.Level);
        return display;
    }

    /// <summary>
    /// edit: the level of the shown menu's row keyed <paramref name="key"/> (its pick's), opened in place as a
    /// click on it opens it, those over it first (§400); nothing where the menu has no such row.
    /// </summary>
    private void OpenShownLevel(string key)
    {
        if (pending?.Options.Find(pick => pick.Kind == OptionPickKind.None && pick.Key == key) is { } level)
        {
            flyout?.OpenLevel(level.Id);
        }
    }
}
