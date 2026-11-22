// accounts.json (Karpo.Accounts\AccountBook.cs): every change a patch on the
// file as it is, secrets protected for this user, a broken file kept aside,
// and what the dock counts as the accounts having changed (AccountSet.Shape).

using Karpo.Accounts;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Accounts;

public sealed class AccountBookTests : IDisposable
{
    private readonly string folder = Path.Combine(Isolation.Folder, "books", Guid.NewGuid().ToString("N"));

    private string File => Path.Combine(folder, "accounts.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static Account Imap(string id, string address) => new()
    {
        Id = id,
        Kind = AccountKind.Imap,
        Address = address,
        Host = "imap.example.com",
        Port = 993,
        Secret = Secrets.Protect("hunter2"),
    };

    [Fact]
    public void ASecretIsNeverWrittenInTheClearAndReadsBackForThisUser()
    {
        var kept = Secrets.Protect("correct horse battery staple");
        Assert.DoesNotContain("horse", kept);
        Assert.Equal("correct horse battery staple", Secrets.Unprotect(kept));
        // Another PC's, or a damaged one: a sign-in to do again, never an exception.
        Assert.Null(Secrets.Unprotect(Convert.ToBase64String(new byte[40])));
        Assert.Null(Secrets.Unprotect("not base64!"));
        Assert.Null(Secrets.Unprotect(null));
    }

    [Fact]
    public void TwoChangesToDifferentAccountsBothStay()
    {
        // The settings app adds an account while the dock writes a token back: each is a patch on
        // the file as it is, so neither undoes the other.
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));
        AccountBook.Change(File, file => file.Accounts[0] = file.Accounts[0] with { Secret = Secrets.Protect("new") });

