// menus: a search field's live results, as rows of the menu under it (2026-10-19, docs\widgets.md
// §219): a clock's time zone, a weather's place, a share's symbol, a feed's address, found as the
// person types and picked without Settings.
//
// What is found is the finder's (Karpo.Core\OptionFields.cs, the very searches the settings app's
// boxes make); this is when it is asked and what the rows say meanwhile:
//
//  - **As the field takes the keys** its rows show: the finder's own first ones ("Use my device's
//    location"; a zone's this PC's and the person's own), and for a finder that needs no letters
//    what it finds for nothing typed. §230 (the person: "the items should show up even if i am not
//    searching"): a search's own level opens with them, the field taking the keys as it opens
//    (Flyout.Fields.cs), so nothing typed is never an empty list: a place's and a share's own and the
//    others in use (OptionFinder.Suggested) while too little is typed to ask the service; for a zone
//    this PC's, the person's own, then every other by offset; for an action every one.
//  - **Typing** asks a moment after the last key (the finder's Wait, the page's 300 ms), at once where
//    it asks no service (the zones). Fewer letters than it needs ask nothing. While a service is asked
//    a greyed "Searching…" row stands in for the results; what was found for the words before is gone
//    at once, as the page's box empties its list, since it would be misleading now.
//  - **A newer search wins.** Each is numbered; one that answers after a newer one was asked — or
//    after a pick, or after the field closed — is dropped, and the older is cancelled.
//  - **Nothing found, or no service** is one greyed row saying so, in the page's words.
//  - **A pick is proved before it is kept**, where the finder proves (a share looked up, a feed read):
//    "Looking up AAPL…" meanwhile, and the refusal in words where it is no good.
//  - **Enter** keeps the first found for the words typed, else the words where they are a thing of
//    their own (a symbol written out, an address), else searches at once.
//  - **Every row** (§271, the person: "they should show all the items with the fixed height so that the
//    list is just scrollable in the end"): the menu shows them all in its search's room, scrolling
//    (Flyout.Inline.cs); §228 to §270 showed five and a "Show more (N)".
//
// The finder answers off the menu's thread; post brings each answer back to it (Flyout.Fields.cs posts
// a message to the menu's window), and changed tells the menu to show the rows again.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Where a search field's search is (see the top of the file).</summary>
internal enum SearchPhase
{
    /// <summary>Nothing asked: too few letters, or the field just opened.</summary>
    Idle,

    /// <summary>Typed: asked once the person stops a moment.</summary>
    Waiting,

    /// <summary>Asked, the answer not in.</summary>
    Searching,

    /// <summary>Answered with something.</summary>
    Found,

    /// <summary>Answered with nothing.</summary>
    Nothing,

    /// <summary>Could not ask.</summary>
    Failed,

    /// <summary>A pick being proved.</summary>
    Checking,

    /// <summary>A pick or the words typed refused, in words.</summary>
    Refused,
}

/// <summary>A search field's live results; see the top of the file.</summary>
/// <param name="finder">What finds and proves.</param>
/// <param name="post">Runs an answer back on the menu's thread.</param>
/// <param name="changed">Tells the menu its rows changed, once an answer is in.</param>
internal sealed class MenuSearch(OptionFinder finder, Action<Action> post, Action changed)
{
    /// <summary>The most found rows a search keeps: every zone Windows has (some 140) for nothing typed (§230), not a world.</summary>
    public const int Most = 200;

    private CancellationTokenSource? asking;

    // Every search and every check numbered: an answer to any but the last is dropped.
    private int asked;

    public OptionFinder Finder => finder;

    public SearchPhase Phase { get; private set; }

    /// <summary>Whether its rows show under the field.</summary>
    public bool Open { get; private set; }

    /// <summary>What was last asked, cleaned of extra spaces.</summary>
    public string Query { get; private set; } = "";

