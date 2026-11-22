// sports §371: the fixtures and results' Automatic height (§603: the list the line scrolled to, a widget of its own). Offered as every measured line's is, where a new line starts,
// and a fixed line switched to it in its menu; then, on ESPN's saved answers — a team on now, its next
// match, its final, a team with its table, a league's round and table, the NBA's, a league with a game on —
// at one to twelve rows, at 100 % and 150 % and at the largest text: the line is as tall as what it draws
// at its most, so it draws the same at that height (nothing lost, nothing past its foot) and ends under its
// last row; a taller most never makes it shorter. With nothing to show — no team, nothing come yet, ESPN
// unreachable before any answer — it is one row. And in a folder (sports-auto-<case>-<dark|light>.png, to
// KARPO_PNG_DIR): the line at Automatic up to 2, 4, 6, 9 and 12 rows beside Fixed 12. Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Sports;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sports;

public sealed class SportsHeightTests
{
    private static readonly DateTime Saved = SavedSports.SavedUtc;
    private static readonly DateTime AfterBetis = new(2026, 10, 17, 18, 40, 0, DateTimeKind.Utc);
    private static readonly DateTime LeagueSaved = SavedSports.LeagueSavedUtc;

    private sealed record Case(string Name, DateTime At, SportsOptions? Team, SportsHeld Held);

