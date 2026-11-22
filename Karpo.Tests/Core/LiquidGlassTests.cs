// §704 (the owner, 2026-11-15: a "Liquid glass" look, Apple's, as Themia's [S-2123]): the material's values, its adaptive
// tint (the least that keeps the text 4.5:1 over what is behind), the reading of a backdrop's light, the palette after
// Glacier, and how the dock wears it (the system's accent, the tint following the wallpaper, a colour chosen kept).

using Karpo.Dock;
using Color = Windows.UI.Color;
using Paint = Karpo.Core.Paint;

namespace Karpo.Tests.Core;

public sealed class LiquidGlassTests
{
    private static readonly Argb White = new(255, 255, 255, 255);

    /// <summary>The glass's text over the tint at <paramref name="alpha"/> over a backdrop of luminance <paramref name="behind"/>, mixed in sRGB.</summary>
    private static double Reads(GlassLook glass, double behind, double alpha)
    {
        static double Srgb(double l) => l <= 0.0031308 ? l * 12.92 : (1.055 * Math.Pow(l, 1 / 2.4)) - 0.055;
        static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        var (back, tint) = (Srgb(behind), Srgb(ToneRules.Luminance(glass.Tint)));
        return LiquidGlass.Contrast(Linear(back + ((tint - back) * alpha)), ToneRules.Luminance(glass.Text));
    }

    /// <summary>Each mode's values are Themia's iOS 26 glass ([S-2123]): the clear tint, the glow, the lens's bands, the rim, the dividers, the lift.</summary>
    [Fact]
    public void EachModesValuesAreThemias()
    {
        var (light, dark) = (LiquidGlass.Light, LiquidGlass.Dark);
        Assert.Equal((White, 0.06, 0.70, new Argb(255, 0x1A, 0x1A, 0x1A)), (light.Tint, light.TintMin, light.TintMax, light.Text));
        Assert.Equal((new Argb(255, 0x1C, 0x1C, 0x1E), 0.14, 0.78, White), (dark.Tint, dark.TintMin, dark.TintMax, dark.Text));
        Assert.Equal([new(0, 0.16), new(0.35, 0.05), new(1, 0)], light.Glow);
        Assert.Equal([new(0, 0.09), new(0.35, 0.03), new(1, 0)], dark.Glow);
        Assert.Equal([new(0, 0.80), new(0.12, 0.40), new(0.5, 0.16), new(0.88, 0.34), new(1, 0.66)], light.Lens);
        Assert.Equal([new(0, 0.60), new(0.12, 0.26), new(0.5, 0.10), new(0.88, 0.22), new(1, 0.46)], dark.Lens);
        Assert.Equal([new(0, 0.92), new(0.06, 0.58), new(0.3, 0.16), new(0.55, 0.08), new(0.9, 0.24), new(1, 0.62)], light.Rim);
        Assert.Equal([new(0, 0.56), new(0.06, 0.34), new(0.3, 0.10), new(0.55, 0.05), new(0.9, 0.13), new(1, 0.36)], dark.Rim);
        Assert.Equal((new Argb(255, 0, 0, 0).At(0.10), White.At(0.12)), (light.Divider, dark.Divider));
        foreach (var (glass, shadow, outline) in new[] { (light, 0.10, 0.30), (dark, 0.24, 0.20) })
        {
            Assert.Equal([0.34, 0.18, 0.08], glass.LensBands);
            Assert.Equal((22f, 2f, 1f, 1f, 14f), (glass.GlowDip, glass.LensBandDip, glass.RimDip, glass.DividerDip, glass.DividerInsetDip));
            Assert.Equal((shadow, 40f, 12f, 24f), (glass.ShadowOpacity, glass.ShadowBlurDip, glass.ShadowOffsetDip, glass.RadiusDip));
            Assert.Equal(White.At(outline), glass.FlyoutOutline);
            Assert.Equal(glass.Tint.At(glass.TintMax), glass.Designed);
        }
        Assert.Same(dark, LiquidGlass.For(dark: true));
        Assert.Same(light, LiquidGlass.For(dark: false));
    }

