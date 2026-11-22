// Runs scenarios against one baseline and writes what happened: per scenario, whether it passed,
// every rule broken on the way and for how long, what Karpo wrote to its logs meanwhile, and what the
// dock held before and after (a leak shows as a climb over cycles).

using System.Text;

namespace Karpo.Resilience;

internal static class Runner
{
    private static readonly string Logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Karpo");

    /// <summary>Where this run's report (and a scenario's pictures) go.</summary>
    public static string Output { get; private set; } = Path.Combine(Root(), "artifacts", "resilience");

    /// <summary>The scenarios that open something on the person's screen or take the machine away: only ever run by name.</summary>
    private static readonly string[] ByNameOnly = ["sleep", "hibernate", "start-placement"];

    public static int Run(string[] args)
    {
        var names = new List<string>();
        var cycles = 1;
        var output = Path.Combine(Root(), "artifacts", "resilience");
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--cycles")
            {
                cycles = int.Parse(args[++i]);
            }
            else if (args[i] == "--keys")
            {
                StartScenario.WithKeys = true;
            }
            else if (args[i] == "--out")
            {
                output = args[++i];
            }
            else
            {
                names.Add(args[i]);
            }
        }
        var chosen = names.Contains("all") ? Scenarios.All.Where(s => !ByNameOnly.Contains(s.Name)).ToList()
            : names.Select(name => Scenarios.All.FirstOrDefault(s => s.Name == name) ?? throw new ArgumentException($"no scenario {name}")).ToList();

