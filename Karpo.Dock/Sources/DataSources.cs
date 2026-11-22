// The data behind the widgets, gathered from the dock's start.
//
// The person, 2026-09-27: "widgets like the processor display / graph only get initialized when i
// open the folder. they should get initialized on start so that they can already show the
// accumulated historical data. this should go for all widgets".
//
// A widget is two things: its data and its drawing. The drawing is the island's, made for a bar
// that shows it or for a folder that holds it, and it stays as lazy as it was: nothing is drawn
// that nobody sees, and a closed folder's widgets are not ticked (docs\widgets.md §11). The data
// is a **source**'s — one per kind of data: the processor's load, the weather of a
// place, a share's price, one switch — made for every widget the config names, on any bar, in
// any folder, in any layout, shown or not, and gathered at its own interval from the dock's
// start. Every widget that shows a kind of data reads the one source of it, so sixteen processor
// graphs are one reading of the processor and one history, and a folder opened after an hour
// draws the hour.
//
// Three kinds of hold keep a source running:
//
//  * **The dock's own** (Always). The performance readings — the processor, the graphics, the
//    memory, the disk and the network, read every second — run from the dock's start whether or
//    not a widget in the config shows them (the person, 2026-10-09: "there should be one service
//    in the background that always gets the data for the graph and then all the widgets that show
//    this data should pull the data from this one service. this service should always run."). A
//    taskbar entry's flyout, a card made later and a layout shown for the first time all draw a
//    history that is already there. Nothing lets these go but Clear.
//  * **The config's** (Want). The dock asks at its start and after every change of the config
//    which sources the config's widgets need, every layout's and every folder's; the ones missing
//    are made and started, and the ones no widget of the config needs any more lose the config's
//    hold.
//  * **A widget's** (Hold, Release). An island takes its source when it is made and lets it go
//    when it goes, so a widget made outside the dock — a test, a picture, the smoke — has its data
//    as well, and one the config has just lost keeps its source until the island is gone.
//
// A source stops (Dispose) with its last hold. What it knew goes with it: a widget that comes back
// later starts its history over, as it would have before.
//
// **Ticking.** The dock's timer ticks every source whose interval has passed (Tick), whether or not
// a bar is on screen. A widget that ticks asks its own source first (DataSource.Catch), so the
// widget draws what was just read; either way a source gathers once per interval, however many
// ask. A source that follows events (IntervalMs 0) never ticks — starting it is all it needs.
//
// Thread: the dock uses this from its own thread. Tests make widgets on several threads at once,
// so holding and releasing are locked; gathering is locked per source.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>
/// What a source is known by: the kind of data, and what picks one of it out — a place, a symbol,
/// a pace. Two widgets whose data has one key read one source.
/// </summary>
internal readonly record struct SourceKey(string Kind, string Of = "")
{
    public override string ToString() => Of.Length == 0 ? Kind : $"{Kind}:{Of}";
}

/// <summary>
/// The data one kind of widget shows, gathered whether or not anything draws it (DataSources).
/// </summary>
internal abstract class DataSource(SourceKey key) : IDisposable
{
    private readonly object gate = new();

    // When it last gathered, in ms of Environment.TickCount64's clock; 0 before the first time.
    private long last;

    public SourceKey Key { get; } = key;

    /// <summary>How often it gathers, in ms; 0 for a source that follows events and never ticks.</summary>
    public abstract int IntervalMs { get; }

    /// <summary>How many times it has gathered: what a test asks after its own ticks.</summary>
    internal int Gathered { get; private set; }

    /// <summary>
    /// Starts following what it stands for: once, when the first hold makes it. It must not block —
    /// the dock calls it while it arranges its bars — so what it has to ask goes to the pool.
    /// </summary>
    protected internal virtual void Start()
    {
    }

    /// <summary>Gathers now: reads, fetches or asks. True when what it holds changed.</summary>
    protected abstract bool Gather(long now);

    /// <summary>A device came or went, or the machine woke: measures from here. True when something changed.</summary>
    protected internal virtual bool Renew(long now) => false;

    /// <summary>A system message the dock forwards (the power broadcasts), heard once however many widgets show this data.</summary>
    protected internal virtual void Hear(uint msg, nuint wParam)
    {
    }

