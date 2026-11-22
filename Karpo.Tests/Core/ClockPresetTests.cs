// The clock is a preset (2026-10-09, branch folder-faces: "the clock taskbar widget is actually just
// a premade widget that is actually a list folder with a face and correct widgets inside the folder";
// "basically the same way that the current clock/date widget is structured, but all made with
// widgets"; "nothing special"). What the library's "Clock" adds is an ordinary list folder wearing the
// clock face, holding the clock flyout's parts as widgets, top to bottom, as the person arranged it
// (2026-10-11) — and after that it is a folder like any other: lines added, taken out and moved, its
// face changed, all of it read back as written. There is no clock taskbar entry any more, and a file
// that has one reads without it. Plain code: no window.

namespace Karpo.Tests.Core;

using Folders = Karpo.Core.Folders;

public sealed class ClockPresetTests
{
    private static Preset Clock => Presets.Find(Presets.ClockId)!;

    private static readonly PresetMachine Machine = new(HasBattery: false, FixedDrives: []);

    /// <summary>
    /// The clock's lines, in order, one card: the time over its date, the timer, the stopwatch, the
    /// focus session, the footer — the person's own arrangement (2026-10-11).
    /// </summary>
    private static readonly string[] Lines = ["timeHeader", "timer", "stopwatch", "focus", "actions"];

    /// <summary>
    /// Added at a "+" of the bar, the clock is one list folder: the clock face showing the date, its
    /// name and its glyph, and its lines in order — the time and the date, the timer, the stopwatch,
    /// the focus session, and the footer's actions: Date &amp; time as the link, Focus as the button.
    /// </summary>
    [Fact]
    public void TheClockAddsAListFolderWithTheClockFaceAndItsLines()
    {
        List<BarItem> bar = [BarKind.Start.New(), BarKind.ShowDesktop.New()];
        bar[1].Section = BarSection.End;

        var id = Presets.AddToBar(bar, new BarSlot(BarSection.End, 0), Clock, Machine);

        var folder = Assert.Single(bar, island => island.Id == id);
        Assert.Equal(BarKind.FolderList, folder.Kind);
        Assert.Equal(BarKind.ShowDesktop, bar[bar.IndexOf(folder) + 1].Kind);
        var options = folder.Folder!;
        Assert.Equal(FolderFaces.ClockId, options.Face.Kind);
        var face = ClockFace.Read(options.Face);
        Assert.Empty(options.Face.Options);
        Assert.False(face.Seconds);
        Assert.Equal(ClockFace.Region, face.Hours);
        Assert.Equal(Lines, Assert.Single(options.Lists).Select(cell => cell.Island.Kind));
        Assert.Equal(Lines, Folders.ReadingOrder(options).Select(cell => cell.Island.Kind));
        Assert.All(options.Cells, cell => Assert.Equal(FolderView.List, cell.Island.View));
        var actions = options.Cells.Single(cell => cell.Island.List == ListKind.Actions).Island.Actions!;
        Assert.Equal(("dateTime", "focus"), (actions.Left, actions.Right));
    }

