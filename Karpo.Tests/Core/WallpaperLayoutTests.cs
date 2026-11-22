// §547: the wallpaper under a bar is the part of the display's wallpaper Windows draws there (Karpo.Core\Wallpapers.cs):
// each fit as Windows lays it out (fill, fit, stretch, tile, center, span), on any display of several — at any place on
// the screen, any scale —, a bar on any edge; the strip the blur reads the bar and its margin, what is past the display
// its edge drawn out; and a strip kept by what it is made of, made again when that changes.

using Karpo.Dock;

namespace Karpo.Tests.Core;

public sealed class WallpaperLayoutTests
{
    private static readonly RectI FullHd = new(0, 0, 1920, 1080);

    /// <summary>A 48 px bar at the bottom of <paramref name="monitor"/>.</summary>
    private static RectI Bottom(RectI monitor, int thick = 48) => new(monitor.Left, monitor.Bottom - thick, monitor.Right, monitor.Bottom);

    private static WallpaperPiece Single(WallpaperFit fit, int width, int height, RectI monitor, RectI strip, RectI? desktop = null) =>
        Assert.Single(Wallpapers.Plan(fit, width, height, monitor, desktop ?? monitor, strip).Pieces);

    /// <summary>Fill: scaled to cover the display, centred, what spills over cut off; a picture of the display's shape exactly so.</summary>
    [Fact]
    public void FillCoversTheDisplayCentred()
    {
        Assert.Equal(new WallpaperPiece(1920, 1080, new RectI(0, 1032, 1920, 1080), 0, 0), Single(WallpaperFit.Fill, 3840, 2160, FullHd, Bottom(FullHd)));
        // A square: 1920 × 1920, 420 px cut off at the top and the bottom each.
        Assert.Equal(new RectI(0, -420, 1920, 1500), Wallpapers.Placed(WallpaperFit.Fill, 1000, 1000, FullHd, FullHd));
        Assert.Equal(new WallpaperPiece(1920, 1920, new RectI(0, 1452, 1920, 1500), 0, 0), Single(WallpaperFit.Fill, 1000, 1000, FullHd, Bottom(FullHd)));
    }

    /// <summary>Fit: all of it on the display, centred; the desktop's colour round it, so a bar beside it shows the colour alone.</summary>
    [Fact]
    public void FitIsAllOfItCentred()
    {
        Assert.Equal(new RectI(420, 0, 1500, 1080), Wallpapers.Placed(WallpaperFit.Fit, 1000, 1000, FullHd, FullHd));
        Assert.Equal(new WallpaperPiece(1080, 1080, new RectI(0, 1032, 1080, 1080), 420, 0), Single(WallpaperFit.Fit, 1000, 1000, FullHd, Bottom(FullHd)));
        var left = new RectI(0, 0, 48, 1080);
        var plan = Wallpapers.Plan(WallpaperFit.Fit, 1000, 1000, FullHd, FullHd, left);
        Assert.Empty(plan.Pieces);
        Assert.Equal(new RectI(0, 0, 48, 1080), plan.Inside);
    }

    /// <summary>Stretch: the display, its shape lost; Center: its own size, centred, cut off where larger.</summary>
    [Fact]
    public void StretchAndCenter()
    {
        Assert.Equal(FullHd, Wallpapers.Placed(WallpaperFit.Stretch, 800, 600, FullHd, FullHd));
        Assert.Equal(new WallpaperPiece(1920, 1080, new RectI(0, 1032, 1920, 1080), 0, 0), Single(WallpaperFit.Stretch, 800, 600, FullHd, Bottom(FullHd)));
        Assert.Equal(new RectI(560, 240, 1360, 840), Wallpapers.Placed(WallpaperFit.Center, 800, 600, FullHd, FullHd));
        Assert.Empty(Wallpapers.Plan(WallpaperFit.Center, 800, 600, FullHd, FullHd, Bottom(FullHd)).Pieces);
        var top = new RectI(0, 0, 1920, 300);
        Assert.Equal(new WallpaperPiece(800, 600, new RectI(0, 0, 800, 60), 560, 240), Single(WallpaperFit.Center, 800, 600, FullHd, top));
        // Larger than the display: centred all the same, its middle shown.
        Assert.Equal(new RectI(-960, -540, 2880, 1620), Wallpapers.Placed(WallpaperFit.Center, 3840, 2160, FullHd, FullHd));
    }

