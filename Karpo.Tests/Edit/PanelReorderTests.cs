// edit, §551 (the person, 2026-11-07: "taskbar menu should show a preview and it should also be DnD and not arrows, remove
// the arrows."): every list of the settings panel put in order is dragged into order (SettingsPanel.Reorder.cs). Held here:
//
//  - **Where it lands** (PanelReorder): after every row whose middle its own middle is past; the rows it passes over make
//    room by its height and the gap after it; Ctrl+Up and Ctrl+Down a place, never past either end.
//  - **Which rows**: the taskbar menu's entries (Edit too), the layouts, a folder's faces, a toggles line's toggles — each a
//    handle at its start, the first stop of its row, said to a screen reader with where it stands ("3 of 6"); no ↑ or ↓ left
//    on any page.
//  - **The real window** (KARPO_UI_TESTS=1): a press on a handle and a move lift the row, the others making room; let go,
//    one move (EditDeckAction.Reorder) to where it landed; Escape and a right press put it back, nothing sent; a press let
//    go before the drag distance a click; Ctrl+Down on a row's control moves it a place, Ctrl+Up on the first nothing.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class PanelReorderTests
{
    private const float Scale = 1.5f;

    /// <summary>Four rows 48 tall, 4 apart, from 0.</summary>
    private static readonly RectF[] Four = [.. Enumerable.Range(0, 4).Select(at => new RectF(0, at * 52, 500, 48))];

    [Theory]
    // Its middle where it stands: where it is.
    [InlineData(1, 76, 1)]
    // Past the next one's middle (128): after it.
    [InlineData(1, 129, 2)]
    [InlineData(1, 127, 1)]
    // Over the first's middle (24): before it; past the last's (180): last.
    [InlineData(1, 23, 0)]
    [InlineData(1, 500, 3)]
    [InlineData(3, -40, 0)]
    public void ItLandsAfterEveryRowWhoseMiddleItIsPast(int from, float middle, int to) =>
        Assert.Equal(to, PanelReorder.DropAt(Four, from, middle));

    [Fact]
    public void TheRowsItPassesOverMakeRoomByItsHeightAndItsGap()
    {
        // The second carried down to the last place: the third and the fourth up by 52, the first not at all.
        Assert.Equal([0f, 0f, -52f, -52f], Enumerable.Range(0, 4).Select(at => PanelReorder.Shift(Four, 1, 3, at)));
        // The last carried to the top: the others down by 52.
        Assert.Equal([52f, 52f, 52f, 0f], Enumerable.Range(0, 4).Select(at => PanelReorder.Shift(Four, 3, 0, at)));
        Assert.All(Enumerable.Range(0, 4), at => Assert.Equal(0f, PanelReorder.Shift(Four, 2, 2, at)));
        // A drag: its place now from the pointer, where it was grabbed kept.
        var drag = new PanelDrag("b", 7, ["a", "b", "c", "d"], Four, 1, Grab: 10) { Y = 62 + 110 };
        Assert.Equal((3, 110f), (drag.To, drag.Lift));
        Assert.Equal((0, 1, -1), (drag.Of(10), drag.Of(60), drag.Of(50)));
    }

    [Fact]
    public void CtrlUpAndCtrlDownMoveItAPlaceNeverPastAnEnd()
    {
        Assert.Equal(1, PanelReorder.Stepped(VK.VK_DOWN, control: true, 0, 3));
        Assert.Equal(1, PanelReorder.Stepped(VK.VK_UP, control: true, 2, 3));
        Assert.Null(PanelReorder.Stepped(VK.VK_UP, control: true, 0, 3));
        Assert.Null(PanelReorder.Stepped(VK.VK_DOWN, control: true, 2, 3));
        Assert.Null(PanelReorder.Stepped(VK.VK_DOWN, control: false, 0, 3));
        Assert.Null(PanelReorder.Stepped(VK.VK_LEFT, control: true, 1, 3));
        Assert.Equal("3 of 6", PanelReorder.Position(2, 6));
    }

    /// <summary>
    /// Every list put in order a handle on each row, its first stop, its tooltip how it moves, a screen reader told it and
    /// where it stands; no ↑ or ↓ button left on any page, nor the words that named them.
    /// </summary>
    [Fact]
    public void EveryListPutInOrderHasHandlesAndNoArrows()
    {
        var pages = PanelControlsTests.Pages().Concat(SettingsPanelTests.Content(widget: true, kind: ListKind.Toggles).Pages).ToList();
        var tools = pages.SelectMany(page => PanelRows.All(page.Rows)).SelectMany(row => row.Tools).ToList();
        Assert.DoesNotContain(tools, tool => tool.Glyph is OptionGlyphs.ArrowUp or OptionGlyphs.ArrowDown);
        var strings = File.ReadAllText(Path.Combine(RepoSource.Root(), "Karpo.Core", "Strings", "en.json"));
        Assert.DoesNotContain("faceEarlier", strings, StringComparison.Ordinal);
        Assert.DoesNotContain("faceLater", strings, StringComparison.Ordinal);

        // The lists dragged into order: the taskbar menu's entries, the layouts, a folder's faces, a toggles line's toggles.
        var content = SettingsPanelTests.Content(widget: true, kind: ListKind.Toggles);
        int Dragged(PanelPage page) => PanelRows.All(content.Of(page)!.Rows).Count(row => row.Drags);
        Assert.Equal(6, Dragged(PanelPage.Taskbar));
        Assert.Equal(MenuCorpus.Fresh().Layouts.Count, Dragged(PanelPage.Layouts));
        var folder = SettingsPanelTests.FolderPage(TwoFaces(), new OptionPlace(MenuCorpus.Fresh(), Dark: true));
        Assert.Equal(2, PanelRows.All(folder.Rows).Count(row => row.Drags));
        Assert.True(Dragged(PanelPage.Widget) >= 2, "a toggles line's toggles are dragged into order");

        var size = PanelLayout.SizeOf(Scale, 1548);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, PanelPage.Taskbar, SettingsPanelTests.BarMenuOpen), size);
        var said = DeckUia.Things(layout, bar: false);
        var tree = DeckUia.Tree(layout, new RECT { right = (int)layout.FlyoutSize.X, bottom = (int)layout.FlyoutSize.Y }, PanelHit.None, bar: false);
        var lines = layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Drags).ToList();
        Assert.Equal(6, lines.Count);
        for (var at = 0; at < lines.Count; at++)
        {
            var line = lines[at];
            var (hit, rect) = layout.Controls(line)[0];
            Assert.Equal((PanelHitKind.Handle, line.Row.Key), (hit.Kind, hit.Key));
            Assert.True(rect.X < layout.NameX(line) && rect.Height == MathF.Round(32 * Scale), $"{line.Row.Key}: its handle at its start");
            Assert.Contains(hit, layout.Stops());
            Assert.Equal(S.DockEditFaceDrag, layout.TipOf(hit));
            Assert.Equal(S.DockEditFaceDrag + " · " + line.Row.Text, said.Single(thing => thing.Hit == hit).Name);
            Assert.Equal((at, 6), layout.PlaceOf(line.Row.Key));
            Assert.Contains(Nodes(tree.Root), node => node.AutomationId == $"Panel.Handle.{line.Row.Key}.-1" && node.Status == PanelReorder.Position(at, 6));
        }
        // Lifted: its list's six rows (§581's default menu, §663 with its second rule and Lock the taskbar), each where it stands.
        var lifted = layout.DragOf(lines[5].Row.Key, lines[5].Rect.Y + 10)!;
        Assert.Equal((5, 5, 10f), (lifted.From, lifted.To, lifted.Grab));
        Assert.Equal(lines.Select(line => line.Row.Key), lifted.Keys);
        Assert.Equal(lines[0].Rect.Y, lifted.Spans[0].Y);
        Assert.Null(layout.DragOf(layout.Blocks.SelectMany(block => block.Lines).First(line => line.Row.Adds).Row.Key, 0));

        static IEnumerable<UiaNode> Nodes(UiaNode node) => [node, .. node.Children.SelectMany(Nodes)];
    }

    /// <summary>A folder wearing two faces: two rows to drag into order.</summary>
    private static WidgetItem TwoFaces()
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.IconId)];
        folder.Folder.Face = folder.Folder.Faces[0];
        return folder;
    }

    // --- The real window ----------------------------------------------------------------------------------------

    private static Look Still => AddListPictures.LookFor(dark: true) with { Animations = false };

    private static readonly RectI Display = new(0, 0, 2880, 1620);

    private static EditDeck Shown(Gfx gfx, HINSTANCE instance)
    {
        var deck = new EditDeck(gfx, instance, 0);

        deck.Show(gfx, Still, Scale, new EditDeckRoom(Display, 0, 72, Work: new RectI(0, 0, 2880, 1548)), EditDeckState.Plain, SettingsPanelTests.Content(), PanelPage.Taskbar);
        // §585: the menu an expander, Behavior's last row: opened, brought into sight.
        deck.Land(PanelPage.Taskbar, SettingsPanelTests.BarMenuKey + "/" + S.CoreBarMenuTaskbarSettings, light: false);
        return deck;
    }

    /// <summary>The line of <paramref name="text"/>, the last of that name (§553: the menu's Lock the taskbar, not Bar's).</summary>
    private static PanelLine Line(EditDeck deck, string text) => deck.Layout!.Blocks.SelectMany(block => block.Lines).Last(line => line.Row.Text == text);

    private static Vector2 HandleOf(EditDeck deck, string text) => deck.Layout!.RectOf(deck.Layout.Controls(Line(deck, text))[0].Hit).Center;

    [UiFact]
    public void ARowDraggedAndLetGoIsOneMoveAndEscapePutsItBack()
    {
        WindowShots.OnHiddenDesktop("panel-reorder", (gfx, instance) =>
        {
            using var deck = Shown(gfx, instance);
            var task = Line(deck, S.CoreBarMenuTaskManager);
            var from = HandleOf(deck, S.CoreBarMenuTaskManager);
            var top = deck.Layout!.OnPanel(Line(deck, S.CoreBarMenuLayouts).Rect);

            // Pressed and let go where it was: a click, nothing lifted, nothing sent.
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONDOWN, 1, EditDeckClickTests.At(deck, from, flyout: true));
            Assert.Equal(EditDeckAction.None, deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONUP, 0, EditDeckClickTests.At(deck, from, flyout: true)).Happened.Action);
            Assert.Null(deck.Reordering);

            // Dragged over the layouts: lifted, landing first; let go, one move there.
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONDOWN, 1, EditDeckClickTests.At(deck, from, flyout: true));
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 1, EditDeckClickTests.At(deck, from - new Vector2(0, 20), flyout: true));
            Assert.Equal(2, deck.Reordering?.From);
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 1, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true));
            Assert.Equal(0, deck.Reordering?.To);
            var let = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONUP, 0, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true)).Happened;
            Assert.Equal(new EditDeckEvent(EditDeckAction.Reorder, Id: task.Row.Item.Id, Page: PanelPage.Taskbar, Key: task.Row.Key, To: 0), let);
            Assert.Null(deck.Reordering);

            // Escape while it is carried: back, nothing sent as it is let go.
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONDOWN, 1, EditDeckClickTests.At(deck, from, flyout: true));
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 1, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true));
            Assert.NotNull(deck.Reordering);
            _ = deck.Message(deck.Hwnd, WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            Assert.Null(deck.Reordering);
            Assert.Equal(EditDeckAction.None, deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONUP, 0, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true)).Happened.Action);

            // A right press while it is carried: back too.
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONDOWN, 1, EditDeckClickTests.At(deck, from, flyout: true));
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 1, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true));
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_RBUTTONDOWN, 2, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true));
            Assert.Null(deck.Reordering);
            Assert.Equal(EditDeckAction.None, deck.Message(deck.FlyoutHwnd, WM.WM_LBUTTONUP, 0, EditDeckClickTests.At(deck, new Vector2(from.X, top.Y + 2), flyout: true)).Happened.Action);
        });
    }

    [UiFact]
    public void CtrlDownMovesTheRowWithTheKeysAPlace()
    {
        WindowShots.OnHiddenDesktop("panel-reorder-keys", (gfx, instance) =>
        {
            using var deck = Shown(gfx, instance);
            var held = KeyState.Held;
            try
            {
                var task = Line(deck, S.CoreBarMenuTaskManager);
                _ = EditDeckClickTests.Click(deck, deck.Layout!.Controls(task)[0].Hit);
                KeyState.Held = key => key == VK.VK_CONTROL;
                var moved = deck.Message(deck.Hwnd, WM.WM_KEYDOWN, VK.VK_DOWN, 0).Happened;
                Assert.Equal(new EditDeckEvent(EditDeckAction.Reorder, Id: task.Row.Item.Id, Page: PanelPage.Taskbar, Key: task.Row.Key, To: 3), moved);
                // The first a place up: nowhere to go, nothing sent.
                KeyState.Held = null;
                _ = EditDeckClickTests.Click(deck, deck.Layout.Controls(Line(deck, S.CoreBarMenuLayouts))[0].Hit);
                KeyState.Held = key => key == VK.VK_CONTROL;
                Assert.NotEqual(EditDeckAction.Reorder, deck.Message(deck.Hwnd, WM.WM_KEYDOWN, VK.VK_UP, 0).Happened.Action);
            }
            finally
            {
                KeyState.Held = held;
            }
        });
    }
}
