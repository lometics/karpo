// Deleting a folder (the person, 2026-10-11): nothing lost, nothing asked; else Windows' own dialog
// says how many widgets go, on its delete button too (Karpo.Core\FolderDeletes.cs). One widget removed
// never asks, several do (§253). The dialog itself
// is the dock's TaskDialogs, behind IDeleteAsk; what it asks is decided here. A column has no delete
// any more (2026-10-12).

using Karpo.Core;
using Karpo.Tests.Core;
using Karpo.Tests.Localization;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderDeleteTests
{
    private static readonly string Owner = Config.LayoutOwner(TaskbarLayouts.DefaultId);

    /// <summary>A folder <paramref name="id"/>, its lists of <paramref name="lists"/>.</summary>
    private static BarItem Folder(string id, params ListKind[][] lists)
    {
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder.Lists = [];
        for (var list = 0; list < lists.Length; list++)
        {
            island.Folder.Lists.Add([]);
            foreach (var kind in lists[list])
            {
                island.Folder.Put(kind.New(), list);
            }
        }
        return island;
    }

    /// <summary>Every line counts as a widget but a divider: a heading and an actions footer count.</summary>
    [Fact]
    public void EveryLineButADividerIsAWidget()
    {
        var folder = Folder("f", [ListKind.Cpu, ListKind.Divider, ListKind.Heading], [ListKind.Divider, ListKind.Actions], []).Folder!;
        Assert.Equal(3, FolderDeletes.Widgets(folder));
        Assert.Equal(2, FolderDeletes.Widgets(folder.Lists[0]));
        Assert.Equal(1, FolderDeletes.Widgets(folder.Lists[1]));
        Assert.Equal(0, FolderDeletes.Widgets(folder.Lists[2]));
        Assert.Equal(0, FolderDeletes.Widgets((FolderOptions?)null));
    }

    /// <summary>An empty folder, one of dividers alone, and a widget that is no folder go without a question.</summary>
    [Fact]
    public void NothingLostNothingAsked()
    {
        Assert.Null(FolderDeletes.ForRemoving([Folder("a", [])]));
        Assert.Null(FolderDeletes.ForRemoving([Folder("b", [ListKind.Divider], [])]));
        Assert.Null(FolderDeletes.ForRemoving([BarKind.Start.New(), null]));
        Assert.Null(FolderDeletes.ForRemoving([]));
    }

    /// <summary>One folder is counted, as several are (§427: it has no name), the widgets in it too, on the delete button as well; Cancel is Windows' word.</summary>
    [Fact]
    public void OneFolderIsCountedAndItsWidgetsToo()
    {
        var sound = Folder("sound", [ListKind.Cpu, ListKind.Divider, ListKind.Gpu], [ListKind.Memory]);
        var question = FolderDeletes.ForRemoving([sound, BarKind.Start.New()]);
        Assert.NotNull(question);
        Assert.Equal(3, question.Widgets);
        Assert.Equal("Delete 1 folder?", question.Title);
        Assert.Equal("The 3 widgets inside it will be removed too.", question.Content);
        Assert.Equal("Delete 3 widgets", question.Delete);
        Assert.Equal("Cancel", question.Cancel);

        var one = FolderDeletes.ForRemoving([Folder("one", [ListKind.Cpu])])!;
        Assert.Equal("The 1 widget inside it will be removed too.", one.Content);
        Assert.Equal("Delete 1 widget", one.Delete);
    }

    /// <summary>Several folders are counted, and what they hold together; an empty one among them is counted, not named.</summary>
    [Fact]
    public void SeveralFoldersAreCounted()
    {
        var question = FolderDeletes.ForRemoving([Folder("a", [ListKind.Cpu]), Folder("b", [ListKind.Gpu, ListKind.Memory]), Folder("c", [])])!;
        Assert.Equal("Delete 3 folders?", question.Title);
        Assert.Equal("The 3 widgets inside them will be removed too.", question.Content);
        Assert.Equal("Delete 3 widgets", question.Delete);
    }

    /// <summary>The dock reads the keys it would remove: a folder on the bar asks, a line in a folder never does.</summary>
    [Fact]
    public void TheDockAsksForAFolderKey()
    {
        var config = Configs.With(Folder("sound", [ListKind.Cpu, ListKind.Gpu]), Folder("empty", []));
        Assert.Equal(2, TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "sound")])!.Widgets);
        Assert.Null(TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "empty")]));
        var line = config.ListFor(Owner)[0].Folder!.Cells[0].Island.Id;
        Assert.Null(TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, Karpo.Core.Folders.Path("sound", line))]));
        Assert.Null(TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "gone")]));
    }

    /// <summary>
    /// §253, the person: "if i just remove one widget there should not be a confirmation dialog, only
    /// when there are multiple ones". The menu's "Remove" and Delete ask the same now: one widget, on
    /// the bar or a folder's line, and an empty folder go without a question; several are counted; a
    /// folder with widgets in it still asks, alone or among others; nothing to remove asks nothing.
    /// </summary>
    [Fact]
    public void OneWidgetGoesWithoutAQuestionSeveralAreCounted()
    {
        var show = BarKind.ShowDesktop.New();
        show.Id = "show";
        var config = Configs.With(Folder("sound", [ListKind.Cpu, ListKind.Gpu]), Folder("empty", []), show);
        var line = config.ListFor(Owner)[0].Folder!.Cells[0].Island.Id;
        var lineKey = Config.IslandKey(Owner, Karpo.Core.Folders.Path("sound", line));

        Assert.Null(TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "show")]));
        Assert.Null(TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "empty")]));
        Assert.Null(TheDock.DeleteQuestionFor(config, [lineKey]));
        Assert.Null(TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "gone"), Config.IslandKey(Owner, "show")]));

        var two = TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "show"), Config.IslandKey(Owner, "empty")])!;
        Assert.Equal("Remove 2 widgets?", two.Title);
        Assert.Equal("Remove", two.Delete);
        Assert.Equal("Cancel", two.Cancel);
        Assert.Equal(2, two.Widgets);
        Assert.Equal("Remove 2 widgets?", TheDock.DeleteQuestionFor(config, [lineKey, Config.IslandKey(Owner, "show")])!.Title);
        // A folder losing widgets says what it loses, alone or among others.
        Assert.Equal("Delete 1 folder?", TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "sound")])!.Title);
        Assert.Equal("Delete 1 folder?", TheDock.DeleteQuestionFor(config, [Config.IslandKey(Owner, "sound"), Config.IslandKey(Owner, "show")])!.Title);

        // One way to ask: the menu's always-asking question is gone.
        var repo = RepoSource.Root();
        Assert.DoesNotContain("fromMenu", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditSelect.cs")));
        Assert.DoesNotContain("ForMenu", File.ReadAllText(Path.Combine(repo, "Karpo.Core", "FolderDeletes.cs")));
    }
}

/// <summary>The button's count in every language: one widget and several, each language's plural.</summary>
[Collection(InLanguage.Collection)]
public sealed class FolderDeleteLanguageTests
{
    [Theory]
    [InlineData("en", "Delete 1 widget", "Delete 6 widgets")]
    [InlineData("de", "1 Widget löschen", "6 Widgets löschen")]
    [InlineData("fr", "Supprimer 1 widget", "Supprimer 6 widgets")]
    [InlineData("es", "Eliminar 1 widget", "Eliminar 6 widgets")]
    [InlineData("zh-Hans", "删除 1 个小组件", "删除 6 个小组件")]
    public void TheButtonCountsInEveryLanguage(string language, string one, string six)
    {
        using var _ = InLanguage.Use(language);
        Assert.Equal(one, S.DockDeleteButton(1));
        Assert.Equal(six, S.DockDeleteButton(6));
        Assert.Contains("6", S.DockDeleteFolderContent(6));
        Assert.Contains("Sound", S.DockDeleteFolderTitle("Sound"));
    }
}
