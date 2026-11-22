// menus: a search field's live results (Karpo.Dock\MenuSearch.cs, docs\widgets.md §219), with a finder
// whose answers the test hands in when it likes, so the order they come back in is the test's:
//
//  - **The wait.** Typing asks only once the person stops a moment (the finder's Wait), a finder that
//    asks no service at once; fewer letters than it needs ask nothing. Meanwhile "Searching…".
//  - **A newer search wins.** An answer to anything but the last question is dropped, however late;
//    the older question is cancelled. A field closed drops every answer still out.
//  - **Nothing, or no service**, is one greyed row in the finder's words; a time-out that nobody
//    asked for is no service, not silence.
//  - **A pick is proved before it is kept**, and refused in words where it is no good.
//  - **Enter** keeps the first found for the words, else the words where they are a thing of their own, else asks now.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Menus;

public sealed class MenuSearchTests
{
    /// <summary>A finder whose every question waits for the test to answer it (<see cref="Answer"/>).</summary>
    private sealed class Held : OptionFinder
    {
        public readonly List<(string Query, TaskCompletionSource<IReadOnlyList<OptionFound>> Answer, CancellationToken Cancel)> Asked = [];
        public readonly List<(OptionFound Found, TaskCompletionSource<OptionChecked> Answer)> Checked = [];

        public override int LeastLetters => 2;

        public override TimeSpan Wait => TimeSpan.FromMilliseconds(300);

        public override bool Remote => true;

        public bool Proves { get; init; }

        public override IReadOnlyList<OptionFound> Offered => [new OptionFound("None")];

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel)
        {
            var answer = new TaskCompletionSource<IReadOnlyList<OptionFound>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Asked.Add((query, answer, cancel));
            return answer.Task;
        }

        public override OptionChecked? Typed(string text) =>
            text.StartsWith('=') ? new OptionChecked(new OptionFound(text[1..], Value: text[1..])) : text.StartsWith('!') ? new OptionChecked(null, "Not one") : null;

        public override string? Checking(OptionFound found) => Proves ? $"Looking up {found.Title}…" : null;

        public override Task<OptionChecked> CheckAsync(OptionFound found, CancellationToken cancel)
        {
            var answer = new TaskCompletionSource<OptionChecked>(TaskCreationOptions.RunContinuationsAsynchronously);
            Checked.Add((found, answer));
            return answer.Task;
        }

        public override string NoneFound(string query) => $"Nothing called {query}";

