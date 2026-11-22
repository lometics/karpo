// sports: a team's or a league's matches as a line of a list (docs\widgets.md §239, §369, §479), read from
// ESPN through the sports store (SportsStore), which keeps one fetch per team or league for every widget
// showing it.
//
// What it follows is the line's own (SportsOptions); with nothing it asks for a team or a league ("Set
// up in Settings"), and nothing is fetched. Nothing is asked until the line is looked at (§11); then it
// ticks at its pace — every half minute while a match is on.
//
// §603 (the person, 2026-11-09: "split all of them"): four widgets of what one line once showed more of as it grew,
// each its own kind and nothing of the others':
//   Score (sports)            the same at both sizes, never scrolling: Compact the kit's row with its caption — a red dot
//                             while it is on (else the team's or the league's logo), "FCB 2–1 RMA" or "vs Real Madrid",
//                             and at the end the clock, "Final", "AET", "Pens" or the day; under it the competition (the
//                             reading's age where it is stale) and the round, the leg or how a shoot-out or a tie came
//                             out —; Full the scoreboard — the same competition and the clock over the two sides with
//                             their logos about the score, the round under it (a league's: its lead match,
//                             SportsPick.Lead). Its height its size's, whatever it says.
//   Fixtures and results      a team: its match on now with the league's others, then every match to come and every
//     (sportsFixtures)        result, each under its heading; a league: its round's days, each under its day's heading
//                             (and its round where the day's games share one), a game a row
//   Table (sportsTable)       every group, its name over its columns: the place on its zone's colour, the logo, the team
//                             and P, GD, Pts — W, D and L too where the line is wide enough; America's W, L and GB
//   Knockout (sportsKnockout) every round in order, its matches as a league's are, each with its day and time
// The three lists are laid out whole (SportsLayout) and scroll (ListScroll.cs), as tall as their content up to their
// Height (§371): nothing is cut, nothing summarised; a competition with no table or no knockout says so on one row.
// Where each match stands, at a glance (§485): one on now a red dot before its clock in the live red, its names strong,
// a league's games on now first under "Live now"; a result how it ended ("Final", "AET", "Pens") — with its day where no
// day's heading says it — its losing side quiet; one to come its kick-off. Headings over what is done (a day gone by, a
// round played whole, "Results") quiet, over what is on or to come strong; a knockout brings its round now into sight.
// Names are the fullest that fit (ListTiers.Name); every match and every place says itself whole in its
// tooltip — both sides' full names, the competition and the round, when it is in this PC's time, how it
// ended — and a group's heading what its columns' letters stand for.
//
// A click opens what is under it on ESPN in the browser (§369): a match row its match, the scoreboard the
// shown match, a table row the team's page. Every one of them is a stop for the keys (§470). Its menu's
// Open website opens the shown match, and its "Refresh" fetches it again (IRefreshable, §451).

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Sports;

/// <summary>Where a sports line's answer comes from, where it is not the store: a test's, so nothing is fetched and no clock is waited on.</summary>
internal sealed class SportsSource
{
    public required Func<SportsHeld> Read { get; init; }

    /// <summary>A logo for an address; null for none.</summary>
    public Func<string, Pixels?>? Logo { get; init; }
}

/// <summary>A team's or a league's live score or next match, and its matches, table and knockout around it, as a line of a list.</summary>
internal sealed unsafe class SportsTile : CellIsland, IRefreshable, IListContent, IListScroll, ILineTargets
{
    private SportsOptions options = new();
    private bool looked;
    private SportsHeld held;

    // §603: the list it is — its fixtures and results, its table, its knockout —, or null for the score.
    private readonly SportsPage? list;

    // A table's own place and a knockout's round now brought into sight, once.
    private bool reveal = true;

    // The target under the pointer and pressed (rows from 1); the page a click chose, for once the folder has
    // gone; and the width and style the line was last laid out in.
    private int hot;
    private RowPress press;
    private string? chosen;
    private bool paintedOne = true;
    private float lastWidth;
    private TileStyle lastStyle;

    public SportsTile(string key, WidgetItem island, Gfx gfx, Config config)
        : base(key, island, gfx)
    {
        list = island.Info?.Id switch
        {
            "sportsFixtures" => SportsPage.Matches,
            "sportsTable" => SportsPage.Table,
            "sportsKnockout" => SportsPage.Knockout,
            _ => null,
        };
        Configure(island, config);
    }

    /// <summary>Tests only: where the answer comes from instead of the store.</summary>
    internal SportsSource? Source { get; set; } = Samples.On ? Samples.SportsSource() : null;

    /// <summary>The store this line registers with: the shared one, or a test's own.</summary>
    internal SportsStore Store { get; set; } = SportsStore.Shared;

    /// <summary>Opens an address in the browser: Windows' handler, or a test's.</summary>
    internal Action<string> Opened { get; set; } = static link => Launch.Open(link);

    /// <summary>The team or the league it follows.</summary>
    internal SportsOptions Options => options;

    /// <summary>§603: the list it is — Matches (its fixtures and results), Table, Knockout —, or null for the score.</summary>
    internal SportsPage? List => list;

    /// <summary>
    /// Every half minute, the quickest it is ever asked for (SportsPace.Live): a tick asks the store only
    /// whether a fetch is due, and the dock's timer is armed once for the bar, not as a match begins and ends.
    /// </summary>
    public override int IntervalMs => options.IsSet && Source is null ? (int)SportsPace.Live.TotalMilliseconds : 0;

    protected override int Count => 1;

    public override void Configure(WidgetItem island, Config config)
    {
        Settings = island;
        var next = island.Sports ?? new SportsOptions();
        // §635: a ghost's line, following nothing yet, follows the samples' team.
        if (!next.IsSet && Sampled)
        {
            next = Samples.SportsTeam();
        }
        var same = next.Key == options.Key;
        options = next;
        if (looked && !same && Source is null)
        {
            Store.Use(this, options);
            Store.Poll(this);
            Take();
        }
    }

    public override string? Tooltip(int index) => null;

    /// <summary>§369: taller than a row, its matches and its table's rows light under the pointer, each a thing to open.</summary>
    protected internal override bool LightsItsRows => !paintedOne;

    /// <summary>
    /// A click opens what is under it on ESPN (§369): target <paramref name="index"/>'s page, else the
    /// shown match's (<see cref="Site"/>); the folder lets go first, then <see cref="Open"/> (Dock.FolderForward).
    /// </summary>
    public override IslandAction Click(int index)
    {
        _ = press.Up();
        var links = Links(Layout(lastWidth > 0 ? lastWidth : FolderFlyout.ListWidth(1f), lastStyle, Read()));
        chosen = index >= 1 && index <= links.Count ? links[index - 1].Link : Site;
        Redrawn();
        return chosen is not null ? new(View: IslandView.App) : default;
    }

    /// <summary>The page the menu's Open website opens: the shown match's on ESPN, else the team's or the league's; null with nothing followed.</summary>
    internal string? Site => SiteOf(Read());

    /// <summary>The match's page on ESPN, else the team's own or the league's; null with nothing followed.</summary>
    internal static string? SiteOf(SportsReading reading) =>
        reading.View.Match is { Link: { } link } && SportsAnswers.Opens(link) ? link : PageOf(reading.Team);

    /// <summary>§369: the team's page on ESPN, or the league's; null with nothing followed.</summary>
    internal static string? PageOf(SportsOptions team) =>
        !team.IsSet ? null
        : team.IsTeam ? TeamPage(team.Sport, team.Team)
        : team.Sport == SportsLeagues.Soccer ? $"https://www.espn.com/soccer/league/_/name/{Uri.EscapeDataString(team.League)}"
        : $"https://www.espn.com/{Uri.EscapeDataString(team.League)}/";

    private static string TeamPage(string sport, string id) => $"https://www.espn.com/{sport}/team/_/id/{Uri.EscapeDataString(id)}";

