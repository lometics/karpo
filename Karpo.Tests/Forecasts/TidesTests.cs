// tides: the tides (docs\widgets.md §236) — Open-Meteo's marine answer read, the highs and lows found
// in it through ripples, flat tops, a series' ends, gaps and the day the clocks change, the next turn
// and the way the sea goes, heights in metres and feet, the reading's states, the store's one fetch
// per place, and the file: the face's option and a tides line written and read back, a word from a
// newer build read as the default, and a file from before read as it was.

using Karpo.Dock;
using Karpo.Dock.Weather;

namespace Karpo.Tests.Forecasts;

public sealed class TidesTests
{
    private static readonly DateTime Midnight = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);

    /// <summary><paramref name="count"/> quarters from <see cref="Midnight"/> of <paramref name="height"/>.</summary>
    private static List<TideSample> Series(Func<int, double?> height, int count) =>
        [.. Enumerable.Range(0, count).Select(index => new TideSample(Midnight.AddMinutes(15 * index), height(index)))];

    /// <summary>A pure semidiurnal sea, 2 m either side, its first high <paramref name="first"/> after midnight.</summary>
    private static double Sine(DateTime utc, TimeSpan first) =>
        2 * Math.Cos(2 * Math.PI * ((utc - Midnight - first).TotalMinutes / SavedTides.Period.TotalMinutes));

    // --- The answer --------------------------------------------------------------------------------

    [Fact]
    public void AnAnswerIsReadInInstantsAndTheQuartersAreTakenFirst()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var answer = TideAnswers.Parse(SavedTides.Answer(at => SavedTides.Wave(at), SavedTides.Start, 4 * 96));
            Assert.Equal(7200, answer.UtcOffsetSeconds);
            Assert.Equal(384, answer.Quarters!.Time!.Length);
            Assert.Equal(96, answer.Hourly!.Time!.Length);
            var samples = TideAnswers.Samples(answer);
            Assert.Equal(384, samples.Count);
            Assert.Equal(SavedTides.Start, samples[0].Utc);
            Assert.Equal(DateTimeKind.Utc, samples[0].Utc.Kind);

            // Without the quarters, the hours.
            var hourly = TideAnswers.Parse(SavedTides.Answer(at => SavedTides.Wave(at), SavedTides.Start, 4 * 96, quarters: false));
            Assert.Equal(96, TideAnswers.Samples(hourly).Count);

            // A place off the sea: an answer, every value null, no samples.
            var inland = TideAnswers.Parse(SavedTides.Answer(_ => null, SavedTides.Start, 96));
            Assert.Empty(TideAnswers.Samples(inland));
        });
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{ "latitude": 1, "hourly": { "time": [] } }""")]
    public void AnAnswerWithoutASeriesIsNoAnswer(string json) =>
        Assert.Throws<InvalidDataException>(() => TideAnswers.Parse(json));

    [Fact]
    public void TheRequestAsksForTheSeaLevelInInstantsAtAKilometre()
    {
        var url = TideAnswers.Url(48.38456, -4.49123);
        Assert.StartsWith("https://marine-api.open-meteo.com/v1/marine?latitude=48.38&longitude=-4.49", url);
        Assert.Contains("minutely_15=sea_level_height_msl", url);
        Assert.Contains("hourly=sea_level_height_msl", url);
        Assert.Contains("timeformat=unixtime", url);
        Assert.Contains("past_days=1", url);
        Assert.Contains("forecast_days=3", url);
    }

    // --- The highs and lows ------------------------------------------------------------------------

    [Fact]
    public void ASeasTurnsAreFoundToTheMinuteAndTheCentimetre()
    {
        var first = TimeSpan.FromMinutes(200);
        var samples = Series(index => Sine(Midnight.AddMinutes(15 * index), first), 3 * 96);
        var turns = TideTurns.Find(samples);
        // Three days of a 12 h 25 min sea from a high at 03:20: highs and lows by turns, the first at the
        // series' start not one, nor the last top before its end.
        Assert.True(turns.Count >= 10);
        for (var index = 0; index < turns.Count; index++)
        {
            var turn = turns[index];
            Assert.Equal(index % 2 == 0, turn.High);
            var truth = Midnight + first + (SavedTides.Period * (index / 2.0));
            Assert.True(Math.Abs((turn.Utc - truth).TotalMinutes) < 2, $"turn {index}: {turn.Utc:HH:mm} for {truth:HH:mm}");
            Assert.Equal(turn.High ? 2 : -2, turn.Height, 0.02);
        }
    }

    [Fact]
    public void RipplesAndASmallSecondBumpAreNoTurns()
    {
        var first = TimeSpan.FromMinutes(200);
        var clean = TideTurns.Find(Series(index => Sine(Midnight.AddMinutes(15 * index), first), 2 * 96));
        // A ripple of 2 cm every 45 minutes, and the model's rounding to the centimetre.
        var noisy = TideTurns.Find(Series(index => Math.Round(Sine(Midnight.AddMinutes(15 * index), first) + (0.02 * Math.Sin(index * 2 * Math.PI / 3)), 2), 2 * 96));
        Assert.Equal(clean.Select(turn => turn.High), noisy.Select(turn => turn.High));
        Assert.All(clean.Zip(noisy), pair => Assert.True(Math.Abs((pair.First.Utc - pair.Second.Utc).TotalMinutes) <= 30));

        // An estuary's double low: the sea rises 7 cm after its low and falls again, as Hoek van
        // Holland's model does — one low, not a low, a high and a low.
        double?[] estuary = [0.22, 0.19, 0.13, 0.05, -0.01, -0.06, -0.10, -0.13, -0.15, -0.14, -0.11, -0.08, -0.08, -0.11, -0.15, -0.21, -0.40, -0.70, -1.00, -1.20, -1.30, -1.20, -0.90, -0.40, 0.10, 0.30, 0.20];
        var turns = TideTurns.Find(Series(index => estuary[index], estuary.Length));
        Assert.Single(turns);
        Assert.False(turns[0].High);
        Assert.Equal(-1.30, turns[0].Height, 0.02);
    }

    [Fact]
    public void AFlatTopIsItsMiddleAndASingleTopIsPlacedBetweenItsNeighbours()
    {
        // A low, a flat high from 02:00 to 03:00, a low.
        double?[] flat = [-1, -0.5, 0, 0.5, 1, 1.4, 1.6, 1.8, 2, 2, 2, 2, 2, 1.8, 1.6, 1.2, 0.8, 0.2, -0.4, -1, -0.6];
        var high = Assert.Single(TideTurns.Find(Series(index => flat[index], flat.Length)), turn => turn.High);
        Assert.Equal(Midnight.AddMinutes(150), high.Utc);
        Assert.Equal(2, high.Height);

        // A top between a lower before and a higher after leans towards the higher: the parabola's top.
        double?[] lean = [-1, 0, 1, 1.8, 2.0, 1.9, 1.2, 0.2, -0.6, -1.2];
        var leaning = Assert.Single(TideTurns.Find(Series(index => lean[index], lean.Length)));
        Assert.True(leaning.Utc > Midnight.AddMinutes(60) && leaning.Utc < Midnight.AddMinutes(67.5), $"{leaning.Utc:HH:mm:ss}");
        Assert.True(leaning.Height > 2.0 && leaning.Height < 2.05);
    }

    [Fact]
    public void NothingAtASeriesEndsOrBesideAGapIsATurn()
    {
        // Falling from the first sample, rising into the last: the ends are not turns.
        double?[] ends = [2, 1.5, 1, 0, -1, -1.5, -2, -1.5, -1, 0, 1, 1.5, 2];
        var turns = TideTurns.Find(Series(index => ends[index], ends.Length));
        var low = Assert.Single(turns);
        Assert.False(low.High);
        Assert.Equal(Midnight.AddMinutes(90), low.Utc);

        // A gap of nulls: a top just before it is not a turn, a whole turn after it is.
        double?[] gapped = [-2, -1, 0, 1, 2, null, null, null, 1, 0, -1, -2, -1, 0, 1];
        var after = Assert.Single(TideTurns.Find(Series(index => gapped[index], gapped.Length)));
        Assert.False(after.High);
        Assert.Equal(Midnight.AddMinutes(165), after.Utc);
        // The height is not drawn across the gap.
        Assert.Null(TideTurns.HeightAt(Series(index => gapped[index], gapped.Length), Midnight.AddMinutes(80)));

        Assert.Empty(TideTurns.Find([]));
        Assert.Empty(TideTurns.Find(Series(_ => null, 20)));
        Assert.Empty(TideTurns.Find(Series(_ => 0.4, 20)));
    }

    [Fact]
    public void TheDayTheClocksChangeKeepsEveryTurnsHour()
    {
        // 25 October 2026, Central Europe goes back from UTC+2 to UTC+1 at 01:00 UTC. The answer was
        // made the evening before, at UTC+2; its instants are the sea's either way.
        var zone = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var fetched = new DateTime(2026, 10, 24, 18, 0, 0, DateTimeKind.Utc);
        var before = new DateTime(2026, 10, 24, 22, 0, 0, DateTimeKind.Utc);
        var after = new DateTime(2026, 10, 25, 14, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2026, 10, 25, 0, 0, 0), TideClock.Wall(before, 7200, fetched, zone));
        Assert.Equal(new DateTime(2026, 10, 25, 15, 0, 0), TideClock.Wall(after, 7200, fetched, zone));
        // A place on another clock than this PC's keeps the answer's own offset.
        Assert.Equal(new DateTime(2026, 10, 25, 10, 0, 0), TideClock.Wall(after, -14400, fetched, zone));

        // A series across the change: its quarters run on evenly in instants, and the turns are where the sea turns.
        var start = new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
        var first = new DateTime(2026, 10, 25, 0, 40, 0, DateTimeKind.Utc);
        var samples = Enumerable.Range(0, 144).Select(index => start.AddMinutes(15 * index))
            .Select(at => new TideSample(at, 2 * Math.Cos(2 * Math.PI * (at - first).TotalMinutes / SavedTides.Period.TotalMinutes))).ToList();
        var highs = TideTurns.Find(samples).Where(turn => turn.High && turn.Utc > start.AddHours(6)).ToList();
        Assert.True(Math.Abs((highs[0].Utc - first).TotalMinutes) < 2);
        // 00:40 UTC is still summer time, 02:40; the next, 13:05 UTC, is winter time, 14:05.
        Assert.True(Math.Abs((TideClock.Wall(highs[0].Utc, 7200, fetched, zone) - new DateTime(2026, 10, 25, 2, 40, 0)).TotalMinutes) < 2);
        Assert.True(Math.Abs((TideClock.Wall(highs[1].Utc, 7200, fetched, zone) - new DateTime(2026, 10, 25, 14, 5, 0)).TotalMinutes) < 2);
    }

    [Fact]
    public void TheNextTurnIsTheFirstAfterNowAndSaysWhichWayTheSeaGoes()
    {
        var first = TimeSpan.FromMinutes(200);
        var samples = Series(index => Sine(Midnight.AddMinutes(15 * index), first), 2 * 96);
        var turns = TideTurns.Find(samples);
        var high = turns.First(turn => turn.High);
        // Before the high, it is next and the sea rises; at it, the low after it is next and the sea falls.
        Assert.Equal(high, TideTurns.Next(turns, high.Utc.AddMinutes(-1)));
        Assert.True(TideTurns.Rising(samples, turns, high.Utc.AddMinutes(-1)));
        var low = TideTurns.Next(turns, high.Utc)!.Value;
        Assert.False(low.High);
        Assert.False(TideTurns.Rising(samples, turns, high.Utc));
        // Past the last turn, the way the series moves.
        var last = turns[^1];
        Assert.Null(TideTurns.Next(turns, last.Utc));
        Assert.Equal(!last.High, TideTurns.Rising(samples, turns, last.Utc.AddHours(1)));
        // Between two quarters, the height straight between them.
        Assert.Equal((samples[4].Height!.Value + samples[5].Height!.Value) / 2, TideTurns.HeightAt(samples, Midnight.AddMinutes(67.5))!.Value, 6);
        Assert.Null(TideTurns.HeightAt(samples, Midnight.AddDays(-1)));
    }

    // --- The words ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(2.06, false, ".", "2.1 m")]
    [InlineData(2.1, true, ".", "6.9 ft")]
    [InlineData(-1.43, false, ",", "-1,4 m")]
    [InlineData(-0.04, false, ".", "0.0 m")]
    [InlineData(-0.01, true, ".", "0.0 ft")]
    [InlineData(12.35, false, ".", "12.4 m")]
    public void AHeightIsInMetresOrFeetToADecimal(double metres, bool imperial, string separator, string said) =>
        Assert.Equal(said, TideText.Height(metres, imperial, separator));

    [Fact]
    public void AReadingSaysTheNextTurnHowSoonAndTheHeightNow()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var now = Clock.Now().ToUniversalTime();
            var reading = TideReading.From(SavedTides.Cache(), now, null, imperial: false);
            Assert.Equal(TideState.Ready, reading.State);
            var next = reading.Next!.Value;
            Assert.True(next.Turn.High);
            Assert.True(Math.Abs((next.Turn.Utc - SavedTides.HighUtc).TotalMinutes) < 2);
            Assert.True(reading.Rising);
            Assert.Equal(TideReading.MarksAhead, reading.Ahead.Count);
            Assert.Equal([true, false, true, false], reading.Ahead.Select(mark => mark.Turn.High));
            Assert.Equal(SavedTides.Wave(now), reading.Height!.Value, 0.02);
            Assert.StartsWith("High ", reading.NextAt);
            Assert.EndsWith("2.4 m", reading.NextAtHeight("."));
            // 12:20 to 15:32, a part minute up: "in 3 h 12 min" or "3 h 13 min" by the parabola's seconds.
            Assert.Matches("^High in 3 h 1[23] min$", reading.NextIn);
            // The curve runs from three hours ago to 21 hours on, a sample either side.
            Assert.True(reading.Curve[0].Utc <= now.AddHours(-3) && reading.Curve[^1].Utc >= now.AddHours(21));
            Assert.All(reading.CurveMarks, mark => Assert.InRange(mark.Turn.Utc, now.AddHours(-3), now.AddHours(21)));

            var tip = reading.Tooltip("Brest, France");
            Assert.StartsWith("Brest, France\n", tip);
            Assert.Contains("Rising", tip);
            Assert.EndsWith("1 h ago", tip); // §388: one meta wording
        });
    }

    [Fact]
    public void APlaceOffTheSeaAFailureAndNothingYetAreSaidPlainly()
    {
        SavedWeather.AtAFixedTime(() =>
        {
            var now = Clock.Now().ToUniversalTime();
            var inland = TideReading.From(SavedTides.Cache(inland: true), now, null);
            Assert.Equal(TideState.Inland, inland.State);
            Assert.Equal("No tide data here, pick a coastal place", inland.Why);

            var waiting = TideReading.From(null, now, null);
            Assert.Equal(TideState.Waiting, waiting.State);
            Assert.False(waiting.Stale);
            Assert.Equal("Getting the tides…", waiting.Why);
            Assert.Equal(TideState.Failed, TideReading.From(null, now, "Open-Meteo didn't answer").State);

            // A failure with an answer standing: the answer, stale, and why; and stale too after three intervals.
            var failed = TideReading.From(SavedTides.Cache(), now, "Open-Meteo can't be reached");
            Assert.Equal(TideState.Ready, failed.State);
            Assert.True(failed.Stale);
            Assert.False(TideReading.From(SavedTides.Cache(fetchedUtc: now.AddHours(-6)), now, null).Stale);
            Assert.True(TideReading.From(SavedTides.Cache(fetchedUtc: now.AddHours(-18)), now, null).Stale);

            Assert.Equal("The tides couldn't be read", TideStore.Explain(new InvalidDataException()));
            Assert.Equal(WeatherStore.Explain(new HttpRequestException()), TideStore.Explain(new HttpRequestException()));
        });
    }

    // --- The store ---------------------------------------------------------------------------------

    [Fact]
    public async Task OnePlaceIsOneFetchAndAFailureKeepsTheLastAnswer()
    {
        var asked = 0;
        var fail = false;
        var store = new TideStore((_, _) =>
        {
            Interlocked.Increment(ref asked);
            return fail ? Task.FromException<TideCache>(new HttpRequestException()) : Task.FromResult(SavedTides.Cache());
        });
        object face = new(), line = new();
        store.Use(face, SavedTides.Options());
        store.Use(line, new WeatherOptions { Place = "Brest", Latitude = 48.38, Longitude = -4.49 });
        store.Poll(face);
        store.Poll(line);
        await Until(() => store.Read(line) is { Shown: not null, Fetching: false });
        Assert.Equal(1, asked);
        Assert.Same(store.Read(face).Shown, store.Read(line).Shown);
        Assert.Equal(TimeSpan.FromHours(6), store.Every);

        fail = true;
        Assert.True(store.Ask(line));
        await Until(() => store.Read(line).Problem is not null);
        Assert.NotNull(store.Read(line).Shown);
        Assert.Equal(2, asked);

        var nowhere = new object();
        store.Use(nowhere, new WeatherOptions());
        store.Poll(nowhere);
        Assert.Equal(2, store.Started);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{ "Places": [ { "FetchedUtc": "2026-10-19T06:00:00Z" } ] }""")]
    [InlineData("""{ "Places": null }""")]
    public void ATidesFileThatCannotBeReadHoldsNothing(string json) =>
        Assert.Empty(TideStore.Read(json));

    private static async Task Until(Func<bool> done)
    {
        for (var tries = 0; tries < 200 && !done(); tries++)
        {
            await Task.Delay(10);
        }
        Assert.True(done());
    }

    // --- The file ----------------------------------------------------------------------------------

    [Fact]
    public void TheFacesShowAndPlaceAndATidesLineAreWrittenAndReadBack()
    {
        var face = TidesFace.New();
        Assert.True(face.Weather!.UseDeviceLocation);
        Assert.Equal(TidesFace.Next, TidesFace.ShowOf(face));
        Assert.Empty(face.Options);

        var island = BarKind.FolderList.New();
        island.Id = "tides";
        island.Folder!.Face = face;
        face.Set(FolderFaces.Tides.Option(TidesFace.Show)!, TidesFace.Countdown);
        face.Weather = new WeatherOptions { Place = "Brest", Latitude = 48.38, Longitude = -4.49 };
        var line = ListKind.Tides.New();
        line.Id = "line";
        Assert.True(line.Weather!.UseDeviceLocation);
        island.Folder.Put(line);
        island.Normalize();
        var config = Karpo.Tests.Core.Configs.With(island);
        Assert.Contains("\"kind\": \"tides\"", config.ToJson());
        var back = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var read = Karpo.Core.Folders.FolderIn(Karpo.Tests.Core.Configs.Islands(back), "tides")!;
        Assert.Equal(TidesFace.Countdown, TidesFace.ShowOf(read.Face));
        Assert.Equal("Brest", TidesFace.PlaceOfFace(read)!.Place);
        Assert.Null(WeatherPlaces.OfFace(read));
        Assert.Equal(ListKind.Tides, Assert.Single(read.Cells).Island.List);

        // A word from a newer build is kept and read as the default.
        face.Set(FolderFaces.Tides.Option(TidesFace.Show)!, "moon");
        Assert.Equal(TidesFace.Next, TidesFace.ShowOf(face));
    }

    [Fact]
    public void ThePresetIsTheFaceOverTheLineAndItsFirstPlaceIsGivenToBoth()
    {
        var preset = Presets.Find(Presets.TidesId)!;
        var folder = Presets.Folder(preset, new PresetMachine(false, [])).Folder!;
        Assert.Equal(FolderFaces.TidesId, folder.Face.Kind);
        // §603: the tides now over the tide graph.
        Assert.Equal([ListKind.Tides, ListKind.TideGraph], folder.Cells.Select(cell => cell.Island.List));

        folder.Face.Weather = new WeatherOptions();
        foreach (var cell in folder.Cells)
        {
            cell.Island.Weather = new WeatherOptions();
        }
        var chosen = new WeatherOptions { Place = "Brest", Latitude = 48.38, Longitude = -4.49 };
        Assert.Equal(3, WeatherPlaces.Spread(folder, chosen));
        Assert.Equal("Brest", folder.Face.Weather.Place);
        Assert.All(folder.Cells, cell => Assert.Equal("Brest", cell.Island.Weather!.Place));
    }

    [Fact]
    public void AFileFromBeforeReadsAsItWas()
    {
        // A weather folder written before the tides: nothing in it changes, nothing is added.
        var island = BarKind.FolderList.New();
        island.Id = "weather";
        island.Folder!.Face = WeatherFace.New();
        var line = ListKind.WeatherNow.New();
        line.Weather = SavedWeather.Options();
        island.Folder.Put(line);
        island.Normalize();
        var json = Karpo.Tests.Core.Configs.With(island).ToJson();
        Assert.DoesNotContain("tides", json);
        var back = Karpo.Tests.Core.Configs.Repaired(json, out var changed);
        Assert.False(changed);
        Assert.Equal(json, back.ToJson());
    }

    [Fact]
    public void TheWidgetIsInTheWeatherPageAfterTheAirQuality()
    {
        Assert.Equal(WidgetCategory.Weather, WidgetInfos.Tides.Category);
        Assert.Equal(WidgetOptionsKind.Weather, WidgetInfos.Tides.Options);
        var weather = AddTree.Browse.Rows.OfType<AddShelf>().Single(shelf => shelf.Id == "weather");
        var ids = weather.Rows.OfType<AddLeaf>().Select(leaf => leaf.Id).ToList();
        Assert.Equal(ids.IndexOf("airForecast") + 1, ids.IndexOf(Presets.TidesId));
        Assert.Equal(ids.IndexOf(Presets.TidesId) + 1, ids.IndexOf("tides"));
        // §603: the tides now and the tide graph, each of two sizes, a new line Full; no Height.
        Assert.Equal(ids.IndexOf("tides") + 1, ids.IndexOf("tideGraph"));
        foreach (var kind in new[] { ListKind.Tides, ListKind.TideGraph })
        {
            Assert.Equal([LineSize.Compact, LineSize.Full], kind.Offered());
            Assert.Equal(LineSize.Full, ListWidgets.Of(kind).Starts);
            Assert.False(kind.HasHeight());
        }
        Assert.Contains(FolderFaces.Tides, FolderFaces.All);
    }
}
