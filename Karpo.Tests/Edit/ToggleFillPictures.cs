// §648 (the person, 2026-11-09: "the toggle button on the taskbar like the microphone on / off have a little space between
// the area that is filled and the outer edge, this should not be."): a switch on the bar that is on fills its whole button —
// its card, its plate at rest, its highlight — with the accent, edge to edge in the plate's corners, at every bar size, on
// every edge, on a bar of cards, a flush one and a floating dock's panel, dark, light and high contrast. Every switch is a
// made-up one (SampleSwitch): no test reads or flips this PC's microphone, Wi-Fi or anything else.
//
// Pictures (KARPO_PNG_DIR), drawn by the real scene over a desktop and captured as the compositor draws it (WindowShots):
// togglefill-<bottom|left>-<32|48>-<dark|light>.png — Start, then the microphone, Wi-Fi, Bluetooth (off), night light, keep
// awake and airplane mode with its name, all on but Bluetooth; and togglefill-hover-48-<dark|light>.png, the pointer over
// the microphone.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Windows.UI;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Start, then six switches, airplane mode with its name beside its glyph.</summary>
    private static List<BarItem> ToggleBar()
    {
        var airplane = BarKind.AirplaneMode.New();
        airplane.Switch = new SwitchOptions { ShowLabel = true };
        return [BarKind.Start.New(), BarKind.MicrophoneSwitch.New(), BarKind.WifiSwitch.New(), BarKind.BluetoothSwitch.New(),
            BarKind.NightLight.New(), BarKind.KeepAwake.New(), airplane];
    }

    /// <summary>The bar's switches made up: every one on but Bluetooth, which is off beside them.</summary>
    private static void MadeUpSwitches() =>
        QuickSwitches.Source = which => new SampleSwitch(which.ToString(), on: which != QuickSwitch.BluetoothRadio, switches: true);

    private static Look HighContrast(bool dark) => AddListPictures.LookFor(dark) with
    {
        HighContrast = true,
        HcWindow = Color.FromArgb(255, 0, 0, 0),
        HcText = Color.FromArgb(255, 255, 255, 255),
        HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText = Color.FromArgb(255, 0, 0, 0),
    };

    /// <summary>
    /// A switch that is on is the accent to its button's edges: its cell is its card and its plate (Layout.Plates), the style it
    /// is drawn with carries the plate's corners (Layout.PlateRadius), and the accent reaches the middle of every edge of the
    /// cell, its corners rounded as the plate's — none on a flush bar. One that is off leaves its edges bare. At 32, 48 and 72
    /// DIP, along the bottom and down the left, at 100 and 150 %, on a bar of cards, a flush one and a floating dock's.
    /// </summary>
    [UiFact]
    public void ASwitchOnFillsItsPlateEdgeToEdge()
    {
        Offscreen.OnStaThread(gfx =>
        {
            MadeUpSwitches();
            try
            {
                foreach (var (name, barStyle, spacing) in new[] { ("cards", BarStyle.Islands, Config.DefaultSpacing), ("flush", BarStyle.Islands, 0), ("floating", BarStyle.Floating, Config.DefaultSpacing) })
                {
                    foreach (var height in new[] { 32, 48, 72 })
                    {
                        foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                        {
                            foreach (var dpi in new uint[] { 96, 144 })
                            {
                                foreach (var look in new[] { AddListPictures.LookFor(true), AddListPictures.LookFor(false), HighContrast(true) })
                                {
                                    var config = new Config { Height = height, Style = barStyle, Layouts = [new TaskbarLayout { Spacing = spacing }] };
                                    var (specs, islands) = Made(gfx, config, ToggleBar());
                                    try
                                    {
                                        var area = edge.IsVertical()
                                            ? new RECT { left = 0, top = 0, right = (int)(dpi * 2), bottom = (int)(1200 * dpi / 96) }
                                            : new RECT { left = 0, top = 0, right = (int)(1200 * dpi / 96), bottom = (int)(dpi * 2) };
                                        var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, new Content { Islands = islands });
                                        var where = $"{name}, {height} DIP, {edge}, {dpi} DPI, {(look.HighContrast ? "contrast" : look.Dark ? "dark" : "light")}";
                                        Assert.Equal(barStyle == BarStyle.Islands && spacing > 0 ? layout.CardRadius : barStyle == BarStyle.Islands ? 0f : layout.SoftPx(4), layout.PlateRadius);
                                        var checkedSwitches = 0;
                                        for (var part = 0; part < layout.Parts.Count; part++)
                                        {
                                            if (!islands.TryGetValue(layout.Parts[part].Key, out var made) || made is not SwitchIsland island)
                                            {
                                                continue;
                                            }
                                            var rect = layout.ItemRect(Hit.Cell(part, 0));
                                            Assert.False(rect.IsEmpty, where);
                                            if (layout.Cards)
                                            {
                                                Assert.Equal(rect, Assert.Single(layout.Plates(part)));
                                            }
                                            var style = layout.StyleFor(part, look);
                                            Assert.Equal(layout.PlateRadius, style.PlateRadius);
                                            var (width, height2) = island.CellSize(0, style);
                                            Assert.Equal((rect.Width, rect.Height), (width, height2));
                                            var on = island.On == true;
                                            Offscreen.Bitmap(width, height2, dc => island.PaintBar(dc, 0, style, width, height2), bitmap =>
                                            {
                                                var (w, h) = ((uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height2));
                                                var (midX, midY) = ((int)(w / 2), (int)(h / 2));
                                                var edges = new[] { (0, midY), ((int)w - 1, midY), (midX, 0), (midX, (int)h - 1) };
                                                foreach (var (x, y) in edges)
                                                {
                                                    var alpha = Offscreen.AlphaAt(bitmap, w, h, x, y);
                                                    Assert.True(on ? alpha == 255 : alpha == 0, $"{where}: {island.Info.Id} {(on ? "on" : "off")} has {alpha} at its edge ({x}, {y}) of {w}×{h}");
                                                }
                                                if (on)
                                                {
                                                    var corner = Offscreen.AlphaAt(bitmap, w, h, 0, 0);
                                                    Assert.True(style.PlateRadius >= 2 ? corner < 255 : corner == 255, $"{where}: {island.Info.Id}'s corner is {corner} for a plate's corner of {style.PlateRadius}");
                                                }
                                            });
                                            checkedSwitches++;
                                        }
                                        Assert.Equal(6, checkedSwitches);
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
                    }
                }
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }

    [UiFact]
    public void ToggleFillSheets()
    {
        const uint dpi = 144;
        WindowShots.OnHiddenDesktop("togglefill", (gfx, _) =>
        {
            MadeUpSwitches();
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque };
                    foreach (var (where, edge) in new[] { ("bottom", Edge.Bottom), ("left", Edge.Left) })
                    {
                        foreach (var height in new[] { 32, 48 })
                        {
                            var config = new Config { Height = height, Style = BarStyle.Islands };
                            var (along, across) = ((int)(720 * dpi / 96), (int)(dpi * 10));
                            var area = edge.IsVertical()
                                ? new RECT { left = 0, top = 0, right = across, bottom = along }
                                : new RECT { left = 0, top = 0, right = along, bottom = across };
                            var (specs, islands) = Made(gfx, config, ToggleBar());
                            try
                            {
                                var content = new Content { Islands = islands };
                                var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
                                var tone = dark ? "dark" : "light";
                                OverADesktop(gfx, layout, look, content, null, $"togglefill-{where}-{height}-{tone}.png");
                                if (edge == Edge.Bottom && height == 48)
                                {
                                    OverADesktop(gfx, layout, look, content, null, $"togglefill-hover-{height}-{tone}.png", pose: scene => scene.Highlight(Hit.Cell(1, 0), pressed: false));
                                }
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
            }
            finally
            {
                QuickSwitches.Source = null;
            }
        });
    }
}
