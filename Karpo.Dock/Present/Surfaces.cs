// The one place windows are shown, hidden, raised, moved and destroyed (docs\widgets.md §354, §359):
// what is asked of Karpo's own goes into this thread's WindowTable and reaches the screen with the frame's
// composition commit (the flicker design's R3), each where its stratum puts it (ZPlan). A source rule holds
// every other file to it (RawWindowCallsTests): SetWindowPos, ShowWindow, DestroyWindow, DeferWindowPos and
// SetWindowRgn are called here alone — the few windows outside the plan (other apps', Explorer's taskbar,
// windows that never show) by calls named for what they are.
//
// The dock opens a frame round every message it handles (Dock.WndProc's Dispatch, Signaled, Drain):
// `using var frame = Surfaces.Begin(compositor, ending)`. As the frame ends — its edit chrome reconciled
// first (the ending) — the clear windows it shows come into sight and those sinking in the plan go there;
// then the compositor is asked for a commit (RequestCommitAsync); when it completes, the rest of the plan is
// applied on the dock's thread, in one batch (DeferWindowPos) for the stack. Should the commit never answer — a
// device lost, a compositor gone — the plan goes after FenceMs all the same, so nothing is left hidden.

using Karpo.Core;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class Surfaces
{
    [ThreadStatic]
    private static WindowTable? table;

    [ThreadStatic]
    private static Win32Surfaces? system;

    private static WindowTable Table
    {
        get
        {
            if (table is null)
            {
                system = new Win32Surfaces();
                table = new WindowTable(system);
                system.Table = table;
            }
            return table;
        }
    }

    /// <summary>A frame: what is asked of a window until it is disposed reaches the screen once its content has.</summary>
    public readonly struct Frame(WindowTable? table, Action? ending) : IDisposable
    {
        public void Dispose() => table?.End(ending);
    }

    /// <summary>
    /// Opens a frame on this thread, its commit asked of <paramref name="compositor"/>. Frames nest; the
    /// outermost one ends it, running <paramref name="ending"/> first, inside it (the dock's edit chrome
    /// reconciled once a frame, §359).
    /// </summary>
    public static Frame Begin(Compositor? compositor, Action? ending = null)
    {
        var frames = Table;
        system!.Use(compositor);
        frames.Begin();
        return new Frame(frames, ending);
    }

    /// <summary>A frame is open on this thread.</summary>
    public static bool InFrame => table?.InFrame == true;

    /// <summary>Nothing asked of a window waits on this thread.</summary>
    public static bool Settled => table?.Settled != false;

    /// <summary>What runs a plan on this thread (the dock: as busy).</summary>
    public static Action<Action>? Around
    {
        set => Table.Around = value;
    }

    /// <summary>What the open frame asked goes as soon as its content has: a modal loop is about to begin (<see cref="WindowTable.Present"/>).</summary>
    public static void Present() => table?.Present();

    /// <summary>Everything asked, applied now: the dock is closing.</summary>
    public static void Flush() => table?.Flush();

    /// <summary>Moved and sized, at once and where it stands in the z-order: shown, it moves now (a window never moves while it shows once rule R2 is in).</summary>
    public static bool Place(HWND window, int x, int y, int width, int height) =>
        SetWindowPos(window, HWND.NULL, x, y, width, height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);

    /// <summary>
    /// What <paramref name="window"/> is for the z-order plan (§359, <see cref="ZPlan"/>): its stratum, its rank
    /// in it, and whether its content is clear while it is out of sight — then it comes into sight as the frame
    /// ends, before its commit, and its content lands with every other window's (<see cref="WindowTable"/>).
    /// </summary>
    public static void Enlist(HWND window, Stratum stratum, int rank = 0, bool clear = false) => Table.Enlist(window, stratum, rank, clear);

    public static void Show(HWND window) => Table.Show(window);

    /// <summary>Hidden; <paramref name="then"/> once it is; §612 not before <paramref name="afterMs"/> from now: its fade out runs out.</summary>
    public static void Hide(HWND window, Action? then = null, int afterMs = 0) => Table.Hide(window, then, afterMs);

    /// <summary>To the front of its stratum.</summary>
    public static void Front(HWND window) => Table.Front(window);

    /// <summary>The plan applied again as the frame lands: Karpo's windows over another app's always-on-top one that came up since.</summary>
    public static void Restack() => Table.Restack();

    public static void Destroy(HWND window, Action? closed = null, int afterMs = 0) => Table.Destroy(window, closed, afterMs);

    public static void Focus(HWND window) => Table.Focus(window);

    /// <summary>The window's region (client px): grown at once, shrunk once the frame's content is there (<see cref="WindowTable.Region"/>).</summary>
    public static void Region(HWND window, RECT rect) => Table.Region(window, rect);

    /// <summary>The window's region, the rectangles' union: a bar's panels and its grip (§358), as <see cref="Region(HWND, RECT)"/>.</summary>
    public static void Region(HWND window, IReadOnlyList<RECT> rects) => Table.Region(window, rects);

    public static void AfterPresent(Action action) => Table.AfterPresent(action);

    public static bool Visible(HWND window) => Table.Visible(window);

    /// <summary>What <paramref name="window"/> stands just under in the plan; <see cref="HWND.HWND_TOPMOST"/> on top.</summary>
    public static HWND PlannedAfter(HWND window) => Table.PlannedAfter(window);

    public static Stratum StratumOf(HWND window) => Table.StratumOf(window);

    /// <summary>The plan's stack is being applied now: its own moves, which a window's WM_WINDOWPOSCHANGING leaves as they are.</summary>
    public static bool Stacking => system?.Stacking == true;

    /// <summary>
    /// §342, §359, §361: a window of the plan's that Windows is about to move in the z-order (<paramref name="pos"/>,
    /// its WM_WINDOWPOSCHANGING) — activated by a click, given the focus (a menu shown again, a folder handed the
    /// focus back by a menu closing), Windows picking it next, anything raising it — goes where the plan has it
    /// instead: just under the window planned over it (<see cref="PlannedAfter"/>). Fixed as the move is asked for,
    /// for every window of the plan, so nothing ever stands a window out of its stratum's place (the person,
    /// 2026-10-24: "the top edit bar sometimes clips behind the folder column lines"; 2026-10-26, a menu's focus
    /// raising it over its tooltip). Nothing for a move that keeps the order, the plan's own, the plan's top
    /// window, or a window the plan does not hold. Called by the windows' procedures before anything else, the
    /// dock busy or not.
    /// </summary>
    public static void Keep(HWND window, WINDOWPOS* pos)
    {
        if ((pos->flags & (uint)SWP.SWP_NOZORDER) == 0 && !Stacking && table is { } planned
            && planned.PlannedAfter(window) is var after && after != HWND.HWND_TOPMOST)
        {
            pos->hwndInsertAfter = after;
        }
    }

    // --- Windows outside the plan (§359) --------------------------------------------------------
    // The raw calls left, every one here, each at once: windows that are not Karpo's shown ones — other
    // apps' and Explorer's, Windows' task dialog — and Karpo's that never show (message windows, the
    // host) or stand where another app's window decides (the peek's strip).

    /// <summary>Another app's window (or Explorer's taskbar) restored, minimized, hidden or shown, as <paramref name="command"/> says, at once.</summary>
    public static void OtherApp(HWND window, int command) => _ = ShowWindow(window, command);

    /// <summary>
    /// Another app's window (Windows' notification, §540) moved to <paramref name="x"/>,<paramref name="y"/>, its
    /// size, z-order and activation as they are, never waiting on its app (SWP_ASYNCWINDOWPOS). Whether it was asked.
    /// </summary>
    public static bool OtherAppMove(HWND window, int x, int y) => SetWindowPos(window, HWND.NULL, x, y, 0, 0,
        SWP.SWP_NOSIZE | SWP.SWP_NOZORDER | SWP.SWP_NOOWNERZORDER | SWP.SWP_NOACTIVATE | SWP.SWP_ASYNCWINDOWPOS);

    /// <summary>
    /// Explorer's taskbar given an empty window region (<paramref name="blank"/>), or its own again (none). §542:
    /// Explorer shows its taskbars again whenever it lays them out, and DWM drew what it showed until the hook
    /// hid it; with nothing in its region it draws nothing and takes no press, however it shows. The region
    /// stays through Explorer's own moves and shows, and only another SetWindowRgn takes it off.
    /// </summary>
    public static void OtherAppBlank(HWND window, bool blank)
    {
        var region = blank ? CreateRectRgn(0, 0, 0, 0) : HRGN.NULL;
        // The window owns the region once this succeeds, and only then.
        if (SetWindowRgn(window, region, BOOL.TRUE) == 0 && region != HRGN.NULL)
        {
            _ = DeleteObject(region);
        }
    }

    /// <summary>Whether <paramref name="window"/>'s region is empty (<see cref="OtherAppBlank"/>): false for no region, a window's own.</summary>
    public static bool IsBlank(HWND window)
    {
        var probe = CreateRectRgn(0, 0, 0, 0);
        try
        {
            return GetWindowRgn(window, probe) == NULLREGION;
        }
        finally
        {
            _ = DeleteObject(probe);
        }
    }

    // app clicks: another app's window restored, minimized or maximized from its button, never waited on
    // (ShowWindowAsync): ShowWindow waits for the window's own thread, and a hung app held the bar's
    // thread with it. Windows queues it to that thread, so an activation asked right after comes after it.

    /// <summary>Another app's window restored from its button, as it was before (maximized, if it was), without waiting on its app.</summary>
    public static void RestoreOther(HWND window) => _ = ShowWindowAsync(window, SW.SW_RESTORE);

    /// <summary>Another app's window minimized without waiting on its app: where asking it (SC_MINIMIZE) would go unanswered (hung, disabled). Never an app above Karpo's: Windows refuses that (UIPI, §662).</summary>
    public static void MinimizeOther(HWND window) => _ = ShowWindowAsync(window, SW.SW_MINIMIZE);

    /// <summary>
    /// §619: another app's window shown, put on top and activated by its own thread, after what was queued to
    /// it before (a restore), never waited on (SWP_ASYNCWINDOWPOS): the modal dialog of a window brought back
    /// from its button, as it was before it went down. (ShowWindowAsync SW_SHOW does not activate a window the
    /// restore already showed again.)
    /// </summary>
    public static void ActivateOther(HWND window) => _ = SetWindowPos(window, HWND.HWND_TOP, 0, 0, 0, 0,
        SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_SHOWWINDOW | SWP.SWP_ASYNCWINDOWPOS);

    /// <summary>Another app's window maximized without waiting on its app: where asking it (SC_MAXIMIZE) would go unanswered or was refused.</summary>
    public static void MaximizeOther(HWND window) => _ = ShowWindowAsync(window, SW.SW_MAXIMIZE);

    /// <summary>
    /// A window not of the plan over every topmost one, at once: Windows' task dialog as it is made
    /// (<paramref name="activate"/>: the keys with it), the tray's window for the apps.
    /// </summary>
    public static void Topmost(HWND window, bool activate = false) =>
        _ = SetWindowPos(window, HWND.HWND_TOPMOST, 0, 0, 0, 0, SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | (activate ? 0u : SWP.SWP_NOACTIVATE));

    /// <summary>A window that never shows let go of at once: a message window, the dock's host.</summary>
    public static void Gone(HWND window) => _ = DestroyWindow(window);

    /// <summary>The peek's strip shown just under <paramref name="below"/>, another app's window it outlines, at once.</summary>
    public static void Beneath(HWND window, HWND below) =>
        _ = SetWindowPos(window, below, 0, 0, 0, 0, SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE | SWP.SWP_SHOWWINDOW);

    /// <summary>The peek's strip out of sight at once: it goes with the window it outlines, not with a frame.</summary>
    public static void Away(HWND window) => _ = ShowWindow(window, SW.SW_HIDE);

    /// <summary>Tests only: this thread's frames fence nothing, as before the fence (<see cref="WindowTable.Unfenced"/>).</summary>
    internal static bool Unfenced
    {
        get => table?.Unfenced == true;
        set => Table.Unfenced = value;
    }

    /// <summary>How this thread's fences went: asked, landed by their commit, gone by the time limit, and how the last was asked.</summary>
    internal static FenceCount Fences => system?.Count ?? default;

    /// <summary>How long a fence waits for its commit before the plan goes all the same.</summary>
    internal const int FenceMs = 250;

    /// <summary>
    /// Tests only: how long after its commit has answered a fence lands, ms — the time between the commit and
    /// the fence a loaded machine can take, held open so a film sees what the screen shows meanwhile (§359).
    /// </summary>
    internal static int FenceHoldMs
    {
        get => fenceHoldMs;
        set => fenceHoldMs = value;
    }

    [ThreadStatic]
    private static int fenceHoldMs;
}