    /// <summary>Each lens band is the profile at its weight, the outermost the strongest; a band the glass has not, clear.</summary>
    [Fact]
    public void EachLensBandIsTheProfileAtItsWeight()
    {
        var glass = LiquidGlass.Light;
        Assert.Equal([new(0, 0.80 * 0.34), new(0.12, 0.40 * 0.34), new(0.5, 0.16 * 0.34), new(0.88, 0.34 * 0.34), new(1, 0.66 * 0.34)], glass.LensStops(0));
        Assert.Equal(glass.Lens.Select(stop => stop.Offset), glass.LensStops(2).Select(stop => stop.Offset));
        Assert.Equal(0.60 * 0.08, LiquidGlass.Dark.LensStops(2)[0].Alpha, 9);
        Assert.All(glass.LensStops(3), stop => Assert.Equal(0, stop.Alpha));
        Assert.All(glass.LensStops(-1), stop => Assert.Equal(0, stop.Alpha));
    }

    /// <summary>The lift: the edge darkness at the glass's edge, falling off as (1 - d/40) squared, gone at 40 DIP.</summary>
    [Fact]
    public void TheLiftFallsOffToNothingAtItsBlur()
    {
        Assert.Equal((0.10, 0.24), (LiquidGlass.Light.Lift(0), LiquidGlass.Dark.Lift(0)));
        Assert.Equal(0.10 * 0.25, LiquidGlass.Light.Lift(20), 9);
        Assert.Equal((0.0, 0.0), (LiquidGlass.Light.Lift(40), LiquidGlass.Dark.Lift(55)));
        Assert.Equal(LiquidGlass.Light.Lift(0), LiquidGlass.Light.Lift(-5));
    }

    /// <summary>
    /// Boxes that meet are one glass: side by side with the same top and bottom (a flush bar's cards), or one on another with
    /// the same sides (a folder's lines); each part's body the whole of it, a hairline on its side of the seam toward the mate
    /// before it, the inset short of the body's outer edges; a box apart, or at another height, its own glass with none.
    /// </summary>
    [Fact]
    public void BoxesThatMeetAreOneGlassPartedByHairlines()
    {
        GlassBox[] row = [new(0, 0, 40, 60), new(40, 0, 100, 60), new(100, 0, 130, 60), new(140, 0, 170, 60), new(170, 4, 200, 56)];
        var parts = LiquidGlass.Join(row, hairline: 1, inset: 14, touch: 0.5);
        var body = new GlassBox(0, 0, 130, 60);
        Assert.Equal([body, body, body, row[3], row[4]], parts.Select(part => part.Body));
        Assert.Null(parts[0].Divider);
        Assert.Equal(new GlassBox(40, 14, 41, 46), parts[1].Divider);
        Assert.Equal(new GlassBox(100, 14, 101, 46), parts[2].Divider);
        Assert.Equal(((GlassBox?)null, (GlassBox?)null), (parts[3].Divider, parts[4].Divider));

        GlassBox[] column = [new(10, 0, 210, 40), new(10, 40.5, 210, 90), new(10, 90, 210, 130), new(10, 140, 210, 180)];
        parts = LiquidGlass.Join(column, hairline: 2, inset: 21, touch: 1);
        Assert.Equal(new GlassBox(10, 0, 210, 130), parts[2].Body);
        Assert.Equal(new GlassBox(31, 40.5, 189, 42.5), parts[1].Divider);
        Assert.Equal(new GlassBox(31, 90, 189, 92), parts[2].Divider);
        Assert.Equal((column[3], (GlassBox?)null), (parts[3].Body, parts[3].Divider));

        // Too short for its inset: one glass, no hairline. A box of no size joins nothing.
        parts = LiquidGlass.Join([new(0, 0, 40, 20), new(40, 0, 80, 20), default], hairline: 1, inset: 14);
        Assert.Equal((new GlassBox(0, 0, 80, 20), (GlassBox?)null), (parts[1].Body, parts[1].Divider));
        Assert.Equal(default(GlassBox), parts[2].Body);
    }

