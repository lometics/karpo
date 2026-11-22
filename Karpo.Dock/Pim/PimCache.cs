// What the calendar, mail, contacts and tasks stores keep on disk, so the
// events, the inboxes and the people show without a connection
// (docs\accounts.md, "Offline").
//
// One file per kind, in the local folder (Config.LocalFolder, so an isolated
// run writes its own and a test hands a folder of its own): calendar-cache.json,
// mail-cache.json, contacts-cache.json and tasks-cache.json. A CalDAV calendar
// is not in calendar-cache.json: it is kept whole, a file of its own
// (CalendarMirror.cs, docs\widgets.md §616). Each holds, per
// account, what the widgets draw of it and when it was read — nothing else. No
// token, no password, no address, no server: an account is its random id. An
// event is its title, place, times and whether it is all day; a calendar its
// id, name and colour; a message its sender, the sender's address, its subject,
// when it came and whether it is unread; a person their name, organisation,
// addresses and numbers, by address book.
//
// Encrypted for this Windows user: the JSON goes through DPAPI (CurrentUser,
// with an entropy of the cache's own, as the secrets in accounts.json do), so
// the files read as nothing to another user, another PC or a copy carried off.
// A file that does not decrypt — another user's, damaged, or the plain JSON of
// before — is the version before it (name.bak, which every write keeps) where
// that does, else no cache, and the next good read writes over it. A file
// another program holds (a virus scanner, a backup) is not an empty cache: the
// store takes it in later and writes nothing over it before — tasks-cache.json
// is the only copy of the To Do changes not sent yet. Neither the JSON nor
// anything in it is ever logged.
//
// Bounded: the events of the window the calendars are read for (Window: last
// month to a year after this one, what the month and year views reach without
// stepping), at most MostEvents a calendar, and the newest MailStore.Newest
// messages an account. The stores read it once, on the pool, before their first
// read of the accounts, and write it there after every read that brought
// something new; a view never touches it — it draws what the store holds in memory.
//
// A restart is no reason to read (the person, 2026-10-31: "i dont want the
// application to fetch the data just because there was a restart"): an account
// taken from the file is due the interval after it was read there (KeptRead), as
// it would have been had the dock run on — the dock's start, a sign-in and a wake
// read only what is due. One kept for another window, missing from the file, or
// the interval old reads at once; the person's own Refresh reads at once whatever
// the file says.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Karpo.Accounts;
using Karpo.Core;

namespace Karpo.Dock.Pim;

/// <summary>
/// How old what a widget shows is: when the accounts behind it were last read, and whether the last
/// try to read one of them again failed and left what was read before on screen.
/// </summary>
/// <param name="UpdatedUtc">The oldest last good read among the accounts shown; null before any.</param>
/// <param name="Stale">A refresh failed and what is shown is from <paramref name="UpdatedUtc"/>.</param>
public readonly record struct Freshness(DateTime? UpdatedUtc, bool Stale)
{
    /// <summary>
    /// What the stale mark's tooltip says: "Last updated 14:05 — couldn't refresh", the day before the
    /// time where it was not today; null while nothing is stale.
    /// </summary>
    public string? Tip(DateTime now) => Stale && UpdatedUtc is { } at ? Say(at.ToLocalTime(), now) : null;

    /// <summary>The words for a read at <paramref name="at"/> (local), seen at <paramref name="now"/> (local).</summary>
    internal static string Say(DateTime at, DateTime now)
    {
        var time = Panels.Nls.Time(at);
        return at.Date == now.Date
            ? S.DockAccountsStaleToday(time)
            : S.DockAccountsStaleEarlier(day: Panels.Nls.MonthDay(DateOnly.FromDateTime(at)), time: time);
    }

    /// <summary>
    /// The freshness of several accounts' data: stale where any account shown failed its last
    /// refresh, from the oldest of those reads; else from the oldest read of all.
    /// </summary>
    internal static Freshness Of(IEnumerable<(DateTime UpdatedUtc, bool Failed)> accounts)
    {
        DateTime? oldest = null;
        DateTime? oldestFailed = null;
        foreach (var (updated, failed) in accounts)
        {
            if (oldest is null || updated < oldest)
            {
                oldest = updated;
            }
            if (failed && (oldestFailed is null || updated < oldestFailed))
            {
                oldestFailed = updated;
            }
        }
        return oldestFailed is { } stale ? new Freshness(stale, true) : new Freshness(oldest, false);
    }
}

