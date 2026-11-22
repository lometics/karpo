// rss: reading a feed (Karpo.Core\Feeds.cs): RSS 2.0, RSS 1.0's RDF and Atom into a title, a site
// and the newest items, and everything a feed may not do to Karpo — expand an entity of its own,
// reach a file, put HTML or a script in front of the person, or name something other than a web page
// for a click to open. Plain logic: no window, no network.

namespace Karpo.Tests.Rss;

public sealed class FeedParserTests
{
    private const string Rss2 = """
        <?xml version="1.0" encoding="UTF-8"?>
        <rss version="2.0" xmlns:media="http://search.yahoo.com/mrss/" xmlns:dc="http://purl.org/dc/elements/1.1/">
          <channel>
            <title>Example &amp; Co News</title>
            <link>https://news.example.com/</link>
            <item>
              <title>Second story</title>
              <link>https://news.example.com/2</link>
              <guid isPermaLink="false">story-2</guid>
              <pubDate>Tue, 10 Jun 2025 04:00:00 GMT</pubDate>
              <enclosure url="https://cdn.example.com/2.jpg" type="image/jpeg" length="1000" />
            </item>
            <item>
              <title><![CDATA[First <em>story</em>]]></title>
              <link>/1</link>
              <guid>https://news.example.com/1</guid>
              <pubDate>Tue, 10 Jun 2025 06:30:00 +0200</pubDate>
              <media:thumbnail url="https://cdn.example.com/1.jpg" width="120" height="80" />
            </item>
            <item>
              <title>Third story</title>
              <link>https://news.example.com/3</link>
              <dc:date>2025-06-09T22:00:00Z</dc:date>
              <enclosure url="https://cdn.example.com/3.mp3" type="audio/mpeg" length="1000" />
            </item>
          </channel>
        </rss>
        """;

    private const string Atom = """
        <?xml version="1.0" encoding="utf-8"?>
        <feed xmlns="http://www.w3.org/2005/Atom" xmlns:media="http://search.yahoo.com/mrss/" xml:base="https://blog.example.org/">
          <title type="html">A &lt;b&gt;bold&lt;/b&gt; blog</title>
          <link rel="self" href="https://blog.example.org/feed.atom" />
          <link rel="alternate" href="https://blog.example.org/" />
          <entry>
            <title type="text">Tom &amp;amp; Jerry &#8217;s day</title>
            <link rel="alternate" href="posts/one" />
            <id>tag:blog.example.org,2025:1</id>
            <updated>2025-06-10T08:00:00Z</updated>
            <published>2025-06-10T07:00:00+01:00</published>
            <media:group><media:content url="https://cdn.example.org/one.png" medium="image" /></media:group>
          </entry>
          <entry>
            <title type="xhtml"><div xmlns="http://www.w3.org/1999/xhtml">An <i>xhtml</i> title</div></title>
            <link href="https://blog.example.org/posts/two" />
            <link rel="enclosure" type="image/png" href="https://cdn.example.org/two.png" />
            <id>tag:blog.example.org,2025:2</id>
            <updated>2025-06-09T08:00:00Z</updated>
          </entry>
        </feed>
        """;

    private const string Rdf = """
        <?xml version="1.0"?>
        <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#" xmlns="http://purl.org/rss/1.0/" xmlns:dc="http://purl.org/dc/elements/1.1/">
          <channel rdf:about="https://old.example.net/">
            <title>Old school</title>
            <link>https://old.example.net/</link>
          </channel>
          <item rdf:about="https://old.example.net/a">
            <title>RDF item</title>
            <link>https://old.example.net/a</link>
            <dc:date>2025-01-02T03:04:05Z</dc:date>
          </item>
        </rdf:RDF>
        """;

