// What must hold of a running Karpo, measured against how it stood before the run (the baseline):
// the same dock still running and answering; on every display that had a bar, a bar on the same
// edge, as thick as before at the display's scale, the full length of the edge, inside the display,
// and the work area still reserved for it; Explorer's taskbar never shown (§465); no bar over
// a full screen app; no bar left on no display. Each broken rule is a line with a key, so a watcher
// can say how long it stayed broken.

namespace Karpo.Resilience;

internal enum Edge
{
    None,
    Bottom,
    Top,
    Left,
    Right,
}

/// <summary>How a display's bar stands: its edge, its window's depth, the strip the work area leaves it, whether it runs the full edge, at what scale.</summary>
internal sealed record BarStand(Edge Edge, int Window, int Strip, bool Full, uint Dpi);

internal sealed record Baseline(int? Dock, int? Guard, Dictionary<string, BarStand> Stands, Snapshot Seen)
{
    /// <summary>What a display that is new to the run should look like: the primary's stand, else any.</summary>
    public BarStand? Typical => Stands.Values.FirstOrDefault();

    public static Baseline From(Snapshot snapshot)
    {
        var stands = new Dictionary<string, BarStand>();
        foreach (var display in snapshot.Displays)
        {
            if (Rules.StandOf(snapshot, display) is { } stand)
            {
                stands[display.Device] = stand;
            }
        }
        return new Baseline(snapshot.Dock, snapshot.Guard, stands, snapshot);
    }
}

internal readonly record struct Broken(string Key, string Says)
{
    public override string ToString() => Says;
}

internal static class Rules
{
    /// <summary>How the bar stands on <paramref name="display"/>: the widest bar touching an edge, and whether the work area leaves its room.</summary>
    public static BarStand? StandOf(Snapshot snapshot, DisplayFacts display)
    {
        BarStand? best = null;
        var bestLength = 0;
        foreach (var bar in snapshot.BarsOn(display.Device))
        {
            var (edge, thickness, length) = EdgeOf(bar.Rect, display.Bounds);
            if (edge == Edge.None || length <= bestLength)
            {
                continue;
            }
            bestLength = length;
            var full = edge is Edge.Bottom or Edge.Top ? length >= display.Bounds.Width - 2 : length >= display.Bounds.Height - 2;
            // The bar's window is taller than the bar (it keeps room for what opens over it), so the
            // bar's own thickness is read where it counts: the strip the work area leaves on its edge.
            var strip = edge switch
            {
                Edge.Bottom => display.Bounds.Bottom - display.Work.Bottom,
                Edge.Top => display.Work.Top - display.Bounds.Top,
                Edge.Left => display.Work.Left - display.Bounds.Left,
                Edge.Right => display.Bounds.Right - display.Work.Right,
                _ => 0,
            };
            best = new BarStand(edge, thickness, strip, full, display.Dpi);
        }
        return best;
    }

    /// <summary>The display edge <paramref name="bar"/> lies on, how thick it is across it and how long along it.</summary>
    public static (Edge Edge, int Thickness, int Length) EdgeOf(Box bar, Box display)
    {
        var horizontal = bar.Width >= bar.Height;
        if (horizontal)
        {
            if (Math.Abs(bar.Bottom - display.Bottom) <= 1)
            {
                return (Edge.Bottom, bar.Height, bar.Width);
            }
            if (Math.Abs(bar.Top - display.Top) <= 1)
            {
                return (Edge.Top, bar.Height, bar.Width);
            }
        }
        else
        {
            if (Math.Abs(bar.Left - display.Left) <= 1)
            {
                return (Edge.Left, bar.Width, bar.Height);
            }
            if (Math.Abs(bar.Right - display.Right) <= 1)
            {
                return (Edge.Right, bar.Width, bar.Height);
            }
        }
        return (Edge.None, 0, 0);
    }

    /// <summary>A window in front that covers the whole display it is on, and is no shell or Karpo window.</summary>
    public static bool FullScreen(WindowSeen? front, DisplayFacts display) =>
        front is { Visible: true, Cloaked: false } window
        && window.Display == display.Device
        && window.Rect.Contains(display.Bounds)
        && window.Class is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" or Probe.BarClass)
        && !window.Class.StartsWith("karpo.", StringComparison.Ordinal);

    /// <summary>
    /// How long a rule may stay broken before it is a failure even though it held again in the end:
    /// the bars and the work areas are let settle (a change of display takes them up to a second);
    /// a popup left behind has a second (a fade, a close that waits for the pointer to settle); a bar
    /// over the apps half of one (an animation); the foreground taken or an app started, none.
    /// Null: judged only by whether it held at the end.
    /// </summary>
    public static TimeSpan? Patience(string key) => key switch
    {
        _ when key.StartsWith("popup:", StringComparison.Ordinal) => TimeSpan.FromSeconds(1),
        _ when key.StartsWith("covers:", StringComparison.Ordinal) => TimeSpan.FromMilliseconds(500),
        "focus" or "hung" => TimeSpan.Zero,
        _ when key.StartsWith("launched:", StringComparison.Ordinal) => TimeSpan.Zero,
        _ => null,
    };

