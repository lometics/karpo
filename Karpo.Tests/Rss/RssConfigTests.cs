// rss: the RSS widget in config.json — its feed's address and the name the feed gave when it was
// proved, on a line of a folder (the bar's entry is the news preset's face since 2026-10-10) —
// written and read back as written, held to what Karpo opens when somebody edits the file by hand,
// and kept through the three-way merge when the dock and the settings app save at once.

using System.Text.Json;
using Karpo.Tests.Core;

namespace Karpo.Tests.Rss;

public sealed class RssConfigTests
{
    private const string Address = "https://news.example.com/rss.xml";

    private static CellItem Line(string address = Address, string title = "Example News")
    {
        var rss = ListKind.Rss.New();
        rss.Id = "news";
        rss.Rss = new RssOptions { Address = address, Title = title };
        rss.Normalize();
        return rss;
    }

    /// <summary>A folder holding the feed's line, as the news preset makes it.</summary>
    private static BarItem Folder(string address = Address, string title = "Example News")
    {
        var folder = Configs.Holding(Line(address, title));
        folder.Id = "folder";
        folder.Normalize();
        return folder;
    }

    /// <summary>The feed's line of the first folder of <paramref name="config"/>.</summary>
    private static RssOptions Feed(Config config) => Configs.Islands(config)[0].Folder!.Cells.Single().Island.Rss!;

    private static JsonElement Saved(Config config)
    {
        using var written = JsonDocument.Parse(config.ToJson());
        return Configs.Find(written.RootElement, "rss")!.Value.Clone();
    }

    [Fact]
    public void ANewWidgetHasNoFeedAndAsksForOne()
    {
        var made = ListKind.Rss.New();
        Assert.NotNull(made.Rss);
        Assert.False(made.Rss!.IsSet);
        Assert.Equal(SetupField.Feed, WidgetSetup.Needs(made));
        Assert.Null(WidgetSetup.Needs(Line()));
    }

    /// <summary>The address and the name are written and read back; the headline switch the bar's entry had is the news face's now, and is never written on the line.</summary>
    [Fact]
    public void TheFeedIsWrittenAndReadBack()
    {
        var config = Configs.With(Folder());
        var saved = Saved(config);
        Assert.Equal(Address, saved.GetProperty("address").GetString());
        Assert.Equal("Example News", saved.GetProperty("title").GetString());
        Assert.False(saved.TryGetProperty("showHeadline", out _));

        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        var rss = Feed(back);
        Assert.Equal(Address, rss.Address);
        Assert.Equal("Example News", rss.Title);
    }

    /// <summary>Only the widget's own record is written: a keyboard carries no feed, and an RSS widget no stock.</summary>
    [Fact]
    public void OnlyTheFeedRecordIsCarried()
    {
        var rss = Line();
        rss.Market = new MarketOptions { Symbol = "MSFT" };
        rss.Normalize();
        Assert.Null(rss.Market);

        var keyboard = BarKind.NightLight.New();
        keyboard.Rss = new RssOptions { Address = Address };
        keyboard.Normalize();
        Assert.Null(keyboard.Rss);
    }

    /// <summary>A hand-edited file's address is held to one Karpo asks for, and the feed's name to plain words.</summary>
    [Theory]
    [InlineData("https://news.example.com/rss.xml", "https://news.example.com/rss.xml")]
    [InlineData("news.example.com/rss.xml", "https://news.example.com/rss.xml")]
    [InlineData("javascript:alert(1)", "")]
    [InlineData("file:///C:/Users/feed.xml", "")]
    [InlineData("", "")]
    public void AnAddressInAFileIsHeldToAWebAddress(string written, string kept)
    {
        var config = Configs.Repaired($$"""
            { "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "f", "kind": "folderList", "folder": { "name": "News", "lists": [ [
                { "island": { "id": "news", "kind": "rss", "rss": { "address": "{{written}}", "title": "<b>Bold</b> &amp; news" } } } ] ] } } ] } ] }
            """);
        var rss = Feed(config);
        Assert.Equal(kept, rss.Address);
        Assert.Equal("Bold & news", rss.Title);
    }

    /// <summary>
    /// The settings app proves a feed while the dock saves the folder's name: both survive the
    /// three-way merge, in either order of saving.
    /// </summary>
    [Fact]
    public void AFeedSurvivesTheMerge()
    {
        var @base = Configs.With(Folder(address: "", title: "")).ToJson();

        var settings = Config.Parse(@base);
        var line = settings.DefaultLayout.Islands[0].Folder!.Cells.Single().Island.Rss!;
        line.Address = Address;
        line.Title = "Example News";
        var dock = Config.Parse(@base);
        dock.DefaultLayout.Islands[0].Folder!.Align = FolderAlign.Right;

        foreach (var merged in new[]
        {
            Configs.Repaired(ConfigMerge.Merge(@base, settings.ToJson(), dock.ToJson())),
            Configs.Repaired(ConfigMerge.Merge(@base, dock.ToJson(), settings.ToJson())),
        })
        {
            var rss = Feed(merged);
            Assert.Equal(Address, rss.Address);
            Assert.Equal("Example News", rss.Title);
            Assert.Equal(FolderAlign.Right, Configs.Islands(merged)[0].Folder!.Align);
        }
    }

    /// <summary>A copy is the same feed, its record its own.</summary>
    [Fact]
    public void ACopyIsTheSameFeed()
    {
        var one = Line();
        var copy = one.Copy();
        Assert.NotEqual(one.Id, copy.Id);
        Assert.Equal(one.Rss!.Address, copy.Rss!.Address);
        copy.Rss.Address = "https://other.example.com/feed";
        Assert.Equal(Address, one.Rss.Address);
    }
}
