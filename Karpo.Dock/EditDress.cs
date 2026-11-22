// §612: what edit mode puts on what Karpo shows — its dress — as one state the bars, the folders open and the dim are drawn
// through (the person, 2026-11-09: "when i open the appearance settings it should not highlight the widgets in any special
// way anymore, it should not show the add buttons again, etc. it should basically show the appearance of the taskbar how it
// would appear in normal mode. it should also remove the darker tint from the background"):
//
//  - **None**: out of edit mode — the bars, the folders and the desktop as they are.
//  - **Edit**: edit mode's dress — the dim over every display, each block outlined, the "+" and the empty rooms, the
//    selection's ring, a folder open arranged spread for its guide lines and its "+".
//  - **Preview**: edit mode on, the Appearance page's flyout open: everything drawn as at None, the dim clear, so what is
//    changed shows as it will be; the edit bar and its flyout stay. Another page, or the flyout closed, is Edit again.
//
// One state, so edit mode coming and going and the Appearance opened and closed are the same change of dress, drawn by the
// same transitions (the dim's fade, the bars' marks fading and their blocks gliding, Scene.Retained.cs; the folder's lines
// gliding, FolderFlyout.Dress). The dock lays its bars out in the dress it last laid (Dock.laidDress) and lays them out again
// as the frame ends where that is no longer the dress (Dock.PresentChrome), in the commit the dim changes in (§359).

namespace Karpo.Dock;

/// <summary>§612: what edit mode puts on the bars, the folders open and the display (see the top of the file).</summary>
public enum EditDress
{
    /// <summary>Out of edit mode.</summary>
    None,

    /// <summary>Edit mode's dress: the dim, the blocks' marks, the "+", a folder spread for its guide lines.</summary>
    Edit,

    /// <summary>Edit mode on, the Appearance's flyout open: drawn as out of it, the dim clear.</summary>
    Preview,
}

/// <summary>§612: which dress is worn, and what of it shows.</summary>
public static class EditDresses
{
    /// <summary>The dress for edit mode on (<paramref name="editing"/>) or off, with <paramref name="open"/>'s flyout open (null for none).</summary>
    public static EditDress Of(bool editing, PanelPage? open) =>
        !editing ? EditDress.None : open == PanelPage.Appearance ? EditDress.Preview : EditDress.Edit;

    /// <summary>Whether edit mode's marks show in <paramref name="dress"/>: the dim, the outlines, the "+", the guide lines.</summary>
    public static bool Worn(this EditDress dress) => dress == EditDress.Edit;
}
