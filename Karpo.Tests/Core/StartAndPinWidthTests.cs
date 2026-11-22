// No width for the Start button and pins, anywhere (the person, 2026-10-08: "no configurable width
// for the Start button and pins"): not on the taskbar, where no entry has one (the bar's entries have no height rows in their menus, Karpo.Dock's LineHeightRows), and
// not in a folder either. Start stands in no folder, and a file that still gives it a width has it
// dropped where it is read. A pin's line down a list keeps its height, which is a height and not a
// width (ListWidgets: one row to six, more of it shown at two).

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class StartAndPinWidthTests
{
    /// <summary>Neither has a size to set on the taskbar, and neither keeps one a file still gives it.</summary>
    [Theory]
    [InlineData(BarKind.Start)]
    [InlineData(BarKind.Pin)]
    public void NoWidthOnTheTaskbar(BarKind kind)
    {
        var entry = kind.New();
        entry.Height = LineHeight.Exactly(6);

        entry.Normalize();
        Assert.Null(entry.Height);
    }

    /// <summary>Start has no line in a list, so no folder can give it a width.</summary>
    [Fact]
    public void StartStandsInNoFolder()
    {
        Assert.Null(ListWidgets.Find(BarKind.Start.Id()));
    }

    /// <summary>
    /// Start read from a file that still gives it a width loses it, quietly; a pin's line in a list
    /// keeps its size (§432), and the file is written with it again.
    /// </summary>
    [Fact]
    public void AWidthAFileGivesStartIsDroppedAndAPinsLineKeepsItsSize()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "start", "kind": "start", "height": { "fixed": true, "cells": 4 } },
              { "id": "l", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "q", "kind": "pin", "size": "full", "pin": { "target": "C:\\Windows\\notepad.exe" } } } ] ] } } ] } ] }
            """);

        var islands = config.Layouts[0].Islands;
        var start = islands.Single(island => island.Id == "start");
        var line = Assert.Single(islands.Single(island => island.Id == "l").Folder!.Cells).Island;
        Assert.Null(start.Height);
        // The list's line: its size, as the file said it, and no height.
        Assert.Equal((ListKind?)ListKind.Pin, line.List);
        Assert.Equal(((LineSize?)LineSize.Full, (LineHeight?)null), (line.Size, line.Height));
        Assert.Equal(LineSize.Full, Karpo.Dock.LineHeightRows.SizeOf(line));

        var again = Configs.RoundTrip(config, out _);
        var lineAgain = Assert.Single(again.Layouts[0].Islands.Single(island => island.Id == "l").Folder!.Cells).Island;
        Assert.Equal((LineSize?)LineSize.Full, lineAgain.Size);
    }

    /// <summary>
    /// A pin's line that a file gives no size starts at its kind's, Compact (§432), not at a width it
    /// would have on the bar.
    /// </summary>
    [Fact]
    public void APinsLineStartsAtItsKindsSize()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "l", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "q", "kind": "pin", "pin": { "target": "C:\\Windows\\notepad.exe" } } } ] ] } } ] } ] }
            """);

        var line = Assert.Single(config.Layouts[0].Islands.Single(island => island.Id == "l").Folder!.Cells).Island;
        Assert.Equal(((LineSize?)LineSize.Compact, (LineHeight?)null), (line.Size, line.Height));
    }
}
