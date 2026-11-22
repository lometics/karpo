// rss: the store the RSS widgets read (Karpo.Dock\Rss\RssStore.cs) — one read per feed at the one
// online pace, the site's ETag and Last-Modified sent back so an unchanged feed costs a 304, the last
// read kept through a failure and through a restart, which headlines are new since the panel was last
// opened, and why a read failed in words. Over a source of its own and a disk in memory: no network,
// no file of the person's, no window.

using Karpo.Dock;
using Karpo.Dock.Rss;

namespace Karpo.Tests.Rss;

public sealed class RssStoreTests
{
    private const string Address = "https://news.example.com/rss";

    private static Feed Sample(int items, int newestMinutesAgo = 0)
    {
        var now = DateTimeOffset.UtcNow;
        return new Feed
        {
            Title = "Example News",
            Site = "https://news.example.com/",
            Items = [.. Enumerable.Range(0, items).Select(index => new FeedItem
            {
                Title = $"Story {items - index}",
                Link = $"https://news.example.com/{items - index}",
                Key = $"story-{items - index}",
                Published = now.AddMinutes(-(newestMinutesAgo + (index * 30))),
            })],
        };
    }

    /// <summary>A source that answers from a queue, writing down what it was asked.</summary>
    private sealed class Answers
    {
        public readonly Queue<Func<RssAsk, RssAnswer>> Next = new();
        public readonly List<RssAsk> Asked = [];

        public RssSource Source => new()
        {
            Ask = (ask, _) =>
            {
                lock (Asked)
                {
                    Asked.Add(ask);
                }
                return Task.FromResult(Next.Dequeue()(ask));
            },
        };
    }

    private static RssOptions Options(string address = Address) => new() { Address = address };

    /// <summary>Waits for the read the store started to come back.</summary>
    private static RssRead Settled(RssStore store, object user)
    {
        Assert.True(Patience.Until(() => !store.Read(user).Fetching), "the read never came back");
        return store.Read(user);
    }

