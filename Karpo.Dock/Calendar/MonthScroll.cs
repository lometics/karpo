// The month's weeks scrolled (docs\widgets.md §424): where they stand, in weeks from an origin, as Windows 11's
// calendar flyout scrolls them — WinUI's CalendarView, whose month view is a ScrollViewer with a snap point on each
// month's first week (CalendarPanel's snap point filter, GetIsFirstItemInScope).
//
//  - **A wheel's notch, an arrow** glide to the month after or before the one the scroll is going to, aligned (its
//    first week at the top, as a month at rest always stood), eased on Windows' decelerate curve in GlideMs; a notch
//    before the last landed goes on from where that one was going.
//  - **A touchpad** moves the weeks by what it says, at once; SettleMs after its last move — a precision touchpad's
//    own inertia is a run of small moves, so this is after the fling has spent itself — they glide to the nearest
//    month's alignment, the snap point Windows' manipulation would have ended on.
//  - **The heading** is the month whose share of its own days in view is the greatest, a tie the earlier's
//    (CalendarViewGeneratorHost.UpdateScope); the other months' days are dimmed by it.
//
// Pure: the time is handed in (ms), and what glides is answered as a Glide, which the line starts on the compositor
// (MonthTile.Glided), so what the compositor shows and what this says agree at every moment — the hit tests and the
// heading read At(now).

using Karpo.Core;

namespace Karpo.Dock;

internal sealed class MonthScroll
{
    /// <summary>§489: how fast the weeks glide: the look's motion (MonthTile sets it); Normal until it does.</summary>
    public Motion Motion { get; set; } = Motion.Normal;

    /// <summary>How long a glide to a month takes, ms: a calendar's glide (Motion.cs).</summary>
    internal int GlideMs => (int)Motion.Ms(MotionKind.Glide);

    /// <summary>How long a touchpad has not moved the weeks before they settle on a month, ms.</summary>
    internal const int SettleMs = 120;

    /// <summary>The weeks a month shows: Windows' six.</summary>
    internal const int Weeks = 6;

    // The calendar's ends: no month whose six weeks would leave it.
    private static readonly DateOnly Earliest = new(1, 2, 1);
    private static readonly DateOnly Latest = new(9999, 11, 1);

    /// <summary>A glide from <paramref name="From"/> to <paramref name="To"/> (weeks from the origin), started at <paramref name="Start"/> ms, <paramref name="Ms"/> long.</summary>
    internal readonly record struct Glide(float From, float To, long Start, int Ms)
    {
        public float At(long now) => now >= Start + Ms ? To : From + ((To - From) * Eased(Math.Clamp((now - Start) / (float)Ms, 0, 1)));
    }

    // Where the weeks stand where nothing glides: the last glide's end, or where the touchpad left them.
    private float rest;
    private Glide? gliding;
    private long? panned;

    /// <summary>At rest on <paramref name="month"/>, its weeks starting on <paramref name="first"/>.</summary>
    public MonthScroll(DateOnly month, DayOfWeek first)
    {
        First = first;
        Origin = CalendarNavigation.WeekStart(Clamped(month), first);
    }

    /// <summary>The first day of the week the positions count from: position 0 is its week at the top.</summary>
    public DateOnly Origin { get; }

    /// <summary>The day the weeks start on.</summary>
    public DayOfWeek First { get; }

    /// <summary>The glide on its way, if any.</summary>
    public Glide? Gliding => gliding;

    /// <summary>Whether the weeks are on their way somewhere: gliding, or moved by a touchpad and not settled yet.</summary>
    public bool Moving => gliding is not null || panned is not null;

    /// <summary>The month the scroll is going to: the glide's end's, else the heading where it stands.</summary>
    public DateOnly Target => HeadingAt(gliding?.To ?? rest);

    /// <summary>Where the weeks stand at <paramref name="now"/>, in weeks from <see cref="Origin"/> (the week at the top).</summary>
    public float At(long now) => gliding is { } glide ? glide.At(now) : rest;

    /// <summary>The month heading the weeks at <paramref name="now"/>.</summary>
    public DateOnly Heading(long now) => HeadingAt(At(now));

    /// <summary>Where <paramref name="month"/> is aligned: its first week at the top.</summary>
    public float Aligned(DateOnly month) => WeekOf(First1(Clamped(month)));

    /// <summary>The week <paramref name="day"/> is in, from the origin's.</summary>
    public int WeekOf(DateOnly day) => (int)Math.Floor((day.DayNumber - Origin.DayNumber) / 7.0);

    /// <summary>The first day of week <paramref name="week"/> from the origin's.</summary>
    public DateOnly WeekStart(int week) => DateOnly.FromDayNumber(Origin.DayNumber + (7 * week));

