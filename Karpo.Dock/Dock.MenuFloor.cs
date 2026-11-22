// menus: no widget's menu is empty, nor its row of icon buttons alone (the person, 2026-10-11: "the
// right click context actions when only shown as the row and nothing else to show it just looks pretty
// bad in my opinion. make sure that there are also always items that are shown and not just the row").
//
// **At rest.** A widget's own rows, and nothing that copies, removes or restyles it, which are edit
// mode's (§253): a line's kind's (Dock.WidgetMenus.cs, ActsFor(ListKind)),
// an entry's or a folder's faces' (Dock.FaceMenus.cs). Where those leave no row that does anything now —
// a heading, a note, a sign-out button, a drive that is not plugged in, a to-do list with nothing done —
// the floor puts in what every widget of its place can do: a line's folder is edited (edit mode, the
// folder open in it, where the line is dragged); an entry on the bar has its page in Windows, its
// taskbar settings where it has none of its own; a folder is edited. A greyed row counts for nothing: a
// menu of one greyed row still looks empty.
//
// **Arranged.** Edit mode's menu is the buttons — a folder's "Open folder" under them is gone, a click
// on the folder opens it (§253) —, the bar's own at the row's far end with "Done editing"
// — Windows' check mark — standing where "Edit taskbar" stands at rest (the person, 2026-10-12: "it
// should be replaced by a close/done edit mode button. the individual 'done editing' context menu entry
// should then be removed"); on one of several selected widgets the buttons act on them together, and
// nothing else clears the selection here: Escape and a click on empty room do (the person, 2026-10-19:
// "select none should not be a menu action"; §240). It is edit mode's one menu: a right click opens it as a click does, and
// the room round the widgets has none (the person, 2026-10-19: "the separate right click menus in the
// edit mode should be removed"; §214).
//
// No menu moves a widget a place back or on (the person, 2026-10-12: "move left move right should not
// be a context menu action"): arranging is dragging, and Ctrl+arrow from the keyboard.

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>Whether <paramref name="items"/> has a row that does something now: neither a heading, a rule, a button of the row, nor greyed.</summary>
    internal static bool HasLiveRow(IEnumerable<FlyoutItem> items) =>
        items.Any(item => item is { Header: false, Separator: false, Primary: false, Enabled: true });

    /// <summary>
    /// A widget's own rows at rest (<paramref name="own"/>), with the floor under them where they have
    /// no live row (see the top of the file): an entry's page in Windows, else "Edit folder" — a
    /// folder's own, a line's folder's. None for a widget the file has lost.
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Acts) Floored(
        (List<FlyoutItem> Items, List<WidgetActRow> Acts) own, WidgetItem? island)
    {
        if (island is null || HasLiveRow(own.Items))
        {
            return own;
        }
        var (floor, acts) = island is BarItem { Folder: null } entry
            ? ActRows([WidgetAct.WindowsSettings], widget: null, PageOf(entry.Kind), first: own.Acts.Count)
            : ActRows([WidgetAct.EditFolder], widget: null, first: own.Acts.Count);
        List<FlyoutItem> items = [.. own.Items];
        if (items.Count > 0 && floor.Count > 0)
        {
            items.Add(new FlyoutItem(0, "", Separator: true));
        }
        items.AddRange(floor);
        return (items, [.. own.Acts, .. acts]);
    }

    /// <summary>
    /// The folder the widget keyed <paramref name="key"/> stands in — "layout:&lt;id&gt;/&lt;folder id&gt;"
    /// of a line's "layout:&lt;id&gt;/&lt;folder id&gt;/&lt;id&gt;" — or the key itself, of the bar's own.
    /// </summary>
    internal static string FolderKeyOf(string key) =>
        Islands.IdOf(key).Contains('/') ? key[..key.LastIndexOf('/')] : key;
}
