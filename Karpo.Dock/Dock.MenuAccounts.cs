// accounts: what the settings panel's Accounts page (§535) lists and does (Karpo.Core\OptionAccountRows.cs, docs\widgets.md §324; §351
// a widget's menu links there), and a to-do line's To Do lists renamed and deleted (OptionRows.Lines.cs): Settings'
// Accounts page's own ways (Karpo.Accounts\AccountSignIn.cs), so an account added, signed in to again or
// removed here is the same in accounts.json as one there, which every widget then hears (AccountSet).
//
// A Microsoft or Google account (§687) signs in in the browser, there and then, as the page's "Sign in" does;
// one sign-in at a time. §338: a mail server and a CalDAV server are added in their forms in the menu (AccountForm.cs),
// tried on a thread of their own and answered back on the dock's, where the menu standing open is shown
// again; each account lists what is synced of it as the page's card did (AccountSyncRows.Menu, from
// accounts.json and the dock's sync-report.json), its switches and colours written where the page wrote
// them (AccountBook.SetSynced, SetColour). Nothing opens the settings app any more.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Pim;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>§338: the mail server's and the CalDAV server's forms, kept while the menus come and go; made at the first menu that lists the accounts.</summary>
    private AccountForms? accountForms;

    /// <summary>The accounts as accounts.json has them now, what is synced of each as the dock last said, and what a menu does to them.</summary>
    private OptionAccounts MenuAccounts()
    {
        var accounts = AccountBook.Load();
        var report = SyncReport.Here.Read();
        var colours = AccountSyncRows.Colours(accounts, report);
        var now = Clock.Real();
        var names = AccountNames.Of(accounts);
        return new OptionAccounts(
            [.. accounts.Select(account => AccountSyncRows.Menu(account, report.For(account.Id), now, colours) with { Name = names[account.Id] })],
            kind => MenuSignIn.Start(cancel => AccountSignIn.AddAsync(AccountRows.Kind(kind), cancel)),
            id => MenuSignIn.Start(AccountBook.Find(id) is { InBrowser: true } account ? cancel => AccountSignIn.SignInAgainAsync(account, cancel) : null),
            id =>
            {
                if (AccountBook.Find(id) is { } account)
                {
                    AccountSignIn.Remove(account);
                }
            },
            accountForms ??= new AccountForms(MenuAccountTry.Run, PostToDock(), MenuAccountsChanged))
        {
            CanSignIn = kind => AccountSignIn.CanSignIn(AccountRows.Kind(kind)),
            Sync = AccountBook.SetSynced,
            Recolour = AccountBook.SetColour,
        };
    }
}

/// <summary>§338: an account's form tried from a menu (AccountForms' connect).</summary>
internal static class MenuAccountTry
{
    /// <summary>
    /// <paramref name="form"/> signed in to — kept where <paramref name="add"/> — as the Accounts page's
    /// Add does (AccountSignIn), on a thread of its own, giving up after the sign-in's patience; what goes
    /// wrong is how it went, in the error's words.
    /// </summary>
    public static Task<AccountTry> Run(AccountForm form, bool add)
    {
        // Read here, on the dock's thread, before the form can change.
        var (kind, address, own, server, user) = (form.Kind, form.Address, form.OwnServer, form.Server, form.User);
        var security = own ? (form.Security == AccountSecurity.StartTls ? MailSecurity.StartTls : MailSecurity.Implicit) : (MailSecurity?)null;
        var port = own ? form.PortNumber : null;
        var password = form.Password();
        return Task.Run(async () =>
        {
            using var cancel = new CancellationTokenSource(AccountSignIn.Patience);
            try
            {
                if (kind == OptionAccountKind.Mail)
                {
                    return new AccountTry(await AccountSignIn.MailAsync(address, password, own ? server : "", own ? user : "", security, port, add, cancel.Token).ConfigureAwait(false));
                }
                var (_, host) = await AccountSignIn.DavAsync(address, password, own ? server : "", own ? user : "", add, cancel.Token).ConfigureAwait(false);
                return new AccountTry(host);
            }
            catch (OperationCanceledException)
            {
                return new AccountTry("", S.SettingsAccountsSignInCancelled);
            }
            catch (Exception error) when (error is SignInRequiredException or AccountException or ArgumentException or HttpRequestException or IOException or System.Net.Sockets.SocketException)
            {
                return new AccountTry("", error.Message);
            }
        });
    }
}

public sealed unsafe partial class Dock
{
    /// <summary>§338: what posts an answer back to the dock's thread (its dispatcher queue); at once where it has none (a test's thread).</summary>
    private static Action<Action> PostToDock()
    {
        var queue = Windows.System.DispatcherQueue.GetForCurrentThread();
        return work =>
        {
            if (queue is null || !queue.TryEnqueue(() => Troubles.Guard("Accounts: a form's answer", work)))
            {
                work();
            }
        };
    }

    /// <summary>§338: a form's try came back: a menu standing open shown again as it is now; §535 the settings panel's Accounts page too.</summary>
    private void MenuAccountsChanged()
    {
        if (disposed)
        {
            return;
        }
        if (flyout is { IsOpen: true } list && pending is { } what && what.Options.Count > 0)
        {
            OptionPicked(what, new OptionPick(0, OptionPickKind.None), list);
        }
        if (panelOn is not null)
        {
            StirChrome();
            PresentChrome();
        }
    }

    /// <summary>§338: a menu closed: what was typed in the forms goes, the password wiped (AccountForms.Close).</summary>
    private void MenuAccountsClosed() => accountForms?.Close();

    /// <summary>§324: the accounts' To Do lists, renamed and deleted in To Do through the dock's tasks (TaskStore).</summary>
    private sealed class MenuTodoLists : ITodoLists
    {
        public static readonly MenuTodoLists Shared = new();

        public bool Rename(string account, string list, string name) => TaskStore.Shared.RenameList(account, list, name);

        public bool Delete(string account, string list) => TaskStore.Shared.DeleteList(account, list);
    }
}

/// <summary>§324: a sign-in in the browser from a menu, one at a time.</summary>
internal static class MenuSignIn
{
    /// <summary>The sign-in on its way; null for none.</summary>
    private static Task? signingIn;

    /// <summary><paramref name="signIn"/>, unless one is on its way already; what goes wrong is logged.</summary>
    public static void Start(Func<CancellationToken, Task>? signIn)
    {
        if (signIn is null || signingIn is { IsCompleted: false })
        {
            return;
        }
        signingIn = Task.Run(() => Troubles.Guard("Accounts: signing in from a menu", () => RunAsync(signIn)));
    }

    private static async Task RunAsync(Func<CancellationToken, Task> signIn)
    {
        using var cancel = new CancellationTokenSource(AccountSignIn.Patience);
        try
        {
            await signIn(cancel.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is OperationCanceledException or AccountException or HttpRequestException or IOException or System.Net.Sockets.SocketException)
        {
            Console.Error.WriteLine($"Karpo: a sign-in from a menu did not finish ({error.GetType().Name}).");
        }
    }
}
