// edit: "Copy" in a widget's menu (the person, 2026-10-11: "when copying a widget it should go into
// edit mode, and then not just place it next but actually pin it to the mouse until i left click
// somewhere i want it." ... "pasting should actually not be an option, you basically always just
// copy and try to place immediately, but it should then be copied into the clipboard too so that if
// he decides to paste it in another layout it would work still.").
//
//  - **Edit mode on.** Arranging the bar the widget is on, and, for a widget in a folder, that folder
//    arranged where it stands and still open (BeginEditFromFolderDrag).
//  - **The copy.** Made as "Duplicate" made it (DuplicateIn): its options and new ids throughout, a
//    folder's widgets copied with it, put in right after the original — the bar's or the folder's.
//  - **On the pointer.** It follows the pointer at once, drawn as it is, with the landing shown, as a
//    widget from the add list does (Dock.EditFollow.cs): on the bar a bar's block, carried to every
//    surface of its display; in the folder a line, carried among its lists (FolderFlyout.Carry.cs).
//    A left click puts it down where the landing shows; Escape or a right click takes it out again,
//    and nothing was added.
//  - **On the clipboard.** Edit mode's own (never Windows'), as Ctrl+C puts widgets there: Ctrl+V in
//    edit mode, in this layout or another, hands a copy of it to the pointer again — a bar's widget
//    on the bar under the pointer, a folder's line in the folder standing open arranged.
//  - **Several at once** (the person, 2026-10-12: "same for copy pasting, copy pasting should also
//    work into columns that are empty"). "Copy" on one of several selected widgets copies them all —
//    the bar's together, or a folder's lines together — and they follow the pointer as one, as a
//    drag carries them (FolderFlyout.Pending.cs); Ctrl+C of several lines puts them all on the
//    clipboard, and Ctrl+V hands copies of all of them to the pointer in the open folder, where a
//    click puts them down anywhere a carried line lands: between two lines, in an empty column, in
//    an empty slot beside the lists.
//
// No "Paste" row, and no "Copy" row beside the button: one way, and Ctrl+V for the clipboard.
//
// "Cut" beside it (the person, 2026-10-19: "cut should be an additional option"; §220): edit mode on
// as for "Copy", and the widget on the clipboard and out at once, as Ctrl+X does (Dock.EditSelect.cs);
// Ctrl+V puts it down again. A cut and a paste are a move: the widget comes down with its own ids the
// first time, so a timer running in it goes on running (a paste giving it new ids reset it), and as
// a copy after that, as anything copied comes down.
//
// What was copied wears Excel's copied look while edit mode is on (the person, 2026-10-12: "the item
// that is currently in the clipboard should get a look like an excel cell does when it is being
// copied"): the marching ants round each widget it was copied from, on the bar and in a folder
// (MarchingAnts.cs). As in Excel they stay until Escape — the first Escape takes them off, the next
// leaves edit mode — or the next copy; a paste leaves them, and they go with edit mode.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>
    /// edit: the folder's lines copied last, in their order, which Ctrl+V hands to the pointer in a
    /// folder standing open arranged; none where a bar's widgets were copied last (<see cref="editClipboard"/>).
    /// </summary>
    private readonly List<CellItem> lineClipboard = [];

    /// <summary>edit: the keys of the widgets what is on the clipboard was copied from, which wear the marching ants; none once called off.</summary>
    private readonly List<string> copiedKeys = [];

    /// <summary>
    /// edit: the widgets <paramref name="keys"/> wear the marching ants from now on, and no others: on
    /// every arranged surface and in every folder standing open (see the top of the file).
    /// </summary>
    private void ShowCopied(IEnumerable<string> keys)
    {
        copiedKeys.Clear();
        copiedKeys.AddRange(keys.Distinct(StringComparer.OrdinalIgnoreCase));
        ShowCopiedNow();
    }

    /// <summary>edit: Escape with the marching ants showing takes them off, as Excel's does; false where none showed.</summary>
    private bool ClearCopied()
    {
        if (copiedKeys.Count == 0)
        {
            return false;
        }
        copiedKeys.Clear();
        ShowCopiedNow();
        return true;
    }

    /// <summary>
    /// edit: whether the marching ants show anywhere: Escape takes them off first only where they do
    /// (<see cref="EditKeys.Escape"/>). Lines copied in a folder that has closed show none — Escape
    /// spent on them did nothing to be seen, and the next was needed to leave edit mode.
    /// </summary>
    private bool CopiedInSight() =>
        AntsInSight(
            copiedKeys,
            windows.Where(Arranging).SelectMany(window => window.Layout.Specs.Select(spec => spec.Key)),
            openFolders.Where(open => open.Flyout.IsOpen).Select(open => open.Key));

    /// <summary>
    /// edit: whether any of <paramref name="copied"/> stands in sight: one of the arranged bars' parts
    /// (<paramref name="barKeys"/>), or a line of a folder standing open (<paramref name="openFolders"/>, the folders' keys).
    /// </summary>
    internal static bool AntsInSight(IReadOnlyCollection<string> copied, IEnumerable<string> barKeys, IEnumerable<string> openFolders)
    {
        if (copied.Count == 0)
        {
            return false;
        }
        var folders = openFolders.ToList();
        return barKeys.Any(key => copied.Contains(key, StringComparer.OrdinalIgnoreCase))
            || copied.Any(key => folders.Exists(folder => key.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>edit: the marching ants where <see cref="copiedKeys"/> has them, on the bars and in the folders.</summary>
    private void ShowCopiedNow()
    {
        foreach (var window in windows)
        {
            if (Arranging(window))
            {
                window.ShowEdit();
            }
        }
        foreach (var open in openFolders)
        {
            open.Flyout.Copied = [.. copiedKeys];
        }
    }

    /// <summary>edit: the parts of <paramref name="layout"/> on the clipboard, which wear the marching ants; null for none.</summary>
    private IReadOnlyCollection<int>? CopiedOn(Layout layout) => CopiedParts(copiedKeys, layout.Specs.Select(spec => spec.Key).ToList());

    /// <summary>edit: which of <paramref name="keys"/> — a bar's parts, in its order — are among <paramref name="copied"/>; null for none.</summary>
    internal static IReadOnlyCollection<int>? CopiedParts(IReadOnlyCollection<string> copied, IReadOnlyList<string> keys)
    {
        if (copied.Count == 0)
        {
            return null;
        }
        var parts = new List<int>();
        for (var part = 0; part < keys.Count; part++)
        {
            if (copied.Contains(keys[part], StringComparer.OrdinalIgnoreCase))
            {
                parts.Add(part);
            }
        }
        return parts.Count > 0 ? parts : null;
    }

    /// <summary>
    /// edit: "Copy" on the widget <paramref name="key"/> names (see the top of the file), from a menu
    /// on <paramref name="window"/>, over <paramref name="folder"/> where the widget is a folder's
    /// line. False where nothing was copied: a widget the file has lost, a kind its folder refuses
    /// more of, or something following the pointer already.
    /// </summary>
    private bool CopyToPointer(DockWindow window, string key, OpenFolder? folder)
    {
        if (follow is not null || !CanDuplicate(config, key))
        {
            return false;
        }
        var inFolder = folder is not null && Islands.IdOf(key).Contains('/');
        // edit: "Copy" on one of several selected copies them all — those beside it, the bar's or its folder's.
        var sources = CopiedWith(key, ActedOn(key)).Where(each => CanDuplicate(config, each)).ToList();
        if (inFolder ? !Arranging(folder!.Window) && !BeginEditFromFolderDrag(folder) : !Arranging(window) && !BeginEditFor(window))
        {
            return false;
        }
        var next = config.Clone();
        var copies = sources.Select(each => DuplicateIn(next, each)).OfType<string>().ToList();
        if (copies.Count == 0)
        {
            return false;
        }
        // licence: a copy on the bar is a widget added (Dock.License.cs).
        if (!LicenseAllows(next, Islands.OwnerOf(key) ?? "", window))
        {
            return true;
        }
        if (inFolder)
        {
            Clip(sources, copies.Select(copy => next.FindIsland(Islands.OwnerOf(copy), Islands.IdOf(copy))));
            // Let go over no place, a copy stays beside what it was copied from, where it went in.
            return CarryInto(folder!, next, copies, home: null);
        }
        Apply(next);
        Clip(sources, copies.Select(copy => config.FindIsland(Islands.OwnerOf(copy), Islands.IdOf(copy))));
        return FollowOnBar(copies);
    }

    /// <summary>
    /// edit: "Cut" on the widget <paramref name="key"/> names, from a menu on <paramref name="window"/>
    /// (over <paramref name="folder"/> for a folder's line): edit mode on, as "Copy" turns it on, so
    /// Ctrl+V can put it down again; then it, or every selected widget where it is one of them, cut as Ctrl+X cuts them
    /// (<see cref="SelectionAct"/>). False where nothing was cut.
    /// </summary>
    private bool CutFromMenu(DockWindow window, string key, OpenFolder? folder)
    {
        if (follow is not null || config.FindIsland(Islands.OwnerOf(key), Islands.IdOf(key)) is null)
        {
            return false;
        }
        var inFolder = folder is not null && Islands.IdOf(key).Contains('/');
        if (inFolder ? !Arranging(folder!.Window) && !BeginEditFromFolderDrag(folder) : !Arranging(window) && !BeginEditFor(window))
        {
            return false;
        }
        return SelectionAct(EditKeyAct.Cut, window, key, inFolder ? folder : null);
    }

    /// <summary>
    /// edit: what a copy of <paramref name="key"/> copies, <paramref name="acted"/> being what an act
    /// on it acts on (<see cref="ActedOn"/>): those of them where it is — the same folder's lines, or
    /// the same display's bar widgets — in their order, it among them; it alone where it is alone.
    /// </summary>
    internal static List<string> CopiedWith(string key, IEnumerable<string> acted)
    {
        var owner = Islands.OwnerOf(key);
        var path = Islands.IdOf(key);
        var cut = path.IndexOf('/');
        var prefix = cut > 0 ? path[..(cut + 1)] : null;
        bool Beside(string each) =>
            string.Equals(Islands.OwnerOf(each), owner, StringComparison.OrdinalIgnoreCase)
            && (prefix is null
                ? !Islands.IdOf(each).Contains('/')
                : Islands.IdOf(each).StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Islands.IdOf(each).IndexOf('/') == cut);
        var with = acted.Where(Beside).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return with.Contains(key, StringComparer.OrdinalIgnoreCase) ? with : [key];
    }

    /// <summary>edit: edit mode on for <paramref name="window"/>'s display; whether it is arranged now.</summary>
    private bool BeginEditFor(DockWindow window)
    {
        BeginEdit(window);
        return Arranging(window);
    }

    /// <summary>
    /// edit: <paramref name="copied"/> on edit mode's clipboard, as they are now, in their order: a
    /// bar's widgets for the bar (<see cref="editClipboard"/>), a folder's lines for a folder
    /// (<see cref="lineClipboard"/>); of a mix, the bar's alone. <paramref name="sources"/>, what they
    /// were copied from, wear the copied look (<see cref="ShowCopied"/>) — none where they are
    /// <paramref name="cut"/> and leave. What is cut is moved by the next Ctrl+V (<see cref="Cut"/>):
    /// till then the countdowns its timers and stopwatches have are carried
    /// (<see cref="Countdowns.Carry"/>), and none once something else is on the clipboard. Nothing
    /// where none was copied.
    /// </summary>
    private void Clip(IEnumerable<string> sources, IEnumerable<WidgetItem?> copied, bool cut = false)
    {
        var items = copied.OfType<WidgetItem>().ToList();
        var entries = items.OfType<BarItem>().ToList();
        var lines = items.OfType<CellItem>().ToList();
        if (entries.Count == 0 && lines.Count == 0)
        {
            return;
        }
        editClipboard.Clear();
        lineClipboard.Clear();
        if (entries.Count > 0)
        {
            editClipboard.AddRange(entries.Select(entry => entry.Clone()));
        }
        else
        {
            lineClipboard.AddRange(lines.Select(line => line.Clone()));
        }
        clipboardCut = cut;
        countdowns.Carry(cut ? [.. Folders.Ids(editClipboard), .. lineClipboard.Select(line => line.Id)] : []);
        ShowCopied(cut ? [] : sources);
    }

    /// <summary>
    /// edit: the bar's widgets <paramref name="keys"/>, just put in, are the selection and follow the
    /// pointer together, the first taken hold of (<see cref="Follow"/>). Where no surface shows them,
    /// they stay where they went in.
    /// </summary>
    private bool FollowOnBar(IReadOnlyList<string> keys)
    {
        editSelection.Set(keys, []);
        ShowSelection();
        Follow(keys[0], together: keys);
        return true;
    }

    /// <summary>
    /// edit: the folder's lines <paramref name="keys"/>, which <paramref name="next"/> puts into the
    /// folder standing open in <paramref name="open"/> — added, pasted, copied — come in on the pointer
    /// (§239): the folder is told first (<see cref="FolderFlyout.Expect"/>), so the layout the commit
    /// brings leaves them in no list and nothing of it moves; then saved, and carried there together
    /// (<see cref="FolderFlyout.Carry(IReadOnlyList{string}, FolderPlace?)"/>), the selection; the hooks
    /// hear the click that puts them down — where the landing shows, else at <paramref name="home"/> —
    /// and the one that calls them off, as for a widget on the bar. Where the folder cannot carry them,
    /// they stay where they went in.
    /// </summary>
    private bool CarryInto(OpenFolder open, Config next, IReadOnlyList<string> keys, FolderPlace? home)
    {
        var carries = openFolders.Contains(open) && open.Flyout.IsOpen;
        if (carries)
        {
            open.Flyout.Expect(keys);
        }
        Apply(next);
        EditPick(keys[0]);
        if (keys.Count > 1)
        {
            editSelection.Set(keys, []);
            ShowSelection();
        }
        if (!carries || !openFolders.Contains(open) || !open.Flyout.Carry(keys, home))
        {
            return true;
        }
        follow = new Following(keys[0], keys.Count > 1 ? keys : null) { InFolder = open };
        EditHook().Arm();
        return true;
    }

    /// <summary>
    /// edit: Ctrl+V of a folder's lines (<see cref="lineClipboard"/>): copies with new ids into the
    /// folder standing open arranged that takes their kinds, the one opened last, following the pointer
    /// there together (<see cref="CarryInto"/>); a click puts them down wherever a carried line lands —
    /// an empty column or slot too — and over no place where the folder adds
    /// (<see cref="FolderFlyout.AddPlace"/>), as a widget added there lands. Nothing where no such folder stands open.
    /// </summary>
    private bool PasteLine()
    {
        if (lineClipboard.Count == 0 || follow is not null || editor is null)
        {
            return false;
        }
        var open = openFolders.LastOrDefault(each => each.Flyout.IsOpen && Arranging(each.Window)
            && config.FindIsland(Islands.OwnerOf(each.Key), Islands.IdOf(each.Key)) is BarItem { Folder: not null } holder
            && lineClipboard.Exists(line => Folders.CanAdd(holder.Kind, line.Kind)));
        if (open is null)
        {
            return false;
        }
        var next = config.Clone();
        var pasted = lineClipboard.Select(line => PasteLineIn(next, open.Key, line, clipboardCut)).OfType<string>().ToList();
        if (pasted.Count == 0)
        {
            return false;
        }
        return CarryInto(open, next, pasted, open.Flyout.AddPlace());
    }

    /// <summary>
    /// edit: a copy of <paramref name="line"/> with new ids at the end of the first list of the folder
    /// <paramref name="folderKey"/> names in <paramref name="config"/> — the line itself, moved, where
    /// it was <paramref name="cut"/> and no widget of <paramref name="config"/> has its id
    /// (<see cref="CellItem.Pasted"/>); the copy's key, or null where there is no such folder or it
    /// takes no more of the line's kind.
    /// </summary>
    internal static string? PasteLineIn(Config config, string folderKey, CellItem line, bool cut = false)
    {
        var owner = Islands.OwnerOf(folderKey);
        if (config.FindIsland(owner, Islands.IdOf(folderKey)) is not BarItem { Folder: { } folder } target || !Folders.CanAdd(target.Kind, line.Kind))
        {
            return null;
        }
        var copy = line.Pasted(cut, Folders.Ids(config.AllIslands));
        return Folders.InsertLine(target.Kind, folder, copy, new FolderPlace(0, folder.Lists.Count > 0 ? folder.Lists[0].Count : 0)) is null
            ? null
            : Config.IslandKey(owner, Folders.Path(target.Id, copy.Id));
    }
}
