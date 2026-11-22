// Where a block dragged in edit mode would land: which section, and where in it.
//
// The complaint the first of these were written for: "moving a widget all the way to the left or
// right is not possible. it just groups with the rightmost widget." The drag asked Layout.DropSlot
// about the dragged block's middle, clamped to the bar, which can only reach `length - width/2` —
// short of the last widget's middle whenever the block is the wider. The drop point is the pointer
// now, so both ends are reachable at any width and any grab.
//
// Sections (2026-10-05): the bar is three sections, and a drop goes into the section under the
// pointer — between two, the nearer — at the nearest gap among that section's own blocks. An empty
// section is its "+" while the bar is arranged, which is where a block is dropped into it, and at
// rest the point it would grow from, so a pin dragged to the middle of a bar with nothing in the
// middle lands in the middle.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class DropSlotTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind, BarSection section = BarSection.Start) =>
        new(Config.IslandKey(null, id), kind) { Section = section };

    private static Layout Bar(IReadOnlyList<PartSpec> specs, Edge edge = Edge.Top, int height = 41, bool editing = true) =>
        Layout.ForTests(96, Area(1920, 1080), edge, new Config { Height = height }, specs, Layout.CellPx(96, new Config { Height = height }), editing: editing);

    /// <summary>A bar with something at its start and its end, and nothing in the middle.</summary>
    private static readonly PartSpec[] Full =
    [
        Spec("w", BarKind.WidgetsBoard),
        Spec("c", BarKind.NightLight),
        Spec("t", BarKind.Tray, BarSection.End),
        Spec("sd", BarKind.ShowDesktop, BarSection.End),
    ];

    /// <summary>Windows 11's three: the weather at the start, Start and a widget in the middle, the clock at the end.</summary>
    private static readonly PartSpec[] Three =
    [
        Spec("w", BarKind.WidgetsBoard),
        Spec("s", BarKind.Start, BarSection.Middle),
        Spec("m", BarKind.WidgetsBoard, BarSection.Middle),
        Spec("c", BarKind.NightLight, BarSection.End),
    ];

    /// <summary>Every slot in the list, counted as <see cref="Layout.DropSlot"/> counts them.</summary>
    private static int LastSlot(Layout layout, int dragging) =>
        Enumerable.Range(0, layout.Specs.Count).Count(part => part != dragging);

    // --- Both ends are reachable ----------------------------------------------------

    /// <summary>
    /// The pointer at the bar's very first pixel puts the block first in the start section, and at
    /// its very last pixel last in the end section — whichever block is being carried.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TheFirstAndLastPixelOfTheBarAreTheFirstAndLastSlot(int dragging)
    {
        var layout = Bar(Full);

        Assert.Equal(new BarSlot(BarSection.Start, 0), layout.DropSlot(0f, dragging));
        Assert.Equal(new BarSlot(BarSection.End, LastSlot(layout, dragging)), layout.DropSlot(layout.Length - 1f, dragging));
    }

    /// <summary>
    /// The bug itself, named so it cannot come back: the drop point must not be the block's middle
    /// kept on the bar. A block wider than the first widget can never bring that middle before the
    /// first widget's own, so the first slot was unreachable however hard the pointer was shoved.
    /// </summary>
    [Fact]
    public void TheBlocksClampedMiddleCannotReachTheFirstSlotButThePointerCan()
    {
        // The calendar three cells long, as a date with its day's name is.
        var config = new Config { Height = 41 };
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Top, config, Full, Layout.CellPx(96, config), editing: true, cells: [1, 3, 1, 1]);
        const int Dragging = 1;
        var width = layout.PartAt(Dragging).Width;
        Assert.True(width > layout.PartAt(0).Width, "the case is a block wider than the widget at the start");

        foreach (var grab in new[] { 0f, width / 2f })
        {
            var start = Math.Clamp(0f - grab, 0f, MathF.Max(0f, layout.Length - width));
            Assert.NotEqual(new BarSlot(BarSection.Start, 0), layout.DropSlot(start + (width / 2f), Dragging));
        }

        Assert.Equal(new BarSlot(BarSection.Start, 0), layout.DropSlot(0f, Dragging));
    }

    /// <summary>
    /// "Left and right" is before and after in the bar's own direction: on a bar down the side the
    /// first slot is at the top and the last at the bottom, and the same points reach them.
    /// </summary>
    [Theory]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void BothEndsAreReachableOnEveryEdge(Edge edge)
    {
        var layout = Bar(Full, edge);

        Assert.Equal(new BarSlot(BarSection.Start, 0), layout.DropSlot(0f, 1));
        Assert.Equal(new BarSlot(BarSection.End, LastSlot(layout, 1)), layout.DropSlot(layout.Length - 1f, 1));
    }

    // --- Which side of a widget -----------------------------------------------------

    /// <summary>The half of a widget the pointer is in says which side of it the block goes.</summary>
    [Fact]
    public void ThePointerPastAWidgetsMiddleGoesAfterIt()
    {
        var layout = Bar(Full);
        const int Dragging = 1;
        var tray = layout.PartAt(2);

        Assert.Equal(new BarSlot(BarSection.End, 2), layout.DropSlot(tray.Left + (tray.Width * 0.9f), Dragging));
        Assert.Equal(new BarSlot(BarSection.End, 1), layout.DropSlot(tray.Left + (tray.Width * 0.1f), Dragging));
    }

    // --- Sections -------------------------------------------------------------------

    /// <summary>
    /// The nearest gap within the section under the pointer: over the middle's blocks, a place among
    /// them; past the middle's last block, after it — in the middle, not at the start of the end.
    /// </summary>
    [Fact]
    public void ADropGoesToTheNearestGapInTheSectionUnderThePointer()
    {
        var layout = Bar(Three, editing: false);
        var start = layout.PartAt(1);
        var media = layout.PartAt(2);

        Assert.Equal(new BarSlot(BarSection.Middle, 1), layout.DropSlot(start.Left + 1f, -1));
        Assert.Equal(new BarSlot(BarSection.Middle, 2), layout.DropSlot(media.Left + 1f, -1));
        Assert.Equal(new BarSlot(BarSection.Middle, 3), layout.DropSlot(media.Left + media.Width + 2f, -1));
    }

    /// <summary>
    /// Between two sections, the nearer one has the drop: just past the start's last block is the
    /// start's end, just before the end's first block is the end's start.
    /// </summary>
    [Fact]
    public void TheRoomBetweenTwoSectionsBelongsToTheNearer()
    {
        var layout = Bar(Three, editing: false);
        var weather = layout.PartAt(0);
        var clock = layout.PartAt(3);

        Assert.Equal(new BarSlot(BarSection.Start, 1), layout.DropSlot(weather.Left + weather.Width + 20f, 2));
        Assert.Equal(new BarSlot(BarSection.End, 2), layout.DropSlot(clock.Left - 20f, 2));
    }

    /// <summary>
    /// At rest an empty middle is the bar's centre: something dragged there lands in the middle, at
    /// the place the middle's entries would begin in the list.
    /// </summary>
    [Fact]
    public void AtRestTheCentreOfABarWithAnEmptyMiddleIsTheMiddle()
    {
        var layout = Bar(Full, editing: false);

        Assert.Equal(new BarSlot(BarSection.Middle, 2), layout.DropSlot(layout.Length / 2f, -1));
    }

    /// <summary>
    /// While the bar is arranged an empty section is room of its own, which is where a block is
    /// dropped into it: the pointer on the middle's room lands the block in the middle.
    /// </summary>
    [Fact]
    public void InEditModeAnEmptySectionsRoomIsItsDropZone()
    {
        var layout = Bar(Full);
        var room = layout.SectionRect(BarSection.Middle);
        var along = room.X + (room.Width / 2f);

        Assert.Equal(new BarSlot(BarSection.Middle, 1), layout.DropSlot(along, 0));
        Assert.Equal(BarSection.Middle, layout.SectionAt(along));
    }

    /// <summary>
    /// A block carried into another section: the section it leaves closes up, the section it joins
    /// opens for it, and the third does not move a pixel.
    /// </summary>
    [Fact]
    public void DraggingBetweenSectionsMovesOnlyThoseTwo()
    {
        var layout = Bar(Three);
        var before = layout.Parts.Select(part => part.Left).ToArray();

        // The media widget (part 2) from the middle into the start, after the weather.
        var lefts = layout.Arrange(2, new BarSlot(BarSection.Start, 1));

        Assert.Equal(before[3], lefts[3]);
        Assert.Equal(before[0], lefts[0]);
        Assert.True(lefts[2] > before[0] && lefts[2] < before[1], "the media widget stands after the weather, at the start");
        // The middle, now only Start, is centred again on its own: it moved by half of what left it.
        Assert.NotEqual(before[1], lefts[1]);
    }

    /// <summary>A block dropped into an empty section stands where that section's room stood, the others not moving.</summary>
    [Fact]
    public void DraggingIntoAnEmptySectionPutsTheBlockThere()
    {
        var layout = Bar(Full);
        var plus = layout.SectionRect(BarSection.Middle);
        var before = layout.Parts.Select(part => part.Left).ToArray();

        var lefts = layout.Arrange(0, layout.DropSlot(plus.X + 1f, 0));

        Assert.Equal(before[2], lefts[2]);
        Assert.Equal(before[3], lefts[3]);
        Assert.True(Math.Abs(lefts[0] + (layout.PartAt(0).Width / 2f) - (layout.Length / 2f)) < layout.PartAt(0).Width,
            "the block carried into the empty middle stands in the middle of the bar");
    }
}
