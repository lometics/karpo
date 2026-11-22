// A flyout's footer as a line of a list folder (docs\widgets.md §9, "Actions", §68;
// Karpo.Core\SettingsActions.cs).
//
// The person, 2026-10-09: the actions widget "replaces the buttons at the bottom of flyouts …
// Two slots: a left action and a right action". It was drawn first as Windows ends its Wi-Fi
// flyout, a hyperlink and an icon button; the person, 2026-10-10: it "should show both actions as
// text … in normal color and not primary color … one row height all the time". So both slots are
// subtle buttons, as Windows' flyouts end in theirs: the action's glyph and its name in the text
// colour, the subtle fill behind them under the pointer.
//
// The person, 2026-10-16: "the action buttons just look a little off … make sure that the design …
// is exactly the same as it is for the native taskbar flyouts". Windows' quick settings end in a
// footer band, measured: a darker band across the flyout (dark #1C1C1C on the body's #242424) under
// a 1 DIP hairline (#191919), its subtle buttons 12 DIP in from the flyout's sides and 6 DIP from the
// band's top and bottom, a 16 DIP glyph in each, the hover fill #0F white (#292929 on the band). So
// the line is that band, always: it is a flyout's footer wherever it stands, and at a list's end,
// where the presets put it, it is the folder's bottom edge as Windows' is, its corners the card's.
// One row tall (Windows' is 48 DIP; the line keeps its one row), and each button the glyph Settings
// gives its page beside the page's name (Windows' are glyph only; the names stay, at the person's
// 2026-10-10 asking). (No pressed fill: a line that took the press itself
// would have to declare a drag of its own (InteractionTests), and the click opens Settings, which
// closes the folder, at once.) The left button stands on the list's gutter, the right ends on the
// other, so neither fill reaches the card's edge (2026-10-11). Where
// the two names do not both fit, the shorter keeps all of its words if it takes no more than half
// and the other is cut with an ellipsis; else each has half (Split). A name cut short says itself
// whole in its tooltip. Either may be empty; with neither the line says so, quietly. The line is
// one row of the list, always (ListWidget.FixedHeight).
//
// Each is its own target and opens its page of Windows' Settings at once; Settings takes the focus,
// which closes the folder as it closes any flyout. With the keyboard on the line, Left and Right
// walk the two and Enter or Space opens the one ringed.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>
/// An actions line laid out: each slot's button, its glyph's square, where its words start (the
/// left's) or end (the right's), how wide they may be, and whether they are cut short; empty boxes
/// for empty slots. <paramref name="Bare"/>: no room for the names, so each button is its glyph
/// alone, as Windows' footer buttons are, its name in its tooltip.
/// </summary>
internal readonly record struct ActionsPlan(
    D2D_RECT_F Left, D2D_RECT_F LeftGlyph, float LeftText, float LeftRoom, bool LeftCut,
    D2D_RECT_F Right, D2D_RECT_F RightGlyph, float RightText, float RightRoom, bool RightCut,
    bool Bare = false);

internal sealed unsafe class ActionsTile : CellIsland, IListContent, ILinePieces
{
    /// <summary>The text button at the line's start, and the one at its end.</summary>
    internal const int LeftTarget = 1;

    internal const int RightTarget = 2;

    /// <summary>
    /// What opens a page, per thread: a test's, which writes the address down rather than opening
    /// Settings on the person's desktop; null for Windows' own.
    /// </summary>
    [ThreadStatic]
    private static Action<string>? opener;

    internal static Action<string>? Opener
    {
        get => opener;
        set => opener = value;
    }

    /// <summary>The link the calendar app opens by, or null where this PC has no calendar app (a test's: <see cref="CalendarApp.Stand"/>).</summary>
    private static string? CalendarAppLink() => global::Karpo.Dock.CalendarApp.Link();

    /// <summary><paramref name="action"/>, or nothing for the calendar app on a PC without one.</summary>
    private static SettingsAction? Shown(SettingsAction? action) =>
        action?.Id == SettingsActions.CalendarId && CalendarAppLink() is null ? null : action;

    private SettingsAction? left;
    private SettingsAction? right;

    // The target under the pointer; 0 for none. The keys' is the folder's ring (FolderFlyout.Keys.cs).
    private int hot;

    public ActionsTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    public override int IntervalMs => 0;

    protected override int Count => 1;

