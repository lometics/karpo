// sports §483: a round's or a stage's name in Karpo's own words. ESPN names them in English ("Quarterfinals",
// "Round of 16", "Group F", "3rd-Place Match", "League Phase", "Matchday 5"); the ones it is known to send — every
// one in the answers saved on 2026-11-02 (Karpo.Tests\Sports\Saved and the captures they were cut from) and the
// stages its competitions are documented with — are said in the language now, a group's letter and a round's number
// kept; one it is not known to send is shown as ESPN sends it. One function, read by the line (a day's and a round's
// headings, a table's group, a match's caption), its tooltips and the face's: SportsText.Detail, SportsTips.Match.
// Team and competition names are never translated: they are names.

using System.Globalization;

namespace Karpo.Dock.Sports;

/// <summary>§483: a round's or a stage's name, ESPN's English said in Karpo's words.</summary>
internal static class SportsRounds
{
    /// <summary>
    /// <paramref name="espn"/> in the language now — "Viertelfinale" for "Quarterfinals", "Gruppe F" for "Group F",
    /// "13. Spieltag" for "Matchday 13" in German —, or as ESPN wrote it where it is none Karpo knows; empty for empty.
    /// </summary>
    public static string Name(string espn)
    {
        var words = Plain(espn);
        if (words.Length == 0)
        {
            return espn;
        }
        return words switch
        {
            "final" or "finals" => S.DockSportsRoundFinal,
            "semifinals" or "semifinal" => S.DockSportsRoundSemifinals,
            "quarterfinals" or "quarterfinal" => S.DockSportsRoundQuarterfinals,
            "3rd place match" or "3rd place" or "third place" or "third place match" or "3rd place playoff" or "third place playoff" => S.DockSportsRoundThirdPlace,
            "group" or "groups" or "group stage" or "group phase" => S.DockSportsRoundGroupStage,
            "league phase" or "league stage" => S.DockSportsRoundLeaguePhase,
            "knockout round playoffs" or "knockout phase playoffs" or "knockout playoffs" or "knockout round playoff" => S.DockSportsRoundKnockoutPlayoffs,
            "playoff round" => S.DockSportsRoundPlayoffRound,
            "playoffs" or "playoff" => S.DockSportsRoundPlayoffs,
            "preliminary round" => S.DockSportsRoundPreliminary,
            "championship round" => S.DockSportsRoundChampionship,
            "relegation round" => S.DockSportsRoundRelegation,
            "relegation playoff" or "relegation playoffs" or "promotion relegation playoffs" or "promotion relegation playoff" => S.DockSportsRoundRelegationPlayoff,
            "regular season" => S.DockSportsRoundRegularSeason,
            "postseason" => S.DockSportsRoundPostseason,
            "preseason" => S.DockSportsRoundPreseason,
            "1st leg" or "first leg" => S.DockSportsFirstLeg,
            "2nd leg" or "second leg" => S.DockSportsSecondLeg,
            _ => Numbered(words, espn) ?? espn,
        };
    }

    /// <summary>A name of a group, a matchday or a numbered round: "Group F", "Round of 64", "Matchday 5", "Second Round"; null for none.</summary>
    private static string? Numbered(string words, string espn)
    {
        if (After(words, "group ") is { Length: >= 1 and <= 2 } letter && letter.All(char.IsAsciiLetterOrDigit))
        {
            // The letter as ESPN wrote it, "F".
            return S.DockSportsRoundGroup(espn.Trim()[^letter.Length..].ToUpperInvariant());
        }
        if (Count(After(words, "round of ")) is { } teams)
        {
            return teams switch
            {
                16 => S.DockSportsRoundOf16,
                32 => S.DockSportsRoundOf32,
                _ => S.DockSportsRoundOf(teams),
            };
        }
        if (Count(After(words, "matchday ")) is { } day)
        {
            return S.DockSportsRoundMatchday(day);
        }
        if (Count(After(words, "round ")) is { } plain)
        {
            return S.DockSportsRoundNumbered(plain);
        }
        if (words.EndsWith(" round", StringComparison.Ordinal))
        {
            var before = words[..^" round".Length];
            if (After(before, "qualifying ") is { } qualifying && Ordinal(qualifying) is { } nth)
            {
                return S.DockSportsRoundQualifying(nth);
            }
            if (Ordinal(before) is { } number)
            {
                return S.DockSportsRoundNumbered(number);
            }
        }
        if (After(words, "qualifying round ") is { } numbered && Count(numbered) is { } qualifier)
        {
            return S.DockSportsRoundQualifying(qualifier);
        }
        return null;
    }

    /// <summary>What follows <paramref name="start"/> in <paramref name="words"/>; null where they do not begin with it.</summary>
    private static string? After(string words, string start) =>
        words.StartsWith(start, StringComparison.Ordinal) ? words[start.Length..] : null;

    /// <summary>A count written in digits, 1 to 999; null for anything else.</summary>
    private static int? Count(string? digits) =>
        digits is { Length: >= 1 and <= 3 } && digits.All(char.IsAsciiDigit) && int.Parse(digits, CultureInfo.InvariantCulture) is var count and > 0 ? count : null;

    /// <summary>"second", "2nd": 2; null for no ordinal.</summary>
    private static int? Ordinal(string word) => word switch
    {
        "first" => 1,
        "second" => 2,
        "third" => 3,
        "fourth" => 4,
        "fifth" => 5,
        "sixth" => 6,
        "seventh" => 7,
        "eighth" => 8,
        "ninth" => 9,
        "tenth" => 10,
        _ when word.Length > 2 && (word.EndsWith("st", StringComparison.Ordinal) || word.EndsWith("nd", StringComparison.Ordinal)
            || word.EndsWith("rd", StringComparison.Ordinal) || word.EndsWith("th", StringComparison.Ordinal)) => Count(word[..^2]),
        _ => null,
    };

    /// <summary>
    /// ESPN's name as it is matched: lower case, its dashes and slashes spaces, its spaces single, "play-off" one word
    /// and "Rd of" a round's, "match day" one — "3rd-Place Match" is "3rd place match", "Semi-finals" "semifinals".
    /// </summary>
    private static string Plain(string espn)
    {
        var words = string.Join(' ', (espn ?? "").ToLowerInvariant().Replace('-', ' ').Replace('/', ' ').Split(' ', StringSplitOptions.RemoveEmptyEntries));
        words = words.Replace("play off", "playoff", StringComparison.Ordinal).Replace("semi final", "semifinal", StringComparison.Ordinal)
            .Replace("quarter final", "quarterfinal", StringComparison.Ordinal).Replace("match day", "matchday", StringComparison.Ordinal)
            .Replace("post season", "postseason", StringComparison.Ordinal).Replace("pre season", "preseason", StringComparison.Ordinal);
        return words.StartsWith("rd of ", StringComparison.Ordinal) ? "round of " + words["rd of ".Length..] : words;
    }
}
