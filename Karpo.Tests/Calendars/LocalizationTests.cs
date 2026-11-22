// The localization settings: every format either follows Windows — which is what
// Karpo did before the setting existed — or is written the way somebody chose.
//
// Nothing here asserts what this PC's region format actually is: the tests hold
// the *rules* (a set format is used, a cleared one goes back to Windows, a
// change reaches the next reading, seconds come out of a chosen time pattern),
// so they pass in any region.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Panels;

namespace Karpo.Tests.Calendars;

public sealed class LocalizationTests : IDisposable
{
    private static readonly DateOnly Day = new(2026, 9, 22);
    private static readonly DateTime At = new(2026, 9, 22, 14, 40, 7);

    public void Dispose()
    {
        Nls.Formats = null;
        Clock.Now = Clock.Real;
    }

    /// <summary>
    /// Nothing set is "follow Windows": the date reads as the region writes it, and it is the same
    /// answer Karpo gave before the setting existed.
    /// </summary>
    [Fact]
    public void FollowingWindowsIsTheDefault()
    {
        Nls.Formats = null;
        var followed = Nls.ShortDate(Day);

        Assert.False(string.IsNullOrWhiteSpace(followed));
        // A LocalizationOptions that has been made but not set follows too, and says so.
        Assert.True(new LocalizationOptions().Follows);
        Nls.Formats = new LocalizationOptions();
        Assert.Equal(followed, Nls.ShortDate(Day));
    }

    /// <summary>A pattern someone chose is what is written, whatever the region would have said.</summary>
    [Fact]
    public void AChosenDatePatternIsWhatIsWritten()
    {
        Nls.Formats = new LocalizationOptions { ShortDate = "yyyy-MM-dd", LongDate = "dddd" };

        Assert.Equal("2026-09-22", Nls.ShortDate(Day));
        Assert.Equal(Nls.DayName(DayOfWeek.Tuesday), Nls.LongDate(Day));
    }

    /// <summary>
    /// The weekday-and-date line the calendar shows on the bar is built from the same answer, so setting the
    /// short date reaches the widget rather than half of it.
    /// </summary>
    [Fact]
    public void TheWidgetsLineFollowsTheSetting()
    {
        Nls.Formats = new LocalizationOptions { ShortDate = "yyyy-MM-dd" };

        Assert.Contains("2026-09-22", Nls.DayAndShortDate(Day), StringComparison.Ordinal);
    }

    /// <summary>
    /// A setting changed while Karpo runs is read at the next drawing: nothing about a format is
    /// cached, which is also what keeps a followed format following when Windows' own changes.
    /// </summary>
    [Fact]
    public void AChangeReachesTheNextReading()
    {
        Nls.Formats = new LocalizationOptions { ShortDate = "yyyy-MM-dd" };
        Assert.Equal("2026-09-22", Nls.ShortDate(Day));

        Nls.Formats = new LocalizationOptions { ShortDate = "dd.MM.yy" };
        Assert.Equal("22.09.26", Nls.ShortDate(Day));

        // Cleared, it goes back to the region's own without a restart.
        Nls.Formats = null;
        var followed = Nls.ShortDate(Day);
        Nls.Formats = new LocalizationOptions { ShortDate = "dd.MM.yy" };
        Assert.Equal("22.09.26", Nls.ShortDate(Day));
        Nls.Formats = null;
        Assert.Equal(followed, Nls.ShortDate(Day));
    }

    /// <summary>
    /// A time pattern is chosen once, and a widget that shows no seconds takes them out of it
    /// rather than asking for a second pattern — so 12- or 24-hour cannot disagree with itself.
    /// </summary>
    [Theory]
    [InlineData("HH:mm:ss", "HH:mm")]
    [InlineData("H:mm:ss", "H:mm")]
    [InlineData("h:mm:ss tt", "h:mm tt")]
    [InlineData("hh.mm.ss tt", "hh.mm tt")]
    [InlineData("HH:mm", "HH:mm")]
    public void SecondsComeOutOfAChosenTimePattern(string pattern, string expected) =>
        Assert.Equal(expected, Nls.WithoutSeconds(pattern));

    /// <summary>A pattern of nothing but seconds is left alone rather than becoming empty.</summary>
    [Fact]
    public void APatternOfNothingButSecondsIsKept() => Assert.Equal("ss", Nls.WithoutSeconds("ss"));

    [Fact]
    public void AChosenTimePatternIsWhatIsWritten()
    {
        Nls.Formats = new LocalizationOptions { Time = "HH:mm:ss" };
        // The clock reads the wall clock, so the test freezes it (Clock.Now) to the same moment.
        Clock.Now = () => At;

        Assert.Equal("14:40:07", Nls.TimeOfDay(At, seconds: true));
        Assert.Equal("14:40", Nls.TimeOfDay(At, seconds: false));
        Assert.Equal("14:40", Nls.Time(At));
        // The bar's clock asks the same question, so it cannot be an hour cycle apart.
        Assert.Equal("14:40:07", Clock.Read("", seconds: true).Time);
        Assert.Equal("14:40", Clock.Read("", seconds: false).Time);
    }

