// sports: a favourite team's live score or next match (docs\widgets.md §239), as Google's sports card
// shows it: "FCB 2–1 RMA 67'" while it plays, "vs RMA Sat 20:00" before, the final score for a few
// hours after, then the next match again. §369: or a league or a tournament of the table followed
// itself — its round's games and its table —, the table around a followed team, and the teams of a
// league found by its name in the menu's search, each with its logo.
//
// **The service.** ESPN's public site API (site.api.espn.com), which answers without an account or a
// key: a team's schedule (its results and its fixtures; for football, the soccer kind, across every
// competition it plays in), a league's scoreboard for a day (the live clock, and the league's other
// games, and its calendar of the days with games), a league's teams and its table (§369; the table on
// ESPN's older path, apis/v2), and ESPN's own search for a team by name (site.web.api.espn.com). Nothing
// personal is in a request: the team's id, the league's slug, a day, and what was typed into the
// search. Nothing is asked until a team or a league is set. ESPN takes one day a scoreboard request
// (a range answers 400), so a league's round is a request a day.
//
// **The leagues** are one table (SportsLeagues.All): the sport and league slugs ESPN uses, the name a
// search result is shown with, and how the league tells its time while a game is on. A team is found
// only in a league of the table, so a search for "chiefs" offers Kansas City and not a rugby side.
//
// Reading the answers lives here rather than beside the fetch, as the stock widget's does
// (SymbolQuery), so a payload that changes shape costs a field and fails a test, not a widget. Which
// match shows (SportsPick) and how soon to ask again (SportsPace) are here for the same reason.

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>How a league tells the time of a game that is on (<see cref="SportsLeague.Clock"/>).</summary>
public enum SportsClock
{
    /// <summary>Football's minutes, "67'", "45'+2'", and half-time.</summary>
    Minutes,

    /// <summary>Four quarters and a clock, "Q3 5:32"; overtime past the fourth.</summary>
    Quarters,

    /// <summary>Two halves and a clock, "H2 5:32", as college basketball plays.</summary>
    Halves,

    /// <summary>Three periods and a clock, "P2 12:04", as ice hockey plays.</summary>
    Periods,

    /// <summary>Innings, the top or the bottom of one, "▲5".</summary>
    Innings,
}

/// <summary>
/// One league of the table: ESPN's sport and league slugs, its name, how it tells a game's time, the
/// longer name ESPN gives it (what a search finds it by besides its name, §369) and its logo's
/// address on ESPN's picture server.
/// </summary>
public sealed record SportsLeague(string Sport, string League, string Name, SportsClock Clock, string Words = "", string Logo = "")
{
    /// <summary>Whether a score is written home side first, as football's is; else the visitors first, as America's are.</summary>
    public bool HomeFirst => Clock == SportsClock.Minutes;
}

/// <summary>The leagues a team may be followed in, and that may be followed themselves, in one table (see the top of the file).</summary>
public static class SportsLeagues
{
    /// <summary>Football, the soccer kind: ESPN's sport slug, whose schedule is every competition at once.</summary>
    public const string Soccer = "soccer";

    private const string Logos = "https://a.espncdn.com/i/leaguelogos/soccer/500/";
    private const string UsLogos = "https://a.espncdn.com/i/teamlogos/leagues/500/";
    private const string CollegeLogos = "https://a.espncdn.com/redesign/assets/img/icons/NCAASports_";

    // The names are the leagues' own, the same in every language; the words ESPN's longer names; the
    // logos as each league's scoreboard gave them on 2026-10-26.
    public static IReadOnlyList<SportsLeague> All { get; } =
    [
        new(Soccer, "eng.1", "Premier League", SportsClock.Minutes, "English Premier League", Logos + "23.png"), // not-ui: a league's name
        new(Soccer, "esp.1", "LALIGA", SportsClock.Minutes, "Spanish LALIGA", Logos + "15.png"), // not-ui: a league's name
        new(Soccer, "ger.1", "Bundesliga", SportsClock.Minutes, "German Bundesliga", Logos + "10.png"), // not-ui: a league's name
        new(Soccer, "ita.1", "Serie A", SportsClock.Minutes, "Italian Serie A", Logos + "12.png"), // not-ui: a league's name
        new(Soccer, "fra.1", "Ligue 1", SportsClock.Minutes, "French Ligue 1", Logos + "9.png"), // not-ui: a league's name
        new(Soccer, "ned.1", "Eredivisie", SportsClock.Minutes, "Dutch Eredivisie", Logos + "11.png"), // not-ui: a league's name
        new(Soccer, "por.1", "Liga Portugal", SportsClock.Minutes, "Portuguese Primeira Liga", Logos + "14.png"), // not-ui: a league's name
        new(Soccer, "sco.1", "Scottish Premiership", SportsClock.Minutes, "Scottish Premiership", Logos + "45.png"), // not-ui: a league's name
        new(Soccer, "bel.1", "Belgian Pro League", SportsClock.Minutes, "Belgian Pro League", Logos + "6.png"), // not-ui: a league's name
        new(Soccer, "sui.1", "Swiss Super League", SportsClock.Minutes, "Swiss Super League", Logos + "17.png"), // not-ui: a league's name
        new(Soccer, "aut.1", "Austrian Bundesliga", SportsClock.Minutes, "Austrian Bundesliga", Logos + "5.png"), // not-ui: a league's name
        new(Soccer, "tur.1", "Süper Lig", SportsClock.Minutes, "Turkish Super Lig", Logos + "18.png"), // not-ui: a league's name
        new(Soccer, "eng.2", "EFL Championship", SportsClock.Minutes, "English League Championship", Logos + "24.png"), // not-ui: a league's name
        new(Soccer, "usa.1", "MLS", SportsClock.Minutes, "Major League Soccer", Logos + "19.png"), // not-ui: a league's name
        new(Soccer, "mex.1", "Liga MX", SportsClock.Minutes, "Mexican Liga MX", Logos + "22.png"), // not-ui: a league's name
        new(Soccer, "bra.1", "Brasileirão", SportsClock.Minutes, "Brazilian Serie A", Logos + "85.png"), // not-ui: a league's name
        new(Soccer, "arg.1", "Liga Profesional", SportsClock.Minutes, "Argentine Liga Profesional", Logos + "1.png"), // not-ui: a league's name
        new(Soccer, "uefa.champions", "UEFA Champions League", SportsClock.Minutes, "Champions League", Logos + "2.png"), // not-ui: a league's name
        new(Soccer, "uefa.europa", "UEFA Europa League", SportsClock.Minutes, "Europa League", Logos + "2310.png"), // not-ui: a league's name
        new(Soccer, "uefa.europa.conf", "UEFA Conference League", SportsClock.Minutes, "Conference League", Logos + "20296.png"), // not-ui: a league's name
        new(Soccer, "uefa.nations", "UEFA Nations League", SportsClock.Minutes, "Nations League", Logos + "2395.png"), // not-ui: a league's name
        new(Soccer, "uefa.euro", "UEFA EURO", SportsClock.Minutes, "European Championship", Logos + "74.png"), // not-ui: a league's name
        new(Soccer, "fifa.world", "FIFA World Cup", SportsClock.Minutes, "World Cup", Logos + "4.png"), // not-ui: a league's name
        new(Soccer, "conmebol.libertadores", "CONMEBOL Libertadores", SportsClock.Minutes, "Copa Libertadores", Logos + "58.png"), // not-ui: a league's name
        new("football", "nfl", "NFL", SportsClock.Quarters, "National Football League", UsLogos + "nfl.png"), // not-ui: a league's name
        new("football", "college-football", "NCAA Football", SportsClock.Quarters, "College Football", CollegeLogos + "Football.png"), // not-ui: a league's name
        new("basketball", "nba", "NBA", SportsClock.Quarters, "National Basketball Association", UsLogos + "nba.png"), // not-ui: a league's name
        new("basketball", "wnba", "WNBA", SportsClock.Quarters, "Women's National Basketball Association", UsLogos + "wnba.png"), // not-ui: a league's name
        new("basketball", "mens-college-basketball", "NCAA Men's Basketball", SportsClock.Halves, "Men's College Basketball", CollegeLogos + "MensBasketball.png"), // not-ui: a league's name
        new("basketball", "womens-college-basketball", "NCAA Women's Basketball", SportsClock.Quarters, "Women's College Basketball", CollegeLogos + "WomensBasketball.png"), // not-ui: a league's name
        new("baseball", "mlb", "MLB", SportsClock.Innings, "Major League Baseball", UsLogos + "mlb.png"), // not-ui: a league's name
        new("hockey", "nhl", "NHL", SportsClock.Periods, "National Hockey League", UsLogos + "nhl.png"), // not-ui: a league's name
    ];

