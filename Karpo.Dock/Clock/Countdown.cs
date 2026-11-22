// A timer and a focus session as numbers, and the countdowns a dock is running.
// A countdown is a value: what it is, how long, when it ends, and what is left
// while it is paused. Nothing in it reads the clock by itself — the dock's
// one-second tick hands it the time — so every rule here has a test
// (Karpo.Tests\Calendars\ClockWidgetTests.cs, CountdownsTests.cs).
//
// A length is set with a minus and a plus (and the wheel over the number),
// nothing else: no presets, and a focus session has no break — it is one
// stretch that ends when it ends.
//
// Every timer and every focus session is a countdown of its own (Countdowns):
// each widget's, kept by its id, on the bar or in a folder. Any number of them
// run at once, and each ends and rings on its own. They
// are in memory only: a restart of Karpo clears them, which is what a taskbar
// timer should do rather than ring for something the person set before the PC
// went down. What does come back is each one's length, which is a setting of its
// widget in config.json (CountdownOptions).

using System.Globalization;
using System.Runtime.InteropServices;
using Karpo.Core;

namespace Karpo.Dock;

public enum CountdownKind
{
    None,

    /// <summary>A plain timer: a length, and a ring when it is over.</summary>
    Timer,

    /// <summary>A focus session: one stretch of a length, with no break.</summary>
    Focus,
}

public enum CountdownPhase
{
    Idle,
    Running,
    Paused,

    /// <summary>It ran out; it stays there until the person clears or restarts it.</summary>
    Done,
}

