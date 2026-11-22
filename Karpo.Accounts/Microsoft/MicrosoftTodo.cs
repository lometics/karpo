// A Microsoft account's To Do through Graph: /me/todo/lists and each list's
// tasks, read, and a task added, ticked, renamed and deleted (docs\accounts.md,
// "To Do"). Its token is the tasks' own (MicrosoftAuth.TasksTokenAsync), so an
// account that never consented to To Do fails here alone. A list Graph will not
// filter (a bare 400 "Invalid request", Flagged email's) is read plain and split
// here (docs\widgets.md §399).
//
// A due date is a day: To Do keeps it as that day's midnight, either in the
// zone it names or turned to UTC ("2026-10-19T22:00:00" for the 20th in
// Zurich). A midnight is read as the day it says; any other time is a zone's
// midnight carried to UTC, and is read back in this PC's zone (Due).

using System.Globalization;
using System.Text.Json.Nodes;

namespace Karpo.Accounts.Microsoft;

internal sealed class MicrosoftTodo(Account account, Graph graph) : ITaskReader
{
    // Every open task of a list is read, however many: the line lists them all, and one left out would be
    // a task the person never sees. Of the completed only the newest: the line lists them under
    // "Completed", the last ticked off first, as To Do does — the rest are done with, and a list kept for
    // years holds thousands of them.

    /// <summary>Completed tasks read of one list at most: the newest, by when they last changed — which ticking one off is.</summary>
    internal const int MostCompleted = 200;

    private const string Fields = "$select=id,title,status,importance,dueDateTime,createdDateTime,completedDateTime,lastModifiedDateTime";

    public Account Account { get; } = account;

    public MicrosoftTodo(Account account)
        : this(account, new Graph(Web.Http, cancellation => MicrosoftAuth.Shared.TasksTokenAsync(account, cancellation)))
    {
    }

    public async Task<IReadOnlyList<RemoteTaskList>> TaskListsAsync(CancellationToken cancellation)
    {
        var lists = await graph.AllAsync($"{Graph.V1}/me/todo/lists?$top=100", cancellation).ConfigureAwait(false);
        return [.. lists.Select(List).OfType<RemoteTaskList>()];
    }

    public async Task<IReadOnlyList<RemoteTask>> TasksAsync(string list, CancellationToken cancellation)
    {
        var at = Tasks(list);
        try
        {
            var open = await graph.AllAsync($"{at}?{Fields}&$filter=status ne 'completed'&$top=100", cancellation).ConfigureAwait(false);
            // Newest first, so the most read are the newest and not whichever Graph lists first. A list that will
            // not be ordered says 400, as one that will not be filtered does, and is read plain below.
            var done = await graph.AllAsync(
                $"{at}?{Fields}&$filter=status eq 'completed'&$orderby=lastModifiedDateTime desc&$top=100", cancellation, most: MostCompleted).ConfigureAwait(false);
            return [.. open.Concat(done).Select(Parse).OfType<RemoteTask>().DistinctBy(task => task.Id)];
        }
        catch (AccountException refused) when (refused.Status == 400)
        {
            // Graph answers a filtered read of some lists (Flagged email's) with a bare 400 "Invalid request",
            // and their plain read as any other's: every task of it, split here as the filters would have.
            var all = (await graph.AllAsync($"{at}?$top=100", cancellation).ConfigureAwait(false))
                .Select(Parse).OfType<RemoteTask>().DistinctBy(task => task.Id).ToList();
            return [.. all.Where(task => !task.Done),
                .. all.Where(task => task.Done).OrderByDescending(task => task.Completed ?? task.Modified).Take(MostCompleted)];
        }
    }

