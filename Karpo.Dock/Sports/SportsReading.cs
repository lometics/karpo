// sports: what a sports widget says (docs\widgets.md §239), the face's and the line's alike: the score
// "FCB 2–1 RMA" with the game's clock "67'" while it is on, "vs RMA" and "Sat 20:00" before it, the
// final score after, a match put off or called off in its own word. Times are this PC's zone and the
// region's (or Karpo's chosen) format (Nls); the clock is the league's own way of telling it — a
// football minute, a quarter, a period, an inning's half — in signs the same in every language.
//
// What is shown is never made up: before the first answer it says it is getting the scores, after a
// failure the last answer stands with its age, and a reading older than its pace allows is stale.

using System.Globalization;
using Karpo.Core;
using Karpo.Dock.Panels;
using Karpo.Dock.Weather;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.Sports;

/// <summary>Where a sports widget's reading stands.</summary>
internal enum SportsState
{
    /// <summary>Nothing has come yet, and nothing has failed.</summary>
    Waiting,

    /// <summary>An answer is there to show.</summary>
    Ready,

    /// <summary>Nothing has come, and the last try failed.</summary>
    Failed,
}

/// <summary>What a sports widget shows now: the team's or the league's view of its matches, how fresh it is, and why not where nothing came.</summary>
internal sealed record SportsReading(SportsOptions Team, SportsView View, SportsState State, string? Problem, bool Stale, string Updated, string Logo, string Standing)
{
    /// <summary>§369: the league's table, its groups in order; empty where there is none.</summary>
    public IReadOnlyList<SportsGroup> Table { get; init; } = [];

    /// <summary>§369: a league's games of the day it shows (SportsPick.Day); none for a team.</summary>
    public IReadOnlyList<SportsMatch> Day { get; init; } = [];

    /// <summary>§479: a cup's knockout matches, by kick-off; none for a team or a league.</summary>
    public IReadOnlyList<SportsMatch> Knockout { get; init; } = [];

    /// <summary>§479: the views it has something for: its matches always, its table where ESPN keeps one, a cup's knockout where it has begun.</summary>
    public IReadOnlyList<SportsPage> Pages =>
        [SportsPage.Matches, .. Table.Any(group => group.Rows.Count > 0) ? [SportsPage.Table] : Array.Empty<SportsPage>(), .. Knockout.Count > 0 ? [SportsPage.Knockout] : Array.Empty<SportsPage>()];

    /// <summary>§369: a league is followed, not a team.</summary>
    public bool League => !Team.IsTeam;
    /// <summary>The league its shown match is in, else the team's.</summary>
    public SportsClock Clock => SportsLeagues.ClockOf(Team.Sport, View.Match?.League is { Length: > 0 } league ? league : Team.League);

    /// <summary>Whether a score is written home side first: football's way, else the visitors first.</summary>
    public bool HomeFirst => Team.Sport == SportsLeagues.Soccer;

    /// <summary>
    /// What <paramref name="held"/> comes to for <paramref name="team"/> at <paramref name="utcNow"/>:
    /// stale where the last fetch failed or is older than twice its pace and a minute.
    /// </summary>
    public static SportsReading From(SportsOptions team, SportsHeld held, DateTime utcNow, TimeSpan interval)
    {
        var view = SportsPick.Of(held.Shown, team.Team, utcNow);
        if (held.Shown is not { } answer)
        {
            return new SportsReading(team, view, held.Problem is null ? SportsState.Waiting : SportsState.Failed, held.Problem, false, "", "", "");
        }
        var fetched = held.Fetched ?? utcNow;
        var pace = SportsPace.Every(SportsPick.Of(answer, team.Team, fetched), fetched, interval);
        var stale = held.Problem is not null || utcNow - fetched > pace + pace + TimeSpan.FromMinutes(1);
        var updated = WeatherAge.Ago(fetched, utcNow);
        var standing = string.Join(" · ", new[] { answer.Standing, answer.Record }.Where(part => part.Length > 0));
        return new SportsReading(team, view, SportsState.Ready, held.Problem, stale, updated, answer.Logo, standing)
        {
            Table = answer.Table,
            Day = team.IsTeam ? [] : SportsPick.Day(view, answer, SportsLeagues.RoundDays(team.Sport)),
            Knockout = team.IsTeam ? [] : answer.Knockout,
        };
    }

