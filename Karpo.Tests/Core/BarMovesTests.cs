// §534: "Lock the taskbar" and a bar dragged to another edge of its display (Karpo.Core\BarMoves.cs): the edge nearest
// the pointer, held to the bar's own display, at any scale; the system's drag threshold; what the lock stops, and
// what it leaves to edit mode; the one setting, read, written, round-tripped, and both its homes.

using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Core;

public sealed class BarMovesTests
{
    /// <summary>A 1920 by 1080 display at the origin.</summary>
    private static readonly RectI Main = new(0, 0, 1920, 1080);

    // --- The nearest edge -------------------------------------------------------------------

    [Theory]
    [InlineData(960, 1070, Edge.Bottom)]
    [InlineData(960, 10, Edge.Top)]
    [InlineData(10, 540, Edge.Left)]
    [InlineData(1910, 540, Edge.Right)]
    // Up the display from a bottom bar: past its middle the top is nearer.
    [InlineData(960, 539, Edge.Top)]
    [InlineData(960, 541, Edge.Bottom)]
    // Out from the middle of a wide display the top or the bottom is nearer than either side.
    [InlineData(700, 400, Edge.Top)]
    [InlineData(300, 540, Edge.Left)]
    public void ThePointerPicksTheNearestEdge(int x, int y, Edge edge) => Assert.Equal(edge, BarMoves.Nearest(Main, x, y, Edge.Bottom));

    /// <summary>At a corner two edges are as near: the one the bar stands on stays, else the first of Bottom, Top, Left, Right.</summary>
    [Fact]
    public void AtACornerTheBarStaysWhereItIsOrTakesTheFirstEdge()
    {
        Assert.Equal(Edge.Left, BarMoves.Nearest(Main, 0, 0, Edge.Left));
        Assert.Equal(Edge.Top, BarMoves.Nearest(Main, 0, 0, Edge.Top));
        Assert.Equal(Edge.Top, BarMoves.Nearest(Main, 0, 0, Edge.Bottom));
        Assert.Equal(Edge.Top, BarMoves.Nearest(Main, 0, 0, Edge.Right));
        Assert.Equal(Edge.Bottom, BarMoves.Nearest(Main, 1919, 1079, Edge.Top));
        Assert.Equal(Edge.Right, BarMoves.Nearest(Main, 1919, 1079, Edge.Right));
        Assert.Equal(Edge.Bottom, BarMoves.Nearest(Main, 0, 1079, Edge.Top));
    }

    /// <summary>
    /// Each display has its own bar: a pointer over another display picks the edge of the bar's own display nearest it —
    /// a display left of the main one (negative coordinates), one right of it, one above.
    /// </summary>
    [Fact]
    public void APointerOverAnotherDisplayPicksAnEdgeOfTheBarsOwn()
    {
        var left = new RectI(-2560, 0, 0, 1440);
        Assert.Equal(Edge.Right, BarMoves.Nearest(left, 500, 700, Edge.Bottom));
        Assert.Equal(Edge.Left, BarMoves.Nearest(Main, -800, 540, Edge.Bottom));
        Assert.Equal(Edge.Top, BarMoves.Nearest(Main, 960, -300, Edge.Bottom));
        // On its own display, in its own coordinates.
        Assert.Equal(Edge.Left, BarMoves.Nearest(left, -2500, 700, Edge.Bottom));
        Assert.Equal(Edge.Top, BarMoves.Nearest(left, -1280, 50, Edge.Bottom));
    }

    /// <summary>The same place on the same display at 100 %, 150 % and 200 % (its px scaled): the same edge.</summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void TheEdgeIsTheSameAtEveryScale(float scale)
    {
        RectI Scaled(RectI r) => new((int)(r.Left * scale), (int)(r.Top * scale), (int)(r.Right * scale), (int)(r.Bottom * scale));
        var display = Scaled(new RectI(-1920, 0, 0, 1080));
        (int, int) At(int x, int y) => ((int)(x * scale), (int)(y * scale));
        foreach (var ((x, y), edge) in new[] { (At(-960, 1000), Edge.Bottom), (At(-960, 100), Edge.Top), (At(-1850, 540), Edge.Left), (At(-60, 540), Edge.Right) })
        {
            Assert.Equal(edge, BarMoves.Nearest(display, x, y, Edge.Bottom));
        }
    }

    [Fact]
    public void ADisplayWithNoRoomKeepsTheEdge() => Assert.Equal(Edge.Left, BarMoves.Nearest(new RectI(0, 0, 0, 0), 5, 5, Edge.Left));

