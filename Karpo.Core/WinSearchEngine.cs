// §642, §652: Windows search's engine: every keystroke a question to every source the line has switched on
// (IWinSearchSource, WinSearchSource.cs). The quick ones — what is in memory: the apps, Settings' pages, the calculator,
// the web's row, Karpo's mail, calendars, contacts, notes and itself — are asked together at once, off the dock's thread,
// and listed together, so the list does not jump as each answers; each slow one (Windows' index, its names and what files
// hold) on a thread of its own once the typing has rested (<see cref="WinSearchEngine.RestMs"/>): a word typed fast is one
// question, not one a letter. A keystroke cancels the question before (the provider's own Cancel; whatever comes late is
// dropped), so what is listed is always what is typed now; until the new answers come, the last stand.
//
// Every source is handed in (WinSearchSources): the dock's are Windows' and Karpo's (Karpo.Dock\WinSearch), a test's are
// its own, so no test asks the real index, lists the real apps, reads the person's accounts or waits on a clock.

namespace Karpo.Core;

/// <summary>§642: what Windows' index answered: its rows, or why it could not (null where it could).</summary>
public sealed record WinSearchIndexAnswer(IReadOnlyList<WinSearchRow> Rows, string? Problem = null)
{
    public static readonly WinSearchIndexAnswer Empty = new([]);
}

/// <summary>§642: Windows' index, asked with SQL (Search.CollatorDSO).</summary>
public interface IWinSearchIndex
{
    /// <summary>The rows <paramref name="sql"/> finds; cancelled by <paramref name="cancel"/>, which may end it early. Never throws.</summary>
    WinSearchIndexAnswer Query(string sql, CancellationToken cancel);
}

/// <summary>
/// §642: the apps of Start's All apps. An interface of its own so another widget listing the apps (§641's Start-like group)
/// can share one listing.
/// </summary>
public interface IWinSearchApps
{
    /// <summary>The apps, each once; may take a moment (the shell lists them); never throws.</summary>
    IReadOnlyList<WinSearchApp> List();
}

/// <summary>
/// §642, §652: what the engine asks and how it runs and waits: the sources (<see cref="IWinSearchSource"/>, Windows' and
/// Karpo's in the dock, a test's own in the tests), §655 the home's (<see cref="IWinSearchHomeSource"/>), the worker, the
/// rest's wait and the clock.
/// </summary>
/// <param name="All">Every source, in any order: the results are ranked, not listed by source.</param>
/// <param name="Home">§655: every home section's source, in any order: the person orders the sections (WinSearchHome.For).</param>
/// <param name="Run">Runs work off the caller's thread.</param>
/// <param name="Wait">Waits so many ms unless cancelled first: true where it waited out, false where cancelled.</param>
/// <param name="Now">The time.</param>
public sealed record WinSearchSources(
    IReadOnlyList<IWinSearchSource> All,
    IReadOnlyList<IWinSearchHomeSource> Home,
    Action<Action> Run,
    Func<int, CancellationToken, bool> Wait,
    Func<DateTime> Now)
{
    /// <summary>A wait on a real clock: the token's handle, so a keystroke ends it at once.</summary>
    public static bool Sleep(int ms, CancellationToken cancel) => !cancel.WaitHandle.WaitOne(ms);

    /// <summary>
    /// Windows' sources over <paramref name="index"/> and <paramref name="apps"/> — the apps, Settings' pages, the index's
    /// names and contents, the calculator in <paramref name="marks"/>, the web's row searching with <paramref name="web"/> —
    /// and <paramref name="more"/> (Karpo's own) after them; §655 the home's <paramref name="home"/>.
    /// </summary>
    public static WinSearchSources Windows(
        IWinSearchIndex index,
        IWinSearchApps apps,
        Action<Action> run,
        Func<int, CancellationToken, bool> wait,
        Func<DateTime> now,
        NumberMarks? marks = null,
        Func<string, string?>? web = null,
        IEnumerable<IWinSearchSource>? more = null,
        IEnumerable<IWinSearchHomeSource>? home = null) => new(
        [
            new WinSearchAppsSource(apps, run),
            new WinSearchSettingsSource(),
            new WinSearchIndexSource(index, contents: false, now),
            new WinSearchIndexSource(index, contents: true, now),
            new WinSearchCalculatorSource(marks),
            new WinSearchWebSource(web ?? (static _ => null)),
            .. more ?? [],
        ],
        [.. home ?? []],
        run,
        wait,
        now);
}

/// <summary>§642, §652: what one keystroke asks: the text, the chip chosen, and the line's switches (which sources it asks).</summary>
/// <param name="Typed">What is in the box.</param>
/// <param name="Chip">The chip chosen; null for All.</param>
/// <param name="Options">The line's switches; null for every source at its default.</param>
public readonly record struct WinSearchTyped(string Typed, WinSearchCategory? Chip = null, WinSearchOptions? Options = null);

