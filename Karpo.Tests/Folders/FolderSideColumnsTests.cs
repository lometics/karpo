// A folder's columns on a side bar (§303; the person, 2026-10-23: "when shown on the left or right
// the columns from the middle folders should all be centered, right now the largest column give the
// alignment", and arranged "the first column then should start on the left respectively on the right
// side"): each column centred along the bar on the folder's face, and the first column next to the
// bar, the others away from it, at rest and arranged alike — a bottom bar's rule turned to the side.
//
// The open folder's half needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderSideColumnsTests
{
    private const string Owner = "layout:default";
    private const float Scale = 1.5f;
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly RECT Display = new() { right = 1920, bottom = 1080 };

    /// <summary>A folder of lists, each its widgets of <paramref name="lists"/>, an empty array an empty list.</summary>
    private static FolderOptions Lists(params ListKind[][] lists)
    {
        var options = new FolderOptions { Lists = [] };
        for (var list = 0; list < lists.Length; list++)
        {
            options.Lists.Add([]);
            foreach (var kind in lists[list])
            {
                options.Put(kind.New(), list);
            }
        }
        return options;
    }

    /// <summary>"a b c | d | e f": three columns of three, one and two lines; a blank part an empty column.</summary>
    private static BarItem Shaped(string shape)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Lists.Clear();
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

    /// <summary>A side bar 48 DIPs thick at 150 % down <paramref name="edge"/> of a 1920 × 1080 display, the folder's face <paramref name="along"/> down it.</summary>
    private static (POINT Anchor, RECT Work) On(Edge edge, int along) => edge == Edge.Left
        ? (new POINT { x = 72, y = along }, Display with { left = 72 })
        : (new POINT { x = 1920 - 72, y = along }, Display with { right = 1920 - 72 });

    /// <summary>
    /// The plan alone: on a side bar each column's middle is the folder's middle; on a bottom bar
    /// their bottoms are level and on a top bar their tops, as before. The first column on the left,
    /// but on a right bar on the right, the others leftward — arranged as at rest, each column's
    /// buttons over its own top (under it on a top bar).
    /// </summary>
    [Theory]
    [InlineData(Edge.Left, false)]
    [InlineData(Edge.Left, true)]
    [InlineData(Edge.Right, false)]
    [InlineData(Edge.Right, true)]
    [InlineData(Edge.Bottom, false)]
    [InlineData(Edge.Top, true)]
    public void ASideBarsColumnsAreCentredAndRunFromTheBar(Edge edge, bool editing)
    {
        var options = Lists([ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.Cpu], [ListKind.Cpu], [ListKind.AirplaneMode, ListKind.Cpu]);
        var plan = FolderFlyout.Build(Dark, Scale, FolderView.List, options, options.Cells, 1000, editing: editing, edge: edge);

        var middle = plan.Height / 2;
        foreach (var list in plan.Lists)
        {
            switch (edge)
            {
                case Edge.Left or Edge.Right:
                    Assert.InRange((list.Box.top + list.Box.bottom) / 2, middle - 0.5f, middle + 0.5f);
                    break;
                case Edge.Top:
                    Assert.Equal(0, list.Box.top);
                    break;
                default:
                    Assert.Equal(plan.Height, list.Box.bottom);
                    break;
            }
        }
        Assert.Equal(plan.Lists.Max(list => list.Height), plan.Height);
        // The core is the three, as they stand.
        Assert.Equal(PopupPaint.Rect(0, 0, plan.Width, plan.Height), plan.Core);

        // The first column at its start, each next one further on, the lists' gap apart — leftward on a right bar.
        var rightward = edge != Edge.Right;
        Assert.Equal(rightward ? 0 : plan.Width, rightward ? plan.Lists[0].Left : plan.Lists[0].Box.right);
        for (var list = 1; list < plan.Lists.Count; list++)
        {
            var (near, far) = (plan.Lists[list - 1].Box, plan.Lists[list].Box);
            Assert.Equal(plan.ListGap, rightward ? far.left - near.right : near.left - far.right, 1);
        }
        // Each line in its own column.
        for (var index = 0; index < plan.Cards.Count; index++)
        {
            var box = plan.Lists[plan.CardList[index]].Box;
            Assert.True(plan.Cards[index].left == box.left && plan.Cards[index].top >= box.top && plan.Cards[index].bottom <= box.bottom, $"line {index} in its column");
        }
        Assert.Equal(editing ? 6 : 0, plan.Adds.Count);
        foreach (var add in plan.Adds)
        {
            var box = plan.Lists[add.List].Box;
            Assert.True(edge == Edge.Top ? add.Box.top > box.bottom : add.Box.bottom < box.top, "at its own end away from the bar");
            Assert.InRange(add.Box.left, box.left, box.right);
        }
    }

    /// <summary>
    /// Arranged on a side bar the slots stand beyond the last column, away from the bar, and make a
    /// list after the last; the empty column's buttons and the slots' on one row over the empty
    /// column's end, which is centred too. None on the bar's side, where the room ends.
    /// </summary>
    [Theory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ASideBarsSlotsStandBeyondTheLastColumn(Edge edge)
    {
        var options = Lists([ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.Cpu], []);
        var plan = FolderFlyout.Build(Dark, Scale, FolderView.List, options, options.Cells, 1000, editing: true, edge: edge);
        // The room from the bar's inner side, 18 px from the panel, to the display's far edge.
        var room = edge == Edge.Left
            ? new D2D_RECT_F { left = -18, top = -400, right = 1920 - 72 - 18, bottom = 600 }
            : new D2D_RECT_F { left = -(1920 - 72 - 18 - plan.Width), top = -400, right = plan.Width + 18, bottom = 600 };
        var slotted = FolderFlyout.WithSlots(plan, Scale, room, edge);

        Assert.NotEmpty(slotted.Slots);
        Assert.All(slotted.Slots, slot => Assert.Equal(FolderPlace.Slot(2, slot.Place.Apart), slot.Place));
        Assert.All(slotted.Slots, slot => Assert.True(edge == Edge.Left ? slot.Box.left >= plan.Width : slot.Box.right <= 0, "away from the bar"));
        // The empty column has no height, at the folder's middle, where its first line would stand.
        var empty = plan.Lists[1].Box;
        Assert.Equal((PopupPaint.Round(plan.Height / 2), 0f), (empty.top, empty.bottom - empty.top));
        var rows = slotted.Adds.Where(add => add.List is -1 or 1).Select(add => add.Box.top).Distinct().ToList();
        Assert.Single(rows);
        Assert.Equal(empty.top - PopupPaint.Round(FolderFlyout.AddApartDip * Scale) - PopupPaint.Round(FolderFlyout.AddDip * Scale), rows[0]);
    }

    /// <summary>
    /// Arranged, an empty first column hangs off the columns with widgets on the bar's side, under the
    /// bar, where the room ends: as an empty column past a screen's edge, it is not there — no buttons,
    /// no guide lines in the gap between the bar and the folder.
    /// </summary>
    [Theory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void AnEmptyColumnUnderASideBarIsNotThere(Edge edge)
    {
        var options = Lists([], [ListKind.EnergySaver, ListKind.Cpu]);
        var plan = FolderFlyout.Build(Dark, Scale, FolderView.List, options, options.Cells, 1000, editing: true, edge: edge);
        // The core 18 px from the bar's inner side, as the host places it; the room to the display's far edge.
        var room = edge == Edge.Left
            ? new D2D_RECT_F { left = plan.Core.left - 18, top = -400, right = 1600, bottom = 600 }
            : new D2D_RECT_F { left = -1600, top = -400, right = plan.Core.right + 18, bottom = 600 };
        Assert.True(edge == Edge.Left ? plan.Lists[0].Box.left < room.left : plan.Lists[0].Box.right > room.right, "the empty column reaches under the bar");
        var slotted = FolderFlyout.WithSlots(plan, Scale, room, edge);

        Assert.All(slotted.Adds.Where(add => add.List == 0), add => Assert.True(add.Box.right <= add.Box.left, "no button"));
        Assert.DoesNotContain(FolderFlyout.Guides(slotted, room), line => edge == Edge.Left ? line.left < plan.Core.left : line.right > plan.Core.right);
        // The column with widgets keeps its own.
        Assert.Equal(2, slotted.Adds.Count(add => add.List == 1 && add.Box.right > add.Box.left));
    }

    /// <summary>
    /// A real folder of columns of three, one and two lines on a left and a right bar, its face at the
    /// top end (the start group), the middle (the centre group) and the bottom end (the end group):
    /// in the middle every column's middle is level with the face; at an end the block is kept on
    /// the screen as one, every column's middle level with the others'. The first column is next to
    /// the bar, the rest away from it, and arranging moves none of them. Pictures
    /// side-&lt;edge&gt;-&lt;along&gt;-&lt;rest|arranged&gt;.png, a red line through the face.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Left, 60)]
    [InlineData(Edge.Left, 540)]
    [InlineData(Edge.Left, 1020)]
    [InlineData(Edge.Right, 60)]
    [InlineData(Edge.Right, 540)]
    [InlineData(Edge.Right, 1020)]
    public void OnASideBarEveryColumnIsCentredOnTheFaceAndTheFirstIsNextToTheBar(Edge edge, int along)
    {
        WindowShots.OnHiddenDesktop("folder-side-columns", (gfx, instance) =>
        {
            var (anchor, work) = On(edge, along);
            var folder = new FolderIsland($"{Owner}/box", Shaped("a b c | d | e f"), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            List<RECT>? rest = null;
            foreach (var editing in new[] { false, true })
            {
                flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: editing, display: Display);
                WindowShots.Settle();
                var columns = Columns(flyout);
                Assert.Equal(3, columns.Count);
                var middles = columns.Select(column => (column.top + column.bottom) / 2f).ToList();
                Assert.All(middles, middle => Assert.InRange(middle, middles[0] - 1, middles[0] + 1));
                if (along == 540)
                {
                    Assert.InRange(middles[0], anchor.y - 1, anchor.y + 1);
                }
                Assert.True(columns.Min(column => column.top) >= work.top && columns.Max(column => column.bottom) <= work.bottom, "on the screen");
                // The first next to the bar, the gap a flyout keeps from it; the next ones away from it.
                var gap = (int)MathF.Round(12 * Scale);
                Assert.Equal(edge == Edge.Left ? anchor.x + gap : anchor.x - gap, edge == Edge.Left ? columns[0].left : columns[0].right);
                for (var list = 1; list < columns.Count; list++)
                {
                    Assert.True(edge == Edge.Left ? columns[list].left > columns[list - 1].right : columns[list].right < columns[list - 1].left, $"column {list} further from the bar");
                }
                // Arranging moves none of them.
                rest ??= columns;
                Assert.Equal(rest, columns);
                Save(flyout, anchor, $"side-{edge}-{along}-{(editing ? "arranged" : "rest")}.png");
                flyout.Hide();
            }
            folder.Dispose();
        });
    }

    /// <summary>
    /// The bug (the person, 2026-10-23: arranged on a side bar "it does not properly align the columns
    /// ... the first column then should start on the left respectively on the right side"): an empty
    /// column outside the ones with widgets on the bar's side had no room to hang into and pushed them
    /// away from the bar as edit mode began — on a left bar the empty first columns, on a right bar,
    /// whose columns ran towards it, the empty last ones. Now the columns with widgets stay next to the
    /// bar, arranged as at rest, an empty first column hanging off under the bar — its buttons not
    /// there, as an empty column's past a screen's edge along a bottom bar — and an empty last one
    /// beyond them, away from the bar.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ArrangedTheColumnsWithWidgetsStayNextToTheBar(Edge edge)
    {
        WindowShots.OnHiddenDesktop("folder-side-first", (gfx, instance) =>
        {
            var (anchor, work) = On(edge, 540);
            var folder = new FolderIsland($"{Owner}/box", Shaped(" | a b c | d | "), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var gap = (int)MathF.Round(12 * Scale);
            flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: false, display: Display);
            var rest = Columns(flyout)[1..3];
            Assert.Equal(edge == Edge.Left ? anchor.x + gap : anchor.x - gap, edge == Edge.Left ? rest[0].left : rest[0].right);
            flyout.Hide();

            flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: true, display: Display);
            WindowShots.Settle();
            var columns = Columns(flyout);
            Assert.Equal(4, columns.Count);
            Assert.Equal(rest, columns[1..3]);
            // The empty first one under the bar, its buttons not there; the empty last one beyond.
            Assert.True(edge == Edge.Left ? columns[0].right < columns[1].left && columns[0].left < anchor.x : columns[0].left > columns[1].right && columns[0].right > anchor.x, "the empty first column under the bar");
            Assert.All(flyout.Plan!.Adds.Where(add => add.List == 0), add => Assert.True(add.Box.right <= add.Box.left, "no button under the bar"));
            Assert.True(edge == Edge.Left ? columns[3].left > columns[2].right : columns[3].right < columns[2].left, "the empty last column beyond");
            Save(flyout, anchor, $"side-{edge}-empties-arranged.png");
            flyout.Hide();
            flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: false, display: Display);
            WindowShots.Settle();
            Save(flyout, anchor, $"side-{edge}-empties-rest.png");
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>Each column of the open folder, its box on the screen.</summary>
    private static List<RECT> Columns(FolderFlyout flyout)
    {
        var on = flyout.PanelOnScreen;
        return [.. flyout.Plan!.Lists.Select(list => new RECT
        {
            left = on.left + (int)MathF.Round(list.Box.left),
            top = on.top + (int)MathF.Round(list.Box.top),
            right = on.left + (int)MathF.Round(list.Box.right),
            bottom = on.top + (int)MathF.Round(list.Box.bottom),
        })];
    }

    /// <summary>The display round the open folder on a dark backdrop, a red line across it through its face.</summary>
    private static void Save(FolderFlyout flyout, POINT anchor, string file)
    {
        var frame = Display;
        var (canvas, _) = WindowShots.Together([(flyout.Hwnd, frame)], frame, file, (0x30, 0x30, 0x30));
        var width = frame.right - frame.left;
        for (var x = 0; x < width; x++)
        {
            var at = ((anchor.y * width) + x) * 4;
            (canvas[at], canvas[at + 1], canvas[at + 2]) = ((byte)0x20, (byte)0x20, (byte)0xE0);
        }
        WindowShots.SaveSideBySide([(canvas, width, frame.bottom - frame.top)], file, (0x80, 0x80, 0x80));
    }
}
