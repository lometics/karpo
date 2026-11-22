// What an account's card says of what is synced (Karpo.Accounts\AccountSyncRows.cs): a group
// per kind it gives, a row with a switch per inbox, calendar and address book, how many each holds,
// and a part turned off saying so at once, before the dock has heard.

using Karpo.Accounts;

namespace Karpo.Tests.Accounts;

public sealed class AccountSyncRowsTests
{
    private static readonly DateTime Now = new(2026, 10, 11, 15, 0, 0);

    private static readonly DateTime Updated = DateTime.SpecifyKind(Now, DateTimeKind.Local).AddHours(-1).ToUniversalTime();

    private static Account Microsoft => new() { Id = "a", Kind = AccountKind.Microsoft, Address = "ann@outlook.com" };

    private static SyncReportAccount Report() => new()
    {
        Account = "a",
        Calendars = new SyncPart
        {
            UpdatedUtc = Updated,
            FromUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2027, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            Items =
            [
                new SyncItem { Id = "home", Name = "Home", Colour = "#3A96DD", Count = 1 },
                new SyncItem { Id = "work", Name = "Work", Count = 56 },
                new SyncItem { Id = "birthdays", Name = "Birthdays", Off = true },
            ],
        },
        Contacts = new SyncPart { UpdatedUtc = Updated, Items = [new SyncItem { Id = "default", Name = "Contacts", Count = 312 }] },
        Mail = new SyncPart { UpdatedUtc = Updated, Items = [new SyncItem { Id = SyncKeys.Inbox, Name = "Inbox", Count = 1234, Unread = 12 }] },
    };

    [Fact]
    public void EachKindItGivesIsAGroupWithAPartARowAndHowManyItHolds()
    {
        var account = Microsoft.Synced(SyncKeys.Calendar("birthdays"), false);
        var groups = AccountSyncRows.Groups(account, Report(), Now);

        Assert.Equal([AccountData.Calendars, AccountData.Contacts, AccountData.Mail, AccountData.Tasks], groups.Select(group => group.Kind));
        Assert.Equal(["Calendars", "Contacts", "Mail", "Tasks"], groups.Select(group => group.Title));

        var calendars = groups[0].Rows;
        Assert.Equal(["Home", "Work", "Birthdays"], calendars.Select(row => row.Name));
        Assert.Equal(["1 event", "56 events", "Not synced"], calendars.Select(row => row.Detail));
        Assert.Equal([true, true, false], calendars.Select(row => row.On));
        // Home has the account's blue; Work and Birthdays the palette's next, never blue again.
        Assert.Equal(["#3A96DD", "#038387", "#8764B8"], calendars.Select(row => row.Colour));
        Assert.Equal([SyncKeys.Calendar("home"), SyncKeys.Calendar("work"), SyncKeys.Calendar("birthdays")], calendars.Select(row => row.Key));

        Assert.Equal("312 contacts", groups[1].Rows.Single().Detail);
        // §638: a count grouped as the formats group it.
        Assert.Equal(RegionFormats.Count(1234) + " messages · 12 unread", groups[2].Rows.Single().Detail);
        Assert.Equal(SyncKeys.Inbox, groups[2].Rows.Single().Key);
    }

