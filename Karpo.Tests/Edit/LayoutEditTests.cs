// Where a dragged block lands (Layout.DropSlot, Arrange) and the window a height drag
// keeps (WindowFor), on layouts measured without a Gfx (Layout.ForTests).
//
// A bar is three sections (2026-10-05), so a drop is a section and an index; these
// bars hold one section, and the section's own rules are SectionLayoutTests'.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class LayoutEditTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    /// <summary>
    /// Two islands, the clock and show desktop at the end, on a bar too short for
    /// both islands: the second, the last placed, gives way.
    /// </summary>
    private static (Layout Layout, List<BarItem> List) Crowded(bool editing = false)
    {
        var list = new List<BarItem>
        {
            new() { Id = "a", Kind = BarKind.FolderList },
            new() { Id = "b", Kind = BarKind.FolderList },
            new() { Id = "clock", Kind = BarKind.NightLight },
            new() { Id = "sd", Kind = BarKind.ShowDesktop },
        };
        var specs = list.Select(island => Spec(island.Id, island.Kind)).ToList();
        return (Layout.ForTests(96, Area(400, 1000), Edge.Bottom, new Config(), specs, 200f, editing), list);
    }

    [Fact]
    public void DropSlotCountsIslandsThatGaveWay()
    {
        // Regression: the index counted shown parts only, so with an island
        // hidden for room a drop landed one place early in the list.
        var (layout, list) = Crowded();
        Assert.True(layout.Placed(0));
        Assert.False(layout.Placed(1));

        // Island "a" dropped between the clock and show desktop.
        var along = layout.PartAt(3).Left + 1f;
        var index = layout.DropSlot(along, 0);

        Assert.Equal(new BarSlot(BarSection.Start, 2), index);
        IslandEdits.Move(list, list[0], index);
        Assert.Equal(new[] { "b", "clock", "a", "sd" }, list.Select(island => island.Id));
    }

    [Fact]
    public void ArrangeOpensTheGapWhereDropSlotSays()
    {
        var (layout, _) = Crowded();
        var along = layout.PartAt(3).Left + 1f;
        var index = layout.DropSlot(along, 0);

        var lefts = layout.Arrange(0, index);

        Assert.True(lefts[2] + layout.PartAt(2).Width <= lefts[0], "after the clock");
        Assert.True(lefts[0] + layout.PartAt(0).Width <= lefts[3], "before show desktop");
        // The island that gave way stays where it was: it isn't on the bar.
        Assert.Equal(layout.PartAt(1).Left, lefts[1]);
    }

    [Fact]
    public void DropSlotPastTheLastShownPartGoesRightAfterIt()
    {
        var specs = new List<PartSpec>
        {
            Spec("s", BarKind.Start),
            Spec("w", BarKind.WidgetsBoard),
            Spec("c", BarKind.NightLight),
        };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 60f);

        var index = layout.DropSlot(layout.PartAt(1).Left + layout.PartAt(1).Width + 20f, 2);

        Assert.Equal(new BarSlot(BarSection.Start, 2), index);
        Assert.Equal(new BarSlot(BarSection.Start, 0), layout.DropSlot(1f, 2));
    }

    /// <summary>
    /// A drop reads straight off the blocks: the further along the bar the point is, the later
    /// the place, from nought before the first block to the count of the others past the last.
    /// A bar whose entries all stand in one section is one run of places.
    /// </summary>
    [Fact]
    public void DropSlotGrowsWithThePointAlongTheBar()
    {
        var specs = new List<PartSpec>
        {
            Spec("s", BarKind.Start),
            Spec("m", BarKind.WidgetsBoard),
            Spec("c", BarKind.NightLight),
        };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 80f, editing: true);

        var seen = new List<int>();
        for (var along = 0f; along < layout.Length; along += 8f)
        {
            seen.Add(layout.DropSlot(along, 1).Index);
        }

        Assert.Equal(0, seen[0]);
        Assert.Equal(2, seen[^1]);
        // Never backwards, and never past the two blocks that are not being dragged.
        Assert.Equal(seen.OrderBy(place => place), seen);
        Assert.All(seen, place => Assert.InRange(place, 0, 2));
    }

    /// <summary>
    /// A clock in the end section stands at the bar's end, whatever the start holds; a drop just
    /// before it is the end section's first place.
    /// </summary>
    [Fact]
    public void TheEndSectionStandsAtTheBarsEnd()
    {
        var specs = new List<PartSpec>
        {
            Spec("s", BarKind.Start),
            Spec("c", BarKind.NightLight) with { Section = BarSection.End },
        };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 80f);

        var clock = layout.PartAt(1);
        Assert.Equal(layout.Length - Layout.CardInsetPx(96, new Config()), clock.Left + clock.Width);
        Assert.Equal(new BarSlot(BarSection.End, 1), layout.DropSlot(clock.Left - 1f, -1));
    }
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Top, 144u)]
    [InlineData(Edge.Left, 120u)]
    [InlineData(Edge.Right, 144u)]
    [InlineData(Edge.Bottom, 144u, BarStyle.Floating)]
    [InlineData(Edge.Left, 96u, BarStyle.Floating)]
    public void WindowForIsTheWindowTheLayoutPlaces(Edge edge, uint dpi, BarStyle style = BarStyle.Islands)
    {
        var area = new RECT { left = 1920, top = 40, right = 3840, bottom = 1120 };
        var layout = Layout.ForTests(dpi, area, edge, new Config { Height = 72, Style = style }, [Spec("c", BarKind.NightLight)], 60f);

        var window = Layout.WindowFor(dpi, area, edge, 1f, layout.Reserve);

        Assert.Equal(layout.X, window.left);
        Assert.Equal(layout.Y, window.top);
        Assert.Equal(layout.X + layout.Width, window.right);
        Assert.Equal(layout.Y + layout.Height, window.bottom);
    }
}
