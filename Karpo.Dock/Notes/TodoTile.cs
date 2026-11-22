// A to-do list as a line of a list folder (docs\widgets.md §87), as Microsoft To Do and Windows'
// own lists draw one: the list's name, the tasks not done each with its round-cornered box,
// "Completed" with how many, the done ones under it — struck through, quieter — once it is opened,
// and "Add a task" at the line's foot (2026-10-11: it stood under the name, above the tasks; then
// under the last task, until "the add task button should be aligned on the bottom of the task
// widget"): a line taller than its tasks keeps the room between them and the field, never under it.
// Which list is the line's (TodoOptions); none, or one deleted, is the notebook's first.
//
// Everything is in place, in the folder: a press on "Add a task" (or a letter typed while the line
// has the focus) types a task, Enter adds it and types the next, Escape or a press elsewhere keeps
// what was typed; a box ticks; a double click or F2 renames a task, the trash at its end deletes it;
// a task pressed and dragged up or down goes where the accent line shows. The keys, with the focus on
// the line: Up and Down choose a row, Space ticks it, Enter adds or renames, Delete deletes, Alt+Up
// and Alt+Down move it.
//
// As tall as its list (IListContent), up to the line's most; held shorter, the list's name stays at
// the top and "Add a task" at the bottom, and the tasks scroll between them (ListScroll.cs). Its rows are
// drawn in the item style chosen (ItemForm): Compact, Standard or Large rows and words.
//
// **Microsoft To Do** (2026-10-16: "add a todo widget as well ... it should get its tasks from
// microsoft office, like the email etc."). Set to a Microsoft account's To Do list (TodoOptions.Account,
// .TaskList) the line shows that list from the dock's TaskStore (docs\accounts.md, "To Do") the way it
// shows the notebook's: the open tasks — each with its due day and To Do's star at its end where it
// has them, the day red once it has passed — "Completed" with how many, and "Add a task" at the foot.
// A tick, a task added and a task renamed go to To Do (queued, and sent when the account answers);
// the trash asks first, in Windows' own dialog, since the task goes from every device. To Do keeps no
// order Graph can set, so its tasks stand oldest first and are not dragged. An account that never
// consented to To Do, one gone, or a list turned off on the Accounts page is "Set up in Settings",
// which opens the Accounts page; a list not read yet says so in a row under its name, and one that
// could not be refreshed wears the stale mark (StaleMark.cs) at its name's end. A To Do list is read
// again from "Refresh" on its right click (IRefreshable, §451).

