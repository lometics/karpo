// A bar's hold on its strip of screen: what it asked the shell for, and whether the shell keeps it.
//
// The user: "right now the taskbar overlaps the windows for example. this should basically never
// happen. even not when timing is off or the app is restarted etc." The shell's answers are not to be
// trusted — ABM_SETPOS always answers TRUE, ABM_NEW is refused now and then, and after a dark screen
// Explorer took every message for minutes and acted on none (§212) — so what counts is the work area
// itself (GetMonitorInfo's rcWork): a bar holds its strip when the work area leaves it out.
//
// This file is the one bar's part: registering (ABM_REMOVE, ABM_NEW), asking for the strip (ABM_QUERYPOS,
// ABM_SETPOS, sending only what changed), telling where the window went (ABM_WINDOWPOSCHANGED), giving it
// all back (ABM_REMOVE), looking at the work area (Verify) and repairing what is missing (Repair). When
// to look and when to repair is the keeper's (BarKeeper.cs): on the events that can change a work area,
// never on a clock of its own. A repair is a ladder — the position again (and the registration where the
// shell lost or refused it), then, where that did not take, the work area set directly (SPI_SETWORKAREA:
// the work area as it is less the strip, what the shell would have set) — and a budget: at most
// RepairsPerWindow in RepairWindowMs per bar, so a shell that puts the whole screen back each time is
// never fought in a loop.
//
// A bar that hides holds nothing by design; it only has to stay registered. A display the bar is off on
// has no bar and holds nothing.

using Karpo.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What a look at the work area found (<see cref="AppbarHolder.Verify"/>).</summary>
public enum AppbarState
{
    /// <summary>Nothing asked of it yet, or a strip of nothing on a registered bar: nothing to hold.</summary>
    Idle,

    /// <summary>The work area leaves the strip out.</summary>
    Held,

    /// <summary>Registered, and the work area reaches into the strip.</summary>
    Missing,

    /// <summary>
    /// The strip was moved out towards the edge (from beside Explorer's taskbar as Karpo took over, or
    /// thinner), and the work area still ends where it was: the shell dropped the move.
    /// </summary>
    Short,

    /// <summary>The shell would not take the registration, or it is gone.</summary>
    Unregistered,

    /// <summary>The work area can't be read: a display that has just gone. It says nothing either way.</summary>
    Unknown,
}

/// <summary>What a repair does (<see cref="AppbarGuard.Repair"/>).</summary>
public enum AppbarRepair
{
    /// <summary>The position asked for again, sent whether or not it looks changed; registered first where the shell does not know the bar.</summary>
    Position,

    /// <summary>Registered again and the position asked for, then the work area set directly where it still reaches into the strip.</summary>
    Direct,
}

/// <summary>The rules a bar holds its strip by. Pure, so they can be tested without a shell.</summary>
internal static class AppbarGuard
{
    /// <summary>How many repairs a bar makes within <see cref="RepairWindowMs"/>; past that it waits for the window to pass, or for something that can change the work area to happen.</summary>
    public const int RepairsPerWindow = 4;

    /// <summary>The window the repairs are counted in.</summary>
    public const long RepairWindowMs = 60_000;

    /// <summary>
    /// Whether <paramref name="work"/>, a display's work area, leaves out <paramref name="strip"/>
    /// on <paramref name="edge"/>: the bar holds its space. A work area that stops further in than
    /// the strip (another appbar beside it) counts; one that reaches into the strip does not.
    /// </summary>
    public static bool Excludes(RECT work, RECT strip, Edge edge) => edge switch
    {
        Edge.Top => work.top >= strip.bottom,
        Edge.Left => work.left >= strip.right,
        Edge.Right => work.right <= strip.left,
        _ => work.bottom <= strip.top,
    };

    /// <summary>
    /// Whether <paramref name="work"/> ends exactly at <paramref name="strip"/> on <paramref name="edge"/>:
    /// that strip is what the shell holds there, and nothing further in. What a strip moved out towards
    /// the edge is held by: a work area further in is the old strip still held, and the move dropped.
    /// </summary>
    public static bool EndsAt(RECT work, RECT strip, Edge edge) => edge switch
    {
        Edge.Top => work.top == strip.bottom,
        Edge.Left => work.left == strip.right,
        Edge.Right => work.right == strip.left,
        _ => work.bottom == strip.top,
    };

