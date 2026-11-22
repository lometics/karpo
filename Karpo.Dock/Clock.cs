// The time and date in the user's own format, here or in another time zone.
//
// Formatted by Windows, not .NET: the dock runs with InvariantGlobalization,
// and the user's own format settings live in the locale Windows reads. The
// zone arithmetic is .NET's: TimeZoneInfo reads Windows' own zone data, yearly
// rules included, by the zone's Windows id, invariant mode or not.

using System.Runtime.InteropServices;
using System.Text;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <param name="Time">The time; with seconds only when they were asked for.</param>
/// <param name="Date">The short date.</param>
/// <param name="Long">The long date, for the label shown when hovering the clock.</param>
/// <param name="Zone">The zone's display name for a clock set to a time zone; null for local time.</param>
public sealed record Now(string Time, string Date, string Long, string? Zone = null)
{
    /// <summary>
    /// Whether the time has seconds: a setting, which with the region's format decides the widest
    /// the time can read (<see cref="Clock.Widths"/>), and so the room the bar keeps for it.
    /// </summary>
    public bool Seconds { get; init; }
}

public static unsafe class Clock
{
    // winnls.h is not part of TerraFX.Interop.Windows; these come from there.
    private const uint TIME_NOSECONDS = 0x00000002;
    private const uint DATE_SHORTDATE = 0x00000001;
    private const uint DATE_LONGDATE = 0x00000002;

    private const string ZonesKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Time Zones\"; // not-ui

    // Each id read once: a clock with seconds asks every second, and an id
    // Windows doesn't know would otherwise throw every time. Null marks such an id.
    private static readonly Dictionary<string, (TimeZoneInfo Zone, string Name)?> KnownZones = new(StringComparer.OrdinalIgnoreCase);

    // Per thread, so a test that freezes the clock on its own thread cannot be seen by another test
    // (or by the dock) running beside it; unset, it is this PC's own clock.
    [ThreadStatic]
    private static Func<DateTime>? now;

    /// <summary>
    /// Where the wall-clock time comes from: this PC's, except in the tests, which freeze it so that
    /// an offscreen picture of a clock, a date or a folder holding one is the same sheet every run
    /// instead of one that changes with the minute. A seam for the pictures and nothing else —
    /// nothing in the dock sets it but a picture of its own (§536: a look's preview in the settings
    /// panel, <see cref="Still"/>, for the moment it draws), so every widget of a shipped build has
    /// the PC's clock behind it (<see cref="Real"/>), and the timers that arm the next tick
    /// (<see cref="UntilNextMinute"/>, <see cref="UntilNextSecond"/>) read the PC's clock whatever this says.
    /// </summary>
    internal static Func<DateTime> Now
    {
        get => now ?? Real;
        set => now = value;
    }

    /// <summary>
    /// §536: the clock on this thread standing still at <paramref name="at"/> until the scope handed back ends, then as it
    /// was: what a look's preview draws its stand-in clock and calendar at (LookStand), never this PC's minute.
    /// </summary>
    internal static StillClock Still(DateTime at)
    {
        var was = now;
        now = () => at;
        return new StillClock(was);
    }

    /// <summary>§536: a <see cref="Still"/>, which puts the clock back as it ends.</summary>
    internal readonly struct StillClock(Func<DateTime>? was) : IDisposable
    {
        public void Dispose() => now = was;
    }

    /// <summary>
    /// This PC's own clock: what <see cref="Now"/> is, and what a test puts back. On a stage it is <see cref="Shift"/>
    /// from the PC's, and so is every other reading of the PC's clock in the dock (<see cref="RealUtc"/>,
    /// <see cref="RealOffset"/>, <see cref="RealOffsetUtc"/>): what a line shows and how old what it shows is are told
    /// by one clock, so nothing a picture shows is hours stale or yet to come.
    /// </summary>
    internal static DateTime Real() => DateTime.Now + Shift;

    /// <summary><see cref="Real"/> in UTC.</summary>
    internal static DateTime RealUtc() => DateTime.UtcNow + Shift;

    /// <summary><see cref="Real"/> with its offset from UTC.</summary>
    internal static DateTimeOffset RealOffset() => DateTimeOffset.Now + Shift;

    /// <summary><see cref="RealUtc"/> as a moment.</summary>
    internal static DateTimeOffset RealOffsetUtc() => DateTimeOffset.UtcNow + Shift;

    /// <summary>
    /// How far the dock's clock is from the PC's: nothing, but for a stage's pictures (scripts/stage, KARPO_NOW=13:10),
    /// which are taken at any hour and show a day's meetings round the time on the bar. Read once, as the dock starts.
    /// </summary>
    private static readonly TimeSpan Shift = StageShift(Environment.GetEnvironmentVariable("KARPO_NOW"), DateTime.Now);

