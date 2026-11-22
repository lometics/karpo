// The Appearance page (the person, 2026-11-05: "styles should not exist anymore, everywhere where we use styles there should
// be the values to configure yourself again"; docs\widgets.md §507, §509; §567: one look, Karpo's, "Appearance" — no
// layout's, widget's, folder's or line's own): the one editor of a section (Editor), the same rows for each:
//
//   Text     Font, Text color                  where the section holds text (the cards), under its heading (§517); §638 the
//                                font's family, every family installed, its weight beside it in its line, the family's
//                                weights (§556 to §637 one choice of every weight of every family)
//   Colors   Background, Outline, Accent color, Pattern
//                                as many as the section has (LookParts.TonesOf, HasPattern), each colour's swatch in its
//                                icon's place (§509); under its heading where Text stands over it
//            Pattern color        right after Pattern, while a pattern is in force there (§513): the ink it is drawn in
//
// §568: every colour a pair, its light mode's and its dark mode's; the font, the pattern and the blur one each. §613: the page
// shows and writes the half of the mode Karpo's taskbar is in (Looks.Dark), one row a colour (§568 to §612 two buttons in one
// row, its light half's and its dark half's).
//
// §574 (the person, 2026-11-08: "there should not be an additional appearance level above. it should just be the settings
// that can also be preset with the apply preset option."): every value is a setting with a value, nothing else — no
// "Default (…)", nothing said of where it comes from; each row shows the value in force (Looks.Of).
//
// §577 (the person, 2026-11-09: "you should be able to select the palettes or you can also select custom ... the custom
// settings should also always be saved"): the page (Rows) is Palette — Karpo's own, the four looks Karpo designed, Custom —,
// the one worn, whose values the rows show and write, each palette keeping its own (LookPalettes); a designed palette's
// Reset, back to its design (Custom has none; §610 beside Palette's dropdown, in its line). §547: the bar has its Background
// blur besides (BlurRow), right after its background.
//
// §583 (the person, 2026-11-09: "in the style dropdown show if a style has custom user configuration inside it"): each
// palette holding values that change it marked "Changed" in Palette's list and, the one worn, in its box.
//
// §592 (the person, 2026-11-09: "for the appearance settings page i want only one preview now, then there should be tabs
// underneath for the four different sections / groups of styles that you can change"): under Palette and its reset the page's
// one preview — the whole look at once —, then a tab for each part (§626 Taskbar, Taskbar Widgets, Flyouts, Flyout Widgets), each a
// level of its editor's rows (IsTab), the settings panel showing the one chosen under its tab strip.
//
// §613 (the person, 2026-11-09: "the appearance should actually only show for dark or light mode including all the
// settings. you can switch between those with a switcher at the top, either dark light or system"; "you should be able to
// reset individual properties to the style default"): Mode first (ModeRow: Dark, Light, System, the Appearance's own
// setting), the page in the mode it comes to. Each value the palette worn's design draws otherwise in that mode has its reset
// beside its control (ResetOf: Beside, Subtle, an icon button): pressed, the value is unset, the design's again. Custom's
// design is Karpo's own, so Custom's resets go back to Karpo's.

namespace Karpo.Core;

/// <summary>
/// §679 (the person, 2026-11-12: "the previews look shit. you cant really see how it would change anything. and i didnt mean
/// that you literaly have to render a preview of the whole app. it should be more like you did it for the appearance
/// settings"): what a choice of how a display's bar stands or behaves is pictured by — a piece of it, close up, where the
/// choice shows, never the whole display. None for a look's sample.
/// </summary>
public enum BarSketch
{
    None,

    /// <summary>Type: a short bar of stand-ins on a strip of desktop — a strip edge to edge, or islands floating off the edge.</summary>
    Type,

    /// <summary>Show: the bar's foot of a display, a window over it — stopping at the bar, under it, or the bar hidden but for its mark.</summary>
    Show,

    /// <summary>Position: a small display, a bar of a few stand-ins along the edge it stands at.</summary>
    Edge,

    /// <summary>
    /// §700 A display's layout: the layout's own bar, whole, a line of its own; nothing where the display shows none.
    /// </summary>
    Layout,

    /// <summary>
    /// §682 Animations: a flyout coming up over its button again and again at that speed, gliding in (Slower, Normal,
    /// Faster) or there at once (Off).
    /// </summary>
    Motion,