    /// <summary>Whether <paramref name="strip"/>'s inner side is further out towards <paramref name="edge"/> than <paramref name="before"/>'s.</summary>
    public static bool FurtherOut(RECT strip, RECT before, Edge edge) => edge switch
    {
        Edge.Top => strip.bottom < before.bottom,
        Edge.Left => strip.right < before.right,
        Edge.Right => strip.left > before.left,
        _ => strip.top > before.top,
    };

    /// <summary>
    /// What the <paramref name="attempt"/>-th repair in a row of a strip found <paramref name="state"/>
    /// does (1 the first): the position again first, the cheap repair Explorer usually takes; after
    /// that the work area set directly as well, since a shell that took a position and kept the windows
    /// on the strip all the same (a dark screen, §212) will not be helped by being asked again. A strip
    /// the shell left short is only ever asked for again: setting a work area larger than the shell's
    /// could run windows under someone else's appbar.
    /// </summary>
    public static AppbarRepair Repair(int attempt, AppbarState state = AppbarState.Missing) =>
        attempt <= 1 || state == AppbarState.Short ? AppbarRepair.Position : AppbarRepair.Direct;

    /// <summary>
    /// <paramref name="work"/> less <paramref name="strip"/> on <paramref name="edge"/>: the work area
    /// the shell would keep with the strip held. Only ever smaller; a work area already clear of the
    /// strip comes back as it is.
    /// </summary>
    public static RECT Carved(RECT work, RECT strip, Edge edge)
    {
        switch (edge)
        {
            case Edge.Top:
                work.top = Math.Max(work.top, strip.bottom);
                break;
            case Edge.Left:
                work.left = Math.Max(work.left, strip.right);
                break;
            case Edge.Right:
                work.right = Math.Min(work.right, strip.left);
                break;
            default:
                work.bottom = Math.Min(work.bottom, strip.top);
                break;
        }
        return work;
    }

    /// <summary>
    /// The strip to take after the shell moved <paramref name="asked"/> to <paramref name="answer"/>
    /// (ABM_QUERYPOS moves it off any appbar already on that side): the answer, unless it is
    /// pushed more than a quarter of the display in, which no neighbouring taskbar explains and
    /// which is how two bars once stacked each other up the screen (2160 px to 850). Then the
    /// strip stays flush with the edge.
    /// </summary>
    public static RECT Stacked(RECT asked, RECT answer, RECT bounds, Edge edge)
    {
        var span = edge is Edge.Left or Edge.Right ? bounds.right - bounds.left : bounds.bottom - bounds.top;
        return Inward(asked, answer, edge) > span / 4 ? asked : answer;
    }

    /// <summary>
    /// How far the shell moved <paramref name="asked"/> in from <paramref name="edge"/> to
    /// <paramref name="answer"/>: the room it keeps there for the bars it counts on that side.
    /// </summary>
    public static int Inward(RECT asked, RECT answer, Edge edge) => edge switch
    {
        Edge.Top => answer.top - asked.top,
        Edge.Left => answer.left - asked.left,
        Edge.Right => asked.right - answer.right,
        _ => asked.bottom - answer.bottom,
    };
}

/// <summary>
/// One bar's registration with the shell and the strip it holds. The keeper (<see cref="BarKeeper"/>)
/// pushes what it should hold (<see cref="Hold"/>), looks at whether it does (<see cref="Verify"/>) and
/// repairs it (<see cref="Repair"/>) as the events that can change a work area come.
/// </summary>
internal sealed class AppbarHolder
{
    private readonly IAppbarShell shell;
    private readonly HWND bar;
    private readonly uint callback;
    private readonly AppbarTrace trace;
    private readonly Func<long> clock;

    // What the bar last asked to hold (Hold): what a look compares the work area with, and what a
    // repair asks for again.
    private (Edge Edge, int Thickness, RECT Bounds, nint Monitor)? want;

    private RECT? placed;

    // The repairs counted against the budget: how many since when (AppbarGuard.RepairsPerWindow);
    // null for no window open.
    private int repairs;
    private long? windowFrom;

    // When something that can move the work area was last sent for this bar: until the settle after
    // it, a strip not yet showing is waited for, not repaired (Waiting).
    private long? askedAt;

    // The repairs since the strip was last seen held, for the ladder and the trace.
    private int inRow;
    private bool spent;

    // The last position moved the strip out towards the edge: it is held once the work area ends
    // exactly at it (AppbarState.Short).
    private bool outward;

