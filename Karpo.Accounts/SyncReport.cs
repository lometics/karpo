// What the dock last synced of each account, for the settings app's Accounts
// page: per account and per kind (calendars, contacts, mail), when it was read,
// what went wrong, and each calendar, address book or inbox with how many it
// holds — or that the person turned it off.
//
// The dock writes it, the settings app reads it; nothing else does. The stores
// already hold all of it in memory after a read, so writing it is a few hundred
// bytes, and reading it costs the page no account and no network: the counts
// come from the local store, never from asking the server again. Each store puts
// its own kind (Put), the others' left as they are; a write that would say what
// the file says already is not made, so the page's watcher hears only news.
//
// In the local folder beside accounts.json, encrypted for this Windows user as
// the caches are (docs\accounts.md, "Offline"): it names calendars and books.
// A file that does not read is no report; the next sync writes it again.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Karpo.Core;

namespace Karpo.Accounts;

public sealed class SyncReport(string folder)
{
    /// <summary>The file's name in the local folder.</summary>
    public const string FileName = "sync-report.json";

    /// <summary>The report's own, so no other DPAPI blob of this user reads as one.</summary>
    private static readonly byte[] Entropy = "Karpo.SyncReport.v1"u8.ToArray();

    private static readonly Lock Writing = new();

    /// <summary>The person's: the local folder, which an isolated run has its own of.</summary>
    public static SyncReport Here => new(Config.LocalFolder);

    /// <summary>The dock's: none on an isolated run, which reads no account.</summary>
    public static SyncReport? ForDock() => Config.IsIsolated ? null : Here;

    public string Folder { get; } = folder;

    public string FilePath => Path.Combine(Folder, FileName);

    /// <summary>What the file says; empty where there is none or it does not read.</summary>
    public SyncReportFile Read()
    {
        byte[]? json = null;
        try
        {
            if (!File.Exists(FilePath))
            {
                return new SyncReportFile();
            }
            json = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize(json, SyncReportJson.Default.SyncReportFile) ?? new SyncReportFile();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException or JsonException or NotSupportedException)
        {
            // The kind of trouble only: a JSON error's message would quote what the file holds.
            Console.Error.WriteLine($"Karpo: {FileName} not read ({error.GetType().Name})");
            return new SyncReportFile();
        }
        finally
        {
            if (json is not null)
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
    }

    /// <summary>
    /// <paramref name="kind"/> of every account is now <paramref name="parts"/>: an account named
    /// there gets its part, every other one loses its part of that kind, and an account left with no
    /// part at all goes. The other kinds stay as they were.
    /// </summary>
    public void Put(AccountData kind, IReadOnlyList<(string Account, SyncPart Part)> parts)
    {
        Store(kind, parts);
        Told?.Invoke(this);
    }

    /// <summary>
    /// A store said what it last synced (<see cref="Put"/>), raised on the store's thread once the file says it:
    /// the dock's errors read the report again (docs\widgets.md §394).
    /// </summary>
    public static event Action<SyncReport>? Told;

    private void Store(AccountData kind, IReadOnlyList<(string Account, SyncPart Part)> parts)
    {
        lock (Writing)
        {
            var file = Read();
            var before = JsonSerializer.SerializeToUtf8Bytes(file, SyncReportJson.Default.SyncReportFile);
            foreach (var account in file.Accounts)
            {
                account.Set(kind, null);
            }
            foreach (var (id, part) in parts)
            {
                var account = file.Accounts.FirstOrDefault(one => one.Account == id);
                if (account is null)
                {
                    file.Accounts.Add(account = new SyncReportAccount { Account = id });
                }
                account.Set(kind, part);
            }
            file.Accounts.RemoveAll(account => account is { Calendars: null, Contacts: null, Mail: null, Tasks: null });
            var after = JsonSerializer.SerializeToUtf8Bytes(file, SyncReportJson.Default.SyncReportFile);
            try
            {
                if (!after.AsSpan().SequenceEqual(before))
                {
                    Write(after);
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(before);
                CryptographicOperations.ZeroMemory(after);
            }
        }
    }

    /// <summary>Encrypted to a temporary file moved over the old one, so a reader never meets half a file.</summary>
    private void Write(byte[] json)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            var temporary = FilePath + ".tmp";
            File.WriteAllBytes(temporary, ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser));
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Console.Error.WriteLine($"Karpo: {FileName} not saved ({error.GetType().Name}: {error.HResult:X8})");
        }
    }
}

