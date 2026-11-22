// rss: the rules every place that shows a feed shares (Karpo.Core\Feeds.cs, docs\widgets.md §39) —
// how old a headline is said to be, in the language Karpo speaks; which links are ever opened; what a
// typed address becomes. And the menu's "Open website", which opens a feed's site and nothing else.

using Karpo.Dock;
using Karpo.Tests.Localization;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Rss;

[Collection(InLanguage.Collection)]
public sealed class FeedRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 26, 15, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "Now")]
    [InlineData(59, "Now")]
    [InlineData(60, "1 m")]
    [InlineData(59 * 60, "59 m")]
    [InlineData(2 * 3600, "2 h")]
    [InlineData((23 * 3600) + 3599, "23 h")]
    [InlineData(3 * 86400, "3 d")]
    [InlineData(13 * 86400, "1 w")]
    [InlineData(400 * 86400, "1 y")]
    // A clock somewhere is wrong: an item from the future is now.
    [InlineData(-3600, "Now")]
    public void AnAgeIsShortAndRounded(int seconds, string said) =>
        Assert.Equal(said, Feeds.Age(Now.AddSeconds(-seconds), Now));

    [Fact]
    public void NoDateIsNoAge() => Assert.Equal("", Feeds.Age(null, Now));

    /// <summary>An age is the language's own: German, French and Chinese write their units their way.</summary>
    [Theory]
    [InlineData("de", 2 * 3600, "2 Std.")]
    [InlineData("de", 5 * 60, "5 Min.")]
    [InlineData("fr", 3 * 86400, "3 j")]
    [InlineData("fr", 10, "À l’instant")]
    [InlineData("es", 2 * 7 * 86400, "2 sem.")]
    [InlineData("zh-Hans", 2 * 3600, "2 小时")]
    public void AnAgeFollowsTheLanguage(string language, int seconds, string said)
    {
        using var _ = InLanguage.Use(language);
        Assert.Equal(said, Feeds.Age(Now.AddSeconds(-seconds), Now));
    }

    [Fact]
    public void ABylineIsTheSourceAndTheAge()
    {
        Assert.Equal("BBC News · 2 h", Feeds.Byline("BBC News", "2 h"));
        Assert.Equal("BBC News", Feeds.Byline("BBC News", ""));
        Assert.Equal("2 h", Feeds.Byline("", "2 h"));
    }

    [Theory]
    [InlineData("https://example.com/story", true)]
    [InlineData("http://example.com/story?x=1#y", true)]
    [InlineData("HTTPS://EXAMPLE.COM/", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/Windows/System32/calc.exe", false)]
    [InlineData(@"C:\Windows\System32\calc.exe", false)]
    [InlineData(@"\\server\share\run.bat", false)]
    [InlineData("ms-settings:display", false)]
    [InlineData("mailto:someone@example.com", false)]
    [InlineData("data:text/html,<script>alert(1)</script>", false)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("/relative/path", false)]
    [InlineData("https://", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyAWebAddressIsEverOpened(string? link, bool opens) => Assert.Equal(opens, Feeds.Opens(link));

    [Theory]
    [InlineData("https://example.com/feed.xml", "https://example.com/feed.xml")]
    [InlineData("  example.com/rss  ", "https://example.com/rss")]
    [InlineData("feed://example.com/rss", "https://example.com/rss")]
    [InlineData("feed:https://example.com/rss", "https://example.com/rss")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("file:///C:/feed.xml", null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void ATypedAddressIsOneKarpoAsksFor(string typed, string? address) => Assert.Equal(address, Feeds.Address(typed));

    [Theory]
    [InlineData("/1", "https://news.example.com/1")]
    [InlineData("story?id=2", "https://news.example.com/feeds/story?id=2")]
    [InlineData("//cdn.example.com/a.jpg", "https://cdn.example.com/a.jpg")]
    [InlineData("javascript:void(0)", "")]
    [InlineData("vbscript:msgbox", "")]
    [InlineData("file:///etc/passwd", "")]
    public void ALinkIsReadAgainstItsFeedAndOnlyKeptWhereItOpens(string link, string kept) =>
        Assert.Equal(kept, Feeds.Link(link, new Uri("https://news.example.com/feeds/rss")));

    /// <summary>
    /// The menu's "Open website" hands Windows the site the feed named, and only where that is a web
    /// address: a row made from anything else opens nothing at all.
    /// </summary>
    [Theory]
    [InlineData("https://news.example.com/", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData("ms-settings:", false)]
    public void TheMenusWebsiteRowOpensOnlyAWebAddress(string site, bool opened)
    {
        var handed = new List<string>();
        TheDock.RunAct(new WidgetActRow(TheDock.CMD_WIDGET_ACT, WidgetAct.OpenWebsite, site), widget: null, (target, _) => handed.Add(target));

        Assert.Equal(opened ? new[] { site } : [], handed);
    }

    /// <summary>
    /// With no widget behind it, the row is left out, not greyed (§308): the site comes from the feed,
    /// and there is none yet.
    /// </summary>
    [Fact]
    public void TheWebsiteRowIsLeftOutUntilTheFeedNamesASite()
    {
        var (items, rows) = TheDock.ActRows([WidgetAct.OpenWebsite], widget: null);

        Assert.Empty(items);
        Assert.Empty(rows);
        Assert.True(TheDock.HandsAway(WidgetAct.OpenWebsite));
    }
}
