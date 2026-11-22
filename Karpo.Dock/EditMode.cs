// What edit mode does to a bar's island list, apart from any window, so it can
// be tested: moving an island to a place in the list (a drop, a Ctrl+arrow, "Move
// to"), adding one from the add list (AddList.cs), and the choices of a block's options
// flyout (its switches, "Time zone").
//
// A place is a section and an index in the list (BarSlot), counted as
// Layout.DropSlot counts it: among the other islands, in list order, those that
// gave way for room included. The list holds the start's entries, then the
// middle's, then the end's, and a move or an add keeps it so.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

public static class IslandEdits
{
    /// <summary>
    /// Moves <paramref name="island"/> to <paramref name="slot"/>: into the slot's section, before the
    /// entry that has its index among the list's other islands, or after the last when there is
    /// none — and the list kept in its sections' order (<see cref="Sections.Put"/>), so an index
    /// outside the section's own stretch lands at its nearer end. False when it isn't in the list or
    /// nothing changed.
    /// </summary>
    public static bool Move(List<BarItem> list, BarItem island, BarSlot slot)
    {
        var from = list.IndexOf(island);
        if (from < 0)
        {
            return false;
        }
        var section = island.Section;
        list.RemoveAt(from);
        var at = Sections.Put(list, island, slot);
        return at != from || section != island.Section;
    }

    /// <summary>
    /// edit: moves <paramref name="islands"/>, several carried together (Dock.EditSelect.cs), into
    /// <paramref name="slot"/>'s section side by side in their order, the first before the entry at its
    /// index of the list without any of them. False when none is in the list or nothing changed.
    /// </summary>
    public static bool MoveGroup(List<BarItem> list, IReadOnlyList<BarItem> islands, BarSlot slot)
    {
        var moving = islands.Where(island => list.Contains(island)).Distinct().ToList();
        if (moving.Count == 0)
        {
            return false;
        }
        var before = list.Select(island => (island, Sections.Of(island))).ToList();
        list.RemoveAll(island => moving.Contains(island));
        var at = slot.Index;
        foreach (var island in moving)
        {
            at = Sections.Put(list, island, slot with { Index = at }) + 1;
        }
        return !before.SequenceEqual(list.Select(island => (island, Sections.Of(island))));
    }

    /// <summary>
    /// edit: the widgets keyed <paramref name="keys"/> (a bar's, "&lt;owner&gt;/&lt;id&gt;", each found
    /// in its own owner's list) land in <paramref name="toOwner"/>'s list at <paramref name="slot"/>, side
    /// by side in their order (<see cref="MoveGroup"/>): the one drop of edit mode, along a bar, to
    /// another surface of the display or to any surface of another display (§519). A widget of another
    /// layout leaves that layout's list for this one, itself — its id is unique across every layout, so
    /// it keeps it — with its folder's lines; two displays showing one layout share the list, so the
    /// move is along it. False where none of them is found, the owner names no list, or nothing changed.
    /// </summary>
    public static bool MoveTo(Config config, IReadOnlyList<string> keys, string? toOwner, BarSlot slot)
    {
        if (config.LayoutOf(toOwner)?.Islands is not { } target)
        {
            return false;
        }
        var islands = new List<BarItem>();
        var left = false;
        foreach (var key in keys)
        {
            var list = config.ListFor(Islands.OwnerOf(key));
            if (list.Find(island => island is not null && string.Equals(island.Id, Islands.IdOf(key), StringComparison.OrdinalIgnoreCase)) is not { } island
                || islands.Contains(island))
            {
                continue;
            }
            islands.Add(island);
            if (!ReferenceEquals(list, target))
            {
                // Into the list it goes to, anywhere: MoveGroup takes it out again and puts it at the slot.
                list.Remove(island);
                target.Add(island);
                left = true;
            }
        }
        return MoveGroup(target, islands, slot) | left;
    }