    [Fact]
    public void AnRss2FeedIsReadNewestFirst()
    {
        var feed = Feeds.Parse(Rss2, new Uri("https://news.example.com/rss"));

        Assert.Equal("Example & Co News", feed.Title);
        Assert.Equal("https://news.example.com/", feed.Site);
        // Every item says when, so they are put newest first: 04:30 UTC, 04:00 UTC, the day before.
        Assert.Equal(["First story", "Second story", "Third story"], feed.Items.Select(item => item.Title));
        var first = feed.Items[0];
        Assert.Equal("https://news.example.com/1", first.Link);
        Assert.Equal(new DateTimeOffset(2025, 6, 10, 4, 30, 0, TimeSpan.Zero), first.Published!.Value.ToUniversalTime());
        Assert.Equal("https://cdn.example.com/1.jpg", first.Thumbnail);
        Assert.Equal("https://news.example.com/1", first.Key);
        // An image enclosure is a thumbnail, a guid that is no permalink is a key and not a link.
        Assert.Equal("https://cdn.example.com/2.jpg", feed.Items[1].Thumbnail);
        Assert.Equal("story-2", feed.Items[1].Key);
        Assert.Equal("https://news.example.com/2", feed.Items[1].Link);
        // An audio enclosure is not a picture.
        Assert.Equal("", feed.Items[2].Thumbnail);
        Assert.Equal(new DateTimeOffset(2025, 6, 9, 22, 0, 0, TimeSpan.Zero), feed.Items[2].Published);
    }

    [Fact]
    public void AnAtomFeedIsRead()
    {
        var feed = Feeds.Parse(Atom, new Uri("https://blog.example.org/feed.atom"));

        Assert.Equal("A bold blog", feed.Title);
        Assert.Equal("https://blog.example.org/", feed.Site);
        Assert.Equal(2, feed.Items.Count);
        var one = feed.Items[0];
        // Encoded twice, decoded twice; a numeric entity is its character.
        Assert.Equal("Tom & Jerry ’s day", one.Title);
        // Relative to xml:base; published before updated.
        Assert.Equal("https://blog.example.org/posts/one", one.Link);
        Assert.Equal(new DateTimeOffset(2025, 6, 10, 6, 0, 0, TimeSpan.Zero), one.Published!.Value.ToUniversalTime());
        Assert.Equal("https://cdn.example.org/one.png", one.Thumbnail);
        Assert.Equal("tag:blog.example.org,2025:1", one.Key);
        var two = feed.Items[1];
        Assert.Equal("An xhtml title", two.Title);
        Assert.Equal("https://blog.example.org/posts/two", two.Link);
        Assert.Equal("https://cdn.example.org/two.png", two.Thumbnail);
    }

