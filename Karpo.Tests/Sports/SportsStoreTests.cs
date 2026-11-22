// sports: the store (docs\widgets.md §239): one fetch per team for every widget showing it, nothing
// asked before a team is set, a match on now asked for again after half a minute and then from its
// scoreboard alone, and a failure keeping the last answer while it waits longer each time.

using Karpo.Dock.Sports;

namespace Karpo.Tests.Sports;

public sealed class SportsStoreTests
{
    private sealed class Fake
    {
        public DateTime Now = SavedSports.SavedUtc;
        public readonly List<SportsAsk> Asks = [];
        public bool Fail;

        public SportsStore Store() => new((ask, _) =>
        {
            lock (Asks)
            {
                Asks.Add(ask);
            }
            return Fail ? Task.FromException<SportsAnswer>(new HttpRequestException("down")) : Task.FromResult(SavedSports.MonzaLive());
        }, now: () => Now);
    }

    private static void Settled(SportsStore store, object user) =>
        Assert.True(Patience.Until(() => !store.Read(user).Fetching));

    [Fact]
    public void AFaceAndALineOfOneTeamAreOneFetch()
    {
        var fake = new Fake();
        var store = fake.Store();
        object face = new(), line = new(), unset = new();
        store.Use(face, SavedSports.Monza());
        store.Use(line, SavedSports.Monza());
        store.Use(unset, new SportsOptions());
        store.Poll(face);
        store.Poll(line);
        store.Poll(unset);
        Settled(store, face);

        Assert.Equal(1, store.Started);
        Assert.NotNull(store.Read(line).Shown);
        Assert.Equal(SavedSports.SavedUtc, store.Read(line).Fetched);
        // No team, nothing asked and nothing held.
        Assert.Equal(default, store.Read(unset));
    }

    [Fact]
    public void AMatchOnNowIsAskedForAfterHalfAMinuteFromItsScoreboard()
    {
        var fake = new Fake();
        var store = fake.Store();
        var line = new object();
        store.Use(line, SavedSports.Monza());
        store.Poll(line);
        Settled(store, line);
        Assert.False(fake.Asks[0].ScoreboardOnly);

        fake.Now = fake.Now.AddSeconds(20);
        store.Poll(line);
        Assert.Equal(1, store.Started);

        fake.Now = fake.Now.AddSeconds(15);
        store.Poll(line);
        Settled(store, line);
        Assert.Equal(2, store.Started);
        Assert.True(fake.Asks[1].ScoreboardOnly);
        Assert.NotNull(fake.Asks[1].Last);

        // Refresh reads the schedule whole, however recent the last.
        Assert.True(store.Ask(line));
        Settled(store, line);
        Assert.False(fake.Asks[2].ScoreboardOnly);
    }

    [Fact]
    public void AFailureKeepsTheLastAnswerAndWaitsLongerEachTime()
    {
        var fake = new Fake();
        var store = fake.Store();
        var line = new object();
        store.Use(line, SavedSports.Monza());
        store.Poll(line);
        Settled(store, line);

        fake.Fail = true;
        fake.Now = fake.Now.AddSeconds(31);
        store.Poll(line);
        Settled(store, line);
        var held = store.Read(line);
        Assert.NotNull(held.Shown);
        Assert.Equal(S.DockSportsUnreachable, held.Problem);
        Assert.Equal(SavedSports.SavedUtc, held.Fetched);

        // The first failure waits the pace (half a minute), the second twice that.
        fake.Now = fake.Now.AddSeconds(29);
        store.Poll(line);
        Assert.Equal(2, store.Started);
        fake.Now = fake.Now.AddSeconds(2);
        store.Poll(line);
        Settled(store, line);
        Assert.Equal(3, store.Started);
        fake.Now = fake.Now.AddSeconds(45);
        store.Poll(line);
        Assert.Equal(3, store.Started);
        fake.Now = fake.Now.AddSeconds(20);
        store.Poll(line);
        Settled(store, line);
        Assert.Equal(4, store.Started);

        // Back again: the answer and no problem.
        fake.Fail = false;
        Assert.True(store.Ask(line));
        Settled(store, line);
        Assert.Null(store.Read(line).Problem);
        Assert.Equal(fake.Now, store.Read(line).Fetched);
    }

    [Fact]
    public void TheLastUserGoneTheTeamIsForgotten()
    {
        var fake = new Fake();
        var store = fake.Store();
        var line = new object();
        store.Use(line, SavedSports.Monza());
        store.Poll(line);
        Settled(store, line);
        store.Release(line);

        Assert.Equal(default, store.Read(line));
        Assert.False(store.Ask(line));
    }

    [Fact]
    public void AFaceReadsEachOfItsLinesThroughAUserOfItsOwnAndALeagueIsAFetchOfItsOwn()
    {
        // §369: a folder's face reads every sports line's team or league, each through a user of its own.
        var fake = new Fake();
        var store = fake.Store();
        object monza = new(), league = new(), line = new();
        store.Use(monza, SavedSports.Monza());
        store.Use(league, SavedSports.Bundesliga());
        store.Use(line, SavedSports.Monza());
        store.Poll(monza);
        store.Poll(league);
        store.Poll(line);
        Settled(store, monza);
        Settled(store, league);

        Assert.Equal(2, store.Started);
        Assert.Contains(fake.Asks, ask => ask.Team.Key == "soccer/ger.1" && !ask.Team.IsTeam);
        // The face's user for one line gone, the line's own reads on.
        store.Release(monza);
        Assert.NotNull(store.Read(line).Shown);
        Assert.Equal(default, store.Read(monza));
    }

    [Theory]
    [InlineData(typeof(HttpRequestException), "unreachable")]
    [InlineData(typeof(TaskCanceledException), "noAnswer")]
    [InlineData(typeof(InvalidDataException), "unreadable")]
    public void EveryFailureIsSaidInItsOwnWords(Type error, string word) =>
        Assert.Equal(Strings.Text($"dock.sports.{word}"), SportsStore.Explain((Exception)Activator.CreateInstance(error)!));
}