    /// <summary>
    /// §639: the mail group lists the inbox, then every other folder as the dock last listed it, each a switch — off until
    /// turned on — with its counts where the account gave them (off, its unread: "Not synced · 0 unread"), the folder's
    /// glyph and no swatch (it wears its inbox's colour); the inbox off and a folder on, the mail still syncs. In a menu,
    /// the same parts.
    /// </summary>
    [Fact]
    public void AnAccountsMailFoldersAreRowsOffUntilTurnedOn()
    {
        var report = Report();
        report.Mail!.Folders =
        [
            new SyncItem { Id = "AR", Name = "Archive", Count = 50, Unread = 5 },
            new SyncItem { Id = "RE", Name = "Receipts", Count = 7, Unread = 0, Off = true },
            new SyncItem { Id = "PR", Name = "Inbox / Projects", Off = true },
        ];
        var account = Microsoft.Synced(SyncKeys.MailFolder("AR"), true);
        var mail = AccountSyncRows.Groups(account, report, Now)[2];

        Assert.Equal(["Inbox", "Archive", "Receipts", "Inbox / Projects"], mail.Rows.Select(row => row.Name));
        Assert.Equal([SyncKeys.Inbox, SyncKeys.MailFolder("AR"), SyncKeys.MailFolder("RE"), SyncKeys.MailFolder("PR")], mail.Rows.Select(row => row.Key));
        Assert.Equal([true, true, false, false], mail.Rows.Select(row => row.On));
        Assert.Equal([$"{RegionFormats.Count(1234)} messages · 12 unread", "50 messages · 5 unread", "Not synced · 0 unread", "Not synced"], mail.Rows.Select(row => row.Detail));
        Assert.Equal([AccountSyncRows.InboxGlyph, AccountSyncRows.FolderGlyph, AccountSyncRows.FolderGlyph, AccountSyncRows.FolderGlyph], mail.Rows.Select(row => row.Glyph));
        Assert.Equal([true, false, false, false], mail.Rows.Select(row => row.Coloured));

        // Turned on before the dock has read it: not synced yet.
        var receipts = AccountSyncRows.Groups(account.Synced(SyncKeys.MailFolder("RE"), true), report, Now)[2].Rows[2];
        Assert.Equal((true, "Not synced yet"), (receipts.On, receipts.Detail));

        // The inbox off, the archive on: the mail syncs; both off: it does not.
        Assert.NotEqual(S.SettingsAccountsSyncOff, AccountSyncRows.Groups(account.Synced(SyncKeys.Inbox, false), report, Now)[2].State);
        Assert.Equal(S.SettingsAccountsSyncOff, AccountSyncRows.Groups(Microsoft.Synced(SyncKeys.Inbox, false), report, Now)[2].State);

        var menu = AccountSyncRows.Menu(account, report, Now).Groups[2].Parts;
        Assert.Equal([true, true, false, false], menu.Select(part => part.On));
        Assert.Equal([true, false, false, false], menu.Select(part => part.Coloured));
    }

    /// <summary>
    /// §646: an account's mail is a page of its own, Mail folders, its link saying what of it is synced — the inbox alone, the
    /// inbox and how many more, the inbox off how many folders, nothing; no other kind is one (its parts stand in the account's rows).
    /// </summary>
    [Fact]
    public void TheMailFoldersLinkSaysWhatIsSynced()
    {
        var report = Report();
        report.Mail!.Folders =
        [
            new SyncItem { Id = "AR", Name = "Archive", Count = 50, Unread = 5 },
            new SyncItem { Id = "RE", Name = "Receipts", Count = 7, Unread = 0 },
            new SyncItem { Id = "PR", Name = "Inbox / Projects" },
        ];
        string Said(Account account) => AccountSyncRows.Menu(account, report, Now).Groups.Single(group => group.Folders is not null).Folders!;

        Assert.Equal("Inbox synced", Said(Microsoft));
        Assert.Equal("Inbox and 1 more synced", Said(Microsoft.Synced(SyncKeys.MailFolder("AR"), true)));
        Assert.Equal("Inbox and 2 more synced", Said(Microsoft.Synced(SyncKeys.MailFolder("AR"), true).Synced(SyncKeys.MailFolder("PR"), true)));
        Assert.Equal("1 folder synced", Said(Microsoft.Synced(SyncKeys.Inbox, false).Synced(SyncKeys.MailFolder("RE"), true)));
        Assert.Equal("2 folders synced", Said(Microsoft.Synced(SyncKeys.Inbox, false).Synced(SyncKeys.MailFolder("RE"), true).Synced(SyncKeys.MailFolder("AR"), true)));
        Assert.Equal(S.SettingsAccountsSyncOff, Said(Microsoft.Synced(SyncKeys.Inbox, false)));

        // The mail alone; its parts still the inbox and every folder, for its page.
        var groups = AccountSyncRows.Menu(Microsoft, report, Now).Groups;
        Assert.Equal([null, null, "Inbox synced", null], groups.Select(group => group.Folders));
        Assert.Equal(["Inbox", "Archive", "Receipts", "Inbox / Projects"], groups[2].Parts.Select(part => part.Name));
    }

