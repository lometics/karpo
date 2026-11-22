// §620 (the person, 2026-11-09: "the premade groups should therefore have a different layout depending on if the taskbar is
// placed on the top and bottom or left and right"): a ready-made group put on a bar by the add page — its ghost and its
// click alike (AddList.Into) — arranged for the edge the bar stands on: on a top or a bottom bar its lists side by side as
// the recipe has them, down a side bar one list, the recipe's lists one under the other with a divider where one ended
// (Preset.Stacked), as wide as its widest; and pictured from a left and a bottom bar, dark at 150 %, to KARPO_PNG_DIR as
// presetside-<id>.png, each side arrangement standing whole beside a 1440p screen's side bar.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Folders.ListFormPictures;

namespace Karpo.Tests.Folders;

public sealed unsafe class PresetSideTests
{
    private const string Display = "PRESETSIDE1";

    /// <summary>A config whose display <see cref="Display"/> has its bar on <paramref name="edge"/>, and the owner its add page adds for.</summary>
    private static (Config Config, string Owner) BarOn(Edge edge)
    {
        var config = new Config();
        config.AddDisplay(Display, "Test");
        config.Screens.Single(screen => screen.Id == Display).Edge = edge;
        return (config, Config.DisplayOwner(Display));
    }

    /// <summary>Each list of <paramref name="folder"/>, each line its kind, its height and its size.</summary>
    private static string[][] Shape(FolderOptions folder) =>
        [.. folder.Lists.Select(list => list.Select(cell => Line(cell.Island)).ToArray())];

    /// <summary>Each list of <paramref name="folder"/>, each line its kind.</summary>
    private static string[][] Kinds(FolderOptions folder) =>
        [.. folder.Lists.Select(list => list.Select(cell => cell.Island.Kind).ToArray())];

    /// <summary>
    /// Every ready-made group put on a bar on each of the four edges by the add page (<see cref="AddList.Into"/>, what its
    /// ghost shows and its click puts in): on the bottom and the top its lists as the recipe has them, at the recipe's widths;
    /// on the left and the right one list, the recipe's lists one under the other in their order, a divider between each two,
    /// as wide as the widest of them. What it puts in is the folder the preset makes for that edge (<see cref="Presets.Folder"/>),
    /// line for line, on a laptop and on a desktop PC.
    /// </summary>
    [Fact]
    public void EveryPresetIsArrangedForTheEdgeItsBarStandsOn()
    {
        var (_, entries, presets) = AddList.Rows(WidgetType.Bar, turns: false, firstId: 100);
        Assert.Equal(Presets.All.Length, presets.Count);
        foreach (var machine in new[] { PresetsTests.Laptop, PresetsTests.Desktop })
        {
            foreach (var preset in Presets.All)
            {
                var across = Presets.Folder(preset, machine).Folder!;
                foreach (var edge in ScreenEdges.All)
                {
                    var (config, owner) = BarOn(edge);
                    var before = config.ToJson();
                    var spot = new AddSpot(owner, new BarSlot(BarSection.End, 0));
                    var ghost = AddList.Into(config, spot, (uint)(100 + entries.Count + presets.IndexOf(preset)), 100, entries, presets, () => machine)!;
                    Assert.Equal(before, config.ToJson());
                    var placed = ((BarItem)ghost.Config.FindIsland(owner, ghost.Key[(owner.Length + 1)..])!).Folder!;

                    // The folder the preset makes for the edge, line for line.
                    Assert.Equal(Shape(Presets.Folder(preset, machine, edge).Folder!), Shape(placed));
                    if (!edge.IsVertical())
                    {
                        Assert.Equal(Shape(across), Shape(placed));
                        Assert.Equal(preset.Widths, placed.Widths);
                        continue;
                    }
                    // One list: the lists across, one under the other, a divider where one ended; each line as across but
                    // the one the recipe gives a height of its own down a side.
                    var stacked = Kinds(across).SelectMany((list, index) => index == 0 ? list : ["divider", .. list]).ToArray();
                    Assert.Equal(stacked, Assert.Single(Kinds(placed)));
                    var expected = new List<string>();
                    foreach (var list in across.Lists)
                    {
                        if (expected.Count > 0)
                        {
                            expected.Add("divider  ");
                        }
                        expected.AddRange(list.Select(cell => SideHeights.TryGetValue(preset.Id, out var side) && side.Kind == cell.Island.Kind
                            ? $"{cell.Island.Kind} {side.Height} {cell.Island.Size}"
                            : Line(cell.Island)));
                    }
                    Assert.Equal(expected, Assert.Single(Shape(placed)));
                    Assert.Equal(preset.Widths is { Count: > 0 } widths ? [widths.MaxBy(FolderListWidths.Dip)] : null, placed.Widths);
                }
            }
        }
        // Only those: no other recipe gives a line a height of its own down a side.
        Assert.Equal(SideHeights.Keys.Order(), Presets.All.Where(preset => preset.Items.Any(item => item.SideHeight is not null)).Select(preset => preset.Id).Order());
    }

