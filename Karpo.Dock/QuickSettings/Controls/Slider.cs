// WinUI's horizontal slider, as the volume rows have it: a 4 px track (the part
// before the thumb in the accent colour), and a 20 px thumb whose accent core is
// 12 px at rest, 14 under the pointer and 10 while held. A press on the track
// jumps there and keeps dragging; the wheel, the arrows, Page Up/Down, Home and
// End step it, and a folder's keys on a line's slider (Step). The value is a whole number from Minimum to Maximum.

using System.Numerics;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.QuickSettings.Controls;

/// <summary>A slider's arithmetic, apart from its drawing: where a value sits and what a key does to it.</summary>
internal static class SliderMath
{
    /// <summary>The value at <paramref name="x"/> on a track from <paramref name="left"/> to <paramref name="right"/>, rounded, clamped.</summary>
    public static int ValueAt(float x, float left, float right, int minimum, int maximum)
    {
        if (right <= left || maximum <= minimum)
        {
            return minimum;
        }
        var fraction = Math.Clamp((x - left) / (right - left), 0, 1);
        return minimum + (int)MathF.Round(fraction * (maximum - minimum), MidpointRounding.AwayFromZero);
    }

    /// <summary>Where <paramref name="value"/> sits on the track.</summary>
    public static float PositionOf(int value, float left, float right, int minimum, int maximum) =>
        maximum <= minimum ? left : left + (right - left) * (Math.Clamp(value, minimum, maximum) - minimum) / (maximum - minimum);

    /// <summary>What <paramref name="key"/> makes of <paramref name="value"/>; null for a key a slider doesn't take.</summary>
    public static int? Key(int value, int key, int minimum, int maximum, int small, int large) => key switch
    {
        VK.VK_LEFT or VK.VK_DOWN => Math.Clamp(value - small, minimum, maximum),
        VK.VK_RIGHT or VK.VK_UP => Math.Clamp(value + small, minimum, maximum),
        VK.VK_NEXT => Math.Clamp(value - large, minimum, maximum),
        VK.VK_PRIOR => Math.Clamp(value + large, minimum, maximum),
        VK.VK_HOME => minimum,
        VK.VK_END => maximum,
        _ => null,
    };

    /// <summary>
    /// A wheel turn: <paramref name="step"/> per notch (120), a notch up raising it, as Windows'
    /// volume does. Fractions of a notch from a precise wheel add up in <paramref name="rest"/>.
    /// </summary>
    public static int Wheel(int value, int delta, ref int rest, int step, int minimum, int maximum)
    {
        rest += delta;
        var notches = rest / 120;
        rest -= notches * 120;
        return Math.Clamp(value + notches * step, minimum, maximum);
    }
}

internal sealed unsafe class Slider : Widget
{
    private int value;
    private int wheelRest;
    private float grab;
    private bool moved;

    public int Minimum { get; init; }

    public int Maximum { get; init; } = 100;

    /// <summary>The arrows' step.</summary>
    public int SmallChange { get; init; } = 1;

    /// <summary>Page Up and Page Down's step.</summary>
    public int LargeChange { get; init; } = 10;

    /// <summary>A wheel notch's step.</summary>
    public int WheelChange { get; init; } = 2;

    public int Value
    {
        get => value;
        set
        {
            var clamped = Math.Clamp(value, Minimum, Maximum);
            if (this.value != clamped)
            {
                this.value = clamped;
                Invalidate();
            }
        }
    }

    /// <summary>The person moved it; the page applies the value (the level of a device, of an app).</summary>
    public Action<int>? Changed { get; set; }

    /// <summary>
    /// The value has settled: the pointer let go after a drag that moved it, or a screen reader set
    /// it. Not every value a drag passes through — what a volume tick is played for.
    /// </summary>
    public Action<int>? Settled { get; set; }

    /// <summary>
    /// A wheel notch or a key moved the value: one of a run that may go on, which is why it is not
    /// <see cref="Settled"/> — a volume's tick waits for the run to stop (Feedback.Notch).
    /// </summary>
    public Action<int>? Stepped { get; set; }

    /// <summary>Being dragged: a value read from elsewhere in the meantime must not move it.</summary>
    public bool Dragging => Pressed;

    public override bool Focusable => Enabled;

    private float Thumb(QuickContext context) => context.D(20);