    /// <summary>A match's page, else <paramref name="otherwise"/>.</summary>
    private static string? MatchPage(SportsMatch match, string? otherwise) =>
        SportsAnswers.Opens(match.Link) ? match.Link : otherwise;

    /// <summary>Opens the page the click chose, else <see cref="Site"/>, in the browser, once.</summary>
    internal void Open()
    {
        var link = chosen ?? Site;
        chosen = null;
        if (link is { Length: > 0 })
        {
            Opened(link);
        }
    }

    /// <summary>The folder's ticks turn the day over; nothing of its own on the minute but a kick-off's "Today".</summary>
    public override bool MinuteChanged(Gfx gfx) => false;

    /// <summary>A team or a league to fetch for ("Refresh", IRefreshable).</summary>
    public bool CanRefresh => options.IsSet;

    /// <summary>A fetch is on its way.</summary>
    public bool Refreshing
    {
        get
        {
            Take();
            return held.Fetching;
        }
    }

    /// <summary>When the matches shown were fetched.</summary>
    public DateTime? UpdatedUtc => held.Fetched;

    /// <summary>Why a newer answer is not there.</summary>
    string? IRefreshable.Problem
    {
        get
        {
            Take();
            return held.Problem;
        }
    }

    /// <summary>"Refresh": a fetch now whatever the age.</summary>
    public void RefreshNow()
    {
        Look();
        if (Source is null)
        {
            _ = Store.Ask(this);
        }
        Take();
    }

    public override void Dispose()
    {
        if (looked)
        {
            looked = false;
            Store.Release(this);
        }
        base.Dispose();
    }

    protected override bool Sample(bool everything)
    {
        Look();
        if (Source is null)
        {
            if (everything)
            {
                Store.Deliver(this);
            }
            Store.Poll(this);
        }
        Take();
        return false;
    }

    /// <summary>The first look: registered with the store and asked for if it is due (§11). Once.</summary>
    private void Look()
    {
        if (looked || Source is not null)
        {
            return;
        }
        looked = true;
        Store.Use(this, options);
        Store.Poll(this);
    }

    private void Take()
    {
        Look();
        held = Source is { } from ? from.Read() : Store.Read(this);
    }

    /// <summary>What it says now, through Clock.Now so a test can hold the time still.</summary>
    internal SportsReading Read()
    {
        Take();
        return SportsReading.From(options, held, Clock.Now().ToUniversalTime(), Store.Interval);
    }

    /// <summary>A logo for <paramref name="address"/>, drawn for the line's ground; null while it comes or where there is none.</summary>
    private Pixels? Logo(string address, bool dark) =>
        address.Length == 0 ? null : Source is { } from ? from.Logo?.Invoke(address) : Store.LogoOf(this, address, dark);

    /// <summary>Never drawn on a bar: on the bar it is the sports face.</summary>
    internal override void PaintCell(ID2D1DeviceContext* dc, int index, in IslandStyle style, float width, float height) =>
        throw new NotSupportedException($"{Info.Id} is a line of a folder's list, not a widget of a bar.");

    // --- The layout (§479) -------------------------------------------------------------------------

    /// <summary>The score's size on the scoreboard: the kit's Hero, 28 DIP.</summary>
    private static float BigPx(in TileStyle style) => style.Text(LineType.HeroDip);

    /// <summary>A side's logo on the scoreboard.</summary>
    private static float BoardLogo(in TileStyle style) => style.Text(28);

    /// <summary>A heading over a list: a small line with a gap over it and a tight step under it.</summary>
    private static float HeadingHeight(in ListForm form, in TileStyle style) => form.SmallLine + style.Px(LineSpace.GapDip) + style.Px(LineSpace.TightDip);

    /// <summary>The score at its Compact size: the kit's row with its caption.</summary>
    private bool Compact => list is null && LineSizes.Of(Settings) == LineSize.Compact;

    /// <summary>
    /// §479, §603: what the line draws <paramref name="width"/> px wide for <paramref name="reading"/>: the score its
    /// one row (Compact, or nothing to show) or its scoreboard; a list its rows laid out whole, top down, or one row
    /// saying why there are none.
    /// </summary>
    internal SportsLayout Layout(float width, in TileStyle style, SportsReading reading, float height = float.MaxValue)
    {
        lastWidth = width;
        lastStyle = style;
        // Laid out once for an answer, a minute, a width and a size: the pointer, the keys and the drawing ask often.
        var key = (held.Shown, held.Fetched, held.Problem, Clock.Now().Ticks / TimeSpan.TicksPerMinute, width, Compact, style.Scale, style.TextScale);
        if (laidKey.Equals(key) && laid is { } known)
        {
            return known;
        }
        laid = Laid(width, style, reading, height);
        laidKey = key;
        return laid;
    }

    private (SportsAnswer?, DateTime?, string?, long, float, bool, float, float) laidKey;
    private SportsLayout? laid;

