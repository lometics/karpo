// pins: edit mode taking over a drag that began outside it (DockWindow.AdoptDrag, Dock.Pins.cs
// HandOverToEditMode), in the part of it that can be measured without a window: where the pointer
// is holding the block once edit mode has built the bar again under it.
//
// The person, 2026-10-08: a widget dragged at rest that turns edit mode on is carried on, the same
// widget, into edit mode: its ghost follows, its landing shows, and the release places it. For a day
// before (2026-10-07) it was let go as edit mode came, while edit mode still shrank the screen and
// moved the bar from under the pointer; edit mode is edited where the bars stand now.
//
// Why the offset is measured again instead of carried over: edit mode's build still gives a widget
// with nothing to show room to be grabbed by, and every section its "+", so the block held can move
// a little between the press and edit mode arriving. An offset measured against where it stood could
// then put the pointer outside it, and the block would jump on the next move. The folder's half, the
// same card carried on in an arranged folder and placed by the release, is FolderDragEditTests'.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed class AdoptDragTests
{
    /// <summary>
    /// The pointer keeps hold of the block wherever it is measured from: the offset is inside the
    /// block, so the block does not jump when the next move draws it under the pointer.
    /// </summary>
    [Theory]
    [InlineData(100f, 100f, 60f, 0f)]      // at the block's start
    [InlineData(130f, 100f, 60f, 30f)]     // in its middle
    [InlineData(159f, 100f, 60f, 59f)]     // just inside its end
    [InlineData(40f, 100f, 60f, 0f)]       // the block moved away: held at its start
    [InlineData(400f, 100f, 60f, 60f)]     // and the other way: held at its end
    public void TheGrabIsAlwaysInsideTheBlock(float along, float left, float width, float expected) =>
        Assert.Equal(expected, PinDrags.GrabOffset(along, left, width));

    /// <summary>A block with no width to hold is held at nought rather than at a negative offset.</summary>
    [Theory]
    [InlineData(0f)]
    [InlineData(-4f)]
    public void ABlockWithNoWidthIsHeldAtItsStart(float width) =>
        Assert.Equal(0f, PinDrags.GrabOffset(500f, 100f, width));

    /// <summary>
    /// Measured from where the pointer is now, the offset moves by at most what the pointer moved —
    /// and between the press and edit mode arriving the pointer has moved only the drag threshold.
    /// So the block does not visibly shift as edit mode takes the drag over and carries it on.
    /// </summary>
    [Fact]
    public void TakingTheDragOverDoesNotShiftTheBlockUnderThePointer()
    {
        const float left = 200f;
        const float width = 80f;
        var atPress = PinDrags.GrabOffset(240f, left, width);

        // The pointer has crossed the threshold; the block has not moved.
        var atThreshold = PinDrags.GrabOffset(240f + GetSystemMetrics(SM.SM_CXDRAG) + 1, left, width);

        Assert.InRange(atThreshold - atPress, 0f, GetSystemMetrics(SM.SM_CXDRAG) + 1);
    }
}
