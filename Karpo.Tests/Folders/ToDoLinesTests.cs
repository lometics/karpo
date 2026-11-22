// The to-do line set to a Microsoft To Do list (TodoTile, docs\accounts.md "To Do"): its tasks from
// the store, a tick, a task added and renamed going to To Do, a delete asked first, the account that
// needs a reconnect sending the line to the Accounts page, and sheets of it dark and light at 100 % and
// 225 % text. Each test has a store of its own on a fake account (TodoTile.TaskStand) and a frozen
// clock; no account is reached. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1; the sheets go
// to KARPO_PNG_DIR as todo-microsoft-<dark|light>.png.

using System.Numerics;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Tests.Islands;
using Karpo.Tests.Pim;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(NotebookLines.Collection)]
public sealed unsafe class ToDoLinesTests
{
    private static readonly DateTime Today = new(2026, 10, 16, 9, 30, 0);

    /// <summary>A delete question's answer, kept for the test to read.</summary>
    private sealed class Answer(bool yes) : IDeleteAsk
    {
        public List<Karpo.Core.DeleteQuestion> Asked { get; } = [];

        public bool Ask(HWND owner, Karpo.Core.DeleteQuestion question)
        {
            Asked.Add(question);
            return yes;
        }
    }

    /// <summary>A To Do account the way a person's looks: a default list with due days and stars, and a second list.</summary>
    private static FakeTodo Sample()
    {
        var day = DateOnly.FromDateTime(Today);
        return new FakeTodo().With("tasks", "Tasks", true,
                FakeTodo.Task("1", "Renew the passport", important: true, due: day.AddDays(-2), minutes: 1),
                FakeTodo.Task("2", "Call the plumber about the kitchen tap", due: day, minutes: 2),
                FakeTodo.Task("3", "Buy a birthday present for Anna", important: true, due: day.AddDays(1), minutes: 3),
                FakeTodo.Task("4", "Book the train to Geneva", due: day.AddDays(9), minutes: 4),
                FakeTodo.Task("5", "Water the plants", minutes: 5),
                FakeTodo.Task("6", "Pay the electricity bill", done: true, minutes: 6),
                FakeTodo.Task("7", "Return the library books", done: true, minutes: 7))
            .With("shop", "Groceries", false, FakeTodo.Task("m", "Milk"));
    }

    /// <summary>A store of its own on <paramref name="todo"/>, read, the lines' while <paramref name="body"/> runs.</summary>
    private static void With(FakeTodo todo, Action<TaskStore> body, bool read = true)
    {
        var folder = Path.Combine(Isolation.Folder, "todo-lines", Guid.NewGuid().ToString("N"));
        using var book = new Karpo.Core.Notebook(folder);
        using var store = new TaskStore(new AccountSet([todo]), cache: new PimCache(folder), now: () => new DateTimeOffset(Today));
        if (read)
        {
            store.Refresh();
            store.Idle().GetAwaiter().GetResult();
        }
        NotebookTile.Stand = book;
        TodoTile.TaskStand = store;
        try
        {
            body(store);
        }
        finally
        {
            NotebookTile.Stand = null;
            TodoTile.TaskStand = null;
            TodoTile.Asking = null;
        }
    }

    private static CellItem ToDo(string? list = null, Action<CellItem>? set = null) => Line(ListKind.Todo, line =>
    {
        line.Todo!.Account = "me@outlook.com";
        line.Todo.TaskList = list;
        set?.Invoke(line);
    });

