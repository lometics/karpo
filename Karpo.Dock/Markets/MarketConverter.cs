// A currency's converter style as a line (docs\widgets.md §246; it was the currency converter widget's, §242):
// two amounts, one in each of the pair's currencies, either typed and the other worked out as it is typed,
// the swap between them, and under them the rate with its age; the two currencies' names over them and the
// rate's chart at the foot where the line has the room. Its rate is the markets line's own reading
// (MarketIsland), so a converter and a ticker of one pair and range are one fetch.
//
// What it starts at is its settings' (MarketOptions): the pair its symbol is, and the amount. What is typed and swapped is the line's own while the dock runs and is never written: a converter
// is a calculator, and its settings are its menu's. A change to them starts it again from them.
//
// At every height (§9, "A line at every height"), each piece only where it fits whole, top down:
//   one row      the two fields with their currencies and the swap between them
//   two rows     the rate under them, "1 EUR = 0.9412 CHF", its age at the end
//   three rows   the pair's names over the fields, "Euro → Swiss Franc"
//   then         the rate's chart at the foot, edge to edge, in the colour of its move
// A field is WinUI's text box (InlineEditor while it has the keys). The folder's keys stop at the two fields
// and the swap (§471): Enter on a field types in it; there Enter keeps what is typed, Escape takes it back,
// Tab keeps it and goes on to the next line. Amounts are typed in the region's marks (Currencies.Parse)
// and written as every markets figure is.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Markets;

/// <summary>A currency's converter, drawn and worked for its markets line; see the top of the file.</summary>
internal sealed unsafe class MarketConverter(Func<Gfx> gfx, Action repaint)
{
    /// <summary>The swap's target (<see cref="Click"/>).</summary>
    internal const int SwapTarget = 1;

    private readonly InlineEditor editor = new(multiLine: false, Currencies.Longest);
    private (string From, string To) pair;
    private double amount = 1;

    // The line's own while the dock runs: the pair turned round, the amount last given and the side it was
    // given on (0 the first field, 1 the second), and the field being typed in (-1 none).
    private bool swapped;
    private int anchorSide;
    private double anchorValue = 1;
    private int typing = -1;

    // The pointer: the part under it, a press on the swap, a selection being dragged in a field.
    private int hot = -1;
    private bool pressingSwap;
    private bool selecting;
    private long lastPress;
    private Vector2 lastPoint;
    private int clicks;

    // The line as it was last drawn, which the keys read.
    private D2D_RECT_F shownBox;
    private TileStyle shownStyle;

    private Gfx Gfx => gfx();

    /// <summary>Its settings: a change to the pair or the amount starts it again from them.</summary>
    internal void Configure(MarketOptions options)
    {
        var nextPair = MarketSymbols.Pair(options.Symbol) ?? ("", "");
        var nextAmount = options.AmountOrOne;
        if (nextPair != pair || nextAmount != amount)
        {
            swapped = false;
            anchorSide = 0;
            anchorValue = nextAmount;
            typing = -1;
        }
        (pair, amount) = (nextPair, nextAmount);
    }

    /// <summary>The field typed in, -1 for none.</summary>
    internal int Typing => typing;

    /// <summary>The editor of the field typed in.</summary>
    internal InlineEditor Editor => editor;

    /// <summary>Whether the currencies are turned round from its settings'.</summary>
    internal bool Swapped => swapped;

    /// <summary>The folder closed: what is being typed is kept, and the line rests.</summary>
    internal void Rest()
    {
        Stop(keep: true);
        hot = -1;
    }

    /// <summary>A click: the swap's, or nothing — the fields are pressed, not clicked.</summary>
    /// <summary>A click with the keys (§471): the swap swaps; a field is typed in, what it says chosen.</summary>
    internal void Click(int index, MarketReading reading)
    {
        pressingSwap = false;
        if (index == SwapTarget)
        {
            Swap();
        }
        else if (index is > SwapTarget and <= SwapTarget + 2)
        {
            Begin(index - SwapTarget - 1, selectAll: true, reading);
        }
    }

    /// <summary>The currencies turned round; the amount given stays where it is, in the currency now there.</summary>
    internal void Swap()
    {
        Stop(keep: true);
        swapped = !swapped;
        repaint();
    }

    /// <summary>What the line reads, its swap applied.</summary>
    private MarketReading Turned(MarketReading reading) => swapped ? reading.Swapped() : reading;

