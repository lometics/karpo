using System.Text.Json;

namespace Karpo.Tests.Core;

/// <summary>Configs from JSON text, the way <see cref="Config.Load"/> reads them, without touching a file.</summary>
internal static class Configs
{
    /// <summary>Read and repaired.</summary>
    public static Config Repaired(string json) => Repaired(json, out _);

    /// <summary>Read and repaired; <paramref name="changed"/> is what <see cref="Config.Repair"/> said.</summary>
    public static Config Repaired(string json, out bool changed)
    {
        var config = Config.Parse(json);
        changed = config.Repair();
        return config;
    }

    /// <summary>What the other app reads after <paramref name="config"/> was saved: written, read and repaired.</summary>
    public static Config RoundTrip(Config config, out bool changed) => Repaired(config.ToJson(), out changed);

    /// <summary>A kind by its name in the file.</summary>
    public static BarKind Kind(string name) => JsonSerializer.Deserialize<BarKind>($"\"{name}\"");

    /// <summary>The islands of the layout new displays get.</summary>
    public static List<BarItem> Islands(Config config) => Arranged(config.DefaultLayout.Islands);

    /// <summary>
    /// <paramref name="islands"/> as a list. A bar used to be given a spacer at its end by every
    /// repair, which this left out again; bars have sections now and keep no spacer, so it is the
    /// list as it is, kept so the tests that read a repaired bar through it read the same.
    /// </summary>
    public static List<BarItem> Arranged(IEnumerable<BarItem> islands) => islands as List<BarItem> ?? [.. islands];

    /// <summary>
    /// A config whose one layout, "Default", holds <paramref name="islands"/>, in the sections they
    /// carry — a config Karpo would itself have written, so a round trip through
    /// <see cref="Config.Repair"/> changes nothing where the islands are in their places' order. One
    /// with no row stands in the main row.
    /// </summary>
    public static Config With(params BarItem[] islands)
    {
        var config = new Config
        {
            Layouts = [new TaskbarLayout
            {
                Id = TaskbarLayouts.DefaultId,
                Name = TaskbarLayouts.DefaultName,
                Islands = [.. islands],
            }],
            DefaultLayoutId = TaskbarLayouts.DefaultId,
        };
        // And the look a config that never chose wears (Config.DefaultStyle): a config Karpo would
        // have written carries the answer, so a round trip changes nothing.
        config.Style = config.StyleOrDefault;
        return config;
    }

    /// <summary>The top-level property names the file gets.</summary>
    public static IReadOnlyList<string> Keys(Config config)
    {
        using var document = JsonDocument.Parse(config.ToJson());
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToList();
    }

    /// <summary>The property names each island of the first layout gets in the file.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> IslandKeys(Config config)
    {
        using var document = JsonDocument.Parse(config.ToJson());
        return document.RootElement.GetProperty("layouts")[0].GetProperty("islands").EnumerateArray()
            .Select(island => (IReadOnlyList<string>)island.EnumerateObject().Select(property => property.Name).ToList())
            .ToList();
    }

    public static BarKind[] Kinds(IEnumerable<BarItem> list) => list.Select(island => island.Kind).ToArray();

    /// <summary>
    /// A list folder holding <paramref name="lines"/>: where a timer, a focus session or a stopwatch
    /// stands now that none is an entry of the taskbar (docs\widgets.md §92).
    /// </summary>
    public static BarItem Holding(params CellItem[] lines)
    {
        var folder = FolderKind(FolderView.List).New();
        foreach (var line in lines)
        {
            folder.Folder!.Put(line);
        }
        return folder;
    }

    /// <summary>The first object named <paramref name="name"/> anywhere under <paramref name="element"/> in a file, or null.</summary>
    public static JsonElement? Find(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == name)
                    {
                        return property.Value;
                    }
                    if (Find(property.Value, name) is { } found)
                    {
                        return found;
                    }
                }
                return null;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (Find(item, name) is { } found)
                    {
                        return found;
                    }
                }
                return null;
            default:
                return null;
        }
    }

    /// <summary>The folder kind that opens as <paramref name="view"/>, as the registry says it.</summary>
    public static BarKind FolderKind(FolderView view) =>
        BarEntries.All.Single(entry => entry.Opens == view).Kind;
}
