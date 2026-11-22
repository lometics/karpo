// Where an event opens (docs\widgets.md §434): on its account's own site, the only place a program can send a
// person to one event or one day of one account. A Microsoft event names its own address in Outlook on the web
// (Graph's webLink, kept on the occurrence by the reader). For a day, the account's web calendar on that day:
// Outlook on the web's day view (outlook.live.com for a personal address, outlook.office.com for any other),
// Google Calendar's for a Google account and a Google CalDAV one, iCloud's calendar (which opens at today; it takes
// no day) for an iCloud one. A CalDAV server Karpo knows no web calendar for gives none.
//
// Only an https address is ever handed on (MailLinks.Web): what opens it is the shell.

using System.Globalization;

namespace Karpo.Accounts;

public static class CalendarLinks
{
    /// <summary>The domains of Microsoft's personal accounts, whose Outlook on the web is outlook.live.com.</summary>
    private static readonly string[] Personal = ["outlook.", "hotmail.", "live.", "msn.com", "passport.com", "windowslive.com"];

    /// <summary>
    /// <paramref name="account"/>'s web calendar on <paramref name="day"/>: Outlook on the web's day view, Google
    /// Calendar's, iCloud's calendar; null for an account whose calendars Karpo knows no site of.
    /// </summary>
    public static string? Day(Account account, DateOnly day)
    {
        var date = day.ToString("yyyy'/'M'/'d", CultureInfo.InvariantCulture);
        switch (account.Kind)
        {
            case AccountKind.Microsoft:
                var domain = account.Address[(account.Address.LastIndexOf('@') + 1)..].Trim().ToLowerInvariant();
                return Personal.Any(personal => domain.StartsWith(personal, StringComparison.Ordinal))
                    ? $"https://outlook.live.com/calendar/0/view/day/{date}"
                    : $"https://outlook.office.com/calendar/view/day/{date}";
            case AccountKind.Google:
                return Google(account, date);
            case AccountKind.Dav when Uri.TryCreate(account.CalendarHome, UriKind.Absolute, out var home):
                var host = home.Host.ToLowerInvariant();
                return host is "apidata.googleusercontent.com" or "www.google.com" or "calendar.google.com" ? Google(account, date)
                    : host == "icloud.com" || host.EndsWith(".icloud.com", StringComparison.Ordinal) ? "https://www.icloud.com/calendar/"
                    : null;
            default:
                return null;
        }
    }

    /// <summary>Google Calendar's day view of <paramref name="account"/>'s calendars on <paramref name="date"/> (<c>yyyy/M/d</c>).</summary>
    private static string Google(Account account, string date)
    {
        // u/<address> picks the signed-in account the calendar is in, where several are.
        var who = Uri.EscapeDataString(string.IsNullOrWhiteSpace(account.Address) ? "0" : account.Address.Trim());
        return $"https://calendar.google.com/calendar/u/{who}/r/day/{date}";
    }
}
