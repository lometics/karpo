// ⚙ › Back up and reset done the dock's way (Karpo.Dock\SettingsBackup.cs, docs\widgets.md §339), in the
// test run's own config folder, the dialogs answered here: an export read back by an import is the same
// settings, config.json replaced and the one there was kept beside it; an import cancelled at either
// dialog writes nothing; a file that isn't one is told and changes nothing; a reset of the layouts asked
// already writes a new install's layouts and keeps the rest; each says how it went in the About page's words.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class SettingsBackupTests : IDisposable
{
    private static readonly DateTimeOffset When = new(2026, 10, 23, 18, 30, 0, TimeSpan.FromHours(2));

    private readonly string folder = Path.Combine(Isolation.Folder, "settings-backup", Guid.NewGuid().ToString("N"));

    public SettingsBackupTests()
    {
        Files.Reset();
        Directory.CreateDirectory(folder);
    }

    public void Dispose() => Files.Reset();

    /// <summary>The dialogs, answered: the Save dialog's and the Open dialog's path, the import's question; what was asked and told kept.</summary>
    private sealed class Answers(string? save = null, string? open = null, bool import = true) : IBackupDialogs
    {
        public List<string> Asked { get; } = [];

        public List<(string Title, string Message)> Told { get; } = [];

        public ImportPreview? Preview { get; private set; }

        public string? SavePath(HWND owner, string name)
        {
            Asked.Add("save " + name);
            return save;
        }

        public string? OpenPath(HWND owner)
        {
            Asked.Add("open");
            return open;
        }

        public bool ConfirmImport(HWND owner, ImportPreview preview)
        {
            Asked.Add("import");
            Preview = preview;
            return import;
        }

        public void Tell(HWND owner, string title, string message) => Told.Add((title, message));
    }

    private static readonly (string Id, string Name)[] Here = [(@"\\?\DISPLAY#HOME#7", "Living room TV")];

    /// <summary>
    /// Exported from one config and imported over another: the Save dialog offers "Karpo settings &lt;date&gt;",
    /// the file is the export; the import says what changes, replaces config.json with the exported settings
    /// (start with Windows staying this PC's), keeps the file there was beside it, and says so.
    /// </summary>
    [Fact]
    public void AnExportImportedBackIsTheSameSettings()
    {
        var exported = Files.Start(start => start.TaskbarOpacity = 40);
        var path = Path.Combine(folder, "karpo.json");
        var saving = new Answers(save: path);

        var export = SettingsBackup.Export(exported, saving, default, When, "DESKTOP-A");

        Assert.Equal(["save " + S.SettingsAboutExportFileName("2026-10-23")], saving.Asked);
        Assert.Equal(new SettingsBackup.Outcome(KarpoMenu.ExportRow, S.SettingsAboutExportSavedAs(path)), export);
        Assert.Equal(ConfigTransfer.Export(exported, When, "DESKTOP-A"), File.ReadAllText(path));

        var now = Files.Start(start =>
        {
            start.TaskbarOpacity = 90;
            start.StartWithWindows = true;
        });
        var opening = new Answers(open: path);
        var import = SettingsBackup.Import(now, opening, default, Here, PinTargets.System);

        Assert.Equal(["open", "import"], opening.Asked);
        Assert.Contains(opening.Preview!.Changes, change => change.Contains(TransferSummary.Title("layouts"), StringComparison.Ordinal));
        Assert.NotNull(import?.Written);
        Assert.Equal(KarpoMenu.ImportRow, import.Key);
        var kept = Directory.GetFiles(Config.Folder, "config.json.before-import-*").Single();
        Assert.Equal(S.SettingsAboutImportInUseKept(Path.GetFileName(kept)), import.Said);
        Assert.Equal(90, Config.Parse(File.ReadAllText(kept)).TaskbarOpacity);
        var file = Config.Load();
        Assert.Equal(40, file.TaskbarOpacity);
        Assert.True(file.StartWithWindows);
        Assert.Equal(import.Written.ToJson(), file.ToJson());
        Assert.Empty(opening.Told);
    }

    /// <summary>
    /// An import cancelled in the Open dialog or at its question, a Save dialog cancelled: nothing written,
    /// nothing said. A file that isn't a settings file is told as the About page told it, and changes nothing.
    /// </summary>
    [Fact]
    public void ACancelledOrUnreadableImportChangesNothing()
    {
        var now = Files.Start(start => start.TaskbarOpacity = 90);
        var before = File.ReadAllText(Config.FilePath);
        var path = Path.Combine(folder, "karpo.json");
        ConfigTransfer.ExportTo(path, new Config(), When, "DESKTOP-A");

        Assert.Null(SettingsBackup.Export(now, new Answers(), default, When, "DESKTOP-A"));
        Assert.Null(SettingsBackup.Import(now, new Answers(), default, Here, PinTargets.System));
        var declined = new Answers(open: path, import: false);
        Assert.Null(SettingsBackup.Import(now, declined, default, Here, PinTargets.System));
        Assert.Equal(["open", "import"], declined.Asked);

        var junk = Path.Combine(folder, "junk.json");
        File.WriteAllText(junk, "not json");
        var unreadable = new Answers(open: junk);
        Assert.Null(SettingsBackup.Import(now, unreadable, default, Here, PinTargets.System));
        Assert.Equal(["open"], unreadable.Asked);
        Assert.Equal(S.SettingsAboutImportFailed, Assert.Single(unreadable.Told).Title);

        Assert.Equal(before, File.ReadAllText(Config.FilePath));
        Assert.Empty(Directory.GetFiles(Config.Folder, "config.json.before-*"));
    }

    /// <summary>
    /// Reset layouts, asked already: config.json's layouts a new install's, every other setting kept, the
    /// file there was beside it; it says "Layouts reset" and where the old file is, the config as written
    /// for the dock to take.
    /// </summary>
    [Fact]
    public void AResetOfTheLayoutsWritesANewInstallsAndSaysWhereTheOldFileIs()
    {
        var now = Files.Start(start =>
        {
            start.TaskbarOpacity = 55;
            start.Layouts[0].Name = "Mine";
        });

        var reset = SettingsBackup.Reset(now, ResetScope.Layouts, new Answers(), default);

        Assert.NotNull(reset?.Written);
        var kept = Directory.GetFiles(Config.Folder, "config.json.before-reset-*").Single();
        Assert.Equal(
            (KarpoMenu.ResetLayoutsRow, S.SettingsAboutResetInUseKept(Path.GetFileName(kept))),
            (reset.Key, reset.Said));
        var file = Config.Load();
        // §345, §346: the layout's behaviour goes with it, its folders' too; §567 how it looks is Karpo's Appearance, kept.
        Assert.Equal(55, file.TaskbarOpacity);
        Assert.DoesNotContain(file.Layouts, layout => layout.Name == "Mine");
        Assert.Equal(file.ToJson(), reset.Written.ToJson());
    }
}
