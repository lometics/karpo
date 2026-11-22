// A shell: a kind that is in its registry before its drawing is built (WidgetSource.Shell).
//
// The person asked for these first, ahead of what they do: "go ahead and implement just the shells
// for these even if there is no functionality yet" (2026-09-26). So a shell is a registry member
// and the plainest drawing that says which kind it is — its glyph and its title, centred in the
// room it is given — and nothing else: a click does nothing, nothing opens, nothing is read and
// nothing ticks. One class for every shell on every surface (the taskbar, a list's line), so
// building a kind's real drawing is its registry record naming another source,
// and this class is not touched.
//
// The room decides how much of it shows, never the other way round: on the bar the title stands
// beside the glyph only where it fits whole, and the glyph is drawn alone otherwise. A list's line
// keeps the list's one gutter (docs\widgets.md §9, "One
// gutter"): the glyph in the icon column the gutter starts, the title where every row's name
// begins.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class ShellIsland : CellIsland
{
    public ShellIsland(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
    }

    /// <summary>Nothing it shows changes.</summary>
    public override int IntervalMs => 0;

    /// <summary>
    /// One cell, as the taskbar measures it (WidgetShapes.Shell). A shell of a kind the taskbar has
    /// no entry for at all has none: its line is all it is (<see cref="CellIsland.CardOnly"/>), and
    /// it has no length along a bar to answer.
    /// </summary>
    protected override int Count => CardOnly && BarEntries.Find(Info.Id) is null ? 0 : 1;

    public override void Configure(WidgetItem island, Config config) => Settings = island;

    /// <summary>Its title: all a shell has to say.</summary>
    public override string? Tooltip(int index) => Info.Title;

    /// <summary>Nothing: a shell does nothing until its kind is built.</summary>
    public override IslandAction Click(int index) => default;

    protected override bool Sample(bool everything) => false;

    /// <summary>
    /// The taskbar's cell: the glyph and the title beside it, centred, where the cell has room for
    /// the whole title (its shape is WidgetShapes.Shell); the glyph alone, centred, where it has
    /// not — a narrower width somebody set, or a vertical bar.
    /// </summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var fit = new Fit(style);
        var ink = new IslandInk(style.Look);
        var glyph = fit.Glyph();
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        var title = style.Vertical ? 0 : TextLine.Template(Gfx, Info.Title, fit.Primary, Weight.Regular).Width;
        var whole = glyph + fit.Gap + title;
        if (style.Vertical || whole > width - (2 * fit.Edge))
        {
            PopupPaint.Glyph(dc, Gfx, Info.Glyph, glyph, ink.Text, PopupPaint.Rect((width - glyph) / 2, (height - glyph) / 2, glyph, glyph));
            return;
        }
        var left = MathF.Round((width - whole) / 2);
        PopupPaint.Glyph(dc, Gfx, Info.Glyph, glyph, ink.Text, PopupPaint.Rect(left, (height - glyph) / 2, glyph, glyph));
        var text = TextLine.Lay(Gfx, Info.Title, fit.Primary, Weight.Regular, MathF.Max(1, title));
        try
        {
            ID2D1SolidColorBrush* brush = null;
            try
            {
                brush = Gfx.Brush(dc, ink.Text);
                text.Draw(dc, brush, ink.Text, left + glyph + fit.Gap, (height - text.Height) / 2);
            }
            finally
            {
                ComUtil.Release(ref brush);
            }
        }
        finally
        {
            text.Release();
        }
    }

    /// <summary>A list's line.</summary>
    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        Line(dc, box, style);

    /// <summary>
    /// A line of a list, on the list's one gutter: the glyph in the icon column the gutter starts,
    /// the title where every row's name begins, the two centred down the line however tall it is.
    /// </summary>
    private void Line(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var width = TilePaint.Width(box);
        var height = box.bottom - box.top;
        var row = MathF.Max(form.Icon, form.Line);
        var top = box.top + MathF.Max(0, PopupPaint.Round((height - row) / 2));
        PopupPaint.Glyph(dc, Gfx, Info.Glyph, form.Glyph, theme.Text, form.IconBox(box.left, top, row));
        TilePaint.Line(dc, Gfx, Info.Title, form.TextPx, Weight.Regular, theme.Text,
            box.left + form.TextLeft, top + PopupPaint.Round((row - form.Line) / 2), form.Room(width));
    }
}