    /// <summary>
    /// What the face and a one-row line say: the score and its clock, the next match and its day, or why
    /// there is none. A league's match is its two sides, "MUN – DOR Sat 18:30" (§369).
    /// </summary>
    public string Said(DateTime localNow) => View.Match is not { } match
        ? State == SportsState.Ready ? S.DockSportsNone : Problem ?? S.DockSportsLoading
        : $"{(League ? SportsText.Score(match, HomeFirst) : SportsText.Lead(match, View.Shows, Team.Team, HomeFirst))} {SportsText.Trail(match, View.Shows, Clock, localNow)}";

    /// <summary>
    /// §369: the rows of the table to show <paramref name="most"/> of at the most: a league's first
    /// group from the top; a team's group around the team, its place among them — the top where it is near it.
    /// Null where there is no table, or the team is in none of it.
    /// </summary>
    public (SportsGroup Group, int Skip, int Take)? TableRows(int most)
    {
        if (most <= 0 || Table.Count == 0)
        {
            return null;
        }
        if (League)
        {
            var first = Table[0];
            return first.Rows.Count == 0 ? null : (first, 0, Math.Min(most, first.Rows.Count));
        }
        foreach (var group in Table)
        {
            var at = group.Rows.FindIndex(place => place.Id == Team.Team);
            if (at < 0)
            {
                continue;
            }
            var take = Math.Min(most, group.Rows.Count);
            var skip = Math.Clamp(at - ((take - 1) / 2), 0, group.Rows.Count - take);
            return (group, skip, take);
        }
        return null;
    }

    /// <summary>To a screen reader (none over the bar, §367): the team, the match with its competition, where it stands in words (§485), and the reading's age.</summary>
    public string? Tooltip(DateTime localNow)
    {
        var lines = new List<string>();
        if (Team.Name.Length > 0)
        {
            lines.Add(Team.Name);
        }
        if (View.Match is { } match)
        {
            var (own, other, home) = match.Sides(Team.Team);
            var named = home ? $"{own.Name} – {other.Name}" : $"{other.Name} – {own.Name}";
            lines.Add(match.LeagueName.Length > 0 ? $"{named} · {match.LeagueName}" : named);
            lines.Add(Said(localNow));
            // §485: where it stands, in words: on now and its minute, how long ago it finished, when it starts.
            var state = match.State switch
            {
                MatchState.Live => SportsText.LiveSaid(match, Clock),
                MatchState.Final => SportsText.Finished(match, localNow),
                MatchState.Scheduled => SportsText.Starts(match, localNow),
                _ => "",
            };
            if (state.Length > 0)
            {
                lines.Add(state);
            }
            if (SportsText.Detail(match, HomeFirst, round: true) is { Length: > 0 } detail)
            {
                lines.Add(detail);
            }
        }
        else
        {
            lines.Add(Said(localNow));
        }
        if (Updated.Length > 0)
        {
            lines.Add(Updated);
        }
        return string.Join('\n', lines);
    }

    /// <summary>What changes what is drawn: the shown match's state, score and clock, and the staleness.</summary>
    public string Fingerprint(DateTime localNow) =>
        FormattableString.Invariant($"{State}|{View.Shows}|{View.Match?.Id}|{Said(localNow)}|{Stale}|{View.Coming.Count}|{View.Others.Count}|{Table.Count}|{string.Concat(Day.Select(match => $"{match.State}{match.Home.Score}{match.Away.Score}{match.Clock};"))}");
}

/// <summary>The words of a match (see the top of the file).</summary>
internal static class SportsText
{
    /// <summary>"vs", before the visitors to the team's ground: the sports world's own, in every language.</summary>
    private const string Versus = "vs";

    /// <summary>"@", before the ground the team visits.</summary>
    private const string At = "@";

    /// <summary>"FCB 2–1 RMA": the two sides' abbreviations about the score, the home side first where <paramref name="homeFirst"/>; "PAR – TOR" before it.</summary>
    public static string Score(SportsMatch match, bool homeFirst)
    {
        var (first, second) = homeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        return first.Score.Length > 0 && second.Score.Length > 0 && match.State != MatchState.Scheduled
            ? $"{Abbreviation(first)} {first.Score}–{second.Score} {Abbreviation(second)}"
            : $"{Abbreviation(first)} – {Abbreviation(second)}";
    }

