// Keep awake (2026-10-20, docs\widgets.md §269), as PowerToys Awake keeps a PC awake: while it is
// on, Windows' idle timer neither puts the PC to sleep nor turns its screen off — until it is turned
// off, or for the half hour, the hour, two or four it was turned on for. The person: "the setting to
// keep awake the computer should also be added as widget to karpo".
//
// THE SCREEN TOO (§586). The person: "i used keep awake last time and it did not work i believe, the
// screen still went dark after a certain time. keep awake should also keep the screen awake". Until
// then the screen was kept on only where the widget's "Keep the screen on" said so, which was off
// unless chosen and which a toggles row's click and a length from the menu picked from off never
// read: the PC was kept from sleeping while Windows' display timeout darkened its screen — and on a
// PC with Modern Standby a dark screen is the standby, so there it was not kept awake at all. Now
// the one request always asks for both, and the option, its menu row and its words are gone.
//
// HOW. A power request of Karpo's own: PowerCreateRequest with a reason, then PowerSetRequest's
// PowerRequestSystemRequired and PowerRequestDisplayRequired. Not SetThreadExecutionState, which
// PowerToys Awake uses, because a request
//
//   * says who and why: `powercfg /requests` lists Karpo.exe under SYSTEM and DISPLAY with
//     "Keep awake is on in Karpo", in Karpo's language, where an execution state shows as nothing
//     but a process;
//   * belongs to a handle, not a thread: the dock's thread, a menu's pick and the pool's timer that
//     ends a timed one can each set it or clear it, where ES_CONTINUOUS holds only while the thread
//     that set it lives and is cleared only by that thread — a thread of its own, kept for it;
//   * ends with its handle: cleared and closed when it is turned off, when its time is up and when
//     the last widget for it goes, and closed by Windows with the process, so a Karpo that exits or
//     crashes keeps nothing awake.
//
// It asks for nothing more. Windows' power plan and its timeouts are never changed — they are
// Windows' Power & battery page's, where the widget's page leads — and a request does not stop the
// person putting the PC to sleep: the lid, the power button and Start's Sleep still do.
//
// NOT KEPT. Whether it is on is not written anywhere: a Karpo started again starts with it off, as
// PowerToys Awake starts in its passive mode, so nothing left in a file keeps a PC awake that nobody
// remembers asking. How long a click turns it on for is the widget's (SwitchOptions.AwakeMinutes);
// its menu at rest turns it on for another length there and then.
//
// ONE PER THREAD, as do not disturb's service: the dock's one thread has the one every widget, its
// toggles and its menu share, and each test thread its own. Its state is locked, since a timed
// one's time is kept on the pool: it says so each minute, for the time left in the tooltip and on
// the line, and turns itself off at the end. What reaches Windows is behind IAwakeRequest — Windows'
// own request's four calls behind IPowerCalls, so the tests run its life on calls of their own — and
// the clock behind a TimeProvider: a run that may not reach the person's world (every test, every
// smoke pass) is given a request that holds nothing, and the tests their own clock.

using System.Globalization;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What keep awake asks of Windows (see the top of this file).</summary>
internal interface IAwakeRequest
{
    /// <summary>Keeps the PC awake and its screen on; false where Windows refused, and then nothing is held.</summary>
    bool Hold();

    /// <summary>Lets go of it all: nothing is kept awake.</summary>
    void Release();
}

/// <summary>Windows' power request calls (kernel32), behind a seam so the tests run a request's life on calls of their own.</summary>
internal interface IPowerCalls
{
    /// <summary>PowerCreateRequest with a simple reason string; 0 where Windows refused.</summary>
    nint Create(string reason);

    /// <summary>PowerSetRequest.</summary>
    bool Set(nint request, POWER_REQUEST_TYPE type);

    /// <summary>PowerClearRequest.</summary>
    bool Clear(nint request, POWER_REQUEST_TYPE type);

    /// <summary>CloseHandle: Windows clears whatever the request still held.</summary>
    void Close(nint request);
}

/// <summary>Windows' own calls.</summary>
internal sealed unsafe class WindowsPower : IPowerCalls
{
    // winnt.h: POWER_REQUEST_CONTEXT_VERSION and POWER_REQUEST_CONTEXT_SIMPLE_STRING.
    private const uint ContextVersion = 0;
    private const uint SimpleString = 0x1;

    public nint Create(string reason)
    {
        fixed (char* words = reason)
        {
            var context = new REASON_CONTEXT { Version = ContextVersion, Flags = SimpleString };
            context.Reason.SimpleReasonString = words;
            var made = PowerCreateRequest(&context);
            return made == HANDLE.INVALID_VALUE ? 0 : (nint)made.Value;
        }
    }

    public bool Set(nint request, POWER_REQUEST_TYPE type) => PowerSetRequest((HANDLE)request, type);

    public bool Clear(nint request, POWER_REQUEST_TYPE type) => PowerClearRequest((HANDLE)request, type);

