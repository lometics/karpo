// When a tray change is painted in place and when the bars are built again
// (TrayShape.Same): a live icon's new picture must not rebuild the bar, and a
// change that moves anything must.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Perf;

public sealed class TrayShapeTests
{
    private static Pixels Picture() => new() { Data = new byte[4], Width = 1, Height = 1 };

    private static TrayIcon Icon(string key, Pixels? picture, bool hidden = false, string tip = "") =>
        new(key, HWND.NULL, 0, Guid.Empty, 0x400, 4, picture, tip, hidden);

    [Fact]
    public void ANewPictureOrTooltipKeepsTheShape()
    {
        var before = new[] { Icon("a", Picture(), tip: "CPU 5%"), Icon("b", Picture()) };
        var after = new[] { Icon("a", Picture(), tip: "CPU 7%"), Icon("b", before[1].Icon) };
        Assert.True(TrayShape.Same(before, [], after, []));
    }

    [Fact]
    public void AnIconComingOrGoingChangesTheShape()
    {
        var two = new[] { Icon("a", Picture()), Icon("b", Picture()) };
        var three = new[] { Icon("a", Picture()), Icon("b", Picture()), Icon("c", Picture()) };
        Assert.False(TrayShape.Same(two, [], three, []));
        Assert.False(TrayShape.Same(three, [], two, []));
    }

    [Fact]
    public void AnotherIconInTheSamePlaceChangesTheShape()
    {
        // The icons' places are published by key: a different icon needs them published again.
        Assert.False(TrayShape.Same([Icon("a", Picture())], [], [Icon("b", Picture())], []));
        Assert.False(TrayShape.Same([Icon("a", Picture()), Icon("b", Picture())], [], [Icon("b", Picture()), Icon("a", Picture())], []));
    }

    [Fact]
    public void APictureComingOrGoingChangesTheShape()
    {
        // The scene has no sprite for an icon without a picture.
        Assert.False(TrayShape.Same([Icon("a", null)], [], [Icon("a", Picture())], []));
        Assert.False(TrayShape.Same([Icon("a", Picture())], [], [Icon("a", null)], []));
        Assert.True(TrayShape.Same([Icon("a", null)], [], [Icon("a", null)], []));
    }

    [Fact]
    public void TheOverflowChevronComingOrGoingChangesTheShape()
    {
        var shown = new[] { Icon("a", Picture()) };
        Assert.False(TrayShape.Same(shown, [], shown, [Icon("z", Picture())]));
        Assert.False(TrayShape.Same(shown, [Icon("z", Picture())], shown, []));
        // What sits in the overflow may change without moving anything on the bar.
        Assert.True(TrayShape.Same(shown, [Icon("y", Picture())], shown, [Icon("z", Picture()), Icon("w", null)]));
    }

    [Fact]
    public void AnIconWindowsWouldHideCountsLikeAnyOtherOnTheBar()
    {
        // TrayIcon.Hidden is Windows' own choice for the icon, which the split has already been
        // through: an icon on the bar against that choice is drawn, so it must be compared. This
        // used to leave it out, and a bar was never built again when such an icon came or went.
        var one = new[] { Icon("a", Picture()), Icon("x", Picture(), hidden: true) };

        Assert.False(TrayShape.Same(one, [], [Icon("a", Picture())], []));
        Assert.False(TrayShape.Same(one, [], [Icon("a", Picture()), Icon("y", Picture(), hidden: true)], []));
        Assert.True(TrayShape.Same(one, [], [Icon("a", Picture()), Icon("x", Picture(), hidden: true)], []));
    }
}
