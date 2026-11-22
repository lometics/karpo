// tablet: Windows 11's "Optimize taskbar for touch interactions when this device is used as a
// tablet" (Taskbar behaviors; Explorer\Advanced ExpandableTaskbar, on unless it says 0). On a touch
// device folded or undocked into a tablet (SM_CONVERTIBLESLATEMODE is 0 in slate mode), the bar
// stands at touch size — taller, so every button and widget is a bigger target for a finger, as
// Windows' expanded taskbar is —, and back as a laptop it is as the person set it.
//
// Only how the bar is laid out follows the posture: the person's height is never changed, and
// never saved (Config.WithHeight is a copy the layout reads). A PC without touch never counts as a
// tablet, whatever its slate-mode metric says: some desktops report slate mode for want of the
// sensor that would say otherwise.

using Karpo.Core;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal static class TabletPosture
{
    /// <summary>The bar's height in tablet posture, in DIPs, at the least: room for a fingertip on every button.</summary>
    internal const int TouchHeightDip = 64;

    // GetSystemMetrics indexes TerraFX leaves out.
    private const int MaximumTouches = 95;
    private const int ConvertibleSlateMode = 0x2003;

    /// <summary>
    /// Whether the bar stands at touch size: Windows' setting not off (<paramref name="expandable"/>,
    /// null where it was never set), a screen that takes touch (<paramref name="touchPoints"/>), and
    /// the device in slate mode (<paramref name="slateMode"/> 0).
    /// </summary>
    internal static bool Touch(uint? expandable, int touchPoints, int slateMode) =>
        expandable is not 0u && touchPoints > 0 && slateMode == 0;

    /// <summary>The height a bar set to <paramref name="configured"/> DIPs is laid out at: at least <see cref="TouchHeightDip"/> at touch size, else as set.</summary>
    internal static int HeightFor(int configured, bool touch) =>
        touch ? Math.Max(configured > 0 ? configured : 48, TouchHeightDip) : configured;

    /// <summary>The config a bar is laid out with: <paramref name="config"/> itself, or a copy at touch height (<see cref="HeightFor"/>).</summary>
    internal static Config Shown(Config config, bool touch) =>
        HeightFor(config.Height, touch) is var height && height != config.Height ? config.WithHeight(height) : config;

    /// <summary>Whether the bar stands at touch size now, as Windows says.</summary>
    internal static bool Read() =>
        Touch(LivePeek.ReadDword(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ExpandableTaskbar"), GetSystemMetrics(MaximumTouches), GetSystemMetrics(ConvertibleSlateMode));
}
