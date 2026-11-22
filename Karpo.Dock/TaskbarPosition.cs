// The edge Windows' own taskbar stands on, which Karpo sets to the bottom while Karpo runs (docs/widgets.md §591), and
// leaves there once it ends (§600).
//
// The person, 2026-11-09: "the windows taskbar is still not shown where the karpo bar is shown". Since build
// 26300.8493 Windows 11 lets its taskbar stand on any edge (Settings › Personalization › Taskbar › Taskbar behaviors;
// one edge for every display). How this build keeps and applies it, read from its own files:
//
//   - the setting is Settings' SystemSettings_DesktopTaskbar_Lo, registered under
//     HKLM\SOFTWARE\Microsoft\SystemSettings\SettingId with its handler, SettingsHandlers_DesktopTaskbar.dll. A Windows
//     without it has no edge but the bottom: that is what Karpo asks, never a build number;
//   - the value is HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced, TaskbarLocation, a DWORD of
//     ABE_*'s numbers: 0 left, 1 top, 2 right, 3 bottom; none is the bottom. Explorer's Taskbar.dll reads it;
//   - the handler's setter writes that value, then tells Explorer's taskbar with SendMessageW(FindWindow(
//     "Shell_TrayWnd"), WM_USER + 0x1CA, 6, value): no broadcast, no restart. Karpo does the same, to Explorer's own
//     tray (Karpo's tray window has the class too; it would pass the message on all the same);
//   - the policy DisableTaskbarPosition (Software\Policies\Microsoft\Windows\Explorer, HKLM or HKCU; Taskbar.admx)
//     holds the taskbar at the bottom: Karpo then asks nothing either.
//
// Why the bottom while Karpo runs: Windows can auto-hide its taskbar only there (Microsoft, 2026-05-15 and still on
// 2026-07-31: "Auto-hidden taskbar ... not yet supported" in the other positions; Settings: "This setting is only
// available when the taskbar is at the bottom"). Karpo hides Explorer's taskbar by auto-hiding it (§465): one that
// can't auto-hide holds its strip of the screen however hidden its windows are, and every bar on that edge would
// stand beside an empty strip. So the takeover moves it to the bottom first (Taskbar.TakeOver), and Rehide sends it back
// there should it be moved while Karpo runs.
//
// Why not back on Karpo's edge once Karpo ends (§600): Windows has one edge for every display, so Windows' taskbar
// moved to the right of the main display stood on the right of every display. The person, 2026-11-09: "dont do this
// then. because it moves it to the right on all screens." Every way Karpo ends only shows it again, at the bottom.

using Karpo.Core;
using Microsoft.Win32;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What this Windows says about its taskbar's edge, raw (see the top of the file); faked in the tests.</summary>
internal interface ITaskbarPositionStore
{
    /// <summary>Settings has the taskbar position setting (SystemSettings_DesktopTaskbar_Lo is registered).</summary>
    bool Offered();

    /// <summary>The policy DisableTaskbarPosition, the machine's over the user's; null where neither sets it.</summary>
    uint? Policy();

    /// <summary>TaskbarLocation; null where there is none (the bottom).</summary>
    uint? Location();

    /// <summary>TaskbarLocation written; false where Windows refused it.</summary>
    bool Write(uint location);

    /// <summary>Explorer's taskbar told of it, as Settings tells it; false where there is none or it did not answer.</summary>
    bool Tell(uint location);
}

/// <summary>Windows' taskbar's edge: read, mapped and moved (see the top of the file). Pure but for the store.</summary>
internal static class TaskbarPosition
{
    /// <summary>The edge Windows' taskbar stands on while Karpo runs: the one it can be auto-hidden on (see the top of the file).</summary>
    public const Edge Parked = Edge.Bottom;

    /// <summary>What moving it came to.</summary>
    internal enum Moved
    {
        /// <summary>This Windows has no edge but the bottom (no position setting, a policy, a test's run): nothing asked.</summary>
        NotOffered,

        /// <summary>It stands there already: nothing asked.</summary>
        Already,

        /// <summary>Written and Explorer told: it moves now.</summary>
        Done,

        /// <summary>Written, but no Explorer took the word: it stands there from Explorer's next start.</summary>
        Untold,

        /// <summary>Windows refused the value: it stays where it was.</summary>
        Refused,
    }

    /// <summary>TaskbarLocation's number for <paramref name="edge"/> (ABE_*). Pure.</summary>
    public static uint ValueOf(Edge edge) => edge switch
    {
        Edge.Left => 0,
        Edge.Top => 1,
        Edge.Right => 2,
        _ => 3,
    };