    /// <summary>
    /// §338: an account as a menu lists it is the card's groups: each part's switch, count and colour —
    /// the picked one where picked, else none and what it wears unpicked —, a To Do list wearing none; under
    /// a heading why it did not sync, To Do never allowed (and "Reconnect"), or the state of a kind that
    /// lists nothing yet; nothing under one that synced.
    /// </summary>
    [Fact]
    public void AnAccountInAMenuIsItsCardsGroups()
    {
        var report = Report();
        report.Contacts!.Problem = "The server said no.";
        report.Tasks = new SyncPart { Consent = true };
        var account = Microsoft.Synced(SyncKeys.Calendar("birthdays"), false).Coloured(SyncKeys.Calendar("work"), "#E3008C");
        var menu = AccountSyncRows.Menu(account, report, Now);

        Assert.Equal(("a", "ann@outlook.com", OptionAccountKind.Microsoft), (menu.Id, menu.Address, menu.Kind));
        Assert.Equal(["Calendars", "Contacts", "Mail", "Tasks"], menu.Groups.Select(group => group.Title));
        var calendars = menu.Groups[0].Parts;
        Assert.Equal([SyncKeys.Calendar("home"), SyncKeys.Calendar("work"), SyncKeys.Calendar("birthdays")], calendars.Select(part => part.Key));
        Assert.Equal(["1 event", "56 events", "Not synced"], calendars.Select(part => part.Detail));
        Assert.Equal([true, true, false], calendars.Select(part => part.On));
        Assert.Equal([null, "#E3008C", null], calendars.Select(part => part.Colour));
        Assert.Equal("#3A96DD", calendars[0].Automatic);
        Assert.All(calendars, part => Assert.True(part.Coloured));
        Assert.Null(menu.Groups[0].Note);
        Assert.StartsWith(S.SettingsAccountsSyncFailed("The server said no."), menu.Groups[1].Note);
        Assert.Null(menu.Groups[2].Note);
        Assert.True(menu.Groups[3].Reconnect);
        Assert.Equal(S.SettingsAccountsSyncConsent("Microsoft To Do"), menu.Groups[3].Note);
    }

    // The person, 2026-10-12: "align the individual synced stuff as if all of them have an icon. if
    // possible just give all of them an icon." A row without one starts its name further left.
    [Fact]
    public void EveryRowHasItsKindsIconAndAColour()
    {
        var report = Report();
        report.Calendars!.Items.Add(new SyncItem { Id = "odd", Name = "Odd", Colour = "blue", Count = 2 });
        var groups = AccountSyncRows.Groups(Microsoft, report, Now);

        Assert.All(groups.SelectMany(group => group.Rows), row => Assert.False(string.IsNullOrEmpty(row.Glyph)));
        Assert.All(groups[0].Rows, row => Assert.Equal(AccountSyncRows.CalendarGlyph, row.Glyph));
        Assert.Equal(AccountSyncRows.BookGlyph, groups[1].Rows.Single().Glyph);
        Assert.Equal(AccountSyncRows.InboxGlyph, groups[2].Rows.Single().Glyph);
        // Every row wears a colour; one the account gives that is no #RRGGBB is none of its own.
        Assert.All(groups.SelectMany(group => group.Rows), row => Assert.NotNull(SourceColours.Normal(row.Colour)));
        Assert.False(groups[0].Rows[3].Given);
        Assert.True(groups[0].Rows[0].Given);
    }

    // "in the account settings you should be able to set colors for different calendars, email
    // inboxes, or contact lists." (2026-10-12)
    [Fact]
    public void APickedColourIsWornAndAutomaticSaysWhatItWouldBeUnpicked()
    {
        var account = Microsoft.Coloured(SyncKeys.Calendar("home"), "#D13438").Coloured(SyncKeys.Inbox, "#7A7574");
        var groups = AccountSyncRows.Groups(account, Report(), Now);

        var home = groups[0].Rows[0];
        Assert.Equal(("#D13438", "#3A96DD", true, true), (home.Colour, home.Automatic, home.Picked, home.Given));
        // Work keeps its colour: a pick moves no other part.
        Assert.Equal(("#038387", false), (groups[0].Rows[1].Colour, groups[0].Rows[1].Picked));
        Assert.Equal(("#7A7574", true), (groups[2].Rows.Single().Colour, groups[2].Rows.Single().Picked));
        Assert.Equal("Red", AccountSyncRows.ColourName("#d13438"));
        Assert.Equal("#123456", AccountSyncRows.ColourName("#123456"));
    }