    /// <summary>
    /// edit: copies of <paramref name="clipboard"/> (widgets copied with Ctrl+C or "Copy",
    /// Dock.EditSelect.cs) into <paramref name="list"/>, side by side in their order in
    /// <paramref name="slot"/>'s section, the first before the entry at its index: each a deep
    /// copy with an id nothing in the list or its folders has (<see cref="BarItem.Copy"/>,
    /// <see cref="Folders.FreeId"/>). What was <paramref name="cut"/> is moved instead where
    /// <paramref name="everywhere"/> (every layout's widgets; the list's alone where none is given) has
    /// none of its ids, as after the cut — itself with its ids, so a running timer goes on running
    /// (<see cref="BarItem.Pasted"/>). One the list may not have another of is left out
    /// (<see cref="Sections.CanAdd"/>). The copies, in their order.
    /// </summary>
    public static List<BarItem> Paste(List<BarItem> list, IReadOnlyList<BarItem> clipboard, BarSlot slot, bool cut = false, IEnumerable<BarItem>? everywhere = null)
    {
        var added = new List<BarItem>();
        var at = Math.Clamp(slot.Index, 0, list.Count);
        var taken = Folders.Ids(everywhere ?? list);
        foreach (var copied in clipboard)
        {
            if (!Sections.CanAdd(list, copied.Kind))
            {
                continue;
            }
            var copy = copied.Pasted(cut, taken);
            copy.Id = Folders.FreeId(list, copy.Id);
            at = Sections.Put(list, copy, slot with { Index = at }) + 1;
            added.Add(copy);
        }
        return added;
    }

    /// <summary>
    /// places: a place a window's parts name (<paramref name="slot"/>, counted as
    /// <see cref="Layout.DropSlot"/> counts among <paramref name="shown"/>, the entries the window lays
    /// out in its parts' order, all but <paramref name="moving"/>), as the list itself has it: the
    /// section a widget put there joins and its index in <paramref name="list"/> without
    /// <paramref name="moving"/>, which <see cref="Move"/> and <see cref="Sections.Put"/> take.
    /// <para>
    /// A window's indexes are not the list's: a place along an edge shows only its own section. So the
    /// widget goes after the entry before the slot, into that entry's section; at a section's start,
    /// before the entry after it; and into an empty section, at <paramref name="empty"/>'s start: the
    /// slot's section on a bar or the place's own on a place (<see cref="Layout.Only"/>).
    /// </para>
    /// </summary>
    public static BarSlot ToList(IReadOnlyList<BarItem> list, IReadOnlyList<BarItem> shown, BarSlot slot, BarItem? moving, BarSection empty)
    {
        var rest = list.Where(island => !ReferenceEquals(island, moving)).ToList();
        var others = shown.Where(island => !ReferenceEquals(island, moving)).ToList();
        var section = Sections.Known(slot.Section);
        var before = others.Count(island => Sections.Known(island.Section) < section);
        var members = others.Where(island => Sections.Known(island.Section) == section).ToList();
        var at = Math.Clamp(slot.Index - before, 0, members.Count);
        int IndexOf(BarItem island) => rest.FindIndex(entry => ReferenceEquals(entry, island));
        if (at > 0 && IndexOf(members[at - 1]) is var after and >= 0)
        {
            return new BarSlot(Sections.Of(members[at - 1]), after + 1);
        }
        if (at < members.Count && IndexOf(members[at]) is var next and >= 0)
        {
            return new BarSlot(Sections.Of(members[at]), next);
        }
        var place = Sections.Known(empty);
        return new BarSlot(place, Sections.Range(rest, place).From);
    }

    /// <summary>
    /// Where <paramref name="island"/> goes one place towards the bar's start
    /// (<paramref name="step"/> &lt; 0) or end (&gt; 0), as <see cref="Move"/> takes it: past its
    /// neighbour in its section, and from a section's end into the next section — its first place
    /// going towards the end, its last place going towards the start, an empty section included —
    /// which is how a widget is carried from one section into another by the keyboard alone. Null
    /// at the bar's ends, or for an island not in the list: a Ctrl+arrow at the last place does
    /// nothing rather than wrapping.
    /// </summary>
    public static BarSlot? Step(IReadOnlyList<BarItem> list, BarItem island, int step)
    {
        var at = -1;
        for (var index = 0; index < list.Count; index++)
        {
            if (ReferenceEquals(list[index], island))
            {
                at = index;
                break;
            }
        }
        if (step == 0 || at < 0)
        {
            return null;
        }
        var section = Sections.Known(island.Section);
        var to = at + Math.Sign(step);
        if (to >= 0 && to < list.Count && Sections.Of(list[to]) == section)
        {
            return new BarSlot(section, to);
        }
        // Past its section's end: into the next section, at the end it comes in by. Counted among
        // the other islands, that place is the index it stands at now.
        var next = (int)section + Math.Sign(step);
        if (next < (int)BarSection.Start || next > (int)BarSection.End)
        {
            return null;
        }
        return new BarSlot((BarSection)next, at);
    }

