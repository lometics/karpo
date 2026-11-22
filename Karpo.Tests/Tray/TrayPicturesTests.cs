// The person, 2026-10-08: "tray icons are drawn at very low resolution". An app hands the tray one
// picture, most often its small icon for the display's DPI (24 px at 150 %), where the bar draws an
// app icon's 36; DrawIconEx made it 36 by copying pixels. Now it is scaled with a cubic filter
// (TrayPictures.Resize), and where the app's picture is its executable's own icon, that icon is taken
// at the bar's size instead (Tray.ExeIcons). Pure: no window, no shell.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.TrayIcons;

public sealed class TrayPicturesTests
{
    /// <summary>A <paramref name="side"/>-px square of one premultiplied colour.</summary>
    private static byte[] Solid(int side, byte b, byte g, byte r, byte a)
    {
        var data = new byte[side * side * 4];
        for (var i = 0; i < data.Length; i += 4)
        {
            (data[i], data[i + 1], data[i + 2], data[i + 3]) = (b, g, r, a);
        }
        return data;
    }

    /// <summary>Opaque red on the left half, opaque blue on the right: a hard edge down the middle.</summary>
    private static byte[] Halves(int side)
    {
        var data = new byte[side * side * 4];
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = (y * side + x) * 4;
                (data[at], data[at + 1], data[at + 2], data[at + 3]) = x < side / 2 ? ((byte)0, (byte)0, (byte)255, (byte)255) : ((byte)255, (byte)0, (byte)0, (byte)255);
            }
        }
        return data;
    }

    private static int Colours(byte[] data, int side)
    {
        var seen = new HashSet<int>();
        for (var i = 0; i < side * side * 4; i += 4)
        {
            seen.Add(BitConverter.ToInt32(data, i));
        }
        return seen.Count;
    }

    [Theory]
    [InlineData(16, 24)]
    [InlineData(16, 36)]
    [InlineData(24, 36)]
    [InlineData(32, 48)]
    [InlineData(48, 36)]
    [InlineData(32, 24)]
    public void APictureComesBackAtTheSizeAskedFor(int from, int to)
    {
        var scaled = TrayPictures.Resize(Halves(from), from, to);

        Assert.Equal(to * to * 4, scaled.Length);
    }

    [Fact]
    public void TheSameSizeIsACopy()
    {
        var picture = Halves(24);

        var same = TrayPictures.Resize(picture, 24, 24);

        Assert.Equal(picture, same);
        Assert.NotSame(picture, same);
    }

    [Theory]
    [InlineData(16, 36)]
    [InlineData(24, 36)]
    [InlineData(48, 36)]
    public void OneColourStaysThatColourEdgeToEdge(int from, int to)
    {
        // Semi-transparent, premultiplied: an icon's soft shadow, say.
        var scaled = TrayPictures.Resize(Solid(from, 40, 60, 80, 128), from, to);

        for (var i = 0; i < scaled.Length; i += 4)
        {
            Assert.InRange(scaled[i], 39, 41);
            Assert.InRange(scaled[i + 1], 59, 61);
            Assert.InRange(scaled[i + 2], 79, 81);
            Assert.InRange(scaled[i + 3], 127, 129);
        }
    }

    /// <summary>
    /// The defect: a 24-px picture drawn 36 px by copying pixels has its two colours and nothing
    /// between them, a stair of whole pixels. Filtered, the edge has steps between red and blue, and
    /// the halves away from it keep their colours.
    /// </summary>
    [Fact]
    public void ScaledUpAnEdgeIsFilteredNotCopied()
    {
        var scaled = TrayPictures.Resize(Halves(24), 24, 36);

        Assert.True(Colours(scaled, 36) > 2, "no colour between red and blue: copied, not filtered");
        // The far left is red and the far right blue, as they were.
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, scaled[..4]);
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, scaled[(35 * 4)..(36 * 4)]);
    }

    [Fact]
    public void FilteredColourIsNeverAboveItsAlpha()
    {
        // An opaque square on transparency: the filter overshoots at its edge, which premultiplied
        // colour can't hold.
        var data = new byte[16 * 16 * 4];
        for (var y = 4; y < 12; y++)
        {
            for (var x = 4; x < 12; x++)
            {
                var at = (y * 16 + x) * 4;
                (data[at], data[at + 1], data[at + 2], data[at + 3]) = (255, 255, 255, 255);
            }
        }

        foreach (var to in new[] { 20, 24, 36, 12 })
        {
            var scaled = TrayPictures.Resize(data, 16, to);
            for (var i = 0; i < scaled.Length; i += 4)
            {
                Assert.True(scaled[i] <= scaled[i + 3] && scaled[i + 1] <= scaled[i + 3] && scaled[i + 2] <= scaled[i + 3], $"{to} px, pixel {i / 4}");
            }
        }
    }

    [Fact]
    public void ScaledDownEveryPixelCounts()
    {
        // A checkerboard of black and white, halved: grey, not one of the two picked.
        var data = new byte[32 * 32 * 4];
        for (var y = 0; y < 32; y++)
        {
            for (var x = 0; x < 32; x++)
            {
                var value = (byte)((x + y) % 2 == 0 ? 255 : 0);
                var at = (y * 32 + x) * 4;
                (data[at], data[at + 1], data[at + 2], data[at + 3]) = (value, value, value, 255);
            }
        }

        var scaled = TrayPictures.Resize(data, 32, 16);

        for (var y = 2; y < 14; y++)
        {
            for (var x = 2; x < 14; x++)
            {
                Assert.InRange(scaled[(y * 16 + x) * 4], 110, 145);
            }
        }
    }

    [Fact]
    public void AlikeIsTheSamePictureGiveOrTakeAStep()
    {
        var picture = Halves(24);
        var nearly = (byte[])picture.Clone();
        for (var i = 0; i < nearly.Length; i += 16)
        {
            nearly[i] = (byte)Math.Max(0, nearly[i] - 3);
        }
        // The same icon with a badge in its corner: another picture.
        var badged = (byte[])picture.Clone();
        for (var y = 14; y < 24; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                var at = (y * 24 + x) * 4;
                (badged[at], badged[at + 1], badged[at + 2]) = (40, 200, 40);
            }
        }

        Assert.True(TrayPictures.Alike(picture, picture));
        Assert.True(TrayPictures.Alike(picture, nearly));
        Assert.False(TrayPictures.Alike(picture, badged));
        Assert.False(TrayPictures.Alike(picture, Halves(16)));
        Assert.False(TrayPictures.Alike([], []));
    }

    // --- The executable's own icon (Tray.ExeIcons) ------------------------------------------

    private const string App = @"C:\Apps\Chat\Chat.exe";

    private static Pixels Picture(byte[] data, int side) => new() { Data = data, Width = side, Height = side };

    /// <summary>A shell that has <see cref="App"/>'s icon at every size, red and blue halves, and counts what it is asked.</summary>
    private sealed class FakeShell
    {
        public List<(string Path, int Size)> Asked { get; } = [];

        public Pixels? Icon(string path, int size)
        {
            Asked.Add((path, size));
            return path == App ? Picture(Halves(size), size) : null;
        }
    }

    [Theory]
    [InlineData(96, 16)]
    [InlineData(120, 20)]
    [InlineData(144, 24)]
    [InlineData(192, 32)]
    public void AnAppsSmallIconIsTakenFromItsExecutableAtTheBarsSizeForEveryDpi(uint dpi, int small)
    {
        // The app hands over its small icon for the DPI (16 DIP), which is the taskbar's own bar's
        // size (docs\widgets.md §57): nothing to sharpen there.
        Assert.Equal(small, (int)MathF.Ceiling(Layout.TrayIconPx(dpi, new Config { Height = 48 })));
        Assert.Null(new Tray.ExeIcons(new FakeShell().Icon).Sharper(App, Picture(Halves(small), small), small));
        // A 64-DIP bar draws them at 20 DIP.
        var wanted = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, new Config { Height = 64 }));
        Assert.True(wanted > small);
        var shell = new FakeShell();
        var icons = new Tray.ExeIcons(shell.Icon);

        var sharp = icons.Sharper(App, Picture(Halves(small), small), wanted);

        Assert.NotNull(sharp);
        Assert.Equal(wanted, sharp.Width);
        Assert.Equal(wanted, sharp.Height);
        // Compared at the app's own size, and taken at the bar's.
        Assert.Equal([(App, small), (App, wanted)], shell.Asked);
    }

    [Fact]
    public void APictureOfTheAppsOwnIsNeverSwappedForItsExecutables()
    {
        // A badge, another state: not the executable's icon, so the app's picture stays (scaled).
        var badged = Halves(24);
        for (var i = 0; i < 24 * 8 * 4; i += 4)
        {
            (badged[i], badged[i + 1], badged[i + 2]) = (40, 200, 40);
        }
        var icons = new Tray.ExeIcons(new FakeShell().Icon);

        Assert.Null(icons.Sharper(App, Picture(badged, 24), 36));
        // An executable the shell has no icon for.
        Assert.Null(icons.Sharper(@"C:\Apps\Other.exe", Picture(Halves(24), 24), 36));
    }

    [Fact]
    public void APictureAsLargeAsTheBarsNeedsNothing()
    {
        var shell = new FakeShell();
        var icons = new Tray.ExeIcons(shell.Icon);

        Assert.Null(icons.Sharper(App, Picture(Halves(36), 36), 36));
        Assert.Null(icons.Sharper(App, Picture(Halves(48), 48), 36));
        Assert.Empty(shell.Asked);
    }

    [Fact]
    public void TheShellIsAskedOncePerExecutableAndSize()
    {
        var shell = new FakeShell();
        var icons = new Tray.ExeIcons(shell.Icon);

        for (var time = 0; time < 3; time++)
        {
            Assert.NotNull(icons.Sharper(App, Picture(Halves(24), 24), 36));
        }

        Assert.Equal(2, shell.Asked.Count);
    }
}
