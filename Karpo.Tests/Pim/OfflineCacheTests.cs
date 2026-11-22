// The calendar's and the mail's offline cache (Karpo.Dock\Pim\PimCache.cs,
// docs\accounts.md "Offline"): what a good read keeps on disk and how far, that
// a start without a connection shows it, that a failed refresh keeps what was
// shown and says from when, that the next good read clears that, and that the
// file holds what the rows draw and nothing of the account's sign-in.
//
// Every cache here is a folder of its own under the test run's isolated folder
// (Isolation.Folder); nothing reaches %LOCALAPPDATA%\Karpo.

using System.Diagnostics;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class OfflineCacheTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static PimCache Cache() => new(Path.Combine(Isolation.Folder, "pim-" + Guid.NewGuid().ToString("N")));

    private static Occurrence Event(string title, DateTime startUtc, double hours = 1) =>
        new(title, null, false, false, startUtc, startUtc.AddHours(hours));

    private static FakeReader Calendar(params Occurrence[] events)
    {
        var reader = new FakeReader(address: "home@example.com");
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("cal", "Home", "#D13438"), events, []));
        return reader;
    }

    private static CalendarStore Store(FakeReader reader, PimCache cache, Func<DateTime>? now = null) =>
        new(new AccountCalendars(new AccountSet([reader]), now ?? (() => Now), cache: cache));

    private static IReadOnlyList<CalendarEvent> On(CalendarStore store, DateTime day) =>
        store.EventsOn(DateOnly.FromDateTime(day), TimeZoneInfo.Utc);

    private static MailItem Mail(string id, int minutes, bool unread = false) =>
        new(id, $"Sender {id}", $"{id}@example.com", $"Subject {id}", $"PREVIEW-{id}", new DateTimeOffset(Now.AddMinutes(-minutes)), unread);

    // --- Calendar ---------------------------------------------------------------------------------

    [Fact]
    public async Task TheEventsReadAreThereAtTheNextStartWithoutAConnection()
    {
        var cache = Cache();
        var reader = Calendar(Event("Dentist", Now.AddDays(1)));
        var first = Store(reader, cache);
        first.Open();
        await first.Idle();

        reader.Failure = new HttpRequestException("offline");
        var second = Store(reader, cache, () => Now.AddHours(3));
        second.Open();
        await second.Idle();

        var dentist = Assert.Single(On(second, Now.AddDays(1)));
        Assert.Equal("Dentist", dentist.Title);
        Assert.Equal("Home", dentist.Source!.Name);
        Assert.Equal(Windows.UI.Color.FromArgb(255, 0xD1, 0x34, 0x38), dentist.Colour);
        // Shown, so nothing is missing: it is stale, from the first run's read.
        Assert.Null(second.Problem);
        Assert.False(second.Loading);
        Assert.Equal(new Freshness(Now, true), second.Freshness);
    }

    [Fact]
    public async Task ARefreshThatFailsKeepsTheEventsAndSaysFromWhen()
    {
        var reader = Calendar(Event("Standup", Now.AddHours(2)));
        var clock = Now;
        var store = Store(reader, Cache(), () => clock);
        store.Open();
        await store.Idle();
        Assert.Equal(new Freshness(Now, false), store.Freshness);

        clock = Now.AddMinutes(30);
        reader.Failure = new HttpRequestException("no route");
        store.Refresh();
        await store.Idle();

        Assert.Single(On(store, Now));
        Assert.Null(store.Problem);
        Assert.Equal(new Freshness(Now, true), store.Freshness);
        var local = Now.ToLocalTime();
        Assert.Equal(Freshness.Say(local, local), store.Freshness.Tip(local));
        Assert.Null(new Freshness(Now, false).Tip(local));
    }

    [Fact]
    public async Task TheNextGoodReadIsFreshAgain()
    {
        var reader = Calendar(Event("Standup", Now.AddHours(2)));
        var clock = Now;
        var store = Store(reader, Cache(), () => clock);
        store.Open();
        await store.Idle();
        reader.Failure = new HttpRequestException("no route");
        clock = Now.AddMinutes(15);
        store.Refresh();
        await store.Idle();
        Assert.True(store.Freshness.Stale);

        reader.Failure = null;
        reader.Calendars[0] = reader.Calendars[0] with { Occurrences = [Event("Standup", Now.AddHours(2)), Event("Review", Now.AddHours(4))] };
        clock = Now.AddMinutes(20);
        store.Refresh();
        await store.Idle();

        Assert.Equal(new Freshness(Now.AddMinutes(20), false), store.Freshness);
        Assert.Equal(2, On(store, Now).Count);
    }

    /// <summary>
    /// The second of three calendars the account will not give (a shared one taken back): the first and the
    /// third refresh, the second keeps what it held, stale from its last good read — once the whole account
    /// failed then, and stayed as it was for good. Its row on the Accounts page says why.
    /// </summary>
    [Fact]
    public async Task ACalendarThatAloneFailsKeepsItsEventsStaleAndTheOthersRefresh()
    {
        var reader = new FakeReader(address: "work@example.com");
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("one", "Work", null), [Event("Standup", Now.AddHours(2))], []));
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("two", "Shared", null), [Event("Ski trip", Now.AddHours(3))], []));
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("three", "Home", null), [Event("Dentist", Now.AddHours(4))], []));
        var folder = Path.Combine(Isolation.Folder, "report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var report = new SyncReport(folder);
        var clock = Now;
        var store = new CalendarStore(new AccountCalendars(new AccountSet([reader]), () => clock, cache: Cache(), report: report));
        store.Open();
        await store.Idle();

        clock = Now.AddMinutes(30);
        reader.Failing.Add("two");
        reader.Calendars[0] = reader.Calendars[0] with { Occurrences = [Event("Standup moved", Now.AddHours(2))] };
        reader.Calendars[1] = reader.Calendars[1] with { Occurrences = [Event("Never read", Now.AddHours(3))] };
        reader.Calendars[2] = reader.Calendars[2] with { Occurrences = [Event("Dentist", Now.AddHours(4)), Event("Gym", Now.AddHours(5))] };
        store.Refresh();
        await store.Idle();

        Assert.Equal(["Standup moved", "Ski trip", "Dentist", "Gym"], On(store, Now).OrderBy(item => item.Start).Select(item => item.Title));
        Assert.Null(store.Problem);
        // Stale, from the read the second calendar last gave something in.
        Assert.Equal(new Freshness(Now, true), store.Freshness);
        var shared = report.Read().For(reader.Account.Id)!.Calendars!.Items.Single(item => item.Id == "two");
        Assert.Contains(FakeReader.Refused, shared.Problem);
        Assert.Contains("Shared", shared.Problem);

        // It answers again: fresh, all three.
        reader.Failing.Clear();
        clock = Now.AddMinutes(45);
        store.Refresh();
        await store.Idle();
        Assert.Contains(On(store, Now), item => item.Title == "Never read");
        Assert.Equal(new Freshness(Now.AddMinutes(45), false), store.Freshness);
    }

    /// <summary>A calendar failing at its very first read has nothing to keep: what is missing is said, the others shown.</summary>
    [Fact]
    public async Task ACalendarFailingFromTheStartIsSaidToBeMissing()
    {
        var reader = new FakeReader(address: "work@example.com");
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("one", "Work", null), [Event("Standup", Now.AddHours(2))], []));
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("two", "Shared", null), [Event("Ski trip", Now.AddHours(3))], []));
        reader.Failing.Add("two");
        var store = Store(reader, Cache());
        store.Open();
        await store.Idle();

        Assert.Equal(["Standup"], On(store, Now).Select(item => item.Title));
        Assert.Contains("Shared", store.Problem);
        Assert.False(store.Freshness.Stale);
    }

    /// <summary>
    /// A calendar the server gave only part of (a CalDAV 507 it could not get round), or with an event Karpo
    /// cannot show (an hourly rule): what there is is shown, and the calendar says not all of it is.
    /// </summary>
    [Fact]
    public async Task ACalendarNotAllThereSaysSo()
    {
        var cut = new FakeReader(address: "dav@example.com");
        cut.Calendars.Add(new CalendarRead(new RemoteCalendar("big", "Archive", null), [Event("Kept", Now.AddHours(2))], []) { Incomplete = true });
        var store = Store(cut, Cache());
        store.Open();
        await store.Idle();
        Assert.Single(On(store, Now));
        Assert.Contains("Archive", store.Problem);

        var hourly = new FakeReader(address: "dav@example.com");
        hourly.Calendars.Add(new CalendarRead(new RemoteCalendar("odd", "Odd", null), [], [
            "BEGIN:VCALENDAR\r\nBEGIN:VEVENT\r\nUID:1\r\nSUMMARY:Pills\r\nDTSTART:20261009T080000Z\r\nRRULE:FREQ=HOURLY;INTERVAL=8\r\nEND:VEVENT\r\nEND:VCALENDAR\r\n"]));
        var odd = Store(hourly, Cache());
        odd.Open();
        await odd.Idle();
        Assert.Contains("Odd", odd.Problem);
    }

    /// <summary>
    /// The file keeps the window the calendars are read for — last month to a year after this one — and says
    /// so, so the next start finds all its first read would ask for (2026-10-31: no read for a restart).
    /// </summary>
    [Fact]
    public async Task TheCacheKeepsTheWindowTheCalendarsAreReadFor()
    {
        var cache = Cache();
        var reader = Calendar(
            Event("too old", new DateTime(2026, 8, 30, 12, 0, 0, DateTimeKind.Utc)),
            Event("last month", new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc)),
            Event("next autumn", new DateTime(2027, 10, 20, 12, 0, 0, DateTimeKind.Utc)),
            Event("too far", new DateTime(2027, 11, 3, 12, 0, 0, DateTimeKind.Utc)));
        var store = Store(reader, cache);
        store.Open();
        await store.Idle();

        Assert.Equal((new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2027, 11, 1, 0, 0, 0, DateTimeKind.Utc)), reader.Window);
        var account = Assert.Single(cache.ReadCalendars()!.Accounts);
        Assert.Equal(["last month", "next autumn"], Assert.Single(account.Calendars).Events.Select(item => item.Title));
        Assert.Equal(reader.Window, (account.From!.Value, account.To!.Value));
    }

    [Fact]
    public async Task ACalendarKeepsAtMostItsCapOfEvents()
    {
        var cache = Cache();
        var many = Enumerable.Range(0, PimCache.MostEvents + 300).Select(i => Event($"e{i}", Now.AddMinutes(i))).ToArray();
        var store = Store(Calendar(many), cache);
        store.Open();
        await store.Idle();

        var events = Assert.Single(Assert.Single(cache.ReadCalendars()!.Accounts).Calendars).Events;
        Assert.Equal(PimCache.MostEvents, events.Count);
        Assert.Equal("e0", events[0].Title);
    }

    [Fact]
    public async Task AFirstStartWithoutAConnectionSaysSo()
    {
        var reader = Calendar(Event("Dentist", Now.AddDays(1)));
        reader.Failure = new HttpRequestException("offline");
        var store = Store(reader, Cache());
        store.Open();
        await store.Idle();

        Assert.Empty(On(store, Now.AddDays(1)));
        Assert.False(store.Loading);
        Assert.False(store.Freshness.Stale);
        Assert.Equal(S.DockAccountsUnreachable("home@example.com"), store.Problem);
    }

    [Fact]
    public async Task TheViewsReadWhatIsHeldAndNeverWaitForAnAccount()
    {
        var cache = Cache();
        var good = Calendar(Event("Dentist", Now.AddDays(1)));
        var earlier = Store(good, cache);
        earlier.Open();
        await earlier.Idle();

        // A server that does not answer: the look returns at once, the kept events come from the
        // file on the pool, and the views draw them while the read is still out.
        var held = new FakeReader(address: "home@example.com", hold: true);
        var file = cache.ReadCalendars()!;
        file.Accounts[0].Account = held.Account.Id;
        // Read longer ago than the interval: due at the start (one read minutes ago would not be read at all).
        file.Accounts[0].UpdatedUtc = Now.AddHours(-2);
        cache.Write(file);
        var store = Store(held, cache);
        var watch = Stopwatch.StartNew();
        store.Open();
        Assert.True(watch.ElapsedMilliseconds < 500, $"Open took {watch.ElapsedMilliseconds} ms");
        await store.CacheRead();

        Assert.False(store.Idle().IsCompleted);
        watch.Restart();
        Assert.Single(On(store, Now.AddDays(1)));
        Assert.True(watch.ElapsedMilliseconds < 500, $"reading took {watch.ElapsedMilliseconds} ms");
        Assert.False(store.Loading);

        held.Answer();
        await store.Idle();
    }

    [Fact]
    public async Task AnAccountNoLongerHereIsNeitherShownNorKept()
    {
        var cache = Cache();
        var gone = Calendar(Event("Old account's", Now.AddDays(1)));
        var earlier = Store(gone, cache);
        earlier.Open();
        await earlier.Idle();

        var other = new FakeReader(address: "work@example.com");
        other.Calendars.Add(new CalendarRead(new RemoteCalendar("w", "Work", null), [Event("Work's", Now.AddDays(1))], []));
        var store = Store(other, cache);
        store.Open();
        await store.Idle();

        Assert.Equal(["Work's"], On(store, Now.AddDays(1)).Select(item => item.Title));
        Assert.Equal([other.Account.Id], cache.ReadCalendars()!.Accounts.Select(account => account.Account));
    }

    [Fact]
    public async Task ACacheThatDoesNotReadIsNoCache()
    {
        var cache = Cache();
        Directory.CreateDirectory(cache.Folder);
        await File.WriteAllTextAsync(Path.Combine(cache.Folder, PimCache.CalendarFile), "{ not json");
        await File.WriteAllTextAsync(Path.Combine(cache.Folder, PimCache.MailFile), "[1, 2");

        var store = Store(Calendar(Event("Dentist", Now.AddDays(1))), cache);
        store.Open();
        await store.Idle();
        using var mail = new MailStore(new AccountSet([new FakeReader(AccountKind.Imap) { Inbox = new Inbox(1, [Mail("a", 1, true)]) }]), cache: cache);
        mail.Refresh();
        await mail.Idle();

        Assert.Single(On(store, Now.AddDays(1)));
        Assert.Single(mail.Messages);
        Assert.Single(cache.ReadCalendars()!.Accounts);
        Assert.Single(cache.ReadMail()!.Accounts);
    }

    // --- Mail -------------------------------------------------------------------------------------

    [Fact]
    public async Task TheMailReadIsThereAtTheNextStartWithoutAConnection()
    {
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Imap, "me@example.com") { Inbox = new Inbox(4, [Mail("a", 5, true), Mail("b", 50)]) };
        using (var first = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now))
        {
            first.Refresh();
            await first.Idle();
        }

        reader.Failure = new HttpRequestException("offline");
        using var second = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now.AddHours(1));
        second.Refresh();
        await second.Idle();

        Assert.Equal(["Sender a", "Sender b"], second.Messages.Select(message => message.From));
        Assert.Equal(4, second.Unread);
        Assert.True(second.Messages[0].Unread);
        Assert.Null(second.Problem);
        Assert.Equal(new Freshness(Now, true), second.Freshness);
    }

    /// <summary>
    /// §639: each folder the account syncs is kept beside its inbox — its newest as a row shows them, its name, nothing
    /// of a folder not turned on — and a start without a connection shows it, named, on a line that picked it; turned
    /// off since, it is shown nowhere though the file still holds it.
    /// </summary>
    [Fact]
    public async Task TheFoldersSyncedAreThereAtTheNextStartWithoutAConnection()
    {
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Imap, "me@example.com") { Inbox = new Inbox(1, [Mail("a", 5, true)]) };
        reader.Folders.Add((new MailFolder("Archive", "Archive"), new Inbox(2, [Mail("old", 90, true)], 40)));
        reader.Folders.Add((new MailFolder("Receipts", "Receipts"), new Inbox(0, [Mail("receipt", 30)], 7)));
        var synced = reader.Turned(SyncKeys.MailFolder("Archive"), true);
        using (var first = new MailStore(new AccountSet([synced]), cache: cache, now: () => Now))
        {
            first.Refresh();
            await first.Idle();
        }

        var kept = Assert.Single(cache.ReadMail()!.Accounts);
        var folder = Assert.Single(kept.Folders!);
        Assert.Equal(("Archive", "Archive", 2), (folder.Id, folder.Name, folder.Unread));
        Assert.Equal(["Subject old"], folder.Messages.Select(message => message.Subject));

        synced.Failure = new HttpRequestException("offline");
        using var second = new MailStore(new AccountSet([synced]), cache: cache, now: () => Now.AddHours(1));
        second.Refresh();
        await second.Idle();
        var both = Karpo.Core.SourceFilter.Of(["me@example.com|mail", "me@example.com|mail:Archive"]);
        Assert.Equal([("Subject a", "Inbox"), ("Subject old", "Archive")], second.View(both).Messages.Select(message => (message.Subject, message.Folder)));
        Assert.Equal(3, second.View(both).Unread);

        var off = synced.Turned(SyncKeys.MailFolder("Archive"), false);
        using var third = new MailStore(new AccountSet([off]), cache: cache, now: () => Now.AddHours(1));
        third.Refresh();
        await third.Idle();
        Assert.Equal(["Subject a"], third.View(both).Messages.Select(message => message.Subject));
    }

    /// <summary>
    /// §683 (the person, 2026-11-12: "mail folders only shows the inbox which is not correct"): every folder the account
    /// listed is kept, synced or not, and the next start's Mail folders page (the report) lists them all from the file —
    /// before, only the synced ones came back, and none for an account syncing none, till the next read.
    /// </summary>
    [Fact]
    public async Task EveryFolderListedIsBackAtTheNextStart()
    {
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Imap, "me@example.com") { Inbox = new Inbox(1, [Mail("a", 5, true)]) };
        reader.Folders.Add((new MailFolder("Archive", "Archive"), new Inbox(2, [Mail("old", 90, true)], 40)));
        reader.Folders.Add((new MailFolder("Receipts", "Receipts", Unread: 3), new Inbox(0, [Mail("receipt", 30)], 7)));
        using (var first = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now))
        {
            first.Refresh();
            await first.Idle();
        }
        Assert.Equal(["Archive", "Receipts"], Assert.Single(cache.ReadMail()!.Accounts).Listed!.Select(folder => folder.Id));

        var folder = Path.Combine(Isolation.Folder, "report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var report = new SyncReport(folder);
        reader.Failure = new HttpRequestException("offline");
        using var second = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now.AddMinutes(1), report: report);
        second.Refresh();
        await second.Idle();
        var listed = report.Read().For(reader.Account.Id)!.Mail!.Folders!;
        Assert.Equal([("Archive", "Archive"), ("Receipts", "Receipts")], listed.Select(each => (each.Id, each.Name)));
        Assert.True(listed.All(each => each.Off));
    }

    /// <summary>A message's link is kept, so a row read from the file still opens; a file written before rows opened reads as none.</summary>
    [Fact]
    public async Task AMessagesLinkIsKeptAndAFileWithoutOneStillReads()
    {
        var cache = Cache();
        const string Link = "https://outlook.live.com/owa/?ItemID=AAMkAD&exvsurl=1&viewmodel=ReadMessageItem";
        var reader = new FakeReader(AccountKind.Imap, "me@example.com") { Inbox = new Inbox(1, [Mail("a", 5, true) with { Link = Link }, Mail("b", 50)]) };
        using (var first = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now))
        {
            first.Refresh();
            await first.Idle();
        }
        var kept = Assert.Single(cache.ReadMail()!.Accounts);
        Assert.Equal([Link, null], kept.Messages.Select(message => message.Link));
        Assert.Equal(Link, kept.Inbox.Messages[0].Link);

        // As a file of before: no "link" at all.
        var path = Path.Combine(cache.Folder, PimCache.MailFile);
        var file = System.Text.Json.Nodes.JsonNode.Parse(PimCache.Unprotect(await File.ReadAllBytesAsync(path)))!;
        foreach (var message in file["accounts"]![0]!["messages"]!.AsArray())
        {
            message!.AsObject().Remove("link");
        }
        var old = file.ToJsonString();
        Assert.DoesNotContain("\"link\"", old, StringComparison.Ordinal);
        await File.WriteAllBytesAsync(path, PimCache.Protect(System.Text.Encoding.UTF8.GetBytes(old)));
        var read = Assert.Single(cache.ReadMail()!.Accounts);
        Assert.Equal(["Sender a", "Sender b"], read.Inbox.Messages.Select(message => message.From));
        Assert.All(read.Inbox.Messages, message => Assert.Null(message.Link));
    }

    [Fact]
    public async Task EachInboxKeepsItsNewestAndOneFailingKeepsWhatItShowed()
    {
        var cache = Cache();
        var one = new FakeReader(AccountKind.Imap, "one@example.com")
        {
            Inbox = new Inbox(2, [.. Enumerable.Range(0, 50).Select(i => Mail($"one{i}", i, i < 2))]),
        };
        var two = new FakeReader(AccountKind.Imap, "two@example.com") { Inbox = new Inbox(1, [Mail("two", 3, true)]) };
        var clock = Now;
        using var store = new MailStore(new AccountSet([one, two]), cache: cache, now: () => clock);
        store.Refresh();
        await store.Idle();

        var kept = cache.ReadMail()!.Accounts;
        Assert.Equal([MailStore.Newest, 1], kept.Select(account => account.Messages.Count));
        Assert.Equal("Sender one0", kept[0].Messages[0].From);

        clock = Now.AddMinutes(10);
        two.Failure = new HttpRequestException("down");
        store.Refresh(force: true);
        await store.Idle();

        Assert.Contains(store.Messages, message => message.From == "Sender two");
        Assert.Equal(3, store.Unread);
        Assert.Null(store.Problem);
        // Stale from the failing inbox's read, though the other is newer.
        Assert.Equal(new Freshness(Now, true), store.Freshness);
        Assert.Equal(Now, cache.ReadMail()!.Accounts[1].UpdatedUtc);
        Assert.Equal(Now.AddMinutes(10), cache.ReadMail()!.Accounts[0].UpdatedUtc);

        two.Failure = null;
        store.Refresh(force: true);
        await store.Idle();
        Assert.Equal(new Freshness(Now.AddMinutes(10), false), store.Freshness);
    }

    [Fact]
    public async Task AFirstStartWithoutAConnectionSaysSoForTheMail()
    {
        var reader = new FakeReader(AccountKind.Imap, "me@example.com") { Failure = new HttpRequestException("offline") };
        using var store = new MailStore(new AccountSet([reader]), cache: Cache());
        store.Refresh();
        await store.Idle();

        Assert.Empty(store.Messages);
        Assert.False(store.Freshness.Stale);
        Assert.Equal(S.DockAccountsUnreachable("me@example.com"), store.Problem);
    }

    // --- What the files hold ----------------------------------------------------------------------

    [Fact]
    public async Task TheFilesHoldWhatTheRowsShowAndNothingOfTheSignIn()
    {
        const string secret = "AQAAANCMnd8BFdERjHoAwE-refresh-token";
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Microsoft, "secret.person@example.com", secret: secret)
        {
            Inbox = new Inbox(1, [new MailItem("AAMkAGI2-message-id", "Sandra", "sandra@example.ch", "Lunch", "PREVIEW-body text", new DateTimeOffset(Now), true)]),
        };
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("cal-1", "Home", "#D13438"), [Event("Dentist", Now.AddDays(1))], []));
        var calendars = Store(reader, cache);
        calendars.Open();
        await calendars.Idle();
        using var mail = new MailStore(new AccountSet([reader]), cache: cache);
        mail.Refresh();
        await mail.Idle();

        var text = Decrypted(cache, PimCache.CalendarFile) + Decrypted(cache, PimCache.MailFile);
        foreach (var never in new[] { secret, "secret.person@example.com", "AAMkAGI2-message-id", "PREVIEW-body text", "Microsoft" })
        {
            Assert.DoesNotContain(never, text, StringComparison.Ordinal);
        }
        foreach (var shown in new[] { "Dentist", "Home", "#D13438", "Sandra", "sandra@example.ch", "Lunch", reader.Account.Id })
        {
            Assert.Contains(shown, text, StringComparison.Ordinal);
        }
        // And no file of an isolated run's own: the dock's cache is none there.
        Assert.Null(PimCache.ForDock());
    }

    // --- Encrypted for this Windows user ----------------------------------------------------------

    private static string Decrypted(PimCache cache, string name) =>
        System.Text.Encoding.UTF8.GetString(PimCache.Unprotect(File.ReadAllBytes(Path.Combine(cache.Folder, name))));

    /// <summary>Whether <paramref name="bytes"/> hold <paramref name="text"/> as UTF-8 or UTF-16, the ways a string reaches a file.</summary>
    private static bool Holds(byte[] bytes, string text) =>
        bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(text)) >= 0
        || bytes.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes(text)) >= 0;

    [Fact]
    public void TheFilesAreEncryptedAndReadBackAsWritten()
    {
        var cache = Cache();
        var calendars = new CalendarCacheFile();
        calendars.Accounts.Add(new CalendarCacheAccount
        {
            Account = "acc-1",
            UpdatedUtc = Now,
            Calendars = [new CachedCalendar { Id = "cal", Name = "Home", Colour = "#D13438", Events = [CachedEvent.Of(Event("Dentist Dr. Keller", Now.AddDays(1)))] }],
        });
        var mail = new MailCacheFile();
        mail.Accounts.Add(new MailCacheAccount
        {
            Account = "acc-1",
            UpdatedUtc = Now,
            Unread = 3,
            Messages = [CachedMail.Of(new MailItem("id", "Sandra Muster", "sandra@example.ch", "Quarterly salary review", null, new DateTimeOffset(Now), true))],
        });
        cache.Write(calendars);
        cache.Write(mail);

        var bytes = File.ReadAllBytes(Path.Combine(cache.Folder, PimCache.CalendarFile))
            .Concat(File.ReadAllBytes(Path.Combine(cache.Folder, PimCache.MailFile))).ToArray();
        foreach (var plain in new[] { "Dentist Dr. Keller", "Home", "#D13438", "acc-1", "Sandra Muster", "sandra@example.ch", "Quarterly salary review", "\"accounts\"", "\"updatedUtc\"" })
        {
            Assert.False(Holds(bytes, plain), $"the files hold \"{plain}\" in the clear");
        }
        Assert.NotEqual((byte)'{', bytes[0]);
        Assert.False(File.Exists(Path.Combine(cache.Folder, PimCache.CalendarFile + ".tmp")));

        var account = Assert.Single(cache.ReadCalendars()!.Accounts);
        Assert.Equal(("acc-1", Now), (account.Account, account.UpdatedUtc));
        var calendar = Assert.Single(account.Calendars);
        Assert.Equal(("cal", "Home", "#D13438"), (calendar.Id, calendar.Name, calendar.Colour));
        var dentist = Assert.Single(calendar.Events);
        Assert.Equal(Event("Dentist Dr. Keller", Now.AddDays(1)), dentist.Occurrence);
        var inbox = Assert.Single(cache.ReadMail()!.Accounts);
        Assert.Equal(3, inbox.Unread);
        var message = Assert.Single(inbox.Messages);
        Assert.Equal(("Sandra Muster", "sandra@example.ch", "Quarterly salary review", true), (message.From, message.FromAddress, message.Subject, message.Unread));
    }

    [Fact]
    public async Task APlainJsonFileOfBeforeIsNoCacheAndIsReplacedEncrypted()
    {
        var cache = Cache();
        var reader = Calendar(Event("Dentist", Now.AddDays(1)));
        // The file as 40b5622 wrote it: plain JSON, of this very account.
        Directory.CreateDirectory(cache.Folder);
        var plain = new CalendarCacheFile();
        plain.Accounts.Add(new CalendarCacheAccount
        {
            Account = reader.Account.Id,
            UpdatedUtc = Now,
            Calendars = [new CachedCalendar { Id = "cal", Name = "Home", Events = [CachedEvent.Of(Event("Plain old", Now.AddDays(1)))] }],
        });
        await File.WriteAllBytesAsync(
            Path.Combine(cache.Folder, PimCache.CalendarFile),
            System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(plain, PimJson.Default.CalendarCacheFile));
        Assert.Empty(cache.ReadCalendars()!.Accounts);

        // Offline, it shows nothing of it…
        reader.Failure = new HttpRequestException("offline");
        var offline = Store(reader, cache);
        offline.Open();
        await offline.Idle();
        Assert.Empty(On(offline, Now.AddDays(1)));

        // …and the next good read writes over it, encrypted.
        reader.Failure = null;
        var online = Store(reader, cache);
        online.Open();
        await online.Idle();
        Assert.Equal(["Dentist"], Assert.Single(Assert.Single(cache.ReadCalendars()!.Accounts).Calendars).Events.Select(item => item.Title));
        Assert.False(Holds(await File.ReadAllBytesAsync(Path.Combine(cache.Folder, PimCache.CalendarFile)), "Dentist"));
    }

    [Fact]
    public async Task ATamperedFileOrOneNotThisUsersIsNoCache()
    {
        var cache = Cache();
        var reader = new FakeReader(AccountKind.Imap) { Inbox = new Inbox(1, [Mail("a", 1, true)]) };
        using (var first = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now))
        {
            first.Refresh();
            await first.Idle();
        }
        var path = Path.Combine(cache.Folder, PimCache.MailFile);
        var good = await File.ReadAllBytesAsync(path);
        Assert.Single(cache.ReadMail()!.Accounts);

        // One byte changed in the middle of the encrypted data.
        var tampered = (byte[])good.Clone();
        tampered[tampered.Length / 2] ^= 0x5A;
        await File.WriteAllBytesAsync(path, tampered);
        Assert.Empty(cache.ReadMail()!.Accounts);

        // Cut short.
        await File.WriteAllBytesAsync(path, good[..(good.Length / 3)]);
        Assert.Empty(cache.ReadMail()!.Accounts);

        // Protected by DPAPI, but not as this user's Karpo cache: what another user's or another
        // program's blob is to this one (another user's cannot be made in a test).
        var json = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
            new MailCacheFile { Accounts = [new MailCacheAccount { Account = reader.Account.Id, UpdatedUtc = Now }] },
            PimJson.Default.MailCacheFile);
        await File.WriteAllBytesAsync(path, System.Security.Cryptography.ProtectedData.Protect(
            json, "Karpo.Accounts.v1"u8.ToArray(), System.Security.Cryptography.DataProtectionScope.CurrentUser));
        Assert.Empty(cache.ReadMail()!.Accounts);
        await File.WriteAllBytesAsync(path, System.Security.Cryptography.ProtectedData.Protect(
            json, null, System.Security.Cryptography.DataProtectionScope.LocalMachine));
        Assert.Empty(cache.ReadMail()!.Accounts);

        // A start offline over it shows nothing kept, and says why.
        reader.Failure = new HttpRequestException("offline");
        using var second = new MailStore(new AccountSet([reader]), cache: cache, now: () => Now);
        second.Refresh();
        await second.Idle();
        Assert.Empty(second.Messages);
        Assert.Equal(S.DockAccountsUnreachable("me@example.com"), second.Problem);
    }

    // --- Freshness --------------------------------------------------------------------------------

    [Fact]
    public void StaleIsFromTheOldestFailingReadAndFreshFromTheOldestOfAll()
    {
        var at = Now;
        Assert.Equal(new Freshness(at, false), Freshness.Of([(at.AddMinutes(5), false), (at, false)]));
        Assert.Equal(new Freshness(at.AddMinutes(5), true), Freshness.Of([(at, false), (at.AddMinutes(5), true), (at.AddMinutes(9), true)]));
        Assert.Equal(default, Freshness.Of([]));
    }

    [Fact]
    public void TheTipSaysTheTimeTodayAndTheDayBefore()
    {
        var today = new DateTime(2026, 10, 9, 14, 5, 0);
        Assert.Equal($"Last updated {Karpo.Dock.Panels.Nls.Time(today)} — couldn't refresh", Freshness.Say(today, today.AddHours(2)));
        var day = Freshness.Say(today, today.AddDays(1));
        Assert.StartsWith("Last updated ", day, StringComparison.Ordinal);
        Assert.Contains(Karpo.Dock.Panels.Nls.MonthDay(DateOnly.FromDateTime(today)), day, StringComparison.Ordinal);
        Assert.EndsWith("— couldn't refresh", day, StringComparison.Ordinal);
    }
}