    // --- Where it lands ----------------------------------------------------------------------

    [Fact]
    public void TheLandingIsTheStripAlongTheEdge()
    {
        var display = new RectI(-2560, 0, 0, 1440);
        Assert.Equal(new RectI(-2560, 1368, 0, 1440), BarMoves.Landing(display, Edge.Bottom, 72));
        Assert.Equal(new RectI(-2560, 0, 0, 72), BarMoves.Landing(display, Edge.Top, 72));
        Assert.Equal(new RectI(-2560, 0, -2488, 1440), BarMoves.Landing(display, Edge.Left, 72));
        Assert.Equal(new RectI(-72, 0, 0, 1440), BarMoves.Landing(display, Edge.Right, 72));
        // Never thicker than the display.
        Assert.Equal(display, BarMoves.Landing(display, Edge.Top, 5000));
    }

    // --- The drag ----------------------------------------------------------------------------

    /// <summary>
    /// Inside the system's drag threshold a press stays a press (its release a click); a move of more than SM_CXDRAG or
    /// SM_CYDRAG carries the bar, to the edge nearest the pointer, and every step after says whether its landing changed.
    /// </summary>
    [Fact]
    public void PastTheDragThresholdTheBarIsCarried()
    {
        var move = new BarMove(Main, Edge.Bottom, 960, 1060);
        Assert.False(move.Step(964, 1056, 4, 4));
        Assert.False(move.Moving);
        Assert.Equal(Edge.Bottom, move.Target);
        Assert.True(move.Step(965, 1060, 4, 4));
        Assert.True(move.Moving);
        Assert.Equal(Edge.Bottom, move.Target);
        Assert.False(move.Step(960, 900, 4, 4));
        Assert.True(move.Step(960, 200, 4, 4));
        Assert.Equal(Edge.Top, move.Target);
        Assert.False(move.Step(900, 100, 4, 4));
        Assert.True(move.Step(20, 500, 4, 4));
        Assert.Equal(Edge.Left, move.Target);
        Assert.Equal(Edge.Bottom, move.From);
    }

    /// <summary>A move straight up past the threshold's height alone carries it; the threshold is each axis's own.</summary>
    [Fact]
    public void EachAxisHasItsOwnThreshold()
    {
        var move = new BarMove(Main, Edge.Bottom, 960, 1060);
        Assert.False(move.Step(962, 1052, 2, 8));
        Assert.True(move.Step(960, 1051, 2, 8));
        var across = new BarMove(Main, Edge.Bottom, 960, 1060);
        Assert.True(across.Step(963, 1060, 2, 8));
    }

    // --- The lock ----------------------------------------------------------------------------

    /// <summary>
    /// Locked, nothing carries the bar, drags its height or §663 carries a widget at rest; in edit mode the grip and the
    /// widgets still do, and the room still selects.
    /// </summary>
    [Fact]
    public void TheLockStopsMovingAndSizingAtRestOnly()
    {
        Assert.False(BarMoves.Carries(locked: true, editing: false));
        Assert.True(BarMoves.Carries(locked: false, editing: false));
        Assert.False(BarMoves.Carries(locked: false, editing: true));
        Assert.False(BarMoves.Carries(locked: true, editing: true));

        Assert.False(BarMoves.Arranges(locked: true, editing: false));
        Assert.True(BarMoves.Arranges(locked: false, editing: false));
        Assert.True(BarMoves.Arranges(locked: true, editing: true));
        Assert.True(BarMoves.Arranges(locked: false, editing: true));
    }

    /// <summary>As Windows ships its taskbar: locked in a new config and in a file that does not say.</summary>
    [Fact]
    public void ANewConfigIsLocked()
    {
        Assert.True(new Config().LockTaskbar);
        Assert.True(Configs.Repaired("{}").LockTaskbar);
    }

    [Fact]
    public void TheLockRoundTrips()
    {
        var config = new Config();
        Assert.True(BarMoves.WriteLock(config, false));
        Assert.False(BarMoves.WriteLock(config, false));
        Assert.Contains("\"lockTaskbar\": false", config.ToJson(), StringComparison.Ordinal);
        Assert.False(Configs.RoundTrip(config, out _).LockTaskbar);
        Assert.True(BarMoves.WriteLock(config, true));
        Assert.True(Configs.RoundTrip(config, out _).LockTaskbar);
    }

