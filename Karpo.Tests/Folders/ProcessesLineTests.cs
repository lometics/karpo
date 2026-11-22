// The top processes as a line (Karpo.Dock\Performance\ProcessesLine.cs, docs\widgets.md §141): a row
// per app, busiest first, as many as the line holds; figures at once where a reading is recent, else
// half a second on; read every two seconds while it shows; a click opens Task Manager; a row's right
// click names the app with "End task" and "Open file location". Every process is the stand-in's
// (ProcessStand): nothing of this PC's is read, nothing opened, nothing ended.
//
// Pictured: processes-line-<dark|light>.png — at 100 % and 150 %, its three item sizes, at 225 %
// text, a row under the pointer and one under the keys, and before its first figures.

using Karpo.Dock;
using Karpo.Dock.Performance;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class ProcessesLineTests
{
    /// <summary>The line, at <paramref name="height"/>, in a folder of its own: opened, so it has read.</summary>
    private static FolderIsland Made(Gfx gfx, LineHeight? height = null, ItemSize size = ItemSize.Standard, string id = "top") =>
        Folder(gfx, Line(ListKind.CpuProcesses, cell =>
        {
            cell.Id = id;
            cell.Height = height;
            cell.Items = size == ItemSize.Standard ? null : new ItemStyleOptions { Size = size };
        }));

    private static ProcessesLine LineOf(FolderIsland folder) => (ProcessesLine)folder.Tiles[0].Island;

    /// <summary>The style and the box the open folder draws line 0 with at <paramref name="scale"/>.</summary>
    private static (TileStyle Style, D2D_RECT_F Box) Drawn(FolderIsland folder, float scale = 1.5f, float text = 1f)
    {
        var look = LookFor(dark: true, text);
        var plan = Plan(folder, look, scale).Plan;
        var card = plan.Cards[0];
        return (FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look)), PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top));
    }

    /// <summary>
    /// The registry's answers: a list's card that grows (a number of apps), five rows unmeasured, up to
    /// twelve for a new line, which a click on acts (Task Manager); in Performance, nothing to set,
    /// rows of a size only. Its right click is the app's — end it, show its file — then Resource Monitor.
    /// </summary>
    [Fact]
    public void ItIsAGrowingLineThatActs()
    {
        var line = ListWidgets.Of(ListKind.CpuProcesses);
        Assert.Same(WidgetInfos.CpuProcesses, line.Info);
        Assert.Equal("cpuProcesses", line.Info.Id);
        Assert.Equal((ListLine.Card, 10, (2, 24), true, CellAct.Action, WidgetSource.Processes), (line.Line, line.Cells, line.Range, line.Grows, line.Act, line.Drawing));
        Assert.Equal(LineHeight.Automatic(24), LineHeight.StartFor(ListKind.CpuProcesses));
        Assert.Equal((WidgetCategory.Performance, WidgetOptionsKind.None, (Metric?)null), (line.Info.Category, line.Info.Options, line.Info.Metric));
        Assert.Equal(new ItemOffer(ItemParts.None, ItemParts.None), ItemStyles.Offer(ListKind.CpuProcesses));
        Assert.Equal([WidgetAct.EndTask, WidgetAct.OpenFileLocation, WidgetAct.ResourceMonitor], TheDock.ActsFor(ListKind.CpuProcesses));
    }

    /// <summary>
    /// The processor's preset is its graph, its details, its top processes — automatic up to five —
    /// and the Task Manager footer; the add list has the line in the processor's folder.
    /// </summary>
    [Fact]
    public void TheProcessorsPresetHasItsTopProcesses()
    {
        var preset = Presets.Find(Presets.CpuId)!;
        Assert.Equal(["cpu", "cpuDetails", "cpuProcesses", "actions"], preset.Items.Select(item => item.Form));
        Assert.Equal(LineHeight.Automatic(Presets.ProcessCells), preset.Items[2].Height);
        var system = AddTree.On(WidgetType.List)[1].Rows.OfType<AddFolder>().Single(folder => folder.Shelf.Id == "system");
        // The graph is Performance and the details Performance details, each its reading chosen on it (§228,
        // §318), the details in their own folder before the processes.
        var picks = AddTree.Picks(system.Rows).Where(pick => pick.Entry is { Premade: null }).Select(pick => pick.Entry!.Id).ToList();
        Assert.Equal(["cpu", "cpuDetails", "cpuProcesses"], picks.Where(id => id is "cpu" or "cpuDetails" or "cpuProcesses"));
    }

    /// <summary>
    /// As many apps as the line holds, busiest first, whole rows of a list row each: automatic up to
    /// five rows is five apps, fixed at four folder rows four, and the most it may be twelve — and
    /// compact rows fit more in the same height. Each row says the app's name with how many processes
    /// it is, and its share to a tenth of a percent.
    /// </summary>
    [UiFact]
    public void TheRowsAreAsManyAsTheLineHolds()
    {
        using var stand = ProcessStand.Use();
        Offscreen.OnStaThread(gfx =>
        {
            int Count(LineHeight height, ItemSize size = ItemSize.Standard)
            {
                using var folder = Made(gfx, height, size);
                var (style, box) = Drawn(folder);
                return LineOf(folder).Plan(box, style).Count;
            }
            Assert.Equal(5, Count(LineHeight.Automatic(10)));
            // Three folder rows (120 DIP, §442) hold three list rows of 40: n rows are n rows of the list now.
            Assert.Equal(3, Count(LineHeight.Exactly(6)));
            Assert.Equal(4, Count(LineHeight.Exactly(8)));
            Assert.Equal(11, Count(LineHeight.Automatic(24)));
            Assert.True(Count(LineHeight.Exactly(10), ItemSize.Compact) > Count(LineHeight.Exactly(10)));
            Assert.True(Count(LineHeight.Exactly(10), ItemSize.Large) < Count(LineHeight.Exactly(10)));

            using var made = Made(gfx, LineHeight.Automatic(10));
            var (at, inside) = Drawn(made);
            var rows = LineOf(made).Plan(inside, at);
            Assert.Equal(["chrome.exe", "code.exe", "svchost.exe", "explorer.exe", "dwm.exe"], rows.Select(row => row.Group.Key));
            Assert.Equal(S.WidgetProcessesNamed(name: "Google Chrome", count: 23), rows[0].Name);
            Assert.Equal("Windows Explorer", rows[3].Name);
            Assert.Equal(["18.4%", "7.2%", "3.3%", "2.1%", "1.6%"], rows.Select(row => row.Figure));
            Assert.DoesNotContain(LineOf(made).Ranked, group => group.Members.Any(member => member.Id == ProcessTop.IdleId));
        });
    }

    /// <summary>
    /// Read only while it shows, every two seconds, half a second while its first figures wait; a line
    /// opened within a minute of the last reading has its figures at once, one opened later waits for
    /// its second reading.
    /// </summary>
    [UiFact]
    public void ItReadsWhileItShowsAndHasFiguresAtOnceSoonAfter()
    {
        using var stand = ProcessStand.Use();
        Offscreen.OnStaThread(gfx =>
        {
            using var folder = Made(gfx, LineHeight.Automatic(10));
            var line = LineOf(folder);
            // Opened on a fresh machine: one reading, then the next on its first tick.
            Assert.NotEmpty(line.Ranked);
            Assert.Equal(ProcessesLine.EveryMs, line.IntervalMs);
            folder.Closed();
            var reads = stand.Stand.Reads;

            // Opened again soon after: figures at once, from the last reading.
            stand.Stand.Wait(10 * 10_000_000L);
            folder.Opened(gfx);
            Assert.NotEmpty(line.Ranked);
            folder.Closed();

            // Opened after more than a minute: no figures until the second reading.
            stand.Stand.Wait(90 * 10_000_000L);
            line.Visible(true);
            Assert.Empty(line.Ranked);
            Assert.Equal(ProcessesLine.FirstMs, line.IntervalMs);
            line.Tick(gfx);
            Assert.NotEmpty(line.Ranked);
            line.Visible(false);
            Assert.True(stand.Stand.Reads > reads);
        });
    }

    /// <summary>
    /// A click anywhere opens Task Manager; the row under the pointer, or under the keys, is the one a
    /// right click is about — kept for the pick, "End task" left out where it could end none (§308: the
    /// stand-in's processes are no processes), and so no name over nothing — and the keys walk the rows
    /// drawn and give the folder its keys
    /// back past either end.
    /// </summary>
    [UiFact]
    public void AClickOpensTaskManagerAndARightClickNamesTheApp()
    {
        using var stand = ProcessStand.Use();
        Offscreen.OnStaThread(gfx =>
        {
            using var folder = Made(gfx, LineHeight.Automatic(10));
            var line = LineOf(folder);
            var (style, box) = Drawn(folder);
            Offscreen.Bitmap(box.right, box.bottom, dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });
            var rows = line.Plan(box, style);

            Assert.Equal(default, line.Click(0));
            Assert.Equal(["taskmgr.exe"], stand.Opened);

            Assert.Null(line.MenuGroup);
            var second = rows[1].Box;
            line.CardHover(new((second.left + second.right) / 2, (second.top + second.bottom) / 2), box, style, WidgetSurface.List);
            Assert.Equal(2, line.Hot);
            Assert.Same(rows[1].Group, line.MenuGroup);
            var (items, acts) = TheDock.ActRows(TheDock.ActsFor(ListKind.CpuProcesses), line);
            Assert.DoesNotContain(acts, act => act.Act == WidgetAct.EndTask);
            Assert.DoesNotContain(items, item => item.Header || item.Separator);
            Assert.Same(rows[1].Group, ProcessesLine.Asked);
            // No file for the stand-in's apps, so no "Open file location"; Resource Monitor always.
            Assert.DoesNotContain(acts, act => act.Act == WidgetAct.OpenFileLocation);
            Assert.Contains(acts, act => act.Act == WidgetAct.ResourceMonitor);

            // §470: the row the folder's keys are on is what the menu key's menu is about.
            line.CardHover(null, box, style, WidgetSurface.List);
            line.KeyedPart = 1;
            Assert.Same(rows[0].Group, line.MenuGroup);
            line.KeyedPart = 0;
            Assert.Null(line.MenuGroup);
        });
    }

    /// <summary>The line as it opens and as it is worked, dark and light: processes-line-&lt;dark|light&gt;.png.</summary>
    [UiFact]
    public void ProcessesLinePictures()
    {
        foreach (var dark in new[] { true, false })
        {
            using var stand = ProcessStand.Use();
            Offscreen.OnStaThread(gfx =>
            {
                var folders = new List<FolderIsland>();
                try
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                        void Add(string caption, LineHeight height, ItemSize size = ItemSize.Standard, float text = 1f, Action<ProcessesLine, TileStyle, D2D_RECT_F>? work = null, bool unread = false)
                        {
                            var look = LookFor(dark, text);
                            var folder = Made(gfx, height, size, id: $"top{panels.Count}");
                            folders.Add(folder);
                            var line = LineOf(folder);
                            if (unread)
                            {
                                ProcessesLine.ForgetLatest();
                                stand.Stand.Wait(120 * 10_000_000L);
                                line.Visible(false);
                                line.Visible(true);
                            }
                            var plan = Plan(folder, look, scale).Plan;
                            var card = plan.Cards[0];
                            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                            work?.Invoke(line, FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look)), box);
                            panels.Add((caption, look, folder, plan));
                        }
                        Add($"Top processes: automatic up to {Presets.ProcessCells}", LineHeight.Automatic(Presets.ProcessCells));
                        Add("Compact: fixed 5 rows", LineHeight.Exactly(10), ItemSize.Compact);
                        Add("Large: fixed 5 rows", LineHeight.Exactly(10), ItemSize.Large);
                        Add("225 % text: fixed 8 rows", LineHeight.Exactly(16), text: 2.25f);
                        Add("The second row under the pointer", LineHeight.Automatic(Presets.ProcessCells), work: (line, style, box) =>
                        {
                            var row = line.Plan(box, style)[1].Box;
                            line.CardHover(new(row.left + 20, (row.top + row.bottom) / 2), box, style, WidgetSurface.List);
                        });
                        Add("Before its first figures", LineHeight.Automatic(Presets.ProcessCells), unread: true);
                        Sheet(gfx, $"processes-line-{scale * 100:0}-{(dark ? "dark" : "light")}.png", scale, panels);
                    }
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            });
        }
    }
}
