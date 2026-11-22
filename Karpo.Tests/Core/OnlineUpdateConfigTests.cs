// The one online interval in the file (Config.OnlineUpdateMinutes). The person, 2026-10-05: "there
// should be only one counter for online updates".

namespace Karpo.Tests.Core;

public sealed class OnlineUpdateConfigTests
{
    /// <summary>A file of one layout holding <paramref name="islands"/>, nothing more.</summary>
    private static string File(string islands) =>
        $$"""{ "layouts": [ { "id": "default", "name": "Default", "islands": {{islands}} } ], "defaultLayoutId": "default" }""";

    [Fact]
    public void ANewConfigWritesTheDefaultInterval()
    {
        var json = new Config().ToJson();

        Assert.Contains("\"onlineUpdateMinutes\": 15", json, StringComparison.Ordinal);
        Assert.Equal(15, Config.Parse(json).OnlineUpdateMinutes);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(180)]
    // A value none of the choices is: kept as it is, as the box shows it as its placeholder.
    [InlineData(45)]
    public void AnIntervalGoesThroughTheFileAsItIs(int minutes)
    {
        var read = Configs.RoundTrip(new Config { OnlineUpdateMinutes = minutes }, out _);

        Assert.Equal(minutes, read.OnlineUpdateMinutes);
        Assert.Equal(minutes, read.Clone().OnlineUpdateMinutes);
    }

    /// <summary>A hand edit past either end is held to it, as the bar's height and opacity are: a clamp, not a change to save.</summary>
    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, 5)]
    [InlineData(10_000, 180)]
    public void AnIntervalPastEitherEndIsHeldToIt(int written, int held)
    {
        var text = File("[ ]").Replace("{ \"layouts\"", $"{{ \"onlineUpdateMinutes\": {written}, \"layouts\"", StringComparison.Ordinal);
        Configs.Repaired(File("[ ]"), out var cleanChanged);

        var config = Configs.Repaired(text, out var changed);

        Assert.Equal(held, config.OnlineUpdateMinutes);
        // Whatever else Repair fills in for so short a file, the clamp adds nothing to it.
        Assert.Equal(cleanChanged, changed);
    }

    // --- Two apps at once -------------------------------------------------------------------------

    [Fact]
    public void AnIntervalChosenInOneAppSurvivesTheOtherAppsSave()
    {
        var @base = new Config().ToJson();
        // The settings app chose an hour; the dock, meanwhile, saved a taller bar.
        var settings = new Config { OnlineUpdateMinutes = 60 }.ToJson();
        var dock = LookShorthand.Wearing(taskbarOpacity: 60).ToJson();

        foreach (var merged in new[] { ConfigMerge.Merge(@base, dock, settings), ConfigMerge.Merge(@base, settings, dock) })
        {
            var read = Config.Parse(merged);
            Assert.Equal(60, read.OnlineUpdateMinutes);
            Assert.Equal(60, read.TaskbarOpacity);
        }
    }
}