    private SportsLayout Laid(float width, in TileStyle style, SportsReading reading, float height)
    {
        var one = new SportsLayout(One: true, [], LineRows.Px(1, style));
        var pages = reading.Pages;
        if (list is SportsPage.Table or SportsPage.Knockout && !pages.Contains(list.Value))
        {
            // §603: nothing of its own yet — why, or that its competition has none.
            return one with
            {
                Said = reading.State != SportsState.Ready ? reading.Said(Clock.Now()) : list == SportsPage.Table ? S.DockSportsNoTable : S.DockSportsNoKnockout,
            };
        }
        if (list is null or SportsPage.Matches && reading.View.Match is null || Compact)
        {
            return one;
        }
        var form = ListForm.Of(Gfx, style);
        var rows = new List<SportsRow>();
        var y = 0f;
        void Add(SportsRow row)
        {
            rows.Add(row with { Top = y });
            y += row.Height;
        }
        var heading = HeadingHeight(form, style);
        var item = LineRows.Px(1, style);
        var tall = LineRow.Height(style, caption: true);
        var dense = LineRow.Compact(style);
        float MatchHeight(string detail) => detail.Length > 0 ? tall : item;
        var stale = reading.Stale && reading.Updated.Length > 0;
        switch (list)
        {
            case null:
            {
                // The scoreboard, always as tall as with the line under the score: its height is its size's (§603).
                var board = reading.View;
                // §485: how it ended is the head's, with its day ("AET · 17 Mar").
                var boardDetail = SportsText.Detail(board.Match!, reading.HomeFirst, round: true);
                Add(new SportsRow(SportsRowKind.Board, 0, BoardHeight(style))
                {
                    Match = board.Match, Detail = boardDetail, Alarm = stale, Text = stale ? reading.Updated : board.Match!.LeagueName, Live = board.Shows == SportsShows.Live, Link = SiteOf(reading),
                });
                break;
            }
            case SportsPage.Table:
                foreach (var group in reading.Table.Where(group => group.Rows.Count > 0))
                {
                    Add(new SportsRow(SportsRowKind.Columns, 0, heading) { Text = group.Name.Length > 0 ? SportsRounds.Name(group.Name) : S.DockSportsTable, Group = group });
                    foreach (var place in group.Rows)
                    {
                        Add(new SportsRow(SportsRowKind.Place, 0, dense)
                        {
                            Place = place, Group = group, Own = reading.Team.IsTeam && place.Id == reading.Team.Team, Link = TeamPage(reading.Team.Sport, place.Id),
                        });
                    }
                }
                break;
            case SportsPage.Knockout:
                foreach (var round in reading.Knockout.GroupBy(match => match.Round))
                {
                    // §485: a round played whole quiet; the one on now, with its dot, and those to come strong.
                    Add(new SportsRow(SportsRowKind.Heading, 0, heading)
                    {
                        Text = round.Key.Length > 0 ? SportsRounds.Name(round.Key) : S.DockSportsKnockout,
                        Live = round.Any(match => match.State == MatchState.Live),
                        Tone = round.All(match => match.State is MatchState.Final or MatchState.Canceled) ? SportsTone.Quiet : SportsTone.Strong,
                    });
                    foreach (var match in round)
                    {
                        var detail = SportsText.Detail(match, reading.HomeFirst, round: false);
                        Add(new SportsRow(SportsRowKind.Match, 0, MatchHeight(detail)) { Match = match, Form = SportsMatchForm.League, Dated = true, Detail = detail, Link = MatchPage(match, PageOf(reading.Team)) });
                    }
                }
                break;
            default:
                var first = true;
                if (reading.League)
                {
                    var localNow = Clock.Now();
                    var today = DateOnly.FromDateTime(localNow);
                    // §484: the first heading names the competition, the line's first words.
                    string Lead() => first && stale ? reading.Updated : first ? reading.Team.Of?.Name ?? reading.Team.Name : "";
                    void Games(IEnumerable<SportsMatch> games, string shared)
                    {
                        foreach (var match in games)
                        {
                            var detail = SportsText.Detail(match, reading.HomeFirst, round: shared.Length == 0);
                            Add(new SportsRow(SportsRowKind.Match, 0, MatchHeight(detail)) { Match = match, Form = SportsMatchForm.League, Detail = detail, Link = MatchPage(match, PageOf(reading.Team)) });
                        }
                    }
                    // The round in a heading where every game under it is of it, else on each game.
                    static string Shared(IEnumerable<SportsMatch> games) => games.Select(match => match.Round).Distinct().ToList() is [var one] ? one : "";
                    // §485: the games on now first, under "Live now", whatever their day.
                    var live = reading.Day.Where(match => match.State == MatchState.Live).ToList();
                    if (live.Count > 0)
                    {
                        var shared = Shared(live);
                        Add(new SportsRow(SportsRowKind.Heading, 0, heading) { Lead = Lead(), Text = LineRow.Join(S.DockSportsLiveNow, SportsRounds.Name(shared)), Alarm = first && stale, Live = true, Tone = SportsTone.Live });
                        first = false;
                        Games(live, shared);
                    }
                    foreach (var day in reading.Day.Where(match => match.State != MatchState.Live).GroupBy(match => DateOnly.FromDateTime(match.StartUtc.ToLocalTime())))
                    {
                        var shared = Shared(day);
                        var said = LineRow.Join(SportsText.DayOf(day.First().StartUtc, localNow), SportsRounds.Name(shared));
                        // §485: a day gone by quiet, today and the days to come strong.
                        var tone = day.Key < today ? SportsTone.Quiet : SportsTone.Strong;
                        Add(first && stale
                            ? new SportsRow(SportsRowKind.Heading, 0, heading) { Text = reading.Updated, Alarm = true, Tone = tone }
                            : new SportsRow(SportsRowKind.Heading, 0, heading) { Lead = Lead(), Text = said, Tone = tone });
                        first = false;
                        Games(day, shared);
                    }
                    if (reading.View.Shows == SportsShows.Last)
                    {
                        Add(new SportsRow(SportsRowKind.Note, 0, item) { Text = S.DockSportsNone });
                    }
                    break;
                }
                // A team's: the match the score shows among them (§603: no scoreboard over them) — on now first, with the
                // league's others; the next first of those to come, the last first of the results.
                var view = reading.View;
                var shown = view.Match!;
                void List(string title, IEnumerable<SportsMatch> matches, SportsMatchForm kind, SportsTone tone)
                {
                    var all = matches.ToList();
                    if (all.Count == 0)
                    {
                        return;
                    }
                    // The stale reading's age leads the first heading, in the alarm's ink.
                    Add(new SportsRow(SportsRowKind.Heading, 0, heading)
                    {
                        Lead = first && stale ? reading.Updated : "", Alarm = first && stale, Text = title, Tone = tone,
                        Live = tone != SportsTone.Live && all.Any(match => match.State == MatchState.Live),
                    });
                    first = false;
                    foreach (var match in all)
                    {
                        // How a result ended and its date are at its end ("AET · 17 Mar"); a cup's round says its competition
                        // ("Taça de Portugal · Semifinals"), the team plays in several.
                        var detail = kind == SportsMatchForm.Others ? SportsText.Detail(match, reading.HomeFirst, round: false)
                            : LineRow.Join(match.Round.Length > 0 && !(kind == SportsMatchForm.Opponent && match.State != MatchState.Final && match.State != MatchState.Live) ? match.LeagueName : "",
                                SportsText.Detail(match, reading.HomeFirst, round: true));
                        Add(new SportsRow(SportsRowKind.Match, 0, MatchHeight(detail)) { Match = match, Form = kind, Dated = kind != SportsMatchForm.Others, Detail = detail, Link = MatchPage(match, null) });
                    }
                }
                // §485: what is on and to come strong, the results quiet.
                if (view.Shows == SportsShows.Live)
                {
                    List(S.DockSportsLiveNow, [shown], SportsMatchForm.Opponent, SportsTone.Live);
                    List(S.DockSportsOthers, view.Others, SportsMatchForm.Others, SportsTone.Strong);
                }
                List(S.DockSportsComing, view.Shows == SportsShows.Next ? [shown, .. view.Coming] : view.Coming, SportsMatchForm.Opponent, SportsTone.Strong);
                List(S.DockSportsPlayed, view.Shows is SportsShows.Final or SportsShows.Last ? [shown, .. view.Played] : view.Played, SportsMatchForm.Opponent, SportsTone.Quiet);
                if (!pages.Contains(SportsPage.Table) && reading.Standing.Length > 0)
                {
                    Add(new SportsRow(SportsRowKind.Note, 0, item) { Text = reading.Standing });
                }
                break;
        }
        return new SportsLayout(One: false, rows, y);
    }

    /// <summary>
    /// §603: the scoreboard's height, whatever it says: its head (the competition and the clock), the two sides about the
    /// score, the line under the score — the score's Full size.
    /// </summary>
    private float BoardHeight(in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var big = MathF.Max(TilePaint.LineHeight(Gfx, BigPx(style), Weight.Semibold), BoardLogo(style));
        return form.Pad + form.SmallLine + style.Px(4) + big + style.Px(2) + form.SmallLine + form.Pad;
    }

    /// <summary>The rows that open something, in order: target <c>n</c> is the <c>n</c>th.</summary>
    private static List<SportsRow> Links(SportsLayout layout) => [.. layout.Rows.Where(row => row.Link is { Length: > 0 })];

    /// <summary>The layout of the line in <paramref name="box"/>, the box its content is laid out in.</summary>
    private SportsLayout LayoutIn(D2D_RECT_F box, in TileStyle style) => Layout(box.right - box.left, style, Read(), box.bottom - box.top);

    /// <summary>The targets of <paramref name="layout"/> in a line <paramref name="width"/> px wide, from its top left: each row that opens something.</summary>
    private static List<(int Target, D2D_RECT_F Box)> Targets(SportsLayout layout, float width) =>
        [.. Links(layout).Select((row, at) => (at + 1, PopupPaint.Rect(0, row.Top, width, row.Height)))];

    // --- Clicks, tips and keys -------------------------------------------------------------------

    /// <summary>keys (§470): its targets as it lays them out in <paramref name="box"/> — every row that opens something — which the pointer and the keys both read.</summary>
    public IReadOnlyList<(int Target, D2D_RECT_F Box)> LineTargets(D2D_RECT_F box, in TileStyle style)
    {
        var layout = LayoutIn(box, style);
        return layout.One ? [] : [.. Targets(layout, box.right - box.left).Select(target => (target.Target, Moved(target.Box, box)))];
    }

    /// <summary>The number the folder carries for what is under <paramref name="point"/>: a row's, from 1; 0 for none.</summary>
    protected internal override int CardTarget(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var layout = LayoutIn(box, style);
        if (layout.One)
        {
            return 0;
        }
        var x = point.X - box.left;
        var y = point.Y - box.top;
        foreach (var (target, part) in Targets(layout, box.right - box.left))
        {
            if (x >= part.left && x < part.right && y >= part.top && y < part.bottom)
            {
                return target;
            }
        }
        return 0;
    }

