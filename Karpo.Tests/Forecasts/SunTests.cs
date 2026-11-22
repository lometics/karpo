// sun: the sun widget (docs\widgets.md §368) — sunrise, sunset, civil twilight and solar noon against the
// US Naval Observatory's tables (aa.usno.navy.mil, read 2026-10-26, to the minute, in UTC) for cities on
// both sides of the equator, on the days their clocks change, at the equator and inside the Arctic circle
// in polar day and polar night; the next event never one already past; the change against yesterday; the
// day bar always the clock's 24 hours (§657); the words, the daylight's note what is left of it and never a bare sign (§404);
// coordinates typed into the place search; and the file: the face's option, a sun line in each style and a
// time's line written and read back, the preset and its place, each time a widget of its own, the add list.

using Karpo.Dock;

namespace Karpo.Tests.Forecasts;

public sealed class SunTests
{
    private static DateTimeOffset Utc(int month, int day, int hour, int minute) => new(2026, month, day, hour, minute, 0, TimeSpan.Zero);

    /// <summary>Within a minute of the table's minute: what a table rounded to the minute can say.</summary>
    private static void Near(DateTimeOffset expected, DateTimeOffset? actual, string what)
    {
        Assert.True(actual is not null, $"{what}: none");
        var off = Math.Abs((actual!.Value - expected).TotalSeconds);
        Assert.True(off <= 60, $"{what}: {actual:u} is {off:0} s from the table's {expected:u}");
    }

    /// <summary>One USNO day: a place, a moment in its solar day, and the table's rise, set, civil dawn and dusk and transit (UTC).</summary>
    public sealed record Day(string Name, double Latitude, double Longitude, DateTimeOffset Moment, DateTimeOffset Rise, DateTimeOffset Set, DateTimeOffset Dawn, DateTimeOffset Dusk, DateTimeOffset Transit);

    public static TheoryData<Day> Days =>
    [
        // Zürich at midsummer, and on the day Europe's clocks go forward.
        new("Zürich, June 21", 47.3769, 8.5417, Utc(6, 21, 11, 28), Utc(6, 21, 3, 29), Utc(6, 21, 19, 26), Utc(6, 21, 2, 49), Utc(6, 21, 20, 7), Utc(6, 21, 11, 28)),
        new("Zürich, March 29", 47.3769, 8.5417, Utc(3, 29, 11, 31), Utc(3, 29, 5, 11), Utc(3, 29, 17, 51), Utc(3, 29, 4, 40), Utc(3, 29, 18, 22), Utc(3, 29, 11, 31)),
        // New York on the day America's go forward: its sunset is the next UTC day's early hours.
        new("New York, March 8", 40.7128, -74.0060, Utc(3, 8, 17, 7), Utc(3, 8, 11, 19), Utc(3, 8, 22, 55), Utc(3, 8, 10, 52), Utc(3, 8, 23, 23), Utc(3, 8, 17, 7)),
        // Quito, on the equator, at the equinox: twelve hours and a few minutes.
        new("Quito, September 23", -0.1807, -78.4678, Utc(9, 23, 17, 6), Utc(9, 23, 11, 3), Utc(9, 23, 23, 9), Utc(9, 23, 10, 42), Utc(9, 23, 23, 30), Utc(9, 23, 17, 6)),
        // Reykjavik at midwinter: four hours of sun.
        new("Reykjavik, December 21", 64.1466, -21.9426, Utc(12, 21, 13, 26), Utc(12, 21, 11, 22), Utc(12, 21, 15, 29), Utc(12, 21, 10, 3), Utc(12, 21, 16, 49), Utc(12, 21, 13, 26)),
        // Cape Town in the southern summer.
        new("Cape Town, January 15", -33.9249, 18.4241, Utc(1, 15, 10, 56), Utc(1, 15, 3, 51), Utc(1, 15, 18, 0), Utc(1, 15, 3, 22), Utc(1, 15, 18, 28), Utc(1, 15, 10, 56)),
    ];

