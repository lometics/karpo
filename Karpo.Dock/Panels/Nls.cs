// Day and month names and date formats from Windows' own locale data.
//
// The dock runs with InvariantGlobalization: .NET's culture data is the
// invariant culture's, which says "Wednesday" and starts weeks on Sunday
// whatever the user chose. Windows keeps the user's region format, and the
// taskbar's own calendar follows it, so every name and pattern comes from the
// NLS functions of kernel32. They are read afresh for each showing: the region
// format can change while Karpo runs.
//
// The month grid is Gregorian. GetDateFormatEx converts a date into the
// region's own calendar, so for the few region formats whose calendar has
// other months (Hijri, Hebrew, Um al-Qura, Persian) the calendar's texts are
// put together from the Gregorian month names instead, to agree with the grid.

using System.Runtime.InteropServices;
using System.Text;
using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.Panels;

internal static unsafe class Nls
{
    // winnls.h is not part of TerraFX.Interop.Windows; these come from there.
    private const uint LOCALE_RETURN_NUMBER = 0x20000000;
    private const uint LOCALE_SLONGDATE = 0x00000020;
    private const uint LOCALE_SSHORTDATE = 0x0000001F;
    private const uint LOCALE_STIMEFORMAT = 0x00001003;
    private const uint LOCALE_SMONTHDAY = 0x00000078;
    private const uint LOCALE_SMONTHNAME1 = 0x00000038;
    private const uint LOCALE_SSHORTESTDAYNAME1 = 0x00000060;
    private const uint LOCALE_SABBREVDAYNAME1 = 0x00000031;
    private const uint LOCALE_SDAYNAME1 = 0x0000002A;
    private const uint LOCALE_ICALENDARTYPE = 0x00001009;
    private const uint LOCALE_IFIRSTDAYOFWEEK = 0x0000100C;
    private const uint LOCALE_IFIRSTWEEKOFYEAR = 0x0000100D;
    private const uint LOCALE_IMEASURE = 0x0000000D;
    private const uint LOCALE_SDECIMAL = 0x0000000E;
    private const uint DATE_SHORTDATE = 0x00000001;
    private const uint DATE_LONGDATE = 0x00000002;
    private const uint DATE_YEARMONTH = 0x00000008;
    private const uint TIME_NOSECONDS = 0x00000002;
    private const uint CAL_GREGORIAN = 1;
    private const uint CAL_SMONTHNAME1 = 0x00000015;

    // The formats somebody set in Karpo's settings. Per thread so a test can set one without
    // reaching another test drawing beside it; the dock's own answer, which every widget reads, is
    // the shared one it applies once from the config (Apply).
    [ThreadStatic]
    private static LocalizationOptions? mine;

    private static LocalizationOptions? shared;

    /// <summary>
    /// Tests only, on their thread: the region whose names and patterns the dates are written in
    /// ("de-DE", "ru-RU", "fi-FI"), instead of this PC's; null for this PC's.
    /// </summary>
    [ThreadStatic]
    internal static string? Region;

    /// <summary>
    /// How Karpo writes dates and times: the settings someone chose, or null while every one of
    /// them follows Windows — which is the default and what Karpo did before the setting existed.
    /// <b>This is the only place an override is read</b>: every date and time in the app is written
    /// through the methods below, so a format cannot be followed by half the app and set in the
    /// other half. Setting it here is a test's way in; the dock uses <see cref="Apply"/>.
    /// </summary>
    public static LocalizationOptions? Formats
    {
        get => mine ?? shared;
        set => mine = value;
    }

    /// <summary>
    /// The formats from the config, for the whole process: the dock applies them when it reads the
    /// config and whenever it changes. A member left following is left following — nothing is cached
    /// here, and the Windows values are read afresh for each showing, so a region format changed
    /// while Karpo runs still reaches the next drawing.
    /// </summary>
    public static void Apply(LocalizationOptions? formats) => shared = formats?.Follows == true ? null : formats;