/// <summary>§642, §652: the engine (see the top of the file).</summary>
public sealed class WinSearchEngine : IDisposable
{
    /// <summary>How long the typing rests before a slow source (the index) is asked, in ms.</summary>
    public const int RestMs = 120;

    /// <summary>The most rows the index is asked for.</summary>
    public const int MostRows = 60;

    private readonly WinSearchSources sources;
    private readonly WinSearchHistory history;
    private readonly Lock gate = new();

    private int generation;
    private CancellationTokenSource? asking;
    private WinSearchTyped last;
    private WinSearchResults latest = WinSearchResults.None;
    private bool warmed;
    private IReadOnlyList<WinSearchHomeSection> home = [];
    private IReadOnlyList<WinSearchOpened> opened = [];
    private int homeGeneration;
    private bool disposed;

    public WinSearchEngine(WinSearchSources sources, WinSearchHistory history)
    {
        this.sources = sources;
        this.history = history;
    }

    /// <summary>Raised from any thread whenever <see cref="Latest"/> or <see cref="Home"/> changed: the line is drawn again.</summary>
    public event Action? Changed;

    /// <summary>What the last question found so far.</summary>
    public WinSearchResults Latest
    {
        get
        {
            lock (gate)
            {
                return latest;
            }
        }
    }

    /// <summary>§655: the home: the searches made lately (from the history now) and every section as last listed.</summary>
    public WinSearchHome Home
    {
        get
        {
            lock (gate)
            {
                return new WinSearchHome([.. history.Searches], home);
            }
        }
    }

    private int slowAsked;

    /// <summary>How many questions the slow sources (the index) were asked, for the tests.</summary>
    public int IndexAsked => Volatile.Read(ref slowAsked);

    /// <summary>
    /// The line is shown: every source gets ready (the apps listed, Karpo's own read — on the caller's thread, each that
    /// reads for long off it), §655 every home section too, and the home is listed off it, again whenever a section says
    /// what it shows has changed; the line is drawn again as each comes. On the dock's thread (the history is read here).
    /// </summary>
    public void Warm()
    {
        WarmSources();
        lock (gate)
        {
            opened = [.. history.Opened];
        }
        foreach (var source in sources.Home)
        {
            Guard(() =>
            {
                source.Warm(ListHome);
                return true;
            }, false);
        }
        ListHome();
    }

