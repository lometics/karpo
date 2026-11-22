// sports: a team's or a league's matches from ESPN (docs\widgets.md §239, §369), fetched once per team
// or league however many sports widgets show it, as the air's store does for a place (AirQualityStore):
// its users are islands — a folder wearing the sports face, for each of its sports lines, and each sports
// line — keyed by what they follow (SportsOptions.Key), so a face and a line of one team are one fetch
// and read one answer.
//
// Nothing is asked until a widget is looked at with a team or a league. When the next fetch is due is
// the answer's own (SportsPace): every half minute while a match is on, every minute as one is about to
// begin, else the interval every online widget follows, but never past the next kick-off; a failure
// waits longer each time it repeats, and a fetch that failed leaves the last answer standing with its
// age. While a match is on only its league's scoreboard is read, the schedule and the table again every
// half hour.
//
// A team: its schedule, the scoreboard of its league's day where a match is near, and its league's
// table (§369). A league (§369): its scoreboard on the day ESPN shows now; where that day holds nothing
// still to play, the next day its calendar lists too; and its table.
//
// The answers are kept in sports.json in the local folder, so a restart shows the last known scores
// at once. The logos are SportsLogos'.

using Karpo.Core;

namespace Karpo.Dock.Sports;

/// <summary>What a fetch is asked for: the team, the last answer, and whether the scoreboard alone will do.</summary>
internal sealed record SportsAsk(SportsOptions Team, SportsAnswer? Last, bool ScoreboardOnly);

internal sealed class SportsStore
{
    private const string FileName = "sports.json";

    /// <summary>Teams and leagues kept in sports.json that no widget shows now, for when one comes back.</summary>
    private const int KeptTeams = 8;

    /// <summary>The most an answer may weigh: a hockey season's schedule is over a megabyte.</summary>
    private const int MostBytes = 6 << 20;

    private readonly Lock gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly Dictionary<object, Entry> users = new(ReferenceEqualityComparer.Instance);
    private List<SportsCache>? saved;

    // How an answer is fetched, and whether sports.json is read and written: the shared store asks
    // ESPN and keeps the file; a test's answers from a function and keeps nothing.
    private readonly Func<SportsAsk, CancellationToken, Task<SportsAnswer>> download;
    private readonly SportsLogos? logos;
    private readonly bool keeps;
    private readonly OnlineUpdates updates;
    private readonly Func<DateTime> now;

    private SportsStore()
        : this(Download, keeps: true, logos: SportsLogos.Shared)
    {
    }

    /// <summary>A store of its own, fetching through <paramref name="download"/> — a test's; nothing read or written unless <paramref name="keeps"/>; no logos without <paramref name="logos"/>.</summary>
    internal SportsStore(Func<SportsAsk, CancellationToken, Task<SportsAnswer>> download, bool keeps = false, OnlineUpdates? updates = null, Func<DateTime>? now = null, SportsLogos? logos = null)
    {
        this.download = download;
        this.keeps = keeps;
        this.updates = updates ?? OnlineUpdates.Shared;
        this.now = now ?? (() => Clock.RealUtc());
        this.logos = logos;
    }

    /// <summary>The interval every online widget follows, which a team is asked for at between its matches.</summary>
    internal TimeSpan Interval => updates.Every(OnlineUpdates.LeastMinutes);

    /// <summary>How many fetches this store has started, counted as each is decided.</summary>
    internal int Started { get; private set; }

    private static SportsStore? shared;

    /// <summary>Tests only, set before <see cref="Shared"/> is first asked for: what the shared store fetches through instead of the network.</summary>
    internal static Func<SportsAsk, CancellationToken, Task<SportsAnswer>>? SharedFetch { get; set; }

    public static SportsStore Shared =>
        LazyInitializer.EnsureInitialized(ref shared, () => SharedFetch is { } fetch ? new SportsStore(fetch) : new SportsStore());