    private static TileStyle Style(Look look) => FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));

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

    private static void Drawn(CellIsland line, D2D_RECT_F box, TileStyle style) =>
        Bitmap(box.right, box.bottom, dc => line.PaintOn(dc, box, style, WidgetSurface.List), _ => { });

    [UiFact]
    public void AToDoListIsTickedAddedRenamedAndDeletedInToDo()
    {
        var server = Sample();
        OnStaThread(gfx => Frozen(() => With(server, store =>
        {
            var look = LookFor(dark: true);
            var style = Style(look);
            var folder = Folder(gfx, ToDo());
            var line = (TodoTile)folder.Tiles[0].Island;
            Assert.True(line.Remote);
            Assert.Null(line.Unset);
            var box = Box(Plan(folder, look, 1).Plan, 0);
            Drawn(line, box, style);

            // The default list, its open tasks oldest first, the done ones under "Completed".
            Assert.Equal("Tasks", Piece(line, box, style, "name").Text);
            Assert.Equal(["1", "2", "3", "4", "5"], line.View.Open.Select(item => item.Id));
            Assert.Equal(["7", "6"], line.View.Completed.Select(item => item.Id));
            Assert.Equal("Yesterday", TodoTile.DueWords(DateOnly.FromDateTime(Today).AddDays(-1), DateOnly.FromDateTime(Today)));
            Assert.Equal("Today", Piece(line, box, style, "due 2").Text);
            Assert.Equal("Tomorrow", Piece(line, box, style, "due 3").Text);
            Assert.Contains(line.LinePieces(box, style), piece => piece.Name == "star 1");
            Assert.DoesNotContain(line.LinePieces(box, style), piece => piece.Name == "star 2");

            // A box ticks it in To Do.
            Click(line, Centre(Piece(line, box, style, "box 2").Box), box, style);
            Assert.Contains(line.View.Completed, item => item.Id == "2");
            store.Idle().GetAwaiter().GetResult();
            Assert.True(server.Find("tasks", "2")!.Done);

            // "Add a task": typed, Enter adds it to the list in To Do.
            box = Box(Plan(folder, look, 1).Plan, 0);
            Click(line, Centre(Piece(line, box, style, "add").Box), box, style);
            foreach (var c in "Oranges")
            {
                Assert.True(line.CharOn(c, WidgetSurface.List));
            }
            Assert.True(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            Assert.True(line.KeyOn(VK.VK_ESCAPE, WidgetSurface.List));
            Assert.Equal("Oranges", line.View.Open[^1].Text);
            store.Idle().GetAwaiter().GetResult();
            Assert.Contains(server.Tasks["tasks"], task => task.Title == "Oranges");

            // F2 renames it there too.
            Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            Assert.True(line.KeyOn(VK.VK_F2, WidgetSurface.List));
            line.Editor.Editing.SelectAll();
            foreach (var c in "Renew the passport today")
            {
                Assert.True(line.CharOn(c, WidgetSurface.List));
            }
            Assert.True(line.KeyOn(VK.VK_RETURN, WidgetSurface.List));
            store.Idle().GetAwaiter().GetResult();
            Assert.Equal("Renew the passport today", server.Find("tasks", "1")!.Title);

            // Delete asks first, and No keeps it; Yes deletes it from To Do.
            var no = new Answer(false);
            TodoTile.Asking = no;
            Assert.True(line.KeyOn(VK.VK_DELETE, WidgetSurface.List));
            var asked = Assert.Single(no.Asked);
            Assert.Contains("Renew the passport today", asked.Content);
            Assert.Contains(line.View.Open, item => item.Id == "1");
            TodoTile.Asking = new Answer(true);
            Assert.True(line.KeyOn(VK.VK_DELETE, WidgetSurface.List));
            store.Idle().GetAwaiter().GetResult();
            Assert.Null(server.Find("tasks", "1"));
            Assert.DoesNotContain(line.View.Open, item => item.Id == "1");

            // To Do's tasks keep no order Karpo can set: Alt+Down moves nothing.
            Assert.True(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            var before = line.View.Open.Select(item => item.Id).ToList();
            KeyState.Held = key => key == VK.VK_MENU;
            try
            {
                Assert.False(line.KeyOn(VK.VK_DOWN, WidgetSurface.List));
            }
            finally
            {
                KeyState.Held = null;
            }
            Assert.Equal(before, line.View.Open.Select(item => item.Id));
            folder.Closed();
            folder.Dispose();
        })));
    }

    [UiFact]
    public void AListThatCannotBeShownSendsTheLineToTheAccountsPage() =>
        OnStaThread(gfx =>
        {
            var waiting = Sample();
            waiting.Failure = new ConsentException(AccountKind.Microsoft, AccountData.Tasks);
            With(waiting, _ =>
            {
                var folder = Folder(gfx, ToDo());
                var line = (TodoTile)folder.Tiles[0].Island;
                Assert.Equal(("Reconnect to sync tasks", SetupLink.Accounts), (line.Unset!.Short, line.Unset.Link));
                folder.Dispose();
            });

            With(Sample(), _ =>
            {
                var gone = Folder(gfx, ToDo(set: line => line.Todo!.Account = "someone@else.com"));
                Assert.Equal("This list's account is gone", ((TodoTile)gone.Tiles[0].Island).Unset!.Short);
                gone.Dispose();
                // A line on this PC is the notebook's, as before.
                var local = Folder(gfx, Line(ListKind.Todo));
                Assert.False(((TodoTile)local.Tiles[0].Island).Remote);
                Assert.Null(((TodoTile)local.Tiles[0].Island).Unset);
                local.Dispose();
            });
        });

    [UiFact]
    public void TheToDoLinesArePictured() =>
        OnStaThread(gfx => Frozen(() =>
        {
            foreach (var dark in new[] { true, false })
            {
                var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                With(Sample(), store =>
                {
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark, textScale);
                        var style = Style(look);
                        var rest = Folder(gfx, ToDo());
                        panels.Add(($"Microsoft To Do, at rest: {textScale * 100:0} % text", look, rest, Plan(rest, look, 1).Plan));

                        var opened = Folder(gfx, ToDo());
                        var openedLine = (TodoTile)opened.Tiles[0].Island;
                        openedLine.CompletedOpen = true;
                        var openedPlan = Plan(opened, look, 1).Plan;
                        var box = Box(openedPlan, 0);
                        openedLine.HoverOn(Centre(Piece(openedLine, box, style, "task 3").Box), box, style, WidgetSurface.List);
                        panels.Add(($"Completed open, a starred task lit: {textScale * 100:0} % text", look, opened, openedPlan));

                        var other = Folder(gfx, ToDo("shop"));
                        panels.Add(($"Another list: {textScale * 100:0} % text", look, other, Plan(other, look, 1).Plan));
                    }
                });
                // Not read yet, and read once but not refreshed since.
                var offline = Sample();
                With(offline, store =>
                {
                    offline.Failure = new HttpRequestException("offline");
                    store.Refresh(force: true);
                    store.Idle().GetAwaiter().GetResult();
                    var look = LookFor(dark);
                    var stale = Folder(gfx, ToDo());
                    panels.Add(("Could not be refreshed: the stale mark", look, stale, Plan(stale, look, 1).Plan));
                });
                var slow = Sample();
                slow.Held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                With(slow, _ =>
                {
                    var look = LookFor(dark);
                    var loading = Folder(gfx, ToDo());
                    panels.Add(("Being read", look, loading, Plan(loading, look, 1).Plan));
                }, read: false);
                Sheet(gfx, $"todo-microsoft-{(dark ? "dark" : "light")}.png", 1, panels);
                slow.Held.SetResult();
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        }));

    /// <summary>The bar's clock at <see cref="Today"/> while <paramref name="body"/> runs, so a due day says the same every run.</summary>
    private static void Frozen(Action body)
    {
        Clock.Now = () => Today;
        try
        {
            body();
        }
        finally
        {
            Clock.Now = Clock.Real;
        }
    }
}