/// <param name="Asked">Fences asked.</param>
/// <param name="Committed">Landed by their commit.</param>
/// <param name="TimedOut">Gone by <see cref="Surfaces.FenceMs"/>, no commit having answered.</param>
/// <param name="Way">How the last was asked: "commit" (RequestCommitAsync), "batch" (the commit batch), "none" (no compositor).</param>
/// <param name="LastMs">How long the last commit took to answer.</param>
internal readonly record struct FenceCount(int Asked, int Committed, int TimedOut, string Way, double LastMs);

/// <summary>Win32's windows and the compositor's commit, as <see cref="WindowTable"/> reaches them.</summary>
internal sealed unsafe class Win32Surfaces : IWindowSystem
{
    private Compositor? compositor;
    private Windows.System.DispatcherQueue? queue;
    private Windows.System.DispatcherQueueTimer? limit;
    private Windows.System.DispatcherQueueTimer? alarm;
    private uint asked;
    private uint committed;
    private long askedAt;

    public WindowTable? Table { get; set; }

    /// <summary>Whether the plan's stack is being applied now.</summary>
    public bool Stacking { get; private set; }

    public FenceCount Count { get; private set; } = new(0, 0, 0, "none", 0);

    public long Now => Environment.TickCount64;

    public void Use(Compositor? compositor)
    {
        if (compositor is not null)
        {
            this.compositor = compositor;
        }
        if (queue is null)
        {
            try
            {
                queue = Windows.System.DispatcherQueue.GetForCurrentThread();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // No WinRT on this thread: fences land at once.
            }
        }
    }

