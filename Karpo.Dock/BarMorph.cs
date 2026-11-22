// §637: a bar becoming another morphs (the person, 2026-11-09: "switching between layouts should have a very nice animation.
// optimize the animation also to look smooth if it transitions from one style to another. (e.g. dock to normal taskbar etc)").
//
// One mechanism for every such change of a bar that keeps its window — another layout shown on the display (the bar's menu, the
// layouts' line, the Layouts page), the shown layout made another type (a taskbar a dock) —: the bar's scene as it stood is kept,
// whole and untouched, as the morph's leaving picture, and a new scene is laid out for the new bar in the same window, over it.
// Both are told one plan (MorphPlan), made from what each shows (BarPicture: its widgets by key and kind, its grounds):
//
//  - a widget on both bars — the same widget (its key), else the next of its kind along the bar — glides from where it stood to
//    where it stands, at the size it had to its own, the old one crossfading into the new one on the way;
//  - a widget only on the old bar fades out where it stands, a little smaller (a large surface's Hide); one only on the new one
//    fades in where it stands, a little larger first (Show), once the glides are under way;
//  - the ground morphs: a taskbar's strip shrinks into a dock's panels (each panel grown out of it: the strip splits), a dock's
//    panels grow into the strip (they merge), a panel goes to the panel nearest it; the corners round or square on the way.
//
// All of it on one curve (Windows' decelerate) in one time (MotionKind.Morph, 333 ms at Normal), started in one commit on every
// display; the old scene let go of once the compositor says its animations are over (a scoped batch: no timer), or at once where
// the bar changes again first. Nothing with motion off (Karpo's Animations Off, Windows' Animation effects off): the new bar at once.
//
// The model here is pure (BarMorphTests): what glides, what fades, where each ground starts and ends, and each step's share of
// the time and its curve. Scene.Morph.cs runs it on the compositor; DockWindow (Dock.cs) starts and ends it.

using System.Numerics;
using Karpo.Core;
using Windows.UI.Composition;

namespace Karpo.Dock;

/// <summary>§637: one widget a bar shows, as a morph sees it: its key (its widget's), its kind, where it stands in the bar's window (px), and its index among the bar's parts.</summary>
public readonly record struct MorphPart(string Key, BarKind Kind, RectF Rect, int Part = -1);

/// <summary>§637: a bar's ground: the strip along its edge (<see cref="Slot"/> -1), or one of a dock's panels (its section); where it stands in the bar's window (px), and its corners.</summary>
public readonly record struct MorphGround(int Slot, RectF Rect, float Radius);

/// <summary>§637: what a bar shows, as a morph sees it: its widgets in their order along it, and its grounds.</summary>
public sealed record BarPicture(IReadOnlyList<MorphPart> Parts, IReadOnlyList<MorphGround> Grounds)
{
    public static readonly BarPicture Empty = new([], []);

    /// <summary>The same, <paramref name="by"/> px further: another window's corner, or another corner of the same window.</summary>
    public BarPicture Moved(Vector2 by) => by == Vector2.Zero ? this : new(
        [.. Parts.Select(part => part with { Rect = BarMorph.Moved(part.Rect, by) })],
        [.. Grounds.Select(ground => ground with { Rect = BarMorph.Moved(ground.Rect, by) })]);
}

/// <summary>
/// §637: a morph from one bar to another: the widgets that glide (by their index in each picture), those that only go and those
/// that only come; where each new ground starts (by its index in the new picture), and where each old ground goes.
/// </summary>
public sealed record MorphPlan(
    IReadOnlyList<(int From, int To)> Glides,
    IReadOnlyList<int> Leaving,
    IReadOnlyList<int> Entering,
    IReadOnlyList<MorphGround> GroundsFrom,
    IReadOnlyList<MorphGround> GroundsTo);

/// <summary>§637: one step of a morph: its share of the morph's time, from <see cref="Start"/> to <see cref="End"/>, on <see cref="Curve"/>.</summary>
public readonly record struct MorphStep(float Start, float End, MotionCurve Curve)
{
    /// <summary>Where it stands at <paramref name="share"/> of the morph's time (0 to 1).</summary>
    public float At(float share) => share <= Start ? 0f : share >= End ? 1f : Curve.At((share - Start) / (End - Start));
}

