// edit, §529: a widget's page of the settings panel (docs\widgets.md §529; the person, 2026-11-07, of the flow study:
// "header of the 3rd one, but the main body content of the second one"). Its rows are its menu's, as every page's
// (PanelRows.Of: the same keys, so a popup, a search and a link find them), under its header card.
//
// §645 (the person, 2026-11-09: "the widget settings should have normal sections like the other settings pages not just the
// expanding sections. for all widgets"): its rows in plain sections, as the General, Taskbar and Appearance pages have them —
// a heading, then each row a card of its own, always open; nothing to fold at the page's top (§529 to §644 each a group, one
// card under a head saying what it was set to, that could be shut):
//
//   What it shows   the widget's own rows
//   Size            a folder line's Height (its Max rows under it), or a sized line's Size
//
// A level of its menu (Show from, a timer's Length, an alarm's Time and Repeat on) stays an expander in What it shows, shut:
// the second level the person kept (§594). §594 (the person, 2026-11-09: "the tray icons have two levels in their
// configuration so you have to actually expand to configure"): a level that is all What it shows holds (the tray's icons, the
// status icons) is the section itself, its heading the level's name, its rows on the page. §655: so is each of several
// where they are all What it shows holds — levels, and lists put in order (OptionRow.Ordered) under their group's name —:
// Windows search's Search in, When nothing is typed and How many each shows, three plain sections, nothing folded.
//
// §567: no Look: a widget, a folder and a line have no look of their own; Karpo's one Appearance is theirs.
//
// A folder's page (FolderPage), §563 in two sections under their headings as Windows' Settings pages are:
//
//   Faces           §552 each of its faces a group of its own, its Remove at its head's end; Add a face under them
//   Folder          its panel's rows (Alignment), each a card of its own (§588, kept by §594)
//
// A face's group is the one group left (PanelRowKind.Group): one of a list of the person's things, each with its settings —
// as the Layouts page's layouts and the Accounts page's accounts are expanders. It stands open unless shut (its key marked,
// PanelGroups.Mark: PanelMemory keeps the shut ones), its head saying what it shows where its kind says (PanelSentences).

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>§529, §645: a widget's page in sections, a folder's its faces' groups (see the top of the file).</summary>
internal static class PanelGroups
{
    /// <summary>What a group's key holds after its page's: kept in PanelMemory while it is shut, not while open.</summary>
    public const string Mark = "~";

    /// <summary>§645: what a section heading's key holds after its page's, so it is never a row's of one name ("Widget/§Size", "Widget/Size").</summary>
    public const string HeadingMark = "§";

    /// <summary>Whether <paramref name="key"/> is a group's: open unless the memory holds it.</summary>
    public static bool IsGroup(string key) => key.Contains("/" + Mark, StringComparison.Ordinal);

    /// <summary>What a mixed row's key holds to say its widgets' own rows are shown: kept in PanelMemory while they are.</summary>
    public const string Each = "#each";

    /// <summary>The key a mixed row at <paramref name="key"/> is kept open by, its widgets' own rows shown.</summary>
    public static string EachOf(string key) => key + Each;

    /// <summary>The group <paramref name="name"/>'s key on page <paramref name="page"/>.</summary>
    public static string KeyOf(PanelPage page, string name) => page + "/" + Mark + name;

    /// <summary>§645: the section heading <paramref name="name"/>'s key on page <paramref name="page"/>.</summary>
    public static string HeadingOf(PanelPage page, string name) => page + "/" + HeadingMark + name;

    /// <summary>
    /// <paramref name="page"/> of the widget whose menu is <paramref name="items"/> (Cut, Copy and Remove its header's
    /// buttons, the rest its rows), its rows in sections (<see cref="Sections"/>); several selected, their menu in common, a
    /// mixed row saying how they split.
    /// </summary>
    public static PanelPageView Page(PanelPage page, string title, string glyph, IReadOnlyList<FlyoutItem> items, string? side = null, PanelHeader? header = null)
    {
        var flat = PanelRows.Page(page, title, glyph, items, side);
        return flat with { Rows = Sections(page, flat.Rows), Header = header };
    }

