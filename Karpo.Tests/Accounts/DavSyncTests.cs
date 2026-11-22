// §616: a CalDAV calendar brought up to date with sync-collection (RFC 6578)
// against what the dock holds (Karpo.Accounts\Dav\CalendarSync.cs), on the fake
// server (FakeCalDav): a first sync fetches all in multigets of a hundred, a
// quiet calendar costs one request, a delta only what changed, pages and pruned
// tokens and cut multigets and lost deletions each as a real server has them,
// and the token kept only when all came.

using Karpo.Accounts;
using Karpo.Accounts.Dav;

namespace Karpo.Tests.Accounts;

public sealed class DavSyncTests
{
    private static readonly DateTime November = new(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc);

    private DateTime now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private static Account DavAccount(params string[] off) =>
        new()
        {
            Id = "d",
            Kind = AccountKind.Dav,
            Address = "me@example.com",
            CalendarHome = FakeCalDav.Host + FakeCalDav.HomePath,
            Off = off.Length > 0 ? [.. off] : null,
        };

    private DavReader Reader(FakeCalDav server, Account? account = null) =>
        new(account ?? DavAccount(), () => new DavClient(new HttpClient(server), "me", "secret"), now: () => now);

    private static FakeCalDav Server(int objects, string calendar = "work")
    {
        var server = new FakeCalDav().With(calendar);
        for (var i = 0; i < objects; i++)
        {
            server.PutEvent(calendar, $"{i}.ics", $"Event {i}", November.AddDays(i % 60).AddHours(i % 8));
        }
        server.Forget();
        return server;
    }

