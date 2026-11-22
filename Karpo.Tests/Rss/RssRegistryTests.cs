// rss: the RSS widget (the person, 2026-10-05: "add rss widget as well, for all three positions") —
// a list line since its taskbar entry went (2026-10-10, docs\widgets.md §98) to the news preset, whose
// face draws what the entry drew — with the answers its registry asks for, in the add list's "Time"
// group with its own words, no popup in a folder, and a menu decided for its line. Decisions, not
// drawings: no window.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Rss;

public sealed class RssRegistryTests
{
    [Fact]
    public void ItIsALineOnly()
    {
        var info = WidgetInfos.Of("rss");
        Assert.Same(WidgetInfos.Rss, info);
        Assert.Equal(WidgetCategory.Time, info.Category);
        Assert.Equal(WidgetOptionsKind.Rss, info.Options);
        Assert.Null(info.Settings);
        Assert.Null(BarEntries.Find("rss"));

        var line = ListWidgets.Of(ListKind.Rss);
        Assert.Same(info, line.Info);
        Assert.Equal(ListLine.Card, line.Line);
        // A headline's click opens its story: an act, never a popup (docs\widgets.md §15).
        Assert.Equal(CellAct.Action, line.Act);
        Assert.Equal(WidgetSource.Rss, line.Drawing);
        Assert.Equal((2, 24), line.Range);
    }

    /// <summary>The id is config.json's own word, "rss".</summary>
    [Fact]
    public void ItIsCalledRssInTheFile()
    {
        Assert.Equal("rss", ListKind.Rss.Id());
        Assert.Equal(ListKind.Rss, System.Text.Json.JsonSerializer.Deserialize<ListKind>("\"rss\""));
    }

    /// <summary>In a folder's add list, under "Time", with its name, a short blurb and words to be found by.</summary>
    [Fact]
    public void TheAddListOffersItInAFolder()
    {
        var row = Assert.Single(Catalogue.On(WidgetType.List), entry => entry.Id == "rss");
        Assert.Equal(WidgetCategory.Time, row.Category);
        Assert.Equal("RSS feed", row.Title);
        Assert.Equal("A news feed's newest headlines, RSS or Atom", WidgetInfos.Rss.Blurb);
        Assert.Contains("news", WidgetInfos.Rss.Keywords);
        Assert.Contains("atom", WidgetInfos.Rss.Keywords);
        Assert.True(CatalogueSearch.Matches("headlines", row));
    }

    /// <summary>
    /// The menu decided for its line (Dock.WidgetMenus.cs): the feed's own site, what the headlines
    /// panel had in its footer (the line's menu adds "Refresh" last, FolderMenuRows, §451). The news face has the same.
    /// </summary>
    [Fact]
    public void ItsMenuIsTheWebsite()
    {
        WidgetAct[] acts = [WidgetAct.OpenWebsite];
        Assert.Equal(acts, TheDock.ActsFor(ListKind.Rss));
        Assert.Equal(acts, TheDock.ActsFor(Presets.Folder(Presets.Find(Presets.NewsId)!, new PresetMachine(false, [])).Folder!.Face.Info));
        Assert.True(TheDock.HandsAway(WidgetAct.OpenWebsite));
    }

    /// <summary>
    /// What the feed is kept fetched by from the dock's start (SourceKinds): one source per address,
    /// however many widgets follow it, and none while no address is set.
    /// </summary>
    [Fact]
    public void OneFeedIsOneSource()
    {
        var one = ListKind.Rss.New();
        Assert.Null(SourceKinds.KeyOf(one));
        one.Rss!.Address = "https://news.example.com/rss";
        var other = ListKind.Rss.New();
        other.Rss!.Address = "https://news.example.com/rss";
        Assert.Equal(SourceKinds.KeyOf(one), SourceKinds.KeyOf(other));
        Assert.Equal(new SourceKey("rss", "https://news.example.com/rss"), SourceKinds.KeyOf(one));
    }
}
