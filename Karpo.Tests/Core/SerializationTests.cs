using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

/// <summary>What the file looks like: readable, without keys that say nothing.</summary>
public sealed class SerializationTests
{
    /// <summary>Regression: every island was written with <c>"pins": null</c>.</summary>
    [Fact]
    public void AnIslandIsWrittenWithOnlyItsOwnOptions()
    {
        var config = Configs.With(BarKind.ShowDesktop.New(), Start.New(), Pin.New());

        var islands = Configs.IslandKeys(config);

        // Its section always, as a word: where a widget stands is its section and its place in the
        // list. No size: a taskbar entry takes the room it needs, and has no width of its own to write
        // (WidgetBounds). No row (§488: the Top and bottom's top row went).
        Assert.Equal(["id", "kind", "section"], islands[0]);
        Assert.Equal(["id", "kind", "section"], islands[1]);
        Assert.Equal(["id", "kind", "section", "pin"], islands[2]);
    }

    /// <summary>
    /// Regression: the bell's count was off unless switched on. The bell is the notifications
    /// preset's face now (§100), and its count is on unless the face is set off, through the file.
    /// </summary>
    [Fact]
    public void TheBellShowsItsCountByDefault()
    {
        var bell = Presets.Folder(Presets.Find(Presets.NotificationsId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        Assert.True(NotificationsFace.ShowsCount(bell.Folder!.Face));
        bell.Folder.Face.Set(FolderFaces.Notifications.Option(NotificationsFace.Count)!, false);
        var json = System.Text.Json.JsonSerializer.Serialize(bell.Folder.Face, ConfigJson.Default.FolderFace);
        Assert.False(NotificationsFace.ShowsCount(System.Text.Json.JsonSerializer.Deserialize(json, ConfigJson.Default.FolderFace)!));
    }

    [Fact]
    public void CloneKeepsIdsAndCopyMakesANewOne()
    {
        var island = BarKind.Pin.New();
        island.Pin!.Target = "OSLO";

        var clone = island.Clone();
        var copy = island.Copy();

        Assert.Equal(island.Id, clone.Id);
        Assert.NotEqual(island.Id, copy.Id);
        Assert.Equal("OSLO", copy.Pin!.Target);
        Assert.True(island.SameOptions(copy));
        copy.Pin.Target = "BERGEN";
        Assert.False(island.SameOptions(copy));
        Assert.Equal("OSLO", island.Pin!.Target);
    }

    [Fact]
    public void ASavedConfigReadsBackTheSame()
    {
        var config = Configs.Repaired("""{ "layouts": [ { "id": "L", "type": "dock", "size": 40, "show": "float", "islands": [ { "id": "c", "kind": "volume", "section": "middle", "volume": { "length": 77 } } ] } ], "screens": [ { "id": "D", "activeLayoutId": "L", "edge": "top" } ] }""");

        var again = Configs.RoundTrip(config, out var changed);

        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
        Assert.Equal(EdgeMode.Float, again.EdgesOf("D").Top.Mode);
        Assert.Equal(40, again.EdgesOf("D").Top.Size);
        Assert.Equal(BarStyle.Floating, again.EdgesOf("D").Top.Style);
        Assert.Equal(BarSection.Middle, Configs.Islands(again)[0].Section);
        Assert.Equal(77, Configs.Islands(again)[0].Volume!.Length);
    }
}