    /// <summary>
    /// §529: a folder's page (the person, 2026-11-07: "the folder face settings should go into the folder settings where you
    /// can set the folder alignment"): <paramref name="items"/> its buttons, its face's side (its faces, each its settings
    /// and its Remove; Add a face) and its panel's side (Alignment, …) as one menu; its header's. §552 (the person,
    /// 2026-11-07: "the folder faces should have their own sections"): each face a group of its own in the faces' order
    /// (<see cref="FaceOf"/>), then Add a face, a button of its own under them. §563 (the person, 2026-11-08: "the folder
    /// configuration should also get section headers"): its faces and Add a face under the heading Faces, its panel's rows
    /// under the heading Folder. §588 (the person, 2026-11-09: "the alignment is burried two levels deep although it is the
    /// only configuration there"), kept by §594: the panel's rows straight under Folder, each a card of its own — §529's
    /// group When it opens went: a section's heading names its rows, no group in it only to hold them. §567: no Look.
    /// </summary>
    public static PanelPageView FolderPage(PanelPage page, string title, string glyph, IReadOnlyList<FlyoutItem> items, PanelHeader? header = null)
    {
        var flat = PanelRows.Page(page, title, glyph, items);
        var faces = flat.Rows.Where(row => row.Item.Setting == OptionMenu.FacesSetting).ToList();
        var opens = flat.Rows.Where(row => !faces.Contains(row) && row.Kind != PanelRowKind.Heading).ToList();
        return flat with
        {
            Rows = [.. Section(page, S.DockPanelSectionFaces, [.. faces.Select(row => row.Item.Reorder == OptionMenu.FaceGroup ? FaceOf(page, row) : row)]), .. Section(page, S.DockPanelSectionFolder, opens)],
            Header = header,
        };
    }

    /// <summary>
    /// §645: <paramref name="rows"/> (a widget's page's, as PanelRows reads them) in plain sections, each its heading and its
    /// rows, every row a card of its own on the page: its own rows under What it shows, a line's Height or Size under Size; a
    /// section with nothing in it none. §594: What it shows holding one level of the menu and nothing else (the tray's icons)
    /// is that level: its name the section's heading, its rows the section's, keyed as they were.
    /// </summary>
    public static List<PanelRow> Sections(PanelPage page, IReadOnlyList<PanelRow> rows)
    {
        var size = rows.Where(row => row.Item.Setting is LineHeightRows.HeightSetting or LineHeightRows.SizeSetting).ToList();
        var shows = rows.Where(row => !size.Contains(row) && row.Kind != PanelRowKind.Heading).ToList();
        var parts = Parts(shows) ?? [(S.DockPanelGroupShows, shows)];
        return [.. parts.SelectMany(part => Section(page, part.Name, part.Rows)), .. Section(page, S.DockEditSizeTitle, size)];
    }

    /// <summary>
    /// §594, §655: <paramref name="shows"/> as sections of their own where they are levels (expanders with nothing at their
    /// end) and lists put in order alone — each its name and its rows —; null where anything else stands among them.
    /// </summary>
    private static List<(string Name, IReadOnlyList<PanelRow> Rows)>? Parts(IReadOnlyList<PanelRow> shows)
    {
        List<(string Name, IReadOnlyList<PanelRow> Rows)> parts = [];
        for (var at = 0; at < shows.Count; at++)
        {
            var row = shows[at];
            if (row is { Kind: PanelRowKind.Expander, Tools.Count: 0 })
            {
                parts.Add((row.Text, [.. row.Rows.Where(each => each.Kind != PanelRowKind.Heading), .. row.Nested]));
                continue;
            }
            // A list put in order: its first row says its group (OptionMenu.Settings), its add row ends it.
            const string group = "group:";
            if (row.Item.Setting is { } setting && setting.StartsWith(group, StringComparison.Ordinal)
                && shows.Skip(at).FirstOrDefault(each => each.Item.Adds) is { } add)
            {
                var end = shows.ToList().IndexOf(add, at);
                parts.Add((setting[group.Length..], [.. shows.Skip(at).Take(end - at + 1)]));
                at = end;
                continue;
            }
            return null;
        }
        return parts.Count > 0 ? parts : null;
    }

    /// <summary>§563, §645: <paramref name="rows"/> under a page's section heading <paramref name="name"/>; none for none.</summary>
    private static IEnumerable<PanelRow> Section(PanelPage page, string name, IReadOnlyList<PanelRow> rows) =>
        rows.Count == 0 ? [] : rows.Prepend(new PanelRow(PanelRowKind.Heading, new FlyoutItem(0, name, Header: true), HeadingOf(page, name)));

