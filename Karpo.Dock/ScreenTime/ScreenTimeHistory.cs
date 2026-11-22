// Screen time by day: what the count (ScreenTimeCount) counts into, what its files keep, and how a
// day and a duration are worded. Pure: no window, no clock and no system call of its own, so the
// day arithmetic can be tested in any time zone.
//
// Restored on 2026-10-09 from before its removal (c8a9db1^, 2026-09-26), unchanged where it counts:
// the files on disk are the ones that Karpo wrote, and this reads and writes them as it did.
//
// A day is a calendar day in the PC's time zone, as long as the zone makes it: 23 hours when
// summer time starts, 25 when it ends, and in a zone whose clocks jump at midnight the day starts
// at the jump. A counted span is cut where the next local day begins, so the minutes before
// midnight count for the day they were spent on.
//
// Two files keep the days (ScreenTimeFiles), in the local folder (Config.LocalFolder):
// screentime.json holds today, in the shape it has always had (a file from before revision 8 is
// taken over as it is); screentime-history.json holds every other day, the last 90, and is written
// only when one of those changed, the file it replaces kept as screentime-history.json.bak.
// Both keep each idle time's days apart ("rules", by its minutes): the count keeps one now
// (ScreenTimeCount.IdleMinutes), and carries any other a file has back to it untouched.

using System.Globalization;
using Karpo.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Dock;

/// <summary>One local day of one idle time: how long the PC was used, from when, and each app's part.</summary>
internal sealed class ScreenTimeDay(DateOnly date)
{
    /// <summary>
    /// An app with less time than this on a day is never shown for it. Its seconds are kept (a
    /// minute may still come today) and still count in <see cref="Total"/>.
    /// </summary>
    public static readonly TimeSpan LeastShown = TimeSpan.FromMinutes(1);

    public DateOnly Date { get; } = date;

    public TimeSpan Total { get; set; }

    /// <summary>When counting began that day, as the wall clock showed it. Kept, not shown.</summary>
    public DateTime? Since { get; set; }

    /// <summary>Each app's time, by screen-time key (<see cref="ScreenTimeApps.KeyOf"/>).</summary>
    public Dictionary<string, TimeSpan> Apps { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The apps worth showing (<see cref="LeastShown"/> and more), most used first; equal times by key, so
    /// the order never changes between showings.
    /// </summary>
    public IReadOnlyList<(string App, TimeSpan Time)> Ranked() =>
        [.. Apps.Where(pair => pair.Value >= LeastShown)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => (pair.Key, pair.Value))];

    public ScreenTimeDay Copy()
    {
        var copy = new ScreenTimeDay(Date) { Total = Total, Since = Since };
        foreach (var (app, time) in Apps)
        {
            copy.Apps[app] = time;
        }
        return copy;
    }
}

/// <summary>
/// The days of one idle time, by local date: counted into with <see cref="Add"/>, kept for
/// <see cref="ScreenTimeCount.KeptDays"/> days by <see cref="Prune"/>.
/// </summary>
internal sealed class ScreenTimeLog
{
    private readonly SortedDictionary<DateOnly, ScreenTimeDay> days = [];

    public int Count => days.Count;

    /// <summary>Oldest first.</summary>
    public IEnumerable<ScreenTimeDay> Days => days.Values;

    /// <summary>The first day kept, or null when there is none.</summary>
    public DateOnly? Oldest => days.Count > 0 ? days.Keys.First() : null;

    public ScreenTimeDay? Day(DateOnly date) => days.GetValueOrDefault(date);

    public TimeSpan TotalOn(DateOnly date) => days.TryGetValue(date, out var day) ? day.Total : TimeSpan.Zero;

    /// <summary>The day's record, made empty when there is none yet.</summary>
    public ScreenTimeDay Ensure(DateOnly date)
    {
        if (!days.TryGetValue(date, out var day))
        {
            day = new ScreenTimeDay(date);
            days[date] = day;
        }
        return day;
    }

