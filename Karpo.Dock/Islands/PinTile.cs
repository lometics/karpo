// A pin inside a folder.
//
// On the bar the scene draws pins itself, as the taskbar's app buttons; inside
// a folder a pin is a widget like the others, so it is an island of its own
// here. It draws its app's icon, its name where there is room, and the same
// running indicator and badge the bar shows, and a click on it does what a click on the
// bar's own pin does: the dock asks the one rule (AppClicks) and this island
// only says that an app was clicked (IslandView.App).
//
// A pin this PC can't open is drawn faintly with a warning badge, as on the bar.
//
// In a list folder a pin is a line of its own at every height it is given, in the lists' one design
// (ListTiers): its icon with the running indicator under it, and its name, on one row; taller, under
// the name, whether it is open and what it opens, as much as the height holds. The whole line opens
// it.
//
// §380: a pin to a folder opens the folder in File Explorer, as every pin opens what it points at; what
// a folder holds is a widget of its own (FolderContentsLine). From §32 to §380 a pin could show it.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

internal sealed unsafe class PinTile : CellIsland, IListContent
{
    /// <summary>Segoe Fluent Icons: Warning, the badge on a pin whose target isn't on this PC.</summary>
    private const string WarningGlyph = "\uE7BA";

    /// <summary>How much of a pin shows when this PC doesn't have what it opens.</summary>
    private const float BrokenOpacity = 0.4f;

    private readonly Dictionary<int, Pixels?> pictures = [];
    private PinOptions options = new();

    public PinTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    /// <summary>Nothing to read on a timer: what it draws changes only with the pin and its windows.</summary>
    public override int IntervalMs => 0;

    /// <summary>What the pin opens, resolved (see <see cref="PinItems"/>).</summary>
    public Item Item { get; private set; } = PinItems.For(null);

