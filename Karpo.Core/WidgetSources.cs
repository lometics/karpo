// accounts: which of the accounts' parts a widget shows (docs\widgets.md §455). The person, 2026-10-31: "for the
// widgets like the contacts widget for example i should be able to select on a widget level which data / accounts /
// email folders to show. do this for the similar widgets as well."
//
// ⚙ › Accounts stays where an account's parts are synced and coloured, the one home of that (§351, §399); a widget
// only picks among what is synced, in its menu's "Show from" level: each part a switch, under its account's name
// where there are more accounts than one (§445). What it picked is the widget's (WidgetItem.Sources): each part by
// its account's address and its SyncKeys key ("me@example.com|calendar:AAMk…"), the address because signing in to
// an account again keeps it where its id would not, and the config roams to PCs whose accounts have other ids.
// Nothing written is every part — every calendar and address book, every account's inbox —, so a widget nobody set
// shows what it always did, and a calendar added at the account shows at once.
//
// What a widget may show from:
//
//  - a calendar of any style and the meetings: each account's calendars, and each country of the public holidays;
//  - the contacts: each account's address books;
//  - the mail: each account's inbox, on by default, and the other folders the account syncs, off by default. §639: a
//    folder is synced where the person turned it on in ⚙ › Accounts, as every other part is synced there; a widget
//    only picks among them (before, a folder was read while a mail widget picked it — gone). §644 (the person,
//    2026-11-09: "for the mail widget you should also be able to select which accounts and from the account which
//    folders you want to show in the widget itself"): every folder of every account is offered, under its account's
//    line, the ones not synced saying so; picking one turns its sync on in accounts.json, the Accounts page's one way,
//    and it says "Syncing…" until it is read. Unpicking it leaves its sync: other widgets may show it.
//
// The stores filter what they hold (SourceFilter); nothing is read twice for a widget.

using System.Runtime.CompilerServices;

namespace Karpo.Core;

/// <summary>
/// One part a widget may show from (<see cref="WidgetSources"/>): its key, its name, the name it stands under (its
/// account's, §445, or "Public holidays"), and whether a widget that picked nothing shows it.
/// </summary>
/// <param name="Key"><see cref="WidgetSources.Key"/>: its account's address and its part's key.</param>
/// <param name="Default">Shown where nothing is picked: every calendar and book, a mail account's inbox; not its other folders.</param>
public sealed record SourcePart(string Key, string Name, string Group, bool Default = true)
{
    /// <summary>§644: what its group's line says under the group's name: its account's address where the name is another; null for none.</summary>
    public string? GroupDetail { get; init; }

    /// <summary>§644: its group's colour, <c>#RRGGBB</c> (a mail account's inbox's, which its rows wear on the bar); null for none.</summary>
    public string? GroupColour { get; init; }

    /// <summary>§644: whether its account syncs it, and has read it since (⚙ › Accounts, accounts.json).</summary>
    public SourceSync Sync { get; init; }

    /// <summary>
    /// §644: turns its sync on for every widget, as ⚙ › Accounts' switch does (accounts.json): picking a part not synced
    /// (a mail folder off until turned on) does it, so the widget shows what it picked. Null where there is nothing to turn on.
    /// </summary>
    public Action? SyncOn { get; init; }
}

/// <summary>§644: whether a <see cref="SourcePart"/> is synced, which its row says at its end.</summary>
public enum SourceSync
{
    /// <summary>Synced and read: its row says nothing more.</summary>
    Synced,

    /// <summary>Turned on and not read since: "Syncing…".</summary>
    Reading,

    /// <summary>Not synced (a mail folder off until turned on, an inbox turned off): "Not synced"; picking it turns it on.</summary>
    Off,
}

/// <summary>What a widget picked to show from, as a set: every part where it picked nothing.</summary>
public sealed class SourceFilter
{
    /// <summary>Every part: a widget that picked nothing, a face, a test's store.</summary>
    public static readonly SourceFilter All = new(null);

    /// <summary>One filter per array a widget holds, so a line drawn every frame makes none.</summary>
    private static readonly ConditionalWeakTable<string[], SourceFilter> Made = [];

    private readonly HashSet<string>? chosen;

    private SourceFilter(string[]? chosen)
    {
        this.chosen = chosen is null ? null : new HashSet<string>(chosen, StringComparer.Ordinal);
        Id = chosen is null ? "" : string.Join("\n", chosen.Order(StringComparer.Ordinal));
    }