    /// <summary>
    /// Counts the span from <paramref name="fromUtc"/> to <paramref name="untilUtc"/> for
    /// <paramref name="app"/> (null: for no app), each part on the local day of
    /// <paramref name="zone"/> it was spent on. Returns the local day it began on, the earliest it
    /// changed; null for an empty span.
    /// </summary>
    public DateOnly? Add(DateTime fromUtc, DateTime untilUtc, string? app, TimeZoneInfo zone)
    {
        DateOnly? first = null;
        var from = ScreenTimeClock.Utc(fromUtc);
        var until = ScreenTimeClock.Utc(untilUtc);
        while (from < until)
        {
            var local = TimeZoneInfo.ConvertTimeFromUtc(from, zone);
            var next = ScreenTimeClock.NextDay(from, zone);
            var end = until < next ? until : next;
            var day = Ensure(DateOnly.FromDateTime(local));
            var span = end - from;
            day.Total += span;
            if (app is not null)
            {
                day.Apps[app] = day.Apps.GetValueOrDefault(app) + span;
            }
            day.Since ??= ScreenTimeClock.WallClock(local);
            first ??= day.Date;
            from = end;
        }
        return first;
    }

    /// <summary>
    /// Takes <paramref name="day"/> in; of two records of one date, the one with more time, as totals
    /// only grow and so the larger is the later. True when it changed the log.
    /// </summary>
    public bool Merge(ScreenTimeDay day)
    {
        if (days.TryGetValue(day.Date, out var kept) && kept.Total >= day.Total)
        {
            return false;
        }
        days[day.Date] = day;
        return true;
    }

    /// <summary>Drops the days more than <paramref name="keep"/> days before <paramref name="today"/>; true when one went.</summary>
    public bool Prune(DateOnly today, int keep)
    {
        var oldest = today.AddDays(-keep);
        var gone = false;
        while (days.Count > 0 && days.Keys.First() is var first && first < oldest)
        {
            days.Remove(first);
            gone = true;
        }
        return gone;
    }

    public ScreenTimeLog Copy()
    {
        var copy = new ScreenTimeLog();
        foreach (var (date, day) in days)
        {
            copy.days[date] = day.Copy();
        }
        return copy;
    }
}

/// <summary>A copy of the days counted (<see cref="ScreenTimeTracker.Snapshot"/>), taken on the local day <paramref name="Today"/>.</summary>
internal sealed record ScreenTimeSnapshot(DateOnly Today, ScreenTimeLog Log)
{
    /// <summary>Nothing counted, on <paramref name="today"/>: what a line shows where nothing counts (a test, an isolated run).</summary>
    public static ScreenTimeSnapshot Empty(DateOnly today) => new(today, new ScreenTimeLog());
}

