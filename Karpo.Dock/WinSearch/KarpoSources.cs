// §652: Windows search's sources of Karpo's own (docs\widgets.md §652): what Karpo keeps on this PC, found as Windows'
// own is — the messages its accounts' inboxes synced (MailStore), the events of its calendars (CalendarStore), the people
// of its address books (ContactStore), its notes (Notebook) — and Karpo itself: the pages and rows of its settings, the
// widgets there are to add, the layouts. Only what Karpo already holds in memory: nothing is read from an account for a
// search, nothing leaves the PC. A source whose account the person has none of finds nothing.
//
// Each is matched by the matcher its data has already: a person by the contacts' line's own search (ContactSearch.Score), a
// widget by the add page's (CatalogueSearch), a name by Windows search's one ranking (WinSearchRank.NameScore). What each
// reads is handed in, so the tests bring their own and the samples theirs (Samples.cs).
//
// What a hit of Karpo's own opens is an address of Karpo's (KarpoTargets: "karpo:settings/Karpo", "karpo:add/Clock",
// "karpo:layout/1", "karpo:note/<id>"), which the dock does once the folder has gone (Dock.Folders.cs, OpenKarpo).

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Pim;

namespace Karpo.Dock.WinSearch;

/// <summary>§652: an address of Karpo's own a hit opens, and what it says (see the top of the file).</summary>
/// <param name="Kind">What it opens: a settings page, the add page, a layout, a note.</param>
/// <param name="Page">The settings page (<see cref="KarpoTargetKind.Settings"/>).</param>
/// <param name="Value">Its level (a page's row key), the add page's search, a layout's place, a note's id; null for none.</param>
internal readonly record struct KarpoTarget(KarpoTargetKind Kind, PanelPage Page = default, string? Value = null);

/// <summary>§652: what an address of Karpo's opens.</summary>
internal enum KarpoTargetKind
{
    /// <summary>A page of Karpo's settings, at a row where it says one.</summary>
    Settings,

    /// <summary>The add page, its search the widget's name.</summary>
    Add,

    /// <summary>A layout, this display switched to it.</summary>
    Layout,

    /// <summary>A note: the folder on this bar showing it, else the add page at the note widget.</summary>
    Note,
}

/// <summary>§652: Karpo's own addresses, written and read.</summary>
internal static class KarpoTargets
{
    public const string Scheme = "karpo:";

    public static string Settings(PanelPage page, string? level = null) => Scheme + "settings/" + page + (level is null ? "" : "/" + Uri.EscapeDataString(level));

    public static string Add(string words) => Scheme + "add/" + Uri.EscapeDataString(words);

    public static string Layout(int index) => Scheme + "layout/" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public static string Note(string id) => Scheme + "note/" + Uri.EscapeDataString(id);

    /// <summary>What <paramref name="target"/> opens; null for an address that is not Karpo's (or not one it knows).</summary>
    public static KarpoTarget? Parse(string? target)
    {
        if (target is null || !target.StartsWith(Scheme, StringComparison.Ordinal))
        {
            return null;
        }
        var parts = target[Scheme.Length..].Split('/', 3);
        string? Part(int at) => parts.Length > at ? Uri.UnescapeDataString(parts[at]) : null;
        return parts[0] switch
        {
            "settings" when Enum.TryParse<PanelPage>(Part(1), out var page) && Enum.IsDefined(page) => new KarpoTarget(KarpoTargetKind.Settings, page, Part(2)),
            "add" when Part(1) is { } words => new KarpoTarget(KarpoTargetKind.Add, Value: words),
            "layout" when int.TryParse(Part(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out _) => new KarpoTarget(KarpoTargetKind.Layout, Value: Part(1)),
            "note" when Part(1) is { Length: > 0 } id => new KarpoTarget(KarpoTargetKind.Note, Value: id),
            _ => null,
        };
    }
}

/// <summary>§652: a name's score against what is typed, or a lesser one where it is found by its words alone (a widget's description, a note's text).</summary>
internal static class KarpoMatch
{
    /// <summary>What words alone score: as a name that holds what is typed, under one that starts so.</summary>
    public const int ByWords = 30;

