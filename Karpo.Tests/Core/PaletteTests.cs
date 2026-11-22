// §618 (the person, 2026-11-09: "add some more styles. the newly added styles should also incorporate stuff like transparency,
// patterns. dark backgrounds, light background. high contrast etc."): the eight palettes Karpo designed after its first five
// (Karpo.Core\Looks.cs, LookPalettes.All). Held here:
//
//  - **Whole in both modes**: every section's every colour said in each of Windows' modes, the font, the pattern and the blur
//    one in both, nothing lost written out as values (LookPalette.Drawn), its name its own in the language Karpo speaks.
//  - **Readable over any desktop**: every text 4.5:1 and every accent 3:1 on its card and on its line, a see-through card over
//    a black and a white wallpaper alike as over Windows' own, and Karpo draws the text as designed (no mix needed).
//  - **What each was made for**: see-through (Acrylic, Glacier), patterned (Blueprint, Moss, Midnight, Glacier), dark first
//    (Midnight's true black), light first (Daylight's white), high contrast (Contrast: black and white, 7:1, every card
//    and flyout outlined), the accent forward (Ember's cards); Windows' high contrast still wins over each.
//  - §624 (the person, 2026-11-09: "i dont like borders around the individual widgets (only inside a folder i mean) so the
//    border should always be defined for the folder itself"): no palette outlines a folder's lines; one that outlines its
//    cards on the bar outlines its folders (the Flyouts), and a menu wears that outline too.
//  - **Worn and kept**: each worn, written to the file and read back the same, its values kept under its own id, its reset.
//  - **The Palette dropdown's order**: Karpo, the neutrals, the see-through ones, the colours, the patterned, Custom last.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Paint = Karpo.Core.Paint;

namespace Karpo.Tests.Core;

public sealed class PaletteTests
{
    /// <summary>The designed palettes in the Palette dropdown's order (Custom after them).</summary>
    public static readonly string[] Order =
    [
        LookPalettes.KarpoId, LookPalettes.GraphiteId, LookPalettes.DaylightId, LookPalettes.MidnightId, LookPalettes.ContrastId,
        LookPalettes.AcrylicId, LookPalettes.GlacierId, LookPalettes.LiquidGlassId, LookPalettes.PaperId, LookPalettes.HarborId,
        LookPalettes.BloomId, LookPalettes.EmberId, LookPalettes.BlueprintId, LookPalettes.MossId,
    ];

    /// <summary>§618's eight.</summary>
    public static readonly string[] Added =
    [
        LookPalettes.DaylightId, LookPalettes.MidnightId, LookPalettes.ContrastId, LookPalettes.AcrylicId, LookPalettes.GlacierId,
        LookPalettes.EmberId, LookPalettes.BlueprintId, LookPalettes.MossId,
    ];

    private static readonly Argb Black = new(255, 0, 0, 0), White = new(255, 255, 255, 255);

    private static LookPalette Of(string id) => LookPalettes.Of(id)!;

    private static IEnumerable<(LookPalette Palette, bool Dark, string What)> Each(IEnumerable<string> ids) =>
        ids.Select(Of).SelectMany(palette => new[] { false, true }.Select(dark => (palette, dark, $"{palette.Id} in {(dark ? "dark" : "light")}")));