/// <summary>§637: the one morph a bar makes into another (see the top of the file).</summary>
public static class BarMorph
{
    /// <summary>How much smaller a widget that goes ends, and a widget that comes starts: a short way, nothing grows from nothing (§489).</summary>
    public const float Shrunk = 0.92f;

    /// <summary>How far a glide's size may differ, either way: a widget much larger or smaller on the other bar crossfades at a size near its own.</summary>
    public const float MostScale = 2f;

    /// <summary>A widget on both bars, and a ground: its way from where it was to where it is, the whole time.</summary>
    public static readonly MorphStep Glide = new(0f, 1f, Motion.Decelerate);

    /// <summary>A glide's new self coming in over its old: in the first half.</summary>
    public static readonly MorphStep CrossIn = new(0f, 0.5f, Motion.Linear);

    /// <summary>A glide's old self under its new one going: once the new one is nearly there, never a frame with neither.</summary>
    public static readonly MorphStep CrossOut = new(0.4f, 0.8f, Motion.Linear);

    /// <summary>A widget only on the old bar: fades out a little smaller in the first half, as a large surface goes (Hide's 167 ms of 333).</summary>
    public static readonly MorphStep Leave = new(0f, 0.5f, Motion.Accelerate);

    /// <summary>A widget only on the new bar: fades in from a little smaller once the glides are under way, as a large surface comes (Show's 250 ms).</summary>
    public static readonly MorphStep Enter = new(0.25f, 1f, Motion.Decelerate);

    /// <summary>The new ground over the old: in the first half.</summary>
    public static readonly MorphStep GroundIn = new(0f, 0.5f, Motion.Linear);

    /// <summary>The old ground: under the new one, never faster than it comes, gone by three quarters — what stood between a dock's panels is not there long.</summary>
    public static readonly MorphStep GroundOut = new(0.25f, 0.75f, Motion.Linear);

    /// <summary>
    /// Whether a bar laid out as <paramref name="before"/> morphs into <paramref name="now"/>: another layout (its id), another
    /// type (a taskbar's islands, a dock's floating panels) or another edge. The same bar laid out again — a widget added, another
    /// height, a colour — is §358's: what stays is kept, what moved glides.
    /// </summary>
    public static bool Between(Layout? before, Layout now) =>
        before is not null && (before.LayoutId != now.LayoutId || before.Style != now.Style || before.Edge != now.Edge);

    /// <summary>
    /// The plan from <paramref name="from"/> to <paramref name="to"/>. A widget glides to the same widget (its key) where the new bar
    /// has it, else to the next of its kind along the bar not taken yet; the rest go or come. Each old ground goes to the new one
    /// nearest it; a new ground starts as the old ones that go to it, together (a dock's panels merging into a strip), or as the
    /// old one nearest it where none does (a strip splitting into panels) — and each old ground ends as the new ones it went to.
    /// </summary>
    public static MorphPlan Plan(BarPicture from, BarPicture to)
    {
        var taken = new bool[from.Parts.Count];
        var paired = new int[to.Parts.Count];
        Array.Fill(paired, -1);
        // The same widget first, wherever it stands.
        for (var now = 0; now < to.Parts.Count; now++)
        {
            for (var was = 0; was < from.Parts.Count; was++)
            {
                if (!taken[was] && from.Parts[was].Key == to.Parts[now].Key)
                {
                    (taken[was], paired[now]) = (true, was);
                    break;
                }
            }
        }
        // Then the next of its kind along the bar.
        for (var now = 0; now < to.Parts.Count; now++)
        {
            if (paired[now] >= 0)
            {
                continue;
            }
            for (var was = 0; was < from.Parts.Count; was++)
            {
                if (!taken[was] && from.Parts[was].Kind == to.Parts[now].Kind)
                {
                    (taken[was], paired[now]) = (true, was);
                    break;
                }
            }
        }
        var glides = new List<(int, int)>();
        var entering = new List<int>();
        for (var now = 0; now < to.Parts.Count; now++)
        {
            if (paired[now] >= 0)
            {
                glides.Add((paired[now], now));
            }
            else
            {
                entering.Add(now);
            }
        }
        var leaving = Enumerable.Range(0, from.Parts.Count).Where(was => !taken[was]).ToList();
        var (groundsFrom, groundsTo) = Grounds(from.Grounds, to.Grounds);
        return new MorphPlan(glides, leaving, entering, groundsFrom, groundsTo);
    }

