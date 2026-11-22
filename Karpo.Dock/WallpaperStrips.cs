// §547: the wallpaper under a bar, blurred by Karpo itself (docs\widgets.md §547; the pure half, how Windows lays a
// wallpaper out, is Karpo.Core\Wallpapers.cs).
//
//   Read      each display's desktop through IDesktopWallpaper — its picture (GetWallpaper; SPI_GETDESKWALLPAPER where it
//             says none: a slideshow's or Spotlight's picture as Windows last wrote it), how it is fitted (GetPosition) and
//             the desktop's colour (GetBackgroundColor) —, on the dock's thread, when the dock starts, when Windows says
//             the wallpaper changed (WM_SETTINGCHANGE) and when the displays do. Only ever read: nothing here sets anything.
//   Compose   the strip under a bar and the margin round it the blur reads (WallpaperKey.Strip), off the dock's thread: the
//             desktop's colour, then the picture's pieces (Wallpapers.Plan) decoded by WIC at the size Windows draws them,
//             only the rows and columns under the strip; past the display's edge, its edge drawn out.
//   Blur      the strip into a composition surface the bar's size, through Direct2D's Gaussian blur on the GPU, once a
//             strip or a blur's amount (KarpoLook.Sigma); a bar shows it under its see-through colour (Scene.Wallpaper).
//
// Nothing of it runs while the bar stands still: a strip is composed again only for another picture, display, scale or
// bar's rectangle, blurred again only for another amount.

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class WallpaperStrips
{
    // CLSID_DesktopWallpaper.
    private static readonly Guid DesktopWallpaperClass = new(0xC2CF3110, 0x460E, 0x4FC1, 0xB9, 0xD0, 0x8A, 0x1C, 0x0C, 0x9C, 0xC4, 0xBD);

    // CLSID_D2D1GaussianBlur.
    private static readonly Guid GaussianBlur = new(0x1FEB6D69, 0x2FE6, 0x4AC9, 0x8C, 0x58, 0x1D, 0x7F, 0x93, 0xE7, 0xA6, 0xA5);

    /// <summary>A display's desktop: where the display is on the screen, and what its desktop shows.</summary>
    public readonly record struct Desktop(RectI Monitor, WallpaperSource Source);

    /// <summary>
    /// tests and the stage: what each display's desktop shows in place of what Windows says (a test picture), by the
    /// display's rectangle; null for Windows'. Never the person's own wallpaper in a picture of the tests.
    /// </summary>
    internal static Func<RectI, WallpaperSource?>? Given { get; set; }

    /// <summary>
    /// What every display's desktop shows now, as IDesktopWallpaper says: one each, with its display's rectangle; none where
    /// it cannot be read. Only read, never set.
    /// </summary>
    public static List<Desktop> Read()
    {
        List<Desktop> found = [];
        IDesktopWallpaper* wallpaper = null;
        try
        {
            var clsid = DesktopWallpaperClass;
            if (CoCreateInstance(&clsid, null, (uint)(CLSCTX.CLSCTX_INPROC_SERVER | CLSCTX.CLSCTX_LOCAL_SERVER), __uuidof<IDesktopWallpaper>(), (void**)&wallpaper).FAILED)
            {
                return found;
            }
            var position = DESKTOP_WALLPAPER_POSITION.DWPOS_FILL;
            _ = wallpaper->GetPosition(&position);
            COLORREF colour = default;
            _ = wallpaper->GetBackgroundColor(&colour);
            var background = Colour((uint)colour);
            var fit = FitOf(position);
            uint count = 0;
            if (wallpaper->GetMonitorDevicePathCount(&count).FAILED)
            {
                return found;
            }
            var system = SystemWallpaper();
            for (uint at = 0; at < count; at++)
            {
                char* id = null;
                if (wallpaper->GetMonitorDevicePathAt(at, &id).FAILED || id is null)
                {
                    continue;
                }
                try
                {
                    RECT rect;
                    if (wallpaper->GetMonitorRECT(id, &rect).FAILED || rect.right <= rect.left || rect.bottom <= rect.top)
                    {
                        // A display not attached now.
                        continue;
                    }
                    char* path = null;
                    var file = "";
                    if (wallpaper->GetWallpaper(id, &path).SUCCEEDED && path is not null)
                    {
                        file = new string(path);
                    }
                    CoTaskMemFree(path);
                    found.Add(new Desktop(new RectI(rect.left, rect.top, rect.right, rect.bottom), Source(file.Length > 0 ? file : system, fit, background)));
                }
                finally
                {
                    CoTaskMemFree(id);
                }
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Troubles.Note("Wallpaper", error);
        }
        finally
        {
            ComUtil.Release(ref wallpaper);
        }
        return found;
    }

    /// <summary>
    /// What the display at <paramref name="monitor"/> shows of <paramref name="desktops"/> (<see cref="Read"/>, or the tests'
    /// own, <see cref="Given"/>); null for none known.
    /// </summary>
    public static WallpaperSource? Of(IReadOnlyList<Desktop> desktops, RectI monitor) =>
        Given is { } given ? given(monitor)
        : desktops.FirstOrDefault(desktop => desktop.Monitor == monitor) is { Monitor.Width: > 0 } shown ? shown.Source
        : null;

    /// <summary>The picture Windows draws the desktop of, as it says it (a slideshow's, Spotlight's: the one it wrote last); empty for none.</summary>
    private static string SystemWallpaper()
    {
        var path = stackalloc char[(int)MAX.MAX_PATH + 1];
        return SystemParametersInfoW(SPI.SPI_GETDESKWALLPAPER, MAX.MAX_PATH, path, 0) ? new string(path) : "";
    }

    /// <summary><paramref name="file"/> as a desktop's picture, when it was written its stamp; none where there is no such file.</summary>
    internal static WallpaperSource Source(string? file, WallpaperFit fit, Argb background)
    {
        try
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file))
            {
                return new WallpaperSource(file, File.GetLastWriteTimeUtc(file).Ticks, fit, background);
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
        return new WallpaperSource(null, 0, fit, background);
    }

    private static WallpaperFit FitOf(DESKTOP_WALLPAPER_POSITION position) => position switch
    {
        DESKTOP_WALLPAPER_POSITION.DWPOS_CENTER => WallpaperFit.Center,
        DESKTOP_WALLPAPER_POSITION.DWPOS_TILE => WallpaperFit.Tile,
        DESKTOP_WALLPAPER_POSITION.DWPOS_STRETCH => WallpaperFit.Stretch,
        DESKTOP_WALLPAPER_POSITION.DWPOS_FIT => WallpaperFit.Fit,
        DESKTOP_WALLPAPER_POSITION.DWPOS_SPAN => WallpaperFit.Span,
        _ => WallpaperFit.Fill,
    };

    /// <summary>A COLORREF (0x00BBGGRR), opaque.</summary>
    private static Argb Colour(uint bgr) => new(255, (byte)bgr, (byte)(bgr >> 8), (byte)(bgr >> 16));

    /// <summary>
    /// The strip of <paramref name="key"/> (<see cref="WallpaperKey.Strip"/>) drawn of its desktop, premultiplied BGRA, opaque:
    /// its colour, its picture's pieces as Windows lays them out, past the display's edge its edge drawn out. Off the dock's
    /// thread; null where it cannot be (a strip of no size). A picture WIC cannot read is the desktop's colour.
    /// </summary>
    public static Pixels? Compose(WallpaperKey key)
    {
        var strip = key.Strip;
        var (width, height) = (strip.Width, strip.Height);
        if (width <= 0 || height <= 0 || (long)width * height > 64L * 1024 * 1024)
        {
            return null;
        }
        var data = new byte[width * height * 4];
        var colour = key.Source.Background;
        for (var at = 0; at < data.Length; at += 4)
        {
            (data[at], data[at + 1], data[at + 2], data[at + 3]) = (colour.B, colour.G, colour.R, 255);
        }
        var plan = Wallpapers.Plan(key.Source.Fit, 0, 0, key.Monitor, key.Desktop, strip);
        if (!key.Source.Solid)
        {
            plan = Draw(key, data, width) ?? plan;
        }
        Extend(data, width, height, plan.Inside);
        return new Pixels { Data = data, Width = width, Height = height };
    }

    /// <summary>
    /// <paramref name="key"/>'s picture drawn into <paramref name="data"/> (its strip, <paramref name="width"/> px wide): each
    /// piece scaled as Windows draws it, only its part under the strip read, laid over the desktop's colour. Its plan; null
    /// where WIC cannot read the picture.
    /// </summary>
    private static WallpaperPlan? Draw(WallpaperKey key, byte[] data, int width)
    {
        var clsid = CLSID.CLSID_WICImagingFactory2;
        IWICImagingFactory* wic = null;
        IWICBitmapDecoder* decoder = null;
        IWICBitmapFrameDecode* frame = null;
        IWICBitmapScaler* scaler = null;
        IWICFormatConverter* converter = null;
        try
        {
            if (CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic).FAILED)
            {
                return null;
            }
            fixed (char* path = key.Source.Path)
            {
                if (wic->CreateDecoderFromFilename(path, null, GENERIC_READ, WICDecodeOptions.WICDecodeMetadataCacheOnDemand, &decoder).FAILED
                    || decoder->GetFrame(0, &frame).FAILED)
                {
                    return null;
                }
            }
            uint wide, high;
            if (frame->GetSize(&wide, &high).FAILED || wide == 0 || high == 0)
            {
                return null;
            }
            var plan = Wallpapers.Plan(key.Source.Fit, (int)wide, (int)high, key.Monitor, key.Desktop, key.Strip);
            if (plan.Pieces.Count == 0)
            {
                return plan;
            }
            // Every piece of a plan is the picture at one size: one scaler and one converter for them all.
            var (drawnWidth, drawnHeight) = (plan.Pieces[0].Width, plan.Pieces[0].Height);
            var source = (IWICBitmapSource*)frame;
            if (drawnWidth != wide || drawnHeight != high)
            {
                if (wic->CreateBitmapScaler(&scaler).FAILED
                    || scaler->Initialize(source, (uint)drawnWidth, (uint)drawnHeight, WICBitmapInterpolationMode.WICBitmapInterpolationModeHighQualityCubic).FAILED)
                {
                    return null;
                }
                source = (IWICBitmapSource*)scaler;
            }
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            if (wic->CreateFormatConverter(&converter).FAILED
                || converter->Initialize(source, &format, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom).FAILED)
            {
                return null;
            }
            foreach (var piece in plan.Pieces)
            {
                var (across, down) = (piece.From.Width, piece.From.Height);
                var part = new byte[across * down * 4];
                var rect = new WICRect { X = piece.From.Left, Y = piece.From.Top, Width = across, Height = down };
                fixed (byte* into = part)
                {
                    if (converter->CopyPixels(&rect, (uint)across * 4, (uint)part.Length, into).FAILED)
                    {
                        return null;
                    }
                }
                Over(part, across, down, data, width, piece.ToX, piece.ToY);
            }
            return plan;
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            ComUtil.Release(ref converter);
            ComUtil.Release(ref scaler);
            ComUtil.Release(ref frame);
            ComUtil.Release(ref decoder);
            ComUtil.Release(ref wic);
        }
    }

    /// <summary><paramref name="part"/> (premultiplied) laid over <paramref name="data"/> at (<paramref name="x"/>, <paramref name="y"/>): a see-through picture over the desktop's colour.</summary>
    private static void Over(byte[] part, int across, int down, byte[] data, int width, int x, int y)
    {
        for (var row = 0; row < down; row++)
        {
            var from = row * across * 4;
            var to = (((y + row) * width) + x) * 4;
            for (var column = 0; column < across * 4; column += 4)
            {
                var alpha = part[from + column + 3];
                if (alpha == 255)
                {
                    Buffer.BlockCopy(part, from + column, data, to + column, 3);
                    continue;
                }
                var keep = 255 - alpha;
                for (var channel = 0; channel < 3; channel++)
                {
                    data[to + column + channel] = (byte)(part[from + column + channel] + ((data[to + column + channel] * keep) + 127) / 255);
                }
            }
        }
    }

    /// <summary>
    /// What of <paramref name="data"/> is outside <paramref name="inside"/> (the display's part of the strip) drawn as its
    /// nearest edge, so the blur reads the display's edge past it, not black; nothing where the strip is off the display.
    /// </summary>
    internal static void Extend(byte[] data, int width, int height, RectI inside)
    {
        if (inside.Width <= 0 || inside.Height <= 0)
        {
            return;
        }
        var stride = width * 4;
        for (var row = inside.Top; row < inside.Bottom; row++)
        {
            var line = row * stride;
            for (var column = 0; column < inside.Left; column++)
            {
                Buffer.BlockCopy(data, line + (inside.Left * 4), data, line + (column * 4), 4);
            }
            for (var column = inside.Right; column < width; column++)
            {
                Buffer.BlockCopy(data, line + ((inside.Right - 1) * 4), data, line + (column * 4), 4);
            }
        }
        for (var row = 0; row < inside.Top; row++)
        {
            Buffer.BlockCopy(data, inside.Top * stride, data, row * stride, stride);
        }
        for (var row = inside.Bottom; row < height; row++)
        {
            Buffer.BlockCopy(data, (inside.Bottom - 1) * stride, data, row * stride, stride);
        }
    }

    /// <summary>
    /// <paramref name="strip"/> (a bar's strip and <paramref name="margin"/> px round it) blurred at <paramref name="sigma"/>
    /// px into a surface the bar's own size — <paramref name="kept"/> drawn again where it is that size —: Direct2D's Gaussian
    /// blur, the margin all it reads past the bar; sharp at 0.
    /// </summary>
    public static CompositionDrawingSurface Blurred(Gfx gfx, Pixels strip, int margin, float sigma, CompositionDrawingSurface? kept)
    {
        var (width, height) = (Math.Max(1, strip.Width - (2 * margin)), Math.Max(1, strip.Height - (2 * margin)));
        if (kept is not null && kept.Size.Width == width && kept.Size.Height == height)
        {
            gfx.Redraw(kept, dc => Paint(dc, strip, margin, sigma));
            return kept;
        }
        return gfx.Draw(width, height, dc => Paint(dc, strip, margin, sigma));
    }

    /// <summary><see cref="Blurred"/>'s drawing: the strip, its margin outside the surface, blurred where <paramref name="sigma"/> is more than a trace.</summary>
    private static void Paint(ID2D1DeviceContext* dc, Pixels strip, int margin, float sigma)
    {
        var properties = new D2D1_BITMAP_PROPERTIES1
        {
            pixelFormat = new D2D1_PIXEL_FORMAT
            {
                format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM,
                alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED,
            },
            dpiX = 96,
            dpiY = 96,
            bitmapOptions = D2D1_BITMAP_OPTIONS.D2D1_BITMAP_OPTIONS_NONE,
            colorContext = null,
        };
        ID2D1Bitmap1* bitmap = null;
        ID2D1Effect* blur = null;
        try
        {
            fixed (byte* data = strip.Data)
            {
                ComUtil.Check(dc->CreateBitmap(new D2D_SIZE_U((uint)strip.Width, (uint)strip.Height), data, (uint)strip.Width * 4, &properties, &bitmap));
            }
            var at = new D2D_POINT_2F(-margin, -margin);
            if (sigma < 0.5f)
            {
                dc->DrawImage((ID2D1Image*)bitmap, &at, null, D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_NEAREST_NEIGHBOR, D2D1_COMPOSITE_MODE.D2D1_COMPOSITE_MODE_SOURCE_COPY);
                return;
            }
            var guid = GaussianBlur;
            ComUtil.Check(dc->CreateEffect(&guid, &blur));
            blur->SetInput(0, (ID2D1Image*)bitmap, 1);
            var deviation = sigma;
            var hard = D2D1_BORDER_MODE.D2D1_BORDER_MODE_HARD;
            ComUtil.Check(blur->SetValue((uint)D2D1_GAUSSIANBLUR_PROP.D2D1_GAUSSIANBLUR_PROP_STANDARD_DEVIATION, D2D1_PROPERTY_TYPE.D2D1_PROPERTY_TYPE_FLOAT, (byte*)&deviation, sizeof(float)));
            ComUtil.Check(blur->SetValue((uint)D2D1_GAUSSIANBLUR_PROP.D2D1_GAUSSIANBLUR_PROP_BORDER_MODE, D2D1_PROPERTY_TYPE.D2D1_PROPERTY_TYPE_ENUM, (byte*)&hard, sizeof(uint)));
            dc->DrawImage(blur, &at, null, D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_LINEAR, D2D1_COMPOSITE_MODE.D2D1_COMPOSITE_MODE_SOURCE_COPY);
        }
        finally
        {
            ComUtil.Release(ref blur);
            ComUtil.Release(ref bitmap);
        }
    }
}
