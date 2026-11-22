// Windows Update, for the status icon for updates (Indicators.cs): whether a restart is waiting to
// finish installing updates, and when Windows will restart for it where it has said so — the time
// somebody scheduled, or the time it picked outside active hours, or the deadline it must restart by —
// and whether updates are waiting, downloading or installing, as Windows 11's own update icon in its
// tray says. The Windows Update widget that showed the restart on its own is gone (the person,
// 2026-10-08): the status icons already say all it said.
//
// EVERYTHING IS READ, NOTHING IS DONE. Karpo never restarts the PC for Windows Update and never
// changes how Windows Update restarts: a click opens Windows Update's page in Settings, and the menu
// its restart options (ms-settings:windowsupdate-options, Advanced options, where Windows 11 keeps
// "Get me up to date" and the restart notifications), where Windows' own buttons do it.
//
// WHERE IT IS READ, all in HKLM's 64-bit view, read-only, watched rather than polled, off the dock's
// thread:
//
//   restart waiting   SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\
//                     RebootRequired, the key Windows Update keeps while a restart is due (as the
//                     status icon always read it); and the volatile key the update orchestrator
//                     makes for the same ("Created reboot required volatile registry key",
//                     VolatileKeys\UOUpdateExRebootRequired in MoUsoCoreWorker.exe), looked for
//                     under its Orchestrator and its UX keys. Volatile keys go with the restart.
//   when              SOFTWARE\Microsoft\WindowsUpdate\UX\StateVariables, where the orchestrator
//                     keeps its restart's state (RebootUXLaunched, UXCachedRebootState and the
//                     smart scheduler's predicted times are there on this PC): a restart scheduled
//                     by the person (ScheduledRebootTime, UserConfirmedRebootTime), one it scheduled
//                     itself (AutoScheduledRebootTime, ForcedAutoScheduledRebootTime), and a deadline
//                     (DeadlineTime, SecondChanceDeadlineTime). The names are the orchestrator's own
//                     (usosvcimpl.dll, MoUsoCoreWorker.exe, whose Ux-prefixed names are stored
//                     without the prefix: UxUpToDateStatusTimestamp is UpToDateStatusTimestamp here);
//                     they were not seen with a restart waiting on this PC, so a time is believed only
//                     while a restart is, and only while it is still to come. A time is a FILETIME
//                     (the UpToDate timestamps are) or milliseconds since 1970 (the smart scheduler's
//                     are); TimeOf takes either, and a date written out.
//
//   what it is doing  SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator\Diagnostics\
//                     UpdateStateSnippet, a line of JSON the update orchestrator (MoUsoCoreWorker.exe,
//                     its "ManagerDiagnostics") writes again each time an action on an update starts
//                     or ends: {"DriverUpdateCount":1,"Drivers":[{"ID":…,"State":"Installed-PendingReboot",
//                     "Time":…}],"OS":{"ID":…,"State":"PostReboot-Completed","Time":…}}. Its states,
//                     from the same binary: Downloading, Downloaded, DownloadBlocked, DownloadFailed,
//                     Installing, InstallFailed, Installed-PendingReboot, Installed-Completed and
//                     PostReboot-Completed; Time is in 100 ns since 1970. It is how a restart for a
//                     driver alone is known (2026-10-05, this PC: a driver "Installed-PendingReboot",
//                     no RebootRequired key, and Windows' tray icon for it up with its orange dot),
//                     and the only cheap word on updates downloading or installing, or waiting to be.
//                     Work in hand or a restart waiting from before this start of Windows ended with
//                     it, and is not believed; updates still waiting (downloaded, held back, failed)
//                     count for a week from their time, and Windows writes them again when it tries
//                     again (Orchestrated).
//
// The Windows Update Agent is not asked: its search goes online, and IUpdateInstaller.IsBusy would have
// to be asked again and again. Everything above is a key watched, so nothing here polls, and nothing
// here follows Karpo's interval for what it fetches online: it is all on this PC.
//
// WHAT WINDOWS SHOWS. Windows 11's own icon for all of this is a notification icon of the update
// orchestrator's (MoNotificationUx.exe with MusNotifyIconHandler.dll: one icon at a time, its window
// "WUIconWindow"): Windows Update's arrows with a dot, from one of three icon files beside it in
// C:\Windows\UUS\amd64 — WindowsUpdateSeeker.ico (a blue dot), WindowsUpdateWarning.ico (orange) and
// WindowsUpdateImportant.ico (red). Which one Windows picks is its own; what was seen is red for a
// restart for Windows itself (the person, 2026-10-04) and orange for a restart for a driver alone
// (this PC, 2026-10-05). So Karpo's status icon wears red for a restart for Windows, orange for a
// driver's, and blue for updates waiting, downloading or installing (UpdateBadge, Badge).

