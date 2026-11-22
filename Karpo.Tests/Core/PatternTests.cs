// The patterns (Karpo.Core\Patterns.cs, docs\widgets.md §462; the person, 2026-10-31: "you should be able to set a
// background pattern for the folder widgets and for the taskbar. make up some default patterns. the pattern is part of the
// styles settings"). Held here:
//
//  - **Each tiles seamlessly**: the endless sheet is the same a period on, so a tile's opposite edges meet as any two
//    neighbouring pixels of it do; each draws something and leaves room; a grid's line is whole pixels at every scale.
//  - **Quiet and readable**: no pattern's ink takes more than ToneRules.PatternMost of its colour, and a text made readable
//    on its card is as readable where the pattern runs under it, in every palette, in both modes, and in colours picked at
//    random.
//  - **A look's value** (§507): a section's or a widget's pattern in each set, unset until said, "none" over what it would
//    inherit, the file's word, the panel has none, an unknown word reads unset.

using Paint = Karpo.Core.Paint;

namespace Karpo.Tests.Core;

public sealed class PatternTests
{
    private static readonly float[] Scales = [1f, 1.25f, 1.5f, 1.75f, 2f, 2.25f, 3f];

    /// <summary>The sheet a period on is the sheet: across and down, every pixel of the tile, at every scale.</summary>
    [Fact]
    public void EveryPatternTilesSeamlessly()
    {
        foreach (var pattern in Patterns.All)
        {
            foreach (var scale in Scales)
            {
                var tile = Patterns.Tile(pattern, scale);
                Assert.Equal(Patterns.Period(pattern, scale), (tile.Width, tile.Height));
                for (var y = 0; y < tile.Height; y++)
                {
                    for (var x = 0; x < tile.Width; x++)
                    {
                        var here = Patterns.Coverage(pattern, scale, x, y);
                        Assert.Equal(here, Patterns.Coverage(pattern, scale, x + tile.Width, y), 6);
                        Assert.Equal(here, Patterns.Coverage(pattern, scale, x, y - tile.Height), 6);
                        Assert.Equal(tile.At(x, y), tile.At(x - tile.Width, y + (3 * tile.Height)));
                    }
                }
            }
        }
    }

    /// <summary>
    /// A tile's opposite edges meet as its neighbouring pixels do: the step from its last column to its first, and from its
    /// last row to its first, is no bigger than the biggest step between two columns (rows) inside it — no seam to see.
    /// </summary>
    [Fact]
    public void ATilesOppositeEdgesMeet()
    {
        foreach (var pattern in Patterns.All)
        {
            foreach (var scale in Scales)
            {
                var tile = Patterns.Tile(pattern, scale);
                int Step(Func<int, int, byte> at, int along, int across, int from, int to)
                {
                    var most = 0;
                    for (var i = 0; i < across; i++)
                    {
                        most = Math.Max(most, Math.Abs(at(from, i) - at(to, i)));
                    }
                    return most;
                }
                byte Column(int x, int y) => tile.At(x, y);
                byte Row(int y, int x) => tile.At(x, y);
                var inside = Enumerable.Range(0, tile.Width - 1).Max(x => Step(Column, tile.Width, tile.Height, x, x + 1));
                Assert.True(Step(Column, tile.Width, tile.Height, tile.Width - 1, 0) <= inside, $"{pattern} at {scale}: a seam across");
                var down = Enumerable.Range(0, tile.Height - 1).Max(y => Step(Row, tile.Height, tile.Width, y, y + 1));
                Assert.True(Step(Row, tile.Height, tile.Width, tile.Height - 1, 0) <= Math.Max(down, inside), $"{pattern} at {scale}: a seam down");
            }
        }
    }

    /// <summary>Every pattern draws something and leaves most of its card bare, and none is two alike.</summary>
    [Fact]
    public void EveryPatternDrawsSomethingAndLeavesRoom()
    {
        var seen = new HashSet<string>();
        foreach (var pattern in Patterns.All)
        {
            foreach (var scale in Scales)
            {
                var tile = Patterns.Tile(pattern, scale);
                var mean = tile.Coverage.Average(value => value / 255.0);
                Assert.True(mean is > 0.02 and < 0.6, $"{pattern} at {scale}: covers {mean:P0}");
                Assert.True(seen.Add($"{scale}:{Convert.ToBase64String(tile.Coverage)}"), $"{pattern} at {scale} is another's");
            }
        }
        Assert.Equal(Patterns.All.Length, Patterns.All.Select(Patterns.Name).Distinct().Count());
        Assert.DoesNotContain(Pattern.None, Patterns.All);
    }

    /// <summary>A grid's lines are whole pixels, fully inked, at every scale: crisp at 100, 150 and 225 %, a px, a px and two.</summary>
    [Fact]
    public void AGridsLinesAreWholePixels()
    {
        foreach (var (scale, thick) in new[] { (1f, 1), (1.5f, 1), (2.25f, 2) })
        {
            var tile = Patterns.Tile(Pattern.Grid, scale);
            var middle = tile.Height / 2;
            Assert.Equal([.. Enumerable.Repeat((byte)255, thick), .. Enumerable.Repeat((byte)0, tile.Width - thick)], Enumerable.Range(0, tile.Width).Select(x => tile.At(x, middle)));
        }
    }

