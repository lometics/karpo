// alarm: an alarm widget's one alarm — a time of day, the days it repeats on, on or off — and the
// rule for when it rings (docs\widgets.md §106).
//
// The person: "add an alarm widget too, the alarm widget should just have one alarm that you can
// configure to a certain time and then activate. (optionally also repeats on configurable days)".
//
// What is set is the widget's own (AlarmOptions, in config.json beside its other options), on
// through a restart: an alarm is a promise about tomorrow, not a countdown of this session. What it
// is doing — ringing, snoozed, which ring it last had — is in memory (AlarmBook), one per widget.
//
// **When it rings.** At its time on the next day it may ring on: any day for one that does not
// repeat, else one of its days. Worked in the wall clock of the zone in force and kept as an
// instant (UTC), so a ring is one moment whatever the clock does afterwards:
//
//   * Set at a time already past today, it is tomorrow's: switching an alarm on, Karpo starting,
//     or changing its time never rings for a moment before it.
//   * Overdue by less than Grace — the PC slept through it, Windows' clock was moved past it — it
//     rings once when it is seen; by more, it is missed and does not ring at all.
//   * Each ring, or miss, is remembered (Entry.Done) and the next is reckoned after it, so the clock
//     going back after a ring never rings it again, and a zone change moves what is next with the
//     wall clock.
//   * A time a change to summer time skips (02:30 on that night) rings at the first minute after
//     the gap; a time the change back to winter time says twice rings at the first of them.
//   * One that does not repeat turns itself off once its ring is over — dismissed, run out, or
//     missed — as Windows' Clock does; one that repeats stays on.
//
// Pure: every rule here is handed the time and the zone, so every rule has a test
// (Karpo.Tests\Calendars\AlarmTests.cs). The dock rings it (Karpo.Dock\Dock.Alarms.cs).

using System.Globalization;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// alarm: an alarm widget's own settings (<see cref="WidgetInfos.Alarm"/>): its time, the days it
/// repeats on, whether it is on, and its name. Every alarm widget has one; a new one is 07:00, off,
/// on no particular day.
/// </summary>
public sealed class AlarmOptions
{
    /// <summary>The most a name says, in characters, as a timer's.</summary>
    public const int MostName = 40;

    /// <summary>When it rings, "HH:mm" on the 24-hour clock whatever the region shows ("07:00", "18:30").</summary>
    public string Time { get; set; } = AlarmTimes.Text(AlarmTimes.Default);

    /// <summary>
    /// The days it repeats on ("mon", "tue" … "sun"), Monday first; absent for one that rings once,
    /// at its time's next coming, and then turns itself off.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Days { get; set; }

    /// <summary>Whether it is set to ring: the switch on its line.</summary>
    public bool On { get; set; }

    /// <summary>Its own name ("Wake up"), shown on its line where there is room; empty for "Alarm".</summary>
    public string Name { get; set; } = "";

    /// <summary>A time that is one, days this build knows, each once, Monday first, and a name no longer than <see cref="MostName"/>.</summary>
    public void Normalize()
    {
        Time = AlarmTimes.Text(AlarmTimes.Minutes(Time));
        Days = AlarmDays.Normalized(Days);
        var name = (Name ?? "").ReplaceLineEndings(" ").Trim();
        Name = name.Length <= MostName ? name : name[..MostName].TrimEnd();
    }
}

/// <summary>An alarm's time of day as minutes after midnight, and as the file writes it.</summary>
public static class AlarmTimes
{
    /// <summary>What a new alarm is set to: seven in the morning.</summary>
    public const int Default = 7 * 60;

    /// <summary>Minutes in a day.</summary>
    public const int Day = 24 * 60;

