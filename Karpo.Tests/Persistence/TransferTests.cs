// Settings taken to another PC: the export file, reading it back (and what
// isn't one), displays that differ between PCs, the import that replaces
// config.json, and the summary the settings app shows before it does.

using System.Text.Json.Nodes;

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class TransferTests : IDisposable
{
    private static readonly DateTimeOffset When = new(2026, 9, 22, 18, 30, 0, TimeSpan.FromHours(2));

    public TransferTests() => Files.Reset();

    public void Dispose() => Files.Reset();

    /// <summary>Two named layouts, home the one for new displays; one display known (an office monitor, not connected here).</summary>
    private static Config TwoLayouts()
    {
        var config = Config.Parse("""
            {
              "appearance": { "palettes": { "karpo": { "taskbar": { "background": { "dark": "#99F3F3F3", "light": "#99F3F3F3" } } } } },
              "layouts": [
                { "id": "home", "name": "Home", "islands": [
                  { "id": "n", "kind": "pin", "pin": { "target": "C:\\Windows\\notepad.exe" } },
                  { "id": "c", "kind": "clock" } ] },
                { "id": "work", "name": "Work", "islands": [ { "id": "s", "kind": "start" } ] }
              ],
              "defaultLayoutId": "home",
              "screens": [ { "id": "\\\\?\\DISPLAY#OFFICE#1", "name": "Office monitor", "activeLayoutId": "work" } ]
            }
            """);
        config.Repair();
        return config;
    }

    [Fact]
    public void AnExportReadsBackAsTheSameSettings()
    {
        var config = TwoLayouts();

        var file = ConfigTransfer.Read(ConfigTransfer.Export(config, When, "DESKTOP-A"));

        Assert.Equal(ConfigTransfer.Version, file.Version);
        Assert.Equal("DESKTOP-A", file.Computer);
        Assert.Equal(When, file.Exported);
        Assert.False(file.Newer);
        Assert.Equal(config.ToJson(), file.Config.ToJson());
    }

    [Fact]
    public void TheExportIsTheConfigUnderAHeader()
    {
        var export = JsonNode.Parse(ConfigTransfer.Export(TwoLayouts(), When, "DESKTOP-A"))!.AsObject();

        Assert.Equal(["format", "version", "exported", "computer", "config"], export.Select(property => property.Key));
        Assert.Equal("karpo-settings", export["format"]!.GetValue<string>());
        Assert.Equal("#99F3F3F3", export["config"]!["appearance"]!["palettes"]!["karpo"]!["taskbar"]!["background"]!["dark"]!.GetValue<string>());
    }

    [Fact]
    public void APlainConfigJsonImportsToo()
    {
        var file = ConfigTransfer.Read(TwoLayouts().ToJson());

        Assert.Null(file.Version);
        Assert.Equal(60, file.Config.TaskbarOpacity);
    }

    [Fact]
    public void AFileFromANewerKarpoSaysSo()
    {
        var export = JsonNode.Parse(ConfigTransfer.Export(TwoLayouts(), When, "B"))!.AsObject();
        export["version"] = ConfigTransfer.Version + 1;

        Assert.True(ConfigTransfer.Read(export.ToJsonString()).Newer);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{ \"height\": 60")]
    [InlineData("[ 1, 2 ]")]
    [InlineData("{ \"name\": \"a shopping list\" }")]
    [InlineData("{ \"format\": \"something-else\", \"config\": { } }")]
    [InlineData("{ \"format\": \"karpo-settings\", \"version\": 1 }")]
    public void WhatIsntASettingsFileIsRefusedWithAReason(string text)
    {
        var error = Assert.Throws<FormatException>(() => ConfigTransfer.Read(text));
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
    }

    /// <summary>
    /// Display ids name the port a display hangs on, so they differ between PCs: the imported display's
    /// layouts stay for when it is connected here, and the displays connected here get the imported
    /// layout for new displays.
    /// </summary>
    [Fact]
    public void DisplaysThatArentHereKeepTheirLayoutsAndTheOnesHereGetTheDefault()
    {
        var imported = TwoLayouts();

        ConfigTransfer.Adopt(imported, [(@"\\?\DISPLAY#HOME#7", "Living room TV")]);

        Assert.Equal("work", imported.ActiveLayout(@"\\?\DISPLAY#OFFICE#1").Id);
        Assert.Equal("home", imported.ActiveLayout(@"\\?\DISPLAY#HOME#7").Id);
        Assert.Equal("Living room TV", imported.ScreenFor(@"\\?\DISPLAY#HOME#7").Name);
        Assert.Equal(2, imported.Screens.Count);
    }

    [Fact]
    public void AnImportReplacesTheFileAndKeepsTheOldOneBeside()
    {
        var before = Files.Start(start => start.TaskbarOpacity = 40);

        var imported = Config.Replace(TwoLayouts(), out var kept);

        Assert.Equal(60, Config.Load().TaskbarOpacity);
        Assert.NotNull(kept);
        Assert.Equal(40, Config.Parse(File.ReadAllText(kept)).TaskbarOpacity);
        Assert.Equal(40, Config.Parse(File.ReadAllText(Config.BackupPath)).TaskbarOpacity);
        Assert.Equal(ConfigReload.Unchanged, imported.Reload(out _));
        // The other app, still on the old file, merges into the import instead of undoing it.
        before.OnlineUpdateMinutes = 120;
        before.Save();
        var file = Config.Load();
        Assert.Equal(60, file.TaskbarOpacity);
        Assert.Equal(["home", "work"], file.Layouts.Select(layout => layout.Id));
        Assert.Equal([@"C:\Windows\notepad.exe"], Files.Pins(file));
        Assert.Equal(120, file.OnlineUpdateMinutes);
    }

    /// <summary>
    /// The dock tells an import from an edit by the mark it leaves (Config.ReplacedAt): an import's
    /// missing pins are another PC's, and must not come off Windows' taskbar (backlog 2).
    /// </summary>
    [Fact]
    public void AnImportLeavesTheMarkTheDockTellsItBy()
    {
        Files.Start(start => start.TaskbarOpacity = 40);
        var before = Config.ReplacedAt();
        Config.Load().Save();
        Assert.Equal(before, Config.ReplacedAt());

        Config.Replace(TwoLayouts(), out _);

        Assert.True(Config.ReplacedAt() > before);
    }

    [Fact]
    public void TheSummaryListsWhatChanges()
    {
        var current = Config.Parse("""{ "layouts": [ { "id": "home", "name": "Main", "look": { "opacity": 70 } }, { "id": "old", "name": "Gaming" } ] }""");
        current.Repair();

        var changes = TransferSummary.Changes(current, TwoLayouts());

        // §345: the taskbar's look is each layout's, a change of it the layouts'.
        Assert.DoesNotContain(changes, line => line.StartsWith("Taskbar", StringComparison.Ordinal));
        Assert.Contains("Layouts: changed", changes);
        Assert.Contains("Pins: 0 → 1", changes);
        Assert.DoesNotContain(changes, line => line.Contains("screens", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheSameSettingsChangeNothing()
    {
        var config = TwoLayouts();

        Assert.Empty(TransferSummary.Changes(config, Config.Parse(config.ToJson())));
    }

    [Fact]
    public void SettingsTheAppDoesntNameAreCalledByTheirNameInTheFile()
    {
        Assert.Equal(S.SettingsOnlineUpdateHeader, TransferSummary.Title("onlineUpdateMinutes"));
        // §465: a setting gone is called by its name, as an older file writes it.
        Assert.Equal("hideTaskbar", TransferSummary.Title("hideTaskbar"));
        Assert.Equal("someNewSetting", TransferSummary.Title("someNewSetting"));
    }

    [Fact]
    public void ThePreviewSaysWhichDisplaysArentHereAndWhatIsMissing()
    {
        var imported = TwoLayouts();
        Files.AddPin(imported, @"Q:\Games\game.exe");
        var file = ConfigTransfer.Read(ConfigTransfer.Export(imported, When, "DESKTOP-A"));

        var preview = TransferSummary.Preview(TwoLayouts(), file, [@"\\?\DISPLAY#HOME#7"], new EmptyPc());

        Assert.Equal(["Office monitor"], preview.AbsentDisplays);
        Assert.Equal([@"C:\Windows\notepad.exe", @"Q:\Games\game.exe"], preview.Missing.Select(missing => missing.Target));
        Assert.Contains(preview.Notes, note => note.Contains("DESKTOP-A"));
        Assert.Contains(preview.Notes, note => note.Contains("Start with Windows"));
    }

    /// <summary>A PC with an empty C: drive and no apps.</summary>
    private sealed class EmptyPc : ITargetSystem
    {
        public bool FileExists(string path) => false;

        public bool DirectoryExists(string path) => path.Equals(@"C:\", StringComparison.OrdinalIgnoreCase);

        public DriveType? Drive(string root) => root.Equals(@"C:\", StringComparison.OrdinalIgnoreCase) ? DriveType.Fixed : null;

        public byte[]? ReadShortcut(string path) => null;

        public IReadOnlyList<string> UserSubKeys(string path) => [];

        public string Expand(string text) => text;

        public string Folder(Environment.SpecialFolder folder) => "";
    }
}
