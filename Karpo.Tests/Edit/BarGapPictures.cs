// §501, §515: a widget's gap between every two widgets of a section (the person, 2026-11-04: "give the individual taskbar
// widgets some space as well between them"). Drawn by the real scene over a desktop and captured as the compositor draws
// it (WindowShots), to KARPO_PNG_DIR as bar-gap-<dark|light>-<100|150>-<plain|dots>-<rest|hover>.png: Start, two pins and
// three open apps at the start, three widgets in the middle and one widget at the end — on cards of a colour of their
// own, bare and with Dots on the cards (§515: a sheet of its own each), at rest and with the middle's middle widget under
// the pointer (its plate apart from its neighbours').
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
    public void BarGapSheets()
    {
        WindowShots.OnHiddenDesktop("bar-gap", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var dotted in new[] { false, true })
                    {
                        var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}-{(dotted ? "dots" : "plain")}";
                        var plain = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque };
                        var look = plain with { Colors = plain.Colors with { Taskbar = A(dark ? "#FF101418" : "#FFE4E8EE"), Card = A(dark ? "#FF2B4566" : "#FFBCD0EA") } };
                        if (dotted)
                        {
                            look = look with { Colors = look.Colors with { CardPattern = new Patterned(Pattern.Dots, dark ? A("#40FFFFFF") : A("#40000000")) } };
                        }
                        var config = new Config { Height = 48, Style = BarStyle.Islands };
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
                            var middle = specs.FindIndex(spec => spec.Kind == BarKind.TouchKeyboard);
                            Assert.True(layout.Placed(middle));
                            Assert.True(layout.WidgetGap > 0f);
                            OverADesktop(gfx, layout, look, content, null, $"bar-gap-{name}-rest.png");
                            var at = layout.BlockRect(middle).Center;
                            var hit = layout.HitTest(at.X, at.Y) ?? throw new Xunit.Sdk.XunitException($"{name}: nothing under the middle's middle widget");
                            OverADesktop(gfx, layout, look, content, null, $"bar-gap-{name}-hover.png", pose: scene => scene.Highlight(hit, false));
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
