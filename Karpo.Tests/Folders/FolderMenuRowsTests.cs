// folders: the rows of a widget's own menu that depend on the widget as it is (Karpo.Dock\
// Dock.WidgetMenus.cs, ActRows) — what is playing, how far a countdown has got, which drive is
// there and what kind it is, what a pin points at — read from real widgets standing in for the
// world: a made-up track, timers of the test's own, made-up drives, a program every Windows has.
// And the acts done on them go through the widget's own controls, never around them.
//
// Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using Karpo.Tests.Storage;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderMenuRowsTests
{
    private static readonly DateTime Frozen = new(2026, 9, 27, 10, 30, 0);

    private static CellIsland Line(Gfx gfx, ListKind kind, Action<CellItem>? set = null)
    {
        var widget = kind.New();
        widget.Id = kind.ToString().ToLowerInvariant();
        set?.Invoke(widget);
        if (Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), widget, kind, gfx, new Config()) is not CellIsland island)
        {
            throw new Xunit.Sdk.XunitException($"{kind}: no line was made.");
        }
        _ = island.Refresh(gfx);
        return island;
    }

    private static string[] Texts(IEnumerable<FlyoutItem> rows) => [.. rows.Select(row => row.Text)];

    /// <summary>
    /// What is playing: the middle row says what the line's middle button would do, and each row is
    /// greyed where the player says its button would do nothing. With nothing playing there is
    /// nothing to control, and no player to bring forward.
    /// </summary>
    [UiFact]
    public void WhatIsPlayingOffersItsOwnButtons()
    {
        Offscreen.OnStaThread(gfx =>
        {
            try
            {
                MediaIsland.Stand = new MediaStand(IsPlaying: true, CanPrevious: false);
                using var playing = Line(gfx, ListKind.Media);
                var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), playing);
                // No previous track, no timeline to step through; and a made-up track has no session
                // to name its app by: nothing to bring forward. Each left out, not greyed (§308).
                Assert.Equal(["Pause", "Next track"], Texts(items));
                Assert.All(items, item => Assert.True(item.Enabled, item.Text));
                // Pressed from the menu, the line's own button: a stand-in has no app to tell, and says nothing.
                TheDock.RunAct(rows[0], playing, (_, _) => Assert.Fail("a media button opened something"));

                MediaIsland.Stand = new MediaStand(IsPlaying: false);
                using var paused = Line(gfx, ListKind.Media);
                Assert.Equal("Play", TheDock.ActRows(TheDock.ActsFor(ListKind.Media), paused).Items[0].Text);

                MediaIsland.Stand = new MediaStand(Nothing: true);
                using var idle = Line(gfx, ListKind.Media);
                var (none, _) = TheDock.ActRows(TheDock.ActsFor(ListKind.Media), idle);
                Assert.Empty(none);
            }
            finally
            {
                MediaIsland.Stand = null;
            }
        });
    }

    /// <summary>
    /// A countdown's menu presses the line's own buttons and says what they say: Start, then Pause,
    /// Resume, and Stop only while there is something to stop; and its steppers, a minute longer or
    /// shorter, only while it is not running — what a line one row tall has no room for. Each works its
    /// own countdown, in the one store a dock keeps — a focus session's menu beside a running timer
    /// offers to start the focus session, as its line does.
    /// </summary>
    [UiFact]
    public void ACountdownsMenuPressesItsOwnButtons()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var countdowns = new Countdowns();
            var now = Frozen;
            CountdownTile Tile(ListKind kind)
            {
                var item = kind.New();
                item.Id = kind.ToString().ToLowerInvariant();
                return new CountdownTile(Config.IslandKey(null, item.Id), item, gfx, new Config(), countdowns, () => now);
            }
            using var timer = Tile(ListKind.Timer);
            using var focus = Tile(ListKind.Focus);
            void Says(CountdownTile tile, string action, bool stops)
            {
                // Stop only while there is something to stop, the steppers while it is not running, as
                // its line shows them; each left out where it would do nothing (§308).
                var (items, _) = TheDock.ActRows(TheDock.ActsFor(ListKind.Timer), tile);
                var steps = action != "Pause";
                Assert.Equal([action, .. stops ? ["Stop"] : Array.Empty<string>(), .. steps ? ["A minute longer", "A minute shorter"] : Array.Empty<string>()], Texts(items));
                Assert.All(items, item => Assert.True(item.Enabled, item.Text));
            }
            WidgetActRow Act(CountdownTile tile, WidgetAct act) =>
                TheDock.ActRows(TheDock.ActsFor(ListKind.Timer), tile).Rows.Single(row => row.Act == act);
            void Never(string target, string? verb) => Assert.Fail("a countdown's button opened something");

            Says(timer, "Start", stops: false);

            // Its steppers set its own length, and no other's.
            TheDock.RunAct(Act(timer, WidgetAct.Longer), timer, Never);
            TheDock.RunAct(Act(timer, WidgetAct.Longer), timer, Never);
            TheDock.RunAct(Act(timer, WidgetAct.Shorter), timer, Never);
            Assert.Equal(6, timer.Settings.Countdown?.Minutes);
            Assert.Null(focus.Settings.Countdown);

            TheDock.RunAct(Act(timer, WidgetAct.StartPause), timer, Never);
            Says(timer, "Pause", stops: true);
            // The other kind's line offers to start its own, as its line's button does.
            Says(focus, "Start", stops: false);

            now = now.AddMinutes(1);
            TheDock.RunAct(Act(timer, WidgetAct.StartPause), timer, Never);
            Says(timer, "Resume", stops: true);

            TheDock.RunAct(Act(timer, WidgetAct.Stop), timer, Never);
            Says(timer, "Start", stops: false);
        });
    }

    /// <summary>
    /// The battery's menu switches energy saver as the energy saver widget does — through that
    /// widget's own switch, made the first time the menu asks — and ticks it while it is on. A PC
    /// without energy saver has the row greyed. A made-up switch stands in, so nothing here reaches
    /// this PC's energy saver.
    /// </summary>
    [UiFact]
    public void TheBatterysMenuSwitchesEnergySaver()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var saver = new FakeSwitch { On = true };
            QuickSwitches.Source = which => which == QuickSwitch.EnergySaver ? saver : null;
            try
            {
                using var battery = Line(gfx, ListKind.Battery);
                var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.Battery), battery, TheDock.WindowsLinkOf(WidgetInfos.Battery));
                // A switch says what a pick does (§308).
                Assert.Equal(["Turn energy saver off", "Power & battery settings"], Texts(items));
                Assert.Null(items[0].Checked);
                Assert.True(items[0].Enabled);
                Assert.True(saver.Started, "the menu asked a switch that was never started");

                TheDock.RunAct(rows[0], battery, (_, _) => Assert.Fail("energy saver opened something"));
                Assert.Equal(1, saver.Acts);

                // A PC with no energy saver has no row for it (§308).
                saver.Available = false;
                Assert.Equal(["Power & battery settings"], Texts(TheDock.ActRows(TheDock.ActsFor(ListKind.Battery), battery, TheDock.WindowsLinkOf(WidgetInfos.Battery)).Items));
            }
            finally
            {
                QuickSwitches.Source = null;
            }
            Assert.True(saver.Disposed, "the battery let its switch go undisposed");
        });
    }

    /// <summary>
    /// A line's menu at rest, read from the line as it is: its own rows alone, no button of edit
    /// mode's row (§253). A line with nothing of its own has the floor's "Edit folder". A made-up
    /// switch stands in for energy saver.
    /// </summary>
    [UiFact]
    public void AtRestALinesMenuIsItsRowsAlone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var saver = new FakeSwitch { On = false };
            QuickSwitches.Source = which => which == QuickSwitch.EnergySaver ? saver : null;
            try
            {
                using var battery = Line(gfx, ListKind.Battery);
                var (rest, acts) = TheDock.RestMenuRows(ListKind.Battery.New(), inFolder: true, battery);
                Assert.Equal(["Turn energy saver on", "Power & battery settings"], Texts(rest));
                Assert.All(rest, row => Assert.NotNull(TheDock.ActRowOf(acts, row.Id)));
            }
            finally
            {
                QuickSwitches.Source = null;
            }
            Assert.Equal(["Edit folder"], Texts(TheDock.RestMenuRows(ListKind.Security.New(), inFolder: true).Items));
            Assert.Equal(["Edit folder"], Texts(TheDock.RestMenuRows(ListKind.Heading.New(), inFolder: true).Items));
        });
    }

    /// <summary>
    /// The notification list's menu switches do not disturb, the service its header switches: off, on
    /// now, for an hour or until tomorrow; on, off. A made-up Windows stands in, and the list reads a
    /// notification database that is not there, so nothing here reaches this PC's setting or its
    /// notifications.
    /// </summary>
    [UiFact]
    public void TheNotificationListsMenuSwitchesDoNotDisturb()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var windows = new Karpo.Tests.QuickSettings.FakeQuiet();
            var quiet = new DoNotDisturb(windows);
            var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "quiet-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new NotificationStore(
                    Path.Combine(folder, "missing", "wpndatabase.db"), Path.Combine(folder, "copy"), Path.Combine(folder, "state"), app => (app, null));
                var cell = ListKind.NotificationsList.New();
                cell.Id = "list";
                using var list = new NotificationPartTile(Config.IslandKey(null, "list"), cell, gfx, new Config(), store, quiet);
                var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.NotificationsList), list);
                // Nothing to clear: no "Clear all" (§308); off, its three ways on.
                Assert.Equal(["Turn do not disturb on", "Turn on for 1 hour", "Turn on until tomorrow", "Notification settings"], Texts(items));
                Assert.All(items, item => Assert.True(item.Enabled, item.Text));

                TheDock.RunAct(rows[0], list, (_, _) => Assert.Fail("do not disturb opened something"));
                Assert.Equal([true], windows.Asked);
                windows.Answer(new DndState(On: true));
                Assert.Equal(["Turn do not disturb off", "Notification settings"], Texts(TheDock.ActRows(TheDock.ActsFor(ListKind.NotificationsList), list).Items));
            }
            finally
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        });
    }

    /// <summary>A made-up switch: it says what it is told and counts the clicks.</summary>
    private sealed class FakeSwitch : IQuickSwitch
    {
        public string Name => "Made up";

        public bool Available { get; set; } = true;

        public bool? On { get; set; }

        public string? Note => null;

        public bool Started { get; private set; }

        public int Acts { get; private set; }

        public bool Disposed { get; private set; }

        public void Start(Action changed) => Started = true;

        public void Act() => Acts++;

        public void Dispose() => Disposed = true;
    }

    /// <summary>
    /// A pin has Windows' own rows for what it opens (PinRows), on the bar as in a folder: a program,
    /// elevated too, a file and a folder "Open file location", "Copy as path" (quoted, as Explorer's)
    /// and "Properties"; a link "Copy link"; and none for an app by its id or a target that is gone.
    /// Each acts on the target it showed.
    /// </summary>
    [UiFact]
    public void APinsTargetHasWindowsOwnRows()
    {
        Offscreen.OnStaThread(_ =>
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
            static Item Pin(string target) => PinItems.For(new PinOptions { Target = target, Name = "Pin" });
            static List<(string Target, string? Verb)> Run(List<WidgetActRow> rows)
            {
                var opened = new List<(string Target, string? Verb)>();
                foreach (var row in rows)
                {
                    TheDock.RunAct(row, null, (target, verb) => opened.Add((target, verb)));
                }
                return opened;
            }

            var (items, rows) = TheDock.PinRows(Pin(notepad));
            Assert.Equal(["Run as administrator", "Open file location", "Copy as path", "Properties"], Texts(items));
            Assert.Equal([(notepad, MenuVerbs.RunAs), (notepad, MenuVerbs.Select), ($"\"{notepad}\"", MenuVerbs.Copy), (notepad, MenuVerbs.Properties)], Run(rows));
            // A copy is done in Karpo: the screen is handed to nobody, and an open folder stays.
            Assert.False(TheDock.HandsAway(WidgetAct.CopyAsPath) || TheDock.HandsAway(WidgetAct.CopyLink));

            foreach (var target in new[] { windows, Path.Combine(windows, "win.ini") })
            {
                var (shown, acts) = TheDock.PinRows(Pin(target));
                Assert.Equal(["Open file location", "Copy as path", "Properties"], Texts(shown));
                Assert.Equal([(target, MenuVerbs.Select), ($"\"{target}\"", MenuVerbs.Copy), (target, MenuVerbs.Properties)], Run(acts));
            }

            var (link, linked) = TheDock.PinRows(Pin("https://example.com/"));
            Assert.Equal(["Copy link"], Texts(link));
            Assert.Equal([("https://example.com/", MenuVerbs.Copy)], Run(linked));

            foreach (var target in new[] { @"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", @"Q:\Nowhere\tool.exe" })
            {
                Assert.Empty(TheDock.PinRows(Pin(target)).Items);
            }
            // A pin's line in a folder has no rows of its kind's: they are its target's, the bar's own rule.
            Assert.Empty(TheDock.ActsFor(ListKind.Pin));
        });
    }
}

