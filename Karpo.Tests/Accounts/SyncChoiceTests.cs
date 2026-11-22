// Turning an inbox, a calendar or an address book off (Account.Off, docs\accounts.md "What is
// synced"): kept in accounts.json as a patch, read back from a file of before with everything on,
// a change the dock hears; a reader asks nothing of a part turned off; and the dock's report of what
// it synced (SyncReport) — each store its own kind, encrypted, never rewritten for nothing.

using System.Net;
using System.Text;
using Karpo.Accounts;
using Karpo.Accounts.Microsoft;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Accounts;

public sealed class SyncChoiceTests : IDisposable
{
    private readonly string folder = Path.Combine(Isolation.Folder, "sync-choice", Guid.NewGuid().ToString("N"));

    private string File => Path.Combine(folder, "accounts.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Account Microsoft(string id) => new() { Id = id, Kind = AccountKind.Microsoft, Address = id + "@outlook.com" };

    [Fact]
    public void EverythingIsSyncedUntilTurnedOffAndTurningItOnAgainLeavesNoTrace()
    {
        var account = Microsoft("a");
        Assert.True(account.Syncs(SyncKeys.Inbox));
        Assert.True(account.Syncs(SyncKeys.Calendar("work")));

        var off = account.Synced(SyncKeys.Calendar("work"), false).Synced(SyncKeys.Inbox, false);
        Assert.False(off.Syncs(SyncKeys.Calendar("work")));
        Assert.False(off.Syncs(SyncKeys.Inbox));
        Assert.True(off.Syncs(SyncKeys.Calendar("home")));
        // Twice off is once off.
        Assert.Equal(2, off.Synced(SyncKeys.Inbox, false).Off!.Count);
        // A calendar and a book of the same id are two things.
        Assert.True(off.Syncs(SyncKeys.Book("work")));

        var on = off.Synced(SyncKeys.Calendar("work"), true).Synced(SyncKeys.Inbox, true);
        Assert.Null(on.Off);
        Assert.Same(account, account.Synced(SyncKeys.Inbox, true));
    }

    [Fact]
    public void ATurnIsAPatchOnTheFileAndAFileOfBeforeHasEverythingOn()
    {
        Directory.CreateDirectory(folder);
        // accounts.json as every Karpo before 2026-10-11 wrote it: no "off" anywhere.
        System.IO.File.WriteAllText(File, """
            { "version": 1, "accounts": [
              { "id": "a", "kind": "Microsoft", "address": "a@outlook.com" },
              { "id": "b", "kind": "Imap", "address": "b@example.com", "host": "imap.example.com", "port": 993, "security": "Implicit" }
            ] }
            """);
        var before = AccountBook.Read(File, out var read).Accounts;
        Assert.Equal(AccountsRead.Ok, read);
        Assert.All(before, account => Assert.Null(account.Off));
        Assert.All(before, account => Assert.True(account.Syncs(SyncKeys.Inbox)));

        AccountBook.SetSynced(File, "a", SyncKeys.Calendar("AAMk=="), false);
        AccountBook.SetSynced(File, "b", SyncKeys.Inbox, false);
        AccountBook.SetSynced(File, "gone", SyncKeys.Inbox, false);

        var after = AccountBook.Read(File, out _).Accounts;
        Assert.Equal(["a", "b"], after.Select(account => account.Id));
        Assert.Equal([SyncKeys.Calendar("AAMk==")], after[0].Off);
        Assert.Equal([SyncKeys.Inbox], after[1].Off);
        // Nothing else of the accounts moved.
        Assert.Equal("imap.example.com", after[1].Host);
        Assert.Contains("\"off\"", System.IO.File.ReadAllText(File));

        AccountBook.SetSynced(File, "a", SyncKeys.Calendar("AAMk=="), true);
        AccountBook.SetSynced(File, "b", SyncKeys.Inbox, true);
        Assert.All(AccountBook.Read(File, out _).Accounts, account => Assert.Null(account.Off));
        Assert.DoesNotContain("\"off\"", System.IO.File.ReadAllText(File));
    }

    [Fact]
    public void ATurnIsAChangeTheDockHears()
    {
        var account = Microsoft("a");
        var shape = AccountSet.Shape([account]);

        Assert.NotEqual(shape, AccountSet.Shape([account.Synced(SyncKeys.Calendar("work"), false)]));
        Assert.Equal(shape, AccountSet.Shape([account.Synced(SyncKeys.Calendar("work"), false).Synced(SyncKeys.Calendar("work"), true)]));
    }

    [Fact]
    public async Task AMicrosoftAccountAsksNothingOfACalendarOrAFolderTurnedOff()
    {
        var asked = new List<string>();
        var http = new HttpClient(new Answering(asked, url => url switch
        {
            _ when url.Contains("/me/calendars?") => """{ "value": [ { "id": "home", "name": "Home" }, { "id": "work", "name": "Work" } ] }""",
            _ when url.Contains("/me/contactFolders?") => """{ "value": [ { "id": "f1", "displayName": "Family" } ] }""",
            _ when url.Contains("/me/mailFolders/inbox?") => """{ "unreadItemCount": 12, "totalItemCount": 1234 }""",
            _ => """{ "value": [] }""",
        }));
        var account = Microsoft("a").Synced(SyncKeys.Calendar("work"), false).Synced(SyncKeys.Book(MicrosoftReader.DefaultBook), false);
        var reader = new MicrosoftReader(account, new Graph(http, _ => Task.FromResult("token")));

        var calendars = await reader.CalendarsAsync(DateTime.UtcNow, DateTime.UtcNow.AddDays(30), CancellationToken.None);
        var books = await reader.ContactsAsync(CancellationToken.None);
        var inbox = (await reader.MailAsync(30, CancellationToken.None))[0].Mail!;

        // Listed, so the Accounts page can name them, and nothing fetched of them.
        Assert.Equal([("home", false), ("work", true)], calendars.Select(read => (read.Calendar.Id, read.Off)));
        Assert.Contains(asked, url => url.Contains("/calendars/home/calendarView"));
        Assert.DoesNotContain(asked, url => url.Contains("/calendars/work/"));
        Assert.Equal([(MicrosoftReader.DefaultBook, true), ("f1", false)], books.Select(read => (read.Book.Id, read.Off)));
        Assert.Equal("Family", books[1].Book.Name);
        Assert.DoesNotContain(asked, url => url.Contains("/me/contacts?"));
        Assert.Contains(asked, url => url.Contains("/contactFolders/f1/contacts"));
        Assert.Equal((12, 1234), (inbox.Unread, inbox.Total));
    }

    /// <summary>
    /// §639: a mail folder is off until the person turns it on — every account before has its inbox alone —, kept in
    /// accounts.json as "on" beside "off", a patch like every other turn, gone from the file when turned off again; a turn
    /// the dock hears. An account whose inbox is off still syncs mail while a folder is on.
    /// </summary>
    [Fact]
    public void AFolderIsOffUntilTurnedOnAndKeptInTheFileAsOn()
    {
        var account = Microsoft("a");
        Assert.False(account.Syncs(SyncKeys.MailFolder("AR")));
        Assert.True(account.Syncs(SyncKeys.Inbox));
        Assert.Same(account, account.Synced(SyncKeys.MailFolder("AR"), false));

        var archive = account.Synced(SyncKeys.MailFolder("AR"), true);
        Assert.Equal([SyncKeys.MailFolder("AR")], archive.On);
        Assert.Null(archive.Off);
        Assert.True(archive.Syncs(SyncKeys.MailFolder("AR")));
        Assert.False(archive.Syncs(SyncKeys.MailFolder("RE")));
        Assert.NotEqual(AccountSet.Shape([account]), AccountSet.Shape([archive]));

        // The inbox off, a folder on: still mail; both off: none.
        var folderOnly = archive.Synced(SyncKeys.Inbox, false);
        Assert.True(folderOnly.SyncsMail);
        Assert.False(folderOnly.Synced(SyncKeys.MailFolder("AR"), false).SyncsMail);

        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(File, """
            { "version": 1, "accounts": [ { "id": "a", "kind": "Microsoft", "address": "a@outlook.com" } ] }
            """);
        Assert.Null(AccountBook.Read(File, out _).Accounts.Single().On);
        AccountBook.SetSynced(File, "a", SyncKeys.MailFolder("AR"), true);
        AccountBook.SetSynced(File, "a", SyncKeys.MailFolder("IN/PR"), true);
        var kept = AccountBook.Read(File, out _).Accounts.Single();
        Assert.Equal([SyncKeys.MailFolder("AR"), SyncKeys.MailFolder("IN/PR")], kept.On);
        Assert.Null(kept.Off);
        Assert.Contains("\"on\"", System.IO.File.ReadAllText(File));

        AccountBook.SetSynced(File, "a", SyncKeys.MailFolder("AR"), false);
        AccountBook.SetSynced(File, "a", SyncKeys.MailFolder("IN/PR"), false);
        Assert.Null(AccountBook.Read(File, out _).Accounts.Single().On);
        Assert.DoesNotContain("\"on\"", System.IO.File.ReadAllText(File));
    }

    /// <summary>
    /// §639: a Microsoft account's mail folders are listed — a folder's folders after it, to their own folders, named
    /// under it, the inbox itself not twice — each with the counts Graph lists it with, and read where turned on: the
    /// archive and a folder of a folder, never the receipts. The inbox turned off is listed and not read.
    /// </summary>
    [Fact]
    public async Task AMicrosoftAccountListsItsFoldersAndReadsThoseTurnedOn()
    {
        var asked = new List<string>();
        var http = new HttpClient(new Answering(asked, url => url switch
        {
            _ when url.Contains("/me/mailFolders/inbox?") => """{ "id": "IN", "unreadItemCount": 2, "totalItemCount": 20 }""",
            _ when url.Contains("/me/mailFolders?") => """
                { "value": [
                  { "id": "IN", "displayName": "Posteingang", "childFolderCount": 1, "unreadItemCount": 2, "totalItemCount": 20 },
                  { "id": "AR", "displayName": "Archive", "childFolderCount": 0, "unreadItemCount": 5, "totalItemCount": 50 },
                  { "id": "RE", "displayName": "Receipts", "childFolderCount": 0, "unreadItemCount": 0, "totalItemCount": 7 } ] }
                """,
            _ when url.Contains("/mailFolders/IN/childFolders?") => """{ "value": [ { "id": "PR", "displayName": "Projects", "childFolderCount": 1, "unreadItemCount": 1, "totalItemCount": 3 } ] }""",
            _ when url.Contains("/mailFolders/PR/childFolders?") => """{ "value": [ { "id": "Y26", "displayName": "2026", "childFolderCount": 0, "unreadItemCount": 4, "totalItemCount": 9 } ] }""",
            _ when url.Contains("/mailFolders/AR/messages?") => """{ "value": [ { "id": "m1", "subject": "Old", "receivedDateTime": "2026-10-01T08:00:00Z", "isRead": false } ] }""",
            _ when url.Contains("/mailFolders/Y26/messages?") => """{ "value": [ { "id": "m2", "subject": "Kick-off", "receivedDateTime": "2026-11-01T08:00:00Z", "isRead": true } ] }""",
            _ => """{ "value": [] }""",
        }));
        var account = Microsoft("a").Synced(SyncKeys.MailFolder("AR"), true).Synced(SyncKeys.MailFolder("Y26"), true);
        var reader = new MicrosoftReader(account, new Graph(http, _ => Task.FromResult("token")));

        var read = await reader.MailAsync(30, CancellationToken.None);

        Assert.Equal(
            [("IN", "Inbox", true), ("PR", "Inbox / Projects", false), ("Y26", "Inbox / Projects / 2026", true), ("AR", "Archive", true), ("RE", "Receipts", false)],
            read.Select(folder => (folder.Folder.Id, folder.Folder.Name, folder.Mail is not null)));
        Assert.True(read[0].Folder.Inbox);
        Assert.Equal((5, 50, "Old"), (read[3].Mail!.Unread, read[3].Mail!.Total, read[3].Mail!.Messages.Single().Subject));
        Assert.Equal("Kick-off", read[2].Mail!.Messages.Single().Subject);
        // Listed with its counts though not read: what the Accounts page shows beside its switch.
        Assert.Equal((0, 7), (read[4].Folder.Unread, read[4].Folder.Total));
        Assert.Contains(asked, url => url.Contains("/mailFolders/inbox/messages?"));
        Assert.DoesNotContain(asked, url => url.Contains("/mailFolders/RE/messages") || url.Contains("/mailFolders/PR/messages"));

        // The inbox turned off: listed with its counts, its messages not asked for; the archive read as ever.
        asked.Clear();
        reader = new MicrosoftReader(account.Synced(SyncKeys.Inbox, false), new Graph(http, _ => Task.FromResult("token")));
        read = await reader.MailAsync(30, CancellationToken.None);
        Assert.Null(read[0].Mail);
        Assert.Equal((2, 20), (read[0].Folder.Unread, read[0].Folder.Total));
        Assert.DoesNotContain(asked, url => url.Contains("/mailFolders/inbox/messages"));
        Assert.Contains(asked, url => url.Contains("/mailFolders/AR/messages?"));
    }

    /// <summary>
    /// §639: an IMAP server's folders as one LIST answers them: none that holds no mail, not the inbox; those of a special
    /// use (RFC 6154) first — drafts, sent, archive, junk, trash — by their own names, not their container's (Gmail's
    /// "[Gmail]"); the rest in the server's order, a folder in the inbox under "Inbox"; the virtual ones (\All, \Flagged)
    /// last; with the counts LIST-STATUS gave.
    /// </summary>
    [Fact]
    public void AnImapServersFoldersAreListedBySpecialUse()
    {
        Karpo.Accounts.Mail.ImapReader.ListedFolder Folder(string name, global::MailKit.FolderAttributes attributes = global::MailKit.FolderAttributes.None, int? unread = null, int? total = null) =>
            new(name, '/', attributes, unread, total);
        var listed = Karpo.Accounts.Mail.ImapReader.Listed(
        [
            Folder("INBOX", global::MailKit.FolderAttributes.HasChildren, 3, 30),
            Folder("INBOX/Projects", unread: 1, total: 4),
            Folder("[Gmail]", global::MailKit.FolderAttributes.NoSelect | global::MailKit.FolderAttributes.HasChildren),
            Folder("[Gmail]/All Mail", global::MailKit.FolderAttributes.All),
            Folder("[Gmail]/Trash", global::MailKit.FolderAttributes.Trash),
            Folder("Receipts"),
            Folder("[Gmail]/Sent Mail", global::MailKit.FolderAttributes.Sent),
            Folder("[Gmail]/Starred", global::MailKit.FolderAttributes.Flagged),
            Folder("[Gmail]/Drafts", global::MailKit.FolderAttributes.Drafts),
            Folder("[Gmail]/Spam", global::MailKit.FolderAttributes.Junk),
            Folder("Gone", global::MailKit.FolderAttributes.NonExistent),
            Folder("Receipts"),
        ], "INBOX");

        Assert.Equal(
            ["Drafts", "Sent Mail", "Spam", "Trash", "Inbox / Projects", "Receipts", "All Mail", "Starred"],
            listed.Select(folder => folder.Name));
        Assert.Equal("[Gmail]/Sent Mail", listed[1].Id);
        Assert.All(listed, folder => Assert.False(folder.Inbox));
        Assert.Equal((1, 4), (listed[4].Unread, listed[4].Total));
        Assert.Null(listed[5].Unread);
    }

    [Fact]
    public void EachStorePutsItsOwnKindAndAnAccountWithNothingGoes()
    {
        var report = new SyncReport(folder);
        var at = new DateTime(2026, 10, 11, 9, 0, 0, DateTimeKind.Utc);
        report.Put(AccountData.Calendars, [("a", new SyncPart { UpdatedUtc = at, Items = [new SyncItem { Id = "home", Name = "Home", Count = 56 }] })]);
        report.Put(AccountData.Mail, [
            ("a", new SyncPart { UpdatedUtc = at, Items = [new SyncItem { Id = SyncKeys.Inbox, Name = "Inbox", Count = 1234, Unread = 12 }] }),
            ("b", new SyncPart { Problem = "b could not be reached." }),
        ]);

        var file = report.Read();
        Assert.Equal(["a", "b"], file.Accounts.Select(account => account.Account));
        Assert.Equal(56, file.For("a")!.Calendars!.Items.Single().Count);
        Assert.Equal((1234, 12), (file.For("a")!.Mail!.Items.Single().Count, file.For("a")!.Mail!.Items.Single().Unread));
        Assert.Null(file.For("b")!.Calendars);
        Assert.Equal("b could not be reached.", file.For("b")!.Mail!.Problem);

        // The mail of b turned off: its part goes, and b with it; a's calendars stay.
        report.Put(AccountData.Mail, [("a", file.For("a")!.Mail!)]);
        file = report.Read();
        Assert.Equal(["a"], file.Accounts.Select(account => account.Account));
        Assert.NotNull(file.For("a")!.Calendars);
    }

    [Fact]
    public void TheReportIsEncryptedAndNotWrittenAgainForNothing()
    {
        var report = new SyncReport(folder);
        var part = new SyncPart { UpdatedUtc = DateTime.UtcNow, Items = [new SyncItem { Id = "x", Name = "Secret project calendar", Count = 3 }] };
        report.Put(AccountData.Calendars, [("a", part)]);

        var bytes = System.IO.File.ReadAllBytes(report.FilePath);
        Assert.DoesNotContain("Secret project", Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain("Secret project", Encoding.Unicode.GetString(bytes));

        var written = System.IO.File.GetLastWriteTimeUtc(report.FilePath);
        System.IO.File.SetLastWriteTimeUtc(report.FilePath, written.AddMinutes(-5));
        report.Put(AccountData.Calendars, [("a", part)]);
        Assert.Equal(written.AddMinutes(-5), System.IO.File.GetLastWriteTimeUtc(report.FilePath));

        // Damaged, or another user's: no report, and the next sync writes it again.
        System.IO.File.WriteAllBytes(report.FilePath, [1, 2, 3]);
        Assert.Empty(report.Read().Accounts);
        report.Put(AccountData.Calendars, [("a", part)]);
        Assert.Single(report.Read().Accounts);
    }

    /// <summary>Graph as a test wants it: each URL asked for is noted, and answered from the table.</summary>
    private sealed class Answering(List<string> asked, Func<string, string> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            lock (asked)
            {
                asked.Add(url);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(answer(url), Encoding.UTF8, "application/json"),
            });
        }
    }
}
