// sports §483: the rounds in Karpo's words, drawn — the World Cup's knockout (its round of 32's shoot-outs) and a
// group day with its groups' table, in English and in German, dark at 150 %
// (sports-rounds-<worldcup-shootouts-knockout|worldcup-groups-matches|worldcup-groups-table>-<en|de>.png in
// KARPO_PNG_DIR). Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Sports;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Sports;

[Collection(InLanguage.Collection)]
public sealed class SportsRoundsPictures
{
    [UiFact]
    public void TheRoundsAreSaidInTheLanguage()
    {
        var cases = SportsCompetitionPictures.Cases;
        (SportsCompetitionPictures.Case Shown, SportsPage View)[] sheets =
        [
            (cases.Single(shown => shown.Name == "worldcup-shootouts"), SportsPage.Knockout),
            (cases.Single(shown => shown.Name == "worldcup-groups"), SportsPage.Matches),
            (cases.Single(shown => shown.Name == "worldcup-groups"), SportsPage.Table),
        ];
        foreach (var language in new[] { "en", "de" })
        {
            using var _ = InLanguage.Use(language);
            OnStaThread(gfx =>
            {
                foreach (var (shown, view) in sheets)
                {
                    SportsPictures.At(shown.At, () =>
                    {
                        using var line = SportsCompetitionPictures.Line(gfx, shown, shown.Answer(), SportsCompetitionPictures.KindOf(view));
                        SportsCompetitionPictures.Sheet(gfx, line, 1.5f, ListFormPictures.LookFor(true),
                            Path.Combine(Pictures, $"sports-rounds-{shown.Name}-{view.ToString().ToLowerInvariant()}-{language}.png"));
                        var said = line.Layout(FolderFlyout.ListWidth(1.5f), FolderFlyout.StyleFor(1.5f, ListFormPictures.LookFor(true), FlyoutTheme.For(ListFormPictures.LookFor(true))), line.Read())
                            .Rows.Select(row => $"{row.Text} {row.Detail}").ToList();
                        var expected = (view, language) switch
                        {
                            (SportsPage.Knockout, "de") => "Sechzehntelfinale",
                            (SportsPage.Knockout, _) => "Round of 32",
                            (SportsPage.Table, "de") => "Gruppe A",
                            (SportsPage.Table, _) => "Group A",
                            (_, "de") => "Gruppe",
                            _ => "Group",
                        };
                        Assert.Contains(said, words => words.Contains(expected, StringComparison.Ordinal));
                    });
                }
            });
        }
    }
}