    /// <summary>
    /// Gathers when its interval has passed since it last did, less <paramref name="slack"/> ms for
    /// the timer's jitter; the first ask always gathers. True when it gathered and what it holds
    /// changed. Safe to call from every driver at once — the dock's timer, a widget's tick, an open
    /// folder's — because only the first ask of an interval gathers.
    /// </summary>
    public bool Catch(long now, long slack = 0)
    {
        if (IntervalMs <= 0)
        {
            return false;
        }
        lock (gate)
        {
            if (Gathered > 0 && now - last + slack < IntervalMs)
            {
                return false;
            }
            last = now;
            Gathered++;
            return Gather(now);
        }
    }

    /// <summary><see cref="Renew"/>, under the source's own lock.</summary>
    public bool Refresh(long now)
    {
        lock (gate)
        {
            return Renew(now);
        }
    }

    /// <summary>Stops following what it stands for, and lets go of what it opened.</summary>
    public virtual void Dispose()
    {
    }
}

/// <summary>
/// Every source the dock's widgets read: one per kind of data, held by the config and by the
/// widgets that show it, ticked by the dock from its start.
/// </summary>
internal sealed class DataSources
{
    private readonly object gate = new();
    private readonly Dictionary<SourceKey, Entry> entries = [];
    private readonly Func<SourceKey, WidgetItem, DataSource?> make;
    private readonly IReadOnlyList<(SourceKey Key, WidgetItem Widget)> always;
    private DataSource[]? snapshot;

    /// <summary>
    /// A registry that makes each source with <paramref name="make"/>: the one the dock uses makes
    /// the real ones (<see cref="SourceKinds.Make"/>), and a test hands in sources that read
    /// numbers it chose. <paramref name="always"/> are the sources it keeps running with no widget
    /// naming them (<see cref="SourceKinds.Always"/>); none for a registry that follows the config alone.
    /// </summary>
    public DataSources(Func<SourceKey, WidgetItem, DataSource?> make, IReadOnlyList<(SourceKey Key, WidgetItem Widget)>? always = null)
    {
        this.make = make;
        this.always = always ?? [];
    }

    /// <summary>The dock's own: one per process, as the dock is.</summary>
    public static DataSources Shared { get; } = new(SourceKinds.Make, SourceKinds.Always());

    private sealed class Entry(DataSource source)
    {
        public DataSource Source { get; } = source;

        /// <summary>How many widgets hold it.</summary>
        public int Holds { get; set; }

        /// <summary>Whether the config names a widget that needs it.</summary>
        public bool Wanted { get; set; }
    }

    /// <summary>The sources running now, by key.</summary>
    public IReadOnlyList<SourceKey> Running
    {
        get
        {
            lock (gate)
            {
                return [.. entries.Keys];
            }
        }
    }

    /// <summary>The source of <paramref name="key"/> while it runs; null otherwise. Holds nothing.</summary>
    public DataSource? Find(SourceKey key)
    {
        lock (gate)
        {
            return entries.TryGetValue(key, out var entry) ? entry.Source : null;
        }
    }

    /// <summary>
    /// The sources <paramref name="config"/>'s widgets need — on every bar, in every folder, on
    /// every display, shown or not — and the ones the registry always keeps (its <c>always</c> list),
    /// made and started where they are missing; those the config
    /// named before and needs no longer lose its hold, and stop unless a widget still holds them.
    /// The dock calls this at its start and after every change of the config.
    /// </summary>
    public void Want(Config config)
    {
        var needed = new Dictionary<SourceKey, WidgetItem>();
        foreach (var (key, widget) in always)
        {
            needed.TryAdd(key, widget);
        }
        foreach (var layout in config.Layouts)
        {
            foreach (var (_, widget) in Folders.All(layout.Islands))
            {
                foreach (var (key, maker) in SourceKinds.Wanted(widget))
                {
                    needed.TryAdd(key, maker);
                }
            }
        }
        var stopped = new List<DataSource>();
        lock (gate)
        {
            foreach (var (key, entry) in entries.ToArray())
            {
                if (entry.Wanted && !needed.ContainsKey(key))
                {
                    entry.Wanted = false;
                    if (entry.Holds == 0)
                    {
                        entries.Remove(key);
                        snapshot = null;
                        stopped.Add(entry.Source);
                    }
                }
            }
            foreach (var (key, widget) in needed)
            {
                if (entries.TryGetValue(key, out var entry))
                {
                    entry.Wanted = true;
                }
                else if (Made(key, widget) is { } source)
                {
                    entries[key] = new Entry(source) { Wanted = true };
                    snapshot = null;
                }
            }
        }
        foreach (var source in stopped)
        {
            Stop(source);
        }
    }

