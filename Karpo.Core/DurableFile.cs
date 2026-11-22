// A file that must never be lost or left half written, read and written the one
// way: config.json (ConfigFile), accounts.json (AccountBook) and the caches the
// dock keeps of what it read (the calendars, the mail, the tasks with the To Do
// changes not sent yet, the islands').
//
// - A read opens the file with every share mode, so a write by the other
//   process (or this one's, on another thread) can replace it while the read is
//   open, and a sharing violation — a virus scanner, a backup tool, an indexer
//   holding the file for a moment — is tried again before the file counts as
//   locked. Locked is not missing: the file is there and says something nobody
//   knows yet, so a caller never takes it as empty and writes over it.
// - A write goes to a temporary file in the same folder, through to the disk
//   (write-through, then a flush of what the file system keeps about the file),
//   and File.Replace swaps it in, the version before becoming name.bak. A crash
//   or a power cut at any moment leaves the old file or the new one, whole. A
//   sharing violation is tried again; a write that still does not go through
//   throws, and the file is as it was.
// - A file that does not read is kept aside as name.broken-<time>, never
//   overwritten by another one kept the same second.

using System.Globalization;
using System.Text;

namespace Karpo.Core;

/// <summary>What reading a file found.</summary>
public enum FileRead
{
    /// <summary>The file was read: what it says is in the text or the bytes.</summary>
    Ok,

    /// <summary>There is no such file.</summary>
    Missing,

    /// <summary>Another program held the file every time it was tried: it is there, and what it says is not known.</summary>
    Locked,
}

public static class DurableFile
{
    /// <summary>Reads and replaces that meet a sharing violation are tried this often, 10 ms more apart each time (about half a second in all).</summary>
    public const int Attempts = 10;

    /// <summary>ReplaceFile renamed the old file to the backup's name but couldn't move the new one in.</summary>
    private const int ErrorUnableToMoveReplacement2 = 1177;

    /// <summary>The file a write turns <paramref name="path"/> into: the version before, which always read.</summary>
    public static string BackupOf(string path) => path + ".bak";

    /// <summary>The file's text; a sharing violation is tried <paramref name="attempts"/> times before the file counts as locked.</summary>
    public static FileRead ReadText(string path, out string? text, int attempts = Attempts)
    {
        var read = Read(path, static stream =>
        {
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }, out var found, attempts);
        text = found;
        return read;
    }

    /// <summary>The file's bytes; a sharing violation is tried <paramref name="attempts"/> times before the file counts as locked.</summary>
    public static FileRead ReadBytes(string path, out byte[]? bytes, int attempts = Attempts)
    {
        var read = Read(path, static stream =>
        {
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }, out var found, attempts);
        bytes = found;
        return read;
    }

    /// <summary><paramref name="text"/> as the file, in UTF-8: see <see cref="Write(string, byte[], bool)"/>.</summary>
    public static void Write(string path, string text, bool keepBackup = true) => Write(path, Encoding.UTF8.GetBytes(text), keepBackup);

