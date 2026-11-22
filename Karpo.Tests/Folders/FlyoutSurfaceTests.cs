// A taskbar flyout's surface (FlyoutTheme.Taskbar, docs\widgets.md §9, "Flyout surface"): the values
// measured off Windows' own quick settings, notification center and calendar on 2026-10-16 (dark,
// transparency effects off, 150 %), pinned here so a token changed by hand has to say why.
//
// The person, 2026-10-16: "make sure that the design in terms of colors and shadows and borders is
// exactly the same as it is for the native taskbar flyouts."

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.TestLooks;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FlyoutSurfaceTests
{
    /// <summary>A colour <paramref name="over"/> another, as the compositor lays it, 0 to 255 a channel.</summary>
    private static int Over(DXGI_RGBA top, byte under) => (int)MathF.Round((top.r * 255 * top.a) + (under * (1 - top.a)));

    private static int Over(Color top, byte under) => Over(FlyoutTheme.Ink(top), under);

    /// <summary>The body: Windows' #242424 dark, where WinUI's acrylic fallback (a menu's) is #2C2C2C.</summary>
    [Fact]
    public void TheBodyIsWindowsOwn()
    {
        Assert.Equal(Color.FromArgb(255, 0x24, 0x24, 0x24), FlyoutTheme.Taskbar(LookFor(dark: true)).Base);
        Assert.Equal(Color.FromArgb(255, 0xF9, 0xF9, 0xF9), FlyoutTheme.Taskbar(LookFor(dark: false)).Base);
        // A menu keeps its own.
        Assert.Equal(Color.FromArgb(255, 0x2C, 0x2C, 0x2C), FlyoutTheme.For(LookFor(dark: true)).Base);
        Assert.False(FlyoutTheme.For(LookFor(dark: true)).RimOutside);
    }

    /// <summary>
    /// The rim is see-through and lies over what is behind the flyout: over the backdrops Windows'
    /// own was measured on it comes out as Windows' did, within a level or two.
    /// </summary>
    [Theory]
    [InlineData(0xFF, 0xC8)]
    [InlineData(0x1C, 0x40)]
    [InlineData(0x13, 0x3A)]
    [InlineData(0x04, 0x31)]
    public void TheRimOverTheDesktopIsWindowsOwn(byte behind, int measured)
    {
        foreach (var dark in new[] { true, false })
        {
            var theme = FlyoutTheme.Taskbar(LookFor(dark));
            Assert.True(theme.RimOutside);
            Assert.Equal(1, theme.StrokeDip);
            Assert.InRange(Over(theme.Stroke, behind), measured - 3, measured + 3);
        }
    }

    /// <summary>Windows casts no shadow without transparency effects, over a white window or a black one; with them it does.</summary>
    [Fact]
    public void AShadowOnlyWithTransparencyEffects()
    {
        Assert.False(FlyoutTheme.Taskbar(LookFor(dark: true)).Casts);
        Assert.False(FlyoutTheme.Taskbar(LookFor(dark: false)).Casts);
        var glass = FlyoutTheme.Taskbar(LookFor(dark: true) with { Glass = true });
        Assert.True(glass.Casts && glass.ShadowOpacity > 0);
        Assert.True(glass.Glass);
        // A menu casts its own whatever.
        Assert.True(FlyoutTheme.For(LookFor(dark: true)).Casts);
    }

    /// <summary>The footer band and its hairline, laid over the body, are the shades Windows' quick settings end in.</summary>
    [Theory]
    [InlineData(true, 0x1C, 0x19)]
    [InlineData(false, 0xEE, 0xE5)]
    public void TheFooterIsWindowsOwn(bool dark, int band, int line)
    {
        var popup = FlyoutTheme.Taskbar(LookFor(dark));
        var theme = QuickTheme.For(popup);
        Assert.InRange(Over(theme.Footer, popup.Base.R), band - 1, band + 1);
        Assert.InRange(Over(theme.FooterLine, popup.Base.R), line - 1, line + 1);
        // Darker than the body, the hairline darker than the band, so it reads on a chosen colour too.
        Assert.True(theme.Footer.r == 0 && theme.FooterLine.a > theme.Footer.a);
    }

    /// <summary>The subtle hover on the band: Windows' gear button's #292929 on #1C1C1C.</summary>
    [Fact]
    public void AButtonLitOnTheBandIsWindowsOwn()
    {
        var popup = FlyoutTheme.Taskbar(LookFor(dark: true));
        var theme = QuickTheme.For(popup);
        var band = Over(theme.Footer, popup.Base.R);
        Assert.InRange(Over(theme.SubtleHover, (byte)band), 0x28, 0x2A);
    }

    /// <summary>A colour somebody chose for flyouts stands over the taskbar surface as over a menu's (§464: laid over it); high contrast keeps the system's.</summary>
    [Fact]
    public void AChosenColourAndHighContrastStand()
    {
        var chosen = LookFor(dark: true) with { Colors = new Karpo.Core.Palette(Flyout: new Karpo.Core.Argb(0xCC, 0x10, 0x20, 0x30)) };
        var theme = FlyoutTheme.Taskbar(chosen);
        // §464: laid over Windows' own surface, never in its place.
        var under = FlyoutTheme.TaskbarDark;
        var over = Karpo.Core.ToneRules.Blend(new Karpo.Core.Argb(0xCC, 0x10, 0x20, 0x30), new Karpo.Core.Argb(255, under.R, under.G, under.B));
        Assert.Equal(Color.FromArgb(255, over.R, over.G, over.B), theme.Base);
        Assert.Equal(FlyoutTheme.TaskbarStroke, theme.Stroke);

        var high = LookFor(dark: true) with
        {
            HighContrast = true,
            HcWindow = Color.FromArgb(255, 0, 0, 0),
            HcText = Color.FromArgb(255, 255, 255, 255),
        };
        var contrast = FlyoutTheme.Taskbar(high);
        Assert.True(contrast.HighContrast && contrast.RimOutside);
        Assert.Equal(Color.FromArgb(255, 255, 255, 255), contrast.Stroke);
        Assert.Equal(0, contrast.ShadowOpacity);
    }
}
