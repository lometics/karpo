// edit: the add list (docs\widgets.md §7b). Every add — the edit bar's Add (Dock.EditDeck.cs), a folder's "+" and the bar's
// own "+" — opens this one list, §576 the edit bar's add page (Dock.AddOf, PanelRows.Picks), §593 at the edit bar's top, as wide
// as it, for the place of what opened it: a "+"'s, or the edit bar's Add's own (Dock.AddAt). A widget or a group under the
// pointer, or the keys, stands where it would go as a ghost, through the bars' own layout (Into, Dock.AddGhost.cs), and its click
// puts in that very change. It replaced the settings app's catalogue, which took a second window and a round trip through the
// file for every widget.
//
// What fits is decided by where the place is (Karpo.Core\Catalogue.cs): a place on the bar offers
// the taskbar entries and the ready-made folders (Presets), a list folder the list widgets and the
// premade widgets; a row of another type is not shown at all. A row this PC can't have stands greyed
// with the reason under its name (Catalogue.WhyNot) rather than missing.
//
// It is the add tree (Karpo.Core\AddTree.cs; 2026-10-11, the person: "do it so that it makes the
// most sense to a new user"), the page's share of it: two columns under their headings, Popular and
// Browse, each subject a row opening its submenu, as Windows' "New ›" is, a folder in it another.
// A ready-made folder is a row as a widget is, in its subject, with nothing to tell them apart but
// what they are called. A row shown again (Popular's, Quick settings') picks what its home does.
//
// A search field stands at its top (§575: the add page's own, PanelHitKind.Filter), which has the keys as
// the list opens: what is typed lists what matches in place of the tree, flat, each with the folders it is in on a second
// line ("Connections › Wi-Fi") and once however often the tree shows it, the names that match first
// (Results); an empty field is the tree again. What matches is CatalogueSearch's: a widget's name,
// its description, its search words, and the folders it is in.
//
// Plain code without a window, so the tests read exactly what the list shows and what a pick does.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// Where an add lands: <paramref name="Slot"/> — a section and an index in the bar's list of
/// <paramref name="Owner"/> (a display's, <see cref="Config.DisplayOwner"/>), counted as <see cref="Layout.DropSlot"/> counts —, or into the folder
/// <paramref name="FolderId"/>. §593 always a place, a pick going in there at once (§297): a "+"'s, or the edit bar's Add's
/// (Dock.AddAt) — §575 to §592 the Add's knew none, and its pick followed the pointer.
/// </summary>
/// <param name="Cell">folders: the place of the "+" that was pressed — between two lines, a list's end or a new list; null for the end of the last list.</param>
/// <param name="To">
/// places: the same place as the list itself counts it, where the window's parts are not the list's
/// own (<see cref="IslandEdits.ToList"/>): its section and its index in the list; null where they are.
/// <paramref name="Slot"/> stays the window's, for the line it draws there.
/// </param>
public sealed record AddSpot(string Owner, BarSlot Slot, string? FolderId = null, FolderPlace? Cell = null, BarSlot? To = null)
{
    /// <summary>
    /// folders: the add list of folder <paramref name="folderId"/> for <paramref name="at"/> — a column's "+" (its end away
    /// from the bar), a slot's (a column of its own there), the folder's own place to add (<see cref="FolderFlyout.AddPlace"/>);
    /// null for the end of its last list.
    /// </summary>
    public static AddSpot InFolder(string owner, string folderId, FolderPlace? at) => new(owner, default, folderId, at);
}

/// <summary>
/// §593: a pick of the add page put in at its place in a copy of the config (<see cref="AddList.Into"/>): the config with it in
/// (<paramref name="Config"/>) and what went in by its key (<paramref name="Key"/>; a ready-made group's, its folder), made from
/// <paramref name="Base"/>, untouched, for the pick's command <paramref name="Id"/>. What the bars and an open folder show while
/// the pick is under the pointer, and what its click makes the config: one change, so the ghost is what the click leaves.
/// §617 with no pick (<paramref name="Id"/> 0, <see cref="AddList.Empty"/>): the empty ghost, the place's room alone.
/// </summary>
public sealed record AddGhost(Config Base, uint Id, Config Config, string Key)
{
    /// <summary>
    /// §643: what the bars and the folders show of it: <see cref="Config"/> with the pick's settings filled in where they are empty
    /// (<see cref="Samples.Dress"/>: a drive, a folder, a pin's target, a camera), in a copy of its own; what the click puts in
    /// is <see cref="Config"/>, without them.
    /// </summary>
    public Config Shown { get; init; } = Config;

