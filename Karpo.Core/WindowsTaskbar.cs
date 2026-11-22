// What Windows 11's own taskbar has, for the bar a new install or a reset gets
// (Sections.DefaultLayout): "you should make the default layout that shows with new installations
// as close as possible to how windows would show it" (2026-10-05), and then "i basically want to
// have everything of the vanilla taskbar in the default layout" (2026-10-10).
//
// Windows 11 25H2 (26200) out of the box, centred: the Widgets button with the weather at the far
// left; Start, the search box, Task view, the pins and the open apps in the middle; at the right
// the "^" and the tray's icons, the input indicator, the network, sound and battery icons (the
// battery only on a PC with one), the time and date, the notifications bell and the show desktop
// sliver. Copilot is no button of the taskbar's own since 24H2 (the Copilot app is a pin where
// Windows pinned it), and neither is Chat (TaskbarMn).
//
// A new bar has every one of them but the Widgets button, at the left rather than centred (both
// 2026-10-14, below), whatever the person switched off in Windows' taskbar settings
// (the Widgets button, search, Task view, the far corner) and however many keyboard layouts are
// installed: the vanilla taskbar, not the person's (2026-10-10). Until then each switch was
// followed, and a PC with one layout got no input indicator, as Windows shows none there. What is
// still read, from the values Windows itself keeps
// (HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced), each where it is missing
// being Windows' own default:
//
//   * ShowSecondsInSystemClock: 1 shows the seconds on the clock: how the clock looks, not
//     whether there is one.
//   * IsBatteryPercentageEnabled: 1 shows the charge's percentage beside the battery icon (Settings ›
//     System › Power & battery › "Battery percentage", Windows 11 24H2 and on; off where it is
//     missing, as Windows ships it). §485: Windows' own quick settings button follows it
//     (WindowsFaces), Karpo's own battery face does not.
//   * Whether this PC has a battery (GetSystemPowerStatus): Windows shows no battery icon without
//     one, and Karpo's entry would stand there as a power button no vanilla taskbar has.
//
// Not the alignment (TaskbarAl): a new bar has Start, search, Task view and the apps at the left and
// the middle empty, whichever way Windows' own is aligned ("the start should be on the left together
// with the app pins ... center should be empty by default"), and no Widgets button nor the weather
// in its place ("dont actually show the widgets button"), both the person's of 2026-10-14. Karpo
// never sets the alignment.
//
// A config of its own (the tests, --smoke) reads none of it: it gets Windows' out-of-the-box
// taskbar (OutOfTheBox), so what a new bar holds does not depend on the PC it is made on.

using System.Globalization;
using System.Runtime.InteropServices;

namespace Karpo.Core;

/// <summary>
/// What of Windows 11's own taskbar a new bar follows (see the top of the file). Every member's
/// default is Windows' out-of-the-box answer.
/// </summary>
public sealed record WindowsTaskbar
{
    /// <summary>The Microsoft Store, by its app id, as Windows pins it.</summary>
    public const string StoreApp = @"shell:AppsFolder\Microsoft.WindowsStore_8wekyb3d8bbwe!App";

    /// <summary>The clock shows the seconds (ShowSecondsInSystemClock).</summary>
    public bool Seconds { get; init; }

    /// <summary>§485: the battery's percentage shows beside its icon (IsBatteryPercentageEnabled).</summary>
    public bool BatteryPercent { get; init; }

    /// <summary>This PC has a battery, so Windows shows its icon beside the network and the sound.</summary>
    public bool Battery { get; init; } = true;

    /// <summary>This PC's screen turns with it, so Windows' quick settings offer rotation lock.</summary>
    public bool Rotates { get; init; }

    /// <summary>§485: Windows' "Battery percentage" switch, a DWORD of Explorer\Advanced.</summary>
    public const string BatteryPercentValue = "IsBatteryPercentageEnabled";

    /// <summary>Windows 11's taskbar as a new install has it, on a PC with a battery.</summary>
    public static WindowsTaskbar OutOfTheBox { get; } = new();

    /// <summary>This PC's taskbar as Windows shows it now; <see cref="OutOfTheBox"/> for a config of its own.</summary>
    public static WindowsTaskbar Read()
    {
        if (Config.Isolated is not null)
        {
            return OutOfTheBox;
        }
        return From(Sections.ReadAdvanced, HasBattery()) with { Rotates = ScreenRotates() };
    }

    /// <summary>
    /// The taskbar the values say: <paramref name="advanced"/> reads one of Explorer\Advanced's
    /// (null where it is missing), and <paramref name="battery"/> whether this PC has one.
    /// </summary>
    public static WindowsTaskbar From(Func<string, int?> advanced, bool battery) => new()
    {
        Seconds = advanced("ShowSecondsInSystemClock") is 1,
        BatteryPercent = advanced(BatteryPercentValue) is 1,
        Battery = battery,
    };

    /// <summary>
    /// The apps Windows 11 pins to a new taskbar, in its order, those of them this PC has: File
    /// Explorer, Microsoft Edge and the Microsoft Store — Windows' own pins, which Microsoft's
    /// documentation names, and what a new bar pins where Windows' own pins can't be read at all.
    /// Outlook and the Copilot app, which some installs pin as well, are pins Windows' own list then
    /// holds, and come with it.
    /// </summary>
    public static IReadOnlyList<string> DefaultPins(ITargetSystem system)
    {
        var pins = new List<string>();
        void Add(Environment.SpecialFolder folder, string name)
        {
            if (system.Folder(folder) is { Length: > 0 } root)
            {
                Offer(Path.Combine(root, name));
            }
        }
        void Offer(string target)
        {
            if (!PinTargets.Check(target, system).Missing)
            {
                pins.Add(target);
            }
        }
        Add(Environment.SpecialFolder.Windows, "explorer.exe");
        Add(Environment.SpecialFolder.CommonPrograms, "Microsoft Edge.lnk"); // not-ui: a file name
        Offer(StoreApp);
        return pins;
    }

    /// <summary>
    /// Whether this PC has a system battery (GetSystemPowerStatus), as the battery widget tells it
    /// (QuickSettings\Services\Battery.cs); true where the call fails, so a battery is never lost.
    /// </summary>
    private static unsafe bool HasBattery()
    {
        PowerStatus status;
        // BatteryFlag 128: no system battery; 255: can't tell, which has that bit too.
        return GetSystemPowerStatus(&status) == 0 || (status.BatteryFlag & 128) == 0;
    }

    /// <summary>
    /// Whether this PC's screen turns with it (GetAutoRotationState: neither AR_NOSENSOR nor
    /// AR_NOT_SUPPORTED), as the rotation lock widget tells it (QuickSettings\Services\Rotation.cs);
    /// false where the call fails, so no tile is offered that does nothing.
    /// </summary>
    public static unsafe bool ScreenRotates()
    {
        uint state;
        return GetAutoRotationState(&state) != 0 && (state & 0x30) == 0;
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern unsafe int GetAutoRotationState(uint* state);

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern unsafe int GetSystemPowerStatus(PowerStatus* status);
}
