// A CalDAV calendar kept whole on disk (docs\widgets.md §616), so a start shows
// every event of it, any year, with no connection and no download again:
//
//   %LOCALAPPDATA%\Karpo\calendar-mirror\<account id>\<hash of the calendar's id>.bin
//
// One file per calendar (Config.LocalFolder; an isolated run keeps it in memory
// only, PimCache.ForDock). What it holds: a header — the version of what is
// kept (KeptICalendar.Version), the calendar's id, name and colour, its place in
// its account's listing, when it was last synced, its sync token and tag,
// whether the server has no sync-collection, whether it is whole, when it was
// last compared whole — and its objects, each its href, its etag and its
// iCalendar as kept (KeptICalendar: no notes, no attendees, no alarms). The
// header is JSON, the objects JSON deflated (ZLibStream, fastest), the whole
// DPAPI-encrypted for this Windows user (entropy Karpo.CalendarMirror.v1) and
// written the durable way (DurableFile: through to the disk, swapped in, the
// version before as .bak). The token and the objects are one file, so a crash
// never leaves a token that says more than the objects hold.
//
// In memory the store holds each calendar as a MirroredCalendar: the header,
// the objects still deflated (Packed: what the file holds of them, and all the
// text there is), their etags, and the parsed index the views draw from. A sync
// that changed something inflates the objects, puts the changes in and deflates
// them again (Apply); one that changed nothing writes the header anew around the
// same deflated objects.
//
// A file that does not decrypt or parse is its .bak where that reads, else
// deleted: the calendar is downloaded again whole. A file of another version is
// deleted too. A file another program holds is not read and not written over;
// the store keeps what it has to write until a write goes through. Nothing in
// a file — a title, an href, a token, an etag — is ever logged; only the kind
// of trouble.

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Karpo.Accounts;
using Karpo.Accounts.Dav;
using Karpo.Core;

namespace Karpo.Dock.Pim;

/// <summary>One calendar kept whole: its header, its objects deflated, their etags, and the index the views draw from.</summary>
internal sealed record MirroredCalendar(MirrorHeader Header, byte[] Packed, IReadOnlyDictionary<string, string> Etags, EventIndex Index)
{
    /// <summary>Objects whose kept text did not parse: counted, so the calendar says not all of it is shown.</summary>
    public IReadOnlySet<string> Broken { get; init; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The last sync said not all of it came (CalendarChanges.Incomplete), or a file kept so: shown as not all there.</summary>
    public bool Short { get; init; }

    public RemoteCalendar Calendar => Header.Calendar.Remote;

    /// <summary>What the next sync is handed (ICalendarSync).</summary>
    public CalendarState State => new()
    {
        Token = Header.Token,
        Tag = Header.Tag,
        NoSyncReport = Header.NoSyncReport,
        Complete = Header.Complete,
        ReconciledUtc = Header.ReconciledUtc,
        Etags = Etags,
    };
}

internal sealed class CalendarMirror(string? folder)
{
    internal const string FolderName = "calendar-mirror";

    /// <summary>The mirror's own, so no other DPAPI blob of this user decrypts as one.</summary>
    private static readonly byte[] Entropy = "Karpo.CalendarMirror.v1"u8.ToArray();

    /// <summary>One read, write or deletion at a time, the store's lock never held meanwhile.</summary>
    private readonly SemaphoreSlim io = new(1, 1);

    /// <summary>Under <see cref="io"/>: the files that did not read, of which the next write keeps no backup.</summary>
    private readonly HashSet<string> unread = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The dock's: in the local folder. An isolated run keeps its calendars in memory only.</summary>
    internal static CalendarMirror ForDock() => new(Karpo.Core.Config.IsIsolated ? null : Path.Combine(Karpo.Core.Config.LocalFolder, FolderName));

    /// <summary>Where the files are; null where nothing is written.</summary>
    internal string? Folder { get; } = folder;