    /// <summary>"7:00", "07:00", "18:30" as minutes after midnight; null for anything else.</summary>
    public static int? Parse(string? text)
    {
        var parts = (text ?? "").Trim().Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || parts[1].Length != 2 || hours > 23 || minutes > 59)
        {
            return null;
        }
        return (hours * 60) + minutes;
    }

    /// <summary>What <paramref name="text"/> sets it to: <see cref="Default"/> where it is no time.</summary>
    public static int Minutes(string? text) => Parse(text) ?? Default;

    /// <summary>
    /// What a time picker's <paramref name="picked"/> writes over <paramref name="stored"/>: its text,
    /// or null where it is the time already set — the time it shows for no time at all, too, so
    /// opening the page writes nothing. The setter that asks raises nothing for a null either: it
    /// raised every time, the picker was given its time back, said so again, and the settings
    /// window recursed until its stack ran out (2026-10-11).
    /// </summary>
    public static string? Picked(string? stored, TimeSpan picked)
    {
        var minutes = (int)Math.Round(picked.TotalMinutes);
        var text = Text(minutes);
        return Parse(text) == Minutes(stored) ? null : text;
    }

    /// <summary>"07:00": minutes after midnight as the file writes them, wrapped into the day.</summary>
    public static string Text(int minutes)
    {
        var day = ((minutes % Day) + Day) % Day;
        return string.Create(CultureInfo.InvariantCulture, $"{day / 60:00}:{day % 60:00}");
    }
}

/// <summary>
/// The days an alarm repeats on, as the file writes them ("mon" … "sun") and as a mask of bits, one
/// per <see cref="DayOfWeek"/> (Sunday the lowest).
/// </summary>
public static class AlarmDays
{
    /// <summary>Each day's word, by <see cref="DayOfWeek"/>.</summary>
    public static readonly string[] Words = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    /// <summary>The days Monday first, the order the file writes them in.</summary>
    public static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    public const int Every = 0x7F;

    /// <summary>Monday to Friday.</summary>
    public const int Weekdays = 0x3E;

    /// <summary>Saturday and Sunday.</summary>
    public const int Weekend = 0x41;

    public static int Bit(DayOfWeek day) => 1 << (int)day;

    public static bool Has(int mask, DayOfWeek day) => (mask & Bit(day)) != 0;

    /// <summary>The mask of <paramref name="days"/>; a word this build does not know is no day.</summary>
    public static int Mask(IEnumerable<string>? days)
    {
        var mask = 0;
        foreach (var word in days ?? [])
        {
            var index = Array.FindIndex(Words, known => string.Equals(known, word?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                mask |= 1 << index;
            }
        }
        return mask;
    }

    /// <summary>The words of <paramref name="mask"/>, Monday first; null for none, which the file leaves out.</summary>
    public static string[]? Of(int mask)
    {
        var days = Week.Where(day => Has(mask, day)).Select(day => Words[(int)day]).ToArray();
        return days.Length > 0 ? days : null;
    }

    /// <summary>The days a file says, as this build writes them (<see cref="Of"/>).</summary>
    public static string[]? Normalized(string[]? days) => Of(Mask(days));

    /// <summary>The days in a week that starts on <paramref name="first"/>, as a region lists them.</summary>
    public static IEnumerable<DayOfWeek> From(DayOfWeek first) => Enumerable.Range(0, 7).Select(step => (DayOfWeek)(((int)first + step) % 7));
}

/// <summary>What an alarm is set to: its time, its days (a mask; none rings once) and whether it is on.</summary>
public readonly record struct AlarmPlan(int Minutes, int Days, bool On)
{
    public static AlarmPlan Of(AlarmOptions? options) =>
        options is null ? new AlarmPlan(AlarmTimes.Default, 0, false) : new AlarmPlan(AlarmTimes.Minutes(options.Time), AlarmDays.Mask(options.Days), options.On);

    /// <summary>It rings on its days, every week; none, and it rings once.</summary>
    public bool Repeats => Days != 0;
}

/// <summary>When an alarm rings next (see the note at the top of the file).</summary>
public static class AlarmSchedule
{
    /// <summary>
    /// The first moment after <paramref name="after"/> (UTC) that <paramref name="plan"/> rings at in
    /// <paramref name="zone"/>, as UTC: its time on the first day after that it may ring on. Whether
    /// it is on is not asked.
    /// </summary>
    public static DateTime Next(AlarmPlan plan, DateTime after, TimeZoneInfo zone)
    {
        var utc = DateTime.SpecifyKind(after, DateTimeKind.Utc);
        var today = TimeZoneInfo.ConvertTimeFromUtc(utc, zone).Date;
        // From the day before, for a zone whose change of offset puts yesterday's time after now;
        // a week and two days covers every day of every week.
        for (var step = -1; step <= 8; step++)
        {
            var day = today.AddDays(step);
            if (plan.Repeats && !AlarmDays.Has(plan.Days, day.DayOfWeek))
            {
                continue;
            }
            var at = At(day, plan.Minutes, zone);
            if (at > utc)
            {
                return at;
            }
        }
        // Not reached: some day of the next eight is one of its days and after now.
        return utc.AddDays(1);
    }

