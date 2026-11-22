// What Karpo's windows are asked to be, kept until their content is on the screen (docs\widgets.md
// §354; the flicker design's rule R3: one presentation frame per message, fenced on the commit), and
// where each stands in the z-order (§359, ZPlan).
//
// A window's content (composition) reaches DWM when the dock's thread commits, after the handler
// returns; a window's own operations (shown, hidden, raised, destroyed) reach it at once. Done in the
// same handler, a window came up before its content was there — a frame of nothing — and an old
// window went before its replacement was drawn. So every handler is a frame (Begin, End): what it asks
// of a window is written here, as the state it wants, and applied in one plan only once the frame's
// commit has landed (Landed) — the z-order plan as one stack, hides, destroys, then the focus.
//
// A window that is clear while it is out of sight (§359: its content's opacity is nothing then — the
// dims, the edit bars, the rails, every popup, the tooltips, the carry) comes into sight as the frame
// ends instead, before its commit: it shows nothing until the commit brings its content, which lands
// with every other window's in that one commit. And it goes out of sight by its content going clear in
// the frame's commit, the window hidden at the fence after it. So edit mode's dim and the bars' edit
// dress, a folder and the edit bar's folder crumb, a block dropped and the bar drawing it come and go
// in the same frame: the window operations only add room where nothing shows.
//
// The z-order is a plan (ZPlan): each window's stratum, rank and when it came to the front. As a frame
// ends, a window that sinks to a lower stratum goes there at once, with the clear windows coming; one
// that rises keeps its place until its content is there, then rises at the fence. The plan is applied
// only when it changed, or when the dock asks for it again (Restack: Karpo's windows over another
// app's always-on-top one that came up since).
//
// The latest state wins: a window shown and hidden again before its fence lands is never shown. Each
// change carries the frame it was made in (its stamp), and a fence applies only what its frame and
// those before asked; a later frame's change waits for its own. Outside a frame (a test driving a
// popup itself) a change is applied at once, as it always was.
//
// Pure: the windows are reached through IWindowSystem (Surfaces, Win32's), so the rules are tested
// without a window (WindowTableTests).

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>A window of the plan's stack: placed under the one before it (the first over every topmost window), shown where <c>Show</c>.</summary>
internal readonly record struct Stacked(HWND Window, bool Show);

/// <summary>
/// What a plan applies, in order: the stack in one batch, top first (<see cref="Stack"/>: the whole
/// plan of the windows in sight, those coming into sight shown in it; empty where the order stands as
/// it is); the hides, and what each asked to be done once it is hidden (<see cref="Then"/>); the
/// destroys, each its content closed with it; the focus; and, once the screen shows all that, what
/// waited for it (<see cref="Presented"/>).
/// </summary>
internal sealed record WindowPlan(
    IReadOnlyList<Stacked> Stack,
    IReadOnlyList<HWND> Hidden,
    IReadOnlyList<Action> Then,
    IReadOnlyList<(HWND Window, Action? Closed)> Destroyed,
    IReadOnlyList<HWND> Focused,
    IReadOnlyList<Action> Presented)
{
    /// <summary>The regions that shrink once the content is in them (<see cref="WindowTable.Region"/>): each window's last asked.</summary>
    public IReadOnlyList<(HWND Window, IReadOnlyList<RECT> Region)> Regions { get; init; } = [];

    /// <summary>Whether it is the frame's early part, applied before its commit: clear windows shown, sinking ones lowered.</summary>
    public bool Early { get; init; }

    public bool IsEmpty => Stack.Count == 0 && Hidden.Count == 0 && Then.Count == 0 && Destroyed.Count == 0 && Focused.Count == 0 && Presented.Count == 0 && Regions.Count == 0;
}

/// <summary>The windows themselves, as <see cref="WindowTable"/> reaches them: Win32's (Surfaces), or a test's log.</summary>
internal interface IWindowSystem
{
    /// <summary>Whether <paramref name="window"/> is shown now, as Windows has it.</summary>
    bool Visible(HWND window);

    /// <summary>Does what <paramref name="plan"/> says, in its order.</summary>
    void Run(WindowPlan plan);

    /// <summary>Asks for the frame <paramref name="generation"/>'s content to be committed; <see cref="WindowTable.Landed"/> once it has.</summary>
    void Fence(uint generation);

