// One KARPO_UI_TESTS run at a time on this PC, whichever checkout or worktree it is from. The UI
// tests' windows, hidden desktops and captures are all served by the one dwm.exe that draws the
// person's screen, on the thread that composes it. A run drove it to 1.4–1.6 cores while the CPU
// stood at a quarter, and two or three runs at once (a session in each worktree) held the screen
// for seconds — up to 18 s — so the person's PC seemed frozen, the pointer and the keys with it
// (2026-10-31). Karpo.Tests.csproj also runs a UI run on four threads rather than one a core
// (ui.runsettings), which took the worst hold from 11.5 s to 1.7 s.
//
// The turn is a file in the temporary folder, held open for writing by the run that has it until
// its process ends; Windows closes it with the process however the run ends, killed or crashed.
// Not a mutex: one belongs to the thread that took it, and this is taken in the module
// initializer, whose thread is the test host's, and no other thread may run this assembly's code
// before the initializer is done. A run waits for its turn before any test of it is found. A run
// without KARPO_UI_TESTS, and this assembly run as a program (Persistence\Worker.cs, which the
// stress tests start from a run that holds the turn), never wait.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Karpo.Tests;

internal static class UiTurn
{
    private static readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "karpo-tests-ui-run.lock");

    /// <summary>The turn, while this run has it: open until the process ends.</summary>
    private static FileStream? held;

#pragma warning disable CA2255 // A test assembly is exactly where a module initializer belongs.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Take()
    {
        if (Environment.GetEnvironmentVariable("KARPO_UI_TESTS") != "1" || Assembly.GetEntryAssembly() == typeof(UiTurn).Assembly)
        {
            return;
        }
        var said = false;
        while (true)
        {
            try
            {
                // Others may read who has it; none may open it to write while this run does.
                held = new FileStream(Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                break;
            }
            catch (IOException) when (!said)
            {
                said = true;
                Console.Error.WriteLine($"Karpo.Tests: another KARPO_UI_TESTS run has the turn ({Holder()}); this one waits for it to end.");
            }
            catch (IOException)
            {
            }
            Thread.Sleep(500);
        }
        var who = Encoding.UTF8.GetBytes($"process {Environment.ProcessId}, {AppContext.BaseDirectory}, since {DateTime.Now:HH:mm:ss}");
        held.SetLength(0);
        held.Write(who);
        held.Flush();
    }

    /// <summary>What the run that has the turn wrote of itself.</summary>
    private static string Holder()
    {
        try
        {
            using var file = new FileStream(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(file);
            return reader.ReadToEnd();
        }
        catch (IOException)
        {
            return "which, it does not say";
        }
    }
}
