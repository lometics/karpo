// The presets are the person's own (2026-10-11: "ah and yes i changed the layouts on the layout page
// already. so take them over to the presets"). PersonsLayouts.json is their config.json's two layouts
// as they arranged them by hand — "All ready-made folders" (PresetLayout) and "Default" — with what
// is only theirs taken out: their pinned apps, and the share, the feed and the drives they chose,
// which a preset leaves for its own "Set up in Settings". These tests hold the presets, the
// premade folders, the preset layout and a new bar's folders to that file; a new bar's
// arrangement is Windows 11's since 2026-10-13 (DefaultBarTests), no longer their "Default". Plain code: no window.
// The Sound folder in both is the one-column design the person rearranged on their main bar
// (2026-10-11, "take the new layout of the volume ready made folder from my main taskbar").

using System.Reflection;
using System.Text.Json.Nodes;

namespace Karpo.Tests.Core;

public sealed class PersonsPresetsTests
{
    /// <summary>The person's PC as the file has it: no battery, one fixed drive.</summary>
    private static readonly PresetMachine Theirs = new(
        HasBattery: false,
        FixedDrives: [new DriveVolume(@"\\?\Volume{00000000-0000-0000-0000-000000000001}\", "C:", "Local Disk", DriveKind.Fixed, 0, 0)]);

    private static string Fixture()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Karpo.Tests.Core.PersonsLayouts.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>The person's layout <paramref name="id"/>, read and repaired as the apps read it.</summary>
    private static TaskbarLayout Layout(string id) => Configs.Repaired(Fixture()).Layout(id)!;

    /// <summary>
    /// The preset the person's folder <paramref name="folder"/> of their "All ready-made folders" is: the one
    /// its name in their file is the title of. A folder keeps no name (§427); their file, written before,
    /// still says it, and this reads it there.
    /// </summary>
    private static Preset? PresetOf(BarItem folder) =>
        JsonNode.Parse(Fixture())!["layouts"]!.AsArray().Single(layout => (string?)layout!["id"] == PresetLayout.Id)!["islands"]!.AsArray()
            .Single(island => (string?)island!["id"] == folder.Id)!["folder"]!["name"]?.GetValue<string>() is { } name
            ? Presets.All.SingleOrDefault(preset => preset.Title == name)
            : null;

    /// <summary>A folder as the file writes it, without the ids, which are each folder's own.</summary>
    private static JsonNode Written(BarItem folder)
    {
        var node = JsonNode.Parse(Configs.With(folder).ToJson())!["layouts"]![0]!["islands"]![0]!["folder"]!;
        Strip(node);
        return node;
    }

    private static void Strip(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                obj.Remove("id");
                // Every list medium is what no widths say.
                if (obj["widths"] is JsonArray widths && widths.All(width => (string?)width == "medium"))
                {
                    obj.Remove("widths");
                }
                foreach (var (_, child) in obj)
                {
                    Strip(child);
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Strip(child);
                }
                break;
        }
    }

    /// <summary>
    /// The presets the person asked to change again after arranging them (their own tests hold
    /// those changes): left out of the line-for-line comparison, and nothing else.
    /// </summary>
    // systemGroup: "for the widget that shows the multiple system widgets like cpu, ram etc, add more
    // details and also make a divider in between in the default layout" (2026-10-11): its readings
    // at three rows, a card each in two lists (PresetsTests, PerformancePresetTests).
    // Not here: media, the watchlist, the stock and power are theirs again (2026-10-11: "update the
    // second layout, i did some changes, take mine in the: now playing widget, stocks widget, stock
    // widget, power options widget").
    // screenTimeGroup: "add a screen time donut chart as well" (2026-10-12): the ring between the
    // total and the apps, the apps six rows (ScreenTimeLinesTests.TheReadyMadeFolderFixesItsLines).
    // cpuGroup: "for the processor add top running processes by cpu as a widget as well" (2026-10-12):
    // its top processes over the footer (ProcessesLineTests).
    // stockGroup: "market and stocks group preset should be merged as they are basically the same" (§648): their Stocks'
    // shares under the Markets face (MarketPresetTests). Their file's two folders are one, named Markets: its Markets
    // folder of one line with no symbol is left out of it.
    private static readonly HashSet<string> AskedForSince = ["systemGroup", "screenTimeGroup", Presets.CpuId, Presets.MarketId];

    /// <summary>
    /// The presets made after they arranged their layout, which their file cannot hold: cast and
    /// nearby sharing (2026-10-11, "all the remaining flyouts should become folders with a face now"),
    /// the desktops (2026-10-11, the desktop switcher's), and the clock and the calendar together
    /// (2026-10-11, "make a premade group that combines both"), and VPN and project (their entries'). The keyboard's
    /// is in the file already, as their keyboard entry, which reading the file makes that folder. The
    /// layout made has them where the
    /// library puts them (PresetLayout.SectionOf); the comparisons leave them out. So are the combined
    /// groups (2026-10-11: "add some combined folder / widget groups / premade groups now").
    /// </summary>
    private static readonly HashSet<string> MadeSince = [Presets.CastId, Presets.NearbyId, Presets.DesktopsId, Presets.VpnId, Presets.ProjectId, Presets.LayoutsId, Presets.MeetingId, Presets.AirQualityId, Presets.TidesId, Presets.SportsId, Presets.QuakesId, Presets.SunId, Presets.StartId, Presets.WinSearchId,
        .. Presets.All.Where(preset => preset.Combined).Select(preset => preset.Id)];

    private static IEnumerable<BarItem> FoldersOf(TaskbarLayout layout) => layout.Islands.Where(island => island.Folder is not null);

    /// <summary>
    /// Every folder of their "All ready-made folders" is what its preset makes, exactly: the lines,
    /// their order, the lists side by side and their widths, each line's height and options, the
    /// footers, the face and its options, the glyph.
    /// </summary>
    [Fact]
    public void EveryPresetIsThePersonsFolder()
    {
        var differ = new List<string>();
        foreach (var folder in FoldersOf(Layout(PresetLayout.Id)))
        {
            var preset = PresetOf(folder);
            Assert.True(preset is not null, $"no preset is their folder {folder.Id}");
            if (AskedForSince.Contains(preset.Id))
            {
                continue;
            }
            var person = Written(folder);
            var made = Written(Presets.Folder(preset, Theirs));
            if (!JsonNode.DeepEquals(person, made))
            {
                differ.Add($"{preset.Id}:\n  theirs {person.ToJsonString()}\n  made   {made.ToJsonString()}");
            }
        }
        Assert.True(differ.Count == 0, string.Join("\n", differ));
    }

    /// <summary>
    /// Their layout holds every preset but the battery's, which a PC without one is not given
    /// (PresetLayout.On): none was taken out by hand.
    /// </summary>
    [Fact]
    public void TheirLayoutHoldsEveryPresetOfAPcWithoutABattery()
    {
        var names = FoldersOf(Layout(PresetLayout.Id)).Select(folder => PresetOf(folder)!.Title).Order();

        Assert.Equal(PresetLayout.On(Theirs).Where(preset => !MadeSince.Contains(preset.Id)).Select(preset => preset.Title).Order(), names);
    }

    /// <summary>
    /// What "All ready-made folders" makes is their arrangement: the same entries in the same
    /// sections and order — Start, the pins and the open apps; no search box, Task view or tray;
    /// the indicators, the keyboard, now playing and show desktop at the end.
    /// </summary>
    [Fact]
    public void ThePresetLayoutIsThePersonsArrangement()
    {
        static IEnumerable<string> Shape(TaskbarLayout layout, Func<BarItem, Preset?> preset) =>
            layout.Islands.Select(island => $"{island.Section} {(island.Folder is null ? island.Kind.ToString() : preset(island)?.Id)}");

        var made = PresetLayout.Make(Theirs);
        made.Islands.RemoveAll(island => island.Folder is not null && MadeSince.Contains(PresetsTests.MadeBy(island, Theirs).Id));

        Assert.Equal(Shape(Layout(PresetLayout.Id), PresetOf), Shape(made, island => PresetsTests.MadeBy(island, Theirs)));
        // And the newer ones where the library puts them: the desktops with the start's groups.
        Assert.Contains(PresetLayout.Make(Theirs).Islands, island => island.Folder?.Face.Kind == FolderFaces.DesktopsId && island.Section == BarSection.Start);
    }

    /// <summary>A taskbar without a pen, a touch screen or a battery, as theirs.</summary>
    private static readonly WindowsTaskbar TheirTaskbar = new() { Battery = false, Seconds = false };

    /// <summary>A new bar on their PC, with no pins (theirs are taken out of the file).</summary>
    private static TaskbarLayout NewBar()
    {
        var made = Config.FirstLayout();
        made.Islands = Sections.DefaultLayout(pins: [], inputs: new InputHardware(Pen: false, Touch: false, Slate: false), windows: TheirTaskbar);
        return made;
    }

    /// <summary>
    /// What is inside a new bar's folders is their presets', which the person arranged in "All
    /// ready-made folders" ("the templates for the folders of course should still come from the
    /// edit"), not the older copies their "Default" still holds: each folder's lines are exactly
    /// what its preset makes.
    /// </summary>
    [Fact]
    public void ANewBarsFoldersAreThePresets()
    {
        static JsonNode Inside(BarItem folder)
        {
            var node = Written(folder).AsObject();
            node.Remove("face");
            return node;
        }

        foreach (var folder in FoldersOf(NewBar()))
        {
            var preset = PresetsTests.MadeBy(folder, Theirs);
            var made = Inside(Presets.Folder(preset, Theirs));

            Assert.True(JsonNode.DeepEquals(made, Inside(folder)), $"{preset.Id}:\n{made.ToJsonString()}\n{Inside(folder).ToJsonString()}");
        }
    }
}
