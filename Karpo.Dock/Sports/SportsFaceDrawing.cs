// The sports face (Karpo.Core\FolderFaces.cs, docs\widgets.md §239, §369): the most pressing match of
// what the folder's sports lines follow on the bar — a game on now before a result just in, before the
// soonest next game (SportsPick.Lead) — "FCB 2–1 RMA 67'" after a red dot while it is on, "vs RMA Sat
// 20:00" before it, "FCB 2–1 RMA Final" for a few hours after; a league's game "MUN – DOR Sat 18:30".
// The answers are the ones the sports store keeps for the lines' teams and leagues, which the folder
// reads as users of its own (FolderIsland.Sports), so a face and a line of one team are one fetch.
//
// Across a bar it is as wide as what it says now (§244, which ends §12.2's rule for faces), and the
// dot while the match is on, up to the room of the widest thing the face can say — a score with the
// leagues' widest clock, a kick-off on the widest day, "Postponed" — with "MMM" for each side,
// measured for what the lines follow. Before the folder holds a line following anything, the face's
// glyph and "No team yet". Stale, the words are quiet; down a bar on its side, the dot over the score or the time.

using Karpo.Core;
using Karpo.Dock.Native;
using Karpo.Dock.Panels;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Sports;

/// <summary>A team's live score or next match on the bar.</summary>
internal sealed unsafe class SportsFaceDrawing : IFaceDrawing
{
    /// <summary>A side's abbreviation at its widest, for the templates.</summary>
    private const string Side = "MMM";

    public string Id => FolderFaces.SportsId;

    public bool Seconds(FolderFace face) => false;

    /// <summary>The reading the face shows now, from what the folder holds for its lines; before it holds anything, its first line's waiting.</summary>
    private static SportsReading? Read(in FaceContext context) =>
        context.Folder?.SportsNow() is { } reading ? reading
        : FolderIsland.SportsOf(context.Options) is { } team ? SportsReading.From(team, default, Clock.Now().ToUniversalTime(), SportsStore.Shared.Interval)
        : null;

    /// <summary>The face's room: the widest of what each of the folder's lines follows can say (§369), at least <paramref name="team"/>'s.</summary>
    private static float Room(in FaceContext context, SportsOptions team, float px)
    {
        var room = Room(context.Gfx, team, px);
        foreach (var each in FolderIsland.SportsAll(context.Options))
        {
            room = MathF.Max(room, Room(context.Gfx, each, px));
        }
        return room;
    }

    /// <summary>The dot's size beside text <paramref name="height"/> px tall.</summary>
    private static float Dot(in Fit fit, float height) => MathF.Max(fit.Px(6), TextLine.Round(height * 0.4f));