    [Fact]
    public void AnRdfFeedIsRead()
    {
        var feed = Feeds.Parse(Rdf);

        Assert.Equal("Old school", feed.Title);
        Assert.Equal("https://old.example.net/", feed.Site);
        var item = Assert.Single(feed.Items);
        Assert.Equal("RDF item", item.Title);
        Assert.Equal("https://old.example.net/a", item.Link);
        Assert.Equal(new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero), item.Published);
    }

    [Fact]
    public void TheBytesEncodingIsTheDocumentsOwn()
    {
        const string text = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?><rss version=\"2.0\"><channel><title>Zürich</title><item><title>Grüezi</title></item></channel></rss>";
        var bytes = System.Text.Encoding.Latin1.GetBytes(text);

        var feed = Feeds.Parse(bytes);

        Assert.Equal("Zürich", feed.Title);
        Assert.Equal("Grüezi", feed.Items[0].Title);
    }

    [Theory]
    // Not XML at all: a JSON answer, a web page that is not well-formed, half a file, nothing.
    [InlineData("""{"items": []}""", FeedProblem.NotXml)]
    [InlineData("<html><body><p>Not found<br></body></html>", FeedProblem.NotXml)]
    [InlineData("<rss version=\"2.0\"><channel><title>Cut off", FeedProblem.NotXml)]
    [InlineData("", FeedProblem.NotXml)]
    // Well-formed, and no feed.
    [InlineData("<html><body><p>A page</p></body></html>", FeedProblem.NotAFeed)]
    [InlineData("<rss version=\"2.0\"></rss>", FeedProblem.NotAFeed)]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" />", FeedProblem.NotAFeed)]
    public void WhatIsNotAFeedSaysWhy(string text, FeedProblem why)
    {
        var error = Assert.Throws<FeedException>(() => Feeds.Parse(text));
        Assert.Equal(why, error.Problem);
    }

    /// <summary>
    /// The billion laughs: a DTD whose entities expand into gigabytes. Nothing of a DTD is processed,
    /// so the document is refused at once — an entity it uses is one nobody declared — and nothing
    /// grows.
    /// </summary>
    [Fact]
    public void AnEntityBombIsNotExpanded()
    {
        const string bomb = """
            <?xml version="1.0"?>
            <!DOCTYPE rss [
              <!ENTITY lol "lol">
              <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;">
              <!ENTITY lol3 "&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;">
              <!ENTITY lol4 "&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;">
              <!ENTITY lol5 "&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;">
              <!ENTITY lol6 "&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;">
              <!ENTITY lol7 "&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;&lol6;">
              <!ENTITY lol8 "&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;&lol7;">
              <!ENTITY lol9 "&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;&lol8;">
            ]>
            <rss version="2.0"><channel><title>&lol9;</title><item><title>x</title></item></channel></rss>
            """;

        Assert.Equal(FeedProblem.NotXml, Assert.Throws<FeedException>(() => Feeds.Parse(bomb)).Problem);
    }

    /// <summary>An external entity naming a file on this PC is never read: the document is refused, and nothing of the file is in any answer.</summary>
    [Fact]
    public void AnExternalEntityIsNeverResolved()
    {
        var file = Path.Combine(Path.GetTempPath(), $"karpo-xxe-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, "secret-words-from-this-pc");
        try
        {
            var xxe = $"""
                <?xml version="1.0"?>
                <!DOCTYPE rss [ <!ENTITY peek SYSTEM "{new Uri(file).AbsoluteUri}"> ]>
                <rss version="2.0"><channel><title>&peek;</title><item><title>&peek;</title></item></channel></rss>
                """;
            var error = Record.Exception(() => Feeds.Parse(xxe));
            var read = error is null ? Feeds.Parse(xxe) : null;
            Assert.True(error is FeedException || read is not null);
            Assert.DoesNotContain("secret-words", read?.Title ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain("secret-words", read?.Items.FirstOrDefault()?.Title ?? "", StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData("<b>Bold</b> news", "Bold news")]
    [InlineData("&lt;b&gt;Escaped&lt;/b&gt; tags", "Escaped tags")]
    [InlineData("Tom &amp;amp; Jerry", "Tom & Jerry")]
    [InlineData("Caf&eacute; &#233; &#x00E9; &nbsp;open", "Café é é open")]
    [InlineData("  Many\n\tspaces   in  it  ", "Many spaces in it")]
    [InlineData("A<br/>line<p>break</p>", "A line break")]
    [InlineData("Zero​width\u0007bell", "Zerowidthbell")]
    [InlineData("<script>alert('x')</script>Safe", "alert('x') Safe")]
    [InlineData(null, "")]
    public void ATitleIsPlainWords(string? text, string plain) =>
        Assert.Equal(plain, Feeds.Plain(text, Feeds.MostTitle));

    [Fact]
    public void ALongTitleIsCutWithAnEllipsis()
    {
        var plain = Feeds.Plain(new string('a', 1000), Feeds.MostTitle);

        Assert.Equal(Feeds.MostTitle, plain.Length);
        Assert.EndsWith("…", plain, StringComparison.Ordinal);
        // Never in half a surrogate pair.
        var emoji = Feeds.Cut(new string('a', 8) + "\U0001F600\U0001F600", 10);
        Assert.False(char.IsHighSurrogate(emoji[^2]));
    }

    [Fact]
    public void AFeedKeepsAtMostThirtyItems()
    {
        var items = string.Concat(Enumerable.Range(0, 100).Select(index => $"<item><title>Item {index}</title></item>"));

        var feed = Feeds.Parse($"<rss version=\"2.0\"><channel><title>Many</title>{items}</channel></rss>");

        Assert.Equal(Feeds.MostItems, feed.Items.Count);
        // No dates: the feed's own order is kept.
        Assert.Equal("Item 0", feed.Items[0].Title);
    }

    [Fact]
    public void AnItemWithNoTitleIsHeadlinedByItsWordsAndOneWithNeitherIsLeftOut()
    {
        var feed = Feeds.Parse("""
            <rss version="2.0"><channel><title>Microblog</title>
              <item><description>&lt;p&gt;Just a short post&lt;/p&gt;</description><link>https://m.example.com/1</link></item>
              <item><link>https://m.example.com/2</link></item>
            </channel></rss>
            """);

        var item = Assert.Single(feed.Items);
        Assert.Equal("Just a short post", item.Title);
    }

    [Fact]
    public void AFeedThatNamesItselfNothingIsCalledByItsHost()
    {
        var feed = Feeds.Parse("<rss version=\"2.0\"><channel><link>https://www.example.com/</link><item><title>x</title></item></channel></rss>");

        Assert.Equal("example.com", feed.Title);
    }

    [Fact]
    public void APictureInTheWordsIsTheLastThumbnailThereIs()
    {
        var feed = Feeds.Parse("""
            <rss version="2.0"><channel><title>Blog</title><link>https://blog.example.com/</link>
              <item><title>With a picture</title><description>&lt;p&gt;&lt;img alt="x" src="/images/a.png"&gt;&lt;/p&gt;</description></item>
              <item><title>A script for a picture</title><description>&lt;img src="javascript:alert(1)"&gt;</description></item>
            </channel></rss>
            """);

        Assert.Equal("https://blog.example.com/images/a.png", feed.Items[0].Thumbnail);
        Assert.Equal("", feed.Items[1].Thumbnail);
    }

    [Theory]
    [InlineData("Tue, 10 Jun 2003 04:00:00 GMT", "2003-06-10T04:00:00+00:00")]
    [InlineData("Tue, 10 Jun 2003 04:00:00 +0200", "2003-06-10T04:00:00+02:00")]
    [InlineData("10 Jun 2003 04:00 -0500", "2003-06-10T04:00:00-05:00")]
    [InlineData("Tue, 10 Jun 03 04:00:00 EDT", "2003-06-10T04:00:00-04:00")]
    [InlineData("Wed, 2 Oct 2002 13:00:00 PST", "2002-10-02T13:00:00-08:00")]
    [InlineData("Wed, 02 October 2002 08:00:00 UT", "2002-10-02T08:00:00+00:00")]
    [InlineData("Wed, 02 Oct 2002 08:00:00 Z", "2002-10-02T08:00:00+00:00")]
    [InlineData("Wed, 02 Oct 2002 08:00:00", "2002-10-02T08:00:00+00:00")]
    [InlineData("2003-12-13T18:30:02Z", "2003-12-13T18:30:02+00:00")]
    [InlineData("2003-12-13T18:30:02.25+01:00", "2003-12-13T18:30:02.25+01:00")]
    [InlineData("2003-12-13", "2003-12-13T00:00:00+00:00")]
    public void ADateIsReadInEitherForm(string text, string expected) =>
        Assert.Equal(DateTimeOffset.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Feeds.Date(text));

    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("Tue, 31 Feb 2003 04:00:00 GMT")]
    [InlineData("Tue, 10 Jun 2003 25:00:00 GMT")]
    [InlineData(null)]
    public void WhatIsNoDateIsNone(string? text) => Assert.Null(Feeds.Date(text));
}
