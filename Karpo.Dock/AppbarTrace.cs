// A short record of what the bars asked of the shell about the space they hold, and what they
// found, so that a start that ends with no space reserved can be understood after the fact.
//
// The failure it is here for leaves nothing on screen to look at: every call looks as though it
// worked, the work area is simply the whole display, and a maximized window runs under the bar
// until the dock is started again. So each shell call is written with its answer, each check of
// the work area that finds the strip missing is written with both rectangles, and so is every
// repair. A check that finds everything as it should be writes nothing, except the first one after
// a start, which says how long the shell took; an idle dock adds nothing to the file.
//
// %LOCALAPPDATA%\Karpo\appbar.log (Config.LocalFolder, so an isolated run writes its own), at most
// two files of MaxBytes each: past that the file becomes appbar.log.1 and a new one starts.
// Writing never throws; a trace that cannot be written is a trace that is not written.

using System.Globalization;
using System.Text;
using Karpo.Core;

namespace Karpo.Dock;

internal sealed class AppbarTrace
{
    /// <summary>How large one file grows before it is rolled over.</summary>
    public const long MaxBytes = 64 * 1024;

    private readonly string? path;
    private readonly long maxBytes;
    private readonly Func<DateTime> clock;

    /// <summary>A trace that writes nothing: the tests' and anything else that must not touch the disk.</summary>
    public static readonly AppbarTrace None = new(null);

    /// <param name="path">Where to write; null for nowhere.</param>
    /// <param name="maxBytes">How large a file grows before it rolls over.</param>
    /// <param name="clock">The time each line is stamped with; local time unless a test says otherwise.</param>
    public AppbarTrace(string? path, long maxBytes = MaxBytes, Func<DateTime>? clock = null)
    {
        this.path = path;
        this.maxBytes = Math.Max(maxBytes, 1024);
        this.clock = clock ?? (() => Clock.Real());
    }

    /// <summary>The trace the dock keeps: <c>appbar.log</c> in Karpo's local folder.</summary>
    public static AppbarTrace ForDock() => new(System.IO.Path.Combine(Config.LocalFolder, "appbar.log"));

    /// <summary>The file written to, or null when this trace writes nothing.</summary>
    public string? FilePath => path;

    /// <summary>Appends one line, stamped with the time, rolling the file over first when it is full.</summary>
    public void Write(string line)
    {
        if (path is null)
        {
            return;
        }
        try
        {
            var folder = System.IO.Path.GetDirectoryName(path);
            if (folder is { Length: > 0 })
            {
                Directory.CreateDirectory(folder);
            }
            var info = new FileInfo(path);
            if (info.Exists && info.Length >= maxBytes)
            {
                // One old file kept, so what led up to a failure is still there after it.
                File.Move(path, path + ".1", overwrite: true);
            }
            var stamp = clock().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            File.AppendAllText(path, stamp + " " + line + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A full disk or a folder that went away must never take the bar with it.
        }
    }
}