    /// <summary>
    /// Each of the eight says every colour of every section in both modes — the bar's background and outline, the cards'
    /// background, outline, text and accent, the lines' (§624 no outline), the flyouts' outline; a pattern its colour —, the font, the pattern and the
    /// blur one in both; its two designs differ; written out as values and laid over Karpo's own it is drawn as designed; and
    /// its name is its own.
    /// </summary>
    [Fact]
    public void EachNewPaletteIsWholeInBothModes()
    {
        Assert.Equal(8, Added.Length);
        foreach (var (palette, dark, what) in Each(Added))
        {
            var design = palette.In(dark);
            Assert.True(design.Taskbar.Background.Color is not null, $"{what}: the bar's background");
            Assert.False(design.Taskbar.Outline.IsWindows, $"{what}: the bar's outline");
            Assert.False(design.Flyouts.Outline.IsWindows, $"{what}: the flyouts' outline");
            foreach (var part in new[] { LookPart.Widgets, LookPart.FolderWidgets })
            {
                var plate = design[part];
                Assert.True(plate.Background.Color is not null && plate.Text.Color is not null && plate.Accent.Color is not null, $"{what}: {part}'s colours");
                Assert.True(part == LookPart.Widgets != plate.Outline.IsWindows, $"{what}: {part}'s outline");
                Assert.True(plate.Pattern == Pattern.None || plate.PatternColor.IsWindows, $"{what}: {part}'s pattern in its text's ink");
            }
            Assert.True(design.Taskbar.Pattern == Pattern.None || design.Taskbar.PatternColor.Color is not null, $"{what}: the bar's pattern its colour");
            // Written out and laid over Karpo's own: as designed, the bar's blur Karpo's where it says none.
            var drawn = palette.Drawn(dark);
            Assert.Equal(design.Taskbar with { Blur = design.Taskbar.Blur ?? KarpoLook.Blur }, drawn.Taskbar);
            Assert.Equal((design.Widgets, design.Flyouts, design.FolderWidgets), (drawn.Widgets, drawn.Flyouts, drawn.FolderWidgets));
        }
        foreach (var palette in Added.Select(Of))
        {
            Assert.NotEqual(palette.Light, palette.Dark);
            foreach (var part in LookParts.All)
            {
                Assert.Equal((palette.Light[part].Pattern, palette.Light[part].Type, palette.Light[part].Blur), (palette.Dark[part].Pattern, palette.Dark[part].Type, palette.Dark[part].Blur));
            }
            Assert.False(string.IsNullOrWhiteSpace(palette.Name));
        }
        Assert.Equal(LookPalettes.Choices.Length, LookPalettes.Choices.Select(palette => palette.Name).Distinct().Count());
        Assert.Equal(["Daylight", "Midnight", "Contrast", "Acrylic", "Glacier", "Ember", "Blueprint", "Moss"],
            Added.Select(id => S.Lookup("settings.look.palette" + char.ToUpperInvariant(id[0]) + id[1..], "en")));
    }

    /// <summary>
    /// Every designed palette's text 4.5:1 and accent 3:1 on its cards and its lines in both modes, where it stands: §464 a card
    /// of its own background on the desktop — Windows' own surface, and a black and a white wallpaper, so a see-through card
    /// reads over any picture —; and Karpo draws the text and the accent as designed, none mixed to read.
    /// </summary>
    [Fact]
    public void EveryTextReadsOverAnyWallpaper()
    {
        foreach (var (palette, dark, what) in Each(LookPalettes.All.Skip(1).Select(palette => palette.Id)))
        {
            var design = palette.In(dark);
            foreach (var part in new[] { LookPart.Widgets, LookPart.FolderWidgets })
            {
                var plate = design[part];
                foreach (var desktop in new[] { ToneRules.WindowsBackground(dark), Black, White })
                {
                    var card = ToneRules.Seen(plate.Background.Color, desktop);
                    var text = ToneRules.Contrast(plate.Text.Color!.Value, card);
                    Assert.True(text >= ToneRules.TextContrast, $"{what}, {part} over {desktop}: text {text:0.00}:1");
                    // §704: Liquid glass says no accent: the system's, which Windows keeps readable on its own surfaces.
                    if (plate.Accent.Color is { } said)
                    {
                        var accent = ToneRules.Contrast(said, card);
                        Assert.True(accent >= ToneRules.AccentContrast, $"{what}, {part} over {desktop}: accent {accent:0.00}:1");
                    }
                }
            }
            var (drawn, _) = ToneRules.Dress(new Dressing(design.Taskbar, design.Widgets), dark);
            Assert.Equal((design.Widgets.Text.Color, design.Widgets.Accent.Color), (drawn.Text, drawn.Accent));
        }
    }