    /// <summary>Asks for <see cref="WindowTable.Woken"/> at about <paramref name="at"/> (<see cref="Now"/>'s ms): a destroy waiting for its fade.</summary>
    void Wake(long at);

    /// <summary>
    /// <paramref name="window"/>'s region (client px) now: the rectangles' union — what of it takes the
    /// pointer and what DWM shows of it.
    /// </summary>
    void Region(HWND window, IReadOnlyList<RECT> rects);

    /// <summary>Now, in ms.</summary>
    long Now { get; }
}

internal sealed class WindowTable(IWindowSystem system)
{
    /// <summary>What a window is asked to be, not applied yet.</summary>
    private sealed class Want
    {
        /// <summary>Shown or hidden; null: as it is.</summary>
        public bool? Shown;

        /// <summary>The keys to it, once it is shown.</summary>
        public bool Focus;

        /// <summary>Destroyed, <see cref="Closed"/> with it, not before <see cref="NotBefore"/>.</summary>
        public bool Destroy;

        public Action? Closed;

        public long NotBefore;

        /// <summary>What its hide asked to be done once the window is out of sight: run once the hide lands, or a show takes its place.</summary>
        public List<Action>? Then;

        /// <summary>The region it shrinks to once its content is there (<see cref="Region(HWND, IReadOnlyList{RECT})"/>), the rectangles' union; null: as it is.</summary>
        public IReadOnlyList<RECT>? Region;

        /// <summary>The frame it was last changed in.</summary>
        public uint Stamp;
    }

    /// <summary>What a window is for the plan: its stratum, its rank in it, and whether it is clear while out of sight.</summary>
    private readonly record struct Layer(Stratum Stratum, int Rank, bool Clear);

    private readonly Dictionary<HWND, Want> wants = [];

    /// <summary>Each window's region as it stands now, the rectangles' union: what was set, grown by what was asked since.</summary>
    private readonly Dictionary<HWND, List<RECT>> regions = [];

    /// <summary>Every window asked anything, till it is destroyed: the windows the plan places.</summary>
    private readonly HashSet<HWND> known = [];

    private readonly Dictionary<HWND, Layer> layers = [];

    /// <summary>When each window came into sight or to the front, by <see cref="clock"/>: the latest on top of its stratum.</summary>
    private readonly Dictionary<HWND, long> fronts = [];
    private long clock;

    /// <summary>The order last applied, top first, and the stratum each stood at in it.</summary>
    private List<HWND> applied = [];
    private readonly Dictionary<HWND, Stratum> standing = [];

    /// <summary>The frame that asked for the plan to be applied again whatever it is (<see cref="Restack"/>); null for none.</summary>
    private uint? restack;

    private readonly List<(Action Action, uint Stamp)> presented = [];
    private int depth;

    /// <summary>The frame open now, or the next; the last ended; the last whose fence landed.</summary>
    private uint open = 1;
    private uint ended;
    private uint landed;

    /// <summary>Whether the frame open now asked for anything.</summary>
    private bool touched;

    /// <summary>What runs a plan: the dock runs it as busy, so the messages it sends take the default path as they always did.</summary>
    public Action<Action>? Around { get; set; }

    /// <summary>
    /// Tests only: frames fence nothing, every ask applied at once as before the fence was there — what
    /// the flicker films compare against (FrameFilm), and how they show they still see the flicker.
    /// </summary>
    internal bool Unfenced { get; set; }

    /// <summary>A frame is open: what is asked now waits for its fence.</summary>
    public bool InFrame => depth > 0 && !Unfenced;

    /// <summary>Nothing waits: every frame's asks are applied.</summary>
    public bool Settled => wants.Count == 0 && presented.Count == 0 && restack is null;

    /// <summary>Tests only: the frames ended, and the last whose fence landed.</summary>
    internal (uint Ended, uint Landed) Generations => (ended, landed);

    public void Begin() => depth++;

