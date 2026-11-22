// edit: a line carried out of its column stays out of its height (§230; the person, 2026-10-19:
// "when i drag a folder widget away from a column into another column the widgets move to the top
// although they should stay at the bottom because they are aligned to the bottom, the container so
// to speak does not change the size properly"). Over no place of the folder — an empty slot beside
// the lists, or off them — the carried line was put at its list's end for the lines to stand by, and
// still took its room there: on a bottom or side bar the lines under it rose to the top of the
// column, its height kept, the gap left on the bar's side. Now it takes no room anywhere: the lines
// it leaves close up on the bar's side and its list's panel shrinks, on every edge; let go in the
// slot, every column stands flush with the bar's side of a panel as tall as the tallest.
//
// Pictures (KARPO_PNG_DIR): folder-column-flush-<edge>.png — at rest, carried over a slot, let go.
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

public sealed unsafe class FolderColumnFlushTests
{
    private const string Owner = "layout:default";
    private const float Scale = 1.5f;
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    /// <summary>"box": the clock's line, a heading and the battery's line; the airplane switch in a second list.</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        Put(island, ListKind.Clock.New(), "clock", 0);
        Put(island, ListKind.Heading.New(), "heading", 0);
        Put(island, ListKind.Battery.New(), "battery", 0);
        Put(island, ListKind.AirplaneMode.New(), "airplane", 1);
        return island;
    }

    private static void Put(BarItem folder, CellItem widget, string id, int list)
    {
        widget.Id = id;
        widget.View = Karpo.Core.Folders.View(folder.Kind);
        folder.Folder!.Put(widget, list: list);
    }

    /// <summary>
    /// The clock's line carried from the tall list over an empty slot: the heading and the battery
    /// close up on the bar's side — on a top bar from the top, on a bottom bar to the bottom, the
    /// battery where it stood, beside a side bar to their middle (§303) — and the list's panel is as
    /// tall as the two; let go there, the clock is a list of its own and every list stands flush on
    /// the bar's side of the panel, or centred on its middle beside a side bar.
    /// </summary>
    [UiTheory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ALineCarriedOutOfItsColumnLeavesItFlushOnTheBarsSide(Edge edge)
    {
        WindowShots.OnHiddenDesktop("folder-column-flush", (gfx, instance) =>
        {
            var (anchor, work) = edge switch
            {
                Edge.Top => (new POINT { x = 960, y = 48 }, new RECT { top = 48, right = 1920, bottom = 1080 }),
                Edge.Left => (new POINT { x = 48, y = 540 }, new RECT { left = 48, right = 1920, bottom = 1080 }),
                Edge.Right => (new POINT { x = 1872, y = 540 }, new RECT { right = 1872, bottom = 1080 }),
                _ => (new POINT { x = 960, y = 1032 }, new RECT { right = 1920, bottom = 1032 }),
            };
            var fromTop = edge == Edge.Top;
            var centred = edge is Edge.Left or Edge.Right;
            var item = Folder();
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", item, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, Scale, anchor, work, edge, folder, editing: true);
            var rest = flyout.Plan!;
            var (clock, heading, battery) = (rest.Cards[0], rest.Cards[1], rest.Cards[2]);
            var gap = heading.top - clock.bottom;
            var tall = (heading.bottom - heading.top) + gap + (battery.bottom - battery.top);
            Assert.True(rest.Lists[0].Height > rest.Lists[1].Height, "the clock's list is the taller");
            var frames = new List<(byte[] Canvas, int Width, int Height)>();
            // One frame for all three, held on the bar's side of the panel at rest.
            var seen = flyout.PanelOnScreen;
            var (frameWidth, frameHeight) = ((int)MathF.Round(rest.Width + rest.Lists[1].Width) + 120, (int)MathF.Round(rest.Height) + 120);
            void Frame()
            {
                WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 80);
                RECT window;
                window = WindowShots.Extent(flyout.Hwnd);
                var left = flyout.PanelOnScreen.left - window.left - 60;
                var top = fromTop ? seen.top - window.top - 20 : seen.bottom - window.top + 20 - frameHeight;
                var part = new RECT { left = left, top = top, right = left + frameWidth, bottom = top + frameHeight };
                _ = WindowShots.Save(flyout.Hwnd, "folder-column-flush-frame.png", part, (pixels, width) => frames.Add((pixels, width, pixels.Length / 4 / width)));
            }
            Frame();

            // Carried onto the slot right of the lists, level with them (left of them on a right bar).
            var slot = rest.Slots.Any(each => each.Box.left >= rest.Width) ? rest.Slots.First(each => each.Box.left >= rest.Width) : rest.Slots.MaxBy(each => each.Box.left);
            var panel = flyout.PanelOnScreen;
            var over = new POINT
            {
                x = panel.left + (int)MathF.Round((slot.Box.left + slot.Box.right) / 2),
                y = panel.top + (int)MathF.Round(rest.Height / 2),
            };
            var press = flyout.ScreenOfCard(0);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + 40 }));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, over));
            Assert.Null(flyout.Aimed);
            Assert.True(flyout.LiftedLine.Lifted);

            // The two left stand together on the bar's side: on a bottom bar where they stood, the
            // clock's room over them gone (they rose to the column's top, under it, before); on a top
            // bar up where the clock stood; beside a side bar half way, the column's middle where it was.
            var headingAt = flyout.LineAt(1);
            var batteryAt = flyout.LineAt(2);
            Assert.Equal(heading.left, headingAt.X);
            Assert.Equal(headingAt.Y + (heading.bottom - heading.top) + gap, batteryAt.Y, 0.5f);
            Assert.Equal(fromTop ? clock.top : centred ? clock.top + ((heading.top - clock.top) / 2) : heading.top, headingAt.Y, 1f);
            // The list is as tall as the two, the carried line in none: the panel follows.
            var arranged = flyout.ArrangedPlan!;
            Assert.Equal(tall, arranged.Lists[0].Height, 0.5f);
            Assert.Equal(MathF.Max(tall, rest.Lists[1].Height), arranged.Height, 0.5f);
            Frame();

            // Let go: the clock is a list of its own in the slot; the dock's half done by hand.
            var placed = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, over));
            Assert.Equal(FolderFlyoutAction.Place, placed.Kind);
            Assert.True(placed.Place.New);
            var cell = item.Folder!.Cells.First(each => each.Island.Id == "clock");
            Assert.True(Karpo.Core.Folders.Move(item.Folder, cell, placed.Place));
            folder.Configure(item, config);
            WindowShots.Settle((int)Motion.NormalMs(MotionKind.Move) + 50);
            _ = flyout.Message(WM.WM_TIMER, FolderFlyout.SettleTimer, 0);
            Assert.False(flyout.Settling);
            var after = flyout.Plan!;
            var filled = Enumerable.Range(0, after.Lists.Count).Where(list => after.CardList.Contains(list)).ToList();
            Assert.Equal(3, filled.Count);
            Assert.Equal(filled.Max(list => after.Lists[list].Height), after.Height, 0.5f);
            for (var index = 0; index < after.Cards.Count; index++)
            {
                Assert.Equal(new Vector2(after.Cards[index].left, after.Cards[index].top), flyout.LineAt(index));
            }
            // Every list on the bar's side of the panel, or on its middle beside a side bar, the panel as tall as the tallest.
            foreach (var list in filled)
            {
                var box = after.Lists[list].Box;
                if (centred)
                {
                    Assert.Equal(after.Height / 2, (box.top + box.bottom) / 2, 0.5f);
                }
                else
                {
                    Assert.Equal(fromTop ? 0 : after.Height, fromTop ? box.top : box.bottom, 0.5f);
                }
            }
            Assert.Equal(after.Height, flyout.PanelOnScreen.bottom - flyout.PanelOnScreen.top, 1f);
            Frame();
            WindowShots.SaveSideBySide(frames, $"folder-column-flush-{edge.ToString().ToLowerInvariant()}.png", (0x20, 0x20, 0x20));
            flyout.Hide();
            folder.Dispose();
        });
    }
}