    private static Case[] Shown =>
    [
        new("live", Saved, SavedSports.Monza(), new SportsHeld(SportsPictures.MonzaWithFixtures(), Saved, null, false)),
        new("next", Saved, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), Saved, null, false)),
        new("final", AfterBetis, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), AfterBetis, null, false)),
        new("table", LeagueSaved, SavedSports.Bayern(), new SportsHeld(SavedSports.BayernAnswer(), LeagueSaved, null, false)),
        new("league", LeagueSaved, SavedSports.Bundesliga(), new SportsHeld(SavedSports.BundesligaAnswer(), LeagueSaved, null, false)),
        new("league-nba", LeagueSaved, SavedSports.Nba(), new SportsHeld(SavedSports.NbaAnswer(), LeagueSaved, null, false)),
        new("league-live", Saved, SportsPictures.SerieA(), new SportsHeld(SportsPictures.SerieAAnswer(), Saved, null, false)),
    ];

    private static Case[] Nothing =>
    [
        new("unset", Saved, null, default),
        new("waiting", Saved, SavedSports.Barcelona(), default),
        new("unreachable", Saved, SavedSports.Barcelona(), new SportsHeld(null, null, S.DockSportsUnreachable, false)),
        new("league-waiting", LeagueSaved, SavedSports.Bundesliga(), default),
    ];

    private static CellItem Item(SportsOptions? team, LineHeight height)
    {
        var item = ListKind.SportsFixtures.New();
        item.Id = "sports";
        item.Sports = team ?? new SportsOptions();
        item.Height = height;
        item.Normalize();
        return item;
    }

    private static SportsTile Line(Gfx gfx, Case shown, LineHeight height)
    {
        var tile = (SportsTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey(ListFormPictures.Owner, "sports"), Item(shown.Team, height), ListKind.SportsFixtures, gfx, new Config())!;
        tile.Source = new SportsSource { Read = () => shown.Held, Logo = SportsPictures.Logo };
        return tile;
    }

    [Fact]
    public void AutomaticIsOfferedWhereANewLineStartsAndInTheMenu()
    {
        Assert.True(ListWidgets.Of(ListKind.SportsFixtures).Grows);
        Assert.Equal(LineHeight.Automatic(24), LineHeight.StartFor(ListKind.SportsFixtures));
        Assert.Equal(LineHeight.Automatic(24), LineHeight.For(ListKind.SportsFixtures.New()));
        Assert.Equal(LineHeight.Automatic(5), new LineHeight(false, 5).Within(ListKind.SportsFixtures));
        // The ready-made folder's line starts where any new one does.
        Assert.Null(Assert.Single(Presets.Find(Presets.SportsId)!.Items, item => item.Kind == "sportsFixtures").Height);

        // A line set fixed, as the ready-made folder's was, switches to Automatic at its rows in its menu.
        var line = Item(SavedSports.Barcelona(), LineHeight.Exactly(6));
        var choice = LineHeightRows.For(line)!.Value;
        Assert.True(choice.Modes);
        Assert.Equal(LineHeight.Automatic(6), LineHeightRows.Act(choice, LineHeightRows.AutomaticId));
        Assert.True(LineHeightRows.Resize(line, LineHeight.Automatic(6)));
        Assert.Equal(LineHeight.Automatic(6), LineHeight.For(line));
    }

    [UiFact]
    public void AnAutomaticLineIsAsTallAsWhatItDrawsAtEveryMost()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            foreach (var shown in Shown)
            {
                SportsPictures.At(shown.At, () =>
                {
                    using var tile = Line(gfx, shown, LineHeight.Automatic(2));
                    foreach (var (scale, text) in new[] { (1f, 1f), (1.5f, 1f), (1f, 2.25f) })
                    {
                        var look = ListFormPictures.LookFor(true, text);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var width = FolderFlyout.ListWidth(scale);
                        var whole = tile.Layout(width, style, tile.Read());
                        var before = 0f;
                        for (var cells = LineHeight.Fewest; cells <= 24; cells++)
                        {
                            var height = LineHeight.Automatic(cells);
                            tile.Configure(Item(shown.Team, height), new Config());
                            var content = tile.ListHeight(width, style);
                            var given = height.Given(content, LineRows.Cell(style));
                            var where = $"{shown.Name} at {scale * 100:0} %, text {text * 100:0} %, automatic up to {cells} cells: {given:0.#} px";
                            // §479: from two rows the whole of it, the same at every height, scrolled where it is taller.
                            var drawn = tile.Layout(width, style, tile.Read(), given);
                            if (given + 0.5f >= LineRows.Px(2, style) && (drawn.One || drawn.Height != whole.Height))
                            {
                                failures.Add($"{where} lays out {drawn.Height:0.#} px, whole {whole.Height:0.#}");
                            }
                            if (!drawn.One && drawn.Height > given + 0.5f && tile.ScrollHeight(width, given, style) < drawn.Height - 0.5f)
                            {
                                failures.Add($"{where}: {drawn.Height:0.#} px of it, and it does not scroll");
                            }
                            // Whole cells (§442): less than a cell under its last row.
                            if (given - (drawn.One ? LineRows.Px(1, style) : drawn.Height) >= LineRows.Cell(style))
                            {
                                failures.Add($"{where}, {given - drawn.Height:0.#} px under its last row");
                            }
                            if (given + 0.5f < before)
                            {
                                failures.Add($"{where}, shorter than at {cells - 1} cells ({before:0.#} px)");
                            }
                            before = given;
                        }
                        // Fill: its whole content, a team's past twelve rows of results, a league's round and table.
                        tile.Configure(Item(shown.Team, LineHeight.Automatic(24).As(LineHeightMode.Fill)), new Config());
                        Assert.True(LineRows.Whole(tile.ListHeight(width, style), style) + 0.5f >= before, $"{shown.Name}: filling, shorter than at twelve rows");
                    }
                });
            }

            // Nothing to show: one row, whatever its most.
            foreach (var shown in Nothing)
            {
                SportsPictures.At(shown.At, () =>
                {
                    using var tile = Line(gfx, shown, LineHeight.Automatic(24));
                    var look = ListFormPictures.LookFor(true);
                    var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                    Assert.Equal(LineRows.Px(1, style), tile.ListHeight(FolderFlyout.ListWidth(1.5f), style));
                });
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [UiFact]
    public void InAFolderTheLineEndsUnderItsLastRow()
    {
        OnStaThread(gfx =>
        {
            const float scale = 1.5f;
            (string Caption, LineHeight Height)[] heights =
            [
                ("Automatic: up to 2", LineHeight.Automatic(4)),
                ("Automatic: up to 4", LineHeight.Automatic(8)),
                ("Automatic: up to 6", LineHeight.Automatic(12)),
                ("Automatic: up to 9", LineHeight.Automatic(18)),
                ("Automatic: up to 12", LineHeight.Automatic(24)),
                ("Fixed: 12 rows", LineHeight.Exactly(24)),
            ];
            foreach (var shown in Shown.Concat(Nothing.Take(2)))
            {
                SportsPictures.At(shown.At, () =>
                {
                    foreach (var dark in new[] { true, false })
                    {
                        var look = ListFormPictures.LookFor(dark);
                        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
                        var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
                        try
                        {
                            foreach (var (caption, height) in heights)
                            {
                                var folder = ListFormPictures.Folder(gfx, made => ((SportsTile)made.Tiles[0].Island).Source = new SportsSource { Read = () => shown.Held, Logo = SportsPictures.Logo }, Item(shown.Team, height));
                                var (natural, plan) = ListFormPictures.Plan(folder, look, scale);
                                panels.Add((caption, look, folder, plan));
                                var given = ListFormPictures.Given(plan, 0);
                                var expected = height.Given(natural[0], LineRows.Cell(style));
                                Assert.True(MathF.Abs(given - expected) <= 1, $"{shown.Name}, {caption}: the folder gives {given} px, the line asks {expected}");
                                var tile = (SportsTile)folder.Tiles[0].Island;
                                var drawn = tile.Layout(plan.Cards[0].right - plan.Cards[0].left, style, tile.Read(), given);
                                var shows = drawn.One ? LineRows.Px(1, style) : MathF.Min(drawn.Height, given);
                                if (!height.Fixed)
                                {
                                    Assert.True(given - shows < LineRows.Cell(style), $"{shown.Name}, {caption}: {given - shows} px under its last row");
                                }
                            }
                            ListFormPictures.Sheet(gfx, $"sports-auto-{shown.Name}-{(dark ? "dark" : "light")}.png", scale, panels);
                        }
                        finally
                        {
                            foreach (var (_, _, folder, _) in panels)
                            {
                                folder.Dispose();
                            }
                        }
                    }
                });
            }
        });
    }
}
