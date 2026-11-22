// WinUI's toggle switch, as the quick settings' headers have it: a 40 × 20
// pill. Off, a rim (ControlStrongStrokeColorDefault) round a faint fill with a
// 12 px knob at the left in the secondary text colour; on, the accent fill with
// the knob at the right in the colour for text on the accent. The knob grows to
// 14 px under the pointer and stretches to 17 × 14 while pressed.
//
// A click (or Space or Enter) turns it and tells the page; the page asks the
// radio and sets On to what the radio really did.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed unsafe class ToggleSwitch : Widget
{
    private bool on;

    public bool On
    {
        get => on;
        set
        {
            if (on != value)
            {
                on = value;
                Invalidate();
            }
        }
    }

    public override bool Focusable => Enabled;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var width = context.D(40);
        var height = context.D(20);
        var left = Width - width;
        var top = PopupPaint.Round((Height - height) / 2);
        var track = PopupPaint.Rect(left, top, width, height);
        var radius = height / 2;
        var down = Pressed && Hot;
        PaintWatch.Space(Spacing.Control, track);
        DXGI_RGBA knob;
        if (on)
        {
            var fill = !Enabled ? theme.AccentDisabled : down ? theme.AccentPressed : Hot ? theme.AccentHover : theme.Accent;
            PopupPaint.Fill(dc, track, radius, fill);
            knob = !Enabled ? theme.TextDisabled : theme.TextOnAccent;
        }
        else
        {
            PopupPaint.Fill(dc, track, radius, Hot && Enabled ? theme.ControlAltFillHover : theme.ControlAltFill);
            PopupPaint.Stroke(dc, track, radius, context.Hairline, Enabled ? theme.StrongStroke : theme.StrongStrokeDisabled);
            knob = !Enabled ? theme.TextDisabled : theme.TextSecondary;
        }
        // The knob: 12 px at rest, 14 under the pointer, 17 × 14 while held.
        var knobHeight = context.D(Enabled && (Hot || down) ? 14 : 12);
        var knobWidth = context.D(Enabled && down ? 17 : Enabled && Hot ? 14 : 12);
        var inset = PopupPaint.Round((height - knobHeight) / 2);
        var x = on ? track.right - inset - knobWidth : track.left + inset;
        PopupPaint.Fill(dc, PopupPaint.Rect(x, top + inset, knobWidth, knobHeight), knobHeight / 2, knob);
        if (FocusVisible)
        {
            FocusRing(dc, context, track, radius, context.D(3));
        }
    }

    /// <summary>Only the pill (and a little around it) takes the pointer.</summary>
    public override bool Contains(Vector2 at) => at.X >= 0 && at.X < Width && at.Y >= 0 && at.Y < Height;
}