    /// <param name="bar">The bar's window.</param>
    /// <param name="callback">The message the shell's notices arrive as.</param>
    /// <param name="label">What the trace calls the bar: its display.</param>
    /// <param name="clock">Milliseconds, ever increasing: <see cref="Environment.TickCount64"/> unless a test says otherwise.</param>
    public AppbarHolder(IAppbarShell shell, HWND bar, uint callback, AppbarTrace trace, string label, Func<long>? clock = null)
    {
        this.shell = shell;
        this.bar = bar;
        this.callback = callback;
        this.trace = trace;
        this.clock = clock ?? (() => Environment.TickCount64);
        Label = label;
    }

    /// <summary>What the trace calls this bar; the dock renames it when its display changes.</summary>
    public string Label { get; set; }

    /// <summary>The shell accepted the last registration (ABM_NEW).</summary>
    public bool Registered { get; private set; }

    /// <summary>
    /// The strip the shell was last given (ABM_SETPOS): null until it has been given one, empty while
    /// the bar hides. What the shell was <i>told</i> — whether it holds it is what <see cref="Verify"/>
    /// finds out.
    /// </summary>
    public RECT? Reserved { get; private set; }

    /// <summary>The side of <see cref="Reserved"/>; meaningless while it is null.</summary>
    public Edge ReservedEdge { get; private set; }

    /// <summary>The shell was given a strip with some thickness (see <see cref="Reserved"/>).</summary>
    public bool HoldsSpace => Reserved is RECT strip && !AppbarSpace.IsEmpty(strip);

    /// <summary>Repairs since the strip was last seen held: 0 while all is well.</summary>
    public int Failures => inRow;

    /// <summary>
    /// ABM_REMOVE and ABM_NEW: the window is new, Explorer restarted, the strip moves to another
    /// side, or a repair. True when the shell took it.
    /// </summary>
    public bool Register()
    {
        Registered = shell.Register(bar, callback);
        askedAt = clock();
        Reserved = null;
        placed = null;
        outward = false;
        trace.Write($"[{Label}] register -> {(Registered ? "ok" : "REFUSED")}{Aside(shell.LastCall)}");
        return Registered;
    }

    /// <summary>
    /// Explorer restarted while this bar's display is a full screen app's: the new Explorer knows no
    /// appbar, and nothing may be sent for that display now. Taken as unregistered, so the reconcile
    /// that follows the app registers it again (a repair).
    /// </summary>
    public void Lost()
    {
        Registered = false;
        Reserved = null;
        placed = null;
        outward = false;
        trace.Write($"[{Label}] Explorer knows this bar no more; registered again once its full screen app is gone");
    }

    /// <summary>
    /// Holds a strip <paramref name="thickness"/> px thick on <paramref name="edge"/> of the display
    /// <paramref name="bounds"/> (<paramref name="monitor"/>): 0 to hold nothing, as a hiding bar
    /// does. Sends only what changed (<see cref="AppbarSpace.Plan"/>); whether it took is
    /// <see cref="Verify"/>'s to find out. True when anything was sent that can move the work area.
    /// A bar the shell does not know only keeps what it is to hold: registering it again is a repair,
    /// counted against the budget (<see cref="Repair"/>), so a shell that keeps refusing is not asked
    /// at every build.
    /// </summary>
    public bool Hold(Edge edge, int thickness, RECT bounds, nint monitor)
    {
        want = (edge, thickness, bounds, monitor);
        if (!Registered)
        {
            return false;
        }
        var sent = false;
        foreach (var step in AppbarSpace.Plan(Registered, Reserved is not null, ReservedEdge, edge, thickness))
        {
            if (step == AppbarStep.Register)
            {
                sent = true;
                if (!Register())
                {
                    // Nothing to set a position on: the next look finds it unregistered.
                    return sent;
                }
            }
            else
            {
                sent |= Position(edge, thickness, bounds, monitor);
            }
        }
        return sent;
    }

    /// <summary>ABM_WINDOWPOSCHANGED, once per place the window moves to.</summary>
    public void Moved(RECT place)
    {
        if (!Registered || (placed is RECT before && Same(before, place)))
        {
            return;
        }
        placed = place;
        shell.Moved(bar);
    }

