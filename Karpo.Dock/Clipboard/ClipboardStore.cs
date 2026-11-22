// clipboard: the history the clipboard line shows (ClipboardLine), shared by every clipboard line and face
// (the backlog, item 13: "shows the recent clipboard values, images too if possible, in a flyout").
//
// Which history it shows is Windows' own while the person has it on (Settings, System, Clipboard,
// "Clipboard history") and Windows gives it to Karpo — the history Win+V shows, read afresh each
// time the folder showing it opens. Otherwise it is Karpo's own (ClipboardRing): what the clipboard heard
// while a clipboard widget stood on a bar, in memory only. Karpo keeps its own only while Windows'
// cannot be had, so a copy is not read twice over, by Windows and by Karpo, for nothing.
//
// It follows ContactStore: the widget attaches, the reading is done on the pool and Changed is
// raised on the dock's thread through a StoreSignal; the line only ever draws what is in memory.
// What it asks of Windows is one seam (IClipboardSystem), so a test brings a fake and never
// touches the person's clipboard.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Whose history the line shows.</summary>
internal enum ClipSource
{
    /// <summary>Windows' own (Win+V).</summary>
    Windows,

    /// <summary>Karpo's own, in memory, while Windows' is off or cannot be read.</summary>
    Karpo,
}

internal sealed class ClipboardStore
{
    /// <summary>The largest thumbnail a picture is made, in px: more than a line's widest picture at 200 %.</summary>
    internal const int ThumbnailWidth = 680;
    internal const int ThumbnailHeight = 400;

    /// <summary>
    /// What the shared store asks of Windows: the real clipboard — or, set by a test run before
    /// anything looks, a stand-in, so a test that makes every kind of widget never hears or reads
    /// the person's clipboard.
    /// </summary>
    internal static IClipboardSystem SharedSystem { get; set; } = ClipboardSystem.Shared;

    private static readonly Lazy<ClipboardStore> SharedStore = new(() => new ClipboardStore(SharedSystem));

    private readonly IClipboardSystem system;
    private readonly StoreSignal signal;
    private readonly Lock gate = new();
    private readonly ClipboardRing ring = new();
    private readonly Dictionary<long, Pixels?> thumbnails = [];
    private readonly HashSet<CellIsland> watchers = new(ReferenceEqualityComparer.Instance);

    private Task working = Task.CompletedTask;

    /// <summary>
    /// How long reading Windows' history may take in all before Karpo's own is shown instead, with
    /// <see cref="HistoryUnread"/>; a test makes it short.
    /// </summary>
    internal TimeSpan HistoryWait { get; init; } = TimeSpan.FromSeconds(8);
    private bool windowsOn;
    private bool windowsFailed;

    internal ClipboardStore(IClipboardSystem system)
    {
        this.system = system;
        signal = new StoreSignal(() => Changed?.Invoke());
    }

    /// <summary>The store the dock uses, made on first use.</summary>
    public static ClipboardStore Shared => SharedStore.Value;

    /// <summary>The history changed, or reading it did. Raised on the dock's thread.</summary>
    public event Action? Changed;

    /// <summary>Whose history <see cref="Items"/> is.</summary>
    public ClipSource Source { get; private set; } = ClipSource.Karpo;

    /// <summary>The items to show, the newest first. Never null.</summary>
    public IReadOnlyList<ClipView> Items { get; private set; } = [];

    /// <summary>Windows' history is being read and nothing of it is in hand yet.</summary>
    public bool Loading { get; private set; }

    /// <summary>
    /// Why Windows' own history is not the one shown, in words for the person; null while it is.
    /// </summary>
    public string? Note => note switch
    {
        Why.Off => HistoryOffWords,
        Why.Unread => HistoryUnread,
        _ => null,
    };

    /// <summary>Which of the two <see cref="Note"/> says, kept rather than its words, which follow the language.</summary>
    private enum Why
    {
        None,
        Off,
        Unread,
    }

