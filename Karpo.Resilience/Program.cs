// Karpo.Resilience: the live harness of docs/resilience.md.
//
//   probe                      what Karpo looks like now, and whether it holds against itself
//   watch <seconds>            a check every 100 ms; every moment a rule was broken, and for how long
//   run <scenario>... [--cycles n] [--out dir]
//                              each scenario against a baseline taken first; a report in artifacts\resilience
//   list                       the scenarios
//   restore                    the displays as they were before the last run (if a run was cut short)
//   window <kind> <display>    (internal) a test window: borderless, exclusive, maximized, hung, titles, progress, flood
//   burn <s> / hog <MB> <s>    (internal) every core busy / the memory nearly gone

using System.Text;

namespace Karpo.Resilience;

internal static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Length == 0)
        {
            Console.WriteLine("usage: probe | watch <s> | run <scenario>... [--cycles n] | list | restore");
            return 2;
        }
        try
        {
            return args[0] switch
            {
                "probe" => ProbeNow(),
                "watch" => Watch(args.Length > 1 ? int.Parse(args[1]) : 30),
                "list" => ListScenarios(),
                "run" => Runner.Run(args[1..]),
                "restore" => Restore(),
                "window" => TestWindow.Run(args[1..]),
                "burn" => Load.Burn(int.Parse(args[1])),
                "hog" => Load.Hog(int.Parse(args[1]), int.Parse(args[2])),
                _ => throw new ArgumentException($"unknown command {args[0]}"),
            };
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static int ProbeNow()
    {
        var snapshot = Probe.Take();
        Console.WriteLine(Describe(snapshot));
        var broken = Rules.Check(Baseline.From(snapshot), snapshot, fullscreenAllowed: true);
        Console.WriteLine(broken.Count == 0 ? "holds against itself" : string.Join(Environment.NewLine, broken.Select(b => "BROKEN " + b)));
        return broken.Count == 0 ? 0 : 1;
    }

    public static string Describe(Snapshot snapshot)
    {
        var text = new StringBuilder();
        text.AppendLine($"at {snapshot.At:HH:mm:ss.fff}  dock {snapshot.Dock?.ToString() ?? "NONE"}  guard {snapshot.Guard?.ToString() ?? "none"}  answers {snapshot.Answers?.ToString() ?? "-"} ({snapshot.AnswerMs} ms)");
        text.AppendLine($"holds: {snapshot.Handles} handles, {snapshot.Gdi} GDI, {snapshot.User} USER, {snapshot.PrivateBytes / 1048576.0:0.0} MB private, {snapshot.Threads} threads");
        foreach (var display in snapshot.Displays)
        {
            text.AppendLine($"{display.Device}{(display.Primary ? " (primary)" : "")} {display.Bounds} work {display.Work} dpi {display.Dpi}");
            foreach (var bar in snapshot.Bars.Where(bar => bar.Display == display.Device))
            {
                text.AppendLine($"    bar {bar.Handle:X} {bar.Rect}{(bar.Visible ? "" : " hidden")}{(bar.Cloaked ? " cloaked" : "")}{(bar.Topmost ? " topmost" : "")}");
            }
            foreach (var bar in snapshot.Taskbars.Where(bar => bar.Display == display.Device))
            {
                text.AppendLine($"    explorer {bar.Class} {bar.Rect}{(bar.Visible ? " SHOWN" : " hidden")}{(bar.Blank ? " blank" : "")}");
            }
        }
        var lost = snapshot.Bars.Where(bar => bar.Display is null).ToList();
        foreach (var bar in lost)
        {
            text.AppendLine($"    bar on no display {bar.Handle:X} {bar.Rect}{(bar.Visible ? " visible" : "")}");
        }
        if (snapshot.Front is { } front)
        {
            text.AppendLine($"front: {front.Class} {front.Rect} on {front.Display}");
        }
        return text.ToString().TrimEnd();
    }

    private static int Watch(int seconds)
    {
        var baseline = Baseline.From(Probe.Take());
        var watcher = new Watcher(baseline);
        var end = DateTime.Now.AddSeconds(seconds);
        while (DateTime.Now < end)
        {
            watcher.Look();
            Thread.Sleep(100);
        }
        Console.WriteLine(watcher.Summary());
        return watcher.Breaks.Count == 0 ? 0 : 1;
    }

    private static int ListScenarios()
    {
        foreach (var scenario in Scenarios.All)
        {
            Console.WriteLine($"{scenario.Name,-26} {scenario.Says}");
        }
        return 0;
    }

    private static int Restore()
    {
        Console.WriteLine(Displays.RestoreSaved());
        Displays.Forget();
        return 0;
    }
}
