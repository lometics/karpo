// folders: what a right click on a widget in a folder offers (Karpo.Dock\Dock.WidgetMenus.cs), and
// what becomes of the folder once a row is picked — decisions, not drawings, so tested as such.
//
// The person: "implement rightclick context actions for all the grid and list widgets if
// applicable. (for all widgets on right click it should not close the folder anymore.)" Every row
// either acts there and then or hands Windows something to open; none opens a popup of Karpo's,
// and none does what the widget's own click already does.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using AfterMenu = Karpo.Dock.Dock.AfterMenu;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

// The Snipping Tool's rows are greyed and its "Get Snipping Tool" shown by what is installed
// (WindowsButtons.VersionOf), a seam ShortcutWidgetTests sets: the table is held with both installed,
// in the seams' collection, which runs alone.
[Collection(WindowsButtonSeams.Collection)]
public sealed class WidgetMenuTests
{
    private static readonly WidgetAct[] Nothing = [];
    // What reads from the web: where it comes from (§308); never a refresh (§315).
    private static readonly WidgetAct[] Weather = [WidgetAct.OpenForecast];
    private static readonly WidgetAct[] Map = [WidgetAct.OpenMap];
    private static readonly WidgetAct[] Feed = [WidgetAct.OpenWebsite];
    private static readonly WidgetAct[] Graph = [WidgetAct.ResourceMonitor];
    // The timeline's steps too: only a line of four rows or more shows the timeline itself.
    private static readonly WidgetAct[] Playing = [WidgetAct.FollowSession, WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.Shuffle, WidgetAct.Repeat, WidgetAct.SeekBack, WidgetAct.SeekForward, WidgetAct.OpenPlayer];
    private static readonly WidgetAct[] LocalDrive = [WidgetAct.OpenDrive, WidgetAct.Eject, WidgetAct.DriveProperties];
    private static readonly WidgetAct[] MappedDrive = [WidgetAct.OpenDrive, WidgetAct.Disconnect, WidgetAct.DriveProperties];
    private static readonly WidgetAct[] Devices = [WidgetAct.OpenDrive, WidgetAct.Eject, WidgetAct.DriveProperties];
    private static readonly WidgetAct[] Calendar = [WidgetAct.CalendarApp];

    // calendar: "Join meeting" first, over a meeting with a Join (docs\widgets.md §206); the year lists no meetings.
    private static readonly WidgetAct[] Meetings = [WidgetAct.JoinMeeting, .. Calendar];
    private static readonly WidgetAct[] Volume = [WidgetAct.Mute, WidgetAct.OpenVolumeMixer, WidgetAct.WindowsSettings];

    // The devices first: a short line shows the one in use alone.
    private static readonly WidgetAct[] Output = [WidgetAct.ChooseDevice, WidgetAct.Mute, WidgetAct.OpenVolumeMixer, WidgetAct.WindowsSettings];
    private static readonly WidgetAct[] Page = [WidgetAct.WindowsSettings];
    private static readonly WidgetAct[] Countdown = [WidgetAct.StartPause, WidgetAct.Stop, WidgetAct.Longer, WidgetAct.Shorter];
    private static readonly WidgetAct[] Notifications = [WidgetAct.ClearAll, WidgetAct.DoNotDisturb, WidgetAct.NotificationSettings];
    private static readonly WidgetAct[] TaskView = [WidgetAct.NewDesktop, WidgetAct.TaskView];
    private static readonly WidgetAct[] Snipping = [WidgetAct.GetSnippingTool, WidgetAct.OpenSnippingTool, WidgetAct.SnipIn3, WidgetAct.SnipIn10, WidgetAct.RecordScreen];