/// <summary>The files, and their bounds.</summary>
internal sealed class PimCache(string folder)
{
    /// <summary>Months of the window before this one: enough for a month view stepped back once.</summary>
    internal const int MonthsBack = 1;

    /// <summary>Months of the window after this one: a year view of the coming year fits inside it.</summary>
    internal const int MonthsAhead = 13;

    /// <summary>Events kept of one calendar at most, whatever the calendar holds in the window.</summary>
    internal const int MostEvents = 2000;

    internal const string CalendarFile = "calendar-cache.json";

    internal const string MailFile = "mail-cache.json";

    internal const string TasksFile = "tasks-cache.json";

    internal const string ContactsFile = "contacts-cache.json";

    /// <summary>The cache's own, so no other DPAPI blob of this user — an account's secret — decrypts as a cache.</summary>
    private static readonly byte[] Entropy = "Karpo.PimCache.v1"u8.ToArray();

    private static readonly Lock Writing = new();

    /// <summary>Under <see cref="Writing"/>: the files that did not read at their last read, of which the next write keeps no backup.</summary>
    private readonly HashSet<string> unread = new(StringComparer.Ordinal);

    /// <summary>The dock's: the local folder. Null on an isolated run, which has no account to keep.</summary>
    internal static PimCache? ForDock() => Karpo.Core.Config.IsIsolated ? null : new PimCache(Karpo.Core.Config.LocalFolder);

    internal string Folder { get; } = folder;

    /// <summary>
    /// The window the calendars are read for (AccountCalendars, until a view asks for more) and the
    /// cache keeps, in UTC: from the start of last month to the start of the thirteenth month after this
    /// one. One window for both, so the calendars kept are the calendars read, and a start finds in the
    /// file all its first read would have asked for.
    /// </summary>
    internal static (DateTime From, DateTime To) Window(DateTime utcNow)
    {
        var month = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (month.AddMonths(-MonthsBack), month.AddMonths(MonthsAhead));
    }

    /// <summary>
    /// A read kept in the file, made at <paramref name="updatedUtc"/>, on this run's clock
    /// (Environment.TickCount64, now <paramref name="tick"/>): when it would have finished had the dock
    /// run on since. A store takes it as a read of its own, so the account is due the
    /// <paramref name="interval"/> after it and a restart reads nothing sooner. Null where the read is
    /// the interval old or older, or dated after now (the clock set back): the account is due at once.
    /// </summary>
    internal static long? KeptRead(DateTime updatedUtc, DateTime utcNow, TimeSpan interval, long tick)
    {
        var age = utcNow - updatedUtc;
        return age >= TimeSpan.Zero && age < interval ? tick - (long)age.TotalMilliseconds : null;
    }

    /// <summary>What calendar-cache.json holds; empty where there is none or it does not read; null where another program holds it.</summary>
    internal CalendarCacheFile? ReadCalendars() => Read(CalendarFile, PimJson.Default.CalendarCacheFile, static () => new CalendarCacheFile());

    /// <summary>What mail-cache.json holds; empty where there is none or it does not read; null where another program holds it.</summary>
    internal MailCacheFile? ReadMail() => Read(MailFile, PimJson.Default.MailCacheFile, static () => new MailCacheFile());

    internal void Write(CalendarCacheFile file) => Write(CalendarFile, JsonSerializer.SerializeToUtf8Bytes(file, PimJson.Default.CalendarCacheFile));

    internal void Write(MailCacheFile file) => Write(MailFile, JsonSerializer.SerializeToUtf8Bytes(file, PimJson.Default.MailCacheFile));

    /// <summary>What tasks-cache.json holds (TaskStore); empty where there is none or it does not read; null where another program holds it.</summary>
    internal TaskCacheFile? ReadTasks() => Read(TasksFile, PimJson.Default.TaskCacheFile, static () => new TaskCacheFile());

