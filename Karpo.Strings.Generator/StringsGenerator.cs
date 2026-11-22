// The generator: Karpo.Core\Strings\*.json in, Karpo.S and the language tables (Karpo.Core.Strings) out.
//
// en.json is the list of keys. Each other file is a language named by its file name (de.json,
// zh-Hans.json) and has to have exactly those keys, with the same places in each text. Out come:
//
//   - S, one member per key: a property for a text with no places (S.SettingsLanguageHeader), a
//     method with one typed parameter per place for a text with them (S.DockVolumeLevel(int level)),
//     and for a pair of keys "x.one" and "x.other" a method that picks between them by its count;
//   - one table per language, in nested classes, so a language nobody uses is never loaded;
//   - qps-ploc, the pseudo-locale, made from English here (Pseudo.cs).
//
// What is wrong with a file is an error in the build, at the line it is on: Karpo never runs with a
// key missing from a language, a translation that lost a place, or a brace left open.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Karpo.Strings.Generator;

[Generator(LanguageNames.CSharp)]
public sealed class StringsGenerator : IIncrementalGenerator
{
    /// <summary>The language every other one is checked against and falls back to.</summary>
    private const string English = "en";

    /// <summary>The names S has of its own, which no key may take.</summary>
    private static readonly HashSet<string> Reserved = ["Lookup", "Keys", "Equals", "GetHashCode", "ToString", "GetType", "ReferenceEquals"];

    private static readonly DiagnosticDescriptor Unreadable = Error("KS001", "A strings file doesn't read", "{0} doesn't read: {1}");
    private static readonly DiagnosticDescriptor Twice = Error("KS002", "A key is written twice", "\"{0}\" is in {1} twice");
    private static readonly DiagnosticDescriptor BadKey = Error("KS003", "A key is badly named", "\"{0}\" is no key: a key is words of letters and digits joined by dots, starting with a small letter, as \"settings.language.header\"");
    private static readonly DiagnosticDescriptor SameName = Error("KS004", "Two keys make the same name", "\"{0}\" and \"{1}\" would both be S.{2}");
    private static readonly DiagnosticDescriptor Missing = Error("KS005", "A language lacks a key", "{0} has no \"{1}\" (en.json has it: \"{2}\")");
    private static readonly DiagnosticDescriptor Extra = Error("KS006", "A language has a key English lacks", "\"{0}\" is in {1} but not in en.json");
    private static readonly DiagnosticDescriptor Places = Error("KS007", "A translation's places differ from English", "\"{0}\" in {1} has the places {2}; en.json has {3}");
    private static readonly DiagnosticDescriptor BadText = Error("KS008", "A text's braces don't make sense", "\"{0}\" in {1}: {2}");
    private static readonly DiagnosticDescriptor NoEnglish = Error("KS009", "There is no en.json", "Karpo.Core\\Strings has no en.json, the list of keys every language follows");
    private static readonly DiagnosticDescriptor BadPair = Error("KS010", "A .one/.other pair doesn't match", "\"{0}\": {1}");

    private static DiagnosticDescriptor Error(string id, string title, string message) =>
        new(id, title, message, "Karpo.Strings", DiagnosticSeverity.Error, isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider
            .Where(static file => IsStringsFile(file.Path))
            .Select(static (file, token) => new LanguageFile(file.Path, file.GetText(token)?.ToString() ?? ""))
            .Collect();
        context.RegisterSourceOutput(files, static (output, all) => Generate(output, all.ToList()));
    }