    private Why note;

    /// <summary>Whether Karpo's own history shows because Windows' is off, which its settings turn on.</summary>
    public bool HistoryOff => note == Why.Off;

    /// <summary>Whether the items can be pinned: Karpo's own can; Windows gives a program no way to pin.</summary>
    public bool CanPin => Source == ClipSource.Karpo;

    /// <summary>Whether Karpo is hearing the clipboard: a clipboard widget stands somewhere.</summary>
    internal bool Listening { get; private set; }

    /// <summary>
    /// A clipboard widget stands on a bar: it is woken when the history changes, and the first one
    /// starts the clipboard being heard, which goes on until the last one goes (§11: a widget's data
    /// is gathered from the dock's start, not from the first look at it).
    /// </summary>
    public void Attach(CellIsland island)
    {
        signal.Add(island);
        if (watchers.Add(island) && watchers.Count == 1)
        {
            Listen(true);
        }
    }

    /// <summary>That widget is gone; with the last one, the clipboard is not heard any more.</summary>
    public void Release(CellIsland island)
    {
        signal.Remove(island);
        if (watchers.Remove(island) && watchers.Count == 0)
        {
            Listen(false);
        }
    }

    /// <summary>Starts or stops hearing the clipboard (for a test, without a widget).</summary>
    internal void Listen(bool on)
    {
        if (Listening == on)
        {
            return;
        }
        Listening = on;
        windowsOn = on && system.HistoryEnabled;
        system.Listen(on ? Copied : null);
    }

    /// <summary>On the dock's thread: raises <see cref="Changed"/> once for all news since the last time.</summary>
    internal void Deliver() => signal.Deliver();

    /// <summary>The reading in flight, for tests.</summary>
    internal Task Idle()
    {
        lock (gate)
        {
            return working;
        }
    }

    /// <summary>
    /// The clipboard changed, on the dock's thread. Where Windows' own history is kept, Windows has
    /// it, and nothing is read; otherwise what is on the clipboard is read on the pool and kept
    /// (<see cref="Heard"/>).
    /// </summary>
    internal void Copied()
    {
        windowsOn = system.HistoryEnabled;
        if (windowsOn && !windowsFailed)
        {
            return;
        }
        lock (gate)
        {
            working = working.ContinueWith(_ => Troubles.Guard("Clipboard: reading what was copied", () => Heard(system.Read())), TaskScheduler.Default);
        }
    }

