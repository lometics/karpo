// §628 (the person, 2026-11-09: "when adding a layout it should not open a dropdown but should open a new page where you also
// have a preview of the different layout presets. there should also be one that starts just blank"): Add a layout's page in
// the Layouts entry's flyout, as the dock shows it (EditDeck), on a display of its own at 150 %, dark and light — its way
// back at its top, a tile for each preset, Blank first, each picturing its bar drawn by the dock's real scene with the
// preset's real widgets (LookPreviews' sample of LookShown.Itself), in the Appearance's mode; and the Layouts page with
// "Add a layout ›" at its foot. Nothing of this PC's: Windows' own pins, the clock at 9:41 (LookStand), the battery, the
// readings and the weather stand-ins. To KARPO_PNG_DIR as addlayout-<page|link>-<dark|light>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void AddLayoutSheets()
    {
        var machine = new LayoutMachine(WindowsTaskbar.DefaultPins(PinTargets.System), NoPenNoTouch, WindowsTaskbar.OutOfTheBox);
        WindowShots.OnHiddenDesktop("add-layout-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            FolderIsland.WeatherStand = SavedWeather.Cache();
            var probe = BatteryIsland.Probe;
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
            IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
            FolderIsland.PerformanceSources = new DataSources((_, widget) =>
                new PerformanceData(widget.Info!.Metric!.Value, PerformanceData.DeviceOf(widget.Info.Metric.Value, widget.Performance ?? new PerformanceOptions()))
                {
                    Reading = () => PresetReading(widget.Info.Metric.Value),
                });
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var tone = dark ? "dark" : "light";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = MenuCorpus.Fresh();
                    var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                    var add = PanelRows.Page(PanelPage.AddLayout, PanelRows.SideOf(PanelPage.AddLayout), PanelRows.GlyphOf(PanelPage.AddLayout),
                        TheDock.LooksItems(config, bar, dark, [], LooksScope.AddLayout, machine: machine));
                    var content = new SettingsContent([.. SettingsPanelTests.Content(dark: dark).Pages, add]);
                    foreach (var (scene, page) in new[] { ("page", PanelPage.AddLayout), ("link", PanelPage.Layouts) })
                    {
                        var drawn = 0;
                        EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"addlayout-{scene}-{tone}.png", dips: new Vector2(1920f, 1080f),
                            panel: deck =>
                            {
                                deck.Memory = new PanelMemory();
                                deck.Memory.Show(page);
                                deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), content);
                                deck.Land(page, null, light: false);
                                var layout = deck.Layout!;
                                Assert.True(layout.IsOpen);
                                // The Layouts entry stands selected whichever of its pages shows.
                                Assert.True(layout.Opened(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Layouts)));
                                if (page == PanelPage.AddLayout)
                                {
                                    Assert.False(layout.BackHead.IsEmpty);
                                    // A sample drawn for each tile in sight, the tiles' pictures, each once.
                                    drawn = deck.Previews?.Drawn ?? 0;
                                    var inSight = layout.Blocks.SelectMany(block => block.Lines).Count(line => line.Row.Picture is not null && layout.OnPanel(line.Rect).Y < layout.Viewport.Bottom);
                                    Assert.True(drawn >= Math.Min(inSight, 1), $"{drawn} samples for {inSight} tiles in sight");
                                }
                                else
                                {
                                    Assert.True(layout.BackHead.IsEmpty);
                                }
                            });
                    }
                }
            }
            finally
            {
                FolderIsland.PerformanceSources = null;
                FolderIsland.WeatherStand = null;
                BatteryIsland.Probe = probe;
                IndicatorSources.Made = null;
            }
        });
    }
}
