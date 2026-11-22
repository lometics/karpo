// edit: a line carried in an arranged folder moves as the bar's block does (FolderFlyout.Drag.cs;
// the person, 2026-10-12: "the editing of the list views should be made as smooth as the taskbar").
// Held here on real folder windows on a desktop of their own, the dock's half done by hand
// (Folders.Move and Configure, as Dock.PlaceInFolder does):
//
//  - past the threshold the line is lifted and stands exactly where the pointer has it, every
//    move, and the lines under it give way to where the move would put them; a heading, shorter
//    than a row, is passed once the line's bottom is past its middle (the regression of letting go
//    over the middle of a shorter line);
//  - let go, the move is the dock's, and the folder laid out for it settles: nothing is lifted once
//    the settle has run;
//  - a frame sheet of a drag under way, dark and light: folder-drag-motion-<look>.png in KARPO_PNG_DIR.
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

public sealed unsafe class FolderDragMotionTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>A list folder: the clock's line, a heading, the battery's line; and with <paramref name="second"/> a second list of the airplane switch.</summary>
    private static BarItem Folder(bool second = false)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        Put(island, ListKind.Clock.New(), "clock");
        Put(island, ListKind.Heading.New(), "heading");
        Put(island, ListKind.Battery.New(), "battery");
        if (second)
        {
            Put(island, ListKind.AirplaneMode.New(), "airplane", list: 1);
        }
        return island;
    }

    private static void Put(BarItem folder, CellItem widget, string id, int list = 0)
    {
        widget.Id = id;
        widget.View = Karpo.Core.Folders.View(folder.Kind);
        folder.Folder!.Put(widget, list: list);
    }

    /// <summary>The panel point (panel px) of <paramref name="screen"/>.</summary>
    private static Vector2 Panel(FolderFlyout flyout, POINT screen) =>
        new(screen.x - flyout.PanelOnScreen.left, screen.y - flyout.PanelOnScreen.top);

    /// <summary>
    /// Lifted at the press, the clock's line stands where the pointer has it at every move;
    /// carried down until its bottom is a little past the heading's middle — its own middle still
    /// over the heading's upper half — the heading gives way, standing where the clock stood, and
    /// let go there it is a move under the heading. Done by hand as the dock does, the folder laid
    /// out for it settles, and after the settle nothing is lifted.
    /// </summary>
    [UiFact]
    public void ALiftedLineFollowsThePointerAndTheOthersGiveWay()
    {
        WindowShots.OnHiddenDesktop("folder-drag-motion", (gfx, instance) =>
        {
            var item = Folder();
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;
            var (clock, heading) = (plan.Cards[0], plan.Cards[1]);
            Assert.True(heading.bottom - heading.top < clock.bottom - clock.top, "the heading is shorter than a row");
            var press = flyout.ScreenOfCard(0);
            var grab = Panel(flyout, press) - new Vector2(clock.left, clock.top);
            var slop = GetSystemMetrics(SM.SM_CYDRAG);

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            // Lifted at the press, as a block of the bar is (EditLook.cs), where it stood.
            Assert.True(flyout.LiftedLine.Lifted, "a press lifts the line");
            Assert.Equal(new Vector2(clock.left, clock.top), flyout.LiftedLine.At);
            foreach (var down in new[] { slop + 2, slop + 5, slop + 9 })
            {
                var at = press with { y = press.y + down };
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, at));
                Assert.True(flyout.LiftedLine.Lifted);
                Assert.Equal(Panel(flyout, at) - grab, flyout.LiftedLine.At);
            }
            // Nothing has given way yet: the heading is where it stands.
            Assert.Equal(new Vector2(heading.left, heading.top), flyout.LineAt(1));

            // The clock's bottom 3 px past the heading's middle, its own middle well above it.
            var middle = (heading.top + heading.bottom) / 2;
            var drop = press with { y = press.y + (int)MathF.Ceiling(middle + 3 - clock.bottom) };
            Assert.True(Panel(flyout, drop).Y < middle, "the pointer is over the heading's upper half");
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, drop));
            Assert.Equal(new FolderPlace(0, 2), flyout.Aimed);
            // The heading stands where the clock stood; the battery, past both, where it was.
            Assert.Equal(new Vector2(heading.left, clock.top), flyout.LineAt(1));
            Assert.Equal(new Vector2(plan.Cards[2].left, plan.Cards[2].top), flyout.LineAt(2));

            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, drop));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.Equal($"{Owner}/box/clock", placed.Widget?.Key);
            Assert.Equal(new FolderPlace(0, 2), placed.Place);
            Assert.True(flyout.Settling, "let go, the line settles");

            // The dock's half: the move, and the folder handed its new options.
            var cell = item.Folder!.Cells.First(each => each.Island.Id == "clock");
            Assert.True(Karpo.Core.Folders.Move(item.Folder, cell, placed.Place));
            folder.Configure(item, config);
            Assert.Equal(["heading", "clock", "battery"], item.Folder.Lists[0].Select(each => each.Island.Id));
            Assert.True(flyout.Settling, "laid out for the move, the lines glide to their new places");
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
            // Its timer, which the test's window does not hand on.
            _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
            Assert.False(flyout.Settling, "the settle ran its course");
            Assert.False(flyout.LiftedLine.Lifted);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A drag under way, frame by frame, dark and light: lifted, the heading giving way (mid-glide
    /// and done), carried into the other list (mid-glide and done), let go there (settling) and at
    /// rest in its new place. To KARPO_PNG_DIR as folder-drag-motion-&lt;look&gt;.png.
    /// </summary>
    [UiFact]
    public void ADragUnderWayIsDrawnFrameByFrame()
    {
        WindowShots.OnHiddenDesktop("folder-drag-motion-frames", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var item = Folder(second: true);
                var config = new Config();
                var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, dark ? Dark : Light, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
                var frames = new List<(byte[] Canvas, int Width, int Height)>();
                void Frame(int wait)
                {
                    WindowShots.Settle(wait);
                    Assert.True(WindowShots.Save(flyout.Hwnd, "folder-drag-motion-frame.png", seen: (pixels, width) => frames.Add((pixels, width, pixels.Length / 4 / width))));
                }
                var press = flyout.ScreenOfCard(0);
                var slop = GetSystemMetrics(SM.SM_CYDRAG);
                _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 2 }));
                Frame(120);
                // Down over the heading: it gives way.
                var plan = flyout.Plan!;
                var over = press with { y = press.y + (int)MathF.Round(plan.Cards[1].bottom - plan.Cards[0].top) };
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, over));
                Frame(40);
                Frame(250);
                // Across into the second list, over the airplane's upper half.
                var airplane = flyout.ScreenOfCard(3);
                var across = airplane with { y = airplane.y - 6 };
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = (press.x + across.x) / 2, y = (press.y + across.y) / 2 }));
                _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, across));
                Frame(40);
                Frame(250);
                var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, across));
                Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
                var cell = item.Folder!.Cells.First(each => each.Island.Id == "clock");
                Assert.True(Karpo.Core.Folders.Move(item.Folder, cell, placed.Place));
                folder.Configure(item, config);
                Frame(60);
                WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
                _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
                Frame(150);
                var height = frames.Max(frame => frame.Height);
                WindowShots.SaveSideBySide(
                    [.. frames.Select(frame => Padded(frame, height, dark))],
                    $"folder-drag-motion-{(dark ? "dark" : "light")}.png",
                    dark ? ((byte)0x20, (byte)0x20, (byte)0x20) : ((byte)0xF3, (byte)0xF3, (byte)0xF3));
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    /// <summary>A frame as tall as the tallest, its own picture at the bottom, where the bar is: the window may grow as the move lands.</summary>
    private static (byte[] Canvas, int Width, int Height) Padded((byte[] Canvas, int Width, int Height) frame, int height, bool dark)
    {
        if (frame.Height == height)
        {
            return frame;
        }
        var canvas = new byte[frame.Width * height * 4];
        var shade = dark ? (byte)0x20 : (byte)0xF3;
        for (var at = 0; at < canvas.Length; at += 4)
        {
            (canvas[at], canvas[at + 1], canvas[at + 2], canvas[at + 3]) = (shade, shade, shade, 255);
        }
        Buffer.BlockCopy(frame.Canvas, 0, canvas, (height - frame.Height) * frame.Width * 4, frame.Canvas.Length);
        return (canvas, frame.Width, height);
    }
}
