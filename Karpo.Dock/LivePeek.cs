// Peek: the window a preview card stands for, shown in front of everything
// with the rest faded, as the taskbar does while the pointer rests on a
// thumbnail.
//
// DWM does it through an export it only names by ordinal, #113
// (DwmpActivateLivePreview). The shape used here is ManagedShell's
// (github.com/cairoshell/ManagedShell, Apache-2.0: NativeMethods.DwmApi.cs and
// WindowHelper.PeekWindow): (peek on, the window to show, the calling window,
// which stays on top, AeroPeekType.Window = 3, and a reserved pointer that
// Windows 8.1 added). The type matters: 1 is the desktop peek of the "Show
// desktop" corner, not a window's.
//
// The calling window is the one thing that argument leaves alone, and it must be
// the bar the card belongs to. This passed the preview popup instead, so the bar
// faded away with every other window while the pointer rested on a card —
// which is not what Windows' own taskbar does.
//
// One window is not enough, though: a bar is often several — each place along
// an edge and each stacked row is a window of its own, and every other display
// has its bars — and the rest still faded with everything else (the person,
// 2026-10-12: "it also hides the karpo taskbar"). What keeps Explorer's taskbars,
// every one on every display, and its thumbnail list in sight through a peek is
// the documented DWMWA_EXCLUDED_FROM_PEEK on each of them. Karpo's bars and its
// window list are marked so when they are made (StaysThrough); the argument is
// still passed, as before.
//
// Windows' own "Enable Peek" (HKCU\Software\Microsoft\Windows\DWM
// EnableAeroPeek, on when missing) is respected: off, nothing fades. Where Peek
// is off or the call fails, a 3-DIP accent outline traces the window instead,
// so the pointer's window is still easy to tell: four thin click-through,
// never-activated, topmost layered windows along its edges. Four strips rather
// than one window the window's size: a layered window keeps a bitmap as large
// as itself, and an outline is almost all hole.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

internal static unsafe class LivePeek
{
    private const string PeekKey = @"Software\Microsoft\Windows\DWM";
    private const string HoverKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";

    /// <summary>ManagedShell's AeroPeekType.Window.</summary>
    private const uint PeekWindow = 3;

    /// <summary>Explorer's own delay when ThumbnailLivePreviewHoverTime is not set.</summary>
    private const uint DefaultDelayMs = 500;

    private const string OutlineClass = "karpo.peek";

    [DllImport("dwmapi.dll", EntryPoint = "#113")]
    private static extern int DwmpActivateLivePreview(uint peekOn, nint peekWindow, nint topmostWindow, uint peekType, nint reserved);

    private static bool peeking;
    private static bool classRegistered;

    // The outline's four strips: top, bottom, left, right.
    private static readonly HWND[] strips = new HWND[4];
    private static bool outlined;

    /// <summary>
    /// How long the pointer rests on a card before its window is peeked, in ms:
    /// Explorer's ThumbnailLivePreviewHoverTime, as the taskbar reads it.
    /// </summary>
    public static uint DelayMs => ReadDword(HoverKey, "ThumbnailLivePreviewHoverTime") is { } ms ? Math.Min(ms, 10_000) : DefaultDelayMs;

    /// <summary>Windows' "Enable Peek": on unless it says 0.</summary>
    public static bool Enabled => ReadDword(PeekKey, "EnableAeroPeek") is not 0u;

    /// <summary>peek: whether resting on Show desktop peeks at the desktop, by Windows' own settings (DesktopPeek).</summary>
    public static bool DesktopEnabled =>
        DesktopPeek.Wanted(ReadDword(HoverKey, "DisablePreviewDesktop"), ReadDword(PeekKey, "EnableAeroPeek"));

    /// <summary>peek: how long the pointer rests on Show desktop before the desktop shows, in ms (DesktopPeek.DelayOf).</summary>
    public static uint DesktopDelayMs => DesktopPeek.DelayOf(ReadDword(HoverKey, "DesktopLivePreviewHoverTime"));

    // peek: the peek showing is the desktop's (ShowDesktop), not a window's.
    private static bool desktop;

    /// <summary>
    /// peek: <paramref name="window"/> stays in sight through every peek, a card's window or the
    /// desktop, as Windows' own taskbars and thumbnail list do (DWMWA_EXCLUDED_FROM_PEEK). Asked of
    /// every bar and of the window list as they are made. Whether DWM took it.
    /// </summary>
    public static bool StaysThrough(HWND window)
    {
        BOOL on = BOOL.TRUE;
        return DwmSetWindowAttribute(window, (uint)DWMWINDOWATTRIBUTE.DWMWA_EXCLUDED_FROM_PEEK, &on, (uint)sizeof(BOOL)).SUCCEEDED;
    }