    /// <summary>
    /// <paramref name="minutes"/> after midnight on <paramref name="day"/>'s wall clock in
    /// <paramref name="zone"/>, as UTC: in a gap the change to summer time makes, the first minute
    /// after it; told twice by the change back, the first time.
    /// </summary>
    public static DateTime At(DateTime day, int minutes, TimeZoneInfo zone)
    {
        var wall = DateTime.SpecifyKind(day.Date.AddMinutes(minutes), DateTimeKind.Unspecified);
        for (var guard = 0; zone.IsInvalidTime(wall) && guard < AlarmTimes.Day; guard++)
        {
            wall = wall.AddMinutes(1);
        }
        if (zone.IsAmbiguousTime(wall))
        {
            // The larger offset is summer time's, which comes first.
            var first = zone.GetAmbiguousTimeOffsets(wall).Max();
            return DateTime.SpecifyKind(wall - first, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeToUtc(wall, zone);
    }
}

public enum AlarmPhase
{
    /// <summary>Switched off.</summary>
    Off,

    /// <summary>On, waiting for its time.</summary>
    Set,

    /// <summary>Its time came: it rings until it is dismissed, snoozed or has rung for <see cref="AlarmBook.RingFor"/>.</summary>
    Ringing,

    /// <summary>Snoozed: it rings again at <see cref="AlarmStatus.Until"/>.</summary>
    Snoozed,
}

/// <summary>What an alarm is doing: its phase, when it rings next while set (UTC), and until when it is snoozed (UTC).</summary>
public readonly record struct AlarmStatus(AlarmPhase Phase, DateTime? Next = null, DateTime? Until = null);

/// <summary>
/// The alarms a dock keeps: one per alarm widget, by its id, handed the widget's own options
/// (<see cref="Sync"/>) and the time on every call. It rings them (<see cref="Rang"/>), snoozes and
/// dismisses them, and turns one that does not repeat off when its ring is over, in the options it
/// was handed (<see cref="Switched"/>, which the dock writes). On the dock's thread only.
/// </summary>
public sealed class AlarmBook
{
    /// <summary>How late a ring may still be: overdue by more — a PC asleep through it — it is missed.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    /// <summary>How long it rings when nobody answers it.</summary>
    public static readonly TimeSpan RingFor = TimeSpan.FromMinutes(5);

    /// <summary>How long a snooze is, as Windows' Clock's is by default.</summary>
    public static readonly TimeSpan SnoozeFor = TimeSpan.FromMinutes(10);

    private sealed class Entry(AlarmOptions options, DateTime armed)
    {
        public AlarmOptions Options = options;
        public AlarmPlan Plan = AlarmPlan.Of(options);

        /// <summary>When it was set as it is now: nothing before it rings.</summary>
        public DateTime Armed = armed;

        /// <summary>The ring it last had, or missed: the next is after it.</summary>
        public DateTime? Done;

        public DateTime? Ringing;
        public DateTime? Snoozed;
    }

    private readonly Dictionary<string, Entry> set = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<TimeZoneInfo> zone;

    /// <param name="zone">The zone its wall clock is in; Windows' own (<see cref="TimeZoneInfo.Local"/>) when null.</param>
    public AlarmBook(Func<TimeZoneInfo>? zone = null)
    {
        this.zone = zone ?? (() => TimeZoneInfo.Local);
    }

    /// <summary>What an alarm is doing changed — set, rung, snoozed, over, switched, or dropped: its id.</summary>
    public event Action<string>? Changed;

    /// <summary>An alarm has just begun to ring, after its state is the new one: its id.</summary>
    public event Action<string>? Rang;

    /// <summary>An alarm's options changed here — its switch flipped, or a ring that does not repeat over: its id. The dock writes them.</summary>
    public event Action<string>? Switched;

    /// <summary>The zone in force.</summary>
    public TimeZoneInfo Zone => zone();

    /// <summary>Whether any of them rings now: the dock's sound plays while one does.</summary>
    public bool Ringing => set.Values.Any(entry => entry.Ringing is not null);

    public IReadOnlyCollection<string> Keys => set.Keys;

    /// <summary>
    /// The alarms <paramref name="alarms"/> has — every alarm widget of the config, with its own
    /// options — at <paramref name="utc"/>: one new, or set to another time, other days or switched,
    /// is set from now, and one that is gone goes. Its options are kept to be read and switched.
    /// </summary>
    public void Sync(IEnumerable<(string Id, AlarmOptions Options)> alarms, DateTime utc)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, options) in alarms)
        {
            if (!seen.Add(id))
            {
                continue;
            }
            var plan = AlarmPlan.Of(options);
            if (set.TryGetValue(id, out var entry) && entry.Plan == plan)
            {
                entry.Options = options;
                continue;
            }
            set[id] = new Entry(options, utc);
            Changed?.Invoke(id);
        }
        foreach (var id in set.Keys.Where(id => !seen.Contains(id)).ToList())
        {
            set.Remove(id);
            Changed?.Invoke(id);
        }
    }