    /// <summary>
    /// What the two fields say now: the field typed in as it is typed, else the amount last given as it was
    /// given; the other its worth at the rate, a dash without one.
    /// </summary>
    internal (string First, string Second) Texts(MarketReading line)
    {
        var reading = Turned(line);
        var side = typing >= 0 ? typing : anchorSide;
        double? given = typing >= 0 ? Currencies.Parse(editor.Text, NumberMarks.Region()) : anchorValue;
        var mine = typing >= 0 ? editor.Text : Currencies.Plain(anchorValue);
        var other = given is { } value && reading.Rate is { } rate
            ? Currencies.Money(Currencies.Convert(value, rate, back: side == 1), side == 0 ? reading.To : reading.From)
            : typing >= 0 && given is null ? "" : "–";
        return side == 0 ? (mine, other) : (other, mine);
    }

    /// <summary>Starts typing in field <paramref name="side"/>, with what it says now, all of it selected where <paramref name="selectAll"/>.</summary>
    internal void Begin(int side, bool selectAll, MarketReading reading)
    {
        if (typing == side)
        {
            return;
        }
        var texts = Texts(reading);
        Stop(keep: true);
        var said = side == 0 ? texts.First : texts.Second;
        typing = side;
        editor.Placeholder = "0";
        editor.Begin(said == "–" ? "" : said.Replace(",", "", StringComparison.Ordinal), selectAll);
        repaint();
    }

    /// <summary>Stops typing: what parses is the amount given from now on, else it is taken back.</summary>
    private void Stop(bool keep)
    {
        if (typing < 0)
        {
            return;
        }
        if (keep && Currencies.Parse(editor.Text, NumberMarks.Region()) is { } value)
        {
            anchorSide = typing;
            anchorValue = value;
        }
        typing = -1;
        selecting = false;
    }

    internal void StopTyping()
    {
        Stop(keep: true);
        repaint();
    }

    /// <summary>Types <paramref name="text"/> into the field typed in, as the keys would.</summary>
    internal void Type(string text)
    {
        foreach (var c in text)
        {
            _ = editor.Char(c);
        }
        repaint();
    }

    // --- The plan -------------------------------------------------------------------------------------

    /// <summary>The height of a converter with every piece: its names, its fields, its rate and its chart.</summary>
    internal float Height(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var field = PopupPaint.Round(style.Text(32));
        return MathF.Ceiling(form.Pad + form.Line + form.Gap + field + form.Gap + form.SmallLine + form.Gap + style.Px(MarketChart.ChartDip) + form.Pad);
    }

    /// <summary>Where each piece of a line in <paramref name="box"/> stands; a piece it has no room for is empty.</summary>
    internal ConverterPlan Plan(D2D_RECT_F box, in TileStyle style, MarketReading line)
    {
        var reading = Turned(line);
        var form = ListForm.Of(Gfx, style);
        var height = TilePaint.Height(box);
        var field = PopupPaint.Round(style.Text(32));
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        // A row of the list, which a field stands in the middle of where the line has more than one.
        var row = PopupPaint.Round(40 * style.Scale * style.TextScale);
        var rated = height + 0.5f >= row + form.SmallLine + form.Pad;
        var named = rated && height + 0.5f >= form.Pad + form.Line + form.Gap + field + form.Gap + form.SmallLine + form.Pad;
        var fieldTop = !rated ? box.top + PopupPaint.Round((height - field) / 2)
            : named ? box.top + form.Pad + form.Line + form.Gap
            : box.top + PopupPaint.Round((row - field) / 2);
        var codeWidth = MathF.Ceiling(MathF.Max(
            PopupPaint.Measure(Gfx, reading.From, form.TextPx, Weight.Semibold).X,
            PopupPaint.Measure(Gfx, reading.To, form.TextPx, Weight.Semibold).X));
        var gap = style.Px(8);
        // Swap: the lines' one glyph button (LineButton), in the middle of the fields' row.
        var buttons = LineButton.Of(Gfx, style);
        var each = MathF.Max(1, MathF.Floor((right - left - buttons.Height - (2 * codeWidth) - (4 * gap)) / 2));
        var first = PopupPaint.Rect(left, fieldTop, each, field);
        var firstCode = PopupPaint.Rect(first.right + gap, fieldTop, codeWidth, field);
        var swap = buttons.Square(firstCode.right + gap, fieldTop + (field / 2));
        var second = PopupPaint.Rect(swap.right + gap, fieldTop, each, field);
        var secondCode = PopupPaint.Rect(second.right + gap, fieldTop, codeWidth, field);
        var header = named ? PopupPaint.Rect(left, box.top + form.Pad, MathF.Max(1, right - left), form.Line) : default;
        var rate = rated ? PopupPaint.Rect(left, first.bottom + form.Gap, MathF.Max(1, right - left), form.SmallLine) : default;
        // The chart at the foot, no taller than a ticker's (MarketChart.ChartDip), where there is room for one.
        var below = (rated ? rate.bottom : first.bottom) + form.Gap;
        var chart = rated && reading is { Ready: true, Day.Count: >= 2 } && box.bottom - below - form.Pad + 0.5f >= style.Px(28)
            ? new D2D_RECT_F(left, MathF.Max(below, box.bottom - style.Px(MarketChart.ChartDip)), right, box.bottom - form.Pad)
            : default;
        return new ConverterPlan(header, first, firstCode, swap, second, secondCode, rate, chart);
    }

