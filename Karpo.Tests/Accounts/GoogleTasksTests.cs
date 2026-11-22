// Google Tasks through its API (Karpo.Accounts\Google\GoogleTasks.cs), as To Do's are read and written
// (ITaskReader): the lists and which is "My Tasks" (@default), a task as Google gives it — its tick, its
// due date, no star, its last change standing for when it was made —, the open ones all and the completed
// newest, what each change sends (no If-Match: Google documents none), a task or a list already gone.

using System.Net;
using System.Text.Json.Nodes;
using Karpo.Accounts;
using Karpo.Accounts.Google;

namespace Karpo.Tests.Accounts;

public sealed class GoogleTasksTests
{
    private static JsonObject Json(string text) => (JsonObject)JsonNode.Parse(text)!;

    private static (GoogleReader Reader, GoogleReaderTests.Server Server) Tasks(Func<string, (HttpStatusCode, string)> answer)
    {
        var server = new GoogleReaderTests.Server(answer);
        return (GoogleReaderTests.Reader(server), server);
    }

    [Fact]
    public void ATaskIsItsTitleTickDueDayAndTimes()
    {
        var task = GoogleTasks(Json("""
            {
              "id": "t1", "etag": "\"LTE4\"", "title": " Buy milk ", "status": "completed",
              "updated": "2026-11-03T08:00:00.000Z", "completed": "2026-11-03T07:59:00.000Z", "due": "2026-11-05T00:00:00.000Z"
            }
            """))!;

        Assert.Equal(("t1", "Buy milk", true, false), (task.Id, task.Title, task.Done, task.Important));
        // A date, whatever this PC's zone: Google keeps it as that day's UTC midnight.
        Assert.Equal(new DateOnly(2026, 11, 5), task.Due);
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 7, 59, 0, TimeSpan.Zero), task.Completed);
        // Google keeps no time a task was made: its last change stands in.
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero), task.Created);
        Assert.Equal(task.Created, task.Modified);
        Assert.Equal("\"LTE4\"", task.ETag);

        var open = GoogleTasks(Json("""{ "id": "t2", "title": "Call Bob", "status": "needsAction", "updated": "2026-11-03T08:00:00Z" }"""))!;
        Assert.False(open.Done);
        Assert.Null(open.Completed);
        Assert.Null(open.Due);
        Assert.Null(GoogleTasks(Json("""{ "id": "t3", "title": "Gone", "deleted": true }""")));
        Assert.Null(GoogleTasks(Json("""{ "title": "No id" }""")));
    }

    private static RemoteTask? GoogleTasks(JsonObject item) => GoogleReader.Parse(item);

    [Fact]
    public async Task TheListsAreEveryListAndMyTasksIsTheDefault()
    {
        var (reader, server) = Tasks(url => url switch
        {
            _ when url.EndsWith("/users/@me/lists/@default") => (HttpStatusCode.OK, """{ "id": "MY", "title": "My Tasks" }"""),
            _ when url.Contains("pageToken=N") => (HttpStatusCode.OK, """{ "items": [ { "id": "L3", "title": "Later" } ] }"""),
            _ => (HttpStatusCode.OK, """{ "items": [ { "id": "L2", "title": " Groceries " }, { "id": "MY", "title": "My Tasks" } ], "nextPageToken": "N" }"""),
        });

        var lists = await reader.TaskListsAsync(CancellationToken.None);

        Assert.Equal([new RemoteTaskList("L2", "Groceries", false), new RemoteTaskList("MY", "My Tasks", true), new RemoteTaskList("L3", "Later", false)], lists);
        Assert.All(server.Asked, asked => Assert.Equal("token-Tasks", asked.Bearer));
    }

    /// <summary>A list's tasks: every open one, and of the completed — the hidden ones too, which Google's own apps hide — the last ticked off.</summary>
    [Fact]
    public async Task EveryOpenTaskIsReadAndTheNewestCompleted()
    {
        var done = new JsonArray([.. Enumerable.Range(0, GoogleReader.MostCompleted + 5).Select(at => (JsonNode)Json(
            $$"""{ "id": "d{{at}}", "title": "Done {{at}}", "status": "completed", "updated": "2026-11-01T00:00:00Z", "completed": "{{new DateTime(2026, 1, 1).AddHours(at):yyyy-MM-ddTHH:mm:ssZ}}" }"""))]);
        var (reader, server) = Tasks(url => (HttpStatusCode.OK, new JsonObject
        {
            ["items"] = new JsonArray([Json("""{ "id": "o1", "title": "Open", "status": "needsAction", "updated": "2026-11-01T00:00:00Z" }"""), .. done.Select(node => node!.DeepClone())]),
        }.ToJsonString()));

        var tasks = await reader.TasksAsync("L1", CancellationToken.None);

        Assert.Equal("o1", tasks[0].Id);
        Assert.Equal(GoogleReader.MostCompleted + 1, tasks.Count);
        Assert.Equal($"d{GoogleReader.MostCompleted + 4}", tasks[1].Id);
        Assert.DoesNotContain(tasks, task => task.Id == "d0");
        var asked = Assert.Single(server.Urls);
        Assert.Contains("/lists/L1/tasks?showCompleted=true&showHidden=true&maxResults=100", asked);
    }

    /// <summary>
    /// What each change sends: an add its title and status; a tick its status, a put-back its status and no completed time
    /// left; a rename its title; never an If-Match — Google documents no precondition, so the last to reach it wins.
    /// </summary>
    [Fact]
    public async Task EachChangeSendsWhatChangedAndNoVersion()
    {
        var (reader, server) = Tasks(url => (HttpStatusCode.OK, """{ "id": "t1", "title": "Milk", "status": "needsAction", "updated": "2026-11-03T08:00:00Z" }"""));

        Assert.Equal("t1", (await reader.AddTaskAsync("L1", "Milk", done: false, CancellationToken.None)).Id);
        await reader.AddTaskAsync("L1", "Paid", done: true, CancellationToken.None);
        await reader.UpdateTaskAsync("L1", "t1", new TaskEdit(Done: true), "\"v1\"", CancellationToken.None);
        await reader.UpdateTaskAsync("L1", "t1", new TaskEdit(Done: false), "\"v1\"", CancellationToken.None);
        await reader.UpdateTaskAsync("L1", "t1", new TaskEdit(Title: "Oat milk"), null, CancellationToken.None);
        await reader.DeleteTaskAsync("L1", "t1", "\"v1\"", CancellationToken.None);
        await reader.RenameListAsync("L1", "Shop", CancellationToken.None);
        await reader.DeleteListAsync("L1", CancellationToken.None);

        var tasks = "https://tasks.googleapis.com/tasks/v1/lists/L1/tasks";
        Assert.Equal(
            [
                ("POST", tasks, """{"title":"Milk","status":"needsAction"}"""),
                ("POST", tasks, """{"title":"Paid","status":"completed"}"""),
                ("PATCH", tasks + "/t1", """{"status":"completed"}"""),
                ("PATCH", tasks + "/t1", """{"status":"needsAction","completed":null}"""),
                ("PATCH", tasks + "/t1", """{"title":"Oat milk"}"""),
                ("DELETE", tasks + "/t1", ""),
                ("PATCH", "https://tasks.googleapis.com/tasks/v1/users/@me/lists/L1", """{"title":"Shop"}"""),
                ("DELETE", "https://tasks.googleapis.com/tasks/v1/users/@me/lists/L1", ""),
            ],
            server.Asked.Select(asked => (asked.Method, asked.Url, asked.Body)));
    }

    [Fact]
    public async Task ATaskOrAListAlreadyGoneIsNoFailure()
    {
        var (reader, _) = Tasks(_ => (HttpStatusCode.NotFound, """{ "error": { "code": 404, "message": "Task not found." } }"""));

        Assert.Null(await reader.UpdateTaskAsync("L1", "t1", new TaskEdit(Done: true), null, CancellationToken.None));
        Assert.Null(await reader.TaskAsync("L1", "t1", CancellationToken.None));
        await reader.DeleteTaskAsync("L1", "t1", null, CancellationToken.None);
        await reader.DeleteListAsync("L1", CancellationToken.None);
        // A list read that Google refuses is a refusal, as To Do's (TaskStore.Refusal decides what it costs).
        Assert.Equal(404, (await Assert.ThrowsAsync<AccountException>(() => reader.TasksAsync("L1", CancellationToken.None))).Status);
    }
}