    [Theory]
    [MemberData(nameof(Days))]
    public void SunriseSunsetTwilightAndNoonAreTheNavalObservatorysToTheMinute(Day day)
    {
        var times = SolarPosition.TimesFor(day.Moment, day.Latitude, day.Longitude);
        Assert.Equal(PolarState.None, times.Polar);
        Near(day.Rise, times.Sunrise, $"{day.Name} sunrise");
        Near(day.Set, times.Sunset, $"{day.Name} sunset");
        var twilight = SolarPosition.CivilTwilightFor(day.Moment, day.Latitude, day.Longitude);
        Near(day.Dawn, twilight.Sunrise, $"{day.Name} dawn");
        Near(day.Dusk, twilight.Sunset, $"{day.Name} dusk");
        Near(day.Transit, SolarPosition.NoonFor(day.Moment, day.Latitude, day.Longitude).At, $"{day.Name} noon");
        // Any moment of the same solar day answers the same day: its morning and its evening.
        Assert.Equal(times, SolarPosition.TimesFor(day.Moment.AddHours(-9), day.Latitude, day.Longitude));
        Assert.Equal(times, SolarPosition.TimesFor(day.Moment.AddHours(9), day.Latitude, day.Longitude));
    }

    [Fact]
    public void SydneysMorningIsItsOwnDayAndItsNextSunriseIsTomorrows()
    {
        // October 4, the day New South Wales' clocks go forward: sunset 08:00 UTC; the next sunrise, the
        // fifth's local morning, 19:27 UTC — never the sunrise of the day already past.
        var sydney = (Latitude: -33.8688, Longitude: 151.2093);
        var times = SolarPosition.TimesFor(Utc(10, 4, 1, 44), sydney.Latitude, sydney.Longitude);
        Near(Utc(10, 4, 8, 0), times.Sunset, "Sydney sunset");
        var next = SolarPosition.NextEvent(Utc(10, 4, 8, 30), sydney.Latitude, sydney.Longitude)!.Value;
        Assert.Equal(SunEventKind.Sunrise, next.Kind);
        Near(Utc(10, 4, 19, 27), next.At, "Sydney's next sunrise");
    }

    [Fact]
    public void TheNextEventIsAlwaysAheadAndTheOtherKindAfterIt()
    {
        var zurich = (Latitude: 47.3769, Longitude: 8.5417);
        for (var minutes = 0; minutes < 48 * 60; minutes += 17)
        {
            var now = Utc(10, 26, 0, 0).AddMinutes(minutes);
            var next = SolarPosition.NextEvent(now, zurich.Latitude, zurich.Longitude)!.Value;
            Assert.True(next.At > now);
            Assert.True(next.At - now < TimeSpan.FromHours(17), $"{now:u}: the next is {next.At:u}");
            var after = SolarPosition.NextEvent(next.At, zurich.Latitude, zurich.Longitude)!.Value;
            Assert.NotEqual(next.Kind, after.Kind);
        }
    }

    [Fact]
    public void InsideTheArcticCircleTheSunStaysUpOrDown()
    {
        var tromso = (Latitude: 69.6492, Longitude: 18.9553);
        // Midsummer: up all day, and never darker than civil twilight (the table: continuously above both).
        var summer = Utc(6, 21, 10, 46);
        Assert.Equal(new SunTimes(null, null, PolarState.Day), SolarPosition.TimesFor(summer, tromso.Latitude, tromso.Longitude));
        Assert.Equal(PolarState.Day, SolarPosition.CivilTwilightFor(summer, tromso.Latitude, tromso.Longitude).Polar);
        Assert.Null(SolarPosition.NextEvent(summer, tromso.Latitude, tromso.Longitude));
        Assert.Null(SolarPosition.DaylightChange(summer, tromso.Latitude, tromso.Longitude));
        Assert.True(SolarPosition.NoonFor(summer, tromso.Latitude, tromso.Longitude).AltitudeDegrees > 40);
        // Midwinter: down all day, but civil twilight at the table's 08:31 and 12:53.
        var winter = Utc(12, 21, 10, 42);
        Assert.Equal(PolarState.Night, SolarPosition.TimesFor(winter, tromso.Latitude, tromso.Longitude).Polar);
        var twilight = SolarPosition.CivilTwilightFor(winter, tromso.Latitude, tromso.Longitude);
        Near(Utc(12, 21, 8, 31), twilight.Sunrise, "Tromsø dawn");
        Near(Utc(12, 21, 12, 53), twilight.Sunset, "Tromsø dusk");
        Assert.True(SolarPosition.NoonFor(winter, tromso.Latitude, tromso.Longitude).AltitudeDegrees < 0);
        // And the sun at the North Pole in June: up, an exact zero cosine is no failure.
        Assert.Equal(PolarState.Day, SolarPosition.TimesFor(summer, 90, 0).Polar);
        Assert.Equal(PolarState.Night, SolarPosition.TimesFor(summer, -90, 0).Polar);
    }