    [Fact]
    public void TheColoursAreWorkedOutAcrossTheAccountsAsTheDockDoes()
    {
        var imap = new Account { Id = "b", Kind = AccountKind.Imap, Address = "b@example.com" };
        var file = new SyncReportFile { Accounts = [Report()] };
        var colours = AccountSyncRows.Colours([Microsoft, imap], file);

        // Two inboxes: the first account's the palette's first, the second's the next.
        var first = AccountSyncRows.Groups(Microsoft, file.For("a"), Now, colours)[2].Rows.Single().Colour;
        var second = AccountSyncRows.Groups(imap, null, Now, colours).Single().Rows.Single().Colour;
        Assert.Equal((SourceColours.Palette[5], SourceColours.Palette[4]), (first, second));
    }

    [Fact]
    public void TheGroupSaysWhenItWasSyncedAndTheCalendarsTheSpanTheirCountsAreOf()
    {
        var groups = AccountSyncRows.Groups(Microsoft, Report(), Now);
        var time = DateTime.SpecifyKind(Updated, DateTimeKind.Utc).ToLocalTime().ToString("t", System.Globalization.CultureInfo.CurrentCulture);

        Assert.StartsWith($"Synced at {time} · events from ", groups[0].State);
        Assert.Equal($"Synced at {time}", groups[2].State);
        Assert.Equal($"Synced {Now.AddDays(-1).AddHours(-1).ToString("M", System.Globalization.CultureInfo.CurrentCulture)} at {time}",
            AccountSyncRows.When(Updated.AddDays(-1), Now));
    }

    [Fact]
    public void APartJustTurnedOffSaysSoBeforeTheDockHasHeardAndOneTurnedOnAgainIsNotSyncedYet()
    {
        // The dock still reports Work as read; the person has just turned it off.
        var off = AccountSyncRows.Groups(Microsoft.Synced(SyncKeys.Calendar("work"), false).Synced(SyncKeys.Inbox, false), Report(), Now);
        Assert.Equal("Not synced", off[0].Rows[1].Detail);
        Assert.False(off[0].Rows[1].On);
        Assert.Equal("Not synced", off[2].Rows.Single().Detail);
        Assert.Equal("Not synced", off[2].State);

        // Birthdays was off at the last sync; the person has just turned it on.
        var on = AccountSyncRows.Groups(Microsoft, Report(), Now);
        Assert.Equal("Not synced yet", on[0].Rows[2].Detail);
        Assert.True(on[0].Rows[2].On);
    }

    [Fact]
    public void BeforeTheDockHasSaidAnythingTheInboxHasItsSwitchAndTheRestWait()
    {
        var groups = AccountSyncRows.Groups(Microsoft, null, Now);

        Assert.Empty(groups[0].Rows);
        Assert.Equal("Not synced yet", groups[0].State);
        Assert.Equal("Not synced yet", groups[1].State);
        var inbox = groups[2].Rows.Single();
        Assert.Equal(("Inbox", "Not synced yet", true), (inbox.Name, inbox.Detail, inbox.On));
    }

    [Fact]
    public void AFailedSyncSaysWhyAndAnAccountShowsOnlyWhatItGives()
    {
        var report = new SyncReportAccount { Account = "b", Mail = new SyncPart { Problem = "imap.example.com could not be reached." } };
        var imap = new Account { Id = "b", Kind = AccountKind.Imap, Address = "b@example.com" };

        var group = Assert.Single(AccountSyncRows.Groups(imap, report, Now));
        Assert.Equal(AccountData.Mail, group.Kind);
        Assert.Equal("Couldn't sync: imap.example.com could not be reached.", group.State);
        Assert.Equal("Not synced yet", group.Rows.Single().Detail);
    }