/// <summary>Local days in a time zone, for counting that never trips over summer time.</summary>
internal static class ScreenTimeClock
{
    /// <summary>
    /// When (UTC) the local day after the one <paramref name="utc"/> falls on begins in
    /// <paramref name="zone"/>: its midnight, in the offset the zone has then; where the clocks
    /// skip midnight, the moment they jump past it; where they go back over it, the first midnight.
    /// </summary>
    public static DateTime NextDay(DateTime utc, TimeZoneInfo zone)
    {
        utc = Utc(utc);
        // Kinds taken off: in the PC's own zone the conversion says Local, and the sums below are UTC.
        var midnight = DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, zone).Date.AddDays(1), DateTimeKind.Unspecified);
        var guess = DateTime.SpecifyKind(midnight - zone.GetUtcOffset(utc), DateTimeKind.Utc);
        // Each offset the zone has around its midnight gives a candidate; the day begins at the
        // earliest one whose local time is on the new day. Summer time moves the clock by an hour
        // or two, so offsets three hours either side of the guess cover every transition.
        Span<TimeSpan> offsets =
        [
            zone.GetUtcOffset(utc),
            zone.GetUtcOffset(Shift(guess, -3)),
            zone.GetUtcOffset(guess),
            zone.GetUtcOffset(Shift(guess, 3)),
        ];
        var best = DateTime.MaxValue;
        foreach (var offset in offsets)
        {
            var candidate = DateTime.SpecifyKind(midnight - offset, DateTimeKind.Utc);
            if (candidate > utc && candidate < best && TimeZoneInfo.ConvertTimeFromUtc(candidate, zone) >= midnight)
            {
                best = candidate;
            }
        }
        if (best == DateTime.MaxValue)
        {
            // Only a zone rewritten by more than three hours at once gets here: a day later still moves on.
            return utc.AddDays(1);
        }
        // A zone whose clocks jump at a moment that is no whole hour (Windows keeps Chile's "24:00" as
        // 23:59:59.999) is on the new day a little before the candidate: halve the span to the first
        // instant that is.
        if (TimeZoneInfo.ConvertTimeFromUtc(best.AddTicks(-1), zone) >= midnight)
        {
            var low = utc;
            var high = best;
            while (high.Ticks - low.Ticks > 1)
            {
                var middle = new DateTime(low.Ticks + (high.Ticks - low.Ticks) / 2, DateTimeKind.Utc);
                if (TimeZoneInfo.ConvertTimeFromUtc(middle, zone) >= midnight)
                {
                    high = middle;
                }
                else
                {
                    low = middle;
                }
            }
            best = high;
        }
        return best;
    }

    /// <summary>The local date of <paramref name="utc"/> in <paramref name="zone"/>.</summary>
    public static DateOnly LocalDate(DateTime utc, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(Utc(utc), zone));

    /// <summary>A reading from DateTime.UtcNow or a file as UTC; one of unspecified kind is taken to be UTC already.</summary>
    public static DateTime Utc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>A local time as the wall clock shows it, to the second, without a kind to be converted by.</summary>
    public static DateTime WallClock(DateTime local) =>
        DateTime.SpecifyKind(new DateTime(local.Ticks - local.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Unspecified);

    private static DateTime Shift(DateTime utc, int hours)
    {
        var ticks = utc.Ticks + hours * TimeSpan.TicksPerHour;
        return new DateTime(Math.Clamp(ticks, DateTime.MinValue.Ticks, DateTime.MaxValue.Ticks), DateTimeKind.Utc);
    }
}

/// <summary>How the lines word days and durations, in the language Karpo speaks.</summary>
internal static class ScreenTimeText
{
    /// <summary>"Today", "Yesterday", else <paramref name="date"/>'s words for the day.</summary>
    public static string DayName(DateOnly day, DateOnly today, Func<DateOnly, string> date) =>
        day == today ? S.WidgetScreenTimeToday
        : day == today.AddDays(-1) ? S.WidgetScreenTimeYesterday
        : date(day);

    /// <summary>"0 min", "&lt; 1 min", "42 min", "3 h", "3 h 42 min".</summary>
    public static string Duration(TimeSpan time)
    {
        if (time <= TimeSpan.Zero)
        {
            return S.WidgetScreenTimeMinutes(0);
        }
        if (time < TimeSpan.FromMinutes(1))
        {
            return S.WidgetScreenTimeUnderMinute;
        }
        var minutes = (int)Math.Min(int.MaxValue, (long)time.TotalMinutes);
        if (minutes < 60)
        {
            return S.WidgetScreenTimeMinutes(minutes);
        }
        return minutes % 60 == 0
            ? S.WidgetScreenTimeHours(minutes / 60)
            : S.WidgetScreenTimeHoursMinutes(hours: minutes / 60, minutes: minutes % 60);
    }

    /// <summary>The widest a day's duration is written, for the room it is kept: "88 h 88 min".</summary>
    public static string DurationShape => S.WidgetScreenTimeHoursMinutes(hours: 88, minutes: 88);
}

/// <summary>screentime.json.</summary>
internal sealed class ScreenTimeFile
{
    /// <summary>The first rule's day, yyyy-MM-dd, local; also what files before revision 8 held.</summary>
    public string Day { get; set; } = "";

    public long Seconds { get; set; }

    /// <summary>When counting started that day, local time.</summary>
    public DateTime? Since { get; set; }

    /// <summary>Earlier days' totals in seconds, by yyyy-MM-dd.</summary>
    public Dictionary<string, long>? Earlier { get; set; }

    /// <summary>The idle minutes of the fields above; missing in files from before revision 8.</summary>
    public int? Idle { get; set; }

