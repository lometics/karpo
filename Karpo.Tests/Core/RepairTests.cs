using static Karpo.Core.BarKind;

namespace Karpo.Tests.Core;

/// <summary>What <see cref="Config.Repair"/> guarantees, whatever the file said.</summary>
public sealed class RepairTests
{
    /// <summary>A file as Karpo writes it now: nothing to repair.</summary>
    private const string Clean = """
        {
          "layouts": [
            { "id": "default", "name": "Default", "icon": "D", "islands": [
              { "id": "a1b2c3d4", "kind": "start", "edge": "bottom" },
              { "id": "0000000c", "kind": "touchKeyboard", "edge": "bottom" },
              { "id": "0000000d", "kind": "touchKeyboard", "edge": "bottom" },
              { "id": "0000000e", "kind": "pin", "edge": "bottom",  "pin": { "target": "C:\\Tools", "name": "", "icon": "" } },
              { "id": "00000013", "kind": "folderList", "edge": "bottom",  "folder": { "lists": [ [
                { "island": { "id": "00000014", "kind": "pin",  "pin": { "target": "C:\\Tools\\a.exe", "name": "", "icon": "" } } } ] ] } },
              { "id": "0000000f", "kind": "folderList", "edge": "bottom",  "folder": { "lists": [ [
                { "island": { "id": "00000010", "kind": "weatherNow",  "weather": { "place": "", "useDeviceLocation": false } } },
                { "island": { "id": "00000011", "kind": "battery" } } ] ] } }
            ] },
            { "id": "L1", "name": "Work", "icon": "W", "islands": [ { "id": "00000015", "kind": "touchKeyboard", "edge": "bottom" } ] },
            { "id": "L2", "name": "Games", "icon": "G", "islands": [] },
            { "id": "display-1", "name": "Dell", "icon": "M", "islands": [ { "id": "00000016", "kind": "tray", "edge": "bottom" } ] }
          ],
          "defaultLayoutId": "default",
          "style": "islands",
          "screens": [
            { "id": "D1", "name": "Dell", "activeLayoutId": "L2", "desktopLayouts": { "desk-a": "L1" } }
          ]
        }
        """;

    /// <summary>A file whose one layout, "Default", holds <paramref name="islands"/> (a JSON array).</summary>
    private static string OnABar(string islands) => $$"""{ "layouts": [ { "id": "default", "name": "Default", "islands": {{islands}} } ] }""";

    [Fact]
    public void ACleanFileNeedsNothing()
    {
        Configs.Repaired(Clean, out var changed);

        Assert.False(changed);
    }

    [Fact]
    public void RepairIsStableOnItsOwnOutput()
    {
        foreach (var messy in (string[])[
            OnABar("""[ { "kind": "touchKeyboard" }, { "id": "x", "kind": "touchKeyboard" }, { "id": "X", "kind": "start" }, { "id": "y", "kind": "start" } ]"""),
            """{ "height": 7, "opacity": 900, "hide": "sometimes", "screenTime": { "idleMinutes": 0 } }""",
            """{ "layouts": [ { "name": "" }, { "id": "a/b" } ], "screens": [ { "id": "D", "activeLayoutId": "gone" } ], "defaultLayoutId": "gone" }""",
            OnABar("""[ null, { "id": "p", "kind": "pin", "pin": { "target": " " } }, { "id": "h", "kind": "hologram" } ]"""),
            OnABar("""[ { "id": "f", "kind": "folderList",  "folder": { "lists": [ [ { "island": { "kind": "clock" } }, { "island": { "id": "f", "kind": "drives" } } ] ] } } ]"""),
            Clean,
        ])
        {
            var config = Configs.Repaired(messy);
            var again = Configs.RoundTrip(config, out var changed);

            Assert.False(changed, messy);
            Assert.Equal(config.ToJson(), again.ToJson());
        }
    }