    [Fact]
    public void WordsChangeInPlaceOnlyWhileTheRowsAreTheSame()
    {
        var before = AccountSyncRows.Groups(Microsoft, Report(), Now);
        var counted = Report();
        counted.Calendars!.Items[1].Count = 57;
        var turned = AccountSyncRows.Groups(Microsoft.Synced(SyncKeys.Calendar("work"), false), counted, Now);
        Assert.True(AccountSyncRows.SameRows(before, turned));

        var added = Report();
        added.Calendars!.Items.Add(new SyncItem { Id = "new", Name = "New", Count = 0 });
        Assert.False(AccountSyncRows.SameRows(before, AccountSyncRows.Groups(Microsoft, added, Now)));
    }

    /// <summary>
    /// §638 (the person, 2026-11-09: "the accounts when collapsed should show the amount of items totally synced"): everything
    /// synced of an account as one line, kind by kind in the card's order, each the sum of the parts its rows count — a part
    /// turned off, not read yet or failing on its own left out —, the counts grouped as the formats group them, the plural each
    /// count's; a kind not given or not counted yet left out; nothing counted, none. A sync that finishes says it again.
    /// </summary>
    [Fact]
    public void WhatIsSyncedOfAnAccountIsOneSum()
    {
        var report = Report();
        report.Tasks = new SyncPart
        {
            UpdatedUtc = Updated,
            Items =
            [
                new SyncItem { Id = "t", Name = "Tasks", Count = 1 },
                new SyncItem { Id = "g", Name = "Groceries", Count = 7 },
                new SyncItem { Id = "x", Name = "Broken", Count = 40, Problem = "Not all there." },
            ],
        };
        var account = Microsoft.Synced(SyncKeys.Calendar("birthdays"), false);
        // Home's 1 and Work's 56 (Birthdays off), the book's 312, the inbox's 1,234 (its unread no more of it), 1 + 7 open tasks:
        // §674 one sum, no kind by kind.
        Assert.Equal(RegionFormats.Count(1611) + " items synced", AccountSyncRows.Holds(account, report));
        Assert.Equal(AccountSyncRows.Holds(account, report), AccountSyncRows.Menu(account, report, Now).Holds);
        // One: its singular.
        report.Calendars!.Items = [];
        report.Contacts = new SyncPart();
        report.Tasks.Items = [new SyncItem { Id = "t", Name = "Tasks", Count = 1 }];
        Assert.Equal("1 item synced", AccountSyncRows.Holds(account.Synced(SyncKeys.Inbox, false), report));
        // The inbox turned off, the books not read yet: neither counted.
        report.Calendars!.Items = [new SyncItem { Id = "home", Name = "Home", Count = 1 }];
        report.Tasks.Items = [new SyncItem { Id = "t", Name = "Tasks", Count = 1 }, new SyncItem { Id = "g", Name = "Groceries", Count = 7 }];
        Assert.Equal("9 items synced", AccountSyncRows.Holds(account.Synced(SyncKeys.Inbox, false), report));
        // A mail account gives its inbox alone; nothing counted, nothing said.
        var imap = new Account { Id = "a", Kind = AccountKind.Imap, Address = "b@example.com" };
        Assert.Equal(RegionFormats.Count(1234) + " items synced", AccountSyncRows.Holds(imap, report));
        Assert.Null(AccountSyncRows.Holds(imap, null));
        Assert.Null(AccountSyncRows.Menu(imap, null, Now).Holds);
    }

    /// <summary>
    /// §638: a count as this PC's region writes a whole number (Windows' NLS: the dock's .NET culture is the invariant one) —
    /// its digits grouped with the region's mark, no decimals —, what every "{count:int}" of Karpo's words says.
    /// </summary>
    [Fact]
    public void ACountIsWrittenAsTheRegionWritesAWholeNumber()
    {
        Assert.Equal("999", RegionFormats.Count(999));
        Assert.Equal("0", RegionFormats.Count(0));
        foreach (var count in new long[] { 1234, 56789, 1234567 })
        {
            var written = RegionFormats.Count(count);
            // The digits in their order, a mark between groups and nothing after the last digit (no decimals).
            Assert.Equal(count.ToString(System.Globalization.CultureInfo.InvariantCulture), new string([.. written.Where(char.IsAsciiDigit)]));
            Assert.True(char.IsAsciiDigit(written[^1]), written);
        }
        Assert.True(RegionFormats.Count(1234567).Length > 7, RegionFormats.Count(1234567));
        Assert.Equal(RegionFormats.Count(1234) + " events", S.SettingsAccountsSyncEvents(1234));
        Assert.Equal("1 event", S.SettingsAccountsSyncEvents(1));
    }

