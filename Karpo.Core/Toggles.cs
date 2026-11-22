// The toggles widget's members: the switches a row of toggle icons can hold (docs\widgets.md §9,
// "Toggles").
//
// The person, 2026-10-09: "one list line holding several toggle icons (hotspot, do not disturb,
// Wi-Fi, Bluetooth, airplane, night light, energy saver …). The user picks which toggles and their
// order. Icon-only row (wraps to more rows if many). No details/arrow: pure on/off." So a toggle is
// here only where Karpo has a switch that is on or off and can flip it: every one-button widget whose
// click switches it (Karpo.Dock\QuickSettings\Services\Switches.cs) — the radios (Wi-Fi and
// Bluetooth, through Windows.Devices.Radios as their lines switch them) and the sound's and the
// microphone's mute among them, each a toggle widget of its own since 2026-10-11. Live captions,
// accessibility and project are not: Windows tells no app whether they are on, and their click only
// opens something. Nearby sharing's switch reads and switches since 2026-10-11 (§115), and is a
// toggle since 2026-10-16, the quick settings preset's. Keep awake is one since 2026-10-20 (§269), a
// click on it keeping the PC awake until it is clicked again. Dark mode is one since 2026-10-29 (§403),
// Windows' own mode, dark or light. Game mode, color filters, mono audio, the magnifier, the narrator
// and sticky keys are since 2026-11-03 ("game mode on and off should go into the toggle widget, see if
// you forgot another toggle as well"): every widget whose click flips it (WidgetInfo.Flips) is a toggle
// here too, through its own switch. Location and mobile data are not: Karpo has no switch for them yet.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>One switch a toggles row can hold. A word this build does not know reads as a value outside the enum and is dropped.</summary>
[JsonConverter(typeof(TolerantEnumConverter<Toggle>))]
public enum Toggle
{
    [JsonStringEnumMemberName("wifi")] Wifi,
    [JsonStringEnumMemberName("bluetooth")] Bluetooth,
    [JsonStringEnumMemberName("airplaneMode")] AirplaneMode,
    [JsonStringEnumMemberName("mobileHotspot")] MobileHotspot,
    [JsonStringEnumMemberName("energySaver")] EnergySaver,
    [JsonStringEnumMemberName("nightLight")] NightLight,
    [JsonStringEnumMemberName("doNotDisturb")] DoNotDisturb,
    [JsonStringEnumMemberName("vpn")] Vpn,
    [JsonStringEnumMemberName("rotationLock")] RotationLock,
    [JsonStringEnumMemberName("spatialSound")] SpatialSound,
    [JsonStringEnumMemberName("sound")] Sound,
    [JsonStringEnumMemberName("microphone")] Microphone,
    [JsonStringEnumMemberName("nearbyShare")] NearbyShare,
    [JsonStringEnumMemberName("keepAwake")] KeepAwake,
    [JsonStringEnumMemberName("darkMode")] DarkMode,
    [JsonStringEnumMemberName("gameMode")] GameMode,
    [JsonStringEnumMemberName("colorFilters")] ColorFilters,
    [JsonStringEnumMemberName("monoAudio")] MonoAudio,
    [JsonStringEnumMemberName("magnifier")] Magnifier,
    [JsonStringEnumMemberName("narrator")] Narrator,
    [JsonStringEnumMemberName("stickyKeys")] StickyKeys,
}

/// <summary>What a toggle is: its glyph and name, and the one-button widget's switch behind it.</summary>
/// <param name="Switch">The one-button widget's switch it flips: every toggle's since the radios became widgets (2026-10-11).</param>
public sealed record ToggleInfo(Toggle Toggle, QuickSwitch? Switch, string Glyph, string NameKey)
{
    /// <summary>What its tooltip and Settings call it.</summary>
    public string Name => Strings.Text(NameKey);
}

