// A Google account's tasks through the Tasks API, read and written as Microsoft To Do's are
// (ITaskReader; docs\accounts.md, "To Do" and "Google"). Its token is the tasks' scope's, so an account
// whose person unticked Google Tasks fails here alone (ConsentException).
//
// Google's lists name no default; its "My Tasks" is the one @default stands for, asked for by that. A
// task is needsAction or completed, with no star; its due is a date Google keeps as that day's UTC
// midnight, read as the day it says. Google keeps no time a task was made: its last change (updated)
// stands in, which the open tasks are ordered by and a tried Add is found again by — a task renamed
// elsewhere moves to the end of the line's list. Hidden tasks (ticked off in Google's own apps) are
// asked for too (showHidden), or the line's Completed would miss them. Every open task of a list is
// read, and of the completed the 200 last ticked off.
//
// Google documents no precondition on a task's etag: a change is sent as it is, and the last one to
// reach Google wins (TaskQueue's own rules still order and fold the changes before they go). A task or
// a list already gone (404) is no failure.

using System.Globalization;
using System.Text.Json.Nodes;

namespace Karpo.Accounts.Google;

internal sealed partial class GoogleReader
{
    /// <summary>Completed tasks read of one list at most: the last ticked off.</summary>
    internal const int MostCompleted = 200;

    public async Task<IReadOnlyList<RemoteTaskList>> TaskListsAsync(CancellationToken cancellation)
    {
        var own = Web.Str((await tasks.GetAsync($"{ListsAt}/@default", cancellation).ConfigureAwait(false))["id"]);
        var lists = await tasks.AllAsync($"{ListsAt}?maxResults=100", "items", cancellation).ConfigureAwait(false);
        return [.. lists.Select(list => List(list, own)).OfType<RemoteTaskList>()];
    }

    public async Task<IReadOnlyList<RemoteTask>> TasksAsync(string list, CancellationToken cancellation)
    {
        var all = (await tasks.AllAsync($"{TasksAt(list)}?showCompleted=true&showHidden=true&maxResults=100", "items", cancellation).ConfigureAwait(false))
            .Select(Parse).OfType<RemoteTask>().DistinctBy(task => task.Id).ToList();
        return [.. all.Where(task => !task.Done),
            .. all.Where(task => task.Done).OrderByDescending(task => task.Completed ?? task.Modified).Take(MostCompleted)];
    }

    public async Task<RemoteTask> AddTaskAsync(string list, string title, bool done, CancellationToken cancellation)
    {
        var body = new JsonObject { ["title"] = title, ["status"] = done ? "completed" : "needsAction" };
        var made = await tasks.SendAsync(HttpMethod.Post, TasksAt(list), body, null, cancellation).ConfigureAwait(false);
        return (made is null ? null : Parse(made)) ?? throw new AccountException(S.CoreAccountsOauthNotJson("Google"), 201);
    }

    public async Task<RemoteTask?> UpdateTaskAsync(string list, string task, TaskEdit edit, string? etag, CancellationToken cancellation)
    {
        var body = new JsonObject();
        if (edit.Title is { } title)
        {
            body["title"] = title;
        }
        if (edit.Done is { } done)
        {
            body["status"] = done ? "completed" : "needsAction";
            if (!done)
            {
                // Put back: Google keeps when it was ticked off until it is told to forget it.
                body["completed"] = null;
            }
        }
        try
        {
            var changed = await tasks.SendAsync(HttpMethod.Patch, TaskAt(list, task), body, null, cancellation).ConfigureAwait(false);
            return changed is null ? await TaskAsync(list, task, cancellation).ConfigureAwait(false) : Parse(changed);
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            return null;
        }
    }

    public async Task DeleteTaskAsync(string list, string task, string? etag, CancellationToken cancellation)
    {
        try
        {
            await tasks.SendAsync(HttpMethod.Delete, TaskAt(list, task), null, null, cancellation).ConfigureAwait(false);
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            // Gone already: deleted on another device, which is what was asked.
        }
    }

    public async Task<RemoteTask?> TaskAsync(string list, string task, CancellationToken cancellation)
    {
        try
        {
            return Parse(await tasks.GetAsync(TaskAt(list, task), cancellation).ConfigureAwait(false));
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            return null;
        }
    }

    public async Task RenameListAsync(string list, string name, CancellationToken cancellation) =>
        await tasks.SendAsync(HttpMethod.Patch, ListAt(list), new JsonObject { ["title"] = name }, null, cancellation).ConfigureAwait(false);

    public async Task DeleteListAsync(string list, CancellationToken cancellation)
    {
        try
        {
            await tasks.SendAsync(HttpMethod.Delete, ListAt(list), null, null, cancellation).ConfigureAwait(false);
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            // Gone already: deleted on another device, which is what was asked.
        }
    }

    private const string ListsAt = $"{GoogleApi.Tasks}/users/@me/lists";

    private static string ListAt(string list) => $"{ListsAt}/{Uri.EscapeDataString(list)}";

    private static string TasksAt(string list) => $"{GoogleApi.Tasks}/lists/{Uri.EscapeDataString(list)}/tasks";

    private static string TaskAt(string list, string task) => $"{TasksAt(list)}/{Uri.EscapeDataString(task)}";

    /// <summary>One task list as Google lists it, the default where its id is <paramref name="own"/>'s; null for one without an id.</summary>
    internal static RemoteTaskList? List(JsonObject item, string? own)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var name = Web.Str(item["title"]);
        return new RemoteTaskList(id, string.IsNullOrWhiteSpace(name) ? S.WidgetTodoNewList : name.Trim(), id == own);
    }

    /// <summary>One task as Google gives it; null for one deleted, or without an id.</summary>
    internal static RemoteTask? Parse(JsonObject item)
    {
        if (Web.Bool(item["deleted"]) == true || Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var title = Web.Str(item["title"]);
        var updated = When(Web.Str(item["updated"])) ?? DateTimeOffset.MinValue;
        var done = Web.Str(item["status"]) == "completed";
        return new RemoteTask(
            id,
            string.IsNullOrWhiteSpace(title) ? "" : title.Trim(),
            done,
            false,
            Due(Web.Str(item["due"])),
            updated,
            done ? When(Web.Str(item["completed"])) ?? updated : null,
            updated,
            Web.Str(item["etag"]));
    }

    /// <summary>A due date as the day it is due: Google keeps it as that day's midnight in UTC, a date and no time.</summary>
    internal static DateOnly? Due(string? value) =>
        value is { Length: >= 10 } && DateOnly.TryParseExact(value[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;

    private static DateTimeOffset? When(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;
}