    /// <summary>
    /// What each was made for. See-through: Acrylic and Glacier, the bar no more than 60 % over the wallpaper, blurred more than
    /// Karpo's own, their cards see-through too. Patterned: Blueprint's grid and Moss's contours on the bar and its cards, Midnight's
    /// dots and Glacier's grain on the bar, each bar's in a colour of its own. Dark first: Midnight's dark bar true black, opaque.
    /// Light first: Daylight's light bar and cards white. High contrast: Contrast black and white, its text 7:1 and its accent
    /// 4.5:1, every card and flyout outlined in its text's colour, §624 its folders' lines not (the folder's outline is theirs). The accent forward: Ember's cards on the bar its colour,
    /// dark under a light bar. Windows' high contrast wins over every one, as over Karpo's own.
    /// </summary>
    [Fact]
    public void EachIsWhatItWasMadeFor()
    {
        foreach (var (palette, dark, what) in Each([LookPalettes.AcrylicId, LookPalettes.GlacierId]))
        {
            var design = palette.In(dark);
            Assert.True(design.Taskbar.Background.Color!.Value.A <= 0x99, $"{what}: the bar not see-through");
            Assert.True(design.Taskbar.Blur > KarpoLook.Blur, $"{what}: the bar's blur");
            Assert.True(design.Widgets.Background.Color!.Value.A < 255 && design.FolderWidgets.Background.Color!.Value.A < 255, $"{what}: the cards not see-through");
        }
        foreach (var (id, pattern, cards) in new[] { (LookPalettes.BlueprintId, Pattern.Grid, true), (LookPalettes.MossId, Pattern.Contours, true), (LookPalettes.MidnightId, Pattern.Dots, false), (LookPalettes.GlacierId, Pattern.Grain, false) })
        {
            foreach (var dark in new[] { false, true })
            {
                var design = Of(id).In(dark);
                Assert.Equal((pattern, true, cards ? pattern : Pattern.None), (design.Taskbar.Pattern, design.Taskbar.PatternColor.Color is not null, design.Widgets.Pattern));
            }
        }
        Assert.Equal(Paint.Parse("#000000"), Of(LookPalettes.MidnightId).Dark.Taskbar.Background);
        var daylight = Of(LookPalettes.DaylightId).Light;
        Assert.Equal((255, 255, 255), (daylight.Taskbar.Background.Color!.Value.R, daylight.Taskbar.Background.Color!.Value.G, daylight.Taskbar.Background.Color!.Value.B));
        Assert.Equal(Paint.Parse("#FFFFFF"), daylight.Widgets.Background);

        foreach (var (palette, dark, what) in Each([LookPalettes.ContrastId]))
        {
            var design = palette.In(dark);
            var (ground, ink) = dark ? (Black, White) : (White, Black);
            Assert.Equal((new Paint(ground), ToneRules.IsDark(ground)), (design.Taskbar.Background, dark));
            Assert.Equal(new Paint(ink), design.Flyouts.Outline);
            foreach (var plate in new[] { design.Widgets, design.FolderWidgets })
            {
                Assert.Equal((new Paint(ground), new Paint(ink), plate == design.Widgets ? new Paint(ink) : default), (plate.Background, plate.Text, plate.Outline));
                Assert.True(ToneRules.Contrast(plate.Accent.Color!.Value, ground) >= ToneRules.TextContrast, $"{what}: its accent");
                Assert.True(ToneRules.Contrast(plate.Text.Color!.Value, ground) >= 7, $"{what}: its text");
            }
        }

        var ember = Of(LookPalettes.EmberId);
        Assert.True(ToneRules.IsDark(ember.Light.Widgets.Background.Color!.Value) && !ToneRules.IsDark(ember.Light.Taskbar.Background.Color!.Value with { A = 255 }));
        foreach (var (palette, dark, what) in Each([LookPalettes.EmberId]))
        {
            var card = palette.In(dark).Widgets.Background.Color!.Value;
            Assert.True(card.R > card.G * 2 && card.G > card.B, $"{what}: its cards not ember");
        }

        foreach (var id in Added)
        {
            var config = new Config();
            Assert.True(LookPalettes.Wear(config, Of(id)));
            foreach (var dark in new[] { false, true })
            {
                Assert.True((TestLooks(dark) with { HighContrast = true }).Wearing(config).Colors.IsAutomatic, id);
            }
        }
    }

    /// <summary>
    /// §624: no palette outlines a folder's lines, in either mode, as designed or drawn, and the Appearance has no Outline for
    /// them to set: a line's outline said is never kept nor read (the file's dropped as it is read). Where a palette wants a
    /// border — its cards on the bar outlined — its folders have one, the Flyouts' (Daylight, Midnight, Contrast, Acrylic,
    /// Glacier; Blueprint's folders too), and a menu wears the Flyouts' outline as a folder does.
    /// </summary>
    [Fact]
    public void AFoldersBorderIsTheFoldersNeverItsLines()
    {
        Assert.DoesNotContain(Tone.Outline, LookParts.TonesOf(LookPart.FolderWidgets));
        foreach (var (palette, dark, what) in Each(LookPalettes.Choices.Select(palette => palette.Id)))
        {
            var (design, drawn) = (palette.In(dark), palette.Drawn(dark));
            Assert.True(design.FolderWidgets.Outline.IsWindows && drawn.FolderWidgets.Outline.IsWindows, $"{what}: its lines outlined");
            if (design.Widgets.Outline.Color is { A: > 0 })
            {
                Assert.True(design.Flyouts.Outline.Color is { A: > 0 }, $"{what}: its cards outlined, its folders not");
            }
            var look = TestLooks(dark).Wearing(new Config { Appearance = new Appearance { Palette = palette.Id } });
            var flyout = look.Flyout();
            Assert.Null(flyout.Colors.CardOutline);
            Assert.Equal(flyout.Colors.FlyoutOutline, look.Colors.FlyoutOutline);
        }
        Assert.Equal(
            [LookPalettes.DaylightId, LookPalettes.MidnightId, LookPalettes.ContrastId, LookPalettes.AcrylicId, LookPalettes.GlacierId, LookPalettes.LiquidGlassId, LookPalettes.BlueprintId],
            LookPalettes.All.Where(palette => palette.Light.Flyouts.Outline.Color is { A: > 0 } || palette.Dark.Flyouts.Outline.Color is { A: > 0 }).Select(palette => palette.Id));

        // An outline of the lines said, in the file or set: never kept, never drawn.
        var config = Configs.Repaired("""
            { "appearance": { "palette": "contrast", "palettes": { "contrast": { "folderWidgets": { "outline": { "dark": "#FF0000" }, "text": { "dark": "#FFFF00" } } } } } }
            """);
        Assert.Equal(((ColorPair?)null, "#FFFF00"), (config.Appearance!["contrast"]!.FolderWidgets!.Outline, config.Appearance["contrast"]!.FolderWidgets!.Text!.Dark));
        Assert.False(Looks.WritePaint(Looks.Section(config, LookPart.FolderWidgets), Tone.Outline, dark: true, new Paint(new Argb(255, 255, 0, 0))));
        Assert.True(Looks.Of(config, dark: true).FolderWidgets.Outline.IsWindows);
        Assert.DoesNotContain("#FF0000", config.ToJson(), StringComparison.Ordinal);
    }

