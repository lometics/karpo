// What importing a settings file would do, said before anything is replaced:
// the settings that change, the pinned apps, files and folders this PC doesn't
// have (kept all the same, and shown as broken), and the displays the file
// knows that aren't connected here (their widgets come back when they are).
// The settings app's import dialog says it, and the edit bar's ⚙ › Back up and
// reset › Import settings in Windows' task dialog (Describe, §339).

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Karpo.Core;

/// <summary>What the import dialog shows.</summary>
/// <param name="Changes">One line per setting or list that changes.</param>
/// <param name="Missing">Pins this PC doesn't have.</param>
/// <param name="AbsentDisplays">The names of the displays the file has widgets for that aren't connected here.</param>
/// <param name="Notes">Anything else worth knowing first, such as a file from a newer Karpo.</param>
public sealed record ImportPreview(
    IReadOnlyList<string> Changes,
    IReadOnlyList<MissingTarget> Missing,
    IReadOnlyList<string> AbsentDisplays,
    IReadOnlyList<string> Notes);

public static class TransferSummary
{
    /// <summary>
    /// What importing <paramref name="file"/> over <paramref name="current"/> would do on this PC, whose
    /// displays are <paramref name="connected"/> (ids).
    /// </summary>
    public static ImportPreview Preview(Config current, SettingsFile file, IReadOnlyCollection<string> connected, ITargetSystem system)
    {
        var notes = new List<string>();
        if (file.Newer)
        {
            notes.Add(S.SettingsAboutImportNewer);
        }
        if (file.Computer is { Length: > 0 } computer)
        {
            // The date as the region writes it, as Windows' own dialogs do.
            notes.Add(file.Exported is { } when
                ? S.SettingsAboutImportExportedFromOn(computer: computer, date: when.LocalDateTime.ToString("g", CultureInfo.CurrentCulture))
                : S.SettingsAboutImportExportedFrom(computer));
        }
        notes.Add(S.SettingsAboutImportStartupStays(S.SettingsGeneralStartupHeader));
        var absent = file.Config.Screens
            .Where(screen => !connected.Contains(screen.Id, StringComparer.OrdinalIgnoreCase))
            .Select(screen => string.IsNullOrWhiteSpace(screen.Name) ? S.SettingsAboutImportADisplay : screen.Name)
            .ToList();
        return new ImportPreview(Changes(current, file.Config), PinTargets.Missing(file.Config, system), absent, notes);
    }