    /// <summary>Its running indicator and its app's badge or overlay, as the bar's own pins have them; the dock sets it.</summary>
    public Mark Mark { get; set; }

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        // Its size is worked out from these (WidgetShapes), so they are kept in step.
        Settings = island;
        options = island.Pin ?? new PinOptions();
        Item = PinItems.For(options);
        pictures.Clear();
        Forget();
    }

    public override string? Tooltip(int index) => Item.Broken is { } broken ? $"{Item.Name}\n{broken}" : Item.Name;

    /// <summary>The dock's, which follows the one rule for every app entry (<see cref="AppClicks"/>): a folder too opens, in File Explorer.</summary>
    public override IslandAction Click(int index) => new(View: IslandView.App);

    protected override bool Sample(bool everything) => false;

    // stage 3: the size is WidgetShapes' now, a function of the settings and the bar's
    // numbers; there is no Length here to reach anything live (docs\widgets.md §4).

    /// <summary>Its icon in the middle of the cell, as a pin on the bar.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height)
    {
        var ink = new IslandInk(style.Look);
        var side = MathF.Min(style.Icon, MathF.Min(width, height) - 4 * style.Scale);
        var icon = PopupPaint.Rect((width - side) / 2, (height - side) / 2, side, side);
        Icon(dc, icon, ink.Text, ink.Secondary, style.Scale);
        Signal(dc, icon, SignalPaint.BadgeInk.Of(style.Look));
        Pill(dc, PopupPaint.Rect(0, 0, width, height), style);
    }

    /// <summary>
    /// What its app says on its button — the badge, or the overlay its window set — on the icon's
    /// corner, as the bar's own pins show it (Dock.Signals.cs sets it with the running indicator).
    /// </summary>
    internal void Signal(ID2D1DeviceContext* dc, D2D_RECT_F icon, SignalPaint.BadgeInk ink) =>
        SignalPaint.Corner(dc, Gfx, Mark.Signal, new RectF(icon.left, icon.top, icon.right - icon.left, icon.bottom - icon.top), ink);

    /// <summary>The pin's own line (<see cref="PaintLine"/>).</summary>
    protected internal override unsafe void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface) =>
        PaintLine(dc, box, style);

    /// <summary>
    /// Its icon in <paramref name="box"/>: the target's, the pin's own glyph — at <paramref name="glyph"/> px
    /// where a line says (the kit's sizes, §388), else four fifths of the box — or its first letters.
    /// </summary>
    internal void Icon(ID2D1DeviceContext* dc, D2D_RECT_F box, DXGI_RGBA text, DXGI_RGBA secondary, float scale, float? glyph = null)
    {
        var side = MathF.Min(box.right - box.left, box.bottom - box.top);
        // The icon is a piece of its own, as a track's art is: where it stands, for a test watching.
        PaintWatch.Shape(PopupPaint.Rect(box.left, box.top, side, side));
        var faint = Item.Broken is not null;
        if (Item.PictureIsGlyph)
        {
            PopupPaint.Glyph(dc, Gfx, Item.Picture!, glyph ?? MathF.Floor(side * 0.8f), faint ? IslandInk.Faded(text, BrokenOpacity) : text, box);
        }
        else
        {
            var key = (int)MathF.Ceiling(side);
            if (!pictures.TryGetValue(key, out var pixels))
            {
                pictures[key] = pixels = Item.Icon(key);
            }
            if (pixels is not null)
            {
                Gfx.DrawPixels(dc, pixels, box.left, box.top, side, faint ? BrokenOpacity : 1f);
            }
            else
            {
                // A link or a target the shell has no icon for: its first letter, as the bar draws it.
                var letter = Item.Name is { Length: > 0 } name ? name[..1].ToUpperInvariant() : "?";
                PopupPaint.Centred(dc, Gfx, letter, MathF.Floor(side * 0.5f), Weight.Semibold, faint ? IslandInk.Faded(text, BrokenOpacity) : text, box, ink: false);
            }
        }
        if (!faint)
        {
            return;
        }
        // The warning badge, on the icon's corner as on the bar.
        var badge = MathF.Max(8 * scale, side * 0.5f);
        PopupPaint.Glyph(dc, Gfx, WarningGlyph, badge, secondary, PopupPaint.Rect(box.right - badge * 0.75f, box.bottom - badge * 0.75f, badge, badge));
    }

    /// <summary>The running indicator under the icon of a cell, as the bar draws it under its pins.</summary>
    private void Pill(ID2D1DeviceContext* dc, D2D_RECT_F cell, in IslandStyle style)
    {
        if (Mark.Count == 0)
        {
            return;
        }
        var ink = new IslandInk(style.Look);
        var thickness = MathF.Max(2, MathF.Round(3 * style.Scale));
        var length = Mark.Active ? 16 * style.Scale : Mark.Count > 1 ? 10 * style.Scale : 6 * style.Scale;
        var middle = (cell.left + cell.right) / 2;
        var bottom = cell.bottom - thickness - MathF.Round(2 * style.Scale);
        PopupPaint.Fill(
            dc,
            PopupPaint.Rect(middle - length / 2, bottom, length, thickness),
            thickness / 2,
            Mark.Active ? ink.Accent : ink.Secondary);
    }

    // --- As a list's line -------------------------------------------------------
    // A pin's line in a list folder at whatever height it is given (docs\widgets.md §9, "A line at
    // every height"), drawn from the pin's own data — its icon, its name, whether it is open and what
    // it opens:
    //
    //   * One row: its icon in the list's leading column with the running indicator under it, as the
    //     bar has it under its pins, and its name — whole, or cut while it still reads.
    //   * The compact line: the icon a little larger, the name, and under it whether it is open and
    //     what it opens.
    //   * The whole widget: the icon larger still, and beside it the name, large — a pin's name is
    //     its reading — with whether it is open and what it opens under it.
    //
    // Which one a line draws is what fits its height at the text size in force (ListTiers.Of); the
    // whole widget never says less than the compact line would at the same height, and what is
    // shorter than the line stands at its top (the person, 2026-10-11: "top aligned and not center their
    // content"; §432 found it centred from three rows). The whole line opens it. Its sizes (§432): Compact the
    // one row, Full the whole widget with what it opens (ListHeight).

    /// <summary>
    /// Its form for its size (§432): one row (Compact); or the whole widget — the icon, the name as a reading,
    /// and what it opens on the lines it takes, two at the most (Full; while it is open, "Open" under them where
    /// they leave a line, the running indicator under the icon saying so anyway).
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (LineSizes.Of(Settings) == LineSize.Compact)
        {
            return LineRows.Px(1, style);
        }
        var form = ListForm.Of(Gfx, style);
        var (_, strip) = PillOf(style);
        var side = WholeIcon(form, style);
        var left = MathF.Max(form.TextLeft, form.Inset + side + style.Gap);
        var room = MathF.Max(1, width - form.Inset - left);
        var lines = ListTiers.Lines(Gfx, [(DetailOf(Item), 2)], room, float.PositiveInfinity, form.SmallPx);
        var name = TilePaint.LineHeight(Gfx, ListTiers.ValuePx(style), Weight.Semibold);
        return (2 * form.Pad) + MathF.Max(side + strip, name + lines.Sum(each => each.Height));
    }

    /// <summary>
    /// What a pin opens, said under its name: why this PC can't open it; a link's address; a file's
    /// or a folder's path; and nothing for an app, whose name already says it.
    /// </summary>
    internal static string DetailOf(Item item)
    {
        if (item.Broken is { Length: > 0 } broken)
        {
            return broken;
        }
        if (item.Kind == ItemKind.Link)
        {
            return Uri.TryCreate(item.Target, UriKind.Absolute, out var address) && address.Host.Length > 0 ? address.Host : item.Target;
        }
        return item.Target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase) ? "" : item.Target;
    }

    /// <summary>Whether it is open, in words: its windows as the running indicator counts them, or nothing while it is not.</summary>
    internal static string RunningOf(Mark mark) => mark.Count switch
    {
        <= 0 => "",
        1 => S.WidgetPinOpen,
        var count => S.WidgetPinWindowsOpen(count),
    };

    /// <summary>
    /// Where a pin's line puts its parts at one height, in the box's px: its tier, the icon's square,
    /// the running indicator's strip under it, the name's line, the form of the name that reads there
    /// (none where none does) and its size, and what is said under the name (each line's box, words
    /// and the lines it may wrap to).
    /// </summary>
    internal readonly record struct PinLine(ListTier Tier, D2D_RECT_F Icon, D2D_RECT_F Pill, D2D_RECT_F Name, string? NameText, float NamePx, IReadOnlyList<(D2D_RECT_F Box, string Text, int Lines)> Under)
    {
        /// <summary>How much the line says: its name, and each line under it.</summary>
        public int Says => (NameText is null ? 0 : 1) + Under.Count;
    }

    /// <summary>The running indicator's thickness, and the strip under the icon it stands in.</summary>
    private static (float Thickness, float Strip) PillOf(in TileStyle style)
    {
        var thickness = MathF.Max(2, style.Px(3));
        return (thickness, thickness + style.Px(2));
    }

    /// <summary>What is said under the name: whether it is open, then what it opens on up to <paramref name="detail"/> lines.</summary>
    private List<(string Text, int Lines)> Said(int detail) => [(RunningOf(Mark), 1), (DetailOf(Item), detail)];

    /// <summary>
    /// The line's layout in <paramref name="box"/>, the drawing's and a test's one question (see the
    /// note above).
    /// </summary>
    internal PinLine LineOf(D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var height = box.bottom - box.top;
        var (_, strip) = PillOf(style);
        var saying = Said(1).Count(each => each.Text.Length > 0);
        // The compact line holds the name and one line under it; the whole widget the larger icon, or
        // the name at the compact line's figure size and what it says under it.
        var compact = (2 * form.Pad) + form.Line + form.SmallLine;
        var fullIcon = WholeIcon(form, style);
        var full = (2 * form.Pad) + MathF.Max(fullIcon + strip, TilePaint.LineHeight(Gfx, ListTiers.ValuePx(style), Weight.Semibold) + (saying * form.SmallLine));
        // §432: in a folder its size's form, whatever the height: one row (Compact) or the whole widget (Full).
        var tier = SizedTier(ListTier.Full) ?? ListTiers.Of(height, compact, MathF.Max(full, compact + form.Gap));
        if (tier == ListTier.One)
        {
            return OneLine(box, style, form);
        }
        var lean = CompactLine(box, style, form);
        if (tier == ListTier.Compact)
        {
            return lean;
        }
        var whole = WholeLine(box, style, form);
        return whole.Says >= lean.Says ? whole : lean;
    }

    /// <summary>The whole widget's icon: the kit's Feature size, in the leading column grown into the room before the text (§388).</summary>
    private static float WholeIcon(in ListForm form, in TileStyle style) => ListTiers.GlyphPx(form, style, ListTier.Full);

    /// <summary>One row: the icon and its running indicator in the leading column, the name where every name begins.</summary>
    private PinLine OneLine(D2D_RECT_F box, in TileStyle style, in ListForm form)
    {
        var (thickness, strip) = PillOf(style);
        var one = ListTiers.Row(Gfx, box, style, [Item.Name], Weight.Regular, 0);
        var side = form.Icon;
        var iconTop = PopupPaint.Round(one.Middle - ((side + strip) / 2));
        var icon = PopupPaint.Rect(box.left + form.Inset, iconTop, side, side);
        return new PinLine(
            ListTier.One,
            icon,
            PopupPaint.Rect(icon.left, icon.bottom + style.Px(2), side, thickness),
            PopupPaint.Rect(one.NameLeft, one.TextTop, one.NameRoom, form.Line),
            one.Name,
            form.TextPx,
            []);
    }

    /// <summary>
    /// The compact line: the icon a little larger at the gutter, the name, and under it whether it is
    /// open and what it opens, as much as the height holds, at the line's top.
    /// </summary>
    private PinLine CompactLine(D2D_RECT_F box, in TileStyle style, in ListForm form)
    {
        var (thickness, strip) = PillOf(style);
        var height = box.bottom - box.top;
        var side = MathF.Max(form.Icon, PopupPaint.Round(ListTiers.GlyphPx(form, style, ListTier.Compact) * 1.2f));
        var left = MathF.Max(box.left + form.TextLeft, box.left + form.Inset + side + style.Gap);
        var room = MathF.Max(1, box.right - form.Inset - left);
        var name = ListTiers.Name(Gfx, [Item.Name], form.TextPx, Weight.Semibold, room);
        var lines = ListTiers.Lines(Gfx, Said(1), room, height - (2 * form.Pad) - form.Line, form.SmallPx);
        var words = form.Line + lines.Sum(each => each.Height);
        var block = MathF.Max(words, side + strip);
        var top = LineRows.Top(box.top, height, block, form.Pad);
        var textTop = top + PopupPaint.Round((block - words) / 2);
        var icon = PopupPaint.Rect(box.left + form.Inset, top + PopupPaint.Round((block - side - strip) / 2), side, side);
        return new PinLine(
            ListTier.Compact,
            icon,
            PopupPaint.Rect(icon.left, icon.bottom + style.Px(2), side, thickness),
            PopupPaint.Rect(left, textTop, room, form.Line),
            name,
            form.TextPx,
            ListTiers.Stacked(lines, left, room, textTop + form.Line));
    }

    /// <summary>
    /// The whole widget: the icon larger still at the gutter, and beside it the name as large as the
    /// line makes a reading and it fits whole at — down to the list's size — with whether it is open
    /// and what it opens on up to two lines under it, at the line's top.
    /// </summary>
    private PinLine WholeLine(D2D_RECT_F box, in TileStyle style, in ListForm form)
    {
        var (thickness, strip) = PillOf(style);
        var height = box.bottom - box.top;
        var side = WholeIcon(form, style);
        var left = MathF.Max(box.left + form.TextLeft, box.left + form.Inset + side + style.Gap);
        var room = MathF.Max(1, box.right - form.Inset - left);
        var saying = Said(2).Count(each => each.Text.Length > 0);
        // As large as the height makes a reading and the name fits whole at, down to the list's own
        // size, where it is cut while it still reads.
        var gfx = Gfx;
        var whole = Item.Name;
        var body = form.TextPx;
        var px = LineType.Largest(style, ListTiers.HeroPx(Gfx, style, height, (2 * form.Pad) + (saying * form.SmallLine)),
            each => each <= body + 0.01f || PopupPaint.Measure(gfx, whole, each, Weight.Semibold).X <= room);
        var name = ListTiers.Name(Gfx, [Item.Name], px, Weight.Semibold, room);
        var nameLine = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        // What it opens first, so a line as tall as its size measured (§432) keeps it while the pin is open too: the
        // running indicator under the icon says that already.
        var lines = ListTiers.Lines(Gfx, Enumerable.Reverse(Said(2)), room, height - (2 * form.Pad) - nameLine, form.SmallPx);
        var words = nameLine + lines.Sum(each => each.Height);
        var block = MathF.Max(words, side + strip);
        var top = LineRows.Top(box.top, height, block, form.Pad);
        var textTop = top + PopupPaint.Round((block - words) / 2);
        var icon = PopupPaint.Rect(box.left + form.Inset, top + PopupPaint.Round((block - side - strip) / 2), side, side);
        return new PinLine(
            ListTier.Full,
            icon,
            PopupPaint.Rect(icon.left, icon.bottom + style.Px(2), side, thickness),
            PopupPaint.Rect(left, textTop, room, nameLine),
            name,
            px,
            ListTiers.Stacked(lines, left, room, textTop + nameLine));
    }

    /// <summary>
    /// The line (<see cref="LineOf"/>): the icon and its running indicator, the name — faint where
    /// this PC can't open the pin — and what is said under it in the secondary ink, all cut with an
    /// ellipsis inside the gutter where the room runs out.
    /// </summary>
    private void PaintLine(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style)
    {
        var line = LineOf(box, style);
        var theme = style.Theme;
        Icon(dc, line.Icon, theme.Text, theme.TextSecondary, style.Scale, LineGlyph.Fitting(style, TilePaint.Width(line.Icon)));
        Signal(dc, line.Icon, SignalPaint.BadgeInk.Of(theme));
        PaintPill(dc, (line.Pill.left + line.Pill.right) / 2, line.Pill.bottom, TilePaint.Height(line.Pill), MathF.Min(TilePaint.Width(line.Pill), style.Px(16)), theme);
        var ink = Item.Broken is null ? theme.Text : theme.TextTertiary;
        if (line.NameText is { } name)
        {
            TilePaint.Line(dc, Gfx, name, line.NamePx, line.Tier == ListTier.One ? Weight.Regular : Weight.Semibold, ink, line.Name.left, line.Name.top, TilePaint.Width(line.Name));
        }
        var small = ListForm.Of(Gfx, style).SmallPx;
        foreach (var (at, words, lines) in line.Under)
        {
            PopupPaint.Wrapped(dc, Gfx, words, small, Weight.Regular, theme.TextSecondary, at.left, at.top, TilePaint.Width(at), lines);
        }
    }

    /// <summary>The same indicator on a card or a line, in the folder's own colours.</summary>
    internal void PaintPill(ID2D1DeviceContext* dc, float middle, float bottom, float thickness, float full, FlyoutTheme theme)
    {
        if (Mark.Count == 0)
        {
            return;
        }
        var length = Mark.Active ? full : Mark.Count > 1 ? full * 0.625f : full * 0.375f;
        PopupPaint.Fill(
            dc,
            PopupPaint.Rect(middle - length / 2, bottom - thickness, length, thickness),
            thickness / 2,
            Mark.Active ? PopupPaint.Ink(theme.Accent) : theme.TextSecondary);
    }
}
