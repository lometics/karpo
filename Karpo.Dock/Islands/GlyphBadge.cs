// A glyph with a count on its corner — the mail face's envelope and its unread
// number, the bell face's bell and how many are new, as the mail entry and the
// bell on the bar drew them: the accent pill over the glyph's top-right corner,
// inside the button, which keeps the room its widest count stands past the glyph
// whatever it shows (Trail, FaceSize.Trail), so it never changes size as a count comes
// and goes (§276).

using System.Globalization;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal static unsafe class GlyphBadge
{
    /// <summary>The count as a badge says it: "99+" beyond 99, null for none.</summary>
    public static string? Text(int count) =>
        count <= 0 ? null : count > 99 ? "99+" : count.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// How far past the glyph's square <paramref name="badge"/> — the widest count, "99+", which the
    /// faces keep room for whatever they show (§276); none for null — stands across a bar, and with <paramref name="stale"/> the stale mark: the room a face keeps
    /// after its square (<see cref="FaceSize.Trail"/>), so the count is never cut where another face,
    /// the name or the button's end follows. Nothing down a side, where the count stands across the bar's width.
    /// </summary>
    public static float Trail(Gfx gfx, in IslandStyle style, bool stale = false, string? badge = "99+")
    {
        if (style.Vertical)
        {
            return 0;
        }
        var fit = new Fit(style);
        var plan = Plan(gfx, style, badge ?? "9");
        var trail = badge is null ? 0 : MathF.Ceiling(plan.BadgeWidth * 0.6f);
        return stale ? MathF.Max(trail, MathF.Ceiling(StaleSide(fit, plan.Glyph) * 0.7f)) : trail;
    }

    /// <summary>
    /// Paints <paramref name="glyph"/> in the middle of <paramref name="box"/>, the glyph's square,
    /// and <paramref name="badge"/> on its corner; with <paramref name="stale"/>, the stale mark
    /// (StaleMark.cs) small on its lower corner. Both are held inside the bar's cell about the box's
    /// middle, as the entry held them inside its own, or inside <paramref name="room"/>, the button's
    /// cell, where it is given. <paramref name="rise"/> is how much of the pill
    /// stands above the glyph's square; <paramref name="low"/> puts it on the lower corner instead —
    /// the bell's while do not disturb is on, whose "zZ" is the upper corner and says so.
    /// </summary>
    public static void Paint(
        ID2D1DeviceContext* dc, Gfx gfx, in IslandStyle style, string glyph, string? badge, bool available, D2D_RECT_F box,
        bool stale = false, float rise = 0.2f, bool low = false, D2D_RECT_F? room = null)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var plan = Plan(gfx, style, badge);
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        ID2D1SolidColorBrush* brush = null;
        TextLine shape = default;
        TextLine count = default;
        TextLine mark = default;
        try
        {
            brush = Gfx.Brush(dc, ink.Text);
            shape = TextLine.Lay(gfx, glyph, plan.Glyph, Weight.Regular, null, gfx.IconFamily);
            var left = TextLine.Round(((box.left + box.right) / 2) - (plan.Glyph / 2));
            var top = TextLine.Round(((box.top + box.bottom) / 2) - (plan.Glyph / 2));
            // The button's cell, else a cell's square about the glyph's middle: the marks stay inside it.
            var held = room ?? Around(box, fit.Cell);
            shape.Draw(dc, brush, available ? ink.Text : ink.Disabled, left + (plan.Glyph - shape.Width) / 2, top + (plan.Glyph - shape.Height) / 2);
            if (stale)
            {
                // Half the glyph, against its lower right corner and mostly clear of it, in the
                // secondary ink: there, but quieter than the count.
                var px = StaleSide(fit, plan.Glyph);
                mark = TextLine.Lay(gfx, StaleMark.Glyph, px, Weight.Regular, null, gfx.IconFamily);
                var markLeft = MathF.Min(left + plan.Glyph - (px * 0.3f), held.right - px);
                var markTop = MathF.Min(top + plan.Glyph - (px * 0.55f), held.bottom - px);
                mark.Draw(dc, brush, ink.Secondary, markLeft + ((px - mark.Width) / 2), markTop + ((px - mark.Height) / 2));
            }
            if (badge is null)
            {
                return;
            }
            var rect = Place(fit, plan, left, top, held, rise, low);
            var (x, y) = (rect.left, rect.top);
            var pill = new D2D1_ROUNDED_RECT
            {
                rect = rect,
                radiusX = plan.BadgeHeight / 2,
                radiusY = plan.BadgeHeight / 2,
            };
            var accent = ink.Accent;
            brush->SetColor(&accent);
            dc->FillRoundedRectangle(&pill, (ID2D1Brush*)brush);
            count = TextLine.Lay(gfx, badge, plan.BadgePx, Weight.Semibold, null);
            count.Draw(dc, brush, OnAccent(style.Look), x + (plan.BadgeWidth - count.Width) / 2, y + (plan.BadgeHeight - count.Height) / 2);
        }
        finally
        {
            count.Release();
            mark.Release();
            shape.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>
    /// Where <see cref="Paint"/> sets the pill of <paramref name="badge"/> for a glyph in
    /// <paramref name="box"/>, or null for none. Pure: the tests hold it inside the button.
    /// </summary>
    internal static D2D_RECT_F? Pill(Gfx gfx, in IslandStyle style, string? badge, D2D_RECT_F box, float rise = 0.2f, bool low = false, D2D_RECT_F? room = null)
    {
        if (badge is null)
        {
            return null;
        }
        var fit = new Fit(style);
        var plan = Plan(gfx, style, badge);
        var left = TextLine.Round(((box.left + box.right) / 2) - (plan.Glyph / 2));
        var top = TextLine.Round(((box.top + box.bottom) / 2) - (plan.Glyph / 2));
        return Place(fit, plan, left, top, room ?? Around(box, fit.Cell), rise, low);
    }

    /// <summary>
    /// The pill over the corner of the glyph's square at <paramref name="left"/>, <paramref name="top"/>,
    /// held inside <paramref name="held"/> a pixel from its edges.
    /// </summary>
    private static D2D_RECT_F Place(in Fit fit, in Layout plan, float left, float top, D2D_RECT_F held, float rise, bool low)
    {
        var x = MathF.Min(left + plan.Glyph - plan.BadgeWidth * 0.4f, held.right - plan.BadgeWidth - fit.Px(1));
        // A fifth of the pill above the glyph's square, not the bell's third: the envelope's ink is
        // short in its square, and a pill standing higher drew the face's ink off the cell's middle
        // on a thin bar.
        var y = low
            ? MathF.Min(held.bottom - plan.BadgeHeight - fit.Px(1), top + plan.Glyph - plan.BadgeHeight * 0.65f)
            : MathF.Max(held.top + fit.Px(1), top - plan.BadgeHeight * rise);
        return new D2D_RECT_F(x, y, x + plan.BadgeWidth, y + plan.BadgeHeight);
    }

    /// <summary>A square <paramref name="side"/> px about <paramref name="box"/>'s middle.</summary>
    internal static D2D_RECT_F Around(D2D_RECT_F box, float side)
    {
        var x = (box.left + box.right) / 2;
        var y = (box.top + box.bottom) / 2;
        return new D2D_RECT_F(x - (side / 2), y - (side / 2), x + (side / 2), y + (side / 2));
    }

    /// <summary>The stale mark's side: about half the glyph, 10 DIP at least.</summary>
    private static float StaleSide(in Fit fit, float glyph) => MathF.Max(MathF.Round(glyph * 0.6f), fit.Px(10));

    /// <summary>
    /// The glyph on the icon font's size the other glyph islands use; the pill measured from the
    /// glyph (never taller than it), with the number shrunk into the pill rather than the pill grown
    /// to the number (docs\widgets.md §12.4).
    /// </summary>
    private static Layout Plan(Gfx gfx, in IslandStyle style, string? badge)
    {
        var fit = new Fit(style);
        var glyph = fit.Glyph();
        if (badge is null)
        {
            return new Layout(glyph, 0, 0, 0);
        }
        // From the glyph alone, never from the text scale: a mark on an icon is the same mark whatever
        // the text size, so the pill is a share of the glyph (six tenths, 15 DIPs at least, never
        // taller than it) and the number is shrunk into it (docs\widgets.md §12.4).
        var height = MathF.Ceiling(MathF.Min(MathF.Max(glyph * 0.6f, fit.Px(15)), glyph * 0.95f));
        var wanted = height;
        var least = MathF.Max(6 * fit.Scale, 1);
        var px = MathF.Max(least, wanted);
        while (px > least && TextLine.Template(gfx, badge, px, Weight.Semibold).Height > height - fit.Px(1))
        {
            px = MathF.Max(least, px - fit.Scale);
        }
        var (textWidth, _) = TextLine.Template(gfx, badge, px, Weight.Semibold);
        var width = MathF.Ceiling(MathF.Min(MathF.Max(height, textWidth + fit.Px(8)), glyph * 1.6f));
        return new Layout(glyph, px, width, height);
    }

    /// <summary>TextOnAccentFillColorPrimary for the Look.</summary>
    private static DXGI_RGBA OnAccent(in Look look)
    {
        if (look.HighContrast)
        {
            var color = look.HcHighlightText;
            return color.A == 0 ? new DXGI_RGBA(1, 1, 1, 1) : new DXGI_RGBA(color.R / 255f, color.G / 255f, color.B / 255f, 1);
        }
        return look.Dark ? new DXGI_RGBA(0, 0, 0, 1) : new DXGI_RGBA(1, 1, 1, 1);
    }

    private readonly record struct Layout(float Glyph, float BadgePx, float BadgeWidth, float BadgeHeight);
}
