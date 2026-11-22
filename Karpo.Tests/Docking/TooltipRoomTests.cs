// The bar's tooltips stand whole in the room the window keeps for them (Layout.TooltipMostHeight;
// 2026-10-11, the person: "when hovering over quick settings the tooltip is cutoff at the top").
// Quick settings' tooltip was its name and each face's words under it — six lines with the network's
// two — and the room above a horizontal bar was three lines: the tooltip ran past the window and was
// cut off. Now the room is six lines, and a longer one is cut to it (BarTooltips.Fitted), on every
// edge, at 100 and 150 %, and for a thin bar and a thick one. A folder shows none any more (§367), but
// a tray icon's tip, the app's own, may run as long.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed class TooltipRoomTests
{
    // The scene's tooltip (Scene.ShowTip): 12 DIP text, 6 DIP above and below it.
    private const float TextDip = 12;
    private const float PadYDip = 6;

    /// <summary>What Quick settings says on a laptop with Wi-Fi: its name, the network's two lines, the volume's and the battery's.</summary>
    private const string QuickSettings = "Quick settings\nHome-5G\nInternet access\nSpeakers (Realtek(R) Audio): 45%\n76% remaining\nAbout 3 h 17 min remaining"; // not-ui: a sample

    /// <summary>The cut: whole where it fits, else its first lines as many as fit, the last ending in "…"; one line always stays.</summary>
    [Fact]
    public void ATooltipTallerThanTheRoomKeepsItsFirstLines()
    {
        static float Height(string text) => text.Split('\n').Length * 10f;
        var ten = string.Join('\n', Enumerable.Range(1, 10).Select(line => $"line {line}"));

        Assert.Equal(ten, BarTooltips.Fitted(ten, 100, Height));
        Assert.Equal("line 1\nline 2\nline 3\nline 4…", BarTooltips.Fitted(ten, 45, Height));
        Assert.Equal("line 1", BarTooltips.Fitted(ten, 5, Height));
        Assert.Equal("one", BarTooltips.Fitted("one", 5, Height));
    }

    /// <summary>
    /// On every edge, at 100 and 150 %, on a 35-DIP bar and a 48-DIP one: Quick settings' six lines
    /// stand whole, and any tooltip — however many lines — stands inside the window and off the bar.
    /// </summary>
    [UiFact]
    public void QuickSettingsTipStandsWholeInsideTheWindowOnEveryEdge()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var twenty = string.Join('\n', Enumerable.Range(1, 20).Select(line => $"Line {line} of a long tooltip"));
            foreach (var dpi in new uint[] { 96, 144 })
            {
                foreach (var height in new[] { 35, 48 })
                {
                    foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
                    {
                        var config = new Config { Height = height };
                        var area = new RECT { left = 0, top = 0, right = 2560 * (int)dpi / 96, bottom = 1440 * (int)dpi / 96 };
                        var layout = Layout.ForTests(dpi, area, edge, config, [new(Config.IslandKey(null, "folder"), BarKind.EnergySaver)], Layout.CellPx(dpi, config));
                        var scale = dpi / 96f;
                        var px = TextDip * scale;
                        var padY = MathF.Round(PadYDip * scale);
                        var at = $"{edge} bar {height} DIP at {dpi} dpi";
                        foreach (var text in new[] { QuickSettings, twenty })
                        {
                            var shown = BarTooltips.Fitted(text, layout.TooltipMostHeight - 2 * padY,
                                lines => PopupPaint.Measure(gfx, lines, px, Weight.Regular, layout.TooltipMaxWidth).Y);
                            if (text == QuickSettings)
                            {
                                Assert.True(shown == text, $"{at}: Quick settings' tooltip was cut to \"{shown}\"");
                            }
                            var measured = PopupPaint.Measure(gfx, shown, px, Weight.Regular, layout.TooltipMaxWidth);
                            var size = new Vector2(measured.X + 18 * scale, measured.Y + 2 * padY);
                            // Over the bar's first item and over its middle.
                            var panel = layout.PanelRect;
                            foreach (var item in new[] { new RectF(panel.X, panel.Y, 4, 4), panel })
                            {
                                var tip = layout.TooltipAnchor(item, size);
                                Assert.True(tip.X >= 0 && tip.Y >= 0 && tip.X + tip.Width <= layout.Width + 0.5f && tip.Y + tip.Height <= layout.Height + 0.5f,
                                    $"{at}: a {shown.Split('\n').Length}-line tooltip at {tip} runs past the window {layout.Width} × {layout.Height}");
                                if (!edge.IsVertical())
                                {
                                    var clear = edge == Edge.Bottom ? tip.Y + tip.Height <= panel.Y + 0.5f : tip.Y >= panel.Y + panel.Height - 0.5f;
                                    Assert.True(clear, $"{at}: a {shown.Split('\n').Length}-line tooltip at {tip} covers the bar at {panel}");
                                }
                            }
                        }
                    }
                }
            }
        });
    }
}