    /// <summary>
    /// What was on the clipboard at a change, kept in Karpo's own history unless a marker keeps it
    /// out (ClipboardRing.Add), its picture made into a thumbnail there and then.
    /// </summary>
    internal void Heard(ClipSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }
        lock (gate)
        {
            if (!ring.Add(snapshot))
            {
                return;
            }
            var top = ring.Items[0];
            if (top.Kind == ClipKind.Image && !thumbnails.ContainsKey(top.Id))
            {
                thumbnails[top.Id] = DibPictures.Thumbnail(top.Dib, ThumbnailWidth, ThumbnailHeight);
            }
            Forget();
            if (Source == ClipSource.Karpo)
            {
                Items = Own();
            }
        }
        signal.Notify();
    }

    /// <summary>
    /// The folder showing the line opens: Windows' history read afresh where it is on, and Karpo's own shown
    /// otherwise — or while Windows' would not be given, saying why (<see cref="Note"/>).
    /// </summary>
    public void Refresh()
    {
        windowsOn = system.HistoryEnabled;
        if (!windowsOn)
        {
            lock (gate)
            {
                Source = ClipSource.Karpo;
                note = Why.Off;
                Loading = false;
                Items = Own();
            }
            return;
        }
        lock (gate)
        {
            Loading = Source != ClipSource.Windows || Items.Count == 0;
            working = working.ContinueWith(_ => Troubles.Guard("Clipboard: reading Windows' history", ReadWindows), TaskScheduler.Default).Unwrap();
        }
    }

    /// <summary>What the line says while Windows' own history is off.</summary>
    internal static string HistoryOffWords => S.DockClipboardHistoryOff;

    /// <summary>What it says where Windows' history is on but would not be read.</summary>
    internal static string HistoryUnread => S.DockClipboardHistoryUnread;

    private async Task ReadWindows()
    {
        IReadOnlyList<ClipView>? read;
        try
        {
            read = await system.ReadHistoryAsync(ThumbnailWidth, ThumbnailHeight).WaitAsync(HistoryWait).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Not answered in time, or failed on its way: as if Windows would not give it.
            Karpo.Core.Troubles.Write($"Clipboard history: not read ({error.GetType().Name})");
            read = null;
        }
        lock (gate)
        {
            Loading = false;
            windowsFailed = read is null;
            if (read is null)
            {
                Source = ClipSource.Karpo;
                note = Why.Unread;
                Items = Own();
            }
            else
            {
                Source = ClipSource.Windows;
                note = Why.None;
                Items = read;
            }
        }
        signal.Notify();
    }

    /// <summary>
    /// That item on the clipboard again, as a click on it in Win+V does — Windows' through its own
    /// history, Karpo's through the clipboard; false where it could not be.
    /// </summary>
    public bool Choose(ClipView item)
    {
        if (Source == ClipSource.Windows)
        {
            return system.Restore(item.Id);
        }
        ClipEntry? entry;
        lock (gate)
        {
            entry = long.TryParse(item.Id, out var id) ? ring.Find(id) : null;
        }
        return entry is not null && system.Put(entry);
    }

    /// <summary>Pins that item of Karpo's own history, or unpins it: it stays through "Clear all".</summary>
    public void Pin(ClipView item, bool pinned)
    {
        if (Source != ClipSource.Karpo || !long.TryParse(item.Id, out var id))
        {
            return;
        }
        lock (gate)
        {
            if (ring.Pin(id, pinned))
            {
                Items = Own();
            }
        }
    }

    /// <summary>Takes that item out of the history it is in.</summary>
    public void Delete(ClipView item)
    {
        lock (gate)
        {
            if (Source == ClipSource.Windows)
            {
                if (system.Delete(item.Id))
                {
                    Items = [.. Items.Where(other => other.Id != item.Id)];
                }
                return;
            }
            if (long.TryParse(item.Id, out var id) && ring.Delete(id))
            {
                Forget();
                Items = Own();
            }
        }
    }

    /// <summary>Everything but the pinned items, as Win+V's "Clear all"; Windows' history is read again after.</summary>
    public void ClearAll()
    {
        if (Source == ClipSource.Windows)
        {
            _ = system.ClearHistory();
            Refresh();
            return;
        }
        lock (gate)
        {
            ring.Clear();
            Forget();
            Items = Own();
        }
    }

    /// <summary>Karpo's own history as the line shows it. Under the gate.</summary>
    private List<ClipView> Own() =>
        [.. ring.Items.Select(entry => new ClipView(
            entry.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            entry.Kind,
            entry.Text,
            entry.Kind == ClipKind.Image ? thumbnails.GetValueOrDefault(entry.Id) : null,
            entry.Pinned))];

    /// <summary>The thumbnails of items that have gone. Under the gate.</summary>
    private void Forget()
    {
        foreach (var id in thumbnails.Keys.Where(id => ring.Find(id) is null).ToList())
        {
            thumbnails.Remove(id);
        }
    }

    /// <summary>
    /// For tests: Karpo's own history as it stands, item by item, with what each holds — what the
    /// line never shows but a test of what is kept must see.
    /// </summary>
    internal IReadOnlyList<ClipEntry> Kept()
    {
        lock (gate)
        {
            return [.. ring.Items];
        }
    }
}
