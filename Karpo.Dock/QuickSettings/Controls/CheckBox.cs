// WinUI's check box: a 20 px box with radius 4 (a rim round a faint fill when
// clear, the accent with a check mark when ticked) and its text 8 px after it.
// A click ticks or clears it by itself and then tells the page.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed unsafe class CheckBox : Widget
{
    // Segoe Fluent Icons: CheckMark.
    private static readonly string CheckGlyph = char.ConvertFromUtf32(0xE73E);

    private bool isChecked;

    public CheckBox(string text, bool isChecked)
    {
        Text = text;
        this.isChecked = isChecked;
        Clicked = Toggle;
    }

    public string Text { get; }

    public bool Checked
    {
        get => isChecked;
        set
        {
            if (isChecked != value)
            {
                isChecked = value;
                Invalidate();
            }
        }
    }

    /// <summary>Ticked or cleared by the person.</summary>
    public Action<bool>? Changed { get; set; }

    public override bool Focusable => Enabled;

    private void Toggle()
    {
        Checked = !isChecked;
        Changed?.Invoke(isChecked);
    }

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var size = context.D(20);
        var box = PopupPaint.Rect(0, PopupPaint.Round((Height - size) / 2), size, size);
        var radius = context.D(4);
        var down = Pressed && Hot;
        if (isChecked)
        {
            var fill = !Enabled ? theme.AccentDisabled : down ? theme.AccentPressed : Hot ? theme.AccentHover : theme.Accent;
            PopupPaint.Fill(dc, box, radius, fill);
            PopupPaint.Glyph(dc, context.Gfx, CheckGlyph, 12 * context.Scale, !Enabled ? theme.TextDisabled : theme.TextOnAccent, box);
        }
        else
        {
            PopupPaint.Fill(dc, box, radius, Hot && Enabled ? theme.ControlAltFillHover : theme.ControlAltFill);
            PopupPaint.Stroke(dc, box, radius, context.Hairline, Enabled ? theme.StrongStroke : theme.StrongStrokeDisabled);
        }
        var left = size + context.D(8);
        var room = MathF.Max(1, Width - left);
        var line = PopupPaint.Measure(context.Gfx, Text, context.TextPx, Weight.Regular, room);
        PopupPaint.Text(dc, context.Gfx, Text, context.TextPx, Weight.Regular, Enabled ? theme.Text : theme.TextDisabled, left, PopupPaint.Round((Height - line.Y) / 2), room);
        if (FocusVisible)
        {
            FocusRing(dc, context, new D2D_RECT_F(0, box.top, MathF.Min(Width, left + line.X), box.bottom), radius, context.D(3));
        }
    }

    /// <summary>The width the box and its text need.</summary>
    public float NaturalWidth(QuickContext context) => MathF.Ceiling(context.D(28) + Label.WidthOf(context, Text, LabelStyle.Body));
}