        var accounts = AccountBook.Read(File, out var read).Accounts;
        Assert.Equal(AccountsRead.Ok, read);
        Assert.Equal(["a", "b"], accounts.Select(account => account.Id));
        Assert.Equal("new", Secrets.Unprotect(accounts[0].Secret));
        Assert.Equal("hunter2", Secrets.Unprotect(accounts[1].Secret));
    }

    [Fact]
    public void TheFileSaysWhatAnAccountIsAndNothingThatCanBeDerived()
    {
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        var text = System.IO.File.ReadAllText(File);

        Assert.Contains("\"kind\": \"Imap\"", text);
        Assert.Contains("\"security\": \"Implicit\"", text);
        Assert.DoesNotContain("gives", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hunter2", text);
    }

    [Fact]
    public void ABrokenFileIsNoAccountsAndIsKeptAsideBeforeItIsWritten()
    {
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(File, "{ this is not json");

        Assert.Empty(AccountBook.Read(File, out var read).Accounts);
        Assert.Equal(AccountsRead.Broken, read);

        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        Assert.Single(Directory.GetFiles(folder, "accounts.json.broken-*"));
        Assert.Single(AccountBook.Read(File, out _).Accounts);
    }

    /// <summary>
    /// A virus scanner, a backup or the indexer holds accounts.json, sharing nothing, for a moment as a
    /// token is written back: the read waits for it, and the token lands with every account still
    /// there. Before, the read that failed was no accounts, and the token's patch on none was written.
    /// </summary>
    [Fact]
    public async Task ATokenWrittenBackWhileTheFileIsHeldForAMomentLandsWithEveryAccount()
    {
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));

        var holder = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.None);
        var letGo = Task.Run(async () =>
        {
            await Task.Delay(150);
            await holder.DisposeAsync();
        });
        AccountBook.SetSecret(File, "a", "new");
        await letGo;

        var accounts = AccountBook.Read(File, out var read).Accounts;
        Assert.Equal(AccountsRead.Ok, read);
        Assert.Equal(["a", "b"], accounts.Select(account => account.Id));
        Assert.Equal("new", Secrets.Unprotect(accounts[0].Secret));
    }

    /// <summary>
    /// Held the whole time by a program that lets the file be replaced but not read: nothing is
    /// known of what it says, so nothing is written — the change fails for the caller to try again —
    /// and every account is there once the file is free.
    /// </summary>
    [Fact]
    public void AChangeWhileTheFileCannotBeReadWritesNothing()
    {
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));

        using (new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.Delete))
        {
            Assert.Equal(AccountsRead.Busy, AccountBook.Load(File, out var none));
            Assert.Empty(none);
            Assert.Throws<IOException>(() => AccountBook.SetSecret(File, "a", "new"));
        }

        var accounts = AccountBook.Read(File, out var read).Accounts;
        Assert.Equal(AccountsRead.Ok, read);
        Assert.Equal(["a", "b"], accounts.Select(account => account.Id));
        Assert.Equal("hunter2", Secrets.Unprotect(accounts[0].Secret));
    }

    /// <summary>
    /// accounts.json empty — a write the disk never finished, a crash — reads as the version before
    /// it, accounts.json.bak, which every change keeps; the next change starts from that and leaves
    /// the good backup where it is.
    /// </summary>
    [Fact]
    public void AnEmptyFileIsTheVersionBeforeIt()
    {
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));
        System.IO.File.WriteAllBytes(File, []);

        Assert.Equal(AccountsRead.Ok, AccountBook.Load(File, out var accounts));
        Assert.Equal(["a"], accounts.Select(account => account.Id));

        AccountBook.Change(File, file => file.Accounts.Add(Imap("c", "c@example.com")));
        Assert.Equal(["a", "c"], AccountBook.Read(File, out _).Accounts.Select(account => account.Id));
        Assert.Contains("a@example.com", System.IO.File.ReadAllText(File + ".bak"));
        Assert.DoesNotContain("c@example.com", System.IO.File.ReadAllText(File + ".bak"));
    }

    /// <summary>
    /// Another program reads accounts.json without letting it be replaced (as Karpo's own read did):
    /// a change waits for it rather than failing at once.
    /// </summary>
    [Fact]
    public async Task AChangeWhileAReaderHoldsTheFileGoesThrough()
    {
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));

        var reader = new FileStream(File, FileMode.Open, FileAccess.Read, FileShare.Read);
        var letGo = Task.Run(async () =>
        {
            await Task.Delay(150);
            await reader.DisposeAsync();
        });
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));
        await letGo;

        Assert.Equal(["a", "b"], AccountBook.Read(File, out _).Accounts.Select(account => account.Id));
    }

    /// <summary>
    /// A change cut short between File.Replace's two renames (the power gone at that moment): no
    /// accounts.json, the version before it as accounts.json.bak. That is the person's accounts —
    /// read as "none", the stores would let every account's cache go.
    /// </summary>
    [Fact]
    public void AFileGoneWithItsVersionBeforeLeftIsThatVersion()
    {
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));
        System.IO.File.Move(File, File + ".bak", overwrite: true);

        Assert.Equal(AccountsRead.Ok, AccountBook.Load(File, out var accounts));
        Assert.Equal(["a", "b"], accounts.Select(account => account.Id));
        AccountBook.Change(File, file => file.Accounts.Add(Imap("c", "c@example.com")));
        Assert.Equal(["a", "b", "c"], AccountBook.Read(File, out _).Accounts.Select(account => account.Id));
    }

    [Fact]
    public void TwoBrokenFilesKeptTheSameSecondAreBothKept()
    {
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(File, "{ first typo");
        AccountBook.Change(File, file => file.Accounts.Add(Imap("a", "a@example.com")));
        System.IO.File.WriteAllText(File, "{ second typo");
        AccountBook.Change(File, file => file.Accounts.Add(Imap("b", "b@example.com")));

        var kept = Directory.GetFiles(folder, "accounts.json.broken-*").Select(System.IO.File.ReadAllText).Order().ToList();
        Assert.Equal(["{ first typo", "{ second typo"], kept);
    }

    [Fact]
    public void AnAccountWithoutAnIdOrAnAddressIsDropped()
    {
        Directory.CreateDirectory(folder);
        System.IO.File.WriteAllText(File, """
            { "version": 1, "accounts": [
              { "id": "a", "kind": "Microsoft", "address": "me@outlook.com" },
              { "id": "", "kind": "Imap", "address": "x@example.com" },
              { "id": "c", "kind": "Dav", "address": "" }
            ] }
            """);
        var account = Assert.Single(AccountBook.Read(File, out _).Accounts);
        Assert.Equal(AccountKind.Microsoft, account.Kind);
    }

    [Fact]
    public void WhatAnAccountGivesFollowsItsKind()
    {
        Assert.Equal(AccountData.Calendars | AccountData.Contacts | AccountData.Mail | AccountData.Tasks,
            new Account { Id = "m", Kind = AccountKind.Microsoft, Address = "a" }.Gives);
        Assert.Equal(AccountData.Mail, Imap("i", "a").Gives);
        Assert.Equal(AccountData.Contacts,
            new Account { Id = "d", Kind = AccountKind.Dav, Address = "a", BookHome = "https://x/books/" }.Gives);
    }

    [Fact]
    public void ATokenWrittenBackIsNotAChangeButAPasswordTypedAgainIs()
    {
        var microsoft = new Account { Id = "m", Kind = AccountKind.Microsoft, Address = "me@outlook.com", Secret = Secrets.Protect("one") };
        var imap = Imap("i", "a@example.com");
        var shape = AccountSet.Shape([microsoft, imap]);

        // The dock writes a new refresh token every hour; the stores must not read again for it.
        Assert.Equal(shape, AccountSet.Shape([microsoft with { Secret = Secrets.Protect("two") }, imap]));
        Assert.NotEqual(shape, AccountSet.Shape([microsoft, imap with { Secret = Secrets.Protect("other") }]));
        Assert.NotEqual(shape, AccountSet.Shape([microsoft]));
    }
}
