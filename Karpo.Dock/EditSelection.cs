// edit: the widgets selected while the bar is arranged (the person, 2026-10-07): "Ctrl+click and
// Shift+click, plus a rubber-band drag on empty frame space; selected widgets show the selection look;
// dragging one drags all of them together; Delete and Remove act on all of them". The rules, apart
// from any window, so they can be tested; the dock keeps one and acts on it (Dock.EditSelect.cs).
//
//  - **A click** on a widget selects it alone, as before (EditClicks: its page opens too).
//  - **Ctrl+click** adds it to the selection, or takes it out again; its page does not open.
//  - **Shift+click** selects the run from the widget last clicked to this one, in the display's own
//    order (its list: every edge's places, each in its sections' order), as Windows' lists do; a
//    Shift+click with nothing selected, or on another display, selects the widget alone.
//  - **A drag on the dim** selects every widget its rectangle runs over, on the bars
//    and places of that display, what was selected before kept with Ctrl or Shift held.
//
// The selection is kept by key ("display:<id>/<id>", Config.IslandKey), so it keeps to its widgets
// through every rebuild; one gone from the file is dropped. The widget last clicked is the anchor: a
// Shift+click runs from it, and a folder standing open rings it (FolderFlyout.Picked).
//
// **Scoped** (the person, 2026-10-12: "when a folder is open the lasso select should always only
// select widgets from the folder, everywhere actually"; "when the folder is closed the same should be
// true but for the taskbar widgets"): the selection never holds a bar's widgets and a folder's lines
// together (EditScope). While a folder stands open arranged, a drag begun anywhere — its own room,
// a bar's, the dim of any display — selects that folder's lines alone; with none open, the bars'
// widgets alone. A Ctrl+ or Shift+click on one of the other kind starts the selection over among its
// kind; a folder opening keeps of the bar's only the folder itself (the one clicked, which wears its
// ring), and closing drops its lines.

namespace Karpo.Dock;

/// <summary>Which widgets may be selected together (see the top of the file): a bar's, or one folder's lines.</summary>
public static class EditScope
{
    /// <summary>
    /// The folder a line's key ("&lt;owner&gt;/&lt;folder id&gt;/&lt;id&gt;") stands in, its key
    /// "&lt;owner&gt;/&lt;folder id&gt;"; null for a widget of a bar ("&lt;owner&gt;/&lt;id&gt;"). An
    /// owner and an id hold no '/' (Config.IslandKey).
    /// </summary>
    public static string? FolderOf(string key)
    {
        var first = key.IndexOf('/');
        var last = key.LastIndexOf('/');
        return first >= 0 && last > first ? key[..last] : null;
    }

