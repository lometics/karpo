// The tray's block while an icon is dragged between it and the overflow: as long as the icons it will
// have — one cell more while an icon from the overflow would land among its icons, one fewer while one
// of its own would go behind the chevron —, the same after the drop, and back as it was on a cancel.
// The person, 2026-10-13: "the system tray widget should actually grow or shrink when i drag an icon
// from the more tray or vice versa, right now the other tray icons just get pushed off the side."
// Measured on a real layout without a Gfx (Layout.ForTests), a widget either side of the tray.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.TrayIcons;

public sealed class TrayDragRoomTests
{
    private static readonly string TrayKey = Config.IslandKey(null, "tray");

    private static readonly string WifiKey = Config.IslandKey(null, "wifi");

    /// <summary>A bar with <paramref name="icons"/> tray icons and the chevron, the tray given <paramref name="more"/> cells for a drag.</summary>
    private static Layout Bar(Edge edge, int icons, int more = 0, int cells = WidgetItem.AutomaticWidth, uint dpi = 96)
    {
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        List<PartSpec> specs =
        [
            new(Config.IslandKey(null, "battery"), BarKind.EnergySaver),
            new(TrayKey, BarKind.Tray, cells),
            new(WifiKey, BarKind.TouchKeyboard),
        ];
        var area = edge.IsVertical() ? new RECT { right = 1200, bottom = 2600 } : new RECT { right = 2600, bottom = 1200 };
        return Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tray: icons, trayOverflow: true,
            trayMore: more == 0 ? null : new Dictionary<string, int> { [TrayKey] = more });
    }

    private static float Length(Layout layout) => layout.PartAt(layout.PartIndex(TrayKey)).Width;

    private static float Start(Layout layout, string key) => layout.PartAt(layout.PartIndex(key)).Left;

    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Left, 96u)]
    [InlineData(Edge.Right, 144u)]
    public void ATrayIsAsLongAsTheIconsItWillHaveWhileOneIsDragged(Edge edge, uint dpi)
    {
        var rest = Bar(edge, 3, dpi: dpi);
        var cell = rest.TrayStep;
        // The chevron's cell and one per icon, at rest.
        Assert.Equal(4 * cell, Length(rest), 0.5f);

        // In from the overflow: a cell more, along the bar's long way on a side bar too; the icons it
        // has are still three, and the widget after it makes room by that cell.
        var growing = Bar(edge, 3, more: 1, dpi: dpi);
        Assert.Equal(5 * cell, Length(growing), 0.5f);
        Assert.Equal(3, growing.PartAt(growing.PartIndex(TrayKey)).Count);
        Assert.Equal(1, growing.TrayMoreOf(growing.PartIndex(TrayKey)));
        Assert.Equal(Start(rest, WifiKey) + cell, Start(growing, WifiKey), 0.5f);
        // Let go there: four icons, as long as the drag had it, so nothing snaps back.
        var dropped = Bar(edge, 4, dpi: dpi);
        Assert.Equal(Length(growing), Length(dropped), 0.5f);
        Assert.Equal(Start(growing, WifiKey), Start(dropped, WifiKey), 0.5f);

        // Out behind the chevron: a cell fewer, and two icons once it is let go there.
        var shrinking = Bar(edge, 3, more: -1, dpi: dpi);
        Assert.Equal(3 * cell, Length(shrinking), 0.5f);
        Assert.Equal(Length(Bar(edge, 2, dpi: dpi)), Length(shrinking), 0.5f);

        // Called off: no cell given or taken, the tray as it was.
        Assert.Equal(Length(rest), Length(Bar(edge, 3, more: 0, dpi: dpi)), 0.5f);
    }

    [Fact]
    public void ATrayOfASetSizeKeepsItThroughADrag()
    {
        // Three grid cells of room: a set size, which the drag neither lengthens nor shortens.
        var rest = Bar(Edge.Bottom, 3, cells: 3);
        var part = rest.PartIndex(TrayKey);
        var growing = Bar(Edge.Bottom, 3, more: 1, cells: 3);
        Assert.Equal(Length(rest), Length(growing), 0.5f);
        Assert.Equal(Length(rest), Length(Bar(Edge.Bottom, 3, more: -1, cells: 3)), 0.5f);
        Assert.Equal(0, growing.TrayMoreOf(part));
        // What it has room for: the icons pushed past it go behind the chevron while the gap stands.
        Assert.Equal((int)MathF.Floor(Length(rest) / rest.TrayStep) - 1, growing.TrayHolds(part));
        Assert.Equal(int.MaxValue, Bar(Edge.Bottom, 3).TrayHolds(part));
    }

    [Fact]
    public void AGrownTrayReadsThePointerAsItDidBefore()
    {
        // The drop the pointer means is read against the tray as long as its icons, not with the cell
        // the drag gave it: else the room past the new cell — the next widget's — would take it too.
        var rest = Bar(Edge.Bottom, 3);
        var growing = Bar(Edge.Bottom, 3, more: 1);
        var tray = rest.PartAt(rest.PartIndex(TrayKey));
        var y = rest.Y + (int)(rest.ItemRect(Hit.TrayOverflow).Y + rest.TrayCell / 2f);
        for (var along = (int)tray.Left; along < tray.Left + tray.Width + 2 * rest.TrayStep; along += 4)
        {
            var at = new POINT { x = rest.X + along, y = y };
            Assert.Equal(TrayDrags.OnLayout(rest, at, 3, 2), TrayDrags.OnLayout(growing, at, 3, 2));
        }
    }

    [Theory]
    // From the overflow over this tray: a cell more; over another bar's, or the overflow: none.
    [InlineData(TrayArea.Shown, true, false, 1)]
    [InlineData(TrayArea.Shown, false, false, 0)]
    [InlineData(TrayArea.Overflow, false, false, 0)]
    // One of its own: along the tray none; behind the chevron, or in the overflow, a cell fewer.
    [InlineData(TrayArea.Shown, true, true, 0)]
    [InlineData(TrayArea.Overflow, true, true, -1)]
    [InlineData(TrayArea.Overflow, false, true, -1)]
    public void TheCellADragGivesOrTakes(TrayArea area, bool aimedHere, bool draws, int more) =>
        Assert.Equal(more, TrayDrags.MoreFor(new TrayDrop(area, 0), aimedHere, draws));

    [Fact]
    public void NothingIsGivenBeforeTheDragShowsAPlace() =>
        Assert.Equal(0, TrayDrags.MoreFor(null, aimedHere: true, draws: true));

    /// <summary>
    /// A tray at the bar's end, a cell shorter while one of its icons is over the chevron or the
    /// overflow: its last icon's place at rest is past the bar's end, and it wasn't drawn at all (its
    /// rectangle was empty), where it closes up into the dragged one's place (§172).
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Left, 96u)]
    public void ATrayAtTheBarsEndKeepsItsLastIconWhileItIsShorter(Edge edge, uint dpi)
    {
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        List<PartSpec> specs =
        [
            new(Config.IslandKey(null, "battery"), BarKind.EnergySaver),
            new PartSpec(TrayKey, BarKind.Tray) with { Section = BarSection.End },
        ];
        var area = edge.IsVertical() ? new RECT { right = 1200, bottom = 1600 } : new RECT { right = 1600, bottom = 1200 };
        var shorter = Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tray: 3, trayOverflow: true,
            trayMore: new Dictionary<string, int> { [TrayKey] = -1 });
        var part = shorter.PartIndex(TrayKey);
        var second = shorter.TrayIconRect(1, part);
        var third = shorter.TrayIconRect(2, part);
        Assert.False(third.IsEmpty);
        // Where it stands at rest, a cell after the second: Scene.TrayDragIn moves it back by that cell.
        Assert.Equal(shorter.TrayStep, shorter.Vertical ? third.Y - second.Y : third.X - second.X, 0.5f);
        // Nothing past it: there is no fourth.
        Assert.True(shorter.TrayIconRect(3, part).IsEmpty);
    }
}
