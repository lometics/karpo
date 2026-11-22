// Where Karpo writes what went wrong, so that somebody can find out afterwards.
//
// The bar is a windowed program: it has no console, and everything it ever said
// about a failure went to Console.Error and died there. That is how a panel came
// to say "no devices" for weeks with the reason thrown away — the code knew, it
// said so, and nobody could hear it. So troubles go to a file beside the other
// things Karpo keeps (Config.LocalFolder\karpo.log) as well as to the console,
// which is still worth having under Karpo.exe --smoke.
//
// One line each: when, what was being done, and the exception's TYPE, message
// and HResult. The type and the HResult are the half that matters for the faults
// this exists for — a COM or WinRT failure whose message is "Element not found"
// says nothing, and 0x80070490 says which one. What ended the program (Fatal) and
// the first of an error the dock carried on from (Report) have their stack too,
// indented under the line.
//
// Three kinds of line:
//   * Note: a service's failure, written every time; services fail rarely.
//   * Report: an error the dock caught and carried on from. These can come on
//     every frame, so they pass TroubleGate: a trouble's first line, then at most
//     one a minute saying how many more there were, and never more than a burst
//     a minute in all.
//   * Fatal: what ended the program. Always written; there is only one.
// A dock's guard adds one more when the dock ends, saying how (Karpo.Dock\DockExit.cs).
//
// Rules this file lives by, because a logger that breaks the program it reports
// on is worse than none:
//   * Never throws. A locked file, a full disk, a folder that cannot be made:
//     the line is dropped and the program carries on.
//   * Never blocks for long. One lock, one append, no flush-and-wait.
//   * Never grows without bound. An entry is cut at MaxEntry characters, and past
//     LimitBytes the file is started again with the last one kept beside it
//     (karpo.log.old), so there are at most two.

using System.Text;

namespace Karpo.Core;

public static class Troubles
{
    /// <summary>The log is started again past this, and the one before it kept beside it.</summary>
    internal const long LimitBytes = 256 * 1024;

    /// <summary>The longest one entry may be, its stack included; the rest is cut.</summary>
    internal const int MaxEntry = 8 * 1024;

    /// <summary>What an entry cut at <see cref="MaxEntry"/> ends with.</summary>
    internal const string Cut = " ...(cut)";

    private static readonly object Gate = new();

    // Report's rate: a trouble at most once a minute, and twenty lines a minute in all.
    private static readonly object ReportsGate = new();
    private static readonly TroubleGate Reports = new(TimeSpan.FromMinutes(1), burst: 20);

    /// <summary>Where the lines go.</summary>
    public static string Path => System.IO.Path.Combine(Config.LocalFolder, "karpo.log");

    /// <summary>The one before it, kept when the log is started again.</summary>
    internal static string Previous => Path + ".old";

    /// <summary>
    /// Something failed while <paramref name="what"/> was being done. The line carries the
    /// exception's type and HResult as well as its message: a WinRT failure's message alone is
    /// usually not enough to tell two faults apart.
    /// </summary>
    public static void Note(string what, Exception error) =>
        Write($"{what}: {Describe(error)}");

    /// <summary>
    /// An error the program caught and carried on from, while <paramref name="what"/> was being done.
    /// Written with its stack, unless the same error came before it: then at most once a minute,
    /// saying how many more there were, and never more than twenty lines a minute in all
    /// (<see cref="TroubleGate"/>).
    /// </summary>
    public static void Report(string what, Exception error)
    {
        var key = $"{what}\n{error.GetType().FullName}\n{error.HResult}\n{error.Message}";
        (bool Write, int HeldBack) let;
        lock (ReportsGate)
        {
            let = Reports.Let(key, DateTime.UtcNow);
        }
        if (!let.Write)
        {
            return;
        }
        Write(let.HeldBack > 0
            ? $"{what}: {Describe(error)} (and {let.HeldBack} more like it since its last line)"
            : $"{what}: {Explain(error)}");
    }

    /// <summary>
    /// Runs <paramref name="work"/> where nobody is waiting to catch what it throws: a timer's
    /// callback, a file watcher's event, a wait the pool answers, a thread of Karpo's own. An
    /// exception that leaves one of those ends the process, the whole bar with it; here it is
    /// reported (<see cref="Report"/>) as having happened while <paramref name="what"/> was being
    /// done, and the thread carries on.
    /// </summary>
    public static void Guard(string what, Action work)
    {
        try
        {
            work();
        }
        catch (Exception error)
        {
            Report(what, error);
        }
    }

    /// <summary>
    /// <see cref="Guard(string, Action)"/> for work that awaits: what a task started and left to
    /// run on its own throws is reported, and the task ends well. Nobody would have looked at the
    /// task's fault.
    /// </summary>
    public static async Task Guard(string what, Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            Report(what, error);
        }
    }

    /// <summary>What ended the program, while <paramref name="what"/> was being done: always written, with the stack.</summary>
    public static void Fatal(string what, Exception error) =>
        Write($"{what}: {Explain(error)}");

    /// <summary>
    /// How an exception reads in the log: its type, its message, and its HResult in hex. Pure, so
    /// the tests can hold the shape without a file.
    /// </summary>
    public static string Describe(Exception error) =>
        $"{error.GetType().Name}: {error.Message} (HResult 0x{error.HResult:X8})";

    /// <summary>
    /// <see cref="Describe"/>, then the stack, a frame to a line and indented, then each inner
    /// exception the same way. Pure.
    /// </summary>
    public static string Explain(Exception error)
    {
        var text = new StringBuilder(Describe(error));
        for (var at = error; at is not null; at = at.InnerException)
        {
            if (!ReferenceEquals(at, error))
            {
                text.Append(Environment.NewLine).Append("  inner ").Append(Describe(at));
            }
            if (at.StackTrace is { Length: > 0 } trace)
            {
                foreach (var frame in trace.Split('\n'))
                {
                    if (frame.Trim() is { Length: > 0 } said)
                    {
                        text.Append(Environment.NewLine).Append("    ").Append(said);
                    }
                }
            }
        }
        return text.ToString();
    }

    /// <summary>An entry no longer than <see cref="MaxEntry"/>. Pure.</summary>
    internal static string Bounded(string line) =>
        line.Length <= MaxEntry ? line : string.Concat(line.AsSpan(0, MaxEntry - Cut.Length), Cut);

    /// <summary>
    /// A line in the log, stamped and appended. Also to the console, which is where it goes under
    /// Karpo.exe --smoke and in the tests; a windowed Karpo has none, which is the point of the file.
    /// </summary>
    public static void Write(string line)
    {
        line = Bounded(line);
        var said = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}";
        Console.Error.WriteLine("Karpo: " + line);
        try
        {
            lock (Gate)
            {
                Roll();
                Directory.CreateDirectory(Config.LocalFolder);
                File.AppendAllText(Path, said + Environment.NewLine);
            }
        }
        catch (Exception)
        {
            // A log that throws is worse than a log that misses a line.
        }
    }

    /// <summary>Starts the log again once it is long enough, keeping the last one beside it.</summary>
    private static void Roll()
    {
        try
        {
            if (new FileInfo(Path) is { Exists: true } file && file.Length >= LimitBytes)
            {
                File.Move(Path, Previous, overwrite: true);
            }
        }
        catch (Exception)
        {
            // Another Karpo has it open, or it went between the look and the move: leave it.
        }
    }
}
