// Looks (Karpo.Core\Looks.cs, docs\widgets.md §507; §567, the person, 2026-11-08: "you should not be able to set widget level
// or folder level looks anymore. remove that feature completely. just one global look configuration. (rename it to
// Appearance) also not on folder level."). Held here:
//
//  - **One look**: Karpo's Appearance, its value else Karpo's own — colour by colour, the font (§556: family and weight as
//    one) too; every card on the bar the Taskbar widgets', every line the Widgets in folders', every open flyout outlined by
//    the Flyouts'. No layout, widget, folder or line has a look of its own, and the file's old keys are not read.
//  - **Each section says what it has**: the bar no text, the Flyouts their outline alone.
//  - **The file**: "appearance" at the top; read back the same; nothing written while nothing is said.
//  - **The palettes**: Karpo's own and its four designed looks, each light and dark, read as designed; §577 one worn, each
//    keeping its own values, a designed one's reset to its design, Custom the person's own.
//  - **No painter** (§517): Copy look and Paste look are gone.

using Karpo.Dock;
using Paint = Karpo.Core.Paint;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Core;

public sealed class LookTests
{
    private static readonly Argb Navy = new(255, 0x1E, 0x3A, 0x5F);
    private static readonly Argb Gold = new(255, 0xFF, 0xB9, 0x00);
    private static readonly Argb Rose = new(255, 0xC4, 0x28, 0x5A);

    private static Paint P(Argb colour) => new(colour);

    private static readonly TextStyle Sitka = new("Sitka Text");
    private static readonly TextStyle GeorgiaBold = new("Georgia", 700);

    /// <summary>A layout of a widget, a folder with a line in it, and another folder.</summary>
    private static (Config Config, BarItem Widget, BarItem Folder, CellItem Line) Bar()
    {
        var config = new Config();
        var widget = BarKind.NightLight.New();
        var folder = BarKind.FolderList.New();
        var line = ListKind.Battery.New();
        _ = folder.Folder!.Put(line);
        config.DefaultLayout.Islands = [widget, folder, BarKind.FolderList.New()];
        return (config, widget, folder, line);
    }

