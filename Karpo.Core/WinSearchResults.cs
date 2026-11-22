// §642: Windows search's results — what each source found made into hits, each hit in its group, ranked, the
// best match first and then a group each as Windows 11's search lists them (Best match, Apps, Settings,
// Documents, Folders, Photos, …). The ranking is Karpo's own, over every source alike, so an app typed by
// its initials ("vsc"), a page by a word of its name and a file by the start of a word of its name come in
// the order a person expects, and what was opened from here before comes first.
//
// Pure: the sources (Karpo.Dock\WinSearch) hand in rows; the tests hand in their own.

using System.Globalization;

namespace Karpo.Core;

/// <summary>§642: one item the index answered with, as its columns read (<see cref="WinSearchQuery.Columns"/>).</summary>
/// <param name="Name">System.ItemNameDisplay: what File Explorer calls it.</param>
/// <param name="Path">
/// System.ItemPathDisplay: its path as File Explorer's address bar writes it — a known folder by its display name ("C:\Users\Public\Public
/// Documents", a phone's "Internal storage"), which the shell cannot parse back —; a mail's where it is in its store. What opens a
/// file is its <see cref="Url"/>'s (<see cref="WinSearchRank.PathOf"/>, §661).
/// </param>
/// <param name="Url">System.ItemUrl: "file:C:/…", "mapi://…" for a mail.</param>
/// <param name="Type">System.ItemType: its extension (".docx"), "Directory" for a folder.</param>
/// <param name="Modified">System.DateModified, UTC.</param>
/// <param name="Size">System.Size in bytes; null for a folder.</param>
/// <param name="Rank">System.Search.Rank, 0 to 1000.</param>
public sealed record WinSearchRow(string Name, string Path, string Url, string Type, DateTime? Modified = null, long? Size = null, int Rank = 0);

/// <summary>§642: an app of Start's All apps: its name and what opens it (<c>shell:AppsFolder\&lt;id&gt;</c>).</summary>
public sealed record WinSearchApp(string Name, string Target);

/// <summary>§642: one result of the line: what it is, what it is called, and what opens it.</summary>
/// <param name="Category">Its group.</param>
/// <param name="Name">What the row says.</param>
/// <param name="Target">What opens it: a path, <c>shell:AppsFolder\…</c>, an ms-settings: page, a mail's URL, a web page; the answer's value.</param>
/// <param name="Detail">The line under the name: a file's folder, an app's "App", a page's "Settings"; null for none.</param>
/// <param name="Glyph">
/// A Segoe Fluent Icons code point the row wears where it has no <see cref="Icon"/>, and while its icon is read; null for its
/// group's (<see cref="WinSearchWords.Glyph"/>).
/// </param>
public sealed record WinSearchHit(WinSearchCategory Category, string Name, string Target, string? Detail = null, string? Glyph = null)
{
    private readonly string? key;
    private readonly string? icon;
    private readonly string? copies;

    /// <summary>
    /// §652: what tells it from every other hit of its group, and what the history remembers it by: its <see cref="Target"/>
    /// where none is given — a message whose target is its inbox's page, an event its calendar's day, are told apart by their own.
    /// </summary>
    public string Key
    {
        get => key ?? Target;
        init => key = value;
    }

    /// <summary>
    /// §652: what its icon is the shell's of (Windows' own, read off the dock's thread and kept): a file's or a folder's path
    /// and an app's Apps folder address where none is given; Settings' app for its pages; a kind of file's
    /// (<see cref="KindIcon"/>) for a sample; null for its glyph alone.
    /// </summary>
    public string? Icon
    {
        get => icon ?? (OnDisk || Category == WinSearchCategory.Apps ? Target : null);
        init => icon = value;
    }

    /// <summary>
    /// §652: what a copy puts on the clipboard: a path in quotes, as File Explorer's "Copy as path" puts it, and an answer's
    /// value where none is given; a person's address; null where it has nothing to copy.
    /// </summary>
    public string? Copies
    {
        get => copies ?? (OnDisk ? $"\"{Target}\"" : Category == WinSearchCategory.Answer ? Target : null);
        init => copies = value;
    }

    /// <summary>§652: the source it came from (<see cref="WinSearchSourceInfos"/>' id); empty for none.</summary>
    public string Source { get; init; } = "";

    /// <summary>§652: the scheme of <see cref="KindIcon"/>.</summary>
    public const string KindScheme = "kind:";