    public void Close(nint request) => _ = CloseHandle((HANDLE)request);
}

/// <summary>
/// Windows' own: a power request of Karpo's — the PC kept from sleeping and its screen on, always both (§586) —, made when it
/// is first held and closed when it is let go (see the top of this file). A part Windows refuses lets the whole go: never
/// the PC kept awake behind a dark screen.
/// </summary>
internal sealed class PowerRequest(IPowerCalls calls) : IAwakeRequest
{
    private static readonly POWER_REQUEST_TYPE[] Parts = [POWER_REQUEST_TYPE.PowerRequestSystemRequired, POWER_REQUEST_TYPE.PowerRequestDisplayRequired];

    private readonly object gate = new();
    private readonly List<POWER_REQUEST_TYPE> held = [];
    private nint handle;

    public PowerRequest()
        : this(new WindowsPower())
    {
    }

    public bool Hold()
    {
        lock (gate)
        {
            if (handle == 0)
            {
                handle = calls.Create(S.DockQuickKeepAwakeReason);
                if (handle == 0)
                {
                    return false;
                }
            }
            foreach (var part in Parts.Except(held))
            {
                if (!calls.Set(handle, part))
                {
                    Close();
                    return false;
                }
                held.Add(part);
            }
            return true;
        }
    }

    public void Release()
    {
        lock (gate)
        {
            Close();
        }
    }

    /// <summary>Each part cleared, the last first, and the handle closed, which would clear them as well.</summary>
    private void Close()
    {
        if (handle == 0)
        {
            return;
        }
        for (var at = held.Count - 1; at >= 0; at--)
        {
            _ = calls.Clear(handle, held[at]);
        }
        held.Clear();
        calls.Close(handle);
        handle = 0;
    }
}

/// <summary>A request that holds nothing: what a run that may not reach the person's world is given.</summary>
internal sealed class NoRequest : IAwakeRequest
{
    public bool Hold() => true;

    public void Release()
    {
    }
}

/// <summary>Keep awake as the widgets see it (see the top of this file).</summary>
internal sealed class KeepAwake
{
    [ThreadStatic]
    private static KeepAwake? shared;

    [ThreadStatic]
    private static Func<KeepAwake>? made;

    private readonly IAwakeRequest request;
    private readonly TimeProvider time;
    private readonly object gate = new();

    private bool on;
    private int minutes;
    private DateTimeOffset until;
    private ITimer? tick;
    private int refusals;
    private bool refused;
    private int users;

