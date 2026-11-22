// A section grows into the free room beside it (the person, 2026-10-08: "the sections dont properly
// expand when there is enough space for it. my center bottom dock bar for example shrinks its apps
// even though there is enough space on the left and right side"). The three places of an edge on
// hover were each held to a third of it (HoverPlaces.Third), so a middle dock of many apps folded
// them into "…" beside two empty thirds. Now they share the edge as a bar's sections share the bar
// (HoverPlaces.Share): the middle centred and growing evenly while both sides have room, then moved
// as little as keeps it off them; the start and the end growing towards the middle; and nothing
// folds or gives way until the whole edge is full. Here: the share itself, the places it gives, and
// the layouts a place and a bar shown all the time make of it, a dock's and a bar's, on every edge.
// Pure: no window is made.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed class SectionGrowthTests
{
    private const float Cell = 40f;
    private static readonly EdgeRun Run = new(0, 1920);

    private static EdgeRun[] Share(int start, int middle, int end, int gap = 0) =>
        HoverPlaces.Share(Run, new EdgeWants(start, middle, end, gap));

    /// <summary>The three stretches meet with no gap and cover the whole run.</summary>
    private static void Tiles(EdgeRun run, EdgeRun[] shares)
    {
        Assert.Equal(run.From, shares[0].From);
        Assert.Equal(shares[0].To, shares[1].From);
        Assert.Equal(shares[1].To, shares[2].From);
        Assert.Equal(run.To, shares[2].To);
    }

    // --- The share ------------------------------------------------------------------------------

    /// <summary>
    /// The bug: a middle wanting 1500 px with nothing either side. Its third was 640 px; it has its
    /// 1500 now, centred on the edge's centre, and a third of what is left.
    /// </summary>
    [Fact]
    public void AMiddleWithEmptyNeighboursUsesTheFreeRoom()
    {
        var shares = Share(0, 1500, 0);

        Tiles(Run, shares);
        Assert.True(shares[1].Length >= 1500, $"the middle has {shares[1].Length} px for 1500");
        Assert.Equal(1920, shares[0].Length + shares[1].Length + shares[2].Length);
        Assert.Equal(960 * 2, shares[1].From + shares[1].To);
    }

    /// <summary>
    /// While both sides have room the middle grows evenly about the edge's centre: its stretch stays
    /// centred whatever it wants, and is never shorter than its want.
    /// </summary>
    [Theory]
    [InlineData(100, 200, 100)]
    [InlineData(300, 640, 200)]
    [InlineData(200, 900, 500)]
    [InlineData(0, 1000, 400)]
    public void TheMiddleGrowsEvenlyWhileBothSidesHaveRoom(int start, int middle, int end)
    {
        var shares = Share(start, middle, end, gap: 8);

        Tiles(Run, shares);
        Assert.True(shares[1].Length >= middle);
        Assert.Equal(1920, shares[1].From + shares[1].To);
        Assert.True(shares[0].Length >= start + 8 && shares[2].Length >= end, "the start and the end keep their own and the gap");
    }

    /// <summary>
    /// Where staying centred would run into the start, the middle moves right just far enough to
    /// keep the gap — no further — and the end keeps what it wants; the other way round on the right.
    /// </summary>
    [Fact]
    public void TheMiddleMovesAsLittleAsItMust()
    {
        var right = Share(700, 800, 100, gap: 8);
        Tiles(Run, right);
        Assert.Equal(708, right[1].From);
        Assert.Equal(1508, right[1].To);

        var left = Share(100, 800, 700, gap: 8);
        Tiles(Run, left);
        Assert.Equal(1920 - 708, left[1].To);
        Assert.Equal(1920 - 708 - 800, left[1].From);
    }

    /// <summary>
    /// The room nobody wants is shared too, so an empty place in edit mode has room for what is
    /// dropped on it: the middle a third of it, evenly either side, the start and the end the rest.
    /// </summary>
    [Fact]
    public void EveryPlaceHasSomeOfTheFreeRoom()
    {
        var shares = Share(108, 300, 108);
        var free = 1920 - 516;

        Assert.Equal(300 + (free / 3), shares[1].Length);
        Assert.Equal(1920, shares[1].From + shares[1].To);
        Assert.Equal(shares[0].Length, shares[2].Length);
        Assert.True(shares[0].Length - 108 >= free / 3, "the start has its third of the free room");
    }

    /// <summary>The start and the end grow from their ends towards the middle, past a third where they need it.</summary>
    [Fact]
    public void TheStartAndTheEndGrowTowardsTheMiddle()
    {
        var shares = Share(1100, 0, 300);

        Tiles(Run, shares);
        Assert.True(shares[0].Length >= 1100);
        Assert.True(shares[2].Length >= 300);
        Assert.Equal(0, shares[1].Length);

        // With the middle empty the two meet at the centre where both fit either side of it.
        var even = Share(200, 0, 300);
        Assert.Equal(960, even[0].To);
        Assert.Equal(960, even[2].From);
    }

    /// <summary>
    /// Whatever they want, while the three fit each has at least its want; only an edge whose three
    /// want more than its length cuts any, and then it is cut to exactly the edge — the smallest met
    /// whole where they come in under an even share.
    /// </summary>
    [Fact]
    public void NoPlaceIsCutUntilTheEdgeIsFull()
    {
        int[] wants = [0, 50, 300, 639, 640, 641, 900, 1300, 2500];
        foreach (var start in wants)
        {
            foreach (var middle in wants)
            {
                foreach (var end in wants)
                {
                    const int gap = 4;
                    var shares = Share(start, middle, end, gap);
                    Tiles(Run, shares);
                    var gaps = (start > 0 && (middle > 0 || end > 0) ? gap : 0) + (end > 0 && middle > 0 ? gap : 0);
                    var full = start + middle + end + gaps > Run.Length;
                    var what = $"{start}/{middle}/{end}: {shares[0].Length}/{shares[1].Length}/{shares[2].Length}";
                    if (!full)
                    {
                        Assert.True(shares[0].Length >= start && shares[1].Length >= middle && shares[2].Length >= end, what);
                    }
                    else
                    {
                        // The room less the gaps is shared out to the last pixel, none over its want,
                        // a want under an even third met whole, and the stretches are those shares
                        // and the gaps.
                        var given = HoverPlaces.Even(Run.Length - gaps, [start, middle, end]);
                        Assert.Equal(Run.Length - gaps, given.Sum());
                        foreach (var (want, got) in new[] { (start, given[0]), (middle, given[1]), (end, given[2]) })
                        {
                            Assert.True(got <= want, what);
                            if (want > 0 && want <= (Run.Length - gaps) / 3)
                            {
                                Assert.Equal(want, got);
                            }
                        }
                        Assert.Equal(given[1], shares[1].Length);
                        Assert.Equal(Run.Length - given[0] - given[1] - given[2], gaps);
                    }
                }
            }
        }
    }

    [Fact]
    public void TheLongestAreCutFirst()
    {
        Assert.Equal([100, 910, 910], HoverPlaces.Even(1920, [100, 2000, 3000]));
        Assert.Equal([640, 640, 640], HoverPlaces.Even(1920, [700, 800, 900]));
        Assert.Equal([0, 1920, 0], HoverPlaces.Even(1920, [0, 5000, 0]));
        Assert.Equal([300, 1621, 0], HoverPlaces.Even(1921, [300, 4000, 0]));
    }

    /// <summary>Until every place of the edge has been measured, its thirds, as before.</summary>
    [Fact]
    public void AWantNotKnownYetKeepsTheThirds()
    {
        var shares = HoverPlaces.Share(Run, new EdgeWants(0, null, 0));

        Assert.Equal(new EdgeRun(0, 640), shares[0]);
        Assert.Equal(new EdgeRun(640, 1280), shares[1]);
        Assert.Equal(new EdgeRun(1280, 1920), shares[2]);
        Assert.Equal(shares, HoverPlaces.Share(Run, default));
    }

    // --- The places, at rest and in edit mode ------------------------------------------------------

    /// <summary>
    /// A display's places take their stretches from what each edge's places want: the bottom's middle
    /// its 1500 px about the centre, and its strip with it; an edge with no wants stays in thirds.
    /// </summary>
    [Fact]
    public void ThePlacesOfADisplayFollowTheirWants()
    {
        var display = new RectI(0, 0, 1920, 1080);
        var places = HoverPlaces.For(display, [], (edge, section) => edge == Edge.Bottom && section == BarSection.Middle, [],
            edge => edge == Edge.Bottom ? new EdgeWants(0, 1500, 0) : default);

        var middle = places.Single(place => place.Edge == Edge.Bottom && place.Section == BarSection.Middle);
        Assert.True(middle.Run.Length >= 1500);
        Assert.Equal(1920, middle.Run.From + middle.Run.To);
        Assert.Equal(new RectI(middle.Run.From, 1079, middle.Run.To, 1080), middle.Strip);
        Assert.Equal(new RectI(middle.Run.From, 0, middle.Run.To, 1080), HoverPlaces.Area(display, middle));
        Assert.Equal(new EdgeRun(640, 1280), places.Single(place => place.Edge == Edge.Top && place.Section == BarSection.Middle).Run);
    }

    // --- The layouts: a place, and a bar shown all the time ------------------------------------------

    private static readonly (EdgeLayout Layout, BarStyle Style)[] Looks =
        [(EdgeLayout.Dock, BarStyle.Floating), (EdgeLayout.Taskbar, BarStyle.Islands)];

    public static TheoryData<Edge, EdgeLayout> EveryEdgeAndLayout()
    {
        var data = new TheoryData<Edge, EdgeLayout>();
        foreach (var edge in ScreenEdges.All)
        {
            foreach (var (layout, _) in Looks)
            {
                data.Add(edge, layout);
            }
        }
        return data;
    }

    private static Config Wearing(EdgeLayout layout) => new() { Style = EdgeLayouts.Style(layout) };

    /// <summary>An area <paramref name="length"/> px along <paramref name="edge"/>, starting <paramref name="from"/> px in, the whole display across.</summary>
    private static RECT Along(Edge edge, int from, int length) => edge.IsVertical()
        ? new RECT { left = 0, top = from, right = 1920, bottom = from + length }
        : new RECT { left = from, top = 0, right = from + length, bottom = 1920 };

    /// <summary>Start and the open apps, <paramref name="tiles"/> of them, in <paramref name="section"/>: a dock of apps.</summary>
    private static PartSpec[] Apps(BarSection section) =>
    [
        new(Config.IslandKey(null, "s"), BarKind.Start) { Section = section },
        new(Config.IslandKey(null, "a"), BarKind.Apps) { Section = section },
    ];

    private static Layout Place(Edge edge, EdgeLayout layout, RECT area, int tiles, BarSection section = BarSection.Middle) =>
        Layout.ForTests(96, area, edge, Wearing(layout), Apps(section), Cell, tiles: tiles, only: section);

    /// <summary>How many apps make a middle that wants about <paramref name="share"/> of a 1920 px edge.</summary>
    private static int TilesFor(Edge edge, EdgeLayout layout, float share)
    {
        var one = Place(edge, layout, Along(edge, 0, 1920), 1).Natural;
        var two = Place(edge, layout, Along(edge, 0, 1920), 2).Natural;
        return (int)MathF.Ceiling(((share * 1920) - one) / (two - one)) + 1;
    }

    /// <summary>
    /// The bug, in every layout on every edge: a middle place of many apps, nothing either side. In
    /// its third it folds its apps into "…" (the old cap); in the stretch it is given now, every app
    /// shows, centred on the edge, and only past the whole edge do they fold again.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryEdgeAndLayout))]
    public void AMiddlePlaceOfManyAppsFoldsOnlyWhenTheWholeEdgeIsFull(Edge edge, EdgeLayout layout)
    {
        var tiles = TilesFor(edge, layout, 0.7f);
        var third = HoverPlaces.Third(Run, BarSection.Middle);
        var before = Place(edge, layout, Along(edge, third.From, third.Length), tiles);
        Assert.NotEmpty(before.Overflowed);

        // What the dock does (DockWindow.Wants, Dock.Reshare): the place's want, measured anywhere,
        // shares the edge out; the place is laid out again in its stretch.
        var natural = before.Natural;
        Assert.InRange(natural, third.Length + 1, Run.Length);
        Assert.Equal(natural, Place(edge, layout, Along(edge, 0, Run.Length), tiles).Natural);
        var stretch = HoverPlaces.Share(Run, new EdgeWants(0, natural, 0, before.PlaceGap))[1];
        var after = Place(edge, layout, Along(edge, stretch.From, stretch.Length), tiles);
        Assert.Empty(after.Overflowed);
        var parts = Enumerable.Range(0, after.Specs.Count).Where(after.Placed).Select(after.PartAt).ToList();
        var centre = stretch.From + ((parts.Min(part => part.Left) + parts.Max(part => part.Right)) / 2f);
        Assert.InRange(centre, 959f, 961f);

        // Past the whole edge: now they fold.
        var many = TilesFor(edge, layout, 1.3f);
        var over = Place(edge, layout, Along(edge, 0, Run.Length), many);
        var all = HoverPlaces.Share(Run, new EdgeWants(0, over.Natural, 0, over.PlaceGap))[1];
        Assert.Equal(Run, all);
        Assert.NotEmpty(over.Overflowed);
    }

    /// <summary>
    /// With neighbours that hold something, the middle place still has every bit of room they leave
    /// it before its apps fold, and they keep what they want: the start's and the end's widgets never
    /// give way for its apps while the edge has room for both.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryEdgeAndLayout))]
    public void ThePlacesBesideKeepWhatTheyWant(Edge edge, EdgeLayout layout)
    {
        var tiles = TilesFor(edge, layout, 0.6f);
        var start = Place(edge, layout, Along(edge, 0, 640), 2, BarSection.Start);
        var end = Place(edge, layout, Along(edge, 1280, 640), 3, BarSection.End);
        var middle = Place(edge, layout, Along(edge, 640, 640), tiles);
        var wants = new EdgeWants(start.Natural, middle.Natural, end.Natural, middle.PlaceGap);
        var shares = HoverPlaces.Share(Run, wants);

        Tiles(Run, shares);
        Assert.Empty(Place(edge, layout, Along(edge, shares[0].From, shares[0].Length), 2, BarSection.Start).Overflowed);
        Assert.Empty(Place(edge, layout, Along(edge, shares[1].From, shares[1].Length), tiles).Overflowed);
        Assert.Empty(Place(edge, layout, Along(edge, shares[2].From, shares[2].Length), 3, BarSection.End).Overflowed);
    }

    /// <summary>
    /// A bar shown all the time lays its sections out along its whole length (Layout.SectionStarts),
    /// never in thirds: a middle of many apps with empty sides uses the free room, centred, in every
    /// layout on every edge, and folds only once the whole bar is full.
    /// </summary>
    [Theory]
    [MemberData(nameof(EveryEdgeAndLayout))]
    public void ABarShownAllTheTimeGrowsItsMiddleIntoTheFreeRoom(Edge edge, EdgeLayout layout)
    {
        Layout Bar(int tiles) => Layout.ForTests(96, Along(edge, 0, 1920), edge, Wearing(layout), Apps(BarSection.Middle), Cell, tiles: tiles);
        var tiles = TilesFor(edge, layout, 0.7f);

        var bar = Bar(tiles);
        Assert.Empty(bar.Overflowed);
        var parts = Enumerable.Range(0, bar.Specs.Count).Where(bar.Placed).Select(bar.PartAt).ToList();
        var (from, to) = (parts.Min(part => part.Left), parts.Max(part => part.Right));
        Assert.True(to - from > 1920 / 3f, $"the middle runs {from}–{to}");
        Assert.InRange((from + to) / 2f, 959f, 961f);
        Assert.NotEmpty(Bar(TilesFor(edge, layout, 1.3f)).Overflowed);
    }

    /// <summary>
    /// What a place wants is what it needs, wherever it was laid out: every part at its own size, its
    /// labelled buttons whole, in edit mode an empty place's room; nothing for a place with nothing.
    /// </summary>
    [Fact]
    public void APlacesWantIsItsNaturalLength()
    {
        var narrow = Place(Edge.Bottom, EdgeLayout.Taskbar, Along(Edge.Bottom, 0, 200), 12);
        var wide = Place(Edge.Bottom, EdgeLayout.Taskbar, Along(Edge.Bottom, 0, 1920), 12);
        Assert.Equal(wide.Natural, narrow.Natural);
        Assert.NotEmpty(narrow.Overflowed);
        Assert.Empty(wide.Overflowed);

        var nothing = Layout.ForTests(96, Along(Edge.Bottom, 0, 640), Edge.Bottom, Wearing(EdgeLayout.Dock), [], Cell, only: BarSection.Middle);
        Assert.Equal(0, nothing.Natural);
        var room = Layout.ForTests(96, Along(Edge.Bottom, 0, 640), Edge.Bottom, Wearing(EdgeLayout.Dock), [], Cell, editing: true, only: BarSection.Middle);
        Assert.True(room.Natural > 0, "an empty place in edit mode wants its empty room");
        // A dock's place keeps its ends inside it, so two never touch; the others keep the sections' gap.
        Assert.Equal(0, room.PlaceGap);
    }
}
