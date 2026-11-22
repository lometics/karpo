// sports: the sports widget drawn (docs\widgets.md §239), from ESPN's saved answers (SavedSports). The
// score and its fixtures and results (§603, a widget each) at one to four and one to six rows — Monza's match on now
// with the league's other match, Barcelona's next match at Paris, its final at Betis, a team not set yet, the scores
// not come yet — dark and light at 150 % and dark at 100 %
// (sports-lines-<live|next|final|unset|waiting>(-fixtures)(-light|-100).png);
// and the face on the bar in each state, at 100 % and 150 % and down a bar on its side (sports-faces-<dark|light>.png).
// The logos are stand-ins, a disc in each side's colour, so a sheet is the same on every machine. The
// sheets go to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures) before anything is asserted. Runs only
// with KARPO_UI_TESTS=1, like the other picture tests.
//
// What is held: a face's width is its team's and never the reading's — every state one width — its ink
// stays inside its cell, and the score is its size's form, the scoreboard at Full.

using Karpo.Dock;
using Karpo.Dock.Sports;
using Karpo.Tests.Core;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sports;

public sealed unsafe class SportsPictures
{
    private static Look LookFor(bool dark) => ListFormPictures.LookFor(dark);

    /// <summary>Monday evening, as the answers were saved: Monza 2–0 Cagliari in the 53rd minute.</summary>
    private static readonly DateTime Saved = SavedSports.SavedUtc;

    /// <summary>Saturday, two hours after Barcelona's kick-off at Betis: its 5–2 final.</summary>
    private static readonly DateTime AfterBetis = new(2026, 10, 17, 18, 40, 0, DateTimeKind.Utc);

    /// <summary>§479: the World Cup's final's evening, two hours after its kick-off.</summary>
    private static readonly DateTime WorldCupFinalNight = new(2026, 7, 19, 22, 0, 0, DateTimeKind.Utc);

    /// <summary>Runs <paramref name="body"/> with the clock at <paramref name="utc"/>.</summary>
    internal static void At(DateTime utc, Action body)
    {
        var was = Clock.Now;
        Clock.Now = () => utc.ToLocalTime();
        try
        {
            body();
        }
        finally
        {
            Clock.Now = was;
        }
    }

