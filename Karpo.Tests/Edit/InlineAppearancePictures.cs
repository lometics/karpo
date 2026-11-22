// §670 (the person, 2026-11-12: "i want the pickers to be inlined. so that they dont show up as pickers that popover but
// basically as expandable sections that have the picker inside ... leaned on win11 style"): every choice and colour of the
// Appearance page a Windows 11 SettingsExpander, its picker inside the card (SettingsPanel.Picker.cs). The page with every
// one shut, then each opened in turn and landed on, its tab chosen, on a display 1920 DIP wide at 150 % in edit mode, Windows
// dark and light. To KARPO_PNG_DIR:
//
//   inline670-page-<dark|light>.png          the page, every picker shut: what each is set to in its head
//   inline670-<tab>-<row>-<dark|light>.png   that picker open, landed on
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
    private static bool Overlap(RectF a, RectF b) => a.X < b.Right - 0.5f && b.X < a.Right - 0.5f && a.Y < b.Bottom - 0.5f && b.Y < a.Bottom - 0.5f;

    [UiFact]
    public void InlineAppearanceSheets()
    {
        WindowShots.OnHiddenDesktop("inline-670", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var windows = dark ? "dark" : "light";
                var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance),
                    TheDock.LooksItems(ModedAppearance(TaskbarMode.System), bar, dark, [], LooksScope.Appearance));
                var tabs = page.Rows.Where(row => row.Kind == PanelRowKind.Tabs).SelectMany(row => row.Rows).ToList();
                var pickers = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Picker).ToList();
                Assert.NotEmpty(pickers);
                // No dropdown nor colour button is left on the page: each is a picker in its card.
                Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Kind is PanelRowKind.Color || row.Kind == PanelRowKind.Choice);
                void Open(EditDeck deck, IEnumerable<string> open, string? land)
                {
                    deck.Memory = new PanelMemory();
                    deck.Memory.Show(PanelPage.Appearance);
                    deck.Memory.Open([.. open]);
                    deck.Show(gfx, look, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                    deck.Land(PanelPage.Appearance, land, light: false);
                }
                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"inline670-page-{windows}.png",
                    dips: new Vector2(1920f, 2600f), panel: deck => Open(deck, [], null));
                foreach (var row in pickers)
                {
                    var tab = tabs.FirstOrDefault(each => row.Key.StartsWith(each.Key + "/", StringComparison.Ordinal));
                    var name = $"{(tab is null ? "page" : tab.Text)}-{row.Text}".Replace(' ', '_').ToLowerInvariant();
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"inline670-{name}-{windows}.png",
                        dips: new Vector2(1920f, 1400f), panel: deck =>
                        {
                            Open(deck, tab is null ? [row.Key] : [tab.Key, row.Key], row.Key);
                            var line = deck.Layout!.LineOf(row.Key)!;
                            Assert.True(deck.Layout.StandsOpen(line.Row));
                            var body = deck.Layout.LineOf(row.Key + "/" + PanelPickers.BodyKey)!;
                            var cells = deck.Layout.PickerCells(body);
                            Assert.NotEmpty(cells);
                            // Every cell within its card, none over another.
                            Assert.All(cells, cell => Assert.True(cell.Rect.X >= body.Rect.X && cell.Rect.Right <= body.Rect.Right + 0.5f && cell.Rect.Bottom <= body.Rect.Bottom + 0.5f, $"{name}: {cell.Item.Text} {cell.Rect} outside {body.Rect}"));
                            for (var a = 0; a < cells.Count; a++)
                            {
                                for (var b = a + 1; b < cells.Count; b++)
                                {
                                    Assert.False(Overlap(cells[a].Rect, cells[b].Rect), $"{name}: {cells[a].Item.Text} over {cells[b].Item.Text}");
                                }
                            }
                        });
                }
            }
        });
    }
}
