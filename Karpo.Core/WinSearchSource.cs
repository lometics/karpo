// §652: where Windows search looks, each place a source — Windows' index (names, and what files hold), Start's apps,
// Settings' pages, the calculator, the web's row, and Karpo's own: the mail, the calendars, the contacts and the notes it
// keeps, and Karpo itself (docs\widgets.md §652). The person, 2026-11-09: "you should also show results from mail and
// calendar and contacts etc. generally also show results from our application too. (should be configurable / on
// offable.) think of a good system here so that it is extensible as well."
//
// A source is one small thing (IWinSearchSource): its id, whether it is slow (asked once the typing rests, on a thread of
// its own), a way to get ready as the line shows (Warm), and Find — what it has for one question, each hit scored by the
// one ranking (WinSearchRank.NameScore, Score), in the groups it answers in. What a source is called, its glyph, the groups
// it answers in and whether it is on until the person says otherwise are its info, in the one list here
// (WinSearchSourceInfos.All): the order of the widget's "Search in" switches, and the one place a source is named.
//
// To add a source: an id and its info in WinSearchSourceInfos.All (its name in WinSearchWords.Source and in the 40
// languages), a class answering Find, and its instance in the line's list (Karpo.Dock\WinSearch\WinSearchSystem.cs).
// The engine asks it (WinSearchEngine), the results group and rank it, the switch turns it off — nothing else changes.
//
// Pure, the sources of Windows' here: what they read is handed in (an index, an app list), so the tests bring their own.

namespace Karpo.Core;

/// <summary>§652: what a source found for one question: its hits, scored, and why it could not answer (null where it could).</summary>
public sealed record WinSearchFound(IReadOnlyList<WinSearchHit> Hits, string? Problem = null)
{
    public static readonly WinSearchFound None = new([]);
}

/// <summary>§652: one place Windows search looks (see the top of the file).</summary>
public interface IWinSearchSource
{
    /// <summary>Its id: its info's (<see cref="WinSearchSourceInfos"/>), the word config.json switches it by.</summary>
    string Id { get; }

    /// <summary>
    /// Whether it is slow (Windows' index): asked once the typing has rested (<see cref="WinSearchEngine.RestMs"/>), on a
    /// thread of its own; a quick one (what is in memory) is asked at every keystroke.
    /// </summary>
    bool Slow => false;

    /// <summary>
    /// The line is shown: whatever it reads before it can answer is read now (the apps listed, a snapshot taken on the
    /// dock's thread), <paramref name="came"/> called once what was being read has come, so what is typed is asked again.
    /// On the caller's thread; never waits.
    /// </summary>
    void Warm(Action came)
    {
    }

    /// <summary>What it read is read again at the next <see cref="Warm"/> (the line went: apps installed meanwhile).</summary>
    void Forget()
    {
    }

    /// <summary>
    /// What it has for <paramref name="ask"/>, each hit scored (0 for none: left out), off the caller's thread; ended early
    /// where <paramref name="cancel"/> is (a keystroke). May throw: a source that fails answers nothing.
    /// </summary>
    WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel);
}

/// <summary>§652: what a source is, apart from what it finds: its name, its glyph, the groups it answers in, whether it is on by default.</summary>
/// <param name="Id">The word config.json switches it by; never changes once released.</param>
/// <param name="Glyph">A Segoe Fluent Icons code point: its switch's.</param>
/// <param name="Categories">The groups its hits are in: a filter (a chip, a typed prefix) of none of them does not ask it.</param>
/// <param name="On">Whether it is asked until the person says otherwise.</param>
public sealed record WinSearchSourceInfo(string Id, string Glyph, IReadOnlyList<WinSearchCategory> Categories, bool On = true)
{
    /// <summary>Its name, in the language Karpo speaks: its switch's.</summary>
    public string Title => WinSearchWords.Source(Id);

    /// <summary>Whether it answers a question filtered to <paramref name="filter"/> (null: every group).</summary>
    public bool Answers(WinSearchCategory? filter) => filter is not { } only || Categories.Contains(only);
}

/// <summary>§652: every source, in the order the widget's "Search in" lists them (see the top of the file).</summary>
public static class WinSearchSourceInfos
{
    public const string Apps = "apps";
    public const string Settings = "settings";
    public const string Files = "files";
    public const string Contents = "contents";
    public const string Mail = "mail";
    public const string Calendar = "calendar";
    public const string Contacts = "contacts";
    public const string Notes = "notes";
    public const string Karpo = "karpo";
    public const string Calculator = "calculator";
    public const string Web = "web";

    /// <summary>
    /// Every source, in order. On by default, but the web's row: nothing typed leaves the PC unless the person asks for it
    /// (§642). One that needs an account the person has none of is on and finds nothing.
    /// </summary>
    public static readonly IReadOnlyList<WinSearchSourceInfo> All =
    [
        new(Apps, OptionGlyphs.Apps, [WinSearchCategory.Apps]),
        new(Settings, WinSearchPages.Glyph, [WinSearchCategory.Settings]),
        new(Files, OptionGlyphs.Folder, [WinSearchCategory.Documents, WinSearchCategory.Folders, WinSearchCategory.Photos, WinSearchCategory.Videos, WinSearchCategory.Music, WinSearchCategory.Email]),
        new(Contents, OptionGlyphs.Document, [WinSearchCategory.Documents, WinSearchCategory.Photos, WinSearchCategory.Videos, WinSearchCategory.Music, WinSearchCategory.Email]),
        new(Mail, OptionGlyphs.Mail, [WinSearchCategory.Email]),
        new(Calendar, WinSearchWords.Calendar, [WinSearchCategory.Events]),
        new(Contacts, OptionGlyphs.Contact, [WinSearchCategory.People]),
        new(Notes, OptionGlyphs.Note, [WinSearchCategory.Notes]),
        new(Karpo, OptionGlyphs.Taskbar, [WinSearchCategory.Karpo]),
        new(Calculator, OptionGlyphs.Calculator, [WinSearchCategory.Answer]),
        new(Web, OptionGlyphs.Globe, [WinSearchCategory.Web], On: false),
    ];

