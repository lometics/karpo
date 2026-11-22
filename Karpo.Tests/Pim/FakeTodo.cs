// A Microsoft account's To Do without Graph: lists and tasks the test gives it, kept as a server
// keeps them — each change stamped with a new version, a change against an old version refused (412)
// — and able to fail as a dead connection or an account without To Do's consent does.

using Karpo.Accounts;

namespace Karpo.Tests.Pim;

internal sealed class FakeTodo : IAccountReader, ITaskReader
{
    private readonly Lock gate = new();
    private int version;
    private int made;

    public FakeTodo(string address = "me@outlook.com", Account? account = null)
    {
        Account = account ?? new Account { Id = Account.NewId(), Kind = AccountKind.Microsoft, Address = address };
    }

    public Account Account { get; }

    /// <summary>What every call throws, instead of answering; null answers.</summary>
    public Exception? Failure { get; set; }

    /// <summary>When the server says a change of its own was made: what a task changed there is stamped with.</summary>
    public DateTimeOffset Clock { get; set; } = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The lists are read only once this is set: what a line shows while the first read is on its way.</summary>
    public TaskCompletionSource? Held { get; set; }

    /// <summary>What a call throws before it does anything, by its words ("update tasks a"); null where it goes through.</summary>
    public Func<string, Exception?>? Fails { get; set; }

    /// <summary>
    /// What the next Add throws after it made the task: the answer lost on its way back — a timeout, a
    /// connection dropped —, To Do having made it all the same. Thrown once.
    /// </summary>
    public Exception? Lost { get; set; }

    /// <summary>What reading one list's tasks throws, by the list's id: a list To Do will not give.</summary>
    public Dictionary<string, Exception> Refused { get; } = new(StringComparer.Ordinal);

    public List<RemoteTaskList> Lists { get; } = [];

    public Dictionary<string, List<RemoteTask>> Tasks { get; } = new(StringComparer.Ordinal);

    /// <summary>Every call, as "verb list task", in order.</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The same account with <paramref name="key"/> turned on or off, holding this one's server.</summary>
    public FakeTodo Turned(string key, bool on)
    {
        var turned = new FakeTodo(account: Account.Synced(key, on)) { Failure = Failure, Clock = Clock };
        turned.Lists.AddRange(Lists);
        foreach (var (list, refusal) in Refused)
        {
            turned.Refused[list] = refusal;
        }
        foreach (var (list, tasks) in Tasks)
        {
            turned.Tasks[list] = tasks;
        }
        return turned;
    }

    /// <summary>A list with <paramref name="tasks"/> on the server.</summary>
    public FakeTodo With(string id, string name, bool isDefault, params RemoteTask[] tasks)
    {
        Lists.Add(new RemoteTaskList(id, name, isDefault));
        Tasks[id] = [.. tasks.Select(task => task with { ETag = task.ETag ?? Tag() })];
        return this;
    }

    public static RemoteTask Task(string id, string title, bool done = false, bool important = false, DateOnly? due = null, int minutes = 0) =>
        new(id, title, done, important, due, new DateTimeOffset(2026, 10, 1, 8, minutes, 0, TimeSpan.Zero), done ? new DateTimeOffset(2026, 10, 2, 8, minutes, 0, TimeSpan.Zero) : null,
            new DateTimeOffset(2026, 10, 1, 8, minutes, 0, TimeSpan.Zero), null);

    /// <summary>Another device changed the task on the server, at <see cref="Clock"/>.</summary>
    public void ChangedThere(string list, string task, Func<RemoteTask, RemoteTask> change)
    {
        lock (gate)
        {
            var tasks = Tasks[list];
            var at = tasks.FindIndex(one => one.Id == task);
            tasks[at] = change(tasks[at]) with { Modified = Clock, ETag = Tag() };
        }
    }

    public RemoteTask? Find(string list, string task)
    {
        lock (gate)
        {
            return Tasks.GetValueOrDefault(list)?.FirstOrDefault(one => one.Id == task);
        }
    }

    private string Tag() => $"W/\"{Interlocked.Increment(ref version)}\"";

    private void Call(string what)
    {
        lock (gate)
        {
            Calls.Add(what);
        }
        if (Failure is { } failure)
        {
            throw failure;
        }
        if (Fails?.Invoke(what) is { } fails)
        {
            throw fails;
        }
    }

