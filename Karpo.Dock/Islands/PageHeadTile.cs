// The head of a line's page in an open folder (Karpo.Core\FolderPages.cs, FolderIsland.Pages.cs): the
// back arrow and the page's title, as Windows' quick settings head the page a tile's ">" opens —
// "< Sound output", "< Accessibility". A page whose first line is a quick settings page has that
// page's own title row, the back arrow put before it (QuickPageTile.Back); this is for the rest.
//
// It is a heading's line (the folder never lights it, FolderFlyout.Quiet), as tall as a quick
// settings page's title row, and all of it the way back (BackHead, 2026-10-30, the person: "i want the
// whole header area to be the back button"): lit under the pointer in one plate and pressed, and a
// click anywhere on it the page's back. Made by the folder for its page alone; never in a config.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe class PageHeadTile : CellIsland, IListContent
{
    /// <summary>The way back's target: the whole line.</summary>
    internal const int BackTarget = 1;

    /// <summary>A quick settings page's title row, in DIPs at 100 % text (PageLayout.Title).</summary>
    internal const float RowDip = 52;

    private readonly BackHead head;
    private string text = "";
    private bool hot;
    private bool keyed;
    private RowPress press;

    public PageHeadTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        StandIn(ListWidgets.Of(ListKind.Heading));
        head = new BackHead(() => Back?.Invoke());
        Configure(island, config);
    }

    /// <summary>What the line does, the way back: the folder's lines again.</summary>
    internal Action? Back { get; init; }

    /// <summary>The page's title.</summary>
    internal string Text => text;

    /// <summary>Whether the way back is lit under the pointer, for tests.</summary>
    internal bool Lit => hot;

    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        text = island.Heading?.Text ?? "";
        head.Title = text;
        Forget();
    }

    public override string? Tooltip(int index) => null;

    public override IslandAction Click(int index)
    {
        press.Up();
        if (index == BackTarget)
        {
            Back?.Invoke();
        }
        return default;
    }

    protected override bool Sample(bool everything) => false;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException("a page's head is a line of an open folder alone.");

    /// <summary>As tall as a quick settings page's title row.</summary>
    public float ListHeight(float width, in TileStyle style) => PopupPaint.Round(style.Text(RowDip));

    /// <summary>
    /// Where the way back, its arrow and the title go in <paramref name="box"/>: the line its target, lit edge to edge
    /// as a list's row is (LineItem), the arrow on the gutter as a quick settings page's title row has it
    /// (PageLayout.Title), the title after it.
    /// </summary>
    internal (D2D_RECT_F Arrow, D2D_RECT_F Title, float Px) LineOf(D2D_RECT_F box, in TileStyle style)
    {
        var height = box.bottom - box.top;
        var row = MathF.Min(height, ListHeight(box.right - box.left, style));
        var side = MathF.Min(row, style.Px(36));
        var gutter = ListForm.Gutter(style);
        var arrow = PopupPaint.Rect(box.left + MathF.Max(0, gutter - style.Px(10)), box.top + PopupPaint.Round((row - side) / 2), side, side);
        head.Place(box, box with { bottom = box.top + row }, arrow);
        var px = style.Text(14);
        var line = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        var titleLeft = arrow.right + style.Px(4);
        var title = PopupPaint.Rect(titleLeft, box.top + PopupPaint.Round((row - line) / 2), MathF.Max(1, box.right - gutter - titleLeft), line);
        return (arrow, title, px);
    }

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var (_, title, px) = LineOf(box, style);
        var context = new QuickContext { Gfx = Gfx, Theme = QuickTheme.For(style.Theme), Scale = style.Scale, TextScale = style.TextScale };
        head.Hot = hot;
        head.Pressed = press.On(BackTarget);
        head.Focused = keyed;
        head.FocusVisible = keyed;
        TilePaint.Placed(dc, box.left, box.top, 1, () => head.Paint(dc, context));
        TilePaint.Line(dc, Gfx, text, px, Weight.Semibold, style.Theme.Text, title.left, title.top, TilePaint.Width(title));
    }

    /// <summary>All of it is the way back.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        LineRows.Inside(box, point) ? BackTarget : 0;

    /// <summary>The way back's arrow says "Back", as a menu's level's head does.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var (arrow, _, _) = LineOf(box, style);
        return LineRows.Inside(arrow, point) ? (head.Name, arrow) : null;
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var over = point is { } at && CardTarget(at, box, style, surface) == BackTarget;
        var changed = press.Over(over ? BackTarget : 0);
        if (point is not null && keyed)
        {
            keyed = false;
            changed = true;
        }
        if (over != hot || changed)
        {
            hot = over;
            Redraw(Gfx);
        }
    }

    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            Redraw(Gfx);
        }
    }

    /// <summary>The way back lights itself, its plate a list row's; the folder does not light the line.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>Enter or Space with the keys on the line goes back.</summary>
    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        if (key is VK.VK_RETURN or VK.VK_SPACE)
        {
            Back?.Invoke();
            return true;
        }
        if (!keyed && key is VK.VK_LEFT or VK.VK_RIGHT)
        {
            keyed = true;
            Redraw(Gfx);
            return true;
        }
        return false;
    }

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = keyed = false;
            press.Up();
        }
    }
}