    /// <summary>
    /// §652: the icon of a kind of file — Windows' own for its type, "kind:.xlsx" — or of a folder ("kind:\"), where no file of
    /// it is read: a sample's, which is nowhere on a disk.
    /// </summary>
    public static string KindIcon(string name, bool folder = false)
    {
        if (folder)
        {
            return KindScheme + "\\";
        }
        var dot = name.LastIndexOf('.');
        return KindScheme + (dot >= 0 ? name[dot..] : "");
    }

    /// <summary>How well it answers, the higher the better (<see cref="WinSearchRank"/>).</summary>
    public double Score { get; init; }

    /// <summary>When a file was last changed, UTC; null for anything else.</summary>
    public DateTime? Modified { get; init; }

    /// <summary>A file's size in bytes; null for anything else.</summary>
    public long? Size { get; init; }

    /// <summary>Whether it is a file or a folder on a disk: it has a place to open, and a path to copy.</summary>
    public bool OnDisk => Category is WinSearchCategory.Documents or WinSearchCategory.Folders or WinSearchCategory.Photos
        or WinSearchCategory.Videos or WinSearchCategory.Music;

    /// <summary>Whether it may be run as administrator: an app, or a program file.</summary>
    public bool Elevates => Category == WinSearchCategory.Apps
        || (Category == WinSearchCategory.Documents && WinSearchRank.Runs(Target));
}

/// <summary>§642: a group of results as the line lists it: its kind, the hits shown, and how many it found in all.</summary>
public sealed record WinSearchGroup(WinSearchCategory Category, IReadOnlyList<WinSearchHit> Hits, int Found);

/// <summary>§642: what one question found: the best match, then the groups, and what each source said.</summary>
public sealed record WinSearchResults
{
    /// <summary>What was asked.</summary>
    public WinSearchAsk Ask { get; init; }

    /// <summary>The one result listed first, apart from its group; null where nothing was found.</summary>
    public WinSearchHit? Best { get; init; }

    /// <summary>The groups after it, in <see cref="WinSearchQuery.Order"/>, each without the best match.</summary>
    public IReadOnlyList<WinSearchGroup> Groups { get; init; } = [];

    /// <summary>How many each group found, the best match counted in its own: the chips' counts.</summary>
    public IReadOnlyDictionary<WinSearchCategory, int> Counts { get; init; } = new Dictionary<WinSearchCategory, int>();

    /// <summary>Whether the index is still being asked: the line says so under what is already listed.</summary>
    public bool Searching { get; init; }

    /// <summary>Why the index could not be asked (the Windows Search service is off, …); null where it answered.</summary>
    public string? Problem { get; init; }

    /// <summary>Every hit in the order the line lists them: the best match, then each group's.</summary>
    public IEnumerable<WinSearchHit> All => Best is { } best ? Groups.SelectMany(group => group.Hits).Prepend(best) : Groups.SelectMany(group => group.Hits);

    /// <summary>Nothing found.</summary>
    public bool Nothing => Best is null && Groups.Count == 0;

    public static readonly WinSearchResults None = new();
}

/// <summary>§642: how a hit is scored and the hits grouped.</summary>
public static class WinSearchRank
{
    /// <summary>The most of a group listed where every group is (a chip or a prefix lists more).</summary>
    public const int MostPerGroup = 4;

    /// <summary>The most of the one group listed where it is filtered.</summary>
    public const int MostFiltered = 40;

