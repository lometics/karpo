// tray: the person's "make the tray icons a little closer together, they dont have to have the 1by1
// ratio anymore" (2026-10-22, docs\widgets.md §290; the squares were theirs of 2026-10-05). Three tray
// icons and the chevron, the second one under the pointer, drawn by the dock's real scene on a desktop
// of its own and captured as the compositor draws it (WindowShots), to KARPO_PNG_DIR:
//   tray-cells-<style>-<height>-<edge>-<look>-<scale>.png
// in every look (connected: the hover's plate; islands: a card per icon), on a 32-, 48- and 64-DIP
// bar, along the bottom and down the left, dark and light, at 100 % and 150 %. Every cell is
// measured by the real Layout: the icon and its padding along the bar, the bar's thickness across
// it, and its picture in the middle of it.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void TrayCellSheets()
    {
        WindowShots.OnHiddenDesktop("tray-cells", (gfx, instance) =>
        {
            foreach (var style in Enum.GetValues<BarStyle>())
            {
                foreach (var height in new[] { 32, 48, 64 })
                {
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                    {
                        foreach (var dark in new[] { true, false })
                        {
                            foreach (var dpi in new uint[] { 96, 144 })
                            {
                                var look = AddListPictures.LookFor(dark) with { Animations = false };
                                var config = new Config { Height = height, Style = style };
                                var room = (int)(320 * dpi / 96);
                                var deep = (int)(dpi * 10);
                                var area = edge.IsVertical()
                                    ? new RECT { left = 0, top = 0, right = deep, bottom = room }
                                    : new RECT { left = 0, top = 0, right = room, bottom = deep };
                                var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                                var icons = DraggedTrayIcons(3, px);
                                var content = new Content { Tray = icons, TrayOverflow = true };
                                var layout = new Layout(gfx, dpi, area, area, edge, config, look, TrayBar(), content);
                                var name = $"{style.ToString().ToLowerInvariant()}-{height}-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}";

                                var cells = new List<RectF> { layout.ItemRect(Hit.TrayOverflow) };
                                for (var index = 0; index < icons.Count; index++)
                                {
                                    cells.Add(layout.ItemRect(Hit.Tray(index)));
                                }
                                foreach (var cell in cells)
                                {
                                    Assert.False(cell.IsEmpty, name);
                                    Assert.Equal(layout.TrayStep, layout.Vertical ? cell.Height : cell.Width);
                                    Assert.Equal(layout.TrayCell, layout.Vertical ? cell.Width : cell.Height);
                                    Assert.True(layout.TrayStep < layout.TrayCell, name);
                                }
                                for (var index = 0; index < icons.Count; index++)
                                {
                                    var cell = cells[index + 1];
                                    var box = layout.TrayIconRect(index);
                                    Assert.InRange((box.X - cell.X) - (cell.Right - box.Right), -1f, 1f);
                                    Assert.InRange((box.Y - cell.Y) - (cell.Bottom - box.Bottom), -1f, 1f);
                                }

                                TrayShot(gfx, layout, content, look, scene => scene.Highlight(Hit.Tray(1), false), $"tray-cells-{name}.png");
                            }
                        }
                    }
                }
            }
        });
    }
}
