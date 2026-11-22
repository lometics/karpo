// A stopwatch as numbers, and the stopwatches a dock is running: the person, "add a stopwatch
// widget as well, in the same style as the timer and focus widget" (2026-10-10).
//
// A stopwatch is a value, as a countdown is (Countdown.cs): what it has banked, since when it has
// been running, and its laps. Nothing in it reads a clock by itself; it is handed the time, so
// every rule has a test (Karpo.Tests\Calendars\StopwatchTests.cs). The time it is handed is a
// monotonic one (StopwatchClock), never the wall clock: a stopwatch measures how long, and the
// wall clock jumps when Windows sets it or the zone changes.
//
// Each stopwatch widget counts on its own, kept by its id in the dock's store, which every face of
// it reads — its entry on the bar, its panel, its line in a folder — so a start on one is a start
// on all of them, and closing the panel leaves it running. In memory, as a countdown is: a restart
// of Karpo clears it.

using System.Collections.Immutable;
using System.Globalization;

namespace Karpo.Dock;

public enum StopwatchPhase
{
    /// <summary>Never started, or reset: nought, and no laps.</summary>
    Idle,
    Running,
    Paused,
}

/// <param name="Banked">What it had counted when it last stopped running.</param>
/// <param name="Since">When it last started running, on <see cref="StopwatchClock"/>.</param>
/// <param name="Laps">Where each lap ended, from the start, oldest first: totals, not lengths.</param>
internal readonly record struct StopwatchState(StopwatchPhase Phase, TimeSpan Banked, TimeSpan Since, ImmutableArray<TimeSpan> Laps)
{
    /// <summary>The most laps it keeps; a lap past them is not taken.</summary>
    public const int MostLaps = 999;

    /// <summary>The most it counts to: past this it reads this, as its figures hold no more.</summary>
    public static readonly TimeSpan Longest = new TimeSpan(99, 59, 59) + TimeSpan.FromMilliseconds(990);

    public static StopwatchState None => new(StopwatchPhase.Idle, TimeSpan.Zero, TimeSpan.Zero, []);

    public bool Running => Phase == StopwatchPhase.Running;

    public bool Idle => Phase == StopwatchPhase.Idle;

    /// <summary>Its laps, never the default array a <c>default</c> state holds.</summary>
    public ImmutableArray<TimeSpan> Splits => Laps.IsDefault ? [] : Laps;

    /// <summary>What it has counted at <paramref name="now"/>: never less than nought, never back in time, never past <see cref="Longest"/>.</summary>
    public TimeSpan Elapsed(TimeSpan now)
    {
        var counted = Banked + (Running && now > Since ? now - Since : TimeSpan.Zero);
        return counted > Longest ? Longest : counted < TimeSpan.Zero ? TimeSpan.Zero : counted;
    }

    /// <summary>Started from nought at <paramref name="now"/>.</summary>
    public static StopwatchState Start(TimeSpan now) => new(StopwatchPhase.Running, TimeSpan.Zero, now, []);

    public StopwatchState Pause(TimeSpan now) =>
        Running ? this with { Phase = StopwatchPhase.Paused, Banked = Elapsed(now), Since = TimeSpan.Zero } : this;

    public StopwatchState Resume(TimeSpan now) =>
        Phase == StopwatchPhase.Paused ? this with { Phase = StopwatchPhase.Running, Since = now } : this;

    /// <summary>
    /// The start button, one rule for every face of it: it starts one that is idle, pauses one that
    /// runs and takes up again one that is paused.
    /// </summary>
    public StopwatchState Act(TimeSpan now) => Phase switch
    {
        StopwatchPhase.Running => Pause(now),
        StopwatchPhase.Paused => Resume(now),
        _ => Start(now),
    };

    /// <summary>A lap ends at <paramref name="now"/>, while it runs: nothing otherwise, nor past <see cref="MostLaps"/>.</summary>
    public StopwatchState Lap(TimeSpan now) =>
        Running && Splits.Length < MostLaps ? this with { Laps = Splits.Add(Elapsed(now)) } : this;

    /// <summary>Back to nought with no laps, whatever it was doing.</summary>
    public static StopwatchState Reset() => None;

    /// <summary>
    /// Lap <paramref name="index"/> (from 0, oldest first): how long it was, and where it ended from
    /// the start. Null past the laps it has.
    /// </summary>
    public (TimeSpan Length, TimeSpan Total)? LapAt(int index)
    {
        var laps = Splits;
        if (index < 0 || index >= laps.Length)
        {
            return null;
        }
        var total = laps[index];
        return (total - (index > 0 ? laps[index - 1] : TimeSpan.Zero), total);
    }

    /// <summary>
    /// How long the lap under way has run at <paramref name="now"/>: since the last lap ended, or
    /// since the start before the first. What a line of one row shows beside its time.
    /// </summary>
    public TimeSpan CurrentLap(TimeSpan now)
    {
        var elapsed = Elapsed(now);
        var laps = Splits;
        var since = laps.Length > 0 ? elapsed - laps[^1] : elapsed;
        return since < TimeSpan.Zero ? TimeSpan.Zero : since;
    }

    /// <summary>Whether it has anything to clear: the reset button is enabled by this.</summary>
    public bool CanReset => !Idle;

    /// <summary>Whether a lap can end now: the lap button is enabled by this.</summary>
    public bool CanLap => Running && Splits.Length < MostLaps;

    public bool Equals(StopwatchState other) =>
        Phase == other.Phase && Banked == other.Banked && Since == other.Since && Splits.SequenceEqual(other.Splits);

    public override int GetHashCode() => HashCode.Combine(Phase, Banked, Since, Splits.Length);
}

