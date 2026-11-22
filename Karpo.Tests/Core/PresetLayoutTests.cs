// The layout of every ready-made group (Karpo.exe --add-preset-layout, PresetLayout): each group
// once, as the add list makes it, spread over the main row's three sections around a new bar's own
// buttons; put into a config without touching another layout or which layout a display shows;
// made again, the one before replaced; written through config.json's own save, so a running dock
// merges it in.

using System.Text.Json.Nodes;
using Karpo.Tests.Persistence;

namespace Karpo.Tests.Core;

[Collection(ConfigFile.Collection)]
public sealed class PresetLayoutTests : IDisposable
{
    public PresetLayoutTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    private static List<BarItem> FoldersOf(TaskbarLayout layout) => [.. layout.Islands.Where(island => island.Folder is not null)];

    private static string LayoutJson(Config config, int index) => JsonNode.Parse(config.ToJson())!["layouts"]![index]!.ToJsonString();

    /// <summary>A config as the dock keeps one: its default layout and another of the person's.</summary>
    private static Config Persons()
    {
        var config = Config.Parse("{}");
        config.Repair();
        config.AddLayout("Mine", [BarKind.Start.New(), BarKind.Apps.New()], LayoutType.Dock);
        config.Repair();
        return config;
    }

    /// <summary>
    /// Every group once, in library order but for the keyboard, after the status icons where a new
    /// bar has it, and now playing, which the person moved beside the keyboard (2026-10-11), each as
    /// the add list makes it.
    /// </summary>
    [Fact]
    public void EveryGroupIsThereOnceInLibraryOrderAsTheAddListMakesIt()
    {
        var layout = PresetLayout.Make(PresetsTests.Laptop);

        var folders = FoldersOf(layout);
        Preset[] order = [.. Presets.All.Where(preset => preset.Id is not (Presets.MediaId or Presets.KeyboardId)), Presets.Find(Presets.KeyboardId)!, Presets.Find(Presets.MediaId)!];
        Assert.Equal(order.Select(preset => preset.Id), folders.Select(folder => PresetsTests.MadeBy(folder, PresetsTests.Laptop).Id));
        foreach (var (preset, folder) in order.Zip(folders))
        {
            var made = Presets.Folder(preset, PresetsTests.Laptop);
            Assert.Equal(made.Kind, folder.Kind);
            Assert.Equal(made.Folder!.Face.Kind, folder.Folder!.Face.Kind);
            Assert.Equal(made.Folder.Faces.Select(face => face.Glyph), folder.Folder.Faces.Select(face => face.Glyph));
            Assert.Equal(
                made.Folder.Cells.Select(cell => (cell.Island.Kind, cell.Island.Height)),
                folder.Folder.Cells.Select(cell => (cell.Island.Kind, cell.Island.Height)));
        }
    }

    [Fact]
    public void APcWithoutABatteryGetsNoBatteryGroup()
    {
        var layout = PresetLayout.Make(PresetsTests.Desktop);

        var made = FoldersOf(layout).Select(folder => PresetsTests.MadeBy(folder, PresetsTests.Desktop).Id).ToList();
        Assert.Equal(Presets.All.Length - 1, made.Count);
        Assert.DoesNotContain(Presets.BatteryId, made);
    }

    [Fact]
    public void ItIsANewBarsWithItsButtonsAndTheGroupsInAllThreeSections()
    {
        var layout = PresetLayout.Make(PresetsTests.Laptop);
        var first = Config.FirstLayout();

        Assert.Equal(PresetLayout.Id, layout.Id);
        Assert.Equal("All ready-made folders", layout.Name);
        Assert.Equal((first.Type, first.Size, first.Show), (layout.Type, layout.Size, layout.Show));
        // A new bar's own buttons, but the search box, Task view, the tray and the brightness, which
        // the person took out of theirs (2026-10-11); and the bar still works.
        var buttons = first.Islands.Where(island => island.Folder is null && island.Kind is not (BarKind.Search or BarKind.TaskView or BarKind.Tray or BarKind.Brightness)).Select(island => island.Kind);
        Assert.Equal(buttons, layout.Islands.Where(island => island.Folder is null).Select(island => island.Kind));
        Assert.DoesNotContain(layout.Islands, island => island.Kind is BarKind.Search or BarKind.TaskView or BarKind.Tray or BarKind.Brightness);
        Assert.Contains(layout.Islands, island => island.Kind == BarKind.Start);
        Assert.Contains(layout.Islands, island => island.Kind == BarKind.Apps);
        Assert.Equal(BarKind.ShowDesktop, layout.Islands[^1].Kind);
        // The groups spread over the three sections.
        var folders = FoldersOf(layout);
        foreach (var section in new[] { BarSection.Start, BarSection.Middle, BarSection.End })
        {
            Assert.Contains(folders, folder => folder.Section == section);
        }
        // The end's groups before the system icons, the normal gap of the bar's groups between them (§511: no
        // separator); now playing alone after the keyboard, before show desktop.
        var end = layout.Islands.Where(island => island.Section == BarSection.End).ToList();
        var icons = end.FindIndex(island => island.Kind == BarKind.Indicators);
        Assert.True(end.Take(icons).Count(island => island.Folder is not null) > 0);
        Assert.NotNull(end[icons - 1].Folder);
        Assert.Equal(
            [FolderFaces.KeyboardId, FolderFaces.NowPlayingId],
            end.Skip(icons).Where(island => island.Folder is not null).Select(island => island.Folder!.Face.Kind));
        Assert.Equal(FolderFaces.KeyboardId, end[icons + 1].Folder?.Face.Kind);
    }

