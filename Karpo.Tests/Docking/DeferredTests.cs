// What Windows says while a menu's or a dialog's modal loop has the dock busy (Dock.WndProc): what
// changes the bars' world is kept and handled once the loop gives the dock back (Dock.Deferrable), or
// it is lost — and the bars stay as the world was before it (docs/resilience.md §4).

using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed class DeferredTests
{
    /// <summary>A display's scale changed with a menu open: without the arrange it asks for, the bar keeps the old scale's size.</summary>
    [Fact]
    public void AScaleChangeDuringAMenuIsKeptForAfterwards()
    {
        Assert.True(Karpo.Dock.Dock.Deferrable(WM.WM_DPICHANGED, 0x0090_0090));
        // Its lParam points at a rectangle that is gone by then.
        Assert.Equal((LPARAM)0, Karpo.Dock.Dock.Kept(WM.WM_DPICHANGED, 0x1234));
    }

    /// <summary>A menu left open over a sleep: the resume must still put the clock, the alarms and the strips right.</summary>
    [Theory]
    [InlineData(PBT.PBT_APMRESUMEAUTOMATIC)]
    [InlineData(PBT.PBT_APMRESUMESUSPEND)]
    public void AResumeDuringAMenuIsKeptForAfterwards(uint resume) =>
        Assert.True(Karpo.Dock.Dock.Deferrable(WM.WM_POWERBROADCAST, resume));

    [Theory]
    [InlineData(WM.WM_DISPLAYCHANGE)]
    [InlineData(WM.WM_SETTINGCHANGE)]
    [InlineData(WM.WM_WTSSESSION_CHANGE)]
    [InlineData(WM.WM_TIMECHANGE)]
    public void EveryChangeOfTheBarsWorldIsKept(uint msg) => Assert.True(Karpo.Dock.Dock.Deferrable(msg, 0));

    /// <summary>A suspend is not: by the time a menu closes the machine is long back, and the resume says what matters.</summary>
    [Fact]
    public void ASuspendIsNotKept() => Assert.False(Karpo.Dock.Dock.Deferrable(WM.WM_POWERBROADCAST, PBT.PBT_APMSUSPEND));
}
