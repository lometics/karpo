// rss: one fixed sample feed for the pictures and the drawn tests — eight stories of a made-up news
// site, their ages fixed against the frozen clock, five with pictures — kept in a store that already
// holds it (as rss.json would after a read) and whose site never answers, so nothing is fetched on
// the pool and waited for, and every run draws the same.

using System.Runtime.CompilerServices;
using System.Text.Json;
using Karpo.Dock;
using Karpo.Dock.Favicons;
using Karpo.Dock.Rss;

namespace Karpo.Tests.Rss;

internal static class RssSamples
{
    public const string Address = "https://news.example.com/rss.xml";

    /// <summary>The stories, newest first: their words, minutes before the frozen clock, and whether each has a picture.</summary>
    private static readonly (string Title, int MinutesAgo, bool Picture)[] Stories =
    [
        ("Lake Zurich ferries add evening crossings for the autumn season", 4, true),
        ("New tram line opens between the station and the university hospital", 38, true),
        ("Researchers map the glacier's retreat with drones over a decade", 2 * 60, false),
        ("City council approves budget for the cycling network", 5 * 60, true),
        ("Local bakery wins national prize for its sourdough", 26 * 60, false),
        ("Weekend weather: sun in the mountains, fog in the lowlands", 2 * 24 * 60, true),
        ("Museum opens a hall of mechanical clocks", 9 * 24 * 60, true),
        ("Night trains return to three routes in December", 40 * 24 * 60, false),
    ];

    /// <summary>The feed as the site would send it and the reader would read it, against <paramref name="now"/>.</summary>
    public static Feed Feed(DateTime now) => new()
    {
        Title = "Example News",
        Site = "https://news.example.com/",
        Items = [.. Stories.Select((story, index) => new FeedItem
        {
            Title = story.Title,
            Link = $"https://news.example.com/stories/{index + 1}",
            Published = new DateTimeOffset(now).AddMinutes(-story.MinutesAgo),
            Thumbnail = story.Picture ? $"https://news.example.com/pictures/{index + 1}.bmp" : "",
            Key = $"story-{index + 1}",
        })],
    };

    /// <summary>
    /// A store that already holds the sample, read three minutes before the real clock (so it is not
    /// due), with its first two stories new since the panel was last opened, whose site never
    /// answers, and whose pictures are made here — a soft gradient of their own, one hue a story.
    /// </summary>
    /// <param name="sites">Where its site's icon comes from (§306); none by default, as in a test run.</param>
    /// <param name="fails">Its site cannot be reached: a read asked for fails, and what was read before stays.</param>
    public static RssStore Settled(DateTime now, int fresh = 2, FaviconStore? sites = null, Feed? held = null, bool fails = false)
    {
        var feed = held ?? Feed(now);
        var cache = new RssCache
        {
            Address = Address,
            FetchedUtc = DateTime.UtcNow.AddMinutes(-3),
            Saved = RssSaved.Of(feed),
            Seen = [.. feed.Items.Skip(fresh).Select(item => item.Key)],
        };
        var file = JsonSerializer.Serialize(new RssFile { Feeds = [cache] }, RssJson.Default.RssFile);
        return new RssStore(
            new RssSource
            {
                Ask = fails ? (_, _) => throw new HttpRequestException("no route") : (_, cancel) => new TaskCompletionSource<RssAnswer>().Task.WaitAsync(cancel),
                // A hue from the address's own letters, the same on every run.
                Picture = (address, _) => Task.FromResult<byte[]?>(Bitmap(address.Sum(letter => letter * 37))),
            },
            new RssDisk { Read = () => file, Write = _ => { } },
            sites: () => sites);
    }

    /// <summary>
    /// A store of site icons that keeps nothing on disk unless handed one, whose sites answer only at
    /// <paramref name="icons"/>' addresses, each with its bytes; what was asked is written down in
    /// <paramref name="asked"/>.
    /// </summary>
    public static FaviconStore Sites(IReadOnlyDictionary<string, byte[]> icons, List<string>? asked = null, FaviconDisk? disk = null) => new(
        new FaviconSource
        {
            Page = (_, _) => Task.FromResult<string?>(null),
            Fetch = (address, _) =>
            {
                lock (icons)
                {
                    asked?.Add(address);
                }
                return Task.FromResult<(byte[], string?)?>(icons.TryGetValue(address, out var bytes) ? (bytes, "image/bmp") : null);
            },
        },
        disk ?? FaviconDisk.None);

