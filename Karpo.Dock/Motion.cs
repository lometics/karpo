// §489: Karpo's one motion (the person, 2026-11-02: "i actually want only one default animation for everything in karpo and it
// should also not be customizable. it should be able to be turned off so that there are no animations at all and the speed
// should be selectable as well between three levels ... the transitions in general should be subtle and professional and
// also clean looking"). The one place every curve and every duration of Karpo's is: what moves asks it, by the kind of motion
// it is (MotionKind), and nothing else names a time or a curve (MotionSourceTests holds every file to it).
//
//  - **The language** is Windows 11's (Fluent motion): what comes in decelerates (0, 0, 0, 1) — fast first, settling into
//    place —, what goes accelerates (0.7, 0, 1, 0.5), a colour or a wash fades straight. A surface coming in rises a short way
//    (ShiftDip) from the side it comes from as it fades in, and goes back the same way; content swapped in place comes in from
//    the way it goes the same short way; nothing bounces, nothing flies far, nothing grows from nothing.
//  - **The times** (Normal): a hover's or a press's 83 ms (Fluent's "fast"), a move's and a swap's 167, a popup's 200 in and
//    117 out, a large surface's (a bar out of hiding, the edit bar) 250 in and 167 out, a calendar's glide 250, §637 a bar
//    morphing into another (a layout switched) 333.
//  - **The speed** (Config.Animation, ⚙ › General's Animations, §549): every time times one factor — Slower 1.5, Normal 1,
//    Faster ⅔.
//  - **Off** (Config.Animation Off, or Windows' Animation effects off): no time at all; every surface stands where it ends at
//    once (Motion.On false; Look.Animations, which every surface already asks).
//
// The compositor's side (MotionEase) makes its easing functions from the same curves, so what the tests hold is what runs.

using System.Numerics;
using Karpo.Core;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>§489: a cubic Bézier easing from (0, 0) to (1, 1), by its two control points, as the compositor's: where it stands at a share of the time.</summary>
public readonly record struct MotionCurve(Vector2 One, Vector2 Two)
{
    /// <summary>The share of the way at <paramref name="x"/>, the share of the time (0 to 1).</summary>
    public float At(float x)
    {
        if (x <= 0 || x >= 1)
        {
            return x <= 0 ? 0 : 1;
        }
        // Windows' decelerate (0, 0, 0, 1) exactly: its x is t³ and its y 3t² − 2t³.
        if (One == Vector2.Zero && Two == Vector2.UnitY)
        {
            var t = MathF.Cbrt(x);
            return (3 * t * t) - (2 * t * t * t);
        }
        // The curve's parameter whose x is the time, by halving: x(t) only grows on a curve whose control points' x are in [0, 1].
        var (low, high) = (0f, 1f);
        for (var step = 0; step < 32; step++)
        {
            var t = (low + high) / 2;
            if (Bezier(t, One.X, Two.X) < x)
            {
                low = t;
            }
            else
            {
                high = t;
            }
        }
        return Bezier((low + high) / 2, One.Y, Two.Y);
    }

    private static float Bezier(float t, float one, float two)
    {
        var u = 1 - t;
        return (3 * u * u * t * one) + (3 * u * t * t * two) + (t * t * t);
    }
}

/// <summary>§489: what kind of motion a surface makes, which says how long it takes and on which curve (<see cref="Motion"/>).</summary>
public enum MotionKind
{
    /// <summary>A hover's or a selection's wash, a colour, a tooltip: 83 ms, straight.</summary>
    Hover,

    /// <summary>A press's give and its release: 83 ms, decelerate.</summary>
    Press,

    /// <summary>Something gliding to its new place or size — giving way, settling, a block moved: 167 ms, decelerate.</summary>
    Move,

    /// <summary>Content swapped in place — a menu's level, a folder's page, a subfolder: 167 ms, decelerate, from <see cref="Motion.ShiftDip"/> along its way.</summary>
    Swap,

    /// <summary>A popup coming in: 200 ms, decelerate, rising <see cref="Motion.ShiftDip"/> from its side.</summary>
    Open,

    /// <summary>A popup going: 117 ms, accelerate, back toward its side.</summary>
    Close,

