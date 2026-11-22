// The accounts' calendars, mail and contacts synced from the dock's start, widget
// or not (Karpo.Dock\Pim\PimSync.cs, docs\accounts.md "When it reads"): the
// accounts decide, a store with no account rests, and an account removed stops —
// read no more, and dropped from the cache and the list.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class PimSyncTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private static PimCache Cache() => new(Path.Combine(Isolation.Folder, "pim-" + Guid.NewGuid().ToString("N")));

    private static FakeReader Microsoft(string address)
    {
        var reader = new FakeReader(AccountKind.Microsoft, address)
        {
            Inbox = new Inbox(1, [new MailItem("m", "Sandra", "sandra@example.ch", "Lunch", null, new DateTimeOffset(Now), true)]),
        };
        reader.People.Add(new Person("p", "Sandra Keller", null, ["sandra@example.ch"], []));
        reader.Calendars.Add(new CalendarRead(
            new RemoteCalendar("cal", "Home", null),
            [new Occurrence("Dentist", null, false, false, Now.AddDays(1), Now.AddDays(1).AddHours(1))],
            []));
        return reader;
    }

    private static async Task Settled(CalendarStore calendars, MailStore mail, ContactStore contacts)
    {
        await calendars.Idle();
        await mail.Idle();
        await contacts.Idle();
    }

    [Fact]
    public async Task AnAccountIsSyncedFromTheStartWithNoWidgetAtAll()
    {
        var reader = Microsoft("me@example.com");
        var set = new AccountSet([reader]);
        var calendars = new CalendarStore(new AccountCalendars(set, () => Now));
        using var mail = new MailStore(set, now: () => Now);
        using var contacts = new ContactStore(set, now: () => Now);

        // Nothing placed, nothing attached, nothing drawn: only the dock's start. No contacts
        // widget anywhere, and the contacts are read all the same (2026-10-29).
        PimSync.Start(calendars, mail, contacts: contacts);
        await Settled(calendars, mail, contacts);

        Assert.Equal(1, reader.CalendarReads);
        Assert.Equal(1, reader.InboxReads);
        Assert.Equal(1, reader.ContactReads);
        Assert.Single(calendars.EventsOn(DateOnly.FromDateTime(Now.AddDays(1)), TimeZoneInfo.Utc));
        Assert.Equal(1, mail.Unread);
        Assert.Equal(["Sandra Keller"], contacts.Contacts.Select(person => person.DisplayName));
        // And they come back at the interval, not only when somebody looks.
        Assert.NotNull(mail.Armed);
        Assert.NotNull(contacts.Armed);
    }

    /// <summary>
    /// §399 (the person, 2026-10-29: "the settings for the account (which calendars / contact lists to show etc.)
    /// should still be available although there might not be any widget that uses it yet"): with no widget, ⚙ ›
    /// Accounts › the account lists every calendar, address book and inbox with its switch, from what the dock's
    /// start synced; a book turned off there is fetched no more, and says so.
    /// </summary>
    [Fact]
    public async Task WithNoWidgetTheAccountListsEveryPartWithItsSwitch()
    {
        var report = new SyncReport(Path.Combine(Isolation.Folder, "report-" + Guid.NewGuid().ToString("N")));
        var reader = Microsoft("me@example.com");
        var set = new AccountSet([reader]);
        var calendars = new CalendarStore(new AccountCalendars(set, () => Now, report: report));
        using var mail = new MailStore(set, now: () => Now, report: report);
        using var contacts = new ContactStore(set, report: report, now: () => Now);
        PimSync.Start(calendars, mail, contacts: contacts);
        await Settled(calendars, mail, contacts);

        List<(string, string, bool)> Parts(Account account) => [.. AccountSyncRows.Menu(account, report.Read().For(account.Id), Now.ToLocalTime()).Groups
            .SelectMany(group => group.Parts.Select(part => (group.Title, part.Name, part.On)))];
        Assert.Equal(
            [(S.SettingsAccountsGivesCalendars, "Home", true), (S.SettingsAccountsGivesContacts, "Contacts", true), (S.SettingsAccountsGivesMail, S.CoreAccountsInbox, true)],
            Parts(reader.Account));

        var off = reader.Turned(SyncKeys.Book("contacts"), false);
        set.Become([off]);
        await Settled(calendars, mail, contacts);
        Assert.Equal((S.SettingsAccountsGivesContacts, "Contacts", false), Parts(off.Account)[1]);
        Assert.Empty(contacts.Contacts);
    }

    [Fact]
    public async Task WithNoAccountTheStoresRestAndAnAccountAddedIsReadAtOnce()
    {
        var set = new AccountSet([]);
        var accounts = new AccountCalendars(set, () => Now);
        var calendars = new CalendarStore(accounts);
        using var mail = new MailStore(set, now: () => Now);
        using var contacts = new ContactStore(set, now: () => Now);
        PimSync.Start(calendars, mail, contacts: contacts);
        await Settled(calendars, mail, contacts);

        // Nothing to come back for: no timer runs.
        Assert.Null(accounts.Armed);
        Assert.Null(mail.Armed);
        Assert.Null(contacts.Armed);
        Assert.Equal(ContactStore.NoAccount, contacts.Problem);

        var reader = Microsoft("new@example.com");
        set.Become([reader]);
        await Settled(calendars, mail, contacts);

        Assert.Equal(1, reader.CalendarReads);
        Assert.Equal(1, reader.InboxReads);
        Assert.Equal(1, reader.ContactReads);
        Assert.NotNull(accounts.Armed);
        Assert.NotNull(mail.Armed);
        Assert.NotNull(contacts.Armed);
        Assert.Single(contacts.Contacts);
    }

    [Fact]
    public async Task AnAccountRemovedStopsAndIsDroppedFromTheCache()
    {
        var cache = Cache();
        var going = Microsoft("going@example.com");
        var staying = Microsoft("staying@example.com");
        var set = new AccountSet([going, staying]);
        var accounts = new AccountCalendars(set, () => Now, cache: cache);
        var calendars = new CalendarStore(accounts);
        using var mail = new MailStore(set, cache: cache, now: () => Now);
        using var contacts = new ContactStore(set, now: () => Now);
        going.People.Clear();
        going.People.Add(new Person("g", "Gaston Going", null, ["gaston@example.ch"], []));
        PimSync.Start(calendars, mail, contacts: contacts);
        await Settled(calendars, mail, contacts);
        Assert.Equal(2, cache.ReadCalendars()!.Accounts.Count);
        Assert.Equal(2, cache.ReadMail()!.Accounts.Count);
        Assert.Equal(["Gaston Going", "Sandra Keller"], contacts.Contacts.Select(person => person.DisplayName));

        set.Become([staying]);
        await Settled(calendars, mail, contacts);
        var (calendarReads, inboxReads, contactReads) = (going.CalendarReads, going.InboxReads, going.ContactReads);

        Assert.Equal([staying.Account.Id], cache.ReadCalendars()!.Accounts.Select(account => account.Account));
        Assert.Equal([staying.Account.Id], cache.ReadMail()!.Accounts.Select(account => account.Account));
        Assert.Equal(1, mail.Unread);
        Assert.Equal(["Sandra Keller"], contacts.Contacts.Select(person => person.DisplayName));

        // Asked for everything again: the account that went is not.
        calendars.Refresh();
        mail.Refresh(force: true);
        contacts.Reread();
        await Settled(calendars, mail, contacts);
        Assert.Equal((calendarReads, inboxReads, contactReads), (going.CalendarReads, going.InboxReads, going.ContactReads));
        Assert.True(staying.CalendarReads >= 2);
        Assert.True(staying.ContactReads >= 2);

        // The last one gone: the timers stop, and the files and the list keep nobody.
        set.Become([]);
        await Settled(calendars, mail, contacts);
        Assert.Null(accounts.Armed);
        Assert.Null(mail.Armed);
        Assert.Null(contacts.Armed);
        Assert.Empty(contacts.Contacts);
        Assert.Empty(cache.ReadCalendars()!.Accounts);
        Assert.Empty(cache.ReadMail()!.Accounts);
        Assert.Empty(mail.Messages);
        Assert.Empty(calendars.Sources);
    }
}