    /// <summary>Every rule by its idle minutes.</summary>
    public Dictionary<string, ScreenTimeRule>? Rules { get; set; }
}

/// <summary>One idle time's totals in screentime.json.</summary>
internal sealed class ScreenTimeRule
{
    public Dictionary<string, double>? Apps { get; set; }

    public string Day { get; set; } = "";

    public long Seconds { get; set; }

    public DateTime? Since { get; set; }

    public Dictionary<string, long>? Earlier { get; set; }
}

/// <summary>screentime-history.json: every day but today, per idle time.</summary>
internal sealed class ScreenTimeHistoryFile
{
    /// <summary>By idle minutes, then by yyyy-MM-dd.</summary>
    public Dictionary<string, Dictionary<string, ScreenTimeDayFile>>? Rules { get; set; }
}

/// <summary>One day in screentime-history.json.</summary>
internal sealed class ScreenTimeDayFile
{
    public long Seconds { get; set; }

    /// <summary>When counting began that day, wall-clock time.</summary>
    public DateTime? Since { get; set; }

    /// <summary>Seconds per app key.</summary>
    public Dictionary<string, double>? Apps { get; set; }
}

/// <summary>What <see cref="ScreenTimeFiles.Read"/> found.</summary>
/// <param name="Logs">Each idle time's days, by its minutes.</param>
/// <param name="Resumed">The idle times the live file counted today for: they go on from its total.</param>
/// <param name="HistoryStale">
/// The history lacks something the live file had (a day it carried over, the earlier totals of a file
/// from before the history), so it is to be written.
/// </param>
internal sealed record ScreenTimeRead(SortedDictionary<int, ScreenTimeLog> Logs, HashSet<int> Resumed, bool HistoryStale);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(ScreenTimeFile))]
[JsonSerializable(typeof(ScreenTimeHistoryFile))]
internal sealed partial class ScreenTimeJson : JsonSerializerContext;

/// <summary>
/// What screentime.json and screentime-history.json hold, as each idle time's
/// <see cref="ScreenTimeLog"/>, and back; and how they are read and written.
/// </summary>
internal static class ScreenTimeFiles
{
    public const string LiveName = "screentime.json";
    public const string HistoryName = "screentime-history.json";

    /// <summary>How many earlier days' totals screentime.json still carries, as before, for an older Karpo.</summary>
    public const int LegacyDays = 14;

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// Every idle time's days from the two files. The history's days come first; the live file's
    /// day (today's, or the last one Karpo counted) and its earlier totals fill in, and of two records
    /// of one day the larger is kept, so a day half moved into the history when Karpo stopped is
    /// whole again. A file from before revision 8 has one total and doesn't say for which idle time:
    /// it is <paramref name="firstIdle"/>'s.
    /// </summary>
    public static ScreenTimeRead Read(ScreenTimeFile? live, ScreenTimeHistoryFile? history, DateOnly today, int firstIdle)
    {
        var historyStale = false;
        var resumed = new HashSet<int>();
        var logs = new SortedDictionary<int, ScreenTimeLog>();
        ScreenTimeLog LogOf(int idle)
        {
            if (!logs.TryGetValue(idle, out var log))
            {
                log = new ScreenTimeLog();
                logs[idle] = log;
            }
            return log;
        }

        foreach (var (name, days) in history?.Rules ?? [])
        {
            if (Idle(name) is not { } idle || days is null)
            {
                continue;
            }
            var log = LogOf(idle);
            foreach (var (text, entry) in days)
            {
                if (ParseDay(text) is { } date && entry is not null)
                {
                    log.Merge(DayOf(date, entry.Seconds, entry.Since, entry.Apps));
                }
            }
        }

        if (live is null)
        {
            return new ScreenTimeRead(logs, resumed, historyStale);
        }
        var rules = new Dictionary<int, ScreenTimeRule>();
        foreach (var (name, entry) in live.Rules ?? [])
        {
            if (Idle(name) is { } idle && entry is not null)
            {
                rules[idle] = entry;
            }
        }
        if (rules.Count == 0)
        {
            rules[live.Idle is { } saved && saved is >= 1 and <= 60 ? saved : Math.Clamp(firstIdle, 1, 60)] = new ScreenTimeRule
            {
                Day = live.Day,
                Seconds = live.Seconds,
                Since = live.Since,
                Earlier = live.Earlier,
            };
        }
        foreach (var (idle, entry) in rules)
        {
            var log = LogOf(idle);
            if (ParseDay(entry.Day) is { } date)
            {
                if (date == today)
                {
                    // A restart today goes on from the saved total; the minutes since the last save
                    // are not known, so they don't count.
                    resumed.Add(idle);
                }
                if (entry.Seconds > 0)
                {
                    var changed = log.Merge(DayOf(date, entry.Seconds, entry.Since, entry.Apps));
                    historyStale |= changed && date != today;
                }
            }
            foreach (var (text, seconds) in entry.Earlier ?? [])
            {
                // Totals only: a day the history has is never replaced by one.
                if (ParseDay(text) is { } earlier && earlier != today && seconds > 0 && log.Day(earlier) is null)
                {
                    log.Merge(DayOf(earlier, seconds, null, null));
                    historyStale = true;
                }
            }
        }
        return new ScreenTimeRead(logs, resumed, historyStale);
    }

