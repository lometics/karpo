// §602 (the person, 2026-11-09: "the apps that are not pinned and shown in the apps widget should have the same space as
// the other pins have to each other"): every app button wears its own plate — a pin's, each open app's in the Apps widget,
// each window's of a pin whose windows are not combined, and "…" —, so the room between two of them is the layout's
// Spacing wherever they stand: pin to pin, the last pin to the first open app, open app to open app. Before, the Apps
// widget wore one plate over all its buttons, so its gaps were filled in and the open apps read as one block.
//
// Held here without drawing: the plates the layout hands the scene (Layout.Plates). The drawn plates are held on the real
// tree in SceneTreeTests.AppsGapSheets.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class AppsGapTests
{
    private static RECT Area(Edge edge) =>
        edge.IsVertical() ? new RECT { right = 1600, bottom = 3000 } : new RECT { right = 3000, bottom = 1600 };

    private static PartSpec Widget(string id, BarKind kind) => new(Config.IslandKey(null, id), kind) { Section = BarSection.Start };

    /// <summary>Start, two pins, the open apps, a widget.</summary>
    private static readonly PartSpec[] Bar =
        [Widget("s", BarKind.Start), Widget("p", BarKind.Pin), Widget("q", BarKind.Pin), Widget("apps", BarKind.Apps), Widget("b", BarKind.NightLight)];

    private static Config Spaced(int spacing, int height) =>
        new() { Height = height, Style = BarStyle.Islands, Layouts = [new TaskbarLayout { Spacing = spacing }] };

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float Reach(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    /// <summary>Every app button's plate, in the bar's order: the pins', then the Apps widget's.</summary>
    private static List<RectF> ButtonPlates(Layout layout) =>
        [.. Enumerable.Range(0, layout.Specs.Count).Where(part => layout.Specs[part].Kind.IsButtons()).SelectMany(layout.Plates)];

    private static void AllTheGapApart(Layout layout, List<RectF> plates, string where)
    {
        for (var at = 1; at < plates.Count; at++)
        {
            Assert.True(
                MathF.Abs(layout.WidgetGap - (Along(layout, plates[at]) - Reach(layout, plates[at - 1]))) < 0.01f,
                $"{where}: buttons {at - 1} and {at} stand {Along(layout, plates[at]) - Reach(layout, plates[at - 1])} px apart, not {layout.WidgetGap}");
        }
    }

    public static TheoryData<Edge, int, int, uint> EveryBar()
    {
        var data = new TheoryData<Edge, int, int, uint>();
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        foreach (var height in new[] { 32, 48, 80 })
        foreach (var spacing in new[] { 0, 3, 6, 12, 24 })
        foreach (var dpi in new uint[] { 96, 144 })
        {
            data.Add(edge, height, spacing, dpi);
        }
        return data;
    }

    /// <summary>
    /// Two pins and three open apps: five plates, each its button's own cell, every two of them the Spacing's gap apart — the
    /// open apps as the pins, and the last pin from the first open app — on every edge, at every bar size, spacing and scale.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryBar))]
    public void TheOpenAppsStandAsThePinsDo(Edge edge, int height, int spacing, uint dpi)
    {
        var config = Spaced(spacing, height);
        var layout = Layout.ForTests(dpi, Area(edge), edge, config, Bar, Layout.CellPx(dpi, config), tiles: 2 + 3);
        var where = $"{edge} {height} DIP, spacing {spacing}, {dpi} dpi";

        var plates = ButtonPlates(layout);
        Assert.Equal(5, plates.Count);
        for (var tile = 0; tile < 5; tile++)
        {
            Assert.Equal(layout.TileCell(tile), plates[tile]);
        }
        Assert.Equal(3, layout.Plates(3).Count);
        AllTheGapApart(layout, plates, where);
        // The widget after them as far from the last open app as every two widgets.
        AllTheGapApart(layout, [plates[^1], layout.Plate(4)], $"{where}, the widget after them");
    }

    /// <summary>
    /// A window each, with titles: a pin with two windows open (a plate a window), a pin with none, three open apps; every
    /// labelled button its own plate as long as it is, every two the Spacing's gap apart.
    /// </summary>
    [Theory]
    [InlineData(32, 6, 96u)]
    [InlineData(48, 0, 96u)]
    [InlineData(48, 6, 144u)]
    [InlineData(48, 12, 96u)]
    [InlineData(80, 24, 144u)]
    public void WindowsEachWithTheirTitlesStandAsThePinsDo(int height, int spacing, uint dpi)
    {
        var config = Spaced(spacing, height);
        var layout = Layout.ForTests(
            dpi,
            Area(Edge.Bottom),
            Edge.Bottom,
            config,
            Bar,
            Layout.CellPx(dpi, config),
            labels: ["Inbox - Mail", "Calendar", null, "Report.docx - Word", "Notes", "Music"],
            tileCounts: [0, 2, 1, 3, 0]);

        var plates = ButtonPlates(layout);
        Assert.Equal(6, plates.Count);
        Assert.True(layout.Labelled(3), "an open app's window says its title");
        for (var tile = 0; tile < 6; tile++)
        {
            Assert.Equal(layout.TileCell(tile), plates[tile]);
        }
        AllTheGapApart(layout, plates, $"labels, {height} DIP, spacing {spacing}, {dpi} dpi");
    }

    /// <summary>More open apps than the bar holds: the ones shown and "…" after them each a plate, the Spacing's gap apart.</summary>
    [Theory]
    [InlineData(Edge.Bottom, 6)]
    [InlineData(Edge.Bottom, 12)]
    [InlineData(Edge.Left, 6)]
    public void TheMoreButtonStandsAsAnAppDoes(Edge edge, int spacing)
    {
        var config = Spaced(spacing, 48);
        var area = edge.IsVertical() ? new RECT { right = 1600, bottom = 600 } : new RECT { right = 600, bottom = 1600 };
        var layout = Layout.ForTests(96, area, edge, config, Bar, Layout.CellPx(96, config), tiles: 2 + 30);

        Assert.NotEmpty(layout.Overflowed);
        var plates = ButtonPlates(layout);
        Assert.Equal(layout.OverflowCell(), plates[^1]);
        Assert.Equal(2 + 30 - layout.Overflowed.Count + 1, plates.Count);
        AllTheGapApart(layout, plates, $"{edge} crowded, spacing {spacing}");
    }

    /// <summary>Every other part its one plate, as before: Start's, a widget's.</summary>
    [Fact]
    public void AWidgetIsOnePlate()
    {
        var config = Spaced(6, 48);
        var layout = Layout.ForTests(96, Area(Edge.Bottom), Edge.Bottom, config, Bar, Layout.CellPx(96, config), tiles: 2 + 3);

        Assert.Equal([layout.Plate(0)], layout.Plates(0));
        Assert.Equal([layout.Plate(4)], layout.Plates(4));
    }
}
