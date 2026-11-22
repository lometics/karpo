// Several processes saving one config.json at the same time, one of them killed
// in the middle: the file must stay whole and hold every change whose save returned.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit.Abstractions;

namespace Karpo.Tests.Persistence;

[Collection(ConfigFile.Collection)]
public sealed class StressTests(ITestOutputHelper output) : IDisposable
{
    private const int Workers = 8;
    private const int Rounds = 40;

    public void Dispose() => Reset();

    private static void Reset()
    {
        Files.Reset();
        foreach (var log in Directory.GetFiles(Config.Folder, "worker-*.log"))
        {
            File.Delete(log);
        }
    }

    [Fact]
    public void ManyProcessesSavingAtOnceLoseNothing()
    {
        Reset();
        Files.Start();
        var go = $@"Local\karpo-tests-go-{Guid.NewGuid():N}";
        using var start = new EventWaitHandle(false, EventResetMode.ManualReset, go);
        var processes = Enumerable.Range(0, Workers).Select(worker => Launch(worker, go)).ToList();
        var clock = Stopwatch.StartNew();
        try
        {
            start.Set();
            // Worker 0 is killed once a few of its saves are done, whatever it is doing then:
            // inside the lock, halfway through a write, between the renames of a replace.
            var victim = processes[0];
            while (Logged(0).Count < 6 && !victim.HasExited && clock.Elapsed < TimeSpan.FromSeconds(60))
            {
                Thread.Sleep(2);
            }
            Assert.False(victim.HasExited, "worker 0 finished before it could be killed");
            victim.Kill();
            foreach (var process in processes.Skip(1))
            {
                Assert.True(process.WaitForExit(TimeSpan.FromSeconds(120)), "a worker didn't finish");
                Assert.True(process.ExitCode == 0, $"worker exited with {process.ExitCode}: {process.StandardError.ReadToEnd()}");
            }
            victim.WaitForExit();
        }
        finally
        {
            foreach (var process in processes)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                process.Dispose();
            }
        }
        clock.Stop();

        var config = Config.Load();
        Assert.Null(config.Recovery);
        var pins = Files.Pins(config);
        var saves = 0;
        var merges = 0;
        for (var worker = 0; worker < Workers; worker++)
        {
            var log = Logged(worker);
            var done = log.Count;
            saves += done;
            merges += log.Count(line => line.EndsWith("merged", StringComparison.Ordinal));
            Assert.True(worker == 0 || done == Rounds, $"worker {worker} logged {done} saves");
            // The killed worker's save in progress may or may not have landed: what it would have changed isn't checked.
            var unsure = worker == 0 ? new HashSet<string> { Worker.Pin(worker, done), Worker.Pin(worker, done - 2) } : [];
            for (var round = 0; round < done; round++)
            {
                var pin = Worker.Pin(worker, round);
                if (unsure.Contains(pin))
                {
                    continue;
                }
                var removed = Worker.Removes(round + 2) && round + 2 < done;
                Assert.True(removed != pins.Contains(pin), $"{pin} should be {(removed ? "gone" : "there")}");
                if (round % 4 == 0)
                {
                    var folder = Files.Folder(config, Worker.Group(worker, round));
                    Assert.NotNull(folder);
                    Assert.Contains(folder.Cells, cell => cell.Island.Pin?.Target == Worker.Member(worker, round));
                }
            }
            if (worker != 0)
            {
                Assert.Equal((Rounds - 1) % 2 == 0 ? TrayState.Taskbar : TrayState.Overflow, Files.TrayStates(config)[$"w{worker}"]);
            }
        }
        Assert.Equal(pins.Count, pins.Distinct().Count());
        Assert.InRange(config.TaskbarOpacity, 30, 30 + Workers - 1);
        // The backup is always a whole version too.
        Assert.NotNull(Config.Parse(File.ReadAllText(Config.BackupPath)));
        output.WriteLine($"{Workers} processes, {saves} saves in {clock.ElapsedMilliseconds} ms, {merges} of them merged others' changes in; worker 0 killed after {Logged(0).Count} saves; {pins.Count} pins in the end");
    }

    private static List<string> Logged(int worker)
    {
        try
        {
            using var stream = new FileStream(Worker.Log(Config.Folder, worker), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch (IOException)
        {
            return [];
        }
    }

    /// <summary>This test assembly, run by the dotnet host as a program (see <see cref="Program"/>).</summary>
    private static Process Launch(int worker, string go)
    {
        var start = new ProcessStartInfo(DotNet())
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        foreach (var argument in (string[])["exec", typeof(Worker).Assembly.Location, Worker.Switch, Config.Folder, $"{worker}", $"{Rounds}", go])
        {
            start.ArgumentList.Add(argument);
        }
        return Process.Start(start)!;
    }

    /// <summary>The dotnet host: the one running the tests when it says (DOTNET_HOST_PATH), else the one of this runtime.</summary>
    private static string DotNet()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
        {
            return host;
        }
        // …\dotnet\shared\Microsoft.NETCore.App\<version>\ → …\dotnet\dotnet.exe
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        return Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", "dotnet.exe"));
    }
}
