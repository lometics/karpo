// What a right-click on a bar shows, decided apart from any window so it can be
// tested rather than clicked.
//
// One split, in the user's own words: "edit taskbar should only appear when you
// are clicking outside of a widget". So a right-click on a widget shows that
// widget's actions and nothing about the bar, and a right-click on the bar shows
// the bar's — "Edit taskbar" and "Taskbar settings" among them, two rows because
// they are two acts. "Edit taskbar" turns edit mode on and opens nothing ("the
// edit mode should not automatically open the settings"); the settings app has a
// row of its own again, and in edit mode the rail has a button for it besides.
// Both of those reverse an earlier turn where one row did both jobs.
//
// And the same split, held to its letter (2026-10-04): "edit taskbar and taskbar
// settings should only appear when right clicking on the taskbar area that is not
// a widget". A widget with no actions of its own — Search, Task view, show desktop
// — never shows the bar's menu, and no widget's menu carries "Edit taskbar" even on
// a bar its widgets cover end to end (RestMenuFor). Both reverse F1/G5, where the
// first fell back to the bar's menu and the second added the row.
//
// Edit mode's menu has "Cut", "Copy" and "Remove" as a row of icon buttons on top, as Windows 11's
// context menus have Cut, Copy and Delete (the person, 2026-10-11: "they should be shown in a row
// like win11 does"): WidgetButtons, drawn by Flyout.Primary.cs, a pin's above its jump list. At rest
// a widget's menu had the row too, and its "Settings" and then its settings' rows; since 2026-10-20
// it has its own acts alone ("the right click context menu actions that are copy, remove, duplicate
// etc whatever, or change the appearance of a widget should only appear in edit mode. they should be
// removed from all other places"; §253): Dock.cs, WidgetMenu, the one place that decides. A pin's
// menu at rest is Windows' own for an app, with "Unpin from taskbar" (PinButtons, §202). A widget
// with nothing of its own has the floor's row (Dock.MenuFloor.cs): its page in Windows, or its
// folder's "Edit folder", the way into edit mode where the rest is.
//
// "Copy" was "Duplicate", and edit mode had "Copy" and "Paste" rows besides (the person, 2026-10-11:
// "when copying a widget it should go into edit mode, and then not just place it next but actually
// pin it to the mouse until i left click somewhere i want it ... pasting should actually not be an
// option"): one button now, which turns edit mode on, makes the copy and hands it to the pointer
// (Dock.EditCopy.cs), and puts it on edit mode's clipboard for a Ctrl+V elsewhere.
//
// "Remove" from a menu asks first, in Windows' own dialog, as the Delete key does: only where several
// widgets go, or a folder loses widgets (FolderDeletes.ForRemoving; the person: "if i just remove one
// widget there should not be a confirmation dialog, only when there are multiple ones", §253 — from
// 2026-10-11 a menu's "Remove" asked for every widget).
//
// The bar's own "Add", "Edit taskbar" and "Taskbar settings" are buttons of the row too (the person,
// 2026-10-11: "edit taskbar and taskbar settings should go into the row menu as well"; 2026-10-12:
// "the taskbar settings button should be a settings cog ... there should also be an add button"):
// alone in the bar's own menu's row. They stood at the far end of a widget's too until 2026-10-19 ("done
// editing and add a widget should be removed from the settings menu on the taskbar widgets"): a widget's
// row is Cut, Copy, and Remove (WidgetButtons, §220; the tray's Settings went in §261, its icons
// being its menu's rows), every menu's buttons together at the row's far end (§350). The bar's menu was
// fixed from 2026-10-12 ("taskbar menu customization should not be possible anymore") to §532, and since
// §366 has no row of buttons ("it should only have one entry that says edit instead"). §581 by default Windows 11's: the
// layouts under no heading, then Task Manager and "Taskbar settings", edit mode with its Taskbar page open
// (BarMenus.Default) — §532 the person's to change again, one for Karpo as a whole, on the settings panel's Taskbar
// page, its last section (§553).
//
// What counts as the bar rather than a widget is Layout.HitTest's answer and not
// a second opinion: Hit.Panel is the room between and around the widgets — the
// room around and between the three sections, an empty section's included —, and
// the empty end of the apps strip hit-tests to it too, having no
// item rectangle of its own. The grip band along the bar's inner edge is the bar
// too — it runs the whole length and is hit before everything under it, which is
// what keeps edit mode reachable on a bar so full that nothing else on it is
// bare. A bar spaced 0 (§533, Layout.Flush) draws no gaps between its widgets,
// so a full one really can have no other bare point, and the way in must not
// depend on there being one.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Where a right-click on a bar landed, as its menus divide it.</summary>
public enum MenuPlace
{
    /// <summary>The bar itself: the room between and around its widgets, and the grip band.</summary>
    Bar,