    [Fact]
    public void TheConfigIsValidAndEveryIdItsOwn()
    {
        var config = Persons();

        PresetLayout.Build(config, PresetsTests.Laptop);

        Assert.False(config.Repair());
        var ids = config.Layouts.SelectMany(layout => Karpo.Core.Folders.All(layout.Islands)).Select(entry => entry.Island.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var again = Config.Parse(config.ToJson());
        again.Repair();
        Assert.Equal(config.ToJson(), again.ToJson());
    }

    [Fact]
    public void OtherLayoutsAndTheDisplaysAreLeftAlone()
    {
        var config = Persons();
        var before = Enumerable.Range(0, config.Layouts.Count).Select(index => LayoutJson(config, index)).ToList();
        var screens = JsonNode.Parse(config.ToJson())!["screens"]?.ToJsonString();
        var defaultId = config.DefaultLayoutId;

        var layout = PresetLayout.Build(config, PresetsTests.Laptop);

        Assert.Equal(before.Count + 1, config.Layouts.Count);
        Assert.Same(layout, config.Layouts[^1]);
        Assert.Equal(before, Enumerable.Range(0, before.Count).Select(index => LayoutJson(config, index)));
        Assert.Equal(screens, JsonNode.Parse(config.ToJson())!["screens"]?.ToJsonString());
        Assert.Equal(defaultId, config.DefaultLayoutId);
    }

    [Fact]
    public void MadeAgainItReplacesTheOneBeforeInItsPlace()
    {
        var config = Persons();
        PresetLayout.Build(config, PresetsTests.Laptop);
        config.AddLayout("After", [BarKind.Start.New()]);
        config.Layouts[2].Islands.RemoveAt(0);
        var count = config.Layouts.Count;
        var others = new[] { 0, 1, 3 }.Select(index => LayoutJson(config, index)).ToList();

        var layout = PresetLayout.Build(config, PresetsTests.Laptop);

        Assert.Equal(count, config.Layouts.Count);
        Assert.Same(layout, config.Layouts[2]);
        Assert.Equal(PresetLayout.Id, layout.Id);
        Assert.Equal(Presets.All.Length, FoldersOf(layout).Count);
        Assert.Equal(others, new[] { 0, 1, 3 }.Select(index => LayoutJson(config, index)));
        Assert.False(config.Repair());
    }

    /// <summary>One made by hand under the same name, with an id of its own, is the one replaced, its id kept.</summary>
    [Fact]
    public void ALayoutOfTheSameNameIsReplacedUnderItsId()
    {
        var config = Persons();
        var old = config.AddLayout("All ready-made folders", [BarKind.Start.New()]);
        config.Repair();

        var layout = PresetLayout.Build(config, PresetsTests.Laptop);

        Assert.Equal(3, config.Layouts.Count);
        Assert.Equal(old.Id, layout.Id);
        Assert.Same(layout, config.Layouts[2]);
    }

    /// <summary>
    /// Written through config.json's own save while a dock holds the file: the backup kept, and the
    /// dock's reload takes the layout, with an edit of its own not yet saved still on top.
    /// </summary>
    [Fact]
    public void WrittenToTheFileARunningDockTakesItOnItsReload()
    {
        Files.Start();
        var dock = Config.Load();
        dock.TaskbarOpacity = 55;

        var layout = PresetLayout.AddToFile(PresetsTests.Laptop);

        Assert.True(File.Exists(Config.BackupPath));
        var file = Config.Load();
        Assert.Contains(file.Layouts, found => found.Id == PresetLayout.Id);
        Assert.Equal(PresetLayout.Id, layout.Id);
        Assert.Equal(ConfigReload.Changed, dock.Reload(out var fresh));
        var taken = Assert.Single(fresh.Layouts, found => found.Id == PresetLayout.Id);
        Assert.Equal(Presets.All.Length, FoldersOf(taken).Count);
        Assert.Equal(55, fresh.TaskbarOpacity);
        Assert.Equal(file.DefaultLayoutId, fresh.DefaultLayoutId);

        // Run again: still one.
        PresetLayout.AddToFile(PresetsTests.Laptop);
        Assert.Single(Config.Load().Layouts, found => found.Id == PresetLayout.Id);
    }
}
