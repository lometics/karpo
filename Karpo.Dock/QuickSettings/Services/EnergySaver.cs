// Whether energy saver is on, as Windows 11 24H2 and later say it.
//
// Energy saver replaced battery saver in 24H2, and the old way of asking stopped telling the truth:
// GetSystemPowerStatus's SystemStatusFlag (and GUID_POWER_SAVING_STATUS, its notification) says 1
// only in energy saver's high-savings mode, which a battery running low turns on. Turned on by hand,
// or on the wall, energy saver is in its standard mode, and SystemStatusFlag stays 0 — the leaf on
// Karpo's battery, its status icon and its switch all said "off" while Windows' own taskbar showed
// it on. And nothing reliably told the bar to look again: the power-status broadcast it listened for
// is about the battery and the charger, and the dock registered for no power setting at all.
//
// GUID_ENERGY_SAVER_STATUS says it: off, standard or high savings. The dock's window registers for it
// (RegisterPowerSettingNotification), so Windows sends WM_POWERBROADCAST / PBT_POWERSETTINGCHANGE with
// the status when it changes and once at registration. The dock keeps the last status here before
// handing the broadcast on to the widgets, which then read it again: the battery's glyph, the status
// icons, the switch (bar, card, list line, the battery's menu) and the battery flyout all read
// energy saver through PowerNow, which takes this status over SystemStatusFlag wherever Windows has
// given one. Where Windows never does (before 24H2), SystemStatusFlag is all there is, and is right.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>The last energy saver status Windows gave (see the top of the file).</summary>
internal sealed class EnergySaver
{
    /// <summary>WM_POWERBROADCAST's wParam for a power setting that changed.</summary>
    public const uint PbtPowerSettingChange = 0x8013;

    /// <summary>ENERGY_SAVER_STATUS: off, standard, high savings.</summary>
    public const uint Off = 0;

    public const uint Standard = 1;

    public const uint HighSavings = 2;

    /// <summary>The dock's: what Windows said to its window.</summary>
    public static EnergySaver Shared { get; } = new();

    // -1 until Windows has said.
    private int status = -1;

    /// <summary>On (standard or high savings), off, or null where Windows has not said.</summary>
    public bool? On => Volatile.Read(ref status) switch
    {
        < 0 => null,
        0 => false,
        _ => true,
    };

    /// <summary>Windows said <paramref name="value"/>: true when that changed what <see cref="On"/> says.</summary>
    public bool Heard(uint value)
    {
        var was = On;
        Volatile.Write(ref status, (int)Math.Min(value, HighSavings));
        return was != On;
    }

    /// <summary>
    /// Whether energy saver is on, from the power status's flag and what Windows said of energy
    /// saver itself (<paramref name="status"/>, null where it never did): the latter wins. Pure.
    /// </summary>
    public static bool Saving(bool statusFlag, bool? status) => status ?? statusFlag;

    /// <summary>
    /// The energy saver status a power setting notification carries; null for any other setting, or
    /// data too short to hold one.
    /// </summary>
    public static unsafe uint? Read(POWERBROADCAST_SETTING* setting)
    {
        if (setting == null || setting->PowerSetting != GUID.GUID_ENERGY_SAVER_STATUS || setting->DataLength < sizeof(uint))
        {
            return null;
        }
        return *(uint*)&setting->Data;
    }

    /// <summary>A power setting notification (a WM_POWERBROADCAST's lParam, or a callback's setting) heard: true when energy saver moved.</summary>
    public unsafe bool Heard(POWERBROADCAST_SETTING* setting) => Read(setting) is { } value && Heard(value);

    /// <summary>
    /// <paramref name="window"/> is told of energy saver from now on (and once now); null where this
    /// Windows has no energy saver status to tell.
    /// </summary>
    public static nint? Follow(HWND window) => Register(window, GUID.GUID_ENERGY_SAVER_STATUS);

    /// <summary>
    /// <paramref name="window"/> is told of the power setting <paramref name="setting"/> from now on
    /// (and once now); null where this Windows has no such setting: energy saver's here, the built-in
    /// screen's brightness for Brightness.Follow. Undone by <see cref="Unfollow"/>.
    /// </summary>
    internal static unsafe nint? Register(HWND window, Guid setting)
    {
        var id = setting;
        // DEVICE_NOTIFY_WINDOW_HANDLE: as WM_POWERBROADCAST, on the window's own thread.
        var registration = RegisterPowerSettingNotification((HANDLE)window.Value, &id, 0);
        return registration == null ? null : (nint)registration;
    }

    /// <summary>Stops what <see cref="Follow"/> or <see cref="Register"/> started.</summary>
    public static unsafe void Unfollow(nint registration) => _ = UnregisterPowerSettingNotification((void*)registration);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern unsafe void* RegisterPowerSettingNotification(HANDLE recipient, Guid* setting, uint flags);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern unsafe int UnregisterPowerSettingNotification(void* registration);
}
