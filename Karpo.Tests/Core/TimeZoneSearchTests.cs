// The clock's time-zone box: finding a Windows zone by a place, a name or an offset.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class TimeZoneSearchTests
{
    // A few zones as Windows lists them, in Windows' own order.
    static readonly ZoneEntry[] Zones =
    [
        new("Pacific Standard Time", "(UTC-08:00) Pacific Time (US & Canada)", "Pacific Standard Time", TimeSpan.FromHours(-8)),
        new("Newfoundland Standard Time", "(UTC-03:30) Newfoundland", "Newfoundland Standard Time", new TimeSpan(-3, -30, 0)),
        new("GMT Standard Time", "(UTC+00:00) Dublin, Edinburgh, Lisbon, London", "GMT Standard Time", TimeSpan.Zero),
        new("W. Europe Standard Time", "(UTC+01:00) Amsterdam, Berlin, Bern, Rome, Stockholm, Vienna", "W. Europe Standard Time", TimeSpan.FromHours(1)),
        new("India Standard Time", "(UTC+05:30) Chennai, Kolkata, Mumbai, New Delhi", "India Standard Time", new TimeSpan(5, 30, 0)),
        new("Tokyo Standard Time", "(UTC+09:00) Osaka, Sapporo, Tokyo", "Tokyo Standard Time", TimeSpan.FromHours(9)),
        new("Korea Standard Time", "(UTC+09:00) Seoul", "Korea Standard Time", TimeSpan.FromHours(9)),
    ];

    static string[] Ids(string query) => TimeZoneSearch.Find(Zones, query).Select(zone => zone.Id).ToArray();

    [Fact]
    public void APlaceFindsItsZone()
    {
        Assert.Equal(new[] { "Tokyo Standard Time" }, Ids("tokyo"));
        Assert.Equal(new[] { "W. Europe Standard Time" }, Ids("Bern"));
    }

    [Fact]
    public void EveryWordHasToMatch()
    {
        Assert.Equal(new[] { "India Standard Time" }, Ids("new delhi"));
        Assert.Empty(Ids("tokyo berlin"));
    }

    [Fact]
    public void AnOffsetFindsTheZonesAtIt()
    {
        Assert.Equal(new[] { "Tokyo Standard Time", "Korea Standard Time" }, Ids("+9"));
        Assert.Equal(new[] { "India Standard Time" }, Ids("utc+5:30"));
        Assert.Equal(new[] { "Newfoundland Standard Time" }, Ids("-03:30"));
        Assert.Equal(new[] { "Korea Standard Time" }, Ids("+9 seoul"));
    }

    [Fact]
    public void AZoneWithAPlaceStartingWithTheWordComesFirst()
    {
        // "a" is in every zone ("Standard"), but only Amsterdam starts with it.
        var found = Ids("a");

        Assert.Equal(Zones.Length, found.Length);
        Assert.Equal("W. Europe Standard Time", found[0]);
        Assert.Equal("Pacific Standard Time", found[1]);
    }

    [Fact]
    public void AccentsNeedNotBeTyped()
    {
        ZoneEntry[] zones = [new("Test Zone", "(UTC+01:00) Zürich, Genève", "Test Zone", TimeSpan.FromHours(1))];

        Assert.Single(TimeZoneSearch.Find(zones, "zurich"));
        Assert.Single(TimeZoneSearch.Find(zones, "GENEVE"));
    }

    [Fact]
    public void NothingTypedFindsNothing()
    {
        Assert.Empty(Ids(""));
        Assert.Empty(Ids("   "));
    }

    [Fact]
    public void TheResultStopsAtTheLimit()
    {
        Assert.Equal(2, TimeZoneSearch.Find(Zones, "standard", limit: 2).Count);
    }

    [Theory]
    [InlineData("+9", 9, 0)]
    [InlineData("-05:30", -5, -30)]
    [InlineData("utc+1", 1, 0)]
    [InlineData("GMT-3", -3, 0)]
    [InlineData("+14", 14, 0)]
    public void OffsetsAreRead(string word, int hours, int minutes)
    {
        Assert.Equal(new TimeSpan(hours, minutes, 0), TimeZoneSearch.Offset(word));
    }

    [Theory]
    [InlineData("9")]
    [InlineData("+15")]
    [InlineData("+9:60")]
    [InlineData("+")]
    [InlineData("tokyo")]
    [InlineData("+9:30:00")]
    public void OtherWordsAreNoOffset(string word)
    {
        Assert.Null(TimeZoneSearch.Offset(word));
    }

    [Fact]
    public void PlacesComeFromTheDisplayName()
    {
        Assert.Equal(new[] { "Osaka", "Sapporo", "Tokyo" }, TimeZoneSearch.Places("(UTC+09:00) Osaka, Sapporo, Tokyo"));
        Assert.Equal(new[] { "Coordinated Universal Time" }, TimeZoneSearch.Places("Coordinated Universal Time"));
    }
}