    internal void Write(TaskCacheFile file) => Write(TasksFile, JsonSerializer.SerializeToUtf8Bytes(file, PimJson.Default.TaskCacheFile));

    /// <summary>What contacts-cache.json holds (ContactStore); empty where there is none or it does not read; null where another program holds it.</summary>
    internal ContactCacheFile? ReadContacts() => Read(ContactsFile, PimJson.Default.ContactCacheFile, static () => new ContactCacheFile());

    internal void Write(ContactCacheFile file) => Write(ContactsFile, JsonSerializer.SerializeToUtf8Bytes(file, PimJson.Default.ContactCacheFile));

    /// <summary><paramref name="json"/> encrypted for this Windows user, as the files hold it.</summary>
    internal static byte[] Protect(byte[] json) => ProtectedData.Protect(json, Entropy, DataProtectionScope.CurrentUser);

    /// <summary>The JSON <paramref name="kept"/> holds; throws CryptographicException where it is not this user's cache.</summary>
    internal static byte[] Unprotect(byte[] kept) => ProtectedData.Unprotect(kept, Entropy, DataProtectionScope.CurrentUser);

    /// <summary>
    /// One Microsoft account's calendars as the cache keeps them: each calendar's occurrences in
    /// <see cref="Window"/>, at most <see cref="MostEvents"/> each, by start. A CalDAV calendar is not
    /// kept here: it is kept whole, in a file of its own (CalendarMirror). Dated with the window it holds all of — what both
    /// <see cref="Window"/> and the read (<paramref name="read"/>) cover — so a start that wants no more
    /// reads nothing; none where a calendar was cut at <see cref="MostEvents"/> or <paramref name="read"/>
    /// is none (a calendar missing from it), and the next start reads the account.
    /// </summary>
    internal static CalendarCacheAccount Keep(string account, DateTime updatedUtc, IEnumerable<(RemoteCalendar Calendar, AccountCalendars.Held Held)> calendars, DateTime utcNow, (DateTime From, DateTime To)? read = null)
    {
        var (from, to) = Window(utcNow);
        var kept = new CalendarCacheAccount { Account = account, UpdatedUtc = updatedUtc };
        var whole = read is not null;
        foreach (var (calendar, held) in calendars)
        {
            var events = new List<Occurrence>();
            foreach (var occurrence in held.Occurrences)
            {
                if (Within(occurrence, from, to))
                {
                    events.Add(occurrence);
                }
            }
            events.Sort(static (a, b) => a.Start.CompareTo(b.Start));
            kept.Calendars.Add(new CachedCalendar
            {
                Id = calendar.Id,
                Name = calendar.Name,
                Colour = calendar.Colour,
                Events = [.. events.Take(MostEvents).Select(CachedEvent.Of)],
            });
            whole &= events.Count <= MostEvents;
        }
        if (whole && read is { } covered && Max(from, covered.From) is var first && Min(to, covered.To) is var last && first < last)
        {
            (kept.From, kept.To) = (first, last);
        }
        return kept;
    }

    private static DateTime Max(DateTime one, DateTime other) => one > other ? one : other;

    private static DateTime Min(DateTime one, DateTime other) => one < other ? one : other;

    /// <summary>
    /// One account's inbox as the cache keeps it: the newest <see cref="MailStore.Newest"/>, what a row shows of each; and
    /// so of each other folder it syncs (§639), by its id, with its name.
    /// </summary>
    internal static MailCacheAccount Keep(string account, DateTime updatedUtc, Inbox inbox, IEnumerable<FolderRead>? folders = null, IEnumerable<FolderRead>? listed = null)
    {
        List<MailCacheFolder> kept = [.. (folders ?? []).Where(folder => folder.Mail is not null)
            .Select(folder => new MailCacheFolder { Id = folder.Folder.Id, Name = folder.Folder.Name, Unread = folder.Mail!.Unread, Messages = Newest(folder.Mail) })];
        List<MailCacheListed> all = [.. (listed ?? []).Select(folder => new MailCacheListed { Id = folder.Folder.Id, Name = folder.Folder.Name, Unread = folder.Folder.Unread, Total = folder.Folder.Total })];
        return new()
        {
            Account = account,
            UpdatedUtc = updatedUtc,
            Unread = inbox.Unread,
            Messages = Newest(inbox),
            Folders = kept.Count > 0 ? kept : null,
            Listed = all.Count > 0 ? all : null,
        };
    }