    /// <summary>
    /// The best of <paramref name="names"/>' scores (WinSearchRank.NameScore); none, then <see cref="ByWords"/> where every word
    /// typed — three letters each at least, so "pa" is no question about a text — is in <paramref name="words"/>
    /// (CatalogueSearch, the add page's); else 0.
    /// </summary>
    public static int Score(WinSearchAsk ask, IEnumerable<string?> names, IEnumerable<string?>? words = null)
    {
        var typed = ask.Words;
        var best = names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => WinSearchRank.NameScore(name!, typed)).DefaultIfEmpty(0).Max();
        if (best > 0 || words is null || typed.Count == 0 || typed.Any(word => word.Length < 3))
        {
            return best;
        }
        return CatalogueSearch.Matches(ask.Text, "", words) ? ByWords : 0;
    }

    /// <summary>
    /// The hits of <paramref name="items"/> that answer <paramref name="ask"/>, each scored by <paramref name="score"/> — and
    /// what <paramref name="more"/> adds — and made by <paramref name="hit"/>.
    /// </summary>
    public static WinSearchFound Found<T>(WinSearchAsk ask, IEnumerable<T> items, Func<T, int> score, Func<T, WinSearchHit> hit, DateTime now, CancellationToken cancel, Func<T, double>? more = null)
    {
        List<WinSearchHit> found = [];
        foreach (var item in items)
        {
            if (cancel.IsCancellationRequested)
            {
                break;
            }
            if (score(item) is var named and > 0)
            {
                var made = hit(item);
                found.Add(made with { Score = WinSearchRank.Score(made, ask.Words, now, named: named) + (more?.Invoke(item) ?? 0) });
            }
        }
        return new(found);
    }
}

/// <summary>§652: the messages Karpo's accounts synced (their inboxes, MailStore): by subject, sender, address and preview; one opens in its account's web mail.</summary>
internal sealed class WinSearchMailSource(Func<IReadOnlyList<MailItem>> messages, Func<DateTime> now) : IWinSearchSource
{
    public string Id => WinSearchSourceInfos.Mail;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) =>
        KarpoMatch.Found(ask, messages(), message => KarpoMatch.Score(ask, [message.Subject, message.From], [message.FromAddress, message.Preview]), Hit, now(), cancel);

    /// <summary>A message as a row: its subject, who sent it and when; it opens in its account's web mail, a copy its sender's address. §655 the home's too.</summary>
    internal static WinSearchHit Hit(MailItem message) =>
        new(WinSearchCategory.Email, message.Subject.Length > 0 ? message.Subject : S.CoreAccountsNoSubject, MailLinks.Web(message.Link) ?? "",
            LineRow.Join(message.From, Panels.Nls.ShortDate(DateOnly.FromDateTime(message.Received.LocalDateTime))), OptionGlyphs.Mail)
        {
            Key = "mail:" + message.Part + "/" + message.Id,
            Modified = message.Received.UtcDateTime,
            Copies = message.FromAddress,
        };

    /// <summary>§655: the home's unread mail: the newest first, <paramref name="most"/> at most.</summary>
    internal static IReadOnlyList<WinSearchHit> Unread(IEnumerable<MailItem> messages, int most) =>
        [.. messages.Where(message => message.Unread).OrderByDescending(message => message.Received).Take(most).Select(Hit)];
}

