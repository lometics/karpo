// How the guard starts a dock again outside its own jobs (Karpo.Dock\DockLauncher.cs): the order
// of the two ways, and the direct way for real, inside job objects this test makes. The direct way
// has to run inside the job, so it runs in a worker: this test assembly started as a program
// (LaunchWorker), put in the job before its first instruction.
//
// Through Explorer is tested only when asked for (KARPO_SHELL_TESTS=1): it has the person's own
// Explorer start a program, which no other test does.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Docking;

/// <summary>A fact that runs only with KARPO_SHELL_TESTS=1: it has Explorer start a hidden program.</summary>
public sealed class ShellFactAttribute : FactAttribute
{
    public ShellFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("KARPO_SHELL_TESTS") != "1")
        {
            Skip = "Has the person's Explorer start a hidden program; set KARPO_SHELL_TESTS=1.";
        }
    }
}

public sealed unsafe class DockLauncherTests
{
    private const uint BreakawayOk = 0x0800;
    private const uint KillOnJobClose = 0x2000;
    private const uint CreateSuspended = 0x4;
    private const uint CreateNoWindow = 0x08000000;

    // --- The order -------------------------------------------------------------------

    [Fact]
    public void Started_directly_Explorer_is_not_asked()
    {
        var asked = false;

        var launch = DockLauncher.Start("Karpo.exe", "", (_, _) => (true, "direct"), (_, _) => { asked = true; return (true, "shell"); });

        Assert.Equal(DockLauncher.Route.Direct, launch.Route);
        Assert.False(asked);
    }

    [Fact]
    public void Refused_directly_it_is_started_by_Explorer()
    {
        var launch = DockLauncher.Start("Karpo.exe", "", (_, _) => (false, "refused (error 5)"), (_, _) => (true, "by Explorer"));

        Assert.Equal(DockLauncher.Route.Shell, launch.Route);
        Assert.Equal("refused (error 5); by Explorer", launch.Says);
    }

    [Fact]
    public void Refused_both_ways_it_says_why_twice()
    {
        var launch = DockLauncher.Start("Karpo.exe", "", (_, _) => (false, "refused (error 5)"), (_, _) => (false, "no Explorer"));

        Assert.Equal(DockLauncher.Route.None, launch.Route);
        Assert.Equal("refused (error 5); no Explorer", launch.Says);
    }

    [Theory]
    [InlineData("--gpu", "--gpu")]
    [InlineData(@"C:\Program Files\Karpo\Karpo.exe", "\"C:\\Program Files\\Karpo\\Karpo.exe\"")]
    [InlineData("", "\"\"")]
    public void An_argument_is_quoted_only_when_it_needs_it(string argument, string quoted) =>
        Assert.Equal(quoted, DockLauncher.Quote(argument));

    // --- The direct way, for real ------------------------------------------------------

    /// <summary>A job that refuses breaking away keeps the direct way shut: error 5, and nothing runs.</summary>
    [Fact]
    public void A_job_that_refuses_breaking_away_is_not_left_directly()
    {
        var (done, says, child) = DirectInJob(allowBreakaway: false);

        Assert.False(done, says);
        Assert.Contains("error 5", says, StringComparison.Ordinal);
        Assert.Null(child);
    }

    /// <summary>
    /// A job that allows it is left. Where this test runs in no job of its own, the program runs
    /// in no job at all. Where it runs inside one that refuses (as under a Claude session), the
    /// program would still be inside that one: then it is not let run, and the guard goes to Explorer.
    /// </summary>
    [Fact]
    public void A_job_that_allows_breaking_away_is_left_and_nothing_runs_inside_any_job()
    {
        var (done, says, child) = DirectInJob(allowBreakaway: true);

        if (InAnyJob(GetCurrentProcess()))
        {
            Assert.False(done, says);
            Assert.Contains("not let run", says, StringComparison.Ordinal);
            Assert.Null(child);
        }
        else
        {
            Assert.True(done, says);
            Assert.NotNull(child);
            Assert.False(child.Value.InTestJob, "the program was still in the test's job");
            Assert.False(child.Value.InAnyJob, "the program was still in a job");
        }
    }

    // --- Through Explorer, when asked for -----------------------------------------------