    /// <summary>
    /// The shift that makes <paramref name="real"/> the time of day <paramref name="asked"/> says ("13:10", 24 hours)
    /// on the same date; nothing for none or a word that is no time. Whole minutes, so a minute starts when the PC's
    /// does and the timers that wait for the next one (<see cref="UntilNextMinute"/>) need know nothing of it.
    /// </summary>
    internal static TimeSpan StageShift(string? asked, DateTime real) =>
        TimeOnly.TryParseExact(asked, "HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var time)
            ? real.Date + time.ToTimeSpan() - new DateTime(real.Ticks - (real.Ticks % TimeSpan.TicksPerMinute))
            : TimeSpan.Zero;

    /// <summary>
    /// Forgets the zones read so far. Their names are Windows' own, in the display language
    /// (RegLoadMUIStringW), so a language or region change would otherwise leave stale names until
    /// Karpo restarts. The dock calls it when it notices the regional format has changed.
    /// </summary>
    public static void ForgetZones()
    {
        lock (KnownZones)
        {
            KnownZones.Clear();
        }
    }

    /// <summary>Local time, without seconds.</summary>
    public static Now Read() => Read("", seconds: false);

    /// <summary>
    /// The time in the zone with Windows id <paramref name="timeZoneId"/>
    /// ("Pacific Standard Time"); an empty or unknown id means local time.
    /// </summary>
    public static Now Read(string timeZoneId, bool seconds)
    {
        var found = Find(timeZoneId);
        var at = Stamp(found is { } zone ? In(zone.Zone) : Now());
        return new Now(Time(&at, seconds), Date(&at, DATE_SHORTDATE), Date(&at, DATE_LONGDATE), found?.Name) { Seconds = seconds };
    }

    /// <summary>The wall-clock time now in <paramref name="zone"/>, from whatever <see cref="Now"/> says here.</summary>
    private static DateTime In(TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(Now().ToUniversalTime(), zone);

    /// <summary>A moment as the date and time formatting functions take it.</summary>
    private static SYSTEMTIME Stamp(DateTime when) => new()
    {
        wYear = (ushort)when.Year,
        wMonth = (ushort)when.Month,
        wDayOfWeek = (ushort)when.DayOfWeek,
        wDay = (ushort)when.Day,
        wHour = (ushort)when.Hour,
        wMinute = (ushort)when.Minute,
        wSecond = (ushort)when.Second,
        wMilliseconds = (ushort)when.Millisecond,
    };

    /// <summary>
    /// The wall-clock time now in the zone with Windows id <paramref name="timeZoneId"/>; an
    /// empty or unknown id means local time. The calendar opens on this date for a clock set
    /// to another zone.
    /// </summary>
    public static DateTime NowIn(string? timeZoneId) =>
        Find(timeZoneId) is { } found ? In(found.Zone) : Now();

    /// <summary>
    /// The place a zone's display name (<see cref="Now.Zone"/>) stands for: its first city, "(UTC+09:00)
    /// Osaka, Sapporo, Tokyo" → "Osaka", or the name itself where it lists none; empty for none.
    /// </summary>
    public static string City(string? zone)
    {
        if (zone is not { Length: > 0 })
        {
            return "";
        }
        var close = zone.IndexOf(')');
        var places = close >= 0 ? zone[(close + 1)..].Trim() : zone;
        var first = places.Split(',')[0].Trim();
        return first.Length > 0 ? first : zone;
    }

    /// <summary>
    /// Until the next minute starts, plus a little so the tick lands inside it
    /// rather than on the edge of the old one. Every Windows zone is a whole
    /// number of minutes away from UTC, so this holds for every clock.
    /// </summary>
    public static uint UntilNextMinute()
    {
        SYSTEMTIME local;
        GetLocalTime(&local);
        var into = (uint)local.wSecond * 1000 + local.wMilliseconds;
        return 60_000u - Math.Min(into, 59_999u) + 40;
    }

    /// <summary>The same for the next second, while a clock shows seconds.</summary>
    public static uint UntilNextSecond()
    {
        SYSTEMTIME local;
        GetLocalTime(&local);
        return 1000u - Math.Min((uint)local.wMilliseconds, 999u) + 40;
    }

    private static (TimeZoneInfo Zone, string Name)? Find(string? id)
    {
        // A zone id names a key under Time Zones; nothing else may be read
        // through it.
        if (string.IsNullOrWhiteSpace(id) || id.Length > 255 || id.Contains('\\') || id.Contains('\0'))
        {
            return null;
        }
        lock (KnownZones)
        {
            if (KnownZones.TryGetValue(id, out var known))
            {
                return known;
            }
            (TimeZoneInfo, string)? found = null;
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
                found = (zone, DisplayName(zone));
            }
            catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException
                or System.Security.SecurityException)
            {
            }
            KnownZones[id] = found;
            return found;
        }
    }

