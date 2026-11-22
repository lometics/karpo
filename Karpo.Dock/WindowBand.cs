// The z-band a window is in (ZBID_*): apps' windows are in band 1, the shell's own views (Task view,
// Alt+Tab, Start, the lock screen) in higher ones, over every app's whatever its z-order.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal static unsafe class WindowBand
{
    private static readonly delegate* unmanaged<HWND, uint*, BOOL> Read = Find();

    /// <summary>
    /// The z-band of <paramref name="hwnd"/>, by user32's GetWindowBand, which is not documented:
    /// looked up once, and null wherever it is missing or fails.
    /// </summary>
    public static uint? Of(HWND hwnd)
    {
        uint band = 0;
        return Read != null && Read(hwnd, &band) != BOOL.FALSE ? band : null;
    }

    private static delegate* unmanaged<HWND, uint*, BOOL> Find()
    {
        try
        {
            return NativeLibrary.TryLoad("user32.dll", out var user32) && NativeLibrary.TryGetExport(user32, "GetWindowBand", out var export)
                ? (delegate* unmanaged<HWND, uint*, BOOL>)export
                : null;
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            return null;
        }
    }
}
