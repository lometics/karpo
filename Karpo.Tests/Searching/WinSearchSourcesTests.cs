// §652: Windows search's sources — one interface, one list of what they are (WinSearchSourceInfos), the line's switches
// ("Search in", WinSearchOptions) — and Karpo's own over made-up data: the mail its accounts synced, its calendars'
// events, its contacts, its notes and Karpo itself (its settings, the widgets to add, the layouts). The registry is the
// one list, every source the dock builds in it; a source switched off is never asked, one that throws answers nothing;
// the hits group by what they are, ranked, the best on top; each Karpo source matches by the matcher its data has.
// Nothing here reads the person's accounts, notebook or index.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.WinSearch;

namespace Karpo.Tests.Searching;

public sealed class WinSearchSourcesTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static WinSearchAsk Ask(string typed) => WinSearchQuery.Parse(typed);

    /// <summary>A source that answers what it is given, counting how often it was asked, or throws.</summary>
    private sealed class Fake(string id, bool slow = false, params WinSearchHit[] hits) : IWinSearchSource
    {
        public int Asked;
        public bool Throws;

        public string Id => id;

        public bool Slow => slow;

        public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel)
        {
            Asked++;
            return Throws ? throw new InvalidOperationException("broken") : new WinSearchFound(hits);
        }
    }

    private static (WinSearchEngine Engine, Action Pump) Engine(params IWinSearchSource[] sources)
    {
        var work = new Queue<Action>();
        var engine = new WinSearchEngine(new WinSearchSources(sources, [], work.Enqueue, static (_, _) => true, () => Now), new WinSearchHistory());
        return (engine, () =>
        {
            while (work.TryDequeue(out var next))
            {
                next();
            }
        });
    }

    // --- The registry and the switches ------------------------------------------------------------------

    [Fact]
    public void TheRegistryIsTheOneListOfSourcesEveryOneTheDockBuildsInIt()
    {
        var ids = WinSearchSourceInfos.All.Select(info => info.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            [WinSearchSourceInfos.Apps, WinSearchSourceInfos.Settings, WinSearchSourceInfos.Files, WinSearchSourceInfos.Contents, WinSearchSourceInfos.Mail,
             WinSearchSourceInfos.Calendar, WinSearchSourceInfos.Contacts, WinSearchSourceInfos.Notes, WinSearchSourceInfos.Karpo,
             WinSearchSourceInfos.Calculator, WinSearchSourceInfos.Web],
            ids);
        // Each with a name and a glyph of its own, every one on by default but the web's (nothing typed leaves the PC unasked).
        Assert.All(WinSearchSourceInfos.All, info =>
        {
            Assert.False(string.IsNullOrWhiteSpace(info.Title));
            Assert.NotEqual(info.Id, info.Title);
            Assert.NotEmpty(info.Glyph);
            Assert.NotEmpty(info.Categories);
            Assert.Equal(info.Id != WinSearchSourceInfos.Web, info.On);
        });
        // The dock's line builds exactly these, each once — nothing read: the stores and the index are asked only on a search.
        var built = WinSearchSystem.Sources(static () => new Config(), static _ => null).All.Select(source => source.Id).ToList();
        Assert.Equal(ids.Order(StringComparer.Ordinal), built.Order(StringComparer.Ordinal));
        // So do a ghost's samples.
        Assert.Equal(ids.Order(StringComparer.Ordinal), WinSearchSamples.Sources(new Config()).All.Select(source => source.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheSwitchesWriteOnlyWhatIsNotTheDefault()
    {
        var options = new WinSearchOptions();
        Assert.True(options.On(WinSearchSourceInfos.Mail));
        Assert.False(options.On(WinSearchSourceInfos.Web));
        options.Set(WinSearchSourceInfos.Mail, false);
        options.Set(WinSearchSourceInfos.Web, true);
        Assert.Equal(new Dictionary<string, bool> { ["mail"] = false, ["web"] = true }, options.Sources);
        options.Set(WinSearchSourceInfos.Mail, true);
        Assert.Equal(new Dictionary<string, bool> { ["web"] = true }, options.Sources);
        options.Set(WinSearchSourceInfos.Web, false);
        Assert.Null(options.Sources);
        // A word no source has, or a default said, is dropped as the file is read.
        var read = new WinSearchOptions { Sources = new() { ["nope"] = false, ["apps"] = true, ["notes"] = false } };
        read.Normalize();
        Assert.Equal(new Dictionary<string, bool> { ["notes"] = false }, read.Sources);
        // A source of no info (a test's own) is always asked.
        Assert.True(WinSearchSourceInfos.On(read, "mine"));
    }

    [Fact]
    public void SearchInIsOneToggleASourceInTheirOrderWritingTheConfig()
    {
        var item = ListKind.WinSearch.New();
        item.Normalize();
        var rows = OptionRows.Of(item).Where(row => row.Key.StartsWith(OptionRows.WinSearchSource, StringComparison.Ordinal)).ToList();
        Assert.Equal(WinSearchSourceInfos.All.Select(info => OptionRows.WinSearchSource + info.Id), rows.Select(row => row.Key));
        Assert.All(rows, row =>
        {
            Assert.Equal(OptionRowKind.Toggle, row.Kind);
            Assert.Equal(WinSearchWords.SearchIn, row.Group);
        });
        Assert.Equal(WinSearchSourceInfos.All.Select(info => info.On), rows.Select(row => row.On));
        // Mail off: written, and so in the file; on again: nothing written, no record.
        Assert.True(rows.Single(row => row.Key == OptionRows.WinSearchSource + "mail").Set!(0));
        Assert.Equal(new Dictionary<string, bool> { ["mail"] = false }, item.WinSearch?.Sources);
        var config = new Config();
        var json = System.Text.Json.JsonSerializer.Serialize(item.WinSearch!, ConfigJson.Default.WinSearchOptions);
        Assert.Equal("""{"sources":{"mail":false}}""", string.Concat(json.Where(c => !char.IsWhiteSpace(c))));
        rows = [.. OptionRows.Of(item).Where(row => row.Key.StartsWith(OptionRows.WinSearchSource, StringComparison.Ordinal))];
        Assert.False(rows.Single(row => row.Key == OptionRows.WinSearchSource + "mail").On);
        Assert.True(rows.Single(row => row.Key == OptionRows.WinSearchSource + "mail").Set!(1));
        Assert.Null(item.WinSearch);
        _ = config;
    }

    // --- The engine over sources -------------------------------------------------------------------------

    [Fact]
    public void ASourceSwitchedOffIsNotAskedOneThatThrowsAnswersNothing()
    {
        var notes = new Fake(WinSearchSourceInfos.Notes, false, new WinSearchHit(WinSearchCategory.Notes, "Team ideas", "karpo:note/1") { Score = 900 });
        var broken = new Fake(WinSearchSourceInfos.Mail) { Throws = true };
        var (engine, pump) = Engine(notes, broken);
        engine.Ask(new WinSearchTyped("team"));
        pump();
        Assert.Equal("Team ideas", engine.Latest.Best?.Name);
        Assert.Equal(1, broken.Asked);
        var off = new WinSearchOptions();
        off.Set(WinSearchSourceInfos.Notes, false);
        engine.Ask(new WinSearchTyped("team", Options: off));
        pump();
        Assert.Equal(1, notes.Asked);
        Assert.True(engine.Latest.Nothing);
        // A chip of a group a source does not answer in does not ask it.
        engine.Ask(new WinSearchTyped("team", Chip: WinSearchCategory.Apps));
        pump();
        Assert.Equal(1, notes.Asked);
    }

    [Fact]
    public void HitsOfEverySourceGroupByWhatTheyAreRankedTheBestOnTop()
    {
        var (engine, pump) = Engine(
            new Fake(WinSearchSourceInfos.Calendar, false,
                new WinSearchHit(WinSearchCategory.Events, "Team check-in", "https://cal/1") { Key = "e1", Score = 920 },
                new WinSearchHit(WinSearchCategory.Events, "Team offsite", "https://cal/2") { Key = "e2", Score = 700 }),
            new Fake(WinSearchSourceInfos.Mail, false,
                new WinSearchHit(WinSearchCategory.Email, "Team lunch", "") { Key = "m1", Score = 800 },
                // Two messages of one inbox's page: two hits, by their keys.
                new WinSearchHit(WinSearchCategory.Email, "Team agenda", "") { Key = "m2", Score = 850 }),
            new Fake(WinSearchSourceInfos.Karpo, false, new WinSearchHit(WinSearchCategory.Karpo, "Meeting", "karpo:add/Meeting") { Score = 400 }),
            new Fake("slow", true, new WinSearchHit(WinSearchCategory.Documents, "Team.docx", @"C:\Team.docx") { Score = 990 }));
        engine.Ask(new WinSearchTyped("team"));
        pump();
        var results = engine.Latest;
        Assert.Equal("Team.docx", results.Best?.Name);
        Assert.Equal([WinSearchCategory.Email, WinSearchCategory.Events, WinSearchCategory.Karpo], results.Groups.Select(group => group.Category));
        Assert.Equal(["Team agenda", "Team lunch"], results.Groups[0].Hits.Select(hit => hit.Name));
        Assert.Equal(["Team check-in", "Team offsite"], results.Groups[1].Hits.Select(hit => hit.Name));
        Assert.Equal(WinSearchSourceInfos.Mail, results.Groups[0].Hits[0].Source);
    }

    // --- Karpo's own -------------------------------------------------------------------------------------

    [Fact]
    public void MailIsFoundBySubjectSenderAndWhatItSays()
    {
        var received = new DateTimeOffset(Now.AddHours(-2));
        IReadOnlyList<MailItem> inbox =
        [
            new("1", "Anna Keller", "anna@example.com", "Offsite agenda", "Rooms for Thursday", received, Unread: true, Link: "https://outlook.office.com/mail/1") { Part = "a@x/inbox" },
            new("1", "Sam Weber", "sam@example.com", "Lunch?", "Shall we meet at the offsite café", received.AddDays(-3), Unread: false, Link: "https://mail.example.com/inbox") { Part = "b@y/inbox" },
            new("2", "GitHub", "noreply@github.com", "Pull request approved", null, received, Unread: false, Link: "https://mail.example.com/inbox") { Part = "b@y/inbox" },
        ];
        var source = new WinSearchMailSource(() => inbox, () => Now);
        var found = source.Find(Ask("offsite"), default).Hits.OrderByDescending(hit => hit.Score).ToList();
        // The subject's word before a preview's; one message per hit, though two share their inbox's page.
        Assert.Equal(["Offsite agenda", "Lunch?"], found.Select(hit => hit.Name));
        Assert.All(found, hit => Assert.Equal(WinSearchCategory.Email, hit.Category));
        Assert.Equal("https://outlook.office.com/mail/1", found[0].Target);
        Assert.NotEqual(found[0].Key, found[1].Key);
        Assert.Equal("anna@example.com", found[0].Copies);
        Assert.Null(found[0].Icon);
        // By the sender; a preview only answers words of three letters.
        Assert.Equal("Lunch?", Assert.Single(source.Find(Ask("sam"), default).Hits).Name);
        Assert.Empty(source.Find(Ask("ca"), default).Hits);
    }

    [Fact]
    public void PeopleAreFoundAsTheContactsLineFindsThem()
    {
        IReadOnlyList<Person> people =
        [
            new("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com"], ["+41 79 123 45 67"]),
            new("b", "Beatrice Hagen", null, [], ["+41 44 555 01 02"]),
            new("c", "Hagar Smith", null, ["hagar@example.com"], []),
        ];
        var source = new WinSearchContactsSource(() => people);
        var found = source.Find(Ask("hag"), default).Hits.OrderByDescending(hit => hit.Score).ToList();
        // The name's start first, then a later word's; as ContactSearch orders them.
        Assert.Equal("Hagar Smith", found[0].Name);
        Assert.Equal(3, found.Count);
        Assert.Equal("mailto:hagar@example.com", found[0].Target);
        Assert.Equal("hagar@example.com", found[0].Copies);
        Assert.StartsWith("tel:", found.Single(hit => hit.Name == "Beatrice Hagen").Target, StringComparison.Ordinal);
        // A number by its digits, an organization by its name.
        Assert.Equal("Adrian Hager", Assert.Single(source.Find(Ask("123 45"), default).Hits).Name);
        Assert.Equal("Adrian Hager", Assert.Single(source.Find(Ask("contoso"), default).Hits).Name);
    }

    [Fact]
    public void EventsAreFoundByTitleAndPlaceTheNearestFirst()
    {
        var now = new DateTime(2026, 11, 9, 9, 0, 0);
        CalendarEvent At(double days, string title, string? where = null) => new(now.AddDays(days), now.AddDays(days).AddHours(1), false, title, where);
        IReadOnlyList<CalendarEvent> events = [At(40, "Team offsite"), At(1, "Team offsite"), At(-3, "Retro", "Team room"), At(2, "Dentist")];
        DateTime from = default, to = default;
        var source = new WinSearchCalendarSource((a, b) =>
        {
            (from, to) = (a, b);
            return events;
        }, static (item, day) => "https://calendar.example.com/" + day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), () => now);
        var found = source.Find(Ask("team"), default).Hits.OrderByDescending(hit => hit.Score).ToList();
        Assert.Equal(["Team offsite", "Team offsite", "Retro"], found.Select(hit => hit.Name));
        // Of two named alike, tomorrow's before next month's; each its own, its day's address.
        Assert.Equal("https://calendar.example.com/2026-11-10", found[0].Target);
        Assert.NotEqual(found[0].Key, found[1].Key);
        Assert.All(found, hit => Assert.Equal(WinSearchCategory.Events, hit.Category));
        Assert.Equal((now.Date.AddDays(-31), now.Date.AddDays(366)), (from, to));
        Assert.Equal(20, WinSearchCalendarSource.Soon(TimeSpan.FromDays(1)));
        Assert.Equal(5, WinSearchCalendarSource.Soon(TimeSpan.FromDays(-1)));
        Assert.Equal(0, WinSearchCalendarSource.Soon(TimeSpan.FromDays(-30)));
    }

    [Fact]
    public void NotesAreFoundByTitleThenByWhatTheySay()
    {
        IReadOnlyList<Note> notes =
        [
            new() { Id = "1", Title = "Groceries", Body = "# List\n- oat milk\n- bread" },
            new() { Id = "2", Title = "Milk recipes", Body = "" },
        ];
        var source = new WinSearchNotesSource(() => notes);
        var found = source.Find(Ask("milk"), default).Hits.OrderByDescending(hit => hit.Score).ToList();
        Assert.Equal(["Milk recipes", "Groceries"], found.Select(hit => hit.Name));
        Assert.Equal(KarpoTargets.Note("2"), found[0].Target);
        Assert.Null(found[0].Copies);
        Assert.Equal("List", found[1].Detail);
        Assert.Equal("# List\n- oat milk\n- bread", found[1].Copies);
        Assert.Equal("oat milk", WinSearchNotesSource.FirstLine("\n  - oat milk"));
        Assert.Null(WinSearchNotesSource.FirstLine(" \n#"));
    }

    [Fact]
    public void KarpoItselfIsItsSettingsTheWidgetsToAddAndTheLayouts()
    {
        var config = new Config();
        config.Layouts.Add(new TaskbarLayout { Id = "work", Name = "Work desk" });
        var finds = WinSearchKarpoSource.Of(config);
        var source = new WinSearchKarpoSource(() => finds);
        source.Warm(static () => { });
        // A page of its settings, by its name.
        var appearance = source.Find(Ask(PanelRows.SideOf(PanelPage.Appearance)), default).Hits.MaxBy(hit => hit.Score);
        Assert.Equal(KarpoTargets.Settings(PanelPage.Appearance), appearance?.Target);
        Assert.Equal(WinSearchCategory.Karpo, appearance?.Category);
        // A widget by its search words, as the add page finds it: "internet" is Wi-Fi's.
        var wifi = WidgetInfos.All.First(info => info.Id == "wifi").Title;
        Assert.Contains(source.Find(Ask("internet"), default).Hits, hit => hit.Name == wifi && hit.Target == KarpoTargets.Add(wifi));
        // A layout by its name, this display switched to it.
        Assert.Contains(source.Find(Ask("work desk"), default).Hits, hit => hit.Name == "Work desk" && hit.Target == KarpoTargets.Layout(config.Layouts.Count - 1));
        // Every General row with words is found by them, landing on the General page.
        Assert.All(KarpoMenu.Rows(config, new KarpoServices(null, static () => { })).Where(row => row.Kind != OptionRowKind.Heading && row.Title.Length > 0),
            row => Assert.Contains(finds, find => find.Name == row.Title && find.Target.StartsWith(KarpoTargets.Settings(PanelPage.Karpo), StringComparison.Ordinal)));
    }

    [Fact]
    public void KarposAddressesReadBackAsTheyWereWritten()
    {
        Assert.Equal(new KarpoTarget(KarpoTargetKind.Settings, PanelPage.Appearance), KarpoTargets.Parse(KarpoTargets.Settings(PanelPage.Appearance)));
        Assert.Equal(new KarpoTarget(KarpoTargetKind.Settings, PanelPage.Karpo, "Pro/Status"), KarpoTargets.Parse(KarpoTargets.Settings(PanelPage.Karpo, "Pro/Status")));
        Assert.Equal(new KarpoTarget(KarpoTargetKind.Add, Value: "Wi-Fi / 2"), KarpoTargets.Parse(KarpoTargets.Add("Wi-Fi / 2")));
        Assert.Equal(new KarpoTarget(KarpoTargetKind.Layout, Value: "3"), KarpoTargets.Parse(KarpoTargets.Layout(3)));
        Assert.Equal(new KarpoTarget(KarpoTargetKind.Note, Value: "n/1"), KarpoTargets.Parse(KarpoTargets.Note("n/1")));
        Assert.Null(KarpoTargets.Parse("karpo:settings/Nope"));
        Assert.Null(KarpoTargets.Parse("karpo:layout/x"));
        Assert.Null(KarpoTargets.Parse(@"C:\karpo:note"));
        Assert.Null(KarpoTargets.Parse(null));
    }

    // --- The fixed height ----------------------------------------------------------------------------------

    [Fact]
    public void TheLineStandsAtOneHeightWhateverItFinds()
    {
        var line = ListWidgets.Of(ListKind.WinSearch);
        Assert.False(line.Grows);
        Assert.Equal(FolderOptions.MostCells, line.Cells);
        Assert.Equal(LineHeight.Exactly(FolderOptions.MostCells), LineHeight.StartFor(ListKind.WinSearch));
        // The ready-made group's line, and a file's automatic height from before, read as that one height.
        var preset = Presets.Find(Presets.WinSearchId)!;
        Assert.Equal(LineHeight.Exactly(FolderOptions.MostCells), Assert.Single(preset.Items).Height);
        Assert.Equal(LineHeight.Exactly(FolderOptions.MostCells), LineHeight.Automatic(FolderOptions.MostCells).Within(ListKind.WinSearch));
        Assert.Equal(LineHeight.Exactly(10), LineHeight.Exactly(10).Within(ListKind.WinSearch));
    }
}
