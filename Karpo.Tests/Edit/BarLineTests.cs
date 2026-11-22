// No line on a taskbar.
//
// The user: "between the individual widgets in the taskbar, there should not be a divider placed
// automatically. just when i place it there". The connected look used to draw a line of its own
// between every pair of neighbouring widgets, between the tray's icons and between the running
// apps' buttons. A line then appeared only where a separator stood; since §505 (the person, 2026-11-05: "the spacers
// should always just be empty space and not a line or dot or anything") none at all, and since §511 there is no
// separator on a bar.
//
// Held on the dock's real tree, with SceneTreeTests' harness:
//
// - The scene of a bar at rest draws no solid rectangle shaped like a line across the bar, in
//   whatever colour — at 28, 36, 48 and 72 DIP, in both looks, dark and
//   light, on all four edges, at 100 % and 150 %.
// - The tree of a connected bar painted to a sheet: where the old dividers stood — two widgets
//   meeting, two tray icons, two app buttons — the pixels are the band's and nothing else. The sheets and their numbers go to
//   KARPO_PNG_DIR (connected-bar-<height>-<look>-<edge>.png and .txt) before anything is asserted.
//   The numbers list every part's place, which is how "nothing moved" is checked: the dividers
//   never had room of their own.

using System.Globalization;
using System.Numerics;
using System.Text;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The running apps' buttons after the two pins (Layout.ForTests counts pins among the tiles).</summary>
    private const int Running = 2;

    /// <summary>The tray's icons, after its overflow chevron.</summary>
    private const int TrayIcons = 3;

    /// <summary>
    /// A bar with every place an automatic divider used to stand — two pins and the running apps
    /// beside them, widgets side by side, the tray's icons — two sections apart.
    /// </summary>
    private static List<PartSpec> Crowded() =>
    [
        Spec("p1", BarKind.Pin),
        Spec("p2", BarKind.Pin),
        Spec("apps", BarKind.Apps),
        Spec("w", BarKind.WidgetsBoard),
        Spec("b", BarKind.EnergySaver),
        Spec("c", BarKind.NightLight) with { Section = BarSection.End },
        Spec("tray", BarKind.Tray) with { Section = BarSection.End },
        Spec("wifi", BarKind.TouchKeyboard) with { Section = BarSection.End },
    ];

    private static Layout CrowdedBar(uint dpi, int height, BarStyle style, Edge edge)
    {
        var config = new Config { Height = height, Style = style };
        var area = new RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
        return Layout.ForTests(dpi, area, edge, config, Crowded(), Layout.CellPx(dpi, config), tray: TrayIcons, trayOverflow: true, tiles: 2 + Running);
    }

    /// <summary>An opaque bar, so a sheet of it is the same picture on every machine.</summary>
    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        Animations: false,
        Material: Material.Opaque);

    /// <summary>One bar to build, and what to call it in a failure.</summary>
    private readonly record struct Built(string Where, Layout Layout, Look Look);

    /// <summary>
    /// Builds each bar's real scene at rest, one after another on one thread and one window, and
    /// hands <paramref name="body"/> the bar and the root of the tree it built.
    /// </summary>
    private static void OnScenes(IEnumerable<Built> bars, Action<Built, Visual> body)
    {
        OnItsOwnThread(gfx =>
        {
            var window = MakeWindow();
            DesktopWindowTarget? target = null;
            try
            {
                target = Target(gfx, window);
                foreach (var bar in bars)
                {
                    using var scene = Scenes.Build(gfx, target, bar.Layout, new Content(), bar.Look, new Icons(), shown: true);
                    body(bar, target.Root);
                }
            }
            finally
            {
                _ = DestroyWindow(window);
            }
        });
    }

    /// <summary>
    /// Every visual under <paramref name="visual"/> that is shown, with where it stands in the window
    /// and how big it is — its own size and its share of its parent's, as the compositor sizes it.
    /// </summary>
    private static IEnumerable<(Visual Visual, Vector2 At, Vector2 Size)> Shown(Visual visual, Vector2 at, Vector2 parent = default)
    {
        if (!visual.IsVisible || visual.Opacity <= 0f)
        {
            yield break;
        }
        var here = at + new Vector2(visual.Offset.X, visual.Offset.Y);
        var size = visual.Size + (visual.RelativeSizeAdjustment * parent);
        yield return (visual, here, size);
        if (visual is ContainerVisual container)
        {
            foreach (var child in container.Children)
            {
                foreach (var found in Shown(child, here, size))
                {
                    yield return found;
                }
            }
        }
    }

    private static string Show(RectF rect) =>
        string.Create(CultureInfo.InvariantCulture, $"({rect.X:0.#}, {rect.Y:0.#}, {rect.Width:0.#} × {rect.Height:0.#})");

    // --- On the tree ------------------------------------------------------------------------------

    /// <summary>
    /// Whether <paramref name="rect"/> is shaped like a line standing across the bar: longer across
    /// it than along it, and no thicker along it than two device hairlines, the thickest line a bar ever drew. Asked by
    /// shape and not by colour, so a line drawn in any colour at all is found.
    /// </summary>
    private static bool LineAcross(Layout layout, RectF rect)
    {
        var (along, across) = layout.Vertical ? (rect.Height, rect.Width) : (rect.Width, rect.Height);
        return along > 0f && along <= 2f * MathF.Max(1f, MathF.Floor(layout.Scale)) && across > along;
    }

    /// <summary>
    /// §505, §511: no bar at rest draws a line: no solid rectangle the scene draws is shaped like a line across
    /// the bar, in whatever colour.
    /// </summary>
    [UiFact]
    public void NoBarDrawsALineAtRest()
    {
        var bars = new List<Built>();
        foreach (var height in new[] { 28, 36, 48, 72 })
        {
            foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
                    {
                        foreach (var dpi in (uint[])[96, 144])
                        {
                            bars.Add(new Built(
                                $"{style} {height} DIP {edge} {(dark ? "dark" : "light")} {dpi} DPI",
                                CrowdedBar(dpi, height, style, edge),
                                LookFor(dark)));
                        }
                    }
                }
            }
        }
        var failures = new List<string>();
        var seen = 0;
        OnScenes(bars, (bar, root) =>
        {
            var layout = bar.Layout;
            var lines = Shown(root, Vector2.Zero)
                .Where(item => item.Visual is SpriteVisual { Brush: CompositionColorBrush { Color.A: > 0 } })
                .Select(item => (Rect: new RectF(item.At.X, item.At.Y, item.Size.X, item.Size.Y), Colour: ((CompositionColorBrush)((SpriteVisual)item.Visual).Brush).Color))
                .Where(item => LineAcross(layout, item.Rect))
                .ToList();
            foreach (var (line, colour) in lines)
            {
                failures.Add($"{bar.Where}: a line at {Show(line)} in #{colour.A:X2}{colour.R:X2}{colour.G:X2}{colour.B:X2}");
            }
            seen++;
        });
        Assert.Equal(bars.Count, seen);
        Assert.True(failures.Count == 0, $"{failures.Count} lines are wrong:\n{string.Join("\n", failures.Take(60))}");
    }

    // --- In the pixels ----------------------------------------------------------------------------

    /// <summary>
    /// Where the old automatic dividers stood on <paramref name="layout"/>, along the bar in window
    /// px: where two neighbouring parts of one section met, and where
    /// two of the tray's squares or two running apps' buttons met.
    /// </summary>
    private static List<(float At, string Between)> OldDividers(Layout layout)
    {
        var seams = new List<(float, string)>();
        var parts = Enumerable.Range(0, layout.Parts.Count)
            .Where(index => layout.Parts[index].Width > 0f && !layout.BlockRect(index).IsEmpty)
            .OrderBy(index => Along(layout, layout.BlockRect(index)))
            .ToList();
        for (var at = 1; at < parts.Count; at++)
        {
            var (before, after) = (layout.Parts[parts[at - 1]], layout.Parts[parts[at]]);
            if (layout.Specs[parts[at - 1]].Section != layout.Specs[parts[at]].Section)
            {
                continue;
            }
            seams.Add((Along(layout, layout.BlockRect(parts[at])), $"{before.Kind} | {after.Kind}"));
        }
        var tray = new List<RectF> { layout.ItemRect(Hit.TrayOverflow) };
        for (var icon = 0; icon < TrayIcons; icon++)
        {
            tray.Add(layout.ItemRect(Hit.Tray(icon)));
        }
        var apps = new List<RectF>();
        for (var tile = 2; tile < 2 + Running; tile++)
        {
            apps.Add(layout.TileCell(tile));
        }
        foreach (var (run, name) in new[] { (tray, "tray icon"), (apps, "app button") })
        {
            for (var at = 1; at < run.Count; at++)
            {
                if (!run[at].IsEmpty && !run[at - 1].IsEmpty)
                {
                    seams.Add((Along(layout, run[at]), $"{name} | {name}"));
                }
            }
        }
        return seams;
    }

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    /// <summary>
    /// A connected bar's tree painted to a sheet, dark and light, at each height, along the top and
    /// down the left: the places the old dividers stood are the band and nothing else.
    /// </summary>
    [UiFact]
    public void ConnectedBarSheets()
    {
        var bars = new List<Built>();
        foreach (var height in new[] { 28, 36, 48, 72 })
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var edge in new[] { Edge.Top, Edge.Left })
                {
                    bars.Add(new Built(
                        $"dock-bar-{height}-{(dark ? "dark" : "light")}-{(edge == Edge.Top ? "top" : "left")}",
                        CrowdedBar(96, height, BarStyle.Floating, edge),
                        LookFor(dark)));
                }
            }
        }
        var failures = new List<string>();
        OnScenes(bars, (bar, root) =>
        {
            var layout = bar.Layout;
            var panel = layout.PanelRect;
            var (width, height) = ((int)MathF.Ceiling(panel.Width), (int)MathF.Ceiling(panel.Height));
            uint[] pixels = [];
            Offscreen.Bitmap(width, height, dc => Paint(dc, root, new Vector2(-panel.X, -panel.Y)), bitmap =>
            {
                pixels = Read(bitmap, width, height);
                Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, bar.Where + ".png"));
            });
            var notes = new StringBuilder();
            notes.AppendLine(CultureInfo.InvariantCulture, $"{bar.Where}: {layout.Parts.Count} parts; px from the window's corner.");
            for (var index = 0; index < layout.Parts.Count; index++)
            {
                notes.AppendLine(CultureInfo.InvariantCulture, $"  part {layout.Parts[index].Kind,-10} at {Show(layout.BlockRect(index))}");
            }

            // Across the bar, the middle half of the well: where the old line was drawn (Layout.Across
            // centres a rectangle across the bar, which is how it was placed).
            var probe = layout.Across(0f, 1f, 2f);
            var middle = layout.Vertical ? probe.X + (probe.Width / 2f) - panel.X : probe.Y + (probe.Height / 2f) - panel.Y;
            var reach = MathF.Round(layout.Well * 0.25f);
            uint Pixel(float along, float across)
            {
                var (x, y) = layout.Vertical
                    ? ((int)MathF.Floor(across), (int)MathF.Floor(along - panel.Y))
                    : ((int)MathF.Floor(along - panel.X), (int)MathF.Floor(across));
                return x >= 0 && y >= 0 && x < width && y < height ? pixels[(y * width) + x] : 0u;
            }
            foreach (var (at, between) in OldDividers(layout))
            {
                var wrong = 0;
                for (var across = middle - reach; across <= middle + reach; across += 1f)
                {
                    var band = Pixel(at + 4f, across);
                    foreach (var column in new[] { at - 1f, at })
                    {
                        if (!Near(Pixel(column, across), band))
                        {
                            wrong++;
                        }
                    }
                }
                notes.AppendLine(CultureInfo.InvariantCulture, $"  old divider {between,-26} at {at:0.#}: {(wrong == 0 ? "band" : $"{wrong} px not the band")}");
                if (wrong > 0)
                {
                    failures.Add($"{bar.Where}: where a {between} meet (at {at:0.#}), {wrong} px are not the band");
                }
            }

            File.WriteAllText(Path.Combine(Offscreen.Pictures, bar.Where + ".txt"), notes.ToString());
        });
        Assert.True(failures.Count == 0, $"{failures.Count} places are wrong:\n{string.Join("\n", failures.Take(60))}");
    }

    /// <summary>
    /// Paints what the scene built — its solid rectangles and its rounded shapes, fill and outline —
    /// in the order it stacked them. The text and the pictures are left out: a line is either of the
    /// first two, and they are all this sheet is about.
    /// </summary>
    private static void Paint(ID2D1DeviceContext* dc, Visual root, Vector2 shift)
    {
        foreach (var (visual, at, size) in Shown(root, shift))
        {
            if (visual is SpriteVisual { Brush: CompositionColorBrush fill })
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(at.X, at.Y, size.X, size.Y), 0f, Ink(fill.Color));
            }
            if (visual is not ShapeVisual shapes)
            {
                continue;
            }
            foreach (var shape in shapes.Shapes)
            {
                if (shape is not CompositionSpriteShape { Geometry: CompositionRoundedRectangleGeometry geometry } sprite)
                {
                    continue;
                }
                var box = new D2D1_ROUNDED_RECT
                {
                    rect = PopupPaint.Rect(at.X + sprite.Offset.X + geometry.Offset.X, at.Y + sprite.Offset.Y + geometry.Offset.Y, geometry.Size.X, geometry.Size.Y),
                    radiusX = geometry.CornerRadius.X,
                    radiusY = geometry.CornerRadius.Y,
                };
                if (sprite.FillBrush is CompositionColorBrush body)
                {
                    var brush = Gfx.Brush(dc, Ink(body.Color));
                    dc->FillRoundedRectangle(&box, (ID2D1Brush*)brush);
                    brush->Release();
                }
                if (sprite.StrokeBrush is CompositionColorBrush ring && sprite.StrokeThickness > 0f)
                {
                    // Centred on the outline, as the compositor strokes it.
                    var brush = Gfx.Brush(dc, Ink(ring.Color));
                    dc->DrawRoundedRectangle(&box, (ID2D1Brush*)brush, sprite.StrokeThickness, null);
                    brush->Release();
                }
            }
        }

        static DXGI_RGBA Ink(Color color) => new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
    }

    /// <summary>The picture's pixels as 0xAARRGGBB, premultiplied, row by row.</summary>
    private static uint[] Read(IWICBitmap* bitmap, int width, int height)
    {
        var pixels = new uint[width * height];
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = width, Height = height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var at = data + (y * stride) + (x * 4);
                    pixels[(y * width) + x] = (uint)(at[3] << 24 | at[2] << 16 | at[1] << 8 | at[0]);
                }
            }
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
        return pixels;
    }

    /// <summary><paramref name="ink"/> drawn over <paramref name="behind"/>, both premultiplied 0xAARRGGBB.</summary>
    private static uint Over(Color ink, uint behind)
    {
        var alpha = ink.A / 255f;
        uint Channel(int shift, byte value) =>
            (uint)Math.Clamp(MathF.Round((value * alpha) + (((behind >> shift) & 0xFF) * (1f - alpha))), 0, 255) << shift;
        return Channel(24, 255) | Channel(16, ink.R) | Channel(8, ink.G) | Channel(0, ink.B);
    }

    /// <summary>Two pixels within a couple of levels of each other in every channel.</summary>
    private static bool Near(uint first, uint second)
    {
        for (var shift = 0; shift < 32; shift += 8)
        {
            if (Math.Abs((int)((first >> shift) & 0xFF) - (int)((second >> shift) & 0xFF)) > 2)
            {
                return false;
            }
        }
        return true;
    }
}
