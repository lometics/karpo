// menus: what a taskbar entry's right click offers at rest, and a folder's,
// face by face (the person, 2026-10-11: "most of the right click context menus of the taskbar
// widgets are empty, they should not be, they should show options related to the widget face, if
// there are multiple faces it should show all of them in order").
//
// **A folder.** Each face it wears has its rows (ActsFor(FaceInfo)), worked on the widget the face
// shows (FolderIsland.WidgetOf): the clock's date and time and the Clock app; the weather's
// place; the Wi-Fi's radio, as Windows' network icon has its troubleshooter and its page;
// the sound's mute and Windows' volume icon's rows; a timer's start and stop on the line it shows;
// the battery's power modes and energy saver; the desktops' new, rename and delete; the layouts' to
// choose from; and so on — the table is ActsFor(FaceInfo), in the order a face's own panel or
// Windows' own icon has them. A folder of one face shows that face's rows as they are. Of several,
// each face's rows follow the last's in the faces' order, a rule between two and no heading (the
// person, 2026-10-22: "when i right click a folder it shows its context menu options, it should not
// show a title for each face"; §257 had the face's name over its rows): the menu reads as the bar
// does, left to right, as Windows' own icons' menus are rows alone. A row that reads as one above it
// does — the clock's and the calendar's "Date & time settings" — is the same command and stands once,
// with the last face that offers it.
// A content or icon face is the folder's contents and has no rows of its own: a folder with nothing
// else to offer has the floor's "Edit folder", edit mode with the folder open in it, last and only then
// (Dock.MenuFloor.cs; the person, 2026-10-23: "no 'Edit folder' as filler"). What reads from the web
// has where it comes from instead (§308): the weather's and the air quality's forecast, the tides' and
// the earthquakes' map, the markets', the news' and the sports' website; never "Refresh", which is the
// line's button's alone (§315).
//
// **Windows' own widgets that wear Windows' face** (§474: the Widgets button, quick settings' button, the
// clock) have their faces' rows as a folder of those faces would, Windows' own icons' rows.
//
// **Any other entry.** Its kind's rows (ActsFor(BarKind)): the Snipping Tool's snips, Task view's new
// desktop, the volume slider's mixer, and for every entry Windows keeps a page for, that page — the
// link the settings app shows under it, word for word. An entry Windows has no page of its own for
// opens Windows' taskbar settings, where it is switched on and off. So no right click on the bar is
// an empty menu.
//
// Every row is something Windows or the widget does there and then, or a page; none opens a popup
// of Karpo's, as the folder rule asks of a line's menu (docs\widgets.md §15), and none changes the
// widget: its settings, a face's place among them, are edit mode's menu's (§253). Karpo's own few —
// a folder edited, the accounts, the layouts — are the dock's (FaceActChosen).

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// What a folder's face menu takes from the dock that no face holds: the desktops' rows
/// (<see cref="Dock.DesktopRows"/>) and the layouts' (<see cref="Dock.LayoutRows"/>).
/// </summary>
internal sealed record FaceMenuParts(IReadOnlyList<FlyoutItem>? Desktops = null, IReadOnlyList<FlyoutItem>? Layouts = null);

public sealed unsafe partial class Dock
{
    /// <summary>Windows' taskbar settings: the page of an entry Windows keeps no page of its own for.</summary>
    internal static readonly WindowsLink TaskbarPage = WindowsPages.Taskbar;

    /// <summary>
    /// What a taskbar entry of <paramref name="kind"/> offers at rest: the Snipping Tool's
    /// own, as its line has them; Task view's new desktop; the volume slider's Windows' volume icon's
    /// rows; the Settings button's Windows Update; and the page Windows keeps for every other entry
    /// (<see cref="PageOf(BarKind)"/>).
    /// </summary>
    internal static IReadOnlyList<WidgetAct> ActsFor(BarKind kind) => kind switch
    {
        BarKind.SnippingTool => [.. SnippingActs, WidgetAct.WindowsSettings],
        // Its click is Task view: a new desktop, gone to as Win+Ctrl+D does, and its page.
        BarKind.TaskView => [WidgetAct.NewDesktop, WidgetAct.WindowsSettings],
        BarKind.Volume => SoundActs,
        BarKind.WindowsSettings => [WidgetAct.WindowsUpdate],
        // keep awake: its line's, the lengths and the screen, then its page (§269).
        BarKind.KeepAwake => AwakeActs,
        // Menus of their own: the status icons' cells' (IWidgetMenu), the Start button's quick links,
        // a pin's and an open app's jump list; the apps' empty room is the bar's. Where one is asked
        // for all the same, the floor gives it its page (Dock.MenuFloor.cs).
        BarKind.Indicators or BarKind.Start or BarKind.Pin or BarKind.Apps or BarKind.FolderList => [],
        _ => [WidgetAct.WindowsSettings],
    };

