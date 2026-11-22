// §666: where Windows' Start stands while Karpo runs — hung from Karpo's Start button — and where its host and search box
// go for that (Karpo.Dock\StartPlacement.cs). Pure: no window is moved here, nothing of this PC's is read.
//
// Windows' side is modelled as §666 found it (WindowsStart below): Start's host as wide as its display, ending where
// Windows' hidden taskbar (48 DIP) begins, Start's frame (Start and Windows' 12 DIP gap round it) laid out at the host's
// foot, at its left or in its middle; Start's search box that frame, placed by Windows first against the screen's end
// and then against the host's end. The tests: the user's own scenes as appbar.log heard them, a table over every edge,
// DPI and display arrangement, and properties over thousands of drawn scenes.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed class StartPlacementTests
{
    private static RECT R(int left, int top, int right, int bottom) => new() { left = left, top = top, right = right, bottom = bottom };

    private static string Show(RECT rect) => $"{rect.left},{rect.top}..{rect.right},{rect.bottom}";

    private static StartDisplay D(RECT bounds, RECT work, uint dpi) => new(bounds, work, dpi);

    // The user's displays (2026-11-09/10, appbar.log, UI Automation): two 3840x2160 at 150 %, the other one left of the main.
    private static readonly RECT Main = R(0, 0, 3840, 2160);
    private static readonly RECT Other = R(-3840, 0, 0, 2160);

    /// <summary>Start's frame as appbar.log heard it at every showing: its search box, 1287x1335 at 150 %.</summary>
    private static readonly (int Width, int Height) UsersStart = (1287 - 36, 1335 - 36);

    /// <summary>Windows' side, as §666 has it: where it puts Start's host and box on a display, and where Start is in the host.</summary>
    private static class WindowsStart
    {
        /// <summary>Start's host as Windows places it on <paramref name="display"/>: as wide, ending a hidden taskbar (48 DIP) above its end.</summary>
        public static RECT Host(StartDisplay display) =>
            R(display.Bounds.left, display.Bounds.top, display.Bounds.right, display.Bounds.bottom - (int)(48 * display.Dpi / 96));

        /// <summary>Start's search box as Windows settles it: Start's frame where Windows lays Start out in its own host.</summary>
        public static RECT Box(StartScene scene, (int Width, int Height) size)
        {
            var start = StartPlacement.InHost(Host(scene.Display), scene, size);
            return R(start.left - scene.Gap, start.top - scene.Gap, start.right + scene.Gap, start.bottom + scene.Gap);
        }

        /// <summary>The box as Windows first puts it as Start opens: the same, against the screen's end (seen: 0,825..1287,2160).</summary>
        public static RECT FirstBox(StartScene scene, (int Width, int Height) size)
        {
            var settled = Box(scene, size);
            var down = scene.Display.Bounds.bottom - settled.bottom;
            return R(settled.left, settled.top + down, settled.right, settled.bottom + down);
        }
    }

    private static RECT At(RECT rect, (int X, int Y)? to) =>
        to is { } at ? R(at.X, at.Y, at.X + rect.right - rect.left, at.Y + rect.bottom - rect.top) : rect;

    /// <summary>Karpo's side, once: the host placed from where it stands, then Start where Windows lays it out in it.</summary>
    private static (RECT Host, RECT Start, RECT Box) Placed(StartScene scene, (int Width, int Height) size, RECT? hostNow = null)
    {
        var host = hostNow ?? WindowsStart.Host(scene.Display);
        var box = WindowsStart.FirstBox(scene, size);
        var boxTo = At(box, StartPlacement.Box(box, host, scene));
        host = At(host, StartPlacement.Host(host, scene, size));
        return (host, StartPlacement.InHost(host, scene, size), boxTo);
    }

    // ---- The user's scenes ----

    /// <summary>
    /// The user (2026-11-10): "the native start menu is still placed at wrong locations". appbar.log: Start's host hung to
    /// 2472,-807 and its box to 2472,18 beside the bar at the right of the main display. The box is Start's frame, so
    /// Start, 1299 px tall, stood at -36 — its top 36 px above the screen — and the box 54 px below it. Now Start stands
    /// Windows' 18 px (12 DIP) under the screen's top and left of the bar, the box round it.
    /// </summary>
    [Fact]
    public void BesideTheUsersRightBarStartStandsWindowsGapUnderTheTopAndLeftOfTheBar()
    {
        var display = D(Main, R(0, 0, 3777, 2160), 144);
        var scene = new StartScene(display, new StartButton(R(3783, 6, 3834, 57), R(3777, 0, 3840, 2160), Edge.Right), false, [display, D(Other, R(-3777, 0, 0, 2160), 144)]);
        var host = R(0, 0, 3840, 2088);
        Assert.Equal((2490, -753), StartPlacement.Host(host, scene, UsersStart));
        Assert.Equal((2490, 0), StartPlacement.Box(R(0, 825, 1287, 2160), host, scene));
        Assert.Equal((2490, 0), StartPlacement.Box(R(0, 753, 1287, 2088), host, scene));
        Assert.Equal("2508,18..3759,1317", Show(StartPlacement.Target(scene, UsersStart)));
        // Where the build before left it as Start opened again (2472,-807..6312,1281): from there too, once.
        Assert.Equal((2490, -753), StartPlacement.Host(R(2472, -807, 6312, 1281), scene, UsersStart));
        Assert.Equal((2490, 0), StartPlacement.Box(R(2472, 18, 3759, 1353), R(2472, -807, 6312, 1281), scene));
        // Heard again where it was put: nothing.
        Assert.Null(StartPlacement.Host(R(2490, -753, 6330, 1335), scene, UsersStart));
        Assert.Null(StartPlacement.Box(R(2490, 0, 3777, 1335), R(2490, -753, 6330, 1335), scene));
    }

    [Fact]
    public void BesideTheUsersLeftBarOnTheOtherDisplayStartStandsWindowsGapRightOfIt()
    {
        var display = D(Other, R(-3777, 0, 0, 2160), 144);
        var scene = new StartScene(display, new StartButton(R(-3834, 6, -3783, 57), R(-3840, 0, -3777, 2160), Edge.Left), false, [D(Main, R(0, 0, 3777, 2160), 144), display]);
        Assert.Equal((-3777, -753), StartPlacement.Host(R(-3840, 0, 0, 2088), scene, UsersStart));
        Assert.Equal((-3777, 0), StartPlacement.Box(R(-3840, 825, -2553, 2160), R(-3840, 0, 0, 2088), scene));
        Assert.Equal("-3759,18..-2508,1317", Show(StartPlacement.Target(scene, UsersStart)));
    }

    /// <summary>
    /// §591's "the start menu is also too high up as if there was an invisible taskbar at the bottom": with no Start button
    /// of Karpo's on the display, Start stands where Windows stands it over a taskbar at the work area's end — down by the
    /// hidden taskbar, Windows' gap above the screen's end; above a bar at the bottom, the gap above the bar.
    /// </summary>
    [Fact]
    public void WithNoStartButtonStartStandsWhereWindowsPutsItOverATaskbarAtTheWorkAreasEnd()
    {
        var bare = D(Main, R(0, 0, 3777, 2160), 144);
        var scene = new StartScene(bare, null, false, [bare]);
        Assert.Equal("18,843..1269,2142", Show(Placed(scene, UsersStart).Start));
        var bottom = D(Main, R(0, 0, 3840, 2097), 144);
        Assert.Equal("18,780..1269,2079", Show(Placed(new StartScene(bottom, null, false, [bottom]), UsersStart).Start));
        // Centred: in the work area's middle.
        Assert.Equal("1263,843..2514,2142", Show(Placed(scene with { Centred = true }, UsersStart).Start));
    }

    // ---- The table: every edge, DPI and arrangement ----

    private static readonly Dictionary<string, (StartScene Scene, (int Width, int Height) Size, string Host, string Start)> Rows = MakeRows();

    public static TheoryData<string> Table() => [.. Rows.Keys];

    private static Dictionary<string, (StartScene, (int, int), string, string)> MakeRows()
    {
        var data = new Dictionary<string, (StartScene, (int, int), string, string)>();
        void Row(string name, StartDisplay display, StartButton? button, bool centred, StartDisplay[] all, (int, int) size, string host, string start) =>
            data.Add(name, (new StartScene(display, button, centred, all), size, host, start));

        // 150 %, 3840x2160, a 63 px (42 DIP) bar on each edge, its Start button at the bar's start.
        var main = D(Main, R(0, 63, 3840, 2160), 144);
        Row("top, start, 150 %", main, new(R(6, 6, 57, 57), R(0, 0, 3840, 63), Edge.Top), false, [main], (1251, 1299), "0,-690", "18,81..1269,1380");
        var bottom = D(Main, R(0, 0, 3840, 2097), 144);
        Row("bottom, start, 150 %", bottom, new(R(6, 2103, 57, 2154), R(0, 2097, 3840, 2160), Edge.Bottom), false, [bottom], (1251, 1299), "0,9", "18,780..1269,2079");
        var left = D(Main, R(63, 0, 3840, 2160), 144);
        Row("left, start, 150 %", left, new(R(6, 6, 57, 57), R(0, 0, 63, 2160), Edge.Left), false, [left], (1251, 1299), "63,-753", "81,18..1332,1317");
        var right = D(Main, R(0, 0, 3777, 2160), 144);
        Row("right, start, 150 %", right, new(R(3783, 6, 3834, 57), R(3777, 0, 3840, 2160), Edge.Right), false, [right], (1251, 1299), "2490,-753", "2508,18..3759,1317");
        // The button in the bar's middle: Start centred on it (Windows' centred look), from either alignment of Windows'.
        Row("top, middle, left-aligned", main, new(R(1895, 6, 1946, 57), R(0, 0, 3840, 63), Edge.Top), false, [main], (1251, 1299), "1277,-690", "1295,81..2546,1380");
        Row("top, middle, centred", main, new(R(1895, 6, 1946, 57), R(0, 0, 3840, 63), Edge.Top), true, [main], (1251, 1299), "1,-690", "1295,81..2546,1380");
        Row("bottom, middle, centred", bottom, new(R(1895, 2103, 1946, 2154), R(0, 2097, 3840, 2160), Edge.Bottom), true, [bottom], (1251, 1299), "1,9", "1295,780..2546,2079");
        // The button at the bar's far end: Start against the work area's far end, the gap kept.
        Row("top, end, 150 %", main, new(R(3783, 6, 3834, 57), R(0, 0, 3840, 63), Edge.Top), false, [main], (1251, 1299), "2553,-690", "2571,81..3822,1380");
        Row("left, foot, 150 %", left, new(R(6, 2103, 57, 2154), R(0, 0, 63, 2160), Edge.Left), false, [left], (1251, 1299), "63,72", "81,843..1332,2142");
        Row("right, halfway, 150 %", right, new(R(3783, 1055, 3834, 1106), R(3777, 0, 3840, 2160), Edge.Right), false, [right], (1251, 1299), "2490,-340", "2508,431..3759,1730");
        // 100 %, 1920x1080, a 48 px (48 DIP) bar; Start 834x636; Windows' gap 12.
        var small = D(R(0, 0, 1920, 1080), R(0, 48, 1920, 1080), 96);
        Row("top, start, 100 %", small, new(R(4, 4, 44, 44), R(0, 0, 1920, 48), Edge.Top), false, [small], (834, 636), "0,-324", "12,60..846,696");
        // 125 %, 2560x1440: gap 15, the hidden taskbar 60.
        var mid = D(R(0, 0, 2560, 1440), R(0, 0, 2500, 1440), 120);
        Row("right, start, 125 %", mid, new(R(2505, 5, 2555, 55), R(2500, 0, 2560, 1440), Edge.Right), false, [mid], (1042, 1083), "1428,-267", "1443,15..2485,1098");
        // 175 %: gap 21. 200 %: gap 24.
        var big = D(R(0, 0, 3840, 2160), R(0, 0, 3840, 2076), 168);
        Row("bottom, start, 175 %", big, new(R(7, 2083, 77, 2153), R(0, 2076, 3840, 2160), Edge.Bottom), false, [big], (1459, 1515), "0,0", "21,540..1480,2055");
        var huge = D(R(0, 0, 3840, 2160), R(96, 0, 3840, 2160), 192);
        Row("left, start, 200 %", huge, new(R(8, 8, 88, 88), R(0, 0, 96, 2160), Edge.Left), false, [huge], (1668, 1732), "96,-284", "120,24..1788,1756");
        // The other display, left of the main one, negative coordinates, its bar at the top.
        var other = D(Other, R(-3840, 63, 0, 2160), 144);
        Row("top, other display", other, new(R(-3834, 6, -3783, 57), R(-3840, 0, 0, 63), Edge.Top), false, [D(Main, Main, 144), other], (1251, 1299), "-3840,-690", "-3822,81..-2571,1380");
        // A display above the main one at 100 %, the main one at 150 % below it (mixed DPI): Start on the upper one.
        var above = D(R(0, -1080, 1920, 0), R(0, -1040, 1920, 0), 96);
        Row("top, display above, mixed DPI", above, new(R(4, -1076, 36, -1044), R(0, -1080, 1920, -1040), Edge.Top), false, [D(Main, Main, 144), above], (834, 636), "0,-1412", "12,-1028..846,-392");
        // A display right of the main one: the whole way would put most of the host on it, so Start goes as far as it may
        // along the bar, at the top, never over the bar.
        var crowded = D(Main, R(0, 0, 3777, 2160), 144);
        Row("right, a display further right", crowded, new(R(3783, 6, 3834, 57), R(3777, 0, 3840, 2160), Edge.Right), false, [crowded, D(R(3840, 0, 7680, 2160), R(3840, 0, 7680, 2160), 144)], (1251, 1299), "1906,-753", "1924,18..3175,1317");
        // A dock (floating, reserving nothing): Start Windows' gap above the dock's panel, centred on its button.
        var docked = D(Main, Main, 144);
        Row("dock at the bottom", docked, new(R(1300, 2040, 1360, 2100), R(1280, 2034, 2560, 2106), Edge.Bottom), false, [docked], (1251, 1299), "687,-54", "705,717..1956,2016");
        return data;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void StartHangsFromTheButtonAsTheTableSays(string name)
    {
        var (scene, size, host, start) = Rows[name];
        var placed = Placed(scene, size);
        Assert.True(host == $"{placed.Host.left},{placed.Host.top}", $"{name}: host at {placed.Host.left},{placed.Host.top}, not {host}");
        Assert.True(start == Show(placed.Start), $"{name}: Start at {Show(placed.Start)}, not {start}");
        // The box is Start's frame wherever Start ends up.
        Assert.Equal(Show(R(placed.Start.left - scene.Gap, placed.Start.top - scene.Gap, placed.Start.right + scene.Gap, placed.Start.bottom + scene.Gap)), Show(placed.Box));
    }

    [Theory]
    [InlineData(96u, 12)]
    [InlineData(120u, 15)]
    [InlineData(144u, 18)]
    [InlineData(168u, 21)]
    [InlineData(192u, 24)]
    public void WindowsGapIsTwelveDipsInTheDisplaysPixels(uint dpi, int gap) => Assert.Equal(gap, StartPlacement.Gap(dpi));

    [Fact]
    public void StartsSizeIsItsSearchBoxLessTheGapAllRound()
    {
        Assert.Equal((1251, 1299), StartPlacement.SizeOfBox(R(0, 753, 1287, 2088), 18));
        Assert.Null(StartPlacement.SizeOfBox(R(0, 0, 36, 500), 18));
    }

    // ---- Karpo's side over time: the host heard before the box, Start resizing, opened again, a display changed ----

    /// <summary>
    /// Start's size is the box's: none known before the first box, so the host heard first is left (the box comes a few ms
    /// later and places both); from then on the host goes by the last box's size, in DIPs, on any display's scale.
    /// </summary>
    [Fact]
    public void StartsSizeIsKeptInDipsFromTheLastBox()
    {
        var size = new StartSize();
        Assert.Null(size.On(144));
        Assert.Equal((1251, 1299), size.Seen(R(0, 825, 1287, 2160), 144));
        Assert.Equal((1251, 1299), size.On(144));
        Assert.Equal((834, 866), size.On(96));
        Assert.Equal((1668, 1732), size.On(192));
        // A box too small to be Start's frame says nothing, and the last one stays.
        Assert.Null(size.Seen(R(0, 0, 30, 30), 144));
        Assert.Equal((1251, 1299), size.On(144));
    }

    /// <summary>
    /// Start resizing itself (the All view, more pins): its box grows, and the host goes again so Start's top stays the
    /// gap under the bar at the top (Windows grows Start up from its host's foot); under a bar at the bottom its foot stays.
    /// </summary>
    [Fact]
    public void StartResizingItselfIsPlacedAgainFromItsNewBox()
    {
        var display = D(Main, R(0, 63, 3840, 2160), 144);
        var scene = new StartScene(display, new StartButton(R(6, 6, 57, 57), R(0, 0, 3840, 63), Edge.Top), false, [display]);
        var (host, start, _) = Placed(scene, UsersStart);
        Assert.Equal(81, start.top);
        // Taller by 300 px: Windows lays it out from the host's foot, so its top went up 300; the box says the new size.
        var taller = (UsersStart.Width, UsersStart.Height + 300);
        var grown = StartPlacement.InHost(host, scene, taller);
        Assert.Equal(81 - 300, grown.top);
        var to = At(host, StartPlacement.Host(host, scene, taller));
        var now = StartPlacement.InHost(to, scene, taller);
        Assert.Equal(81, now.top);
        Assert.Equal(81 + 1599, now.bottom);
        Assert.Null(StartPlacement.Host(to, scene, taller));
    }

    /// <summary>Opened again and again: wherever Windows or Karpo left the host, Start stands in the one place, moved once.</summary>
    [Fact]
    public void OpenedAgainFromWhereverTheHostStoodStartStandsInOnePlace()
    {
        var display = D(Main, R(0, 0, 3777, 2160), 144);
        var scene = new StartScene(display, new StartButton(R(3783, 6, 3834, 57), R(3777, 0, 3840, 2160), Edge.Right), false, [display]);
        var places = new[] { R(0, 0, 3840, 2088), R(0, 72, 3840, 2160), R(2472, -807, 6312, 1281), R(2490, -825, 6330, 1263), R(-100, 300, 3740, 2388) };
        foreach (var from in places)
        {
            var (host, start, box) = Placed(scene, UsersStart, from);
            Assert.Equal("2508,18..3759,1317", Show(start));
            Assert.Equal("2490,0..3777,1335", Show(box));
            Assert.Null(StartPlacement.Host(host, scene, UsersStart));
        }
    }

    /// <summary>A display changed (the bar moved from the right to the top, the scale up): the next hearing places Start by the display as it is now.</summary>
    [Fact]
    public void AfterADisplayChangeStartGoesByTheDisplayAsItIsNow()
    {
        var before = D(Main, R(0, 0, 3777, 2160), 144);
        var right = new StartScene(before, new StartButton(R(3783, 6, 3834, 57), R(3777, 0, 3840, 2160), Edge.Right), false, [before]);
        var (host, _, _) = Placed(right, UsersStart);
        var after = D(Main, R(0, 84, 3840, 2160), 192);
        var top = new StartScene(after, new StartButton(R(8, 8, 76, 76), R(0, 0, 3840, 84), Edge.Top), false, [after]);
        var bigger = (1668, 1732);
        var placed = At(host, StartPlacement.Host(host, top, bigger));
        Assert.Equal("24,108..1692,1840", Show(StartPlacement.InHost(placed, top, bigger)));
    }

    // ---- Properties over drawn scenes ----

    /// <summary>
    /// Over thousands of drawn scenes — one to three displays in every arrangement (negative coordinates, mixed DPI 100–200 %),
    /// a bar on any edge (a taskbar reserving its strip, or a dock floating over the desktop), the button anywhere along it,
    /// either alignment of Windows', any Start that fits, the host wherever it stood on its display: Start never overlaps the
    /// bar, never leaves its display or the room, keeps Windows' gap to the bar (±1) where nothing else holds it back, is
    /// centred on the button where the room lets it, stays on its display for Windows, its box round it, and placing again
    /// moves nothing.
    /// </summary>
    [Fact]
    public void EveryDrawnSceneKeepsStartsRules()
    {
        var random = new Random(666);
        var checkedScenes = 0;
        var gapsChecked = 0;
        for (var each = 0; each < 20_000; each++)
        {
            if (Draw(random) is not { } drawn)
            {
                continue;
            }
            var (scene, size, hostNow, reserves) = drawn;
            checkedScenes++;
            var why = $"scene {each}: display {Show(scene.Display.Bounds)} work {Show(scene.Display.Work)} dpi {scene.Display.Dpi}, "
                + $"{(scene.Button is { } b ? $"{b.Edge} bar {Show(b.Bar)} button {Show(b.Button)}" : "no button")}, centred {scene.Centred}, "
                + $"size {size}, host {Show(hostNow)}, displays {string.Join(" ", scene.Displays.Select(d => Show(d.Bounds)))}";
            var (host, start, box) = Placed(scene, size, hostNow);
            var gap = scene.Gap;
            var room = StartPlacement.Room(scene);
            // Inside the room, so on its display and off every bar that reserves its strip.
            Assert.True(start.left >= room.left && start.top >= room.top && start.right <= room.right && start.bottom <= room.bottom, $"outside the room {Show(room)}: {Show(start)}; {why}");
            // On its display for Windows: the host keeps most of itself there.
            Assert.True(StartPlacement.Keeps(host, scene), $"host {Show(host)} not kept; {why}");
            // The box is Start's frame.
            Assert.Equal(Show(R(start.left - gap, start.top - gap, start.right + gap, start.bottom + gap)), Show(box));
            // Placed again (heard again, Windows placing it again): nothing moves.
            Assert.Null(StartPlacement.Host(host, scene, size));
            Assert.Null(StartPlacement.Box(box, host, scene));
            if (scene.Button is not { } button)
            {
                continue;
            }
            var bar = button.Bar;
            // Never over the bar.
            Assert.False(start.left < bar.right && bar.left < start.right && start.top < bar.bottom && bar.top < start.bottom, $"over the bar: {Show(start)}; {why}");
            var target = StartPlacement.Target(scene, size);
            var whole = start.left == target.left && start.top == target.top;
            if (!whole)
            {
                // Held back by another display: only ever short of the target, never past it.
                Assert.True(scene.Displays.Count > 1, $"short of {Show(target)} on one display: {Show(start)}; {why}");
                continue;
            }
            // Windows' gap to the bar's inner side, where the room does not hold it further off.
            var across = button.Edge switch
            {
                Edge.Top => start.top - Math.Max(bar.bottom, scene.Display.Work.top),
                Edge.Left => start.left - Math.Max(bar.right, scene.Display.Work.left),
                Edge.Right => Math.Min(bar.left, scene.Display.Work.right) - start.right,
                _ => Math.Min(bar.top, scene.Display.Work.bottom) - start.bottom,
            };
            var heldOff = button.Edge switch
            {
                Edge.Top => start.top == room.top && bar.bottom + gap < room.top,
                Edge.Left => start.left == room.left && bar.right + gap < room.left,
                Edge.Right => start.right == room.right && bar.left - gap > room.right,
                _ => start.bottom == room.bottom && bar.top - gap > room.bottom,
            };
            if (reserves || !heldOff)
            {
                Assert.True(Math.Abs(across - gap) <= 1, $"{across} px from the bar, not {gap}; {why}");
                gapsChecked++;
            }
            // Centred on the button along the bar, where the room lets it.
            var vertical = button.Edge is Edge.Left or Edge.Right;
            var (startMid, buttonMid) = vertical
                ? ((start.top + start.bottom) / 2, (button.Button.top + button.Button.bottom) / 2)
                : ((start.left + start.right) / 2, (button.Button.left + button.Button.right) / 2);
            var atEnd = vertical ? start.top == room.top || start.bottom == room.bottom : start.left == room.left || start.right == room.right;
            Assert.True(atEnd || Math.Abs(startMid - buttonMid) <= 1, $"middle {startMid}, the button's {buttonMid}; {why}");
        }
        Assert.True(checkedScenes > 10_000, $"only {checkedScenes} scenes drawn");
        Assert.True(gapsChecked > 5_000, $"Windows' gap checked in only {gapsChecked} scenes");
    }

    private static readonly uint[] Dpis = [96, 120, 144, 168, 192];

    /// <summary>One scene: displays, the bar, the button, Windows' alignment, Start's size, where the host stands; null where Start would not fit.</summary>
    private static (StartScene Scene, (int Width, int Height) Size, RECT Host, bool Reserves)? Draw(Random random)
    {
        var dpi = Dpis[random.Next(Dpis.Length)];
        int Px(int dip) => (int)(dip * (long)dpi / 96);
        var (width, height) = random.Next(3) switch { 0 => (1920, 1080), 1 => (2560, 1440), _ => (3840, 2160) };
        if (random.Next(5) == 0)
        {
            (width, height) = (height, width);
        }
        var origin = (X: random.Next(-2, 3) * 1000 + random.Next(-500, 500), Y: random.Next(-2, 3) * 700 + random.Next(-300, 300));
        var bounds = R(origin.X, origin.Y, origin.X + width, origin.Y + height);
        var edge = (Edge)random.Next(4);
        var thick = Px(random.Next(32, 81));
        var reserves = random.Next(4) != 0;
        RECT bar, work = bounds;
        if (reserves)
        {
            bar = edge switch
            {
                Edge.Top => R(bounds.left, bounds.top, bounds.right, bounds.top + thick),
                Edge.Left => R(bounds.left, bounds.top, bounds.left + thick, bounds.bottom),
                Edge.Right => R(bounds.right - thick, bounds.top, bounds.right, bounds.bottom),
                _ => R(bounds.left, bounds.bottom - thick, bounds.right, bounds.bottom),
            };
            work = edge switch
            {
                Edge.Top => R(bounds.left, bar.bottom, bounds.right, bounds.bottom),
                Edge.Left => R(bar.right, bounds.top, bounds.right, bounds.bottom),
                Edge.Right => R(bounds.left, bounds.top, bar.left, bounds.bottom),
                _ => R(bounds.left, bounds.top, bounds.right, bar.top),
            };
        }
        else
        {
            // A dock: a panel a third to two thirds of the edge long, a margin off it, reserving nothing.
            var margin = Px(random.Next(4, 16));
            var vertical = edge is Edge.Left or Edge.Right;
            var length = (vertical ? height : width) * random.Next(33, 67) / 100;
            var from = (vertical ? bounds.top : bounds.left) + random.Next(0, (vertical ? height : width) - length);
            bar = edge switch
            {
                Edge.Top => R(from, bounds.top + margin, from + length, bounds.top + margin + thick),
                Edge.Left => R(bounds.left + margin, from, bounds.left + margin + thick, from + length),
                Edge.Right => R(bounds.right - margin - thick, from, bounds.right - margin, from + length),
                _ => R(from, bounds.bottom - margin - thick, from + length, bounds.bottom - margin),
            };
        }
        var side = Px(random.Next(32, 49));
        var vert = edge is Edge.Left or Edge.Right;
        var along = random.Next(0, (vert ? bar.bottom - bar.top : bar.right - bar.left) - side);
        var button = vert ? R(bar.left + (thick - side) / 2, bar.top + along, bar.left + (thick - side) / 2 + side, bar.top + along + side)
            : R(bar.left + along, bar.top + (thick - side) / 2, bar.left + along + side, bar.top + (thick - side) / 2 + side);
        var display = D(bounds, work, dpi);
        // Others beside it, above or below, at their own scales; never overlapping it.
        var displays = new List<StartDisplay> { display };
        for (var other = random.Next(3); other > 0; other--)
        {
            var (w, h) = random.Next(2) == 0 ? (1920, 1080) : (3840, 2160);
            var place = random.Next(4);
            var shift = random.Next(-h / 2, h / 2);
            var rect = place switch
            {
                0 => R(bounds.right, bounds.top + shift, bounds.right + w, bounds.top + shift + h),
                1 => R(bounds.left - w, bounds.top + shift, bounds.left, bounds.top + shift + h),
                2 => R(bounds.left + shift, bounds.top - h, bounds.left + shift + w, bounds.top),
                _ => R(bounds.left + shift, bounds.bottom, bounds.left + shift + w, bounds.bottom + h),
            };
            if (displays.All(d => StartPlacement.Overlap(d.Bounds, rect) == 0))
            {
                displays.Add(D(rect, rect, Dpis[random.Next(Dpis.Length)]));
            }
        }
        var gap = StartPlacement.Gap(dpi);
        var size = (Px(random.Next(320, 900)), Px(random.Next(300, 1000)));
        var roomWidth = work.right - work.left - 2 * gap;
        var roomHeight = work.bottom - work.top - 2 * gap;
        // Start fits beside the bar (a dock reserves nothing, so Start fits between it and the far end too).
        var besideWidth = edge switch { Edge.Left => work.right - bar.right - 2 * gap, Edge.Right => bar.left - work.left - 2 * gap, _ => roomWidth };
        var besideHeight = edge switch { Edge.Top => work.bottom - bar.bottom - 2 * gap, Edge.Bottom => bar.top - work.top - 2 * gap, _ => roomHeight };
        if (size.Item1 > Math.Min(roomWidth, besideWidth) || size.Item2 > Math.Min(roomHeight, besideHeight))
        {
            return null;
        }
        var scene = new StartScene(display, random.Next(10) == 0 ? null : new StartButton(button, bar, edge), random.Next(2) == 0, displays);
        // The host as Windows places it, or wherever an opening before left it on this display.
        var host = WindowsStart.Host(display);
        if (random.Next(2) == 0)
        {
            var (dx, dy) = (random.Next(-width / 3, width / 3), random.Next(-height / 3, height / 3));
            host = R(host.left + dx, host.top + dy, host.right + dx, host.bottom + dy);
        }
        return (scene, size, host, reserves);
    }
}