    /// <summary>
    /// What Windows' own regional format says now, as one string: the short and long date patterns,
    /// the time pattern, the first day of the week, the calendar and the measurement system. The
    /// dock keeps the last one it saw and rebuilds when it changes, because a region format changed
    /// while Karpo runs reaches the widths only at a rebuild — the dates on the bar are measured
    /// from templates, and the weather's room from its scale's widest reading.
    /// </summary>
    public static string Signature() =>
        string.Join(
            "|",
            Info(LOCALE_SSHORTDATE) ?? "",
            Info(LOCALE_SLONGDATE) ?? "",
            Info(LOCALE_STIMEFORMAT) ?? "",
            Info(LOCALE_IFIRSTDAYOFWEEK) ?? "",
            Info(LOCALE_ICALENDARTYPE) ?? "",
            Info(LOCALE_IMEASURE) ?? "",
            ShortDayName(DayOfWeek.Monday));

    /// <summary>
    /// Whether temperatures are written in Fahrenheit: the scale someone set on the Localization
    /// page, else the region's (<see cref="RegionFahrenheit"/>). The one answer every temperature
    /// asks — the weather widget has no scale of its own any more.
    /// </summary>
    public static bool Fahrenheit() => Formats?.IsFahrenheit(RegionFahrenheit()) ?? RegionFahrenheit();

    /// <summary>The region's decimal sign (LOCALE_SDECIMAL): what sets a stopwatch's tenth off; "." where it says none.</summary>
    public static string DecimalSeparator() => Info(LOCALE_SDECIMAL) is { Length: > 0 } sign ? sign : ".";

    /// <summary>Whether the user's region format measures in US units (<see cref="RegionFormats.FollowedFahrenheit"/>).</summary>
    public static bool RegionFahrenheit() => RegionFormats.FollowedFahrenheit();

    /// <summary>The first day of the week: the one someone set, else the user's region format's (<see cref="RegionFormats.FollowedFirstDay"/>).</summary>
    public static DayOfWeek FirstDayOfWeek() => Formats?.FirstDayOfWeek ?? RegionFormats.FollowedFirstDay();

    /// <summary>
    /// How the region counts the weeks of a year (LOCALE_IFIRSTWEEKOFYEAR): 0, week 1 holds January 1st;
    /// 1, week 1 is the first whole week; 2, week 1 is the first with at least four days (ISO 8601).
    /// </summary>
    public static int FirstWeekOfYear()
    {
        uint value = 0;
        if (GetLocaleInfoEx(null, LOCALE_IFIRSTWEEKOFYEAR | LOCALE_RETURN_NUMBER, (char*)&value, sizeof(uint) / sizeof(char)) == 0)
        {
            return 0;
        }
        return (int)Math.Min(value, 2);
    }

    /// <summary>The shortest name of <paramref name="day"/> ("Mo"), as the calendar's weekday row shows it.</summary>
    public static string ShortestDayName(DayOfWeek day) =>
        Info(LOCALE_SSHORTESTDAYNAME1 + DayNumber(day)) ?? day.ToString()[..2];

    /// <summary>The abbreviated name of <paramref name="day"/> ("Mon"), over the week view's columns.</summary>
    public static string ShortDayName(DayOfWeek day) =>
        Info(LOCALE_SABBREVDAYNAME1 + DayNumber(day)) ?? day.ToString()[..3];

    /// <summary>The full name of <paramref name="day"/> ("Wednesday").</summary>
    public static string DayName(DayOfWeek day) =>
        Info(LOCALE_SDAYNAME1 + DayNumber(day)) ?? day.ToString();

    /// <summary>The nominative name of <paramref name="month"/> (1–12) in the Gregorian calendar.</summary>
    public static string MonthName(int month)
    {
        int index = Math.Clamp(month, 1, 12) - 1;
        return (Gregorian ? Info(LOCALE_SMONTHNAME1 + (uint)index) : Calendar(CAL_SMONTHNAME1 + (uint)index))
            ?? new DateOnly(2000, index + 1, 1).ToString("MMMM");
    }