        // A run cut short before left its displays changed: back first.
        Output = output;
        Console.WriteLine(Displays.RestoreSaved());
        Displays.Forget();
        Displays.Save();
        var start = Probe.Take();
        var baseline = Baseline.From(start);
        var against = Rules.Check(baseline, start);
        var report = new StringBuilder();
        report.AppendLine($"# Karpo resilience run {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        report.AppendLine("Baseline:");
        report.AppendLine("```");
        report.AppendLine(Program.Describe(start));
        report.AppendLine("```");
        if (baseline.Dock is null || baseline.Stands.Count == 0 || against.Count > 0)
        {
            report.AppendLine($"Baseline unusable: {(baseline.Dock is null ? "Karpo is not running" : baseline.Stands.Count == 0 ? "no bar on any display" : string.Join("; ", against))}");
            Console.WriteLine(report);
            return 1;
        }
        var failures = 0;
        var results = new List<(string Name, int Cycle, string Verdict)>();
        foreach (var scenario in chosen)
        {
            for (var cycle = 1; cycle <= cycles; cycle++)
            {
                Console.WriteLine($"== {scenario.Name}{(cycles > 1 ? $" ({cycle}/{cycles})" : "")}: {scenario.Says}");
                var logMarks = Marks();
                var before = Probe.Take();
                var othersBefore = Native.OthersWindows(before.Dock);
                var watcher = new Watcher(baseline);
                var run = new Run(watcher, baseline);
                string verdict;
                try
                {
                    scenario.Body(run);
                    verdict = run.Failed.Count == 0 ? "PASS" : "FAIL";
                }
                catch (SkipException skip)
                {
                    verdict = $"SKIP ({skip.Message})";
                }
                catch (Exception error)
                {
                    run.Fail($"the scenario threw: {error.GetType().Name}: {error.Message}");
                    verdict = "FAIL";
                }
                finally
                {
                    var put = Displays.RestoreSaved();
                    if (put != "displays as they were")
                    {
                        run.Say(put);
                    }
                }
                // A scenario that ended the dock on purpose is judged from here against the one that took its place.
                if (scenario.EndsDock && Probe.Take() is { Dock: not null } replaced && replaced.Dock != baseline.Dock)
                {
                    baseline = Baseline.From(replaced);
                    watcher = new Watcher(baseline);
                }
                // Whatever the scenario did, the desktop must be whole again before the next.
                if (!verdict.StartsWith("SKIP", StringComparison.Ordinal) && watcher.Settle(TimeSpan.FromSeconds(scenario.SettleSeconds), TimeSpan.FromSeconds(2)) is null)
                {
                    run.Fail($"not whole {scenario.SettleSeconds} s after the scenario: {string.Join("; ", watcher.Open.Select(b => b.Says))}");
                    verdict = "FAIL";
                }
                var after = Probe.Take(ask: false);
                var written = Since(logMarks);
                var faults = written.Where(line => line.File == "karpo.log" && IsFault(line.Text) && !(scenario.EndsDock && IsDockEnd(line.Text))).ToList();
                if (faults.Count > 0 && verdict == "PASS")
                {
                    verdict = "FAIL";
                    run.Failed.Add($"Karpo logged {faults.Count} fault line(s)");
                }
                // Held in the end is not enough for some: a popup left a second, the foreground taken once.
                foreach (var made in watcher.Breaks)
                {
                    if (Rules.Patience(made.Key) is { } patience && made.Lasted(watcher.Now) > patience)
                    {
                        run.Failed.Add($"{made.Says} — for {made.Lasted(watcher.Now).TotalMilliseconds:0} ms, more than the {patience.TotalMilliseconds:0} ms it may");
                        if (verdict == "PASS")
                        {
                            verdict = "FAIL";
                        }
                    }
                }
                // What Karpo spent meanwhile: a slow or busy machine must not find it busy too.
                // (Another dock's time is its own: a scenario that ended the dock has none to compare.)
                var spent = after.Dock == before.Dock ? after.Cpu - before.Cpu : TimeSpan.Zero;
                var took = after.At - before.At;
                run.Say($"Karpo used {spent.TotalMilliseconds:0} ms of CPU in {took.TotalSeconds:0} s ({100 * spent.TotalMilliseconds / Math.Max(1, took.TotalMilliseconds):0.00} % of one core)");
                if (scenario.CpuLimit is { } limit && spent.TotalMilliseconds / Math.Max(1, took.TotalMilliseconds) > limit)
                {
                    run.Failed.Add($"Karpo used more than {limit * 100:0.#} % of one core");
                    if (verdict == "PASS")
                    {
                        verdict = "FAIL";
                    }
                }
                // Karpo never closes an app's window: one gone is named (an app may close itself; the scenario's own are not counted).
                var gone = othersBefore.Where(window => !Native.IsWindowLive(window.Handle)).ToList();
                if (gone.Count > 0)
                {
                    run.Say($"windows of other apps gone meanwhile (Karpo must never close one; check these): {string.Join(", ", gone.Select(w => $"{w.Class} of {w.Process}"))}");
                }
                if (after.Dock != baseline.Dock && written.Any(line => line.Text.Contains("asked by Karpo.exe --quit", StringComparison.Ordinal)))
                {
                    // Someone else (a publish) quit and started Karpo meanwhile: nothing this scenario can be judged by.
                    verdict = "INTERRUPTED (Karpo was quit and started again from outside)";
                    Thread.Sleep(5000);
                    baseline = Baseline.From(Probe.Take());
                }
                if (verdict == "FAIL")
                {
                    failures++;
                }
                Console.WriteLine($"   {verdict}");
                results.Add((scenario.Name, cycle, verdict));
                report.AppendLine();
                report.AppendLine($"## {scenario.Name}{(cycles > 1 ? $" ({cycle}/{cycles})" : "")}: {verdict}");
                report.AppendLine();
                report.AppendLine(scenario.Says);
                report.AppendLine();
                foreach (var line in run.Said)
                {
                    report.AppendLine($"- {line}");
                }
                if (watcher.Breaks.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("Rules broken on the way:");
                    report.AppendLine("```");
                    report.AppendLine(watcher.Summary());
                    report.AppendLine("```");
                }
                report.AppendLine();
                report.AppendLine($"Holds: handles {before.Handles} → {after.Handles}, GDI {before.Gdi} → {after.Gdi}, USER {before.User} → {after.User}, private {before.PrivateBytes / 1048576.0:0.0} → {after.PrivateBytes / 1048576.0:0.0} MB, threads {before.Threads} → {after.Threads}");
                if (written.Count > 0)
                {
                    report.AppendLine();
                    report.AppendLine("Karpo wrote:");
                    report.AppendLine("```");
                    foreach (var line in written.Take(200))
                    {
                        report.AppendLine($"{line.File}: {line.Text}");
                    }
                    if (written.Count > 200)
                    {
                        report.AppendLine($"... {written.Count - 200} more");
                    }
                    report.AppendLine("```");
                }
                if (after.Dock is null)
                {
                    report.AppendLine("Karpo is not running: the run stops.");
                    goto end;
                }
            }
        }
    end:
        Console.WriteLine(Displays.RestoreSaved());
        Displays.Forget();
        report.Insert(0, "");
        var summary = new StringBuilder();
        summary.AppendLine("| scenario | verdict |");
        summary.AppendLine("|---|---|");
        foreach (var (name, cycle, verdict) in results)
        {
            summary.AppendLine($"| {name}{(cycles > 1 ? $" #{cycle}" : "")} | {verdict} |");
        }
        var text = report.ToString();
        var cut = text.IndexOf("\n## ", StringComparison.Ordinal);
        text = cut < 0 ? text + summary : text[..cut] + "\n" + summary + text[cut..];
        Directory.CreateDirectory(output);
        var file = Path.GetFullPath(Path.Combine(output, $"run-{DateTime.Now:yyyyMMdd-HHmmss}.md"));
        File.WriteAllText(file, text);
        Console.WriteLine(summary);
        Console.WriteLine($"report: {file}");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>The repository: the folder above the harness that holds Karpo.slnx; else the current one.</summary>
    private static string Root()
    {
        for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Karpo.slnx")))
            {
                return folder.FullName;
            }
        }
        return Environment.CurrentDirectory;
    }

    /// <summary>The guard's word that the dock ended: a fault, but where the scenario ended it.</summary>
    private static bool IsDockEnd(string line) => line.Contains("guard: dock", StringComparison.Ordinal) && line.Contains("ended", StringComparison.Ordinal);

    private static bool IsFault(string line) =>
        line.Contains("caught and carried on", StringComparison.Ordinal)
        || line.Contains("fatal", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Unhandled", StringComparison.Ordinal)
        || (line.Contains("guard: dock", StringComparison.Ordinal) && line.Contains("ended", StringComparison.Ordinal));

    private static Dictionary<string, long> Marks()
    {
        var marks = new Dictionary<string, long>();
        foreach (var name in new[] { "karpo.log", "appbar.log" })
        {
            var path = Path.Combine(Logs, name);
            marks[name] = File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        return marks;
    }

    private static List<(string File, string Text)> Since(Dictionary<string, long> marks)
    {
        var lines = new List<(string, string)>();
        foreach (var (name, mark) in marks)
        {
            var path = Path.Combine(Logs, name);
            if (!File.Exists(path))
            {
                continue;
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            // A log that rolled over (appbar.log → appbar.log.1) starts again from 0.
            stream.Position = stream.Length >= mark ? mark : 0;
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (line.Length > 0)
                {
                    lines.Add((name, line));
                }
            }
        }
        return lines;
    }
}