    /// <summary>A large surface coming in — a bar out of hiding, the edit bar: 250 ms, decelerate.</summary>
    Show,

    /// <summary>A large surface going: 167 ms, accelerate.</summary>
    Hide,

    /// <summary>A calendar's step to another month or day: 250 ms, decelerate.</summary>
    Glide,

    /// <summary>
    /// §637: a bar becoming another — another layout shown, a taskbar becoming a dock —, its ground and its widgets morphing from
    /// the one to the other: 333 ms, decelerate (Fluent's time for a point-to-point move across a display).
    /// </summary>
    Morph,
}

/// <summary>
/// §489: Karpo's one motion, as Karpo and Windows have it now (<see cref="Look.Motion"/>): whether anything moves at all
/// (<see cref="On"/>) and how fast (<see cref="Speed"/>); every surface's time and curve. See the top of the file. Its
/// default is <see cref="Still"/>: nothing moves until something says it does.
/// </summary>
public readonly record struct Motion(bool On, Animation Speed)
{
    /// <summary>Nothing moves.</summary>
    public static readonly Motion Still = new(false, Animation.Normal);

    /// <summary>Karpo as it comes: everything moves, at Normal speed.</summary>
    public static readonly Motion Normal = new(true, Animation.Normal);

    /// <summary>Windows' decelerate curve, what comes in: fast first, settling into place.</summary>
    public static readonly MotionCurve Decelerate = new(new Vector2(0, 0), new Vector2(0, 1));

    /// <summary>Windows' accelerate curve, what goes: slow first, then away.</summary>
    public static readonly MotionCurve Accelerate = new(new Vector2(0.7f, 0), new Vector2(1, 0.5f));

    /// <summary>Straight, as the clock runs: a colour's change, a fade of a wash.</summary>
    public static readonly MotionCurve Linear = new(new Vector2(1 / 3f, 1 / 3f), new Vector2(2 / 3f, 2 / 3f));

    /// <summary>How far anything moves as it comes in, goes or swaps, in DIPs: one short way for everything.</summary>
    public const float ShiftDip = 12;

    /// <summary>A pressed icon's size: a little smaller, as Windows' taskbar has it.</summary>
    public const float PressedScale = 0.9f;

    /// <summary>
    /// Edit mode's marching ants' one lap and a runner's (a signal's indeterminate bar), in ms: loops that run while
    /// something is so, not transitions — at the same pace at every speed, and none with motion off.
    /// </summary>
    public const double AntsLapMs = 480;

    /// <inheritdoc cref="AntsLapMs"/>
    public const double RunnerLapMs = 2000;

    /// <summary>The motion as <paramref name="config"/> has it, Windows' Animation effects on (<paramref name="windows"/>) or off.</summary>
    public static Motion Of(bool windows, Config config) => new(windows && config.Animation != Animation.Off, config.Animation);

    /// <summary>How many times its Normal time a motion takes at <paramref name="speed"/>.</summary>
    public static double Factor(Animation speed) => speed switch
    {
        Animation.Slower => 1.5,
        Animation.Faster => 2 / 3.0,
        _ => 1,
    };

    /// <summary><paramref name="kind"/>'s time at Normal speed, ms.</summary>
    public static double NormalMs(MotionKind kind) => kind switch
    {
        MotionKind.Hover or MotionKind.Press => 83,
        MotionKind.Move or MotionKind.Swap or MotionKind.Hide => 167,
        MotionKind.Open => 200,
        MotionKind.Close => 117,
        MotionKind.Show or MotionKind.Glide => 250,
        MotionKind.Morph => 333,
        _ => 167,
    };

    /// <summary>The curve <paramref name="kind"/> runs on.</summary>
    public static MotionCurve CurveOf(MotionKind kind) => kind switch
    {
        MotionKind.Hover => Linear,
        MotionKind.Close or MotionKind.Hide => Accelerate,
        _ => Decelerate,
    };

    /// <summary>How long <paramref name="kind"/> takes now, in whole ms: 0 with motion off.</summary>
    public double Ms(MotionKind kind) => On ? Math.Round(NormalMs(kind) * Factor(Speed)) : 0;

    /// <summary>
    /// <see cref="Ms"/> as a compositor's animation's Duration, at least a ms (the compositor takes nothing shorter): what every
    /// key frame animation of Karpo's runs for. Asked only where motion is <see cref="On"/>.
    /// </summary>
    public TimeSpan Time(MotionKind kind) => TimeSpan.FromMilliseconds(Math.Max(1, Ms(kind)));

    /// <summary>A lap of a loop (<see cref="AntsLapMs"/>, <see cref="RunnerLapMs"/>) as a Duration.</summary>
    public static TimeSpan Lap(double ms) => TimeSpan.FromMilliseconds(ms);

    /// <summary>
    /// What is left of a motion this engine timed (<paramref name="ms"/>: a popup turned back part way, <see cref="MotionClock"/>;
    /// a glide's record) as a Duration, at least a ms.
    /// </summary>
    public static TimeSpan Span(double ms) => TimeSpan.FromMilliseconds(Math.Max(1, ms));

    /// <summary>How far a surface moves, in whole px at <paramref name="scale"/>: none with motion off.</summary>
    public float Shift(float scale) => On ? MathF.Round(ShiftDip * scale) : 0;
}