    /// <summary>"Wednesday, 17 September": the region's long date without its year, and with the weekday.</summary>
    public static string LongDateWithoutYear(DateOnly day)
    {
        if (!Gregorian)
        {
            return $"{DayName(day.DayOfWeek)}, {day.Day} {MonthName(day.Month)}";
        }
        if ((Chosen(Formats?.LongDate) ?? Info(LOCALE_SLONGDATE)) is { Length: > 0 } pattern
            && WithoutYear(pattern) is { Length: > 0 } shorter
            && Format(day, shorter) is { Length: > 0 } text)
        {
            return text;
        }
        return Format(day, DATE_LONGDATE) ?? day.ToString("D");
    }

    /// <summary>"Tuesday, 22 September 2026": the region's long date, the day view's title.</summary>
    public static string LongDate(DateOnly day) =>
        (Chosen(Formats?.LongDate) is { } pattern ? Format(day, pattern) : null)
        ?? (Gregorian ? Format(day, DATE_LONGDATE) : null)
        ?? $"{DayName(day.DayOfWeek)}, {day.Day} {MonthName(day.Month)} {day.Year}";

    /// <summary>"September 2026": the month title.</summary>
    public static string YearMonth(int year, int month) =>
        (Gregorian ? Format(new DateOnly(year, month, 1), DATE_YEARMONTH) : null) ?? $"{MonthName(month)} {year}";

    /// <summary>The region's short date ("17.09.2026").</summary>
    public static string ShortDate(DateOnly day) =>
        (Chosen(Formats?.ShortDate) is { } pattern ? Format(day, pattern) : null)
        ?? Format(day, DATE_SHORTDATE)
        ?? day.ToString("d");

    /// <summary>
    /// The region's short date without its year ("17.09.", "17/09", "9/17"): as much of a date as
    /// the width of a vertical bar holds.
    /// </summary>
    public static string ShortDateWithoutYear(DateOnly day)
    {
        if ((Chosen(Formats?.ShortDate) ?? (Gregorian ? Info(LOCALE_SSHORTDATE) : null)) is { Length: > 0 } pattern)
        {
            var tokens = Tokenize(pattern);
            for (int i = tokens.FindIndex(token => token.Field == 'y'); i >= 0; i = tokens.FindIndex(token => token.Field == 'y'))
            {
                RemoveField(tokens, i);
            }
            var shorter = string.Concat(tokens.Select(token => token.Text)).Trim();
            if (shorter.Length > 0 && tokens.Exists(token => token.Field != '\0') && Format(day, shorter) is { Length: > 0 } text)
            {
                return text;
            }
        }
        return MonthDay(day);
    }

    /// <summary>
    /// The region's short date ("28.12.2026") a field at a time, in its order: "28", "12", "2026", each
    /// as the pattern writes it ("d", "MM", "yy") — a date down a side bar too narrow for it on a row,
    /// a row each (DateText.SideForms). Without <paramref name="year"/> no year; with
    /// <paramref name="numbers"/> a month the pattern names ("Dez.") is its number ("12"). A weekday
    /// and an era are left out. Each field comes back with its letter: 'd', 'M' or 'y'.
    /// </summary>
    public static (char Field, string Text)[] ShortDateFields(DateOnly day, bool year, bool numbers = false)
    {
        if ((Chosen(Formats?.ShortDate) ?? (Gregorian ? Info(LOCALE_SSHORTDATE) : null)) is { Length: > 0 } pattern)
        {
            var fields = new List<(char, string)>();
            foreach (var token in Tokenize(pattern))
            {
                if (token.Field is not ('d' or 'M' or 'y') || (token.Field == 'd' && token.Text.Length >= 3) || (token.Field == 'y' && !year))
                {
                    continue;
                }
                var picture = numbers && token.Field == 'M' && token.Text.Length >= 3 ? "MM" : token.Text;
                if (Format(day, picture) is { Length: > 0 } text)
                {
                    fields.Add((token.Field, text));
                }
            }
            if (fields.Count > 0)
            {
                return [.. fields];
            }
        }
        var invariant = global::System.Globalization.CultureInfo.InvariantCulture;
        (char, string)[] numeric = [('d', day.Day.ToString(invariant)), ('M', day.Month.ToString(invariant))];
        return year ? [.. numeric, ('y', day.Year.ToString(invariant))] : numeric;
    }

