// The bar at touch size in tablet posture, as Windows 11's "Optimize taskbar for touch interactions
// when this device is used as a tablet" has it (TabletPosture), and the desktop peek on the Show
// desktop corner (DesktopPeek): decisions and a layout measured without a Gfx, so nothing reads
// the PC's posture or settings, and nothing fades on the person's screen.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class TabletPostureTests
{
    /// <summary>
    /// Touch size needs all three: Windows' setting not off (never set is on, as Windows ships it),
    /// a screen that takes touch, and the device in slate mode. A PC without touch is never a tablet,
    /// whatever its slate-mode metric says.
    /// </summary>
    [Theory]
    [InlineData(1u, 10, 0, true)]
    [InlineData(null, 10, 0, true)]
    [InlineData(0u, 10, 0, false)]
    [InlineData(1u, 10, 1, false)]
    [InlineData(1u, 0, 0, false)]
    [InlineData(null, 0, 0, false)]
    public void TouchSizeIsTheSettingATouchScreenAndSlateMode(uint? expandable, int touchPoints, int slateMode, bool touch) =>
        Assert.Equal(touch, TabletPosture.Touch(expandable, touchPoints, slateMode));

    /// <summary>At touch size a bar is at least the touch height; a taller one keeps its own; out of it, it is as set.</summary>
    [Theory]
    [InlineData(32, true, TabletPosture.TouchHeightDip)]
    [InlineData(48, true, TabletPosture.TouchHeightDip)]
    [InlineData(96, true, 96)]
    [InlineData(0, true, TabletPosture.TouchHeightDip)]
    [InlineData(32, false, 32)]
    [InlineData(96, false, 96)]
    public void AtTouchSizeTheBarIsAtLeastTheTouchHeight(int configured, bool touch, int height) =>
        Assert.Equal(height, TabletPosture.HeightFor(configured, touch));

    /// <summary>The person's config is never changed: the bar is laid out from a copy, and only where the height differs.</summary>
    [Fact]
    public void ThePersonsHeightIsNeverChanged()
    {
        var config = new Config { Height = 40, Style = BarStyle.Islands };

        var shown = TabletPosture.Shown(config, touch: true);

        Assert.NotSame(config, shown);
        Assert.Equal(TabletPosture.TouchHeightDip, shown.Height);
        Assert.Equal(40, config.Height);
        Assert.Equal(BarStyle.Islands, shown.Style);
        Assert.Same(config, TabletPosture.Shown(config, touch: false));
        // A bar already taller than touch size is laid out from the person's own config.
        var tall = new Config { Height = 96 };
        Assert.Same(tall, TabletPosture.Shown(tall, touch: true));
    }

    /// <summary>
    /// At touch size every button is a bigger target: the bar is thicker, its cells longer, and an
    /// app button's highlight at least as large — a 32 DIP bar's icons grow with it to the full size.
    /// </summary>
    [Fact]
    public void AtTouchSizeEveryButtonIsABiggerTarget()
    {
        var config = new Config { Height = 32 };
        List<PartSpec> specs = [new(Config.IslandKey(null, "start"), BarKind.Start), new(Config.IslandKey(null, "apps"), BarKind.Apps)];
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        Layout Lay(Config shown) => Layout.ForTests(96, area, Edge.Bottom, shown, specs, Layout.CellPx(96, shown), tiles: 3);

        var laptop = Lay(TabletPosture.Shown(config, touch: false));
        var tablet = Lay(TabletPosture.Shown(config, touch: true));

        Assert.True(tablet.Thickness > laptop.Thickness, $"{tablet.Thickness} px at touch size, {laptop.Thickness} px as set");
        Assert.True(tablet.Slot > laptop.Slot);
        Assert.True(tablet.Icon > laptop.Icon);
        var laptopButton = laptop.ItemRect(Hit.Tile(0));
        var tabletButton = tablet.ItemRect(Hit.Tile(0));
        Assert.True(tabletButton.Width > laptopButton.Width && tabletButton.Height > laptopButton.Height);
        // The whole bar at touch size is at least what a fingertip needs, at every button.
        Assert.True(tablet.Slot >= 40f, $"a touch-size cell is {tablet.Slot} px");
    }

    // --- The desktop peek ---------------------------------------------------------------------------

    /// <summary>
    /// Resting on Show desktop peeks only where Windows' "Use Peek to preview the desktop" says 0 —
    /// missing is off, as Windows 11 leaves it — and Peek itself is not off.
    /// </summary>
    [Theory]
    [InlineData(0u, 1u, true)]
    [InlineData(0u, null, true)]
    [InlineData(0u, 0u, false)]
    [InlineData(1u, 1u, false)]
    [InlineData(null, 1u, false)]
    [InlineData(null, null, false)]
    public void TheDesktopPeeksWhereWindowsSaysSo(uint? disablePreviewDesktop, uint? enableAeroPeek, bool peeks) =>
        Assert.Equal(peeks, DesktopPeek.Wanted(disablePreviewDesktop, enableAeroPeek));

    /// <summary>The rest before it shows is Windows' own DesktopLivePreviewHoverTime, within ten seconds, else half a second.</summary>
    [Theory]
    [InlineData(null, DesktopPeek.DefaultDelayMs)]
    [InlineData(0u, 0u)]
    [InlineData(1000u, 1000u)]
    [InlineData(600000u, 10000u)]
    public void TheRestIsWindowsOwn(uint? hoverTime, uint delay) =>
        Assert.Equal(delay, DesktopPeek.DelayOf(hoverTime));
}