    /// <summary>What <paramref name="sources"/> (a widget's <see cref="WidgetItem.Sources"/>) picks: <see cref="All"/> for none.</summary>
    public static SourceFilter Of(string[]? sources) => sources is null ? All : Made.GetValue(sources, static each => new SourceFilter(each));

    /// <summary>What <paramref name="item"/> picked; <see cref="All"/> for none, or no widget.</summary>
    public static SourceFilter Of(WidgetItem? item) => Of(item?.Sources);

    /// <summary>The same for every filter that picks the same parts, whatever their order: what a store keeps a view by.</summary>
    public string Id { get; }

    /// <summary>Whether it picks nothing: every part's default.</summary>
    public bool Everything => chosen is null;

    /// <summary>
    /// Whether the part of <paramref name="key"/> is shown: picked, or where nothing is picked its
    /// <paramref name="byDefault"/>. A part of no key (a test's calendar, the smoke's) is always shown.
    /// </summary>
    public bool Shows(string? key, bool byDefault = true) => key is null || (chosen is null ? byDefault : chosen.Contains(key));
}

/// <summary>Which of the accounts' parts a widget shows; see the top of the file.</summary>
public static class WidgetSources
{
    /// <summary>The key of the rows of "Show from", each part's after it.</summary>
    public const string Row = "ShowFrom.";

    /// <summary>The group the public holidays' countries stand under, and their keys' first part.</summary>
    public const string HolidaysGroup = "holidays";

    /// <summary>A part's key: its account's address and its SyncKeys key ("me@example.com|calendar:work").</summary>
    public static string Key(string address, string part) => address + "|" + part;

    /// <summary>A public holidays' calendar's key: its region, else its country ("holidays|CH-ZH", "holidays|GB").</summary>
    public static string Holidays(HolidayShown calendar) => Key(HolidaysGroup, calendar.Region.Length > 0 ? calendar.Region : calendar.Country);

    /// <summary>§554: the key of the config's calendar <paramref name="calendar"/>, as it is shown (Windows' region's country for one that follows it).</summary>
    public static string Holidays(HolidayCountry calendar) => Key(HolidaysGroup, calendar.Region.Length > 0 ? calendar.Region : PublicHolidays.CountryOf(calendar));

    /// <summary>What <paramref name="item"/> may show from: the calendars, the contacts, the mail; none for anything else, a to-do line too (§455).</summary>
    public static AccountShows KindOf(WidgetItem? item) => OptionAccountRows.Shows(item) switch
    {
        AccountShows.Calendars => AccountShows.Calendars,
        AccountShows.Contacts => AccountShows.Contacts,
        AccountShows.Mail => AccountShows.Mail,
        _ => AccountShows.None,
    };