    /// <summary>
    /// peek: the desktop through every window, <paramref name="bar"/> — the bar whose Show desktop
    /// the pointer rests on — kept above it: a window peek with no window to show, which fades
    /// everything but the calling window, as Windows 11's own corner does. Nothing where DWM refuses.
    /// </summary>
    public static void ShowDesktop(HWND bar)
    {
        End();
        try
        {
            if (DwmpActivateLivePreview(1, 0, IsWindow(bar) ? (nint)bar : 0, PeekWindow, 0) >= 0)
            {
                peeking = true;
                desktop = true;
            }
        }
        catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
        {
        }
    }

    /// <summary>peek: ends the desktop peek, and only that — a window's peek from its card stays.</summary>
    public static void EndDesktop()
    {
        if (desktop)
        {
            End();
        }
    }

    /// <summary>
    /// Shows <paramref name="window"/> in front of everything else, with <paramref name="bar"/>
    /// — the dock window the card belongs to — kept above the peek, so the bar does not fade with
    /// the rest; or, where Peek is off or refused, the outline in <paramref name="accent"/> drawn
    /// just under <paramref name="popup"/>. Ends whatever an earlier call showed.
    /// </summary>
    /// <param name="bar">
    /// DWM's third argument: the calling window, which stays on top. Windows' taskbar passes its
    /// own bar here, which is why it survives a peek; passing the popup instead — as this did —
    /// leaves the popup up and fades the bar away with everything else. With several displays it
    /// has to be the bar the pointer is on, not just any of them.
    /// </param>
    public static void Show(HWND window, HWND popup, HWND bar, Color accent)
    {
        End();
        if (!IsWindow(window))
        {
            return;
        }
        if (Enabled)
        {
            try
            {
                var onTop = IsWindow(bar) ? bar : popup;
                var result = DwmpActivateLivePreview(1, (nint)window, (nint)onTop, PeekWindow, 0);
                if (result >= 0)
                {
                    peeking = true;
                    return;
                }
                Console.Error.WriteLine($"Karpo: Peek refused (0x{result:X8}); outlining the window instead");
            }
            catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
            {
                Console.Error.WriteLine("Karpo: Peek is not available here; outlining the window instead");
            }
        }
        Outline(window, popup, accent);
    }

    /// <summary>Ends the peek or the outline, whichever is showing.</summary>
    public static void End()
    {
        desktop = false;
        if (peeking)
        {
            peeking = false;
            try
            {
                _ = DwmpActivateLivePreview(0, 0, 0, PeekWindow, 0);
            }
            catch (Exception error) when (error is EntryPointNotFoundException or DllNotFoundException)
            {
            }
        }
        if (outlined)
        {
            outlined = false;
            foreach (var strip in strips)
            {
                if (strip != HWND.NULL)
                {
                    Surfaces.Away(strip);
                }
            }
        }
    }

