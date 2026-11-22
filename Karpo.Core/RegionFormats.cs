// The date and time patterns this PC's region has, and what a pattern looks
// like when it is used.
//
// Both processes need them: the dock writes dates with them, and the settings
// app offers them in a list with a preview beside each — the list is the
// region's own, which is what Windows' "Change data formats" page shows, rather
// than a box someone types "dd/MM/yy" into and gets a subtly wrong year from.
//
// Straight to the NLS functions of kernel32 rather than through .NET's culture
// data, for the same reason Karpo.Dock\Panels\Nls.cs is: the dock runs with
// InvariantGlobalization, where .NET's culture is the invariant one whatever the
// person chose. Windows keeps their real answer.

using System.Runtime.InteropServices;

namespace Karpo.Core;

public static unsafe class RegionFormats
{
    // winnls.h, which is not in TerraFX.Interop.Windows.
    private const uint DATE_SHORTDATE = 0x00000001;
    private const uint DATE_LONGDATE = 0x00000002;
    private const uint LOCALE_RETURN_NUMBER = 0x20000000;
    private const uint LOCALE_IFIRSTDAYOFWEEK = 0x0000100C;
    private const uint LOCALE_IMEASURE = 0x0000000D;
    private const uint LOCALE_SDECIMAL = 0x0000000E;

    /// <summary>
    /// The date patterns the region itself offers, in Windows' own order and without repeats;
    /// <paramref name="long"/> asks for the long forms. Empty where Windows tells us nothing, which
    /// is the caller's cue to offer following Windows alone.
    /// </summary>
    public static IReadOnlyList<string> DateFormats(bool @long)
    {
        collected = [];
        try
        {
            _ = EnumDateFormatsExEx(&TakeDate, null, @long ? DATE_LONGDATE : DATE_SHORTDATE, 0);
            return Distinct(collected);
        }
        finally
        {
            collected = null;
        }
    }

    /// <summary>The time patterns the region offers ("HH:mm:ss", "h:mm:ss tt"), the same way.</summary>
    public static IReadOnlyList<string> TimeFormats()
    {
        collected = [];
        try
        {
            _ = EnumTimeFormatsEx(&TakeTime, null, 0, 0);
            return Distinct(collected);
        }
        finally
        {
            collected = null;
        }
    }

