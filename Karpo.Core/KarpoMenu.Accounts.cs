// §335: the accounts, every account Karpo has (OptionAccountRows.Page): every account listed with its kind under its
// address, "Sign in again" on a Microsoft account's, Remove asked first (AccountSignIn, through the dock's MenuAccounts),
// then every kind offered by "Add an account". None where the accounts are not listed. §338: each account's own rows are
// what is synced of it (its inboxes, calendars, address books and To Do lists, their switches and colours, "Reconnect"),
// and the mail server's and CalDAV's forms have a masked password field (OptionAccountRows, AccountForm). §351: the one
// place they are managed; a widget that shows what they hold links here.
//
// §535 (the person, 2026-11-07: "accounts should get their complete own section."): a page of their own, the settings
// panel's Accounts (KarpoPage.Accounts), no longer a level under Karpo's Services.

namespace Karpo.Core;

public static partial class KarpoMenu
{
    /// <summary>§535: the Accounts page: every account of <paramref name="services"/>' and the ways to add one; none where they are not listed.</summary>
    public static IReadOnlyList<OptionRow> Accounts(Config config, KarpoServices services) =>
        services.Accounts is { } accounts ? OptionAccountRows.Page(accounts) : [];
}
