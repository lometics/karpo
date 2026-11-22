// Drawing a widget without a screen: an STA thread with a dispatcher queue and a software Direct2D
// target over a WIC bitmap, the pixels read back, and the bitmap saved as a PNG.
//
// The widgets' own tests use this to measure what a widget really draws — where its ink lands, how
// wide its cells come out — rather than trusting the numbers that went in. Nothing here makes a
// window, so it needs no hidden desktop; the tests that show real popups have their own harness.

using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Islands;

internal static unsafe class Offscreen
{
    internal delegate void Painter(ID2D1DeviceContext* dc);

    internal delegate void Ready(IWICBitmap* bitmap);

    /// <summary>Where the pictures go (KARPO_PNG_DIR, else the temp folder).</summary>
    internal static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    /// <summary>Runs <paramref name="body"/> on an STA thread with a dispatcher queue and a software Gfx.</summary>
    internal static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }

    /// <summary>
    /// Draws into a transparent bitmap and then hands it over. The drawing has to have ended and
    /// the render target let go before anything can lock the bitmap: WIC refuses
    /// (WINCODEC_ERR_ALREADYLOCKED) while Direct2D still holds it.
    /// </summary>
    internal static void Bitmap(float width, float height, Painter draw, Ready ready)
    {
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap((uint)MathF.Ceiling(width), (uint)MathF.Ceiling(height), &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var clear = new DXGI_RGBA(0, 0, 0, 0);
            dc->Clear(&clear);
            draw(dc);
            ComUtil.Check(dc->EndDraw(null, null));
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ready(bitmap);
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    /// <summary>
    /// The box of the pixels with ink on them — more opaque than <paramref name="least"/>, so a faded
    /// backdrop can be left out —; all zero when nothing was drawn.
    /// </summary>
    internal static (float Left, float Top, float Right, float Bottom) InkOf(IWICBitmap* bitmap, uint width, uint height, byte least = 24)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            float left = width;
            float top = height;
            float right = 0;
            float bottom = 0;
            for (var y = 0u; y < height; y++)
            {
                for (var x = 0u; x < width; x++)
                {
                    // Premultiplied BGRA: the alpha is the fourth byte.
                    if (data[(y * stride) + (x * 4) + 3] > least)
                    {
                        left = MathF.Min(left, x);
                        top = MathF.Min(top, y);
                        right = MathF.Max(right, x + 1);
                        bottom = MathF.Max(bottom, y + 1);
                    }
                }
            }
            return right > left ? (left, top, right, bottom) : (0, 0, 0, 0);
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    /// <summary>How opaque the pixel at (<paramref name="x"/>, <paramref name="y"/>) is, 0 to 255; 0 off the bitmap.</summary>
    internal static byte AlphaAt(IWICBitmap* bitmap, uint width, uint height, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return 0;
        }
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)width, Height = (int)height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            // Premultiplied BGRA: the alpha is the fourth byte.
            return data[((uint)y * stride) + ((uint)x * 4) + 3];
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    /// <summary>Draws with the origin moved, without disturbing what the caller set.</summary>
    internal static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = (x * before._11) + (y * before._21) + before._31;
        moved._32 = (x * before._12) + (y * before._22) + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    internal static void Save(IWICBitmap* bitmap, string path)
    {
        IWICImagingFactory* wic = null;
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
            ComUtil.Release(ref wic);
        }
    }
}
