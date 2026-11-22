// edit, §574 (the person, 2026-11-08: "the appearance says something like "x from karpo" or something like that, there should
// not be an additional appearance level above. it should just be the settings that can also be preset with the apply preset
// option."; docs\widgets.md §574), apart from any window:
//
//  - **Every value a setting with a value.** No row of the Appearance offers a "Default (…)", none says where its value
//    comes from; each shows the value in force — Karpo's palette's where the file says none.
//  - **§577 a palette worn holds them.** Karpo's own is a palette, the first; a new config wears it; a value picked is kept
//    with the palette worn, which stays worn; a designed palette's one reset puts all its values back as designed; §613 a
//    value its design draws otherwise its own reset beside it (AppearanceModeTests).
//  - **The panel draws them as any row**: no counts at a section's head, no muted name, nothing to a screen reader of where a
//    value comes from; §613 a colour one button, of the mode the taskbar is in; its picker under it.

using Karpo.Dock;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed class AppearanceValuesTests
{
    /// <summary><paramref name="config"/>'s Appearance page's rows, as the dock builds them, Windows in its dark mode or not.</summary>
    private static IReadOnlyList<OptionRow> Rows(Config config, bool dark = true) => LookRows.Rows(config, config.DefaultLayout, dark);

    /// <summary>The Appearance page as the panel reads it, its menu's picks into <paramref name="picks"/>.</summary>
    private static PanelPageView Page(Config config, List<OptionPick>? picks = null) =>
        PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, "", OptionMenu.Rows(Rows(config), picks ?? []));

    /// <summary>§592: the page's tab strip.</summary>
    private static PanelRow Tabs(PanelPageView page) => page.Rows.Single(row => row.Kind == PanelRowKind.Tabs);

    /// <summary>Section <paramref name="part"/>'s row ending <paramref name="ends"/> ("font", "accent"), §613 of the mode shown (Windows' <paramref name="dark"/>).</summary>
    private static OptionRow Value(Config config, LookPart part, string ends, bool dark = true) =>
        OptionRows.Find(Rows(config, dark), LookRows.SectionKey(part) + "." + ends)!;

    private static OptionRow Palette(Config config) => OptionRows.Find(Rows(config), LookRows.PaletteRow)!;

    /// <summary>"Default (": what a value of a default would start its words with.</summary>
    private static string DefaultWords => S.SettingsValueDefault("\u0001").Split('\u0001')[0];

    [Fact]
    public void EveryValueIsASettingWithAValueKarposPalettesWhereTheFileSaysNone()
    {
        var config = MenuCorpus.Fresh();
        Assert.Null(config.Appearance);
        var all = OptionRows.Flat(Rows(config)).ToList();
        // No "Default (…)" anywhere: not a choice, not a tile, not a summary; no Reset all.
        Assert.DoesNotContain(all, row => row.Choices.Any(choice => choice.StartsWith(DefaultWords, StringComparison.Ordinal))
            || row.Tiles.Any(tile => tile.Name.StartsWith(DefaultWords, StringComparison.Ordinal) || tile.Glyph == OptionGlyphs.Automatic)
            || row.Summary?.StartsWith(DefaultWords, StringComparison.Ordinal) == true);
        // §613: every value its palette's design: no value's reset; the palette's one, the one action.
        Assert.Equal([LookRows.ResetRow], all.Where(row => row.Kind == OptionRowKind.Action).Select(row => row.Key));
        // The sections say nothing of their values.
        Assert.All(Rows(config).Where(row => row.Kind == OptionRowKind.Heading), row => Assert.Null(row.Summary));
        // The value in force, each: Karpo's own of the mode shown.
        Assert.Equal(KarpoLook.Card, Value(config, LookPart.Widgets, "background").Color);
        Assert.Equal(KarpoLook.LightCard, Value(config, LookPart.Widgets, "background", dark: false).Color);
        Assert.Equal(KarpoLook.Ground, Value(config, LookPart.Taskbar, "background").Color);
        Assert.Equal(KarpoLook.AccentLight2, Value(config, LookPart.Widgets, "accent").Color);
        Assert.Equal(ToneRules.WindowsText(dark: true), Value(config, LookPart.FolderWidgets, "text").Color);
        // Karpo draws no outline: None, its first choice, ticked.
        var outline = Value(config, LookPart.Flyouts, "outline");
        Assert.Equal((0, S.SettingsAppearanceSeparatorStyleNone), (outline.Chosen, outline.Tiles[0].Name));
        var font = Value(config, LookPart.Widgets, "font");
        Assert.Equal(default(TextStyle), font.ChoiceFaces[font.Chosen]);
        Assert.Equal(0, Value(config, LookPart.Widgets, "pattern").Chosen);
        Assert.Equal(S.SettingsAppearanceSeparatorStyleNone, Value(config, LookPart.Widgets, "pattern").Choices[0]);
        Assert.Equal(KarpoLook.Blur, Value(config, LookPart.Taskbar, "blur").Value);
    }

    /// <summary>
    /// A value picked is written as it is, Karpo's own too (no "unset" to fall back to), kept with the palette worn, which
    /// §577 stays worn; its one reset puts it back as designed, in both modes.
    /// </summary>
    [Fact]
    public void APickIsWrittenAsItIsAndKeptWithThePaletteWorn()
    {
        var config = MenuCorpus.Fresh();
        var karpo = LookPalettes.All[0];
        Assert.Equal(LookPalettes.KarpoId, karpo.Id);
        // A new config wears Karpo; wearing it changes nothing.
        Assert.Same(karpo, LookPalettes.Worn(config));
        Assert.Equal(0, Palette(config).Chosen);
        Assert.False(LookPalettes.Wear(config, karpo));

        // Karpo's own font picked: written, Karpo still worn as designed.
        var font = Value(config, LookPart.Widgets, "font");
        Assert.True(font.Set!(font.Chosen));
        Assert.Equal(default(TextStyle).Text, config.WornValues!.Widgets!.Font);
        Assert.False(LookPalettes.Changed(config, karpo));

        // Another font: Karpo still worn, changed.
        Assert.True(Value(config, LookPart.Widgets, "font").Set!(0));
        Assert.Same(karpo, LookPalettes.Worn(config));
        Assert.True(LookPalettes.Changed(config, karpo));
        Assert.Equal(0, Palette(config).Chosen);

        // A palette worn: its every value; one changed: still worn, kept with it.
        var paper = LookPalettes.Of(LookPalettes.PaperId)!;
        Assert.True(Palette(config).Set!(Array.IndexOf(LookPalettes.Choices, paper)));
        Assert.Same(paper, LookPalettes.Worn(config));
        Assert.Equal(paper.Dark.Widgets.Background, Looks.Of(config, dark: true).Widgets.Background);
        Assert.True(Value(config, LookPart.Taskbar, "blur").Set!(60));
        Assert.Same(paper, LookPalettes.Worn(config));
        Assert.Equal(60, config.Appearance![LookPalettes.PaperId]!.Taskbar!.Blur);

        // Karpo worn again, then its reset: Karpo's own throughout, in both modes.
        Assert.True(Palette(config).Set!(0));
        Assert.True(OptionRows.Find(Rows(config), LookRows.ResetRow)!.Set!(0));
        Assert.Equal(Looks.Of((Appearance?)null, dark: true), Looks.Of(config, dark: true));
        Assert.Equal(Looks.Of((Appearance?)null, dark: false), Looks.Of(config, dark: false));
        Assert.Equal(60, config.Appearance![LookPalettes.PaperId]!.Taskbar!.Blur);
    }

    /// <summary>
    /// The panel: a section's head says nothing of its values; every row's name in the text's ink (none muted); to a screen
    /// reader nothing of where a value comes from; §613 a colour one button at the controls' end, named for its row alone.
    /// </summary>
    [Fact]
    public void ThePanelDrawsEveryValueAsAnyRow()
    {
        var config = MenuCorpus.Fresh();
        _ = Value(config, LookPart.Widgets, "font").Set!(3);
        var page = Page(config);
        Assert.All(Tabs(page).Rows, tab => Assert.Null(tab.Value));
        var widgets = PanelPage.Appearance + "/" + PanelTabs.Mark + LookParts.Name(LookPart.Widgets);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), 1.5f, new PanelInput(0, new SettingsContent([page]), PanelPage.Appearance, new HashSet<string> { widgets }), PanelLayout.SizeOf(1.5f, 3200));
        var lines = layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Key.StartsWith(widgets + "/", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(lines);
        foreach (var dark in new[] { true, false })
        {
            var theme = FlyoutTheme.For(SettingsPanelRender.LookFor(dark));
            Assert.All(lines.Where(line => !line.Row.Greyed), line => Assert.Equal(theme.Text, PanelPainter.NameInk(line.Row, theme)));
        }
        Assert.All(lines, line => Assert.Null(line.Row.Second));

        // §613: a colour, nothing beside it while it is its palette's; §670 its picker inline, its head the whole line.
        var background = lines.Single(line => line.Row.Text == S.SettingsAppearanceCustomBackgroundHeader);
        Assert.Null(background.Row.Pair);
        var controls = layout.Controls(background);
        var button = Assert.Single(controls);
        Assert.Equal((PanelHitKind.Head, background.Row.Key), (button.Hit.Kind, button.Hit.Key));
        Assert.Equal(background.Rect, button.Rect);

        // A screen reader: its row's name alone, no mode; no help saying where a value comes from.
        var name = DeckUia.Things(layout, bar: false).Single(thing => thing.Hit.Key == background.Row.Key).Name;
        Assert.Equal(background.Row.Text, name);
        var tree = DeckUia.Tree(layout, new TerraFX.Interop.Windows.RECT { left = 0, top = 0, right = 1000, bottom = 3200 }, PanelHit.None);
        Assert.DoesNotContain(Nodes(tree.Root), node => node.Help is { } help && help.Contains("Karpo", StringComparison.Ordinal));

        static IEnumerable<UiaNode> Nodes(UiaNode node) => [node, .. node.Children.SelectMany(Nodes)];
    }

    /// <summary>
    /// §577, the person (2026-11-09): "the light mode color selectors dont work" — §613 one button a colour, of the mode shown,
    /// in either mode. Every colour's button stands where it is drawn — the place a press opens its picker under
    /// (EditDeck.OnScreen, the stage map: the layout's RectOf) and the line it is on (LineOf: the keys' focus, a landing) — and
    /// finds its level (PanelRows.Find, what the popup shows); a value's reset beside it (§613) its own place too.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void EveryColourOpensItsPickerUnderItsButton(bool dark)
    {
        var config = MenuCorpus.Fresh();
        // One value changed, so a reset stands beside a colour.
        Assert.True(Value(config, LookPart.Widgets, "background", dark).Set!(3));
        var items = OptionMenu.Rows(Rows(config, dark), []);
        var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, "", items);
        // §592: each tab chosen in turn, its colours laid.
        var (count, resets) = (0, 0);
        foreach (var tab in Tabs(page).Rows)
        {
            var layout = new PanelLayout(new SettingsPanelTests.Words(), 1.5f, new PanelInput(0, new SettingsContent([page]), PanelPage.Appearance, new HashSet<string> { tab.Key }), PanelLayout.SizeOf(1.5f, 6000));
            foreach (var line in layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Kind == PanelRowKind.Picker && PanelPickers.Colours(line.Row.Item)))
            {
                count++;
                foreach (var (hit, rect) in layout.Controls(line))
                {
                    resets += hit.Kind == PanelHitKind.Act ? 1 : 0;
                    Assert.Equal(layout.OnPanel(rect), layout.RectOf(hit));
                    Assert.Same(line, layout.LineOf(hit.Key));
                    var on = layout.OnPanel(rect);
                    Assert.Equal(hit, layout.HitIn(new System.Numerics.Vector2(on.X + (on.Width / 2), on.Y + (on.Height / 2))));
                    // §670: its head opens its level in its card.
                    if (hit.Kind == PanelHitKind.Head)
                    {
                        var level = PanelRows.Find(items, nameof(PanelPage.Appearance), hit.Key);
                        Assert.True(level is { Children.Count: > 0 }, $"{hit.Key}: no picker");
                    }
                }
            }
        }
        Assert.True(count >= 10, $"{count} colours on the page");
        Assert.Equal(1, resets);
    }

    /// <summary>
    /// §583, the person (2026-11-09): "in the style dropdown show if a style has custom user configuration inside it". Each
    /// palette whose values change it marked "Changed" after its name in Palette's list; the one worn marked in the row's box
    /// too, and to a screen reader. The mark follows the values: none at first, Custom none while it holds none; a value
    /// changed marks the palette worn alone; another palette worn keeps the first one's mark; a value said that is the
    /// design's own changes nothing, so no mark; the palette's reset takes its mark away.
    /// </summary>
    [Fact]
    public void EachPaletteHoldingThePersonsEditsIsMarkedInTheListAndTheBox()
    {
        var config = MenuCorpus.Fresh();
        var palettes = LookPalettes.Choices;
        var graphite = palettes.Single(palette => palette.Id == LookPalettes.GraphiteId);
        string[] Marked() => [.. palettes.Where((_, at) => Palette(config).ChoiceMarks[at].Length > 0).Select(palette => palette.Id)];
        PanelRow Box() => PanelRows.All(Page(config).Rows).Single(row => row.Text == S.SettingsLookPalette && row.Kind == PanelRowKind.Picker);
        Assert.Empty(Marked());
        Assert.Null(Box().ValueMark);

        // A value changed with Karpo's own worn: Karpo's alone, in the list (each choice's words after its name) and the box.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Background, Karpo.Core.Paint.Parse("#2D7D9A")));
        Assert.Equal([LookPalettes.KarpoId], Marked());
        var box = Box();
        Assert.Equal(S.SettingsLookChanged, box.ValueMark);
        Assert.Equal([S.SettingsLookChanged, .. Enumerable.Repeat<string?>(null, palettes.Length - 1)], box.Item.Children!.Select(choice => choice.Value?.Text));
        Assert.Equal(S.SettingsLookPaletteKarpo, box.Value);

        // Graphite worn: as designed, no mark in the box; Karpo's kept in the list.
        Assert.True(Palette(config).Set!(Array.IndexOf(palettes, graphite)));
        Assert.Equal([LookPalettes.KarpoId], Marked());
        Assert.Null(Box().ValueMark);
        // A value said that is Graphite's own changes nothing: no mark.
        var designed = graphite.Drawn(true)[LookPart.Taskbar].Background;
        _ = Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, true, designed);
        Assert.Equal([LookPalettes.KarpoId], Marked());
        // One that is not: Graphite's mark too.
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, true, Karpo.Core.Paint.Parse("#802D7D9A")));
        Assert.Equal([LookPalettes.KarpoId, LookPalettes.GraphiteId], Marked());
        Assert.Equal(S.SettingsLookChanged, Box().ValueMark);

        // Custom: none while it holds none; a value set, marked.
        Assert.True(Palette(config).Set!(Array.IndexOf(palettes, LookPalettes.Custom)));
        Assert.DoesNotContain(LookPalettes.CustomId, Marked());
        Assert.True(Value(config, LookPart.Widgets, "font").Set!(3));
        Assert.Contains(LookPalettes.CustomId, Marked());

        // Graphite's reset: its mark gone, the others' kept.
        Assert.True(LookPalettes.Reset(config, graphite));
        Assert.Equal([LookPalettes.KarpoId, LookPalettes.CustomId], Marked());

        // A screen reader hears the box's mark after the palette worn.
        Assert.True(Palette(config).Set!(0));
        var page = Page(config);
        var layout = new PanelLayout(new SettingsPanelTests.Words(), 1.5f, new PanelInput(0, new SettingsContent([page]), PanelPage.Appearance, new HashSet<string>()), PanelLayout.SizeOf(1.5f, 3200));
        var tree = DeckUia.Tree(layout, new TerraFX.Interop.Windows.RECT { left = 0, top = 0, right = 1000, bottom = 3200 }, PanelHit.None, bar: false);
        Assert.Contains(Nodes(tree.Root), node => node.Name.StartsWith(S.SettingsLookPalette, StringComparison.Ordinal) && node.Status == S.SettingsLookPaletteKarpo + ", " + S.SettingsLookChanged);

        static IEnumerable<UiaNode> Nodes(UiaNode node) => [node, .. node.Children.SelectMany(Nodes)];
    }

    /// <summary>The words of the layer above are gone, in every language: "Set here", "From Karpo", the counts, the resets.</summary>
    [Fact]
    public void TheWordsOfWhereAValueComesFromAreGone()
    {
        foreach (var key in new[] { "settings.value.setHere", "settings.from.karpo", "settings.resetTo.karpo", "settings.counts.karpo.some", "settings.counts.karpo.none", "dock.panel.reset", "settings.value.karpo" })
        {
            Assert.Null(S.Lookup(key));
        }
        Assert.Equal("Karpo", S.SettingsLookPaletteKarpo);
    }

    /// <summary>
    /// §673, the person (2026-11-12): "reset to default should be shown at the end of the tabs in the appearance section. aligned
    /// to the right". The palette worn's reset (§610: no row of its own) stands at the right end of the parts' tab strip, where
    /// the cards end, the tabs a gutter off it; the keys reach it after the tabs; greyed while the palette is as designed (said to
    /// be off), in force once changed, pressed: the palette as designed again; its tooltip and its name to a screen reader its
    /// whole name, "Reset Graphite to default". Custom has none: the tabs alone. Too narrow for its words beside the tabs, its
    /// icon alone.
    /// </summary>
    [Fact]
    public void ThePalettesResetStandsAtTheTabsEnd()
    {
        var config = MenuCorpus.Fresh();
        var graphite = LookPalettes.Of(LookPalettes.GraphiteId)!;
        const float scale = 1.5f;
        PanelLayout Laid(PanelPageView page, float widest = 0) =>
            new(new SettingsPanelTests.Words(), scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Appearance, new HashSet<string>()), PanelLayout.SizeOf(scale, 3200, widest));
        PanelLine Strip(PanelLayout layout) => layout.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Kind == PanelRowKind.Tabs);
        var rect = new TerraFX.Interop.Windows.RECT { left = 0, top = 0, right = 1000, bottom = 3200 };

        // Karpo worn as designed: the reset the strip's, greyed; no row of its own, nothing beside Palette.
        var page = Page(config);
        Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Kind == PanelRowKind.Action);
        // §671 the preview first; §672 Mode and Palette pickers inline, §682 Animations after them; §673 the reset the tab strip's.
        Assert.Equal([PanelRowKind.Preview, PanelRowKind.Picker, PanelRowKind.Picker, PanelRowKind.Picker, PanelRowKind.Tabs], page.Rows.Select(row => row.Kind));
        Assert.Null(page.Rows[2].Pair);
        Assert.All(page.Rows[4].Rows, tab => Assert.Null(tab.Pair));
        var reset = page.Rows[4].Pair!;
        Assert.Equal((PanelRowKind.Action, S.SettingsLookReset, true), (reset.Kind, reset.Text, reset.Greyed));
        var layout = Laid(page);
        var strip = Strip(layout);
        var controls = layout.Controls(strip);
        Assert.Equal((PanelHitKind.Act, reset.Key), (controls[^1].Hit.Kind, controls[^1].Hit.Key));
        var button = controls[^1].Rect;
        // Its words where every tab stands whole beside them, else its icon alone.
        Assert.Equal((strip.Rect.Right, layout.P(PanelMetrics.Control), layout.BesideWide(strip)), (button.Right, button.Height, button.Width));
        Assert.Contains(button.Width, new[] { layout.ActionWide(reset), layout.P(PanelMetrics.Control) });
        Assert.All(layout.TabsAt(strip), tab => Assert.False(tab.Cut && button.Width > layout.P(PanelMetrics.Control)));
        Assert.All(layout.TabsAt(strip), tab => Assert.True(tab.Rect.Right + layout.P(PanelMetrics.Gutter) <= button.X + 0.5f));
        // The keys reach it after the tabs; a screen reader names it whole, off.
        var stops = layout.Stops();
        Assert.Equal(stops.FindLastIndex(each => each.Kind == PanelHitKind.Tab) + 1, stops.IndexOf(controls[^1].Hit));
        Assert.Equal(S.SettingsLookResetPalette(LookPalettes.All[0].Name), layout.TipOf(controls[^1].Hit));
        var said = DeckUia.Things(layout, bar: false).Single(thing => thing.Hit == controls[^1].Hit);
        Assert.Equal((S.SettingsLookResetPalette(LookPalettes.All[0].Name), UiaRole.Button, false), (said.Name, said.Role, said.Enabled));
        var node = Nodes(DeckUia.Tree(layout, rect, PanelHit.None, bar: false).Root).Single(each => each.Name == said.Name);
        Assert.Equal((null, null, false), (node.Status, node.Help, node.Enabled));

        // Graphite worn and changed: in force, named for Graphite; pressed, Graphite as designed again, greyed.
        Assert.True(LookPalettes.Wear(config, graphite));
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Widgets), Tone.Background, dark: true, Karpo.Core.Paint.Parse("#1E3A5F")));
        List<OptionPick> picks = [];
        page = Page(config, picks);
        reset = page.Rows[4].Pair!;
        Assert.False(reset.Greyed);
        layout = Laid(page);
        controls = layout.Controls(Strip(layout));
        var middle = layout.OnPanel(controls[^1].Rect).Center;
        var hit = layout.HitIn(new System.Numerics.Vector2(middle.X, middle.Y));
        Assert.Equal(controls[^1].Hit, hit);
        Assert.Equal(S.SettingsLookResetPalette(graphite.Name), layout.TipOf(hit));
        Assert.Equal((S.SettingsLookResetPalette(graphite.Name), true), DeckUia.Things(layout, bar: false).Where(thing => thing.Hit == hit).Select(thing => (thing.Name, thing.Enabled)).Single());
        var pick = OptionMenu.PickOf(picks, hit.Id);
        Assert.Equal(LookRows.ResetRow, pick?.Key);
        Assert.True(OptionMenu.ApplyRow(OptionRows.Find(Rows(config), pick!.Key)!, pick));
        Assert.Same(graphite, LookPalettes.Worn(config));
        Assert.False(LookPalettes.Changed(config, graphite));
        Assert.True(Page(config).Rows[4].Pair!.Greyed);

        // Narrow (a flyout of 360 DIP): its icon alone, a 32 DIP square at the strip's end, the tabs still off it.
        layout = Laid(Page(config), widest: 360 * scale);
        strip = Strip(layout);
        controls = layout.Controls(strip);
        Assert.Equal((layout.P(PanelMetrics.Control), layout.P(PanelMetrics.Control), strip.Rect.Right), (controls[^1].Rect.Width, controls[^1].Rect.Height, controls[^1].Rect.Right));
        Assert.All(layout.TabsAt(strip), tab => Assert.True(tab.Rect.Right + layout.P(PanelMetrics.Gutter) <= controls[^1].Rect.X + 0.5f));

        // Custom: no reset; the tabs alone.
        Assert.True(LookPalettes.Wear(config, LookPalettes.Custom));
        page = Page(config);
        Assert.Null(page.Rows[4].Pair);
        Assert.DoesNotContain(PanelRows.All(page.Rows), row => row.Kind == PanelRowKind.Action);
        layout = Laid(page);
        Assert.All(layout.Controls(Strip(layout)), each => Assert.Equal(PanelHitKind.Tab, each.Hit.Kind));

        static IEnumerable<UiaNode> Nodes(UiaNode node) => [node, .. node.Children.SelectMany(Nodes)];
    }

    /// <summary>A question's answer, kept for the test to read: never Windows' dialog.</summary>
    private sealed class Answer(bool yes)
    {
        public List<DeleteQuestion> Asked { get; } = [];

        public bool Ask(DeleteQuestion question)
        {
            Asked.Add(question);
            return yes;
        }
    }

    /// <summary>
    /// §634, the person (2026-11-09): "reset to default for the style should show a system confirmation modal". The palette's
    /// reset asks first in Windows' dialog — "Reset Graphite?", what is lost, Reset and Cancel — as the dock acts the page's row
    /// (Dock.GoesAhead: the row read again from a copy of the config, as LooksPicked reads it; the answer injected, no dialog
    /// shown): Cancel keeps every change, Reset puts Graphite back as designed, one change. A value's own reset asks nothing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThePalettesResetAsksFirstAValuesDoesNot(bool yes)
    {
        var config = MenuCorpus.Fresh();
        var graphite = LookPalettes.Of(LookPalettes.GraphiteId)!;
        Assert.True(LookPalettes.Wear(config, graphite));
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Widgets), Tone.Background, dark: true, Karpo.Core.Paint.Parse("#1E3A5F")));
        var bar = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
        List<OptionPick> picks = [];
        var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, "", Karpo.Dock.Dock.LooksItems(config, bar, true, picks, LooksScope.Appearance));
        var reset = page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Pair!;
        var pick = OptionMenu.PickOf(picks, reset.Item.Id)!;
        Assert.Equal((OptionPickKind.Act, LookRows.ResetRow), (pick.Kind, pick.Key));

        // As LooksPicked: the row read again from a copy, asked, applied only on yes.
        var next = config.Clone();
        var row = OptionRows.Find(BarLooks.Rows(next, bar, true, LooksScope.Appearance), pick.Key);
        var answer = new Answer(yes);
        var ahead = Karpo.Dock.Dock.GoesAhead(row, pick, answer.Ask);
        var question = Assert.Single(answer.Asked);
        Assert.Equal(new DeleteQuestion("Reset Graphite?", "Your changes to Graphite will be lost.", "Reset", "Cancel", 0), question);
        Assert.Equal(yes, ahead);
        if (ahead)
        {
            Assert.True(OptionMenu.ApplyRow(row, pick));
        }
        Assert.Equal(!yes, LookPalettes.Changed(next, graphite));
        Assert.Same(graphite, LookPalettes.Worn(next));

        // A value's reset (Taskbar widgets' Background's) asks nothing: done at once.
        var value = OptionRows.Find(Rows(config), LookRows.ColourKey(LookRows.SectionKey(LookPart.Widgets), Tone.Background) + LookRows.ResetEnd)!;
        Assert.Null(value.Ask);
        var quiet = new Answer(false);
        Assert.True(Karpo.Dock.Dock.GoesAhead(value, new OptionPick(1, OptionPickKind.Act, Key: value.Key), quiet.Ask));
        Assert.Empty(quiet.Asked);
    }
}