/// <param name="Length">What was chosen, so it can be started again.</param>
/// <param name="Until">When it ends.</param>
/// <param name="Left">What is left while it is paused.</param>
internal readonly record struct Countdown(
    CountdownKind Kind,
    CountdownPhase Phase,
    TimeSpan Length,
    DateTime Until,
    TimeSpan Left)
{
    /// <summary>The longest a timer may be set to, and the shortest: whole minutes, as it is set.</summary>
    public static readonly TimeSpan Longest = TimeSpan.FromHours(12);

    public static readonly TimeSpan Shortest = TimeSpan.FromMinutes(1);

    /// <summary>What a timer and a focus session are set to before anyone has changed them.</summary>
    public static readonly TimeSpan TimerDefault = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan FocusDefault = TimeSpan.FromMinutes(25);

    /// <summary>From an hour up, the minutes stepper moves five minutes at a time rather than one.</summary>
    private static readonly TimeSpan CoarseFrom = TimeSpan.FromHours(1);

    public static Countdown None => default;

    /// <summary>Nothing is set.</summary>
    public bool Idle => Kind == CountdownKind.None || Phase == CountdownPhase.Idle;

    /// <summary>It counts down, so the dock ticks every second.</summary>
    public bool Ticking => Phase == CountdownPhase.Running;

    /// <summary>A timer or focus session of <paramref name="length"/>, started at <paramref name="now"/>.</summary>
    public static Countdown Start(CountdownKind kind, TimeSpan length, DateTime now)
    {
        if (kind == CountdownKind.None)
        {
            return None;
        }
        var run = Clamp(length);
        return new Countdown(kind, CountdownPhase.Running, run, now + run, run);
    }

    /// <summary>What is left; zero once it is over.</summary>
    public TimeSpan Remaining(DateTime now) => Phase switch
    {
        CountdownPhase.Running => Until > now ? Until - now : TimeSpan.Zero,
        CountdownPhase.Paused => Left,
        _ => TimeSpan.Zero,
    };

    /// <summary>The countdown at <paramref name="now"/>: it is over once it has run out.</summary>
    public Countdown At(DateTime now) =>
        Phase != CountdownPhase.Running || now < Until ? this : this with { Phase = CountdownPhase.Done, Left = TimeSpan.Zero };

    public Countdown Pause(DateTime now) =>
        Phase == CountdownPhase.Running ? this with { Phase = CountdownPhase.Paused, Left = Remaining(now) } : this;

    public Countdown Resume(DateTime now) =>
        Phase == CountdownPhase.Paused ? this with { Phase = CountdownPhase.Running, Until = now + Left } : this;

    /// <summary>Back to the start, still set to the same length, not running.</summary>
    public Countdown Reset() =>
        Idle ? this : this with { Phase = CountdownPhase.Paused, Until = default, Left = Length };

    /// <summary>The same again, from <paramref name="now"/>.</summary>
    public Countdown Again(DateTime now) => Start(Kind, Length, now);

    /// <summary>
    /// <paramref name="length"/> a step of minutes on (<paramref name="direction"/> 1) or back (−1),
    /// moved to the next whole step: a minute at a time under an hour, five minutes from an hour up.
    /// Never below <see cref="Shortest"/> or above <see cref="Longest"/>, and it never wraps.
    /// </summary>
    public static TimeSpan StepMinutes(TimeSpan length, int direction) =>
        Step(length, direction, length >= CoarseFrom ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(1));

    /// <summary>
    /// The next whole <paramref name="step"/> after (or before) <paramref name="length"/>: 1:01 up by
    /// five minutes is 1:05, and down is 1:00, so the number lands on the step's own grid.
    /// </summary>
    private static TimeSpan Step(TimeSpan length, int direction, TimeSpan step)
    {
        if (direction == 0 || step <= TimeSpan.Zero)
        {
            return Clamp(length);
        }
        var ticks = Clamp(length).Ticks;
        var size = step.Ticks;
        var next = direction > 0
            ? (ticks / size + 1) * size
            : ((ticks + size - 1) / size - 1) * size;
        return Clamp(TimeSpan.FromTicks(Math.Max(0, next)));
    }

    /// <summary>
    /// What a clock face or an entry shows while this runs, always the same five figures wide so
    /// the widget never moves as it counts down: minutes and seconds ("04:59") under an hour, hours
    /// and minutes ("01:05") from an hour up, "00:00" once it is over. Empty when nothing is set.
    /// </summary>
    public string Cell(DateTime now)
    {
        if (Idle)
        {
            return "";
        }
        var whole = Phase == CountdownPhase.Done ? TimeSpan.Zero : Remaining(now);
        whole = TimeSpan.FromSeconds(Math.Ceiling(whole.TotalSeconds));
        var culture = CultureInfo.InvariantCulture;
        return whole >= TimeSpan.FromHours(1)
            ? string.Create(culture, $"{(int)whole.TotalHours:00}:{whole.Minutes:00}")
            : string.Create(culture, $"{whole.Minutes:00}:{whole.Seconds:00}");
    }

    /// <summary>What the flyout says the countdown is doing.</summary>
    public string Says(DateTime now) => Phase switch
    {
        CountdownPhase.Done => Kind == CountdownKind.Focus ? S.DockClockFocusOver : S.DockClockTimerOver,
        CountdownPhase.Paused => Kind == CountdownKind.Focus ? S.DockClockFocusPaused : S.DockClockPaused,
        CountdownPhase.Running => Kind == CountdownKind.Focus ? S.DockClockFocusTime : S.DockClockTimer,
        _ => "",
    };

    /// <summary>"4:59", "12:34", "1:04:12": minutes and seconds, hours only when there are some.</summary>
    public static string Span(TimeSpan left)
    {
        var whole = left < TimeSpan.Zero ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        var culture = CultureInfo.InvariantCulture;
        return whole >= TimeSpan.FromHours(1)
            ? string.Create(culture, $"{(int)whole.TotalHours}:{whole.Minutes:00}:{whole.Seconds:00}")
            : string.Create(culture, $"{whole.Minutes}:{whole.Seconds:00}");
    }

    /// <summary>The minutes a length is set to, as its stepper shows them ("90" for an hour and a half).</summary>
    public static string MinutesOf(TimeSpan length) =>
        ((int)Clamp(length).TotalMinutes).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// "25 min", "1 h", "1 h 30 min": a length in words, for a tooltip or a line of text. Never under
    /// a minute — <see cref="Clamp"/> sees to that — and a length that isn't whole minutes (nothing
    /// sets one now: a length is a widget's whole minutes) would keep its seconds.
    /// </summary>
    public static string Words(TimeSpan length)
    {
        var whole = Clamp(length);
        var hours = (int)whole.TotalHours;
        var minutes = whole.Minutes;
        var seconds = whole.Seconds;
        var text = hours > 0 ? S.DockClockHours(hours) : "";
        if (minutes > 0)
        {
            text = text.Length > 0 ? $"{text} {S.DockClockMinutes(minutes)}" : S.DockClockMinutes(minutes);
        }
        if (seconds > 0 && hours == 0)
        {
            text = text.Length > 0 ? $"{text} {S.DockClockSeconds(seconds)}" : S.DockClockSeconds(seconds);
        }
        return text;
    }

    /// <summary>A length within what a timer may be, whole seconds.</summary>
    public static TimeSpan Clamp(TimeSpan length) =>
        length < Shortest ? Shortest
        : length > Longest ? Longest
        : TimeSpan.FromSeconds(Math.Round(length.TotalSeconds));
}

