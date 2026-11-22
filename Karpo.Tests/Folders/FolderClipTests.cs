// No width, move or add is refused because the folder would not fit its display (the person,
// 2026-10-23: "rather than limit the user in what he can do layout wise i think we should rather
// allow a widget / widgets to clip over the edge and then just warn the user about that"; §320):
//
//  - **The plan** (pure): an arranged folder's lists past the room are OffScreen, nearest first, the
//    empty ones never; every list's buttons centred on what shows of it, none where too little does,
//    inside a list as tall as the room; the warning chip against the edge the first one runs past.
//  - **The open folder**: the person's News folder (six empty columns a far slot left, then a widest
//    clock and a wide line) on their 3840 px display at 150 %: every column its preset, at rest and
//    arranged, nothing said; widened to widest beside widest on a 1920 px display: the far column runs
//    past the screen's edge, arranged the chip says so on it, and the panel never moves in over a
//    side bar. Pictures: folderclip-*.png in KARPO_PNG_DIR.
//
// The open folder's half needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderClipTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly Look Light = Dark with { Dark = false };

    /// <summary>A folder of lists of battery lines, each named by its id: "a b | c", "| | a".</summary>
    private static FolderOptions Lists(string shape)
    {
        var folder = new FolderOptions { Lists = [] };
        foreach (var list in shape.Split('|'))
        {
            folder.Lists.Add([.. list.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var island = ListKind.Battery.New();
                island.Id = id;
                return new FolderCell { Island = island };
            })]);
        }
        return folder;
    }

    private static FolderPlan Arranged(FolderOptions options, float scale, Edge edge = Edge.Bottom) =>
        FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, 1000, editing: true, edge: edge);

    private static D2D_RECT_F Rect(float left, float top, float right, float bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    /// <summary>
    /// Arranged and placed so the room ends inside its third list: the lists with widgets past the
    /// room are OffScreen, the one cut before the one wholly past; an empty one past it never; each
    /// list's buttons centred on what shows of it, the one wholly past with none; the chip on the cut
    /// list, its margin in from the room's edge, at the list's middle.
    /// </summary>
    [Fact]
    public void TheListsPastTheRoomAreOffScreenNearestFirst()
    {
        var options = Lists("a | b | c | d | ");
        var plan = Arranged(options, 1);
        var third = plan.Lists[2].Box;
        var room = Rect(-200, -400, third.left + 100, plan.Height + 10);
        var placed = FolderFlyout.WithSlots(plan, 1, room, Edge.Bottom);
        Assert.Equal([2, 3], placed.OffScreen);

        // Every list's own buttons: a "+" and a width.
        foreach (var list in new[] { 0, 1 })
        {
            var own = placed.Adds.Where(add => add.List == list).ToList();
            var box = placed.Lists[list].Box;
            Assert.Equal(2, own.Count);
            Assert.Equal((box.left + box.right) / 2, (own[0].Box.left + own[1].Box.right) / 2, 1);
        }
        var cut = placed.Adds.Where(add => add.List == 2).ToList();
        Assert.Equal((third.left + room.right) / 2, (cut[0].Box.left + cut[1].Box.right) / 2, 1);
        Assert.All(placed.Adds.Where(add => add.List is 3 or 4), add => Assert.True(add.Box.right <= add.Box.left, "no button for a list past the room"));

        var size = new Vector2(220, 32);
        var chip = FolderFlyout.WarningBox(placed, room, size, 1);
        Assert.Equal(room.right - FolderFlyout.WarningMarginDip - size.X, chip.left);
        Assert.InRange(chip.top, ((third.top + third.bottom) / 2) - (size.Y / 2) - 1, ((third.top + third.bottom) / 2) - (size.Y / 2) + 1);

        // All on the screen: nothing said.
        var whole = FolderFlyout.WithSlots(plan, 1, Rect(-200, -400, plan.Width + 200, plan.Height + 10), Edge.Bottom);
        Assert.Empty(whole.OffScreen);
        Assert.Equal(default, FolderFlyout.WarningBox(whole, room, size, 1));
    }

    /// <summary>
    /// A list as tall as the room has no row past its far end for its buttons: they stand just inside
    /// it, the buttons' gap in, on a bottom bar under its top and on a top bar over its bottom, never
    /// outside the room.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ATallListsButtonsStandInsideItsFarEnd(Edge edge)
    {
        var options = Lists("a b c d e f g h i j k l m n o p q r s t u v w x y z");
        var plan = Arranged(options, 1, edge);
        var box = plan.Lists[0].Box;
        var room = Rect(-200, box.top, box.right + 200, box.bottom);
        var placed = FolderFlyout.WithSlots(plan, 1, room, edge);
        var own = placed.Adds.Where(add => add.List == 0).ToList();
        Assert.NotEmpty(own);
        Assert.All(own, add =>
        {
            Assert.True(add.Box.top >= room.top && add.Box.bottom <= room.bottom, $"{add.Kind} at {add.Box.top}..{add.Box.bottom} outside {room.top}..{room.bottom}");
            Assert.Equal(edge == Edge.Top ? box.bottom - FolderFlyout.AddApartDip - FolderFlyout.AddDip : box.top + FolderFlyout.AddApartDip, add.Box.top);
        });
    }

    /// <summary>
    /// The person's News folder as their config has it: six empty columns (medium, narrow, medium,
    /// medium, medium, wide) a far slot left, then a widest column with a clock and a wide one with a
    /// tall line, on a bar along the bottom of a 3840 by 2160 display at 150 %, the face left of the
    /// middle. Every list is its preset at rest and arranged — the clock's 1200 px, the other's 720,
    /// where they were both cut to 474 — the folder inside the work area, nothing said, the empty
    /// columns hanging off to the left. Widened to widest beside widest on a 1920 px display at 150 %,
    /// the two are still 1200 px each: the far one runs past the screen's right edge, at rest as
    /// arranged, and arranged it is OffScreen, the chip against that edge on it, its buttons on what
    /// shows of it.
    /// </summary>
    [UiFact]
    public void ThePersonsNewsFolderWidensPastTheScreenAndSaysSo()
    {
        WindowShots.OnHiddenDesktop("folderclip", (gfx, instance) =>
        {
            const float scale = 1.5f;
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var holder = BarKind.FolderList.New();
            holder.Folder!.Lists = [[], [], [], [], [], [], [], []];
            holder.Folder.Widths = [FolderListWidth.Medium, FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Widest, FolderListWidth.Wide];
            var clock = ListKind.Clock.New();
            clock.Height = LineHeight.Exactly(6);
            holder.Folder.Put(clock, 6);
            var tall = ListKind.Battery.New();
            tall.Height = LineHeight.Exactly(24);
            holder.Folder.Put(tall, 7);
            var config = new Config();
            foreach (var (screenWidth, screenHeight, widest) in new[] { (3840, 2160, false), (1920, 1080, true) })
            {
                if (widest)
                {
                    Assert.True(Karpo.Core.Folders.SetWidth(holder.Folder, 7, FolderListWidth.Widest));
                }
                var screen = new RECT { left = 0, top = 0, right = screenWidth, bottom = screenHeight };
                var work = screen with { bottom = screenHeight - 62 };
                var anchor = new POINT { x = (int)(screenWidth * 0.3f), y = work.bottom };
                foreach (var editing in new[] { false, true })
                {
                    foreach (var look in editing && widest ? new[] { Dark, Light } : [Dark])
                    {
                        using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
                        flyout.Show(gfx, look, scale, anchor, work, Edge.Bottom, folder, editing: editing, display: screen);
                        WindowShots.Settle(150);
                        var plan = flyout.Plan!;
                        var name = $"{screenWidth} px {(editing ? "arranged" : "at rest")}";
                        Assert.Equal([1200f, widest ? 1200f : 720f], plan.Lists.Skip(6).Select(list => list.Width));
                        var on = flyout.PanelOnScreen;
                        var core = plan.Core;
                        var coreRight = on.left + core.right;
                        if (!widest)
                        {
                            Assert.True(on.left + core.left >= work.left && coreRight <= work.right, $"{name}: {on.left + core.left}..{coreRight} outside the work area");
                            Assert.Empty(plan.OffScreen);
                            Assert.Equal(default, flyout.WarningShown);
                        }
                        else
                        {
                            // Its start as far in as a flyout's margin, its far end past the screen's.
                            Assert.Equal(work.left + PopupPaint.Round(12 * scale), on.left + core.left, 1);
                            Assert.True(coreRight > work.right, $"{name}: ends at {coreRight}, inside {work.right}");
                            if (editing)
                            {
                                Assert.Equal([7], plan.OffScreen);
                                var chip = flyout.WarningShown;
                                Assert.True(chip.right > chip.left, "no off-screen warning");
                                Assert.Equal(work.right - PopupPaint.Round(FolderFlyout.WarningMarginDip * scale), on.left + chip.right, 1);
                                var list = plan.Lists[7].Box;
                                Assert.True(chip.left >= list.left && chip.top >= list.top && chip.bottom <= list.bottom, "the chip on the column it cuts");
                                // The cut column's buttons on what shows of it, on the screen.
                                foreach (var index in Enumerable.Range(0, plan.Adds.Count).Where(index => plan.Adds[index].List == 7))
                                {
                                    var at = flyout.ScreenOfAdd(index);
                                    Assert.InRange(at.x, on.left + (int)list.left, work.right);
                                }
                            }
                            else
                            {
                                Assert.Empty(plan.OffScreen);
                                Assert.Equal(default, flyout.WarningShown);
                            }
                        }
                        var file = $"folderclip-news-{screenWidth}-{(widest ? "widest-" : "")}{(editing ? "edit" : "rest")}-{(look.Dark ? "dark" : "light")}-150.png";
                        if (screenWidth == 1920 || !editing)
                        {
                            Assert.True(WindowShots.SaveTogether([(flyout.Hwnd, screen)], file, (0x30, 0x30, 0x30), over: editing ? new[] { flyout.Hwnd } : null));
                        }
                        flyout.Hide();
                    }
                }
            }
        });
    }

    /// <summary>
    /// The width menu says beforehand which widths run the folder off the screen (§328): a list's
    /// width counts where it is between the lists with widgets, by as much as it changes their box;
    /// one outside them never does, nor does a list on a display with room. The rows of those widths
    /// wear the warning glyph and "Runs off the screen", ticked or not, and still pick.
    /// </summary>
    [Fact]
    public void AWidthThatRunsOffIsKnownBeforeItIsPicked()
    {
        var options = Lists(" | a |  | b | ");
        options.Widths = [FolderListWidth.Medium, FolderListWidth.Widest, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium];
        var plan = Arranged(options, 1);
        var core = plan.Core.right - plan.Core.left;
        Assert.Equal(800 + plan.ListGap + 360 + plan.ListGap + 360, core, 1);
        // Room for the box as it is and 200 px more: medium's 360 grows to wide's 480, not to extra wide's 640.
        var room = core + 200;
        foreach (var list in new[] { 2, 3 })
        {
            Assert.Equal([FolderListWidth.ExtraWide, FolderListWidth.Widest], FolderListWidths.All.Where(width => FolderFlyout.RunsOff(plan, list, width, 1, room)));
        }
        // The widest list itself: narrower always fits, widest is what it is.
        Assert.Empty(FolderListWidths.All.Where(width => FolderFlyout.RunsOff(plan, 1, width, 1, room)));
        Assert.All(FolderListWidths.All, width => Assert.False(FolderFlyout.RunsOff(plan, 0, width, 1, 10), "outside the lists with widgets"));
        Assert.All(FolderListWidths.All, width => Assert.False(FolderFlyout.RunsOff(plan, 4, width, 1, 10), "outside the lists with widgets"));
        Assert.All(FolderListWidths.All, width => Assert.False(FolderFlyout.RunsOff(plan, -1, width, 1, 10), "a slot"));

        var rows = Karpo.Dock.Dock.FolderWidthRows(FolderListWidth.Widest, [FolderListWidth.ExtraWide, FolderListWidth.Widest]);
        Assert.Equal(FolderListWidths.All.Count, rows.Count);
        Assert.All(rows, row => Assert.True(row.Enabled));
        foreach (var (row, width) in rows.Zip(FolderListWidths.All))
        {
            var marked = width is FolderListWidth.ExtraWide or FolderListWidth.Widest;
            Assert.Equal(marked ? S.DockFolderWidthOffScreen : null, row.Detail);
            Assert.Equal(marked, row.Icon is FlyoutIcon.Glyph);
            Assert.Equal(width == FolderListWidth.Widest, row.Checked);
        }
        Assert.All(Karpo.Dock.Dock.FolderWidthRows(FolderListWidth.Medium), row => Assert.Null(row.Detail));
    }

    /// <summary>
    /// The open folder's width menu marks exactly the widths that, picked, have the folder say it is
    /// off screen: a widest clock beside a list of another width on a 1920 px display at 150 %, along
    /// a bottom bar and beside a left and a right one, each width of each list asked first
    /// (<see cref="FolderFlyout.RunsOffAt"/>) and then picked and laid out (<see cref="FolderPlan.OffScreen"/>).
    /// </summary>
    [UiFact]
    public void TheWidthMenusMarksAreWhatPickingSays()
    {
        WindowShots.OnHiddenDesktop("folderclip-marks", (gfx, instance) =>
        {
            const float scale = 1.5f;
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var config = new Config();
            var screen = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
            const int strip = 72;
            foreach (var edge in new[] { Edge.Bottom, Edge.Left, Edge.Right })
            {
                var work = edge switch
                {
                    Edge.Left => screen with { left = strip },
                    Edge.Right => screen with { right = screen.right - strip },
                    _ => screen with { bottom = screen.bottom - 62 },
                };
                var anchor = edge switch
                {
                    Edge.Left => new POINT { x = strip, y = 540 },
                    Edge.Right => new POINT { x = screen.right - strip, y = 540 },
                    _ => new POINT { x = 576, y = work.bottom },
                };
                var marked = new List<string>();
                foreach (var list in new[] { 0, 1 })
                {
                    foreach (var width in FolderListWidths.All)
                    {
                        var holder = BarKind.FolderList.New();
                        holder.Folder!.Lists = [[], []];
                        holder.Folder.Widths = [FolderListWidth.Widest, FolderListWidth.Medium];
                        holder.Folder.Put(ListKind.Clock.New(), 0);
                        holder.Folder.Put(ListKind.Battery.New(), 1);
                        using (var before = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config))
                        {
                            flyout.Show(gfx, Dark, scale, anchor, work, edge, before, editing: true, display: screen);
                            var says = flyout.RunsOffAt(list).Contains(width);
                            flyout.Hide();
                            _ = Karpo.Core.Folders.SetWidth(holder.Folder, list, width);
                            using var after = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
                            flyout.Show(gfx, Dark, scale, anchor, work, edge, after, editing: true, display: screen);
                            Assert.True(says == flyout.Plan!.OffScreen.Count > 0, $"{edge}, list {list} {width}: marked {says}, off screen {string.Join(",", flyout.Plan.OffScreen)}");
                            flyout.Hide();
                            if (says)
                            {
                                marked.Add($"{list}:{width}");
                            }
                        }
                    }
                }
                // Some widths fit and some run off, on every edge: the marks tell them apart.
                Assert.Contains("1:Widest", marked);
                Assert.DoesNotContain("1:Narrow", marked);
            }
        });
    }

    /// <summary>
    /// Across a side bar a folder wider than the room beside it keeps its near side the gap off the
    /// bar and runs past the screen's far edge: it was moved back in over the bar.
    /// </summary>
    [UiFact]
    public void ASideBarsWideFolderRunsPastTheFarEdgeNotOverTheBar()
    {
        WindowShots.OnHiddenDesktop("folderclip-side", (gfx, instance) =>
        {
            const float scale = 1.5f;
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var holder = BarKind.FolderList.New();
            holder.Folder!.Lists = [[], []];
            holder.Folder.Widths = [FolderListWidth.Widest, FolderListWidth.Widest];
            holder.Folder.Put(ListKind.Clock.New(), 0);
            holder.Folder.Put(ListKind.Battery.New(), 1);
            var config = new Config();
            var screen = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
            const int strip = 72;
            foreach (var edge in new[] { Edge.Left, Edge.Right })
            {
                var work = edge == Edge.Left ? screen with { left = strip } : screen with { right = screen.right - strip };
                var anchor = new POINT { x = edge == Edge.Left ? strip : screen.right - strip, y = 540 };
                using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, config);
                flyout.Show(gfx, Dark, scale, anchor, work, edge, folder, editing: true, display: screen);
                WindowShots.Settle(150);
                var on = flyout.PanelOnScreen;
                var gap = PopupPaint.Round(12 * scale);
                if (edge == Edge.Left)
                {
                    Assert.Equal(anchor.x + gap, on.left, 1);
                    Assert.True(on.right > screen.right);
                }
                else
                {
                    Assert.Equal(anchor.x - gap, on.right, 1);
                    Assert.True(on.left < screen.left);
                }
                Assert.Equal([1], flyout.Plan!.OffScreen);
                Assert.True(flyout.WarningShown.right > flyout.WarningShown.left);
                Assert.True(WindowShots.SaveTogether([(flyout.Hwnd, screen)], $"folderclip-side-{edge.ToString().ToLowerInvariant()}-edit-dark-150.png", (0x30, 0x30, 0x30), over: [flyout.Hwnd]));
                flyout.Hide();
            }
        });
    }
}
