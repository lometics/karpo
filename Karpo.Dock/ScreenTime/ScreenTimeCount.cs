// The counting itself, apart from Windows: what ScreenTimeTracker tells it — a minute's look, the
// window in front, the session locking, the PC going to sleep, a display going dark — and what it
// makes of that, into the days it keeps (ScreenTimeLog). Pure: every call is handed the time and
// when somebody last touched the keyboard or the mouse, so a test drives it through a lock, a
// night's sleep or a midnight without waiting for any of them.
//
// The rule (as before, c8a9db1^): time counts while the session is unlocked, a display is on and
// the PC awake, and somebody was at the keyboard or the mouse within the idle time — five minutes
// (IdleMinutes): a person reading or watching is still at the screen, a person gone for lunch is
// not. A span is counted for the app whose window was in front, and cut at each local midnight so
// the minutes before it count for the day they were spent on.
//
// **What the messages miss, the look puts right.** The lock, the sleep and the display going off
// arrive as window messages, and a message can be lost — the dock was busy in a menu's loop, the
// PC slept before it was delivered. A flag left set would then count nothing for the rest of the
// day. So each look says whether the session is locked now (asked of Windows, not remembered), and
// input after the PC went to sleep or its display went dark means it is awake and lit again: the
// count starts again from that input. A flag left clear the other way costs at most the idle time,
// which the rule already allows.

namespace Karpo.Dock;

internal sealed class ScreenTimeCount
{
    /// <summary>How many days before today the history keeps; older ones are dropped when it is saved.</summary>
    public const int KeptDays = 90;

    /// <summary>
    /// How long without input counts as away. One for the whole PC: the widgets that showed screen
    /// time used to set their own, and two of them could disagree about the same day.
    /// </summary>
    public const int IdleMinutes = 5;

    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(IdleMinutes);

    private readonly TimeZoneInfo zone;
    private bool locked;
    private bool displayOff;
    private bool asleep;

    /// <summary>When the PC went to sleep or its display dark (UTC), for the look to tell it is back.</summary>
    private DateTime awaySince;

    private DateOnly prunedFor;

    /// <summary>
    /// Every idle time's days the files held, by its minutes: <see cref="IdleMinutes"/>' is counted
    /// into, and any other is written back as it was read.
    /// </summary>
    public SortedDictionary<int, ScreenTimeLog> Logs { get; }

    /// <summary>The days counted into.</summary>
    public ScreenTimeLog Log { get; }

    /// <summary>Counting has been done up to here (UTC).</summary>
    public DateTime Counted { get; private set; }

    /// <summary>The app the window in front belongs to (<see cref="ScreenTimeApps.KeyOf"/>), or null.</summary>
    public string? App { get; private set; }

    /// <summary>A day other than today changed since the history was last written.</summary>
    public bool HistoryChanged { get; set; }

    /// <summary>Whether the span since the last count is one that never counts: locked, asleep or dark.</summary>
    public bool Away => locked || displayOff || asleep;

    /// <summary>
    /// The count, from what the files held (<paramref name="read"/>), at <paramref name="now"/>.
    /// Where the files did not count today (<see cref="ScreenTimeRead.Resumed"/>) and the session
    /// began today, it counts from the <paramref name="logon"/>: Karpo may have started long after,
    /// and that time was spent at this screen too.
    /// </summary>
    public ScreenTimeCount(ScreenTimeRead read, TimeZoneInfo zone, DateTime now, DateTime? logon, DateTime input, bool lockedNow)
    {
        this.zone = zone;
        Logs = read.Logs;
        if (!Logs.TryGetValue(IdleMinutes, out var log))
        {
            // A file of another idle time only (one an island set to ten minutes wrote): its days go on
            // here, as changing an island's idle time once carried them.
            log = Nearest(Logs)?.Copy() ?? new ScreenTimeLog();
            Logs[IdleMinutes] = log;
            HistoryChanged = log.Count > 0;
        }
        Log = log;
        HistoryChanged |= read.HistoryStale;
        locked = lockedNow;
        Counted = Utc(now);
        var today = Today(now);
        if (!read.Resumed.Contains(IdleMinutes) && Log.Day(today) is null && logon is { } began
            && ScreenTimeClock.LocalDate(began, zone) == today && Utc(began) < Counted)
        {
            Counted = Utc(began);
            Tally(now, input);
        }
    }

    /// <summary>The local day <paramref name="now"/> is on.</summary>
    public DateOnly Today(DateTime now) => ScreenTimeClock.LocalDate(now, zone);

