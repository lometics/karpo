// The Windows Update widget, taken out (2026-10-08): its restart and its dot already show in the status
// icons (§21.3), which say more — updates waiting, downloading and installing too.
//
// What is held here: no registry, no library page and no shared record has the widget any more, and
// the status icons keep everything it said; and a file that still holds one — on the bar or as a
// list's line — loads without it, quietly: it is a word no registry has, which a file is
// read past (TolerantEnumConverter, Config.Repair), and everything around it stays as it was.

namespace Karpo.Tests.Core;

public sealed class WindowsUpdateRemovedTests
{
    /// <summary>No widget of any view, no record and no page of the library has it.</summary>
    [Fact]
    public void NoRegistryOrLibraryHasIt()
    {
        Assert.Null(BarEntries.Find("windowsUpdate"));
        Assert.Null(ListWidgets.Find("windowsUpdate"));
        Assert.Null(WidgetInfos.Find("windowsUpdate"));
        Assert.DoesNotContain(WidgetInfos.All, info => info.Id == "windowsUpdate");
        foreach (var page in Catalogue.Pages)
        {
            Assert.DoesNotContain(Catalogue.On(page), entry => entry.Id == "windowsUpdate");
        }
        Assert.DoesNotContain("WindowsUpdate", Enum.GetNames<BarKind>());
        Assert.DoesNotContain("WindowsUpdate", Enum.GetNames<ListKind>());
        Assert.DoesNotContain("WindowsUpdate", Enum.GetNames<WidgetSource>());
    }

    /// <summary>The status icons are still where it can be added from, found by "update" and "windows update".</summary>
    [Fact]
    public void TheStatusIconsStillSayIt()
    {
        Assert.NotNull(BarEntries.Find("indicators"));
        Assert.Contains(WidgetInfos.Indicators.Keywords, word => word.Contains("windows update", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A bar's Windows Update is gone, its neighbours in their places, and the file reads the same after.</summary>
    [Fact]
    public void ABarsWindowsUpdateIsDroppedQuietly()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "start", "kind": "start" },
              { "id": "update", "kind": "windowsUpdate", "label": true },
              { "id": "status", "kind": "indicators" },
              { "id": "time", "kind": "touchKeyboard" } ] } ] }
            """);

        var list = Configs.Islands(config);
        Assert.Equal([BarKind.Start, BarKind.Indicators, BarKind.TouchKeyboard], Configs.Kinds(list));
        Assert.Equal(["start", "status", "time"], list.Select(island => island.Id));
        Assert.DoesNotContain("windowsUpdate", config.ToJson(), StringComparison.Ordinal);
        Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
    }

    /// <summary>A list folder's line of it is gone; the folder and its other widgets stay.</summary>
    [Fact]
    public void AFoldersWindowsUpdateIsDroppedQuietly()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [
                { "island": { "id": "v", "kind": "windowsUpdate" } },
                { "island": { "id": "h", "kind": "cpu" } } ] ] } } ] } ] }
            """);

        var islands = config.Layouts[0].Islands;
        Assert.Equal(["f"], islands.Select(island => island.Id));
        var list = islands.Single(island => island.Id == "f").Folder!.Cells;
        Assert.Equal(["h"], list.Select(cell => cell.Island.Id));
        Assert.Equal(ListKind.Cpu, list[0].Island.List);

        Assert.DoesNotContain("windowsUpdate", config.ToJson(), StringComparison.Ordinal);
        Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
    }
}
