// §603: the widgets that still showed more as their line grew split into widgets of fixed content, each in a few sizes
// (docs\widgets.md §603): the performance graphs, today's screen time and its history, the tides now and the tide graph,
// the sun's arc, the sports score and its fixtures and results, table and knockout.
//
// The person, 2026-11-09: "split all of them. also remove the are chart option from the screen time, it should always
// show the bar chart and not the area chart". What holds without a window: the file's words for each kind and size, read
// and written back as they are; no Height on any sized one; the lists keeping theirs; the add page's order; the words
// the split removed read as nothing. Each drawing's own (each size the same data, a taller box no more) is in the
// Folders' PerformanceSizesTests, ScreenTimeSplitTests, TidesSunSplitTests and SportsSplitTests.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class TierSplitTests
{
    /// <summary>A file with one list folder, its lines <paramref name="lines"/> (JSON, comma-separated).</summary>
    private static string File(string lines) => $$"""
        { "version": {{Config.Shape}}, "layouts": [ { "id": "L", "islands": [
          { "id": "f", "kind": "folderList", "section": "end", "folder": { "lists": [ [ {{lines}} ] ] } } ] } ] }
        """;

    private static List<CellItem> Lines(Config config) =>
        [.. config.Layouts.Single().Islands.Single().Folder!.Cells.Select(cell => cell.Island)];

    /// <summary>Every sized kind of the split, as the file says it (its kind, style and reading), at both sizes, and the form each reads as.</summary>
    public static TheoryData<string, string, ListKind> Sized => new()
    {
        { """ "kind": "performance", "reading": "cpu", "style": "graph" """, "compact", ListKind.Cpu },
        { """ "kind": "performance", "reading": "cpu", "style": "graph" """, "full", ListKind.Cpu },
        { """ "kind": "performance", "reading": "network", "style": "graph" """, "compact", ListKind.Network },
        { """ "kind": "performance", "reading": "network", "style": "graph" """, "full", ListKind.Network },
        { """ "kind": "screenTime" """, "compact", ListKind.ScreenTime },
        { """ "kind": "screenTime" """, "full", ListKind.ScreenTime },
        { """ "kind": "screenTimeHistory" """, "compact", ListKind.ScreenTimeHistory },
        { """ "kind": "screenTimeHistory" """, "full", ListKind.ScreenTimeHistory },
        { """ "kind": "tides" """, "compact", ListKind.Tides },
        { """ "kind": "tides" """, "full", ListKind.Tides },
        { """ "kind": "tideGraph" """, "compact", ListKind.TideGraph },
        { """ "kind": "tideGraph" """, "full", ListKind.TideGraph },
        { """ "kind": "sun" """, "compact", ListKind.SunArc },
        { """ "kind": "sun" """, "full", ListKind.SunArc },
        { """ "kind": "sports" """, "compact", ListKind.Sports },
        { """ "kind": "sports" """, "full", ListKind.Sports },
    };

    /// <summary>A split line is read as its form at its size, keeps no height, and is written back word for word.</summary>
    [Theory]
    [MemberData(nameof(Sized))]
    public void EachSizedKindAndSizeIsReadAndWrittenBackAsItIs(string words, string size, ListKind form)
    {
        var config = Configs.Repaired(File($$"""{ "island": { "id": "a", {{words}}, "size": "{{size}}" } }"""));
        var line = Lines(config).Single();
        Assert.Equal(form, line.List);
        Assert.Equal(size, line.Size!.Value.ToString().ToLowerInvariant());
        Assert.Null(line.Height);
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
        Assert.Contains($"\"size\": \"{size}\"", config.ToJson().Replace("\":\"", "\": \"", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    /// <summary>
    /// The sports' lists are lists: each keeps the Height it is given, has no size, and is written back as it is with
    /// what it follows.
    /// </summary>
    [Theory]
    [InlineData("sportsFixtures", ListKind.SportsFixtures)]
    [InlineData("sportsTable", ListKind.SportsTable)]
    [InlineData("sportsKnockout", ListKind.SportsKnockout)]
    public void TheSportsListsKeepTheirHeight(string kind, ListKind form)
    {
        var config = Configs.Repaired(File($$"""
            { "island": { "id": "a", "kind": "{{kind}}", "height": { "fixed": false, "cells": 14 }, "sports": { "sport": "soccer", "league": "eng.1", "team": "359", "name": "Arsenal" } } }
            """));
        var line = Lines(config).Single();
        Assert.Equal(form, line.List);
        Assert.Null(line.Size);
        Assert.Equal(LineHeight.Automatic(14), line.Height);
        Assert.Equal("359", line.Sports!.Team);
        _ = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
    }

    /// <summary>
    /// A height a file gives a split line is no height of it: the line is where its kind starts (§432). No migration: a
    /// height of rows is not turned into a size, which is the conversion's, once, on the person's own file
    /// (artifacts\tier-split\convert-tier-split.py).
    /// </summary>
    [Fact]
    public void AHeightIsNoneOfASplitLinesAndItsSizeIsWhereItStarts()
    {
        var config = Configs.Repaired(File("""
            { "island": { "id": "a", "kind": "performance", "reading": "cpu", "style": "graph", "height": { "fixed": true, "cells": 10 } } },
            { "island": { "id": "b", "kind": "screenTime", "height": { "fixed": true, "cells": 8 } } },
            { "island": { "id": "c", "kind": "tides", "height": { "fixed": false, "cells": 7 } } },
            { "island": { "id": "d", "kind": "sun", "height": { "fixed": true, "cells": 2 } } },
            { "island": { "id": "e", "kind": "sports", "height": { "fixed": false, "cells": 24 } } }
            """));
        var lines = Lines(config);
        Assert.All(lines, line => Assert.Null(line.Height));
        Assert.Equal([LineSize.Compact, LineSize.Full, LineSize.Full, LineSize.Full, LineSize.Full], lines.Select(line => line.Size!.Value));
    }

    /// <summary>
    /// The options the split removed are no words of this build's: the screen time's area ("screenTime": {"graph":
    /// "area"}) and a sports line's view ("view": "table") read as nothing and are not written back (§603).
    /// </summary>
    [Fact]
    public void TheAreaAndTheViewAreNoWordsAnyMore()
    {
        var config = Configs.Repaired(File("""
            { "island": { "id": "a", "kind": "screenTime", "size": "full", "screenTime": { "graph": "area" } } },
            { "island": { "id": "b", "kind": "sports", "size": "full", "sports": { "sport": "soccer", "league": "eng.1", "team": "359", "name": "Arsenal", "view": "table" } } }
            """));
        Assert.Equal(["a", "b"], Lines(config).Select(line => line.Id));
        var json = config.ToJson();
        Assert.DoesNotContain("\"graph\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"view\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("area", json, StringComparison.Ordinal);
    }

    /// <summary>Every sized kind of the split has two sizes, Compact smaller than Full where nothing measures them, and no Height.</summary>
    [Fact]
    public void EverySplitKindIsTwoSizesAndNoHeight()
    {
        foreach (var kind in LineSizeTests.TierSplit)
        {
            Assert.Equal([LineSize.Compact, LineSize.Full], kind.Offered());
            Assert.False(kind.HasHeight(), $"{kind} has a Height");
            var line = ListWidgets.Of(kind);
            Assert.True(LineSizes.CellsOf(line, LineSize.Compact) < LineSizes.CellsOf(line, LineSize.Full), $"{kind}: Compact is no smaller than Full");
        }
        // The graphs start Compact, a figure over its band; the rest at Full.
        Assert.Equal(LineSize.Compact, ListWidgets.Of(ListKind.Cpu).Starts);
        Assert.Equal(LineSize.Full, ListWidgets.Of(ListKind.ScreenTime).Starts);
        Assert.All([ListKind.SportsFixtures, ListKind.SportsTable, ListKind.SportsKnockout], kind => Assert.True(kind.HasHeight(), $"{kind} has no Height"));
    }

    /// <summary>
    /// The add page: each split pair after its ready-made folder, in its order — the tides now and the tide graph, the
    /// score and its three lists (a run of their own), screen time today and its history before the ring and the apps.
    /// </summary>
    [Fact]
    public void TheAddPageListsEachSplitWidgetAfterItsFolder()
    {
        List<string> Ids(string shelf) =>
            [.. AddTree.Browse.Rows.OfType<AddShelf>().Single(row => row.Id == shelf).Rows.OfType<AddLeaf>().Select(leaf => leaf.Id)];
        var weather = Ids("weather");
        Assert.Equal([Presets.TidesId, "tides", "tideGraph", Presets.QuakesId], weather.Skip(weather.IndexOf(Presets.TidesId)).Take(4));
        Assert.Equal([Presets.SportsId, "sports", "sportsFixtures", "sportsTable", "sportsKnockout"], weather.Skip(weather.IndexOf(Presets.SportsId)).Take(5));
        var system = Ids("system");
        Assert.Equal(["screenTimeGroup", "screenTime", "screenTimeHistory", "screenTimeChart", "screenTimeApps"], system.Skip(system.IndexOf("screenTimeGroup")).Take(5));
        // The sports run stands apart from the news and the markets by a line.
        var rows = AddTree.Browse.Rows.OfType<AddShelf>().Single(row => row.Id == "weather").Rows;
        var at = rows.ToList().FindIndex(row => row is AddLeaf { Id: Presets.SportsId });
        Assert.IsType<AddLine>(rows[at - 1]);
    }

    /// <summary>The ready-made folders: the tides now over its graph, the score over its fixtures, today's screen time over its history.</summary>
    [Fact]
    public void TheReadyMadeFoldersHoldTheSplitWidgets()
    {
        Assert.Equal(["tides", "tideGraph"], Presets.Find(Presets.TidesId)!.Items.Select(item => item.Form));
        Assert.Equal(["sports", "sportsFixtures"], Presets.Find(Presets.SportsId)!.Items.Select(item => item.Form));
        Assert.Equal(["screenTime", "screenTimeHistory", "screenTimeChart", "screenTimeApps"], Presets.Find("screenTimeGroup")!.Items.Select(item => item.Form));
    }
}