    /// <summary>
    /// What <paramref name="id"/> is doing at <paramref name="utc"/>: from the book where it keeps it,
    /// else from <paramref name="options"/> alone, as if set now.
    /// </summary>
    public AlarmStatus Of(string id, AlarmOptions? options, DateTime utc)
    {
        if (set.TryGetValue(id, out var entry))
        {
            if (!entry.Plan.On)
            {
                return new AlarmStatus(AlarmPhase.Off);
            }
            var next = Next(entry, utc);
            return entry.Ringing is not null ? new AlarmStatus(AlarmPhase.Ringing, next)
                : entry.Snoozed is { } until ? new AlarmStatus(AlarmPhase.Snoozed, next, until)
                : new AlarmStatus(AlarmPhase.Set, next);
        }
        var plan = AlarmPlan.Of(options);
        return plan.On ? new AlarmStatus(AlarmPhase.Set, AlarmSchedule.Next(plan, utc, Zone)) : new AlarmStatus(AlarmPhase.Off);
    }

    /// <summary>
    /// The time passed: each alarm at <paramref name="utc"/>. One whose time came rings
    /// (<see cref="Rang"/>), once; one that has rung long enough stops; a snooze over rings again.
    /// True when anything changed.
    /// </summary>
    public bool Tick(DateTime utc)
    {
        var changed = false;
        List<string>? rang = null;
        foreach (var (id, entry) in set.ToList())
        {
            if (!entry.Plan.On)
            {
                continue;
            }
            // Windows' clock set back before it was set: it is set from there, so its time still
            // comes. Not after a ring, which is never rung again.
            if (entry.Done is null && entry.Armed > utc)
            {
                entry.Armed = utc;
            }
            if (entry.Ringing is { } since)
            {
                if (utc - since >= RingFor)
                {
                    End(id, entry);
                    changed = true;
                }
                continue;
            }
            if (entry.Snoozed is { } until)
            {
                if (utc < until)
                {
                    continue;
                }
                entry.Snoozed = null;
                changed = true;
                if (utc - until <= Grace)
                {
                    entry.Ringing = utc;
                    (rang ??= []).Add(id);
                    Changed?.Invoke(id);
                }
                else
                {
                    End(id, entry);
                }
                continue;
            }
            // Every time that came since the last: the newest rings where it is not too late, the
            // rest are missed. A PC asleep for days steps over each of them here, once.
            DateTime? due = null;
            for (var next = Next(entry, utc); next <= utc; next = Next(entry, utc))
            {
                entry.Done = due = next;
            }
            if (due is not { } came)
            {
                continue;
            }
            changed = true;
            if (utc - came <= Grace)
            {
                entry.Ringing = utc;
                (rang ??= []).Add(id);
                Changed?.Invoke(id);
            }
            else if (!entry.Plan.Repeats)
            {
                End(id, entry);
            }
            else
            {
                Changed?.Invoke(id);
            }
        }
        foreach (var id in rang ?? [])
        {
            Rang?.Invoke(id);
        }
        return changed;
    }

    /// <summary>
    /// Its switch, flipped at <paramref name="utc"/> in <paramref name="options"/> (the widget's own):
    /// on, it is set from now; off, it stops whatever it was doing.
    /// </summary>
    public void Switch(string id, AlarmOptions options, DateTime utc)
    {
        options.On = !options.On;
        set[id] = new Entry(options, utc);
        Switched?.Invoke(id);
        Changed?.Invoke(id);
    }

