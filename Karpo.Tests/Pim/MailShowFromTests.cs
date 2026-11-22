// §644: a mail widget picks, in its own Show from (its menu's level, its settings page's expander), which accounts and of
// each which folders it shows. The person, 2026-11-09: "for the mail widget you should also be able to select which
// accounts and from the account which folders you want to show in the widget itself. e.g. inbox from two accounts or
// similar."
//
// Held here over fake accounts (FakeReader), no server reached, accounts.json and sync-report.json in the test's own
// folder: every account's every folder listed under its account's line (its name, its address, its inbox's colour), the
// ones not synced saying so; several accounts' inboxes and folders picked at once; a folder not synced picked turns its
// sync on in accounts.json, says "Syncing…" until the store has read it, then shows; nothing written for the defaults.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Edit;

namespace Karpo.Tests.Pim;

public sealed class MailShowFromTests
{
    private static readonly DateTime Now = new(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);

    private const string Ann = "ann@outlook.com";

    private const string Work = "ann@contoso.com";

    private static string Key(string address, string part) => WidgetSources.Key(address, part);

    private static MailItem Message(string subject, int hoursAgo) =>
        new(subject, "Sandra", "sandra@example.ch", subject, null, new DateTimeOffset(Now.AddHours(-hoursAgo)), true);

    /// <summary>Ann's Microsoft account, named: its inbox, the archive synced, two folders not.</summary>
    private static FakeReader Mine()
    {
        var reader = new FakeReader(AccountKind.Microsoft, Ann) { Inbox = new Inbox(1, [Message("In my inbox", 0)], 10) };
        reader.Folders.Add((new MailFolder("AR", "Archive", Unread: 1, Total: 40), new Inbox(1, [Message("In the archive", 3)], 40)));
        reader.Folders.Add((new MailFolder("PR", "Inbox / Projects", Unread: 0, Total: 5), new Inbox(0, [], 5)));
        reader.Folders.Add((new MailFolder("SE", "Sent Items", Unread: 0, Total: 90), new Inbox(0, [], 90)));
        return reader.Named("Ann Smith").Turned(SyncKeys.MailFolder("AR"), true);
    }

    /// <summary>Ann's work mail server, no name of its own: its inbox, and Sent and Receipts not synced.</summary>
    private static FakeReader Works()
    {
        var reader = new FakeReader(AccountKind.Imap, Work) { Inbox = new Inbox(1, [Message("At work", 1)], 20) };
        reader.Folders.Add((new MailFolder("Sent", "Sent", Unread: 0, Total: 12), new Inbox(0, [], 12)));
        reader.Folders.Add((new MailFolder("Receipts", "Receipts", Unread: 1, Total: 3), new Inbox(1, [Message("A receipt", 2)], 3)));
        return reader;
    }

    /// <summary>The two accounts read once by a mail store telling its own report.</summary>
    private static async Task<(MailStore Store, AccountSet Set, SyncReport Report, FakeReader Mine, FakeReader Work)> Read()
    {
        var (mine, work) = (Mine(), Works());
        var set = new AccountSet([mine, work]);
        var report = new SyncReport(Path.Combine(Isolation.Folder, "mailpick-" + Guid.NewGuid().ToString("N")));
        var store = new MailStore(set, report: report, now: () => Now);
        store.Refresh();
        await store.Idle();
        return (store, set, report, mine, work);
    }

    /// <summary>The rows of <paramref name="item"/>'s "Show from", as its menu shows them.</summary>
    private static FlyoutItem ShowFrom(WidgetItem item, IReadOnlyList<SourcePart> parts, List<OptionPick> picks) =>
        OptionMenu.Rows(OptionRows.Of(item, new OptionDevices { Parts = parts }), picks).Single(row => row.Text == S.DockMenuShowFromTitle);

    /// <summary>Each row of a Show from: an account's line "# name", a part "name on|off" and what it says at its end.</summary>
    private static string[] Said(FlyoutItem showFrom) =>
        [.. showFrom.Children!.Select(child => child.Note ? "# " + child.Text
            : $"{child.Text} {(((FlyoutControl.Toggle)child.Control!).On ? "on" : "off")}{(child.Value is { } value ? " · " + value.Text : "")}")];

