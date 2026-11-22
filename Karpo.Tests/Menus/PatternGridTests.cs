// §634 (the person, 2026-11-09: "the pattern selector should actually not be a dropdown but a grid of the patterns where you
// can select the pattern you want."): a part's Pattern on the Appearance page opens a grid of tiles, None and every pattern,
// each a piece of the card it would be drawn on wearing it (its colour, its rim, the pattern in its ink), the one in force
// ringed; the closed control is a dropdown's box, the pattern's swatch beside its name. A tile picked writes the pattern and
// is tried on while it is rested on (§622: a Set pick of the Appearance). On a real popup (KARPO_UI_TESTS=1): a value's box
// wide, three by three, walked by the arrows, each tile told as it is rested on and named in its tooltip.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class PatternGridTests
{
    private const float Scale = 1.5f;

    private static readonly BarSpot Bar = new(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };

    private static readonly Pattern[] Choices = [Pattern.None, .. Patterns.All];

    /// <summary>The Appearance page's menu rows of <paramref name="config"/>, as the dock builds them, their picks into <paramref name="picks"/>.</summary>
    private static List<FlyoutItem> Items(Config config, List<OptionPick> picks) => TheDock.LooksItems(config, Bar, true, picks, LooksScope.Appearance);

    /// <summary>The row of <paramref name="part"/>'s Pattern among <paramref name="items"/>, at any depth.</summary>
    private static FlyoutItem PatternItem(IEnumerable<FlyoutItem> items, List<OptionPick> picks, LookPart part) =>
        Flat(items).Single(item => OptionMenu.PickOf(picks, item.Id) is { Kind: OptionPickKind.None } pick && pick.Key == LookRows.SectionKey(part) + ".pattern" && item.Children is { Count: > 0 });

    private static IEnumerable<FlyoutItem> Flat(IEnumerable<FlyoutItem> items) => items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. Flat(item.Children ?? [])]);

    /// <summary>
    /// Every part with a pattern: its row a dropdown (a Choice), the chosen pattern's swatch in its icon's place and its name at
    /// its end; its level None and every pattern, in that order, each a tile and nothing else — 60 DIP in a 72 DIP cell of its
    /// own, three to a line —, each the section's card wearing it in the pattern's ink (None slashed, no pattern), the one in
    /// force ticked, each named in its tooltip; three lines of three in the popup's 240 DIP.
    /// </summary>
    [Fact]
    public void ThePatternOpensAGridOfEveryPatternAndNone()
    {
        var config = MenuCorpus.Fresh();
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Widgets), Pattern.Waves));
        List<OptionPick> picks = [];
        var items = Items(config, picks);
        var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, "", items);
        var parts = LookParts.All.Where(LookParts.HasPattern).ToList();
        Assert.NotEmpty(parts);
        foreach (var part in parts)
        {
            var row = PatternItem(items, picks, part);
            var chosen = Looks.Of(config, dark: true)[part].Pattern;
            // The closed control: a dropdown, the chosen one's swatch beside its name.
            // §670: its picker inline, its tiles in its card.
            var panel = PanelRows.All(page.Rows).Single(each => each.Item.Id == row.Id && each.Kind != PanelRowKind.PickerBody);
            Assert.Equal(PanelRowKind.Picker, panel.Kind);
            var swatch = Assert.IsType<FlyoutTile>(row.Icon);
            Assert.False(swatch.Cell);
            Assert.Equal(chosen == Pattern.None ? null : chosen, swatch.Pattern?.Pattern);
            Assert.Equal(Patterns.Name(chosen), row.Value!.Text);

            // The grid: None and every pattern, tiles alone.
            var tiles = row.Children!;
            Assert.Equal(Choices.Select(Patterns.Name), tiles.Select(tile => tile.Text));
            Assert.All(tiles, tile => Assert.True(Flyout.IsTile(tile), tile.Text));
            Assert.All(tiles, tile => Assert.True(Flyout.TipsTile(tile), tile.Text));
            var drawn = Looks.Of(config, dark: true);
            for (var at = 0; at < Choices.Length; at++)
            {
                var tile = Assert.IsType<FlyoutTile>(tiles[at].Icon);
                var look = Looks.PatternSwatch(drawn, part, Choices[at], dark: true);
                Assert.Equal((MenuPickers.LookTileDip, MenuPickers.LookCellDip, MenuPickers.LookTilesAcross), (tile.SideDip, tile.CellDip, tile.Across));
                Assert.Equal((look.Fill, look.Rim, look.Pattern), (tile.Color!.Value, tile.Rim, tile.Pattern));
                Assert.Equal(Choices[at] == Pattern.None, tile.Slashed);
                Assert.Equal(Choices[at] == Pattern.None ? null : Choices[at], tile.Pattern?.Pattern);
                Assert.Equal(Choices[at] == chosen, tiles[at].Checked);
                Assert.Equal((OptionPickKind.Set, LookRows.SectionKey(part) + ".pattern", at), OptionMenu.PickOf(picks, tiles[at].Id) is { } pick ? (pick.Kind, pick.Key, pick.Value) : default);
            }
            // Three lines of three in the popup's own room (a value's box wide, less the rows' inset).
            var room = (PanelMetrics.ControlWide - (2 * 5)) * Scale;
            var spots = Flyout.TileSpots(tiles, room, 32 * Scale, Scale);
            Assert.Equal(Enumerable.Range(0, 9).Select(at => (at % 3, at / 3)), spots.Select(spot => (spot.Column, spot.Line)));
        }
        // Waves chosen on the cards: its tile ticked, its name in the box.
        Assert.Equal(Patterns.Name(Pattern.Waves), PatternItem(items, picks, LookPart.Widgets).Value!.Text);
    }

    /// <summary>
    /// A tile picked writes its pattern (None too), as the list did, one change; and it is one of the Appearance's choices tried
    /// on while it is rested on (§622: a Set pick, worn on a copy, the person's config untouched).
    /// </summary>
    [Fact]
    public void ATilePickedWritesThePatternAndIsTriedOn()
    {
        var config = MenuCorpus.Fresh();
        foreach (var part in LookParts.All.Where(LookParts.HasPattern))
        {
            foreach (var (pattern, at) in Choices.Select((pattern, at) => (pattern, at)).Reverse())
            {
                List<OptionPick> picks = [];
                var tile = PatternItem(Items(config, picks), picks, part).Children![at];
                var pick = OptionMenu.PickOf(picks, tile.Id)!;
                Assert.True(LookRows.TriedOn(pick.Key));
                // Tried on: a copy wears it, the person's config as it was.
                var before = Looks.Of(config, dark: true)[part].Pattern;
                var worn = config.Clone();
                var row = OptionRows.Find(BarLooks.Rows(worn, Bar, true, LooksScope.Appearance), pick.Key)!;
                _ = OptionMenu.ApplyRow(row, pick);
                Assert.Equal(pattern, Looks.Of(worn, dark: true)[part].Pattern);
                Assert.Equal(before, Looks.Of(config, dark: true)[part].Pattern);
                // Picked: written.
                row = OptionRows.Find(BarLooks.Rows(config, Bar, true, LooksScope.Appearance), pick.Key)!;
                _ = OptionMenu.ApplyRow(row, pick);
                Assert.Equal(pattern, Looks.Of(config, dark: true)[part].Pattern);
            }
        }
    }

    /// <summary>
    /// The grid on a real popup, hung from a value's box as the panel hangs it: a value's box wide (240 DIP), three tiles to a
    /// line in three lines, the one in force ringed. The arrows walk it — Right along a line, Down to the tile under, Left and
    /// Up back — and each tile the keys or the pointer rest on is told (the try-on's Hover) and named in its tooltip.
    /// </summary>
    [UiFact]
    public void TheGridIsWalkedByTheArrowsAndToldWhereItRests()
    {
        WindowShots.OnHiddenDesktop("pattern-grid", (gfx, instance) =>
        {
            var config = MenuCorpus.Fresh();
            Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Widgets), Pattern.Grid));
            List<OptionPick> picks = [];
            var tiles = PatternItem(Items(config, picks), picks, LookPart.Widgets).Children!.ToList();
            var work = new RECT { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };
            var box = new RECT { left = 600, top = 300, right = 600 + (int)(PanelMetrics.ControlWide * Scale), bottom = 300 + (int)(32 * Scale) };
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true, TellsHover = true };
            menu.Show(gfx, AddListPictures.LookFor(true), Scale, tiles, FlyoutLayout.List, new POINT { x = box.left, y = box.bottom }, work, Edge.Top, control: box);
            WindowShots.Settle();
            Assert.True(menu.IsOpen);
            Assert.Equal((int)MathF.Round(PanelMetrics.ControlWide * Scale), menu.PanelOnScreen.right - menu.PanelOnScreen.left);

            // Three to a line, three lines: the same top along a line, the same left down a column.
            var middles = tiles.Select(tile => menu.RowMiddle(tile.Id)!.Value).ToList();
            for (var at = 0; at < 9; at++)
            {
                Assert.Equal(middles[at / 3 * 3].y, middles[at].y);
                Assert.Equal(middles[at % 3].x, middles[at].x);
            }
            Assert.True(middles[1].x > middles[0].x && middles[3].y > middles[0].y);

            // The keys: onto the grid, then along and down.
            uint Key(int vk)
            {
                _ = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)vk, 0);
                return menu.HotId ?? 0;
            }
            var start = Key(VK.VK_DOWN);
            var from = tiles.FindIndex(tile => tile.Id == start);
            Assert.True(from >= 0, "the keys did not come onto the grid");
            // From the first tile of a line: Right is the next, Down the one under, Left and Up back.
            while (from % 3 != 0 || from / 3 > 1)
            {
                from = tiles.FindIndex(tile => tile.Id == Key(from % 3 != 0 ? VK.VK_LEFT : VK.VK_UP));
            }
            Assert.Equal(tiles[from + 1].Id, Key(VK.VK_RIGHT));
            Assert.Equal((FlyoutEventKind.Hover, tiles[from + 1].Id), Told(menu));
            Assert.Equal(tiles[from + 4].Id, Key(VK.VK_DOWN));
            Assert.Equal((FlyoutEventKind.Hover, tiles[from + 4].Id), Told(menu));
            Assert.Equal(tiles[from + 3].Id, Key(VK.VK_LEFT));
            Assert.Equal(tiles[from].Id, Key(VK.VK_UP));

            // The pointer on a tile: told, and named in its tooltip.
            _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(middles[5]));
            Assert.Equal(tiles[5].Id, menu.HotId);
            Assert.Equal((FlyoutEventKind.Hover, tiles[5].Id), Told(menu));
            Assert.Equal(Patterns.Name(Choices[5]), menu.TipOfRow(tiles[5].Id));
            menu.Hide();
        });
    }

    /// <summary>The menu's hover timer's tick, as Windows sends it once the pointer or the keys have rested.</summary>
    private static (FlyoutEventKind Kind, uint Id) Told(Flyout menu)
    {
        var told = menu.Message(WM.WM_TIMER, 2, 0);
        return (told.Kind, told.Id);
    }

    private static LPARAM At(POINT point) => (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));
}
