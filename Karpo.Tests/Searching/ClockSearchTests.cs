// A clock's click searches the time where it stands, with the app's engine (2026-10-28: "the time
// widget you should be able to click as well, it should then google the current time for the
// timezone/place in the prefered search engine"): a clock of another zone "time in <the zone's city>",
// this PC's own "time", which every engine answers with the time where the PC is. The clock's line
// and the clock flyout's header do it once the folder has gone, as a ticker's click does (§263); the
// bar's clock is a folder wearing the clock face, and its click still opens that folder, as Windows'
// clock opens its calendar. Nothing reaches the browser.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Searching;

public sealed class ClockSearchTests
{
    /// <summary>The address: "time in" a place, "time" alone for this PC's, encoded, with the app's engine or Google's where a custom one can't.</summary>
    [Fact]
    public void TheTimeIsSearchedForWhereTheClockStands()
    {
        Assert.Equal("https://www.google.com/search?q=time+in+Tokyo", SearchUrls.Time(new Config(), "Tokyo"));
        Assert.Equal("https://www.google.com/search?q=time", SearchUrls.Time(new Config(), ""));
        Assert.Equal("https://www.google.com/search?q=time", SearchUrls.Time(new Config(), "   "));
        Assert.Equal("https://www.google.com/search?q=time+in+S%C3%A3o+Paulo", SearchUrls.Time(new Config(), " São Paulo "));
        Assert.Equal("https://www.google.com/search?q=time+in+Eastern+Time+%28US+%26+Canada%29", SearchUrls.Time(new Config(), "Eastern Time (US & Canada)"));
        Assert.Equal("https://duckduckgo.com/?q=time+in+Bern", SearchUrls.Time(new Config { SearchEngine = SearchEngine.DuckDuckGo }, "Bern"));
        Assert.Equal("https://www.bing.com/search?q=time", SearchUrls.Time(new Config { SearchEngine = SearchEngine.Bing }, ""));
        Assert.Equal("https://example.com/?q=time+in+Bern", SearchUrls.Time(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "https://example.com/?q={q}" }, "Bern"));
        Assert.Equal("https://www.google.com/search?q=time+in+Bern", SearchUrls.Time(new Config { SearchEngine = SearchEngine.Custom, SearchCustomUrl = "not an address" }, "Bern"));
    }

    /// <summary>A zone's display name read as the city a clock of it stands for: its first place, the name itself where it lists none.</summary>
    [Fact]
    public void AZonesCityIsTheFirstPlaceItNames()
    {
        Assert.Equal("Osaka", Clock.City("(UTC+09:00) Osaka, Sapporo, Tokyo"));
        Assert.Equal("Amsterdam", Clock.City("(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna"));
        Assert.Equal("Coordinated Universal Time", Clock.City("(UTC) Coordinated Universal Time"));
        Assert.Equal("Eastern Time (US & Canada)", Clock.City("(UTC-05:00) Eastern Time (US & Canada)"));
        Assert.Equal("", Clock.City(null));
        Assert.Equal("", Clock.City(""));
    }

    /// <summary>
    /// The clock's line and the clock flyout's header: a click asks the folder to go and forwards (their
    /// lines act now, and light as a whole), and the dock's Open then searches, once — a clock of another
    /// zone its city, this PC's own clock the time here, whatever its label says.
    /// </summary>
    [UiFact]
    public void AClocksClickSearchesItsTimeOnceTheFolderHasGone()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config { SearchEngine = SearchEngine.DuckDuckGo };
            const string tokyo = "Tokyo Standard Time";
            var city = Clock.City(Clock.Read(tokyo, seconds: false).Zone);
            Assert.NotEqual("", city);
            foreach (var (kind, zone, label, url) in new[]
            {
                (ListKind.Clock, tokyo, "", $"https://duckduckgo.com/?q=time+in+{SearchUrls.Encode(city)}"),
                (ListKind.Clock, tokyo, "Mum", $"https://duckduckgo.com/?q=time+in+{SearchUrls.Encode(city)}"),
                (ListKind.Clock, "", "Home", "https://duckduckgo.com/?q=time"),
                (ListKind.Clock, "", "", "https://duckduckgo.com/?q=time"),
                (ListKind.TimeHeader, tokyo, "", $"https://duckduckgo.com/?q=time+in+{SearchUrls.Encode(city)}"),
                (ListKind.TimeHeader, "", "", "https://duckduckgo.com/?q=time"),
            })
            {
                var item = kind.New();
                item.Clock = new ClockOptions { TimeZone = zone, Label = label };
                var cell = new FolderCell { Island = item };
                var opened = new List<string>();
                using var line = (CellIsland)(kind == ListKind.Clock ? new ClockTile("test/clock", item, gfx, config) : new TimeHeaderTile("test/time", item, gfx, config));
                ((IOpensOnceGone)line).Opened = opened.Add;
                var action = line.Click(0);
                Assert.Equal(IslandView.App, action.View);
                Assert.True(FolderFlyout.Forwards(action, FolderFlyout.ActOf(cell, line)), $"{kind} {zone}");
                Assert.True(FolderFlyout.LightsWhole(cell, line), $"{kind}: the whole line is the one thing to press");
                Assert.Empty(opened);
                ((IOpensOnceGone)line).Open();
                ((IOpensOnceGone)line).Open();
                Assert.Equal([url], opened);
            }
        });
    }
}