    /// <summary>
    /// <paramref name="island"/> shows <paramref name="team"/> now (not set: nothing). On the dock's
    /// thread. The island woken with news is <paramref name="wakes"/>, else the user itself where it is
    /// one: a folder reads a team for each of its sports lines through a user apiece (§369), and is woken for each.
    /// </summary>
    public void Use(object island, SportsOptions team, CellIsland? wakes = null)
    {
        Release(island);
        if (!team.IsSet)
        {
            return;
        }
        lock (gate)
        {
            if (!entries.TryGetValue(team.Key, out var entry))
            {
                var cached = Cached(team.Key);
                entry = new Entry(team.Key, Copy(team))
                {
                    Shown = cached?.Answer,
                    Fetched = cached?.FetchedUtc,
                };
                entry.Due = cached is null ? DateTime.MinValue : cached.FetchedUtc + Pace(entry, cached.FetchedUtc);
                entries[team.Key] = entry;
            }
            entry.Users.Add(island);
            if ((wakes ?? island as CellIsland) is { } woken)
            {
                entry.Signal.Add(woken);
                entry.Wakes[island] = woken;
            }
            users[island] = entry;
        }
    }

    public void Release(object island)
    {
        lock (gate)
        {
            if (!users.Remove(island, out var entry))
            {
                return;
            }
            entry.Users.Remove(island);
            if (entry.Wakes.Remove(island, out var woken) && !entry.Wakes.ContainsValue(woken))
            {
                entry.Signal.Remove(woken);
            }
            if (entry.Users.Count == 0)
            {
                entry.Stop.Cancel();
                entries.Remove(entry.Key);
            }
        }
    }

    /// <summary>The last answer for the island's team, when it was fetched, why a newer one isn't there, and whether one is on its way.</summary>
    public SportsHeld Read(object island)
    {
        lock (gate)
        {
            return users.TryGetValue(island, out var entry)
                ? new SportsHeld(entry.Shown, entry.Fetched, entry.Failure is { } failure ? Explain(failure) : null, entry.Work is { IsCompleted: false })
                : default;
        }
    }

    /// <summary>Starts a request for the island's team when its answer is due.</summary>
    public void Poll(object island)
    {
        lock (gate)
        {
            if (users.TryGetValue(island, out var entry) && (now() >= entry.Due || updates.RetryNow(entry.Failures > 0, entry.Online)) && entry.Work is not { IsCompleted: false })
            {
                Fetch(entry);
            }
        }
    }

    /// <summary>A fetch for the island's team now, however recent the last: Refresh. False when one is on its way.</summary>
    public bool Ask(object island)
    {
        lock (gate)
        {
            if (!users.TryGetValue(island, out var entry) || entry.Work is { IsCompleted: false })
            {
                return false;
            }
            Fetch(entry, whole: true);
            return true;
        }
    }

    /// <summary>On the dock's thread: raises the users' news once for all of it since the last time.</summary>
    internal void Deliver(object island)
    {
        StoreSignal? signal;
        lock (gate)
        {
            signal = users.TryGetValue(island, out var entry) ? entry.Signal : null;
        }
        signal?.Deliver();
    }

    /// <summary>
    /// A logo from ESPN's picture server (SportsLogos) — its drawing for a dark ground where
    /// <paramref name="dark"/> — null while it is on its way (the island is woken when it comes) and
    /// for an address of another host.
    /// </summary>
    public Pixels? LogoOf(object island, string address, bool dark)
    {
        if (logos is null)
        {
            return null;
        }
        StoreSignal? signal;
        lock (gate)
        {
            signal = users.TryGetValue(island, out var entry) ? entry.Signal : null;
        }
        return logos.Get(address, dark, signal is null ? null : signal.Notify);
    }

    /// <summary>The pace <paramref name="entry"/> is fetched at, from what it shows at <paramref name="at"/>.</summary>
    private TimeSpan Pace(Entry entry, DateTime at) =>
        SportsPace.Every(SportsPick.Of(entry.Shown, entry.Team.Team, at), at, Interval);

