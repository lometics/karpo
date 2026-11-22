// A widget's line in a list folder, where the line is the widget's own drawing: its list form
// (docs\widgets.md §9, "List forms").
//
// A list's rows keep one set of edges (FolderFlyout.RowInset, RowIcon, RowText): one leading
// column for whatever stands first — a pin's icon, a kind's glyph — every name beginning where the
// next row's does, and the same air at the trailing edge. A list form is a row with more to say,
// so it keeps those edges and grows downwards: its first row of text stands exactly where the
// list's own row puts its name, and whatever else it has goes under it, in the same column.
//
// This is the frame and nothing more. What a form draws in it is its subject's own — the pieces
// its card and its flyout already paint — so there is one drawing of a battery's charge or a
// graph, not one for the card and a second for the line.
//
// **One gutter for every line** (docs\widgets.md §9, "One gutter"): the list's row inset, the air
// its own rows keep at each edge (Gutter). Whatever a line draws at rest stands between its two
// gutters — its words, a glyph's column, a bar, a card's or a chosen row's plate, a button's box
// — and whatever leads or ends it stands on them, so the text of one line starts where the next
// line's does, whoever drew either. A panel's region re-housed on a line — the weather's header, a
// drive's, a stock's — keeps its own gutter inside itself and is placed so that
// gutter lands on the list's (Housed): the region is drawn as it is, only moved. The one thing a
// line lets past its gutters is a graph, which runs the line's whole width, edge to edge, with
// its words still on the gutter: a graph's picture is the width it has (docs\widgets.md §9).

using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The edges and sizes a list form lays itself out on, for one style (<see cref="Of"/>).</summary>
internal readonly record struct ListForm
{
    /// <summary>The list's text size: its rows' names, and a form's ordinary lines.</summary>
    public required float TextPx { get; init; }

    /// <summary>A form's small lines: a detail, a caption, a figure under a bar.</summary>
    public required float SmallPx { get; init; }

    /// <summary>The air at a line's leading and trailing edge: the list's one gutter (<see cref="Gutter"/>).</summary>
    public required float Inset { get; init; }

    /// <summary>The leading column's width, where the glyph or the picture stands (<see cref="FolderFlyout.RowIcon"/>).</summary>
    public required float Icon { get; init; }

    /// <summary>A kind's glyph in that column: the size the list's own row draws it at, so the glyphs of a list line up.</summary>
    public required float Glyph { get; init; }

    /// <summary>Where the text begins, from the line's leading edge (<see cref="FolderFlyout.RowText"/>).</summary>
    public required float TextLeft { get; init; }

    /// <summary>
    /// The air above the first row and below the last: what puts a single row of text at the height
    /// the list's own row puts its name, so a form one row tall is exactly a row of the list.
    /// </summary>
    public required float Pad { get; init; }

    /// <summary>Between two rows of a form: a figure and the bar under it, a bar and its caption.</summary>
    public required float Gap { get; init; }

    /// <summary>A row of the list's text, measured.</summary>
    public required float Line { get; init; }

    /// <summary>A small line, measured.</summary>
    public required float SmallLine { get; init; }

    public static ListForm Of(Gfx gfx, in TileStyle style)
    {
        var textPx = style.Text(14);
        var line = TilePaint.LineHeight(gfx, textPx);
        var row = PopupPaint.Round(40 * style.Scale * style.TextScale);
        return new ListForm
        {
            TextPx = textPx,
            SmallPx = style.Text(12),
            Inset = Gutter(style),
            Icon = FolderFlyout.RowIcon(textPx),
            Glyph = PopupPaint.Round(textPx * 16f / 14f),
            TextLeft = FolderFlyout.RowText(textPx),
            Pad = MathF.Max(0, PopupPaint.Round((row - line) / 2)),
            Gap = style.Px(6),
            Line = line,
            SmallLine = TilePaint.LineHeight(gfx, style.Text(12)),
        };
    }

    /// <summary>
    /// The list's one side gutter for a line drawn in <paramref name="style"/>, in px: the air its
    /// own rows keep at each edge (<see cref="FolderFlyout.RowInset"/>), which every line's content
    /// stands between. A line that builds no form asks here, so there is one answer.
    /// </summary>
    public static float Gutter(in TileStyle style) => FolderFlyout.RowInset(style.Text(14));

    /// <summary>
    /// Where a panel's control goes on a line <paramref name="width"/> px wide, so that the gutter
    /// it keeps inside itself, <paramref name="own"/> px from each of its edges, lands on the list's
    /// <paramref name="gutter"/>: moved out by the difference and as much wider at each side, or in
    /// and narrower where its own is the smaller. The control is drawn as it is; only its box moves.
    /// </summary>
    public static D2D_RECT_F Housed(float gutter, float own, float top, float width, float height) =>
        PopupPaint.Rect(gutter - own, top, MathF.Max(1, width - (2 * (gutter - own))), height);

    /// <summary>The text's room on a line <paramref name="width"/> px wide: from where it begins to the trailing air.</summary>
    public float Room(float width) => MathF.Max(1, width - TextLeft - Inset);

    /// <summary>
    /// The leading column's square, centred on a row that starts at <paramref name="top"/> and is
    /// <paramref name="height"/> px tall, in a line whose box starts at <paramref name="left"/>.
    /// </summary>
    public D2D_RECT_F IconBox(float left, float top, float height) =>
        PopupPaint.Rect(left + Inset, top + PopupPaint.Round((height - Icon) / 2), Icon, Icon);
}
