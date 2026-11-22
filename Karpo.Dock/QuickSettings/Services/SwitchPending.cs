// A switch on its way (docs\widgets.md §182). The person: "during the enableling of a hotspot for
// example the toggle icon is still displayed dark, but there should be a loading or enableling
// disabling state". A click asks Windows, and Windows takes its time: the hotspot's tethering
// manager seconds, a radio or a Settings handler a moment. Until it says the switch is where it was
// sent, the switch is pending: its buttons show where it is going, quietened, with "Turning on…" or
// "Turning off…" where a line has words and a turning ring on the toggles row's button, as Windows'
// own tile takes its new look at once and its page greys the switch until Windows has done.
//
// One pending per switch, whichever button was clicked (Pendings.Of): a row's button and a line of
// the same switch go busy together. Pure and on the dock's thread: a click begins it, every look at
// it settles it against what the switch says now. It ends when the switch reads the state it was
// sent to (confirmed), when the switch counts one more refusal (IQuickSwitch.Refusals: Windows said
// no, and the real state shows again, its note saying why), or after TimeoutMs, when the real state
// is simply read again. A click while it is pending is ignored: the switch is already on its way.
// Nothing shows for the first ShowAfterMs, so a switch that answers at once never flickers.

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What a look at a pending switch found.</summary>
internal enum PendingStep
{
    /// <summary>Nothing was pending.</summary>
    None,

    /// <summary>Still on its way.</summary>
    Waiting,

    /// <summary>The switch reads the state it was sent to.</summary>
    Confirmed,

    /// <summary>Windows refused it: the switch counted a refusal.</summary>
    Refused,

    /// <summary>Nothing said for <see cref="SwitchPending.TimeoutMs"/>: the real state is read again.</summary>
    TimedOut,
}

/// <summary>A switch's click on its way to Windows, and what to show meanwhile.</summary>
internal sealed class SwitchPending
{
    /// <summary>How long a switch may take before it looks busy: one that answers sooner never flickers.</summary>
    public const long ShowAfterMs = 200;

    /// <summary>How long it may stay pending before the real state is shown again.</summary>
    public const long TimeoutMs = 30_000;

    /// <summary>The clock the widgets read it at; a test's own, per thread.</summary>
    [ThreadStatic]
    private static Func<long>? clock;

    internal static Func<long>? Clock
    {
        get => clock;
        set => clock = value;
    }

    /// <summary>Now, in ms: the test's clock where it set one, else the tick count.</summary>
    public static long Now => clock?.Invoke() ?? Environment.TickCount64;

    private readonly List<Action> wakes = [];
    private bool? target;
    private long since;
    private int refusals;

    /// <summary>Where it was sent; null while nothing is pending.</summary>
    public bool? Target => target;

    /// <summary>Whether a click is on its way.</summary>
    public bool Busy => target is not null;

    /// <summary>Whether it looks busy at <paramref name="now"/>: pending for <see cref="ShowAfterMs"/> at least.</summary>
    public bool Shows(long now) => target is not null && now - since >= ShowAfterMs;

    /// <summary>The state to draw: where it is going while it looks busy, else <paramref name="on"/>, what the switch says.</summary>
    public bool? Shown(bool? on, long now) => Shows(now) ? target : on;

    /// <summary>
    /// A click on a switch that reads <paramref name="on"/>, having refused <paramref name="refused"/>
    /// times: false while one is already on its way, and the click is ignored. A switch whose state
    /// is not known has nowhere to be sent and nothing pends — the click does what it does.
    /// </summary>
    public bool Begin(bool? on, int refused, long now)
    {
        if (target is not null)
        {
            return false;
        }
        if (on is { } from)
        {
            target = !from;
            since = now;
            refusals = refused;
        }
        return true;
    }

    /// <summary>
    /// Settles it against the switch as it reads now: confirmed where it is where it was sent, refused
    /// where it counted a refusal since, timed out after <see cref="TimeoutMs"/>, else still waiting.
    /// Anything but waiting ends it.
    /// </summary>
    public PendingStep Settle(bool? on, int refused, long now)
    {
        if (target is not { } wanted)
        {
            return PendingStep.None;
        }
        var step = on == wanted ? PendingStep.Confirmed
            : refused != refusals ? PendingStep.Refused
            : now - since >= TimeoutMs ? PendingStep.TimedOut
            : PendingStep.Waiting;
        if (step != PendingStep.Waiting)
        {
            target = null;
        }
        return step;
    }

    /// <summary>When it next has something to change by itself: its busy look's start, then its timeout; null while nothing pends.</summary>
    public long? Due(long now) => target is null ? null : now - since < ShowAfterMs ? since + ShowAfterMs : since + TimeoutMs;

    /// <summary>A widget shows the switch: it is woken, on the dock's thread's terms, when a click begins.</summary>
    public void Listen(Action wake)
    {
        lock (wakes)
        {
            wakes.Add(wake);
        }
    }

    public void Unlisten(Action wake)
    {
        lock (wakes)
        {
            wakes.Remove(wake);
        }
    }

    /// <summary>Every widget of the switch: a click began, so each looks again and arms its own clock.</summary>
    public void Wake()
    {
        Action[] all;
        lock (wakes)
        {
            all = [.. wakes];
        }
        foreach (var wake in all)
        {
            wake();
        }
    }
}

/// <summary>Each switch's one pending, however many buttons it has.</summary>
internal static class Pendings
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IQuickSwitch, SwitchPending> table = new();

    public static SwitchPending Of(IQuickSwitch which) => table.GetValue(which, static _ => new SwitchPending());

    /// <summary>
    /// A click on <paramref name="which"/> as every button takes it: ignored while one is on its way;
    /// otherwise it pends (where its state is known and its click switches it), acts, and is settled
    /// at once, so a switch that answers as it is asked is never pending. True where it acted.
    /// </summary>
    public static bool Act(IQuickSwitch which, Action act)
    {
        var pending = Of(which);
        var now = SwitchPending.Now;
        if (!pending.Begin(which.Switches ? which.On : null, which.Refusals, now))
        {
            return false;
        }
        act();
        if (pending.Settle(which.On, which.Refusals, now) == PendingStep.Waiting)
        {
            pending.Wake();
        }
        return true;
    }
}

/// <summary>
/// A widget's clock for its pending switches: wakes it when one starts to look busy, when one times
/// out, and, while the toggles row's ring turns, every <see cref="QuickSettings.Controls.Spinner.TickMs"/>.
/// The dock's thread steps the ring as it takes the wake (CellIsland.Sample).
/// </summary>
internal sealed class PendingClock(CellIsland owner) : IDisposable
{
    private Timer? timer;

    /// <summary>The ring's phase.</summary>
    public float Spin { get; private set; }

    /// <summary>The ring's next step.</summary>
    public void Step() => Spin = QuickSettings.Controls.Spinner.Next(Spin);

    /// <summary>A wake at <paramref name="due"/>; none where it is null.</summary>
    public void Arm(long? due, long now)
    {
        if (due is not { } at)
        {
            timer?.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }
        timer ??= new Timer(static state => ((PendingClock)state!).Fire(), this, Timeout.Infinite, Timeout.Infinite);
        timer.Change((int)Math.Clamp(at - now, 1, 60_000), Timeout.Infinite);
    }

    private void Fire()
    {
        try
        {
            _ = owner.Wake();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A thread of the pool lets nothing out (NativeBoundaryTests).
            Console.Error.WriteLine($"Karpo: a pending switch's clock failed: {error.Message}");
        }
    }

    public void Dispose()
    {
        timer?.Dispose();
        timer = null;
    }
}
