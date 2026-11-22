// One account Karpo reads, as it is kept in accounts.json.
//
// An account is what a person added on the settings app's Accounts page: a
// Microsoft account, a Google account, a mail server, or a calendar and contacts
// server. What it can give follows from its kind (Account.Gives), never from a
// list of switches: a Microsoft or Google account has all four, a mail server
// only mail, a DAV server calendars and contacts.
//
// The secret — a Microsoft or Google refresh token, or a password — is kept on the
// account, protected with DPAPI for this Windows user (Secrets). It never
// leaves this PC in the clear and is unreadable to another user or another PC,
// so accounts.json lives in the local folder, not beside the roaming config:
// a config carried to another PC must not carry a secret that cannot be read
// there.

using System.Text.Json.Serialization;

namespace Karpo.Accounts;

/// <summary>What an account is, which decides how it signs in and what it gives.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AccountKind>))]
public enum AccountKind
{
    /// <summary>A Microsoft account, personal or work: Graph, signed in through the browser.</summary>
    Microsoft,

    /// <summary>A mail server, with an address and a password.</summary>
    Imap,

    /// <summary>A CalDAV and CardDAV server, with a user name and a password.</summary>
    Dav,

    /// <summary>A Google account: Calendar, People, Gmail and Tasks, signed in through the browser (docs\accounts.md, "Google").</summary>
    Google,
}

/// <summary>What an account can give Karpo.</summary>
[Flags]
public enum AccountData
{
    None = 0,
    Calendars = 1,
    Contacts = 2,
    Mail = 4,

    /// <summary>Task lists and their tasks: Microsoft To Do, Google Tasks. Read and written (docs\accounts.md, "To Do").</summary>
    Tasks = 8,
}

/// <summary>How a mail connection is protected.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MailSecurity>))]
public enum MailSecurity
{
    /// <summary>TLS from the first byte (993).</summary>
    Implicit,

    /// <summary>Plain, then STARTTLS (143).</summary>
    StartTls,

    /// <summary>No TLS at all. Offered by autoconfig files only; never guessed.</summary>
    None,
}

/// <summary>One account, as accounts.json keeps it.</summary>
public sealed record Account
{
    /// <summary>Karpo's own id for it: random, and the same for as long as the account is kept.</summary>
    public required string Id { get; init; }

    public required AccountKind Kind { get; init; }

    /// <summary>The address it is known by: what the Accounts page lists and what a problem names.</summary>
    public required string Address { get; init; }

    /// <summary>The person's name as the account gave it, where it gave one.</summary>
    public string? Name { get; init; }

    /// <summary>The user name the server signs in with, where it is not the address (IMAP, DAV).</summary>
    public string? User { get; init; }

    /// <summary>The IMAP server.</summary>
    public string? Host { get; init; }

    public int Port { get; init; }

    public MailSecurity Security { get; init; }

    /// <summary>The CalDAV calendar home, found when the account was added; empty where it has none.</summary>
    public string? CalendarHome { get; init; }

    /// <summary>The CardDAV address-book home, found when the account was added; empty where it has none.</summary>
    public string? BookHome { get; init; }

    /// <summary>
    /// The refresh token or password, protected for this Windows user (<see cref="Secrets"/>).
    /// Never the secret itself.
    /// </summary>
    public string? Secret { get; init; }

    /// <summary>
    /// What the person turned off of it on the Accounts page, as <see cref="SyncKeys"/>: the inbox, a
    /// calendar, an address book. Nothing of these is fetched and nothing of them is shown. Null, or
    /// left out of the file, where nothing is off — every account before 2026-10-11 has everything on.
    /// </summary>
    public IReadOnlyList<string>? Off { get; init; }

    /// <summary>
    /// §639: what the person turned on of it on the Accounts page that is off until turned on, as <see cref="SyncKeys"/>:
    /// a mail folder other than the inbox (<see cref="SyncKeys.MailFolder"/>). Null, or left out of the file, where none
    /// is — an account's mail is its inbox alone until the person picks more.
    /// </summary>
    public IReadOnlyList<string>? On { get; init; }

    /// <summary>
    /// Whether the part <paramref name="key"/> (<see cref="SyncKeys"/>) is synced: an inbox, a calendar, a book, a task
    /// list unless turned off; a mail folder only where turned on (<see cref="SyncKeys.OffUntilOn"/>).
    /// </summary>
    public bool Syncs(string key) => SyncKeys.OffUntilOn(key)
        ? On?.Contains(key, StringComparer.Ordinal) == true
        : Off is null || !Off.Contains(key, StringComparer.Ordinal);

    /// <summary>§639: whether any of its mail is synced: its inbox, or a folder turned on. None, and its mail is not read at all.</summary>
    [JsonIgnore]
    public bool SyncsMail => Syncs(SyncKeys.Inbox) || On?.Any(SyncKeys.OffUntilOn) == true;

    /// <summary>The account with <paramref name="key"/> turned on or off; the same account where it already was.</summary>
    public Account Synced(string key, bool on)
    {
        if (Syncs(key) == on)
        {
            return this;
        }
        if (SyncKeys.OffUntilOn(key))
        {
            List<string> turned = on
                ? [.. On ?? [], key]
                : [.. On!.Where(one => !string.Equals(one, key, StringComparison.Ordinal))];
            return this with { On = turned.Count == 0 ? null : turned };
        }
        List<string> off = on
            ? [.. Off!.Where(one => !string.Equals(one, key, StringComparison.Ordinal))]
            : [.. Off ?? [], key];
        return this with { Off = off.Count == 0 ? null : off };
    }