    private void Fetch(Entry entry, bool whole = false)
    {
        Started++;
        var started = now();
        entry.Attempted = started;
        entry.Online = updates.Comebacks;
        var ask = new SportsAsk(entry.Team, entry.Shown, !whole && SportsPace.ScoreboardOnly(SportsPick.Of(entry.Shown, entry.Team.Team, started), entry.Shown, started));
        var cancel = entry.Stop.Token;
        entry.Work = Task.Run(
            async () =>
            {
                try
                {
                    var result = await download(ask, cancel).ConfigureAwait(false);
                    var fetched = now();
                    lock (gate)
                    {
                        entry.Shown = result;
                        entry.Fetched = fetched;
                        entry.Failure = null;
                        entry.Failures = 0;
                        entry.Due = fetched + Pace(entry, fetched);
                    }
                    Save(entry.Key, result, fetched);
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Console.Error.WriteLine($"Karpo: sports not updated: {error.GetType().Name}");
                    lock (gate)
                    {
                        entry.Failure = error;
                        entry.Failures++;
                        entry.Due = started + SportsPace.Retry(Pace(entry, started), entry.Failures);
                    }
                }
                entry.Signal.Notify();
            },
            cancel);
    }

    /// <summary>The words for what went wrong.</summary>
    internal static string Explain(Exception error) => error switch
    {
        InvalidDataException or System.Text.Json.JsonException => S.DockSportsUnreadable,
        TaskCanceledException or TimeoutException => S.DockSportsNoAnswer,
        _ => S.DockSportsUnreachable,
    };

    /// <summary>
    /// ESPN asked: for a team its schedule and its league's table (unless the scoreboard alone will do),
    /// then the scoreboard of the match it shows where one is near; for a league, <see cref="League"/>.
    /// </summary>
    private static async Task<SportsAnswer> Download(SportsAsk ask, CancellationToken cancel)
    {
        var team = ask.Team;
        if (!team.IsTeam)
        {
            return await League(ask, cancel).ConfigureAwait(false);
        }
        var answer = new SportsAnswer();
        if (ask.ScoreboardOnly && ask.Last is { } last)
        {
            answer = new SportsAnswer { Matches = [.. last.Matches], Others = [.. last.Others], Standing = last.Standing, Record = last.Record, Logo = last.Logo, ScheduledUtc = last.ScheduledUtc, Table = last.Table };
        }
        else
        {
            answer.Table = await Table(team, ask.Last, cancel).ConfigureAwait(false);
            var first = true;
            foreach (var url in SportsAnswers.ScheduleUrls(team))
            {
                var text = await Get(url, cancel).ConfigureAwait(false);
                foreach (var match in SportsAnswers.Matches(text, team.League))
                {
                    if (!answer.Matches.Any(kept => kept.Id == match.Id))
                    {
                        answer.Matches.Add(match);
                    }
                }
                if (first)
                {
                    (answer.Standing, answer.Record, answer.Logo) = SportsAnswers.TeamOf(text);
                    first = false;
                }
            }
            answer.Matches.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
            answer.ScheduledUtc = Clock.RealUtc();
        }
        if (SportsPace.Scoreboard(SportsPick.Of(answer, team.Team, Clock.RealUtc()), Clock.RealUtc()) is { } near)
        {
            var league = near.League.Length > 0 && SportsLeagues.IsSlug(near.League) ? near.League : team.League;
            var text = await Get(SportsAnswers.ScoreboardUrl(team.Sport, league, SportsAnswers.EspnDay(near.StartUtc)), cancel).ConfigureAwait(false);
            SportsAnswers.Merge(answer, SportsAnswers.Matches(text, league), team.Team);
        }
        return answer;
    }

