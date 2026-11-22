// §628 (the person, 2026-11-09: "when adding a layout it should not open a dropdown but should open a new page where you also
// have a preview of the different layout presets. there should also be one that starts just blank"): Add a layout's page,
// apart from any window (Karpo.Core\BarLooks.cs AddLayoutRows, Karpo.Dock\SettingsPanel*.cs). Held here:
//
//  - **The page lists every preset, Blank first**: each a tile — its name, its icon, what it is for under it —, picturing
//    the layout it makes (LookShown.Itself); Blank's bar holds nothing.
//  - **What is pictured is what is made**: a pick makes exactly the pictured layout (its type, size, icon, name and every
//    widget where it stands), one layout more and nothing else changed, shown on the display; Blank an empty one.
//  - **Under the Layouts page's entry**: "Add a layout" a link to the page (no list); the page has no entry of its own, the
//    Layouts entry stands selected, its flyout hangs from it; its way back at its top, standing as the tiles scroll.
//  - **The keys and a screen reader**: the way back the first stop, then each tile in order; a screen reader hears "Back,
//    Add a layout", each tile a button named for its preset, what it is for its help.
//  - **Laid out**: tiles one to a line across the cards, each its picture over its words, the picture a desktop strip of
//    1280 by 144 DIP scaled to its width.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class AddLayoutPageTests
{
    private const float Scale = 1.5f;

    private static float Px(float dip) => MathF.Round(dip * Scale, MidpointRounding.AwayFromZero);

    private static BarSpot Bar() => new(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };

    /// <summary>The Add a layout page as the dock builds it (Dock.PanelOf), for the test laptop (LayoutPresetsTests.Laptop).</summary>
    private static PanelPageView Page(Config config, List<OptionPick>? picks = null, LicenseStatus? license = null) =>
        PanelRows.Page(PanelPage.AddLayout, PanelRows.SideOf(PanelPage.AddLayout), PanelRows.GlyphOf(PanelPage.AddLayout),
            TheDock.LooksItems(config, Bar(), dark: true, picks ?? [], LooksScope.AddLayout, license, machine: LayoutPresetsTests.Laptop));

    /// <summary>The panel's pages with the Layouts page and Add a layout's page under it (SettingsPanelTests.Content's and this one).</summary>
    private static SettingsContent Content(Config config) =>
        new([.. SettingsPanelTests.Content().Pages, Page(config)]);

    /// <summary>Each preset, Blank first, a tile: its name, its icon, what it is for, and the layout it makes as its picture.</summary>
    [Fact]
    public void ThePageListsEveryPresetBlankFirstEachATilePicturingItsLayout()
    {
        var config = MenuCorpus.Fresh();
        var page = Page(config);
        Assert.Equal(LayoutPresets.BlankId, LayoutPresets.All[0].Id);
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Title), page.Rows.Select(row => row.Text));
        Assert.All(page.Rows, row => Assert.Equal(PanelRowKind.Pick, row.Kind));
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Blurb), page.Rows.Select(row => row.Second));
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Glyph), page.Rows.Select(row => row.Glyph));
        Assert.All(page.Rows, row => Assert.True(row.Picture is { Itself: true }, row.Text));
        // Nothing else of the page: no preview of its own, nothing that drops a list.
        Assert.All(page.Rows, row => Assert.Null(row.Shows));
        // Blank's bar holds nothing; the rest each their widgets.
        Assert.Empty(page.Rows[0].Picture!.Layout.Islands);
        Assert.All(page.Rows.Skip(1), row => Assert.NotEmpty(row.Picture!.Layout.Islands));
        // Every key once.
        Assert.Equal(page.Rows.Count, page.Rows.Select(row => row.Key).Distinct().Count());
    }

    /// <summary>
    /// What a tile pictures is what its pick makes: one layout more, at the end, the pictured one's type, size, icon, name and
    /// every widget where it stands, shown on the display; every other layout as it was. Blank's empty.
    /// </summary>
    [Fact]
    public void APickMakesExactlyThePicturedLayoutBlankAnEmptyOne()
    {
        foreach (var preset in LayoutPresets.All)
        {
            var config = MenuCorpus.Fresh();
            var before = Layouts(config);
            List<OptionPick> picks = [];
            var page = Page(config, picks);
            var tile = page.Rows.Single(row => row.Text == preset.Title);
            var pictured = tile.Picture!.Layout;
            var pick = OptionMenu.PickOf(picks, tile.Item.Id)!;
            Assert.Equal(OptionPickKind.Act, pick.Kind);
            Assert.True(OptionMenu.ApplyRow(OptionRows.Find(BarLooks.Rows(config, Bar(), dark: true, LooksScope.AddLayout, machine: LayoutPresetsTests.Laptop), pick.Key), pick), preset.Id);

            Assert.Equal(before.Count + 1, config.Layouts.Count);
            Assert.Equal(before, Layouts(config).Take(before.Count));
            var made = config.Layouts[^1];
            Assert.Equal(made.Id, config.ActiveLayout(MenuCorpus.Display).Id);
            Assert.Equal((pictured.Name, pictured.Icon, pictured.Type, pictured.Size), (made.Name, made.Icon, made.Type, made.Size));
            Assert.Equal(Widgets(pictured), Widgets(made));
            if (preset == LayoutPresets.Blank)
            {
                Assert.Empty(made.Islands);
                Assert.Equal(LayoutPresets.Blank.Title, made.Name);
            }
        }

        // Each layout as the file writes it.
        static List<string> Layouts(Config config) => [.. System.Text.Json.Nodes.JsonNode.Parse(config.ToJson())!["layouts"]!.AsArray().Select(node => node!.ToJsonString())];

        static IEnumerable<string> Widgets(TaskbarLayout layout) => layout.Islands.Select(island =>
            string.Join(',', island.Kind, island.Section, island.Pin?.Target, island.Folder?.Face.Kind, island.Folder?.Cells.Count, island.WindowsSearch?.ShowBox));
    }

    /// <summary>The picture's words as the preview's key reads them: the same for the same layout made again, its widgets' new ids aside.</summary>
    [Fact]
    public void APicturesKeyIsTheLayoutsWidgetsNotTheirIds()
    {
        var config = MenuCorpus.Fresh();
        var once = Page(config).Rows.Select(row => row.Picture!.Values()).ToList();
        var again = Page(config).Rows.Select(row => row.Picture!.Values()).ToList();
        Assert.Equal(once, again);
        Assert.Equal(once.Count, once.Distinct().Count());
    }

    /// <summary>
    /// "Add a layout" leads to its page: a link (no list), its button alone at the Layouts page's foot, its chevron pointing on;
    /// the link's way in is the page itself, under the Layouts page's entry.
    /// </summary>
    [Fact]
    public void AddALayoutIsALinkToThePage()
    {
        var content = SettingsPanelTests.Content();
        var layouts = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, PanelPage.Layouts), PanelLayout.SizeOf(Scale, 1548));
        var add = layouts.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsLayoutsAdd);
        Assert.Equal((PanelRowKind.Link, true, true), (add.Row.Kind, add.Row.Foot, add.Row.Adds));
        Assert.Null(PanelRows.PopupOf(add.Row.Item, add.Row.Item.Id));
        Assert.Equal([PanelHitKind.Link], layouts.Controls(add).Select(each => each.Hit.Kind));
        Assert.Equal(OptionLinks.Opens(OptionLink.AddLayout), add.Row.Item.Opens);
        Assert.Equal(new EditLanding(PanelPage.AddLayout), EditEntries.Landing(EditEntries.Of(OptionLink.AddLayout)));
        Assert.Equal((PanelPage.Layouts, true), (PanelRows.EntryOf(PanelPage.AddLayout), PanelRows.IsUnder(PanelPage.AddLayout)));
        // §646: and an account's Mail folders under the Accounts page's.
        Assert.Equal((PanelPage.Accounts, true), (PanelRows.EntryOf(PanelPage.MailFolders), PanelRows.IsUnder(PanelPage.MailFolders)));
        Assert.All(Enum.GetValues<PanelPage>().Where(page => page is not (PanelPage.AddLayout or PanelPage.MailFolders)), page => Assert.Equal(page, PanelRows.EntryOf(page)));
    }

    /// <summary>
    /// The page under the Layouts entry: no entry of its own on the bar, the Layouts entry selected; its way back at its top
    /// (one plate the whole of which goes back, the arrow at its start, the title after it), the page scrolling under it; the
    /// tiles one to a line across the cards, each its picture over its words, a click on any of it its pick.
    /// </summary>
    [Fact]
    public void ThePageStandsUnderTheLayoutsEntryItsWayBackAtItsTop()
    {
        var config = MenuCorpus.Fresh();
        var content = Content(config);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, PanelPage.AddLayout), PanelLayout.SizeOf(Scale, 1548));
        Assert.True(layout.IsOpen);
        Assert.DoesNotContain(layout.Header, part => part.Kind == PanelHitKind.Nav && part.Page == PanelPage.AddLayout);
        Assert.True(layout.Opened(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Layouts)));
        Assert.False(layout.Opened(new PanelHit(PanelHitKind.Nav, Page: PanelPage.Taskbar)));
        // The way back: 16 in, a control tall, the arrow 12 in from its start, the title 8 after it; the page under it.
        Assert.Equal(new RectF(Px(16), Px(16), layout.FlyoutSize.X - (2 * Px(16)), Px(32)), layout.BackHead);
        Assert.Equal(layout.BackHead.X + Px(12), layout.BackArrow.X);
        Assert.Equal(layout.BackArrow.Right + Px(8), layout.BackTitle.X);
        Assert.Equal(layout.BackHead.Bottom, layout.Viewport.Y);
        Assert.Equal(new PanelHit(PanelHitKind.Back), layout.HitIn(new System.Numerics.Vector2(layout.BackHead.Right - 2, layout.BackHead.Y + 2)));
        Assert.Equal(layout.BackHead, layout.RectOf(new PanelHit(PanelHitKind.Back)));
        // No way back on a page of its own entry.
        var own = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, PanelPage.Layouts), PanelLayout.SizeOf(Scale, 1548));
        Assert.True(own.BackHead.IsEmpty);
        Assert.DoesNotContain(new PanelHit(PanelHitKind.Back), own.Stops());

        var lines = layout.Blocks.SelectMany(block => block.Lines).ToList();
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Title), lines.Select(line => line.Row.Text));
        var cards = (Left: Px(16), Right: layout.FlyoutSize.X - Px(16));
        for (var at = 0; at < lines.Count; at++)
        {
            var line = lines[at];
            Assert.True(line.Tile);
            // One to a line, across the cards, 4 apart.
            Assert.Equal((cards.Left, cards.Right), (line.Rect.X, line.Rect.Right));
            if (at > 0)
            {
                Assert.Equal(lines[at - 1].Rect.Bottom + Px(4), line.Rect.Y);
            }
            // Its picture 8 in, a 1280 by 144 DIP strip at its width; its words under it; its preview's room the picture.
            var picture = layout.PictureOf(line);
            Assert.Equal((line.Rect.X + Px(8), line.Rect.Y + Px(8), line.Rect.Width - Px(16)), (picture.X, picture.Y, picture.Width));
            Assert.Equal(MathF.Round(picture.Width * 144 / 1280), picture.Height);
            Assert.Equal(picture, layout.PreviewBox(line));
            var words = layout.WordsOf(line);
            Assert.Equal((picture.Bottom, line.Rect.Bottom), (words.Rect.Y, words.Rect.Bottom));
            Assert.True(words.Rect.Height >= Px(PanelMetrics.TileTwoLines), line.Row.Text);
            // A click anywhere on it its pick.
            Assert.Equal([(PanelHitKind.Act, line.Row.Item.Id, line.Rect)], layout.Controls(line).Select(each => (each.Hit.Kind, each.Hit.Id, each.Rect)));
        }
    }

    /// <summary>
    /// The keys: the way back the flyout's first stop, then each tile in order. A screen reader: the way back a button saying
    /// where it goes back from, at the pane's head; each tile a button named for its preset, what it is for its help.
    /// </summary>
    [Fact]
    public void TheKeysAndAScreenReaderGoBackFirstThenEachTile()
    {
        var config = MenuCorpus.Fresh();
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, Content(config), PanelPage.AddLayout), PanelLayout.SizeOf(Scale, 1548));
        var stops = layout.Stops().Where(stop => !PanelLayout.OnBar(stop.Kind)).ToList();
        Assert.Equal(PanelHitKind.Back, stops[0].Kind);
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Title), stops.Skip(1).Select(stop => layout.Row(stop.Key)!.Text));
        Assert.All(stops.Skip(1), stop => Assert.Equal(PanelHitKind.Act, stop.Kind));

        var said = DeckUia.Things(layout, bar: false);
        Assert.Equal((S.DockPageBack + ", " + S.SettingsLayoutsAdd, UiaRole.Button, UiaPattern.Invoke), (said[0].Name, said[0].Role, said[0].Pattern));
        Assert.Equal(LayoutPresets.All.Select(preset => (preset.Title, UiaRole.Button, UiaPattern.Invoke, true)), said.Skip(1).Select(thing => (thing.Name, thing.Role, thing.Pattern, thing.Enabled)));
        var tree = DeckUia.Tree(layout, new TerraFX.Interop.Windows.RECT { right = 900, bottom = 900 }, PanelHit.None, bar: false);
        Assert.Equal(S.SettingsLayoutsAdd, tree.Root.Name);
        Assert.Equal(S.DockPageBack + ", " + S.SettingsLayoutsAdd, tree.Root.Children[0].Name);
        var tiles = tree.Root.Children.Skip(1).SelectMany(group => group.Children).ToList();
        Assert.Equal(LayoutPresets.All.Select(preset => preset.Blurb), tiles.Select(node => node.Help));
    }

    /// <summary>Past the licence's layouts every tile greyed, why under its name, a stop all the same, and nothing hit by the pointer.</summary>
    [Fact]
    public void PastTheLicenceEveryTileIsGreyedSayingWhy()
    {
        var config = MenuCorpus.Fresh();
        while (LicenseGate.Layouts(SettingsPanelTests.Free, config.Layouts.Count) is null)
        {
            Assert.True(BarLooks.AddShown(config, Bar(), LayoutPresets.Blank, SettingsPanelTests.Free, LayoutPresetsTests.Laptop));
        }
        var page = Page(config, license: SettingsPanelTests.Free);
        var why = LicenseGate.Layouts(SettingsPanelTests.Free, config.Layouts.Count);
        Assert.All(page.Rows, row => Assert.Equal((true, why), (row.Greyed, row.Second)));
        var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, new SettingsContent([page]), PanelPage.AddLayout), PanelLayout.SizeOf(Scale, 1548));
        var line = layout.Blocks.SelectMany(block => block.Lines).First();
        Assert.Contains(layout.Controls(line)[0].Hit, layout.Stops());
        Assert.Equal(PanelHitKind.Page, layout.HitIn(new System.Numerics.Vector2(line.Rect.X + 4, layout.OnPanel(line.Rect).Y + 4)).Kind);
    }
}
