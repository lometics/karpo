// A file another program holds, or one cut short, is not one with nothing in it
// (Karpo.Core\DurableFile.cs, Karpo.Dock\Pim\AccountSet.cs, PimCache.cs,
// Karpo.Dock\Islands\Shared.cs): accounts.json held while the dock reads it again
// leaves every account and what the stores hold of it; tasks-cache.json held or
// broken at a start keeps the To Do changes not sent yet; an island's file held
// at its read is not written over by a store that never saw it.
//
// A virus scanner, a backup or the indexer is played by a FileStream that shares
// nothing (or only the file's replacing), held for as long as the test needs.
// Every file is under the run's isolated folder.

using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Pim;

public sealed class HeldFileTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    private readonly string folder = Path.Combine(Isolation.Folder, "held-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A store's last write still on its way: the run's isolated folder goes at the end anyway.
        }
    }

    private string TasksFile => Path.Combine(folder, PimCache.TasksFile);

    private static FakeTodo Todo() => new FakeTodo()
        .With("tasks", "Tasks", true, FakeTodo.Task("a", "Call the plumber", minutes: 1), FakeTodo.Task("b", "Buy stamps", minutes: 2));

    private TaskStore Store(AccountSet set) => new(set, cache: new PimCache(folder), now: () => Now);

    private static async Task<TaskStore> Read(TaskStore store)
    {
        store.Refresh();
        await store.Idle();
        return store;
    }

    /// <summary>A file held, sharing nothing: what a virus scanner's or a backup's open is to Karpo.</summary>
    private static FileStream Hold(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    /// <summary>
    /// accounts.json written (a token back) and, as the dock reads it again, held by another program:
    /// the set keeps the account, says nothing changed, and the tasks store keeps the account's tasks
    /// and the change not sent yet — in memory and in tasks-cache.json. Before, the read that failed
    /// was no accounts: the store dropped the account and wrote its cache without it.
    /// </summary>
    [Fact]
    public async Task AccountsHeldAsTheyAreReadAgainLeaveTheAccountAndItsCache()
    {
        var todo = Todo();
        var accounts = Path.Combine(folder, "accounts.json");
        AccountBook.Change(accounts, file => file.Accounts.Add(todo.Account));
        (AccountsRead, IReadOnlyList<IAccountReader>) Load()
        {
            var read = AccountBook.Load(accounts, out var found);
            return (read, [.. found.Where(account => account.Id == todo.Account.Id).Select(_ => (IAccountReader)todo)]);
        }
        using var set = new AccountSet(Load, watch: null);
        Assert.True(set.Known);
        var changed = 0;
        set.Changed += () => changed++;
        using var store = await Read(Store(set));
        var shown = store.Show("me@outlook.com", null);
        todo.Failure = new HttpRequestException("offline");
        store.SetDone(shown.Account!, shown.List!, "b", true);
        await store.Idle();

        using (Hold(accounts))
        {
            set.Reload();
        }
        Assert.Same(todo, Assert.Single(set.Readers));
        Assert.Equal(0, changed);

        store.Refresh(force: true);
        await store.Idle();
        Assert.Contains(store.Show("me@outlook.com", null).Completed, task => task.Id == "b");
        var kept = Assert.Single(new PimCache(folder).ReadTasks()!.Accounts);
        Assert.Equal(todo.Account.Id, kept.Account);
        Assert.Contains(kept.Queue, change => change.Task == "b");
    }

    /// <summary>
    /// accounts.json held by another program as the dock starts: no account is known, so no store
    /// takes anything from its cache or writes over it; once the file reads, the accounts are there
    /// and the cache with them.
    /// </summary>
    [Fact]
    public async Task AccountsHeldAtTheStartAreWaitedForAndTheCacheIsKept()
    {
        var todo = Todo();
        var accounts = Path.Combine(folder, "accounts.json");
        AccountBook.Change(accounts, file => file.Accounts.Add(todo.Account));
        (AccountsRead, IReadOnlyList<IAccountReader>) Load()
        {
            var read = AccountBook.Load(accounts, out var found);
            return (read, [.. found.Where(account => account.Id == todo.Account.Id).Select(_ => (IAccountReader)todo)]);
        }
        using (var before = await Read(Store(new AccountSet([todo]))))
        {
            var shown = before.Show("me@outlook.com", null);
            todo.Failure = new HttpRequestException("offline");
            before.SetDone(shown.Account!, shown.List!, "b", true);
            await before.Idle();
        }

        AccountSet set;
        TaskStore store;
        using (Hold(accounts))
        {
            set = new AccountSet(Load, watch: null);
            Assert.False(set.Known);
            Assert.Empty(set.Readers);
            store = await Read(Store(set));
        }
        using (set)
        using (store)
        {
            // Nothing was taken from the cache, and nothing written over it.
            Assert.Contains(Assert.Single(new PimCache(folder).ReadTasks()!.Accounts).Queue, change => change.Task == "b");

            set.Reload();
            Assert.True(set.Known);
            await store.Idle();
            Assert.Contains(store.Show("me@outlook.com", null).Completed, task => task.Id == "b");
            Assert.Contains(Assert.Single(new PimCache(folder).ReadTasks()!.Accounts).Queue, change => change.Task == "b");
        }
    }

    /// <summary>
    /// tasks-cache.json held by another program as the store starts: it is not taken for an empty
    /// cache. A change made meanwhile waits in memory; once the file reads it is taken in, and both
    /// changes are kept. Before, the store started empty and its next write dropped the change of the
    /// run before.
    /// </summary>
    [Fact]
    public async Task ATaskChangeNotSentSurvivesTheCacheBeingHeldAtTheStart()
    {
        var todo = Todo();
        var set = new AccountSet([todo]);
        using (var first = await Read(Store(set)))
        {
            var shown = first.Show("me@outlook.com", null);
            todo.Failure = new HttpRequestException("offline");
            first.SetDone(shown.Account!, shown.List!, "b", true);
            await first.Idle();
        }

        using var second = Store(set);
        string list;
        using (Hold(TasksFile))
        {
            await Read(second);
            list = second.Show("me@outlook.com", null).List ?? "tasks";
            second.Add(todo.Account.Id, list, "Book the train");
            await second.Idle();
        }
        second.Refresh(force: true);
        await second.Idle();

        using var third = await Read(Store(set));
        var shownThird = third.Show("me@outlook.com", null);
        Assert.Contains(shownThird.Completed, task => task.Id == "b");
        Assert.Contains(shownThird.Open, task => task.Title == "Book the train");
    }

    /// <summary>
    /// tasks-cache.json cut short (a crash, a disk that never finished the write): the version before
    /// it, tasks-cache.json.bak, which every write keeps, holds the change not sent yet.
    /// </summary>
    [Fact]
    public async Task ATaskChangeNotSentSurvivesTheCacheBeingCutShort()
    {
        var todo = Todo();
        var set = new AccountSet([todo]);
        using (var first = await Read(Store(set)))
        {
            var shown = first.Show("me@outlook.com", null);
            todo.Failure = new HttpRequestException("offline");
            first.SetDone(shown.Account!, shown.List!, "b", true);
            await first.Idle();
            // Once more, so that the version before the last is one with the change in it.
            first.Refresh(force: true);
            await first.Idle();
        }
        var whole = await File.ReadAllBytesAsync(TasksFile);
        await File.WriteAllBytesAsync(TasksFile, whole[..(whole.Length / 2)]);

        using var second = await Read(Store(set));
        Assert.Contains(second.Show("me@outlook.com", null).Completed, task => task.Id == "b");
    }

    /// <summary>
    /// An island's file held by another program at its read reads as nothing for the store, which is
    /// not let write over it; one emptied by a crash is the version before it.
    /// </summary>
    [Fact]
    public void AnIslandFileHeldAtItsReadIsNotWrittenOverAndAnEmptyOneIsTheVersionBefore()
    {
        var name = "held-" + Guid.NewGuid().ToString("N") + ".json";
        var path = IslandFiles.PathOf(name);
        try
        {
            IslandFiles.Write(name, "{\"places\":[1]}");
            IslandFiles.Write(name, "{\"places\":[1,2]}");

            using (Hold(path))
            {
                Assert.Null(IslandFiles.Read(name));
            }
            IslandFiles.Write(name, "{\"places\":[]}");
            Assert.Equal("{\"places\":[1,2]}", File.ReadAllText(path));

            File.WriteAllText(path, "");
            Assert.Equal("{\"places\":[1]}", IslandFiles.Read(name));
            IslandFiles.Write(name, "{\"places\":[3]}");
            Assert.Equal("{\"places\":[3]}", IslandFiles.Read(name));
            Assert.Equal("{\"places\":[1]}", File.ReadAllText(path + ".bak"));
        }
        finally
        {
            foreach (var file in Directory.GetFiles(Path.GetDirectoryName(path)!, name + "*"))
            {
                File.Delete(file);
            }
        }
    }
}