    /// <summary>The league of the table <paramref name="sport"/> and <paramref name="league"/> name; null for one it does not hold.</summary>
    public static SportsLeague? Find(string? sport, string? league) =>
        All.FirstOrDefault(each => string.Equals(each.Sport, sport, StringComparison.OrdinalIgnoreCase)
            && string.Equals(each.League, league, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// §369: how many days a league's round is read and shown over: three for football of either kind,
    /// whose rounds are a weekend's (Friday to Sunday, Thursday to Monday's week for the NFL's scoreboard
    /// already), one for the leagues that play every day.
    /// </summary>
    public static int RoundDays(string? sport) => sport is Soccer or "football" ? 3 : 1;

    /// <summary>§369: a sport's name in the language now, "Football", "Ice hockey": what a league's row in a search says under it.</summary>
    public static string SportName(string? sport) => sport switch
    {
        Soccer => S.CoreSportsSoccer,
        "football" => S.CoreSportsFootball,
        "basketball" => S.CoreSportsBasketball,
        "baseball" => S.CoreSportsBaseball,
        "hockey" => S.CoreSportsHockey,
        _ => "",
    };

    /// <summary>
    /// How a game of <paramref name="sport"/> in <paramref name="league"/> tells its time: the table's,
    /// else its sport's (a cup the table does not name is played as its sport is).
    /// </summary>
    public static SportsClock ClockOf(string? sport, string? league) =>
        Find(sport, league)?.Clock ?? All.FirstOrDefault(each => string.Equals(each.Sport, sport, StringComparison.OrdinalIgnoreCase))?.Clock ?? SportsClock.Minutes;

    /// <summary>Whether a slug is one ESPN could have written: letters, digits, dots, dashes and underscores, and not too long.</summary>
    public static bool IsSlug(string? slug) =>
        slug is { Length: > 0 and <= 48 } && slug.All(letter => char.IsAsciiLetterOrDigit(letter) || letter is '.' or '-' or '_');
}

/// <summary>
/// §479: what a sports list shows: its matches, its table, a cup's knockout — §603: each a widget of its own
/// (sportsFixtures, sportsTable, sportsKnockout), no option of a line's.
/// </summary>
public enum SportsPage
{
    /// <summary>The matches to come and played, or a league's round.</summary>
    Matches,

    /// <summary>The table, every group of it.</summary>
    Table,

    /// <summary>A cup's knockout, round by round.</summary>
    Knockout,
}

/// <summary>
/// sports: what a sports widget follows (docs\widgets.md §239, §369): a team — ESPN's sport, the league
/// it was found in, its id there — or, with no team, a league or a tournament of the table itself; and
/// its name and abbreviation as they were found, so the widget says what it follows before anything is
/// fetched. Nothing is set on a new widget; it asks for a team or a league.
/// </summary>
public sealed class SportsOptions
{
    /// <summary>ESPN's sport slug, "soccer", "basketball"; empty = not set.</summary>
    public string Sport { get; set; } = "";

    /// <summary>The league the team was found in, or the league followed, ESPN's slug, "esp.1", "nba".</summary>
    public string League { get; set; } = "";

    /// <summary>The team's id at ESPN, "83"; empty where the league itself is followed (§369).</summary>
    public string Team { get; set; } = "";

    /// <summary>What the team or the league is called, "Barcelona"; only ever shown, never asked for.</summary>
    public string Name { get; set; } = "";

    /// <summary>The team's abbreviation, "BAR"; empty for a league.</summary>
    public string Abbreviation { get; set; } = "";

    /// <summary>The most a name kept in the file may be.</summary>
    public const int MostName = 60;

    /// <summary>Something is followed: a team, or a league of the table.</summary>
    [JsonIgnore]
    public bool IsSet => Sport.Length > 0 && League.Length > 0;

    /// <summary>§369: a team is followed (else, set, its league).</summary>
    [JsonIgnore]
    public bool IsTeam => IsSet && Team.Length > 0;

    /// <summary>The league it was found in or that is followed, from the table; null for none the table holds.</summary>
    [JsonIgnore]
    public SportsLeague? Of => SportsLeagues.Find(Sport, League);

    /// <summary>What one fetch for it is told apart by: the same team in the same league is one, a league "soccer/ger.1".</summary>
    [JsonIgnore]
    public string Key => !IsSet ? "" : IsTeam ? $"{Sport}/{League}/{Team}" : $"{Sport}/{League}";

    /// <summary>
    /// As a file may hold it: trimmed, the slugs and the id only where they are ones ESPN could have
    /// written — a team in a league the table does not hold, or a hand-edited id, is nothing at all —
    /// and the words no longer than a card shows.
    /// </summary>
    public void Normalize()
    {
        Sport = (Sport ?? "").Trim().ToLowerInvariant();
        League = (League ?? "").Trim().ToLowerInvariant();
        Team = (Team ?? "").Trim();
        if (SportsLeagues.Find(Sport, League) is null || Team.Length > 12 || !Team.All(char.IsAsciiDigit))
        {
            (Sport, League, Team) = ("", "", "");
        }
        Name = Plain(Name, MostName);
        Abbreviation = Plain(Abbreviation, 8);
    }

    internal static string Plain(string? text, int most)
    {
        var plain = string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return plain.Length <= most ? plain : plain[..most].TrimEnd();
    }
}

/// <summary>A team a search found: where it plays, its id there, its name, abbreviation and logo.</summary>
public sealed record SportsTeam(string Sport, string League, string Id, string Name, string Abbreviation, string Logo)
{
    /// <summary>The league of the table it plays in.</summary>
    public SportsLeague? Of => SportsLeagues.Find(Sport, League);
}

/// <summary>Where a match stands.</summary>
public enum MatchState
{
    /// <summary>Not begun.</summary>
    Scheduled,

    /// <summary>On now: half-time and a delay included.</summary>
    Live,

    /// <summary>Played to its end.</summary>
    Final,

    /// <summary>Put off to another day, or stopped to be finished another day.</summary>
    Postponed,

    /// <summary>Called off, abandoned or forfeited.</summary>
    Canceled,
}

/// <summary>One side of a match.</summary>
public sealed class SportsSide
{
    public string Id { get; set; } = "";

    public string Abbreviation { get; set; } = "";

    /// <summary>Its short name, "Real Madrid", "Lakers".</summary>
    public string Name { get; set; } = "";

    /// <summary>§479: its full name, "Paris Saint-Germain", "Los Angeles Lakers"; empty where ESPN gave only the short one.</summary>
    public string FullName { get; set; } = "";

    /// <summary>Its score as ESPN writes it, "2"; empty before the match.</summary>
    public string Score { get; set; } = "";

    /// <summary>§479: its goals in a penalty shoot-out, "4"; empty where there was none.</summary>
    public string Shootout { get; set; } = "";

    /// <summary>§479: its goals over both legs of a tie, "5", after this one; empty for a match of one leg.</summary>
    public string Aggregate { get; set; } = "";

    /// <summary>§479: it goes through to the next round, the tie decided.</summary>
    public bool Advances { get; set; }

    /// <summary>ESPN's logo's address, empty where it gave none.</summary>
    public string Logo { get; set; } = "";

    public bool Winner { get; set; }
}

/// <summary>One match as ESPN said it, in a schedule or on a scoreboard.</summary>
public sealed class SportsMatch
{
    public string Id { get; set; } = "";

    public DateTime StartUtc { get; set; }

    /// <summary>Whether the kick-off's time is known; else only its day ("TBD").</summary>
    public bool TimeKnown { get; set; } = true;

    public MatchState State { get; set; }

    /// <summary>ESPN's status, "STATUS_HALFTIME": what tells half-time and the kind of stop apart.</summary>
    public string Status { get; set; } = "";

    /// <summary>The game's clock, "67'", "5:32"; the inning's half for baseball ("Top 5th").</summary>
    public string Clock { get; set; } = "";

    public int Period { get; set; }

    /// <summary>The league it is played in, ESPN's slug, and its name.</summary>
    public string League { get; set; } = "";

    public string LeagueName { get; set; } = "";

    /// <summary>ESPN's page for it; empty where it gave none.</summary>
    public string Link { get; set; } = "";

    public SportsSide Home { get; set; } = new();

    public SportsSide Away { get; set; } = new();

    /// <summary>
    /// §479: the round or the stage it is played in, as ESPN names it: "Group F", "Round of 16", "League Phase",
    /// "Quarterfinals"; empty for a league's ordinary matchday.
    /// </summary>
    public string Round { get; set; } = "";

    /// <summary>§479: which leg of a tie of two it is, 1 or 2; 0 for a match of one.</summary>
    public int Leg { get; set; }

    /// <summary>§479: it was played to its end after extra time ("AET"), or into a shoot-out.</summary>
    [JsonIgnore]
    public bool ExtraTime => Status.Contains("AET", StringComparison.OrdinalIgnoreCase) || Penalties;

    /// <summary>§479: it went to penalties: a shoot-out on now or decided by one.</summary>
    [JsonIgnore]
    public bool Penalties => Status.EndsWith("_PEN", StringComparison.OrdinalIgnoreCase) || Status.Contains("SHOOTOUT", StringComparison.OrdinalIgnoreCase)
        || (Home.Shootout.Length > 0 && Away.Shootout.Length > 0);

    /// <summary>Whether <paramref name="team"/> plays in it.</summary>
    public bool Plays(string team) => Home.Id == team || Away.Id == team;

    /// <summary>The side <paramref name="team"/> is, and the other.</summary>
    public (SportsSide Own, SportsSide Other, bool Home) Sides(string team) =>
        Away.Id == team ? (Away, Home, false) : (Home, Away, true);
}

/// <summary>What the sports store keeps for a team: its matches, the league's others on the day, and what ESPN says of it.</summary>
public sealed class SportsAnswer
{
    /// <summary>The team's matches, by kick-off.</summary>
    public List<SportsMatch> Matches { get; set; } = [];

    /// <summary>The other matches of the league its live or nearest match is in, on that day.</summary>
    public List<SportsMatch> Others { get; set; } = [];

    /// <summary>Where it stands, as ESPN writes it ("1st in Spanish LALIGA"); empty where it said nothing.</summary>
    public string Standing { get; set; } = "";

    /// <summary>Its record, "9-0-0".</summary>
    public string Record { get; set; } = "";

    /// <summary>Its logo's address.</summary>
    public string Logo { get; set; } = "";

    /// <summary>When the schedule was last read whole; a live fetch reads only the scoreboard.</summary>
    public DateTime ScheduledUtc { get; set; }

    /// <summary>§369: the league's table — its groups or conferences, each in order —; empty where ESPN keeps none.</summary>
    public List<SportsGroup> Table { get; set; } = [];

    /// <summary>§479: a cup's knockout matches, every round's so far and those fixed to come, by kick-off; empty for a league.</summary>
    public List<SportsMatch> Knockout { get; set; } = [];

    /// <summary>§479: the months of the knockout read ("202607"), so a month over is not read again.</summary>
    public List<string> KnockoutMonths { get; set; } = [];
}

/// <summary>§479: one stage of a cup as its calendar lists it: "Round of 16", and the days it is played over.</summary>
public sealed record SportsStage(string Label, DateTime StartUtc, DateTime EndUtc);

/// <summary>§369: one table of a league: the whole league's, or a group's or a conference's ("Group A").</summary>
public sealed class SportsGroup
{
    /// <summary>Its name where the league has several ("Group A", "Eastern Conference"); empty for a league's one table.</summary>
    public string Name { get; set; } = "";

    /// <summary>Its teams, first place first.</summary>
    public List<SportsPlace> Rows { get; set; } = [];
}

/// <summary>§369: a team's place in a table: where it stands, who it is, and what stands it there — football's points, else its wins and losses.</summary>
public sealed class SportsPlace
{
    public int Rank { get; set; }

    public string Id { get; set; } = "";

    public string Abbreviation { get; set; } = "";

    /// <summary>Its short name, "Bayern".</summary>
    public string Name { get; set; } = "";

    /// <summary>§479: its full name, "Bayern Munich".</summary>
    public string FullName { get; set; } = "";

    public string Logo { get; set; } = "";

    /// <summary>Football's points, "17"; else its record, "5-1".</summary>
    public string Value { get; set; } = "";

    /// <summary>§479: the games it played, won, drew and lost, as ESPN writes them, "7"; empty where it gave none.</summary>
    public string Played { get; set; } = "";

    public string Won { get; set; } = "";

    public string Drawn { get; set; } = "";

    public string Lost { get; set; } = "";

    /// <summary>§479: its goal difference, "+19", or its points' (basketball's).</summary>
    public string Difference { get; set; } = "";

    /// <summary>§479: its points, "17" (football's, hockey's).</summary>
    public string Points { get; set; } = "";

    /// <summary>§479: how many games it is behind the first, "4.5", "-" for the first (basketball's, baseball's).</summary>
    public string Behind { get; set; } = "";

    /// <summary>§479: the colour ESPN marks its place with, "#81D6AC" — the zone it stands in; empty for none.</summary>
    public string Zone { get; set; } = "";

    /// <summary>§479: what that zone is, as ESPN says it, "Champions League", "Relegation"; empty for none.</summary>
    public string ZoneText { get; set; } = "";
}

/// <summary>ESPN's answers read, and the requests asked (see the top of the file).</summary>
public static class SportsAnswers
{
    private const string Site = "https://site.api.espn.com/apis/site/v2/sports/";

    /// <summary>The most teams a search asks for: the menu shows every one, scrolling (§271).</summary>
    public const int SearchCount = 20;

    /// <summary>
    /// The schedule's requests for <paramref name="team"/>: for football, every competition it plays in
    /// (ESPN's "all"), its results and its fixtures apart; for the other sports its league's season,
    /// which holds both. Its id and the league's slug, nothing else.
    /// </summary>
    public static IReadOnlyList<string> ScheduleUrls(SportsOptions team)
    {
        var id = Uri.EscapeDataString(team.Team);
        return team.Sport == SportsLeagues.Soccer
            ? [$"{Site}soccer/all/teams/{id}/schedule", $"{Site}soccer/all/teams/{id}/schedule?fixture=true"]
            : [$"{Site}{team.Sport}/{team.League}/teams/{id}/schedule"];
    }

    /// <summary>A league's scoreboard for <paramref name="day"/>, the day as ESPN's own calendar has it (US Eastern).</summary>
    public static string ScoreboardUrl(string sport, string league, DateOnly day) =>
        $"{Site}{sport}/{league}/scoreboard?dates={day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}";

    /// <summary>ESPN's search for a team by what was typed, and nothing else.</summary>
    public static string SearchUrl(string text) =>
        $"https://site.web.api.espn.com/apis/common/v3/search?query={Uri.EscapeDataString(text)}&type=team&limit={SearchCount}";

    /// <summary>§369: a league's scoreboard on the day ESPN shows now (the day of its games on, else its latest), or on <paramref name="day"/>.</summary>
    public static string LeagueUrl(string sport, string league, DateOnly? day = null) =>
        day is { } on ? ScoreboardUrl(sport, league, on) : $"{Site}{sport}/{league}/scoreboard";

    /// <summary>§369: a league's teams, every one, for a search that names the league.</summary>
    public static string TeamsUrl(string sport, string league) => $"{Site}{sport}/{league}/teams";

    /// <summary>§369: a league's table — on ESPN's older path, the one that keeps it (the site path answers with a link alone).</summary>
    public static string TableUrl(string sport, string league) =>
        $"https://site.api.espn.com/apis/v2/sports/{sport}/{league}/standings";

    /// <summary>The day ESPN files a match that starts at <paramref name="utc"/> under: its date in New York.</summary>
    public static DateOnly EspnDay(DateTime utc)
    {
        try
        {
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"))); // not-ui: Windows' zone id
        }
        catch (Exception error) when (error is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(utc.AddHours(-5));
        }
    }

    /// <summary>
    /// The teams a search answer found in a league of the table, in its order; one found elsewhere —
    /// another sport, a league the table does not hold, a team with no id — is left out. A team whose
    /// league is not the table's but whose home league is, is offered in its home league.
    /// </summary>
    public static List<SportsTeam> Teams(string json)
    {
        var found = new List<SportsTeam>();
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return found;
        }
        foreach (var item in items.EnumerateArray())
        {
            var sport = Text(item, "sport");
            var id = Text(item, "id");
            var league = SportsLeagues.Find(sport, Text(item, "league")) ?? SportsLeagues.Find(sport, Text(item, "defaultLeagueSlug"));
            if (league is null || id.Length == 0 || !id.All(char.IsAsciiDigit)
                || (Text(item, "type") is { Length: > 0 } type && type != "team")
                || found.Any(team => team.Id == id && team.League == league.League))
            {
                continue;
            }
            found.Add(new SportsTeam(league.Sport, league.League, id, Text(item, "displayName"), Text(item, "abbreviation"), FirstLogo(item)));
        }
        return found;
    }

    /// <summary>
    /// The matches in a schedule or a scoreboard answer, each with its league (its own, else the
    /// answer's, else <paramref name="league"/>). One without an id, a kick-off or two sides is left out.
    /// </summary>
    public static List<SportsMatch> Matches(string json, string league = "")
    {
        var found = new List<SportsMatch>();
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
        {
            return found;
        }
        var root = document.RootElement;
        var (answerSlug, answerName) = ("", "");
        if (root.TryGetProperty("leagues", out var leagues) && leagues.ValueKind == JsonValueKind.Array && leagues.GetArrayLength() > 0)
        {
            answerSlug = Text(leagues[0], "slug");
            answerName = Text(leagues[0], "abbreviation") is { Length: > 0 } abbreviation ? abbreviation : Text(leagues[0], "name");
        }
        foreach (var each in events.EnumerateArray())
        {
            if (Match(each, answerSlug.Length > 0 ? answerSlug : league, answerName) is { } match)
            {
                found.Add(match);
            }
        }
        found.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        return found;
    }

    /// <summary>
    /// Whether <paramref name="json"/> is an answer of matches at all — an object with its list of
    /// events, empty or not — and not an error page; or, for <paramref name="list"/> another than
    /// "events", an answer of that list ("children", a table's; "sports", a league's teams').
    /// </summary>
    public static bool Readable(string json, string list = "events")
    {
        using var document = Parse(json);
        return document is not null && document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty(list, out var events) && events.ValueKind == JsonValueKind.Array;
    }

    /// <summary>
    /// §369: a league's teams in a teams answer, by name, each found in <paramref name="league"/>; none
    /// for a league the table does not hold.
    /// </summary>
    public static List<SportsTeam> LeagueTeams(string json, SportsLeague league)
    {
        var found = new List<SportsTeam>();
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("sports", out var sports) || sports.ValueKind != JsonValueKind.Array)
        {
            return found;
        }
        foreach (var sport in sports.EnumerateArray())
        {
            if (!sport.TryGetProperty("leagues", out var leagues) || leagues.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var each in leagues.EnumerateArray())
            {
                if (!each.TryGetProperty("teams", out var teams) || teams.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (var entry in teams.EnumerateArray())
                {
                    var team = entry.TryGetProperty("team", out var its) && its.ValueKind == JsonValueKind.Object ? its : entry;
                    var id = Text(team, "id");
                    if (id.Length == 0 || !id.All(char.IsAsciiDigit) || found.Any(kept => kept.Id == id))
                    {
                        continue;
                    }
                    found.Add(new SportsTeam(league.Sport, league.League, id, Text(team, "displayName"), Text(team, "abbreviation"), FirstLogo(team)));
                }
            }
        }
        found.Sort((one, other) => string.Compare(one.Name, other.Name, StringComparison.CurrentCultureIgnoreCase));
        return found;
    }

    /// <summary>
    /// §369: a league's table in a standings answer: each group or conference with its teams in order —
    /// football's by its rank and its points; the others' by their seed, else their winning share, with
    /// their record. A group's name only where there are several. Empty where there is none.
    /// </summary>
    public static List<SportsGroup> Table(string json, string sport)
    {
        var groups = new List<SportsGroup>();
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
        {
            return groups;
        }
        var football = sport == SportsLeagues.Soccer;
        foreach (var child in children.EnumerateArray())
        {
            if (!child.TryGetProperty("standings", out var standings) || standings.ValueKind != JsonValueKind.Object
                || !standings.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            var rows = new List<(SportsPlace Place, double Share)>();
            foreach (var entry in entries.EnumerateArray())
            {
                var team = entry.TryGetProperty("team", out var its) && its.ValueKind == JsonValueKind.Object ? its : default;
                if (team.ValueKind != JsonValueKind.Object || Text(team, "id") is not { Length: > 0 } id)
                {
                    continue;
                }
                var stats = Stats(entry);
                var rank = Number(stats, football ? "rank" : "playoffSeed");
                var note = entry.TryGetProperty("note", out var marked) && marked.ValueKind == JsonValueKind.Object ? marked : default;
                rows.Add((new SportsPlace
                {
                    Rank = rank is > 0 and < 1000 ? (int)rank : 0,
                    Id = id,
                    Abbreviation = Text(team, "abbreviation"),
                    Name = Text(team, "shortDisplayName") is { Length: > 0 } shortName ? shortName : Text(team, "displayName"),
                    FullName = Text(team, "displayName"),
                    Logo = Text(team, "logo") is { Length: > 0 } logo ? logo : FirstLogo(team),
                    Value = football ? Shown(stats, "points") : Shown(stats, "overall"),
                    Played = Shown(stats, "gamesPlayed"),
                    Won = Shown(stats, "wins"),
                    Drawn = Shown(stats, "ties"),
                    Lost = Shown(stats, "losses"),
                    Difference = Shown(stats, "pointDifferential"),
                    Points = Shown(stats, "points"),
                    Behind = Shown(stats, "gamesBehind"),
                    Zone = note.ValueKind == JsonValueKind.Object && Text(note, "color") is { Length: 7 } color && color[0] == '#' && color[1..].All(char.IsAsciiHexDigit) ? color : "",
                    ZoneText = note.ValueKind == JsonValueKind.Object ? Text(note, "description") : "",
                }, Number(stats, "winPercent")));
            }
            // Ranked first, by their rank; the unranked after them, the best share first.
            rows.Sort((one, other) => (one.Place.Rank, other.Place.Rank) switch
            {
                ( > 0, > 0) => one.Place.Rank.CompareTo(other.Place.Rank),
                ( > 0, _) => -1,
                (_, > 0) => 1,
                _ => other.Share.CompareTo(one.Share),
            });
            // A seed is an order, not a place two may share (ESPN gives two the 8th): the others' place is their row's.
            for (var at = 0; at < rows.Count; at++)
            {
                if (rows[at].Place.Rank == 0 || !football)
                {
                    rows[at].Place.Rank = at + 1;
                }
            }
            groups.Add(new SportsGroup { Name = Text(child, "name"), Rows = [.. rows.Select(row => row.Place)] });
        }
        if (groups.Count == 1)
        {
            groups[0].Name = "";
        }
        return groups;
    }

    /// <summary>
    /// §369: the first day after <paramref name="after"/> a league's scoreboard answer lists as one with
    /// games (its calendar, where ESPN lists the days with games rather than the days without); null for none.
    /// </summary>
    public static DateOnly? NextDay(string json, DateOnly after)
    {
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("leagues", out var leagues) || leagues.ValueKind != JsonValueKind.Array || leagues.GetArrayLength() == 0)
        {
            return null;
        }
        var league = leagues[0];
        if (Flag(league, "calendarIsWhitelist") != true || !league.TryGetProperty("calendar", out var calendar) || calendar.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        DateOnly? next = null;
        foreach (var day in calendar.EnumerateArray())
        {
            // "2026-10-31T07:00Z": a day as ESPN files it, its date the day.
            if (day.ValueKind == JsonValueKind.String && day.GetString() is { Length: >= 10 } text
                && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && date > after && (next is null || date < next))
            {
                next = date;
            }
        }
        return next;
    }

    /// <summary>
    /// §479: a cup's stages as its scoreboard's calendar lists them ("Group", "Round of 32" … "Final", each with its
    /// days), in order; none for a league, whose calendar is its days.
    /// </summary>
    public static List<SportsStage> Stages(string json)
    {
        var stages = new List<SportsStage>();
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("leagues", out var leagues) || leagues.ValueKind != JsonValueKind.Array || leagues.GetArrayLength() == 0
            || !leagues[0].TryGetProperty("calendar", out var calendar) || calendar.ValueKind != JsonValueKind.Array)
        {
            return stages;
        }
        foreach (var season in calendar.EnumerateArray())
        {
            if (season.ValueKind != JsonValueKind.Object || !season.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            foreach (var entry in entries.EnumerateArray())
            {
                if (Text(entry, "label") is { Length: > 0 } label && When(Text(entry, "startDate")) is { } start && When(Text(entry, "endDate")) is { } end && end > start)
                {
                    stages.Add(new SportsStage(label, start, end));
                }
            }
        }
        stages.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        return stages;
    }

    /// <summary>§479: how far ahead a cup's knockout is read: its next round's draw is out by then.</summary>
    public static readonly TimeSpan KnockoutAhead = TimeSpan.FromDays(35);

    /// <summary>
    /// §479: the months a cup's knockout is read in ("202606", "202607"): from its first knockout stage — every stage
    /// after its last group or league phase — to its last stage's beginning, or to <see cref="KnockoutAhead"/> from <paramref name="utcNow"/>
    /// where that is sooner. None for a cup with no knockout, or whose knockout is further off than that.
    /// </summary>
    public static List<string> KnockoutMonths(IReadOnlyList<SportsStage> stages, DateTime utcNow)
    {
        var knockout = KnockoutStages(stages);
        if (knockout.Count == 0 || knockout[0].StartUtc > utcNow + KnockoutAhead)
        {
            return [];
        }
        var first = new DateOnly(knockout[0].StartUtc.Year, knockout[0].StartUtc.Month, 1);
        // To the last stage's beginning: a calendar's stage runs on past its last match (the World Cup's final "to 1 August").
        var until = knockout.Max(stage => stage.StartUtc) is var end && end < utcNow + KnockoutAhead ? end : utcNow + KnockoutAhead;
        var last = new DateOnly(until.Year, until.Month, 1);
        var months = new List<string>();
        for (var month = first; month <= last && months.Count < 12; month = month.AddMonths(1))
        {
            months.Add(month.ToString("yyyyMM", CultureInfo.InvariantCulture));
        }
        return months;
    }

    /// <summary>§479: a cup's knockout stages: every stage after its last group or league phase; none where it has no such phase.</summary>
    public static List<SportsStage> KnockoutStages(IReadOnlyList<SportsStage> stages)
    {
        var table = -1;
        for (var at = 0; at < stages.Count; at++)
        {
            if (stages[at].Label.Contains("group", StringComparison.OrdinalIgnoreCase) || stages[at].Label.Contains("league", StringComparison.OrdinalIgnoreCase))
            {
                table = at;
            }
        }
        return table < 0 ? [] : [.. stages.Skip(table + 1)];
    }

    /// <summary>§479: a league's scoreboard for a whole month, "202607": every match of it in one answer.</summary>
    public static string MonthUrl(string sport, string league, string month) =>
        $"{Site}{sport}/{league}/scoreboard?dates={Uri.EscapeDataString(month)}&limit=200";

    /// <summary>A standings entry's statistics by their names.</summary>
    private static Dictionary<string, JsonElement> Stats(JsonElement entry)
    {
        var stats = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (entry.TryGetProperty("stats", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var stat in list.EnumerateArray())
            {
                if (Text(stat, "name") is { Length: > 0 } name)
                {
                    stats.TryAdd(name, stat);
                }
            }
        }
        return stats;
    }

    private static double Number(Dictionary<string, JsonElement> stats, string name) =>
        stats.TryGetValue(name, out var stat) && stat.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble()
        : stats.TryGetValue(name, out stat) && double.TryParse(Text(stat, "displayValue"), NumberStyles.Float, CultureInfo.InvariantCulture, out var shown) ? shown
        : 0;

    private static string Shown(Dictionary<string, JsonElement> stats, string name) =>
        stats.TryGetValue(name, out var stat) ? Text(stat, "displayValue") : "";

    /// <summary>What a schedule answer says of the team itself: where it stands, its record and its logo.</summary>
    public static (string Standing, string Record, string Logo) TeamOf(string json)
    {
        using var document = Parse(json);
        if (document is null || !document.RootElement.TryGetProperty("team", out var team) || team.ValueKind != JsonValueKind.Object)
        {
            return ("", "", "");
        }
        return (Text(team, "standingSummary"), Text(team, "recordSummary"), Text(team, "logo") is { Length: > 0 } logo ? logo : FirstLogo(team));
    }

    /// <summary>
    /// <paramref name="answer"/> with what a scoreboard said: each of the team's matches it holds
    /// taken from it (fresher than the schedule), and the league's other matches of the day kept beside
    /// them. A match of the team's the schedule did not have yet is added.
    /// </summary>
    public static void Merge(SportsAnswer answer, IReadOnlyList<SportsMatch> scoreboard, string team)
    {
        foreach (var match in scoreboard.Where(match => match.Plays(team)))
        {
            var at = answer.Matches.FindIndex(kept => kept.Id == match.Id);
            if (at >= 0)
            {
                // A schedule names the competition; a league's scoreboard only its own.
                match.LeagueName = match.LeagueName.Length > 0 ? match.LeagueName : answer.Matches[at].LeagueName;
                answer.Matches[at] = match;
            }
            else
            {
                answer.Matches.Add(match);
            }
        }
        answer.Matches.Sort((one, other) => one.StartUtc.CompareTo(other.StartUtc));
        answer.Others = [.. scoreboard.Where(match => !match.Plays(team))];
    }

    private static SportsMatch? Match(JsonElement each, string league, string leagueName)
    {
        if (!each.TryGetProperty("competitions", out var competitions) || competitions.ValueKind != JsonValueKind.Array || competitions.GetArrayLength() == 0)
        {
            return null;
        }
        var competition = competitions[0];
        var id = Text(each, "id");
        if (id.Length == 0 || When(Text(each, "date")) is not { } start
            || !competition.TryGetProperty("competitors", out var sides) || sides.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        SportsSide? home = null;
        SportsSide? away = null;
        foreach (var side in sides.EnumerateArray())
        {
            var read = Side(side);
            if (Text(side, "homeAway") == "away")
            {
                away ??= read;
            }
            else
            {
                home ??= read;
            }
        }
        if (home is null || away is null)
        {
            return null;
        }
        var status = competition.TryGetProperty("status", out var own) && own.ValueKind == JsonValueKind.Object ? own
            : each.TryGetProperty("status", out var shared) && shared.ValueKind == JsonValueKind.Object ? shared
            : default;
        var (state, name, clock, period) = Status(status);
        var slug = league;
        var named = leagueName;
        if (each.TryGetProperty("league", out var its) && its.ValueKind == JsonValueKind.Object)
        {
            slug = Text(its, "slug") is { Length: > 0 } ownSlug ? ownSlug : slug;
            named = Text(its, "abbreviation") is { Length: > 0 } abbreviation ? abbreviation : Text(its, "name") is { Length: > 0 } full ? full : named;
        }
        var timeValid = !(Flag(competition, "timeValid") == false || Flag(each, "timeValid") == false);
        // §479: a tie of two legs — a scoreboard says each side's aggregate in the tie's series, a schedule in its score.
        if (competition.TryGetProperty("series", out var series) && series.ValueKind == JsonValueKind.Object
            && series.TryGetProperty("competitors", out var tied) && tied.ValueKind == JsonValueKind.Array)
        {
            var decided = Flag(series, "completed") == true;
            foreach (var entry in tied.EnumerateArray())
            {
                var side = Text(entry, "id") is { Length: > 0 } sideId ? sideId == home.Id ? home : sideId == away.Id ? away : null : null;
                if (side is not null)
                {
                    side.Aggregate = side.Aggregate.Length > 0 ? side.Aggregate : Whole(entry, "aggregateScore");
                    side.Advances |= decided && Flag(entry, "winner") == true;
                }
            }
        }
        var leg = competition.TryGetProperty("leg", out var legs) && legs.ValueKind == JsonValueKind.Object
            && legs.TryGetProperty("value", out var legValue) && legValue.ValueKind == JsonValueKind.Number && legValue.TryGetInt32(out var legNumber) && legNumber is 1 or 2
            ? legNumber : 0;
        return new SportsMatch
        {
            Id = id,
            StartUtc = start,
            TimeKnown = timeValid,
            State = state,
            Status = name,
            Clock = clock,
            Period = period,
            League = slug,
            LeagueName = named,
            Link = Link(each),
            Home = home,
            Away = away,
            Round = Round(each, competition),
            Leg = leg,
        };
    }

    /// <summary>
    /// §479: the round a match is played in: a scoreboard's note after the competition's name ("FIFA World Cup,
    /// Round of 32" is "Round of 32"), else a schedule's stage where it is one and not the season's own name
    /// ("League Phase", not "2025-26 Portuguese Primeira Liga"); empty for neither.
    /// </summary>
    private static string Round(JsonElement each, JsonElement competition)
    {
        var note = Text(competition, "altGameNote");
        if (note.IndexOf(", ", StringComparison.Ordinal) is var comma and > 0)
        {
            return note[(comma + 2)..].Trim();
        }
        return each.TryGetProperty("seasonType", out var stage) && stage.ValueKind == JsonValueKind.Object
            && Text(stage, "name") is { Length: > 0 } name && !char.IsAsciiDigit(name[0])
            ? name : "";
    }

    /// <summary>A whole number ESPN writes as a number (5.0) or as words ("5"), as words, "5"; empty for none.</summary>
    private static string Whole(JsonElement element, string property) =>
        element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value) ? ""
        : value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) && number is >= 0 and < 1000 ? ((int)Math.Round(number)).ToString(CultureInfo.InvariantCulture)
        : value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim() ?? ""
        : "";

    private static SportsSide Side(JsonElement side)
    {
        var team = side.TryGetProperty("team", out var its) && its.ValueKind == JsonValueKind.Object ? its : default;
        var score = side.TryGetProperty("score", out var value) ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()?.Trim() ?? "",
            // A schedule writes it { "value": 2.0, "displayValue": "2" }.
            JsonValueKind.Object => Text(value, "displayValue"),
            JsonValueKind.Number => value.GetRawText(),
            _ => "",
        } : "";
        // §479: a shoot-out's and a tie's goals beside the score — a scoreboard's on the side, a schedule's in its score.
        var scored = side.TryGetProperty("score", out var held) && held.ValueKind == JsonValueKind.Object ? held : default;
        return new SportsSide
        {
            Id = team.ValueKind == JsonValueKind.Object ? Text(team, "id") : "",
            Abbreviation = team.ValueKind == JsonValueKind.Object ? Text(team, "abbreviation") : "",
            Name = team.ValueKind != JsonValueKind.Object ? ""
                : Text(team, "shortDisplayName") is { Length: > 0 } shortName ? shortName : Text(team, "displayName"),
            FullName = team.ValueKind == JsonValueKind.Object ? Text(team, "displayName") : "",
            Shootout = Whole(side, "shootoutScore") is { Length: > 0 } shootout ? shootout : Whole(scored, "shootoutScore"),
            Aggregate = Whole(side, "aggregateScore") is { Length: > 0 } aggregate ? aggregate : Whole(scored, "aggregateScore"),
            Advances = Flag(side, "advance") == true,
            Score = score,
            Logo = team.ValueKind != JsonValueKind.Object ? ""
                : Text(team, "logo") is { Length: > 0 } logo ? logo : FirstLogo(team),
            Winner = Flag(side, "winner") == true,
        };
    }