    /// <summary>
    /// §369: a league asked: its scoreboard on the day ESPN shows now, and — not while a game is on
    /// (<see cref="SportsAsk.ScoreboardOnly"/>) — where that day holds nothing still to play, the next
    /// day of its calendar too, and its table. The last answer's table stands while only the scoreboard is read.
    /// </summary>
    private static async Task<SportsAnswer> League(SportsAsk ask, CancellationToken cancel)
    {
        var league = ask.Team;
        var text = await Get(SportsAnswers.LeagueUrl(league.Sport, league.League), cancel).ConfigureAwait(false);
        var answer = new SportsAnswer
        {
            Matches = SportsAnswers.Matches(text, league.League),
            Logo = league.Of?.Logo ?? "",
            Table = ask.Last?.Table ?? [],
            Knockout = ask.Last?.Knockout ?? [],
            KnockoutMonths = ask.Last?.KnockoutMonths ?? [],
            ScheduledUtc = ask.Last?.ScheduledUtc ?? default,
        };
        if (ask.ScoreboardOnly)
        {
            return answer;
        }
        if (!answer.Matches.Any(match => match.State is MatchState.Live or MatchState.Scheduled))
        {
            // The next round: its first day of the calendar, and the round's other days (a football
            // weekend's three, SportsLeagues.RoundDays) — at most three asks, at the online interval.
            var after = answer.Matches.Select(match => SportsAnswers.EspnDay(match.StartUtc)).Append(SportsAnswers.EspnDay(Clock.RealUtc())).Max();
            var first = SportsAnswers.NextDay(text, after);
            for (var day = first; day is { } on && on <= first!.Value.AddDays(SportsLeagues.RoundDays(league.Sport) - 1); day = SportsAnswers.NextDay(text, on))
            {
                var next = await Get(SportsAnswers.LeagueUrl(league.Sport, league.League, on), cancel).ConfigureAwait(false);
                answer.Matches.AddRange(SportsAnswers.Matches(next, league.League).Where(match => !answer.Matches.Any(kept => kept.Id == match.Id)));
            }
            answer.Matches.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        }
        answer.Table = await Table(league, ask.Last, cancel).ConfigureAwait(false);
        (answer.Knockout, answer.KnockoutMonths) = await Knockout(league, SportsAnswers.Stages(text), ask.Last, Clock.RealUtc(), Get, cancel).ConfigureAwait(false);
        answer.ScheduledUtc = Clock.RealUtc();
        return answer;
    }

