// edit: what the bar shows of a widget carried along it while folders may stand open above it,
// decided apart from any window so it can be tested (DockWindow.EditView asks it at every move and
// every tick of the drag's timer).
//
// One rule lives here, from the person's "multiple folders open because even though you are
// already in the folder area it somehow still moves the widget down at the taskbar as well": over
// an open folder's panel the folder has the widget, and the bar is left as it stood the moment the
// pointer went onto the panel: no gap moves and the carried block stays where it was. The bar holds
// the mouse, so it went on reading the pointer's x as a place along it — opening gaps and sliding
// its blocks under the folder.
//
// Back on the bar, everything goes on from where it was.
//
// A closed folder used to open under a carried widget that rested on its button, so the widget
// could be put in a place of its choosing. Nothing moves from the bar into a folder any more
// (2026-10-09), so a folder's button is no place to rest a widget on.

using Karpo.Core;

namespace Karpo.Dock;

public sealed class BarCarry
{
    /// <summary>What the bar shows of the carried block at one step of the drag.</summary>
    /// <param name="Along">Where the pointer is taken to be along the bar, which the carried block follows.</param>
    /// <param name="Index">Where the block would land, and so where the gap stands.</param>
    public readonly record struct Shown(float Along, BarSlot Index);

    // What the bar showed at its last step with the pointer on it: what it keeps while a panel has the pointer.
    private Shown? kept;

    /// <summary>
    /// One step of the drag. <paramref name="overPanel"/>: an open folder's panel has the pointer —
    /// its panel, not the room its shadow keeps around it (<see cref="FolderFlyout.Offer"/>).
    /// <paramref name="onBar"/> works out what the bar shows with the pointer on it, and is asked
    /// only then, because asking it moves the bar.
    /// </summary>
    public Shown Step(bool overPanel, Func<Shown> onBar)
    {
        if (overPanel)
        {
            // The folder has it: the bar stands as it last stood. A drag that never was on the bar
            // (none yet) has one look at it, which it then keeps.
            kept ??= onBar();
            return kept.Value;
        }
        var shown = onBar();
        kept = shown;
        return shown;
    }
}
