// §655: Windows search's home — what the line shows before anything is typed — and its page. The sections are one registry
// (WinSearchHomeInfos.All) the dock's and the ghost's home sources answer; what a line shows is its own order, switches and
// counts, a thing shown once, a section with nothing left out; the page sets them plainly (Search in with the way to
// Windows' Searching Windows, When nothing is typed an ordered list, Items per section a count each), writing only what
// is not the default; Top apps are the apps opened most; the home's sections read what Karpo already reads, Start's source
// told once and let go; the split's breakpoint. Nothing here reads this PC, opens anything or draws.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.StartWidgets;
using Karpo.Dock.WinSearch;

namespace Karpo.Tests.Searching;

public sealed class WinSearchHomeTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static WinSearchHit File(string path) => WinSearchHomeHits.OfPath(path, Now, folder: false);

    private static WinSearchHit App(string name) => WinSearchHomeHits.OfApp(new WinSearchApp(name, @"shell:AppsFolder\" + name));

    /// <summary>A home with every section listing what it is handed.</summary>
    private static WinSearchHome Home(IReadOnlyList<string> searches, params (string Id, WinSearchHit[] Hits)[] sections) =>
        new(searches, [.. sections.Select(each => new WinSearchHomeSection(WinSearchHomeInfos.Of(each.Id)!, each.Hits, []))]);

    // --- The registry -------------------------------------------------------------------------------------

    [Fact]
    public void TheSectionsAreOneListTheDockAndTheGhostAnswer()
    {
        var ids = WinSearchHomeInfos.All.Select(info => info.Id).ToList();
        Assert.Equal(["searches", "apps", "pinned", "files", "folders", "events", "mail", "notes"], ids);
        Assert.All(WinSearchHomeInfos.All, info =>
        {
            Assert.NotEqual(info.Id, info.Title);
            Assert.NotEmpty(info.Glyph);
            Assert.InRange(info.Shown, WinSearchHomeInfos.Fewest, info.Most);
        });
        // Every one shown by default but the notes, in the registry's order.
        Assert.Equal(ids.Where(id => id != WinSearchHomeInfos.Notes), WinSearchHomeInfos.Defaults);
        // The dock's line and a ghost each have a home source for every section but the recent searches (the engine's own).
        var answered = ids.Where(id => id != WinSearchHomeInfos.Searches).Order(StringComparer.Ordinal);
        Assert.Equal(answered, WinSearchSystem.Sources(static () => new Config(), static _ => null).Home.Select(source => source.Id).Order(StringComparer.Ordinal));
        Assert.Equal(answered, WinSearchSamples.Sources(new Config()).Home.Select(source => source.Id).Order(StringComparer.Ordinal));
    }

    // --- What a line shows ----------------------------------------------------------------------------------

    [Fact]
    public void ALineShowsItsSectionsInItsOrderEachCutToItsCountEachThingOnce()
    {
        var paint = App("Paint");
        var home = Home(["tax", "budget", "trip", "car", "rent"],
            (WinSearchHomeInfos.Apps, [paint, App("Word")]),
            (WinSearchHomeInfos.Pinned, [App("Paint"), App("Edge")]),
            (WinSearchHomeInfos.Files, [.. Enumerable.Range(0, 9).Select(at => File($@"C:\f{at}.txt"))]),
            (WinSearchHomeInfos.Mail, []));
        // By default: the searches (four), Top apps, Pinned without what Top apps showed, six files; no mail, nothing to show.
        var shown = home.For(null);
        Assert.Equal(["searches", "apps", "pinned", "files"], shown.Select(section => section.Info.Id));
        Assert.Equal(["tax", "budget", "trip", "car"], shown[0].Searches);
        Assert.Equal(["Edge"], shown[2].Hits.Select(hit => hit.Name));
        Assert.Equal(6, shown[3].Count);
        // The person's own: the files first, two of them, then the pinned apps — Paint among them now —; no searches.
        var options = new WinSearchOptions();
        options.SetHome([WinSearchHomeInfos.Files, WinSearchHomeInfos.Pinned]);
        options.SetHomeMost(WinSearchHomeInfos.Files, 2);
        shown = home.For(options);
        Assert.Equal(["files", "pinned"], shown.Select(section => section.Info.Id));
        Assert.Equal(2, shown[0].Count);
        Assert.Equal(["Paint", "Edge"], shown[1].Hits.Select(hit => hit.Name));
    }

    [Fact]
    public void OnlyWhatIsNotTheDefaultIsWritten()
    {
        var options = new WinSearchOptions();
        Assert.True(options.Empty);
        // The default sections in their order: none written.
        options.SetHome(WinSearchHomeInfos.Defaults);
        Assert.Null(options.Home);
        // Another order, a word no section has and one twice: the known ones, once each.
        options.SetHome(["files", "nope", "apps", "files"]);
        Assert.Equal(["files", "apps"], options.Home);
        // A count held to its section's range; its default none.
        options.SetHomeMost(WinSearchHomeInfos.Files, 500);
        Assert.Equal(24, options.HomeMost![WinSearchHomeInfos.Files]);
        Assert.Equal(24, WinSearchHomeInfos.Count(options, WinSearchHomeInfos.Files));
        options.SetHomeMost(WinSearchHomeInfos.Files, WinSearchHomeInfos.Of(WinSearchHomeInfos.Files)!.Shown);
        Assert.Null(options.HomeMost);
        options.SetHome(WinSearchHomeInfos.Defaults);
        Assert.True(options.Empty);
        // A line's record: written as the file has it, and none where nothing differs.
        var item = ListKind.WinSearch.New();
        item.WinSearch = new WinSearchOptions { Home = ["mail", "files"], HomeMost = new() { ["mail"] = 5, ["files"] = 6 } };
        item.Normalize();
        var json = System.Text.Json.JsonSerializer.Serialize(item.WinSearch!, ConfigJson.Default.WinSearchOptions);
        Assert.Equal("""{"home":["mail","files"],"homeMost":{"mail":5}}""", string.Concat(json.Where(c => !char.IsWhiteSpace(c))));
        item.WinSearch = new WinSearchOptions { Home = [.. WinSearchHomeInfos.Defaults] };
        item.Normalize();
        Assert.Null(item.WinSearch);
    }

    [Fact]
    public void TopAppsAreTheAppsOpenedMost()
    {
        WinSearchApp[] apps = [new("Paint", "p"), new("Word", "w"), new("Edge", "e"), new("Notepad", "n")];
        (int, DateTime) Started(string target) => target switch
        {
            "p" => (2, Now.AddDays(-3)),
            "w" => (2, Now.AddDays(-1)),
            "e" => (1, Now.AddDays(-9)),
            _ => (0, default),
        };
        // Edge opened from the line too: three, the most; of Paint and Word, opened alike, Word last.
        IReadOnlyList<WinSearchOpened> opened =
        [
            new(WinSearchCategory.Apps, "E", "Edge", "ed", Now.AddHours(-1)),
            new(WinSearchCategory.Apps, "e", "Edge", "edge", Now.AddHours(-2)),
            new(WinSearchCategory.Documents, "w", "w", "w", Now),
        ];
        Assert.Equal(["Edge", "Word", "Paint"], WinSearchHomeHits.TopApps(apps, Started, opened, 5).Select(hit => hit.Name));
        Assert.Equal(["Edge"], WinSearchHomeHits.TopApps(apps, Started, opened, 1).Select(hit => hit.Name));
        Assert.All(WinSearchHomeHits.TopApps(apps, Started, opened, 5), hit => Assert.Equal(WinSearchCategory.Apps, hit.Category));
    }

    /// <summary>
    /// §658: on a first use nothing was opened from Karpo yet: the usual apps (Start's pinned) stand in, in their order, after
    /// those opened, each once — the section is never empty on a PC with apps.
    /// </summary>
    [Fact]
    public void TopAppsFallBackToThePinnedOnAFirstUse()
    {
        WinSearchApp[] apps = [new("Paint", "p"), new("Word", "w"), new("Edge", "e"), new("Notepad", "n")];
        WinSearchApp[] pinned = [new("Edge", "e"), new("Notepad", "n"), new("Word", "w")];
        static (int, DateTime) Never(string target) => (0, default);
        Assert.Equal(["Edge", "Notepad", "Word"], WinSearchHomeHits.TopApps(apps, Never, [], 6, pinned).Select(hit => hit.Name));
        Assert.Equal(["Edge", "Notepad"], WinSearchHomeHits.TopApps(apps, Never, [], 2, pinned).Select(hit => hit.Name));
        (int, DateTime) PaintOnce(string target) => target == "p" ? (1, Now) : (0, default);
        Assert.Equal(["Paint", "Edge", "Notepad"], WinSearchHomeHits.TopApps(apps, PaintOnce, [], 3, pinned).Select(hit => hit.Name));
        Assert.Empty(WinSearchHomeHits.TopApps(apps, Never, [], 6));
    }

    // --- The page ------------------------------------------------------------------------------------------

    private static List<OptionRow> Rows(WidgetItem item, Action<string>? open = null) =>
        [.. OptionRows.Of(item, new OptionDevices { Open = open }).Where(row => row.Group is { } group
            && (group == WinSearchWords.SearchIn || group == WinSearchWords.HomeShown || group == WinSearchWords.HomeCounts))];

    [Fact]
    public void ThePageLinksToSearchingWindowsUnderSearchIn()
    {
        var item = ListKind.WinSearch.New();
        item.Normalize();
        var opened = new List<string>();
        var rows = Rows(item, opened.Add);
        var index = rows.Single(row => row.Key == OptionRows.WinSearchIndex);
        Assert.Equal(WinSearchWords.SearchIn, index.Group);
        // The last of Search in, after every source's switch: an action naming Windows' page, saying what it is for.
        Assert.Equal(index, rows.Last(row => row.Group == WinSearchWords.SearchIn));
        Assert.Equal(OptionRowKind.Action, index.Kind);
        Assert.Equal(WindowsPages.SearchingWindows.Title, index.Title);
        Assert.False(index.Set!(0));
        Assert.Equal(["ms-settings:cortana-windowssearch"], opened);
        Assert.Null(item.WinSearch);
    }

    [Fact]
    public void WhenNothingIsTypedIsAnOrderedListItsCountsBesideIt()
    {
        var item = ListKind.WinSearch.New();
        item.Normalize();
        var rows = Rows(item);
        var sections = rows.Where(row => row.Group == WinSearchWords.HomeShown).ToList();
        // Every section, those shown first in their order, then the notes, off; the toggles row's ordered list, in its own words.
        Assert.Equal(WinSearchHomeInfos.Defaults.Append(WinSearchHomeInfos.Notes).Select(id => OptionRows.WinSearchHome + id), sections.Select(row => row.Key));
        Assert.All(sections, row =>
        {
            Assert.Equal(OptionRowKind.Toggle, row.Kind);
            Assert.True(row.Ordered);
            Assert.Equal(S.CoreWinSearchHomeAdd, row.OrderedWords?.Add);
        });
        Assert.Equal(sections.Select(row => row.Key != OptionRows.WinSearchHome + WinSearchHomeInfos.Notes), sections.Select(row => row.On));
        Assert.Null(sections.Last().Move);
        // A count for each section shown, at its default, within its range.
        var counts = rows.Where(row => row.Group == WinSearchWords.HomeCounts).ToList();
        Assert.Equal(WinSearchHomeInfos.Defaults.Select(id => OptionRows.WinSearchHomeMost + id), counts.Select(row => row.Key));
        Assert.All(counts, row => Assert.Equal(OptionRowKind.Number, row.Kind));
        Assert.Equal(6, counts.Single(row => row.Key == OptionRows.WinSearchHomeMost + "files").Value);

        // The files dragged to the top: the order written.
        Assert.True(sections.Single(row => row.Key == OptionRows.WinSearchHome + "files").Move!(0));
        Assert.Equal(["files", "searches", "apps", "pinned", "folders", "events", "mail"], item.WinSearch?.Home);
        // The mail taken out, the notes put in at the end.
        rows = Rows(item);
        Assert.True(rows.Single(row => row.Key == OptionRows.WinSearchHome + "mail").Set!(0));
        Assert.True(Rows(item).Single(row => row.Key == OptionRows.WinSearchHome + "notes").Set!(1));
        Assert.Equal(["files", "searches", "apps", "pinned", "folders", "events", "notes"], item.WinSearch?.Home);
        // Ten files: written; six again: none.
        Assert.True(Rows(item).Single(row => row.Key == OptionRows.WinSearchHomeMost + "files").Set!(10));
        Assert.Equal(new Dictionary<string, int> { ["files"] = 10 }, item.WinSearch?.HomeMost);
        Assert.False(Rows(item).Single(row => row.Key == OptionRows.WinSearchHomeMost + "files").Set!(10));
        Assert.True(Rows(item).Single(row => row.Key == OptionRows.WinSearchHomeMost + "files").Set!(6));
        Assert.Null(item.WinSearch?.HomeMost);
        // Back to the default order and sections: no record at all.
        item.WinSearch!.SetHome(WinSearchHomeInfos.Defaults);
        item.Normalize();
        Assert.Null(item.WinSearch);
    }

    /// <summary>
    /// §717 (the menu rules' R4): "Search in" says how many sources are asked, as Show from says it, and "Items per section"
    /// how many the sections shown show together; both follow what is set.
    /// </summary>
    [Fact]
    public void SearchInAndItsCountsSayWhatTheyHold()
    {
        var item = ListKind.WinSearch.New();
        item.Normalize();
        var all = WinSearchSourceInfos.All.Count;
        string? Said(string group) => Rows(item).Where(row => row.Group == group).Select(row => row.GroupSummary).Distinct().Single();
        // Every source but the web's is asked until the person says otherwise.
        Assert.Equal(S.DockMenuShowFromSome(all - 1, all), Said(WinSearchWords.SearchIn));
        Assert.True(Rows(item).Single(row => row.Key == OptionRows.WinSearchSource + WinSearchSourceInfos.Web).Set!(1));
        Assert.Equal(S.DockMenuShowFromAll, Said(WinSearchWords.SearchIn));
        // The counts of the sections shown, added up; one more file, one more.
        var shown = WinSearchHomeInfos.Shown(null).Sum(info => info.Shown);
        Assert.Equal(S.DockEditShownCount(shown), Said(WinSearchWords.HomeCounts));
        Assert.True(Rows(item).Single(row => row.Key == OptionRows.WinSearchHomeMost + WinSearchHomeInfos.Files).Set!(7));
        Assert.Equal(S.DockEditShownCount(shown + 1), Said(WinSearchWords.HomeCounts));
    }

    // --- The dock's sections -----------------------------------------------------------------------------------

    [Fact]
    public void TheDocksSectionsReadWhatKarpoAlreadyReads()
    {
        var start = new StartSamples(newApp: null);
        var word = StartSamples.SampleApps.Single(app => app.Name == "Word");
        start.Uses.Opened(word.Target, StartSamples.Now);
        // A config whose Start (beta) has its own pins: Paint, then Word.
        var config = new Config();
        var pins = Presets.Folder(Presets.Find("startGroup")!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var line = pins.Folder!.Cells.Select(cell => cell?.Island).First(island => island?.List == ListKind.StartPins)!;
        var paint = StartSamples.SampleApps.Single(app => app.Name == "Paint");
        line.Start = new StartOptions { Pins = [paint.Target, word.Target] };
        config.Layouts.Add(new TaskbarLayout { Id = "start", Islands = [pins] });
        var local = new DateTime(2026, 11, 9, 9, 0, 0);
        CalendarEvent[] events =
        [
            new(local.AddHours(-3), local.AddHours(-2), false, "Over", null),
            new(local.AddHours(3), local.AddHours(4), false, "Lunch", "Café"),
            new(local.AddHours(-1), local.AddHours(1), false, "Now on", null),
            new(local.AddDays(30), local.AddDays(30).AddHours(1), false, "Too far", null),
        ];
        var received = new DateTimeOffset(local);
        MailItem[] mail =
        [
            new("1", "Ann", "ann@example.com", "Read", "", received, Unread: false),
            new("2", "Ben", "ben@example.com", "Older", "", received.AddHours(-2), Unread: true),
            new("3", "Cy", "cy@example.com", "Newer", "", received.AddHours(-1), Unread: true),
        ];
        Note[] notes = [new() { Id = "a", Title = "Old", Modified = received.AddDays(-2) }, new() { Id = "b", Title = "New", Modified = received }];
        var homes = WinSearchHomes.Of(start, () => config, () => mail, (from, to) => [.. events.Where(each => each.End >= from && each.Start <= to)],
            static (_, _) => "https://calendar.example.com", () => notes, () => local);
        IReadOnlyList<string> Names(string id) => [.. homes.Single(each => each.Id == id).List(new WinSearchHomeAsk(12, Now, [])).Select(hit => hit.Name)];
        // §658: Word opened, then the pins fill the rest, each once.
        Assert.Equal(["Word", "Paint"], Names(WinSearchHomeInfos.Apps));
        Assert.Equal(["Paint", "Word"], Names(WinSearchHomeInfos.Pinned));
        Assert.Equal(StartSamples.SampleFiles.Select(file => file.Name), Names(WinSearchHomeInfos.Files));
        Assert.Equal(StartSamples.SampleFolders.Select(folder => folder.Name), Names(WinSearchHomeInfos.Folders));
        Assert.Equal(WinSearchCategory.Folders, homes.Single(each => each.Id == WinSearchHomeInfos.Folders).List(new WinSearchHomeAsk(1, Now, []))[0].Category);
        Assert.Equal(["Now on", "Lunch"], Names(WinSearchHomeInfos.Events));
        Assert.Equal(["Newer", "Older"], Names(WinSearchHomeInfos.Mail));
        Assert.Equal(["New", "Old"], Names(WinSearchHomeInfos.Notes));
        // No Start (beta) in the config: this PC's usual pins.
        config = new Config();
        Assert.Equal(StartPins.Apps(StartPins.Defaults(StartSamples.SampleApps), StartSamples.SampleApps).Take(12).Select(app => app.Name), Names(WinSearchHomeInfos.Pinned));
    }

    [Fact]
    public void StartsSourceTellsTheLineOnceWhileItShowsAndNoMoreOnceItHasGone()
    {
        var start = new StartSamples(newApp: null);
        var homes = WinSearchHomes.Of(start, static () => new Config(), static () => [], static (_, _) => [], static (_, _) => null, static () => [], static () => DateTime.Now);
        var told = 0;
        void Came() => told++;
        foreach (var home in homes)
        {
            home.Warm(Came);
        }
        start.Opened(StartSamples.SampleApps[0].Target);
        Assert.Equal(1, told);
        foreach (var home in homes)
        {
            home.Forget();
        }
        start.Opened(StartSamples.SampleApps[0].Target);
        Assert.Equal(1, told);
    }

    // --- The engine's home -----------------------------------------------------------------------------------

    [Fact]
    public void ASectionThatFailsListsNothingAndGoneTheSectionsAreLetGo()
    {
        var work = new Queue<Action>();
        var forgotten = 0;
        IWinSearchHomeSource[] home =
        [
            new WinSearchHomeSource(WinSearchHomeInfos.Files, static _ => throw new InvalidOperationException("broken")),
            new WinSearchHomeSource(WinSearchHomeInfos.Mail, static ask => [new WinSearchHit(WinSearchCategory.Email, $"most {ask.Most}", "m")], forget: () => forgotten++),
        ];
        var engine = new WinSearchEngine(new WinSearchSources([], home, work.Enqueue, static (_, _) => true, () => Now), new WinSearchHistory());
        engine.Warm();
        while (work.TryDequeue(out var next))
        {
            next();
        }
        Assert.Equal(["mail"], engine.Home.For(null).Select(section => section.Info.Id));
        // Each asked for its most.
        Assert.Equal("most 12", engine.Home.Sections.Single(section => section.Info.Id == "mail").Hits[0].Name);
        engine.Dispose();
        Assert.True(forgotten > 0);
    }

    // --- The split -----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void FromFiveHundredSixtyDipALineIsWide(float scale)
    {
        var style = FolderFlyout.StyleFor(scale, Karpo.Tests.Folders.ListFormPictures.LookFor(dark: true), FlyoutTheme.For(Karpo.Tests.Folders.ListFormPictures.LookFor(dark: true)));
        Assert.False(WinSearchLine.Wide(559 * scale, style));
        Assert.True(WinSearchLine.Wide(560 * scale, style));
        Assert.False(WinSearchLine.Wide(280 * scale, style));
        Assert.True(WinSearchLine.Wide(900 * scale, style));
    }
}