    /// <summary>
    /// §479: a cup's knockout, every round's matches so far and those fixed to come: its scoreboard a month at a time
    /// over its knockout's months (SportsAnswers.KnockoutMonths), a month over and read before taken from
    /// <paramref name="last"/> rather than asked again; a month that cannot be had keeps what the last answer held of it.
    /// Its matches, by kick-off, and the months over that are read.
    /// </summary>
    internal static async Task<(List<SportsMatch> Matches, List<string> Months)> Knockout(SportsOptions league, IReadOnlyList<SportsStage> stages, SportsAnswer? last,
        DateTime utcNow, Func<string, CancellationToken, string, Task<string>> get, CancellationToken cancel)
    {
        var knockout = SportsAnswers.KnockoutStages(stages);
        var months = SportsAnswers.KnockoutMonths(stages, utcNow);
        var matches = new List<SportsMatch>();
        var over = new List<string>();
        if (months.Count == 0)
        {
            return (matches, over);
        }
        static string MonthOf(SportsMatch match) => SportsAnswers.EspnDay(match.StartUtc).ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);
        var from = knockout[0].StartUtc;
        var today = SportsAnswers.EspnDay(utcNow);
        foreach (var month in months)
        {
            var ended = DateOnly.ParseExact(month, "yyyyMM", System.Globalization.CultureInfo.InvariantCulture).AddMonths(1) < today.AddDays(-1);
            var kept = last?.Knockout.Where(match => MonthOf(match) == month).ToList() ?? [];
            if (ended && last is not null && last.KnockoutMonths.Contains(month))
            {
                matches.AddRange(kept);
                over.Add(month);
                continue;
            }
            try
            {
                var text = await get(SportsAnswers.MonthUrl(league.Sport, league.League, month), cancel, "events").ConfigureAwait(false);
                matches.AddRange(SportsAnswers.Matches(text, league.League).Where(match => match.StartUtc >= from && !matches.Any(had => had.Id == match.Id)));
                if (ended)
                {
                    over.Add(month);
                }
            }
            catch (Exception error) when (error is HttpRequestException or InvalidDataException or System.Text.Json.JsonException || (error is TaskCanceledException && !cancel.IsCancellationRequested))
            {
                matches.AddRange(kept);
            }
        }
        matches.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        return (matches, over);
    }

    /// <summary>
    /// §369: the table of <paramref name="team"/>'s league; the last one where it cannot be had — a cup
    /// out of its group stage keeps none, and a table is never worth a failed answer.
    /// </summary>
    private static async Task<List<SportsGroup>> Table(SportsOptions team, SportsAnswer? last, CancellationToken cancel)
    {
        try
        {
            var text = await Get(SportsAnswers.TableUrl(team.Sport, team.League), cancel, "children").ConfigureAwait(false);
            return SportsAnswers.Table(text, team.Sport);
        }
        catch (Exception error) when (error is HttpRequestException or InvalidDataException or System.Text.Json.JsonException || (error is TaskCanceledException && !cancel.IsCancellationRequested))
        {
            return last?.Table ?? [];
        }
    }

    /// <summary>One answer of ESPN's as text, held to <see cref="MostBytes"/>; one that is no answer of its <paramref name="list"/> (events) is unreadable.</summary>
    private static async Task<string> Get(string url, CancellationToken cancel, string list = "events")
    {
        using var response = await IslandWeb.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var text = await IslandWeb.Text(response, MostBytes, cancel).ConfigureAwait(false)
            ?? throw new InvalidDataException("sports answer too long");
        return SportsAnswers.Readable(text, list) ? text : throw new InvalidDataException("not a sports answer");
    }

    private static SportsOptions Copy(SportsOptions team) =>
        new() { Sport = team.Sport, League = team.League, Team = team.Team, Name = team.Name, Abbreviation = team.Abbreviation };

    private SportsCache? Cached(string key)
    {
        if (!keeps)
        {
            return null;
        }
        saved ??= Load();
        return saved.FirstOrDefault(cache => cache.Key == key);
    }

    private static List<SportsCache> Load()
    {
        if (IslandFiles.Read(FileName) is not { } json)
        {
            return [];
        }
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize(json, IslandJson.Default.SportsFile) is { Teams: { } teams }
                ? [.. teams.Where(team => team is { Answer: not null, Key.Length: > 0 })]
                : [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    private void Save(string key, SportsAnswer answer, DateTime fetched)
    {
        if (!keeps)
        {
            return;
        }
        string json;
        lock (gate)
        {
            saved ??= Load();
            saved.RemoveAll(cache => cache.Key == key);
            saved.Insert(0, new SportsCache { Key = key, FetchedUtc = fetched, Answer = answer });
            if (saved.Count > KeptTeams + entries.Count)
            {
                saved.RemoveRange(KeptTeams + entries.Count, saved.Count - KeptTeams - entries.Count);
            }
            json = System.Text.Json.JsonSerializer.Serialize(new SportsFile { Teams = [.. saved] }, IslandJson.Default.SportsFile);
        }
        IslandFiles.Write(FileName, json);
    }

    private sealed class Entry(string key, SportsOptions team)
    {
        public string Key { get; } = key;

        public SportsOptions Team { get; } = team;

        public StoreSignal Signal { get; } = new(() => { });

        public CancellationTokenSource Stop { get; } = new();

        public HashSet<object> Users { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>The island each user wakes: itself, or the folder it reads a team for.</summary>
        public Dictionary<object, CellIsland> Wakes { get; } = new(ReferenceEqualityComparer.Instance);

        public SportsAnswer? Shown { get; set; }

        public DateTime? Fetched { get; set; }

        public Exception? Failure { get; set; }

        public int Failures { get; set; }

        public DateTime Attempted { get; set; }

        /// <summary>OnlineUpdates.Comebacks as it was attempted.</summary>
        public int Online { get; set; }

        public DateTime Due { get; set; }

        public Task? Work { get; set; }
    }
}

/// <summary>What the store holds for a widget's team: the answer, when it came, why a newer one is not there, and whether one is on its way.</summary>
internal readonly record struct SportsHeld(SportsAnswer? Shown, DateTime? Fetched, string? Problem, bool Fetching);

/// <summary>sports.json.</summary>
internal sealed class SportsFile
{
    public List<SportsCache>? Teams { get; set; }
}

/// <summary>One team's answer in sports.json, and when it was fetched.</summary>
internal sealed class SportsCache
{
    public string Key { get; set; } = "";

    public DateTime FetchedUtc { get; set; }

    public SportsAnswer? Answer { get; set; }
}
