// sports §483: a round's or a stage's name in Karpo's words (SportsRounds) — every one ESPN sent in the answers saved
// on 2026-11-02 (SavedSports) and the stages its competitions are documented with, in English and in German, a group's
// letter and a round's number carried into the words; a name Karpo does not know shown as ESPN sent it; and the line's
// headings, a match's caption and its tooltip saying them so. Nothing reaches the network.

using Karpo.Dock.Sports;
using Karpo.Tests.Localization;

namespace Karpo.Tests.Sports;

[Collection(InLanguage.Collection)]
public sealed class SportsRoundsTests
{
    /// <summary>Every name ESPN is known to send, what it is in English and in German.</summary>
    public static TheoryData<string, string, string> Known => new()
    {
        { "Final", "Final", "Finale" },
        { "Semifinals", "Semifinals", "Halbfinale" },
        { "Semi-finals", "Semifinals", "Halbfinale" },
        { "Quarterfinals", "Quarterfinals", "Viertelfinale" },
        { "Quarter-finals", "Quarterfinals", "Viertelfinale" },
        { "Round of 16", "Round of 16", "Achtelfinale" },
        { "Rd of 16", "Round of 16", "Achtelfinale" },
        { "Round of 32", "Round of 32", "Sechzehntelfinale" },
        { "Round of 64", "Round of 64", "Runde der letzten 64" },
        { "3rd-Place Match", "Third-place match", "Spiel um Platz 3" },
        { "3rd-Place", "Third-place match", "Spiel um Platz 3" },
        { "Third Place", "Third-place match", "Spiel um Platz 3" },
        { "Group F", "Group F", "Gruppe F" },
        { "Group L", "Group L", "Gruppe L" },
        { "Group", "Group stage", "Gruppenphase" },
        { "Group Stage", "Group stage", "Gruppenphase" },
        { "League Phase", "League phase", "Ligaphase" },
        { "Knockout Round Playoffs", "Knockout playoffs", "K.-o.-Phasen-Play-offs" },
        { "Knockout phase playoffs", "Knockout playoffs", "K.-o.-Phasen-Play-offs" },
        { "Playoff Round", "Playoff round", "Play-off-Runde" },
        { "Play-off round", "Playoff round", "Play-off-Runde" },
        { "Playoffs", "Playoffs", "Play-offs" },
        { "Preliminary Round", "Preliminary round", "Vorrunde" },
        { "Qualifying First Round", "Qualifying round 1", "Qualifikation, 1. Runde" },
        { "Qualifying Third Round", "Qualifying round 3", "Qualifikation, 3. Runde" },
        { "Second Round", "Round 2", "2. Runde" },
        { "2nd Round", "Round 2", "2. Runde" },
        { "5th Round", "Round 5", "5. Runde" },
        { "Matchday 5", "Matchday 5", "5. Spieltag" },
        { "Matchday 13", "Matchday 13", "13. Spieltag" },
        { "Championship Round", "Championship round", "Meisterrunde" },
        { "Relegation Round", "Relegation round", "Abstiegsrunde" },
        { "Promotion/Relegation Playoffs", "Relegation playoff", "Relegation" },
        { "Relegation playoff", "Relegation playoff", "Relegation" },
        { "Regular Season", "Regular season", "Hauptrunde" },
        { "Postseason", "Postseason", "Nachsaison" },
        { "Preseason", "Preseason", "Saisonvorbereitung" },
        { "1st Leg", "1st leg", "Hinspiel" },
        { "2nd Leg", "2nd leg", "Rückspiel" },
    };

    [Theory]
    [MemberData(nameof(Known))]
    public void EachKnownRoundIsSaidInKarposWords(string espn, string english, string german)
    {
        Assert.Equal(english, SportsRounds.Name(espn));
        using (InLanguage.Use("de"))
        {
            Assert.Equal(german, SportsRounds.Name(espn));
        }
    }

