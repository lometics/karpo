using System.Text.Json;
using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

/// <summary>
/// A kind this build does not know — one taken out, like the combined performance island whose
/// four cells are four widgets now, or one from a newer Karpo — is dropped, and the file still
/// loads with everything else in it.
/// </summary>
public sealed class RemovedKindsTests
{
    private const string WithPerformance = """
        {
          "gpuAdapters": [ "NVIDIA GeForce" ],
          "layouts": [
            { "id": "default", "name": "Default", "islands": [
              { "id": "s1", "kind": "start" },
              { "id": "p1", "kind": "cpu",  "performance": { "seconds": 3 } },
              { "id": "p2", "kind": "gpu" },
              { "id": "c1", "kind": "volume",  "volume": { "length": 77 } },
              { "id": "p3", "kind": "memory" },
              { "id": "p4", "kind": "network" },
              { "id": "p5", "kind": "performance",  "performance": { "cpu": true, "memory": true } } ] },
            { "id": "L1", "name": "Work", "islands": [
              { "id": "w1", "kind": "network" },
              { "id": "w2", "kind": "widgetsBoard" } ] }
          ],
          "defaultLayoutId": "default"
        }
        """;

    /// <summary>
    /// perf: the graph entries went with the readings' presets (shape 32, docs\widgets.md §97), so
    /// they go as every kind this build does not know goes, and so does the island that held
    /// all four at once, a kind this build does not know, is dropped.
    /// </summary>
    [Fact]
    public void TheCombinedIslandIsDroppedAndTheGraphsStay()
    {
        var config = Configs.Repaired(WithPerformance, out var changed);

        // The graphs and p5, the combined "performance" island, are lost; the rest stays.
        Assert.True(changed);
        Assert.Equal(["s1", "c1"], Configs.Islands(config).Select(island => island.Id));
        Assert.Equal([Start, BarKind.Volume], Configs.Islands(config).Select(island => island.Kind));
        var volume = Configs.Islands(config).Single(island => island.Id == "c1").Volume!;
        Assert.Equal(77, volume.Length);

        var work = config.Layouts.Single(layout => layout.Name == "Work");
        Assert.Equal("Work", work.Name);
        Assert.Equal(["w2"], Configs.Arranged(work.Islands).Select(island => island.Id));
    }

    /// <summary>
    /// A top-level key with no property behind it ("gpuAdapters") is read past and never written;
    /// the graphs are written under their own names.
    /// </summary>
    [Fact]
    public void ATopLevelKeyThisBuildDoesntHaveIsNeverWritten()
    {
        var config = Configs.Repaired(WithPerformance);
        var text = config.ToJson();

        Assert.DoesNotContain("gpuAdapters", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"cpu\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"hologram\"")]
    [InlineData("\"\"")]
    [InlineData("null")]
    [InlineData("99")]
    public void AnIslandOfAKindThisBuildDoesntKnowIsDropped(string kind)
    {
        var config = Configs.Repaired($$"""
            {
              "layouts": [ { "id": "default", "name": "Default", "islands": [
                { "id": "a", "kind": "start" },
                { "id": "b", "kind": {{kind}},  "hologram": { "shiny": true } },
                { "id": "c", "kind": "touchKeyboard" } ] } ]
            }
            """, out var changed);

        Assert.True(changed);
        Assert.Equal(["a", "c"], Configs.Islands(config).Select(island => island.Id));
    }