    [Fact]
    public void IdsAreUniqueWithinAList()
    {
        var config = Configs.Repaired(OnABar("""
            [
              { "id": "a", "kind": "touchKeyboard" },
              { "id": "A", "kind": "touchKeyboard" },
              { "id": "", "kind": "widgetsBoard" },
              { "kind": "widgetsBoard" },
              { "id": "b", "kind": "energySaver" } ]
            """), out var changed);

        Assert.True(changed);
        var ids = Configs.Islands(config).Select(island => island.Id).ToList();
        Assert.Equal(5, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal("a", ids[0]);
        Assert.Equal("b", ids[4]);
        Assert.All(ids.Skip(1).Take(3), id => Assert.Matches("^[0-9a-f]{8}$", id));
    }

    /// <summary>
    /// An id stands in one layout only, not once in each: the dock keeps a timer's countdown and an
    /// alarm's ring by the id alone, whatever layout it is in (WidgetCopiesTests). Of the same id in
    /// three layouts the first keeps it, the later ones are given new ones.
    /// </summary>
    [Fact]
    public void AnIdStandsInOneLayoutOnlyTheFirstKeepingIt()
    {
        var config = Configs.Repaired(Clean.Replace("\"00000015\"", "\"0000000c\"").Replace("\"00000016\"", "\"0000000c\""), out var changed);

        Assert.True(changed);
        Assert.Equal("0000000c", config.Layouts[0].Islands[1].Id);
        Assert.NotEqual("0000000c", config.Layouts[1].Islands[0].Id);
        Assert.NotEqual("0000000c", config.Layouts[3].Islands[0].Id);
        Assert.NotEqual(config.Layouts[1].Islands[0].Id, config.Layouts[3].Islands[0].Id);
    }

    [Fact]
    public void ASecondOfAKindIsKept()
    {
        var config = Configs.Repaired(OnABar("""
            [
              { "id": "s1", "kind": "start" },
              { "id": "n1", "kind": "touchKeyboard" },
              { "id": "s2", "kind": "start" },
              { "id": "c1", "kind": "touchKeyboard" },
              { "id": "n2", "kind": "touchKeyboard" },
              { "id": "c2", "kind": "touchKeyboard" },
              { "id": "t1", "kind": "tray" },
              { "id": "t2", "kind": "tray" } ]
            """), out var changed);

        // Nothing was thrown away: a second of a kind is no repair (the file is written back for the look it now names).
        Assert.True(changed);
        Assert.Equal(["s1", "n1", "s2", "c1", "n2", "c2", "t1", "t2"], Configs.Islands(config).Select(island => island.Id));
    }

    [Fact]
    public void EachIslandHasExactlyItsKindsOptions()
    {
        var config = Configs.Repaired(OnABar("""
            [
              { "id": "c", "kind": "touchKeyboard",  "weather": { "place": "Oslo" }, "pin": { "target": "C:\\Tools" }, "folder": { "columns": 2 } },
              { "id": "n", "kind": "touchKeyboard" },
              { "id": "f", "kind": "folderList" } ]
            """));

        var list = Configs.Islands(config);
        var clock = list[0];
        Assert.Null(clock.Calendar);
        Assert.Null(clock.Weather);
        Assert.Null(clock.Pin);
        Assert.Null(clock.Folder);
        Assert.Null(list[1].Weather);
        // A folder with no options in the file gets the shape a new folder gets — asked of the
        // defaults rather than written here as a number, so a change of mind about what a new
        // folder is does not have to be made in two places.
        var fresh = new FolderOptions();
        Assert.Equal((fresh.Widths?.Count, fresh.Lists.Count, fresh.Cells.Count), (list[2].Folder!.Widths?.Count, list[2].Folder!.Lists.Count, list[2].Folder!.Cells.Count));
    }

    [Fact]
    public void ValuesAreClampedWithoutCountingAsAChange()
    {
        var config = Configs.Repaired("""
            {
              "screenTime": { "idleMinutes": 0 },
              "layouts": [ { "id": "default", "name": "Default", "icon": "D", "size": 500, "islands": [
                { "id": "a", "kind": "energySaver", "edge": "bottom" },
                { "id": "b", "kind": "folderList", "edge": "bottom", "folder": { "lists": [ [ { "island": { "id": "bw", "kind": "weatherNow", "weather": { "place": "Zurich" } } } ] ] } },
                { "id": "c", "kind": "folderList", "edge": "bottom", "folder": { "lists": [ [ { "island": { "id": "cc", "kind": "calendar", "calendar": { "daysAhead": 99, "refreshMinutes": 999 } } } ] ] } } ] } ],
              "defaultLayoutId": "default"
            }
            """, out var changed);

        Assert.False(changed);
        Assert.Equal(Config.MaxHeight, config.DefaultLayout.Size);
        // Screen time's idle time went with screen time (2026-09-26): an older file's is read past,
        // counts as no change, and is not written back.
        Assert.DoesNotContain("screenTime", config.ToJson(), StringComparison.Ordinal);
        var list = Configs.Islands(config);
        Assert.Equal("Zurich", list[1].Folder!.Cells[0].Island.Weather!.Place);
        // §496: how far ahead the agenda lists, held to its most (60 days), counting as no change.
        Assert.Equal(CalendarOptions.DaysAheadMost, list[2].Folder!.Cells[0].Island.Calendar!.DaysAhead);
        // The feed's refresh is retired: nothing reads it, so Repair leaves it exactly as the
        // file has it rather than rewriting somebody's file over a dead value.
        Assert.Equal(999, list[2].Folder!.Cells[0].Island.Calendar!.RefreshMinutes);
    }

    [Theory]
    [InlineData(0, 48)]
    [InlineData(10, Config.MinHeight)]
    [InlineData(48, 48)]
    [InlineData(1000, Config.MaxHeight)]
    public void ALayoutsSizeStaysInRange(int height, int expected)
    {
        Assert.Equal(expected, Configs.Repaired($$"""{ "layouts": [ { "id": "L", "size": {{height}} } ] }""").Layout("L")!.Size);
    }

    /// <summary>
    /// A bar holds taskbar entries (BarEntries) and nothing else: a folder's piece typed onto a bar
    /// by hand goes when the file is read, and the file says it changed, so it is written back once.
    /// </summary>
    [Fact]
    public void ABarKeepsOnlyTaskbarEntries()
    {
        var config = Configs.Repaired(OnABar("""
            [ { "id": "a", "kind": "start" }, { "id": "b", "kind": "weather" }, { "id": "c", "kind": "soundOutput" }, { "id": "d", "kind": "touchKeyboard" } ]
            """), out var changed);

        Assert.True(changed);
        Assert.Equal(new[] { Start, BarKind.TouchKeyboard }, Configs.Islands(config).Select(island => island.Kind));
        Configs.RoundTrip(config, out var again);
        Assert.False(again);
    }

    [Fact]
    public void NullEntriesGo()
    {
        var config = Configs.Repaired("""
            { "layouts": [ null, { "id": "L", "islands": [ null, { "id": "b", "kind": "tray" } ] } ], "screens": [ null ] }
            """, out var changed);

        Assert.True(changed);
        Assert.Single(config.Layouts);
        Assert.Single(Configs.Islands(config));
        Assert.Empty(config.Screens);
    }

    [Fact]
    public void ReferencesToLayoutsThatAreGoneGo()
    {
        var config = Configs.Repaired("""
            {
              "layouts": [ { "id": "L1", "name": "Work", "islands": [] } ],
              "screens": [ { "id": "D1", "activeLayoutId": "gone" } ],
              "defaultLayoutId": "gone2"
            }
            """, out var changed);

        Assert.True(changed);
        var screen = config.Screens.Single();
        Assert.Equal("L1", screen.ActiveLayoutId);
        // New displays get a layout that exists.
        Assert.Equal("L1", config.DefaultLayoutId);
    }

    [Fact]
    public void LayoutIdsAreUniqueAndCantBreakIslandKeys()
    {
        var config = Configs.Repaired("""
            { "layouts": [ { "id": "a/b" }, { "id": "x" }, { "id": "x" }, { "name": "  Games " } ] }
            """, out var changed);

        Assert.True(changed);
        var ids = config.Layouts.Select(layout => layout.Id).ToList();
        Assert.Equal(4, ids.Distinct().Count());
        Assert.DoesNotContain(ids, id => id.Contains('/'));
        Assert.Equal("x", ids[1]);
        Assert.Equal("Games", config.Layouts[3].Name);
        Assert.Equal("New layout", config.Layouts[0].Name);
    }

    [Fact]
    public void DisplaysWithoutAnIdOrTwiceGo()
    {
        var config = Configs.Repaired("""
            { "screens": [ { "id": "" }, { "id": "D1", "enabled": false }, { "id": "d1", "enabled": true } ] }
            """, out var changed);

        Assert.True(changed);
        Assert.False(config.Screens.Single().Enabled);
    }

    /// <summary>A widget's id is unique among its layout's and its folders', so "folder/widget" finds one.</summary>
    [Fact]
    public void IdsInFoldersAreUniqueAcrossTheLayout()
    {
        var config = Configs.Repaired(OnABar("""
            [
              { "id": "w", "kind": "widgetsBoard" },
              { "id": "f", "kind": "folderList",  "folder": { "lists": [ [
                { "island": { "id": "w", "kind": "clock" } },
                { "island": { "id": "F", "kind": "clock" } },
                { "island": { "id": "a/b", "kind": "media" } } ] ] } },
              { "id": "g", "kind": "folderList",  "folder": { "lists": [ [ { "island": { "id": "w", "kind": "battery" } } ] ] } } ]
            """), out var changed);

        Assert.True(changed);
        var all = Karpo.Core.Folders.All(Configs.Islands(config)).Select(entry => entry.Island.Id).ToList();
        Assert.Equal(all.Count, all.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.DoesNotContain(all, id => id.Contains('/'));
        Assert.Equal("w", Configs.Islands(config)[0].Id);
    }

    [Fact]
    public void AFileWithoutLayoutsGetsTheDefaultArrangement()
    {
        var config = Configs.Repaired("""{ "onlineUpdateMinutes": 60 }""", out var changed);

        // Made up here, so written back: the other app would make up other ids.
        Assert.True(changed);
        Assert.Contains(Configs.Islands(config), island => island.Kind == Start);
        Assert.Equal(60, config.OnlineUpdateMinutes);
    }

    [Fact]
    public void APinsOptionsAreTrimmedAndOnlyItsOwn()
    {
        var config = Configs.Repaired(OnABar("""
            [ { "id": "p", "kind": "pin", "pin": { "target": "  C:\\a.exe ", "name": " Mail ", "icon": null }, "calendar": { "timeZone": "x" } } ]
            """));

        var pin = Configs.Islands(config)[0];
        Assert.Equal(("C:\\a.exe", "Mail", ""), (pin.Pin!.Target, pin.Pin.Name, pin.Pin.Icon));
        Assert.Null(pin.Clock);
        Assert.False(pin.Pin.IconIsGlyph);
        pin.Pin.Icon = "\uE8A5";
        Assert.True(pin.Pin.IconIsGlyph);
    }
}
