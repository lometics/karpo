// The bar's newer islands side by side (IslandPictures); and the tray's overflow
// flyout taking a right click on a hidden icon (OverflowRightClick), and telling
// the pointer and the left button over its icons
// (OverflowTellsThePointerAndTheButtons). None ever puts a window on the
// person's own desktop. The notification list that was here is a line of the
// notifications preset now (Folders\NotificationsPresetTests.cs).
//
// Both need a window station and a GPU-less Direct2D, so they only run when
// KARPO_UI_TESTS=1; the pictures go to KARPO_PNG_DIR (a temporary folder
// otherwise).
//
// ---------------------------------------------------------------------------
// Why every popup here has a window class of its own, and why Register checks
// which procedure is in force. Do not undo either: this was measured, not
// feared.
//
// A window class is registered once per process by whoever gets there first,
// and RegisterClassW returns 0 in silence when the name is already taken. The
// windows then answer with the *first* registrant's procedure. DismissOffscreen
// Tests used to register Flyout.ClassName — this file's name too — with a
// procedure that is nothing but DefWindowProc, and it unregistered it in a
// finally that assumed serial execution, while the folders' and the calendar's
// offscreen tests hold windows of that class in parallel and UnregisterClass
// fails whenever one exists.
//
// Two things came of that, both real:
//
//   * The clock panel's tick test (the clock flyout is gone) was the suite's one unsteady test, and it looked
//     like a timing race. It was not: its messages were not late, they were
//     being dropped. Forcing the class onto a dead-end procedure fails it every
//     time; giving the clock panel a name of its own passes every time.
//   * FlyoutRuns passed while testing nothing. Counted: 3375 messages reach the
//     calendar and the notification list on a clean run, 0 with the class
//     hijacked — and the test is green either way. Every pointer sweep, click,
//     wheel turn and key in it was a no-op on the runs where the race went the
//     wrong way, and it said so to nobody.
//
// So a shared class name is not forbidden — several tests here can share one
// procedure safely, and the folders' and calendar's tests never route messages
// through a window at all. What is forbidden is a name held by somebody else's
// procedure, and Register asserts exactly that, at the moment of registering.
// ---------------------------------------------------------------------------

using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

/// <summary>A fact that runs only with KARPO_UI_TESTS=1: it needs Direct2D, composition and a hidden desktop.</summary>
public sealed class UiFactAttribute : FactAttribute
{
    public UiFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KARPO_UI_TESTS") != "1")
        {
            Skip = "Draws and shows the flyouts offscreen; set KARPO_UI_TESTS=1 (pictures in KARPO_PNG_DIR).";
        }
    }
}

/// <summary>A theory that runs only with KARPO_UI_TESTS=1, for the same reasons <see cref="UiFactAttribute"/> has.</summary>
public sealed class UiTheoryAttribute : TheoryAttribute
{
    public UiTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("KARPO_UI_TESTS") != "1")
        {
            Skip = "Draws offscreen; set KARPO_UI_TESTS=1.";
        }
    }
}

public sealed unsafe partial class FlyoutOffscreenTests
{
    /// <summary>
    /// A selected folder's window class here, which no other test class registers. The popups that
    /// share <see cref="Flyout.ClassName"/> are at the mercy of whoever registered it first — see
    /// <see cref="Register(HINSTANCE, string)"/> — and that is what made the clock panel's tick test
    /// unsteady.
    /// </summary>
    private const string CountdownClassName = "karpo.countdown.test";

