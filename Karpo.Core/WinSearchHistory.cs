// §642: what Windows search remembers on this PC — the searches made lately, for its home (as Windows 11's search
// lists "Recent searches"), and what was opened from it and for which words, so that what a person opens again comes
// first next time (Windows 11's best match learns the same way). Machine-local state, not a setting: kept in
// Config.LocalFolder\winsearch.txt (an isolated run's own), a line each, never in config.json; "Clear search history"
// on the line's menu empties it.

using System.Globalization;
using System.Text;

namespace Karpo.Core;

/// <summary>§642: one thing opened from Windows search: what, for which words, and when.</summary>
public sealed record WinSearchOpened(WinSearchCategory Category, string Target, string Name, string Query, DateTime When);

/// <summary>§642: Windows search's memory (see the top of the file). Not thread-safe: the dock's thread alone uses it.</summary>
public sealed class WinSearchHistory
{
    /// <summary>The most searches remembered, the newest first.</summary>
    public const int MostSearches = 8;

    /// <summary>The most things opened remembered, the newest first.</summary>
    public const int MostOpened = 60;

    /// <summary>What a target opened before adds to its score.</summary>
    public const double OpenedBoost = 200;

    private readonly List<string> searches = [];
    private readonly List<WinSearchOpened> opened = [];

    /// <summary>The searches made lately, the newest first.</summary>
    public IReadOnlyList<string> Searches => searches;

    /// <summary>What was opened lately, the newest first.</summary>
    public IReadOnlyList<WinSearchOpened> Opened => opened;

    /// <summary>Raised after every change: the dock writes the file.</summary>
    public event Action? Changed;

    /// <summary><paramref name="text"/> was searched for (a result opened for it, or Enter): first among the searches, once.</summary>
    public void Searched(string text)
    {
        var trimmed = Clean(text);
        if (trimmed.Length == 0)
        {
            return;
        }
        searches.RemoveAll(each => string.Equals(each, trimmed, StringComparison.CurrentCultureIgnoreCase));
        searches.Insert(0, trimmed);
        if (searches.Count > MostSearches)
        {
            searches.RemoveRange(MostSearches, searches.Count - MostSearches);
        }
        Changed?.Invoke();
    }

    /// <summary><paramref name="hit"/> was opened for <paramref name="query"/>, now: remembered first, the search with it.</summary>
    public void Open(WinSearchHit hit, string query, DateTime now)
    {
        if (hit.Category is WinSearchCategory.Answer or WinSearchCategory.Web)
        {
            Searched(query);
            return;
        }
        // §652: by its key, which is its target but where several share one (messages of one inbox's page).
        opened.RemoveAll(each => string.Equals(each.Target, hit.Key, StringComparison.OrdinalIgnoreCase));
        opened.Insert(0, new WinSearchOpened(hit.Category, Clean(hit.Key), Clean(hit.Name), Clean(query), now));
        if (opened.Count > MostOpened)
        {
            opened.RemoveRange(MostOpened, opened.Count - MostOpened);
        }
        Searched(query);
    }

    /// <summary>Forgets <paramref name="text"/> among the searches.</summary>
    public void Forget(string text)
    {
        if (searches.RemoveAll(each => string.Equals(each, text, StringComparison.CurrentCultureIgnoreCase)) > 0)
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Forgets everything.</summary>
    public void Clear()
    {
        searches.Clear();
        opened.Clear();
        Changed?.Invoke();
    }

    /// <summary>
    /// What <paramref name="target"/> opened before adds for <paramref name="typed"/>: nothing where it never was;
    /// <see cref="OpenedBoost"/> where it was, and as much again where it was opened for words that start as these do
    /// (typing "pa" after opening Paint for "paint", or "paint" after opening it for "pa").
    /// </summary>
    public double Boost(string target, string typed)
    {
        var found = opened.Find(each => string.Equals(each.Target, target, StringComparison.OrdinalIgnoreCase));
        if (found is null)
        {
            return 0;
        }
        var now = CatalogueSearch.Fold(typed.Trim());
        var then = CatalogueSearch.Fold(found.Query);
        var same = now.Length > 0 && then.Length > 0 && (then.StartsWith(now, StringComparison.Ordinal) || now.StartsWith(then, StringComparison.Ordinal));
        return OpenedBoost * (same ? 2 : 1);
    }

    /// <summary>Text with no tab or line break, which the file's lines are cut at.</summary>
    private static string Clean(string text) => text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

    /// <summary>The file's text: "s", a search, a line each; "o", what was opened, its group, target, name, words and UTC ticks.</summary>
    public string Write()
    {
        var text = new StringBuilder();
        foreach (var search in searches)
        {
            text.Append("s\t").Append(search).Append('\n');
        }
        foreach (var each in opened)
        {
            text.Append("o\t").Append(each.Category).Append('\t').Append(each.Target).Append('\t').Append(each.Name).Append('\t').Append(each.Query)
                .Append('\t').Append(each.When.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The memory a file's text holds; a line it cannot read is left out, whatever else it holds kept.</summary>
    public static WinSearchHistory Read(string? text)
    {
        var history = new WinSearchHistory();
        foreach (var line in (text ?? "").Split('\n'))
        {
            var parts = line.TrimEnd('\r').Split('\t');
            if (parts is ["s", var search] && search.Trim().Length > 0 && history.searches.Count < MostSearches)
            {
                history.searches.Add(search.Trim());
            }
            else if (parts is ["o", var group, var target, var name, var query, var ticks]
                && Enum.TryParse<WinSearchCategory>(group, out var category) && Enum.IsDefined(category)
                && target.Length > 0
                && long.TryParse(ticks, NumberStyles.None, CultureInfo.InvariantCulture, out var at) && at >= 0 && at <= DateTime.MaxValue.Ticks
                && history.opened.Count < MostOpened)
            {
                history.opened.Add(new WinSearchOpened(category, target, name, query, new DateTime(at, DateTimeKind.Utc)));
            }
        }
        return history;
    }

    /// <summary>Where the dock keeps it: this PC's own state, an isolated run's own.</summary>
    public static string Path => System.IO.Path.Combine(Config.LocalFolder, "winsearch.txt");

    /// <summary>The memory the file holds; an empty one where there is none or it cannot be read.</summary>
    public static WinSearchHistory Load(string path)
    {
        try
        {
            return DurableFile.ReadText(path, out var text) == FileRead.Ok ? Read(text) : new WinSearchHistory();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return new WinSearchHistory();
        }
    }

    /// <summary>Writes it to <paramref name="path"/>; a failure is reported, not thrown.</summary>
    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
            DurableFile.Write(path, Write(), keepBackup: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Troubles.Report("Windows search: keeping its history", error); // not-ui: a trouble's name
        }
    }
}
