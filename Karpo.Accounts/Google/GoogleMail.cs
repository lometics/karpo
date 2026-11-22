// A Google account's mail through the Gmail API (docs\accounts.md, "Google"; docs\widgets.md §639).
// Gmail has labels where other mail has folders, and a message may wear several: each label is a folder
// here. The inbox first, then Gmail's own (Sent, Drafts, Spam, Trash, Starred, Important) by the names
// Karpo gives them, then the person's labels by name — a label in a label ("Work/Clients") named as a
// folder in a folder is, "Work / Clients". Gmail's categories (CATEGORY_*), UNREAD and CHAT are no
// folders. The inbox is synced unless turned off, a label only where turned on (Account.Syncs).
//
// Of a label synced: its counts (labels.get: messagesTotal, messagesUnread), its newest messages
// (messages.list, newest first), and each of those by its headers alone (format=metadata: From,
// Subject, Message-ID), six at a time. A message is unread while it wears UNREAD. The inbox failing
// fails the read; another label fails alone.

using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;

namespace Karpo.Accounts.Google;

internal sealed partial class GoogleReader
{
    /// <summary>The id of Gmail's inbox.</summary>
    internal const string InboxLabel = "INBOX";

    /// <summary>The most labels listed (§455, §639): a mailbox of thousands lists its first.</summary>
    internal const int MostFolders = 500;

    /// <summary>How many of a label's messages are asked for at once.</summary>
    private const int AtOnce = 6;

    /// <summary>What Gmail is asked for of each message: all <see cref="Message"/> reads.</summary>
    private const string MessageFields = "format=metadata&metadataHeaders=From&metadataHeaders=Subject&metadataHeaders=Message-ID&fields=id,labelIds,snippet,internalDate,payload/headers";

