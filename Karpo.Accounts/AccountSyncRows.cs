// What the Accounts page shows inside an account's card: what is synced of it, kind by kind, and
// a switch for each inbox, calendar and address book (docs\accounts.md, "What is synced").
//
// "in the account view it should show which items are synced and how many of them. you should also
// be able to not sync some of the emails inboxes, calendars, contact lists etc." (2026-10-11)
//
// Two sources, never the network: the account itself (accounts.json) says what is on and off, and
// the dock's report (sync-report.json, SyncReport) says what each part held at the last sync and
// when that was. A part the person has just turned off reads "Not synced" at once, before the dock
// has heard; one just turned on reads "Not synced yet" until the dock has read it.
//
// Each part wears a colour (Karpo.Accounts' SourceColours): the one the person picked, the one the
// account gives it, else the palette's by its place among every account's parts of that kind — worked
// out here from the same accounts and the dock's report in the same order the dock does, so the page's
// swatch is the colour the bar draws (Colours).
//
// Moved from the settings app (§338): the dock's menus show the same (OptionAccountRows' parts, Menu).

using System.Globalization;
using Karpo.Core;

namespace Karpo.Accounts;

/// <summary>One kind of an account, as the card's body shows it: a heading, then a row per part.</summary>
/// <param name="State">When it was synced, what went wrong, or that it has not been yet.</param>
public sealed record SyncGroup(AccountData Kind, string Title, string Glyph, string State, IReadOnlyList<SyncRow> Rows)
{
    /// <summary>
    /// A kind the person never let Karpo at (<see cref="SyncPart.Consent"/>: To Do on an account added before Karpo asked
    /// for it, a kind unticked on Google's consent screen): the heading offers "Reconnect to sync …".
    /// </summary>
    public bool Reconnect { get; init; }
}

/// <summary>One inbox, calendar or address book, with its colour and its switch.</summary>
/// <param name="Key">What turns it off and what its colour is kept by (<see cref="SyncKeys"/>).</param>
/// <param name="Detail">How many it holds, or that it is not synced.</param>
/// <param name="Colour">What it wears, <c>#RRGGBB</c>.</param>
/// <param name="Glyph">Its kind's glyph, before its name: the envelope, the calendar, the address book.</param>
public sealed record SyncRow(string Key, string Name, string Detail, bool On, string Colour, string Glyph)
{
    /// <summary>What it wears unpicked: the account's own colour, else the palette's (<see cref="SourceColours"/>).</summary>
    public string Automatic { get; init; } = "";

    /// <summary>The person picked <see cref="Colour"/> on this page.</summary>
    public bool Picked { get; init; }

    /// <summary><see cref="Automatic"/> is the account's own colour, not the palette's.</summary>
    public bool Given { get; init; }

    /// <summary>It wears a colour, and its row the swatch: an inbox, a calendar, a book; a task list does not.</summary>
    public bool Coloured { get; init; } = true;
}

/// <summary>A part's colour, as <see cref="AccountSyncRows.Colours"/> works it out.</summary>
/// <param name="Colour">What it wears.</param>
/// <param name="Automatic">What it would wear unpicked.</param>
/// <param name="Picked">The person picked it.</param>
/// <param name="Given">The account gives it a colour of its own.</param>
public readonly record struct PartColour(string Colour, string Automatic, bool Picked, bool Given);

public static class AccountSyncRows
{
    /// <summary>§639: a mail folder other than the inbox: Segoe Fluent Icons' Folder.</summary>
    public const string FolderGlyph = "";

    // The rows' glyphs: an inbox the envelope, a calendar the calendar, an address book the book.
    public const string InboxGlyph = "";
    public const string CalendarGlyph = "";
    public const string TaskListGlyph = "";
    public const string BookGlyph = "";