    /// <summary>
    /// The outline around <paramref name="window"/>'s frame as DWM draws it
    /// (DWMWA_EXTENDED_FRAME_BOUNDS, without the invisible resize borders),
    /// just under <paramref name="popup"/>. Rounded like a Windows 11 window,
    /// square when the window is maximised, as Windows draws it then.
    /// </summary>
    private static void Outline(HWND window, HWND popup, Color accent)
    {
        if (IsIconic(window))
        {
            // Nothing of it on screen to trace.
            return;
        }
        RECT frame;
        if (DwmGetWindowAttribute(window, (uint)DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frame, (uint)sizeof(RECT)).FAILED
            && !GetWindowRect(window, &frame))
        {
            return;
        }
        int width = frame.right - frame.left;
        int height = frame.bottom - frame.top;
        if (width <= 0 || height <= 0 || width > 32_768 || height > 32_768)
        {
            return;
        }
        uint dpiX = 96;
        uint dpiY = 96;
        var monitor = MonitorFromWindow(window, (uint)MONITOR.MONITOR_DEFAULTTONEAREST);
        if (GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY).FAILED)
        {
            dpiX = 96;
        }
        var scale = dpiX / 96f;
        var thickness = MathF.Max(1, MathF.Round(OutlineShape.ThicknessDip * scale));
        var radius = IsZoomed(window) ? 0 : MathF.Round(OutlineShape.RadiusDip * scale);
        if (accent.A == 0)
        {
            // Windows 11's default blue, for a Look without an accent.
            accent = Color.FromArgb(255, 0x00, 0x78, 0xD4);
        }
        if (!EnsureStrips())
        {
            return;
        }
        var band = OutlineShape.Band(thickness, radius, width, height);
        var screen = GetDC(HWND.NULL);
        try
        {
            // Top and bottom run the whole width, corners included; left and
            // right fill in between.
            var pieces = new (int Left, int Top, int Width, int Height)[]
            {
                (0, 0, width, band),
                (0, height - band, width, band),
                (0, band, band, height - 2 * band),
                (width - band, band, band, height - 2 * band),
            };
            var below = popup;
            for (var index = 0; index < pieces.Length; index++)
            {
                var (left, top, pieceWidth, pieceHeight) = pieces[index];
                var strip = strips[index];
                if (pieceWidth <= 0 || pieceHeight <= 0)
                {
                    Surfaces.Away(strip);
                    continue;
                }
                if (Paint(strip, screen, frame.left + left, frame.top + top, left, top, pieceWidth, pieceHeight, width, height, thickness, radius, accent))
                {
                    // Topmost, and just under the popup, so the popup stays
                    // readable where the two overlap.
                    Surfaces.Beneath(strip, below);
                    below = strip;
                    outlined = true;
                }
            }
        }
        finally
        {
            _ = ReleaseDC(HWND.NULL, screen);
        }
    }

    /// <summary>One strip's pixels, handed to its layered window.</summary>
    private static bool Paint(HWND strip, HDC screen, int x, int y, int left, int top, int width, int height, int fullWidth, int fullHeight, float thickness, float radius, Color accent)
    {
        var info = new BITMAPINFO
        {
            bmiHeader = new BITMAPINFOHEADER
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = width,
                // Top-down rows.
                biHeight = -height,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = (uint)BI.BI_RGB,
            },
        };
        void* bits = null;
        var bitmap = CreateDIBSection(screen, &info, (uint)DIB_RGB_COLORS, &bits, HANDLE.NULL, 0);
        if (bitmap == HBITMAP.NULL || bits == null)
        {
            return false;
        }
        var memory = CreateCompatibleDC(screen);
        var old = SelectObject(memory, (HGDIOBJ)bitmap.Value);
        try
        {
            OutlineShape.Fill(new Span<uint>(bits, width * height), left, top, width, height, fullWidth, fullHeight, thickness, radius, accent.R, accent.G, accent.B);
            var position = new POINT { x = x, y = y };
            var size = new SIZE { cx = width, cy = height };
            var source = new POINT();
            var blend = new BLENDFUNCTION { BlendOp = AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = AC_SRC_ALPHA };
            return UpdateLayeredWindow(strip, screen, &position, &size, memory, &source, default, &blend, (uint)ULW_ALPHA);
        }
        finally
        {
            _ = SelectObject(memory, old);
            _ = DeleteDC(memory);
            _ = DeleteObject((HGDIOBJ)bitmap.Value);
        }
    }

    private static bool EnsureStrips()
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        if (!classRegistered)
        {
            fixed (char* name = OutlineClass)
            {
                var wc = new WNDCLASSW
                {
                    lpfnWndProc = &Procedure,
                    hInstance = instance,
                    lpszClassName = name,
                };
                // A second registration (a dock started twice in one
                // process) finds the class there already.
                classRegistered = RegisterClassW(&wc) != 0 || GetLastError() == ERROR.ERROR_CLASS_ALREADY_EXISTS;
            }
            if (!classRegistered)
            {
                return false;
            }
        }
        for (var index = 0; index < strips.Length; index++)
        {
            if (strips[index] != HWND.NULL && IsWindow(strips[index]))
            {
                continue;
            }
            fixed (char* name = OutlineClass)
            fixed (char* title = "Karpo peek") // not-ui: a window's title
            {
                strips[index] = CreateWindowExW(
                    // Click-through (transparent and layered), never activated,
                    // no taskbar button, above normal windows.
                    WS.WS_EX_LAYERED | WS.WS_EX_TRANSPARENT | WS.WS_EX_NOACTIVATE | WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST,
                    name,
                    title,
                    WS.WS_POPUP,
                    0,
                    0,
                    1,
                    1,
                    HWND.NULL,
                    HMENU.NULL,
                    instance,
                    null);
            }
            if (strips[index] == HWND.NULL)
            {
                return false;
            }
        }
        return true;
    }

    [UnmanagedCallersOnly]
    private static LRESULT Procedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        msg switch
        {
            // Never in the way of the pointer, even where it is drawn.
            WM.WM_NCHITTEST => (LRESULT)HTTRANSPARENT,
            WM.WM_MOUSEACTIVATE => (LRESULT)MA.MA_NOACTIVATE,
            _ => Closing.Default(hwnd, msg, wParam, lParam),
        };

    internal static uint? ReadDword(string subkey, string value)
    {
        uint data = 0;
        var size = (uint)sizeof(uint);
        fixed (char* key = subkey)
        fixed (char* name = value)
        {
            return RegGetValueW(HKEY.HKEY_CURRENT_USER, key, name, (uint)RRF.RRF_RT_REG_DWORD, null, &data, &size) == ERROR.ERROR_SUCCESS
                ? data
                : null;
        }
    }
}