    // "add a todo widget ... it should get its tasks from microsoft office, like the email etc." (2026-10-16)
    [Fact]
    public void AnAccountsToDoListsAreRowsWithTheirOpenTasksAndNoColour()
    {
        var report = Report();
        report.Tasks = new SyncPart
        {
            UpdatedUtc = Updated,
            Items =
            [
                new SyncItem { Id = "t", Name = "Tasks", Count = 1, Default = true },
                new SyncItem { Id = "g", Name = "Groceries", Count = 7 },
                new SyncItem { Id = "o", Name = "Old", Off = true },
            ],
        };
        var groups = AccountSyncRows.Groups(Microsoft.Synced(SyncKeys.TaskList("o"), false), report, Now);
        var tasks = groups[3];

        Assert.Equal((AccountData.Tasks, false), (tasks.Kind, tasks.Reconnect));
        Assert.Equal(["1 open task", "7 open tasks", "Not synced"], tasks.Rows.Select(row => row.Detail));
        Assert.Equal([SyncKeys.TaskList("t"), SyncKeys.TaskList("g"), SyncKeys.TaskList("o")], tasks.Rows.Select(row => row.Key));
        Assert.All(tasks.Rows, row => Assert.False(row.Coloured));
        Assert.All(tasks.Rows, row => Assert.Equal(AccountSyncRows.TaskListGlyph, row.Glyph));
    }

    [Fact]
    public void AnAccountWithoutToDosConsentOffersAReconnect()
    {
        var report = Report();
        report.Tasks = new SyncPart { Consent = true };
        var tasks = AccountSyncRows.Groups(Microsoft, report, Now)[3];

        Assert.True(tasks.Reconnect);
        Assert.Equal("Not synced: Karpo needs your permission for Microsoft To Do", tasks.State);
        Assert.Empty(tasks.Rows);
        // Reconnected, the heading loses its button: the card is built again, not its words changed.
        var after = Report();
        after.Tasks = new SyncPart { UpdatedUtc = Updated };
        Assert.False(AccountSyncRows.SameRows(AccountSyncRows.Groups(Microsoft, report, Now), AccountSyncRows.Groups(Microsoft, after, Now)));
    }

    /// <summary>
    /// §687: a Google account whose person unticked Gmail on Google's consent screen: its Mail heading says so in Google's
    /// name for it and offers "Reconnect to sync mail" — its rows still there —, its calendars synced as ever; in the menu its
    /// group knows its kind, so the Reconnect names the mail.
    /// </summary>
    [Fact]
    public void AGoogleKindNeverGrantedOffersAReconnectForThatKind()
    {
        var google = new Account { Id = "a", Kind = AccountKind.Google, Address = "ann@gmail.com" };
        var report = Report();
        report.Mail = new SyncPart { Consent = true };
        var groups = AccountSyncRows.Groups(google, report, Now);

        Assert.Equal([AccountData.Calendars, AccountData.Contacts, AccountData.Mail, AccountData.Tasks], groups.Select(group => group.Kind));
        Assert.Equal([false, false, true, false], groups.Select(group => group.Reconnect));
        Assert.Equal("Not synced: Karpo needs your permission for Gmail", groups[2].State);
        Assert.Equal(SyncKeys.Inbox, Assert.Single(groups[2].Rows).Key);

        var menu = AccountSyncRows.Menu(google, report, Now);
        Assert.Equal(OptionAccountKind.Google, menu.Kind);
        Assert.Equal((true, AccountShows.Mail), (menu.Groups[2].Reconnect, menu.Groups[2].Kind));
        Assert.Equal("Not synced: Karpo needs your permission for Gmail", menu.Groups[2].Note);
        Assert.Equal(S.CoreAccountsReconnectMail, OptionAccountRows.ReconnectName(menu.Groups[2].Kind));
        Assert.Equal(S.CoreAccountsReconnectTasks, OptionAccountRows.ReconnectName(AccountShows.Tasks));
    }
}