    /// <summary>
    /// The frame ends; the outermost one, as it does, runs <paramref name="ending"/> first, inside it — what
    /// the frame changed reconciled once into what it shows (the dock's edit chrome, §359).
    /// </summary>
    public void End(Action? ending = null)
    {
        if (depth == 0)
        {
            return;
        }
        if (depth == 1 && ending is not null)
        {
            try
            {
                ending();
            }
            finally
            {
                depth--;
                Close();
            }
            return;
        }
        if (--depth == 0)
        {
            Close();
        }
    }

    /// <summary>
    /// What the frame open now asked goes to the screen as soon as its content has: a modal loop (a file
    /// dialog, the colour dialog, a message box) is about to begin inside the handler, and its frame would
    /// end only after it.
    /// </summary>
    public void Present()
    {
        if (depth > 0)
        {
            Close();
        }
    }

    private void Close()
    {
        if (!touched)
        {
            return;
        }
        touched = false;
        if (!Unfenced)
        {
            Early();
        }
        ended = open;
        open++;
        system.Fence(ended);
    }

    /// <summary>Frame <paramref name="generation"/>'s content has been committed: what it and those before asked is applied.</summary>
    public void Landed(uint generation)
    {
        if (generation > landed)
        {
            landed = generation;
        }
        Apply(null);
    }

    /// <summary>A destroy's fade may be over.</summary>
    public void Woken() => Apply(null);

    /// <summary>Everything asked, applied now: the dock is closing.</summary>
    public void Flush()
    {
        landed = ended = open;
        touched = false;
        open++;
        Apply(null, flush: true);
    }

    // --- Asks ----------------------------------------------------------------------------------

    /// <summary>
    /// What <paramref name="window"/> is for the z-order plan (<see cref="ZPlan"/>): <paramref name="stratum"/>,
    /// <paramref name="rank"/> in it, and whether its content is <paramref name="clear"/> while it is out of
    /// sight, so it may come into sight before its commit. A window never enlisted is a popup.
    /// </summary>
    public void Enlist(HWND window, Stratum stratum, int rank = 0, bool clear = false)
    {
        if (window == HWND.NULL)
        {
            return;
        }
        known.Add(window);
        var layer = new Layer(stratum, rank, clear);
        if (layers.TryGetValue(window, out var was) && was == layer)
        {
            return;
        }
        layers[window] = layer;
        // Another place in the plan is the frame's to apply.
        touched = true;
        Done(window);
    }

    /// <summary>Shown, where its stratum puts it; the latest shown on top of its stratum.</summary>
    public void Show(HWND window)
    {
        // A window let go of stays gone: its owner is done with it.
        if (wants.TryGetValue(window, out var going) && going.Destroy)
        {
            return;
        }
        known.Add(window);
        // In sight, nothing asked of it: nothing to do — a model reconciled again asks nothing (§359).
        if (going is null && system.Visible(window))
        {
            return;
        }
        if (!Visible(window))
        {
            fronts[window] = ++clock;
        }
        var want = Ask(window);
        want.Shown = true;
        // §612: a hide waiting for its fade is called off by the showing.
        want.NotBefore = 0;
        // What its hide was to do once it was out of sight is done: a showing takes its place.
        if (want.Then is { } then)
        {
            want.Then = null;
            foreach (var action in then)
            {
                action();
            }
        }
        Done(window);
    }

    /// <summary>
    /// Hidden; <paramref name="then"/> once it is out of sight (or shown again first) — §612 and not before
    /// <paramref name="afterMs"/> from now in a frame: its fade out runs out (the edit dim's), as a destroy's does.
    /// </summary>
    public void Hide(HWND window, Action? then = null, int afterMs = 0)
    {
        known.Add(window);
        // Out of sight, nothing asked of it: what was to be done once it is out of sight is done now.
        if (!wants.ContainsKey(window) && !system.Visible(window))
        {
            then?.Invoke();
            return;
        }
        var want = Ask(window);
        if (!want.Destroy)
        {
            want.Shown = false;
            want.NotBefore = InFrame && afterMs > 0 ? Math.Max(want.NotBefore, system.Now + afterMs) : 0;
        }
        want.Focus = false;
        if (then is not null)
        {
            (want.Then ??= []).Add(then);
        }
        Done(window);
    }