    /// <summary>The info of <paramref name="id"/>; null for an id no source has (a test's own).</summary>
    public static WinSearchSourceInfo? Of(string id) => All.FirstOrDefault(info => info.Id == id);

    /// <summary>Whether source <paramref name="id"/> is asked by a line set to <paramref name="options"/>: its switch, else its default; a source of no info always.</summary>
    public static bool On(WinSearchOptions? options, string id) =>
        options?.Sources is { } said && said.TryGetValue(id, out var on) ? on : Of(id)?.On ?? true;
}

/// <summary>§652, §642: Start's apps, as the shell lists them, listed once as the line shows and kept until it goes.</summary>
/// <param name="apps">The listing.</param>
/// <param name="run">Runs the listing off the caller's thread.</param>
public sealed class WinSearchAppsSource(IWinSearchApps apps, Action<Action> run) : IWinSearchSource
{
    private readonly Lock gate = new();
    private IReadOnlyList<WinSearchApp>? listed;
    private bool listing;

    /// <summary>tests: how many times the apps were listed.</summary>
    public int Listed { get; private set; }

    public string Id => WinSearchSourceInfos.Apps;

    public void Warm(Action came)
    {
        lock (gate)
        {
            if (listing || listed is not null)
            {
                return;
            }
            listing = true;
        }
        run(() =>
        {
            IReadOnlyList<WinSearchApp> found;
            try
            {
                found = apps.List();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Troubles.Report("Windows search: the apps", error); // not-ui: a trouble's name
                found = [];
            }
            lock (gate)
            {
                listed = found;
                listing = false;
                Listed++;
            }
            came();
        });
    }

    public void Forget()
    {
        lock (gate)
        {
            if (!listing)
            {
                listed = null;
            }
        }
    }

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel)
    {
        IReadOnlyList<WinSearchApp> known;
        lock (gate)
        {
            known = listed ?? [];
        }
        return new([.. WinSearchRank.FromApps(ask, known, default, null)]);
    }
}

/// <summary>§652, §642: Settings' pages (WinSearchPages).</summary>
public sealed class WinSearchSettingsSource : IWinSearchSource
{
    public string Id => WinSearchSourceInfos.Settings;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) => new([.. WinSearchPages.Find(ask, default, null)]);
}

/// <summary>§652, §642: the calculator's and the units' answer, worked out on the PC in the region's marks; always the best match.</summary>
public sealed class WinSearchCalculatorSource(NumberMarks? marks = null) : IWinSearchSource
{
    public string Id => WinSearchSourceInfos.Calculator;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) =>
        ask.Filter is null && WinSearchCalc.Answer(ask.Text, marks ?? NumberMarks.Invariant) is { } answer
            ? new([new WinSearchHit(WinSearchCategory.Answer, answer.Text, answer.Value, answer.Question, WinSearchCalc.Glyph) { Score = 100_000 }])
            : WinSearchFound.None;
}

/// <summary>§652, §642: the web's row, searching what is typed with the app's engine (<paramref name="url"/>); off by default.</summary>
public sealed class WinSearchWebSource(Func<string, string?> url) : IWinSearchSource
{
    public string Id => WinSearchSourceInfos.Web;

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel) =>
        WinSearchRank.Web(ask, url(ask.Text)) is { } web ? new([web]) : WinSearchFound.None;
}

/// <summary>
/// §652, §642: Windows' index — the names of files and folders (<see cref="WinSearchSourceInfos.Files"/>), or with
/// <paramref name="contents"/> what files hold too (<see cref="WinSearchSourceInfos.Contents"/>, asked where a word is
/// <see cref="WinSearchQuery.ContentsFrom"/> letters long; what both find is listed once). Slow: once the typing rests.
/// </summary>
public sealed class WinSearchIndexSource(IWinSearchIndex index, bool contents, Func<DateTime> now) : IWinSearchSource
{
    public string Id => contents ? WinSearchSourceInfos.Contents : WinSearchSourceInfos.Files;

    public bool Slow => true;

    /// <summary>The SQL <paramref name="ask"/> asks it; null where it asks nothing (no words, a group the index does not hold, no word long enough for contents).</summary>
    public string? Sql(WinSearchAsk ask) =>
        contents && !ask.Words.Any(word => word.Length >= WinSearchQuery.ContentsFrom) ? null
        : WinSearchQuery.Sql(ask, contents, WinSearchEngine.MostRows);

    public WinSearchFound Find(WinSearchAsk ask, CancellationToken cancel)
    {
        if (Sql(ask) is not { } sql)
        {
            return WinSearchFound.None;
        }
        var answer = index.Query(sql, cancel);
        return new([.. WinSearchRank.FromIndex(ask, answer.Rows, now(), null)], answer.Problem);
    }
}
