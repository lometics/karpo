// What an account gives of tasks — Microsoft To Do's or Google Tasks' lists and their tasks —
// and the rules for the changes made to them while the dock cannot reach the
// account (docs\accounts.md, "To Do").
//
// Unlike the rest of Karpo.Accounts this is written to as well as read: a task
// ticked, added, renamed or deleted on the to-do line goes to To Do. Every such
// change is first a TaskChange in a queue the dock keeps (and keeps on disk), so
// it is shown at once and sent when the account answers; until then the line
// shows the server's tasks with the queue laid over them (TaskQueue.Apply).
//
// A change sent over one the server has since — another device ticked the task,
// renamed it — is a conflict, which Graph says by refusing the change's etag
// (412). The last writer wins: the change goes again over the server's newer
// version where it was made after that version, and is dropped where it was not.
// Google Tasks documents no precondition on a task's etag, so there the change
// is sent as it is and the last one to reach Google wins (docsccounts.md, "Google").

namespace Karpo.Accounts;

/// <summary>One To Do task list.</summary>
/// <param name="Id">Graph's id: the same from run to run.</param>
/// <param name="Default">The account's own "Tasks" list (Graph's <c>wellknownListName</c> defaultList, Google's <c>@default</c>).</param>
public sealed record RemoteTaskList(string Id, string Name, bool Default);

/// <summary>One To Do task, as much of it as the to-do line draws and a change needs.</summary>
/// <param name="Due">The day it is due, where it has one: a date, the same wherever it is read.</param>
/// <param name="Important">To Do's star: its importance is high.</param>
/// <param name="Completed">When it was ticked off; null while it is open.</param>
/// <param name="Modified">When the server last changed it, which a conflict is decided by.</param>
/// <param name="ETag">The server's version of it, which a change is sent against; null for one not sent yet.</param>
public sealed record RemoteTask(
    string Id,
    string Title,
    bool Done,
    bool Important,
    DateOnly? Due,
    DateTimeOffset Created,
    DateTimeOffset? Completed,
    DateTimeOffset Modified,
    string? ETag);

/// <summary>What a change sends of a task: its words, its tick, or both; null leaves it as it is.</summary>
public sealed record TaskEdit(string? Title = null, bool? Done = null);

/// <summary>The server has a newer version of the task than the one a change was sent against (412).</summary>
public sealed class TaskConflictException(string message) : Exception(message);

/// <summary>An account's tasks: Microsoft To Do through Graph, Google Tasks through its API. Every call goes there and then.</summary>
public interface ITaskReader
{
    Account Account { get; }

    /// <summary>Every task list of the account, its default one among them.</summary>
    Task<IReadOnlyList<RemoteTaskList>> TaskListsAsync(CancellationToken cancellation);

    /// <summary>The open tasks of the list, and its newest completed ones.</summary>
    Task<IReadOnlyList<RemoteTask>> TasksAsync(string list, CancellationToken cancellation);

    /// <summary>A task saying <paramref name="title"/>, made in the list; what the server made of it.</summary>
    Task<RemoteTask> AddTaskAsync(string list, string title, bool done, CancellationToken cancellation);

    /// <summary>The task changed, against <paramref name="etag"/> where there is one.</summary>
    /// <exception cref="TaskConflictException">The server has a newer version than <paramref name="etag"/>.</exception>
    /// <returns>The task as the server has it after; null where the task is gone there.</returns>
    Task<RemoteTask?> UpdateTaskAsync(string list, string task, TaskEdit edit, string? etag, CancellationToken cancellation);

    /// <summary>The task deleted; a task already gone is no failure.</summary>
    /// <exception cref="TaskConflictException">The server has a newer version than <paramref name="etag"/>.</exception>
    Task DeleteTaskAsync(string list, string task, string? etag, CancellationToken cancellation);

    /// <summary>The task as the server has it now; null where it is gone.</summary>
    Task<RemoteTask?> TaskAsync(string list, string task, CancellationToken cancellation);

    /// <summary>§324: the list called <paramref name="name"/> now.</summary>
    Task RenameListAsync(string list, string name, CancellationToken cancellation);

    /// <summary>§324: the list deleted, its tasks with it; a list already gone is no failure.</summary>
    Task DeleteListAsync(string list, CancellationToken cancellation);
}

/// <summary>What a change does to a task.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TaskChangeKind>))]
public enum TaskChangeKind
{
    /// <summary>A new task, under an id of the queue's own until the server gives it one.</summary>
    Add,

    /// <summary>Ticked off, or put back.</summary>
    Done,

    /// <summary>Its words changed.</summary>
    Rename,

    /// <summary>Deleted.</summary>
    Delete,
}

/// <summary>
/// One change made on the line and not yet taken by the server: kept in order, shown at once, and
/// sent when the account answers.
/// </summary>
/// <param name="Task">The task's id: the server's, or a new task's own (<see cref="TaskQueue.NewId"/>) until it is sent.</param>
/// <param name="At">When the person made it, which wins a conflict against an older version.</param>
public sealed record TaskChange(TaskChangeKind Kind, string List, string Task, string? Title, bool Done, DateTimeOffset At)
{
    /// <summary>
    /// An Add sent once whose answer never came — a timeout, a connection dropped, a server error after
    /// Graph's own tries, an answer that did not read: To Do may have made the task all the same. Before it
    /// is sent again, the list is looked through for a task of its words made since (TaskStore), so a
    /// retry never makes the task twice; and it takes no later change into itself (<see cref="TaskQueue.Enqueue"/>),
    /// so its words stay the ones it was sent with.
    /// </summary>
    public bool Tried { get; init; }
}