    /// <summary>What the last search found, best first, at most <see cref="Most"/>.</summary>
    public IReadOnlyList<OptionFound> Found { get; private set; } = [];

    /// <summary>The greyed row's words: searching, nothing found, no service, a pick being proved or refused; null for none.</summary>
    public string? Said { get; private set; }

    /// <summary>When a search waiting is due, in <see cref="Environment.TickCount64"/>'s ms.</summary>
    public long DueAt { get; private set; }

    /// <summary>
    /// The rows it shows under the field now, picks each: the finder's own first, then what was found
    /// — or, nothing asked (too little typed), what it suggests (§230).
    /// </summary>
    public IReadOnlyList<OptionFound> Rows => !Open ? [] : Phase is SearchPhase.Checking ? []
        : Phase is SearchPhase.Idle && Found.Count == 0 ? Chosen([.. finder.Offered, .. finder.Suggested])
        : Query.Length == 0 ? Chosen([.. finder.Offered, .. Found])
        : [.. finder.Offered, .. Found];

    /// <summary>§411 (R6): with nothing typed, the one the setting holds first, ticked, then the rest in their order.</summary>
    private static IReadOnlyList<OptionFound> Chosen(IReadOnlyList<OptionFound> rows) => [.. rows.Where(row => row.Chosen), .. rows.Where(row => !row.Chosen)];

    /// <summary>How long a search waits after the last key, in ms.</summary>
    public uint WaitMs => (uint)Math.Max(0, finder.Wait.TotalMilliseconds);

    /// <summary>The field took the keys: its rows show, and a finder that needs no letters finds what it has for nothing typed.</summary>
    public void Opened()
    {
        Open = true;
        if (finder.LeastLetters <= 0 && Phase == SearchPhase.Idle && Found.Count == 0)
        {
            Query = "";
            Ask();
        }
    }

    /// <summary>
    /// What the field holds is <paramref name="text"/> now (<paramref name="now"/> in ms): a search
    /// at once where the finder asks no service, else one due a moment later — true then, and the
    /// caller wakes it at <see cref="DueAt"/> (<see cref="Due"/>) —; fewer letters than it needs ask
    /// nothing.
    /// </summary>
    public bool Typed(string text, long now)
    {
        Open = true;
        Cancel();
        asked++;
        Query = Clean(text);
        Found = [];
        Said = null;
        if (Query.Length < finder.LeastLetters)
        {
            Phase = SearchPhase.Idle;
            return false;
        }
        if (finder.Wait <= TimeSpan.Zero)
        {
            Ask();
            return false;
        }
        Phase = SearchPhase.Waiting;
        DueAt = now + WaitMs;
        Said = finder.Remote ? S.SettingsWidgetSearching : null;
        return true;
    }

    /// <summary>The wait is over (<paramref name="now"/> in ms): the search waiting is asked.</summary>
    public void Due(long now)
    {
        if (Phase == SearchPhase.Waiting && now >= DueAt)
        {
            Ask();
        }
    }

    /// <summary>
    /// Enter, with <paramref name="text"/> in the field: the first found for these very words, else
    /// the words kept where they are a thing of their own (<see cref="OptionFinder.Typed"/>: "F" is
    /// Ford), else refused in the finder's words, else a search at once. <paramref name="keep"/> keeps
    /// what is picked, proved.
    /// </summary>
    public void Enter(string text, Action<OptionFound> keep)
    {
        if (Found.Count > 0 && Phase == SearchPhase.Found && Query == Clean(text))
        {
            Pick(Found[0], keep);
            return;
        }
        var typed = finder.Typed(text);
        if (typed?.Found is { } own)
        {
            Pick(own, keep);
            return;
        }
        if (typed?.Said is { } refused)
        {
            Cancel();
            asked++;
            Open = true;
            Found = [];
            Phase = SearchPhase.Refused;
            Said = refused;
            return;
        }
        if (Clean(text).Length >= finder.LeastLetters && finder.LeastLetters < int.MaxValue)
        {
            Cancel();
            Open = true;
            Query = Clean(text);
            Ask();
        }
    }

