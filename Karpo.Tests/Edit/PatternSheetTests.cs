// The continuity rule (docs\widgets.md §462; the person, 2026-10-31: "the pattern ... should be continuous between widgets
// inside the same group but not between groups of widgets"), held on the real trees: each card shows the piece of its
// group's one sheet it stands on — the sheet's corner, where the card stands less where its layer says it stands on the
// sheet, is its section's corner in a folder —, so two neighbours of a group run on into each other and the next group
// starts its own; the bar's own pattern is one sheet from the bar's corner. §515: on the bar there are no groups, every
// widget's card a sheet of its own from its own corner.
//
// Needs a compositor and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Where the sheet a layer shows stands, its corner: where the layer stands, less where it says it stands on the sheet.</summary>
    private static Vector2 SheetCorner(Vector3 offset, PatternLayer layer) =>
        new Vector2(offset.X, offset.Y) - (layer.Showing ?? throw new Xunit.Sdk.XunitException("the layer shows no pattern")).At;

    /// <summary>
    /// §515: on the bar every widget's card is a sheet of its own, from its own corner — two side by side as much as one in the
    /// next section. The bar's pattern one sheet from the bar's corner.
    /// </summary>
    [UiFact]
    public void EveryCardOnTheBarIsASheetOfItsOwn()
    {
        var config = new Config();
        WearingPattern(config, LookPalettes.GraphiteId, Pattern.Dots, [LookPart.Taskbar, .. CardParts]);
        var look = (Dressed with { Dark = false }).Wearing(config);
        Assert.NotNull(look.Colors.CardPattern);
        List<PartSpec> specs = [Spec("a", BarKind.NightLight), Spec("b", BarKind.EnergySaver), Spec("c", BarKind.NightLight) with { Section = BarSection.End }];
        OnABar(48, specs, (layout, scene) =>
        {
            Vector2 Corner(int part) => new(layout.CardRect(part).X, layout.CardRect(part).Y);
            Vector2 Sheet(string id)
            {
                var key = $"card:{Config.IslandKey(null, id)}";
                var layer = scene.PatternOf(key) ?? throw new Xunit.Sdk.XunitException($"{id} has no pattern");
                Assert.Equal(look.Colors.CardPattern, layer.Showing?.Pattern);
                return SheetCorner(scene.Registered(key)!.Offset, layer);
            }
            // a, b beside it and c in the next section: each its own sheet, from its own corner.
            Assert.Equal(Corner(0), Sheet("a"));
            Assert.Equal(Corner(1), Sheet("b"));
            Assert.Equal(Corner(2), Sheet("c"));
            Assert.NotEqual(Sheet("a"), Sheet("b"));
            // The bar's own: one sheet from its corner.
            var bar = scene.PatternOf("panel") ?? throw new Xunit.Sdk.XunitException("the bar has no pattern");
            Assert.Equal((look.Colors.TaskbarPattern!.Value, Vector2.Zero), bar.Showing);
        }, look: look, editing: false);
    }

    /// <summary>
    /// In a folder: each section — a column, or the part of one a separator of Space only splits off — is one sheet from its
    /// corner, every line of it a window onto it; three sections, three sheets.
    /// </summary>
    [UiFact]
    public void ASectionOfAFolderIsOneSheet()
    {
        WindowShots.OnHiddenDesktop("pattern-sheets", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.5f, 2.25f })
            {
                var config = new Config { Height = 48 };
                WearingPattern(config, LookPalettes.GraphiteId, Pattern.Contours, CardParts);
                var folder = BarKind.FolderList.New();
                folder.Id = "sheets";
                TwoColumns(folder.Folder!);
                config.DefaultLayout.Islands = [folder];
                var look = (TestLooks.LookFor(dark: true) with { Animations = false }).Wearing(config);
                using var open = new FolderIsland("layout:default/sheets", folder, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, look, scale, new POINT { x = 700, y = 1200 }, new RECT { right = 2400, bottom = 1200 }, Edge.Bottom, open);
                var plan = flyout.Plan!;
                var lines = flyout.LinePatterns;
                var sheets = new Dictionary<Vector2, int>();
                for (var index = 0; index < lines.Count && index < plan.Cards.Count; index++)
                {
                    if (plan.RunOf(index) is not { } section)
                    {
                        continue;
                    }
                    var (offset, layer) = lines[index];
                    Assert.Equal(look.Flyout().Colors.CardPattern, layer.Showing?.Pattern);
                    var corner = SheetCorner(offset, layer);
                    Assert.Equal(new Vector2(section.left, section.top), corner);
                    sheets[corner] = sheets.GetValueOrDefault(corner) + 1;
                }
                // The second column's three lines, the first's two over its separator and the one under it.
                Assert.Equal([3, 2, 1], sheets.Values.OrderDescending());
                flyout.Hide();
            }
        });
    }
}
