// The widgets the person took out on 2026-09-26, and where they are still found.
//
// "widgets to remove from grid display: virtual desktop, taskbar layout, keyboard layout, date",
// "widgets to remove from list display: day switcher, date", "weather hours, weather days, screen
// time, screen time graph, screen time app list, should be removed as widgets" and "notifications
// bell should not be shown in grid view". Each is gone from the registry of every view it left —
// the desktop and layout switchers, the keyboard layout and the bell stay on the taskbar, and no
// list has them — and a file that still names one there loses it where the file is read, and says
// so, so the file is written back once and never again. The date stayed on the taskbar until it
// joined the calendar (2026-09-27): no registry has its word now, and the calendar is what shows
// the date. The grid folders those words were first taken out of are gone themselves (2026-10-09).

namespace Karpo.Tests.Core;

public sealed class RemovedWidgetsTests
{
    /// <summary>No widget at all any more: no registry and no shared record has them.</summary>
    [Theory]
    [InlineData("screenTimeDays")]
    [InlineData("screenTimeGraph")]
    [InlineData("screenTimeList")]
    // calendar: the date is the calendar's taskbar entry now (OneCalendarTests).
    [InlineData("date")]
    public void GoneFromEveryRegistry(string id)
    {
        Assert.Null(BarEntries.Find(id));
        Assert.Null(ListWidgets.Find(id));
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == id);
    }

    /// <summary>
    /// The date's word is no widget's, and the calendar is no taskbar entry either (2026-10-09): the
    /// date on the bar is a folder's calendar face, and the calendar's agenda a folder's line.
    /// </summary>
    [Fact]
    public void TheDateIsAFolderFaceAndTheCalendarALine()
    {
        Assert.Null(BarEntries.Find("date"));
        Assert.Null(ListWidgets.Find("date"));
        Assert.Null(BarEntries.Find("calendar"));
        Assert.NotNull(ListWidgets.Find("calendar"));
        Assert.NotNull(FolderFaces.Find(FolderFaces.CalendarId));
    }

    /// <summary>
    /// A list folder in a file that still holds what left lists loses it where the file is read,
    /// and the file says it changed; what a list does take stays where it was.
    /// </summary>
    [Fact]
    public void AListFolderInAFileLosesWhatLeftLists()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "a", "kind": "screenTimeDays" } },
                { "island": { "id": "b", "kind": "date" } },
                { "island": { "id": "e", "kind": "screenTimeGraph" } },
                { "island": { "id": "g", "kind": "screenTimeList" } },
                { "island": { "id": "h", "kind": "cpu" } } ] ] } } ] } ] }
            """, out var changed);

        Assert.True(changed, "a list folder lost its widgets and the file would not be written back");
        var cells = config.Layouts[0].Islands.Single(island => island.Id == "f").Folder!.Cells;
        Assert.Equal(["h"], cells.Select(cell => cell.Island.Id));
        Assert.Equal(ListKind.Cpu, cells[0].Island.List);
        Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
    }

    /// <summary>
    /// A bar in a file that still holds screen time loses it where the file is read, and the file
    /// says it changed; the idle time an older file kept for it is read past and not written back.
    /// The files screen time kept its days in are not the config's, and nothing here reads, moves
    /// or deletes them.
    /// </summary>
    [Fact]
    public void ABarInAFileLosesItsScreenTime()
    {
        var config = Configs.Repaired("""
            { "screenTime": { "idleMinutes": 10 }, "layouts": [ { "id": "L", "islands": [
              { "id": "s", "kind": "screenTime" },
              { "id": "c", "kind": "touchKeyboard" } ] } ] }
            """, out var changed);

        Assert.True(changed, "the bar lost its screen time and the file would not be written back");
        var islands = config.Layouts[0].Islands;
        Assert.DoesNotContain(islands, island => island.Id == "s");
        Assert.Contains(islands, island => island.Id == "c" && island.Kind == BarKind.TouchKeyboard);
        Assert.DoesNotContain("screenTime", config.ToJson(), StringComparison.Ordinal);
        Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
    }
}
