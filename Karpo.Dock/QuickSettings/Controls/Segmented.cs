// A choice of a few as Windows 11 draws one: the Windows Community Toolkit's Segmented, which
// Windows' own apps use where a choice has two to four options. The segments share one track (a
// quiet fill inside WinUI's control stroke, SegmentedTrack); the one chosen stands raised on it — a
// control's fill and stroke, the accent's short pill under its content — and the others are subtle,
// lit under the pointer. A segment is its glyph, with its word beside it where the control is wide
// enough for every segment's (Worded); else the glyph alone, its name the tooltip.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

/// <summary>What the segments of a segmented control stand on: its track, behind them, taking no pointer.</summary>
internal sealed unsafe class SegmentedTrack : Widget
{
    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var radius = context.D(4);
        PopupPaint.Fill(dc, Local, radius, context.Theme.ControlAltFill);
        PopupPaint.Stroke(dc, Local, radius, context.Hairline, context.Theme.ControlStroke);
    }
}

/// <summary>One segment of a segmented control (see the top of the file).</summary>
internal sealed unsafe class SegmentedItem : Widget
{
    private bool selected;
    private bool worded;

    public SegmentedItem(string text, string glyph)
    {
        Text = text;
        Glyph = glyph;
    }

    public string Text { get; }

    public string Glyph { get; }

    /// <summary>The one chosen: raised on the track, the accent's pill under it.</summary>
    public bool Selected
    {
        get => selected;
        set
        {
            if (selected != value)
            {
                selected = value;
                Invalidate();
            }
        }
    }

    /// <summary>Its word beside its glyph; else the glyph alone.</summary>
    public bool Worded
    {
        get => worded;
        set
        {
            if (worded != value)
            {
                worded = value;
                Invalidate();
            }
        }
    }

    public override bool Focusable => Enabled;

    /// <summary>Between a segment's edge and its plate: the track shows round the one chosen.</summary>
    public static float Inset(QuickContext context) => context.D(2);

    /// <summary>
    /// The width a segment showing <paramref name="text"/> beside its glyph needs: the glyph, 8 DIP,
    /// the word, and 12 DIP either side inside its plate.
    /// </summary>
    public static float WordedWidth(QuickContext context, string text) =>
        MathF.Ceiling((2 * (context.D(12) + Inset(context))) + context.D(16) + context.D(8) + Label.WidthOf(context, text, LabelStyle.Body));

    /// <summary>The least a segment of its glyph alone is wide.</summary>
    public static float GlyphWidth(QuickContext context) => context.D(40);

    /// <summary>A segment's height: WinUI's 32 DIP, or a line of the text with 4 DIP over and under it where that is more.</summary>
    public static float HeightFor(QuickContext context) =>
        MathF.Max(context.D(32), MathF.Ceiling(TilePaint.LineHeight(context.Gfx, context.TextPx)) + (2 * Inset(context)) + context.D(8));

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var inset = Inset(context);
        var plate = new D2D_RECT_F(inset, inset, MathF.Max(inset + 1, Width - inset), MathF.Max(inset + 1, Height - inset));
        var radius = context.D(4);
        var down = Pressed && Hot && Enabled;
        if (selected)
        {
            PopupPaint.Fill(dc, plate, radius, down ? theme.ControlFillPressed : Hot ? theme.ControlFillHover : theme.ControlFill);
            Button.Outline(dc, context, plate, radius);
        }
        else if (down || (Hot && Enabled))
        {
            PopupPaint.Fill(dc, plate, radius, down ? theme.SubtlePressed : theme.SubtleHover);
        }
        var bright = (Hot || down) && theme.HighContrast;
        var ink = !Enabled ? theme.TextDisabled : bright ? theme.TextOnHover : down && !selected ? theme.TextSecondary : theme.Text;

        var glyphBox = context.D(16);
        var gap = worded ? context.D(8) : 0;
        var room = MathF.Max(1, plate.right - plate.left - (2 * context.D(12)) - glyphBox - gap);
        var said = worded ? PopupPaint.Measure(context.Gfx, Text, context.TextPx, Weight.Regular, room) : default;
        var x = PopupPaint.Round((Width - (glyphBox + gap + said.X)) / 2);
        PopupPaint.Glyph(dc, context.Gfx, Glyph, context.GlyphPx, ink, PopupPaint.Rect(x, PopupPaint.Round((Height - glyphBox) / 2), glyphBox, glyphBox));
        if (worded)
        {
            PopupPaint.Text(dc, context.Gfx, Text, context.TextPx, Weight.Regular, ink, x + glyphBox + gap, PopupPaint.Round((Height - said.Y) / 2), room);
        }
        if (selected)
        {
            // The toolkit's pill: 16 by 3 DIP on the accent, centred under the content.
            var pill = context.D(16);
            var thick = MathF.Max(2, context.D(3));
            PopupPaint.Fill(dc, PopupPaint.Rect(PopupPaint.Round((Width - pill) / 2), plate.bottom - thick - context.D(1), pill, thick), thick / 2, Enabled ? theme.Accent : theme.TextDisabled);
        }
        if (FocusVisible)
        {
            FocusRing(dc, context, plate, radius, context.D(1));
        }
    }
}
