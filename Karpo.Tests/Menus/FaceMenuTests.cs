// menus: what a right click on the bar offers below the row of buttons (Karpo.Dock\Dock.FaceMenus.cs;
// the person, 2026-10-11: "most of the right click context menus of the taskbar widgets are empty,
// they should not be, they should show options related to the widget face, if there are multiple
// faces it should show all of them in order"). A folder's rows are its faces', in their order, a rule
// between two faces' and no heading (2026-10-22); every other entry's are its kind's and its page in
// Windows. No face and no entry has the buttons alone. The sound and battery faces keep the rows
// Windows 11's own icons have, in its order and words, each handing Windows the page it names.
// Decisions, not drawings: nothing here opens anything. The drawn menus are MenuModePictures'.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class FaceMenuTests
{
    private static string[] Texts(IEnumerable<FlyoutItem> rows) =>
        [.. rows.Select(row => row.Separator ? "—" : row.Header ? "# " + row.Text : row.Text)];

    private static BarItem Preset(string id) =>
        Presets.Folder(Presets.Find(id)!, new PresetMachine(HasBattery: false, FixedDrives: []));

    private static BarItem Wearing(params FolderFace[] faces) =>
        new() { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Faces = [.. faces] } };

    /// <summary>The dock's own rows a folder's faces take: the desktop switcher's, and two layouts to choose from.</summary>
    private static readonly FaceMenuParts Parts = new(
        TheDock.DesktopRows(Switchers.ActionsFor(0, current: 0, count: 2, shell: true)),
        TheDock.LayoutRows([new TaskbarLayout { Id = "a", Name = "Work" }, new TaskbarLayout { Id = "b", Name = "Play" }], "a"));

    /// <summary>What the sound face offers: a mute, then Windows' volume icon's rows, in its words, and the page each opens.</summary>
    private static readonly (string Text, string Page)[] SoundRows =
    [
        ("Open volume mixer", "ms-settings:apps-volume"),
        ("Sound settings", "ms-settings:sound"),
        // Windows' own troubleshooter is the Get Help app's, which has no way in for a
        // program: Settings' troubleshooters, where the audio one is.
        ("Troubleshoot sound problems", "ms-settings:troubleshoot"),
    ];

    /// <summary>
    /// The sound face's rows: its mute — left out with no output to mute, as here, where no folder is
    /// made (§308) — then Windows' own volume icon's, each numbered as the widget acts are, found by its
    /// command and handing Windows its page when picked. The battery face's: energy saver where the PC
    /// has it, and Power &amp; battery (its power modes come from the folder made, which reads Windows').
    /// </summary>
    [Fact]
    public void TheSoundAndBatteryFacesHaveWindowsOwnRows()
    {
        var (items, acts) = TheDock.BarMenuRows(Preset(Presets.SoundId));
        Assert.Equal([.. SoundRows.Select(row => row.Text)], Texts(items));
        foreach (var ((_, page), act) in SoundRows.Zip(acts))
        {
            Assert.Same(act, TheDock.ActRowOf(acts, act.Id));
            Assert.True(TheDock.HandsAway(act.Act), $"{act.Act} opens a page");
            var opened = new List<(string Target, string? Verb)>();
            TheDock.RunAct(act, widget: null, (target, verb) => opened.Add((target, verb)));
            Assert.Equal([(page, (string?)null)], opened);
        }

        Assert.Equal(["Power & battery settings"], Texts(TheDock.BarMenuRows(Preset(Presets.BatteryId)).Items));
    }

    /// <summary>
    /// Every face's menu has rows below the buttons, even with no folder made to read and none of its
    /// widgets in it: its own (the table is ActsFor(FaceInfo)) — a page, an app, or what the face is
    /// about — else the floor's "Edit folder", for a face with none (the weather's, read again from its
    /// line's own menu, §451).
    /// </summary>
    [Fact]
    public void NoFaceIsTheButtonsAlone()
    {
        foreach (var face in FolderFaces.All)
        {
            var holder = Wearing(FolderFace.Of(face.Id));
            var (items, _) = TheDock.Floored(TheDock.BarMenuRows(holder, folder: null, Parts), holder);
            Assert.True(items.Exists(item => !item.Header && !item.Separator), $"the {face.Id} face has nothing below its buttons");
            // One face: its rows as they are, under no heading.
            Assert.DoesNotContain(items, item => item.Header && item.Text == face.Title);
        }
    }

    /// <summary>
    /// A few faces' rows, as the person asked for them: the Wi-Fi's radio, troubleshooter and page; the
    /// clock's date and time and the Clock app; the weather's forecast; what is playing; the
    /// stock's page; the desktops' new, rename, delete and Task view; the layouts to
    /// choose from, then their page.
    /// </summary>
    [Fact]
    public void TheFacesRowsAreWhatTheFaceIsAbout()
    {
        Assert.Equal([WidgetAct.WifiRadio, WidgetAct.DiagnoseNetwork, WidgetAct.NetworkSettings], TheDock.ActsFor(FolderFaces.Wifi));
        Assert.Equal([WidgetAct.DateTimeSettings, WidgetAct.ClockApp], TheDock.ActsFor(FolderFaces.Clock));
        // Its place is a setting, edit mode's (§253); its forecast (§308), no refresh (§315).
        Assert.Equal([WidgetAct.OpenForecast], TheDock.ActsFor(FolderFaces.Weather));
        Assert.Equal([WidgetAct.PlayPause, WidgetAct.Previous, WidgetAct.Next, WidgetAct.OpenPlayer], TheDock.ActsFor(FolderFaces.NowPlaying));
        Assert.Equal([WidgetAct.OpenWebsite], TheDock.ActsFor(FolderFaces.Markets));
        Assert.Equal([WidgetAct.PowerMode, WidgetAct.EnergySaver, WidgetAct.PowerSettings], TheDock.ActsFor(FolderFaces.Battery));

        // The Wi-Fi's radio, left out until Windows has said whether it is on (§308).
        Assert.Equal(["Diagnose network problems", "Network & internet settings"], Texts(TheDock.BarMenuRows(Wearing(FolderFace.Of(FolderFaces.WifiId))).Items));
        Assert.Equal(
            ["New desktop", "Task view", "Rename this desktop", "Delete this desktop"],
            Texts(TheDock.BarMenuRows(Wearing(FolderFace.Of(FolderFaces.DesktopsId)), folder: null, Parts).Items));
        Assert.Equal(["Work", "Play", "—", "Layout settings"], Texts(TheDock.BarMenuRows(Wearing(FolderFace.Of(FolderFaces.LayoutsId)), folder: null, Parts).Items));
        // A content face is what is inside, and offers to arrange it.
        // None of its own (§308): the floor's, alone.
        Assert.Empty(TheDock.BarMenuRows(Wearing(new FolderFace())).Items);
        Assert.Equal(["Edit folder"], Texts(TheDock.RestMenuRows(Wearing(new FolderFace()), inFolder: false).Items));
    }

    /// <summary>
    /// Several faces: each one's rows in the faces' order, a rule between two and no heading (the person,
    /// 2026-10-22: "when i right click a folder it shows its context menu options, it should not show a
    /// title for each face"); the rows numbered one after another, each found by its command; a face with
    /// no rows (the weather's, read again on its line, §280) is left out. A row reading as one another
    /// face offers is the same command and stands once, with the last face that offers it.
    /// </summary>
    [Fact]
    public void SeveralFacesRowsFollowInTheirOrderUnderNoHeading()
    {
        var (items, acts) = TheDock.BarMenuRows(Wearing(ClockFace.New(), WeatherFace.New(), FolderFace.Of(FolderFaces.SoundId)));

        Assert.Equal(
        [
            "Date & time settings", "Open Clock", "—",
            .. SoundRows.Select(row => row.Text),
        ], Texts(items));
        Assert.Equal(Enumerable.Range(0, acts.Count).Select(at => TheDock.CMD_WIDGET_ACT + (uint)at), acts.Select(act => act.Id));
        Assert.Equal(
            [WidgetAct.DateTimeSettings, WidgetAct.ClockApp, WidgetAct.OpenVolumeMixer, WidgetAct.SoundSettings, WidgetAct.TroubleshootSound],
            items.Where(item => item.Id != 0).Select(item => TheDock.ActRowOf(acts, item.Id)!.Act));

        // Two clocks, and a clock and a calendar: the same command once, no heading anywhere.
        var tokyo = ClockFace.New();
        tokyo.Set(FolderFaces.Clock.Option(ClockFace.Label)!, "Tokyo");
        Assert.Equal(["Date & time settings", "Open Clock"], Texts(TheDock.BarMenuRows(Wearing(ClockFace.New(), tokyo)).Items));
        Assert.Equal(
            ["Open Clock", "—", "Open Calendar", "Date & time settings"],
            Texts(TheDock.BarMenuRows(Wearing(ClockFace.New(), FolderFace.Of(FolderFaces.CalendarId))).Items));
    }

    /// <summary>
    /// The bell's rows (tiles 43 and 48 of the menus sheet) keep "Notification settings" even where the
    /// date and time before it offers the same page, as Windows 11's clock does: the command stands once,
    /// with the last face that offers it. At rest, with no widget to ask, the bell has nothing to clear
    /// and no switch (§308); its do not disturb rows are FolderMenuRowsTests'.
    /// </summary>
    [Fact]
    public void TheBellsRowsKeepNotificationSettings()
    {
        string[] bell = [WindowsPages.Notifications.Title];
        Assert.Equal(bell, Texts(TheDock.BarMenuRows(Wearing(FolderFace.Of(FolderFaces.NotificationsId))).Items));
        Assert.Equal(
            ["Date & time settings", "—", .. bell],
            Texts(TheDock.BarMenuRows(Wearing(FolderFace.Of(FolderFaces.DateTimeId), FolderFace.Of(FolderFaces.NotificationsId))).Items));
        // The other way round the date and time keeps it, and the bell has no row left.
        Assert.Equal(
            ["Date & time settings", WindowsPages.Notifications.Title],
            Texts(TheDock.BarMenuRows(Wearing(FolderFace.Of(FolderFaces.NotificationsId), FolderFace.Of(FolderFaces.DateTimeId))).Items));
    }

    /// <summary>
    /// Every other entry of the bar has rows below its buttons: its kind's own, and the page Windows
    /// keeps for it — its taskbar settings where it keeps none of its own.
    /// </summary>
    [Fact]
    public void NoEntryOfTheBarIsTheButtonsAlone()
    {
        // A pin's is its jump list, the Start button's its quick links, the apps' room the bar's own menu, the status icons each their
        // own, a folder its faces'.
        BarKind[] elsewhere = [BarKind.Pin, BarKind.Start, BarKind.Apps, BarKind.Indicators, BarKind.FolderList];
        foreach (var kind in Enum.GetValues<BarKind>().Where(kind => !elsewhere.Contains(kind) && BarEntries.Find(kind) is not null))
        {
            var (items, acts) = TheDock.BarMenuRows(kind.New());
            Assert.True(items.Count > 0, $"{kind} has nothing below its buttons");
            Assert.Equal(acts.Count, items.Count(item => item.Id != 0));
        }
        Assert.Equal(["New desktop", "Multitasking settings"], Texts(TheDock.BarMenuRows(BarKind.TaskView.New()).Items));
        Assert.Equal(["Taskbar settings"], Texts(TheDock.BarMenuRows(BarKind.ShowDesktop.New()).Items));
        Assert.Equal(["Open volume mixer", "Sound settings", "Troubleshoot sound problems"], Texts(TheDock.BarMenuRows(BarKind.Volume.New()).Items));
    }

    /// <summary>
    /// The network icon's own rows went with its entry to the network's line in the Wi-Fi preset
    /// (2026-10-10): its right click there offers what Windows' network icon does.
    /// </summary>
    [Fact]
    public void TheNetworkLineHasTheNetworkIconsRows()
    {
        Assert.Equal([WidgetAct.DiagnoseNetwork, WidgetAct.NetworkSettings], TheDock.ActsFor(ListKind.Wifi));
        Assert.Equal([WidgetAct.WindowsSettings], TheDock.ActsFor(ListKind.Bluetooth));
    }

    /// <summary>A widget the file has lost has no rows at all.</summary>
    [Fact]
    public void AWidgetTheFileHasLostHasNoMenu()
    {
        var (items, acts) = TheDock.BarMenuRows(null);
        Assert.Empty(items);
        Assert.Empty(acts);
    }
}
