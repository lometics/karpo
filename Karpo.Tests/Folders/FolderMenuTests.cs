// folders: a right click on a widget in a folder opens that widget's own menu over the folder, and
// the folder stays open (the person: "implement rightclick context actions for all the grid and
// list widgets if applicable. (for all widgets on right click it should not close the folder
// anymore.)"). A left click in a folder still opens no flyout: a menu is not a flyout.
//
// These hold the folder's half of it, on real folder windows and real menus on a desktop of their
// own: the right click hands the widget to the dock and the folder holds itself open while the
// menu stands — through the focus going to the menu and a press elsewhere — and lets go when the
// dock says the menu has ended. A pick that acts in Karpo is run through the dock's own act (with
// a made-up sound machine behind it, and a launcher that only writes down what it was handed), and
// the folder is still open after it. What the dock then does with the folder is its decision
// (Dock.FolderAfterMenu, WidgetMenuTests), asked here with what the menu really said.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using AfterMenu = Karpo.Dock.Dock.AfterMenu;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderMenuTests
{
    private const string Owner = "layout:default";
    private static readonly RECT Work = new() { left = 0, top = 0, right = 1920, bottom = 1040 };
    private static readonly POINT Anchor = new() { x = 960, y = 1040 };

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>A list folder with a battery's line and a clock's.</summary>
    private static BarItem List()
    {
        var island = Folder();
        Put(island, ListKind.Battery, "battery", 0);
        Put(island, ListKind.Clock, "clock", 1);
        return island;
    }

    private static BarItem Folder(string id = "box")
    {
        var island = BarKind.FolderList.New();
        island.Id = id;
        return island;
    }

    private static CellItem Put(BarItem folder, ListKind kind, string id, int row)
    {
        var line = kind.New();
        line.Id = id;
        folder.Folder!.Put(line);
        return line;
    }

    /// <summary>A mouse message's point: the screen point in the window's own px.</summary>
    private static LPARAM Client(HWND window, POINT screen)
    {
        var at = screen;
        Assert.True(ScreenToClient(window, &at));
        return (LPARAM)(nint)((at.y << 16) | (at.x & 0xFFFF));
    }

    /// <summary>The middle of the menu's row <paramref name="id"/>, in its window's px, as a mouse message's point.</summary>
    private static LPARAM Row(Flyout menu, uint id)
    {
        var across = menu.ItemAnchor(id, Edge.Bottom);
        var along = menu.ItemAnchor(id, Edge.Left);
        return Client(menu.Hwnd, new POINT { x = across.x, y = along.y });
    }

    /// <summary>What the folder's host answers a dismiss with, as the hook and the bar send it.</summary>
    private static uint Generation(FolderFlyout folder) => FlyoutHost.OpenMenus.Single(host => host.Hwnd == folder.Hwnd).Generation;

    /// <summary>
    /// A right click on a widget, at rest and while the bar is arranged, hands that widget to the
    /// dock for its menu and leaves the folder open, holding itself open while the menu stands;
    /// when the dock says the menu has ended the folder is still open and goes back to what it was
    /// — closing for a press elsewhere at rest, staying open while arranged.
    /// <para>
    /// Regression: at rest the folder closed itself before it read the widget, and closing it
    /// empties its tiles, so the right click threw — "Index was out of range", from the folder's
    /// message handler, in the person's log — and no menu ever showed.
    /// </para>
    /// </summary>
    [UiTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ARightClickOnAWidgetLeavesTheFolderOpenBehindItsMenu(bool editing)
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing);
            Assert.Equal(editing, flyout.StaysOpen);
            var over = flyout.ScreenOfCard(1);

            var happened = flyout.Message(WM.WM_RBUTTONUP, default, Client(flyout.Hwnd, over));

            Assert.Equal(FolderFlyoutAction.Context, happened.Kind);
            Assert.Equal($"{Owner}/box/clock", happened.Widget?.Key);
            Assert.Equal(over, happened.At);
            Assert.True(flyout.IsOpen, "the folder closed under the menu");
            Assert.True(flyout.StaysOpen, "the folder does not hold itself open while the menu stands");

            flyout.MenuClosed();

            Assert.True(flyout.IsOpen, "the folder closed when the menu did");
            Assert.Equal(editing, flyout.StaysOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>A right click on no widget — the panel's edge, outside every line — asks for nothing and holds nothing.</summary>
    [UiFact]
    public void ARightClickBetweenTheWidgetsAsksForNothing()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var folder = new FolderIsland($"{Owner}/box", List(), gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing: false);
            // The panel's top-left corner, in the pad before the first line.
            var happened = flyout.Message(WM.WM_RBUTTONUP, default, (LPARAM)(nint)((1 << 16) | 1));

            Assert.Equal(FolderFlyoutAction.None, happened.Kind);
            Assert.True(flyout.IsOpen);
            Assert.False(flyout.StaysOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The whole of it at rest, with a real menu over a real folder: the output device's line is
    /// right-clicked, its menu comes up with the line's own acts over the rows every widget has, and
    /// the menu taking the focus and a press elsewhere while it stands leave the folder alone.
    /// "Mute" is picked: the made-up machine is muted, nothing is handed to Windows, and the folder
    /// is open after it — the dock's decision for that pick is to leave it so. The menu is opened
    /// once more and closed by Escape, which leaves the folder open too.
    /// </summary>
    [UiFact]
    public void ChoosingAnActThatStaysInKarpoLeavesTheFolderOpen()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var machine = new SoundMachine();
            var island = Folder();
            var output = Put(island, ListKind.SoundOutput, "output", 0);
            Put(island, ListKind.Cpu, "cpu", 1);
            SoundParts.Made = () => machine;
            FolderIsland folder;
            try
            {
                folder = new FolderIsland($"{Owner}/box", island, gfx, new Config());
            }
            finally
            {
                SoundParts.Made = null;
            }
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing: false);
            var opened = new List<(string Target, string? Verb)>();

            var happened = flyout.Message(WM.WM_RBUTTONUP, default, Client(flyout.Hwnd, flyout.ScreenOfCard(0)));
            Assert.Equal(FolderFlyoutAction.Context, happened.Kind);
            var widget = Assert.IsAssignableFrom<IIsland>(happened.Widget);
            var (items, acts) = TheDock.FolderMenuRows(output, widget);
            Assert.Equal(["Output device", "Speakers", "", "Mute", "Open volume mixer", "Sound settings"], items.Select(item => item.Text));
            Assert.Equal(true, items[1].Checked);
            Assert.Equal(false, items.Single(item => item.Text == "Mute").Checked);

            using var menu = new Flyout(gfx, instance, 0);
            menu.Show(gfx, Dark, 1f, items, FlyoutLayout.List, happened.At, Work, Edge.Bottom, "Output");
            Assert.True(menu.IsOpen);
            // The menu has the focus now, and a press lands outside the folder: both only close a
            // folder that is not held.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_ACTIVATE, (WPARAM)WA_INACTIVE, default).Kind);
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(FlyoutHost.WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)Generation(flyout), default).Kind);
            Assert.True(flyout.IsOpen, "the folder closed while its widget's menu stood over it");

            // "Mute", picked.
            var mute = acts.Single(row => row.Act == WidgetAct.Mute).Id;
            Assert.Equal(default, menu.Message(WM.WM_LBUTTONDOWN, 1, Row(menu, mute)));
            var picked = menu.Message(WM.WM_LBUTTONUP, 0, Row(menu, mute));
            // A switch: it acts and the menu stays, its tick shown flipped (MenuKeeps.cs, §219).
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, mute), picked);
            Assert.True(menu.IsOpen);
            var row = Assert.IsType<WidgetActRow>(TheDock.ActRowOf(acts, picked.Id));
            TheDock.RunAct(row, flyout.Folder?.Widget(widget.Key), (target, verb) => opened.Add((target, verb)));
            menu.Refresh(MenuKeeps.Ticked(menu.ShownItems, acts, picked.Id));
            Assert.Equal(true, menu.ShownItems.Single(item => item.Text == "Mute").Checked);

            Assert.True(machine.Muted, "the pick did not mute");
            Assert.Empty(opened);
            Assert.Equal(AfterMenu.Stays, TheDock.FolderAfterMenu(false, TheDock.PickHandsAway(acts, picked.Id), focusElsewhere: false));
            menu.Hide();
            flyout.MenuClosed();
            Assert.True(flyout.IsOpen, "the folder closed after a pick that acted in Karpo");
            Assert.False(flyout.StaysOpen);
            // The menu reads the widget as it is now: muted, it is ticked.
            Assert.Equal(true, TheDock.FolderMenuRows(output, widget).Items.Single(item => item.Text == "Mute").Checked);

            // Escape: no press, and the folder stays.
            Assert.Equal(FolderFlyoutAction.Context, flyout.Message(WM.WM_RBUTTONUP, default, Client(flyout.Hwnd, flyout.ScreenOfCard(0))).Kind);
            menu.Show(gfx, Dark, 1f, items, FlyoutLayout.List, happened.At, Work, Edge.Bottom, "Output");
            Assert.Equal(FlyoutEventKind.Dismissed, menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, default).Kind);
            Assert.Null(menu.DismissedAt);
            Assert.Equal(AfterMenu.Stays, TheDock.FolderAfterMenu(false, handedAway: false, focusElsewhere: false));
            flyout.MenuClosed();
            Assert.True(flyout.IsOpen);

            // Let go of, the folder closes for the focus going elsewhere again, as any folder at rest does.
            Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(WM.WM_ACTIVATE, (WPARAM)WA_INACTIVE, default).Kind);
            Assert.False(flyout.IsOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A press away from a folder while a widget's menu stands over it closes the menu alone; the
    /// next press closes the folder. Regression (the person, 2026-10-16: "when the right click
    /// context menu is open in a folder and i click away from the folder it closes both, but it
    /// should only close the context menu first. on the next click the folder then."): the menu
    /// said where the press that closed it was, and a press outside the folder closed both.
    /// <para>
    /// A menu over a folder uses up the presses outside it and its submenus (docs\widgets.md
    /// §185): the hook answers the press itself, so the bar, another app, the desktop and the
    /// folder hear nothing of it, and it closes the menu wherever it was, a bar's included. The
    /// press is handed over as the hook hands it, on a desktop that stands for the one in use.
    /// </para>
    /// </summary>
    [UiFact]
    public void APressAwayClosesTheMenuOverAFolderAloneAndTheNextOneTheFolder()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var island = List();
            var folder = new FolderIsland($"{Owner}/box", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            using var menu = new Flyout(gfx, instance, 0) { UsesUpPresses = true };
            var away = new POINT { x = Work.right - 20, y = 20 };
            InputDesktop.Holds = () => true;
            // Wherever the press lands, a bar would take it as its own.
            FlyoutHost.PressesHandledBy = _ => true;
            try
            {
                flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing: false);
                var happened = flyout.Message(WM.WM_RBUTTONUP, default, Client(flyout.Hwnd, flyout.ScreenOfCard(0)));
                Assert.Equal(FolderFlyoutAction.Context, happened.Kind);
                Assert.False(flyout.Contains(away));
                menu.Show(gfx, Dark, 1f, [new(1, "Copy", new FlyoutIcon.Glyph("")), new(2, "Settings", new FlyoutIcon.Glyph(""))], FlyoutLayout.List, happened.At, Work, Edge.Bottom, "Battery");
                // The hook knows the menu's windows, and only the menu's: the folder's are not among them.
                Assert.Equal([(nint)menu.Hwnd], PressHook.Keeping(menu.Hwnd)!);
                Assert.Null(PressHook.Keeping(flyout.Hwnd));

                // The press away, used up: the menu closes for it, giving the focus back; the
                // folder, held, stays through it, and the dock leaves it open.
                FlyoutHost.Pressed(WM.WM_LBUTTONDOWN, away, usedUp: true);
                if (Dismissal(flyout.Hwnd) is { } early)
                {
                    Assert.Equal(FolderFlyoutAction.None, flyout.Message(early.message, early.wParam, early.lParam).Kind);
                }
                var dismissal = Assert.IsType<MSG>(Dismissal(menu.Hwnd));
                Assert.Equal(1, (nint)dismissal.lParam);
                Assert.Equal(FlyoutEventKind.Dismissed, menu.Message(dismissal.message, dismissal.wParam, dismissal.lParam).Kind);
                Assert.False(menu.IsOpen);
                Assert.Null(PressHook.Keeping(menu.Hwnd));
                Assert.Equal(AfterMenu.Stays, TheDock.FolderAfterMenu(false, handedAway: false, focusElsewhere: false));
                flyout.MenuClosed();
                Assert.True(flyout.IsOpen, "the press that closed the menu closed the folder too");

                // The next press away closes the folder, as a press outside a folder always does.
                FlyoutHost.PressesHandledBy = null;
                FlyoutHost.Pressed(WM.WM_LBUTTONDOWN, away);
                var next = Assert.IsType<MSG>(Dismissal(flyout.Hwnd));
                Assert.Equal(FolderFlyoutAction.Dismissed, flyout.Message(next.message, next.wParam, next.lParam).Kind);
                Assert.False(flyout.IsOpen);
            }
            finally
            {
                FlyoutHost.PressesHandledBy = null;
                InputDesktop.Holds = InputDesktop.Here;
                flyout.Hide();
                folder.Dispose();
            }
        });
    }

    private static MSG? Dismissal(HWND window)
    {
        MSG message;
        return PeekMessageW(&message, window, FlyoutHost.WM_KARPO_HOST_DISMISS, FlyoutHost.WM_KARPO_HOST_DISMISS, PM.PM_REMOVE)
            ? message
            : null;
    }

    /// <summary>
    /// A row that opens something of Windows' goes through the launcher the dock hands over — here
    /// one that only writes it down, so nothing opens — and the dock's decision closes the folder
    /// at rest for it, as the program taking the focus would a moment later, and keeps it open while
    /// the bar is arranged. There the menu has no such rows: it is the rows arranging takes, as on
    /// the bar, and even the one of them that opens the settings app leaves the folder be.
    /// </summary>
    [UiFact]
    public void ARowThatOpensSomethingClosesTheFolderAtRestAndNotWhileArranged()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            var island = Folder();
            var cpu = Put(island, ListKind.Cpu, "cpu", 0);
            var folder = new FolderIsland($"{Owner}/box", island, gfx, new Config());
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var opened = new List<(string Target, string? Verb)>();

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing: false);
            var happened = flyout.Message(WM.WM_RBUTTONUP, default, Client(flyout.Hwnd, flyout.ScreenOfCard(0)));
            var widget = Assert.IsAssignableFrom<IIsland>(happened.Widget);
            var (items, acts) = TheDock.FolderMenuRows(cpu, widget);
            Assert.Equal(["Open Resource Monitor"], items.Select(item => item.Text));
            var monitor = acts.Single().Id;
            TheDock.RunAct(acts.Single(), widget, (target, verb) => opened.Add((target, verb)));
            Assert.Equal([("resmon.exe", (string?)null)], opened);
            Assert.Equal(AfterMenu.Closes, TheDock.FolderAfterMenu(false, TheDock.PickHandsAway(acts, monitor), focusElsewhere: false));
            flyout.MenuClosed();
            flyout.Hide();

            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing: true);
            Assert.Equal(FolderFlyoutAction.Context, flyout.Message(WM.WM_RBUTTONUP, default, Client(flyout.Hwnd, flyout.ScreenOfCard(0))).Kind);
            // Arranged, even what would take the screen leaves the folder open ("Edit taskbar").
            Assert.Equal(AfterMenu.Stays, TheDock.FolderAfterMenu(true, TheDock.PickHandsAway([], TheDock.CMD_TASKBAR_SETTINGS), focusElsewhere: true));
            flyout.MenuClosed();
            Assert.True(flyout.IsOpen);
            Assert.True(flyout.StaysOpen);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// The file changing under an open folder lays it out again, and a widget the pointer is
    /// carrying is still the widget it picked up: it is kept by what it is, not by where it stood.
    /// Regression: the folder kept the carried widget's place in its list across the new layout, so
    /// with a widget before it gone the place named the next widget, and letting go moved that one.
    /// </summary>
    [UiFact]
    public void AWidgetBeingMovedIsStillItselfAfterTheFolderIsLaidOutAgain()
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            static BarItem Row(bool battery)
            {
                var island = Folder("row");
                if (battery)
                {
                    Put(island, ListKind.Battery, "battery", 0);
                }
                Put(island, ListKind.Clock, "clock", 1);
                Put(island, ListKind.Memory, "memory", 2);
                // As tall as the clock, so letting go on its middle is past the clock's own middle.
                island.Folder!.Cells.Single(cell => cell.Island.Id == "memory").Island.Height = LineHeight.Exactly(6);
                return island;
            }
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/row", Row(battery: true), gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing: true);

            // The clock, the second line, picked up.
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_LBUTTONDOWN, 1, Client(flyout.Hwnd, flyout.ScreenOfCard(1))).Kind);
            // The file changes under it: the battery before it goes, and the folder is laid out again.
            folder.Configure(Row(battery: false), config);
            // Carried onto the memory's line, the second now, and let go there.
            var onto = Client(flyout.Hwnd, flyout.ScreenOfCard(1));
            Assert.Equal(FolderFlyoutAction.None, flyout.Message(WM.WM_MOUSEMOVE, 1, onto).Kind);
            var happened = flyout.Message(WM.WM_LBUTTONUP, 0, onto);

            Assert.Equal(FolderFlyoutAction.Place, happened.Kind);
            Assert.Equal($"{Owner}/row/clock", happened.Widget?.Key);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>
    /// A setting changed in a line's menu while its folder stands open shows at once, the menu still
    /// up (§224): the dock saves it and hands the folder its new settings, and the open folder draws
    /// the lines it has now — a graph restyled from the processor to the memory, a widget made again
    /// under the same key and as tall as before, and a clock whose label changed, kept with its new
    /// cell. Regression (the person, 2026-10-19: "when changing the settings of a widget in the folder
    /// view it does not update the widget shown"): with the layout's signature unchanged the open
    /// folder went on drawing the lines it held before — the graph it had let go of, and the clock's
    /// old cell — until it was opened again.
    /// </summary>
    [UiTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASettingChangedInALinesMenuShowsInTheOpenFolderAtOnce(bool editing)
    {
        OnHiddenDesktop((gfx, instance) =>
        {
            static BarItem Lines(ListKind graph, string label)
            {
                var island = Folder();
                Put(island, graph, "graph", 0).Height = LineHeight.Exactly(6);
                var clock = Put(island, ListKind.Clock, "clock", 1);
                clock.Clock = new ClockOptions { Label = label };
                clock.Height = LineHeight.Exactly(4);
                return island;
            }
            var config = new Config();
            var folder = new FolderIsland($"{Owner}/box", Lines(ListKind.Cpu, "Here"), gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, Anchor, Work, Edge.Bottom, folder, FlyoutAlign.Center, editing);
            var before = flyout.Lines.Select(tile => tile.Island).ToList();
            var plan = flyout.Plan!;

            folder.Configure(Lines(ListKind.Memory, "Zurich"), config);

            Assert.True(flyout.IsOpen);
            Assert.Equal(plan.Cards, flyout.Plan!.Cards);
            Assert.Equal(folder.Tiles.Select(tile => tile.Island), flyout.Lines.Select(tile => tile.Island), ReferenceEqualityComparer.Instance);
            Assert.Equal(folder.Tiles.Select(tile => tile.Cell), flyout.Lines.Select(tile => tile.Cell), ReferenceEqualityComparer.Instance);
            Assert.NotSame(before[0], flyout.Lines[0].Island);
            Assert.Equal(ListKind.Memory, ListWidgets.Find(flyout.Lines[0].Island.Info.Id)?.Kind);
            Assert.Same(before[1], flyout.Lines[1].Island);
            Assert.Equal("Zurich", flyout.Lines[1].Cell.Island.Clock?.Label);
            flyout.Hide();
            folder.Dispose();
        });
    }

    /// <summary>A made-up sound machine: one output, muted or not, and nothing reaches Windows.</summary>
    private sealed class SoundMachine : ISoundSource
    {
        public bool Muted { get; private set; }

        public void Start(Changes changes)
        {
        }

        public SoundState Read(int changes) =>
            new(true, 0.5f, Muted, [new AudioOutput("speakers", "Speakers", char.ConvertFromUtf32(0xE7F5), true)], [], [], false);

        public void SetVolume(float value)
        {
        }

        public void SetMuted(bool muted) => Muted = muted;

        public void SetDefault(string id)
        {
        }

        public void SetDefaultInput(string id)
        {
        }

        public void SetInputMuted(bool muted)
        {
        }

        public void SetAppVolume(string key, float value)
        {
        }

        public void SetAppMuted(string key, bool muted)
        {
        }

        public void Dispose()
        {
        }
    }

    private static void OnHiddenDesktop(Action<Gfx, HINSTANCE> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = "karpo-folder-menu-test-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                // Before this thread owns any window: its windows are then made there.
                Assert.True(SetThreadDesktop(hidden));
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                    FlyoutOffscreenTests.Register(instance);
                    body(gfx, instance);
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