    private static bool Picks(WidgetItem line, IReadOnlyList<SourcePart> parts, string name, string group)
    {
        List<OptionPick> picks = [];
        var row = ShowFrom(line, parts, picks);
        // The part of that name under that account's line.
        var at = row.Children!.ToList().FindIndex(child => child.Note && child.Text == group);
        var part = row.Children!.Skip(at + 1).First(child => child.Text == name);
        return OptionMenu.Apply(line, OptionMenu.PickOf(picks, part.Id)!, new OptionDevices { Parts = parts });
    }

    [Fact]
    public async Task ShowFromListsEveryAccountsFoldersUnderItsLine()
    {
        var (store, _, report, mine, work) = await Read();
        using var held = store;
        var parts = SourceParts.Of([mine.Account, work.Account], report.Read(), AccountShows.Mail);

        // Every folder of each account, its inbox first, those not synced too.
        Assert.Equal(
            [
                ("Ann Smith", "Inbox", true, SourceSync.Synced), ("Ann Smith", "Archive", false, SourceSync.Synced),
                ("Ann Smith", "Inbox / Projects", false, SourceSync.Off), ("Ann Smith", "Sent Items", false, SourceSync.Off),
                (Work, "Inbox", true, SourceSync.Synced), (Work, "Sent", false, SourceSync.Off), (Work, "Receipts", false, SourceSync.Off),
            ],
            parts.Select(part => (part.Group, part.Name, part.Default, part.Sync)));
        // The address under a name that is not it; the colour each account's rows wear on the bar, two apart.
        Assert.All(parts.Where(part => part.Group == "Ann Smith"), part => Assert.Equal(Ann, part.GroupDetail));
        Assert.All(parts.Where(part => part.Group == Work), part => Assert.Null(part.GroupDetail));
        var worn = store.View(SourceFilter.All).Messages.ToDictionary(message => message.Subject, message => message.Colour);
        Assert.Equal(worn["In my inbox"], parts[0].GroupColour);
        Assert.Equal(worn["At work"], parts[4].GroupColour);
        Assert.NotEqual(parts[0].GroupColour, parts[4].GroupColour);

        // The menu: each account a line to read over its folders, its address under it and its colour in its icon's place;
        // a new line shows every inbox, its folders off, those not synced saying so.
        var line = ListKind.Mail.New();
        var row = ShowFrom(line, parts, []);
        Assert.Equal(S.DockMenuShowFromInboxes, row.Value!.Text);
        var off = S.SettingsAccountsSyncOff;
        Assert.Equal(
            ["# Ann Smith", "Inbox on", "Archive off", $"Inbox / Projects off · {off}", $"Sent Items off · {off}", $"# {Work}", "Inbox on", $"Sent off · {off}", $"Receipts off · {off}"],
            Said(row));
        var head = row.Children![0];
        Assert.Equal(Ann, head.Detail);
        Assert.Equal(Argb.Parse(parts[0].GroupColour), Assert.IsType<FlyoutTile>(head.Icon).Color);
        Assert.Null(row.Children!.Single(child => child.Note && child.Text == Work).Detail);
    }

