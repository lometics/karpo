// The dock's stores wearing the colours of the Accounts page (docs\accounts.md, "Colours"): a
// calendar's events its pick, else the account's colour, else the palette's; the messages of several
// inboxes and the people of several address books their inbox's and book's colour, one alone none;
// and a colour picked drawn at once, with nothing read again.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class PartColourStoreTests
{
    private static readonly DateTime Now = new(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

    private static FakeReader Calendars()
    {
        var reader = new FakeReader(AccountKind.Microsoft, "me@example.com");
        reader.Calendars.Add(new CalendarRead(
            new RemoteCalendar("home", "Home", "#E3008C"),
            [new Occurrence("Dentist", null, false, false, Now.AddDays(1), Now.AddDays(1).AddHours(1))],
            []));
        reader.Calendars.Add(new CalendarRead(
            new RemoteCalendar("work", "Work", null),
            [new Occurrence("Standup", null, false, false, Now.AddDays(1).AddHours(2), Now.AddDays(1).AddHours(3))],
            []));
        return reader;
    }

    private static FakeReader Mail(string address) => new(AccountKind.Imap, address)
    {
        Inbox = new Inbox(1, [new MailItem("m", "Sandra", "sandra@example.ch", "Lunch from " + address, null, new DateTimeOffset(Now), true)], 10),
    };

    [Fact]
    public async Task ACalendarWearsItsPickElseTheAccountsColourElseThePalettesAndAPickIsDrawnWithoutARead()
    {
        var reader = Calendars();
        var set = new AccountSet([reader]);
        var calendars = new CalendarStore(new AccountCalendars(set, () => Now));
        calendars.Open();
        await calendars.Idle();

        var day = DateOnly.FromDateTime(Now.AddDays(1));
        Windows.UI.Color Of(string title) => calendars.EventsOn(day, TimeZoneInfo.Utc).Single(item => item.Title == title).Colour;
        // Home has Outlook's own pink; Work none, so the palette's first: blue.
        Assert.Equal(CalendarColours.Of("#E3008C"), Of("Dentist"));
        Assert.Equal(CalendarColours.Of(SourceColours.Palette[5]), Of("Standup"));

        var picked = reader.Coloured(SyncKeys.Calendar("work"), "#498205");
        var changed = 0;
        calendars.Changed += () => changed++;
        set.Become([picked]);
        await calendars.Idle();

        Assert.Equal(CalendarColours.Of("#498205"), Of("Standup"));
        Assert.Equal(CalendarColours.Of("#E3008C"), Of("Dentist"));
        Assert.Equal(0, picked.CalendarReads);
        Assert.Equal(1, reader.CalendarReads);
        // The same source on screen, in its new colour.
        Assert.Equal(CalendarColours.Of("#498205"), calendars.Sources.Single(source => source.Name == "Work").Colour);
    }

    [Fact]
    public async Task TheMessagesOfSeveralInboxesWearTheirInboxsColourAndOfOneNone()
    {
        var mine = Mail("me@example.com");
        var work = Mail("work@example.com");
        var set = new AccountSet([mine]);
        using var mail = new MailStore(set, now: () => Now);
        mail.Keep();
        await mail.Idle();
        Assert.Null(mail.Messages.Single().Colour);

        set.Become([mine, work]);
        await mail.Idle();
        string? Of(string address) => mail.Messages.Single(message => message.Subject.EndsWith(address, StringComparison.Ordinal)).Colour;
        Assert.Equal(SourceColours.Palette[5], Of("me@example.com"));
        Assert.Equal(SourceColours.Palette[4], Of("work@example.com"));

        var reads = work.InboxReads;
        var picked = work.Coloured(SyncKeys.Inbox, "#D13438");
        set.Become([mine, picked]);
        Assert.Equal("#D13438", Of("work@example.com"));
        Assert.Equal(SourceColours.Palette[5], Of("me@example.com"));
        Assert.Equal(0, picked.InboxReads);
        Assert.Equal(reads, work.InboxReads);
    }

    [Fact]
    public async Task ThePeopleOfSeveralBooksWearTheirBooksColourAndOfOneNone()
    {
        var reader = new FakeReader(AccountKind.Dav, "me@example.com");
        reader.Books.Add((new AddressBook("family", "Family"), [new Person("1", "Ann", null, ["ann@example.com"], [])]));
        reader.Books.Add((new AddressBook("work", "Work"), [new Person("2", "Bob", null, ["bob@example.com"], [])]));
        var set = new AccountSet([reader]);
        var contacts = new ContactStore(set, now: () => Now);
        contacts.Refresh();
        await contacts.Idle();

        Assert.Equal([SourceColours.Palette[5], SourceColours.Palette[4]], contacts.Contacts.Select(person => person.Colour));

        set.Become([reader.Coloured(SyncKeys.Book("work"), "#7A7574")]);
        Assert.Equal([SourceColours.Palette[5], "#7A7574"], contacts.Contacts.Select(person => person.Colour));
        Assert.Equal(1, reader.ContactReads);

        // One book left on: nobody is marked.
        set.Become([reader.Turned(SyncKeys.Book("work"), false)]);
        await contacts.Idle();
        Assert.Equal(["Ann"], contacts.Contacts.Select(person => person.DisplayName));
        Assert.Null(contacts.Contacts.Single().Colour);
    }
}
