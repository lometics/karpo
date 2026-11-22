// How the Accounts page writes an account in its list, as Windows writes a device or an app it
// lists (G58's spec, §2.7): the address as its name, and under it what Karpo shows of it -
// "Calendars, contacts, mail" - then where it comes from: the server's host, "Microsoft account", "Google account".
// Moved from the settings app for the dock's menus (§338).

using Karpo.Core;

namespace Karpo.Accounts;

public static class AccountRows
{
    /// <summary>What Karpo shows of an account: "Calendars, contacts, mail", "Mail"; never empty.</summary>
    public static string Gives(AccountData data) => (data & (AccountData.Calendars | AccountData.Contacts | AccountData.Mail)) switch
    {
        AccountData.Calendars | AccountData.Contacts | AccountData.Mail => S.SettingsAccountsGivesAll,
        AccountData.Calendars | AccountData.Contacts => S.SettingsAccountsGivesCalendarsContacts,
        AccountData.Calendars | AccountData.Mail => S.SettingsAccountsGivesCalendarsMail,
        AccountData.Contacts | AccountData.Mail => S.SettingsAccountsGivesContactsMail,
        AccountData.Calendars => S.SettingsAccountsGivesCalendars,
        AccountData.Contacts => S.SettingsAccountsGivesContacts,
        AccountData.Mail => S.SettingsAccountsGivesMail,
        _ => S.SettingsAccountsGivesNothing,
    };

    /// <summary>
    /// Where an account comes from: the mail server's host, the host of the DAV server's first
    /// home, "Microsoft account", "Google account"; "" where nothing says.
    /// </summary>
    public static string Source(Account account) => account.Kind switch
    {
        AccountKind.Microsoft => S.SettingsAccountsMicrosoftHeader,
        AccountKind.Google => S.SettingsAccountsGoogleHeader,
        AccountKind.Imap => account.Host ?? "",
        AccountKind.Dav when Uri.TryCreate(account.CalendarHome is { Length: > 0 } ? account.CalendarHome : account.BookHome, UriKind.Absolute, out var home) => home.Host,
        _ => "",
    };

    /// <summary>How the menus name <paramref name="kind"/>: its header, its add, its glyph (OptionAccountRows).</summary>
    public static OptionAccountKind Option(AccountKind kind) => kind switch
    {
        AccountKind.Microsoft => OptionAccountKind.Microsoft,
        AccountKind.Google => OptionAccountKind.Google,
        AccountKind.Imap => OptionAccountKind.Mail,
        _ => OptionAccountKind.Dav,
    };

    /// <summary>The account kind a menu's <paramref name="kind"/> adds.</summary>
    public static AccountKind Kind(OptionAccountKind kind) => kind switch
    {
        OptionAccountKind.Microsoft => AccountKind.Microsoft,
        OptionAccountKind.Google => AccountKind.Google,
        OptionAccountKind.Mail => AccountKind.Imap,
        _ => AccountKind.Dav,
    };

    /// <summary>The line under an account's address: what it gives, and where it comes from where that is known.</summary>
    public static string Description(Account account) =>
        Source(account) is { Length: > 0 } source
            ? S.SettingsAccountsRowDescription(gives: Gives(account.Gives), source: source)
            : Gives(account.Gives);
}
