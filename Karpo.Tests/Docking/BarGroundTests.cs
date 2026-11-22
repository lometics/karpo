// What the bar's ground is drawn in (docs\widgets.md §546; the person, 2026-11-07: "the background of the taskbar changed
// with the dark / light mode but it should not. also the opacity of the taskbar does not work."): a background chosen is
// the person's — §568 its half of the mode Windows is in (written in both, the same in both), at its own opacity with
// Windows' transparency effects on or off —; §547 nothing chosen is Karpo's own of the mode, #202020 or §568 #F3F3F3 at
// 85 %, whatever Windows' effects; what is see-through is
// laid over the wallpaper Karpo blurs itself (Material.Translucent), never DWM's blur. Through the real path: the Look editor's row,
// the file, the layout's look worn (Look.Wearing), the bar's tokens (Scene.Theme) and the colour its ground is painted
// (Scene.GroundOf).

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Folders;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Docking;

public sealed class BarGroundTests
{
    /// <summary>Graphite dark's bar, the person's own until today: #1B1C1F at 88 %.</summary>
    private static readonly Argb Graphite = new(0xE0, 0x1B, 0x1C, 0x1F);

    private static Color C(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>Windows as the dock reads it: its mode, and whether its transparency effects are on.</summary>
    private static Look Windows(bool dark, bool glass) => TestLooks.LookFor(dark) with { Glass = glass };

    /// <summary>A config whose default layout's Taskbar has <paramref name="background"/>; none for Windows' own.</summary>
    private static Config With(Argb? background)
    {
        var config = new Config();
        if (background is { } color)
        {
            Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, new Karpo.Core.Paint(color)));
        }
        return config;
    }

    private static Look Worn(Config config, bool dark, bool glass) => Windows(dark, glass).Wearing(config);

    /// <summary>The colour the bar's ground is painted, over what is behind the bar (the blur, or the desktop).</summary>
    private static Color Ground(Config config, bool dark, bool glass) => Scene.GroundOf(Scene.Theme(Worn(config, dark, glass), 1f, 1f));

    /// <summary>
    /// A background chosen the same for both modes, half opaque or more, is drawn the same in both — its colour, its material,
    /// the mode the bar reads in. It reads as its own colour, not as laid over Windows' surface of the mode (§546: an orange at
    /// 50 % read dark over #202020 and light over #F3F3F3). §568: what stands on it unset (the widgets' cards) is Karpo's own of
    /// each mode, so the rest of the bar is the mode's.
    /// </summary>
    [Theory]
    [InlineData(0xE01B1C1Fu, false)]
    [InlineData(0xE01B1C1Fu, true)]
    [InlineData(0x80FF8C00u, false)]
    [InlineData(0x80FF8C00u, true)]
    [InlineData(0xB3EBECEEu, false)]
    public void AChosenBackgroundIsTheSameInBothModes(uint argb, bool glass)
    {
        var config = With(new Argb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        config.WornValues!.Taskbar!.Pattern = Pattern.Grid;
        var (dark, light) = (Worn(config, dark: true, glass), Worn(config, dark: false, glass));

        Assert.Equal(Ground(config, dark: true, glass), Ground(config, dark: false, glass));
        Assert.Equal((dark.Material, dark.Opacity), (light.Material, light.Opacity));
        Assert.Equal(dark.Colors.Taskbar, light.Colors.Taskbar);
        Assert.Equal(dark.Colors.TaskbarPattern, light.Colors.TaskbarPattern);
    }

    /// <summary>
    /// The opacity of a background chosen is drawn, Windows' transparency effects on or off (this PC: EnableTransparency 0)
    /// — never made solid: §547 over the wallpaper Karpo blurs, either way.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItsOpacityIsDrawnWithOrWithoutTransparencyEffects(bool glass)
    {
        foreach (var dark in new[] { true, false })
        {
            Assert.Equal(C(Graphite), Ground(With(Graphite), dark, glass));
            Assert.Equal(Material.Translucent, Worn(With(Graphite), dark, glass).Material);
        }
    }

    /// <summary>A background chosen opaque is solid, in both modes, effects or none.</summary>
    [Fact]
    public void AnOpaqueBackgroundIsSolid()
    {
        var solid = Graphite with { A = 255 };
        foreach (var (dark, glass) in new[] { (true, true), (true, false), (false, true), (false, false) })
        {
            Assert.Equal(C(solid), Ground(With(solid), dark, glass));
            Assert.Equal(Material.Opaque, Worn(With(solid), dark, glass).Material);
        }
    }

    /// <summary>
    /// §547, §568: nothing chosen, Karpo's own taskbar of the mode — #202020 or #F3F3F3 at 85 % over the wallpaper blurred —,
    /// whatever Windows' transparency effects (§546 until then: Windows' own, #1C1C1C or #EEEEEE solid, #202020 or #F3F3F3 at
    /// 70 % over DWM's blur).
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void NothingChosenIsKarposOwnInEveryMode(bool dark, bool glass)
    {
        Assert.Equal(dark ? Color.FromArgb(0xD9, 0x20, 0x20, 0x20) : Color.FromArgb(0xD9, 0xF3, 0xF3, 0xF3), Ground(With(null), dark, glass));
        Assert.Equal((Material.Translucent, 0.85f), (Worn(With(null), dark, glass).Material, Worn(With(null), dark, glass).Opacity));
    }

    /// <summary>
    /// A colour mixed in the Taskbar's Background row (§523: its opacity slider, its hex box) is written with its alpha, read
    /// back from the file with it, and the bar is painted in it at that opacity.
    /// </summary>
    [Fact]
    public void TheOpacityMixedSurvivesTheFileAndReachesTheBar()
    {
        var config = new Config();
        var background = OptionRows.Find(LookRows.Rows(config, config.DefaultLayout, windows: true), LookRows.ColourKey(LookRows.SectionKey(LookPart.Taskbar), Tone.Background))!;
        Assert.True(background.Alpha);

        Assert.True(background.SetColor!(Graphite, true));
        var read = Configs.RoundTrip(config, out _);

        // §568: the dark mode's half alone; the light mode's Karpo's own.
        Assert.Equal(new ColorPair { Dark = "#E01B1C1F" }, read.WornValues?.Taskbar?.Background);
        Assert.Equal(C(Graphite), Ground(read, dark: true, glass: false));
        Assert.NotEqual(C(Graphite), Ground(read, dark: false, glass: false));
    }

    /// <summary>A widget's see-through card on the bar keeps its opacity without transparency effects too, over the desktop.</summary>
    [Fact]
    public void ASeeThroughCardKeepsItsOpacity()
    {
        var navy = new Argb(0x80, 0x1E, 0x3A, 0x5F);
        foreach (var glass in new[] { false, true })
        {
            var tokens = Scene.Theme(Windows(dark: true, glass) with { Colors = new Palette(Card: navy) }, 1f, 1f);
            Assert.Equal(C(navy), tokens.CardFill);
        }
    }

    /// <summary>
    /// A folder's see-through line keeps its opacity, over the desktop; §547 no DWM blur under it with transparency effects
    /// on either: the taskbar's folders are the same whatever Windows' effects.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASeeThroughLineKeepsItsOpacity(bool glass)
    {
        var navy = new Argb(0x80, 0x1E, 0x3A, 0x5F);
        var theme = FlyoutTheme.Folder(Windows(dark: true, glass).Wearing((Appearance?)null).Flyout());

        Assert.Equal((C(navy), false), FolderFlyout.LineFill(theme, new Palette(Card: navy)));
    }
}