    /// <summary>
    /// §369: "Bayern 2–1 Dortmund": the two sides' names about the score, as <see cref="Score"/> has them; "Bayern –
    /// Dortmund" before it. §479: their full names where <paramref name="full"/>, "Bayern Munich 2–1 Borussia Dortmund".
    /// </summary>
    public static string Named(SportsMatch match, bool homeFirst, bool full = false)
    {
        var (first, second) = homeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        string Name(SportsSide side) => full && side.FullName.Length > 0 ? side.FullName : side.Name.Length > 0 ? side.Name : Abbreviation(side);
        return first.Score.Length > 0 && second.Score.Length > 0 && match.State != MatchState.Scheduled
            ? $"{Name(first)} {first.Score}–{second.Score} {Name(second)}"
            : $"{Name(first)} – {Name(second)}";
    }

    /// <summary>
    /// §369: a game in a line's list: the clock while it is on, "Final" after, the word for one put off, else its
    /// kick-off's time where it is on the list's <paramref name="day"/> (its head says the day), its day and time
    /// where it is not ("Sun 15:30"); its day alone where its time is not known. §485: a row under no day's heading
    /// (<paramref name="day"/> null: a knockout's round, a team's results) says a result's day too, "AET · 29 Jun".
    /// </summary>
    public static string Row(SportsMatch match, SportsClock clock, DateTime localNow, DateOnly? day) => match.State switch
    {
        MatchState.Live => Clock(match, clock),
        MatchState.Final => day is null ? LineRow.Join(Ended(match), Dated(DateOnly.FromDateTime(match.StartUtc.ToLocalTime()), localNow)) : Ended(match),
        _ => Word(match) ?? (day is { } on && match.TimeKnown && DateOnly.FromDateTime(match.StartUtc.ToLocalTime()) == on ? Nls.Time(match.StartUtc.ToLocalTime()) : When(match, localNow)),
    };

    /// <summary>
    /// §485: the shortest a row under no day's heading says where its match stands, where its whole leaves the names no
    /// room: a result's day ("17 Mar"), a kick-off's time today, its weekday within the week, else its day ("19 Jul");
    /// null for one on now or put off, whose words are short already.
    /// </summary>
    public static string? Short(SportsMatch match, DateTime localNow)
    {
        var start = match.StartUtc.ToLocalTime();
        var day = DateOnly.FromDateTime(start);
        var days = day.DayNumber - DateOnly.FromDateTime(localNow).DayNumber;
        return match.State switch
        {
            MatchState.Final => Dated(day, localNow),
            MatchState.Scheduled => days == 0 && match.TimeKnown ? Nls.Time(start) : days is > 0 and < 7 ? Nls.ShortDayName(start.DayOfWeek) : Nls.MonthDay(day),
            _ => null,
        };
    }

    /// <summary>
    /// §485: whether <paramref name="side"/> lost <paramref name="match"/>, played to its end: the other side ESPN's
    /// winner of it (a shoot-out's too), else the other's score the higher; a draw has no loser.
    /// </summary>
    public static bool Lost(SportsMatch match, SportsSide side)
    {
        if (match.State != MatchState.Final || (side != match.Home && side != match.Away))
        {
            return false;
        }
        var other = side == match.Home ? match.Away : match.Home;
        if (side.Winner || other.Winner)
        {
            return other.Winner && !side.Winner;
        }
        return int.TryParse(side.Score, NumberStyles.Integer, CultureInfo.InvariantCulture, out var mine)
            && int.TryParse(other.Score, NumberStyles.Integer, CultureInfo.InvariantCulture, out var theirs) && theirs > mine;
    }

