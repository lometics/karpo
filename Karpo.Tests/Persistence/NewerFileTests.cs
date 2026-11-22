// What this build does with what it doesn't know in config.json: a file of a newer shape is never written,
// not even through a merge, and a repair that only drops what this build doesn't know (a hand edit's typo, a
// widget of a kind it lacks) is not written over the file at the load. A newer build's file says a newer
// shape whenever it holds something this one doesn't know (ConfigSchemaTests).

using System.Text.Json.Nodes;

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class NewerFileTests : IDisposable
{
    private static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public NewerFileTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    /// <summary>A file with a property this build has none of, and a widget of a kind it doesn't know, in <paramref name="shape"/>.</summary>
    private static string Unknowns(int shape) => $$"""
        {
          "version": {{shape}},
          "newTopLevel": { "a": 1 },
          "layouts": [ { "id": "home", "name": "Home", "islands": [
            { "id": "s", "kind": "start" },
            { "id": "n", "kind": "hologram", "hologram": { "depth": 3 } },
            { "id": "t", "kind": "tray" } ] } ],
          "defaultLayoutId": "home"
        }
        """;

    /// <summary>
    /// Regression: a load whose repair only dropped what this build doesn't know (a widget of a kind it
    /// lacks) wrote the file back at once, without it. Every process drops it alike as it reads; nothing is
    /// written until a save of something else.
    /// </summary>
    [Fact]
    public void ARepairThatOnlyDropsWhatThisBuildDoesntKnowIsNotWrittenBack()
    {
        var text = Unknowns(Config.Shape);
        File.WriteAllText(Config.FilePath, text);
        File.SetLastWriteTimeUtc(Config.FilePath, LongAgo);

        var config = Config.Load();

        Assert.DoesNotContain(Karpo.Tests.Core.Configs.Islands(config), island => island.Id == "n");
        Assert.Equal(text, Files.Text());
        Assert.Equal(LongAgo, File.GetLastWriteTimeUtc(Config.FilePath));
    }

    /// <summary>An id Repair had to make up is written back at once, so every process reading the file finds the same one.</summary>
    [Fact]
    public void AnIdMadeUpIsWrittenBack()
    {
        File.WriteAllText(Config.FilePath, """{ "layouts": [ { "id": "home", "name": "Home", "islands": [ { "kind": "start" } ] } ], "defaultLayoutId": "home" }""");

        var first = Karpo.Tests.Core.Configs.Islands(Config.Load()).Single().Id;

        Assert.Equal(first, Karpo.Tests.Core.Configs.Islands(Config.Load()).Single().Id);
        Assert.Contains(first, Files.Text(), StringComparison.Ordinal);
    }

    /// <summary>A newer shape's file, with what this build doesn't know in it, is read and survives a save: nothing written.</summary>
    [Fact]
    public void ANewerShapesFileSurvivesALoadAndASave()
    {
        var text = Unknowns(Config.Shape + 1);
        File.WriteAllText(Config.FilePath, text);

        var config = Config.Load();
        config.TaskbarOpacity = 60;

        Assert.True(config.FromNewerKarpo);
        Assert.Throws<NewerConfigException>(() => config.Save());
        Assert.Equal(text, Files.Text());
    }

    /// <summary>
    /// Regression: a newer Karpo's write met by this build's save was merged in through this build's model
    /// (theirs.ToJson()): what this build doesn't know of it was dropped, and the file written in the newer
    /// shape without it. The save refuses; a reload takes the newer file, which this build then never saves.
    /// </summary>
    [Fact]
    public void ANewerKarposWriteMeanwhileIsNeverMergedOver()
    {
        Files.Start(start => Files.AddPin(start, @"C:\a.exe"));
        var dock = Config.Load();
        var newer = JsonNode.Parse(Files.Text())!;
        newer["version"] = Config.Shape + 1;
        newer["newTopLevel"] = 5;
        var text = newer.ToJsonString();
        File.WriteAllText(Config.FilePath, text);

        dock.TaskbarOpacity = 40;

        Assert.Throws<NewerConfigException>(() => dock.Save());
        Assert.Equal(text, Files.Text());
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var fresh));
        Assert.True(fresh.FromNewerKarpo);
        Assert.Throws<NewerConfigException>(() => fresh.Save());
        Assert.Equal(text, Files.Text());
    }
}
