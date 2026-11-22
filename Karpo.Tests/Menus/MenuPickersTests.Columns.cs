// §627 (the person, 2026-11-09): "the color selector should have a maximum of one line of recent colors and two lines of
// color presets." — "the design should be not row wise but column wise." A colour's picker in two columns side by side:
// its swatches (None or Default, one line of recent colours, two lines of presets, six to a line) beside its mixer (the
// spectrum, the hue, the opacity, the hex box); Flyout.Columns.cs lays any level so, ColorPalette holds the caps.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

public sealed partial class MenuPickersTests
{
    /// <summary>
    /// §690 (the person, 2026-11-12: "there should be one row that is filled with the most recent color choices. but other than
    /// that the color pickers everywhere should have the same preset colors"): one set of presets, two lines of six, each once,
    /// opaque, every colour row's — the Appearance's in every tone and mode, an account's —; the recent colours one line with
    /// None (where a colour may be none), newest first, the oldest dropped past it.
    /// </summary>
    [Fact]
    public void APickerHasOneLineOfRecentColoursAndTwoOfPresets()
    {
        Assert.Equal(6, ColorPalette.Columns);
        var presets = ColorPalette.Presets;
        Assert.Equal(ColorPalette.Columns * ColorPalette.Lines, presets.Count);
        Assert.Equal(presets.Count, presets.Distinct().Count());
        Assert.All(presets, colour => Assert.Equal(255, colour.A));

        var config = new Config();
        var mixed = Enumerable.Range(1, 10).Select(at => new Argb(255, (byte)(at * 20), 0x40, 0x80)).ToList();
        foreach (var colour in mixed)
        {
            Assert.True(ColorPalette.Remember(config, colour));
        }
        Assert.Equal(mixed.AsEnumerable().Reverse().Take(6), ColorPalette.Recent(config));
        Assert.Equal(6, config.RecentColors!.Count);
        // A file of more (an older one's eight) shows one line.
        config.RecentColors = [.. mixed.Select(colour => colour.ToString())];
        Assert.Equal(6, ColorPalette.Recent(config).Count);

        foreach (var dark in new[] { true, false })
        {
            foreach (var row in Colors(new OptionPlace(config, dark)))
            {
                var line = row.Tiles.Where(tile => tile.Group != ColorPalette.PresetsGroup).ToList();
                Assert.InRange(line.Count(tile => !tile.Plain), 1, ColorPalette.Columns);
                Assert.InRange(line.Count, 1, ColorPalette.Columns);
                Assert.Equal(presets, row.Tiles.Where(tile => tile.Group == ColorPalette.PresetsGroup).Select(tile => tile.Color!.Value));
            }
        }
        var account = PartColours.Row("account", "Color", null, PartColours.Palette[5], _ => true);
        Assert.Equal(presets, account.Tiles.Where(tile => !tile.Plain).Select(tile => tile.Color!.Value));
    }

    /// <summary>
    /// The picker's level in two columns (Flyout.Lanes): its swatches the first, a line of six cells wide; its mixer the
    /// second, the rest of the picker's 400 DIP, a rule's room between; each run of swatches in lines of six — the recent
    /// colours one, the presets two.
    /// </summary>
    [Fact]
    public void ThePickersSwatchesStandInAColumnBesideItsMixer()
    {
        const float scale = 1.5f;
        var config = new Config();
        foreach (var at in Enumerable.Range(1, 9))
        {
            _ = ColorPalette.Remember(config, new Argb(255, (byte)(at * 25), 0x30, 0x60));
        }
        var level = OptionMenu.Section(Colors(new OptionPlace(config, true)), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader).Children!;
        var (inset, cell, rule) = (5 * scale, Flyout.TileCellDip * scale, 9 * scale);
        var room = (PanelMetrics.PickerWide * scale) - (2 * inset);
        var lanes = Flyout.Lanes(level, inset, room, cell, rule)!;
        Assert.NotNull(lanes);
        var split = level.ToList().FindIndex(Flyout.StartsColumn);
        Assert.True(split > 0, "the picker's level has no column of its own for its mixer");
        Assert.All(level.Take(split), (item, at) => Assert.Equal(0, lanes[at].Index));
        Assert.All(level.Skip(split + 1), item => Assert.True(item.Control is FlyoutMix or FlyoutField { Hex: true }, $"{item.Text} stands in the mixer's column"));
        Assert.Equal((inset, 6 * cell), (lanes[0].X, lanes[0].W));
        var mixer = lanes[split + 1];
        Assert.Equal((1, inset + (6 * cell) + rule), (mixer.Index, mixer.X));
        Assert.Equal(inset + room, mixer.X + mixer.W);
        Assert.InRange(mixer.W / scale, 180, 200);

        var spots = Flyout.TileSpots(level, i => lanes[i].W, cell, scale);
        var tiles = level.Select((item, at) => (item, at)).Where(each => Flyout.IsTile(each.item)).ToList();
        Assert.All(tiles, each => Assert.Equal(6, spots[each.at].Columns));
        // §690: None and five recent colours one line; the twelve presets two more.
        var recent = tiles.Where(each => ColorPalette.Recent(config).Any(colour => ((FlyoutTile)each.item.Icon!).Color == colour)).ToList();
        Assert.Equal(ColorPalette.Columns - 1, recent.Count);
        Assert.All(recent, each => Assert.Equal(0, spots[each.at].Line));
        Assert.Equal(1, spots[tiles[^1].at].Line);
        Assert.Equal(recent.Count + 12, tiles.Count);
        // A level of one column has none.
        Assert.Null(Flyout.Lanes([new(1, "One"), new(0, "", Separator: true), new(2, "Two")], inset, room, cell, rule));
    }

