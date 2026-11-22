// The to-do lists and the notes (docs\widgets.md §87): one store, one file, read and written by both
// apps — the dock's to-do and note lines, and the settings app's pickers, which make, rename and
// delete them.
//
// **Where.** %LOCALAPPDATA%\Karpo\notebook.json (Config.LocalFolder, which KARPO_CONFIG_DIR moves for
// a test), beside the other things of the person's that are not settings: what somebody wrote is
// theirs, and config.json is exported, merged and edited by hand. One file for both, because a note
// and a list are made, renamed and deleted the same way, by the same pickers, and one file is one lock
// and one watcher.
//
// **Not encrypted.** The PIM cache is (dd11aa3): it is a copy of what lives on a server, and losing
// the key costs a download. A note is the only copy there is. DPAPI ties it to the Windows profile's
// key: a password reset by an administrator, a restored profile or a copy to a new PC and every note
// is unreadable for ever, and a person who opens the file to save their notes finds noise. The file is
// as private as the profile's own folder is (its ACL is the user's), and a disk BitLocker covers.
//
// **Writes.** Every change is a function of what the file says now, run under a named mutex for the
// file and a lock in the process: the file is read again, the change applied, and the text written to
// a temporary file and swapped in (the version before kept as notebook.json.bak). So the dock and the
// settings app never lose each other's change, and a crash never leaves half a file.
//
// **Reads are tolerant.** A file that does not parse is kept aside as notebook.json.broken-<time> and
// the backup read instead, else nothing: never written over. Inside a file that parses, whatever is
// missing or odd is put right (Normalize): an id twice or none, a name of spaces, a finished time on
// a task not done.
//
// **Live.** Changed is raised after this process's change and when the file changes under it (Watch:
// the settings app's, a hand edit), so every line showing a list or a note shows it as it is.

using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>One task of a to-do list.</summary>
public sealed class TodoItem
{
    public string Id { get; set; } = "";

    public string Text { get; set; } = "";

    public bool Done { get; set; }

    public DateTimeOffset Created { get; set; }

    /// <summary>When it was ticked off; null while it is not done.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? Finished { get; set; }

    /// <summary>A Microsoft To Do task's due day, as the line draws it; never kept in notebook.json.</summary>
    [JsonIgnore]
    public DateOnly? Due { get; set; }

    /// <summary>A Microsoft To Do task's star; never kept in notebook.json.</summary>
    [JsonIgnore]
    public bool Important { get; set; }
}

/// <summary>A named to-do list: its tasks in the order the person put them.</summary>
public sealed class TodoList
{
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>Its tasks, done or not, in their order; a done task keeps its place, so unticking puts it back.</summary>
    public List<TodoItem> Items { get; set; } = [];
}

/// <summary>A named note: its title and its body, Markdown as the person typed it.</summary>
public sealed class Note
{
    public string Id { get; set; } = "";

    public string Title { get; set; } = "";

    public string Body { get; set; } = "";

    public DateTimeOffset Modified { get; set; }
}

/// <summary>notebook.json: every to-do list and every note, each in the order they were made.</summary>
public sealed class NotebookFile
{
    /// <summary>The shape this build writes.</summary>
    public const int Shape = 1;

    public int Version { get; set; } = Shape;

    public List<TodoList> Lists { get; set; } = [];

    public List<Note> Notes { get; set; } = [];

    /// <summary>The list <paramref name="id"/>; else the first, the one a line set to none shows; null when there is none.</summary>
    public TodoList? List(string? id) =>
        Lists.FirstOrDefault(list => list.Id == id) ?? Lists.FirstOrDefault();

    /// <summary>The note <paramref name="id"/>; else the first; null when there is none.</summary>
    public Note? Note(string? id) =>
        Notes.FirstOrDefault(note => note.Id == id) ?? Notes.FirstOrDefault();

    /// <summary>A deep copy, for a change made on the side and swapped in whole.</summary>
    public NotebookFile Copy() =>
        JsonSerializer.Deserialize(JsonSerializer.Serialize(this, NotebookJson.Default.NotebookFile), NotebookJson.Default.NotebookFile)!;