/// <summary>Every toggle, and the toggles widget's options.</summary>
public static class Toggles
{
    /// <summary>Every toggle, in <see cref="Toggle"/>'s order: the order Settings offers them in.</summary>
    public static readonly ToggleInfo[] All =
    [
        Of(Toggle.Wifi, QuickSwitch.WifiRadio, "dock.quick.wifi.title"),
        Of(Toggle.Bluetooth, QuickSwitch.BluetoothRadio, "dock.quick.bluetooth.title"),
        Of(Toggle.AirplaneMode, QuickSwitch.AirplaneMode),
        Of(Toggle.MobileHotspot, QuickSwitch.MobileHotspot),
        Of(Toggle.EnergySaver, QuickSwitch.EnergySaver),
        Of(Toggle.NightLight, QuickSwitch.NightLight),
        Of(Toggle.DoNotDisturb, QuickSwitch.DoNotDisturb),
        Of(Toggle.Vpn, QuickSwitch.Vpn),
        Of(Toggle.RotationLock, QuickSwitch.RotationLock),
        Of(Toggle.SpatialSound, QuickSwitch.SpatialSound),
        Of(Toggle.Sound, QuickSwitch.Sound, "dock.quick.sound.title"),
        Of(Toggle.Microphone, QuickSwitch.Microphone, "dock.quick.sound.microphone"),
        Of(Toggle.NearbyShare, QuickSwitch.NearbyShare),
        Of(Toggle.KeepAwake, QuickSwitch.KeepAwake),
        Of(Toggle.DarkMode, QuickSwitch.DarkMode),
        Of(Toggle.GameMode, QuickSwitch.GameMode),
        Of(Toggle.ColorFilters, QuickSwitch.ColorFilters),
        Of(Toggle.MonoAudio, QuickSwitch.MonoAudio),
        Of(Toggle.Magnifier, QuickSwitch.Magnifier),
        Of(Toggle.Narrator, QuickSwitch.Narrator),
        Of(Toggle.StickyKeys, QuickSwitch.StickyKeys),
    ];

    /// <summary>
    /// A toggle: its one-button widget's switch, glyph and name — or <paramref name="nameKey"/>, the
    /// quick settings' own word for it, which is the widget's title too (the "on/off" is the add
    /// list's alone, <see cref="WidgetInfo.AddTitle"/>).
    /// </summary>
    private static ToggleInfo Of(Toggle toggle, QuickSwitch which, string? nameKey = null)
    {
        var widget = WidgetInfos.For(which);
        return new(toggle, which, widget.Glyph, nameKey ?? $"{widget.WordsKey}.title");
    }

    /// <summary>The toggle <paramref name="toggle"/>, or null for a value outside the enum.</summary>
    public static ToggleInfo? Find(Toggle toggle) => (uint)toggle < (uint)All.Length ? All[(int)toggle] : null;

    /// <summary>What a new toggles row holds: the first row of Windows 11's quick settings, and do not disturb.</summary>
    public static readonly Toggle[] Default =
        [Toggle.Wifi, Toggle.Bluetooth, Toggle.AirplaneMode, Toggle.EnergySaver, Toggle.NightLight, Toggle.DoNotDisturb];

    /// <summary>
    /// <paramref name="chosen"/> as a row can hold it: the toggles this build knows, each once, in
    /// the order chosen. Empty is kept: a row of nothing says so and asks to be set up.
    /// </summary>
    public static List<Toggle> Known(IEnumerable<Toggle>? chosen) =>
        [.. (chosen ?? []).Where(toggle => Find(toggle) is not null).Distinct()];
}

/// <summary>list: the toggles a toggles row shows, in its order (<see cref="WidgetInfos.Toggles"/>).</summary>
public sealed class TogglesOptions
{
    /// <summary>The toggles, left to right and then down: what the person picked, in their order.</summary>
    public List<Toggle> Shown { get; set; } = [.. Toggles.Default];
}
