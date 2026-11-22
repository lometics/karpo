// §489: Karpo's one motion (Karpo.Dock\Motion.cs; the person, 2026-11-02: "only one default animation for everything in karpo
// ... it should be able to be turned off so that there are no animations at all and the speed should be selectable as well
// between three levels"). The engine's times and curves per kind; the three speeds scaling every one of them by one factor;
// off (Karpo's switch or Windows' Animation effects) nothing at all; the popup plan every popup comes in and goes by, per
// edge; the clock a close in the middle of an opening turns back on; and that nothing else in Karpo names a time or a curve.

using System.Numerics;
using System.Text.RegularExpressions;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed partial class MotionTests
{
    private static readonly MotionKind[] Kinds = Enum.GetValues<MotionKind>();

    /// <summary>
    /// Windows 11's times at Normal: a hover's and a press's 83 ms, a move's and a swap's 167, a popup's 200 in and 117 out, a
    /// large surface's 250 in and 167 out, a calendar's glide 250, §637 a bar morphing into another 333; what comes in decelerates,
    /// what goes accelerates, a wash fades straight. Every one of them short: nothing in Karpo takes longer than a third of a second.
    /// </summary>
    [Fact]
    public void EachKindHasItsTimeAndItsCurve()
    {
        var normal = Motion.Normal;
        Assert.Equal(
            [83d, 83d, 167d, 167d, 200d, 117d, 250d, 167d, 250d, 333d],
            Kinds.Select(normal.Ms));
        Assert.Equal(Motion.Linear, Motion.CurveOf(MotionKind.Hover));
        Assert.Equal(Motion.Accelerate, Motion.CurveOf(MotionKind.Close));
        Assert.Equal(Motion.Accelerate, Motion.CurveOf(MotionKind.Hide));
        foreach (var kind in new[] { MotionKind.Press, MotionKind.Move, MotionKind.Swap, MotionKind.Open, MotionKind.Show, MotionKind.Glide, MotionKind.Morph })
        {
            Assert.Equal(Motion.Decelerate, Motion.CurveOf(kind));
        }
        Assert.All(Kinds.Where(kind => kind != MotionKind.Morph), kind => Assert.InRange(normal.Ms(kind), 80, 250));
        // §637: a bar morphing into another, the width of a display: Fluent's point-to-point third of a second, the one longer.
        Assert.InRange(normal.Ms(MotionKind.Morph), 300, 350);
        Assert.Equal(new Vector2(0, 0), Motion.Decelerate.One);
        Assert.Equal(new Vector2(0, 1), Motion.Decelerate.Two);
        Assert.Equal((new Vector2(0.7f, 0), new Vector2(1, 0.5f)), (Motion.Accelerate.One, Motion.Accelerate.Two));
        Assert.InRange(Motion.ShiftDip, 8, 16);
    }

    /// <summary>The three speeds: every time 1.5 times as long Slower, two thirds Faster, whole ms; Normal the default, in the look and the config alike.</summary>
    [Fact]
    public void TheSpeedScalesEveryTimeByOneFactor()
    {
        Assert.Equal((1.5, 1d, 2 / 3.0), (Motion.Factor(Animation.Slower), Motion.Factor(Animation.Normal), Motion.Factor(Animation.Faster)));
        var (slower, normal, faster) = (new Motion(true, Animation.Slower), Motion.Normal, new Motion(true, Animation.Faster));
        foreach (var kind in Kinds)
        {
            Assert.Equal(Math.Round(normal.Ms(kind) * 1.5), slower.Ms(kind));
            Assert.Equal(Math.Round(normal.Ms(kind) * 2 / 3), faster.Ms(kind));
            Assert.True(slower.Ms(kind) > normal.Ms(kind) && normal.Ms(kind) > faster.Ms(kind), $"{kind}");
            Assert.Equal(TimeSpan.FromMilliseconds(slower.Ms(kind)), slower.Time(kind));
        }
        Assert.Equal(Animation.Normal, new Config().Animation);
        Assert.Equal(Animation.Normal, new Look(Glass: false, Dark: false, Accent: default).Speed);
        Assert.Equal(Motion.Normal, new Look(Glass: false, Dark: false, Accent: default).Motion);
    }

    /// <summary>
    /// Off — Karpo's Animations set to Off (§549), or Windows' Animation effects whatever Karpo's says — nothing takes any time: every
    /// surface stands where it ends at once, popups come and go at once, nothing shifts.
    /// </summary>
    [Fact]
    public void OffIsNothingAtAllAndWindowsOffIsOffWhateverKarpoSays()
    {
        var on = new Config();
        var off = new Config { Animation = Animation.Off };
        Assert.True(Motion.Of(windows: true, on).On);
        Assert.False(Motion.Of(windows: true, off).On);
        Assert.False(Motion.Of(windows: false, on).On);
        Assert.Equal(new Motion(true, Animation.Faster), Motion.Of(windows: true, new Config { Animation = Animation.Faster }));
        foreach (var still in new[] { Motion.Still, Motion.Of(windows: false, on), Motion.Of(windows: true, off) })
        {
            Assert.All(Kinds, kind => Assert.Equal(0, still.Ms(kind)));
            Assert.Equal(0, still.Shift(1.5f));
            Assert.Same(PopupMotion.None, PopupMotion.Of(still));
            Assert.Same(PopupMotion.None, PopupMotion.Of(still, PopupEntrance.Bar));
        }
        // The look the dock reads Windows' into, as Karpo moves (Look.Moving).
        var windows = new Look(Glass: false, Dark: false, Accent: default);
        Assert.False(windows.Moving(off).Motion.On);
        Assert.False((windows with { Animations = false }).Moving(on).Motion.On);
        Assert.Equal(new Motion(true, Animation.Faster), windows.Moving(new Config { Animation = Animation.Faster }).Motion);
    }

    /// <summary>
    /// §549: each of Animations' choices runs at the times §489's switch and speed gave it: Off none at all, Slower 1.5 times
    /// Normal's, Faster two thirds — a popup's 200 ms in 300, 200, 133 —; Windows' Animation effects off none whatever is chosen.
    /// </summary>
    [Theory]
    [InlineData(Animation.Off, 0d, 0d)]
    [InlineData(Animation.Slower, 300d, 124d)]
    [InlineData(Animation.Normal, 200d, 83d)]
    [InlineData(Animation.Faster, 133d, 55d)]
    public void EachAnimationsChoiceRunsAtItsTimes(Animation animation, double open, double hover)
    {
        var config = new Config { Animation = animation };
        var motion = Motion.Of(windows: true, config);
        Assert.Equal((animation != Animation.Off, open, hover), (motion.On, motion.Ms(MotionKind.Open), motion.Ms(MotionKind.Hover)));
        Assert.Equal(motion.Ms(MotionKind.Open), new Look(Glass: false, Dark: false, Accent: default).Moving(config).Motion.Ms(MotionKind.Open));
        Assert.False(Motion.Of(windows: false, config).On);
        Assert.All(Kinds, kind => Assert.Equal(0, Motion.Of(windows: false, config).Ms(kind)));
    }

    /// <summary>
    /// Every popup by the one plan: a menu, a flyout and a folder rise 12 DIP from their side fading in over 200 ms and go back
    /// over 117; a tooltip fades where it stands over 83 each way; the edit bar rises over 250 and goes over 167. A folder
    /// opened for another is there at once (and still goes by the plan); arranged it fades where it stands.
    /// </summary>
    [Fact]
    public void EveryPopupComesInAndGoesByTheOnePlan()
    {
        var normal = Motion.Normal;
        static (double, double, float) Of(PopupMotion plan) => (plan.Enter.TotalMilliseconds, plan.Exit.TotalMilliseconds, plan.ShiftDip);
        var popup = PopupMotion.Of(normal);
        Assert.Equal((200d, 117d, 12f), Of(popup));
        Assert.Equal((Motion.Decelerate, Motion.Accelerate), (popup.EnterCurve, popup.ExitCurve));
        Assert.Equal((83d, 83d, 0f), Of(PopupMotion.Of(normal, PopupEntrance.Tip)));
        Assert.Equal((Motion.Linear, Motion.Linear), (PopupMotion.Of(normal, PopupEntrance.Tip).EnterCurve, PopupMotion.Of(normal, PopupEntrance.Tip).ExitCurve));
        Assert.Equal((250d, 167d, 12f), Of(PopupMotion.Of(normal, PopupEntrance.Bar)));
        Assert.Equal((0d, 117d, 12f), Of(PopupMotion.Of(normal, switching: true)));
        Assert.Equal((200d, 117d, 0f), Of(PopupMotion.Of(normal, arranged: true)));
        Assert.Equal((300d, 176d, 12f), Of(PopupMotion.Of(new Motion(true, Animation.Slower))));
        Assert.Equal((133d, 78d, 12f), Of(PopupMotion.Of(new Motion(true, Animation.Faster))));
    }

    /// <summary>Shut, a panel stands toward its bar: up from a bottom bar, down from a top one, out from a side; a submenu toward the item it hangs from. Whole px.</summary>
    [Theory]
    [InlineData(1f, 12)]
    [InlineData(1.25f, 15)]
    [InlineData(1.5f, 18)]
    [InlineData(1.75f, 21)]
    public void ItComesFromItsSide(float scale, int far)
    {
        var plan = PopupMotion.Of(Motion.Normal);
        Assert.Equal(new Vector2(0, far), plan.Away(Edge.Bottom, scale));
        Assert.Equal(new Vector2(0, -far), plan.Away(Edge.Top, scale));
        Assert.Equal(new Vector2(-far, 0), plan.Away(Edge.Left, scale));
        Assert.Equal(new Vector2(far, 0), plan.Away(Edge.Right, scale));
        Assert.Equal(new Vector2(-far, 0), plan.Beside(leftward: false, scale));
        Assert.Equal(new Vector2(far, 0), plan.Beside(leftward: true, scale));
        Assert.Equal(Vector2.Zero, PopupMotion.Of(Motion.Normal, arranged: true).Away(Edge.Bottom, scale));
    }

    /// <summary>
    /// The curves run one way, start at 0 and end at 1; decelerate is fast first (half the way by an eighth of the time,
    /// exactly: its x is t³, its y 3t² − 2t³), accelerate slow first, straight straight.
    /// </summary>
    [Fact]
    public void TheCurvesRunOneWay()
    {
        foreach (var curve in new[] { Motion.Decelerate, Motion.Accelerate, Motion.Linear })
        {
            Assert.Equal(0, curve.At(0), 4);
            Assert.Equal(1, curve.At(1), 4);
            for (var x = 0.05f; x <= 1; x += 0.05f)
            {
                Assert.True(curve.At(x) >= curve.At(x - 0.05f), $"{curve} goes back at {x}");
            }
        }
        Assert.Equal(0.5f, Motion.Decelerate.At(0.125f), 5);
        Assert.True(Motion.Decelerate.At(0.25f) > 0.6f);
        Assert.True(Motion.Accelerate.At(0.5f) < 0.35f);
        Assert.Equal(0.3f, Motion.Linear.At(0.3f), 3);
        Assert.Equal(Motion.Decelerate.At(0.5f), MonthScroll.Eased(0.5f));
    }

    /// <summary>
    /// The clock: shut, it stands at 0; an opening runs it to 1 over the whole time on the enter curve, ending there; a
    /// close goes back to 0 over the exit's time. A close in the middle of an opening starts where the opening had got to
    /// and takes the share of the exit's time that is left — no jump —; an opening again in the middle of a close the same.
    /// </summary>
    [Fact]
    public void AReversalGoesBackFromWhereItIs()
    {
        var plan = PopupMotion.Of(Motion.Normal);
        var clock = new MotionClock();
        Assert.Equal(0, clock.At(0));

        var (from, ms) = clock.Go(1000, open: true, plan);
        Assert.Equal((0f, 200d), (from, ms));
        Assert.Equal(0, clock.At(1000), 3);
        Assert.Equal(1, clock.At(1200), 3);
        Assert.Equal(1, clock.At(5000), 3);
        Assert.Equal(Motion.Decelerate.At(0.25f), clock.At(1050), 3);

        // Shut in the middle: from where it stands, the share of 117 ms that is left of the way.
        var middle = clock.At(1030);
        (from, ms) = clock.Go(1030, open: false, plan);
        Assert.Equal(middle, from, 3);
        Assert.Equal(117 * middle, ms, 1);
        Assert.Equal(middle, clock.At(1030), 3);
        Assert.Equal(0, clock.At(1030 + ms), 3);

        // Opened again in the middle of that: up again from where it got to.
        var back = clock.At(1060);
        (from, ms) = clock.Go(1060, open: true, plan);
        Assert.Equal(back, from, 3);
        Assert.Equal(200 * (1 - back), ms, 1);
        Assert.Equal(1, clock.At(1060 + ms), 3);

        // Set where it is at once (no motion; a showing again of the open popup): there, now.
        clock.Snap(open: true);
        Assert.Equal(1, clock.At(1061));
        (from, ms) = clock.Go(2000, open: true, plan);
        Assert.Equal((1f, 0d), (from, ms));
    }

    /// <summary>
    /// Every surface asks the engine: no file of the dock but Motion.cs makes an easing function or a spring, or gives an
    /// animation a time of its own — every Duration is the engine's (a kind's Time, a lap, what is left of a timed motion), and
    /// none is a number of milliseconds written where it runs. Red before §489: 55 such places in 12 files.
    /// </summary>
    [Fact]
    public void NothingButTheEngineNamesATimeOrACurve()
    {
        var root = RepoSource.Root();
        if (root.Length == 0)
        {
            return;
        }
        var wrong = new List<string>();
        foreach (var path in RepoSource.CSharp("Karpo.Dock"))
        {
            // The engine itself; and what has no compositor (a calendar event's Duration is no animation's).
            if (Path.GetFileName(path) == "Motion.cs" || !File.ReadAllText(path).Contains("Windows.UI.Composition", StringComparison.Ordinal))
            {
                continue;
            }
            var lines = File.ReadAllLines(path);
            for (var at = 0; at < lines.Length; at++)
            {
                var line = lines[at];
                if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }
                var made = MadeCurve().IsMatch(line);
                var timed = Duration().Match(line) is { Success: true } duration && !Asked().IsMatch(duration.Groups[1].Value);
                if (made || timed)
                {
                    wrong.Add($"{Path.GetRelativePath(root, path)}:{at + 1}: {line.Trim()}");
                }
            }
        }
        Assert.True(wrong.Count == 0, "A time or a curve of its own, not the engine's (Karpo.Dock\\Motion.cs):" + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    [GeneratedRegex(@"Create(CubicBezier|Linear|Step)EasingFunction|CreateSpring\w*Animation")]
    private static partial Regex MadeCurve();

    [GeneratedRegex(@"\.Duration\s*=\s*(.+);")]
    private static partial Regex Duration();

    /// <summary>What a Duration may be: the engine's — a kind's time, a loop's lap, what is left of a timed motion — or a time handed down from it.</summary>
    [GeneratedRegex(@"^(\S*\.?Time\(MotionKind\.\w+( : MotionKind\.\w+)?\)|Time\(.*MotionKind.*\)|(Karpo\.Dock\.)?Motion\.(Lap|Span)\(.*\)|time)$")]
    private static partial Regex Asked();
}
