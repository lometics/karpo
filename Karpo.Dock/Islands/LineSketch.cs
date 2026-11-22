// A list line laid out for the height it is given, and drawn in the same pass (the person,
// 2026-09-27: "for the list view widgets they have not been optimized ad all. they should also be
// optimized to look good from 1 to 6 cell heights").
//
// A line used to be one drawing laid out for the height its content wanted, and given any other
// height it was cut at the bottom or left air under itself. A line laid out for every height is a
// set of forms instead — one line on one row, the compact line, the whole widget — chosen by what
// the height holds (ListTiers, which every list line shares), then filling it. That choice and that
// layout are the widget's own; what is shared besides is this:
//
//   * LineSketch: the painting calls a line makes (a line of text cut with an ellipsis at its room,
//     a glyph centred in its box, a plate behind something, a picture), each drawn where there is a
//     device and, device or not, written down as a piece with its box and what it says. So one pass
//     lays the line out, draws it, and says what it drew — which is what a test holds it to: every
//     piece inside the line and between its gutters, nothing standing on anything else, and on one
//     row the widget's main reading (ILinePieces).
//   * LineRows: how tall a number of the list's rows and cells is, by the folder's own arithmetic
//     (FolderFlyout.CellPx, §442), and how many items a form shows, never fewer on a taller line. Which
//     form a height takes is what fits it (ListTiers, beside this), not how many rows it is: a line
//     as tall as its own content is rarely a whole number of rows.
//
// It is additive: a line that does not use it draws as it always did.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// One piece of a list line as it was laid out: what it is, where it stands (px, in the line's own
/// space) and what it says; <paramref name="Behind"/> for a plate other pieces stand on.
/// </summary>
internal readonly record struct LinePiece(string Name, D2D_RECT_F Box, string Text = "", bool Behind = false);

/// <summary>A list line that lays itself out for any height it is given, and says what it laid out.</summary>
internal interface ILinePieces
{
    /// <summary>The name of the piece that is the widget's main reading: what a line of one row shows first.</summary>
    string MainPiece { get; }

    /// <summary>What the line lays out in <paramref name="box"/> at <paramref name="style"/>: every piece, where it stands, what it says.</summary>
    IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style);
}

/// <summary>
/// The painting calls of a list line, drawn where there is a device and written down as pieces
/// whether there is or not (see the top of the file).
/// </summary>
internal sealed unsafe class LineSketch
{
    private readonly ID2D1DeviceContext* dc;

    public LineSketch(ID2D1DeviceContext* dc, Gfx gfx)
    {
        this.dc = dc;
        Gfx = gfx;
    }

    public Gfx Gfx { get; }

    /// <summary>Every piece laid out so far, in the order it was.</summary>
    public List<LinePiece> Pieces { get; } = [];

    /// <summary>Whether this pass draws, or only lays out.</summary>
    public bool Draws => dc != null;

    /// <summary>The device, for a picture that draws itself; null while only laying out.</summary>
    public ID2D1DeviceContext* Device => dc;

    /// <summary>
    /// One line of <paramref name="text"/> from (<paramref name="x"/>, <paramref name="y"/>), cut with an
    /// ellipsis at <paramref name="room"/>; the box it took, or an empty one where the room holds
    /// nothing readable of it (<see cref="TilePaint.Readable"/>) and it is left out.
    /// </summary>
    public D2D_RECT_F Text(string name, string text, float px, Weight weight, DXGI_RGBA ink, float x, float y, float room)
    {
        if (text.Length == 0 || !TilePaint.Readable(Gfx, text, px, weight, room))
        {
            return new D2D_RECT_F(x, y, x, y);
        }
        var size = PopupPaint.Measure(Gfx, text, px, weight, room);
        if (dc != null)
        {
            PopupPaint.Text(dc, Gfx, text, px, weight, ink, x, y, room);
        }
        var box = new D2D_RECT_F(x, y, x + MathF.Min(size.X, room), y + size.Y);
        Pieces.Add(new LinePiece(name, box, text));
        return box;
    }

