// The dock's side of folders and of the edit mode's "Add", without a window:
// the folder list's plan (FolderFlyout.Build) and where "Add" marks the bar
// (Layout.InsertionRect). A block dragged along the bar goes into no folder: nothing
// moves between the taskbar and a folder (Partners).

using Karpo.Dock;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderDockTests
{
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>A folder of one list of <paramref name="kinds"/>, in their order.</summary>
    private static FolderOptions Folder(params BarKind[] kinds) =>
        new() { Lists = CellItems.OneList(kinds.Select(kind => kind.New().AsCell())) };

    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind, BarSection section = BarSection.Start) =>
        new(Config.IslandKey("layout:default", id), kind) { Section = section };

    private static float[] Lefts(Layout layout) => [.. Enumerable.Range(0, layout.Specs.Count).Select(part => layout.PartAt(part).Left)];

    // --- The folder's list ------------------------------------------------------

    [Fact]
    public void AListHasALinePerWidgetAcrossThePanel()
    {
        var options = Folder(BarKind.Pin, BarKind.AirplaneMode);

        var plan = FolderFlyout.Build(Light, 1f, FolderView.List, options, options.Cells, 1040);

        Assert.Equal(FolderView.List, plan.View);
        Assert.Equal(2, plan.Cards.Count);
        // Across the panel means across the panel: a list's items reach its own border, with no
        // inset between a card and the edge (the row's own inset lives inside the card).
        Assert.All(plan.Cards, card => Assert.Equal(plan.Width, card.right - card.left));
        // The pin first: it comes first in its list.
        Assert.True(plan.Cards[0].top < plan.Cards[1].top);
        Assert.Empty(plan.Empty);
    }

    [Fact]
    public void AnEmptyFolderStillShowsWhereWidgetsGo()
    {
        var plan = FolderFlyout.Build(Light, 1.5f, FolderView.List, new FolderOptions(), [], 1040);

        Assert.Empty(plan.Cards);
        Assert.NotEmpty(plan.Empty);
        Assert.True(plan.Lists[0].Height > 0);
        Assert.Equal(plan.Lists[0].Height, plan.Height, 3);
    }

    [Fact]
    public void ALongListShowsWhatFitsInTheRoomBesideTheBar()
    {
        var options = Folder([.. Enumerable.Repeat(BarKind.Pin, 30)]);

        var plan = FolderFlyout.Build(Light, 1f, FolderView.List, options, options.Cells, 600);

        Assert.True(plan.Height <= FolderFlyout.Reach(600, 0, 1));
        // The rest is not shown, whole lines from the end, and nothing scrolls (2026-10-12).
        var shown = Enumerable.Range(0, plan.Cards.Count).Count(plan.Shows);
        Assert.Equal(Enumerable.Range(shown, 30 - shown), plan.Lists[0].Hidden);
        Assert.Equal(plan.Lists[0].Box.bottom, plan.Cards[shown - 1].bottom, 3);
    }

    [Fact]
    public void AShapeThisBuildDoesntKnowShowsTheList()
    {
        var options = Folder(BarKind.WidgetsBoard);
        var plan = FolderFlyout.Build(Light, 1f, (FolderView)42, options, options.Cells, 1040);

        Assert.Equal(FolderView.List, plan.View);
        Assert.Single(plan.Cards);
    }

    // --- The "Add" marker --------------------------------------------------------

    [Fact]
    public void TheAddMarkerStandsBetweenTheBlocksItGoesBetween()
    {
        List<PartSpec> specs = [Spec("w", BarKind.WidgetsBoard), Spec("m", BarKind.WidgetsBoard), Spec("b", BarKind.EnergySaver)];
        // The gap the marker stands in is the islands look's.
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config { Style = BarStyle.Islands }, specs, 60f, editing: true);

        var first = layout.InsertionRect(new BarSlot(BarSection.Start, 0));
        var between = layout.InsertionRect(new BarSlot(BarSection.Start, 1));
        var last = layout.InsertionRect(new BarSlot(BarSection.Start, 3));
        var past = layout.InsertionRect(new BarSlot(BarSection.Start, 99));

        Assert.False(between.IsEmpty);
        // The bar keeps no room at its ends any more, so the line for "before the first" has
        // nowhere to stand but on the first block's leading edge: it is clamped onto the bar
        // rather than drawn off it.
        Assert.Equal(Layout.CardInsetPx(96, new Config()), layout.PartAt(0).Left);
        Assert.True(first.X >= 0f && first.X <= layout.PartAt(0).Left + first.Width);
        Assert.True(between.X + (between.Width / 2f) >= layout.PartAt(0).Left + layout.PartAt(0).Width - 1f);
        Assert.True(between.X + (between.Width / 2f) <= layout.PartAt(1).Left + 1f);
        Assert.True(last.X + (last.Width / 2f) >= layout.PartAt(2).Left + layout.PartAt(2).Width - 1f);
        Assert.Equal(last, past);
        // Across the bar, clear of its edges.
        Assert.True(between.Height > between.Width);
        Assert.True(between.Height < layout.Thickness);
    }

    /// <summary>
    /// A bar with nothing on it still says where the widget will land: the marker stands in the
    /// start's empty room, which is where the first widget will be.
    /// </summary>
    [Fact]
    public void TheAddMarkerOnAnEmptyBarStandsAtItsStart()
    {
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), [], 60f, editing: true);

        var marker = layout.InsertionRect(new BarSlot(BarSection.Start, 0));

        Assert.False(marker.IsEmpty);
        Assert.True(marker.X >= 0f, "on the bar");
        Assert.True(marker.X + marker.Width <= layout.SectionRect(BarSection.Start).Right, "within the start's empty room");
        Assert.True(marker.Height > marker.Width, "and across the bar");
    }

    [Fact]
    public void TheAddMarkerRunsAlongAVerticalBarsWidth()
    {
        List<PartSpec> specs = [Spec("w", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight)];
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Left, new Config(), specs, 60f, editing: true);

        var marker = layout.InsertionRect(new BarSlot(BarSection.Start, 1));

        Assert.True(marker.Width > marker.Height);
        Assert.True(marker.Y >= layout.PartAt(0).Left + layout.PartAt(0).Width - 1f);
    }

    // --- No "Add" button, and no room kept for one -------------------------------

    private static List<PartSpec> ABar() =>
    [
        Spec("w", BarKind.WidgetsBoard),
        Spec("d", BarKind.WidgetsBoard),
        Spec("s", BarKind.Start, BarSection.Middle),
        Spec("a", BarKind.Apps, BarSection.Middle),
        Spec("t", BarKind.Tray, BarSection.End),
        Spec("c", BarKind.NightLight, BarSection.End),
    ];

    /// <summary>
    /// Past a section's "+" there is nothing to press: edit mode hit-tests to blocks, the "+" at
    /// each section's end and the grip, and to nothing else. A press on the bar past the last block
    /// and its section's "+" does nothing at all.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void EditModeHasNothingToPressPastTheLastBlock(Edge edge)
    {
        List<PartSpec> specs = [Spec("w", BarKind.WidgetsBoard), Spec("d", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight)];
        var layout = Layout.ForTests(96, Area(1920, 1200), edge, new Config(), specs, 60f, editing: true);
        var last = layout.PartAt(specs.Count - 1);
        var past = last.Left + last.Width + 60f;
        var across = layout.Thickness / 2f;

        var hit = layout.Vertical ? layout.HitTest(across, past, true) : layout.HitTest(past, across, true);

        Assert.True(hit is null or { Kind: HitKind.Panel }, $"nothing to press past the blocks, got {hit}");
    }

    /// <summary>
    /// Edit mode keeps no room on the bar for a "+" — each section's is on its edit bar beside it
    /// (2026-10-07) — so the widgets stand where they stood at rest, the end's clock still flush with
    /// the bar's end; only the empty middle takes room of its own.
    /// </summary>
    [Fact]
    public void EditModeKeepsNoRoomForAPlusOnTheBar()
    {
        List<PartSpec> specs = [Spec("s", BarKind.Start), Spec("c", BarKind.NightLight, BarSection.End)];
        var rest = Layout.ForTests(96, Area(1920, 1200), Edge.Bottom, new Config(), specs, 60f);
        var editing = Layout.ForTests(96, Area(1920, 1200), Edge.Bottom, new Config(), specs, 60f, editing: true);

        Assert.Equal(rest.PartAt(1).Left + rest.PartAt(1).Width, rest.Length - Layout.CardInsetPx(96, new Config()), 1f);
        Assert.Equal(rest.PartAt(0).Left, editing.PartAt(0).Left, 1f);
        Assert.Equal(rest.PartAt(1).Left, editing.PartAt(1).Left, 1f);
        Assert.Equal(new BarSlot(BarSection.End, specs.Count), editing.EndSlot(BarSection.End));
        Assert.Single(editing.EmptyRooms());
    }
    /// <summary>
    /// Entering edit mode shows you your bar. An empty part wears a placeholder block so there is
    /// something to take hold of, but that block reserves nothing: on a bar with no room to spare
    /// it is the first thing given up. An empty section's room is different: it is where a block is
    /// dropped into that section, so it is always there, and a bar with no other room makes it: the
    /// last widget gives way, and the bar says so (NotShown, which the section's edit bar lists).
    /// Every other widget stands where it stood at rest.
    /// </summary>
    [Fact]
    public void EditModeGivesUpAPlaceholderBeforeAWidget()
    {
        List<PartSpec> specs =
        [
            Spec("a", BarKind.Apps),
            Spec("w", BarKind.WidgetsBoard),
            Spec("d", BarKind.WidgetsBoard),
            Spec("b", BarKind.EnergySaver),
            Spec("c", BarKind.ShowDesktop),
        ];
        // A bar exactly as long as its widgets need at rest: Apps has no windows, so it measures
        // nothing there and there is no room anywhere for the block it wears while editing.
        var roomy = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 60f);
        var last = roomy.PartAt(specs.Count - 1);
        var exact = Area((int)MathF.Ceiling(last.Left + last.Width + Layout.CardInsetPx(96, new Config())), 1080);

        var rest = Layout.ForTests(96, exact, Edge.Bottom, new Config(), specs, 60f);
        var editing = Layout.ForTests(96, exact, Edge.Bottom, new Config(), specs, 60f, editing: true);

        Assert.False(rest.Placed(0), "Apps has nothing to show at rest");
        Assert.False(editing.Placed(0), "the placeholder goes first");
        Assert.Equal(new BarSlot(BarSection.Start, specs.Count), editing.EndSlot(BarSection.Start));
        // The widgets that gave way are the last placed, the battery first of all; show desktop, which
        // is no widget, stays, and everything before them stands where it stood at rest.
        Assert.NotEmpty(editing.NotShown);
        Assert.Equal(BarKind.EnergySaver, editing.NotShown[^1].Kind);
        var gaveWay = editing.NotShown.Select(spec => specs.IndexOf(spec)).ToList();
        Assert.Equal(Enumerable.Range(specs.Count - 1 - gaveWay.Count, gaveWay.Count), gaveWay);
        for (var part = 1; part < specs.Count; part++)
        {
            Assert.True(rest.Placed(part), $"{specs[part].Kind} is on the bar at rest");
            if (gaveWay.Contains(part))
            {
                continue;
            }
            Assert.True(editing.Placed(part), $"{specs[part].Kind} is still on the bar while it is edited");
            if (part < gaveWay.Min())
            {
                Assert.Equal(rest.PartAt(part).Left, editing.PartAt(part).Left, 1f);
            }
        }
    }

    /// <summary>
    /// The widget the add list adds at a section's end lands where the marker said it would:
    /// after the section's last block.
    /// </summary>
    [Fact]
    public void AWidgetAddedAtTheEndLandsWhereTheMarkerStood()
    {
        List<PartSpec> bar = [Spec("w", BarKind.WidgetsBoard), Spec("d", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight)];
        // The gap the marker stands in is the islands look's.
        var before = Layout.ForTests(96, Area(1920, 1200), Edge.Bottom, new Config { Style = BarStyle.Islands }, bar, 60f, editing: true);
        var marker = before.InsertionRect(new BarSlot(BarSection.Start, bar.Count));

        List<PartSpec> specs = [.. bar, Spec("new", BarKind.EnergySaver)];
        var after = Layout.ForTests(96, Area(1920, 1200), Edge.Bottom, new Config { Style = BarStyle.Islands }, specs, 60f, editing: true);
        var added = after.PartAt(specs.Count - 1);

        // The marker stands in the gap the widget will take: after the last block, and no further
        // along than the widget's own start. The gap is the tolerance — the line is thinner than
        // it and has to fit inside it, so asking for the two to share an x would be asking for
        // something the marker is not.
        var last = before.PartAt(bar.Count - 1);
        var centre = marker.X + (marker.Width / 2f);
        Assert.True(centre >= last.Left + last.Width - 1f, $"the marker at {centre} is after the last block, which ends at {last.Left + last.Width}");
        Assert.True(centre <= added.Left + 1f, $"the marker at {centre} is no further than the widget it names, which starts at {added.Left}");
    }

    /// <summary>
    /// "Add to taskbar…" adds where the menu was opened: the place a point on the bar names is
    /// the one a drag to that point would give (Dock's SlotAt is this call), and the marker for
    /// it stands at that point, between the two blocks it goes between.
    /// </summary>
    [Fact]
    public void TheBarsMenuAddsWhereItWasOpened()
    {
        List<PartSpec> specs = [Spec("w", BarKind.WidgetsBoard), Spec("m", BarKind.WidgetsBoard), Spec("b", BarKind.EnergySaver)];
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config { Style = BarStyle.Islands }, specs, 60f, editing: true);
        var second = layout.PartAt(1);

        var atStart = layout.DropSlot(layout.PartAt(0).Left + 1f, -1);
        var pastSecond = layout.DropSlot(second.Left + second.Width - 1f, -1);
        var marker = layout.InsertionRect(pastSecond);

        Assert.Equal(new BarSlot(BarSection.Start, 0), atStart);
        Assert.Equal(new BarSlot(BarSection.Start, 2), pastSecond);
        Assert.True(marker.X + (marker.Width / 2f) >= second.Left + second.Width - 1f, "the marker follows the block the menu was opened past");
        Assert.True(marker.X + (marker.Width / 2f) <= layout.PartAt(2).Left + 1f);
    }

    /// <summary>
    /// A widget added at the end of the end section is the bar's last, flush with its end; the
    /// end's own parts make room for it by moving back, and the start does not move.
    /// </summary>
    [Fact]
    public void TheEndSectionMakesRoomForANewWidgetByMovingBack()
    {
        var specs = ABar();
        var before = Layout.ForTests(96, Area(1920, 1200), Edge.Bottom, new Config(), specs, 60f);
        var wasLast = before.PartAt(specs.Count - 1);

        specs.Add(Spec("new", BarKind.EnergySaver, BarSection.End));
        var after = Layout.ForTests(96, Area(1920, 1200), Edge.Bottom, new Config(), specs, 60f);
        var added = after.PartAt(specs.Count - 1);

        Assert.True(added.Left > after.PartAt(specs.Count - 2).Left, "the new widget is last");
        Assert.Equal(after.Length - Layout.CardInsetPx(96, new Config()), added.Left + added.Width, 1f);
        Assert.True(after.PartAt(specs.Count - 2).Left < wasLast.Left, "the end's run moved back");
        Assert.Equal(before.PartAt(0).Left, after.PartAt(0).Left);
        Assert.Equal(before.PartAt(2).Left, after.PartAt(2).Left);
    }

    // --- Dropping a block onto a folder -------------------------------------------

    /// <summary>
    /// A block dragged along the bar goes into no folder (Partners: nothing moves between the
    /// taskbar and a folder): over a folder's button it moves along the bar as over any other
    /// block, the folder's button among them, and nothing lights for a drop.
    /// </summary>
    [Fact]
    public void ADraggedWidgetMovesPastAFolderAndNotIntoIt()
    {
        List<PartSpec> specs =
        [
            Spec("s", BarKind.Start),
            Spec("w", BarKind.WidgetsBoard),
            Spec("f", BarKind.FolderList),
            Spec("b", BarKind.EnergySaver),
        ];
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, new Config(), specs, 60f, editing: true);
        // Past the folder's middle but inside its middle half, where a drop once went into it.
        var over = layout.PartAt(2).Left + layout.PartAt(2).Width * 0.6f;

        var slot = layout.DropSlot(over, 1);
        var lefts = layout.Arrange(1, slot);

        Assert.True(lefts[2] < layout.PartAt(2).Left, "the folder's button moved back into the weather's place");
    }
}
