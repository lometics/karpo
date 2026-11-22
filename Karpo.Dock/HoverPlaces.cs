// Where the hover places of one display lie, in physical pixels: the places are the three sections
// of each of its edges, less an edge shown all the time, whose sections are its bar's own. An edge's
// places run its whole length: a bar stands along the whole edge, as Windows' taskbar does, and a
// layout never has bars on two edges that meet (one edge, or the top and the bottom, or the left and
// the right), so no two reach across each other. A hover place reserves no space; it hides off its stretch of
// the edge and slides in when the pointer reaches the 1 px strip along that stretch. Which places
// there are, how long each stretch is and where its strip lies is worked out here, apart from any
// window, so it can be tested.
//
// The three places of an edge share it as a bar's three sections share the bar (the person,
// 2026-10-08: "the sections dont properly expand when there is enough space"): each as long as what
// it holds wants (EdgeWants, each place's Layout.Natural), the middle centred on the edge and growing
// evenly about the centre while both sides have room, then moving as little as keeps it off them,
// the start and the end growing from their ends towards the middle (Share). Only an edge whose three
// want more than it is long cuts them, the longest first. The thirds only until each is measured.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>A stretch along one edge, in screen pixels: x along the top and the bottom, y down a side.</summary>
public readonly record struct EdgeRun(int From, int To)
{
    public int Length => Math.Max(0, To - From);
}

/// <summary>
/// One hover place on a display: a section of an edge that comes in on hover (<see cref="EdgeMode.Hover"/>).
/// </summary>
/// <param name="Available">
/// False on an inner edge, one that touches another display: the pointer crosses there to the
/// other display, so nothing may wait on it.
/// </param>
/// <param name="SharedWith">
/// For an unavailable place, the index in the other displays given to <see cref="HoverPlaces.For"/>
/// of the one its edge touches. Null otherwise.
/// </param>
/// <param name="Run">The place's stretch of the edge (<see cref="HoverPlaces.Share"/>), which its window lays itself out along.</param>
/// <param name="Strip">
/// The 1 px strip along the edge that brings the place in. Empty (all zero) when the place is
/// empty or unavailable: nothing waits on an edge for a place with nothing to show.
/// </param>
public readonly record struct HoverPlace(
    Edge Edge,
    BarSection Section,
    bool Available,
    int? SharedWith,
    EdgeRun Run,
    RectI Strip);

/// <summary>
/// How long each of an edge's three places wants to be, in px along the edge: what its bar needs to
/// show all it holds at its own size (<c>Layout.Natural</c>); 0 for a place with nothing to show,
/// null for one not measured yet. <paramref name="Gap"/> is the least room between two places that
/// both hold something (<c>Layout.PlaceGap</c>).
/// </summary>
public readonly record struct EdgeWants(int? Start, int? Middle, int? End, int Gap = 0)
{
    /// <summary>Whether every place's want is known: until then the places are the edge's thirds.</summary>
    public bool Known => Start is not null && Middle is not null && End is not null;
}

public static class HoverPlaces
{
    private static readonly BarSection[] Sections = [BarSection.Start, BarSection.Middle, BarSection.End];

    /// <summary>
    /// The hover places of a display whose bounds are <paramref name="bounds"/>, in the order bottom,
    /// top, left, right (<see cref="ScreenEdges.All"/>), and start, middle, end on each.
    /// </summary>
    /// <param name="standing">The edges shown all the time: they have no hover places.</param>
    /// <param name="filled">Whether a place has anything in it.</param>
    /// <param name="otherMonitors">The bounds of the other displays.</param>
    /// <param name="wants">
    /// How long each edge's places want to be (<see cref="EdgeWants"/>), which they share the edge by
    /// (<see cref="Share"/>); with none, or one not known yet, they are its thirds.
    /// </param>
    public static IReadOnlyList<HoverPlace> For(
        RectI bounds,
        IReadOnlyCollection<Edge> standing,
        Func<Edge, BarSection, bool> filled,
        IReadOnlyList<RectI> otherMonitors,
        Func<Edge, EdgeWants>? wants = null)
    {
        var places = new List<HoverPlace>(12);
        foreach (var edge in ScreenEdges.All)
        {
            if (standing.Contains(edge))
            {
                continue;
            }
            var shared = Touching(bounds, edge, otherMonitors);
            var shares = Share(RunOf(bounds, edge), wants?.Invoke(edge) ?? default);
            foreach (var section in Sections)
            {
                var stretch = shares[(int)section];
                // Only a place that can bring itself in has a strip: filled, and not on an inner edge.
                var strip = shared is null && filled(edge, section) && stretch.Length > 0 ? StripOf(bounds, edge, stretch) : default;
                places.Add(new HoverPlace(edge, section, shared is null, shared, stretch, strip));
            }
        }
        return places;
    }

