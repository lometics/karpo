// What the settings app wrote in English until the i18n audit (2026-10-12) speaks Karpo's language:
// the icon gallery's names and search, and the import summary's lines.


namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class SettingsWordsLanguageTests
{
    [Fact]
    public void TheIconGalleryIsNamedAndFoundInTheLanguageAndInEnglish()
    {
        using (InLanguage.Use("de"))
        {
            var folder = IconSearch.All.Single(choice => choice.Id == "folder");
            Assert.Equal("Ordner", folder.Name);
            // The German word finds it, and the English one still does.
            Assert.Contains(folder, IconSearch.Search("ordner"));
            Assert.Contains(folder, IconSearch.Search("folder"));
            Assert.Contains(IconSearch.All.Single(choice => choice.Id == "home"), IconSearch.Search("haus"));
        }
    }

    [Fact]
    public void TheImportSummaryIsInTheLanguage()
    {
        var current = Config.Parse("""{ "onlineUpdateMinutes": 15 }""");
        current.Repair();
        var imported = Config.Parse("""{ "onlineUpdateMinutes": 60, "layouts": [ { "id": "home", "name": "Home" } ] }""");
        imported.Repair();

        using (InLanguage.Use("fr"))
        {
            var changes = TransferSummary.Changes(current, imported);

            Assert.Contains("Intervalle de mise à jour : 15 → 60", changes);
            Assert.Contains("Dispositions : modifié", changes);
        }
    }
}
