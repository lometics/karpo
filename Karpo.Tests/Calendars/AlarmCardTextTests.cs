// When an alarm rings, as Windows' Clock app says it (Karpo.Dock\Clock\AlarmCardText.cs, docs\widgets.md §187,
// §196): "Rings in …".

using Karpo.Dock;

namespace Karpo.Tests.Calendars;

public sealed class AlarmCardTextTests
{
    /// <summary>A minute begun counts whole; days and hours from a day up, hours and minutes under it; never under a minute.</summary>
    [Fact]
    public void ItSaysWhenItRings()
    {
        Assert.Equal("Rings in 8 h 23 min", AlarmCardText.RingsIn(new TimeSpan(8, 22, 1)));
        Assert.Equal("Rings in 1 min", AlarmCardText.RingsIn(TimeSpan.FromSeconds(5)));
        Assert.Equal("Rings in 1 min", AlarmCardText.RingsIn(TimeSpan.Zero));
        Assert.Equal("Rings in 2 h", AlarmCardText.RingsIn(TimeSpan.FromHours(2)));
        Assert.Equal("Rings in 45 min", AlarmCardText.RingsIn(TimeSpan.FromMinutes(45)));
        Assert.Equal("Rings in 1 d", AlarmCardText.RingsIn(TimeSpan.FromHours(24)));
        Assert.Equal("Rings in 6 d 23 h", AlarmCardText.RingsIn(new TimeSpan(6, 22, 59, 30)));
    }
}