    /// <summary>
    /// §485: a match on now said in words: "Live, 67 minutes played" for a football minute, "Live, half-time", else
    /// "Live, Q3 5:32" with the league's own clock.
    /// </summary>
    public static string LiveSaid(SportsMatch match, SportsClock clock)
    {
        if (match.Status.Contains("HALFTIME", StringComparison.OrdinalIgnoreCase))
        {
            return S.DockSportsTipHalftime;
        }
        var said = Clock(match, clock);
        return clock == SportsClock.Minutes && said.EndsWith('\'') && int.TryParse(said.AsSpan(0, said.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            ? S.DockSportsTipLiveMinutes(minutes)
            : said.Length > 0 ? S.DockSportsTipLive(said) : S.DockSportsLive;
    }

    /// <summary>§485: how long ago a match played to its end was, by its day in this PC's zone: "Finished today", "yesterday", "2 days ago"; "Finished" past a month.</summary>
    public static string Finished(SportsMatch match, DateTime localNow)
    {
        var days = DateOnly.FromDateTime(localNow).DayNumber - DateOnly.FromDateTime(match.StartUtc.ToLocalTime()).DayNumber;
        return days switch
        {
            <= 0 => S.DockSportsTipFinishedToday,
            1 => S.DockSportsTipFinishedYesterday,
            <= 30 => S.DockSportsTipFinishedDaysAgo(days),
            _ => S.DockSportsTipFinished,
        };
    }

    /// <summary>§485: when a match to come begins, in this PC's zone: "Starts today at 20:45", "tomorrow at", "Sat, 07.11. at"; its day alone where its time is not known.</summary>
    public static string Starts(SportsMatch match, DateTime localNow)
    {
        var start = match.StartUtc.ToLocalTime();
        var day = DateOnly.FromDateTime(start);
        var days = day.DayNumber - DateOnly.FromDateTime(localNow).DayNumber;
        var named = Nls.DayAndShortDate(day, year: days is > 182 or < -182);
        if (!match.TimeKnown)
        {
            return S.DockSportsTipStartsDay(named);
        }
        var time = Nls.Time(start);
        return days switch
        {
            0 => S.DockSportsTipStartsToday(time),
            1 => S.DockSportsTipStartsTomorrow(time),
            _ => S.DockSportsTipStartsOn(named, time),
        };
    }

    /// <summary>§369: the day a league's list is of, in this PC's zone: "Today", "Tomorrow", "Yesterday", else "Sat, 31.10." as the region writes it.</summary>
    public static string DayOf(DateTime startUtc, DateTime localNow)
    {
        var day = DateOnly.FromDateTime(startUtc.ToLocalTime());
        return (day.DayNumber - DateOnly.FromDateTime(localNow).DayNumber) switch
        {
            0 => S.DockCalendarToday,
            1 => S.DockCalendarTomorrow,
            -1 => S.DockCalendarYesterday,
            // §479: half a year off or more, its year, so an old season never reads as this one.
            > 182 or < -182 => Nls.DayAndShortDate(day, year: true),
            _ => Nls.DayAndShortDate(day, year: false),
        };
    }

    /// <summary>
    /// §369: a side's monogram, its abbreviation's first <paramref name="letters"/> ("FC", "B"), else
    /// its name's; "?" for neither.
    /// </summary>
    public static string Monogram(SportsSide side, int letters)
    {
        var from = side.Abbreviation.Length > 0 ? side.Abbreviation : side.Name.Replace(" ", "", StringComparison.Ordinal);
        if (from.Length == 0)
        {
            return "?";
        }
        var info = new StringInfo(from);
        return info.SubstringByTextElements(0, Math.Min(letters, info.LengthInTextElements)).ToUpper(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// §369: the colour of a side's monogram's disc: one of a dozen mid tones, the same for the same
    /// side every time (an ordinal hash of <paramref name="key"/>, its id), white letters reading on each in light and dark.
    /// </summary>
    public static DXGI_RGBA Tint(string key)
    {
        var hash = 2166136261u;
        foreach (var letter in key)
        {
            hash = (hash ^ letter) * 16777619u;
        }
        var tone = Tones[(int)(hash % (uint)Tones.Length)];
        return new DXGI_RGBA(((tone >> 16) & 0xFF) / 255f, ((tone >> 8) & 0xFF) / 255f, (tone & 0xFF) / 255f, 1);
    }

    // Deep mid tones, Material's 700 and 800 shades, which white letters read on.
    private static readonly uint[] Tones = [0xC62828, 0xAD1457, 0x6A1B9A, 0x4527A0, 0x283593, 0x1565C0, 0x00695C, 0x2E7D32, 0x558B2F, 0xB85600, 0x5D4037, 0x455A64];

    /// <summary>"vs RMA" where <paramref name="team"/> is at home, "@ PSG" where it visits; the other side's name where <paramref name="named"/>.</summary>
    public static string Opponent(SportsMatch match, string team, bool named = false)
    {
        var (_, other, home) = match.Sides(team);
        return $"{(home ? Versus : At)} {(named && other.Name.Length > 0 ? other.Name : Abbreviation(other))}";
    }

    /// <summary>The first half of what a face says: the score while there is one, the opponent before.</summary>
    public static string Lead(SportsMatch match, SportsShows shows, string team, bool homeFirst) =>
        shows is SportsShows.Live or SportsShows.Final or SportsShows.Last ? Score(match, homeFirst) : Opponent(match, team);

    /// <summary>Its second half: the clock while it is on, "Final" after, the day before, the word for one put off.</summary>
    public static string Trail(SportsMatch match, SportsShows shows, SportsClock clock, DateTime localNow) => shows switch
    {
        SportsShows.Live => Clock(match, clock),
        SportsShows.Final => Ended(match),
        SportsShows.Last => Dated(DateOnly.FromDateTime(match.StartUtc.ToLocalTime()), localNow),
        _ => Word(match) ?? When(match, localNow),
    };

    /// <summary>§479: how a match played to its end ended: "Pens" after a shoot-out, "AET" after extra time, else "Final".</summary>
    public static string Ended(SportsMatch match) =>
        match.Penalties ? S.DockSportsPens : match.ExtraTime ? S.DockSportsAfterExtraTime : S.DockSportsFinal;

    /// <summary>§479: a day gone by, "28 Sept" — with its year where it is not this year's or is half a year back, so an old season never reads as this one.</summary>
    public static string Dated(DateOnly day, DateTime localNow) =>
        day.Year == localNow.Year && DateOnly.FromDateTime(localNow).DayNumber - day.DayNumber < 183 ? Nls.MonthDay(day) : Nls.ShortDate(day);

    /// <summary>
    /// §479: what a match says beyond its score: its round where <paramref name="round"/> ("Round of 16", §483 in Karpo's words), its leg,
    /// a shoot-out ("Paraguay win 4–3 on penalties", "Penalties 3–2" while on) and a tie's aggregate ("Sporting
    /// advance 5–3 on aggregate", "Aggregate 3–3"), joined by dots; empty for an ordinary match.
    /// </summary>
    public static string Detail(SportsMatch match, bool homeFirst, bool round)
    {
        var (first, second) = homeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        var parts = new List<string>();
        if (round && match.Round.Length > 0)
        {
            parts.Add(SportsRounds.Name(match.Round));
        }
        if (match.Leg is 1 or 2)
        {
            parts.Add(match.Leg == 1 ? S.DockSportsFirstLeg : S.DockSportsSecondLeg);
        }
        if (match.State is MatchState.Live or MatchState.Final && first.Shootout.Length > 0 && second.Shootout.Length > 0)
        {
            var won = match.State == MatchState.Final ? Won(first, second, first.Shootout, second.Shootout) : null;
            parts.Add(won is { } side
                ? S.DockSportsWonOnPenalties(Called(side), side == first ? $"{first.Shootout}–{second.Shootout}" : $"{second.Shootout}–{first.Shootout}")
                : S.DockSportsPenalties($"{first.Shootout}–{second.Shootout}"));
        }
        if (match.State is MatchState.Live or MatchState.Final && first.Aggregate.Length > 0 && second.Aggregate.Length > 0)
        {
            var through = match.State != MatchState.Final ? null : first.Advances ? first : second.Advances ? second : null;
            parts.Add(through is { } side
                ? S.DockSportsAdvanceOnAggregate(Called(side), side == first ? $"{first.Aggregate}–{second.Aggregate}" : $"{second.Aggregate}–{first.Aggregate}")
                : S.DockSportsAggregate($"{first.Aggregate}–{second.Aggregate}"));
        }
        return string.Join(" · ", parts);
    }

    /// <summary>The side that won: ESPN's winner, else the one with more of <paramref name="mine"/> against <paramref name="theirs"/>; null for neither.</summary>
    private static SportsSide? Won(SportsSide first, SportsSide second, string mine, string theirs) =>
        first.Winner ? first : second.Winner ? second
        : int.TryParse(mine, out var one) && int.TryParse(theirs, out var two) && one != two ? one > two ? first : second
        : null;

    /// <summary>A side as a sentence names it: its short name, else its abbreviation.</summary>
    public static string Called(SportsSide side) => side.Name.Length > 0 ? side.Name : Abbreviation(side);

    /// <summary>The word for a match put off or called off; null for one that is not.</summary>
    public static string? Word(SportsMatch match) => match.State switch
    {
        MatchState.Postponed => S.DockSportsPostponed,
        MatchState.Canceled => S.DockSportsCanceled,
        _ => null,
    };

    /// <summary>
    /// The game's time while it is on, in the league's way (<see cref="SportsClock"/>): "67'", "45'+2'",
    /// half-time; "Q3 5:32" and "OT"; "H2 5:32"; "P2 12:04"; "▲5" for the top of the fifth.
    /// </summary>
    public static string Clock(SportsMatch match, SportsClock clock)
    {
        if (match.Status.Contains("HALFTIME", StringComparison.OrdinalIgnoreCase))
        {
            return S.DockSportsHalftime;
        }
        if (match.Status.Contains("SHOOTOUT", StringComparison.OrdinalIgnoreCase))
        {
            return S.DockSportsPens;
        }
        var time = match.Clock is { Length: > 0 } shown && shown != "0:00" && shown != "0.0" ? shown : "";
        string With(string period) => time.Length > 0 ? $"{period} {time}" : period;
        var number = match.Period.ToString(CultureInfo.InvariantCulture);
        return clock switch
        {
            SportsClock.Minutes => time,
            SportsClock.Quarters => With(match.Period > 4 ? Overtime(match.Period - 4) : $"Q{number}"),
            SportsClock.Halves => With(match.Period > 2 ? Overtime(match.Period - 2) : $"H{number}"),
            SportsClock.Periods => With(match.Period > 3 ? Overtime(match.Period - 3) : $"P{number}"),
            _ => SportsAnswers.Inning(match.Clock) switch
            {
                true => $"▲{number}",
                false => $"▼{number}",
                null => number,
            },
        };
    }

    /// <summary>"OT", "2OT".</summary>
    private static string Overtime(int count) => count > 1 ? $"{count.ToString(CultureInfo.InvariantCulture)}OT" : "OT";

    /// <summary>
    /// When a match begins, in this PC's zone: "Today 20:00", "Tomorrow 20:00", "Sat 20:00" within the
    /// week, "25 Oct 20:00" past it; the day alone where its time is not known yet.
    /// </summary>
    public static string When(SportsMatch match, DateTime localNow)
    {
        var start = match.StartUtc.ToLocalTime();
        var days = (start.Date - localNow.Date).Days;
        var day = days is > 1 and < 7 ? Nls.ShortDayName(start.DayOfWeek) : Nls.MonthDay(DateOnly.FromDateTime(start));
        if (!match.TimeKnown)
        {
            return days is 0 or 1 ? Nls.ShortDayName(start.DayOfWeek) : day;
        }
        var time = Nls.Time(start);
        return days switch
        {
            0 => S.DockSportsToday(time),
            1 => S.DockSportsTomorrow(time),
            _ => S.DockSportsDay(day, time),
        };
    }

    /// <summary>A side's abbreviation, else the first letters of its name.</summary>
    public static string Abbreviation(SportsSide side) =>
        side.Abbreviation.Length > 0 ? side.Abbreviation
        : side.Name.Length > 0 ? side.Name[..Math.Min(3, side.Name.Length)].ToUpperInvariant()
        : "?";
}

/// <summary>§479: one column of a table: its letters over it, what they stand for, and its figure for a place.</summary>
internal sealed record SportsColumn(string Head, string Name, Func<SportsPlace, string> Value);

/// <summary>§479: the columns a sport's table shows.</summary>
internal static class SportsColumns
{
    private static SportsColumn Played => new(S.DockSportsColumnPlayed, S.DockSportsColumnPlayedName, place => place.Played);

    private static SportsColumn Won => new(S.DockSportsColumnWon, S.DockSportsColumnWonName, place => place.Won);

    private static SportsColumn Drawn => new(S.DockSportsColumnDrawn, S.DockSportsColumnDrawnName, place => place.Drawn);

    private static SportsColumn Lost => new(S.DockSportsColumnLost, S.DockSportsColumnLostName, place => place.Lost);

    private static SportsColumn Difference => new(S.DockSportsColumnDifference, S.DockSportsColumnDifferenceName, place => place.Difference);

    private static SportsColumn Points => new(S.DockSportsColumnPoints, S.DockSportsColumnPointsName, place => place.Points.Length > 0 ? place.Points : place.Value);

    private static SportsColumn Behind => new(S.DockSportsColumnBehind, S.DockSportsColumnBehindName, place => place.Behind);

    /// <summary>
    /// Football's P, GD, Pts — and W, D, L between them where <paramref name="wide"/>; ice hockey's the same without
    /// draws; the others' W, L and GB (games behind).
    /// </summary>
    public static IReadOnlyList<SportsColumn> Of(string sport, bool wide) => sport switch
    {
        SportsLeagues.Soccer => wide ? [Played, Won, Drawn, Lost, Difference, Points] : [Played, Difference, Points],
        "hockey" => wide ? [Played, Won, Lost, Difference, Points] : [Played, Difference, Points],
        _ => [Won, Lost, Behind],
    };

    /// <summary>What a table keeps where nothing more fits beside the names: the points, or America's wins and losses.</summary>
    public static IReadOnlyList<SportsColumn> Least(string sport) => sport is SportsLeagues.Soccer or "hockey" ? [Points] : [Won, Lost];
}

/// <summary>§479: what a sports line's rows say whole in their tooltips.</summary>
internal static class SportsTips
{
    /// <summary>
    /// A match: both sides' full names, the home side first where <paramref name="homeFirst"/>; its competition and
    /// round; where it stands in words at <paramref name="localNow"/> (§485: "Live, 67 minutes played", "Finished 2 days ago · AET"
    /// and its day and time in this PC's zone, "Starts today at 20:45", or that it was put off and its day); its leg
    /// and how a shoot-out or a tie came out.
    /// </summary>
    public static string Match(SportsMatch match, bool homeFirst, SportsClock clock, DateTime localNow)
    {
        var (first, second) = homeFirst ? (match.Home, match.Away) : (match.Away, match.Home);
        static string Full(SportsSide side) => side.FullName.Length > 0 ? side.FullName : SportsText.Called(side);
        var lines = new List<string>
        {
            match.State is MatchState.Live or MatchState.Final && first.Score.Length > 0 && second.Score.Length > 0
                ? $"{Full(first)} {first.Score}–{second.Score} {Full(second)}"
                : $"{Full(first)} – {Full(second)}",
        };
        if (LineRow.Join(match.LeagueName, SportsRounds.Name(match.Round)) is { Length: > 0 } where)
        {
            lines.Add(where);
        }
        var start = match.StartUtc.ToLocalTime();
        var day = Nls.DayAndShortDate(DateOnly.FromDateTime(start));
        var when = match.TimeKnown ? $"{day} {Nls.Time(start)}" : day;
        // §485: where it stands in words: "Live, 67 minutes played"; "Finished 2 days ago · AET" and its day; "Starts at 20:45".
        lines.Add(match.State switch
        {
            MatchState.Live => SportsText.LiveSaid(match, clock),
            MatchState.Final => LineRow.Join(SportsText.Finished(match, localNow), match.ExtraTime ? SportsText.Ended(match) : "", when),
            MatchState.Scheduled => SportsText.Starts(match, localNow),
            _ => LineRow.Join(SportsText.Word(match), when),
        });
        if (SportsText.Detail(match, homeFirst, round: false) is { Length: > 0 } detail)
        {
            lines.Add(detail);
        }
        return string.Join('\n', lines);
    }

    /// <summary>A place: its rank and full name, the zone it stands in, and every figure with its name.</summary>
    public static string Place(SportsPlace place, string sport)
    {
        var name = place.FullName.Length > 0 ? place.FullName : place.Name;
        var figures = SportsColumns.Of(sport, wide: true).Where(column => column.Value(place).Length > 0).Select(column => $"{column.Name} {column.Value(place)}");
        return string.Join('\n', new[] { $"{place.Rank.ToString(CultureInfo.CurrentCulture)}. {name}", place.ZoneText, string.Join(" · ", figures) }.Where(line => line.Length > 0));
    }

    /// <summary>A group's heading: its name, and what each column's letters stand for.</summary>
    public static string Columns(string group, IReadOnlyList<SportsColumn> columns) =>
        string.Join('\n', [group, .. columns.Select(column => $"{column.Head}: {column.Name}")]);
}
