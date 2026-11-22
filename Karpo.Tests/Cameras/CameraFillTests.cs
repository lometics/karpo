// camera: Fill fills the whole line (the person, 2026-10-31: "the video camera when set to fill does not fill the
// whole widget although i want it to."; docs\widgets.md §331, §463). A camera line drawn as the open folder draws it
// (FolderFlyout.PaintContent, clipped to the section's rounded corners as its card is), over a key colour, with a
// checkered picture of a known shape — wide, tall and 4:3 — in lines of two, five and eight rows and as tall as it
// asks for, at three widths, at 100 % and 150 %:
//
//  - **Fill covers the line**: every pixel of the line inside its rounded corners is picture (or the name's plate
//    over it) — no gutter, no air over or under it, no bars; the key colour nowhere.
//  - **Cut, centred, its shape kept**: the checks are as wide as they are tall, at the scale that covers the line
//    (the larger of the two fits), and a check's corner stands at the line's middle both ways.
//  - **Whole is letterboxed**: the checks at the scale that fits the picture inside the line's room, and the key
//    colour — the line's own ground, nothing under the picture — beside or over and under it.
//  - **Under the pointer and pressed**, a filling picture lights as the folder lights a line, over the picture.
//
// Pictures: camera-fill-<scale>.png in KARPO_PNG_DIR. Needs Direct2D: KARPO_UI_TESTS=1. No camera is opened: the line
// shows a picture made here (CameraLine.Source).

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.CameraLines;

public sealed unsafe class CameraFillTests
{
    /// <summary>One check of the picture, in its own px.</summary>
    private const int Check = 20;

    private static readonly (byte R, byte G, byte B) One = (0x20, 0x90, 0xD0);
    private static readonly (byte R, byte G, byte B) Other = (0xF0, 0xC0, 0x20);

    /// <summary>The colour under the line: where it shows, the picture does not.</summary>
    private static readonly Windows.UI.Color Key = Windows.UI.Color.FromArgb(255, 0xFF, 0x00, 0xFF);

    private static readonly (int Width, int Height)[] Frames = [(640, 360), (360, 640), (400, 300)];

    private static readonly int[] Rows = [2, 5, 8, 0];

    private static readonly float[] Widths = [0.75f, 1f, 1.5f];

