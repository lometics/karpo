// Pinning by dropping: what was dropped goes in where it was dropped, in
// its order, and nothing is pinned twice (PinList).

using Karpo.Dock;

// Not Karpo.Tests.Desktops: the core tests name BarKind.Desktops as "Desktops" (using static),
// which a namespace of that name would shadow.
namespace Karpo.Tests.DesktopsArea;

public sealed class PinListTests
{
    [Fact]
    public void DroppedGoInAtThePositionInTheirOrder()
    {
        var pins = PinList.Insert(["a", "d"], ["b", "c"], 1);

        Assert.Equal(new[] { "a", "b", "c", "d" }, pins);
    }

    [Fact]
    public void APinAlreadyThereIsNotPinnedTwice()
    {
        // Regression: a drop onto a pins island added whatever was dropped,
        // however often it was pinned already.
        var pins = PinList.Insert([@"C:\Tools\app.exe", "https://example.com"], [@"c:\tools\APP.exe", "https://example.com/", "b"], 0);

        Assert.Equal(new[] { "b", @"C:\Tools\app.exe", "https://example.com" }, pins);
    }

    [Fact]
    public void AFolderWithOrWithoutItsTrailingSlashIsOnePin()
    {
        Assert.True(PinList.Same(@"C:\Users\me\Documents\", @"c:\users\me\documents"));
        Assert.True(PinList.Same("https://example.com/", "HTTPS://EXAMPLE.COM"));
        Assert.False(PinList.Same(@"C:\", @"C:"));
        Assert.False(PinList.Same("https://example.com/a", "https://example.com/b"));
        Assert.False(PinList.Same(null, "a"));
    }

    [Fact]
    public void DroppedTwiceInOneDropIsPinnedOnce()
    {
        var pins = PinList.Insert([], [@"C:\Tools", @"c:\tools", @"C:\Tools\"], 0);

        Assert.Equal(new[] { @"C:\Tools" }, pins);
    }

    [Fact]
    public void DividersStayAndAreNeverDropped()
    {
        var pins = PinList.Insert(["a", "|", "b", "|"], ["|", "c", " "], 4);

        Assert.Equal(new[] { "a", "|", "b", "|", "c" }, pins);
    }

    [Theory]
    [InlineData(-5, new[] { "x", "a", "b" })]
    [InlineData(99, new[] { "a", "b", "x" })]
    public void APositionOutsideTheListIsClamped(int position, string[] expected) =>
        Assert.Equal(expected, PinList.Insert(["a", "b"], ["x"], position));
}
