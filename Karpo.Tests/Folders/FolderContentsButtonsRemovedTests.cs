// §492: a folder's contents on the taskbar, taken out (2026-11-03, the person: the folder contents taskbar widget
// is "a remnant that is actually out of date. remove it"): the row of buttons a bar showed for a folder's items, and
// the list a subfolder's button opened from the bar (PinFolderFlyout), are gone. A folder's contents are a line of a
// Karpo folder's list alone, and a pin to a folder still opens it in File Explorer.
//
// What is held here: no taskbar registry or add list has the entry, its "Show titles" went with it, and a bar a
// build with one wrote reads without it — dropped as a word this build does not know, its neighbours where they
// were — while the same word on a line of a folder's list still reads as the line.

using Karpo.Tests.Core;

namespace Karpo.Tests.Folders;

public sealed class FolderContentsButtonsRemovedTests
{
    /// <summary>No taskbar entry, no bar's add list and no language has the bar's folder contents or their titles; the line stays.</summary>
    [Fact]
    public void TheBarHasNoFolderContents()
    {
        Assert.Null(BarEntries.Find("folderContents"));
        Assert.DoesNotContain(Enum.GetNames<BarKind>(), name => name == "FolderContents");
        Assert.DoesNotContain(Catalogue.On(WidgetType.Bar), entry => entry.Id == "folderContents");
        Assert.NotNull(ListWidgets.Find("folderContents"));
        Assert.Contains(Catalogue.On(WidgetType.List), entry => entry.Id == "folderContents");
        Assert.Null(typeof(PinOptions).GetProperty("ShowTitles"));
        Assert.DoesNotContain(new[] { "en", "de", "es", "fr", "zh-Hans" }, language =>
            Strings.Lookup("settings.pin.titles.header", language) is not null);
    }

    /// <summary>
    /// A bar holding a folder's contents reads without them: the entries either side stay in order, the file says it
    /// changed so it is written back once, and a folder's contents line in a Karpo folder on the same bar stays.
    /// </summary>
    [Fact]
    public void ABarsFolderContentsAreDroppedAndAFoldersLineStays()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "start", "kind": "start" },
              { "id": "docs", "kind": "folderContents", "pin": { "target": "C:\\Users", "showTitles": true, "navigation": "contents" } },
              { "id": "apps", "kind": "apps" },
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "line", "kind": "folderContents", "pin": { "target": "C:\\Users", "navigation": "contents" } } } ] ] } } ] } ] }
            """, out var changed);

        Assert.True(changed, "a file with the bar's folder contents in it would not be written back");
        Assert.Equal([BarKind.Start, BarKind.Apps, BarKind.FolderList], Configs.Kinds(Configs.Islands(config)));
        var line = Assert.Single(Karpo.Core.Folders.All(config.DefaultLayout.Islands), each => each.Island.Info == WidgetInfos.FolderContents).Island;
        Assert.Equal((@"C:\Users", PinFolderNavigation.Contents), (line.Pin!.Target, line.Pin.Navigation));
        Assert.DoesNotContain("showTitles", config.ToJson(), StringComparison.Ordinal);
        var again = Configs.RoundTrip(config, out var twice);
        Assert.False(twice, "today's words would change again");
        Assert.Equal([BarKind.Start, BarKind.Apps, BarKind.FolderList], Configs.Kinds(Configs.Islands(again)));
    }
}