    [Theory]
    // A name Karpo does not know, an old World Cup's stage, the season's own name, a competition's name, nothing at all.
    [InlineData("Second Phase")]
    [InlineData("1950 Group Stage")]
    [InlineData("2025-26 Portuguese Primeira Liga")]
    [InlineData("Eastern Conference")]
    [InlineData("Group of Death")]
    [InlineData("Round of sixteen")]
    [InlineData("Matchday")]
    [InlineData("")]
    public void AnUnknownNameIsShownAsEspnSentIt(string espn)
    {
        Assert.Equal(espn, SportsRounds.Name(espn));
        using (InLanguage.Use("de"))
        {
            Assert.Equal(espn, SportsRounds.Name(espn));
        }
    }

    [Fact]
    public void ItsLetterAndNumberGoWhereTheLanguagePutsThem()
    {
        using (InLanguage.Use("ko"))
        {
            Assert.Equal("F조", SportsRounds.Name("Group F"));
            Assert.Equal("64강", SportsRounds.Name("Round of 64"));
        }
        using (InLanguage.Use("ja"))
        {
            Assert.Equal("第5節", SportsRounds.Name("Matchday 5"));
        }
        using (InLanguage.Use("tr"))
        {
            Assert.Equal("B Grubu", SportsRounds.Name("group b"));
        }
    }

    [Fact]
    public void EveryRoundTheSavedAnswersHoldIsKarposOwn()
    {
        // Every round, stage and group in the answers saved on 2026-11-02: the World Cup's knockout and its group day and
        // groups, the Champions League's two legs, final, league phase and table, Sporting's cups, the cups' calendars.
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var matches in new[]
        {
            SavedSports.WorldCupKnockout(), SavedSports.WorldCupGroupDay().Matches, SavedSports.WorldCupAnswer().Matches,
            SportsAnswers.Matches(SavedSports.Answer("ucl-march")), SportsAnswers.Matches(SavedSports.Answer("ucl-final")),
            SportsAnswers.Matches(SavedSports.Answer("ucl-now")), SavedSports.SportingAnswer().Matches,
        })
        {
            names.UnionWith(matches.Select(match => match.Round).Where(round => round.Length > 0));
        }
        names.UnionWith(SavedSports.WorldCupAnswer().Table.Select(group => group.Name));
        names.UnionWith(SportsAnswers.Stages(SavedSports.Answer("wc-now")).Select(stage => stage.Label));
        names.UnionWith(SportsAnswers.Stages(SavedSports.Answer("ucl-now")).Select(stage => stage.Label));
        Assert.Contains("Round of 32", names);
        Assert.Contains("3rd-Place Match", names);
        Assert.Contains("League Phase", names);
        Assert.Contains("Group F", names);
        using (InLanguage.Use("de"))
        {
            Assert.All(names, name => Assert.NotEqual(name, SportsRounds.Name(name)));
        }
    }

    [Fact]
    public void TheCaptionAndTheTooltipSayTheRoundInTheLanguage()
    {
        var march = SportsAnswers.Matches(SavedSports.Answer("ucl-march"));
        var second = march.Single(match => match.Home.Abbreviation == "SCP" && match.Away.Abbreviation == "BODO");
        var germany = SavedSports.WorldCupKnockout().Single(match => match.Home.Abbreviation == "GER" && match.Away.Abbreviation == "PAR");
        using (InLanguage.Use("de"))
        {
            Assert.Equal($"Achtelfinale · Rückspiel · {S.DockSportsAdvanceOnAggregate("Sporting", "5–3")}", SportsText.Detail(second, homeFirst: true, round: true));
            Assert.StartsWith("Sechzehntelfinale · ", SportsText.Detail(germany, homeFirst: true, round: true), StringComparison.Ordinal);
            Assert.Equal("UEFA Champions League · Achtelfinale", SportsTips.Match(second, homeFirst: true, SportsClock.Minutes, DateTime.Now).Split('\n')[1]);
        }
        // Team and competition names stay as ESPN sends them.
        Assert.Equal("UEFA Champions League · Round of 16", SportsTips.Match(second, homeFirst: true, SportsClock.Minutes, DateTime.Now).Split('\n')[1]);
    }
}
