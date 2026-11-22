// The file's shape (Config.Shape) held to the model (the person, 2026-10-31: "i dont want people complaining
// ... that a settings file get overwritten").
//
// An older Karpo keeps a newer one's config.json whole by never writing it: a file that says a newer shape is
// read, shown and left alone (Config.FromNewerKarpo; Config.Save refuses it, and refuses to merge into it).
// That only holds while a newer build's file says a newer shape whenever it may hold something an older build
// doesn't know. Read by the older build, a property it has no property for is read past, a word it doesn't
// know — a widget of a new kind, a new choice of an enum — reads as nothing (Config.Repair drops the widget,
// sets the default), and its next save writes the file without them. Keeping what it doesn't know instead was
// tried and set aside (2026-10-31): a key this build doesn't have is also how a key Karpo retired reads, and
// those are meant to leave the file (RemovedKindsTests, TaskbarAppsTests, FolderModelTests).
//
// So every property and every word config.json may hold is listed in ConfigWords.txt under the shape it
// belongs to, and one added without the shape raised fails here. One taken away needs no new shape (an older
// build reading the newer file finds nothing it would drop), only the list written again.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Karpo.Tests.Persistence;

public sealed class ConfigSchemaTests
{
    private const string WordsFile = "ConfigWords.txt";

    /// <summary>Set to 1 to write ConfigWords.txt again from the model as it is, after raising the shape where something was added.</summary>
    private const string WriteVariable = "KARPO_WRITE_CONFIG_WORDS";

    /// <summary>
    /// What config.json may hold — every object's properties, every enum's words, every folder line's
    /// kind, style and reading, every folder face's kind — is ConfigWords.txt while the shape is the one
    /// it names. Something added needs the shape raised: an older build would drop it and save the file
    /// without it, where a newer shape has it leave the file alone.
    /// </summary>
    [Fact]
    public void WhatTheFileMayHoldIsAddedOnlyWithANewShape()
    {
        var now = Words();
        var path = Path.Combine(RepoSource.Root(), "Karpo.Tests", "Persistence", WordsFile);
        if (Environment.GetEnvironmentVariable(WriteVariable) == "1")
        {
            File.WriteAllLines(path, [$"shape {Config.Shape}", .. now]);
        }
        var lines = File.ReadAllLines(path);
        var shape = int.Parse(lines[0]["shape ".Length..], System.Globalization.CultureInfo.InvariantCulture);
        var listed = lines.Skip(1).Where(line => line.Length > 0).ToHashSet(StringComparer.Ordinal);
        var added = now.Where(word => !listed.Contains(word)).ToList();
        var gone = listed.Where(word => !now.Contains(word)).ToList();
        if (shape == Config.Shape)
        {
            Assert.True(added.Count == 0,
                $"config.json may hold what an older Karpo of shape {Config.Shape} doesn't know, and would drop from it: {string.Join(", ", added)}. " +
                $"Raise Config.Shape, then write {WordsFile} again ({WriteVariable}=1).");
        }
        Assert.True(shape == Config.Shape && gone.Count == 0 && added.Count == 0,
            $"{WordsFile} is of shape {shape}, the model of shape {Config.Shape}; write it again ({WriteVariable}=1). " +
            $"Added: {string.Join(", ", added)}. Gone: {string.Join(", ", gone)}.");
    }

    /// <summary>Every type of config.json's contract, from the config down.</summary>
    private static List<JsonTypeInfo> Contract()
    {
        var seen = new HashSet<Type>();
        var found = new List<JsonTypeInfo>();
        void Walk(Type type)
        {
            if (!seen.Add(type) || ConfigJson.Default.GetTypeInfo(type) is not { } info)
            {
                return;
            }
            found.Add(info);
            if (info.Kind == JsonTypeInfoKind.Object)
            {
                foreach (var property in info.Properties)
                {
                    Walk(property.PropertyType);
                }
            }
            if (info.ElementType is { } element)
            {
                Walk(element);
            }
            if (info.KeyType is { } key)
            {
                Walk(key);
            }
            if (Nullable.GetUnderlyingType(type) is { } underlying)
            {
                Walk(underlying);
            }
        }
        Walk(typeof(Config));
        return found;
    }

    /// <summary>
    /// The properties and words, one a line, sorted: "property Config.layouts", "enum Edge: \"top\"",
    /// "line kind \"clock\"" and so on. (The holidays' block writes itself, HolidayOptionsConverter: its
    /// own words are not seen here.)
    /// </summary>
    private static List<string> Words()
    {
        var words = new SortedSet<string>(StringComparer.Ordinal);
        var contract = Contract();
        foreach (var info in contract.Where(info => info.Kind == JsonTypeInfoKind.Object))
        {
            var type = Nullable.GetUnderlyingType(info.Type) ?? info.Type;
            foreach (var property in info.Properties)
            {
                // A property marked [JsonIgnore] for good (a value worked out from others) is never in the file.
                if (property.AttributeProvider?.GetCustomAttributes(typeof(System.Text.Json.Serialization.JsonIgnoreAttribute), true)
                    .OfType<System.Text.Json.Serialization.JsonIgnoreAttribute>().Any(ignore => ignore.Condition == System.Text.Json.Serialization.JsonIgnoreCondition.Always) == true)
                {
                    continue;
                }
                words.Add($"property {type.Name}.{property.Name}");
            }
        }
        foreach (var info in contract.Where(info => info.Type.IsEnum))
        {
            foreach (var value in Enum.GetValues(info.Type))
            {
                words.Add($"enum {info.Type.Name}: {JsonSerializer.Serialize(value, info)}");
            }
        }
        foreach (var line in ListWidgets.All)
        {
            var cell = new CellItem { Kind = line.Info.Id };
            using var written = JsonDocument.Parse(JsonSerializer.Serialize(cell, ConfigJson.Default.CellItem));
            foreach (var key in new[] { "kind", WidgetStyles.StyleKey, WidgetStyles.ReadingKey })
            {
                if (written.RootElement.TryGetProperty(key, out var word) && word.ValueKind == JsonValueKind.String)
                {
                    words.Add($"line {key} \"{word.GetString()}\"");
                }
            }
        }
        foreach (var face in FolderFaces.All)
        {
            words.Add($"face kind \"{face.Id}\"");
        }
        return [.. words];
    }
}