    /// <summary>
    /// The groups of <paramref name="account"/>'s card, in the order the line under its address
    /// names them: calendars, contacts, mail — only those it gives.
    /// </summary>
    /// <param name="report">What the dock last said of it; null before it has said anything.</param>
    /// <param name="now">Local now, which says "today".</param>
    /// <param name="colours">
    /// Every part's colour across the accounts (<see cref="Colours"/>); null works them out from this
    /// account alone, as a page with one account would.
    /// </param>
    public static IReadOnlyList<SyncGroup> Groups(Account account, SyncReportAccount? report, DateTime now, IReadOnlyDictionary<string, PartColour>? colours = null)
    {
        colours ??= Colours([account], report is null ? new SyncReportFile() : new SyncReportFile { Accounts = [report] });
        var groups = new List<SyncGroup>();
        if (account.Gives.HasFlag(AccountData.Calendars))
        {
            groups.Add(Group(account, AccountData.Calendars, report?.Calendars, now, colours));
        }
        if (account.Gives.HasFlag(AccountData.Contacts))
        {
            groups.Add(Group(account, AccountData.Contacts, report?.Contacts, now, colours));
        }
        if (account.Gives.HasFlag(AccountData.Mail))
        {
            groups.Add(Group(account, AccountData.Mail, report?.Mail, now, colours));
        }
        if (account.Gives.HasFlag(AccountData.Tasks))
        {
            groups.Add(Tasks(account, report?.Tasks, now));
        }
        return groups;
    }

    /// <summary>
    /// What every part of <paramref name="accounts"/> wears, keyed <see cref="SourceColours.Key"/>: per
    /// kind, the parts in the dock's order — the accounts as added, each one's calendars and address books
    /// as the dock last listed them (<paramref name="report"/>), its inbox — so the page says the colour
    /// the bar draws. With it, what each would wear unpicked, for the picker's Automatic.
    /// </summary>
    public static IReadOnlyDictionary<string, PartColour> Colours(IReadOnlyList<Account> accounts, SyncReportFile report)
    {
        var found = new Dictionary<string, PartColour>(StringComparer.Ordinal);
        Kind(accounts.Where(account => account.Gives.HasFlag(AccountData.Calendars))
            .SelectMany(account => (report.For(account.Id)?.Calendars?.Items ?? [])
                .Select(item => (account, SyncKeys.Calendar(item.Id), SourceColours.Normal(item.Colour)))));
        Kind(accounts.Where(account => account.Gives.HasFlag(AccountData.Contacts))
            .SelectMany(account => (report.For(account.Id)?.Contacts?.Items ?? [])
                .Select(item => (account, SyncKeys.Book(item.Id), (string?)null))));
        Kind(accounts.Where(account => account.Gives.HasFlag(AccountData.Mail))
            .Select(account => (account, SyncKeys.Inbox, (string?)null)));
        return found;

        void Kind(IEnumerable<(Account Account, string Key, string? Given)> kind)
        {
            var parts = kind.ToList();
            var worn = SourceColours.Of(parts);
            foreach (var (account, key, given) in parts)
            {
                var id = SourceColours.Key(account.Id, key);
                found.TryAdd(id, new PartColour(worn[id].Colour, worn[id].Automatic, account.Colour(key) is not null, given is not null));
            }
        }
    }

    /// <summary>The colour of the part <paramref name="key"/> of <paramref name="account"/>, worked out alone where <paramref name="colours"/> has none.</summary>
    private static PartColour ColourOf(IReadOnlyDictionary<string, PartColour> colours, Account account, string key, string? given)
    {
        if (colours.TryGetValue(SourceColours.Key(account.Id, key), out var colour))
        {
            return colour;
        }
        var (worn, automatic) = SourceColours.Assign([(account.Colour(key), SourceColours.Normal(given))])[0];
        return new PartColour(worn, automatic, account.Colour(key) is not null, SourceColours.Normal(given) is not null);
    }

    /// <summary>A colour in words, for a tooltip and a screen reader: the palette's name ("Blue"), else its <c>#RRGGBB</c>.</summary>
    public static string ColourName(string colour) => PartColours.Name(SourceColours.Normal(colour) ?? colour);