    /// <summary>Where each new ground starts, and where each old one ends (see <see cref="Plan"/>).</summary>
    private static (List<MorphGround> From, List<MorphGround> To) Grounds(IReadOnlyList<MorphGround> old, IReadOnlyList<MorphGround> now)
    {
        // Each old ground goes to the new one nearest it.
        var goesTo = old.Select(ground => Nearest(ground.Rect, now)).ToArray();
        var starts = new List<MorphGround>();
        var sources = new List<List<int>>();
        for (var at = 0; at < now.Count; at++)
        {
            var mine = Enumerable.Range(0, old.Count).Where(was => goesTo[was] == at).ToList();
            if (mine.Count == 0 && Nearest(now[at].Rect, old) is var nearest and >= 0)
            {
                mine.Add(nearest);
            }
            sources.Add(mine);
        }
        for (var at = 0; at < now.Count; at++)
        {
            var mine = sources[at];
            // None at all (a bar that had no ground): it starts where it stands, coming in by its fade alone.
            starts.Add(mine.Count == 0
                ? now[at]
                : now[at] with { Rect = Share(Hull(mine.Select(was => old[was].Rect)), now[at].Rect, now.Where((_, other) => other != at && sources[other].SequenceEqual(mine)).Select(other => other.Rect)), Radius = old[mine[0]].Radius });
        }
        var ends = new List<MorphGround>();
        for (var was = 0; was < old.Count; was++)
        {
            var mine = Enumerable.Range(0, now.Count).Where(at => sources[at].Contains(was)).ToList();
            ends.Add(mine.Count == 0
                ? old[was]
                : old[was] with { Rect = Share(Hull(mine.Select(at => now[at].Rect)), old[was].Rect, Enumerable.Range(0, old.Count).Where(other => other != was && goesTo[other] == goesTo[was] && mine.Count == 1).Select(other => old[other].Rect)), Radius = now[mine[0]].Radius });
        }
        return (starts, ends);
    }

    /// <summary>
    /// §705: the part of <paramref name="whole"/> that is <paramref name="mine"/>'s where it shares it with <paramref name="others"/>
    /// (a dock's panels merging into one strip, or a strip splitting into them): cut along the bar at the middles of the gaps
    /// between them, each its own stretch of the strip — never every panel growing over the whole strip, the gaps between them
    /// shut at once by panels in full sight. Alone, the whole.
    /// </summary>
    internal static RectF Share(RectF whole, RectF mine, IEnumerable<RectF> others)
    {
        var across = whole.Width >= whole.Height;
        float Lo(RectF rect) => across ? rect.X : rect.Y;
        float Hi(RectF rect) => across ? rect.Right : rect.Bottom;
        var (lo, hi) = (Lo(whole), Hi(whole));
        foreach (var other in others)
        {
            if (Hi(other) <= Lo(mine))
            {
                lo = MathF.Max(lo, (Hi(other) + Lo(mine)) / 2f);
            }
            else if (Lo(other) >= Hi(mine))
            {
                hi = MathF.Min(hi, (Hi(mine) + Lo(other)) / 2f);
            }
        }
        if (hi <= lo)
        {
            return whole;
        }
        return across ? whole with { X = lo, Width = hi - lo } : whole with { Y = lo, Height = hi - lo };
    }

    /// <summary>The one of <paramref name="grounds"/> whose middle is nearest <paramref name="rect"/>'s; -1 for none.</summary>
    private static int Nearest(RectF rect, IReadOnlyList<MorphGround> grounds)
    {
        var (best, far) = (-1, float.MaxValue);
        for (var at = 0; at < grounds.Count; at++)
        {
            var distance = Vector2.DistanceSquared(rect.Center, grounds[at].Rect.Center);
            if (distance < far)
            {
                (best, far) = (at, distance);
            }
        }
        return best;
    }