    /// <summary><paramref name="sources"/> as the file keeps them: each once, none empty; null for none at all.</summary>
    public static string[]? Normalized(string[]? sources) =>
        sources?.Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim()).Distinct(StringComparer.Ordinal).ToArray() is { Length: > 0 } kept ? kept : null;

    /// <summary>
    /// <paramref name="key"/> turned on or off in what <paramref name="item"/> shows of <paramref name="parts"/>: from
    /// what it shows now (its defaults where it picked nothing). Nothing is written where it comes back to its
    /// defaults; a part picked that is not listed now (an account not read yet) is kept. The last part is not turned
    /// off: a widget shows from something. False where nothing changed.
    /// </summary>
    public static bool Turn(WidgetItem item, IReadOnlyList<SourcePart> parts, string key, bool on)
    {
        var now = new List<string>(item.Sources ?? [.. parts.Where(part => part.Default).Select(part => part.Key)]);
        if (now.Contains(key, StringComparer.Ordinal) == on)
        {
            return false;
        }
        if (on)
        {
            now.Add(key);
        }
        else if (now.Count > 1)
        {
            now.Remove(key);
        }
        else
        {
            return false;
        }
        var listed = parts.Select(part => part.Key).ToHashSet(StringComparer.Ordinal);
        var defaults = parts.Where(part => part.Default).Select(part => part.Key).ToHashSet(StringComparer.Ordinal);
        item.Sources = defaults.SetEquals(now)
            ? null
            : [.. parts.Select(part => part.Key).Where(each => now.Contains(each, StringComparer.Ordinal)), .. now.Where(each => !listed.Contains(each))];
        return true;
    }

    /// <summary>The public holidays' calendars of <paramref name="holidays"/> as parts a calendar may show from, each country by its name.</summary>
    public static IReadOnlyList<SourcePart> HolidayParts(HolidayOptions? holidays) =>
        [.. PublicHolidays.Calendars(holidays?.Normalized()).Select(calendar => new SourcePart(Holidays(calendar), calendar.Label, HolidaysGroup))];

    /// <summary>
    /// "Show from": a level of switches, a part each, under its account's line where more than one place gives them
    /// (§445, §644), saying at its end what it shows. None where nothing is listed (no account synced yet).
    /// </summary>
    internal static void Into(WidgetItem item, IReadOnlyList<SourcePart>? parts, List<OptionRow> rows)
    {
        var kind = KindOf(item);
        if (kind == AccountShows.None || parts is not { Count: > 0 })
        {
            return;
        }
        var shown = SourceFilter.Of(item);
        var summary = Said(item, parts, kind);
        var headed = parts.Select(part => part.Group).Distinct(StringComparer.Ordinal).Count() > 1;
        OptionRow In(OptionRow row) => row with { Slot = OptionSlot.Subject, Group = S.DockMenuShowFromTitle, GroupGlyph = OptionGlyphs.ShowFrom, GroupSummary = summary };
        foreach (var group in parts.GroupBy(part => part.Group, StringComparer.Ordinal))
        {
            // §644: each account a line of its own over its parts — its name, its address under it, its colour where its rows
            // wear one (a mail account's inbox's) —, a line to read in the menu and a row of Show from's card on the settings
            // page alike (§445 had a heading, which the page's card leaves out).
            if (headed)
            {
                var first = group.First();
                rows.Add(In(new OptionRow(Row + "#" + group.Key, OptionRowKind.Note, group.Key == HolidaysGroup ? S.CoreHolidaysName : group.Key)
                {
                    Why = first.GroupDetail,
                    Swatch = Argb.Parse(first.GroupColour) is { } colour ? new LookSwatch(colour, null, null) : null,
                }));
            }
            foreach (var part in group)
            {
                // §644: a part not synced is offered too, off, saying so; picked, it is synced ("Syncing…" until it is read).
                rows.Add(In(new OptionRow(Row + part.Key, OptionRowKind.Toggle, part.Name)
                {
                    On = part.Sync != SourceSync.Off && shown.Shows(part.Key, part.Default),
                    Summary = part.Sync switch
                    {
                        SourceSync.Reading => S.DockMenuShowFromSyncing,
                        SourceSync.Off => S.SettingsAccountsSyncOff,
                        _ => null,
                    },
                    Set = value => Pick(item, parts, part, value != 0),
                }));
            }
        }
    }

    /// <summary>
    /// §644: <paramref name="part"/> picked (<paramref name="on"/>) or not in what <paramref name="item"/> shows of
    /// <paramref name="parts"/> (<see cref="Turn"/>); picked where it is not synced, its sync turned on for every widget
    /// (<see cref="SourcePart.SyncOn"/>, as ⚙ › Accounts' switch). Not picked, its sync stays: other widgets may show it, and
    /// ⚙ › Accounts is where a part's sync is turned off. False where nothing changed.
    /// </summary>
    public static bool Pick(WidgetItem item, IReadOnlyList<SourcePart> parts, SourcePart part, bool on)
    {
        var synced = on && part.Sync == SourceSync.Off && part.SyncOn is not null;
        if (synced)
        {
            part.SyncOn!();
        }
        return Turn(item, parts, part.Key, on) | synced;
    }

    /// <summary>
    /// What "Show from" says at its end: "All" (the mail's "All inboxes") where nothing is picked; the one part's name
    /// where one is; else how many of how many.
    /// </summary>
    internal static string Said(WidgetItem item, IReadOnlyList<SourcePart> parts, AccountShows kind)
    {
        if (item.Sources is null)
        {
            return kind == AccountShows.Mail ? S.DockMenuShowFromInboxes : S.DockMenuShowFromAll;
        }
        var shown = SourceFilter.Of(item);
        var on = parts.Where(part => part.Sync != SourceSync.Off && shown.Shows(part.Key)).ToList();
        return on.Count == 1 ? on[0].Name : S.DockMenuShowFromSome(on.Count, parts.Count);
    }
}
