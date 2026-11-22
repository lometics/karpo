using System.Text.Json;
using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

/// <summary>Config.Load and Save on the (temporary) config.json.</summary>
[Collection(ConfigFile.Collection)]
public sealed class LoadSaveTests : IDisposable
{
    private static readonly DateTime LongAgo = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public LoadSaveTests() => Clear();

    public void Dispose() => Clear();

    /// <summary>
    /// config.json and what loading and saving leave beside it (backup, kept broken files, files of
    /// older shapes kept aside): each test starts from nothing.
    /// </summary>
    private static void Clear()
    {
        ConfigFile.Delete();
        foreach (var file in Directory.Exists(Config.Folder)
            ? Directory.GetFiles(Config.Folder, "config.json.*").Concat(Directory.GetFiles(Config.Folder, "config.shape*.json"))
            : [])
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void NoFileMeansDefaultsAndNoWrite()
    {
        var config = Config.Load();

        Assert.Equal(48, config.Height);
        Assert.False(File.Exists(Config.FilePath));
    }

    /// <summary>Without a backup to go back to (see Persistence\RecoveryTests for the rest).</summary>
    [Fact]
    public void ABrokenFileIsKeptAsideAndDefaultsLoad()
    {
        var config = ConfigFile.Load("{ \"height\": 40, oops");

        Assert.Equal(48, config.Height);
        Assert.Equal("{ \"height\": 40, oops", File.ReadAllText(Assert.Single(Directory.GetFiles(Config.Folder, "config.json.broken-*"))));
    }

    [Fact]
    public void IdsMissingFromTheFileAreMadeUpOnce()
    {
        var dock = ConfigFile.Load($$"""
            {
              "version": {{Config.Shape}},
              "layouts": [ { "name": "Work", "islands": [ { "kind": "start" }, { "kind": "tray" } ] } ]
            }
            """);

        var settings = Config.Load();

        Assert.Equal(Configs.Islands(dock).Select(island => island.Id), Configs.Islands(settings).Select(island => island.Id));
        Assert.Equal(dock.Layouts[0].Id, settings.Layouts[0].Id);
    }

    /// <summary>
    /// perf: a processor graph somebody placed in a folder survives the load and is written again by
    /// the next save; the combined island of four cells, a kind this build does not know, leaves, and
    /// so does a top-level key with no property behind it.
    /// </summary>
    [Fact]
    public void TheGraphKindsSurviveALoadAndOnlyWhatThisBuildDoesntKnowLeaves()
    {
        var config = ConfigFile.Load($$"""
            {
              "version": {{Config.Shape}},
              "layouts": [ { "id": "default", "name": "Default", "islands": [
                { "id": "c", "kind": "folderList", "folder": { "name": "Graphs", "lists": [ [ { "island": { "id": "g", "kind": "cpu" } } ] ] } },
                { "id": "p", "kind": "performance" },
                { "id": "t", "kind": "tray" } ] } ],
              "gpuAdapters": [ "GPU" ]
            }
            """);
        config.Save();

        var text = ConfigFile.Text();
        // The processor's graph is the performance widget measuring the processor as a graph (§228, WidgetStyles).
        Assert.Contains("\"kind\": \"performance\"", text, StringComparison.Ordinal);
        Assert.Contains("\"reading\": \"cpu\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\"kind\": \"cpu\"", text, StringComparison.Ordinal);
        Assert.DoesNotContain("gpuAdapters", text, StringComparison.Ordinal);
        var loaded = Configs.Islands(Config.Load());
        Assert.Equal(["c", "t"], loaded.Select(island => island.Id));
        Assert.Equal(ListKind.Cpu, loaded[0].Folder!.Cells.Single().Island.List);
    }

    /// <summary>
    /// §668: a pin is the person's to remove. Pins this PC can't open — a program an update moved out of its
    /// versioned folder, an app not installed (or being re-registered), a drive not there — are loaded, saved and
    /// loaded again where they stood, nothing dropped and nothing rewritten.
    /// </summary>
    [Fact]
    public void PinsThisPCCannotOpenSurviveALoadAndASave()
    {
        string[] targets =
        [
            @"C:\Users\Nobody\AppData\Local\AnthropicClaude\app-0.0.1\claude.exe",
            @"shell:AppsFolder\Gone.Package_0123456789abc!App",
            @"Q:\Nowhere\tool.exe",
        ];
        var pins = targets.Select(target =>
        {
            var pin = Pin.New();
            pin.Pin!.Target = target;
            return pin;
        });
        var config = Configs.With([Start.New(), .. pins, Apps.New()]);
        config.Save();

        var loaded = Config.Load();
        loaded.Save();
        var again = Config.Load();

        Assert.Equal(targets, Configs.Islands(again).Where(island => island.Kind == Pin).Select(island => island.Pin!.Target));
        Assert.Equal(config.ToJson(), again.ToJson());
    }

    [Fact]
    public void AFileKarpoWroteIsNotWrittenAgainOnLoad()
    {
        var pin = Pin.New();
        pin.Pin!.Target = "C:\\Windows\\notepad.exe";
        var config = Configs.With(Start.New(), pin, BarKind.NightLight.New());
        config.AddDisplay("D1", "Dell");
        config.Save();
        File.SetLastWriteTimeUtc(Config.FilePath, LongAgo);

        var loaded = Config.Load();

        Assert.Equal(LongAgo, File.GetLastWriteTimeUtc(Config.FilePath));
        Assert.Equal(config.ToJson(), loaded.ToJson());
    }

    [Fact]
    public void SaveLeavesNoTemporaryFileBehind()
    {
        Configs.With(Start.New()).Save();

        Assert.True(File.Exists(Config.FilePath));
        Assert.Empty(Directory.GetFiles(Config.Folder, "*.tmp"));
    }
}