/// <summary>
/// §652: the events of Karpo's calendars (CalendarStore), from a month back to a year ahead: by title and place; one opens
/// where a click on it in a calendar line opens (EventLinks).
/// </summary>
/// <param name="events">The events between two wall-clock times.</param>
/// <param name="target">Where an event listed under its day opens.</param>
/// <param name="now">The wall-clock time now.</param>
internal sealed class WinSearchCalendarSource(Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>> events, Func<CalendarEvent, DateOnly, string?> target, Func<DateTime> now) : IWinSearchSource
{
    public string Id => WinSearchSourceInfos.Calendar;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel)
    {
        var at = now();
        var today = DateOnly.FromDateTime(at);
        return KarpoMatch.Found(ask, events(at.Date.AddDays(-31), at.Date.AddDays(366)), item => KarpoMatch.Score(ask, [item.Title], [item.Location]),
            item => Hit(item, today, target), default, cancel, item => Soon(item.Start - at));
    }

    /// <summary>An event as a row: its title, its day, its time and its place; it opens where a calendar line's click opens it. §655 the home's too.</summary>
    internal static WinSearchHit Hit(CalendarEvent item, DateOnly today, Func<CalendarEvent, DateOnly, string?> target)
    {
        var day = DateOnly.FromDateTime(item.Start);
        return new WinSearchHit(WinSearchCategory.Events, item.Title, target(item, day) ?? "",
            LineRow.Join(CalendarText.DayName(day, today), CalendarText.Span(item, day), item.Location), WinSearchWords.Calendar)
        {
            Key = "event:" + item.Source?.Id + "\n" + item.Start.Ticks + "\n" + item.Title,
        };
    }

    /// <summary>How far ahead the home's "Coming up" looks.</summary>
    internal static readonly TimeSpan Ahead = TimeSpan.FromDays(14);

    /// <summary>§655: the home's events coming up: those not over yet within <see cref="Ahead"/>, the soonest first, <paramref name="most"/> at most.</summary>
    internal static IReadOnlyList<WinSearchHit> Coming(Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>> events, Func<CalendarEvent, DateOnly, string?> target, DateTime now, int most) =>
        [.. events(now.Date, now.Add(Ahead)).Where(item => item.End > now).OrderBy(item => item.Start).Take(most).Select(item => Hit(item, DateOnly.FromDateTime(now), target))];

    /// <summary>
    /// What an event's nearness adds, so of events named alike the one meant comes first: 20 still to come within a week, 10
    /// within a month, 5 gone within a week.
    /// </summary>
    internal static double Soon(TimeSpan away) =>
        away >= TimeSpan.Zero ? (away <= TimeSpan.FromDays(7) ? 20 : away <= TimeSpan.FromDays(31) ? 10 : 0)
        : away >= TimeSpan.FromDays(-7) ? 5 : 0;
}

/// <summary>
/// §652: the people of Karpo's address books (ContactStore), matched as the contacts' line matches them (ContactSearch); one
/// opens a mail to them (mailto:), else a call (tel:); a copy is their address or number.
/// </summary>
internal sealed class WinSearchContactsSource(Func<IReadOnlyList<Person>> people) : IWinSearchSource
{
    public string Id => WinSearchSourceInfos.Contacts;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) =>
        KarpoMatch.Found(ask, people(), person => ContactSearch.Score(person, ask.Text),
            person => new WinSearchHit(WinSearchCategory.People, person.DisplayName,
                ContactActs.MailUri(person.Email) ?? (person.Phone is { } phone ? ContactActs.TelUri(phone) : null) ?? "",
                person.Detail, OptionGlyphs.Contact)
            {
                Key = "person:" + person.Book + "/" + person.Id,
                Copies = person.Email ?? person.Phone,
                // §661: their picture, the one the contacts line wears (ContactPhotos' memory: nothing is fetched for it).
                Icon = ContactPhotos.Address(person),
            },
            default, cancel);
}

/// <summary>§652: Karpo's notes (Notebook): by title, then by what they say; one opens the folder showing it. A copy is its text.</summary>
internal sealed class WinSearchNotesSource(Func<IReadOnlyList<Note>> notes) : IWinSearchSource
{
    /// <summary>The most of a note's first line said under its name.</summary>
    private const int MostSaid = 80;

    public string Id => WinSearchSourceInfos.Notes;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) =>
        KarpoMatch.Found(ask, notes(), note => KarpoMatch.Score(ask, [note.Title], [note.Body]), Hit, default, cancel);

    /// <summary>A note as a row: its title and its first line; it opens the folder showing it, a copy its text. §655 the home's too.</summary>
    internal static WinSearchHit Hit(Note note) =>
        new(WinSearchCategory.Notes, note.Title, KarpoTargets.Note(note.Id), FirstLine(note.Body), OptionGlyphs.Note)
        {
            Copies = note.Body.Length > 0 ? note.Body : null,
        };

    /// <summary>§655: the home's notes: the ones changed last first, <paramref name="most"/> at most.</summary>
    internal static IReadOnlyList<WinSearchHit> Recent(IEnumerable<Note> notes, int most) =>
        [.. notes.OrderByDescending(note => note.Modified).Take(most).Select(Hit)];

    /// <summary>A note's first line with words, its Markdown marks off its start, held short; null for none.</summary>
    internal static string? FirstLine(string body)
    {
        foreach (var raw in body.Split('\n'))
        {
            var line = raw.Trim().TrimStart('#', '-', '*', '>', ' ', '[', ']', 'x').Trim();
            if (line.Length > 0)
            {
                return line.Length <= MostSaid ? line : line[..MostSaid].TrimEnd() + "…";
            }
        }
        return null;
    }
}

