// §472: each layout preset as "Add a layout" makes it, on a display of its own at 150 %, light and dark, at rest over a
// wallpaper: its bars where they stand, wearing its style, drawn by the dock's real scene with real widgets on a desktop of
// its own (WindowShots) — and, but for Windows', whose clock opens Windows' own notification center, and §628 Blank's, which
// has none, its clock's folder
// open over it (the Planner's inbox: its Today's forecast would need the network). Nothing of this PC's is shown: the time, the weather, the battery, the readings and the notifications are
// stand-ins. To KARPO_PNG_DIR as layout-preset-<id>-<light|dark>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>What the stand-in readings say: the processor a quarter busy, the graphics at 8 %, the memory near half, the disk and the network quiet.</summary>
    private static float PresetReading(Metric metric) => metric switch
    {
        Metric.Cpu => 0.23f,
        Metric.Gpu => 0.08f,
        Metric.Memory => 0.47f,
        Metric.Disk => 0.03f,
        _ => 0.12f,
    };

    [UiFact]
    public void LayoutPresetSheets()
    {
        var machine = new LayoutMachine(WindowsTaskbar.DefaultPins(PinTargets.System), NoPenNoTouch, WindowsTaskbar.OutOfTheBox);
        var folder = NotificationsPresetTests.Temp();
        try
        {
            WindowShots.OnHiddenDesktop("layout-presets", (gfx, instance) =>
            {
                Clock.Now = () => new DateTime(2026, 11, 2, 9, 41, 0);
                FolderIsland.WeatherStand = SavedWeather.Cache();
                var probe = BatteryIsland.Probe;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
                IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
                FolderIsland.PerformanceSources = new DataSources((_, widget) =>
                    new PerformanceData(widget.Info!.Metric!.Value, PerformanceData.DeviceOf(widget.Info.Metric.Value, widget.Performance ?? new PerformanceOptions()))
                    {
                        Reading = () => PresetReading(widget.Info.Metric.Value),
                    });
                NotificationPartTile.Stand = NotificationsPresetTests.Store(folder,
                [
                    (1001L, 1, 3, "<toast><visual><binding template=\"ToastGeneric\"><text>Design review</text><text>Moved to 14:00 in the small room.</text></binding></visual></toast>"),
                    (1002L, 2, 18, "<toast><visual><binding template=\"ToastGeneric\"><text>Lunch on Friday?</text><text>The new place by the river opens this week.</text></binding></visual></toast>"),
                    (1003L, 3, 52, "<toast><visual><binding template=\"ToastGeneric\"><text>Standup</text><text>In 10 minutes</text></binding></visual></toast>"),
                ], "layout-presets");
                try
                {
                    foreach (var preset in LayoutPresets.All)
                    {
                        foreach (var dark in new[] { false, true })
                        {
                            var config = new Config();
                            var layout = BarLooks.Add(config, preset, machine: machine);
                            var look = (AddListPictures.LookFor(dark) with { Animations = false }).Wearing(config);
                            gfx.Typeface = Looks.MenuText(config);
                            try
                            {
                                EditedDisplay(gfx, instance, 144, config, look, new Picture(layout, Edge.Bottom), $"layout-preset-{preset.Id}-{(dark ? "dark" : "light")}.png",
                                    editing: false, folder: preset != LayoutPresets.Windows && preset != LayoutPresets.Blank, dips: new System.Numerics.Vector2(1440f, 860f),
                                    folderFace: preset == LayoutPresets.Planner ? FolderFaces.MailId : null);
                            }
                            finally
                            {
                                gfx.Typeface = default;
                            }
                        }
                    }
                }
                finally
                {
                    NotificationPartTile.Stand = null;
                    FolderIsland.PerformanceSources = null;
                    FolderIsland.WeatherStand = null;
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
}