    /// <summary>
    /// Combine buttons: the app buttons of a few open windows, an app each or a window each with its title; when the taskbar is
    /// full, both — a few windows each with its title, then a full bar's an app each.
    /// </summary>
    Buttons,

    /// <summary>Show windows from: the app buttons of this desktop's windows, or every desktop's.</summary>
    Desktops,

    /// <summary>Open flyouts: a flyout open over a button of the bar, the pointer coming onto it and clicking or resting, played over and over.</summary>
    Opens,
}

/// <summary>
/// §536: the look rows edit, as the settings panel previews it over them: <paramref name="Config"/>'s Appearance (§567), §592
/// the whole of it at once, on a bar of <paramref name="Layout"/>'s kind and size. The live objects of the config the rows were
/// made of, so the preview reads every value as it is now — a colour being mixed too (§523), whose rows are made again of the
/// config it is tried on.
/// </summary>
public sealed record LookShown(Config Config, TaskbarLayout Layout)
{
    /// <summary>
    /// §551: the taskbar's right-click menu of these entries, drawn as <see cref="Layout"/>'s bar draws it (the Taskbar menu
    /// page's preview); null for a look's sample.
    /// </summary>
    public IReadOnlyList<BarMenuEntry>? Menu { get; init; }

    /// <summary>
    /// §628: <see cref="Layout"/>'s own bar as it stands, every widget of it where it stands (Add a layout's tile: a layout preset
    /// as it makes it), not a look's sample over stand-ins; false for a look's sample.
    /// </summary>
    public bool Itself { get; init; }

    /// <summary>
    /// §675: <see cref="Layout"/>'s bar (its widgets: one widget alone, a palette's stand-ins) drawn as tall as its picture,
    /// its widgets in its middle — a choice's picture in an inline picker, what that choice draws (Itself with it); false
    /// for a bar drawn as small as its picture is wide.
    /// </summary>
    public bool Zoomed { get; init; }

    /// <summary>
    /// §679: a piece of <see cref="Layout"/>'s bar close up where a choice of how it stands or behaves shows (Itself with it,
    /// <see cref="BarSketch"/>); None for none.
    /// </summary>
    public BarSketch Sketch { get; init; }

    /// <summary>§679: the display a sketch's edge and whether it shows Karpo are read of (Edge); null for none.</summary>
    public string? Display { get; init; }

    /// <summary>
    /// Every value the preview is drawn from, as words: the Appearance, the bar's kind and size; §551 a menu's entries, the layouts it offers and which is shown, whether the bars
    /// are locked; §628 a layout's own bar its widgets, each its kind, its section and what a pin opens. The same words, the same picture.
    /// </summary>
    public string Values() => string.Join('|',
        Menu is { } entries
            ? string.Join(';', entries.Select(entry => string.Join(',', entry.Kind, entry.Action, entry.Name, entry.Target, entry.Arguments, entry.Icon)))
                + "|" + string.Join(';', Config.Layouts.Select(layout => string.Join(',', layout.Id, layout.Name, layout.Icon)))
                + "|" + Layout.Id + "|" + Config.LockTaskbar
            : Zoomed ? string.Join(';', Layout.Islands.Select(island => System.Text.Json.JsonSerializer.Serialize(island, ConfigJson.Default.BarItem)))
            : Itself ? string.Join(';', Layout.Islands.Select(island => string.Join(',', island.Kind, island.Section, island.Pin?.Target, island.Folder?.Face?.Kind)))
            : "",
        Zoomed,
        Sketch == BarSketch.None ? "" : string.Join(',', Sketch, Display, Display is { } display ? $"{Config.EdgeOfScreen(display)},{Config.ScreenFor(display).Enabled}" : "",
            Layout.Show, Layout.Id, Layout.Combine, Layout.AllDesktops, Layout.OpenOnHover, Config.Animation),
        Layout.Type,
        Layout.Size,
        Config.Appearance is { } appearance ? System.Text.Json.JsonSerializer.Serialize(appearance, ConfigJson.Default.Appearance) : "");
}

/// <summary>§567: the Appearance page, and the one editor of a section; see the top of the file.</summary>
public static class LookRows
{
    /// <summary>The start of the page's rows' keys ("Appearance.widgets.background").</summary>
    public const string Key = "Appearance";

    /// <summary>§613: the page's Mode, the mode Karpo's taskbar is in.</summary>
    public const string ModeRow = Key + ".mode";