    /// <summary>
    /// The month heading the weeks with <paramref name="position"/> at the top: of the months in view, the one with
    /// the greatest share of its own days shown — a row partly in view by as much of it as shows —, a tie the earlier's.
    /// </summary>
    public DateOnly HeadingAt(float position)
    {
        var shares = new List<(DateOnly Month, float Days)>(3);
        var top = (int)MathF.Floor(position);
        for (var week = top; week < position + Weeks; week++)
        {
            var shows = MathF.Min(week + 1, position + Weeks) - MathF.Max(week, position);
            if (shows <= 0)
            {
                continue;
            }
            var start = Clamp(WeekStart(week));
            for (var day = 0; day < 7; day++)
            {
                var date = DateOnly.FromDayNumber(Math.Min(start.DayNumber + day, DateOnly.MaxValue.DayNumber));
                var month = First1(date);
                var at = shares.FindIndex(share => share.Month == month);
                if (at < 0)
                {
                    shares.Add((month, shows));
                }
                else
                {
                    shares[at] = (month, shares[at].Days + shows);
                }
            }
        }
        var best = shares[0];
        foreach (var share in shares.Skip(1))
        {
            if (share.Days / DateTime.DaysInMonth(share.Month.Year, share.Month.Month) > best.Days / DateTime.DaysInMonth(best.Month.Year, best.Month.Month) + 1e-4f)
            {
                best = share;
            }
        }
        return Clamped(best.Month);
    }

    /// <summary><paramref name="months"/> on from the month the scroll is going to (a notch, an arrow): the glide there, or none where it landed at once.</summary>
    public Glide? Step(int months, long now, bool animate) => To(Shifted(Target, months), now, animate, always: true);

    /// <summary>To <paramref name="month"/>, aligned (a day picked, today again): the glide there; none where it is going there already, or landed at once.</summary>
    public Glide? To(DateOnly month, long now, bool animate) => To(month, now, animate, always: false);

    private Glide? To(DateOnly month, long now, bool animate, bool always)
    {
        var to = Aligned(month);
        if (!always && (gliding?.To ?? rest) == to && panned is null)
        {
            return null;
        }
        return GlideTo(to, now, animate);
    }

    /// <summary>A touchpad moved the weeks by <paramref name="weeks"/> (down is on): from where they are, a glide left where it had got to.</summary>
    public void Pan(float weeks, long now)
    {
        rest = Math.Clamp(At(now) + weeks, Aligned(Earliest), Aligned(Latest));
        gliding = null;
        panned = now;
    }

    /// <summary>
    /// The scroll at <paramref name="now"/>: a glide that has landed is at rest; a touchpad quiet for
    /// <see cref="SettleMs"/> settles on the nearest month — the glide that starts, or null.
    /// </summary>
    public Glide? Tick(long now, bool animate = true)
    {
        if (gliding is { } glide && now >= glide.Start + glide.Ms)
        {
            rest = glide.To;
            gliding = null;
        }
        if (panned is { } last && now - last >= SettleMs)
        {
            return GlideTo(Nearest(rest), now, animate);
        }
        return null;
    }

    /// <summary>The month alignment nearest <paramref name="position"/>: the snap point a manipulation ends on.</summary>
    public float Nearest(float position)
    {
        var heading = HeadingAt(position);
        var best = Aligned(heading);
        foreach (var month in new[] { Shifted(heading, -1), Shifted(heading, 1) })
        {
            if (MathF.Abs(Aligned(month) - position) < MathF.Abs(best - position))
            {
                best = Aligned(month);
            }
        }
        return best;
    }

    private Glide? GlideTo(float to, long now, bool animate)
    {
        var from = At(now);
        panned = null;
        if (!animate || from == to)
        {
            rest = to;
            gliding = null;
            return null;
        }
        var glide = new Glide(from, to, now, GlideMs);
        gliding = glide;
        rest = to;
        return glide;
    }

    /// <summary>§489: the one motion's decelerate curve (Motion.Decelerate) at <paramref name="x"/> of the time, as the compositor runs it.</summary>
    public static float Eased(float x) => Motion.Decelerate.At(x);

    private static DateOnly First1(DateOnly day) => new(day.Year, day.Month, 1);

    private static DateOnly Clamped(DateOnly month)
    {
        var first = First1(month);
        return first < Earliest ? Earliest : first > Latest ? Latest : first;
    }

    private static DateOnly Clamp(DateOnly day) => day < DateOnly.MinValue ? DateOnly.MinValue : day;

    /// <summary><paramref name="month"/> moved by <paramref name="months"/>, held between the calendar's ends.</summary>
    private static DateOnly Shifted(DateOnly month, int months)
    {
        var index = Math.Clamp((((long)month.Year * 12) + month.Month - 1) + months, ((long)Earliest.Year * 12) + Earliest.Month - 1, ((long)Latest.Year * 12) + Latest.Month - 1);
        return new DateOnly((int)(index / 12), (int)(index % 12) + 1, 1);
    }
}
