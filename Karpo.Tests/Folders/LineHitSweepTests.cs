// folders: a line lights where a press acts on it, and a line with one thing to do lights and acts over all
// of it (the person, 2026-10-28: "for the weather widget the button is still not the whole widget, there are
// some spaces that when hovering over dont get highlighted. this should not be. it should highlight the whole
// area for all the widgets"; docs\widgets.md §379). One sweep over every kind a list holds, drawn as the open
// folder draws it (CellIsland.PaintOn) and asked as it asks (CellIsland.TargetOn), every third pixel:
//
//  - **Lit where it acts.** The pointer on a part a press acts on lights that part, and the part it lights
//    is all a press there acts on: every point of a part inside what it lights, no point inside what it
//    lights that a press does nothing at.
//  - **One thing to do is the whole line.** A line whose presses all do one thing — nothing but that part
//    — does it over the whole line, and lights as the folder lights a line that is
//    one thing to press (FolderFlyout.LightsWhole), its own buttons carved out.
//
// Needs Direct2D: KARPO_UI_TESTS=1. Nothing reaches the network.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.Weather;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed unsafe class LineHitSweepTests
{
    private const int Step = 3;

    // How far past its plate a part may take a press, DIP: a button's air, as Windows' buttons have.
    private const float Slop = 6;

    private static bool Weather(ListKind kind) => kind is ListKind.WeatherNow or ListKind.WeatherHours or ListKind.WeatherDays;

    /// <summary>The line drawn into <paramref name="width"/> × <paramref name="height"/> px, its pixels, premultiplied BGRA.</summary>
    internal static byte[] Drawn(CellIsland line, D2D_RECT_F box, TileStyle style)
    {
        var width = (uint)MathF.Ceiling(box.right);
        var height = (uint)MathF.Ceiling(box.bottom);
        var pixels = new byte[width * height * 4];
        Bitmap(width, height, dc => line.PaintOn(dc, box, style, WidgetSurface.List), bitmap =>
        {
            IWICBitmapLock* locked = null;
            try
            {
                var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
                ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
                uint stride = 0;
                ComUtil.Check(locked->GetStride(&stride));
                uint size = 0;
                byte* data = null;
                ComUtil.Check(locked->GetDataPointer(&size, &data));
                for (var y = 0u; y < height; y++)
                {
                    new ReadOnlySpan<byte>(data + (y * stride), (int)(width * 4)).CopyTo(pixels.AsSpan((int)(y * width * 4)));
                }
            }
            finally
            {
                ComUtil.Release(ref locked);
            }
        });
        return pixels;
    }

    /// <summary>The box of the pixels that differ between two drawings of the same size, and which do; null where none does.</summary>
    internal static (D2D_RECT_F Box, bool[] Lit)? Apart(byte[] one, byte[] other, int width)
    {
        int left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        var lit = new bool[one.Length / 4];
        for (var at = 0; at < one.Length; at += 4)
        {
            var most = 0;
            for (var channel = 0; channel < 4; channel++)
            {
                most = Math.Max(most, Math.Abs(one[at + channel] - other[at + channel]));
            }
            if (most > 1)
            {
                var x = at / 4 % width;
                var y = at / 4 / width;
                (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x + 1), Math.Max(bottom, y + 1));
                lit[at / 4] = true;
            }
        }
        return right < 0 ? null : (new D2D_RECT_F(left, top, right, bottom), lit);
    }

    private static bool Inside(D2D_RECT_F box, Vector2 point, float inset = 0) =>
        point.X >= box.left + inset && point.X < box.right - inset && point.Y >= box.top + inset && point.Y < box.bottom - inset;

    /// <summary>
    /// <paramref name="line"/> in a <paramref name="width"/> × <paramref name="height"/> px line: what each third
    /// pixel is, what lights where it is pointed at, and what the rules above make of it; failures said with <paramref name="name"/>.
    /// </summary>
    private static void Sweep(string name, ListWidget widget, FolderCell cell, CellIsland line, float width, float height, TileStyle style, List<string> failures)
    {
        var box = PopupPaint.Rect(0, 0, width, height);
        // A line the folder lights as a whole is one thing to press all over, which the folder asks of it.
        if (FolderFlyout.LightsWhole(cell, line))
        {
            return;
        }
        var points = new List<(Vector2 At, int Target)>();
        for (var y = 1; y < height - 1; y += Step)
        {
            for (var x = 1; x < width - 1; x += Step)
            {
                var at = new Vector2(x + 0.5f, y + 0.5f);
                points.Add((at, line.TargetOn(at, box, style, WidgetSurface.List)));
            }
        }
        var parts = points.Select(point => point.Target).Where(target => target is not 0).Distinct().ToList();
        // One thing to say and to do — a line of clicks alone, the same pieces whatever happens, whose one part
        // is what it shows (a quarter of it or more) rather than a button on it: the whole line. A timer's start button or a list of one notification are not that.
        var says = widget.Interaction == Interaction.Click && !widget.Grows
            && points.Count(point => point.Target is not 0) * 4 >= points.Count;
        if (parts.Count == 1 && says && points.Count(point => point.Target == 0) is var dead and > 0)
        {
            var example = points.First(point => point.Target == 0).At;
            failures.Add($"{name}: its one part ({parts[0]}) leaves {dead} of {points.Count} points doing nothing, e.g. ({example.X}, {example.Y}); a line that is one thing to press is all of it");
        }
        line.HoverOn(null, box, style, WidgetSurface.List);
        var rest = Drawn(line, box, style);
        foreach (var part in parts)
        {
            var on = points.First(point => point.Target == part).At;
            line.HoverOn(on, box, style, WidgetSurface.List);
            var lit = Apart(rest, Drawn(line, box, style), (int)MathF.Ceiling(width));
            // A picture of its largest part lit, on the panel's backdrop: line-hit-<kind>.png in KARPO_PNG_DIR.
            if (part == parts.MaxBy(each => points.Count(point => point.Target == each)))
            {
                var backdrop = PopupPaint.Ink(FlyoutTheme.For(ListFormPictures.LookFor(dark: true)).Base);
                Bitmap(width, height, dc =>
                {
                    var under = backdrop;
                    dc->Clear(&under);
                    line.PaintOn(dc, box, style, WidgetSurface.List);
                }, bitmap => Save(bitmap, Path.Combine(Pictures, $"line-hit-{name.Replace(' ', '-')}.png")));
            }
            line.HoverOn(null, box, style, WidgetSurface.List);
            // A part the wheel turns (a timer's number) is no thing to press, and lights nothing.
            if (lit is null && widget.Interaction.HasFlag(Interaction.Wheel))
            {
                continue;
            }
            if (lit is not { } found)
            {
                failures.Add($"{name}: part {part} lights nothing under the pointer at ({on.X}, {on.Y})");
                continue;
            }
            var (plate, mask) = found;
            // A button takes a press a little past its plate, as Windows' do: a few px, never a band of the line.
            var slop = style.Px(Slop);
            var outside = points.Where(point => point.Target == part && !Inside(plate with { left = plate.left - slop, top = plate.top - slop, right = plate.right + slop, bottom = plate.bottom + slop }, point.At)).ToList();
            // A slider is pressed along its track and lights its thumb, as Windows' does (Interaction.Drag).
            if (outside.Count > 0 && !widget.Interaction.HasFlag(Interaction.Drag))
            {
                failures.Add($"{name}: part {part} acts at {outside.Count} points past what it lights ({plate.left},{plate.top})-({plate.right},{plate.bottom}), e.g. ({outside[0].At.X}, {outside[0].At.Y})");
            }
            var deadInside = points.Where(point => point.Target == 0 && Inside(plate, point.At, style.Px(4)) && mask[((int)point.At.Y * (int)MathF.Ceiling(width)) + (int)point.At.X]).ToList();
            if (deadInside.Count > 0)
            {
                failures.Add($"{name}: part {part} lights ({plate.left},{plate.top})-({plate.right},{plate.bottom}), and a press does nothing at {deadInside.Count} points in it, e.g. ({deadInside[0].At.X}, {deadInside[0].At.Y})");
            }
        }
    }

    /// <summary>
    /// Every kind a list holds, made as a folder makes it, at its own height and — the weather's conditions,
    /// whose drawing changes with it — at one row, the compact line, the header and the header with the days.
    /// </summary>
    [UiFact]
    public void EveryLineLightsWhereItActsAndOneThingToDoIsAllOfIt()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx => SavedWeather.AtAFixedTime(() =>
        {
            MediaIsland.Stand = new MediaStand();
            try
            {
                var look = ListFormPictures.LookFor(dark: true) with { Animations = false };
                var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
                foreach (var widget in ListWidgets.All)
                {
                    var item = ListFormPictures.Line(widget.Kind, made =>
                    {
                        if (Weather(widget.Kind))
                        {
                            made.Weather = SavedWeather.Options();
                        }
                    });
                    var cell = new FolderCell { Island = item };
                    var island = Karpo.Dock.Islands.CreateLine($"layout:default/folder/{item.Id}", item, widget.Kind, gfx, new Config());
                    try
                    {
                        if (island is WeatherLineTile weather)
                        {
                            weather.Source = SavedWeather.Source();
                        }
                        if (island is not CellIsland line)
                        {
                            continue;
                        }
                        const float width = 360;
                        var natural = line is IListContent content ? content.ListHeight(width, style) : 0;
                        var heights = new List<float> { natural > 0 ? natural : ListFormPictures.Cells(widget.Cells, 1, 1) };
                        if (widget.Kind == ListKind.WeatherNow)
                        {
                            heights.AddRange([ListFormPictures.Rows(1, 1, 1), ListFormPictures.Rows(2, 1, 1), ListFormPictures.Rows(8, 1, 1)]);
                        }
                        foreach (var height in heights)
                        {
                            try
                            {
                                Sweep($"{widget.Kind} {height:0} px", widget, cell, line, width, height, style, failures);
                            }
                            catch (Exception error) when (error is not OutOfMemoryException)
                            {
                                failures.Add($"{widget.Kind} {height:0} px: threw {error.GetType().Name}: {error.Message}");
                            }
                        }
                    }
                    finally
                    {
                        island?.Dispose();
                    }
                }
            }
            finally
            {
                MediaIsland.Stand = null;
            }
        }));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures) + $"{Environment.NewLine}({failures.Count} in all)");
    }
}
