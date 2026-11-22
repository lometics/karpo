// Tests that switch Karpo's language.
//
// Every test reads Karpo in English (Isolation.cs pins it), and the language is one value for the
// whole process (Strings.Use), so a test that switches it would change the words under every test
// running beside it. Those tests carry [Collection(InLanguage.Collection)], which runs them one at
// a time and after the rest, and take the language with InLanguage.Use, which puts English back.

namespace Karpo.Tests.Localization;

[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class InLanguage
{
    public const string Collection = "language";

    /// <summary>Karpo speaks <paramref name="language"/> until the result is disposed; then English again.</summary>
    public static IDisposable Use(string language)
    {
        Strings.Use(language);
        return new English();
    }

    private sealed class English : IDisposable
    {
        public void Dispose() => Strings.Use("en");
    }
}

/// <summary>The language files as they are in the repository, read as plain JSON.</summary>
internal static class StringFiles
{
    /// <summary>
    /// Every language there is a file for, English first, the rest in Settings' order
    /// (Languages.All): a translation is tested as soon as its file is there.
    /// </summary>
    public static readonly string[] Codes =
    [
        .. Directory.GetFiles(Folder, "*.json")
            .Select(file => Path.GetFileNameWithoutExtension(file)!)
            .OrderBy(code => Languages.All.Select(known => known.Code).ToList().IndexOf(code) is var at and >= 0 ? at : int.MaxValue)
            .ThenBy(code => code, StringComparer.Ordinal),
    ];

    /// <summary>Every language but English.</summary>
    public static IEnumerable<string> Translations => Codes.Skip(1);

    public static string Repository
    {
        get
        {
            for (var up = new DirectoryInfo(AppContext.BaseDirectory); up is not null; up = up.Parent)
            {
                if (File.Exists(Path.Combine(up.FullName, "Karpo.slnx")))
                {
                    return up.FullName;
                }
            }
            throw new InvalidOperationException("The repository was not found above the tests.");
        }
    }

    public static string Folder => Path.Combine(Repository, "Karpo.Core", "Strings");

    /// <summary>The key/text pairs of <paramref name="language"/>'s file, in its order. A key written twice fails here.</summary>
    public static List<KeyValuePair<string, string>> Read(string language)
    {
        var text = File.ReadAllText(Path.Combine(Folder, language + ".json"));
        using var document = System.Text.Json.JsonDocument.Parse(text);
        var pairs = new List<KeyValuePair<string, string>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.True(seen.Add(property.Name), $"{language}.json has \"{property.Name}\" twice");
            Assert.Equal(System.Text.Json.JsonValueKind.String, property.Value.ValueKind);
            pairs.Add(new(property.Name, property.Value.GetString()!));
        }
        return pairs;
    }

    public static Dictionary<string, string> Map(string language) => Read(language).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    /// <summary>The places of a text by name ("count" for "{count:int} items"), each once; a doubled brace is a brace, not a place.</summary>
    public static SortedSet<string> Places(string text)
    {
        var places = new SortedSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '{' && i + 1 < text.Length && text[i + 1] == '{')
            {
                i++;
                continue;
            }
            if (text[i] != '{')
            {
                continue;
            }
            var end = text.IndexOf('}', i);
            if (end < 0)
            {
                break;
            }
            var inside = text[(i + 1)..end];
            places.Add(inside.Split(':')[0]);
            i = end;
        }
        return places;
    }

    /// <summary>Whether every brace is a doubled brace or opens a place that closes before the next one opens.</summary>
    public static bool Balanced(string text)
    {
        var open = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (!open && (c is '{' or '}') && i + 1 < text.Length && text[i + 1] == c)
            {
                i++;
                continue;
            }
            if (c == '{')
            {
                if (open)
                {
                    return false;
                }
                open = true;
            }
            else if (c == '}')
            {
                if (!open)
                {
                    return false;
                }
                open = false;
            }
        }
        return !open;
    }

    /// <summary>A text as the tables hold it: places without their types, doubled braces single where there is no place.</summary>
    public static string Stored(string text)
    {
        var withoutTypes = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!\{)\{([A-Za-z_][A-Za-z0-9_]*):[a-z]+\}", "{$1}");
        return Places(text).Count == 0 ? withoutTypes.Replace("{{", "{").Replace("}}", "}") : withoutTypes;
    }
}