    public async Task<IReadOnlyList<RemoteTaskList>> TaskListsAsync(CancellationToken cancellation)
    {
        Call("lists");
        if (Held is { } held)
        {
            await held.Task.ConfigureAwait(false);
        }
        lock (gate)
        {
            return [.. Lists];
        }
    }

    public Task<IReadOnlyList<RemoteTask>> TasksAsync(string list, CancellationToken cancellation)
    {
        Call($"tasks {list}");
        if (Refused.TryGetValue(list, out var refusal))
        {
            throw refusal;
        }
        lock (gate)
        {
            return System.Threading.Tasks.Task.FromResult<IReadOnlyList<RemoteTask>>([.. Tasks[list]]);
        }
    }

    public Task<RemoteTask> AddTaskAsync(string list, string title, bool done, CancellationToken cancellation)
    {
        Call($"add {list} {title}");
        lock (gate)
        {
            var task = new RemoteTask($"srv{++made}", title.Trim(), done, false, null, Clock, done ? Clock : null, Clock, Tag());
            Tasks[list].Add(task);
            if (Lost is { } lost)
            {
                Lost = null;
                throw lost;
            }
            return System.Threading.Tasks.Task.FromResult(task);
        }
    }

    public Task<RemoteTask?> UpdateTaskAsync(string list, string task, TaskEdit edit, string? etag, CancellationToken cancellation)
    {
        Call($"update {list} {task}");
        lock (gate)
        {
            var tasks = Tasks[list];
            var at = tasks.FindIndex(one => one.Id == task);
            if (at < 0)
            {
                return System.Threading.Tasks.Task.FromResult<RemoteTask?>(null);
            }
            if (etag is not null && etag != tasks[at].ETag)
            {
                throw new TaskConflictException("412");
            }
            var changed = tasks[at];
            if (edit.Title is { } title)
            {
                changed = changed with { Title = title };
            }
            if (edit.Done is { } done)
            {
                changed = changed with { Done = done, Completed = done ? Clock : null };
            }
            tasks[at] = changed = changed with { Modified = Clock, ETag = Tag() };
            return System.Threading.Tasks.Task.FromResult<RemoteTask?>(changed);
        }
    }

    public Task DeleteTaskAsync(string list, string task, string? etag, CancellationToken cancellation)
    {
        Call($"delete {list} {task}");
        lock (gate)
        {
            var tasks = Tasks[list];
            var at = tasks.FindIndex(one => one.Id == task);
            if (at >= 0)
            {
                if (etag is not null && etag != tasks[at].ETag)
                {
                    throw new TaskConflictException("412");
                }
                tasks.RemoveAt(at);
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    public Task<RemoteTask?> TaskAsync(string list, string task, CancellationToken cancellation)
    {
        Call($"get {list} {task}");
        return System.Threading.Tasks.Task.FromResult(Find(list, task));
    }

    public Task RenameListAsync(string list, string name, CancellationToken cancellation)
    {
        Call($"rename list {list} {name}");
        lock (gate)
        {
            var at = Lists.FindIndex(one => one.Id == list);
            if (at >= 0)
            {
                Lists[at] = Lists[at] with { Name = name };
            }
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    public Task DeleteListAsync(string list, CancellationToken cancellation)
    {
        Call($"delete list {list}");
        lock (gate)
        {
            Lists.RemoveAll(one => one.Id == list);
            Tasks.Remove(list);
            return System.Threading.Tasks.Task.CompletedTask;
        }
    }

    // The rest of the account is not what these tests are about.
    public Task<IReadOnlyList<CalendarRead>> CalendarsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellation) =>
        System.Threading.Tasks.Task.FromResult<IReadOnlyList<CalendarRead>>([]);

    public Task<IReadOnlyList<BookRead>> ContactsAsync(CancellationToken cancellation) =>
        System.Threading.Tasks.Task.FromResult<IReadOnlyList<BookRead>>([]);

    public Task<IReadOnlyList<FolderRead>> MailAsync(int count, CancellationToken cancellation) =>
        System.Threading.Tasks.Task.FromResult<IReadOnlyList<FolderRead>>([]);
}
