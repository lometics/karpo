// quick: the brightness widget's one setting, which displays its slider dims (BrightnessOptions):
// every display, which is no options at all and writes nothing, or the one it is on. Held here: the
// file reads and writes it, a widget nobody set writes nothing, a value that means every display is
// dropped, a list's line keeps it as the taskbar's entry does, and two saves that each changed one
// widget both land (ConfigMerge).

using System.Text.Json.Nodes;

namespace Karpo.Tests.Core;

public sealed class BrightnessOptionsTests
{
    private const string OwnFile = """{ "layouts": [ { "id": "L", "islands": [ { "id": "b", "kind": "brightness", "brightness": { "screens": "own" } } ] } ] }""";

    /// <summary>A widget nobody set sets every display, and its file says nothing of it.</summary>
    [Fact]
    public void AWidgetNobodySetDimsEveryDisplayAndWritesNothing()
    {
        var config = Configs.With(BarKind.Brightness.New());
        var island = Assert.Single(Configs.Islands(config));
        Assert.Null(island.Brightness);
        Assert.Equal(["id", "kind", "section"], Configs.IslandKeys(config)[0]);

        // A file from before the setting reads as every display, and writes back as it was.
        var old = Configs.Repaired("""{ "layouts": [ { "id": "L", "islands": [ { "id": "b", "kind": "brightness" } ] } ] }""");
        Assert.Null(Configs.Islands(old)[0].Brightness);
        Configs.RoundTrip(old, out var changed);
        Assert.False(changed);
    }

    /// <summary>"Only the screen it's on" is written, read back the same, and survives a clone.</summary>
    [Fact]
    public void OnlyItsOwnScreenRoundTrips()
    {
        var config = Configs.Repaired(OwnFile);
        var island = Configs.Islands(config)[0];
        Assert.Equal(BrightnessScreens.Own, island.Brightness?.Screens);

        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
        Assert.Equal(BrightnessScreens.Own, Configs.Islands(again)[0].Brightness?.Screens);
        Assert.Contains("\"screens\"", config.ToJson());
        Assert.Contains("\"own\"", config.ToJson());

        Assert.Equal(BrightnessScreens.Own, island.Clone().Brightness?.Screens);
        Assert.Equal(BrightnessScreens.Own, island.Copy().Brightness?.Screens);
    }

    /// <summary>Every display written out is the same as nothing written: the options are dropped.</summary>
    [Fact]
    public void EveryDisplayIsNoOptions()
    {
        var island = BarKind.Brightness.New();
        island.Brightness = new BrightnessOptions { Screens = BrightnessScreens.All };
        island.Normalize();
        Assert.Null(island.Brightness);

        var config = Configs.Repaired("""{ "layouts": [ { "id": "L", "islands": [ { "id": "b", "kind": "brightness", "brightness": { "screens": "all" } } ] } ] }""");
        Assert.Null(Configs.Islands(config)[0].Brightness);
        Assert.DoesNotContain("\"brightness\":", config.ToJson());
    }

    /// <summary>A widget of another kind never carries the brightness's options, as no widget carries another's.</summary>
    [Fact]
    public void AnotherKindDropsThem()
    {
        var volume = BarKind.Volume.New();
        volume.Brightness = new BrightnessOptions { Screens = BrightnessScreens.Own };
        volume.Normalize();
        Assert.Null(volume.Brightness);
    }

    /// <summary>
    /// The setting is the widget's wherever it stands: a line of a list carries it through its own
    /// normalizing, as the taskbar's entry does.
    /// </summary>
    [Fact]
    public void ALineOfAListKeepsIt()
    {
        var line = ListKind.Brightness.New();
        line.Brightness = new BrightnessOptions { Screens = BrightnessScreens.Own };
        line.Normalize();
        Assert.Equal(BrightnessScreens.Own, line.Brightness?.Screens);
    }

    /// <summary>
    /// The settings app chose "Only the screen it's on" while the dock moved the same widget: the
    /// three-way merge keeps both, and a choice of every display on one side (the key gone) against
    /// nothing changed on the other leaves it gone.
    /// </summary>
    [Fact]
    public void TheMergeKeepsTheChoiceBesideTheOtherSidesEdit()
    {
        const string before = "{'islands':[{'id':'b','kind':'brightness','atLeast':2}]}";
        const string chose = "{'islands':[{'id':'b','kind':'brightness','atLeast':2,'brightness':{'screens':'own'}}]}";
        const string resized = "{'islands':[{'id':'b','kind':'brightness','atLeast':3}]}";
        Assert.Equal(
            Json("{'islands':[{'id':'b','kind':'brightness','atLeast':3,'brightness':{'screens':'own'}}]}"),
            Merge(before, chose, resized));
        Assert.Equal(
            Json("{'islands':[{'id':'b','kind':'brightness','atLeast':3,'brightness':{'screens':'own'}}]}"),
            Merge(before, resized, chose));

        // Back to every display on one side, the other untouched: the key goes.
        Assert.Equal(Json(before), Merge(chose, before, chose));
    }

    private static string Merge(string @base, string ours, string theirs) =>
        ConfigMerge.Merge(Json(@base), Json(ours), Json(theirs));

    private static string Json(string text) => JsonNode.Parse(text.Replace('\'', '"'))!.ToJsonString();
}
