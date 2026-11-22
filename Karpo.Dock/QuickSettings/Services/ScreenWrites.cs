// What the brightness worker sends each display next (Brightness.cs, docs\widgets.md §495): the
// level the person asked for each display, and the one errand at a time that gets it there. Pure —
// no display, no thread, no clock of its own: the caller says what time it is, does the errand, and
// says how it went. The tests drive it with made-up displays that are slow, drop what they are sent
// and refuse; the brightness worker drives it with the PC's own.
//
// Why it exists (the person, 2026-11-03: "sometimes when changing the brightness fast not all of my
// displays can keep up, sometimes one of the two will then just stay at a lower or higher
// brightness"). A monitor's DDC/CI takes a command over a slow wire and needs a rest after it — the
// standard's 50 ms after a Set VCP before the next message — and one sent sooner is dropped without a
// word: SetVCPFeature says it went, the monitor never applies it. Brightness used to write a display
// and read it back at once, inside that rest, and believe the read whatever it said: a dropped last
// write read back as the level before, and was taken as "the monitor rounded it". Nothing ever sent
// the last level again, and a write that failed outright was forgotten the same way.
//
// The rules, per display:
//   * One errand at a time. A display with a write or a read on its way is sent nothing more until it
//     comes back, and then nothing until its rest (the gap the caller says it needs) is over.
//   * The newest level wins. Asking only records the level; when the display is free it is sent the
//     newest, and every level asked in between is never sent at all.
//   * Confirmed, not hoped. Once nothing was asked or written for SettleMs, the display is read back.
//     The level asked, as near as the display can show it (the caller says: a panel's own levels, a
//     monitor's range): done. Another: it is sent again, after a growing wait, at most Attempts
//     writes in all. No guessing at what a display rounds to from what it answers — two dropped
//     writes in a row would look the same. No answer: read again, at most Reads times, and then the
//     level asked stands, as it was taken.
//   * Bounded, and said. A display that keeps failing — writes refused, levels not taken, no answer —
//     stops being sent anything for that level, and the trouble is said once (karpo.log), until it
//     takes one again. A new level asked starts afresh.
//   * Late news cannot win. An errand's outcome counts only while it is the display's errand on its
//     way; one reported again, or for a level since replaced, changes nothing.

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What is sent to a display: its level written, or read back to confirm it.</summary>
internal enum ErrandKind
{
    Write,
    Verify,
}

/// <summary>One command on its way to a display; <paramref name="Ticket"/> tells it from any other.</summary>
internal sealed record Errand(string Device, ErrandKind Kind, int Level, long Ticket);

/// <summary>
/// Where a display ended for the level asked: <paramref name="Level"/> what it is at as last known
/// (null where nothing was learnt), and <paramref name="Trouble"/> a line for karpo.log where it did
/// not get there.
/// </summary>
internal sealed record Landed(int? Level, string? Trouble);

/// <summary>The timings of <see cref="ScreenWrites"/>, in ms (see docs\widgets.md §495 for why these).</summary>
/// <param name="SettleMs">Quiet — nothing asked, nothing written — before a display is read back.</param>
/// <param name="RetryMs">The first wait before a level is sent again or read again; doubled each time.</param>
/// <param name="Attempts">The most writes one level asked gets, the first included.</param>
/// <param name="Reads">The most reads again after a read with no answer.</param>
internal sealed record ScreenPace(int SettleMs = 300, int RetryMs = 200, int Attempts = 4, int Reads = 3)
{
    public static ScreenPace Default { get; } = new();
}

/// <summary>Each display's level asked and the errands that get it there (see the file's header). Not thread-safe: the caller locks.</summary>
internal sealed class ScreenWrites(ScreenPace pace)
{
    private readonly Dictionary<string, Display> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> free = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> troubled = new(StringComparer.OrdinalIgnoreCase);
    private long tickets;

    /// <summary>One display with a level asked and not yet confirmed.</summary>
    private sealed class Display(int desired)
    {
        public int Desired = desired;

        /// <summary>The level it last took a write of; null where unknown (none yet, or the last refused).</summary>
        public int? Sent;

        /// <summary>The read back said another level: the level is sent again.</summary>
        public bool Resend;

        public long Quiet;
        public long RetryAt;
        public int Resends;
        public int Failures;
        public int Silences;

        public Errand? Flight;

        public bool NeedsWrite => Sent != Desired || Resend;
    }

    public ScreenPace Pace => pace;

    /// <summary>
    /// <paramref name="level"/> asked for <paramref name="device"/> at <paramref name="now"/>: recorded,
    /// and nothing more. A level other than the one being got there starts afresh.
    /// </summary>
    public void Want(string device, int level, long now)
    {
        level = Math.Clamp(level, 0, 100);
        if (!pending.TryGetValue(device, out var display))
        {
            pending[device] = new Display(level) { Quiet = now };
            return;
        }
        display.Quiet = Math.Max(display.Quiet, now);
        if (display.Desired == level)
        {
            return;
        }
        display.Desired = level;
        (display.Resend, display.RetryAt, display.Resends, display.Failures, display.Silences) = (false, 0, 0, 0, 0);
    }

    /// <summary>The level asked for <paramref name="device"/> while it is being got there; null once it landed.</summary>
    public int? Wanted(string device) => pending.TryGetValue(device, out var display) ? display.Desired : null;

    /// <summary>Whether <paramref name="device"/> has a level being got there: nobody else reads it meanwhile.</summary>
    public bool Busy(string device) => pending.ContainsKey(device);