/// <summary>
/// The countdowns a dock is running: one per widget — a timer's or a focus session's own, kept by
/// its widget's id, so it goes on running where the widget is carried and goes when the widget is
/// removed (<see cref="Keep"/>). Each runs, pauses, ends and rings on its own, and any number of
/// them run at once: "you should be able to add many different timers with their own default
/// times" (the person, 2026-09-27). There used to be one countdown for the whole process, which a
/// timer and a focus session took over from each other, and every widget of a kind showed it.
/// <para>
/// One per dock, made by the dock and handed to what it makes — its islands, a folder and the
/// widgets in it, a countdown's own panel — and never a static the whole process shares: a test makes
/// its own, so two tests running at once cannot see each other's. On the dock's thread only.
/// </para>
/// </summary>
internal sealed class Countdowns
{
    private readonly Dictionary<string, Countdown> set = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>A countdown changed — started, paused, taken up again, over, cleared, or dropped: its key.</summary>
    public event Action<string>? Changed;

    /// <summary>A countdown has just run out, once, after its state is the new one: its key, and what it is now.</summary>
    public event Action<string, Countdown>? Ended;

    /// <summary>
    /// A length was stepped — a card's minus or plus, the wheel, a pane's steppers — and changed in
    /// its widget's options: whoever keeps the config (the dock) writes it once the steps settle.
    /// </summary>
    public event Action<string>? Stepped;

    /// <summary>
    /// The dock's stopwatches (Stopwatch.cs), kept beside its countdowns so they go wherever the
    /// countdowns are handed — an island, a folder and the widgets in it, a panel — without a second
    /// store to thread through every one of them.
    /// </summary>
    public Stopwatches Stopwatches { get; } = new();

    /// <summary>
    /// alarm: the dock's alarms (Karpo.Core\Alarms.cs), kept here for the same reason: every alarm
    /// line, wherever it is made, reads and works the one book the dock rings (Dock.Alarms.cs).
    /// </summary>
    public AlarmBook Alarms { get; } = new();

    /// <summary>What the countdown of <paramref name="key"/> is doing; <see cref="Countdown.None"/> when it is not set.</summary>
    public Countdown Of(string key) => set.GetValueOrDefault(key);

    /// <summary>Whether any of them counts down, so the dock ticks every second.</summary>
    public bool Ticking => set.Values.Any(countdown => countdown.Ticking);

    /// <summary>The keys of the countdowns that are set: running, paused or over.</summary>
    public IReadOnlyCollection<string> Keys => set.Keys;

    public void Set(string key, Countdown next)
    {
        if (next == Of(key))
        {
            return;
        }
        if (next.Idle)
        {
            set.Remove(key);
        }
        else
        {
            set[key] = next;
        }
        Changed?.Invoke(key);
    }

    public void Start(string key, CountdownKind kind, TimeSpan length, DateTime now) => Set(key, Countdown.Start(kind, length, now));

    public void Pause(string key, DateTime now) => Set(key, Of(key).Pause(now));

    public void Resume(string key, DateTime now) => Set(key, Of(key).Resume(now));

    /// <summary>Stops it: nothing set, the length it starts at again.</summary>
    public void Clear(string key) => Set(key, Countdown.None);

    /// <summary>
    /// The start button of the countdown <paramref name="key"/>, one rule for every face of it — an
    /// entry, a card, a line, its own panel, a folder's menu: it pauses the countdown while it
    /// runs, takes it up again while it is paused with time left, and otherwise starts it at
    /// <paramref name="length"/>, which starts again one that ran out. It touches no other.
    /// </summary>
    public void Act(string key, CountdownKind kind, TimeSpan length, DateTime now)
    {
        switch (Of(key))
        {
            case { Phase: CountdownPhase.Running }:
                Pause(key, now);
                break;
            case { Phase: CountdownPhase.Paused, Left.Ticks: > 0 }:
                Resume(key, now);
                break;
            default:
                Start(key, kind, length, now);
                break;
        }
    }

    /// <summary>A length of <paramref name="key"/>'s was stepped (<see cref="Stepped"/>).</summary>
    public void Remember(string key) => Stepped?.Invoke(key);