/// <summary>§652: one thing of Karpo's itself a search finds: a page or a row of its settings, a widget to add, a layout.</summary>
/// <param name="Name">What it is called.</param>
/// <param name="Detail">The line under its name: where it is, what it does.</param>
/// <param name="Glyph">Its glyph.</param>
/// <param name="Target">What opens it (<see cref="KarpoTargets"/>).</param>
/// <param name="Words">What else it is found by (a widget's description and search words); none for its name alone.</param>
internal sealed record KarpoFind(string Name, string? Detail, string Glyph, string Target, IReadOnlyList<string> Words);

/// <summary>
/// §652: Karpo itself: its settings' pages and rows, the widgets to add, the layouts — read on the dock's thread as the line
/// shows (<see cref="IWinSearchSource.Warm"/>: the config and the menus are the dock's), matched off it.
/// </summary>
internal sealed class WinSearchKarpoSource(Func<IReadOnlyList<KarpoFind>> read) : IWinSearchSource
{
    private IReadOnlyList<KarpoFind> known = [];

    public string Id => WinSearchSourceInfos.Karpo;

    public void Warm(Action came)
    {
        try
        {
            Volatile.Write(ref known, read());
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Report("Windows search: Karpo's own", error); // not-ui: a trouble's name
        }
    }

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) =>
        KarpoMatch.Found(ask, Volatile.Read(ref known), find => KarpoMatch.Score(ask, [find.Name], find.Words),
            find => new WinSearchHit(WinSearchCategory.Karpo, find.Name, find.Target, find.Detail, find.Glyph),
            default, cancel);

    /// <summary>
    /// What of Karpo a search finds in <paramref name="config"/>: the pages of its settings, the rows of its General page (each
    /// landing on its level where it has one), every widget there is to add, each layout.
    /// </summary>
    public static IReadOnlyList<KarpoFind> Of(Config config)
    {
        List<KarpoFind> found = [];
        var settings = WinSearchWords.KarpoSettings;
        foreach (var page in new[] { PanelPage.Karpo, PanelPage.Accounts, PanelPage.Layouts, PanelPage.Taskbar, PanelPage.Appearance, PanelPage.Add, PanelPage.AddLayout })
        {
            found.Add(new(PanelRows.SideOf(page), settings, PanelRows.GlyphOf(page), KarpoTargets.Settings(page), []));
        }
        var general = PanelRows.SideOf(PanelPage.Karpo);
        var rows = KarpoMenu.Rows(config, new KarpoServices(null, static () => { }));
        HashSet<string> named = new(StringComparer.CurrentCultureIgnoreCase);
        foreach (var row in rows.Where(row => row.Kind != OptionRowKind.Heading && row.Title.Length > 0))
        {
            // A row of a group lands on the group (keyed by its first row), a row with a level of its own on it; any other on its page.
            var level = row.Group is { } group ? rows.First(each => each.Group == group).Key : row.Kind == OptionRowKind.Level ? row.Key : null;
            if (row.Group is { } heading && named.Add(heading))
            {
                found.Add(new(heading, LineRow.Join(settings, general), row.GroupGlyph is { Length: > 0 } glyph ? glyph : PanelRows.GlyphOf(PanelPage.Karpo), KarpoTargets.Settings(PanelPage.Karpo, level), []));
            }
            if (named.Add(row.Title))
            {
                found.Add(new(row.Title, LineRow.Join(settings, general, row.Group), row.Glyph is { Length: > 0 } own ? own : PanelRows.GlyphOf(PanelPage.Karpo), KarpoTargets.Settings(PanelPage.Karpo, level), []));
            }
        }
        foreach (var entry in Catalogue.Everything().DistinctBy(entry => entry.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            found.Add(new(entry.Title, entry.Blurb, entry.Glyph, KarpoTargets.Add(entry.Title), CatalogueSearch.WordsOf(entry)));
        }
        for (var at = 0; at < config.Layouts.Count; at++)
        {
            var layout = config.Layouts[at];
            found.Add(new(layout.Name, S.DockEditLayout, layout.Icon is { Length: > 0 } icon ? icon : TaskbarLayouts.LayoutGlyph, KarpoTargets.Layout(at), []));
        }
        return found;
    }
}
