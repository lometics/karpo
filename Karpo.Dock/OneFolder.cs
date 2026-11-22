// One folder open at a time, in edit mode as out of it (the person, 2026-10-09: "only one folder can
// be open at a time"), and what closes it. At rest a folder is a popup like any other; while its bar
// is arranged it stays open through everything a person does on the bar and around it, and closes
// only on its own button clicked again, another folder opening, Escape, edit mode ending, or something
// launched (§491).
//
// A decision apart from any window, so it is tested as one; the dock and the folder do what it
// answers (Dock.Folders.cs, FolderFlyout.Show). It replaces the pair of folders edit mode once kept
// open side by side (FolderPair, 2026-09-26).

namespace Karpo.Dock;

/// <summary>What may close the open folder.</summary>
public enum FolderCloser
{
    /// <summary>Its own button on the bar, clicked again.</summary>
    OwnButton,

    /// <summary>Another folder opening: one folder is open at a time.</summary>
    OtherFolder,

    /// <summary>Escape, with the folder's window in front.</summary>
    Escape,

    /// <summary>Edit mode ending.</summary>
    EditEnds,

    /// <summary>A press anywhere outside the folder, or another window taking the focus.</summary>
    PressElsewhere,

    /// <summary>Another popup opening: a menu, a flyout, the add list.</summary>
    OtherPopup,

    /// <summary>§491: something launched — a program, a page, a setting, a file —, which comes up over nothing of Karpo's.</summary>
    Launch,
}

internal static class OneFolder
{
    /// <summary>
    /// Whether <paramref name="what"/> closes the open folder, its bar being arranged or not
    /// (<paramref name="arranging"/>). Its own button, another folder, Escape, edit mode ending and a
    /// launch (§491: edit mode itself stays) always do; a press elsewhere and another popup only at
    /// rest — while the bar is arranged the folder stays open through the drags and the presses on
    /// the bar behind it.
    /// </summary>
    public static bool Closes(FolderCloser what, bool arranging) => what switch
    {
        FolderCloser.OwnButton or FolderCloser.OtherFolder or FolderCloser.Escape or FolderCloser.EditEnds or FolderCloser.Launch => true,
        _ => !arranging,
    };

    /// <summary>
    /// Whether a folder opens, <paramref name="empty"/> or not, its bar being arranged or not
    /// (<paramref name="arranging"/>): always while it is arranged, where its empty columns are what
    /// is added into; at rest only with something in it — an empty one has nothing to show but its
    /// empty columns' "Add widgets here" (the person, 2026-10-22: "it should actually not be openable
    /// when it is empty in viewing mode, only in edit mode"; §279).
    /// </summary>
    public static bool Opens(bool empty, bool arranging) => arranging || !empty;

    /// <summary>
    /// §348 (the person, 2026-10-25: "when closing the edit mode it should only close the edit mode but
    /// not also the folder"): whether the folder open as the edit bar's cross ends edit mode, <paramref name="empty"/>
    /// or not, stays open, at rest: where it would open at rest, with something to show; an empty one
    /// closes, as it never opens at rest. Escape and edit mode's other ends close it (<see cref="FolderCloser.EditEnds"/>).
    /// </summary>
    public static bool Rests(bool empty) => Opens(empty, arranging: false);

    /// <summary>
    /// §487 (the person, 2026-11-02: "when clicking done in edit mode it should not close the current folder that you are
    /// in"): whether the keys go back, as edit mode ends, to the window that had them before it began — where edit mode
    /// had them (<paramref name="editHadThem"/>: a bar being edited) and no folder stays open (<paramref name="folderRests"/>,
    /// <see cref="Rests"/>). A folder kept open has them, as a folder opened at rest does: that window taking the focus
    /// from it is "another window taking the focus", which closes a folder at rest (<see cref="FolderCloser.PressElsewhere"/>).
    /// </summary>
    public static bool KeysBack(bool editHadThem, bool folderRests) => editHadThem && !folderRests;
}
