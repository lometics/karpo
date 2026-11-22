// A restart is no reason to read (the person, 2026-10-31: "make sure that the caches persist restarts and
// stuff. i dont want the application to fetch the data just because there was a restart."): the calendars,
// the mail, the contacts and To Do's tasks, made again over the files a run before kept (PimCache), read an
// account kept less than the interval ago at the interval after that read — not at the start — and one kept
// longer ago, of another window, or not at all, at once. The changes kept for To Do still go at the start:
// sending is not reading. Every cache is a folder of its own under the run's isolated folder; no account
// is reached.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class RestartReadTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    /// <summary>The interval the stores here follow: the default quarter of an hour.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(OnlineUpdates.DefaultMinutes);

    private static PimCache Cache() => new(Path.Combine(Isolation.Folder, "restart-" + Guid.NewGuid().ToString("N")));

    private static MailItem Mail(string id) =>
        new(id, $"Sender {id}", $"{id}@example.com", $"Subject {id}", null, new DateTimeOffset(Now.AddHours(-1)), true);

    private static Occurrence Event(string title, DateTime startUtc) => new(title, null, false, false, startUtc, startUtc.AddHours(1));

    /// <summary>The timer was set for the kept read's interval to be up: <paramref name="left"/> from now, give or take the test's own time.</summary>
    private static void ArmedFor(TimeSpan left, TimeSpan? armed)
    {
        Assert.NotNull(armed);
        Assert.InRange(armed.Value, left - TimeSpan.FromSeconds(30), left);
    }

    // --- Mail -------------------------------------------------------------------------------------

    [Fact]
    public async Task AnInboxRead5MinutesAgoIsNotReadAtTheStartAndOneRead20MinutesAgoIs()
    {
        var cache = Cache();
        var fresh = new FakeReader(AccountKind.Imap, "fresh@example.com") { Inbox = new Inbox(1, [Mail("fresh")]) };
        var old = new FakeReader(AccountKind.Imap, "old@example.com") { Inbox = new Inbox(1, [Mail("old")]) };
        cache.Write(new MailCacheFile
        {
            Accounts = [PimCache.Keep(fresh.Account.Id, Now.AddMinutes(-5), fresh.Inbox), PimCache.Keep(old.Account.Id, Now.AddMinutes(-20), old.Inbox)],
        });

        using var store = new MailStore(new AccountSet([fresh, old]), new OnlineUpdates(), cache, () => Now);
        store.Keep();
        await store.Idle();

        Assert.Equal(0, fresh.InboxReads);
        Assert.Equal(1, old.InboxReads);
        Assert.Equal(["Sender fresh", "Sender old"], store.Messages.Select(message => message.From).Order());
        // The fresh inbox is due the interval after its read: in ten minutes, not fifteen.
        ArmedFor(Interval - TimeSpan.FromMinutes(5), store.Armed);
    }

    [Fact]
    public async Task AKeptInboxIsReadOnceTheIntervalSinceItsReadIsUp()
    {
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Imap) { Inbox = new Inbox(1, [Mail("a")]) };
        cache.Write(new MailCacheFile { Accounts = [PimCache.Keep(reader.Account.Id, Now.AddMinutes(-7), reader.Inbox)] });
        var updates = new OnlineUpdates();
        using var store = new MailStore(new AccountSet([reader]), updates, cache, () => Now);
        store.Keep();
        await store.Idle();
        Assert.Equal(0, reader.InboxReads);

        // Five minutes from the read kept seven minutes ago are up: read now, by the store's own timer.
        updates.Set(5);
        Assert.True(Patience.Until(() => reader.InboxReads == 1));
    }

    [Fact]
    public async Task AnInboxKeptForAnotherAccountIsNoReadOfThisOne()
    {
        var cache = Cache();
        var gone = new FakeReader(AccountKind.Imap, "gone@example.com") { Inbox = new Inbox(1, [Mail("gone")]) };
        var here = new FakeReader(AccountKind.Imap, "here@example.com") { Inbox = new Inbox(1, [Mail("here")]) };
        cache.Write(new MailCacheFile { Accounts = [PimCache.Keep(gone.Account.Id, Now.AddMinutes(-1), gone.Inbox)] });

        using var store = new MailStore(new AccountSet([here]), new OnlineUpdates(), cache, () => Now);
        store.Keep();
        await store.Idle();

        Assert.Equal(1, here.InboxReads);
        Assert.Equal(["Sender here"], store.Messages.Select(message => message.From));
    }

    [Fact]
    public async Task ThePersonsRefreshReadsAKeptInboxAtOnce()
    {
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Imap) { Inbox = new Inbox(1, [Mail("a")]) };
        cache.Write(new MailCacheFile { Accounts = [PimCache.Keep(reader.Account.Id, Now.AddMinutes(-2), reader.Inbox)] });
        using var store = new MailStore(new AccountSet([reader]), new OnlineUpdates(), cache, () => Now);
        store.Keep();
        await store.Idle();

        store.Refresh(force: true);
        await store.Idle();
        Assert.Equal(1, reader.InboxReads);
    }

    // --- Calendars --------------------------------------------------------------------------------

    private static FakeReader Calendar(params Occurrence[] events)
    {
        var reader = new FakeReader(address: "home@example.com");
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("cal", "Home", "#D13438"), events, []));
        return reader;
    }

    /// <summary>A run of the calendars at <paramref name="at"/>: made, read, done.</summary>
    private static async Task<AccountCalendars> Calendars(FakeReader reader, PimCache cache, DateTime at)
    {
        var calendars = new AccountCalendars(new AccountSet([reader]), () => at, new OnlineUpdates(), cache);
        calendars.Refresh();
        await calendars.Idle();
        return calendars;
    }

    [Fact]
    public async Task CalendarsRead5MinutesAgoAreNotReadAtTheStartAndOnesRead20MinutesAgoAre()
    {
        var cache = Cache();
        var reader = Calendar(Event("Dentist", Now.AddDays(1)));
        using (await Calendars(reader, cache, Now.AddMinutes(-5)))
        {
        }
        using var again = await Calendars(reader, cache, Now);
        Assert.Equal(1, reader.CalendarReads);
        Assert.Equal(["Dentist"], Assert.Single(again.Calendars).EventsIn(Now, Now.AddDays(2), TimeZoneInfo.Utc).Select(item => item.Title));
        Assert.False(again.Freshness.Stale);
        ArmedFor(Interval - TimeSpan.FromMinutes(5), again.Armed);

        var older = Cache();
        var other = Calendar(Event("Dentist", Now.AddDays(1)));
        using (await Calendars(other, older, Now.AddMinutes(-20)))
        {
        }
        using var late = await Calendars(other, older, Now);
        Assert.Equal(2, other.CalendarReads);
    }

    /// <summary>Kept four minutes ago, but in the month before: the window wanted now is not the one kept.</summary>
    [Fact]
    public async Task CalendarsKeptForAnotherWindowAreReadAtTheStart()
    {
        var cache = Cache();
        var midnight = new DateTime(2026, 11, 1, 0, 2, 0, DateTimeKind.Utc);
        var reader = Calendar(Event("Dentist", midnight.AddDays(1)));
        using (await Calendars(reader, cache, midnight.AddMinutes(-4)))
        {
        }
        using var again = await Calendars(reader, cache, midnight);
        Assert.Equal(2, reader.CalendarReads);
        Assert.Equal(PimCache.Window(midnight), reader.Window);
    }

    /// <summary>A calendar cut at the cap is not all there: the file holds no window, and the next start reads.</summary>
    [Fact]
    public async Task CalendarsKeptOnlyInPartAreReadAtTheStart()
    {
        var cache = Cache();
        var reader = Calendar([.. Enumerable.Range(0, PimCache.MostEvents + 1).Select(i => Event($"e{i}", Now.AddMinutes(i)))]);
        using (await Calendars(reader, cache, Now.AddMinutes(-5)))
        {
        }
        Assert.Null(Assert.Single(cache.ReadCalendars()!.Accounts).From);

        using var again = await Calendars(reader, cache, Now);
        Assert.Equal(2, reader.CalendarReads);
    }

    // --- Contacts ---------------------------------------------------------------------------------

    private static FakeReader People(params string[] names)
    {
        var reader = new FakeReader(address: "people@example.com");
        reader.People.AddRange(names.Select(name => new Person(name, name, null, [$"{name}@example.com"], [])));
        return reader;
    }

    private static async Task<ContactStore> Contacts(FakeReader reader, PimCache cache, DateTime at)
    {
        var store = new ContactStore(new AccountSet([reader]), new OnlineUpdates(), now: () => at, cache: cache);
        store.Keep();
        await store.Idle();
        return store;
    }

    [Fact]
    public async Task ContactsRead5MinutesAgoAreNotReadAtTheStartAndOnesRead20MinutesAgoAre()
    {
        var cache = Cache();
        var reader = People("Ada", "Grace");
        using (await Contacts(reader, cache, Now.AddMinutes(-5)))
        {
        }
        using var again = await Contacts(reader, cache, Now);
        Assert.Equal(1, reader.ContactReads);
        Assert.Equal(["Ada", "Grace"], again.Contacts.Select(person => person.DisplayName));
        Assert.Equal(["Ada@example.com"], again.Contacts[0].Emails);
        Assert.False(again.Loading);
        ArmedFor(Interval - TimeSpan.FromMinutes(5), again.Armed);

        var older = Cache();
        var other = People("Ada");
        using (await Contacts(other, older, Now.AddMinutes(-20)))
        {
        }
        using var late = await Contacts(other, older, Now);
        Assert.Equal(2, other.ContactReads);
    }

    /// <summary>The people are there at a start without a connection, as the events and the messages are.</summary>
    [Fact]
    public async Task ThePeopleReadAreThereAtTheNextStartWithoutAConnection()
    {
        var cache = Cache();
        var reader = People("Ada");
        using (await Contacts(reader, cache, Now.AddHours(-3)))
        {
        }
        reader.Failure = new HttpRequestException("offline");
        using var again = await Contacts(reader, cache, Now);
        Assert.Equal(2, reader.ContactReads);
        Assert.Equal(["Ada"], again.Contacts.Select(person => person.DisplayName));
        Assert.Null(again.Problem);
    }

    /// <summary>A book that alone failed at the read kept is tried again at the start, however fresh the rest.</summary>
    [Fact]
    public async Task ContactsKeptWithABookThatFailedAreReadAtTheStart()
    {
        var cache = Cache();
        var reader = new FakeReader(address: "people@example.com");
        reader.Books.Add((new AddressBook("one", "Friends"), [new Person("a", "Ada", null, [], [])]));
        reader.Books.Add((new AddressBook("two", "Work"), [new Person("g", "Grace", null, [], [])]));
        reader.Failing.Add("two");
        using (await Contacts(reader, cache, Now.AddMinutes(-5)))
        {
        }
        reader.Failing.Clear();
        using var again = await Contacts(reader, cache, Now);
        Assert.Equal(2, reader.ContactReads);
        Assert.Equal(["Ada", "Grace"], again.Contacts.Select(person => person.DisplayName));
    }

    // --- To Do ------------------------------------------------------------------------------------

    private static FakeTodo Todo() => new FakeTodo()
        .With("tasks", "Tasks", true, FakeTodo.Task("a", "Call the plumber", minutes: 1), FakeTodo.Task("b", "Buy stamps", minutes: 2));

    private static async Task<TaskStore> Run(FakeTodo todo, PimCache cache, DateTime at)
    {
        var store = new TaskStore(new AccountSet([todo]), new OnlineUpdates(), cache, () => new DateTimeOffset(at));
        store.Keep();
        await store.Idle();
        return store;
    }

    [Fact]
    public async Task TasksRead5MinutesAgoAreNotReadAtTheStartAndOnesRead20MinutesAgoAre()
    {
        var cache = Cache();
        var todo = Todo();
        using (await Run(todo, cache, Now.AddMinutes(-5)))
        {
        }
        todo.Calls.Clear();
        var file = Path.Combine(cache.Folder, PimCache.TasksFile);
        var kept = await File.ReadAllBytesAsync(file);
        using var again = await Run(todo, cache, Now);
        // The bar's to-do lines show as the dock starts: no line opened on an old read.
        again.Refresh(fresh: true);
        await again.Idle();
        Assert.Empty(todo.Calls);
        // Nor is the file written again for nothing (each write is a new DPAPI blob).
        Assert.Equal(kept, await File.ReadAllBytesAsync(file));
        Assert.Equal(["Call the plumber", "Buy stamps"], again.Show("me@outlook.com", null).Open.Select(task => task.Title));
        ArmedFor(Interval - TimeSpan.FromMinutes(5), again.Armed);

        var older = Cache();
        var other = Todo();
        using (await Run(other, older, Now.AddMinutes(-20)))
        {
        }
        other.Calls.Clear();
        using var late = await Run(other, older, Now);
        Assert.Contains("lists", other.Calls);
    }

    /// <summary>A change kept for To Do goes at the start; the lists, read five minutes before, are not read for it.</summary>
    [Fact]
    public async Task AChangeKeptIsSentAtTheStartWithoutReadingWhatIsFresh()
    {
        var cache = Cache();
        var todo = Todo();
        using (var first = await Run(todo, cache, Now.AddMinutes(-5)))
        {
            todo.Failure = new HttpRequestException("offline");
            var shown = first.Show("me@outlook.com", null);
            first.SetDone(shown.Account!, shown.List!, "b", true);
            await first.Idle();
        }
        todo.Failure = null;
        todo.Calls.Clear();

        using var again = await Run(todo, cache, Now);
        Assert.Equal(["update tasks b"], todo.Calls);
        Assert.True(todo.Find("tasks", "b")!.Done);
        Assert.Empty(Assert.Single(cache.ReadTasks()!.Accounts).Queue);
    }
}
