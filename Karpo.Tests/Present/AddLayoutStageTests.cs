// §628 (the person, 2026-11-09: "when adding a layout it should not open a dropdown but should open a new page where you also
// have a preview of the different layout presets. there should also be one that starts just blank"): Add a layout's page as
// the real dock shows it — Karpo.exe --stage on a desktop of its own, its config a file of its own (PanelPopupPlaceTests.Drive),
// driven as the mouse and the keys drive it:
//
//  - Displays and layouts' "Add a layout" opens the page in the Layouts entry's flyout, no list: its way back, a tile for
//    each preset, Blank first; the way back (a click, then Backspace) the Layouts page again, nothing made.
//  - A tile clicked makes its layout, written to the file once, and the flyout is the Layouts page again, at the new
//    layout, its expander open (its Name's box in sight); Ctrl+Z takes it out, one step.
//
// Needs a window station: KARPO_UI_TESTS=1.

using System.Text.Json.Nodes;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;

namespace Karpo.Tests.Present;

public sealed class AddLayoutStageTests(ITestOutputHelper output)
{
    /// <summary>The layouts the dock's file holds, by their names.</summary>
    private static List<string> Layouts(PanelPopupPlaceTests.Probe probe) =>
        [.. JsonNode.Parse(probe.ConfigText())!["layouts"]!.AsArray().Select(layout => (string)layout!["name"]!)];

    /// <summary>The panel's thing of <paramref name="part"/> whose key ends in <paramref name="words"/>, in sight; null for none.</summary>
    private static PanelPopupPlaceTests.Spot? Named(PanelPopupPlaceTests.Probe probe, PanelHitKind part, string words) =>
        probe.Read("stage-edit.json").FirstOrDefault(spot => spot.Hit == "Deck" && spot.Key.StartsWith(part + ":", StringComparison.Ordinal) && spot.Key.EndsWith("/" + words, StringComparison.Ordinal));

    [UiFact]
    public void AddALayoutOpensItsPageBackReturnsATilesClickMakesItsLayout() => PanelPopupPlaceTests.Drive(output, Edge.Bottom, null, probe =>
    {
        probe.Open();
        probe.Nav(PanelPage.Layouts);
        var before = Layouts(probe);
        void OpenThePage()
        {
            var link = Named(probe, PanelHitKind.Link, S.SettingsLayoutsAdd) ?? throw new InvalidOperationException("no Add a layout");
            probe.Click(link.Center);
            Assert.Equal(nameof(PanelPage.AddLayout), probe.PageShown());
            // No list of it: a page, its way back, a tile for each preset, Blank first.
            Assert.Empty(probe.Menus());
            _ = probe.Spot(PanelHitKind.Back);
            var tiles = probe.Read("stage-edit.json").Where(spot => spot.Hit == "Deck" && spot.Key.StartsWith(PanelHitKind.Act + ":" + nameof(PanelPage.AddLayout) + "/", StringComparison.Ordinal)).ToList();
            Assert.Equal(LayoutPresets.All[0].Title, tiles.OrderBy(spot => spot.Rect.top).First().Key.Split('/')[^1]);
            Assert.Contains(tiles, spot => spot.Key.EndsWith("/" + LayoutPresets.Windows.Title, StringComparison.Ordinal));
        }

        OpenThePage();
        probe.Click(probe.Spot(PanelHitKind.Back).Center);
        Assert.Equal(nameof(PanelPage.Layouts), probe.PageShown());
        Assert.Equal(before, Layouts(probe));

        // Backspace goes back too.
        OpenThePage();
        probe.Key(probe.Deck(), VK.VK_BACK);
        probe.Pump(900);
        Assert.Equal(nameof(PanelPage.Layouts), probe.PageShown());
        Assert.Equal(before, Layouts(probe));

        // A tile: its layout made, the Layouts page at it, its expander open.
        OpenThePage();
        var blank = Named(probe, PanelHitKind.Act, LayoutPresets.Blank.Title) ?? throw new InvalidOperationException("no Blank tile");
        probe.Click(blank.Center);
        Assert.Equal([.. before, LayoutPresets.Blank.Title], Layouts(probe));
        var made = JsonNode.Parse(probe.ConfigText())!["layouts"]!.AsArray()[^1]!;
        Assert.Empty(made["islands"]?.AsArray() ?? []);
        Assert.Equal(nameof(PanelPage.Layouts), probe.PageShown());
        Assert.NotNull(Named(probe, PanelHitKind.Text, LayoutPresets.Blank.Title + "/" + S.SettingsLayoutsNameHeader));

        // One step of the undo.
        probe.Undo();
        Assert.Equal(before, Layouts(probe));
    });
}
