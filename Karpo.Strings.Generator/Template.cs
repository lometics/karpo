// A text with places for values in it: "Volume: {level:int}%".
//
// A place is a name in braces, with its type after a colon where it is not a string. The type is
// written in English (en.json) and may be left out of a translation, which takes the English one;
// a translation that writes a different one is an error. A brace that is meant as a brace is
// written twice, "{{" and "}}", as in .NET's own format strings.

using System.Collections.Generic;
using System.Text;

namespace Karpo.Strings.Generator;

/// <summary>A piece of a template: words as they are, or a place for a value.</summary>
internal sealed class Piece
{
    private Piece(string? words, string? name, string? type)
    {
        Words = words;
        Name = name;
        Type = type;
    }

    /// <summary>The words, braces already single; null for a place.</summary>
    public string? Words { get; }

    /// <summary>The place's name; null for words.</summary>
    public string? Name { get; }

    /// <summary>The type written after the colon; null where none was written.</summary>
    public string? Type { get; }

    public bool IsPlace => Name is not null;

    public static Piece Of(string words) => new(words, null, null);

    public static Piece Place(string name, string? type) => new(null, name, type);
}

internal static class Template
{
    /// <summary>The types a place may have. A number that needs a format of its own is formatted by the caller and passed as a string.</summary>
    public static readonly string[] Types = ["string", "int", "long", "double"];

    /// <summary>The pieces of <paramref name="text"/>; null and <paramref name="error"/> for a text whose braces don't make sense.</summary>
    public static List<Piece>? Parse(string text, out string? error)
    {
        var pieces = new List<Piece>();
        var words = new StringBuilder();
        error = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    words.Append('}');
                    i++;
                    continue;
                }
                error = $"a '}}' with no '{{' before it, at {i + 1} (a brace meant as a brace is written \"}}}}\")";
                return null;
            }
            if (c != '{')
            {
                words.Append(c);
                continue;
            }
            if (i + 1 < text.Length && text[i + 1] == '{')
            {
                words.Append('{');
                i++;
                continue;
            }
            var end = text.IndexOf('}', i + 1);
            if (end < 0)
            {
                error = $"a '{{' that is never closed, at {i + 1} (a brace meant as a brace is written \"{{{{\")";
                return null;
            }
            var inside = text.Substring(i + 1, end - i - 1);
            var colon = inside.IndexOf(':');
            var name = colon < 0 ? inside : inside.Substring(0, colon);
            var type = colon < 0 ? null : inside.Substring(colon + 1);
            if (!IsName(name))
            {
                error = $"\"{{{inside}}}\" is no place: a place is a name of letters and digits, as {{count}} or {{count:int}}";
                return null;
            }
            if (type is not null && System.Array.IndexOf(Types, type) < 0)
            {
                error = $"\"{{{inside}}}\" has a type Karpo doesn't take; the types are {string.Join(", ", Types)}";
                return null;
            }
            if (words.Length > 0)
            {
                pieces.Add(Piece.Of(words.ToString()));
                words.Clear();
            }
            pieces.Add(Piece.Place(name, type));
            i = end;
        }
        if (words.Length > 0)
        {
            pieces.Add(Piece.Of(words.ToString()));
        }
        return pieces;
    }

    /// <summary>The places of <paramref name="pieces"/>, each once, in the order they first appear.</summary>
    public static List<Piece> Places(List<Piece> pieces)
    {
        var seen = new HashSet<string>();
        var places = new List<Piece>();
        foreach (var piece in pieces)
        {
            if (piece.IsPlace && seen.Add(piece.Name!))
            {
                places.Add(piece);
            }
        }
        return places;
    }

    /// <summary>The words alone, with every brace single: what a text without places shows.</summary>
    public static string Words(List<Piece> pieces)
    {
        var text = new StringBuilder();
        foreach (var piece in pieces)
        {
            text.Append(piece.Words);
        }
        return text.ToString();
    }

    private static bool IsName(string name)
    {
        if (name.Length == 0 || !(char.IsLetter(name[0]) || name[0] == '_'))
        {
            return false;
        }
        foreach (var c in name)
        {
            if (!(char.IsLetterOrDigit(c) || c == '_'))
            {
                return false;
            }
        }
        return true;
    }
}