    /// <summary>
    /// Counts the time since the last count. Somebody counts as present until the idle time has
    /// passed since their last <paramref name="input"/>; a span locked, asleep or dark never counts.
    /// </summary>
    public void Tally(DateTime now, DateTime input)
    {
        now = Utc(now);
        if (now <= Counted)
        {
            // The clock was set back: start from here.
            Counted = now;
            return;
        }
        var from = Counted;
        Counted = now;
        if (Away)
        {
            return;
        }
        var until = Utc(input) + Idle;
        if (until > now)
        {
            until = now;
        }
        if (until <= from)
        {
            return;
        }
        // The log splits a span over midnight between the two days; minutes that go to a day
        // before today change the history.
        if (Log.Add(from, until, App, zone) is { } first && first != Today(now))
        {
            HistoryChanged = true;
        }
    }

    /// <summary>Another window came to the front: what went before is the last app's, what follows <paramref name="app"/>'s.</summary>
    public void Foreground(DateTime now, DateTime input, string? app)
    {
        Tally(now, input);
        App = app;
    }

    /// <summary>The session locked: counted up to now, and nothing until it is unlocked.</summary>
    public void Lock(DateTime now, DateTime input)
    {
        Tally(now, input);
        locked = true;
    }

    /// <summary>The session unlocked: counting starts again now.</summary>
    public void Unlock(DateTime now, DateTime input) => Resume(now, input, () => locked = false);

    /// <summary>The PC goes to sleep: counted up to now, and nothing until it wakes.</summary>
    public void Sleep(DateTime now, DateTime input)
    {
        Tally(now, input);
        asleep = true;
        awaySince = Utc(now);
    }

    /// <summary>The PC woke: counting starts again now.</summary>
    public void Wake(DateTime now, DateTime input) => Resume(now, input, () => asleep = false);

    /// <summary>The displays went dark (a dimmed display is still looked at): counted up to now, then nothing.</summary>
    public void Dark(DateTime now, DateTime input)
    {
        Tally(now, input);
        if (!displayOff)
        {
            displayOff = true;
            awaySince = Utc(now);
        }
    }

    /// <summary>A display is on again: counting starts again now.</summary>
    public void Lit(DateTime now, DateTime input) => Resume(now, input, () => displayOff = false);

    /// <summary>
    /// The minute's look: whether the session is locked is <paramref name="lockedNow"/>, whatever
    /// the messages said; input since the PC went to sleep or dark means it is back, counted from
    /// that input; then the count up to now, and the days past the kept ones dropped once a day.
    /// </summary>
    public void Look(DateTime now, DateTime input, bool lockedNow)
    {
        if (lockedNow && !locked)
        {
            Lock(now, input);
        }
        else if (!lockedNow && locked)
        {
            Unlock(now, input);
        }
        if ((asleep || displayOff) && Utc(input) > awaySince)
        {
            Tally(now, input);
            (asleep, displayOff) = (false, false);
            var back = Utc(input);
            Counted = back < Counted ? back : Counted;
        }
        Tally(now, input);
        var today = Today(now);
        if (prunedFor != today)
        {
            prunedFor = today;
            foreach (var log in Logs.Values)
            {
                HistoryChanged |= log.Prune(today, KeptDays);
            }
        }
    }

    /// <summary>A copy of the days, today's counted up to <paramref name="now"/>.</summary>
    public ScreenTimeSnapshot Snapshot(DateTime now, DateTime input)
    {
        Tally(now, input);
        return new ScreenTimeSnapshot(Today(now), Log.Copy());
    }

    /// <summary>Today's seconds: whether today's file has anything new.</summary>
    public long Seconds(DateOnly today) => (long)Log.TotalOn(today).TotalSeconds;

    /// <summary>Counts up to now, then starts again from now after a span that did not count.</summary>
    private void Resume(DateTime now, DateTime input, Action clear)
    {
        Tally(now, input);
        clear();
        Counted = Utc(now);
    }

    /// <summary>Of the logs with days, the one whose idle time is closest to <see cref="IdleMinutes"/> (the shorter of two as close).</summary>
    private static ScreenTimeLog? Nearest(SortedDictionary<int, ScreenTimeLog> logs)
    {
        ScreenTimeLog? best = null;
        var bestIdle = 0;
        foreach (var (idle, log) in logs)
        {
            if (log.Count > 0 && (best is null || Math.Abs(idle - IdleMinutes) < Math.Abs(bestIdle - IdleMinutes)))
            {
                (best, bestIdle) = (log, idle);
            }
        }
        return best;
    }

    private static DateTime Utc(DateTime value) => ScreenTimeClock.Utc(value);
}
