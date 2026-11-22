// accounts: adding a mail server (IMAP) or a CalDAV and CardDAV server in a menu (docs\widgets.md §338;
// the settings-in-the-dock plan's P4, Q6 A: "a masked password field in the dock's menu"). The Accounts
// page's two forms as a level of rows, the same wherever "Add an account" is — ⚙ Settings › Accounts and
// every widget's Accounts (§324):
//
//   Email address          you@example.com ›       a line of text
//   Password               •••••••• ›              a masked line (OptionRow.Masked): bullets, no copy
//   Set the server myself  [toggle]
//     Server               imap.example.com ›      while set myself
//     Port                 993 ›                   IMAP's alone: empty for its security's usual one
//     Security             SSL/TLS ›               IMAP's alone: SSL/TLS or STARTTLS
//     User name            ›                       where it is not the address
//   ──
//   Test connection                                signs in and says how it went, keeping nothing
//   Add                                            signs in and keeps the account, as the page's Add
//
// The form is a draft, not the config: what is typed is kept on the form (AccountForm), which the rows
// are read from each time the menu is shown, and nothing of the config changes. Test and Add are greyed,
// what is missing under them, until the form can be tried; while one runs every row is greyed and says
// what it is doing, and how it went stands under it once it is back. Adding keeps the account as the page
// does (Karpo.Accounts' AccountSignIn, its password protected by Windows, DPAPI) and empties the form.
//
// The password is never a row's text, never the config's, never logged: the form holds it as characters
// it wipes once the account is added or the menu closes (Close), as far as .NET's strings let it.
//
// What tries the form is the caller's (AccountForms' connect: the dock's signs in over the network on a
// thread of its own; a test's answers at once), and how it went comes back on the menu's thread (post).

namespace Karpo.Core;

/// <summary>How a mail server is spoken to: TLS from the start (993), or STARTTLS on a plain connection (143).</summary>
public enum AccountSecurity
{
    Tls,
    StartTls,
}

/// <summary>Where a form is: being filled in, being tried (tested or added), or how its last try went.</summary>
public enum AccountFormState
{
    Editing,
    Testing,
    Adding,
    Tested,
    Added,
    Failed,
}

/// <summary>How a form's try went: the server's host it signed in to, or why it did not (<paramref name="Problem"/>).</summary>
public sealed record AccountTry(string Host, string? Problem = null)
{
    public bool Worked => Problem is null;
}

/// <summary>A mail server's or a CalDAV server's form, as typed so far; see the top of the file.</summary>
public sealed class AccountForm(OptionAccountKind kind)
{
    /// <summary>A port's most.</summary>
    public const int MostPort = 65535;

    private char[] password = [];

    /// <summary>Which form it is: <see cref="OptionAccountKind.Mail"/> or <see cref="OptionAccountKind.Dav"/>.</summary>
    public OptionAccountKind Kind { get; } = kind;

    public string Address { get; private set; } = "";

    /// <summary>"Set the server myself": the server, its port and security and the user name are typed, else found from the address.</summary>
    public bool OwnServer { get; private set; }

    public string Server { get; private set; } = "";

    /// <summary>The port as typed; empty for the security's usual one.</summary>
    public string Port { get; private set; } = "";

    public AccountSecurity Security { get; private set; }

    public string User { get; private set; } = "";

    public AccountFormState State { get; private set; }

    /// <summary>How the last try went, in words; null while the form is being filled in.</summary>
    public string? Outcome { get; private set; }

    /// <summary>Whether the last try was an add (else a test): the row its outcome stands under.</summary>
    public bool Added { get; private set; }

    /// <summary>Whether a try is on its way: the rows are greyed.</summary>
    public bool Busy => State is AccountFormState.Testing or AccountFormState.Adding;

    public bool HasPassword => password.Length > 0;

    /// <summary>The password, for the try alone: a new string each time, which nothing keeps.</summary>
    public string Password() => new(password);

    /// <summary>The port typed as a number; null for the usual one; 0 for words that are no port.</summary>
    public int? PortNumber =>
        Port.Length == 0 ? null
        : int.TryParse(Port, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var port) && port is > 0 and <= MostPort ? port
        : 0;

    /// <summary>The port it is tried on: the typed one, else its security's usual one.</summary>
    public int UsualPort => Security == AccountSecurity.StartTls ? 143 : 993;

    /// <summary>
    /// What it lacks before it can be tried, in the page's words; null for nothing. A mail server needs an
    /// address and its password, and a port, where one is typed, from 1 to 65535; a CalDAV server an
    /// address or a server, the password, and a user name where there is no address.
    /// </summary>
    public string? Problem
    {
        get
        {
            var own = OwnServer;
            if (Kind == OptionAccountKind.Mail)
            {
                var at = Address.LastIndexOf('@');
                if (at <= 0 || at >= Address.Length - 1 || !HasPassword)
                {
                    return S.SettingsAccountsImapMissing;
                }
                return own && PortNumber == 0 ? S.DockAccountsPortBad : null;
            }
            if ((Address.Length == 0 && (!own || Server.Length == 0)) || !HasPassword)
            {
                return S.SettingsAccountsDavMissing;
            }
            return SignInName.Length == 0 ? S.SettingsAccountsDavUser : null;
        }
    }

