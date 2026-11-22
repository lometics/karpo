// Looks again and again, and remembers each rule broken: when it broke, when it held again, and
// what the probe said the first time — so a report can say "the work area on DISPLAY2 was left
// unreserved for 840 ms after the resolution change", not only whether it ended well.

using System.Diagnostics;
using System.Text;

namespace Karpo.Resilience;

internal sealed class Watcher(Baseline baseline)
{
    public sealed class Break(string key, string says, TimeSpan from)
    {
        public string Key { get; } = key;
        public string Says { get; } = says;
        public TimeSpan From { get; } = from;
        public TimeSpan? To { get; set; }
        public TimeSpan Lasted(TimeSpan now) => (To ?? now) - From;
    }

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Dictionary<string, Break> open = [];

    public List<Break> Breaks { get; } = [];

    public Snapshot? Last { get; private set; }

    public TimeSpan Now => clock.Elapsed;

    /// <summary>One look; true when every rule holds.</summary>
    public bool Look(bool fullscreenAllowed = true)
    {
        var snapshot = Probe.Take();
        Last = snapshot;
        var broken = Rules.Check(baseline, snapshot, fullscreenAllowed);
        var now = clock.Elapsed;
        var keys = broken.Select(b => b.Key).ToHashSet();
        foreach (var (key, was) in open.ToList())
        {
            if (!keys.Contains(key))
            {
                was.To = now;
                open.Remove(key);
            }
        }
        foreach (var rule in broken)
        {
            if (!open.ContainsKey(rule.Key))
            {
                var made = new Break(rule.Key, rule.Says, now);
                open[rule.Key] = made;
                Breaks.Add(made);
            }
        }
        return broken.Count == 0;
    }

    /// <summary>The rules broken right now.</summary>
    public IReadOnlyCollection<Break> Open => open.Values;

    /// <summary>
    /// Looks until every rule has held for <paramref name="steady"/> on end, or <paramref name="limit"/>
    /// passes. How long it took to hold (from the call), or null when it never did.
    /// </summary>
    public TimeSpan? Settle(TimeSpan limit, TimeSpan steady, bool fullscreenAllowed = true)
    {
        var start = clock.Elapsed;
        TimeSpan? heldSince = null;
        while (clock.Elapsed - start < limit)
        {
            if (Look(fullscreenAllowed))
            {
                heldSince ??= clock.Elapsed;
                if (clock.Elapsed - heldSince >= steady)
                {
                    return heldSince - start;
                }
            }
            else
            {
                heldSince = null;
            }
            Thread.Sleep(100);
        }
        return null;
    }

    /// <summary>Looks for <paramref name="span"/> regardless (a scenario's hold-still part).</summary>
    public void For(TimeSpan span, bool fullscreenAllowed = true)
    {
        var start = clock.Elapsed;
        while (clock.Elapsed - start < span)
        {
            Look(fullscreenAllowed);
            Thread.Sleep(100);
        }
    }

    public string Summary()
    {
        var text = new StringBuilder();
        if (Breaks.Count == 0)
        {
            text.Append("no rule broken");
            return text.ToString();
        }
        foreach (var made in Breaks)
        {
            var lasted = made.Lasted(clock.Elapsed);
            text.AppendLine($"  {(made.To is null ? "STILL " : "")}broken at +{made.From.TotalSeconds:0.0} s for {lasted.TotalMilliseconds:0} ms: {made.Says}");
        }
        return text.ToString().TrimEnd();
    }
}