/// <summary>The time a stopwatch counts on: monotonic, so setting Windows' clock never moves one.</summary>
internal static class StopwatchClock
{
    public static TimeSpan Now() => System.Diagnostics.Stopwatch.GetElapsedTime(0);
}

/// <summary>How a stopwatch's time is written.</summary>
internal static class StopwatchText
{
    /// <summary>
    /// The widest any reading is, for the room kept for it: hours, minutes, seconds and the tenth, its
    /// figures all one width, so nothing moves as it counts (<see cref="Tenths"/>).
    /// </summary>
    public static string Template(string separator) => $"88:88:88{separator}8";

    /// <summary>
    /// "0:00.0", "12:34.5", "1:02:03.4": minutes, seconds and the tenth — the tenth begun, never
    /// rounded up, as a stopwatch reads — and the hours in front from an hour up. The tenth is set off
    /// by <paramref name="separator"/>, the region's decimal sign (Nls.DecimalSeparator).
    /// </summary>
    public static string Tenths(TimeSpan elapsed, string separator)
    {
        var whole = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > StopwatchState.Longest ? StopwatchState.Longest : elapsed;
        var tenth = (int)(whole.Ticks / (TimeSpan.TicksPerSecond / 10) % 10);
        var culture = CultureInfo.InvariantCulture;
        return whole >= TimeSpan.FromHours(1)
            ? string.Create(culture, $"{(int)whole.TotalHours}:{whole.Minutes:00}:{whole.Seconds:00}{separator}{tenth}")
            : string.Create(culture, $"{whole.Minutes}:{whole.Seconds:00}{separator}{tenth}");
    }

    /// <summary>The widest a Clock app reading is: its hours, minutes and seconds, and its hundredths after the decimal sign (<see cref="Clock"/>).</summary>
    public static (string Main, string Fraction) ClockTemplate(string separator) => ("88:88:88", $"{separator}88");

    /// <summary>
    /// "00:01:23" and ",45": as Windows' Clock app writes a stopwatch — hours, minutes and seconds two
    /// figures each, then the hundredths after the region's decimal sign, which its card draws
    /// smaller. The hundredth begun, never rounded up; never under nought nor past
    /// <see cref="StopwatchState.Longest"/>.
    /// </summary>
    public static (string Main, string Fraction) Clock(TimeSpan elapsed, string separator)
    {
        var whole = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > StopwatchState.Longest ? StopwatchState.Longest : elapsed;
        var hundredth = (int)(whole.Ticks / (TimeSpan.TicksPerSecond / 100) % 100);
        var culture = CultureInfo.InvariantCulture;
        return (string.Create(culture, $"{(int)whole.TotalHours:00}:{whole.Minutes:00}:{whole.Seconds:00}"), string.Create(culture, $"{separator}{hundredth:00}"));
    }

    /// <summary>A lap's length or total as the Clock app's table writes it: "00:00:05.32", all of a size.</summary>
    public static string Lap(TimeSpan elapsed, string separator)
    {
        var (main, fraction) = Clock(elapsed, separator);
        return main + fraction;
    }

    /// <summary>
    /// What an entry on the bar and a clock face show: always five figures, as a countdown's are
    /// (Countdown.Cell), so neither moves as it counts — minutes and seconds ("04:59") under an hour,
    /// hours and minutes ("01:05") from an hour up. Whole seconds: the bar is not redrawn ten times
    /// a second.
    /// </summary>
    public static string Cell(TimeSpan elapsed)
    {
        var whole = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed > StopwatchState.Longest ? StopwatchState.Longest : elapsed;
        var culture = CultureInfo.InvariantCulture;
        return whole >= TimeSpan.FromHours(1)
            ? string.Create(culture, $"{(int)whole.TotalHours:00}:{whole.Minutes:00}")
            : string.Create(culture, $"{whole.Minutes:00}:{whole.Seconds:00}");
    }
}

/// <summary>
/// The stopwatches a dock is running, one per stopwatch widget, kept by its id. Made by the dock and
/// handed round with its countdowns (<see cref="Countdowns.Stopwatches"/>), never a static: a test
/// makes its own. On the dock's thread only.
/// </summary>
internal sealed class Stopwatches
{
    private readonly Dictionary<string, StopwatchState> set = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A stopwatch changed — started, paused, taken up, lapped, reset or dropped: its key.</summary>
    public event Action<string>? Changed;

    public StopwatchState Of(string key) => set.TryGetValue(key, out var state) ? state : StopwatchState.None;

    /// <summary>Whether any of them runs: the dock then ticks a second, for an entry on the bar and a clock face.</summary>
    public bool Running => set.Values.Any(state => state.Running);

    public IReadOnlyCollection<string> Keys => set.Keys;

    public void Set(string key, StopwatchState next)
    {
        if (next == Of(key))
        {
            return;
        }
        if (next.Idle)
        {
            set.Remove(key);
        }
        else
        {
            set[key] = next;
        }
        Changed?.Invoke(key);
    }

    public void Act(string key, TimeSpan now) => Set(key, Of(key).Act(now));

    public void Lap(string key, TimeSpan now) => Set(key, Of(key).Lap(now));

    public void Reset(string key) => Set(key, StopwatchState.Reset());

    /// <summary>Only the stopwatches of <paramref name="alive"/> stay: a widget removed takes its stopwatch with it.</summary>
    public void Keep(IEnumerable<string> alive)
    {
        var kept = new HashSet<string>(alive, StringComparer.OrdinalIgnoreCase);
        foreach (var key in set.Keys.Where(key => !kept.Contains(key)).ToList())
        {
            set.Remove(key);
            Changed?.Invoke(key);
        }
    }
}
