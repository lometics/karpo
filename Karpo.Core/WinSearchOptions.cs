// §642, §652: a Windows search line's switches, as config.json keeps them on the line ("winSearch"): one per source
// (WinSearchSourceInfos), "Search in" on its page — `"sources": { "web": true, "mail": false }`. A source at its default
// (every one on but the web's row: nothing typed leaves the PC unless the person asks for it) is written as none, and a
// word no source has is dropped; so a line left as it came writes nothing at all.
//
// §655: what it shows before anything is typed (WinSearchHome): the sections shown, in their order —
// `"home": ["apps", "files", "searches"]` — and how many each shows where that is not its own default — `"homeMost": { "files": 10 }`.
// The sections and the order a home has by default (WinSearchHomeInfos.Defaults) are written as none, as is a count at its
// section's default; a word no section has is dropped, a count held to its section's range.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>§652: a Windows search line's switches (see the top of the file).</summary>
public sealed class WinSearchOptions
{
    /// <summary>Each source switched away from its default, by its id: on or off; null for none.</summary>
    [JsonPropertyName("sources"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, bool>? Sources { get; set; }

    /// <summary>§655: the home's sections shown, in their order, by their ids; null for the default ones in theirs.</summary>
    [JsonPropertyName("home"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Home { get; set; }

    /// <summary>§655: how many each home section shows where that is not its default, by its id; null for none.</summary>
    [JsonPropertyName("homeMost"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int>? HomeMost { get; set; }

    /// <summary>Whether nothing differs from a new line's: no record of it is written.</summary>
    [JsonIgnore]
    public bool Empty => Sources is null && Home is null && HomeMost is null;

    /// <summary>Whether source <paramref name="id"/> is asked.</summary>
    public bool On(string id) => WinSearchSourceInfos.On(this, id);

    /// <summary>Source <paramref name="id"/> switched <paramref name="on"/>: written where that is not its default, else left out.</summary>
    public void Set(string id, bool on)
    {
        (Sources ??= new(StringComparer.Ordinal))[id] = on;
        Normalize();
    }

    /// <summary>§655: the home shows <paramref name="sections"/>, in their order: written where that is not its default, else left out.</summary>
    public void SetHome(IEnumerable<string> sections)
    {
        Home = [.. sections];
        Normalize();
    }

    /// <summary>§655: home section <paramref name="id"/> shows <paramref name="count"/>: written where that is not its default, else left out.</summary>
    public void SetHomeMost(string id, int count)
    {
        (HomeMost ??= new(StringComparer.Ordinal))[id] = count;
        Normalize();
    }

    /// <summary>
    /// Each switch at its default written as none, a word no source has dropped; §655 the home's sections their known ones
    /// once each, none where they are the default ones in their order, a count held to its section's range and none at its
    /// default; none left, no record of them.
    /// </summary>
    public void Normalize()
    {
        if (Sources is { } said)
        {
            Dictionary<string, bool> kept = new(StringComparer.Ordinal);
            foreach (var (id, on) in said)
            {
                if (WinSearchSourceInfos.Of(id) is { } info && info.On != on)
                {
                    kept[info.Id] = on;
                }
            }
            Sources = kept.Count > 0 ? kept : null;
        }
        if (Home is { } home)
        {
            List<string> known = [.. home.Where(id => WinSearchHomeInfos.Of(id) is not null).Distinct(StringComparer.Ordinal)];
            Home = known.SequenceEqual(WinSearchHomeInfos.Defaults, StringComparer.Ordinal) ? null : known;
        }
        if (HomeMost is { } most)
        {
            Dictionary<string, int> kept = new(StringComparer.Ordinal);
            foreach (var (id, count) in most)
            {
                if (WinSearchHomeInfos.Of(id) is { } info && Math.Clamp(count, WinSearchHomeInfos.Fewest, info.Most) is var held && held != info.Shown)
                {
                    kept[info.Id] = held;
                }
            }
            HomeMost = kept.Count > 0 ? kept : null;
        }
    }
}
