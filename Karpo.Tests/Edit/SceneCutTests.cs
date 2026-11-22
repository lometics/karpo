// §476: the bar's cut (Scene.Cut, §464) let go of. Karpo crashed in dcomp.dll (an access violation at +0x40182, read
// of 0x30; 2026-11-01 12:06 and 11-02 10:33, after 15 and 11 hours): a CompositionPathGeometry whose Path is set to
// null after it had one crashes the process, and Scene.Cut did so whenever a bar went from cards to none — a setting
// picked in a menu rebuilding the bars without a card (the dump: OptionPicked > Apply > Arrange > Rebuild >
// DockWindow.Lay > Scene.Update > DressCards > Cut > set_Path).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// A bar of cards that loses them all, and gets them back: its background whole, then cut again, the process alive.
    /// §512: every widget wears its card on every kind of bar, so a bar loses its cards by its look (its widgets'
    /// background unset), not by being made floating.
    /// </summary>
    [UiFact]
    public void ABarThatLosesItsCardsIsDrawnWholeAndLives()
    {
        PartSpec[] specs = [Spec("w", BarKind.WidgetsBoard), Spec("c", BarKind.NightLight) with { Section = BarSection.End }];
        // A see-through card, as the user's style has: a surface of its own, cut out of the bar.
        var carded = Dressed with { Colors = new Palette(Card: new Argb(0x40, 0xFF, 0xFF, 0xFF)) };
        OnABar(48, specs, (layout, scene) =>
        {
            Assert.True(scene.Cuts, "the cards are cut out of the bar");
            for (var again = 0; again < 3; again++)
            {
                scene.Update(layout, new Content(), Dressed, new Icons(), shown: true);
                Assert.False(scene.Cuts, "no card, no cut: the bar drawn whole");
                scene.Update(layout, new Content(), carded, new Icons(), shown: true);
                Assert.True(scene.Cuts, "the cards back, cut out again");
            }
        }, look: carded, editing: false, style: BarStyle.Islands);
    }
}
