// The bar a brand-new Karpo starts with (2026-10-13: "for the default layout when a user first starts
// karpo, show the date and time over each other like windows does it and also group the
// notifications functionality with the bell there as well ... so that it matches the windows 11
// default behaviour / layout as close as possible"): a config made in an empty folder and repaired as
// the dock's first run does, its first display's layout drawn by the dock's real scene with real
// widgets over a wallpaper, on stand-ins for everything live — a saved forecast, a battery at 76 %,
// Wi-Fi and the speaker, seven notifications, three open apps and four tray icons. To KARPO_PNG_DIR
// as windows-default-<look>-<scale>-<width>.png, dark and light, at 100 % and 150 %, on 1920- and
// 2560-pixel displays; and the right end of each alone as windows-default-corner-<look>-<scale>.png.
// The flyouts it opens are Folders\CombinedPresetTests' combined-clockNotificationsGroup.png and
// combined-quickPanelGroup.png, and NotificationCenterPictures below with the notifications in.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>A brand-new config's first display's layout, as the dock's first run makes it.</summary>
    private static List<BarItem> FreshBar()
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Display");
        var bar = config.ActiveLayout("D1").Islands;
        // The weather's face on a saved forecast's place, never this PC's.
        foreach (var island in bar)
        {
            if (island.Folder?.FaceOf(FolderFaces.WeatherId) is { } weather)
            {
                weather.Weather = SavedWeather.Options();
            }
        }
        return bar;
    }

    [UiFact]
    public void WindowsDefaultSheets()
    {
        var folder = NotificationsPresetTests.Temp();
        try
        {
            WindowShots.OnHiddenDesktop("windows-default", (gfx, _) =>
            {
                var store = NotificationsPresetTests.Store(folder, NotificationsPresetTests.Centre);
                Clock.Now = () => new DateTime(2026, 10, 13, 22, 19, 0);
                FolderIsland.WeatherStand = SavedWeather.Cache();
                NotificationPartTile.Stand = store;
                var probe = BatteryIsland.Probe;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
                IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
                try
                {
                    foreach (var dark in new[] { true, false })
                    {
                        foreach (var dpi in new uint[] { 96, 144 })
                        {
                            var look = AddListPictures.LookFor(dark) with { Animations = false };
                            var config = new Config();
                            var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                            var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                            foreach (var width in new[] { 1920, 2560 })
                            {
                                var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                                var bar = FreshBar();
                                var (specs, islands) = Made(gfx, config, bar);
                                try
                                {
                                    var content = new Content
                                    {
                                        Islands = islands,
                                        Tiles = [.. Enumerable.Range(0, 3).Select(BusyApp)],
                                        Tray = DraggedTrayIcons(4, px),
                                        TrayOverflow = true,
                                    };
                                    var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                                    // The clock button's time over the date stands left of its bell, show desktop last.
                                    var clock = layout.Parts[bar.FindIndex(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId)];
                                    Assert.Equal(BarKind.ShowDesktop, layout.Parts[^1].Kind);
                                    Assert.True(clock.Right <= layout.Parts[^1].Left + 0.5f);
                                    OverADesktop(gfx, layout, look, content, null, $"windows-default-{name}-{width}.png");
                                    if (width == 1920)
                                    {
                                        var tray = layout.Parts.Single(part => part.Kind == BarKind.Tray);
                                        OverADesktop(gfx, layout, look, content, null, $"windows-default-corner-{name}.png", (int)tray.Left - (int)(24 * dpi / 96f));
                                    }
                                }
                                finally
                                {
                                    foreach (var island in islands.Values)
                                    {
                                        island.Dispose();
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    FolderIsland.WeatherStand = null;
                    NotificationPartTile.Stand = null;
                    BatteryIsland.Probe = probe;
                    IndicatorSources.Made = null;
                    Clock.Now = Clock.Real;
                }
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }

    /// <summary>
    /// The clock button's flyout as a new bar opens it, with none, three and twelve notifications over
    /// the calendar, on a 1080p and a 1440p display over the bar, at 100 % and 150 %:
    /// windows-default-clock-flyout-&lt;1080|1440&gt;-&lt;scale&gt;.png. The notifications Fill (§171, §173):
    /// up to the top of the work area less the gap and the margin, as Windows' notification center
    /// reaches, whatever their count, and scrolling under their count past it — the calendar under
    /// them whole every time.
    /// </summary>
    [UiFact]
    public void NotificationCenterPictures()
    {
        var folder = NotificationsPresetTests.Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                var stores = new[] { 0, 3, 12 }.ToDictionary(count => count, count => NotificationsPresetTests.Store(folder,
                    count == 12 ? NotificationsPresetTests.Twelve : NotificationsPresetTests.Centre[..count], name: $"n{count}"));
                Clock.Now = () => new DateTime(2026, 10, 13, 22, 19, 0);
                try
                {
                    foreach (var screen in new[] { 1080, 1440 })
                    {
                        foreach (var scale in new[] { 1f, 1.5f })
                        {
                            var made = new List<FolderIsland>();
                            try
                            {
                                var work = screen - PopupPaint.Round(48 * scale);
                                // To the top of the work area less the gap and the margin (§173), and an arranged list's "+" (§185).
                                var most = FolderFlyout.Reach(work, 0, scale);
                                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                                foreach (var (count, store) in stores)
                                {
                                    var holder = FreshBar().Single(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
                                    NotificationPartTile.Stand = store;
                                    FolderIsland island;
                                    try
                                    {
                                        island = new FolderIsland($"{ListFormPictures.Owner}/{holder.Id}-{screen}-{count}", holder, gfx, new Config());
                                    }
                                    finally
                                    {
                                        NotificationPartTile.Stand = null;
                                    }
                                    made.Add(island);
                                    _ = ((IIsland)island).Refresh(gfx);
                                    island.Opened(gfx);
                                    var look = LookFor(true);
                                    var natural = FolderFlyout.Natural(island.Tiles, island.Laid, scale, look);
                                    var plan = FolderFlyout.Build(look, scale, island.View, island.Laid, [.. island.Tiles.Select(tile => tile.Cell)], work, natural: natural);
                                    var where = $"{count} on {screen}p at {scale * 100:0} %";
                                    Assert.True(!plan.Hidden.Any(), $"{where}: left out {string.Join(",", plan.Hidden)}");
                                    var notifications = island.Tiles.ToList().FindIndex(tile => tile.Cell.Island.Kind == "notificationsList");
                                    var lines = string.Join(",", island.Tiles.Select((tile, index) => $"{tile.Cell.Island.Kind} {Given(plan, index)}/{natural[index]}"));
                                    // To the top of the work area, whatever the count, the calendar under the
                                    // notifications whole: every other line as tall as with no room to fill.
                                    Assert.True(MathF.Abs(plan.Height - most) < 1, $"{where}: {plan.Height} px of {most}; {lines}");
                                    var unfilled = FolderFlyout.Build(look, scale, island.View, island.Laid, [.. island.Tiles.Select(tile => tile.Cell)], 100_000, natural: natural, room: 1);
                                    for (var index = 0; index < island.Tiles.Count; index++)
                                    {
                                        if (index != notifications)
                                        {
                                            Assert.True(MathF.Abs(Given(plan, index) - Given(unfilled, index)) < 1, $"{where}: {lines}");
                                        }
                                    }
                                    panels.Add(($"{count} notifications, {screen}p: {plan.Width / scale:0} × {plan.Height / scale:0} DIP of {most / scale:0}", look, island, plan));
                                }
                                Sheet(gfx, $"windows-default-clock-flyout-{screen}-{scale * 100:0}.png", scale, panels);
                            }
                            finally
                            {
                                foreach (var island in made)
                                {
                                    island.Closed();
                                    island.Dispose();
                                }
                            }
                        }
                    }
                }
                finally
                {
                    Clock.Now = Clock.Real;
                }
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }
}
