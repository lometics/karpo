// Another height laid out while the bar is still gliding (docs\widgets.md §383; the person, 2026-10-29:
// "when resizing the taskbar the layout gets completely messed up ... some elements changed its size but
// the bar did not really grow on the bottom"). The bar is laid out again at every step of a height drag and
// at a height set in its menu; between two steps anything else may lay it out too — an app opening, a
// window's title, a widget's width — and that one glides (167 ms). What stands on the screen once every
// step has landed must be the bar at its last height, whatever was gliding when the step came: the panel,
// the cards and what stands on them the size the layout says, flush with the edge, and the strip the bar
// holds and the window it stands in agreeing. Not reproduced (2026-10-29): both hold today; they pin it.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;
using Bar = Karpo.Tests.Present.RetainedBarTests.Bar;

namespace Karpo.Tests.Present;

public sealed unsafe class BarResizeTests
{
    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static Config Sized(int height, BarStyle style) => new() { Height = height, Style = style };

    /// <summary>The screen shown for <paramref name="ms"/>, pumped as the dock's loop is.</summary>
    private static void Shown(int ms)
    {
        var until = Environment.TickCount64 + ms;
        while (Environment.TickCount64 < until)
        {
            FrameFilm.Pump();
            Thread.Sleep(2);
        }
    }

    /// <summary>
    /// The heights a drag and the menu give, up and down, quick, at 100 % and 150 %, on a bottom bar and a
    /// top one, a bar and a dock: each a layout whose panel is its thickness, flush with its edge, whose
    /// strip held (<see cref="Layout.Reserve"/>) is that thickness, and which stands inside the window its
    /// edge keeps (<see cref="Layout.FrameFor"/>, which never moves with the height, §358).
    /// </summary>
    [UiFact]
    public void EveryHeightIsOneHeightForThePanelTheStripAndTheWindow()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var look = RetainedBarTests.Still;
            int[] steps = [48, 49, 52, 64, 72, 71, 56, 40, 32, 33, 44, 48];
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var frame = Layout.FrameFor(dpi, Display, edge, 1f);
                    foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                    {
                        foreach (var height in steps)
                        {
                            var config = Sized(height, style);
                            var layout = new Layout(gfx, dpi, Display, Display, edge, config, look, RetainedBarTests.Parts(), RetainedBarTests.Things(3), editing: false);
                            var said = $"{edge} {dpi} {style} {height}";
                            var thickness = Math.Max(Layout.ThicknessFor(dpi, height), 1f);
                            Assert.True(MathF.Abs(layout.Thickness - thickness) < 1f || layout.Thickness > thickness, $"{said}: thickness {layout.Thickness}, asked {thickness}");
                            Assert.Equal(layout.Thickness, layout.PanelRect.Height, 0.5f);
                            // The strip held reaches from the edge to the panel's inner side: a bar's is its
                            // thickness, flush with the edge; a dock's the room it floats over too.
                            var (inner, outer) = edge == Edge.Bottom
                                ? (Display.bottom - (layout.Y + layout.PanelRect.Y), Display.bottom - (layout.Y + layout.PanelRect.Bottom))
                                : (layout.Y + layout.PanelRect.Bottom - Display.top, layout.Y + layout.PanelRect.Y - Display.top);
                            Assert.Equal(layout.Reserve, inner, 0.5f);
                            if (style == BarStyle.Islands)
                            {
                                Assert.Equal(0f, outer, 0.5f);
                                Assert.Equal(layout.Thickness, layout.Reserve, 0.5f);
                            }
                            // Inside the window its edge keeps, whatever the height.
                            Assert.True(layout.X >= frame.left && layout.Y >= frame.top && layout.X + layout.Width <= frame.right && layout.Y + layout.Height <= frame.bottom,
                                $"{said}: the layout {layout.X},{layout.Y} {layout.Width}x{layout.Height} past its window {frame.left},{frame.top},{frame.right},{frame.bottom}");
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// The bar gliding — an app opened, the apps' card growing — and another height laid out before the
    /// glide is over, then a few more, quick, as a drag gives them: once everything has landed the screen
    /// shows the bar at its last height, the same as a bar laid out at that height from the start. On a
    /// bottom bar and a top one, islands and a dock.
    /// </summary>
    [UiFact]
    public void AHeightLaidOutMidGlideEndsAtThatHeight()
    {
        RetainedBarTests.OnHiddenDesktop("bar-resize-glide", (gfx, instance) =>
        {
            var moving = RetainedBarTests.Still with { Animations = true };
            var wrong = new List<string>();
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                {
                    foreach (var steps in new[] { new[] { 64 }, new[] { 56, 64, 72, 64 }, new[] { 40, 36, 32 } })
                    {
                        var name = $"{edge} {style} 48→{string.Join('→', steps)}";
                        FrameFilm.Shot shown;
                        RECT area;
                        using (var bar = new Bar(gfx, instance, edge))
                        {
                            area = bar.Frame;
                            bar.Lay(RetainedBarTests.Parts(), RetainedBarTests.Things(2), moving, Sized(48, style));
                            FrameFilm.Settle();
                            // An app opens: the apps' card grows and what stands after it moves, gliding.
                            bar.Lay(RetainedBarTests.Parts(), RetainedBarTests.Things(6), moving, Sized(48, style));
                            Assert.True(style != BarStyle.Islands || bar.Scene.Glides.Count > 0, "no glide");
                            Shown(40);
                            // The heights, quick, before the glide is over.
                            foreach (var height in steps)
                            {
                                bar.Lay(RetainedBarTests.Parts(), RetainedBarTests.Things(6), moving, Sized(height, style));
                                // A drag's tick (Dock.RESIZE_MS): the frames on the screen in between.
                                Shown(33);
                            }
                            Thread.Sleep(400);
                            FrameFilm.Settle();
                            shown = FrameFilm.Snap(area, [bar.Hwnd], 0, "after");
                        }
                        FrameFilm.Settle();
                        FrameFilm.Shot laid;
                        using (var fresh = new Bar(gfx, instance, edge))
                        {
                            fresh.Lay(RetainedBarTests.Parts(), RetainedBarTests.Things(6), RetainedBarTests.Still, Sized(steps[^1], style));
                            FrameFilm.Settle();
                            laid = FrameFilm.Snap(area, [fresh.Hwnd], 0, "laid");
                        }
                        FrameFilm.Settle();
                        var film = new FrameFilm.Film($"bar-resize-{edge}-{style}-{steps.Length}".ToLowerInvariant(), area) { Before = laid, End = shown };
                        film.Save();
                        var off = film.Differs(shown, laid);
                        if (off > 0.002)
                        {
                            wrong.Add($"{name}: {off:P2} of the bar off the bar laid at {steps[^1]}");
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, string.Join(Environment.NewLine, wrong));
        });
    }
}
