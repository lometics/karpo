// §394, §520: the folded edit bar — Settings ⌄, its errors' indicator before "Add" (none, three, more than 99), Add and
// Done; §630 no grip — one over the other. At 150 %, dark, light and high contrast, to KARPO_PNG_DIR as
// editbar-errors-<theme>-150.png. The real dock's, with the list open under the indicator: ArrangedFolderStandsTests
// (film-editbar-errors.png).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void EditBarErrorsSheets()
    {
        WindowShots.OnHiddenDesktop("editbar-errors", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var bounds = new RectI(0, 0, 2880, 1620);
            var plain = PictureState();
            var states = new[] { plain, plain with { Errors = 3 }, plain with { Errors = 120 } };
            foreach (var (theme, look) in new[]
            {
                ("dark", AddListPictures.LookFor(true) with { Animations = false }),
                ("light", AddListPictures.LookFor(false) with { Animations = false }),
                ("contrast", EditDeckPlanTests.HighContrast),
            })
            {
                var backdrop = theme == "light" ? ((byte)243, (byte)243, (byte)243) : ((byte)0, (byte)0, (byte)0);
                var decks = new List<EditDeck>();
                try
                {
                    for (var at = 0; at < states.Length; at++)
                    {
                        var deck = new EditDeck(gfx, instance, 0);
                        decks.Add(deck);
                        deck.Show(gfx, look, scale, new EditDeckRoom(bounds with { Top = bounds.Top + (100 * at), Bottom = bounds.Bottom + (100 * at) }, 0), states[at]);
                    }
                    // The errors say how many in words.
                    Assert.Equal(S.DockEditErrors(3), decks[1].Layout!.Header.Single(part => part.Kind == PanelHitKind.Errors).Words);
                    Assert.DoesNotContain(decks[0].Layout!.Header, part => part.Kind == PanelHitKind.Errors);
                    WindowShots.Settle(200);
                    Assert.True(WindowShots.SaveTogether([.. decks.Select(deck => (deck.Hwnd, deck.PanelOnScreen))], $"editbar-errors-{theme}-150.png", backdrop));
                }
                finally
                {
                    decks.ForEach(deck => deck.Dispose());
                }
            }
        });
    }
}