    /// <summary>ESPN's status read: where the match stands, its status's name, its clock and its period.</summary>
    private static (MatchState State, string Name, string Clock, int Period) Status(JsonElement status)
    {
        if (status.ValueKind != JsonValueKind.Object)
        {
            return (MatchState.Scheduled, "", "", 0);
        }
        var type = status.TryGetProperty("type", out var its) && its.ValueKind == JsonValueKind.Object ? its : default;
        var name = type.ValueKind == JsonValueKind.Object ? Text(type, "name") : "";
        var state = type.ValueKind == JsonValueKind.Object ? Text(type, "state") : "";
        var period = status.TryGetProperty("period", out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole) ? whole : 0;
        // Baseball's half of the inning is in the words alone, "Top 5th"; no other sport's start so.
        var detail = type.ValueKind == JsonValueKind.Object ? Text(type, "shortDetail") : "";
        var clock = state == "in" && Inning(detail) is not null ? detail : Text(status, "displayClock");
        var upper = name.ToUpperInvariant();
        var stopped = upper.Contains("POSTPONED", StringComparison.Ordinal) || upper.Contains("SUSPENDED", StringComparison.Ordinal) || upper.Contains("RESCHEDULED", StringComparison.Ordinal);
        var called = upper.Contains("CANCEL", StringComparison.Ordinal) || upper.Contains("ABANDON", StringComparison.Ordinal) || upper.Contains("FORFEIT", StringComparison.Ordinal);
        var completed = type.ValueKind == JsonValueKind.Object && Flag(type, "completed") == true;
        var read = stopped ? MatchState.Postponed
            : called ? MatchState.Canceled
            : state == "in" ? MatchState.Live
            : state == "post" ? completed ? MatchState.Final : MatchState.Postponed
            : MatchState.Scheduled;
        return (read, name, clock, period);
    }