    /// <summary>The default menu, which has "Lock the taskbar" again (§663; §581 to §662 it had to be added).</summary>
    private static List<BarMenuEntry> WithLock() => BarMenus.Default();

    /// <summary>
    /// The bar's right click: §663 "Lock the taskbar" in the default, between Task Manager and Taskbar settings, ruled off
    /// from Task Manager as Windows 10's was; ticked while locked, its command the dock's own.
    /// </summary>
    [Fact]
    public void TheMenusRowIsTickedWhileLocked()
    {
        var locked = BarMenus.Lines(WithLock(), [], windowsTerminal: true, _ => new TargetCheck(TargetState.Here, ""), locked: true);
        var unlocked = BarMenus.Lines(WithLock(), [], windowsTerminal: true, _ => new TargetCheck(TargetState.Here, ""), locked: false);
        Assert.Equal(["Task Manager", "Lock the taskbar", "Taskbar settings"], locked.Where(line => line.Kind == BarMenuLine.Row).Select(line => line.Text));
        Assert.Equal(
            [BarMenuLine.Row, BarMenuLine.Divider, BarMenuLine.Row, BarMenuLine.Row],
            locked.Select(line => line.Kind));
        Assert.True(locked.Single(line => line.Entry?.Action == BarMenuAction.LockTaskbar).Checked);
        Assert.False(unlocked.Single(line => line.Entry?.Action == BarMenuAction.LockTaskbar).Checked);
        Assert.All(locked.Where(line => line.Entry?.Action != BarMenuAction.LockTaskbar), line => Assert.Null(line.Checked));

        foreach (var on in new[] { true, false })
        {
            var row = TheDock.BarRows(WithLock(), [], windowsTerminal: true, Karpo.Dock.ShortcutLooks.None, locked: on).Items
                .Single(item => item.Text == S.CoreBarMenuLockTaskbar);
            Assert.Equal((TheDock.CMD_LOCK_TASKBAR, on), (row.Id, row.Checked));
        }
    }

    /// <summary>
    /// The Taskbar page's switch is the same setting: on while locked, under "Bar" after the bar's own rows, and set from it
    /// the menu's row is ticked or not; the right click's row flips it (the dock's ToggleLock writes what WriteLock does).
    /// </summary>
    [Fact]
    public void ThePagesSwitchAndTheMenusRowAreOneSetting()
    {
        var config = new Config();
        config.EditScreen("\\\\.\\DISPLAY1", "One");
        var bar = new BarSpot("\\\\.\\DISPLAY1", "One");
        var rows = BarLooks.TaskbarRows(config, bar);
        var keys = rows.Select(row => row.Key).ToList();
        var row = rows.Single(row => row.Key == BarMoves.LockRowKey);
        Assert.Equal(OptionRowKind.Toggle, row.Kind);
        Assert.Equal(S.CoreBarMenuLockTaskbar, row.Title);
        Assert.True(row.On);
        Assert.True(keys.IndexOf(BarMoves.LockRowKey) < keys.IndexOf(BarLooks.BehaviourHeading));
        Assert.True(keys.IndexOf(BarMoves.LockRowKey) > keys.IndexOf(BarLooks.ShowRow));

        Assert.True(row.Set!(0));
        Assert.False(config.LockTaskbar);
        Assert.False(BarLooks.TaskbarRows(config, bar).Single(each => each.Key == BarMoves.LockRowKey).On);
        Assert.False(BarMenus.Lines(WithLock(), [], true, _ => new TargetCheck(TargetState.Here, ""), locked: config.LockTaskbar)
            .Single(line => line.Entry?.Action == BarMenuAction.LockTaskbar).Checked);

        // The right click's row: the other way from what it shows.
        Assert.True(BarMoves.WriteLock(config, !config.LockTaskbar));
        Assert.True(config.LockTaskbar);
    }

    /// <summary>The edge the drag writes is the one the Display page's Position writes: one value, the display's.</summary>
    [Fact]
    public void TheDropWritesThePositionsEdge()
    {
        var config = new Config();
        var bar = new BarSpot("\\\\.\\DISPLAY1", "One");
        Assert.True(BarLooks.WriteEdge(config, bar, Edge.Top));
        Assert.Equal(Edge.Top, config.EdgeOfScreen(bar.Display));
        var position = BarLooks.Position(config, bar);
        Assert.Equal(Array.IndexOf(BarLooks.Edges, Edge.Top), position.Chosen);
        Assert.Equal(Edge.Bottom, config.EdgeOfScreen("\\\\.\\DISPLAY2"));
    }
}
