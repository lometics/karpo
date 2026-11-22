// How a tray icon's picture is taken at the bar's size (Tray.Render): an icon an app loaded from its
// own icon group at another size is taken from that group at the bar's size, the way an app's small
// icon for the display's DPI would have come, rather than scaled down from the large one. GDI only:
// no window is made and no tray is touched.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.TrayIcons;

public sealed unsafe class TrayRenderTests
{
    private const uint ImageIcon = 1;
    private const uint Shared = 0x8000; // LR_SHARED

    // shell32.dll's folder: an icon group with a 16-px and a 32-px image, as an app's own icon has.
    private const int Folder = 4;

    /// <summary>shell32's folder at <paramref name="size"/> px, loaded as an app loads its icon (LR_SHARED), so it carries its group.</summary>
    private static HICON Loaded(int size)
    {
        HMODULE module;
        fixed (char* name = "shell32.dll")
        {
            module = LoadLibraryW(name);
        }
        Assert.NotEqual(HMODULE.NULL, module);
        var icon = (HICON)(nint)LoadImageW((HINSTANCE)(nint)module.Value, (char*)(nuint)Folder, ImageIcon, size, size, Shared).Value;
        Assert.NotEqual(HICON.NULL, icon);
        return icon;
    }

    [Fact]
    public void ALargeIconFromAGroupIsTakenAtTheBarsSizeFromTheGroup()
    {
        // A shared icon is the system's, not ours to destroy.
        var large = Loaded(32);
        var small = Loaded(16);

        var fromLarge = Tray.Render(large, 16);
        var fromSmall = Tray.Render(small, 16);
        Assert.NotNull(fromLarge);
        Assert.NotNull(fromSmall);

        Assert.Equal(16, fromLarge.Width);
        Assert.Equal(16, fromLarge.Height);
        // The group's own 16-px image, not the 32-px one scaled down.
        Assert.Equal(fromSmall.Data, fromLarge.Data);
    }

    [Fact]
    public void AnIconAtTheBarsSizeIsTakenAsItIs()
    {
        var small = Loaded(16);

        var picture = Tray.Render(small, 16);
        Assert.NotNull(picture);

        Assert.Equal(16 * 16 * 4, picture.Data.Length);
        Assert.Contains(picture.Data, value => value != 0);
    }

    [Fact]
    public void NoIconIsNoPicture() => Assert.Null(Tray.Render(HICON.NULL, 16));

    /// <summary>
    /// The person: *"tray icons are drawn at very low resolution"*. At every DPI the bar asks for its
    /// own size (an app icon's 24 DIP on the 48-DIP bar), and an icon an app loaded small from its
    /// group (its 16 DIP for the display) is taken from the group at that size, not stretched.
    /// </summary>
    [Theory]
    [InlineData(96u)]
    [InlineData(120u)]
    [InlineData(144u)]
    [InlineData(192u)]
    public void TheSourceIsTakenAtTheBarsSizeForTheDpi(uint dpi)
    {
        var wanted = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, new Config { Height = 48 }));
        var small = (int)MathF.Round(16 * dpi / 96f);
        var handed = Loaded(small);

        var (picture, native) = Tray.Capture(handed, wanted);

        Assert.NotNull(picture);
        Assert.Equal(wanted, picture.Width);
        // The group's own image at that size, as an app would have loaded it.
        Assert.Equal(Tray.Render(Loaded(wanted), wanted)!.Data, picture.Data);
        // Nothing to sharpen later: it is sharp already.
        Assert.Null(native);
    }

    /// <summary>
    /// An icon made in memory has its one picture: taken at its own size and filtered up to the bar's
    /// (TrayPictures.Resize) — DrawIconEx copied its pixels, a stair at every edge — and handed back
    /// as it was, for its executable's icon to be compared with (Tray.ExeIcons).
    /// </summary>
    [Fact]
    public void AnIconMadeInMemoryIsFilteredUpToTheBarsSize()
    {
        var icon = Made(24);
        try
        {
            var (picture, native) = Tray.Capture(icon, 36);

            Assert.NotNull(picture);
            Assert.NotNull(native);
            Assert.Equal(36, picture.Width);
            Assert.Equal(24, native.Width);
            Assert.Equal(Karpo.Core.TrayPictures.Resize(native.Data, 24, 36), picture.Data);
            // Colours between the halves' red and blue: filtered, not copied.
            var colours = new HashSet<int>();
            for (var i = 0; i < picture.Data.Length; i += 4)
            {
                colours.Add(BitConverter.ToInt32(picture.Data, i));
            }
            Assert.True(colours.Count > 2);
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    [Fact]
    public void AnIconMadeLargerIsFilteredDownAndNeedsNothingMore()
    {
        var icon = Made(48);
        try
        {
            var (picture, native) = Tray.Capture(icon, 36);

            Assert.NotNull(picture);
            Assert.Equal(36, picture.Width);
            Assert.Null(native);
        }
        finally
        {
            DestroyIcon(icon);
        }
    }

    /// <summary>An icon made in memory, as most apps make their tray icons: <paramref name="side"/> px, red on the left half and blue on the right, opaque.</summary>
    private static HICON Made(int side)
    {
        var header = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = side,
                biHeight = -side,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = (uint)BI.BI_RGB,
            },
        };
        void* bits = null;
        var colour = CreateDIBSection(HDC.NULL, &header, (uint)DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        Assert.NotEqual(HBITMAP.NULL, colour);
        var pixels = new Span<byte>(bits, side * side * 4);
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var at = (y * side + x) * 4;
                (pixels[at], pixels[at + 1], pixels[at + 2], pixels[at + 3]) = x < side / 2 ? ((byte)0, (byte)0, (byte)255, (byte)255) : ((byte)255, (byte)0, (byte)0, (byte)255);
            }
        }
        var maskBits = new byte[((side + 15) / 16) * 2 * side];
        HBITMAP mask;
        fixed (byte* zero = maskBits)
        {
            mask = CreateBitmap(side, side, 1, 1, zero);
        }
        var info = new ICONINFO { fIcon = BOOL.TRUE, hbmColor = colour, hbmMask = mask };
        var icon = CreateIconIndirect(&info);
        DeleteObject((HGDIOBJ)colour.Value);
        DeleteObject((HGDIOBJ)mask.Value);
        Assert.NotEqual(HICON.NULL, icon);
        return icon;
    }
}