    /// <summary>The extensions a picture has.</summary>
    private static readonly HashSet<string> Pictures = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".heic", ".heif", ".avif", ".ico", ".svg", ".raw", ".cr2", ".nef", ".arw", ".dng", ".jxr",
    };

    /// <summary>The extensions a video has.</summary>
    private static readonly HashSet<string> Movies = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm", ".m4v", ".mpg", ".mpeg", ".3gp", ".flv", ".ts", ".m2ts",
    };

    /// <summary>The extensions a song or a recording has.</summary>
    private static readonly HashSet<string> Songs = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".opus", ".wma", ".aiff", ".alac", ".mid", ".midi",
    };

    /// <summary>The extensions of a program Windows runs, which "Run as administrator" is offered for.</summary>
    private static readonly HashSet<string> Programs = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".bat", ".cmd", ".msc", ".ps1" };

    /// <summary>Whether <paramref name="path"/> is a program Windows runs.</summary>
    public static bool Runs(string path) => Programs.Contains(Extension(path));

    private static string Extension(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot >= 0 && dot > path.LastIndexOfAny(['\\', '/']) ? path[dot..] : "";
    }

    /// <summary>
    /// The group an item of the index is in: a mail by its address (mapi:, outlook…), a folder by its type ("Directory"),
    /// else a picture, a video or a song by its extension, else a document.
    /// </summary>
    public static WinSearchCategory CategoryOf(WinSearchRow row)
    {
        if (!row.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) && row.Url.Length > 0)
        {
            return WinSearchCategory.Email;
        }
        if (string.Equals(row.Type, "Directory", StringComparison.OrdinalIgnoreCase) || string.Equals(row.Type, "File folder", StringComparison.OrdinalIgnoreCase)) // not-ui: the index's words
        {
            return WinSearchCategory.Folders;
        }
        var extension = row.Type.StartsWith('.') ? row.Type : Extension(row.Path);
        return Pictures.Contains(extension) ? WinSearchCategory.Photos
            : Movies.Contains(extension) ? WinSearchCategory.Videos
            : Songs.Contains(extension) ? WinSearchCategory.Music
            : WinSearchCategory.Documents;
    }

    /// <summary>
    /// Whether a row of the index is listed: not a shortcut of Start's own menu, which is an app the apps list already
    /// (and with its own name, not "Word.lnk"); nothing without a name.
    /// </summary>
    public static bool Lists(WinSearchRow row) =>
        row.Name.Length > 0
        && !(string.Equals(Extension(row.Path), ".lnk", StringComparison.OrdinalIgnoreCase)
             && row.Path.Contains(@"\Start Menu\", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// How well <paramref name="name"/> answers <paramref name="words"/>, 0 to 100 — accents and capitals aside, every word
    /// somewhere in it or nothing (0):
    /// <list type="bullet">
    /// <item>100: the whole name is what is typed.</item>
    /// <item>90: the name starts with what is typed.</item>
    /// <item>For each word, the best of: 80 the name starts with it; 70 a word of the name does ("pad" in "Notepad++" no,
    /// "note" in "Sticky Notes" yes, a word after a space, a dash, a dot, an underscore or a capital); 60 the initials of the
    /// name's words do ("vsc", Visual Studio Code); 40 the name holds it; 20 its letters are in order in one word of the name, from its first
    /// ("ntpd", Notepad) — three letters at least. The name's score is its words' mean.</item>
    /// </list>
    /// </summary>
    public static int NameScore(string name, IReadOnlyList<string> words)
    {
        if (words.Count == 0)
        {
            return 0;
        }
        var folded = CatalogueSearch.Fold(name);
        var whole = CatalogueSearch.Fold(string.Join(' ', words));
        if (folded == whole)
        {
            return 100;
        }
        if (folded.StartsWith(whole, StringComparison.Ordinal))
        {
            return 90;
        }
        // A fold that changed the length (ß to ss) leaves the capitals' starts behind: the folded name's own then.
        var starts = folded.Length == name.Length ? WordStarts(name) : WordStarts(folded);
        var initials = string.Concat(starts.Select(at => at < folded.Length ? folded[at] : ' '));
        var sum = 0;
        foreach (var raw in words)
        {
            var word = CatalogueSearch.Fold(raw);
            var score = folded.StartsWith(word, StringComparison.Ordinal) ? 80
                : starts.Any(at => at < folded.Length && string.CompareOrdinal(folded, at, word, 0, word.Length) == 0) ? 70
                : word.Length >= 2 && initials.StartsWith(word, StringComparison.Ordinal) ? 60
                : folded.Contains(word, StringComparison.Ordinal) ? 40
                : word.Length >= 3 && Pieces(folded, starts).Any(piece => piece.StartsWith(word[0]) && InOrder(piece, word)) ? 20
                : 0;
            if (score == 0)
            {
                return 0;
            }
            sum += score;
        }
        return sum / words.Count;
    }

    /// <summary>Where each word of <paramref name="name"/> starts: after a space or a mark, at a capital after a small letter, at a digit after a letter.</summary>
    internal static List<int> WordStarts(string name)
    {
        List<int> starts = [];
        for (var at = 0; at < name.Length; at++)
        {
            var c = name[at];
            if (!char.IsLetterOrDigit(c))
            {
                continue;
            }
            if (at == 0)
            {
                starts.Add(at);
                continue;
            }
            var before = name[at - 1];
            if (!char.IsLetterOrDigit(before)
                || (char.IsUpper(c) && char.IsLower(before))
                || (char.IsDigit(c) && char.IsLetter(before)))
            {
                starts.Add(at);
            }
        }
        return starts;
    }

    /// <summary>The words of a folded name, cut where <paramref name="starts"/> says each begins.</summary>
    private static IEnumerable<string> Pieces(string folded, List<int> starts)
    {
        for (var at = 0; at < starts.Count; at++)
        {
            var from = Math.Min(starts[at], folded.Length);
            var to = at + 1 < starts.Count ? Math.Min(starts[at + 1], folded.Length) : folded.Length;
            yield return folded[from..to];
        }
    }

    /// <summary>Whether every letter of <paramref name="word"/> is in <paramref name="text"/>, in order.</summary>
    private static bool InOrder(string text, string word)
    {
        var at = 0;
        foreach (var c in word)
        {
            at = text.IndexOf(c, at);
            if (at < 0)
            {
                return false;
            }
            at++;
        }
        return true;
    }

    /// <summary>What a group weighs, so that of two hits named alike the one a person most often means comes first: an app, then a page of Settings, then a folder.</summary>
    private static double Weight(WinSearchCategory category) => category switch
    {
        WinSearchCategory.Answer => 10_000,
        WinSearchCategory.Apps => 150,
        WinSearchCategory.Settings => 80,
        WinSearchCategory.Karpo => 40,
        WinSearchCategory.Folders => 30,
        WinSearchCategory.Web => -1_000,
        _ => 0,
    };

    /// <summary>
    /// A hit's score: its name's (<see cref="NameScore"/>) times ten — an item of the index whose name answers nothing but
    /// whose contents did has 5 —, its group's weight, how fresh a file is (a day 30, a week 15, a month 5), the index's own
    /// rank (a fiftieth of it, 0 to 20: a tiebreak, never a name's answer) and what the person opened from here before (<paramref name="opened"/>: 200, and as much
    /// again where it was opened for words starting so). <paramref name="named"/> is the name's score where it is worked out
    /// otherwise (a page of Settings, found by its English name or a word of its own).
    /// </summary>
    public static double Score(WinSearchHit hit, IReadOnlyList<string> words, DateTime now, WinSearchHistory? opened = null, int rank = 0, int? named = null)
    {
        var name = named ?? NameScore(hit.Name, words);
        if (hit.Category == WinSearchCategory.Answer || hit.Category == WinSearchCategory.Web)
        {
            name = 100;
        }
        if (name == 0 && !hit.OnDisk && hit.Category != WinSearchCategory.Email)
        {
            return 0;
        }
        var score = (name == 0 ? 5 : name * 10) + Weight(hit.Category) + (Math.Clamp(rank, 0, 1000) / 50.0);
        if (hit.Modified is { } modified)
        {
            var age = now - modified;
            score += age <= TimeSpan.FromDays(1) ? 30 : age <= TimeSpan.FromDays(7) ? 15 : age <= TimeSpan.FromDays(30) ? 5 : 0;
        }
        if (opened is not null)
        {
            score += opened.Boost(hit.Key, string.Join(' ', words));
        }
        return score;
    }

    /// <summary>
    /// The results of <paramref name="ask"/> from every source's hits, scored already (<see cref="Score"/>): each target once
    /// (its best), what scores nothing left out, the best of all the best match — the answer wherever there is one —, then a
    /// group each in <see cref="WinSearchQuery.Order"/>, best first, names alike by name; at most <see cref="MostPerGroup"/>
    /// of each, <see cref="MostFiltered"/> of a filtered one. The web's row is never the best match while anything else answers.
    /// </summary>
    public static WinSearchResults Group(WinSearchAsk ask, IEnumerable<WinSearchHit> hits, bool searching = false, string? problem = null)
    {
        var kept = hits
            .Where(hit => hit.Score > 0 && (ask.Filter is null || hit.Category == ask.Filter || hit.Category == WinSearchCategory.Answer))
            .GroupBy(hit => hit.Category + "\n" + hit.Key, StringComparer.OrdinalIgnoreCase)
            .Select(same => same.MaxBy(hit => hit.Score)!)
            .OrderByDescending(hit => hit.Score)
            .ThenBy(hit => hit.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var counts = kept.GroupBy(hit => hit.Category).ToDictionary(group => group.Key, group => group.Count());
        var best = kept.FirstOrDefault(hit => hit.Category != WinSearchCategory.Web) ?? kept.FirstOrDefault();
        var most = ask.Filter is null ? MostPerGroup : MostFiltered;
        List<WinSearchGroup> groups = [];
        foreach (var category in WinSearchQuery.Order)
        {
            var all = kept.Where(hit => hit.Category == category).ToList();
            var shown = all.Where(hit => !ReferenceEquals(hit, best)).Take(most).ToList();
            if (shown.Count > 0)
            {
                groups.Add(new WinSearchGroup(category, shown, all.Count));
            }
        }
        return new WinSearchResults
        {
            Ask = ask,
            Best = best,
            Groups = groups,
            Counts = counts,
            Searching = searching,
            Problem = problem,
        };
    }

    /// <summary>
    /// The index's rows as hits of <paramref name="ask"/>: each listed one (<see cref="Lists"/>) in its group, its folder the
    /// line under its name, its address what opens it (a file's path, a mail's URL).
    /// </summary>
    public static IEnumerable<WinSearchHit> FromIndex(WinSearchAsk ask, IEnumerable<WinSearchRow> rows, DateTime now, WinSearchHistory? opened)
    {
        var words = ask.Words;
        foreach (var row in rows.Where(Lists))
        {
            var category = CategoryOf(row);
            var target = category == WinSearchCategory.Email ? row.Url : PathOf(row);
            if (target.Length == 0)
            {
                continue;
            }
            var hit = new WinSearchHit(category, row.Name, target, category == WinSearchCategory.Email ? null : FolderOf(target))
            {
                Modified = row.Modified,
                Size = category == WinSearchCategory.Folders ? null : row.Size,
            };
            yield return hit with { Score = Score(hit, words, now, opened, row.Rank) };
        }
    }

    /// <summary>
    /// §661: the path of a file or folder of the index, the one the shell parses: its <see cref="WinSearchRow.Url"/>'s
    /// ("file:C:/Users/Public/Documents/x.ini" → "C:\Users\Public\Documents\x.ini"; "file://server/share/x" → "\\server\share\x"),
    /// not its display path (<see cref="WinSearchRow.Path"/>), which names a known folder as Explorer shows it ("Public
    /// Documents") — its icon, opening it, its location and the path copied all failed there. The display path where the
    /// address is no file's.
    /// </summary>
    public static string PathOf(WinSearchRow row)
    {
        if (!row.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return row.Path;
        }
        var rest = row.Url[5..];
        if (rest.StartsWith("///", StringComparison.Ordinal))
        {
            rest = rest[3..];
        }
        else if (rest.StartsWith("//", StringComparison.Ordinal))
        {
            rest = @"\\" + rest[2..];
        }
        rest = rest.Replace('/', '\\');
        return rest.Length > 0 ? rest : row.Path;
    }

    /// <summary>The folder a path is in, as File Explorer writes it; null for a root or none.</summary>
    public static string? FolderOf(string path)
    {
        var cut = path.TrimEnd('\\').LastIndexOf('\\');
        return cut > 0 ? path[..cut] : null;
    }

    /// <summary>The apps that answer <paramref name="ask"/>, scored; none where a filter other than the apps' is chosen.</summary>
    public static IEnumerable<WinSearchHit> FromApps(WinSearchAsk ask, IEnumerable<WinSearchApp> apps, DateTime now, WinSearchHistory? opened)
    {
        if (ask.Filter is not (null or WinSearchCategory.Apps) || ask.Empty)
        {
            yield break;
        }
        var words = ask.Words;
        foreach (var app in apps)
        {
            var hit = new WinSearchHit(WinSearchCategory.Apps, app.Name, app.Target, WinSearchWords.AppDetail);
            var score = Score(hit, words, now, opened);
            if (score > 0)
            {
                yield return hit with { Score = score };
            }
        }
    }

    /// <summary>The web's row, searching what is typed with the app's engine (<paramref name="url"/>); none without one.</summary>
    public static WinSearchHit? Web(WinSearchAsk ask, string? url) =>
        ask.Empty || url is null || ask.Filter is not (null or WinSearchCategory.Web) ? null
        : new WinSearchHit(WinSearchCategory.Web, WinSearchWords.SearchTheWeb(ask.Text), url, null, "\uE774") { Score = 1 };

    /// <summary>A file's size as File Explorer says it ("12.3 MB"), in the language's own figures.</summary>
    public static string SizeText(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return (unit == 0 ? value.ToString("0", CultureInfo.CurrentCulture) : value.ToString(value < 10 ? "0.#" : "0", CultureInfo.CurrentCulture)) + " " + units[unit];
    }
}