    /// <summary>
    /// The guard's case under a Claude session: in a job that refuses breaking away. The direct way is
    /// refused, and Explorer starts the program as its own child, outside that job. (Not in no job at
    /// all: Windows puts what Explorer starts in a job of the Program Compatibility Assistant's, which
    /// only allows breaking away and never ends anything.)
    /// </summary>
    [ShellFact]
    public void From_inside_a_job_that_refuses_breaking_away_Explorer_starts_it_outside()
    {
        var name = $"Local\\karpo-launch-{Guid.NewGuid():N}";
        var report = Path.Combine(Path.GetTempPath(), $"karpo-shell-{Guid.NewGuid():N}.txt");
        string[] program = [DotNet(), "exec", typeof(LaunchWorker).Assembly.Location, LaunchWorker.ReportSwitch, report, name];

        var said = InJob(name, allowBreakaway: false, [LaunchWorker.FullSwitch, .. program]).Split('|', 2);

        Assert.True(said[0] == nameof(DockLauncher.Route.Shell), string.Join(" | ", said));
        Assert.Contains("error 5", said[1], StringComparison.Ordinal);
        var deadline = DateTime.Now.AddSeconds(30);
        while (!File.Exists(report) && DateTime.Now < deadline)
        {
            Thread.Sleep(100);
        }
        Thread.Sleep(200);
        var reported = File.ReadAllText(report).Split('|');
        File.Delete(report);
        var explorers = Process.GetProcessesByName("explorer").Select(process => (uint)process.Id).ToList();
        Assert.True(reported[0] == "outside the test's job", string.Join(" | ", reported));
        Assert.True(explorers.Contains(uint.Parse(reported[2], System.Globalization.CultureInfo.InvariantCulture)), $"not Explorer's child: {string.Join(" | ", reported)}");
    }

    // --- Helpers --------------------------------------------------------------------------

    /// <summary>
    /// LaunchWorker's direct way, in a job of this test's making, asked to start ping; and when it
    /// started it, whether ping is in that job and in any job. A ping that broke away ends by itself
    /// a second later.
    /// </summary>
    private static (bool Done, string Says, (bool InTestJob, bool InAnyJob)? Child) DirectInJob(bool allowBreakaway)
    {
        var name = $"Local\\karpo-launch-{Guid.NewGuid():N}";
        var said = InJob(name, allowBreakaway, [LaunchWorker.Switch, Path.Combine(Environment.SystemDirectory, "PING.EXE"), "-n", "2", "127.0.0.1"]).Split('|', 2);
        if (said[0] != "True")
        {
            return (false, said[1], null);
        }
        var pid = uint.Parse(said[1].Split(' ')[3], System.Globalization.CultureInfo.InvariantCulture);
        var child = OpenProcess((uint)(SYNCHRONIZE | PROCESS.PROCESS_QUERY_LIMITED_INFORMATION), BOOL.FALSE, pid);
        Assert.True(child != HANDLE.NULL, $"the program it started ({pid}) was already gone");
        try
        {
            var job = Job(name);
            try
            {
                BOOL inTest, inAny;
                Assert.True(IsProcessInJob(child, job, &inTest));
                Assert.True(IsProcessInJob(child, HANDLE.NULL, &inAny));
                WaitForSingleObject(child, 30_000);
                return (true, said[1], (inTest, inAny));
            }
            finally
            {
                CloseHandle(job);
            }
        }
        finally
        {
            CloseHandle(child);
        }
    }

    /// <summary>
    /// LaunchWorker with <paramref name="args"/>, in a new job named <paramref name="name"/>: started
    /// suspended and put in the job before it runs. What it wrote comes back. The job ends whatever is
    /// still in it when the test lets go of it.
    /// </summary>
    private static string InJob(string name, bool allowBreakaway, string[] args)
    {
        HANDLE job;
        fixed (char* named = name)
        {
            job = CreateJobObjectW(null, named);
        }
        Assert.True(job != HANDLE.NULL);
        var output = Path.Combine(Path.GetTempPath(), $"karpo-launch-{Guid.NewGuid():N}.txt");
        try
        {
            var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
            limits.BasicLimitInformation.LimitFlags = KillOnJobClose | (allowBreakaway ? BreakawayOk : 0);
            Assert.True(SetInformationJobObject(job, JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, &limits, (uint)sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION)));
            string[] all = [DotNet(), "exec", typeof(LaunchWorker).Assembly.Location, args[0], output, .. args[1..]];
            var line = (string.Join(' ', all.Select(DockLauncher.Quote)) + "\0").ToCharArray();
            var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
            PROCESS_INFORMATION worker;
            fixed (char* application = DotNet())
            fixed (char* command = line)
            {
                Assert.True(CreateProcessW(application, command, null, null, BOOL.FALSE, CreateSuspended | CreateNoWindow, null, null, &startup, &worker));
            }
            try
            {
                Assert.True(AssignProcessToJobObject(job, worker.hProcess));
                ResumeThread(worker.hThread);
                Assert.Equal(0u, WaitForSingleObject(worker.hProcess, 120_000));
            }
            finally
            {
                CloseHandle(worker.hThread);
                CloseHandle(worker.hProcess);
            }
            return File.ReadAllText(output);
        }
        finally
        {
            CloseHandle(job);
            File.Delete(output);
        }
    }

    /// <summary>The job of that name, to ask about.</summary>
    internal static HANDLE Job(string name)
    {
        fixed (char* named = name)
        {
            return OpenJobObjectW(0x0004 /* JOB_OBJECT_QUERY */, BOOL.FALSE, named);
        }
    }

    private static bool InAnyJob(HANDLE process)
    {
        BOOL result;
        Assert.True(IsProcessInJob(process, HANDLE.NULL, &result));
        return result;
    }

    /// <summary>The dotnet host: the one running the tests when it says (DOTNET_HOST_PATH), else the one of this runtime.</summary>
    internal static string DotNet()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
        {
            return host;
        }
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        return Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", "dotnet.exe"));
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern BOOL IsProcessInJob(HANDLE process, HANDLE job, BOOL* result);
}