    /// <summary>screentime.json for <paramref name="today"/>: each idle time's today, and its last earlier totals.</summary>
    public static ScreenTimeFile Live(IReadOnlyDictionary<int, ScreenTimeLog> logs, DateOnly today)
    {
        var file = new ScreenTimeFile { Rules = [] };
        foreach (var (idle, log) in logs)
        {
            var day = log.Day(today);
            var earlier = new Dictionary<string, long>();
            foreach (var past in log.Days.Where(past => past.Date < today && past.Date >= today.AddDays(-LegacyDays) && past.Total > TimeSpan.Zero))
            {
                earlier[Text(past.Date)] = (long)past.Total.TotalSeconds;
            }
            file.Rules[idle.ToString(CultureInfo.InvariantCulture)] = new ScreenTimeRule
            {
                Day = Text(today),
                Seconds = (long)(day?.Total.TotalSeconds ?? 0),
                Since = day?.Since,
                Apps = day is null ? [] : day.Apps.ToDictionary(pair => pair.Key, pair => pair.Value.TotalSeconds, StringComparer.OrdinalIgnoreCase),
                Earlier = earlier,
            };
        }
        // The first idle time also at the top, where files before revision 8 kept their one total.
        if (logs.Count > 0)
        {
            var idle = logs.Keys.Min();
            var top = file.Rules[idle.ToString(CultureInfo.InvariantCulture)];
            (file.Day, file.Seconds, file.Since, file.Earlier, file.Idle) = (top.Day, top.Seconds, top.Since, top.Earlier, idle);
        }
        return file;
    }

    /// <summary>screentime-history.json: every day but <paramref name="today"/> that has time, per idle time.</summary>
    public static ScreenTimeHistoryFile History(IReadOnlyDictionary<int, ScreenTimeLog> logs, DateOnly today)
    {
        var file = new ScreenTimeHistoryFile { Rules = [] };
        foreach (var (idle, log) in logs)
        {
            var days = new Dictionary<string, ScreenTimeDayFile>();
            foreach (var day in log.Days.Where(day => day.Date != today && day.Total > TimeSpan.Zero))
            {
                days[Text(day.Date)] = new ScreenTimeDayFile
                {
                    Seconds = (long)day.Total.TotalSeconds,
                    Since = day.Since,
                    Apps = day.Apps.Count == 0 ? null : day.Apps.ToDictionary(pair => pair.Key, pair => Math.Round(pair.Value.TotalSeconds, 1), StringComparer.OrdinalIgnoreCase),
                };
            }
            if (days.Count > 0)
            {
                file.Rules[idle.ToString(CultureInfo.InvariantCulture)] = days;
            }
        }
        return file;
    }