    /// <summary>
    /// The second passed: each countdown at <paramref name="now"/>. One that ran out raises
    /// <see cref="Ended"/> once, after its state is the new one, and stays over until it is cleared
    /// or started again. True when what they show changed, which a running one always has.
    /// </summary>
    public bool Tick(DateTime now)
    {
        if (set.Count == 0)
        {
            return false;
        }
        var changed = false;
        List<(string Key, Countdown State)>? ended = null;
        foreach (var (key, was) in set.ToList())
        {
            var next = was.At(now);
            changed |= was.Ticking;
            if (next == was)
            {
                continue;
            }
            set[key] = next;
            changed = true;
            Changed?.Invoke(key);
            if (was.Phase == CountdownPhase.Running && next.Phase != CountdownPhase.Running)
            {
                (ended ??= []).Add((key, next));
            }
        }
        foreach (var (key, state) in ended ?? [])
        {
            Ended?.Invoke(key, state);
        }
        return changed;
    }

    /// <summary>
    /// edit: the keys of the widgets cut onto edit mode's clipboard, which no layout has while they
    /// are there and whose countdowns and stopwatches <see cref="Keep"/> keeps all the same: the next
    /// Ctrl+V puts them down with their ids (<see cref="BarItem.Pasted"/>), and a cut and a paste are
    /// a move, which a running timer goes on running through. None once something else is on the clipboard.
    /// </summary>
    private HashSet<string> carried = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>edit: <paramref name="keys"/> are the widgets cut onto the clipboard now (<see cref="carried"/>); none for a copy.</summary>
    public void Carry(IEnumerable<string> keys) => carried = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Only the countdowns of <paramref name="alive"/> stay (<see cref="KeysIn"/>), and those of the
    /// widgets cut onto the clipboard (<see cref="Carry"/>): a widget removed takes its countdown with
    /// it. One carried to another place keeps its id, and so its countdown. The stopwatches are kept
    /// by the same rule.
    /// </summary>
    public void Keep(IEnumerable<string> alive)
    {
        var kept = new HashSet<string>(alive, StringComparer.OrdinalIgnoreCase);
        kept.UnionWith(carried);
        foreach (var key in set.Keys.Where(key => !kept.Contains(key)).ToList())
        {
            set.Remove(key);
            Changed?.Invoke(key);
        }
        Stopwatches.Keep(kept);
    }

    /// <summary>
    /// The key of every countdown <paramref name="config"/> has: each timer's, focus session's and
    /// stopwatch's own, wherever it stands — on any display's taskbar, in a folder.
    /// </summary>
    public static IEnumerable<string> KeysIn(Config config)
    {
        foreach (var layout in config.Layouts)
        {
            foreach (var (_, island) in Folders.All(layout.Islands))
            {
                if (island.Info?.Options == WidgetOptionsKind.Countdown || island.Info == WidgetInfos.Stopwatch)
                {
                    yield return island.Id;
                }
            }
        }
    }
}

/// <summary>
/// The ring's own rule: every countdown that runs out rings, once, and several running out in the
/// same second ring once between them — two sounds on top of each other are one sound cut short.
/// The dock keeps one (<see cref="ClockSound.Ring"/> behind it).
/// </summary>
internal sealed class Ringer(Action ring)
{
    private long? rang;

    /// <summary>A countdown ran out at <paramref name="now"/> (ms): it rings unless one rang less than a second ago. True when it rang.</summary>
    public bool Ended(long now)
    {
        if (rang is { } last && now - last < 1000)
        {
            return false;
        }
        rang = now;
        ring();
        return true;
    }
}

/// <summary>The sound a timer makes when it is over: Windows' own notification sound.</summary>
internal static class ClockSound
{
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_ALIAS = 0x00010000;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint MB_OK = 0x00000000;

    /// <summary>
    /// Karpo has no packaged identity, so it cannot raise a Windows toast: a countdown that runs out
    /// rings with the sound Windows gives notifications, once, and its own face says it is over.
    /// </summary>
    public static void Ring()
    {
        try
        {
            if (!PlaySoundW("Notification.Default", 0, SND_ALIAS | SND_ASYNC | SND_NODEFAULT))
            {
                _ = MessageBeep(MB_OK);
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    [DllImport("winmm", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySoundW(string? pszSound, nint hmod, uint fdwSound);

    [DllImport("user32", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MessageBeep(uint uType);
}