    /// <summary>What the dock would hold after these changes on top of <paramref name="before"/>: the state it hands over next.</summary>
    private static CalendarState After(CalendarState? before, IEnumerable<CalendarChanges> all)
    {
        var etags = new Dictionary<string, string>(before?.Etags ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        CalendarChanges? last = null;
        foreach (var changes in all)
        {
            foreach (var one in changes.Changed)
            {
                etags[one.Href] = one.Etag;
            }
            foreach (var href in changes.Removed)
            {
                etags.Remove(href);
            }
            last = changes;
        }
        if (last is null or { Unchanged: true } or { Failed: not null })
        {
            return before!;
        }
        return new CalendarState
        {
            Token = last.Token,
            Tag = last.Tag,
            NoSyncReport = last.NoSyncReport,
            Complete = !last.Incomplete,
            ReconciledUtc = last.ReconciledUtc,
            Etags = etags,
        };
    }

    private async Task<(CalendarChanges Changes, CalendarState State)> SyncAsync(FakeCalDav server, CalendarState? state, string calendar = "work")
    {
        var held = new Dictionary<string, CalendarState>(StringComparer.Ordinal);
        if (state is not null)
        {
            held[FakeCalDav.Id(calendar)] = state;
        }
        var parts = new List<CalendarChanges>();
        var all = await Reader(server).SyncCalendarsAsync(held, part => { parts.Add(part); return Task.CompletedTask; }, CancellationToken.None);
        var changes = all.Single(one => one.Calendar.Id == FakeCalDav.Id(calendar));
        parts.Add(changes);
        return (changes, After(state, parts));
    }

    /// <summary>
    /// A first sync lists the calendar once (sync-collection, Depth 0) and fetches every object in multigets of a
    /// hundred: 250 objects, three multigets. Each comes kept (no notes, no attendees), with its etag, by its href; the
    /// token and the ctag are kept, the calendar whole and compared today.
    /// </summary>
    [Fact]
    public async Task AFirstSyncFetchesEverythingInMultigetsOfAHundred()
    {
        var server = Server(250);

        var (changes, state) = await SyncAsync(server, null);

        Assert.Equal(250, changes.Changed.Count);
        Assert.Empty(changes.Removed);
        Assert.False(changes.Incomplete);
        Assert.All(changes.Changed, one => Assert.DoesNotContain("Private notes", one.Text, StringComparison.Ordinal));
        Assert.All(changes.Changed, one => Assert.DoesNotContain("ATTENDEE", one.Text, StringComparison.Ordinal));
        Assert.Contains(changes.Changed, one => one.Href == FakeCalDav.Id("work") + "7.ics" && one.Text!.Contains("SUMMARY:Event 7", StringComparison.Ordinal));
        Assert.Equal("http://dav.example.com/sync/work/250", changes.Token);
        Assert.Equal("ctag-250", changes.Tag);
        Assert.Equal(now, changes.ReconciledUtc);
        Assert.True(state.Complete);
        Assert.Equal(1, server.Count("PROPFIND"));
        Assert.Equal(1, server.Count("sync-collection"));
        Assert.Contains("REPORT /cal/work/ 0 sync-collection token=", server.Requests);
        Assert.Equal(3, server.Count("multiget"));
        Assert.Equal(2, server.Count("multiget 100"));
    }

    /// <summary>A calendar nothing changed in costs the home's one PROPFIND: its ctag is what the last sync found.</summary>
    [Fact]
    public async Task AQuietCalendarCostsOneRequest()
    {
        var server = Server(40);
        var (_, state) = await SyncAsync(server, null);
        server.Forget();

        var (changes, _) = await SyncAsync(server, state);

        Assert.True(changes.Unchanged);
        Assert.Equal(["PROPFIND /cal/ 1"], server.Requests);
    }

    /// <summary>Without a ctag the sync-token property gates the same.</summary>
    [Fact]
    public async Task WithoutACtagTheSyncTokenGates()
    {
        var server = Server(10);
        server.CTag = false;
        var (_, state) = await SyncAsync(server, null);
        server.Forget();

        var (changes, _) = await SyncAsync(server, state);

        Assert.True(changes.Unchanged);
        Assert.Single(server.Requests);
    }

    /// <summary>
    /// A delta brings only what changed: two changed, one new, one deleted, from the token kept; one multiget of three,
    /// one removal, and the new token.
    /// </summary>
    [Fact]
    public async Task ADeltaFetchesOnlyWhatChangedAndRemovesWhatWent()
    {
        var server = Server(120);
        var (_, state) = await SyncAsync(server, null);
        server.PutEvent("work", "3.ics", "Moved", November.AddDays(3));
        server.PutEvent("work", "4.ics", "Renamed", November.AddDays(4));
        server.PutEvent("work", "new.ics", "New", November.AddDays(5));
        server.Delete("work", "9.ics");
        server.Forget();

        var (changes, after) = await SyncAsync(server, state);

        Assert.Equal(["3.ics", "4.ics", "new.ics"], changes.Changed.Select(one => one.Href[(one.Href.LastIndexOf('/') + 1)..]).Order());
        Assert.Equal([FakeCalDav.Id("work") + "9.ics"], changes.Removed);
        Assert.Contains("REPORT /cal/work/ 0 sync-collection token=http://dav.example.com/sync/work/120", server.Requests);
        Assert.Equal(1, server.Count("multiget 3"));
        Assert.Equal(1, server.Count("multiget"));
        Assert.Equal(120, after.Etags.Count);
        Assert.Equal("http://dav.example.com/sync/work/124", after.Token);
    }

    /// <summary>A server that answers in pages (a 507 for the calendar and a token to go on from) is asked page by page: all come.</summary>
    [Fact]
    public async Task PagesOfASyncCollectionAreAskedUntilTheLast()
    {
        var server = Server(100);
        server.PageSize = 30;

        var (changes, state) = await SyncAsync(server, null);

        Assert.Equal(100, changes.Changed.Count);
        Assert.False(changes.Incomplete);
        Assert.Equal(4, server.Count("sync-collection"));
        Assert.Equal("http://dav.example.com/sync/work/100", state.Token);
    }

    /// <summary>
    /// Pages that never move on (the same token given back with the 507): what came is put in, nothing is taken out,
    /// the token before is kept and the calendar is not whole, so it is asked again next time.
    /// </summary>
    [Fact]
    public async Task PagesThatDoNotMoveOnLeaveTheCalendarIncomplete()
    {
        var server = Server(50);
        var (_, state) = await SyncAsync(server, null);
        server.PageSize = 10;
        server.PagesStuck = true;
        now = now.AddDays(2);
        server.Delete("work", "1.ics");
        server.Forget();

        var (changes, after) = await SyncAsync(server, state);

        Assert.True(changes.Incomplete);
        Assert.Empty(changes.Removed);
        // The whole listing (a day on: compared whole), its first page answered with no token to go on from: asked once.
        Assert.Equal(1, server.Count("sync-collection"));
        Assert.Equal(state.Token, after.Token);
        Assert.False(after.Complete);
    }

    /// <summary>
    /// A token the server pruned (403, DAV:valid-sync-token) is let go once: the whole listing, compared by etag, so
    /// only what differs is downloaded again, what is gone is removed, and a new token kept.
    /// </summary>
    [Fact]
    public async Task APrunedTokenIsReplacedByTheWholeListingComparedByEtag()
    {
        var server = Server(80);
        var (_, state) = await SyncAsync(server, null);
        server.PutEvent("work", "5.ics", "Changed", November);
        server.Delete("work", "6.ics");
        server.PruneBefore = 81;
        server.Forget();

        var (changes, after) = await SyncAsync(server, state);

        Assert.Equal(["5.ics"], changes.Changed.Select(one => one.Href[(one.Href.LastIndexOf('/') + 1)..]));
        Assert.Equal([FakeCalDav.Id("work") + "6.ics"], changes.Removed);
        Assert.Equal(2, server.Count("sync-collection"));
        Assert.Equal(1, server.Count("sync-collection token=http"));
        Assert.Equal("http://dav.example.com/sync/work/82", after.Token);
        Assert.False(changes.Incomplete);
    }

    /// <summary>
    /// A server without sync-collection (not among its reports) is listed by PROPFIND, each object's etag, and only
    /// what differs fetched; it is marked so, and asked no sync-collection again. A quiet one still costs one request.
    /// </summary>
    [Fact]
    public async Task AServerWithoutSyncCollectionIsListedByEtag()
    {
        var server = Server(30);
        server.SyncCollection = false;

        var (first, state) = await SyncAsync(server, null);
        Assert.Equal(30, first.Changed.Count);
        Assert.True(first.NoSyncReport);
        Assert.Null(first.Token);
        Assert.Equal(0, server.Count("sync-collection"));
        Assert.Equal(1, server.Count("PROPFIND /cal/work/ 1"));

        server.PutEvent("work", "2.ics", "Changed", November);
        server.Delete("work", "3.ics");
        server.Forget();
        var (delta, _) = await SyncAsync(server, state);
        Assert.Equal(["2.ics"], delta.Changed.Select(one => one.Href[(one.Href.LastIndexOf('/') + 1)..]));
        Assert.Equal([FakeCalDav.Id("work") + "3.ics"], delta.Removed);
        Assert.Equal(1, server.Count("multiget 1"));

        server.Forget();
        var (quiet, _) = await SyncAsync(server, After(state, [delta]));
        Assert.True(quiet.Unchanged);
        Assert.Single(server.Requests);
    }

    /// <summary>A server that says nothing of its reports and refuses a sync-collection (400, 403, 405, 501) is listed by etag.</summary>
    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(405)]
    [InlineData(501)]
    public async Task ASyncCollectionRefusedFallsBackToTheListing(int status)
    {
        var server = Server(12);
        server.SyncCollection = false;
        server.SupportedReportSet = false;
        server.NoSyncStatus = status;

        var (changes, state) = await SyncAsync(server, null);

        Assert.Equal(12, changes.Changed.Count);
        Assert.True(state.NoSyncReport);
        Assert.Null(changes.Failed);
        Assert.Equal(1, server.Count("sync-collection"));
    }