    /// <summary>
    /// Puts right what a hand edit or an older build may have left: no list missing, every id there
    /// and once, words trimmed and held to their most, a finished time only on what is done.
    /// </summary>
    public void Normalize()
    {
        Version = Shape;
        Lists = [.. (Lists ?? []).Where(list => list is not null)];
        Notes = [.. (Notes ?? []).Where(note => note is not null)];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        string Unique(string? id)
        {
            var kept = string.IsNullOrWhiteSpace(id) || !ids.Add(id) ? NewId(ids) : id;
            ids.Add(kept);
            return kept;
        }
        foreach (var list in Lists)
        {
            list.Id = Unique(list.Id);
            list.Name = Notebook.Name(list.Name);
            list.Items = [.. (list.Items ?? []).Where(item => item is not null)];
            foreach (var item in list.Items)
            {
                item.Id = Unique(item.Id);
                item.Text = Notebook.TaskText(item.Text);
                if (!item.Done)
                {
                    item.Finished = null;
                }
            }
        }
        foreach (var note in Notes)
        {
            note.Id = Unique(note.Id);
            note.Title = Notebook.Name(note.Title);
            note.Body = Notebook.Body(note.Body);
        }
    }

    /// <summary>An id nothing in <paramref name="taken"/> has.</summary>
    internal static string NewId(ISet<string> taken)
    {
        while (true)
        {
            var id = Guid.NewGuid().ToString("N")[..10];
            if (!taken.Contains(id))
            {
                return id;
            }
        }
    }
}

/// <summary>
/// A to-do list as a line shows it: the tasks not done in their order, then the done ones, the last
/// ticked off first (as Microsoft To Do lists its completed). Pure.
/// </summary>
public readonly record struct TodoView(IReadOnlyList<TodoItem> Open, IReadOnlyList<TodoItem> Completed)
{
    public static TodoView Of(TodoList? list) =>
        list is null
            ? new TodoView([], [])
            : new TodoView(
                [.. list.Items.Where(item => !item.Done)],
                [.. list.Items.Where(item => item.Done).OrderByDescending(item => item.Finished ?? DateTimeOffset.MinValue)]);
}

/// <summary>The store: the file as it is, and every change to it (see the top of this file).</summary>
public sealed class Notebook : IDisposable
{
    /// <summary>The most a list's or a note's name says, in characters.</summary>
    public const int MostName = 60;

    /// <summary>The most a task says, in characters.</summary>
    public const int MostTask = 500;

    /// <summary>The most a note's body holds, in characters: a note, not a book.</summary>
    public const int MostBody = 100_000;

    /// <summary>How long a change waits for the other app's to finish before going ahead anyway.</summary>
    private const int LockMilliseconds = 5000;

    private const string FileName = "notebook.json";

    private static readonly Lock Making = new();
    private static Notebook? shared;

    private readonly Lock writing = new();
    private readonly Mutex fileLock;
    private FileSystemWatcher? watcher;
    private Timer? settle;
    private NotebookFile current = new();

    // The text last read or written, to tell a change of somebody else's from this store's own save.
    private string? known;
    private bool disposed;