    // --- Drawn ----------------------------------------------------------------------------------------

    internal void Paint(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, MarketReading line)
    {
        shownBox = box;
        shownStyle = style;
        var reading = Turned(line);
        var plan = Plan(box, style, line);
        var form = ListForm.Of(Gfx, style);
        var theme = style.Theme;
        var quick = QuickTheme.For(theme);
        var ink = new IslandInk(style.Look);
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            if (TilePaint.Width(plan.Chart) > 0)
            {
                var moved = reading.Stale ? theme.TextSecondary : MarketChart.Moved(reading.Direction, style.Look, ink);
                MarketChart.Behind(dc, plan.Chart, reading, moved, MarketChart.Coloured(reading, style.Look), down: true, [], style.Px(28), style.Px(4));
            }
            if (TilePaint.Width(plan.Header) > 0)
            {
                string[] names = [CurrencyNames.Pair(reading.From, reading.To), $"{reading.From} → {reading.To}"];
                if (ListTiers.Name(Gfx, names, form.TextPx, Weight.Semibold, TilePaint.Width(plan.Header)) is { } said)
                {
                    TilePaint.Line(dc, Gfx, said, form.TextPx, Weight.Semibold, theme.Text, plan.Header.left, plan.Header.top, TilePaint.Width(plan.Header));
                }
            }
            var (firstText, secondText) = Texts(line);
            Field(dc, style, quick, plan.First, firstText, 0, reading);
            Field(dc, style, quick, plan.Second, secondText, 1, reading);
            Code(dc, style, plan.FirstCode, reading.From);
            Code(dc, style, plan.SecondCode, reading.To);
            LineButton.Of(Gfx, style).Paint(dc, Gfx, style.Theme, plan.Swap, "", MarketRows.SwapGlyph, ButtonStyle.Standard, hot == SwapTarget, pressingSwap);

            if (TilePaint.Width(plan.Rate) > 0)
            {
                Rate(dc, style, form, plan.Rate, reading, ink);
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// A field: WinUI's text box at rest — its fill, its outline and the strong stroke along its bottom,
    /// lighter under the pointer — with its amount; the editor's own while it is typed in.
    /// </summary>
    private void Field(ID2D1DeviceContext* dc, in TileStyle style, QuickTheme theme, D2D_RECT_F box, string text, int side, MarketReading reading)
    {
        var px = ListForm.Of(Gfx, style).TextPx;
        if (typing == side)
        {
            editor.Paint(dc, Gfx, box, px, theme, style);
            return;
        }
        var radius = style.Px(4);
        var hair = MathF.Max(1, style.Px(1));
        PopupPaint.Fill(dc, box, radius, hot == FieldTarget(side) ? theme.ControlFillHover : theme.ControlFill);
        PopupPaint.Stroke(dc, box, radius, hair, theme.ControlStroke);
        var strip = new D2D_RECT_F(box.left, box.bottom - hair, box.right, box.bottom);
        dc->PushAxisAlignedClip(&strip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        PopupPaint.Fill(dc, box, radius, theme.StrongStroke);
        dc->PopAxisAlignedClip();
        var across = style.Px(10);
        var room = MathF.Max(1, TilePaint.Width(box) - (2 * across));
        // A worth worked out where the given amount is the other field's; a dash, or a rate past due, quiet.
        var given = side == (typing >= 0 ? typing : anchorSide);
        var ink = !given && (!reading.Ready || reading.Stale) ? theme.TextSecondary : theme.Text;
        var top = box.top + PopupPaint.Round((TilePaint.Height(box) - TilePaint.LineHeight(Gfx, px)) / 2);
        TilePaint.Line(dc, Gfx, text, px, Weight.Regular, ink, box.left + across, top, room);
    }

    /// <summary>A field's currency, beside it.</summary>
    private void Code(ID2D1DeviceContext* dc, in TileStyle style, D2D_RECT_F box, string code)
    {
        var px = ListForm.Of(Gfx, style).TextPx;
        var top = box.top + PopupPaint.Round((TilePaint.Height(box) - TilePaint.LineHeight(Gfx, px, Weight.Semibold)) / 2);
        TilePaint.Line(dc, Gfx, code, px, Weight.Semibold, style.Theme.Text, box.left, top, TilePaint.Width(box) + 1);
    }

    /// <summary>
    /// The rate, "1 EUR = 0.9412 CHF", with its age at the row's end where both stand whole, the age in the
    /// alarm's ink once it is past due; without a rate, why, or that one is coming.
    /// </summary>
    private void Rate(ID2D1DeviceContext* dc, in TileStyle style, in ListForm form, D2D_RECT_F box, MarketReading reading, in IslandInk ink)
    {
        var theme = style.Theme;
        var room = TilePaint.Width(box);
        if (reading.Rate is not { } rate)
        {
            TilePaint.Line(dc, Gfx, reading.Waiting, form.SmallPx, Weight.Regular, theme.TextSecondary, box.left, box.top, room);
            return;
        }
        var said = Currencies.RateText(reading.From, reading.To, rate);
        var saidWidth = PopupPaint.Measure(Gfx, said, form.SmallPx, Weight.Regular).X;
        var age = reading.MarketLine.Length > 0 ? reading.MarketLine : reading.Updated;
        var ageWidth = age.Length > 0 ? PopupPaint.Measure(Gfx, age, form.SmallPx, Weight.Regular).X : 0;
        if (ageWidth > 0 && saidWidth + style.Px(12) + ageWidth <= room)
        {
            TilePaint.LineRight(dc, Gfx, age, form.SmallPx, Weight.Regular, reading.Stale ? ink.Alarm : theme.TextSecondary, box.right, box.top, ageWidth + 1);
        }
        TilePaint.Line(dc, Gfx, said, form.SmallPx, Weight.Regular, theme.TextSecondary, box.left, box.top, room);
    }

    // --- The pointer and the keys ---------------------------------------------------------------------

    /// <summary>A field's target, past the swap's.</summary>
    private static int FieldTarget(int side) => SwapTarget + 1 + side;

    /// <summary>What is at <paramref name="point"/>: the swap, a field, or nothing (-1).</summary>
    private int At(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading)
    {
        var plan = Plan(box, style, reading);
        static bool In(D2D_RECT_F rect, Vector2 at) => rect.right > rect.left && at.X >= rect.left && at.X < rect.right && at.Y >= rect.top && at.Y < rect.bottom;
        return In(plan.Swap, point) ? SwapTarget
            : In(plan.First, point) ? FieldTarget(0)
            : In(plan.Second, point) ? FieldTarget(1)
            : -1;
    }

    /// <summary>
    /// The swap and the two fields, the line's targets (the keys' stops, §471): a click on the swap swaps; a press on
    /// a field is its own (<see cref="Press"/>, the caret where it lands), and Enter on it types in it.
    /// </summary>
    internal int Target(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading) =>
        Math.Max(0, At(point, box, style, reading));

    /// <summary>Over a field: the text cursor.</summary>
    internal bool OverField(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading) => At(point, box, style, reading) > SwapTarget;

    internal void Hover(Vector2? point, D2D_RECT_F box, in TileStyle style, MarketReading reading)
    {
        var at = point is { } where ? At(where, box, style, reading) : -1;
        if (point is null)
        {
            pressingSwap = false;
        }
        if (at != hot)
        {
            hot = at;
            repaint();
        }
    }

    /// <summary>The swap lights pressed until its click.</summary>
    internal void Down(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading)
    {
        if (At(point, box, style, reading) == SwapTarget)
        {
            pressingSwap = true;
            repaint();
        }
    }

    /// <summary>A press in a field is the field's: typing starts there, the caret where it was pressed, and a drag selects.</summary>
    internal bool Press(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading)
    {
        shownBox = box;
        shownStyle = style;
        var at = At(point, box, style, reading);
        if (at <= SwapTarget)
        {
            Stop(keep: true);
            Down(point, box, style, reading);
            return false;
        }
        var side = at - SwapTarget - 1;
        var count = Clicks(point);
        Begin(side, selectAll: false, reading);
        var plan = Plan(box, style, reading);
        editor.Press(point, count, KeyState.Down(VK.VK_SHIFT), Gfx, ListForm.Of(Gfx, style).TextPx, side == 0 ? plan.First : plan.Second, style);
        selecting = true;
        repaint();
        return true;
    }

    internal void Drag(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading)
    {
        if (!selecting || typing < 0)
        {
            return;
        }
        var plan = Plan(box, style, reading);
        editor.Drag(point, Gfx, ListForm.Of(Gfx, style).TextPx, typing == 0 ? plan.First : plan.Second, style);
        repaint();
    }

    internal void Release()
    {
        selecting = false;
        repaint();
    }

    /// <summary>One click, or two or three where it follows the last in Windows' double-click time and near it.</summary>
    private int Clicks(Vector2 point)
    {
        var now = Environment.TickCount64;
        var near = MathF.Abs(point.X - lastPoint.X) <= GetSystemMetrics(SM.SM_CXDOUBLECLK)
            && MathF.Abs(point.Y - lastPoint.Y) <= GetSystemMetrics(SM.SM_CYDOUBLECLK);
        clicks = near && now - lastPress <= GetDoubleClickTime() ? Math.Min(3, clicks + 1) : 1;
        lastPress = now;
        lastPoint = point;
        return clicks;
    }

    /// <summary>A character typed: the field's while one is typed in; a figure typed on the line starts the field its amount was given in.</summary>
    internal bool Char(char c, MarketReading reading)
    {
        if (typing >= 0)
        {
            if (editor.Char(c))
            {
                repaint();
            }
            return true;
        }
        if (!char.IsDigit(c))
        {
            return false;
        }
        Begin(anchorSide, selectAll: true, reading);
        _ = editor.Char(c);
        repaint();
        return true;
    }

    internal bool Key(int key, MarketReading reading)
    {
        var shift = KeyState.Down(VK.VK_SHIFT);
        var control = KeyState.Down(VK.VK_CONTROL);
        // Not typing, the keys are the folder's: its stops are the fields and the swap (§471), Enter on a field types in it.
        if (typing < 0)
        {
            return false;
        }
        switch (key)
        {
            case VK.VK_ESCAPE:
                Stop(keep: false);
                repaint();
                return true;
            case VK.VK_RETURN:
                Stop(keep: true);
                repaint();
                return true;
            // Tab keeps what is typed and is the folder's, line to line, as every line's field lets it go.
            case VK.VK_TAB:
                Stop(keep: true);
                repaint();
                return false;
        }
        var plan = Plan(shownBox, shownStyle, reading);
        _ = editor.Key(key, shift, control, Gfx, ListForm.Of(Gfx, shownStyle).TextPx, typing == 0 ? plan.First : plan.Second, shownStyle, out _);
        repaint();
        return true;
    }

    /// <summary>The swap's name over it; the rate's age where a refresh failed and it stands (<paramref name="stale"/>).</summary>
    internal (string Text, D2D_RECT_F Part)? Tip(Vector2 point, D2D_RECT_F box, in TileStyle style, MarketReading reading, string? stale)
    {
        var plan = Plan(box, style, reading);
        if (At(point, box, style, reading) == SwapTarget)
        {
            return (S.SettingsWidgetMarketSwap, plan.Swap);
        }
        if (point.Y >= plan.Rate.top && point.Y < plan.Rate.bottom && stale is not null)
        {
            return (stale, plan.Rate);
        }
        return null;
    }
}

/// <summary>
/// Where a converter's pieces stand (<see cref="MarketConverter.Plan"/>): the names over it, the two fields
/// with their currencies and the swap, the rate under them and the chart at the foot; a piece the line has
/// no room for is empty.
/// </summary>
internal readonly record struct ConverterPlan(D2D_RECT_F Header, D2D_RECT_F First, D2D_RECT_F FirstCode, D2D_RECT_F Swap, D2D_RECT_F Second, D2D_RECT_F SecondCode, D2D_RECT_F Rate, D2D_RECT_F Chart);