/// <summary>
/// A drive's rows, run alone: the drives are made up through the one seam every drive widget
/// reads (<see cref="DriveWatch.Source"/>), which is a static.
/// </summary>
[Collection(AttachedDrivesCollection.Collection)]
public sealed class FolderMenuDriveTests
{
    private const string StickVolume = @"\\?\Volume{55555555-5555-5555-5555-555555555555}\";
    private const string CardVolume = @"\\?\Volume{66666666-6666-6666-6666-666666666666}\";
    private const string SystemVolume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
    private const string GoneVolume = @"\\?\Volume{99999999-9999-9999-9999-999999999999}\";
    private const string Share = @"\\server\team";

    private static Drive Disk(int letter, string label, DriveKind kind, string volume) =>
        new() { Letter = letter, Kind = kind, Volume = volume, Label = label, Total = 64UL << 30, Free = 16UL << 30 };

    private static string[] Texts(IEnumerable<FlyoutItem> rows) => [.. rows.Select(row => row.Text)];

    /// <summary>
    /// A drive's menu is Explorer's for it: Open and Properties, Eject for a stick or a disc and
    /// Disconnect for a mapped share — each acting on the drive the row named. A drive that is not
    /// there has none of them. The removable devices' menu has each device plugged in under its
    /// name, with a drive's rows.
    /// </summary>
    [UiFact]
    public void ADrivesMenuIsExplorersForThatDrive()
    {
        Drive[] drives =
        [
            Disk(4, "Stick", DriveKind.Removable, StickVolume),
            Disk(5, "Card", DriveKind.Removable, CardVolume),
            Disk(2, "Windows", DriveKind.Fixed, SystemVolume),
            Disk(25, "Team", DriveKind.Remote, Share),
        ];
        var before = DriveWatch.Source;
        DriveWatch.Source = (_, remote) => drives.Where(drive => remote || drive.Kind != DriveKind.Remote).ToList();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                CellIsland Drive(ListKind kind, string volume, string letter) =>
                    MakeLine(gfx, kind, widget => widget.Drive = new DriveTargetOptions { Volume = volume, Letter = letter, Label = "" });
                void Offers(ListKind kind, CellIsland island, string[] texts, string?[] on)
                {
                    var (items, rows) = TheDock.ActRows(TheDock.ActsFor(kind), island);
                    Assert.Equal(texts, Texts(items));
                    Assert.Equal(on, rows.Select(row => row.On));
                }

                using var stick = Drive(ListKind.Drive, StickVolume, "E:");
                Offers(ListKind.Drive, stick, ["Open", "Eject", "Properties"], [@"E:\", @"E:\", @"E:\"]);

                using var system = Drive(ListKind.Drive, SystemVolume, "C:");
                Offers(ListKind.Drive, system, ["Open", "Properties"], [@"C:\", @"C:\"]);

                using var share = Drive(ListKind.NetworkDrive, Share, "Z:");
                Offers(ListKind.NetworkDrive, share, ["Open", "Disconnect", "Properties"], [@"Z:\", @"Z:\", @"Z:\"]);

                using var gone = Drive(ListKind.Drive, GoneVolume, "Q:");
                Offers(ListKind.Drive, gone, [], []);

                using var devices = MakeLine(gfx, ListKind.RemovableDevices);
                Offers(
                    ListKind.RemovableDevices,
                    devices,
                    [drives[0].Title, "Open", "Eject", "Properties", drives[1].Title, "Open", "Eject", "Properties"],
                    [@"E:\", @"E:\", @"E:\", @"F:\", @"F:\", @"F:\"]);
                var (named, _) = TheDock.ActRows(TheDock.ActsFor(ListKind.RemovableDevices), devices);
                Assert.Equal([true, false, false, false, true, false, false, false], named.Select(item => item.Header));

                var opened = new List<(string Target, string? Verb)>();
                foreach (var row in TheDock.ActRows(TheDock.ActsFor(ListKind.Drive), stick).Rows)
                {
                    TheDock.RunAct(row, stick, (target, verb) => opened.Add((target, verb)));
                }
                Assert.Equal([(@"E:\", (string?)null), (@"E:\", MenuVerbs.Eject), (@"E:\", MenuVerbs.Properties)], opened);
            });
        }
        finally
        {
            DriveWatch.Source = before;
        }
    }

    private static CellIsland MakeLine(Gfx gfx, ListKind kind, Action<CellItem>? set = null)
    {
        var widget = kind.New();
        widget.Id = kind.ToString().ToLowerInvariant();
        set?.Invoke(widget);
        if (Karpo.Dock.Islands.CreateLine(Config.IslandKey(null, widget.Id), widget, kind, gfx, new Config()) is not CellIsland island)
        {
            throw new Xunit.Sdk.XunitException($"{kind}: no line was made.");
        }
        _ = island.Refresh(gfx);
        return island;
    }
}
