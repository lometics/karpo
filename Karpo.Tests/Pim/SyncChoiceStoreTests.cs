// The dock's stores with a part turned off on the Accounts page (docs\accounts.md, "What is
// synced"): a calendar, an inbox or an address book turned off is shown nowhere, dropped from the
// cache, and listed in the report as off; turned on again, it is read and shown at once. And what
// the stores tell the Accounts page: each part with how many it holds, and when it was read.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class SyncChoiceStoreTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc);

    private static string Folder() => Path.Combine(Isolation.Folder, "sync-store-" + Guid.NewGuid().ToString("N"));

    private static FakeReader Microsoft(string address)
    {
        var reader = new FakeReader(AccountKind.Microsoft, address)
        {
            Inbox = new Inbox(2, [new MailItem("m", "Sandra", "sandra@example.ch", "Lunch from " + address, null, new DateTimeOffset(Now), true)], 40),
        };
        reader.Calendars.Add(new CalendarRead(
            new RemoteCalendar("home", "Home", "#3A96DD"),
            [new Occurrence("Dentist", null, false, false, Now.AddDays(1), Now.AddDays(1).AddHours(1))],
            []));
        reader.Calendars.Add(new CalendarRead(
            new RemoteCalendar("work", "Work", null),
            [
                new Occurrence("Standup", null, false, false, Now.AddDays(1).AddHours(2), Now.AddDays(1).AddHours(3)),
                new Occurrence("Review", null, false, false, Now.AddDays(2), Now.AddDays(2).AddHours(1)),
            ],
            []));
        return reader;
    }

    [Fact]
    public async Task ACalendarTurnedOffIsShownNowhereKeptNowhereAndComesBackWhenTurnedOn()
    {
        var folder = Folder();
        var cache = new PimCache(folder);
        var report = new SyncReport(folder);
        var reader = Microsoft("me@example.com");
        var set = new AccountSet([reader]);
        var calendars = new CalendarStore(new AccountCalendars(set, () => Now, cache: cache, report: report));
        calendars.Open();
        await calendars.Idle();

        Assert.Equal(["Home", "Work"], calendars.Sources.Select(source => source.Name));
        var told = report.Read().For(reader.Account.Id)!.Calendars!;
        Assert.Equal([("Home", 1, false), ("Work", 2, false)], told.Items.Select(item => (item.Name, item.Count ?? -1, item.Off)));
        Assert.Equal(Now, told.UpdatedUtc);
        Assert.NotNull(told.FromUtc);

        var off = reader.Turned(SyncKeys.Calendar("work"), false);
        set.Become([off]);
        await calendars.Idle();

        Assert.Equal(["Home"], calendars.Sources.Select(source => source.Name));
        Assert.Single(calendars.EventsOn(DateOnly.FromDateTime(Now.AddDays(1)), TimeZoneInfo.Utc));
        Assert.Equal(["home"], cache.ReadCalendars()!.Accounts.Single().Calendars.Select(calendar => calendar.Id));
        told = report.Read().For(reader.Account.Id)!.Calendars!;
        Assert.Equal([("Home", 1, false), ("Work", -1, true)], told.Items.Select(item => (item.Name, item.Count ?? -1, item.Off)));

        set.Become([off.Turned(SyncKeys.Calendar("work"), true)]);
        await calendars.Idle();

        Assert.Equal(["Home", "Work"], calendars.Sources.Select(source => source.Name));
        Assert.Equal(2, cache.ReadCalendars()!.Accounts.Single().Calendars.Count);
    }

    [Fact]
    public async Task ACalendarTurnedOffIsHiddenAtOnceEvenWhenTheAccountCannotBeReached()
    {
        var reader = Microsoft("me@example.com");
        var set = new AccountSet([reader]);
        var calendars = new CalendarStore(new AccountCalendars(set, () => Now));
        calendars.Open();
        await calendars.Idle();

        // Offline: the read that follows the turn fails, and what was read before stays — but not the calendar turned off.
        var off = reader.Turned(SyncKeys.Calendar("work"), false);
        off.Failure = new HttpRequestException("offline on purpose (SyncChoiceStoreTests)");
        set.Become([off]);
        await calendars.Idle();

        Assert.Equal(["Home"], calendars.Sources.Select(source => source.Name));
    }

    [Fact]
    public async Task AnInboxTurnedOffIsNotReadAndItsMailGoesFromTheListAndTheFile()
    {
        var folder = Folder();
        var cache = new PimCache(folder);
        var report = new SyncReport(folder);
        var mine = Microsoft("me@example.com");
        var work = Microsoft("work@example.com");
        var set = new AccountSet([mine, work]);
        using var mail = new MailStore(set, cache: cache, now: () => Now, report: report);
        mail.Keep();
        await mail.Idle();

        Assert.Equal(4, mail.Unread);
        var inbox = report.Read().For(work.Account.Id)!.Mail!.Items.Single();
        Assert.Equal((SyncKeys.Inbox, 40, 2), (inbox.Id, inbox.Count ?? -1, inbox.Unread ?? -1));

        var workOff = work.Turned(SyncKeys.Inbox, false);
        set.Become([mine, workOff]);
        await mail.Idle();

        Assert.Equal(0, workOff.InboxReads);
        Assert.Equal(2, mail.Unread);
        Assert.DoesNotContain(mail.Messages, message => message.Subject.Contains("work@", StringComparison.Ordinal));
        Assert.Equal([mine.Account.Id], cache.ReadMail()!.Accounts.Select(account => account.Account));
        Assert.Null(report.Read().For(work.Account.Id));

        // Every inbox off: no mail, said as such — not "add an account".
        set.Become([mine.Turned(SyncKeys.Inbox, false), workOff]);
        await mail.Idle();
        Assert.Empty(mail.Messages);
        Assert.True(mail.HasAccount);
        Assert.Equal(MailStore.AllOff, mail.Problem);
    }

    [Fact]
    public async Task AnAddressBookTurnedOffGivesNobodyAndIsToldAsOff()
    {
        var folder = Folder();
        var report = new SyncReport(folder);
        var reader = new FakeReader(AccountKind.Dav, "me@example.com");
        reader.Books.Add((new AddressBook("family", "Family"), [new Person("1", "Ann", null, ["ann@example.com"], [])]));
        reader.Books.Add((new AddressBook("work", "Work"), [new Person("2", "Bob", null, ["bob@example.com"], []), new Person("3", "Cy", null, ["cy@example.com"], [])]));
        var set = new AccountSet([reader]);
        var contacts = new ContactStore(set, report: report, now: () => Now);
        contacts.Refresh();
        await contacts.Idle();

        Assert.Equal(["Ann", "Bob", "Cy"], contacts.Contacts.Select(person => person.DisplayName));
        Assert.Equal([("Family", 1), ("Work", 2)], report.Read().For(reader.Account.Id)!.Contacts!.Items.Select(item => (item.Name, item.Count ?? -1)));

        set.Become([reader.Turned(SyncKeys.Book("work"), false)]);
        await contacts.Idle();

        Assert.Equal(["Ann"], contacts.Contacts.Select(person => person.DisplayName));
        Assert.Equal([("Family", 1, false), ("Work", -1, true)],
            report.Read().For(reader.Account.Id)!.Contacts!.Items.Select(item => (item.Name, item.Count ?? -1, item.Off)));
    }

    /// <summary>
    /// §687: a Google account whose person unticked a kind on Google's consent screen: that kind's store tells the Accounts
    /// page it waits for the consent (Reconnect) — not a failure, so no "Couldn't sync" —, while its widget names the kind
    /// missing; signed in to again with it granted, the next read is told as any.
    /// </summary>
    [Fact]
    public async Task AKindNeverGrantedIsToldAsConsentNotAsAFailure()
    {
        var folder = Folder();
        var report = new SyncReport(folder);
        var reader = new FakeReader(AccountKind.Google, "me@gmail.com") { Failure = new ConsentException(AccountKind.Google, AccountData.Calendars) };
        reader.Books.Add((new AddressBook("myContacts", "Contacts"), [new Person("people/c1", "Ann", null, ["ann@example.com"], [])]));
        var set = new AccountSet([reader]);
        var calendars = new CalendarStore(new AccountCalendars(set, () => Now, report: report));
        calendars.Open();
        await calendars.Idle();

        var told = report.Read().For(reader.Account.Id)!.Calendars!;
        Assert.Equal((true, null), (told.Consent, told.Problem));
        Assert.Contains("Google Calendar", calendars.Problem);

        reader.Failure = new ConsentException(AccountKind.Google, AccountData.Mail);
        using var mail = new MailStore(set, now: () => Now, report: report);
        mail.Keep();
        await mail.Idle();
        Assert.Equal((true, null), (report.Read().For(reader.Account.Id)!.Mail!.Consent, report.Read().For(reader.Account.Id)!.Mail!.Problem));

        reader.Failure = new ConsentException(AccountKind.Google, AccountData.Contacts);
        var contacts = new ContactStore(set, report: report, now: () => Now);
        contacts.Refresh();
        await contacts.Idle();
        Assert.True(report.Read().For(reader.Account.Id)!.Contacts!.Consent);

        reader.Failure = null;
        contacts.Reread();
        await contacts.Idle();
        told = report.Read().For(reader.Account.Id)!.Contacts!;
        Assert.Equal((null, null), (told.Consent, told.Problem));
        Assert.Equal(["Ann"], contacts.Contacts.Select(person => person.DisplayName));
    }
}