    internal KeepAwake(IAwakeRequest request, TimeProvider? time = null)
    {
        this.request = request;
        this.time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// This thread's: the dock's, in the dock. Windows' own request, and on a run that may not reach
    /// the person's world one that holds nothing.
    /// </summary>
    public static KeepAwake Shared => shared ??= Made?.Invoke()
        ?? new KeepAwake(Config.IsIsolated || QuickSources.UseSamples ? new NoRequest() : new PowerRequest());

    /// <summary>Where a test's comes from. Per thread; one handed over is built at the next look.</summary>
    internal static Func<KeepAwake>? Made
    {
        get => made;
        set
        {
            made = value;
            shared = null;
        }
    }

    /// <summary>It was turned on, off, or its time moved on; from any thread.</summary>
    public event Action? Changed;

    /// <summary>Whether the PC is kept awake.</summary>
    public bool On
    {
        get
        {
            lock (gate)
            {
                return on;
            }
        }
    }

    /// <summary>How long it was turned on for, in minutes (one of <see cref="KeepAwakeLengths.All"/>); 0 until it is turned off, and while off.</summary>
    public int Minutes
    {
        get
        {
            lock (gate)
            {
                return on ? minutes : 0;
            }
        }
    }

    /// <summary>What a timed one has left, never less than nothing; null while off, and for one on until turned off.</summary>
    public TimeSpan? Left
    {
        get
        {
            lock (gate)
            {
                return on && minutes > 0 ? Max(until - time.GetUtcNow(), TimeSpan.Zero) : null;
            }
        }
    }

    /// <summary>How many times Windows has refused to keep it awake (IQuickSwitch.Refusals).</summary>
    public int Refusals => Volatile.Read(ref refusals);

    /// <summary>Whether the last time it was asked, Windows refused: the tooltip says so until it is asked again.</summary>
    public bool Refused
    {
        get
        {
            lock (gate)
            {
                return refused;
            }
        }
    }

    /// <summary>
    /// On from now, the PC and its screen, for <paramref name="length"/> minutes (0: until it is turned
    /// off); again from now where it was on already. Where Windows refuses, it is off.
    /// </summary>
    public void Turn(int length)
    {
        length = Math.Max(0, length);
        lock (gate)
        {
            if (!request.Hold())
            {
                Stop();
                refused = true;
                Interlocked.Increment(ref refusals);
            }
            else
            {
                on = true;
                refused = false;
                minutes = length;
                until = time.GetUtcNow() + TimeSpan.FromMinutes(length);
                Arm();
            }
        }
        Changed?.Invoke();
    }

    /// <summary>Off: Windows may put the PC to sleep again as it would.</summary>
    public void Off()
    {
        lock (gate)
        {
            if (!on)
            {
                return;
            }
            Stop();
        }
        Changed?.Invoke();
    }

    /// <summary>A widget for it starts following it (KeepAwakeSwitch.Start).</summary>
    internal void Join() => Interlocked.Increment(ref users);

    /// <summary>
    /// A widget for it is gone. With the last, it is turned off: nothing would be left to say the PC
    /// is kept awake, nor to let it sleep again.
    /// </summary>
    internal void Leave()
    {
        if (Interlocked.Decrement(ref users) <= 0)
        {
            Volatile.Write(ref users, 0);
            Off();
        }
    }

    /// <summary>
    /// What <paramref name="left"/> is said as, in whole minutes rounded up — "1 h 20 min", "1 min" —
    /// so it never says nothing while there is time left (BatteryText.Span's words).
    /// </summary>
    internal static string Say(TimeSpan left) =>
        BatteryText.Span(TimeSpan.FromMinutes(Math.Max(1, Math.Ceiling(left.TotalMinutes))));

    /// <summary>The words of a row's length (<see cref="KeepAwakeLengths"/>), as a row's target keeps them.</summary>
    internal static string Word(int length) => length.ToString(CultureInfo.InvariantCulture);

    /// <summary>A row's length back from its words; null for words no row writes.</summary>
    internal static int? Length(string? word) =>
        int.TryParse(word, NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? KeepAwakeLengths.Known(length) : null;

    // Under the gate: off, the request let go and the clock stopped.
    private void Stop()
    {
        request.Release();
        on = false;
        minutes = 0;
        tick?.Dispose();
        tick = null;
    }

    /// <summary>
    /// Under the gate: a timed one's clock set for the next whole minute of what it has left, when what
    /// it says changes, or for its end, whichever comes first; nothing for one on until turned off.
    /// </summary>
    private void Arm()
    {
        if (!on || minutes == 0)
        {
            tick?.Dispose();
            tick = null;
            return;
        }
        var left = until - time.GetUtcNow();
        var due = left <= TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromTicks(left.Ticks % TimeSpan.TicksPerMinute);
        if (due == TimeSpan.Zero && left > TimeSpan.Zero)
        {
            due = Min(left, TimeSpan.FromMinutes(1));
        }
        tick ??= time.CreateTimer(static state => ((KeepAwake)state!).Ticked(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        tick.Change(due, Timeout.InfiniteTimeSpan);
    }

    /// <summary>On the pool, which lets nothing out: off at its end, else the minute it says has changed.</summary>
    private void Ticked() => Troubles.Guard("Keep awake: its time", () =>
    {
        lock (gate)
        {
            if (!on || minutes == 0)
            {
                return;
            }
            if (time.GetUtcNow() >= until)
            {
                Stop();
            }
            else
            {
                Arm();
            }
        }
        Changed?.Invoke();
    });

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

    private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;
}

/// <summary>
/// Keep awake as a one-button widget (SwitchIsland), its toggle and its line: on and off with a click,
/// for as long as the widget says, its screen on with it. It keeps nothing of its own — the
/// state is the service's, so every widget for it, its toggle and its menu never disagree.
/// </summary>
internal sealed class KeepAwakeSwitch : IQuickSwitch
{
    private readonly KeepAwake service;
    private Action? changed;
    private bool following;

    public KeepAwakeSwitch(string name, KeepAwake service)
    {
        Name = name;
        this.service = service;
    }

    public string Name { get; }

    public bool Available => true;

    public bool? On => service.On;

    public bool Switches => true;

    public int Refusals => service.Refusals;

    /// <summary>What a click does, or that Windows refused the last.</summary>
    public string? Note => service.Refused ? S.DockQuickKeepAwakeRefused : S.DockQuickKeepAwakeNote;

    /// <summary>A timed one's time left, under its name and in its tooltip; nothing for one on until turned off.</summary>
    public string? Detail => service.Left is { } left ? S.DockQuickKeepAwakeLeft(KeepAwake.Say(left)) : null;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        if (!following)
        {
            following = true;
            service.Changed += Heard;
            service.Join();
        }
    }

    /// <summary>Off where it is on; else on until turned off (a toggles row's click).</summary>
    public void Act() => Act(null);

    /// <summary>Off where it is on; else on for as long as <paramref name="options"/> say.</summary>
    public void Act(SwitchOptions? options)
    {
        if (service.On)
        {
            service.Off();
            return;
        }
        service.Turn(KeepAwakeLengths.Known(options?.AwakeMinutes ?? 0));
    }

    public void Dispose()
    {
        if (following)
        {
            following = false;
            service.Changed -= Heard;
            service.Leave();
        }
    }

    private void Heard() => changed?.Invoke();
}
