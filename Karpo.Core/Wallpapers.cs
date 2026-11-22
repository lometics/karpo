// §547: the wallpaper under the bar, as Windows lays it out (the person, 2026-11-07: "the taskbar itself should be a little
// transparent and should show the desktop background image a little blurred behind it. (if possible) how blurry the
// background is should be changeable.").
//
// DWM's own blur is Windows' transparency effects', off on many PCs and not to be made more or less; so Karpo draws it
// itself: each display's wallpaper (IDesktopWallpaper: its picture, how it is fitted, the desktop's colour), the part of it
// under the bar's strip mapped exactly as Windows lays it out (Plan, below: pure), blurred once on the GPU and kept
// (Karpo.Dock's WallpaperStrips). Windows draws a wallpaper in the display's own pixels, never scaled by its DPI: the DPI
// is the bar's size and the blur's radius only.
//
//   Fill      the picture scaled to cover the display, its shape kept, centred: what spills over is cut off
//   Fit       scaled to be all of it on the display, centred; the desktop's colour round it
//   Stretch   scaled to the display, its shape lost
//   Tile      at its own size, again and again from the display's top left corner
//   Center    at its own size, centred; the desktop's colour round it, or cut off
//   Span      one picture over all the displays: Fill over the rectangle round them all, each display its part
//
// A solid colour desktop (no picture) is that colour. What the blur reads past the display's edge (a bar on a screen's
// edge) is the display's edge again, not black: Plan says what is inside the display, the rest is its edge drawn out.

namespace Karpo.Core;

/// <summary>§547: how Windows fits a wallpaper's picture to the display (DESKTOP_WALLPAPER_POSITION).</summary>
public enum WallpaperFit
{
    Center,
    Tile,
    Stretch,
    Fit,
    Fill,
    Span,
}

/// <summary>
/// §547: what a display's desktop shows: its picture's file (null or empty for none — a solid colour), when it was last
/// written (so a file written again under the same name is another picture), how it is fitted, and the desktop's colour
/// round it and in its place.
/// </summary>
public readonly record struct WallpaperSource(string? Path, long Written, WallpaperFit Fit, Argb Background)
{
    /// <summary>Whether the desktop is its colour alone.</summary>
    public bool Solid => string.IsNullOrEmpty(Path);
}

/// <summary>
/// §547: one piece of a wallpaper drawn into a strip: the picture scaled to <paramref name="Width"/> × <paramref name="Height"/>
/// px (its own size where it is not scaled), the part of it <paramref name="From"/> (in those px), and where its top left
/// corner lands in the strip (<paramref name="ToX"/>, <paramref name="ToY"/>, the strip's px).
/// </summary>
public readonly record struct WallpaperPiece(int Width, int Height, RectI From, int ToX, int ToY);

/// <summary>
/// §547: how a strip of the screen (<paramref name="Strip"/>, screen px) is drawn of a display's wallpaper: the desktop's
/// colour, then <paramref name="Pieces"/>, all of it within <paramref name="Inside"/> (the part of the strip on the display,
/// in the strip's px); what is outside it the display's edge drawn out.
/// </summary>
public sealed record WallpaperPlan(RectI Strip, RectI Inside, IReadOnlyList<WallpaperPiece> Pieces);

/// <summary>
/// §547: what a bar's blurred wallpaper is made of, which it is kept by and made again for when any of it changes: the
/// desktop's picture and fit (<paramref name="Source"/>), the display and all of them (<paramref name="Monitor"/>,
/// <paramref name="Desktop"/>: a spanned picture's), and the strip of the screen it is drawn for — the bar's own rectangle
/// (<paramref name="Bar"/>) and the margin round it the blur reads (<paramref name="Margin"/>, the most blur's at the
/// display's scale, so the blur's amount is drawn from the same strip). The amount of blur is no part of it: another is
/// the same strip blurred again.
/// </summary>
public readonly record struct WallpaperKey(WallpaperSource Source, RectI Monitor, RectI Desktop, RectI Bar, int Margin)
{
    /// <summary>The strip the wallpaper is drawn into: the bar's rectangle grown by the margin on every side.</summary>
    public RectI Strip => new(Bar.Left - Margin, Bar.Top - Margin, Bar.Right + Margin, Bar.Bottom + Margin);
}

/// <summary>§547: where Windows draws a wallpaper; see the top of the file.</summary>
public static class Wallpapers
{
    /// <summary>
    /// The margin round a bar's strip the blur reads, in px, on a display at <paramref name="scale"/>: three standard
    /// deviations of the most blur (<see cref="KarpoLook.Sigma"/>), all a Gaussian reaches.
    /// </summary>
    public static int Margin(float scale) => (int)MathF.Ceiling(3f * KarpoLook.Sigma(KarpoLook.MostBlur, scale));

