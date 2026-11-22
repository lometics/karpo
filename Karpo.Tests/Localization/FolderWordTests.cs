// With the grid folder gone (2026-10-09, §70) there is one kind of folder, and the person: "List
// folder" should be called just "Folder" everywhere it is seen. A widget that stands in one is a
// "folder widget", as one on the bar is a "taskbar entry". Held here in every language's file: no
// text says "list folder", "list widget" or "list view" in any of its forms, and the folder and the
// library's page for its widgets say the one word.

namespace Karpo.Tests.Localization;

public sealed class FolderWordTests
{
    /// <summary>The words of the time before, by language: the folder, its widgets and its view, as each file said them.</summary>
    public static TheoryData<string, string> OldWords => new()
    {
        { "en", "list folder" }, { "en", "list widget" }, { "en", "list view" },
        { "de", "Listenordner" }, { "de", "Listen-Widget" }, { "de", "Listenansicht" },
        { "fr", "dossier liste" }, { "fr", "widget de liste" }, { "fr", "vue liste" },
        { "es", "carpeta de lista" }, { "es", "widget de lista" }, { "es", "vista de lista" },
        { "zh-Hans", "列表文件夹" }, { "zh-Hans", "列表小组件" }, { "zh-Hans", "列表视图" },
    };

    [Theory]
    [MemberData(nameof(OldWords))]
    public void NoTextSaysTheListFoldersOldName(string language, string words)
    {
        var said = StringFiles.Read(language)
            .Where(pair => pair.Value.Contains(words, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key);
        Assert.Empty(said);
    }

    /// <summary>The folder and the library's page of what goes in one: one word (§427: a folder has no name).</summary>
    [Theory]
    [InlineData("en", "Folder")]
    [InlineData("de", "Ordner")]
    [InlineData("fr", "Dossier")]
    [InlineData("es", "Carpeta")]
    [InlineData("zh-Hans", "文件夹")]
    public void TheFolderIsCalledFolder(string language, string word)
    {
        var map = StringFiles.Map(language);
        Assert.Equal(word, map["core.widget.folderList.title"]);
        Assert.Equal(word, map["core.catalogue.page.list"]);
    }

    /// <summary>In English, as the person reads it: the edit bar's button, the library and a folder widget's heading.</summary>
    [Fact]
    public void EnglishSaysFolderAndFolderWidget()
    {
        Assert.Equal("Folder", BarKind.FolderList.Title());
        Assert.Equal("Folder widget", WidgetType.List.Name());
        Assert.Equal("Taskbar entry", WidgetType.Bar.Name());
    }
}