    /// <summary>
    /// <paramref name="day"/> written with <paramref name="pattern"/>, for the preview beside a
    /// pattern in the settings; null where Windows will not format it, which is a pattern to leave
    /// out of the list rather than to offer.
    /// </summary>
    public static string? Date(DateTime day, string pattern)
    {
        var at = Stamp(day);
        char* text = stackalloc char[128];
        int length;
        fixed (char* picture = pattern)
        {
            length = GetDateFormatEx(null, 0, &at, picture, text, 128, null);
        }
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    /// <summary>The same for a time of day.</summary>
    public static string? Time(DateTime at, string pattern)
    {
        var when = Stamp(at);
        char* text = stackalloc char[64];
        int length;
        fixed (char* picture = pattern)
        {
            length = GetTimeFormatEx(null, 0, &when, picture, text, 64);
        }
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    /// <summary>What the region itself writes, for the "Follow Windows" line's preview.</summary>
    public static string? FollowedDate(DateTime day, bool @long)
    {
        var at = Stamp(day);
        char* text = stackalloc char[128];
        var length = GetDateFormatEx(null, @long ? DATE_LONGDATE : DATE_SHORTDATE, &at, null, text, 128, null);
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    /// <summary>
    /// The region's first day of the week (LOCALE_IFIRSTDAYOFWEEK, counted from Monday), what the dock starts a week on
    /// and the first day's "Default (…)" names; Monday where it says none.
    /// </summary>
    public static DayOfWeek FollowedFirstDay()
    {
        uint value = 0;
        return GetLocaleInfoEx(null, LOCALE_IFIRSTDAYOFWEEK | LOCALE_RETURN_NUMBER, (char*)&value, sizeof(uint) / sizeof(char)) == 0
            ? DayOfWeek.Monday
            : (DayOfWeek)((value + 1) % 7);
    }

    /// <summary>
    /// Whether the region measures in US units (LOCALE_IMEASURE is 1), which is where Windows' own weather shows Fahrenheit;
    /// metric, or a region that does not say, is Celsius.
    /// </summary>
    public static bool FollowedFahrenheit()
    {
        uint value = 0;
        return GetLocaleInfoEx(null, LOCALE_IMEASURE | LOCALE_RETURN_NUMBER, (char*)&value, sizeof(uint) / sizeof(char)) != 0 && value == 1;
    }

    /// <summary>
    /// §638: a count — how many of something — as the region writes a whole number: its digits grouped with its own mark
    /// ("1,234", "1.234", "1’234", India's "1,23,456"), no decimals; its plain digits where Windows tells nothing. What every
    /// count of Karpo's words is said in (the strings' "{count:int}").
    /// </summary>
    public static string Count(long count)
    {
        var plain = count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        char* text = stackalloc char[64];
        char* point = stackalloc char[8];
        int length;
        fixed (char* value = plain)
        {
            length = GetNumberFormatEx(null, 0, value, null, text, 64);
        }
        if (length <= 1)
        {
            return plain;
        }
        var written = new string(text, 0, length - 1);
        // The region's decimals cut off: a count has none.
        var decimals = GetLocaleInfoEx(null, LOCALE_SDECIMAL, point, 8);
        return decimals > 1 && written.LastIndexOf(new string(point, 0, decimals - 1), StringComparison.Ordinal) is var at and > 0 ? written[..at] : written;
    }

    /// <summary>The same for the time.</summary>
    public static string? FollowedTime(DateTime at)
    {
        var when = Stamp(at);
        char* text = stackalloc char[64];
        var length = GetTimeFormatEx(null, 0, &when, null, text, 64);
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    // Windows hands each pattern to a callback. The list is thread-static rather than passed
    // through lParam because the callback is unmanaged and must not carry a managed object.
    [ThreadStatic]
    private static List<string>? collected;

    [UnmanagedCallersOnly]
    private static int TakeDate(char* pattern, uint calendar, nint state)
    {
        try
        {
            _ = calendar;
            _ = state;
            Take(pattern);
            return 1;
        }
        catch (Exception error)
        {
            Troubles.Report("Region formats: a date pattern", error);
            return 1;
        }
    }

    [UnmanagedCallersOnly]
    private static int TakeTime(char* pattern, nint state)
    {
        try
        {
            _ = state;
            Take(pattern);
            return 1;
        }
        catch (Exception error)
        {
            Troubles.Report("Region formats: a time pattern", error);
            return 1;
        }
    }

    private static void Take(char* pattern)
    {
        if (collected is not { } list || pattern == null)
        {
            return;
        }
        var text = new string(pattern);
        if (text.Length > 0)
        {
            list.Add(text);
        }
    }

    private static IReadOnlyList<string> Distinct(List<string>? patterns)
    {
        if (patterns is not { Count: > 0 })
        {
            return [];
        }
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var list = new List<string>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (seen.Add(pattern))
            {
                list.Add(pattern);
            }
        }
        return list;
    }

    private static SystemTime Stamp(DateTime at) => new()
    {
        Year = (ushort)at.Year,
        Month = (ushort)at.Month,
        DayOfWeek = (ushort)at.DayOfWeek,
        Day = (ushort)at.Day,
        Hour = (ushort)at.Hour,
        Minute = (ushort)at.Minute,
        Second = (ushort)at.Second,
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemTime
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetLocaleInfoEx(char* lpLocaleName, uint LCType, char* lpLCData, int cchData);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetNumberFormatEx(char* lpLocaleName, uint dwFlags, char* lpValue, void* lpFormat, char* lpNumberStr, int cchNumber);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetDateFormatEx(
        char* lpLocaleName,
        uint dwFlags,
        SystemTime* lpDate,
        char* lpFormat,
        char* lpDateStr,
        int cchDate,
        char* lpCalendar);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetTimeFormatEx(
        char* lpLocaleName,
        uint dwFlags,
        SystemTime* lpTime,
        char* lpFormat,
        char* lpTimeStr,
        int cchTime);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int EnumDateFormatsExEx(
        delegate* unmanaged<char*, uint, nint, int> lpDateFmtEnumProcExEx,
        char* lpLocaleName,
        uint dwFlags,
        nint lParam);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int EnumTimeFormatsEx(
        delegate* unmanaged<char*, nint, int> lpTimeFmtEnumProcEx,
        char* lpLocaleName,
        uint dwFlags,
        nint lParam);
}