    [Fact]
    public async Task SeveralAccountsInboxesAndFoldersArePickedTogether()
    {
        var (store, _, report, mine, work) = await Read();
        using var held = store;
        var parts = SourceParts.Of([mine.Account, work.Account], report.Read(), AccountShows.Mail);
        var line = ListKind.Mail.New();

        // The default: both accounts' inboxes, nothing written.
        Assert.Equal(["In my inbox", "At work"], store.View(SourceFilter.Of(line)).Messages.Select(message => message.Subject));
        Assert.Null(line.Sources);

        // The archive beside both inboxes; then Ann's inbox off: her archive and the work inbox, each row naming its folder.
        Assert.True(Picks(line, parts, "Archive", "Ann Smith"));
        Assert.Equal([Key(Ann, SyncKeys.Inbox), Key(Ann, "mail:AR"), Key(Work, SyncKeys.Inbox)], line.Sources);
        Assert.Equal(S.DockMenuShowFromSome(3, 7), ShowFrom(line, parts, []).Value!.Text);
        Assert.True(Picks(line, parts, "Inbox", "Ann Smith"));
        Assert.Equal([Key(Ann, "mail:AR"), Key(Work, SyncKeys.Inbox)], line.Sources);
        var view = store.View(SourceFilter.Of(line));
        Assert.Equal([("At work", "Inbox"), ("In the archive", "Archive")], view.Messages.Select(message => (message.Subject, message.Folder)));
        Assert.Equal(2, view.Unread);

        // The file keeps the picks of both accounts as they were.
        var json = System.Text.Json.JsonSerializer.Serialize(line, ConfigJson.Default.CellItem);
        var back = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        back.Normalize();
        Assert.Equal(line.Sources, back.Sources);

        // Back to both inboxes alone: the defaults, so nothing written.
        Assert.True(Picks(line, parts, "Inbox", "Ann Smith"));
        Assert.True(Picks(line, parts, "Archive", "Ann Smith"));
        Assert.Null(line.Sources);
        Assert.DoesNotContain("sources", System.Text.Json.JsonSerializer.Serialize(line, ConfigJson.Default.CellItem));
    }