    private static Look TestLooks(bool dark) => Karpo.Tests.Folders.TestLooks.LookFor(dark);

    /// <summary>
    /// Every palette worn in turn: the file says it (Karpo's own nothing), reads back the same and wears it, drawn as designed in
    /// both modes; a value changed is kept under its own id and read back, its reset (Custom none) draws it as designed again.
    /// </summary>
    [Fact]
    public void EachPaletteWornIsKeptInTheFile()
    {
        var navy = new Paint(new Argb(255, 0x1E, 0x3A, 0x5F));
        foreach (var palette in LookPalettes.Choices)
        {
            var config = new Config();
            _ = LookPalettes.Wear(config, palette);
            var json = config.ToJson();
            Assert.Equal(palette.Id != LookPalettes.KarpoId, json.Contains($"\"palette\": \"{palette.Id}\"", StringComparison.Ordinal));
            // A new config is repaired on its first read (its layouts); the Appearance comes back as written.
            var back = Configs.RoundTrip(config, out var changed);
            Assert.Equal(json.Contains("\"appearance\"", StringComparison.Ordinal), back.ToJson().Contains($"\"palette\": \"{palette.Id}\"", StringComparison.Ordinal));
            Assert.Same(palette, LookPalettes.Worn(back));
            Assert.Equal((palette.Drawn(false), palette.Drawn(true)), (Looks.Of(back, dark: false), Looks.Of(back, dark: true)));

            Assert.True(Looks.WritePaint(Looks.Section(back, LookPart.Widgets), Tone.Background, dark: true, navy), palette.Id);
            var again = Configs.RoundTrip(back, out changed);
            Assert.False(changed, palette.Id);
            Assert.Equal("#1E3A5F", again.Appearance![palette.Id]!.Widgets!.Background!.Dark);
            Assert.Equal(navy, Looks.Of(again, dark: true).Widgets.Background);
            Assert.Equal(palette.Drawn(false), Looks.Of(again, dark: false));
            Assert.Equal(palette.Resets, LookPalettes.Reset(again, palette));
            Assert.Equal(palette.Resets ? palette.Drawn(true) : Looks.Of(back, dark: true), Looks.Of(again, dark: true));
        }
    }

    /// <summary>
    /// The Palette dropdown lists every palette in its order — Karpo's own, the neutrals (Graphite, Daylight, Midnight,
    /// Contrast), the see-through (Acrylic, Glacier), the colours (Paper, Harbor, Bloom, Ember), the patterned (Blueprint, Moss),
    /// Custom last — each its name and its swatch; a pick wears it.
    /// </summary>
    [Fact]
    public void ThePaletteDropdownListsThemInOrder()
    {
        var config = new Config();
        OptionRow Palette() => OptionRows.Find(BarLooks.Rows(config, new BarSpot("display", "One"), dark: false, scope: LooksScope.Appearance), LookRows.PaletteRow)!;
        Assert.Equal([.. Order, LookPalettes.CustomId], LookPalettes.Choices.Select(palette => palette.Id));
        Assert.Equal(LookPalettes.Choices.Select(palette => palette.Name), Palette().Choices);
        Assert.Equal(LookPalettes.Choices.Length, Palette().ChoiceSwatches.Count);
        Assert.Equal(S.SettingsLookCustom, Palette().Choices[^1]);
        foreach (var id in Added)
        {
            var at = Array.IndexOf(LookPalettes.Choices, Of(id));
            Assert.True(Palette().Set!(at), id);
            Assert.Equal((at, id), (Palette().Chosen, LookPalettes.Worn(config).Id));
        }
    }
}
