// A setting's description is held short, as Windows 11 Settings writes them: one sentence at most,
// with no full stop at its end, and about ten words, twelve at the very most. What explains an edge
// case belongs in the docs, not on the row. Checked against the words written in code, and against
// the words the add-widget list shows under each widget, which are descriptions too.

using System.Text.RegularExpressions;
using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class ShortDescriptionsTests
{
    /// <summary>The most words a description may have.</summary>
    private const int MostWords = 12;

    /// <summary>Why <paramref name="text"/> is too long for a description; null when it is short enough.</summary>
    private static string? Problem(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        if (words > MostWords)
        {
            return $"{words} words";
        }
        if (text.TrimEnd().EndsWith('.'))
        {
            return "ends with a full stop";
        }
        // A full stop inside, but not one that ends an abbreviation ("St. Gallen").
        return Regex.IsMatch(text, @"(?<!\b(?:St|Mr|Dr|vs))[.!?] \S") ? "more than one sentence" : null;
    }

    [Theory]
    [InlineData("Closes the dock and gives the taskbar back", null)]
    [InlineData("In pixels at 100% scale; Windows' taskbar is 48", null)]
    [InlineData("Outlook.com, Hotmail, work or school: calendars, contacts, mail", null)]
    [InlineData("Independent software studio in St. Gallen, Switzerland", null)]
    [InlineData("Turned off in Task Manager's Startup apps.", "ends with a full stop")]
    [InlineData("Optional. Where it is not the address", "more than one sentence")]
    [InlineData("One two three four five six seven eight nine ten eleven twelve thirteen", "13 words")]
    public void TheRuleReadsAsItSays(string text, string? problem) => Assert.Equal(problem, Problem(text));

    /// <summary>The descriptions the pages built in code take from here, in every state they have.</summary>
    [Fact]
    public void TheDescriptionsWrittenInCodeAreShortToo()
    {
        var hardware = new[]
        {
            new InputHardware(Pen: false, Touch: false, Slate: false),
            new InputHardware(Pen: true, Touch: true, Slate: true),
            new InputHardware(Pen: false, Touch: true, Slate: false),
        };
        string[] said =
        [
            // rss: the feed card's line under its box, in each of its states.
            S.SettingsRssAddressNotAddress, S.SettingsRssAddressNotAFeed, S.SettingsRssAddressUnreachable,
            S.SettingsRssAddressChecking("news.example.com"),
            .. from kind in InputButtons.Kinds
               from show in Enum.GetValues<InputButtonShow>()
               from here in hardware
               select InputButtons.Says(kind, show, here),
        ];

        Assert.All(said.Where(text => text.Length > 0), text => Assert.True(Problem(text) is null, $"\"{text}\" — {Problem(text)}"));
    }

    /// <summary>The add-widget list's line under each widget, preset and page.</summary>
    [Fact]
    public void TheAddListsBlurbsAreShortToo()
    {
        string[] blurbs =
        [
            .. WidgetInfos.All.Select(info => info.Blurb),
            .. Presets.All.Select(preset => preset.Blurb),
            .. Catalogue.Pages.Select(page => page.Blurb()),
        ];

        Assert.All(blurbs, blurb => Assert.True(Problem(blurb) is null, $"\"{blurb}\" — {Problem(blurb)}"));
    }
}
