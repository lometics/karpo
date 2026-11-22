// How Karpo writes dates, times and units, as lists to choose from: the settings app's Localization page
// (SettingsModel.Formats.cs) and the edit bar's ⚙ › Formats › (KarpoMenu.Formats.cs, §337) offer and
// read the same.
//
// Every one of them is a list whose first entry follows Windows — the default, and what Karpo did before the
// setting existed, "Default (22/05/2026)" with what Windows writes (§518; "Follow Windows" before) — followed by
// the region's own patterns (RegionFormats, the same list Windows' "Change data formats" page shows). A pattern is chosen from that list rather than typed, so
// nobody can write "dd/MM/yy" and get a subtly wrong year; each entry is shown as the date it produces,
// because "22/05/2026" is what somebody is choosing and "dd/MM/yyyy" is jargon.
//
// The dock reads the settings through Karpo.Dock\Panels\Nls.cs, the only place a format is read there.

namespace Karpo.Core;

/// <summary>The formats' lists and where a setting stands in them; see the top of the file.</summary>
public static class FormatChoices
{
    /// <summary>
    /// A Friday afternoon, the date and time every entry in the lists is shown as: its day past the 12th so a date reads one
    /// way only, in a month and on a day of short names so a long date fits at its row's end in its "Default (…)" (§518;
    /// §337 to §516 a Tuesday in September).
    /// </summary>
    public static readonly DateTime Sample = new(2026, 5, 22, 14, 40, 7);

    /// <summary>The days in the order the first day's list has them after its default: Sunday to Saturday, as <see cref="DayOfWeek"/> has them.</summary>
    public static readonly DayOfWeek[] Days = [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday];

    /// <summary>"Default (22/05/2026)", then one entry per short date pattern of <paramref name="patterns"/>, shown as what it writes.</summary>
    public static IReadOnlyList<string> ShortDates(IReadOnlyList<string> patterns) =>
        Choices(RegionFormats.FollowedDate(Sample, @long: false), patterns, pattern => RegionFormats.Date(Sample, pattern));

    /// <summary>The same for the long date patterns.</summary>
    public static IReadOnlyList<string> LongDates(IReadOnlyList<string> patterns) =>
        Choices(RegionFormats.FollowedDate(Sample, @long: true), patterns, pattern => RegionFormats.Date(Sample, pattern));

    /// <summary>The same for the time patterns.</summary>
    public static IReadOnlyList<string> Times(IReadOnlyList<string> patterns) =>
        Choices(RegionFormats.FollowedTime(Sample), patterns, pattern => RegionFormats.Time(Sample, pattern));

    /// <summary>"Default (Monday)", the region's first day, then Sunday to Saturday (<see cref="Days"/>).</summary>
    public static IReadOnlyList<string> FirstDays
    {
        get
        {
            string[] days =
            [
                S.SettingsLocalizationFirstDaySunday,
                S.SettingsLocalizationFirstDayMonday,
                S.SettingsLocalizationFirstDayTuesday,
                S.SettingsLocalizationFirstDayWednesday,
                S.SettingsLocalizationFirstDayThursday,
                S.SettingsLocalizationFirstDayFriday,
                S.SettingsLocalizationFirstDaySaturday,
            ];
            return [S.SettingsValueDefault(days[(int)RegionFormats.FollowedFirstDay()]), .. days];
        }
    }

    /// <summary>"Default (Celsius (°C))", the region's, then Celsius, Fahrenheit: the order of <see cref="TemperatureUnit"/>.</summary>
    public static IReadOnlyList<string> Temperatures =>
    [
        S.SettingsValueDefault(RegionFormats.FollowedFahrenheit() ? S.SettingsLocalizationTemperatureFahrenheit : S.SettingsLocalizationTemperatureCelsius),
        S.SettingsLocalizationTemperatureCelsius,
        S.SettingsLocalizationTemperatureFahrenheit,
    ];

    /// <summary>"Default (KB, MB, GB)", as Windows shows them, then powers of 1000, powers of 1024: the order of <see cref="ByteUnit"/>.</summary>
    public static IReadOnlyList<string> Sizes =>
        [S.SettingsValueDefault(Units[0]), S.SettingsLocalizationSizesDecimal, S.SettingsLocalizationSizesBinary];

    /// <summary>§411 (R14): what the row says of each of <see cref="Sizes"/> at its end, short: their units, the default's as it is called.</summary>
    public static IReadOnlyList<string> SizesSaid => [S.SettingsValueDefault(Units[0]), .. Units.Skip(1)];

    /// <summary>The units of each of <see cref="Sizes"/>.</summary>
    private static readonly string[] Units =
    [
        "KB, MB, GB", // not-ui: units, the same in every language Windows speaks
        "kB, MB, GB", // not-ui: units
        "KiB, MiB, GiB", // not-ui: units
    ];

    /// <summary>0 follows Windows; 1 to 7 are Sunday to Saturday.</summary>
    public static int FirstDayIndex(DayOfWeek? day) => day is { } chosen ? (int)chosen + 1 : 0;

    /// <summary>The day entry <paramref name="index"/> of <see cref="FirstDays"/> stands for; null for following Windows.</summary>
    public static DayOfWeek? FirstDayAt(int index) => index is >= 1 and <= 7 ? (DayOfWeek)(index - 1) : null;

    /// <summary>"Default (…)", what Windows writes (Windows' own where it says nothing), and then each pattern, each shown as what it writes.</summary>
    public static IReadOnlyList<string> Choices(string? followed, IReadOnlyList<string> patterns, Func<string, string?> write)
    {
        var list = new List<string>(patterns.Count + 1)
        {
            S.SettingsValueDefault(followed is { Length: > 0 } shown ? shown : S.SettingsValueWindows),
        };
        foreach (var pattern in patterns)
        {
            if (write(pattern) is { Length: > 0 } text)
            {
                list.Add(text);
            }
        }
        return list;
    }

    /// <summary>Where <paramref name="chosen"/> stands in the list; 0 (follow) for nothing set.</summary>
    public static int IndexOf(string? chosen, IReadOnlyList<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(chosen))
        {
            return 0;
        }
        for (var i = 0; i < patterns.Count; i++)
        {
            if (string.Equals(patterns[i], chosen, StringComparison.Ordinal))
            {
                return i + 1;
            }
        }
        // A pattern the region no longer offers (its format was changed under us): follow again.
        return 0;
    }

    /// <summary>The pattern an index stands for; null for the first entry, which follows Windows.</summary>
    public static string? Pattern(int index, IReadOnlyList<string> patterns) =>
        index >= 1 && index <= patterns.Count ? patterns[index - 1] : null;
}