    /// <summary>§577: the page's Palette, the palette worn.</summary>
    public const string PaletteRow = Key + ".palette";

    /// <summary>§577: the page's reset of the palette worn to its design (none for Custom).</summary>
    public const string ResetRow = Key + ".reset";

    /// <summary>§613: a value's reset to its palette's design, after its row's key ("Appearance.widgets.font.reset").</summary>
    public const string ResetEnd = ".reset";

    /// <summary>§613: the Mode row's choices, in the switcher's order: Dark, Light, System.</summary>
    public static readonly TaskbarMode[] Modes = [TaskbarMode.Dark, TaskbarMode.Light, TaskbarMode.System];

    /// <summary>§517: an editor's headings, over its text's rows and over its colours'.</summary>
    public const string TextHeading = ".heading.text";
    public const string ColorsHeading = ".heading.colors";

    /// <summary>A part's rows' keys' start: "Appearance.widgets".</summary>
    public static string SectionKey(LookPart part) => Key + "." + LookParts.JsonName(part);

    /// <summary>§592: the end of a part's tab's key ("Appearance.widgets.tab").</summary>
    public const string Tab = ".tab";

    /// <summary>
    /// §622 (the person, 2026-11-09: "when hovering over an entry in the palette it should apply it / preview it with the
    /// whole taskbar ... the hovering should also work in other selectors like the font selector or color selectors"):
    /// whether a choice of the row <paramref name="key"/> is tried on while the pointer or the keys rest on it — every value of
    /// the Appearance (the palette, a font, a pattern, a colour's swatch), each a choice that changes only how the taskbar
    /// looks; nothing else's.
    /// </summary>
    public static bool TriedOn(string key) => key.StartsWith(Key + ".", StringComparison.Ordinal);

    /// <summary>§592: whether <paramref name="key"/> is a part's tab of the page, the level of its rows: the settings panel's tab of it.</summary>
    public static bool IsTab(string? key) => key is not null && key.StartsWith(Key + ".", StringComparison.Ordinal) && key.EndsWith(Tab, StringComparison.Ordinal);

    /// <summary>
    /// The Appearance page of <paramref name="config"/>, Windows being in its dark mode (<paramref name="windows"/>) or not, its
    /// preview on a bar of <paramref name="shown"/>'s kind and size: §613 Mode, then everything in the mode it comes to
    /// (<see cref="Looks.Dark"/>), its swatches and pickers too; §577 Palette, the palette worn (each its swatch as it is drawn
    /// with its values, §583 marked where its values change it); a designed palette's reset to its design, greyed while it is
    /// drawn as designed; §592 a tab for each part, a level of its editor's rows (<see cref="Editor"/>), each showing the one
    /// look (the panel's one preview over the tabs).
    /// </summary>
    public static IReadOnlyList<OptionRow> Rows(Config config, TaskbarLayout shown, bool windows)
    {
        var dark = Looks.Dark(config.Appearance, windows);
        var place = new OptionPlace(config, dark) { Layout = shown };
        var palettes = LookPalettes.Choices;
        var worn = LookPalettes.Worn(config);
        List<OptionRow> rows =
        [
            // §613: the switcher at the top, the Appearance's own setting: Windows' mode (System) by default.
            new(ModeRow, OptionRowKind.Choice, S.SettingsAppearanceMode)
            {
                Glyph = OptionGlyphs.Contrast,
                Slot = OptionSlot.Look,
                Segmented = true,
                Choices = [.. Modes.Select(ModeName)],
                ChoiceGlyphs = [OptionGlyphs.Moon, OptionGlyphs.Sun, OptionGlyphs.Monitor],
                Chosen = Array.IndexOf(Modes, config.Appearance?.Mode ?? TaskbarMode.System),
                Set = at => at >= 0 && at < Modes.Length && Looks.WriteMode(config, Modes[at]),
            },
            new(PaletteRow, OptionRowKind.Choice, S.SettingsLookPalette)
            {
                Glyph = OptionGlyphs.Styles,
                Slot = OptionSlot.Look,
                Choices = [.. palettes.Select(palette => palette.Name)],
                ChoiceSwatches = [.. palettes.Select(palette => Looks.Swatch(Looks.Of(config.Appearance?[palette.Id], palette.Drawn(dark), dark), LookPart.Taskbar, dark))],
                // §583 (the person, 2026-11-09: "in the style dropdown show if a style has custom user configuration inside
                // it"): each palette holding values that change it marked so; Custom only once it holds some.
                ChoiceMarks = [.. palettes.Select(palette => LookPalettes.Changed(config, palette) ? S.SettingsLookChanged : "")],
                Chosen = Array.IndexOf(palettes, worn),
                Set = at => at >= 0 && at < palettes.Length && LookPalettes.Wear(config, palettes[at]),
            },
            // §682 (the person, 2026-11-12: "animations should go into appearance settings"): how Karpo moves, under how it looks.
            KarpoMenu.AnimationRow(config),
        ];
        // §592: each part a tab, its editor's rows its level (its own Text and Colors headings left out: the tab says what they
        // are), the one look over them all.
        var look = new LookShown(config, shown);
        rows.AddRange(LookParts.All.Select(part => new OptionRow(SectionKey(part) + Tab, OptionRowKind.Level, LookParts.Name(part))
        {
            Glyph = LookParts.Glyph(part),
            Shows = look,
            Level = [.. Editor(place, Looks.Section(config, part), SectionKey(part)).Where(row => row.Kind != OptionRowKind.Heading)],
        }));
        // §610: a designed palette's reset, its words "Reset to default", its whole name "Reset Graphite to default" (Detail: its
        // tooltip, a screen reader's name). None for Custom. §673 (the person, 2026-11-12: "reset to default should be shown at
        // the end of the tabs in the appearance section. aligned to the right"): beside the parts' tabs (Beside), at their
        // strip's right end — no longer beside Palette's dropdown.
        if (worn.Resets)
        {
            rows.Add(new(ResetRow, OptionRowKind.Action, S.SettingsLookReset)
            {
                Glyph = OptionGlyphs.Reset,
                Slot = OptionSlot.Look,
                Detail = S.SettingsLookResetPalette(worn.Name),
                Enabled = LookPalettes.Changed(config, worn),
                Set = _ => LookPalettes.Reset(config, worn),
                Beside = true,
                // §634 (the person, 2026-11-09: "reset to default for the style should show a system confirmation modal"):
                // Windows' own question first, Cancel its default; a value's own reset (ResetOf) asks nothing.
                Ask = new DeleteQuestion(S.SettingsLookResetTitle(worn.Name), S.SettingsLookResetConfirm(worn.Name), S.SettingsLookResetButton, S.DockDeleteCancel, 0),
            });
        }
        return rows;
    }

