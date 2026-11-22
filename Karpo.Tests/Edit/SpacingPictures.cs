// §533, §589: a layout's Spacing — 0, 6 (the default), 12 and 20 DIP — on a thin (32 DIP) and the default (48 DIP) bar,
// drawn by the real scene over a desktop and captured as the compositor draws it (WindowShots), to KARPO_PNG_DIR as
// bar-spacing-<0|6|12|20>-<32|48>-<dark|light>.png at 150 %: Start, two pins and three open apps at the start, three
// widgets in the middle and one at the end, each on its rest plate. 0 is flush at both heights, the others keep their
// room at both (the 32-DIP bar was flush by itself before §533); at 20 the sections stand 20 apart too.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void BarSpacingSheets()
    {
        const uint dpi = 144;
        WindowShots.OnHiddenDesktop("bar-spacing", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque };
                foreach (var height in new[] { 32, 48 })
                {
                    foreach (var spacing in new[] { 0, Config.DefaultSpacing, 12, 20 })
                    {
                        var config = new Config { Height = height, Style = BarStyle.Islands, Layouts = [new TaskbarLayout { Spacing = spacing }] };
                        var area = new RECT { left = 0, top = 0, right = (int)(820 * dpi / 96), bottom = (int)(dpi * 10) };
                        List<BarItem> bar = [BarKind.Start.New(), Pin("p1"), Pin("p2"), BarKind.Apps.New(),
                            BarKind.WidgetsBoard.New(), BarKind.TouchKeyboard.New(), BarKind.NightLight.New(), BarKind.EnergySaver.New()];
                        foreach (var item in bar)
                        {
                            item.Section = item.Kind switch
                            {
                                BarKind.WidgetsBoard or BarKind.TouchKeyboard or BarKind.NightLight => BarSection.Middle,
                                BarKind.EnergySaver => BarSection.End,
                                _ => BarSection.Start,
                            };
                        }
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var content = AppGroupContent(islands);
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                            Assert.Equal(spacing == 0, layout.Flush);
                            Assert.Equal(spacing == 0, layout.WidgetGap == 0f);
                            var name = $"{spacing}-{height}-{(dark ? "dark" : "light")}";
                            OverADesktop(gfx, layout, look, content, null, $"bar-spacing-{name}.png");
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