    /// <summary>The page the left button opens, and the one the right opens; null for an empty slot.</summary>
    internal (SettingsAction? Left, SettingsAction? Right) Chosen => (left, right);

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        left = Shown(SettingsActions.Find(island.Actions?.Left));
        right = Shown(SettingsActions.Find(island.Actions?.Right));
        Forget();
    }

    public override string? Tooltip(int index) => null;

    /// <summary>Either button opens its page; anywhere else does nothing.</summary>
    public override IslandAction Click(int index)
    {
        // Quick settings' pencil: the folder arranges itself where it stands (FolderFlyout.Activate).
        if ((index == LeftTarget ? left : index == RightTarget ? right : null)?.Id == SettingsActions.EditId)
        {
            return new IslandAction(View: IslandView.Arrange);
        }
        Open(index);
        return default;
    }

    /// <summary>Opens the page of target <paramref name="index"/>, where its slot has one.</summary>
    internal void Open(int index)
    {
        var action = index == LeftTarget ? left : index == RightTarget ? right : null;
        // The pencil is the folder's to do (Click): nothing opens for it.
        if (action is null || action.Id == SettingsActions.EditId)
        {
            return;
        }
        // desktops: Task view is no page or program but Win+Tab, as the taskbar's button presses it.
        if (action.Id == SettingsActions.TaskViewId)
        {
            Desktops.TaskView();
            return;
        }
        // The calendar app by the link Windows has an app for (outlookcal:, else ms-calendar:).
        var uri =action.Id == SettingsActions.CalendarId ? CalendarAppLink() ?? action.Uri : action.Uri;
        if (Opener is { } test)
        {
            test(uri);
            return;
        }
        _ = Launch.Open(uri);
    }

    protected override bool Sample(bool everything) => false;

    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a list's line; the bar has no actions line.");

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (!shown)
        {
            hot = 0;
        }
    }

    // --- Laid out -----------------------------------------------------------------------------------

    /// <summary>
    /// How wide each of two names may be in <paramref name="room"/>, given they are
    /// <paramref name="left"/> and <paramref name="right"/> wide: both whole where they fit; else the
    /// shorter whole where it takes no more than half, and the other the rest; else half each.
    /// </summary>
    internal static (float Left, float Right) Split(float left, float right, float room)
    {
        room = MathF.Max(0, room);
        if (left + right <= room)
        {
            return (left, right);
        }
        var half = room / 2;
        return left <= half ? (left, room - left)
            : right <= half ? (room - right, right)
            : (half, half);
    }

    /// <summary>
    /// The line in <paramref name="box"/>: each slot the lines' one button (LineButton, subtle) in the
    /// middle of the line — its glyph, then its words. The left button stands on the gutter, the right
    /// ends on the other (12 DIP from the card's sides, where Windows' gear stands from the flyout's),
    /// a button's gap between the two. A button is a control inside the card: its fill keeps the
    /// card's padding, never reaching the card's edge (the person, 2026-10-11: the buttons "directly
    /// touch" the border). Where a name would keep fewer than about two letters, every button is its
    /// glyph alone in a square (Bare). Pure but for measuring text.
    /// </summary>
    internal ActionsPlan Lay(D2D_RECT_F box, in TileStyle style)
    {
        var gutter = ListForm.Gutter(style);
        var buttons = LineButton.Of(Gfx, style);
        var px = buttons.TextPx;
        var control = buttons.Height;
        var top = box.top + PopupPaint.Round((box.bottom - box.top - control) / 2);
        var padX = buttons.Pad;
        var glyph = buttons.GlyphPx;
        var between = buttons.Gap;
        // What a button takes besides its words: its sides, and its glyph with the gap after it.
        var chrome = (2 * padX) + glyph + buttons.GlyphGap;
        var inner = MathF.Max(1, box.right - box.left - (2 * gutter));
        var leftWide = left is null ? 0 : PopupPaint.Measure(Gfx, left.Name, px, Weight.Regular).X;
        var rightWide = right is null ? 0 : PopupPaint.Measure(Gfx, right.Name, px, Weight.Regular).X;
        var (leftRoom, rightRoom) = left is not null && right is not null
            ? Split(leftWide, rightWide, inner - (2 * chrome) - between)
            : (left is null ? 0 : inner - chrome, right is null ? 0 : inner - chrome);
        var leftShown = MathF.Max(0, MathF.Min(leftWide, leftRoom));
        var rightShown = MathF.Max(0, MathF.Min(rightWide, rightRoom));
        var leftBox = left is null ? default : PopupPaint.Rect(box.left + gutter, top, leftShown + chrome, control);
        var rightBox = right is null ? default : PopupPaint.Rect(box.right - gutter - rightShown - chrome, top, rightShown + chrome, control);
        var glyphTop = top + PopupPaint.Round((control - glyph) / 2);
        // About two letters at the least, or the whole of a shorter name.
        if ((left is not null && leftRoom < MathF.Min(leftWide, 2 * px) - 0.5f) || (right is not null && rightRoom < MathF.Min(rightWide, 2 * px) - 0.5f))
        {
            // Square, but never more than half the line each (a narrow line at a large text size),
            // the glyph no wider than its button.
            var wide = MathF.Floor(MathF.Min(control, left is not null && right is not null ? (inner - between) / 2 : inner));
            var mark = MathF.Min(glyph, wide);
            var inset = PopupPaint.Round((wide - mark) / 2);
            var markTop = top + PopupPaint.Round((control - mark) / 2);
            var bareLeft = left is null ? default : PopupPaint.Rect(box.left + gutter, top, wide, control);
            var bareRight = right is null ? default : PopupPaint.Rect(box.right - gutter - wide, top, wide, control);
            return new ActionsPlan(
                bareLeft, left is null ? default : PopupPaint.Rect(bareLeft.left + inset, markTop, mark, mark), bareLeft.right, 1, left is not null,
                bareRight, right is null ? default : PopupPaint.Rect(bareRight.left + inset, markTop, mark, mark), bareRight.right, 1, right is not null,
                Bare: true);
        }
        var leftGlyph = left is null ? default : PopupPaint.Rect(leftBox.left + padX, glyphTop, glyph, glyph);
        var rightGlyph = right is null ? default : PopupPaint.Rect(rightBox.left + padX, glyphTop, glyph, glyph);
        return new ActionsPlan(
            leftBox, leftGlyph, leftGlyph.right + buttons.GlyphGap, MathF.Max(1, leftRoom), leftWide > leftRoom + 0.5f,
            rightBox, rightGlyph, box.right - gutter - padX, MathF.Max(1, rightRoom), rightWide > rightRoom + 0.5f);
    }

    /// <summary>A list's row; the line is one row whatever this says (ListWidget.FixedHeight).</summary>
    public float ListHeight(float width, in TileStyle style) => PopupPaint.Round(style.Text(40));

    // --- Drawn -----------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var theme = QuickTheme.For(style.Theme);
        var px = style.Text(14);
        var gutter = ListForm.Gutter(style);
        var line = TilePaint.LineHeight(Gfx, px);
        // The footer band across the line under its hairline, as Windows' quick settings end: each a
        // shade of the body, side by side, never one over the other.
        // A ground, as a graph behind a line's words is: a test reading the words leaves it out
        // (PaintWatch.NoBackdrops).
        if (!PaintWatch.NoBackdrops)
        {
            var hair = MathF.Max(1, PopupPaint.Round(style.Scale));
            PopupPaint.Divider(dc, box.left, box.right, box.top, hair, theme.FooterLine);
            PopupPaint.Fill(dc, box with { top = box.top + hair }, 0, theme.Footer);
        }
        if (left is null && right is null)
        {
            TilePaint.Line(dc, Gfx, S.WidgetActionsNone, px, Weight.Regular, theme.TextTertiary, box.left + gutter,
                box.top + PopupPaint.Round((box.bottom - box.top - line) / 2), MathF.Max(1, box.right - box.left - (2 * gutter)));
            return;
        }
        var plan = Lay(box, style);
        // The lines' one button, subtle: its glyph, then its name cut to the room it was laid out with.
        var buttons = LineButton.Of(Gfx, style);
        if (left is not null)
        {
            buttons.Paint(dc, Gfx, style.Theme, plan.Left, plan.Bare ? "" : left.Name, left.Glyph, ButtonStyle.Subtle, hot == LeftTarget);
        }
        if (right is not null)
        {
            buttons.Paint(dc, Gfx, style.Theme, plan.Right, plan.Bare ? "" : right.Name, right.Glyph, ButtonStyle.Subtle, hot == RightTarget);
        }
    }

    /// <summary>The button at <paramref name="point"/>, in the lines' reach round it (LineButton.Hit); 0 for the rest of the line.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box, style);
        var buttons = LineButton.Of(Gfx, style);
        return left is not null && LineRows.Inside(buttons.Hit(plan.Left, box.top, box.bottom), point) ? LeftTarget
            : right is not null && LineRows.Inside(buttons.Hit(plan.Right, box.top, box.bottom), point) ? RightTarget
            : 0;
    }


    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var at = point is { } where ? CardTarget(where, box, style, surface) : 0;
        if (at != hot)
        {
            hot = at;
            Redraw(Gfx);
        }
    }

    /// <summary>The buttons light themselves; the line does not light as well.</summary>
    protected internal override bool LightsItsRows => true;

    /// <summary>A name cut short says itself whole in its button's tooltip; a whole one needs none.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box, style);
        return CardTarget(point, box, style, surface) switch
        {
            LeftTarget when plan.LeftCut => (left!.Name, plan.Left),
            RightTarget when plan.RightCut => (right!.Name, plan.Right),
            _ => null,
        };
    }

    // --- As pieces ----------------------------------------------------------------------------------

    public string MainPiece => "left";

    /// <summary>Each button and the whole name of its page, cut short on the line or not.</summary>
    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(box, style);
        var pieces = new List<LinePiece>();
        if (left is not null)
        {
            pieces.Add(new LinePiece("left", plan.Left, left.Name));
        }
        if (right is not null)
        {
            pieces.Add(new LinePiece("right", plan.Right, right.Name));
        }
        return pieces;
    }
}