    /// <summary>Tile: at its own size from the display's top left corner — the second display's own corner —, each tile the strip meets.</summary>
    [Fact]
    public void TileRepeatsFromTheDisplaysCorner()
    {
        var second = new RectI(1920, 0, 3840, 1080);
        var plan = Wallpapers.Plan(WallpaperFit.Tile, 500, 400, second, Wallpapers.Round([FullHd, second]), Bottom(second));
        Assert.Equal(
            [
                new WallpaperPiece(500, 400, new RectI(0, 232, 500, 280), 0, 0),
                new WallpaperPiece(500, 400, new RectI(0, 232, 500, 280), 500, 0),
                new WallpaperPiece(500, 400, new RectI(0, 232, 500, 280), 1000, 0),
                new WallpaperPiece(500, 400, new RectI(0, 232, 420, 280), 1500, 0),
            ],
            plan.Pieces);
        // A strip that starts within a tile, on a display left of the primary one.
        var left = new RectI(-1920, 0, 0, 1080);
        var side = new RectI(-1920 + 700, 900, -1920 + 1300, 1000);
        var pieces = Wallpapers.Plan(WallpaperFit.Tile, 500, 400, left, left, side).Pieces;
        Assert.Equal(new WallpaperPiece(500, 400, new RectI(200, 100, 500, 200), 0, 0), pieces[0]);
        Assert.Equal(new WallpaperPiece(500, 400, new RectI(0, 100, 300, 200), 300, 0), pieces[1]);
        Assert.Equal(2, pieces.Count);
    }

    /// <summary>Span: one picture filled over the rectangle round every display, each display its part of it.</summary>
    [Fact]
    public void SpanIsOnePictureOverAllTheDisplays()
    {
        var second = new RectI(1920, 0, 3840, 1080);
        var all = Wallpapers.Round([FullHd, second]);
        Assert.Equal(new RectI(0, 0, 3840, 1080), all);
        Assert.Equal(new WallpaperPiece(3840, 1080, new RectI(1920, 1032, 3840, 1080), 0, 0), Single(WallpaperFit.Span, 3840, 1080, second, Bottom(second), all));
        Assert.Equal(new WallpaperPiece(3840, 1080, new RectI(0, 1032, 1920, 1080), 0, 0), Single(WallpaperFit.Span, 3840, 1080, FullHd, Bottom(FullHd), all));
        // A display above the other: the rectangle round them both is taller; the picture fills it.
        var above = new RectI(0, -1080, 1920, 0);
        Assert.Equal(new RectI(0, -1080, 1920, 1080), Wallpapers.Round([FullHd, above]));
        Assert.Equal(new RectI(-960, -1080, 2880, 1080), Wallpapers.Placed(WallpaperFit.Span, 3840, 2160, above, Wallpapers.Round([FullHd, above])));
    }

    /// <summary>
    /// A bar on each edge of a display at 150 % somewhere left of and above the primary one: its strip the bar and the blur's
    /// margin (three of the most blur's standard deviations: 150 px at 150 %), the part on the display inside it in the
    /// strip's px; the picture's piece just that part.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ABarOnAnyEdgeOfAnyDisplayAtAnyScale(Edge edge)
    {
        var monitor = new RectI(-2560, -200, 0, 1240);
        const int thick = 72;
        var bar = edge switch
        {
            Edge.Top => new RectI(monitor.Left, monitor.Top, monitor.Right, monitor.Top + thick),
            Edge.Left => new RectI(monitor.Left, monitor.Top, monitor.Left + thick, monitor.Bottom),
            Edge.Right => new RectI(monitor.Right - thick, monitor.Top, monitor.Right, monitor.Bottom),
            _ => new RectI(monitor.Left, monitor.Bottom - thick, monitor.Right, monitor.Bottom),
        };
        var margin = Wallpapers.Margin(1.5f);
        Assert.Equal(150, margin);
        var key = new WallpaperKey(new WallpaperSource("a.jpg", 1, WallpaperFit.Stretch, default), monitor, monitor, bar, margin);
        var strip = key.Strip;
        Assert.Equal(new RectI(bar.Left - 150, bar.Top - 150, bar.Right + 150, bar.Bottom + 150), strip);
        var plan = Wallpapers.Plan(WallpaperFit.Stretch, 1280, 720, monitor, monitor, strip);
        var on = Wallpapers.Intersect(strip, monitor)!.Value;
        Assert.Equal(new RectI(on.Left - strip.Left, on.Top - strip.Top, on.Right - strip.Left, on.Bottom - strip.Top), plan.Inside);
        var piece = Assert.Single(plan.Pieces);
        Assert.Equal((2560, 1440), (piece.Width, piece.Height));
        Assert.Equal(new RectI(on.Left - monitor.Left, on.Top - monitor.Top, on.Right - monitor.Left, on.Bottom - monitor.Top), piece.From);
        Assert.Equal((plan.Inside.Left, plan.Inside.Top), (piece.ToX, piece.ToY));
        // The margin past the display's edges the bar stands on is outside: the edge drawn out there.
        Assert.Equal(edge is Edge.Left or Edge.Top or Edge.Bottom ? 150 : 0, plan.Inside.Left);
        Assert.Equal(edge is Edge.Left or Edge.Right or Edge.Top ? 150 : 0, plan.Inside.Top);
    }

