// §602 (the person, 2026-11-09: "the apps that are not pinned and shown in the apps widget should have the same space as
// the other pins have to each other"): Start, two pins (the second running) and three open apps nothing pins (the first
// focused), then a widget — on a bar along the bottom (icons alone, and a window each with its title) and down the left
// side, spaced 6 and 12 DIP, dark and light, at 150 % — drawn by the real scene over a desktop and captured as the
// compositor draws it (WindowShots), to KARPO_PNG_DIR as appsgap-<bottom|bottom-labels|left>-<6|12>-<dark|light>.png.
// Every app button wears its own plate, so the open apps stand apart as the pins do.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void AppsGapSheets()
    {
        const uint dpi = 144;
        WindowShots.OnHiddenDesktop("appsgap", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque };
                foreach (var (where, edge, labels) in new[] { ("bottom", Edge.Bottom, false), ("bottom-labels", Edge.Bottom, true), ("left", Edge.Left, false) })
                {
                    foreach (var spacing in new[] { Config.DefaultSpacing, 12 })
                    {
                        var config = new Config { Height = 48, Style = BarStyle.Islands, Layouts = [new TaskbarLayout { Spacing = spacing }] };
                        var (along, across) = ((int)((labels ? 1100 : 640) * dpi / 96), (int)(dpi * 10));
                        var area = edge.IsVertical()
                            ? new RECT { left = 0, top = 0, right = across, bottom = along }
                            : new RECT { left = 0, top = 0, right = along, bottom = across };
                        List<BarItem> bar = [BarKind.Start.New(), Pin("p1"), Pin("p2"), BarKind.Apps.New(), BarKind.NightLight.New()];
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var content = AppGroupContent(islands);
                            if (labels)
                            {
                                content = new Content
                                {
                                    Islands = content.Islands,
                                    Tiles = content.Tiles,
                                    Labels = [null, "Inbox - Mail", "Report.docx - Word", "Notes", "Music"],
                                };
                            }
                            var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
                            var name = $"{where}-{spacing}-{(dark ? "dark" : "light")}";
                            OverADesktop(gfx, layout, look, content, null, $"appsgap-{name}.png", pose: scene =>
                            {
                                // Each pin and each open app its own plate at rest, over its own cell, the gap apart.
                                List<RectF> plates = [.. scene.RestPlatesOf(1), .. scene.RestPlatesOf(2), .. scene.RestPlatesOf(3)];
                                Assert.Equal([.. Enumerable.Range(0, 5).Select(layout.TileCell)], plates);
                                for (var at = 1; at < plates.Count; at++)
                                {
                                    var gap = edge.IsVertical() ? plates[at].Y - plates[at - 1].Bottom : plates[at].X - plates[at - 1].Right;
                                    Assert.True(MathF.Abs(gap - layout.WidgetGap) < 0.01f, $"{name}: buttons {at - 1} and {at} stand {gap} px apart, not {layout.WidgetGap}");
                                }
                            });
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
        });

        static BarItem Pin(string id)
        {
            var pin = BarKind.Pin.New();
            pin.Id = id;
            return pin;
        }
    }
}