    /// <summary>The page Windows keeps for <paramref name="kind"/>'s entry (<see cref="WindowsLinkOf"/>), else its taskbar settings.</summary>
    internal static WindowsLink PageOf(BarKind kind) => WindowsLinkOf(BarEntries.Find(kind)?.Info) ?? TaskbarPage;

    // Windows' volume icon's rows, in its order and words.
    private static readonly WidgetAct[] SoundActs = [WidgetAct.OpenVolumeMixer, WidgetAct.SoundSettings, WidgetAct.TroubleshootSound];

    /// <summary>
    /// What a face offers on its folder's right click, in the order its own panel or Windows' own icon
    /// has them (see the top of the file). Every face has something: a face whose widget is missing
    /// (no timer in the folder, no drive line) still has the app or the page its rows lead to.
    /// </summary>
    internal static IReadOnlyList<WidgetAct> ActsFor(FaceInfo face) => face.Id switch
    {
        FolderFaces.IconId => [],
        FolderFaces.ClockId => [WidgetAct.DateTimeSettings, WidgetAct.ClockApp],
        FolderFaces.CalendarId => [WidgetAct.CalendarApp, WidgetAct.DateTimeSettings],
        // meeting: the meeting it shows joined (§206), the calendar app, and the accounts whose calendars it reads (§205).
        FolderFaces.MeetingId => [WidgetAct.JoinMeeting, WidgetAct.CalendarApp, WidgetAct.ManageAccounts],
        // Windows 11's own clock's two rows.
        FolderFaces.DateTimeId => [WidgetAct.DateTimeSettings, WidgetAct.NotificationSettings],
        // weather, air (§210), tides (§236), quakes (§240), markets, news and sports: where it comes
        // from (§308; no refresh, §315); the place each is for is edit mode's (§253).
        FolderFaces.WeatherId or FolderFaces.AirQualityId => ForecastActs,
        FolderFaces.TidesId or FolderFaces.QuakesId => MapActs,
        FolderFaces.MarketsId or FolderFaces.NewsId or FolderFaces.SportsId => WebsiteActs,
        FolderFaces.WifiId => [WidgetAct.WifiRadio, WidgetAct.DiagnoseNetwork, WidgetAct.NetworkSettings],
        FolderFaces.BluetoothId => [WidgetAct.BluetoothRadio, WidgetAct.WindowsSettings],
        FolderFaces.SoundId => [WidgetAct.Mute, .. SoundActs],
        FolderFaces.RemovableId => [.. ActsFor(ListKind.RemovableDevices), WidgetAct.AutoPlaySettings],
        FolderFaces.TimerId or FolderFaces.FocusId => [.. ActsFor(ListKind.Timer), WidgetAct.ClockApp],
        FolderFaces.StopwatchId => [.. ActsFor(ListKind.Stopwatch), WidgetAct.ClockApp],
        // Windows' hotspot icon's way to its settings: a switch on the bar would be a click away
        // from turning it off by mistake (§93).
        FolderFaces.HotspotId => [WidgetAct.HotspotSettings],
        FolderFaces.CastId => [WidgetAct.CastConnect, WidgetAct.WindowsSettings],
        FolderFaces.NearbyId => [WidgetAct.WindowsSettings],
        FolderFaces.BatteryId => [WidgetAct.PowerMode, WidgetAct.EnergySaver, WidgetAct.PowerSettings],
        FolderFaces.PowerId => [WidgetAct.PowerSettings],
        FolderFaces.MailId => [WidgetAct.ManageAccounts],
        FolderFaces.NowPlayingId => [WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.OpenPlayer],
        FolderFaces.CpuId or FolderFaces.GpuId or FolderFaces.MemoryId or FolderFaces.DiskId or FolderFaces.NetworkActivityId
            => [WidgetAct.TaskManager, WidgetAct.ResourceMonitor],
        FolderFaces.DriveId => [.. ActsFor(ListKind.Drive), WidgetAct.StorageSettings],
        FolderFaces.NetworkDriveId => [.. ActsFor(ListKind.NetworkDrive), WidgetAct.FileExplorer],
        FolderFaces.NotificationsId => ActsFor(ListKind.NotificationsList),
        FolderFaces.ClipboardId => [WidgetAct.ClipboardHistory, WidgetAct.WindowsSettings],
        FolderFaces.ContactsId => [WidgetAct.ManageAccounts],
        FolderFaces.KeyboardId => [WidgetAct.WindowsSettings],
        FolderFaces.VpnId => [WidgetAct.WindowsSettings],
        FolderFaces.LayoutsId => [WidgetAct.LayoutSettings],
        // The desktops' are the switcher's own commands (FaceRows), not acts of this table.
        FolderFaces.DesktopsId => [],
        _ => [],
    };