    /// <summary>A multiget the server cuts short (a 507 after so many) is asked again in halves for what did not come: all come, none twice.</summary>
    [Fact]
    public async Task AMultigetCutShortIsAskedAgainInHalves()
    {
        var server = Server(100);
        server.MultigetMost = 30;

        var (changes, state) = await SyncAsync(server, null);

        Assert.Equal(100, changes.Changed.Count);
        Assert.Equal(100, changes.Changed.Select(one => one.Href).Distinct().Count());
        Assert.True(state.Complete);
        Assert.True(server.Count("multiget") > 1);
    }

    /// <summary>
    /// An object that does not come at all (left out of the multiget, no 507): what came is put in, but the token is not
    /// kept, so the next sync asks again from the one before and fetches it then.
    /// </summary>
    [Fact]
    public async Task TheTokenIsKeptOnlyWhenEveryChangeCame()
    {
        var server = Server(20);
        var (_, state) = await SyncAsync(server, null);
        server.PutEvent("work", "1.ics", "One", November);
        server.PutEvent("work", "2.ics", "Two", November);
        server.MultigetDrops.Add("2.ics");

        var (changes, after) = await SyncAsync(server, state);
        Assert.Equal(["1.ics"], changes.Changed.Select(one => one.Href[(one.Href.LastIndexOf('/') + 1)..]));
        Assert.True(changes.Incomplete);
        Assert.Equal(state.Token, after.Token);

        server.MultigetDrops.Clear();
        server.Forget();
        var (again, last) = await SyncAsync(server, after);
        Assert.Contains("2.ics", again.Changed.Select(one => one.Href[(one.Href.LastIndexOf('/') + 1)..]));
        Assert.True(last.Complete);
        Assert.Equal("http://dav.example.com/sync/work/22", last.Token);
    }

    /// <summary>A calendar the server refuses (403) fails alone; the other syncs as ever.</summary>
    [Fact]
    public async Task ACalendarRefusedFailsAlone()
    {
        var server = Server(5).With("shared");
        server.Refuse.Add("shared");

        var all = await Reader(server).SyncCalendarsAsync(new Dictionary<string, CalendarState>(), null, CancellationToken.None);

        Assert.Equal(5, all.Single(one => one.Calendar.Name == "work").Changed.Count);
        Assert.NotNull(all.Single(one => one.Calendar.Name == "shared").Failed);
    }