    /// <summary>No pattern's ink is more than <see cref="ToneRules.PatternMost"/> of its colour; a light ink less; None none.</summary>
    [Fact]
    public void APatternsInkIsCapped()
    {
        Assert.Null(ToneRules.PatternOf(Pattern.None, new Argb(255, 0, 0, 0)));
        Assert.Null(ToneRules.PatternOf((Pattern)99, new Argb(255, 0, 0, 0)));
        foreach (var pattern in Patterns.All)
        {
            Assert.InRange(Patterns.Strength(pattern), 0.01, ToneRules.PatternMost);
            var dark = ToneRules.PatternOf(pattern, new Argb(255, 0x10, 0x10, 0x10))!.Value;
            var light = ToneRules.PatternOf(pattern, new Argb(255, 0xF0, 0xF0, 0xF0))!.Value;
            Assert.True(dark.Ink.A <= Math.Ceiling(ToneRules.PatternMost * 255), $"{pattern}: {dark.Ink}");
            Assert.True(light.Ink.A < dark.Ink.A, $"{pattern}: a light ink as strong as a dark one");
            Assert.Equal((0x10, 0x10, 0x10), (dark.Ink.R, dark.Ink.G, dark.Ink.B));
        }
    }

    /// <summary>
    /// The text and the accent on a card dressed in <paramref name="dressing"/> are never less readable where a pattern runs
    /// under them — the card's, the bar's under a card that lets it through — than on the same surface bare, up to 4.5:1 (3:1);
    /// with <paramref name="whole"/>, every pattern drawn at its full strength and the text 4.5:1 over it.
    /// </summary>
    private static void ReadableOverItsPattern(Dressing dressing, bool dark, string what, bool whole = false)
    {
        var (drawn, mode) = ToneRules.Dress(dressing, dark);
        // §464: a card of its own background stands on the desktop (Windows' own surface of the mode in its place), not on
        // the bar or its pattern; one of Windows' own on the bar.
        var onBar = dressing.Plate.Background.IsWindows;
        var under = onBar ? ToneRules.Seen(drawn.Taskbar, ToneRules.WindowsBackground(mode)) : ToneRules.WindowsBackground(dark);
        Argb[] On(Argb ground) => drawn.Card is { A: >= 128 } solid ? [ToneRules.Seen(solid, ground)] : drawn.Card is { } faint ? [ground, ToneRules.Seen(faint, ground)] : [ground];
        var pairs = new List<(Argb Bare, Argb Lined)>();
        var bare = On(under).ToList();
        if (onBar && drawn.TaskbarPattern is { } ground)
        {
            var lined = On(ToneRules.Blend(ground.Ink, under));
            pairs.AddRange(On(under).Zip(lined));
            bare.AddRange(lined);
        }
        if (drawn.CardPattern is { } card)
        {
            pairs.AddRange(bare.Select(surface => (surface, ToneRules.Blend(card.Ink, surface))));
        }
        if (whole)
        {
            Assert.Equal(ToneRules.PatternOf(dressing.Plate.Pattern, dressing.Plate.Text.Color!.Value), drawn.CardPattern);
        }
        foreach (var (ink, ratio) in new[] { (drawn.Text, ToneRules.TextContrast), (drawn.Accent, ToneRules.AccentContrast) })
        {
            if (ink is not { } opaque)
            {
                continue;
            }
            opaque = opaque with { A = 255 };
            foreach (var (plain, lined) in pairs)
            {
                var least = whole ? ratio : Math.Min(ratio, ToneRules.Contrast(opaque, plain));
                Assert.True(ToneRules.Contrast(opaque, lined) >= least - 0.01, $"{what}: {ToneRules.Contrast(opaque, lined):0.00}:1 over its pattern, {ToneRules.Contrast(opaque, plain):0.00}:1 bare");
            }
        }
    }