    /// <summary>§617: the empty ghost, no pick's: what stands where the page adds while no tile is under the pointer.</summary>
    public bool Empty => Id == 0;
}

public static class AddList
{
    /// <summary>What a "+" is called, to a screen reader and in its tooltip, where it has no section to name.</summary>
    public static string Here => S.DockAddHere;

    /// <summary>
    /// What the "+" of <paramref name="section"/>'s edit bar is called, to a screen reader and in its
    /// tooltip: "Add a widget to the left", "… to the middle", "… to the right" — the top and the
    /// bottom on a bar standing on a side (<paramref name="vertical"/>) — so the three are told apart.
    /// </summary>
    public static string HereIn(BarSection section, bool vertical) => section switch
    {
        BarSection.Middle => S.DockAddMiddle,
        BarSection.End => vertical ? S.DockAddBottom : S.DockAddRight,
        _ => vertical ? S.DockAddTop : S.DockAddLeft,
    };

    /// <summary>
    /// Which library <paramref name="place"/> takes from: the taskbar entries on the bar, a folder's
    /// own type in a folder. Null for a folder <paramref name="list"/> no longer has.
    /// </summary>
    public static WidgetType? TypeOf(AddSpot place, IReadOnlyList<BarItem> list) =>
        place.FolderId is not { } id ? WidgetType.Bar
        : Holder(list, id) is { } holder ? Catalogue.TypeOf(Folders.View(holder.Kind))
        : null;

    /// <summary>
    /// The list's rows for a place of <paramref name="type"/>: the page's share of the add tree
    /// (<see cref="AddTree.On"/>), each column's heading and its rows, a folder a row with its submenu
    /// (<see cref="FlyoutItem.Children"/>). Every widget and premade widget the page has is in
    /// <c>Entries</c> once, and every preset in <c>Presets</c>, in the tree's order: the row of
    /// <c>Entries[n]</c> has the command <paramref name="firstId"/> + n, the row of <c>Presets[n]</c>
    /// the command after the last entry's + n, and a row shown again (Popular's, Quick settings') its
    /// home's. A folder's row has a command past those, which picks nothing. A widget that cannot go
    /// there stands greyed in its place, the reason on the row as its detail
    /// (<see cref="Catalogue.WhyNot"/>), which the list shows as the row's second line, rather than
    /// missing: a row that is not there leaves the person wondering whether it exists.
    /// <para>
    /// A row's <see cref="FlyoutItem.Words"/> are what the search finds it by; a row shown again has
    /// none, so the search lists each once (<see cref="Results"/>). A folder's are its English name.
    /// </para>
    /// <para>
    /// §586: with <paramref name="placed"/> (the layout's, <see cref="AddCounts.Placed"/>) each row says how many of it the
    /// layout has, a folder its widgets' sum (<see cref="FlyoutItem.Placed"/>, <see cref="AddCounts.Of"/>).
    /// </para>
    /// </summary>
    /// <param name="turns">Whether this PC's screen turns, for rotation lock.</param>
    public static (List<FlyoutItem> Items, List<CatalogueEntry> Entries, List<Preset> Presets) Rows(WidgetType type, bool turns, uint firstId, IReadOnlyDictionary<string, int>? placed = null)
    {
        var sections = AddTree.On(type);
        var homes = sections.SelectMany(section => AddTree.Picks(section.Rows)).Where(pick => !pick.Again).ToList();
        var entries = homes.Where(pick => pick.Entry is not null).Select(pick => pick.Entry!).ToList();
        var presets = homes.Where(pick => pick.Preset is not null).Select(pick => pick.Preset!).ToList();
        var ids = new Dictionary<string, uint>(StringComparer.Ordinal);
        foreach (var pick in homes)
        {
            ids[pick.Key] = pick.Entry is { } entry
                ? firstId + (uint)entries.IndexOf(entry)
                : firstId + (uint)(entries.Count + presets.IndexOf(pick.Preset!));
        }
        var folders = firstId + (uint)(entries.Count + presets.Count);

        FlyoutItem Item(AddRow row) => Made(row) with { Placed = placed is null ? 0 : AddCounts.Of(row, placed) };

        FlyoutItem Made(AddRow row) => row switch
        {
            AddPick { Entry: { } entry } pick => Row(entry, ids[pick.Key], turns) with { Words = pick.Again ? null : CatalogueSearch.WordsOf(entry), Badge = BadgeOf(pick) },
            AddPick { Preset: { } preset } pick => new FlyoutItem(ids[pick.Key], preset.Title, new FlyoutIcon.Glyph(preset.Glyph), Words: pick.Again ? null : Presets.WordsOf(preset)) { Badge = BadgeOf(pick) },
            AddFolder folder => new FlyoutItem(folders++, folder.Shelf.Title, new FlyoutIcon.Glyph(folder.Shelf.Glyph), Children: [.. folder.Rows.Select(Item)], Words: [.. folder.Shelf.EnglishWords]),
            _ => new FlyoutItem(0, "", Separator: true),
        };

        var items = new List<FlyoutItem>();
        foreach (var section in sections)
        {
            items.Add(new FlyoutItem(0, section.Shelf.Title, Header: true));
            items.AddRange(section.Rows.Select(Item));
        }
        return (items, entries, presets);
    }

