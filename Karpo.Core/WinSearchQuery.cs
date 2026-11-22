// §642: Windows search (beta), a search box over Windows' own index, the apps, Settings' pages and the
// calculator, as a folder's line (Karpo.Dock\WinSearch\WinSearchLine.cs). This file is what is typed made
// into a question: the filter a typed prefix names ("documents: budget", as Windows 11's search box takes
// it), the words, and the SQL Windows Search's OLE DB provider is asked (docs\widgets.md §642; Microsoft's
// "Querying the index with Windows Search SQL syntax": CONTAINS, wildcards, literals).
//
// Pure: no Windows here, so the tests hold every escaping and every scope.

using System.Globalization;
using System.Text;

namespace Karpo.Core;

/// <summary>§642: what a result of Windows search is, and so the group it is listed in; also the filter that lists one group alone.</summary>
public enum WinSearchCategory
{
    /// <summary>The calculator's answer (or a unit's), always the best match where there is one.</summary>
    Answer,

    /// <summary>An app of Start's All apps (the shell's Apps folder).</summary>
    Apps,

    /// <summary>A page of Windows' Settings (ms-settings:).</summary>
    Settings,

    /// <summary>A file of the index that is no picture, video or song.</summary>
    Documents,

    /// <summary>A folder of the index.</summary>
    Folders,

    /// <summary>A picture of the index.</summary>
    Photos,

    /// <summary>A video of the index.</summary>
    Videos,

    /// <summary>A song or a recording of the index.</summary>
    Music,

    /// <summary>A mail the index holds (classic Outlook's, where it is indexed); §652 a message Karpo synced from an account.</summary>
    Email,

    /// <summary>§652: an event of the calendars Karpo syncs.</summary>
    Events,

    /// <summary>§652: a person of the address books Karpo syncs.</summary>
    People,

    /// <summary>§652: a note of Karpo's notebook.</summary>
    Notes,

    /// <summary>§652: Karpo itself: a page or a row of its settings, a widget to add, a layout.</summary>
    Karpo,

    /// <summary>The search engine's page for what is typed: only where the line's "Search the web" is on.</summary>
    Web,
}

/// <summary>§642: one question to Windows search: the filter (null for every group) and the words typed.</summary>
/// <param name="Filter">The one group asked for, by a chip or a typed prefix; null for all.</param>
/// <param name="Text">What is typed, the prefix taken off, trimmed.</param>
public readonly record struct WinSearchAsk(WinSearchCategory? Filter, string Text)
{
    /// <summary>The words of <see cref="Text"/>, each one the index is asked for.</summary>
    public IReadOnlyList<string> Words => WinSearchQuery.Words(Text);

    /// <summary>Nothing typed: the line's home (recent searches, recent files).</summary>
    public bool Empty => Text.Length == 0;
}

/// <summary>§642: what is typed made into a question, and the question made into Windows Search's SQL.</summary>
public static class WinSearchQuery
{
    /// <summary>The most words one question asks for: more is a sentence, not a search box's.</summary>
    public const int MostWords = 8;

    /// <summary>The most characters one word is asked for with.</summary>
    public const int MostWordLength = 64;

    /// <summary>The fewest characters a word must have before the files' contents are searched for it too (names always are).</summary>
    public const int ContentsFrom = 3;

    /// <summary>The groups in the order the line lists them, after the best match.</summary>
    public static readonly WinSearchCategory[] Order =
    [
        WinSearchCategory.Answer, WinSearchCategory.Apps, WinSearchCategory.Settings, WinSearchCategory.Documents, WinSearchCategory.Folders,
        WinSearchCategory.Photos, WinSearchCategory.Videos, WinSearchCategory.Music, WinSearchCategory.Email, WinSearchCategory.Events,
        WinSearchCategory.People, WinSearchCategory.Notes, WinSearchCategory.Karpo, WinSearchCategory.Web,
    ];

    /// <summary>The groups a person can filter by (a chip each, a typed prefix each): every one but the answer.</summary>
    public static readonly WinSearchCategory[] Filters = [.. Order.Where(category => category != WinSearchCategory.Answer)];