    [Fact]
    public void TheDaysLengthenAfterTheWinterSolsticeAndShortenAfterTheSummersAndTheChangeIsSaid()
    {
        var zurich = (Latitude: 47.3769, Longitude: 8.5417);
        Assert.True(SolarPosition.DaylightChange(Utc(1, 15, 12, 0), zurich.Latitude, zurich.Longitude) > TimeSpan.Zero);
        Assert.True(SolarPosition.DaylightChange(Utc(10, 26, 12, 0), zurich.Latitude, zurich.Longitude) < TimeSpan.FromMinutes(-2));
        // §404: in words, never a bare sign (the person read "−2 min" as what was left of the day).
        Assert.Equal("2 min 14 s longer than yesterday", SunText.Change(TimeSpan.FromSeconds(134)));
        Assert.Equal("58 s shorter than yesterday", SunText.Change(TimeSpan.FromSeconds(-58)));
        Assert.Equal("1 h 23 min left", SunText.Left(TimeSpan.FromMinutes(82.5)));
        Assert.Equal("Same as yesterday", SunText.Change(TimeSpan.FromSeconds(0.4)));
        Assert.Equal("11 h 33 min", SunText.Daylight(new SunTimes(Utc(1, 1, 7, 0), Utc(1, 1, 18, 33), PolarState.None)));
        Assert.Equal("24 h", SunText.Daylight(new SunTimes(null, null, PolarState.Day)));
        Assert.Equal("0 h", SunText.Daylight(new SunTimes(null, null, PolarState.Night)));
        Assert.Equal("in 3 h 10 min", SunText.In(TimeSpan.FromMinutes(189.5)));
        Assert.Equal("in 1 min", SunText.In(TimeSpan.FromSeconds(-5)));
        Assert.Equal("Sunset in 12 min", SunText.NamedIn(SunEventKind.Sunset, TimeSpan.FromMinutes(12)));
        Assert.Equal("Sunrise 07:28", SunText.At(SunEventKind.Sunrise, "07:28"));
        Assert.Equal("34°", SunText.Degrees(33.6));
        Assert.Equal("0°", SunText.Degrees(-0.3));
        Assert.Equal("−3°", SunText.Degrees(-3.2));
        Assert.Equal("—", SunText.Time(null));
    }

    /// <summary>
    /// §404, the daylight's note (the person, 2026-10-29: "daylight should not show the - before the remaining time
    /// but should say 1h 23min left"): while the sun is up, how much of the day is left; while it is down, the change
    /// against yesterday in words; inside the polar circles, which it is. Never a bare sign.
    /// </summary>
    [Fact]
    public void TheDaylightSaysHowMuchIsLeftAndNeverABareMinus()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        SunDay At(DateTimeOffset now, double latitude = 47.3769, double longitude = 8.5417) => SunDay.Of(now, latitude, longitude, zone);

