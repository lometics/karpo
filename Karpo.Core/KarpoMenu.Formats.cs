// §335, §337: "Formats ›", a level: how Karpo writes dates, times and units, every setting of the
// Localization page's but the language — short date, long date, time, first day of the week (its "Date
// and time formats" expander), temperature, file sizes —, each a choice of FormatChoices' lists, Windows'
// own first ("Default (…)", §518) and each pattern shown as what it writes, written to "formats" as the page writes it.
// Then Windows' region settings, opened by the dock (KarpoServices.Open), and under a rule "Reset all",
// greyed while everything follows (§411: Reset all the one name of a reset, last; §337 to §410 "Follow Windows
// for everything"). The level's row is a page and says nothing at its end (§411; §337 to §410 the short date and
// the time). The dock writes with them at once (Dock.Apply: Panels.Nls).

namespace Karpo.Core;

/// <summary>§337: what opens an address of Windows' (ms-settings:) or of the web for a row.</summary>
public sealed partial record KarpoServices
{
    /// <summary>Opens an address in what Windows has for it: a page of Windows' Settings, a site; nothing where not given.</summary>
    public Action<string> Open { get; init; } = _ => { };
}

public static partial class KarpoMenu
{
    // The rows' keys.
    public const string ShortDateRow = "FormatShortDate";
    public const string LongDateRow = "FormatLongDate";
    public const string TimeRow = "FormatTime";
    public const string FirstDayRow = "FormatFirstDay";
    public const string TemperatureRow = "FormatTemperature";
    public const string SizesRow = "FormatSizes";
    public const string FollowWindowsRow = "FormatsFollowWindows";
    public const string RegionRow = "FormatsRegion";

    /// <summary>Windows' own regional format page, which the formats follow by default.</summary>
    public const string RegionSettings = "ms-settings:regionformatting";

    /// <summary>
    /// "Formats ›", saying at its end whether they follow Windows (§517; §411 to §513 nothing): every format of <paramref name="config"/>'s, Windows'
    /// region settings, then under a rule "Reset all" (§411, R11: §337 to §410 "Follow Windows for everything").
    /// </summary>
    public static IReadOnlyList<OptionRow> Formats(Config config, KarpoServices services)
    {
        var formats = config.Formats;
        var shortDates = RegionFormats.DateFormats(@long: false);
        var longDates = RegionFormats.DateFormats(@long: true);
        var times = RegionFormats.TimeFormats();
        // §517: at its row's end whether they follow Windows ("Default (Windows' own)", §518) or are the person's own.
        OptionRow In(OptionRow row) => row with
        {
            Group = S.SettingsLocalizationFormatsSection,
            GroupGlyph = OptionGlyphs.DateAndTime,
            GroupSummary = formats.Follows ? S.SettingsValueDefault(S.SettingsValueWindows) : S.SettingsLookCustom,
        };
        return
        [
            In(Pattern(ShortDateRow, S.SettingsLocalizationShortDateHeader, OptionGlyphs.Day, FormatChoices.ShortDates(shortDates), shortDates, formats.ShortDate, pattern => formats.ShortDate = pattern)),
            In(Pattern(LongDateRow, S.SettingsLocalizationLongDateHeader, OptionGlyphs.Month, FormatChoices.LongDates(longDates), longDates, formats.LongDate, pattern => formats.LongDate = pattern)),
            In(Pattern(TimeRow, S.SettingsLocalizationTimeHeader, OptionGlyphs.Time, FormatChoices.Times(times), times, formats.Time, pattern => formats.Time = pattern)),
            In(Listed(FirstDayRow, S.SettingsLocalizationFirstDayHeader, OptionGlyphs.Week, FormatChoices.FirstDays, FormatChoices.FirstDayIndex(formats.FirstDayOfWeek), at => formats.FirstDayOfWeek = FormatChoices.FirstDayAt(at))),
            In(Listed(TemperatureRow, S.SettingsLocalizationTemperatureHeader, OptionGlyphs.Temperature, FormatChoices.Temperatures, (int)formats.Temperature, at => formats.Temperature = (TemperatureUnit)at)),
            // §411 (R14): at the row's end its units alone.
            In(Listed(SizesRow, S.SettingsLocalizationSizesHeader, OptionGlyphs.Disk, FormatChoices.Sizes, (int)formats.Sizes, at => formats.Sizes = (ByteUnit)at) with { ChoicesSaid = FormatChoices.SizesSaid }),
            In(new OptionRow(RegionRow, OptionRowKind.Action, S.SettingsLocalizationRegionHeader)
            {
                Glyph = OptionGlyphs.Launch,
                Set = _ =>
                {
                    services.Open(RegionSettings);
                    return false;
                },
            }),
            // The page's last card, as it puts back every one above it: dim while everything follows.
            In(new OptionRow(FollowWindowsRow + "-", OptionRowKind.Heading, "")),
            In(new OptionRow(FollowWindowsRow, OptionRowKind.Action, S.SettingsAppearanceCustomResetAll)
            {
                Glyph = OptionGlyphs.Reset,
                Enabled = !formats.Follows,
                Set = _ =>
                {
                    if (config.Formats.Follows)
                    {
                        return false;
                    }
                    config.Formats = new LocalizationOptions();
                    return true;
                },
            }),
        ];
    }

    /// <summary>
    /// A format chosen from the region's <paramref name="patterns"/> (<paramref name="choices"/>, Windows' own first,
    /// "Default (22/05/2026)", §518), <paramref name="now"/> ticked — one the region no longer offers as followed —; a
    /// pick kept (<paramref name="keep"/>, null for following) where it is another.
    /// </summary>
    private static OptionRow Pattern(string key, string title, string glyph, IReadOnlyList<string> choices, IReadOnlyList<string> patterns, string? now, Action<string?> keep) =>
        Listed(key, title, glyph, choices, FormatChoices.IndexOf(now, patterns), at => keep(FormatChoices.Pattern(at, patterns)));

    /// <summary>A choice of <paramref name="choices"/>, <paramref name="chosen"/> ticked; a pick kept (<paramref name="keep"/>) where it is another.</summary>
    private static OptionRow Listed(string key, string title, string glyph, IReadOnlyList<string> choices, int chosen, Action<int> keep) =>
        new(key, OptionRowKind.Choice, title)
        {
            Glyph = glyph,
            Choices = choices,
            Chosen = chosen,
            Set = at =>
            {
                if (at < 0 || at >= choices.Count || at == chosen)
                {
                    return false;
                }
                keep(at);
                return true;
            },
        };
}
