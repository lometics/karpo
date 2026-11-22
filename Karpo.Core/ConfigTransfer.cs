// Settings taken to another PC: config.json in a file of its own, with a header
// saying what it is, and what an import does before it replaces anything.
//
// The file holds the settings only. Caches, the notification state and the
// taskbar state live in %LOCALAPPDATA%\Karpo and stay where they are. Display ids differ between PCs (they name the port a
// display hangs on), so an import keeps the imported displays' entries, whose
// layouts come back if that display is ever connected here, and gives each
// display connected here that it doesn't know the imported layout for new
// displays, as the dock would when it sees a display for the first time.

using System.Text;
using System.Text.Json;

namespace Karpo.Core;

/// <summary>A settings file read for importing.</summary>
/// <param name="Config">The settings in it, read and repaired.</param>
/// <param name="Version">The export format's version; null for a plain config.json.</param>
/// <param name="Computer">The PC it was exported on, when it says.</param>
/// <param name="Exported">When it was exported, when it says.</param>
public sealed record SettingsFile(Config Config, int? Version, string? Computer, DateTimeOffset? Exported)
{
    /// <summary>Made by a newer Karpo, whose settings this one may not all know.</summary>
    public bool Newer => Version > ConfigTransfer.Version;
}

public static class ConfigTransfer
{
    /// <summary>What an export's <c>format</c> says.</summary>
    public const string Format = "karpo-settings";

    /// <summary>The export format this Karpo writes: the header's <c>version</c>.</summary>
    public const int Version = 1;

    /// <summary>
    /// The export file's text for <paramref name="config"/>: a header (format, version, when, which
    /// PC) and the config as config.json has it.
    /// </summary>
    public static string Export(Config config, DateTimeOffset exported, string computer)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", Format);
            writer.WriteNumber("version", Version);
            writer.WriteString("exported", exported);
            writer.WriteString("computer", computer);
            writer.WritePropertyName("config");
            using (var document = JsonDocument.Parse(config.ToJson()))
            {
                document.RootElement.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    /// <summary>
    /// Reads a settings file: an export (its header checked), or a plain config.json copied from
    /// another PC. Throws <see cref="FormatException"/> with a sentence for the person when the
    /// file is neither.
    /// </summary>
    public static SettingsFile Read(string text)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text.TrimStart('\uFEFF'), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        }
        catch (JsonException)
        {
            throw new FormatException("The file isn't a Karpo settings file: it isn't JSON, or it was cut short.");
        }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("The file isn't a Karpo settings file.");
            }
            if (root.TryGetProperty("format", out var format))
            {
                if (format.ValueKind != JsonValueKind.String || format.GetString() != Format)
                {
                    throw new FormatException("The file isn't a Karpo settings file.");
                }
                if (!root.TryGetProperty("config", out var inside) || inside.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException("The settings file has no settings in it.");
                }
                int? version = root.TryGetProperty("version", out var number) && number.TryGetInt32(out var value) ? value : null;
                string? computer = root.TryGetProperty("computer", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
                DateTimeOffset? exported = root.TryGetProperty("exported", out var when) && when.TryGetDateTimeOffset(out var time) ? time : null;
                return new SettingsFile(Settings(inside.GetRawText()), version, computer, exported);
            }
            // A config.json as it is: it has to look like one.
            if (!root.EnumerateObject().Any(property => ConfigKeys.Contains(property.Name)))
            {
                throw new FormatException("The file isn't a Karpo settings file.");
            }
            return new SettingsFile(Settings(root.GetRawText()), null, null, null);
        }
    }

    /// <summary>
    /// Writes the export of <paramref name="config"/> (<see cref="Export"/>) to <paramref name="path"/>, as
    /// the settings app's Export and the edit bar's ⚙ › Back up and reset › Export settings write it (§339).
    /// Throws <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/> where it can't be written.
    /// </summary>
    public static void ExportTo(string path, Config config, DateTimeOffset exported, string computer) =>
        File.WriteAllText(path, Export(config, exported, computer));

    /// <summary>
    /// Makes the settings in <paramref name="file"/> this PC's, over <paramref name="current"/>: whether
    /// Karpo starts with Windows stays this PC's own choice, the displays <paramref name="connected"/>
    /// here that the file doesn't know get its layout for new displays (<see cref="Adopt"/>), and
    /// config.json is replaced whole (<see cref="Config.Replace(Config, out string?)"/>, the file there
    /// was kept beside it: <paramref name="kept"/>). Returns the config as written. The settings app's
    /// Import and ⚙'s (§339) alike. Throws <see cref="IOException"/> where it can't be written.
    /// </summary>
    public static Config Import(SettingsFile file, Config current, IEnumerable<(string Id, string Name)> connected, out string? kept)
    {
        var imported = file.Config.Clone();
        imported.StartWithWindows = current.StartWithWindows;
        Adopt(imported, connected);
        return Config.Replace(imported, out kept);
    }

    /// <summary>
    /// Makes <paramref name="imported"/> ready for this PC's <paramref name="connected"/> displays: each
    /// one it has no entry for gets one now, showing the imported layout for new displays, as the dock
    /// gives a display it sees for the first time. Entries for displays that aren't connected are
    /// kept: their layouts come back when such a display is.
    /// </summary>
    public static void Adopt(Config imported, IEnumerable<(string Id, string Name)> connected)
    {
        foreach (var (id, name) in connected)
        {
            imported.AddDisplay(id, name);
        }
    }

    /// <summary>Keys a config.json has at least one of, so that it is recognised as Karpo's.</summary>
    private static readonly HashSet<string> ConfigKeys = new(StringComparer.Ordinal) { "version", "layouts", "screens" };

    private static Config Settings(string json)
    {
        Config config;
        try
        {
            config = Config.Parse(json);
        }
        catch (JsonException error)
        {
            throw new FormatException($"The settings in the file don't read: {error.Message}");
        }
        config.Repair();
        return config;
    }
}