    /// <summary>
    /// The English prefixes Windows 11's search box takes ("documents: budget"), and a few more said as often; the
    /// group's name in the language Karpo speaks is taken too (<see cref="Parse"/>).
    /// </summary>
    private static readonly (string Word, WinSearchCategory Category)[] Prefixes =
    [
        ("apps", WinSearchCategory.Apps), ("app", WinSearchCategory.Apps), // not-ui: typed prefixes
        ("settings", WinSearchCategory.Settings), ("setting", WinSearchCategory.Settings), // not-ui: typed prefixes
        ("documents", WinSearchCategory.Documents), ("document", WinSearchCategory.Documents), ("docs", WinSearchCategory.Documents), // not-ui: typed prefixes
        ("files", WinSearchCategory.Documents), ("file", WinSearchCategory.Documents), // not-ui: typed prefixes
        ("folders", WinSearchCategory.Folders), ("folder", WinSearchCategory.Folders), // not-ui: typed prefixes
        ("photos", WinSearchCategory.Photos), ("photo", WinSearchCategory.Photos), ("pictures", WinSearchCategory.Photos), ("images", WinSearchCategory.Photos), // not-ui: typed prefixes
        ("videos", WinSearchCategory.Videos), ("video", WinSearchCategory.Videos), // not-ui: typed prefixes
        ("music", WinSearchCategory.Music), ("songs", WinSearchCategory.Music), // not-ui: typed prefixes
        ("email", WinSearchCategory.Email), ("mail", WinSearchCategory.Email), // not-ui: typed prefixes
        ("calendar", WinSearchCategory.Events), ("events", WinSearchCategory.Events), ("event", WinSearchCategory.Events), // not-ui: typed prefixes
        ("contacts", WinSearchCategory.People), ("contact", WinSearchCategory.People), ("people", WinSearchCategory.People), // not-ui: typed prefixes
        ("notes", WinSearchCategory.Notes), ("note", WinSearchCategory.Notes), // not-ui: typed prefixes
        ("karpo", WinSearchCategory.Karpo), // not-ui: typed prefixes
        ("web", WinSearchCategory.Web), // not-ui: typed prefixes
    ];

    /// <summary>
    /// What <paramref name="typed"/> asks: a group's prefix and a colon at its start ("documents: budget", "Fotos:x") is that
    /// group's filter — the English word or the group's name in the language Karpo speaks, any case and accents —, else
    /// <paramref name="chosen"/> (a chip's); the rest trimmed is what is searched for. A colon after anything else (a drive,
    /// "c:\users") is part of what is typed.
    /// </summary>
    public static WinSearchAsk Parse(string? typed, WinSearchCategory? chosen = null)
    {
        var text = (typed ?? "").Trim();
        var colon = text.IndexOf(':');
        if (colon > 0)
        {
            var head = CatalogueSearch.Fold(text[..colon].Trim());
            foreach (var category in Filters)
            {
                if (head == CatalogueSearch.Fold(WinSearchWords.Category(category)))
                {
                    return new WinSearchAsk(category, text[(colon + 1)..].Trim());
                }
            }
            foreach (var (word, category) in Prefixes)
            {
                if (head == word)
                {
                    return new WinSearchAsk(category, text[(colon + 1)..].Trim());
                }
            }
        }
        return new WinSearchAsk(chosen, text);
    }