    /// <summary>Where the thumb's centre can go: half a thumb in from either end.</summary>
    private (float Left, float Right) Track(QuickContext context)
    {
        var half = Thumb(context) / 2;
        return (half, MathF.Max(half, Width - half));
    }

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var (left, right) = Track(context);
        var middle = PopupPaint.Round(Height / 2);
        var thickness = context.D(4);
        var x = PopupPaint.Round(SliderMath.PositionOf(value, left, right, Minimum, Maximum));
        var trackTop = middle - thickness / 2;
        // As far as its thumb goes, as tall as the thumb.
        PaintWatch.Space(Spacing.Meter, new D2D_RECT_F(left - Thumb(context) / 2, middle - Thumb(context) / 2, right + Thumb(context) / 2, middle + Thumb(context) / 2));
        PopupPaint.Fill(dc, new D2D_RECT_F(left - thickness / 2, trackTop, right + thickness / 2, trackTop + thickness), thickness / 2,
            Enabled ? theme.StrongFill : theme.StrongStrokeDisabled);
        if (x > left)
        {
            PopupPaint.Fill(dc, new D2D_RECT_F(left - thickness / 2, trackTop, x, trackTop + thickness), thickness / 2,
                Enabled ? theme.Accent : theme.AccentDisabled);
        }
        var thumb = Thumb(context);
        var centre = new Vector2(x, middle);
        PopupPaint.Circle(dc, centre, thumb / 2, theme.ControlSolid);
        PopupPaint.Ring(dc, centre, thumb / 2, context.Hairline, theme.ControlStroke);
        var core = context.D(!Enabled ? 12 : Pressed ? 10 : Hot ? 14 : 12) / 2;
        PopupPaint.Circle(dc, centre, core, Enabled ? Pressed ? theme.AccentPressed : Hot ? theme.AccentHover : theme.Accent : theme.AccentDisabled);
        if (FocusVisible)
        {
            FocusRing(dc, context, new D2D_RECT_F(0, middle - thumb / 2, Width, middle + thumb / 2), thumb / 2, context.D(3));
        }
    }

    public override bool PointerPressed(Vector2 at, Mods mods, int clicks)
    {
        // A panel's context, or a card host's (Widget.Host): the thumb's size is a scale, and a
        // slider in a folder's card has no panel to take it from.
        if (ContextNow is not { } context)
        {
            return false;
        }
        var (left, right) = Track(context);
        var x = SliderMath.PositionOf(value, left, right, Minimum, Maximum);
        // On the thumb, the drag keeps where it took hold; elsewhere the thumb jumps there first.
        grab = MathF.Abs(at.X - x) <= Thumb(context) / 2 ? at.X - x : 0;
        Set(SliderMath.ValueAt(at.X - grab, left, right, Minimum, Maximum));
        return true;
    }

    public override void PointerMoved(Vector2 at)
    {
        if (!Pressed || ContextNow is not { } context)
        {
            return;
        }
        var (left, right) = Track(context);
        Set(SliderMath.ValueAt(at.X - grab, left, right, Minimum, Maximum));
    }

    public override void PointerReleased(Vector2 at, bool inside)
    {
        base.PointerReleased(at, inside);
        // The end of a drag: one settled value, however many the drag passed through.
        if (moved)
        {
            moved = false;
            Settled?.Invoke(value);
        }
    }

    public override bool Wheel(int delta) => StepTo(SliderMath.Wheel(value, delta, ref wheelRest, WheelChange, Minimum, Maximum));

    public override bool Key(int key, Mods mods) =>
        SliderMath.Key(value, key, Minimum, Maximum, SmallChange, LargeChange) is { } next && StepTo(next);

    /// <summary>
    /// keys: <paramref name="steps"/> of its arrows' step from where it is, held to its ends — a folder's keys on a
    /// line's slider (CellIsland.CardStep: one, ten, or past either end).
    /// </summary>
    public bool Step(int steps) => StepTo((int)Math.Clamp((long)value + ((long)steps * SmallChange), Minimum, Maximum));

    /// <summary>A notch or a key moved it to <paramref name="next"/>: shown, passed on, and told as a step of a run (<see cref="Stepped"/>).</summary>
    private bool StepTo(int next)
    {
        if (Set(next))
        {
            Stepped?.Invoke(value);
        }
        return true;
    }

    /// <summary>A value the person chose: shown, and passed on when it differs. True when it moved.</summary>
    private bool Set(int next)
    {
        if (next == value)
        {
            return false;
        }
        Value = next;
        moved = Pressed || moved;
        Changed?.Invoke(value);
        return true;
    }
}