    /// <summary>
    /// Destroyed, <paramref name="closed"/> (its content) with it, once its content is gone from the
    /// screen — and not before <paramref name="afterMs"/> from now in a frame: its closing fade runs out.
    /// </summary>
    public void Destroy(HWND window, Action? closed = null, int afterMs = 0)
    {
        known.Add(window);
        var want = Ask(window);
        want.Destroy = true;
        want.Shown = false;
        want.Focus = false;
        want.Closed = want.Closed is { } before && closed is not null ? before + closed : want.Closed ?? closed;
        want.NotBefore = InFrame ? Math.Max(want.NotBefore, system.Now + afterMs) : 0;
        Done(window);
    }

    /// <summary>To the front of its stratum (of those brought there in the same frame, the last one asked).</summary>
    public void Front(HWND window)
    {
        if (window == HWND.NULL)
        {
            return;
        }
        known.Add(window);
        fronts[window] = ++clock;
        touched = true;
        Done(window);
    }

    /// <summary>
    /// The plan applied again as the frame lands, whether it changed or not: Karpo's windows over another
    /// app's always-on-top one that came up in front since (a bar coming out of hiding, the desktop shown).
    /// </summary>
    public void Restack()
    {
        restack ??= open;
        touched = true;
        if (!InFrame)
        {
            touched = false;
            Apply(null);
        }
    }

    /// <summary>
    /// The window's region (client px) is <paramref name="rect"/> — what takes the pointer and what DWM shows
    /// of it (§355: a popup's window spans its display; its region is where the panel stands). In sight and
    /// in a frame, it grows at once to hold the new place as well as the old — room added, nothing shown that
    /// was not — and shrinks to the new once the content is there; hidden, or outside a frame, at once.
    /// </summary>
    public void Region(HWND window, RECT rect) => Region(window, [rect]);

    /// <summary>
    /// The window's region (client px) is the union of <paramref name="rects"/>, as <see cref="Region(HWND, RECT)"/>:
    /// a bar's panels and its grip, each a rectangle of its own (§358).
    /// </summary>
    public void Region(HWND window, IReadOnlyList<RECT> rects)
    {
        var asked = wants.TryGetValue(window, out var going) ? going : null;
        if (asked is { Destroy: true } || rects.Count == 0)
        {
            return;
        }
        // As it is and nothing else asked: nothing to do.
        if (asked?.Region is null && regions.TryGetValue(window, out var standing) && Same(standing, rects))
        {
            return;
        }
        if (!InFrame || !system.Visible(window))
        {
            if (wants.TryGetValue(window, out var waiting))
            {
                waiting.Region = null;
            }
            SetRegion(window, [.. rects]);
            return;
        }
        if (regions.TryGetValue(window, out var now) && !rects.All(rect => now.Exists(part => Holds(part, rect))))
        {
            SetRegion(window, [.. now, .. rects.Where(rect => !now.Exists(part => Holds(part, rect)))]);
        }
        Ask(window).Region = [.. rects];
    }

    private void SetRegion(HWND window, List<RECT> rects)
    {
        regions[window] = rects;
        system.Region(window, rects);
    }

    private static bool Same(RECT a, RECT b) => a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;

    private static bool Same(IReadOnlyList<RECT> a, IReadOnlyList<RECT> b) => a.Count == b.Count && a.Zip(b).All(pair => Same(pair.First, pair.Second));

    private static bool Holds(RECT outer, RECT inner) =>
        inner.left >= outer.left && inner.top >= outer.top && inner.right <= outer.right && inner.bottom <= outer.bottom;

    /// <summary>Tests only: the window's region as it stands, the rectangles' union; null for none set.</summary>
    internal IReadOnlyList<RECT>? RegionOf(HWND window) => regions.TryGetValue(window, out var rects) ? rects : null;

    /// <summary>The keys to <paramref name="window"/>: once it is shown, where its showing waits; at once otherwise.</summary>
    public void Focus(HWND window)
    {
        if (InFrame && wants.TryGetValue(window, out var want) && want.Shown == true)
        {
            want.Focus = true;
            return;
        }
        Run(Plan(focused: [window]));
    }

    /// <summary>
    /// <paramref name="action"/> once the screen shows what was asked before it: hidden windows gone, the
    /// shown ones there (a snip of the screen, a shortcut sending keys to what is under).
    /// </summary>
    public void AfterPresent(Action action)
    {
        if (!InFrame)
        {
            Run(Plan(presented: [action]));
            return;
        }
        presented.Add((action, open));
        touched = true;
    }

