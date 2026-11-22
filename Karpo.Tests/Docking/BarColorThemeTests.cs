// How the dock draws the colours derived from three (Karpo.Core\Tones.cs, §405): the one place is Scene.Theme for
// the bar and FlyoutTheme.For for the flyouts — Windows' own colour where nothing is derived, and Windows' system
// colours alone in high contrast —, the bar made as its opacity says (SurfaceStyle.Made, Scene.Settle), and a change of
// the colours of Karpo's own style alone one the bars take in place (Dock.OnlyColorsDiffer, Scene.Recolour).

using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Docking;

public sealed class BarColorThemeTests
{
    private static readonly Argb Navy = new(0xCC, 0x1E, 0x3A, 0x5F);
    private static readonly Argb Rose = new(0x60, 0xFF, 0x80, 0xA0);

    private static Look LookFor(bool dark, Material material = Material.Opaque, Palette colors = default, bool contrast = false) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        HighContrast: contrast,
        HcWindow: Color.FromArgb(255, 0, 0, 0),
        HcText: Color.FromArgb(255, 255, 255, 255),
        HcHighlight: Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText: Color.FromArgb(255, 0, 0, 0),
        Material: material,
        Colors: colors);

    private static Color C(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static Scene.Tokens Theme(Look look) => Scene.Theme(look, 1f, 1f);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithNothingChosenEveryColorIsWindowsOwn(bool dark)
    {
        var automatic = Theme(LookFor(dark));

        Assert.Equal(dark ? Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Color.FromArgb(255, 0xEE, 0xEE, 0xEE), automatic.Base);
        Assert.Null(automatic.CardFill);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ADerivedColorTakesItsPlaceAndNothingElseMoves(bool dark)
    {
        var automatic = Theme(LookFor(dark));
        var chosen = Theme(LookFor(dark, colors: new Palette(Card: Navy, CardOutline: Rose)));

        // §464: a see-through card stands on the blur; §546 without transparency effects at its opacity all the same, as the bar is.
        Assert.Equal(C(Navy), chosen.CardFill);
        Assert.Equal(C(Navy), Theme(LookFor(dark, colors: new Palette(Card: Navy)) with { Glass = true }).CardFill);
        Assert.Equal(C(Rose), chosen.CardStroke);
        Assert.Equal(automatic.Base, chosen.Base);
        Assert.Equal(automatic.Hover, chosen.Hover);
        Assert.Equal(automatic.Text, chosen.Text);
        Assert.Equal(automatic.Active, chosen.Active);
    }

    /// <summary>§405: the bar's colour is drawn solid, and over the blur at the bar's opacity: a colour carries no opacity of its own.</summary>
    [Theory]
    [InlineData(Material.Opaque)]
    [InlineData(Material.Acrylic)]
    public void TheBarsColourIsSolidAndItsOpacityTheSurfaces(Material material)
    {
        var look = LookFor(dark: false, material, new Palette(Taskbar: Navy)) with { Opacity = 0.3f, Glass = true };

        var tokens = Theme(look);

        Assert.Equal(Color.FromArgb(255, Navy.R, Navy.G, Navy.B), tokens.Base);
        Assert.Equal(Color.FromArgb(255, Navy.R, Navy.G, Navy.B), tokens.Tint);
        Assert.Equal(0.3f, tokens.Opacity);
    }

    /// <summary>§405: acrylic needs Windows' transparency effects: without them it is solid, as Windows draws its own; high contrast always is.</summary>
    [Theory]
    [InlineData(Material.Acrylic, true, false, Material.Acrylic)]
    [InlineData(Material.Acrylic, false, false, Material.Opaque)]
    [InlineData(Material.Clear, false, false, Material.Clear)]
    [InlineData(Material.Translucent, false, false, Material.Translucent)]
    [InlineData(Material.Translucent, true, true, Material.Opaque)]
    [InlineData(Material.Opaque, true, false, Material.Opaque)]
    [InlineData(Material.Clear, true, true, Material.Opaque)]
    public void TheMaterialSettlesAgainstWhatWindowsAllows(Material made, bool glass, bool high, Material settled) =>
        Assert.Equal(settled, Scene.Settle(made, glass, high));

    /// <summary>The highlight, its press and the open app's button follow the highlight derived, the Fluent steps under it kept.</summary>
    [Fact]
    public void TheHighlightLightsTheOpenAppAndPressesALittleLess()
    {
        var hover = new Argb(0x30, 0x00, 0x80, 0xFF);

        var tokens = Theme(LookFor(dark: true, colors: new Palette(Highlight: hover)));

        Assert.Equal(C(hover), tokens.Hover);
        Assert.Equal(C(hover), tokens.ActiveBack);
        Assert.Equal(Color.FromArgb(0x20, 0x00, 0x80, 0xFF), tokens.Pressed);
        Assert.True(tokens.HoverOnActive.A < tokens.Pressed.A);
    }

    /// <summary>High contrast draws Windows' system colours and nobody's own, as Windows asks of every app.</summary>
    [Fact]
    public void HighContrastIgnoresEveryColour()
    {
        var all = new Palette(Navy, Navy, Navy, Navy, Navy, Navy, Navy);
        var plain = Theme(LookFor(dark: true, contrast: true));

        var chosen = Theme(LookFor(dark: true, colors: all, contrast: true));

        Assert.Equal(plain.Base, chosen.Base);
        Assert.Equal(plain.CardFill, chosen.CardFill);
        Assert.Equal(plain.Hover, chosen.Hover);
        Assert.Equal(FlyoutTheme.For(LookFor(dark: true, contrast: true)).Base, FlyoutTheme.For(LookFor(dark: true, colors: all, contrast: true)).Base);
        // And a look dressed in high contrast carries none.
        var dressed = LookFor(dark: true, contrast: true).Dressed(new Dressing(new PartPaints(new Karpo.Core.Paint(Navy)), new PartPaints(new Karpo.Core.Paint(Rose), new Karpo.Core.Paint(Rose), new Karpo.Core.Paint(Rose), new Karpo.Core.Paint(Rose))));
        Assert.True(dressed.Colors.IsAutomatic);
        Assert.Null(dressed.Ink);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheMenusBackgroundIsLaidOverWindowsOwn(bool dark)
    {
        var automatic = FlyoutTheme.For(LookFor(dark));

        var chosen = FlyoutTheme.For(LookFor(dark, colors: new Palette(Flyout: Rose)));

        // §464: laid over Windows' own flyout, never in its place: no more see-through than it.
        Assert.Equal(C(ToneRules.Over(Rose, new Argb(automatic.Tint.A, automatic.Tint.R, automatic.Tint.G, automatic.Tint.B))), chosen.Tint);
        Assert.Equal(C(ToneRules.Blend(Rose, new Argb(255, automatic.Base.R, automatic.Base.G, automatic.Base.B))), chosen.Base);
        Assert.Equal(automatic.Text, chosen.Text);
        Assert.Equal(automatic.Accent, chosen.Accent);
    }

    /// <summary>No widget draws the derived backgrounds, so a background being picked redraws none: their styles stay equal.</summary>
    [Fact]
    public void AWidgetsStyleDoesNotCarryTheBackgrounds()
    {
        var plain = new IslandStyle(1.5f, false, 48f, 24f, LookFor(dark: true));
        var colored = new IslandStyle(1.5f, false, 48f, 24f, LookFor(dark: true, colors: new Palette(Card: Navy)));

        Assert.Equal(plain, colored);
        Assert.True(colored.Look.Colors.IsAutomatic);
    }

    private static readonly Config Plain = Made();

    private static Config Made()
    {
        var config = new Config();
        _ = config.DefaultLayout;
        return config;
    }

    /// <summary>One config, the same layouts, Karpo wearing a style of its own whose widgets' cards are <paramref name="background"/> (none for none).</summary>
    private static Config WithBackground(Argb? background)
    {
        var config = Plain.Clone();
        if (background is { } color)
        {
            config.Colour(new PartPaints(new Karpo.Core.Paint(color)));
        }
        return config;
    }

    /// <summary>
    /// §459, §696: another colour of the Appearance's alone (an opacity too) is a colour's change (the config's); with anything
    /// else changed (a pattern), or nothing, none. Whether a bar shows it in place is the bar's (InPlace).
    /// </summary>
    [Fact]
    public void AnotherColourAloneIsAColoursChange()
    {
        var before = WithBackground(Navy);
        var after = WithBackground(Rose);
        after.TaskbarOpacity = 30;

        Assert.True(Dock.Dock.OnlyColorsDiffer(before, WithBackground(Rose)));
        Assert.True(Dock.Dock.OnlyColorsDiffer(WithBackground(null), before));
        // An opacity is a colour's alpha: a colour's change too (a bar made see-through by it is built again: its material).
        Assert.True(Dock.Dock.OnlyColorsDiffer(before, after));
        Assert.False(Dock.Dock.OnlyColorsDiffer(before, before.Clone()));
        var patterned = before.Clone();
        Assert.True(Looks.WritePattern(Looks.Section(patterned, LookPart.Widgets), Pattern.Dots));
        Assert.False(Dock.Dock.OnlyColorsDiffer(before, patterned));
    }

    /// <summary>
    /// §696: a bar recolours in place where only its brushes change — a ground, a card, an outline another colour —; it is
    /// built again where a card or a ground comes or goes, or what its widgets draw themselves changes (the text, the accent,
    /// the highlight) or a pattern's ink.
    /// </summary>
    [Fact]
    public void ABarRecoloursInPlaceOnlyWhereItsBrushesAlone()
    {
        var laid = new Palette(Taskbar: Navy, Card: Navy, Text: new Argb(255, 255, 255, 255));
        Assert.True(Dock.Dock.InPlace(laid, laid with { Taskbar = Rose, Card = Rose }));
        Assert.False(Dock.Dock.InPlace(laid, laid with { Card = null }));
        Assert.False(Dock.Dock.InPlace(laid with { TaskbarOutline = null }, laid with { TaskbarOutline = Rose }));
        Assert.False(Dock.Dock.InPlace(laid, laid with { Text = new Argb(255, 0, 0, 0) }));
        Assert.False(Dock.Dock.InPlace(laid, laid with { Accent = Rose }));
    }

    /// <summary>The recent colours changing alone — Done in a custom colour dialog — recolour in place, never build.</summary>
    [Fact]
    public void RecentColorsAloneAreNoRebuild()
    {
        var before = WithBackground(Navy);
        var after = before.Clone();
        after.RecentColors = [Navy.ToString()];

        Assert.True(Dock.Dock.OnlyColorsDiffer(before, after));
        Assert.Null(before.RecentColors);
    }

    /// <summary>High contrast keeps its outlined cards: Windows' window colour, its text colour round it.</summary>
    [Fact]
    public void HighContrastStillDrawsItsCards()
    {
        var contrast = Theme(LookFor(dark: true, contrast: true));

        Assert.Equal(Color.FromArgb(255, 0, 0, 0), contrast.CardFill);
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), contrast.CardStroke);
    }
}