        var afternoon = At(Utc(10, 26, 14, 40));
        Assert.Equal(SunEventKind.Sunset, afternoon.Next!.Value.Kind);
        Assert.Equal(SunText.Left(afternoon.Next.Value.At - afternoon.Now), SunText.DaylightNote(afternoon));
        Assert.StartsWith("1 h ", SunText.DaylightNote(afternoon));
        Assert.EndsWith(" left", SunText.DaylightNote(afternoon));

        var night = At(Utc(10, 26, 21, 0));
        Assert.Equal(SunText.Change(night.Change!.Value), SunText.DaylightNote(night));
        Assert.EndsWith(" shorter than yesterday", SunText.DaylightNote(night));
        Assert.EndsWith(" longer than yesterday", SunText.DaylightNote(At(Utc(3, 2, 4, 0))));

        Assert.Equal(S.DockSunPolarDay, SunText.DaylightNote(At(Utc(6, 21, 10, 46), 69.6492, 18.9553)));
        Assert.Equal(S.DockSunPolarNight, SunText.DaylightNote(At(Utc(12, 21, 10, 42), 69.6492, 18.9553)));
        foreach (var day in new[] { afternoon, night, At(Utc(10, 26, 5, 0)), At(Utc(3, 2, 4, 0)), At(Utc(3, 2, 12, 0)) })
        {
            var note = SunText.DaylightNote(day) ?? "";
            Assert.NotEmpty(note);
            Assert.False(note.StartsWith('−') || note.StartsWith('-') || note.StartsWith('+'), $"{day.Now:u}: \"{note}\"");
        }
    }

    [Fact]
    public void TheArcRisesAtSunriseStandsHighestAtNoonAndSetsAtSunset()
    {
        var zurich = (Latitude: 47.3769, Longitude: 8.5417);
        var day = Utc(6, 21, 11, 28);
        var times = SolarPosition.TimesFor(day, zurich.Latitude, zurich.Longitude);
        var atRise = SkyPath.PositionOf(SolarPosition.Now(times.Sunrise!.Value.AddMinutes(10), zurich.Longitude), zurich.Latitude);
        var atNoon = SkyPath.PositionOf(SolarPosition.Now(SolarPosition.NoonFor(day, zurich.Latitude, zurich.Longitude).At, zurich.Longitude), zurich.Latitude);
        var atNight = SkyPath.PositionOf(SolarPosition.Now(Utc(6, 21, 23, 30), zurich.Longitude), zurich.Latitude);
        Assert.True(atRise.Up && atRise.Progress < 0.05, $"{atRise}");
        Assert.True(atNoon.Up && Math.Abs(atNoon.Progress - 0.5) < 0.01 && Math.Abs(atNoon.AltitudeDegrees - 66) < 1, $"{atNoon}");
        Assert.False(atNight.Up);
        // The path's ends are on the horizon, its middle the noon altitude.
        var samples = SkyPath.Samples(23.44, zurich.Latitude, below: false);
        Assert.InRange(samples[0].AltitudeDegrees, -0.01, 0.01);
        Assert.InRange(samples[^1].AltitudeDegrees, -0.01, 0.01);
        Assert.InRange(samples[24].AltitudeDegrees, 65.9, 66.1);
        Assert.Equal(180, SkyPath.SemiDiurnalArc(23.44, 70));
        Assert.Equal(0, SkyPath.SemiDiurnalArc(-23.44, 70));
    }

    /// <summary>One day bar to check: a place seen from Zürich's clock at an instant, and the instants its lit stretches run between (null: the bar's end).</summary>
    public sealed record BarDay(string Name, double Latitude, double Longitude, DateTimeOffset Now, (DateTimeOffset? From, DateTimeOffset? To)[] Lit);

    private static readonly (double Latitude, double Longitude) Zurich = (47.3769, 8.5417);
    private static readonly (double Latitude, double Longitude) NewYork = (40.7143, -74.006);
    private static readonly (double Latitude, double Longitude) Tokyo = (35.6762, 139.6503);
    private static readonly (double Latitude, double Longitude) Tromso = (69.6492, 18.9553);

    private static DateTimeOffset? Rise((double Latitude, double Longitude) at, DateTimeOffset moment) => SolarPosition.TimesFor(moment, at.Latitude, at.Longitude).Sunrise;

    private static DateTimeOffset? Set((double Latitude, double Longitude) at, DateTimeOffset moment) => SolarPosition.TimesFor(moment, at.Latitude, at.Longitude).Sunset;

    public static TheoryData<BarDay> BarDays =>
    [
        // An ordinary day: 14:00 here, the day's sunrise to its sunset.
        new("Zürich, Nov 10", Zurich.Latitude, Zurich.Longitude, Utc(11, 10, 13, 0), [(Rise(Zurich, Utc(11, 10, 12, 0)), Set(Zurich, Utc(11, 10, 12, 0)))]),
        // 00:10 here, before Zürich's solar day turns (00:26): still today's sunrise and sunset, never all of it lit.
        new("Zürich, Nov 10, 00:10", Zurich.Latitude, Zurich.Longitude, Utc(11, 9, 23, 10), [(Rise(Zurich, Utc(11, 10, 12, 0)), Set(Zurich, Utc(11, 10, 12, 0)))]),
        // The day the clocks go forward: 23 hours of instants, still the clock's 24 hours across.
        new("Zürich, Mar 29", Zurich.Latitude, Zurich.Longitude, Utc(3, 29, 11, 30), [(Rise(Zurich, Utc(3, 29, 12, 0)), Set(Zurich, Utc(3, 29, 12, 0)))]),
        // And the day they go back: 25 hours of instants.
        new("Zürich, Oct 25", Zurich.Latitude, Zurich.Longitude, Utc(10, 25, 12, 0), [(Rise(Zurich, Utc(10, 25, 12, 0)), Set(Zurich, Utc(10, 25, 12, 0)))]),
        // New York at 03:00 here: its solar day is still yesterday's, its daylight this afternoon and evening here.
        new("New York, Nov 10, 03:00 here", NewYork.Latitude, NewYork.Longitude, Utc(11, 10, 2, 0), [(Rise(NewYork, Utc(11, 10, 17, 0)), Set(NewYork, Utc(11, 10, 17, 0)))]),
        // Tokyo at noon here: its daylight across this PC's midnight, the morning's end and the evening's start.
        new("Tokyo, Nov 10, 12:00 here", Tokyo.Latitude, Tokyo.Longitude, Utc(11, 10, 11, 0), [(null, Set(Tokyo, Utc(11, 10, 3, 0))), (Rise(Tokyo, Utc(11, 11, 3, 0)), null)]),
        // Polar summer and winter: all lit, none.
        new("Tromsø, midsummer", Tromso.Latitude, Tromso.Longitude, Utc(6, 21, 10, 46), [(null, null)]),
        new("Tromsø, midwinter", Tromso.Latitude, Tromso.Longitude, Utc(12, 21, 10, 42), []),
    ];

    /// <summary>
    /// §657 (the person, 2026-11-10: "sunrise sunset bar should always show 24 h (one full day) but does not do so"):
    /// the day bar is the clock's 24 hours of the day holding now, 00:00 at its start, noon in its middle — on a 23- or
    /// 25-hour day too — and lit wherever the sun is up in them, whichever of the place's solar days that daylight
    /// belongs to: never the whole bar because the place's solar day has not turned, never half a wrapped day.
    /// </summary>
    [Theory]
    [MemberData(nameof(BarDays))]
    public void TheDayBarIsAlwaysTheClocksTwentyFourHours(BarDay day)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        double Across(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, zone).TimeOfDay.TotalHours / 24;
        var plan = SunDayBar.Of(day.Now, day.Latitude, day.Longitude, zone);

        // 24 hours of the clock, whatever the day's length in instants.
        Assert.Equal(Across(day.Now), plan.Now, 6);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(day.Now, zone).DateTime);
        Assert.Equal(0.5, SunDayBar.Share(new DateTimeOffset(today.ToDateTime(new TimeOnly(12, 0)), zone.GetUtcOffset(today.ToDateTime(new TimeOnly(12, 0)))), today, zone)!.Value, 6);

        Assert.Equal(day.Lit.Length, plan.Lit.Count);
        for (var index = 0; index < day.Lit.Length; index++)
        {
            var (from, to) = day.Lit[index];
            Assert.Equal(from is { } f ? Across(f) : 0, plan.Lit[index].From, 6);
            Assert.Equal(to is { } t ? Across(t) : 1, plan.Lit[index].To, 6);
        }
        // A tick at every sunrise and sunset on it, and none other; now lit where it is in a lit stretch.
        Assert.Equal(day.Lit.Sum(lit => (lit.From is null ? 0 : 1) + (lit.To is null ? 0 : 1)), plan.Marks.Count);
        Assert.Equal(day.Lit.Length > 0 && plan.Lit.Any(lit => plan.Now >= lit.From && plan.Now <= lit.To), plan.NowLit);
    }

    [Fact]
    public void TheDayBarsDaylightIsTheDaysLengthOfTwentyFourHours()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        // Tokyo's November daylight, about 10 h 28 min, cut in two by this PC's midnight, is still that share of 24 hours.
        var plan = SunDayBar.Of(Utc(11, 10, 11, 0), Tokyo.Latitude, Tokyo.Longitude, zone);
        var lit = plan.Lit.Sum(stretch => stretch.To - stretch.From) * 24;
        Assert.InRange(lit, 10.3, 10.6);
        Assert.Equal(SunEventKind.Sunset, plan.Marks[0].Kind);
        Assert.Equal(SunEventKind.Sunrise, plan.Marks[1].Kind);
        Assert.Equal(plan.Marks[1], plan.First(SunEventKind.Sunrise));
        Assert.False(plan.NowLit);
    }

    // --- The place ---------------------------------------------------------------------------------

    [Theory]
    [InlineData("47.37, 8.54", 47.37, 8.54, true)]
    [InlineData("47.37 8.54", 47.37, 8.54, true)]
    [InlineData("-33.87; 151.21", -33.87, 151.21, true)]
    [InlineData("69.65° 18.96°", 69.65, 18.96, true)]
    [InlineData("−0.18, −78.47", -0.18, -78.47, true)]
    [InlineData("95, 8", 95, 8, false)]
    [InlineData("47, 200", 47, 200, false)]
    public void CoordinatesTypedAreAPlaceOnlyOnTheEarth(string typed, double latitude, double longitude, bool inRange) =>
        Assert.Equal((latitude, longitude, inRange), PlaceQuery.Coordinates(typed)!.Value);

    [Theory]
    [InlineData("Zürich")]
    [InlineData("47.37")]
    [InlineData("New York 10")]
    [InlineData("1, 2, 3")]
    public void WordsAreNoCoordinates(string typed) => Assert.Null(PlaceQuery.Coordinates(typed));

    [Fact]
    public async Task ThePlaceSearchTakesCoordinatesAndRefusesThemOffTheEarth()
    {
        var line = ListKind.SunArc.New();
        var row = OptionRows.Of(line).Single(each => each.Key == OptionFields.WeatherPlace);
        var found = Assert.Single(await row.Finder!.FindAsync("47.3769, 8.5417", CancellationToken.None));
        Assert.Equal("47.377, 8.542", found.Title);
        Assert.True(row.Pick!(found));
        Assert.Equal(("", 47.3769, 8.5417, false), (line.Weather!.Place, line.Weather.Latitude, line.Weather.Longitude, line.Weather.UseDeviceLocation));
        // A place known by its coordinates is called by them, not "Current location".
        Assert.Equal("47.377, 8.542", PlaceNames.Of(line.Weather));
        Assert.Empty(await row.Finder.FindAsync("91, 8", CancellationToken.None));
        Assert.Equal(S.SettingsWidgetWeatherPlaceOutOfRange, row.Finder.NoneFound("91, 8"));
    }

    // --- The menu and the file ---------------------------------------------------------------------

    /// <summary>
    /// §404: the sun widget is its picture, the arc or the day bar; its times are widgets of their own, so no style
    /// is the times and no switch shows rows of them under the picture.
    /// </summary>
    [Fact]
    public void TheSunWidgetIsItsPictureInTwoStyles()
    {
        var arc = ListKind.SunArc.New();
        // Its place is the weather's: this PC's location, then the search.
        Assert.Contains(OptionRows.Of(arc), row => row.Key == OptionRows.WeatherUsesDevice);
        Assert.DoesNotContain(OptionRows.Of(arc), row => row.Key == "SunEvents");
        Assert.Equal(["Arc", "Day bar"], WidgetStyles.LookNames(arc));
        Assert.True(WidgetStyles.ChooseLook(arc, 1));
        Assert.Equal(ListKind.SunDayBar, arc.List);
        Assert.Equal(["arc", "dayBar"], WidgetStyles.Sun.Styles.Select(style => style.Word));
    }

    [Fact]
    public void TheFacesShowAndPlaceAndSunLinesAreWrittenAndReadBack()
    {
        var face = SunFace.New();
        Assert.True(face.Weather!.UseDeviceLocation);
        Assert.Equal(SunFace.Next, SunFace.ShowOf(face));

        var island = BarKind.FolderList.New();
        island.Id = "sun";
        island.Folder!.Face = face;
        face.Set(FolderFaces.Sun.Option(SunFace.Show)!, SunFace.Countdown);
        face.Weather = new WeatherOptions { Place = "Tromsø, Norway", Latitude = 69.6492, Longitude = 18.9553 };
        var arc = ListKind.SunArc.New();
        arc.Id = "arc";
        var bar = ListKind.SunDayBar.New();
        bar.Id = "bar";
        // §404: a time a widget of its own, its word its kind's, its place the weather's.
        var daylight = ListKind.Daylight.New();
        daylight.Id = "daylight";
        daylight.Weather = new WeatherOptions { Place = "Zürich", Latitude = 47.3769, Longitude = 8.5417 };
        island.Folder.Put(arc);
        island.Folder.Put(bar);
        island.Folder.Put(daylight);
        island.Normalize();
        var config = Karpo.Tests.Core.Configs.With(island);
        var json = config.ToJson();
        Assert.Contains("\"kind\": \"sun\"", json);
        Assert.Contains("\"style\": \"dayBar\"", json);
        Assert.Contains("\"kind\": \"daylight\"", json);
        Assert.DoesNotContain("sunEvents", json);
        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var read = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "sun")!;
        Assert.Equal(SunFace.Countdown, SunFace.ShowOf(read.Face));
        Assert.Equal("Tromsø, Norway", SunFace.PlaceOfFace(read)!.Place);
        Assert.Equal([ListKind.SunArc, ListKind.SunDayBar, ListKind.Daylight], read.Cells.Select(cell => cell.Island.List));
        Assert.Equal("Zürich", read.Cells[2].Island.Weather!.Place);

        // A word from a newer build is kept and read as the default.
        face.Set(FolderFaces.Sun.Option(SunFace.Show)!, "moon");
        Assert.Equal(SunFace.Next, SunFace.ShowOf(face));
    }

    /// <summary>
    /// §404: the ready-made folder is the face over the arc and the four times the times style started with —
    /// sunrise, sunset, the daylight and solar noon, each a widget of its own —; the first place chosen is
    /// given to the face and every line.
    /// </summary>
    [Fact]
    public void ThePresetIsTheFaceOverTheArcAndTheTimesAndItsFirstPlaceIsGivenToAll()
    {
        var preset = Presets.Find(Presets.SunId)!;
        var folder = Presets.Folder(preset, new PresetMachine(false, [])).Folder!;
        Assert.Equal(FolderFaces.SunId, folder.Face.Kind);
        Assert.Equal([ListKind.SunArc, ListKind.Sunrise, ListKind.Sunset, ListKind.Daylight, ListKind.SolarNoon], folder.Cells.Select(cell => cell.Island.List));

        folder.Face.Weather = new WeatherOptions();
        foreach (var cell in folder.Cells)
        {
            cell.Island.Weather = new WeatherOptions();
        }
        var chosen = new WeatherOptions { Place = "Quito, Ecuador", Latitude = -0.18, Longitude = -78.47 };
        Assert.Equal(6, WeatherPlaces.Spread(folder, chosen));
        Assert.Equal("Quito, Ecuador", folder.Face.Weather.Place);
        Assert.All(folder.Cells, cell => Assert.Equal("Quito, Ecuador", cell.Island.Weather!.Place));
    }

    /// <summary>The times by their names: what the add list, a line's menu and its tooltip call each.</summary>
    public static TheoryData<ListKind, string> Times => new()
    {
        { ListKind.Sunrise, "sunrise" },
        { ListKind.Sunset, "sunset" },
        { ListKind.Daylight, "daylight" },
        { ListKind.SolarNoon, "solarNoon" },
        { ListKind.Dawn, "dawn" },
        { ListKind.Dusk, "dusk" },
    };

    /// <summary>
    /// §404 (the person, 2026-10-29: "the daylight, sunset, solar noon, sunrise, etc. metrics should actually be
    /// individual widgets"): each time a widget of its own — a line of a place, the weather's options, drawn by
    /// the sun's line, automatic and one row where it starts, no style to choose.
    /// </summary>
    [Theory]
    [MemberData(nameof(Times))]
    public void EachTimeIsAWidgetOfItsOwn(ListKind kind, string id)
    {
        var line = ListWidgets.Of(kind);
        Assert.Equal(id, line.Info.Id);
        Assert.Same(line.Info, WidgetInfos.Find(id));
        Assert.Equal(WidgetCategory.Weather, line.Info.Category);
        Assert.Equal(WidgetOptionsKind.Weather, line.Info.Options);
        Assert.Equal(WidgetSource.Sun, line.Drawing);
        // §432: its row (Compact) or with its note (Full, where it starts); dawn and dusk, which have no note, the row alone.
        Assert.Equal(kind is ListKind.Dawn or ListKind.Dusk ? new[] { LineSize.Full } : new[] { LineSize.Compact, LineSize.Full }, kind.Offered());
        Assert.Equal(kind is ListKind.Dawn or ListKind.Dusk ? 2 : 3, line.Cells);
        Assert.Equal((kind is ListKind.Dawn or ListKind.Dusk ? null : (LineSize?)LineSize.Full, (LineHeight?)null), (kind.New().Size, kind.New().Height));
        Assert.Null(WidgetStyles.Of(kind));
        Assert.True(kind.New().Weather!.UseDeviceLocation);
        Assert.NotEqual($"core.widget.{id}.title", line.Info.Title);
    }

    [Fact]
    public void TheSunIsAFolderOfTheWeatherPageAfterTheEarthquakes()
    {
        Assert.Equal(WidgetCategory.Weather, WidgetInfos.SunArc.Category);
        Assert.Equal(WidgetOptionsKind.Weather, WidgetInfos.SunArc.Options);
        var weather = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "weather");
        // §490: no subject deeper than its own level — the sun's folder is a row of Weather and news, right after the
        // earthquakes, not a shelf of its own.
        var leaves = weather.Rows.OfType<AddLeaf>().ToList();
        Assert.Empty(weather.Rows.OfType<AddShelf>());
        Assert.Equal(leaves.FindIndex(leaf => leaf.Id == "quakes") + 1, leaves.FindIndex(leaf => leaf.Id == Presets.SunId));
        Assert.Contains(FolderFaces.Sun, FolderFaces.All);
        Assert.Same(WidgetStyles.Sun, WidgetStyles.Of(ListKind.SunDayBar)!.Value.Widget);
        Assert.True(FolderFaces.HasPlace(FolderFaces.SunId));
    }
}
