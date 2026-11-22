// edit, §613 (the person, 2026-11-09: "the appearance should actually only show for dark or light mode including all the
// settings. you can switch between those with a switcher at the top, either dark light or system." — "this is a real setting.
// default is from system."; "you should be able to reset individual properties to the style default"; docs\widgets.md
// §613), apart from any window, Windows' mode injected (never the PC's own):
//
//  - **The mode a setting.** The Appearance says the taskbar's mode: System (Windows', left out of the file), Dark or Light;
//    the taskbar wears that half of every colour whatever Windows is in; high contrast Windows' own whatever it says.
//  - **The page in one mode.** Mode first, a switcher; then everything of the mode in force: one row a colour, its value and
//    its picker of that mode; a pick writes that half alone.
//  - **A value's reset.** Each value its palette's design draws otherwise in the mode shown has an icon button beside its
//    control; pressed, the value unset, the design's again; none where it is the design's; Custom's back to Karpo's.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed class AppearanceModeTests
{
    private const float Scale = 1.5f;

    private static IReadOnlyList<OptionRow> Rows(Config config, bool windows) => LookRows.Rows(config, config.DefaultLayout, windows);

    private static OptionRow Find(Config config, bool windows, string key) => OptionRows.Find(Rows(config, windows), key)!;

    private static string Key(LookPart part, string ends) => LookRows.SectionKey(part) + "." + ends;

    private static PanelPageView Page(Config config, bool windows, List<OptionPick>? picks = null) =>
        PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, "", OptionMenu.Rows(Rows(config, windows), picks ?? []));

    /// <summary>The page laid out, <paramref name="part"/>'s tab chosen.</summary>
    private static PanelLayout Laid(PanelPageView page, LookPart part) =>
        new(new SettingsPanelTests.Words(), Scale, new PanelInput(0, new SettingsContent([page]), PanelPage.Appearance,
            new HashSet<string> { PanelPage.Appearance + "/" + PanelTabs.Mark + LookParts.Name(part) }), PanelLayout.SizeOf(Scale, 4000));

    /// <summary>
    /// A real setting, Windows' by default: a new config says none (System, left out of the file) and the taskbar is in
    /// Windows' mode; Dark or Light written and read back, worn whatever Windows is in; System again leaves the file as it
    /// was; a word this build does not know reads as System; a file of shape 79 (no mode) reads as System.
    /// </summary>
    [Fact]
    public void TheModeIsARealSettingWindowsByDefault()
    {
        var config = MenuCorpus.Fresh();
        var mode = Find(config, windows: true, LookRows.ModeRow);
        Assert.Equal((OptionRowKind.Choice, true), (mode.Kind, mode.Segmented));
        Assert.Equal([S.SettingsAppearanceDark, S.SettingsAppearanceLight, S.SettingsAppearanceSystem], mode.Choices);
        Assert.Equal(2, mode.Chosen);
        Assert.Null(config.Appearance);
        foreach (var windows in new[] { true, false })
        {
            Assert.Equal(windows, Looks.Dark(config.Appearance, windows));
        }

        Assert.True(mode.Set!(1));
        Assert.Equal(TaskbarMode.Light, config.Appearance!.Mode);
        Assert.Contains("\"mode\": \"light\"", config.ToJson(), StringComparison.Ordinal);
        var read = Configs.RoundTrip(config, out _);
        Assert.Equal(TaskbarMode.Light, read.Appearance!.Mode);
        Assert.False(Looks.Dark(read.Appearance, windows: true));
        Assert.Equal(1, Find(read, windows: true, LookRows.ModeRow).Chosen);
        Assert.False(Find(read, windows: true, LookRows.ModeRow).Set!(1));

        Assert.True(Find(read, windows: false, LookRows.ModeRow).Set!(0));
        Assert.True(Looks.Dark(read.Appearance, windows: false));
        Assert.True(Find(read, windows: false, LookRows.ModeRow).Set!(2));
        Assert.Null(read.Appearance);

        var odd = Configs.Repaired("""{ "version": 80, "appearance": { "mode": "dusk", "palette": "paper" } }""");
        Assert.Equal((TaskbarMode.System, LookPalettes.PaperId), (odd.Appearance!.Mode, odd.Appearance.Palette));
        var older = Configs.Repaired("""{ "version": 79, "appearance": { "palette": "graphite" } }""");
        Assert.Equal((TaskbarMode.System, LookPalettes.GraphiteId), (older.Appearance!.Mode, older.Appearance.Palette));
        Assert.True(Config.Shape >= 80, $"the mode came with shape 80; the file is shape {Config.Shape}");
    }

    /// <summary>
    /// The taskbar wears the mode the Appearance says: every bar, folder and flyout in that half of every colour pair, whatever
    /// Windows is in (Look.Wearing, Dock.TaskbarLook); System Windows'; in high contrast Windows' own system colours whatever
    /// it says. Only a mode not pinned makes Windows' mode another taskbar (Dock.LookChanged rebuilds nothing otherwise).
    /// </summary>
    [Fact]
    public void TheTaskbarWearsTheModeTheAppearanceSays()
    {
        var config = MenuCorpus.Fresh();
        var bar = Looks.Section(config, LookPart.Taskbar);
        Assert.True(Looks.WritePaint(bar, Tone.Background, true, Karpo.Core.Paint.Parse("#14213D")));
        Assert.True(Looks.WritePaint(bar, Tone.Background, false, Karpo.Core.Paint.Parse("#FCA311")));
        var (darkWindows, lightWindows) = (AddListPictures.LookFor(dark: true), AddListPictures.LookFor(dark: false));
        foreach (var mode in LookRows.Modes)
        {
            _ = Looks.WriteMode(config, mode);
            foreach (var windows in new[] { darkWindows, lightWindows })
            {
                var worn = windows.Wearing(config);
                var dark = mode switch { TaskbarMode.Dark => true, TaskbarMode.Light => false, _ => windows.Dark };
                Assert.Equal((dark, Karpo.Core.Paint.Parse(dark ? "#14213D" : "#FCA311")), (worn.Dark, worn.Worn.Taskbar.Background));
                Assert.Equal(dark, FlyoutTheme.For(worn).Dark);
            }
            // Windows' mode turned: another taskbar only for System.
            Assert.Equal(mode == TaskbarMode.System, darkWindows.Wearing(config) != lightWindows.Wearing(config));
            Assert.True((darkWindows with { HighContrast = true }).Wearing(config).Colors.IsAutomatic);
        }
    }

    /// <summary>
    /// The page shows the mode in force alone: one row a colour (no ".dark" nor ".light" key, no pair), its value that mode's;
    /// a pick writes that half alone, the other kept; the swatches the palette's of that mode. Windows dark, Light pinned: the
    /// light half throughout.
    /// </summary>
    [Fact]
    public void ThePageShowsAndWritesTheModeInForceAlone()
    {
        var config = MenuCorpus.Fresh();
        Assert.True(Looks.WriteMode(config, TaskbarMode.Light));
        var all = OptionRows.Flat(Rows(config, windows: true)).ToList();
        Assert.DoesNotContain(all, row => row.Key.EndsWith(".dark", StringComparison.Ordinal) || row.Key.EndsWith(".light", StringComparison.Ordinal));
        var background = Find(config, windows: true, Key(LookPart.Widgets, "background"));
        Assert.Equal(KarpoLook.LightCard, background.Color);
        Assert.Equal(KarpoLook.Ground, Find(Clone(config, TaskbarMode.Dark), windows: false, Key(LookPart.Taskbar, "background")).Color);

        var sand = new Argb(255, 0xE8, 0xD9, 0xB5);
        Assert.True(background.SetColor!(sand, false));
        Assert.Equal(sand.ToString(), Argb.Parse(config.WornValues!.Widgets!.Background!.Light)!.Value.ToString());
        Assert.Null(config.WornValues.Widgets.Background.Dark);
        Assert.Equal(sand, Find(config, windows: true, Key(LookPart.Widgets, "background")).Color);
        // The same value seen in the dark mode is Karpo's dark card, untouched.
        Assert.Equal(KarpoLook.Card, Find(Clone(config, TaskbarMode.Dark), windows: true, Key(LookPart.Widgets, "background")).Color);

        // The panel: §670 a colour its picker inline, its head opening it in its card: no popup.
        var layout = Laid(Page(config, windows: true), LookPart.Widgets);
        var colours = layout.Blocks.SelectMany(block => block.Lines).Where(line => line.Row.Kind == PanelRowKind.Picker && PanelPickers.Colours(line.Row.Item)).ToList();
        Assert.NotEmpty(colours);
        Assert.All(colours, line => Assert.Single(layout.Controls(line), each => each.Hit.Kind == PanelHitKind.Head));
        Assert.DoesNotContain(colours.SelectMany(layout.Controls), each => each.Hit.Kind == PanelHitKind.Pop);

        static Config Clone(Config config, TaskbarMode mode)
        {
            var copy = config.Clone();
            _ = Looks.WriteMode(copy, mode);
            return copy;
        }
    }

    /// <summary>
    /// A value its palette's design draws otherwise, in the mode shown, has its reset right after it (Beside, Subtle: an icon
    /// button), none while it is the design's; pressed, the value is unset — the design's again — and the reset gone. Graphite:
    /// a colour, the font, the pattern and the blur; the other mode's half of a colour shows no reset in this one. Custom's
    /// design is Karpo's: its resets go back to Karpo's own.
    /// </summary>
    [Fact]
    public void AValueOtherThanItsPalettesHasItsResetBesideIt()
    {
        var config = MenuCorpus.Fresh();
        var graphite = LookPalettes.Of(LookPalettes.GraphiteId)!;
        Assert.True(LookPalettes.Wear(config, graphite));
        IReadOnlyList<OptionRow> Level(LookPart part, bool windows = true) =>
            Rows(config, windows).Single(row => row.Key == LookRows.SectionKey(part) + LookRows.Tab).Level;
        string[] Resets(bool windows = true) => [.. OptionRows.Flat(Rows(config, windows)).Where(row => row.Key.EndsWith(LookRows.ResetEnd, StringComparison.Ordinal) && row.Key != LookRows.ResetRow).Select(row => row.Key)];
        Assert.Empty(Resets());

        var background = Key(LookPart.Widgets, "background");
        Assert.True(Find(config, windows: true, background).SetColor!(new Argb(255, 0x1E, 0x3A, 0x5F), false));
        Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.Widgets), new TextStyle("Bahnschrift")));
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Taskbar), Pattern.Dots));
        Assert.True(Looks.WriteBlur(Looks.Section(config, LookPart.Taskbar), 70));
        string[] want = [background + LookRows.ResetEnd, Key(LookPart.Widgets, "font") + LookRows.ResetEnd, Key(LookPart.Taskbar, "blur") + LookRows.ResetEnd, Key(LookPart.Taskbar, "pattern") + LookRows.ResetEnd];
        Assert.Equal(want.Order(), Resets().Order());
        // Right after its row, beside it, an icon button named for its value.
        var widgets = Level(LookPart.Widgets).ToList();
        var at = widgets.FindIndex(row => row.Key == background);
        var reset = widgets[at + 1];
        Assert.Equal((background + LookRows.ResetEnd, OptionRowKind.Action, true, true, OptionGlyphs.Reset), (reset.Key, reset.Kind, reset.Beside, reset.Subtle, reset.Glyph));
        Assert.Equal(S.SettingsLookResetValue(S.SettingsAppearanceCustomBackgroundHeader), reset.Detail);
        // The light mode shows the light half: Graphite's own, no reset of the background there.
        Assert.DoesNotContain(background + LookRows.ResetEnd, Resets(windows: false));

        // Pressed: the design's again, its reset gone; the others kept.
        Assert.True(reset.Set!(0));
        Assert.Equal(graphite.Drawn(true).Widgets.Background, Looks.Of(config, dark: true).Widgets.Background);
        Assert.DoesNotContain(background + LookRows.ResetEnd, Resets());
        foreach (var key in want.Skip(1))
        {
            Assert.True(OptionRows.Find(Rows(config, true), key)!.Set!(0));
        }
        Assert.Empty(Resets());
        Assert.False(LookPalettes.Changed(config, graphite));

        // Custom: back to Karpo's own.
        Assert.True(LookPalettes.Wear(config, LookPalettes.Custom));
        Assert.True(Find(config, windows: true, background).SetColor!(new Argb(255, 0x1E, 0x3A, 0x5F), false));
        Assert.True(Find(config, windows: true, background + LookRows.ResetEnd).Set!(0));
        Assert.Equal(KarpoLook.Card, Find(config, windows: true, background).Color);
    }

    /// <summary>
    /// On the panel a value's reset is an icon button, a 32 DIP square 8 before its control, in its line (PanelRow.Pair): the
    /// control where it stands without it, the row's name before them; a click finds it, the keys reach it before the control;
    /// its tooltip and its name to a screen reader its whole name, "Reset Background to default", nothing more.
    /// </summary>
    [Fact]
    public void AValuesResetIsAnIconButtonBeforeItsControl()
    {
        var config = MenuCorpus.Fresh();
        var background = Key(LookPart.Widgets, "background");
        static PanelLine Background(PanelLayout layout) => layout.Blocks.SelectMany(block => block.Lines).Single(each => each.Row.Text == S.SettingsAppearanceCustomBackgroundHeader);
        var before = Laid(Page(config, windows: true), LookPart.Widgets);
        var plain = before.Controls(Background(before)).Single();
        Assert.True(Find(config, windows: true, background).SetColor!(new Argb(255, 0x1E, 0x3A, 0x5F), false));
        List<OptionPick> picks = [];
        var layout = Laid(Page(config, windows: true, picks), LookPart.Widgets);
        var line = Background(layout);
        var reset = line.Row.Pair!;
        Assert.True(reset.Item.Subtle);
        var controls = layout.Controls(line);
        // §670: before what its picker's head says, the head the whole line.
        Assert.Equal([PanelHitKind.Act, PanelHitKind.Head], controls.Select(each => each.Hit.Kind));
        var (button, colour) = (controls[0].Rect, controls[1].Rect);
        Assert.Equal((plain.Rect.X, plain.Rect.Width), (colour.X, colour.Width));
        Assert.Equal((layout.P(PanelMetrics.Control), layout.P(PanelMetrics.Control)), (button.Width, button.Height));
        Assert.Equal(layout.PickerValueLeft(line) - layout.P(PanelMetrics.Label), button.Right, 3);
        // The row's controls start at it: its name never runs under it.
        Assert.Equal(button.X, layout.ControlLeft(line), 3);
        Assert.True(layout.NameX(line) + layout.NameRoom(line) + layout.P(PanelMetrics.Gutter) <= button.X + 0.5f);
        var middle = layout.OnPanel(button).Center;
        var hit = layout.HitIn(new System.Numerics.Vector2(middle.X, middle.Y));
        Assert.Equal(controls[0].Hit, hit);
        var stops = layout.Stops();
        Assert.Equal(stops.IndexOf(controls[0].Hit) + 1, stops.IndexOf(controls[1].Hit));
        var whole = S.SettingsLookResetValue(S.SettingsAppearanceCustomBackgroundHeader);
        Assert.Equal(whole, layout.TipOf(hit));
        var said = DeckUia.Things(layout, bar: false).Single(thing => thing.Hit == hit);
        Assert.Equal((whole, UiaRole.Button, true), (said.Name, said.Role, said.Enabled));
        // Pressed through the menu's pick: the value its palette's again.
        var pick = OptionMenu.PickOf(picks, hit.Id);
        Assert.Equal(background + LookRows.ResetEnd, pick?.Key);
        Assert.True(OptionMenu.ApplyRow(OptionRows.Find(Rows(config, true), pick!.Key)!, pick));
        Assert.Equal(KarpoLook.Card, Find(config, windows: true, background).Color);
        Assert.Null(Background(Laid(Page(config, windows: true), LookPart.Widgets)).Row.Pair);
    }
}