/// <summary>§489: the compositor's easing functions, made from <see cref="Motion"/>'s curves: the only place one is made.</summary>
internal static class MotionEase
{
    /// <summary>The easing <paramref name="kind"/> runs on.</summary>
    public static CompositionEasingFunction Of(Compositor c, MotionKind kind) => Of(c, Motion.CurveOf(kind));

    /// <summary>An easing function of <paramref name="curve"/>.</summary>
    public static CompositionEasingFunction Of(Compositor c, MotionCurve curve) =>
        curve == Motion.Linear ? c.CreateLinearEasingFunction() : c.CreateCubicBezierEasingFunction(curve.One, curve.Two);

    /// <summary>A straight run, for a loop's lap.</summary>
    public static CompositionEasingFunction Straight(Compositor c) => c.CreateLinearEasingFunction();

    /// <summary>
    /// §489: content swapped in place — a menu's level, a folder's page, a subfolder, a layout on a bar — comes in to
    /// <paramref name="rest"/> (<paramref name="visual"/>'s Offset, where it stands) from <paramref name="way"/> times
    /// <see cref="Motion.ShiftDip"/> along <paramref name="axis"/> (a unit vector) at <paramref name="scale"/>: a Swap, decelerating.
    /// Only its offset moves, never its opacity, so where DWM has stopped animations (FlyoutHost.Open) it stands where it
    /// belongs, or a few px off it, and is never unseen. Nothing with motion off or no way; what it made goes to <paramref name="keep"/>.
    /// </summary>
    public static void SwapIn(Compositor c, Visual visual, Vector3 rest, Vector3 axis, int way, float scale, Motion motion, Action<IDisposable> keep)
    {
        visual.StopAnimation("Offset");
        visual.Offset = rest;
        if (!motion.On || way == 0)
        {
            return;
        }
        var ease = Of(c, MotionKind.Swap);
        var move = c.CreateVector3KeyFrameAnimation();
        keep(ease);
        keep(move);
        move.InsertKeyFrame(0, rest + (axis * motion.Shift(scale) * way));
        move.InsertKeyFrame(1, rest, ease);
        move.Duration = motion.Time(MotionKind.Swap);
        visual.StartAnimation("Offset", move);
    }
}

/// <summary>§489: what a popup is, which says how it comes in and goes (<see cref="PopupMotion.Of"/>).</summary>
public enum PopupEntrance
{
    /// <summary>A menu, a flyout, a folder: rises from its side and fades in (Open), back the same way (Close).</summary>
    Popup,

    /// <summary>A tooltip: fades in and out where it stands (Hover).</summary>
    Tip,

    /// <summary>A large surface — the edit bar: rises from its side and fades in (Show), back the same way (Hide).</summary>
    Bar,
}