    /// <summary>Whether any display has.</summary>
    public bool Any => pending.Count > 0;

    /// <summary>When <paramref name="device"/> may be sent its next command: its last one's end and its rest.</summary>
    public long FreeAt(string device) => free.TryGetValue(device, out var at) ? at : long.MinValue;

    /// <summary>A command other than an errand (a read for a look) went to <paramref name="device"/>, ending at <paramref name="now"/>.</summary>
    public void Spoke(string device, long now, int gap) => free[device] = now + gap;

    /// <summary>
    /// The errand due now, marked on its way; null where none is — <paramref name="wake"/> then says
    /// when one will be (long.MaxValue for never, until a level is asked).
    /// </summary>
    public Errand? Next(long now, out long wake)
    {
        string? best = null;
        var due = long.MaxValue;
        foreach (var (device, display) in pending)
        {
            if (display.Flight is not null)
            {
                continue;
            }
            var at = Math.Max(FreeAt(device), display.RetryAt);
            if (!display.NeedsWrite)
            {
                at = Math.Max(at, display.Quiet + pace.SettleMs);
            }
            if (at < due)
            {
                (best, due) = (device, at);
            }
        }
        if (best is null || due > now)
        {
            wake = due;
            return null;
        }
        wake = now;
        var chosen = pending[best];
        chosen.Flight = new Errand(best, chosen.NeedsWrite ? ErrandKind.Write : ErrandKind.Verify, chosen.Desired, ++tickets);
        return chosen.Flight;
    }

    /// <summary>
    /// <paramref name="errand"/>, a write, came back at <paramref name="now"/>: <paramref name="took"/>
    /// whether the display took it; <paramref name="gap"/> its rest after a command. What it landed at
    /// where it gave up; else null.
    /// </summary>
    public Landed? Wrote(Errand errand, bool took, long now, int gap)
    {
        if (Back(errand, now, gap) is not { } display)
        {
            return null;
        }
        if (took)
        {
            (display.Sent, display.Resend, display.Failures, display.RetryAt) = (errand.Level, false, 0, 0);
            display.Quiet = Math.Max(display.Quiet, now);
            return null;
        }
        display.Sent = null;
        if (++display.Failures >= pace.Attempts)
        {
            return GiveUp(errand.Device, null, $"brightness: {errand.Device} refused {errand.Level} {display.Failures} times"); // not-ui: karpo.log
        }
        display.RetryAt = now + Backoff(display.Failures);
        return null;
    }

    /// <summary>
    /// <paramref name="errand"/>, a read back, came back at <paramref name="now"/> with what the display
    /// <paramref name="said"/> (null for no answer); <paramref name="shows"/> what it may say once it
    /// took the level (its steps around it; the level itself for a display with every one). What it
    /// landed at where that is settled; else null.
    /// </summary>
    public Landed? Verified(Errand errand, int? said, long now, int gap, (int Low, int High) shows)
    {
        if (Back(errand, now, gap) is not { } display || display.NeedsWrite)
        {
            return null;
        }
        var asked = display.Desired;
        if (said is { } shown && (shown == asked || (shown >= shows.Low && shown <= shows.High)))
        {
            return Done(errand.Device, shown);
        }
        if (said is not { } level)
        {
            if (++display.Silences > pace.Reads)
            {
                return GiveUp(errand.Device, asked, $"brightness: {errand.Device} took {asked} but did not answer {display.Silences} reads"); // not-ui: karpo.log
            }
            display.RetryAt = now + Backoff(display.Silences);
            return null;
        }
        display.Silences = 0;
        if (++display.Resends >= pace.Attempts)
        {
            return GiveUp(errand.Device, level, $"brightness: {errand.Device} asked {asked} {display.Resends} times, still at {level}"); // not-ui: karpo.log
        }
        (display.Resend, display.RetryAt) = (true, now + Backoff(display.Resends));
        return null;
    }

    /// <summary>The displays were found again: those gone are forgotten, and nothing is on its way to any.</summary>
    public void Keep(IEnumerable<string> devices)
    {
        var still = new HashSet<string>(devices, StringComparer.OrdinalIgnoreCase);
        foreach (var device in pending.Keys.Where(device => !still.Contains(device)).ToList())
        {
            _ = pending.Remove(device);
        }
        foreach (var display in pending.Values)
        {
            (display.Flight, display.Sent) = (null, null);
        }
        free.Clear();
    }

    /// <summary>The caller lost track of what was on its way (it threw): nothing is, and each errand is given again.</summary>
    public void Abandon()
    {
        foreach (var display in pending.Values)
        {
            display.Flight = null;
        }
    }

    /// <summary><paramref name="device"/> has no way to a level any more: what was asked of it is dropped.</summary>
    public void Drop(string device) => pending.Remove(device);

    /// <summary>The errand back, where it is still its display's own; null for one late or unknown.</summary>
    private Display? Back(Errand errand, long now, int gap)
    {
        if (!pending.TryGetValue(errand.Device, out var display) || !ReferenceEquals(display.Flight, errand))
        {
            return null;
        }
        display.Flight = null;
        free[errand.Device] = now + gap;
        return display;
    }

    private Landed Done(string device, int level)
    {
        _ = pending.Remove(device);
        _ = troubled.Remove(device);
        return new Landed(level, null);
    }

    private Landed GiveUp(string device, int? level, string trouble)
    {
        _ = pending.Remove(device);
        return new Landed(level, troubled.Add(device) ? trouble : null);
    }

    private long Backoff(int times) => (long)pace.RetryMs << Math.Clamp(times - 1, 0, 6);
}
