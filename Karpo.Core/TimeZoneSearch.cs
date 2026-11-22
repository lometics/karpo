// Finding a Windows time zone by what a person types: a city from its display
// name ("tokyo"), part of its id ("pacific"), or its offset ("+9", "utc-5:30").
//
// In the core: the settings app's zone box and the zone field of a clock's menu on the bar
// (docs\widgets.md §219) find alike.

using System.Globalization;
using System.Text;

namespace Karpo.Core;

/// <summary>A zone as Windows lists it: its id ("Tokyo Standard Time") and display name ("(UTC+09:00) Osaka, Sapporo, Tokyo").</summary>
public sealed record ZoneEntry(string Id, string DisplayName, string StandardName, TimeSpan Offset);

public static class TimeZoneSearch
{
    private static IReadOnlyList<ZoneEntry>? windows;

    /// <summary>Every zone Windows knows, in its own order (by offset, then name), read the first time it is asked for.</summary>
    public static IReadOnlyList<ZoneEntry> Windows => windows ??= [.. TimeZoneInfo.GetSystemTimeZones()
        .Select(zone => new ZoneEntry(zone.Id, zone.DisplayName, zone.StandardName, zone.BaseUtcOffset))];

    /// <summary>
    /// The zones every word of <paramref name="query"/> is found in, in Windows' own
    /// order (by offset, then name), at most <paramref name="limit"/>. A word matches
    /// the display name, the id or the standard name, ignoring case and accents; a
    /// word like "+9", "-5:30" or "utc+1" matches the zone's offset instead. Zones
    /// with a place that starts with a word come first. An empty query finds nothing.
    /// </summary>
    public static IReadOnlyList<ZoneEntry> Find(IEnumerable<ZoneEntry> zones, string query, int limit = 20)
    {
        var words = query.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).Select(Fold).ToArray();
        if (words.Length == 0)
        {
            return [];
        }
        var found = new List<(ZoneEntry Zone, bool Starts, int Order)>();
        var order = 0;
        foreach (var zone in zones)
        {
            order++;
            var text = Fold($"{zone.DisplayName} {zone.Id} {zone.StandardName}");
            var places = Places(zone.DisplayName).Select(Fold).ToArray();
            var matches = true;
            var starts = false;
            foreach (var word in words)
            {
                if (Offset(word) is { } offset)
                {
                    matches &= zone.Offset == offset;
                    continue;
                }
                if (!text.Contains(word, StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
                starts |= places.Any(place => place.StartsWith(word, StringComparison.Ordinal));
            }
            if (matches)
            {
                found.Add((zone, starts, order));
            }
        }
        return found
            .OrderByDescending(match => match.Starts)
            .ThenBy(match => match.Order)
            .Take(limit)
            .Select(match => match.Zone)
            .ToList();
    }

    /// <summary>The places of a display name: "(UTC+09:00) Osaka, Sapporo, Tokyo" → Osaka, Sapporo, Tokyo.</summary>
    public static IEnumerable<string> Places(string displayName)
    {
        var close = displayName.IndexOf(')');
        var rest = close >= 0 ? displayName[(close + 1)..] : displayName;
        return rest.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>"+9", "-05:30", "utc+1", "gmt-3" as an offset; null for any other word.</summary>
    public static TimeSpan? Offset(string word)
    {
        var text = word.Trim().ToLowerInvariant();
        foreach (var prefix in (string[])["utc", "gmt"])
        {
            if (text.StartsWith(prefix, StringComparison.Ordinal))
            {
                text = text[prefix.Length..];
            }
        }
        if (text.Length < 2 || text[0] is not ('+' or '-'))
        {
            return null;
        }
        var sign = text[0] == '-' ? -1 : 1;
        var parts = text[1..].Split(':');
        if (parts.Length > 2
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || hours > 14)
        {
            return null;
        }
        var minutes = 0;
        if (parts.Length == 2 && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out minutes) || minutes >= 60))
        {
            return null;
        }
        return sign * new TimeSpan(hours, minutes, 0);
    }

    // Latin letters with accents and the letter they are written on, for where Unicode
    // decomposition isn't there to do it (a process with invariant globalization).
    static readonly Dictionary<char, char> Unaccented = new (string Letters, char Plain)[]
    {
        ("àáâãäåāăą", 'a'), ("çćĉċč", 'c'), ("ďđ", 'd'), ("èéêëēĕėęě", 'e'), ("ĝğġģ", 'g'), ("ĥħ", 'h'),
        ("ìíîïĩīĭįı", 'i'), ("ĵ", 'j'), ("ķ", 'k'), ("ĺļľŀł", 'l'), ("ñńņň", 'n'), ("òóôõöøōŏő", 'o'),
        ("ŕŗř", 'r'), ("śŝşš", 's'), ("ţťŧ", 't'), ("ùúûüũūŭůűų", 'u'), ("ŵ", 'w'), ("ýÿŷ", 'y'), ("źżž", 'z'),
    }.SelectMany(family => family.Letters.Select(letter => (letter, family.Plain))).ToDictionary(pair => pair.letter, pair => pair.Plain);

    /// <summary>Lower case without accents, so "zurich" finds "Zürich".</summary>
    static string Fold(string text)
    {
        var lower = text.ToLowerInvariant();
        try
        {
            lower = lower.Normalize(NormalizationForm.FormD);
        }
        catch (PlatformNotSupportedException)
        {
            // No decomposition here: the table below does the common letters.
        }
        var plain = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                plain.Append(Unaccented.GetValueOrDefault(c, c));
            }
        }
        return plain.ToString();
    }
}
