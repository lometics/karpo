// Task view in a folder (the person, 2026-09-27: "add a shortcut widget to the windows windows and
// desktop overview as well. to the taskbar and to the folder views. (same design as the other one
// icon designs.)"): the bar's button and a list's line, found on both pages of the library, each
// pressing Win+Tab through the one seam every Task view button presses (Desktops.TaskView).
//
// Nothing here presses a key on the machine: the seam is handed a count for the length of a test
// (Desktops.TaskViewKeys). The line is the list's own row of its glyph and its name, as a pin's and
// a switch's are. It leaves a sheet of the line, KARPO_PNG_DIR\list-taskView.png.
//
// The click and the drawings need Direct2D: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

// Karpo.Dock is the namespace as well as the class, and the namespace wins from in here.
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class TaskViewTests
{
    // --- Where it stands ------------------------------------------------------------------------

    /// <summary>On the Taskbar and List pages of the library alike, each row placing that page's own member.</summary>
    [Fact]
    public void ItIsOnEveryPageOfTheLibrary()
    {
        foreach (var page in Catalogue.Pages)
        {
            var row = Assert.Single(Catalogue.On(page), entry => entry.Id == "taskView");
            Assert.Equal("Task view", row.Title);
            Assert.Equal("Your open windows and desktops at a glance (Win+Tab)", row.Blurb);
            Assert.Equal("", row.Glyph);
        }
        Assert.Equal(BarKind.TaskView, Catalogue.On(WidgetType.Bar).Single(entry => entry.Id == "taskView").Bar);
        Assert.Equal(ListKind.TaskView, Catalogue.On(WidgetType.List).Single(entry => entry.Id == "taskView").List);
    }

    // --- A click --------------------------------------------------------------------------------

    /// <summary>
    /// The bar's button and a list's line both press Win+Tab through the one seam
    /// (Desktops.TaskView). In a folder the widget acts itself and hands the folder nothing to carry
    /// out — its click is an act, not a flyout of the folder's — so the folder is left to close as
    /// Task view takes the screen.
    /// </summary>
    [UiFact]
    public void TheBarsButtonAndAFoldersLinePressTheOneSeam()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var pressed = 0;
            var before = Desktops.TaskViewKeys;
            Desktops.TaskViewKeys = () => pressed++;
            try
            {
                Assert.True(TheDock.PressWindowsButton(HitKind.TaskView));
                Assert.Equal(1, pressed);

                var holder = BarKind.FolderList.New();
                holder.Id = "holder";
                holder.Folder = new FolderOptions();
                var cell = ListKind.TaskView.New();
                cell.Id = "taskview";
                holder.Folder.Put(cell);
                using var folder = new FolderIsland(Config.IslandKey(null, "holder"), holder, gfx, new Config());
                var tile = Assert.Single(folder.Tiles);
                Assert.IsType<TaskViewTile>(tile.Island);

                var action = tile.Island.Click(0);

                Assert.Equal(2, pressed);
                var act = FolderFlyout.ActOf(tile.Cell);
                Assert.Equal(CellAct.Action, act);
                Assert.False(FolderFlyout.Forwards(action, act), "Task view hands a list folder something to carry out");
            }
            finally
            {
                Desktops.TaskViewKeys = before;
            }
        });
    }

    // --- The right click -----------------------------------------------------------------------

    /// <summary>
    /// A right click on the line gives "New desktop" (Win+Ctrl+D) over "Task view" (Win+Tab), every
    /// desktop menu's one order (§308), each through its seam, and each hands the screen to Windows, so the folder
    /// closes behind it. Nothing here presses a key on the machine: both seams count instead.
    /// </summary>
    [Fact]
    public void ItsMenuOpensTaskViewAndMakesANewDesktop()
    {
        Assert.Equal([WidgetAct.NewDesktop, WidgetAct.TaskView], TheDock.ActsFor(ListKind.TaskView));
        var (items, rows) = TheDock.ActRows(TheDock.ActsFor(ListKind.TaskView), widget: null);
        Assert.Equal(["New desktop", "Task view"], items.Select(item => item.Text));
        var (views, desktops) = (0, 0);
        var (taskView, newDesktop) = (Desktops.TaskViewKeys, Desktops.NewDesktopKeys);
        Desktops.TaskViewKeys = () => views++;
        Desktops.NewDesktopKeys = () => desktops++;
        try
        {
            var opened = new List<string>();
            TheDock.RunAct(rows[1], widget: null, (target, _) => opened.Add(target));
            Assert.Equal((1, 0), (views, desktops));
            TheDock.RunAct(rows[0], widget: null, (target, _) => opened.Add(target));
            Assert.Equal((1, 1), (views, desktops));
            Assert.Empty(opened);
        }
        finally
        {
            (Desktops.TaskViewKeys, Desktops.NewDesktopKeys) = (taskView, newDesktop);
        }
        Assert.True(TheDock.HandsAway(WidgetAct.TaskView));
        Assert.True(TheDock.HandsAway(WidgetAct.NewDesktop));
    }

    // --- The line -------------------------------------------------------------------------------

    /// <summary>
    /// Its line is the list's own row of its glyph and its name, one row tall, as a pin's and a
    /// switch's are: at 100, 150 and 200 %, at ordinary and the largest text, it draws inside its
    /// line, and its name is whole. The sheet shows it at each.
    /// </summary>
    [UiFact]
    public void ItsLineIsTheListsOwnRowAtEveryScale()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var wrong = new List<string>();
            var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
            var before = PaintWatch.Watching;
            PaintWatch.Watching = static (_, _, color) => color;
            try
            {
                foreach (var scale in new[] { 1f, 1.5f, 2f })
                {
                    foreach (var textScale in ListFormPictures.TextScales)
                    {
                        var look = ListFormPictures.LookFor(dark: true, textScale);
                        var folder = ListFormPictures.Folder(gfx, ListFormPictures.Line(ListKind.TaskView));
                        var (_, plan) = ListFormPictures.Plan(folder, look, scale);
                        var what = $"{scale:0.##}x, text {textScale:0.##}";
                        if (Drawn(gfx, folder, plan, look, scale) is { } fault)
                        {
                            wrong.Add($"{what}: {fault}");
                        }
                        if (scale == 1.5f)
                        {
                            panels.Add(($"Task view: 150 %, text {textScale * 100:0} %", look, folder, plan));
                        }
                        else
                        {
                            folder.Dispose();
                        }
                    }
                }
            }
            finally
            {
                PaintWatch.Watching = before;
            }
            try
            {
                ListFormPictures.Sheet(gfx, "list-taskView.png", 1.5f, panels);
            }
            finally
            {
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Dispose();
                }
            }
            Assert.True(wrong.Count == 0, string.Join("\n", wrong));
        });
    }

    /// <summary>What is wrong with the one line of <paramref name="plan"/> as the folder draws it; null for nothing.</summary>
    private static string? Drawn(Gfx gfx, FolderIsland folder, FolderPlan plan, Look look, float scale)
    {
        var card = plan.Cards[0];
        var width = card.right - card.left;
        var height = card.bottom - card.top;
        var margin = PopupPaint.Round(12 * scale);
        var tile = folder.Tiles[0];
        var theme = FlyoutTheme.For(look);
        (float Left, float Top, float Right, float Bottom) ink = default;
        PaintWatch.Trimmed = 0;
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () =>
                FolderFlyout.PaintListLine(dc, gfx, tile.Island, tile.Cell.Island, PopupPaint.Rect(0, 0, width, height), plan, theme, scale)),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        if (PaintWatch.Trimmed > 0)
        {
            return $"{PaintWatch.Trimmed} line(s) cut with an ellipsis";
        }
        if (ink.Right <= ink.Left)
        {
            return "nothing drawn";
        }
        if (ink.Left < margin - 1 || ink.Top < margin - 1 || ink.Right > margin + width + 1 || ink.Bottom > margin + height + 1)
        {
            return $"drawn past its line: ink {ink.Left - margin:0},{ink.Top - margin:0} to {ink.Right - margin:0},{ink.Bottom - margin:0} in a line {width:0} by {height:0}";
        }
        return null;
    }
}
