// The to-do and the note lines (docs\widgets.md §87): what a press, a key and a character do to the
// notebook, how tall each is and what each shows when held shorter, the note's live editor (every
// mark shown on every line, the keys, a long note scrolled), the to-do text box's caret moving
// between wrapped lines, which list or note a line
// shows, and sheets of both at 100 % and 225 % text, dark and light. Each test has a notebook of its
// own in the run's isolated folder (NotebookTile.Stand), never the person's. Needs Direct2D, so it
// runs only with KARPO_UI_TESTS=1; the sheets go to KARPO_PNG_DIR.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[CollectionDefinition(Collection, DisableParallelization = true)]
public sealed class NotebookLines
{
    public const string Collection = "notebook lines";
}

[Collection(NotebookLines.Collection)]
public sealed unsafe class NotebookLinesTests
{
    // Before any test's "now", so a task ticked off in a test is the last done.
    private static readonly DateTimeOffset Monday = new(2026, 1, 5, 9, 0, 0, TimeSpan.FromHours(1));

    internal const string SampleNote = """
        # Week plan
        Ship the **notes** widget, then *rest*. See [the docs](https://learn.microsoft.com/windows) or https://karpo.app.

        ## Tasks
        - [x] Write the parser
        - [ ] Draw the `NoteLayout`
          - [ ] Nested check
        1. First
        2. Second with ~~old~~ new words

        > A quote that runs long enough to wrap onto a second line in a narrow list.

        ```
        dotnet test -c Release
        ```

        ---
        Last words.
        """;

    /// <summary>The sample as the notebook keeps it, its lines \n each, where the places in it are counted.</summary>
    private static readonly string Sample = SampleNote.ReplaceLineEndings("\n");

    /// <summary>A notebook of its own for <paramref name="body"/>, the lines' while it runs.</summary>
    private static void WithBook(Action<Notebook> body)
    {
        var folder = Path.Combine(Isolation.Folder, "notebook-lines", Guid.NewGuid().ToString("N"));
        using var book = new Notebook(folder);
        NotebookTile.Stand = book;
        var opened = new List<string>();
        NotebookTile.Opening = opened.Add;
        try
        {
            body(book);
        }
        finally
        {
            NotebookTile.Stand = null;
            NotebookTile.Opening = null;
        }
    }

    /// <summary>A list of tasks, two of them done.</summary>
    private static string Groceries(Notebook book)
    {
        var list = book.AddList("Groceries");
        // Each new task stands at the top: the list reads Eggs, Coffee beans, Bread, Milk.
        foreach (var words in new[] { "Milk", "Bread", "Coffee beans", "Eggs" })
        {
            book.AddTask(list, words, Monday);
        }
        var items = book.Current.List(list)!.Items;
        book.SetDone(items[2].Id, true, Monday.AddMinutes(5));
        book.SetDone(items[3].Id, true, Monday.AddMinutes(9));
        return list;
    }

