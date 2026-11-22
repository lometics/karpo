// The grid folder and every grid widget are gone (2026-10-09). The person: "the grid folders and
// grid widgets ... should be deleted completely". What is held here is that nothing of the grid is
// left to come back by accident: no type of it in any of Karpo's assemblies, no member of an enum
// that named it, no page of the library, no word of it in any language — and a file that still has
// a grid folder in it, in this shape, loads with the folder dropped and everything else kept.

using System.Reflection;
using Karpo.Dock;

namespace Karpo.Tests.Core;

public sealed class GridRemovedTests
{
    /// <summary>Karpo's own assemblies: the model and the dock.</summary>
    private static readonly Assembly[] Karpos =
    [
        typeof(Config).Assembly,
        typeof(FolderFlyout).Assembly,
    ];

    /// <summary>
    /// No type of the grid in any assembly: its registry, its kinds, its cards and the settings
    /// app's span card and its arithmetic.
    /// </summary>
    [Theory]
    [InlineData("GridKind")]
    [InlineData("GridWidget")]
    [InlineData("GridWidgets")]
    [InlineData("GridCard")]
    [InlineData("SliderCard")]
    [InlineData("SoundOutputCard")]
    [InlineData("CardFrame")]
    [InlineData("SpanChoice")]
    [InlineData("SpanAxis")]
    [InlineData("SpanCard")]
    [InlineData("FolderMoves")]
    [InlineData("FolderResize")]
    [InlineData("DragTarget")]
    public void NoTypeOfTheGridIsLeft(string name)
    {
        foreach (var assembly in Karpos)
        {
            Assert.DoesNotContain(assembly.GetTypes(), type => type.Name == name);
        }
    }

    /// <summary>No enum names a grid any more: the folder kinds, the views, the types, the surfaces.</summary>
    [Fact]
    public void NoEnumNamesTheGrid()
    {
        Assert.Equal([FolderView.List], Enum.GetValues<FolderView>());
        Assert.Equal([WidgetType.Bar, WidgetType.List], Enum.GetValues<WidgetType>());
        Assert.Equal([WidgetSurface.List], Enum.GetValues<WidgetSurface>());
        Assert.False(Enum.TryParse<BarKind>("Folder", out _));
        Assert.Equal([BarKind.FolderList], Enum.GetValues<BarKind>().Where(kind => kind.IsFolder()));
        Assert.Null(BarEntries.Find("folder"));
        Assert.Null(WidgetInfos.Find("folder"));
    }

    /// <summary>A folder's widget is a list's line: there is no grid member on it to set.</summary>
    [Fact]
    public void AFoldersWidgetHasNoGridForm()
    {
        Assert.Null(typeof(CellItem).GetProperty("Grid"));
        Assert.Null(typeof(FolderOptions).GetProperty("ShowTitles"));
        Assert.Null(typeof(CatalogueEntry).GetProperty("Grid"));
    }

    /// <summary>The library has two pages, the taskbar's and the list's, and no row of either is a grid's.</summary>
    [Fact]
    public void TheLibraryHasNoGridPage()
    {
        Assert.Equal([WidgetType.Bar, WidgetType.List], Catalogue.Pages);
        Assert.All(Catalogue.Pages.SelectMany(Catalogue.On), entry => Assert.NotEqual("folder", entry.Id));
    }

    /// <summary>Every preset is a list folder now: the two that were grids went with them.</summary>
    [Fact]
    public void EveryPresetIsAListFolder()
    {
        Assert.All(Presets.All, preset => Assert.Equal(BarKind.FolderList, preset.Folder));
        Assert.Null(Presets.Find("quickSettingsGroup"));
        Assert.Null(Presets.Find("widgetsGroup"));
    }

    /// <summary>
    /// A file in this shape that still has a grid folder — written by hand, or by a build between
    /// the two — loads with that folder dropped as a kind this build does not know, and everything
    /// else where it was.
    /// </summary>
    [Fact]
    public void AGridFolderInAFileIsDroppedAndTheRestStays()
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "c", "kind": "start" },
              { "id": "g", "kind": "folder", "folder": { "lists": [ [ { "island": { "id": "w", "kind": "stock" } } ] ] } },
              { "id": "l", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "p", "kind": "pin" } } ] ] } } ] } ] }
            """, out var changed);

        Assert.True(changed);
        Assert.Equal(["c", "l"], Configs.Islands(config).Select(island => island.Id));
        Assert.Equal(["p"], Configs.Islands(config)[1].Folder!.Cells.Select(cell => cell.Island.Id));
    }

    /// <summary>No language says a grid's words any more: the strings went with what said them.</summary>
    [Fact]
    public void NoLanguageKeepsTheGridsWords()
    {
        foreach (var language in Karpo.Tests.Localization.StringFiles.Codes)
        {
            var keys = Karpo.Tests.Localization.StringFiles.Map(language).Keys;
            Assert.DoesNotContain(keys, key => key.Contains(".grid", StringComparison.Ordinal));
            Assert.DoesNotContain("core.widget.folder.title", keys);
            Assert.DoesNotContain("dock.menu.ungroup", keys);
            Assert.DoesNotContain("settings.folder.titles.header", keys);
        }
    }
}
