// §655: Windows search's home — what the line shows before anything is typed (the person, 2026-11-09: "even when i have
// nothing typed in search widget yet it should already show usable contents like recent files, folders, pinned stuff, apps
// etc whatever. this should be configurable to a high degree").
//
// The home is sections, each one small thing as a search's source is (§652): a home source (IWinSearchHomeSource) answers
// what it has to show (List), most first, and may get ready as the line shows (Warm). What a section is — its name, its
// glyph, whether its items stand as rows or as tiles, how many it shows until the person says otherwise and how many it may
// show at most, whether it is shown at all by default — is its info, in the one list here (WinSearchHomeInfos.All), which is
// also the order the sections stand in until the person orders them. The recent searches are the engine's own (its history):
// a section of the list like the others, ordered, switched and counted as they are, its items the words searched.
//
// What the person sets (WinSearchOptions: "home", "homeMost") is applied as the line is drawn (WinSearchHome.For): the
// sections shown in their order, each cut to its count, a thing a section before it showed not shown again (an app
// pinned and used a lot is one tile), a section with nothing to show left out.
//
// To add a section: an id and its info in WinSearchHomeInfos.All (its name in WinSearchWords.Home and the 40 languages)
// and a home source answering List, in the line's list (Karpo.Dock\WinSearch\WinSearchSystem.cs). Its switch, its count and
// its place in the order appear on the widget's page; nothing else changes.

namespace Karpo.Core;

/// <summary>§655: how a home section's items stand.</summary>
public enum WinSearchHomeLook
{
    /// <summary>A row each: an icon, a name and what it is (files, folders, events, mail, notes).</summary>
    Rows,

    /// <summary>A tile each, its icon over its name, as many across as the line has room for (apps).</summary>
    Tiles,

    /// <summary>The searches made lately: a row each, typed again by a click, forgotten by its ×.</summary>
    Searches,
}

/// <summary>§655: what a home section is, apart from what it shows (see the top of the file).</summary>
/// <param name="Id">The word config.json names it by; never changes once released.</param>
/// <param name="Glyph">A Segoe Fluent Icons code point: its switch's.</param>
/// <param name="Look">How its items stand.</param>
/// <param name="Shown">How many it shows until the person says otherwise.</param>
/// <param name="Most">How many it may show at most: what it is asked for, and its count's highest.</param>
/// <param name="On">Whether it is shown until the person says otherwise.</param>
public sealed record WinSearchHomeInfo(string Id, string Glyph, WinSearchHomeLook Look, int Shown, int Most, bool On = true)
{
    /// <summary>Its name, in the language Karpo speaks: its heading's and its switch's.</summary>
    public string Title => WinSearchWords.Home(Id);
}

/// <summary>§655: every home section, in the order they stand until the person orders them (see the top of the file).</summary>
public static class WinSearchHomeInfos
{
    public const string Searches = "searches";
    public const string Apps = "apps";
    public const string Pinned = "pinned";
    public const string Files = "files";
    public const string Folders = "folders";
    public const string Events = "events";
    public const string Mail = "mail";
    public const string Notes = "notes";

    /// <summary>The fewest a section shown shows.</summary>
    public const int Fewest = 1;

    /// <summary>Every section, in order: all shown but the notes.</summary>
    public static readonly IReadOnlyList<WinSearchHomeInfo> All =
    [
        new(Searches, OptionGlyphs.History, WinSearchHomeLook.Searches, Shown: 4, Most: WinSearchHistory.MostSearches),
        new(Apps, OptionGlyphs.Apps, WinSearchHomeLook.Tiles, Shown: 6, Most: 24),
        new(Pinned, OptionGlyphs.Pin, WinSearchHomeLook.Tiles, Shown: 12, Most: 24),
        new(Files, OptionGlyphs.Document, WinSearchHomeLook.Rows, Shown: 6, Most: 24),
        new(Folders, OptionGlyphs.Folder, WinSearchHomeLook.Rows, Shown: 4, Most: 12),
        new(Events, WinSearchWords.Calendar, WinSearchHomeLook.Rows, Shown: 3, Most: 12),
        new(Mail, OptionGlyphs.Mail, WinSearchHomeLook.Rows, Shown: 3, Most: 12),
        new(Notes, OptionGlyphs.Note, WinSearchHomeLook.Rows, Shown: 3, Most: 12, On: false),
    ];

    /// <summary>The info of <paramref name="id"/>; null for an id no section has.</summary>
    public static WinSearchHomeInfo? Of(string id) => All.FirstOrDefault(info => info.Id == id);

    /// <summary>The sections shown until the person says otherwise, in order.</summary>
    public static IReadOnlyList<string> Defaults { get; } = [.. All.Where(info => info.On).Select(info => info.Id)];

    /// <summary>The sections a line set to <paramref name="options"/> shows, in its order: its own, else <see cref="Defaults"/>.</summary>
    public static IReadOnlyList<WinSearchHomeInfo> Shown(WinSearchOptions? options) =>
        [.. (options?.Home ?? Defaults).Select(Of).OfType<WinSearchHomeInfo>()];

    /// <summary>How many of section <paramref name="id"/> a line set to <paramref name="options"/> shows: its own count, else the section's.</summary>
    public static int Count(WinSearchOptions? options, string id) =>
        Of(id) is not { } info ? 0
        : options?.HomeMost is { } said && said.TryGetValue(id, out var count) ? Math.Clamp(count, Fewest, info.Most)
        : info.Shown;
}

/// <summary>§655: what a home source is asked: how many at most, the time, and what was opened from the line lately (the history's, as it stood).</summary>
public readonly record struct WinSearchHomeAsk(int Most, DateTime Now, IReadOnlyList<WinSearchOpened> Opened);