    /// <summary>
    /// §338: <paramref name="account"/> as a menu lists it (OptionAccountRows): its <see cref="Groups"/>, each
    /// part's switch, count and colour, and under a kind's heading why it did not sync, that it was
    /// never allowed (To Do, a kind unticked at Google), or, where it lists nothing yet, its state.
    /// </summary>
    public static OptionAccount Menu(Account account, SyncReportAccount? report, DateTime now, IReadOnlyDictionary<string, PartColour>? colours = null) =>
        new(account.Id, account.Address, AccountRows.Option(account.Kind))
        {
            Holds = Holds(account, report),
            Groups = [.. Groups(account, report, now, colours).Select(group => new OptionAccountGroup(group.Title, [.. group.Rows.Select(row => new OptionAccountPart(row.Key, row.Name, row.Glyph, row.On)
            {
                Detail = row.Detail,
                Colour = row.Picked ? row.Colour : null,
                Automatic = row.Coloured ? row.Automatic : "",
            })])
            {
                Note = group.Rows.Count == 0 || group.Reconnect || Failed(group.Kind, report) ? group.State : null,
                Reconnect = group.Reconnect,
                Kind = group.Kind switch
                {
                    AccountData.Calendars => AccountShows.Calendars,
                    AccountData.Contacts => AccountShows.Contacts,
                    AccountData.Mail => AccountShows.Mail,
                    _ => AccountShows.Tasks,
                },
                Glyph = group.Glyph,
                // §646: the mail's parts on their own page, Mail folders, its link saying what of them is synced.
                Folders = group.Kind == AccountData.Mail ? Synced(group.Rows) : null,
            })],
        };

    /// <summary>
    /// §638 (the person, 2026-11-09: "the accounts when collapsed should show the amount of items totally synced"): everything
    /// synced of <paramref name="account"/> as the dock last said (<paramref name="report"/>) — the events of its calendars, the
    /// contacts of its books, the messages of its inbox, the open tasks of its lists, each part counted whose count its row
    /// shows (synced, read, no problem of its own). §674 (the person, 2026-11-12: "your account overview should only show the
    /// combined amount of items synced not individually how many"): one sum, "1,444 items synced"; null for none counted.
    /// </summary>
    public static string? Holds(Account account, SyncReportAccount? report)
    {
        int? total = null;
        // A part's key (SyncKeys) from its item's id; null for an item that is no part of its own (the mail's but its inbox).
        void Kind(AccountData kind, SyncPart? part, Func<string, string?> key)
        {
            var counted = account.Gives.HasFlag(kind) && part is not null
                ? part.Items.Where(item => item is { Off: false, Count: not null } && item.Problem is not { Length: > 0 } && key(item.Id) is { } each && account.Syncs(each)).ToList()
                : [];
            if (counted.Count > 0)
            {
                total = (total ?? 0) + counted.Sum(item => item.Count!.Value);
            }
        }
        Kind(AccountData.Calendars, report?.Calendars, SyncKeys.Calendar);
        Kind(AccountData.Contacts, report?.Contacts, SyncKeys.Book);
        Kind(AccountData.Mail, report?.Mail, id => id == SyncKeys.Inbox ? SyncKeys.Inbox : null);
        Kind(AccountData.Tasks, report?.Tasks, SyncKeys.TaskList);
        return total is { } all ? S.SettingsAccountsSyncItems(all) : null;
    }

    /// <summary>
    /// §646: what the link to an account's Mail folders says of <paramref name="rows"/> (the inbox, every folder): "Inbox
    /// synced", "Inbox and 2 more synced", the inbox off "2 folders synced", none "Not synced".
    /// </summary>
    public static string Synced(IReadOnlyList<SyncRow> rows)
    {
        var inbox = rows.Any(row => row.Key == SyncKeys.Inbox && row.On);
        var more = rows.Count(row => row.Key != SyncKeys.Inbox && row.On);
        return (inbox, more) switch
        {
            (true, 0) => S.SettingsAccountsMailFoldersInbox,
            (true, _) => S.SettingsAccountsMailFoldersInboxAnd(more),
            (false, > 0) => S.SettingsAccountsMailFoldersSynced(more),
            _ => S.SettingsAccountsSyncOff,
        };
    }

    /// <summary>Whether the last sync of <paramref name="kind"/> went wrong.</summary>
    private static bool Failed(AccountData kind, SyncReportAccount? report) => (kind switch
    {
        AccountData.Calendars => report?.Calendars,
        AccountData.Contacts => report?.Contacts,
        AccountData.Mail => report?.Mail,
        _ => report?.Tasks,
    })?.Problem is { Length: > 0 };