    /// <summary>One line ending at <paramref name="right"/>, cut at <paramref name="room"/>; the box it took, empty where it is left out.</summary>
    public D2D_RECT_F TextRight(string name, string text, float px, Weight weight, DXGI_RGBA ink, float right, float y, float room)
    {
        if (text.Length == 0 || !TilePaint.Readable(Gfx, text, px, weight, room))
        {
            return new D2D_RECT_F(right, y, right, y);
        }
        var size = PopupPaint.Measure(Gfx, text, px, weight, room);
        var left = right - MathF.Min(size.X, room);
        if (dc != null)
        {
            PopupPaint.Text(dc, Gfx, text, px, weight, ink, left, y, room);
        }
        var box = new D2D_RECT_F(left, y, right, y + size.Y);
        Pieces.Add(new LinePiece(name, box, text));
        return box;
    }

    /// <summary>
    /// <paramref name="text"/> wrapped into at most <paramref name="lines"/> lines of <paramref name="room"/>,
    /// the last cut with an ellipsis; the box it took.
    /// </summary>
    public D2D_RECT_F Wrapped(string name, string text, float px, Weight weight, DXGI_RGBA ink, float x, float y, float room, int lines)
    {
        var height = PopupPaint.Wrapped(dc, Gfx, text, px, weight, ink, x, y, room, lines);
        if (height <= 0)
        {
            return new D2D_RECT_F(x, y, x, y);
        }
        var box = new D2D_RECT_F(x, y, x + room, y + height);
        Pieces.Add(new LinePiece(name, box, text));
        return box;
    }

    /// <summary>A glyph of the icon font, centred by its ink in <paramref name="box"/>.</summary>
    public void Glyph(string name, string glyph, float px, DXGI_RGBA ink, D2D_RECT_F box)
    {
        if (glyph.Length == 0)
        {
            return;
        }
        if (dc != null)
        {
            PopupPaint.Glyph(dc, Gfx, glyph, px, ink, box);
        }
        Pieces.Add(new LinePiece(name, box, glyph));
    }

    /// <summary>A plate behind something: the accent behind a lit glyph, a button's face.</summary>
    public void Plate(string name, D2D_RECT_F box, float radius, DXGI_RGBA ink)
    {
        if (dc != null)
        {
            PopupPaint.Fill(dc, box, radius, ink);
        }
        Pieces.Add(new LinePiece(name, box, Behind: true));
    }

    /// <summary>A card other pieces stand on: its fill, and its rim <paramref name="rim"/> px wide inside its edge.</summary>
    public void Card(string name, D2D_RECT_F box, float radius, DXGI_RGBA fill, DXGI_RGBA stroke, float rim)
    {
        if (dc != null)
        {
            PopupPaint.Fill(dc, box, radius, fill);
            if (rim > 0)
            {
                PopupPaint.Stroke(dc, box, radius, rim, stroke);
            }
        }
        Pieces.Add(new LinePiece(name, box, Behind: true));
    }

    /// <summary>
    /// A line's button (<see cref="LineButton"/>) in <paramref name="box"/>, written down as its plate
    /// — "<paramref name="name"/> plate", saying its style, where it has one — and as what it holds,
    /// <paramref name="name"/>, saying its word or its glyph.
    /// </summary>
    public void Button(string name, in LineButton sizes, FlyoutTheme theme, D2D_RECT_F box, string text, string? glyph,
        QuickSettings.Controls.ButtonStyle style, bool hot = false, bool pressed = false, bool enabled = true, bool dimmed = false)
    {
        if (box.right <= box.left)
        {
            return;
        }
        sizes.Paint(dc, Gfx, theme, box, text, glyph, style, hot, pressed, enabled: enabled, dimmed: dimmed);
        if (style is QuickSettings.Controls.ButtonStyle.Standard or QuickSettings.Controls.ButtonStyle.Accent || (hot && enabled))
        {
            Pieces.Add(new LinePiece(name + " plate", box, style.ToString(), Behind: true));
        }
        Pieces.Add(new LinePiece(name, box, text.Length > 0 ? text : glyph ?? ""));
    }

