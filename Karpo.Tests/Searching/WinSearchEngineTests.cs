// §642, §652: Windows search's engine over a fake index, fake apps and a hand-turned clock: the quick sources answer
// together at once, the index (names, and contents) only once the typing rests and once for a word typed fast, a keystroke
// cancels the question before (its late answer dropped), the index's problem said, apps that come late answer what was
// typed, a source switched off is not asked, one that throws answers nothing; and the history kept on this PC (searches,
// what was opened) written and read back. No test asks the real index or lists the real apps.

using System.Collections.Concurrent;

namespace Karpo.Tests.Searching;

public sealed class WinSearchEngineTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>An index that answers what it was given, remembers every question, and may hold an answer back until told.</summary>
    private sealed class FakeIndex : IWinSearchIndex
    {
        public readonly List<string> Asked = [];
        public IReadOnlyList<WinSearchRow> Rows = [];
        public string? Problem;
        public Action<CancellationToken>? During;

        public WinSearchIndexAnswer Query(string sql, CancellationToken cancel)
        {
            lock (Asked)
            {
                Asked.Add(sql);
            }
            During?.Invoke(cancel);
            return new WinSearchIndexAnswer(Rows, Problem);
        }
    }

    private sealed class FakeApps(params WinSearchApp[] apps) : IWinSearchApps
    {
        public int Listed;

        public IReadOnlyList<WinSearchApp> List()
        {
            Listed++;
            return apps;
        }
    }

    /// <summary>Work handed off, run when the test says (<see cref="Pump"/>); waits answered by <see cref="Rests"/>.</summary>
    private sealed class Hands
    {
        public readonly ConcurrentQueue<Action> Work = new();
        public readonly List<int> Waits = [];
        public bool Rests = true;

        public void Pump()
        {
            while (Work.TryDequeue(out var work))
            {
                work();
            }
        }

        /// <summary>The first work handed off, alone.</summary>
        public void PumpOne()
        {
            if (Work.TryDequeue(out var work))
            {
                work();
            }
        }

        public bool Wait(int ms, CancellationToken cancel)
        {
            Waits.Add(ms);
            return Rests && !cancel.IsCancellationRequested;
        }
    }

    private static (WinSearchEngine Engine, FakeIndex Index, FakeApps Apps, Hands Hands) Made(WinSearchHistory? history = null, params WinSearchApp[] apps)
    {
        var index = new FakeIndex();
        var listed = new FakeApps(apps.Length > 0 ? apps : [new("Paint", @"shell:AppsFolder\Paint"), new("Visual Studio Code", @"shell:AppsFolder\Code")]);
        var hands = new Hands();
        string[] recent = [@"C:\Users\me\Documents\plan.docx", @"C:\Users\me\Pictures\cat.png"];
        var sources = WinSearchSources.Windows(index, listed, hands.Work.Enqueue, hands.Wait, () => Now,
            web: static text => "https://example.com/?q=" + text,
            home: [new WinSearchHomeSource(WinSearchHomeInfos.Files, ask => [.. recent.Take(ask.Most).Select(path => WinSearchHomeHits.OfPath(path, null, folder: false))])]);
        return (new WinSearchEngine(sources, history ?? new WinSearchHistory()), index, listed, hands);
    }

    private static readonly WinSearchRow Report = new("report.docx", @"C:\Users\me\Documents\report.docx", "file:C:/Users/me/Documents/report.docx", ".docx", Now, 1000, 500);

    [Fact]
    public void WhatIsInMemoryAnswersAtOnceTheIndexOnceTheTypingRests()
    {
        var (engine, index, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        index.Rows = [Report];
        var changes = 0;
        engine.Changed += () => changes++;
        engine.Ask(new WinSearchTyped("pa"));
        // The quick sources, together, off the caller's thread: the app, nothing of the index yet, the index still being asked.
        hands.PumpOne();
        Assert.Equal("Paint", engine.Latest.Best?.Name);
        Assert.Equal(WinSearchSourceInfos.Apps, engine.Latest.Best?.Source);
        Assert.True(engine.Latest.Searching);
        Assert.Empty(index.Asked);
        hands.Pump();
        // The index's two sources each waited for the typing to rest; contents need a word of three letters, so one question.
        Assert.Equal([WinSearchEngine.RestMs, WinSearchEngine.RestMs], hands.Waits);
        Assert.Single(index.Asked);
        Assert.False(engine.Latest.Searching);
        Assert.Contains(engine.Latest.All, hit => hit.Name == "report.docx" && hit.Category == WinSearchCategory.Documents && hit.Source == WinSearchSourceInfos.Files);
        Assert.Equal(3, changes);
    }

    /// <summary>A quick source that counts its questions and the threads they came on.</summary>
    private sealed class Counted : IWinSearchSource
    {
        public readonly ConcurrentBag<int> Threads = [];

        public string Id => WinSearchSourceInfos.Notes;

        public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel)
        {
            Threads.Add(Environment.CurrentManagedThreadId);
            return WinSearchFound.None;
        }
    }

    /// <summary>
    /// §658: the line shown and its first question asked on the dock's thread do nothing slow there: the apps listed, every
    /// source asked, the index questioned and the home listed, all handed to the worker; the dock's thread returns at once,
    /// what is found listed as it comes.
    /// </summary>
    [Fact]
    public void TheFirstQuestionRunsNothingOnTheCallersThread()
    {
        var index = new FakeIndex { Rows = [Report] };
        var apps = new FakeApps(new WinSearchApp("Paint", @"shell:AppsFolder\Paint"));
        var hands = new Hands();
        var counted = new Counted();
        var homeListed = 0;
        var sources = WinSearchSources.Windows(index, apps, hands.Work.Enqueue, hands.Wait, () => Now, more: [counted],
            home: [new WinSearchHomeSource(WinSearchHomeInfos.Files, _ => { homeListed++; return []; })]);
        using var engine = new WinSearchEngine(sources, new WinSearchHistory());
        var caller = Environment.CurrentManagedThreadId;

        engine.Warm();
        engine.Ask(new WinSearchTyped("pa"));
        Assert.Equal(0, apps.Listed);
        Assert.Empty(counted.Threads);
        Assert.Empty(index.Asked);
        Assert.Equal(0, homeListed);
        Assert.Null(engine.Latest.Best);

        var worker = new Thread(hands.Pump);
        worker.Start();
        Assert.True(worker.Join(TimeSpan.FromSeconds(30)));
        Assert.Equal(1, apps.Listed);
        Assert.DoesNotContain(caller, counted.Threads);
        Assert.NotEmpty(index.Asked);
        Assert.Equal(1, homeListed);
    }

    [Fact]
    public void AWordTypedFastIsOneQuestionToTheIndex()
    {
        var (engine, index, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        foreach (var typed in new[] { "r", "re", "rep", "repo" })
        {
            engine.Ask(new WinSearchTyped(typed));
        }
        hands.Pump();
        // Each keystroke cancelled the wait before it: only the last one's question was asked, of the names and of the contents.
        Assert.Equal(2, engine.IndexAsked);
        Assert.Equal(2, index.Asked.Count);
        Assert.All(index.Asked, sql => Assert.Contains("\"repo*\"", sql, StringComparison.Ordinal));
        Assert.Single(index.Asked, sql => sql.Contains("System.Search.Contents", StringComparison.Ordinal));
    }

    [Fact]
    public void AKeystrokeCancelsTheQuestionBeforeAndItsLateAnswerIsDropped()
    {
        var (engine, index, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        index.Rows = [Report];
        CancellationToken seen = default;
        index.During = cancel =>
        {
            seen = cancel;
            // The person types on while the index is answering the first question.
            index.During = null;
            index.Rows = [];
            engine.Ask(new WinSearchTyped("paint"));
        };
        engine.Ask(new WinSearchTyped("report"));
        hands.Pump();
        Assert.True(seen.IsCancellationRequested);
        Assert.Equal("paint", engine.Latest.Ask.Text);
        Assert.DoesNotContain(engine.Latest.All, hit => hit.Name == "report.docx");
        Assert.Equal("Paint", engine.Latest.Best?.Name);
    }

    [Fact]
    public void WhyTheIndexCouldNotAnswerIsSaidAndTheRestStillAnswers()
    {
        var (engine, index, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        index.Problem = "off";
        engine.Ask(new WinSearchTyped("paint"));
        hands.Pump();
        Assert.Equal("off", engine.Latest.Problem);
        Assert.Equal("Paint", engine.Latest.Best?.Name);
    }

    [Fact]
    public void TheIndexIsNotAskedForAppsSettingsTheWebOrNothing()
    {
        var (engine, index, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        foreach (var typed in new[] { "apps: paint", "settings: display", "web: weather", "", "   " })
        {
            engine.Ask(new WinSearchTyped(typed));
            hands.Pump();
        }
        Assert.Empty(index.Asked);
        Assert.True(engine.Latest.Nothing);
        engine.Ask(new WinSearchTyped("display", Chip: WinSearchCategory.Settings));
        hands.Pump();
        Assert.Equal(WindowsPages.Display.Uri, engine.Latest.Best?.Target);
        // §652: a page wears Settings' own icon.
        Assert.Equal(WinSearchPages.SettingsApp, engine.Latest.Best?.Icon);
        Assert.False(engine.Latest.Searching);
    }

    [Fact]
    public void TheAnswerAndTheWebsRowWhereAsked()
    {
        var (engine, _, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        engine.Ask(new WinSearchTyped("12*3"));
        hands.Pump();
        Assert.Equal(("= 36", "36"), (engine.Latest.Best?.Name, engine.Latest.Best?.Target));
        // §652: the web's row a source like any, off by default; switched on, its row.
        engine.Ask(new WinSearchTyped("weather"));
        hands.Pump();
        Assert.DoesNotContain(engine.Latest.All, hit => hit.Category == WinSearchCategory.Web);
        var on = new WinSearchOptions();
        on.Set(WinSearchSourceInfos.Web, true);
        engine.Ask(new WinSearchTyped("weather", Options: on));
        hands.Pump();
        var web = Assert.Single(engine.Latest.All, hit => hit.Category == WinSearchCategory.Web);
        Assert.Equal("https://example.com/?q=weather", web.Target);
        engine.Ask(new WinSearchTyped("weather", Chip: WinSearchCategory.Web, Options: on));
        hands.Pump();
        Assert.Equal(WinSearchCategory.Web, engine.Latest.Best?.Category);
        // The calculator switched off: no answer, whatever is typed.
        var noCalculator = new WinSearchOptions();
        noCalculator.Set(WinSearchSourceInfos.Calculator, false);
        engine.Ask(new WinSearchTyped("12*3", Options: noCalculator));
        hands.Pump();
        Assert.DoesNotContain(engine.Latest.All, hit => hit.Category == WinSearchCategory.Answer);
    }

    [Fact]
    public void AppsThatComeLateAnswerWhatWasTypedMeanwhile()
    {
        var (engine, _, apps, hands) = Made();
        engine.Ask(new WinSearchTyped("code"));
        // The apps are not listed yet.
        Assert.Null(engine.Latest.Best);
        hands.Pump();
        Assert.Equal(1, apps.Listed);
        Assert.Equal("Visual Studio Code", engine.Latest.Best?.Name);
        // Listed once a while, not a keystroke.
        engine.Ask(new WinSearchTyped("paint"));
        hands.Pump();
        Assert.Equal(1, apps.Listed);
    }

    [Fact]
    public void TheHomeIsTheSearchesMadeLatelyAndEverySectionAsListedOffTheThread()
    {
        var history = new WinSearchHistory();
        history.Searched("budget");
        var (engine, _, _, hands) = Made(history);
        engine.Warm();
        Assert.Empty(engine.Home.Sections);
        hands.Pump();
        var home = engine.Home;
        Assert.Equal(["budget"], home.Searches);
        var files = Assert.Single(home.Sections);
        Assert.Equal(WinSearchHomeInfos.Files, files.Info.Id);
        Assert.Equal([("plan.docx", WinSearchCategory.Documents), ("cat.png", WinSearchCategory.Photos)], files.Hits.Select(hit => (hit.Name, hit.Category)));
        Assert.Equal(@"C:\Users\me\Documents", files.Hits[0].Detail);
        history.Searched("plan");
        Assert.Equal(["plan", "budget"], engine.Home.Searches);
    }

    [Fact]
    public void WhatWasOpenedComesFirstNextTime()
    {
        var history = new WinSearchHistory();
        var (engine, index, _, hands) = Made(history, new WinSearchApp("Paint", @"shell:AppsFolder\Paint"));
        engine.Warm();
        hands.Pump();
        var painting = new WinSearchRow("Paintings", @"C:\Users\me\Paintings", "file:C:/Users/me/Paintings", "Directory", Now.AddYears(-2));
        index.Rows = [painting];
        engine.Ask(new WinSearchTyped("pain"));
        hands.Pump();
        Assert.Equal("Paint", engine.Latest.Best?.Name);
        history.Open(engine.Latest.All.Single(hit => hit.Name == "Paintings"), "pain", Now);
        engine.Ask(new WinSearchTyped("pain"));
        hands.Pump();
        Assert.Equal("Paintings", engine.Latest.Best?.Name);
    }

    [Fact]
    public void AnEngineDisposedAnswersNothingMore()
    {
        var (engine, _, _, hands) = Made();
        engine.Warm();
        hands.Pump();
        engine.Ask(new WinSearchTyped("report"));
        engine.Dispose();
        var changed = false;
        engine.Changed += () => changed = true;
        hands.Pump();
        engine.Ask(new WinSearchTyped("paint"));
        Assert.False(changed);
    }

    [Fact]
    public void TheHistoryIsWrittenAndReadBackAndHeldShort()
    {
        var history = new WinSearchHistory();
        for (var i = 0; i < 12; i++)
        {
            history.Searched($"search {i}");
        }
        history.Searched("SEARCH 3");
        Assert.Equal(WinSearchHistory.MostSearches, history.Searches.Count);
        Assert.Equal("SEARCH 3", history.Searches[0]);
        Assert.Single(history.Searches, each => each.Equals("search 3", StringComparison.OrdinalIgnoreCase));
        var hit = new WinSearchHit(WinSearchCategory.Documents, "a\tb.txt", @"C:\a b.txt");
        history.Open(hit, "a\nb", Now);
        history.Open(new WinSearchHit(WinSearchCategory.Answer, "= 2", "2"), "1+1", Now);
        var read = WinSearchHistory.Read(history.Write() + "o\tNope\tx\ty\tz\t1\nrubbish\n\no\tApps\t\tname\tq\t5\n");
        Assert.Equal(history.Searches, read.Searches);
        var opened = Assert.Single(read.Opened);
        Assert.Equal(new WinSearchOpened(WinSearchCategory.Documents, @"C:\a b.txt", "a b.txt", "a b", Now), opened);
        read.Forget("1+1");
        Assert.DoesNotContain("1+1", read.Searches);
        read.Clear();
        Assert.Empty(read.Searches);
        Assert.Empty(read.Opened);
    }

    [Fact]
    public void TheHistorysFileIsThisPCsAndReadsBackEmptyWhereThereIsNone()
    {
        var folder = Directory.CreateTempSubdirectory("karpo-winsearch-");
        try
        {
            var path = Path.Combine(folder.FullName, "winsearch.txt");
            Assert.Empty(WinSearchHistory.Load(path).Searches);
            var history = new WinSearchHistory();
            history.Searched("tax");
            history.Save(path);
            Assert.Equal(["tax"], WinSearchHistory.Load(path).Searches);
            Assert.StartsWith(Config.LocalFolder, WinSearchHistory.Path, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
