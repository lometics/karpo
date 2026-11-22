// Microsoft To Do in the dock (Karpo.Dock\Pim\TaskStore.cs, docs\accounts.md "To Do"): the lists and
// tasks read and shown, a line's list picked by id or the default, a change shown at once and sent,
// a change made offline kept on disk and sent at the next start, a conflict settled by the last
// writer, an account without To Do's consent waiting for a reconnect, and what the Accounts page is
// told. Every store has a folder of its own under the run's isolated folder; no account is reached.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class TaskStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private static string Folder() => Path.Combine(Isolation.Folder, "tasks-" + Guid.NewGuid().ToString("N"));

    private static FakeTodo Todo() => new FakeTodo()
        .With("tasks", "Tasks", true, FakeTodo.Task("a", "Call the plumber", minutes: 1), FakeTodo.Task("b", "Buy stamps", important: true, minutes: 2),
            FakeTodo.Task("c", "Pay rent", done: true, minutes: 3))
        .With("shop", "Groceries", false, FakeTodo.Task("m", "Milk"));

    private static TaskStore Store(FakeTodo todo, string? folder = null, Func<DateTimeOffset>? now = null) =>
        Store(new AccountSet([todo]), folder, now);

    private static TaskStore Store(AccountSet set, string? folder = null, Func<DateTimeOffset>? now = null)
    {
        folder ??= Folder();
        return new TaskStore(set, cache: new PimCache(folder), now: now ?? (() => Now), report: new SyncReport(folder));
    }

    private static async Task<TaskStore> Read(TaskStore store)
    {
        store.Refresh();
        await store.Idle();
        return store;
    }

    [Fact]
    public async Task TheDefaultListIsShownWhereTheLineNamesNoneOrOneGone()
    {
        var todo = Todo();
        var store = await Read(Store(todo));

        foreach (var named in new[] { null, "", "gone" })
        {
            var shown = store.Show("ME@outlook.com", named);
            Assert.Equal(TaskListState.Shown, shown.State);
            Assert.Equal("Tasks", shown.Name);
            Assert.Equal(["Call the plumber", "Buy stamps"], shown.Open.Select(task => task.Title));
            Assert.Equal(["Pay rent"], shown.Completed.Select(task => task.Title));
        }
        Assert.Equal(["Milk"], store.Show("me@outlook.com", "shop").Open.Select(task => task.Title));
        Assert.Equal(TaskListState.NoAccount, store.Show("other@outlook.com", null).State);
    }

    [Fact]
    public void ADefaultIsPickedByItsFlagThenTheFirst()
    {
        RemoteTaskList[] lists = [new("x", "Work", false), new("t", "Tasks", true)];
        Assert.Equal("t", TaskStore.Pick(lists, null)!.Id);
        Assert.Equal("x", TaskStore.Pick(lists, "x")!.Id);
        Assert.Equal("x", TaskStore.Pick([new("x", "Work", false)], "gone")!.Id);
        Assert.Null(TaskStore.Pick([], null));
    }

    [Fact]
    public async Task BeforeTheFirstReadTheLineIsLoading()
    {
        var store = Store(Todo());
        Assert.Equal(TaskListState.Loading, store.Show("me@outlook.com", null).State);
        await Read(store);
        Assert.Equal(TaskListState.Shown, store.Show("me@outlook.com", null).State);
    }

    [Fact]
    public async Task ATickAnAddARenameAndADeleteAreShownAtOnceAndSent()
    {
        var todo = Todo();
        var store = await Read(Store(todo));
        var shown = store.Show("me@outlook.com", null);

        store.SetDone(shown.Account!, shown.List!, "a", true);
        var added = store.Add(shown.Account!, shown.List!, "  Water the plants  ")!;
        Assert.True(TaskQueue.IsNew(added));
        store.Rename(shown.Account!, shown.List!, "b", "Buy stamps and envelopes");
        // At once, before anything was sent: the server's tasks with the changes over them.
        var now = store.Show("me@outlook.com", null);
        Assert.Contains(now.Completed, task => task.Id == "a");
        Assert.Contains(now.Open, task => task.Title == "Water the plants");

        await store.Idle();
        Assert.True(todo.Find("tasks", "a")!.Done);
        Assert.Equal("Buy stamps and envelopes", todo.Find("tasks", "b")!.Title);
        var made = Assert.Single(todo.Tasks["tasks"], task => task.Title == "Water the plants");

        // The new task has the server's id now, and a delete of it goes there by that id.
        var after = store.Show("me@outlook.com", null);
        Assert.Contains(after.Open, task => task.Id == made.Id);
        store.Delete(shown.Account!, shown.List!, made.Id);
        await store.Idle();
        Assert.Null(todo.Find("tasks", made.Id));
        Assert.DoesNotContain(store.Show("me@outlook.com", null).Open, task => task.Id == made.Id);
    }

    [Fact]
    public async Task WordsOfSpacesDeleteTheTask()
    {
        var todo = Todo();
        var store = await Read(Store(todo));
        var shown = store.Show("me@outlook.com", null);
        store.Rename(shown.Account!, shown.List!, "a", "   ");
        await store.Idle();
        Assert.Null(todo.Find("tasks", "a"));
        Assert.Null(store.Add(shown.Account!, shown.List!, " "));
    }

    [Fact]
    public async Task AChangeMadeOfflineIsKeptAndSentAtTheNextStart()
    {
        var folder = Folder();
        var todo = Todo();
        var first = await Read(Store(todo, folder));
        var shown = first.Show("me@outlook.com", null);

        todo.Failure = new HttpRequestException("offline");
        first.SetDone(shown.Account!, shown.List!, "b", true);
        first.Add(shown.Account!, shown.List!, "Book the train");
        await first.Idle();

        // Shown, not sent, and the list says it could not be refreshed.
        var offline = first.Show("me@outlook.com", null);
        Assert.Contains(offline.Completed, task => task.Id == "b");
        Assert.Contains(offline.Open, task => task.Title == "Book the train");
        Assert.True(offline.Freshness.Stale);
        Assert.False(todo.Find("tasks", "b")!.Done);

        // Another start, still offline: the kept tasks and changes are there at once.
        var second = await Read(Store(todo, folder));
        var kept = second.Show("me@outlook.com", null);
        Assert.Equal(TaskListState.Shown, kept.State);
        Assert.Contains(kept.Completed, task => task.Id == "b");
        Assert.Contains(kept.Open, task => task.Title == "Book the train");

        // Back online: sent, once each.
        todo.Failure = null;
        second.Refresh(force: true);
        await second.Idle();
        Assert.True(todo.Find("tasks", "b")!.Done);
        Assert.Single(todo.Tasks["tasks"], task => task.Title == "Book the train");
        Assert.False(second.Show("me@outlook.com", null).Freshness.Stale);
    }

    [Fact]
    public async Task AConflictIsWonByTheLastWriter()
    {
        var todo = Todo();
        var clock = Now;
        var store = await Read(Store(todo, now: () => clock));
        var shown = store.Show("me@outlook.com", null);

        // Renamed on another device before this change, which is newer: this one wins.
        todo.Clock = Now.AddMinutes(-5);
        todo.ChangedThere("tasks", "a", task => task with { Title = "Call the plumber today" });
        store.Rename(shown.Account!, shown.List!, "a", "Call the electrician");
        await store.Idle();
        Assert.Equal("Call the electrician", todo.Find("tasks", "a")!.Title);

        // Renamed there after this change was made (offline, sent later): the server's stands.
        todo.Failure = new HttpRequestException("offline");
        clock = Now.AddMinutes(1);
        store.Rename(shown.Account!, shown.List!, "b", "Buy two stamps");
        await store.Idle();
        todo.Clock = Now.AddMinutes(10);
        todo.ChangedThere("tasks", "b", task => task with { Title = "Buy a book of stamps" });
        todo.Failure = null;
        store.Refresh(force: true);
        await store.Idle();
        Assert.Equal("Buy a book of stamps", todo.Find("tasks", "b")!.Title);
        Assert.Contains(store.Show("me@outlook.com", null).Open, task => task.Title == "Buy a book of stamps");
    }

    [Fact]
    public async Task AnAccountWithoutToDosConsentWaitsForAReconnect()
    {
        var folder = Folder();
        var todo = Todo();
        todo.Failure = new ConsentException(AccountKind.Microsoft, AccountData.Tasks);
        var set = new AccountSet([todo]);
        var store = await Read(Store(set, folder));

        Assert.Equal(TaskListState.Consent, store.Show("me@outlook.com", null).State);
        var told = new SyncReport(folder).Read().For(todo.Account.Id)!.Tasks!;
        Assert.True(told.Consent);
        Assert.Null(told.Problem);

        // Signed in to again on the Accounts page: accounts.json is written, and the tasks are read.
        todo.Failure = null;
        set.Rewrite();
        await store.Idle();
        Assert.Equal(TaskListState.Shown, store.Show("me@outlook.com", null).State);
        Assert.Null(new SyncReport(folder).Read().For(todo.Account.Id)!.Tasks!.Consent);
    }

    [Fact]
    public async Task AListTurnedOffIsNotReadAndTheReportSaysWhatEachHolds()
    {
        var folder = Folder();
        var todo = Todo().Turned(SyncKeys.TaskList("shop"), false);
        var store = await Read(Store(todo, folder));

        Assert.DoesNotContain("tasks shop", todo.Calls);
        Assert.Equal(TaskListState.Off, store.Show("me@outlook.com", "shop").State);
        var told = new SyncReport(folder).Read().For(todo.Account.Id)!.Tasks!;
        Assert.Equal([("Tasks", 2, false, true), ("Groceries", -1, true, false)],
            told.Items.Select(item => (item.Name, item.Count ?? -1, item.Off, item.Default == true)));
        Assert.Equal(Now.UtcDateTime, told.UpdatedUtc);
    }

    /// <summary>
    /// The person, 2026-10-29: "there is one error, invalid request. the settings for the account (which calendars /
    /// contact lists to show etc.) should still be available although there might not be any widget that uses it
    /// yet." One list To Do will not give (Graph's bare 400) failed the account's whole read: no list was listed,
    /// so ⚙ › Accounts had no switch to turn it off with. Now it fails alone: the others are read and every list is
    /// listed with its switch, no widget showing any; the refused one says why, on its row and in the edit bar's
    /// errors; turned off, it is not asked again and the error goes.
    /// </summary>
    [Fact]
    public async Task AListToDoRefusesFailsAloneAndCanBeTurnedOff()
    {
        var folder = Folder();
        var todo = Todo().With("flagged", "Flagged email", false);
        todo.Refused["flagged"] = new AccountException("Microsoft answered 400: Invalid request", 400);
        var set = new AccountSet([todo]);
        var store = await Read(Store(set, folder));

        var shown = store.Show("me@outlook.com", null);
        Assert.Equal((TaskListState.Shown, false), (shown.State, shown.Freshness.Stale));
        Assert.Equal(["Milk"], store.Show("me@outlook.com", "shop").Open.Select(task => task.Title));
        var flagged = store.Show("me@outlook.com", "flagged");
        Assert.Equal((TaskListState.Failed, "Flagged email"), (flagged.State, flagged.Name));
        Assert.Contains("“Flagged email”", flagged.Problem, StringComparison.Ordinal);
        Assert.Contains("Invalid request", flagged.Problem, StringComparison.Ordinal);

        var told = new SyncReport(folder).Read().For(todo.Account.Id)!;
        Assert.Null(told.Tasks!.Problem);
        var group = AccountSyncRows.Menu(todo.Account, told, Now.LocalDateTime).Groups.Single(group => group.Title == S.SettingsAccountsGivesTasks);
        Assert.Equal([("Tasks", true), ("Groceries", true), ("Flagged email", true)], group.Parts.Select(part => (part.Name, part.On)));
        Assert.Equal(flagged.Problem, group.Parts[2].Detail);
        var fault = Assert.Single(FaultSources.Accounts([todo.Account], new SyncReport(folder).Read()));
        Assert.Equal((todo.Account.Address, flagged.Problem, todo.Account.Id), (fault.Name, fault.Message, fault.Of));

        // Turned off on its row: not asked again, and the error is gone.
        var off = todo.Turned(SyncKeys.TaskList("flagged"), false);
        set.Become([off]);
        await store.Idle();
        Assert.Contains("lists", off.Calls);
        Assert.DoesNotContain("tasks flagged", off.Calls);
        Assert.Equal(TaskListState.Off, store.Show("me@outlook.com", "flagged").State);
        Assert.Empty(FaultSources.Accounts([off.Account], new SyncReport(folder).Read()));
    }

    [Fact]
    public async Task TheCacheIsEncryptedAndHoldsNoSecret()
    {
        var folder = Folder();
        var todo = new FakeTodo(account: new Account { Id = Account.NewId(), Kind = AccountKind.Microsoft, Address = "me@outlook.com", Secret = "SECRET-BLOB" })
            .With("tasks", "Tasks", true, FakeTodo.Task("a", "Renew the passport"));
        await Read(Store(todo, folder));

        var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, PimCache.TasksFile));
        var text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.DoesNotContain("Renew the passport", text);
        var plain = System.Text.Encoding.UTF8.GetString(PimCache.Unprotect(bytes));
        Assert.Contains("Renew the passport", plain);
        Assert.DoesNotContain("SECRET-BLOB", plain);
        Assert.DoesNotContain("me@outlook.com", plain);
    }

    /// <summary>Until <paramref name="done"/> holds, five seconds at most: what was sent from the dock's thread without a handle to wait on.</summary>
    private static async Task Until(Func<bool> done)
    {
        for (var waited = 0; !done() && waited < 5000; waited += 20)
        {
            await Task.Delay(20);
        }
        Assert.True(done());
    }

    /// <summary>
    /// §324: a list renamed or deleted in a to-do line's menu is so at once — on the line and in the
    /// Accounts page's report — and in To Do after; its default "Tasks" is neither, nor a name of spaces.
    /// A list deleted takes its tasks and the changes waiting for it; a line showing it shows the default.
    /// </summary>
    [Fact]
    public async Task AListIsRenamedAndDeletedAtOnceAndInToDoButNeverTheDefault()
    {
        var folder = Folder();
        var todo = Todo().With("trip", "Trip", false, FakeTodo.Task("p", "Passport"));
        var store = await Read(Store(todo, folder));
        string? Told(string list) => new SyncReport(folder).Read().For(todo.Account.Id)!.Tasks!.Items.FirstOrDefault(item => item.Id == list)?.Name;

        Assert.False(store.RenameList("me@outlook.com", "tasks", "Inbox"));
        Assert.False(store.RenameList("me@outlook.com", "shop", "  "));
        Assert.False(store.RenameList("other@outlook.com", "shop", "Food"));
        Assert.True(store.RenameList("ME@outlook.com", "shop", "  Food \n"));
        Assert.Equal("Food", store.Show("me@outlook.com", "shop").Name);
        Assert.Equal("Food", Told("shop"));
        Assert.False(store.RenameList("me@outlook.com", "shop", "Food"));
        await Until(() => todo.Calls.Contains("rename list shop Food"));
        await Until(() => !store.Reading);
        Assert.Equal("Food", store.Show("me@outlook.com", "shop").Name);

        Assert.False(store.DeleteList("me@outlook.com", "tasks"));
        Assert.False(store.DeleteList("me@outlook.com", "gone"));
        Assert.True(store.DeleteList("me@outlook.com", "trip"));
        Assert.Equal("Tasks", store.Show("me@outlook.com", "trip").Name);
        Assert.Null(Told("trip"));
        await Until(() => todo.Calls.Contains("delete list trip"));
        await Until(() => !store.Reading);
        Assert.DoesNotContain(todo.Lists, list => list.Id == "trip");
        Assert.Equal("Tasks", store.Show("me@outlook.com", "trip").Name);
    }

    /// <summary>
    /// A tick To Do could not take just then — a server error after Graph's own tries, a throttle longer than
    /// Graph waits out, a timeout of its own — is kept and lands at the next pass. It was dropped on any such
    /// answer, the tick gone without a word at the next read.
    /// </summary>
    [Theory]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(429)]
    [InlineData(408)]
    public async Task AChangeToDoCouldNotTakeNowLandsLater(int status)
    {
        var todo = Todo();
        var store = await Read(Store(todo));
        var shown = store.Show("me@outlook.com", null);
        todo.Fails = call => call.StartsWith("update", StringComparison.Ordinal) ? new AccountException($"Microsoft Graph answered {status}", status) : null;

        store.SetDone(shown.Account!, shown.List!, "a", true);
        await store.Idle();
        Assert.False(todo.Find("tasks", "a")!.Done);
        // Still shown ticked, and the list says it could not be brought up to date.
        var waiting = store.Show("me@outlook.com", null);
        Assert.Contains(waiting.Completed, task => task.Id == "a");
        Assert.True(waiting.Freshness.Stale);

        todo.Fails = null;
        store.Refresh(force: true);
        await store.Idle();
        Assert.True(todo.Find("tasks", "a")!.Done);
        Assert.False(store.Show("me@outlook.com", null).Freshness.Stale);
    }

    /// <summary>A change To Do refuses for good (a 400, a list gone) is still dropped, not sent for ever.</summary>
    [Fact]
    public async Task AChangeToDoRefusesIsDropped()
    {
        var todo = Todo();
        var store = await Read(Store(todo));
        var shown = store.Show("me@outlook.com", null);
        todo.Fails = call => call.StartsWith("update", StringComparison.Ordinal) ? new AccountException("Microsoft Graph answered 400", 400) : null;

        store.SetDone(shown.Account!, shown.List!, "a", true);
        await store.Idle();
        todo.Fails = null;
        store.Refresh(force: true);
        await store.Idle();

        Assert.False(todo.Find("tasks", "a")!.Done);
        Assert.Single(todo.Calls, call => call == "update tasks a");
        Assert.DoesNotContain(store.Show("me@outlook.com", null).Completed, task => task.Id == "a");
    }

    /// <summary>
    /// An Add To Do made whose answer never came back (a timeout): sent again as it was, it made the task
    /// twice. Now it is looked for first — and found, the task is that one, at the next pass and after a
    /// restart alike —; one that never reached To Do is sent again, once.
    /// </summary>
    [Fact]
    public async Task AnAddWhoseAnswerWasLostIsNotMadeTwice()
    {
        var folder = Folder();
        var todo = Todo();
        todo.Clock = Now;
        var store = await Read(Store(todo, folder));
        var shown = store.Show("me@outlook.com", null);

        todo.Lost = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.");
        store.Add(shown.Account!, shown.List!, "Water the plants");
        await store.Idle();
        Assert.Single(todo.Tasks["tasks"], task => task.Title == "Water the plants");
        Assert.Single(store.Show("me@outlook.com", null).Open, task => task.Title == "Water the plants");

        // A restart before the next pass: the Add is kept knowing it may have been made.
        store.Dispose();
        var again = await Read(Store(todo, folder));
        again.Refresh(force: true);
        await again.Idle();

        var made = Assert.Single(todo.Tasks["tasks"], task => task.Title == "Water the plants");
        var after = again.Show("me@outlook.com", null);
        Assert.Equal(made.Id, Assert.Single(after.Open, task => task.Title == "Water the plants").Id);
        Assert.Single(todo.Calls, call => call == "add tasks Water the plants");

        // Never reached To Do (refused before it did anything): sent again, and made once.
        todo.Fails = call => call.StartsWith("add", StringComparison.Ordinal) ? new HttpRequestException("connection reset") : null;
        again.Add(shown.Account!, shown.List!, "Post the letter");
        await again.Idle();
        todo.Fails = null;
        again.Refresh(force: true);
        await again.Idle();
        Assert.Single(todo.Tasks["tasks"], task => task.Title == "Post the letter");
        Assert.Single(again.Show("me@outlook.com", null).Open, task => task.Title == "Post the letter");
    }

    /// <summary>
    /// A tried Add takes no later change into itself: renamed before it was found, the task To Do made is
    /// found by the words it was sent with and then renamed — not made again under the new ones.
    /// </summary>
    [Fact]
    public void ATriedAddTakesNoLaterChangeIntoItself()
    {
        var at = Now;
        var add = new TaskChange(TaskChangeKind.Add, "tasks", "new:1", "Milk", false, at) { Tried = true };
        var renamed = TaskQueue.Enqueue([add], new TaskChange(TaskChangeKind.Rename, "tasks", "new:1", "Oat milk", false, at));
        Assert.Equal(["Milk", "Oat milk"], renamed.Select(change => change.Title));
        var deleted = TaskQueue.Enqueue(renamed, new TaskChange(TaskChangeKind.Delete, "tasks", "new:1", null, false, at));
        Assert.Equal([TaskChangeKind.Add, TaskChangeKind.Delete], deleted.Select(change => change.Kind));

        // Never sent: folded as ever.
        var fresh = add with { Tried = false };
        Assert.Equal(["Oat milk"], TaskQueue.Enqueue([fresh], new TaskChange(TaskChangeKind.Rename, "tasks", "new:1", "Oat milk", false, at)).Select(change => change.Title));
        Assert.Empty(TaskQueue.Enqueue([fresh], new TaskChange(TaskChangeKind.Delete, "tasks", "new:1", null, false, at)));
    }
}