using System.Numerics;
using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Pim;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class TodoTile : NotebookTile, IListContent, IListScroll, ILinePieces, IRefreshable
{
    private const string AddGlyph = "";
    private const string DeleteGlyph = "";
    private const string OpenGlyph = "";
    private const string ClosedGlyph = "";

    /// <summary>Segoe Fluent Icons: FavoriteStarFill, To Do's star on an important task.</summary>
    private const string StarGlyph = "";

    /// <summary>What a row of the line is.</summary>
    internal enum RowKind
    {
        Header,
        Add,
        Task,
        Completed,
        Done,

        /// <summary>A To Do list's words under its name: being read, or why it could not be.</summary>
        Note,
    }

    /// <summary>
    /// Tests only, on the thread that makes the line: the To Do store a line set to a To Do list reads,
    /// instead of the dock's, so no account is ever reached.
    /// </summary>
    [ThreadStatic]
    internal static TaskStore? TaskStand;

    /// <summary>Tests only: what answers "Delete this task?" instead of Windows' dialog.</summary>
    [ThreadStatic]
    internal static IDeleteAsk? Asking;

    // The To Do store while the line shows a To Do list; null while it shows the notebook's.
    private TaskStore? tasks;

    /// <summary>One row, in the line's px from its top; a task's own where it is one.</summary>
    internal readonly record struct Row(RowKind Kind, D2D_RECT_F Box, TodoItem? Item = null, string Words = "");

    /// <summary>The rows the line shows and how tall they are; <paramref name="Natural"/> is all of them.</summary>
    internal sealed record Plan(IReadOnlyList<Row> Rows, float Height, float Natural);

    /// <summary>Which part of a row: the whole of it, its box or its trash.</summary>
    private enum Spot
    {
        Row,
        Box,
        Delete,
    }

    private readonly InlineEditor editor = new(multiLine: false, Notebook.MostTask) { Placeholder = S.WidgetTodoAddTask };

    // What is being typed: a new task (null id while adding), a task's new words, or nothing.
    private bool adding;
    private string? renaming;

    // The completed tasks shown under their heading; closed each time the folder opens.
    private bool open;

    // The row under the pointer and which part of it; the row the keys chose, and whether they did.
    private int hot = -1;
    private Spot hotPart;
    private int chosen = -1;
    private bool keyed;

    // A press: on which row and part, where, and a task being dragged into another place.
    private int pressedRow = -1;
    private Spot pressedPart;
    private Vector2 pressedAt;
    private Vector2 draggedTo;
    private bool dragging;
    private bool selecting;

    // The last plan drawn, for the keys and the pointer to find rows by.
    private Plan? shown;
    private D2D_RECT_F shownBox;
    private TileStyle shownStyle;

    public TodoTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        Configure(island, config);
    }

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        // Set to a To Do list, the line follows the store; set back to the notebook, it lets go.
        if (Remote && tasks is null)
        {
            tasks = TaskStand ?? TaskStore.Shared;
            tasks.Attach(this);
            tasks.Refresh();
        }
        else if (!Remote && tasks is { } held)
        {
            held.Release(this);
            tasks = null;
        }
    }

    /// <summary>Whether it shows a Microsoft To Do list rather than one of the notebook's.</summary>
    internal bool Remote => Settings.Todo?.IsToDo == true;

    /// <summary>The To Do list it shows, from the store's memory; null where it shows the notebook's.</summary>
    internal TaskListView? ToDo => Remote && tasks is { } store ? store.Show(Settings.Todo!.Account!, Settings.Todo.TaskList) : null;

    /// <summary>
    /// The notebook's list this line shows: its own, else the first; null where there is none, or it shows To Do's. §643: a
    /// ghost's (made showing the samples) whose list holds nothing, the samples' (<see cref="Samples.Tasks"/>), never written.
    /// </summary>
    internal TodoList? List => Remote ? null
        : Book.Current.List(Settings.Todo?.List) is var own && Sampled && own is not { Items.Count: > 0 } ? Samples.Tasks() : own;

    internal TodoView View => ToDo is { } shown ? Of(shown) : TodoView.Of(List);

    /// <summary>A To Do list as the notebook's are drawn: its open tasks in order, the completed the last ticked off first.</summary>
    private static TodoView Of(TaskListView shown) => new([.. shown.Open.Select(Item)], [.. shown.Completed.Select(Item)]);

    private static TodoItem Item(RemoteTask task) => new()
    {
        Id = task.Id,
        Text = task.Title,
        Done = task.Done,
        Created = task.Created,
        Finished = task.Completed,
        Due = task.Due,
        Important = task.Important,
    };

    /// <summary>
    /// A To Do list that cannot be shown until something is done in Settings: the account consented to
    /// none of its tasks, it is gone, or the list's sync is off. Each opens the Accounts page.
    /// </summary>
    internal override SetupNeed? Unset => ToDo?.State switch
    {
        TaskListState.Consent => Accounts(S.CoreAccountsReconnectTasks),
        TaskListState.NoAccount => Accounts(S.WidgetTodoAccountGone),
        TaskListState.Off => Accounts(S.WidgetTodoListOff),
        _ => base.Unset,
    };

    private static SetupNeed Accounts(string words) => new(null, SetupLink.Accounts, words, words);

    /// <summary>"Last updated 14:05 — couldn't refresh" while a To Do list shows what was read before (StaleMark.cs).</summary>
    internal override string? StaleTip => ToDo?.Freshness.Tip(Clock.Real());

    /// <summary>A To Do list to read again ("Refresh", IRefreshable); the notebook's lists are this PC's own.</summary>
    public bool CanRefresh => ToDo is not null;

    /// <summary>The accounts' lists are being read.</summary>
    public bool Refreshing => Remote && tasks?.Reading == true;

    /// <summary>When the list shown was read.</summary>
    public DateTime? UpdatedUtc => ToDo?.Freshness.UpdatedUtc;

    /// <summary>Why the list could not be read, or "Last updated 14:05 — couldn't refresh".</summary>
    string? IRefreshable.Problem => ToDo is { } shown ? shown.Problem ?? shown.Freshness.Tip(Clock.Real()) : null;

    /// <summary>"Refresh": every account's lists read again now, however recently they were.</summary>
    public void RefreshNow() => tasks?.Refresh(force: true);

    /// <summary>menus: the list's done tasks gone, as its own "Clear completed" (Dock.WidgetMenus.cs). The notebook's only: To Do's stay To Do's.</summary>
    internal void ClearCompleted()
    {
        if (List is { } list)
        {
            _ = Book.ClearCompleted(list.Id);
        }
    }

    /// <summary>The store posted: it raises Changed, so an open list follows.</summary>
    protected override bool Sample(bool everything)
    {
        if (everything)
        {
            tasks?.Deliver();
        }
        return base.Sample(everything);
    }

    public override void Dispose()
    {
        tasks?.Release(this);
        tasks = null;
        base.Dispose();
    }

    // --- Changed: the notebook's list, or To Do's ------------------------------------------------------

    private void Added(string words)
    {
        if (ToDo is { } shown)
        {
            if (shown is { Account: { } account, List: { } list })
            {
                _ = tasks!.Add(account, list, words);
            }
            return;
        }
        // Into the line's list, under its tasks, just over the field it was typed in; the first
        // task of a notebook with none makes one, which a line with no list of its own goes on
        // showing as the first.
        Book.AddTask(Settings.Todo?.List, words, Now, last: true);
    }

    private void Ticked(TodoItem item, bool done)
    {
        if (ToDo is { } shown)
        {
            if (shown is { Account: { } account, List: { } list })
            {
                tasks!.SetDone(account, list, item.Id, done);
            }
            return;
        }
        Book.SetDone(item.Id, done, Now);
    }

    private void Renamed(string task, string words)
    {
        if (ToDo is { } shown)
        {
            if (shown is { Account: { } account, List: { } list })
            {
                tasks!.Rename(account, list, task, words);
            }
            return;
        }
        Book.SetText(task, words);
    }

    /// <summary>The task deleted: the notebook's at once, To Do's once the person said yes, since it goes from every device.</summary>
    private void Deleted(TodoItem item)
    {
        if (ToDo is not { } shown)
        {
            Book.DeleteTask(item.Id);
            return;
        }
        if (shown is not { Account: { } account, List: { } list })
        {
            return;
        }
        var question = new DeleteQuestion(S.WidgetTodoDeleteTitle, S.WidgetTodoDeleteText(task: item.Text, service: tasks!.Service(account)), S.WidgetTodoDelete, S.DockDeleteCancel, 0);
        if ((Asking ?? new TaskDialogs()).Ask(GetForegroundWindow(), question))
        {
            tasks!.Delete(account, list, item.Id);
        }
    }

    internal bool Adding => adding;

    internal string? Renaming => renaming;

    internal InlineEditor Editor => editor;

    internal bool CompletedOpen
    {
        get => open;
        set => open = value;
    }

    protected internal override bool CardTyping => adding || renaming is not null;

    protected override void Blink() => editor.Blink();

    public override void Visible(bool shown)
    {
        base.Visible(shown);
        if (shown)
        {
            // A To Do list opened: read again where the last read is a minute old, as the mail does.
            tasks?.Refresh(fresh: true);
        }
        else
        {
            open = false;
            hot = chosen = pressedRow = -1;
            keyed = dragging = selecting = false;
        }
    }

    // --- Laid out ------------------------------------------------------------------------------------

    /// <summary>
    /// The rows at <paramref name="width"/> in <paramref name="room"/> px: every one where they fit,
    /// "Add a task" on the room's foot; else the header, the rows that fit under it and "Add a task"
    /// last — where the line scrolls, it is laid out as tall as all of them (ListScroll.cs).
    /// </summary>
    internal Plan Lay(float width, in TileStyle style, float room = float.PositiveInfinity)
    {
        var head = HeaderRow(style);
        var row = ItemRow(style);
        var shown = ToDo;
        var view = shown is null ? TodoView.Of(List) : Of(shown);
        var items = new List<(RowKind Kind, TodoItem? Item)>();
        // A To Do list with nothing to show yet says why under its name: being read, or the trouble.
        var note = shown is { State: TaskListState.Loading } ? S.WidgetTodoLoading
            : shown is { State: TaskListState.Failed, Problem: { } problem } ? problem
            : null;
        if (note is not null && view.Open.Count == 0 && view.Completed.Count == 0)
        {
            items.Add((RowKind.Note, null));
        }
        items.AddRange(view.Open.Select(item => (RowKind.Task, (TodoItem?)item)));
        if (view.Completed.Count > 0)
        {
            items.Add((RowKind.Completed, null));
            if (open)
            {
                items.AddRange(view.Completed.Select(item => (RowKind.Done, (TodoItem?)item)));
            }
        }
        var fixedHeight = head + row;
        // "Add a task" the line's foot, its band the line's edge (LineItem): no air under it.
        var natural = fixedHeight + (items.Count * row);
        var fill = room >= natural - 0.5f
            ? new WidgetFill { Shown = items.Count, Left = 0, Height = items.Count * row }
            : WidgetFill.Of(items.Count, row, room - fixedHeight);
        var rows = new List<Row> { new(RowKind.Header, PopupPaint.Rect(0, 0, width, head), Words: shown?.Name ?? List?.Name ?? S.WidgetTodoNewList) };
        var y = head;
        foreach (var (kind, item) in items.Take(fill.Shown))
        {
            rows.Add(new Row(kind, PopupPaint.Rect(0, y, width, row), item,
                kind switch { RowKind.Completed => S.WidgetTodoCompleted, RowKind.Note => note!, _ => item?.Text ?? "" }));
            y += row;
        }
        // "Add a task" last, on the line's foot, as To Do keeps its field at the list's foot: a line
        // taller than its tasks keeps the room above it; held shorter, it stays, and the tasks give
        // way above it; scrolled, it is the foot that stays (ScrollFoot).
        if (float.IsFinite(room))
        {
            y = MathF.Max(y, room - row);
        }
        rows.Add(new Row(RowKind.Add, PopupPaint.Rect(0, y, width, row)));
        y += row;
        return new Plan(rows, y, natural);
    }

    public float ListHeight(float width, in TileStyle style) => Lay(width, style).Natural;

    /// <summary>All of the list, where the line has room for more than its name: the rest scrolls under the name (ListScroll.cs).</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        height + 0.5f < HeaderRow(style) + ItemRow(style) ? 0 : Lay(width, style).Natural;

    /// <summary>The list's name stays while its tasks scroll.</summary>
    public float ScrollHead(float width, float content, in TileStyle style) => HeaderRow(style);

    /// <summary>"Add a task" stays at the line's foot while the tasks scroll.</summary>
    public float ScrollFoot(float width, float content, in TileStyle style) => ItemRow(style);

    /// <summary>The list's name's row: a row of the list.</summary>
    private static float HeaderRow(in TileStyle style) => PopupPaint.Round(40 * style.Scale * style.TextScale);

    /// <summary>A task's row, "Add a task"'s and "Completed"'s, at the item style's size: 32, 40 or 48 DIP.</summary>
    private float ItemRow(in TileStyle style) =>
        PopupPaint.Round(ItemStyles.Of(Settings).Size switch { ItemSize.Compact => 32, ItemSize.Large => 48, _ => 40 } * style.Scale * style.TextScale);

    /// <summary>A task's words, at the item style's size.</summary>
    private float Px(in TileStyle style) => ItemForm.For(Gfx, style, Settings).TextPx;

    private static D2D_RECT_F Moved(D2D_RECT_F at, D2D_RECT_F box) => new(box.left + at.left, box.top + at.top, box.left + at.right, box.top + at.bottom);

    /// <summary>A row's box (its tick), in the icon column.</summary>
    private static D2D_RECT_F BoxOf(D2D_RECT_F row, in ListForm form, in TileStyle style)
    {
        var column = form.IconBox(row.left, row.top, row.bottom - row.top);
        var side = PopupPaint.Round(style.Text(18));
        return PopupPaint.Rect(column.left + PopupPaint.Round((column.right - column.left - side) / 2), row.top + PopupPaint.Round((row.bottom - row.top - side) / 2), side, side);
    }

    /// <summary>A task's trash, the lines' one glyph button (LineButton) standing on the trailing gutter.</summary>
    private D2D_RECT_F DeleteOf(D2D_RECT_F row, in ListForm form, in TileStyle style)
    {
        var side = LineButton.Of(Gfx, style).Height;
        return PopupPaint.Rect(row.right - form.Inset - side, row.top + PopupPaint.Round((row.bottom - row.top - side) / 2), side, side);
    }

    /// <summary>Where a row's text box stands while it is typed in: from where the words begin to the trailing gutter.</summary>
    private static D2D_RECT_F EditorOf(D2D_RECT_F row, in ListForm form, in TileStyle style)
    {
        var height = PopupPaint.Round(style.Text(32));
        return new D2D_RECT_F(row.left + form.TextLeft - style.Px(10), row.top + PopupPaint.Round((row.bottom - row.top - height) / 2), row.right - form.Inset, row.top + PopupPaint.Round((row.bottom - row.top - height) / 2) + height);
    }

    // --- Drawn ----------------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box.right - box.left, style, box.bottom - box.top);
        shown = plan;
        shownBox = box;
        shownStyle = style;
        var theme = QuickTheme.For(style.Theme);
        var form = ListForm.Of(Gfx, style);
        var radius = style.Px(4);
        for (var index = 0; index < plan.Rows.Count; index++)
        {
            var row = plan.Rows[index];
            var at = Moved(row.Box, box);
            // Out of sight in a scrolled line, or under its head or its foot: not drawn (CellIsland.Hidden).
            if (Hidden(at.top, at.bottom))
            {
                continue;
            }
            var height = at.bottom - at.top;
            // What lights and takes a press is the row edge to edge, each the next's edge (LineItem); the keys' ring a little in.
            var band = LineItem.Band(box, at.top, at.bottom);
            var ring = PopupPaint.Rect(at.left + style.Px(4), at.top + style.Px(2), at.right - at.left - style.Px(8), height - style.Px(4));
            var hovered = hot == index && !dragging;
            var textRoom = MathF.Max(1, at.right - form.Inset - (at.left + form.TextLeft));
            float Middle(float px) => at.top + PopupPaint.Round((height - TilePaint.LineHeight(Gfx, px)) / 2);
            switch (row.Kind)
            {
                case RowKind.Header:
                {
                    // A To Do list that could not be refreshed: the stale mark at the name's end.
                    var mark = StaleMarkOf(at, form);
                    if (mark is { } stale)
                    {
                        PopupPaint.Glyph(dc, Gfx, StaleMark.Glyph, StaleMark.Size(form), theme.TextSecondary, stale);
                    }
                    TilePaint.Line(dc, Gfx, row.Words, form.TextPx, Weight.Semibold, theme.Text, at.left + form.Inset, Middle(form.TextPx),
                        MathF.Max(1, at.right - at.left - (2 * form.Inset) - StaleMark.Room(style, form, mark is not null)));
                    break;
                }
                case RowKind.Note:
                    TilePaint.Line(dc, Gfx, row.Words, Px(style), Weight.Regular, theme.TextSecondary, at.left + form.TextLeft, Middle(Px(style)), textRoom);
                    break;
                case RowKind.Add:
                    if (adding)
                    {
                        editor.Paint(dc, Gfx, EditorOf(at, form, style), Px(style), theme, style);
                        PopupPaint.Glyph(dc, Gfx, AddGlyph, form.Glyph, theme.Accent, form.IconBox(at.left, at.top, height));
                        break;
                    }
                    if (hovered)
                    {
                        LineItem.Light(dc, band, theme, pressedRow == index);
                    }
                    PopupPaint.Glyph(dc, Gfx, AddGlyph, form.Glyph, theme.TextSecondary, form.IconBox(at.left, at.top, height));
                    TilePaint.Line(dc, Gfx, S.WidgetTodoAddTask, Px(style), Weight.Regular, theme.TextSecondary, at.left + form.TextLeft, Middle(Px(style)), textRoom);
                    break;
                case RowKind.Task or RowKind.Done:
                {
                    var item = row.Item!;
                    var done = row.Kind == RowKind.Done;
                    var carried = dragging && pressedRow == index;
                    if (carried)
                    {
                        LineItem.Chosen(dc, band, theme, lit: false);
                    }
                    else if (hovered && hotPart == Spot.Row)
                    {
                        LineItem.Light(dc, band, theme, pressedRow == index);
                    }
                    else if (hovered)
                    {
                        LineItem.Light(dc, band, theme, pressed: false);
                    }
                    NoteLayout.CheckBox(dc, Gfx, BoxOf(at, form, style), done, hovered && hotPart == Spot.Box, theme, style);
                    if (renaming == item.Id)
                    {
                        editor.Paint(dc, Gfx, EditorOf(at, form, style), Px(style), theme, style);
                        break;
                    }
                    var trash = DeleteOf(at, form, style);
                    // A To Do task's due day and star at the end, left of the trash where it shows.
                    var end = Trail(dc, item, done, at, hovered ? trash.left - style.Px(4) : at.right - form.Inset, form, style, theme);
                    var room = MathF.Max(1, end - (at.left + form.TextLeft));
                    Words(dc, item.Text, Px(style), done ? theme.TextSecondary : theme.Text, at.left + form.TextLeft, Middle(Px(style)), room, done);
                    if (hovered)
                    {
                        LineButton.Of(Gfx, style).Paint(dc, Gfx, style.Theme, trash, "", DeleteGlyph, ButtonStyle.Subtle, hotPart == Spot.Delete,
                            pressedRow == index && pressedPart == Spot.Delete);

                    }
                    break;
                }
                case RowKind.Completed:
                {
                    if (hovered)
                    {
                        LineItem.Light(dc, band, theme, pressedRow == index);
                    }
                    PopupPaint.Glyph(dc, Gfx, open ? OpenGlyph : ClosedGlyph, PopupPaint.Round(form.Glyph * LineGlyph.InlineDip / LineGlyph.RowDip), theme.TextSecondary, form.IconBox(at.left, at.top, height));
                    var words = $"{row.Words}  {View.Completed.Count}";
                    TilePaint.Line(dc, Gfx, words, Px(style), Weight.Semibold, theme.TextSecondary, at.left + form.TextLeft, Middle(Px(style)), textRoom);
                    break;
                }
            }
            if (keyed && chosen == index && !CardTyping)
            {
                TogglesTile.FocusRing(dc, ring, radius, style, theme);
            }
        }
        // Where a dragged task would land: the accent line between two rows.
        if (dragging && DropAt(plan, draggedTo) is { } drop)
        {
            var y = box.top + drop.Y;
            var thick = MathF.Max(2, style.Px(2));
            PopupPaint.Fill(dc, new D2D_RECT_F(box.left + form.Inset, y - (thick / 2), box.right - form.Inset, y + (thick / 2)), thick / 2, theme.Accent);
        }
    }

    /// <summary>A task's words, struck through where it is done.</summary>
    private void Words(ID2D1DeviceContext* dc, string text, float px, DXGI_RGBA ink, float x, float y, float room, bool struck)
    {
        if (!struck)
        {
            TilePaint.Line(dc, Gfx, text, px, Weight.Regular, ink, x, y, room);
            return;
        }
        var layout = Gfx.TextLayout(text, px, Weight.Regular, room);
        try
        {
            _ = layout->SetStrikethrough(BOOL.TRUE, new DWRITE_TEXT_RANGE { startPosition = 0, length = (uint)text.Length });
            var origin = new D2D_POINT_2F(PopupPaint.Round(x), PopupPaint.Round(y));
            var brush = Gfx.Brush(dc, PaintWatch.Seen(layout, origin, ink));
            try
            {
                Mirror.Text(dc, origin, layout, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
            }
            finally
            {
                brush->Release();
            }
        }
        finally
        {
            layout->Release();
        }
    }

    /// <summary>
    /// Where the stale mark stands on the header row <paramref name="at"/>, at its trailing gutter; null
    /// while the list is fresh, or the notebook's.
    /// </summary>
    private D2D_RECT_F? StaleMarkOf(D2D_RECT_F at, in ListForm form)
    {
        if (ToDo is not { Freshness.Stale: true })
        {
            return null;
        }
        var size = StaleMark.Size(form);
        return PopupPaint.Rect(at.right - form.Inset - size, at.top + PopupPaint.Round((at.bottom - at.top - size) / 2), size, size);
    }

    /// <summary>
    /// A To Do task's star and due day, drawn right to left from <paramref name="right"/> on the row
    /// <paramref name="at"/> (nothing drawn with a null <paramref name="dc"/>); where the words must end.
    /// </summary>
    private float Trail(ID2D1DeviceContext* dc, TodoItem item, bool done, D2D_RECT_F at, float right, in ListForm form, in TileStyle style, QuickTheme theme) =>
        Trail(dc, item, done, at, right, form, style, theme, out _, out _);

    private float Trail(ID2D1DeviceContext* dc, TodoItem item, bool done, D2D_RECT_F at, float right, in ListForm form, in TileStyle style, QuickTheme theme,
        out D2D_RECT_F? star, out D2D_RECT_F? due)
    {
        star = null;
        due = null;
        var height = at.bottom - at.top;
        // The words keep room for five letters or so: what does not fit beside them is left out, the day first.
        var floor = at.left + form.TextLeft + (5 * form.TextPx);
        if (item.Important && right - PopupPaint.Round(form.SmallPx * 1.15f) >= floor)
        {
            var size = PopupPaint.Round(form.SmallPx * 1.15f);
            var box = PopupPaint.Rect(right - size, at.top + PopupPaint.Round((height - size) / 2), size, size);
            star = box;
            if (dc != null)
            {
                PopupPaint.Glyph(dc, Gfx, StarGlyph, size, done ? theme.TextSecondary : theme.Accent, box);
            }
            right = box.left - style.Gap;
        }
        if (item.Due is { } day)
        {
            var words = DueWords(day, Today);
            var width = PopupPaint.Measure(Gfx, words, form.SmallPx, Weight.Regular).X;
            if (right - width < floor)
            {
                return right;
            }
            var line = TilePaint.LineHeight(Gfx, form.SmallPx);
            var box = PopupPaint.Rect(right - width, at.top + PopupPaint.Round((height - line) / 2), width, line);
            due = box;
            if (dc != null)
            {
                // Passed and not done: To Do's red.
                var ink = !done && day < Today ? theme.Critical : theme.TextSecondary;
                TilePaint.Line(dc, Gfx, words, form.SmallPx, Weight.Regular, ink, box.left, box.top, width + 1);
            }
            right = box.left - style.Gap * 2;
        }
        return right;
    }

    /// <summary>Today on the bar's clock.</summary>
    private static DateOnly Today => DateOnly.FromDateTime(Clock.Now());

    /// <summary>A due day as To Do says it: Yesterday, Today, Tomorrow, else the day and month.</summary>
    internal static string DueWords(DateOnly day, DateOnly today) => (day.DayNumber - today.DayNumber) switch
    {
        -1 => S.DockCalendarYesterday,
        0 => S.DockCalendarToday,
        1 => S.DockCalendarTomorrow,
        _ => Panels.Nls.MonthDay(day),
    };

    // --- The pointer ----------------------------------------------------------------------------------

    /// <summary>The row at <paramref name="point"/> (the line's px) and which part of it; -1 for none.</summary>
    private (int Row, Spot Part) At(Plan plan, Vector2 point, D2D_RECT_F box, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        for (var index = 0; index < plan.Rows.Count; index++)
        {
            var at = Moved(plan.Rows[index].Box, box);
            if (!LineRows.Inside(at, point))
            {
                continue;
            }
            if (plan.Rows[index].Kind is RowKind.Task or RowKind.Done)
            {
                var column = form.IconBox(at.left, at.top, at.bottom - at.top);
                if (point.X < column.right + style.Px(4))
                {
                    return (index, Spot.Box);
                }
                if (LineRows.Inside(DeleteOf(at, form, style), point))
                {
                    return (index, Spot.Delete);
                }
            }
            return (index, Spot.Row);
        }
        return (-1, Spot.Row);
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var (row, part) = point is { } at ? At(Lay(box.right - box.left, style, box.bottom - box.top), at, box, style) : (-1, Spot.Row);
        if (point is not null)
        {
            keyed = false;
        }
        hot = row;
        hotPart = part;
    }

    /// <summary>The row at <paramref name="point"/> the line takes a press on, from 1 (the header and a note are none): what is there, as a hit test asks it.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box.right - box.left, style, box.bottom - box.top);
        var (index, _) = At(plan, point, box, style);
        return index < 0 || plan.Rows[index].Kind is RowKind.Header or RowKind.Note ? 0 : index + 1;
    }

    /// <summary>Every row but the header and the count of the rest is the line's own: a press is taken, and acted on at its release.</summary>
    protected internal override bool CardPress(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var plan = Lay(box.right - box.left, style, box.bottom - box.top);
        // The rows as they are now, which the drag and the release read.
        shown = plan;
        shownBox = box;
        shownStyle = style;
        var (index, part) = At(plan, point, box, style);
        var clicks = Clicks(point);
        if (index < 0 || plan.Rows[index].Kind is RowKind.Header or RowKind.Note)
        {
            Stop();
            return false;
        }
        var row = plan.Rows[index];
        var form = ListForm.Of(Gfx, style);
        var editorBox = EditorOf(Moved(row.Box, box), form, style);
        // In the text box being typed in: the caret, a word or everything, and a drag selects.
        if ((row.Kind == RowKind.Add && adding) || (row.Item is { } typed && renaming == typed.Id && part == Spot.Row))
        {
            editor.Press(point, clicks, KeyState.Down(VK.VK_SHIFT), Gfx, Px(style), editorBox, style);
            selecting = true;
            pressedRow = index;
            Redrawn();
            return true;
        }
        Stop();
        keyed = false;
        pressedRow = index;
        pressedPart = part;
        pressedAt = draggedTo = point;
        dragging = false;
        if (row.Kind == RowKind.Add)
        {
            Begin(null, "");
            pressedRow = -1;
        }
        else if (row.Kind == RowKind.Task && part == Spot.Row && clicks == 2)
        {
            Begin(row.Item!.Id, row.Item.Text);
            pressedRow = -1;
        }
        Redrawn();
        return true;
    }

    protected internal override void CardDrag(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (selecting && pressedRow >= 0 && shown is { } plan && pressedRow < plan.Rows.Count)
        {
            var form = ListForm.Of(Gfx, style);
            editor.Drag(point, Gfx, Px(style), EditorOf(Moved(plan.Rows[pressedRow].Box, box), form, style), style);
            Redrawn();
            return;
        }
        draggedTo = point;
        // A task pressed and moved past the drag threshold up or down is carried into another place.
        if (!dragging && pressedRow >= 0 && pressedPart == Spot.Row && shown is { } laid && pressedRow < laid.Rows.Count
            && laid.Rows[pressedRow].Kind == RowKind.Task && !Remote
            && MathF.Abs(point.Y - pressedAt.Y) > GetSystemMetrics(SM.SM_CYDRAG))
        {
            dragging = true;
        }
        if (dragging)
        {
            Redrawn();
        }
    }

    protected internal override void CardRelease()
    {
        var index = pressedRow;
        var part = pressedPart;
        var wasDragging = dragging;
        var wasSelecting = selecting;
        pressedRow = -1;
        dragging = false;
        selecting = false;
        if (wasSelecting || index < 0 || shown is not { } plan || index >= plan.Rows.Count)
        {
            Redrawn();
            return;
        }
        var row = plan.Rows[index];
        if (wasDragging)
        {
            if (DropAt(plan, draggedTo) is { } drop && row.Item is { } moving)
            {
                Book.MoveTask(moving.Id, drop.Before);
            }
            Redrawn();
            return;
        }
        // A click is a press let go where it went down.
        var (released, releasedPart) = At(plan, draggedTo, shownBox, shownStyle);
        if (released != index || releasedPart != part)
        {
            Redrawn();
            return;
        }
        switch (row.Kind)
        {
            case RowKind.Completed:
                open = !open;
                break;
            case RowKind.Task or RowKind.Done when part == Spot.Box:
                Ticked(row.Item!, row.Kind == RowKind.Task);
                break;
            case RowKind.Task or RowKind.Done when part == Spot.Delete:
                Deleted(row.Item!);
                break;
        }
        Redrawn();
    }

    /// <summary>
    /// Where a task dragged to <paramref name="point"/> lands among the tasks not done: the line
    /// between two of them nearest the pointer, and the task it would stand before (null for the end).
    /// </summary>
    private (float Y, string? Before)? DropAt(Plan plan, Vector2 point)
    {
        var tasks = plan.Rows.Where(row => row.Kind == RowKind.Task).ToList();
        if (tasks.Count == 0)
        {
            return null;
        }
        var y = point.Y - shownBox.top;
        foreach (var row in tasks)
        {
            if (y < (row.Box.top + row.Box.bottom) / 2)
            {
                return (row.Box.top, row.Item!.Id);
            }
        }
        // Past the last shown: after it, before the next task not shown, else at the end.
        var open = View.Open;
        var last = tasks[^1].Item!;
        var after = open.SkipWhile(item => item.Id != last.Id).Skip(1).FirstOrDefault();
        return (tasks[^1].Box.bottom, after?.Id);
    }

    // --- Typing ---------------------------------------------------------------------------------------

    /// <summary>Starts typing a new task (<paramref name="task"/> null) or a task's new words.</summary>
    private void Begin(string? task, string words)
    {
        adding = task is null;
        renaming = task;
        editor.Placeholder = S.WidgetTodoAddTask;
        editor.Begin(words, selectAll: false);
    }

    /// <summary>Starts typing a new task, as a press on "Add a task" does.</summary>
    internal void BeginAdding() => Begin(null, "");

    /// <summary>Starts renaming <paramref name="task"/>, as a double click on it does.</summary>
    internal void BeginRenaming(TodoItem task) => Begin(task.Id, task.Text);

    /// <summary>What is typed is kept: a new task added, a task renamed (its words taken away delete it).</summary>
    private void Keep()
    {
        if (adding)
        {
            Added(editor.Text);
        }
        else if (renaming is { } task)
        {
            Renamed(task, editor.Text);
        }
    }

    /// <summary>Keeps what is typed and stops typing.</summary>
    private void Stop()
    {
        if (!CardTyping)
        {
            return;
        }
        Keep();
        adding = false;
        renaming = null;
    }

    protected internal override void CardStopTyping() => Stop();

    protected internal override bool CardChar(char c, WidgetSurface surface)
    {
        if (CardTyping)
        {
            if (editor.Char(c))
            {
                Redrawn();
            }
            return true;
        }
        // A letter typed with the focus on the line starts a task with it; a space ticks (CardKey).
        if (char.IsControl(c) || char.IsWhiteSpace(c))
        {
            return false;
        }
        Begin(null, "");
        editor.Char(c);
        Redrawn();
        return true;
    }

    /// <summary>keys: the line walks its tasks and their editor itself (CellIsland.WalksItsOwnKeys).</summary>
    protected internal override bool WalksItsOwnKeys => true;

    /// <summary>keys: the row its keys chose, by its target (its place from 1).</summary>
    protected internal override int? OwnKeyedTarget => keyed && chosen >= 0 ? chosen + 1 : 0;

    /// <summary>keys: the row its keys chose is ringed, but while a task is typed (one ring, §471).</summary>
    protected internal override bool RingsOwnKeys => keyed && chosen >= 0 && !CardTyping;

    /// <summary>keys: gone to another line, the row they chose is ringed no more.</summary>
    protected internal override void OwnKeys(bool on)
    {
        if (!on && keyed)
        {
            keyed = false;
            Redrawn();
        }
    }

    protected internal override bool CardKey(int key, WidgetSurface surface)
    {
        var shift = KeyState.Down(VK.VK_SHIFT);
        var control = KeyState.Down(VK.VK_CONTROL);
        if (CardTyping)
        {
            switch (key)
            {
                case VK.VK_ESCAPE:
                    Stop();
                    return true;
                case VK.VK_TAB:
                    Stop();
                    return false;
                case VK.VK_RETURN when adding:
                    // Added, and the next typed at once, as To Do's field does.
                    Keep();
                    editor.Begin("", selectAll: false);
                    return true;
                case VK.VK_RETURN:
                    Stop();
                    return true;
            }
            if (shown is { } plan && plan.Rows.FirstOrDefault(row => (row.Kind == RowKind.Add && adding) || (row.Item?.Id == renaming && renaming is not null)) is { Kind: not RowKind.Header } typing)
            {
                var form = ListForm.Of(Gfx, shownStyle);
                editor.Key(key, shift, control, Gfx, Px(shownStyle), EditorOf(Moved(typing.Box, shownBox), form, shownStyle), shownStyle, out _);
            }
            else
            {
                editor.Key(key, shift, control, Gfx, ListForm.Of(Gfx, shownStyle).TextPx, default, shownStyle, out _);
            }
            return true;
        }
        // The rows as they are now: a change since the line was last drawn has moved them.
        var rows = shownBox.right > shownBox.left
            ? Lay(shownBox.right - shownBox.left, shownStyle, shownBox.bottom - shownBox.top).Rows
            : Lay(PopupPaint.Round(360 * shownStyle.Scale), shownStyle).Rows;
        var choosable = Enumerable.Range(0, rows.Count).Where(index => rows[index].Kind is RowKind.Add or RowKind.Task or RowKind.Completed or RowKind.Done).ToList();
        var at = choosable.IndexOf(chosen);
        var alt = KeyState.Down(VK.VK_MENU);
        switch (key)
        {
            case VK.VK_UP or VK.VK_DOWN when alt:
                if (at >= 0 && !Remote && rows[chosen] is { Kind: RowKind.Task, Item: { } moving })
                {
                    var order = View.Open.Select(item => item.Id).ToList();
                    var from = order.IndexOf(moving.Id);
                    var to = key == VK.VK_UP ? from - 1 : from + 2;
                    if (from >= 0 && to >= 0 && to <= order.Count)
                    {
                        Book.MoveTask(moving.Id, to < order.Count ? order[to] : null);
                        chosen += key == VK.VK_UP ? -1 : 1;
                    }
                    keyed = true;
                    return true;
                }
                return false;
            case VK.VK_UP or VK.VK_DOWN:
            {
                var next = at < 0 ? (key == VK.VK_DOWN ? 0 : choosable.Count - 1) : at + (key == VK.VK_DOWN ? 1 : -1);
                if (next < 0 || next >= choosable.Count)
                {
                    // Past the first or the last row, the keys go on to the line before or after.
                    keyed = false;
                    chosen = -1;
                    return false;
                }
                chosen = choosable[next];
                keyed = true;
                return true;
            }
            case VK.VK_SPACE when at >= 0:
                return Act(rows[chosen], enter: false);
            case VK.VK_RETURN:
                return at < 0 ? Act(rows.First(row => row.Kind == RowKind.Add), enter: true) : Act(rows[chosen], enter: true);
            case VK.VK_F2 when at >= 0 && rows[chosen] is { Kind: RowKind.Task or RowKind.Done, Item: { } task }:
                Begin(task.Id, task.Text);
                return true;
            case VK.VK_DELETE when at >= 0 && rows[chosen] is { Kind: RowKind.Task or RowKind.Done, Item: { } gone }:
                Deleted(gone);
                return true;
        }
        return false;
    }

    /// <summary>Space or Enter on a row: "Add a task" types, a task ticks (Enter renames), "Completed" opens.</summary>
    private bool Act(Row row, bool enter)
    {
        switch (row.Kind)
        {
            case RowKind.Add:
                Begin(null, "");
                return true;
            case RowKind.Task or RowKind.Done when enter:
                Begin(row.Item!.Id, row.Item.Text);
                return true;
            case RowKind.Task or RowKind.Done:
                Ticked(row.Item!, row.Kind == RowKind.Task);
                return true;
            case RowKind.Completed:
                open = !open;
                return true;
        }
        return false;
    }

    // --- Words for the pointer and the screen reader --------------------------------------------------

    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (StaleMark.Tip(LinePieces(box, style), point, StaleTip, style) is { } stale)
        {
            return stale;
        }
        var plan = Lay(box.right - box.left, style, box.bottom - box.top);
        var (index, part) = At(plan, point, box, style);
        if (index < 0 || plan.Rows[index] is not { Kind: RowKind.Task or RowKind.Done, Item: { } item } row)
        {
            return null;
        }
        var form = ListForm.Of(Gfx, style);
        var at = Moved(row.Box, box);
        // Under the pointer the trash shows, and the star and the day stand left of it.
        _ = Trail(null, item, row.Kind == RowKind.Done, at, DeleteOf(at, form, style).left - style.Px(4), form, style, QuickTheme.For(style.Theme), out var star, out var due);
        if (star is { } starred && LineRows.Inside(starred, point))
        {
            return (S.WidgetTodoImportant, starred);
        }
        if (due is { } dated && item.Due is { } day && LineRows.Inside(dated, point))
        {
            return (S.WidgetTodoDueOn(Panels.Nls.MonthDay(day)), dated);
        }
        return part switch
        {
            Spot.Box => (row.Kind == RowKind.Task ? S.WidgetTodoMarkDone : S.WidgetTodoMarkNotDone, BoxOf(at, form, style)),
            Spot.Delete => (S.WidgetTodoDelete, DeleteOf(at, form, style)),
            // The whole of a task's words, where they are cut.
            _ when PopupPaint.Measure(Gfx, item.Text, Px(style), Weight.Regular).X > form.Room(at.right - at.left) => (item.Text, at),
            _ => null,
        };
    }

    public string MainPiece => "name";

    public IReadOnlyList<LinePiece> LinePieces(D2D_RECT_F box, in TileStyle style)
    {
        var plan = Lay(box.right - box.left, style, box.bottom - box.top);
        var form = ListForm.Of(Gfx, style);
        var pieces = new List<LinePiece>();
        foreach (var row in plan.Rows)
        {
            var at = Moved(row.Box, box);
            switch (row.Kind)
            {
                case RowKind.Header:
                    pieces.Add(new LinePiece("name", at, row.Words));
                    if (StaleMarkOf(at, form) is { } stale)
                    {
                        pieces.Add(new LinePiece(StaleMark.Piece, stale));
                    }
                    break;
                case RowKind.Note:
                    pieces.Add(new LinePiece("note", at, row.Words));
                    break;
                case RowKind.Add:
                    pieces.Add(new LinePiece("add", at, S.WidgetTodoAddTask));
                    break;
                case RowKind.Task or RowKind.Done:
                    pieces.Add(new LinePiece($"{(row.Kind == RowKind.Done ? "done" : "task")} {row.Item!.Id}", at, row.Item.Text));
                    pieces.Add(new LinePiece($"box {row.Item.Id}", BoxOf(at, form, style)));
                    _ = Trail(null, row.Item, row.Kind == RowKind.Done, at, at.right - form.Inset, form, style, QuickTheme.For(style.Theme), out var star, out var due);
                    if (star is { } starred)
                    {
                        pieces.Add(new LinePiece($"star {row.Item.Id}", starred, S.WidgetTodoImportant));
                    }
                    if (due is { } dated)
                    {
                        pieces.Add(new LinePiece($"due {row.Item.Id}", dated, DueWords(row.Item.Due!.Value, Today)));
                    }
                    break;
                case RowKind.Completed:
                    pieces.Add(new LinePiece("completed", at, $"{row.Words} {View.Completed.Count}"));
                    break;
            }
        }
        return pieces;
    }
}
