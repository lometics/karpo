// Downloaded bytes to pixels the bar can draw.
//
// **What this stack decodes.** WIC's built-in codecs are ICO, PNG, JPEG, GIF, BMP and TIFF, and
// that is what a favicon is in practice. There is **no WIC SVG codec** on Windows — Direct2D can
// draw an SVG document (ID2D1DeviceContext5), but that is a device-bound path with its own subset
// of SVG and it is not this one. So an SVG favicon is **not decoded**: it is ranked last among the
// candidates (FaviconUrls.Rank) and, if it is the only thing a site offers, the pin keeps the
// letter tile it already had. That is the stated fallback, and it is a fallback rather than a
// failure — nothing blanks, nothing flashes.
//
// **A multi-size .ico** is several frames in one file, which is the whole reason the format is
// still used here. BestFrame picks by the same rule the ranking uses: the smallest frame at least
// as large as the cell, else the largest there is, because shrinking a picture keeps detail that
// growing one invents. That choice is a pure function and is tested as one.

using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.Favicons;

internal static unsafe class FaviconImage
{
    /// <summary>
    /// Whether this build can draw what is at <paramref name="address"/>, judged by its extension
    /// and the content type the server gave. Only SVG is ruled out, and only because WIC has no
    /// codec for it; everything else is tried and simply fails to decode if it is not a picture.
    /// </summary>
    public static bool Decodable(string address, string? contentType)
    {
        if (contentType is { Length: > 0 } type && type.Contains("svg", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var path = Uri.TryCreate(address, UriKind.Absolute, out var url) ? url.AbsolutePath : address;
        return !path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Which frame of a multi-size picture to draw in a <paramref name="wanted"/> px cell: the
    /// smallest that is at least as large, else the largest there is. -1 for no frames at all.
    /// </summary>
    public static int BestFrame(IReadOnlyList<int> sides, int wanted)
    {
        var best = -1;
        for (var frame = 0; frame < sides.Count; frame++)
        {
            if (sides[frame] <= 0)
            {
                continue;
            }
            if (best < 0)
            {
                best = frame;
                continue;
            }
            best = Better(sides[frame], sides[best], wanted) ? frame : best;
        }
        return best;
    }

    /// <summary>Whether <paramref name="side"/> is the better of the two for a wanted cell.</summary>
    private static bool Better(int side, int against, int wanted)
    {
        var fits = side >= wanted;
        var otherFits = against >= wanted;
        if (fits != otherFits)
        {
            // One of them is big enough: that one, because growing a small picture invents detail.
            return fits;
        }
        // Both big enough: the smaller, which is the least shrinking. Neither: the larger.
        return fits ? side < against : side > against;
    }

    /// <summary>
    /// <paramref name="bytes"/> decoded into a square of at most <paramref name="size"/> px, as
    /// premultiplied pixels the bar draws like any other icon. Null when nothing here can read them
    /// — an SVG, an HTML error page served as a picture, a truncated file — and null is the signal
    /// to keep whatever the pin already shows.
    /// <para>
    /// Called from the fetching thread, never from the dock's: it is CPU work on a downloaded blob.
    /// WIC is free-threaded and this creates its own factory each time, so no state is shared.
    /// </para>
    /// </summary>
    public static Pixels? Decode(byte[] bytes, int size)
    {
        if (bytes.Length == 0 || size <= 0)
        {
            return null;
        }
        var clsid = CLSID.CLSID_WICImagingFactory2;
        IWICImagingFactory* wic = null;
        IWICStream* stream = null;
        IWICBitmapDecoder* decoder = null;
        IWICBitmapFrameDecode* frame = null;
        IWICBitmapScaler* scaler = null;
        IWICFormatConverter* converter = null;
        try
        {
            if (CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic).FAILED
                || wic->CreateStream(&stream).FAILED)
            {
                return null;
            }
            fixed (byte* data = bytes)
            {
                if (stream->InitializeFromMemory(data, (uint)bytes.Length).FAILED)
                {
                    return null;
                }
            }
            if (wic->CreateDecoderFromStream((IStream*)stream, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand, &decoder).FAILED)
            {
                return null;
            }
            uint frames;
            if (decoder->GetFrameCount(&frames).FAILED || frames == 0)
            {
                return null;
            }
            // An .ico holds one frame per size; every other format holds one frame that is the
            // picture. The same rule answers both, and for one frame it answers "that one".
            var sides = new List<int>((int)frames);
            for (uint at = 0; at < frames; at++)
            {
                IWICBitmapFrameDecode* measured = null;
                uint wide = 0;
                uint high = 0;
                if (decoder->GetFrame(at, &measured).SUCCEEDED)
                {
                    _ = measured->GetSize(&wide, &high);
                }
                ComUtil.Release(ref measured);
                sides.Add((int)Math.Min(wide, high));
            }
            var best = BestFrame(sides, size);
            if (best < 0 || decoder->GetFrame((uint)best, &frame).FAILED)
            {
                return null;
            }
            uint width;
            uint height;
            if (frame->GetSize(&width, &height).FAILED || width == 0 || height == 0)
            {
                return null;
            }
            // Never enlarged: a 16 px favicon in a 24 px cell is drawn at 16 and centred, which is
            // honest, where stretching it would be four blurred pixels pretending to be detail.
            var fit = MathF.Min(1, size / (float)Math.Max(width, height));
            var scaledWidth = (uint)Math.Max(1, MathF.Round(width * fit));
            var scaledHeight = (uint)Math.Max(1, MathF.Round(height * fit));
            if (wic->CreateBitmapScaler(&scaler).FAILED
                || scaler->Initialize((IWICBitmapSource*)frame, scaledWidth, scaledHeight, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant).FAILED
                || wic->CreateFormatConverter(&converter).FAILED)
            {
                return null;
            }
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            if (converter->Initialize((IWICBitmapSource*)scaler, &format, WICBitmapDitherType.WICBitmapDitherTypeNone, null, 0, WICBitmapPaletteType.WICBitmapPaletteTypeCustom).FAILED)
            {
                return null;
            }
            var pixels = new byte[scaledWidth * scaledHeight * 4];
            var rect = new WICRect { X = 0, Y = 0, Width = (int)scaledWidth, Height = (int)scaledHeight };
            fixed (byte* into = pixels)
            {
                if (converter->CopyPixels(&rect, scaledWidth * 4, (uint)pixels.Length, into).FAILED)
                {
                    return null;
                }
            }
            return Blank(pixels) ? null : new Pixels { Data = pixels, Width = (int)scaledWidth, Height = (int)scaledHeight };
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
            ComUtil.Release(ref stream);
            ComUtil.Release(ref wic);
        }
    }

    /// <summary>
    /// Nothing but transparent pixels: some sites answer /favicon.ico with a 1×1 clear GIF, which
    /// decodes perfectly and draws as an empty square. That counts as no icon, so the letter stays.
    /// </summary>
    private static bool Blank(byte[] pixels)
    {
        for (var at = 3; at < pixels.Length; at += 4)
        {
            if (pixels[at] != 0)
            {
                return false;
            }
        }
        return true;
    }
}