    /// <summary>The store kept in <paramref name="folder"/>: read now, watched only once asked (<see cref="Watch"/>).</summary>
    public Notebook(string folder)
    {
        Folder = folder;
        Path = System.IO.Path.Combine(folder, FileName);
        // One name per file, so the tests' folders do not wait on the person's.
        var name = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(Path.ToUpperInvariant())))[..16];
        fileLock = new Mutex(false, $@"Local\Karpo.Notebook.{name}"); // not-ui: a mutex name
        // What is there now, whatever it is: a backup read in place of a broken file has no text of
        // its own to compare, so this is not Reload's "the same as before".
        if (Read() is { } read)
        {
            known = read.Text;
            current = read.File;
        }
    }

    /// <summary>The one store of this process, in <see cref="Config.LocalFolder"/>.</summary>
    public static Notebook Shared
    {
        get
        {
            lock (Making)
            {
                return shared ??= new Notebook(Config.LocalFolder);
            }
        }
    }

    public string Folder { get; }

    public string Path { get; }

    /// <summary>The file as it is now. Never changed in place: a change swaps in another, so a reader may keep it.</summary>
    public NotebookFile Current => Volatile.Read(ref current);

    /// <summary>
    /// The lists or the notes changed, here or in the file: on the thread that changed them, or the
    /// watcher's. A listener that draws posts to its own thread.
    /// </summary>
    public event Action? Changed;

    // --- Changes ------------------------------------------------------------------------------------

    /// <summary>
    /// Applies <paramref name="edit"/> to the file as it is on disk now, under the lock, and saves it
    /// where it says it changed something. True when it did.
    /// </summary>
    public bool Change(Func<NotebookFile, bool> edit)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        bool changed;
        lock (writing)
        {
            var held = Hold();
            try
            {
                // What is on disk now: somebody else's change is the ground this one stands on. A file
                // held by another program is not nothing: the change stands on what was last read.
                if (Read() is { } read)
                {
                    known = read.Text;
                    Volatile.Write(ref current, read.File);
                }
                var next = Current.Copy();
                if (!edit(next))
                {
                    return false;
                }
                next.Normalize();
                var written = JsonSerializer.Serialize(next, NotebookJson.Default.NotebookFile);
                Write(written);
                known = written;
                Volatile.Write(ref current, next);
                changed = true;
            }
            finally
            {
                if (held)
                {
                    fileLock.ReleaseMutex();
                }
            }
        }
        if (changed)
        {
            Changed?.Invoke();
        }
        return changed;
    }

    /// <summary>A new list named <paramref name="name"/> (<see cref="NewListName"/> where it is blank), at the end; its id.</summary>
    public string AddList(string? name)
    {
        var id = "";
        Change(file =>
        {
            id = NotebookFile.NewId(Ids(file));
            file.Lists.Add(new TodoList { Id = id, Name = Name(name) is { Length: > 0 } given ? given : NewListName(file) });
            return true;
        });
        return id;
    }

    public bool RenameList(string id, string name) => Change(file =>
        file.Lists.FirstOrDefault(list => list.Id == id) is { } list && Name(name) is { Length: > 0 } given && list.Name != given
            && Set(() => list.Name = given));

    public bool DeleteList(string id) => Change(file => file.Lists.RemoveAll(list => list.Id == id) > 0);

    /// <summary>
    /// A task saying <paramref name="text"/> at the top of list <paramref name="list"/> — or of the
    /// first list, made where there is none, so the first task typed into a new line has somewhere to
    /// go — or with <paramref name="last"/> at its end, under the others, where a field at the list's
    /// foot typed it. Its id; null for words that are only spaces.
    /// </summary>
    public string? AddTask(string? list, string text, DateTimeOffset now, bool last = false)
    {
        var words = TaskText(text);
        if (words.Length == 0)
        {
            return null;
        }
        string? id = null;
        Change(file =>
        {
            var taken = Ids(file);
            if (file.List(list) is not { } into)
            {
                into = new TodoList { Id = NotebookFile.NewId(taken), Name = S.WidgetTodoNewList };
                taken.Add(into.Id);
                file.Lists.Add(into);
            }
            id = NotebookFile.NewId(taken);
            into.Items.Insert(last ? into.Items.Count : 0, new TodoItem { Id = id, Text = words, Created = now });
            return true;
        });
        return id;
    }

    /// <summary>Ticks task <paramref name="task"/> off, or puts it back; it keeps its place among the others.</summary>
    public bool SetDone(string task, bool done, DateTimeOffset now) => Change(file =>
        Task(file, task) is { } item && item.Done != done && Set(() =>
        {
            item.Done = done;
            item.Finished = done ? now : null;
        }));

    /// <summary>What task <paramref name="task"/> says; words that are only spaces delete it, as To Do's do.</summary>
    public bool SetText(string task, string text) => Change(file =>
    {
        var words = TaskText(text);
        if (Task(file, task) is not { } item || item.Text == words)
        {
            return false;
        }
        if (words.Length == 0)
        {
            return file.Lists.Any(list => list.Items.Remove(item));
        }
        item.Text = words;
        return true;
    });

    public bool DeleteTask(string task) => Change(file => file.Lists.Any(list => list.Items.RemoveAll(item => item.Id == task) > 0));

    /// <summary>
    /// Moves task <paramref name="task"/> to stand before <paramref name="before"/> in its list, or
    /// at the end where that is null or not in the list.
    /// </summary>
    public bool MoveTask(string task, string? before) => Change(file =>
    {
        foreach (var list in file.Lists)
        {
            var from = list.Items.FindIndex(item => item.Id == task);
            if (from < 0)
            {
                continue;
            }
            var order = Moved(list.Items.Select(item => item.Id).ToList(), task, before);
            if (order.SequenceEqual(list.Items.Select(item => item.Id)))
            {
                return false;
            }
            var byId = list.Items.ToDictionary(item => item.Id);
            list.Items = [.. order.Select(id => byId[id])];
            return true;
        }
        return false;
    });

    /// <summary>Every done task of list <paramref name="list"/> gone.</summary>
    public bool ClearCompleted(string list) => Change(file =>
        file.Lists.FirstOrDefault(each => each.Id == list) is { } found && found.Items.RemoveAll(item => item.Done) > 0);

    /// <summary>A new note named <paramref name="title"/> (<see cref="NewNoteName"/> where it is blank), at the end; its id.</summary>
    public string AddNote(string? title, DateTimeOffset now)
    {
        var id = "";
        Change(file =>
        {
            id = NotebookFile.NewId(Ids(file));
            file.Notes.Add(new Note { Id = id, Title = Name(title) is { Length: > 0 } given ? given : NewNoteName(file), Modified = now });
            return true;
        });
        return id;
    }

    public bool RenameNote(string id, string title) => Change(file =>
        file.Notes.FirstOrDefault(note => note.Id == id) is { } note && Name(title) is { Length: > 0 } given && note.Title != given
            && Set(() => note.Title = given));

    public bool DeleteNote(string id) => Change(file => file.Notes.RemoveAll(note => note.Id == id) > 0);

    /// <summary>
    /// What note <paramref name="note"/> says — or the first note, made where there is none, so the first
    /// words typed into a new line are kept. The note's id.
    /// </summary>
    public string SetBody(string? note, string body, DateTimeOffset now)
    {
        var text = Body(body);
        var id = note ?? "";
        Change(file =>
        {
            if (file.Note(note) is not { } into)
            {
                if (text.Length == 0)
                {
                    return false;
                }
                into = new Note { Id = NotebookFile.NewId(Ids(file)), Title = NewNoteName(file) };
                file.Notes.Add(into);
            }
            id = into.Id;
            if (into.Body == text)
            {
                return false;
            }
            into.Body = text;
            into.Modified = now;
            return true;
        });
        return id;
    }

    // --- The rules ----------------------------------------------------------------------------------

    /// <summary>
    /// §317: what a new note is called where nothing is typed: "Note", or "Note 2" and on where that is
    /// taken, as a new layout is named (BarLooks.NewName).
    /// </summary>
    public static string NewNoteName(NotebookFile file) => Numbered(file.Notes.Select(note => note.Title), S.WidgetNoteNewNote, S.WidgetNoteNewNoteNumbered);

    /// <summary>§317: what a new list is called where nothing is typed: "Tasks", or "Tasks 2" and on.</summary>
    public static string NewListName(NotebookFile file) => Numbered(file.Lists.Select(list => list.Name), S.WidgetTodoNewList, S.WidgetTodoNewListNumbered);

    private static string Numbered(IEnumerable<string> taken, string first, Func<int, string> numbered)
    {
        var names = taken.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        var name = first;
        for (var number = 2; names.Contains(name); number++)
        {
            name = numbered(number);
        }
        return name;
    }

    /// <summary>A list's or a note's name: one line, trimmed, at most <see cref="MostName"/> characters.</summary>
    public static string Name(string? name) => Clip(OneLine(name), MostName);

    /// <summary>A task's words: one line, trimmed, at most <see cref="MostTask"/> characters.</summary>
    public static string TaskText(string? text) => Clip(OneLine(text), MostTask);

    /// <summary>A note's body: its lines as \n, at most <see cref="MostBody"/> characters, nothing else changed.</summary>
    public static string Body(string? body)
    {
        var text = (body ?? "").ReplaceLineEndings("\n");
        return text.Length <= MostBody ? text : Clip(text, MostBody);
    }

    /// <summary>
    /// The order of <paramref name="ids"/> with <paramref name="moving"/> taken out and put back before
    /// <paramref name="before"/>, or at the end where that is null or not there. Pure.
    /// </summary>
    public static List<string> Moved(IReadOnlyList<string> ids, string moving, string? before)
    {
        var order = ids.Where(id => id != moving).ToList();
        if (order.Count == ids.Count)
        {
            return [.. ids];
        }
        var at = before is null ? -1 : order.IndexOf(before);
        order.Insert(at < 0 ? order.Count : at, moving);
        return order;
    }

    private static string OneLine(string? text)
    {
        var words = new StringBuilder((text ?? "").Length);
        foreach (var c in (text ?? "").ReplaceLineEndings("\n"))
        {
            words.Append(c is '\r' or '\n' or '\t' ? ' ' : char.IsControl(c) ? '\0' : c);
        }
        return words.Replace("\0", "").ToString().Trim();
    }

    private static string Clip(string text, int most)
    {
        if (text.Length <= most)
        {
            return text;
        }
        var length = char.IsHighSurrogate(text[most - 1]) ? most - 1 : most;
        return text[..length].TrimEnd();
    }

    private static bool Set(Action change)
    {
        change();
        return true;
    }

    private static TodoItem? Task(NotebookFile file, string id) =>
        file.Lists.SelectMany(list => list.Items).FirstOrDefault(item => item.Id == id);

    private static HashSet<string> Ids(NotebookFile file) =>
    [
        .. file.Lists.Select(list => list.Id),
        .. file.Lists.SelectMany(list => list.Items).Select(item => item.Id),
        .. file.Notes.Select(note => note.Id),
    ];

    // --- The file -----------------------------------------------------------------------------------

    /// <summary>
    /// Reads the file again; where it says something else than this store last read or wrote, it is
    /// what <see cref="Current"/> is, and <see cref="Changed"/> is raised. True when it changed.
    /// </summary>
    public bool Reload(bool raise = true)
    {
        lock (writing)
        {
            if (Read() is not { } read || read.Text == known)
            {
                return false;
            }
            known = read.Text;
            Volatile.Write(ref current, read.File);
        }
        if (raise)
        {
            Changed?.Invoke();
        }
        return true;
    }

    /// <summary>
    /// Follows the file: a change of the settings app's, or a hand edit, read a moment after it lands
    /// (a writer's two renames are one change) and told through <see cref="Changed"/>.
    /// </summary>
    public void Watch()
    {
        lock (writing)
        {
            if (watcher is not null || disposed)
            {
                return;
            }
            try
            {
                Directory.CreateDirectory(Folder);
                settle = new Timer(_ => Settled(), null, Timeout.Infinite, Timeout.Infinite);
                watcher = new FileSystemWatcher(Folder, FileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime,
                };
                watcher.Changed += Touched;
                watcher.Created += Touched;
                watcher.Renamed += Touched;
                watcher.Deleted += Touched;
                watcher.EnableRaisingEvents = true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Console.Error.WriteLine($"Karpo: {FileName} not watched: {error.Message}");
            }
        }
    }

    /// <summary>The file was touched: it is read once it has been still for a moment.</summary>
    private void Touched(object? sender, FileSystemEventArgs change)
    {
        try
        {
            settle?.Change(150, Timeout.Infinite);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // Disposed between the event and here: nothing to read for.
            Console.Error.WriteLine($"Karpo: {FileName} change not followed: {error.Message}");
        }
    }

    private void Settled()
    {
        try
        {
            if (!disposed)
            {
                Reload();
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"Karpo: {FileName} not read again: {error.Message}");
        }
    }

    /// <summary>
    /// The file as it is, and its text: an empty notebook and null where there is none; null where
    /// another program holds it, which is not the same as nothing. One that does not parse is kept
    /// aside and the backup read in its place (see the top of this file).
    /// </summary>
    private (NotebookFile File, string? Text)? Read()
    {
        switch (Parse(Path))
        {
            case { Found: Found.Read } read:
                return (read.File!, read.Text);
            case { Found: Found.Busy }:
                return null;
            case { Found: Found.None }:
                return (new NotebookFile(), null);
        }
        // There, and not a notebook: never written over. Kept under a name of its own.
        if (DurableFile.KeepBroken(Path) is not { } aside)
        {
            Console.Error.WriteLine($"Karpo: {FileName} does not read and could not be kept aside");
            return null;
        }
        Console.Error.WriteLine($"Karpo: {FileName} does not read; kept as {System.IO.Path.GetFileName(aside)}.");
        // The last good version takes its place, as config.json's does, so the next change stands on it.
        if (Parse(Path + ".bak") is not { Found: Found.Read } backup)
        {
            return (new NotebookFile(), null);
        }
        try
        {
            File.Copy(Path + ".bak", Path, overwrite: false);
            return (backup.File!, backup.Text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: {FileName}'s backup not put back: {error.Message}");
            return (backup.File!, null);
        }
    }

    /// <summary>What reading a notebook's file found.</summary>
    internal enum Found
    {
        /// <summary>No file.</summary>
        None,

        /// <summary>A notebook, normalized.</summary>
        Read,

        /// <summary>A file that is no notebook: not JSON, or not this shape.</summary>
        Unreadable,

        /// <summary>A file another program holds, or one this account may not read.</summary>
        Busy,
    }

    /// <summary>The notebook at <paramref name="path"/>, normalized, and its text, or why there is none.</summary>
    internal static (Found Found, NotebookFile? File, string? Text) Parse(string path)
    {
        switch (DurableFile.ReadText(path, out var read))
        {
            case FileRead.Missing:
                return (Found.None, null, null);
            case FileRead.Locked:
                Console.Error.WriteLine($"Karpo: {System.IO.Path.GetFileName(path)} not read: held by another program");
                return (Found.Busy, null, null);
        }
        var text = read!;
        try
        {
            if (JsonSerializer.Deserialize(text, NotebookJson.Default.NotebookFile) is not { } file)
            {
                return (Found.Unreadable, null, text);
            }
            file.Normalize();
            return (Found.Read, file, text);
        }
        catch (JsonException)
        {
            return (Found.Unreadable, null, text);
        }
    }

    private void Write(string text)
    {
        try
        {
            DurableFile.Write(Path, text);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: {FileName} not saved: {error.Message}");
        }
    }

    /// <summary>The other app's lock on the file, for a moment; false where it was not had in time (the change goes ahead).</summary>
    private bool Hold()
    {
        try
        {
            return fileLock.WaitOne(LockMilliseconds);
        }
        catch (AbandonedMutexException)
        {
            // The other app ended while holding it: it is this one's now.
            return true;
        }
    }

    public void Dispose()
    {
        lock (writing)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            watcher?.Dispose();
            watcher = null;
            settle?.Dispose();
            settle = null;
        }
        fileLock.Dispose();
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true)]
[JsonSerializable(typeof(NotebookFile))]
internal sealed partial class NotebookJson : JsonSerializerContext;