/// <summary>sync-report.json.</summary>
public sealed class SyncReportFile
{
    public List<SyncReportAccount> Accounts { get; set; } = [];

    /// <summary>The account of that id; null where the dock has said nothing of it yet.</summary>
    public SyncReportAccount? For(string id) => Accounts.FirstOrDefault(account => account.Account == id);
}

/// <summary>One account: each kind it gives, as last synced; null for a kind not synced (yet).</summary>
public sealed class SyncReportAccount
{
    /// <summary>Its id (<see cref="Account.Id"/>).</summary>
    public string Account { get; set; } = "";

    public SyncPart? Calendars { get; set; }

    public SyncPart? Contacts { get; set; }

    public SyncPart? Mail { get; set; }

    /// <summary>To Do's task lists; absent from a report written before Karpo read them.</summary>
    public SyncPart? Tasks { get; set; }

    public SyncPart? Of(AccountData kind) => kind switch
    {
        AccountData.Calendars => Calendars,
        AccountData.Contacts => Contacts,
        AccountData.Mail => Mail,
        AccountData.Tasks => Tasks,
        _ => null,
    };

    internal void Set(AccountData kind, SyncPart? part)
    {
        switch (kind)
        {
            case AccountData.Calendars:
                Calendars = part;
                break;
            case AccountData.Contacts:
                Contacts = part;
                break;
            case AccountData.Mail:
                Mail = part;
                break;
            case AccountData.Tasks:
                Tasks = part;
                break;
        }
    }
}

/// <summary>One kind of one account, as its store last had it.</summary>
public sealed class SyncPart
{
    /// <summary>When it was last read well; null before it was.</summary>
    public DateTime? UpdatedUtc { get; set; }

    /// <summary>Why the last try failed, in words for the person; null when it went well.</summary>
    public string? Problem { get; set; }

    /// <summary>
    /// The account signs in but the person never let Karpo at this kind — To Do on a Microsoft account added before
    /// Karpo asked for it, a kind unticked on Google's consent screen —, and it needs signing in to again
    /// (ConsentException); null otherwise.
    /// </summary>
    public bool? Consent { get; set; }

    /// <summary>The span the calendars' counts are of (UTC); null for the other kinds.</summary>
    public DateTime? FromUtc { get; set; }

    public DateTime? ToUtc { get; set; }

    /// <summary>Each calendar, address book or inbox, in the account's order.</summary>
    public List<SyncItem> Items { get; set; } = [];

    /// <summary>
    /// §639: the mail's folders but the inbox, in the account's order, each a part the Accounts page turns on (off until
    /// it is) and a mail widget may then show from (WidgetSources): with its counts where it was read, or the account
    /// gave them with its list; <see cref="SyncItem.Off"/> where it is not synced. Null for the other kinds.
    /// </summary>
    public List<SyncItem>? Folders { get; set; }
}

/// <summary>One calendar, address book or inbox.</summary>
public sealed class SyncItem
{
    /// <summary>Its id within the account: what <see cref="SyncKeys"/> names it by.</summary>
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>A calendar's colour, <c>#RRGGBB</c>, where the account gives one.</summary>
    public string? Colour { get; set; }

    /// <summary>
    /// How many it holds: the events in the span, the people, the messages; null where it is off or
    /// the account did not say.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>An inbox's unread messages.</summary>
    public int? Unread { get; set; }

    /// <summary>The person turned it off: nothing of it is fetched.</summary>
    public bool Off { get; set; }

    /// <summary>A task list that is the account's own "Tasks" (RemoteTaskList.Default); null for every other part.</summary>
    public bool? Default { get; set; }

    /// <summary>
    /// §399: why this part alone was not read — a To Do list To Do refused, the account's other lists read; a
    /// calendar or an address book the account would not give, shown as it was before —, or why it is not all
    /// there (the server gave only part of it), in words for the person; null where it was read, or it is off.
    /// </summary>
    public string? Problem { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SyncReportFile))]
internal sealed partial class SyncReportJson : JsonSerializerContext;