    /// <summary>The margin is the most blur's reach: 100 px at 100 %, 150 at 150 %, 200 at 200 %.</summary>
    [Theory]
    [InlineData(1f, 100)]
    [InlineData(1.5f, 150)]
    [InlineData(2f, 200)]
    public void TheMarginIsTheMostBlursReach(float scale, int margin) => Assert.Equal(margin, Wallpapers.Margin(scale));

    /// <summary>What is past the display is its nearest edge drawn out: rows above and below, columns to each side.</summary>
    [Fact]
    public void PastTheDisplayIsItsEdgeDrawnOut()
    {
        // 4 × 3 px, the display's part the middle 2 × 1.
        var data = new byte[4 * 3 * 4];
        void Set(int x, int y, byte value) => data[((y * 4) + x) * 4] = value;
        byte Get(int x, int y) => data[((y * 4) + x) * 4];
        Set(1, 1, 10);
        Set(2, 1, 20);
        WallpaperStrips.Extend(data, 4, 3, new RectI(1, 1, 3, 2));
        Assert.Equal([10, 10, 20, 20], Enumerable.Range(0, 4).Select(x => Get(x, 0)));
        Assert.Equal([10, 10, 20, 20], Enumerable.Range(0, 4).Select(x => Get(x, 1)));
        Assert.Equal([10, 10, 20, 20], Enumerable.Range(0, 4).Select(x => Get(x, 2)));
    }

    /// <summary>
    /// A strip is kept by what it is made of: the same again is the same strip; another picture (another file, the same one
    /// written again, another fit or colour), another display or arrangement, another scale's margin or another bar's
    /// rectangle — taller, moved to another edge — is made again. The blur's amount is no part of it: the same strip blurred again.
    /// </summary>
    [Fact]
    public void AStripIsMadeAgainOnlyWhenWhatItIsMadeOfChanges()
    {
        var source = new WallpaperSource(@"C:\Pictures\one.jpg", 100, WallpaperFit.Fill, new Argb(255, 0, 0, 0));
        var key = new WallpaperKey(source, FullHd, FullHd, Bottom(FullHd), Wallpapers.Margin(1f));
        Assert.Equal(key, new WallpaperKey(source with { }, FullHd, FullHd, Bottom(FullHd), 100));
        WallpaperKey[] others =
        [
            key with { Source = source with { Path = @"C:\Pictures\two.jpg" } },
            key with { Source = source with { Written = 101 } },
            key with { Source = source with { Fit = WallpaperFit.Span } },
            key with { Source = source with { Background = new Argb(255, 0, 0x60, 0xA0) } },
            key with { Monitor = new RectI(0, 0, 2560, 1440) },
            key with { Desktop = Wallpapers.Round([FullHd, new RectI(1920, 0, 3840, 1080)]) },
            key with { Margin = Wallpapers.Margin(1.5f) },
            key with { Bar = Bottom(FullHd, 72) },
            key with { Bar = new RectI(0, 0, 1920, 48) },
        ];
        Assert.All(others, other => Assert.NotEqual(key, other));
        Assert.True(new WallpaperSource(null, 0, WallpaperFit.Fill, default).Solid);
        Assert.True(new WallpaperSource("", 0, WallpaperFit.Fill, default).Solid);
    }
}
