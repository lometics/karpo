// Karpo: a dock that costs nothing while nobody is looking at it.
//
// One thread, no render loop. The loop below blocks until a message arrives
// or a watched handle (the config folder, the virtual desktop registry key)
// is signalled, and everything that moves is a composition animation that
// DWM runs by itself, so the process is never woken to draw a frame.

using System.Diagnostics;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
// `using static ...Windows` also brings in a region constant named ERROR.
using ERROR = TerraFX.Interop.Windows.ERROR;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Dock;

public static unsafe class Program
{
    // Without this .NET puts the main thread in the multithreaded apartment
    // first, and OleInitialize fails with RPC_E_CHANGED_MODE.
    [STAThread]
    public static int Main(string[] args)
    {
        // A Wi-Fi network's properties, Windows' own dialog, in a process of its own: whatever it
        // does ends this process, never the dock that started it (WifiProperties.cs).
        // First, so a profile named like another switch is still only a name.
        if (args.Length > 0 && args[0] == QuickSettings.Services.WifiProperties.Switch)
        {
            return QuickSettings.Services.WifiProperties.Run(args);
        }
        // The offscreen check, before anything that belongs to a running dock:
        // it takes no mutex, and a crash in it must not put the taskbar back.
        if (args.Contains("--smoke"))
        {
            return Smoke.Run(args);
        }
        // A layout of every ready-made group written into config.json, and nothing else: no window,
        // no mutex, no taskbar. A running dock merges it in on its next reload (PresetLayout.cs).
        if (args.Contains("--add-preset-layout"))
        {
            try
            {
                var power = QuickLinkMachine.Read();
                var layout = PresetLayout.AddToFile(PresetMachine.Read(BatteryIsland.Probe() is not null, power.Sleep, power.Hibernate));
                Console.Out.WriteLine($"Karpo: layout \"{layout.Name}\" ({layout.Id}) written to {Config.FilePath}");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Karpo: the layout was not written: {error.Message}");
                return 1;
            }
        }
        // One layout made what a first install makes, its id, name and pins kept, and nothing else:
        // as --add-preset-layout, no window, no mutex, no taskbar (LayoutRenewal.cs).
        if (Array.IndexOf(args, "--renew-layout") is var renew and >= 0)
        {
            if (renew + 1 >= args.Length || args[renew + 1].StartsWith("--", StringComparison.Ordinal))
            {
                Console.Error.WriteLine("Karpo: usage: Karpo.exe --renew-layout <layoutId>");
                return 2;
            }
            try
            {
                var done = LayoutRenewal.RenewInFile(args[renew + 1]);
                Console.Out.WriteLine($"Karpo: config.json kept as it was in {done.Backup}");
                Console.Out.WriteLine(
                    $"Karpo: layout \"{done.Layout.Name}\" ({done.Layout.Id}) renewed as a first install makes it: "
                    + $"{done.ItemsBefore} items before, {done.Layout.Islands.Count} now, its {done.PinsKept} pins kept; written to {Config.FilePath}");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Karpo: the layout was not renewed: {error.Message}");
                return 1;
            }
        }
        // The layouts, or every setting, made what a new install makes, as the settings app's About
        // page does: no window, no mutex, no taskbar; a running dock takes the file (ConfigReset.cs).
        if (Array.IndexOf(args, "--reset-settings") is var reset and >= 0)
        {
            var scope = reset + 1 < args.Length ? args[reset + 1] : "";
            if (scope is not ("layouts" or "all"))
            {
                Console.Error.WriteLine("Karpo: usage: Karpo.exe --reset-settings layouts|all");
                return 2;
            }
            try
            {
                ConfigReset.ResetFile(scope == "all" ? ResetScope.All : ResetScope.Layouts, out var kept);
                if (kept is not null)
                {
                    Console.Out.WriteLine($"Karpo: config.json kept as it was in {kept}");
                }
                Console.Out.WriteLine($"Karpo: {(scope == "all" ? "every setting" : "the layouts")} reset as a new install has them; written to {Config.FilePath}");
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"Karpo: nothing was reset: {error.Message}");
                return 1;
            }
        }
        // The running dock's host asked to quit, saying who asks (Closing.cs).
        if (args.Contains("--quit"))
        {
            fixed (char* name = DockHost.ClassName)
            {
                var window = FindWindowW(name, null);
                if (window != HWND.NULL) PostMessageW(window, WM.WM_CLOSE, (uint)DockHost.QuitBy.Command, 0);
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == DockGuard.Switch)
        {
            if (DockGuard.Parse(args) is { } guard)
            {
                DockGuard.Run(guard.Pid, guard.Restarts, guard.Kept);
            }
            return 0;
        }
        // §710: the Store's copy, started at sign-in by its package's startup task, which has no command
        // line of its own: "--startup" is added, as a Run entry's start has it.
        args = PackagedStartup.Arguments(args);
        // Started by the logon task while the Run entry it stands in for wouldn't start Karpo (gone, or
        // switched off in Task Manager): nothing starts, as nothing would have (StartupTask.cs).
        if (StartupRegistration.StartedByTaskUnwanted(args))
        {
            Troubles.Write("start: the logon task's, while the Run entry would not start Karpo, so nothing starts"); // not-ui: karpo.log
            return 0;
        }
        // A crash that still runs managed code puts the taskbar back here; one
        // that doesn't is left to the guard process. What it was goes to
        // karpo.log first: putting the taskbar back asks Explorer, which may not answer.
        AppDomain.CurrentDomain.UnhandledException += (_, crash) =>
        {
            if (crash.ExceptionObject is Exception error)
            {
                Troubles.Fatal("an exception nothing caught is ending the dock", error);
            }
            else
            {
                Troubles.Write($"an exception nothing caught is ending the dock: {crash.ExceptionObject}");
            }
            if (!Stage.On)
            {
                Taskbar.Restore();
            }
        };
        // stage: the dock on a made-up display, for a recording (Stage.cs).
        if (!Stage.Parse(args, out var stageError))
        {
            Console.Error.WriteLine($"Karpo: {stageError}");
            return 2;
        }
        var options = Options.FromArgs(args);
        try
        {
            // The manifest already asks for this, and a second request fails
            // with access denied; the call stays for a build without it.
            SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            // A single-threaded apartment with OLE on top: the shell's icon and
            // launch code expects the first, drag and drop needs the second.
            ComUtil.Check(OleInitialize(null));

            // A second dock would sit exactly on top of the first and take its
            // clicks. The handle stays open for as long as the process runs; a stage takes none (§393).
            var instance = DockInstance.Take(Stage.On);
            if (!instance.Mine)
            {
                // edit: a second start (the Start menu, Explorer, a pin) opens the running dock's ⚙
                // Settings; an automatic one asks nothing (Dock.EditEntries.cs), and says why it ends.
                if (SecondStart.Asks(args, Config.IsIsolated))
                {
                    SecondStart.Ask();
                }
                else
                {
                    Troubles.Write($"start: {string.Join(' ', args)}: a dock runs already, so this one ends"); // not-ui: karpo.log
                }
                return 0;
            }

            // Composition commits through this queue, which is pumped by the loop below.
            var queueOptions = new DispatcherQueueOptions
            {
                dwSize = (uint)sizeof(DispatcherQueueOptions),
                threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_NONE,
            };
            IDispatcherQueueController* rawQueue = null;
            ComUtil.Check(CreateDispatcherQueueController(queueOptions, &rawQueue));
            var queue = ComUtil.Adopt<Windows.System.DispatcherQueueController>(rawQueue);
            try
            {
                Run(options);
            }
            finally
            {
                // Alive until the dock, whose composition commits through it,
                // is gone.
                GC.KeepAlive(queue);
            }
            // Who asked it to quit, the one exit code its guard leaves down; 0 after a WM_QUIT
            // Karpo did not post, which the guard starts again (Closing.cs).
            return Closing.ExitCode(0);
        }
        catch (Exception error)
        {
            // A windowed program's console is nowhere: the file is the only place this is heard.
            // The exit code of its own lets the guard tell this end from a quit and from a kill;
            // an error on the way out of a quit Karpo asked for is still that quit.
            Troubles.Fatal("the dock ended on an error", error);
            return Closing.ExitCode(DockExit.Fatal);
        }
    }