    /// <summary>§613: what the switcher calls <paramref name="mode"/>: Windows' own words, "Dark", "Light", "System".</summary>
    public static string ModeName(TaskbarMode mode) => mode switch
    {
        TaskbarMode.Dark => S.SettingsAppearanceDark,
        TaskbarMode.Light => S.SettingsAppearanceLight,
        _ => S.SettingsAppearanceSystem,
    };

    /// <summary>
    /// §507: the one editor of the section kept at <paramref name="slot"/>, its rows keyed from <paramref name="key"/>, §574
    /// each the value in force (<see cref="Looks.Of(Config, bool)"/>: what the file leaves out the palette's), §613 in the mode
    /// <paramref name="place"/> is in, and each value its design draws otherwise its reset beside it. See the top of the file.
    /// </summary>
    public static IReadOnlyList<OptionRow> Editor(OptionPlace place, LookSlot slot, string key)
    {
        var part = slot.Part;
        var dark = place.Dark;
        // §613: the values in force in the mode shown, and what the palette worn's design draws in it (Custom's Karpo's own).
        var drawn = Looks.Of(place.Config, dark);
        var worn = drawn[part];
        var design = LookPalettes.Worn(place.Config).Drawn(dark)[part];
        List<OptionRow> rows = [];
        // §613: a row, then its reset beside it where its value is not the design's.
        void Add(OptionRow row, bool changed, Func<bool> undo)
        {
            rows.Add(row);
            if (changed)
            {
                rows.Add(ResetOf(row, undo));
            }
        }
        var text = LookParts.HasText(part);
        // §517: the text's rows under "Text", the colours' under "Colors", where the section has both.
        if (text)
        {
            rows.Add(new(key + TextHeading, OptionRowKind.Heading, S.SettingsSectionsText));
            // §638: the family's dropdown, its weight's beside it (Beside: one line), then the font's one reset beside them.
            var font = FontRow(slot, key + ".font", worn.Type);
            rows.Add(font);
            rows.Add(WeightRow(slot, key + ".weight", worn.Type));
            if (worn.Type != design.Type)
            {
                rows.Add(ResetOf(font, () => Looks.WriteFont(slot, null)));
            }
        }
        // §509: its swatch in its icon's place (MenuPickers), §613 of the mode shown. §513: the pattern's after the pattern,
        // which it is the ink of. §517: the text's colour with the text's rows, the others under "Colors".
        void Colour(Tone tone, Paint shown) => Add(
            OptionPickers.Paints(place, tone, ColourKey(key, tone), ToneNames.Name(tone), shown, paint => Looks.WritePaint(slot, tone, dark, paint), dark),
            worn[tone] != design[tone], () => Looks.WritePaint(slot, tone, dark, null));
        var tones = LookParts.TonesOf(part).Where(tone => tone != Tone.PatternColor).ToList();
        if (text && tones.Contains(Tone.Text))
        {
            Colour(Tone.Text, Shown(worn[Tone.Text], Tone.Text, dark));
        }
        if (text)
        {
            rows.Add(new(key + ColorsHeading, OptionRowKind.Heading, S.SettingsSectionsColors));
        }
        foreach (var tone in tones.Where(tone => !text || tone != Tone.Text))
        {
            // §649: an attached folder's ground unset is the bar's colour, solid: the value in force (§574).
            Colour(tone, tone == Tone.Ground && worn.Ground.IsWindows ? new Paint(Looks.Ground(drawn, dark)) : Shown(worn[tone], tone, dark));
            // §547: the bar its Background blur right after its background, which it is under.
            if (tone == Tone.Background && LookParts.HasBlur(part))
            {
                Add(BlurRow(slot, key + ".blur", worn), (worn.Blur ?? KarpoLook.Blur) != (design.Blur ?? KarpoLook.Blur), () => Looks.WriteBlur(slot, null));
            }
        }
        if (LookParts.HasPattern(part))
        {
            Add(PatternRow(place, slot, key + ".pattern", drawn), worn.Pattern != design.Pattern, () => Looks.WritePattern(slot, null));
            if (worn.Pattern != Pattern.None)
            {
                // Where the file says none: the ink the pattern is drawn in, its text's of its mode, fainter.
                Colour(Tone.PatternColor, worn.PatternColor is { IsWindows: false } said ? said
                    : Looks.PatternSwatch(drawn, part, worn.Pattern, dark).Pattern?.Ink is { } ink ? new Paint(ink) : default);
            }
        }
        return rows;
    }