    /// <summary>
    /// <paramref name="found"/> picked: kept at once where the finder proves nothing, else proved
    /// first ("Looking up…"), and kept or refused in words when the answer is in.
    /// </summary>
    public void Pick(OptionFound found, Action<OptionFound> keep)
    {
        Cancel();
        var number = ++asked;
        if (finder.Checking(found) is not { } checking)
        {
            keep(found);
            return;
        }
        Phase = SearchPhase.Checking;
        Said = checking;
        Open = true;
        var cancel = asking = new CancellationTokenSource();
        Task<OptionChecked> task;
        try
        {
            task = finder.CheckAsync(found, cancel.Token);
        }
        catch (Exception error) when (finder.Expected(error))
        {
            Failed();
            return;
        }
        Answer(task, number, cancel.Token, done =>
        {
            var proved = done.Result;
            if (proved.Found is { } kept)
            {
                Phase = SearchPhase.Idle;
                Said = null;
                Open = false;
                keep(kept);
                return;
            }
            Phase = SearchPhase.Refused;
            Said = proved.Said ?? finder.Failed;
        });
    }

    /// <summary>The field let the keys go or was kept: no rows, and nothing asked any more.</summary>
    public void Close()
    {
        Cancel();
        asked++;
        Open = false;
        Phase = SearchPhase.Idle;
        Query = "";
        Found = [];
        Said = null;
    }

    private void Ask()
    {
        Cancel();
        var number = ++asked;
        var cancel = asking = new CancellationTokenSource();
        Phase = SearchPhase.Searching;
        Said = finder.Remote ? S.SettingsWidgetSearching : null;
        var query = Query;
        Task<IReadOnlyList<OptionFound>> task;
        try
        {
            task = finder.FindAsync(query, cancel.Token);
        }
        catch (Exception error) when (finder.Expected(error))
        {
            Failed();
            return;
        }
        Answer(task, number, cancel.Token, done =>
        {
            Found = [.. done.Result.Take(Most)];
            Phase = Found.Count > 0 ? SearchPhase.Found : SearchPhase.Nothing;
            Said = Found.Count > 0 ? null : finder.NoneFound(query);
        });
    }

    /// <summary>
    /// <paramref name="task"/>'s answer to search or check <paramref name="number"/>, taken on the
    /// menu's thread — at once where it is in already — unless a newer one was asked or it was called
    /// off (<paramref name="cancel"/>); then the menu is told. One that failed, or ran out of time
    /// without being called off, could not ask.
    /// </summary>
    private void Answer<T>(Task<T> task, int number, CancellationToken cancel, Action<Task<T>> take)
    {
        bool Take()
        {
            if (number != asked || cancel.IsCancellationRequested)
            {
                return false;
            }
            if (task.IsCanceled)
            {
                Failed();
            }
            else if (task.Exception?.InnerException is { } error)
            {
                Failed(error);
            }
            else
            {
                take(task);
            }
            return true;
        }
        if (task.IsCompleted)
        {
            _ = Take();
            return;
        }
        // On the pool, the answer is handed to the menu's thread; Take runs there.
        _ = task.ContinueWith(
            _ => Troubles.Guard("Menu search: an answer back", () => post(() => // not-ui: a trouble's name
            {
                if (Take())
                {
                    changed();
                }
            })),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void Failed(Exception? error = null)
    {
        if (error is not null && !finder.Expected(error))
        {
            Troubles.Note("Menu search", error); // not-ui: a trouble's name
        }
        Found = [];
        Phase = SearchPhase.Failed;
        Said = finder.Failed;
    }

    private void Cancel()
    {
        asking?.Cancel();
        asking = null;
    }

    private static string Clean(string text) => string.Join(' ', (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