    /// <summary>
    /// The loop. However it ends, by WM_QUIT or by an exception, disposing the
    /// dock puts the taskbar back.
    /// </summary>
    private static void Run(Options options)
    {
        // stage: the calendar and inbox a recording shows, made up, before the first build (Stage.cs).
        if (Stage.On)
        {
            Stage.Samples();
        }
        // §556: a look's Font offers this PC's families, read from DirectWrite when first listed.
        InstalledFonts.Reader = SystemFonts.Read;
        using var dock = Dock.Open(options);
        MSG msg;
        while (true)
        {
            var handles = dock.Handles();
            uint woke;
            fixed (HANDLE* waited = handles)
            {
                woke = MsgWaitForMultipleObjectsEx(
                    (uint)handles.Length,
                    waited,
                    INFINITE,
                    QS.QS_ALLINPUT,
                    MWMO.MWMO_INPUTAVAILABLE);
            }
            var index = woke - WAIT.WAIT_OBJECT_0;
            if (index < (uint)handles.Length)
            {
                dock.Signaled(handles[index]);
            }
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                if (msg.message == WM.WM_QUIT)
                {
                    if (Closing.Asked is null)
                    {
                        Troubles.Write("dock: a WM_QUIT Karpo did not post ended it without quitting, so its guard starts it again"); // not-ui: karpo.log
                    }
                    return;
                }
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
            dock.Drain();
            // Back to sleep next: the moment a collection costs nobody anything.
            Collector.WhenDue();
        }
    }

