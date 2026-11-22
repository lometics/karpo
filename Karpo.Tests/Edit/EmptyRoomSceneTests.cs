// Edit mode's sections on the dock's real scene (2026-10-07): a section with nothing in it is an empty
// block of its own (Layout.EmptyRooms), where the layout says it is and nowhere at rest; a carried
// block is drawn by the carry window, so the bar keeps it out of sight and shows a blue ghost of its
// size where it would land, the others sliding to make room; carried over another surface the bar
// closes up behind it with no ghost; and a block carried here from another surface shows its ghost
// at the size it will have here. The hatching that crawled over the free room is gone.
//
// Needs a compositor: KARPO_UI_TESTS=1, as the other scene tree tests.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>A widget at the start and one at the end: the middle is empty.</summary>
    private static List<PartSpec> NoMiddle() =>
    [
        Spec("w", BarKind.WidgetsBoard) with { Section = BarSection.Start },
        Spec("c", BarKind.NightLight) with { Section = BarSection.End },
    ];

    /// <summary>A widget in each section: the weather at the start, Start in the middle, the clock at the end.</summary>
    private static List<PartSpec> ThreeSections() =>
    [
        Spec("w", BarKind.WidgetsBoard) with { Section = BarSection.Start },
        Spec("s", BarKind.Start) with { Section = BarSection.Middle },
        Spec("c", BarKind.NightLight) with { Section = BarSection.End },
    ];

    /// <summary>In edit mode an empty section is an empty block, exactly where the layout says its room is.</summary>
    [UiFact]
    public void AnEmptySectionIsAnEmptyBlockInEditMode()
    {
        OnABar(48, NoMiddle(), (layout, scene) =>
        {
            var room = Assert.Single(layout.EmptyRooms());
            Assert.Equal([room], scene.EmptyBlocksShown());
            Assert.Null(scene.GhostShown);
        }, style: BarStyle.Islands);
    }

    /// <summary>A bar at rest shows no empty block, and no ghost.</summary>
    [UiFact]
    public void ABarAtRestShowsNoEmptyBlock()
    {
        OnABar(48, NoMiddle(), (_, scene) =>
        {
            Assert.Empty(scene.EmptyBlocksShown());
            Assert.Null(scene.GhostShown);
        }, editing: false);
    }

    /// <summary>
    /// A carried block: its ghost stands in the gap the others open for it, as long as it is, and the
    /// empty blocks follow the sections — the end it left is empty now. Put down, all goes back.
    /// </summary>
    [UiFact]
    public void TheGhostAndTheEmptyBlocksFollowACarriedBlock()
    {
        OnABar(48, ThreeSections(), (layout, scene) =>
        {
            var slot = new BarSlot(BarSection.Start, 1);
            Assert.Empty(scene.EmptyBlocksShown());

            scene.Edit(new EditView(Dragging: 2, PointerAlong: 60f, DropIndex: slot));

            var lefts = layout.Arrange(2, slot);
            var block = layout.BlockRect(2);
            var ghost = Assert.IsType<RectF>(scene.GhostShown);
            Assert.Equal(block.X + lefts[2] - layout.PartAt(2).Left, ghost.X, 0.5f);
            Assert.Equal(block.Width, ghost.Width, 0.5f);
            Assert.Equal(layout.EmptyRooms(2, slot), scene.EmptyBlocksShown());
            Assert.Single(scene.EmptyBlocksShown());

            scene.Edit(null);

            Assert.Null(scene.GhostShown);
            Assert.Empty(scene.EmptyBlocksShown());
        }, style: BarStyle.Islands);
    }

    /// <summary>Carried over another surface of the display, the bar it came from closes up behind it and shows no ghost.</summary>
    [UiFact]
    public void CarriedAwayTheBarClosesUpWithNoGhost()
    {
        OnABar(48, ThreeSections(), (layout, scene) =>
        {
            scene.Edit(new EditView(Dragging: 2, DropIndex: new BarSlot(BarSection.End, 2), Away: true));

            Assert.Null(scene.GhostShown);
            Assert.Equal(layout.Closed(2).Rooms, scene.EmptyBlocksShown());
            Assert.Single(scene.EmptyBlocksShown());
        }, style: BarStyle.Islands);
    }

    /// <summary>
    /// A block carried here from another surface shows its ghost where it would land, at the size it
    /// will have here (Layout.Landing with Layout.Carry), and the section it joins is no longer empty.
    /// </summary>
    [UiFact]
    public void ABlockCarriedHereShowsItsGhostAtItsSize()
    {
        OnABar(48, NoMiddle(), (layout, scene) =>
        {
            var from = Layout.ForTests(96, new RECT { right = 1600, bottom = 1200 }, Edge.Bottom, new Config { Height = 48 }, [Spec("m", BarKind.WidgetsBoard)], 40f, editing: true);
            var carried = Assert.IsType<Layout.Carried>(from.Carry(from.Specs[0].Key));
            var landed = layout.Landing(new BarSlot(BarSection.Middle, 1), carried);

            scene.Edit(new EditView(Offered: landed));

            Assert.Equal(landed.Rect, scene.GhostShown);
            Assert.Empty(scene.EmptyBlocksShown());
            Assert.Equal(from.BlockRect(0).Width, landed.Rect.Width, 0.5f);
        }, style: BarStyle.Islands);
    }
}
