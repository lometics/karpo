// edit: in a folder standing open while its bar is arranged, a click on a line opens that
// widget's own page in Karpo's settings, as a click on a widget on the bar does (the person,
// 2026-10-04: "when clicking on a widget in edit mode it should open its settings";
// Karpo.Dock\EditClicks.cs). The folder says so (FolderFlyoutAction.Settings, with the widget and
// where it was let go) and stays open; the dock opens the page.
//
// Held here on real folder windows on a desktop of their own:
//
//  - a press and release inside the drag threshold is the click on a line;
//  - a line carried past the threshold is being moved, never clicked — even let go back where it
//    was pressed — and carried past another line it goes there;
//  - the folder's "+" keeps its own click, which opens the add list;
//  - Enter on the line with the keyboard's focus is the click, once however long it is held;
//  - at rest a click on a line is still the widget's own (and here, the clock's, which does nothing);
//  - a folder opened by a click on the bar while it is arranged stays open, and arranged, while the
//    settings window that click opened takes the focus; at rest the same loss of focus closes it.
//  - the widget clicked wears the selected look, the accent's ring round its line, while
//    the folder is arranged, until another is clicked; at rest none does.
//
// The bar's half, and the rules themselves: Edit\EditClickTests. Needs Direct2D and a window
// station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderEditClickTests
{
    private const string Owner = "layout:default";

    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly Look Light = new(Glass: false, Dark: false, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };
    private static readonly RECT Work = new() { right = 1920, bottom = 1040 };

    /// <summary>
    /// A press and release on a line, inside the drag threshold, opens its settings; the folder stays
    /// open. At rest a click on a line is the widget's own, never its settings.
    /// </summary>
    [UiFact]
    public void AClickOnALineOpensItsSettings()
    {
        WindowShots.OnHiddenDesktop("folder-edit-click", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            // The clock's line.
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CXDRAG);

            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press)).Kind);
            // A wobble of the hand, up to the threshold and no further.
            var wobble = press with { x = press.x + slop };
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, wobble)).Kind);
            var clicked = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, wobble));

            Assert.Equal(FolderFlyoutAction.Settings, clicked.Kind);
            Assert.Equal($"{Owner}/box/clock", clicked.Widget?.Key);
            // Where it was let go, for the dock's double-click guard.
            Assert.Equal(wobble.x, clicked.At.x);
            Assert.Equal(wobble.y, clicked.At.y);
            // And its line on the screen, which the menu its click opens stands beside (§212).
            Assert.True(clicked.Field.left <= press.x && press.x < clicked.Field.right && clicked.Field.top <= press.y && press.y < clicked.Field.bottom,
                $"the line's card {clicked.Field.left},{clicked.Field.top}..{clicked.Field.right},{clicked.Field.bottom} is not round the press {press.x},{press.y}");
            Assert.True(flyout.IsOpen, "the folder closed on a click that opens the settings");

            // Another line, another page.
            var battery = Client(flyout.Hwnd, flyout.ScreenOfCard(1));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, battery);
            var other = flyout.Message(WM.WM_LBUTTONUP, 0, battery);
            Assert.Equal(FolderFlyoutAction.Settings, other.Kind);
            Assert.Equal($"{Owner}/box/battery", other.Widget?.Key);
            Assert.True(flyout.IsOpen);
            flyout.Hide();

            // At rest a click on a line is the widget's own, never its settings.
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var resting = Client(flyout.Hwnd, flyout.ScreenOfCard(0));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, resting);
            Assert.NotEqual(FolderFlyoutAction.Settings, flyout.Message(WM.WM_LBUTTONUP, 0, resting).Kind);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A line carried past the drag threshold is being moved: let go back where it was pressed it
    /// is no click, and carried past another line it goes there.
    /// </summary>
    [UiFact]
    public void ALineDraggedIsMovedNotClicked()
    {
        WindowShots.OnHiddenDesktop("folder-edit-drag", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CXDRAG);

            // Out past the threshold, still on its own line, and back to the press.
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { x = press.x + slop + 2 }));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, press)).Kind);

            // Let go past the threshold on its own line: no click either.
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            var past = press with { x = press.x + slop + 2 };
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, past));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, past)).Kind);

            // Carried onto the lower half of the battery's line: it goes under it. (Its upper half is
            // above it — where the clock already is, which moves nothing.)
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            var other = flyout.ScreenOfCard(1);
            other.y += 10;
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, other));
            var moved = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, other));
            Assert.Equal(FolderFlyoutAction.Place, moved.Kind);
            Assert.Equal($"{Owner}/box/clock", moved.Widget?.Key);
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The arranged folder reads the bar's keymap (EditKeys): the menu key and Shift+F10's menu at the
    /// line, Delete on it, End to the last; Escape puts a line being moved back and the folder stays,
    /// as a block of the bar goes back; a right press does the same and its release opens no menu;
    /// a right click on the room is the room's menu; Escape with nothing held closes the folder.
    /// </summary>
    [UiFact]
    public void TheFolderReadsTheBarsKeymap()
    {
        WindowShots.OnHiddenDesktop("folder-edit-keymap", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var press = flyout.ScreenOfCard(0);
            var slop = GetSystemMetrics(SM.SM_CXDRAG);

            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0));
            var menu = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_APPS, 0);
            Assert.Equal(FolderFlyoutAction.Context, menu.Kind);
            Assert.Equal($"{Owner}/box/clock", menu.Widget?.Key);
            Assert.NotEqual(default, menu.At);
            flyout.MenuClosed();
            var removed = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DELETE, 0);
            Assert.Equal((FolderFlyoutAction.Selection, EditKeyAct.Remove), (removed.Kind, removed.Act));
            Assert.Equal($"{Owner}/box/clock", removed.Widget?.Key);
            // Held down, Delete removes nothing more.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DELETE, (LPARAM)(nint)(1 << 30)).Kind);
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_END, 0));

            // A line carried past the threshold: Escape puts it back, and the folder stays open.
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            var other = flyout.ScreenOfCard(1);
            other.y += 10;
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, other));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0).Kind);
            Assert.True(flyout.IsOpen, "Escape closed the folder instead of putting the line back");
            // Its release, after, places nothing.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, other)).Kind);

            // A right press does the same, and its release opens no menu.
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, press));
            _ = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, press with { y = press.y + slop + 12 }));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_RBUTTONDOWN, 2, Client(flyout.Hwnd, other)).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_RBUTTONUP, 0, Client(flyout.Hwnd, other)).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, other)).Kind);
            Assert.True(flyout.IsOpen);

            // Nothing held: Escape is one level out, the folder closes.
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0).Kind);
            folder.Dispose();
        });
    }

    /// <summary>
    /// The person, 2026-10-19: "add back the add button per column" (§246). Arranged, a list's buttons
    /// are its "+" and its width, each keeping its own click (the add list, the widths menu, never a
    /// widget's settings).
    /// </summary>
    [UiFact]
    public void TheColumnsHaveTheirPlus()
    {
        WindowShots.OnHiddenDesktop("folder-edit-plus", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1.5f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var plan = flyout.Plan!;

            Assert.Equal(plan.Lists.Count, plan.Adds.Count(add => add.Kind == FolderAddKind.Width && add.List >= 0));
            Assert.Equal(plan.Lists.Count, plan.Adds.Count(add => add.Kind == FolderAddKind.Add && add.List >= 0));
            foreach (var (index, wanted) in new[] { (0, FolderFlyoutAction.Add), (1, FolderFlyoutAction.Width) })
            {
                var button = Client(flyout.Hwnd, flyout.ScreenOfAdd(index));
                _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, button);
                Assert.Equal(wanted, flyout.Message(WM.WM_LBUTTONUP, 0, button).Kind);
                flyout.MenuClosed();
                Assert.True(flyout.IsOpen);
            }
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The person, 2026-10-22: "remove the close button in folder edit mode" (§287). Arranged, nothing
    /// stands past the columns' "+" row: where the close button stood (centred on the lists with
    /// widgets, 68 DIPs off them) the pointer finds no part — no tip comes, and a click there asks
    /// nothing of the dock and leaves the folder open. Its face on the bar, Escape and a right click
    /// on a widget of the bar close it (EditClickTests).
    /// </summary>
    [UiFact]
    public void TheArrangedFolderHasNoCloseButton()
    {
        WindowShots.OnHiddenDesktop("folder-edit-no-close", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1.5f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var panel = flyout.PanelOnScreen;
            var core = flyout.Plan!.Core;
            var stood = new POINT { x = panel.left + (int)((core.left + core.right) / 2), y = panel.top - 78 };
            // The "+" row is the farthest thing off the lists.
            for (var index = 0; flyout.ScreenOfAdd(index) is { x: not 0 } plus; index++)
            {
                Assert.True(plus.y > stood.y + 24, $"column button {index} stands where the close button did");
            }
            var on = Client(flyout.Hwnd, stood);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 0, on).Kind);
            _ = flyout.Message(WM.WM_TIMER, 2, 0);
            Assert.Null(flyout.TipShown);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, on);
            var up = flyout.Message(WM.WM_LBUTTONUP, 0, on).Kind;
            Assert.True(up is FolderFlyoutAction.None or FolderFlyoutAction.BandReleased, $"{up}");
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The person, 2026-10-19: "the separate right click menus in the edit mode should be removed"
    /// (§214). Arranged, a right click on a line asks for what its click does — its card on the
    /// screen, which the one menu stands beside, as a left click hands it (FolderFlyoutAction.Settings)
    /// — and the folder holds itself open for it. At rest it is the line's own menu at the pointer,
    /// no card.
    /// </summary>
    [UiFact]
    public void ARightClickOnALineIsItsClick()
    {
        WindowShots.OnHiddenDesktop("folder-edit-right", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var line = Client(flyout.Hwnd, flyout.ScreenOfCard(0));

            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, line);
            var clicked = flyout.Message(WM.WM_LBUTTONUP, 0, line);
            Assert.Equal(FolderFlyoutAction.Settings, clicked.Kind);
            Assert.True(clicked.Field.right > clicked.Field.left);

            var right = flyout.Message(WM.WM_RBUTTONUP, 0, line);
            Assert.Equal(FolderFlyoutAction.Context, right.Kind);
            Assert.Same(clicked.Widget, right.Widget);
            Assert.Equal(clicked.Field, right.Field);
            Assert.True(flyout.StaysOpen);
            flyout.MenuClosed();
            flyout.Hide();

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var resting = flyout.Message(WM.WM_RBUTTONUP, 0, Client(flyout.Hwnd, flyout.ScreenOfCard(0)));
            Assert.Equal(FolderFlyoutAction.Context, resting.Kind);
            Assert.Equal(default, resting.Field);
            flyout.MenuClosed();
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A press on the arranged folder's empty room, between and round its lines, begins the dock's
    /// selection rectangle and holds the mouse to its release (2026-10-12: a lasso inside, as on the
    /// bar); a press on a line still carries it. What the rectangle runs over is the lines it overlaps,
    /// and every selected line wears the ring, not only the one last clicked. At rest the empty room
    /// begins nothing.
    /// </summary>
    [UiFact]
    public void ADragOnTheEmptyRoomSelectsLines()
    {
        WindowShots.OnHiddenDesktop("folder-edit-band", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", TwoLists(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            var empty = EmptyOf(flyout);

            var pressed = flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, empty));
            Assert.Equal(FolderFlyoutAction.BandPressed, pressed.Kind);
            Assert.Equal(empty, pressed.At);
            Assert.Equal(flyout.Hwnd, GetCapture());
            var further = empty with { x = empty.x + 40, y = empty.y - 30 };
            var moved = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(flyout.Hwnd, further));
            Assert.Equal(FolderFlyoutAction.BandMoved, moved.Kind);
            Assert.Equal(further, moved.At);
            Assert.Equal(FolderFlyoutAction.BandReleased, flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, further)).Kind);
            Assert.NotEqual(flyout.Hwnd, GetCapture());
            Assert.True(flyout.IsOpen);

            // A right click there opens nothing: the room has no menu of its own any more (§214).
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_RBUTTONUP, 0, Client(flyout.Hwnd, empty)).Kind);

            // A line's press is still its carry, never the rectangle.
            var line = Client(flyout.Hwnd, flyout.ScreenOfCard(0));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONDOWN, 1, line).Kind);
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, line);

            // What a rectangle runs over: the clock's line alone, both, none.
            var clock = flyout.ScreenOfCard(0);
            var battery = flyout.ScreenOfCard(1);
            Assert.Equal([$"{Owner}/box/clock"], flyout.KeysIn(new RECT { left = clock.x - 2, top = clock.y - 2, right = clock.x + 2, bottom = clock.y + 2 }));
            Assert.Equal([$"{Owner}/box/clock", $"{Owner}/box/battery"], flyout.KeysIn(new RECT
            {
                left = Math.Min(clock.x, battery.x) - 2,
                top = Math.Min(clock.y, battery.y) - 2,
                right = Math.Max(clock.x, battery.x) + 2,
                bottom = Math.Max(clock.y, battery.y) + 2,
            }));
            Assert.Empty(flyout.KeysIn(new RECT { left = empty.x, top = empty.y, right = empty.x + 1, bottom = empty.y + 1 }));

            // Both selected: both ringed.
            flyout.Picked = $"{Owner}/box/clock";
            flyout.AlsoPicked = [$"{Owner}/box/battery"];
            Assert.True(flyout.PickedShown);
            Assert.Equal(1, flyout.AlsoPickedShown);
            flyout.AlsoPicked = [];
            Assert.Equal(0, flyout.AlsoPickedShown);
            flyout.Hide();

            // At rest the empty room begins nothing.
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            var resting = EmptyOf(flyout);
            Assert.NotEqual(FolderFlyoutAction.BandPressed, flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, resting)).Kind);
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, Client(flyout.Hwnd, resting));
            flyout.Hide();
            folder.Dispose();
        });

        // A point of the folder's room beside its shorter list's one line, over or under it: on the
        // panel, on no line.
        static POINT EmptyOf(FolderFlyout flyout)
        {
            var plan = flyout.Plan!;
            var first = plan.Cards[0];
            var middle = flyout.ScreenOfCard(0);
            var dx = middle.x - ((first.left + first.right) / 2);
            var dy = middle.y - ((first.top + first.bottom) / 2);
            var lone = plan.Cards[plan.CardList.ToList().IndexOf(1)];
            var x = lone.left + ((lone.right - lone.left) / 8);
            static bool In(TerraFX.Interop.DirectX.D2D_RECT_F box, float x, float y) => x >= box.left - 2 && x < box.right + 2 && y >= box.top - 2 && y < box.bottom + 2;
            foreach (var y in new[] { lone.top / 2, (lone.bottom + plan.Height) / 2 })
            {
                if (y > 1 && y < plan.Height - 1 && !plan.Cards.Any(card => In(card, x, y)) && !plan.Adds.Any(add => In(add.Box, x, y)))
                {
                    return new POINT { x = (int)MathF.Round(x + dx), y = (int)MathF.Round(y + dy) };
                }
            }
            Assert.Fail($"the folder has no empty room to press on: {plan.Width}x{plan.Height}, {string.Join("; ", plan.Cards.Select(card => $"{card.left},{card.top},{card.right},{card.bottom}"))}");
            return default;
        }
    }

    /// <summary>A list folder of two lists: the clock's line and the battery's, and beside them the brightness's alone.</summary>
    private static BarItem TwoLists()
    {
        var island = List();
        var brightness = ListKind.Brightness.New();
        brightness.Id = "brightness";
        brightness.View = Karpo.Core.Folders.View(island.Kind);
        island.Folder!.Lists.Add([new FolderCell { Island = brightness }]);
        return island;
    }

    /// <summary>
    /// Enter on the line with the keyboard's focus is the click while arranged — once, however long
    /// it is held — and at rest is still the widget's own click (the clock's, which does nothing in a
    /// folder).
    /// </summary>
    [UiFact]
    public void EnterOnALineOpensItsSettings()
    {
        WindowShots.OnHiddenDesktop("folder-edit-enter", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);

            // Home: the first line in reading order, the clock.
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0));
            var entered = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RETURN, 0);
            Assert.Equal(FolderFlyoutAction.Settings, entered.Kind);
            Assert.Equal($"{Owner}/box/clock", entered.Widget?.Key);
            // The keys carry no point: no double-click to tell apart.
            Assert.Equal(default, entered.At);
            // Held down, the repeats (lParam bit 30) open nothing more.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RETURN, (LPARAM)(nint)(1 << 30)).Kind);
            // Space is a click, as on the bar (EditKeys): the line selected, nothing in front.
            var spaced = flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_SPACE, 0);
            Assert.Equal(FolderFlyoutAction.Select, spaced.Kind);
            Assert.Equal($"{Owner}/box/clock", spaced.Widget?.Key);
            Assert.True(flyout.IsOpen);
            flyout.Hide();

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            Assert.Equal(default, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_HOME, 0));
            Assert.NotEqual(FolderFlyoutAction.Settings, flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RETURN, 0).Kind);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A click on a folder on the bar being arranged opens the folder and then its page, and the
    /// settings window takes the focus (EditClicks.OnBlock). The folder, shown arranged, stays open
    /// through that — the focus going to another window, and the presses in it, which are outside
    /// the folder — and is still arranged after: a click on a line is still the click that opens
    /// its page, not the widget's own. At rest the same loss of focus closes it, as light dismiss does.
    /// </summary>
    [UiFact]
    public void AFolderBeingArrangedSurvivesTheSettingsWindowTakingTheFocus()
    {
        WindowShots.OnHiddenDesktop("folder-edit-settings-focus", (gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, editing: true);
            Assert.True(flyout.StaysOpen);

            // The settings window comes to the front: the folder hears it lose the activation (to a
            // window of another process, so lParam names none of ours), and the frame go inactive.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_ACTIVATE, (WPARAM)WA_INACTIVE, default).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_NCACTIVATE, 0, default).Kind);
            Assert.True(flyout.IsOpen, "the folder closed when the settings window took the focus");
            // A press in the settings window, outside the folder: the hook tells the folder, which stays.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)Generation(flyout), default).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_CANCELMODE, 0, default).Kind);
            Assert.True(flyout.IsOpen, "the folder closed for a press in the settings window");
            Assert.True(flyout.StaysOpen);

            // Still arranged: a click on a line opens its page.
            var press = Client(flyout.Hwnd, flyout.ScreenOfCard(0));
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, press);
            var clicked = flyout.Message(WM.WM_LBUTTONUP, 0, press);
            Assert.Equal(FolderFlyoutAction.Settings, clicked.Kind);
            Assert.Equal($"{Owner}/box/clock", clicked.Widget?.Key);
            Assert.True(flyout.IsOpen);
            flyout.Hide();

            // At rest the same loss of the focus is a light dismiss.
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder);
            Assert.False(flyout.StaysOpen);
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_ACTIVATE, (WPARAM)WA_INACTIVE, default).Kind);
            Assert.False(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The widget clicked is the selected one ("it should also show the widget as selected"): while
    /// the folder is arranged its line wears the accent's ring, the dock having said which
    /// (FolderFlyout.Picked, Dock.EditPick); another clicked takes the ring; none, or a folder at
    /// rest, wears none. Pictures of a list with the ring, at 100 % and 150 %, dark and light, to
    /// KARPO_PNG_DIR as folder-edit-selected-list-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void TheWidgetClickedWearsTheSelectedRing()
    {
        WindowShots.OnHiddenDesktop("folder-edit-selected", (gfx, instance) =>
        {
            foreach (var (view, island) in new[] { ("list", List()) })
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var look = dark ? Dark : Light;
                        var folder = new FolderIsland($"{Owner}/box", island, gfx, new Config());
                        using var flyout = new FolderFlyout(gfx, instance, 0);
                        flyout.Picked = $"{Owner}/box/clock";
                        flyout.Show(gfx, look, scale, Anchor, Work, Edge.Bottom, folder, editing: true);
                        Assert.True(flyout.PickedShown, $"the clicked {view} widget wears no ring");
                        WindowShots.Settle(250);
                        var file = $"folder-edit-selected-{view}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");

                        // Another widget clicked takes the ring; none clicked, none wears it.
                        flyout.Picked = $"{Owner}/box/battery";
                        Assert.True(flyout.PickedShown);
                        flyout.Picked = $"{Owner}/elsewhere";
                        Assert.False(flyout.PickedShown, "a widget of no folder of this one is ringed in it");
                        flyout.Picked = null;
                        Assert.False(flyout.PickedShown);
                        flyout.Hide();

                        // At rest nothing is selected, whatever the dock last said.
                        flyout.Picked = $"{Owner}/box/clock";
                        flyout.Show(gfx, look, scale, Anchor, Work, Edge.Bottom, folder);
                        Assert.False(flyout.PickedShown, "a folder at rest shows a selection");
                        flyout.Hide();
                        folder.Dispose();
                    }
                }
            }
        });
    }

    /// <summary>
    /// The selected ring is whole wherever the line stands ("often clipped by the edge, clipped by
    /// other widgets, behind other stuff", 2026-10-11): the first line and the last of a list, a
    /// line of one row and one of two, a list of a folder of several panels, at 100 %, 150 % and
    /// 200 %, dark and light. All four sides of the ring are the accent at their middles, and the
    /// ring lies inside the window. Pictures to KARPO_PNG_DIR as folder-edit-ring-&lt;line&gt;-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void TheSelectedRingIsWholeWhereverTheLineStands()
    {
        WindowShots.OnHiddenDesktop("folder-edit-ring", (gfx, instance) =>
        {
            var twoLists = List();
            var airplane = ListKind.AirplaneMode.New();
            airplane.Id = "airplane";
            airplane.View = FolderView.List;
            twoLists.Folder!.Put(airplane, list: 1);
            foreach (var key in new[] { "clock", "battery", "airplane" })
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var scale in new[] { 1f, 1.5f, 2f })
                    {
                        var look = dark ? Dark : Light;
                        var folder = new FolderIsland($"{Owner}/box", twoLists, gfx, new Config());
                        using var flyout = new FolderFlyout(gfx, instance, 0);
                        flyout.Picked = $"{Owner}/box/{key}";
                        flyout.Show(gfx, look, scale, Anchor, Work, Edge.Bottom, folder, editing: true);
                        Assert.True(flyout.PickedShown, $"{key} wears no ring");
                        WindowShots.Settle(250);
                        var ring = flyout.PickedRing;
                        RECT window;
                        window = WindowShots.Extent(flyout.Hwnd);
                        var (width, height) = (window.right - window.left, window.bottom - window.top);
                        Assert.True(ring.left >= 0 && ring.top >= 0 && ring.right <= width && ring.bottom <= height, $"{key}'s ring {ring.left},{ring.top}-{ring.right},{ring.bottom} leaves the {width}x{height} window");
                        var file = $"folder-edit-ring-{key}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        byte[] pixels = [];
                        var stride = 0;
                        Assert.True(WindowShots.Save(flyout.Hwnd, file, seen: (seen, w) => (pixels, stride) = (seen, w)), $"{file} came back blank");
                        // The stroke is 2 hairlines wide at the box's edge: some pixel of that band, a
                        // third of the way along each side (clear of a "+" standing on the middle of
                        // a side, which is over it), is the accent itself.
                        var band = 2 * Math.Max(1, (int)MathF.Round(scale));
                        var (alongX, alongY) = (ring.left + ((ring.right - ring.left) / 3), ring.top + ((ring.bottom - ring.top) / 4));
                        bool Accent(int x, int y)
                        {
                            var at = ((y * stride) + x) * 4;
                            return Math.Abs(pixels[at + 2] - 0x00) < 24 && Math.Abs(pixels[at + 1] - 0x78) < 24 && Math.Abs(pixels[at] - 0xD4) < 24;
                        }
                        foreach (var (side, along) in new (string, Func<int, (int X, int Y)>)[]
                        {
                            ("top", step => (alongX, ring.top + step)),
                            ("bottom", step => (alongX, ring.bottom - 1 - step)),
                            ("left", step => (ring.left + step, alongY)),
                            ("right", step => (ring.right - 1 - step, alongY)),
                        })
                        {
                            Assert.True(Enumerable.Range(-1, band + 2).Any(step => Accent(along(step).X, along(step).Y)), $"{file}: the ring's {side} side at {along(0).X},{along(0).Y} is not the accent");
                        }
                        flyout.Hide();
                        folder.Dispose();
                    }
                }
            }
        });
    }

    /// <summary>
    /// An arranged folder's lines wear no outline, only the selected one its ring (the person,
    /// 2026-10-23: "in folder edit mode dont show the lines around a widget, only when selected"):
    /// the unselected battery's line has nothing at its edges but what is inside it, the clock's has
    /// the accent's ring. At 150 %, dark and light: folder-edit-unselected-&lt;look&gt;.png.
    /// </summary>
    [UiFact]
    public void AnArrangedFoldersLinesWearNoOutlineButTheSelectedRing()
    {
        WindowShots.OnHiddenDesktop("folder-edit-unselected", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                // Where the battery's line stands: its ring is on its edge.
                flyout.Picked = $"{Owner}/box/battery";
                flyout.Show(gfx, dark ? Dark : Light, 1.5f, Anchor, Work, Edge.Bottom, folder, editing: true);
                WindowShots.Settle(250);
                var line = flyout.PickedRing;
                flyout.Picked = $"{Owner}/box/clock";
                Assert.True(flyout.PickedShown);
                var ring = flyout.PickedRing;
                WindowShots.Settle(250);
                var file = $"folder-edit-unselected-{(dark ? "dark" : "light")}.png";
                byte[] pixels = [];
                var stride = 0;
                Assert.True(WindowShots.Save(flyout.Hwnd, file, seen: (seen, w) => (pixels, stride) = (seen, w)), $"{file} came back blank");
                int Apart(int x, int y, int x2, int y2)
                {
                    var (one, other) = ((((y * stride) + x) * 4), (((y2 * stride) + x2) * 4));
                    return Enumerable.Range(0, 3).Sum(channel => Math.Abs(pixels[one + channel] - pixels[other + channel]));
                }
                // The battery's top edge, where it meets the clock's line, as the line a few px in: nothing
                // drawn on it. (Its other sides are the panel's, whose rim is there.) Its first px is past it: the
                // clock's ring stands edge to edge with it (§410), and its soft edge may reach that far.
                var alongX = line.left + ((line.right - line.left) / 3);
                foreach (var step in new[] { 1, 2 })
                {
                    Assert.True(Apart(alongX, line.top + step, alongX, line.top + 5) < 8, $"{file}: the unselected line has an outline along its top");
                }
                // The clock's ring: its top side unlike the line inside it.
                var ringX = ring.left + ((ring.right - ring.left) / 3);
                Assert.True(Apart(ringX, ring.top, ringX, ring.top + 6) > 60, $"{file}: the selected line wears no ring");
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    private static uint Generation(FolderFlyout folder) => FlyoutHost.OpenMenus.Single(host => host.Hwnd == folder.Hwnd).Generation;

    internal static LPARAM Client(HWND window, POINT screen)
    {
        var at = screen;
        Assert.True(ScreenToClient(window, &at));
        return (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF));
    }

    /// <summary>A list folder with a clock's line and the battery's.</summary>
    internal static BarItem List()
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        Put(island, ListKind.Clock.New(), "clock");
        Put(island, ListKind.Battery.New(), "battery");
        return island;
    }

    private static void Put(BarItem folder, CellItem widget, string id)
    {
        widget.Id = id;
        widget.View = Karpo.Core.Folders.View(folder.Kind);
        folder.Folder!.Put(widget);
    }
}