    /// <summary>
    /// §641: a pick's badge on its tile: "Beta" for a widget marked so (<see cref="WidgetInfo.Beta"/>) and a ready-made group
    /// holding one (<see cref="Preset.Beta"/>); none for the rest.
    /// </summary>
    internal static string? BadgeOf(AddPick pick) =>
        pick.Preset?.Beta == true || (pick.Entry is { } entry && WidgetInfos.Find(entry.Id)?.Beta == true) ? S.DockAddBeta : null;

    /// <summary>One widget's row: its icon and name, greyed with the reason where it can't go here (<see cref="Catalogue.WhyNot"/>).</summary>
    private static FlyoutItem Row(CatalogueEntry row, uint id, bool turns)
    {
        var why = Catalogue.WhyNot(row, turns);
        return new FlyoutItem(id, row.Title, new FlyoutIcon.Glyph(row.Glyph), Detail: why, Enabled: why is null);
    }

    /// <summary>
    /// What can be picked of <paramref name="items"/>, in order: each row, and where a row opens a
    /// submenu (a folder), the submenu's rows instead of it — a row shown again as often as it is.
    /// </summary>
    public static IEnumerable<FlyoutItem> Picks(IEnumerable<FlyoutItem> items)
    {
        foreach (var item in items)
        {
            if (item.Header || item.Separator)
            {
                continue;
            }
            if (item.Children is { } children)
            {
                foreach (var child in Picks(children))
                {
                    yield return child;
                }
                continue;
            }
            yield return item;
        }
    }

    /// <summary>The line between the folders of a search result's place: "Connections › Wi-Fi".</summary>
    public const string PathSeparator = " › ";