    /// <summary>A store whose site never answers and that holds nothing: the widget is getting the news for as long as the test wants.</summary>
    public static RssStore Waiting() =>
        new(new RssSource { Ask = (_, cancel) => new TaskCompletionSource<RssAnswer>().Task.WaitAsync(cancel) }, RssDisk.None);

    /// <summary>A store whose site cannot be reached, holding nothing read before.</summary>
    public static RssStore Broken()
    {
        var store = new RssStore(new RssSource { Ask = (_, _) => throw new HttpRequestException("no route") }, RssDisk.None);
        broken.Add(store, store);
        return store;
    }

    /// <summary>The stores <see cref="Broken"/> made: a widget on one is drawn once its read has failed, never while it is still asking.</summary>
    private static readonly ConditionalWeakTable<RssStore, RssStore> broken = [];

    /// <summary>A widget of <paramref name="item"/>'s kind on the sample's address, on <paramref name="store"/>.</summary>
    public static RssIsland Made(Gfx gfx, WidgetItem item, RssStore store, bool feed = true)
    {
        item.Id = "news";
        if (feed)
        {
            item.Rss = new RssOptions { Address = Address, Title = "Example News" };
        }
        item.Normalize();
        var island = new RssIsland(Config.IslandKey(null, item.Id), item, gfx, new Config(), store);
        if (feed && broken.TryGetValue(store, out _))
        {
            // The read runs on the pool: the failure is waited for, then taken, as the dock's tick would.
            Assert.True(Patience.Until(() => store.Read(island).Problem is not null), "the failed read never came back");
            island.MinuteChanged(gfx);
        }
        return island;
    }

    /// <summary>Waits for the pictures the widget asked for to arrive, so a drawing has them; how many did.</summary>
    public static int Pictured(RssIsland island)
    {
        var wanted = island.Headlines.Where(item => item.Thumbnail.Length > 0).Select(item => item.Thumbnail).ToList();
        foreach (var address in wanted)
        {
            _ = island.Store.Picture(island, address);
        }
        Patience.Until(() => wanted.All(address => island.Store.Picture(island, address) is not null));
        return wanted.Count(address => island.Store.Picture(island, address) is not null);
    }

    /// <summary>A 96 x 64 24-bit BMP (or of the size asked), a diagonal gradient of one hue picked by <paramref name="seed"/>: a stand-in for a photo, or a site's icon.</summary>
    internal static byte[] Bitmap(int seed, int width = 96, int height = 64)
    {
        var stride = width * 3;
        var size = 54 + (stride * height);
        var bytes = new byte[size];
        bytes[0] = (byte)'B';
        bytes[1] = (byte)'M';
        BitConverter.GetBytes(size).CopyTo(bytes, 2);
        BitConverter.GetBytes(54).CopyTo(bytes, 10);
        BitConverter.GetBytes(40).CopyTo(bytes, 14);
        BitConverter.GetBytes(width).CopyTo(bytes, 18);
        BitConverter.GetBytes(height).CopyTo(bytes, 22);
        BitConverter.GetBytes((short)1).CopyTo(bytes, 26);
        BitConverter.GetBytes((short)24).CopyTo(bytes, 28);
        var hue = Math.Abs(seed % 360) / 360f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var light = 0.35f + (0.45f * (x + y) / (width + height));
                var (r, g, b) = Hsl(hue, 0.55f, light);
                var at = 54 + (y * stride) + (x * 3);
                (bytes[at], bytes[at + 1], bytes[at + 2]) = (b, g, r);
            }
        }
        return bytes;
    }

    private static (byte R, byte G, byte B) Hsl(float h, float s, float l)
    {
        float Channel(float t)
        {
            t = t < 0 ? t + 1 : t > 1 ? t - 1 : t;
            var q = l < 0.5f ? l * (1 + s) : l + s - (l * s);
            var p = (2 * l) - q;
            return t < 1 / 6f ? p + ((q - p) * 6 * t) : t < 0.5f ? q : t < 2 / 3f ? p + ((q - p) * ((2 / 3f) - t) * 6) : p;
        }
        return ((byte)(Channel(h + (1 / 3f)) * 255), (byte)(Channel(h) * 255), (byte)(Channel(h - (1 / 3f)) * 255));
    }
}
