// folder contents (§498): the thumbnails a folder's line shows in its tiles and icons — a picture's own, a video's
// frame — as File Explorer's large icons show them. The shell may take a while over one it never drew before, so
// they are never asked for on the dock's thread: a cell drawn asks for its own (Of), which is null until it is read
// and then kept; the reads go one after another on one run of the folder source's (FolderSource.Run, a thread of its
// own with COM up for Windows' folders), the newest wanted first — the cells on screen now before those scrolled
// past — and the line is drawn again as each comes. Only the cells drawn ask, so a folder of thousands of photos
// reads the dozen it shows. What is kept is bounded by its bytes, the longest unseen let go first; a file changed is
// another thumbnail (its time is in the key). A test hands in a source whose thumbnails are drawn and whose run it
// holds, so nothing reads a real file.

using Karpo.Core;

namespace Karpo.Dock.Drives;

/// <summary>The thumbnails one folder's line shows, read off the dock's thread and kept (see the top of the file).</summary>
internal sealed class FolderThumbnails : IDisposable
{
    /// <summary>The most bytes of thumbnails kept: about a hundred large icons at 200 %.</summary>
    internal const long Budget = 16L * 1024 * 1024;

    /// <summary>The most waiting to be read: the cells drawn last; those scrolled past further back are let go.</summary>
    internal const int MostWaiting = 64;

    /// <summary>The kinds of file the shell draws a thumbnail of that is worth the wait: pictures and videos.</summary>
    private static readonly HashSet<string> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".jfif", ".png", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".avif", ".jxr", ".dng",
        ".mp4", ".m4v", ".mov", ".avi", ".wmv", ".mkv", ".webm",
    };

    private readonly Lock gate = new();
    private readonly FolderSource source;
    private readonly Action arrived;
    private readonly Dictionary<string, (Pixels? Picture, LinkedListNode<string> Seen)> kept = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> seen = new();
    private readonly LinkedList<(string Key, string Path, int Px)> waiting = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, string Path, int Px)>> wanted = new(StringComparer.OrdinalIgnoreCase);
    private long bytes;
    private bool running;
    private bool disposed;
    private int reads;

    /// <param name="arrived">Asks for the line to be drawn again: a thumbnail came, from whatever thread.</param>
    public FolderThumbnails(FolderSource source, Action arrived)
    {
        this.source = source;
        this.arrived = arrived;
    }

    /// <summary>Whether <paramref name="entry"/> is a file of a kind that wears its thumbnail: a picture or a video.</summary>
    public static bool Shows(in DriveEntry entry)
    {
        if (entry.Folder)
        {
            return false;
        }
        var dot = entry.Name.LastIndexOf('.');
        return dot > 0 && Kinds.Contains(entry.Name[dot..]);
    }

    /// <summary>How many thumbnails the source was asked for, for tests.</summary>
    public int Reads => Volatile.Read(ref reads);

    /// <summary>How many bytes of them are kept, for tests.</summary>
    public long Bytes
    {
        get
        {
            lock (gate)
            {
                return bytes;
            }
        }
    }

    /// <summary>
    /// The thumbnail of the file at <paramref name="path"/> as it was last changed (<paramref name="modified"/>), fitted
    /// into <paramref name="px"/> px: kept, else null and asked for — the line is drawn again once it is in. Null too
    /// where the shell has none, which is kept as an answer and not asked again; and always where the source has none.
    /// </summary>
    public Pixels? Of(string path, DateTime modified, int px)
    {
        if (source.Thumbnail is null || path.Length == 0)
        {
            return null;
        }
        var key = $"{path}|{modified.Ticks}|{px}";
        lock (gate)
        {
            if (disposed)
            {
                return null;
            }
            if (kept.TryGetValue(key, out var found))
            {
                seen.Remove(found.Seen);
                seen.AddLast(found.Seen);
                return found.Picture;
            }
            // The newest wanted at the end, read first; the oldest let go past the most.
            if (wanted.Remove(key, out var was))
            {
                waiting.Remove(was);
            }
            wanted[key] = waiting.AddLast((key, path, px));
            while (waiting.Count > MostWaiting && waiting.First is { } oldest)
            {
                wanted.Remove(oldest.Value.Key);
                waiting.RemoveFirst();
            }
            if (running)
            {
                return null;
            }
            running = true;
        }
        source.Run(Drain);
        // A source that runs at once (a test's) has it already.
        lock (gate)
        {
            return kept.TryGetValue(key, out var come) ? come.Picture : null;
        }
    }

    /// <summary>Reads what is waiting, the newest first, until nothing is; the line is drawn again after each.</summary>
    private void Drain()
    {
        while (true)
        {
            (string Key, string Path, int Px) next;
            lock (gate)
            {
                if (disposed || waiting.Last is not { } last)
                {
                    running = false;
                    return;
                }
                next = last.Value;
                waiting.RemoveLast();
                wanted.Remove(next.Key);
            }
            Pixels? picture = null;
            try
            {
                Interlocked.Increment(ref reads);
                picture = source.Thumbnail?.Invoke(next.Path, next.Px);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // A handler that misbehaves costs the file its thumbnail: its icon stays.
            }
            lock (gate)
            {
                if (disposed)
                {
                    running = false;
                    return;
                }
                Keep(next.Key, picture);
            }
            arrived();
        }
    }

    /// <summary>Keeps <paramref name="picture"/> under <paramref name="key"/>, letting the longest unseen go past the budget. Under the gate.</summary>
    private void Keep(string key, Pixels? picture)
    {
        if (kept.Remove(key, out var old))
        {
            seen.Remove(old.Seen);
            bytes -= old.Picture?.Data.LongLength ?? 0;
        }
        kept[key] = (picture, seen.AddLast(key));
        bytes += picture?.Data.LongLength ?? 0;
        while (bytes > Budget && seen.First is { } first && first.Value != key)
        {
            if (kept.Remove(first.Value, out var gone))
            {
                bytes -= gone.Picture?.Data.LongLength ?? 0;
            }
            seen.RemoveFirst();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            disposed = true;
            waiting.Clear();
            wanted.Clear();
            kept.Clear();
            seen.Clear();
            bytes = 0;
        }
    }
}