    private static string Name(int pid)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(pid);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            return "ended";
        }
    }

    /// <param name="fullscreenAllowed">A display with a full screen app in front may have no bar (it made way); false only where none is expected.</param>
    public static List<Broken> Check(Baseline baseline, Snapshot now, bool fullscreenAllowed = true)
    {
        var broken = new List<Broken>();
        if (now.Dock is null)
        {
            broken.Add(new("dock", "the dock is not running"));
            return broken;
        }
        if (baseline.Dock is { } was && now.Dock != was)
        {
            broken.Add(new("restarted", $"the dock is another process ({was} → {now.Dock}): it ended and was started again"));
        }
        if (baseline.Guard is not null && now.Guard is null)
        {
            broken.Add(new("guard", "the guard is not running"));
        }
        if (now.Answers == false)
        {
            broken.Add(new("hung", "the dock did not answer a WM_NULL within 3 s"));
        }
        else if (now.Answers == true && now.AnswerMs > 1000)
        {
            broken.Add(new("slow", $"the dock took {now.AnswerMs} ms to answer a WM_NULL"));
        }
        foreach (var display in now.Displays)
        {
            var tag = display.Device.TrimStart('\\', '.');
            var bars = now.BarsOn(display.Device).ToList();
            if (FullScreen(now.Front, display))
            {
                if (bars.Any(bar => bar.Rect.Intersects(display.Bounds)) && fullscreenAllowed)
                {
                    broken.Add(new($"over-full:{tag}", $"[{tag}] a bar is shown over the full screen app {now.Front!.Class}"));
                }
                continue;
            }
            var expected = baseline.Stands.GetValueOrDefault(display.Device) ?? (baseline.Stands.Count > 0 && !baseline.Seen.Displays.Any(d => d.Device == display.Device) ? baseline.Typical : null);
            if (expected is null)
            {
                continue;
            }
            var stand = StandOf(now, display);
            if (stand is null)
            {
                broken.Add(new($"no-bar:{tag}", $"[{tag}] no bar on the display ({display.Bounds})"));
                continue;
            }
            if (stand.Edge != expected.Edge)
            {
                broken.Add(new($"edge:{tag}", $"[{tag}] the bar is on the {stand.Edge} edge, was on the {expected.Edge}"));
            }
            if (expected.Full && !stand.Full)
            {
                broken.Add(new($"short:{tag}", $"[{tag}] the bar no longer runs the full edge"));
            }
            if (expected.Strip > 0)
            {
                // The strip scales with the display: 40 DIP is 60 px at 150 %, 50 at 125 %.
                var strip = (int)Math.Round(expected.Strip * (double)display.Dpi / expected.Dpi);
                if (stand.Strip < strip - 2)
                {
                    broken.Add(new($"work:{tag}", $"[{tag}] the work area {display.Work} leaves the bar {stand.Strip} px, not {strip}: windows go under it"));
                }
                else if (stand.Strip > strip + 2)
                {
                    broken.Add(new($"work:{tag}", $"[{tag}] the work area {display.Work} leaves {stand.Strip} px, more than the bar's {strip}: a gap over the bar"));
                }
                if (stand.Window < Math.Min(stand.Strip, strip) - 1)
                {
                    broken.Add(new($"thick:{tag}", $"[{tag}] the bar's window is {stand.Window} px deep, less than its {strip} px strip"));
                }
            }
            foreach (var bar in bars.Where(bar => !display.Bounds.Contains(bar.Rect)))
            {
                broken.Add(new($"spill:{tag}", $"[{tag}] bar {bar.Handle:X} at {bar.Rect} runs off the display {display.Bounds}"));
            }
        }
        // Karpo never takes the foreground by itself: nothing here clicks it.
        if (now.Front is { } front && front.Process == now.Dock)
        {
            broken.Add(new("focus", $"Karpo's {front.Class} at {front.Rect} took the foreground: the app in front lost it"));
        }
        // A tooltip, a preview, a menu shown with the pointer nowhere on Karpo: left behind.
        if (!now.PointerOnKarpo)
        {
            foreach (var popup in now.Popups)
            {
                broken.Add(new($"popup:{popup.Class}:{popup.Title}", $"Karpo's {popup.Class} \"{popup.Title}\" is shown at {popup.Rect} with the pointer at {now.Cursor.X},{now.Cursor.Y}, not on Karpo"));
            }
        }
        // The bar's window is taller than the bar: above the strip it must let every press through.
        foreach (var (display, x, y) in now.Covers)
        {
            broken.Add(new($"covers:{display.TrimStart('\\', '.')}", $"[{display.TrimStart('\\', '.')}] a bar's window takes the pointer at {x},{y}, above its strip, over the apps"));
        }
        // Karpo opens nothing by itself.
        foreach (var child in now.Launched)
        {
            broken.Add(new($"launched:{child}", $"Karpo started a process of its own accord ({child}: {Name(child)})"));
        }
        foreach (var bar in now.Bars.Where(bar => bar.Display is null && bar.Visible && !bar.Cloaked && !bar.Rect.Empty))
        {
            broken.Add(new($"lost:{bar.Handle:X}", $"bar {bar.Handle:X} is shown at {bar.Rect}, on no display"));
        }
        // §465: while Karpo runs, Windows' taskbar never shows, on any display.
        foreach (var bar in now.Taskbars.Where(bar => bar.InSight && bar.Display is { } on && !FullScreen(now.Front, now.DisplayOf(on)!)))
        {
            var tag = bar.Display!.TrimStart('\\', '.');
            broken.Add(new($"explorer:{tag}", $"[{tag}] Explorer's taskbar ({bar.Class}) is shown at {bar.Rect}"));
        }
        return broken;
    }
}
