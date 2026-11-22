// The pressed look of a row on a line whose rows light themselves (Karpo.Dock\Islands\RowPress.cs):
// down on a row, let go by the click, by the pointer leaving the row, or by the showing ending.

using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class RowPressTests
{
    [Fact]
    public void APressIsDownUntilTheClickOrThePointerLeavesItsRow()
    {
        var press = new RowPress();
        Assert.False(press.On(0));
        Assert.False(press.Down(0), "a press on nothing changes nothing");

        Assert.True(press.Down(3));
        Assert.True(press.On(3));
        Assert.False(press.On(4));
        Assert.False(press.Down(3), "the same row again: nothing to draw");

        // Over its own row it stays; anywhere else it is let go.
        Assert.False(press.Over(3));
        Assert.True(press.On(3));
        Assert.True(press.Over(4));
        Assert.False(press.On(3));
        Assert.False(press.On(4), "the row the pointer moved onto is not pressed");
        Assert.False(press.Over(0));

        // The click lets it go, once.
        press.Down(5);
        Assert.True(press.Up());
        Assert.False(press.Up());
        Assert.Equal(0, press.Held);
    }
}