    /// <summary>
    /// Where one picture of <paramref name="width"/> × <paramref name="height"/> px fitted <paramref name="fit"/> lies on the
    /// screen (screen px) on <paramref name="monitor"/>, <paramref name="desktop"/> round every display (a spanned one's); for
    /// a tiled one, its first tile.
    /// </summary>
    public static RectI Placed(WallpaperFit fit, int width, int height, RectI monitor, RectI desktop)
    {
        var over = fit == WallpaperFit.Span ? desktop : monitor;
        (int Width, int Height) size = fit switch
        {
            WallpaperFit.Stretch => (over.Width, over.Height),
            WallpaperFit.Fill or WallpaperFit.Span => Scaled(width, height, Math.Max(over.Width / (double)width, over.Height / (double)height)),
            WallpaperFit.Fit => Scaled(width, height, Math.Min(over.Width / (double)width, over.Height / (double)height)),
            _ => (width, height),
        };
        if (fit == WallpaperFit.Tile)
        {
            return new RectI(over.Left, over.Top, over.Left + size.Width, over.Top + size.Height);
        }
        // Centred, half the difference rounded down.
        var left = over.Left + Floor(over.Width - size.Width, 2);
        var top = over.Top + Floor(over.Height - size.Height, 2);
        return new RectI(left, top, left + size.Width, top + size.Height);
    }

    /// <summary>
    /// How <paramref name="strip"/> (screen px) is drawn of a picture of <paramref name="width"/> × <paramref name="height"/>
    /// px fitted <paramref name="fit"/> on <paramref name="monitor"/> (<paramref name="desktop"/> round them all): every piece
    /// of the picture on the part of the strip on the display. None for a picture of no size (a solid colour desktop).
    /// </summary>
    public static WallpaperPlan Plan(WallpaperFit fit, int width, int height, RectI monitor, RectI desktop, RectI strip)
    {
        var on = Intersect(strip, monitor);
        var inside = on is { } shown ? Offset(shown, -strip.Left, -strip.Top) : new RectI(0, 0, 0, 0);
        List<WallpaperPiece> pieces = [];
        if (on is not { } seen || width <= 0 || height <= 0)
        {
            return new WallpaperPlan(strip, inside, pieces);
        }
        var first = Placed(fit, width, height, monitor, desktop);
        if (first.Width <= 0 || first.Height <= 0)
        {
            return new WallpaperPlan(strip, inside, pieces);
        }
        void Piece(RectI copy)
        {
            if (Intersect(copy, seen) is { } part)
            {
                pieces.Add(new WallpaperPiece(copy.Width, copy.Height, Offset(part, -copy.Left, -copy.Top), part.Left - strip.Left, part.Top - strip.Top));
            }
        }
        if (fit != WallpaperFit.Tile)
        {
            Piece(first);
            return new WallpaperPlan(strip, inside, pieces);
        }
        // The tiles from the display's corner, those the strip's part on it meets.
        var (across, down) = (first.Width, first.Height);
        for (var y = first.Top + (Floor(seen.Top - first.Top, down) * down); y < seen.Bottom; y += down)
        {
            for (var x = first.Left + (Floor(seen.Left - first.Left, across) * across); x < seen.Right; x += across)
            {
                Piece(new RectI(x, y, x + across, y + down));
            }
        }
        return new WallpaperPlan(strip, inside, pieces);
    }

    /// <summary>The rectangle round every one of <paramref name="monitors"/>: what a spanned picture covers.</summary>
    public static RectI Round(IEnumerable<RectI> monitors)
    {
        RectI? all = null;
        foreach (var monitor in monitors)
        {
            all = all is { } so ? new RectI(Math.Min(so.Left, monitor.Left), Math.Min(so.Top, monitor.Top), Math.Max(so.Right, monitor.Right), Math.Max(so.Bottom, monitor.Bottom)) : monitor;
        }
        return all ?? default;
    }

    /// <summary>Where <paramref name="a"/> and <paramref name="b"/> overlap; null where they don't.</summary>
    public static RectI? Intersect(RectI a, RectI b)
    {
        var both = new RectI(Math.Max(a.Left, b.Left), Math.Max(a.Top, b.Top), Math.Min(a.Right, b.Right), Math.Min(a.Bottom, b.Bottom));
        return both.Width > 0 && both.Height > 0 ? both : null;
    }

    private static RectI Offset(RectI rect, int x, int y) => new(rect.Left + x, rect.Top + y, rect.Right + x, rect.Bottom + y);

    private static (int Width, int Height) Scaled(int width, int height, double scale) =>
        (Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero)), Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero)));

    /// <summary><paramref name="value"/> over <paramref name="by"/>, rounded down, below zero too.</summary>
    private static int Floor(int value, int by) => (int)Math.Floor(value / (double)by);
}
