// §339: "Back up and reset ›", a level: the settings app's settings file and Reset (its About page's two
// expanders) in the edit bar's ⚙ (the settings-in-the-dock plan's B6 to B10), the same words and the same
// ways (ConfigTransfer, TransferSummary, ConfigReset):
//
//  - **Export settings** — Windows' Save dialog, then every setting in one file (ConfigTransfer.ExportTo).
//  - **Import settings** — Windows' Open dialog, then Windows' task dialog saying what changes, what this
//    PC lacks and which displays aren't here (TransferSummary.Describe); Import replaces config.json
//    (ConfigTransfer.Import), the file there was kept beside it.
//  - **Open file location** — File Explorer at config.json, selected beside the copies an import and a
//    reset keep; its path under the row.
//  - **Reset layouts**, **Reset all settings** — asked first in Windows' dialog with the page's words
//    (what goes, what stays, where the file is kept), then ConfigReset.ResetFile.
//
// The dialogs are the dock's (KarpoBackup); afterwards ⚙ stands open on this level again, how each ended
// under its row ("Saved as …").

namespace Karpo.Core;

/// <summary>
/// §339: what Back up and reset does, the dock's: <paramref name="Export"/> and <paramref name="Import"/>
/// through Windows' dialogs, <paramref name="OpenFile"/> config.json opened, <paramref name="Reset"/> a
/// scope reset (asked already).
/// </summary>
public sealed record KarpoBackup(Action Export, Action Import, Action OpenFile, Action<ResetScope> Reset)
{
    /// <summary>The settings file's path, under Open file location.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>How each row's last act ended, by the row's key ("Saved as …"); a row with none says what it does.</summary>
    public IReadOnlyDictionary<string, string> Said { get; init; } = new Dictionary<string, string>();
}

/// <summary>§339: the backup's part of what ⚙ is given.</summary>
public sealed partial record KarpoServices
{
    /// <summary>What Back up and reset does (<see cref="KarpoBackup"/>); null for none, and then no such row.</summary>
    public KarpoBackup? Backup { get; init; }
}

public static partial class KarpoMenu
{
    /// <summary>Export settings' row.</summary>
    public const string ExportRow = "Export";

    /// <summary>Import settings' row.</summary>
    public const string ImportRow = "Import";

    /// <summary>Open file location's row.</summary>
    public const string ConfigFileRow = "ConfigFile";

    /// <summary>Reset layouts' row.</summary>
    public const string ResetLayoutsRow = "ResetLayouts";

    /// <summary>Reset all settings' row.</summary>
    public const string ResetAllRow = "ResetAll";

    // Segoe Fluent Icons: UpdateRestore (the About page's Reset), Upload.
    private const string GlyphRestore = "\uE777";
    private const string GlyphUpload = "\uE898";

    /// <summary>What Reset layouts asks first: the About page's title, what goes and what stays, and its button.</summary>
    public static DeleteQuestion ResetLayoutsQuestion => new(
        S.SettingsAboutResetLayoutsTitle,
        string.Join("\n\n", S.SettingsAboutResetLayoutsMessage, S.SettingsAboutResetKept),
        S.SettingsAboutResetLayoutsConfirm,
        S.SettingsCommonCancel,
        0);

    /// <summary>What Reset all settings asks first: the About page's title, what goes, that startup stays, where the file is kept.</summary>
    public static DeleteQuestion ResetAllQuestion => new(
        S.SettingsAboutResetAllTitle,
        string.Join("\n\n", S.SettingsAboutResetAllMessage, S.SettingsAboutImportStartupStays(S.SettingsGeneralStartupHeader), S.SettingsAboutResetKept),
        S.SettingsAboutResetAllConfirm,
        S.SettingsCommonCancel,
        0);

    /// <summary>"Back up and reset ›": what <paramref name="services"/>' backup does (see the top of the file); none where it isn't given.</summary>
    public static IReadOnlyList<OptionRow> Backup(Config config, KarpoServices services)
    {
        if (services.Backup is not { } backup)
        {
            return [];
        }
        string Said(string key, string otherwise) => backup.Said.TryGetValue(key, out var said) ? said : otherwise;
        OptionRow Act(string key, string title, string glyph, string why, Action act) => new(key, OptionRowKind.Action, title)
        {
            Glyph = glyph,
            Group = S.DockSettingsBackup,
            GroupGlyph = GlyphRestore,
            Why = Said(key, why),
            Set = _ =>
            {
                act();
                return false;
            },
        };
        return
        [
            Act(ExportRow, S.SettingsAboutExportHeader, GlyphUpload, S.SettingsAboutExportDescription, backup.Export),
            Act(ImportRow, S.SettingsAboutImportHeader, OptionGlyphs.Download, S.SettingsAboutImportDescription, backup.Import),
            Act(ConfigFileRow, S.DockWidgetMenuOpenFileLocation, OptionGlyphs.Open, backup.FilePath, backup.OpenFile),
            Act(ResetLayoutsRow, S.SettingsAboutResetLayoutsHeader, TaskbarLayouts.LayoutGlyph, S.SettingsAboutResetLayoutsDescription, () => backup.Reset(ResetScope.Layouts)) with { Ask = ResetLayoutsQuestion },
            Act(ResetAllRow, S.SettingsAboutResetAllHeader, OptionGlyphs.Reset, S.SettingsAboutResetAllDescription, () => backup.Reset(ResetScope.All)) with { Ask = ResetAllQuestion },
        ];
    }
}
