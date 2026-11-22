// tray: the list of notification icons the dock writes next to config.json (tray-icons.json).
//
// One entry per live icon, in the order the tray has them: the identity the config names it by,
// the first line of its tooltip, its app's executable, the key of Windows' own record of it under
// HKCU\Control Panel\NotifyIconSettings and Windows' own choice for it (IsPromoted). The dock reads
// the identities back at its next start to keep the icons' order (Dock.LastTrayOrder). The settings app
// listed the icons from it on General until §261; where each shows is the tray's menu's now
// (OptionTray), from the live icons.

using System.Text.Json;

namespace Karpo.Core;

/// <summary>One icon in tray-icons.json.</summary>
/// <param name="Key">Its identity (<see cref="TrayIdentity"/>), what a tray's <see cref="TrayIconOptions.States"/> names it by.</param>
/// <param name="Name">The first line of its tooltip, "Tray icon" where it has none.</param>
/// <param name="Path">Its app's executable, where it is known.</param>
/// <param name="Record">The name of Windows' own record of it under NotifyIconSettings; empty where Windows keeps none.</param>
/// <param name="Promoted">Windows' own choice for it: on the taskbar (IsPromoted), or in the hidden icon menu.</param>
public sealed record TrayCatalogueEntry(string Key, string Name, string Path = "", string Record = "", bool Promoted = false);

public static class TrayCatalogue
{
    /// <summary>The file's name in the config folder.</summary>
    public const string FileName = "tray-icons.json";

    /// <summary>What a tray icon with no tooltip is called.</summary>
    public static string NoName => S.CoreTrayNoName;

    /// <summary>The file's text for <paramref name="icons"/>, in their order.</summary>
    public static string ToJson(IEnumerable<TrayCatalogueEntry> icons)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var icon in icons)
            {
                writer.WriteStartObject();
                writer.WriteString("Key", icon.Key);
                writer.WriteString("Name", string.IsNullOrWhiteSpace(icon.Name) ? NoName : icon.Name);
                writer.WriteString("Path", icon.Path ?? "");
                writer.WriteString("Record", icon.Record ?? "");
                writer.WriteBoolean("Promoted", icon.Promoted);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>
    /// The icons in <paramref name="text"/>, in order; an entry without a key is left out, and one
    /// without a path or a record reads with none. Throws
    /// <see cref="JsonException"/> for text that isn't such a list.
    /// </summary>
    public static List<TrayCatalogueEntry> Parse(string text)
    {
        var icons = new List<TrayCatalogueEntry>();
        using var document = JsonDocument.Parse(text);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("tray-icons.json is not a list");
        }
        foreach (var icon in document.RootElement.EnumerateArray())
        {
            if (icon.ValueKind != JsonValueKind.Object || Text(icon, "Key") is not { Length: > 0 } key)
            {
                continue;
            }
            icons.Add(new TrayCatalogueEntry(
                key,
                Text(icon, "Name") is { Length: > 0 } name && !string.IsNullOrWhiteSpace(name) ? name : NoName,
                Text(icon, "Path") ?? "",
                Text(icon, "Record") ?? "",
                icon.TryGetProperty("Promoted", out var promoted) && promoted.ValueKind == JsonValueKind.True));
        }
        return icons;
    }

    /// <summary>
    /// The dock writes <paramref name="icons"/> into <paramref name="folder"/>, the config's
    /// (<see cref="Config.Folder"/>, which KARPO_CONFIG_DIR moves), where the settings app reads them
    /// (<see cref="Read"/>): the one place both name, so the two never look in different folders.
    /// Throws what writing the file throws.
    /// </summary>
    public static void Write(string folder, IEnumerable<TrayCatalogueEntry> icons)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(System.IO.Path.Combine(folder, FileName), ToJson(icons));
    }

    /// <summary>
    /// The icons the dock last wrote into <paramref name="folder"/>: none where there is no file yet
    /// (the dock hasn't run), and null where it can't be read now — one being written, or broken —
    /// so a reader keeps what it showed.
    /// </summary>
    public static List<TrayCatalogueEntry>? Read(string folder)
    {
        try
        {
            using var stream = new FileStream(System.IO.Path.Combine(folder, FileName), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement icon, string name) =>
        icon.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
