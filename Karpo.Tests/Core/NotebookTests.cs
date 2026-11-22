// The notebook (Karpo.Core\Notebook.cs): the to-do lists and the notes both apps read and change.
// Each test has a folder of its own under the run's isolated one, so nothing here reaches the
// person's %LOCALAPPDATA%\Karpo, and no two tests share a file.

using System.Text.Json;

namespace Karpo.Tests.Core;

public sealed class NotebookTests : IDisposable
{
    private readonly string folder = Path.Combine(Isolation.Folder, "notebook", Guid.NewGuid().ToString("N"));

    private static readonly DateTimeOffset Monday = new(2026, 10, 12, 9, 0, 0, TimeSpan.FromHours(2));

    public void Dispose()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private Notebook Open() => new(folder);

    private string FilePath => Path.Combine(folder, "notebook.json");

    [Fact]
    public void ItIsKeptInTheLocalFolderTheTestsMove()
    {
        Assert.StartsWith(Isolation.Folder, Config.LocalFolder, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Path.Combine(Config.LocalFolder, "notebook.json"), Notebook.Shared.Path);
    }

    [Fact]
    public void ListsTasksAndNotesSurviveARoundTrip()
    {
        using (var book = Open())
        {
            var list = book.AddList("Groceries");
            var milk = book.AddTask(list, "Milk", Monday)!;
            book.AddTask(list, "Bread", Monday.AddMinutes(1));
            Assert.True(book.SetDone(milk, true, Monday.AddHours(1)));
            var note = book.AddNote("Ideas", Monday);
            book.SetBody(note, "# Ideas\r\n- one", Monday.AddMinutes(5));
        }
        using var again = Open();
        var file = again.Current;
        var groceries = Assert.Single(file.Lists);
        Assert.Equal("Groceries", groceries.Name);
        // A new task stands at the top, as To Do's "Add a task" puts it.
        Assert.Equal(["Bread", "Milk"], groceries.Items.Select(item => item.Text));
        Assert.True(groceries.Items[1].Done);
        Assert.Equal(Monday.AddHours(1), groceries.Items[1].Finished);
        var ideas = Assert.Single(file.Notes);
        Assert.Equal("Ideas", ideas.Title);
        Assert.Equal("# Ideas\n- one", ideas.Body);
        Assert.Equal(Monday.AddMinutes(5), ideas.Modified);
        // The version before is kept beside it.
        Assert.True(File.Exists(FilePath + ".bak"));
    }

    [Fact]
    public void TheFirstTaskOfAnEmptyNotebookMakesAList()
    {
        using var book = Open();
        Assert.Null(book.Current.List(null));
        var task = book.AddTask(null, "  Call the bank  ", Monday);
        Assert.NotNull(task);
        var list = Assert.Single(book.Current.Lists);
        Assert.Equal("Tasks", list.Name);
        Assert.Equal("Call the bank", Assert.Single(list.Items).Text);
        // Words that are only spaces are no task.
        Assert.Null(book.AddTask(null, "   ", Monday));
    }

    [Fact]
    public void AListThatIsGoneIsTheFirst()
    {
        using var book = Open();
        var first = book.AddList("One");
        var second = book.AddList("Two");
        Assert.Equal(second, book.Current.List(second)!.Id);
        Assert.Equal(first, book.Current.List("gone")!.Id);
        Assert.Equal(first, book.Current.List(null)!.Id);
        Assert.True(book.DeleteList(first));
        Assert.Equal(second, book.Current.List(first)!.Id);
    }

    /// <summary>The to-do line's field stands at the list's foot, so what it adds goes under the others, done ones and all.</summary>
    [Fact]
    public void ATaskAddedLastGoesUnderTheOthers()
    {
        using var book = Open();
        var list = book.AddList("Work");
        book.AddTask(list, "A", Monday);
        var done = book.AddTask(list, "Done", Monday, last: true)!;
        Assert.True(book.SetDone(done, true, Monday));
        book.AddTask(list, "B", Monday, last: true);
        Assert.Equal(["A", "Done", "B"], book.Current.List(list)!.Items.Select(item => item.Text));
        Assert.Equal(["A", "B"], TodoView.Of(book.Current.List(list)).Open.Select(item => item.Text));
    }

    [Fact]
    public void TasksAreTickedRenamedMovedAndDeleted()
    {
        using var book = Open();
        var list = book.AddList("Work");
        var c = book.AddTask(list, "C", Monday)!;
        var b = book.AddTask(list, "B", Monday)!;
        var a = book.AddTask(list, "A", Monday)!;
        string[] Order() => [.. book.Current.List(list)!.Items.Select(item => item.Text)];
        Assert.Equal(["A", "B", "C"], Order());

        Assert.True(book.MoveTask(a, null));
        Assert.Equal(["B", "C", "A"], Order());
        Assert.True(book.MoveTask(a, b));
        Assert.Equal(["A", "B", "C"], Order());
        Assert.False(book.MoveTask(a, b), "already there: nothing written");

        Assert.True(book.SetText(b, "Bee"));
        Assert.False(book.SetText(b, "Bee"));
        Assert.Equal(["A", "Bee", "C"], Order());
        // A task's words taken away delete it, as To Do's do.
        Assert.True(book.SetText(c, " "));
        Assert.Equal(["A", "Bee"], Order());

        Assert.True(book.SetDone(a, true, Monday));
        Assert.False(book.SetDone(a, true, Monday));
        Assert.True(book.SetDone(a, false, Monday));
        Assert.Null(book.Current.List(list)!.Items[0].Finished);
        Assert.True(book.DeleteTask(a));
        Assert.Equal(["Bee"], Order());
        Assert.False(book.DeleteTask(a));
    }

