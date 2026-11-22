// A time zone said short (Karpo.Core\TimeZoneText.cs, docs\widgets.md §230): its offset in force on
// the day, whole hours bare, minutes after a colon, a true minus sign, "UTC" for none; this PC's for no
// id, the id itself for one Windows doesn't know.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class TimeZoneTextTests
{
    private static readonly DateTimeOffset Winter = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Summer = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 0, "UTC")]
    [InlineData(1, 0, "UTC+1")]
    [InlineData(14, 0, "UTC+14")]
    [InlineData(5, 30, "UTC+5:30")]
    [InlineData(5, 45, "UTC+5:45")]
    [InlineData(-3, 0, "UTC−3")]
    [InlineData(-3, -30, "UTC−3:30")]
    [InlineData(-12, 0, "UTC−12")]
    public void AnOffsetIsSaidShort(int hours, int minutes, string said) =>
        Assert.Equal(said, TimeZoneText.Short(new TimeSpan(hours, minutes, 0)));

    /// <summary>The offset in force on the day: Zürich's summer time, India's half hour, Brasília's minus, the UTC zone's none.</summary>
    [Theory]
    [InlineData("W. Europe Standard Time", "UTC+1", "UTC+2")]
    [InlineData("India Standard Time", "UTC+5:30", "UTC+5:30")]
    [InlineData("Newfoundland Standard Time", "UTC−3:30", "UTC−2:30")]
    [InlineData("E. South America Standard Time", "UTC−3", "UTC−3")]
    [InlineData("UTC", "UTC", "UTC")]
    [InlineData("Tokyo Standard Time", "UTC+9", "UTC+9")]
    public void AZoneSaysItsOffsetOnTheDay(string id, string winter, string summer) =>
        Assert.Equal((winter, summer), (TimeZoneText.Short(id, Winter), TimeZoneText.Short(id, Summer)));

    [Fact]
    public void NoIdIsThisPcsAndAnUnknownOneItself()
    {
        Assert.Equal(TimeZoneText.Short(TimeZoneInfo.Local, Summer), TimeZoneText.Short("", Summer));
        Assert.Equal(TimeZoneText.Short(TimeZoneInfo.Local, Summer), TimeZoneText.Short((string?)null, Summer));
        Assert.Equal("Nowhere Standard Time", TimeZoneText.Short("Nowhere Standard Time", Summer));
    }
}