    private static readonly DateOnly Today = new(2026, 9, 22);

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>The newer islands side by side on bars of 48 and 32 DIPs and a vertical one, to compare their sizing.</summary>
    [UiFact]
    public void IslandPictures()
    {
        OnStaThread(desktop: false, gfx =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "islands-" + Guid.NewGuid().ToString("N"));
            var database = Path.Combine(folder, "db", "wpndatabase.db");
            BuildDatabase(database);
            var store = new NotificationStore(database, Path.Combine(folder, "copy"), Path.Combine(folder, "state"), app => (app, null));
            store.Prepare();
            var config = new Config();
            // The notifications preset's bell, on the made-up database.
            NotificationPartTile.Stand = store;
            var bell = new FolderIsland("/n", Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.NotificationsId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: [])), gfx, config);
            NotificationPartTile.Stand = null;
            var state = new SystemState(
                char.ConvertFromUtf32(0xE873), char.ConvertFromUtf32(0xE994),
                NetworkBackGlyph: char.ConvertFromUtf32(0xE701), BluetoothGlyph: char.ConvertFromUtf32(0xE702), BluetoothOn: true);
            FolderIsland.StatusStand = state;
            var sound = new FolderIsland("/s", Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.SoundId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: [])), gfx, config);
            FolderIsland.StatusStand = null;
            // The battery preset's face on a made-up charge, whatever this PC has.
            BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
            var battery = new FolderIsland("/y", Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.BatteryId)!, new Karpo.Core.PresetMachine(HasBattery: true, FixedDrives: [])), gfx, config);
            CellIsland[] islands =
            [
                bell,
                sound,
                battery,
                new FolderIsland("/k", Karpo.Core.Presets.Folder(Karpo.Core.Presets.Find(Karpo.Core.Presets.KeyboardId)!, new Karpo.Core.PresetMachine(HasBattery: false, FixedDrives: [])), gfx, config),
            ];
            foreach (var dark in new[] { true, false })
            {
                foreach (var (thickness, vertical) in new[] { (48f, false), (32f, false), (48f, true) })
                {
                    var look = LookFor(dark);
                    var style = new IslandStyle(1.5f, vertical, thickness * 1.5f, 24 * 1.5f, look);
                    var sizes = islands.Select(island => island.CellSize(0, style)).ToList();
                    var gap = 6f;
                    var along = sizes.Sum(size => vertical ? size.Height : size.Width) + gap * (islands.Length + 1);
                    var across = thickness * 1.5f;
                    var theme = FlyoutTheme.For(look);
                    var file = $"islands-{(dark ? "dark" : "light")}-{thickness}{(vertical ? "-vertical" : "")}.png";
                    Picture(vertical ? across : along, vertical ? along : across, 1.5f, theme, file, dc =>
                    {
                        var at = gap;
                        for (var i = 0; i < islands.Length; i++)
                        {
                            var (width, height) = sizes[i];
                            var x = vertical ? 0 : at;
                            var y = vertical ? at : 0;
                            // The island's card, as the scene puts one behind each.
                            PopupPaint.Fill(dc, PopupPaint.Rect(x, y, width, height), 6, PopupPaint.Ink(theme.CardBackground));
                            var island = islands[i];
                            Shifted(dc, x, y, () => island.PaintCell(dc, 0, style, width, height));
                            at += (vertical ? height : width) + gap;
                        }
                    });
                }
            }
            foreach (var island in islands)
            {
                island.Dispose();
            }
            BatteryIsland.Probe = SystemStatus.ReadBatteryNow;
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        });
    }

    /// <summary>
    /// The tray's hidden icons: a right click on one closes the flyout and names the icon, as
    /// a left click does, so the dock can send its app the whole right click. Released on another
    /// icon, or in a flyout that takes no right clicks, it does nothing.
    /// </summary>
    [UiFact]
    public void OverflowRightClick()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 1700, y = 1040 };
            FlyoutItem[] icons =
            [
                new(2000, "Sync", new FlyoutIcon.Picture(Solid(24, 0xFF0F6CBD))),
                new(2001, "Chat", new FlyoutIcon.Picture(Solid(24, 0xFFE3008C))),
            ];
            using var flyout = new Flyout(gfx, instance, 0) { GroupContent = true, StripIconSize = 24, ContextClicks = true };
            flyout.Show(gfx, LookFor(true), 1f, icons, FlyoutLayout.Grid, anchor, work, Karpo.Core.Edge.Bottom);
            Assert.True(flyout.IsOpen);
            var first = Centre(flyout, 2000);
            var second = Centre(flyout, 2001);
            Assert.NotEqual(first, second);

            // Down on one icon, up on the other: nothing.
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_RBUTTONDOWN, 2, first));
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_RBUTTONUP, 0, second));
            Assert.True(flyout.IsOpen);

            // Down and up on the same one: named, and closed.
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_RBUTTONDOWN, 2, second));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Context, 2001), flyout.Message(WM.WM_RBUTTONUP, 0, second));
            Assert.False(flyout.IsOpen);
            Pump();

            // A flyout that takes no right clicks (a menu, a jump list) stays open; a left click still chooses.
            flyout.ContextClicks = false;
            flyout.Show(gfx, LookFor(false), 1f, icons, FlyoutLayout.Grid, anchor, work, Karpo.Core.Edge.Bottom);
            first = Centre(flyout, 2000);
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_RBUTTONDOWN, 2, first));
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_RBUTTONUP, 0, first));
            Assert.True(flyout.IsOpen);
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_LBUTTONDOWN, 1, first));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Invoke, 2000), flyout.Message(WM.WM_LBUTTONUP, 0, first));
            Assert.False(flyout.IsOpen);
            Pump();
        });
    }

    /// <summary>
    /// The tray's hidden icons tell the pointer and the left button as they happen
    /// (Flyout.TellsPointer), so the dock sends their apps what a bar sends its icons: the icon the
    /// pointer moves over and none once it has left, its coming to rest there, and the button's down
    /// and its up on the same icon — and the flyout stays open through a click, so a second one can
    /// be a double click and the app taking the foreground is what closes it. An up on another icon
    /// is not told. Each icon says where it stands on the screen, for its app's popup and menu; and
    /// the keyboard still chooses, and closes.
    /// </summary>
    [UiFact]
    public void OverflowTellsThePointerAndTheButtons()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 1700, y = 1040 };
            FlyoutItem[] icons =
            [
                new(2000, "Sync", new FlyoutIcon.Picture(Solid(24, 0xFF0F6CBD))),
                new(2001, "Chat", new FlyoutIcon.Picture(Solid(24, 0xFFE3008C))),
            ];
            using var flyout = new Flyout(gfx, instance, 0) { GroupContent = true, StripIconSize = 24, ContextClicks = true, DragItems = true, TellsPointer = true };
            flyout.Show(gfx, LookFor(true), 1f, icons, FlyoutLayout.Grid, anchor, work, Karpo.Core.Edge.Bottom);
            Assert.True(flyout.IsOpen);
            var first = Centre(flyout, 2000);
            var second = Centre(flyout, 2001);

            // Over one icon, resting there, then over the other.
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Pointer, 2000), flyout.Message(WM.WM_MOUSEMOVE, 0, first));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Rest, 2000), flyout.Message(WM.WM_MOUSEHOVER, 0, first));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Pointer, 2001), flyout.Message(WM.WM_MOUSEMOVE, 0, second));

            // A click is its down and its up, told, and the flyout stays open for a second one.
            for (var click = 0; click < 2; click++)
            {
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Press, 2001), flyout.Message(WM.WM_LBUTTONDOWN, 1, second));
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Release, 2001), flyout.Message(WM.WM_LBUTTONUP, 0, second));
                Assert.True(flyout.IsOpen);
            }

            // Down on one icon and up on the other: the down is told, the up is not.
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Press, 2000), flyout.Message(WM.WM_LBUTTONDOWN, 1, first));
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_LBUTTONUP, 0, second));
            Assert.True(flyout.IsOpen);

            // Where each icon stands on the screen: inside the flyout's window, and apart.
            RECT window;
            window = WindowShots.Extent(flyout.Hwnd);
            var sync = Assert.NotNull(flyout.ItemOnScreen(2000));
            var chat = Assert.NotNull(flyout.ItemOnScreen(2001));
            foreach (var place in new[] { sync, chat })
            {
                Assert.True(place.left >= window.left && place.top >= window.top && place.right <= window.right && place.bottom <= window.bottom,
                    $"{place.left},{place.top}..{place.right},{place.bottom} outside the window {window.left},{window.top}..{window.right},{window.bottom}");
                Assert.True(place.right > place.left && place.bottom > place.top);
            }
            Assert.True(sync.right <= chat.left || chat.right <= sync.left || sync.bottom <= chat.top || chat.bottom <= sync.top, "the two icons stand on each other");
            Assert.Null(flyout.ItemOnScreen(2999));

            // The pointer gone: over nothing.
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Pointer, 0), flyout.Message(WM.WM_MOUSELEAVE, 0, 0));

            // The keyboard still chooses, and closes.
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Pointer, 2001), flyout.Message(WM.WM_MOUSEMOVE, 0, second));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Invoke, 2001), flyout.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0));
            Assert.False(flyout.IsOpen);
            Assert.Null(flyout.ItemOnScreen(2001));
            Pump();
        });
    }

    /// <summary>
    /// tray: an icon held in the overflow is dragged out onto the bar below it, even when its app
    /// answers the press by coming to the front. The overflow is a menu, which closes for another
    /// window taking the foreground — and it closed under the held icon, so the drag out died where it
    /// began (the person: "dragging and dropping system tray icons from the hidden view into the shown
    /// view is not currently possible"). While a row is held it stays; the drag is told all the way
    /// to the drop, with where it was let go on the screen.
    /// </summary>
    [UiFact]
    public void AnIconHeldInTheOverflowIsDraggedOutWhenItsAppComesForward()
    {
        OnStaThread(desktop: true, gfx =>
        {
            using var flyout = Overflow(gfx);
            var first = Centre(flyout, 2000);
            var (x, y) = ((short)(first & 0xFFFF), (short)((first >> 16) & 0xFFFF));

            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Press, 2000), flyout.Message(WM.WM_LBUTTONDOWN, 1, first));
            // Its app comes to the front (WA_INACTIVE): the overflow stays while the icon is held.
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_ACTIVATE, 0, 0));
            Assert.True(flyout.IsOpen);

            // Down out of the overflow, onto the bar under it: a drag, wherever the pointer goes.
            var below = Point(x, y + 300);
            var moved = flyout.Message(WM.WM_MOUSEMOVE, 1, below);
            Assert.Equal((FlyoutEventKind.DragMove, 2000u), (moved.Kind, moved.Id));
            Assert.True(flyout.IsOpen);

            var dropped = flyout.Message(WM.WM_LBUTTONUP, 0, below);
            var at = new POINT { x = x, y = y + 300 };
            Assert.True(ClientToScreen(flyout.Hwnd, &at));
            Assert.Equal((FlyoutEventKind.DragDrop, 2000u, at.x, at.y), (dropped.Kind, dropped.Id, dropped.At.x, dropped.At.y));
            flyout.Hide();
            Pump();
        });
    }

    /// <summary>
    /// tray: an overflow that closes while an icon is dragged from it takes the drag with it. The
    /// rows went and the drag stayed, so the next showing took a plain move over it for a drag of
    /// whichever icon now stood in the old row.
    /// </summary>
    [UiFact]
    public void AnOverflowClosedMidDragForgetsTheDrag()
    {
        OnStaThread(desktop: true, gfx =>
        {
            using var flyout = Overflow(gfx);
            var first = Centre(flyout, 2000);
            var (x, y) = ((short)(first & 0xFFFF), (short)((first >> 16) & 0xFFFF));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Press, 2000), flyout.Message(WM.WM_LBUTTONDOWN, 1, first));
            Assert.Equal(FlyoutEventKind.DragMove, flyout.Message(WM.WM_MOUSEMOVE, 1, Point(x, y + 300)).Kind);

            flyout.Hide();
            // And the pointer is not kept by a flyout that has gone.
            Assert.NotEqual(flyout.Hwnd, GetCapture());
            ShowOverflow(gfx, flyout);

            // No button down: a move over an icon is the pointer over it, not a drag.
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Pointer, 2000), flyout.Message(WM.WM_MOUSEMOVE, 0, Centre(flyout, 2000)));
            Assert.Equal(default(FlyoutEvent), flyout.Message(WM.WM_LBUTTONUP, 0, Centre(flyout, 2001)));
            Assert.True(flyout.IsOpen);
            flyout.Hide();
            Pump();
        });
    }

    /// <summary>tray: the overflow as the dock opens it, two hidden icons in it, over a bottom bar.</summary>
    private static Flyout Overflow(Gfx gfx)
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        Register(instance);
        var flyout = new Flyout(gfx, instance, 0) { GroupContent = true, StripIconSize = 24, ContextClicks = true, DragItems = true, TellsPointer = true };
        ShowOverflow(gfx, flyout);
        Assert.True(flyout.IsOpen);
        return flyout;
    }

    private static void ShowOverflow(Gfx gfx, Flyout flyout)
    {
        FlyoutItem[] icons =
        [
            new(2000, "Sync", new FlyoutIcon.Picture(Solid(24, 0xFF0F6CBD))),
            new(2001, "Chat", new FlyoutIcon.Picture(Solid(24, 0xFFE3008C))),
        ];
        flyout.Show(gfx, LookFor(true), 1f, icons, FlyoutLayout.Grid, new POINT { x = 1700, y = 1040 }, new RECT { left = 0, top = 0, right = 1920, bottom = 1040 }, Karpo.Core.Edge.Bottom);
    }

    /// <summary>
    /// The middle of item <paramref name="id"/>, in the flyout's client px, as a mouse message's
    /// lParam: its anchor for a bottom bar gives the x, for a left one the y.
    /// </summary>
    private static nint Centre(Flyout flyout, uint id)
    {
        var across = flyout.ItemAnchor(id, Karpo.Core.Edge.Bottom);
        var along = flyout.ItemAnchor(id, Karpo.Core.Edge.Left);
        var point = new POINT { x = across.x, y = along.y };
        Assert.True(ScreenToClient(flyout.Hwnd, &point));
        return Point(point.x, point.y);
    }

    // --- Pictures ------------------------------------------------------------


    /// <summary>
    /// The keyboard's layouts (a Panel of rows) and the tray's hidden icons (a Grid of tiles) are
    /// flyouts of a widget's content: as tall as what they show, up to the most every flyout has,
    /// past which they scroll. A menu or a jump list (List) may take all the room there is, as
    /// Windows' own menus do, so sixty rows of one go past the most.
    /// </summary>
    [UiFact]
    public void TheKeyboardsLayoutsAndTheTraysIconsStopAtTheMost()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            var work = new RECT { left = 0, top = 0, right = 1920, bottom = 1040 };
            var anchor = new POINT { x = 1700, y = 1040 };
            var most = new FlyoutPlacement(anchor, work, Karpo.Core.Edge.Bottom).MostHeight(1);
            FlyoutItem[] Rows(int count) => [.. Enumerable.Range(0, count).Select(i => new FlyoutItem((uint)(3000 + i), $"Layout {i}", new FlyoutIcon.Glyph("")))];
            FlyoutItem[] Icons(int count) => [.. Enumerable.Range(0, count).Select(i => new FlyoutItem((uint)(4000 + i), $"Icon {i}", new FlyoutIcon.Picture(Solid(24, 0xFF0F6CBD))))];
            using (var flyout = new Flyout(gfx, instance, 0))
            {
                // Three layouts: every row, and the panel no taller.
                flyout.Show(gfx, LookFor(true), 1f, Rows(3), FlyoutLayout.Panel, anchor, work, Karpo.Core.Edge.Bottom);
                Assert.Equal(flyout.ContentHeight, flyout.ShownHeight);
                Assert.True(flyout.PanelSize.Y < most);
                var room = Fitting.WindowHeight(flyout.Hwnd) - (int)flyout.PanelSize.Y;
                // Sixty: the most, and they scroll.
                flyout.Show(gfx, LookFor(true), 1f, Rows(60), FlyoutLayout.Panel, anchor, work, Karpo.Core.Edge.Bottom);
                Assert.Equal(most, flyout.PanelSize.Y);
                Assert.Equal(most + room, Fitting.WindowHeight(flyout.Hwnd));
                Assert.True(flyout.ContentHeight > flyout.ShownHeight, "sixty layouts past the most do not scroll");
                flyout.Hide();
                Pump();
            }
            using (var tray = new Flyout(gfx, instance, 0) { GroupContent = true, StripIconSize = 24, ContextClicks = true })
            {
                // Four hidden icons: one row of tiles, and the panel no taller.
                tray.Show(gfx, LookFor(true), 1f, Icons(4), FlyoutLayout.Grid, anchor, work, Karpo.Core.Edge.Bottom);
                Assert.Equal(tray.ContentHeight, tray.ShownHeight);
                Assert.True(tray.PanelSize.Y < most);
                // Four hundred: the most, and they scroll.
                tray.Show(gfx, LookFor(true), 1f, Icons(400), FlyoutLayout.Grid, anchor, work, Karpo.Core.Edge.Bottom);
                Assert.Equal(most, tray.PanelSize.Y);
                Assert.True(tray.ContentHeight > tray.ShownHeight, "four hundred icons past the most do not scroll");
                tray.Hide();
                Pump();
            }
            using (var menu = new Flyout(gfx, instance, 0))
            {
                menu.Show(gfx, LookFor(true), 1f, Rows(60), FlyoutLayout.List, anchor, work, Karpo.Core.Edge.Bottom);
                Assert.True(menu.PanelSize.Y > most, $"a menu of sixty rows is {menu.PanelSize.Y} px, the flyouts' most {most}");
                menu.Hide();
                Pump();
            }
        });
    }

    /// <summary>A solid square of premultiplied BGRA, standing in for an app's icon.</summary>
    private static Pixels Solid(int size, uint argb)
    {
        var data = new byte[size * size * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            data[i] = (byte)argb;
            data[i + 1] = (byte)(argb >> 8);
            data[i + 2] = (byte)(argb >> 16);
            data[i + 3] = (byte)(argb >> 24);
        }
        return new Pixels { Data = data, Width = size, Height = size };
    }

    private delegate void Painter(ID2D1DeviceContext* dc);

    /// <summary>The panel on a neutral backdrop, with its chrome (solid base, stroke, radius 8), then the content.</summary>
    private static void Picture(float width, float height, float scale, FlyoutTheme theme, string file, Painter content)
    {
        const int margin = 16;
        var w = (uint)MathF.Ceiling(width) + 2 * margin;
        var h = (uint)MathF.Ceiling(height) + 2 * margin;
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = theme.Dark ? new DXGI_RGBA(0.20f, 0.24f, 0.30f, 1) : new DXGI_RGBA(0.72f, 0.78f, 0.86f, 1);
            dc->Clear(&backdrop);
            var panel = new D2D_RECT_F(margin, margin, margin + width, margin + height);
            PopupPaint.Fill(dc, panel, 8 * scale, PopupPaint.Ink(theme.Base));
            PopupPaint.Stroke(dc, panel, 8 * scale, MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
            var context = dc;
            Shifted(context, margin, margin, () => content(context));
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        // The shift first, then whatever was set before.
        var moved = before;
        moved._31 = x * before._11 + y * before._21 + before._31;
        moved._32 = x * before._12 + y * before._22 + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    // --- Runs on a hidden desktop ----------------------------------------------

    private readonly record struct Sent(uint Msg, WPARAM WParam, LPARAM LParam);

    /// <summary>Where the test window procedure hands the popup's messages.</summary>
    [ThreadStatic]
    private static Action<Sent>? Target;

    /// <summary>
    /// The popups' window class with this test's procedure. Other offscreen tests register it through
    /// here too: a class has one procedure per process, and theirs would take this one's messages.
    /// </summary>
    internal static void Register(HINSTANCE instance) => Register(instance, Flyout.ClassName);

    /// <summary>
    /// <paramref name="className"/> with this test's procedure, and — the part that matters — a
    /// check that the class now in force really is this one's.
    /// <para>
    /// A class is registered once per process by whoever gets there first, and
    /// <c>RegisterClassW</c> says nothing at all when the name is already taken. Sharing a name is
    /// therefore safe only while everyone sharing it registers the <em>same</em> procedure; the
    /// moment two procedures want one name, the loser's windows answer with the winner's, and a
    /// test that drives a popup by sending it messages passes having driven nothing. Every
    /// registration is checked here rather than the rule being left to a comment, so that the next
    /// name collision is a red test at the moment it happens instead of a quiet one years later.
    /// </para>
    /// </summary>
    internal static void Register(HINSTANCE instance, string className)
    {
        fixed (char* name = className)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Procedure, hInstance = instance, lpszClassName = name, hCursor = LoadCursorW(HINSTANCE.NULL, IDC.IDC_ARROW) };
            _ = RegisterClassW(&type);
            // Asked of a window of the class rather than by comparing procedure addresses: two
            // pointers to one function need not be equal (CS8909), and the question is what the
            // class's windows actually run, which only running one answers.
            var probe = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
            Assert.True(probe != HWND.NULL, $"the window class {className} is not registered");
            nint answer;
            try
            {
                answer = SendMessageW(probe, WM_PROBE, 0, 0);
            }
            finally
            {
                _ = DestroyWindow(probe);
            }
            Assert.True(
                answer == ProbeAnswer,
                $"the window class {className} is held by another test's procedure, so this test's popups would answer nothing "
                + "and it would pass having exercised nothing: give the popup a class name of its own");
        }
    }

    /// <summary>
    /// A message only this test's procedure answers, and what it answers — the check
    /// <see cref="Register(HINSTANCE, string)"/> makes. The menus' offscreen tests ask the same
    /// message of their own class and expect a different answer, so neither mistakes the other's.
    /// </summary>
    private const uint WM_PROBE = WM.WM_APP + 0x1D5;

    private const nint ProbeAnswer = 0x464C59; // "FLY"

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // Answered before anything is handed on: the probe is Register's question, not the popup's.
        if (msg == WM_PROBE)
        {
            return ProbeAnswer;
        }
        try
        {
            Target?.Invoke(new Sent(msg, wParam, lParam));
        }
        catch (Exception error)
        {
            Failures.Add(error);
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    [ThreadStatic]
    private static List<Exception>? failures;

    private static List<Exception> Failures => failures ??= [];

    /// <summary>The pointer over every part of the window, clicks, the wheel and the keys, as a person would send them.</summary>
    private static void Exercise(HWND window)
    {
        // Over its box (§355: the window spans the display), in client px.
        var extent = Karpo.Tests.Edit.WindowShots.Extent(window);
        var corner = Karpo.Tests.Edit.WindowShots.Client(window, 0, 0);
        var bounds = new RECT { left = corner.x, top = corner.y, right = corner.x + extent.right - extent.left, bottom = corner.y + extent.bottom - extent.top };
        for (var y = bounds.top; y < bounds.bottom; y += 23)
        {
            for (var x = bounds.left; x < bounds.right; x += 31)
            {
                Send(window, WM.WM_MOUSEMOVE, 0, Point(x, y));
            }
        }
        Send(window, WM.WM_MOUSELEAVE, 0, 0);
        // Clicks in the middle third, where the days and the cards are; one may close the popup.
        for (var y = bounds.top + ((bounds.bottom - bounds.top) / 3); y < bounds.top + ((bounds.bottom - bounds.top) * 2 / 3) && IsWindowVisible(window); y += 37)
        {
            var x = (bounds.left + bounds.right) / 2;
            Send(window, WM.WM_MOUSEMOVE, 0, Point(x, y));
            Send(window, WM.WM_LBUTTONDOWN, 1, Point(x, y));
            Send(window, WM.WM_LBUTTONUP, 0, Point(x, y));
        }
        RECT screen;
        screen = WindowShots.Extent(window);
        foreach (var delta in new[] { 120, -120, -240, 360 })
        {
            Send(window, WM.WM_MOUSEWHEEL, (nuint)(uint)(delta << 16), Point(screen.left + 50, screen.top + 50));
        }
        foreach (var key in new[] { VK.VK_LEFT, VK.VK_RIGHT, VK.VK_UP, VK.VK_DOWN, VK.VK_PRIOR, VK.VK_NEXT, VK.VK_HOME, VK.VK_END })
        {
            Send(window, WM.WM_KEYDOWN, (nuint)key, 0);
        }
        Pump();
        Assert.Empty(Failures);
    }

    private static void Send(HWND window, uint msg, nuint wParam, nint lParam)
    {
        if (IsWindow(window))
        {
            _ = SendMessageW(window, msg, (WPARAM)wParam, (LPARAM)lParam);
        }
    }

    private static nint Point(int x, int y) => (nint)(((y & 0xFFFF) << 16) | (x & 0xFFFF));

    private static void Pump()
    {
        MSG message;
        while (PeekMessageW(&message, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&message);
            _ = DispatchMessageW(&message);
        }
    }


    private static void BuildDatabase(string path) => BuildDatabase(path, 12);

    /// <summary>
    /// A synthetic wpndatabase with <paramref name="count"/> toasts, which the sizing tests
    /// use to give the bell a different number of notifications waiting.
    /// </summary>
    internal static void BuildDatabase(string path, int count)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        Assert.Equal(Sqlite.OK, Sqlite.Open(path, Sqlite.OPEN_READWRITE | Sqlite.OPEN_CREATE, out var db));
        try
        {
            var now = DateTime.UtcNow.ToFileTimeUtc();
            var sql = new StringBuilder(
                "CREATE TABLE Notification (\"Order\" INTEGER PRIMARY KEY AUTOINCREMENT, Id INTEGER NOT NULL, HandlerId INTEGER, Type TEXT NOT NULL, "
                + "Payload BLOB, ExpiryTime INT64, ArrivalTime INT64, PayloadType TEXT NOT NULL);"
                + "CREATE TABLE NotificationHandler (RecordId INTEGER PRIMARY KEY, PrimaryId TEXT NOT NULL);"
                + "INSERT INTO NotificationHandler VALUES (1, 'Mail'), (2, 'Teams');");
            for (var i = 1; i <= count; i++)
            {
                var toast = $"<toast><visual><binding template=\"ToastGeneric\"><text>Message {i}</text><text>Body of message {i}, long enough to wrap onto a second line in the list.</text></binding></visual></toast>";
                sql.Append($"INSERT INTO Notification (Id, HandlerId, Type, Payload, ExpiryTime, ArrivalTime, PayloadType) VALUES ({i}, {1 + i % 2}, 'toast', X'{Convert.ToHexString(Encoding.UTF8.GetBytes(toast))}', 0, {now - i * TimeSpan.TicksPerMinute * 7}, 'Xml');");
            }
            Assert.Equal(Sqlite.OK, Sqlite.Execute(db, sql.ToString()));
        }
        finally
        {
            Sqlite.Close(db);
        }
    }

    // --- The thread both run on ------------------------------------------------

    /// <summary>
    /// Runs <paramref name="body"/> on a new thread made an STA by its dispatcher queue (the compositor needs
    /// one) and a Gfx; with <paramref name="desktop"/>, on a desktop of its own that nobody sees.
    /// </summary>
    private static void OnStaThread(bool desktop, Action<Gfx> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                if (desktop)
                {
                    fixed (char* name = "karpo-islands-test-" + Guid.NewGuid().ToString("N"))
                    {
                        hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                    }
                    Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                    // Before this thread owns any window: its windows are then made there.
                    Assert.True(SetThreadDesktop(hidden));
                }
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    // The queue makes the thread an STA, after SetThreadDesktop: COM may make a window for the
                    // apartment, and a thread that owns a window can no longer change desktops.
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