    /// <summary>
    /// Whether <paramref name="key"/> may be selected while the folder keyed <paramref name="open"/>
    /// stands open arranged: its lines alone; with none open (null), a bar's widgets alone.
    /// </summary>
    public static bool In(string key, string? open) =>
        string.Equals(FolderOf(key), open, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> may be selected together: both a bar's, or both one folder's lines.</summary>
    public static bool Together(string a, string b) => In(a, FolderOf(b));

    /// <summary>Whether <paramref name="key"/> stays selected as the folder keyed <paramref name="folder"/> opens arranged: its lines, and the folder itself, the one clicked.</summary>
    public static bool StaysOpening(string key, string folder) =>
        In(key, folder) || string.Equals(key, folder, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="key"/> stays selected as the folder keyed <paramref name="folder"/> closes: all but its lines.</summary>
    public static bool StaysClosing(string key, string folder) => !In(key, folder);
}

/// <summary>The widgets selected while the bar is arranged (see the top of the file).</summary>
public sealed class EditSelection
{
    private readonly List<string> keys = [];

    /// <summary>The selected widgets' keys, in the order they were selected.</summary>
    public IReadOnlyList<string> Keys => keys;

    /// <summary>The widget last clicked, which a Shift+click runs from; null with none.</summary>
    public string? Anchor { get; private set; }

    public int Count => keys.Count;

    public bool Contains(string? key) => key is not null && keys.Contains(key, StringComparer.OrdinalIgnoreCase);

    /// <summary>A click: <paramref name="key"/> alone, the anchor; none for null.</summary>
    public void Only(string? key)
    {
        keys.Clear();
        Anchor = key;
        if (key is not null)
        {
            keys.Add(key);
        }
    }

    /// <summary>Nothing selected.</summary>
    public void Clear() => Only(null);

    /// <summary>
    /// Ctrl+click: <paramref name="key"/> into the selection, or out of it again. Into it, it is the
    /// anchor; out of it, the anchor is the one selected last before it, or none.
    /// </summary>
    public void Toggle(string key)
    {
        // Never a bar's widget and a folder's line together: one of the other kind starts over (EditScope).
        Keep(each => EditScope.Together(each, key));
        var at = keys.FindIndex(each => string.Equals(each, key, StringComparison.OrdinalIgnoreCase));
        if (at >= 0)
        {
            keys.RemoveAt(at);
            if (string.Equals(Anchor, key, StringComparison.OrdinalIgnoreCase))
            {
                Anchor = keys.Count > 0 ? keys[^1] : null;
            }
            return;
        }
        keys.Add(key);
        Anchor = key;
    }

    /// <summary>
    /// Shift+click: every widget of <paramref name="order"/> (the display's keys in its list's order)
    /// from the anchor to <paramref name="key"/>, both in; the anchor stays, so another Shift+click
    /// runs from it again. Without an anchor in <paramref name="order"/>, <paramref name="key"/> alone.
    /// </summary>
    public void Range(string key, IReadOnlyList<string> order)
    {
        int Index(string? of) => of is null ? -1 : order.ToList().FindIndex(each => string.Equals(each, of, StringComparison.OrdinalIgnoreCase));
        var from = Index(Anchor);
        var to = Index(key);
        if (from < 0 || to < 0)
        {
            Only(key);
            return;
        }
        var anchor = Anchor;
        keys.Clear();
        for (var index = Math.Min(from, to); index <= Math.Max(from, to); index++)
        {
            keys.Add(order[index]);
        }
        Anchor = anchor;
    }

    /// <summary>
    /// A drag over the dim: <paramref name="picked"/>, with <paramref name="kept"/> — what was
    /// selected when it began, where Ctrl or Shift was held — before them. The anchor is the first
    /// picked, or the one kept.
    /// </summary>
    public void Set(IEnumerable<string> picked, IReadOnlyList<string> kept)
    {
        var anchor = Anchor;
        keys.Clear();
        foreach (var key in kept.Concat(picked))
        {
            if (!Contains(key))
            {
                keys.Add(key);
            }
        }
        Anchor = kept.Count > 0 && Contains(anchor) ? anchor : keys.Count > 0 ? keys[0] : null;
    }

    /// <summary>Those <paramref name="exists"/> says are gone from the file are dropped, the anchor with them.</summary>
    public void Keep(Func<string, bool> exists)
    {
        keys.RemoveAll(key => !exists(key));
        if (Anchor is not null && !exists(Anchor))
        {
            Anchor = keys.Count > 0 ? keys[^1] : null;
        }
    }

    /// <summary>
    /// §519: widgets moved to another display's list go by new keys (<paramref name="renamed"/>, old to
    /// new): those selected stay selected under them, in their place, the anchor too.
    /// </summary>
    public void Rename(IReadOnlyDictionary<string, string> renamed)
    {
        string To(string key) => renamed.FirstOrDefault(each => string.Equals(each.Key, key, StringComparison.OrdinalIgnoreCase)).Value ?? key;
        for (var index = 0; index < keys.Count; index++)
        {
            keys[index] = To(keys[index]);
        }
        Anchor = Anchor is null ? null : To(Anchor);
    }

    /// <summary>The selected among <paramref name="order"/> (a display's keys, or a bar's), in its order.</summary>
    public List<string> InOrder(IEnumerable<string> order) => [.. order.Where(Contains)];

    /// <summary>
    /// Every selected widget: those of <paramref name="order"/> (the displays' keys) in its order, then
    /// the rest in the order they were selected — a folder's lines, which no display's order holds.
    /// </summary>
    /// <remarks>
    /// Regression: this was <see cref="InOrder"/> alone, so a line selected in a folder (a click on it
    /// selects it) was left out, and Delete or its "Remove" removed nothing.
    /// </remarks>
    public List<string> All(IEnumerable<string> order)
    {
        var first = InOrder(order);
        return [.. first, .. keys.Where(key => !first.Contains(key, StringComparer.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// What the keys act on (<see cref="ActedOn"/>'s key) where they walked the focus to <paramref name="walked"/> (null: they
    /// did not; a press put it there) and <paramref name="pressed"/> was pressed last: the widget walked to, which is every
    /// selected one where it is one of them; else every selected widget; else, with none selected, the one pressed. Never the
    /// one pressed while others are selected (the person, 2026-11-03: "when selecting multiple widgets ctrl x does not work"):
    /// a lasso or a Ctrl+click after the press selected others, and Ctrl+X cut the one pressed alone — Ctrl+clicked out of
    /// the selection, one no longer selected.
    /// </summary>
    public string? KeysOn(string? walked, string? pressed) => walked ?? (Count > 0 ? null : pressed);

    /// <summary>
    /// What Delete, "Remove" or "Copy" on <paramref name="key"/> acts on: every selected widget
    /// (<see cref="All"/>) where it is one of them, or where there is none (Delete with no widget
    /// focused); else it alone.
    /// </summary>
    public List<string> ActedOn(string? key, IEnumerable<string> order) =>
        Count > 0 && (key is null || Contains(key)) ? All(order)
        : key is not null ? [key]
        : [];
}