    /// <summary>A disc in a colour of the side's own, 64 px: what stands for its logo.</summary>
    internal static Pixels? Logo(string address)
    {
        const int side = 64;
        var hash = (uint)address.GetHashCode(StringComparison.Ordinal);
        var (r, g, b) = ((byte)(60 + (hash % 160)), (byte)(60 + ((hash >> 8) % 160)), (byte)(60 + ((hash >> 16) % 160)));
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var dx = x - 31.5f;
                var dy = y - 31.5f;
                var inside = (dx * dx) + (dy * dy) <= 30 * 30;
                var at = ((y * side) + x) * 4;
                if (inside)
                {
                    (data[at], data[at + 1], data[at + 2], data[at + 3]) = (b, g, r, 255);
                }
            }
        }
        return new Pixels { Data = data, Width = side, Height = side };
    }

    /// <summary>Monza's answer with what is to come after its match on now, as its schedule would have it.</summary>
    internal static SportsAnswer MonzaWithFixtures()
    {
        var answer = SavedSports.MonzaLive();
        answer.Matches.Add(Fixture("m1", Saved.AddDays(5), "4007", "MON", "Monza", "109", "JUV", "Juventus", home: false));
        answer.Matches.Add(Fixture("m2", Saved.AddDays(12).AddHours(-3), "4007", "MON", "Monza", "103", "INT", "Internazionale", home: true));
        answer.Standing = "4th in Italian Serie A";
        answer.Record = "5-2-1";
        return answer;
    }

    /// <summary>§369: Serie A, followed itself.</summary>
    internal static SportsOptions SerieA() => new() { Sport = "soccer", League = "ita.1", Name = "Serie A" };

    /// <summary>§369: Serie A's scoreboard while Monza played: its two games of the evening, one on.</summary>
    internal static SportsAnswer SerieAAnswer() => new()
    {
        ScheduledUtc = Saved,
        Matches = SportsAnswers.Matches(SavedSports.Answer("soccer-scoreboard"), "ita.1"),
        Logo = SportsLeagues.Find("soccer", "ita.1")!.Logo,
    };

    private static SportsMatch Fixture(string id, DateTime start, string team, string abbreviation, string name, string otherId, string other, string otherName, bool home)
    {
        var own = new SportsSide { Id = team, Abbreviation = abbreviation, Name = name, Logo = $"https://a.espncdn.com/i/teamlogos/soccer/500/{team}.png" };
        var them = new SportsSide { Id = otherId, Abbreviation = other, Name = otherName, Logo = $"https://a.espncdn.com/i/teamlogos/soccer/500/{otherId}.png" };
        return new SportsMatch { Id = id, StartUtc = start, State = MatchState.Scheduled, League = "ita.1", LeagueName = "Serie A", Home = home ? own : them, Away = home ? them : own };
    }

    private static SportsTile Line(Gfx gfx, SportsOptions? team, SportsHeld held, bool logos = true, ListKind kind = ListKind.Sports, LineSize? size = null)
    {
        var item = kind.New();
        item.Id = "sports";
        item.Sports = team ?? new SportsOptions();
        item.Size = size ?? item.Size;
        var tile = (SportsTile)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "sports"), item, kind, gfx, new Config())!;
        tile.Source = new SportsSource { Read = () => held, Logo = logos ? Logo : null };
        return tile;
    }

    [UiFact]
    public void TheLineDrawsWhatItsHeightHolds()
    {
        OnStaThread(gfx =>
        {
            void Sheets(string name, DateTime at, SportsOptions? team, SportsHeld held, bool logos = true) => At(at, () =>
            {
                foreach (var (kind, suffix) in new[] { (ListKind.Sports, ""), (ListKind.SportsFixtures, "-fixtures") })
                {
                    using var line = Line(gfx, team, held, logos, kind);
                    foreach (var dark in new[] { true, false })
                    {
                        ListLinesTests.Sheet(gfx, line, kind, 1.5f, LookFor(dark), Path.Combine(Pictures, $"sports-lines-{name}{suffix}{(dark ? "" : "-light")}.png"));
                    }
                    ListLinesTests.Sheet(gfx, line, kind, 1f, LookFor(true), Path.Combine(Pictures, $"sports-lines-{name}{suffix}-100.png"));
                }
            });
            Sheets("live", Saved, SavedSports.Monza(), new SportsHeld(MonzaWithFixtures(), Saved, null, false));
            Sheets("next", Saved, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), Saved, null, false));
            Sheets("final", AfterBetis, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), AfterBetis, null, false));
            Sheets("stale", Saved.AddMinutes(9), SavedSports.Monza(), new SportsHeld(MonzaWithFixtures(), Saved, S.DockSportsUnreachable, false));
            Sheets("unset", Saved, null, default);
            Sheets("waiting", Saved, SavedSports.Barcelona(), default);
            // §369: a team with its league's table; a league's day and its table, football's and the
            // NBA's; Serie A followed while a game is on; the logos not come yet, each side's monogram.
            var leagueSaved = SavedSports.LeagueSavedUtc;
            Sheets("table", leagueSaved, SavedSports.Bayern(), new SportsHeld(SavedSports.BayernAnswer(), leagueSaved, null, false));
            Sheets("league", leagueSaved, SavedSports.Bundesliga(), new SportsHeld(SavedSports.BundesligaAnswer(), leagueSaved, null, false));
            Sheets("league-nba", leagueSaved, SavedSports.Nba(), new SportsHeld(SavedSports.NbaAnswer(), leagueSaved, null, false));
            Sheets("league-live", Saved, SerieA(), new SportsHeld(SerieAAnswer(), Saved, null, false));
            Sheets("badges", leagueSaved, SavedSports.Bundesliga(), new SportsHeld(SavedSports.BundesligaAnswer(), leagueSaved, null, false), logos: false);
            Sheets("table-badges", leagueSaved, SavedSports.Bayern(), new SportsHeld(SavedSports.BayernAnswer(), leagueSaved, null, false), logos: false);

            At(Saved, () =>
            {
                var held = new SportsHeld(MonzaWithFixtures(), Saved, null, false);
                var style = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
                var width = FolderFlyout.ListWidth(1.5f);
                // §603: the score its one row at Compact, the scoreboard alone at Full, whatever its height.
                using var compact = Line(gfx, SavedSports.Monza(), held, size: LineSize.Compact);
                Assert.True(compact.Layout(width, style, compact.Read(), LineRows.Px(8, style)).One);
                using var live = Line(gfx, SavedSports.Monza(), held);
                var board = live.Layout(width, style, live.Read(), LineRows.Px(1, style));
                Assert.Equal([SportsRowKind.Board], board.Rows.Select(row => row.Kind));
                Assert.Equal(board.Height, live.ListHeight(width, style));
                Assert.Equal("https://www.espn.com/soccer/match/_/gameId/401874762", live.Site);
                // §479: its fixtures and results, laid out once and scrolled: the match on now and the league's other under
                // their headings, then what is to come — every one of it, nothing cut.
                using var fixtures = Line(gfx, SavedSports.Monza(), held, kind: ListKind.SportsFixtures);
                var laid = fixtures.Layout(width, style, fixtures.Read(), LineRows.Px(2, style));
                Assert.False(laid.One);
                Assert.Equal(
                    [SportsRowKind.Heading, SportsRowKind.Match, SportsRowKind.Heading, SportsRowKind.Match, SportsRowKind.Heading, SportsRowKind.Match, SportsRowKind.Match, SportsRowKind.Note],
                    laid.Rows.Select(row => row.Kind));
                Assert.Equal(laid.Height, fixtures.Layout(width, style, fixtures.Read(), LineRows.Px(12, style)).Height);
            });

            // §369, §479: a league's round, every game of it, and its table a view of its own; a click opens what is under it.
            At(leagueSaved, () =>
            {
                var held = new SportsHeld(SavedSports.BundesligaAnswer(), leagueSaved, null, false);
                using var league = Line(gfx, SavedSports.Bundesliga(), held, kind: ListKind.SportsFixtures);
                var style = FolderFlyout.StyleFor(1.5f, LookFor(true), FlyoutTheme.For(LookFor(true)));
                var width = FolderFlyout.ListWidth(1.5f);
                var matches = league.Layout(width, style, league.Read());
                Assert.Equal([SportsPage.Matches, SportsPage.Table], league.Read().Pages);
                Assert.Equal(league.Read().Day.Count, matches.Rows.Count(row => row.Kind == SportsRowKind.Match));
                Assert.Equal(3, matches.Rows.Count(row => row.Kind == SportsRowKind.Heading));
                // §484: no views over it: the list starts at the top, its first heading naming the league.
                Assert.Equal(SportsRowKind.Heading, matches.Rows[0].Kind);
                Assert.Equal(0f, matches.Rows[0].Top);
                Assert.StartsWith(league.Options.Name, matches.Rows[0].Words, StringComparison.Ordinal);

                // A click opens what is under it: a game its match; the table, a widget of its own, a row of which opens its team.
                var box = PopupPaint.Rect(0, 0, width, matches.Height);
                Bitmap(width, box.bottom, dc => league.PaintCard(dc, box, style, WidgetSurface.List), _ => { });
                var opened = new List<string>();
                league.Opened = opened.Add;
                var firstGame = matches.Rows.First(row => row.Kind == SportsRowKind.Match);
                var target = league.CardTarget(new System.Numerics.Vector2(width / 2, firstGame.Top + 4), box, style, WidgetSurface.List);
                Assert.True(target > 0);
                Assert.Equal(IslandView.App, league.Click(target).View);
                league.Open();
                Assert.Equal(["https://www.espn.com/soccer/match/_/gameId/401884746/mainz-sv-elversberg"], opened);
                using var table = Line(gfx, SavedSports.Bundesliga(), held, kind: ListKind.SportsTable);
                table.Opened = opened.Add;
                Assert.Equal(SportsPage.Table, table.List);
                var places = table.Layout(width, style, table.Read());
                Assert.Equal(18, places.Rows.Count(row => row.Kind == SportsRowKind.Place));
                var tableBox = PopupPaint.Rect(0, 0, width, places.Height);
                var lastRow = places.Rows[^1];
                _ = table.Click(table.CardTarget(new System.Numerics.Vector2(width / 2, lastRow.Top + 4), tableBox, style, WidgetSurface.List));
                table.Open();
                Assert.StartsWith("https://www.espn.com/soccer/team/_/id/", opened[^1], StringComparison.Ordinal);
                // Beside every row: the shown match.
                _ = table.Click(0);
                table.Open();
                Assert.Equal("https://www.espn.com/soccer/match/_/gameId/401884746/mainz-sv-elversberg", opened[^1]);
            });
        });
    }

    /// <summary>A folder wearing the sports face over a sports line of <paramref name="team"/>, the face showing <paramref name="held"/>.</summary>
    internal static FolderIsland Folder(Gfx gfx, SportsOptions? team, SportsHeld? held)
    {
        var line = ListKind.Sports.New();
        line.Id = "sports";
        line.Sports = team ?? new SportsOptions();
        var island = Configs.Holding(line);
        island.Id = "sportsFolder";
        island.Folder!.Face = FolderFace.Of(FolderFaces.SportsId);
        island.Normalize();
        FolderIsland.SportsStand = held;
        try
        {
            return new FolderIsland(Config.IslandKey(null, island.Id), island, gfx, new Config());
        }
        finally
        {
            FolderIsland.SportsStand = null;
        }
    }

    internal sealed record Shot(string Caption, DateTime At, SportsOptions? Team, SportsHeld? Held, float Scale = 1f, bool Vertical = false);

    [UiFact]
    public void TheFaceSaysTheScoreOrTheNextMatchAsWideAsItReads()
    {
        var failures = new List<string>();
        OnStaThread(gfx =>
        {
            var barcelona = new SportsHeld(SavedSports.BarcelonaAnswer(), Saved, null, false);
            var monza = new SportsHeld(MonzaWithFixtures(), Saved, null, false);
            var postponed = SavedSports.BarcelonaAnswer();
            postponed.Matches.Single(match => match.Home.Abbreviation == "PSG").State = MatchState.Postponed;
            Shot[] shots =
            [
                new("Live, 53'", Saved, SavedSports.Monza(), monza),
                new("Next: at Paris tomorrow", Saved, SavedSports.Barcelona(), barcelona),
                new("Final at Betis", AfterBetis, SavedSports.Barcelona(), new SportsHeld(SavedSports.BarcelonaAnswer(), AfterBetis, null, false)),
                new("Next, put off", Saved, SavedSports.Barcelona(), new SportsHeld(postponed, Saved, null, false)),
                new("Stale, ESPN unreachable", Saved.AddMinutes(9), SavedSports.Monza(), new SportsHeld(MonzaWithFixtures(), Saved, S.DockSportsUnreachable, false)),
                new("Waiting", Saved, SavedSports.Barcelona(), default(SportsHeld)),
                new("No team yet", Saved, null, null),
                new("150 %, live", Saved, SavedSports.Monza(), monza, 1.5f),
                new("150 %, next", Saved, SavedSports.Barcelona(), barcelona, 1.5f),
                new("On its side, live", Saved, SavedSports.Monza(), monza, 1.5f, Vertical: true),
                new("On its side, next", Saved, SavedSports.Barcelona(), barcelona, 1.5f, Vertical: true),
                new("League: its next round, Friday", SavedSports.LeagueSavedUtc, SavedSports.Bundesliga(), new SportsHeld(SavedSports.BundesligaAnswer(), SavedSports.LeagueSavedUtc, null, false)),
                new("League, a game on", Saved, SerieA(), new SportsHeld(SerieAAnswer(), Saved, null, false)),
                new("NBA tonight", SavedSports.LeagueSavedUtc, SavedSports.Nba(), new SportsHeld(SavedSports.NbaAnswer(), SavedSports.LeagueSavedUtc, null, false)),
                // §479: how a knockout match ended, said on the bar too.
                new("World Cup final, after extra time", WorldCupFinalNight, SavedSports.WorldCup(), new SportsHeld(SavedSports.WorldCupAnswer(), WorldCupFinalNight, null, false)),
                new("Round of 32, on penalties", SavedSports.ShootoutsUtc.AddHours(2), SavedSports.WorldCup(), new SportsHeld(SavedSports.WorldCupShootouts(), SavedSports.ShootoutsUtc.AddHours(2), null, false)),
                new("Swiss Super League, last of 2025", SavedSports.CupsSavedUtc, SavedSports.SuperLeague(), new SportsHeld(SavedSports.SuperLeagueAnswer(), SavedSports.CupsSavedUtc, null, false)),
            ];
            foreach (var dark in new[] { true, false })
            {
                Faces(gfx, shots, LookFor(dark), Path.Combine(Pictures, $"sports-faces-{(dark ? "dark" : "light")}.png"), failures);
            }

            // As wide as what it reads (docs\widgets.md §244): live, next, final, put off, stale and
            // waiting are not all one width, and none is under a cell.
            var style = new IslandStyle(1.5f, false, 72, 36, LookFor(true));
            var widths = new List<float>();
            foreach (var shot in shots.Where(shot => shot.Team?.Team == "83"))
            {
                At(shot.At, () =>
                {
                    using var island = Folder(gfx, shot.Team, shot.Held);
                    widths.Add(island.CellSize(0, style).Width);
                });
            }
            if (widths.Distinct().Count() == 1 || widths.Min() < style.Cell)
            {
                failures.Add($"Barcelona: {string.Join(" / ", widths)} px for its readings");
            }

            // What the face says and its tooltip.
            At(Saved, () =>
            {
                using var face = Folder(gfx, SavedSports.Monza(), monza);
                var context = new FaceContext(gfx, new IslandStyle(1f, false, 48, 24, LookFor(true)), face.Options, face);
                Assert.Equal($"Monza\nMonza – Cagliari · Serie A\nMON 2–0 CAG 53'\n{S.DockSportsTipLiveMinutes(53)}\nNow", FaceDrawings.Of(face.Options.Face).Tooltip(context));
                Assert.Contains("|Live|401874762|MON 2–0 CAG 53'|False", FaceDrawings.Of(face.Options.Face).Reading(context));
                using var none = Folder(gfx, null, null);
                var unset = new FaceContext(gfx, new IslandStyle(1f, false, 48, 24, LookFor(true)), none.Options, none);
                Assert.Equal("unset", FaceDrawings.Of(none.Options.Face).Reading(unset));
                Assert.Null(FaceDrawings.Of(none.Options.Face).Tooltip(unset));
            });
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Each shot's folder drawn on the bar's ground under its caption; its ink held inside its cell.</summary>
    internal static void Faces(Gfx gfx, IReadOnlyList<Shot> shots, Look look, string path, List<string> failures)
    {
        const float pad = 14;
        const float label = 18;
        const int across = 3;
        var made = new List<(Shot Shot, FolderIsland Island, IslandStyle Style, float W, float H)>();
        try
        {
            foreach (var shot in shots)
            {
                At(shot.At, () =>
                {
                    var island = Folder(gfx, shot.Team, shot.Held);
                    var thickness = 48 * shot.Scale;
                    var style = new IslandStyle(shot.Scale, shot.Vertical, thickness, MathF.Round(thickness / 2), look);
                    var (w, h) = island.CellSize(0, style);
                    made.Add((shot, island, style, w, h));
                    failures.AddRange(Inside(island, style, $"{shot.Caption} ({(look.Dark ? "dark" : "light")})"));
                });
            }
            var column = made.Max(item => MathF.Max(item.W, 220)) + pad;
            var rows = (made.Count + across - 1) / across;
            var rowHeight = made.Max(item => item.H) + label + pad;
            var width = pad + (across * column);
            var height = pad + (rows * rowHeight);
            Bitmap(width, height, dc =>
            {
                var ground = look.Dark ? new DXGI_RGBA(0x20 / 255f, 0x20 / 255f, 0x20 / 255f, 1) : new DXGI_RGBA(0xEE / 255f, 0xEE / 255f, 0xEE / 255f, 1);
                dc->Clear(&ground);
                var caption = look.Dark ? new DXGI_RGBA(1, 1, 1, 0.6f) : new DXGI_RGBA(0, 0, 0, 0.6f);
                for (var index = 0; index < made.Count; index++)
                {
                    var (shot, island, style, w, h) = made[index];
                    var x = pad + ((index % across) * column);
                    var y = pad + ((index / across) * rowHeight);
                    PopupPaint.Text(dc, gfx, shot.Caption, 12, Weight.Regular, caption, x, y);
                    var top = y + label;
                    PopupPaint.Stroke(dc, PopupPaint.Rect(x - 1, top - 1, w + 2, h + 2), 2, 1, look.Dark ? new DXGI_RGBA(1, 1, 1, 0.12f) : new DXGI_RGBA(0, 0, 0, 0.12f));
                    var drawn = dc;
                    var cellStyle = style;
                    At(shot.At, () => Shifted(drawn, x, top, () => island.PaintCell(drawn, 0, cellStyle, w, h)));
                }
            }, bitmap => Save(bitmap, path));
        }
        finally
        {
            foreach (var item in made)
            {
                item.Island.Dispose();
            }
        }
    }

    /// <summary>Where the face's ink is drawn into its cell: inside it.</summary>
    private static List<string> Inside(FolderIsland island, IslandStyle style, string where)
    {
        var failures = new List<string>();
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, style, width, height)),
            bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
        if (right <= left)
        {
            failures.Add($"{where}: draws nothing");
        }
        else if (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f)
        {
            failures.Add($"{where}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
        }
        return failures;
    }
}