    /// <summary>A server too busy (429, 503) is the account's failure, tried again as any: no calendar is marked failed alone.</summary>
    [Fact]
    public async Task AThrottledServerFailsTheAccount()
    {
        var server = Server(5);
        server.Throttle = 2;

        var refused = await Assert.ThrowsAsync<AccountException>(() => Reader(server).SyncCalendarsAsync(new Dictionary<string, CalendarState>(), null, CancellationToken.None));
        Assert.Equal(429, refused.Status);
    }

    /// <summary>A calendar turned off is listed and nothing of it asked.</summary>
    [Fact]
    public async Task ACalendarTurnedOffIsListedAndNotAsked()
    {
        var server = Server(5);

        var all = await Reader(server, DavAccount(SyncKeys.Calendar(FakeCalDav.Id("work")))).SyncCalendarsAsync(new Dictionary<string, CalendarState>(), null, CancellationToken.None);

        Assert.True(Assert.Single(all).Off);
        Assert.Equal(["PROPFIND /cal/ 1"], server.Requests);
    }

    /// <summary>
    /// A server that writes an href one way in its listing (its host in capitals, '@' as %40) and another in its
    /// multiget: one key, so a second sync after a change downloads only that one, and nothing is removed.
    /// </summary>
    [Fact]
    public async Task HrefsEscapedDifferentlyAreOneObject()
    {
        var server = new FakeCalDav().With("work");
        server.EscapeHrefsDifferently = true;
        server.PutEvent("work", "anna@example.com-1.ics", "One", November);
        server.PutEvent("work", "anna@example.com-2.ics", "Two", November);
        var (first, state) = await SyncAsync(server, null);
        Assert.Equal(2, first.Changed.Count);
        Assert.False(first.Incomplete);

        server.PutEvent("work", "anna@example.com-2.ics", "Two again", November);
        now = now.AddDays(2);
        server.Forget();
        var (changes, after) = await SyncAsync(server, state);

        Assert.Single(changes.Changed);
        Assert.Empty(changes.Removed);
        Assert.Equal(2, after.Etags.Count);
        Assert.Equal(1, server.Count("multiget 1"));
    }

    /// <summary>
    /// A server that loses deletions (its sync-collection never says one went): the delta misses it, and the daily
    /// comparison of the whole listing removes it, downloading nothing.
    /// </summary>
    [Fact]
    public async Task ADeletionTheServerLostIsCaughtByTheDailyComparison()
    {
        var server = Server(15);
        server.LoseDeletions = true;
        var (_, state) = await SyncAsync(server, null);
        server.Delete("work", "4.ics");

        var (delta, between) = await SyncAsync(server, state);
        Assert.Empty(delta.Removed);

        now = now.AddDays(1);
        server.Forget();
        var (daily, after) = await SyncAsync(server, between);
        Assert.Equal([FakeCalDav.Id("work") + "4.ics"], daily.Removed);
        Assert.Empty(daily.Changed);
        Assert.Equal(0, server.Count("multiget"));
        Assert.Equal(14, after.Etags.Count);
        Assert.Equal(now, after.ReconciledUtc);
    }

    /// <summary>
    /// A first download of 2,500 hands what it has to the dock every 1,000 objects, not whole, and its answer holds the
    /// last 500; each object comes once.
    /// </summary>
    [Fact]
    public async Task AFirstDownloadIsHandedOverInSteps()
    {
        var server = Server(2500);
        var parts = new List<CalendarChanges>();

        var all = await Reader(server).SyncCalendarsAsync(new Dictionary<string, CalendarState>(), part => { parts.Add(part); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal([1000, 1000], parts.Select(part => part.Changed.Count));
        Assert.All(parts, part => Assert.True(part.Incomplete));
        Assert.All(parts, part => Assert.Null(part.Token));
        var last = Assert.Single(all);
        Assert.Equal(500, last.Changed.Count);
        Assert.False(last.Incomplete);
        Assert.Equal(2500, parts.Concat(all).SelectMany(part => part.Changed).Select(one => one.Href).Distinct().Count());
    }

    /// <summary>A reader asked the old way (the window is no matter) answers every calendar whole, kept: the dock never asks so.</summary>
    [Fact]
    public async Task AReadIsEveryObjectWhole()
    {
        var server = Server(7);
        server.PutEvent("work", "old.ics", "Long ago", new DateTime(2010, 1, 1, 9, 0, 0, DateTimeKind.Utc));

        var read = Assert.Single(await Reader(server).CalendarsAsync(November, November.AddMonths(1), CancellationToken.None));

        Assert.Equal(8, read.Objects.Count);
        Assert.Contains(read.Objects, text => text.Contains("Long ago", StringComparison.Ordinal));
    }
}