    [Fact]
    public void ADoneTaskKeepsItsPlaceAndTheViewListsTheLastDoneFirst()
    {
        using var book = Open();
        var list = book.AddList("Home");
        foreach (var words in new[] { "4", "3", "2", "1" })
        {
            book.AddTask(list, words, Monday);
        }
        var items = book.Current.List(list)!.Items;
        book.SetDone(items[1].Id, true, Monday.AddMinutes(1));
        book.SetDone(items[2].Id, true, Monday.AddMinutes(2));
        var view = TodoView.Of(book.Current.List(list));
        Assert.Equal(["1", "4"], view.Open.Select(item => item.Text));
        Assert.Equal(["3", "2"], view.Completed.Select(item => item.Text));
        // Put back, it is where it was.
        book.SetDone(items[1].Id, false, Monday);
        Assert.Equal(["1", "2", "4"], TodoView.Of(book.Current.List(list)).Open.Select(item => item.Text));
        Assert.True(book.ClearCompleted(list));
        Assert.Equal(["1", "2", "4"], book.Current.List(list)!.Items.Select(item => item.Text));
    }

    [Fact]
    public void NamesAndWordsAreOneLineTrimmedAndHeldToTheirMost()
    {
        Assert.Equal("Two words", Notebook.Name("  Two\r\nwords \t"));
        Assert.Equal(Notebook.MostName, Notebook.Name(new string('x', 500)).Length);
        Assert.Equal(Notebook.MostTask, Notebook.TaskText(new string('y', 900)).Length);
        Assert.Equal("a\nb\nc", Notebook.Body("a\r\nb\rc"));
        // Never half a surrogate pair at the cut.
        var emoji = new string('x', Notebook.MostName - 1) + "😀";
        Assert.False(char.IsHighSurrogate(Notebook.Name(emoji)[^1]));
        using var book = Open();
        var list = book.AddList("Old");
        Assert.False(book.RenameList(list, "   "), "a blank name is no name");
        Assert.True(book.RenameList(list, "New"));
        Assert.Equal("New", book.Current.Lists[0].Name);
        var unnamed = book.AddList(null);
        Assert.Equal("Tasks", book.Current.List(unnamed)!.Name);
    }

    [Fact]
    public void MovedIsPure()
    {
        Assert.Equal(["b", "a", "c"], Notebook.Moved(["a", "b", "c"], "a", "c"));
        Assert.Equal(["b", "c", "a"], Notebook.Moved(["a", "b", "c"], "a", null));
        Assert.Equal(["a", "b", "c"], Notebook.Moved(["a", "b", "c"], "x", "a"));
        Assert.Equal(["b", "c", "a"], Notebook.Moved(["a", "b", "c"], "a", "gone"));
    }

    [Fact]
    public void NotesAreMadeRenamedAndDeleted()
    {
        using var book = Open();
        // Words typed into a line with no note make one.
        var made = book.SetBody(null, "first words", Monday);
        Assert.Equal("Note", book.Current.Note(made)!.Title);
        Assert.Equal(made, book.SetBody(made, "first words", Monday));
        var second = book.AddNote("Shopping", Monday);
        Assert.True(book.RenameNote(second, "Errands"));
        Assert.Equal(["Note", "Errands"], book.Current.Notes.Select(note => note.Title));
        Assert.True(book.DeleteNote(made));
        Assert.Equal(second, book.Current.Note(made)!.Id);
        // Nothing typed into a notebook without notes makes nothing.
        Assert.True(book.DeleteNote(second));
        book.SetBody(null, "", Monday);
        Assert.Empty(book.Current.Notes);
    }

    /// <summary>The settings app and the dock each change the file: neither loses the other's change.</summary>
    [Fact]
    public void TwoStoresOnOneFileKeepEachOthersChanges()
    {
        using var dock = Open();
        using var settings = Open();
        var list = dock.AddList("Shared");
        // The settings app has not read the file since; its change stands on the dock's.
        settings.AddNote("From settings", Monday);
        dock.AddTask(list, "From the dock", Monday);
        using var third = Open();
        Assert.Equal("Shared", Assert.Single(third.Current.Lists).Name);
        Assert.Equal("From the dock", Assert.Single(third.Current.Lists[0].Items).Text);
        Assert.Equal("From settings", Assert.Single(third.Current.Notes).Title);
        // The one that did not write it reads it again and says so.
        var heard = 0;
        settings.Changed += () => heard++;
        Assert.True(settings.Reload());
        Assert.Equal(1, heard);
        Assert.False(settings.Reload(), "nothing new");
    }

