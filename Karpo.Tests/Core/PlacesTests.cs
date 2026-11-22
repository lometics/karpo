// places: every entry stands in a section of its layout's one bar (TaskbarLayouts.cs), and the bar
// shows as its layout says: all the time, holding its space or not, or its places coming in on hover.
//
// What is held here: an entry's section is its own, and how its bar shows, or its layout's type, never
// moves it; the list is kept by section; a widget moved to another section keeps the other app's edit
// of it. (§488: a second row, the Top and bottom's top bar, is gone; a "row" a file still says is read
// past and leaves the file.)

using System.Text.Json.Nodes;
using static Karpo.Core.BarKind;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Core;

public sealed class PlacesTests
{
    private static List<string> IdsOf(IEnumerable<BarItem> list) => [.. list.Select(island => island.Id)];

    private static BarItem Entry(string id, BarKind kind, BarSection section) =>
        new() { Id = id, Kind = kind, Section = section };

    private static readonly BarSection[] AllSections = [BarSection.Start, BarSection.Middle, BarSection.End];

    /// <summary>The list is kept by section, each keeping its own order; a row a file still says is read past and not written.</summary>
    [Fact]
    public void TheListIsKeptBySection()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "type": "taskbar", "islands": [
              { "id": "t2", "kind": "tray", "section": "end", "row": "top" },
              { "id": "e", "kind": "doNotDisturb", "section": "end", "row": "main" },
              { "id": "t1", "kind": "widgetsBoard", "section": "start" },
              { "id": "s", "kind": "start", "section": "middle" } ] } ] }
            """, out var changed);

        Assert.True(changed);
        var list = Configs.Islands(config);
        Assert.Equal(["t1", "s", "t2", "e"], IdsOf(list));
        Assert.DoesNotContain("\"row\"", config.ToJson(), StringComparison.Ordinal);
        Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
    }

    /// <summary>A section's range is its own run of the list, an empty section's an empty run where it would be.</summary>
    [Fact]
    public void ARangeIsItsSectionsRun()
    {
        List<BarItem> list =
        [
            Entry("a", BarKind.WidgetsBoard, BarSection.Start),
            Entry("b", BarKind.NightLight, BarSection.End),
            Entry("c", Tray, BarSection.End),
        ];
        Sections.Order(list);

        Assert.Equal((0, 1), Sections.Range(list, BarSection.Start));
        Assert.Equal((1, 1), Sections.Range(list, BarSection.Middle));
        Assert.Equal((1, 3), Sections.Range(list, BarSection.End));
        Assert.Equal(BarSection.End, Sections.SectionAt(list, 2));
        Assert.Equal(BarSection.End, Sections.SectionAt(list, 9));
        Assert.Equal(BarSection.Start, Sections.SectionAt([], 0));
    }

    /// <summary>Put into a slot moves an entry to its section, at its index where that is inside the section and at the section's nearer end where not.</summary>
    [Fact]
    public void PutMovesBetweenSections()
    {
        var clock = Entry("b", BarKind.NightLight, BarSection.End);
        List<BarItem> list =
        [
            Entry("a", BarKind.WidgetsBoard, BarSection.Start),
            clock,
            Entry("c", Tray, BarSection.End),
        ];

        list.Remove(clock);
        var at = Sections.Put(list, clock, new BarSlot(BarSection.End, 2));
        Assert.Equal(["a", "c", "b"], IdsOf(list));
        Assert.Equal(2, at);

        list.Remove(clock);
        at = Sections.Put(list, clock, new BarSlot(BarSection.Start, 0));
        Assert.Equal(BarSection.Start, clock.Section);
        Assert.Equal(["b", "a", "c"], IdsOf(list));
        Assert.Equal(0, at);

        list.Remove(clock);
        Sections.Put(list, clock, new BarSlot(BarSection.Start, 5));
        Assert.Equal(["a", "b", "c"], IdsOf(list));

        var pin = BarKind.Pin.New();
        Sections.Put(list, pin, new BarSlot(BarSection.End, 2));
        Assert.Equal(BarSection.End, Sections.Of(pin));
    }

    /// <summary>
    /// How a display shows its edge never moves what stands on its bar: a bar shown all the time shows
    /// the whole of it, each place its own section of it, and an anchor — a display whose bar comes in on
    /// hover — nothing; another edge nothing.
    /// </summary>
    [Fact]
    public void TheEdgeShowsItsOwnWidgetsWhicheverWayItShows()
    {
        var layout = new TaskbarLayout
        {
            Islands =
            [
                Entry("s", Start, BarSection.Middle),
                Entry("e", BarKind.NightLight, BarSection.End),
                Entry("ts", BarKind.WidgetsBoard, BarSection.Start),
                Entry("te", Tray, BarSection.End),
            ],
        };
        Sections.Order(layout.Islands);
        var list = layout.Islands;

        Assert.Equal(["ts", "s", "e", "te"], IdsOf(TheDock.Shows(layout, Edge.Bottom, null, Edge.Bottom)));
        foreach (var section in AllSections)
        {
            var expected = IdsOf(list.Where(island => island.Section == section));
            Assert.Equal(expected, IdsOf(TheDock.Shows(layout, Edge.Bottom, new EdgePlace(Edge.Bottom, section), null)));
            Assert.Empty(TheDock.Shows(layout, Edge.Bottom, new EdgePlace(Edge.Top, section), null));
        }
        Assert.Empty(TheDock.Shows(layout, Edge.Bottom, null, Edge.Top));
        Assert.Empty(TheDock.Shows(layout, Edge.Bottom, null, null));
        // The same layout as a Dock shows the same.
        _ = layout.Become(LayoutType.Dock);
        Assert.Equal(["ts", "s", "e", "te"], IdsOf(TheDock.Shows(layout, Edge.Bottom, null, Edge.Bottom)));
    }

    /// <summary>
    /// The dock moves the clock from the end to the start while the settings app gives it a label: both
    /// survive the merge.
    /// </summary>
    [Fact]
    public void AWidgetMovedToAnotherSectionKeepsTheOtherAppsEdit()
    {
        static string Bar(string clock) => JsonNode.Parse($$"""
            { "layouts": [ { "id": "L", "type": "taskbar", "islands": [
              { "id": "st", "kind": "start", "section": "middle" },
              {{clock}},
              { "id": "tr", "kind": "tray", "section": "end" } ] } ] }
            """)!.ToJsonString();
        var @base = Bar("""{ "id": "ck", "kind": "volume", "section": "end" }""");
        var moved = Bar("""{ "id": "ck", "kind": "volume", "section": "start" }""");
        var labelled = Bar("""{ "id": "ck", "kind": "volume", "section": "end", "volume": { "length": 77 } }""");

        var config = Configs.Repaired(ConfigMerge.Merge(@base, moved, labelled));

        var list = Configs.Islands(config);
        Assert.Equal(["ck", "st", "tr"], IdsOf(list));
        var clock = list[0];
        Assert.Equal(BarSection.Start, Sections.Of(clock));
        Assert.Equal(77, clock.Volume!.Length);
    }
}