    /// <summary>
    /// The source <paramref name="widget"/>'s data comes from, made and started if nothing holds it
    /// yet, and held until <see cref="Release"/>; null for a widget whose kind has no source.
    /// </summary>
    public DataSource? Hold(WidgetItem widget) =>
        SourceKinds.KeyOf(widget) is { } key ? Hold(key, widget) : null;

    /// <summary>The source of <paramref name="key"/>, made with <paramref name="widget"/>'s settings if it is not running; held until <see cref="Release"/>.</summary>
    public DataSource? Hold(SourceKey key, WidgetItem widget)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(key, out var entry))
            {
                if (Made(key, widget) is not { } source)
                {
                    return null;
                }
                entry = new Entry(source);
                entries[key] = entry;
                snapshot = null;
            }
            entry.Holds++;
            return entry.Source;
        }
    }

    /// <summary>A widget lets its source go; the source stops when nothing holds it any more.</summary>
    public void Release(DataSource? source)
    {
        if (source is null)
        {
            return;
        }
        lock (gate)
        {
            if (!entries.TryGetValue(source.Key, out var entry) || !ReferenceEquals(entry.Source, source))
            {
                return;
            }
            entry.Holds = Math.Max(0, entry.Holds - 1);
            if (entry.Holds > 0 || entry.Wanted)
            {
                return;
            }
            entries.Remove(source.Key);
            snapshot = null;
        }
        Stop(source);
    }

    /// <summary>
    /// The shortest interval of any source that ticks, in ms; 0 when none does. What the dock's
    /// timer runs at, bars on screen or not.
    /// </summary>
    public int Period
    {
        get
        {
            lock (gate)
            {
                return entries.Values
                    .Select(entry => entry.Source.IntervalMs)
                    .Where(interval => interval > 0)
                    .DefaultIfEmpty(0)
                    .Min();
            }
        }
    }

    /// <summary>
    /// Every source whose interval has passed gathers (<see cref="DataSource.Catch"/>), less
    /// <paramref name="slack"/> ms for the timer's jitter. True when any of them changed.
    /// </summary>
    public bool Tick(long now, long slack = 0)
    {
        var changed = false;
        foreach (var source in Sources())
        {
            try
            {
                changed |= source.Catch(now, slack);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: {source.Key} not read: {error.Message}");
            }
        }
        return changed;
    }

    /// <summary>A device came or went, or the machine woke: every source measures from here.</summary>
    public void Refresh(long now)
    {
        foreach (var source in Sources())
        {
            try
            {
                _ = source.Refresh(now);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: {source.Key} not renewed: {error.Message}");
            }
        }
    }

    /// <summary>A system message the dock forwards to its widgets, heard by every source as well.</summary>
    public void System(uint msg, nuint wParam)
    {
        foreach (var source in Sources())
        {
            try
            {
                source.Hear(msg, wParam);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Console.Error.WriteLine($"Karpo: {source.Key} did not hear {msg}: {error.Message}");
            }
        }
    }

    /// <summary>Every source stops: the dock is closing.</summary>
    public void Clear()
    {
        List<DataSource> stopped;
        lock (gate)
        {
            stopped = [.. entries.Values.Select(entry => entry.Source)];
            entries.Clear();
            snapshot = null;
        }
        foreach (var source in stopped)
        {
            Stop(source);
        }
    }

    /// <summary>The sources running now, as one array kept until the set changes: a tick a second allocates nothing.</summary>
    private DataSource[] Sources()
    {
        lock (gate)
        {
            return snapshot ??= [.. entries.Values.Select(entry => entry.Source)];
        }
    }

    /// <summary>A new source of <paramref name="key"/>, started; null where the kind has none or it could not be made.</summary>
    private DataSource? Made(SourceKey key, WidgetItem widget)
    {
        DataSource? source = null;
        try
        {
            source = make(key, widget);
            source?.Start();
            return source;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: {key} not started: {error.Message}");
            source?.Dispose();
            return null;
        }
    }

    private static void Stop(DataSource source)
    {
        try
        {
            source.Dispose();
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: {source.Key} not stopped: {error.Message}");
        }
    }
}
