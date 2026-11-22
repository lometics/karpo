// edit: where the display's edit bar stands (Karpo.Dock\Dock.EditDeck.cs, EditDeckPlan): never over widgets or bars,
// across from them. Held here, for every layout type on every edge, its bars shown all the time or on hover with their
// places brought in: centred, on the side across from the bars (§296: at the bottom under bars along the top alone, at the
// top otherwise), 8 DIPs off what stands along that edge (past the deepest of it), or off the display's edge with nothing
// there; inside the display; over no bar and no place. What it opens opens away from that side; §520 its settings panel
// hangs from where it hangs (SettingsPanelTests).

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed class EditDeckPlanTests
{
    /// <summary>150 %, as the person's displays are.</summary>
    private const float Scale = 1.5f;

    /// <summary>A display 1920 by 1080 DIPs at 150 %.</summary>
    private static readonly RectI Display = new(0, 0, 2880, 1620);

    /// <summary>A 48 DIP bar at 150 %.</summary>
    private const int Depth = 72;

    /// <summary>An edit bar at 150 %, wider than its words make it: any width stands the same way.</summary>
    private const int Wide = 1200;

    /// <summary>Its height: <see cref="EditDeckPlan.BarDip"/> at 150 %.</summary>
    private const int Tall = 72;

    /// <summary><see cref="EditDeckPlan.MarginDip"/> at 150 %.</summary>
    private const int Margin = 12;

    /// <summary>
    /// The windows of a layout of <paramref name="type"/> along <paramref name="edge"/> in edit mode,
    /// each by its panel and the room off its edge, as the dock hands them over (Dock.DeckRoom): a bar
    /// shown all the time along its whole edge; on hover, its three places brought in, the middle one
    /// deeper than the others (a taller widget in it); a dock's three docks, floating: one window
    /// along the edge, or three places on hover.
    /// </summary>
    private static List<(Edge Edge, RectI At)> Bars(LayoutType type, Edge edge, bool hover)
    {
        var bars = new List<(Edge, RectI)>();
        var vertical = edge is Edge.Left or Edge.Right;
        var length = vertical ? Display.Height : Display.Width;
        if (type == LayoutType.Dock && hover)
        {
            // Each of its three docks a place of its own, each the lift deeper than a bar.
            bars.Add((edge, Strip(edge, 0, 400, Depth + 12)));
            bars.Add((edge, Strip(edge, (length / 2) - 450, (length / 2) + 450, Depth + 12)));
            bars.Add((edge, Strip(edge, length - 500, length, Depth + 12)));
        }
        else if (type == LayoutType.Dock)
        {
            // One window along the whole edge, its three docks in it.
            bars.Add((edge, Strip(edge, 0, length, Depth + 12)));
        }
        else if (hover)
        {
            bars.Add((edge, Strip(edge, 0, 500, Depth)));
            bars.Add((edge, Strip(edge, (length / 2) - 350, (length / 2) + 350, Depth + 24)));
            bars.Add((edge, Strip(edge, length - 600, length, Depth)));
        }
        else
        {
            bars.Add((edge, Strip(edge, 0, length, Depth)));
        }
        return bars;
    }

    /// <summary>A window against <paramref name="edge"/> from <paramref name="from"/> to <paramref name="to"/> along it, <paramref name="depth"/> px deep.</summary>
    private static RectI Strip(Edge edge, int from, int to, int depth) => edge switch
    {
        Edge.Top => new RectI(from, Display.Top, to, Display.Top + depth),
        Edge.Left => new RectI(Display.Left, from, Display.Left + depth, to),
        Edge.Right => new RectI(Display.Right - depth, from, Display.Right, to),
        _ => new RectI(from, Display.Bottom - depth, to, Display.Bottom),
    };

    private static bool Overlaps(RectI a, RectI b) =>
        Math.Min(a.Right, b.Right) > Math.Max(a.Left, b.Left) && Math.Min(a.Bottom, b.Bottom) > Math.Max(a.Top, b.Top);

    public static TheoryData<LayoutType, Edge, bool> Layouts()
    {
        var data = new TheoryData<LayoutType, Edge, bool>();
        foreach (var type in LayoutTypes.All)
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
            {
                data.Add(type, edge, false);
                data.Add(type, edge, true);
            }
        }
        return data;
    }

    /// <summary>The depth, px from the display's top, of what stands along its top edge: its deepest window there, 0 with none.</summary>
    internal static int TopDepth(RectI display, IEnumerable<(Edge Edge, RectI At)> bars) =>
        bars.Where(bar => bar.Edge == Edge.Top).Select(bar => bar.At.Bottom - display.Top).DefaultIfEmpty(0).Max();

    /// <summary>The depth, px from the display's bottom, of what stands along its bottom edge: its deepest window there, 0 with none.</summary>
    internal static int BottomDepth(RectI display, IEnumerable<(Edge Edge, RectI At)> bars) =>
        bars.Where(bar => bar.Edge == Edge.Bottom).Select(bar => display.Bottom - bar.At.Top).DefaultIfEmpty(0).Max();

    /// <summary>What the edit bar of a display showing <paramref name="bars"/> stands by, as the dock hands it over (Dock.DeckRoom).</summary>
    internal static EditDeckRoom Room(RectI display, IReadOnlyCollection<(Edge Edge, RectI At)> bars) =>
        new(
            display,
            TopDepth(display, bars),
            BottomDepth(display, bars),
            bars.Where(bar => bar.Edge == Edge.Left).Select(bar => bar.At.Right - display.Left).DefaultIfEmpty(0).Max(),
            bars.Where(bar => bar.Edge == Edge.Right).Select(bar => display.Right - bar.At.Left).DefaultIfEmpty(0).Max());

    /// <summary>
    /// §343: the edit bar, wider now, keeps clear of a bar along a side by its window's room round its
    /// panel (FlyoutHost.RoomDip, which takes the pointer): centred on the display where that leaves
    /// the room, pushed along off the side bar where not; with nothing at the sides, centred as ever.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void ItKeepsClearOfTheBarsAtTheSides(float scale)
    {
        var clear = (int)MathF.Round(FlyoutHost.RoomDip * scale, MidpointRounding.AwayFromZero);
        var display = new RectI(0, 0, (int)(1280 * scale), (int)(720 * scale));
        var wide = (int)(1100 * scale);
        var side = (int)(120 * scale);
        var alone = EditDeckPlan.Place(new EditDeckRoom(display, 0), wide, Tall, scale).Panel;
        Assert.Equal((display.Width - wide) / 2, alone.Left);
        var left = EditDeckPlan.Place(new EditDeckRoom(display, 0, 0, side), wide, Tall, scale).Panel;
        Assert.Equal(side + clear, left.Left);
        var right = EditDeckPlan.Place(new EditDeckRoom(display, 0, 0, 0, side), wide, Tall, scale).Panel;
        Assert.Equal(display.Right - side - clear, right.Right);
        // Room enough beside a narrow side bar: centred still.
        var narrow = EditDeckPlan.Place(new EditDeckRoom(display, 0, 0, 10, 10), (int)(600 * scale), Tall, scale).Panel;
        Assert.Equal((display.Width - (int)(600 * scale)) / 2, narrow.Left);
    }

    /// <summary>
    /// Where the edit bar <paramref name="at"/> must stand by <paramref name="room"/>: <paramref name="gap"/>
    /// under what stands along the top, or over the display's bottom when it stands there (§296).
    /// </summary>
    internal static int Off(EditDeckRoom room, RectI at, int gap) =>
        room.Side == Edge.Bottom ? room.Display.Bottom - room.BottomDepth - gap - at.Bottom : at.Top - (room.Display.Top + room.TopDepth + gap);

    /// <summary>
    /// For every layout type on every edge, its bars shown all the time or on hover: the edit bar is
    /// centred on the display, 8 DIPs off the deepest of what stands along the side it stands at,
    /// inside the display 8 DIPs from its sides, and over no bar and no place of any edge.
    /// </summary>
    [Theory]
    [MemberData(nameof(Layouts))]
    public void TheEditBarIsCentredOverNothing(LayoutType type, Edge edge, bool hover)
    {
        var bars = Bars(type, edge, hover);
        var room = Room(Display, bars);
        var at = EditDeckPlan.Place(room, Wide, Tall, Scale).Panel;

        Assert.Equal((Wide, Tall), (at.Width, at.Height));
        Assert.Equal((Display.Width - Wide) / 2, at.Left - Display.Left);
        Assert.Equal(0, Off(room, at, Margin));
        Assert.All(bars, bar => Assert.False(Overlaps(at, bar.At), $"the edit bar {at} covers the {bar.Edge} window {bar.At}"));
        Assert.True(at.Left >= Display.Left + Margin && at.Right <= Display.Right - Margin && at.Top >= Display.Top + Margin && at.Bottom <= Display.Bottom - Margin, $"the edit bar {at} leaves the display");
    }

    /// <summary>
    /// The person, 2026-10-23: "when the taskbar is shown on the top the edit bar should be shown on the
    /// bottom" (§296). The edit bar stands across from the bars: at the bottom under a Taskbar or a Dock
    /// along the top, shown all the time or on hover, 8 DIPs over the display's bottom; at the top over
    /// one along the bottom and beside one along a side, 8 DIPs under the display's top.
    /// </summary>
    [Theory]
    [InlineData(LayoutType.Taskbar, Edge.Top, false, Edge.Bottom, 0)]
    [InlineData(LayoutType.Taskbar, Edge.Top, true, Edge.Bottom, 0)]
    [InlineData(LayoutType.Dock, Edge.Top, false, Edge.Bottom, 0)]
    [InlineData(LayoutType.Dock, Edge.Top, true, Edge.Bottom, 0)]
    [InlineData(LayoutType.Taskbar, Edge.Bottom, false, Edge.Top, 0)]
    [InlineData(LayoutType.Dock, Edge.Bottom, true, Edge.Top, 0)]
    [InlineData(LayoutType.Taskbar, Edge.Left, false, Edge.Top, 0)]
    [InlineData(LayoutType.Dock, Edge.Right, true, Edge.Top, 0)]
    public void TheEditBarStandsAcrossFromTheBars(LayoutType type, Edge edge, bool hover, Edge side, int under)
    {
        var bars = Bars(type, edge, hover);
        var room = Room(Display, bars);
        var spot = EditDeckPlan.Place(room, Wide, Tall, Scale);

        Assert.Equal(side, room.Side);
        Assert.Equal(side, spot.Edge);
        if (side == Edge.Bottom)
        {
            Assert.Equal(Display.Bottom - Margin, spot.Panel.Bottom);
            Assert.Equal(Display.Bottom, spot.Anchor.y);
        }
        else
        {
            Assert.Equal(Display.Top + under + Margin, spot.Panel.Top);
            Assert.Equal(Display.Top + under, spot.Anchor.y);
        }
    }

    /// <summary>
    /// At every scale the edit bar stands the same way: its margin is <see cref="EditDeckPlan.MarginDip"/>
    /// at that scale, off the deepest window of its side, centred, and inside the display.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2.25f)]
    public void TheMarginScalesWithTheDisplay(float scale)
    {
        var gap = (int)MathF.Round(EditDeckPlan.MarginDip * scale, MidpointRounding.AwayFromZero);
        foreach (var (type, edge) in new[] { (LayoutType.Taskbar, Edge.Top), (LayoutType.Taskbar, Edge.Bottom), (LayoutType.Dock, Edge.Left) })
        {
            var room = Room(Display, Bars(type, edge, hover: false));
            var at = EditDeckPlan.Place(room, Wide, Tall, scale).Panel;

            Assert.Equal(0, Off(room, at, gap));
            Assert.Equal((Display.Width - Wide) / 2, at.Left - Display.Left);
            Assert.True(at.Left >= Display.Left + gap && at.Right <= Display.Right - gap && at.Top >= Display.Top + gap && at.Bottom <= Display.Bottom - gap);
        }
    }

    /// <summary>
    /// What the edit bar opens (the add list, the tooltips) hangs from the side the edit bar stands at,
    /// so it drops down from one at the top and rises from one at the bottom, never over it or off the
    /// display. Regression: the add list was given the bottom and opened upward, off the top of the
    /// display.
    /// </summary>
    [Fact]
    public void ItsListsOpenAwayFromItsSide()
    {
        Assert.Equal(Edge.Top, EditDeckPlan.Place(new EditDeckRoom(Display, 0), Wide, Tall, Scale).Edge);
        Assert.Equal(Edge.Top, EditDeckPlan.Place(new EditDeckRoom(Display, 0, Depth), Wide, Tall, Scale).Edge);
        Assert.Equal(Edge.Bottom, EditDeckPlan.Place(new EditDeckRoom(Display, Depth), Wide, Tall, Scale).Edge);
        Assert.Equal(Edge.Top, EditDeckPlan.Place(new EditDeckRoom(Display, Depth, Depth), Wide, Tall, Scale).Edge);
    }

    /// <summary>
    /// With nothing at the top it stands 8 DIPs below the display's top; an edit bar longer than the
    /// display starts at its left rather than past it; and one pushed down by what stands above it
    /// stays inside the display, clamped above its bottom margin.
    /// </summary>
    [Fact]
    public void WithNothingAtTheTopItStandsAtTheTopAndStaysInside()
    {
        var alone = EditDeckPlan.Place(new EditDeckRoom(Display, 0), Wide, Tall, Scale).Panel;
        Assert.Equal((Display.Left + ((Display.Width - Wide) / 2), Display.Top + Margin), (alone.Left, alone.Top));

        var narrow = new RectI(0, 0, 900, 1620);
        var long_ = EditDeckPlan.Place(new EditDeckRoom(narrow, 0), Wide, Tall, Scale).Panel;
        Assert.Equal(narrow.Left + Margin, long_.Left);

        var deep = EditDeckPlan.Place(new EditDeckRoom(Display, Display.Height, Depth), Wide, Tall, Scale).Panel;
        Assert.Equal(Display.Bottom - Margin, deep.Bottom);
        Assert.True(deep.Top >= Display.Top + Margin);
    }

    /// <summary>A high contrast look: black, white text, Windows' cyan highlight.</summary>
    internal static Look HighContrast => AddListPictures.LookFor(true) with
    {
        HighContrast = true,
        HcWindow = Windows.UI.Color.FromArgb(255, 0, 0, 0),
        HcText = Windows.UI.Color.FromArgb(255, 255, 255, 255),
        HcHighlight = Windows.UI.Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText = Windows.UI.Color.FromArgb(255, 0, 0, 0),
        Animations = false,
    };
}
