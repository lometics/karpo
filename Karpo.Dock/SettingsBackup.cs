// ⚙ › Back up and reset done the dock's way (§339; the rows are Karpo.Core\KarpoMenu.Backup.cs): Windows'
// Save dialog, then the export written (ConfigTransfer.ExportTo); Windows' Open dialog, the file read and
// what importing it would do said in Windows' task dialog (TransferSummary.Read, Describe), then the import
// (ConfigTransfer.Import); a reset, asked already, written (ConfigReset.ResetFile). Each ends in an
// Outcome: the config as written for the dock to take, and how it went in the settings app's words (its
// info bar's message) for under the act's row. What went wrong is told in the task dialog, as the
// settings app's dialog told it; a cancel says nothing and changes nothing.
//
// Plain code over IBackupDialogs, so a test answers instead of the dialogs showing.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>§339: the dialogs Back up and reset shows (<see cref="WindowsBackupDialogs"/>); a test answers instead.</summary>
internal interface IBackupDialogs
{
    /// <summary>Windows' Save dialog for a settings file, <paramref name="name"/> in its name box: the path chosen, null for cancelled.</summary>
    string? SavePath(HWND owner, string name);

    /// <summary>Windows' Open dialog for a settings file: the path chosen, null for cancelled.</summary>
    string? OpenPath(HWND owner);

    /// <summary>What importing would do (<paramref name="preview"/>), asked: true where Import was chosen.</summary>
    bool ConfirmImport(HWND owner, ImportPreview preview);

    /// <summary>What went wrong, said.</summary>
    void Tell(HWND owner, string title, string message);
}

/// <summary>§339: Windows' own: the Save and Open dialogs (SaveDialog), the task dialog (TaskDialogs).</summary>
internal sealed class WindowsBackupDialogs : IBackupDialogs
{
    public string? SavePath(HWND owner, string name) => SaveDialog.Save(owner, name, S.SettingsAboutExportFileType, ".json");

    public string? OpenPath(HWND owner) => SaveDialog.Open(owner, S.SettingsAboutExportFileType, ".json");

    public bool ConfirmImport(HWND owner, ImportPreview preview)
    {
        var (content, details) = TransferSummary.Describe(preview);
        return TaskDialogs.Confirm(owner, S.SettingsAboutImportTitle, content, details, S.SettingsAboutImportConfirm, S.SettingsCommonCancel);
    }

    public void Tell(HWND owner, string title, string message) => TaskDialogs.Tell(owner, title, message);
}

/// <summary>§339: Back up and reset's acts; see the top of the file.</summary>
internal static class SettingsBackup
{
    /// <summary>
    /// How an act ended: the config as written (<paramref name="Written"/>, which the dock takes as it is,
    /// unsaved: the file is it already; null for nothing written) and the sentence under the act's row
    /// <paramref name="Key"/> (<paramref name="Said"/>).
    /// </summary>
    internal sealed record Outcome(string Key, string Said, Config? Written = null);

    /// <summary>
    /// Export settings: Windows' Save dialog, "Karpo settings &lt;date&gt;" in its name box, then
    /// <paramref name="config"/> written there. Null where it was cancelled, or failed (told).
    /// </summary>
    public static Outcome? Export(Config config, IBackupDialogs dialogs, HWND owner, DateTimeOffset now, string computer)
    {
        var name = S.SettingsAboutExportFileName(now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        if (dialogs.SavePath(owner, name) is not { Length: > 0 } path)
        {
            return null;
        }
        try
        {
            ConfigTransfer.ExportTo(path, config, now, computer);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            dialogs.Tell(owner, S.SettingsAboutExportFailed, error.Message);
            return null;
        }
        return new Outcome(KarpoMenu.ExportRow, S.SettingsAboutExportSavedAs(path));
    }

    /// <summary>
    /// Import settings: Windows' Open dialog, the file read and what importing it over <paramref name="current"/>
    /// would do on this PC (its displays <paramref name="connected"/>) asked, then config.json replaced. Null
    /// where it was cancelled at either step, or failed (told): nothing written then.
    /// </summary>
    public static Outcome? Import(Config current, IBackupDialogs dialogs, HWND owner, IReadOnlyList<(string Id, string Name)> connected, ITargetSystem system)
    {
        if (dialogs.OpenPath(owner) is not { Length: > 0 } path)
        {
            return null;
        }
        var (file, preview, problem) = TransferSummary.Read(path, current, [.. connected.Select(display => display.Id)], system);
        if (file is null || preview is null)
        {
            dialogs.Tell(owner, S.SettingsAboutImportFailed, problem ?? S.SettingsAboutImportUnreadable);
            return null;
        }
        if (!dialogs.ConfirmImport(owner, preview))
        {
            return null;
        }
        Config written;
        string? kept;
        try
        {
            written = ConfigTransfer.Import(file, current, connected, out kept);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            dialogs.Tell(owner, S.SettingsAboutImportFailed, error.Message);
            return null;
        }
        return new Outcome(KarpoMenu.ImportRow, kept is null ? S.SettingsAboutImportInUse : S.SettingsAboutImportInUseKept(Path.GetFileName(kept)), written);
    }

    /// <summary>
    /// Reset layouts, Reset all settings — asked already (KarpoMenu.ResetLayoutsQuestion, ResetAllQuestion) —:
    /// config.json made what a new install has for <paramref name="scope"/> (ConfigReset.ResetFile), the
    /// file there was kept beside it. Null where it failed (told).
    /// </summary>
    public static Outcome? Reset(Config current, ResetScope scope, IBackupDialogs dialogs, HWND owner)
    {
        Config written;
        string? kept;
        try
        {
            written = ConfigReset.ResetFile(current, scope, out kept);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            dialogs.Tell(owner, S.SettingsAboutResetFailed, error.Message);
            return null;
        }
        return new Outcome(
            scope == ResetScope.All ? KarpoMenu.ResetAllRow : KarpoMenu.ResetLayoutsRow,
            kept is null ? S.SettingsAboutResetInUse : S.SettingsAboutResetInUseKept(Path.GetFileName(kept)),
            written);
    }
}