    /// <summary>What a row's switch is named for a screen reader: "Sync Work, me@example.com".</summary>
    public static string SwitchName(Account account, SyncRow row) => S.SettingsAccountsSyncSwitchName(name: row.Name, address: account.Address);

    /// <summary>Whether two lists of groups have the same rows, so a page can change their words in place.</summary>
    public static bool SameRows(IReadOnlyList<SyncGroup> one, IReadOnlyList<SyncGroup> other) =>
        one.Count == other.Count
        && one.Zip(other).All(pair => pair.First.Kind == pair.Second.Kind
            && pair.First.Reconnect == pair.Second.Reconnect
            && pair.First.Rows.Select(row => row.Key).SequenceEqual(pair.Second.Rows.Select(row => row.Key)));

    private static SyncGroup Group(Account account, AccountData kind, SyncPart? part, DateTime now, IReadOnlyDictionary<string, PartColour> colours)
    {
        var rows = new List<SyncRow>();
        if (kind == AccountData.Mail)
        {
            // One inbox an account, whether or not the dock has read it yet: its switch is there at once.
            var inbox = part?.Items.FirstOrDefault(item => item.Id == SyncKeys.Inbox);
            var on = account.Syncs(SyncKeys.Inbox);
            rows.Add(Row(SyncKeys.Inbox, inbox?.Name ?? S.CoreAccountsInbox, Detail(kind, inbox, on, part is not null), on, ColourOf(colours, account, SyncKeys.Inbox, null), InboxGlyph));
            // §639: then every other folder as the dock last listed it, off until turned on, with its counts (off, its
            // unread) where the account gave them; it wears its inbox's colour on the bar, so it has no swatch of its own.
            foreach (var folder in part?.Folders ?? [])
            {
                var key = SyncKeys.MailFolder(folder.Id);
                var synced = account.Syncs(key);
                // Off: its unread beside "Not synced" where the account counted them — short, so the row's end holds it.
                var detail = synced ? Detail(kind, folder, true, true)
                    : folder.Unread is { } unread ? S.SettingsAccountsSyncPair(first: S.SettingsAccountsSyncOff, second: S.SettingsAccountsSyncUnread(unread))
                    : S.SettingsAccountsSyncOff;
                rows.Add(new SyncRow(key, folder.Name, detail, synced, "", FolderGlyph) { Coloured = false });
            }
        }
        else
        {
            foreach (var item in part?.Items ?? [])
            {
                var key = kind == AccountData.Calendars ? SyncKeys.Calendar(item.Id) : SyncKeys.Book(item.Id);
                var on = account.Syncs(key);
                var colour = ColourOf(colours, account, key, kind == AccountData.Calendars ? item.Colour : null);
                rows.Add(Row(key, item.Name, Detail(kind, item, on, true), on, colour, kind == AccountData.Calendars ? CalendarGlyph : BookGlyph));
            }
        }
        return new SyncGroup(kind, Title(kind), Glyph(kind), State(kind, part, account, now), rows) { Reconnect = part?.Consent == true };
    }

    /// <summary>
    /// An account's tasks: a row per task list as the dock last listed them, with its open tasks and its
    /// switch; none before the dock has read them. An account that never consented to its tasks says so,
    /// and its heading offers "Reconnect to sync tasks".
    /// </summary>
    private static SyncGroup Tasks(Account account, SyncPart? part, DateTime now)
    {
        var rows = new List<SyncRow>();
        foreach (var item in part?.Items ?? [])
        {
            var key = SyncKeys.TaskList(item.Id);
            var on = account.Syncs(key);
            var detail = !on ? S.SettingsAccountsSyncOff
                : item.Problem is { Length: > 0 } problem ? problem
                : item.Off || item.Count is null ? S.SettingsAccountsSyncNotYet
                : S.SettingsAccountsSyncOpenTasks(item.Count.Value);
            rows.Add(new SyncRow(key, item.Name, detail, on, "", TaskListGlyph) { Coloured = false });
        }
        return new SyncGroup(AccountData.Tasks, S.SettingsAccountsGivesTasks, TaskListGlyph, State(AccountData.Tasks, part, account, now), rows) { Reconnect = part?.Consent == true };
    }

