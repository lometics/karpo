// Microsoft To Do through Graph (Karpo.Accounts\Microsoft\MicrosoftTodo.cs) and the queue's rules
// (Karpo.Accounts\Tasks.cs): a list and a task as Graph gives them — the default list, status,
// importance, a due day in whatever zone it comes — what each change sends, a refused version, a task
// already gone, and how changes not sent yet fold together and lie over the server's tasks.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Karpo.Accounts;
using Karpo.Accounts.Microsoft;

namespace Karpo.Tests.Accounts;

public sealed class ToDoTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");

    private static readonly TimeZoneInfo NewYork = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

    /// <summary>Graph, answering each request from <paramref name="answer"/> and keeping what it was sent.</summary>
    private sealed class Server(Func<HttpRequestMessage, (HttpStatusCode Status, string Body)> answer) : HttpMessageHandler
    {
        public List<(string Method, string Url, string? IfMatch, string Body)> Sent { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            var ifMatch = request.Headers.TryGetValues("If-Match", out var tags) ? tags.Single() : null;
            Sent.Add((request.Method.Method, Uri.UnescapeDataString(request.RequestUri!.ToString()), ifMatch, body));
            var (status, text) = answer(request);
            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }

    private static (MicrosoftTodo Todo, Server Server) Todo(Func<HttpRequestMessage, (HttpStatusCode, string)> answer)
    {
        var server = new Server(answer);
        var account = new Account { Id = "acc", Kind = AccountKind.Microsoft, Address = "me@outlook.com" };
        return (new MicrosoftTodo(account, new Graph(new HttpClient(server), _ => Task.FromResult("token"))), server);
    }

    private static JsonObject Json(string text) => JsonNode.Parse(text)!.AsObject();

    // --- What Graph gives -------------------------------------------------------------------------------

    [Fact]
    public void AListIsItsNameAndWhetherItIsTheDefault()
    {
        Assert.Equal(new RemoteTaskList("L1", "Tasks", true), MicrosoftTodo.List(Json("""{ "id": "L1", "displayName": "Tasks", "wellknownListName": "defaultList" }""")));
        Assert.Equal(new RemoteTaskList("L2", "Groceries", false), MicrosoftTodo.List(Json("""{ "id": "L2", "displayName": " Groceries ", "wellknownListName": "none" }""")));
        Assert.Null(MicrosoftTodo.List(Json("""{ "displayName": "No id" }""")));
    }

    [Fact]
    public void ATaskIsItsTitleTickStarVersionAndTimes()
    {
        var task = MicrosoftTodo.Parse(Json("""
            {
              "@odata.etag": "W/\"abc\"",
              "id": "T1", "title": "Renew passport", "status": "completed", "importance": "high",
              "createdDateTime": "2026-10-01T08:00:00.1234567Z",
              "lastModifiedDateTime": "2026-10-02T10:30:00Z",
              "completedDateTime": { "dateTime": "2026-10-02T00:00:00.0000000", "timeZone": "UTC" }
            }
            """))!;
        Assert.Equal("Renew passport", task.Title);
        Assert.True(task.Done);
        Assert.True(task.Important);
        Assert.Equal("W/\"abc\"", task.ETag);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 10, 30, 0, TimeSpan.Zero), task.Modified);
        Assert.Equal(new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero), task.Completed);
        Assert.Null(task.Due);

        // Every status but completed is open; normal and low importance carry no star.
        foreach (var status in new[] { "notStarted", "inProgress", "waitingOnOthers", "deferred" })
        {
            var open = MicrosoftTodo.Parse(Json($$"""{ "id": "T", "title": "x", "status": "{{status}}", "importance": "normal" }"""))!;
            Assert.False(open.Done);
            Assert.False(open.Important);
            Assert.Null(open.Completed);
        }
        Assert.Equal("", MicrosoftTodo.Parse(Json("""{ "id": "T" }"""))!.Title);
        Assert.Null(MicrosoftTodo.Parse(Json("""{ "title": "No id" }""")));
    }

    [Theory]
    // To Do's own: the day's midnight in the zone it names, or that midnight carried to UTC.
    [InlineData("2026-10-20T00:00:00.0000000", "W. Europe Standard Time", "Zurich", "2026-10-20")]
    [InlineData("2026-10-20T00:00:00.0000000", "UTC", "NewYork", "2026-10-20")]
    [InlineData("2026-10-19T22:00:00.0000000", "UTC", "Zurich", "2026-10-20")]
    [InlineData("2026-10-20T04:00:00.0000000", "UTC", "NewYork", "2026-10-20")]
    // A midnight in an IANA zone, and a zone nobody knows, read as the day they say.
    [InlineData("2026-12-31T00:00:00", "Europe/Zurich", "NewYork", "2026-12-31")]
    [InlineData("2026-03-01T00:00:00", "Mars/Olympus", "Zurich", "2026-03-01")]
    public void ADueDayIsTheDayItWasSetFor(string dateTime, string zone, string here, string day)
    {
        var due = MicrosoftTodo.Due(Json($$"""{ "dateTime": "{{dateTime}}", "timeZone": "{{zone}}" }"""), here == "Zurich" ? Zurich : NewYork);
        Assert.Equal(DateOnly.Parse(day, System.Globalization.CultureInfo.InvariantCulture), due);
    }

    [Fact]
    public void NoDueDayIsNone()
    {
        Assert.Null(MicrosoftTodo.Due(null, Zurich));
        Assert.Null(MicrosoftTodo.Due(Json("""{ "dateTime": "soon", "timeZone": "UTC" }"""), Zurich));
    }

    // --- What is asked and sent ---------------------------------------------------------------------------

    [Fact]
    public async Task TheTasksAreTheOpenOnesAndTheCompletedFollowingEveryPage()
    {
        var (todo, server) = Todo(request =>
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.ToString());
            return url.Contains("skip", StringComparison.Ordinal)
                ? (HttpStatusCode.OK, """{ "value": [ { "id": "o2", "title": "Second page" } ] }""")
                : url.Contains("status ne 'completed'", StringComparison.Ordinal)
                    ? (HttpStatusCode.OK, """{ "value": [ { "id": "o1", "title": "Open" } ], "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/todo/lists/L%201/tasks?skip=1" }""")
                    : (HttpStatusCode.OK, """{ "value": [ { "id": "d1", "title": "Done", "status": "completed" }, { "id": "o1", "title": "Open" } ] }""");
        });

        var tasks = await todo.TasksAsync("L 1", CancellationToken.None);

        Assert.Equal(["o1", "o2", "d1"], tasks.Select(task => task.Id));
        Assert.All(server.Sent, sent => Assert.Equal("GET", sent.Method));
        Assert.Contains(server.Sent, sent => sent.Url.Contains("/me/todo/lists/L 1/tasks?", StringComparison.Ordinal) && sent.Url.Contains("$filter=status eq 'completed'", StringComparison.Ordinal));
    }

    /// <summary>
    /// What Graph answers a To Do list it will not filter (Flagged email's; 2026-10-29: "there is one error,
    /// invalid request"): a bare 400. That list is read plain, every page, and split here — its open tasks,
    /// its newest completed.
    /// </summary>
    private const string InvalidRequest = """{ "error": { "code": "invalidRequest", "message": "Invalid request" } }""";

    [Fact]
    public async Task AListGraphWillNotFilterIsReadPlainAndSplitHere()
    {
        var (todo, server) = Todo(request =>
        {
            var url = Uri.UnescapeDataString(request.RequestUri!.ToString());
            return url.Contains("$filter", StringComparison.Ordinal)
                ? (HttpStatusCode.BadRequest, InvalidRequest)
                : url.Contains("skip", StringComparison.Ordinal)
                    ? (HttpStatusCode.OK, """{ "value": [ { "id": "d2", "title": "Older", "status": "completed", "completedDateTime": { "dateTime": "2026-10-01T00:00:00", "timeZone": "UTC" } } ] }""")
                    : (HttpStatusCode.OK, """
                        { "value": [
                            { "id": "o1", "title": "Answer Ann", "status": "notStarted" },
                            { "id": "d1", "title": "Newer", "status": "completed", "completedDateTime": { "dateTime": "2026-10-20T00:00:00", "timeZone": "UTC" } } ],
                          "@odata.nextLink": "https://graph.microsoft.com/v1.0/me/todo/lists/F/tasks?$top=100&$skip=100" }
                        """);
        });

        var tasks = await todo.TasksAsync("F", CancellationToken.None);

        Assert.Equal(["o1", "d1", "d2"], tasks.Select(task => task.Id));
        Assert.Contains(server.Sent, sent => sent.Url.EndsWith("/me/todo/lists/F/tasks?$top=100", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AListRefusedReadPlainTooStaysRefused()
    {
        // Not hidden: a list Graph refuses however it is asked is the caller's to say.
        var (todo, _) = Todo(_ => (HttpStatusCode.BadRequest, InvalidRequest));
        var refused = await Assert.ThrowsAsync<AccountException>(() => todo.TasksAsync("F", CancellationToken.None));
        Assert.Equal(400, refused.Status);
    }

    [Fact]
    public async Task AChangeIsSentAgainstItsVersion()
    {
        var (todo, server) = Todo(request => request.Method == HttpMethod.Delete
            ? (HttpStatusCode.NoContent, "")
            : (HttpStatusCode.OK, """{ "id": "T1", "title": "Milk", "status": "completed", "@odata.etag": "W/\"2\"" }"""));

        var added = await todo.AddTaskAsync("L1", "Milk", false, CancellationToken.None);
        var ticked = await todo.UpdateTaskAsync("L1", "T1", new TaskEdit(Done: true), "W/\"1\"", CancellationToken.None);
        await todo.UpdateTaskAsync("L1", "T1", new TaskEdit(Title: "Oat milk", Done: false), null, CancellationToken.None);
        await todo.DeleteTaskAsync("L1", "T1", "W/\"2\"", CancellationToken.None);

        Assert.Equal("T1", added.Id);
        Assert.True(ticked!.Done);
        Assert.Equal(("POST", """{"title":"Milk"}"""), (server.Sent[0].Method, server.Sent[0].Body));
        Assert.EndsWith("/me/todo/lists/L1/tasks", server.Sent[0].Url, StringComparison.Ordinal);
        Assert.Equal(("PATCH", "W/\"1\"", """{"status":"completed"}"""), (server.Sent[1].Method, server.Sent[1].IfMatch, server.Sent[1].Body));
        Assert.EndsWith("/me/todo/lists/L1/tasks/T1", server.Sent[1].Url, StringComparison.Ordinal);
        Assert.Equal((null, """{"title":"Oat milk","status":"notStarted"}"""), (server.Sent[2].IfMatch, server.Sent[2].Body));
        Assert.Equal(("DELETE", "W/\"2\""), (server.Sent[3].Method, server.Sent[3].IfMatch));
    }

    /// <summary>§324: a list renamed is its displayName patched; deleted, the list itself; one gone already is no failure.</summary>
    [Fact]
    public async Task AListIsRenamedAndDeletedThere()
    {
        var (todo, server) = Todo(request => request.Method == HttpMethod.Delete && request.RequestUri!.ToString().EndsWith("/L9", StringComparison.Ordinal)
            ? (HttpStatusCode.NotFound, """{ "error": { "code": "ErrorItemNotFound" } }""")
            : (HttpStatusCode.NoContent, ""));
        await todo.RenameListAsync("L 1", "Food", CancellationToken.None);
        await todo.DeleteListAsync("L 1", CancellationToken.None);
        await todo.DeleteListAsync("L9", CancellationToken.None);
        Assert.Equal(("PATCH", """{"displayName":"Food"}"""), (server.Sent[0].Method, server.Sent[0].Body));
        Assert.EndsWith("/me/todo/lists/L 1", server.Sent[0].Url, StringComparison.Ordinal);
        Assert.Equal("DELETE", server.Sent[1].Method);
        Assert.EndsWith("/me/todo/lists/L 1", server.Sent[1].Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANewerVersionThereIsAConflictAndATaskGoneIsNoFailure()
    {
        var (stale, _) = Todo(_ => (HttpStatusCode.PreconditionFailed, """{ "error": { "code": "PreconditionFailed" } }"""));
        await Assert.ThrowsAsync<TaskConflictException>(() => stale.UpdateTaskAsync("L1", "T1", new TaskEdit(Done: true), "W/\"1\"", CancellationToken.None));
        await Assert.ThrowsAsync<TaskConflictException>(() => stale.DeleteTaskAsync("L1", "T1", "W/\"1\"", CancellationToken.None));

        var (gone, _) = Todo(_ => (HttpStatusCode.NotFound, """{ "error": { "code": "ErrorItemNotFound", "message": "Not found" } }"""));
        Assert.Null(await gone.UpdateTaskAsync("L1", "T1", new TaskEdit(Done: true), null, CancellationToken.None));
        Assert.Null(await gone.TaskAsync("L1", "T1", CancellationToken.None));
        await gone.DeleteTaskAsync("L1", "T1", null, CancellationToken.None);
    }

    // --- The queue ------------------------------------------------------------------------------------------

    private static TaskChange Change(TaskChangeKind kind, string task, string? title = null, bool done = false, int minutes = 0) =>
        new(kind, "L1", task, title, done, At.AddMinutes(minutes));

    [Fact]
    public void ANewTaskTakesItsLaterChangesIntoItsAddAndGoesWithItsDelete()
    {
        var id = TaskQueue.NewId();
        var queue = TaskQueue.Enqueue([], Change(TaskChangeKind.Add, id, "Milk"));
        queue = TaskQueue.Enqueue(queue, Change(TaskChangeKind.Rename, id, "Oat milk"));
        queue = TaskQueue.Enqueue(queue, Change(TaskChangeKind.Done, id, done: true));
        var add = Assert.Single(queue);
        Assert.Equal((TaskChangeKind.Add, "Oat milk", true), (add.Kind, add.Title, add.Done));

        Assert.Empty(TaskQueue.Enqueue(queue, Change(TaskChangeKind.Delete, id)));
    }

    [Fact]
    public void ATasksSecondRenameOrTickTakesThePlaceOfItsFirstAndADeleteOfAll()
    {
        var queue = TaskQueue.Enqueue([], Change(TaskChangeKind.Rename, "T1", "One"));
        queue = TaskQueue.Enqueue(queue, Change(TaskChangeKind.Done, "T1", done: true));
        queue = TaskQueue.Enqueue(queue, Change(TaskChangeKind.Rename, "T2", "Other"));
        queue = TaskQueue.Enqueue(queue, Change(TaskChangeKind.Rename, "T1", "Two"));
        Assert.Equal([("T1", TaskChangeKind.Done), ("T2", TaskChangeKind.Rename), ("T1", TaskChangeKind.Rename)], queue.Select(change => (change.Task, change.Kind)));
        Assert.Equal("Two", queue[^1].Title);

        queue = TaskQueue.Enqueue(queue, Change(TaskChangeKind.Delete, "T1"));
        Assert.Equal([("T2", TaskChangeKind.Rename), ("T1", TaskChangeKind.Delete)], queue.Select(change => (change.Task, change.Kind)));
    }

    [Fact]
    public void TheQueueLiesOverTheServersTasksInOrder()
    {
        RemoteTask[] server =
        [
            new("T1", "Milk", false, false, null, At, null, At, "e1"),
            new("T2", "Bread", false, true, new DateOnly(2026, 10, 20), At.AddMinutes(1), null, At, "e2"),
            new("T3", "Eggs", true, false, null, At.AddMinutes(2), At, At, "e3"),
        ];
        var id = TaskQueue.NewId();
        var shown = TaskQueue.Apply(server,
        [
            Change(TaskChangeKind.Done, "T1", done: true, minutes: 5),
            Change(TaskChangeKind.Rename, "T2", "Rye bread"),
            Change(TaskChangeKind.Delete, "T3"),
            Change(TaskChangeKind.Add, id, "Butter", minutes: 6),
            new TaskChange(TaskChangeKind.Delete, "Other list", "T1", null, false, At),
        ], "L1");

        Assert.Equal(["T1", "T2", id], shown.Select(task => task.Id));
        Assert.True(shown[0].Done);
        Assert.Equal(At.AddMinutes(5), shown[0].Completed);
        Assert.Equal(("Rye bread", true, new DateOnly(2026, 10, 20)), (shown[1].Title, shown[1].Important, shown[1].Due));
        Assert.Equal(("Butter", false), (shown[2].Title, shown[2].Done));
        // The line: the open oldest first, the completed the last ticked first.
        Assert.Equal(["T2", id], TaskQueue.Open(shown).Select(task => task.Id));
        Assert.Equal(["T1"], TaskQueue.Completed(shown).Select(task => task.Id));
    }

    [Fact]
    public void AnAddsLaterChangesNameTheServersIdOnceItIsMade()
    {
        var id = TaskQueue.NewId();
        var queue = TaskQueue.Rebind([Change(TaskChangeKind.Rename, id, "x"), Change(TaskChangeKind.Done, "T9", done: true)], id, "srv1");
        Assert.Equal(["srv1", "T9"], queue.Select(change => change.Task));
    }

    [Fact]
    public void TheLastWriterWins()
    {
        var change = Change(TaskChangeKind.Rename, "T1", "Mine", minutes: 10);
        var server = new RemoteTask("T1", "Theirs", false, false, null, At, null, At.AddMinutes(5), "e");
        Assert.True(TaskQueue.OursWins(change, server));
        Assert.False(TaskQueue.OursWins(change, server with { Modified = At.AddMinutes(11) }));
        Assert.False(TaskQueue.OursWins(change, null));
    }
}