    /// <summary>Whether the region's short date writes a name in it — a month's ("MMM") or a weekday's — whose width changes with the date.</summary>
    public static bool ShortDateHasNames() =>
        (Chosen(Formats?.ShortDate) ?? (Gregorian ? Info(LOCALE_SSHORTDATE) : null)) is { Length: > 0 } pattern
        && Tokenize(pattern).Exists(token => token.Field is 'M' or 'd' && token.Text.Length >= 3);

    /// <summary>
    /// The weekday and the region's short date on one line, as the region writes them: "Thu,
    /// 17.09.2026" where the long date starts with the day's name, "17.09.2026 Thu" where it ends
    /// with it, with the long date's own separator between them. With <paramref name="year"/> false the
    /// date is the day and the month alone ("Thu, 17.09."), in the same order and with the same
    /// separator: a taskbar widget at its automatic size has no room for a year nobody reads off it.
    /// </summary>
    public static string DayAndShortDate(DateOnly day, bool year = true)
    {
        var date = year ? ShortDate(day) : ShortDateWithoutYear(day);
        // A pattern someone chose in the localization settings may already have the day's name in
        // it ("ddd d MMM"); then the date is the whole line rather than a second weekday beside the
        // first.
        if (HasWeekday(Chosen(Formats?.ShortDate)))
        {
            return date;
        }
        var name = ShortDayName(day.DayOfWeek);
        var (first, separator) = DayPlace();
        return first ? $"{name}{separator}{date}" : $"{date}{separator}{name}";
    }

    /// <summary>Whether <paramref name="pattern"/> writes the day's name (ddd, dddd) as well as its number.</summary>
    private static bool HasWeekday(string? pattern) =>
        pattern is { Length: > 0 } && Tokenize(pattern).Exists(token => token.Field == 'd' && token.Text.Length >= 3);

    /// <summary>
    /// Where the region's long date puts the day's name — before the rest of the date or after it —
    /// and what stands between them. A long date without a weekday puts it first, after a space.
    /// </summary>
    private static (bool First, string Separator) DayPlace()
    {
        if ((Chosen(Formats?.LongDate) ?? (Gregorian ? Info(LOCALE_SLONGDATE) : null)) is not { Length: > 0 } pattern)
        {
            return (true, " ");
        }
        var tokens = Tokenize(pattern);
        int weekday = tokens.FindIndex(token => token.Field == 'd' && token.Text.Length >= 3);
        if (weekday < 0)
        {
            return (true, " ");
        }
        int other = tokens.FindIndex(token => token.Field is 'M' or 'y' || (token.Field == 'd' && token.Text.Length < 3));
        var first = other < 0 || weekday < other;
        // The literals between the name and the rest of the date (", ", " ", "、").
        var from = first ? weekday + 1 : other + 1;
        var to = first ? (other < 0 ? tokens.Count : other) : weekday;
        var between = new StringBuilder();
        for (int i = Math.Max(0, from); i < Math.Min(to, tokens.Count); i++)
        {
            if (tokens[i].Field == '\0')
            {
                between.Append(tokens[i].Text.Replace("'", "", StringComparison.Ordinal));
            }
        }
        var separator = between.ToString();
        return (first, string.IsNullOrWhiteSpace(separator) ? " " : separator);
    }