    private static List<CachedMail> Newest(Inbox inbox) => [.. inbox.Messages.OrderByDescending(message => message.Received).Take(MailStore.Newest).Select(CachedMail.Of)];

    /// <summary>
    /// Whether <paramref name="occurrence"/> is on in [<paramref name="from"/>, <paramref name="to"/>),
    /// a whole day or a floating time taken as if UTC: at most a day off at the cache's edges.
    /// </summary>
    private static bool Within(Occurrence occurrence, DateTime from, DateTime to) =>
        CalendarStore.Overlaps(
            DateTime.SpecifyKind(occurrence.Start, DateTimeKind.Utc),
            DateTime.SpecifyKind(occurrence.End, DateTimeKind.Utc),
            from,
            to);

    /// <summary>
    /// What the file holds; null where another program held it every time it was tried (a virus
    /// scanner, a backup): what it holds is not known, and the caller writes nothing over it until it
    /// has read it. A file that does not read (cut short, damaged, another user's, the plain JSON of
    /// before) is the version before it (name.bak) where that reads; else no cache, and the next good
    /// read writes it again.
    /// </summary>
    private T? Read<T>(string name, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, Func<T> none)
        where T : class
    {
        var path = Path.Combine(Folder, name);
        switch (DurableFile.ReadBytes(path, out var kept))
        {
            case FileRead.Missing:
                return none();
            case FileRead.Locked:
                Console.Error.WriteLine($"Karpo: {name} held by another program; not read");
                return null;
        }
        if (Open(name, kept!, type) is { } file)
        {
            lock (Writing)
            {
                unread.Remove(name);
            }
            return file;
        }
        lock (Writing)
        {
            unread.Add(name);
        }
        return DurableFile.ReadBytes(DurableFile.BackupOf(path), out var before) == FileRead.Ok && Open(name, before!, type) is { } backup
            ? backup
            : none();
    }

    /// <summary>The cache <paramref name="kept"/> holds, or null where it is not this user's or does not read as one.</summary>
    private static T? Open<T>(string name, byte[] kept, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type)
        where T : class
    {
        byte[]? json = null;
        try
        {
            json = Unprotect(kept);
            return JsonSerializer.Deserialize(json, type);
        }
        catch (Exception error) when (error is CryptographicException or JsonException or NotSupportedException)
        {
            // The kind of trouble only — a JSON error's message would quote what the file holds.
            Console.Error.WriteLine($"Karpo: {name} not read ({error.GetType().Name})");
            return null;
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
    /// <paramref name="json"/> encrypted and written the durable way (DurableFile: through to the disk,
    /// swapped in, the version before kept as name.bak), so a crash never leaves half a cache — the
    /// tasks' holds the only copy of the To Do changes not sent yet. One write at a time; the plain
    /// bytes are wiped after.
    /// </summary>
    private void Write(string name, byte[] json)
    {
        lock (Writing)
        {
            try
            {
                // A file that did not read at the last read never becomes the backup: the good version before it stays.
                DurableFile.Write(Path.Combine(Folder, name), Protect(json), keepBackup: !unread.Remove(name));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or CryptographicException)
            {
                Console.Error.WriteLine($"Karpo: {name} not saved ({error.GetType().Name}: {error.HResult:X8})");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(json);
            }
        }
    }
}

/// <summary>calendar-cache.json.</summary>
internal sealed class CalendarCacheFile
{
    public List<CalendarCacheAccount> Accounts { get; set; } = [];
}

/// <summary>One account's calendars, as last read.</summary>
internal sealed class CalendarCacheAccount
{
    /// <summary>Karpo's own id for the account (Account.Id): random, and nothing of the account itself.</summary>
    public string Account { get; set; } = "";

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// The window it holds every event of (UTC, PimCache.Keep): a start that wants no more of the
    /// account takes it for the read it was. None where it holds only part of one, and in a file from
    /// before: the account is read at the start.
    /// </summary>
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public List<CachedCalendar> Calendars { get; set; } = [];
}

internal sealed class CachedCalendar
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary><c>#RRGGBB</c>, where the account gives one.</summary>
    public string? Colour { get; set; }

    public List<CachedEvent> Events { get; set; } = [];

    internal RemoteCalendar Remote => new(Id, Name, Colour);
}

/// <summary>An occurrence as a row shows it (<see cref="Occurrence"/>).</summary>
internal sealed class CachedEvent
{
    public string Title { get; set; } = "";

