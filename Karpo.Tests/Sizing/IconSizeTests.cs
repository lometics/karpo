// The icons that are not an app's are Windows 11's 16 DIP on its 48-DIP bar (the person, 2026-10-09:
// "the icons that are not pins, like network, tray icons, notifications, etc in the taskbar are too
// big"): the tray's, the network's, the sound's, the bell, a widget's glyph — one size, a third of
// the bar, beside an app's 24 DIP, half of it, and in proportion on the font's steps on any other
// bar (docs\widgets.md §57).

using Karpo.Dock;

namespace Karpo.Tests.Sizing;

public sealed class IconSizeTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    private static Fit FitFor(uint dpi, Config config) =>
        new(new IslandStyle(dpi / 96f, false, Layout.IslandThicknessPx(dpi, config), Layout.IconPx(dpi, config), Dark));

    [Theory]
    [InlineData(96u)]
    [InlineData(120u)]
    [InlineData(144u)]
    public void OnTheTaskbarsOwnBarTheyAreWindowsSixteenBesideTwentyFour(uint dpi)
    {
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        var scale = dpi / 96f;
        var fit = FitFor(dpi, config);
        Assert.Equal(MathF.Round(16 * scale), fit.SystemIcon);
        Assert.Equal(MathF.Round(16 * scale), fit.Glyph());
        Assert.Equal(MathF.Round(16 * scale), Layout.TrayIconPx(dpi, config));
        Assert.Equal(MathF.Round(24 * scale), Layout.IconPx(dpi, config));
    }

    [Theory]
    [InlineData(28, 16)]
    [InlineData(32, 16)]
    [InlineData(40, 16)]
    [InlineData(48, 16)]
    [InlineData(64, 20)]
    [InlineData(80, 24)]
    public void TheyGrowWithTheBarOnTheFontsSteps(int height, int dips)
    {
        foreach (var dpi in new uint[] { 96, 144 })
        {
            var config = new Config { Height = height, Style = BarStyle.Islands };
            var fit = FitFor(dpi, config);
            Assert.Equal(MathF.Round(dips * dpi / 96f), fit.SystemIcon);
            // A tray icon is the network's size beside it, never larger than an app's icon.
            Assert.Equal(MathF.Min(fit.SystemIcon, Layout.IconPx(dpi, config)), Layout.TrayIconPx(dpi, config));
            // The status icons behind their arrow are the bar's.
            Assert.Equal(fit.SystemIcon, Fit.SystemIconFor(Layout.CellPx(dpi, config), dpi / 96f));
            // And never fill their square: room round them, and from 40 DIP up well under two thirds
            // of it (under that, 16 DIP is the least the font is drawn for).
            var card = Layout.CellPx(dpi, config);
            Assert.True(fit.SystemIcon < card && (height < 40 || fit.SystemIcon <= card * 0.62f), $"{height} DIP at {dpi}: a {fit.SystemIcon}-px icon in a {card}-px card");
        }
    }

    [Fact]
    public void AFloatingDockKeepsTheSameIcons()
    {
        var islands = FitFor(96, new Config { Height = 48, Style = BarStyle.Islands });
        var dock = FitFor(96, new Config { Height = 48, Style = BarStyle.Floating });
        Assert.Equal(16f, islands.SystemIcon);
        Assert.Equal(16f, dock.SystemIcon);
    }
}