    /// <summary>A widget, whichever kind: its own menu, never the bar's.</summary>
    Widget,
}

/// <summary>Which menu a right-click at rest opens (<see cref="Dock.RestMenuFor"/>).</summary>
public enum RestMenu
{
    /// <summary>The bar's own menu, "Edit" among its rows.</summary>
    Bar,

    /// <summary>The widget's own actions, and nothing about the bar.</summary>
    Widget,

    /// <summary>None at all: a widget with no actions of its own.</summary>
    None,
}

public sealed unsafe partial class Dock
{
    /// <summary>
    /// Whether <paramref name="hit"/> landed on the bar or on a widget. Nothing under the pointer
    /// at all is the bar: a menu asked for by the keyboard names no item.
    /// </summary>
    internal static MenuPlace PlaceOf(Hit? hit) =>
        hit is null or { Kind: HitKind.Panel or HitKind.ResizeGrip } ? MenuPlace.Bar : MenuPlace.Widget;

    /// <summary>
    /// Which menu a right-click at rest on <paramref name="place"/> opens, where the widget there
    /// has <paramref name="ownRows"/> rows at rest — its own acts, the floor's where it has none
    /// (<see cref="Floored"/>): the bar's on the bar, the widget's own on a widget, and none where
    /// there are no rows (2026-10-04: "edit taskbar and taskbar settings should only appear when right
    /// clicking on the taskbar area that is not a widget"). The floor leaves only a widget the file has
    /// lost with none. Edit mode is not asked: there every block has the rows arranging takes.
    /// </summary>
    internal static RestMenu RestMenuFor(MenuPlace place, int ownRows) =>
        place == MenuPlace.Bar ? RestMenu.Bar : ownRows > 0 ? RestMenu.Widget : RestMenu.None;

    /// <summary>
    /// layouts: a row per named layout, the one this bar shows ticked — §561 a lone one too (the person, 2026-11-08: "in
    /// the right click taskbar menu layouts should also show even if only one layout is present"): the list of one, which
    /// says which layout the bar is in.
    /// </summary>
    internal static List<FlyoutItem> LayoutRows(IReadOnlyList<TaskbarLayout> layouts, string activeId) =>
        [.. layouts.Select((layout, index) => new FlyoutItem(
            CMD_LAYOUT + (uint)index,
            layout.Name,
            Glyph(layout.Icon is { Length: > 0 } icon ? icon : TaskbarLayouts.LayoutGlyph),
            Checked: layout.Id == activeId))];

    /// <summary>
    /// The bar's own menu as Windows 11's (§581: "the default right click menu on the taskbar should be like in windows:
    /// task manager and taskbar settings ... and the layout selector should be also there"): <paramref name="layouts"/>
    /// under no heading (§366), then Task Manager, §663 "Lock the taskbar" ticked where the bars are
    /// <paramref name="locked"/>, and Taskbar settings, the last edit mode with its Taskbar page open. Nothing here is
    /// about a widget.
    /// <para>
    /// The default menu (<see cref="BarMenus.Default"/>, drawn by Dock.BarMenu.cs); §532 the person's own is the
    /// settings panel's "Taskbar menu" (<see cref="Config.BarMenuEntries"/>).
    /// </para>
    /// </summary>
    internal static List<FlyoutItem> BarRows(IReadOnlyList<FlyoutItem> layouts, bool locked = true) =>
        BarRows(BarMenus.Default(), layouts, windowsTerminal: true, ShortcutLooks.None, locked).Items;

