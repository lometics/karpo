// What the bar makes room for: one cell per notification icon the split left on
// it (Layout.TrayCells). The icons behind the chevron are already out of
// Content.Tray, and Windows' own choice for an icon says nothing here.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.TrayIcons;

public sealed class TrayBarTests
{
    /// <summary>One icon as the bar receives it. <paramref name="appHidden"/> is Windows' own choice for it.</summary>
    private static TrayIcon Icon(string key, bool appHidden) =>
        new(key, HWND.NULL, 0, Guid.Empty, 0, 4, null, key, appHidden, key);

    [Fact]
    public void EveryIconOnTheBarGetsACell() =>
        Assert.Equal(3, Layout.TrayCells(new Content { Tray = [Icon("a", false), Icon("b", false), Icon("c", false)] }));

    [Fact]
    public void AnIconWindowsWouldHideStillGetsOne()
    {
        // The defect: the bar counted only the icons Windows promotes, so an icon the person had
        // dragged out of the overflow, or set to Show, was given no cell and drawn nowhere — and
        // with "all icons" on, which is the default now, that was every icon.
        var content = new Content { Tray = [Icon("a", true), Icon("b", true), Icon("c", false)] };

        Assert.Equal(3, Layout.TrayCells(content));
    }

    [Fact]
    public void ABarWithNoTrayIconsTakesNoCells()
    {
        Assert.Equal(0, Layout.TrayCells(new Content()));
        Assert.Equal(0, Layout.TrayCells(new Content { Tray = [] }));
    }
}
