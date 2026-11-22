// §682 (the person, 2026-11-12): the settings pages' inline rows open, each as the page has it — the Karpo page's (language,
// holidays), the Accounts page's (an account's colour, its mail folders), a folder's icon, the Appearance's animations —,
// Windows dark and light, on a display 1920 DIP wide at 150 %. To KARPO_PNG_DIR as inline682-<name>-<dark|light>.png.
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
    public void InlineRowSheets()
    {
        WindowShots.OnHiddenDesktop("inline-682", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var windows = dark ? "dark" : "light";
                var config = MenuCorpus.Fresh();
                var services = MenuCorpus.Services();
                var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                var karpo = PanelRows.Page(PanelPage.Karpo, "Karpo", "", TheDock.SettingsItems(config, services, []));
                var accounts = PanelRows.Page(PanelPage.Accounts, S.SettingsNavAccounts, PanelRows.GlyphOf(PanelPage.Accounts), TheDock.SettingsItems(config, services, [], page: KarpoPage.Accounts));
                var appearance = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, dark, [], LooksScope.Appearance));
                var layouts = PanelRows.Page(PanelPage.Layouts, "Layouts", PanelRows.GlyphOf(PanelPage.Layouts), TheDock.LooksItems(config, bar, dark, [], LooksScope.Layouts));

                IEnumerable<PanelRow> Every(PanelPageView page) => PanelRows.All(page.Rows).SelectMany(row => (IEnumerable<PanelRow>)[row, .. row.Pairs]);
                PanelRow? Find(PanelPageView page, Func<PanelRow, bool> which) => Every(page).FirstOrDefault(which);
                // §682: the row a dropdown beside it stands in, and that dropdown, open under its line.
                void Beside(string name, PanelPageView page, PanelPage at, Func<PanelRow, bool> which)
                {
                    var holder = PanelRows.All(page.Rows).FirstOrDefault(row => row.Pairs.Any(which) || which(row));
                    var inline = holder is null ? null : holder.Pairs.FirstOrDefault(which) ?? holder;
                    Assert.True(inline is null || PanelPickers.Inline(inline), $"{name}: {inline?.Kind} not inline");
                    Open(name, page, at, holder, inline is null ? null : [inline.Key]);
                }
                void Open(string name, PanelPageView page, PanelPage at, PanelRow? row, IEnumerable<string>? more = null)
                {
                    if (row is null)
                    {
                        Console.WriteLine($"inline682: no row for {name}");
                        return;
                    }
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"inline682-{name}-{windows}.png", dips: new Vector2(1920f, 1400f), panel: deck =>
                    {
                        deck.Memory = new PanelMemory();
                        deck.Memory.Show(at);
                        var path = row.Key.Split('/');
                        deck.Memory.Open([.. Enumerable.Range(1, path.Length).Select(n => string.Join('/', path.Take(n))), .. more ?? []]);
                        // §689: the errors' page hangs from their indicator: there while there are errors.
                        var state = deck.State ?? PictureState();
                        deck.Show(gfx, look, scale, deck.Room!, at == PanelPage.Errors ? state with { Errors = page.Rows.Count } : state, new SettingsContent([page]), at);
                        deck.Land(at, more?.LastOrDefault() ?? row.Key, light: false);
                    });
                }

                // §689: the errors' page.
                var now = DateTime.Now;
                List<Fault> faults =
                [
                    new("weather", FaultKind.Widget, "Weather", OptionGlyphs.Cloud, "The weather service didn't answer", "bar/1", now.AddMinutes(-5).ToUniversalTime()),
                    new("account", FaultKind.Account, "me@example.com", OptionGlyphs.Accounts, "Sign in again to sync", "me@example.com", now.AddDays(-1).ToUniversalTime()),
                ];
                var errors = PanelRows.Page(PanelPage.Errors, S.DockEditErrors(2), PanelRows.GlyphOf(PanelPage.Errors), TheDock.FaultItems(faults, now));
                Open("errors", errors, PanelPage.Errors, errors.Rows[0]);
                // §693: a palette changed, its badge after its name.
                var changed = MenuCorpus.Fresh();
                _ = Looks.WritePaint(Looks.Section(changed, LookPart.Taskbar), Tone.Background, dark: true, Karpo.Core.Paint.Parse("#FFFFFF"));
                var palettes = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(changed, bar, dark, [], LooksScope.Appearance));
                Open("palette-changed", palettes, PanelPage.Appearance, Find(palettes, row => row.Item.Setting == LookRows.PaletteRow && row.Kind == PanelRowKind.Picker));
                Open("language", karpo, PanelPage.Karpo, Find(karpo, row => row.Text == S.SettingsLanguageSection && row.Kind == PanelRowKind.Picker));
                Open("animations", karpo, PanelPage.Karpo, Find(karpo, row => row.Item.Setting == KarpoMenu.AnimationsRow) ?? Find(appearance, row => row.Item.Setting == KarpoMenu.AnimationsRow));
                Open("animations-appearance", appearance, PanelPage.Appearance, Find(appearance, row => row.Item.Setting == KarpoMenu.AnimationsRow));
                Open("holidays", karpo, PanelPage.Karpo, Find(karpo, row => row.Item.Setting == OptionHolidays.Add));
                Beside("holidays-add", karpo, PanelPage.Karpo, row => row.Item.Setting == OptionHolidays.Add && PanelPickers.Inline(row));
                Beside("holidays-region", karpo, PanelPage.Karpo, row => row.Item.Setting?.StartsWith(OptionHolidays.Region, StringComparison.Ordinal) == true && PanelPickers.Inline(row));
                // §682: a button's picker (an account's calendar's colour, a holiday calendar's) unfolded under its line.
                void Folded(string name, PanelPageView page, PanelPage at)
                {
                    var holder = PanelRows.All(page.Rows).FirstOrDefault(row => row.Folded.Any());
                    Open(name, page, at, holder, holder is null ? null : [holder.Folded.First().Key]);
                }
                Folded("colour", accounts, PanelPage.Accounts);
                Folded("holidays-colour", karpo, PanelPage.Karpo);
                Open("mailfolders", accounts, PanelPage.Accounts, Find(accounts, row => row.Kind == PanelRowKind.Link));
                Open("position", layouts, PanelPage.Layouts, Find(layouts, row => row.Item.Setting == BarLooks.KeyOf(BarLooks.PositionRow, bar.Display)));
                Open("layout", layouts, PanelPage.Layouts, Find(layouts, row => row.Item.Setting == BarLooks.KeyOf(BarLooks.ShownRow, bar.Display)));
                var icon = Find(layouts, row => row.Item.Setting?.StartsWith(BarLooks.IconRow, StringComparison.Ordinal) == true && row.Kind == PanelRowKind.Picker);
                Open("icon", layouts, PanelPage.Layouts, icon);
            }
        });
    }
}