/// <summary>The queue's rules (see the top of this file). Pure.</summary>
public static class TaskQueue
{
    /// <summary>What a new task's id starts with until the server gives it one.</summary>
    public const string NewPrefix = "new:";

    /// <summary>An id for a task made here and not sent yet.</summary>
    public static string NewId() => NewPrefix + Guid.NewGuid().ToString("N")[..12];

    /// <summary>Whether <paramref name="task"/> is a task made here and not sent yet.</summary>
    public static bool IsNew(string task) => task.StartsWith(NewPrefix, StringComparison.Ordinal);

    /// <summary>
    /// <paramref name="queue"/> with <paramref name="change"/> after it, folded into what is already
    /// there: a new task not sent yet takes its words and tick into its Add, or goes with it where it
    /// is deleted; a task's second rename or tick takes the place of its first; a delete takes the
    /// place of everything before it of that task. An Add that was sent and may have been made
    /// (<see cref="TaskChange.Tried"/>) is not folded into: what comes after it is sent after it, to the
    /// task it turns out to be — its delete too, which must reach a task To Do may already have.
    /// </summary>
    public static List<TaskChange> Enqueue(IReadOnlyList<TaskChange> queue, TaskChange change)
    {
        var next = new List<TaskChange>(queue);
        var add = next.FindIndex(one => one.Kind == TaskChangeKind.Add && one.Task == change.Task && !one.Tried);
        if (add >= 0)
        {
            switch (change.Kind)
            {
                case TaskChangeKind.Rename:
                    next[add] = next[add] with { Title = change.Title };
                    return next;
                case TaskChangeKind.Done:
                    next[add] = next[add] with { Done = change.Done };
                    return next;
                case TaskChangeKind.Delete:
                    // Never sent: nothing of it goes to the server at all.
                    next.RemoveAll(one => one.Task == change.Task);
                    return next;
            }
        }
        if (change.Kind == TaskChangeKind.Delete)
        {
            next.RemoveAll(one => one.Task == change.Task && !(one.Kind == TaskChangeKind.Add && one.Tried));
        }
        else if (change.Kind is TaskChangeKind.Rename or TaskChangeKind.Done)
        {
            next.RemoveAll(one => one.Task == change.Task && one.Kind == change.Kind);
        }
        next.Add(change);
        return next;
    }

    /// <summary>
    /// The list <paramref name="list"/> as the line shows it: the server's tasks with the changes not
    /// yet sent laid over them in order.
    /// </summary>
    public static IReadOnlyList<RemoteTask> Apply(IReadOnlyList<RemoteTask> server, IEnumerable<TaskChange> queue, string list)
    {
        var tasks = new List<RemoteTask>(server);
        foreach (var change in queue)
        {
            if (change.List != list)
            {
                continue;
            }
            var at = tasks.FindIndex(task => task.Id == change.Task);
            switch (change.Kind)
            {
                case TaskChangeKind.Add when at < 0:
                    tasks.Add(new RemoteTask(change.Task, change.Title ?? "", change.Done, false, null, change.At, change.Done ? change.At : null, change.At, null));
                    break;
                case TaskChangeKind.Done when at >= 0 && tasks[at].Done != change.Done:
                    tasks[at] = tasks[at] with { Done = change.Done, Completed = change.Done ? change.At : null };
                    break;
                case TaskChangeKind.Rename when at >= 0:
                    tasks[at] = tasks[at] with { Title = change.Title ?? tasks[at].Title };
                    break;
                case TaskChangeKind.Delete when at >= 0:
                    tasks.RemoveAt(at);
                    break;
            }
        }
        return tasks;
    }

    /// <summary>
    /// After an Add went through: the changes after it that name the new task's own id name the id the
    /// server gave it.
    /// </summary>
    public static List<TaskChange> Rebind(IReadOnlyList<TaskChange> queue, string from, string to) =>
        [.. queue.Select(change => change.Task == from ? change with { Task = to } : change)];

    /// <summary>
    /// A change refused for an older version (412), against the server's task now: whether it goes
    /// again over that version. The last writer wins — the change where it was made at or after the
    /// server's last change, the server's where that came later. A task gone there takes nothing.
    /// </summary>
    public static bool OursWins(TaskChange change, RemoteTask? server) =>
        server is not null && change.At >= server.Modified;

    /// <summary>The open tasks in the order the line shows them: the oldest first, so one added stands last, just over the field it was typed in.</summary>
    public static IReadOnlyList<RemoteTask> Open(IEnumerable<RemoteTask> tasks) =>
        [.. tasks.Where(task => !task.Done).OrderBy(task => task.Created).ThenBy(task => task.Id, StringComparer.Ordinal)];

    /// <summary>The completed tasks, the last ticked off first, as To Do lists them.</summary>
    public static IReadOnlyList<RemoteTask> Completed(IEnumerable<RemoteTask> tasks) =>
        [.. tasks.Where(task => task.Done).OrderByDescending(task => task.Completed ?? task.Modified).ThenBy(task => task.Id, StringComparer.Ordinal)];
}