    /// <summary>
    /// §627 on a real menu (KARPO_PNG_DIR picker627-dark.png and -light.png, Background's picker as the settings panel hangs
    /// it, a line of recent colours): 400 DIP wide; the swatches' column at the left from the top, the mixer's beside it from
    /// the top, none over the other; the level as tall as the mixer, shorter than §613's stacked rows. The keys: Tab down the
    /// swatches and on into the mixer; Down along the swatches' column, past its end into the mixer, whose spectrum keeps its
    /// own Up (§523).
    /// </summary>
    [UiFact]
    public void ThePickerStandsInColumnsOnARealMenu()
    {
        WindowShots.OnHiddenDesktop("picker-627", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var config = new Config();
                foreach (var colour in new[] { 0x8E562E, 0x4F6BED, 0xC19C00, 0x038387, 0xB146C2, 0xD13438, 0x2B2B2B })
                {
                    _ = ColorPalette.Remember(config, new Argb(255, (byte)(colour >> 16), (byte)(colour >> 8), (byte)colour));
                }
                var place = new OptionPlace(config, dark);
                var level = OptionMenu.Section(Colors(place), []).Single(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader).Children!.ToList();
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                // Hung from a colour button as the panel hangs it: a control's popup.
                var button = new RECT { left = (int)(900 * scale), top = (int)(200 * scale), right = (int)(980 * scale), bottom = (int)(232 * scale) };
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                menu.Show(gfx, look, scale, level, FlyoutLayout.List, new POINT { x = button.left, y = button.bottom }, work, Edge.Top, control: button);
                WindowShots.Settle();
                Assert.True(menu.IsOpen);
                Assert.True(Save(menu, $"picker627-{(dark ? "dark" : "light")}.png", dark));
                var panel = menu.PanelOnScreen;
                Assert.Equal((int)MathF.Round(PanelMetrics.PickerWide * scale), panel.right - panel.left);

                var laid = menu.RowsLaid;
                var swatches = laid.Where(row => row.Lane == 0).ToList();
                var mixer = laid.Where(row => row.Lane == 1).ToList();
                Assert.Equal(4, mixer.Count);
                Assert.All(mixer, row => Assert.True(row.Item.Control is FlyoutMix or FlyoutField { Hex: true }));
                // Side by side, each from the top, none over the other.
                var swatchesRight = swatches.Max(row => row.X + row.W);
                Assert.True(swatchesRight < mixer.Min(row => row.X), $"the swatches end at {swatchesRight}, the mixer starts at {mixer.Min(row => row.X)}");
                Assert.InRange(mixer.Min(row => row.Y) - swatches.Min(row => row.Y), -4 * scale, 4 * scale);
                // One line of recent colours, two of presets: three lines of swatches.
                var lines = swatches.Where(row => Flyout.IsTile(row.Item)).Select(row => row.Y).Distinct().Count();
                Assert.Equal(3, lines);
                // As tall as its taller column, the mixer: shorter than the stacked level (the swatches over the mixer).
                var tall = panel.bottom - panel.top;
                var stacked = (swatches.Max(row => row.Y + row.H) - swatches.Min(row => row.Y)) + (mixer.Max(row => row.Y + row.H) - mixer.Min(row => row.Y));
                Assert.True(tall < stacked, $"the picker is {tall} px tall, its columns stacked {stacked}");

                // The keys: Down from the first swatch stays in their column, line by line, and past its end goes on to the spectrum.
                var order = laid.Select(row => row.Item.Id).ToList();
                var spectrum = mixer[0].Item.Id;
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_HOME, 0);
                Assert.Equal(order[0], menu.KeysOn);
                var visited = new List<uint> { menu.KeysOn };
                for (var step = 0; step < 8 && menu.KeysOn != spectrum; step++)
                {
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                    visited.Add(menu.KeysOn);
                }
                Assert.Equal(spectrum, menu.KeysOn);
                Assert.All(visited.SkipLast(1), id => Assert.Contains(swatches, row => row.Item.Id == id));
                Assert.Equal(1 + lines, visited.Count - 1);
                // The spectrum's own arrows (§523): Up moves its point, the keys stay on it.
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_UP, 0);
                Assert.Equal(spectrum, menu.KeysOn);
                // Tab from the first swatch through every swatch, then the mixer's rows in their order.
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_HOME, 0);
                Assert.Equal(order[0], menu.KeysOn);
                foreach (var id in order.Skip(1))
                {
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_TAB, 0);
                    Assert.Equal(id, menu.KeysOn);
                }
                menu.Hide();
            }
        });
    }
}
