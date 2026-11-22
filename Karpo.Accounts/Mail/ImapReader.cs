// A mail account over IMAP, through MailKit: the inbox's unread count and its
// newest messages, and so of each other folder the person turned on (§639),
// read-only. It never marks anything read, moves anything or keeps a connection
// open between reads — each read connects, asks and leaves.

using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;

namespace Karpo.Accounts.Mail;

internal sealed class ImapReader(Account account) : IAccountReader
{
    public Account Account { get; } = account;

    public Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation) =>
        Task.FromResult<IReadOnlyList<CalendarRead>>([]);

    public Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation) =>
        Task.FromResult<IReadOnlyList<BookRead>>([]);

    /// <summary>The most mail folders listed (§455): a server of thousands lists its first.</summary>
    private const int MostFolders = 500;

    public async Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation)
    {
        var password = AccountBook.Secret(Account)
            ?? throw new SignInRequiredException(S.CoreAccountsNoPassword(Account.Address));
        using var client = await Imap.ConnectAsync(
            Account.Host ?? "", Account.Port, Account.Security, Account.User ?? Account.Address, password, cancellation)
            .ConfigureAwait(false);

        var inbox = client.Inbox;
        List<FolderRead> found =
        [
            new(new MailFolder(inbox.FullName, S.CoreAccountsInbox, Inbox: true),
                Account.Syncs(SyncKeys.Inbox) ? await ReadAsync(inbox, count, cancellation).ConfigureAwait(false) : null),
        ];
        // §639: every other folder that holds mail, in one LIST with their special uses (RFC 6154) — and their counts
        // where the server gives them with it (LIST-STATUS, RFC 5819; never a STATUS each) —, read where the person
        // turned it on on the Accounts page.
        var counted = client.Capabilities.HasFlag(ImapCapabilities.ListStatus) ? StatusItems.Count | StatusItems.Unread : StatusItems.None;
        IList<IMailFolder> listed = client.PersonalNamespaces.Count == 0 ? []
            : await client.GetFoldersAsync(client.PersonalNamespaces[0], counted, subscribedOnly: false, cancellation).ConfigureAwait(false);
        var byName = listed.GroupBy(each => each.FullName, StringComparer.Ordinal).ToDictionary(each => each.Key, each => each.First(), StringComparer.Ordinal);
        var folders = Listed(
            listed.Select(each => new ListedFolder(each.FullName, each.DirectorySeparator, each.Attributes,
                counted == StatusItems.None ? null : each.Unread, counted == StatusItems.None ? null : each.Count)),
            inbox.FullName);
        foreach (var folder in folders)
        {
            if (!Account.Syncs(SyncKeys.MailFolder(folder.Id)))
            {
                found.Add(new FolderRead(folder, null));
                continue;
            }
            try
            {
                found.Add(new FolderRead(folder, await ReadAsync(byName[folder.Id], count, cancellation).ConfigureAwait(false)));
            }
            catch (Exception refused) when (refused is ImapCommandException or FolderNotFoundException)
            {
                // A folder the server will not open fails alone, as a calendar does (Parts).
                found.Add(new FolderRead(folder, null) { Failed = refused.Message });
            }
        }
        await client.DisconnectAsync(true, cancellation).ConfigureAwait(false);
        return found;
    }

    /// <summary>One folder as a LIST answers it: its full name, its separator, its attributes and, with LIST-STATUS, its counts.</summary>
    internal readonly record struct ListedFolder(string FullName, char Separator, FolderAttributes Attributes, int? Unread = null, int? Total = null);

    /// <summary>
    /// §639: the folders a LIST found as the Accounts page lists them, the inbox not among them: none that holds no mail
    /// (\Noselect, \NonExistent); those of a special use (RFC 6154) first — the drafts, the sent, the archive, the junk,
    /// the trash —, each by its own name, not its container's ("Sent Mail", not "[Gmail] / Sent Mail"); then the rest
    /// in the server's order, a folder in a folder after its parent's name; the virtual ones last (\All, \Flagged: every
    /// message of the others again). At most <see cref="MostFolders"/>.
    /// </summary>
    internal static IReadOnlyList<MailFolder> Listed(IEnumerable<ListedFolder> listed, string inbox) =>
        [.. listed
            .Where(each => !each.Attributes.HasFlag(FolderAttributes.NoSelect) && !each.Attributes.HasFlag(FolderAttributes.NonExistent)
                && !string.Equals(each.FullName, inbox, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(each => each.FullName, StringComparer.Ordinal)
            .Take(MostFolders)
            .Select((each, at) => (Each: each, At: at, Use: Use(each.Attributes)))
            .OrderBy(each => each.Use)
            .ThenBy(each => each.At)
            .Select(each => new MailFolder(each.Each.FullName, Name(each.Each, inbox, each.Use != Plain), Unread: each.Each.Unread, Total: each.Each.Total))];

    /// <summary>Where a folder of none of the uses stands among them (<see cref="Use"/>).</summary>
    private const int Plain = 5;

    /// <summary>A folder's place by its special use: the drafts, the sent, the archive, the junk, the trash, then <see cref="Plain"/>, then the virtual ones.</summary>
    private static int Use(FolderAttributes attributes) =>
        attributes.HasFlag(FolderAttributes.Drafts) ? 0
        : attributes.HasFlag(FolderAttributes.Sent) ? 1
        : attributes.HasFlag(FolderAttributes.Archive) ? 2
        : attributes.HasFlag(FolderAttributes.Junk) ? 3
        : attributes.HasFlag(FolderAttributes.Trash) ? 4
        : attributes.HasFlag(FolderAttributes.All) || attributes.HasFlag(FolderAttributes.Flagged) ? Plain + 1
        : Plain;

    /// <summary>
    /// A folder's name as a row shows it: its path, its parents' names before it, the inbox's as Karpo calls it; one of a
    /// special use (<paramref name="own"/>) by its own name alone.
    /// </summary>
    private static string Name(ListedFolder folder, string inbox, bool own)
    {
        var parts = folder.Separator == '\0' ? [folder.FullName] : folder.FullName.Split(folder.Separator);
        if (own)
        {
            return parts[^1];
        }
        if (parts.Length > 1 && string.Equals(parts[0], inbox, StringComparison.OrdinalIgnoreCase))
        {
            parts[0] = S.CoreAccountsInbox;
        }
        return string.Join(" / ", parts);
    }

    /// <summary>A folder's unread count, how many it holds, and its newest <paramref name="count"/> messages, read-only.</summary>
    private async Task<Inbox> ReadAsync(IMailFolder folder, int count, CancellationToken cancellation)
    {
        await folder.StatusAsync(StatusItems.Unread, cancellation).ConfigureAwait(false);
        var unread = folder.Unread;
        await folder.OpenAsync(FolderAccess.ReadOnly, cancellation).ConfigureAwait(false);
        var total = folder.Count;

        var messages = new List<MailItem>();
        if (folder.Count > 0)
        {
            var first = Math.Max(0, folder.Count - count);
            var found = await folder.FetchAsync(
                first,
                -1,
                MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.Flags
                    | MessageSummaryItems.InternalDate | MessageSummaryItems.PreviewText,
                cancellation).ConfigureAwait(false);
            messages.AddRange(found.Select(summary => Message(summary, Account)).OrderByDescending(message => message.Received));
        }
        return new Inbox(unread, messages, total);
    }

    private static MailItem Message(IMessageSummary summary, Account account)
    {
        var from = summary.Envelope?.From?.Mailboxes.FirstOrDefault();
        var subject = summary.Envelope?.Subject;
        var preview = summary.PreviewText?.Trim();
        return new MailItem(
            summary.UniqueId.ToString(),
            string.IsNullOrWhiteSpace(from?.Name) ? from?.Address ?? S.CoreAccountsUnknownSender : from.Name,
            from?.Address,
            string.IsNullOrWhiteSpace(subject) ? S.CoreAccountsNoSubject : subject,
            string.IsNullOrEmpty(preview) ? null : preview,
            summary.InternalDate ?? summary.Envelope?.Date ?? DateTimeOffset.MinValue,
            summary.Flags is { } flags && !flags.HasFlag(MessageFlags.Seen),
            MailLinks.Imap(account.Host, account.Address, summary.Envelope?.MessageId));
    }
}

/// <summary>Connecting and signing in, shared by the reader and the settings app's check.</summary>
public static class Imap
{
    /// <summary>How long a connection may take to answer anything.</summary>
    private const int Patience = 20_000;

    /// <summary>A connected, signed-in client. A refused password is a <see cref="SignInRequiredException"/>.</summary>
    public static async Task<ImapClient> ConnectAsync(
        string host, int port, MailSecurity security, string user, string password, CancellationToken cancellation)
    {
        var client = new ImapClient { Timeout = Patience };
        try
        {
            await client.ConnectAsync(host, port, security switch
            {
                MailSecurity.Implicit => SecureSocketOptions.SslOnConnect,
                MailSecurity.StartTls => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.None,
            }, cancellation).ConfigureAwait(false);
            await client.AuthenticateAsync(user, password, cancellation).ConfigureAwait(false);
            return client;
        }
        catch (AuthenticationException refused)
        {
            client.Dispose();
            throw new SignInRequiredException($"{host} refused that user name and password.", refused);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }
}