    /// <summary>The edge TaskbarLocation <paramref name="value"/> stands for; none, or a number this build does not know, is the bottom, as Explorer has it. Pure.</summary>
    public static Edge EdgeOf(uint? value) => value switch
    {
        0 => Edge.Left,
        1 => Edge.Top,
        2 => Edge.Right,
        _ => Edge.Bottom,
    };

    /// <summary>Whether Windows lets its taskbar stand on another edge than the bottom: Settings offers it, and no policy holds it. Pure but for <paramref name="store"/>.</summary>
    public static bool Offered(ITaskbarPositionStore store) => store.Offered() && store.Policy() is null or 0;

    /// <summary>The edge Windows' taskbar stands on; null where it has no other than the bottom (<see cref="Offered"/>). Pure but for <paramref name="store"/>.</summary>
    public static Edge? Current(ITaskbarPositionStore store) => Offered(store) ? EdgeOf(store.Location()) : null;

    /// <summary>Windows' taskbar moved to <paramref name="edge"/> as Settings moves it: written, then Explorer told; only where it stands elsewhere. Pure but for <paramref name="store"/>.</summary>
    public static Moved Move(ITaskbarPositionStore store, Edge edge)
    {
        if (Current(store) is not { } now)
        {
            return Moved.NotOffered;
        }
        if (now == edge)
        {
            return Moved.Already;
        }
        var value = ValueOf(edge);
        if (!store.Write(value))
        {
            return Moved.Refused;
        }
        return store.Tell(value) ? Moved.Done : Moved.Untold;
    }

    /// <summary>What moving it to <paramref name="edge"/> came to, as the appbar trace and karpo.log say it; null where nothing was asked. Pure.</summary>
    public static string? Says(Moved moved, Edge edge) => moved switch
    {
        Moved.Done => $"Windows' taskbar moved to the {Name(edge)}", // not-ui: a log
        Moved.Untold => $"Windows' taskbar set to the {Name(edge)}, Explorer not told: from its next start", // not-ui: a log
        Moved.Refused => $"Windows' taskbar to be moved to the {Name(edge)}, but Windows refused TaskbarLocation", // not-ui: a log
        _ => null,
    };

    private static string Name(Edge edge) => edge.ToString().ToLowerInvariant();
}

/// <summary>
/// This Windows' own (see the top of the file). Never from a test's run or a stage: a dock on a config of its own has no
/// taskbar of the person's to move, so there it says the position is not offered and reads nothing.
/// </summary>
internal sealed unsafe class LiveTaskbarPosition : ITaskbarPositionStore
{
    private const string Setting = @"SOFTWARE\Microsoft\SystemSettings\SettingId\SystemSettings_DesktopTaskbar_Lo";
    private const string Advanced = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string Policies = @"Software\Policies\Microsoft\Windows\Explorer";
    private const string Value = "TaskbarLocation";

    /// <summary>The message Settings tells Explorer's taskbar a setting with, and the taskbar position's number.</summary>
    private const uint TraySetting = WM.WM_USER + 0x1CA;
    private const nuint PositionSetting = 6;

    public bool Offered()
    {
        if (Config.IsIsolated || Stage.On)
        {
            return false;
        }
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(Setting);
            return key is not null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public uint? Policy() => Dword(Registry.LocalMachine, Policies, "DisableTaskbarPosition") ?? Dword(Registry.CurrentUser, Policies, "DisableTaskbarPosition");

    public uint? Location() => Dword(Registry.CurrentUser, Advanced, Value);

    public bool Write(uint location)
    {
        if (Config.IsIsolated || Stage.On)
        {
            return false;
        }
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(Advanced, writable: true);
            key.SetValue(Value, unchecked((int)location), RegistryValueKind.DWord);
            return true;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            Troubles.Report("TaskbarPosition: writing TaskbarLocation", error);
            return false;
        }
    }

    public bool Tell(uint location)
    {
        var tray = Taskbar.ExplorerTray();
        if (tray == HWND.NULL)
        {
            return false;
        }
        nuint result = 0;
        return SendMessageTimeoutW(tray, TraySetting, PositionSetting, (nint)location, (uint)(SMTO_ABORTIFHUNG | SMTO_NORMAL), 2000, &result).Value != 0;
    }

    private static uint? Dword(RegistryKey hive, string path, string name)
    {
        try
        {
            using var key = hive.OpenSubKey(path);
            return key?.GetValue(name) is int value ? unchecked((uint)value) : null;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
