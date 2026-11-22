// The bar's three sections (2026-10-05), on layouts measured without a Gfx (Layout.ForTests).
//
// The person's decisions, which every test here holds one of:
//   - Each section lays out on its own: a size change in one section only rearranges that section,
//     and the middle stays centred, independent of the left and the right.
//   - Collisions: the middle stays exactly centred as long as it fits; then it shifts as little as
//     keeps it off the left and the right; then the app buttons fold into "…" as before; then the
//     existing overflow rules.
//   - Vertical bars: the same three sections, top, middle and bottom.
//   - Edit mode: each section has its own "+" at its end, adding into that section; an empty
//     section still shows its "+" (and a drop zone) in edit mode, and takes no room at rest.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class SectionLayoutTests
{
    private const float Cell = 40f;

    /// <summary>The room a first or a last card keeps off the bar's ends (Layout.CardInsetPx).</summary>
    private static readonly float Inset = Layout.CardInsetPx(96, new Config());

    private static RECT Area(int length, Edge edge) => edge.IsVertical()
        ? new RECT { left = 0, top = 0, right = 48, bottom = length }
        : new RECT { left = 0, top = 0, right = length, bottom = 48 };

    private static PartSpec Spec(string id, BarKind kind, BarSection section) =>
        new(Config.IslandKey(null, id), kind) { Section = section };

    /// <summary>A bar of <paramref name="length"/> px: Weather at the start, Start and the apps in the middle, the clock at the end.</summary>
    private static Layout Bar(int tiles, int length = 1920, Edge edge = Edge.Bottom, bool editing = false, params PartSpec[] extra) =>
        Layout.ForTests(96, Area(length, edge), edge, new Config(),
        [
            Spec("w", BarKind.FolderList, BarSection.Start),
            .. extra.Where(spec => spec.Section == BarSection.Start),
            Spec("s", BarKind.Start, BarSection.Middle),
            Spec("a", BarKind.Apps, BarSection.Middle),
            .. extra.Where(spec => spec.Section == BarSection.Middle),
            Spec("c", BarKind.NightLight, BarSection.End),
            .. extra.Where(spec => spec.Section == BarSection.End),
        ], Cell, editing: editing, tiles: tiles);

    /// <summary>Where the middle section's parts start and end along the bar.</summary>
    private static (float From, float To) Middle(Layout layout)
    {
        var parts = Enumerable.Range(0, layout.Specs.Count)
            .Where(part => layout.Specs[part].Section == BarSection.Middle && layout.Placed(part))
            .Select(layout.PartAt)
            .ToList();
        return (parts.Min(part => part.Left), parts.Max(part => part.Right));
    }

    private static Part Of(Layout layout, string id) => layout.PartAt(layout.PartIndex(Config.IslandKey(null, id)));

    // --- Alignment ------------------------------------------------------------------

    /// <summary>The start's parts run from the bar's start, the end's to its end, the middle's centred on it.</summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void EachSectionIsAlignedToItsPlace(Edge edge)
    {
        var layout = Bar(3, edge: edge);

        Assert.Equal(Inset, Of(layout, "w").Left);
        Assert.Equal(layout.Length - Inset, Of(layout, "c").Right);
        var (from, to) = Middle(layout);
        Assert.True(Math.Abs((from + to) / 2f - layout.Length / 2f) <= 1f, $"the middle runs {from}–{to} on a bar of {layout.Length}");
    }

    // --- Independence -----------------------------------------------------------------

    /// <summary>
    /// App buttons coming and going are the middle's own business: the weather and the clock stay
    /// where they were to the pixel, and the middle grows about the centre.
    /// </summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(5, 2)]
    [InlineData(2, 12)]
    public void AppsComingAndGoingMoveOnlyTheMiddle(int before, int after)
    {
        var first = Bar(before);
        var then = Bar(after);

        Assert.Equal(Of(first, "w").Left, Of(then, "w").Left);
        Assert.Equal(Of(first, "c").Left, Of(then, "c").Left);
        var (from, to) = Middle(then);
        Assert.True(Math.Abs((from + to) / 2f - then.Length / 2f) <= 1f, "the middle stays centred as it grows and shrinks");
    }

    /// <summary>A section growing at the start or the end does not move the middle while there is room.</summary>
    [Fact]
    public void TheStartAndTheEndGrowingDoNotMoveTheMiddle()
    {
        var plain = Bar(3);
        var busy = Bar(3, extra:
        [
            Spec("w2", BarKind.FolderList, BarSection.Start),
            Spec("w3", BarKind.FolderList, BarSection.Start),
            Spec("n", BarKind.DoNotDisturb, BarSection.End),
            Spec("b", BarKind.EnergySaver, BarSection.End),
        ]);

        Assert.Equal(Middle(plain), Middle(busy));
        Assert.Equal(plain.Length - Inset, Of(busy, "b").Right);
    }

    /// <summary>Where the start's run ends, the end's begins, and the middle's sits, the three never overlap.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(20)]
    public void TheSectionsNeverOverlap(int tiles)
    {
        var layout = Bar(tiles, length: 900);
        var (from, to) = Middle(layout);

        Assert.True(Of(layout, "w").Right <= from);
        Assert.True(to <= Of(layout, "c").Left);
    }

    // --- Collisions -------------------------------------------------------------------

    /// <summary>
    /// A start section reaching past where the centred middle would begin: the middle moves off the
    /// centre, by exactly as much as keeps it a gap clear of the start's last part — no more.
    /// </summary>
    [Fact]
    public void TheMiddleShiftsAsLittleAsKeepsItClear()
    {
        PartSpec[] wide = [.. Enumerable.Range(0, 30).Select(at => Spec($"x{at}", BarKind.FolderList, BarSection.Start))];
        var layout = Bar(3, extra: wide);
        var startEnd = wide.Max(spec => layout.PartAt(layout.PartIndex(spec.Key)).Right);
        var (from, to) = Middle(layout);

        Assert.True((from + to) / 2f > layout.Length / 2f + 1f, "the middle has left the centre");
        Assert.Equal(startEnd + layout.SectionGap, from);
        Assert.Empty(layout.Overflowed);
    }

    /// <summary>Only once the middle has nowhere left to move do its app buttons fold into "…".</summary>
    [Fact]
    public void ThenTheAppsFoldIntoTheOverflow()
    {
        var layout = Bar(30, length: 1000);

        Assert.NotEmpty(layout.Overflowed);
        Assert.Empty(layout.NotShown);
        var (from, to) = Middle(layout);
        Assert.True(Of(layout, "w").Right <= from && to <= Of(layout, "c").Left);
    }

    /// <summary>And only then does a widget give way, the last placed first.</summary>
    [Fact]
    public void ThenWidgetsGiveWayTheLastFirst()
    {
        PartSpec[] many = [.. Enumerable.Range(0, 12).Select(at => Spec($"x{at}", BarKind.FolderList, BarSection.End))];
        var layout = Bar(4, length: 560, extra: many);

        Assert.NotEmpty(layout.NotShown);
        Assert.Equal(many[^1].Key, layout.NotShown[^1].Key);
        Assert.True(layout.Overflowed.Count > 0, "the apps folded before any widget gave way");
    }

    // --- Edit mode ----------------------------------------------------------------------

    /// <summary>
    /// Every section's adds go to its end — the place after its last entry in the list — and the
    /// section stands where its widgets do, which is where its edit bar goes beside it. The bar has
    /// no "+" of its own any more: a point on a section is its widget's, or the bar.
    /// </summary>
    [Fact]
    public void EverySectionsAddsGoToItsEnd()
    {
        var layout = Bar(3, editing: true);

        Assert.Equal([1, 3, 4], ((BarSection[])[BarSection.Start, BarSection.Middle, BarSection.End]).Select(section => layout.EndSlot(section).Index));
        Assert.True(layout.SectionRect(BarSection.Start).X <= Of(layout, "w").Left && layout.SectionRect(BarSection.Start).Right >= Of(layout, "w").Right);
        Assert.True(layout.SectionRect(BarSection.End).Right <= layout.Length && layout.SectionRect(BarSection.End).Right >= Of(layout, "c").Right);
        Assert.Empty(layout.EmptyRooms());
    }

    /// <summary>
    /// An empty section is room of its own while the bar is arranged — three cells long, the
    /// middle's at the bar's centre — where a block is dropped into it; at rest it takes no room.
    /// </summary>
    [Fact]
    public void AnEmptySectionIsThreeCellsInEditModeAndNothingAtRest()
    {
        PartSpec[] specs = [Spec("w", BarKind.FolderList, BarSection.Start), Spec("c", BarKind.NightLight, BarSection.End)];
        var rest = Layout.ForTests(96, Area(1920, Edge.Bottom), Edge.Bottom, new Config(), specs, Cell);
        var edit = Layout.ForTests(96, Area(1920, Edge.Bottom), Edge.Bottom, new Config(), specs, Cell, editing: true);

        Assert.Empty(rest.EmptyRooms());
        Assert.True(rest.SectionRect(BarSection.Middle).IsEmpty);
        Assert.Equal(rest.Length - Inset, rest.PartAt(1).Right);
        var room = Assert.Single(edit.EmptyRooms());
        Assert.Equal(edit.SectionRect(BarSection.Middle).X, room.X);
        Assert.True(room.Width >= Layout.EmptyCells * edit.SmallWell, "three cells at least");
        Assert.True(room.Width < (Layout.EmptyCells + 1) * edit.SmallWell, "and not four");
        Assert.True(Math.Abs(room.X + room.Width / 2f - edit.Length / 2f) <= 1f);
        Assert.Equal(new BarSlot(BarSection.Middle, 1), edit.EndSlot(BarSection.Middle));
    }

    /// <summary>The room around and between the sections is the bar's own, at rest: the bar's menu opens there.</summary>
    [Fact]
    public void TheRoomBetweenSectionsIsTheBar()
    {
        var layout = Bar(2);
        var gap = (Of(layout, "w").Right + Middle(layout).From) / 2f;

        Assert.Equal(Hit.Panel, layout.HitTest(gap, layout.PanelRect.Y + layout.PanelRect.Height / 2f));
    }

    /// <summary>The keys walk the bar left to right in edit mode: each section's blocks.</summary>
    [Fact]
    public void TheKeysWalkTheSectionsInOrder()
    {
        var layout = Bar(2, editing: true);

        var stops = EditStops.Order(layout);

        Assert.Equal([0, 1, 2, 3], stops);
    }
}
