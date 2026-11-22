// A folder's style (§647; the person, 2026-11-09: "for every folder you should be able to also set a folder style. right
// now how it is should be the default one. one other folder style should be that in case of showing the taskbar on the
// left or right side the whole columns where the widgets are placed should be filled with a solid background color, or
// when the tasbar is shown on the bottom or top that the whole area up the highest or lowest widget is filled with a solid
// color ... and this solid area should connect all the way to the taskbar so that it looks like it is part of it"). Held here:
//
//  - **The file**: "style" in the folder's options, written only where it is Attached; a file without it, or with a word
//    this build does not know, is Cards — every folder as it stood.
//  - **The row**: the folder's page, Folder style, a dropdown of Cards and Attached, Cards in force. §656 on a dock's folder
//    no such row, and the folder opens as cards whatever its style says, which is kept for a taskbar.
//  - **The rule** (pure): §651 the ground the bar grown out — along the bar the work area's whole length, edge to edge,
//    across it from the bar's inner side to the panel's far side —, the panel flush on the bar and free to reach the work
//    area's ends, never past the work area; the anchor moved onto the bar's inner side; the drawer's way back into the bar
//    its whole depth; the ground the bar's colour as the bar paints it; the columns a seam apart, no air.
//  - **The open folder** (KARPO_UI_TESTS=1): on every edge the ground adjoins the bar — no gap — and runs from end to end,
//    the columns flush on the bar whatever the face, a seam between them, a press on the ground is the folder's; Cards
//    unchanged; the style changed while it stands open; the drawer with Animations on, at once with them off.

