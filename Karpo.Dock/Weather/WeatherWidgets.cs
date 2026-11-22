// The one control the weather has of its own beside the kit's blocks, on the quick settings' Widget base
// so it gets the same hover, pressed and focus behaviour as the rest of Karpo's popups:
//
// - WeatherDayRow: one day — its name, its glyph, its chance of precipitation,
//   and its low and high with a bar between them placed on the scale the whole
//   list shares, so a warm day's bar sits to the right of a cold one's.
//
// The conditions now, which had a header control here, are the kit's Header and Hero (WeatherPartTile, §401).

using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Weather;

/// <summary>One day of the list: its glyph, its name, its chance of rain, and its range.</summary>
internal sealed unsafe class WeatherDayRow : Widget
{
    /// <summary>
    /// Its own left inset. The flyout places the row half the panel's gutter in, so the row's own
    /// half lands its content on the gutter with everything else down the panel — which is why this
    /// is written as a share of <see cref="Widget.PanelPadDip"/> and not as the 8 it comes to. It was
    /// the 8, and a gutter that moved would have left this row a little out with no way to tell.
    /// </summary>
    private const float InsetDip = PanelPadDip / 2;
    private const float NameDip = 46;
    private const float ChanceDip = 34;
    private const float ValueDip = 34;
    private const float BarDip = 4;

    /// <summary>"Today", "Wed".</summary>
    public string Day { get; set; } = "";

    public string Glyph { get; set; } = WeatherCodes.Unknown;

    /// <summary>"40%", or empty.</summary>
    public string Chance { get; set; } = "";

    public string Low { get; set; } = "";

    public string High { get; set; } = "";

    /// <summary>Where the day's low sits on the list's scale, 0 to 1.</summary>
    public float From { get; set; }

    /// <summary>Where its high sits, 0 to 1.</summary>
    public float To { get; set; }

    /// <summary>Today's row is written in semibold, as Windows marks it.</summary>
    public bool Today { get; set; }

    public static float RowHeight(QuickContext context) => context.T(38);

    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var pad = context.D(InsetDip);
        // The kit's row (§401): the glyph in the list's leading column on the gutter, the day where every name begins.
        var icon = FolderFlyout.RowIcon(context.TextPx);
        PopupPaint.Glyph(dc, context.Gfx, Glyph, context.GlyphPx, WeatherCodes.Ink(Glyph, theme.Popup),
            PopupPaint.Rect(pad, PopupPaint.Round((Height - icon) / 2), icon, icon));

        var nameLeft = pad + FolderFlyout.RowText(context.TextPx) - FolderFlyout.RowInset(context.TextPx);
        var nameWidth = context.T(NameDip);
        var weight = Today ? Weight.Semibold : Weight.Regular;
        var name = PopupPaint.Measure(context.Gfx, Day, context.TextPx, weight, nameWidth);
        PopupPaint.Text(dc, context.Gfx, Day, context.TextPx, weight, theme.Text, nameLeft,
            PopupPaint.Round((Height - name.Y) / 2), nameWidth);

        var chanceWidth = context.T(ChanceDip);
        var chanceLeft = nameLeft + nameWidth + context.D(4);
        // At the largest text the row has no room for the chance, the low and the high side by
        // side: the chance gives way first, so the two figures never run into each other.
        var valueRoom = context.T(ValueDip);
        var crowded = chanceLeft + chanceWidth + context.D(4) + valueRoom + context.D(4) > Width - pad - valueRoom;
        if (crowded)
        {
            chanceWidth = 0;
        }
        if (Chance.Length > 0 && !crowded)
        {
            // A label, not on, selected or an action: the secondary ink, never the accent (§388).
            var chanceInk = theme.HighContrast ? theme.Text : theme.TextSecondary;
            var chance = PopupPaint.Measure(context.Gfx, Chance, context.SmallPx, Weight.Regular, chanceWidth);
            PopupPaint.Text(dc, context.Gfx, Chance, context.SmallPx, Weight.Regular, chanceInk, chanceLeft,
                PopupPaint.Round((Height - chance.Y) / 2), chanceWidth);
        }

        var valueWidth = context.T(ValueDip);
        var lowLeft = chanceLeft + chanceWidth + context.D(4);
        var low = PopupPaint.Measure(context.Gfx, Low, context.TextPx, Weight.Regular, valueWidth);
        PopupPaint.Text(dc, context.Gfx, Low, context.TextPx, Weight.Regular, theme.TextSecondary,
            lowLeft + MathF.Max(0, valueWidth - low.X), PopupPaint.Round((Height - low.Y) / 2), valueWidth);

        var highLeft = Width - pad - valueWidth;
        var high = PopupPaint.Measure(context.Gfx, High, context.TextPx, Weight.Regular, valueWidth);
        PopupPaint.Text(dc, context.Gfx, High, context.TextPx, Weight.Regular, theme.Text,
            highLeft + MathF.Max(0, valueWidth - high.X), PopupPaint.Round((Height - high.Y) / 2), valueWidth);

        var barLeft = lowLeft + valueWidth + context.D(10);
        var barRight = highLeft - context.D(10);
        if (barRight - barLeft > context.D(16))
        {
            Bar(dc, context, PopupPaint.Rect(barLeft, PopupPaint.Round((Height - context.D(BarDip)) / 2), barRight - barLeft, context.D(BarDip)));
        }
    }

    /// <summary>The day's range on the list's scale: a faint track with the day's own stretch on it.</summary>
    private void Bar(ID2D1DeviceContext* dc, QuickContext context, D2D_RECT_F track)
    {
        var theme = context.Theme;
        var radius = (track.bottom - track.top) / 2;
        PopupPaint.Fill(dc, track, radius, theme.HighContrast ? theme.ControlStroke : theme.ControlAltFill);
        var width = track.right - track.left;
        var from = track.left + (Math.Clamp(From, 0, 1) * width);
        var to = track.left + (Math.Clamp(To, 0, 1) * width);
        // Even a day whose low and high round to the same number gets a visible stretch.
        var least = 2 * radius;
        if (to - from < least)
        {
            to = MathF.Min(track.right, from + least);
            from = MathF.Max(track.left, to - least);
        }
        PopupPaint.Fill(dc, new D2D_RECT_F(from, track.top, to, track.bottom), radius, theme.Accent);
    }
}
