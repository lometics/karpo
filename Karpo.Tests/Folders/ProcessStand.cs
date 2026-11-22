// A PC's processes with nothing read from this one, for the top processes' line (ProcessesLine): a
// handful of apps each at its own steady share of an eight-processor machine, every reading two
// seconds after the last, named without the shell and drawn with the glyph an app without an icon
// gets. Ids no Windows hands out, so a menu asking whether one would end opens nothing real.

using Karpo.Dock;
using Karpo.Dock.Performance;

namespace Karpo.Tests.Folders;

internal sealed class ProcessStand
{
    public const int Processors = 8;

    /// <summary>Two seconds between readings, in 100 ns.</summary>
    public const long Span = 20_000_000;

    /// <summary>The apps, busiest first: image, name, how many processes, share of the whole processor.</summary>
    public static readonly (string Image, string Name, int Count, float Share)[] Apps =
    [
        ("chrome.exe", "Google Chrome", 23, 0.184f),
        ("Code.exe", "Visual Studio Code", 9, 0.072f),
        ("svchost.exe", "Host Process for Windows Services", 61, 0.033f),
        ("explorer.exe", "Windows Explorer", 1, 0.021f),
        ("dwm.exe", "Desktop Window Manager", 1, 0.016f),
        ("System", "System", 1, 0.012f),
        ("AdobeCollabSync.exe", "Adobe Creative Cloud Experience Collaboration Synchronizer", 2, 0.009f),
        ("ms-teams.exe", "Microsoft Teams", 6, 0.004f),
        ("Spotify.exe", "Spotify", 5, 0.001f),
        ("OneDrive.exe", "Microsoft OneDrive", 1, 0f),
        ("Taskmgr.exe", "Task Manager", 1, 0f),
    ];

    private const uint FirstId = 4_000_000_000;

    private readonly Dictionary<uint, long> used = [];
    private long at;

    /// <summary>Time passes with nobody reading, in 100 ns.</summary>
    public void Wait(long span) => at += span;

    /// <summary>How many readings were taken.</summary>
    public int Reads { get; private set; }

    /// <summary>The next reading, two seconds on: each app's processes a share of its share each.</summary>
    public ProcessLook Next()
    {
        Reads++;
        at += Span;
        var processes = new List<ProcessSample>();
        var busy = 0.0;
        for (var app = 0; app < Apps.Length; app++)
        {
            var (image, _, count, share) = Apps[app];
            busy += share;
            for (var each = 0; each < count; each++)
            {
                var id = image == "System" ? 4u : FirstId + (uint)(app * 100) + (uint)each;
                var step = (long)((double)share * Span * Processors / count);
                used[id] = used.GetValueOrDefault(id, (app + 1) * 1_000_000_000L) + step;
                processes.Add(new ProcessSample(id, 1000 + id, used[id], image));
            }
        }
        used[ProcessTop.IdleId] = used.GetValueOrDefault(ProcessTop.IdleId) + (long)((1 - busy) * Span * Processors);
        processes.Add(new ProcessSample(ProcessTop.IdleId, 0, used[ProcessTop.IdleId], ""));
        return new ProcessLook(at, Processors, [.. processes]);
    }

    /// <summary>An app's name as the stand-in knows it, no icon and no file.</summary>
    public static ProcessName Name(ProcessGroup group) =>
        new(Apps.FirstOrDefault(app => string.Equals(app.Image, group.Image, StringComparison.OrdinalIgnoreCase)).Name ?? ProcessTop.StemOf(group.Image), null, null);

    /// <summary>The stand-in in place of Windows' processes until disposed; nothing opened either.</summary>
    public static Using Use(ProcessStand? stand = null) => new(stand ?? new ProcessStand());

    internal sealed class Using : IDisposable
    {
        private readonly (Func<ProcessLook?> Look, Func<ProcessGroup, ProcessName> Names, Action<string> Opener) was;

        public Using(ProcessStand stand)
        {
            Stand = stand;
            was = (ProcessesLine.Look, ProcessesLine.Names, ProcessesLine.Opener);
            ProcessesLine.ForgetLatest();
            ProcessesLine.Look = stand.Next;
            ProcessesLine.Names = Name;
            ProcessesLine.Opener = Opened.Add;
        }

        public ProcessStand Stand { get; }

        /// <summary>What a click opened.</summary>
        public List<string> Opened { get; } = [];

        public void Dispose()
        {
            (ProcessesLine.Look, ProcessesLine.Names, ProcessesLine.Opener) = was;
            ProcessesLine.ForgetLatest();
            ProcessesLine.Asked = null;
        }
    }
}