    public string? Location { get; set; }

    public bool AllDay { get; set; }

    public bool Floating { get; set; }

    /// <summary>A date or a wall clock when all day or floating; else UTC.</summary>
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>Where its online meeting is joined, the link alone (docs\widgets.md §205); none in a file from before.</summary>
    public string? Join { get; set; }

    /// <summary>Shown as free or declined; written only where it is.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Free { get; set; }

    /// <summary>Where the event opens on its account's site (§434); written only where the account named one.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? Link { get; set; }

    internal static CachedEvent Of(Occurrence occurrence) => new()
    {
        Title = occurrence.Title,
        Location = occurrence.Location,
        AllDay = occurrence.AllDay,
        Floating = occurrence.Floating,
        Start = occurrence.Start,
        End = occurrence.End,
        Join = occurrence.Join,
        Free = occurrence.Free,
        Link = occurrence.Link,
    };

    internal Occurrence Occurrence
    {
        get
        {
            var kind = AllDay || Floating ? DateTimeKind.Unspecified : DateTimeKind.Utc;
            return new Occurrence(Title, Location, AllDay, Floating, DateTime.SpecifyKind(Start, kind), DateTime.SpecifyKind(End, kind), Join, Free, Link);
        }
    }
}

/// <summary>mail-cache.json.</summary>
internal sealed class MailCacheFile
{
    public List<MailCacheAccount> Accounts { get; set; } = [];
}

/// <summary>One account's inbox, as last read.</summary>
internal sealed class MailCacheAccount
{
    public string Account { get; set; } = "";

    public DateTime UpdatedUtc { get; set; }

    public int Unread { get; set; }

    public List<CachedMail> Messages { get; set; } = [];

    /// <summary>§639: the folders it syncs besides the inbox; absent where it syncs none.</summary>
    public List<MailCacheFolder>? Folders { get; set; }

    /// <summary>
    /// §683 (the person, 2026-11-12: "mail folders only shows the inbox which is not correct"): every folder the account listed
    /// besides the inbox, synced or not, so its Mail folders page lists them from the start, not only once it is read again.
    /// </summary>
    public List<MailCacheListed>? Listed { get; set; }

    internal Inbox Inbox => new(Unread, [.. Messages.Select(message => message.Item)]);
}

/// <summary>§639: one folder besides the inbox, as last read: its id and its name, its messages as its inbox's are kept.</summary>
internal sealed class MailCacheFolder
{
    public string Id { get; set; } = "";

    /// <summary>Its name as a row shows it, where more than one folder is shown; absent from a file of before, which reads as its id.</summary>
    public string? Name { get; set; }

    public int Unread { get; set; }

    public List<CachedMail> Messages { get; set; } = [];

    internal Inbox Mail => new(Unread, [.. Messages.Select(message => message.Item)]);
}

/// <summary>§683: one folder an account listed besides the inbox: its id, its name, its counts as listed.</summary>
internal sealed class MailCacheListed
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public int? Unread { get; set; }

    public int? Total { get; set; }
}

/// <summary>A message as a row shows it: no id, no preview, nothing a row does not draw.</summary>
internal sealed class CachedMail
{
    public string From { get; set; } = "";

    public string? FromAddress { get; set; }

    public string Subject { get; set; } = "";

    public DateTimeOffset Received { get; set; }

    public bool Unread { get; set; }