        public override string Failed => "No service";
    }

    /// <summary>A search over <paramref name="finder"/> whose answers wait in <paramref name="posted"/> until the test runs them, as the menu's window would.</summary>
    private static MenuSearch Search(OptionFinder finder, List<Action> posted, Action? changed = null) =>
        new(finder, posted.Add, changed ?? (() => { }));

    private static OptionFound Place(string name) => new(name, "Switzerland", name);

    /// <summary>Runs every answer that came back so far, as the menu's window would; waits a moment for the finder's continuations to post.</summary>
    private static void Deliver(List<Action> posted)
    {
        for (var tries = 0; tries < 100 && posted.Count == 0; tries++)
        {
            Thread.Sleep(5);
        }
        foreach (var answer in posted.ToList())
        {
            posted.Remove(answer);
            answer();
        }
    }

    [Fact]
    public void TypingAsksOnlyOnceThePersonStopsAMoment()
    {
        var finder = new Held();
        List<Action> posted = [];
        var search = Search(finder, posted);
        search.Opened();
        Assert.Equal(["None"], search.Rows.Select(row => row.Title));

        // Too few letters: nothing asked, the offered row alone.
        Assert.False(search.Typed("z", 1000));
        Assert.Equal(SearchPhase.Idle, search.Phase);
        Assert.Empty(finder.Asked);

        Assert.True(search.Typed("zu", 1000));
        Assert.Equal((SearchPhase.Waiting, 1300L), (search.Phase, search.DueAt));
        Assert.Equal(S.SettingsWidgetSearching, search.Said);
        Assert.True(search.Typed("zur", 1100));
        search.Due(1300);
        // Still waiting: the last key was at 1100.
        Assert.Empty(finder.Asked);
        search.Due(1400);
        Assert.Equal("zur", Assert.Single(finder.Asked).Query);
        Assert.Equal(SearchPhase.Searching, search.Phase);
    }

    [Fact]
    public void ANewerSearchWinsHoweverLateTheOlderAnswers()
    {
        var finder = new Held();
        List<Action> posted = [];
        var changes = 0;
        var search = Search(finder, posted, () => changes++);
        search.Typed("zu", 0);
        search.Due(long.MaxValue);
        search.Typed("zug", 0);
        search.Due(long.MaxValue);
        Assert.Equal(2, finder.Asked.Count);
        // The older question was called off.
        Assert.True(finder.Asked[0].Cancel.IsCancellationRequested);

        finder.Asked[1].Answer.SetResult([Place("Zug")]);
        Deliver(posted);
        finder.Asked[0].Answer.SetResult([Place("Zürich"), Place("Zuoz")]);
        Deliver(posted);
        Assert.Equal(["Zug"], search.Found.Select(found => found.Title));
        Assert.Equal(SearchPhase.Found, search.Phase);
        Assert.Null(search.Said);
        Assert.Equal(1, changes);
        // The offered rows first, then what was found.
        Assert.Equal(["None", "Zug"], search.Rows.Select(row => row.Title));
    }

    [Fact]
    public void AnAnswerAfterTheFieldClosedIsDropped()
    {
        var finder = new Held();
        List<Action> posted = [];
        var search = Search(finder, posted);
        search.Typed("bern", 0);
        search.Due(long.MaxValue);
        search.Close();
        finder.Asked[0].Answer.SetResult([Place("Bern")]);
        Thread.Sleep(20);
        foreach (var answer in posted.ToList())
        {
            answer();
        }
        Assert.Empty(search.Found);
        Assert.Empty(search.Rows);
        Assert.False(search.Open);
    }

    [Fact]
    public void NothingFoundAndNoServiceAreSaidInTheFindersWords()
    {
        var finder = new Held();
        List<Action> posted = [];
        var search = Search(finder, posted);
        search.Typed("qqq", 0);
        search.Due(long.MaxValue);
        finder.Asked[0].Answer.SetResult([]);
        Deliver(posted);
        Assert.Equal((SearchPhase.Nothing, "Nothing called qqq"), (search.Phase, search.Said));

        search.Typed("bern", 0);
        search.Due(long.MaxValue);
        finder.Asked[1].Answer.SetException(new HttpRequestException("down"));
        Deliver(posted);
        Assert.Equal((SearchPhase.Failed, "No service"), (search.Phase, search.Said));

        // A time-out nobody asked for (HttpClient's) is no service, not a search left searching for ever.
        search.Typed("basel", 0);
        search.Due(long.MaxValue);
        finder.Asked[2].Answer.SetCanceled();
        Deliver(posted);
        Assert.Equal((SearchPhase.Failed, "No service"), (search.Phase, search.Said));
    }

    [Fact]
    public void APickIsProvedBeforeItIsKept()
    {
        var finder = new Held { Proves = true };
        List<Action> posted = [];
        var search = Search(finder, posted);
        search.Typed("apple", 0);
        search.Due(long.MaxValue);
        finder.Asked[0].Answer.SetResult([Place("AAPL"), Place("AAPL.TO")]);
        Deliver(posted);

        List<OptionFound> kept = [];
        search.Pick(search.Found[1], kept.Add);
        Assert.Equal((SearchPhase.Checking, "Looking up AAPL.TO…"), (search.Phase, search.Said));
        // While it is proved, no rows to pick from.
        Assert.Empty(search.Rows);
        finder.Checked[0].Answer.SetResult(new OptionChecked(null, "No prices for AAPL.TO"));
        Deliver(posted);
        Assert.Empty(kept);
        Assert.Equal((SearchPhase.Refused, "No prices for AAPL.TO"), (search.Phase, search.Said));

        search.Pick(Place("AAPL"), kept.Add);
        finder.Checked[1].Answer.SetResult(new OptionChecked(Place("AAPL") with { Detail = "Apple Inc." }));
        Deliver(posted);
        Assert.Equal("Apple Inc.", Assert.Single(kept).Detail);
        Assert.False(search.Open);
    }

    [Fact]
    public void APickAFinderDoesNotProveIsKeptAtOnce()
    {
        var finder = new Held();
        List<OptionFound> kept = [];
        var search = Search(finder, []);
        search.Pick(Place("Bern"), kept.Add);
        Assert.Equal("Bern", Assert.Single(kept).Title);
        Assert.Empty(finder.Checked);
    }

    [Fact]
    public void EnterKeepsTheWordsTheFirstFoundOrAsksNow()
    {
        var finder = new Held();
        List<Action> posted = [];
        List<OptionFound> kept = [];
        var search = Search(finder, posted);

        // Words that are a thing of their own (a symbol written out): kept as they are.
        search.Enter("=MSFT", kept.Add);
        Assert.Equal("MSFT", Assert.Single(kept).Title);

        // Words refused: said, nothing asked.
        search.Enter("!x", kept.Add);
        Assert.Equal((SearchPhase.Refused, "Not one"), (search.Phase, search.Said));

        // A search not asked yet: asked at once, without the wait.
        search.Typed("bern", 0);
        search.Enter("bern", kept.Add);
        Assert.Equal("bern", Assert.Single(finder.Asked).Query);
        finder.Asked[0].Answer.SetResult([Place("Bern"), Place("Bernina")]);
        Deliver(posted);

        // Found: the first is kept.
        search.Enter("bern", kept.Add);
        Assert.Equal(["MSFT", "Bern"], kept.Select(found => found.Title));

        // Found for these very words, even where they would be a thing of their own: the first found.
        search.Typed("=ms", 0);
        search.Due(long.MaxValue);
        finder.Asked[1].Answer.SetResult([Place("Microsoft")]);
        Deliver(posted);
        search.Enter("=ms", kept.Add);
        Assert.Equal("Microsoft", kept[^1].Title);
    }

    /// <summary>A finder that needs no letters (a time zone's) offers what it has as the field opens, and finds as it is typed, without waiting.</summary>
    [Fact]
    public void AFinderOfNoServiceFindsAtOnce()
    {
        var zones = OptionFields.OfWidget(ListKind.Clock.New(), ["Tokyo Standard Time"]).Single(row => row.Key == OptionFields.ClockZone);
        List<Action> posted = [];
        var search = Search(zones.Finder!, posted);
        search.Opened();
        Assert.Equal(SearchPhase.Found, search.Phase);
        // This PC's first, ticked, then the zones the person's clocks show.
        Assert.Equal(S.SettingsWidgetClockZoneLocal, search.Found[0].Title);
        Assert.True(search.Found[0].Chosen);
        Assert.Equal("Tokyo Standard Time", Assert.IsType<ZoneEntry>(search.Found[1].Value).Id);

        Assert.False(search.Typed("+9", 0));
        Assert.Equal(SearchPhase.Found, search.Phase);
        Assert.All(search.Found, found => Assert.Equal(TimeSpan.FromHours(9), Assert.IsType<ZoneEntry>(found.Value).Offset));
        // The person's own first among them.
        Assert.Equal("Tokyo Standard Time", Assert.IsType<ZoneEntry>(search.Found[0].Value).Id);
        Assert.Empty(posted);

        Assert.False(search.Typed("qqqqq", 0));
        Assert.Equal((SearchPhase.Nothing, S.DockEditZoneNotFound("qqqqq")), (search.Phase, search.Said));
    }

    /// <summary>
    /// §230: nothing typed is never an empty list. A zone's: this PC's, the person's own, then every
    /// other in Windows' order, every one a row (§271).
    /// </summary>
    [Fact]
    public void NothingTypedAZoneOffersEveryZoneThePersonsOwnFirst()
    {
        var zones = OptionFields.OfWidget(ListKind.Clock.New(), ["Tokyo Standard Time", "Pacific Standard Time"]).Single(row => row.Key == OptionFields.ClockZone);
        var search = Search(zones.Finder!, []);
        search.Opened();
        var ids = search.Rows.Select(row => Assert.IsType<ZoneEntry>(row.Value).Id).ToList();
        Assert.Equal(["", "Tokyo Standard Time", "Pacific Standard Time"], ids.Take(3));
        Assert.Equal(TimeZoneSearch.Windows.Count + 1, ids.Count);
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // The rest by offset, as Windows lists them.
        var offsets = search.Rows.Skip(3).Select(row => ((ZoneEntry)row.Value!).Offset).ToList();
        Assert.Equal(offsets.Order(), offsets);
        // Each says its place names, its offset now under them.
        var tokyo = search.Rows[1];
        Assert.Equal(("Osaka, Sapporo, Tokyo", "UTC+9"), (tokyo.Title, tokyo.Detail));
    }

    /// <summary>
    /// §230: a place's, nothing or too little typed: its own place ticked, the others the person's
    /// widgets show — and nothing asked of the service; the device's location is its switch's, no row
    /// of the search (§240).
    /// </summary>
    [Fact]
    public void NothingTypedAPlaceOffersItsOwnAndTheOthersInUse()
    {
        var weather = ListKind.WeatherNow.New();
        weather.Weather = new WeatherOptions { Place = "Zürich, Switzerland", Latitude = 47.37, Longitude = 8.54 };
        // The person's faces: Zürich again, Bern, and the device's location, which is no place of its own.
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.WeatherId)];
        folder.Folder.Faces[0].Weather = new WeatherOptions { Place = "Zürich, Switzerland", Latitude = 47.37, Longitude = 8.54 };
        folder.Folder.Faces[1].Weather = new WeatherOptions { Place = "Bern, Switzerland", Latitude = 46.95, Longitude = 7.45 };
        folder.Folder.Faces[2].Weather = new WeatherOptions { UseDeviceLocation = true, Latitude = 1, Longitude = 2 };
        var config = new Config();
        config.Layouts.Add(new TaskbarLayout { Id = "a", Name = "A", Islands = [folder] });
        var places = OptionFields.PlacesInUse(config);
        Assert.Equal(["Zürich, Switzerland", "Bern, Switzerland"], places.Select(place => place.Label));

        var row = OptionRows.Of(weather, new OptionDevices { Places = places }).Single(each => each.Key == OptionFields.WeatherPlace);
        var search = Search(row.Finder!, []);
        search.Opened();
        // §283: each by its town, where it is beside it, as the search lists its results.
        Assert.Equal([("Zürich", "Switzerland"), ("Bern", "Switzerland")], search.Rows.Take(2).Select(found => (found.Title, found.Detail)));
        Assert.Equal([true, false], search.Rows.Take(2).Select(found => found.Chosen));
        // §411: then the world's great cities, Zürich not twice.
        Assert.Equal(("London", "United Kingdom"), (search.Rows[2].Title, search.Rows[2].Detail));
        Assert.Single(search.Rows, found => found.Title == "Zürich");
        var suggested = search.Rows.Count;
        _ = search.Typed("z", 0);
        Assert.Equal(suggested, search.Rows.Count);
        Assert.Equal(SearchPhase.Idle, search.Phase);

        // Picked, it is kept as it was: the same words and the same place.
        var bern = new WeatherOptions();
        Assert.True(OptionTexts.SetPlace(bern, search.Rows[1]));
        Assert.Equal(("Bern, Switzerland", 46.95, 7.45), (bern.Place, bern.Latitude, bern.Longitude));
    }

    /// <summary>§230: a share's, nothing typed: its own ticked, then the others the person's stocks show; an action's every one.</summary>
    [Fact]
    public void NothingTypedAShareOffersItsOwnAndAnActionEveryOne()
    {
        var stock = ListKind.Markets.New();
        stock.Market = new MarketOptions { Symbol = "AAPL", Detail = "Apple Inc. · NASDAQ · USD" };
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.MarketsId), FolderFace.Of(FolderFaces.MarketsId)];
        folder.Folder.Faces[0].Market = new MarketOptions { Symbol = "MSFT" };
        folder.Folder.Faces[1].Market = new MarketOptions { Symbol = "aapl" };
        var config = new Config();
        config.Layouts.Add(new TaskbarLayout { Id = "a", Name = "A", Islands = [folder] });
        var shares = OptionFields.SharesInUse(config);
        Assert.Equal(["MSFT", "aapl"], shares.Select(share => share.Symbol));
        var row = OptionRows.Of(stock, new OptionDevices { Shares = shares }).Single(each => each.Key == MarketRows.Symbol);
        var search = Search(row.Finder!, []);
        search.Opened();
        // §411: then well-known ones, so nothing typed is never an empty list.
        Assert.Equal([("Apple Inc.", "AAPL", true), ("MSFT", "", false), ("NVIDIA Corporation", "NVDA", false)], search.Rows.Take(3).Select(found => (found.Title, found.Detail, found.Chosen)));

        var actions = OptionRows.Of(ListKind.Actions.New()).First(each => each.Kind == OptionRowKind.Search);
        var all = Search(actions.Finder!, []);
        all.Opened();
        // §411: the one it opens first, ticked; "None" among the rest.
        Assert.True(all.Rows[0].Chosen);
        Assert.Contains(all.Rows, found => found.Title == S.SettingsWidgetActionsNone);
        Assert.Equal(SettingsActions.Search(null).Sum(group => group.Actions.Count) + 1, all.Rows.Count);
    }

    /// <summary>
    /// Every row (§271; §228 to §270 showed five and "Show more"): the finder's own and all that was
    /// found, however many, and as many again for the next words. A zone's "utc" finds more than five,
    /// as many as the menu keeps at most.
    /// </summary>
    [Fact]
    public void EveryRowFoundIsShown()
    {
        var finder = new Held();
        List<Action> posted = [];
        var search = Search(finder, posted);
        search.Opened();
        Assert.True(search.Typed("zu", 0));
        search.Due(long.MaxValue);
        finder.Asked[^1].Answer.SetResult([.. Enumerable.Range(1, 11).Select(at => Place($"Place {at}"))]);
        Deliver(posted);
        // The offered row and eleven found, every one.
        Assert.Equal(12, search.Rows.Count);

        _ = search.Typed("zur", 0);
        search.Due(long.MaxValue);
        finder.Asked[^1].Answer.SetResult([.. Enumerable.Range(1, 4).Select(at => Place($"Place {at}"))]);
        Deliver(posted);
        Assert.Equal(5, search.Rows.Count);

        var zones = OptionFields.OfWidget(ListKind.Clock.New()).Single(row => row.Key == OptionFields.ClockZone);
        var zone = Search(zones.Finder!, []);
        _ = zone.Typed("utc", 0);
        Assert.InRange(zone.Rows.Count, 6, MenuSearch.Most);
    }
}
