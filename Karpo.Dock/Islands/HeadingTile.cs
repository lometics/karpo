// A heading in a list folder: a title over the lines under it, which groups them (backlog 29).
//
// The person: "a title line that groups the lines under it. One level only, list view only. Its text
// is set in its settings. It looks like Windows 11's section headings; take Settings' group headers
// as the model." Windows' Settings heads a group of cards with a line of BodyStrong text — 14 DIP,
// semibold, 20 DIP a line, in the primary text colour — standing on the cards' own left edge, with
// more air above it than under it, so it belongs to what follows. A heading here is that: its words
// on the list's one gutter (ListForm.Gutter, where a line's plate begins), AboveDip under the line's
// top and BelowDip over its foot, and the line that tall and no taller (Height): its own height, not
// a row's, and not one anybody sets (the person, 2026-10-11: "the heading should not have a
// configurable size, it does not even have to conform to the 1cell height, it can have its own size
// actually"). The air over it is what sets the group apart from the one above, as Settings draws no
// line between groups: the space is the divider.
//
// One level: a heading heads the lines down to the next heading, and nothing heads a heading. It is
// a line of its own and does nothing when clicked, so it never lights under the pointer either
// (FolderFlyout.Quiet). A heading nobody has named says its kind's name, "Heading", in the ink any
// heading's words are, and asks for nothing (the person, 2026-10-11: "the heading should not show a
// setup button but should have a default value shown"); its words are set on its page.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class HeadingTile : CellIsland
{
    /// <summary>The air over a heading's words, in DIPs: what sets its group apart from the one above.</summary>
    internal const float AboveDip = 8;

    /// <summary>Its words' line, in DIPs at a text scale of 1: BodyStrong's, 14 DIP text on a 20 DIP line.</summary>
    internal const float WordsDip = 20;

    /// <summary>The air under its words, in DIPs, less than over them: it belongs to what follows.</summary>
    internal const float BelowDip = 4;

    private string text = "";

    public HeadingTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>
    /// How tall a heading's line is at <paramref name="scale"/> and <paramref name="textScale"/>, in
    /// whole px: the air over its words, their line and the air under them. The list's layout's and
    /// the drawing's one number (FolderFlyout.Build, <see cref="LineOf"/>).
    /// </summary>
    internal static float Height(float scale, float textScale) =>
        PopupPaint.Round(AboveDip * scale) + PopupPaint.Round(WordsDip * scale * textScale) + PopupPaint.Round(BelowDip * scale);

    /// <summary>Nothing it shows changes but its words, which come with its settings.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>What it says: its own words, or its kind's name where it has none.</summary>
    internal string Text => text.Length > 0 ? text : Info.Title;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        text = island.Heading?.Text ?? "";
        Forget();
    }

    public override string? Tooltip(int index) => null;

    /// <summary>A heading is not a thing to press: a click on it does nothing, and the folder stays as it is.</summary>
    public override IslandAction Click(int index) => default;

    protected override bool Sample(bool everything) => false;

    /// <summary>Only a list draws it, as its line; this is its words should anything else ask.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var ink = new IslandInk(style.Look);
        var fit = new Fit(style);
        var said = Text;
        var size = PopupPaint.Measure(Gfx, said, fit.Primary, Weight.Semibold, width);
        PopupPaint.Text(dc, Gfx, said, fit.Primary, Weight.Semibold, ink.Text, 0, MathF.Round((height - size.Y) / 2), width);
    }

    /// <summary>
    /// Where the words go on a line <paramref name="box"/>: on the list's gutter, <see cref="AboveDip"/>
    /// down, in the middle of their <see cref="WordsDip"/> line, and cut with an ellipsis at the other
    /// gutter; on a line shorter than a heading's own (<see cref="Height"/>), whole and in its middle.
    /// The drawing's and a test's one question.
    /// </summary>
    internal (string Text, float Px, D2D_RECT_F Box) LineOf(D2D_RECT_F box, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var px = style.Text(14);
        var line = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        var slot = PopupPaint.Round(WordsDip * style.Scale * style.TextScale);
        var height = box.bottom - box.top;
        var top = height + 0.5f >= Height(style.Scale, style.TextScale)
            ? box.top + style.Px(AboveDip) + ((slot - line) / 2)
            : box.top + ((height - line) / 2);
        var said = Text;
        // Its first letter's ink on the gutter, as a group header's stands on its cards' edge: a large
        // letter's side bearing would otherwise stand it a pixel or two in.
        var bearing = MathF.Max(0, MathF.Round(InkText.Ink(Gfx, said[..1], px, Weight.Semibold).Left));
        return (said, px, PopupPaint.Rect(box.left + gutter - bearing, MathF.Round(top), MathF.Max(1, box.right - box.left - (2 * gutter) + bearing), line));
    }

    /// <summary>The heading's words on its line, in the primary text's ink and the body's strong weight.</summary>
    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var (said, px, at) = LineOf(box, style);
        TilePaint.Line(dc, Gfx, said, px, Weight.Semibold, style.Theme.Text, at.left, at.top, at.right - at.left);
    }
}