    /// <summary>A picture that draws itself into <paramref name="box"/>: a clock's face, a notification's card.</summary>
    public void Picture(string name, D2D_RECT_F box, PictureDraw draw)
    {
        if (dc != null)
        {
            draw(dc, box);
        }
        Pieces.Add(new LinePiece(name, box));
    }

    /// <summary>A piece somebody else drew or will draw, written down all the same.</summary>
    public void Placed(string name, D2D_RECT_F box, string text = "") => Pieces.Add(new LinePiece(name, box, text));
}

/// <summary>
/// The list's rows and cells, as a folder counts them down a line (FolderFlyout.CellPx, §442): a cell is half a row,
/// 20 DIP at 100 % and the list's text, a row two, and n of either is n times one, exactly.
/// </summary>
internal static class LineRows
{
    /// <summary>The folder's cell at <paramref name="style"/>'s scale and text size, px: half a row, on a whole px.</summary>
    public static float Cell(in TileStyle style) => FolderFlyout.CellPx(style.Scale, style.TextScale);

    /// <summary>How tall <paramref name="cells"/> cells are at <paramref name="style"/>'s scale and text size, in px: a row at the least.</summary>
    public static float Cells(int cells, in TileStyle style) => Math.Max(Karpo.Core.LineHeight.Fewest, cells) * Cell(style);

    /// <summary>How tall <paramref name="rows"/> rows of a list are at <paramref name="style"/>'s scale and text size, in px: two cells each.</summary>
    public static float Px(int rows, in TileStyle style) => Cells(Math.Max(1, rows) * Karpo.Core.LineHeight.Row, style);

    /// <summary>
    /// §432, §442: the whole cells <paramref name="px"/> of content takes, in px — what a form that spreads itself over
    /// its line (a clock's face, the slider's large level, a ring, a row of buttons) measures, so it fills the cells a
    /// sized line is given rather than standing short in them.
    /// </summary>
    public static float Whole(float px, in TileStyle style) => Cells(Karpo.Core.LineHeight.CellsFor(px, Cell(style)), style);

    /// <summary>
    /// The most a line of <paramref name="settings"/> may be, in px (§371, §388): the person's own cells, a
    /// Fill line's past any screen, never the room it was last given. What an automatic line measures
    /// its content at, so it is as tall as what it draws there.
    /// </summary>
    public static float Most(Karpo.Core.WidgetItem settings, in TileStyle style)
    {
        var height = Karpo.Core.LineHeight.For(settings);
        return Cells(height.Fill ? Karpo.Core.LineHeight.Most : height.Cells, style);
    }

    /// <summary>How many whole rows a line <paramref name="height"/> px tall is: one at the least.</summary>
    public static int In(float height, in TileStyle style) =>
        Math.Max(1, (int)MathF.Floor((height + 0.5f) / Px(1, style)));

    /// <summary>
    /// Where a block <paramref name="block"/> px tall starts in <paramref name="room"/> px from
    /// <paramref name="top"/>: <paramref name="pad"/> under its top — what a line shorter than the room
    /// keeps — or, where the room leaves less than that above and below, in its middle (the person,
    /// 2026-10-11: "if the widgets cannot fill the space available to them they should be top aligned").
    /// </summary>
    public static float Top(float top, float room, float block, float pad) =>
        PopupPaint.Round(top + Math.Clamp((room - block) / 2, 0, MathF.Max(0, pad)));

    /// <summary>A point's box, for a piece a hit test looks for.</summary>
    public static bool Inside(D2D_RECT_F box, Vector2 point) =>
        point.X >= box.left && point.X < box.right && point.Y >= box.top && point.Y < box.bottom;
}
