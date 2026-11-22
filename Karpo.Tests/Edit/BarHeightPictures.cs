// The bar's real scene at the heights a bar has (docs\widgets.md §56), shown on a desktop of its
// own and captured as the compositor draws it (WindowShots), to KARPO_PNG_DIR as
// bar-height-<height>-<look>-<scale>[-top].png: the default bar with a run of the widgets that have the
// most to say beside its own — the readings, the drive, the timer — so the cards' room off the
// bar's edges and ends, the groups and what stands in them are seen at 28, 32, 36, 40, 48, 64 and 80
// DIPs along the bottom, and at 32 and 40 along the top.
// Every card is checked against the room it keeps (Layout.CardInsetPx) on the real Layout.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void BarHeightSheets()
    {
        WindowShots.OnHiddenDesktop("bar-height", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var (height, edge) in new[] { 28, 32, 36, 40, 48, 64, 80 }.Select(height => (height, Edge.Bottom)).Append((32, Edge.Top)).Append((40, Edge.Top)))
                    {
                        if (!dark && height is 36 or 64)
                        {
                            continue;
                        }
                        var name = $"{height}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}{(edge == Edge.Top ? "-top" : "")}";
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        var config = new Config { Height = height };
                        var area = new RECT { left = 0, top = 0, right = (int)(2400 * dpi / 96), bottom = (int)(dpi * 10) };
                        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
                        // A run of widgets at the start, after what the bar starts with.
                        var start = bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start);
                        foreach (var (kind, at) in new[] { BarKind.WidgetsBoard, BarKind.TouchKeyboard, BarKind.NightLight, BarKind.EnergySaver }.Select((kind, at) => (kind, at)))
                        {
                            var item = kind.New();
                            item.Id = $"extra-{kind}".ToLowerInvariant();
                            item.Section = BarSection.Start;
                            bar.Insert(Math.Max(0, start) + at, item);
                        }
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var content = new Content { Islands = islands };
                            var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
                            var inset = Layout.CardInsetPx(dpi, config);
                            var panel = layout.PanelRect;
                            var shown = 0;
                            for (var part = 0; part < layout.Parts.Count; part++)
                            {
                                if (!layout.Specs[part].Kind.IsIsland() && layout.Specs[part].Kind is BarKind.Apps or BarKind.Pin)
                                {
                                    continue;
                                }
                                var block = layout.BlockRect(part);
                                if (block.IsEmpty)
                                {
                                    continue;
                                }
                                shown++;
                                Assert.True(
                                    MathF.Abs(block.Y - panel.Y - inset) <= 0.5f && MathF.Abs(panel.Bottom - block.Bottom - inset) <= 0.5f,
                                    $"{layout.Specs[part].Kind} at {name}: its card is {block.Y - panel.Y} px from the bar's top and {panel.Bottom - block.Bottom} from its bottom, and the room is {inset}");
                            }
                            Assert.True(shown > 8, $"only {shown} parts were shown at {name}");
                            var first = Enumerable.Range(0, layout.Parts.Count).Where(part => !layout.BlockRect(part).IsEmpty).Min(part => layout.BlockRect(part).X);
                            Assert.True(first - panel.X >= inset - 0.5f, $"the first card is {first - panel.X} px from the bar's start, and the room is {inset}");
                            ShootWith(gfx, layout, content, look, $"bar-height-{name}.png", default);
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
    }
}