    /// <summary>
    /// A new island of <paramref name="kind"/> added at <paramref name="slot"/> when one was given
    /// (the add list was opened on a place of the bar), else where its kind belongs
    /// (<see cref="Sections.Insert"/>), with an id nothing in the list has (<see cref="Put"/>). Null
    /// when the list may not have another of that kind. The edit rail's direct adds are this, at the
    /// end of the end section (Dock.EditRail.cs, AddAtEnd).
    /// </summary>
    public static BarItem? Add(List<BarItem> list, BarKind kind, BarSlot? slot = null) =>
        Sections.CanAdd(list, kind) ? Put(list, kind.New(), slot) : null;

    /// <summary>
    /// <paramref name="island"/>, new to <paramref name="list"/>, put at <paramref name="slot"/>
    /// when one was given, else where its kind belongs — and first given an id no island of the
    /// list or of its folders has (<see cref="Folders.FreeId"/>): its own where that is free, else a
    /// new one. An id is
    /// what the dock keys a widget's island by and what a folder's widget is found by, so two
    /// widgets of one id would be one of them drawn twice and the other not at all.
    /// </summary>
    internal static BarItem Put(List<BarItem> list, BarItem island, BarSlot? slot = null)
    {
        island.Id = Folders.FreeId(list, island.Id);
        if (slot is { } at)
        {
            Sections.Put(list, island, at);
        }
        else
        {
            Sections.Insert(list, island);
        }
        return island;
    }

    /// <summary>
    /// The options flyout's title: the kind's name, and what tells this island
    /// from others of its kind (a clock's label or zone, a weather island's
    /// place, what a pin stands for); a folder "Folder", having no name (§427).
    /// </summary>
    public static string Header(WidgetItem island, Func<string, string?>? known = null)
    {
        known ??= ZoneName;
        // What it is, asked of its record rather than of where it stands: the same header names
        // it on the bar's menu, a folder's menu and a list's line.
        var info = island.Info;
        var title = info?.Title ?? S.DockMenuWidget;
        return Detail(island, info, known) is { } detail ? $"{title} · {detail}" : title;
    }

    /// <summary>
    /// What <paramref name="island"/> is, and nothing else: the widget the person added, as the add list
    /// and its menu's Style and Reading rows offer it ("Clock", "Weather", "Performance", "Pin", "Folder"),
    /// the title at the start of its configuration menu's top row (Flyout.Primary.cs, §323; §314, the
    /// person: "the widget titles in the edit menus should just be the widget type, not anything else like
    /// place or so"). A line of a widget of several styles or readings is that widget's
    /// (<see cref="WidgetStyles"/>): a weather line "Weather" whatever its style, never its form's catalogue
    /// name ("Conditions", "Next hours"), a performance line "Performance" whatever it reads, never "Processor
    /// graph" (§323: "some type names read oddly"). No place, zone, label, name, reading or target, which
    /// <see cref="Header"/> adds.
    /// </summary>
    public static string Kind(WidgetItem island) =>
        WidgetStyles.Of(island) is { } styled ? styled.Widget.Title
        : island.Info?.Title ?? S.DockMenuWidget;

    /// <summary>What tells <paramref name="island"/> from others of what it is, or null where nothing does.</summary>
    private static string? Detail(WidgetItem island, WidgetInfo? info, Func<string, string?> known)
    {
        if (info == WidgetInfos.Clock)
        {
            return island.Clock is { Label.Length: > 0 } clock ? clock.Label
                : island.Clock is { TimeZone.Length: > 0 } zoned ? known(zoned.TimeZone) ?? zoned.TimeZone
                : null;
        }
        if (PlaceNames.Known(island.Weather) is { Length: > 0 } place)
        {
            return place;
        }
        if (info == WidgetInfos.Pin)
        {
            // pins: the name the bar shows under it, the target's own when it has none of its own —
            // a file's without its extension, as a file pin's always was (§308).
            return island.Pin is { Name.Length: > 0 } named ? named.Name
                : island.Pin is { Target.Length: > 0 } pin ? WithoutExtension(PinItems.For(pin).Name, pin.Target)
                : null;
        }
        // drives: which drive it shows, so two drive widgets are told apart in a list.
        if (info?.Drives is DriveFilter.Local or DriveFilter.Network && island.Drive is { } drive && drive.IsSet)
        {
            return DriveTargets.Title(drive.Label, drive.Letter);
        }
        return null;
    }

