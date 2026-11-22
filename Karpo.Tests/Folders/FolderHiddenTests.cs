// A folder never scrolls (the person, 2026-10-12: "a folder should never show a scrollbar, it should
// hide the widgets that dont fit and show a warning sign like the taskbar does it"). A list taller
// than the most a list may be (the room beside the bar less an arranged list's "+", §185) leaves out its last lines, whole ones from its
// end, each list on its own; arranged, the list's buttons carry a third — the warning, in the edit
// bar's warning orange, its glyph the hidden widget's own or the warning's for several — whose
// tooltip says how many and whose click lists them, as the edit bar's "not shown" button does. At
// rest nothing says so, as on the bar.
//
// The plan's half is pure; the open folder's needs Direct2D and a window station (KARPO_UI_TESTS=1),
// and pictures it to KARPO_PNG_DIR: folder-hidden-<dark|light>-<rest|edit>.png.

using Karpo.Dock;
using Karpo.Tests.Edit;
using static TerraFX.Interop.Windows.Windows;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderHiddenTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>A folder of lists, each its widgets of <paramref name="lists"/>.</summary>
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

    private static ListKind[] Lines(int count) => [.. Enumerable.Repeat(ListKind.EnergySaver, count)];

    private static FolderPlan Plan(FolderOptions options, float work, bool editing = false, Edge edge = Edge.Bottom) =>
        FolderFlyout.Build(Dark, 1f, FolderView.List, options, options.Cells, work, editing: editing, edge: edge);

    /// <summary>The work area at 100 % whose folder reaches <paramref name="most"/> px: less the gap and the far margin, 12 px each, and the "+" and its gap, 30 (no close button's row since §287).</summary>
    private static float Reaching(float most) => most + 24 + 30;

    /// <summary>The indices of list <paramref name="list"/>'s widgets in the plan, in its order.</summary>
    private static List<int> Of(FolderPlan plan, int list) => [.. Enumerable.Range(0, plan.Cards.Count).Where(index => plan.CardList[index] == list)];

    /// <summary>
    /// Each list shows as many whole lines as fit under the most — lines of 40 px edge to edge
    /// (§410), under the room less the "+" (FolderFlyout.Reach) — and leaves the rest out from its end; the others are not
    /// cut for it. Nothing scrolls: every line shown stands inside its list's panel.
    /// </summary>
    [Theory]
    [InlineData(600f)]
    [InlineData(800f)]
    [InlineData(1040f)]
    [InlineData(300f)]
    public void EachListLeavesOutTheLinesPastTheMostFromItsEnd(float work)
    {
        var options = Lists(Lines(40), Lines(3), Lines(25));
        var plan = Plan(options, work);
        var most = MathF.Max(40 + 24, FolderFlyout.Reach(work, 0, 1));
        var fit = (int)MathF.Floor(most / 40);

        foreach (var (list, count) in new[] { (0, 40), (1, 3), (2, 25) })
        {
            var lines = Of(plan, list);
            var shown = Math.Min(count, fit);
            Assert.Equal(lines[shown..], plan.Lists[list].Hidden);
            Assert.All(lines[..shown], index => Assert.True(plan.Shows(index), $"line {index} of list {list} shows"));
            Assert.All(lines[shown..], index => Assert.False(plan.Shows(index) || plan.Holds(index), $"line {index} of list {list} is left out"));
            Assert.Equal(shown * 40, plan.Lists[list].Height, 1);
            Assert.All(lines[..shown], index => Assert.True(plan.Cards[index].bottom <= plan.Lists[list].Box.bottom + 0.01f));
        }
        Assert.True(plan.Height <= most);
        Assert.Equal(plan.Lists.SelectMany(list => list.Hidden), plan.Hidden);
    }

    /// <summary>
    /// Whole lines only, and from the end: a tall line that does not fit is left out whole, not cut,
    /// and every line after it with it — a short one that would fit in what is left too.
    /// </summary>
    [Fact]
    public void ATallLineThatDoesNotFitGoesWholeWithEveryLineAfterIt()
    {
        // Nine lines (360 px of the 420), then a line of four rows, which does not fit, then a line, which would.
        var options = Lists([.. Lines(9), ListKind.SoundInput, ListKind.EnergySaver]);
        var plan = Plan(options, Reaching(420));

        Assert.Equal([9, 10], plan.Lists[0].Hidden);
        Assert.Equal(9 * 40, plan.Lists[0].Height, 1);
        Assert.Equal(plan.Lists[0].Box.bottom, plan.Cards[8].bottom, 1);
    }

    /// <summary>
    /// A divider left last parts nothing from nothing: it goes with the lines it stood over, and is
    /// not counted among the widgets not shown — there is nothing of it to name.
    /// </summary>
    [Fact]
    public void ADividerLeftLastGoesAndIsNotCounted()
    {
        var options = Lists([.. Lines(11), ListKind.Divider, .. Lines(5)]);
        var plan = Plan(options, Reaching(420));

        Assert.False(plan.Shows(11));
        Assert.Equal(Enumerable.Range(12, 5), plan.Lists[0].Hidden);
        Assert.Equal(plan.Lists[0].Box.bottom, plan.Cards[10].bottom, 1);
        Assert.Equal(plan.Lists[0].Box.bottom, plan.Pieces[^1].bottom, 1);
    }

    /// <summary>A first line taller than the most is given the most — its items scroll inside it — and nothing is left out for it.</summary>
    [Fact]
    public void AFirstLineTallerThanTheMostIsGivenTheMost()
    {
        var holder = BarKind.FolderList.New();
        var cell = ListKind.Clipboard.New();
        cell.Height = LineHeight.Exactly(FolderOptions.MostCells);
        Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder!, cell));
        var options = holder.Folder!;
        var plan = Plan(options, 300);

        Assert.Empty(plan.Lists[0].Hidden);
        Assert.True(plan.Shows(0));
        Assert.Equal(FolderFlyout.ListMost(300, 0, 1), plan.Cards[0].bottom - plan.Cards[0].top, 1);
    }

    /// <summary>
    /// Arranged, a list that leaves widgets out has a third button beside its "+" and its width —
    /// the warning — the three centred on the list together; a list that shows everything has none,
    /// and at rest there is none at all, as the bar says nothing of its own at rest. The lines stand
    /// where they stand at rest.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ArrangedAListThatLeavesWidgetsOutHasTheWarning(Edge edge)
    {
        var options = Lists(Lines(30), Lines(2));
        var rest = Plan(options, 600, editing: false, edge);
        var plan = Plan(options, 600, editing: true, edge);

        Assert.Empty(rest.Adds);
        Assert.Equal(rest.Cards, plan.Cards);
        Assert.Equal(
            [FolderAddKind.Add, FolderAddKind.Width, FolderAddKind.Hidden, FolderAddKind.Add, FolderAddKind.Width],
            plan.Adds.Select(add => add.Kind));
        var group = plan.Adds.Where(add => add.List == 0).Select(add => add.Box).ToList();
        Assert.All(group, box => Assert.Equal(group[0].top, box.top, 1));
        Assert.True(group[1].left > group[0].right && group[2].left > group[1].right, "the \"+\", the width, then the warning");
        var middle = plan.Lists[0].Left + (plan.Lists[0].Width / 2);
        Assert.InRange(middle - group[0].left - (group[2].right - middle), -1, 1);
        Assert.Equal(0, plan.Adds.Single(add => add.Kind == FolderAddKind.Hidden).List);
    }

    /// <summary>What the warning says, as the edit bar's "not shown" button says it: one widget by name, several counted.</summary>
    [UiFact]
    public void TheWarningNamesOneAndCountsSeveral()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var holder = BarKind.FolderList.New();
            foreach (var kind in new[] { ListKind.EnergySaver, ListKind.NightLight, ListKind.AirplaneMode })
            {
                holder.Folder!.Put(kind.New());
            }
            using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Config());
            var widgets = folder.Tiles.Select(tile => tile.Island).ToList();

            Assert.Equal(S.DockFolderNotShownOne(widgets[1].Info.Title), FolderFlyout.HiddenTitle([widgets[1]]));
            Assert.Equal(S.DockFolderNotShownMany(3), FolderFlyout.HiddenTitle(widgets));
            Assert.Equal(widgets[1].Info.Glyph, FolderFlyout.HiddenGlyph([widgets[1]]));
            Assert.Equal(EditRailPlan.Glyphs.NotShown, FolderFlyout.HiddenGlyph(widgets));
        });
    }

    /// <summary>
    /// A line carried under the last line a list shows lands there — before the lines it leaves out,
    /// not after them, where it would go out of sight the moment it was let go.
    /// </summary>
    [Fact]
    public void ALineCarriedUnderTheLastShownLandsBeforeTheHiddenOnes()
    {
        var options = Lists(Lines(30), Lines(2));
        var plan = Plan(options, 600, editing: true);
        var places = options.Cells.Select(cell => Karpo.Core.Folders.PlaceOf(options, cell)).ToList();
        var lastShown = Of(plan, 0).Last(plan.Shows);
        var under = plan.Cards[lastShown].bottom + 4;
        var moving = Of(plan, 1)[0];

        var gap = FolderDrag.Gap(plan, places, moving, 0, under, under + 40);

        Assert.Equal(lastShown + 1, gap);
    }

    /// <summary>
    /// The open folder, arranged: the warning stands beside the overfull list's width button, its
    /// press asks the dock for the widgets left out, in order, and holds the button pressed while their
    /// menu stands. Pictured overfull at rest and arranged, in both themes.
    /// </summary>
    [UiFact]
    public void TheOpenFolderWarnsAndListsWhatItLeftOut()
    {
        WindowShots.OnHiddenDesktop("folder-hidden", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 1100, bottom = 500 };
            var anchor = new POINT { x = 550, y = 500 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var dark in new[] { true, false })
            {
                var holder = BarKind.FolderList.New();
                holder.Folder!.Lists = [[], []];
                foreach (var kind in new[] { ListKind.Clock, ListKind.EnergySaver, ListKind.NightLight, ListKind.AirplaneMode, ListKind.Battery, ListKind.Cpu, ListKind.Memory, ListKind.SoundVolume })
                {
                    holder.Folder.Put(kind.New(), 0);
                }
                foreach (var kind in new[] { ListKind.BluetoothSwitch, ListKind.WifiSwitch })
                {
                    holder.Folder.Put(kind.New(), 1);
                }
                foreach (var kind in new[] { ListKind.MicrophoneSwitch, ListKind.Brightness, ListKind.Disk })
                {
                    holder.Folder.Put(kind.New(), 0);
                }
                using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Config());
                var name = $"folder-hidden-{(dark ? "dark" : "light")}";

                flyout.Show(gfx, TestLooks.LookFor(dark), 1f, anchor, work, Edge.Bottom, folder);
                WindowShots.Settle(300);
                var rest = flyout.Plan!;
                Assert.NotEmpty(rest.Lists[0].Hidden);
                Assert.Empty(rest.Lists[1].Hidden);
                Assert.Empty(rest.Adds);
                Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-rest.png"));
                flyout.Hide();

                flyout.Show(gfx, TestLooks.LookFor(dark), 1f, anchor, work, Edge.Bottom, folder, editing: true);
                WindowShots.Settle(300);
                var plan = flyout.Plan!;
                var warning = plan.Adds.Select((add, index) => (add, index)).Single(entry => entry.add.Kind == FolderAddKind.Hidden).index;
                Assert.Equal(0, plan.Adds[warning].List);
                Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-edit.png"));

                var at = FolderEditClickTests.Client(flyout.Hwnd, flyout.ScreenOfAdd(warning));
                _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, at);
                var pressed = flyout.Message(WM.WM_LBUTTONUP, 0, at);
                Assert.Equal(FolderFlyoutAction.NotShown, pressed.Kind);
                Assert.Equal(plan.Lists[0].Hidden.Select(index => folder.Tiles[index].Island), pressed.Hidden!);
                Assert.True(flyout.IsOpen);
                Assert.True(flyout.StaysOpen);
                flyout.MenuClosed();
                flyout.Hide();
            }
        });
    }

    /// <summary>The folder of <see cref="TheOpenFolderWarnsAndListsWhatItLeftOut"/>: eleven lines in a first list, more than fit, and two in a second.</summary>
    private static BarItem Overfull()
    {
        var holder = BarKind.FolderList.New();
        holder.Folder!.Lists = [[], []];
        foreach (var kind in new[] { ListKind.Clock, ListKind.EnergySaver, ListKind.NightLight, ListKind.AirplaneMode, ListKind.Battery, ListKind.Cpu, ListKind.Memory, ListKind.SoundVolume, ListKind.MicrophoneSwitch, ListKind.Brightness, ListKind.Disk })
        {
            holder.Folder.Put(kind.New(), 0);
        }
        foreach (var kind in new[] { ListKind.BluetoothSwitch, ListKind.WifiSwitch })
        {
            holder.Folder.Put(kind.New(), 1);
        }
        return holder;
    }

    /// <summary>
    /// Lines carried together (FolderFlyout.Pending.cs) and the lines a list leaves out: a line not shown
    /// is never carried with the selected ones, though selected; and the group let go under the last
    /// line shown lands before the ones not shown — in sight, as one line does — not after them.
    /// </summary>
    [UiFact]
    public void AGroupCarriedUnderTheLastShownLandsBeforeTheHiddenOnes()
    {
        WindowShots.OnHiddenDesktop("folder-hidden-group", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 1100, bottom = 500 };
            var anchor = new POINT { x = 550, y = 500 };
            var holder = Overfull();
            var config = new Config();
            using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, anchor, work, Edge.Bottom, folder, editing: true);
            WindowShots.Settle(100);
            var plan = flyout.Plan!;
            var hidden = plan.Lists[0].Hidden;
            Assert.True(hidden.Count >= 2);
            var keys = folder.Tiles.Select(tile => tile.Island.Key).ToList();
            var lastShown = Of(plan, 0).Last(plan.Shows);
            flyout.Picked = keys[0];
            flyout.AlsoPicked = [keys[1], keys[hidden[0]]];

            var press = flyout.ScreenOfCard(0);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, FolderEditClickTests.Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, FolderEditClickTests.Client(flyout.Hwnd, press with { y = press.y - GetSystemMetrics(SM.SM_CYDRAG) - 2 }));
            Assert.Equal([keys[0], keys[1]], flyout.CarriedTogether);

            // Over the lower half of the last line shown: the stack's middle under its middle.
            var under = flyout.ScreenOfCard(lastShown);
            under.y += 4;
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, FolderEditClickTests.Client(flyout.Hwnd, under));
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, FolderEditClickTests.Client(flyout.Hwnd, under));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal([keys[0], keys[1]], placed.Keys);

            // The dock's half: the two right after the last line shown, the hidden ones after them.
            var cells = new[] { 0, 1 }.Select(index => folder.Tiles[index].Cell).ToList();
            Assert.True(Karpo.Core.Folders.MoveGroup(holder.Folder!, cells, placed.Place));
            var order = holder.Folder!.Lists[0].Select(cell => cell.Island.Id).ToList();
            var expected = Enumerable.Range(2, lastShown - 1).Concat([0, 1]).Concat(hidden).Select(index => folder.Tiles[index].Cell.Island.Id).ToList();
            Assert.Equal(expected, order);
            flyout.Hide();
        });
    }

    /// <summary>
    /// A line's height changed in its menu (LineHeightRows.cs) lays the open folder out again: a line
    /// grown past what fits leaves the last lines out and the warning comes; shrunk back, they show and
    /// it goes. Laid out again against the work area the folder opened in, not its monitor's, which
    /// on the test's desktop is far taller than the 500 px handed over and left nothing out.
    /// </summary>
    [UiFact]
    public void AHeightChangedInTheMenuBringsAndTakesTheWarning()
    {
        WindowShots.OnHiddenDesktop("folder-hidden-height", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 1100, bottom = 500 };
            var anchor = new POINT { x = 550, y = 500 };
            var holder = BarKind.FolderList.New();
            var tall = ListKind.Clipboard.New();
            holder.Folder!.Put(tall);
            foreach (var kind in Lines(6))
            {
                holder.Folder.Put(kind.New());
            }
            _ = LineHeightRows.Resize(tall, LineHeight.Exactly(2));
            var config = new Config();
            using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, anchor, work, Edge.Bottom, folder, editing: true);
            WindowShots.Settle(100);
            Assert.Empty(flyout.Plan!.Hidden);
            Assert.DoesNotContain(flyout.Plan!.Adds, add => add.Kind == FolderAddKind.Hidden);

            Assert.True(LineHeightRows.Resize(tall, LineHeight.Exactly(FolderOptions.MostCells)));
            folder.Configure(holder, config);
            WindowShots.Settle(100);
            Assert.NotEmpty(flyout.Plan!.Hidden);
            Assert.Contains(flyout.Plan!.Adds, add => add.Kind == FolderAddKind.Hidden);

            Assert.True(LineHeightRows.Resize(tall, LineHeight.Exactly(2)));
            folder.Configure(holder, config);
            WindowShots.Settle(100);
            Assert.Empty(flyout.Plan!.Hidden);
            Assert.DoesNotContain(flyout.Plan!.Adds, add => add.Kind == FolderAddKind.Hidden);
            flyout.Hide();
        });
    }
}