    protected internal override void CardHover(Vector2? point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var target = point is { } at && !paintedOne ? CardTarget(at, box, style, surface) : 0;
        var let = press.Over(target);
        if (target == hot && !let)
        {
            return;
        }
        hot = target;
        _ = Wake();
    }

    /// <summary>A press on a row lights it pressed; the press stays the folder's, which makes it the click (RowPress).</summary>
    protected internal override void CardDown(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        if (!paintedOne && press.Down(CardTarget(point, box, style, surface)))
        {
            _ = Wake();
        }
    }

    /// <summary>§479: what is under the pointer said whole: a match, a place, a group's columns; the one row the whole reading.</summary>
    protected internal override (string Text, D2D_RECT_F Part)? CardTip(Vector2 point, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var reading = Read();
        var layout = Layout(box.right - box.left, style, reading, box.bottom - box.top);
        if (layout.One)
        {
            return reading.Tooltip(Clock.Now()) is { } said ? (said, box) : null;
        }
        var y = point.Y - box.top;
        foreach (var row in layout.Rows)
        {
            if (y < row.Top || y >= row.Bottom)
            {
                continue;
            }
            var part = new D2D_RECT_F(box.left, box.top + row.Top, box.right, box.top + row.Bottom);
            switch (row.Kind)
            {
                case SportsRowKind.Match or SportsRowKind.Board when row.Match is { } match:
                    return (SportsTips.Match(match, reading.HomeFirst, SportsLeagues.ClockOf(reading.Team.Sport, match.League.Length > 0 ? match.League : reading.Team.League), Clock.Now()), part);
                case SportsRowKind.Place when row.Place is { } place:
                    return (SportsTips.Place(place, reading.Team.Sport), part);
                case SportsRowKind.Columns:
                    return (SportsTips.Columns(row.Text, SportsColumns.Of(reading.Team.Sport, wide: true)), part);
                case SportsRowKind.Heading or SportsRowKind.Note when row.Words.Length > 0:
                    return (row.Words, part);
                default:
                    return null;
            }
        }
        return null;
    }

    /// <summary>§479: a table's own place, a knockout's round now, brought into sight once.</summary>
    public (float Top, float Bottom)? ScrollReveal(float width, in TileStyle style)
    {
        if (!reveal || list is not (SportsPage.Table or SportsPage.Knockout))
        {
            return null;
        }
        var layout = Layout(width, style, Read());
        if (layout.One)
        {
            return null;
        }
        reveal = false;
        return list switch
        {
            SportsPage.Table => layout.Rows.FirstOrDefault(row => row.Own) is { } own ? (own.Top, own.Bottom) : null,
            SportsPage.Knockout => Current(layout),
            _ => null,
        };
    }

    /// <summary>
    /// §485: a knockout's round now — the one a match of is on, else the first with a match to come — from its heading to
    /// its last match, to be brought into sight; null where every round is played (the list's top is then where it is).
    /// </summary>
    internal static (float Top, float Bottom)? Current(SportsLayout layout)
    {
        var rows = layout.Rows;
        var at = rows.ToList().FindIndex(row => row.Kind == SportsRowKind.Heading && row.Live);
        if (at < 0)
        {
            at = rows.ToList().FindIndex(row => row.Kind == SportsRowKind.Heading && row.Tone != SportsTone.Quiet);
        }
        if (at <= 0)
        {
            return null;
        }
        var end = at + 1;
        while (end < rows.Count && rows[end].Kind != SportsRowKind.Heading)
        {
            end++;
        }
        return (rows[at].Top, rows[end - 1].Bottom);
    }

    // --- Heights (§371, ListScroll) --------------------------------------------------------------

    /// <summary>
    /// list: the score its size's form, whatever it says (§603): one row, or the scoreboard. A list as tall as its
    /// content, up to the most its line may be (IListContent, §371) — the person's own rows, a Fill line's past any
    /// screen —, the rest scrolling (ListScroll.cs); the one row with nothing to show.
    /// </summary>
    public float ListHeight(float width, in TileStyle style)
    {
        if (list is null)
        {
            return Compact ? LineRow.Height(style, caption: true) : BoardHeight(style);
        }
        var most = LineRows.Most(Settings, style);
        var layout = Layout(width, style, Read(), most);
        return layout.One ? LineRows.Px(1, style) : MathF.Ceiling(MathF.Min(layout.Height, most));
    }

    /// <summary>A list's whole content, where it has rows; the one row and the score never scroll.</summary>
    public float ScrollHeight(float width, float height, in TileStyle style) =>
        list is not null && Layout(width, style, Read(), height) is { One: false } layout ? MathF.Ceiling(layout.Height) : 0;

    // --- The drawing ------------------------------------------------------------------------------