    [Fact]
    public async Task PickingAFolderNotSyncedTurnsItsSyncOnAndItIsRead()
    {
        var (store, set, report, mine, work) = await Read();
        using var held = store;
        // accounts.json in the test's folder, as the Accounts page and the dock have it.
        var file = Path.Combine(Isolation.Folder, "mailpick-" + Guid.NewGuid().ToString("N"), "accounts.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        AccountBook.Change(file, accounts => accounts.Accounts.AddRange([mine.Account, work.Account]));
        IReadOnlyList<SourcePart> Listed()
        {
            _ = AccountBook.Load(file, out var accounts);
            return SourceParts.Of(accounts, report.Read(), AccountShows.Mail, (id, key) => AccountBook.SetSynced(file, id, key, true));
        }
        var line = ListKind.Mail.New();

        // Receipts not synced: offered, off. Picked: its sync on in accounts.json, the line showing both inboxes and it.
        Assert.Contains($"Receipts off · {S.SettingsAccountsSyncOff}", Said(ShowFrom(line, Listed(), [])));
        Assert.True(Picks(line, Listed(), "Receipts", Work));
        Assert.Equal([Key(Ann, SyncKeys.Inbox), Key(Work, SyncKeys.Inbox), Key(Work, "mail:Receipts")], line.Sources);
        _ = AccountBook.Load(file, out var now);
        Assert.Equal(["mail:Receipts"], now.Single(account => account.Address == Work).On);
        Assert.Equal(["mail:AR"], now.Single(account => account.Address == Ann).On);

        // Until it is read: "Syncing…".
        Assert.Contains($"Receipts on · {S.DockMenuShowFromSyncing}", Said(ShowFrom(line, Listed(), [])));

        // The dock hears accounts.json and reads it at once: synced, its newest on the line, nothing more said.
        var turned = work.Turned(SyncKeys.MailFolder("Receipts"), true);
        Assert.Equal(now.Single(account => account.Address == Work).On, turned.Account.On);
        set.Become([mine, turned]);
        await store.Idle();
        Assert.Equal(["Receipts"], turned.Asked);
        Assert.Contains("Receipts on", Said(ShowFrom(line, Listed(), [])));
        Assert.Equal(["In my inbox", "At work", "A receipt"], store.View(SourceFilter.Of(line)).Messages.Select(message => message.Subject));

        // Unpicked: the line's again, its sync stays (another widget may show it; ⚙ › Accounts turns it off).
        Assert.True(Picks(line, Listed(), "Receipts", Work));
        Assert.Null(line.Sources);
        _ = AccountBook.Load(file, out now);
        Assert.Equal(["mail:Receipts"], now.Single(account => account.Address == Work).On);
        Assert.Contains("Receipts off", Said(ShowFrom(line, Listed(), [])));
    }

    [Fact]
    public void AnInboxTurnedOffIsOfferedAndPickingItSyncsItAgain()
    {
        var synced = 0;
        List<SourcePart> parts =
        [
            new(Key(Ann, SyncKeys.Inbox), S.CoreAccountsInbox, "Ann Smith") { Sync = SourceSync.Off, SyncOn = () => synced++ },
            new(Key(Work, SyncKeys.Inbox), S.CoreAccountsInbox, Work),
        ];
        var line = ListKind.Mail.New();
        // Off at the account: off here, whatever the line picked; the row's end says so, "All inboxes" counting the one synced.
        Assert.Equal(["# Ann Smith", $"Inbox off · {S.SettingsAccountsSyncOff}", $"# {Work}", "Inbox on"], Said(ShowFrom(line, parts, [])));
        Assert.Equal(S.DockMenuShowFromInboxes, ShowFrom(line, parts, []).Value!.Text);
        // Picked: synced again; the line still picks every inbox, so nothing is written.
        Assert.True(Picks(line, parts, S.CoreAccountsInbox, "Ann Smith"));
        Assert.Equal(1, synced);
        Assert.Null(line.Sources);
    }

    /// <summary>
    /// §644: on the mail widget's settings page Show from is an expander in What it shows, as every widget's: in it each
    /// account a row of its card — its name, its address under it, its colour — over its folders' switches, a row each.
    /// </summary>
    [Fact]
    public void TheWidgetsPageGroupsShowFromByAccount()
    {
        List<SourcePart> parts =
        [
            new(Key(Ann, SyncKeys.Inbox), S.CoreAccountsInbox, "Ann Smith") { GroupDetail = Ann, GroupColour = "#0078D4" },
            new(Key(Ann, "mail:AR"), "Archive", "Ann Smith", Default: false) { GroupDetail = Ann, GroupColour = "#0078D4" },
            new(Key(Work, SyncKeys.Inbox), S.CoreAccountsInbox, Work) { GroupColour = "#038387" },
            new(Key(Work, "mail:Sent"), "Sent", Work, Default: false) { GroupColour = "#038387", Sync = SourceSync.Off },
        ];
        var items = OptionMenu.Rows(OptionRows.Of(ListKind.Mail.New(), new OptionDevices { Parts = parts }), []);
        var page = PanelGroups.Page(PanelPage.Widget, "", "", items);
        var showFrom = PanelRows.All(page.Rows).Single(row => row.Text == S.DockMenuShowFromTitle);
        Assert.Equal(PanelRowKind.Expander, showFrom.Kind);
        Assert.Equal(
            [(PanelRowKind.Note, "Ann Smith"), (PanelRowKind.Toggle, "Inbox"), (PanelRowKind.Toggle, "Archive"), (PanelRowKind.Note, Work), (PanelRowKind.Toggle, "Inbox"), (PanelRowKind.Toggle, "Sent")],
            showFrom.Rows.Select(row => (row.Kind, row.Text)));
        Assert.Equal(Ann, showFrom.Rows[0].Second);
        Assert.IsType<FlyoutTile>(showFrom.Rows[0].Item.Icon);

        // Opened, each a line of its card, in that order: the accounts' lines too.
        var layout = new PanelLayout(new SettingsPanelTests.Words(), 1.5f, new PanelInput(0, new SettingsContent([page]), PanelPage.Widget, new HashSet<string> { showFrom.Key }), PanelLayout.SizeOf(1.5f, 1400));
        var lines = showFrom.Rows.Select(row => layout.LineOf(row.Key)).ToList();
        Assert.All(lines, line => Assert.NotNull(line));
        Assert.True(lines.Zip(lines.Skip(1)).All(pair => pair.First!.Rect.Bottom <= pair.Second!.Rect.Y));
    }
}