    /// <summary>
    /// §613: <paramref name="row"/>'s reset to its palette's design, standing beside its control (Beside), an icon button
    /// (Subtle, Windows' subtle button): "Reset Background to default" its whole name; pressed, <paramref name="undo"/> unsets
    /// the value, the design's again.
    /// </summary>
    private static OptionRow ResetOf(OptionRow row, Func<bool> undo) => new(row.Key + ResetEnd, OptionRowKind.Action, S.SettingsLookReset)
    {
        Glyph = OptionGlyphs.Reset,
        Slot = OptionSlot.Look,
        Detail = S.SettingsLookResetValue(row.Title),
        Beside = true,
        Subtle = true,
        Set = _ => undo(),
    };

    /// <summary>§568: the key of section <paramref name="key"/>'s colour <paramref name="tone"/>: "Appearance.widgets.background".</summary>
    public static string ColourKey(string key, Tone tone) => key + "." + ToneNames.JsonName(tone);

    /// <summary>
    /// §574: the colour <paramref name="worn"/> (<paramref name="tone"/> in force in the mode <paramref name="dark"/>) as a
    /// value: as said, else what Karpo draws where it decides as it draws (its text, its accent; no outline: None).
    /// </summary>
    private static Paint Shown(Paint worn, Tone tone, bool dark) =>
        !worn.IsWindows ? worn : ColorPalette.Unset(tone, dark) is { } drawn ? new Paint(drawn) : Paint.Nothing;

