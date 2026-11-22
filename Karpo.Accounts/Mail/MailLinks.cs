// Where a message opens: the account's web mail, the only place a program can send a person to one
// message of one account. A Microsoft account names the message's own address in Outlook on the web
// (Graph's webLink). Gmail — a Google account, or Gmail's IMAP server — finds a message by its
// Message-ID (a search, which opens the one it finds). An IMAP server names nothing, so the web mail
// is known by its server: the others Karpo knows open at their inbox. A server Karpo knows no web mail
// for gives no link, and its rows open nothing.
//
// Only an https address is ever handed on: what opens it is the shell, and an account's answer is not
// trusted to name what the shell runs.

namespace Karpo.Accounts;

public static class MailLinks
{
    /// <summary>The web mail of the IMAP servers Karpo knows, by host: where their inbox opens.</summary>
    private static readonly Dictionary<string, string> Inboxes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["outlook.office365.com"] = "https://outlook.office.com/mail/",
        ["imap-mail.outlook.com"] = "https://outlook.live.com/mail/",
        ["imap.mail.yahoo.com"] = "https://mail.yahoo.com/",
        ["imap.mail.me.com"] = "https://www.icloud.com/mail/",
        ["imap.aol.com"] = "https://mail.aol.com/",
    };

    /// <summary>The hosts of Gmail's IMAP.</summary>
    private static bool Gmail(string host) =>
        host.Equals("imap.gmail.com", StringComparison.OrdinalIgnoreCase) || host.Equals("imap.googlemail.com", StringComparison.OrdinalIgnoreCase);

    /// <summary><paramref name="link"/> where it is an absolute https address; else null.</summary>
    public static string? Web(string? link) =>
        Uri.TryCreate(link?.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? link!.Trim() : null;

    /// <summary>
    /// Where a message of the IMAP account on <paramref name="host"/> opens: on Gmail the message, found
    /// by its <paramref name="messageId"/> in <paramref name="address"/>'s mailbox (its inbox without
    /// one); on another server Karpo knows, its inbox; null on any other.
    /// </summary>
    public static string? Imap(string? host, string? address, string? messageId)
    {
        host = host?.Trim().TrimEnd('.');
        if (string.IsNullOrEmpty(host))
        {
            return null;
        }
        return Gmail(host) ? Google(address, messageId) : Inboxes.GetValueOrDefault(host);
    }

    /// <summary>
    /// Where a message of <paramref name="address"/>'s Gmail opens: the message, found by its <paramref name="messageId"/>;
    /// the inbox without one. A Google account's (Google\GoogleMail.cs) and Gmail's IMAP server's.
    /// </summary>
    public static string Google(string? address, string? messageId)
    {
        // u/<address> picks the signed-in account the message is in, where several are.
        var mailbox = $"https://mail.google.com/mail/u/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(address) ? "0" : address.Trim())}/";
        var id = messageId?.Trim().Trim('<', '>');
        return string.IsNullOrEmpty(id) ? mailbox + "#inbox" : mailbox + "#search/rfc822msgid%3A" + Uri.EscapeDataString(id);
    }
}
