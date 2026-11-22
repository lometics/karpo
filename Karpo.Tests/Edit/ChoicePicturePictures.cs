// §675 (the person, 2026-11-12: "make sure that you not just add the text but actually also add an image that tries to explain /
// show the effect of the change too", "the different styles should get a preview like the patterns for every style in the
// dropdown"): a choice that changes how a thing is drawn pictured by what it draws (Dock.ChoicePictures.cs) — a widget's Style
// each the widget alone set so, the Appearance's Palette and Mode each a piece of a bar of stand-ins wearing it —, each picker
// open on a display 1920 DIP wide at 150 % in edit mode, Windows dark and light. To KARPO_PNG_DIR:
//
//   choice675-<widget>-style-<dark|light>.png   a widget's Style open, a picture a choice
//   choice675-palette-<dark|light>.png          the Appearance's Palette open
//   choice675-mode-<dark|light>.png             the Appearance's Mode open
//   choice678-<type|show|position|layout>-<dark|light>.png   §678 a whole display a choice: Taskbar's Type, Show; a display's edge, layout
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void ChoicePictureSheets()
    {
        WindowShots.OnHiddenDesktop("choice-675", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var windows = dark ? "dark" : "light";
                var config = MenuCorpus.Fresh();
                var place = new OptionPlace(config, dark);

                // Every widget of the config's bars with a Style of a few choices: its page, Style open, each choice its picture.
                foreach (var layout in config.Layouts)
                {
                    foreach (var island in layout.Islands.Where(each => each.Folder is null))
                    {
                        if (OptionRows.Of(island, place: place).FirstOrDefault(row => row is { Slot: OptionSlot.Style, Kind: OptionRowKind.Choice, Choices.Count: >= 2 and <= TheDock.PicturedMost }) is not { } style)
                        {
                            continue;
                        }
                        List<LookShown> pictures = [];
                        for (var at = 0; at < style.Choices.Count; at++)
                        {
                            var copy = config.Clone();
                            var its = copy.Layouts.Single(each => each.Id == layout.Id);
                            var alone = (BarItem)Karpo.Core.Folders.Find(its.Islands, island.Id)!;
                            _ = OptionRows.Find(OptionRows.Of(alone, place: place with { Config = copy }), style.Key)!.Set!(at);
                            pictures.Add(TheDock.Alone(copy, its, alone));
                        }
                        // The pictures differ: each choice draws its own (no two the same words).
                        Assert.Equal(pictures.Count, pictures.Select(each => each.Values()).Distinct().Count());
                        var items = Pictured(MenuCorpus.Arranged(island, place), style.Key, pictures);
                        var page = PanelRows.Page(PanelPage.Widget, island.Kind.ToString(), "", items);
                        var row = PanelRows.All(page.Rows).Single(each => each.Kind == PanelRowKind.Picker && each.Item.Setting == style.Key);
                        Sheet(page, PanelPage.Widget, row, $"choice675-{island.Kind.ToString().ToLowerInvariant()}-style-{windows}.png");
                    }
                }

                // The Appearance's Palette and Mode; §693 Karpo's changed, its badge after its name.
                _ = Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, dark: true, Karpo.Core.Paint.Parse("#FF1E3A5F"));
                var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                var shown = config.ActiveLayout(MenuCorpus.Display);
                var rows = LookRows.Rows(config, shown, dark);
                var appearance = TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance);
                foreach (var key in new[] { LookRows.PaletteRow, LookRows.ModeRow })
                {
                    var choices = OptionRows.Find(rows, key)!.Choices.Count;
                    List<LookShown> pictures = [];
                    for (var at = 0; at < choices; at++)
                    {
                        var copy = config.Clone();
                        Assert.True(OptionRows.Find(LookRows.Rows(copy, shown, dark), key)!.Set!(at) || at == OptionRows.Find(rows, key)!.Chosen);
                        pictures.Add(TheDock.StandIns(copy, shown));
                    }
                    var items = Pictured(appearance, key, pictures);
                    var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), items);
                    var row = PanelRows.All(page.Rows).Single(each => each.Kind == PanelRowKind.Picker && each.Item.Setting == key);
                    Sheet(page, PanelPage.Appearance, row, $"choice675-{(key == LookRows.PaletteRow ? "palette" : "mode")}-{windows}.png");
                }

                // §679: Taskbar's Type as the Palette; its Show and Behavior's choices, a display's layout and edge, a piece of
                // the bar close up where each shows.
                foreach (var (scope, at, key, name) in new[]
                {
                    (LooksScope.Taskbar, PanelPage.Taskbar, BarLooks.TypeRow, "type"),
                    (LooksScope.Taskbar, PanelPage.Taskbar, BarLooks.ShowRow, "show"),
                    (LooksScope.Taskbar, PanelPage.Taskbar, BarLooks.CombineRow, "combine"),
                    (LooksScope.Taskbar, PanelPage.Taskbar, BarLooks.DesktopsRow, "desktops"),
                    (LooksScope.Taskbar, PanelPage.Taskbar, BarLooks.HoverRow, "hover"),
                    // §682: the Appearance's Animations, four to a line.
                    (LooksScope.Appearance, PanelPage.Appearance, KarpoMenu.AnimationsRow, "animations"),
                    (LooksScope.Layouts, PanelPage.Layouts, BarLooks.KeyOf(BarLooks.PositionRow, bar.Display), "position"),
                    (LooksScope.Layouts, PanelPage.Layouts, BarLooks.KeyOf(BarLooks.ShownRow, bar.Display), "layout"),
                })
                {
                    var barRows = BarLooks.Rows(config, bar, dark, scope);
                    var choices = OptionRows.Find(barRows, key)!.Choices.Count;
                    List<LookShown> pictures = [];
                    for (var each = 0; each < choices; each++)
                    {
                        var copy = config.Clone();
                        _ = OptionRows.Find(BarLooks.Rows(copy, bar, dark, scope), key)!.Set!(each);
                        pictures.Add(TheDock.SketchOf(key, bar) is var (sketch, display) ? TheDock.Sketched(copy, sketch, display) : TheDock.StandIns(copy, copy.ActiveLayout(bar.Display)));
                    }
                    // Each choice its own picture.
                    Assert.Equal(pictures.Count, pictures.Select(each => each.Values()).Distinct().Count());
                    var items = Pictured(TheDock.LooksItems(config, bar, dark, [], scope), key, pictures);
                    var page = PanelRows.Page(at, name, PanelRows.GlyphOf(at), items);
                    var row = PanelRows.All(page.Rows).Single(each => each.Kind == PanelRowKind.Picker && each.Item.Setting == key);
                    Sheet(page, at, row, $"choice679-{name}-{windows}.png");
                }

                // §676: a lookup (the language) open: its search over what it finds, in its card.
                var karpo = PanelRows.Page(PanelPage.Karpo, "Karpo", "", TheDock.SettingsItems(config, MenuCorpus.Services(), []));
                var language = PanelRows.All(karpo.Rows).Single(each => each.Kind == PanelRowKind.Picker && each.Text == S.SettingsLanguageSection);
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"choice676-language-{windows}.png", dips: new Vector2(1920f, 1400f), panel: deck =>
                {
                    deck.Memory = new PanelMemory();
                    deck.Memory.Show(PanelPage.Karpo);
                    deck.Memory.Open([language.Key]);
                    deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([karpo]), PanelPage.Karpo);
                    deck.Land(PanelPage.Karpo, language.Key, light: false);
                    var body = deck.Layout!.LineOf(language.Key + "/" + PanelPickers.BodyKey)!;
                    Assert.NotEmpty(deck.Layout.PickerCells(body));
                    Assert.NotNull(deck.Layout.LineOf(language.Key + "/" + PanelPickers.SearchKey));
                });

                void Sheet(PanelPageView page, PanelPage at, PanelRow row, string file) =>
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1400f), panel: deck =>
                    {
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(at);
                        // The row open, and whatever holds it (a display's expander).
                        var path = row.Key.Split('/');
                        deck.Memory.Open([.. Enumerable.Range(1, path.Length).Select(n => string.Join('/', path.Take(n)))]);
                        deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]), at);
                        deck.Land(at, row.Key, light: false);
                        var body = deck.Layout!.LineOf(row.Key + "/" + PanelPickers.BodyKey)!;
                        var cells = deck.Layout.PickerCells(body);
                        // Each choice a tile of its picture, its sample over it.
                        Assert.All(cells.Where(cell => cell.Kind != PickerCellKind.Title), cell => Assert.Equal(PickerCellKind.Tile, cell.Kind));
                        Assert.Equal(cells.Count(cell => cell.Kind == PickerCellKind.Tile), deck.Previews!.Keys.Count(each => each.Key.StartsWith(body.Row.Key + "#", StringComparison.Ordinal)));
                        for (var a = 0; a < cells.Count; a++)
                        {
                            Assert.True(cells[a].Rect.Right <= body.Rect.Right + 0.5f, $"{file}: {cells[a].Item.Text} past its card");
                            for (var b = a + 1; b < cells.Count; b++)
                            {
                                Assert.False(Overlap(cells[a].Rect, cells[b].Rect), $"{file}: {cells[a].Item.Text} over {cells[b].Item.Text}");
                            }
                        }
                    });
            }
        });
    }

    /// <summary>§675: <paramref name="items"/> with the choices of the row <paramref name="key"/> given <paramref name="pictures"/>, in their order, as the dock gives them.</summary>
    private static List<FlyoutItem> Pictured(IReadOnlyList<FlyoutItem> items, string key, IReadOnlyList<LookShown> pictures) =>
        [.. items.Select(row =>
        {
            var item = row.Control is FlyoutControl.Segments segments ? row with { Control = null, Children = PanelRows.ChoicesOf(segments), PickOne = true } : row;
            var kids = item.Children is { Count: > 0 } children ? Pictured(children, key, pictures) : null;
            var next = kids is null ? item : item with { Children = kids };
            if (item.Setting == key && item.PickOne && kids is not null)
            {
                var at = 0;
                next = next with { Children = [.. kids.Select(kid => PanelPickers.Choice(kid) ? kid with { Shows = pictures[at++] } : kid)] };
            }
            return next;
        })];
}