    /// <summary>
    /// Where startup memory goes, one line per stage, in debug builds only.
    /// Use <c>scripts/measure.ps1</c> for numbers worth comparing: these are
    /// taken before DWM has asked for a single frame.
    /// </summary>
    [Conditional("DEBUG")]
    public static void TraceMemory(string stage)
    {
        var counters = new PROCESS_MEMORY_COUNTERS_EX { cb = (uint)sizeof(PROCESS_MEMORY_COUNTERS_EX) };
        if (K32GetProcessMemoryInfo(GetCurrentProcess(), (PROCESS_MEMORY_COUNTERS*)&counters, counters.cb))
        {
            static double Mb(nuint bytes) => (double)bytes / (1024.0 * 1024.0);
            Console.Error.WriteLine(
                $"Karpo: {stage,-8} {Mb(counters.PrivateUsage),6:F1} MB private {Mb(counters.WorkingSetSize),6:F1} MB working set");
        }
    }
}

/// <summary>
/// A full collection after every few megabytes the dock allocates, taken just
/// before it goes back to sleep. Left to itself, the GC sizes its youngest
/// generation by the CPU's cache (tens of megabytes on a desktop CPU) and
/// keeps all of that committed, and a process that allocates as little as this
/// one fills it only every hour or so. Meanwhile every composition object and
/// surface the dock let go of without closing it holds its native memory,
/// which only a collection, and the finalizers after it, hands back. On a heap
/// of a few megabytes a full collection takes a millisecond or two. (The GC's
/// own setting for that budget, GCGen0MaxBudget, is read only from the
/// environment in a native AOT build, not from the project.)
/// </summary>
internal static class Collector
{
    /// <summary>What the dock may allocate between two collections.</summary>
    public const long Budget = 4L * 1024 * 1024;

    private static long collectedAt;

    /// <summary>Collects when <see cref="Budget"/> or more was allocated since the last collection. Cheap otherwise.</summary>
    public static void WhenDue()
    {
        if (GC.GetTotalAllocatedBytes(precise: false) - collectedAt < Budget)
        {
            return;
        }
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        collectedAt = GC.GetTotalAllocatedBytes(precise: false);
    }
}
