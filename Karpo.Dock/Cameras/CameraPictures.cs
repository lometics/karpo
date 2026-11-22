// camera: a camera's picture — an MJPEG stream's part or a snapshot — decoded into pixels the line draws
// (docs\widgets.md §331). WIC's own decoders (JPEG, PNG, GIF, BMP, TIFF, and WebP where Windows has its
// codec), on the stream's thread and never the dock's; one decoding for both ways a picture comes.
//
// A picture is decoded no larger than the line shows it (Most, its longer side in px): a 1600 px
// camera in a 540 px line costs the line's pixels, not the camera's. It is never enlarged — the line's
// drawing scales it up where it has to, which invents nothing a decoder would not.

using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static unsafe class CameraPictures
{
    /// <summary>
    /// <paramref name="bytes"/> decoded into premultiplied pixels whose longer side is at most
    /// <paramref name="most"/> px, its shape kept; null where WIC cannot read them (half a picture, an
    /// error page). WIC is free-threaded; each call makes its own factory, so nothing is shared. Into the
    /// next of <paramref name="ring"/>'s arrays where one is given (a stream's), else a new one.
    /// </summary>
    public static Pixels? Decode(ReadOnlySpan<byte> bytes, int most, PixelRing? ring = null)
    {
        if (bytes.Length == 0 || most <= 0)
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
                // The stream reads the bytes where they are, so everything is done while they are pinned.
                if (stream->InitializeFromMemory(data, (uint)bytes.Length).FAILED
                    || wic->CreateDecoderFromStream((IStream*)stream, null, WICDecodeOptions.WICDecodeMetadataCacheOnDemand, &decoder).FAILED
                    || decoder->GetFrame(0, &frame).FAILED)
                {
                    return null;
                }
                uint width;
                uint height;
                if (frame->GetSize(&width, &height).FAILED || width == 0 || height == 0)
                {
                    return null;
                }
                var (wide, high) = Fitted((int)width, (int)height, most);
                IWICBitmapSource* source = (IWICBitmapSource*)frame;
                if (wide != width || high != height)
                {
                    if (wic->CreateBitmapScaler(&scaler).FAILED
                        || scaler->Initialize(source, (uint)wide, (uint)high, WICBitmapInterpolationMode.WICBitmapInterpolationModeFant).FAILED)
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
                var picture = ring?.Next(wide, high) ?? new Pixels { Data = new byte[wide * high * 4], Width = wide, Height = high };
                var rect = new WICRect { X = 0, Y = 0, Width = wide, Height = high };
                fixed (byte* into = picture.Data)
                {
                    if (converter->CopyPixels(&rect, (uint)wide * 4, (uint)picture.Data.Length, into).FAILED)
                    {
                        return null;
                    }
                }
                return picture;
            }
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

    /// <summary>A <paramref name="width"/> × <paramref name="height"/> picture's size with its longer side at most <paramref name="most"/>, its shape kept; never larger.</summary>
    public static (int Width, int Height) Fitted(int width, int height, int most)
    {
        var fit = MathF.Min(1, most / (float)Math.Max(width, height));
        return (Math.Max(1, (int)MathF.Round(width * fit)), Math.Max(1, (int)MathF.Round(height * fit)));
    }
}