    [Fact]
    public void ATwelveHourPatternIsHonouredEverywhere()
    {
        Nls.Formats = new LocalizationOptions { Time = "h:mm:ss tt" };
        Clock.Now = () => At;

        var shown = Nls.TimeOfDay(At, seconds: false);
        Assert.NotNull(shown);
        Assert.StartsWith("2:40", shown, StringComparison.Ordinal);
        Assert.StartsWith("2:40", Clock.Read("", seconds: false).Time, StringComparison.Ordinal);
    }

    /// <summary>The first day of the week is a setting too, and the calendar reads it from here.</summary>
    [Fact]
    public void TheFirstDayOfTheWeekIsChosenOrFollowed()
    {
        Nls.Formats = new LocalizationOptions { FirstDayOfWeek = DayOfWeek.Saturday };
        Assert.Equal(DayOfWeek.Saturday, Nls.FirstDayOfWeek());
        // And the calendar's week begins there, since it asks Nls for the day.
        Assert.Equal(DayOfWeek.Saturday, CalendarNavigation.WeekStart(new DateOnly(2026, 9, 22), Nls.FirstDayOfWeek()).DayOfWeek);

        Nls.Formats = null;
        Assert.InRange((int)Nls.FirstDayOfWeek(), 0, 6);
    }

    /// <summary>
    /// The patterns offered are the region's own (what Windows' "Change data formats" page lists),
    /// not a list Karpo made up — and each of them formats a date rather than being decoration.
    /// </summary>
    [Fact]
    public void TheRegionsOwnPatternsAreWhatIsOffered()
    {
        var shortDates = Nls.DateFormats(@long: false);
        var longDates = Nls.DateFormats(@long: true);
        var times = Nls.TimeFormats();

        Assert.NotEmpty(shortDates);
        Assert.NotEmpty(longDates);
        Assert.NotEmpty(times);
        Assert.Equal(shortDates.Count, shortDates.Distinct(StringComparer.Ordinal).Count());
        foreach (var pattern in shortDates.Concat(longDates))
        {
            Nls.Formats = new LocalizationOptions { ShortDate = pattern };
            Assert.False(string.IsNullOrWhiteSpace(Nls.ShortDate(Day)), $"{pattern} formats nothing");
        }
        foreach (var pattern in times)
        {
            Nls.Formats = new LocalizationOptions { Time = pattern };
            Assert.False(string.IsNullOrWhiteSpace(Nls.TimeOfDay(At, seconds: true)), $"{pattern} formats nothing");
        }
    }

    /// <summary>
    /// The default for a temperature and for sizes is "follow Windows" too: while it follows, the
    /// region's measurement system is the answer; once set, the setting is, whatever the region
    /// says. The weather widget has no scale of its own to disagree with it (2026-10-05).
    /// </summary>
    [Fact]
    public void TheUnitsFollowUntilTheyAreSet()
    {
        var formats = new LocalizationOptions();

        Assert.Equal(TemperatureUnit.Follow, formats.Temperature);
        Assert.Equal(ByteUnit.Windows, formats.Sizes);
        // Following: the region's answer is the answer.
        Assert.True(formats.IsFahrenheit(regionSays: true));
        Assert.False(formats.IsFahrenheit(regionSays: false));

        // Set: it is the answer, whatever the region says.
        formats.Temperature = TemperatureUnit.Celsius;
        Assert.False(formats.IsFahrenheit(regionSays: true));
        formats.Temperature = TemperatureUnit.Fahrenheit;
        Assert.True(formats.IsFahrenheit(regionSays: false));

        Assert.False(formats.Follows);
        Assert.Equal(TemperatureUnit.Fahrenheit, formats.Clone().Temperature);
    }

    /// <summary>
    /// A widget's size follows the format it is set to — a long date needs more room than a short
    /// one, and that is a setting, not a state. What must not happen is a size that follows the
    /// value, which is why the shape measures a template: every day of the week gives one answer.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void ATemplateIsMeasuredUnderTheFormatThatIsSet()
    {
        Karpo.Tests.Islands.Offscreen.OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), Look);
            var fit = new Fit(style);

            Nls.Formats = new LocalizationOptions { ShortDate = "dd.MM.yy" };
            var shortest = DateText.Widest(gfx, fit.Primary);

            Nls.Formats = new LocalizationOptions { ShortDate = "dddd, d MMMM yyyy" };
            var longest = DateText.Widest(gfx, fit.Primary);

            Assert.True(longest > shortest, $"a long format measured {longest} px, no more than the short one's {shortest}");

            // And under one format, every day of the week measures the same: the template, not today.
            var again = DateText.Widest(gfx, fit.Primary);
            Assert.Equal(longest, again);
        });
    }

    static Look Look => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4));
}
