// A menu laid out as Explorer's context menu (Flyout.cs, Flyout.Primary.cs; the person, 2026-10-16:
// "make sure that the design of the right click looks exactly the same, right now the top row does
// not match the design for example and the keyboard shortcuts are also not shown"): the measures
// taken off Explorer's own menu at 150 %, held at every scale; the row of buttons icons alone, square
// (2026-10-19, §214: "the settings row should only have icons not descriptions of the icons"); the
// shortcuts shown, in one column, and taken by the menu; Explorer's two-colour pictures where Windows
// has them (ExplorerIcons).
//
// The layout needs Direct2D and a window station: KARPO_UI_TESTS=1. The rest is pure.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

// Some of these speak German and French (InLanguage.Use): one at a time, after the rest.
[Collection(InLanguage.Collection)]
public sealed class MenuLookTests
{
    private static float Px(float dip, float scale) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Explorer's file menu in Karpo's rows (ExplorerMenuPictures.FileMenu), at 100, 125, 150 and 200 %:
    /// the row of buttons hard under the rim, 40 DIP tall; five square buttons of 40 DIP at the row's
    /// end (§350), a hairline between, each its plate 4 DIP in all round and its 16 DIP icon in the middle,
    /// no word under it; the rows' plates 28 DIP tall and 32 apart, 5 DIP in; the icon 16 DIP and the
    /// words 44 DIP in from the menu's edge; every shortcut from one column's left, its right 16 DIP in.
    /// </summary>
    [UiFact]
    public void AMenuHasExplorersMeasures()
    {
        WindowShots.OnHiddenDesktop("menu-look", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.25f, 1.5f, 2f })
            {
                using var menu = new Flyout(gfx, instance, 0);
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                menu.Show(gfx, AddListPictures.LookFor(true), scale, ExplorerMenuPictures.FileMenu(), FlyoutLayout.List, new POINT { x = work.right / 2, y = 0 }, work, Edge.Top, null);
                WindowShots.Settle(30);
                var looks = menu.Looks.ToDictionary(look => look.Id);
                var width = menu.PanelSize.X;
                var at = $"at {scale * 100} %";

                // The row: five square buttons at the row's end, a hairline apart; the menu as wide as its rows.
                var edge = Px(1, scale) + Px(Flyout.PrimaryEdgeDip, scale);
                var button = Px(Flyout.PrimaryButtonDip, scale);
                var inset = Px(Flyout.PrimaryPlateDip, scale);
                Assert.True(width >= 2 * edge + (5 * button) + (4 * Px(1, scale)), at);
                var plates = Enumerable.Range(1, 5).Select(id => looks[(uint)id].Plate).ToList();
                Assert.All(plates, plate => Assert.Equal(Px(1, scale) + inset, plate.Y));
                Assert.All(plates, plate => Assert.Equal(button - (2 * inset), plate.W, 1f));
                Assert.All(plates, plate => Assert.Equal(button - (2 * inset), plate.H, 1f));
                Assert.Equal(width - edge - inset, plates[^1].X + plates[^1].W, 0.5f);
                for (int i = 1; i < 5; i++)
                {
                    Assert.Equal(button + Px(1, scale), plates[i].X - plates[i - 1].X, 1f);
                }
                // Its icon alone, in the middle of the button; no word under it.
                var cut = looks[1];
                Assert.Equal(Px(16, scale), cut.Icon.W);
                Assert.Equal(cut.Plate.X + (cut.Plate.W / 2), cut.Icon.X + (cut.Icon.W / 2), 1f);
                Assert.Equal(cut.Plate.Y + (cut.Plate.H / 2), cut.Icon.Y + (cut.Icon.H / 2), 1f);
                Assert.All(Enumerable.Range(1, 5), id => Assert.Equal(0, looks[(uint)id].Words.W));
                // The row is a button tall: the next rule hard under it.
                Assert.Equal(Px(1, scale) + button, plates[0].Y + plates[0].H + inset, 1f);

                // The rows.
                var open = looks[6];
                var with = looks[7];
                Assert.Equal(Px(28, scale), open.Plate.H);
                Assert.Equal(Px(32, scale), with.Plate.Y - open.Plate.Y, 1f);
                Assert.Equal(Px(5, scale), open.Plate.X);
                Assert.Equal(width - (2 * Px(5, scale)), open.Plate.W);
                Assert.Equal(Px(5, scale) + Px(11, scale), open.Icon.X);
                Assert.Equal(Px(5, scale) + Px(11, scale) + Px(28, scale), open.Words.X);
                Assert.Equal(open.Plate.Y + MathF.Round((open.Plate.H - open.Icon.H) / 2, MidpointRounding.AwayFromZero), open.Icon.Y);

                // The shortcuts: where a row has one, in one column, the widest ending 16 DIP in.
                uint[] keyed = [6, 11, 12];
                Assert.All(keyed, id => Assert.True(looks[id].Keys.W > 0, $"row {id} shows no shortcut {at}"));
                Assert.All(looks.Values.Where(look => !keyed.Contains(look.Id)), look => Assert.Equal(0, look.Keys.W));
                Assert.Single(keyed.Select(id => looks[id].Keys.X).Distinct());
                var widest = keyed.Max(id => looks[id].Keys.X + looks[id].Keys.W);
                Assert.Equal(width - Px(5, scale) - Px(11, scale), widest, 1f);
                menu.Hide();
            }
        });
    }

    /// <summary>
    /// The person, 2026-10-16: "the context menu row should not stretch if there are not enough items to
    /// fill the whole row." A widget's three buttons (§366: the bar's menu has none now) over its rows, in a menu wider than they are: each keeps
    /// its own width, together at the row's end (its left in Arabic; §350: "the top edit row in the
    /// context menu should show all of the icons right aligned"), a hairline apart, the rest of the row
    /// empty at its start; the rows under it the menu's whole width.
    /// </summary>
    [UiFact]
    public void AFewButtonsKeepTheirWidthsAtTheRowsEnd()
    {
        WindowShots.OnHiddenDesktop("menu-few", (gfx, instance) =>
        {
            foreach (var (language, scale) in new[] { ("en", 1f), ("en", 1.5f), ("ar", 1f), ("ar", 1.5f) })
            {
                using var speaking = InLanguage.Use(language);
                List<FlyoutItem> rows = [.. TheDock.WidgetButtons(), .. TheDock.BarRows(BarMenus.Default(), [], windowsTerminal: true, ShortcutLooks.None).Items];
                using var menu = new Flyout(gfx, instance, 0);
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                menu.Show(gfx, AddListPictures.LookFor(true), scale, rows, FlyoutLayout.List, new POINT { x = work.right / 2, y = work.bottom }, work, Edge.Bottom, null);
                WindowShots.Settle(30);
                var width = menu.PanelSize.X;
                var ids = rows.Where(row => row.Primary).Select(row => row.Id).ToList();
                Assert.Equal(3, ids.Count);
                var looks = menu.Looks.ToDictionary(look => look.Id);
                var plates = ids.Select(id => looks[id].Plate).ToList();
                var at = $"{language} at {scale * 100} %";
                var edge = Px(1, scale) + Px(Flyout.PrimaryEdgeDip, scale);
                var side = Px(Flyout.PrimaryPlateDip, scale);
                var rightToLeft = language == "ar";
                // The last at the row's end, each before it a button's width and a hairline back.
                if (rightToLeft)
                {
                    Assert.Equal(edge + side, plates[^1].X, 0.5f);
                }
                else
                {
                    Assert.Equal(width - edge - side, plates[^1].X + plates[^1].W, 1f);
                }
                Assert.All(plates, plate => Assert.Equal(Px(Flyout.PrimaryButtonDip, scale) - (2 * side), plate.W, 0.5f));
                for (int i = 1; i < plates.Count; i++)
                {
                    var (near, far) = rightToLeft ? (plates[i], plates[i - 1]) : (plates[i - 1], plates[i]);
                    Assert.Equal((2 * side) + Px(1, scale), far.X - (near.X + near.W), 1f);
                }
                // The rest of the row stays empty, at its start: the first button starts well in from it.
                var first = plates[0];
                var room = rightToLeft ? width - edge - side - (first.X + first.W) : first.X - side - edge;
                Assert.True(room > Px(32, scale), $"{at}: {room} px left of {width}");
                // The rows under it still the menu's whole width.
                var below = looks.Values.First(look => !ids.Contains(look.Id) && look.Plate.W > 0);
                Assert.Equal(width - (2 * Px(5, scale)), below.Plate.W);
                menu.Hide();
            }
        });
    }

    /// <summary>A row's shortcut chooses it while the menu stands open: Delete is "Remove" in a widget's menu in edit mode.</summary>
    [UiFact]
    public void AShortcutChoosesItsRow()
    {
        WindowShots.OnHiddenDesktop("menu-keys", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0);
            var work = new RECT { right = 1920, bottom = 1040 };
            menu.Show(gfx, AddListPictures.LookFor(true), 1f, [.. TheDock.WidgetButtons(), new(7, "Own")], FlyoutLayout.List, new POINT { x = 960, y = 1040 }, work, Edge.Bottom, null);
            WindowShots.Settle(30);
            var chosen = menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_DELETE, 0);
            Assert.Equal(FlyoutEventKind.Invoke, chosen.Kind);
            Assert.Equal(TheDock.WidgetButtons().Single(row => row.Text == "Remove").Id, chosen.Id);
            Assert.False(menu.IsOpen);
        });
    }

    /// <summary>Explorer's pictures are drawn where this Windows has them, a glyph where not.</summary>
    [UiFact]
    public void ExplorersPicturesAreDrawnWhereWindowsHasThem()
    {
        var has = File.Exists(ExplorerIcons.PathOf("windows.copy", ExplorerIcons.Tone.Dark));
        WindowShots.OnHiddenDesktop("menu-pictures", (gfx, instance) =>
        {
            using var menu = new Flyout(gfx, instance, 0);
            var work = new RECT { right = 1920, bottom = 1040 };
            menu.Show(gfx, AddListPictures.LookFor(true), 1f, ExplorerMenuPictures.FileMenu(), FlyoutLayout.List, new POINT { x = 960, y = 0 }, work, Edge.Top, null);
            WindowShots.Settle(30);
            var looks = menu.Looks.ToDictionary(look => look.Id);
            Assert.Equal(has, looks[2].Explorer);
            // "Add to Favourites" has a star Explorer has no picture of here: a glyph.
            Assert.False(looks[9].Explorer);
        });
    }

    // --- Pure -------------------------------------------------------------------------------------

    /// <summary>
    /// The row's spans, in every menu (§350): each button its own width, a hairline between each two, all
    /// together against the row's far end, the room to spare at its start; nothing stretches, nothing
    /// stands apart, and none before the row's start where the row has no room.
    /// </summary>
    [Fact]
    public void TheRowsButtonsStandTogetherAtItsEnd()
    {
        // Five square buttons of 60 px at 150 %, 4 px in, 2 px between, in a row as wide as they are.
        Assert.Equal([(4f, 60f), (66f, 60f), (128f, 60f), (190f, 60f), (252f, 60f)], Flyout.PrimarySpans([60, 60, 60, 60, 60], 4, 2, width: 316));
        // Cut, Copy and Remove in a 384 px menu: the last 3 px short of its end, the gaps all alike.
        var spans = Flyout.PrimarySpans([60, 60, 60], 3, 2, width: 384);
        Assert.Equal([(197f, 60f), (259f, 60f), (321f, 60f)], spans);
        Assert.Equal(384f - 3f, spans[^1].X + spans[^1].W);
        Assert.Equal([2f, 2f], spans.Zip(spans.Skip(1), (near, far) => far.X - (near.X + near.W)));
        // One alone: at the end.
        Assert.Equal([(321f, 60f)], Flyout.PrimarySpans([60], 3, 2, width: 384));
        // Fractional sizes come out whole.
        Assert.All(Flyout.PrimarySpans([40.4f, 40.6f], 1.5f, 1, width: 300.7f), span => Assert.Equal(MathF.Round(span.X), span.X));
        // A row too narrow: from its start, as many as there are.
        Assert.Equal([(3f, 60f), (65f, 60f), (127f, 60f)], Flyout.PrimarySpans([60, 60, 60], 3, 2, width: 100));
        Assert.Empty(Flyout.PrimarySpans([], 2, 1, width: 384));
    }

    /// <summary>A shortcut's words in the menu's language, and the keys it is.</summary>
    [Fact]
    public void AShortcutReadsAndMatches()
    {
        Assert.Equal("Ctrl+C", MenuChord.Copy.Text);
        Assert.Equal("Del", MenuChord.Delete.Text);
        Assert.Equal("Esc", MenuChord.Escape.Text);
        Assert.Equal("F2", new MenuChord(VK.VK_F2).Text);
        Assert.Equal("Copy (Ctrl+C)", MenuChord.Copy.Tip("Copy"));
        Assert.True(MenuChord.Copy.Is('C', control: true, alt: false));
        Assert.False(MenuChord.Copy.Is('C', control: false, alt: false));
        Assert.False(MenuChord.Copy.Is('C', control: true, alt: true));
        Assert.False(MenuChord.Delete.Is(VK.VK_DELETE, control: true, alt: false));
        using (InLanguage.Use("de"))
        {
            Assert.Equal("Strg+C", MenuChord.Copy.Text);
            Assert.Equal("Entf", MenuChord.Delete.Text);
        }
        using (InLanguage.Use("fr"))
        {
            Assert.Equal("Échap", MenuChord.Escape.Text);
        }
    }

    /// <summary>
    /// The shortcuts a menu shows are the ones Karpo does: "Copy" Ctrl+C and "Remove" Delete, as in edit
    /// mode (EditKeys), "Done editing" Escape; a row's own words (a window's menu's) win over its keys.
    /// </summary>
    [Fact]
    public void TheRowsWithShortcutsAreTheOnesKarpoDoes()
    {
        var buttons = TheDock.WidgetButtons();
        Assert.Equal(MenuChord.Cut, buttons.Single(row => row.Text == "Cut").Keys);
        Assert.Equal(MenuChord.Copy, buttons.Single(row => row.Text == "Copy").Keys);
        Assert.Equal(MenuChord.Delete, buttons.Single(row => row.Text == "Remove").Keys);
        Assert.Equal(EditKeyAct.Cut, EditKeys.Of('X', control: true, shift: false, repeat: false).Act);
        Assert.Equal(EditKeyAct.None, EditKeys.Of('X', control: true, shift: false, repeat: true).Act);
        Assert.Equal(EditKeyAct.Copy, EditKeys.Of('C', control: true, shift: false, repeat: false).Act);
        Assert.Equal(EditKeyAct.Remove, EditKeys.Of(VK.VK_DELETE, control: false, shift: false, repeat: false).Act);
        Assert.Equal("Alt+F4", new FlyoutItem(1, "Close", Accelerator: "Alt+F4", Keys: MenuChord.Escape).Shortcut);
    }

    /// <summary>
    /// A button of the row is its icon alone (2026-10-19, §214): every one has a picture, and its whole
    /// name — with its shortcut, as Explorer's say theirs — is its tooltip and what a screen reader says.
    /// </summary>
    [Fact]
    public void TheRowsButtonsAreIconsNamedInTheirTooltips()
    {
        var widget = TheDock.WidgetButtons();
        Assert.All(widget, row => Assert.NotNull(row.Icon));
        Assert.Equal(["Cut (Ctrl+X)", "Copy (Ctrl+C)", "Remove (Del)"], widget.Select(Flyout.TipText));
    }

    /// <summary>Explorer's pictures by the glyph a row names: its Copy, Cut, Delete; none for a glyph it has no picture of.</summary>
    [Fact]
    public void ExplorersPicturesAreFoundByGlyph()
    {
        Assert.Equal("windows.copy", ExplorerIcons.NameFor(""));
        Assert.Equal("windows.cut", ExplorerIcons.NameFor(""));
        Assert.Equal("windows.ribbondelete", ExplorerIcons.NameFor(""));
        Assert.Equal("windows.folderoptions", ExplorerIcons.NameFor(""));
        Assert.Null(ExplorerIcons.NameFor(""));
        Assert.Null(ExplorerIcons.NameFor(""));
        Assert.Null(ExplorerIcons.NameFor(""));
        Assert.EndsWith(Path.Combine("contrast-standard", "theme-light", "windows.copy.svg"), ExplorerIcons.PathOf("windows.copy", ExplorerIcons.Tone.Light));
        Assert.Null(ExplorerIcons.For("", ExplorerIcons.Tone.Dark));
    }
}