    /// <summary>
    /// §638: the font's family: every family installed (<see cref="TextStyles.Families"/>), <paramref name="worn"/>'s (the one in
    /// force) among them where it is none of them; each drawn in the font it comes to (<see cref="OptionRow.ChoiceFaces"/>):
    /// picked, the weight in force where the family has it, else its nearest (<see cref="TextStyles.In"/>). §556 to §637 one
    /// choice of every weight of every family.
    /// </summary>
    private static OptionRow FontRow(LookSlot slot, string key, TextStyle worn)
    {
        var families = TextStyles.Families(worn);
        List<TextStyle> fonts = [.. families.Select(family => TextStyles.In(family, worn))];
        return new(key, OptionRowKind.Choice, S.SettingsAppearanceStylesFont)
        {
            Glyph = OptionGlyphs.Font,
            Slot = OptionSlot.Look,
            Choices = families,
            ChoiceFaces = fonts,
            Grows = true,
            Chosen = families.FindIndex(family => string.Equals(family, worn.Family, StringComparison.OrdinalIgnoreCase)),
            Set = at => at >= 0 && at < fonts.Count && Looks.WriteFont(slot, fonts[at]),
        };
    }

    /// <summary>
    /// §638: the font's weight, standing beside its family (Beside): the weights <paramref name="worn"/>'s family has
    /// (<see cref="TextStyles.Weights"/>), its own among them where it is none of them, each named (Light, Regular, Bold) and
    /// drawn in itself; a dropdown however few (Grows: Georgia's two are no segmented control).
    /// </summary>
    private static OptionRow WeightRow(LookSlot slot, string key, TextStyle worn)
    {
        var weights = TextStyles.Weights(worn.Family, worn.Weight);
        List<TextStyle> fonts = [.. weights.Select(weight => new TextStyle(worn.Family, weight))];
        // No glyph of its own: it stands in its family's line, whose icon is the font's (R13).
        return new(key, OptionRowKind.Choice, S.SettingsAppearanceStylesWeight)
        {
            Slot = OptionSlot.Look,
            Choices = [.. weights.Select(TextStyles.WeightName)],
            ChoiceFaces = fonts,
            Grows = true,
            Beside = true,
            Chosen = weights.IndexOf(worn.Weight),
            Set = at => at >= 0 && at < fonts.Count && Looks.WriteFont(slot, fonts[at]),
        };
    }

    /// <summary>
    /// §547: the bar's Background blur, how blurred the wallpaper under its see-through background is: a number 0 to
    /// 100 % (<see cref="KarpoLook.MostBlur"/>), the one in force (Karpo's palette's 30 where the file says none,
    /// <see cref="KarpoLook.Blur"/>); §568 one in both modes. Greyed, with why, where the bar as <paramref name="drawn"/> (§613
    /// in the mode shown) is not see-through — solid, or None —: nothing of the wallpaper is drawn under it then.
    /// </summary>
    private static OptionRow BlurRow(LookSlot slot, string key, PartPaints drawn)
    {
        var seeThrough = SurfaceStyle.OpacityOf(drawn.Background) is > 0 and < 100;
        return new(key, OptionRowKind.Number, S.SettingsLookBlur)
        {
            Glyph = OptionGlyphs.Picture,
            Slot = OptionSlot.Look,
            Value = drawn.Blur ?? KarpoLook.Blur,
            Least = 0,
            Most = KarpoLook.MostBlur,
            Step = 5,
            Unit = OptionUnit.Percent,
            Enabled = seeThrough,
            Why = seeThrough ? null : S.SettingsLookBlurWhy,
            Set = value => Looks.WriteBlur(slot, KarpoLook.Held(value)),
        };
    }

    /// <summary>
    /// §462: the pattern: None and every pattern, each with its swatch — the section's card in its colours wearing it, in the
    /// mode <paramref name="place"/> is drawn in —, the one in force chosen; a pick goes back (R5). §634 (the person,
    /// 2026-11-09: "the pattern selector should actually not be a dropdown but a grid of the patterns"): a grid of them, each
    /// tile the pattern itself (<see cref="OptionRow.Tiled"/>).
    /// </summary>
    private static OptionRow PatternRow(OptionPlace place, LookSlot slot, string key, LookPaints drawn)
    {
        var part = slot.Part;
        Pattern[] choices = [Pattern.None, .. Patterns.All];
        return new(key, OptionRowKind.Choice, S.SettingsAppearanceStylesPattern)
        {
            Slot = OptionSlot.Look,
            Choices = [.. choices.Select(Patterns.Name)],
            ChoiceSwatches = [.. choices.Select(pattern => Looks.PatternSwatch(drawn, part, pattern, place.Dark))],
            Tiled = true,
            Chosen = Array.IndexOf(choices, drawn[part].Pattern),
            Set = at => at >= 0 && at < choices.Length && Looks.WritePattern(slot, choices[at]),
        };
    }
}