    /// <summary>
    /// <paramref name="before"/> (null: nothing yet) with <paramref name="changes"/> put in: their objects in, by href, and
    /// out, parsed; the header the changes leave. A <paramref name="final"/> answer may make it whole; a step of a
    /// first download (a checkpoint) never does. Pure: nothing is written.
    /// </summary>
    internal static MirroredCalendar Apply(MirroredCalendar? before, CalendarChanges changes, int order, DateTime utcNow, bool final)
    {
        var items = before is null ? new Dictionary<string, MirrorItem>(StringComparer.Ordinal) : Unpack(before.Packed);
        var broken = new HashSet<string>(before?.Broken ?? new HashSet<string>(), StringComparer.Ordinal);
        var gone = new List<string>();
        foreach (var href in changes.Removed)
        {
            items.Remove(href);
            broken.Remove(href);
            gone.Add(href);
        }
        var parsed = new List<(string, ICalendar)>();
        foreach (var one in changes.Changed)
        {
            items[one.Href] = new MirrorItem { Href = one.Href, Etag = one.Etag, Ics = one.Text };
            broken.Remove(one.Href);
            if (Parse(one.Text) is { } calendar)
            {
                parsed.Add((one.Href, calendar));
            }
            else
            {
                gone.Add(one.Href);
                if (one.Text is not null)
                {
                    broken.Add(one.Href);
                }
            }
        }
        var header = new MirrorHeader
        {
            Version = KeptICalendar.Version,
            Calendar = MirrorCalendarName.Of(changes.Calendar),
            Order = order,
            UpdatedUtc = utcNow,
            Token = changes.Token,
            Tag = final ? changes.Tag : null,
            NoSyncReport = changes.NoSyncReport,
            Complete = final && !changes.Incomplete,
            ReconciledUtc = changes.ReconciledUtc,
        };
        return new MirroredCalendar(header, Pack(items.Values), Etags(items.Values), (before?.Index ?? EventIndex.Empty).With(parsed, gone))
        {
            Broken = broken,
            Short = final ? changes.Incomplete : before?.Short ?? false,
        };
    }

    /// <summary><paramref name="calendar"/> with a header of now: synced and nothing changed, its objects as they were.</summary>
    internal static MirroredCalendar Touched(MirroredCalendar calendar, RemoteCalendar remote, int order, DateTime utcNow) =>
        calendar with { Header = calendar.Header with { Calendar = MirrorCalendarName.Of(remote), Order = order, UpdatedUtc = utcNow } };

    /// <summary>
    /// Every calendar kept of <paramref name="account"/>, by its place in the account's listing. A file of another version,
    /// or one that reads as neither it nor its .bak, is deleted (downloaded again whole); one another program holds is
    /// left out and left alone.
    /// </summary>
    internal async Task<List<MirroredCalendar>> ReadAsync(string account, CancellationToken cancellation)
    {
        var found = new List<MirroredCalendar>();
        if (Folder is null || AccountFolder(account) is not { } where)
        {
            return found;
        }
        await io.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            if (!Directory.Exists(where))
            {
                return found;
            }
            foreach (var path in Directory.EnumerateFiles(where, "*.bin"))
            {
                switch (DurableFile.ReadBytes(path, out var kept))
                {
                    case FileRead.Missing:
                        continue;
                    case FileRead.Locked:
                        Console.Error.WriteLine("Karpo: a calendar mirror is held by another program; not read");
                        continue;
                }
                var calendar = Open(kept!);
                if (calendar is null)
                {
                    // The version before, which always read; the next write keeps no backup of the file that did not.
                    unread.Add(path);
                    if (DurableFile.ReadBytes(DurableFile.BackupOf(path), out var before) == FileRead.Ok)
                    {
                        calendar = Open(before!);
                    }
                }
                if (calendar is not null)
                {
                    found.Add(calendar);
                    continue;
                }
                Delete(path);
            }
        }
        finally
        {
            io.Release();
        }
        found.Sort((a, b) => a.Header.Order.CompareTo(b.Header.Order));
        return found;
    }

