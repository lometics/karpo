// §455: a widget shows from what it picked of the accounts' parts ("Show from"), the person, 2026-10-31: "for the
// widgets like the contacts widget for example i should be able to select on a widget level which data / accounts /
// email folders to show. do this for the similar widgets as well."
//
// Held here over fake accounts (FakeReader), no server reached: a contacts line limited to one address book shows
// that book's people; a mail line limited to a folder shows that folder's messages and unread count, a folder read
// where the person turned it on in ⚙ › Accounts and only then offered (§639), several folders merged by date with
// each row's folder named; a calendar's agenda and the next meeting limited to one calendar show its
// events; a widget that picked nothing shows what it always did. And the menu: "Show from" lists the synced parts
// under each account's name, each a switch that stays on its level, nothing written while it is every default.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Pim;

public sealed class WidgetSourcesTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 9, 0, 0, DateTimeKind.Utc);

    private const string Ann = "ann@example.com";

    private const string Work = "ann@contoso.com";

    private static string Key(string address, string part) => WidgetSources.Key(address, part);

    // --- Contacts ------------------------------------------------------------------------------------

    /// <summary>One account, two address books: Home's Ada, Work's Bob.</summary>
    private static FakeReader TwoBooks()
    {
        var reader = new FakeReader(AccountKind.Microsoft, Ann);
        reader.Books.Add((new AddressBook("home", "Home"), [new Person("a", "Ada Home", null, ["ada@home.example"], [])]));
        reader.Books.Add((new AddressBook("work", "Work"), [new Person("b", "Bob Work", null, ["bob@work.example"], [])]));
        return reader;
    }

    [UiFact]
    public async Task AContactsLineLimitedToOneBookShowsOnlyThatBooksPeople()
    {
        using var store = new ContactStore(new AccountSet([TwoBooks()]));
        store.Refresh();
        await store.Idle();

        Offscreen.OnStaThread(gfx =>
        {
            var all = ListKind.Contacts.New();
            using var every = new ContactsLine("test/all", all, gfx, new Config(), store);
            Assert.Equal(["Ada Home", "Bob Work"], every.Matches.Select(person => person.DisplayName));

            var one = ListKind.Contacts.New();
            one.Sources = [Key(Ann, SyncKeys.Book("work"))];
            using var work = new ContactsLine("test/work", one, gfx, new Config(), store);
            Assert.Equal(["Bob Work"], work.Matches.Select(person => person.DisplayName));
        });
    }

    // --- Mail ----------------------------------------------------------------------------------------

    /// <summary>One account with its inbox and two folders, Archive and Receipts: one message each, an hour apart.</summary>
    private static FakeReader TwoFolders()
    {
        MailItem Message(string subject, bool unread, int hoursAgo) => new(subject, "Sandra", "sandra@example.ch", subject, null, new DateTimeOffset(Now.AddHours(-hoursAgo)), unread);
        var reader = new FakeReader(AccountKind.Imap, Ann) { Inbox = new Inbox(1, [Message("In the inbox", true, 0)], 10) };
        reader.Folders.Add((new MailFolder("Archive", "Archive", Unread: 3, Total: 40), new Inbox(3, [Message("In the archive", true, 1)], 40)));
        reader.Folders.Add((new MailFolder("Receipts", "Receipts", Unread: 0, Total: 7), new Inbox(0, [Message("A receipt", false, 2)], 7)));
        return reader;
    }

    [UiFact]
    public async Task AMailLineLimitedToAFolderShowsThatFoldersMessages()
    {
        // §639: the archive turned on in ⚙ › Accounts, so synced; a line picks it.
        var reader = TwoFolders().Turned(SyncKeys.MailFolder("Archive"), true);
        using var store = new MailStore(new AccountSet([reader]));
        var line = ListKind.Mail.New();
        line.Sources = [Key(Ann, SyncKeys.MailFolder("Archive"))];
        store.Refresh();
        await store.Idle();

        Assert.Equal(["Archive"], reader.Asked);
        Offscreen.OnStaThread(gfx =>
        {
            using var limited = new MailIsland("test/archive", line, gfx, new Config(), store);
            Assert.Equal(["In the archive"], limited.Shown.Messages.Select(message => message.Subject));
            Assert.Equal(3, limited.Shown.Unread);
            // One folder shown: no folder named on its rows.
            Assert.All(limited.Shown.Messages, message => Assert.Null(message.Folder));

            // Nothing picked: the inbox, as ever, and its count.
            using var plain = new MailIsland("test/inbox", ListKind.Mail.New(), gfx, new Config(), store);
            Assert.Equal(["In the inbox"], plain.Shown.Messages.Select(message => message.Subject));
            Assert.Equal(1, plain.Shown.Unread);
            Assert.Equal(1, store.Unread);
        });
    }

    /// <summary>
    /// §639: a folder is read only where the person turned it on in ⚙ › Accounts — none by default, every folder listed
    /// for the Accounts page with its counts —; turned on, it is read at once. §644: "Show from" offers it either way, saying
    /// whether it is synced. Turned off, it shows nowhere, a line that picked it too.
    /// </summary>
    [Fact]
    public async Task AFolderIsReadWhereTurnedOnAndOfferedToShowFromSayingWhetherItIsSynced()
    {
        var reader = TwoFolders();
        var set = new AccountSet([reader]);
        var folder = Path.Combine(Isolation.Folder, "sources-" + Guid.NewGuid().ToString("N"));
        var report = new SyncReport(folder);
        using var store = new MailStore(set, report: report);
        store.Keep();
        await store.Idle();

        // Nothing turned on: no folder read, every one listed as off with what the account counted; §644 offered, not synced.
        Assert.Empty(reader.Asked);
        var told = report.Read().For(reader.Account.Id)!.Mail!;
        Assert.Equal([("Archive", true, 40, 3), ("Receipts", true, 7, 0)], told.Folders!.Select(item => (item.Name, item.Off, item.Count ?? -1, item.Unread ?? -1)));
        Assert.Equal([("Inbox", true, SourceSync.Synced), ("Archive", false, SourceSync.Off), ("Receipts", false, SourceSync.Off)],
            SourceParts.Of([reader.Account], report.Read(), AccountShows.Mail).Select(part => (part.Name, part.Default, part.Sync)));

        // The receipts turned on: read at once, synced beside the inbox, off until a line picks it.
        var receipts = reader.Turned(SyncKeys.MailFolder("Receipts"), true);
        set.Become([receipts]);
        await store.Idle();
        Assert.Equal(["Receipts"], receipts.Asked);
        Assert.False(report.Read().For(reader.Account.Id)!.Mail!.Folders!.Single(item => item.Name == "Receipts").Off);
        var parts = SourceParts.Of([receipts.Account], report.Read(), AccountShows.Mail);
        Assert.Equal([("Inbox", true, SourceSync.Synced), ("Archive", false, SourceSync.Off), ("Receipts", false, SourceSync.Synced)], parts.Select(part => (part.Name, part.Default, part.Sync)));
        Assert.Equal([Key(Ann, SyncKeys.Inbox), Key(Ann, "mail:Archive"), Key(Ann, "mail:Receipts")], parts.Select(part => part.Key));
        var both = SourceFilter.Of([Key(Ann, SyncKeys.Inbox), Key(Ann, "mail:Receipts")]);
        Assert.Equal(["In the inbox", "A receipt"], store.View(both).Messages.Select(message => message.Subject));
        Assert.Equal(1, store.Unread);

        // Turned off again: gone from every line, the one that picked it too.
        set.Become([receipts.Turned(SyncKeys.MailFolder("Receipts"), false)]);
        await store.Idle();
        Assert.Equal(["In the inbox"], store.View(both).Messages.Select(message => message.Subject));
    }

    /// <summary>
    /// §639: a line showing more than one folder lists their newest merged by date, each row naming its folder
    /// ("Archive · 1h"); a line of inboxes alone, or of one folder, names none. The unread count and the mail face's badge
    /// are of what the line picked. With the inbox turned off and a folder on, the account still syncs that folder.
    /// </summary>
    [Fact]
    public async Task ALineOfSeveralFoldersNamesEachRowsFolderAndTheBadgeCountsWhatItShows()
    {
        var reader = TwoFolders().Turned(SyncKeys.MailFolder("Archive"), true).Turned(SyncKeys.MailFolder("Receipts"), true);
        var set = new AccountSet([reader]);
        using var store = new MailStore(set);
        store.Refresh();
        await store.Idle();

        var picked = SourceFilter.Of([Key(Ann, SyncKeys.Inbox), Key(Ann, "mail:Archive"), Key(Ann, "mail:Receipts")]);
        var view = store.View(picked);
        Assert.Equal([("In the inbox", "Inbox"), ("In the archive", "Archive"), ("A receipt", "Receipts")],
            view.Messages.Select(message => (message.Subject, message.Folder)));
        Assert.Equal(4, view.Unread);
        Assert.Equal(S.SettingsAccountsSyncPair(first: "Archive", second: MailIsland.When(view.Messages[1], Now.ToLocalTime())),
            MailIsland.Stamp(view.Messages[1], Now.ToLocalTime(), time: true));
        Assert.Equal("Archive", MailIsland.Stamp(view.Messages[1], Now.ToLocalTime(), time: false));
        Assert.All(store.Messages, message => Assert.Null(message.Folder));

        // The face counts what its folder's mail line shows; every inbox where it picked nothing.
        Assert.Equal((false, "4", false), MailFaceDrawing.Look(store, picked));
        Assert.Equal((false, "1", false), MailFaceDrawing.Look(store));

        // The inbox off, the folders on: the folders are read, the inbox nowhere.
        var folderOnly = reader.Turned(SyncKeys.Inbox, false);
        set.Become([folderOnly]);
        await store.Idle();
        Assert.Equal(1, folderOnly.InboxReads);
        Assert.Equal(["In the archive", "A receipt"], store.View(picked).Messages.Select(message => message.Subject));
        Assert.Empty(store.Messages);
    }

    // --- Calendars -----------------------------------------------------------------------------------

    /// <summary>One account, two calendars: Home's dentist, Work's standup, both tomorrow.</summary>
    private static FakeReader TwoCalendars()
    {
        var reader = new FakeReader(AccountKind.Microsoft, Ann);
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("home", "Home", "#3A96DD"),
            [new Occurrence("Dentist", null, false, false, Now.AddDays(1), Now.AddDays(1).AddHours(1))], []));
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("work", "Work", null),
            [new Occurrence("Standup", null, false, false, Now.AddDays(1).AddHours(2), Now.AddDays(1).AddHours(3), Join: "https://teams.microsoft.com/l/meetup-join/1")], []));
        return reader;
    }

    [UiFact]
    public async Task ACalendarLimitedToOneCalendarShowsOnlyItsEvents()
    {
        var store = new CalendarStore(new AccountCalendars(new AccountSet([TwoCalendars()]), () => Now));
        store.Open();
        await store.Idle();
        var now = Now.ToLocalTime();

        Offscreen.OnStaThread(gfx =>
        {
            CalendarIsland.Made = store;
            try
            {
                using var every = new CalendarIsland("test/all", ListKind.Calendar.New(), gfx, new Config());
                Assert.Equal(["Dentist", "Standup"], every.Upcoming(now).Select(item => item.Title));

                var work = ListKind.Calendar.New();
                work.Sources = [Key(Ann, SyncKeys.Calendar("work"))];
                using var limited = new CalendarIsland("test/work", work, gfx, new Config());
                Assert.Equal(["Standup"], limited.Upcoming(now).Select(item => item.Title));

                var home = ListKind.Calendar.New();
                home.Sources = [Key(Ann, SyncKeys.Calendar("home"))];
                using var mine = new CalendarIsland("test/home", home, gfx, new Config());
                Assert.Equal(["Dentist"], mine.Upcoming(now).Select(item => item.Title));
            }
            finally
            {
                CalendarIsland.Made = null;
            }
        });

        // The next meetings read the same: Work's alone where only Work is shown.
        Assert.Equal(["Dentist", "Standup"], Meetings.Ahead(store, now, 2).Select(meeting => meeting.Event.Title));
        Assert.Equal(["Standup"], Meetings.Ahead(store, now, 2, shown: SourceFilter.Of([Key(Ann, SyncKeys.Calendar("work"))])).Select(meeting => meeting.Event.Title));
    }

    // --- The menu ------------------------------------------------------------------------------------

    /// <summary>Two accounts' calendars and the public holidays, as the dock lists them for a calendar's menu.</summary>
    private static List<SourcePart> Calendars() =>
    [
        new(Key(Ann, SyncKeys.Calendar("home")), "Home", "Ann Smith"),
        new(Key(Ann, SyncKeys.Calendar("birthdays")), "Birthdays", "Ann Smith"),
        new(Key(Work, SyncKeys.Calendar("work")), "Calendar", "Contoso"),
        new(WidgetSources.Key(WidgetSources.HolidaysGroup, "CH"), "Switzerland", WidgetSources.HolidaysGroup),
    ];

    /// <summary>The rows of <paramref name="item"/>'s "Show from", as its menu shows them.</summary>
    private static FlyoutItem ShowFrom(WidgetItem item, IReadOnlyList<SourcePart> parts, List<OptionPick> picks) =>
        OptionMenu.Rows(OptionRows.Of(item, new OptionDevices { Parts = parts }), picks).Single(row => row.Text == S.DockMenuShowFromTitle);

    [Fact]
    public void ShowFromListsTheSyncedPartsUnderEachAccountsNameEachASwitch()
    {
        var line = ListKind.Calendar.New();
        List<OptionPick> picks = [];
        var row = ShowFrom(line, Calendars(), picks);

        // Nothing stored: every calendar on, "All" at the row's end.
        Assert.Null(line.Sources);
        Assert.Equal(S.DockMenuShowFromAll, row.Value!.Text);
        Assert.Equal(
            ["# Ann Smith", "Home on", "Birthdays on", "# Contoso", "Calendar on", $"# {S.CoreHolidaysName}", "Switzerland on"],
            row.Children!.Select(child => child.Note ? "# " + child.Text : $"{child.Text} {((FlyoutControl.Toggle)child.Control!).On switch { true => "on", false => "off" }}"));

        // One turned off: the rest written, the row saying how many; on again: nothing written.
        var birthdays = row.Children!.Single(child => child.Text == "Birthdays");
        Assert.True(OptionMenu.Apply(line, OptionMenu.PickOf(picks, birthdays.Id)!, new OptionDevices { Parts = Calendars() }));
        Assert.Equal([Key(Ann, SyncKeys.Calendar("home")), Key(Work, SyncKeys.Calendar("work")), "holidays|CH"], line.Sources);
        Assert.Equal(S.DockMenuShowFromSome(3, 4), ShowFrom(line, Calendars(), []).Value!.Text);
        picks.Clear();
        birthdays = ShowFrom(line, Calendars(), picks).Children!.Single(child => child.Text == "Birthdays");
        Assert.False(((FlyoutControl.Toggle)birthdays.Control!).On);
        Assert.True(OptionMenu.Apply(line, OptionMenu.PickOf(picks, birthdays.Id)!, new OptionDevices { Parts = Calendars() }));
        Assert.Null(line.Sources);
    }

    [Fact]
    public void AMailLinesFoldersAreOffUntilPickedAndOneAccountHasNoHeading()
    {
        List<SourcePart> parts =
        [
            new(Key(Ann, SyncKeys.Inbox), S.CoreAccountsInbox, "Ann Smith"),
            new(Key(Ann, SyncKeys.MailFolder("Archive")), "Archive", "Ann Smith", Default: false),
            new(Key(Ann, SyncKeys.MailFolder("Inbox/Projects")), "Inbox / Projects", "Ann Smith", Default: false),
        ];
        var line = ListKind.Mail.New();
        List<OptionPick> picks = [];
        var row = ShowFrom(line, parts, picks);

        Assert.Equal(S.DockMenuShowFromInboxes, row.Value!.Text);
        Assert.DoesNotContain(row.Children!, child => child.Header || child.Note);
        Assert.Equal([true, false, false], row.Children!.Select(child => ((FlyoutControl.Toggle)child.Control!).On));

        // The archive on beside the inbox; then the inbox off: the archive alone, named at the row's end.
        Assert.True(OptionMenu.Apply(line, OptionMenu.PickOf(picks, row.Children![1].Id)!, new OptionDevices { Parts = parts }));
        Assert.Equal([Key(Ann, SyncKeys.Inbox), Key(Ann, "mail:Archive")], line.Sources);
        picks.Clear();
        row = ShowFrom(line, parts, picks);
        Assert.True(OptionMenu.Apply(line, OptionMenu.PickOf(picks, row.Children![0].Id)!, new OptionDevices { Parts = parts }));
        Assert.Equal([Key(Ann, "mail:Archive")], line.Sources);
        Assert.Equal("Archive", ShowFrom(line, parts, []).Value!.Text);

        // The last one stays: a widget shows from something.
        picks.Clear();
        row = ShowFrom(line, parts, picks);
        Assert.False(OptionMenu.Apply(line, OptionMenu.PickOf(picks, row.Children![1].Id)!, new OptionDevices { Parts = parts }));
        Assert.Equal([Key(Ann, "mail:Archive")], line.Sources);
    }

    [Fact]
    public void OnlyTheAccountsWidgetsHaveShowFromAndTheFileKeepsWhatWasPicked()
    {
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            var line = kind.New();
            line.Sources = ["x|calendar:y"];
            line.Normalize();
            var shows = WidgetSources.KindOf(line) != AccountShows.None;
            Assert.Equal(shows, line.Sources is not null);
            Assert.Equal(kind is ListKind.Calendar or ListKind.Month or ListKind.Events or ListKind.DayView or ListKind.WeekView or ListKind.YearView
                or ListKind.Mail or ListKind.Contacts or ListKind.Meeting, shows);
        }
        // A to-do line keeps its one list (§445): no "Show from".
        Assert.Equal(AccountShows.None, WidgetSources.KindOf(ListKind.Todo.New()));

        var mail = ListKind.Mail.New();
        mail.Sources = [" ann@example.com|mail:Archive ", "ann@example.com|mail:Archive", ""];
        mail.Normalize();
        Assert.Equal(["ann@example.com|mail:Archive"], mail.Sources);
        var json = System.Text.Json.JsonSerializer.Serialize(mail, ConfigJson.Default.CellItem);
        Assert.Equal(["ann@example.com|mail:Archive"], System.Text.Json.Nodes.JsonNode.Parse(json)!["sources"]!.AsArray().Select(key => (string)key!));
        Assert.DoesNotContain("sources", System.Text.Json.JsonSerializer.Serialize(ListKind.Mail.New(), ConfigJson.Default.CellItem));
    }
}