    /// <summary>
    /// Whether a menu opened from a bar on <paramref name="edge"/> has its <em>first</em> row
    /// nearest the pointer. A popup is placed against the bar and grows away from it
    /// (<c>FlyoutHost.Arrange</c>): a bottom bar's menu grows upward, so its last row is the one
    /// nearest; a top bar's grows downward, so its first row is. On a left or right bar the panel
    /// is centred on the anchor rather than anchored at either end, so neither end is nearer and
    /// the bottom bar's answer stands — which is also where a destructive row conventionally goes.
    /// <para>
    /// This exists because "closest to reach" is a rule about the pointer and "last" is only its
    /// answer on a bottom bar. Written as "last" it would be wrong the day somebody moves their
    /// taskbar to the top, and wrong in a way nobody would think to look for.
    /// </para>
    /// </summary>
    internal static bool NearestRowIsFirst(Edge edge) => edge == Edge.Top;

    /// <summary>
    /// An app button's menu, in three parts: the closing row, the app's own jump list, and Karpo's
    /// rows. Only the closing row and Karpo's are ours — <paramref name="jumps"/> is what Windows
    /// gave us for that app, in the order it gave it, and neither its lines nor its headings are
    /// ours to move. The rule below is about the rows Karpo writes and stops at the jump list's
    /// first heading.
    /// <para>
    /// <b>The closing row goes nearest the pointer</b>, at the user's word — "with closest i meant
    /// it should be all the way at the bottom. so that it is the closest to reach". The reason is
    /// reach, not convention, so the row follows the bar's edge
    /// (<see cref="NearestRowIsFirst"/>) rather than sitting at a fixed end. A separator stands
    /// between it and the rest either way, so it is a deliberate last stop rather than one a fast
    /// click can overshoot into.
    /// </para>
    /// <para>
    /// There is only ever one such row — "Close all windows" is the same row renamed when
    /// <paramref name="open"/> is more than one, not a second one to keep beside it — so nothing
    /// that closes anything can be left behind at the far end.
    /// </para>
    /// </summary>
    /// <param name="name">The app, as its row reads; <paramref name="icon"/> is the app's own picture.</param>
    /// <param name="pinned">Pinned already, so the row offers to take it off the bar.</param>
    /// <param name="open">How many of the app's windows are open; none, and there is nothing to close.</param>
    /// <param name="buttons">
    /// A pin widget's icon buttons while its bar is arranged (<see cref="PinButtons"/>), which the
    /// menu draws on top, above the jump list. Its "Remove" is the unpinning, so there is no "Unpin"
    /// row, and its "Settings" is the row's. Null at rest, a pin's menu then Windows' own for an app
    /// with "Unpin from taskbar", and for an open app that is no pin, which has "Pin to taskbar".
    /// </param>
    /// <param name="edge">The bar's edge, which says which end of the menu the pointer is at.</param>
    /// <param name="endTask">
    /// app clicks: Windows' own "End task" is on (EndTask.Enabled), so a running app's menu offers it
    /// beside the closing row, on the far side of it — as Windows 11 puts it just above Close window.
    /// </param>
    /// <param name="file">
    /// pins: the rows Windows has for what the pin opens — Run as administrator, Open file location, Copy
    /// as path, Properties, or a link's Copy link — under the app's own row (PinRows, Dock.WidgetMenus.cs),
    /// on the bar as in a folder.
    /// </param>
    /// <param name="inFolder">
    /// folders: the pin stands in a folder, so its row takes it out of there: "Remove from folder", not
    /// "Unpin from taskbar" (<see cref="UnpinItem"/>).
    /// </param>
    internal static List<FlyoutItem> AppRows(
        IReadOnlyList<FlyoutItem> jumps,
        string name,
        FlyoutIcon icon,
        bool pinned,
        int open,
        IReadOnlyList<FlyoutItem>? buttons,
        Edge edge,
        bool endTask = false,
        IReadOnlyList<FlyoutItem>? file = null,
        bool inFolder = false)
    {
        List<FlyoutItem> rest =
        [
            .. buttons ?? [],
            // The shell's, untouched.
            .. jumps,
            .. jumps.Count > 0 ? (FlyoutItem[])[new(0, "", Separator: true)] : [],
            new(CMD_LAUNCH, name, icon),
            .. file ?? [],
            .. buttons is not null ? []
                : pinned ? (FlyoutItem[])[UnpinItem(inFolder)]
                : [new(CMD_PIN, S.DockMenuPin, Glyph(GlyphPin))],
        ];
        if (open <= 0)
        {
            return rest;
        }
        FlyoutItem close = new(CMD_CLOSE, open == 1 ? S.DockMenuCloseWindow : S.DockMenuCloseAllWindows, Glyph(GlyphClose));
        FlyoutItem rule = new(0, "", Separator: true);
        if (endTask)
        {
            FlyoutItem end = new(CMD_END_TASK, S.DockMenuEndTask, Glyph(GlyphEndTask));
            return NearestRowIsFirst(edge) ? [close, end, rule, .. rest] : [.. rest, rule, end, close];
        }
        return NearestRowIsFirst(edge) ? [close, rule, .. rest] : [.. rest, rule, close];
    }