    [Fact]
    public void TheFirstReadShowsTheFeedAndNothingInItIsNew()
    {
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(5), null, null));
        var store = new RssStore(answers.Source, RssDisk.None);
        var user = new object();

        store.Use(user, Options());
        Assert.Null(store.Read(user).Feed);
        store.Poll(user);
        var read = Settled(store, user);

        Assert.Equal(5, read.Feed!.Items.Count);
        Assert.Null(read.Problem);
        Assert.Equal(0, read.NewCount);
        Assert.Single(answers.Asked);
    }

    [Fact]
    public void ANewerHeadlineIsNewUntilThePanelIsOpened()
    {
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(3), null, null));
        answers.Next.Enqueue(_ => new RssAnswer(Sample(4), null, null));
        var store = new RssStore(answers.Source, RssDisk.None);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        Settled(store, user);

        Assert.True(store.Ask(user));
        var read = Settled(store, user);
        Assert.Equal(1, read.NewCount);
        Assert.True(read.IsNew(read.Feed!.Items[0]));
        Assert.False(read.IsNew(read.Feed.Items[1]));

        store.Seen(user);
        Assert.Equal(0, store.Read(user).NewCount);
    }

    /// <summary>The site's ETag and Last-Modified go back with the next read, and a 304 keeps the feed as it was.</summary>
    [Fact]
    public void AnUnchangedFeedIsAskedWithWhatTheSiteSaidAndKept()
    {
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(3), "\"abc\"", "Tue, 10 Jun 2025 04:00:00 GMT"));
        answers.Next.Enqueue(ask => new RssAnswer(null, ask.ETag, ask.LastModified));
        var store = new RssStore(answers.Source, RssDisk.None);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        var first = Settled(store, user);

        store.Ask(user);
        var second = Settled(store, user);

        Assert.Equal(2, answers.Asked.Count);
        Assert.Null(answers.Asked[0].ETag);
        Assert.Equal("\"abc\"", answers.Asked[1].ETag);
        Assert.Equal("Tue, 10 Jun 2025 04:00:00 GMT", answers.Asked[1].LastModified);
        Assert.Equal(first.Feed!.Items.Select(item => item.Key), second.Feed!.Items.Select(item => item.Key));
        Assert.True(second.FetchedUtc >= first.FetchedUtc);
    }

    /// <summary>A failed read keeps the headlines read before and says why beside them, never an empty widget.</summary>
    [Fact]
    public void AFailureKeepsWhatWasReadAndSaysWhy()
    {
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(3), null, null));
        answers.Next.Enqueue(_ => throw new HttpRequestException("no route"));
        var store = new RssStore(answers.Source, RssDisk.None);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        Settled(store, user);

        store.Ask(user);
        var read = Settled(store, user);

        Assert.Equal(3, read.Feed!.Items.Count);
        Assert.Equal(S.DockRssUnreachable, read.Problem);
    }

    [Fact]
    public void WhyAReadFailedIsSaid()
    {
        Assert.Equal(S.DockRssNotAFeed, RssStore.Explain(new FeedException(FeedProblem.NotAFeed)));
        Assert.Equal(S.DockRssUnreadable, RssStore.Explain(new FeedException(FeedProblem.NotXml)));
        Assert.Equal(S.DockRssTooLong, RssStore.Explain(new FeedException(FeedProblem.TooLong)));
        Assert.Equal(S.DockRssRefused, RssStore.Explain(new RssRefusedException()));
        Assert.Equal(S.DockRssUnreachable, RssStore.Explain(new HttpRequestException()));
        Assert.Equal(S.DockRssNoAnswer, RssStore.Explain(new TaskCanceledException()));
        Assert.Equal(S.DockRssUnreadable, RssStore.Explain(new InvalidOperationException()));
    }

    /// <summary>
    /// A feed is read again at the one online interval (OnlineUpdates) and not before, however often
    /// the widgets ask; a failure waits five minutes; a site that said to slow down, half an hour,
    /// even for a hand's Refresh.
    /// </summary>
    [Fact]
    public void AFeedIsReadAtTheOnlineInterval()
    {
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        answers.Next.Enqueue(_ => throw new RssBusyException());
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        var updates = new OnlineUpdates();
        updates.Set(30);
        var store = new RssStore(answers.Source, RssDisk.None, () => now, updates);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        Settled(store, user);

        Assert.Equal(TimeSpan.FromMinutes(30), store.Every);
        now += store.Every - TimeSpan.FromSeconds(1);
        store.Poll(user);
        Assert.Single(answers.Asked);
        now += TimeSpan.FromSeconds(1);
        store.Poll(user);
        Settled(store, user);
        Assert.Equal(2, answers.Asked.Count);

        // Busy: not again for half an hour, by the clock or by hand.
        now += TimeSpan.FromMinutes(29);
        store.Poll(user);
        Assert.False(store.Ask(user));
        Assert.Equal(2, answers.Asked.Count);
        now += TimeSpan.FromMinutes(1);
        store.Poll(user);
        Settled(store, user);
        Assert.Equal(3, answers.Asked.Count);
    }

    /// <summary>
    /// The wall clock set back (a hibernation on a PC whose clock ran fast, set right as it woke): the
    /// feed read "in the future" is not left waiting hours for the clock to catch up — it is due.
    /// </summary>
    [Fact]
    public void AClockSetBackLeavesNothingWaitingForIt()
    {
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        answers.Next.Enqueue(_ => throw new RssBusyException());
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        var updates = new OnlineUpdates();
        updates.Set(30);
        var store = new RssStore(answers.Source, RssDisk.None, () => now, updates);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        Settled(store, user);

        now -= TimeSpan.FromHours(3);
        store.Poll(user);
        Settled(store, user);
        Assert.Equal(2, answers.Asked.Count);

        // Busy, then the clock back again: the half hour's wait is not three and a half.
        now -= TimeSpan.FromHours(3);
        Assert.True(store.Ask(user));
        Settled(store, user);
        Assert.Equal(3, answers.Asked.Count);
    }

    /// <summary>
    /// The network back after a wake: a feed whose read failed is read at the next poll, not five
    /// minutes on — but a site that said it was busy (a 429) is still let rest its half hour.
    /// </summary>
    [Fact]
    public void TheNetworkBackRetriesAFailureButNotASiteThatSaidWait()
    {
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var answers = new Answers();
        answers.Next.Enqueue(_ => throw new HttpRequestException("no route"));
        answers.Next.Enqueue(_ => throw new RssBusyException());
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        var updates = new OnlineUpdates();
        var store = new RssStore(answers.Source, RssDisk.None, () => now, updates);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        Settled(store, user);
        store.Poll(user);
        Assert.Single(answers.Asked);

        updates.CameBack();
        store.Poll(user);
        Settled(store, user);
        Assert.Equal(2, answers.Asked.Count);

        updates.CameBack();
        store.Poll(user);
        Assert.Equal(2, answers.Asked.Count);
    }

    /// <summary>
    /// The interval is asked at each poll: set longer, a feed read twenty minutes ago is not due; set
    /// shorter, it is, with nothing else to do; and never sooner than the feeds' floor.
    /// </summary>
    [Fact]
    public void AChangedIntervalAppliesAtOnce()
    {
        var now = new DateTime(2026, 9, 26, 12, 0, 0, DateTimeKind.Utc);
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        var updates = new OnlineUpdates();
        updates.Set(60);
        var store = new RssStore(answers.Source, RssDisk.None, () => now, updates);
        var user = new object();
        store.Use(user, Options());
        store.Poll(user);
        Settled(store, user);

        now += TimeSpan.FromMinutes(20);
        store.Poll(user);
        Assert.Single(answers.Asked);
        updates.Set(15);
        store.Poll(user);
        Settled(store, user);
        Assert.Equal(2, answers.Asked.Count);

        updates.Set(1);
        Assert.Equal(TimeSpan.FromMinutes(RssStore.LeastMinutes), store.Every);
    }

    /// <summary>Two widgets of one feed are one read; the feed is let go of with the last of them.</summary>
    [Fact]
    public void OneFeedIsReadOnceForEveryWidgetOfIt()
    {
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(2), null, null));
        var store = new RssStore(answers.Source, RssDisk.None);
        var (one, two) = (new object(), new object());
        store.Use(one, Options());
        store.Use(two, Options());
        store.Poll(one);
        Settled(store, one);
        store.Poll(two);

        Assert.Single(answers.Asked);
        Assert.Equal(2, store.Read(two).Feed!.Items.Count);
        store.Release(one);
        Assert.NotNull(store.Read(two).Feed);
        store.Release(two);
        Assert.Null(store.Read(two).Feed);
    }

    /// <summary>No address, no read: a fresh widget asks nobody anything.</summary>
    [Fact]
    public void NoAddressIsNoRead()
    {
        var answers = new Answers();
        var store = new RssStore(answers.Source, RssDisk.None);
        var user = new object();
        store.Use(user, new RssOptions());
        store.Poll(user);
        Assert.False(store.Ask(user));
        Assert.Empty(answers.Asked);
    }

    /// <summary>
    /// The last read is kept in rss.json, with what had been seen, so a widget shows its headlines the
    /// moment Karpo starts — before any answer — and the dots are where they were.
    /// </summary>
    [Fact]
    public void TheLastReadIsKeptForTheNextStart()
    {
        string? file = null;
        var disk = new RssDisk { Read = () => file, Write = text => file = text };
        var answers = new Answers();
        answers.Next.Enqueue(_ => new RssAnswer(Sample(3), "\"v1\"", null));
        answers.Next.Enqueue(_ => new RssAnswer(Sample(4), "\"v2\"", null));
        var first = new RssStore(answers.Source, disk);
        var user = new object();
        first.Use(user, Options());
        first.Poll(user);
        Settled(first, user);
        first.Ask(user);
        Settled(first, user);
        Assert.NotNull(file);

        var never = new RssSource { Ask = (_, cancel) => new TaskCompletionSource<RssAnswer>().Task.WaitAsync(cancel) };
        var next = new RssStore(never, disk);
        var again = new object();
        next.Use(again, Options());
        var read = next.Read(again);
        Assert.Equal(4, read.Feed!.Items.Count);
        Assert.Equal("Example News", read.Feed.Title);
        Assert.Equal(1, read.NewCount);
    }

    /// <summary>A hand-edited rss.json is held to what a feed from the site is held to: plain words, web links.</summary>
    [Fact]
    public void AKeptFeedIsHeldToTheSameRules()
    {
        const string file = """
            { "feeds": [ { "address": "https://news.example.com/rss", "fetchedUtc": "2099-01-01T00:00:00Z",
              "saved": { "title": "<b>Kept</b>", "site": "javascript:alert(1)", "items": [
                { "title": "A &amp; B", "link": "file:///C:/Windows/notepad.exe", "thumbnail": "ms-settings:", "key": "k" },
                { "title": "", "link": "https://news.example.com/empty", "key": "e" } ] } } ] }
            """;
        var store = new RssStore(new RssSource { Ask = (_, cancel) => new TaskCompletionSource<RssAnswer>().Task.WaitAsync(cancel) }, new RssDisk { Read = () => file, Write = _ => { } });
        var user = new object();
        store.Use(user, Options());

        var feed = store.Read(user).Feed!;
        Assert.Equal("Kept", feed.Title);
        Assert.Equal("", feed.Site);
        var item = Assert.Single(feed.Items);
        Assert.Equal("A & B", item.Title);
        Assert.Equal("", item.Link);
        Assert.Equal("", item.Thumbnail);
    }

    /// <summary>A picture is cropped to the square in its middle, as a news card crops a photo.</summary>
    [Fact]
    public void APictureIsCroppedSquareFromItsMiddle()
    {
        // 4 x 2, each pixel's blue byte its column.
        var data = new byte[4 * 2 * 4];
        for (var row = 0; row < 2; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                data[((row * 4) + column) * 4] = (byte)column;
                data[(((row * 4) + column) * 4) + 3] = 255;
            }
        }
        var square = RssStore.Square(new Pixels { Data = data, Width = 4, Height = 2 })!;

        Assert.Equal(2, square.Width);
        Assert.Equal(2, square.Height);
        Assert.Equal([1, 2, 1, 2], new[] { square.Data[0], square.Data[4], square.Data[8], square.Data[12] }.Select(b => (int)b));
        Assert.Null(RssStore.Square(null));
    }

    /// <summary>A picture is only ever asked of a web address, and a store with no way to fetch one draws none.</summary>
    [Fact]
    public void APictureIsOnlyAskedOfAWebAddress()
    {
        var asked = new List<string>();
        var source = new RssSource
        {
            Ask = (_, cancel) => new TaskCompletionSource<RssAnswer>().Task.WaitAsync(cancel),
            Picture = (address, _) =>
            {
                lock (asked)
                {
                    asked.Add(address);
                }
                return Task.FromResult<byte[]?>(null);
            },
        };
        var store = new RssStore(source, RssDisk.None);
        var user = new object();
        store.Use(user, Options());

        Assert.Null(store.Picture(user, "file:///C:/Windows/win.ini"));
        Assert.Null(store.Picture(user, "javascript:alert(1)"));
        Assert.Null(store.Picture(user, "https://cdn.example.com/a.jpg"));
        Assert.True(Patience.Until(() => { lock (asked) { return asked.Count == 1; } }));
        Assert.Equal(["https://cdn.example.com/a.jpg"], asked);
        Assert.Null(new RssStore(new RssSource { Ask = source.Ask }, RssDisk.None).Picture(user, "https://cdn.example.com/a.jpg"));
    }
}