    /// <summary>§655: every home section listed again, off the caller's thread, each its most (a section that fails lists nothing); the newest listing wins.</summary>
    private void ListHome()
    {
        int mine;
        IReadOnlyList<WinSearchOpened> lately;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            mine = ++homeGeneration;
            lately = opened;
        }
        sources.Run(() =>
        {
            var now = sources.Now();
            List<WinSearchHomeSection> listed = [];
            foreach (var source in sources.Home)
            {
                if (WinSearchHomeInfos.Of(source.Id) is not { } info)
                {
                    continue;
                }
                var hits = Guard(() => source.List(new WinSearchHomeAsk(info.Most, now, lately)), []);
                listed.Add(new WinSearchHomeSection(info, [.. hits.Take(info.Most)], []));
            }
            lock (gate)
            {
                if (disposed || mine != homeGeneration)
                {
                    return;
                }
                home = listed;
            }
            Changed?.Invoke();
        });
    }

    private void WarmSources()
    {
        lock (gate)
        {
            warmed = true;
        }
        foreach (var source in sources.All)
        {
            Guard(() =>
            {
                source.Warm(Again);
                return true;
            }, false);
        }
    }

    /// <summary>A source's reading came (the apps, late): what is typed is asked again, with it.</summary>
    private void Again()
    {
        WinSearchTyped again;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            again = last;
        }
        if (again.Typed is { Length: > 0 })
        {
            Ask(again);
        }
    }

    /// <summary>What the sources read is read again at the next <see cref="Warm"/> (apps installed or removed meanwhile); §655 the home's sections tell no more.</summary>
    public void Forget()
    {
        lock (gate)
        {
            warmed = false;
        }
        foreach (var source in sources.All)
        {
            source.Forget();
        }
        foreach (var source in sources.Home)
        {
            Guard(() =>
            {
                source.Forget();
                return true;
            }, false);
        }
    }

    /// <summary>The sources a question to <paramref name="typed"/> asks: switched on (<see cref="WinSearchSourceInfos.On"/>), answering its filter.</summary>
    public IReadOnlyList<IWinSearchSource> Asked(WinSearchTyped typed, WinSearchAsk ask) =>
        [.. sources.All.Where(source => WinSearchSourceInfos.On(typed.Options, source.Id) && (WinSearchSourceInfos.Of(source.Id)?.Answers(ask.Filter) ?? true))];

    /// <summary>
    /// A keystroke (see the top of the file): the quick sources asked at once, together, off the caller's thread — their
    /// answers listed as one (<see cref="Latest"/>, <see cref="Changed"/>), what was listed before standing meanwhile —; each
    /// slow one on its own once the typing has rested <see cref="RestMs"/>, listed with them as it answers. The question
    /// before is cancelled, and whatever of it comes late dropped.
    /// </summary>
    public void Ask(WinSearchTyped typed)
    {
        bool cold;
        lock (gate)
        {
            cold = !warmed;
        }
        if (cold)
        {
            // Typed before the line was warmed: the sources get ready, and what is typed is asked again once they are.
            WarmSources();
        }
        var ask = WinSearchQuery.Parse(typed.Typed, typed.Chip);
        CancellationTokenSource cancel;
        int mine;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            // Cancelled, never disposed: a worker waiting on it may still hold its token.
            asking?.Cancel();
            asking = cancel = new CancellationTokenSource();
            mine = ++generation;
            last = typed;
        }
        if (ask.Empty)
        {
            Publish(mine, new WinSearchResults { Ask = ask });
            return;
        }
        var asked = Asked(typed, ask);
        var quick = asked.Where(source => !source.Slow).ToList();
        var slow = asked.Where(source => source.Slow).ToList();
        var round = new Round(ask, slow.Count);
        var token = cancel.Token;
        sources.Run(() =>
        {
            foreach (var source in quick)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                round.Add(Find(source, ask, token));
            }
            if (!token.IsCancellationRequested)
            {
                Publish(mine, round.Quick());
            }
        });
        foreach (var source in slow)
        {
            sources.Run(() =>
            {
                if (!sources.Wait(RestMs, token) || token.IsCancellationRequested)
                {
                    return;
                }
                Interlocked.Increment(ref slowAsked);
                var found = Find(source, ask, token);
                if (!token.IsCancellationRequested && round.Slow(found) is { } results)
                {
                    Publish(mine, results);
                }
            });
        }
    }

    /// <summary>What <paramref name="source"/> found for <paramref name="ask"/>, each hit marked its and boosted by what was opened from here before; nothing where it failed.</summary>
    private WinSearchFound Find(IWinSearchSource source, WinSearchAsk ask, CancellationToken cancel)
    {
        var found = Guard(() => source.Find(ask, cancel), WinSearchFound.None);
        var typed = string.Join(' ', ask.Words);
        return found with { Hits = [.. found.Hits.Select(hit => hit with { Source = source.Id, Score = hit.Score > 0 ? hit.Score + history.Boost(hit.Key, typed) : hit.Score })] };
    }

    /// <summary>One question's answers as they come: the quick sources' together, then each slow one's; listed once the quick ones are in.</summary>
    private sealed class Round(WinSearchAsk ask, int slow)
    {
        private readonly Lock gate = new();
        private readonly List<WinSearchHit> hits = [];
        private string? problem;
        private int waiting = slow;
        private bool quick;

        public void Add(WinSearchFound found)
        {
            lock (gate)
            {
                hits.AddRange(found.Hits);
                problem ??= found.Problem;
            }
        }

        /// <summary>The quick sources have all answered: what is in so far, the slow ones still being asked where any is.</summary>
        public WinSearchResults Quick()
        {
            lock (gate)
            {
                quick = true;
                return Results();
            }
        }

        /// <summary>A slow source answered: what is in now, where the quick ones are in; else null, theirs listing it.</summary>
        public WinSearchResults? Slow(WinSearchFound found)
        {
            lock (gate)
            {
                hits.AddRange(found.Hits);
                problem ??= found.Problem;
                waiting--;
                return quick ? Results() : null;
            }
        }

        private WinSearchResults Results() => WinSearchRank.Group(ask, [.. hits], searching: waiting > 0, problem: problem);
    }

    /// <summary><paramref name="results"/> are what is listed now, where question <paramref name="mine"/> is still the last.</summary>
    private void Publish(int mine, WinSearchResults results)
    {
        lock (gate)
        {
            if (mine != generation || disposed)
            {
                return;
            }
            latest = results;
        }
        Changed?.Invoke();
    }

    /// <summary><paramref name="read"/>'s answer, or <paramref name="otherwise"/> where it threw: a source that fails answers nothing.</summary>
    private static T Guard<T>(Func<T> read, T otherwise)
    {
        try
        {
            return read();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Report("Windows search: a source", error); // not-ui: a trouble's name
            return otherwise;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            asking?.Cancel();
            asking = null;
        }
        Forget();
    }
}