    /// <summary>
    /// pins: the row that takes a pin away, one for every pin's menu, a missing one's too
    /// (<see cref="BrokenRows"/>): "Unpin from taskbar" on the bar, as Windows' own, and "Remove from
    /// folder" for one <paramref name="inFolder"/>, which it takes out of the folder.
    /// </summary>
    internal static FlyoutItem UnpinItem(bool inFolder) =>
        new(CMD_UNPIN, inFolder ? S.DockMenuRemoveFromFolder : S.DockMenuUnpin, Glyph(GlyphUnpin));

    /// <summary>
    /// pins: the row of buttons a pin's menu has on top (<see cref="AppRows"/>): a pin widget's
    /// (<see cref="WidgetButtons"/>, "Cut", "Copy", "Remove") while its bar is
    /// <paramref name="arranged"/>, as every widget's menu has it then; none at rest, where its menu is
    /// Windows' own for an app — its jump list, the app, "Unpin from taskbar", closing its windows —
    /// with no row on top (the person, 2026-10-17: "for pin widgets in not edit mode you should remove
    /// the top row in the context menu"). None for an open app that is no pin.
    /// </summary>
    internal static List<FlyoutItem>? PinButtons(bool pin, bool arranged, bool copy = true) =>
        pin && arranged ? WidgetButtons(copy) : null;

    /// <summary>
    /// The desktop switcher's own actions. "This desktop" is the one you are on, which is the one
    /// the widget names: it was the button under the pointer while there was a button per desktop.
    /// What may actually be done to it is <paramref name="actions"/> — the same rule a row of the
    /// switcher's list answers from (<see cref="Switchers.ActionsFor"/>), so the two places a
    /// desktop can be renamed or closed can never offer different things. "Rename" stands only where
    /// the desktop can be renamed (the person, 2026-10-22: "if you cant rename a desktop it should also
    /// not show up as context menu action"): without the shell's call there is no way to name one at
    /// all, so a greyed row would offer nothing; a delete Windows refuses stays, greyed, saying why.
    /// In the one order every desktop menu has (the person, 2026-10-23; §308): New desktop, Task view,
    /// Rename, Delete — the Task view entry's and the desktops line's are its first rows
    /// (Dock.WidgetMenus.cs, TaskViewActs).
    /// </summary>
    internal static List<FlyoutItem> DesktopRows(Switchers.DesktopActions actions) =>
    [
        new(CMD_NEW_DESKTOP, S.DockMenuNewDesktop, Glyph(GlyphAdd)),
        new(CMD_TASK_VIEW, S.DockMenuTaskView, Glyph(GlyphTaskView)),
        .. actions.CanRename ? [new FlyoutItem(CMD_RENAME_DESKTOP, S.DockMenuRenameDesktop, Glyph(""))] : Array.Empty<FlyoutItem>(),
        new(CMD_DELETE_DESKTOP, S.DockMenuDeleteDesktop, Glyph(GlyphClose), Enabled: actions.CanDelete, Detail: actions.WhyNotDelete),
    ];

