// Made-up folders for the folder widget and a folder pin's flyout (Karpo.Dock\Drives\FolderContents.cs):
// a tree of names in memory, the failures a real disk has (a folder that is gone, one this account
// may not read, a share that answers with an error), icons drawn here rather than asked of the shell,
// and what was opened written down instead of opened. Nothing a test does with it reads a real folder
// or starts a program. §498: a file's size made up from its name, the type names a Windows would give the
// kinds the pictures use, and a picture's thumbnail drawn here — a landscape, wider than tall — counted, the
// reads and thumbnails run where a test says (RunBy: at once, or held until the test lets them go).

using Karpo.Dock;
using Karpo.Dock.Drives;

namespace Karpo.Tests.Folders;

internal sealed class FakeFolders
{
    private static readonly DateTime When = new(2026, 9, 30, 14, 5, 0, DateTimeKind.Local);

    private readonly Dictionary<string, List<FolderItem>> folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> failures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock gate = new();
    private readonly List<(string Path, Action Changed)> watched = [];

    public FakeFolders()
    {
        Source = new FolderSource
        {
            Enumerate = Enumerate,
            Exists = path => folders.ContainsKey(Trim(path)) || failures.ContainsKey(Trim(path)),
            Icon = (path, px) =>
            {
                Interlocked.Increment(ref icons);
                return Picture(path, px);
            },
            Watch = (path, changed) =>
            {
                lock (gate)
                {
                    watched.Add((path, changed));
                }
                return new Unwatch(this, path);
            },
            Open = (path, folder) =>
            {
                lock (gate)
                {
                    Opened.Add((path, folder));
                }
            },
            // At once, on the caller's thread: what was read is there when the call returns.
            Run = work => work(),
            TypeName = name => Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".xlsx" => "Microsoft Excel Worksheet",
                ".docx" => "Microsoft Word Document",
                ".png" => "PNG File",
                ".jpg" => "JPG File",
                ".md" => "Markdown Source File",
                ".exe" => "Application",
                ".pdf" => "Microsoft Edge PDF Document",
                _ => null,
            },
            Thumbnail = (path, px) =>
            {
                Interlocked.Increment(ref thumbnails);
                lock (gate)
                {
                    ThumbnailThreads.Add(Environment.CurrentManagedThreadId);
                }
                return Path.GetExtension(path).ToLowerInvariant() is ".png" or ".jpg" ? Landscape(path, px) : null;
            },
        };
    }

    private int thumbnails;

    /// <summary>§498: how many thumbnails the shell would have been asked for.</summary>
    public int Thumbnails => Volatile.Read(ref thumbnails);

    /// <summary>§498: the threads they were asked for on.</summary>
    public List<int> ThumbnailThreads { get; } = [];

    /// <summary>§498: the same folders, their reads and thumbnails run by <paramref name="run"/> (another thread, or held).</summary>
    public FolderSource RunBy(Action<Action> run) => new()
    {
        Enumerate = Source.Enumerate,
        Exists = Source.Exists,
        Icon = Source.Icon,
        Watch = Source.Watch,
        Open = Source.Open,
        Run = run,
        TypeName = Source.TypeName,
        Thumbnail = Source.Thumbnail,
    };

    public FolderSource Source { get; }

    /// <summary>What was opened, in order: the path and whether it was opened as a folder.</summary>
    public List<(string Path, bool Folder)> Opened { get; } = [];

    private int icons;

    /// <summary>How many icons the shell would have been asked for.</summary>
    public int Icons => Volatile.Read(ref icons);

    /// <summary>How many reads the folders have had.</summary>
    public int Reads => Volatile.Read(ref reads);

    private int reads;

    /// <summary>The folders being watched now.</summary>
    public IReadOnlyList<string> Watched
    {
        get
        {
            lock (gate)
            {
                return [.. watched.Select(each => each.Path)];
            }
        }
    }

    /// <summary>A folder at <paramref name="path"/> holding <paramref name="items"/>: a name ending in "\" is a folder.</summary>
    public FakeFolders With(string path, params string[] items)
    {
        var list = new List<FolderItem>();
        for (var index = 0; index < items.Length; index++)
        {
            var name = items[index];
            var folder = name.EndsWith('\\');
            list.Add(new FolderItem(folder ? name.TrimEnd('\\') : name, folder, When.AddDays(-index * 3).AddHours(-index), FileAttributes.Normal,
                folder ? 0 : (ulong)((name.Length * 9_731) + (index * 104_729))));
        }
        folders[Trim(path)] = list;
        return this;
    }

    /// <summary>An item the file system hides from a folder that is already there.</summary>
    public FakeFolders Hidden(string path, string name, FileAttributes attributes)
    {
        folders[Trim(path)].Add(new FolderItem(name, false, When, attributes));
        return this;
    }

    /// <summary>A folder whose read fails with <paramref name="error"/>.</summary>
    public FakeFolders Failing(string path, Exception error)
    {
        failures[Trim(path)] = error;
        return this;
    }

    /// <summary>The folder at <paramref name="path"/> holds <paramref name="items"/> from now on, and its watchers hear of it.</summary>
    public void Change(string path, params string[] items)
    {
        With(path, items);
        List<Action> told;
        lock (gate)
        {
            told = [.. watched.Where(each => string.Equals(Trim(each.Path), Trim(path), StringComparison.OrdinalIgnoreCase)).Select(each => each.Changed)];
        }
        foreach (var changed in told)
        {
            changed();
        }
    }

    /// <summary>The standard tree the pictures and the runs use: a Projects folder three levels deep, and files of several kinds.</summary>
    public static FakeFolders Projects() => new FakeFolders()
        .With(@"C:\Users\Ann\Projects",
            "Karpo\\", "Notes\\", "Receipts 2026\\", "Website\\",
            "budget.xlsx", "Holiday plan.docx", "logo.png", "Meeting notes 2.md", "Meeting notes 10.md", "setup.exe")
        .With(@"C:\Users\Ann\Projects\Karpo", "docs\\", "src\\", "tests\\", "README.md", "Karpo.slnx")
        .With(@"C:\Users\Ann\Projects\Karpo\docs", "pictures\\", "backlog.md", "widgets.md")
        .With(@"C:\Users\Ann\Projects\Karpo\docs\pictures", "flyout.png", "folder.png")
        .With(@"C:\Users\Ann\Projects\Karpo\src", "Dock\\", "Core\\")
        .With(@"C:\Users\Ann\Projects\Karpo\tests")
        .With(@"C:\Users\Ann\Projects\Notes", "ideas.txt")
        .With(@"C:\Users\Ann\Projects\Receipts 2026")
        .With(@"C:\Users\Ann\Projects\Website", "index.html");

    private IEnumerable<FolderItem> Enumerate(string path)
    {
        Interlocked.Increment(ref reads);
        if (failures.TryGetValue(Trim(path), out var error))
        {
            throw error;
        }
        if (!folders.TryGetValue(Trim(path), out var items))
        {
            throw new DirectoryNotFoundException($"Could not find a part of the path '{path}'.");
        }
        return [.. items];
    }

    private static string Trim(string path) => path.TrimEnd('\\');

    /// <summary>
    /// A picture of what is at <paramref name="path"/>, as the shell would draw it, drawn here: a
    /// yellow folder with its tab for a folder, a page with its corner turned for a file — its
    /// colour by its kind, so two kinds are told apart in a picture.
    /// </summary>
    private Pixels Picture(string path, int px)
    {
        var folder = folders.ContainsKey(Trim(path)) || !Path.HasExtension(path);
        var kind = Path.GetExtension(path).ToLowerInvariant();
        var (r, g, b) = folder ? (0xF6, 0xC3, 0x4A)
            : kind switch
            {
                ".xlsx" => (0x21, 0x86, 0x4B),
                ".docx" => (0x2B, 0x6C, 0xD8),
                ".png" => (0xC2, 0x4E, 0xD0),
                ".exe" => (0x5E, 0x6A, 0x7A),
                _ => (0x8A, 0x96, 0xA6),
            };
        var data = new byte[px * px * 4];
        for (var y = 0; y < px; y++)
        {
            for (var x = 0; x < px; x++)
            {
                var fx = x / (float)px;
                var fy = y / (float)px;
                var inside = folder
                    ? (fy >= 0.22f && fy <= 0.86f && fx >= 0.06f && fx <= 0.94f) || (fy >= 0.14f && fy < 0.22f && fx >= 0.06f && fx <= 0.42f)
                    : fx >= 0.18f && fx <= 0.82f && fy >= 0.08f && fy <= 0.92f && !(fx > 0.62f && fy < 0.08f + (fx - 0.62f));
                if (!inside)
                {
                    continue;
                }
                var stripe = !folder && fy > 0.62f && fy < 0.80f;
                var (pr, pg, pb) = folder || stripe ? (r, g, b) : (0xF4, 0xF6, 0xF8);
                var at = ((y * px) + x) * 4;
                data[at] = (byte)pb;
                data[at + 1] = (byte)pg;
                data[at + 2] = (byte)pr;
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = px, Height = px };
    }

    /// <summary>
    /// §498: a picture's thumbnail, drawn here: a landscape four by three, fitted into <paramref name="px"/> — a sky of the
    /// name's own colour, a sun, green hills — so a thumbnail is told from an icon at a glance, and two pictures apart.
    /// </summary>
    private static Pixels Landscape(string path, int px)
    {
        var width = px;
        var height = Math.Max(1, px * 3 / 4);
        var hue = (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(Path.GetFileName(path)) % 360;
        var (skyR, skyG, skyB) = hue < 120 ? (0x7A, 0xB8, 0xF0) : hue < 240 ? (0xF2, 0xA2, 0x6B) : (0x9C, 0x8C, 0xE8);
        var data = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var fx = x / (float)width;
                var fy = y / (float)height;
                var hill = 0.62f + (0.10f * MathF.Sin((fx * 6.2f) + (hue / 60f)));
                var sun = ((fx - 0.72f) * (fx - 0.72f)) + ((fy - 0.28f) * (fy - 0.28f) * 0.56f) < 0.008f;
                var (r, g, b) = fy > hill ? (0x3E, 0x8E, 0x4A) : sun ? (0xFF, 0xE0, 0x6A) : (skyR, skyG, skyB);
                var at = ((y * width) + x) * 4;
                data[at] = (byte)b;
                data[at + 1] = (byte)g;
                data[at + 2] = (byte)r;
                data[at + 3] = 255;
            }
        }
        return new Pixels { Data = data, Width = width, Height = height };
    }

    private sealed class Unwatch(FakeFolders owner, string path) : IDisposable
    {
        public void Dispose()
        {
            lock (owner.gate)
            {
                owner.watched.RemoveAll(each => each.Path == path);
            }
        }
    }
}