    /// <summary>
    /// The words of <paramref name="text"/>: split at white space, each without the characters no search needs (control
    /// characters, a wildcard of the person's own) and at most <see cref="MostWordLength"/> long, at most <see cref="MostWords"/>.
    /// </summary>
    public static IReadOnlyList<string> Words(string? text)
    {
        List<string> words = [];
        foreach (var raw in (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var clean = new StringBuilder(raw.Length);
            foreach (var c in raw)
            {
                if (!char.IsControl(c) && c != '*')
                {
                    clean.Append(c);
                }
            }
            var word = clean.ToString();
            if (word.Length == 0)
            {
                continue;
            }
            words.Add(word.Length <= MostWordLength ? word : word[..MostWordLength]);
            if (words.Count == MostWords)
            {
                break;
            }
        }
        return words;
    }

    /// <summary>Whether the index is asked anything for <paramref name="filter"/>: every group's but the apps', Settings', the answer's and the web's.</summary>
    public static bool AsksIndex(WinSearchCategory? filter) =>
        filter is null or WinSearchCategory.Documents or WinSearchCategory.Folders or WinSearchCategory.Photos
            or WinSearchCategory.Videos or WinSearchCategory.Music or WinSearchCategory.Email;

    /// <summary>The columns asked for, in the order the rows hold them (<see cref="WinSearchRow"/>).</summary>
    public static readonly string[] Columns =
    [
        "System.ItemNameDisplay", "System.ItemPathDisplay", "System.ItemUrl", "System.ItemType", "System.DateModified", "System.Size", "System.Search.Rank",
    ];

    /// <summary>System.Kind's word for a group the index holds; null for the others.</summary>
    public static string? KindOf(WinSearchCategory category) => category switch
    {
        WinSearchCategory.Documents => "document",
        WinSearchCategory.Folders => "folder",
        WinSearchCategory.Photos => "picture",
        WinSearchCategory.Videos => "video",
        WinSearchCategory.Music => "music",
        WinSearchCategory.Email => "email",
        _ => null,
    };

    /// <summary>
    /// The SQL Windows Search is asked for <paramref name="ask"/> (Search.CollatorDSO, SystemIndex), or null where the index
    /// has nothing to answer (no words, or a group it does not hold):
    /// <code>SELECT TOP n &lt;Columns&gt; FROM SystemIndex WHERE (word 1) AND (word 2) [AND kind] [AND scope] ORDER BY System.Search.Rank DESC</code>
    /// each word asked of the item's name as a prefix (<c>CONTAINS(System.ItemNameDisplay,'"word*"')</c>), and of what the item
    /// holds where <paramref name="contents"/> is on and the word is <see cref="ContentsFrom"/> long. A filter is its kind
    /// (<c>System.Kind = 'picture'</c>); every group's but the mail's holds to files (<c>SCOPE='file:'</c>), so a mail store's items
    /// come only where mail is asked for or all is. <paramref name="scopes"/> holds the question to those folders and under
    /// (<c>SCOPE='file:C:/Users/me/Documents'</c>, any of them), for a line kept to a few places; none is the whole index.
    /// </summary>
    public static string? Sql(WinSearchAsk ask, bool contents, int top, IReadOnlyList<string>? scopes = null)
    {
        var words = ask.Words;
        if (words.Count == 0 || !AsksIndex(ask.Filter) || top <= 0)
        {
            return null;
        }
        var sql = new StringBuilder(256);
        sql.Append("SELECT TOP ").Append(top.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(string.Join(", ", Columns)); // not-ui: SQL
        sql.Append(" FROM SystemIndex WHERE "); // not-ui: SQL
        for (var at = 0; at < words.Count; at++)
        {
            if (at > 0)
            {
                sql.Append(" AND "); // not-ui: SQL
            }
            var term = Term(words[at]);
            sql.Append("(CONTAINS(System.ItemNameDisplay, ").Append(term).Append(')'); // not-ui: SQL
            if (contents && words[at].Length >= ContentsFrom)
            {
                sql.Append(" OR CONTAINS(System.Search.Contents, ").Append(term).Append(')'); // not-ui: SQL
            }
            sql.Append(')');
        }
        if (ask.Filter is { } filter && KindOf(filter) is { } kind)
        {
            sql.Append(" AND System.Kind = ").Append(Literal(kind)); // not-ui: SQL
        }
        if (ask.Filter is not (null or WinSearchCategory.Email))
        {
            sql.Append(" AND SCOPE = 'file:'"); // not-ui: SQL
        }
        if (scopes is { Count: > 0 })
        {
            sql.Append(" AND ("); // not-ui: SQL
            for (var at = 0; at < scopes.Count; at++)
            {
                sql.Append(at > 0 ? " OR " : "").Append("SCOPE = ").Append(Literal(ScopeUrl(scopes[at]))); // not-ui: SQL
            }
            sql.Append(')');
        }
        sql.Append(" ORDER BY System.Search.Rank DESC"); // not-ui: SQL
        return sql.ToString();
    }

    /// <summary>
    /// One word as a CONTAINS condition's literal: a phrase in double quotes with a wildcard at its end, a double quote in it
    /// doubled (CONTAINS' own rule), then the whole a string literal, a single quote doubled (SQL's).
    /// <c>O'Brien</c> → <c>'"O''Brien*"'</c>; <c>say"hi</c> → <c>'"say""hi*"'</c>.
    /// </summary>
    public static string Term(string word) => Literal("\"" + word.Replace("\"", "\"\"", StringComparison.Ordinal) + "*\"");

    /// <summary>A string literal of Windows Search's SQL: in single quotes, a single quote in it doubled.</summary>
    public static string Literal(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";

    /// <summary>A folder as SCOPE takes it: <c>file:</c> and its path with forward slashes, no slash at its end.</summary>
    public static string ScopeUrl(string folder)
    {
        var path = folder.Trim().Replace('\\', '/').TrimEnd('/');
        return path.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ? path : "file:" + path;
    }
}
