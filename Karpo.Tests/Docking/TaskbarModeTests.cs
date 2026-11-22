// §568 (the person, 2026-11-08: "dark and light mode should be merged together again and the dark and light mode setting of
// the operating system should be respected on the taskbar again"): everything that is the person's taskbar — the bar, its
// widgets, a folder's face and lines, the flyouts, menus and tooltips opened from it — is drawn in the mode Windows is in:
// the Appearance's colours of that mode (each a dark and a light half) over Karpo's own of it (KarpoLook.Paints). §547 still:
// never Windows' accent, "Show accent color on Start and taskbar" or its transparency effects. Windows' high contrast still
// wins. The settings keep following Windows (§526). The mode is the look's, injected: Windows' real setting never touched.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using Karpo.Tests.Folders;
using Color = Windows.UI.Color;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Docking;

public sealed class TaskbarModeTests
{
    private static Color C(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static readonly Argb Navy = new(0xFF, 0x1E, 0x3A, 0x5F);
    private static readonly Argb Sand = new(0xFF, 0xF1, 0xEB, 0xDF);

    /// <summary>
    /// Windows as the dock may read it in one mode: its accent (Windows' blue or an orange), "Show accent color on Start and
    /// taskbar" and transparency effects, every one of the eight, in each mode.
    /// </summary>
    public static TheoryData<bool, bool, bool, bool> Windowses()
    {
        var all = new TheoryData<bool, bool, bool, bool>();
        foreach (var dark in new[] { true, false })
        {
            foreach (var orange in new[] { false, true })
            {
                foreach (var prevalence in new[] { false, true })
                {
                    foreach (var glass in new[] { false, true })
                    {
                        all.Add(dark, orange, prevalence, glass);
                    }
                }
            }
        }
        return all;
    }

    private static Look Windows(bool dark, bool orange, bool prevalence, bool glass)
    {
        var look = TestLooks.LookFor(dark) with { Glass = glass, AccentOnSurface = prevalence && dark };
        if (orange)
        {
            var (accent, light, deep) = (Color.FromArgb(255, 0xCA, 0x50, 0x10), Color.FromArgb(255, 0xF7, 0x95, 0x4F), Color.FromArgb(255, 0x8E, 0x38, 0x0B));
            look = look with { Accent = accent, AccentLight2 = light, AccentDark1 = deep, AccentDark2 = deep };
        }
        return look.Plain();
    }

    /// <summary>An Appearance with nothing set; and one set throughout: Paper worn (both modes), a pattern, a flyout outline.</summary>
    private static Config Chosen(bool any)
    {
        var config = new Config();
        if (any)
        {
            Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.PaperId)!));
            Assert.True(Looks.WritePattern(Looks.Section(config, LookPart.Taskbar), Pattern.Grid));
            Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, new Karpo.Core.Paint(new Argb(0xFF, 0x80, 0x20, 0x20))));
        }
        return config;
    }

    /// <summary>Every field of the bar's tokens, as words, so two can be compared whole.</summary>
    private static string Words(object tokens) => string.Join('|',
        tokens.GetType().GetFields().Select(field => $"{field.Name}={field.GetValue(tokens)}"));

    /// <summary>Everything the taskbar of <paramref name="config"/> is drawn in under <paramref name="windows"/>, as words.</summary>
    private static string Taskbar(Look windows, Config config)
    {
        var bar = windows.Wearing(config);
        var flyout = bar.Flyout();
        return string.Join("\n",
            bar.ToString(),
            Words(Scene.Theme(bar, 1f, 1f)),
            flyout.ToString(),
            FlyoutTheme.For(bar).ToString(),
            FlyoutTheme.Taskbar(bar).ToString(),
            FlyoutTheme.Folder(flyout).ToString(),
            FlyoutTheme.For(flyout).ToString(),
            QuickTheme.For(FlyoutTheme.Taskbar(bar)).ToString());
    }

    /// <summary>
    /// §547 still: in one mode the taskbar's tokens are the same whatever Windows' accent, the accent on the taskbar and its
    /// transparency effects, with nothing set and with everything set, a flyout and a folder's line too. §568: in the other
    /// mode they are another taskbar.
    /// </summary>
    [Theory]
    [MemberData(nameof(Windowses))]
    public void InOneModeTheTaskbarIsTheSameWhateverElseWindowsIs(bool dark, bool orange, bool prevalence, bool glass)
    {
        foreach (var any in new[] { false, true })
        {
            var config = Chosen(any);
            Assert.Equal(Taskbar(Windows(dark, false, false, false), config), Taskbar(Windows(dark, orange, prevalence, glass), config));
            Assert.NotEqual(Taskbar(Windows(!dark, false, false, false), config), Taskbar(Windows(dark, orange, prevalence, glass), config));
        }
    }

    /// <summary>
    /// Karpo's own where nothing is set (KarpoLook), of the mode Windows is in: dark the bar #202020 at 85 %, each widget on a
    /// solid #2B2B2B card, the text white, Karpo's blue #60CDFF; light the bar #F3F3F3 at 85 %, each widget on a solid #FBFBFB
    /// card, the text dark, Karpo's blue #005FB8 — the bar laid see-through over the wallpaper Karpo blurs whether
    /// transparency effects are on or not, the flyouts and a folder's lines solid.
    /// </summary>
    [Fact]
    public void NothingSetIsKarposOwnOfTheMode()
    {
        foreach (var dark in new[] { true, false })
        {
            foreach (var glass in new[] { false, true })
            {
                var bar = Windows(dark, orange: true, prevalence: false, glass).Wearing(new Config());
                var tokens = Scene.Theme(bar, 1f, 1f);
                Assert.Equal(Material.Translucent, bar.Material);
                Assert.Equal(0.85f, bar.Opacity);
                Assert.Equal(dark ? Color.FromArgb(0xD9, 0x20, 0x20, 0x20) : Color.FromArgb(0xD9, 0xF3, 0xF3, 0xF3), Scene.GroundOf(tokens));
                Assert.Equal(C(dark ? KarpoLook.Card : KarpoLook.LightCard), tokens.CardFill);
                Assert.Equal(255, tokens.CardFill!.Value.A);
                Assert.Equal(dark, bar.Dark);
                Assert.False(bar.Glass);
                Assert.True(dark ? tokens.Text.R == 0xFF : tokens.Text.R < 0x40, $"{(dark ? "dark" : "light")}: the text {tokens.Text}");
                Assert.Equal(C(KarpoLook.AccentOn(dark)), tokens.Active);
                Assert.Equal(C(KarpoLook.AccentOn(dark)), FlyoutTheme.For(bar).Accent);
                Assert.False(FlyoutTheme.Taskbar(bar).Glass);
                Assert.False(FlyoutTheme.Folder(bar.Flyout()).Glass);
            }
        }
        Assert.Equal(FlyoutTheme.TaskbarDark, FlyoutTheme.Taskbar(Windows(true, false, false, false).Wearing(new Config())).Base);
        Assert.NotEqual(FlyoutTheme.TaskbarDark, FlyoutTheme.Taskbar(Windows(false, false, false, false).Wearing(new Config())).Base);
    }

    /// <summary>
    /// §568: a colour's dark half drawn while Windows is dark, its light half while it is light; a half left unset Karpo's own of
    /// its mode, the other half as set; a change of mode picks the other half, nothing else written.
    /// </summary>
    [Fact]
    public void TheModePicksItsHalf()
    {
        var config = new Config();
        var widgets = Looks.Section(config, LookPart.Widgets);
        Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: true, new Karpo.Core.Paint(Navy)));
        Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: false, new Karpo.Core.Paint(Sand)));
        Assert.Equal(C(Navy), Scene.Theme(Windows(true, false, false, false).Wearing(config), 1f, 1f).CardFill);
        Assert.Equal(C(Sand), Scene.Theme(Windows(false, false, false, false).Wearing(config), 1f, 1f).CardFill);
        // The light half unset again: Karpo's own light card, the dark one as set.
        Assert.True(Looks.WritePaint(widgets, Tone.Background, dark: false, null));
        Assert.Equal(new ColorPair { Dark = Navy.Rgb }, config.WornValues!.Widgets!.Background);
        Assert.Equal(C(KarpoLook.LightCard), Scene.Theme(Windows(false, false, false, false).Wearing(config), 1f, 1f).CardFill);
        Assert.Equal(C(Navy), Scene.Theme(Windows(true, false, false, false).Wearing(config), 1f, 1f).CardFill);
        // Written the same again: nothing changed.
        Assert.False(Looks.WritePaint(widgets, Tone.Background, dark: true, new Karpo.Core.Paint(Navy)));
        // The resolver alone, each mode its own default.
        Assert.Equal((new Karpo.Core.Paint(Navy), new Karpo.Core.Paint(KarpoLook.LightCard)), (Looks.Of(config, dark: true).Widgets.Background, Looks.Of(config, dark: false).Widgets.Background));
    }

    /// <summary>A light ground chosen in the dark mode's half reads light, in Karpo's blue's shade for a light ground.</summary>
    [Fact]
    public void ALightGroundReadsLightInKarposBlue()
    {
        var config = new Config();
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, new Karpo.Core.Paint(new Argb(0xFF, 0xF3, 0xF3, 0xF3))));
        foreach (var dark in new[] { true, false })
        {
            var bar = Windows(dark, orange: true, prevalence: true, glass: false).Wearing(config);
            Assert.False(bar.Dark);
            Assert.Equal(Material.Opaque, bar.Material);
            Assert.Equal(C(KarpoLook.AccentDark1), Scene.Theme(bar, 1f, 1f).Active);
        }
    }

    /// <summary>High contrast still wins over every look, the taskbar's too: Windows' system colours, solid.</summary>
    [Fact]
    public void HighContrastStillWins()
    {
        var high = TestLooks.LookFor(dark: true) with
        {
            HighContrast = true,
            HcWindow = Color.FromArgb(255, 0, 0, 0),
            HcText = Color.FromArgb(255, 0xFF, 0xFF, 0x00),
            HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
            HcHighlightText = Color.FromArgb(255, 0, 0, 0),
        };
        var bar = high.Plain().Wearing(Chosen(any: true));
        var tokens = Scene.Theme(bar, 1f, 1f);
        Assert.Equal(Material.Opaque, tokens.Material);
        Assert.Equal(Color.FromArgb(255, 0, 0, 0), tokens.Base);
        Assert.Equal(Color.FromArgb(255, 0xFF, 0xFF, 0x00), tokens.Text);
        Assert.True(bar.Colors.IsAutomatic);
    }

    /// <summary>The settings still follow Windows (§526): its mode, its accent, its transparency effects.</summary>
    [Fact]
    public void TheSettingsStillFollowWindows()
    {
        var (dark, light) = (Windows(true, false, false, true), Windows(false, true, false, false));
        var (darkSettings, lightSettings) = (TheDock.SettingsLookOf(dark), TheDock.SettingsLookOf(light));
        Assert.True(darkSettings.Dark);
        Assert.False(lightSettings.Dark);
        Assert.True(darkSettings.Glass);
        Assert.NotEqual(darkSettings.Accent, lightSettings.Accent);
        Assert.NotEqual(FlyoutTheme.For(darkSettings), FlyoutTheme.For(lightSettings));
    }
}
