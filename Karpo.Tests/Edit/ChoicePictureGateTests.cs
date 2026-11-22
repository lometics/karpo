// edit, §681 (the person, 2026-11-12: "the palettes are not shown with previews anymore. why?"): the Appearance's Palette was
// never pictured on the page — its choices each a swatch tile, and more than eight of them, the gate turned it away. The
// pictures sheet drew them only because it handed its own pictures in. The gate is asked here with the page's own choices.

using Karpo.Dock;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class ChoicePictureGateTests
{
    private static List<FlyoutItem> All(IEnumerable<FlyoutItem> items) =>
        [.. items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. All(item.Children ?? [])])];

    /// <summary>Palette's choices as the Appearance page has them pass the gate, swatches and how many soever.</summary>
    [Fact]
    public void TheAppearancesPaletteIsPictured()
    {
        const string key = LookRows.PaletteRow;
        var config = MenuCorpus.Fresh();
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        var items = TheDock.LooksItems(config, bar, dark: true, [], LooksScope.Appearance);
        var row = All(items).First(item => item.Setting == key && item.Children is { Count: > 0 });
        var choices = (row.Children ?? []).Where(PanelPickers.Choice).ToList();
        // What turned it away: a swatch each, more than the most a picker pictures.
        Assert.True(choices.Count > TheDock.PicturedMost);
        Assert.Contains(choices, kid => kid.Icon is FlyoutTile);
        Assert.True(TheDock.Pictures(key, choices.Count, choices.Any(kid => kid.Icon is FlyoutTile)));
    }

    /// <summary>Any other row: a few choices, none a tile of its own.</summary>
    [Fact]
    public void OtherRowsKeepTheirTilesOrTheirList()
    {
        Assert.True(TheDock.Pictures(LookRows.ModeRow, 3, tiled: false));
        Assert.True(TheDock.Pictures(BarLooks.ShowRow, 3, tiled: false));
        Assert.False(TheDock.Pictures(BarLooks.ShowRow, TheDock.PicturedMost + 1, tiled: false));
        Assert.False(TheDock.Pictures("Appearance.widgets.pattern", 4, tiled: true));
    }
}