    /// <summary>
    /// <paramref name="calendar"/> written as its file. False where it could not be (another program holds the file, the
    /// disk is full): the store keeps it to write again.
    /// </summary>
    internal async Task<bool> WriteAsync(string account, MirroredCalendar calendar, CancellationToken cancellation)
    {
        if (Folder is null)
        {
            return true;
        }
        if (AccountFolder(account) is not { } where)
        {
            return false;
        }
        var plain = Plain(calendar);
        await io.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            var path = Path.Combine(where, FileName(calendar.Calendar.Id));
            DurableFile.Write(path, ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser), keepBackup: !unread.Contains(path));
            unread.Remove(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
        {
            Console.Error.WriteLine($"Karpo: a calendar mirror not saved ({error.GetType().Name}: {error.HResult:X8})");
            return false;
        }
        finally
        {
            io.Release();
            CryptographicOperations.ZeroMemory(plain);
        }
    }

    /// <summary>The file of one calendar (turned off, or no longer listed), and its .bak.</summary>
    internal async Task DeleteAsync(string account, string calendar, CancellationToken cancellation)
    {
        if (Folder is null || AccountFolder(account) is not { } where)
        {
            return;
        }
        await io.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            Delete(Path.Combine(where, FileName(calendar)));
        }
        finally
        {
            io.Release();
        }
    }

    /// <summary>
    /// Every account's folder but <paramref name="keep"/>'s: an account removed, now or while the dock was not running.
    /// Called with the accounts known, never while accounts.json could not be read.
    /// </summary>
    internal async Task SweepAsync(IReadOnlySet<string> keep, CancellationToken cancellation)
    {
        if (Folder is null || !Directory.Exists(Folder))
        {
            return;
        }
        await io.WaitAsync(cancellation).ConfigureAwait(false);
        try
        {
            foreach (var where in Directory.EnumerateDirectories(Folder))
            {
                if (!keep.Contains(Path.GetFileName(where)))
                {
                    try
                    {
                        Directory.Delete(where, recursive: true);
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Karpo: a removed account's calendar mirror not deleted ({error.GetType().Name})");
                    }
                }
            }
        }
        finally
        {
            io.Release();
        }
    }

    /// <summary>The file a calendar is kept in: the first 16 hex digits of its id's SHA-256, so no id is in a name.</summary>
    internal static string FileName(string calendar) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(calendar)))[..16] + ".bin";

    /// <summary>An account's folder: its id, which is Karpo's own and random; null for one that is no plain name.</summary>
    private string? AccountFolder(string account) =>
        account.Length is > 0 and <= 64 && account.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? Path.Combine(Folder!, account) : null;

    private static void Delete(string path)
    {
        foreach (var one in (string[])[path, DurableFile.BackupOf(path)])
        {
            try
            {
                File.Delete(one);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Karpo: a calendar mirror not deleted ({error.GetType().Name})");
            }
        }
    }

    /// <summary>What a file holds, decrypted: the header's length, the header, the objects deflated.</summary>
    internal static byte[] Plain(MirroredCalendar calendar)
    {
        var header = JsonSerializer.SerializeToUtf8Bytes(calendar.Header, MirrorJson.Default.MirrorHeader);
        var plain = new byte[4 + header.Length + calendar.Packed.Length];
        BitConverter.TryWriteBytes(plain.AsSpan(0, 4), header.Length);
        header.CopyTo(plain, 4);
        calendar.Packed.CopyTo(plain, 4 + header.Length);
        return plain;
    }

    /// <summary>The calendar a file holds, parsed; null where it is not this user's, not a mirror, or of another version.</summary>
    internal static MirroredCalendar? Open(byte[] kept)
    {
        byte[]? plain = null;
        try
        {
            plain = ProtectedData.Unprotect(kept, Entropy, DataProtectionScope.CurrentUser);
            var length = BitConverter.ToInt32(plain, 0);
            if (length <= 0 || 4 + length > plain.Length)
            {
                return null;
            }
            if (JsonSerializer.Deserialize(plain.AsSpan(4, length), MirrorJson.Default.MirrorHeader) is not { Version: KeptICalendar.Version } header)
            {
                return null;
            }
            var packed = plain[(4 + length)..];
            var items = Unpack(packed);
            var parsed = new List<(string, ICalendar)>(items.Count);
            var broken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items.Values)
            {
                if (Parse(item.Ics) is { } calendar)
                {
                    parsed.Add((item.Href, calendar));
                }
                else if (item.Ics is not null)
                {
                    broken.Add(item.Href);
                }
            }
            return new MirroredCalendar(header, packed, Etags(items.Values), EventIndex.Of(parsed)) { Broken = broken, Short = !header.Complete };
        }
        catch (Exception error) when (error is CryptographicException or JsonException or NotSupportedException
            or InvalidDataException or ArgumentException or IndexOutOfRangeException)
        {
            // The kind of trouble only: a JSON error's message would quote what the file holds.
            Console.Error.WriteLine($"Karpo: a calendar mirror not read ({error.GetType().Name})");
            return null;
        }
        finally
        {
            if (plain is not null)
            {
                CryptographicOperations.ZeroMemory(plain);
            }
        }
    }

    private static ICalendar? Parse(string? text)
    {
        if (text is null)
        {
            return null;
        }
        try
        {
            return ICalendar.Parse(text);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> Etags(IEnumerable<MirrorItem> items) =>
        items.ToDictionary(item => item.Href, item => item.Etag, StringComparer.Ordinal);

    /// <summary>The objects as JSON, deflated.</summary>
    internal static byte[] Pack(IEnumerable<MirrorItem> items)
    {
        using var packed = new MemoryStream();
        using (var deflate = new ZLibStream(packed, CompressionLevel.Fastest, leaveOpen: true))
        {
            JsonSerializer.Serialize(deflate, items.ToList(), MirrorJson.Default.ListMirrorItem);
        }
        return packed.ToArray();
    }

    /// <summary>The objects <see cref="Pack"/> made, by href.</summary>
    internal static Dictionary<string, MirrorItem> Unpack(byte[] packed)
    {
        using var deflate = new ZLibStream(new MemoryStream(packed), CompressionMode.Decompress);
        var items = JsonSerializer.Deserialize(deflate, MirrorJson.Default.ListMirrorItem) ?? [];
        var all = new Dictionary<string, MirrorItem>(items.Count, StringComparer.Ordinal);
        foreach (var item in items)
        {
            all[item.Href] = item;
        }
        return all;
    }
}

/// <summary>A mirror file's header: everything but the objects.</summary>
internal sealed record MirrorHeader
{
    [JsonPropertyName("v")]
    public int Version { get; init; }

    public MirrorCalendarName Calendar { get; init; } = new();

    /// <summary>Its place among its account's calendars, so a start without a connection lists them as the account does.</summary>
    public int Order { get; init; }

    /// <summary>When it was last synced well: a start takes the account's oldest for its read (PimCache.KeptRead).</summary>
    public DateTime UpdatedUtc { get; init; }

    public string? Token { get; init; }

    public string? Tag { get; init; }

    public bool NoSyncReport { get; init; }

    /// <summary>It holds all of the calendar as of its token; false while a first download is on its way, or cut short.</summary>
    public bool Complete { get; init; }

    public DateTime? ReconciledUtc { get; init; }
}

/// <summary>A calendar as its account lists it.</summary>
internal sealed record MirrorCalendarName
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string? Colour { get; init; }

    [JsonIgnore]
    public RemoteCalendar Remote => new(Id, Name, Colour);

    public static MirrorCalendarName Of(RemoteCalendar calendar) => new() { Id = calendar.Id, Name = calendar.Name, Colour = calendar.Colour };
}

/// <summary>One object as the file keeps it: its href, its etag, its iCalendar as kept (null: it has no event).</summary>
internal sealed class MirrorItem
{
    public string Href { get; set; } = "";

    public string Etag { get; set; } = "";

    public string? Ics { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MirrorHeader))]
[JsonSerializable(typeof(List<MirrorItem>))]
internal sealed partial class MirrorJson : JsonSerializerContext;