    /// <summary>The least rectangle holding every one of <paramref name="rects"/>.</summary>
    public static RectF Hull(IEnumerable<RectF> rects)
    {
        var (left, top, right, bottom) = (float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
        foreach (var rect in rects)
        {
            (left, top, right, bottom) = (MathF.Min(left, rect.X), MathF.Min(top, rect.Y), MathF.Max(right, rect.Right), MathF.Max(bottom, rect.Bottom));
        }
        return left > right ? default : new RectF(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// How large a glide's widget stands at its start, against its own size: the smaller of its two sides' shares, so a widget
    /// wider or narrower on the other bar (a labelled button, a clock of another format) only glides, and one of another height
    /// grows or shrinks to its own; never more than <see cref="MostScale"/> either way.
    /// </summary>
    public static float ScaleFrom(RectF was, RectF now)
    {
        if (now.IsEmpty || was.IsEmpty)
        {
            return 1f;
        }
        var share = MathF.Min(was.Width / now.Width, was.Height / now.Height);
        return Math.Clamp(share, 1f / MostScale, MostScale);
    }

    /// <summary><paramref name="rect"/>, <paramref name="by"/> px further.</summary>
    public static RectF Moved(RectF rect, Vector2 by) => rect with { X = rect.X + by.X, Y = rect.Y + by.Y };

    /// <summary>
    /// §637: whether a bar holding <paramref name="held"/> px across its edge asks for the <paramref name="wanted"/> it is laid out to
    /// hold now, a morph running (<paramref name="morphing"/>) or not. A bar growing as it morphs holds its new strip only once it
    /// has grown — the windows over it move once, onto a bar already there, never leaving a strip of the desktop showing while it
    /// grows into it; one shrinking lets go at the start, the windows moving once under a bar still thick that shrinks off them.
    /// </summary>
    public static bool HoldsNow(bool morphing, int held, int wanted) => !morphing || wanted <= held;
}

/// <summary>
/// §637: a morph running on one bar's window: the scene it left (closed once it is over), the new scene, and the compositor's
/// batch whose end ends it (<see cref="BarMorph"/>). <see cref="End"/> is the one way it ends — the batch over, the bar laid out
/// again first, the window gone —; once.
/// </summary>
internal sealed class Morphing
{
    private Scene? leaving;
    private readonly Scene coming;
    private CompositionScopedBatch? batch;
    private Action? over;

    private Morphing(Scene leaving, Scene coming, Action over)
    {
        this.leaving = leaving;
        this.coming = coming;
        this.over = over;
    }

    /// <summary>The plan it runs.</summary>
    public MorphPlan Plan { get; private set; } = null!;

    /// <summary>Whether it is still running.</summary>
    public bool Running => leaving is not null;

    /// <summary>
    /// The morph from <paramref name="leaving"/> — the bar as it stood, its picture <paramref name="from"/>, already in the new
    /// scene's window px — to <paramref name="coming"/>, laid out already, by <paramref name="motion"/>. <paramref name="over"/> once
    /// it has ended (<see cref="End"/>), on the thread that started it.
    /// </summary>
    public static Morphing Start(Compositor compositor, Scene leaving, BarPicture from, Scene coming, Motion motion, Action over)
    {
        var morph = new Morphing(leaving, coming, over);
        var to = coming.Picture();
        morph.Plan = BarMorph.Plan(from, to);
        CompositionScopedBatch? batch = null;
        try
        {
            batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Report("Morph: no batch to wait on", error);
        }
        leaving.MorphOut(morph.Plan, from, to, motion);
        coming.MorphIn(morph.Plan, from, to, motion);
        if (batch is null)
        {
            morph.End();
            return morph;
        }
        morph.batch = batch;
        batch.End();
        // The batch's end comes on whatever thread the compositor says it on: the morph ends on this one's queue.
        var queue = Windows.System.DispatcherQueue.GetForCurrentThread();
        if (queue is null)
        {
            morph.End();
            return morph;
        }
        batch.Completed += (_, _) => queue.TryEnqueue(() => Troubles.Guard("Morph: its end", morph.End));
        return morph;
    }

    /// <summary>Over: the new scene standing where it ends, the old one let go of, the window told. Once; nothing after the first.</summary>
    public void End()
    {
        if (leaving is not { } gone)
        {
            return;
        }
        leaving = null;
        // §701: whatever the new scene's settling throws, the old one goes and the window is told — never a morph held over.
        try
        {
            coming.MorphOver();
        }
        catch (ObjectDisposedException)
        {
            // The window went with its scene meanwhile.
        }
        finally
        {
            try
            {
                ((IDisposable)gone).Dispose();
                batch?.Dispose();
                batch = null;
            }
            finally
            {
                var then = over;
                over = null;
                then?.Invoke();
            }
        }
    }
}
