// The battery and the power button are presets (docs\widgets.md §94): what a click on each gave —
// the battery flyout, the charge with what it is doing and the time left, energy saver and the
// power mode; the power flyout, sleep, hibernate, shut down, restart, sign out and lock over the
// power mode — are folders of lines under the battery face (the charge, as the battery on the bar
// drew it) and the power face (its glyph), with the premade power actions as their footer. The
// power mode and the power commands are lines of their own (QuickPageTile). Made-up batteries and
// machines throughout (SampleBattery, BatteryIsland.Probe, a stand-in for the commands and for
// Windows' overlay): nothing here changes the power mode of the PC it runs on, or sleeps, shuts
// down, restarts, signs out or locks it.
//
// Pictured beside the flyouts they replace (rendered before they went: quick-battery-*.png,
// quick-power-*.png, battery-*-48-*.png): battery-preset-<dark|light>.png and
// power-preset-<dark|light>.png at 150 % and 225 % text, and the face on the bar,
// battery-face-<dark|light>.png.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class BatteryPowerPresetTests
{
    private static readonly PresetMachine WithBattery = new(HasBattery: true, FixedDrives: []);

    private static readonly PresetMachine OnTheWall = new(HasBattery: false, FixedDrives: []);

    private static readonly QuickLinkMachine Everything = new(Battery: true, WindowsTerminal: true, Sleep: true, Hibernate: true);

    private static readonly SystemStatus.Battery Draining = new(76, false, false);

    /// <summary>The quick sources with <paramref name="battery"/> for the power mode and <paramref name="machine"/> for the commands.</summary>
    private static QuickSources Sources(SampleBattery battery, QuickLinkMachine machine) => new()
    {
        Wifi = static () => new SampleWifi(),
        Bluetooth = static () => new SampleBluetooth(),
        Battery = () => battery,
        Desktops = static () => new SampleDesktopNames(),
        Machine = () => machine,
    };

    /// <summary>
    /// The preset <paramref name="id"/>'s folder on a PC with <paramref name="charge"/> (none: no
    /// battery), its power mode reading <paramref name="battery"/>; opened where asked.
    /// </summary>
    private static FolderIsland Made(Gfx gfx, string id, bool open, SystemStatus.Battery? charge = null, SampleBattery? battery = null, QuickLinkMachine? machine = null)
    {
        var mode = battery ?? new SampleBattery();
        var power = machine ?? Everything;
        QuickPageTile.Sources = Sources(mode, power);
        PowerButtonTile.MachineStand = () => power;
        BatteryIsland.Probe = () => charge;
        BatteryIsland.Made = () => new SampleBattery { Present = charge is not null };
        var holder = Presets.Folder(Presets.Find(id)!, (charge is null ? OnTheWall : WithBattery) with { Sleeps = power.Sleep, Hibernates = power.Hibernate });
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        if (open)
        {
            folder.Opened(gfx);
        }
        return folder;
    }

    private static void Done(FolderIsland folder)
    {
        folder.Closed();
        folder.Dispose();
        QuickPageTile.Sources = null;
        PowerButtonTile.Runner = null;
        PowerButtonTile.MachineStand = null;
        BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
        BatteryIsland.Made = null;
    }

    private static TileStyle Style(Look look, float scale = 1.5f) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    /// <summary>The line of <paramref name="page"/> laid out at the list's width: its controls, in the card's own px.</summary>
    private static (QuickPageTile Tile, D2D_RECT_F Box, IReadOnlyList<Widget> Placed) Laid(FolderIsland folder, QuickKind page, TileStyle style)
    {
        var tile = folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>().Single(each => each.Page == page);
        tile.Pump();
        var width = FolderFlyout.ListWidth(1.5f);
        var box = PopupPaint.Rect(0, 0, width, tile.ListHeight(width, style));
        tile.LayOut(box, style);
        return (tile, box, tile.Placed);
    }

    /// <summary>The rows of <paramref name="page"/>'s line.</summary>
    private static List<ListRow> Rows(FolderIsland folder, QuickKind page, TileStyle style) =>
        [.. Laid(folder, page, style).Placed.OfType<ListRow>()];

    /// <summary>Every word on the line: its labels, and its rows' titles.</summary>
    private static string Said(FolderIsland folder, QuickKind page, TileStyle style) =>
        string.Join("\n", Laid(folder, page, style).Placed.SelectMany(widget => widget switch
        {
            Label label => [label.Text],
            ListRow row => new[] { row.Title, row.Detail ?? "" },
            _ => [],
        }));

    /// <summary>A press and a release in the middle of the line's control named <paramref name="name"/>.</summary>
    private static void Click(FolderIsland folder, QuickKind page, TileStyle style, string name)
    {
        var (tile, box, placed) = Laid(folder, page, style);
        var widget = placed.FirstOrDefault(each => each.Name == name) ?? throw new Xunit.Sdk.XunitException($"no {name} on the line");
        var at = widget.Bounds;
        if (tile.CardPress(new Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2), box, style, WidgetSurface.List))
        {
            tile.CardRelease();
        }
        tile.Pump();
    }

    // --- The presets -----------------------------------------------------------------------------------

    /// <summary>
    /// The battery flyout as lines in its order — the battery, energy saver, the power mode — then its
    /// footer in the same card as the premade power actions, under the battery face; the battery's line
    /// left out on a PC without one, as the flyout left the charge out.
    /// </summary>
    [Fact]
    public void TheBatteryPresetIsTheFlyoutsLinesUnderTheBatteryFace()
    {
        var preset = Presets.Find(Presets.BatteryId)!;
        Assert.Equal(FolderFaces.BatteryId, preset.Face!.Kind);
        Assert.Equal(["battery", "energySaver", "powerMode", "actions"], preset.Items.Select(item => item.Form));
        Assert.Same(PremadeWidgets.PowerActions, preset.Items[^1].Premade);
        Assert.Equal(("power", "energySaver"), PremadeWidgets.PowerActions.Actions);

        var folder = Presets.Folder(preset, WithBattery).Folder!;
        Assert.Same(FolderFaces.Battery, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal([ListKind.Battery, ListKind.EnergySaver, ListKind.PowerMode, ListKind.Actions], folder.Cells.Select(cell => cell!.Island!.List));
        Assert.Equal(("power", "energySaver"), (folder.Cells[^1]!.Island!.Actions!.Left, folder.Cells[^1]!.Island!.Actions!.Right));

        var wall = Presets.Folder(preset, OnTheWall).Folder!;
        Assert.Equal([ListKind.EnergySaver, ListKind.PowerMode, ListKind.Actions], wall.Cells.Select(cell => cell!.Island!.List));
    }

    /// <summary>
    /// The power flyout's commands as the power buttons, a line each and nothing else (2026-10-11:
    /// "the power folder / premade group should only show the individual power buttons"), under the
    /// power face, in the person's order (2026-10-11: "take mine in the ... power options widget"):
    /// sign out, lock, sleep, restart, shut down; sleep only where Windows' power menu offers it, and
    /// hibernate not at all, as theirs has none.
    /// </summary>
    [Fact]
    public void ThePowerPresetIsThePowerButtonsUnderThePowerFace()
    {
        var preset = Presets.Find(Presets.PowerId)!;
        Assert.Equal(FolderFaces.PowerId, preset.Face!.Kind);
        Assert.Equal(["signOut", "lock", "sleep", "restart", "shutDown"], preset.Items.Select(item => item.Form));
        Assert.DoesNotContain(preset.Items, item => item.Form is "powerMode" or "actions" or "battery");
        var folder = Presets.Folder(preset, OnTheWall).Folder!;
        Assert.Same(FolderFaces.Power, folder.Face.Info);
        Assert.Empty(folder.Face.Info.Options);
        Assert.Equal([ListKind.SignOut, ListKind.Lock, ListKind.Sleep, ListKind.Restart, ListKind.ShutDown], folder.Cells.Select(cell => cell!.Island!.List));
        Assert.Equal(
            [ListKind.SignOut, ListKind.Lock, ListKind.Sleep, ListKind.Restart, ListKind.ShutDown],
            Presets.Folder(preset, OnTheWall with { Hibernates = true }).Folder!.Cells.Select(cell => cell!.Island!.List));
        Assert.Equal(
            [ListKind.SignOut, ListKind.Lock, ListKind.Restart, ListKind.ShutDown],
            Presets.Folder(preset, OnTheWall with { Sleeps = false }).Folder!.Cells.Select(cell => cell!.Island!.List));
    }

    /// <summary>
    /// The entries are gone from the bar and their flyouts with them; the battery stays a line, the
    /// power mode a line of its own drawn by the page's own controls, and each power command a
    /// button: the list's own row of its glyph and its name, whose click acts.
    /// </summary>
    [Fact]
    public void TheEntriesAreGoneAndTheirRowsAreLines()
    {
        Assert.Null(BarEntries.Find("battery"));
        Assert.Null(BarEntries.Find("power"));
        Assert.Equal(WidgetSource.Battery, ListWidgets.Find("battery")!.Drawing);
        Assert.Equal(WidgetSource.QuickPage, ListWidgets.Find("powerMode")!.Drawing);
        Assert.Null(ListWidgets.Find("powerCommands"));
        Assert.Null(WidgetInfos.Find("powerCommands"));
        foreach (var command in Enum.GetValues<PowerCommand>())
        {
            var button = ListWidgets.Find(WidgetInfos.For(command).Id)!;
            Assert.Equal((WidgetSource.PowerButton, ListLine.Row, CellAct.Action), (button.Drawing, button.Line, button.Act));
            Assert.Null(BarEntries.Find(button.Info.Id));
        }
        Assert.Null(WidgetInfos.Find("power"));
        Assert.DoesNotContain(Enum.GetNames<QuickKind>(), name => name is "Battery" or "Power");
        Assert.Equal([WidgetAct.WindowsSettings], Karpo.Dock.Dock.ActsFor(ListKind.PowerMode));
        Assert.Equal([WidgetAct.WindowsSettings], Karpo.Dock.Dock.ActsFor(ListKind.ShutDown));
        Assert.Empty(Karpo.Dock.Dock.ActsFor(ListKind.Lock));
        Assert.Equal("ms-settings:powersleep", Karpo.Dock.Dock.WindowsLinkOf(WidgetInfos.PowerMode)!.Value.Uri);
    }

    // --- The power mode --------------------------------------------------------------------------------

    /// <summary>
    /// The power mode's line: its heading, what it is about and the three modes, each with its own
    /// gauge, Balanced marked; a click asks for another, marked at once; one asked for again asks nothing.
    /// </summary>
    [UiFact]
    public void ThePowerModeLineMarksTheModeAndAsksForAnother() => Offscreen.OnStaThread(gfx =>
    {
        var battery = new SampleBattery();
        var folder = Made(gfx, Presets.BatteryId, open: true, Draining, battery);
        try
        {
            var style = Style(LookFor(dark: true));
            var said = Said(folder, QuickKind.PowerMode, style);
            Assert.Contains(S.DockQuickPowerMode, said);
            Assert.Contains(S.DockQuickPowerModeCaption, said);
            var modes = Rows(folder, QuickKind.PowerMode, style);
            Assert.Equal([S.DockQuickPowerEfficiency, S.DockQuickPowerBalanced, S.DockQuickPowerPerformance], modes.Select(row => row.Title));
            Assert.Equal(3, modes.Select(row => row.Glyph).Distinct().Count());
            Assert.Equal(S.DockQuickPowerBalanced, Assert.Single(modes, row => row.Selected).Title);

            Click(folder, QuickKind.PowerMode, style, S.DockQuickPowerPerformance);
            Assert.Equal([PowerMode.Performance], battery.Asked);
            Assert.Equal(S.DockQuickPowerPerformance, Assert.Single(Rows(folder, QuickKind.PowerMode, style), row => row.Selected).Title);
            Click(folder, QuickKind.PowerMode, style, S.DockQuickPowerPerformance);
            Assert.Equal([PowerMode.Performance], battery.Asked);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>Windows refusing a mode: the mark goes back to the mode in force, and the line says so.</summary>
    [UiFact]
    public void ARefusedModeGoesBackAndSaysSo() => Offscreen.OnStaThread(gfx =>
    {
        var battery = new SampleBattery { Refuses = true };
        var folder = Made(gfx, Presets.BatteryId, open: true, battery: battery);
        try
        {
            var style = Style(LookFor(dark: true));
            Click(folder, QuickKind.PowerMode, style, S.DockQuickPowerEfficiency);
            Assert.Equal([PowerMode.Efficiency], battery.Asked);
            Assert.Equal(S.DockQuickPowerBalanced, Assert.Single(Rows(folder, QuickKind.PowerMode, style), row => row.Selected).Title);
            Assert.Contains(S.DockQuickPowerModeRefused, Said(folder, QuickKind.PowerMode, style));
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>A PC whose plan takes no overlay offers no modes: the line says so, with no rows that would do nothing.</summary>
    [UiFact]
    public void APlanWithNoModesSaysSo() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, Presets.BatteryId, open: true, battery: new SampleBattery { Present = false, Offers = null });
        try
        {
            var style = Style(LookFor(dark: true));
            Assert.Empty(Rows(folder, QuickKind.PowerMode, style));
            Assert.Contains(S.DockQuickPowerModeNone, Said(folder, QuickKind.PowerMode, style));
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>
    /// The battery face's menu keeps the power modes where the plan offers none: under their heading,
    /// every one greyed and none ticked, rather than left out (§247).
    /// </summary>
    [UiFact]
    public void TheFacesMenuGreysThePowerModesWhereThereAreNone() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, Presets.BatteryId, open: false, battery: new SampleBattery { Present = false, Offers = null });
        try
        {
            var (items, _) = Karpo.Dock.Dock.ActRows([WidgetAct.PowerMode], folder);
            Assert.Equal(S.DockQuickPowerMode, items[0].Text);
            var modes = items.Where(item => !item.Header && !item.Separator).ToList();
            Assert.Equal(PowerModePage.Modes.Length, modes.Count);
            Assert.All(modes, mode => Assert.False(mode.Enabled));
            Assert.All(modes, mode => Assert.NotEqual(true, mode.Checked));
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>
    /// Windows is asked for the mode off the caller's thread (the folder never stands still for
    /// powrprof): SetMode comes back while Windows is still answering, the answer is asked on another
    /// thread, and it comes back as news — the mode in force, or a refusal. A stand-in answers for
    /// Windows: the PC's own power mode is never touched.
    /// </summary>
    [Fact]
    public void TheModeIsAskedOffTheCallersThread()
    {
        using var asking = new ManualResetEventSlim();
        using var answer = new ManualResetEventSlim();
        var asked = new List<(Guid Overlay, int Thread)>();
        var takes = true;
        using var battery = new WindowsBattery(overlay =>
        {
            lock (asked)
            {
                asked.Add((overlay, Environment.CurrentManagedThreadId));
            }
            asking.Set();
            answer.Wait(TimeSpan.FromSeconds(10));
            return takes;
        });
        var news = 0;
        var changes = new Changes(_ => { });
        battery.Start(changes);

        battery.SetMode(PowerMode.Performance);
        Assert.True(asking.Wait(TimeSpan.FromSeconds(10)), "Windows was never asked");
        // Back already, while Windows has not answered.
        Assert.False(WindowsBattery.Setting.IsCompleted);
        answer.Set();
        Assert.True(WindowsBattery.Setting.Wait(TimeSpan.FromSeconds(10)));
        news |= changes.Take();
        Assert.Equal(BatteryNews.Power, news & BatteryNews.Power);
        Assert.Equal(0, news & BatteryNews.Refused);

        takes = false;
        battery.SetMode(PowerMode.Balanced);
        Assert.True(WindowsBattery.Setting.Wait(TimeSpan.FromSeconds(10)));
        Assert.Equal(BatteryNews.Refused, changes.Take() & BatteryNews.Refused);

        Assert.Equal([WindowsBattery.BestPerformance, Guid.Empty], asked.Select(each => each.Overlay));
        Assert.DoesNotContain(asked, each => each.Thread == Environment.CurrentManagedThreadId);
    }

    // --- The power buttons -----------------------------------------------------------------------------

    /// <summary>
    /// Each button is one command of Windows' power menu, named and drawn as the quick-link menu's,
    /// in its order. A click asks the folder to close and does nothing yet; the dock does the command
    /// once the folder has gone (Act), asking nothing — handed to the stand-in here, never run.
    /// </summary>
    [UiFact]
    public void EachButtonIsItsCommandAndActsOnceTheFolderHasGone() => Offscreen.OnStaThread(gfx =>
    {
        var ran = new List<QuickLink>();
        var folder = Made(gfx, Presets.PowerId, open: true);
        PowerButtonTile.Runner = ran.Add;
        try
        {
            var buttons = folder.Tiles.Select(tile => tile.Island).OfType<PowerButtonTile>().ToList();
            Assert.Equal([QuickLink.SignOut, QuickLink.Lock, QuickLink.Sleep, QuickLink.Restart, QuickLink.ShutDown], buttons.Select(button => button.Link));
            Assert.Equal(
                [S.DockQuickLinkSignOut, S.DockQuickLinkLock, S.DockQuickLinkSleep, S.DockQuickLinkRestart, S.DockQuickLinkShutDown],
                buttons.Select(button => button.Info.Title));
            Assert.Equal(buttons.Count, buttons.Select(button => button.Info.Glyph).Distinct().Count());
            Assert.All(buttons, button => Assert.Null(button.Missing));

            var locks = buttons[1];
            Assert.Equal(IslandView.App, locks.Click(0).View);
            Assert.Empty(ran);
            locks.Act();
            buttons[2].Act();
            Assert.Equal([QuickLink.Lock, QuickLink.Sleep], ran);
        }
        finally
        {
            Done(folder);
        }
    });

    /// <summary>
    /// A button for a command this PC's power menu does not offer — sleep taken out, hibernate never
    /// turned on — says so in its hover, and its click does nothing: no folder closes, nothing runs.
    /// </summary>
    [UiFact]
    public void AButtonThePowerMenuDoesNotOfferSaysSoAndDoesNothing() => Offscreen.OnStaThread(gfx =>
    {
        var ran = new List<QuickLink>();
        PowerButtonTile.MachineStand = static () => new QuickLinkMachine(Battery: false, WindowsTerminal: true, Sleep: false, Hibernate: false);
        PowerButtonTile.Runner = ran.Add;
        var sleep = new PowerButtonTile("test/sleep", ListKind.Sleep.New(), gfx, new Config());
        var restart = new PowerButtonTile("test/restart", ListKind.Restart.New(), gfx, new Config());
        try
        {
            Assert.Equal(S.DockPowerNotOffered, sleep.Missing);
            Assert.Equal($"{WidgetInfos.Sleep.Title}\n{S.DockPowerNotOffered}", sleep.Tooltip(0));
            Assert.Equal(IslandView.None, sleep.Click(0).View);
            sleep.Act();
            Assert.Empty(ran);
            Assert.Null(restart.Missing);
            Assert.Equal(WidgetInfos.Restart.Title, restart.Tooltip(0));
            Assert.True(PowerButtonTile.Offered(PowerCommand.Hibernate, new QuickLinkMachine(false, false, Sleep: false, Hibernate: true)));
            Assert.False(PowerButtonTile.Offered(PowerCommand.Hibernate, new QuickLinkMachine(false, false, Sleep: true, Hibernate: false)));
        }
        finally
        {
            sleep.Dispose();
            restart.Dispose();
            PowerButtonTile.Runner = null;
            PowerButtonTile.MachineStand = null;
        }
    });

    /// <summary>The footer opens Power &amp; battery, for the flyouts' "Battery settings" and "Power settings", and Energy saver.</summary>
    [UiFact]
    public void TheFooterOpensPowerAndBatteryAndEnergySaver() => Offscreen.OnStaThread(gfx =>
    {
        var folder = Made(gfx, Presets.BatteryId, open: true, Draining);
        var opened = new List<string>();
        ActionsTile.Opener = opened.Add;
        try
        {
            var actions = folder.Tiles.Select(tile => tile.Island).OfType<ActionsTile>().Single();
            actions.Open(ActionsTile.LeftTarget);
            actions.Open(ActionsTile.RightTarget);
            Assert.Equal(["ms-settings:powersleep", "ms-settings:batterysaver"], opened);
        }
        finally
        {
            ActionsTile.Opener = null;
            Done(folder);
        }
    });

    // --- The faces -------------------------------------------------------------------------------------

    /// <summary>What the battery face says: the percentage, held at 100, none where unknown (its hover: <see cref="TheBatteryFaceIsAsWideAsTheChargeItShows"/>).</summary>
    [Fact]
    public void TheBatteryFaceSaysThePercentage()
    {
        Assert.Equal("76%", BatteryFaceDrawing.Text(Draining));
        Assert.Equal("100%", BatteryFaceDrawing.Text(new SystemStatus.Battery(104, true, false)));
        Assert.Equal("", BatteryFaceDrawing.Text(new SystemStatus.Battery(-1, true, false)));
    }

    /// <summary>
    /// The charge moving, the charger plugged in and energy saver paint the face again in place —
    /// the glyph, the figure, the hover — never a rebuild, and never another size; a tick with
    /// nothing new paints nothing. A power broadcast makes it read again, as the battery did.
    /// </summary>
    [UiFact]
    public void TheBatteryFacePaintsAgainInPlaceAsTheChargeMoves() => Offscreen.OnStaThread(gfx =>
    {
        var charge = (SystemStatus.Battery?)Draining;
        QuickPageTile.Sources = Sources(new SampleBattery(), Everything);
        // The folder reads the charge where the probe in force as it is made reads it: this one.
        BatteryIsland.Probe = () => charge;
        var island = new FolderIsland($"{Owner}/battery", Presets.Folder(Presets.Find(Presets.BatteryId)!, WithBattery), gfx, new Config());
        try
        {
            Assert.True(island.ShowsBattery);
            Assert.Equal(60_000, island.IntervalMs);
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor(dark: true));
            var size = island.CellSize(0, style);
            _ = island.Measure(style);
            _ = island.Surface(0, style);
            Assert.Contains("|76%|", island.FaceShown, StringComparison.Ordinal);
            var painted = 0;
            PaintWatch.Watching = (_, _, color) =>
            {
                painted++;
                return color;
            };
            try
            {
                Assert.False(island.FaceTick(gfx));
                Assert.Equal(0, painted);

                charge = new SystemStatus.Battery(75, false, false);
                Assert.False(island.Tick(gfx));
                Assert.True(painted > 0, "a charge that fell was not painted");
                Assert.Contains("|75%|", island.FaceShown, StringComparison.Ordinal);
                Assert.Equal(SystemStatus.Describe(charge.Value).Tip, island.Tooltip(0));
                Assert.Equal(size, island.CellSize(0, style));

                painted = 0;
                charge = new SystemStatus.Battery(75, true, false);
                const uint PBT_APMPOWERSTATUSCHANGE = 0x000A;
                island.System(TerraFX.Interop.Windows.WM.WM_POWERBROADCAST, (TerraFX.Interop.Windows.WPARAM)(nuint)PBT_APMPOWERSTATUSCHANGE, default);
                Assert.False(((IIsland)island).Refresh(gfx));
                Assert.True(painted > 0, "the charger plugged in was not painted");
                Assert.StartsWith(SystemStatus.Describe(charge.Value).Glyph, island.FaceShown, StringComparison.Ordinal);
                Assert.Equal(size, island.CellSize(0, style));
            }
            finally
            {
                PaintWatch.Watching = null;
            }
        }
        finally
        {
            Done(island);
        }
    });

    /// <summary>
    /// The battery face across a bar is as wide as the charge it shows (docs\widgets.md §244): "5%"
    /// narrower than "45%", "45%" than "100%", two figures one width whatever they are (tabular), and
    /// no figure narrower still; down a bar on its side one size; and on a PC without a battery, or
    /// one that does not know its charge, as wide as a full one, which it shows greyed (§247), with
    /// nothing ticking.
    /// </summary>
    [UiFact]
    public void TheBatteryFaceIsAsWideAsTheChargeItShows() => Offscreen.OnStaThread(gfx =>
    {
        SystemStatus.Battery[] charges = [new(5, false, false), new(45, true, false), new(15, false, true), new(100, true, false), new(-1, true, false)];
        foreach (var height in new[] { 28, 48, 72, 115 })
        {
            var config = new Config { Height = height };
            foreach (var vertical in new[] { false, true })
            {
                foreach (var text in new[] { 1f, 2.25f })
                {
                    var style = new IslandStyle(1.5f, vertical, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true, text));
                    var sizes = charges.Select(charge =>
                    {
                        var island = Made(gfx, Presets.BatteryId, open: false, charge);
                        try
                        {
                            return island.CellSize(0, style);
                        }
                        finally
                        {
                            Done(island);
                        }
                    }).ToList();
                    var where = $"{height} DIP, text ×{text}{(vertical ? ", vertical" : "")}";
                    if (vertical)
                    {
                        Assert.True(sizes.Distinct().Count() == 1, $"{where}: {string.Join(", ", sizes)}");
                    }
                    else
                    {
                        var (five, fortyFive, fifteen, hundred, none) = (sizes[0].Width, sizes[1].Width, sizes[2].Width, sizes[3].Width, sizes[4].Width);
                        // A tall bar's cell, the least a widget takes, may hold even "100%".
                        Assert.True(five <= fortyFive && fortyFive <= hundred && (height > 48 || fortyFive < hundred),
                            $"{where}: 5% {five}, 45% {fortyFive}, 100% {hundred}");
                        Assert.Equal(fortyFive, fifteen);
                        Assert.Equal(hundred, none);
                    }

                    var wall = Made(gfx, Presets.BatteryId, open: false);
                    try
                    {
                        Assert.Equal(sizes[3], wall.CellSize(0, style));
                        Assert.Equal(0, wall.IntervalMs);
                        Assert.Equal(CellIsland.NotOnThisPc(WidgetInfos.Battery.Title, S.WidgetBatteryNoBattery), wall.Tooltip(0));
                    }
                    finally
                    {
                        Done(wall);
                    }
                }
            }
        }
    });

    /// <summary>The power face: its glyph and the button's ends at every bar (§275), never painted again, its hover its name.</summary>
    [UiFact]
    public void ThePowerFaceIsItsGlyph() => Offscreen.OnStaThread(gfx =>
    {
        var island = Made(gfx, Presets.PowerId, open: false);
        try
        {
            foreach (var height in new[] { 28, 48, 72 })
            {
                var config = new Config { Height = height };
                var style = new IslandStyle(1.5f, false, Layout.CellPx(144, config), Layout.IconPx(144, config), LookFor(dark: true));
                Assert.Equal(FaceWidthTests.GlyphButton(style), island.CellSize(0, style).Width, 0.5f);
            }
            Assert.Equal(0, island.IntervalMs);
            Assert.False(island.FaceTick(gfx));
            Assert.Equal(S.WidgetPowerName, island.Tooltip(0));
        }
        finally
        {
            Done(island);
        }
    });

    /// <summary>
    /// The battery face's right click: energy saver, where the PC has it, and Windows' battery icon's
    /// Power &amp; battery settings (its power modes come from a folder made); with no folder to ask,
    /// the page alone (§308). The power face's Power &amp; battery too.
    /// </summary>
    [Fact]
    public void TheFacesRowsAreTheEntriesRows()
    {
        Assert.Equal([WidgetAct.PowerMode, WidgetAct.EnergySaver, WidgetAct.PowerSettings], Karpo.Dock.Dock.ActsFor(FolderFaces.Battery));
        var (items, rows) = Karpo.Dock.Dock.BarMenuRows(Presets.Folder(Presets.Find(Presets.BatteryId)!, WithBattery));
        Assert.Equal([WindowsPages.Power.Title], items.Select(item => item.Text));
        Assert.Equal([WidgetAct.PowerSettings], rows.Select(row => row.Act));
        Assert.Equal("ms-settings:powersleep", Karpo.Dock.Dock.TargetOf(WidgetAct.PowerSettings));
        Assert.Equal([WindowsPages.Power.Title], Karpo.Dock.Dock.BarMenuRows(Presets.Folder(Presets.Find(Presets.PowerId)!, OnTheWall)).Items.Select(item => item.Text));
    }

    // --- Pictures --------------------------------------------------------------------------------------

    /// <summary>
    /// The battery preset on a battery, charging, on a PC on the wall, with a mode refused, and at 225 %
    /// text; the power preset on a PC that sleeps, on one that does not, and at 225 %
    /// text — at 150 %, to stand beside quick-battery-*.png and quick-power-*.png of the flyouts.
    /// </summary>
    [UiFact]
    public void PresetPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    List<(string, Look, FolderIsland, FolderPlan)> panels = [];
                    void Add(string caption, string id, SystemStatus.Battery? charge, float text = 1f, SampleBattery? battery = null, QuickLinkMachine? machine = null, string? press = null)
                    {
                        var look = LookFor(dark, text);
                        var folder = Made(gfx, id, open: true, charge, battery, machine);
                        folders.Add(folder);
                        if (press is not null)
                        {
                            Click(folder, QuickKind.PowerMode, Style(look), press);
                        }
                        foreach (var tile in folder.Tiles.Select(each => each.Island).OfType<QuickPageTile>())
                        {
                            _ = Laid(folder, tile.Page, Style(look));
                        }
                        panels.Add((caption, look, folder, Plan(folder, look, 1.5f).Plan));
                    }

                    Add("On battery: 76%", Presets.BatteryId, Draining);
                    Add("Charging, in energy saver", Presets.BatteryId, new SystemStatus.Battery(45, true, true), battery: new SampleBattery { Saver = true });
                    Add("On the wall", Presets.BatteryId, null, battery: new SampleBattery { Present = false });
                    Add("A mode refused", Presets.BatteryId, Draining, battery: new SampleBattery { Refuses = true }, press: S.DockQuickPowerPerformance);
                    Add("225 % text", Presets.BatteryId, Draining, 2.25f);
                    Sheet(gfx, $"battery-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);

                    panels = [];
                    Add("Sleeps", Presets.PowerId, null);
                    Add("No sleep", Presets.PowerId, null, machine: new QuickLinkMachine(Battery: false, WindowsTerminal: true, Sleep: false, Hibernate: false));
                    Add("225 % text", Presets.PowerId, null, 2.25f);
                    Sheet(gfx, $"power-preset-{(dark ? "dark" : "light")}.png", 1.5f, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                    QuickPageTile.Sources = null;
                    PowerButtonTile.MachineStand = null;
                    BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
                    BatteryIsland.Made = null;
                }
            });
        }
    }

    /// <summary>
    /// The battery face and the power face on a bar of 40 and 48 DIP and at 150 %, and down a vertical
    /// bar: a battery draining, low, charging, in energy saver and full, a PC on the wall, and the power
    /// face — battery-face-&lt;dark|light&gt;.png, beside battery-*-48-*.png of the battery on the bar.
    /// </summary>
    [UiFact]
    public void FacePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                var look = LookFor(dark);
                var theme = FlyoutTheme.For(look);
                (string Id, SystemStatus.Battery? Charge)[] states =
                [
                    (Presets.BatteryId, Draining),
                    (Presets.BatteryId, new SystemStatus.Battery(7, false, false)),
                    (Presets.BatteryId, new SystemStatus.Battery(45, true, false)),
                    (Presets.BatteryId, new SystemStatus.Battery(30, false, true)),
                    (Presets.BatteryId, new SystemStatus.Battery(100, true, false)),
                    (Presets.BatteryId, null),
                    (Presets.PowerId, null),
                ];
                var styles = new[] { (40, 96u, false), (48, 96u, false), (48, 144u, false), (48, 144u, true) }
                    .Select(each => new IslandStyle(each.Item2 / 96f, each.Item3, Layout.CellPx(each.Item2, new Config { Height = each.Item1 }), Layout.IconPx(each.Item2, new Config { Height = each.Item1 }), look))
                    .ToList();
                const float gap = 12;
                // Each state is made, measured and painted with its own charge in force: whether
                // there is a battery at all is read as it is measured.
                var cells = new List<(IslandStyle Style, (string Id, SystemStatus.Battery? Charge) State, float X, float Y, float W, float H)>();
                var x = gap;
                var tallest = 0f;
                foreach (var style in styles)
                {
                    foreach (var state in states)
                    {
                        var island = Made(gfx, state.Id, open: false, state.Charge);
                        try
                        {
                            var (w, h) = island.CellSize(0, style);
                            cells.Add((style, state, x, gap, w, h));
                            x += (style.Vertical ? w : w) + gap;
                            tallest = MathF.Max(tallest, h);
                        }
                        finally
                        {
                            Done(island);
                        }
                    }
                    x += gap;
                }
                var width = x;
                var height = gap + tallest + gap;
                Offscreen.Bitmap(width, height, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                    foreach (var (style, state, left, top, w, h) in cells)
                    {
                        var island = Made(gfx, state.Id, open: false, state.Charge);
                        try
                        {
                            PopupPaint.Fill(dc, PopupPaint.Rect(left, top, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                            Offscreen.Shifted(dc, left, top, () => island.PaintCell(dc, 0, style, w, h));
                        }
                        finally
                        {
                            Done(island);
                        }
                    }
                }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"battery-face-{(dark ? "dark" : "light")}.png")));
            });
        }
    }
}