    public bool Visible(HWND window) => IsWindowVisible(window);

    public void Fence(uint generation)
    {
        asked = generation;
        askedAt = Environment.TickCount64;
        if (compositor is not { } c || queue is null)
        {
            Count = Count with { Asked = Count.Asked + 1, Way = "none" };
            Troubles.Guard("Surfaces: a frame's windows", () => Land(generation, byCommit: false));
            return;
        }
        var way = "commit";
        try
        {
            var commit = c.RequestCommitAsync();
            var hold = Surfaces.FenceHoldMs;
            commit.Completed = (_, _) => Post(generation, hold);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // The design's fallback: the batch the next commit closes.
            try
            {
                var batch = c.GetCommitBatch(CompositionBatchTypes.None);
                batch.Completed += (_, _) => Post(generation, 0);
                way = "batch";
            }
            catch (Exception again) when (again is not OutOfMemoryException)
            {
                way = "none";
                Troubles.Report("Surfaces: no commit to wait on", again);
            }
        }
        Count = Count with { Asked = Count.Asked + 1, Way = way };
        if (way == "none")
        {
            Troubles.Guard("Surfaces: a frame's windows", () => Land(generation, byCommit: false));
            return;
        }
        // Should the commit never answer, the plan goes all the same.
        if (limit is null)
        {
            limit = queue.CreateTimer();
            limit.IsRepeating = false;
            limit.Tick += (_, _) => Troubles.Guard("Surfaces: a fence's time limit", () =>
            {
                if (committed < asked)
                {
                    Count = Count with { TimedOut = Count.TimedOut + 1 };
                    Land(asked, byCommit: false);
                }
            });
        }
        limit.Stop();
        limit.Interval = TimeSpan.FromMilliseconds(Surfaces.FenceMs);
        limit.Start();
    }

