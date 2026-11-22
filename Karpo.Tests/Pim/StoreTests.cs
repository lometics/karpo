// The contacts and mail stores over several accounts (Karpo.Dock\Pim): what
// they merge, how they sort, and what they say when an account is missing or
// does not answer.

using Karpo.Accounts;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class StoreTests
{
    /// <summary>
    /// accounts.json read again on the watch's timer, and the read throws: it is reported, the set
    /// keeps the readers it had, and nothing leaves the timer's pool thread, where it would have
    /// ended the process (AccountSet.Reload had no catch).
    /// </summary>
    [Fact]
    public void AReadOfTheAccountsThatThrowsLeavesTheSetAsItWas()
    {
        var reads = 0;
        (AccountsRead, IReadOnlyList<IAccountReader>) Load() =>
            ++reads == 1 ? (AccountsRead.Missing, []) : throw new IOException("A test's accounts file throws on purpose (StoreTests)");
        using var set = new AccountSet(Load, new AccountWatch());
        var changed = 0;
        set.Changed += () => changed++;

        set.Reload();

        Assert.Equal(2, reads);
        Assert.Empty(set.Readers);
        Assert.Equal(0, changed);
    }

    private static Person Contact(string id, string name, string? email = null) =>
        new(id, name, null, email is null ? [] : [email], []);

    private static MailItem Mail(string id, int minute, bool unread = false) =>
        new(id, "Sender", null, $"Subject {id}", null, new DateTimeOffset(2026, 9, 26, 10, minute, 0, TimeSpan.Zero), unread);

    [Fact]
    public async Task ContactsFromEveryAccountAreOneListByName()
    {
        var home = new FakeReader(AccountKind.Dav, "home@example.com");
        home.People.AddRange([Contact("1", "Zoë Ammann", "zoe@example.ch"), Contact("2", "Émile Borel")]);
        var work = new FakeReader(AccountKind.Microsoft, "work@example.com");
        work.People.AddRange([Contact("3", "Adrian Hager"), Contact("4", "zoë ammann", "ZOE@example.ch")]);
        var store = new ContactStore(new AccountSet([home, work]));

        store.Refresh();
        await store.Idle();

        // By name, accents folded (É with the Es); the same person in two accounts is listed once.
        Assert.Equal(["Adrian Hager", "Émile Borel", "Zoë Ammann"], store.Contacts.Select(person => person.DisplayName));
        Assert.Null(store.Problem);
    }

    [Fact]
    public async Task AContactsReadIsNotRepeatedWhileItIsFresh()
    {
        var account = new FakeReader(AccountKind.Dav);
        var store = new ContactStore(new AccountSet([account]));

        store.Refresh();
        await store.Idle();
        store.Refresh();
        store.Refresh();
        await store.Idle();

        Assert.Equal(1, account.ContactReads);
    }

    /// <summary>
    /// Each account's contacts are their own sync: one that cannot be read keeps the people it gave
    /// last, and the others are read as before; a failure with nothing kept is the problem.
    /// </summary>
    [Fact]
    public async Task AnAccountThatFailsKeepsItsPeopleWhileTheOthersAreRead()
    {
        var home = new FakeReader(AccountKind.Dav, "home@example.com");
        home.People.Add(Contact("1", "Zoë Ammann"));
        var work = new FakeReader(AccountKind.Microsoft, "work@example.com");
        work.People.Add(Contact("2", "Adrian Hager"));
        using var store = new ContactStore(new AccountSet([home, work]));
        store.Refresh();
        await store.Idle();

        work.Failure = new HttpRequestException("no route");
        home.People.Add(Contact("3", "Émile Borel"));
        store.Reread();
        await store.Idle();

        Assert.Equal(["Adrian Hager", "Émile Borel", "Zoë Ammann"], store.Contacts.Select(person => person.DisplayName));
        Assert.Null(store.Problem);
        Assert.Equal(2, work.ContactReads);

        // Never read and failing: nothing to show, and the problem says why.
        var gone = new FakeReader(AccountKind.Dav, "gone@example.com") { Failure = new HttpRequestException("no route") };
        using var none = new ContactStore(new AccountSet([gone]));
        none.Refresh();
        await none.Idle();
        Assert.Empty(none.Contacts);
        Assert.NotNull(none.Problem);
    }

    [Fact]
    public async Task AnImapAccountIsNotAskedForContacts()
    {
        var mail = new FakeReader(AccountKind.Imap);
        var store = new ContactStore(new AccountSet([mail]));

        store.Refresh();
        await store.Idle();

        Assert.Equal(0, mail.ContactReads);
        Assert.Equal(ContactStore.NoAccount, store.Problem);
    }

    [Fact]
    public async Task MailFromEveryInboxIsNewestFirstAndTheUnreadAreCountedTogether()
    {
        var one = new FakeReader(AccountKind.Imap, "one@example.com") { Inbox = new Inbox(3, [Mail("a", 10, true), Mail("b", 30)]) };
        var two = new FakeReader(AccountKind.Microsoft, "two@example.com") { Inbox = new Inbox(2, [Mail("c", 20, true)]) };
        var dav = new FakeReader(AccountKind.Dav);
        using var store = new MailStore(new AccountSet([one, two, dav]));

        store.Refresh();
        await store.Idle();

        Assert.Equal(["b", "c", "a"], store.Messages.Select(message => message.Id));
        Assert.Equal(5, store.Unread);
        // A DAV account has no mail and is not asked for any.
        Assert.Equal(0, dav.InboxReads);
        Assert.Null(store.Problem);
    }

    [Fact]
    public async Task AnInboxThatDoesNotAnswerIsNamedAndTheOthersStay()
    {
        var good = new FakeReader(AccountKind.Imap, "good@example.com") { Inbox = new Inbox(1, [Mail("a", 1, true)]) };
        var bad = new FakeReader(AccountKind.Imap, "bad@example.com") { Failure = new SignInRequiredException("imap.example.com refused that user name and password.") };
        using var store = new MailStore(new AccountSet([good, bad]));

        store.Refresh();
        await store.Idle();

        Assert.Single(store.Messages);
        Assert.Equal(1, store.Unread);
        Assert.Equal("bad@example.com: imap.example.com refused that user name and password.", store.Problem);
    }

    [Fact]
    public async Task ADrawnMailWidgetReadsOnceAndAPanelOpeningStraightAfterDoesNotReadAgain()
    {
        var account = new FakeReader(AccountKind.Imap);
        using var store = new MailStore(new AccountSet([account]));

        for (var draw = 0; draw < 20; draw++)
        {
            store.Refresh();
        }
        await store.Idle();
        store.Refresh(fresh: true);
        await store.Idle();

        Assert.Equal(1, account.InboxReads);
    }

    [Fact]
    public async Task WithNoMailAccountTheWidgetSaysWhereToAddOne()
    {
        using var store = new MailStore(new AccountSet([]));

        store.Refresh();
        await store.Idle();

        Assert.False(store.HasAccount);
        Assert.Equal(MailStore.NoAccount, store.Problem);
    }

    [Fact]
    public void AtMostTheNewestAreKept()
    {
        var many = Enumerable.Range(0, 50).Select(minute => Mail(minute.ToString(System.Globalization.CultureInfo.InvariantCulture), minute));
        var kept = MailStore.Merged([many]);
        Assert.Equal(MailStore.Newest, kept.Count);
        Assert.Equal("49", kept[0].Id);
    }

    /// <summary>
    /// A quiet inbox beside a busy one: its newest are a day old, the other's thirty are of the last hour.
    /// Only the thirty newest of all, it showed nothing, as if it were never read; it shows its own newest
    /// now, after the other's by their time.
    /// </summary>
    [Fact]
    public async Task MailFromAQuietInboxStillShowsBesideABusyOne()
    {
        var busy = new FakeReader(AccountKind.Imap, "busy@example.com")
        {
            Inbox = new Inbox(0, [.. Enumerable.Range(0, 40).Select(minute => Mail($"busy{minute}", minute))]),
        };
        var yesterday = new DateTimeOffset(2026, 9, 25, 9, 0, 0, TimeSpan.Zero);
        var quiet = new FakeReader(AccountKind.Microsoft, "quiet@example.com")
        {
            Inbox = new Inbox(1, [
                new MailItem("quiet1", "Ruth", null, "Lunch", null, yesterday, true),
                new MailItem("quiet2", "Ruth", null, "Before", null, yesterday.AddHours(-3), false)]),
        };
        using var store = new MailStore(new AccountSet([busy, quiet]));

        store.Refresh();
        await store.Idle();

        Assert.Equal(["quiet1", "quiet2"], store.Messages.TakeLast(2).Select(message => message.Id));
        Assert.Equal(MailStore.Newest + 2, store.Messages.Count);
        Assert.Equal("busy39", store.Messages[0].Id);
    }

    /// <summary>
    /// One address book of three that the account will not give (a folder Graph refuses): the other two are
    /// read again, and its people stay as they were, with why on its row; never read and failing, it is what
    /// the line says is missing.
    /// </summary>
    [Fact]
    public async Task ABookThatAloneFailsKeepsItsPeopleAndTheOthersRefresh()
    {
        var account = new FakeReader(AccountKind.Microsoft, "work@example.com");
        account.Books.Add((new AddressBook("one", "Colleagues"), [Contact("1", "Adrian Hager")]));
        account.Books.Add((new AddressBook("two", "Family"), [Contact("2", "Zoë Ammann")]));
        account.Books.Add((new AddressBook("three", "Club"), [Contact("3", "Émile Borel")]));
        var folder = Directory.CreateTempSubdirectory("karpo-books-").FullName;
        try
        {
            var report = new SyncReport(folder);
            using var store = new ContactStore(new AccountSet([account]), report: report);
            store.Refresh();
            await store.Idle();

            account.Books[0].People.Add(Contact("4", "Beat Hagenbuch"));
            account.Books[1].People.Add(Contact("5", "Not Read"));
            account.Books[2].People.Add(Contact("6", "Ruth Imboden"));
            account.Failing.Add("two");
            store.Reread();
            await store.Idle();

            Assert.Equal(["Adrian Hager", "Beat Hagenbuch", "Émile Borel", "Ruth Imboden", "Zoë Ammann"], store.Contacts.Select(person => person.DisplayName));
            Assert.Null(store.Problem);
            var family = report.Read().For(account.Account.Id)!.Contacts!.Items.Single(item => item.Id == "two");
            Assert.Equal(1, family.Count);
            Assert.Contains("Family", family.Problem);

            // Failing from the first read: nothing of it to keep, and the line says it is missing.
            var fresh = new FakeReader(AccountKind.Microsoft, "new@example.com");
            fresh.Books.Add((new AddressBook("one", "Colleagues"), [Contact("1", "Adrian Hager")]));
            fresh.Books.Add((new AddressBook("two", "Family"), [Contact("2", "Zoë Ammann")]));
            fresh.Failing.Add("two");
            using var first = new ContactStore(new AccountSet([fresh]));
            first.Refresh();
            await first.Idle();
            Assert.Equal(["Adrian Hager"], first.Contacts.Select(person => person.DisplayName));
            Assert.Contains("Family", first.Problem);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
