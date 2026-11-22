// The name an account is shown by (Karpo.Accounts\AccountNames.cs, docs\widgets.md §445): the name it gave,
// else its address, and where two would read the same their addresses; ⚙ › Accounts lists it by that name and a
// to-do line's menu groups its lists under it.

using Karpo.Accounts;

namespace Karpo.Tests.Accounts;

public sealed class AccountNamesTests
{
    private static Account Of(string id, AccountKind kind, string address, string? name = null) => new() { Id = id, Kind = kind, Address = address, Name = name };

    [Fact]
    public void AnAccountIsShownByItsNameElseItsAddress()
    {
        Account[] accounts =
        [
            Of("a", AccountKind.Microsoft, "ann@outlook.com", "Ann Smith"),
            Of("b", AccountKind.Imap, "ann@example.com"),
            Of("c", AccountKind.Dav, "ann@fastmail.com", "  "),
            Of("d", AccountKind.Microsoft, "bob@contoso.com", " Bob Jones "),
        ];
        var names = AccountNames.Of(accounts);
        Assert.Equal(["Ann Smith", "ann@example.com", "ann@fastmail.com", "Bob Jones"], accounts.Select(account => names[account.Id]));
    }

    /// <summary>Two accounts of one name — the person's own and their work's — would be told apart by nothing: each its address.</summary>
    [Fact]
    public void TwoAccountsOfOneNameAreShownByTheirAddresses()
    {
        Account[] accounts =
        [
            Of("a", AccountKind.Microsoft, "ann@outlook.com", "Ann Smith"),
            Of("b", AccountKind.Microsoft, "ann.smith@contoso.com", "ann smith"),
            Of("c", AccountKind.Dav, "ann@fastmail.com", "Fastmail"),
        ];
        var names = AccountNames.Of(accounts);
        Assert.Equal(["ann@outlook.com", "ann.smith@contoso.com", "Fastmail"], accounts.Select(account => names[account.Id]));
    }

    /// <summary>A to-do line's lists of an account carry its name, the heading they stand under in the line's menu.</summary>
    [Fact]
    public void AnAccountsToDoListsCarryItsName()
    {
        Account[] accounts =
        [
            Of("a", AccountKind.Microsoft, "ann@outlook.com", "Ann Smith"),
            Of("b", AccountKind.Microsoft, "bob@contoso.com"),
        ];
        var report = new SyncReportFile
        {
            Accounts =
            [
                new SyncReportAccount
                {
                    Account = "a",
                    Tasks = new SyncPart { Items = [new SyncItem { Id = "tasks", Name = "Tasks", Default = true }, new SyncItem { Id = "shop", Name = "Groceries" }] },
                },
            ],
        };
        var sources = TodoSources.Of(accounts, report);
        Assert.Equal(
            [("ann@outlook.com", "Ann Smith", "Tasks"), ("ann@outlook.com", "Ann Smith", "Groceries"), ("bob@contoso.com", "bob@contoso.com", S.WidgetTodoNewList)],
            sources.Select(source => (source.Account, source.AccountName, source.Name)));
    }
}
