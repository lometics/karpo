// sections: a bar's three sections in the file (2026-10-05: the bar is three sections again).
//
// What is held here: every entry is written with its section; a file that says none, or a word this
// build does not know, reads as the start; and the list is kept in its sections' order. And the
// reason the sections are a
// tag on each entry of one list rather than three lists: a widget moved to another section by one
// app keeps the edit the other app made to it at the same time.

using System.Text.Json.Nodes;
using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

public sealed class SectionFilesTests
{
    private static List<BarSection> SectionsOf(IEnumerable<BarItem> list) => [.. list.Select(island => island.Section)];

    private static List<string> IdsOf(IEnumerable<BarItem> list) => [.. list.Select(island => island.Id)];

    /// <summary>An entry that says no section is the start's; one in a word this build does not know is too, written back.</summary>
    [Fact]
    public void NoSectionOrAnUnknownOneIsTheStart()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "a", "kind": "showDesktop", "section": "end" },
              { "id": "b", "kind": "start" },
              { "id": "c", "kind": "tray", "section": "sideways" } ] } ] }
            """, out var changed);

        Assert.True(changed);
        var list = Configs.Islands(config);
        // The start's first, in their own order; the end's after them.
        Assert.Equal(["b", "c", "a"], IdsOf(list));
        Assert.Equal([BarSection.Start, BarSection.Start, BarSection.End], SectionsOf(list));
        Assert.Contains("\"section\": \"start\"", config.ToJson(), StringComparison.Ordinal);
    }

    /// <summary>A file in its sections' order with every section written reads and writes as it is.</summary>
    [Fact]
    public void AFileInItsSectionsOrderIsLeftAlone()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "a", "kind": "widgetsBoard", "section": "start" },
              { "id": "b", "kind": "start", "section": "middle" },
              { "id": "c", "kind": "showDesktop", "section": "end" } ] } ] }
            """);

        var again = Configs.RoundTrip(config, out var changed);

        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
    }

    /// <summary>
    /// Why a tag on each entry rather than three lists: the dock moves the clock into the middle while
    /// the settings app gives it a label. Both survive the merge — a moved widget is one value
    /// changed, not an entry taken out of one list (which the merge reads as a removal, and a removal
    /// wins over an edit) and put into another.
    /// </summary>
    [Fact]
    public void AWidgetMovedToAnotherSectionKeepsTheOtherAppsEdit()
    {
        static string Bar(string clock) => JsonNode.Parse($$"""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "st", "kind": "start", "section": "middle" },
              {{clock}},
              { "id": "tr", "kind": "tray", "section": "end" } ] } ] }
            """)!.ToJsonString();
        var @base = Bar("""{ "id": "ck", "kind": "volume", "section": "end" }""");
        var moved = Bar("""{ "id": "ck", "kind": "volume", "section": "middle" }""");
        var labelled = Bar("""{ "id": "ck", "kind": "volume", "section": "end", "volume": { "length": 77 } }""");

        var config = Configs.Repaired(ConfigMerge.Merge(@base, moved, labelled));

        var clock = Assert.Single(Configs.Islands(config), island => island.Kind == BarKind.Volume);
        Assert.Equal(BarSection.Middle, clock.Section);
        Assert.Equal(77, clock.Volume!.Length);
    }
}