    public async Task<RemoteTask> AddTaskAsync(string list, string title, bool done, CancellationToken cancellation)
    {
        var body = new JsonObject { ["title"] = title };
        if (done)
        {
            body["status"] = "completed";
        }
        var made = await graph.SendAsync(HttpMethod.Post, Tasks(list), body, null, cancellation).ConfigureAwait(false);
        return (made is null ? null : Parse(made)) ?? throw new AccountException(S.CoreAccountsOauthNotJson("Microsoft"), 201);
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
            body["status"] = done ? "completed" : "notStarted";
        }
        try
        {
            var changed = await graph.SendAsync(HttpMethod.Patch, $"{Tasks(list)}/{Uri.EscapeDataString(task)}", body, etag, cancellation).ConfigureAwait(false);
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
            await graph.SendAsync(HttpMethod.Delete, $"{Tasks(list)}/{Uri.EscapeDataString(task)}", null, etag, cancellation).ConfigureAwait(false);
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
            return Parse(await graph.GetAsync($"{Tasks(list)}/{Uri.EscapeDataString(task)}?{Fields}", cancellation).ConfigureAwait(false));
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            return null;
        }
    }

    public async Task RenameListAsync(string list, string name, CancellationToken cancellation) =>
        await graph.SendAsync(HttpMethod.Patch, ListAt(list), new JsonObject { ["displayName"] = name }, null, cancellation).ConfigureAwait(false);

    public async Task DeleteListAsync(string list, CancellationToken cancellation)
    {
        try
        {
            await graph.SendAsync(HttpMethod.Delete, ListAt(list), null, null, cancellation).ConfigureAwait(false);
        }
        catch (AccountException gone) when (gone.Status == 404)
        {
            // Gone already: deleted on another device, which is what was asked.
        }
    }

    private static string ListAt(string list) => $"{Graph.V1}/me/todo/lists/{Uri.EscapeDataString(list)}";

    private static string Tasks(string list) => $"{ListAt(list)}/tasks";

    /// <summary>One task list as Graph lists it; null for one without an id.</summary>
    internal static RemoteTaskList? List(JsonObject item)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var name = Web.Str(item["displayName"]);
        var isDefault = Web.Str(item["wellknownListName"]) == "defaultList";
        return new RemoteTaskList(id, string.IsNullOrWhiteSpace(name) ? S.WidgetTodoNewList : name.Trim(), isDefault);
    }

    /// <summary>One task as Graph gives it; null for one without an id.</summary>
    internal static RemoteTask? Parse(JsonObject item)
    {
        if (Web.Str(item["id"]) is not { Length: > 0 } id)
        {
            return null;
        }
        var title = Web.Str(item["title"]);
        var created = Instant(item["createdDateTime"]) ?? DateTimeOffset.MinValue;
        var done = Web.Str(item["status"]) == "completed";
        return new RemoteTask(
            id,
            string.IsNullOrWhiteSpace(title) ? "" : title.Trim(),
            done,
            Web.Str(item["importance"]) == "high",
            Due(item["dueDateTime"], TimeZoneInfo.Local),
            created,
            done ? Zoned(item["completedDateTime"]) ?? Instant(item["lastModifiedDateTime"]) : null,
            Instant(item["lastModifiedDateTime"]) ?? created,
            Web.Str(item["@odata.etag"]));
    }

    /// <summary>
    /// A dateTimeTimeZone due date as the day it is due, read in <paramref name="here"/>: a midnight is
    /// the day it says, whatever zone it names; another time is a zone's midnight in the zone it names
    /// (UTC, mostly), turned to <paramref name="here"/>. Null where there is none.
    /// </summary>
    internal static DateOnly? Due(JsonNode? value, TimeZoneInfo here)
    {
        if (Web.Str(value?["dateTime"]) is not { Length: > 0 } text
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
        {
            return null;
        }
        when = DateTime.SpecifyKind(when, DateTimeKind.Unspecified);
        if (when.TimeOfDay == TimeSpan.Zero)
        {
            return DateOnly.FromDateTime(when);
        }
        var zone = Zone(Web.Str(value?["timeZone"])) ?? TimeZoneInfo.Utc;
        var utc = TimeZoneInfo.ConvertTimeToUtc(when, zone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, here));
    }

    /// <summary>A dateTimeTimeZone as an instant: its wall clock in the zone it names, UTC where it names none Windows knows.</summary>
    private static DateTimeOffset? Zoned(JsonNode? value)
    {
        if (Web.Str(value?["dateTime"]) is not { Length: > 0 } text
            || !DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var when))
        {
            return null;
        }
        var zone = Zone(Web.Str(value?["timeZone"])) ?? TimeZoneInfo.Utc;
        var utc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(when, DateTimeKind.Unspecified), zone);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    /// <summary>A zone as Graph names one, Windows' name or IANA's; null for UTC, none, or one Windows does not know.</summary>
    internal static TimeZoneInfo? Zone(string? id)
    {
        if (string.IsNullOrEmpty(id) || id is "UTC" or "Etc/UTC" or "tzone://Microsoft/Utc")
        {
            return null;
        }
        try
        {
            // Windows' names and IANA's both: .NET maps one to the other where ICU is there.
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return null;
        }
    }

    private static DateTimeOffset? Instant(JsonNode? value) =>
        DateTimeOffset.TryParse(Web.Str(value), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at : null;
}
