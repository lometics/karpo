// How each edge of a display shows, as the layout it shows has it on that display (TaskbarLayout.Edges): an edge the
// layout has a bar on shows all the time and holds its strip as Windows' taskbar does, shows all the
// time over the windows without holding any, or comes in only when the pointer comes near it; it is
// a floating dock or a bar in islands, at the layout's size. Every other edge has nothing. Never in
// the file: the layout says it all (TaskbarLayouts.cs).

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// How a bar of a layout shows (<see cref="TaskbarLayout.Show"/>). A word this build does not know
/// reads as a value outside the enum, and <see cref="Config.Repair"/> makes it <see cref="Reserve"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<EdgeMode>))]
public enum EdgeMode
{
    /// <summary>"Always, reserve space": always in sight, holding its strip, so maximized windows stop at it.</summary>
    [JsonStringEnumMemberName("reserve")] Reserve,

    /// <summary>"Always, over windows": always in sight, over the windows, holding no strip.</summary>
    [JsonStringEnumMemberName("float")] Float,

    /// <summary>"When the pointer comes near": out of sight, and in when the pointer reaches the edge.</summary>
    [JsonStringEnumMemberName("hover")] Hover,
}

/// <summary>What an edge of a display holds: nothing, a floating dock, or a bar whose widgets are islands.</summary>
public enum EdgeLayout
{
    /// <summary>No bar on the edge.</summary>
    None,

    /// <summary>Floating docks along the edge, one per section that holds something, apart from each other: a Dock layout's.</summary>
    Dock,

    /// <summary>A bar along the whole edge, its sections at the start, the centre and the end, each widget an island.</summary>
    Taskbar,
}

/// <summary>What each kind of bar wears.</summary>
public static class EdgeLayouts
{
    /// <summary>The look a bar of <paramref name="layout"/> is laid out in: a dock floats, a bar wears islands. None has no bar, and reads as islands.</summary>
    public static BarStyle Style(this EdgeLayout layout) => layout == EdgeLayout.Dock ? BarStyle.Floating : BarStyle.Islands;
}

/// <summary>One edge of a display: how it shows, how thick it is and what it holds (<see cref="EdgeLayout"/>).</summary>
public sealed class EdgeBar
{
    public EdgeMode Mode { get; set; } = EdgeMode.Hover;

    /// <summary>The bar's thickness in DIPs, <see cref="Config.MinHeight"/> to <see cref="Config.MaxHeight"/>; 48 is Windows 11's taskbar's.</summary>
    public int Size { get; set; } = Config.DefaultHeight;

    /// <summary>What the edge holds, whatever its mode.</summary>
    public EdgeLayout Layout { get; set; }

    /// <summary>§345: the layout its bar is of, whose look and app buttons it wears (<see cref="Config.WithEdge"/>); null with no bar.</summary>
    public TaskbarLayout? Of { get; set; }

    /// <summary>The look its bar is laid out in: its layout's (<see cref="EdgeLayouts.Style"/>).</summary>
    public BarStyle Style => Layout.Style();

    /// <summary>Whether the edge has a bar at all: any layout but <see cref="EdgeLayout.None"/>.</summary>
    public bool Shown => Layout != EdgeLayout.None;

    /// <summary>Whether it is in sight all the time, holding its strip or not: a bar not on hover.</summary>
    public bool Standing => Shown && Mode != EdgeMode.Hover;

    /// <summary>Whether its bar comes in only when the pointer comes near: a bar on hover.</summary>
    public bool Hovers => Shown && Mode == EdgeMode.Hover;

    /// <summary>Whether <paramref name="section"/> is one of its places: all three on a dock and on a bar, none with no bar.</summary>
    public bool Has(BarSection section) => Layout != EdgeLayout.None;
}

/// <summary>
/// A hover place on a display: a third of one of its edges, as the screen has it (the edge and the
/// section along it).
/// </summary>
public readonly record struct EdgePlace(Edge Edge, BarSection Section);

/// <summary>A display's four edges (see the top of the file), each an <see cref="EdgeBar"/>.</summary>
public sealed class ScreenEdges
{
    /// <summary>The edges a bar can stand on, in their order: the bottom, the top, the left, the right.</summary>
    public static readonly Edge[] All = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    public EdgeBar Bottom { get; init; } = new();

    public EdgeBar Top { get; init; } = new();

    public EdgeBar Left { get; init; } = new();

    public EdgeBar Right { get; init; } = new();

    /// <summary>Four edges with nothing on them.</summary>
    public static ScreenEdges Empty() => new();

    /// <summary>
    /// <paramref name="edge"/>'s. A value this build does not know has no bar (a new, empty one each
    /// time), so it is never read as the bottom: a place down both sides of a bottom bar once came of
    /// that.
    /// </summary>
    public EdgeBar this[Edge edge] => edge switch
    {
        Edge.Bottom => Bottom,
        Edge.Top => Top,
        Edge.Left => Left,
        Edge.Right => Right,
        _ => new EdgeBar(),
    };

    /// <summary>The edges shown all the time (<see cref="EdgeBar.Standing"/>), in <see cref="All"/>'s order.</summary>
    public IReadOnlyList<Edge> Standing => [.. All.Where(edge => this[edge].Standing)];

    /// <summary>The edges with a bar, shown all the time or on hover (<see cref="EdgeBar.Shown"/>), in <see cref="All"/>'s order.</summary>
    public IReadOnlyList<Edge> Shown => [.. All.Where(edge => this[edge].Shown)];

    /// <summary>
    /// The edge a display's bar stands on, as one edge: the first that holds its strip, else the
    /// first shown all the time; null where every edge with a bar comes in on hover.
    /// </summary>
    public Edge? Main =>
        All.Where(edge => this[edge].Standing && this[edge].Mode == EdgeMode.Reserve).Select(edge => (Edge?)edge).FirstOrDefault()
        ?? Standing.Select(edge => (Edge?)edge).FirstOrDefault();

    /// <summary>
    /// Whether <paramref name="section"/> along <paramref name="edge"/> is one of the display's places:
    /// the edge has a bar, and the bar that section (<see cref="EdgeBar.Has"/>). An edge with no bar has none.
    /// </summary>
    public bool Has(Edge edge, BarSection section) => this[edge].Has(section);
}