    /// <summary>
    /// The tint is the least that keeps the text 4.5:1 over what is behind, to a hundredth: light glass clear over a white
    /// wallpaper and clouded over a black one, dark glass the other way; its most where nothing is known, or nothing reads.
    /// </summary>
    [Fact]
    public void TheTintIsTheLeastThatKeepsTheTextReadable()
    {
        foreach (var glass in new[] { LiquidGlass.Light, LiquidGlass.Dark })
        {
            Assert.Equal(glass.TintMax, glass.TintOpacity(null));
            Assert.Equal(glass.TintMax, glass.TintOpacity(double.NaN));
            var last = glass.Dark ? 0.0 : 1.0;
            for (var step = 0; step <= 20; step++)
            {
                var behind = step / 20.0;
                var alpha = glass.TintOpacity(behind);
                Assert.InRange(alpha, glass.TintMin, glass.TintMax);
                Assert.Equal(Math.Round(alpha, 2), alpha);
                if (alpha > glass.TintMin && alpha < glass.TintMax)
                {
                    Assert.True(Reads(glass, behind, alpha) >= ToneRules.TextContrast, $"{behind}: {alpha} does not read");
                    Assert.True(Reads(glass, behind, alpha - 0.01) < ToneRules.TextContrast, $"{behind}: {alpha} is not the least");
                }
                // Light glass clouds over as the wallpaper darkens; dark glass as it lightens.
                Assert.True(glass.Dark ? alpha >= last : alpha <= last, $"{behind}: {alpha} after {last}");
                last = alpha;
            }
        }
        Assert.Equal(0.06, LiquidGlass.Light.TintOpacity(1));
        Assert.Equal(0.14, LiquidGlass.Dark.TintOpacity(0));
        Assert.Equal(0.51, LiquidGlass.Light.TintOpacity(0));
        Assert.Equal(0.61, LiquidGlass.Dark.TintOpacity(1));
        // A light out of range held to it.
        Assert.Equal(LiquidGlass.Light.TintOpacity(0), LiquidGlass.Light.TintOpacity(-3));
    }

    /// <summary>The palette's own tint follows what is behind; a colour somebody chose overrides it, as chosen.</summary>
    [Fact]
    public void AChosenColourOverridesTheTint()
    {
        var glass = LiquidGlass.Light;
        Assert.Equal(glass.Tint.At(0.06), glass.Tinted(glass.Designed, 1));
        Assert.Equal(glass.Designed, glass.Tinted(glass.Designed, null));
        var navy = new Argb(0xCC, 0x1E, 0x3A, 0x5F);
        Assert.Equal(navy, glass.Tinted(navy, 1));
    }

