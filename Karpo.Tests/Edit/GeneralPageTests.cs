// edit, §614 (the person, 2026-11-09: "the edit bar should not show karpo as entry but General as entry"): the edit bar's
// first entry, its page's title and every link that leads there say "General", Windows Settings' word, in every language;
// the page's section of Karpo Pro and Back up and reset is still "Karpo", and the gear stays its glyph.

using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class GeneralPageTests
{
    [Fact]
    public void TheFirstEntryAndItsPageAreGeneral()
    {
        var content = SettingsPanelTests.Content();
        var page = content.Of(PanelPage.Karpo)!;
        Assert.Equal((S.SettingsGeneralHeader, S.SettingsGeneralHeader, OptionGlyphs.Settings), (page.Side, page.Title, page.Glyph));
        Assert.Equal("General", S.SettingsGeneralHeader);
        // Karpo's own section on it keeps its name.
        Assert.Contains(KarpoMenu.Rows(new Config(), Karpo.Tests.Menus.MenuCorpus.Services()), row => row.Kind == OptionRowKind.Heading && row.Title == S.SettingsSectionsKarpo);
        Assert.Equal("Karpo", S.SettingsSectionsKarpo);
    }

    /// <summary>A widget's link to the public holidays says it leads to General: "General › Public holidays", and so does a screen reader.</summary>
    [Fact]
    public void ALinkThereSaysGeneral()
    {
        Assert.Equal(S.DockMenuLinkOpens(S.CoreHolidaysName, S.SettingsGeneralHeader), OptionLinks.Opens(OptionLink.Holidays));
        Assert.Equal(S.SettingsGeneralHeader + PanelRows.Separator + S.CoreHolidaysName,
            PanelRows.LeadOf(OptionMenu.LinkItem(OptionLinks.Row(OptionLink.Holidays, OptionSlot.Subject), 1)));
    }
}
