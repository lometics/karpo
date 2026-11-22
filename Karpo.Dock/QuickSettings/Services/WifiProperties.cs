// A Wi-Fi network's properties: Windows' own dialog for its saved profile
// (WlanUIEditProfile), its connection and its security pages, as Windows' own
// list opens it. Where it cannot be shown, Windows' Wi-Fi settings open instead.
//
// The dialog runs in a Karpo process of its own (Karpo.exe --wifi-properties),
// never in the dock. The first version called it on a thread of the dock's, from
// wlanapi.dll — which does not export it: it lives in wlanui.dll. The call threw
// EntryPointNotFoundException at the top of that thread, and an exception there
// ends the process: every click on Properties took the bar down (28 Sep, three
// times in a minute, karpo.log). The import names wlanui.dll now; but the dialog is
// Windows' code, modal, with its own pages and its own COM, and whatever it does —
// a missing export on some other build, a crash inside a page — must not end the
// bar. So the dock only starts the process and waits for it, off its own thread;
// a process that ends any way but well (it could not show the dialog, it threw, it
// crashed) has Windows' Wi-Fi settings opened in its place, and nothing the wait
// itself meets escapes the thread either.

using System.Diagnostics;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal static unsafe class WifiProperties
{
    /// <summary>The switch that makes Karpo.exe this dialog and nothing else: <c>--wifi-properties {adapter} {profile}</c>.</summary>
    public const string Switch = "--wifi-properties";

    /// <summary>Where the person goes when the dialog cannot be shown.</summary>
    public const string Settings = "ms-settings:network-wifi";

    /// <summary>The process ended well: the dialog was shown, and closed.</summary>
    internal const int Shown = 0;

    /// <summary>Windows would not show the dialog (WlanUIEditProfile answered an error).</summary>
    internal const int Refused = 1;

    /// <summary>The arguments were wrong, or showing it threw.</summary>
    internal const int Failed = 2;

    /// <summary>
    /// ERROR_CANCELLED. Windows' dialog answers ERROR_SUCCESS when the person closes it with Cancel
    /// or its close button, the same as for OK (tried on Windows 11 24H2 on a desktop of its own):
    /// only a real failure is an error — ERROR_NOT_FOUND for a profile it does not have,
    /// ERROR_INVALID_PARAMETER for an adapter it does not know, each after Windows' own error box.
    /// A cancel that some build answers as this is a cancel all the same, and opens nothing else.
    /// </summary>
    internal const uint Cancelled = 1223;

    /// <summary>
    /// Opens the dialog of <paramref name="profile"/> on <paramref name="adapter"/> without making the
    /// dock wait: a thread of its own starts the process and waits for it. False where not even the
    /// thread could start, and the caller opens Windows' settings itself.
    /// </summary>
    public static bool Start(string profile, Guid adapter) =>
        Begin(profile, adapter, InAProcessOfItsOwn, static target => Launch.Open(target)) is not null;

    /// <summary>
    /// The thread that shows the dialog, started; null where it could not start. What shows the
    /// dialog and what opens Windows' settings are handed in, for the tests, which wait for it:
    /// nothing either does can escape the thread.
    /// </summary>
    internal static Thread? Begin(string profile, Guid adapter, Func<string, Guid, bool> show, Action<string> fallBack)
    {
        try
        {
            var thread = new Thread(() => Show(profile, adapter, show, fallBack)) { IsBackground = true, Name = "Wi-Fi properties" }; // not-ui: a thread's name
            thread.Start();
            return thread;
        }
        catch (Exception error)
        {
            Troubles.Note("Wi-Fi properties: starting the thread", error);
            return null;
        }
    }

    /// <summary>
    /// Shows the dialog, and Windows' Wi-Fi settings where it was not shown. Whatever either throws
    /// is noted and goes no further: this runs at the top of a thread, where anything thrown ends
    /// the process.
    /// </summary>
    internal static void Show(string profile, Guid adapter, Func<string, Guid, bool> show, Action<string> fallBack)
    {
        bool shown;
        try
        {
            shown = show(profile, adapter);
        }
        catch (Exception error)
        {
            Troubles.Note("Wi-Fi properties: showing them", error);
            shown = false;
        }
        if (shown)
        {
            return;
        }
        try
        {
            fallBack(Settings);
        }
        catch (Exception error)
        {
            Troubles.Note("Wi-Fi properties: opening Wi-Fi settings instead", error);
        }
    }

    /// <summary>
    /// Karpo.exe again, as the dialog, waited for: true when it showed the dialog and ended well.
    /// The dock's right to come forward (it was clicked) is handed on, or the dialog would open
    /// behind the window the person was in.
    /// </summary>
    private static bool InAProcessOfItsOwn(string profile, Guid adapter)
    {
        if (Environment.ProcessPath is not { } exe)
        {
            return false;
        }
        var start = new ProcessStartInfo(exe) { UseShellExecute = false };
        foreach (var argument in Arguments(profile, adapter))
        {
            start.ArgumentList.Add(argument);
        }
        // §491: through the launcher, which closes what of Karpo's is open and hands the right on.
        using var process = Launch.Program(start);
        if (process is null)
        {
            return false;
        }
        process.WaitForExit();
        if (process.ExitCode != Shown)
        {
            Troubles.Write($"Wi-Fi properties: the dialog's process ended with 0x{process.ExitCode:X8}; opening Wi-Fi settings instead");
            return false;
        }
        return true;
    }

    /// <summary>The process's arguments, which <see cref="Run"/> reads back.</summary>
    internal static string[] Arguments(string profile, Guid adapter) => [Switch, adapter.ToString("D"), profile];

    /// <summary>Karpo.exe started with <see cref="Switch"/>: shows the dialog, and says how it went in its exit code.</summary>
    public static int Run(string[] args) => Run(args, Edit);

    /// <summary>
    /// <see cref="Run(string[])"/> with the dialog handed in, for the tests: Windows' call answers an
    /// error code, and <see cref="Refused"/> or <see cref="Failed"/> send the dock to Windows' settings.
    /// </summary>
    internal static int Run(string[] args, Func<string, Guid, uint> dialog)
    {
        if (args is not [Switch, var id, var profile] || !Guid.TryParse(id, out var adapter) || profile.Length == 0)
        {
            return Failed;
        }
        try
        {
            // Closed, whether with OK or Cancel, is shown: only a failure opens Windows' settings.
            return dialog(profile, adapter) is 0 or Cancelled ? Shown : Refused;
        }
        catch (Exception error)
        {
            Troubles.Note("Wi-Fi properties: the dialog", error);
            return Failed;
        }
    }

    /// <summary>Windows' own properties of the profile named <paramref name="profile"/>, on its first page (connection).</summary>
    private static uint Edit(string profile, Guid adapter)
    {
        uint reason = 0;
        fixed (char* name = profile)
        {
            return Wlan.WlanUIEditProfile(Wlan.UiVersion, name, &adapter, HWND.NULL, 0, null, &reason);
        }
    }
}