    /// <summary>A commit answered, on whatever thread: the plan is applied on this one's queue, after what runs now.</summary>
    private void Post(uint generation, int hold)
    {
        if (hold > 0 && queue is not null && queue.TryEnqueue(() => Troubles.Guard("Surfaces: a held fence", () => Hold(generation, hold))))
        {
            return;
        }
        if (queue is null || !queue.TryEnqueue(() => Troubles.Guard("Surfaces: a frame's windows", () => Land(generation, byCommit: true))))
        {
            Troubles.Guard("Surfaces: a frame's windows", () => Land(generation, byCommit: true));
        }
    }

    /// <summary>Tests only: the held fences' timers, kept until they fire.</summary>
    private readonly List<Windows.System.DispatcherQueueTimer> held = [];

    /// <summary>Tests only (<see cref="Surfaces.FenceHoldMs"/>): the commit has answered — no time limit steps in — and the plan goes <paramref name="ms"/> later.</summary>
    private void Hold(uint generation, int ms)
    {
        if (generation > committed)
        {
            committed = generation;
            Count = Count with { Committed = Count.Committed + 1, LastMs = Environment.TickCount64 - askedAt };
        }
        var timer = queue!.CreateTimer();
        held.Add(timer);
        timer.IsRepeating = false;
        timer.Interval = TimeSpan.FromMilliseconds(ms);
        timer.Tick += (_, _) => Troubles.Guard("Surfaces: a frame's windows", () =>
        {
            held.Remove(timer);
            Table?.Landed(generation);
        });
        timer.Start();
    }