    [UiFact]
    public void FillCoversTheWholeLineAndWholeIsLetterboxed()
    {
        OnStaThread(gfx =>
        {
            var failures = new List<string>();
            foreach (var scale in new[] { 1f, 1.5f })
            {
                Sheet(gfx, scale, failures);
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40)) + (failures.Count > 40 ? $"\n… and {failures.Count - 40} more" : ""));
        });
    }

    /// <summary>A picture <paramref name="width"/> × <paramref name="height"/> of checks <see cref="Check"/> px, a check's corner at its middle.</summary>
    internal static Pixels Checks(int width, int height)
    {
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                // Counted from the middle, so a check's corner is there whatever the size.
                var across = (int)Math.Floor((x - (width / 2.0)) / Check);
                var down = (int)Math.Floor((y - (height / 2.0)) / Check);
                var (r, g, b) = ((across + down) & 1) == 0 ? One : Other;
                var at = ((y * width) + x) * 4;
                (data[at], data[at + 1], data[at + 2], data[at + 3]) = (b, g, r, 255);
            }
        }
        return new Pixels { Data = data, Width = width, Height = height };
    }

    private static CameraLine Line(Gfx gfx, CameraFit? fit, Pixels frame)
    {
        var item = ListKind.Camera.New();
        item.Id = "camera";
        item.Camera = new CameraOptions { Url = "http://garden.local/video.mjpg", Name = "Cam", Fit = fit };
        item.Normalize();
        var line = (CameraLine)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "camera"), item, ListKind.Camera, gfx, new Config())!;
        var shot = new CameraShot(frame, 2, CameraState.Live, null, CameraProtocol.Snapshot, DateTime.UtcNow.AddMinutes(-3));
        line.Source = () => shot;
        return line;
    }

    private enum Lit
    {
        Rest,
        Hover,
        Pressed,
    }

    private sealed record Case(string Name, CameraFit? Fit, Pixels Frame, float Width, float Height, Lit Lit);

    private static void Sheet(Gfx gfx, float scale, List<string> failures)
    {
        var look = ListFormPictures.LookFor(dark: true);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var pad = PopupPaint.Round(16 * scale);
        var radius = PopupPaint.Round(8 * scale);
        var cases = new List<Case>();
        foreach (var (fw, fh) in Frames)
        {
            var frame = Checks(fw, fh);
            foreach (var share in Widths)
            {
                var width = PopupPaint.Round(FolderFlyout.ListWidth(scale) * share);
                foreach (var rows in Rows)
                {
                    foreach (var fit in new CameraFit?[] { null, CameraFit.Whole })
                    {
                        using var asked = new LineHolder(Line(gfx, fit, frame));
                        var height = rows > 0 ? LineRows.Px(rows, style) : asked.Line.ListHeight(width, style);
                        var name = $"{scale * 100:0} %, {fw}×{fh} in {width}×{height} ({(rows > 0 ? $"{rows} rows" : "automatic")}), {(fit is null ? "Fill" : "Whole")}";
                        cases.Add(new Case(name, fit, frame, width, height, Lit.Rest));
                    }
                }
            }
        }
        // Lit: a filling picture under the pointer and pressed, beside the same at rest.
        var lit = Checks(640, 360);
        var litWidth = FolderFlyout.ListWidth(scale);
        var litHeight = LineRows.Px(5, style);
        foreach (var how in new[] { Lit.Rest, Lit.Hover, Lit.Pressed })
        {
            cases.Add(new Case($"{scale * 100:0} %, lit {how}", null, lit, litWidth, litHeight, how));
        }

        // Each line held in a bitmap of its own, as a folder's line is a surface of its own (far down a sheet, Direct2D's
        // software rasteriser lays a picture a pixel or two off); then the sheet, flowed, for the eye.
        var lines = cases.Select(one => Line(gfx, one.Fit, one.Frame)).ToList();
        void Paint(ID2D1DeviceContext* dc, int index, D2D_RECT_F place)
        {
            var (one, line) = (cases[index], lines[index]);
            var box = PopupPaint.Rect(0, 0, one.Width, one.Height);
            var middle = new Vector2(one.Width / 2, one.Height / 3);
            line.Visible(true);
            if (one.Lit != Lit.Rest)
            {
                line.HoverOn(middle, box, style, WidgetSurface.List);
            }
            if (one.Lit == Lit.Pressed)
            {
                _ = line.PressOn(middle, box, style, WidgetSurface.List);
            }
            PopupPaint.Fill(dc, place, 0, PopupPaint.Ink(Key));
            PopupPaint.Clipped(dc, place, radius, () => Shifted(dc, place.left, place.top, () =>
                FolderFlyout.PaintContent(dc, line, box, style)));
        }
        try
        {
            var rest = new Dictionary<string, (int R, int G, int B)>();
            for (var index = 0; index < cases.Count; index++)
            {
                var one = cases[index];
                var place = PopupPaint.Rect(pad, pad, one.Width, one.Height);
                var (width, height) = ((uint)MathF.Ceiling(one.Width + (2 * pad)), (uint)MathF.Ceiling(one.Height + (2 * pad)));
                var at = index;
                Bitmap(width, height, dc => Paint(dc, at, place), bitmap =>
                {
                    var data = new byte[width * height * 4];
                    fixed (byte* into = data)
                    {
                        Karpo.Dock.Native.ComUtil.Check(bitmap->CopyPixels(null, width * 4, (uint)data.Length, into));
                    }
                    (int R, int G, int B) Pixel(float x, float y)
                    {
                        var where = (((int)y * (int)width) + (int)x) * 4;
                        return (data[where + 2], data[where + 1], data[where]);
                    }
                    Hold(one, place, lines[at], style, radius, Pixel, rest, failures);
                });
            }
            // The folder's subtle fill laid over the picture: under the pointer and pressed each differ from rest and from
            // the other (Windows' pressed fill is fainter than its hover).
            var (still, hover, pressed) = (rest[nameof(Lit.Rest)], rest[nameof(Lit.Hover)], rest[nameof(Lit.Pressed)]);
            if (Apart(hover, still) < 3)
            {
                failures.Add($"{scale * 100:0} %: a filling picture under the pointer is {hover}, as at rest {still}: not lit");
            }
            if (Apart(pressed, still) < 2 || Apart(pressed, hover) < 2)
            {
                failures.Add($"{scale * 100:0} %: a filling picture pressed is {pressed}, at rest {still}, under the pointer {hover}: not lit pressed");
            }

            // The sheet: the cases flowed in rows, a row as tall as its tallest.
            var most = 2400 * scale;
            var places = new List<D2D_RECT_F>();
            var (x, y, tallest) = ((float)pad, (float)pad, 0f);
            foreach (var one in cases)
            {
                if (x + one.Width + pad > most && x > pad)
                {
                    (x, y, tallest) = (pad, y + tallest + pad, 0);
                }
                places.Add(PopupPaint.Rect(x, y, one.Width, one.Height));
                x += one.Width + pad;
                tallest = MathF.Max(tallest, one.Height);
            }
            var (sheetWidth, sheetHeight) = (places.Max(place => place.right) + pad, places.Max(place => place.bottom) + pad);
            Bitmap(sheetWidth, sheetHeight, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C)));
                for (var index = 0; index < cases.Count; index++)
                {
                    Paint(dc, index, places[index]);
                }
            }, bitmap => Save(bitmap, Path.Combine(Pictures, $"camera-fill-{scale * 100:0}.png")));
        }
        finally
        {
            foreach (var line in lines)
            {
                line.Dispose();
            }
        }
    }

    private delegate (int R, int G, int B) Reader(float x, float y);

    private static void Hold(Case one, D2D_RECT_F place, CameraLine line, in TileStyle style, float radius, Reader pixel,
        Dictionary<string, (int R, int G, int B)> rest, List<string> failures)
    {
        var (left, top, right, bottom) = (place.left, place.top, place.right, place.bottom);
        var (width, height) = (right - left, bottom - top);
        var fill = one.Fit is null;
        var room = fill ? new D2D_RECT_F(0, 0, width, height) : line.PictureBox(new D2D_RECT_F(0, 0, width, height), style);
        var (roomWidth, roomHeight) = (room.right - room.left, room.bottom - room.top);
        var fits = (roomWidth / one.Frame.Width, roomHeight / one.Frame.Height);
        var drawnScale = fill ? MathF.Max(fits.Item1, fits.Item2) : MathF.Min(fits.Item1, fits.Item2);
        var pitch = Check * drawnScale;

        if (one.Name.Contains(", lit ", StringComparison.Ordinal))
        {
            // Lit: a check's middle near the line's middle, beside the same at rest.
            rest[one.Lit.ToString()] = pixel(left + (width / 2) + (pitch / 2), top + (height / 2) - (pitch / 2));
            return;
        }

        // Where the key colour shows, the picture does not.
        var bare = 0;
        var firstBare = default((float X, float Y)?);
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                if (InCorner(x + 0.5f - left, y + 0.5f - top, width, height, radius) || !IsKey(pixel(x, y)))
                {
                    continue;
                }
                bare++;
                firstBare ??= (x - left, y - top);
            }
        }
        if (fill && bare > 0)
        {
            failures.Add($"{one.Name}: {bare} px of the line are not picture, the first at {firstBare}");
        }
        if (!fill && bare == 0 && MathF.Abs((roomWidth / roomHeight) - (one.Frame.Width / (float)one.Frame.Height)) > 0.05f)
        {
            failures.Add($"{one.Name}: whole inside the line, yet no bar beside or over it");
        }

        // Whole: its bars are the line's own ground, nothing laid under the picture (the person, 2026-11-02: "the camera
        // widget should not show the lighter background behind the fit all image, it should show regular background").
        if (!fill)
        {
            var (shownW, shownH) = (MathF.Min(roomWidth, one.Frame.Width * (MathF.Min(roomWidth / one.Frame.Width, roomHeight / one.Frame.Height))),
                MathF.Min(roomHeight, one.Frame.Height * (MathF.Min(roomWidth / one.Frame.Width, roomHeight / one.Frame.Height))));
            var (barX, barY) = roomWidth - shownW > 6
                ? (left + room.left + ((roomWidth - shownW) / 4), top + ((room.top + room.bottom) / 2))
                : roomHeight - shownH > 6 ? (left + ((room.left + room.right) / 2), top + room.top + ((roomHeight - shownH) / 4)) : (-1f, -1f);
            if (barX >= 0 && pixel(barX, barY) is var seen && Apart(seen, (Key.R, Key.G, Key.B)) > 3)
            {
                failures.Add($"{one.Name}: its bar at ({barX - left:0}, {barY - top:0}) is {seen}, not the line's own ground {(Key.R, Key.G, Key.B)}");
            }
        }

        // The checks across a row near the top and down a column near the end: as wide as tall, at the scale that
        // covers (Fill) or fits (Whole), a corner at the middle both ways.
        // Inside what is drawn of the picture: the line's room filled, or the picture's own shape whole in it.
        var (shownWidth, shownHeight) = (MathF.Min(roomWidth, one.Frame.Width * drawnScale), MathF.Min(roomHeight, one.Frame.Height * drawnScale));
        var (middleX, middleY) = (left + ((room.left + room.right) / 2), top + ((room.top + room.bottom) / 2));
        // Through checks' middles, never along an edge between them.
        var rowY = middleY - (pitch * (MathF.Floor(shownHeight * 0.25f / pitch) + 0.5f));
        var columnX = middleX + (pitch * (MathF.Floor(shownWidth * 0.35f / pitch) + 0.5f));
        var across = Edges(x => pixel(x, rowY), middleX - (shownWidth / 2) + 2, middleX + (shownWidth / 2) - 2);
        var down = Edges(y => pixel(columnX, y), middleY - (shownHeight / 2) + 2, middleY + (shownHeight / 2) - 2);
        foreach (var (way, edges, middle) in new[] { ("across", across, middleX), ("down", down, middleY) })
        {
            if (edges.Count < 2)
            {
                failures.Add($"{one.Name}: fewer than two checks' edges {way}");
                continue;
            }
            var steps = edges.Zip(edges.Skip(1), (a, b) => b - a).OrderBy(step => step).ToList();
            var step = steps[steps.Count / 2];
            if (MathF.Abs(step - pitch) > 1.2f)
            {
                failures.Add($"{one.Name}: a check is {step:0.0} px {way}, not {pitch:0.0} ({(fill ? "covering" : "whole")} at {drawnScale:0.000})");
            }
            if (edges.Min(edge => MathF.Abs(edge - middle)) > 1.2f)
            {
                failures.Add($"{one.Name}: no check's edge at the middle {way} ({middle:0.0}); nearest {edges.MinBy(edge => MathF.Abs(edge - middle)):0.0}; edges {string.Join(", ", edges)}");
            }
        }
    }

    /// <summary>Where along a run the picture turns from one check's colour to the other's, between pixels.</summary>
    private static List<float> Edges(Func<float, (int R, int G, int B)> at, float from, float to)
    {
        var edges = new List<float>();
        int? was = null;
        for (var along = MathF.Floor(from); along < to; along++)
        {
            var seen = at(along);
            if (IsKey(seen))
            {
                was = null;
                continue;
            }
            var which = Apart(seen, One) < Apart(seen, Other) ? 0 : 1;
            if (was is { } before && before != which)
            {
                edges.Add(along);
            }
            was = which;
        }
        return edges;
    }

    private static int Apart((int R, int G, int B) seen, (int R, int G, int B) ink) =>
        Math.Abs(seen.R - ink.R) + Math.Abs(seen.G - ink.G) + Math.Abs(seen.B - ink.B);

    /// <summary>The key colour: the line's own ground.</summary>
    private static bool IsKey((int R, int G, int B) seen) => seen.R > 200 && seen.G < 70 && seen.B > 200;

    /// <summary>Outside the rounded corners' arcs, or on their smoothed edge.</summary>
    private static bool InCorner(float x, float y, float width, float height, float radius)
    {
        var cx = x < radius ? radius : x > width - radius ? width - radius : x;
        var cy = y < radius ? radius : y > height - radius ? height - radius : y;
        return (x != cx || y != cy) && Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) > radius - 1.5f;
    }

    private sealed class LineHolder(CameraLine line) : IDisposable
    {
        public CameraLine Line => line;

        public void Dispose() => line.Dispose();
    }
}