    public async Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation)
    {
        var labels = await gmail.GetAsync($"{GoogleApi.Gmail}/labels", cancellation).ConfigureAwait(false);
        var found = new List<FolderRead>();
        foreach (var folder in Folders(Web.Items(labels, "labels")))
        {
            if (!Account.Syncs(folder.Inbox ? SyncKeys.Inbox : SyncKeys.MailFolder(folder.Id)))
            {
                found.Add(new FolderRead(folder, null));
                continue;
            }
            if (folder.Inbox)
            {
                // The inbox failing fails the read.
                found.Add(await LabelAsync(folder, count, cancellation).ConfigureAwait(false));
                continue;
            }
            try
            {
                found.Add(await LabelAsync(folder, count, cancellation).ConfigureAwait(false));
            }
            catch (AccountException refused)
            {
                // A label Gmail will not give fails alone, as a calendar does (Parts).
                found.Add(new FolderRead(folder, null) { Failed = refused.Message });
            }
        }
        return found;
    }

    /// <summary>One label's counts and its newest <paramref name="count"/> messages, newest first.</summary>
    private async Task<FolderRead> LabelAsync(MailFolder folder, int count, CancellationToken cancellation)
    {
        var label = await gmail.GetAsync($"{GoogleApi.Gmail}/labels/{Uri.EscapeDataString(folder.Id)}", cancellation).ConfigureAwait(false);
        var listed = await gmail.GetAsync(
            $"{GoogleApi.Gmail}/messages?labelIds={Uri.EscapeDataString(folder.Id)}&maxResults={count.ToString(CultureInfo.InvariantCulture)}",
            cancellation).ConfigureAwait(false);
        var ids = Web.Items(listed, "messages").Select(message => Web.Str(message["id"])).OfType<string>().Take(count).ToList();
        var messages = new MailItem?[ids.Count];
        await Parallel.ForEachAsync(
            Enumerable.Range(0, ids.Count),
            new ParallelOptions { MaxDegreeOfParallelism = AtOnce, CancellationToken = cancellation },
            async (at, each) =>
            {
                try
                {
                    messages[at] = Message(await gmail.GetAsync($"{GoogleApi.Gmail}/messages/{Uri.EscapeDataString(ids[at])}?{MessageFields}", each).ConfigureAwait(false), Account.Address);
                }
                catch (AccountException gone) when (gone.Status == 404)
                {
                    // Deleted since it was listed: it is not there to show.
                }
            }).ConfigureAwait(false);
        var unread = Web.Int(label["messagesUnread"]);
        var total = Web.Int(label["messagesTotal"]);
        return new FolderRead(
            folder with { Unread = unread, Total = total },
            new Inbox(unread ?? 0, [.. messages.OfType<MailItem>().OrderByDescending(message => message.Received)], total));
    }

    /// <summary>Gmail's own labels that are folders, in their order, by the names Karpo gives them.</summary>
    private static IEnumerable<(string Id, string Name)> Own() =>
    [
        ("SENT", S.CoreAccountsGmailSent),
        ("DRAFT", S.CoreAccountsGmailDrafts),
        ("SPAM", S.CoreAccountsGmailSpam),
        ("TRASH", S.CoreAccountsGmailTrash),
        ("STARRED", S.CoreAccountsGmailStarred),
        ("IMPORTANT", S.CoreAccountsGmailImportant),
    ];

    /// <summary>
    /// The folders of <paramref name="labels"/> (Gmail's labels.list) in the order a row lists them: the inbox, Gmail's own
    /// that are folders, the person's by name. No category, UNREAD or CHAT; at most <see cref="MostFolders"/>.
    /// </summary>
    internal static IReadOnlyList<MailFolder> Folders(IReadOnlyList<JsonObject> labels)
    {
        var listed = labels.Select(label => Web.Str(label["id"])).OfType<string>().ToHashSet(StringComparer.Ordinal);
        List<MailFolder> folders = [new MailFolder(InboxLabel, S.CoreAccountsInbox, Inbox: true)];
        folders.AddRange(Own().Where(own => listed.Contains(own.Id)).Select(own => new MailFolder(own.Id, own.Name)));
        folders.AddRange(labels
            .Where(label => Web.Str(label["type"]) == "user" && Web.Str(label["id"]) is { Length: > 0 })
            .Select(label => new MailFolder(Web.Str(label["id"])!, Nested(Web.Str(label["name"]) is { Length: > 0 } name ? name : Web.Str(label["id"])!)))
            .OrderBy(folder => folder.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(MostFolders - folders.Count));
        return folders;
    }

    /// <summary>A label's name as a folder's: a label in a label ("Work/Clients") after its parent's, "Work / Clients".</summary>
    private static string Nested(string name) => string.Join(" / ", name.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

    /// <summary>One message as Gmail gives its headers; null for one without an id.</summary>
    /// <param name="address">The account's, which picks its mailbox in Gmail's web page where several are signed in.</param>
    internal static MailItem? Message(JsonObject item, string address)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var headers = Web.Items(item["payload"], "headers");
        string? Header(string name) => headers.Where(header => string.Equals(Web.Str(header["name"]), name, StringComparison.OrdinalIgnoreCase))
            .Select(header => Web.Str(header["value"])).FirstOrDefault();

        var from = MimeKit.InternetAddressList.TryParse(Header("From") ?? "", out var senders) ? senders.Mailboxes.FirstOrDefault() : null;
        var subject = Header("Subject")?.Trim();
        if (subject?.Contains("=?", StringComparison.Ordinal) == true)
        {
            // Words a sender's mail program left encoded (RFC 2047), which Gmail passes on as they came.
            subject = MimeKit.Utils.Rfc2047.DecodeText(System.Text.Encoding.UTF8.GetBytes(subject));
        }
        // The snippet is HTML's text: "&#39;" for an apostrophe.
        var preview = WebUtility.HtmlDecode(Web.Str(item["snippet"]))?.Trim();
        var received = long.TryParse(Web.Str(item["internalDate"]), NumberStyles.None, CultureInfo.InvariantCulture, out var milliseconds)
            ? DateTimeOffset.FromUnixTimeMilliseconds(milliseconds)
            : DateTimeOffset.MinValue;
        var labels = item["labelIds"] as JsonArray ?? [];
        return new MailItem(
            id,
            string.IsNullOrWhiteSpace(from?.Name) ? from?.Address ?? S.CoreAccountsUnknownSender : from.Name,
            from?.Address,
            string.IsNullOrWhiteSpace(subject) ? S.CoreAccountsNoSubject : subject,
            string.IsNullOrEmpty(preview) ? null : preview,
            received,
            labels.Any(label => Web.Str(label) == "UNREAD"),
            MailLinks.Google(address, Header("Message-ID")));
    }
}