    /// <summary>
    /// §620: the lines a recipe gives a height of their own down a side bar, each a height given across only so that two lists
    /// side by side end level: the clock and calendar's stopwatch its one row, as the clock's; Today's events six rows, as the
    /// calendar's; the markets' feed as tall as two shares. Every other preset is the rule alone.
    /// </summary>
    private static readonly Dictionary<string, (string Kind, LineHeight Height)> SideHeights = new()
    {
        [Presets.ClockCalendarId] = ("stopwatch", LineHeight.Exactly(2)),
        [Presets.TodayId] = ("events", LineHeight.Exactly(12)),
        [Presets.MarketsNewsId] = ("rss", LineHeight.Exactly(2 * Presets.WatchlistCells)),
        // §641: All apps under the rest of Start, shorter than beside it.
        [Presets.StartId] = ("startApps", LineHeight.Exactly(Presets.StartAppsSideCells)),
    };

    /// <summary>A line as <see cref="Shape"/> says it: its kind, its height and its size.</summary>
    private static string Line(CellItem island) => $"{island.Kind} {island.Height} {island.Size}";

    /// <summary>
    /// The plan of <paramref name="folder"/> opened from a bar on <paramref name="edge"/> of a 2560 × <paramref name="screen"/>
    /// px display at <paramref name="scale"/>, its bar 48 DIP, its lines at <paramref name="natural"/>: the room its placement
    /// gives it (§173) — up to the work area's top over a bottom bar, its whole height less a margin at each end beside a side
    /// bar — and that room.
    /// </summary>
    private static (FolderPlan Plan, float Room) PlanOn(FolderIsland folder, Look look, float scale, Edge edge, IReadOnlyList<float> natural, int screen)
    {
        var bar = (int)PopupPaint.Round(48 * scale);
        var (work, anchor) = edge.IsVertical()
            ? (new RECT { left = bar, top = 0, right = 2560, bottom = screen }, new POINT { x = bar, y = screen / 2 })
            : (new RECT { left = 0, top = 0, right = 2560, bottom = screen - bar }, new POINT { x = 1280, y = screen - bar });
        var room = new FlyoutPlacement(anchor, work, edge).Room(scale);
        var plan = FolderFlyout.Build(look, scale, folder.View, folder.Laid, [.. folder.Tiles.Select(tile => tile.Cell)], work.bottom - work.top, natural: natural, edge: edge, room: room);
        return (plan, room);
    }

    /// <summary>
    /// Every ready-made group's folder opened from a bottom bar and from a left bar, dark at 150 %, every line on its stand-in
    /// (CombinedPresetTests.WithStandIns): presetside-&lt;id&gt;.png, the two side by side, and presetside-heights.txt, how tall
    /// each is with every line that grows at its most beside a 1440p and a 1080p screen's side bar. Each of more than one list
    /// stands whole down a 1440p screen's side bar at 150 %, nothing left out, as across it stands whole over its bottom bar
    /// (CombinedPresetTests). Needs Direct2D: KARPO_UI_TESTS=1.
    /// </summary>
    [UiFact]
    public void EachPresetOpenedFromALeftAndABottomBar()
    {
        const float scale = 1.5f;
        var report = new List<string>();
        var whole = new List<string>();
        Offscreen.OnStaThread(gfx => CombinedPresetTests.WithStandIns(() =>
        {
            foreach (var preset in Presets.All)
            {
                var folders = new List<FolderIsland>();
                try
                {
                    var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                    var look = LookFor(dark: true);
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                    {
                        var folder = CombinedPresetTests.Opened(gfx, Presets.Folder(preset, PresetsTests.Laptop, edge), edge.ToString());
                        folders.Add(folder);
                        var natural = FolderFlyout.Natural(folder.Tiles, folder.Laid, scale, look);
                        var (plan, _) = PlanOn(folder, look, scale, edge, natural, 1440);
                        panels.Add(($"{preset.Title}: from a {edge.ToString().ToLowerInvariant()} bar, {folder.Laid.Lists.Count} list(s), {plan.Width / scale:0} × {plan.Height / scale:0} DIP", look, folder, plan));
                        if (edge == Edge.Left && Presets.Folder(preset, PresetsTests.Laptop).Folder!.Lists.Count > 1)
                        {
                            var most = CombinedPresetTests.AtTheirMost(folder, natural);
                            var (big, bigRoom) = PlanOn(folder, look, scale, edge, most, 1440);
                            var (small, smallRoom) = PlanOn(folder, look, scale, edge, most, 1080);
                            report.Add($"{preset.Id}: side {plan.Height / scale:0} DIP as pictured; at its most {big.Height / scale:0} of {bigRoom / scale:0} DIP beside 1440p (left out: {string.Join(",", big.Hidden)}), {small.Height / scale:0} of {smallRoom / scale:0} beside 1080p (left out: {string.Join(",", small.Hidden)})");
                            if (big.Hidden.Any() || big.Height > bigRoom + 0.5f)
                            {
                                whole.Add($"{preset.Id} down a side bar stands {big.Height / scale} DIP of {bigRoom / scale}, leaving out {string.Join(",", big.Hidden)}");
                            }
                        }
                    }
                    Sheet(gfx, $"presetside-{preset.Id}.png", scale, panels);
                }
                finally
                {
                    foreach (var folder in folders)
                    {
                        folder.Closed();
                        folder.Dispose();
                    }
                }
            }
        }));
        File.WriteAllLines(Path.Combine(Offscreen.Pictures, "presetside-heights.txt"), report);
        Assert.Empty(whole);
    }
}
