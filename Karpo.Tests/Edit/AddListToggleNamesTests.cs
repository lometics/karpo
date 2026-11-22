// A toggle's name in the add list (WidgetInfo.AddTitle). The person, 2026-10-11: "the airplane mode
// is also an airplane mode on / off switch, so it should be properly named. all the toggle switches
// should be named the same in that regard", and then: "dont actually change the text of the widget to
// Microphone on/off, it should just show up like that in the add widgets lists". So the add list says
// "<thing> on/off" for every toggle, in every language, and everything else says the thing.

using Karpo.Dock;
using Karpo.Tests.Localization;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

[Collection(InLanguage.Collection)]
public sealed class AddListToggleNamesTests
{
    /// <summary>What each language says after a toggle's name in the add list.</summary>
    public static TheoryData<string, string> OnOff => new()
    {
        { "en", " on/off" },
        { "de", " ein/aus" },
        { "fr", " activé/désactivé" },
        { "es", " activado/desactivado" },
        { "zh-Hans", " 开/关" },
    };

    private static WidgetInfo[] Flipping => [.. WidgetInfos.All.Where(info => info.Flips)];

    /// <summary>
    /// Every toggle's add-list name is its title and the language's "on/off", and its title — what its
    /// line, its tooltip and its page say — is the thing's name alone; nothing else is renamed.
    /// </summary>
    [Theory]
    [MemberData(nameof(OnOff))]
    public void EveryToggleIsOnOffInTheAddListAlone(string language, string onOff)
    {
        using var speaking = InLanguage.Use(language);
        Assert.NotEmpty(Flipping);
        foreach (var info in Flipping)
        {
            Assert.Equal(info.Title + onOff, info.AddTitle);
            Assert.DoesNotContain(onOff.Trim(), info.Title, StringComparison.Ordinal);
        }
        Assert.All(WidgetInfos.All.Where(info => !info.Flips), info => Assert.Equal(info.Title, info.AddTitle));

        // The add list's rows say it, wherever the tree shows them; the search finds both names.
        var (items, entries, _) = AddList.Rows(WidgetType.List, turns: true, TheDock.CMD_ADD_WIDGET);
        foreach (var entry in entries.Where(entry => entry.Premade is null))
        {
            var info = WidgetInfos.Find(entry.Id)!;
            Assert.All(AddList.Picks(items).Where(pick => pick.Id == TheDock.CMD_ADD_WIDGET + (uint)entries.IndexOf(entry)),
                // A widget with styles is named as the widget (§209), or as its reading (§283).
                pick => Assert.Equal(WidgetStyles.Of(entry.Id) is { } styled ? styled.Widget.TitleOf(styled.Style) : info.AddTitle, pick.Text));
        }
        foreach (var info in Flipping.Where(info => entries.Any(entry => entry.Id == info.Id && entry.Premade is null)))
        {
            Assert.Contains(AddList.Results(items, info.Title), result => result.Text == info.AddTitle);
            Assert.Contains(AddList.Results(items, info.AddTitle), result => result.Text == info.AddTitle);
        }
    }

    /// <summary>In another language a toggle is found by its English add-list name too.</summary>
    [Fact]
    public void AToggleIsFoundByItsEnglishNameElsewhere()
    {
        using var speaking = InLanguage.Use("de");
        var (items, _, _) = AddList.Rows(WidgetType.List, turns: true, TheDock.CMD_ADD_WIDGET);
        Assert.Contains(AddList.Results(items, "microphone on/off"), result => result.Text == "Mikrofon ein/aus");
    }
}
