// What adds an account, signs in to one again and removes one: Settings' Accounts page's, and since §324
// a widget's menu's on the bar too (Karpo.Dock\Dock.MenuAccounts.cs; docs\widgets.md §324) — one way for
// both, so an account added, mended or removed in either is the same in accounts.json. Since §338 a mail
// server's and a CalDAV server's too (the page's adds moved here), its password kept as every account's
// secret is (AccountBook.Add: protected by Windows for this user, DPAPI).

using Karpo.Accounts.Google;
using Karpo.Accounts.Microsoft;

namespace Karpo.Accounts;

public static class AccountSignIn
{
    /// <summary>How long a sign-in in the browser is waited for.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromMinutes(5);

    /// <summary>The sign-in of an account of <paramref name="kind"/> in the browser (<see cref="Account.InBrowser"/>); null for a kind with a password.</summary>
    public static OAuthClient? Browser(AccountKind kind) => kind switch
    {
        AccountKind.Microsoft => MicrosoftAuth.Shared,
        AccountKind.Google => GoogleAuth.Shared,
        _ => null,
    };

    /// <summary>Whether an account of <paramref name="kind"/> can be signed in to in the browser: this Karpo has its client there.</summary>
    public static bool CanSignIn(AccountKind kind) => Browser(kind)?.HasClientId == true;

    /// <summary>An account of <paramref name="kind"/> — Microsoft, Google — signed in to in the browser, and kept (<see cref="Keep"/>).</summary>
    /// <exception cref="ArgumentOutOfRangeException">A kind that signs in with a password.</exception>
    public static async Task<Account> AddAsync(AccountKind kind, CancellationToken cancellation)
    {
        var client = Browser(kind) ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        var found = await client.SignInAsync(cancellation).ConfigureAwait(false);
        return Keep(new Account
        {
            Id = Account.NewId(),
            Kind = kind,
            Address = found.Address,
            Name = found.Name.Length > 0 ? found.Name : null,
        }, found.RefreshToken);
    }

    /// <summary>
    /// The Microsoft or Google account signed in to again in the browser (Reconnect), asking for everything a sign-in
    /// now does — To Do, a kind the person unticked on Google's consent screen —, and its new sign-in kept on the same
    /// account: its id, what is turned off and its colours stay, and every line set to it goes on showing it. The dock
    /// hears accounts.json written and reads it at once. A sign-in to another account keeps nothing. The address the
    /// sign-in was for.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">An account that signs in with a password.</exception>
    public static async Task<string> SignInAgainAsync(Account account, CancellationToken cancellation)
    {
        var client = Browser(account.Kind) ?? throw new ArgumentOutOfRangeException(nameof(account), account.Kind, null);
        var found = await client.SignInAsync(cancellation, account.Address).ConfigureAwait(false);
        if (string.Equals(found.Address, account.Address, StringComparison.OrdinalIgnoreCase))
        {
            AccountBook.SetSecret(account.Id, found.RefreshToken);
            client.Forget(account.Id);
        }
        return found.Address;
    }

    /// <summary>
    /// §338: a mail server (IMAP) found for <paramref name="address"/> and signed in to with
    /// <paramref name="password"/> — on <paramref name="server"/>, its <paramref name="security"/> and
    /// <paramref name="port"/> where the person set them, with <paramref name="user"/> where it is not the
    /// address — and kept where <paramref name="keep"/> (else only tried: Test connection). The server's host.
    /// The Accounts page's add, and the dock's menu's form.
    /// </summary>
    /// <exception cref="SignInRequiredException">The server refused the password.</exception>
    /// <exception cref="AccountException">No server answered.</exception>
    /// <exception cref="ArgumentException">The address is none.</exception>
    public static async Task<string> MailAsync(string address, string password, string server, string user, MailSecurity? security, int? port, bool keep, CancellationToken cancellation)
    {
        var found = await new Mail.MailDiscovery().SignInAsync(address, user, password, server, cancellation, security, port).ConfigureAwait(false);
        if (keep)
        {
            Keep(new Account
            {
                Id = Account.NewId(),
                Kind = AccountKind.Imap,
                Address = address,
                User = found.User,
                Host = found.Host,
                Port = found.Port,
                Security = found.Security,
            }, password);
        }
        return found.Host;
    }

    /// <summary>
    /// §338: a CalDAV and CardDAV server found — the address's domain's, or <paramref name="server"/> where
    /// typed — and signed in to as <paramref name="user"/> (the address where empty), its calendars' and
    /// address books' homes found, and the account kept where <paramref name="keep"/>. The address it is
    /// kept under (the address, else the user at the server's host) and the server's host. The Accounts
    /// page's add, and the dock's menu's form.
    /// </summary>
    /// <exception cref="SignInRequiredException">The server refused the password.</exception>
    /// <exception cref="AccountException">No server answered.</exception>
    public static async Task<(string Address, string Host)> DavAsync(string address, string password, string server, string user, bool keep, CancellationToken cancellation)
    {
        var signIn = user.Length > 0 ? user : address;
        var home = await new Dav.DavDiscovery().FindAsync(server, address, signIn, password, cancellation).ConfigureAwait(false);
        var shown = address.Length > 0 ? address : $"{signIn}@{home.Principal.Host}";
        if (keep)
        {
            Keep(new Account
            {
                Id = Account.NewId(),
                Kind = AccountKind.Dav,
                Address = shown,
                Name = home.Name.Length > 0 ? home.Name : null,
                User = signIn,
                CalendarHome = home.Calendars,
                BookHome = home.Books,
            }, password);
        }
        return (shown, home.Principal.Host);
    }

    /// <summary>The account gone from Karpo, its sign-in forgotten on this PC; nothing in the account itself changes.</summary>
    public static void Remove(Account account)
    {
        AccountBook.Remove(account.Id);
        Browser(account.Kind)?.Forget(account.Id);
    }

    /// <summary>
    /// Keeps <paramref name="account"/>, in place of any account of the same kind and address: adding
    /// one again is how it is signed in again, and it takes a new id, which is what tells the dock
    /// to read it afresh.
    /// </summary>
    public static Account Keep(Account account, string secret)
    {
        foreach (var old in AccountBook.Load())
        {
            if (old.Kind == account.Kind && string.Equals(old.Address, account.Address, StringComparison.OrdinalIgnoreCase))
            {
                Remove(old);
            }
        }
        return AccountBook.Add(account, secret);
    }
}
