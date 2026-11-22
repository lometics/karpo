// §621 on the dock's real scene: the add page's ghost of a pick of several cards — the open apps, three buttons — is an outline
// round each button where it stands, not one round them all; a single widget's its block's one; the empty ghost's one
// rectangle turning into the pick's three and back to one.
//
// Needs a compositor: KARPO_UI_TESTS=1, as the other scene tree tests.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Start, the open apps (three buttons), a widget: all in the start section.</summary>
    private static List<PartSpec> AppsBetween() =>
    [
        Spec("s", BarKind.Start) with { Section = BarSection.Start },
        Spec("apps", BarKind.Apps) with { Section = BarSection.Start },
        Spec("n", BarKind.NightLight) with { Section = BarSection.Start },
    ];

    [UiFact]
    public void TheAddGhostOutlinesEachCardOfItsPick()
    {
        OnABar(48, AppsBetween(), (layout, scene) =>
        {
            // The empty ghost (§617): the widget's block, one outline.
            scene.Edit(new EditView(Ghost: 2, GhostEmpty: true));
            Assert.Equal([layout.BlockRect(2)], scene.GhostsShown);

            // The open apps under the pointer: an outline round each button, each its own card.
            scene.Edit(new EditView(Ghost: 1));
            Assert.Equal(3, scene.GhostsShown.Count);
            Assert.Equal(layout.Plates(1), scene.GhostsShown);
            Assert.Equal(layout.TileCell(0), scene.GhostsShown[0]);

            // A single widget again: its block's one outline, the others gone.
            scene.Edit(new EditView(Ghost: 2));
            Assert.Equal([layout.BlockRect(2)], scene.GhostsShown);
            Assert.Equal(layout.BlockRect(2), scene.GhostShown);

            scene.Edit(null);
            Assert.Empty(scene.GhostsShown);
            Assert.Null(scene.GhostShown);
        }, style: BarStyle.Islands, tiles: 3);
    }
}