    protected internal override void PaintCard(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, WidgetSurface surface)
    {
        var reading = Read();
        var width = box.right - box.left;
        var layout = Layout(width, style, reading, box.bottom - box.top);
        var form = ListForm.Of(Gfx, style);
        paintedOne = layout.One;
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            var localNow = Clock.Now();
            if (layout.One)
            {
                One(dc, box, style, form, reading, layout.Said);
                return;
            }
            var number = 0;
            foreach (var row in layout.Rows)
            {
                var target = row.Link is { Length: > 0 } ? ++number : 0;
                var band = new D2D_RECT_F(box.left, box.top + row.Top, box.right, box.top + row.Bottom);
                if (Hidden(band.top, band.bottom))
                {
                    continue;
                }
                if (target > 0 && hot == target)
                {
                    LineItem.Light(dc, band, style.Theme, press.On(target));
                }
                switch (row.Kind)
                {
                    case SportsRowKind.Heading:
                        Heading(dc, box, style, form, row, reading.Stale);
                        break;
                    case SportsRowKind.Note:
                        TilePaint.Line(dc, Gfx, row.Text, form.SmallPx, Weight.Regular, style.Theme.TextSecondary, box.left + form.Inset,
                            PopupPaint.Round(band.top + ((row.Height - form.SmallLine) / 2)), MathF.Max(1, width - (2 * form.Inset)));
                        break;
                    case SportsRowKind.Board:
                        Board(dc, box, style, form, reading, row, localNow);
                        break;
                    case SportsRowKind.Match:
                        MatchRow(dc, box, style, form, reading, row, localNow);
                        break;
                    case SportsRowKind.Columns:
                        ColumnsRow(dc, box, style, form, reading, row);
                        break;
                    case SportsRowKind.Place:
                        PlaceRow(dc, box, style, form, reading, row);
                        break;
                }
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>The red of a match on now: the ink Windows' own alarms are drawn in.</summary>
    private static DXGI_RGBA LiveInk(in TileStyle style) => new IslandInk(style.Look).Alarm;

    private static D2D_RECT_F Moved(D2D_RECT_F part, D2D_RECT_F box) => new(box.left + part.left, box.top + part.top, box.left + part.right, box.top + part.bottom);

    /// <summary>
    /// A list's heading: its lead (the competition, or the stale reading's age in the alarm's ink) quiet, then its words in
    /// their tone (§485): a day gone by or a round played whole quiet, today, the days and rounds to come strong, "Live now"
    /// in the live red after a red dot; a red dot at the end of any other while one of its matches is on.
    /// </summary>
    private void Heading(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SportsRow row, bool stale)
    {
        var theme = style.Theme;
        var top = box.top + row.Bottom - style.Px(LineSpace.TightDip) - form.SmallLine;
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var dot = style.Px(6);
        var liveInk = stale ? theme.TextSecondary : LiveInk(style);
        if (row.Live && row.Tone != SportsTone.Live)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(right - dot, PopupPaint.Round(top + ((form.SmallLine - dot) / 2)), dot, dot), dot / 2, liveInk);
            right -= dot + style.Gap;
        }
        if (row.Lead.Length > 0)
        {
            var lead = row.Lead + LineRow.Dot;
            TilePaint.Line(dc, Gfx, lead, form.SmallPx, Weight.Semibold, row.Alarm ? LiveInk(style) : theme.TextSecondary, left, top, MathF.Max(1, right - left));
            left += PopupPaint.Measure(Gfx, lead, form.SmallPx, Weight.Semibold).X;
        }
        if (row.Tone == SportsTone.Live && left + dot < right)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(PopupPaint.Round(left), PopupPaint.Round(top + ((form.SmallLine - dot) / 2)), dot, dot), dot / 2, liveInk);
            left += dot + style.Px(4);
        }
        var ink = row.Alarm && row.Lead.Length == 0 ? LiveInk(style) : row.Tone switch
        {
            SportsTone.Live => liveInk,
            SportsTone.Strong => theme.Text,
            _ => theme.TextSecondary,
        };
        if (left < right)
        {
            TilePaint.Line(dc, Gfx, row.Text, form.SmallPx, Weight.Semibold, ink, left, top, MathF.Max(1, right - left));
        }
    }

    /// <summary>
    /// One row: the dot or the logo in the leading column, the score or the opponent, and the clock or the day at the end;
    /// or, with nothing to show or <paramref name="why"/> given (a list with nothing of its own), the glyph and why.
    /// </summary>
    private void One(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SportsReading reading, string? why = null)
    {
        var theme = style.Theme;
        var localNow = Clock.Now();
        var height = TilePaint.Height(box);
        var middle = box.top + (height / 2);
        if (why is not null || reading.View.Match is not { } match)
        {
            // The kit's Status (§388): on the line's top row, never in the middle of a taller size (§603: the score's
            // Full keeps its five cells while it waits or has nothing).
            var said = why ?? reading.Said(localNow);
            var top = box.top + PopupPaint.Round(MathF.Min(height, LineRows.Px(1, style)) / 2);
            PopupPaint.Glyph(dc, Gfx, Info.Glyph, form.Glyph, theme.TextSecondary, form.IconBox(box.left, top - (form.Line / 2), form.Line));
            TilePaint.Line(dc, Gfx, said, form.TextPx, Weight.Regular, theme.TextSecondary, box.left + form.TextLeft, PopupPaint.Round(top - (form.Line / 2)), MathF.Max(1, form.Room(TilePaint.Width(box))));
            return;
        }
        var live = reading.View.Shows == SportsShows.Live;
        var trail = SportsText.Trail(match, reading.View.Shows, reading.Clock, localNow);
        var field = PopupPaint.Measure(Gfx, trail, form.TextPx, Weight.Semibold).X;
        string[] names = !reading.League && reading.View.Shows is SportsShows.Next
            ? [SportsText.Opponent(match, reading.Team.Team, named: true), SportsText.Opponent(match, reading.Team.Team)]
            : [SportsText.Named(match, reading.HomeFirst), SportsText.Score(match, reading.HomeFirst)];
        // §603: the score's row says under it what its scoreboard says over and under the sides: the competition (or the
        // stale reading's age) and the round, the leg, how a shoot-out or a tie came out.
        var caption = list is null ? LineRow.Join(reading.Stale && reading.Updated.Length > 0 ? reading.Updated : match.LeagueName, SportsText.Detail(match, reading.HomeFirst, round: true)) : "";
        var rowBox = caption.Length == 0 ? box : box with
        {
            top = PopupPaint.Round(middle - ((form.Line + form.SmallLine) / 2)),
            bottom = PopupPaint.Round(middle - ((form.Line + form.SmallLine) / 2)) + form.Line,
        };
        var row = ListTiers.Row(Gfx, rowBox, style, names, Weight.Semibold, field);
        if (caption.Length > 0)
        {
            TilePaint.Line(dc, Gfx, caption, form.SmallPx, Weight.Regular, reading.Stale && reading.Updated.Length > 0 ? LiveInk(style) : theme.TextSecondary, row.NameLeft, rowBox.bottom,
                MathF.Max(1, box.right - form.Inset - row.NameLeft));
        }
        var icon = form.IconBox(box.left, row.Middle - (form.Line / 2), form.Line);
        if (live)
        {
            var dot = style.Px(8);
            PopupPaint.Fill(dc, PopupPaint.Rect(PopupPaint.Round(((icon.left + icon.right) / 2) - (dot / 2)), PopupPaint.Round(row.Middle - (dot / 2)), dot, dot), dot / 2, reading.Stale ? theme.TextSecondary : LiveInk(style));
        }
        else
        {
            var side = form.Glyph;
            var at = PopupPaint.Rect(PopupPaint.Round(((icon.left + icon.right) / 2) - (side / 2)), PopupPaint.Round(row.Middle - (side / 2)), side, side);
            if (reading.League)
            {
                if (Logo(reading.Logo, style.Look.Dark) is { } league)
                {
                    PopupPaint.Picture(dc, league, at, 0);
                }
                else
                {
                    PopupPaint.Glyph(dc, Gfx, Info.Glyph, form.Glyph, theme.TextSecondary, icon);
                }
            }
            else
            {
                var (own, _, _) = match.Sides(reading.Team.Team);
                Badge(dc, style, own.Logo.Length > 0 ? own : new SportsSide { Id = own.Id, Abbreviation = own.Abbreviation, Name = own.Name, Logo = reading.Logo }, at);
            }
        }
        if (row.Name is { } name)
        {
            TilePaint.Line(dc, Gfx, name, form.TextPx, Weight.Semibold, reading.Stale ? theme.TextSecondary : theme.Text, row.NameLeft, row.TextTop, row.NameRoom);
        }
        TilePaint.LineRight(dc, Gfx, trail, form.TextPx, Weight.Semibold, live && !reading.Stale ? LiveInk(style) : theme.TextSecondary, row.FieldRight, row.TextTop, row.Field);
    }

    /// <summary>
    /// The scoreboard: the competition (or the reading's age, where it is stale) and the clock or the day over the
    /// two sides, each with its logo and name, either side of the score; under the score its round, its leg and how
    /// a shoot-out or a tie came out (§479). The side that did not win or go through is quiet after the match.
    /// </summary>
    private void Board(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SportsReading reading, SportsRow row, DateTime localNow)
    {
        var theme = style.Theme;
        var match = row.Match!;
        var shows = reading.View.Shows;
        var live = shows == SportsShows.Live;
        var left = box.left + form.Inset;
        var right = box.right - form.Inset;
        var top = box.top + row.Top + form.Pad;

        var status = Status(match, shows, reading.Clock, localNow);
        var statusWidth = PopupPaint.Measure(Gfx, status, form.SmallPx, Weight.Semibold).X;
        var dot = live ? style.Px(6) : 0;
        var statusLeft = right - statusWidth - (dot > 0 ? dot + style.Px(4) : 0);
        if (live)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(statusLeft, PopupPaint.Round(top + ((form.SmallLine - dot) / 2)), dot, dot), dot / 2, reading.Stale ? theme.TextSecondary : LiveInk(style));
        }
        TilePaint.LineRight(dc, Gfx, status, form.SmallPx, Weight.Semibold, live && !reading.Stale ? LiveInk(style) : theme.TextSecondary, right, top, statusWidth + 1);
        if (row.Text.Length > 0)
        {
            TilePaint.Line(dc, Gfx, row.Text, form.SmallPx, Weight.Regular, row.Alarm ? LiveInk(style) : theme.TextSecondary, left, top, MathF.Max(1, statusLeft - style.Gap - left));
        }

        var big = MathF.Max(TilePaint.LineHeight(Gfx, BigPx(style), Weight.Semibold), BoardLogo(style));
        var y = top + form.SmallLine + style.Px(4);
        var (first, second) = reading.HomeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        var scored = shows is SportsShows.Live or SportsShows.Final or SportsShows.Last && first.Score.Length > 0 && second.Score.Length > 0;
        var middle = scored ? $"{first.Score} – {second.Score}" : SportsText.Word(match) is { } word ? word : "vs";
        var middlePx = scored ? BigPx(style) : form.TextPx;
        var middleWeight = scored ? Weight.Semibold : Weight.Regular;
        var middleWidth = PopupPaint.Measure(Gfx, middle, middlePx, middleWeight).X;
        var centre = (left + right) / 2;
        var middleLine = TilePaint.LineHeight(Gfx, middlePx, middleWeight);
        TilePaint.Line(dc, Gfx, middle, middlePx, middleWeight, scored && !reading.Stale ? theme.Text : theme.TextSecondary,
            PopupPaint.Round(centre - (middleWidth / 2)), PopupPaint.Round(y + ((big - middleLine) / 2)), middleWidth + 1);
        var logo = BoardLogo(style);
        var room = MathF.Max(1, centre - (middleWidth / 2) - style.Gap - left - logo - style.Gap);
        var nameTop = PopupPaint.Round(y + ((big - form.Line) / 2));
        var logoTop = PopupPaint.Round(y + ((big - logo) / 2));
        var ended = shows is SportsShows.Final or SportsShows.Last;
        bool Through(SportsSide side) => first.Advances || second.Advances ? side.Advances : side.Winner;
        bool Quiet(SportsSide side) => ended && (Through(first) || Through(second)) && !Through(side);
        Badge(dc, style, first, PopupPaint.Rect(left, logoTop, logo, logo));
        if (ListTiers.Name(Gfx, Names(first), form.TextPx, Weight.Semibold, room) is { } one)
        {
            TilePaint.Line(dc, Gfx, one, form.TextPx, Weight.Semibold, Quiet(first) ? theme.TextSecondary : theme.Text, left + logo + style.Gap, nameTop, room);
        }
        Badge(dc, style, second, PopupPaint.Rect(right - logo, logoTop, logo, logo));
        if (ListTiers.Name(Gfx, Names(second), form.TextPx, Weight.Semibold, room) is { } two)
        {
            TilePaint.LineRight(dc, Gfx, two, form.TextPx, Weight.Semibold, Quiet(second) ? theme.TextSecondary : theme.Text, right - logo - style.Gap, nameTop, room);
        }
        if (row.Detail.Length > 0)
        {
            // Under the score, in the middle of the line.
            var detailWidth = MathF.Min(right - left, PopupPaint.Measure(Gfx, row.Detail, form.SmallPx, Weight.Regular).X);
            TilePaint.Line(dc, Gfx, row.Detail, form.SmallPx, Weight.Regular, theme.TextSecondary, PopupPaint.Round(MathF.Max(left, centre - (detailWidth / 2))), y + big + style.Px(2), right - left);
        }
    }

    /// <summary>
    /// §485: what the scoreboard's head says at its end: "Live · 67'" while it is on, how it ended a few hours after,
    /// how it ended and its day once it is the last played ("AET · 17 Mar"), else its kick-off or the word for one put off.
    /// </summary>
    internal static string Status(SportsMatch match, SportsShows shows, SportsClock clock, DateTime localNow) => shows switch
    {
        SportsShows.Live => LineRow.Join(S.DockSportsLive, SportsText.Clock(match, clock)),
        SportsShows.Last => SportsText.Row(match, clock, localNow, null),
        _ => SportsText.Trail(match, shows, clock, localNow),
    };

    /// <summary>A side's names, fullest first: its full name, its short one, its abbreviation.</summary>
    private static string[] Names(SportsSide side) => [side.FullName, side.Name, SportsText.Abbreviation(side)];

    /// <summary>
    /// A match's row: the leading column's logo (the home side's, or the opponent's in a team's lists, or a dot while
    /// a league's other match is on), the two sides about the score — the fullest names that fit — or the opponent,
    /// its clock, how it ended or its kick-off at the end, and its detail under them (§479).
    /// </summary>
    private void MatchRow(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SportsReading reading, SportsRow row, DateTime localNow)
    {
        var theme = style.Theme;
        var match = row.Match!;
        var right = box.right - form.Inset;
        var textLeft = box.left + form.TextLeft;
        var lines = form.Line + (row.Detail.Length > 0 ? form.SmallLine : 0);
        var top = PopupPaint.Round(box.top + row.Top + ((row.Height - lines) / 2));
        var live = match.State == MatchState.Live;
        var clock = SportsLeagues.ClockOf(reading.Team.Sport, match.League.Length > 0 ? match.League : reading.Team.League);
        // §485: under a day's heading its time or how it ended; under none (a round, a team's lists) a result's day too.
        var trail = SportsText.Row(match, clock, localNow, row.Dated ? null : DateOnly.FromDateTime(match.StartUtc.ToLocalTime()));
        // §485: on now, a red dot before its clock, both strong in the live red; else quiet.
        var on = live && !reading.Stale;
        var trailWeight = live ? Weight.Semibold : Weight.Regular;
        var trailWidth = PopupPaint.Measure(Gfx, trail, form.TextPx, trailWeight).X;
        // Its shortest where the whole would leave its names no room for their abbreviations (the largest text, the
        // narrowest list): a result's day, a kick-off's day or time; its tooltip says the rest.
        if (row.Dated && right - trailWidth - style.Gap - textLeft < PopupPaint.Measure(Gfx, SportsText.Score(match, reading.HomeFirst), form.TextPx, Weight.Regular).X
            && SportsText.Short(match, localNow) is { } shorter)
        {
            trail = shorter;
            trailWidth = PopupPaint.Measure(Gfx, trail, form.TextPx, trailWeight).X;
        }
        TilePaint.LineRight(dc, Gfx, trail, form.TextPx, trailWeight, on ? LiveInk(style) : theme.TextSecondary, right, top, trailWidth + 1);
        var field = trailWidth;
        if (live)
        {
            var dot = style.Px(6);
            PopupPaint.Fill(dc, PopupPaint.Rect(PopupPaint.Round(right - trailWidth - style.Px(4) - dot), PopupPaint.Round(top + ((form.Line - dot) / 2)), dot, dot), dot / 2, on ? LiveInk(style) : theme.TextSecondary);
            field += style.Px(4) + dot;
        }
        var room = MathF.Max(1, right - field - style.Gap - textLeft);
        var icon = form.IconBox(box.left, top, form.Line);
        var side = form.Glyph;
        var badge = PopupPaint.Rect(PopupPaint.Round(((icon.left + icon.right) / 2) - (side / 2)), PopupPaint.Round(top + ((form.Line - side) / 2)), side, side);
        var scored = match.State is MatchState.Final or MatchState.Live;
        string[] names;
        if (row.Form == SportsMatchForm.Opponent)
        {
            var (_, other, _) = match.Sides(reading.Team.Team);
            Badge(dc, style, other, badge);
            names = scored
                ? [SportsText.Named(match, reading.HomeFirst, full: true), SportsText.Named(match, reading.HomeFirst), SportsText.Score(match, reading.HomeFirst)]
                : match.LeagueName.Length > 0
                    ? [$"{SportsText.Opponent(match, reading.Team.Team, named: true)} · {match.LeagueName}", SportsText.Opponent(match, reading.Team.Team, named: true), SportsText.Opponent(match, reading.Team.Team)]
                    : [SportsText.Opponent(match, reading.Team.Team, named: true), SportsText.Opponent(match, reading.Team.Team)];
        }
        else
        {
            Badge(dc, style, reading.HomeFirst ? match.Home : match.Away, badge);
            names = [SportsText.Named(match, reading.HomeFirst, full: true), SportsText.Named(match, reading.HomeFirst), SportsText.Score(match, reading.HomeFirst)];
        }
        var weight = live ? Weight.Semibold : Weight.Regular;
        if (ListTiers.Name(Gfx, names, form.TextPx, weight, room) is { } name)
        {
            Result(dc, style, form, match, reading.HomeFirst, name, weight, textLeft, top, room);
        }
        if (row.Detail.Length > 0)
        {
            TilePaint.Line(dc, Gfx, row.Detail, form.SmallPx, Weight.Regular, theme.TextSecondary, textLeft, top + form.Line, MathF.Max(1, right - textLeft));
        }
    }

    /// <summary>
    /// §485: a match's names about its score, <paramref name="name"/> the form of them that fits: a result's losing side
    /// quiet, the winner and the score as the text is; anything else — a draw, a game on or to come, a form cut short —
    /// all in the text's ink.
    /// </summary>
    private void Result(ID2D1DeviceContext* dc, in TileStyle style, in ListForm form, SportsMatch match, bool homeFirst, string name, Weight weight, float left, float top, float room)
    {
        var theme = style.Theme;
        var (first, second) = homeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        var (firstLost, secondLost) = (SportsText.Lost(match, first), SportsText.Lost(match, second));
        if ((firstLost || secondLost) && Parts(match, homeFirst, name) is { } parts && PopupPaint.Measure(Gfx, name, form.TextPx, weight).X <= room)
        {
            var scoreLeft = left + PopupPaint.Measure(Gfx, parts.First, form.TextPx, weight).X;
            var secondLeft = left + PopupPaint.Measure(Gfx, parts.First + parts.Score, form.TextPx, weight).X;
            TilePaint.Line(dc, Gfx, parts.First, form.TextPx, weight, firstLost ? theme.TextSecondary : theme.Text, left, top, scoreLeft - left + 1);
            TilePaint.Line(dc, Gfx, parts.Score, form.TextPx, weight, theme.Text, scoreLeft, top, secondLeft - scoreLeft + 1);
            TilePaint.Line(dc, Gfx, parts.Second, form.TextPx, weight, secondLost ? theme.TextSecondary : theme.Text, secondLeft, top, MathF.Max(1, left + room - secondLeft));
            return;
        }
        TilePaint.Line(dc, Gfx, name, form.TextPx, weight, theme.Text, left, top, room);
    }

    /// <summary>
    /// §485: <paramref name="name"/> — one of <see cref="SportsText.Named"/>'s or <see cref="SportsText.Score"/>'s forms of
    /// <paramref name="match"/> — in its three parts, "Germany", " 1–1", " Paraguay", each space leading so each part's
    /// width is measured whole; null where it is none of them.
    /// </summary>
    internal static (string First, string Score, string Second)? Parts(SportsMatch match, bool homeFirst, string name)
    {
        var (first, second) = homeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        if (first.Score.Length == 0 || second.Score.Length == 0)
        {
            return null;
        }
        var score = $" {first.Score}–{second.Score}";
        static string Full(SportsSide side) => side.FullName.Length > 0 ? side.FullName : Short(side);
        static string Short(SportsSide side) => side.Name.Length > 0 ? side.Name : SportsText.Abbreviation(side);
        foreach (var (one, two) in new[] { (Full(first), Full(second)), (Short(first), Short(second)), (SportsText.Abbreviation(first), SportsText.Abbreviation(second)) })
        {
            if (name == one + score + " " + two)
            {
                return (one, score, " " + two);
            }
        }
        return null;
    }

    /// <summary>§479: a group's heading: its name at the gutter, its columns' letters over their figures.</summary>
    private void ColumnsRow(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SportsReading reading, SportsRow row)
    {
        var top = box.top + row.Bottom - style.Px(LineSpace.TightDip) - form.SmallLine;
        var (columns, widths, _) = Fit(reading, row.Group!, box.right - box.left, style, form);
        var right = box.right - form.Inset;
        for (var at = columns.Count - 1; at >= 0; at--)
        {
            TilePaint.LineRight(dc, Gfx, columns[at].Head, form.SmallPx, Weight.Semibold, style.Theme.TextSecondary, right, top, widths[at]);
            right -= widths[at];
        }
        TilePaint.Line(dc, Gfx, row.Text, form.SmallPx, Weight.Semibold, style.Theme.TextSecondary, box.left + form.Inset, top, MathF.Max(1, right - style.Gap - box.left - form.Inset));
    }

    /// <summary>
    /// §479: the columns a table <paramref name="width"/> px wide shows for <paramref name="group"/> and how wide each is:
    /// all of its sport's where the team's name keeps 120 DIP beside them, else the few that matter, else the points
    /// alone; and whether the logo stands before the name (not where even then the name would not read).
    /// </summary>
    private (IReadOnlyList<SportsColumn> Columns, float[] Widths, bool Logo) Fit(SportsReading reading, SportsGroup group, float width, in TileStyle style, in ListForm form)
    {
        // Measured once a group, a width and a size: every row of the group asks.
        var key = (group, width, style.Scale, style.TextScale);
        if (fits.TryGetValue(key, out var known))
        {
            return known;
        }
        if (fits.Count > 64)
        {
            fits.Clear();
        }
        return fits[key] = Fitted(reading, group, width, style, form);
    }

    private readonly Dictionary<(SportsGroup, float, float, float), (IReadOnlyList<SportsColumn>, float[], bool)> fits = [];

    private (IReadOnlyList<SportsColumn> Columns, float[] Widths, bool Logo) Fitted(SportsReading reading, SportsGroup group, float width, in TileStyle style, in ListForm form)
    {
        var gfx = Gfx;
        var textPx = form.TextPx;
        var smallPx = form.SmallPx;
        var air = style.Px(10);
        (IReadOnlyList<SportsColumn>, float[]) Measure(IReadOnlyList<SportsColumn> columns) =>
            (columns, [.. columns.Select(column => MathF.Ceiling(MathF.Max(
                PopupPaint.Measure(gfx, column.Head, smallPx, Weight.Semibold).X,
                group.Rows.Select(place => PopupPaint.Measure(gfx, column.Value(place), textPx, Weight.Semibold).X).DefaultIfEmpty(0).Max())) + air)]);
        var left = width - form.Inset - style.Gap - NameLeft(style, form, logo: true);
        // Every name keeps at least its abbreviation (or its first letters and "…", ListTiers.Name).
        var least = group.Rows.Select(place => PopupPaint.Measure(gfx, place.Abbreviation.Length is > 0 and <= 4 ? place.Abbreviation : "MMM…", textPx, Weight.Semibold).X).DefaultIfEmpty(0).Max();
        var full = Measure(SportsColumns.Of(reading.Team.Sport, wide: true));
        if (left - full.Item2.Sum() >= MathF.Max(least, style.Text(120)))
        {
            return (full.Item1, full.Item2, true);
        }
        // Narrower, the few that matter; at the narrowest and largest text, the points alone, and the logo goes before the name does.
        var few = Measure(SportsColumns.Of(reading.Team.Sport, wide: false));
        if (left - few.Item2.Sum() >= least)
        {
            return (few.Item1, few.Item2, true);
        }
        var alone = Measure(SportsColumns.Least(reading.Team.Sport));
        return (alone.Item1, alone.Item2, left - alone.Item2.Sum() >= least);
    }

    /// <summary>Tests: the room a place's name has in <paramref name="group"/>'s table <paramref name="width"/> px wide, and its columns.</summary>
    internal (float Room, int Columns) NameRoom(SportsReading reading, SportsGroup group, float width, in TileStyle style)
    {
        var form = ListForm.Of(Gfx, style);
        var (columns, widths, logo) = Fit(reading, group, width, style, form);
        return (width - form.Inset - widths.Sum() - style.Gap - NameLeft(style, form, logo), columns.Count);
    }

    /// <summary>Where a place's team's name begins, from the line's leading edge: after the place and the logo where <paramref name="logo"/>.</summary>
    private float NameLeft(in TileStyle style, in ListForm form, bool logo) =>
        form.Inset + PopupPaint.Measure(Gfx, "88", form.TextPx, Weight.Semibold).X + style.Gap + (logo ? form.Glyph + style.Gap : 0);

    /// <summary>
    /// §479: a place in a table: its zone's colour at the leading edge, the place, the logo, the fullest name that fits,
    /// and its columns' figures, the last (the points, or the games behind) strong; the team's own row strong.
    /// </summary>
    private void PlaceRow(ID2D1DeviceContext* dc, D2D_RECT_F box, in TileStyle style, in ListForm form, SportsReading reading, SportsRow row)
    {
        var theme = style.Theme;
        var place = row.Place!;
        var band = box.top + row.Top;
        var top = PopupPaint.Round(band + ((row.Height - form.Line) / 2));
        var weight = row.Own ? Weight.Semibold : Weight.Regular;
        var left = box.left + form.Inset;
        if (Zone(place.Zone) is { } zone)
        {
            var thick = MathF.Max(2, style.Px(3));
            PopupPaint.Fill(dc, PopupPaint.Rect(box.left + PopupPaint.Round((form.Inset - thick) / 2), band + style.Px(6), thick, row.Height - style.Px(12)), thick / 2, zone);
        }
        var rankWidth = PopupPaint.Measure(Gfx, "88", form.TextPx, Weight.Semibold).X;
        TilePaint.LineRight(dc, Gfx, place.Rank.ToString(global::System.Globalization.CultureInfo.CurrentCulture), form.TextPx, weight, row.Own ? theme.Text : theme.TextSecondary, left + rankWidth, top, rankWidth + 1);
        var side = form.Glyph;
        var logoLeft = left + rankWidth + style.Gap;
        var (columns, widths, logo) = Fit(reading, row.Group!, box.right - box.left, style, form);
        if (logo)
        {
            Badge(dc, style, new SportsSide { Id = place.Id, Abbreviation = place.Abbreviation, Name = place.Name, Logo = place.Logo }, PopupPaint.Rect(PopupPaint.Round(logoLeft), PopupPaint.Round(band + ((row.Height - side) / 2)), side, side));
        }
        var right = box.right - form.Inset;
        for (var at = columns.Count - 1; at >= 0; at--)
        {
            var last = at == columns.Count - 1;
            TilePaint.LineRight(dc, Gfx, columns[at].Value(place), form.TextPx, last || row.Own ? Weight.Semibold : Weight.Regular, last ? theme.Text : theme.TextSecondary, right, top, widths[at]);
            right -= widths[at];
        }
        var nameLeft = box.left + NameLeft(style, form, logo);
        var room = MathF.Max(1, right - style.Gap - nameLeft);
        if (ListTiers.Name(Gfx, [place.FullName, place.Name, place.Abbreviation], form.TextPx, weight, room) is { } name)
        {
            TilePaint.Line(dc, Gfx, name, form.TextPx, weight, theme.Text, nameLeft, top, room);
        }
    }

    /// <summary>ESPN's "#81D6AC" as an ink; null for none.</summary>
    private static DXGI_RGBA? Zone(string color) =>
        color.Length == 7 && uint.TryParse(color.AsSpan(1), global::System.Globalization.NumberStyles.HexNumber, global::System.Globalization.CultureInfo.InvariantCulture, out var rgb)
            ? new DXGI_RGBA(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1)
            : null;

    /// <summary>
    /// A side's logo in <paramref name="at"/>; until it comes, or where it has none, its monogram on a disc
    /// of a colour of its own (§369: the same side the same colour everywhere), so no two sides look alike.
    /// </summary>
    private void Badge(ID2D1DeviceContext* dc, in TileStyle style, SportsSide side, D2D_RECT_F at)
    {
        if (Logo(side.Logo, style.Look.Dark) is { } pixels)
        {
            PopupPaint.Picture(dc, pixels, at, 0);
            return;
        }
        var size = at.right - at.left;
        PopupPaint.Fill(dc, at, size / 2, SportsText.Tint(side.Id.Length > 0 ? side.Id : side.Abbreviation));
        var letters = SportsText.Monogram(side, size >= style.Px(24) ? 2 : 1);
        var px = MathF.Max(style.Px(8), size * (letters.Length > 1 ? 0.36f : 0.5f));
        var width = PopupPaint.Measure(Gfx, letters, px, Weight.Semibold).X;
        var line = TilePaint.LineHeight(Gfx, px, Weight.Semibold);
        TilePaint.Line(dc, Gfx, letters, px, Weight.Semibold, new DXGI_RGBA(1, 1, 1, 1), PopupPaint.Round(at.left + ((size - width) / 2)), PopupPaint.Round(at.top + ((size - line) / 2)), width + 1);
    }
}

