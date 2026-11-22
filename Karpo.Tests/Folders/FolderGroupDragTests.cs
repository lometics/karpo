// edit: several selected lines of an arranged folder carried together, and lines on the clipboard
// in the marching ants (FolderFlyout.Pending.cs, FolderFlyout.Ants.cs; the person, 2026-10-12:
// "dragging multiple widgets in folder edit mode should work" ... "copy pasting should also work into
// columns that are empty" ... "the item that is currently in the clipboard should get a look like an
// excel cell does when it is being copied"). On real folder windows on a desktop of their own, the
// dock's half done by hand (Folders.MoveGroup and Configure, as Dock.PlaceInFolder does):
//
//  - a press on one of two selected lines and a drag lifts both, stacked, the other line where the
//    stack has it; over an empty column they would land there, and let go they are one Place for
//    the dock with both keys, in their order;
//  - Escape while a line is carried puts it back: nothing lifted, and the release is no move;
//  - pasted lines carried with no button held land in an empty column as a click puts them down;
//  - a copied line wears the ants, marching; not while lines are carried; a frame sheet,
//    folder-group-<look>.png in KARPO_PNG_DIR: the stack being carried over the empty column, and
//    the copied lines at rest.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.FolderEditClickTests;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderGroupDragTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>A list folder: the clock's line, the battery's and the airplane switch's in one list, and an empty column beside it.</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        foreach (var (line, id) in new[] { (ListKind.Clock.New(), "clock"), (ListKind.Battery.New(), "battery"), (ListKind.AirplaneMode.New(), "airplane") })
        {
            line.Id = id;
            line.View = Karpo.Core.Folders.View(island.Kind);
            island.Folder!.Put(line);
        }
        island.Folder!.Lists.Add([]);
        return island;
    }

    private static string Key(string id) => $"{Owner}/box/{id}";

    private static string Shown(FolderOptions folder) =>
        string.Join(" | ", folder.Lists.Select(list => string.Join(" ", list.Select(cell => cell.Island.Id))));

    /// <summary>Where on the screen the middle of list <paramref name="list"/>'s panel stands.</summary>
    private static POINT ScreenOfList(FolderFlyout flyout, int list)
    {
        var box = flyout.Plan!.Lists[list].Box;
        var on = flyout.PanelOnScreen;
        return new POINT { x = on.left + (int)MathF.Round((box.left + box.right) / 2), y = on.top + (int)MathF.Round((box.top + box.bottom) / 2) };
    }

    /// <summary>
    /// Two lines selected, the clock's and the airplane switch's with the battery's between: a drag
    /// from the clock lifts both, the airplane's under the clock's in the stack; over the empty column
    /// they would land at its top, and let go they are moved there together, in their order. Escape
    /// while the battery's is carried puts it back, and its release moves nothing.
    /// </summary>
    [UiFact]
    public void SelectedLinesAreCarriedTogetherIntoAnEmptyColumn()
    {
        WindowShots.OnHiddenDesktop("folder-group-drag", (gfx, instance) =>
        {
            var item = Folder();
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            flyout.Picked = Key("clock");
            flyout.AlsoPicked = [Key("airplane")];
            var plan = flyout.Plan!;
            var slop = GetSystemMetrics(SM.SM_CYDRAG);
            var press = flyout.ScreenOfCard(0);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - slop - 2 }));
            Assert.Equal([Key("clock"), Key("airplane")], flyout.CarriedTogether);
            Assert.True(flyout.LiftedLine.Lifted);
            // The airplane's line on its plate, right under the clock's in the stack.
            var clockTall = plan.Cards[0].bottom - plan.Cards[0].top;
            Assert.Equal(flyout.LiftedLine.At + new Vector2(0, clockTall + 2), flyout.LineAt(2));
            Assert.Equal(0, flyout.AlsoPickedShown);

            var column = ScreenOfList(flyout, 1);
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, column));
            Assert.Equal(new FolderPlace(1, 0), flyout.Aimed);
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, column));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal(new FolderPlace(1, 0), placed.Place);
            Assert.Equal([Key("clock"), Key("airplane")], placed.Keys);
            Assert.Empty(flyout.CarriedTogether);

            // The dock's half.
            var cells = new[] { "clock", "airplane" }.Select(id => item.Folder!.Cells.First(each => each.Island.Id == id)).ToList();
            Assert.True(Karpo.Core.Folders.MoveGroup(item.Folder!, cells, placed.Place));
            folder.Configure(item, config);
            Assert.Equal("battery | clock airplane", Shown(item.Folder!));
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
            _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
            Assert.False(flyout.LiftedLine.Lifted);

            // Escape mid-drag: back where it was, and the release is no move.
            flyout.Picked = Key("battery");
            flyout.AlsoPicked = [];
            var battery = flyout.ScreenOfCard(1);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, battery));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, ScreenOfList(flyout, 1)));
            Assert.True(flyout.LiftedLine.Lifted);
            Assert.Empty(flyout.CarriedTogether);
            _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            Assert.True(flyout.IsOpen, "Escape put the line back; the folder stays");
            Assert.False(flyout.LiftedLine.Lifted);
            Assert.Null(flyout.Aimed);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, ScreenOfList(flyout, 1))).Kind);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Lines pasted (Dock.PasteLine) are carried with no button held, together; a click over the empty
    /// column puts them down there, both, in their order.
    /// </summary>
    [UiFact]
    public void PastedLinesArePutDownInAnEmptyColumn()
    {
        WindowShots.OnHiddenDesktop("folder-group-paste", (gfx, instance) =>
        {
            var item = Folder();
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);

            // The dock's paste: copies at the end of the first list, then carried.
            foreach (var (kind, id) in new[] { (ListKind.Cpu, "cpu"), (ListKind.Memory, "memory") })
            {
                var line = kind.New();
                line.Id = id;
                line.View = Karpo.Core.Folders.View(item.Kind);
                item.Folder!.Put(line);
            }
            folder.Configure(item, config);
            var column = ScreenOfList(flyout, 1);
            _ = SetCursorPos(column.x, column.y);
            Assert.True(flyout.Carry([Key("cpu"), Key("memory")]));
            Assert.True(flyout.Carrying);
            Assert.Equal([Key("cpu"), Key("memory")], flyout.CarriedTogether);

            var landed = flyout.LandCarry(ScreenOfList(flyout, 1));
            Assert.Equal(FolderFlyoutAction.Place, landed.Kind);
            Assert.Equal(new FolderPlace(1, 0), landed.Place);
            Assert.Equal([Key("cpu"), Key("memory")], landed.Keys);
            Assert.False(flyout.Carrying);
            var cells = new[] { "cpu", "memory" }.Select(id => item.Folder!.Cells.First(each => each.Island.Id == id)).ToList();
            Assert.True(Karpo.Core.Folders.MoveGroup(item.Folder!, cells, landed.Place));
            Assert.Equal("clock battery airplane | cpu memory", Shown(item.Folder!));
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// Copied lines wear the marching ants while the folder is arranged, marching, in place of their
    /// ring; a lifted line does not. The sheet: dark and light, the copied lines at rest, and two
    /// selected lines carried together over the empty column (folder-group-&lt;look&gt;.png).
    /// </summary>
    [UiFact]
    public void CopiedLinesWearTheMarchingAnts()
    {
        WindowShots.OnHiddenDesktop("folder-group-ants", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var item = Folder();
                var config = new Config();
                var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, dark ? Dark : Light, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
                var frames = new List<(byte[] Canvas, int Width, int Height)>();
                void Frame(int wait)
                {
                    WindowShots.Settle(wait);
                    Assert.True(WindowShots.Save(flyout.Hwnd, "folder-group-frame.png", seen: (pixels, width) => frames.Add((pixels, width, pixels.Length / 4 / width))));
                }

                flyout.Picked = Key("clock");
                flyout.AlsoPicked = [Key("airplane")];
                flyout.Copied = [Key("clock"), Key("battery")];
                Assert.Equal([0, 1], flyout.CopiedShown);
                Assert.True(flyout.AntsMarch);
                // The clock's ring gives way to the ants; the airplane's, not copied, stays.
                Assert.False(flyout.PickedShown);
                Assert.Equal(1, flyout.AlsoPickedShown);
                Frame(200);

                var press = flyout.ScreenOfCard(0);
                _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y - 12 }));
                var column = ScreenOfList(flyout, 1);
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, column));
                // Not while lines are carried and give way: the ants come back once they rest.
                Assert.Empty(flyout.CopiedShown);
                Frame(250);
                _ = flyout.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
                WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
                _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
                Assert.Equal([0, 1], flyout.CopiedShown);

                flyout.Copied = [];
                Assert.Empty(flyout.CopiedShown);
                var height = frames.Max(frame => frame.Height);
                WindowShots.SaveSideBySide(
                    [.. frames],
                    $"folder-group-{(dark ? "dark" : "light")}.png",
                    dark ? ((byte)0x20, (byte)0x20, (byte)0x20) : ((byte)0xF3, (byte)0xF3, (byte)0xF3));
                flyout.Hide();
                folder.Dispose();
            }
        });
    }
}
