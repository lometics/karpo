// Pictures of Karpo's own windows as the compositor draws them, for the tests that look at what a
// window really shows rather than at a second drawing of it: a window made on a desktop of its own
// is asked for its full content (PrintWindow, PW_RENDERFULLCONTENT, which takes the composition
// tree as well as the window's own pixels) and saved as a PNG.
//
// Nothing here is ever on the person's own desktop: the windows are made on a hidden one, by a
// thread that set itself there before it owned any window.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Edit;

internal static unsafe class WindowShots
{
    private const uint PW_RENDERFULLCONTENT = 2;

    /// <summary>Runs <paramref name="body"/> on a thread of its own on a hidden desktop, with a dispatcher queue and a software Gfx.</summary>
    internal static void OnHiddenDesktop(string what, Action<Gfx, HINSTANCE> body)
    {
        Exception? failed = null;
        HDESK hidden = default;
        var thread = new Thread(() =>
        {
            try
            {
                fixed (char* name = $"karpo-{what}-" + Guid.NewGuid().ToString("N"))
                {
                    hidden = CreateDesktopW(name, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
                }
                Assert.True(hidden != HDESK.NULL, "no hidden desktop");
                // Before this thread owns any window: its windows are then made there.
                Assert.True(SetThreadDesktop(hidden));
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
                    var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                    FlyoutOffscreenTests.Register(instance);
                    body(gfx, instance);
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
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the hidden desktop: " + failed);
        }
    }

    /// <summary>Lets the window's thread run its queue for a moment, so a composition commit has landed.</summary>
    internal static void Settle(int ms = 150)
    {
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(10);
        }
        _ = DwmFlush();
    }

    /// <summary>
    /// Where <paramref name="window"/> shows on the screen, what the tests call its window: a popup's region
    /// — its window spans its display (FlyoutHost.FrameOn) and its region is where the popup stands, which
    /// was its window before §355 — else the window. Every picture of a window and every place in "window
    /// px" here is of this; a message's client px are <see cref="Client"/>'s.
    /// </summary>
    internal static RECT Extent(HWND window)
    {
        RECT box;
        _ = GetWindowRect(window, &box);
        RECT region;
        if (!IsFrame(window, box) || GetWindowRgnBox(window, &region) is not (SIMPLEREGION or COMPLEXREGION))
        {
            return box;
        }
        return new RECT { left = box.left + region.left, top = box.top + region.top, right = box.left + region.right, bottom = box.top + region.bottom };
    }

    /// <summary>A point of <paramref name="window"/>'s <see cref="Extent"/> (px from its corner) in the window's client px, as a mouse message has it.</summary>
    internal static POINT Client(HWND window, int x, int y)
    {
        RECT box;
        _ = GetWindowRect(window, &box);
        var extent = Extent(window);
        return new POINT { x = x + extent.left - box.left, y = y + extent.top - box.top };
    }

    /// <summary>Whether <paramref name="window"/>, standing at <paramref name="box"/>, is a popup's frame: its display less a px on its far sides, at least (§355).</summary>
    private static bool IsFrame(HWND window, RECT box)
    {
        var monitor = MonitorFromWindow(window, MONITOR.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!GetMonitorInfoW(monitor, &info))
        {
            return false;
        }
        var frame = FlyoutHost.FrameOn(info.rcMonitor);
        // At least that: a test's made-up screen larger than the display grows the frame to hold it.
        return box.left <= frame.left && box.top <= frame.top && box.right >= frame.right && box.bottom >= frame.bottom;
    }
    /// <summary>
    /// <paramref name="window"/> as it shows now, composition and all, cut to <paramref name="part"/>
    /// (px of its <see cref="Extent"/>; the whole of it where it is empty) and saved to <paramref name="file"/> in the
    /// pictures folder. True when the picture holds anything but one colour — a capture that came
    /// back blank says so rather than leaving a black square to be mistaken for a picture.
    /// <paramref name="seen"/>, when given, is handed the picture's pixels (BGRA) and its width, for
    /// a test that measures what it shows. With <paramref name="alpha"/> the pixels keep the capture's
    /// own alpha (premultiplied), for a window whose clear room shows what is under it.
    /// </summary>
    internal static bool Save(HWND window, string file, RECT part = default, Action<byte[], int>? seen = null, bool alpha = false)
    {
        RECT box;
        _ = GetWindowRect(window, &box);
        // §515: PrintWindow's full content of a window an odd number of px tall comes back resampled, every px a little
        // further along than it is drawn (2026-11-05, a 1 px line at 1500 px: 1920 × 255 showed it at 1501.4, × 129 at 1503.5,
        // × 73 at 1506.5; × 72, 128, 200 and 1080 exactly there), so a card's edge on whole px read as smeared over two and the
        // bar's far end px off. Taken one px taller, the row past its foot empty, and stood back after.
        var odd = (box.bottom - box.top) % 2 != 0;
        if (odd)
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, box.right - box.left, box.bottom - box.top + 1, SWP.SWP_NOMOVE | SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            Settle(50);
            _ = GetWindowRect(window, &box);
        }
        try
        {
            return Capture(window, box, file, part, seen, alpha);
        }
        finally
        {
            if (odd)
            {
                _ = SetWindowPos(window, HWND.NULL, 0, 0, box.right - box.left, box.bottom - box.top - 1, SWP.SWP_NOMOVE | SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            }
        }
    }

    /// <summary><see cref="Save"/>'s capture of <paramref name="window"/>, standing at <paramref name="box"/>.</summary>
    private static bool Capture(HWND window, RECT box, string file, RECT part, Action<byte[], int>? seen, bool alpha)
    {
        var width = box.right - box.left;
        var height = box.bottom - box.top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }
        var screen = GetDC(HWND.NULL);
        var memory = CreateCompatibleDC(screen);
        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        info.bmiHeader.biCompression = BI.BI_RGB;
        void* bits = null;
        var dib = CreateDIBSection(memory, &info, DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        var old = SelectObject(memory, dib);
        try
        {
            _ = PrintWindow(window, memory, PW_RENDERFULLCONTENT);
            _ = GdiFlush();
            // Of the extent: a popup's region in its window, which spans its display (§355).
            var extent = Extent(window);
            var (dx, dy) = (extent.left - box.left, extent.top - box.top);
            var (ew, eh) = (extent.right - extent.left, extent.bottom - extent.top);
            if (part.right <= part.left || part.bottom <= part.top)
            {
                part = new RECT { left = 0, top = 0, right = ew, bottom = eh };
            }
            part.left = Math.Clamp(part.left, 0, ew) + dx;
            part.right = Math.Clamp(part.right, part.left - dx, ew) + dx;
            part.top = Math.Clamp(part.top, 0, eh) + dy;
            part.bottom = Math.Clamp(part.bottom, part.top - dy, eh) + dy;
            // And of the window: a popup's box may hang past its display's edge.
            part.left = Math.Clamp(part.left, 0, width);
            part.right = Math.Clamp(part.right, part.left, width);
            part.top = Math.Clamp(part.top, 0, height);
            part.bottom = Math.Clamp(part.bottom, part.top, height);
            var w = part.right - part.left;
            var h = part.bottom - part.top;
            if (w == 0 || h == 0)
            {
                return false;
            }
            var pixels = new byte[w * h * 4];
            var source = (byte*)bits;
            var first = -1;
            var varied = false;
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var from = ((part.top + y) * width + part.left + x) * 4;
                    var to = (y * w + x) * 4;
                    pixels[to] = source[from];
                    pixels[to + 1] = source[from + 1];
                    pixels[to + 2] = source[from + 2];
                    pixels[to + 3] = alpha ? source[from + 3] : (byte)255;
                    var colour = source[from] | (source[from + 1] << 8) | (source[from + 2] << 16);
                    if (first < 0)
                    {
                        first = colour;
                    }
                    else if (colour != first)
                    {
                        varied = true;
                    }
                }
            }
            WritePng(pixels, w, h, Path.Combine(Offscreen.Pictures, file));
            seen?.Invoke(pixels, w);
            return varied;
        }
        finally
        {
            _ = SelectObject(memory, old);
            _ = DeleteObject(dib);
            _ = DeleteDC(memory);
            _ = ReleaseDC(HWND.NULL, screen);
        }
    }