    /// <summary>
    /// layouts: the switcher widget is gone — the taskbar's own menu lists the layouts. A file
    /// that has one (the dock placed it by itself) reads, drops it, and never writes it again.
    /// </summary>
    [Fact]
    public void ALayoutSwitcherIsDroppedAndNeverWrittenAgain()
    {
        var config = Configs.Repaired("""
            {
              "layouts": [
                { "id": "L1", "name": "Work", "islands": [
                  { "id": "sw", "kind": "layoutSwitcher" },
                  { "id": "c1", "kind": "touchKeyboard" } ] }
              ],
              "screens": [ { "id": "D1", "activeLayoutId": "L1" } ]
            }
            """, out var changed);

        Assert.True(changed);
        Assert.Equal(["c1"], Configs.Arranged(config.IslandsFor("D1")).Select(island => island.Id));
        Assert.DoesNotContain("layoutSwitcher", config.ToJson(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(BarEntries.Find(Configs.Kind("layoutSwitcher")));
    }

    /// <summary>
    /// folders: the row folder is gone ("the row folder should actually be removed completely for
    /// now"). A file that still has one reads, and the folder goes as every kind this build does
    /// not know goes — with what was in it, because a folder's widgets are its own and there is no
    /// migration to carry them out. Everything around it stays where it was, and it is never
    /// written again.
    /// </summary>
    [Fact]
    public void ARowFolderIsDroppedAndNeverWrittenAgain()
    {
        var config = Configs.Repaired("""
            {
              "layouts": [ { "id": "default", "name": "Default", "islands": [
                { "id": "s1", "kind": "start" },
                { "id": "r1", "kind": "folderRow",  "folder": { "name": "Pins", "lists": [ [
                  { "island": { "id": "p1", "kind": "pin",  "pin": { "target": "C:\\Tools\\a.exe" } } } ] ] } },
                { "id": "c1", "kind": "touchKeyboard" } ] } ]
            }
            """, out var changed);

        Assert.True(changed);
        Assert.Equal(["s1", "c1"], Configs.Islands(config).Select(island => island.Id));
        Assert.Equal([Start, BarKind.TouchKeyboard], Configs.Islands(config).Select(island => island.Kind));
        Assert.DoesNotContain("folderRow", config.ToJson(), StringComparison.OrdinalIgnoreCase);
        Assert.Null(BarEntries.Find(Configs.Kind("folderRow")));
    }

    /// <summary>
    /// §653: Start's search box is gone ("start search folder widget does not seem to do anything. remove it."). A file that
    /// still has one in a folder's list reads, and the line goes as every kind this build does not know goes; the folder and
    /// its other lines stay, and it is never written again. No code names it.
    /// </summary>
    [Fact]
    public void AStartSearchLineIsDroppedAndNeverWrittenAgain()
    {
        var config = Configs.Repaired("""
            {
              "layouts": [ { "id": "default", "name": "Default", "islands": [
                { "id": "s1", "kind": "start" },
                { "id": "f1", "kind": "folderList",  "folder": { "lists": [ [
                  { "island": { "id": "q1", "kind": "startSearch" } },
                  { "island": { "id": "p1", "kind": "startPins" } } ] ] } } ] } ]
            }
            """, out var changed);

        Assert.True(changed);
        var folder = Configs.Islands(config).Single(island => island.Id == "f1").Folder!;
        Assert.Equal(["p1"], folder.Cells.Select(cell => cell.Island.Id));
        Assert.DoesNotContain("startSearch", config.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void KindNamesRoundTrip()
    {
        foreach (var kind in BarEntries.All.Select(entry => entry.Kind))
        {
            var text = JsonSerializer.Serialize(kind);
            Assert.Equal(kind, JsonSerializer.Deserialize<BarKind>(text));
            Assert.Matches("^\"[a-z][A-Za-z]*\"$", text);
        }
        Assert.Equal("\"showDesktop\"", JsonSerializer.Serialize(ShowDesktop));
    }

    [Fact]
    public void AnUnknownNameReadsAsAValueNoKindHas()
    {
        var kind = Configs.Kind("hologram");

        Assert.Equal(TolerantEnumConverter<BarKind>.Unknown, kind);
        Assert.False(Enum.IsDefined(kind));
        Assert.Null(BarEntries.Find(kind));
    }

    [Fact]
    public void OtherMistakesStillFailTheFile()
    {
        // Only names are forgiven; a kind that is an object is not a config.
        Assert.Throws<JsonException>(() => Config.Parse("""{ "layouts": [ { "id": "L", "islands": [ { "kind": { "name": "clock" } } ] } ] }"""));
    }
}