    /// <summary>A ringing alarm snoozed: it stops and rings again <see cref="SnoozeFor"/> from <paramref name="utc"/>.</summary>
    public void Snooze(string id, DateTime utc)
    {
        if (set.TryGetValue(id, out var entry) && entry.Ringing is not null)
        {
            entry.Ringing = null;
            entry.Snoozed = utc + SnoozeFor;
            Changed?.Invoke(id);
        }
    }

    /// <summary>A ringing or snoozed alarm dismissed: its ring is over, and one that does not repeat is off.</summary>
    public void Dismiss(string id, DateTime utc)
    {
        _ = utc;
        if (set.TryGetValue(id, out var entry) && (entry.Ringing is not null || entry.Snoozed is not null))
        {
            End(id, entry);
        }
    }

    /// <summary>
    /// When the next thing happens to any of them, as UTC — a time coming, a snooze over, a ring
    /// that has run long enough — for the dock to look again then; null while none is on.
    /// </summary>
    public DateTime? Due(DateTime utc)
    {
        DateTime? first = null;
        foreach (var entry in set.Values.Where(entry => entry.Plan.On))
        {
            var at = entry.Ringing is { } since ? since + RingFor : entry.Snoozed ?? Next(entry, utc);
            first = first is { } earlier && earlier <= at ? earlier : at;
        }
        return first;
    }

    /// <summary>The next ring of <paramref name="entry"/>: after its last, or after it was set — or after now, for a clock set back before that.</summary>
    private DateTime Next(Entry entry, DateTime utc) =>
        AlarmSchedule.Next(entry.Plan, entry.Done ?? (entry.Armed <= utc ? entry.Armed : utc), Zone);

    /// <summary>Its ring is over: one that repeats waits for its next, one that does not turns itself off.</summary>
    private void End(string id, Entry entry)
    {
        entry.Ringing = null;
        entry.Snoozed = null;
        if (!entry.Plan.Repeats && entry.Options.On)
        {
            entry.Options.On = false;
            entry.Plan = AlarmPlan.Of(entry.Options);
            Switched?.Invoke(id);
        }
        Changed?.Invoke(id);
    }
}

/// <summary>What an alarm's line says under its time (the strings are Karpo's; the day names and the time the region's).</summary>
public static class AlarmWords
{
    /// <summary>
    /// The days of <paramref name="mask"/>: "Every day", "Weekdays", "Weekends", else each day's short
    /// name in the region's week (from <paramref name="first"/>), "Mon, Wed".
    /// </summary>
    public static string Days(int mask, DayOfWeek first, Func<DayOfWeek, string> name) => mask switch
    {
        AlarmDays.Every => S.DockAlarmEveryDay,
        AlarmDays.Weekdays => S.DockAlarmWeekdays,
        AlarmDays.Weekend => S.DockAlarmWeekends,
        _ => string.Join(", ", AlarmDays.From(first).Where(day => AlarmDays.Has(mask, day)).Select(name)),
    };

    /// <summary>
    /// What its line says of it at <paramref name="utc"/>: "Off"; "Ringing"; "Snoozed until 7:10";
    /// one that repeats, its days; one that does not, when it rings — "Today", "Tomorrow", or the
    /// day's short name where a clock change puts it further off.
    /// </summary>
    public static string Hint(AlarmStatus status, AlarmPlan plan, DateTime utc, TimeZoneInfo zone, DayOfWeek first,
        Func<DayOfWeek, string> name, Func<DateTime, string> time)
    {
        switch (status.Phase)
        {
            case AlarmPhase.Off:
                return S.DockAlarmOff;
            case AlarmPhase.Ringing:
                return S.DockAlarmRinging;
            case AlarmPhase.Snoozed when status.Until is { } until:
                return S.DockAlarmSnoozedUntil(time(TimeZoneInfo.ConvertTimeFromUtc(until, zone)));
        }
        if (plan.Repeats)
        {
            return Days(plan.Days, first, name);
        }
        if (status.Next is not { } next)
        {
            return "";
        }
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).Date;
        var day = TimeZoneInfo.ConvertTimeFromUtc(next, zone).Date;
        return (day - today).Days switch
        {
            <= 0 => S.DockAlarmToday,
            1 => S.DockAlarmTomorrow,
            _ => name(day.DayOfWeek),
        };
    }
}