    private Want Ask(HWND window)
    {
        if (!wants.TryGetValue(window, out var want))
        {
            wants[window] = want = new Want();
        }
        want.Stamp = open;
        touched = true;
        return want;
    }

    /// <summary>Outside a frame, what was asked of <paramref name="window"/> is applied at once.</summary>
    private void Done(HWND window)
    {
        if (!InFrame)
        {
            touched = false;
            Apply(window);
        }
    }

    // --- Questions -----------------------------------------------------------------------------

    /// <summary>Whether <paramref name="window"/> is shown, or will be once what was asked lands.</summary>
    public bool Visible(HWND window) =>
        wants.TryGetValue(window, out var want) && (want.Destroy || want.Shown is not null)
            ? !want.Destroy && want.Shown == true
            : system.Visible(window);

    /// <summary>What <paramref name="window"/> is for the plan: a popup where it was never enlisted.</summary>
    public Stratum StratumOf(HWND window) => layers.TryGetValue(window, out var layer) ? layer.Stratum : Stratum.Popup;

    /// <summary>
    /// What <paramref name="window"/> stands just under in the plan as the frame will leave it: the window
    /// over it there, or <see cref="HWND.HWND_TOPMOST"/> where it is on top (or not in sight).
    /// </summary>
    public HWND PlannedAfter(HWND window)
    {
        var showing = wants.Where(entry => entry.Value.Shown == true && !entry.Value.Destroy).Select(entry => entry.Key).ToHashSet();
        var hiding = wants.Where(entry => entry.Value.Shown == false || entry.Value.Destroy).Select(entry => entry.Key).ToHashSet();
        return ZPlan.After(Order(early: false, showing, hiding), window);
    }

    /// <summary>Tests only: the order last applied, top first.</summary>
    internal IReadOnlyList<HWND> Applied => applied;

    // --- The plan ------------------------------------------------------------------------------

    /// <summary>
    /// The windows in sight once <paramref name="showing"/> come and <paramref name="hiding"/> go, top first.
    /// <paramref name="early"/>: before the commit, a window rising to a higher stratum than it stood at keeps
    /// its place until its content is there; one sinking goes at once.
    /// </summary>
    private List<HWND> Order(bool early, IReadOnlySet<HWND> showing, IReadOnlySet<HWND> hiding)
    {
        var entries = new List<Layered>();
        foreach (var window in known)
        {
            if (hiding.Contains(window) || (!showing.Contains(window) && !system.Visible(window)))
            {
                continue;
            }
            entries.Add(new Layered(window, Placed(window, early), layers.TryGetValue(window, out var layer) ? layer.Rank : 0, fronts.GetValueOrDefault(window)));
        }
        return ZPlan.Of(entries);
    }

    /// <summary>The stratum <paramref name="window"/> is placed at: its own, or where it stood while it rises (<paramref name="early"/>).</summary>
    private Stratum Placed(HWND window, bool early)
    {
        var now = StratumOf(window);
        return early && standing.TryGetValue(window, out var was) && was < now ? was : now;
    }

    /// <summary><paramref name="order"/> is the order on the screen now.</summary>
    private void Stood(List<HWND> order, bool early)
    {
        applied = order;
        foreach (var window in order)
        {
            standing[window] = Placed(window, early);
        }
    }

    /// <summary>
    /// The frame's early part, as it ends, before its commit: the clear windows it shows come into sight —
    /// nothing of them shows until the commit brings their content — and the windows sinking to a lower
    /// stratum go there, the plan applied as it stands but for those rising.
    /// </summary>
    private void Early()
    {
        var early = wants
            .Where(entry => entry.Value.Shown == true && !entry.Value.Destroy && layers.TryGetValue(entry.Key, out var layer) && layer.Clear && !system.Visible(entry.Key))
            .Select(entry => entry.Key)
            .ToHashSet();
        var order = Order(early: true, early, new HashSet<HWND>());
        if (early.Count == 0 && ZPlan.Same(order, applied))
        {
            return;
        }
        Stood(order, early: true);
        Run(new WindowPlan([.. order.Select(window => new Stacked(window, early.Contains(window)))], [], [], [], [], []) { Early = true });
    }

