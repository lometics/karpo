// edit: one keymap for edit mode, the bar's and an open folder's alike (the person, 2026-10-12: "the
// editor for the folder view and the editor for the taskbar are different but still i want the
// overall look and feel to be the same ... keyboard shortcuts, animations, context menus, dnd
// behaviour"). The bar (Dock.EditKey) and a folder standing open arranged (FolderFlyout.Key) each
// read a key through Of and do what it says on their own widgets; what a key means is decided here
// once, so the two can't drift apart again (docs\widgets.md, "One edit mode").
//
//   Escape             a widget carried goes back (a drag, or a copy following the pointer); else, on
//                      the bar, the marching ants of what was copied go, as Excel's; else one level
//                      out: a folder closes (edit mode stays), on the bar too while one is open; else
//                      the bar leaves edit mode
//   Tab, Shift+Tab     the next or the one before, round
//   arrows             the focus to the widget beside, that way
//   Ctrl+arrows        the widget with the focus a place that way
//   Shift+arrows       the widget with the focus a size larger or smaller, as its menu sizes it (EditResize.cs,
//                      §471): Down a cell taller, Up a cell shorter; Right wider, Left narrower
//   Home, End          the focus to the first, the last
//   Space              a click on it: it is selected, its menu beside it
//   Enter              a folder opened, any other widget's menu
//   Menu key, Shift+F10  its menu, as a right click
//   Delete             it, or every selected one where it is one of them, comes out
//   Ctrl+X             it, or every selected one, onto the clipboard and out, as Ctrl+C and Delete together
//   Ctrl+C, Ctrl+V     copied; copies follow the pointer
//   Ctrl+A             every widget of the surface selected: the display's bars and places, the folder's lines
//   Ctrl+Z             the last change undone (§352, EditHistory.cs)
//   Ctrl+Y, Ctrl+Shift+Z  the change undone last made again
//
// A key held down acts once, but for the arrows and undo and redo, which step on as Windows' own do: a
// press that removes, copies, pastes, opens or selects again with every repeat is a key stuck, not a wish.
//
// A click on a widget, by the same token (Click): plain, it is selected alone and does what a click
// does; with Ctrl it goes into the selection or out of it; with Shift the run from the one last
// clicked is selected.
//
// Plain code without windows, so the tests can reach it.

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What a key does in edit mode, on the bar or in a folder standing open arranged (<see cref="EditKeys.Of"/>).</summary>
public enum EditKeyAct
{
    /// <summary>Nothing of edit mode's: the key goes on as it would.</summary>
    None,

    /// <summary>A widget carried goes back; else one level out (<see cref="EditKeys.Escape"/>).</summary>
    Escape,

    /// <summary>Tab: the focus to the next, round.</summary>
    Next,

    /// <summary>Shift+Tab: the focus to the one before, round.</summary>
    Previous,

    /// <summary>An arrow: the focus to the widget beside, that way (<see cref="EditKeyStroke.Key"/>).</summary>
    Focus,

    /// <summary>Ctrl+arrow: the widget with the focus a place that way (<see cref="EditKeyStroke.Key"/>).</summary>
    Move,

    /// <summary>Shift+arrow: the widget with the focus a size larger or smaller, as its menu sizes it (<see cref="EditResize"/>).</summary>
    Resize,

    /// <summary>Home: the focus to the first.</summary>
    First,

    /// <summary>End: the focus to the last.</summary>
    Last,

    /// <summary>Space: a click on the widget with the focus (<see cref="EditKeyPress.Click"/>).</summary>
    Click,

    /// <summary>Enter: a folder opened, any other widget's menu (<see cref="EditKeyPress.Open"/>).</summary>
    Open,

    /// <summary>The menu key, Shift+F10: its menu (<see cref="EditKeyPress.Menu"/>).</summary>
    Menu,

    /// <summary>Delete: it, or every selected widget where it is one of them.</summary>
    Remove,

    /// <summary>Ctrl+X: copied as Ctrl+C copies, and taken out, as one change.</summary>
    Cut,

    /// <summary>Ctrl+C.</summary>
    Copy,

    /// <summary>Ctrl+V.</summary>
    Paste,

    /// <summary>Ctrl+A: every widget of the surface selected.</summary>
    SelectAll,

    /// <summary>Ctrl+Z: the last change of edit mode's undone (§352).</summary>
    Undo,

    /// <summary>Ctrl+Y, Ctrl+Shift+Z: the change undone last made again (§352).</summary>
    Redo,
}

/// <summary>A key read by the keymap: what it does, and the key itself (an arrow's way).</summary>
public readonly record struct EditKeyStroke(EditKeyAct Act, int Key = 0);

/// <summary>What Escape does in edit mode, in this order (<see cref="EditKeys.Escape"/>).</summary>
public enum EditEscape
{
    /// <summary>The widget carried — dragged, or a copy following the pointer — goes back.</summary>
    Cancel,

    /// <summary>Nothing carried: the marching ants of what was copied go, as Excel's (Dock.EditCopy.cs); the clipboard keeps it.</summary>
    Uncopy,