    /// <summary>
    /// What the search lists for <paramref name="query"/> in place of the tree <paramref name="items"/>
    /// (<see cref="Rows"/>): every row that matches, flat and once — the rows the tree shows again
    /// (no <see cref="FlyoutItem.Words"/>) are their homes' — each with where it is in the tree as
    /// its second line, the folders it is in ("Connections › Wi-Fi") or, at the top, its column's
    /// heading; a greyed row's reason after it. A row matches by its name, its words
    /// (CatalogueSearch) and the names of the folders it is in, so "connections" lists all of them;
    /// the rows whose name is what was typed come first, then those whose name has it all, then
    /// those found by their words, then by their folders, each in the tree's order. Empty for a
    /// query that says nothing.
    /// </summary>
    public static List<FlyoutItem> Results(IReadOnlyList<FlyoutItem> items, string? query)
    {
        var wanted = CatalogueSearch.Words(query);
        var found = new List<(FlyoutItem Item, int Rank)>();
        if (wanted.Length == 0)
        {
            return [];
        }
        var exact = string.Join(' ', wanted);
        string heading = "";
        foreach (var item in items)
        {
            if (item.Header)
            {
                heading = item.Text;
                continue;
            }
            Visit(item, []);
        }
        return [.. found.OrderBy(each => each.Rank).Select(each => each.Item)];

        void Visit(FlyoutItem item, List<FlyoutItem> path)
        {
            if (item.Separator)
            {
                return;
            }
            if (item.Children is { } children)
            {
                List<FlyoutItem> deeper = [.. path, item];
                foreach (var child in children)
                {
                    Visit(child, deeper);
                }
                return;
            }
            if (item.Words is not { } words)
            {
                return;
            }
            var folders = path.SelectMany(folder => (IEnumerable<string>)[folder.Text, .. folder.Words ?? []]).ToList();
            int rank = string.Equals(CatalogueSearch.Fold(item.Text.Trim()), exact, StringComparison.Ordinal) ? 0
                : CatalogueSearch.Matches(query, item.Text, null) ? 1
                : CatalogueSearch.Matches(query, item.Text, words) ? 2
                : CatalogueSearch.Matches(query, item.Text, [.. words, .. folders]) ? 3
                : -1;
            if (rank < 0)
            {
                return;
            }
            var where = path.Count > 0 ? string.Join(PathSeparator, path.Select(folder => folder.Text)) : heading;
            found.Add((item with { Detail = item.Detail is { Length: > 0 } why ? $"{where} · {why}" : where }, rank));
        }
    }

    /// <summary>
    /// The widget Enter adds from the search field: the one result (<see cref="Results"/>), where it
    /// can be added here; null where more match, or none, or the one that does is greyed.
    /// </summary>
    public static uint? Single(IReadOnlyList<FlyoutItem> results) =>
        results is [{ Enabled: true } only] ? only.Id : null;

    /// <summary>
    /// Adds <paramref name="row"/> to <paramref name="list"/> at <paramref name="place"/>: a taskbar entry
    /// at its section and index in the bar's list (<see cref="IslandEdits.Add"/>), a folder's widget at the place of
    /// its "+" or else at the end of its last list (<see cref="Folders.AddNew"/>, which the settings app's
    /// test hooks add with too). The new widget's path ("&lt;id&gt;" on the bar, "&lt;folder&gt;/&lt;id&gt;"
    /// in a folder), or null where nothing was added: a row of another type than the place's, or a
    /// folder that has gone.
    /// </summary>
    public static string? Add(List<BarItem> list, AddSpot place, CatalogueEntry row)
    {
        if (TypeOf(place, list) is not { } type || row.Type != type)
        {
            return null;
        }
        if (place.FolderId is { } id)
        {
            return Holder(list, id) is not { } holder ? null
                // A premade widget: an ordinary line of its widget, set up as it says.
                : row.Premade is { } premade ? Folders.AddNew(list, holder, premade.New(), place.Cell)
                : Folders.AddNew(list, holder, row.Id, place.Cell);
        }
        return row.Bar is { } kind && IslandEdits.Add(list, kind, place.To ?? place.Slot) is { } added ? added.Id : null;
    }

    /// <summary>
    /// Adds the ready-made group <paramref name="preset"/> to <paramref name="list"/> at
    /// <paramref name="place"/>: its one folder, with its widgets inside, in the section of the "+"
    /// at its index (<see cref="Presets.AddToBar"/>), the folder and every widget in it with an id no
    /// other has. <paramref name="machine"/> is this PC: its battery and its drives. <paramref name="edge"/> is the edge the
    /// bar stands on, which arranges the folder (§620: its lists one under the other down a side bar). The paths of
    /// what was added — the folder's id alone — and empty where nothing was: a place that offers no
    /// ready-made folder, which is every folder (<see cref="Presets.On"/>).
    /// </summary>
    public static List<string> Add(List<BarItem> list, AddSpot place, Preset preset, PresetMachine machine, Edge edge = Edge.Bottom)
    {
        if (place.FolderId is not null || TypeOf(place, list) is not { } type || !Presets.On(type).Contains(preset))
        {
            return [];
        }
        return [Presets.AddToBar(list, place.To ?? place.Slot, preset, machine, edge)];
    }