    /// <summary>
    /// folders: <paramref name="island"/>'s menu while its bar is arranged, whether it stands on the
    /// bar or in a folder — one answer keyed on the widget, because a widget in a folder is no part
    /// of any bar and a menu keyed on a bar part could never reach it: its icon buttons
    /// (<see cref="WidgetButtons"/>), which mean the same in both places — "Remove" takes it out of
    /// wherever it is, and "Copy" hands a copy to the pointer there, greyed where it can't
    /// (<paramref name="copy"/>). No "Settings": every widget's settings are in the menu itself
    /// (OptionMenu.cs), the tray's icons too since §261. No "Ungroup": nothing moves between
    /// a folder and the taskbar (Partners). Empty for a widget the config has lost, which leaves no
    /// menu rather than the bar's. No "Open" on a folder's: a click on its face opens it (§527; §481 to §526 "Open" first).
    /// </summary>
    internal static List<FlyoutItem> WidgetRowsFor(WidgetItem? island, bool copy = true) =>
        island is null ? [] : WidgetButtons(copy);

    /// <summary>
    /// What a widget's menu is read out as: its header ("Clock · Tokyo", <see cref="IslandEdits.Header"/>),
    /// or <paramref name="unknown"/>'s kind for one the file has lost. Spoken only: the menu draws no
    /// title, at rest or arranged, on the bar or in a folder — its first row is its first act (the
    /// person, 2026-10-10: "in the right click context menu for all taskbar widgets you should remove
    /// their title").
    /// </summary>
    internal static string? WidgetMenuName(WidgetItem? island, string? unknown) =>
        island is not null ? IslandEdits.Header(island) : unknown;

    /// <summary>
    /// A widget's menu at rest (§253): its own acts alone — a line's in a folder
    /// (<paramref name="inFolder"/>, <see cref="FolderMenuRows"/>), an entry's on the bar, a folder's
    /// for each of its faces (<see cref="BarMenuRows"/>), the floor's where it has none
    /// (<see cref="Floored"/>) — worked on <paramref name="widget"/>, the line or the folder as it is
    /// now. Never a button of the row nor a setting: "Cut", "Copy", "Remove" and every row that changes
    /// how the widget looks are edit mode's menu's alone (the person, 2026-10-20). None for a widget the
    /// file has lost, which so has no menu (<see cref="RestMenuFor"/>).
    /// </summary>
    internal static (List<FlyoutItem> Items, List<WidgetActRow> Acts) RestMenuRows(WidgetItem? island, bool inFolder, IIsland? widget = null, FaceMenuParts? parts = null) =>
        Floored(
            inFolder ? FolderMenuRows(island, widget)
            : island is BarItem entry ? BarMenuRows(entry, widget as FolderIsland, parts, widget)
            : ([], []),
            island);

    /// <summary>
    /// menus: a widget's icon buttons (<see cref="FlyoutItem.Primary"/>) in Explorer's order, what
    /// takes and copies first and what deletes last (§220), the three together at the row's far end
    /// (§350): "Cut", "Copy", greyed where the copy has nowhere to go (<paramref name="copy"/>,
    /// <see cref="CanDuplicate"/>), and "Remove" with Explorer's Delete glyph. Nothing of the bar's: "Add" and "Done editing" are the
    /// edit bar's, and "Edit taskbar" the bar's own menu's. Their words are their tooltips. The tray's
    /// "Settings", which opened its icons on Settings' General page, is gone: they are its menu's rows
    /// (OptionTray, §261).
    /// </summary>
    internal static List<FlyoutItem> WidgetButtons(bool copy = true) =>
    [
        new(CMD_CUT_ISLAND, S.DockMenuCut, Glyph(GlyphCut), Primary: true, Keys: MenuChord.Cut),
        new(CMD_COPY_ISLAND, S.DockMenuCopy, Glyph(GlyphCopy), Enabled: copy, Primary: true, Keys: MenuChord.Copy),
        new(CMD_REMOVE_ISLAND, S.DockMenuRemove, Glyph(GlyphDelete), Primary: true, Keys: MenuChord.Delete),
    ];

