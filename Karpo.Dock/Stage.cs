// Karpo.exe --stage x,y,w,h[,dpi]: the dock on a made-up display, a rectangle of a real one, for
// recording what Karpo does (scripts\stage).
//
// The bars, their flyouts and their popups stand inside the rectangle as they would on a display that
// size, at the dpi given (the display's own when none is), so a recording of that rectangle is Karpo
// as it is. It never touches what the person's own dock owns: no mutex, no appbar, no taskbar taken
// over, and only a config of its own — it refuses to run without KARPO_CONFIG_DIR. Where the islands
// are goes to stage-map.json in that folder after every build, for the script that moves the pointer.

using System.Globalization;
using System.Text;
using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Pim;

namespace Karpo.Dock;

internal static class Stage
{
    public const string Switch = "--stage";

    /// <summary>
    /// §627: the picture a stage run's desktops show, a test's own — stage-wallpaper.bmp in its config's folder —; null for
    /// none (the stage reads no wallpaper of the person's).
    /// </summary>
    public static string? Wallpaper => On && Path.Combine(Config.Folder, "stage-wallpaper.bmp") is var path && File.Exists(path) ? path : null;

    /// <summary>The made-up displays, each a rectangle in screen px at its dpi, when this is a stage run; the first the primary.</summary>
    public static IReadOnlyList<(RectI Rect, uint Dpi)> Displays { get; private set; } = [];

    public static bool On => Displays.Count > 0;

    /// <summary>Reads <c>--stage x,y,w,h[,dpi][;x,y,w,h[,dpi]…]</c>; false, with why, when it is there and wrong.</summary>
    public static bool Parse(string[] args, out string? error)
    {
        var read = Read(args, Environment.GetEnvironmentVariable("KARPO_CONFIG_DIR") is { Length: > 0 }, out error);
        if (read is { } stage)
        {
            Displays = stage;
        }
        return error is null;
    }

    /// <summary>
    /// What <c>--stage x,y,w,h[,dpi][;x,y,w,h[,dpi]…]</c> asks for, read and nothing set: a display a
    /// rectangle; null with no error when the switch isn't there, null with why when it is wrong or
    /// <paramref name="isolated"/> is false (a stage never runs on the person's own config). The dpi is 96
    /// when not given.
    /// </summary>
    internal static List<(RectI Rect, uint Dpi)>? Read(string[] args, bool isolated, out string? error)
    {
        error = null;
        var at = Array.IndexOf(args, Switch);
        if (at < 0)
        {
            return null;
        }
        if (!isolated)
        {
            error = "--stage runs only on a config of its own: set KARPO_CONFIG_DIR";
            return null;
        }
        var displays = new List<(RectI Rect, uint Dpi)>();
        foreach (var each in at + 1 < args.Length ? args[at + 1].Split(';') : [""])
        {
            var numbers = each.Split(',').Select(n => int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null).ToArray();
            if (numbers.Length is not (4 or 5) || numbers.Any(n => n is null) || numbers[2] <= 0 || numbers[3] <= 0 || (numbers.Length == 5 && numbers[4] <= 0))
            {
                error = "usage: Karpo.exe --stage x,y,width,height[,dpi][;x,y,width,height[,dpi]…]";
                return null;
            }
            var (x, y) = (numbers[0]!.Value, numbers[1]!.Value);
            displays.Add((new RectI(x, y, x + numbers[2]!.Value, y + numbers[3]!.Value), numbers.Length == 5 ? (uint)numbers[4]!.Value : 96));
        }
        return displays;
    }

    /// <summary>The displays a stage run has: each rectangle, all of it work area, with no handle of its own; the first the primary.</summary>
    public static List<MonitorInfo> Monitors() =>
        [.. Displays.Select((display, index) => index == 0
            ? new MonitorInfo("stage", "Stage", @"\\.\STAGE", true, display.Rect, display.Rect, 0, display.Dpi)
            : new MonitorInfo($"stage-{index + 1}", $"Stage {index + 1}", $@"\\.\STAGE{index + 1}", false, display.Rect, display.Rect, 0, display.Dpi))];

    /// <summary>
    /// The calendar, the inbox and the contacts a recording shows: made up, as --smoke's are, and no account asked. On
    /// the dock's thread, before its first build.
    /// </summary>
    public static void Samples()
    {
        // §635: the samples' own calendars, inbox and people (Samples.Accounts.cs), put in the dock's stores.
        foreach (var calendar in global::Karpo.Dock.Samples.Calendars())
        {
            CalendarStore.Shared.Add(calendar);
        }
        CalendarStore.Shared.Open();
        MailStore.Shared.Answered(global::Karpo.Dock.Samples.Inbox(), unread: 3);
        ContactStore.Shared.Answered(global::Karpo.Dock.Samples.People());
    }

