// The test assembly as a program of its own, for StressTests: several copies of
// it save one config.json at the same time, as the dock and the settings app do
// (and a person's text editor might), each in its own process.

namespace Karpo.Tests.Persistence;

/// <summary>
/// The entry point of Karpo.Tests.dll (the test host never calls it; Karpo.Tests.csproj turns the
/// generated one off). <c>dotnet exec Karpo.Tests.dll --config-worker …</c> runs a <see cref="Worker"/>;
/// <c>--launch-worker</c>, <c>--launch-worker-full</c> and <c>--job-report</c> run the guard's launcher tests' halves
/// (Docking\DockLauncherTests.cs); <c>--band-app</c> and <c>--band-explorer</c> the band's round trip's
/// (Signals\TaskbandRoundTripTests.cs); <c>--above-click</c> a click from below a window (Pinning\AboveKarpoTests.cs).
/// </summary>
public static class Program
{
    public static int Main(string[] args) => args.Length == 0 ? 0 : args[0] switch
    {
        Worker.Switch => Worker.Run(args[1..]),
        Docking.LaunchWorker.Switch => Docking.LaunchWorker.Run(args[1..]),
        Docking.LaunchWorker.FullSwitch => Docking.LaunchWorker.RunFull(args[1..]),
        Docking.LaunchWorker.ReportSwitch => Docking.LaunchWorker.Report(args[1..]),
        Signals.BandWorker.AppSwitch => Signals.BandWorker.App(args[1..]),
        Signals.BandWorker.ExplorerSwitch => Signals.BandWorker.Explorer(args[1..]),
        Pinning.AboveWorker.Switch => Pinning.AboveWorker.Run(args[1..]),
        _ => 0,
    };
}

/// <summary>
/// One writer: loads config.json, then saves <c>count</c> changes of its own, logging each one once
/// its save has returned, so the test knows what the file must hold in the end.
/// </summary>
internal static class Worker
{
    public const string Switch = "--config-worker";

    /// <summary>The pin the worker adds in round <paramref name="round"/>.</summary>
    public static string Pin(int worker, int round) => $"w{worker}-{round}";

    /// <summary>The folder the worker adds in rounds divisible by 4.</summary>
    public static string Group(int worker, int round) => $"g{worker}x{round}";

    /// <summary>The pin in that folder.</summary>
    public static string Member(int worker, int round) => $"m{worker}-{round}";

    /// <summary>In rounds divisible by 3 (from 3 on), the worker removes its pin of two rounds before.</summary>
    public static bool Removes(int round) => round >= 3 && round % 3 == 0;

    public static string Log(string folder, int worker) => Path.Combine(folder, $"worker-{worker}.log");

    /// <summary>folder, worker number, rounds, name of the event that starts everyone at once.</summary>
    public static int Run(string[] args)
    {
        var folder = args[0];
        var worker = int.Parse(args[1]);
        var rounds = int.Parse(args[2]);
        // The test assembly's module initializer pointed this process at a folder of its own.
        try
        {
            Directory.Delete(Isolation.Folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
        Environment.SetEnvironmentVariable("KARPO_CONFIG_DIR", folder);
        using (var start = EventWaitHandle.OpenExisting(args[3]))
        {
            start.WaitOne(TimeSpan.FromSeconds(30));
        }

        var config = Config.Load();
        for (var round = 0; round < rounds; round++)
        {
            Files.AddPin(config, Pin(worker, round));
            if (Removes(round))
            {
                Files.RemovePin(config, Pin(worker, round - 2));
            }
            if (round % 4 == 0)
            {
                Files.AddFolder(config, Group(worker, round), Member(worker, round));
            }
            Files.TrayStates(config)[$"w{worker}"] = round % 2 == 0 ? TrayState.Taskbar : TrayState.Overflow;
            // Everyone changes this one: the last saver's value, and still a valid file.
            config.TaskbarOpacity = 30 + worker;
            var saved = config.Save();
            // A save that brought others' changes in hands back another config.
            var merged = !ReferenceEquals(saved, config);
            // Half the writers go on with what Save returned, half with their own: neither may lose anything.
            if (worker % 2 == 0)
            {
                config = saved;
            }
            File.AppendAllText(Log(folder, worker), $"{round} {(merged ? "merged" : "own")}\n");
            if (round % 5 == 4 && config.Reload(out var fresh) == ConfigReload.Changed)
            {
                config = fresh;
            }
        }
        return 0;
    }
}