    /// <summary>
    /// The colours the person picked on the Accounts page for parts of it, by <see cref="SyncKeys"/>:
    /// <c>#RRGGBB</c>. A part not named wears its own (<see cref="SourceColours"/>). Null, or left out
    /// of the file, where nothing was picked — every account before 2026-10-12.
    /// </summary>
    [JsonConverter(typeof(ColoursConverter))]
    public IReadOnlyDictionary<string, string>? Colours { get; init; }

    /// <summary>The colour the person picked for the part <paramref name="key"/>; null where they picked none (or one that does not read).</summary>
    public string? Colour(string key) =>
        Colours is { } colours && colours.TryGetValue(key, out var colour) ? SourceColours.Normal(colour) : null;

    /// <summary>The account with <paramref name="key"/> wearing <paramref name="colour"/>, or its own again (null); the same account where nothing changes.</summary>
    public Account Coloured(string key, string? colour)
    {
        var normal = SourceColours.Normal(colour);
        if (Colour(key) == normal && (normal is not null || Colours?.ContainsKey(key) != true))
        {
            return this;
        }
        var colours = new Dictionary<string, string>(Colours ?? new Dictionary<string, string>(), StringComparer.Ordinal);
        if (normal is null)
        {
            colours.Remove(key);
        }
        else
        {
            colours[key] = normal;
        }
        return this with { Colours = colours.Count == 0 ? null : colours };
    }

    /// <summary>What it can give.</summary>
    [JsonIgnore]
    public AccountData Gives => Kind switch
    {
        AccountKind.Microsoft or AccountKind.Google => AccountData.Calendars | AccountData.Contacts | AccountData.Mail | AccountData.Tasks,
        AccountKind.Imap => AccountData.Mail,
        AccountKind.Dav =>
            (string.IsNullOrEmpty(CalendarHome) ? AccountData.None : AccountData.Calendars)
            | (string.IsNullOrEmpty(BookHome) ? AccountData.None : AccountData.Contacts),
        _ => AccountData.None,
    };

    /// <summary>How the Accounts page names the kind.</summary>
    [JsonIgnore]
    public string KindName => Kind switch
    {
        AccountKind.Microsoft => "Microsoft",
        AccountKind.Google => "Google",
        AccountKind.Imap => S.CoreAccountsKindImap,
        AccountKind.Dav => "CalDAV / CardDAV", // not-ui: the protocols' names
        _ => Kind.ToString(),
    };

    /// <summary>
    /// It signs in in the browser and keeps a refresh token, not a password (OAuthClient): a Microsoft or a Google
    /// account, signed in to again by Reconnect, its token written back as the provider renews it.
    /// </summary>
    [JsonIgnore]
    public bool InBrowser => Kind is AccountKind.Microsoft or AccountKind.Google;

    /// <summary>A fresh id.</summary>
    public static string NewId() => Guid.NewGuid().ToString("N")[..12];
}

/// <summary>
/// The names of the parts of an account the person can turn off (<see cref="Account.Off"/>) or on
/// (<see cref="Account.On"/>): the inbox, a mail folder, one calendar, one address book — each by the id its
/// reader gives it, which is the same from run to run.
/// </summary>
public static class SyncKeys
{
    /// <summary>The account's inbox: its mail.</summary>
    public const string Inbox = "mail";

    /// <summary>
    /// §639: a mail folder other than the inbox (<see cref="MailFolder.Id"/>): off until the person turns it on on the
    /// Accounts page (<see cref="Account.On"/>), then read beside the inbox and offered to a mail widget's "Show from".
    /// </summary>
    public static string MailFolder(string id) => FolderPrefix + id;

    private const string FolderPrefix = "mail:";

    /// <summary>§639: whether the part <paramref name="key"/> is off until turned on: a mail folder; every other part is on until turned off.</summary>
    public static bool OffUntilOn(string key) => key.StartsWith(FolderPrefix, StringComparison.Ordinal);

    /// <summary>The calendar of that id (<see cref="RemoteCalendar.Id"/>).</summary>
    public static string Calendar(string id) => "calendar:" + id;

    /// <summary>The address book of that id (<see cref="AddressBook.Id"/>).</summary>
    public static string Book(string id) => "contacts:" + id;

    /// <summary>The task list of that id (<see cref="RemoteTaskList.Id"/>).</summary>
    public static string TaskList(string id) => "tasks:" + id;
}

/// <summary>
/// <see cref="Account.Colours"/> read tolerantly: an object of strings, its other entries skipped; anything
/// else is no colours — a hand edit there never makes the whole file unreadable, and so every account gone.
/// </summary>
internal sealed class ColoursConverter : JsonConverter<IReadOnlyDictionary<string, string>?>
{
    public override IReadOnlyDictionary<string, string>? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
    {
        if (reader.TokenType != System.Text.Json.JsonTokenType.StartObject)
        {
            reader.Skip();
            return null;
        }
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType == System.Text.Json.JsonTokenType.PropertyName)
        {
            var key = reader.GetString()!;
            reader.Read();
            if (reader.TokenType == System.Text.Json.JsonTokenType.String && SourceColours.Normal(reader.GetString()) is { } colour)
            {
                found[key] = colour;
            }
            else
            {
                reader.Skip();
            }
        }
        return found.Count == 0 ? null : found;
    }

    public override void Write(System.Text.Json.Utf8JsonWriter writer, IReadOnlyDictionary<string, string>? value, System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (key, colour) in value ?? new Dictionary<string, string>())
        {
            writer.WriteString(key, colour);
        }
        writer.WriteEndObject();
    }
}

/// <summary>accounts.json: the accounts, in the order they were added.</summary>
internal sealed class AccountFile
{
    public int Version { get; set; } = 1;

    public List<Account> Accounts { get; set; } = [];
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AccountFile))]
internal sealed partial class AccountJson : JsonSerializerContext;
