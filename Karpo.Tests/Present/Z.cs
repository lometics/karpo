// The z-order as Windows has it now, for a test that asks where a window stands (§359: where each of
// Karpo's windows stands is the plan's, ZPlan; this reads what the screen shows of it).

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Present;

internal static class Z
{
    /// <summary>Whether <paramref name="window"/> stands in front of <paramref name="under"/> in the z-order now.</summary>
    public static bool Above(HWND window, HWND under)
    {
        if (window == HWND.NULL || under == HWND.NULL || window == under)
        {
            return false;
        }
        for (var at = GetWindow(under, GW_HWNDPREV); at != HWND.NULL; at = GetWindow(at, GW_HWNDPREV))
        {
            if (at == window)
            {
                return true;
            }
        }
        return false;
    }
}