/// <summary>§479: what a row of a sports line is.</summary>
internal enum SportsRowKind
{
    /// <summary>A list's heading: a day, a round, "Coming up".</summary>
    Heading,

    /// <summary>A team's shown match, its sides about the score.</summary>
    Board,

    /// <summary>A match.</summary>
    Match,

    /// <summary>A group's heading over its columns.</summary>
    Columns,

    /// <summary>A team's place in a table.</summary>
    Place,

    /// <summary>A quiet line: where the team stands, or that nothing is coming.</summary>
    Note,
}

/// <summary>§485: a heading's tone: what is under it done, on or to come.</summary>
internal enum SportsTone
{
    /// <summary>Over what is done — a day gone by, a round played whole, the results —: the quiet text's ink.</summary>
    Quiet,

    /// <summary>Over what is on or to come — today, the days and rounds ahead, "Coming up" —: the text's own ink.</summary>
    Strong,

    /// <summary>"Live now", over the games on now: a red dot, and the words in the live red.</summary>
    Live,
}

/// <summary>§479: how a match's row names it.</summary>
internal enum SportsMatchForm
{
    /// <summary>The two sides about the score, the home side's logo: a league's game.</summary>
    League,

    /// <summary>"vs Real Madrid · LALIGA", the opponent's logo: a followed team's.</summary>
    Opponent,

