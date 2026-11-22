// Files and apps dragged onto a bar that is not being edited — a folder or an app from File
// Explorer or Start — and where the pin they make lands (Layout.DropSlot, Layout.Landing).
//
// The person, earlier: "it should place it based on which end you drag it closer to. the blue area
// that appears should have the size 1x1 or what the new pins size would be." Since the bar is three
// sections (2026-10-05) the drop goes into the section under the pointer — between two, the nearer
// — at the nearest gap among that section's own parts; and what the bar shows while the files are
// over it is the new pin's own cell, at the size it will have and at the very place it will land —
// the bar laid out again with the pin in it — with only its own section making room.
//
// Measured without a Gfx (Layout.ForTests), at rest unless a test says otherwise.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Pinning;

public sealed class DropLandingTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind, BarSection section) =>
        new(Config.IslandKey(null, id), kind) { Section = section };

    private static Layout Bar(IReadOnlyList<PartSpec> specs, Edge edge = Edge.Bottom, bool editing = false, int tiles = 0) =>
        Layout.ForTests(96, Area(1920, 1080), edge, new Config(), specs, Layout.CellPx(96, new Config()), editing, tiles: tiles);

    private static BarSlot At(BarSection section, int index) => new(section, index);

    /// <summary>The weather at the start, Start and search in the middle, the clock at the end: the bar the person drags onto.</summary>
    private static List<PartSpec> Three() =>
    [
        Spec("weather", BarKind.WidgetsBoard, BarSection.Start),
        Spec("start", BarKind.Start, BarSection.Middle),
        Spec("search", BarKind.Search, BarSection.Middle),
        Spec("clock", BarKind.NightLight, BarSection.End),
    ];

    /// <summary>The same bar with nothing in the middle.</summary>
    private static List<PartSpec> NoMiddle() =>
    [
        Spec("weather", BarKind.WidgetsBoard, BarSection.Start),
        Spec("clock", BarKind.NightLight, BarSection.End),
    ];

    // --- Which section, which gap ---------------------------------------------------

    /// <summary>
    /// The room between two sections is the nearer section's: a drop a little past the weather is
    /// the start's last place, a little before Start the middle's first, a little past search the
    /// middle's last, a little before the clock the end's first.
    /// </summary>
    [Fact]
    public void BetweenTwoSectionsTheNearerHasTheDrop()
    {
        var layout = Bar(Three());
        var weather = layout.PartAt(0);
        var start = layout.PartAt(1);
        var search = layout.PartAt(2);
        var clock = layout.PartAt(3);

        Assert.Equal(At(BarSection.Start, 1), layout.DropSlot(weather.Right + 10f, -1));
        Assert.Equal(At(BarSection.Middle, 1), layout.DropSlot(start.Left - 10f, -1));
        Assert.Equal(At(BarSection.Middle, 3), layout.DropSlot(search.Right + 10f, -1));
        Assert.Equal(At(BarSection.End, 3), layout.DropSlot(clock.Left - 10f, -1));
    }

    /// <summary>Within a section the half of a part the pointer is on says which side of it.</summary>
    [Fact]
    public void WithinASectionTheNearerEndOfAPartDecides()
    {
        var layout = Bar(Three());
        var start = layout.PartAt(1);
        var search = layout.PartAt(2);

        Assert.Equal(At(BarSection.Middle, 1), layout.DropSlot(start.Left + start.Width * 0.25f, -1));
        Assert.Equal(At(BarSection.Middle, 2), layout.DropSlot(start.Left + start.Width * 0.75f, -1));
        Assert.Equal(At(BarSection.Middle, 2), layout.DropSlot(search.Left + search.Width * 0.25f, -1));
        Assert.Equal(At(BarSection.Middle, 3), layout.DropSlot(search.Left + search.Width * 0.75f, -1));
    }

    /// <summary>
    /// A bar with an empty middle: the middle is the bar's centre, so files dragged near the centre
    /// make the middle's first pin there, and the two sides' room belongs to the sides.
    /// </summary>
    [Fact]
    public void AnEmptyMiddleIsTheCentreOfTheBar()
    {
        var layout = Bar(NoMiddle());

        Assert.Equal(At(BarSection.Middle, 1), layout.DropSlot(layout.Length / 2f, -1));
        Assert.Equal(At(BarSection.Start, 1), layout.DropSlot(layout.Length * 0.2f, -1));
        Assert.Equal(At(BarSection.End, 1), layout.DropSlot(layout.Length * 0.8f, -1));
    }

    // --- The mark: the new pin's own cell ---------------------------------------------

    /// <summary>The mark is the size of the pin the drop makes — one app button, a cell of the bar.</summary>
    [Theory]
    [InlineData(BarSection.Start, 1)]
    [InlineData(BarSection.Middle, 1)]
    [InlineData(BarSection.Middle, 3)]
    [InlineData(BarSection.End, 3)]
    [InlineData(BarSection.End, 4)]
    public void TheMarkIsOneNewPinsCell(BarSection section, int index)
    {
        var layout = Bar(Three());

        var mark = layout.LandingRect(At(section, index));

        Assert.False(mark.IsEmpty);
        Assert.Equal(layout.Slot, mark.Width, 0.5f);
        Assert.True(mark.Height <= layout.Thickness, "and no taller than the bar");
    }

    /// <summary>
    /// The mark stands exactly where the pin will: the bar laid out with the pin added at that place,
    /// in that section, has its button's cell on that very rectangle.
    /// </summary>
    [Theory]
    [InlineData(BarSection.Start, 1)]
    [InlineData(BarSection.Middle, 1)]
    [InlineData(BarSection.Middle, 2)]
    [InlineData(BarSection.Middle, 3)]
    [InlineData(BarSection.End, 3)]
    [InlineData(BarSection.End, 4)]
    public void TheMarkIsWhereThePinWillStand(BarSection section, int index)
    {
        var specs = Three();
        var layout = Bar(specs);

        var mark = layout.LandingRect(At(section, index));
        var pin = BarKind.Pin.New();
        pin.Section = section;
        var grown = new List<PartSpec>(specs);
        grown.Insert(index, PartSpec.For(Config.IslandKey(null, "dropped"), pin));
        var after = Bar(grown, tiles: 1);

        Assert.Equal(after.TileCell(0), mark);
        Assert.Equal(after.PartAt(index).Left, mark.X, 0.5f);
    }

    /// <summary>
    /// Only the pin's own section makes room for it: the start's last pin moves nothing else; the
    /// middle opens round the pin, half each way, staying centred, and the start and the end stay;
    /// the end's first pin pushes nothing at the start or in the middle.
    /// </summary>
    [Fact]
    public void OnlyThePinsSectionMakesRoom()
    {
        var layout = Bar(Three());

        var (_, atStart) = layout.Landing(At(BarSection.Start, 1));
        foreach (var part in new[] { 0, 1, 2, 3 })
        {
            Assert.Equal(layout.PartAt(part).Left, atStart[part], 0.5f);
        }

        var (mark, middle) = layout.Landing(At(BarSection.Middle, 2));
        Assert.True(middle[1] < layout.PartAt(1).Left, "Start makes room to its left");
        Assert.True(middle[2] > layout.PartAt(2).Left, "search makes room to its right");
        Assert.Equal(layout.PartAt(0).Left, middle[0], 0.5f);
        Assert.Equal(layout.PartAt(3).Left, middle[3], 0.5f);
        Assert.True(mark.X >= middle[1] + layout.PartAt(1).Width, "the pin stands after Start");
        Assert.True(mark.Right <= middle[2], "and before search");

        var (_, atEnd) = layout.Landing(At(BarSection.End, 3));
        Assert.Equal(layout.PartAt(0).Left, atEnd[0], 0.5f);
        Assert.Equal(layout.PartAt(1).Left, atEnd[1], 0.5f);
        Assert.Equal(layout.PartAt(3).Left, atEnd[3], 0.5f);
    }

    /// <summary>A bar down the side: the mark is one cell down it.</summary>
    [Fact]
    public void AVerticalBarsMarkIsOneCellDownIt()
    {
        var layout = Bar(Three(), Edge.Left);

        var mark = layout.LandingRect(At(BarSection.Middle, 2));

        Assert.Equal(layout.Slot, mark.Height, 0.5f);
        Assert.True(mark.Width <= layout.Thickness);
    }

    /// <summary>The place is clamped to the list: past either end is the end.</summary>
    [Fact]
    public void APlaceOffTheListIsItsEnd()
    {
        var layout = Bar(Three());

        Assert.Equal(layout.LandingRect(At(BarSection.Start, 0)), layout.LandingRect(At(BarSection.Start, -3)));
        Assert.Equal(layout.LandingRect(At(BarSection.End, 4)), layout.LandingRect(At(BarSection.End, 99)));
    }
}
