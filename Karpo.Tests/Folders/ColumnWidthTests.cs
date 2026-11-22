// A column's width is one of five presets (2026-10-18, the person: "lets not let them decide the
// width, just add like 2 more widths as selection"): extra wide and widest after narrow, medium and
// wide, each picked and kept as the others are; a width of its own a build of 2026-10-17 wrote
// ("customWidths") read once as the preset nearest it; a folder wider than its display narrowed
// there no more: what does not fit runs past the screen's edge (§320). The presets' own rules are
// FolderListWidthTests'; the pictures are ColumnWidthPictures.

using System.Text.Json;
using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Core;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class ColumnWidthTests
{
    /// <summary>A folder of lists of battery lines, each named by its id: "a b | c".</summary>
    private static FolderOptions Lists(string shape)
    {
        var folder = new FolderOptions { Lists = [] };
        foreach (var list in shape.Split('|'))
        {
            folder.Lists.Add([.. list.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(id =>
            {
                var island = ListKind.Battery.New();
                island.Id = id;
                return new FolderCell { Island = island };
            })]);
        }
        return folder;
    }

    private static FolderListWidth[] Widths(FolderOptions folder) => [.. Enumerable.Range(0, folder.Lists.Count).Select(folder.WidthOf)];

    private static JsonObject Written(BarItem holder) =>
        JsonNode.Parse(JsonSerializer.Serialize(holder, ConfigJson.Default.BarItem))!["folder"]!.AsObject();

    private static BarItem Holder(FolderOptions lists)
    {
        var holder = BarKind.FolderList.New();
        holder.Id = "box";
        holder.Folder!.Lists = lists.Lists;
        holder.Folder.Widths = lists.Widths;
        return holder;
    }

    private static FolderOptions FolderOf(Config config) => Configs.Islands(config).Single(island => island.Folder is not null).Folder!;

    /// <summary>
    /// Every one of the five, picked for a list, is that list's width, written by its name and read
    /// back as it was, nothing to repair.
    /// </summary>
    [Fact]
    public void EachPresetAppliesAndPersists()
    {
        string[] names = ["narrow", "medium", "wide", "extraWide", "widest"];
        for (var at = 0; at < FolderListWidths.All.Count; at++)
        {
            var preset = FolderListWidths.All[at];
            var folder = Lists("a | b");
            folder.Widths = [FolderListWidth.Wide, FolderListWidth.Wide];
            Assert.Equal(preset != FolderListWidth.Wide, Karpo.Core.Folders.SetWidth(folder, 1, preset));
            Assert.Equal([FolderListWidth.Wide, preset], Widths(folder));

            var holder = Holder(folder);
            Assert.Equal(["wide", names[at]], Written(holder)["widths"]!.AsArray().Select(node => node!.GetValue<string>()));
            var again = Configs.RoundTrip(Configs.With(holder), out var changed);
            Assert.False(changed, $"{preset} read back as something to repair");
            Assert.Equal([FolderListWidth.Wide, preset], Widths(FolderOf(again)));
        }
    }

    /// <summary>A file of the three presets there were reads and writes exactly as before: the same text after a round trip.</summary>
    [Fact]
    public void PresetFilesStayAsTheyWere()
    {
        var folder = Lists("a | b | c");
        folder.Widths = [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide];
        var config = Configs.With(Holder(folder));
        var text = config.ToJson();
        Assert.DoesNotContain("customWidths", text);
        Assert.Equal(text, Configs.RoundTrip(config, out var changed).ToJson());
        Assert.False(changed);
    }

    /// <summary>The folder's object in a config's JSON, wherever the layout keeps it.</summary>
    private static JsonObject? FolderNode(JsonNode? node) => node switch
    {
        JsonObject obj when obj["folder"] is JsonObject folder && folder["lists"] is not null => folder,
        JsonObject obj => obj.Select(pair => FolderNode(pair.Value)).FirstOrDefault(found => found is not null),
        JsonArray array => array.Select(FolderNode).FirstOrDefault(found => found is not null),
        _ => null,
    };

    // --- No display narrows a column (§320) -------------------------------------------------

    /// <summary>
    /// The person, 2026-10-23: "in one of my folders i cannot make the columns any wider ... rather
    /// than limit the user in what he can do layout wise i think we should rather allow a widget /
    /// widgets to clip over the edge and then just warn the user about that." Their News folder: six
    /// empty columns a far slot left before a widest clock and a wide feed, at 150 % on a 3840 px
    /// display. Every list counted, the empty ones too, so the two with widgets were cut to one
    /// level (474 px) and no width picked for them changed anything. Now each list is its preset,
    /// whatever the display, measured as it is drawn, and widening the clock's list widens it.
    /// </summary>
    [Fact]
    public void NoDisplayNarrowsAColumn()
    {
        var look = new Look(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));
        var news = Lists("| | | | | | a | b");
        news.Widths = [FolderListWidth.Medium, FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Wide, FolderListWidth.Widest, FolderListWidth.Wide];
        var plan = FolderFlyout.Build(look, 1.5f, FolderView.List, news, news.Cells, 2098 - 62);
        Assert.Equal([1200f, 720f], plan.Lists.Skip(6).Select(list => list.Width));
        Assert.Equal(plan.Lists[6].Width, plan.Cards[0].right - plan.Cards[0].left);
        Assert.Equal([.. news.Widths.Select(width => FolderFlyout.ListWidth(1.5f, width))], FolderFlyout.ListWidths(news, 1.5f));

        // Wider is wider: the feed's list from wide to widest.
        Assert.True(Karpo.Core.Folders.SetWidth(news, 7, FolderListWidth.Widest));
        var wider = FolderFlyout.Build(look, 1.5f, FolderView.List, news, news.Cells, 2098 - 62);
        Assert.Equal(1200f, wider.Lists[7].Width);
        Assert.True(wider.Width > plan.Width);
    }
}