using System.Globalization;
using System.Text.Json;
using Microsoft.Win32;
using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>What Windows Update is doing, as far as the taskbar shows it: the further down, the more it says.</summary>
internal enum UpdateStage
{
    /// <summary>Nothing to say: up to date, or nothing read yet.</summary>
    None,

    /// <summary>Updates waiting: downloaded and not installed, held back (a metered connection), or failed and to be tried again.</summary>
    Available,
    Downloading,
    Installing,

    /// <summary>A restart is waiting to finish installing updates.</summary>
    Restart,
}

/// <summary>The dot Windows' tray icon wears on the arrows, named for its icon file (see the top of this file).</summary>
internal enum UpdateBadge
{
    None,

    /// <summary>WindowsUpdateSeeker.ico: blue.</summary>
    Seeker,

    /// <summary>WindowsUpdateWarning.ico: orange.</summary>
    Warning,

    /// <summary>WindowsUpdateImportant.ico: red.</summary>
    Important,
}

/// <summary>Windows Update's restart, and what else it is doing, as the status icon shows it.</summary>
/// <param name="Known">Whether anything has been read yet; nothing is said before.</param>
/// <param name="Restart">A restart is waiting to finish installing updates.</param>
/// <param name="Scheduled">When Windows will restart for it, where it has said so.</param>
/// <param name="Deadline">When it must have restarted by, where something set a deadline.</param>
internal sealed record UpdateState(bool Known, bool Restart, DateTime? Scheduled = null, DateTime? Deadline = null)
{
    /// <summary>What it is doing besides, with no restart waiting: updates waiting, downloading or installing.</summary>
    public UpdateStage Stage { get; init; }

    /// <summary>The restart waiting is for drivers alone, not for Windows itself: Windows' orange dot rather than its red.</summary>
    public bool DriversOnly { get; init; }

    /// <summary>Nothing read yet.</summary>
    public static UpdateState Unknown => new(false, false);

    /// <summary>Read, and no restart waiting.</summary>
    public static UpdateState Nothing => new(true, false);

    /// <summary>A restart waiting, with no time Windows has said.</summary>
    public static UpdateState Due => new(true, true);
}

/// <summary>What the status icon for updates says and opens. Pure: the tests hold every word.</summary>
internal static class WindowsUpdate
{
    /// <summary>Windows Update's page in Settings: what a click opens.</summary>
    public const string Page = "ms-settings:windowsupdate";

    /// <summary>Its restart options: Advanced options, where Windows 11 keeps them.</summary>
    public const string RestartOptions = "ms-settings:windowsupdate-options";

    public static string PageTitle => WindowsPages.WindowsUpdate.Title;

    public static string RestartOptionsTitle => S.DockQuickUpdateRestartOptions;

    /// <summary>Windows Update's glyph in Settings, the arrows of its tray icon (Segoe Fluent Icons).</summary>
    public const string Glyph = "";

    /// <summary>What there is to show on the taskbar: a restart waiting first, then what it is doing; nothing before anything is read.</summary>
    internal static UpdateStage Shown(UpdateState state) =>
        !state.Known ? UpdateStage.None : state.Restart ? UpdateStage.Restart : state.Stage;

    /// <summary>
    /// The dot Windows' tray icon wears for <paramref name="state"/> (see the top of this file): red for
    /// a restart for Windows, orange for one for drivers alone, blue for updates waiting, downloading
    /// or installing, none with nothing to say.
    /// </summary>
    internal static UpdateBadge Badge(UpdateState state) => Shown(state) switch
    {
        UpdateStage.Restart => state.DriversOnly ? UpdateBadge.Warning : UpdateBadge.Important,
        UpdateStage.None => UpdateBadge.None,
        _ => UpdateBadge.Seeker,
    };

    /// <summary>
    /// What the status icon's hover says, as short as Windows' own taskbar words: "Updates available",
    /// "Downloading updates", "Installing updates", "Restart required"; empty with nothing to say.
    /// </summary>
    internal static string Status(UpdateStage stage) => stage switch
    {
        UpdateStage.Available => S.DockQuickIndicatorUpdatesAvailable,
        UpdateStage.Downloading => S.DockQuickIndicatorDownloadingUpdates,
        UpdateStage.Installing => S.DockQuickIndicatorInstallingUpdates,
        UpdateStage.Restart => S.DockQuickUpdateRestartRequired,
        _ => "",
    };
}

