// §512, §514: every widget wears its own card (Scene.PlaceCard) in its own rectangle (Layout.Plate), uncut and rounded all
// round, the widget's gap between two the bar's. Held on the dock's real tree, a card colour set for the cards to show.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void EveryWidgetWearsItsOwnCardInTheScene()
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Left })
        {
            var config = new Config { Height = 48, Style = BarStyle.Islands };
            var area = new TerraFX.Interop.Windows.RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
            var layout = Layout.ForTests(
                96,
                area,
                edge,
                config,
                [Spec("a", BarKind.WidgetsBoard), Spec("b", BarKind.NightLight), Spec("c", BarKind.EnergySaver), Spec("d", BarKind.TouchKeyboard) with { Section = BarSection.End }],
                Layout.CellPx(96, config));
            var vertical = layout.Vertical;
            var seen = 0;
            var look = LookFor(dark: true) with { Colors = new Palette(Card: new Argb(255, 0x44, 0x44, 0x44)) };

            OnItsOwnThread(gfx =>
            {
                var window = MakeWindow();
                try
                {
                    var target = Target(gfx, window);
                    using var scene = Scenes.Build(gfx, target, layout, new Content(), look, new Icons(), shown: true);
                    Check(scene, target.Root);
                }
                finally
                {
                    _ = DestroyWindow(window);
                }
            });
            Assert.Equal(1, seen);

            void Check(Scene scene, Visual root)
            {
                // The cards: the shapes the scene registered as each part's card, in the order of the bar.
                var cards = new[] { "a", "b", "c", "d" }.Select(id => Shown(root, Vector2.Zero)
                    .Single(item => item.Visual == scene.Registered($"card:{Config.IslandKey(null, id)}"))).ToList();
                Assert.Equal(4, cards.Count);
                foreach (var (card, part) in cards.Zip(new[] { 0, 1, 2, 3 }))
                {
                    // Uncut, its part's rectangle exactly.
                    Assert.Null(card.Visual.Clip);
                    var plate = layout.Plate(part);
                    Assert.Equal(new Vector2(plate.X, plate.Y), card.At);
                    Assert.Equal(plate.Size, card.Size);
                }
                // A widget's gap of the bar between two cards.
                for (var index = 1; index < 3; index++)
                {
                    var (before, after) = (cards[index - 1], cards[index]);
                    var gap = vertical ? after.At.Y - (before.At.Y + before.Size.Y) : after.At.X - (before.At.X + before.Size.X);
                    Assert.Equal(layout.WidgetGap, gap, 0.01f);
                }
                seen++;
            }
        }
    }
}
