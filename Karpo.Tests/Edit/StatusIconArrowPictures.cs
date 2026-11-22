// The status icons' arrow (Karpo.Core\StatusIcons.cs, IndicatorsIsland): the bar a new config gets,
// with five status icons so — the microphone in use, a metered network and a restart waiting on the
// bar, the camera in use and the mobile hotspot set behind the arrow, and do not disturb hidden
// completely — drawn by the dock's real scene with real widgets over a desktop of the tests' own and
// captured as the compositor draws it (WindowShots), to KARPO_PNG_DIR:
//   status-arrow-bar-<look>-<scale>.png        the right end of the bar: three status icons, then
//                                              their arrow, on the status icons' one card;
//   status-arrow-bar-hover-<look>-<scale>.png  the same, the arrow lit under the pointer;
//   status-arrow-flyout-<look>-<scale>.png     the arrow's flyout as the dock opens it (Dock.ShowFlyout,
//                                              IslandAction.Icons): the two icons behind it in the
//                                              bar's squares, the panel no larger than they are;
// dark and light, at 100 % and 150 %. Every part is measured by the real Layout.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Five status icons so, one of them hidden completely.</summary>
    private static IndicatorState FiveStatusIcons => new()
    {
        Microphone = ["Teams"],
        Camera = ["Teams"],
        Hotspot = true,
        Metered = true,
        DoNotDisturb = true,
        Update = UpdateState.Due,
    };

    /// <summary>The camera and the hotspot behind the arrow, do not disturb nowhere.</summary>
    private static StatusIconOptions? ArrowOptions =>
        StatusIcons.With(StatusIcons.With(StatusIcons.With(null, StatusIcon.Camera, TrayState.Overflow), StatusIcon.Hotspot, TrayState.Overflow), StatusIcon.DoNotDisturb, TrayState.Hidden);

    [UiFact]
    public void StatusIconArrowSheets()
    {
        WindowShots.OnHiddenDesktop("status-arrow", (gfx, instance) =>
        {
            // Per thread: the hidden desktop's.
            IndicatorSources.Made = () => new SampleIndicators(FiveStatusIcons);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var scale = dpi / 96f;
                        var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        var config = new Config { Height = 48 };
                        var width = dpi == 96 ? 1600 : 2400;
                        var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch);
                        bar.Single(item => item.Kind == BarKind.Indicators).StatusIcons = ArrowOptions;
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var content = new Content
                            {
                                Islands = islands,
                                Tiles = [.. Enumerable.Range(0, 3).Select(BusyApp)],
                                Tray = DraggedTrayIcons(2, (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config))),
                                TrayOverflow = true,
                            };
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                            var statusPart = layout.Parts.FindIndex(part => part.Kind == BarKind.Indicators);
                            var status = layout.Parts[statusPart];
                            var island = Assert.IsType<IndicatorsIsland>(islands[status.Key]);
                            // Three on the bar, then the arrow; do not disturb nowhere.
                            Assert.Equal(4, status.Count);
                            Assert.Equal(3, island.Chevron);
                            Assert.Equal([IndicatorKind.Microphone, IndicatorKind.Metered, IndicatorKind.WindowsUpdate], island.Shown.Select(icon => icon.Kind));
                            Assert.Equal([IndicatorKind.Camera, IndicatorKind.Hotspot], island.Behind.Select(icon => icon.Kind));

                            var from = (int)layout.Parts.Single(part => part.Kind == BarKind.Tray).Left - (int)(48 * scale);
                            OverADesktop(gfx, layout, look, content, null, $"status-arrow-bar-{name}.png", from);
                            OverADesktop(gfx, layout, look, content, null, $"status-arrow-bar-hover-{name}.png", from,
                                scene => scene.Highlight(Hit.Cell(statusPart, island.Chevron), false));

                            // The arrow's cell is the icons' square.
                            var arrow = layout.ItemRect(Hit.Cell(statusPart, island.Chevron));
                            var icon = layout.ItemRect(Hit.Cell(statusPart, 0));
                            Assert.Equal(icon.Width, arrow.Width, 0.5f);
                            Assert.Equal(icon.Height, arrow.Height, 0.5f);

                            // The arrow's flyout, as the dock opens it (Dock.ShowFlyout for IslandAction.Icons).
                            var action = ((IIsland)island).Click(island.Chevron);
                            Assert.True(action.Icons);
                            var items = Assert.IsAssignableFrom<IReadOnlyList<FlyoutItem>>(action.Items).ToList();
                            using var flyout = new Flyout(gfx, instance, 0)
                            {
                                StripIconSize = Fit.SystemIconFor(layout.CellSide, layout.Scale),
                                StripCell = layout.TrayOverflowCell,
                                GridColumns = Layout.TrayOverflowColumns,
                                FitsItsIcons = true,
                                GroupContent = true,
                                ContextClicks = true,
                            };
                            var work = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1032 * scale) };
                            var anchor = new POINT { x = work.right - (int)(200 * scale), y = work.bottom };
                            flyout.Show(gfx, look, scale, items, action.Layout, anchor, work, Edge.Bottom);
                            Assert.True(flyout.IsOpen);
                            WindowShots.Settle();
                            var file = $"status-arrow-flyout-{name}.png";
                            Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
                            // Two of the bar's squares side by side, and the panel no larger than they are.
                            Assert.Equal(Flyout.FittedSize(2, 1, layout.TrayOverflowCell, scale), flyout.PanelSize);
                            var first = Assert.NotNull(flyout.ItemOnScreen(IndicatorsIsland.BehindItem));
                            var second = Assert.NotNull(flyout.ItemOnScreen(IndicatorsIsland.BehindItem + 1));
                            Assert.Equal(first.top, second.top);
                            Assert.InRange((float)(second.left - first.left), layout.TrayOverflowCell - 1, layout.TrayOverflowCell + 1);
                            flyout.Hide();
                            WindowShots.Settle(50);
                        }
                        finally
                        {
                            foreach (var made in islands.Values)
                            {
                                made.Dispose();
                            }
                        }
                    }
                }
            }
            finally
            {
                IndicatorSources.Made = null;
            }
        });
    }
}
