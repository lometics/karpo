// A widget's id is unique across every layout of a config, and every copy goes by one rule (the person:
// "i dont want ... widgets behave weirdly because they got copied"). The dock keeps a widget's own state
// by its id alone — a timer's countdown, a stopwatch, an alarm's ring — so a layout duplicated with its
// folders' widgets under their old ids ran the original's timer in the copy, heard only the first
// alarm's edits, and rang the wrong folder. Now:
//
//  - **A copy** of a widget, a folder or a layout gives new ids to everything in it (BarItem.Copy,
//    TaskbarLayout.CopyAs, BarLooks.Duplicate).
//  - **Repair** keeps an id unique across all layouts: of two, the first keeps it, the later one is
//    given a new one, so a file already in that state is healed when it is read.
//  - **A cut and a paste** are a move: the widget comes down with its own ids the first time, and its
//    countdown is carried meanwhile (Countdowns.Carry); pasted again it is a copy.

using Karpo.Dock;
using CoreFolders = Karpo.Core.Folders;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Core;

public sealed class WidgetCopiesTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc);

    /// <summary>A config whose one layout holds a folder with a timer and an alarm: the folder, the timer and the alarm.</summary>
    private static (Config Config, BarItem Folder, CellItem Timer, CellItem Alarm) Home()
    {
        var timer = ListKind.Timer.New();
        timer.Countdown = new CountdownOptions { Minutes = 5, Name = "Tea" };
        var alarm = ListKind.Alarm.New();
        alarm.Alarm = new AlarmOptions { Time = "07:00", On = true, Name = "Wake" };
        var folder = Configs.FolderKind(FolderView.List).New();
        folder.Folder!.Put(timer);
        folder.Folder.Put(alarm);
        var config = Configs.With(folder);
        config.Repair();
        return (config, folder, timer, alarm);
    }

    /// <summary>Every id of <paramref name="config"/>, each layout's and its folders', in their order.</summary>
    private static List<string> Ids(Config config) => Ids(config.AllIslands);

    /// <summary>Every id of <paramref name="list"/> and its folders, in their order.</summary>
    private static List<string> Ids(IEnumerable<BarItem> list) => [.. CoreFolders.All(list).Select(entry => entry.Island.Id)];

    private static void AllDistinct(Config config)
    {
        var ids = Ids(config);
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>The widget in the folder of <paramref name="layout"/> that is of <paramref name="kind"/>.</summary>
    private static CellItem Inside(TaskbarLayout layout, ListKind kind) =>
        layout.Islands.Single(island => island.Folder is not null).Folder!.Cells.Select(cell => cell.Island).Single(cell => cell.List == kind);

    // --- A layout duplicated -----------------------------------------------------------------------

    /// <summary>
    /// A layout duplicated: every id of both layouts distinct, its folders' widgets' too; the copy's
    /// timer stays where it was when the original's starts, and its alarm set to another time rings
    /// at its own, not the original's.
    /// </summary>
    [Fact]
    public void ADuplicatedLayoutsFolderWidgetsAreItsOwn()
    {
        var (config, _, timer, alarm) = Home();

        var copy = BarLooks.Duplicate(config, config.DefaultLayout);

        AllDistinct(config);
        Assert.Equal(2, CoreFolders.All(copy.Islands).Count(entry => entry.Island is CellItem));
        var copiedTimer = Inside(copy, ListKind.Timer);
        var copiedAlarm = Inside(copy, ListKind.Alarm);
        Assert.Equal("Tea", copiedTimer.Countdown!.Name);
        Assert.Equal("Wake", copiedAlarm.Alarm!.Name);
        // And Repair leaves it as it is: nothing to heal.
        Assert.Equal(Ids(config), Ids(Configs.RoundTrip(config, out _)));

        // The original's timer started: the copy's stays.
        var countdowns = new Countdowns();
        countdowns.Act(timer.Id, CountdownKind.Timer, TimeSpan.FromMinutes(5), Now);
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.Equal(CountdownPhase.Running, countdowns.Of(timer.Id).Phase);
        Assert.True(countdowns.Of(copiedTimer.Id).Idle);
        // And the other way round: the copy's started, then paused; the original runs on.
        countdowns.Act(copiedTimer.Id, CountdownKind.Timer, TimeSpan.FromMinutes(5), Now);
        countdowns.Act(copiedTimer.Id, CountdownKind.Timer, TimeSpan.FromMinutes(5), Now.AddMinutes(1));
        Assert.Equal(CountdownPhase.Paused, countdowns.Of(copiedTimer.Id).Phase);
        Assert.Equal(CountdownPhase.Running, countdowns.Of(timer.Id).Phase);

        // The copy's alarm set to eight: it rings at eight, the original at seven.
        var book = new AlarmBook(() => TimeZoneInfo.Utc);
        book.Sync(TheDock.AlarmsIn(config), Now);
        copiedAlarm.Alarm.Time = "08:00";
        book.Sync(TheDock.AlarmsIn(config), Now);
        Assert.Equal(Now.Date.AddHours(7), book.Of(alarm.Id, alarm.Alarm, Now).Next);
        Assert.Equal(Now.Date.AddHours(8), book.Of(copiedAlarm.Id, copiedAlarm.Alarm, Now).Next);
    }

    // --- Repair -------------------------------------------------------------------------------------

    /// <summary>
    /// A file with the same ids in two layouts — a widget on the bar and a folder's widgets, as a
    /// duplicate made before this wrote them — is healed when it is read: the first layout keeps its
    /// ids, the later one's are new, and the file is written back.
    /// </summary>
    [Fact]
    public void RepairKeepsTheFirstOfAnIdInTwoLayoutsAndRenamesTheLater()
    {
        const string Layout = """
            [ { "id": "start1", "kind": "start" },
              { "id": "fold1", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "tea1", "kind": "timer" } },
                { "island": { "id": "wake1", "kind": "alarm" } } ] ] } } ]
            """;
        var json = $$"""
            { "layouts": [
                { "id": "home", "name": "Home", "islands": {{Layout}} },
                { "id": "work", "name": "Work", "islands": {{Layout}} } ],
              "defaultLayoutId": "home" }
            """;

        var config = Configs.Repaired(json, out var changed);

        Assert.True(changed);
        AllDistinct(config);
        Assert.Equal(["start1", "fold1", "fold1/tea1", "fold1/wake1"], CoreFolders.All(config.Layout("home")!.Islands).Select(entry => entry.Path));
        var work = Ids(config.Layout("work")!.Islands);
        Assert.Equal(4, work.Count);
        Assert.DoesNotContain(work, id => id is "start1" or "fold1" or "tea1" or "wake1");
        // Healed once: read again, nothing changes.
        Assert.Equal(Ids(config), Ids(Configs.RoundTrip(config, out var again)));
        Assert.False(again);
    }

    // --- Copy, cut and paste ------------------------------------------------------------------------

    /// <summary>
    /// A folder copied in one layout and pasted into another twice: each paste a copy with new ids,
    /// its widgets' too, and every id of the config distinct.
    /// </summary>
    [Fact]
    public void AFolderCopiedAndPastedTwiceIsAllDistinct()
    {
        var (config, folder, _, _) = Home();
        var work = BarLooks.Add(config, "Work");
        var clipboard = new[] { folder.Clone() };

        var first = IslandEdits.Paste(work.Islands, clipboard, new BarSlot(BarSection.Middle, 0), cut: false, config.AllIslands);
        var second = IslandEdits.Paste(work.Islands, clipboard, new BarSlot(BarSection.Middle, 0), cut: false, config.AllIslands);

        Assert.Single(first);
        Assert.Single(second);
        AllDistinct(config);
        Assert.Equal(2, second[0].Folder!.Cells.Count);
    }

    /// <summary>
    /// A folder with a running timer cut and pasted into another layout is moved: it and its widgets
    /// keep their ids, and the timer runs on through it — carried while nothing holds it, as the cut
    /// leaves it. Pasted a second time it is a copy, all new ids and a timer of its own.
    /// </summary>
    [Fact]
    public void ACutAndAPasteMoveAFolderWithItsRunningTimer()
    {
        var (config, folder, timer, alarm) = Home();
        var work = BarLooks.Add(config, "Work");
        var countdowns = new Countdowns();
        countdowns.Act(timer.Id, CountdownKind.Timer, TimeSpan.FromMinutes(5), Now);
        var clipboard = new[] { folder.Clone() };

        // Cut: on the clipboard, out of the file; its countdown carried.
        countdowns.Carry(CoreFolders.Ids(clipboard));
        config.DefaultLayout.Islands.Remove(folder);
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.Equal(CountdownPhase.Running, countdowns.Of(timer.Id).Phase);

        // Pasted: moved, every id its own.
        var moved = Assert.Single(IslandEdits.Paste(work.Islands, clipboard, new BarSlot(BarSection.Middle, 0), cut: true, config.AllIslands));
        Assert.Equal(folder.Id, moved.Id);
        Assert.Equal([timer.Id, alarm.Id], moved.Folder!.Cells.Select(cell => cell.Island.Id));
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.Equal(CountdownPhase.Running, countdowns.Of(timer.Id).Phase);

        // Pasted again: a copy, nothing shared.
        var copy = Assert.Single(IslandEdits.Paste(work.Islands, clipboard, new BarSlot(BarSection.Middle, 0), cut: true, config.AllIslands));
        AllDistinct(config);
        Assert.NotEqual(folder.Id, copy.Id);
        var copiedTimer = copy.Folder!.Cells.Select(cell => cell.Island).Single(cell => cell.List == ListKind.Timer);
        Assert.True(countdowns.Of(copiedTimer.Id).Idle);

        // Something else on the clipboard: the cut is carried no more, and what no layout has goes.
        countdowns.Carry([]);
        work.Islands.Remove(moved);
        countdowns.Keep(Countdowns.KeysIn(config));
        Assert.True(countdowns.Of(timer.Id).Idle);
    }

    /// <summary>A folder's line cut and pasted into a folder of another layout keeps its id; pasted again, a new one.</summary>
    [Fact]
    public void ACutLineIsMovedOnceAndCopiedAfter()
    {
        var (config, folder, timer, _) = Home();
        var work = BarLooks.Add(config, "Work");
        var other = Configs.FolderKind(FolderView.List).New();
        work.Islands.Add(other);
        var line = timer.Clone();
        CoreFolders.Remove(config.DefaultLayout.Islands, CoreFolders.Path(folder.Id, timer.Id));
        var into = Config.IslandKey(Config.LayoutOwner(work.Id), other.Id);

        var moved = TheDock.PasteLineIn(config, into, line, cut: true);
        var copied = TheDock.PasteLineIn(config, into, line, cut: true);

        Assert.Equal(Config.IslandKey(Config.LayoutOwner(work.Id), CoreFolders.Path(other.Id, timer.Id)), moved);
        Assert.NotNull(copied);
        Assert.NotEqual(moved, copied);
        AllDistinct(config);
    }
}