/// <summary>
/// What the update orchestrator says it is doing (<see cref="WindowsUpdateRegistry.OfSnippet"/>): the
/// furthest stage any update is at, and whether a restart waiting is for Windows itself rather than
/// for drivers alone. Nothing by default.
/// </summary>
internal readonly record struct Orchestrated(UpdateStage Stage, bool ForWindows);

/// <summary>Where Windows Update keeps its restart in the registry, and what that says (see the top of this file).</summary>
internal static class WindowsUpdateRegistry
{
    internal const string AutoUpdate = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update"; // not-ui: a registry path
    internal const string Orchestrator = @"SOFTWARE\Microsoft\WindowsUpdate\Orchestrator";
    internal const string Ux = @"SOFTWARE\Microsoft\WindowsUpdate\UX";
    internal const string StateVariables = Ux + @"\StateVariables";

    /// <summary>The orchestrator's volatile key for a restart waiting, under either of the keys it may be in.</summary>
    internal const string VolatileRestart = @"VolatileKeys\UOUpdateExRebootRequired";

    /// <summary>The update orchestrator's own key under Windows Update's, where it keeps what it is doing (see the top of this file).</summary>
    internal const string Working = @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Orchestrator"; // not-ui: a registry path

    /// <summary>Where under <see cref="Working"/> the orchestrator writes its updates' states, and the value.</summary>
    internal const string Diagnostics = Working + @"\Diagnostics";

    internal const string Snippet = "UpdateStateSnippet";

    /// <summary>How long updates waiting are believed after the orchestrator last wrote them: Windows writes them again when it tries again.</summary>
    internal static readonly TimeSpan Waiting = TimeSpan.FromDays(7);

    /// <summary>
    /// The keys watched, in HKLM: Auto Update for its RebootRequired key coming and going (a key's
    /// own subkeys are its names, which a watch of it alone hears), the orchestrator's and its UX's
    /// with everything under them, and the orchestrator's working key with everything under it, its
    /// Diagnostics key included, which may come later than the watch.
    /// </summary>
    internal static readonly (string Key, bool Subtree)[] Watched = [(AutoUpdate, false), (Orchestrator, true), (Ux, true), (Working, true)];

    /// <summary>A restart scheduled by the person, first; then one Windows scheduled itself.</summary>
    internal static readonly string[] ScheduledNames = ["ScheduledRebootTime", "UserConfirmedRebootTime", "AutoScheduledRebootTime", "ForcedAutoScheduledRebootTime"];

    /// <summary>A deadline for the restart.</summary>
    internal static readonly string[] DeadlineNames = ["DeadlineTime", "SecondChanceDeadlineTime"];

    /// <summary>Windows' state now, read from the registry. Throws nothing a missing or locked key raises.</summary>
    internal static UpdateState Read()
    {
        using var root = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        var due = Has(root, $@"{AutoUpdate}\RebootRequired")
            || Has(root, $@"{Orchestrator}\{VolatileRestart}")
            || Has(root, $@"{Ux}\{VolatileRestart}");
        var utcNow = Clock.RealUtc();
        // Windows' start: now less the milliseconds since it, which go on counting through sleep.
        var orchestrated = OfSnippet(Value(root, Diagnostics, Snippet) as string, utcNow, utcNow - TimeSpan.FromMilliseconds(Environment.TickCount64));
        RegistryKey? variables = null;
        try
        {
            variables = root.OpenSubKey(StateVariables, writable: false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
        }
        using (variables)
        {
            return Of(due, orchestrated, name => variables?.GetValue(name), utcNow);
        }
    }

    /// <summary>
    /// The state for the restart keys (<paramref name="due"/>) and what the orchestrator's snippet
    /// says (<paramref name="orchestrated"/>): a restart waiting where either says so — for drivers
    /// alone where only the snippet does, and only for drivers — else what it is doing. Pure.
    /// </summary>
    internal static UpdateState Of(bool due, Orchestrated orchestrated, Func<string, object?> value, DateTime utcNow)
    {
        var restart = due || orchestrated.Stage == UpdateStage.Restart;
        var state = Of(restart, value, utcNow);
        return restart
            ? state with { DriversOnly = !due && !orchestrated.ForWindows }
            : state with { Stage = orchestrated.Stage };
    }

    /// <summary>
    /// What the orchestrator's <see cref="Snippet"/> says (see the top of this file): the furthest
    /// stage any of its updates is at — Windows' own and its drivers' — and whether a restart waiting
    /// is for Windows itself. Nothing for no snippet, or one that is not its JSON. Pure:
    /// <paramref name="bootUtc"/> is when Windows started.
    /// </summary>
    internal static Orchestrated OfSnippet(string? json, DateTime utcNow, DateTime bootUtc)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return default;
            }
            var furthest = UpdateStage.None;
            var forWindows = false;
            void Take(JsonElement update, bool windows)
            {
                if (update.ValueKind != JsonValueKind.Object
                    || !update.TryGetProperty("State", out var named) || named.ValueKind != JsonValueKind.String)
                {
                    return;
                }
                DateTime? at = update.TryGetProperty("Time", out var time) && time.ValueKind == JsonValueKind.Number && time.TryGetInt64(out var ticks)
                    ? SnippetTime(ticks)
                    : null;
                var stage = StageOf(named.GetString(), at, utcNow, bootUtc);
                forWindows |= windows && stage == UpdateStage.Restart;
                if (stage > furthest)
                {
                    furthest = stage;
                }
            }
            if (root.TryGetProperty("OS", out var system))
            {
                Take(system, windows: true);
            }
            if (root.TryGetProperty("Drivers", out var drivers) && drivers.ValueKind == JsonValueKind.Array)
            {
                foreach (var driver in drivers.EnumerateArray())
                {
                    Take(driver, windows: false);
                }
            }
            return new Orchestrated(furthest, forWindows);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// One update's state as the orchestrator names it, at the time it wrote it: work in hand or a
    /// restart waiting only where written since Windows started (a restart since has made it, or
    /// ended the work), updates waiting for <see cref="Waiting"/>; a state with no time is believed.
    /// Done, and any name this build does not know, is nothing. Pure.
    /// </summary>
    internal static UpdateStage StageOf(string? state, DateTime? at, DateTime utcNow, DateTime bootUtc)
    {
        var stage = state switch
        {
            "Installed-PendingReboot" => UpdateStage.Restart,
            "Installing" => UpdateStage.Installing,
            "Downloading" => UpdateStage.Downloading,
            "Downloaded" or "DownloadBlocked" or "DownloadFailed" or "InstallFailed" => UpdateStage.Available,
            _ => UpdateStage.None,
        };
        if (stage == UpdateStage.None || at is not { } written)
        {
            return stage;
        }
        var believed = stage == UpdateStage.Available ? written > utcNow - Waiting : written >= bootUtc;
        return believed ? stage : UpdateStage.None;
    }