    /// <summary>Where a click on its row opens it (MailItem.Link); absent from a file written before rows opened, which reads as none.</summary>
    public string? Link { get; set; }

    internal static CachedMail Of(MailItem item) => new()
    {
        From = item.From,
        FromAddress = item.FromAddress,
        Subject = item.Subject,
        Received = item.Received,
        Unread = item.Unread,
        Link = item.Link,
    };

    internal MailItem Item => new("", From, FromAddress, Subject, null, Received, Unread, MailLinks.Web(Link));
}

/// <summary>
/// tasks-cache.json: each Microsoft account's To Do lists, their tasks and the changes not sent yet
/// (TaskStore). Unlike the mail's, a task keeps its id and its version: a change waiting here names
/// the task it is for, and goes against the version it was made on.
/// </summary>
internal sealed class TaskCacheFile
{
    public List<TaskCacheAccount> Accounts { get; set; } = [];
}

internal sealed class TaskCacheAccount
{
    public string Account { get; set; } = "";

    public DateTime UpdatedUtc { get; set; }

    public List<CachedTaskList> Lists { get; set; } = [];

    /// <summary>The changes made on the line and not taken by the account yet, in order.</summary>
    public List<TaskChange> Queue { get; set; } = [];
}

internal sealed class CachedTaskList
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public bool Default { get; set; }

    /// <summary>Its tasks were read (it is not turned off): an empty list is then no tasks, not unknown.</summary>
    public bool Read { get; set; }

    public List<RemoteTask> Tasks { get; set; } = [];

    internal RemoteTaskList Remote => new(Id, Name, Default);

    internal static CachedTaskList Of(RemoteTaskList list, IReadOnlyList<RemoteTask>? tasks) => new()
    {
        Id = list.Id,
        Name = list.Name,
        Default = list.Default,
        Read = tasks is not null,
        Tasks = [.. tasks ?? []],
    };
}

/// <summary>
/// contacts-cache.json: each account's address books and who is in them, as the contacts line shows and
/// searches them (ContactStore) — a person's name, organisation, addresses and numbers, and nothing else.
/// </summary>
internal sealed class ContactCacheFile
{
    public List<ContactCacheAccount> Accounts { get; set; } = [];
}

internal sealed class ContactCacheAccount
{
    public string Account { get; set; } = "";

    public DateTime UpdatedUtc { get; set; }

    public List<CachedBook> Books { get; set; } = [];
}

/// <summary>One address book as its account's last good read left it (<see cref="BookRead"/>).</summary>
internal sealed class CachedBook
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>The person turned it off: listed, with nobody in it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Off { get; set; }

    /// <summary>Why it alone could not be read at that read, keeping the people of the one before; written only where it was.</summary>
    public string? Failed { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Incomplete { get; set; }

    public List<CachedPerson> People { get; set; } = [];

    internal BookRead Read => new(new AddressBook(Id, Name), [.. People.Select(person => person.Person)], Off) { Failed = Failed, Incomplete = Incomplete };

    internal static CachedBook Of(BookRead book) => new()
    {
        Id = book.Book.Id,
        Name = book.Book.Name,
        Off = book.Off,
        Failed = book.Failed,
        Incomplete = book.Incomplete,
        People = [.. book.People.Select(CachedPerson.Of)],
    };
}

/// <summary>A person as the contacts line draws and finds them (<see cref="Person"/>), without the book's colour, which the dock paints.</summary>
internal sealed class CachedPerson
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string? Organization { get; set; }

    public List<string> Emails { get; set; } = [];

    public List<string> Phones { get; set; } = [];

    internal Person Person => new(Id, DisplayName, Organization, Emails, Phones);

    internal static CachedPerson Of(Person person) => new()
    {
        Id = person.Id,
        DisplayName = person.DisplayName,
        Organization = person.Organization,
        Emails = [.. person.Emails],
        Phones = [.. person.Phones],
    };
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CalendarCacheFile))]
[JsonSerializable(typeof(MailCacheFile))]
[JsonSerializable(typeof(TaskCacheFile))]
[JsonSerializable(typeof(ContactCacheFile))]
internal sealed partial class PimJson : JsonSerializerContext;
