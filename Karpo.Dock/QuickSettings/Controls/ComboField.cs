// WinUI's ComboBox at rest, as Windows 11 draws it: a standard control's fill and outline, what is
// chosen at its start (a glyph, then its name, cut where it is too long), and the down chevron at
// its end in the secondary ink. 32 px high, radius 4, text 14 px, 12 of padding at the start and
// the chevron's 12 px glyph 11 from the end. The list it opens is not its own: its host opens a menu
// under it (the sound page's devices, SoundPartTile), so this is the field and nothing more.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed unsafe class ComboField : Widget
{
    /// <summary>Segoe Fluent Icons: ChevronDown, WinUI's ComboBox's.</summary>
    internal const string ChevronGlyph = "";

    private string text = "";
    private string? glyph;

    /// <summary>What is chosen, or what the field says where there is nothing to choose.</summary>
    public string Text
    {
        get => text;
        set
        {
            if (text != value)
            {
                text = value;
                Invalidate();
            }
        }
    }

    /// <summary>A Segoe Fluent Icons glyph before the text: the chosen device's.</summary>
    public string? Glyph
    {
        get => glyph;
        set
        {
            if (glyph != value)
            {
                glyph = value;
                Invalidate();
            }
        }
    }

    public override bool Focusable => Enabled;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var radius = context.D(4);
        var down = Pressed && Hot;
        var fill = !Enabled ? theme.ControlFillDisabled : down ? theme.ControlFillPressed : Hot ? theme.ControlFillHover : theme.ControlFill;
        PopupPaint.Fill(dc, Local, radius, fill);
        Button.Outline(dc, context, Local, radius);
        var ink = !Enabled ? theme.TextDisabled : down ? theme.TextSecondary : Hot && theme.HighContrast ? theme.TextOnHover : theme.Text;
        var quiet = !Enabled ? theme.TextDisabled : theme.TextSecondary;

        var chevron = context.D(12);
        var chevronBox = PopupPaint.Rect(Width - context.D(11) - chevron, PopupPaint.Round((Height - chevron) / 2), chevron, chevron);
        PopupPaint.Glyph(dc, context.Gfx, ChevronGlyph, 12 * context.Scale, quiet, chevronBox);

        var x = context.D(12);
        if (glyph is { } code)
        {
            var side = context.D(16);
            PopupPaint.Glyph(dc, context.Gfx, code, context.GlyphPx, ink, PopupPaint.Rect(x, PopupPaint.Round((Height - side) / 2), side, side));
            x += side + context.D(8);
        }
        var room = MathF.Max(1, chevronBox.left - context.D(8) - x);
        if (text.Length > 0)
        {
            var size = PopupPaint.Measure(context.Gfx, text, context.TextPx, Weight.Regular, room);
            PopupPaint.Text(dc, context.Gfx, text, context.TextPx, Weight.Regular, ink, x, PopupPaint.Round((Height - size.Y) / 2), room);
        }
        if (FocusVisible)
        {
            FocusRing(dc, context, Local, radius, context.D(3));
        }
    }
}
