// What a rebuild of the bars keeps by key for the next one (app icons, pin
// group pictures), and closes when nobody uses it any more.
//
// Composition surfaces hold memory the GC doesn't see: dropped without being
// closed, each one keeps its pixels until a collection, and the finalizer
// after it, happen to come round, which in a process that allocates as little
// as the dock can be hours. So what a cache lets go of is closed by hand. Not
// at once, though: a scene built before the last rebuild may still show it (a
// bar whose build failed keeps its old scene), so what went unused is set aside
// and closed only when a whole rebuild more has passed without anyone asking
// for it again.

namespace Karpo.Dock;

/// <summary>
/// Values kept by key from one rebuild to the next: <see cref="TryGet"/> and <see cref="Add"/> mark what a
/// build uses; <see cref="Sweep"/>, after every bar was built, sets aside what went unused and closes what
/// was set aside by the sweep before and not taken back since. Used on the dock's thread only.
/// </summary>
internal sealed class Kept<TKey, TValue>
    where TKey : notnull
    where TValue : class, IDisposable
{
    private readonly Dictionary<TKey, TValue> live = [];
    private readonly HashSet<TKey> used = [];
    private Dictionary<TKey, TValue> aside = [];

    /// <summary>How many values are kept, set aside ones included.</summary>
    public int Count => live.Count + aside.Count;

    /// <summary>The value kept for <paramref name="key"/>, marked used; one set aside comes back.</summary>
    public bool TryGet(TKey key, out TValue value)
    {
        if (live.TryGetValue(key, out var found) || (aside.Remove(key, out found) && live.TryAdd(key, found)))
        {
            used.Add(key);
            value = found;
            return true;
        }
        value = null!;
        return false;
    }

    /// <summary>Keeps <paramref name="value"/> for <paramref name="key"/>, marked used; what was kept for it before is closed.</summary>
    public void Add(TKey key, TValue value)
    {
        if (live.Remove(key, out var old) && !ReferenceEquals(old, value))
        {
            old.Dispose();
        }
        if (aside.Remove(key, out var setAside) && !ReferenceEquals(setAside, value))
        {
            setAside.Dispose();
        }
        live[key] = value;
        used.Add(key);
    }

    /// <summary>
    /// After a rebuild: closes what the last sweep set aside and nobody took back, sets aside what this
    /// rebuild didn't use, and starts counting again. Returns how many values it closed.
    /// </summary>
    public int Sweep()
    {
        var closed = aside.Count;
        foreach (var value in aside.Values)
        {
            value.Dispose();
        }
        var next = new Dictionary<TKey, TValue>();
        foreach (var (key, value) in live)
        {
            if (!used.Contains(key))
            {
                next[key] = value;
            }
        }
        foreach (var key in next.Keys)
        {
            live.Remove(key);
        }
        aside = next;
        used.Clear();
        return closed;
    }

    /// <summary>Closes everything, as the dock goes.</summary>
    public void Clear()
    {
        foreach (var value in live.Values)
        {
            value.Dispose();
        }
        foreach (var value in aside.Values)
        {
            value.Dispose();
        }
        live.Clear();
        aside.Clear();
        used.Clear();
    }
}