/// <summary>
/// §489 (§446 before, a folder's alone): how a popup comes in (<see cref="Enter"/>, on <see cref="EnterCurve"/>) and goes
/// (<see cref="Exit"/>, on <see cref="ExitCurve"/>), and how far it moves from its side as it does (<see cref="ShiftDip"/>);
/// it fades in and out with it. <see cref="None"/>: at once.
/// </summary>
public sealed record PopupMotion(TimeSpan Enter, TimeSpan Exit, float ShiftDip, MotionCurve EnterCurve, MotionCurve ExitCurve)
{
    /// <summary>Nothing moves: shown and hidden at once.</summary>
    public static readonly PopupMotion None = new(TimeSpan.Zero, TimeSpan.Zero, 0, Motion.Decelerate, Motion.Accelerate);

    /// <summary>
    /// The plan for a popup of <paramref name="entrance"/> under <paramref name="motion"/>: none with motion off.
    /// <paramref name="switching"/>: it opens as another of its kind goes (a folder for another), and is there at once, the other
    /// going under it — never a frame of neither, nor two moving across each other. Arranged (<paramref name="arranged"/>) it
    /// fades where it stands: its guide lines and slots reach the display's edges, which a rise would show moving.
    /// </summary>
    public static PopupMotion Of(Motion motion, PopupEntrance entrance = PopupEntrance.Popup, bool arranged = false, bool switching = false)
    {
        if (!motion.On)
        {
            return None;
        }
        var (into, away) = entrance switch
        {
            PopupEntrance.Tip => (MotionKind.Hover, MotionKind.Hover),
            PopupEntrance.Bar => (MotionKind.Show, MotionKind.Hide),
            _ => (MotionKind.Open, MotionKind.Close),
        };
        return new PopupMotion(
            switching ? TimeSpan.Zero : TimeSpan.FromMilliseconds(motion.Ms(into)),
            TimeSpan.FromMilliseconds(motion.Ms(away)),
            entrance == PopupEntrance.Tip || arranged ? 0 : Motion.ShiftDip,
            Motion.CurveOf(into),
            Motion.CurveOf(away));
    }

    /// <summary>Where the panel stands shut, from where it stands open: toward its bar along <paramref name="edge"/>, in whole px at <paramref name="scale"/>.</summary>
    public Vector2 Away(Edge edge, float scale)
    {
        var far = MathF.Round(ShiftDip * scale);
        return edge switch
        {
            Edge.Top => new Vector2(0, -far),
            Edge.Left => new Vector2(-far, 0),
            Edge.Right => new Vector2(far, 0),
            _ => new Vector2(0, far),
        };
    }

    /// <summary>Where a panel opened beside an item (a submenu) stands shut: toward the item it hangs from, <paramref name="leftward"/> or not.</summary>
    public Vector2 Beside(bool leftward, float scale) => new((leftward ? 1 : -1) * MathF.Round(ShiftDip * scale), 0);
}

/// <summary>
/// §446, §489: where between shut (0) and open (1) a panel stands at a moment (ms on any one clock): the motion it was last
/// set going on — from where, to where, since when, how long, on which curve — or where it was last put.
/// </summary>
internal struct MotionClock
{
    private float from;
    private float to;
    private double start;
    private double ms;
    private MotionCurve curve;

    /// <summary>Where it stands at <paramref name="now"/>.</summary>
    public readonly float At(double now) =>
        ms <= 0 || now >= start + ms ? to : from + ((to - from) * curve.At((float)((now - start) / ms)));

    /// <summary>
    /// Set going at <paramref name="now"/> toward open (<paramref name="open"/>) or shut, by <paramref name="plan"/>: from where
    /// it stands, in the share of the plan's time that is left of the way. Where it starts, and how long it takes (0: there).
    /// </summary>
    public (float From, double Ms) Go(double now, bool open, PopupMotion plan)
    {
        var here = At(now);
        var target = open ? 1f : 0f;
        var whole = open ? plan.Enter : plan.Exit;
        (from, to, start, curve) = (here, target, now, open ? plan.EnterCurve : plan.ExitCurve);
        ms = whole.TotalMilliseconds * Math.Abs(target - here);
        return (here, ms);
    }

    /// <summary>Put open (<paramref name="open"/>) or shut at once.</summary>
    public void Snap(bool open)
    {
        (from, to, ms) = (open ? 1 : 0, open ? 1 : 0, 0);
    }
}