    public static ScreenTimeFile? ParseLive(string? json)
    {
        if (json is null)
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize(json, ScreenTimeJson.Default.ScreenTimeFile);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ScreenTimeHistoryFile? ParseHistory(string? json)
    {
        if (json is null)
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize(json, ScreenTimeJson.Default.ScreenTimeHistoryFile);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Serialize(ScreenTimeFile file) => JsonSerializer.Serialize(file, ScreenTimeJson.Default.ScreenTimeFile);

    public static string Serialize(ScreenTimeHistoryFile file) => JsonSerializer.Serialize(file, ScreenTimeJson.Default.ScreenTimeHistoryFile);

    /// <summary>The history in <paramref name="folder"/>, or its backup when the file is missing or broken.</summary>
    public static ScreenTimeHistoryFile? ReadHistory(string folder) =>
        ParseHistory(ReadText(Path.Combine(folder, HistoryName))) ?? ParseHistory(ReadText(Path.Combine(folder, HistoryName + ".bak")));

    public static ScreenTimeFile? ReadLive(string folder) => ParseLive(ReadText(Path.Combine(folder, LiveName)));

    /// <summary>
    /// Writes <paramref name="text"/> as <paramref name="name"/> in <paramref name="folder"/>: into a
    /// temporary file flushed to the disk first, which then replaces the old one, so neither a crash
    /// nor a power cut leaves half a file. With <paramref name="backup"/>, the replaced file stays as
    /// name.bak. False when it could not be written (the old file is then as it was).
    /// </summary>
    public static bool Write(string folder, string name, string text, bool backup)
    {
        try
        {
            DurableFile.Write(Path.Combine(folder, name), text, keepBackup: backup);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: {name} not saved: {error.Message}");
            return false;
        }
    }

    public static DateOnly? ParseDay(string? text) =>
        DateOnly.TryParseExact(text, DayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    public static string Text(DateOnly date) => date.ToString(DayFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// The file's text; null where there is none or it can't be read. A file another program holds for a
    /// moment (a scanner, a backup) is waited for (DurableFile.ReadText), not taken for missing: the history's
    /// backup is a day older, and the next save would write that day over the newer one.
    /// </summary>
    private static string? ReadText(string path) => DurableFile.ReadText(path, out var text) == FileRead.Ok ? text : null;

    private static int? Idle(string? name) =>
        int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var idle) && idle is >= 1 and <= 60 ? idle : null;

    private static ScreenTimeDay DayOf(DateOnly date, long seconds, DateTime? since, Dictionary<string, double>? apps)
    {
        var day = new ScreenTimeDay(date)
        {
            Total = TimeSpan.FromSeconds(Math.Max(0, seconds)),
            Since = since is { } at ? ScreenTimeClock.WallClock(at) : null,
        };
        foreach (var (app, time) in apps ?? [])
        {
            if (app is { Length: > 0 } && double.IsFinite(time) && time > 0)
            {
                day.Apps[app] = TimeSpan.FromSeconds(time);
            }
        }
        return day;
    }
}

/// <summary>
/// Which app screen time counts a window's time for: a Store app shown in an
/// ApplicationFrameHost frame counts for the app inside, a packaged app by its
/// AUMID ("aumid:Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"), anything else
/// by its executable's path.
/// </summary>
internal static class ScreenTimeApps
{
    public const string AumidPrefix = "aumid:";

    public static bool IsFrameHost(string? exe) =>
        exe is not null && Path.GetFileName(exe).Equals("ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The key for a window of <paramref name="exe"/>: <paramref name="hosted"/> is the AUMID of the
    /// app in a frame (only asked for when <paramref name="exe"/> is the frame host, and null for an empty
    /// frame, which counts for no app), <paramref name="packaged"/> the process's own AUMID.
    /// </summary>
    public static string? KeyOf(string? exe, string? packaged, string? hosted)
    {
        if (IsFrameHost(exe))
        {
            return hosted is { Length: > 0 } inside ? AumidPrefix + inside : null;
        }
        if (packaged is { Length: > 0 } own)
        {
            return AumidPrefix + own;
        }
        return string.IsNullOrWhiteSpace(exe) ? null : exe;
    }

    /// <summary>The AUMID a key stands for; null for an executable's path.</summary>
    public static string? AumidOf(string key) =>
        key.StartsWith(AumidPrefix, StringComparison.OrdinalIgnoreCase) ? key[AumidPrefix.Length..] : null;
}