    /// <summary>Everything the face may say for <paramref name="team"/>, at its widest: what its room is measured from.</summary>
    internal static IReadOnlyList<string> Templates(SportsOptions team)
    {
        var clock = SportsLeagues.ClockOf(team.Sport, team.League) switch
        {
            SportsClock.Minutes => "90'+10'",
            SportsClock.Quarters => "Q4 88:88", // not-ui: a template
            SportsClock.Halves => "H2 88:88", // not-ui: a template
            SportsClock.Periods => "P3 88:88", // not-ui: a template
            _ => "▼88",
        };
        var late = new DateTime(2026, 9, 23, 23, 59, 0, DateTimeKind.Local);
        var score = $"{Side} 88–88 {Side}";
        // A team's opponent, "vs MMM"; a league's game its two sides (§369).
        var versus = team.IsTeam ? $"vs {Side}" : $"{Side} – {Side}";
        // §479: a final after extra time or a shoot-out says so; a last match of an older season its year.
        List<string> said = [$"{score} {clock}", $"{score} {S.DockSportsFinal}", $"{score} {S.DockSportsHalftime}", $"{score} {Nls.MonthDay(new DateOnly(2026, 9, 28))}",
            $"{score} {S.DockSportsAfterExtraTime}", $"{score} {S.DockSportsPens}", $"{score} {Nls.ShortDate(new DateOnly(2025, 9, 28))}",
            $"{versus} {S.DockSportsToday(Nls.Time(late))}", $"{versus} {S.DockSportsTomorrow(Nls.Time(late))}",
            $"{versus} {S.DockSportsPostponed}", $"{versus} {S.DockSportsCanceled}"];
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            said.Add($"{versus} {S.DockSportsDay(Nls.ShortDayName(day), Nls.Time(late))}");
        }
        said.Add($"{versus} {S.DockSportsDay(Nls.MonthDay(new DateOnly(2026, 9, 28)), Nls.Time(late))}");
        return said;
    }

    /// <summary>The widest the face's words read for <paramref name="team"/>, at <paramref name="px"/>.</summary>
    internal static float Room(Gfx gfx, SportsOptions team, float px) =>
        MathF.Ceiling(Templates(team).Max(text => TextLine.Template(gfx, text, px, Weight.Regular).Width));

    /// <summary>
    /// Down a vertical bar: the rows the widest score, kick-off or day stands in across the bar (§598) —
    /// "2–1" on a row, else "2" over "1"; "20∶45" else "20" over "45".
    /// </summary>
    private static (VerticalFit.SideRows Plan, string[] Rows) Stacked(in FaceContext context, SportsReading reading)
    {
        var plan = VerticalFit.Rows(context.Gfx, context.Fit, VerticalFit.Phrase, ["88–88", TidesFaceDrawing.WidestTime(context.Gfx, context.Fit.Primary), .. WeekNames]);
        return (plan, VerticalFit.Phrase[plan.Form](Short(reading, Clock.Now())));
    }

    /// <summary>The week's short day names, a kick-off on another day being its day alone (<see cref="Short"/>).</summary>
    private static IEnumerable<string> WeekNames => Enum.GetValues<DayOfWeek>().Select(Nls.ShortDayName);

    public FaceSize Measure(in FaceContext context)
    {
        var fit = context.Fit;
        if (context.Vertical)
        {
            // Before a team is set, the glyph alone (Paint).
            if (Read(context) is not { } shown)
            {
                return new FaceSize(fit.Glyph(), Square: false);
            }
            var (plan, rows) = Stacked(context, shown);
            return new FaceSize(Dot(fit, plan.Height) + fit.Leading + plan.LengthOf(rows), Square: false);
        }
        var px = fit.Primary;
        if (Read(context) is not { } reading)
        {
            return new FaceSize(FaceDrawings.EmptyWidth(context.Gfx, fit), Square: false);
        }
        // What it says now (§244), up to the room of the widest it can say, and the dot while it is on.
        var (_, line) = TextLine.Template(context.Gfx, "88", px, Weight.Regular);
        var lead = reading.View.Shows == SportsShows.Live ? Dot(fit, line) + fit.Gap : 0;
        var said = MathF.Min(Room(context, reading.Team, px), TextLine.Measure(context.Gfx, reading.Said(Clock.Now()), px, Weight.Regular, null));
        return new FaceSize(MathF.Ceiling(lead + said), Square: false);
    }

    /// <summary>What the face shows now: a new score, clock, state or day paints it again.</summary>
    public string Reading(in FaceContext context) =>
        Read(context) is { } reading ? reading.Fingerprint(Clock.Now()) : "unset";

    /// <summary>The team, the match and its competition, and the reading's age; none before a team is set.</summary>
    public string? Tooltip(in FaceContext context) => Read(context)?.Tooltip(Clock.Now());

    public void Paint(ID2D1DeviceContext* dc, in FaceContext context, D2D_RECT_F box)
    {
        var fit = context.Fit;
        var ink = new IslandInk(context.Style.Look);
        var gfx = context.Gfx;
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        dc->SetTextAntialiasMode(D2D1_TEXT_ANTIALIAS_MODE.D2D1_TEXT_ANTIALIAS_MODE_GRAYSCALE);
        if (Read(context) is not { } reading)
        {
            if (context.Vertical)
            {
                var glyph = fit.Glyph();
                PopupPaint.Glyph(dc, gfx, WidgetInfos.Sports.Glyph, glyph, ink.Text, PopupPaint.Rect(box.left + ((width - glyph) / 2), box.top + ((height - glyph) / 2), glyph, glyph));
                return;
            }
            FaceDrawings.Empty(dc, context.Gfx, context.Style, box, WidgetInfos.Sports.Glyph);
            return;
        }
        var localNow = Clock.Now();
        var live = reading.View.Shows == SportsShows.Live;
        var words = reading.State == SportsState.Ready && !reading.Stale ? ink.Text : ink.Secondary;
        if (context.Vertical)
        {
            var (plan, rows) = Stacked(context, reading);
            var dot = Dot(fit, plan.Height);
            var length = plan.LengthOf(rows);
            var top = box.top + TextLine.Round((height - (dot + fit.Leading + length)) / 2);
            if (live)
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(box.left + TextLine.Round((width - dot) / 2), top, dot, dot), dot / 2, ink.Alarm);
            }
            // Without the dot the words stand in the middle of the cell.
            FaceDrawings.Rows(dc, gfx, rows, plan, words, box, live ? top + dot + fit.Leading : box.top + TextLine.Round((height - length) / 2));
            return;
        }
        var textPx = fit.Primary;
        var (_, textLine) = TextLine.Template(gfx, "88", textPx, Weight.Regular);
        var size = Dot(fit, textLine);
        var room = Room(context, reading.Team, textPx);
        var said = reading.Said(localNow);
        var saidWidth = MathF.Min(room, TextLine.Measure(gfx, said, textPx, Weight.Regular, null));
        var lead = live ? size + fit.Gap : 0;
        var row = lead + saidWidth;
        var left = box.left + TextLine.Round((width - row) / 2);
        var (inkTop, inkBottom) = TextLine.Ink(gfx, "88", textPx, Weight.Regular);
        var y = box.top + TextLine.Round((height / 2) - ((inkTop + inkBottom) / 2));
        if (live)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(left, box.top + TextLine.Round((height - size) / 2), size, size), size / 2, reading.Stale ? ink.Secondary : ink.Alarm);
        }
        var text = TextLine.Lay(gfx, said, textPx, Weight.Regular, MathF.Max(1, box.right - left - lead));
        ID2D1SolidColorBrush* brush = null;
        try
        {
            brush = Gfx.Brush(dc, words);
            text.Draw(dc, brush, words, left + lead, y);
        }
        finally
        {
            text.Release();
            ComUtil.Release(ref brush);
        }
    }

    /// <summary>What a bar on its side has room for: the score, else the kick-off's time, else a dash.</summary>
    private static string Short(SportsReading reading, DateTime localNow)
    {
        if (reading.View.Match is not { } match)
        {
            return "–";
        }
        if (reading.View.Shows is SportsShows.Live or SportsShows.Final or SportsShows.Last && match.Home.Score.Length > 0 && match.Away.Score.Length > 0)
        {
            var (first, second) = reading.HomeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
            return $"{first.Score}–{second.Score}";
        }
        var start = match.StartUtc.ToLocalTime();
        return match.TimeKnown && start.Date == localNow.Date ? Nls.Time(start) : Nls.ShortDayName(start.DayOfWeek);
    }
}
