// edit, §536: a look's preview in the settings panel, apart from any window (Karpo.Dock\LookPreview.cs): every level that
// edits a layout's look has its preview at its top, and nowhere else — §562 a widget's own look none; each look picks its sample by one rule; a value changed is
// another picture, anything else the same one; a colour being mixed (§523) reaches it; what a sample shows stands in for
// this PC's and the network's; and the preview is an image to a screen reader, nothing the keys stop on.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class LookPreviewTests
{
    private const float Scale = 1.5f;

    private static readonly BarSpot Bar = new(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };

    private static readonly Look Windows = AddListPictures.LookFor(dark: true);

    /// <summary>The Appearance page as the dock builds it (Dock.PanelOf) of <paramref name="config"/>.</summary>
    private static PanelPageView LookPage(Config config) =>
        PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(config, Bar, true, [], LooksScope.Appearance));

    /// <summary>The previews of <paramref name="page"/>, at any depth.</summary>
    private static List<PanelRow> Previews(PanelPageView page) => [.. PanelRows.All(page.Rows).Where(row => row.Kind == PanelRowKind.Preview)];

    /// <summary>§592: the look <paramref name="page"/>'s one preview shows (the Appearance page's: the whole of it).</summary>
    private static LookShown Look(PanelPageView page) => Assert.Single(Previews(page)).Shows!;

    /// <summary>
    /// §576 (the bug: §575's flyout cut its samples to the bar's 48 DIP, the Taskbar page's menu, at its foot, a blank box):
    /// every preview of a page in its flyout, the Taskbar page's menu and the Appearance's four, stands inside what its samples'
    /// layer is cut to, which is the flyout's page.
    /// </summary>
    [Fact]
    public void EveryPreviewInAFlyoutStandsInsideItsCut()
    {
        var content = SettingsPanelTests.Content();
        foreach (var page in new[] { PanelPage.Taskbar, PanelPage.Appearance })
        {
            var open = new HashSet<string>(PanelRows.All(content.Of(page)!.Rows).Select(row => row.Key), StringComparer.Ordinal);
            var layout = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, page, open), new System.Numerics.Vector2(0, 100_000));
            var (size, (left, top, right, bottom)) = LookPreviews.Cut(layout);
            var lines = layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Shows is not null).ToList();
            Assert.NotEmpty(lines);
            Assert.All(lines, line =>
            {
                var box = layout.OnPanel(layout.PreviewBox(line));
                Assert.True(box.X >= left && box.Y >= top && box.Right <= size.X - right && box.Bottom <= size.Y - bottom,
                    $"{page}'s preview {line.Row.Key} at {box} is cut away: the layer {size}, insets {left}, {top}, {right}, {bottom}");
            });
        }
    }

    [Fact]
    public void EveryLevelThatEditsALookHasItsPreviewAtItsTopAndNowhereElse()
    {
        var config = MenuCorpus.Fresh();
        var place = new OptionPlace(config, Dark: true);
        List<PanelPageView> pages = [LookPage(config), .. MenuCorpus.All().Where(menu => menu.Settings).Select(menu => PanelRows.Page(PanelPage.Widget, menu.Name, "", menu.Items))];
        var looks = 0;
        foreach (var page in pages)
        {
            var all = PanelRows.All(page.Rows).ToList();
            // §551: a section that shows its look (the Taskbar page's Taskbar menu: the menu itself; §583 an Appearance
            // section, no level since) its preview right under its heading.
            var sections = page.Rows.Select((row, at) => (Row: row, At: at)).Where(each => each.Row.Kind == PanelRowKind.Heading && each.Row.Item.Shows is not null).ToList();
            foreach (var (heading, at) in sections)
            {
                Assert.Equal(PanelRowKind.Preview, page.Rows[at + 1].Kind);
                Assert.Same(heading.Item.Shows, page.Rows[at + 1].Shows);
                Assert.EndsWith("/" + heading.Text + "/" + PanelRows.PreviewKey, page.Rows[at + 1].Key, StringComparison.Ordinal);
            }
            // §585: an expander that shows its look (the Taskbar page's Taskbar menu) its preview first in it.
            var expanders = all.Where(row => row.Kind == PanelRowKind.Expander && row.Item.Shows is not null).ToList();
            foreach (var expander in expanders)
            {
                Assert.Equal(PanelRowKind.Preview, expander.Rows[0].Kind);
                Assert.Same(expander.Item.Shows, expander.Rows[0].Shows);
                Assert.Equal(expander.Key + "/" + PanelRows.PreviewKey, expander.Rows[0].Key);
            }
            // §592: a tab strip whose tabs show their look (the Appearance's) its preview the page's, §671 first on it, bare.
            var strips = page.Rows.Select((row, at) => (Row: row, At: at)).Where(each => each.Row.Kind == PanelRowKind.Tabs && each.Row.Rows[0].Item.Shows is not null).ToList();
            foreach (var (strip, _) in strips)
            {
                Assert.Equal(PanelRowKind.Preview, page.Rows[0].Kind);
                Assert.True(page.Rows[0].Bare);
                Assert.Same(strip.Rows[0].Item.Shows, page.Rows[0].Shows);
                Assert.All(strip.Rows, tab => Assert.Same(strip.Rows[0].Item.Shows, tab.Item.Shows));
                Assert.Equal(PanelTabs.StripOf(strip.Key) + "/" + PanelRows.PreviewKey, page.Rows[0].Key);
            }
            // Nothing else is one: one preview a look.
            Assert.Equal(sections.Count + expanders.Count + strips.Count, all.Count(row => row.Kind == PanelRowKind.Preview));
            looks += sections.Count + expanders.Count + strips.Count;
        }
        // §592: the Appearance page's one preview, of the whole look, over its tabs; §567 no widget has a look.
        Assert.Single(Previews(LookPage(config)));
        Assert.True(looks >= 2, $"{looks} looks");
        _ = place;
    }

    /// <summary>
    /// §592 (the person, 2026-11-09: "for the appearance settings page i want only one preview now, then there should be tabs
    /// underneath for the four different sections / groups of styles that you can change"): Palette (§610 its reset beside it), the one preview,
    /// then one tab strip of the four parts — §626 Taskbar, Taskbar Widgets, Flyouts, Flyout Widgets —, each tab its name and icon,
    /// its editor's rows in it (its own Text and Colors headings left out: the tab names them), every one there once, in its
    /// order, keyed in its tab ("Appearance/@Taskbar Widgets/Font"). No heading, nothing folds. Laid out: the strip, under it the
    /// chosen tab's rows alone — the first where none is chosen, any other its key in the memory —, each a card of the page.
    /// </summary>
    [Fact]
    public void TheAppearancePageIsOnePreviewOverFourTabs()
    {
        var config = MenuCorpus.Fresh();
        var layout = config.ActiveLayout(MenuCorpus.Display);
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Background, Karpo.Core.Paint.Parse("#2D7D9A")));
        var page = LookPage(config);
        var all = PanelRows.All(page.Rows).ToList();
        // §671: the preview first; §613 Mode and §577 Palette, §672 each its picker inline, §682 Animations after them; §673 the palette's reset the tab
        // strip's: no row of its own.
        Assert.Equal([PanelRowKind.Preview, PanelRowKind.Picker, PanelRowKind.Picker, PanelRowKind.Picker, PanelRowKind.Tabs], page.Rows.Select(row => row.Kind));
        Assert.Equal((PanelRowKind.Action, S.SettingsLookReset), (page.Rows[4].Pair?.Kind, page.Rows[4].Pair?.Text));
        Assert.DoesNotContain(all, row => row.Kind is PanelRowKind.Expander or PanelRowKind.Heading or PanelRowKind.Group);
        var strip = page.Rows[^1];
        Assert.Equal(PanelPage.Appearance + "/" + PanelTabs.Key, strip.Key);
        Assert.Equal(LookParts.All.Select(LookParts.Name), strip.Rows.Select(tab => tab.Text));
        // §626: the person's words, the Taskbar the Taskbar page's own.
        Assert.Equal(["Taskbar", "Taskbar Widgets", "Flyouts", "Flyout Widgets"], strip.Rows.Select(tab => tab.Text));
        Assert.Equal(S.DockEditTaskbar, LookParts.Name(LookPart.Taskbar));
        var place = new OptionPlace(config, Dark: true) { Layout = layout };
        foreach (var (part, tab) in LookParts.All.Zip(strip.Rows))
        {
            Assert.Equal((PanelRowKind.Tab, PanelPage.Appearance + "/" + PanelTabs.Mark + LookParts.Name(part), LookParts.Glyph(part)), (tab.Kind, tab.Key, tab.Glyph));
            Assert.True(PanelTabs.IsTab(tab.Key));
            // §613: a value's reset beside its control, in its row.
            var editor = LookRows.Editor(place, Looks.Section(config, part), LookRows.SectionKey(part))
                .Where(each => !each.Beside && each.Kind != OptionRowKind.Heading).Select(each => each.Title).ToList();
            // §670: an inline picker's own rows (its search, its choices, its hex box) its; a font's weight in its family's card.
            var rows = PanelRows.All(tab.Rows).Where(each => !each.Key.Contains("/#", StringComparison.Ordinal)).ToList();
            Assert.Equal(editor, rows.Select(each => each.Text));
            Assert.All(rows, each => Assert.StartsWith(tab.Key + "/", each.Key, StringComparison.Ordinal));
            Assert.All(rows, each => Assert.False(PanelTabs.IsTab(each.Key)));
            // A landing on one of its rows chooses its tab.
            Assert.All(rows, each => Assert.Equal([tab.Key], PanelRows.PathTo(page.Rows, each.Key)));
        }
        Assert.Contains(all, each => each.Pair is not null);

        // Laid out: the strip, then the chosen tab's rows alone, each a card of the page; the others' nowhere.
        var content = SettingsPanelTests.Content();
        var shown = content.Of(PanelPage.Appearance)!;
        var tabs = shown.Rows.Single(row => row.Kind == PanelRowKind.Tabs);
        for (var chosen = -1; chosen < tabs.Rows.Count; chosen++)
        {
            var open = new HashSet<string>(StringComparer.Ordinal);
            if (chosen >= 0)
            {
                open.Add(tabs.Rows[chosen].Key);
            }
            var laid = new PanelLayout(new SettingsPanelTests.Words(), Scale, new PanelInput(0, content, PanelPage.Appearance, open), new Vector2(0, 100_000));
            var at = Math.Max(0, chosen);
            Assert.Equal(at, laid.Chosen(tabs));
            var lines = laid.Blocks.SelectMany(block => block.Lines).ToList();
            var line = Assert.Single(lines, each => each.Row.Kind == PanelRowKind.Tabs);
            // §671: the page's preview first, over Mode and Palette too.
            Assert.Equal(PanelRowKind.Preview, lines[0].Row.Kind);
            Assert.Equal(tabs.Rows[at].Rows.Select(row => row.Key), lines.Skip(lines.IndexOf(line) + 1).Where(each => !each.Inner).Select(each => each.Row.Key));
            Assert.All(lines.Skip(lines.IndexOf(line) + 1), each => Assert.False(each.Inner));
            // Each tab a thing to click, side by side from the cards' start, as tall as the strip; the chosen one the keys' stop alone.
            // §673: the palette's reset at the strip's end, after them.
            var hits = laid.Controls(line).Where(each => each.Hit.Kind == PanelHitKind.Tab).ToList();
            Assert.Equal(tabs.Rows.Select(tab => tab.Key), hits.Select(each => each.Hit.Key));
            Assert.All(hits, each => Assert.Equal((PanelHitKind.Tab, line.Rect.Y, line.Rect.Height), (each.Hit.Kind, each.Rect.Y, each.Rect.Height)));
            Assert.Equal(line.Rect.X, hits[0].Rect.X);
            Assert.All(hits.Zip(hits.Skip(1)), pair => Assert.True(pair.Second.Rect.X >= pair.First.Rect.Right));
            Assert.Equal([hits[at].Hit], laid.Stops().Where(stop => stop.Kind == PanelHitKind.Tab));
            var middle = laid.OnPanel(hits[2].Rect).Center;
            Assert.Equal(hits[2].Hit, laid.HitIn(new Vector2(middle.X, middle.Y)));
            Assert.Equal(hits[2].Hit.Key, laid.TabsAlong(hits[at].Hit)[2].Key);
            // To a screen reader a tab list named for its page, its four tabs in it, the chosen one selected.
            var items = DeckUia.Things(laid, bar: false).Where(thing => thing.Hit.Kind == PanelHitKind.Tab).ToList();
            Assert.Equal(LookParts.All.Select(LookParts.Name), items.Select(item => item.Name));
            Assert.All(items, item => Assert.Equal((UiaRole.TabItem, UiaPattern.Select), (item.Role, item.Pattern)));
            Assert.Equal(Enumerable.Range(0, items.Count).Select(each => (bool?)(each == at)), items.Select(item => item.On));
            var tree = DeckUia.Tree(laid, new TerraFX.Interop.Windows.RECT { left = 0, top = 0, right = 1140, bottom = 100_000 }, PanelHit.None, bar: false);
            var list = Assert.Single(Nodes(tree.Root), node => node.Role == UiaRole.Tab);
            Assert.Equal(S.SettingsAppearanceHeader, list.Name);
            Assert.Equal(LookParts.All.Select(LookParts.Name), list.Children.Select(node => node.Name));
            Assert.All(list.Children, node => Assert.Equal((UiaRole.TabItem, true), (node.Role, node.Focusable)));
            Assert.Equal(at, list.Children.ToList().FindIndex(node => node.On == true));
        }
        static IEnumerable<UiaNode> Nodes(UiaNode node) => node.Children.SelectMany(Nodes).Prepend(node);
    }

    /// <summary>
    /// §592: the tab chosen is kept as its key in the panel's memory, one of a strip at a time: a click or a landing chooses it,
    /// the strip's other tab let go; another strip's, an expander's and a group's left as they were.
    /// </summary>
    [Fact]
    public void TheTabChosenIsRememberedOneAStrip()
    {
        var memory = new PanelMemory();
        Assert.Equal(PanelTabs.Mark, "@");
        const string bar = "Appearance/@Taskbar", widgets = "Appearance/@Taskbar Widgets", elsewhere = "Other/@Taskbar", expander = "Taskbar/Behavior/Taskbar menu", group = "Widget/~What it shows";
        memory.Toggle(expander);
        memory.Toggle(group);
        memory.Open([elsewhere, bar]);
        Assert.True(memory.IsOpen(bar));
        memory.Open([widgets]);
        Assert.False(memory.IsOpen(bar));
        Assert.True(memory.IsOpen(widgets) && memory.IsOpen(elsewhere) && memory.IsOpen(expander) && memory.IsOpen(group));
        // A landing's path to a row in a tab (PanelRows.PathTo: the tab, no strip): that tab chosen.
        memory.Open([bar]);
        Assert.True(memory.IsOpen(bar));
        Assert.False(memory.IsOpen(widgets));
        Assert.Equal("Appearance", PanelTabs.StripOf(widgets));
        Assert.False(PanelTabs.IsTab("Appearance/@Taskbar/Background"));
        Assert.False(PanelTabs.IsTab("Appearance/" + PanelTabs.Key));
    }

    [Fact]
    public void EachLookPicksItsSampleByOneRule()
    {
        var config = MenuCorpus.Fresh();
        var page = LookPage(config);
        // §592: the Appearance's one preview the whole look — the bar, its widgets' cards, a folder open over its face, its
        // outline round its lines —; the taskbar menu's the menu.
        Assert.Equal(LookSample.Whole, LookPreview.SampleOf(Look(page)));
        Assert.Equal(S.DockEditPreviewOf(S.SettingsAppearanceHeader), LookPreview.NameOf(Look(page)));
        var menu = new LookShown(config, config.ActiveLayout(MenuCorpus.Display)) { Menu = config.BarMenuEntries };
        Assert.Equal(LookSample.Menu, LookPreview.SampleOf(menu));
        Assert.Equal(S.DockEditPreviewOf(S.SettingsBarMenuHeader), LookPreview.NameOf(menu));
    }

    /// <summary>
    /// §613: a sample is drawn once, in the mode the taskbar is in — Windows' for System, else the Mode said, whatever Windows
    /// is in —, wearing that half of every colour pair; in high contrast Windows' own look whatever the Mode says.
    /// </summary>
    [Fact]
    public void ASampleIsDrawnInTheTaskbarsModeWearingItsHalf()
    {
        var config = MenuCorpus.Fresh();
        var bar = Looks.Section(config, LookPart.Taskbar);
        Assert.True(Looks.WritePaint(bar, Tone.Background, true, Karpo.Core.Paint.Parse("#1F2A44")));
        Assert.True(Looks.WritePaint(bar, Tone.Background, false, Karpo.Core.Paint.Parse("#E8D9B5")));
        foreach (var mode in LookRows.Modes)
        {
            _ = Looks.WriteMode(config, mode);
            foreach (var windowsDark in new[] { true, false })
            {
                var worn = AddListPictures.LookFor(windowsDark).Wearing(config);
                var dark = mode == TaskbarMode.System ? windowsDark : mode == TaskbarMode.Dark;
                Assert.Equal(dark, worn.Dark);
                Assert.Equal(Karpo.Core.Paint.Parse(dark ? "#1F2A44" : "#E8D9B5"), worn.Worn.Taskbar.Background);
            }
            var high = (Windows with { HighContrast = true }).Wearing(config);
            Assert.True(high.Colors.IsAutomatic);
        }
    }

    /// <summary>§613: the mode's badge at the room's top right, 8 DIP in, 28 round, whatever the room's size and the scale.</summary>
    [Fact]
    public void TheModesBadgeStandsAtTheRoomsTopRight()
    {
        foreach (var (size, scale) in new[] { (new Vector2(780, 300), 1f), (new Vector2(1170, 450), 1.5f), (new Vector2(400, 200), 2f) })
        {
            var badge = LookPreview.BadgeOf(size, scale);
            Assert.Equal((MathF.Round(28 * scale), MathF.Round(28 * scale)), (badge.Width, badge.Height));
            Assert.Equal(size.X - MathF.Round(8 * scale), badge.X + badge.Width, 3);
            Assert.Equal(MathF.Round(8 * scale), badge.Y, 3);
        }
    }

    /// <summary>A measurer whose folder's lines are <paramref name="Lines"/> DIP tall: the plan the open folder would lay out.</summary>
    private sealed record Measured(float Lines) : IPanelText
    {
        public float Width(string text, float px, bool strong = false) => new SettingsPanelTests.Words().Width(text, px, strong);

        public float Tall(string text, float px, float width) => new SettingsPanelTests.Words().Tall(text, px, width);

        public float LinesDip(LookShown shows, float scale) => Lines;
    }

    /// <summary>
    /// §571: a sample's room holds what it draws, whole — the folder's lines as tall as the open folder lays them out, never a
    /// guessed height: §592 the whole look's 12 over the folder, its lines, the gap down to its face's bar, the bar, the gap
    /// under it. Taller lines (a bigger text size, another font), a taller room; the panel's line and the room in it as tall as
    /// that.
    /// </summary>
    [Fact]
    public void ASamplesRoomHoldsTheFoldersLinesWhole()
    {
        var config = MenuCorpus.Fresh();
        var page = LookPage(config);
        var flyouts = Look(page);
        var bar = LookPreview.BarDip(flyouts.Layout);
        foreach (var tall in new[] { 120f, 280f, 520f })
        {
            var measure = new Measured(tall);
            // §671: the bar at the room's foot, nothing under it.
            Assert.Equal(LookPreview.AroundDip + tall + LookPreview.OpenGapDip + bar, LookPreview.BoxDip(flyouts, measure, Scale));
            // Laid out on the panel: the preview's line and its room as tall as the lines ask.
            var content = new SettingsContent([page]);
            var layout = new PanelLayout(measure, Scale, new PanelInput(0, content, PanelPage.Appearance, new HashSet<string>()), new System.Numerics.Vector2(1140, 2400));
            var line = layout.Blocks.SelectMany(block => block.Lines).Single(each => each.Row.Shows is not null);
            // §671: bare, its room the whole of it.
            Assert.Equal(MathF.Round(LookPreview.BoxDip(flyouts, measure, Scale) * Scale, MidpointRounding.AwayFromZero), line.Rect.Height);
            Assert.True(layout.PreviewBox(line).Height >= MathF.Round(LookPreview.BoxDip(flyouts, measure, Scale) * Scale) - 1);
        }
    }

    [Fact]
    public void ALookValueChangedIsAnotherPictureAnythingElseTheSame()
    {
        var config = MenuCorpus.Fresh();
        var layout = config.ActiveLayout(MenuCorpus.Display);
        var size = new System.Numerics.Vector2(780, 108);
        string Key(LookPart part) => LookPreview.KeyOf(Look(LookPage(config)), Windows, Scale, size);
        var before = LookParts.All.ToDictionary(part => part, Key);
        // The same values made again: the same picture.
        Assert.Equal(before, LookParts.All.ToDictionary(part => part, Key));
        // The layout's name is nothing a sample shows.
        layout.Name = "Elsewhere";
        Assert.Equal(before, LookParts.All.ToDictionary(part => part, Key));
        // A colour of Taskbar widgets: another picture.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Text, Karpo.Core.Paint.Parse("#FFB900")));
        Assert.NotEqual(before[LookPart.Widgets], Key(LookPart.Widgets));
        // And a font, and a pattern of the bar's.
        var colored = Key(LookPart.Widgets);
        Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.Widgets), new TextStyle("Georgia")));
        Assert.NotEqual(colored, Key(LookPart.Widgets));
        var font = Key(LookPart.Taskbar);
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Taskbar), Pattern.Dots));
        Assert.NotEqual(font, Key(LookPart.Taskbar));
        // The lines in folders, and the flyouts' outline.
        var lines = Key(LookPart.FolderWidgets);
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.FolderWidgets), Tone.Background, Karpo.Core.Paint.Parse("#2D7D9A")));
        Assert.NotEqual(lines, Key(LookPart.FolderWidgets));
        var flyouts = Key(LookPart.Flyouts);
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, Karpo.Core.Paint.Parse("#E74856")));
        Assert.NotEqual(flyouts, Key(LookPart.Flyouts));
        // Another scale, another width, Windows in another mode: another picture.
        var shows = Look(LookPage(config));
        var now = LookPreview.KeyOf(shows, Windows, Scale, size);
        Assert.NotEqual(now, LookPreview.KeyOf(shows, Windows, 1, size));
        Assert.NotEqual(now, LookPreview.KeyOf(shows, Windows, Scale, size with { X = 600 }));
        Assert.NotEqual(now, LookPreview.KeyOf(shows, AddListPictures.LookFor(dark: false), Scale, size));
    }

    [Fact]
    public void AColourBeingMixedReachesThePreview()
    {
        // §523: the dock tries a colour being mixed on a copy of the config (Dock.MenuMixed: Apply(next, save: false)), whose
        // rows the panel is made of again: its preview reads the colour tried, the config as it was its own.
        var config = MenuCorpus.Fresh();
        var size = new System.Numerics.Vector2(780, 108);
        var before = LookPreview.KeyOf(Look(LookPage(config)), Windows, Scale, size);
        var trial = config.Clone();
        var row = OptionRows.Find(BarLooks.Rows(trial, Bar, true, LooksScope.Appearance), LookRows.ColourKey(LookRows.SectionKey(LookPart.Widgets), Tone.Background))!;
        var mixed = new Argb(0xCC, 0x12, 0x8A, 0x3C);
        Assert.True(row.SetColor!(mixed, false));
        var tried = Look(LookPage(trial));
        Assert.Same(trial, tried.Config);
        Assert.Contains("128A3C", tried.Values(), StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(before, LookPreview.KeyOf(tried, Windows, Scale, size));
        // Nothing of it in the config the mix began on, until it is kept.
        Assert.Equal(before, LookPreview.KeyOf(Look(LookPage(config)), Windows, Scale, size));
    }

    /// <summary>
    /// §634 (the person, 2026-11-09: "the background while a folder is open preview does not properly show"): the whole look's
    /// sample lays the Flyouts' backdrop (§623) over its desktop as the dock lays it over every display while a folder is open —
    /// the very rule (FolderBackdrop.Of, a folder open at rest) in the look the sample is drawn in: none by default, the half of
    /// the taskbar's mode, none in high contrast; a swatch tried on (§622: a copy of the config worn, its rows made again) is in
    /// the preview's look and another picture.
    /// </summary>
    [Fact]
    public void TheFolderBackdropIsInThePreviewAsItStandsLive()
    {
        var config = MenuCorpus.Fresh();
        Look Drawn(Config of, bool windowsDark = true) => AddListPictures.LookFor(windowsDark).Wearing(Look(LookPage(of)).Config);
        Assert.Null(LookPreview.BackdropOf(Drawn(config)));
        var size = new System.Numerics.Vector2(780, 300);
        var none = LookPreview.KeyOf(Look(LookPage(config)), Windows, Scale, size);

        var flyouts = Looks.Section(config, LookPart.Flyouts);
        Assert.True(Looks.WritePaint(flyouts, Tone.Backdrop, dark: true, new Karpo.Core.Paint(new Argb(0x80, 0, 0, 0))));
        Assert.Equal(new Argb(0x80, 0, 0, 0), LookPreview.BackdropOf(Drawn(config)));
        Assert.NotEqual(none, LookPreview.KeyOf(Look(LookPage(config)), Windows, Scale, size));
        // The light half unset: none where the taskbar is light.
        Assert.Null(LookPreview.BackdropOf(Drawn(config, windowsDark: false)));
        _ = Looks.WriteMode(config, TaskbarMode.Dark);
        Assert.Equal(new Argb(0x80, 0, 0, 0), LookPreview.BackdropOf(Drawn(config, windowsDark: false)));
        Assert.Null(LookPreview.BackdropOf((Windows with { HighContrast = true }).Wearing(config)));

        // A swatch rested on: worn on a copy (Dock.TryOn), the preview made of it lays that one; the person's config untouched.
        var worn = config.Clone();
        var row = OptionRows.Find(BarLooks.Rows(worn, Bar, true, LooksScope.Appearance), LookRows.ColourKey(LookRows.SectionKey(LookPart.Flyouts), Tone.Backdrop))!;
        var other = row.Tiles.ToList().FindIndex(tile => !tile.Plain && tile.Color is { } color && color != new Argb(255, 0, 0, 0));
        Assert.True(other >= 0 && row.Set!(other));
        Assert.Equal(row.Tiles[other].Color!.Value with { A = 0x80 }, LookPreview.BackdropOf(Drawn(worn)));
        Assert.Equal(new Argb(0x80, 0, 0, 0), LookPreview.BackdropOf(Drawn(config)));
    }

    [Fact]
    public void ASampleStandsInForThisPcAndTheNetwork()
    {
        var config = MenuCorpus.Fresh();
        var layout = config.ActiveLayout(MenuCorpus.Display);
        // A section's stand-ins: the bar's own buttons and a folder wearing the clock, a clock and a calendar of made-up
        // events in it; no pin, no target, no file.
        Assert.Equal([BarKind.Start, BarKind.Search, BarKind.TaskView, BarKind.WindowsSettings, BarKind.SnippingTool, BarKind.FolderList], LookPreview.BarStandIns().Select(item => item.Kind));
        Assert.Equal([FolderFaces.ClockId], LookPreview.ClockFolder().Folder!.Faces.Select(face => face!.Kind));
        Assert.Equal(LookPreview.LineStandIns().Select(item => item.Kind), LookPreview.PanelOf().Folder!.Cells.Select(cell => cell.Island.Kind));
        _ = layout;
        // While a sample is made: the clock still at 9:41 today, the calendar two made-up events of no account; after, as before.
        var made = CalendarIsland.Made;
        using (new LookStand())
        {
            Assert.Equal(DateTime.Today.AddHours(9).AddMinutes(41), Clock.Now());
            var store = CalendarIsland.Made;
            Assert.NotNull(store);
            Assert.All(store!.Sources, source => Assert.IsType<ListCalendar>(source));
            var events = store.Sources.SelectMany(source => source.EventsIn(DateTime.Today, DateTime.Today.AddDays(2), TimeZoneInfo.Local)).ToList();
            Assert.Equal([S.DockEditPreviewMeeting, S.DockEditPreviewLunch, S.DockEditPreviewMeeting], events.Select(each => each.Title));
            Assert.All(events, each => Assert.Null(each.Location));
        }
        Assert.Same(made, CalendarIsland.Made);
        Assert.InRange((Clock.Now() - DateTime.Now).Duration(), TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void APreviewIsAnImageNothingToPressOrStopOn()
    {
        var config = MenuCorpus.Fresh();
        var page = LookPage(config);
        var content = new SettingsContent([page]);
        var words = new SettingsPanelTests.Words();
        var layout = new PanelLayout(words, Scale, new PanelInput(0, content, PanelPage.Appearance, new HashSet<string>()), new System.Numerics.Vector2(1140, 1200));
        var line = layout.Blocks.SelectMany(block => block.Lines).Single(each => each.Row.Shows is not null);
        // §671: first on the page, bare — no card, no caption —, as tall as its sample asks, its room the whole line.
        Assert.False(line.Inner);
        Assert.True(line.Row.Bare);
        Assert.Equal(MathF.Round(LookPreview.BoxDip(line.Row.Shows!, words, Scale) * Scale, MidpointRounding.AwayFromZero), line.Rect.Height);
        var box = layout.PreviewBox(line);
        Assert.Equal(line.Rect, box);
        Assert.Equal(MathF.Round(LookPreview.BoxDip(line.Row.Shows!, words, Scale) * Scale, MidpointRounding.AwayFromZero), box.Height, 1);
        // No control, no stop of the keys, nothing a click there does.
        Assert.Empty(layout.Controls(line));
        Assert.DoesNotContain(layout.Stops(), stop => stop.Key == line.Row.Key);
        var at = layout.OnPanel(box);
        Assert.Equal(PanelHitKind.Page, layout.HitIn(new System.Numerics.Vector2(at.X + (at.Width / 2), at.Y + (at.Height / 2))).Kind);
        // A screen reader is given an image, named for the look it shows.
        var image = DeckUia.Things(layout, bar: false).First(thing => thing.Hit.Kind == PanelHitKind.Preview);
        Assert.Equal((UiaRole.Image, S.DockEditPreviewOf(S.SettingsAppearanceHeader)), (image.Role, image.Name));

        Assert.Equal(at, layout.RectOf(image.Hit));
    }
}