    /// <summary>A CalDAV server's user name: the one typed, else the address.</summary>
    public string SignInName => OwnServer && User.Length > 0 ? User : Address;

    public bool SetAddress(string typed) => Edit(() => Address = typed.Trim());

    public bool SetOwnServer(bool on) => Edit(() => OwnServer = on);

    public bool SetServer(string typed) => Edit(() => Server = typed.Trim());

    public bool SetPort(string typed) => Edit(() => Port = typed.Trim());

    public bool SetSecurity(AccountSecurity security) => Edit(() => Security = security);

    public bool SetUser(string typed) => Edit(() => User = typed.Trim());

    /// <summary>The password typed, in place of the one before, which is wiped.</summary>
    public bool SetPassword(string typed) => Edit(() =>
    {
        Array.Clear(password);
        password = typed.ToCharArray();
    });

    /// <summary>Everything typed gone, the password wiped; how the last try went too, unless <paramref name="keepOutcome"/>.</summary>
    public void Clear(bool keepOutcome = false)
    {
        Array.Clear(password);
        password = [];
        (Address, OwnServer, Server, Port, Security, User) = ("", false, "", "", AccountSecurity.Tls, "");
        if (!keepOutcome)
        {
            (State, Outcome, Added) = (AccountFormState.Editing, null, false);
        }
    }

    /// <summary>A try begins (AccountForms.Try): the rows greyed, saying what it does.</summary>
    internal void Begin(bool add)
    {
        (State, Outcome, Added) = (add ? AccountFormState.Adding : AccountFormState.Testing, null, add);
    }

    /// <summary>The menu closed while a try was on its way: what was typed goes once it is back.</summary>
    internal bool Left { get; set; }

    /// <summary>
    /// A try came back: how it went; an account added empties the form, saying it was, as does a menu
    /// closed meanwhile (<see cref="Left"/>).
    /// </summary>
    internal void End(AccountTry tried, string address)
    {
        var add = State == AccountFormState.Adding;
        (State, Outcome) = !tried.Worked ? (AccountFormState.Failed, tried.Problem)
            : add ? (AccountFormState.Added, S.SettingsAccountsAddedAt(address: address, host: tried.Host))
            : (AccountFormState.Tested, S.DockAccountsConnected(tried.Host));
        if ((add && tried.Worked) || Left)
        {
            Clear(keepOutcome: true);
        }
        Left = false;
    }

    /// <summary>A field set, unless a try is on its way: the last try's outcome goes with what it was for. Changes nothing of the config: false.</summary>
    private bool Edit(Action set)
    {
        if (Busy)
        {
            return false;
        }
        set();
        (State, Outcome, Added) = (AccountFormState.Editing, null, false);
        return false;
    }
}

/// <summary>
/// The two forms, kept while the menus come and go, and what tries one: <paramref name="connect"/> signs
/// in with what <see cref="AccountForm"/> holds — keeping the account where asked — on the menu's thread,
/// answering when it is done; <paramref name="post"/> brings an answer back to that thread; <paramref name="changed"/>
/// shows the menu again.
/// </summary>
public sealed class AccountForms(Func<AccountForm, bool, Task<AccountTry>> connect, Action<Action> post, Action changed)
{
    public AccountForm Mail { get; } = new(OptionAccountKind.Mail);

    public AccountForm Dav { get; } = new(OptionAccountKind.Dav);

    /// <summary>The form of <paramref name="kind"/>; null for a Microsoft account, which signs in in the browser.</summary>
    public AccountForm? Of(OptionAccountKind kind) => kind switch
    {
        OptionAccountKind.Mail => Mail,
        OptionAccountKind.Dav => Dav,
        _ => null,
    };

    /// <summary>
    /// <paramref name="form"/> tried — tested, or added where <paramref name="add"/> —, unless it cannot
    /// be yet or a try is on its way: greyed meanwhile, how it went once back. Whether it began.
    /// </summary>
    public bool Try(AccountForm form, bool add)
    {
        if (form.Busy || form.Problem is not null)
        {
            return false;
        }
        var address = form.Kind == OptionAccountKind.Mail || form.Address.Length > 0 ? form.Address : form.SignInName;
        form.Begin(add);
        Task<AccountTry> trying;
        try
        {
            trying = connect(form, add);
        }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException)
        {
            trying = Task.FromResult(new AccountTry("", error.Message));
        }
        _ = trying.ContinueWith(
            done => post(() =>
            {
                form.End(done.IsCompletedSuccessfully ? done.Result : new AccountTry("", done.Exception?.GetBaseException().Message ?? S.SettingsAccountsSignInCancelled), address);
                changed();
            }),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return true;
    }

    /// <summary>The menu closed: what was typed in a form at rest is gone, its password wiped; one being tried is left to finish.</summary>
    public void Close()
    {
        foreach (var form in new[] { Mail, Dav })
        {
            if (form.Busy)
            {
                form.Left = true;
            }
            else
            {
                form.Clear();
            }
        }
    }
}