    /// <summary>
    /// Writes <paramref name="bytes"/> as the file at <paramref name="path"/>: into a temporary file
    /// beside it, through to the disk, then swapped in, the old file becoming name.bak when
    /// <paramref name="keepBackup"/> (never pass it for a file that does not read: it would take the
    /// place of a good backup). Throws IOException or UnauthorizedAccessException when it could not
    /// be written; the file is then as it was. The caller makes sure no two writes of the same file
    /// run at once.
    /// </summary>
    public static void Write(string path, byte[] bytes, bool keepBackup = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        // Once a run, what earlier runs left beside it goes (Tidy).
        lock (tidied)
        {
            if (tidied.Add(Path.GetFullPath(path)))
            {
                Tidy(path);
            }
        }
        var temporary = $"{path}.{Environment.ProcessId}-{Guid.NewGuid().ToString("N")[..8]}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                // Write-through takes the data to the disk as it goes; this also
                // flushes what the file system keeps about the file, such as its length.
                stream.Flush(flushToDisk: true);
            }
            Swap(temporary, path, keepBackup);
        }
        finally
        {
            // Gone after a swap; still there when the swap failed.
            try
            {
                File.Delete(temporary);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Moves a file that does not read out of the way as name.broken-&lt;time&gt; (name.broken-&lt;time&gt;-2
    /// where one was kept the same second), so nothing it said is lost; copies it when it can't be
    /// moved. Returns where it went; null when it could be neither moved nor copied.
    /// </summary>
    public static string? KeepBroken(string path)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var kept = $"{path}.broken-{stamp}";
        for (var number = 2; File.Exists(kept); number++)
        {
            kept = $"{path}.broken-{stamp}-{number}";
        }
        try
        {
            File.Move(path, kept);
            Tidy(path);
            return kept;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Copy(path, kept);
                Tidy(path);
                return kept;
            }
            catch (Exception again) when (again is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Karpo: {Path.GetFileName(path)} doesn't read and couldn't be kept aside: {again.Message}");
                return null;
            }
        }
    }

    /// <summary>How many copies of one kind (name.broken-*, name.before-import-*, name.before-reset-*) are kept: the newest.</summary>
    public const int CopiesKept = 5;

    /// <summary>How old a temporary file must be before it is taken for one a crash left (a write takes a moment).</summary>
    public static readonly TimeSpan StaleTemporary = TimeSpan.FromHours(1);

    /// <summary>The kinds of copy a file of Karpo's is kept as beside itself, each with its time after it.</summary>
    private static readonly string[] CopyKinds = ["broken", "before-import", "before-reset"];

    // The files tidied in this run: once each is enough (Write tidies a file the first time it writes it).
    private static readonly HashSet<string> tidied = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What piles up beside <paramref name="path"/> over the months, put down to what is worth keeping:
    /// the newest <see cref="CopiesKept"/> of each kind of copy (a file that did not read, the file an
    /// import or a reset replaced), and no temporary file a crash or a power cut left in the middle of a
    /// write (name.&lt;pid&gt;-&lt;id&gt;.tmp, older than <see cref="StaleTemporary"/>). Never the file
    /// itself or its .bak. Nothing that fails here is an error: it is tried again next run.
    /// </summary>
    public static void Tidy(string path)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        var name = Path.GetFileName(path);
        if (folder is null || !Directory.Exists(folder))
        {
            return;
        }
        try
        {
            foreach (var kind in CopyKinds)
            {
                var old = Directory.EnumerateFiles(folder, $"{name}.{kind}-*")
                    // Their times sort as they were kept: yyyyMMdd-HHmmss, a -2 after one kept the same second.
                    .OrderByDescending(file => Path.GetFileName(file), StringComparer.OrdinalIgnoreCase)
                    .Skip(CopiesKept)
                    .ToList();
                foreach (var file in old)
                {
                    Delete(file);
                }
            }
            var stale = DateTime.UtcNow - StaleTemporary;
            foreach (var file in Directory.EnumerateFiles(folder, $"{name}.*.tmp"))
            {
                if (File.GetLastWriteTimeUtc(file) < stale)
                {
                    Delete(file);
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Delete(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static FileRead Read<T>(string path, Func<Stream, T> take, out T? found, int attempts)
        where T : class
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                found = take(stream);
                return FileRead.Ok;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                found = null;
                return FileRead.Missing;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt >= attempts)
                {
                    found = null;
                    return FileRead.Locked;
                }
                Thread.Sleep(10 * attempt);
            }
        }
    }

    private static void Swap(string temporary, string path, bool keepBackup)
    {
        var backup = BackupOf(path);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (keepBackup && File.Exists(path))
                {
                    File.Replace(temporary, path, backup, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(temporary, path, overwrite: true);
                }
                return;
            }
            catch (IOException error) when ((error.HResult & 0xFFFF) == ErrorUnableToMoveReplacement2)
            {
                // The old file already carries the backup's name: finish the move, or
                // put the old file back, so that the file is never missing.
                try
                {
                    File.Move(temporary, path);
                    return;
                }
                catch (Exception again) when (again is IOException or UnauthorizedAccessException)
                {
                    try
                    {
                        File.Copy(backup, path, overwrite: false);
                    }
                    catch (Exception restore) when (restore is IOException or UnauthorizedAccessException)
                    {
                    }
                    throw;
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (attempt < Attempts)
                {
                    // Someone has the file open without sharing its deletion (another
                    // program's read, a virus scanner): it is usually gone in a moment.
                    Thread.Sleep(10 * attempt);
                    continue;
                }
                if (keepBackup && File.Exists(path))
                {
                    // ReplaceFile doesn't work on every volume (some network shares):
                    // the same two steps by hand.
                    File.Copy(path, backup, overwrite: true);
                    File.Move(temporary, path, overwrite: true);
                    return;
                }
                throw;
            }
        }
    }
}