    /// <summary>menus: Segoe Fluent Icons' Delete, the glyph of Explorer's own delete button.</summary>
    private static readonly string GlyphDelete = char.ConvertFromUtf32(0xE74D);

    /// <summary>menus: Segoe Fluent Icons' Cut, the glyph of Explorer's own cut button.</summary>
    private static readonly string GlyphCut = char.ConvertFromUtf32(0xE8C6);

    /// <summary>
    /// A widget's menu while its bar is arranged: what arranging it takes, its icon buttons
    /// (<see cref="WidgetButtons"/>), as what it shows and how it behaves is on its settings page and
    /// moving it is done by dragging it.
    /// </summary>
    internal static List<FlyoutItem> WidgetRows() => WidgetButtons();

    /// <summary>
    /// menus: whether "Copy" can make a copy of the widget <paramref name="key"/> names beside it:
    /// a bar's entry always can (a bar holds as many of a kind as you like); a folder's line where its
    /// folder still takes its kind (<see cref="Folders.CanAdd"/>); a widget the file has lost can't.
    /// </summary>
    internal static bool CanDuplicate(Config config, string key)
    {
        var owner = Islands.OwnerOf(key);
        var path = Islands.IdOf(key);
        switch (config.FindIsland(owner, path))
        {
            case BarItem:
                return true;
            case CellItem cell:
                var cut = path.IndexOf('/');
                return cut > 0 && config.ListFor(owner).FirstOrDefault(entry => entry.Folder is not null
                        && string.Equals(entry.Id, path[..cut], StringComparison.OrdinalIgnoreCase)) is { } folder
                    && Folders.CanAdd(folder.Kind, cell.Kind);
            default:
                return false;
        }
    }

    /// <summary>
    /// menus: the copy "Copy" makes of the widget <paramref name="key"/> names, in <paramref name="config"/>:
    /// a copy with its options and a new id (<see cref="BarItem.Copy"/>, <see cref="CellItem.Copy"/>) —
    /// a folder's copy holds copies of its widgets, each with a new id too — put in right after it, on
    /// the bar as in its folder's list, where the pointer then carries it (Dock.EditCopy.cs). The
    /// copy's key, or null where nothing changed (<see cref="CanDuplicate"/>).
    /// </summary>
    internal static string? DuplicateIn(Config config, string key)
    {
        if (!CanDuplicate(config, key))
        {
            return null;
        }
        var owner = Islands.OwnerOf(key);
        var path = Islands.IdOf(key);
        var list = config.ListFor(owner);
        var cut = path.IndexOf('/');
        if (cut < 0)
        {
            var entry = (BarItem)config.FindIsland(owner, path)!;
            var copy = entry.Copy();
            list.Insert(list.IndexOf(entry) + 1, copy);
            return Config.IslandKey(owner, copy.Id);
        }
        var holder = list.First(entry => entry.Folder is not null && string.Equals(entry.Id, path[..cut], StringComparison.OrdinalIgnoreCase));
        if (Folders.CellOf(holder.Folder!, path[(cut + 1)..]) is not { } cell || Folders.PlaceOf(holder.Folder!, cell) is not { } at)
        {
            return null;
        }
        var line = cell.Island.Copy();
        return Folders.InsertLine(holder.Kind, holder.Folder!, line, at with { Index = at.Index + 1 }) is null
            ? null
            : Config.IslandKey(owner, Folders.Path(holder.Id, line.Id));
    }
}