/// <summary>
/// The peek outline's pixels: a rounded rectangle's rim, antialiased, in
/// premultiplied BGRA. Pure, so it can be looked at without a screen.
/// </summary>
public static class OutlineShape
{
    /// <summary>How thick the rim is, in DIPs.</summary>
    public const float ThicknessDip = 3;

    /// <summary>A Windows 11 window's corner radius, in DIPs.</summary>
    public const float RadiusDip = 8;

    /// <summary>
    /// How deep the top and bottom strips reach, in px: the rim and the part of
    /// the corners that curves inwards, and a pixel of antialiasing; never more
    /// than half the rectangle.
    /// </summary>
    public static int Band(float thickness, float radius, int width, int height) =>
        Math.Max(0, Math.Min((int)MathF.Ceiling(MathF.Max(thickness, radius) + 1), Math.Min(width, height) / 2));

    /// <summary>
    /// How much of pixel (<paramref name="x"/>, <paramref name="y"/>) the rim
    /// covers, 0 to 1, for a <paramref name="width"/> × <paramref name="height"/>
    /// rectangle whose outer corners have <paramref name="radius"/>.
    /// </summary>
    public static float Coverage(int x, int y, int width, int height, float thickness, float radius)
    {
        var halfX = width / 2f;
        var halfY = height / 2f;
        var outerRadius = Math.Clamp(radius, 0, MathF.Min(halfX, halfY));
        var innerRadius = MathF.Max(outerRadius - thickness, 0);
        // The pixel's centre, from the rectangle's centre.
        var px = MathF.Abs(x + 0.5f - halfX);
        var py = MathF.Abs(y + 0.5f - halfY);
        var outside = Distance(px, py, halfX, halfY, outerRadius);
        var inside = Distance(px, py, halfX - thickness, halfY - thickness, innerRadius);
        var covered = Math.Clamp(0.5f - outside, 0, 1) - Math.Clamp(0.5f - inside, 0, 1);
        return Math.Clamp(covered, 0, 1);
    }

    /// <summary>
    /// Fills <paramref name="pixels"/>, a <paramref name="width"/> × <paramref name="height"/>
    /// piece of the rim whose top-left is (<paramref name="left"/>, <paramref name="top"/>)
    /// in the whole outline, with the colour at the rim's coverage.
    /// </summary>
    public static void Fill(Span<uint> pixels, int left, int top, int width, int height, int fullWidth, int fullHeight, float thickness, float radius, byte r, byte g, byte b)
    {
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                var coverage = Coverage(left + column, top + row, fullWidth, fullHeight, thickness, radius);
                var alpha = (uint)MathF.Round(coverage * 255, MidpointRounding.AwayFromZero);
                // Premultiplied, as UpdateLayeredWindow's per-pixel alpha wants it.
                var red = (r * alpha + 127) / 255;
                var green = (g * alpha + 127) / 255;
                var blue = (b * alpha + 127) / 255;
                pixels[row * width + column] = (alpha << 24) | (red << 16) | (green << 8) | blue;
            }
        }
    }

    /// <summary>
    /// Signed distance from a point (folded into the first quadrant around the
    /// centre) to a rounded rectangle with half-sizes <paramref name="halfX"/>,
    /// <paramref name="halfY"/>: negative inside.
    /// </summary>
    private static float Distance(float px, float py, float halfX, float halfY, float radius)
    {
        if (halfX <= 0 || halfY <= 0)
        {
            // Nothing left inside: everything is outside it.
            return float.PositiveInfinity;
        }
        radius = Math.Clamp(radius, 0, MathF.Min(halfX, halfY));
        var qx = px - (halfX - radius);
        var qy = py - (halfY - radius);
        var outside = MathF.Sqrt(MathF.Max(qx, 0) * MathF.Max(qx, 0) + MathF.Max(qy, 0) * MathF.Max(qy, 0));
        var inside = MathF.Min(MathF.Max(qx, qy), 0);
        return outside + inside - radius;
    }
}
