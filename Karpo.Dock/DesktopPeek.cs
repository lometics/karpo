// peek: the desktop shown through every window while the pointer rests on the Show desktop corner,
// where Windows' own "Use Peek to preview the desktop when you move your mouse to the Show desktop
// button" is on (Explorer\Advanced DisablePreviewDesktop 0 — off where it is missing, as Windows 11
// leaves it) and Peek itself is (DWM EnableAeroPeek, on unless it says 0). The rest after which it
// shows is Windows' own DesktopLivePreviewHoverTime. Shown and ended by LivePeek.

namespace Karpo.Dock;

internal static class DesktopPeek
{
    /// <summary>The rest before the desktop shows where DesktopLivePreviewHoverTime is not set, in ms.</summary>
    internal const uint DefaultDelayMs = 500;

    /// <summary>Whether resting on Show desktop peeks at the desktop, by Windows' two settings as read (null where one is not set).</summary>
    internal static bool Wanted(uint? disablePreviewDesktop, uint? enableAeroPeek) =>
        disablePreviewDesktop == 0u && enableAeroPeek is not 0u;

    /// <summary>The rest before the desktop shows, in ms: Windows' own, within ten seconds, or <see cref="DefaultDelayMs"/>.</summary>
    internal static uint DelayOf(uint? hoverTime) => hoverTime is { } ms ? Math.Min(ms, 10_000u) : DefaultDelayMs;
}
