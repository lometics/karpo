// A sheet of Segoe Fluent Icons code points, so a glyph for a widget is chosen
// by looking at it rather than by guessing. Runs only with KARPO_UI_TESTS=1 and
// KARPO_GLYPH_SHEET=1; the pictures go to KARPO_PNG_DIR.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.QuickSettings;

public sealed unsafe class GlyphProbe
{
    [UiFact]
    public void Sheets()
    {
        if (Environment.GetEnvironmentVariable("KARPO_GLYPH_SHEET") != "1")
        {
            return;
        }
        var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
            ? named
            : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
        Directory.CreateDirectory(folder);
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
                    foreach (var start in new[] { 0xE700, 0xE800, 0xE900, 0xEA00, 0xEB00, 0xEC00, 0xED00, 0xEE00, 0xF100 })
                    {
                        Sheet(gfx, start, Path.Combine(folder, $"glyphs-{start:X4}.png"));
                    }
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
        thread.Start();
        thread.Join();
        Assert.Null(failed);
    }

    private static void Sheet(Gfx gfx, int start, string path)
    {
        const int columns = 16;
        const int rows = 16;
        const float cell = 74;
        var width = (uint)(columns * cell);
        var height = (uint)(rows * cell);
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
            ComUtil.Check(wic->CreateBitmap(width, height, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
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
            var white = new DXGI_RGBA(1, 1, 1, 1);
            dc->Clear(&white);
            var ink = new DXGI_RGBA(0.1f, 0.1f, 0.12f, 1);
            var quiet = new DXGI_RGBA(0.45f, 0.45f, 0.5f, 1);
            for (var i = 0; i < columns * rows; i++)
            {
                var code = start + i;
                var box = PopupPaint.Rect(i % columns * cell, i / columns * cell, cell, cell - 18);
                PopupPaint.Glyph(dc, gfx, char.ConvertFromUtf32(code), 28, ink, box);
                PopupPaint.Centred(dc, gfx, code.ToString("X4"), 11, Weight.Regular, quiet,
                    PopupPaint.Rect(i % columns * cell, i / columns * cell + cell - 20, cell, 18), ink: false);
            }
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, path);
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

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
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
        }
    }
}