    /// <summary>ABM_REMOVE for good: the bar is going away, or Karpo is quitting.</summary>
    public void Release()
    {
        if (Registered)
        {
            shell.Remove(bar);
            trace.Write($"[{Label}] removed");
        }
        Registered = false;
        Reserved = null;
        placed = null;
        want = null;
        inRow = 0;
    }

    /// <summary>
    /// Whether the shell keeps the strip: compares the work area with what was asked for, and asks the
    /// shell nothing. <c>Why</c> says what is wrong, for the trace.
    /// </summary>
    public (AppbarState State, string Why) Verify()
    {
        if (want is not { } asked)
        {
            return (AppbarState.Idle, "");
        }
        if (!Registered)
        {
            return (AppbarState.Unregistered, "not registered");
        }
        var holds = asked.Thickness > 0;
        if (holds && (Reserved is not RECT given || AppbarSpace.IsEmpty(given)))
        {
            return (AppbarState.Missing, "no strip given");
        }
        if (Reserved is not RECT strip || (!holds && !outward))
        {
            return (AppbarState.Idle, "");
        }
        if (shell.WorkArea(asked.Monitor) is not RECT work)
        {
            return (AppbarState.Unknown, "");
        }
        if (holds && !AppbarGuard.Excludes(work, strip, ReservedEdge))
        {
            return (AppbarState.Missing, $"work {Show(work)} reaches into {ReservedEdge} strip {Show(strip)}");
        }
        if (outward)
        {
            if (!AppbarGuard.EndsAt(work, strip, ReservedEdge))
            {
                return (AppbarState.Short, $"work {Show(work)} ends short of {ReservedEdge} strip {Show(strip)}, moved out towards the edge");
            }
            outward = false;
        }
        return (holds ? AppbarState.Held : AppbarState.Idle, "");
    }

    /// <summary>
    /// A look found the strip held: the repairs in a row are over. True when there were any, so the
    /// keeper can say it held again.
    /// </summary>
    public bool Settled()
    {
        var had = inRow > 0;
        if (had)
        {
            trace.Write($"[{Label}] held again after {inRow} repair{(inRow == 1 ? "" : "s")}");
        }
        inRow = 0;
        spent = false;
        return had;
    }

    /// <summary>Something that can change a work area without a word to the bars happened: the budget is whole again.</summary>
    public void Refill()
    {
        repairs = 0;
        windowFrom = null;
        spent = false;
    }

    /// <summary>Whether a repair may go out at <paramref name="now"/> (ms): fewer than the budget in the window.</summary>
    public bool MayRepair(long now)
    {
        Window(now);
        return repairs < AppbarGuard.RepairsPerWindow;
    }

    /// <summary>The budget's window: a new one where none is open or the last has passed.</summary>
    private void Window(long now)
    {
        if (windowFrom is not long from || now - from >= AppbarGuard.RepairWindowMs)
        {
            repairs = 0;
            windowFrom = now;
        }
    }

    /// <summary>
    /// Whether something was sent for this bar less than <paramref name="settle"/> ms before
    /// <paramref name="now"/>: a strip not showing yet is waited for until the settle — several notices
    /// of one change (a work area, the appbars moved) must not spend the budget in a moment.
    /// </summary>
    public bool Waiting(long now, long settle) => askedAt is long at && now - at < settle;

    /// <summary>
    /// Says, once until the budget is whole again, that a strip still missing is left as the shell has
    /// it: no repair is left in the window.
    /// </summary>
    public void Spent(string why)
    {
        if (!spent)
        {
            trace.Write($"[{Label}] NOT held: {why}; {AppbarGuard.RepairsPerWindow} repairs in {AppbarGuard.RepairWindowMs / 1000} s did not take, left as the shell has it until something changes");
            spent = true;
        }
    }