    /// <summary>
    /// Several windows as they stand on the screen, in one picture: of each, the part
    /// <paramref name="parts"/> names (screen px — a popup's panel, without the room around it for
    /// its shadow), each over the ones before it, on a plain <paramref name="backdrop"/> (BGR) with
    /// <paramref name="margin"/> px around them all — a folder and the lists cascading out of it, as
    /// a person sees them. True when every window gave a picture with more than one colour in it.
    /// The windows of <paramref name="over"/> are laid over what is under them by their alpha, pure
    /// black as clear — an arranged folder spread over the display, its guide lines over the bars.
    /// </summary>
    internal static bool SaveTogether(IReadOnlyList<(HWND Window, RECT Part)> parts, string file, (byte B, byte G, byte R) backdrop, int margin = 16, IReadOnlyCollection<HWND>? over = null)
    {
        if (parts.Count == 0)
        {
            return false;
        }
        var frame = new RECT
        {
            left = parts.Min(part => part.Part.left) - margin,
            top = parts.Min(part => part.Part.top) - margin,
            right = parts.Max(part => part.Part.right) + margin,
            bottom = parts.Max(part => part.Part.bottom) + margin,
        };
        var (canvas, every) = Together(parts, frame, file, backdrop, over);
        WritePng(canvas, frame.right - frame.left, frame.bottom - frame.top, Path.Combine(Offscreen.Pictures, file));
        return every;
    }