    /// <summary>
    /// Reads the settings file at <paramref name="path"/> and says what importing it over
    /// <paramref name="current"/> would do on this PC (<see cref="Preview"/>). When it can't be read or
    /// isn't one, the file and the preview are null and the problem says why.
    /// </summary>
    public static (SettingsFile? File, ImportPreview? Preview, string? Problem) Read(string path, Config current, IReadOnlyCollection<string> connected, ITargetSystem system)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return (null, null, error.Message);
        }
        try
        {
            var file = ConfigTransfer.Read(text);
            return (file, Preview(current, file, connected, system), null);
        }
        catch (FormatException error)
        {
            return (null, null, error.Message);
        }
    }

    /// <summary>How many lines of a list the task dialog shows before "and N more", as the settings app's dialog shows.</summary>
    public const int Shown = 12;

    /// <summary>
    /// <paramref name="preview"/> as Windows' task dialog says it (§339): what changes as its content, a
    /// line each (or that nothing does); what this PC lacks, which displays aren't here and what is good
    /// to know as its details, each under its heading, as the settings app's dialog lists them. A list
    /// of more than <see cref="Shown"/> ends in "and N more".
    /// </summary>
    public static (string Content, string Details) Describe(ImportPreview preview)
    {
        static List<string> Lines(IReadOnlyList<string> lines)
        {
            List<string> said = [.. lines.Take(Shown).Select(line => "• " + line)];
            if (lines.Count > Shown)
            {
                said.Add(S.SettingsAboutImportMore(lines.Count - Shown));
            }
            return said;
        }
        static string? Section(string title, IReadOnlyList<string> lines, string? after = null) =>
            lines.Count == 0 ? null : string.Join("\n", [title, .. Lines(lines), .. after is null ? [] : new[] { after }]);
        var content = string.Join("\n", Lines(preview.Changes.Count > 0 ? preview.Changes : [S.SettingsAboutImportNoChange]));
        string?[] details =
        [
            Section(S.SettingsAboutImportMissing, [.. preview.Missing.Select(missing => S.SettingsAboutImportMissingLine(target: missing.Target, where: missing.Where, reason: missing.Reason))], S.SettingsAboutImportMissingKept),
            Section(S.SettingsAboutImportAbsent, preview.AbsentDisplays, S.SettingsAboutImportAbsentKept),
            Section(S.SettingsAboutImportNotes, [.. preview.Notes, S.SettingsAboutImportBackup]),
        ];
        return (content, string.Join("\n\n", details.OfType<string>()));
    }

    /// <summary>
    /// The settings that differ between <paramref name="current"/> and <paramref name="imported"/>, as
    /// the settings app names them: values with before and after, lists and objects as changed, and
    /// how many pins there are. Keys the model gains later are listed too, by their name in the file.
    /// </summary>
    public static IReadOnlyList<string> Changes(Config current, Config imported)
    {
        var before = Root(current);
        var after = Root(imported);
        var lines = new List<string>();
        var keys = after.Select(property => property.Key).Concat(before.Select(property => property.Key)).Distinct().ToList();
        foreach (var key in keys)
        {
            // Machine's own: displays are summed up apart, and startup stays. The file's own
            // bookkeeping is nothing the person set.
            if (key is "screens" or "startWithWindows" or "version")
            {
                continue;
            }
            before.TryGetPropertyValue(key, out var old);
            after.TryGetPropertyValue(key, out var fresh);
            if (JsonNode.DeepEquals(old, fresh))
            {
                continue;
            }
            var line = old is JsonValue && fresh is JsonValue
                ? S.SettingsAboutImportChangeLine(setting: Title(key), before: Value(old), after: Value(fresh))
                : S.SettingsAboutImportChanged(Title(key));
            lines.Add(line);
        }
        // The pins are widgets of the displays, and in their folders; how many there are says the most.
        var (pinsBefore, pinsAfter) = (Pins(current), Pins(imported));
        if (pinsBefore != pinsAfter)
        {
            lines.Add(S.SettingsAboutImportPins(before: pinsBefore, after: pinsAfter));
        }
        return lines;
    }

    /// <summary>The different targets pinned on <paramref name="config"/>'s displays, on the bars and in folders.</summary>
    private static int Pins(Config config) =>
        Folders.All(config.AllIslands)
            .Where(entry => entry.Island.Info == WidgetInfos.Pin && entry.Island.Pin is { Target.Length: > 0 })
            .Select(entry => entry.Island.Pin!.Target)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

    private static JsonObject Root(Config config) =>
        JsonNode.Parse(ConfigTransfer.Export(config, DateTimeOffset.UnixEpoch, ""))!["config"]!.AsObject();

    private static string Value(JsonNode? value)
    {
        if (value is null)
        {
            return S.SettingsAboutImportNone;
        }
        return value.GetValueKind() switch
        {
            JsonValueKind.True => S.SettingsAboutImportOn,
            JsonValueKind.False => S.SettingsAboutImportOff,
            JsonValueKind.String => value.GetValue<string>() is { Length: > 0 } text ? text : S.SettingsAboutImportNone,
            _ => value.ToJsonString(),
        };
    }

    /// <summary>
    /// What the settings app calls a setting, by its key in the file: the card's own header, so the
    /// summary says what the page says. A key this version has no card for is shown as the file
    /// writes it, a name rather than words.
    /// </summary>
    public static string Title(string key) => key switch
    {
        "recentColors" => S.SettingsAppearanceCustomRecent,
        "formats" => S.SettingsLocalizationFormatsHeader,
        "language" => S.SettingsLanguageHeader,
        "onlineUpdateMinutes" => S.SettingsOnlineUpdateHeader,
        "searchEngine" => S.SettingsSearchEngineHeader,
        "searchCustomUrl" => S.SettingsSearchAddressHeader,
        "mapProvider" => S.SettingsMapProviderHeader,
        "weatherProvider" => S.SettingsLinksWeatherHeader,
        "marketsProvider" => S.SettingsLinksMarketsHeader,
        "layouts" => S.SettingsLayoutsHeader,
        "defaultLayoutId" => S.SettingsLayoutsDefaultHeader,
        "animation" => S.SettingsGeneralAnimationsHeader,
        _ => key,
    };
}