    /// <summary>"Sep 15" or "15. Sep": the region's month-day pattern with the abbreviated month.</summary>
    public static string MonthDay(DateOnly day)
    {
        if (!Gregorian)
        {
            return $"{day.Day} {MonthName(day.Month)}";
        }
        if (Info(LOCALE_SMONTHDAY) is { Length: > 0 } pattern)
        {
            // Only the full month name becomes the abbreviated one; a
            // numeric month (M, MM) stays as the region has it.
            var shorter = Abbreviate(pattern);
            if (Format(day, shorter) is { Length: > 0 } text)
            {
                return text;
            }
        }
        return ShortDate(day);
    }

    /// <summary>A time of day without seconds, as the taskbar clock shows it ("10:30", "10:30 AM").</summary>
    public static string Time(DateTime at) => TimeOfDay(at, seconds: false) ?? at.ToString("t");

    /// <summary>
    /// A time of day as Karpo writes it: the pattern someone set — with the seconds taken out of it
    /// where <paramref name="seconds"/> is false, so 12- or 24-hour is chosen once and holds
    /// everywhere — else the region's own. Null only where Windows refuses to format it at all.
    /// </summary>
    public static string? TimeOfDay(DateTime at, bool seconds)
    {
        var system = System(DateOnly.FromDateTime(at), at.Hour, at.Minute);
        system.wSecond = (ushort)at.Second;
        char* text = stackalloc char[64];
        if (TimePattern(seconds) is { Length: > 0 } pattern)
        {
            int chosen;
            fixed (char* picture = pattern)
            {
                chosen = GetTimeFormatEx(null, 0, &system, picture, text, 64);
            }
            if (chosen > 1)
            {
                return new string(text, 0, chosen - 1);
            }
        }
        int length = GetTimeFormatEx(null, seconds ? 0 : TIME_NOSECONDS, &system, null, text, 64);
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    /// <summary>
    /// The time pattern someone set, with its seconds taken out where they are not wanted; null
    /// while the time follows Windows.
    /// </summary>
    public static string? TimePattern(bool seconds)
    {
        if (Chosen(Formats?.Time) is not { } pattern)
        {
            return null;
        }
        return seconds ? pattern : WithoutSeconds(pattern);
    }

    /// <summary>
    /// A time pattern without its seconds: "HH:mm:ss" becomes "HH:mm", "h:mm:ss tt" becomes
    /// "h:mm tt". The separator that led into the seconds goes with them, as Windows' own
    /// TIME_NOSECONDS does, and a pattern with no seconds in it comes back as it was.
    /// </summary>
    internal static string WithoutSeconds(string pattern)
    {
        var result = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '\'')
            {
                quoted = !quoted;
                result.Append(c);
                continue;
            }
            if (!quoted && c == 's')
            {
                while (i + 1 < pattern.Length && pattern[i + 1] == 's')
                {
                    i++;
                }
                var cut = result.Length;
                while (cut > 0 && !char.IsLetter(result[cut - 1]))
                {
                    cut--;
                }
                result.Length = cut;
                continue;
            }
            result.Append(c);
        }
        var text = result.ToString().Trim();
        return text.Length > 0 ? text : pattern;
    }

    /// <summary>A pattern somebody set, or null for one that is empty or only spaces (which follows).</summary>
    private static string? Chosen(string? pattern) => string.IsNullOrWhiteSpace(pattern) ? null : pattern;

    /// <summary>
    /// The long date pattern with the year and its separators taken out, the weekday added when
    /// the region leaves it out, and a padded day number unpadded next to a month name.
    /// </summary>
    internal static string WithoutYear(string pattern)
    {
        var tokens = Tokenize(pattern);
        // Eras (g, gg) belong to the year.
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].Field == 'g')
            {
                RemoveField(tokens, i);
            }
        }
        for (int i = tokens.FindIndex(t => t.Field == 'y'); i >= 0; i = tokens.FindIndex(t => t.Field == 'y'))
        {
            RemoveField(tokens, i);
        }
        bool weekday = tokens.Exists(t => t.Field == 'd' && t.Text.Length >= 3);
        bool monthName = tokens.Exists(t => t.Field == 'M' && t.Text.Length >= 3);
        var result = new StringBuilder();
        foreach (var token in tokens)
        {
            // "05 September" reads as a typo; the taskbar's calendar says "5 September".
            result.Append(monthName && token.Field == 'd' && token.Text.Length == 2 ? "d" : token.Text);
        }
        var text = result.ToString().Trim();
        if (text.Length == 0 || !tokens.Exists(t => t.Field != '\0'))
        {
            return "";
        }
        return weekday ? text : "dddd, " + text;
    }

    /// <summary>
    /// The date patterns this region itself offers — what Windows' own "Change data formats" page
    /// lists, and what Karpo's localization settings offer instead of a box someone types
    /// "dd/MM/yy" into. Both processes ask <see cref="RegionFormats"/>, so the dock and the settings
    /// app cannot offer different lists.
    /// </summary>
    public static IReadOnlyList<string> DateFormats(bool @long) => RegionFormats.DateFormats(@long);

    /// <summary>The time patterns this region offers ("HH:mm:ss", "h:mm:ss tt"), the same way.</summary>
    public static IReadOnlyList<string> TimeFormats() => RegionFormats.TimeFormats();

    /// <summary>Takes out the field at <paramref name="index"/> with the literals that belong to it.</summary>
    private static void RemoveField(List<Token> tokens, int index)
    {
        // The field may be the first thing in the pattern ("yyyy-MM-dd", which a person can choose
        // in the localization settings even where their region would not write it that way), and
        // then there is nothing before it to look through.
        int previous = index > 0 ? tokens.FindLastIndex(index - 1, t => t.Field != '\0') : -1;
        int next = index + 1 < tokens.Count ? tokens.FindIndex(index + 1, t => t.Field != '\0') : -1;
        if (next < 0)
        {
            // Last: the separator before it and anything after it ("MMMM d, yyyy", "d MMMM yyyy 'г.'").
            int from = previous + 1;
            tokens.RemoveRange(from, tokens.Count - from);
        }
        else
        {
            // First or in the middle: it and the literals up to the next field ("yyyy'年'M'月'd'日'").
            tokens.RemoveRange(index, next - index);
        }
    }

    private readonly record struct Token(char Field, string Text);

    /// <summary>
    /// A date picture as fields (runs of d, M, y, g) and literals (quoted text and everything
    /// else), each keeping its original spelling so the pattern can be put back together.
    /// </summary>
    private static List<Token> Tokenize(string pattern)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < pattern.Length)
        {
            char c = pattern[i];
            if (c is 'd' or 'M' or 'y' or 'g')
            {
                int start = i;
                while (i < pattern.Length && pattern[i] == c)
                {
                    i++;
                }
                tokens.Add(new Token(c, pattern[start..i]));
            }
            else if (c == '\'')
            {
                int start = i++;
                while (i < pattern.Length)
                {
                    if (pattern[i] == '\'')
                    {
                        // '' inside quotes is a quote.
                        if (i + 1 < pattern.Length && pattern[i + 1] == '\'')
                        {
                            i += 2;
                            continue;
                        }
                        i++;
                        break;
                    }
                    i++;
                }
                tokens.Add(new Token('\0', pattern[start..i]));
            }
            else
            {
                int start = i;
                while (i < pattern.Length && pattern[i] is not ('d' or 'M' or 'y' or 'g' or '\''))
                {
                    i++;
                }
                tokens.Add(new Token('\0', pattern[start..i]));
            }
        }
        return tokens;
    }

    private static string Abbreviate(string pattern)
    {
        var result = new StringBuilder();
        foreach (var token in Tokenize(pattern))
        {
            result.Append(token.Field == 'M' && token.Text.Length >= 4 ? "MMM" : token.Text);
        }
        return result.ToString();
    }

    /// <summary>LOCALE_SSHORTESTDAYNAME1 and LOCALE_SDAYNAME1 are Monday; Sunday is the seventh.</summary>
    private static uint DayNumber(DayOfWeek day) => (uint)(((int)day + 6) % 7);

    /// <summary>
    /// Whether the region's calendar has the Gregorian months (Gregorian itself, and the Japanese,
    /// Taiwanese, Korean and Thai calendars, which only count years differently).
    /// </summary>
    private static bool Gregorian
    {
        get
        {
            uint type = 1;
            fixed (char* region = Region)
            {
                if (GetLocaleInfoEx(region, LOCALE_ICALENDARTYPE | LOCALE_RETURN_NUMBER, (char*)&type, sizeof(uint) / sizeof(char)) == 0)
                {
                    return true;
                }
            }
            return type is 1 or 2 or 3 or 4 or 5 or 7 or 9 or 10 or 11 or 12;
        }
    }

    private static string? Info(uint type)
    {
        char* text = stackalloc char[128];
        int length;
        fixed (char* region = Region)
        {
            length = GetLocaleInfoEx(region, type, text, 128);
        }
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    private static string? Calendar(uint type)
    {
        char* text = stackalloc char[128];
        int length;
        fixed (char* region = Region)
        {
            length = GetCalendarInfoEx(region, CAL_GREGORIAN, null, type, text, 128, null);
        }
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    private static string? Format(DateOnly day, uint flags)
    {
        var system = System(day, 0, 0);
        char* text = stackalloc char[128];
        int length;
        fixed (char* region = Region)
        {
            length = GetDateFormatEx(region, flags, &system, null, text, 128, null);
        }
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    private static string? Format(DateOnly day, string pattern)
    {
        var system = System(day, 0, 0);
        char* text = stackalloc char[128];
        int length;
        fixed (char* picture = pattern)
        fixed (char* region = Region)
        {
            length = GetDateFormatEx(region, 0, &system, picture, text, 128, null);
        }
        return length > 1 ? new string(text, 0, length - 1) : null;
    }

    private static SYSTEMTIME System(DateOnly day, int hour, int minute) => new()
    {
        wYear = (ushort)day.Year,
        wMonth = (ushort)day.Month,
        wDay = (ushort)day.Day,
        wDayOfWeek = (ushort)day.DayOfWeek,
        wHour = (ushort)hour,
        wMinute = (ushort)minute,
    };

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetLocaleInfoEx(char* lpLocaleName, uint LCType, char* lpLCData, int cchData);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetCalendarInfoEx(
        char* lpLocaleName,
        uint Calendar,
        char* lpReserved,
        uint CalType,
        char* lpCalData,
        int cchData,
        uint* lpValue);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetDateFormatEx(
        char* lpLocaleName,
        uint dwFlags,
        SYSTEMTIME* lpDate,
        char* lpFormat,
        char* lpDateStr,
        int cchDate,
        char* lpCalendar);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern int GetTimeFormatEx(
        char* lpLocaleName,
        uint dwFlags,
        SYSTEMTIME* lpTime,
        char* lpFormat,
        char* lpTimeStr,
        int cchTime);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern BOOL EnumDateFormatsExEx(
        delegate* unmanaged<char*, uint, LPARAM, BOOL> lpDateFmtEnumProcExEx,
        char* lpLocaleName,
        uint dwFlags,
        LPARAM lParam);

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern BOOL EnumTimeFormatsEx(
        delegate* unmanaged<char*, LPARAM, BOOL> lpTimeFmtEnumProcEx,
        char* lpLocaleName,
        uint dwFlags,
        LPARAM lParam);
}