    /// <summary>A .json file in a folder called Strings.</summary>
    private static bool IsStringsFile(string path) =>
        string.Equals(Path.GetExtension(path), ".json", System.StringComparison.OrdinalIgnoreCase)
        && string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "Strings", System.StringComparison.OrdinalIgnoreCase);

    /// <summary>A file as the compiler hands it over; equal when its path and text are, so an unchanged file generates nothing again.</summary>
    private sealed class LanguageFile : System.IEquatable<LanguageFile>
    {
        public LanguageFile(string path, string text)
        {
            Path = path;
            Text = text;
        }

        public string Path { get; }

        public string Text { get; }

        public string Code => System.IO.Path.GetFileNameWithoutExtension(Path);

        public bool Equals(LanguageFile? other) => other is not null && other.Path == Path && other.Text == Text;

        public override bool Equals(object? obj) => Equals(obj as LanguageFile);

        public override int GetHashCode() => Path.GetHashCode() ^ Text.GetHashCode();
    }

    /// <summary>A key of en.json as it is generated.</summary>
    private sealed class Key
    {
        public Key(int index, Entry entry, List<Piece> pieces)
        {
            Index = index;
            Entry = entry;
            Pieces = pieces;
            Places = Template.Places(pieces);
            Name = MemberName(entry.Key);
        }

        public int Index { get; }

        public Entry Entry { get; }

        public List<Piece> Pieces { get; }

        public List<Piece> Places { get; }

        public string Name { get; }
    }

    private static void Generate(SourceProductionContext output, List<LanguageFile> files)
    {
        var read = new Dictionary<string, (LanguageFile File, Dictionary<string, Entry> Entries, List<Entry> Order)>(System.StringComparer.Ordinal);
        foreach (var file in files.OrderBy(file => file.Code, System.StringComparer.Ordinal))
        {
            var entries = FlatJson.Read(file.Text, out var error);
            if (entries is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(Unreadable, At(file.Path, error!.Start, 1, error.Line, error.Column), Path.GetFileName(file.Path), error.Message));
                continue;
            }
            var byKey = new Dictionary<string, Entry>(System.StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (byKey.ContainsKey(entry.Key))
                {
                    output.ReportDiagnostic(Diagnostic.Create(Twice, At(file.Path, entry), entry.Key, Path.GetFileName(file.Path)));
                    continue;
                }
                byKey[entry.Key] = entry;
            }
            read[file.Code] = (file, byKey, entries);
        }
        if (!read.TryGetValue(English, out var english))
        {
            if (files.Count > 0)
            {
                output.ReportDiagnostic(Diagnostic.Create(NoEnglish, Location.None));
            }
            return;
        }

        // English: the keys, their names and their places.
        var keys = new List<Key>();
        var names = new Dictionary<string, string>(System.StringComparer.Ordinal);
        foreach (var entry in english.Order.Where(entry => english.Entries[entry.Key] == entry))
        {
            if (!IsKey(entry.Key))
            {
                output.ReportDiagnostic(Diagnostic.Create(BadKey, At(english.File.Path, entry), entry.Key));
                continue;
            }
            var pieces = Template.Parse(entry.Text, out var problem);
            if (pieces is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(BadText, At(english.File.Path, entry), entry.Key, "en.json", problem));
                pieces = [Piece.Of(entry.Text)];
            }
            var key = new Key(keys.Count, entry, pieces);
            if (Reserved.Contains(key.Name) || names.ContainsKey(key.Name))
            {
                output.ReportDiagnostic(Diagnostic.Create(SameName, At(english.File.Path, entry), names.TryGetValue(key.Name, out var other) ? other : key.Name, entry.Key, key.Name));
                continue;
            }
            names[key.Name] = entry.Key;
            keys.Add(key);
        }
        var pairs = Pairs(output, english.File.Path, keys, names);

        // Every other language: the same keys, the same places. What is wrong still generates, from
        // English, so one mistake is one error rather than a page of them where S is used.
        var codes = new List<string> { English };
        codes.AddRange(read.Keys.Where(code => code != English && code != Pseudo.Code).OrderBy(code => code, System.StringComparer.Ordinal));
        var tables = new List<string[]>();
        foreach (var code in codes)
        {
            var (file, entries, order) = read[code];
            var name = Path.GetFileName(file.Path);
            var table = new string[keys.Count];
            foreach (var key in keys)
            {
                if (!entries.TryGetValue(key.Entry.Key, out var entry))
                {
                    output.ReportDiagnostic(Diagnostic.Create(Missing, At(file.Path, 0, 1, 0, 0), name, key.Entry.Key, key.Entry.Text));
                    table[key.Index] = Stored(key, key.Pieces);
                    continue;
                }
                var pieces = code == English ? key.Pieces : Template.Parse(entry.Text, out var problem) is { } parsed ? parsed : Fail(problem!);
                List<Piece> Fail(string problem)
                {
                    output.ReportDiagnostic(Diagnostic.Create(BadText, At(file.Path, entry), key.Entry.Key, name, problem));
                    return key.Pieces;
                }
                if (code != English && !SamePlaces(key.Places, Template.Places(pieces)))
                {
                    output.ReportDiagnostic(Diagnostic.Create(Places, At(file.Path, entry), key.Entry.Key, name, Describe(Template.Places(pieces)), Describe(key.Places)));
                    pieces = key.Pieces;
                }
                table[key.Index] = Stored(key, pieces);
            }
            foreach (var entry in order)
            {
                if (!english.Entries.ContainsKey(entry.Key))
                {
                    output.ReportDiagnostic(Diagnostic.Create(Extra, At(file.Path, entry), entry.Key, name));
                }
            }
            tables.Add(table);
        }
        codes.Add(Pseudo.Code);
        tables.Add(keys.Select(key => Stored(key, Template.Parse(Pseudo.Of(key.Pieces), out _)!)).ToArray());

        var (s, strings) = Emit(keys, pairs, codes, tables);
        output.AddSource("S.g.cs", SourceText.From(s, Encoding.UTF8));
        output.AddSource("Strings.g.cs", SourceText.From(strings, Encoding.UTF8));
    }

    /// <summary>What a language's table holds for a key: the words for a text with no places, the template for one with them.</summary>
    private static string Stored(Key key, List<Piece> pieces) =>
        key.Places.Count == 0 ? Template.Words(pieces) : Rewrite(pieces);

    /// <summary>A template written back from its pieces, braces doubled, each place with the type English gives it.</summary>
    private static string Rewrite(List<Piece> pieces)
    {
        var text = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (piece.IsPlace)
            {
                text.Append('{').Append(piece.Name).Append('}');
            }
            else
            {
                text.Append(piece.Words!.Replace("{", "{{").Replace("}", "}}"));
            }
        }
        return text.ToString();
    }

    private static bool SamePlaces(List<Piece> english, List<Piece> translated)
    {
        if (english.Count != translated.Count)
        {
            return false;
        }
        foreach (var place in translated)
        {
            var match = english.FirstOrDefault(other => other.Name == place.Name);
            if (match is null || (place.Type is not null && place.Type != (match.Type ?? "string")))
            {
                return false;
            }
        }
        return true;
    }

    private static string Describe(List<Piece> places) =>
        places.Count == 0 ? "none" : string.Join(", ", places.Select(place => "{" + place.Name + "}"));

    /// <summary>The "x.one" / "x.other" pairs: the stem's name and its two keys.</summary>
    private static List<(string Name, Key One, Key Other)> Pairs(SourceProductionContext output, string path, List<Key> keys, Dictionary<string, string> names)
    {
        var pairs = new List<(string, Key, Key)>();
        var byKey = keys.ToDictionary(key => key.Entry.Key);
        foreach (var one in keys.Where(key => key.Entry.Key.EndsWith(".one", System.StringComparison.Ordinal)))
        {
            var stem = one.Entry.Key.Substring(0, one.Entry.Key.Length - ".one".Length);
            if (!byKey.TryGetValue(stem + ".other", out var other))
            {
                output.ReportDiagnostic(Diagnostic.Create(BadPair, At(path, one.Entry), one.Entry.Key, $"has no \"{stem}.other\" beside it"));
                continue;
            }
            var count = other.Places.FirstOrDefault(place => place.Name == "count");
            if (count is null || (count.Type is not "int" and not "long"))
            {
                output.ReportDiagnostic(Diagnostic.Create(BadPair, At(path, other.Entry), other.Entry.Key, "a pair is picked by its count, so \"other\" needs {count:int} or {count:long}"));
                continue;
            }
            if (one.Places.Any(place => other.Places.All(theirs => theirs.Name != place.Name)))
            {
                output.ReportDiagnostic(Diagnostic.Create(BadPair, At(path, one.Entry), one.Entry.Key, $"a place here is not in \"{stem}.other\""));
                continue;
            }
            var name = MemberName(stem);
            if (Reserved.Contains(name) || names.ContainsKey(name))
            {
                output.ReportDiagnostic(Diagnostic.Create(SameName, At(path, one.Entry), stem, names.TryGetValue(name, out var taken) ? taken : stem, name));
                continue;
            }
            names[name] = stem;
            pairs.Add((name, one, other));
        }
        return pairs;
    }

    // --- The code ------------------------------------------------------------------------------

    private static (string S, string Strings) Emit(List<Key> keys, List<(string Name, Key One, Key Other)> pairs, List<string> codes, List<string[]> tables)
    {
        // S in the namespace Karpo, where it is found before TerraFX's S (Strings\S.cs); the tables in Karpo.Core.
        var code = Header("Karpo");
        code.AppendLine("public static partial class S");
        code.AppendLine("{");
        foreach (var key in keys)
        {
            Summary(code, key.Entry.Text, key.Entry.Key);
            if (key.Places.Count == 0)
            {
                code.Append("    public static string ").Append(key.Name).Append(" => global::Karpo.Core.Strings.Current[").Append(key.Index).AppendLine("];");
                continue;
            }
            code.Append("    public static string ").Append(key.Name).Append('(').Append(Parameters(key.Places)).AppendLine(") => global::Karpo.Core.Strings.Index switch");
            code.AppendLine("    {");
            for (var language = 1; language < codes.Count; language++)
            {
                code.Append("        ").Append(language).Append(" => ").Append(Interpolated(Template.Parse(tables[language][key.Index], out _)!, key.Places)).AppendLine(",");
            }
            code.Append("        _ => ").Append(Interpolated(key.Pieces, key.Places)).AppendLine(",");
            code.AppendLine("    };");
        }
        foreach (var (name, one, other) in pairs)
        {
            Summary(code, other.Entry.Text, other.Entry.Key.Substring(0, other.Entry.Key.Length - ".other".Length) + ".one/.other");
            var arguments = string.Join(", ", other.Places.Select(place => "@" + place.Name));
            var oneArguments = string.Join(", ", one.Places.Select(place => "@" + place.Name));
            code.Append("    public static string ").Append(name).Append('(').Append(Parameters(other.Places)).Append(") => global::Karpo.Core.Strings.IsOne(@count) ? ")
                .Append(one.Name).Append(one.Places.Count == 0 ? "" : "(" + oneArguments + ")")
                .Append(" : ").Append(other.Name).Append('(').Append(arguments).AppendLine(");");
        }
        code.AppendLine("}");
        var s = code.ToString();

        code = Header("Karpo.Core");
        code.AppendLine("public static partial class Strings");
        code.AppendLine("{");
        code.AppendLine("    private static partial string[] Codes() => Data.Codes;");
        code.AppendLine();
        code.AppendLine("    private static partial string[] KeyList() => Data.Keys;");
        code.AppendLine();
        code.AppendLine("    private static partial string[] NameList() => Data.Names;");
        code.AppendLine();
        code.AppendLine("    private static partial string[] Table(int language) => language switch");
        code.AppendLine("    {");
        for (var language = 1; language < codes.Count; language++)
        {
            code.Append("        ").Append(language).Append(" => T").Append(language).AppendLine(".Texts,");
        }
        code.AppendLine("        _ => T0.Texts,");
        code.AppendLine("    };");
        code.AppendLine();
        code.AppendLine("    private static class Data");
        code.AppendLine("    {");
        code.Append("        internal static readonly string[] Codes = [").Append(string.Join(", ", codes.Select(Literal))).AppendLine("];");
        code.AppendLine();
        code.AppendLine("        internal static readonly string[] Keys =");
        code.AppendLine("        [");
        foreach (var key in keys)
        {
            code.Append("            ").Append(Literal(key.Entry.Key)).AppendLine(",");
        }
        code.AppendLine("        ];");
        code.AppendLine();
        code.AppendLine("        internal static readonly string[] Names =");
        code.AppendLine("        [");
        foreach (var key in keys)
        {
            code.Append("            ").Append(Literal(key.Name)).AppendLine(",");
        }
        code.AppendLine("        ];");
        code.AppendLine("    }");
        for (var language = 0; language < codes.Count; language++)
        {
            code.AppendLine();
            code.Append("    // ").AppendLine(codes[language]);
            code.Append("    private static class T").AppendLine(language.ToString());
            code.AppendLine("    {");
            code.AppendLine("        internal static readonly string[] Texts =");
            code.AppendLine("        [");
            foreach (var text in tables[language])
            {
                code.Append("            ").Append(Literal(text)).AppendLine(",");
            }
            code.AppendLine("        ];");
            code.AppendLine("    }");
        }
        code.AppendLine("}");
        return (s, code.ToString());
    }

    private static StringBuilder Header(string space)
    {
        var code = new StringBuilder();
        code.AppendLine("// <auto-generated/>");
        code.AppendLine("// Karpo.Strings.Generator, from Karpo.Core\\Strings\\*.json. Change the JSON, not this.");
        code.AppendLine("#nullable enable");
        code.AppendLine();
        code.Append("namespace ").Append(space).AppendLine(";");
        code.AppendLine();
        return code;
    }

    private static void Summary(StringBuilder code, string english, string key)
    {
        code.AppendLine();
        code.Append("    /// <summary>").Append(Xml(english)).Append(" <c>").Append(Xml(key)).AppendLine("</c></summary>");
    }

    private static string Parameters(List<Piece> places) =>
        string.Join(", ", places.Select(place => (place.Type ?? "string") + " @" + place.Name));

    /// <summary>
    /// A C# interpolated string of <paramref name="pieces"/>, its key's English places <paramref name="places"/>. §638: a count (a
    /// whole number named "count", what a plural pair is picked by: always how many of something) written as the region writes
    /// a whole number (RegionFormats.Count: "1,234 events", "1.234 Termine"; the dock's .NET culture is the invariant one).
    /// </summary>
    private static string Interpolated(List<Piece> pieces, List<Piece> places)
    {
        var counts = places.Exists(place => place.Name == "count" && place.Type is "int" or "long");
        var text = new StringBuilder("$\"");
        foreach (var piece in pieces)
        {
            if (piece.IsPlace && piece.Name == "count" && counts)
            {
                // In parentheses: an interpolation's colon would start a format.
                text.Append("{(global::Karpo.Core.RegionFormats.Count(@count))}");
            }
            else if (piece.IsPlace)
            {
                text.Append("{@").Append(piece.Name).Append('}');
            }
            else
            {
                text.Append(Escape(piece.Words!).Replace("{", "{{").Replace("}", "}}"));
            }
        }
        return text.Append('"').ToString();
    }

    private static string Literal(string text) => "\"" + Escape(text) + "\"";

    private static string Escape(string text)
    {
        var escaped = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            switch (c)
            {
                case '\\': escaped.Append("\\\\"); break;
                case '"': escaped.Append("\\\""); break;
                case '\n': escaped.Append("\\n"); break;
                case '\r': escaped.Append("\\r"); break;
                case '\t': escaped.Append("\\t"); break;
                default:
                    if (c < ' ' || c is (char)0x2028 or (char)0x2029 or (char)0x85)
                    {
                        escaped.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        escaped.Append(c);
                    }
                    break;
            }
        }
        return escaped.ToString();
    }

    private static string Xml(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", " ").Replace("\n", " ");

    // --- Keys and names --------------------------------------------------------------------------

    /// <summary>"settings.language.header": words of letters and digits joined by dots, the first starting with a small letter.</summary>
    private static bool IsKey(string key)
    {
        if (key.Length == 0 || key[0] < 'a' || key[0] > 'z')
        {
            return false;
        }
        var words = key.Split('.');
        return words.All(word => word.Length > 0 && word.All(c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')));
    }

    /// <summary>"settings.language.header" is SettingsLanguageHeader; "dock.shortDate" is DockShortDate.</summary>
    private static string MemberName(string key)
    {
        var name = new StringBuilder(key.Length);
        foreach (var word in key.Split('.'))
        {
            if (word.Length == 0)
            {
                continue;
            }
            name.Append(char.ToUpperInvariant(word[0])).Append(word, 1, word.Length - 1);
        }
        return name.ToString();
    }

    private static Location At(string path, Entry entry) => At(path, entry.Start, entry.Length, entry.Line, entry.Column);

    private static Location At(string path, int start, int length, int line, int column) =>
        Location.Create(
            path,
            new TextSpan(start, length),
            new LinePositionSpan(new LinePosition(line, column), new LinePosition(line, column + length)));
}