    /// <summary>
    /// The stretches of <paramref name="run"/> an edge's three places take, start, middle and end, by
    /// how long <paramref name="wants"/> says each wants to be (the top of the file). They meet with
    /// no gap and cover the whole run, so a band over each runs the whole edge.
    /// <list type="bullet">
    /// <item>Where the three fit, <see cref="EdgeWants.Gap"/> apart where both hold something: the
    /// start's from the run's start, the end's to its end, and the middle's centred on the run —
    /// exactly while that keeps it a gap clear of the other two, else as near the centre as does, as
    /// a bar's middle section stands (<c>Layout.SectionStarts</c>). The room none of them wants is
    /// shared too, so each has some for what is dropped on it in edit mode: the middle's stretch is
    /// longer either side by the same, up to a third of it in all, as far as both sides leave it, so
    /// it stays about its own centre; the start and the end have the rest, each up to it.</item>
    /// <item>Where they do not, the run less the gaps is shared out (<see cref="Even"/>): a place that
    /// wants less than an even share has it all, and the others share the rest evenly — the longest
    /// are cut first, and only those fold their app buttons or give widgets up.</item>
    /// <item>A place that wants nothing has a stretch of none where another can use it: an empty
    /// middle's is the start's and the end's, each up to the centre or past it as they need.</item>
    /// </list>
    /// Until every want is known (<see cref="EdgeWants.Known"/>), the thirds.
    /// </summary>
    public static EdgeRun[] Share(EdgeRun run, EdgeWants wants)
    {
        if (!wants.Known)
        {
            return [Third(run, BarSection.Start), Third(run, BarSection.Middle), Third(run, BarSection.End)];
        }
        var length = run.Length;
        var start = Math.Max(0, wants.Start ?? 0);
        var middle = Math.Max(0, wants.Middle ?? 0);
        var end = Math.Max(0, wants.End ?? 0);
        var gap = Math.Max(0, wants.Gap);
        // The gap after the start and before the end, where the place across it holds something too.
        var afterStart = start > 0 && (middle > 0 || end > 0) ? gap : 0;
        var beforeEnd = end > 0 && middle > 0 ? gap : 0;
        var free = length - (start + afterStart + middle + beforeEnd + end);
        if (free >= 0)
        {
            var least = run.From + start + afterStart;
            var most = run.To - end - beforeEnd - middle;
            var at = Math.Clamp(run.From + ((length - middle) / 2), least, Math.Max(least, most));
            // The same more either side of the middle, so its centre stays put: a third of the free
            // room in all, as far as both sides leave it.
            var more = middle > 0 ? Math.Max(0, Math.Min(Math.Min(at - least, most - at), free / 6)) : 0;
            var (from, to) = (at - more, at + middle + more);
            return [new EdgeRun(run.From, from), new EdgeRun(from, to), new EdgeRun(to, run.To)];
        }
        var given = Even(Math.Max(0, length - afterStart - beforeEnd), [start, middle, end]);
        var first = Math.Min(run.From + given[0] + afterStart, run.To);
        var second = Math.Min(first + given[1], run.To);
        return [new EdgeRun(run.From, first), new EdgeRun(first, second), new EdgeRun(second, run.To)];
    }

    /// <summary>
    /// <paramref name="room"/> px shared among <paramref name="wants"/> that come to more: a want no
    /// more than an even share of what is left is met whole, the smallest first, and the others share
    /// what is left evenly, the odd pixels to the first of them. None for a want of nothing.
    /// </summary>
    internal static int[] Even(int room, int[] wants)
    {
        var given = new int[wants.Length];
        var open = Enumerable.Range(0, wants.Length).Where(index => wants[index] > 0).OrderBy(index => wants[index]).ToList();
        var left = room;
        while (open.Count > 0 && wants[open[0]] <= left / open.Count)
        {
            given[open[0]] = wants[open[0]];
            left -= wants[open[0]];
            open.RemoveAt(0);
        }
        open.Sort();
        for (var at = 0; at < open.Count; at++)
        {
            given[open[at]] = (left / open.Count) + (at < left % open.Count ? 1 : 0);
        }
        return given;
    }

    /// <summary>A section's third of <paramref name="run"/>. The three meet with no gap and cover it all.</summary>
    internal static EdgeRun Third(EdgeRun run, BarSection section)
    {
        int length = run.Length;
        int first = run.From + length / 3, second = run.From + length * 2 / 3;
        return section switch
        {
            BarSection.Start => new EdgeRun(run.From, first),
            BarSection.Middle => new EdgeRun(first, second),
            _ => new EdgeRun(second, run.From + length),
        };
    }

