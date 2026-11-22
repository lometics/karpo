// §670: the Appearance page's pickers inline (SettingsPanel.Picker.cs), driven as Windows hands the edit bar's flyout the
// pointer and the keys, on a desktop of its own (KARPO_UI_TESTS=1):
//
//  - a head's click opens its picker in its card and shuts it again; nothing pops up;
//  - a choice clicked sends its own id, the menu's pick for it, which set as the dock sets it changes the config;
//  - a press on the hue track and a drag mix (EditDeckAction.Mix, worn), let go keep (MixKept) the colour where it stopped,
//    each the mixer's own pick (OptionPickKind.Mix); the thumb follows while held;
//  - a font's search filters its choices as it is typed, keeps its words left, and Escape takes them, every choice back;
//    nothing of it reaches the config.
//
// And without windows: every picker's cells stay inside its card and never over each other, at any width a flyout has.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe class InlinePickerTests
{
    private static readonly RectI Display = new(0, 0, 2880, 1620);

    private static EditDeckRoom Room() => new(Display, 0, 72, Work: new RectI(0, 0, 2880, 1548));

    private static Look Still => AddListPictures.LookFor(dark: true) with { Animations = false };

    private static (Config Config, List<OptionPick> Picks, SettingsContent Content) Appearance()
    {
        var config = MenuCorpus.Fresh();
        List<OptionPick> picks = [];
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, bar, true, picks, LooksScope.Appearance));
        return (config, picks, new SettingsContent([page]));
    }

    private static PanelRow Picker(SettingsContent content, string name, int tab = -1)
    {
        var page = content.Of(PanelPage.Appearance)!;
        var under = tab < 0 ? page.Rows : page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Rows[tab].Rows;
        return under.Single(row => row.Text == name && row.Kind == PanelRowKind.Picker);
    }

    private static EditDeckEvent Press(EditDeck deck, uint msg, Vector2 point) =>
        deck.Message(deck.FlyoutHwnd, msg, msg == WM.WM_LBUTTONUP ? 0u : 1u, EditDeckClickTests.At(deck, point, flyout: true)).Happened;

    [UiFact]
    public void AChoiceIsClickedInItsCard()
    {
        WindowShots.OnHiddenDesktop("inline-choice", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var (config, picks, content) = Appearance();
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Appearance);
            var palette = Picker(content, S.SettingsLookPalette);
            var body = palette.Key + "/" + PanelPickers.BodyKey;
            Assert.Null(deck.Layout!.LineOf(body));
            // Its head: open in its card, nothing asked of the dock.
            var head = deck.Layout.Controls(deck.Layout.LineOf(palette.Key)!).Single(each => each.Hit.Kind == PanelHitKind.Head).Hit;
            Assert.Equal(EditDeckAction.None, EditDeckClickTests.Click(deck, head).Action);
            var line = deck.Layout!.LineOf(body)!;
            var cells = deck.Layout.PickerCells(line);
            Assert.All(cells, cell => Assert.Equal(PickerCellKind.Option, cell.Kind));
            Assert.Single(cells, cell => cell.Item.Checked == true);
            // A choice: its id, the menu's pick, set as the dock sets it.
            var other = cells.First(cell => cell.Item.Checked != true);
            var asked = EditDeckClickTests.Click(deck, other.Hit);
            Assert.Equal((EditDeckAction.Act, PanelPage.Appearance, other.Item.Id), (asked.Action, asked.Page, asked.Id));
            var pick = OptionMenu.PickOf(picks, asked.Id)!;
            var before = config.Appearance?.Palette;
            Assert.True(OptionMenu.ApplyRow(OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, true), pick.Key)!, pick));
            Assert.NotEqual(before, config.Appearance?.Palette);
            // Its head again: shut.
            Assert.Equal(EditDeckAction.None, EditDeckClickTests.Click(deck, head).Action);
            Assert.Null(deck.Layout!.LineOf(body));
        });
    }

    [UiFact]
    public void AColourIsMixedByDraggingAndKeptLetGo()
    {
        WindowShots.OnHiddenDesktop("inline-mix", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var (_, picks, content) = Appearance();
            // The taskbar widgets' accent: a colour of its own hue (a grey has none for its track to turn).
            var background = Picker(content, S.SettingsAppearanceCustomAccentHeader, tab: 1);
            var strip = content.Of(PanelPage.Appearance)!.Rows.Single(row => row.Kind == PanelRowKind.Tabs);
            deck.Memory = new PanelMemory();
            deck.Memory.Show(PanelPage.Appearance);
            deck.Memory.Open([strip.Rows[1].Key, background.Key]);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Appearance);
            deck.Land(PanelPage.Appearance, background.Key + "/" + PanelPickers.BodyKey, light: false);
            var line = deck.Layout!.LineOf(background.Key + "/" + PanelPickers.BodyKey)!;
            var hue = deck.Layout.PickerCells(line).Single(cell => cell is { Kind: PickerCellKind.Mix, Part: MixPart.Hue });
            Assert.Equal(OptionPickKind.Mix, OptionMenu.PickOf(picks, hue.Item.Id)!.Kind);
            var track = deck.Layout.OnPanel(hue.Rect);
            var start = ((FlyoutMix)hue.Item.Control!).Color;
            // Pressed a quarter along, dragged to three quarters: worn each time, the hue where the pointer is.
            var pressed = Press(deck, WM.WM_LBUTTONDOWN, new Vector2(track.X + (track.Width / 4), track.Center.Y));
            Assert.Equal((EditDeckAction.Mix, FlyoutEventKind.Mix, hue.Item.Id), (pressed.Action, pressed.Mixed, pressed.Id));
            var dragged = Press(deck, WM.WM_MOUSEMOVE, new Vector2(track.X + (track.Width * 3 / 4), track.Center.Y));
            Assert.Equal((EditDeckAction.Mix, FlyoutEventKind.Mix), (dragged.Action, dragged.Mixed));
            Assert.InRange(ColorMix.ToHsv(dragged.Color).H, 240, 300);
            Assert.Equal(start.A, dragged.Color.A);
            // Let go: kept, the colour it stopped at; the pointer no longer mixes.
            var kept = Press(deck, WM.WM_LBUTTONUP, new Vector2(track.X + (track.Width * 3 / 4), track.Center.Y));
            Assert.Equal((EditDeckAction.Mix, FlyoutEventKind.MixKept, dragged.Color), (kept.Action, kept.Mixed, kept.Color));
            Assert.NotEqual(EditDeckAction.Mix, Press(deck, WM.WM_MOUSEMOVE, new Vector2(track.X + 4, track.Center.Y)).Action);
        });
    }

    /// <summary>
    /// The person, 2026-11-12: "when dragging to change a color it immeadiately blows up the recent color list with it". Each
    /// colour worn rebuilds the page and its menu, so the dock tells one inline mix by its page (Dock.SameMix): the same
    /// display's same page one mix all along, kept once let go; another page or display another; a menu itself only.
    /// </summary>
    [Fact]
    public void AnInlineMixIsOneMixWhileItsPageIsRebuilt()
    {
        Assert.True(TheDock.SameMix(("A", PanelPage.Appearance), ("A", PanelPage.Appearance)));
        Assert.False(TheDock.SameMix(("A", PanelPage.Appearance), ("B", PanelPage.Appearance)));
        Assert.False(TheDock.SameMix(("A", PanelPage.Appearance), ("A", PanelPage.Widget)));
    }

    [UiFact]
    public void AFontsSearchFiltersItsChoices()
    {
        WindowShots.OnHiddenDesktop("inline-search", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var (_, _, content) = Appearance();
            var font = Picker(content, S.SettingsAppearanceStylesFont, tab: 1);
            var strip = content.Of(PanelPage.Appearance)!.Rows.Single(row => row.Kind == PanelRowKind.Tabs);
            deck.Memory = new PanelMemory();
            deck.Memory.Show(PanelPage.Appearance);
            deck.Memory.Open([strip.Rows[1].Key, font.Key]);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Appearance);
            deck.Land(PanelPage.Appearance, font.Key, light: false);
            string body = font.Key + "/" + PanelPickers.BodyKey, search = font.Key + "/" + PanelPickers.SearchKey;
            List<string> Shown() => [.. deck.Layout!.PickerCells(deck.Layout.LineOf(body)!).Where(cell => cell.Kind == PickerCellKind.Option).Select(cell => cell.Item.Text)];
            var all = Shown();
            Assert.True(all.Count > 1);
            var box = deck.Layout!.Controls(deck.Layout.LineOf(search)!).Single().Hit;
            Assert.Equal(PanelHitKind.Text, box.Kind);
            Assert.Equal(EditDeckAction.None, EditDeckClickTests.Click(deck, box).Action);
            foreach (var c in "geo")
            {
                _ = deck.Message(deck.Hwnd, WM.WM_CHAR, (WPARAM)(nuint)c, 0);
            }
            Assert.Equal(["Georgia"], Shown());
            // Left by Enter: its words stay, filtering; nothing sent.
            Assert.Equal(EditDeckAction.None, deck.Message(deck.Hwnd, WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_RETURN, 0).Happened.Action);
            Assert.Equal(["Georgia"], Shown());
            // Typed in again and Escape: every choice back.
            Assert.Equal(EditDeckAction.None, EditDeckClickTests.Click(deck, box).Action);
            _ = deck.Message(deck.Hwnd, WM.WM_KEYDOWN, (WPARAM)(nuint)VK.VK_ESCAPE, 0);
            Assert.Equal(all, Shown());
        });
    }

    /// <summary>
    /// §671: a picker opened at the page's foot scrolls the page till the whole of it is in sight (at once here, animations off:
    /// the glide's end); the page's preview stands first, bare.
    /// </summary>
    [UiFact]
    public void APickerOpenedPastTheFootIsBroughtIntoSight()
    {
        WindowShots.OnHiddenDesktop("inline-reveal", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var (_, _, content) = Appearance();
            Assert.True(content.Of(PanelPage.Appearance)!.Rows[0] is { Kind: PanelRowKind.Preview, Bare: true });
            var pattern = Picker(content, S.SettingsAppearanceStylesPattern, tab: 0);
            deck.Memory = new PanelMemory();
            deck.Memory.Show(PanelPage.Appearance);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Appearance);
            deck.Land(PanelPage.Appearance, pattern.Key, light: false);
            var head = deck.Layout!.Controls(deck.Layout.LineOf(pattern.Key)!).Single(each => each.Hit.Kind == PanelHitKind.Head).Hit;
            Assert.Equal(EditDeckAction.None, EditDeckClickTests.Click(deck, head).Action);
            var now = deck.Layout!;
            var span = now.Spans[pattern.Key];
            Assert.True(span.Height > now.P(PanelMetrics.OneLine) * 2, "the picker is open");
            Assert.True(span.Y >= now.Scroll - 0.5f && span.Bottom <= now.Scroll + now.Viewport.Height + 0.5f,
                $"{span} not in sight of {now.Scroll}..{now.Scroll + now.Viewport.Height}");
        });
    }

    /// <summary>Every picker's cells inside its card and apart, at a flyout's width and at a narrow one.</summary>
    [Theory]
    [InlineData(580f)]
    [InlineData(360f)]
    public void EveryPickersCellsStandInItsCard(float wideDip)
    {
        var (_, _, content) = Appearance();
        var page = content.Of(PanelPage.Appearance)!;
        var pickers = PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Picker).ToList();
        // §672: Mode's too; §682 Animations'.
        Assert.Equal(20, pickers.Count);
        var open = new HashSet<string>(pickers.Select(row => row.Key));
        foreach (var tab in page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Rows)
        {
            var shown = new HashSet<string>(open) { tab.Key };
            var layout = new PanelLayout(new FakePanelText(), 1.5f, new PanelInput(0, content, PanelPage.Appearance, shown), new Vector2(wideDip * 1.5f, 4000));
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Kind == PanelRowKind.PickerBody))
            {
                var cells = layout.PickerCells(line);
                Assert.NotEmpty(cells);
                foreach (var cell in cells)
                {
                    Assert.True(cell.Rect.X >= line.Rect.X && cell.Rect.Right <= line.Rect.Right + 0.5f && cell.Rect.Y >= line.Rect.Y && cell.Rect.Bottom <= line.Rect.Bottom + 0.5f,
                        $"{line.Row.Key}: {cell.Item.Text} {cell.Rect} outside {line.Rect}");
                }
                for (var a = 0; a < cells.Count; a++)
                {
                    for (var b = a + 1; b < cells.Count; b++)
                    {
                        var (x, y) = (cells[a].Rect, cells[b].Rect);
                        Assert.False(x.X < y.Right - 0.5f && y.X < x.Right - 0.5f && x.Y < y.Bottom - 0.5f && y.Y < x.Bottom - 0.5f, $"{line.Row.Key}: {cells[a].Item.Text} over {cells[b].Item.Text}");
                    }
                }
            }
        }
    }

    /// <summary>A text measure of fixed widths: half the size a letter (as Segoe UI's average is), one line tall.</summary>
    /// <summary>
    /// §676, the person (2026-11-12): "if required searching should also be possible in those inline dropdowns. make sure that
    /// you handle layout shift properly there. especially when it is a setting that is on the bottom of a page, i dont want it
    /// moving around then". A lookup (a time zone: its field's finder) is an inline picker too: its search box over what it
    /// finds, the one in force ticked first before anything is typed; at most eight, what it says under them (nothing found,
    /// searching); a click on one its own pick, its place among them. Its card as tall whatever it finds: none, two, twenty.
    /// </summary>
    [Fact]
    public void ALookupIsItsSearchOverWhatItFindsAndNeverMoves()
    {
        var finder = new Zones([new OptionFound("Asia/Tokyo"), new OptionFound("Europe/Zurich") { Chosen = true }]);
        var field = new FlyoutItem(7, "Time zone", Control: new FlyoutField("", "Search", Finder: finder));
        var item = new FlyoutItem(6, "Time zone", Children: [field], Value: new FlyoutValue("Europe/Zurich"));
        var under = new FlyoutItem(8, "Below", Control: new FlyoutControl.Toggle(true));
        var page = PanelRows.Page(PanelPage.Widget, "", "", [item, under]);
        var picker = page.Rows[0];
        Assert.Equal((PanelRowKind.Picker, "Europe/Zurich"), (picker.Kind, picker.Value));
        Assert.Equal([PanelRowKind.Text, PanelRowKind.PickerBody], picker.Rows.Select(row => row.Kind));
        var search = picker.Rows[0].Key;
        var body = picker.Rows[1].Key;
        Assert.True(PanelPickers.LooksUp(picker.Rows[0]));
        Assert.Equal(body, PanelPickers.BodyOf(search));
        PanelLayout Laid(PanelFinds? finds, string typed = "") =>
            new(new FakePanelText(), 1.5f, new PanelInput(0, new SettingsContent([page]), PanelPage.Widget, new HashSet<string> { picker.Key },
                Searches: new Dictionary<string, string> { [search] = typed }, Finds: finds is null ? null : new Dictionary<string, PanelFinds> { [search] = finds }), new Vector2(580 * 1.5f, 4000));

        // Before it is asked: what it offers, the one in force first and ticked; each its own pick.
        var before = Laid(null);
        var cells = before.PickerCells(before.LineOf(body)!);
        Assert.Equal(["Europe/Zurich", "Asia/Tokyo"], cells.Select(cell => cell.Item.Text));
        Assert.Equal([true, false], cells.Select(cell => cell.Item.Checked == true));
        Assert.All(cells, cell => Assert.Equal((PanelHitKind.Act, field.Id, body), (cell.Hit.Kind, cell.Hit.Id, cell.Hit.Key)));
        Assert.Equal([0, 1], cells.Select(cell => cell.Hit.Index));

        // Twenty found: §682 two columns, every one of them, those past its height scrolled to (nothing said of them). Nothing
        // found for words typed: the field's words for it. Searching: said under them.
        var many = Laid(new PanelFinds([.. Enumerable.Range(0, 20).Select(at => new OptionFound("Zone " + at))], null));
        var manyLine = many.LineOf(body)!;
        var manyCells = many.PickerCells(manyLine);
        Assert.True(manyCells.Count(cell => cell.Kind == PickerCellKind.Option) >= 2 * PickerMetrics.FoundMost);
        Assert.Equal(2, manyCells.Where(cell => cell.Kind == PickerCellKind.Option).Select(cell => cell.Rect.X).Distinct().Count());
        Assert.True(many.PickerOverflow(manyLine) > 0);
        Assert.DoesNotContain(manyCells, cell => cell.Kind == PickerCellKind.More);
        var none = Laid(new PanelFinds([], null), typed: "qqq");
        Assert.Equal((PickerCellKind.More, S.DockMenuNoMatch), none.PickerCells(none.LineOf(body)!).Select(cell => (cell.Kind, cell.Item.Text)).Single());
        var asking = Laid(new PanelFinds([], S.SettingsWidgetSearching), typed: "zur");
        Assert.Equal(S.SettingsWidgetSearching, asking.PickerCells(asking.LineOf(body)!).Single().Item.Text);

        // Never a step: the body and the row under it stand where they stood, whatever it finds.
        var layouts = new[] { before, many, none, asking };
        Assert.Single(layouts.Select(layout => layout.LineOf(body)!.Rect).Distinct());
        Assert.Single(layouts.Select(layout => layout.LineOf(page.Rows[1].Key)!.Rect.Y).Distinct());
    }

    /// <summary>
    /// §682 (the person, 2026-11-12: "language inline dropdown does not show the languages when opening, only when searching"):
    /// a lookup that needs no letters and asks no service (the languages) lists what it finds for nothing typed before its
    /// search is asked, the one in force first.
    /// </summary>
    [Fact]
    public void TheLanguagesShowBeforeAnythingIsTyped()
    {
        var finder = new KarpoMenu.LanguageFinder(null, ["en-US"]);
        var rows = PanelFinds.Before(finder).Rows;
        Assert.Equal(finder.Names.Count, rows.Count);
        Assert.True(rows[0].Chosen);
    }

    /// <summary>A lookup that offers what it is given and finds nothing more.</summary>
    private sealed class Zones(IReadOnlyList<OptionFound> offered) : OptionFinder
    {
        public override IReadOnlyList<OptionFound> Offered => offered;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<OptionFound>>([.. offered.Where(found => found.Title.Contains(query, StringComparison.OrdinalIgnoreCase))]);

        public override string NoneFound(string query) => S.DockMenuNoMatch;
    }

    private sealed class FakePanelText : IPanelText
    {
        public float Width(string text, float px, bool strong = false) => text.Length * px * 0.5f;

        public float Tall(string text, float px, float width) => px * 1.4f;

        public float LinesDip(LookShown shows, float scale) => 120;
    }
}