    /// <summary>
    /// Windows' own name for the zone in the user's language, "(UTC+01:00)
    /// Amsterdam, Berlin, …". The registry keeps it as a reference into
    /// tzres.dll; .NET's DisplayName would follow its invariant culture instead.
    /// </summary>
    private static string DisplayName(TimeZoneInfo zone)
    {
        HKEY key;
        fixed (char* path = ZonesKey + zone.Id)
        {
            if (RegOpenKeyExW(HKEY.HKEY_LOCAL_MACHINE, path, 0, (uint)KEY.KEY_READ, &key) != 0)
            {
                return Fallback(zone);
            }
        }
        try
        {
            var text = stackalloc char[256];
            new Span<char>(text, 256).Clear();
            uint size = 0;
            fixed (char* value = "MUI_Display")
            {
                if (RegLoadMUIStringW(key, value, text, 255 * sizeof(char), &size, 0, null) == 0)
                {
                    var name = new string(text);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }
                }
            }
            return Fallback(zone);
        }
        finally
        {
            _ = RegCloseKey(key);
        }
    }

    private static string Fallback(TimeZoneInfo zone) =>
        string.IsNullOrWhiteSpace(zone.DisplayName) ? zone.Id : zone.DisplayName;

    /// <summary>
    /// The longest the time ever reads in this region's format, for measuring a clock's room
    /// without asking what the time is. Hours are the only part whose figures vary: a 12-hour
    /// format writes 1 o'clock with one figure and 11 with two, so a clock measured on the time
    /// showing would be narrower in the morning and the bar would move at ten. Formatting a
    /// fixed late evening gives the two-figure hour every format's widest reading has.
    /// </summary>
    public static string Widest(bool seconds) => At(23, 59, 59, seconds);

    /// <summary>
    /// The readings a clock on the bar is measured over (Scene.MeasureClock): the widest evening
    /// (<see cref="Widest"/>) and the widest morning, since a 12-hour format's two markers ("AM",
    /// "PM") are not the same width. The bar keeps room for the wider, so neither ten o'clock nor
    /// noon moves it.
    /// </summary>
    public static string[] Widths(bool seconds) => [At(23, 59, 59, seconds), At(11, 59, 59, seconds)];

    /// <summary>
    /// A given time of day in this region's format, for the tests that hold <see cref="Widest"/>
    /// against every hour there is. Nothing on the bar reads the clock to size itself.
    /// </summary>
    internal static string At(int hour, int minute, int second, bool seconds)
    {
        var at = new SYSTEMTIME
        {
            wYear = 2026, wMonth = 12, wDayOfWeek = 3, wDay = 30,
            wHour = (ushort)hour, wMinute = (ushort)minute, wSecond = (ushort)second, wMilliseconds = 0,
        };
        return Time(&at, seconds);
    }

    private static string Time(SYSTEMTIME* at, bool seconds)
    {
        char* text = stackalloc char[64];
        // The time pattern someone set in Karpo's localization settings, where they set one: the
        // one place that answers "how is a time written here" is Nls, and the clock asks it rather
        // than keeping a second answer of its own.
        if (Panels.Nls.TimePattern(seconds) is { Length: > 0 } pattern)
        {
            int chosen;
            fixed (char* picture = pattern)
            {
                chosen = GetTimeFormatEx(null, 0, at, picture, text, 64);
            }
            if (chosen > 1)
            {
                return Utf16(new ReadOnlySpan<char>(text, 64), chosen);
            }
        }
        // A null locale name means the user's default, formats included.
        var length = GetTimeFormatEx(null, seconds ? 0 : TIME_NOSECONDS, at, null, text, 64);
        return Utf16(new ReadOnlySpan<char>(text, 64), length);
    }

    private static string Date(SYSTEMTIME* at, uint format)
    {
        char* text = stackalloc char[128];
        // The same for the date: a pattern someone chose, else the region's own shape.
        var set = format == DATE_SHORTDATE ? Panels.Nls.Formats?.ShortDate : Panels.Nls.Formats?.LongDate;
        if (!string.IsNullOrWhiteSpace(set))
        {
            int chosen;
            fixed (char* picture = set)
            {
                chosen = GetDateFormatEx(null, 0, at, picture, text, 128, null);
            }
            if (chosen > 1)
            {
                return Utf16(new ReadOnlySpan<char>(text, 128), chosen);
            }
        }
        var length = GetDateFormatEx(null, format, at, null, text, 128, null);
        return Utf16(new ReadOnlySpan<char>(text, 128), length);
    }

    // The count the format functions return includes the terminating zero.
    private static string Utf16(ReadOnlySpan<char> text, int length)
    {
        var count = Math.Min(Math.Max(length, 1) - 1, text.Length);
        // String::from_utf16_lossy in the Rust: an unpaired surrogate becomes U+FFFD.
        return Encoding.Unicode.GetString(MemoryMarshal.AsBytes(text[..count]));
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetTimeFormatEx(
        char* lpLocaleName,
        uint dwFlags,
        SYSTEMTIME* lpTime,
        char* lpFormat,
        char* lpTimeStr,
        int cchTime);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetDateFormatEx(
        char* lpLocaleName,
        uint dwFlags,
        SYSTEMTIME* lpDate,
        char* lpFormat,
        char* lpDateStr,
        int cchDate,
        char* lpCalendar);
}