    private void Land(uint generation, bool byCommit)
    {
        if (byCommit && generation > committed)
        {
            committed = generation;
            Count = Count with { Committed = Count.Committed + 1, LastMs = Environment.TickCount64 - askedAt };
        }
        Table?.Landed(generation);
    }

    public void Wake(long at)
    {
        if (queue is null)
        {
            return;
        }
        if (alarm is null)
        {
            alarm = queue.CreateTimer();
            alarm.IsRepeating = false;
            alarm.Tick += (_, _) => Troubles.Guard("Surfaces: a fade's end", () => Table?.Woken());
        }
        alarm.Stop();
        alarm.Interval = TimeSpan.FromMilliseconds(Math.Max(1, at - Now));
        alarm.Start();
    }

    public void Region(HWND window, IReadOnlyList<RECT> rects)
    {
        if (!IsWindow(window) || rects.Count == 0)
        {
            return;
        }
        var region = CreateRectRgn(rects[0].left, rects[0].top, rects[0].right, rects[0].bottom);
        foreach (var rect in rects.Skip(1))
        {
            var more = CreateRectRgn(rect.left, rect.top, rect.right, rect.bottom);
            _ = CombineRgn(region, region, more, RGN_OR);
            _ = DeleteObject(more);
        }
        // The window owns the region once this succeeds, and only then.
        if (SetWindowRgn(window, region, BOOL.TRUE) == 0)
        {
            _ = DeleteObject(region);
        }
    }

    public void Run(WindowPlan plan)
    {
        // The regions that shrink, the content now in them.
        foreach (var (window, rects) in plan.Regions)
        {
            Region(window, rects);
        }
        if (plan.Stack.Count > 0)
        {
            Stacking = true;
            try
            {
                Stack([.. plan.Stack.Where(entry => IsWindow(entry.Window))]);
            }
            finally
            {
                Stacking = false;
            }
            // §361: a stage run says the plan, for a test to hold the screen's order to.
            if (Stage.On && Table is { } table)
            {
                Stage.Planned(table.Applied.Select(window => ((nint)window, table.StratumOf(window).ToString())));
            }
        }
        foreach (var window in plan.Hidden)
        {
            if (IsWindow(window))
            {
                _ = ShowWindow(window, SW.SW_HIDE);
            }
        }
        foreach (var then in plan.Then)
        {
            Troubles.Guard("Surfaces: after a hide", then);
        }
        foreach (var (window, closed) in plan.Destroyed)
        {
            // Out of sight first, so its content closing is never seen.
            if (IsWindow(window) && IsWindowVisible(window))
            {
                _ = ShowWindow(window, SW.SW_HIDE);
            }
            if (closed is not null)
            {
                Troubles.Guard("Surfaces: a window's content", closed);
            }
            if (IsWindow(window))
            {
                _ = DestroyWindow(window);
            }
        }
        foreach (var window in plan.Focused)
        {
            if (IsWindow(window) && IsWindowVisible(window))
            {
                _ = SetForegroundWindow(window);
                _ = SetFocus(window);
            }
        }
        if (plan.Presented.Count > 0)
        {
            // The frame being composed as the first returns may have begun with what went.
            _ = DwmFlush();
            _ = DwmFlush();
            foreach (var action in plan.Presented)
            {
                Troubles.Guard("Surfaces: after the screen", action);
            }
        }
    }

    /// <summary>
    /// The plan in one batch (§359): each window just under the one before it, the first over every
    /// topmost window, those coming into sight shown — one change to the screen, never a window shown
    /// under what is about to be lifted off it. One by one where Windows refuses the batch.
    /// </summary>
    private static void Stack(List<Stacked> stack)
    {
        if (stack.Count == 0)
        {
            return;
        }
        static uint Flags(Stacked entry) =>
            SWP.SWP_NOMOVE | SWP.SWP_NOSIZE | SWP.SWP_NOACTIVATE | (entry.Show ? SWP.SWP_SHOWWINDOW : 0u);
        var batch = BeginDeferWindowPos(stack.Count);
        var above = HWND.HWND_TOPMOST;
        foreach (var entry in stack)
        {
            if (batch == HDWP.NULL)
            {
                break;
            }
            batch = DeferWindowPos(batch, entry.Window, above, 0, 0, 0, 0, Flags(entry));
            above = entry.Window;
        }
        if (batch != HDWP.NULL && EndDeferWindowPos(batch))
        {
            return;
        }
        above = HWND.HWND_TOPMOST;
        foreach (var entry in stack)
        {
            _ = SetWindowPos(entry.Window, above, 0, 0, 0, 0, Flags(entry));
            above = entry.Window;
        }
    }
}
