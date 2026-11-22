// What of Windows' own taskbar settings Karpo's faces follow, read once and again as Windows says a setting changed
// (WM_SETTINGCHANGE, which Windows' Settings sends as each is switched; Dock.cs): the clock's seconds
// (ShowSecondsInSystemClock), which the time-and-date face shows (WindowsClock, DateTimeFaceDrawing), and §485 the
// battery's percentage (IsBatteryPercentageEnabled, Settings › System › Power & battery › "Battery percentage"), which
// Windows' own quick settings button shows beside its battery (WindowsBatteryPercent, WindowsFaces). Read, never written.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>Windows' taskbar settings as last read (<see cref="WindowsTaskbar.Read"/>); see the top of the file.</summary>
internal static class WindowsTaskbarFollowed
{
    private static WindowsTaskbar? read;

    /// <summary>The settings as last read; read at first ask.</summary>
    public static WindowsTaskbar Now => read ??= WindowsTaskbar.Read();

    /// <summary>
    /// Reads them again (the dock asks on WM_SETTINGCHANGE): true where what a face follows — the seconds, the battery's
    /// percentage — changed since they were last read, when the faces need building again; false at the first read.
    /// </summary>
    public static bool Changed()
    {
        var now = WindowsTaskbar.Read();
        var had = read;
        read = now;
        return had is not null && Differ(had, now);
    }

    /// <summary>Whether <paramref name="then"/> and <paramref name="now"/> differ in what a face follows. Pure.</summary>
    internal static bool Differ(WindowsTaskbar then, WindowsTaskbar now) =>
        then.Seconds != now.Seconds || then.BatteryPercent != now.BatteryPercent;
}

/// <summary>§485: whether Windows shows the battery's percentage beside its icon, which Windows' own quick settings button follows.</summary>
internal static class WindowsBatteryPercent
{
    /// <summary>Tests only, on their thread: what Windows is taken to say, instead of this PC's setting.</summary>
    [ThreadStatic]
    internal static bool? Stand;

    /// <summary>IsBatteryPercentageEnabled as last read (<see cref="WindowsTaskbarFollowed"/>).</summary>
    public static bool On => Stand ?? WindowsTaskbarFollowed.Now.BatteryPercent;
}