    /// <summary>
    /// §552: a folder's face's <paramref name="row"/> (its settings nested under it, its Remove at its end) as a group of its
    /// own: its head the face's name and icon, its Remove at the head's end, its settings its rows — each keyed as it was, so
    /// a popup, a search and a link find it — and its sentence what it shows where its kind says (a clock's zone, a
    /// weather's place). Keyed as its row, marked a group's: two faces of one name (keyed apart, "Clock #2") two groups.
    /// </summary>
    public static PanelRow FaceOf(PanelPage page, PanelRow row)
    {
        var at = page + "/";
        var name = row.Key.StartsWith(at, StringComparison.Ordinal) ? row.Key[at.Length..] : row.Key;
        // §551: its head its face's row still — its move among the faces (Reorder, its grip) —, so the faces are dragged into order.
        return new PanelRow(PanelRowKind.Group, new FlyoutItem(row.Item.Id, row.Text, row.Item.Icon, Control: row.Item.Control, Reorder: row.Item.Reorder), KeyOf(page, name))
        {
            Rows = row.Nested,
            Tools = row.Tools,
            Sentence = PanelSentences.Shows(row.Nested),
        };
    }
}

/// <summary>
/// §529: what a face's group's head says it shows, as a sentence, from its rows' values — written per kind where a kind has
/// something to say (a calendar, a clock, a weather, a pin), null where it has not: the head is its name alone then, never
/// filler; and how a mixed row's widgets split. Each a string of Karpo's, in every language it speaks.
/// </summary>
internal static class PanelSentences
{
    /// <summary>The row of <paramref name="rows"/> (nested ones too) showing <paramref name="setting"/>; null for none.</summary>
    private static PanelRow? Of(IReadOnlyList<PanelRow> rows, string setting) => PanelRows.All(rows).FirstOrDefault(row => row.Item.Setting == setting);

    /// <summary>What <paramref name="row"/> says it is set to: a number box's value, a choice's, a segment's, a search's.</summary>
    public static string? Said(PanelRow? row) => row switch
    {
        null => null,
        { Kind: PanelRowKind.Stepper } => row.Stepper?.Value,
        { Kind: PanelRowKind.Toggle } => row.On ? S.WidgetSwitchOn : S.WidgetSwitchOff,
        _ => row.Value is { Length: > 0 } value ? value : null,
    };

    /// <summary>What a widget shows, by the settings its rows have: a calendar's, a clock's, a weather's, a pin's; null for any other.</summary>
    public static string? Shows(IReadOnlyList<PanelRow> rows)
    {
        var style = Of(rows, OptionRows.Style);
        var days = Said(Of(rows, OptionRows.CalendarDaysAhead));
        // A calendar: its days ahead, or a style of the calendar's (an agenda, a day, a week, a month, a year).
        if (days is not null || style?.Item.Children?.Any(kid => kid.Text == S.CoreWidgetStyleCalendarAgenda) == true)
        {
            var from = Of(rows, OptionRows.GroupKey(S.DockMenuShowFromTitle)) is { } showFrom ? Said(showFrom) is { } said && said != S.DockMenuShowFromAll ? said : S.DockPanelFromEvery : null;
            if (days is not null && (style is null || Said(style) == S.CoreWidgetStyleCalendarAgenda))
            {
                return from is null ? S.DockPanelCalendarAgendaAll(days) : S.DockPanelCalendarAgenda(days, from);
            }
            return from is null || Said(style) is not { } view ? null : S.DockPanelCalendarView(view, from);
        }
        if (Said(Of(rows, OptionRows.ClockZone)) is { } zone && Of(rows, OptionRows.ClockSeconds) is { } seconds)
        {
            return seconds.On ? S.DockPanelClockSeconds(zone) : S.DockPanelClockTime(zone);
        }
        if (Said(Of(rows, OptionRows.WeatherPlace)) is { } place)
        {
            return S.DockPanelWeather(place);
        }
        if (Said(Of(rows, OptionPickers.PinTarget)) is { } target)
        {
            return S.DockPanelPin(target);
        }
        return null;
    }

    /// <summary>§529: how a mixed row's widgets' own rows (<paramref name="theirs"/>) split by their values: "2 × Regular, 1 × Semibold".</summary>
    public static string SplitOf(IReadOnlyList<PanelRow> theirs) => Splits([.. theirs.Select(row => Said(row) ?? "")]);

    /// <summary>§529: <paramref name="values"/> (each one's) as they split: "2 × Compact, 1 × Full"; each apart, the values alone ("Documents, Downloads, example.com").</summary>
    public static string Splits(IReadOnlyList<string> values)
    {
        var splits = values.GroupBy(value => value, StringComparer.Ordinal).ToList();
        return splits.All(split => split.Count() == 1) ? string.Join(", ", values) : string.Join(", ", splits.Select(split => S.DockPanelSplit(split.Count(), split.Key)));
    }
}