    /// <summary>The league's other match of the day, a dot while it is on.</summary>
    Others,
}

/// <summary>§479: one row of a sports line, laid out (<see cref="SportsTile.Layout"/>): what it is, where it starts and how tall it is, from the content's top.</summary>
internal sealed record SportsRow(SportsRowKind Kind, float Top, float Height)
{
    public string Text { get; init; } = "";

    /// <summary>§485: a heading's words before its own, quiet: the competition the line's first heading names, or the stale reading's age.</summary>
    public string Lead { get; init; } = "";

    /// <summary>A heading's words whole, its lead first: what its tooltip says.</summary>
    public string Words => LineRow.Join(Lead, Text);

    /// <summary>The words (or the lead, where there is one) are the stale reading's age, in the alarm's ink.</summary>
    public bool Alarm { get; init; }

    /// <summary>Something of it is on now: a red dot.</summary>
    public bool Live { get; init; }

    /// <summary>§485: how a heading's own words are drawn: over what is done quiet, over what is on or to come strong, "Live now" red.</summary>
    public SportsTone Tone { get; init; }

    public SportsMatch? Match { get; init; }

    public SportsMatchForm Form { get; init; }

    /// <summary>Its kick-off says its day too: it stands under no day's heading.</summary>
    public bool Dated { get; init; }

    /// <summary>The line under a match: its round, its leg, how a shoot-out or a tie came out.</summary>
    public string Detail { get; init; } = "";

    public SportsGroup? Group { get; init; }

    public SportsPlace? Place { get; init; }

    /// <summary>The followed team's own place: strong.</summary>
    public bool Own { get; init; }

    /// <summary>What a click on it opens; null for nothing.</summary>
    public string? Link { get; init; }

    public float Bottom => Top + Height;
}

/// <summary>
/// §479: what a sports line draws (<see cref="SportsTile.Layout"/>): the one row, or its rows from the content's top,
/// <paramref name="Height"/> px of them, a list's of which scroll.
/// </summary>
internal sealed record SportsLayout(bool One, IReadOnlyList<SportsRow> Rows, float Height)
{
    /// <summary>§603: what the one row says instead of the match: why a list has nothing of its own; null for the match.</summary>
    public string? Said { get; init; }
}
