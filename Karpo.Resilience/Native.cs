// Small reads of the desktop the runner needs besides the probe.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Resilience;

internal static unsafe class Native
{
    /// <summary>Every visible top-level window of another app's (neither Karpo's nor the harness's own), as a scenario starts.</summary>
    public static List<WindowSeen> OthersWindows(int? dock)
    {
        var displays = Probe.Displays();
        var self = Environment.ProcessId;
        var found = new List<WindowSeen>();
        foreach (var window in Probe.TopWindows())
        {
            if (Probe.See(window, displays) is { Visible: true, Cloaked: false } seen
                && seen.Process != dock && seen.Process != self
                && !seen.Class.StartsWith("resilience.", StringComparison.Ordinal) && !seen.Rect.Empty)
            {
                found.Add(seen);
            }
        }
        return found;
    }

    public static bool IsWindowLive(nint window) => IsWindow((HWND)window);
}