/// <summary>§655: one section of the home (see the top of the file).</summary>
public interface IWinSearchHomeSource
{
    /// <summary>Its id: its info's (<see cref="WinSearchHomeInfos"/>).</summary>
    string Id { get; }

    /// <summary>
    /// The line is shown: whatever it reads is read again where it is stale, <paramref name="came"/> called whenever what it
    /// shows has changed. On the caller's thread; never waits.
    /// </summary>
    void Warm(Action came)
    {
    }

    /// <summary>The line has gone: <see cref="Warm"/>'s <c>came</c> is called no more.</summary>
    void Forget()
    {
    }

    /// <summary>What it shows, most first, <see cref="WinSearchHomeAsk.Most"/> at most; off the caller's thread. May throw: then nothing.</summary>
    IReadOnlyList<WinSearchHit> List(WinSearchHomeAsk ask);
}

/// <summary>§655: a home source of a function: what it lists, and what it does as the line shows and goes.</summary>
public sealed class WinSearchHomeSource(string id, Func<WinSearchHomeAsk, IReadOnlyList<WinSearchHit>> list, Action<Action>? warm = null, Action? forget = null)
    : IWinSearchHomeSource
{
    public string Id => id;

    public void Warm(Action came) => warm?.Invoke(came);

    public void Forget() => forget?.Invoke();

    public IReadOnlyList<WinSearchHit> List(WinSearchHomeAsk ask) => list(ask);
}

/// <summary>§655: a section of the home and what it has: its items, or for the recent searches the words.</summary>
public sealed record WinSearchHomeSection(WinSearchHomeInfo Info, IReadOnlyList<WinSearchHit> Hits, IReadOnlyList<string> Searches)
{
    /// <summary>How many it has.</summary>
    public int Count => Info.Look == WinSearchHomeLook.Searches ? Searches.Count : Hits.Count;
}

/// <summary>§655: the home: every section's items as last listed, and the searches made lately.</summary>
public sealed record WinSearchHome(IReadOnlyList<string> Searches, IReadOnlyList<WinSearchHomeSection> Sections)
{
    public static readonly WinSearchHome None = new([], []);

    /// <summary>
    /// What a line set to <paramref name="options"/> shows (see the top of the file): the sections it shows, in its order, each
    /// cut to its count; a thing an earlier section showed not shown again; a section with nothing left out.
    /// </summary>
    public IReadOnlyList<WinSearchHomeSection> For(WinSearchOptions? options)
    {
        List<WinSearchHomeSection> shown = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (var info in WinSearchHomeInfos.Shown(options))
        {
            var count = WinSearchHomeInfos.Count(options, info.Id);
            WinSearchHomeSection section;
            if (info.Look == WinSearchHomeLook.Searches)
            {
                section = new(info, [], [.. Searches.Take(count)]);
            }
            else
            {
                var listed = Sections.FirstOrDefault(each => each.Info.Id == info.Id)?.Hits ?? [];
                section = new(info, [.. listed.Where(hit => !seen.Contains(hit.Key)).Take(count)], []);
                seen.UnionWith(section.Hits.Select(hit => hit.Key));
            }
            if (section.Count > 0)
            {
                shown.Add(section);
            }
        }
        return shown;
    }
}

/// <summary>§655: the home's items made, alike from whatever they are read from (the dock's, the samples', a test's).</summary>
public static class WinSearchHomeHits
{
    /// <summary>A file or a folder at <paramref name="path"/>, used at <paramref name="when"/> (UTC): its group by its kind, its folder under its name.</summary>
    public static WinSearchHit OfPath(string path, DateTime? when, bool folder)
    {
        var name = Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } own ? own : path;
        var row = new WinSearchRow(name, path, "file:" + path.Replace('\\', '/'), folder ? "Directory" : Path.GetExtension(path)); // not-ui: the index's word
        return new WinSearchHit(WinSearchRank.CategoryOf(row), name, path, WinSearchRank.FolderOf(path)) { Modified = when };
    }

    /// <summary>An app, as a search lists it.</summary>
    public static WinSearchHit OfApp(WinSearchApp app) => new(WinSearchCategory.Apps, app.Name, app.Target, WinSearchWords.AppDetail);

    /// <summary>
    /// The apps opened most — how often each was opened from Karpo's Start (<paramref name="started"/>) and from the line
    /// (<paramref name="opened"/>) together, the one opened last first of two alike —, <paramref name="most"/> at most; §658
    /// where fewer were ever opened (a first use), filled from <paramref name="usual"/> in its order (Start's pinned apps), so
    /// the section is never empty on a PC that has apps.
    /// </summary>
    public static IReadOnlyList<WinSearchHit> TopApps(IEnumerable<WinSearchApp> apps, Func<string, (int Count, DateTime Last)> started, IReadOnlyList<WinSearchOpened> opened, int most, IEnumerable<WinSearchApp>? usual = null)
    {
        var fromLine = opened.Where(each => each.Category == WinSearchCategory.Apps)
            .GroupBy(each => each.Target, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => (Count: group.Count(), Last: group.Max(each => each.When)), StringComparer.OrdinalIgnoreCase);
        var used = apps
            .Select(app =>
            {
                var (count, last) = started(app.Target);
                if (fromLine.TryGetValue(app.Target, out var line))
                {
                    count += line.Count;
                    last = line.Last > last ? line.Last : last;
                }
                return (App: app, Count: count, Last: last);
            })
            .Where(each => each.Count > 0)
            .OrderByDescending(each => each.Count)
            .ThenByDescending(each => each.Last)
            .ThenBy(each => each.App.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(each => each.App);
        return [.. used.Concat(usual ?? []).DistinctBy(app => app.Target, StringComparer.OrdinalIgnoreCase).Take(most).Select(OfApp)];
    }
}