    /// <summary><paramref name="name"/> without the extension of <paramref name="target"/>, a file's; as it is for an app, a folder or an address.</summary>
    internal static string WithoutExtension(string name, string target) =>
        !Items.IsWeb(target) && Path.GetExtension(target) is { Length: > 1 } extension
            && name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && name.Length > extension.Length
            ? name[..^extension.Length]
            : name;

    /// <summary>
    /// What Windows calls zone <paramref name="id"/>, shortened to the places
    /// ("(UTC+09:00) Osaka, Sapporo, Tokyo" says "Osaka, Sapporo, Tokyo"); null
    /// for a zone it doesn't know.
    /// </summary>
    public static string? ZoneName(string id)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(id);
            var name = zone.DisplayName;
            var cut = name.IndexOf(") ", StringComparison.Ordinal);
            return cut >= 0 && cut + 2 < name.Length ? name[(cut + 2)..] : name;
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>
/// edit: where the keys stop on a bar being arranged (Tab, the arrows): every block, in their order
/// along it: the start section's, the middle's, then the end's — left to right, or top to bottom on
/// a bar standing on a side. The sections' "+" are on their edit bars (EditRail.cs), which the keys
/// reach after the blocks (Dock.EditRail.cs).
/// </summary>
public static class EditStops
{
    /// <summary>The stops of <paramref name="layout"/>: each block's part.</summary>
    public static List<int> Order(Layout layout) =>
        [.. Enumerable.Range(0, layout.Specs.Count)
            .Where(layout.Placed)
            .OrderBy(part => layout.PartAt(part).Left)];
}

/// <summary>
/// places: where a block carried in edit mode lands, apart from any window: on the surface of the
/// display nearest the pointer, wherever the pointer is (the person, 2026-10-07: "a drag anywhere on
/// the screen always has a drop location: the nearest valid slot in the nearest place").
/// </summary>
public static class EditDrops
{
    /// <summary>
    /// Which of <paramref name="areas"/> (screen px; each surface's bar and the room between it and
    /// the screen's edge) is nearest <paramref name="at"/>: the one it is in, else the one it is the
    /// least way from. Those <paramref name="open"/> says no to take nothing. A tie goes to the first,
    /// which is the surface the block was carried from. -1 when none is open.
    /// <para>
    /// §519: the surfaces are every display's; <paramref name="screens"/> gives each one's display
    /// (screen px). The display the pointer is over has it where it has a surface open — the nearest of
    /// those, however much nearer another display's is across the seam; over a display with none, or
    /// over no display, the nearest of all.
    /// </para>
    /// </summary>
    public static int Nearest(POINT at, IReadOnlyList<RECT> areas, IReadOnlyList<bool> open, IReadOnlyList<RECT>? screens = null)
    {
        bool On(int index) => screens is not null && index < screens.Count && Distance(screens[index], at) == 0;
        var here = Enumerable.Range(0, areas.Count).Any(index => index < open.Count && open[index] && On(index));
        var best = -1;
        var least = long.MaxValue;
        for (var index = 0; index < areas.Count; index++)
        {
            if (index >= open.Count || !open[index] || (here && !On(index)))
            {
                continue;
            }
            var distance = Distance(areas[index], at);
            if (distance < least)
            {
                (best, least) = (index, distance);
            }
        }
        return best;
    }

    /// <summary>How far <paramref name="at"/> is from <paramref name="area"/>, squared, in px: 0 inside it.</summary>
    public static long Distance(RECT area, POINT at)
    {
        long dx = at.x < area.left ? area.left - at.x : at.x >= area.right ? at.x - (area.right - 1) : 0;
        long dy = at.y < area.top ? area.top - at.y : at.y >= area.bottom ? at.y - (area.bottom - 1) : 0;
        return (dx * dx) + (dy * dy);
    }
}