    /// <summary>One level out: the bar leaves edit mode; a folder closes, and edit mode stays.</summary>
    Leave,

    /// <summary>On the bar with a folder open: the folder closes first, and edit mode stays; the next Escape leaves.</summary>
    CloseFolder,

    /// <summary>§520: words in the edit bar's search (§575, or in a box of its flyout): they go, and the flyout stays.</summary>
    ClearSearch,

    /// <summary>§575: a flyout of the edit bar open: it closes, and edit mode stays.</summary>
    CloseFlyout,
}

/// <summary>What a click on a widget in edit mode does to the selection, by the keys held (<see cref="EditKeys.Click"/>).</summary>
public enum EditClickPick
{
    /// <summary>It alone, and what a click does besides (<see cref="EditClicks.OnClick"/>).</summary>
    Only,

    /// <summary>Ctrl: into the selection, or out of it again; nothing else.</summary>
    Toggle,

    /// <summary>Shift: the run from the one last clicked; nothing else.</summary>
    Range,
}

/// <summary>Edit mode's one keymap; see the file's head.</summary>
public static class EditKeys
{
    /// <summary>The letters edit mode reads with Ctrl.</summary>
    private const int KeyA = 'A';
    private const int KeyC = 'C';
    private const int KeyX = 'X';
    private const int KeyV = 'V';
    private const int KeyY = 'Y';
    private const int KeyZ = 'Z';

    /// <summary>What <paramref name="key"/> does, with Ctrl (<paramref name="control"/>) and Shift held or not, held down (<paramref name="repeat"/>) or new.</summary>
    public static EditKeyStroke Of(int key, bool control, bool shift, bool repeat) => key switch
    {
        VK.VK_ESCAPE => new(EditKeyAct.Escape),
        VK.VK_TAB => new(shift ? EditKeyAct.Previous : EditKeyAct.Next),
        VK.VK_LEFT or VK.VK_RIGHT or VK.VK_UP or VK.VK_DOWN => new(control ? EditKeyAct.Move : shift ? EditKeyAct.Resize : EditKeyAct.Focus, key),
        VK.VK_HOME => new(EditKeyAct.First),
        VK.VK_END => new(EditKeyAct.Last),
        KeyZ when control => new(shift ? EditKeyAct.Redo : EditKeyAct.Undo),
        KeyY when control => new(EditKeyAct.Redo),
        _ when repeat => default,
        VK.VK_SPACE => new(EditKeyAct.Click),
        VK.VK_RETURN => new(EditKeyAct.Open),
        VK.VK_APPS => new(EditKeyAct.Menu),
        VK.VK_F10 when shift => new(EditKeyAct.Menu),
        VK.VK_DELETE => new(EditKeyAct.Remove),
        KeyX when control => new(EditKeyAct.Cut),
        KeyC when control && !shift => new(EditKeyAct.Copy),
        KeyV when control && !shift => new(EditKeyAct.Paste),
        KeyA when control => new(EditKeyAct.SelectAll),
        _ => default,
    };

    /// <summary>
    /// The selection's acts (Dock.SelectionAct): Delete, Ctrl+X, Ctrl+C, Ctrl+V, Ctrl+A, Ctrl+Z and Ctrl+Y — what the keys do to
    /// the selected widgets, whatever has the keys: a bar, a folder standing open arranged, or a menu over them, which passes
    /// them on (Flyout.PassesKey; the person, 2026-11-03: "when the context menu is open i should still be able to perform the
    /// same keyboard shortcuts like cut copy etc.").
    /// </summary>
    public static bool OfTheSelection(EditKeyAct act) =>
        act is EditKeyAct.Remove or EditKeyAct.Cut or EditKeyAct.Copy or EditKeyAct.Paste or EditKeyAct.SelectAll or EditKeyAct.Undo or EditKeyAct.Redo;

    /// <summary>
    /// Escape's step, from the inside out (§520 decision 4): a widget carried (<paramref name="carrying"/>) goes back first;
    /// then the marching ants of what was copied (<paramref name="copied"/>) go; then the edit bar's search's words
    /// (<paramref name="searching"/>), then §575 its flyout (<paramref name="flyoutOpen"/>) closes; then one level out: a
    /// folder open (<paramref name="folderOpen"/>) closes, else edit mode ends. An open dropdown or picker closes on its own
    /// Escape first.
    /// </summary>
    public static EditEscape Escape(bool carrying, bool copied = false, bool folderOpen = false, bool searching = false, bool flyoutOpen = false) =>
        carrying ? EditEscape.Cancel
        : copied ? EditEscape.Uncopy
        : searching ? EditEscape.ClearSearch
        : flyoutOpen ? EditEscape.CloseFlyout
        : folderOpen ? EditEscape.CloseFolder
        : EditEscape.Leave;

    /// <summary>What a click does to the selection with Ctrl (<paramref name="control"/>) or Shift held — Ctrl first, as Windows' lists read both.</summary>
    public static EditClickPick Click(bool control, bool shift) =>
        control ? EditClickPick.Toggle
        : shift ? EditClickPick.Range
        : EditClickPick.Only;
}