    /// <summary>
    /// <see cref="SaveTogether"/>'s picture, of <paramref name="frame"/> (screen px) and kept rather
    /// than saved: BGRA, a row of the frame's width after another — what several pictures are made
    /// into one of (<see cref="SaveSideBySide"/>). True with it when every window gave a picture.
    /// </summary>
    internal static (byte[] Canvas, bool Every) Together(IReadOnlyList<(HWND Window, RECT Part)> parts, RECT frame, string file, (byte B, byte G, byte R) backdrop, IReadOnlyCollection<HWND>? over = null)
    {
        var left = frame.left;
        var top = frame.top;
        var width = frame.right - frame.left;
        var height = frame.bottom - frame.top;
        var canvas = new byte[width * height * 4];
        for (var at = 0; at < canvas.Length; at += 4)
        {
            canvas[at] = backdrop.B;
            canvas[at + 1] = backdrop.G;
            canvas[at + 2] = backdrop.R;
            canvas[at + 3] = 255;
        }
        var every = true;
        foreach (var (window, part) in parts)
        {
            var box = Extent(window);
            // The part in the window's own px (its extent's), which is what the capture is in.
            var cut = new RECT { left = part.left - box.left, top = part.top - box.top, right = part.right - box.left, bottom = part.bottom - box.top };
            byte[]? pixels = null;
            var w = 0;
            var laid = over?.Contains(window) == true;
            if (!Save(window, file, cut, (shown, shownWidth) => (pixels, w) = (shown, shownWidth), alpha: laid) || pixels is null || w == 0)
            {
                every = false;
                continue;
            }
            var h = pixels.Length / 4 / w;
            // Where the part stands in the picture: where it was cut from, as Save keeps the cut inside the window.
            var x0 = Math.Max(part.left, box.left) - left;
            var y0 = Math.Max(part.top, box.top) - top;
            for (var y = 0; y < h && y0 + y < height; y++)
            {
                if (!laid)
                {
                    Buffer.BlockCopy(pixels, y * w * 4, canvas, (((y0 + y) * width) + x0) * 4, Math.Min(w, width - x0) * 4);
                    continue;
                }
                // Premultiplied, over what is there. The capture gives the clear room as opaque black
                // more often than not: that is clear too (nothing the folder draws is pure black).
                for (var x = 0; x < w && x0 + x < width; x++)
                {
                    var from = ((y * w) + x) * 4;
                    var to = ((((y0 + y) * width) + x0 + x) * 4);
                    var keep = pixels[from] == 0 && pixels[from + 1] == 0 && pixels[from + 2] == 0 ? 255 : 255 - pixels[from + 3];
                    for (var channel = 0; channel < 3; channel++)
                    {
                        canvas[to + channel] = (byte)Math.Min(255, pixels[from + channel] + (canvas[to + channel] * keep / 255));
                    }
                }
            }
        }
        return (canvas, every);
    }

    /// <summary>
    /// Pictures made by <see cref="Together"/>, each <c>Width</c> by <c>Height</c>, side by side in one,
    /// <paramref name="gap"/> px of <paramref name="backdrop"/> between them: displays as they stand
    /// beside each other.
    /// </summary>
    internal static void SaveSideBySide(IReadOnlyList<(byte[] Canvas, int Width, int Height)> pictures, string file, (byte B, byte G, byte R) backdrop, int gap = 8)
    {
        var width = pictures.Sum(picture => picture.Width) + (gap * Math.Max(0, pictures.Count - 1));
        var height = pictures.Max(picture => picture.Height);
        var canvas = new byte[width * height * 4];
        for (var at = 0; at < canvas.Length; at += 4)
        {
            canvas[at] = backdrop.B;
            canvas[at + 1] = backdrop.G;
            canvas[at + 2] = backdrop.R;
            canvas[at + 3] = 255;
        }
        var x0 = 0;
        foreach (var (pixels, w, h) in pictures)
        {
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(pixels, y * w * 4, canvas, ((y * width) + x0) * 4, w * 4);
            }
            x0 += w + gap;
        }
        WritePng(canvas, width, height, Path.Combine(Offscreen.Pictures, file));
    }

    internal static void WritePng(byte[] pixels, int width, int height, string path)
    {
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        try
        {
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppBGRA;
            fixed (byte* data = pixels)
            {
                ComUtil.Check(wic->CreateBitmapFromMemory((uint)width, (uint)height, &format, (uint)(width * 4), (uint)pixels.Length, data, &bitmap));
            }
            Offscreen.Save(bitmap, path);
        }
        finally
        {
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
        }
    }
}