    /// <summary>A picture's light: a grid of each cell's mean luminance, and its mean under a box, each cell by how much the box covers.</summary>
    [Fact]
    public void ABackdropsLightIsTheMeanOfItsGridUnderTheBox()
    {
        const int width = 64, height = 36;
        var picture = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var white = x >= width / 2;
                var at = ((y * width) + x) * 4;
                (picture[at], picture[at + 1], picture[at + 2], picture[at + 3]) = white ? ((byte)255, (byte)255, (byte)255, (byte)255) : ((byte)0, (byte)0, (byte)0, (byte)255);
            }
        }
        var grid = LiquidGlass.Grid(picture, width, height);
        Assert.Equal(LiquidGlass.GridColumns * LiquidGlass.GridRows, grid.Length);
        Assert.Equal(0, grid[0]);
        Assert.Equal(1, grid[LiquidGlass.GridColumns - 1], 5);
        Assert.Equal(0.5, LiquidGlass.Mean(grid, 32, 18, 0, 0, 1, 1)!.Value, 5);
        Assert.Equal(0, LiquidGlass.Mean(grid, 32, 18, 0, 0, 0.5, 1)!.Value, 5);
        Assert.Equal(2 / 3.0, LiquidGlass.Mean(grid, 32, 18, 0.25, 0, 1, 1)!.Value, 5);
        // Half a cell covered weighs half: a black cell whole and half a white one.
        Assert.Equal(1 / 3.0, LiquidGlass.Mean(grid, 32, 18, 15 / 32.0, 0, 16.5 / 32, 1)!.Value, 5);
        Assert.Null(LiquidGlass.Mean(grid, 32, 18, 1.2, 0, 2, 1));
        Assert.Null(LiquidGlass.Mean(grid, 32, 18, 0.5, 0.5, 0.5, 1));
        // A strip and its margin: the light under its middle.
        Assert.Equal(0.5, LiquidGlass.Behind(picture, width, height, 4)!.Value, 1);
        Assert.Null(LiquidGlass.Behind(picture, width, height, 18));
        _ = Assert.Throws<ArgumentException>(() => LiquidGlass.Grid(picture, width, height + 1));
    }

    /// <summary>
    /// The palette, right after Glacier: its tint at its most where nothing is known, Windows' text of the mode, no accent (the
    /// system's), no outline (its rim is its material's), every flyout outlined white, the bar blurred at 70; the material in
    /// what it draws, of its mode — and in no other palette's, and never a value of the file's.
    /// </summary>
    [Fact]
    public void ThePaletteIsTheMaterial()
    {
        var ids = LookPalettes.All.Select(palette => palette.Id).ToList();
        Assert.Equal(ids.IndexOf(LookPalettes.GlacierId) + 1, ids.IndexOf(LookPalettes.LiquidGlassId));
        var palette = LookPalettes.Of(LookPalettes.LiquidGlassId)!;
        Assert.Equal("Liquid glass", S.Lookup("settings.look.paletteLiquidGlass", "en"));
        foreach (var dark in new[] { false, true })
        {
            var glass = LiquidGlass.For(dark);
            var design = palette.In(dark);
            Assert.Same(glass, design.Glass);
            Assert.Same(glass, palette.Drawn(dark).Glass);
            foreach (var part in new[] { LookPart.Taskbar, LookPart.Widgets, LookPart.FolderWidgets })
            {
                Assert.Equal(new Paint(glass.Designed), design[part].Background);
                Assert.True(design[part].Accent.IsWindows, $"{part}: an accent of its own");
            }
            Assert.Equal((new Paint(glass.Text), new Paint(glass.Text)), (design.Widgets.Text, design.FolderWidgets.Text));
            Assert.Equal((Paint.Nothing, Paint.Nothing), (design.Taskbar.Outline, design.Widgets.Outline));
            Assert.Equal(new Paint(glass.FlyoutOutline), design.Flyouts.Outline);
            Assert.Equal(LookPalettes.GlassBlur, design.Taskbar.Blur);
            // Read as its mode: the light glass light, the dark one dark.
            var (_, drawnDark) = ToneRules.Dress(new Dressing(design.Taskbar, design.Widgets), windowsDark: !dark);
            Assert.Equal(dark, drawnDark);
        }
        Assert.All(LookPalettes.Choices.Where(each => each != palette), each => Assert.True(each.Drawn(false).Glass is null && each.Drawn(true).Glass is null, each.Id));
        // Worn and written: the file names the palette, nothing of the material.
        var config = new Config();
        Assert.True(LookPalettes.Wear(config, palette));
        Assert.DoesNotContain("glass\"", config.ToJson().Replace("liquidGlass\"", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);
        Assert.Same(LiquidGlass.Dark, Looks.Of(config, dark: true).Glass);
    }

    /// <summary>
    /// The dock wears it: the system's accent (Windows' own, where every other palette wears Karpo's blue), none of it in high
    /// contrast; the bar's and its cards' tint as clear as the wallpaper under it allows, its most while that is not known, and
    /// a colour chosen for the cards as chosen.
    /// </summary>
    [Fact]
    public void TheDockWearsTheSystemsAccentAndATintThatFollowsTheWallpaper()
    {
        var system = Color.FromArgb(255, 0xE8, 0x11, 0x23);
        var windows = new Look(Glass: false, Dark: false, Accent: system);
        var config = new Config();
        _ = LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.LiquidGlassId)!);
        var worn = windows.Wearing(config);
        Assert.Same(LiquidGlass.Light, worn.Liquid);
        Assert.Equal((system, system, system), (worn.Accent, worn.AccentLight2, worn.AccentDark1));
        Assert.Null((windows with { HighContrast = true }).Wearing(config).Liquid);
        Assert.NotEqual(system, windows.Wearing(new Config()).Accent);
        Assert.Null(windows.Wearing(new Config()).Liquid);

        var bright = Scene.Glassed(Scene.Theme(worn, 1, 1), worn, light: 1);
        var unknown = Scene.Glassed(Scene.Theme(worn, 1, 1), worn, light: null);
        var black = Scene.Glassed(Scene.Theme(worn, 1, 1), worn, light: 0);
        Assert.Equal(Material.Translucent, bright.Material);
        Assert.Equal((0.06f, 0.70f), (MathF.Round(bright.Opacity, 2), MathF.Round(unknown.Opacity, 2)));
        Assert.Equal(0.51f, MathF.Round(black.Opacity, 2));
        Assert.Equal(LiquidGlass.Light.Tint.At(0.06).A, bright.CardFill!.Value.A);
        Assert.Equal(LiquidGlass.Light.Designed.A, unknown.CardFill!.Value.A);

        // A colour chosen for the cards: drawn as chosen, whatever is behind.
        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Widgets), Tone.Background, dark: false, new Paint(new Argb(0xCC, 0x1E, 0x3A, 0x5F))));
        var chosen = windows.Wearing(config);
        Assert.Equal(Color.FromArgb(0xCC, 0x1E, 0x3A, 0x5F), Scene.Glassed(Scene.Theme(chosen, 1, 1), chosen, light: 1).CardFill);
    }
}
