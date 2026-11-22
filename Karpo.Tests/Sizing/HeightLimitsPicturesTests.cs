// Every kind whose content does not vary at a run of the heights it may be given (docs\widgets.md
// §140): Fixed at one row — every kind's fewest since §166 — each up to eight, and then tall ones,
// since no kind has a most of its own (§192: "remove the max height for widgets"), drawn as the open
// folder draws them, so a look at the sheet shows every height the card offers looks right. Each line
// keeps its ink inside its box at every one. The weather's lines are drawn with a forecast
// (SavedWeather), so their shorter forms are on the sheet and not only "can't be reached".
//
// And every card with a height, of a varying content too, very tall (§192): forty rows at 150 % in the
// dark theme, its ink inside its box — what the person sees setting a widget to the top of the screen.
// What a line does not fill stands at its top (ListScrollTests.EveryLineStandsAtTheTopOfATallLine).
//
// Sheets: height-limits-<kind>.png and tall-<kind>.png in KARPO_PNG_DIR.

using Karpo.Dock;
using Karpo.Dock.Weather;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sizing;

public sealed class HeightLimitsPicturesTests(ITestOutputHelper output)
{
    /// <summary>The heights each is drawn at: every one to eight, where the tiers change, then tall ones.</summary>
    private static readonly int[] Heights = [1, 2, 3, 4, 5, 6, 7, 8, 12, 20];

    /// <summary>The rows a very tall line is drawn at: past the 32 any line could once be set to, about a 1440p screen at 150 %.</summary>
    private const int Tall = 40;

    public static TheoryData<string> Kinds =>
        [.. ListWidgets.All.Where(line => line.Line == ListLine.Card && !line.Grows && line.Kind.HasHeight()).Select(line => line.Kind.Id())];

    public static TheoryData<string> Cards =>
        [.. ListWidgets.All.Where(line => line.Line == ListLine.Card && line.Kind.HasHeight()).Select(line => line.Kind.Id())];

    [UiTheory]
    [MemberData(nameof(Kinds))]
    public void EveryAllowedHeightIsDrawnInsideItsBox(string id) => Draw(id, Heights, 1f, $"height-limits-{id}.png");

    [UiTheory]
    [MemberData(nameof(Cards))]
    public void AVeryTallLineIsDrawnInsideItsBox(string id) => Draw(id, [Tall], 1.5f, $"tall-{id}.png");

    private void Draw(string id, int[] heights, float scale, string file)
    {
        var kind = ListWidgets.Find(id)!.Kind;
        OnStaThread(gfx =>
        {
            var was = Clock.Now;
            var weather = kind is ListKind.WeatherNow or ListKind.WeatherHours or ListKind.WeatherDays;
            Clock.Now = weather ? () => SavedWeather.Fixed : () => ListFormCases.Frozen;
            var look = LookFor(dark: true);
            var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
            try
            {
                // A chart that stretches is drawn up to its most (§460: a height past it is held to it), once.
                foreach (var cells in heights.Select(rows => Math.Min(rows * LineHeight.Row, LineHeight.Limits(kind).Most)).Distinct())
                {
                    var set = cells / (float)LineHeight.Row;
                    var folder = ListFormPictures.Folder(gfx, Line(kind, island =>
                    {
                        island.Height = LineHeight.Exactly(cells);
                        if (weather)
                        {
                            island.Weather = SavedWeather.Options();
                        }
                    }));
                    foreach (var line in folder.Tiles.Select(tile => tile.Island).OfType<WeatherLineTile>())
                    {
                        line.Source = SavedWeather.Source();
                    }
                    var (_, plan) = Plan(folder, look, scale);
                    var given = Given(plan, 0);
                    Assert.Equal(ListFormPictures.Cells(cells, scale, 1f), given, 1);
                    var ink = Ink(folder, plan, 0, look, scale);
                    Assert.True(ink.Top >= -0.5f && ink.Bottom <= given + 0.5f, $"{id} at {set} rows: ink {ink.Top:0}..{ink.Bottom:0} outside a line {given:0} px tall");
                    output.WriteLine($"{id} at {set} rows: given {given:0}, ink {ink.Top:0}..{ink.Bottom:0}");
                    panels.Add(($"{id}: {set} rows", look, folder, plan));
                }
                Sheet(gfx, file, scale, panels);
            }
            finally
            {
                Clock.Now = was;
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }
}