    /// <summary>Windows' own and every palette, light and dark (§509), wearing every pattern on its bar and every card, in both of Windows' modes: every pattern whole, its text 4.5:1 and its accent 3:1 over it.</summary>
    [Fact]
    public void PalettesReadOverEveryPattern()
    {
        foreach (var (id, paints) in LookPalettes.All.SelectMany(palette => new[] { (palette.Id + "-light", palette.Light), (palette.Id + "-dark", palette.Dark) }).Prepend(("windows", default(LookPaints))))
        {
            foreach (var dark in new[] { false, true })
            {
                foreach (var pattern in Patterns.All)
                {
                    foreach (var (ground, plate) in new[] { (paints.Taskbar, paints.Widgets), (paints.Flyouts, paints.FolderWidgets) })
                    {
                        // §574: Karpo's own palette, as Windows' own, leaves its text to be drawn: not whole.
                        ReadableOverItsPattern(new Dressing(ground with { Pattern = pattern }, plate with { Pattern = pattern }), dark, $"{id} {(dark ? "dark" : "light")} {pattern}", whole: id != "windows" && !id.StartsWith(LookPalettes.KarpoId + "-", StringComparison.Ordinal));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Colours picked at random — a card of any colour and opacity, a text and an accent of any colour, a bar of any — wearing
    /// any pattern: the text and the accent are never less readable over a pattern than bare, the pattern fainter where no colour
    /// reads on it as well (regression: the text was made readable on the plain card alone, and a pattern brought a text a hair
    /// over 4.5:1 under it; then a mid-tone card, on which no text reads, read worse with its pattern).
    /// </summary>
    [Fact]
    public void AnyColoursReadOverAnyPattern()
    {
        var random = new Random(460);
        Argb Any(byte least = 255) => new((byte)random.Next(least, 256), (byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256));
        for (var round = 0; round < 3000; round++)
        {
            var pattern = Patterns.All[random.Next(Patterns.All.Length)];
            var ground = new PartPaints(new Paint(Any(120)), Pattern: Patterns.All[random.Next(Patterns.All.Length)]);
            var plate = new PartPaints(new Paint(Any(60)), Paint.Nothing, new Paint(Any()), new Paint(Any()), pattern);
            ReadableOverItsPattern(new Dressing(ground, plate), round % 2 == 0, $"round {round}");
        }
    }

    /// <summary>
    /// §507: a pattern is a look's value: unset until said (Karpo's own None), "none" over what it would come to, the file's
    /// word, read back; the Flyouts have none; a word this build does not know unset.
    /// </summary>
    [Fact]
    public void APatternIsALooksValue()
    {
        var config = new Config();
        var lines = Looks.Section(config, LookPart.FolderWidgets);
        var bar = Looks.Section(config, LookPart.Taskbar);
        Assert.Equal(Pattern.None, Looks.Of(config, dark: true).Taskbar.Pattern);
        Assert.True(Looks.WritePattern(lines, Pattern.Waves));
        Assert.False(Looks.WritePattern(lines, Pattern.Waves));
        Assert.True(Looks.WritePattern(bar, Pattern.Grain));
        Assert.False(Looks.WritePattern(Looks.Section(config, LookPart.Flyouts), Pattern.Dots));
        var drawn = Looks.Of(config, dark: true);
        Assert.Equal((Pattern.Waves, Pattern.Grain, Pattern.None), (drawn.FolderWidgets.Pattern, drawn.Taskbar.Pattern, drawn.Widgets.Pattern));

        // The file: the word, read back the same.
        var json = config.ToJson();
        Assert.Contains("\"pattern\": \"waves\"", json, StringComparison.Ordinal);
        Assert.Contains("\"pattern\": \"grain\"", json, StringComparison.Ordinal);
        var back = Configs.RoundTrip(config, out _);
        Assert.Equal(drawn, Looks.Of(back, dark: true));

        // A word this build does not know, and a pattern on a section with none: unset, the rest of the look kept.
        var odd = Configs.Repaired("""{ "appearance": { "palettes": { "karpo": { "widgets": { "pattern": "paisley", "background": { "dark": "#FF0000" } }, "flyouts": { "pattern": "dots", "outline": { "dark": "#000000" } } } } } }""");
        var x = Looks.Of(odd, dark: true);
        Assert.Equal((Pattern.None, Pattern.None), (x.Widgets.Pattern, x.Flyouts.Pattern));
        Assert.Equal(new Argb(255, 255, 0, 0), x.Widgets.Background.Color);
    }

    /// <summary>An Appearance that has patterns dresses its looks with them: its bar's, its cards' and its folders' lines', each in its own ink; high contrast none.</summary>
    [Fact]
    public void ALookWearsItsPatterns()
    {
        var config = new Config();
        _ = LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.PaperId)!);
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Taskbar), Pattern.Grain));
        Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.FolderWidgets), Pattern.Contours));
        var look = Karpo.Tests.Folders.TestLooks.LookFor(dark: false).Wearing(config);
        Assert.Equal(Pattern.Grain, look.Colors.TaskbarPattern?.Pattern);
        Assert.Null(look.Colors.CardPattern);
        var folder = look.Flyout();
        Assert.Equal(Pattern.Contours, folder.Colors.CardPattern?.Pattern);
        // In the ink of its text, quiet.
        var text = Looks.Of(config, dark: false).FolderWidgets.Text.Color!.Value;
        Assert.Equal((text.R, text.G, text.B), (folder.Colors.CardPattern!.Value.Ink.R, folder.Colors.CardPattern.Value.Ink.G, folder.Colors.CardPattern.Value.Ink.B));
        Assert.Null((Karpo.Tests.Folders.TestLooks.LookFor(dark: false) with { HighContrast = true }).Wearing(config).Colors.TaskbarPattern);
    }
}