    /// <summary>The whole of <paramref name="edge"/> of <paramref name="bounds"/>: its width along the top and the bottom, its height down a side.</summary>
    internal static EdgeRun RunOf(RectI bounds, Edge edge) =>
        edge.IsVertical() ? new EdgeRun(bounds.Top, bounds.Bottom) : new EdgeRun(bounds.Left, bounds.Right);

    /// <summary>The 1 px strip inside <paramref name="bounds"/> on <paramref name="edge"/>, along <paramref name="run"/>.</summary>
    private static RectI StripOf(RectI bounds, Edge edge, EdgeRun run) => edge switch
    {
        Edge.Top => new RectI(run.From, bounds.Top, run.To, bounds.Top + 1),
        Edge.Left => new RectI(bounds.Left, run.From, bounds.Left + 1, run.To),
        Edge.Right => new RectI(bounds.Right - 1, run.From, bounds.Right, run.To),
        _ => new RectI(run.From, bounds.Bottom - 1, run.To, bounds.Bottom),
    };

    /// <summary>
    /// The index of the first display in <paramref name="others"/> that <paramref name="edge"/> of
    /// <paramref name="bounds"/> touches, along any length of it; null when it touches none. A display
    /// meeting it only at a corner point does not count.
    /// </summary>
    internal static int? Touching(RectI bounds, Edge edge, IReadOnlyList<RectI> others)
    {
        for (int i = 0; i < others.Count; i++)
        {
            var o = others[i];
            var touches = edge switch
            {
                Edge.Top => o.Bottom == bounds.Top && Overlap(o.Left, o.Right, bounds.Left, bounds.Right),
                Edge.Left => o.Right == bounds.Left && Overlap(o.Top, o.Bottom, bounds.Top, bounds.Bottom),
                Edge.Right => o.Left == bounds.Right && Overlap(o.Top, o.Bottom, bounds.Top, bounds.Bottom),
                _ => o.Top == bounds.Bottom && Overlap(o.Left, o.Right, bounds.Left, bounds.Right),
            };
            if (touches) return i;
        }
        return null;
    }

    private static bool Overlap(int a0, int a1, int b0, int b1) => Math.Min(a1, b1) > Math.Max(a0, b0);

    // --- The window of a place ---------------------------------------------

    /// <summary>
    /// Whether <paramref name="place"/> has a window of its own: it can come in (not on an inner
    /// edge) and has something to show. An empty place has none, and nothing waits on its edge.
    /// While its display is edited (<paramref name="editing"/>) every place has one: an empty place
    /// shows its empty room, and one on an inner edge is a place like any other, which takes what is
    /// dropped on it and added to it (2026-10-07) — though it never comes in at rest.
    /// An empty place on a <paramref name="banded"/> edge has one too (<see cref="Banded"/>): it
    /// shows the edge's band over its stretch, and comes in with the rest of the edge, never by itself.
    /// </summary>
    public static bool Wanted(HoverPlace place, bool filled, bool editing = false, bool banded = false) =>
        editing || (place.Available && (filled || banded));

    /// <summary>
    /// Whether <paramref name="edge"/> comes in as one bar, its band along the whole of it, the
    /// stretches with nothing in them too, as a bar shown all the time stands: in a look with a band
    /// (connected, islands), where any of its places has something in it. Every place of such an edge
    /// has a window, an empty one showing only the band (the person, 2026-10-07: "the whole bar
    /// should have the correct background"). A floating edge's sections are panels of their own,
    /// and an empty one shows nothing.
    /// </summary>
    public static bool Banded(BarStyle style, Edge edge, Func<Edge, BarSection, bool> filled) =>
        style != BarStyle.Floating && Sections.Any(section => filled(edge, section));

    /// <summary>
    /// The rectangle a place's bar is laid out in (Layout's area), in screen px: its stretch along
    /// the edge (<see cref="Share"/>), the whole display across it, so the bar stands on the edge as a
    /// full one does.
    /// </summary>
    public static RectI Area(RectI bounds, HoverPlace place) => place.Edge.IsVertical()
        ? new(bounds.Left, place.Run.From, bounds.Right, place.Run.To)
        : new(place.Run.From, bounds.Top, place.Run.To, bounds.Bottom);

    /// <summary>
    /// What a bar that holds no space is made of — a place, or a bar shown all the time over the
    /// windows: the bar's own material, except clear, which over the windows it comes in on would
    /// leave its icons on whatever is there: §547 the wallpaper Karpo blurs instead, its colour of no opacity
    /// over it (Translucent, Scene.Wallpaper). Its look and its thickness are its edge's own
    /// (<see cref="Config.WithEdge"/>), whatever its mode (the person, 2026-10-07: "all modes follow the
    /// chosen style").
    /// </summary>
    public static Look Dressed(Look look) => look.Material == Material.Clear
        ? look with { Material = Material.Translucent }
        : look;
}