    /// <summary>
    /// A reading for a recording: made up, as a PC at ordinary work reads — the processor and the
    /// graphics a little busy with now and then a burst, the memory a little under half full, the disk
    /// mostly idle, the network trickling with the odd download — and its whole history there from
    /// the start, so a graph is drawn edge to edge the moment it shows. Never the machine's own.
    /// </summary>
    public static PerformanceData Performance(Metric metric, string device)
    {
        var walk = new Walk(metric);
        var data = new PerformanceData(metric, device) { Reading = walk.Next };
        for (var i = 0; i <= PerformanceData.Samples; i++)
        {
            data.History.Add(walk.Next() ?? 0);
        }
        if (metric == Metric.Memory)
        {
            // 64 GB, 44 % in use: the memory line's "28.2 of 64 GB".
            const ulong total = 64UL << 30;
            data.Feed(memory: new MemoryReading(44, total, (ulong)(total * 0.56)));
        }
        return data;
    }

    /// <summary>A reading that wanders: a level, a slow swell, small noise, and now and then a burst that fades.</summary>
    private sealed class Walk(Metric metric)
    {
        private readonly Random random = new(17 + (int)metric);
        private double burst;
        private double time;

        public float? Next()
        {
            time += 1;
            var (level, swell, noise, bursts, most) = metric switch
            {
                Metric.Cpu => (0.16, 0.06, 0.05, 0.05, 0.85),
                Metric.Gpu => (0.10, 0.04, 0.03, 0.04, 0.70),
                Metric.Memory => (0.44, 0.01, 0.002, 0.0, 0.60),
                Metric.Disk => (0.02, 0.01, 0.015, 0.06, 0.60),
                _ => (300_000.0, 120_000.0, 90_000.0, 0.05, 4_000_000.0),
            };
            if (random.NextDouble() < bursts)
            {
                burst = Math.Max(burst, (most - level) * (0.4 + (0.6 * random.NextDouble())));
            }
            burst *= 0.82;
            var value = level + (swell * Math.Sin(time / 37.0)) + (noise * ((random.NextDouble() * 2) - 1)) + burst;
            return (float)Math.Clamp(value, metric == Metric.Network ? 0 : 0.005, metric == Metric.Network ? most : 1);
        }
    }

    private static readonly Dictionary<nint, string> Maps = [];

    /// <summary>One bar window's entries, as JSON objects in screen px; written out with every other bar's.</summary>
    public static void Mapped(nint window, string entries)
    {
        // Written only when it changed: the file is in the folder the dock watches, and each write
        // wakes it.
        if (Maps.TryGetValue(window, out var was) && was == entries)
        {
            return;
        }
        Maps[window] = entries;
        Write();
    }

    public static void Gone(nint window)
    {
        if (Maps.Remove(window))
        {
            Write();
        }
    }

    private static void Write()
    {
        var text = new StringBuilder("[\n");
        text.AppendJoin(",\n", Maps.Values.Where(v => v.Length > 0));
        text.Append("\n]\n");
        Write("stage-map.json", text.ToString());
    }

    private static readonly Dictionary<string, string> Written = [];

    /// <summary>
    /// §361: what edit mode shows besides the bars, as JSON objects in screen px — each edit bar's buttons
    /// in sight, an arranged folder's panel and its lines — to stage-edit.json, for a test to press on.
    /// </summary>
    public static void Edited(IEnumerable<string> entries) => Write("stage-edit.json", "[\n" + string.Join(",\n", entries) + "\n]\n");

    /// <summary>
    /// §361: the z-order plan last applied, top first, each window with its stratum (ZPlan), to
    /// stage-z.json: what a test holds the windows' order on the screen to.
    /// </summary>
    public static void Planned(IEnumerable<(nint Window, string Stratum)> plan) =>
        Write("stage-z.json", "[\n" + string.Join(",\n", plan.Select(entry => string.Create(CultureInfo.InvariantCulture, $"{{\"hwnd\":{entry.Window},\"stratum\":\"{entry.Stratum}\"}}"))) + "\n]\n");

    /// <summary><paramref name="text"/> to <paramref name="name"/> in the config's folder, where it changed (each write wakes the dock, which watches the folder).</summary>
    private static void Write(string name, string text)
    {
        if (Written.TryGetValue(name, out var was) && was == text)
        {
            return;
        }
        Written[name] = text;
        try
        {
            var path = Path.Combine(Config.Folder, name);
            File.WriteAllText(path + ".tmp", text);
            File.Move(path + ".tmp", path, overwrite: true);
        }
        catch (IOException)
        {
            // Read at that moment by the script: the next change writes it again.
            Written.Remove(name);
        }
    }

    public static string Json(string s)
    {
        var text = new StringBuilder("\"");
        foreach (var c in s)
        {
            text.Append(c switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                < ' ' => $"\\u{(int)c:x4}",
                _ => c.ToString(),
            });
        }
        return text.Append('"').ToString();
    }
}