    /// <summary>What every list line offers beside its Settings (and, while arranged, Duplicate and Remove).</summary>
    private static readonly Dictionary<ListKind, WidgetAct[]> Lines = new()
    {
        // The weather face's, the stock face's and the mail face's rows, a line's as a face's (Dock.FaceMenus.cs).
        [ListKind.WeatherNow] = Weather,
        [ListKind.Markets] = Feed,
        [ListKind.Rss] = Feed,
        // sports: the match on ESPN (§239).
        [ListKind.Sports] = Feed,
        [ListKind.SportsFixtures] = Feed,
        [ListKind.SportsTable] = Feed,
        [ListKind.SportsKnockout] = Feed,
        // §641: a Start widget's own rows, for what is under the pointer (StartGroupTests).
        [ListKind.StartPins] = [WidgetAct.Start],
        [ListKind.StartApps] = [WidgetAct.Start],
        [ListKind.StartRecommended] = [WidgetAct.Start],
        [ListKind.StartProfile] = [WidgetAct.Start],
        // §642: Windows search acts on its rows in the line itself (their buttons, the pane's actions); its menu the floor's.
        [ListKind.WinSearch] = Nothing,
        [ListKind.Cpu] = Graph,
        [ListKind.Gpu] = Graph,
        [ListKind.Memory] = Graph,
        [ListKind.Disk] = Graph,
        [ListKind.Network] = Graph,
        [ListKind.CpuDetails] = Graph,
        [ListKind.GpuDetails] = Graph,
        [ListKind.MemoryDetails] = Graph,
        [ListKind.DiskDetails] = Graph,
        [ListKind.NetworkDetails] = Graph,
        // The app right-clicked on, ended or its file shown, then the graphs' row (§141).
        [ListKind.CpuProcesses] = [WidgetAct.EndTask, WidgetAct.OpenFileLocation, WidgetAct.ResourceMonitor],
        // A pin's rows are its target's, on the bar as in a folder (Dock.PinRows), none of its kind's.
        [ListKind.Pin] = Nothing,
        [ListKind.Media] = Playing,
        // The apps playing: a row is picked on the line itself; what is playing's controls are here.
        [ListKind.MediaSources] = [WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.OpenPlayer],
        [ListKind.Drive] = LocalDrive,
        [ListKind.NetworkDrive] = MappedDrive,
        [ListKind.RemovableDevices] = Devices,
        // §380: a folder's contents open their items with a click; the folder shown opens in File Explorer from here alone.
        [ListKind.FolderContents] = [WidgetAct.OpenInExplorer],
        [ListKind.Calendar] = Meetings,
        [ListKind.SoundOutput] = Output,
        [ListKind.SoundInput] = [WidgetAct.ChooseDevice, WidgetAct.MuteMicrophone, WidgetAct.WindowsSettings],
        // Each app's mute, for the apps a short line leaves out.
        [ListKind.SoundMixer] = [WidgetAct.MuteApp, WidgetAct.OpenVolumeMixer, WidgetAct.WindowsSettings],
        [ListKind.SoundVolume] = Volume,
        [ListKind.Brightness] = Page,
        [ListKind.SpatialSound] = Page,
        [ListKind.Battery] = [WidgetAct.EnergySaver, WidgetAct.WindowsSettings],
        [ListKind.AirplaneMode] = Page,
        [ListKind.WifiSwitch] = Page,
        [ListKind.BluetoothSwitch] = Page,
        [ListKind.SoundSwitch] = Page,
        [ListKind.MicrophoneSwitch] = Page,
        [ListKind.GameMode] = Page,
        [ListKind.ColorFilters] = Page,
        [ListKind.MonoAudio] = Page,
        [ListKind.DarkMode] = Page,
        [ListKind.Magnifier] = Page,
        [ListKind.Narrator] = Page,
        [ListKind.StickyKeys] = Page,
        // keep awake: how long and the screen, which its click does not choose, then its page (§269).
        [ListKind.KeepAwake] = [WidgetAct.AwakeFor, WidgetAct.WindowsSettings],
        [ListKind.MobileHotspot] = Page,
        [ListKind.Vpn] = Page,
        [ListKind.RotationLock] = Page,
        [ListKind.EnergySaver] = Page,
        [ListKind.NightLight] = Page,
        [ListKind.LiveCaptions] = Page,
        [ListKind.DoNotDisturb] = Page,
        // A real switch since §115, with its page in Windows as the others have.
        [ListKind.NearbyShare] = Page,
        [ListKind.Accessibility] = Nothing,
        // Windows' Cast and Project tiles: the display's settings, where both lead.
        [ListKind.Cast] = Page,
        [ListKind.Project] = Page,
        [ListKind.Security] = Nothing,
        [ListKind.Clock] = [WidgetAct.DateTimeSettings],
        [ListKind.Timer] = Countdown,
        [ListKind.Focus] = Countdown,
        [ListKind.Stopwatch] = [WidgetAct.StartPause, WidgetAct.Lap, WidgetAct.Reset],
        [ListKind.NotificationsList] = Notifications,
        [ListKind.Mail] = [WidgetAct.ManageAccounts],
        [ListKind.ScreenTime] = [WidgetAct.OpenApp],
        [ListKind.ScreenTimeHistory] = [WidgetAct.OpenApp],
        [ListKind.ScreenTimeApps] = [WidgetAct.OpenApp],
        [ListKind.ScreenTimeChart] = [WidgetAct.OpenApp],
        [ListKind.TaskView] = TaskView,
        [ListKind.SnippingTool] = Snipping,
        // The Settings button's own, on the bar and in a list.
        [ListKind.WindowsSettings] = [WidgetAct.WindowsUpdate],
        // A title: naming it is its settings, and moving it the floor's (MenuFloorTests).
        [ListKind.Heading] = Nothing,
        // A line: its style is edit mode's (§253), and moving it the floor's.
        [ListKind.Divider] = Nothing,
        // A flyout made of widgets: the clock's header has the clock's page, a month the calendar's app and
        // the date's page; the rest are their own controls.
        [ListKind.Toggles] = Nothing,
        [ListKind.Actions] = Nothing,
        [ListKind.Month] = [WidgetAct.CalendarApp, WidgetAct.DateTimeSettings],
        [ListKind.TimeHeader] = [WidgetAct.DateTimeSettings],
        [ListKind.WeatherHours] = Weather,
        [ListKind.WeatherDays] = Weather,
        [ListKind.DateHeader] = [WidgetAct.DateTimeSettings],
        [ListKind.Events] = Meetings,
        [ListKind.Meeting] = Meetings,
        [ListKind.AirQuality] = Weather,
        [ListKind.AirPollutants] = Weather,
        [ListKind.AirPollen] = Weather,
        [ListKind.AirForecast] = Weather,
        [ListKind.WeatherHoursList] = Weather,
        [ListKind.WeatherDaysGraph] = Weather,
        [ListKind.Tides] = Map,
        [ListKind.TideGraph] = Map,
        [ListKind.Quakes] = Map,
        // camera: its page in the browser (§331).
        [ListKind.Camera] = Feed,
        // sun: nothing to act on; its place is its setting (§368), each of its times' too (§404).
        [ListKind.SunArc] = Nothing,
        [ListKind.SunDayBar] = Nothing,
        [ListKind.Sunrise] = Nothing,
        [ListKind.Sunset] = Nothing,
        [ListKind.Daylight] = Nothing,
        [ListKind.SolarNoon] = Nothing,
        [ListKind.Dawn] = Nothing,
        [ListKind.Dusk] = Nothing,
        [ListKind.DayView] = Meetings,
        [ListKind.WeekView] = Meetings,
        [ListKind.YearView] = Calendar,
        // notes: everything else is done in the line itself; which list or note is the line's settings.
        [ListKind.Todo] = [WidgetAct.ClearCompleted],
        [ListKind.Note] = Nothing,
        // The network icon's own rows, where its page is now; Bluetooth's page in Windows.
        [ListKind.Wifi] = [WidgetAct.DiagnoseNetwork, WidgetAct.NetworkSettings],
        [ListKind.Bluetooth] = Page,
        // The mobile hotspot's page in Windows, as Bluetooth's line has its own (§93).
        [ListKind.Hotspot] = Page,
        [ListKind.PowerMode] = Page,
        // Cast's panel and Win+P's modes, the display settings; nearby sharing's page (§114, §115).
        [ListKind.WirelessDisplays] = Page,
        [ListKind.Projection] = Page,
        [ListKind.NearbySharing] = Page,
        [ListKind.Keyboard] = Page,
        [ListKind.VpnConnections] = Page,
        // The layouts are Karpo's own: no page of Windows' for them, Karpo's Layouts page.
        [ListKind.Layouts] = [WidgetAct.LayoutSettings],
        // power: a button is its click; Power & battery where Windows keeps a page for it.
        [ListKind.Sleep] = Page,
        [ListKind.Hibernate] = Page,
        [ListKind.ShutDown] = Page,
        [ListKind.Restart] = Page,
        [ListKind.SignOut] = Nothing,
        [ListKind.Lock] = Nothing,
        // The clipboard face's rows, and the line's own "Clear all".
        [ListKind.Clipboard] = [WidgetAct.ClearAll, WidgetAct.ClipboardHistory, WidgetAct.WindowsSettings],
        // pim: the person under the pointer — write, call, copy — then the accounts, as the mail's are.
        [ListKind.Contacts] = [WidgetAct.ContactEmail, WidgetAct.ContactCall, WidgetAct.ContactCopy, WidgetAct.ManageAccounts],
        // An alarm's switch and its ringing buttons are on its line; Windows' Clock app beside them.
        [ListKind.Alarm] = [WidgetAct.ClockApp],
        // desktops: what Windows does with desktops besides the rows: Task view and a new one gone to, as Task view's line.
        [ListKind.Desktops] = TaskView,
    };

    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Select(row => row.Separator ? "—" : row.Text)];

    /// <summary>
    /// The table: every line of a list, and the rows its right click gives
    /// at rest — the kind's own acts alone, no Cut, Copy, Remove nor settings (§253). A kind added to either registry without a word here fails, so none ships
    /// with a menu nobody decided on.
    /// </summary>
    [Fact]
    public void EveryLineHasTheMenuItsKindWasGiven()
    {
        var installed = WindowsButtons.VersionOf;
        WindowsButtons.VersionOf = _ => new Version(11, 2607);
        try
        {
            EveryLine();
        }
        finally
        {
            WindowsButtons.VersionOf = installed;
        }
    }

    private static void EveryLine()
    {
        Assert.Equal(Enum.GetValues<ListKind>().Order(), Lines.Keys.Order());
        foreach (var (kind, acts) in Lines)
        {
            Assert.True(acts.SequenceEqual(TheDock.ActsFor(kind)), $"{kind}: {string.Join(", ", TheDock.ActsFor(kind))}");
            var line = kind.New();
            Assert.True(acts.SequenceEqual(TheDock.ActsFor(line)), $"{kind} as a line");
            AssertMenu(line, acts, kind.ToString());
        }
    }

    /// <summary>
    /// The menu a widget in a folder shows at rest, before anything is known of it live: every act
    /// that needs no widget has its row, and nothing else: Cut, Copy, Remove and the settings are the
    /// menu's while the bar is arranged (§253). The acts whose rows depend on the widget as it is — a drive
    /// that is there, a pin's file, the sound devices and apps there are — are the offscreen tests'
    /// (FolderMenuTests, ListMenuCoverageTests).
    /// </summary>
    private static void AssertMenu(CellItem cell, WidgetAct[] acts, string kind)
    {
        var (items, rows) = TheDock.FolderMenuRows(cell, widget: null);
        var shown = acts.Where(act => act is not (WidgetAct.OpenDrive or WidgetAct.Eject or WidgetAct.Disconnect or WidgetAct.DriveProperties
            or WidgetAct.RunAsAdministrator or WidgetAct.OpenFileLocation or WidgetAct.FileProperties
            or WidgetAct.ChooseDevice or WidgetAct.MuteApp
            // Only where two apps play, and where the app lets shuffle and repeat be set (MediaPresetTests).
            or WidgetAct.FollowSession or WidgetAct.Shuffle or WidgetAct.Repeat
            // Only for an app right-clicked or picked (ScreenTimeLinesTests).
            or WidgetAct.OpenApp
            // Only over an app of the top processes (ProcessesLineTests).
            or WidgetAct.EndTask
            // Only on a folder's contents with a folder (FolderContentsLineTests).
            or WidgetAct.OpenInExplorer
            // §641: only for the Start widget itself, what is under its pointer (StartGroupTests).
            or WidgetAct.Start
            // Only over a person (ContactsPresetTests).
            or WidgetAct.ContactEmail or WidgetAct.ContactCall or WidgetAct.ContactCopy
            // Only over a meeting with a Join (JoinEverywhereTests).
            or WidgetAct.JoinMeeting
            // Only while the app is not installed, and here it is (ShortcutWidgetTests).
            or WidgetAct.GetSnippingTool
            // Only where the widget can do it now: with nothing live a control is left out (§308).
            or WidgetAct.PlayPause or WidgetAct.Previous or WidgetAct.Next or WidgetAct.SeekBack or WidgetAct.SeekForward or WidgetAct.OpenPlayer
            or WidgetAct.StartPause or WidgetAct.Stop or WidgetAct.Longer or WidgetAct.Shorter or WidgetAct.Lap or WidgetAct.Reset
            or WidgetAct.ClearAll or WidgetAct.ClearCompleted or WidgetAct.DoNotDisturb or WidgetAct.EnergySaver or WidgetAct.Mute or WidgetAct.MuteMicrophone
            // Only where the widget names a site or a place.
            or WidgetAct.OpenWebsite or WidgetAct.OpenForecast or WidgetAct.OpenMap))
            // keep awake: a row for each length (KeepAwakeTests).
            .SelectMany(act => act == WidgetAct.AwakeFor ? Enumerable.Repeat(act, KeepAwakeLengths.All.Length) : [act]).ToArray();
        Assert.Equal(shown, rows.Select(row => row.Act));
        // The widget's page in Windows is the link its record names, under the settings app's words.
        foreach (var (row, item) in rows.Zip(items.Where(item => item.Id != 0)).Where(pair => pair.First.Act == WidgetAct.WindowsSettings))
        {
            var link = TheDock.WindowsLinkOf(cell.Info) ?? throw new Xunit.Sdk.XunitException($"{kind} has a page row and no page");
            Assert.Equal(link.Uri, row.On);
            Assert.Equal(link.Title, item.Text);
        }
        Assert.DoesNotContain("Settings", Texts(items));
        Assert.DoesNotContain("Copy", Texts(items));
        Assert.DoesNotContain("Remove", Texts(items));
        Assert.Equal(shown.Length, items.Count(item => !item.Separator && !item.Header && item.Id != 0));
        // Numbered from the widget acts' first command, each row the command it is picked by.
        Assert.Equal(rows.Select(row => row.Id), items.Where(item => item.Id != 0).Take(shown.Length).Select(item => item.Id));
        Assert.All(rows, row => Assert.Same(row, TheDock.ActRowOf(rows, row.Id)));
        // At rest the menu is these rows alone, no button of edit mode's row (§253); the floor's
        // where there are none (MenuFloorTests).
        var rest = TheDock.RestMenuRows(cell, inFolder: true).Items;
        Assert.DoesNotContain(rest, row => row.Primary);
        Assert.Equal(items, rest.Take(items.Count));
    }

    /// <summary>
    /// A widget's page in Windows is the one its record names — the link the settings app shows under
    /// it — and the sound panel's pieces, which name none, take the sound widget's. A widget Windows
    /// keeps no settings for has no such row, whatever its kind asks for.
    /// </summary>
    [Fact]
    public void AWidgetsPageInWindowsIsTheOneItsRecordNames()
    {
        Assert.Equal("ms-settings:nightlight", TheDock.WindowsLinkOf(WidgetInfos.NightLight)?.Uri);
        Assert.Equal("ms-settings:batterysaver", TheDock.WindowsLinkOf(WidgetInfos.EnergySaver)?.Uri);
        Assert.Equal("ms-settings:powersleep", TheDock.WindowsLinkOf(WidgetInfos.Battery)?.Uri);
        Assert.Equal(WidgetInfos.Volume.Settings, TheDock.WindowsLinkOf(WidgetInfos.SoundOutput));
        Assert.Equal("ms-settings:sound", TheDock.WindowsLinkOf(WidgetInfos.SoundMixer)?.Uri);
        Assert.Null(TheDock.WindowsLinkOf(WidgetInfos.Clock));

        var (items, rows) = TheDock.ActRows([WidgetAct.WindowsSettings], widget: null, TheDock.WindowsLinkOf(WidgetInfos.LiveCaptions));
        Assert.Equal([WidgetInfos.LiveCaptions.Settings!.Value.Title], Texts(items));
        Assert.Equal(WidgetInfos.LiveCaptions.Settings!.Value.Uri, Assert.Single(rows).On);
        Assert.Empty(TheDock.ActRows([WidgetAct.WindowsSettings], widget: null, page: null).Items);
    }

    /// <summary>The widget acts' commands are their own: nothing else of the dock's menus is numbered there.</summary>
    [Fact]
    public void AnActIsFoundByItsCommandAndNothingElseIs()
    {
        var (_, rows) = TheDock.ActRows([WidgetAct.ClockApp, WidgetAct.OpenVolumeMixer], widget: null);

        Assert.Equal(WidgetAct.OpenVolumeMixer, TheDock.ActRowOf(rows, TheDock.CMD_WIDGET_ACT + 1)?.Act);
        Assert.Null(TheDock.ActRowOf(rows, TheDock.CMD_WIDGET_ACT + 2));
        Assert.Null(TheDock.ActRowOf(rows, TheDock.CMD_WIDGET_ACT - 1));
        Assert.Null(TheDock.ActRowOf([], TheDock.CMD_WIDGET_ACT));
    }

    /// <summary>
    /// Without a widget to ask, a control is left out rather than greyed (§308): nothing plays, no
    /// countdown runs, the list is empty, there is no energy saver to switch, no site and no place to
    /// open, nothing to read again — the widget's own control would do nothing either. What is left
    /// is what works: here, the page.
    /// </summary>
    [Fact]
    public void AControlWithNothingToControlIsLeftOut()
    {
        var (items, rows) = TheDock.ActRows(
            [.. Playing, .. Countdown, WidgetAct.ClearAll, WidgetAct.DoNotDisturb, WidgetAct.EnergySaver, WidgetAct.Mute, .. Weather, .. Map, .. Feed, WidgetAct.NotificationSettings],
            widget: null);

        Assert.Equal([WindowsPages.Notifications.Title], Texts(items));
        Assert.Equal([WidgetAct.NotificationSettings], rows.Select(row => row.Act));
        Assert.Equal(TheDock.CMD_WIDGET_ACT, items.Single().Id);
    }

    /// <summary>
    /// Every row that opens something hands Windows exactly the page, the program or the file its
    /// row named, through the one seam a test stands in for — and every act on the widget itself
    /// asks Windows for nothing.
    /// </summary>
    [Fact]
    public void ARowThatOpensSomethingHandsWindowsWhatItNamed()
    {
        var opened = new List<(string Target, string? Verb)>();
        void Open(string target, string? verb) => opened.Add((target, verb));
        (WidgetAct Act, string? On, string Target, string? Verb)[] cases =
        [
            (WidgetAct.ResourceMonitor, null, "resmon.exe", null),
            (WidgetAct.CalendarApp, null, "outlookcal:", null),
            (WidgetAct.WindowsSettings, "ms-settings:nightlight", "ms-settings:nightlight", null),
            (WidgetAct.DateTimeSettings, null, "ms-settings:dateandtime", null),
            (WidgetAct.NotificationSettings, null, "ms-settings:notifications", null),
            (WidgetAct.OpenVolumeMixer, null, "ms-settings:apps-volume", null),
            (WidgetAct.SoundSettings, null, "ms-settings:sound", null),
            (WidgetAct.TroubleshootSound, null, "ms-settings:troubleshoot", null),
            (WidgetAct.DiagnoseNetwork, null, "ms-settings:troubleshoot", null),
            (WidgetAct.NetworkSettings, null, "ms-settings:network", null),
            (WidgetAct.PowerSettings, null, "ms-settings:powersleep", null),
            (WidgetAct.OpenDrive, @"E:\", @"E:\", null),
            (WidgetAct.Eject, @"E:\", @"E:\", MenuVerbs.Eject),
            (WidgetAct.Disconnect, @"Z:\", @"Z:\", MenuVerbs.Disconnect),
            (WidgetAct.DriveProperties, @"C:\", @"C:\", MenuVerbs.Properties),
            (WidgetAct.RunAsAdministrator, @"C:\Tools\tool.exe", @"C:\Tools\tool.exe", MenuVerbs.RunAs),
            (WidgetAct.OpenFileLocation, @"C:\Tools\tool.exe", @"C:\Tools\tool.exe", MenuVerbs.Select),
            (WidgetAct.FileProperties, @"C:\Tools\tool.exe", @"C:\Tools\tool.exe", MenuVerbs.Properties),
            // pins: a copy is the clipboard's, through the same seam (Dock.OpenFromMenu).
            (WidgetAct.CopyAsPath, "\"C:\\Tools\\tool.exe\"", "\"C:\\Tools\\tool.exe\"", MenuVerbs.Copy),
            (WidgetAct.CopyLink, "https://example.com/", "https://example.com/", MenuVerbs.Copy),
        ];
        foreach (var (act, on, target, verb) in cases)
        {
            opened.Clear();
            TheDock.RunAct(new WidgetActRow(TheDock.CMD_WIDGET_ACT, act, on), widget: null, Open);
            Assert.Equal([(target, verb)], opened);
        }
        foreach (var act in new[]
        {
            WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.OpenPlayer,
            WidgetAct.Mute, WidgetAct.MuteMicrophone, WidgetAct.StartPause, WidgetAct.Stop, WidgetAct.Longer, WidgetAct.Shorter, WidgetAct.Lap, WidgetAct.Reset, WidgetAct.ClearAll,
            WidgetAct.EnergySaver, WidgetAct.DoNotDisturb, WidgetAct.SeekBack, WidgetAct.SeekForward,
            WidgetAct.ChooseDevice, WidgetAct.MuteApp, WidgetAct.FollowSession, WidgetAct.Shuffle, WidgetAct.Repeat,
            // contacts: a copy stays in the folder.
            WidgetAct.ContactCopy,
        })
        {
            opened.Clear();
            TheDock.RunAct(new WidgetActRow(TheDock.CMD_WIDGET_ACT, act), widget: null, Open);
            Assert.Empty(opened);
        }
        // screentime: an app's "Open" is the dock's own (its windows forward, else started), never
        // the shell opening the path, which would start a second copy of an app that is open.
        opened.Clear();
        TheDock.RunAct(new WidgetActRow(TheDock.CMD_WIDGET_ACT, WidgetAct.OpenApp, @"C:\Program Files\Example\app.exe"), widget: null, Open);
        Assert.Empty(opened);
    }

    /// <summary>
    /// What hands the screen to another program and what stays in Karpo: opening anything leaves,
    /// acting on the widget — even ejecting, whose word is Windows' own notice, and energy saver,
    /// switched as its tile switches it — stays.
    /// §664 (the person, 2026-11-10: "when clicking refresh in the context menu it closed the panel, this should never
    /// happen"): every act of a widget's menu is said here to leave Karpo or to act in place, once — a new one fails until
    /// it is —, and the dock's one rule agrees: what leaves closes the folder at rest, what acts in place leaves it open,
    /// the focus coming back to it or not.
    /// </summary>
    [Fact]
    public void EveryActIsClassifiedAsLeavingOrInPlace()
    {
        WidgetAct[] leaving =
        [
            // An app, a file, a place, a page opened.
            WidgetAct.OpenPlayer, WidgetAct.OpenDrive, WidgetAct.CalendarApp, WidgetAct.ClockApp, WidgetAct.OpenApp, WidgetAct.Start,
            WidgetAct.RunAsAdministrator, WidgetAct.OpenFileLocation, WidgetAct.FileExplorer, WidgetAct.OpenInExplorer,
            WidgetAct.OpenWebsite, WidgetAct.OpenForecast, WidgetAct.OpenMap, WidgetAct.ContactEmail, WidgetAct.ContactCall,
            WidgetAct.JoinMeeting, WidgetAct.ResourceMonitor, WidgetAct.TaskManager, WidgetAct.OpenVolumeMixer,
            WidgetAct.OpenSnippingTool, WidgetAct.GetSnippingTool, WidgetAct.SnipIn3, WidgetAct.SnipIn10, WidgetAct.RecordScreen,
            // Windows' own windows and panels.
            WidgetAct.DriveProperties, WidgetAct.FileProperties, WidgetAct.WindowsSettings, WidgetAct.DateTimeSettings,
            WidgetAct.NotificationSettings, WidgetAct.SoundSettings, WidgetAct.TroubleshootSound, WidgetAct.DiagnoseNetwork,
            WidgetAct.NetworkSettings, WidgetAct.PowerSettings, WidgetAct.HotspotSettings, WidgetAct.AutoPlaySettings,
            WidgetAct.WindowsUpdate, WidgetAct.StorageSettings, WidgetAct.TaskView, WidgetAct.NewDesktop,
            WidgetAct.ClipboardHistory, WidgetAct.CastConnect, WidgetAct.EndTask,
            // Karpo's own settings, which take the folder over.
            WidgetAct.EditFolder, WidgetAct.ManageAccounts, WidgetAct.LayoutSettings,
        ];
        WidgetAct[] inPlace =
        [
            // Read again (§451).
            WidgetAct.Refresh,
            // Played, sought, followed, shuffled.
            WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.SeekBack, WidgetAct.SeekForward,
            WidgetAct.FollowSession, WidgetAct.Shuffle, WidgetAct.Repeat,
            // Switched or chosen where it stands.
            WidgetAct.Mute, WidgetAct.MuteMicrophone, WidgetAct.ChooseDevice, WidgetAct.MuteApp, WidgetAct.EnergySaver,
            WidgetAct.DoNotDisturb, WidgetAct.WifiRadio, WidgetAct.BluetoothRadio, WidgetAct.PowerMode, WidgetAct.AwakeFor,
            WidgetAct.StartPause, WidgetAct.Stop, WidgetAct.Longer, WidgetAct.Shorter, WidgetAct.Lap, WidgetAct.Reset,
            WidgetAct.ClearAll, WidgetAct.ClearCompleted, WidgetAct.Eject, WidgetAct.Disconnect, WidgetAct.StartEdit,
            // Copied.
            WidgetAct.ContactCopy, WidgetAct.CopyAsPath, WidgetAct.CopyLink,
        ];
        Assert.Empty(leaving.Intersect(inPlace));
        Assert.Equal(leaving.Length, leaving.Distinct().Count());
        Assert.Equal(inPlace.Length, inPlace.Distinct().Count());
        var unsaid = Enum.GetValues<WidgetAct>().Except(leaving).Except(inPlace).ToList();
        Assert.True(unsaid.Count == 0, $"say whether these leave Karpo or act in place: {string.Join(", ", unsaid)}");
        foreach (var act in Enum.GetValues<WidgetAct>())
        {
            var leaves = leaving.Contains(act);
            Assert.True(leaves == TheDock.HandsAway(act), $"{act}: the dock says it {(leaves ? "acts in place" : "leaves Karpo")}");
            foreach (var focusElsewhere in new[] { false, true })
            {
                var after = TheDock.FolderAfterMenu(arranging: false, TheDock.HandsAway(act), focusElsewhere);
                Assert.Equal(leaves || focusElsewhere ? AfterMenu.Closes : AfterMenu.Stays, after);
            }
        }
    }

    /// <summary>
    /// Of the rows every widget has, only what changes the widget where it stands stays in Karpo:
    /// copying and removing it — and, for a pin, closing its app's windows. Its app and its jump list
    /// take the screen, so one picked at rest closes the folder it stood over.
    /// </summary>
    [Fact]
    public void OfTheCommonRowsOnlyTheEditsStayInKarpo()
    {
        var common = TheDock.WidgetRowsFor(ListKind.Battery.New()).Where(row => !row.Separator).ToList();
        Assert.Equal(["Cut", "Copy", "Remove"], common.Select(row => row.Text));
        Assert.Equal([false, false, false], common.Select(row => TheDock.PickHandsAway([], row.Id)));

        var pin = TheDock.AppRows([], "Notepad", new FlyoutIcon.Glyph("x"), pinned: true, open: 1, TheDock.PinButtons(pin: true, arranged: true), Edge.Bottom)
            .Where(row => !row.Separator).ToList();
        Assert.Equal(["Cut", "Copy", "Remove", "Notepad", "Close window"], pin.Select(row => row.Text));
        Assert.Equal([false, false, false, true, false], pin.Select(row => TheDock.PickHandsAway([], row.Id)));
    }

    /// <summary>
    /// What becomes of the folder under the menu. While the bar is arranged it stays through
    /// everything. At rest it stays for a pick that acted in Karpo, for Escape and for a press
    /// anywhere — the press that closes the menu closes the menu alone (docs\widgets.md §185) — and
    /// closes for a pick that opened something and for the focus going elsewhere.
    /// </summary>
    [Fact]
    public void TheFolderStaysUnlessThePickOrTheFocusLeftIt()
    {
        // At rest.
        Assert.Equal(AfterMenu.Stays, TheDock.FolderAfterMenu(false, handedAway: false, focusElsewhere: false));
        Assert.Equal(AfterMenu.Closes, TheDock.FolderAfterMenu(false, handedAway: true, focusElsewhere: false));
        Assert.Equal(AfterMenu.Closes, TheDock.FolderAfterMenu(false, handedAway: false, focusElsewhere: true));

        // Arranged: through all of it.
        foreach (var handedAway in new[] { false, true })
        {
            foreach (var elsewhere in new[] { false, true })
            {
                Assert.Equal(AfterMenu.Stays, TheDock.FolderAfterMenu(true, handedAway, elsewhere));
            }
        }
    }

    /// <summary>
    /// Windows' own rows for a pin's file are there for a file this PC has, and "Run as
    /// administrator" only for a program or a shortcut to one — Windows offers it for nothing else.
    /// An app by its id, a link and a pin whose target is gone have none.
    /// </summary>
    [Fact]
    public void APinsFileRowsAreForAFileAndElevatingForAProgram()
    {
        static Item Pin(string target, ItemKind kind = ItemKind.Shell, string? broken = null, params string[] keys) =>
            new() { Target = target, Name = "pin", Kind = kind, Keys = [.. keys], Broken = broken };

        var program = Pin(@"C:\Tools\tool.exe", keys: @"exe:c:\tools\tool.exe");
        Assert.Equal(program.Target, TheDock.FileOf(program));
        Assert.True(TheDock.Elevates(program));

        var shortcut = Pin(@"C:\Users\me\Desktop\Tool.lnk", keys: @"exe:c:\tools\tool.exe");
        Assert.Equal(shortcut.Target, TheDock.FileOf(shortcut));
        Assert.True(TheDock.Elevates(shortcut));

        var document = Pin(@"C:\Users\me\Desktop\Plan.lnk");
        Assert.NotNull(TheDock.FileOf(document));
        Assert.False(TheDock.Elevates(document));

        Assert.Null(TheDock.FileOf(Pin(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")));
        Assert.Null(TheDock.FileOf(Pin("https://example.com/", ItemKind.Link)));
        Assert.Null(TheDock.FileOf(Pin(@"D:\Gone\tool.exe", broken: "D: isn't connected")));
        Assert.Null(TheDock.FileOf(Pin("tool.exe")));
    }

    /// <summary>
    /// A pin in a folder is the bar's pin menu with its file's rows under the app's own, and its
    /// unpinning says what it does there: "Remove from folder". At rest
    /// either is Windows' own menu for an app, no row of buttons on top and "Unpin from taskbar" a
    /// row (the person, 2026-10-17: "for pin widgets in not edit mode you should remove the top row in
    /// the context menu"), and no shortcut of the row's left to act. While the bar is arranged it has
    /// the widget's row on top, above the jump list: "Copy", "Settings" and "Remove", its unpinning —
    /// so no "Unpin" row. An open app that is no pin has no buttons either way, and "Pin to taskbar".
    /// </summary>
    [Fact]
    public void APinsMenuHasTheRowOnTopOnlyWhileArranged()
    {
        FlyoutItem[] file = [new(TheDock.CMD_WIDGET_ACT, "Run as administrator"), new(TheDock.CMD_WIDGET_ACT + 1, "Open file location")];
        FlyoutItem[] jumps = [new(0, "Recent", Header: true), new(9000, "Plan.txt")];
        var icon = new FlyoutIcon.Glyph("x");

        var restBar = Flyout.WithPrimaryRow(TheDock.AppRows(jumps, "Tool", icon, pinned: true, open: 1, TheDock.PinButtons(pin: true, arranged: false), Edge.Bottom));
        var restFolder = Flyout.WithPrimaryRow(TheDock.AppRows([], "Tool", icon, pinned: true, open: 0, TheDock.PinButtons(pin: true, arranged: false), Edge.Bottom, file: file, inFolder: true));
        Assert.Equal(["Recent", "Plan.txt", "—", "Tool", "Unpin from taskbar", "—", "Close window"], Texts(restBar));
        Assert.Equal(["Tool", "Run as administrator", "Open file location", "Remove from folder"], Texts(restFolder));
        Assert.DoesNotContain(restBar, row => row.Primary || row.Keys is not null);
        Assert.DoesNotContain(restFolder, row => row.Primary || row.Keys is not null);

        var bar = Flyout.WithPrimaryRow(TheDock.AppRows(jumps, "Tool", icon, pinned: true, open: 1, TheDock.PinButtons(pin: true, arranged: true), Edge.Bottom));
        var folder = Flyout.WithPrimaryRow(TheDock.AppRows([], "Tool", icon, pinned: true, open: 0, TheDock.PinButtons(pin: true, arranged: true), Edge.Bottom, file: file, inFolder: true));

        Assert.Equal(["Cut", "Copy", "Remove", "—", "Recent", "Plan.txt", "—", "Tool", "—", "Close window"], Texts(bar));
        Assert.True(bar.Take(3).All(row => row.Primary));
        Assert.Equal(["Cut", "Copy", "Remove", "—", "Tool", "Run as administrator", "Open file location"], Texts(folder));
        Assert.DoesNotContain(bar, row => row.Text == "Unpin from taskbar");
        Assert.Null(TheDock.PinButtons(pin: false, arranged: true));

        var app = TheDock.AppRows([], "Tool", icon, pinned: false, open: 1, buttons: null, Edge.Bottom);
        Assert.Equal(["Tool", "Pin to taskbar", "—", "Close window"], Texts(app));
        Assert.DoesNotContain(app, row => row.Primary);
    }
}
