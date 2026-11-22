// §621 (the person, 2026-11-09: "the ghost blue lines when placing are outlining the outside of the combined widgets, they should
// outline each widget individually"): the add page's ghost outlines each card of what it holds — each app button of a pick of
// them on the bar (Layout.GhostRects), each line of a ghost folder (FolderFlyout.ShowGhosts) — never one outline round them
// all; a single widget keeps its block's one outline. Its rectangles grow from where the ghost stood (EditLook.GhostFrom): each
// from its own where as many stood, else from the room all of them took — the empty ghost's one rectangle splitting into a
// pick's several, several gathering into one.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class GhostOutlineTests
{
    private static PartSpec Widget(string id, BarKind kind) => new(Config.IslandKey(null, id), kind) { Section = BarSection.Start };

    private static Layout Bar(params PartSpec[] specs)
    {
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        var area = new RECT { right = 3000, bottom = 1600 };
        return Layout.ForTests(96, area, Edge.Bottom, config, specs, Layout.CellPx(96, config), editing: true, tiles: 3);
    }

    [Fact]
    public void APickOfSeveralCardsIsOutlinedCardByCard()
    {
        var layout = Bar(Widget("s", BarKind.Start), Widget("apps", BarKind.Apps), Widget("n", BarKind.NightLight));

        var rects = layout.GhostRects(1);

        Assert.Equal(3, rects.Count);
        Assert.Equal(layout.Plates(1), rects);
        Assert.Equal([.. Enumerable.Range(0, 3).Select(layout.TileCell)], rects);
        // Each its own, the gap between them clear: none reaches into the next.
        for (var at = 1; at < rects.Count; at++)
        {
            Assert.True(rects[at].X - rects[at - 1].Right > 0f, $"buttons {at - 1} and {at} touch");
        }
        // Never one round them all.
        Assert.DoesNotContain(layout.BlockRect(1), rects);
    }

    [Fact]
    public void ASingleWidgetKeepsItsBlocksOneOutline()
    {
        var layout = Bar(Widget("s", BarKind.Start), Widget("apps", BarKind.Apps), Widget("n", BarKind.NightLight));

        Assert.Equal([layout.BlockRect(2)], layout.GhostRects(2));
        Assert.Equal([layout.BlockRect(0)], layout.GhostRects(0));
        Assert.Empty(layout.GhostRects(7));
    }

    [Fact]
    public void ARectangleGrowsFromItsOwnElseFromTheRoomAllTook()
    {
        var one = new RectF(10, 0, 40, 40);
        RectF[] three = [new(0, 0, 20, 40), new(30, 0, 20, 40), new(60, 5, 20, 30)];

        // Nothing stood: put where it goes.
        Assert.Null(EditLook.GhostFrom([], 1, 0));
        Assert.Null(EditLook.GhostFrom([], 3, 2));
        // As many: each from its own.
        Assert.Equal(one, EditLook.GhostFrom([one], 1, 0));
        Assert.Equal(three[1], EditLook.GhostFrom(three, 3, 1));
        // One splitting into three: each from the one.
        Assert.Equal(one, EditLook.GhostFrom([one], 3, 0));
        Assert.Equal(one, EditLook.GhostFrom([one], 3, 2));
        // Three gathering into one: from the room the three took.
        Assert.Equal(new RectF(0, 0, 80, 40), EditLook.GhostFrom(three, 1, 0));
        Assert.Equal(new RectF(0, 0, 80, 40), EditLook.Around(three));
        Assert.Equal(default, EditLook.Around([]));
    }
}
