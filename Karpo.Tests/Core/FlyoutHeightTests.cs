// How tall a widget's flyout kept its list (§53, §59) went with the last flyout that had a list to
// hold to a height: the keyboard's layouts and VPN's connections are folders' lines since 2026-10-11,
// and a line is as tall as what it lists. A file that still says a height, or the count of rows an
// older one kept, loads whole and says it no more once written.

namespace Karpo.Tests.Core;

public sealed class FlyoutHeightTests
{
    /// <summary>
    /// A file with a flyout height on an entry and on a line, and an old count of rows: everything in it
    /// reads as it was, and none of the three words is written again.
    /// </summary>
    [Fact]
    public void AFileWithAFlyoutHeightLoadsWholeAndForgetsIt()
    {
        var config = Configs.Repaired("""
            { "version": 38, "layouts": [ { "id": "L", "islands": [
              { "id": "s", "kind": "start", "section": "middle", "flyoutHeight": "medium" },
              { "id": "w", "kind": "taskView", "section": "middle", "flyoutHeight": 333, "flyoutRows": 5 },
              { "id": "f", "kind": "folderList", "section": "end", "folder": { "lists": [ [
                { "island": { "id": "c", "kind": "clipboard", "flyoutHeight": "small", "flyoutRows": { "min": 3, "max": 8 } } } ] ] } } ] } ] }
            """);

        var islands = config.Layouts.Single().Islands;
        Assert.Equal(["s", "w", "f"], islands.Select(island => island.Id));
        Assert.Equal([BarKind.Start, BarKind.TaskView, BarKind.FolderList], islands.Select(island => island.Kind));
        Assert.Equal("c", Assert.Single(islands[2].Folder!.Cells).Island.Id);
        var json = config.ToJson();
        Assert.DoesNotContain("flyoutHeight", json, StringComparison.Ordinal);
        Assert.DoesNotContain("flyoutRows", json, StringComparison.Ordinal);
    }
}