    /// <summary>§567: the one resolver: the Appearance's value, else Karpo's own; each value on its own, the bar's cards, the menus and the open flyouts alike.</summary>
    [Fact]
    public void TheAppearancesValueElseKarposOwn()
    {
        var (config, _, _, _) = Bar();
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Background, P(Navy)));
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Text, P(Gold)));
        Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.Widgets), Sitka));
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.FolderWidgets), Tone.Background, P(Rose)));
        var drawn = Looks.Of(config, dark: true);
        Assert.Equal((P(Navy), P(Gold), default(Paint), Sitka), (drawn.Widgets.Background, drawn.Widgets.Text, drawn.Widgets.Accent, drawn.Widgets.Type));
        Assert.Equal((P(Rose), default(Paint), default(TextStyle)), (drawn.FolderWidgets.Background, drawn.FolderWidgets.Text, drawn.FolderWidgets.Type));
        // Karpo's own where nothing is set: the bar's ground.
        Assert.Equal(KarpoLook.Paints(true).Taskbar, drawn.Taskbar);
        // The dock's looks: the bar's text in its widgets' font, the menus' the Widgets in folders'.
        var bar = Karpo.Tests.Folders.TestLooks.LookFor(dark: false).Wearing(config);
        Assert.Equal((Sitka, default(TextStyle)), (bar.Typeface, bar.MenuTypeface));
        Assert.Equal(default(TextStyle), bar.Flyout().Typeface);
        var menu = FlyoutTheme.For(bar).Base;
        Assert.Equal((Rose.R, Rose.G, Rose.B), (menu.R, menu.G, menu.B));
    }

    /// <summary>
    /// §567: one look Karpo-wide: every layout's bar wears it, a layout's copy and a preset carry none, and what an older file
    /// said of a layout's ("looks"), a widget's or a line's own ("look", "flyoutOutline") is not read: nothing of it stays.
    /// </summary>
    [Fact]
    public void ThereIsOneAppearanceAndNoOtherLevel()
    {
        var config = new Config();
        var second = config.DefaultLayout.CopyAs("Second");
        config.Layouts.Add(second);
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, P(Navy)));
        var windows = Karpo.Tests.Folders.TestLooks.LookFor(dark: true);
        Assert.Equal(windows.Wearing(config), windows.Wearing(config.Appearance));
        Assert.Equal(P(Navy), Looks.Of(config, dark: true).Taskbar.Background);
        Assert.DoesNotContain("\"looks\"", config.ToJson(), StringComparison.Ordinal);

        var old = Configs.Repaired("""
            { "layouts": [ { "id": "L", "name": "L", "looks": { "taskbar": { "background": "#FF0000" } },
              "islands": [ { "id": "w", "kind": "nightLight", "look": { "accent": "#0000FF", "flyoutOutline": "#00FF00" } },
                { "id": "f", "kind": "folderList", "look": { "background": "#00FF00" } } ] } ],
              "defaultLayoutId": "L" }
            """);
        Assert.Null(old.Appearance);
        Assert.Equal(KarpoLook.Paints(true), Looks.Of(old, dark: true));
        var json = old.ToJson();
        Assert.DoesNotContain("\"look", json, StringComparison.Ordinal);
        Assert.DoesNotContain("flyoutOutline", json, StringComparison.Ordinal);
        Assert.DoesNotContain("#FF0000", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// §514, §624: the Flyouts' outline is every open flyout's and every menu's — the one border of what opens over the desktop;
    /// the Widgets in folders have none to set (a folder's lines never outlined each), and none is drawn round them.
    /// </summary>
    [Fact]
    public void TheMenusAndTheFlyoutsWearTheFlyoutsOutline()
    {
        var config = new Config();
        var look = Karpo.Tests.Folders.TestLooks.LookFor(dark: false).Wearing(config);
        Assert.Equal((null, null), (look.Colors.FlyoutOutline, look.Flyout().Colors.FlyoutOutline));
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, P(Gold)));
        Assert.False(LookShorthand.Both(Looks.Section(config, LookPart.FolderWidgets), Tone.Outline, P(Rose)));
        look = Karpo.Tests.Folders.TestLooks.LookFor(dark: false).Wearing(config);
        Assert.Equal((Gold, Gold), (look.Colors.FlyoutOutline, look.Flyout().Colors.FlyoutOutline));
        Assert.Null(look.Flyout().Colors.CardOutline);
    }

    /// <summary>Each section says what it has: the bar a background, an outline and a pattern; the Flyouts their outline; the cards all seven, §624 a folder's lines all but an outline.</summary>
    [Fact]
    public void EachSectionSaysWhatItHas()
    {
        var config = new Config();
        var bar = Looks.Section(config, LookPart.Taskbar);
        Assert.False(LookShorthand.Both(bar, Tone.Text, P(Gold)));
        Assert.False(Looks.WriteFont(bar, GeorgiaBold));
        Assert.True(Looks.WritePattern(bar, Pattern.Dots));
        var panel = Looks.Section(config, LookPart.Flyouts);
        Assert.False(LookShorthand.Both(panel, Tone.Background, P(Gold)));
        Assert.False(Looks.WritePattern(panel, Pattern.Dots));
        Assert.True(LookShorthand.Both(panel, Tone.Outline, P(Gold)));
        foreach (var part in new[] { LookPart.Widgets, LookPart.FolderWidgets })
        {
            var cards = Looks.Section(config, part);
            Assert.All(ToneNames.All, tone => Assert.Equal(part == LookPart.Widgets || tone != Tone.Outline, LookShorthand.Both(cards, tone, P(Navy))));
            Assert.True(Looks.WriteFont(cards, GeorgiaBold));
            Assert.True(Looks.WritePattern(cards, Pattern.Grain));
        }
        Assert.Equal(["taskbar", "widgets", "flyouts", "folderWidgets"], LookParts.All.Select(LookParts.JsonName));
    }

    /// <summary>
    /// The file: the Appearance's "appearance", each value its word, read back the same; nothing said, nothing written; a
    /// value that does not read, unset; a section's word it may not say, dropped.
    /// </summary>
    [Fact]
    public void TheFileKeepsTheAppearanceAsSaid()
    {
        var (config, _, _, _) = Bar();
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, P(Navy.At(0.8))));
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.Outline, Paint.Nothing));
        Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.FolderWidgets), new TextStyle("Cascadia Mono", 350)));
        var json = config.ToJson();
        Assert.Contains("\"appearance\": {", json, StringComparison.Ordinal);
        // §568: a colour a pair, its dark half and its light one.
        Assert.Contains("\"background\": {", json, StringComparison.Ordinal);
        Assert.Contains("\"dark\": \"#CC1E3A5F\"", json, StringComparison.Ordinal);
        Assert.Contains("\"light\": \"#CC1E3A5F\"", json, StringComparison.Ordinal);
        Assert.Contains("\"dark\": \"none\"", json, StringComparison.Ordinal);
        Assert.Contains("\"font\": \"Cascadia Mono/350\"", json, StringComparison.Ordinal);
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(json, back.ToJson());
        Assert.Equal(Looks.Of(config, dark: true), Looks.Of(back, dark: true));
        Assert.Equal(Looks.Of(config, dark: false), Looks.Of(back, dark: false));
        var plain = Configs.RoundTrip(new Config(), out _);
        Assert.DoesNotContain("\"appearance\"", plain.ToJson(), StringComparison.Ordinal);

        // A half that does not read is unset, the other kept; a colour that is no pair (a colour alone) reads as nothing; a
        // section's word it may not say is dropped.
        var odd = Configs.Repaired("""
            { "appearance": { "palettes": { "karpo": {
                "taskbar": { "text": { "dark": "#FF0000" }, "font": "georgia", "background": { "dark": "teal" } },
                "flyouts": { "background": { "dark": "#FF0000" }, "outline": { "light": "#00FF00", "dark": "teal" } },
                "widgets": { "font": " /700", "accent": "#0000FF", "text": { "dark": "#0000FF", "light": 7 } } } } } }
            """);
        var looks = odd.WornValues!;
        Assert.Null(looks.Taskbar);
        Assert.Equal((new ColorPair { Light = "#00FF00" }, (ColorPair?)null), (looks.Flyouts!.Outline, looks.Flyouts.Background));
        Assert.Equal(((ColorPair?)null, new ColorPair { Dark = "#0000FF" }, (string?)null), (looks.Widgets!.Accent, looks.Widgets.Text, looks.Widgets.Font));
    }

    /// <summary>
    /// §509, §568: Karpo's looks, kept as palettes, each designed for both of Windows' modes: every text 4.5:1 and every accent
    /// 3:1 on its card in its mode, its light design's bar light and its dark one's dark, one font in both; the first four
    /// designed (§459) without outlines, as they were. §574: Karpo's own the first palette, its designs KarpoLook's (its text,
    /// its accent and its outline Karpo's to draw). §618: thirteen, §704 fourteen, each its own bars and its own name.
    /// </summary>
    [Fact]
    public void ThePalettesReadAsDesignedInBothModes()
    {
        var karpo = LookPalettes.All[0];
        Assert.Equal((LookPalettes.KarpoId, S.SettingsLookPaletteKarpo), (karpo.Id, karpo.Name));
        Assert.Equal((KarpoLook.Paints(false), KarpoLook.Paints(true)), (karpo.Light, karpo.Dark));
        string[] unlined = [LookPalettes.GraphiteId, LookPalettes.PaperId, LookPalettes.HarborId, LookPalettes.BloomId];
        foreach (var palette in LookPalettes.All.Skip(1))
        {
            foreach (var dark in new[] { false, true })
            {
                var paints = palette.In(dark);
                foreach (var (ground, plate) in new[] { (paints.Taskbar, paints.Widgets), (default(PartPaints), paints.FolderWidgets) })
                {
                    var card = ToneRules.Seen(plate.Background.Color, ToneRules.WindowsBackground(dark));
                    var what = $"{palette.Id} in {(dark ? "dark" : "light")}";
                    Assert.True(ToneRules.Contrast(plate.Text.Color!.Value, card) >= ToneRules.TextContrast, $"{what}: text {ToneRules.Contrast(plate.Text.Color!.Value, card):0.0}:1");
                    // §704: Liquid glass says none: the system's accent.
                    Assert.True(plate.Accent.Color is not { } accent || ToneRules.Contrast(accent, card) >= ToneRules.AccentContrast, $"{what}: accent");
                    // §624: a folder's lines never outlined, in any palette.
                    Assert.True(plate == paints.FolderWidgets ? plate.Outline.IsWindows : !unlined.Contains(palette.Id) || plate.Outline == Paint.Nothing, $"{what}: an outline");
                }
                Assert.Equal(dark, ToneRules.IsDark(paints.Taskbar.Background.Color!.Value with { A = 255 }));
            }
            Assert.Equal(palette.Light.Widgets.Type, palette.Dark.Widgets.Type);
        }
        Assert.Equal(PaletteTests.Order, LookPalettes.All.Select(palette => palette.Id));
        Assert.Equal(S.SettingsAppearanceThemeGraphite, LookPalettes.All[1].Name);
        // §618: each design its own (Midnight's dark bar and Contrast's both black, the rest of them apart).
        Assert.Equal(2 * LookPalettes.All.Length, LookPalettes.All.SelectMany(palette => new[] { palette.Light, palette.Dark }).Distinct().Count());
        Assert.Equal(LookPalettes.All.Length, LookPalettes.All.Select(palette => palette.Name).Distinct().Count());
        Assert.Equal(["Segoe UI Variable Text/400", "Segoe UI Variable Text/400", "Segoe UI Variable Text/400", "Segoe UI Variable Text/400",
            "Segoe UI Variable Text/600", "Segoe UI Variable Text/400", "Segoe UI Variable Text/400", "Segoe UI Variable Text/400", "Sitka Text/400", "Bahnschrift/400",
            "Segoe UI Variable Text/600", "Segoe UI Variable Text/600", "Bahnschrift/400", "Segoe UI Variable Text/400"],
            LookPalettes.All.Select(palette => palette.Light.Widgets.Type.Text));
    }

    /// <summary>§547: a palette's design as drawn: its values, the bar's blur left Karpo's own (a palette says none).</summary>
    private static LookPaints Drawn(LookPaints design) => design.With(LookPart.Taskbar, design.Taskbar with { Blur = KarpoLook.Blur });

    /// <summary>
    /// §577 (the person, 2026-11-09: "you should be able to select the palettes or you can also select custom ... when you have
    /// graphite selected for example you should also be able to change the settings but then you can also reset them to the
    /// default for the style again, not with custom. the custom settings should also always be saved ... the style
    /// configurations for the other styles should also be saved even if it is not active."): Palette lists Karpo, the designed
    /// ones and Custom, the one worn ticked; one worn is drawn as designed; a value changed is kept with the palette worn, which
    /// stays worn; another worn and back, each its own values; a designed one's reset puts it back as designed, the others'
    /// values kept, greyed while there is nothing to reset (a value said that is the design's own is nothing); Custom starts as
    /// Karpo's, keeps its values and has no reset. The file keeps it all.
    /// </summary>
    [Fact]
    public void EachPaletteKeepsItsOwnValuesAndADesignedOneResetsToItsDesign()
    {
        var config = new Config();
        OptionRow? Row(string key) => OptionRows.Find(BarLooks.Rows(config, new BarSpot("display", "One"), dark: false, scope: LooksScope.Appearance), key);
        OptionRow Palettes() => Row(LookRows.PaletteRow)!;
        var choices = LookPalettes.Choices;
        Assert.Equal([.. LookPalettes.All, LookPalettes.Custom], choices);
        Assert.Equal(choices.Select(palette => palette.Name), Palettes().Choices);
        Assert.Equal((0, S.SettingsLookCustom), (Palettes().Chosen, Palettes().Choices[^1]));
        Assert.Equal((S.SettingsLookReset, false), (Row(LookRows.ResetRow)!.Title, Row(LookRows.ResetRow)!.Enabled));

        // A value changed with nothing worn: Karpo's own, kept with it; Karpo's reset in force.
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Widgets), Pattern.Dots));
        Assert.Equal(Pattern.Dots, config.Appearance![LookPalettes.KarpoId]!.Widgets!.Pattern);
        Assert.Same(LookPalettes.All[0], LookPalettes.Worn(config));
        Assert.True(Row(LookRows.ResetRow)!.Enabled);

        // Paper worn: as designed, Karpo's dots Karpo's; ticked; the file says so; nothing of Paper's to reset.
        var paper = LookPalettes.Of(LookPalettes.PaperId)!;
        Assert.True(Palettes().Set!(Array.IndexOf(choices, paper)));
        Assert.False(LookPalettes.Wear(config, paper));
        Assert.Equal((Drawn(paper.Dark), Drawn(paper.Light)), (Looks.Of(config, dark: true), Looks.Of(config, dark: false)));
        Assert.Equal((paper.Drawn(true), paper.Drawn(false)), (Drawn(paper.Dark), Drawn(paper.Light)));
        Assert.Equal(Array.IndexOf(choices, paper), Palettes().Chosen);
        Assert.Contains("\"palette\": \"paper\"", config.ToJson(), StringComparison.Ordinal);
        Assert.False(Row(LookRows.ResetRow)!.Enabled);
        // §610: beside Palette, its whole name the palette's ("Reset Paper to default").
        Assert.Equal((S.SettingsLookResetPalette(paper.Name), true), (Row(LookRows.ResetRow)!.Detail, Row(LookRows.ResetRow)!.Beside));

        // §568: Paper's dark bar changed: Paper still worn, its light bar Paper's, its cards Paper's; kept with Paper.
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, dark: true, P(Navy)));
        Assert.Same(paper, LookPalettes.Worn(config));
        Assert.Equal(P(Navy), Looks.Of(config, dark: true).Taskbar.Background);
        Assert.Equal(paper.Light.Taskbar.Background, Looks.Of(config, dark: false).Taskbar.Background);
        Assert.Equal(paper.Dark.Widgets, Looks.Of(config, dark: true).Widgets);
        Assert.Equal("#1E3A5F", config.Appearance[LookPalettes.PaperId]!.Taskbar!.Background!.Dark);
        Assert.True(Row(LookRows.ResetRow)!.Enabled);

        // Another worn and back: each its own values.
        var harbor = LookPalettes.Of(LookPalettes.HarborId)!;
        Assert.True(LookPalettes.Wear(config, harbor));
        Assert.Equal(Drawn(harbor.Dark), Looks.Of(config, dark: true));
        Assert.True(LookPalettes.Wear(config, paper));
        Assert.Equal(P(Navy), Looks.Of(config, dark: true).Taskbar.Background);
        Assert.True(LookPalettes.Wear(config, LookPalettes.All[0]));
        Assert.Equal(Pattern.Dots, Looks.Of(config, dark: true).Widgets.Pattern);
        Assert.Null(config.Appearance.Palette);

        // Paper's reset, worn: as designed again; Karpo's values kept.
        Assert.True(LookPalettes.Wear(config, paper));
        Assert.True(Row(LookRows.ResetRow)!.Set!(0));
        Assert.Equal(Drawn(paper.Dark), Looks.Of(config, dark: true));
        Assert.Null(config.Appearance[LookPalettes.PaperId]);
        Assert.NotNull(config.Appearance[LookPalettes.KarpoId]);
        Assert.False(Row(LookRows.ResetRow)!.Enabled);
        Assert.False(LookPalettes.Reset(config, paper));
        // A value said that is its design's own: written, nothing to reset.
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Taskbar), Tone.Background, dark: true, paper.Dark.Taskbar.Background));
        Assert.NotNull(config.Appearance[LookPalettes.PaperId]);
        Assert.False(LookPalettes.Changed(config, paper));

        // Custom: Karpo's design at first; its values kept; no reset at all.
        Assert.True(Palettes().Set!(choices.Length - 1));
        Assert.Same(LookPalettes.Custom, LookPalettes.Worn(config));
        Assert.Equal((Drawn(KarpoLook.Paints(true)), Drawn(KarpoLook.Paints(false))), (Looks.Of(config, dark: true), Looks.Of(config, dark: false)));
        Assert.Null(Row(LookRows.ResetRow));
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Widgets), Tone.Background, dark: false, P(Gold)));
        Assert.Equal(P(Gold), Looks.Of(config, dark: false).Widgets.Background);
        Assert.False(LookPalettes.Reset(config, LookPalettes.Custom));
        Assert.True(LookPalettes.Wear(config, harbor));
        Assert.True(LookPalettes.Wear(config, LookPalettes.Custom));
        Assert.Equal(P(Gold), Looks.Of(config, dark: false).Widgets.Background);

        // The file keeps every palette's values and the one worn.
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(config.ToJson(), back.ToJson());
        Assert.Same(LookPalettes.Custom, LookPalettes.Worn(back));
        Assert.Equal([LookPalettes.CustomId, LookPalettes.KarpoId, LookPalettes.PaperId], back.Appearance!.Palettes!.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Looks.Of(config, dark: false), Looks.Of(back, dark: false));
    }

    /// <summary>
    /// §517 (the person: "remove the copy paste look functionality"): no painter. Ctrl+Shift+C and Ctrl+Shift+V do nothing in
    /// edit mode, and a widget's buttons are Cut, Copy and Remove.
    /// </summary>
    [Fact]
    public void ThereIsNoLookPainter()
    {
        Assert.Equal(EditKeyAct.None, EditKeys.Of('C', control: true, shift: true, repeat: false).Act);
        Assert.Equal(EditKeyAct.None, EditKeys.Of('V', control: true, shift: true, repeat: false).Act);
        Assert.Equal(EditKeyAct.Copy, EditKeys.Of('C', control: true, shift: false, repeat: false).Act);
        Assert.Equal(EditKeyAct.Paste, EditKeys.Of('V', control: true, shift: false, repeat: false).Act);
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], TheDock.WidgetRowsFor(BarKind.NightLight.New()).Select(row => row.Text));
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], TheDock.WidgetRowsFor(ListKind.Divider.New()).Select(row => row.Text));
    }

    /// <summary>
    /// §513: a pattern's colour resolves as every value does — the Appearance's, else its text's at the pattern's strength
    /// (§462) —; set, the pattern is drawn in it as said, its opacity the pattern's, and nowhere else: the Taskbar widgets' on
    /// the bar's cards and faces, the Widgets in folders' on the lines, the Taskbar's on the bar. The Flyouts have none. The
    /// file's "patternColor", read back the same.
    /// </summary>
    [Fact]
    public void APatternsColourIsItsSectionsElseItsTextsFainter()
    {
        var (config, _, _, _) = Bar();
        var windows = Karpo.Tests.Folders.TestLooks.LookFor(dark: true);
        var white = ToneRules.WindowsText(dark: true);
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Taskbar), Pattern.Lines));
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Widgets), Pattern.Dots));
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.FolderWidgets), Pattern.Grid));

        // Unset: the text's ink at the pattern's strength, as ever.
        var bar = windows.Wearing(config);
        Assert.Equal(ToneRules.PatternOf(Pattern.Dots, white), bar.Colors.CardPattern);
        Assert.Equal(ToneRules.PatternOf(Pattern.Lines, white), bar.Colors.TaskbarPattern);
        Assert.Equal(ToneRules.PatternOf(Pattern.Grid, white), bar.Flyout().Colors.CardPattern);

        // The Taskbar widgets': every card on the bar, as said; the bar's and the lines' not.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Widgets), Tone.PatternColor, P(Gold.At(0.6))));
        bar = windows.Wearing(config);
        Assert.Equal(new Patterned(Pattern.Dots, Gold.At(0.6)), bar.Colors.CardPattern);
        Assert.Equal(ToneRules.PatternOf(Pattern.Lines, white), bar.Colors.TaskbarPattern);
        Assert.Equal(ToneRules.PatternOf(Pattern.Grid, white), bar.Flyout().Colors.CardPattern);

        // The Widgets in folders': the lines.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.FolderWidgets), Tone.PatternColor, P(Navy)));
        bar = windows.Wearing(config);
        Assert.Equal(new Patterned(Pattern.Grid, Navy), bar.Flyout().Colors.CardPattern);
        Assert.Equal(new Patterned(Pattern.Dots, Gold.At(0.6)), bar.Colors.CardPattern);

        // The Taskbar's: the bar.
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.PatternColor, P(Rose.At(0.3))));
        bar = windows.Wearing(config);
        Assert.Equal(new Patterned(Pattern.Lines, Rose.At(0.3)), bar.Colors.TaskbarPattern);

        // The Flyouts have none.
        Assert.False(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.PatternColor, P(Gold)));

        // The file.
        var json = config.ToJson();
        Assert.Contains("\"dark\": \"#99FFB900\"", json, StringComparison.Ordinal);
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(json, back.ToJson());
        Assert.Equal(Looks.Of(config, dark: true), Looks.Of(back, dark: true));
        var odd = Configs.Repaired("""
            { "appearance": { "palettes": { "karpo": { "flyouts": { "patternColor": { "dark": "#FF0000" }, "outline": { "dark": "#00FF00" } }, "widgets": { "patternColor": { "dark": "teal" } } } } } }
            """);
        Assert.Null(odd.WornValues!.Flyouts!.PatternColor);
        Assert.Null(odd.WornValues.Widgets);
    }

    /// <summary>
    /// §513: Pattern color stands right after Pattern in every section's editor that has a pattern — the Taskbar's, the Taskbar
    /// widgets', the Widgets in folders' —, a colour's row, while a pattern is in force there; none while none is, none on the
    /// Flyouts. Where the file says none it shows the colour the pattern is drawn in, the pattern's ink of its text (§574: a
    /// value, no "Default (…)"); a colour picked keeps that opacity.
    /// </summary>
    [Fact]
    public void PatternColorStandsAfterPatternWhileAPatternIsInForce()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: true) { Layout = config.DefaultLayout };
        IReadOnlyList<OptionRow> Section(LookPart part) => LookRows.Editor(place, Looks.Section(config, part), "s");
        static int At(IReadOnlyList<OptionRow> rows, string key) => rows.ToList().FindIndex(row => row.Key == key);

        foreach (var part in new[] { LookPart.Taskbar, LookPart.Widgets, LookPart.FolderWidgets })
        {
            Assert.Equal(-1, At(Section(part), "s.patternColor"));
            Assert.True(Looks.WritePattern(Looks.Section(config, part), Pattern.Dots));
            var rows = Section(part);
            // §613: the mode shown's alone, after the pattern and its reset (the pattern is no longer its design's).
            Assert.Equal(At(rows, "s.pattern") + 1, At(rows, "s.pattern" + LookRows.ResetEnd));
            Assert.Equal(At(rows, "s.pattern") + 2, At(rows, "s.patternColor"));
            var row = rows[At(rows, "s.patternColor")];
            Assert.Equal((OptionRowKind.Color, S.SettingsLookPatternColor), (row.Kind, row.Title));
            Assert.Equal(ToneRules.PatternOf(Pattern.Dots, ToneRules.WindowsText(dark: true))!.Value.Ink, row.Color);
            Assert.DoesNotContain(row.Tiles, tile => tile.Glyph == OptionGlyphs.Automatic);
        }
        Assert.DoesNotContain(Section(LookPart.Flyouts), row => row.Key.Contains("patternColor", StringComparison.Ordinal));

        // A colour picked keeps the opacity in force; §523 a colour mixed writes its own.
        var colour = Section(LookPart.Widgets).Single(row => row.Key == "s.patternColor");
        var red = colour.Tiles.ToList().FindIndex(tile => tile.Color is { } shown && shown.R > 0xC0 && shown.G < 0x40 && shown.B < 0x40);
        Assert.True(colour.Set!(red));
        var written = Argb.Parse(config.WornValues!.Widgets!.PatternColor!.Dark)!.Value;
        Assert.Equal(colour.Color!.Value.A, written.A);
        Assert.Null(config.WornValues.Widgets.PatternColor.Light);
        colour = Section(LookPart.Widgets).Single(row => row.Key == "s.patternColor");
        Assert.True(colour.SetColor!(written.At(0.5), true));
        Assert.Equal(written.At(0.5), Argb.Parse(config.WornValues!.Widgets!.PatternColor!.Dark));
    }

    /// <summary>
    /// §638 (the person, 2026-11-09: "font weight should be selectable individually but on the same row. not combined into one
    /// dropdown"): every section that holds text — the Taskbar widgets', the Widgets in folders' — has its Font, a choice of
    /// every family installed, and right after it its weight standing beside it (Beside), a dropdown however few (Grows) of the
    /// weights that family has, each named and drawn in itself; Karpo's own family at Regular chosen where the file says none,
    /// each family drawn at the weight it would come to; no reset while the font is the palette's. The bar's and the Flyouts'
    /// have neither (§556 to §637 one choice of every weight of every family).
    /// </summary>
    [Fact]
    public void EverySectionOfTextHasItsFontsFamilyAndItsWeightBesideIt()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: true) { Layout = config.DefaultLayout };
        var families = InstalledFonts.All.Select(font => font.Family).ToList();
        foreach (var part in LookParts.All)
        {
            var rows = LookRows.Editor(place, Looks.Section(config, part), "s").ToList();
            if (!LookParts.HasText(part))
            {
                Assert.DoesNotContain(rows, row => row.Key is "s.font" or "s.weight");
                continue;
            }
            var at = rows.FindIndex(row => row.Key == "s.font");
            var (font, weight) = (rows[at], rows[at + 1]);
            Assert.Equal((OptionRowKind.Choice, S.SettingsAppearanceStylesFont, false, true), (font.Kind, font.Title, font.Beside, font.Grows));
            Assert.Equal(families, font.Choices);
            Assert.Equal(TextStyle.KarpoFamily, font.Choices[font.Chosen]);
            Assert.Equal(default(TextStyle), font.ChoiceFaces[font.Chosen]);
            // Each family in the weight it comes to: Regular, which every one has here.
            Assert.All(font.ChoiceFaces, face => Assert.Equal(TextStyle.Regular, face.Weight));
            Assert.Equal(("s.weight", OptionRowKind.Choice, S.SettingsAppearanceStylesWeight, true, true), (weight.Key, weight.Kind, weight.Title, weight.Beside, weight.Grows));
            Assert.Equal(
                [S.SettingsAppearanceStylesWeightLight, S.SettingsAppearanceStylesWeightSemilight, S.SettingsAppearanceStylesWeightRegular, S.SettingsAppearanceStylesWeightSemibold, S.SettingsAppearanceStylesWeightBold],
                weight.Choices);
            Assert.Equal([300, 350, 400, 600, 700], weight.ChoiceFaces.Select(face => face.Weight));
            Assert.All(weight.ChoiceFaces, face => Assert.True(face.IsKarpoFamily));
            Assert.Equal(S.SettingsAppearanceStylesWeightRegular, weight.Choices[weight.Chosen]);
            Assert.DoesNotContain(rows, row => row.Key == "s.font" + LookRows.ResetEnd);
        }
    }

    /// <summary>
    /// §638: a family picked keeps the weight in force where it has it, else takes its nearest — of two as near the heavier past
    /// Medium, the lighter below, as CSS matches a weight —; a weight picked keeps the family. One value written each time.
    /// </summary>
    [Fact]
    public void AFamilyPickedKeepsItsWeightOrTakesTheNearestAndAWeightKeepsItsFamily()
    {
        Assert.Equal(new TextStyle("Sitka Text", 600), TextStyles.In("Sitka Text", new TextStyle(null, 600)));
        Assert.Equal(GeorgiaBold, TextStyles.In("Georgia", new TextStyle(null, 600)));
        Assert.Equal(new TextStyle("Georgia", 400), TextStyles.In("Georgia", new TextStyle(null, 300)));
        Assert.Equal(new TextStyle("Sitka Text", 600), TextStyles.In("Sitka Text", new TextStyle("Bahnschrift", 550)));
        Assert.Equal(new TextStyle("Bahnschrift", 400), TextStyles.In("Bahnschrift", new TextStyle("Sitka Text", 450)));
        Assert.Equal(new TextStyle("Nowhere Sans", 400), TextStyles.In("Nowhere Sans", GeorgiaBold));

        var config = new Config();
        var place = new OptionPlace(config, Dark: false) { Layout = config.DefaultLayout };
        OptionRow Row(string key) => LookRows.Editor(place, Looks.Section(config, LookPart.Widgets), "k").Single(row => row.Key == key);
        bool Pick(string key, string choice) => Row(key).Set!(Row(key).Choices.ToList().IndexOf(choice));
        Assert.True(Pick("k.weight", S.SettingsAppearanceStylesWeightSemibold));
        Assert.Equal("Segoe UI Variable Text/600", config.WornValues!.Widgets!.Font);
        // The families each at the weight a pick comes to: Georgia has no Semibold, Bold the nearest.
        Assert.Equal(GeorgiaBold, Row("k.font").ChoiceFaces[Row("k.font").Choices.ToList().IndexOf("Georgia")]);
        Assert.True(Pick("k.font", "Sitka Text"));
        Assert.Equal("Sitka Text/600", config.WornValues.Widgets.Font);
        Assert.True(Pick("k.font", "Georgia"));
        Assert.Equal("Georgia/700", config.WornValues.Widgets.Font);
        Assert.Equal([S.SettingsAppearanceStylesWeightRegular, S.SettingsAppearanceStylesWeightBold], Row("k.weight").Choices);
        Assert.True(Pick("k.weight", S.SettingsAppearanceStylesWeightRegular));
        Assert.Equal("Georgia/400", config.WornValues.Widgets.Font);
        Assert.Equal("Georgia", Row("k.font").Choices[Row("k.font").Chosen]);
    }

    /// <summary>§556: a font picked writes one value, "Family/Weight", read back the same; §574 Karpo's own picked is written as any.</summary>
    [Fact]
    public void AFontPickedIsOneValue()
    {
        var config = new Config();
        var place = new OptionPlace(config, Dark: false) { Layout = config.DefaultLayout };
        OptionRow Font() => LookRows.Editor(place, Looks.Section(config, LookPart.Widgets), "k").Single(row => row.Key == "k.font");
        OptionRow Weight() => LookRows.Editor(place, Looks.Section(config, LookPart.Widgets), "k").Single(row => row.Key == "k.weight");
        Assert.True(Font().Set!(Font().Choices.ToList().IndexOf("Georgia")));
        Assert.True(Weight().Set!(Weight().ChoiceFaces.ToList().IndexOf(GeorgiaBold)));
        Assert.Equal("Georgia/700", config.WornValues!.Widgets!.Font);
        Assert.Equal(GeorgiaBold, Looks.Of(config, dark: true).Widgets.Type);
        Assert.Equal(GeorgiaBold, Font().ChoiceFaces[Font().Chosen]);
        Assert.Equal(GeorgiaBold, Weight().ChoiceFaces[Weight().Chosen]);
        var json = config.ToJson();
        Assert.Contains("\"font\": \"Georgia/700\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"weight\"", json, StringComparison.Ordinal);
        var back = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(json, back.ToJson());
        Assert.True(Font().Set!(Font().Choices.ToList().IndexOf(TextStyle.KarpoFamily)));
        Assert.Equal("Segoe UI Variable Text/700", config.WornValues!.Widgets!.Font);
        Assert.True(Weight().Set!(Weight().ChoiceFaces.ToList().IndexOf(default)));
        Assert.Equal("Segoe UI Variable Text/400", config.WornValues!.Widgets!.Font);
        Assert.Equal(default(TextStyle), Looks.Of(config, dark: true).Widgets.Type);
    }

    /// <summary>
    /// §556: the file's one form: "Family/Weight", a family alone Regular, Karpo's own family at 400 <c>default</c>; nothing
    /// or a weight alone unset; a weight held to DirectWrite's 1 to 999. A family this PC has not is kept as said, listed
    /// among the choices where it would stand, ticked.
    /// </summary>
    [Fact]
    public void AFontIsReadInItsOneFormAndOneNotInstalledIsKept()
    {
        Assert.Equal(new TextStyle("Georgia", 700), TextStyle.Parse("Georgia/700"));
        Assert.Equal(new TextStyle("Georgia"), TextStyle.Parse(" Georgia "));
        Assert.Equal(default(TextStyle), TextStyle.Parse("Segoe UI Variable Text/400"));
        Assert.Equal("Segoe UI Variable Text/400", default(TextStyle).Text);
        Assert.Equal((600, 700), new TextStyle("Georgia", 600).Weights);
        Assert.Equal((400, 600), default(TextStyle).Weights);
        Assert.Equal((300, 500), new TextStyle(null, 300).Weights);
        Assert.Equal(999, new TextStyle("Georgia", 5000).Weight);
        Assert.Null(TextStyle.Parse("/700"));
        Assert.Null(TextStyle.Parse("  "));
        Assert.Equal(new TextStyle("Some/Odd Name"), TextStyle.Parse("Some/Odd Name"));
        Assert.Equal(S.SettingsAppearanceStylesWeightBold, TextStyles.WeightName(700));
        Assert.Equal(S.SettingsAppearanceStylesWeightSemilight, TextStyles.WeightName(350));
        Assert.Equal(S.SettingsAppearanceStylesWeightRegular, TextStyles.WeightName(420));

        var config = Configs.Repaired("""
            { "appearance": { "palettes": { "karpo": { "widgets": { "font": "Nowhere Sans/650" }, "folderWidgets": { "font": "Georgia" } } } } }
            """);
        Assert.Equal(("Nowhere Sans/650", "Georgia/400"), (config.WornValues!.Widgets!.Font, config.WornValues.FolderWidgets!.Font));
        var nowhere = new TextStyle("Nowhere Sans", 650);
        Assert.Equal(nowhere, Looks.Of(config, dark: true).Widgets.Type);
        var place = new OptionPlace(config, Dark: false) { Layout = config.DefaultLayout };
        var rows = LookRows.Editor(place, Looks.Section(config, LookPart.Widgets), "k");
        var font = rows.Single(row => row.Key == "k.font");
        Assert.Equal(nowhere, font.ChoiceFaces[font.Chosen]);
        Assert.Equal("Nowhere Sans", font.Choices[font.Chosen]);
        Assert.Equal(InstalledFonts.All.Count + 1, font.Choices.Count);
        // Listed where it would stand: after the families it comes after, before the rest.
        Assert.True(string.Compare(font.Choices[font.Chosen - 1], "Nowhere Sans", StringComparison.CurrentCultureIgnoreCase) < 0);
        Assert.True(string.Compare(font.Choices[font.Chosen + 1], "Nowhere Sans", StringComparison.CurrentCultureIgnoreCase) > 0);
        // §638: its weight the one it is in, alone, named as OpenType names the nearest.
        var weight = rows.Single(row => row.Key == "k.weight");
        Assert.Equal([S.SettingsAppearanceStylesWeightSemibold], weight.Choices);
        Assert.Equal((0, nowhere), (weight.Chosen, weight.ChoiceFaces[0]));
        // A weight its family is not installed in is kept as said among its weights, ticked.
        var odd = Configs.Repaired("""{ "appearance": { "palettes": { "karpo": { "widgets": { "font": "Georgia/600" } } } } }""");
        var georgia = LookRows.Editor(new OptionPlace(odd, Dark: false) { Layout = odd.DefaultLayout }, Looks.Section(odd, LookPart.Widgets), "k").Single(row => row.Key == "k.weight");
        Assert.Equal([400, 600, 700], georgia.ChoiceFaces.Select(face => face.Weight));
        Assert.Equal(1, georgia.Chosen);
    }
}