    private static SyncRow Row(string key, string name, string detail, bool on, PartColour colour, string glyph) =>
        new(key, name, detail, on, colour.Colour, glyph) { Automatic = colour.Automatic, Picked = colour.Picked, Given = colour.Given };

    private static string Title(AccountData kind) => kind switch
    {
        AccountData.Calendars => S.SettingsAccountsGivesCalendars,
        AccountData.Contacts => S.SettingsAccountsGivesContacts,
        _ => S.SettingsAccountsGivesMail,
    };

    private static string Glyph(AccountData kind) => kind switch
    {
        AccountData.Calendars => "",
        AccountData.Contacts => "",
        _ => "",
    };

    /// <summary>
    /// The title row's words: that the person never let Karpo at it, why the last sync failed, else when it was (and,
    /// for the calendars, the span their counts are of), else that it has not been yet.
    /// </summary>
    internal static string State(AccountData kind, SyncPart? part, Account account, DateTime now)
    {
        if (kind == AccountData.Mail && !account.SyncsMail)
        {
            return S.SettingsAccountsSyncOff;
        }
        if (part?.Consent == true)
        {
            return S.SettingsAccountsSyncConsent(ConsentException.Service(account.Kind, kind));
        }
        if (part?.Problem is { Length: > 0 } problem)
        {
            return S.SettingsAccountsSyncFailed(problem);
        }
        if (part?.UpdatedUtc is not { } updated)
        {
            return S.SettingsAccountsSyncNotYet;
        }
        var when = When(updated, now);
        return kind == AccountData.Calendars && part is { FromUtc: { } from, ToUtc: { } to }
            ? S.SettingsAccountsSyncPair(first: when, second: S.SettingsAccountsSyncSpan(from: Month(from), to: Month(to.AddDays(-1))))
            : when;
    }

    /// <summary>"Synced at 14:05" today, "Synced 10 October at 14:05" before.</summary>
    internal static string When(DateTime updatedUtc, DateTime now)
    {
        var at = DateTime.SpecifyKind(updatedUtc, DateTimeKind.Utc).ToLocalTime();
        var time = at.ToString("t", CultureInfo.CurrentCulture);
        return at.Date == now.Date
            ? S.SettingsAccountsSyncAtToday(time)
            : S.SettingsAccountsSyncAtEarlier(day: at.ToString("M", CultureInfo.CurrentCulture), time: time);
    }

    private static string Month(DateTime utc) => utc.ToString("Y", CultureInfo.CurrentCulture);

    /// <summary>A row's words: off, not read yet, or how many it holds ("1234 messages · 12 unread", "56 events", "312 contacts").</summary>
    private static string Detail(AccountData kind, SyncItem? item, bool on, bool told)
    {
        if (!on)
        {
            return S.SettingsAccountsSyncOff;
        }
        // Turned on again since the dock last said, or never read: it is fetched at the next sync.
        if (!told || item is null || item.Off)
        {
            return S.SettingsAccountsSyncNotYet;
        }
        // A calendar or a book that alone failed its last read, or is not all there: why, in place of its count.
        if (item.Problem is { Length: > 0 } problem)
        {
            return problem;
        }
        return kind switch
        {
            AccountData.Calendars => S.SettingsAccountsSyncEvents(item.Count ?? 0),
            AccountData.Contacts => S.SettingsAccountsSyncPeople(item.Count ?? 0),
            _ => Counts(item) ?? S.SettingsAccountsSyncSynced,
        };
    }

    /// <summary>A mail folder's counts in words ("1234 messages · 12 unread"); null where the account said none.</summary>
    private static string? Counts(SyncItem item) => (item.Count, item.Unread) switch
    {
        ({ } count, { } unread) => S.SettingsAccountsSyncPair(first: S.SettingsAccountsSyncMessages(count), second: S.SettingsAccountsSyncUnread(unread)),
        (null, { } unread) => S.SettingsAccountsSyncUnread(unread),
        ({ } count, null) => S.SettingsAccountsSyncMessages(count),
        _ => null,
    };
}