    // --- Applying ------------------------------------------------------------------------------

    /// <summary>
    /// Applies what is due: of <paramref name="only"/> alone (asked outside a frame), else what the
    /// frames up to the last landed asked, a destroy once its fade is over too; and the plan, where it
    /// changed or was asked for again.
    /// </summary>
    private void Apply(HWND? only, bool flush = false)
    {
        var now = system.Now;
        long? wake = null;
        bool Due(uint stamp) => flush || stamp <= landed;
        var due = new List<(HWND Window, Want Want)>();
        foreach (var (window, want) in wants)
        {
            if (only is { } one ? window != one : !Due(want.Stamp))
            {
                continue;
            }
            if ((want.Destroy || want.Shown == false) && !flush && only is null && want.NotBefore > now)
            {
                wake = Math.Min(wake ?? long.MaxValue, want.NotBefore);
                continue;
            }
            due.Add((window, want));
        }
        foreach (var (window, _) in due)
        {
            wants.Remove(window);
        }
        var again = restack is { } asked && (only is not null || Due(asked));
        if (again)
        {
            restack = null;
        }
        var ready = new List<Action>();
        if (only is null)
        {
            // What waits for the screen runs once nothing asked before it is left: a fade too.
            foreach (var entry in presented.ToList())
            {
                if (Due(entry.Stamp) && !wants.Values.Any(want => want.Stamp <= entry.Stamp))
                {
                    ready.Add(entry.Action);
                    presented.Remove(entry);
                }
            }
        }
        var plan = PlanOf(due, ready, again);
        if (!plan.IsEmpty)
        {
            Run(plan);
        }
        if (wake is { } at)
        {
            system.Wake(at);
        }
    }

    private WindowPlan PlanOf(List<(HWND Window, Want Want)> due, List<Action> ready, bool again)
    {
        var showing = due.Where(entry => entry.Want.Shown == true && !entry.Want.Destroy && !system.Visible(entry.Window)).Select(entry => entry.Window).ToHashSet();
        var hidden = due.Where(entry => entry.Want.Shown == false && !entry.Want.Destroy && system.Visible(entry.Window)).Select(entry => entry.Window).ToList();
        var destroyed = due.Where(entry => entry.Want.Destroy).Select(entry =>
        {
            var closed = entry.Want.Closed;
            foreach (var action in entry.Want.Then ?? [])
            {
                closed = closed is null ? action : action + closed;
            }
            return (entry.Window, closed);
        }).ToList();
        // The plan of what is in sight once this lands — a later frame's asks still waiting stand as they are now.
        var going = hidden.Concat(destroyed.Select(entry => entry.Window)).ToHashSet();
        var order = Order(early: false, showing, going);
        List<Stacked> stack = [];
        if (again || showing.Count > 0 || !ZPlan.Same(order, applied))
        {
            stack = [.. order.Select(window => new Stacked(window, showing.Contains(window)))];
            Stood(order, early: false);
        }
        var then = due.Where(entry => !entry.Want.Destroy && entry.Want.Then is not null).SelectMany(entry => entry.Want.Then!).ToList();
        var focused = due.Where(entry => entry.Want.Focus && entry.Want.Shown == true && !entry.Want.Destroy).Select(entry => entry.Window).ToList();
        var shrunk = due.Where(entry => !entry.Want.Destroy && entry.Want.Region is not null).Select(entry => (entry.Window, entry.Want.Region!)).ToList();
        foreach (var (window, region) in shrunk)
        {
            regions[window] = [.. region];
        }
        foreach (var (window, _) in destroyed)
        {
            regions.Remove(window);
            known.Remove(window);
            layers.Remove(window);
            fronts.Remove(window);
            standing.Remove(window);
        }
        return new WindowPlan(stack, hidden, then, destroyed, focused, ready) { Regions = shrunk };
    }

    private static WindowPlan Plan(List<HWND>? focused = null, List<Action>? presented = null) =>
        new([], [], [], [], focused ?? [], presented ?? []);

    private void Run(WindowPlan plan)
    {
        if (Around is { } around)
        {
            around(() => system.Run(plan));
        }
        else
        {
            system.Run(plan);
        }
    }
}
