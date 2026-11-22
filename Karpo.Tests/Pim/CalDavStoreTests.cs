// §616: the calendar store with a CalDAV account kept whole — the real reader
// (DavReader) on the fake server (FakeCalDav), its mirror in a folder of the
// test's own. A start without a connection shows every year with no read; a
// restart within the interval reads nothing; a view looking at 2040 asks the
// server nothing; a refresh that fails keeps all, stale; calendar-cache.json
// holds no CalDAV account; a change on the server reaches the view and the
// file; a calendar turned off loses its file and is downloaded again when on;
// an account removed loses its folder; a first download cut off partway
// resumes with what it had; a file another program holds is written later.

using Karpo.Accounts;
using Karpo.Accounts.Dav;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Accounts;

namespace Karpo.Tests.Pim;

public sealed class CalDavStoreTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly string folder = Path.Combine(Isolation.Folder, "caldav-" + Guid.NewGuid().ToString("N"));

    private readonly FakeCalDav server = new FakeCalDav().With("work", "Work");

    private DateTime clock = Now;

    private string Mirrors => Path.Combine(folder, CalendarMirror.FolderName);

    private static Account Dav(string id = "acc1", params string[] off) => new()
    {
        Id = id,
        Kind = AccountKind.Dav,
        Address = "me@example.com",
        CalendarHome = FakeCalDav.Host + FakeCalDav.HomePath,
        Off = off.Length > 0 ? [.. off] : null,
    };

    private DavReader Reader(Account? account = null) =>
        new(account ?? Dav(), () => new DavClient(new HttpClient(server), "me", "secret"), now: () => clock);

    private CalendarStore Store(params IAccountReader[] readers) => Store(new AccountSet(readers));

    private CalendarStore Store(AccountSet set) =>
        new(new AccountCalendars(set, () => clock, cache: new PimCache(folder), mirror: new CalendarMirror(Mirrors)));

    /// <summary>A meeting in 2010, one in 2035, and a birthday every 14 November since 2016.</summary>
    private void Years()
    {
        server.PutEvent("work", "2010.ics", "Kickoff 2010", new DateTime(2010, 3, 4, 9, 0, 0, DateTimeKind.Utc));
        server.PutEvent("work", "2035.ics", "Review 2035", new DateTime(2035, 6, 7, 9, 0, 0, DateTimeKind.Utc));
        server.Put("work", "birthday.ics", "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:birthday\r\nSUMMARY:Birthday\r\nDTSTART;VALUE=DATE:20161114\r\nDTEND;VALUE=DATE:20161115\r\nRRULE:FREQ=YEARLY\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n");
    }

    private static List<string> Titles(CalendarStore store, DateTime from, DateTime to) =>
        [.. store.EventsIn(from, to, TimeZoneInfo.Utc).OrderBy(one => one.Start).Select(one => one.Title)];

    private static List<string> Everything(CalendarStore store) => Titles(store, new DateTime(2009, 1, 1), new DateTime(2037, 1, 1));

    private async Task<CalendarStore> SyncedAsync(params IAccountReader[] readers)
    {
        var store = Store(readers.Length > 0 ? readers : [Reader()]);
        store.Open();
        await store.Idle();
        return store;
    }

    /// <summary>
    /// After one sync, a start without a connection shows 2010, 2035 and this year's birthday from the mirror before any
    /// read, every year there; the read that fails leaves it all on screen, stale.
    /// </summary>
    [Fact]
    public async Task AStartWithoutAConnectionShowsEveryYearWithNoRead()
    {
        Years();
        await SyncedAsync();
        server.Throttle = 1000;
        server.Forget();
        clock = Now.AddHours(3);

        var offline = Store(Reader());
        offline.Open();
        await offline.CacheRead();
        Assert.Equal(["Kickoff 2010"], Titles(offline, new DateTime(2010, 3, 1), new DateTime(2010, 4, 1)));
        Assert.Equal(["Review 2035"], Titles(offline, new DateTime(2035, 6, 1), new DateTime(2035, 7, 1)));
        Assert.Equal(["Birthday"], Titles(offline, new DateTime(2026, 11, 1), new DateTime(2026, 12, 1)));
        await offline.Idle();
        Assert.Equal(23, Everything(offline).Count);
        Assert.True(offline.Freshness.Stale);
        Assert.Null(offline.Problem);
    }

    /// <summary>A restart within the interval of the last sync asks the server nothing; a view of 2040 asks nothing either.</summary>
    [Fact]
    public async Task ARestartWithinTheIntervalAndAViewFarAheadReadNothing()
    {
        Years();
        await SyncedAsync();
        server.Forget();
        clock = Now.AddMinutes(5);

        var again = await SyncedAsync();
        Assert.Empty(server.Requests);
        Assert.Equal(23, Everything(again).Count);

        Assert.Single(Titles(again, new DateTime(2040, 11, 1), new DateTime(2040, 12, 1)));
        await again.Idle();
        Assert.Empty(server.Requests);
    }

    /// <summary>calendar-cache.json holds no CalDAV account: its calendars are its mirror's files.</summary>
    [Fact]
    public async Task TheCalendarCacheHoldsNoCalDavAccount()
    {
        Years();
        await SyncedAsync();

        Assert.Empty(new PimCache(folder).ReadCalendars()!.Accounts);
        Assert.Single(Directory.GetFiles(Path.Combine(Mirrors, "acc1"), "*.bin"));
    }

    /// <summary>A change on the server reaches the view at the next refresh, and the file: a new start without a connection shows it.</summary>
    [Fact]
    public async Task AChangeOnTheServerReachesTheViewAndTheFile()
    {
        Years();
        var store = await SyncedAsync();
        server.PutEvent("work", "2010.ics", "Kickoff moved", new DateTime(2010, 3, 5, 9, 0, 0, DateTimeKind.Utc));
        server.Delete("work", "2035.ics");
        server.PutEvent("work", "new.ics", "Lunch", Now.AddDays(1));
        server.Forget();

        store.Refresh();
        await store.Idle();
        Assert.Equal(["Kickoff moved"], Titles(store, new DateTime(2010, 3, 1), new DateTime(2010, 4, 1)));
        Assert.Empty(Titles(store, new DateTime(2035, 6, 1), new DateTime(2035, 7, 1)));
        Assert.Equal(["Lunch"], Titles(store, Now.Date.AddDays(1), Now.Date.AddDays(2)));
        Assert.Equal(1, server.Count("multiget 2"));

        server.Throttle = 1000;
        clock = Now.AddHours(1);
        var offline = await SyncedAsync();
        Assert.Equal(["Kickoff moved"], Titles(offline, new DateTime(2010, 3, 1), new DateTime(2010, 4, 1)));
        Assert.Empty(Titles(offline, new DateTime(2035, 6, 1), new DateTime(2035, 7, 1)));
    }

    /// <summary>A calendar turned off loses its file and shows nothing; turned on, it is downloaded again whole.</summary>
    [Fact]
    public async Task ACalendarTurnedOffLosesItsFileAndIsDownloadedAgainWhenOn()
    {
        Years();
        var set = new AccountSet([Reader()]);
        var store = Store(set);
        store.Open();
        await store.Idle();
        var files = Path.Combine(Mirrors, "acc1");
        Assert.Single(Directory.GetFiles(files, "*.bin"));

        set.Become([Reader(Dav("acc1", SyncKeys.Calendar(FakeCalDav.Id("work"))))]);
        await store.Idle();
        Assert.Empty(Directory.GetFiles(files, "*.bin"));
        Assert.Empty(Everything(store));

        server.Forget();
        set.Become([Reader()]);
        await store.Idle();
        Assert.Equal(23, Everything(store).Count);
        Assert.Equal(1, server.Count("multiget 3"));
        Assert.Single(Directory.GetFiles(files, "*.bin"));
    }

    /// <summary>An account removed loses its folder at once; one removed while the dock was not running, at the next start.</summary>
    [Fact]
    public async Task AnAccountRemovedLosesItsFolder()
    {
        Years();
        var set = new AccountSet([Reader()]);
        var store = Store(set);
        store.Open();
        await store.Idle();
        Assert.True(Directory.Exists(Path.Combine(Mirrors, "acc1")));

        set.Become([]);
        await store.Idle();
        Assert.False(Directory.Exists(Path.Combine(Mirrors, "acc1")));

        await SyncedAsync(Reader(Dav("acc2")));
        Assert.True(Directory.Exists(Path.Combine(Mirrors, "acc2")));
        await SyncedAsync(Reader(Dav("acc3")));
        Assert.False(Directory.Exists(Path.Combine(Mirrors, "acc2")));
    }

    /// <summary>
    /// A first download of 2,500 cut off partway (the server gone after the first thousand): the thousand are on screen
    /// and kept; the next start shows them with no read, and its sync fetches only the 1,500 missing.
    /// </summary>
    [Fact]
    public async Task AFirstDownloadCutOffResumesWithWhatItHad()
    {
        for (var i = 0; i < 2500; i++)
        {
            server.PutEvent("work", $"{i}.ics", $"Event {i}", Now.AddDays(i % 300));
        }
        // The listing, the sync-collection, the first thousand's ten multigets and a few of the next.
        server.FailAfter = 1 + 1 + 10 + 3;
        var first = await SyncedAsync();
        var shown = Everything(first).Count;
        Assert.Equal(1000, shown);
        Assert.True(first.Freshness.Stale);

        server.FailAfter = null;
        server.Forget();
        var again = Store(Reader());
        again.Open();
        await again.CacheRead();
        Assert.Equal(1000, Everything(again).Count);
        await again.Idle();
        Assert.Equal(2500, Everything(again).Count);
        Assert.Equal(15, server.Count("multiget"));
        Assert.Contains("REPORT /cal/work/ 0 sync-collection token=", server.Requests);
    }

    /// <summary>A file another program holds when a change comes is written at the next sync, once it lets go.</summary>
    [Fact]
    public async Task AFileHeldByAnotherProgramIsWrittenLater()
    {
        Years();
        var store = await SyncedAsync();
        var path = Path.Combine(Mirrors, "acc1", CalendarMirror.FileName(FakeCalDav.Id("work")));
        server.PutEvent("work", "new.ics", "Lunch", Now.AddDays(1));
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            store.Refresh();
            await store.Idle();
            Assert.Equal(["Lunch"], Titles(store, Now.Date.AddDays(1), Now.Date.AddDays(2)));
        }
        var held = Assert.Single(await new CalendarMirror(Mirrors).ReadAsync("acc1", CancellationToken.None));
        Assert.Equal(3, held.Etags.Count);

        store.Refresh();
        await store.Idle();
        Assert.Equal(4, Assert.Single(await new CalendarMirror(Mirrors).ReadAsync("acc1", CancellationToken.None)).Etags.Count);
    }
}
