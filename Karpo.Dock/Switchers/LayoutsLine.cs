// layouts: the taskbar layouts as a line of a folder's list (docs\widgets.md §127). It was the layout
// switcher's flyout until the switcher became the layouts preset (2026-10-11: "all the remaining
// flyouts should become folders with a face now"): a folder wearing the name of the layout this bar
// shows, holding this line, which does what the flyout did.
//
// **A row per layout**, in the config's order: its own glyph and its name, the one this bar shows
// marked as WinUI marks a list's chosen item — the subtle fill, the accent pill at its start and its
// name semibold. The rows are the bar's own right-click menu's (Dock.LayoutRows, Switchers.Layouts):
// the same layouts, the same order (§561: the menu shows a lone one too) — below two there is nothing to choose:
// where there is one, the line is the kit's one Status saying so, with a standard "Add" (§404) that, once
// the folder has gone, opens Display ▾ at "Add a layout", the layout presets' one picker (TakeAdd; §485:
// till then it made Karpo's as "New layout").
//
// **A click switches.** A row's click (or Enter on it) is the folder's act (CellAct.Action,
// IslandView.Layouts): the folder closes, and the dock switches this display to the layout chosen
// (TakeChoice, Dock.SwitchLayout) with the very method the bar's menu runs — after the folder has
// gone, since the switch builds the bar the folder stood on again. A click on the one shown does
// nothing and leaves the folder open.
//
// **What it reads.** The config it was handed and the bar it stands on (its key's owner); a switch,
// a rename or a new layout builds the bar again, and with it this line. Its height follows how many
// there are (IListContent); held shorter, they scroll (ListScroll.cs).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal sealed unsafe class LayoutsLine : CellIsland, IListContent, IListScroll, ILinePieces
{
    private Config config;

    // What is under the pointer and the number the folder was given for it. The keys' row is the folder's (FolderFlyout.Keys.cs).
    private int hot;
    private RowPress press;

    // The layout a click chose, by its place in the config's list, for the dock once the folder has gone; or
    // "Add", a layout to make (§404).
    private int? chosen;
    private bool adding;

    /// <summary>The number the folder carries for the Status's "Add" with one layout (no row has a number then).</summary>
    private const int AddPart = 1;

    /// <summary>The Status's one action with one layout (§404): a standard "Add".</summary>
    private static readonly (string Text, string? Glyph)[] Add = [(S.SettingsCommonAdd, null)];

    public LayoutsLine(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        this.config = config;
        Configure(island, config);
    }

    /// <summary>Nothing ticks: a layout change is a config change, which builds the line again.</summary>
    public override int IntervalMs => 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        this.config = config;
        Forget();
    }

    public override string? Tooltip(int index) => null;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list; on the bar it is the layouts preset's face.");

    /// <summary>Nothing is read: the rows are the config's, which builds the line again when it changes.</summary>
    protected override bool Sample(bool everything) => false;

    /// <summary>Rows light themselves; the line does not light as a whole.</summary>
    protected internal override bool LightsItsRows => true;

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = 0;
            _ = press.Up();
        }
    }

    // --- What it shows ------------------------------------------------------------------------------

    /// <summary>
    /// The layouts as rows, the one the bar this line stands on shows marked: none where there are
    /// fewer than two (Switchers.Layouts; §561 the bar's menu shows a lone one).
    /// </summary>
    internal IReadOnlyList<SwitchRow> Rows()
    {
        var shown = config.LayoutOf(Islands.OwnerOf(Key))?.Id ?? "";
        return Switchers.Layouts(Dock.LayoutRows(config.Layouts, shown));
    }

    /// <summary>A row: 40 DIP, as every list's row.</summary>
    private static float RowHeight(in TileStyle style) => PopupPaint.Round(40 * style.Scale * style.TextScale);

    /// <summary>
    /// How tall all the rows are, from the line's top to its foot with no air over the first or under the last, whose
    /// bands are the line's edges (LineItem); one of the list's rows, its Status, where there are none (§401).
    /// </summary>
    private float Natural(in TileStyle style) => Rows().Count is var count and > 0 ? count * RowHeight(style) : LineRows.Px(1, style);

    public float ListHeight(float width, in TileStyle style) => Natural(style);

    /// <summary>All of them, where the line has room for a row: the rest scrolls (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        height + 0.5f < RowHeight(style) ? 0 : Natural(style);

    /// <summary>Row <paramref name="index"/>'s box in <paramref name="box"/>.</summary>
    private D2D_RECT_F RowBox(D2D_RECT_F box, int index, in TileStyle style)
    {
        var height = RowHeight(style);
        var top = box.top + (index * height);
        return new D2D_RECT_F(box.left, top, box.right, top + height);
    }

    /// <summary>One layout: where the Status's words and its "Add" stand (§404).</summary>
    private LineStatus.Laid Status(D2D_RECT_F box, in TileStyle style) =>
        LineStatus.Lay(Gfx, box, style, glyph: true, S.DockSwitcherOnlyLayout, Add);

    /// <summary>The keys' ring round a row: its row a little in from the line's edges, so the line's clip leaves all of it.</summary>
    private static D2D_RECT_F PlateOf(D2D_RECT_F row, in TileStyle style) =>
        new(row.left + style.Px(4), row.top + style.Px(2), row.right - style.Px(4), row.bottom - style.Px(2));

    // --- Clicks -------------------------------------------------------------------------------------

    /// <summary>The number the folder carries for the row at <paramref name="point"/>: its place plus one, 0 for none.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var rows = Rows();
        if (rows.Count == 0)
        {
            return Status(box, style).Button is { } button && LineRows.Inside(LineButton.Of(Gfx, style).Hit(button, box.top, box.bottom), point) ? AddPart : 0;
        }
        for (var index = 0; index < rows.Count; index++)
        {
            if (LineRows.Inside(RowBox(box, index, style), point))
            {
                return index + 1;
            }
        }
        return 0;
    }

    /// <summary>
    /// What the click the hit test found does; with no part (the keys: Enter or Space) the row the
    /// keys are on. Another layout's row switches to it once the folder has gone; the one shown, or
    /// nothing, does nothing.
    /// </summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        var at = index - 1;
        var rows = Rows();
        if (rows.Count == 0 && index == AddPart)
        {
            // §404: one layout, its Status's "Add": the dock makes one once the folder has gone (TakeAdd).
            adding = true;
            return new IslandAction(View: IslandView.Layouts);
        }
        if (at < 0 || at >= rows.Count || rows[at].Current)
        {
            Redrawn();
            return default;
        }
        // A row per layout, in the config's order (Dock.LayoutRows): its place is the layout's.
        chosen = at;
        return new IslandAction(View: IslandView.Layouts);
    }

    /// <summary>
    /// The dock, once the folder has gone: the layout a click chose, by its place in the config's
    /// list, where one waits — asked once, and forgotten as it is told.
    /// </summary>
    internal int? TakeChoice()
    {
        var to = chosen;
        chosen = null;
        return to;
    }

    /// <summary>
    /// The dock, once the folder has gone (§404): whether the Status's "Add" was clicked, so Display ▾ opens at "Add a
    /// layout" (§485, EditEntry.AddLayout) — asked once, and forgotten as it is told.
    /// </summary>
    internal bool TakeAdd()
    {
        var was = adding;
        adding = false;
        return was;
    }

    // --- The pointer --------------------------------------------------------------------------------

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at ? CardTarget(at, box, style, surface) : 0;
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>A press on a row lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    // --- Drawn --------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var theme = QuickTheme.For(style.Theme);
        var form = ListForm.Of(Gfx, style);
        var items = ItemForm.For(Gfx, style, Settings);
        var rows = Rows();
        if (rows.Count == 0)
        {
            // One layout: nothing to choose — the kit's one Status (§388) —, and "Add" to make another (§404).
            var lit = hot == AddPart;
            LineStatus.Draw(new LineSketch(dc, Gfx), box, style, Info.Glyph, S.DockSwitcherOnlyLayout, Add, 0, lit, lit && press.On(AddPart));
            return;
        }
        for (var index = 0; index < rows.Count; index++)
        {
            PaintRow(dc, RowBox(box, index, style), rows[index], index, form, items, style, theme);
        }
    }

    /// <summary>A layout's row: its plate (the shown one's fill and pill, the lit one's), its glyph and its name.</summary>
    private void PaintRow(ID2D1DeviceContext* dc, D2D_RECT_F at, SwitchRow row, int index, in ListForm form, in ItemForm items, in TileStyle style, QuickTheme theme)
    {
        // The row edge to edge, each the next's edge (LineItem): the shown one's fill and pill, the lit one's.
        var lit = hot == index + 1;
        if (row.Current)
        {
            LineItem.Chosen(dc, at, theme, lit);
            LineItem.Pill(dc, at, style, theme.Accent);
        }
        else if (lit)
        {
            LineItem.Light(dc, at, theme, press.On(index + 1));
        }
        var height = at.bottom - at.top;
        PopupPaint.Glyph(dc, Gfx, row.Glyph, form.Glyph, theme.Text, form.IconBox(at.left, at.top, height));
        var px = items.TextPx;
        var weight = row.Current ? Weight.Semibold : Weight.Regular;
        var left = at.left + form.TextLeft;
        TilePaint.Line(dc, Gfx, row.Name, px, weight, theme.Text, left, at.top + PopupPaint.Round((height - TilePaint.LineHeight(Gfx, px, weight)) / 2),
            MathF.Max(1, at.right - form.Inset - left));
    }

    // --- Words for the pointer and the screen reader ------------------------------------------------

    /// <summary>A name cut at the row's end, whole.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var rows = Rows();
        if (rows.Count == 0 || CardTarget(point, box, style, surface) is not (> 0 and var target))
        {
            return null;
        }
        var row = rows[target - 1];
        var at = RowBox(box, target - 1, style);
        var form = ListForm.Of(Gfx, style);
        var room = at.right - form.Inset - (at.left + form.TextLeft);
        var weight = row.Current ? Weight.Semibold : Weight.Regular;
        return PopupPaint.Measure(Gfx, row.Name, ItemForm.For(Gfx, style, Settings).TextPx, weight).X > room ? (row.Name, at) : null;
    }

    /// <summary>What a line of one row shows first: the first layout's name.</summary>
    public string MainPiece => "layout 0";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var rows = Rows();
        if (rows.Count == 0)
        {
            var status = Status(box, style);
            return
            [
                new LinePiece("only", status.Words ?? RowBox(box, 0, style), S.DockSwitcherOnlyLayout),
                .. status.Button is { } button ? [new LinePiece("add", button, S.SettingsCommonAdd)] : Array.Empty<LinePiece>(),
            ];
        }
        var pieces = new List<LinePiece>();
        for (var index = 0; index < rows.Count; index++)
        {
            var at = RowBox(box, index, style);
            pieces.Add(new LinePiece($"layout {index}", at, rows[index].Name));
            if (rows[index].Current)
            {
                pieces.Add(new LinePiece("current", at, rows[index].Name));
            }
        }
        return pieces;
    }
}