    /// <summary>
    /// Repairs what a look found (<paramref name="state"/>: a strip missing or left short, a
    /// registration the shell refused) by the ladder (<see cref="AppbarGuard.Repair"/>), and counts it
    /// against the budget.
    /// </summary>
    public void Repair(long now, string why, AppbarState state = AppbarState.Missing)
    {
        if (want is not { } asked)
        {
            return;
        }
        Window(now);
        repairs++;
        inRow++;
        askedAt = now;
        var how = AppbarGuard.Repair(inRow, state);
        var wasOutward = outward;
        // What Explorer was doing, on the first repair in a row: enough to tell after the fact what it
        // was waiting for, without asking it at every repair.
        var context = inRow == 1 ? Aside(shell.Describe()) : "";
        trace.Write($"[{Label}] NOT held: {why} (repair {inRow}) -> {how}{context}");
        if (how == AppbarRepair.Direct || !Registered)
        {
            if (!Register())
            {
                return;
            }
            // Registered afresh, holding nothing: what it is to hold is asked for as at any start.
            Hold(asked.Edge, asked.Thickness, asked.Bounds, asked.Monitor);
        }
        else
        {
            // Forgotten, so the position really goes out rather than being taken for unchanged — the
            // empty strip of a bar giving its space back too; a strip moved out is still held only
            // where the work area comes out to it.
            Reserved = null;
            Position(asked.Edge, asked.Thickness, asked.Bounds, asked.Monitor);
            outward |= wasOutward;
        }
        if (how == AppbarRepair.Direct)
        {
            SetWorkArea(asked);
        }
    }

    /// <summary>
    /// Gives the shell the strip on <paramref name="edge"/>: its whole thickness, or with
    /// <paramref name="thickness"/> 0 an empty strip, which is how space is given back without the
    /// registration being put at risk (<see cref="AppbarSpace.Plan"/>). Only a change is sent:
    /// each one makes the shell work every work area out again. True when one was sent.
    /// </summary>
    private bool Position(Edge edge, int thickness, RECT bounds, nint monitor)
    {
        var rc = AppbarSpace.Fit(bounds, edge, thickness);
        if (thickness > 0)
        {
            // The bar as it would sit on the bare edge; the shell moves it off any bar already
            // there (Explorer's taskbar while it still holds its own), and the thickness is applied
            // again from where it ends up — unless it was moved further than any taskbar beside it
            // explains.
            var answer = AppbarSpace.Fit(shell.Query(bar, edge, rc), edge, thickness);
            var taken = AppbarGuard.Stacked(rc, answer, bounds, edge);
            if (!Same(taken, answer))
            {
                trace.Write($"[{Label}] query pushed the strip to {Show(answer)}; kept it at {Show(rc)}");
            }
            rc = taken;
        }
        if (Reserved is RECT held && ReservedEdge == edge && Same(held, rc))
        {
            return false;
        }
        if (Reserved is RECT before && ReservedEdge == edge)
        {
            outward = AppbarGuard.FurtherOut(rc, before, edge);
        }
        shell.SetPos(bar, edge, rc);
        askedAt = clock();
        Reserved = rc;
        ReservedEdge = edge;
        trace.Write($"[{Label}] setpos {edge} {Show(rc)}{Aside(shell.LastCall)}");
        return true;
    }

    /// <summary>
    /// After a repair that did not take: where the work area still reaches into the strip, the shell
    /// took the bar and keeps the windows on it all the same (a dark screen, §212), so the work area is
    /// set to what the shell would have made it — as it is, less the strip. Only for a bar the shell
    /// registered: one it refused, or a desktop with no Explorer (a test's), sets nothing.
    /// </summary>
    private void SetWorkArea((Edge Edge, int Thickness, RECT Bounds, nint Monitor) asked)
    {
        if (!Registered || asked.Thickness <= 0)
        {
            return;
        }
        var (strip, edge) = Reserved is RECT given && !AppbarSpace.IsEmpty(given) && ReservedEdge == asked.Edge
            ? (given, ReservedEdge)
            : (AppbarSpace.Fit(asked.Bounds, asked.Edge, asked.Thickness), asked.Edge);
        if (shell.WorkArea(asked.Monitor) is not RECT work || AppbarGuard.Excludes(work, strip, edge))
        {
            // Gone, or the repair took at once.
            return;
        }
        var carved = AppbarGuard.Carved(work, strip, edge);
        if (AppbarSpace.IsEmpty(carved))
        {
            return;
        }
        var set = shell.SetWorkArea(carved);
        askedAt = clock();
        trace.Write($"[{Label}] work area {Show(work)} still on the strip: set to {Show(carved)} directly -> {(set ? "ok" : "REFUSED")}");
    }

    /// <summary>What the shell had to say, in brackets after a trace line; nothing when it said nothing.</summary>
    private static string Aside(string said) => said.Length > 0 ? $" ({said})" : "";

    private static bool Same(RECT a, RECT b) =>
        a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;

    internal static string Show(RECT rc) => $"{rc.left},{rc.top}..{rc.right},{rc.bottom}";
}