    private static TileStyle Style(Look look, float scale = 1) => FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));

    private static D2D_RECT_F Box(FolderPlan plan, int index) =>
        PopupPaint.Rect(0, 0, plan.Cards[index].right - plan.Cards[index].left, plan.Cards[index].bottom - plan.Cards[index].top);

    private static Vector2 Centre(D2D_RECT_F box) => new((box.left + box.right) / 2, (box.top + box.bottom) / 2);

    private static LinePiece Piece(ILinePieces line, D2D_RECT_F box, TileStyle style, string name) =>
        line.LinePieces(box, style).First(piece => piece.Name == name);

    private static void Click(CellIsland line, Vector2 at, D2D_RECT_F box, TileStyle style)
    {
        Assert.True(line.PressOn(at, box, style, WidgetSurface.List));
        line.DragOn(at, box, style, WidgetSurface.List);
        line.ReleaseOn(WidgetSurface.List);
    }

    private static void Type(CellIsland line, string words)
    {
        foreach (var c in words)
        {
            Assert.True(line.CharOn(c, WidgetSurface.List));
        }
    }

    /// <summary>Paints the line once, as the folder would, so it knows where its rows stand.</summary>
    private static void Drawn(CellIsland line, D2D_RECT_F box, TileStyle style) =>
        Bitmap(box.right, box.bottom, dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });

    // --- To-do ----------------------------------------------------------------------------------------

    [UiFact]
    public void ATaskIsTypedTickedRenamedAndDeleted() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var list = Groceries(book);
            var look = LookFor(dark: true);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Todo));
            var todo = (TodoTile)folder.Tiles[0].Island;
            var (_, plan) = Plan(folder, look, 1);
            var box = Box(plan, 0);
            Drawn(todo, box, style);
            Assert.Equal(["Eggs", "Coffee beans"], todo.View.Open.Select(item => item.Text));

            // "Add a task", at the line's foot: typed, Enter adds it under the others, just over the
            // field, and types the next; Escape keeps that one too.
            var pieces = ((ILinePieces)todo).LinePieces(box, style);
            Assert.Equal("add", pieces[^1].Name);
            Assert.True(Piece(todo, box, style, "add").Box.top >= pieces.Where(piece => piece.Name.StartsWith("task ", StringComparison.Ordinal)).Max(piece => piece.Box.bottom) - 0.5f);
            Click(todo, Centre(Piece(todo, box, style, "add").Box), box, style);
            Assert.True(todo.Adding);
            Assert.True(todo.TypingOn);
            Type(todo, "Tea");
            Assert.True(todo.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Assert.True(todo.Adding, "still typing, for the next");
            Assert.Equal(["Eggs", "Coffee beans", "Tea"], todo.View.Open.Select(item => item.Text));
            Type(todo, "Jam");
            Assert.True(todo.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
            Assert.False(todo.TypingOn);
            Assert.Equal(["Eggs", "Coffee beans", "Tea", "Jam"], todo.View.Open.Select(item => item.Text));

            // A box ticks: the task goes under "Completed".
            box = Box(Plan(folder, look, 1).Plan, 0);
            var tea = todo.View.Open[2];
            Click(todo, Centre(Piece(todo, box, style, $"box {tea.Id}").Box), box, style);
            Assert.True(book.Current.List(list)!.Items.Single(item => item.Id == tea.Id).Done);
            Assert.Equal(tea.Id, todo.View.Completed[0].Id);

            // A double click renames; what is typed replaces the words, Enter keeps them.
            box = Box(Plan(folder, look, 1).Plan, 0);
            var jam = todo.View.Open[^1];
            var words = Centre(Piece(todo, box, style, $"task {jam.Id}").Box);
            Click(todo, words, box, style);
            Click(todo, words, box, style);
            Assert.Equal(jam.Id, todo.Renaming);
            todo.Editor.Editing.SelectAll();
            Type(todo, "Apricot jam");
            Assert.True(todo.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Assert.Null(todo.Renaming);
            Assert.Equal("Apricot jam", book.Current.List(list)!.Items.Single(item => item.Id == jam.Id).Text);

            // The keys: Down chooses the first task; Delete deletes it.
            var eggs = todo.View.Open[0];
            Assert.True(todo.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.True(todo.KeyOn(VK.VK_DELETE, WidgetSurface.List));
            Assert.DoesNotContain(book.Current.List(list)!.Items, item => item.Id == eggs.Id);
            // Past the last row, the keys are the folder's again.
            var steps = 0;
            while (todo.KeyOn(VK.VK_DOWN, WidgetSurface.List))
            {
                Assert.True(++steps < 20, "Down never let the keys go");
            }

            // A letter typed with the focus on the line starts a task with it; a space does not.
            Assert.False(todo.CharOn(' ', WidgetSurface.List));
            Assert.True(todo.CharOn('Z', WidgetSurface.List));
            Assert.True(todo.Adding);
            Assert.Equal("Z", todo.Editor.Text);
            // A press elsewhere in the folder (or the folder closing) keeps it.
            todo.StopTypingOn();
            Assert.Equal("Z", todo.View.Open[^1].Text);
            folder.Closed();
            folder.Dispose();
        }));

    [UiFact]
    public void ATaskIsDraggedIntoAnotherPlace() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var list = book.AddList("Order");
            foreach (var words in new[] { "D", "C", "B", "A" })
            {
                book.AddTask(list, words, Monday);
            }
            var look = LookFor(dark: false);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Todo));
            var todo = (TodoTile)folder.Tiles[0].Island;
            var box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(todo, box, style);
            var a = todo.View.Open[0];
            var c = todo.View.Open[2];
            var start = Centre(Piece(todo, box, style, $"task {a.Id}").Box);
            var onto = Piece(todo, box, style, $"task {c.Id}").Box;
            // Pressed on A's words, dragged to the top half of C, let go: A stands before C.
            Assert.True(todo.PressOn(start, box, style, WidgetSurface.List));
            todo.DragOn(start with { Y = onto.top + 4 }, box, style, WidgetSurface.List);
            todo.ReleaseOn(WidgetSurface.List);
            Assert.Equal(["B", "A", "C", "D"], todo.View.Open.Select(item => item.Text));
            // Alt+Down moves the chosen task one down, from the keys.
            Assert.True(todo.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.True(todo.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            folder.Closed();
            folder.Dispose();
        }));

    /// <summary>
    /// As tall as the list; held shorter, every row inside the line, and the rest scroll under the
    /// list's name (ListScroll.cs) rather than being counted.
    /// </summary>
    [UiFact]
    public void ALongListScrollsToTheRest() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var list = book.AddList("Long");
            for (var index = 0; index < 12; index++)
            {
                book.AddTask(list, $"Task {index}", Monday);
            }
            var look = LookFor(dark: true);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Todo), Line(ListKind.Todo, line => line.Height = LineHeight.Exactly(12)));
            var (natural, plan) = Plan(folder, look, 1);
            var todo = (TodoTile)folder.Tiles[0].Island;
            var row = PopupPaint.Round(40 * style.Scale * style.TextScale);
            // "Add a task" the foot, its band the line's bottom edge (§436).
            Assert.Equal(14 * row, natural[0]);
            var held = (TodoTile)folder.Tiles[1].Island;
            var box = Box(plan, 1);
            var pieces = held.LinePieces(box, style);
            // "Add a task" at the end of the list, nothing counted: the rest scroll.
            Assert.Equal("add", pieces[^1].Name);
            Assert.DoesNotContain(pieces, piece => piece.Name == "more");
            var shown = pieces.Count(piece => piece.Name.StartsWith("task ", StringComparison.Ordinal));
            Assert.True(shown >= 1);
            Assert.All(pieces, piece => Assert.True(piece.Box.bottom <= box.bottom + 0.5f, $"{piece.Name} ends at {piece.Box.bottom}, past {box.bottom}"));
            var laid = held.Scrolled(box, style, WidgetSurface.List);
            Assert.True(laid.Scrolls);
            Assert.Equal(row, laid.Head - style.Px(2), 0.5f);
            Assert.Equal(12, held.LinePieces(laid.Moved, style).Count(piece => piece.Name.StartsWith("task ", StringComparison.Ordinal)));
            _ = todo;
            folder.Closed();
            folder.Dispose();
        }));

    /// <summary>
    /// "Add a task" is on the line's foot (2026-10-11: "the add task button should be aligned on the
    /// bottom of the task widget"): a line taller than its tasks keeps the room above the field, and
    /// a line that scrolls keeps the field at its bottom while the tasks pass under it.
    /// </summary>
    [UiFact]
    public void AddATaskStaysOnTheFoot() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var list = book.AddList("Short");
            book.AddTask(list, "Only", Monday);
            var look = LookFor(dark: true);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Todo, line => line.Height = LineHeight.Exactly(12)));
            var (_, plan) = Plan(folder, look, 1);
            var todo = (TodoTile)folder.Tiles[0].Island;
            var box = Box(plan, 0);
            var pieces = todo.LinePieces(box, style);
            var add = pieces.Single(piece => piece.Name == "add").Box;
            var task = pieces.Single(piece => piece.Name.StartsWith("task ", StringComparison.Ordinal)).Box;
            Assert.Equal(box.bottom, add.bottom, 1);
            Assert.True(add.top - task.bottom > 40, "the room is between the task and the field");

            for (var index = 0; index < 12; index++)
            {
                book.AddTask(list, $"Task {index}", Monday);
            }
            foreach (var offset in new[] { 0f, 60f, float.MaxValue })
            {
                todo.ListScroll = offset;
                var laid = todo.Scrolled(box, style, WidgetSurface.List);
                Assert.True(laid.Scrolls);
                Assert.True(laid.Foot > 0);
                var field = todo.LinePieces(laid.End, style).Single(piece => piece.Name == "add").Box;
                Assert.True(field.top >= laid.FootBand.top - 0.5f && field.bottom <= box.bottom + 0.5f, $"the field at {field.top}..{field.bottom}, the foot {laid.FootBand.top}..{laid.FootBand.bottom}");
                // A press on it types a task, at any offset.
                Assert.True(todo.PressOn(new System.Numerics.Vector2((field.left + field.right) / 2, (field.top + field.bottom) / 2), box, style, WidgetSurface.List));
                todo.ReleaseOn(WidgetSurface.List);
                Assert.True(todo.Adding);
                todo.StopTypingOn();
            }
            folder.Closed();
            folder.Dispose();
        }));

    // --- Notes ----------------------------------------------------------------------------------------

    /// <summary>
    /// Escape in the open folder, one thing at a time: a line that is typing ends its typing (and
    /// keeps what was typed), then an app picked in the folder is let go (Channel.Escapes), and only
    /// then does the folder close. Neither of the first two may close it.
    /// </summary>
    [UiFact]
    public void EscapeEndsTypingThenLetsThePickGoThenCloses() =>
        Karpo.Tests.Edit.WindowShots.OnHiddenDesktop("notes-escape", (gfx, instance) => WithBook(book =>
        {
            var id = book.AddNote("Scratch", Monday);
            book.SetBody(id, "Hello", Monday);
            var look = LookFor(dark: true);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Note));
            try
            {
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, look, 1f, new POINT { x = 1700, y = 1040 }, new RECT { right = 1920, bottom = 1040 }, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
                Assert.True(flyout.IsOpen);
                var note = (NoteTile)folder.Tiles[0].Island;
                var box = Box(Plan(folder, look, 1).Plan, 0);
                Drawn(note, box, style);
                Click(note, Centre(Piece(note, box, style, "paragraph 0").Box), box, style);
                Assert.True(note.Typing);
                note.Editor.End(extend: false);
                ((IWidgetContainer)folder).Publish(Channels.App, new AppId("editor.exe"));
                FolderFlyoutEvent Escape() => flyout.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0);

                // The characters go to the line's text box, not to the folder.
                Assert.Equal(default, flyout.Message(WM.WM_CHAR, (WPARAM)(nuint)'!', 0));

                // 1: the typing ends, what was typed kept; the pick and the folder stay.
                Assert.Equal(FolderFlyoutAction.None, Escape().Kind);
                Assert.False(note.Typing);
                Assert.Equal("Hello!", book.Current.Note(id)!.Body);
                Assert.Equal(new AppId("editor.exe"), folder.ChannelValue(Channels.App));
                Assert.True(flyout.IsOpen, "Escape that ended the typing closed the folder");

                // 2: the pick is let go; the folder stays.
                Assert.Equal(FolderFlyoutAction.None, Escape().Kind);
                Assert.Null(folder.ChannelValue(Channels.App));
                Assert.True(flyout.IsOpen, "Escape that let the pick go closed the folder");

                // 3: nothing left to take back, so the folder closes.
                Assert.Equal(FolderFlyoutAction.Dismissed, Escape().Kind);
                Assert.False(flyout.IsOpen);
            }
            finally
            {
                folder.Dispose();
            }
        }));

    [UiFact]
    public void ANoteIsEditedInPlaceAndKeptOnEscape() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var id = book.AddNote("Scratch", Monday);
            book.SetBody(id, "Hello", Monday);
            var look = LookFor(dark: true);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Note));
            var note = (NoteTile)folder.Tiles[0].Island;
            var box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(note, box, style);
            // A click on the body puts the caret there, and the keys are the note's.
            var hello = Piece(note, box, style, "paragraph 0").Box;
            Click(note, new Vector2(hello.left + 1, Centre(hello).Y), box, style);
            Assert.True(note.Typing);
            Assert.Equal("Hello", note.Editor.Text);
            Assert.Equal(0, note.Editor.Caret);
            Assert.True(note.KeyOn(VK.VK_END, WidgetSurface.List));
            Assert.Equal(5, note.Editor.Caret);
            Type(note, ", world");
            Assert.True(note.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Type(note, "- [ ] new");
            Assert.True(note.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
            Assert.False(note.Typing);
            Assert.Equal("Hello, world\n- [ ] new", book.Current.Note(id)!.Body);
            // Enter on the line edits it again; the folder closing keeps what was typed.
            Assert.True(note.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Type(note, "!");
            folder.Closed();
            Assert.EndsWith("new!", book.Current.Note(id)!.Body);
            folder.Dispose();
        }));

    [UiFact]
    public void ALinkOpensAndATasksBoxTicksTheNote() =>
        OnStaThread(gfx =>
        {
            var folderName = Path.Combine(Isolation.Folder, "notebook-lines", Guid.NewGuid().ToString("N"));
            using var book = new Notebook(folderName);
            NotebookTile.Stand = book;
            var opened = new List<string>();
            NotebookTile.Opening = opened.Add;
            try
            {
                var id = book.AddNote("Links", Monday);
                book.SetBody(id, "[Karpo](https://karpo.app)\n\n- [ ] tick me", Monday);
                var look = LookFor(dark: false);
                var style = Style(look);
                var folder = Folder(gfx, Line(ListKind.Note));
                var note = (NoteTile)folder.Tiles[0].Island;
                var box = Box(Plan(folder, look, 1).Plan, 0);
                Drawn(note, box, style);
                var link = Piece(note, box, style, "paragraph 0").Box;
                // On the link's words, past its "[", which shows (§321).
                var words = link.left + PopupPaint.Measure(gfx, "[K", style.Text(14), Weight.Regular).X;
                Click(note, new Vector2(words, Centre(link).Y), box, style);
                Assert.Equal(["https://karpo.app"], opened);
                Assert.False(note.Typing, "a link opens; it does not edit");
                Assert.Equal("https://karpo.app", note.TipOn(new Vector2(words, Centre(link).Y), box, style, WidgetSurface.List)?.Text);
                // The task's "[ ]", after its "- ": a click there ticks it, and again clears it.
                var task = Piece(note, box, style, "task 2").Box;
                var square = new Vector2(task.left + PopupPaint.Measure(gfx, "- [", style.Text(14), Weight.Regular).X, Centre(task).Y);
                Click(note, square, box, style);
                Assert.Equal("[Karpo](https://karpo.app)\n\n- [x] tick me", book.Current.Note(id)!.Body);
                Assert.False(note.Typing);
                Assert.False(note.TextOn(square, box, style), "the box is the arrow's, not the text cursor's");
                // A click on the words is the caret's, not the box's.
                Click(note, new Vector2(task.left + PopupPaint.Measure(gfx, "- [x] tick m", style.Text(14), Weight.Regular).X, Centre(task).Y + 4), box, style);
                Assert.True(note.Typing);
                Assert.Equal("[Karpo](https://karpo.app)\n\n- [x] tick me", note.Editor.Text);
                Drawn(note, box, style);
                Click(note, new Vector2(square.X, square.Y - 4), box, style);
                Assert.Equal("[Karpo](https://karpo.app)\n\n- [ ] tick me", note.Editor.Text);
                folder.Closed();
                folder.Dispose();
            }
            finally
            {
                NotebookTile.Stand = null;
                NotebookTile.Opening = null;
            }
        });

    /// <summary>
    /// A note held shorter than it is scrolls inside its line: the wheel moves it (and is the list's
    /// once it can go no further), the caret is kept in view, and no ink is drawn past the line.
    /// </summary>
    [UiFact]
    public void ALongNoteScrollsInsideItsLine() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var id = book.AddNote("Plan", Monday);
            book.SetBody(id, SampleNote, Monday);
            foreach (var textScale in TextScales)
            {
                var look = LookFor(dark: true, textScale);
                var style = Style(look);
                var folder = Folder(gfx, Line(ListKind.Note), Line(ListKind.Note, line => line.Height = LineHeight.Exactly(8)));
                var (natural, plan) = Plan(folder, look, 1);
                var full = (NoteTile)folder.Tiles[0].Island;
                Assert.Equal(natural[0], full.ListHeight(plan.Cards[0].right - plan.Cards[0].left, style));
                var held = (NoteTile)folder.Tiles[1].Island;
                var box = Box(plan, 1);
                Drawn(held, box, style);
                var middle = Centre(box);
                // Up at the top: nothing to scroll, the list's.
                Assert.False(held.WheelOn(middle, box, style, WidgetSurface.List, 1));
                Assert.True(held.WheelOn(middle, box, style, WidgetSurface.List, -1));
                Assert.True(held.Scroll > 0);
                var ink = Ink(folder, plan, 1, look, 1);
                Assert.True(ink.Bottom <= box.bottom, $"ink to {ink.Bottom} in a line {box.bottom} tall at {textScale * 100:0} % text");
                Assert.All(held.LinePieces(box, style), piece => Assert.True(piece.Box.bottom <= box.bottom + 0.5f && piece.Box.top >= box.top - 0.5f, piece.Name));
                // Far down, it stops at the end; then the wheel is the list's again.
                while (held.WheelOn(middle, box, style, WidgetSurface.List, -5))
                {
                }
                Assert.False(held.WheelOn(middle, box, style, WidgetSurface.List, -1));
                Assert.True(held.WheelOn(middle, box, style, WidgetSurface.List, 100));
                Assert.Equal(0, held.Scroll);
                // Typing at the end brings it into view.
                held.Begin();
                held.Editor.End(extend: false);
                Drawn(held, box, style);
                Assert.True(held.Scroll > 0);
                Assert.Contains(held.LinePieces(box, style), piece => piece.Text == "Last words.");
                folder.Closed();
                folder.Dispose();
            }
        }));

    /// <summary>
    /// One editor, always live, showing every mark on every line, at rest and wherever the caret is
    /// (§321); a click lands on the character under it; the keys move by the lines as shown, Enter
    /// goes on with a list, Tab indents it, and a link with the caret in it is edited rather than opened.
    /// </summary>
    [UiFact]
    public void TheNoteIsEditedAsItIsShown() =>
        OnStaThread(gfx => WithBook(book =>
        {
            const string body = "# Title\nSome **bold** words\n- one\n- two\n[docs](https://example.com)";
            var id = book.AddNote("Live", Monday);
            book.SetBody(id, body, Monday);
            var look = LookFor(dark: false);
            var style = Style(look);
            var folder = Folder(gfx, Line(ListKind.Note));
            var note = (NoteTile)folder.Tiles[0].Island;
            var box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(note, box, style);
            string Words(string piece) => Piece(note, box, style, piece).Text;
            // At rest, every mark shows.
            Assert.Equal("# Title", Words("heading 0"));
            Assert.Equal("Some **bold** words", Words("paragraph 1"));
            Assert.Equal("- one", Words("bullet 2"));
            var heading = Piece(note, box, style, "heading 0").Box;

            // A click between "bo" and "ld": the caret is there in the text, one for one with what shows.
            var bold = Piece(note, box, style, "paragraph 1").Box;
            Click(note, new Vector2(bold.left + PopupPaint.Measure(gfx, "Some **bo", style.Text(14), Weight.Regular).X, Centre(bold).Y), box, style);
            Assert.True(note.Typing);
            Assert.Equal("# Title\nSome **bo".Length, note.Editor.Caret);
            Drawn(note, box, style);
            // Nothing moves for the caret: the lines read and stand as at rest.
            Assert.Equal("Some **bold** words", Words("paragraph 1"));
            Assert.Equal("# Title", Words("heading 0"));
            Assert.Equal(heading, Piece(note, box, style, "heading 0").Box);

            // Up: on the heading, and every line still as it was.
            Assert.True(note.KeyOn(VK.VK_UP, WidgetSurface.List));
            Drawn(note, box, style);
            Assert.Equal(0, Markdown.LineAt(note.Editor.Lines, note.Editor.Caret));
            Assert.Equal("# Title", Words("heading 0"));
            Assert.Equal("Some **bold** words", Words("paragraph 1"));

            // Down twice to the list; End, Enter: a new item; Tab indents it.
            Assert.True(note.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.True(note.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.Equal(2, Markdown.LineAt(note.Editor.Lines, note.Editor.Caret));
            Assert.True(note.KeyOn(VK.VK_END, WidgetSurface.List));
            Assert.True(note.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Type(note, "half");
            Assert.True(note.KeyOn(VK.VK_TAB, WidgetSurface.List));
            Assert.Equal("# Title\nSome **bold** words\n- one\n  - half\n- two\n[docs](https://example.com)", note.Editor.Text);
            // Home: the line's start, before its indent and marker, which show.
            Assert.True(note.KeyOn(VK.VK_HOME, WidgetSurface.List));
            Assert.Equal(note.Editor.CaretLine.Start, note.Editor.Caret);
            Drawn(note, box, style);
            Assert.Equal("  - half", Words("bullet 3"));

            // A link with the caret away opens; with the caret in it, a click edits it.
            var opened = new List<string>();
            NotebookTile.Opening = opened.Add;
            // A line longer by one: the folder lays the line out again, as tall as it is now.
            box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(note, box, style);
            var link = Piece(note, box, style, "paragraph 5").Box;
            Assert.Equal("[docs](https://example.com)", Words("paragraph 5"));
            var docs = link.left + PopupPaint.Measure(gfx, "[d", style.Text(14), Weight.Regular).X;
            // Far enough apart that the two clicks are not one double click.
            Click(note, new Vector2(docs, Centre(link).Y - 5), box, style);
            Assert.Equal(["https://example.com"], opened);
            Assert.True(note.Typing, "a link's click leaves the keys where they were");
            note.Editor.MoveTo(note.Editor.Text.Length - 2, extend: false);
            Drawn(note, box, style);
            Click(note, new Vector2(docs, Centre(link).Y + 5), box, style);
            Assert.Single(opened);
            Assert.True(note.Editor.Caret < note.Editor.Text.Length - 20);

            // Escape keeps it all.
            Assert.True(note.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
            Assert.Equal(note.Editor.Text, book.Current.Note(id)!.Body);
            folder.Closed();
            folder.Dispose();
        }));

    /// <summary>A note line in a list <paramref name="width"/> wide, showing <paramref name="note"/>, as tall as it is.</summary>
    private static FolderIsland NoteIn(Gfx gfx, FolderListWidth width, string note, Action<CellItem>? set = null)
    {
        var holder = BarKind.FolderList.New();
        holder.Folder!.Widths = [width];
        Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, holder.Folder, Line(ListKind.Note, line =>
        {
            line.Note!.Note = note;
            line.Height = LineHeight.Automatic(LineHeight.Most);
            set?.Invoke(line);
        })));
        holder.Normalize();
        var folder = new FolderIsland($"{Owner}/{holder.Id}", holder, gfx, new Config());
        folder.Opened(gfx);
        return folder;
    }

    /// <summary>
    /// The toolbar over the note: a button toggles its format where the caret is and leaves the keys
    /// with the note, shows pressed where the caret's words are what it makes, says what it does
    /// and its key, and its key does the same; at rest a button starts the typing first.
    /// </summary>
    [UiFact]
    public void TheToolbarFormatsWhereTheCaretIs() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var id = book.AddNote("Bar", Monday);
            book.SetBody(id, "Some words\nmore", Monday);
            var look = LookFor(dark: false);
            var style = Style(look);
            var folder = NoteIn(gfx, FolderListWidth.Wide, id);
            var note = (NoteTile)folder.Tiles[0].Island;
            var box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(note, box, style);
            var names = note.LinePieces(box, style).Where(piece => piece.Name.StartsWith("tool ", StringComparison.Ordinal)).Select(piece => piece.Name).ToList();
            // Wide has room for every button beside the title, and so no "More".
            Assert.Equal(["tool heading", "tool bold", "tool italic", "tool strike", "tool code", "tool link", "tool bullets", "tool numbers", "tool tasks", "tool quote"], names);
            var bold = Piece(note, box, style, "tool bold").Box;
            Assert.Equal(S.WidgetNoteFormatBold, note.TipOn(Centre(bold), box, style, WidgetSurface.List)?.Text);
            Assert.False(note.TextOn(Centre(bold), box, style), "over a button the pointer is the arrow");
            // §317: the toolbar stands in the title's row, after its words, and the body under that row.
            var title = Piece(note, box, style, "title").Box;
            Assert.True(bold.top >= title.top && bold.bottom <= title.bottom, "the buttons in the title's row");
            Assert.True(title.right <= Piece(note, box, style, "tool heading").Box.left);
            Assert.True(Piece(note, box, style, "paragraph 0").Box.top >= title.bottom);

            // At rest, a button starts the typing, and acts where the caret goes: the end.
            Click(note, Centre(Piece(note, box, style, "tool bullets").Box), box, style);
            Assert.True(note.Typing);
            Assert.Equal("Some words\n- more", note.Editor.Text);

            // "words" selected; bold: the selection is kept on the words and the keys stay the note's.
            note.Editor.MoveTo(5, extend: false);
            note.Editor.MoveTo(10, extend: true);
            box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(note, box, style);
            bold = Piece(note, box, style, "tool bold").Box;
            Click(note, Centre(bold), box, style);
            Assert.Equal("Some **words**\n- more", note.Editor.Text);
            Assert.True(note.Typing);
            Assert.Equal("words", note.Editor.SelectedText);
            Assert.True(note.Editor.IsOn(NoteFormat.Bold));
            // A press that slides off the button before it is let go does nothing.
            Assert.True(note.PressOn(Centre(bold), box, style, WidgetSurface.List));
            note.DragOn(new Vector2(Centre(bold).X, box.bottom - 2), box, style, WidgetSurface.List);
            note.ReleaseOn(WidgetSurface.List);
            Assert.Equal("Some **words**\n- more", note.Editor.Text);

            // Ctrl+B takes it away again; Ctrl+Alt+B (AltGr) is not the toolbar's.
            var held = new HashSet<int> { VK.VK_CONTROL };
            KeyState.Held = held.Contains;
            try
            {
                Assert.True(note.KeyOn('B', WidgetSurface.List));
                Assert.Equal("Some words\n- more", note.Editor.Text);
                held.Add(VK.VK_MENU);
                Assert.False(note.KeyOn('B', WidgetSurface.List));
                Assert.Equal("Some words\n- more", note.Editor.Text);
                held.Remove(VK.VK_MENU);
                held.Add(VK.VK_SHIFT);
                Assert.True(note.KeyOn('8', WidgetSurface.List));
                Assert.Equal("- Some words\n- more", note.Editor.Text);
            }
            finally
            {
                KeyState.Held = null;
            }
            // Undone as one step each.
            Assert.True(note.Editor.Undo());
            Assert.Equal("Some words\n- more", note.Editor.Text);
            Assert.True(note.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
            Assert.Equal("Some words\n- more", book.Current.Note(id)!.Body);
            folder.Closed();
            folder.Dispose();
        }));

    /// <summary>
    /// §317: the toolbar in the title's row at every width and text size: inside the row and the gutter,
    /// its last button at the row's end, the title's words before the first and keeping their room up to
    /// a short title's; with no room for every button, those that fit and "More" at the end. Too narrow
    /// a line keeps "More" alone, which opens the rest in rows under the title, the line taller by them;
    /// a line too short for a line of the note has none.
    /// </summary>
    [UiFact]
    public void TheToolbarStandsInTheTitlesRowAndKeepsTheRestUnderMore() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var id = book.AddNote("Narrow", Monday);
            book.SetBody(id, "Words", Monday);
            foreach (var textScale in TextScales)
            {
                var look = LookFor(dark: true, textScale);
                var style = Style(look);
                var gutter = ListForm.Gutter(style);
                foreach (var width in FolderListWidths.All)
                {
                    var wide = NoteIn(gfx, width, id);
                    var line = (NoteTile)wide.Tiles[0].Island;
                    var at = Box(Plan(wide, look, 1).Plan, 0);
                    Drawn(line, at, style);
                    var said = $"{width} at {textScale * 100:0} % text";
                    var title = Piece(line, at, style, "title").Box;
                    var buttons = line.LinePieces(at, style).Where(piece => piece.Name.StartsWith("tool ", StringComparison.Ordinal)).ToList();
                    Assert.NotEmpty(buttons);
                    Assert.All(buttons, piece => Assert.True(piece.Box.top >= title.top && piece.Box.bottom <= title.bottom && piece.Box.left >= title.right && piece.Box.right <= at.right - gutter + 0.5f, $"{piece.Name}, {said}"));
                    Assert.Equal(at.right - gutter, buttons[^1].Box.right, 0.5f);
                    // The title keeps "Narrow" whole where any button stands before "More".
                    var words = TilePaint.Width(title) - ListForm.Gutter(style);
                    Assert.True(buttons.Count == 1 || words >= MathF.Min(style.Text(96), 40 * textScale), $"the title's room {words}, {said}");
                    // "More" at the end exactly where not every one of the ten stands.
                    Assert.Equal(buttons.Count(piece => piece.Name != "tool more") < 10, buttons[^1].Name == "tool more");
                    Assert.True(Piece(line, at, style, "paragraph 0").Box.top >= title.bottom, said);
                    wide.Closed();
                    wide.Dispose();
                }
                var folder = NoteIn(gfx, FolderListWidth.Narrow, id);
                var note = (NoteTile)folder.Tiles[0].Island;
                var (natural, plan) = Plan(folder, look, 1);
                var box = Box(plan, 0);
                Drawn(note, box, style);
                string[] Tools() => [.. note.LinePieces(box, style).Where(piece => piece.Name.StartsWith("tool ", StringComparison.Ordinal)).Select(piece => piece.Name)];
                var shut = Tools();
                Assert.Equal("tool more", shut[^1]);
                Assert.True(shut.Length < 11, $"{shut.Length} buttons in a narrow line at {textScale * 100:0} % text");
                Assert.True(shut.Length == 1 || shut[0] == "tool heading");
                Click(note, Centre(Piece(note, box, style, "tool more").Box), box, style);
                Assert.False(note.Typing, "More opens the rest, and nothing more");
                var (taller, again) = Plan(folder, look, 1);
                Assert.True(taller[0] > natural[0]);
                box = Box(again, 0);
                Drawn(note, box, style);
                var open = Tools();
                Assert.Equal(11, open.Length);
                Assert.Equal(open.Length, open.Distinct().Count());
                // Every button inside the line's gutters, none on another.
                var placed = note.LinePieces(box, style).Where(piece => piece.Name.StartsWith("tool ", StringComparison.Ordinal)).ToList();
                Assert.All(placed, piece => Assert.True(piece.Box.left >= gutter - 0.5f && piece.Box.right <= box.right - gutter + 0.5f, piece.Name));
                for (var one = 0; one < placed.Count; one++)
                {
                    for (var other = one + 1; other < placed.Count; other++)
                    {
                        var (a, b) = (placed[one].Box, placed[other].Box);
                        Assert.False(a.left < b.right - 0.5f && b.left < a.right - 0.5f && a.top < b.bottom - 0.5f && b.top < a.bottom - 0.5f, $"{placed[one].Name} on {placed[other].Name}");
                    }
                }
                Assert.True(Piece(note, box, style, "paragraph 0").Box.top >= placed.Max(piece => piece.Box.bottom));
                folder.Closed();
                folder.Dispose();

                // Held to a row, there is no room for it.
                var low = NoteIn(gfx, FolderListWidth.Narrow, id, line => line.Height = LineHeight.Exactly(2));
                var lowNote = (NoteTile)low.Tiles[0].Island;
                var lowBox = Box(Plan(low, look, 1).Plan, 0);
                Assert.DoesNotContain(lowNote.LinePieces(lowBox, style), piece => piece.Name.StartsWith("tool ", StringComparison.Ordinal));
                low.Closed();
                low.Dispose();
            }
        }));

    [UiFact]
    public void TheNoteToolbarIsPictured() =>
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                WithBook(book =>
                {
                    var id = book.AddNote("Week plan", Monday);
                    book.SetBody(id, "# Week plan\nShip the **notes** widget, then *rest*.\n- [ ] Draw the toolbar\n> A quote", Monday);
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var textScale in TextScales)
                    {
                        foreach (var width in FolderListWidths.All)
                        {
                            var look = LookFor(dark, textScale);
                            var style = Style(look);
                            var said = $"{width}, {textScale * 100:0} % text";
                            var rest = NoteIn(gfx, width, id);
                            panels.Add(($"At rest: {said}", look, rest, Plan(rest, look, 1).Plan));
                            // Typing in the bold, the pointer on italic.
                            var typed = NoteIn(gfx, width, id);
                            var tile = (NoteTile)typed.Tiles[0].Island;
                            tile.Begin();
                            tile.Editor.MoveTo("# Week plan\nShip the **no".Length, extend: false);
                            var plan = Plan(typed, look, 1).Plan;
                            var box = Box(plan, 0);
                            Drawn(tile, box, style);
                            if (tile.LinePieces(box, style).FirstOrDefault(piece => piece.Name == "tool italic") is { Name: not null } italic)
                            {
                                tile.HoverOn(Centre(italic.Box), box, style, WidgetSurface.List);
                            }
                            panels.Add(($"Caret in the bold, pointer on italic: {said}", look, typed, plan));
                            // The caret in the heading, and "More" open where there is one.
                            var headed = NoteIn(gfx, width, id);
                            tile = (NoteTile)headed.Tiles[0].Island;
                            tile.Begin();
                            tile.Editor.MoveTo(3, extend: false);
                            plan = Plan(headed, look, 1).Plan;
                            box = Box(plan, 0);
                            Drawn(tile, box, style);
                            if (tile.LinePieces(box, style).FirstOrDefault(piece => piece.Name == "tool more") is { Name: not null } more)
                            {
                                Click(tile, Centre(more.Box), box, style);
                                plan = Plan(headed, look, 1).Plan;
                                panels.Add(($"Caret in the heading, More open: {said}", look, headed, plan));
                            }
                            else
                            {
                                panels.Add(($"Caret in the heading: {said}", look, headed, plan));
                            }
                        }
                    }
                    Sheet(gfx, $"notes-toolbar-{(dark ? "dark" : "light")}.png", 1, panels);
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
            // §317: dark at 150 %, typing in the bold at each width, the pointer on italic, "More" open on the narrowest.
            WithBook(book =>
            {
                const float scale = 1.5f;
                var id = book.AddNote("Week plan", Monday);
                book.SetBody(id, "# Week plan\nShip the **notes** widget, then *rest*.\n- [ ] Draw the toolbar\n> A quote", Monday);
                var look = LookFor(dark: true);
                var style = Style(look, scale);
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                foreach (var width in FolderListWidths.All.Take(3))
                {
                    var typed = NoteIn(gfx, width, id);
                    var tile = (NoteTile)typed.Tiles[0].Island;
                    tile.Begin();
                    tile.Editor.MoveTo("# Week plan\nShip the **no".Length, extend: false);
                    var plan = Plan(typed, look, scale).Plan;
                    var box = Box(plan, 0);
                    Drawn(tile, box, style);
                    if (tile.LinePieces(box, style).FirstOrDefault(piece => piece.Name == "tool italic") is { Name: not null } italic)
                    {
                        tile.HoverOn(Centre(italic.Box), box, style, WidgetSurface.List);
                    }
                    panels.Add(($"Typing in the bold, pointer on italic: {width}, 150 %", look, typed, plan));
                }
                var narrow = NoteIn(gfx, FolderListWidth.Narrow, id);
                var shut = (NoteTile)narrow.Tiles[0].Island;
                var shutBox = Box(Plan(narrow, look, scale).Plan, 0);
                Drawn(shut, shutBox, style);
                Click(shut, Centre(Piece(shut, shutBox, style, "tool more").Box), shutBox, style);
                panels.Add(("More open: Narrow, 150 %", look, narrow, Plan(narrow, look, scale).Plan));
                Sheet(gfx, "notes-toolbar-dark-150.png", scale, panels);
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            });
        });

    /// <summary>The box's caret moves between wrapped lines by Up and Down, and to a line's ends by Home and End.</summary>
    [UiFact]
    public void TheCaretMovesBetweenLines() =>
        OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            var style = Style(look);
            var px = style.Text(14);
            var editor = new InlineEditor(multiLine: true, 1000);
            editor.Begin("first line\nsecond line\nthird", selectAll: false);
            var box = PopupPaint.Rect(0, 0, 400, 200);
            Bitmap(400, 200, dc => editor.Paint(dc, gfx, box, px, QuickTheme.For(look), style), _ => { });
            var editing = editor.Editing;
            Assert.Equal(editor.Text.Length, editing.Caret);
            Assert.True(editor.Key(VK.VK_UP, false, false, gfx, px, box, style, out _));
            Assert.InRange(editing.Caret, "first line\n".Length, "first line\nsecond line".Length);
            Assert.True(editor.Key(VK.VK_HOME, false, false, gfx, px, box, style, out _));
            Assert.Equal("first line\n".Length, editing.Caret);
            Assert.True(editor.Key(VK.VK_END, false, false, gfx, px, box, style, out _));
            Assert.Equal("first line\nsecond line".Length, editing.Caret);
            Assert.True(editor.Key(VK.VK_UP, false, false, gfx, px, box, style, out _));
            Assert.InRange(editing.Caret, 0, "first line".Length);
            Assert.True(editor.Key(VK.VK_UP, false, false, gfx, px, box, style, out _));
            Assert.Equal(0, editing.Caret);
            Assert.True(editor.Key(VK.VK_END, true, true, gfx, px, box, style, out _));
            Assert.Equal(editor.Text, editing.SelectedText);
            Assert.True(editor.Key(VK.VK_BACK, false, false, gfx, px, box, style, out var changed));
            Assert.True(changed);
            Assert.Equal("", editor.Text);
            // Enter is a new line here.
            Assert.True(editor.Key(VK.VK_RETURN, false, false, gfx, px, box, style, out changed));
            Assert.Equal("\n", editor.Text);
            // A long line wraps, and Down goes to its second line rather than the next \n.
            editor.Begin(string.Join(" ", Enumerable.Repeat("word", 60)), selectAll: false);
            editor.Editing.Home(extend: false);
            Assert.True(editor.Key(VK.VK_DOWN, false, false, gfx, px, box, style, out _));
            Assert.InRange(editing.Caret, 1, editor.Text.Length - 1);
        });

    // --- Which one --------------------------------------------------------------------------------------

    /// <summary>A line shows its own list or note; one that is gone is the first; a change elsewhere shows.</summary>
    [UiFact]
    public void ALineShowsTheListOrNoteItIsSetTo() =>
        OnStaThread(gfx => WithBook(book =>
        {
            var first = book.AddList("First");
            var second = book.AddList("Second");
            var noteOne = book.AddNote("One", Monday);
            var noteTwo = book.AddNote("Two", Monday);
            var folder = Folder(gfx,
                Line(ListKind.Todo),
                Line(ListKind.Todo, line => line.Todo!.List = second),
                Line(ListKind.Note, line => line.Note!.Note = noteTwo),
                Line(ListKind.Todo, line => line.Todo!.List = "gone"));
            Assert.Equal(first, ((TodoTile)folder.Tiles[0].Island).List!.Id);
            Assert.Equal(second, ((TodoTile)folder.Tiles[1].Island).List!.Id);
            Assert.Equal(noteTwo, ((NoteTile)folder.Tiles[2].Island).Note!.Id);
            Assert.Equal(first, ((TodoTile)folder.Tiles[3].Island).List!.Id);
            // Another store on the file (the settings app) renames the second; the line shows it.
            using (var other = new Notebook(book.Folder))
            {
                other.RenameList(second, "Renamed");
            }
            // Read again now, as the watcher would a moment later.
            book.Reload();
            Assert.Equal("Renamed", ((TodoTile)folder.Tiles[1].Island).List!.Name);
            // A task added in one line is in every line showing that list.
            var look = LookFor(dark: true);
            var style = Style(look);
            var (_, plan) = Plan(folder, look, 1);
            var a = (TodoTile)folder.Tiles[0].Island;
            Drawn(a, Box(plan, 0), style);
            a.BeginAdding();
            Type(a, "Shared");
            a.StopTypingOn();
            Assert.Equal("Shared", ((TodoTile)folder.Tiles[3].Island).View.Open[0].Text);
            _ = noteOne;
            folder.Closed();
            folder.Dispose();
        }));

    // --- Pictures ---------------------------------------------------------------------------------------

    [UiFact]
    public void TheToDoLinesArePictured() =>
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                WithBook(book =>
                {
                    var list = Groceries(book);
                    var big = book.AddList("Packing");
                    for (var index = 0; index < 10; index++)
                    {
                        book.AddTask(big, index == 3 ? "A task with words long enough that they do not fit on one line of the list" : $"Item {index + 1}", Monday);
                    }
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark, textScale);
                        var style = Style(look);
                        // At rest; the completed opened with a task under the pointer; typing a task; held short.
                        var rest = Folder(gfx, Line(ListKind.Todo, line => line.Todo!.List = list));
                        panels.Add(($"At rest: {textScale * 100:0} % text", look, rest, Plan(rest, look, 1).Plan));

                        var opened = Folder(gfx, Line(ListKind.Todo, line => line.Todo!.List = list));
                        var openedTile = (TodoTile)opened.Tiles[0].Island;
                        openedTile.CompletedOpen = true;
                        var openedPlan = Plan(opened, look, 1).Plan;
                        var box = Box(openedPlan, 0);
                        var task = openedTile.LinePieces(box, style).First(piece => piece.Name.StartsWith("task ", StringComparison.Ordinal));
                        openedTile.HoverOn(Centre(task.Box), box, style, WidgetSurface.List);
                        panels.Add(($"Completed open, a task lit: {textScale * 100:0} % text", look, opened, openedPlan));

                        var typing = Folder(gfx, Line(ListKind.Todo, line => line.Todo!.List = list));
                        var typingTile = (TodoTile)typing.Tiles[0].Island;
                        typingTile.BeginAdding();
                        Type(typingTile, "Oranges");
                        panels.Add(($"Typing a task: {textScale * 100:0} % text", look, typing, Plan(typing, look, 1).Plan));

                        var held = Folder(gfx, Line(ListKind.Todo, line =>
                        {
                            line.Todo!.List = big;
                            line.Height = LineHeight.Exactly(12);
                        }));
                        panels.Add(($"Ten tasks held to six rows: {textScale * 100:0} % text", look, held, Plan(held, look, 1).Plan));
                    }
                    Sheet(gfx, $"notes-todo-{(dark ? "dark" : "light")}.png", 1, panels);
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        });

    [UiFact]
    public void TheNoteLinesArePictured() =>
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                WithBook(book =>
                {
                    var plan = book.AddNote("Week plan", Monday);
                    book.SetBody(plan, SampleNote, Monday);
                    var empty = book.AddNote("Empty", Monday);
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark, textScale);
                        FolderIsland Whole(int? caret = null, int? anchor = null)
                        {
                            var folder = Folder(gfx, Line(ListKind.Note, line =>
                            {
                                line.Note!.Note = plan;
                                line.Height = LineHeight.Automatic(LineHeight.Most);
                            }));
                            if (caret is { } at)
                            {
                                var tile = (NoteTile)folder.Tiles[0].Island;
                                tile.Begin();
                                tile.Editor.MoveTo(anchor ?? at, extend: false);
                                tile.Editor.MoveTo(at, extend: true);
                            }
                            return folder;
                        }
                        void Add(string caption, FolderIsland folder) => panels.Add(($"{caption}: {textScale * 100:0} % text", look, folder, Plan(folder, look, 1).Plan));
                        Add("At rest", Whole());
                        Add("Caret in the heading", Whole(Sample.IndexOf("plan", StringComparison.Ordinal)));
                        Add("Caret in the bold", Whole(Sample.IndexOf("otes**", StringComparison.Ordinal)));
                        Add("Caret in a task's words", Whole(Sample.IndexOf("Draw the", StringComparison.Ordinal) + 4));
                        Add("Caret in a task's marker", Whole(Sample.IndexOf("- [ ] Draw", StringComparison.Ordinal)));
                        Add("Caret in the link, words selected", Whole(Sample.IndexOf("the docs", StringComparison.Ordinal) + 3, Sample.IndexOf("Ship", StringComparison.Ordinal)));
                        var held = Folder(gfx, Line(ListKind.Note, line =>
                        {
                            line.Note!.Note = plan;
                            line.Height = LineHeight.Exactly(10);
                        }));
                        var heldTile = (NoteTile)held.Tiles[0].Island;
                        var heldPlan = Plan(held, look, 1).Plan;
                        var heldBox = Box(heldPlan, 0);
                        Drawn(heldTile, heldBox, Style(look));
                        _ = heldTile.WheelOn(Centre(heldBox), heldBox, Style(look), WidgetSurface.List, -2);
                        panels.Add(($"Held to five rows, scrolled: {textScale * 100:0} % text", look, held, heldPlan));
                        var none = Folder(gfx, Line(ListKind.Note, line => line.Note!.Note = empty));
                        ((NoteTile)none.Tiles[0].Island).Begin();
                        Add("An empty note, typing", none);
                    }
                    Sheet(gfx, $"notes-note-{(dark ? "dark" : "light")}.png", 1, panels);
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        });

    /// <summary>§321: a note's marks at 150 %, dark and light — at rest, typing in the bold, a selection over two lines.</summary>
    [UiFact]
    public void TheNotesMarksArePictured() =>
        OnStaThread(gfx =>
        {
            const float scale = 1.5f;
            const string body = "# Week plan\n## Tasks\nShip the **notes** widget, then *rest*.\n- [x] Write the parser\n- [ ] Draw the `NoteLayout`\n  - [ ] Nested check\n- a bullet\n1. First\n> A quote";
            foreach (var dark in new[] { true, false })
            {
                WithBook(book =>
                {
                    var id = book.AddNote("Week plan", Monday);
                    book.SetBody(id, body, Monday);
                    var look = LookFor(dark);
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    FolderIsland Typed(int caret, int anchor)
                    {
                        var folder = NoteIn(gfx, FolderListWidth.Medium, id);
                        var tile = (NoteTile)folder.Tiles[0].Island;
                        tile.Begin();
                        tile.Editor.MoveTo(anchor, extend: false);
                        tile.Editor.MoveTo(caret, extend: true);
                        return folder;
                    }
                    var rest = NoteIn(gfx, FolderListWidth.Medium, id);
                    panels.Add(("At rest, 150 %", look, rest, Plan(rest, look, scale).Plan));
                    var bold = body.IndexOf("otes**", StringComparison.Ordinal);
                    var typing = Typed(bold, bold);
                    panels.Add(("Typing in the bold, 150 %", look, typing, Plan(typing, look, scale).Plan));
                    var selected = Typed(body.IndexOf("Draw", StringComparison.Ordinal), body.IndexOf("parser", StringComparison.Ordinal));
                    panels.Add(("Selected over two tasks, 150 %", look, selected, Plan(selected, look, scale).Plan));
                    Sheet(gfx, $"notes-marks-{(dark ? "dark" : "light")}-150.png", scale, panels);
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        });

    /// <summary>§669: every block and inline kind a note reads, each wrapping, for the showcase sheets.</summary>
    internal const string Showcase = """
        # Markdown in a note, a heading long enough to wrap
        A paragraph with **bold**, *italic*, ~~struck~~, `inline code` and a [link](https://karpo.app) that runs on to wrap.

        ## Lists
        - A bullet whose words run long enough to wrap onto a second line
          - A nested bullet that also wraps onto a second line of its own
            - Deeper still, wrapping as well under its own words
        - [ ] A task whose words run long enough to wrap onto a second line
        - [x] A ticked task, struck and muted, that wraps as well
          - [ ] A nested task under the ticked one
        8. Eighth, a numbered item that wraps onto a second line
        9. Ninth
        10. Tenth, whose number is wider than the ninth's
           - A bullet under the tenth, wrapping under its words
          and a line that goes on with the tenth

        ### A quote
        > A quote that runs long enough to wrap onto a second line in a narrow list.
        > - A list in the quote, wrapping onto its second line too

        ```
        dotnet test --filter NoteLayout --blame-hang-timeout 4m
        ```

        ---
        Last words.
        """;

    /// <summary>§669: the showcase note, narrow and wide, at 100 % and 225 % text, dark and light.</summary>
    [UiFact]
    public void TheNotesShowcaseIsPictured() =>
        OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                WithBook(book =>
                {
                    var id = book.AddNote("Showcase", Monday);
                    book.SetBody(id, Showcase, Monday);
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark, textScale);
                        foreach (var width in new[] { FolderListWidth.Narrow, FolderListWidth.Wide })
                        {
                            var folder = NoteIn(gfx, width, id);
                            panels.Add(($"{width}: {textScale * 100:0} % text", look, folder, Plan(folder, look, 1).Plan));
                        }
                    }
                    Sheet(gfx, $"notes-showcase-{(dark ? "dark" : "light")}.png", 1, panels);
                    foreach (var (_, _, folder, _) in panels)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                });
            }
        });
}
