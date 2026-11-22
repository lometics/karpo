// What a widget dragged along the bar does, decided apart from any window so it can be tested.
//
// A widget dragged along the bar moves along it, and that is all: nothing moves from the bar into a
// folder, so a widget is never dropped into a folder's button nor made a folder of with another it
// rests on (the person, 2026-10-09: "i would rather disable it completely").

namespace Karpo.Dock;

public static class PinDrags
{
    /// <summary>
    /// pins: where along a block the pointer is holding it, in the block's own px, for a block
    /// <paramref name="width"/> long starting at <paramref name="left"/> with the pointer at
    /// <paramref name="along"/> (window px along the bar).
    /// <para>
    /// It exists for the one caller that cannot use the press point: edit mode taking over a drag
    /// that began outside it (<c>DockWindow.AdoptDrag</c>). Edit mode is edited where the bar stands
    /// now, but its build still gives a widget with nothing to show room to be grabbed by, and every
    /// section its "+", so the block held may stand a little elsewhere than at the press. Measured
    /// from where the pointer is now and clamped into the block, the offset cannot put the pointer
    /// outside it, and the block does not jump on the next move.
    /// </para>
    /// </summary>
    public static float GrabOffset(float along, float left, float width) =>
        width <= 0f ? 0f : Math.Clamp(along - left, 0f, width);
}
