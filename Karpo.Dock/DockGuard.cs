// The guard: `Karpo.exe --guard <pid> [--restarts n@first] [-- <flags>]`, a process of its own
// that the dock starts once it has hidden Windows' taskbar (Dock.SpawnGuard). It waits for the
// dock to end, however it ends. It never ends the dock.
//
// Once the dock has ended, the guard:
//   1. writes one line to karpo.log saying how it ended (DockExit.cs) and what comes next: the
//      dock can say why it is leaving only when it leaves by its own way out, and the guard is the
//      one witness left when it doesn't;
//   2. leaves everything to a newer dock if one holds the instance mutex, and otherwise puts
//      Windows' taskbar back straight away, so nobody is without one while anything else happens;
//   3. starts the dock again unless GuardPolicy says it stays down: not after a quit Karpo asked
//      for (its exit code says so; a dock ended from outside with 0 starts again), not while
//      Windows signs out or shuts down, not a dock on a test config, and never a fourth time
//      within 10 minutes (2 s, 20 s and 120 s apart). The count goes to the restarted dock as
//      `--restarts n@first`, and from it to its own guard; the flags after `--` (how it draws)
//      go along too.
// The restarted dock is started outside every job this guard is in (DockLauncher), and every
// step is a line in karpo.log.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class DockGuard
{
    public const string Switch = "--guard";

    /// <summary>The dock's flags a restarted dock keeps: how it draws, not how it was started.</summary>
    internal static readonly string[] Kept = ["--gpu", "--no-glass"];

    /// <summary>How long the guard watches for the restarted dock to come up (its instance mutex).</summary>
    private static readonly TimeSpan ComingUp = TimeSpan.FromSeconds(15);

    private const int SmShuttingDown = 0x2000;

    /// <summary>Guards the dock with process id <paramref name="pid"/>, which was handed <paramref name="before"/>.</summary>
    public static void Run(uint pid, Restarts before, IReadOnlyList<string> kept)
    {
        var dock = DockExit.Open(pid);
        if (dock == HANDLE.NULL)
        {
            return;
        }
        var end = DockExit.Wait(dock, pid);
        var decision = GuardPolicy.Decide(end.Code, NewerDock(), SessionEnding(), Config.IsIsolated, before, Now());
        // Written before the taskbar goes back: that asks Explorer, which may not answer.
        Troubles.Write($"{DockExit.Line(end)}; {decision.Says}");
        if (decision.Step == GuardStep.LeaveToNewer)
        {
            return;
        }
        Troubles.Write($"guard: {Taskbar.Restore()}");
        if (decision.Step != GuardStep.Restart)
        {
            return;
        }
        Thread.Sleep(decision.Wait);
        // A wait of up to two minutes is long enough for things to change.
        if (NewerDock())
        {
            Troubles.Write("guard: a dock was started meanwhile, so this one is not started again");
            return;
        }
        if (SessionEnding())
        {
            Troubles.Write("guard: Windows is signing out or shutting down, so the dock is not started again");
            return;
        }
        if (Environment.ProcessPath is not { } exe)
        {
            Troubles.Write("guard: the dock's program could not be found, so it is not started again");
            return;
        }
        var launch = DockLauncher.Start(exe, DockArguments(decision.Next, kept));
        Troubles.Write($"guard: restart {decision.Next.Count} of {GuardPolicy.Most}: {launch.Says}");
        if (launch.Route != DockLauncher.Route.None)
        {
            Troubles.Write(CameUp()
                ? "guard: the restarted dock is up"
                : $"guard: no dock came up within {DockExit.Span(ComingUp)} of the restart");
        }
    }

    /// <summary>What <c>Karpo.exe --guard</c> was given: null without a process id. Unknown flags are dropped. Pure.</summary>
    internal static (uint Pid, Restarts Restarts, string[] Kept)? Parse(IReadOnlyList<string> args)
    {
        if (args.Count < 2
            || args[0] != Switch
            || !uint.TryParse(args[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var pid))
        {
            return null;
        }
        var rest = args.Skip(2).ToList();
        var dash = rest.IndexOf("--");
        var kept = dash >= 0 ? rest.Skip(dash + 1).Where(Kept.Contains).Distinct().ToArray() : [];
        return (pid, Restarts.In(dash >= 0 ? rest.Take(dash).ToList() : rest), kept);
    }

    /// <summary>The guard's arguments, for the dock that starts it. Pure.</summary>
    internal static string[] Arguments(uint pid, Restarts restarts, IReadOnlyList<string> kept)
    {
        var args = new List<string> { Switch, pid.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        if (restarts.Count > 0)
        {
            args.Add(Restarts.Switch);
            args.Add(restarts.Text);
        }
        if (kept.Count > 0)
        {
            args.Add("--");
            args.AddRange(kept);
        }
        return [.. args];
    }

    /// <summary>A restarted dock's command line after the program: the count, then the kept flags. Pure.</summary>
    internal static string DockArguments(Restarts restarts, IReadOnlyList<string> kept) =>
        string.Join(' ', new[] { Restarts.Switch, restarts.Text }.Concat(kept.Where(Kept.Contains)));

    /// <summary>Whether a dock holds the instance mutex: once this guard's dock has ended, a newer one.</summary>
    internal static bool NewerDock() => DockInstance.Held();

    private static bool SessionEnding() => GetSystemMetrics(SmShuttingDown) != 0;

    private static long Now() => Clock.RealOffsetUtc().ToUnixTimeSeconds();

    /// <summary>Whether a dock takes the instance mutex within <see cref="ComingUp"/>.</summary>
    private static bool CameUp()
    {
        for (var waited = TimeSpan.Zero; waited < ComingUp; waited += TimeSpan.FromMilliseconds(250))
        {
            if (NewerDock())
            {
                return true;
            }
            Thread.Sleep(250);
        }
        return NewerDock();
    }
}
