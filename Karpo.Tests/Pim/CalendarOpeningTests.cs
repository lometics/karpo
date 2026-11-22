// When Karpo's calendar reaches the person's accounts, and what it shows when
// they do not answer (Karpo.Dock\Pim\AccountCalendars.cs, CalendarStore.cs).
//
// docs\widgets.md §11: a widget's constructor makes the object; the first look
// starts the work — drawn, hovered or clicked. A placed calendar widget being
// drawn on a real bar IS that first look, so it asks; making one, and telling
// one its settings, are not.
//
// What keeps --smoke and the offscreen pictures away from the accounts is not
// the widget but the store: only CalendarStore.Shared is built with the
// person's accounts, and only when the config is theirs. An isolated run
// (KARPO_CONFIG_DIR, which every test and the smoke pass set) gets a store
// with none, so a widget drawn offscreen may ask as often as it likes.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class CalendarOpeningTests
{
    private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static CalendarStore Store(params IAccountReader[] readers) =>
        new(new AccountCalendars(new AccountSet(readers)));

    [Fact]
    public void AnIsolatedRunsSharedStoreHoldsNoAccountsAtAll()
    {
        // The guarantee the whole of §11 rests on here, asserted where it lives rather than
        // inferred from what some widget happens to call. Every test runs isolated
        // (Karpo.Tests\Isolation.cs), and so does Karpo.exe --smoke.
        Assert.True(Config.IsIsolated);

        Assert.False(CalendarStore.Shared.CanReachAccounts);
        Assert.Empty(AccountSet.Shared.Readers);
        Assert.False(AccountSet.Shared.IsLive);

        CalendarStore.Shared.Open();
        CalendarStore.Shared.Open();

        Assert.False(CalendarStore.Shared.Loading);
        Assert.Null(CalendarStore.Shared.Problem);
        Assert.True(CalendarStore.Shared.Idle().IsCompleted);
    }

    [Fact]
    public async Task AskingForEventsNeverReachesAnAccountByItself()
    {
        var account = new FakeReader();
        var store = Store(account);

        // Reading what is already in memory is not looking: the wider window a view asks for
        // (AccountCalendars.Want) is remembered, and starts nothing until somebody has looked.
        Assert.Empty(store.EventsOn(DateOnly.FromDateTime(DateTime.Today)));
        Assert.Empty(store.EventsIn(DateTime.Today, DateTime.Today.AddDays(7)));
        Assert.Empty(store.Upcoming(DateTime.Now, 5));
        Assert.Empty(store.EventsIn(DateTime.Today.AddYears(3), DateTime.Today.AddYears(3).AddDays(30)));
        store.Deliver();
        await store.Idle();

        Assert.Equal(0, account.CalendarReads);
        Assert.False(store.Loading);
        Assert.False(store.HasCalendars);
    }

    [Fact]
    public async Task TheFirstLookIsWhatReachesTheAccounts()
    {
        var account = new FakeReader();
        var store = Store(account);

        store.Open();
        await store.Idle();

        Assert.Equal(1, account.CalendarReads);
    }

    [Fact]
    public async Task WhileItIsAskingItSaysSoAndCountsNothing()
    {
        // Blank, never wrong: a cell and a card must not say "No events" about a calendar nobody
        // has answered for yet, so Loading is true from the moment of the look until the answer.
        var account = new FakeReader(hold: true);
        var store = Store(account);

        store.Open();

        Assert.True(store.Loading);
        Assert.Null(store.Problem);
        Assert.Empty(store.EventsOn(DateOnly.FromDateTime(DateTime.Today)));

        account.Answer();
        await store.Idle();

        Assert.False(store.Loading);
    }

    [Fact]
    public async Task AnAccountThatDoesNotAnswerIsNamedAndTheCalendarStays()
    {
        var account = new FakeReader(address: "work@example.com") { Failure = new SignInRequiredException("Microsoft wants you to sign in again.") };
        var store = Store(account);

        store.Open();
        await store.Idle();

        Assert.False(store.Loading);
        Assert.False(store.HasCalendars);
        Assert.Equal("work@example.com: Microsoft wants you to sign in again.", store.Problem);
    }

    [Fact]
    public async Task WithNoAccountItSaysWhereToAddOne()
    {
        var store = Store();

        store.Open();
        await store.Idle();

        Assert.Equal(AccountCalendars.NoAccount, store.Problem);
    }

    [Fact]
    public async Task AFailedAccountIsNotAskedAgainAtOnce()
    {
        // A failure rests for a minute, so a widget redrawn on every frame of a hover does not hammer
        // a server that refused it. This is what makes calling Open from a paint safe.
        var account = new FakeReader { Failure = new HttpRequestException("down") };
        var store = Store(account);

        store.Open();
        await store.Idle();
        for (var draw = 0; draw < 50; draw++)
        {
            store.Open();
        }
        await store.Idle();

        Assert.Equal(1, account.CalendarReads);
    }

    [Fact]
    public async Task OneFailingAccountLeavesTheOthersEvents()
    {
        var good = new FakeReader(address: "home@example.com");
        good.Calendars.Add(new CalendarRead(
            new RemoteCalendar("cal", "Home", "#D13438"),
            [new Occurrence("Dentist", null, false, false, new DateTime(2026, 10, 6, 7, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 6, 8, 0, 0, DateTimeKind.Utc))],
            []));
        var bad = new FakeReader(address: "work@example.com") { Failure = new AccountException("Microsoft refused (403).") };
        var store = Store(good, bad);

        store.Open();
        await store.Idle();

        var day = store.EventsOn(new DateOnly(2026, 10, 6), Berlin);
        var dentist = Assert.Single(day);
        Assert.Equal(new DateTime(2026, 10, 6, 9, 0, 0), dentist.Start);
        Assert.Equal("Home", dentist.Source!.Name);
        // The account's own colour, not the palette's.
        Assert.Equal(Windows.UI.Color.FromArgb(255, 0xD1, 0x34, 0x38), dentist.Colour);
        Assert.Equal("work@example.com: Microsoft refused (403).", store.Problem);
    }

    [Fact]
    public async Task ADavCalendarsSeriesIsExpandedInTheZoneItIsShownIn()
    {
        // CalDAV answers the series, not its occurrences: ICalendar expands it, and a weekly 09:00
        // in Berlin stays at 09:00 across the change to winter time.
        var account = new FakeReader(AccountKind.Dav);
        account.Calendars.Add(new CalendarRead(
            new RemoteCalendar("https://dav.example.com/calendars/me/work/", "Work", null),
            [],
            ["BEGIN:VCALENDAR\r\nVERSION:2.0\r\nBEGIN:VEVENT\r\nUID:s\r\nSUMMARY:Standup\r\n"
                + "DTSTART;TZID=Europe/Berlin:20261020T090000\r\nDURATION:PT15M\r\nRRULE:FREQ=WEEKLY;COUNT=3\r\n"
                + "END:VEVENT\r\nEND:VCALENDAR\r\n"]));
        var store = Store(account);

        store.Open();
        await store.Idle();

        var starts = store.EventsIn(new DateTime(2026, 10, 1), new DateTime(2026, 11, 30), Berlin)
            .Select(item => item.Start).ToList();
        Assert.Equal([new DateTime(2026, 10, 20, 9, 0, 0), new DateTime(2026, 10, 27, 9, 0, 0), new DateTime(2026, 11, 3, 9, 0, 0)], starts);
        Assert.Null(store.Problem);
    }

    [Fact]
    public async Task ACalendarKeepsItsObjectAndColourFromReadToRead()
    {
        var account = new FakeReader();
        account.Calendars.Add(new CalendarRead(new RemoteCalendar("a", "A", null), [], []));
        account.Calendars.Add(new CalendarRead(new RemoteCalendar("b", "B", null), [], []));
        var calendars = new AccountCalendars(new AccountSet([account]));

        calendars.Refresh();
        await calendars.Idle();
        var first = calendars.Calendars.ToList();

        // The same answer again, reached by a wider window.
        calendars.Want(DateTime.Today.AddYears(2), DateTime.Today.AddYears(2).AddDays(7));
        await calendars.Idle();

        Assert.Equal(2, account.CalendarReads);
        Assert.Same(first[0], calendars.Calendars[0]);
        Assert.Same(first[1], calendars.Calendars[1]);
        // Neither has a colour of its own: the palette's, by place — blue, then teal (SourceColours).
        Assert.Equal(CalendarColours.Palette[5], first[0].Colour);
        Assert.Equal(CalendarColours.Palette[4], first[1].Colour);
    }
}