    /// <summary>The snippet's Time, 100 ns since 1970, in UTC; null where it is no time.</summary>
    internal static DateTime? SnippetTime(long ticks) =>
        ticks > 0 && ticks <= DateTime.MaxValue.Ticks - DateTime.UnixEpoch.Ticks ? DateTime.UnixEpoch.AddTicks(ticks) : null;

    private static object? Value(RegistryKey root, string path, string name)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            return key?.GetValue(name);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    /// <summary>
    /// The state for a restart waiting or not (<paramref name="due"/>) and the orchestrator's values as
    /// <paramref name="value"/> reads them, at <paramref name="utcNow"/>: a time only while a restart
    /// is waiting and only while it is still to come, in local time. Pure.
    /// </summary>
    internal static UpdateState Of(bool due, Func<string, object?> value, DateTime utcNow)
    {
        if (!due)
        {
            return UpdateState.Nothing;
        }
        DateTime? First(string[] names)
        {
            foreach (var name in names)
            {
                if (TimeOf(value(name)) is { } at && at > utcNow)
                {
                    return at.ToLocalTime();
                }
            }
            return null;
        }
        return new UpdateState(true, true, First(ScheduledNames), First(DeadlineNames));
    }

    /// <summary>
    /// A time as the orchestrator writes one, in UTC: a FILETIME (100 ns since 1601), milliseconds or
    /// seconds since 1970, or a date written out; null for nothing, zero, or anything else.
    /// </summary>
    internal static DateTime? TimeOf(object? value)
    {
        try
        {
            switch (value)
            {
                case string text when DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed):
                    return parsed;
                case long or int or uint or ulong:
                {
                    var number = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                    return number switch
                    {
                        // FILETIMEs from 1970 on.
                        >= 116_444_736_000_000_000 => DateTime.FromFileTimeUtc(number),
                        // Milliseconds since 1970, from 2001 on.
                        >= 1_000_000_000_000 => DateTime.UnixEpoch.AddMilliseconds(number),
                        // Seconds since 1970, from 2001 on.
                        >= 1_000_000_000 and < 100_000_000_000 => DateTime.UnixEpoch.AddSeconds(number),
                        _ => null,
                    };
                }
                case byte[] { Length: 8 } bytes:
                    return TimeOf(BitConverter.ToInt64(bytes));
            }
        }
        catch (Exception error) when (error is ArgumentOutOfRangeException or OverflowException)
        {
        }
        return null;
    }

    private static bool Has(RegistryKey root, string path)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            return key is not null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return false;
        }
    }
}