using System.Text.Json;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderStyleTests
{
    private const string Owner = "layout:default";
    private static readonly Look Dark = AddListPictures.LookFor(dark: true) with { Animations = false };

    /// <summary>A 1920 × 1080 display, its bar 48 DIP along an edge.</summary>
    private static readonly RECT Display = new() { right = 1920, bottom = 1080 };

    private static int Px(float dip, float scale) => (int)MathF.Round(dip * scale, MidpointRounding.AwayFromZero);

    /// <summary>The work area of a bar <paramref name="bar"/> px thick along <paramref name="edge"/> (DockWindow.FolderWork).</summary>
    private static RECT WorkOf(Edge edge, int bar) => edge switch
    {
        Edge.Top => Display with { top = bar },
        Edge.Left => Display with { left = bar },
        Edge.Right => Display with { right = Display.right - bar },
        _ => Display with { bottom = Display.bottom - bar },
    };

    /// <summary>The middle of a face's inner side at <paramref name="along"/>, <paramref name="inset"/> px inside the bar (an island's margin).</summary>
    private static POINT FaceAnchor(RECT work, Edge edge, int along, int inset = 0) => edge switch
    {
        Edge.Top => new POINT { x = along, y = work.top - inset },
        Edge.Left => new POINT { x = work.left - inset, y = along },
        Edge.Right => new POINT { x = work.right + inset, y = along },
        _ => new POINT { x = along, y = work.bottom + inset },
    };

    // --- The file and the row ------------------------------------------------------------------

    [Fact]
    public void ANewFolderAndAFileWithoutOneAreCardsWhichIsHowEveryFolderStood()
    {
        Assert.Equal(FolderStyle.Cards, new FolderOptions().Style);
        Assert.Equal(FolderStyle.Cards, BarKind.FolderList.New().Folder!.Style);
        Assert.Equal([FolderStyle.Cards, FolderStyle.Attached], FolderStyles.All);
    }

    [Fact]
    public void TheFileKeepsAStyleOnlyWhereItIsAttached()
    {
        var folder = BarKind.FolderList.New();
        Assert.DoesNotContain("\"cards\"", JsonSerializer.Serialize(folder, ConfigJson.Default.BarItem), StringComparison.Ordinal);
        folder.Folder!.Style = FolderStyle.Attached;
        var written = JsonSerializer.Serialize(folder, ConfigJson.Default.BarItem);
        Assert.Contains("\"style\":\"attached\"", written.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
        var again = JsonSerializer.Deserialize(written, ConfigJson.Default.BarItem)!;
        again.Normalize();
        Assert.Equal(FolderStyle.Attached, again.Folder!.Style);
        // Back to Cards: written as nothing again.
        again.Folder.Style = FolderStyle.Cards;
        Assert.DoesNotContain("\"cards\"", JsonSerializer.Serialize(again, ConfigJson.Default.BarItem), StringComparison.Ordinal);
        // A word this build does not know is Cards, as a file without one.
        var odd = written.Replace("\"attached\"", "\"floating\"", StringComparison.Ordinal);
        var read = JsonSerializer.Deserialize(odd, ConfigJson.Default.BarItem)!;
        read.Normalize();
        Assert.Equal(FolderStyle.Cards, read.Folder!.Style);
    }

    /// <summary>The folder's own rows: its alignment, then Folder style — a dropdown of Cards and Attached, each its icon —, Cards in force; a pick written on the folder.</summary>
    [Fact]
    public void TheFoldersPageSetsItsStyle()
    {
        var item = BarKind.FolderList.New();
        Assert.Equal([OptionRows.FolderStyle, OptionRows.FolderAlign], OptionRows.Of(item).Select(row => row.Key));
        var row = OptionRows.Find(OptionRows.Of(item), OptionRows.FolderStyle)!;
        Assert.Equal((OptionRowKind.Choice, OptionSlot.Look, S.DockFolderStyleTitle, false, 0), (row.Kind, row.Slot, row.Title, row.Segmented, row.Chosen));
        Assert.Equal([S.DockFolderStyleCards, S.DockFolderStyleAttached], row.Choices);
        Assert.Equal([OptionGlyphs.Tiles, OptionGlyphs.Taskbar], row.ChoiceGlyphs);
        Assert.False(row.Set!(0));
        Assert.True(row.Set!(1));
        Assert.Equal(FolderStyle.Attached, item.Folder!.Style);
        Assert.Equal(1, OptionRows.Find(OptionRows.Of(item), OptionRows.FolderStyle)!.Chosen);
        Assert.True(OptionRows.Find(OptionRows.Of(item), OptionRows.FolderStyle)!.Set!(0));
        Assert.Equal(FolderStyle.Cards, item.Folder.Style);
        // Only a folder's.
        Assert.DoesNotContain(OptionRows.Of(BarKind.Pin.New()), each => each.Key == OptionRows.FolderStyle);
        Assert.DoesNotContain(OptionRows.Of(ListKind.Heading.New()), each => each.Key == OptionRows.FolderStyle);
    }

    /// <summary>
    /// §656 (the person, 2026-11-09: "in dock mode you should not be able to have the folder docked too"): on a dock's folder its
    /// page has no Folder style row — not greyed, not there —; on a taskbar's it has, as on a bar of no layout.
    /// </summary>
    [Fact]
    public void OnADockTheFoldersPageHasNoStyleRow()
    {
        var config = MenuCorpus.Fresh();
        var taskbar = new TaskbarLayout { Type = LayoutType.Taskbar };
        var dock = new TaskbarLayout { Type = LayoutType.Dock };
        var item = BarKind.FolderList.New();
        item.Folder!.Style = FolderStyle.Attached;
        bool Has(TaskbarLayout? layout)
        {
            var place = new OptionPlace(config, Dark: true) { Layout = layout };
            var page = SettingsPanelTests.FolderPage(item, place);
            var row = OptionRows.Of(item, place: place).Any(each => each.Key == OptionRows.FolderStyle);
            Assert.Equal(row, page.Rows.Any(each => each.Text == S.DockFolderStyleTitle));
            return row;
        }
        Assert.True(Has(taskbar));
        Assert.False(Has(dock));
        // A dock's folder keeps its style in the file: the layout a taskbar again, the row is back, Attached in force.
        Assert.Equal(FolderStyle.Attached, item.Folder.Style);
        dock.Become(LayoutType.Taskbar);
        Assert.True(Has(dock));
        Assert.Equal(1, OptionRows.Find(OptionRows.Of(item, place: new OptionPlace(config, Dark: true) { Layout = dock }), OptionRows.FolderStyle)!.Chosen);
    }

    /// <summary>§656: the style a folder opens in: its own beside a taskbar, Cards beside a dock; a taskbar's offers the row, a dock's not.</summary>
    [Fact]
    public void OnADockAFolderIsWornAsCards()
    {
        Assert.Equal(FolderStyle.Attached, FolderStyles.Worn(FolderStyle.Attached, dock: false));
        Assert.Equal(FolderStyle.Cards, FolderStyles.Worn(FolderStyle.Cards, dock: false));
        Assert.Equal(FolderStyle.Cards, FolderStyles.Worn(FolderStyle.Attached, dock: true));
        Assert.Equal(FolderStyle.Cards, FolderStyles.Worn(FolderStyle.Cards, dock: true));
        Assert.True(FolderStyles.Offered(new TaskbarLayout { Type = LayoutType.Taskbar }));
        Assert.True(FolderStyles.Offered(null));
        Assert.False(FolderStyles.Offered(new TaskbarLayout { Type = LayoutType.Dock }));
    }

    /// <summary>§656: the Appearance's preview of a dock's bar shows its folder as cards, even where an attached folder's background is said.</summary>
    [Fact]
    public void OnADockTheAppearancePreviewsFolderStandsAsCards()
    {
        var config = new Config();
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Flyouts), Tone.Ground, true, new Karpo.Core.Paint(new Argb(255, 0x1A, 0x3A, 0x63))));
        var look = AddListPictures.LookFor(dark: true).Wearing(config);
        Assert.NotNull(LookPreview.GroundOf(look, new TaskbarLayout { Type = LayoutType.Taskbar }));
        Assert.Null(LookPreview.GroundOf(look, new TaskbarLayout { Type = LayoutType.Dock }));
    }

    // --- The rule ---------------------------------------------------------------------------------

    /// <summary>
    /// §651 (the person: "the background should always span from edge to edge"): the ground of a 600 × 400 px panel, in panel
    /// px, is the bar grown out: along the bar the work area's whole length, end to end; across it from the bar's inner side to
    /// the panel's far side. The panel stands flush on the bar (no gap) and, against an end, flush with it (no margin).
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void TheGroundIsTheBarGrownOutFromEdgeToEdge(float scale)
    {
        var bar = Px(48, scale);
        const int w = 600, h = 400;
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        {
            var work = WorkOf(edge, bar);
            var anchor = FolderFlyout.AtBar(FaceAnchor(work, edge, 900), work, edge);
            var at = new FlyoutPlacement(anchor, work, edge).Flush.PanelAt(w, h, scale);
            var ground = FlyoutHost.GroundOf(edge, work, at.left, at.top, w, h);
            // On the screen.
            var (left, top, right, bottom) = (at.left + ground.left, at.top + ground.top, at.left + ground.right, at.top + ground.bottom);
            switch (edge)
            {
                case Edge.Bottom:
                    Assert.Equal(((float)work.left, (float)at.top, (float)work.right, (float)work.bottom), (left, top, right, bottom));
                    Assert.Equal(work.bottom, at.bottom);
                    break;
                case Edge.Top:
                    Assert.Equal(((float)work.left, (float)work.top, (float)work.right, (float)at.bottom), (left, top, right, bottom));
                    Assert.Equal(work.top, at.top);
                    break;
                case Edge.Left:
                    Assert.Equal(((float)work.left, (float)work.top, (float)at.right, (float)work.bottom), (left, top, right, bottom));
                    Assert.Equal(work.left, at.left);
                    break;
                default:
                    Assert.Equal(((float)at.left, (float)work.top, (float)work.right, (float)work.bottom), (left, top, right, bottom));
                    Assert.Equal(work.right, at.right);
                    break;
            }
            // Against either end flush with it: the ground's end is the columns' edge, no margin of air.
            var vertical = edge is Edge.Left or Edge.Right;
            var start = new FlyoutPlacement(anchor, work, edge, Align: FlyoutAlign.Start).Flush.PanelAt(w, h, scale);
            var end = new FlyoutPlacement(anchor, work, edge, Align: FlyoutAlign.End).Flush.PanelAt(w, h, scale);
            Assert.Equal(vertical ? work.top : work.left, vertical ? start.top : start.left);
            Assert.Equal(vertical ? work.bottom : work.right, vertical ? end.bottom : end.right);
            // As Cards, as ever: the gap off the bar and the margin off the ends.
            var cards = new FlyoutPlacement(anchor, work, edge, Align: FlyoutAlign.End).PanelAt(w, h, scale);
            Assert.Equal((vertical ? work.bottom : work.right) - Px(12, scale), vertical ? cards.bottom : cards.right);
        }
    }

    /// <summary>The ground never runs past the work area: not under another bar, not off the screen, whatever the panel.</summary>
    [Fact]
    public void TheGroundNeverRunsPastTheWorkArea()
    {
        var work = WorkOf(Edge.Bottom, 72);
        // A panel 4 px into the work area: its ground still from the work area's start to its end.
        var ground = FlyoutHost.GroundOf(Edge.Bottom, work, 4, 518, 600, 490);
        Assert.Equal((-4f, 0f, 1916f, 490f), (ground.left, ground.top, ground.right, ground.bottom));
        // Taller than the room and pushed past its top: from the work area's top.
        var tall = FlyoutHost.GroundOf(Edge.Bottom, work with { top = 40 }, 400, 10, 600, 998);
        Assert.Equal((30f, 998f), (tall.top, tall.bottom));
        // Down a side bar always the whole height, whatever the panel; from the bar to the panel's far side.
        var side = FlyoutHost.GroundOf(Edge.Right, WorkOf(Edge.Right, 72), 1448, 300, 400, 200);
        Assert.Equal((0f, -300f, 400f, 780f), (side.left, side.top, side.right, side.bottom));
        // A panel wider than the room beside a side bar: the ground stops at the work area's far edge.
        var wide = FlyoutHost.GroundOf(Edge.Left, WorkOf(Edge.Left, 72), 72, 300, 2400, 200);
        Assert.Equal(1920f - 72f, wide.right);
    }

    /// <summary>
    /// §651: attached, two columns meet with a seam between them — the folder's outline's weight, a whole px or two, no air —, the
    /// seam where the plan has it; as cards they stand a separator's room apart, as ever.
    /// </summary>
    [Theory]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 2f)]
    [InlineData(2f, 2f)]
    public void AttachedColumnsMeetAtASeam(float scale, float seam)
    {
        var holder = Shaped("a b c | d e", FolderStyle.Attached);
        var cells = holder.Folder!.Lists.SelectMany(list => list).ToList();
        foreach (var edge in new[] { Edge.Bottom, Edge.Left, Edge.Right })
        {
            var attached = FolderFlyout.Build(Dark, scale, FolderView.List, holder.Folder, cells, 1000, edge: edge, attached: true);
            Assert.Equal(seam, attached.ListGap);
            var (from, to) = Assert.Single(attached.Seams);
            Assert.Equal(seam, to - from);
            var lists = attached.Lists.OrderBy(list => list.Left).ToList();
            Assert.Equal((lists[0].Box.right, lists[1].Left), (from, to));
            Assert.Equal(0f, attached.Radius);
            var cards = FolderFlyout.Build(Dark, scale, FolderView.List, holder.Folder, cells, 1000, edge: edge);
            Assert.Equal(FolderFlyout.SeparatorRoom(scale), cards.ListGap);
        }
        // High contrast's outline is 2 DIP: its seam with it.
        var high = FolderFlyout.Build(Dark with { HighContrast = true }, 1f, FolderView.List, holder.Folder, cells, 1000, attached: true);
        Assert.Equal(2f, high.ListGap);
    }

    /// <summary>The anchor of a face inset from the bar's inner side moves onto it, along the bar where it was; on the bar's side alone.</summary>
    [Fact]
    public void AnAttachedFolderHangsFromTheBarsInnerSide()
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
        {
            var work = WorkOf(edge, 72);
            Assert.Equal(FaceAnchor(work, edge, 700), FolderFlyout.AtBar(FaceAnchor(work, edge, 700, inset: 6), work, edge));
        }
    }

    /// <summary>Shut, it stands in its bar by as much as its ground reaches out of it: wholly behind the box's edge there.</summary>
    [Fact]
    public void ItsDrawerGoesBackIntoTheBarItsWholeDepth()
    {
        var ground = new D2D_RECT_F { left = -18, top = -18, right = 618, bottom = 418 };
        Assert.Equal(new System.Numerics.Vector2(0, 436), FlyoutHost.Drawer(Edge.Bottom, ground));
        Assert.Equal(new System.Numerics.Vector2(0, -436), FlyoutHost.Drawer(Edge.Top, ground));
        Assert.Equal(new System.Numerics.Vector2(-636, 0), FlyoutHost.Drawer(Edge.Left, ground));
        Assert.Equal(new System.Numerics.Vector2(636, 0), FlyoutHost.Drawer(Edge.Right, ground));
        // With Animations off there is no motion at all: at once.
        Assert.Same(PopupMotion.None, PopupMotion.Of(Dark.Motion));
    }

    /// <summary>
    /// §651: the ground the bar's colour as the bar paints itself (Scene.GroundOf): Karpo's own bar #202020 at 85 % (dark),
    /// #F3F3F3 (light), its opacity with it — the host lays it on the wallpaper the bar is blurred over —; an opaque bar's
    /// solid; the Appearance's Taskbar colour where one is picked.
    /// </summary>
    [Fact]
    public void TheGroundIsTheBarAsTheBarIsPainted()
    {
        foreach (var dark in new[] { true, false })
        {
            var bar = AddListPictures.LookFor(dark).Wearing(new Config());
            var ground = FolderFlyout.AttachedGround(bar);
            var tokens = Scene.Theme(bar, 1, 1);
            Assert.Equal(Material.Translucent, tokens.Material);
            Assert.Equal(Scene.GroundOf(tokens), ground);
            Assert.InRange(ground.A, 1, 254);
            Assert.True(dark ? ground.R < 0x80 : ground.R > 0x80, $"a {(dark ? "dark" : "light")} bar's ground #{ground.R:X2}{ground.G:X2}{ground.B:X2}");
            // The Appearance's row names the bar's colour (a pick there solid, its opacity its own).
            var shown = Looks.Ground(Looks.Of(new Config(), dark), dark);
            Assert.Equal((255, ground.R, ground.G, ground.B), (shown.A, shown.R, shown.G, shown.B));
            // An opaque bar's ground is solid.
            var opaque = AddListPictures.LookFor(dark).Wearing(LookShorthand.Wearing(taskbarOpacity: 100));
            Assert.Equal(255, FolderFlyout.AttachedGround(opaque).A);
        }
        var picked = AddListPictures.LookFor(dark: true) with { Colors = new Palette { Taskbar = new Argb(0x80, 0x33, 0x66, 0x99) } };
        var colour = FolderFlyout.AttachedGround(picked);
        Assert.Equal((0x33, 0x66, 0x99), (colour.R, colour.G, colour.B));
        Assert.Equal(Scene.GroundOf(Scene.Theme(picked, 1, 1)), colour);
    }

    // --- The open folder ------------------------------------------------------------------------

    /// <summary>"a b c | d e": columns of airplane-mode lines.</summary>
    private static BarItem Shaped(string shape, FolderStyle style)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Style = style;
        island.Folder.Lists.Clear();
        island.Folder.Widths?.Clear();
        foreach (var part in shape.Split('|'))
        {
            island.Folder.Lists.Add([.. part.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var line = ListKind.AirplaneMode.New();
                line.Id = id;
                line.View = FolderView.List;
                return new FolderCell { Island = line };
            })]);
        }
        return island;
    }

    /// <summary>
    /// A real folder of two columns on each edge, its face 6 px inside the bar (an island's margin), attached: its ground
    /// adjoins the bar's inner side — not a px between —, down a side bar the work area's whole height, along a bottom or top
    /// bar the gap past the panel; the cards the gap off the bar; a press on the ground's empty room is the folder's. As Cards
    /// the same folder stands where it always stood, off its face, on no ground.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom, 1.5f)]
    [InlineData(Edge.Top, 1.5f)]
    [InlineData(Edge.Left, 1.5f)]
    [InlineData(Edge.Right, 1.5f)]
    [InlineData(Edge.Bottom, 1f)]
    [InlineData(Edge.Left, 2f)]
    public void TheAttachedFolderAdjoinsItsBar(Edge edge, float scale)
    {
        WindowShots.OnHiddenDesktop("folder-style", (gfx, instance) =>
        {
            var work = WorkOf(edge, Px(48, scale));
            var anchor = FaceAnchor(work, edge, 700, inset: 6);
            var config = new Config();

            using (var cards = new FolderIsland($"{Owner}/box", Shaped("a b c | d e", FolderStyle.Cards), gfx, config))
            using (var flyout = new FolderFlyout(gfx, instance, 0))
            {
                flyout.Show(gfx, Dark, scale, anchor, work, edge, cards, display: Display);
                WindowShots.Settle();
                var on = flyout.PanelOnScreen;
                Assert.Equal(new FlyoutPlacement(anchor, work, edge).PanelAt(on.right - on.left, on.bottom - on.top, scale), on);
                Assert.Null(flyout.GroundShown);
                Assert.False(flyout.Contains(OffThePanel(work, on, edge)));
                flyout.Hide();
            }

            using var folder = new FolderIsland($"{Owner}/box", Shaped("a b c | d e", FolderStyle.Attached), gfx, config);
            using var attached = new FolderFlyout(gfx, instance, 0);
            attached.Show(gfx, Dark, scale, anchor, work, edge, folder, display: Display);
            WindowShots.Settle();
            var panel = attached.PanelOnScreen;
            var (ground, colour) = attached.GroundShown!.Value;
            Assert.Equal(FolderFlyout.AttachedGround(Dark), colour);
            // §651: the bar grown out: from end to end of the work area, from the bar to the columns' far side.
            Assert.Equal(edge switch
            {
                Edge.Bottom => new RECT { left = work.left, top = panel.top, right = work.right, bottom = work.bottom },
                Edge.Top => new RECT { left = work.left, top = work.top, right = work.right, bottom = panel.bottom },
                Edge.Left => new RECT { left = work.left, top = work.top, right = panel.right, bottom = work.bottom },
                _ => new RECT { left = panel.left, top = work.top, right = work.right, bottom = work.bottom },
            }, ground);
            // The columns flush on the bar's inner side, whatever the face's inset.
            Assert.Equal(0, edge switch
            {
                Edge.Bottom => work.bottom - panel.bottom,
                Edge.Top => panel.top - work.top,
                Edge.Left => panel.left - work.left,
                _ => work.right - panel.right,
            });
            // Every column on the ground, the two a seam apart, no air.
            var pieces = attached.PiecesShown!;
            Assert.Equal(2, pieces.Select(piece => piece.left).Distinct().Count());
            Assert.All(pieces, piece => Assert.True(panel.left + piece.left >= ground.left && panel.left + piece.right <= ground.right && panel.top + piece.top >= ground.top && panel.top + piece.bottom <= ground.bottom));
            var seam = MathF.Max(1, MathF.Round(scale, MidpointRounding.AwayFromZero));
            var (from, to) = Assert.Single(attached.Plan!.Seams);
            Assert.Equal(seam, to - from);
            // Dressed: no paper handed it, so solid; its far side's line the outline, its seam the dividers' ink.
            var dressed = attached.GroundDressed!.Value;
            Assert.Equal(FlyoutTheme.Opaque(colour), dressed.Fill);
            Assert.False(dressed.Papered);
            Assert.Equal(attached.Theme.Stroke, dressed.Edge);
            Assert.Equal(FolderFlyout.RuleInk(attached.Theme), dressed.Seam);
            // A press on its empty room, off the columns, is on the folder: it closes nothing.
            Assert.True(attached.Contains(OffThePanel(ground, panel, edge)));
            attached.Hide();
        });
    }

    /// <summary>
    /// §656: an attached folder opened from a dock's bar stands as cards — no ground, off its face by the flyouts' gap, where a
    /// folder of cards of the same lines stands, its columns the same cards —; opened from a taskbar, the same folder stands
    /// attached on its ground (its style kept).
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom, 1.5f)]
    [InlineData(Edge.Left, 1f)]
    public void OnADockTheAttachedFolderStandsAsCards(Edge edge, float scale)
    {
        WindowShots.OnHiddenDesktop("folder-style-dock", (gfx, instance) =>
        {
            var work = WorkOf(edge, Px(48, scale) + Px(8, scale));
            var anchor = FaceAnchor(work, edge, 700, inset: 6);
            var config = new Config();

            RECT cardsAt;
            List<D2D_RECT_F> cardPieces;
            List<(float Left, float Right)> cardSeams;
            using (var cards = new FolderIsland($"{Owner}/box", Shaped("a b c | d e", FolderStyle.Cards), gfx, config))
            using (var flyout = new FolderFlyout(gfx, instance, 0))
            {
                flyout.Show(gfx, Dark, scale, anchor, work, edge, cards, display: Display, dock: true);
                WindowShots.Settle();
                (cardsAt, cardPieces, cardSeams) = (flyout.PanelOnScreen, [.. flyout.PiecesShown!], [.. flyout.Plan!.Seams]);
                flyout.Hide();
            }

            using var folder = new FolderIsland($"{Owner}/box", Shaped("a b c | d e", FolderStyle.Attached), gfx, config);
            using var attached = new FolderFlyout(gfx, instance, 0);
            attached.Show(gfx, Dark, scale, anchor, work, edge, folder, display: Display, dock: true);
            WindowShots.Settle();
            var panel = attached.PanelOnScreen;
            Assert.Equal(cardsAt, panel);
            Assert.Equal(new FlyoutPlacement(anchor, work, edge).PanelAt(panel.right - panel.left, panel.bottom - panel.top, scale), panel);
            Assert.Equal(cardPieces, attached.PiecesShown!);
            Assert.Null(attached.GroundShown);
            Assert.False(attached.Flat);
            // The columns a separator's room apart, as cards, not a seam's.
            Assert.Equal(cardSeams, attached.Plan!.Seams);
            Assert.Equal(FolderStyle.Attached, folder.Options.Style);
            attached.Hide();
            WindowShots.Settle();

            // From a taskbar: attached, on its ground, flush on the bar.
            attached.Show(gfx, Dark, scale, anchor, work, edge, folder, display: Display);
            WindowShots.Settle();
            Assert.NotNull(attached.GroundShown);
            Assert.True(attached.Flat);
            var (from, to) = Assert.Single(attached.Plan!.Seams);
            Assert.True(to - from < cardSeams[0].Right - cardSeams[0].Left);
            attached.Hide();
        });
    }

    /// <summary>A point on the ground's empty room past the panel along the bar, at the end with more room, 2 px off its far side.</summary>
    private static POINT OffThePanel(RECT ground, RECT panel, Edge edge) => edge switch
    {
        Edge.Left or Edge.Right => new POINT
        {
            x = (panel.left + panel.right) / 2,
            y = panel.top - ground.top > ground.bottom - panel.bottom ? ground.top + 2 : ground.bottom - 3,
        },
        _ => new POINT
        {
            x = panel.left - ground.left > ground.right - panel.right ? ground.left + 2 : ground.right - 3,
            y = (panel.top + panel.bottom) / 2,
        },
    };

    /// <summary>
    /// Set on its page while the folder stands open (a folder's change configures it again): Attached moves it onto its ground
    /// from the bar, Cards back off its face, no ground.
    /// </summary>
    [UiFact]
    public void ChangedWhileOpenTheFolderTakesItsStyleAtOnce()
    {
        WindowShots.OnHiddenDesktop("folder-style-change", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var work = WorkOf(Edge.Left, 72);
            var anchor = FaceAnchor(work, Edge.Left, 500, inset: 6);
            var holder = Shaped("a b | c", FolderStyle.Cards);
            var config = new Config();
            using var folder = new FolderIsland($"{Owner}/box", holder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, scale, anchor, work, Edge.Left, folder, display: Display);
            WindowShots.Settle();
            var cards = flyout.PanelOnScreen;
            Assert.Null(flyout.GroundShown);

            holder.Folder!.Style = FolderStyle.Attached;
            folder.Configure(holder, config);
            WindowShots.Settle();
            var (ground, _) = Assert.IsType<(RECT, Color)>(flyout.GroundShown);
            Assert.Equal((work.left, work.top, work.bottom), (ground.left, ground.top, ground.bottom));
            // §651: flush on the bar.
            Assert.Equal(work.left, flyout.PanelOnScreen.left);

            holder.Folder.Style = FolderStyle.Cards;
            folder.Configure(holder, config);
            WindowShots.Settle();
            Assert.Null(flyout.GroundShown);
            Assert.Equal(cards, flyout.PanelOnScreen);
            flyout.Hide();
        });
    }

    /// <summary>With Animations on it comes out of the bar (its showing sets off coming in); off, it is there at once.</summary>
    [UiFact]
    public void ItSlidesOutOfTheBarWithAnimationsAndIsThereAtOnceWithout()
    {
        WindowShots.OnHiddenDesktop("folder-style-motion", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var work = WorkOf(Edge.Bottom, 72);
            var anchor = FaceAnchor(work, Edge.Bottom, 900);
            var config = new Config();
            using var folder = new FolderIsland($"{Owner}/box", Shaped("a b", FolderStyle.Attached), gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var on in new[] { true, false })
            {
                var look = Dark with { Animations = on };
                flyout.Show(gfx, look, scale, anchor, work, Edge.Bottom, folder, display: Display, motion: PopupMotion.Of(look.Motion));
                Assert.Equal(on, flyout.Entered);
                WindowShots.Settle();
                Assert.NotNull(flyout.GroundShown);
                flyout.Hide();
                WindowShots.Settle();
            }
        });
    }
}
