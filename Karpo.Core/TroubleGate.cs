// How often one trouble may be written to karpo.log (Troubles.cs).
//
// A failure that repeats (a redraw that fails on every frame, a service that fails on every
// tick) must neither fill the log nor push out what came before it. So a trouble writes its
// first line, and after that at most one line a window, which says how many more there were in
// between; and however many different troubles there are, no more than a burst of lines a window
// in all. A trouble met while the burst is spent is remembered all the same, and its first line
// comes as soon as the next window allows. Only what never happens again after being held back
// goes unreported: its first line was written, and the rest were the same.
//
// Pure: the time comes in with each question, so the tests can hold the whole table without
// waiting a minute.

namespace Karpo.Core;

internal sealed class TroubleGate
{
    private readonly TimeSpan window;
    private readonly int burst;
    private readonly int remembered;
    private readonly Dictionary<string, Seen> seen = new(StringComparer.Ordinal);
    private DateTime spentSince = DateTime.MinValue;
    private int spent;

    /// <param name="window">How long a trouble stays quiet after a line of its own, and how long the burst lasts.</param>
    /// <param name="burst">The most lines written in one window, all troubles together.</param>
    /// <param name="remembered">The most troubles kept in mind; past that the one written longest ago is forgotten.</param>
    public TroubleGate(TimeSpan window, int burst, int remembered = 64)
    {
        this.window = window;
        this.burst = Math.Max(burst, 1);
        this.remembered = Math.Max(remembered, 1);
    }

    /// <summary>How many troubles are kept in mind now.</summary>
    public int Remembered => seen.Count;

    /// <summary>
    /// Whether a line for <paramref name="key"/> may be written at <paramref name="now"/>, and when it
    /// may, how many of the same were held back since its last line.
    /// </summary>
    public (bool Write, int HeldBack) Let(string key, DateTime now)
    {
        if (now - spentSince >= window)
        {
            spentSince = now;
            spent = 0;
        }
        if (!seen.TryGetValue(key, out var trouble))
        {
            if (seen.Count >= remembered)
            {
                Forget();
            }
            // Never written: whatever quiet a written line earns, it hasn't earned it.
            trouble = new Seen { Written = DateTime.MinValue };
            seen[key] = trouble;
        }
        if (now - trouble.Written < window || spent >= burst)
        {
            trouble.HeldBack++;
            return (false, 0);
        }
        var held = trouble.HeldBack;
        trouble.Written = now;
        trouble.HeldBack = 0;
        spent++;
        return (true, held);
    }

    /// <summary>The trouble whose last line is oldest goes; one never written counts as oldest.</summary>
    private void Forget()
    {
        string? oldest = null;
        var at = DateTime.MaxValue;
        foreach (var (key, trouble) in seen)
        {
            if (trouble.Written < at)
            {
                at = trouble.Written;
                oldest = key;
            }
        }
        if (oldest is not null)
        {
            seen.Remove(oldest);
        }
    }

    private sealed class Seen
    {
        public DateTime Written;
        public int HeldBack;
    }
}