    /// <summary>Many changes at once, from threads and two stores, are each written once.</summary>
    [Fact]
    public void ChangesAtOnceAreAllKept()
    {
        using var one = Open();
        using var two = Open();
        var list = one.AddList("Busy");
        Parallel.For(0, 40, index => (index % 2 == 0 ? one : two).AddTask(list, $"Task {index}", Monday));
        using var after = Open();
        Assert.Equal(40, after.Current.List(list)!.Items.Select(item => item.Text).Distinct().Count());
    }

    [Fact]
    public void ChangedIsRaisedForEveryChangeAndNotForNothing()
    {
        using var book = Open();
        var heard = 0;
        book.Changed += () => heard++;
        var list = book.AddList("L");
        Assert.Equal(1, heard);
        Assert.False(book.RenameList(list, "L"));
        Assert.Equal(1, heard);
    }

    [Fact]
    public void AFileThatDoesNotReadIsKeptAsideAndTheBackupRead()
    {
        using (var book = Open())
        {
            book.AddList("Kept");
            book.AddList("Second save");
        }
        File.WriteAllText(FilePath, "{ this is not json");
        using var again = Open();
        // The backup is the save before the last: the first list, never nothing.
        Assert.Equal("Kept", Assert.Single(again.Current.Lists).Name);
        var aside = Assert.Single(Directory.GetFiles(folder, "notebook.json.broken-*"));
        Assert.Equal("{ this is not json", File.ReadAllText(aside));
        // The next change writes a good file again.
        again.AddList("After");
        using var third = Open();
        Assert.Equal(["Kept", "After"], third.Current.Lists.Select(list => list.Name));
    }

    [Fact]
    public void AnOddFileIsPutRight()
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, """
            {
              // a hand edit
              "version": 1,
              "lists": [
                { "id": "a", "name": "  One  ", "items": [ { "id": "x", "text": "first", "done": false, "finished": "2026-10-01T10:00:00+02:00" }, null ] },
                { "id": "a", "items": null },
                null
              ],
              "notes": [ { "title": "N", "body": "line\r\nline", "unknown": 3 } ],
            }
            """);
        using var book = Open();
        var file = book.Current;
        Assert.Equal(2, file.Lists.Count);
        Assert.Equal("One", file.Lists[0].Name);
        Assert.Null(file.Lists[0].Items[0].Finished);
        Assert.Single(file.Lists[0].Items);
        Assert.NotEqual(file.Lists[0].Id, file.Lists[1].Id);
        Assert.Empty(file.Lists[1].Items);
        Assert.False(string.IsNullOrEmpty(file.Notes[0].Id));
        Assert.Equal("line\nline", file.Notes[0].Body);
    }

    /// <summary>A file another program holds is not an empty notebook: a change stands on what was read, and nothing is lost.</summary>
    [Fact]
    public void AFileHeldElsewhereIsNotWrittenOverWithNothing()
    {
        using var book = Open();
        var list = book.AddList("Precious");
        book.AddTask(list, "Keep me", Monday);
        using (new FileStream(FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.False(book.Reload(), "held: nothing read, nothing changed");
            Assert.Equal("Precious", Assert.Single(book.Current.Lists).Name);
        }
        book.AddTask(list, "And me", Monday);
        using var after = Open();
        Assert.Equal(["And me", "Keep me"], after.Current.List(list)!.Items.Select(item => item.Text));
    }

    [Fact]
    public void TheFileIsCamelCaseJsonAPersonCanRead()
    {
        using var book = Open();
        book.AddList("Readable");
        var text = File.ReadAllText(FilePath);
        Assert.Contains("\n", text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("Readable", json.RootElement.GetProperty("lists")[0].GetProperty("name").GetString());
    }

    /// <summary>The watcher hears a change made by another store, as the dock hears the settings app's.</summary>
    [Fact]
    public async Task TheWatcherHearsAnotherStoresChange()
    {
        using var dock = Open();
        dock.AddList("Start");
        dock.Watch();
        var heard = new TaskCompletionSource();
        dock.Changed += () => heard.TrySetResult();
        using var settings = Open();
        settings.AddNote("Elsewhere", Monday);
        Assert.Same(heard.Task, await Task.WhenAny(heard.Task, Task.Delay(5000)));
        Assert.Equal("Elsewhere", Assert.Single(dock.Current.Notes).Title);
    }

    [Fact]
    public void AWidgetsListAndNoteAreItsOptions()
    {
        var todo = ListKind.Todo.New();
        todo.Normalize();
        Assert.NotNull(todo.Todo);
        Assert.Equal("", todo.Todo!.List);
        Assert.Null(todo.Note);
        todo.Todo.List = "  abc ";
        todo.Normalize();
        Assert.Equal("abc", todo.Todo.List);
        var note = ListKind.Note.New();
        note.Normalize();
        Assert.NotNull(note.Note);
        Assert.Null(note.Todo);
        // Kept through a copy, as the file keeps it.
        note.Note!.Note = "n1";
        Assert.Equal("n1", note.Clone().Note!.Note);
    }
}