    /// <summary>
    /// §593: <paramref name="config"/> with the pick of command <paramref name="id"/> put in at <paramref name="place"/> — a
    /// widget of <paramref name="library"/> (its first <paramref name="firstId"/>), else a ready-made group of
    /// <paramref name="presets"/>, counted on from the widgets', its widgets read of this PC (<paramref name="machine"/>, asked
    /// for one only), arranged for the edge the place's display has its bar on (§620) — in a copy: the config itself untouched,
    /// nothing saved. Null where nothing goes in (another type's row, a folder gone, a command of neither).
    /// </summary>
    public static AddGhost? Into(Config config, AddSpot place, uint id, uint firstId, IReadOnlyList<CatalogueEntry> library, IReadOnlyList<Preset> presets, Func<PresetMachine> machine)
    {
        if (id < firstId || id - firstId >= (uint)(library.Count + presets.Count))
        {
            return null;
        }
        var at = (int)(id - firstId);
        var next = config.Clone();
        var list = next.ListFor(place.Owner);
        var path = at < library.Count ? Add(list, place, library[at])
            : Add(list, place, presets[at - library.Count], machine(), next.ScreenOf(place.Owner)?.Edge ?? Edge.Bottom) is [var folder, ..] ? folder : null;
        if (path is null)
        {
            return null;
        }
        // §643: shown set up, put in as it is.
        var shown = next.Clone();
        Samples.Dress(Folders.Find(shown.ListFor(place.Owner), path));
        return new AddGhost(config, id, next, Config.IslandKey(place.Owner, path)) { Shown = shown };
    }

    /// <summary>
    /// §617: the empty ghost — what stands where the add page adds from the moment it opens while no tile is under the pointer:
    /// <paramref name="config"/> with a room the size of one standard widget put in at <paramref name="place"/> — one cell on a
    /// bar, one line in a folder — in a copy, as <see cref="Into"/> puts a pick (the room is task view's, a single button on
    /// either; the dock shows none of its drawing, only the ghost's outline round it). Null where nothing goes in (a folder gone).
    /// </summary>
    public static AddGhost? Empty(Config config, AddSpot place)
    {
        var next = config.Clone();
        var list = next.ListFor(place.Owner);
        var path = TypeOf(place, list) is { } type && Catalogue.On(type).FirstOrDefault(row => row.Id == WidgetInfos.TaskView.Id) is { } room
            ? Add(list, place, room)
            : null;
        return path is null ? null : new AddGhost(config, 0, next, Config.IslandKey(place.Owner, path));
    }

    /// <summary>
    /// Which of the widgets a ready-made group just added (<paramref name="paths"/>) has its settings
    /// opened, as every widget added from the list has: the first that needs setting up before it
    /// shows anything (<see cref="WidgetSetup.Needs"/>, the weather of the widgets board with no place
    /// yet), looking inside the folder that was added; else the first thing added — the folder, whose
    /// page is where its widgets are. Null where nothing was added.
    /// </summary>
    public static string? SettingsPath(IReadOnlyList<BarItem> list, IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (Folders.Find(list, path) is not { } added)
            {
                continue;
            }
            if (WidgetSetup.Needs(added) is not null)
            {
                return path;
            }
            foreach (var cell in Folders.ReadingOrder((added as BarItem)?.Folder ?? new FolderOptions()))
            {
                if (WidgetSetup.Needs(cell.Island) is not null)
                {
                    return Folders.Path(path, cell.Island.Id);
                }
            }
        }
        return paths.Count > 0 ? paths[0] : null;
    }

    /// <summary>The folder <paramref name="id"/> of the bar, as the island that holds it; null where it is none.</summary>
    private static BarItem? Holder(IReadOnlyList<BarItem> list, string id) =>
        Folders.Find(list, id) is BarItem { Folder: not null } holder && holder.Kind.IsFolder() ? holder : null;
}
