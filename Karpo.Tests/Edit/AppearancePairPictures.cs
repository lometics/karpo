// edit, the Appearance page as Karpo draws it while Windows is dark and while it is light (the mode the look is read in,
// injected: Windows' real setting never touched), on the panel the person picked (§520): a display 1920 × 1080 DIP at 150 % in
// edit mode. §613's own sheets (the Mode, one mode a page, a value's reset, the picker, the preview on a desktop) are
// AppearanceModePictures.cs.
//
// §592: the page's one preview over its four tabs (AppearanceTabsSheets): appearance-tab-<n>-<part>-<dark|light>.png, each
// tab chosen, the whole page on a display tall enough for it.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>§568: the Taskbar widgets' colours, each mode its own (§613 the page showing the one of the mode it is in).</summary>
    private static Config PairedAppearance()
    {
        var config = MenuCorpus.Fresh();
        var widgets = Looks.Section(config, LookPart.Widgets);
        void Set(Tone tone, string dark, string light)
        {
            Assert.True(Looks.WritePaint(widgets, tone, dark: true, Karpo.Core.Paint.Parse(dark)));
            Assert.True(Looks.WritePaint(widgets, tone, dark: false, Karpo.Core.Paint.Parse(light)));
        }
        Set(Tone.Background, "#1E3A5F", "#F1EBDF");
        Set(Tone.Text, "#FFE7A0", "#3A3027");
        Assert.True(Looks.WritePaint(widgets, Tone.Accent, dark: true, Karpo.Core.Paint.Parse("#4CC8DF")));
        return config;
    }


    /// <summary>
    /// §592 (the person, 2026-11-09: "for the appearance settings page i want only one preview now, then there should be tabs
    /// underneath for the four different sections"): the Appearance page — Palette, its reset, the one preview of the whole
    /// look, the tab strip — with each of its four tabs chosen, its rows under the strip, the whole page on a display tall
    /// enough for it: appearance-tab-<n>-<part>-<dark|light>.png.
    /// </summary>
    [UiFact]
    public void AppearanceTabsSheets()
    {
        WindowShots.OnHiddenDesktop("appearance-tabs", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var config = PairedAppearance();
                _ = Looks.WritePattern(Looks.Section(config, LookPart.Taskbar), Pattern.Dots);
                for (var at = 0; at < LookParts.All.Length; at++)
                {
                    var part = LookParts.All[at];
                    var file = $"appearance-tab-{at + 1}-{LookParts.JsonName(part)}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 2400f), panel: deck =>
                    {
                        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                        var shown = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                        var strip = shown.Rows.Single(row => row.Kind == PanelRowKind.Tabs);
                        Assert.Equal(LookParts.All.Select(LookParts.Name), strip.Rows.Select(tab => tab.Text));
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(PanelPage.Appearance);
                        deck.Memory.Open([strip.Rows[at].Key]);
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), new SettingsContent([shown]));
                        deck.Land(PanelPage.Appearance, null, light: false);
                        // One preview, the strip, the chosen tab's rows alone; the page's last row in sight.
                        var lines = deck.Layout!.Blocks.SelectMany(block => block.Lines).ToList();
                        Assert.Single(lines, each => each.Row.Kind == PanelRowKind.Preview);
                        Assert.Equal(at, deck.Layout.Chosen(strip));
                        var rows = strip.Rows[at].Rows;
                        Assert.All(rows, row => Assert.NotNull(deck.Layout.LineOf(row.Key)));
                        Assert.All(strip.Rows.Where((_, other) => other != at).SelectMany(tab => tab.Rows), row => Assert.Null(deck.Layout.LineOf(row.Key)));
                        var last = deck.Layout.LineOf(rows[^1].Key)!;
                        Assert.False(deck.Layout.OnPanel(last.Rect).Bottom > deck.Layout.Viewport.Bottom, $"{file}: the page's foot is out of sight");
                    });
                }
            }
        });
    }

    /// <summary>
    /// §577: the Appearance page with a palette worn and changed — Graphite, its cards navy while dark and sand while light —:
    /// appearance-palette-<dark|light>.png, its Palette (§583 "Changed" after Graphite in its box) and its Reset to default in
    /// force (§610 beside the dropdown) over the tabs, §613 each colour one button; appearance-palette-list-<dark|light>.png,
    /// Palette's list open, every palette its swatch, Custom last, §583 Graphite and Paper marked "Changed".
    /// </summary>
    [UiFact]
    public void AppearancePaletteSheets()
    {
        WindowShots.OnHiddenDesktop("appearance-palette", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var list in new[] { false, true })
                {
                    var config = MenuCorpus.Fresh();
                    Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.GraphiteId)!));
                    var widgets = Looks.Section(config, LookPart.Widgets);
                    Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: true, Karpo.Core.Paint.Parse("#1E3A5F")));
                    Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: false, Karpo.Core.Paint.Parse("#F1EBDF")));
                    // §583: Paper changed too, not worn: marked in the list all the same.
                    Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.PaperId)!));
                    Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Widgets), Tone.Accent, dark: true, Karpo.Core.Paint.Parse("#E0457B")));
                    Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.GraphiteId)!));
                    var file = $"appearance-palette{(list ? "-list" : "")}-{(dark ? "dark" : "light")}.png";
                    var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                    var shown = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                    var strip = shown.Rows.Single(row => row.Kind == PanelRowKind.Tabs);
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, list ? 1080f : 3800f),
                        panel: deck =>
                        {
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(PanelPage.Appearance);
                            // §670: Palette's list open in its card (no popup).
                            var paletteKey = shown.Rows.Single(row => row.Text == S.SettingsLookPalette).Key;
                            if (list)
                            {
                                deck.Memory.Open([paletteKey]);
                            }
                            deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), new SettingsContent([shown]));
                            deck.Land(PanelPage.Appearance, list ? paletteKey : null, light: false);
                            var lines = deck.Layout!.Blocks.SelectMany(block => block.Lines).ToList();
                            var palette = lines.Single(line => line.Row.Text == S.SettingsLookPalette && line.Row.Kind == PanelRowKind.Picker);
                            Assert.Equal(LookPalettes.Of(LookPalettes.GraphiteId)!.Name, palette.Row.Value);
                            Assert.Equal(S.SettingsLookChanged, palette.Row.ValueMark);
                            // §610: its reset beside its dropdown, in force.
                            Assert.False(palette.Row.Pair!.Greyed);
                            Assert.DoesNotContain(lines, line => line.Row.Kind == PanelRowKind.Action);
                            if (!list)
                            {
                                // §613: each colour of the mode shown; §670 its picker inline, its head the whole line.
                                var colours = lines.Where(line => line.Row.Kind == PanelRowKind.Picker && PanelPickers.Colours(line.Row.Item)).ToList();
                                Assert.NotEmpty(colours);
                                Assert.All(colours, line => Assert.Single(deck.Layout.Controls(line), each => each.Hit.Kind == PanelHitKind.Head));
                                Assert.Contains(deck.Layout.Blocks, block => block.Row.Key == strip.Key);
                            }
                        },
                        panelPopup: null);
                    if (list)
                    {
                        var choices = shown.Rows.Single(row => row.Text == S.SettingsLookPalette).Item.Children!.Where(each => !each.Separator && !each.Header).ToList();
                        Assert.Equal(LookPalettes.Choices.Select(each => each.Name), choices.Select(each => each.Text));
                        Assert.Equal(LookPalettes.Choices.Select(each => each.Id is LookPalettes.GraphiteId or LookPalettes.PaperId ? S.SettingsLookChanged : null), choices.Select(each => each.Value?.Text));
                    }
                }
            }
        });
    }

    /// <summary>
    /// §610 (the person, 2026-11-09: "reset to default in appearance should be shown on the same line as the apply style
    /// dropdown"): the top of the Appearance page, Palette's row with its reset before its dropdown —
    /// palettereset-<changed|unchanged|custom>-<dark|light>.png: Graphite worn and changed (the reset in force), Graphite as
    /// designed (greyed), Custom (none, the dropdown alone).
    /// </summary>
    [UiFact]
    public void PaletteResetSheets()
    {
        WindowShots.OnHiddenDesktop("palette-reset", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var state in new[] { "changed", "unchanged", "custom" })
                {
                    var config = MenuCorpus.Fresh();
                    Assert.True(LookPalettes.Wear(config, state == "custom" ? LookPalettes.Custom : LookPalettes.Of(LookPalettes.GraphiteId)!));
                    if (state == "changed")
                    {
                        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Widgets), Tone.Background, dark, Karpo.Core.Paint.Parse(dark ? "#1E3A5F" : "#F1EBDF")));
                    }
                    var file = $"palettereset-{state}-{(dark ? "dark" : "light")}.png";
                    var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                    var shown = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f),
                        panel: deck =>
                        {
                            deck.Memory = new PanelMemory();
                            deck.Memory.Show(PanelPage.Appearance);
                            deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), new SettingsContent([shown]));
                            deck.Land(PanelPage.Appearance, null, light: false);
                            var lines = deck.Layout!.Blocks.SelectMany(block => block.Lines).ToList();
                            var palette = lines.Single(line => line.Row.Text == S.SettingsLookPalette && line.Row.Kind == PanelRowKind.Picker);
                            Assert.DoesNotContain(lines, line => line.Row.Kind == PanelRowKind.Action);
                            var controls = deck.Layout.Controls(palette);
                            if (state == "custom")
                            {
                                Assert.Null(palette.Row.Pair);
                                Assert.Single(controls);
                                return;
                            }
                            Assert.Equal((PanelHitKind.Act, state == "unchanged"), (controls[0].Hit.Kind, palette.Row.Pair!.Greyed));
                            Assert.Equal(controls[1].Rect.X - deck.Layout.P(PanelMetrics.Gap), controls[0].Rect.Right);
                        });
                }
            }
        });
    }
}
