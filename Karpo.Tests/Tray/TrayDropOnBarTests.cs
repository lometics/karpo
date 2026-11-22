// Where a tray icon dragged over a bar lands, read from a point on the screen against a real layout
// (TrayDrags.OnLayout, Layout.AlongOnBar), as the dock reads the pointer of a drag out of the overflow
// or along the bar (Dock.TrayTargetAt).
//
// The defect (the person: "dragging tray icons between the bar and the overflow still doesn't work"):
// the point was measured across the bar from the top of the bar's window and taken up to the bar's
// thickness, but the window is deeper than the bar by the room it keeps for tooltips, over a bottom
// bar. The bar itself lay past that, so no point on a bottom bar's tray was ever a drop — not out of
// the overflow, not along the bar, not onto the chevron — while the empty room above it was.

using Karpo.Dock;
using TerraFX.Interop.Windows;

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayDropOnBarTests
{
    private static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    private static readonly uint[] Dpis = [96, 144];

    /// <summary>A display's bar along <paramref name="edge"/> with a widget and a tray of <paramref name="icons"/> icons.</summary>
    private static Layout Bar(uint dpi, Edge edge, int icons, BarStyle style = BarStyle.Islands, bool overflow = true)
    {
        var config = new Config { Height = 48, Style = style };
        List<PartSpec> specs =
        [
            new(Config.IslandKey(null, "battery"), BarKind.EnergySaver),
            new(Config.IslandKey(null, "tray"), BarKind.Tray),
        ];
        // A display that does not start at the screen's origin, as a second one does.
        var area = new RECT { left = 1920, top = 0, right = 1920 + 2560, bottom = 1440 };
        return Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tray: icons, trayOverflow: overflow);
    }

    /// <summary>The middle of <paramref name="rect"/> (window px) on the screen.</summary>
    private static POINT OnScreen(Layout layout, RectF rect) =>
        new() { x = layout.X + (int)(rect.X + rect.Width / 2f), y = layout.Y + (int)(rect.Y + rect.Height / 2f) };

    [Fact]
    public void TheBarsWindowIsDeeperThanTheBar()
    {
        // What the drag tripped over: the window keeps room for tooltips over the bar.
        var layout = Bar(96, Edge.Bottom, icons: 3);
        Assert.True(layout.Height > layout.Thickness + 1, $"window {layout.Height} px deep, bar {layout.Thickness}");
        var cell = OnScreen(layout, layout.ItemRect(Hit.Tray(0)));
        // The old reading: across from the window's top, and nothing past the bar's thickness.
        Assert.True(cell.y - layout.Y > layout.Thickness, "a tray icon on a bottom bar lies past the bar's thickness from the window's top");
    }

    /// <summary>
    /// Every tray icon's middle on a bar along every edge, in every look, at 100 and 150 %: an icon
    /// taken from the bar lands in its own place there, and one from the overflow on the boundary
    /// nearest; the cell after the last takes one from the overflow at the end; the chevron hides it.
    /// </summary>
    [Fact]
    public void EveryTrayIconsMiddleIsADropOnEveryEdge()
    {
        const int icons = 3;
        foreach (var style in Enum.GetValues<BarStyle>())
        {
            foreach (var dpi in Dpis)
            {
                foreach (var edge in Edges)
                {
                    var layout = Bar(dpi, edge, icons, style);
                    var what = $"{style}, {dpi} dpi, {edge}";
                    for (var index = 0; index < icons; index++)
                    {
                        var at = OnScreen(layout, layout.ItemRect(Hit.Tray(index)));
                        Assert.NotNull(layout.AlongOnBar(at));
                        // Moved along the bar: the slot it is over.
                        Assert.Equal(new TrayDrop(TrayArea.Shown, index), TrayDrags.OnLayout(layout, at, icons, hidden: 2, heldAt: 0));
                        // Out of the overflow: a place on the bar, never the overflow again.
                        Assert.True(TrayDrags.OnLayout(layout, at, icons, hidden: 2) is { Area: TrayArea.Shown }, what);
                    }
                    // Out of the overflow, after the last icon: the end.
                    var last = layout.ItemRect(Hit.Tray(icons - 1));
                    var after = layout.Vertical ? last with { Y = last.Y + layout.TrayStep } : last with { X = last.X + layout.TrayStep };
                    Assert.Equal(new TrayDrop(TrayArea.Shown, icons), TrayDrags.OnLayout(layout, OnScreen(layout, after), icons, hidden: 2));
                    // Onto the chevron: behind it, after the two there.
                    Assert.Equal(new TrayDrop(TrayArea.Overflow, 2), TrayDrags.OnLayout(layout, OnScreen(layout, layout.ItemRect(Hit.TrayOverflow)), icons, hidden: 2));
                }
            }
        }
    }

    /// <summary>The room over a bottom bar its window keeps for tooltips is no drop, nor is anywhere off the window.</summary>
    [Fact]
    public void TheTooltipsRoomAndAnywhereOffTheBarAreNoDrop()
    {
        foreach (var dpi in Dpis)
        {
            var layout = Bar(dpi, Edge.Bottom, icons: 3);
            var cell = OnScreen(layout, layout.ItemRect(Hit.Tray(1)));
            // The window's top, straight over the icon: the tooltips' room.
            Assert.Null(layout.AlongOnBar(cell with { y = layout.Y + 1 }));
            Assert.Null(TrayDrags.OnLayout(layout, cell with { y = layout.Y + 1 }, 3, hidden: 2));
            // Just over the bar's strip, and past the window either way.
            Assert.Null(TrayDrags.OnLayout(layout, cell with { y = layout.Y + (int)layout.PanelTop - 1 }, 3, hidden: 2));
            Assert.Null(TrayDrags.OnLayout(layout, cell with { y = layout.Y - 10 }, 3, hidden: 2));
            Assert.Null(TrayDrags.OnLayout(layout, cell with { x = layout.X - 10 }, 3, hidden: 2));
            // The bar's whole strip is the bar, down to the screen's edge.
            Assert.NotNull(TrayDrags.OnLayout(layout, cell with { y = layout.Y + layout.Height - 1 }, 3, hidden: 2));
        }
    }

    /// <summary>
    /// A layout per edge: two bars on one display, along the bottom and the top. A point on the top
    /// bar's tray is the top bar's, and nothing on the bottom bar's, though both windows are on the
    /// same display.
    /// </summary>
    [Fact]
    public void TwoBarsOnOneDisplayEachTakeOnlyTheirOwn()
    {
        var bottom = Bar(96, Edge.Bottom, icons: 3);
        var top = Bar(96, Edge.Top, icons: 3);
        var onTop = OnScreen(top, top.ItemRect(Hit.Tray(1)));
        var onBottom = OnScreen(bottom, bottom.ItemRect(Hit.Tray(1)));

        Assert.NotNull(TrayDrags.OnLayout(top, onTop, 3, hidden: 0));
        Assert.Null(TrayDrags.OnLayout(bottom, onTop, 3, hidden: 0));
        Assert.NotNull(TrayDrags.OnLayout(bottom, onBottom, 3, hidden: 0));
        Assert.Null(TrayDrags.OnLayout(top, onBottom, 3, hidden: 0));
    }

    /// <summary>A bar with no tray takes no drop, wherever the point is on it.</summary>
    [Fact]
    public void ABarWithoutATrayTakesNone()
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
        var layout = Layout.ForTests(96, area, Edge.Bottom, config, [new(Config.IslandKey(null, "battery"), BarKind.EnergySaver)], Layout.CellPx(96, config));
        var middle = new POINT { x = 960, y = layout.Y + layout.Height - (int)(layout.Thickness / 2f) };

        Assert.NotNull(layout.AlongOnBar(middle));
        Assert.Null(TrayDrags.OnLayout(layout, middle, 3, hidden: 1));
    }
}