    /// <summary>The page in Windows a face's "… settings" row opens: the widget's its face stands for; null for none.</summary>
    internal static WindowsLink? PageOf(FaceInfo face) => face.Id switch
    {
        FolderFaces.BluetoothId => WidgetInfos.Bluetooth.Settings,
        FolderFaces.CastId => WidgetInfos.WirelessDisplays.Settings,
        FolderFaces.NearbyId => WidgetInfos.NearbySharing.Settings,
        FolderFaces.ClipboardId => WidgetInfos.Clipboard.Settings,
        FolderFaces.KeyboardId => WidgetInfos.Keyboard.Settings,
        FolderFaces.VpnId => WidgetInfos.Vpn.Settings,
        _ => null,
    };

    /// <summary>
    /// A taskbar entry's own rows at rest: a folder's for each of its faces
    /// (<see cref="FaceRows"/>), worked on <paramref name="folder"/>'s widgets, with the dock's
    /// <paramref name="parts"/>; any other entry's kind's (<see cref="ActsFor(BarKind)"/>), worked on
    /// <paramref name="widget"/>, the entry as it is now (keep awake's lengths read its options). None
    /// for a widget the file has lost.
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Acts) BarMenuRows(BarItem? island, FolderIsland? folder = null, FaceMenuParts? parts = null, IIsland? widget = null)
    {
        if (island is null)
        {
            return ([], []);
        }
        if (island.Folder is { } options)
        {
            return FaceRows(options, folder, parts);
        }
        // §474: one of Windows' own widgets has its faces' rows, as Windows' own icons have them — the clock's, the
        // network's, the volume's —, never a row that changes it; its kind's where they have none (the weather's, which
        // leads to its forecast's site, has none without a forecast).
        if (island.Kind.WearsWindowsFace() && FaceRows(folder?.Options ?? WindowsFaces.Of(island.Kind, WidgetMachine.Read().HasBattery, WindowsBatteryPercent.On)!, folder, parts) is { Items.Count: > 0 } faces)
        {
            return faces;
        }
        var (items, rows) = ActRows(ActsFor(island.Kind), widget, PageOf(island.Kind));
        return (items, [.. rows.Select(row => row with { Works = row.Works ?? widget })]);
    }

    /// <summary>
    /// A folder's rows, face by face (see the top of the file): each face's
    /// (<see cref="ActsFor(FaceInfo)"/>) worked on its own widget (<see cref="FolderIsland.WidgetOf"/>;
    /// none where <paramref name="island"/> is null, as for a test), numbered one after another, a rule
    /// between two faces' and no heading; a row reading as one a later face offers stands with that face
    /// alone (a bell's "Notification settings" with its rows). A face with nothing
    /// to show now — no layout to choose and no page — is left out.
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Acts) FaceRows(FolderOptions folder, FolderIsland? island, FaceMenuParts? parts = null)
    {
        var all = new List<FlyoutItem>();
        var acts = new List<WidgetActRow>();
        var groups = new List<List<FlyoutItem>>();
        foreach (var face in folder.Faces)
        {
            var info = face.Info;
            var widget = island?.WidgetOf(face);
            List<FlyoutItem> items = [];
            switch (info.Id)
            {
                // The switcher's own commands on the desktop you are on, which the face names.
                case FolderFaces.DesktopsId:
                    items.AddRange(parts?.Desktops ?? []);
                    break;
                // The layouts to choose from, where there are two or more, before their page.
                case FolderFaces.LayoutsId when parts?.Layouts is { Count: > 0 } layouts:
                    items.AddRange(layouts);
                    items.Add(new FlyoutItem(0, "", Separator: true));
                    break;
            }
            var (own, rows) = ActRows(ActsFor(info), widget, PageOf(info), first: acts.Count, face);
            items.AddRange(own);
            acts.AddRange(rows.Select(row => row with { Works = row.Works ?? widget }));
            groups.Add(items);
        }
        // The same command several faces offer stands once, with the last that offers it: a bell's
        // "Notification settings" with the bell's rows, not with the date and time's before them.
        for (var at = 0; at < groups.Count; at++)
        {
            var later = groups.Skip(at + 1).SelectMany(group => group).Where(row => row.Id != 0).Select(row => row.Text).ToHashSet();
            groups[at].RemoveAll(row => row.Id != 0 && later.Contains(row.Text));
            Tidy(groups[at]);
            if (groups[at].Exists(item => !item.Header && !item.Separator))
            {
                if (all.Count > 0)
                {
                    all.Add(new FlyoutItem(0, "", Separator: true));
                }
                all.AddRange(groups[at]);
            }
        }
        // A row that stands once leaves its earlier face's act with it: an act for every row, and none for a row not there.
        return (all, [.. acts.Where(act => all.Exists(item => item.Id == act.Id))]);
    }

    /// <summary>
    /// A face's name in edit mode's menu, its row and the head of its level (OptionMenu.FaceSection): its
    /// name, and a clock's label where it has one, so two clocks in other zones read apart.
    /// </summary>
    internal static string TitleOf(FolderFace face) =>
        face.Info == FolderFaces.Clock && ClockFace.Read(face).Label is { Length: > 0 } label ? $"{face.Info.Title} · {label}" : face.Info.Title;

    /// <summary>No rule at either end of <paramref name="items"/>, nor two together.</summary>
    private static void Tidy(List<FlyoutItem> items)
    {
        for (var at = items.Count - 1; at >= 0; at--)
        {
            if (items[at].Separator && (at == 0 || at == items.Count - 1 || items[at - 1].Separator))
            {
                items.RemoveAt(at);
            }
        }
    }

    /// <summary>What a folder's face menu on <paramref name="window"/> takes from the dock: the desktops' rows and the layouts'.</summary>
    private FaceMenuParts FacePartsFor(DockWindow window) => new(DesktopRows(CurrentDesktopActions()), LayoutItems(window));

    /// <summary>
    /// A face's row that is Karpo's to do (<see cref="WidgetAct.EditFolder"/>, the accounts, the
    /// layouts), done for the folder keyed <paramref name="key"/> on
    /// <paramref name="window"/>; false for every other act, which RunAct does.
    /// </summary>
    private bool FaceActChosen(DockWindow window, WidgetAct act, string key)
    {
        switch (act)
        {
            // Edit mode, and the folder open in it, as a click on it there opens it, and picked; a
            // line's, the folder it stands in (the floor's: Dock.MenuFloor.cs).
            case WidgetAct.EditFolder:
                key = FolderKeyOf(key);
                BeginEdit(window);
                if (Arranging(window) && window.Layout.PartIndex(key) is var part and >= 0)
                {
                    EditPick(key);
                    if (window.IslandAt(Hit.Cell(part, 0)) is FolderIsland folder && Opened(folder.Key) is null)
                    {
                        ShowFolder(window, Hit.Cell(part, 0));
                    }
                }
                return true;
            // §340: ⚙ › Accounts; the Layouts face's own, Display ▾ and its layouts (Dock.EditEntries.cs, §361).
            case WidgetAct.ManageAccounts:
                OpenEditMenuLater(window, EditEntry.ManageAccounts);
                return true;
            case WidgetAct.LayoutSettings:
                OpenEditMenuLater(window, EditEntry.LayoutSettings);
                return true;
            default:
                return false;
        }
    }
}