    /// <summary>
    /// The clock is the person's own (2026-10-11: "the clock layout i already changed on the given
    /// list, get it from there"): the Clock folder of their "All ready-made folders" layout, as their
    /// config.json had it, is what the preset makes — the same lines in the same order, each at the
    /// height it had (where a new line of its kind starts, Automatic up to six rows), the same
    /// footer, no divider, no month and no agenda (the calendar's).
    /// </summary>
    [Fact]
    public void TheClockIsThePersonsArrangement()
    {
        var theirs = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "f", "kind": "folderList", "folder": {
                "face": { "kind": "clock", "options": {} }, "lists": [ [
                { "island": { "id": "a", "kind": "timeHeader", "height": { "fixed": false, "cells": 12 } } },
                { "island": { "id": "b", "kind": "timer", "height": { "fixed": false, "cells": 12 } } },
                { "island": { "id": "c", "kind": "stopwatch", "height": { "fixed": true, "cells": 2 } } },
                { "island": { "id": "d", "kind": "focus", "height": { "fixed": false, "cells": 12 } } },
                { "island": { "id": "e", "kind": "actions", "actions": { "left": "dateTime", "right": "focus" } } } ] ] } } ] } ] }
            """, out _);
        var person = Configs.Islands(theirs).Single().Folder!;

        var made = Presets.Folder(Clock, Machine).Folder!;

        static IEnumerable<(string, LineHeight, string?, string?)> Shape(FolderOptions folder) =>
            Folders.ReadingOrder(folder).Select(cell => (cell.Island.Kind, LineHeight.For(cell.Island), cell.Island.Actions?.Left, cell.Island.Actions?.Right));
        Assert.Equal(Shape(person), Shape(made));
        Assert.Equal(person.Face.Kind, made.Face.Kind);
        Assert.Equal(Lines, Shape(made).Select(line => line.Item1));
        // Their automatic heights read as fixed: none of the three has content that varies (§138); the
        // stopwatch, whose laps do (§388), is their one row, as their file has it.
        Assert.All(Shape(made).Take(4).Where(line => line.Item1 != "stopwatch"), line => Assert.Equal(LineHeight.Automatic(12).Within(ListWidgets.Find(line.Item1)!.Kind), line.Item2));
        Assert.Equal(LineHeight.Exactly(2), Shape(made).Single(line => line.Item1 == "stopwatch").Item2);
        Assert.DoesNotContain(Clock.Items, item => item.IsDivider || item.Form is "month" or "calendar" or "events");
    }

    /// <summary>Two clocks are two folders: their faces and their widgets their own, nothing shared.</summary>
    [Fact]
    public void EachClockIsItsOwn()
    {
        var one = Presets.Folder(Clock, Machine);
        var two = Presets.Folder(Clock, Machine);

        Assert.NotSame(one.Folder!.Face, two.Folder!.Face);
        one.Folder.Face.Set(FolderFaces.Clock.Option(ClockFace.Seconds)!, true);
        Assert.False(ClockFace.Read(two.Folder.Face).Seconds);
        Assert.False(ClockFace.Read(Clock.Face!).Seconds);
        Assert.Empty(one.Folder.Cells.Select(cell => cell.Island.Id).Intersect(two.Folder.Cells.Select(cell => cell.Island.Id)));
    }

    /// <summary>
    /// After it is added it is nothing special: a line added, one taken out, one moved to the top and
    /// its face changed to the calendar's page — the file written and read back has all of it, as it
    /// would any folder's.
    /// </summary>
    [Fact]
    public void AfterwardsItIsAnOrdinaryFolder()
    {
        var config = Configs.With(BarKind.Start.New());
        var list = config.DefaultLayout.Islands;
        var id = Presets.AddToBar(list, new BarSlot(BarSection.End, 0), Clock, Machine);
        var folder = list.Single(island => island.Id == id);
        var options = folder.Folder!;

        // A row of toggles added, the timer taken out, the actions moved to the top.
        Assert.NotNull(Folders.AddNew(list, folder, "toggles"));
        var timer = options.Cells.Single(cell => cell.Island.List == ListKind.Timer).Island;
        Assert.Same(timer, Folders.Remove(list, Folders.Path(folder.Id, timer.Id)));
        var actions = options.Cells.Single(cell => cell.Island.List == ListKind.Actions);
        Assert.NotNull(Folders.Take(options, actions));
        Assert.NotNull(Folders.InsertLine(BarKind.FolderList, options, actions.Island, new FolderPlace(0, 0)));
        // Another face, with options of its own.
        options.Face = FolderFace.Of(FolderFaces.CalendarId);
        options.Face.Set(FolderFaces.Calendar.Option(CalendarFace.Look)!, CalendarFace.Page);

        var read = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var again = read.DefaultLayout.Islands.Single(island => island.Id == id).Folder!;
        Assert.Equal(
            ["actions", "timeHeader", "stopwatch", "focus", "toggles"],
            Folders.ReadingOrder(again).Select(cell => cell.Island.Kind));
        Assert.Equal(FolderFaces.CalendarId, again.Face.Kind);
        Assert.True(CalendarFace.IsPage(again.Face));
    }

    /// <summary>
    /// The clock taskbar entry is gone: no kind of the bar is "clock", nothing on the bar's add list
    /// is the clock widget, and a file that has one reads without it (the clock's line in a
    /// folder stays). The library's "Clock" is the preset.
    /// </summary>
    [Fact]
    public void ThereIsNoClockEntryAnyMore()
    {
        Assert.DoesNotContain(BarEntries.All, entry => entry.Info == WidgetInfos.Clock);
        Assert.Null(BarEntries.Find(Configs.Kind("clock")));
        Assert.DoesNotContain(Catalogue.On(WidgetType.Bar), entry => entry.Id == WidgetInfos.Clock.Id);
        Assert.Contains(Presets.On(WidgetType.Bar), preset => preset.Id == Presets.ClockId);

        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "s", "kind": "start" },
              { "id": "c", "kind": "clock", "section": "end", "clock": { "timeZone": "Tokyo Standard Time" } },
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "l", "kind": "clock" } } ] ] } } ] } ] }
            """, out var changed);

        Assert.True(changed);
        Assert.Equal(["s", "f"], Configs.Islands(config).Select(island => island.Id));
        Assert.Equal(ListKind.Clock, Assert.Single(Configs.Islands(config)[1].Folder!.Cells).Island.List);
    }
}
