// The name an account is shown by (docs\widgets.md §445): ⚙ › Accounts lists it by it, its address under it,
// and a to-do line's menu groups the account's lists under it. The name the account gave (Account.Name, a
// Microsoft account's display name, a DAV server's), else its address; and where two accounts would read the
// same — the person's own and their work's, both "Ann Smith" — each its address, which tells them apart.

namespace Karpo.Accounts;

public static class AccountNames
{
    /// <summary>Each of <paramref name="accounts"/>' names, by its id.</summary>
    public static IReadOnlyDictionary<string, string> Of(IReadOnlyCollection<Account> accounts)
    {
        static string? Given(Account account) => account.Name?.Trim() is { Length: > 0 } name ? name : null;
        var twice = accounts.Select(Given).OfType<string>().GroupBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Where(same => same.Count() > 1).Select(same => same.Key).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        return accounts.ToDictionary(account => account.Id, account => Given(account) is { } name && !twice.Contains(name) ? name : account.Address, StringComparer.Ordinal);
    }
}