    /// <summary>Which half of an inning baseball's words say: true for the top, false for the bottom, null for neither ("Mid" is the top over, "End" the bottom).</summary>
    public static bool? Inning(string? detail) =>
        detail is null ? null
        : detail.StartsWith("Top", StringComparison.Ordinal) || detail.StartsWith("Mid", StringComparison.Ordinal) ? true
        : detail.StartsWith("Bot", StringComparison.Ordinal) || detail.StartsWith("End", StringComparison.Ordinal) ? false
        : null;

    /// <summary>ESPN's own page for a match, the desktop one; empty where it gave none.</summary>
    private static string Link(JsonElement each)
    {
        if (!each.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
        {
            return "";
        }
        foreach (var link in links.EnumerateArray())
        {
            var href = Text(link, "href");
            if (Opens(href) && link.TryGetProperty("rel", out var rel) && rel.ValueKind == JsonValueKind.Array
                && rel.EnumerateArray().Any(word => word.ValueKind == JsonValueKind.String && word.GetString() == "desktop"))
            {
                return href;
            }
        }
        return "";
    }

    /// <summary>Whether <paramref name="address"/> is a page of ESPN's, which a click may open in the browser.</summary>
    public static bool Opens(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps
        && (url.Host == "espn.com" || url.Host.EndsWith(".espn.com", StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether <paramref name="address"/> is a logo on ESPN's picture server, the one host a logo is fetched from.</summary>
    public static bool IsLogo(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps && url.Host == "a.espncdn.com";

    /// <summary>
    /// A logo asked for at <paramref name="side"/> px through ESPN's own resizer, so a 500 px picture
    /// is not fetched for a 24 px circle: "https://a.espncdn.com/combiner/i?img=/i/teamlogos/…&amp;w=64&amp;h=64".
    /// </summary>
    /// <para>Where <paramref name="dark"/>, its drawing for a dark ground ("/500-dark/"), which ESPN keeps beside most.</para>
    public static string LogoUrl(string address, int side, bool dark = false)
    {
        if (!IsLogo(address) || !Uri.TryCreate(address, UriKind.Absolute, out var url) || url.AbsolutePath.StartsWith("/combiner/", StringComparison.Ordinal))
        {
            return address;
        }
        var path = dark ? url.AbsolutePath.Replace("/500/", "/500-dark/", StringComparison.Ordinal) : url.AbsolutePath;
        var size = side.ToString(CultureInfo.InvariantCulture);
        return $"https://a.espncdn.com/combiner/i?img={path}&w={size}&h={size}";
    }

    private static string FirstLogo(JsonElement owner) =>
        owner.TryGetProperty("logos", out var logos) && logos.ValueKind == JsonValueKind.Array && logos.GetArrayLength() > 0
            ? Text(logos[0], "href")
            : "";

    private static DateTime? When(string text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when) ? when.UtcDateTime : null;

    /// <summary>The answer as a document whose root is an object; null for anything else.</summary>
    private static JsonDocument? Parse(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                return document;
            }
            document.Dispose();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? Flag(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim() ?? ""
            : "";
}

/// <summary>What a sports widget shows of its team's matches (<see cref="SportsPick.Of"/>).</summary>
public enum SportsShows
{
    /// <summary>No match at all: none played, none to come.</summary>
    Nothing,

    /// <summary>A match on now.</summary>
    Live,

    /// <summary>A match played to its end a few hours ago.</summary>
    Final,

    /// <summary>The next match.</summary>
    Next,

    /// <summary>The last match played, with nothing to come yet (the season over).</summary>
    Last,
}

/// <summary>
/// What a sports widget shows (<see cref="SportsPick.Of"/>): its match and why, the matches after it,
/// the ones before it, newest first, and the league's other matches of the day.
/// </summary>
public sealed record SportsView(SportsShows Shows, SportsMatch? Match, IReadOnlyList<SportsMatch> Coming, IReadOnlyList<SportsMatch> Played, IReadOnlyList<SportsMatch> Others)
{
    public static SportsView None { get; } = new(SportsShows.Nothing, null, [], [], []);
}

/// <summary>Which of a team's matches a widget shows now (see the top of the file).</summary>
public static class SportsPick
{
    /// <summary>How long a final score stays after its kick-off: the match and a few hours after it, then the next.</summary>
    public static readonly TimeSpan FinalFor = TimeSpan.FromHours(6);

    /// <summary>How long a match whose kick-off has passed may still be "not begun" and be the next: a late start, a delay.</summary>
    public static readonly TimeSpan LateFor = TimeSpan.FromHours(3);

    /// <summary>How soon the next match takes over from a final score: an hour before its kick-off.</summary>
    public static readonly TimeSpan NextAhead = TimeSpan.FromHours(1);

    /// <summary>
    /// What <paramref name="answer"/> comes to at <paramref name="utcNow"/> for <paramref name="team"/>:
    /// a match on now, else one ended within <see cref="FinalFor"/> of its kick-off (until the next is
    /// within <see cref="NextAhead"/>), else the next — one put off or called off says so in its place
    /// until its day has passed — else the last played; nothing where there are none.
    /// </summary>
    public static SportsView Of(SportsAnswer? answer, string team, DateTime utcNow)
    {
        if (answer is null)
        {
            return SportsView.None;
        }
        var matches = answer.Matches.Where(match => team.Length == 0 || match.Plays(team)).OrderBy(match => match.StartUtc).ToList();
        var live = matches.FirstOrDefault(match => match.State == MatchState.Live);
        bool Coming(SportsMatch match) => match.State switch
        {
            MatchState.Scheduled => match.StartUtc >= utcNow - LateFor,
            MatchState.Postponed or MatchState.Canceled => match.StartUtc >= utcNow - FinalFor,
            _ => false,
        };
        var coming = matches.Where(match => match != live && Coming(match)).ToList();
        var played = matches.Where(match => match.State == MatchState.Final && match.StartUtc <= utcNow).OrderByDescending(match => match.StartUtc).ToList();
        var next = coming.FirstOrDefault();
        var recent = played.FirstOrDefault(match => match.StartUtc >= utcNow - FinalFor);
        if (live is not null)
        {
            return new SportsView(SportsShows.Live, live, coming, played, answer.Others);
        }
        if (recent is not null && (next is null || next.StartUtc - utcNow > NextAhead))
        {
            return new SportsView(SportsShows.Final, recent, coming, played.Skip(1).ToList(), answer.Others);
        }
        if (next is not null)
        {
            return new SportsView(SportsShows.Next, next, coming.Skip(1).ToList(), played, answer.Others);
        }
        return played.Count > 0
            ? new SportsView(SportsShows.Last, played[0], [], played.Skip(1).ToList(), answer.Others)
            : SportsView.None;
    }

    /// <summary>
    /// §369: a league's games of the round its view shows — from the day of the game on now, of the
    /// results just in, or of the next game, for <paramref name="days"/> days (a football round's weekend,
    /// SportsLeagues.RoundDays) — by kick-off, as ESPN files the days; none where it shows nothing.
    /// </summary>
    public static IReadOnlyList<SportsMatch> Day(SportsView view, SportsAnswer? answer, int days = 1)
    {
        if (view.Match is not { } shown || answer is null)
        {
            return [];
        }
        var first = SportsAnswers.EspnDay(shown.StartUtc);
        var last = first.AddDays(Math.Max(1, days) - 1);
        return [.. answer.Matches.Where(match => SportsAnswers.EspnDay(match.StartUtc) is var day && day >= first && day <= last)
            .OrderBy(match => match.StartUtc).ThenBy(match => match.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// §369: which of several views a face shows, the most pressing first: a game on now, then a result
    /// just in, then the soonest next game, then the last played; the first of equals. -1 for none at all.
    /// </summary>
    public static int Lead(IReadOnlyList<SportsView> views)
    {
        var best = -1;
        for (var at = 0; at < views.Count; at++)
        {
            if (views[at].Shows != SportsShows.Nothing && (best < 0 || Before(views[at], views[best])))
            {
                best = at;
            }
        }
        return best < 0 && views.Count > 0 ? 0 : best;
    }

    private static int Weight(SportsShows shows) => shows switch
    {
        SportsShows.Live => 0,
        SportsShows.Final => 1,
        SportsShows.Next => 2,
        SportsShows.Last => 3,
        _ => 4,
    };

    private static bool Before(SportsView one, SportsView other) =>
        Weight(one.Shows) != Weight(other.Shows) ? Weight(one.Shows) < Weight(other.Shows)
        : one.Shows == SportsShows.Next && one.Match is { } mine && other.Match is { } theirs && mine.StartUtc < theirs.StartUtc;
}

/// <summary>
/// How soon a sports widget's team is asked for again (see the top of the file). The one exception to
/// Karpo's one online interval (OnlineUpdates): a live score a quarter of an hour old is a wrong score,
/// so a match on now is asked for every half minute, and one about to begin every minute. Otherwise
/// the interval every online widget follows — but never past the next kick-off, so a match is not
/// missed by a long interval. A failure waits longer each time it repeats.
/// </summary>
public static class SportsPace
{
    /// <summary>A match on now: every 30 seconds.</summary>
    public static readonly TimeSpan Live = TimeSpan.FromSeconds(30);

    /// <summary>A match beginning within <see cref="Soon"/>, or past its kick-off and not begun: every minute.</summary>
    public static readonly TimeSpan Starting = TimeSpan.FromMinutes(1);

    /// <summary>How near a kick-off is "about to begin".</summary>
    public static readonly TimeSpan Soon = TimeSpan.FromMinutes(15);

    /// <summary>The longest a failure waits before trying again, however often it failed.</summary>
    public static readonly TimeSpan MostRetry = TimeSpan.FromMinutes(30);

    /// <summary>How long a live fetch may go on reading only the scoreboard before the schedule is read whole again.</summary>
    public static readonly TimeSpan ScheduleEvery = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How soon after a fetch at <paramref name="utcNow"/> the next is due, for <paramref name="view"/>,
    /// with <paramref name="interval"/> the online interval.
    /// </summary>
    public static TimeSpan Every(SportsView view, DateTime utcNow, TimeSpan interval)
    {
        if (view.Shows == SportsShows.Live)
        {
            return Live;
        }
        var next = view.Shows == SportsShows.Next ? view.Match : view.Coming.FirstOrDefault();
        if (next is { State: MatchState.Scheduled })
        {
            var until = next.StartUtc - utcNow;
            if (until <= Soon)
            {
                return Starting;
            }
            // Not past the kick-off's minute: asked again as it nears.
            var before = until - Soon;
            return before < interval ? before : interval;
        }
        return interval;
    }

    /// <summary>
    /// How long after the <paramref name="failures"/>th failure in a row the next try is: the pace it
    /// had, doubled with each, never under half a minute nor over <see cref="MostRetry"/>.
    /// </summary>
    public static TimeSpan Retry(TimeSpan pace, int failures)
    {
        var wait = pace < Live ? Live : pace;
        for (var count = 1; count < failures && wait < MostRetry; count++)
        {
            wait += wait;
        }
        return wait > MostRetry ? MostRetry : wait;
    }

    /// <summary>
    /// Whether the next fetch may read only the scoreboard of the shown match's league: while it is on or
    /// about to begin, and the schedule was read within <see cref="ScheduleEvery"/>. The rest of the time
    /// the schedule is read whole, and the scoreboard with it where a match of the day is near.
    /// </summary>
    public static bool ScoreboardOnly(SportsView view, SportsAnswer? answer, DateTime utcNow) =>
        answer is not null && utcNow - answer.ScheduledUtc < ScheduleEvery
        && view.Match is { } match
        && (view.Shows == SportsShows.Live || (view.Shows == SportsShows.Next && match.State == MatchState.Scheduled && match.StartUtc - utcNow <= Soon));

    /// <summary>
    /// The match whose league's scoreboard a full fetch reads too: the one on now, or the shown one where
    /// it is within a day of now (for its clock and the day's other matches); null for none.
    /// </summary>
    public static SportsMatch? Scoreboard(SportsView view, DateTime utcNow) =>
        view.Match is { } match && (view.Shows == SportsShows.Live || (match.StartUtc - utcNow).Duration() <= TimeSpan.FromHours(12)) ? match : null;
}

/// <summary>
/// ESPN's search for a team, for the team's search field in a sports widget's menu and on its page in
/// Settings alike: awaited off the window's thread and cancellable, as the share's search is (Symbols).
/// No cookies, no key, and nothing in the request but what was typed.
/// </summary>
public static class SportsSearch
{
    /// <summary>Fewer letters than this match half of every league.</summary>
    public const int LeastLetters = 2;

    /// <summary>How long the field waits after the last key before it asks.</summary>
    public static readonly TimeSpan Wait = TimeSpan.FromMilliseconds(300);

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient(new HttpClientHandler { UseCookies = false }) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("karpo");
        return client;
    }

    /// <summary>The teams in the table's leagues that <paramref name="text"/> finds, best first. Throws on network and service errors.</summary>
    public static async Task<List<SportsTeam>> FindAsync(string text, CancellationToken cancel)
    {
        using var response = await Http.GetAsync(SportsAnswers.SearchUrl(text), cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        return SportsAnswers.Teams(json);
    }

    /// <summary>§369: a league's teams, every one, by name. Throws on network and service errors.</summary>
    public static async Task<List<SportsTeam>> TeamsOfAsync(SportsLeague league, CancellationToken cancel)
    {
        using var response = await Http.GetAsync(SportsAnswers.TeamsUrl(league.Sport, league.League), cancel).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
        return SportsAnswers.LeagueTeams(json, league);
    }

    /// <summary>
    /// §369: the leagues of the table <paramref name="text"/> names — in their names, ESPN's longer
    /// names or their slugs, whatever the case and the accents ("bundes", "premier", "world cup", "nba") —
    /// best first — its name beginning with it, then holding it as a word of its own ("champions" the
    /// Champions League before the Championship), then the rest — else in the table's order. None for
    /// fewer than <see cref="LeastLetters"/>.
    /// </summary>
    public static List<SportsLeague> Leagues(string text)
    {
        var words = text.Trim();
        if (words.Length < LeastLetters)
        {
            return [];
        }
        var compare = CultureInfo.InvariantCulture.CompareInfo;
        const CompareOptions loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
        bool Holds(string part) => compare.IndexOf(part, words, loose) >= 0;
        int Rank(SportsLeague league)
        {
            if (compare.IsPrefix(league.Name, words, loose))
            {
                return 0;
            }
            var at = compare.IndexOf(league.Name, words, loose);
            return at < 0 ? 3 : at + words.Length >= league.Name.Length || !char.IsLetterOrDigit(league.Name[at + words.Length]) ? 1 : 2;
        }
        return [.. SportsLeagues.All
            .Where(league => Holds(league.Name) || Holds(league.Words) || Holds(league.League))
            .OrderBy(Rank)];
    }

    /// <summary>A search result's line under its name: its league's name and its abbreviation, "NBA · LAL".</summary>
    public static string Detail(SportsTeam team) =>
        string.Join(" · ", new[] { team.Of?.Name ?? "", team.Abbreviation }.Where(part => part.Length > 0));

    /// <summary>The line under the field: what it follows, "Barcelona · LALIGA" or "Bundesliga · Football", or, with nothing yet, what may be found.</summary>
    public static string Status(SportsOptions? team) =>
        team is not { IsSet: true } ? S.SettingsWidgetSportsTeamDescription
        : string.Join(" · ", new[] { team.Name, team.IsTeam ? team.Of?.Name ?? "" : SportsLeagues.SportName(team.Sport) }.Where(part => part.Length > 0));
}