/// <summary>
/// This test assembly as a program (<c>dotnet exec Karpo.Tests.dll …</c>, see Persistence\Worker.cs):
/// <see cref="Switch"/> runs DockLauncher's direct way and writes what it said; <see cref="ReportSwitch"/>
/// writes whether this process is in a job, and its parent.
/// </summary>
internal static unsafe class LaunchWorker
{
    public const string Switch = "--launch-worker";
    public const string FullSwitch = "--launch-worker-full";
    public const string ReportSwitch = "--job-report";

    /// <summary>output file, program, then its arguments: the direct way only.</summary>
    public static int Run(string[] args)
    {
        var (done, says) = DockLauncher.Direct(args[1], Arguments(args));
        File.WriteAllText(args[0], $"{done}|{says}");
        return 0;
    }

    /// <summary>output file, program, then its arguments: the guard's whole way, direct then Explorer.</summary>
    public static int RunFull(string[] args)
    {
        var launch = DockLauncher.Start(args[1], Arguments(args));
        File.WriteAllText(args[0], $"{launch.Route}|{launch.Says}");
        return 0;
    }

    /// <summary>output file, the test's job's name: whether this process is in that job, in any, its parent, and its job's flags.</summary>
    public static int Report(string[] args)
    {
        var job = DockLauncherTests.Job(args[1]);
        BOOL inTest = BOOL.FALSE, inAny;
        if (job != HANDLE.NULL)
        {
            _ = IsProcessInJob(GetCurrentProcess(), job, &inTest);
            CloseHandle(job);
        }
        _ = IsProcessInJob(GetCurrentProcess(), HANDLE.NULL, &inAny);
        var basic = default(ProcessBasicInformation);
        uint length;
        _ = NtQueryInformationProcess(GetCurrentProcess(), 0, &basic, (uint)sizeof(ProcessBasicInformation), &length);
        File.WriteAllText(args[0], $"{(inTest ? "inside the test's job" : "outside the test's job")}|{(inAny ? "in a job" : "no job")}|{(uint)basic.InheritedFromUniqueProcessId}|{JobFlags()}");
        return 0;
    }

    /// <summary>The program's arguments as one command line, each quoted as it needs.</summary>
    private static string Arguments(string[] args) => string.Join(' ', args.Skip(2).Select(DockLauncher.Quote));

    /// <summary>The limit flags of the job this process is in (its innermost), in hex; empty in none.</summary>
    private static string JobFlags()
    {
        var limits = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
        uint length;
        return QueryInformationJobObject(HANDLE.NULL, JOBOBJECTINFOCLASS.JobObjectExtendedLimitInformation, &limits, (uint)sizeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION), &length)
            ? $"0x{limits.BasicLimitInformation.LimitFlags:X}"
            : "";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nuint AffinityMask;
        public nint BasePriority;
        public nuint UniqueProcessId;
        public nuint InheritedFromUniqueProcessId;
    }

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern BOOL IsProcessInJob(HANDLE process, HANDLE job, BOOL* result);

    [DllImport("ntdll", ExactSpelling = true)]
    private static extern int NtQueryInformationProcess(HANDLE process, int informationClass, void* information, uint length, uint* returned);
}
