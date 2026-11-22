// Nearby sharing, for its preset's line (NearbyPage), its face and its switch (docs\widgets.md
// §115): who may share with this PC — nobody, its own devices, everyone nearby — where what they
// share is saved, and the name it is found by.
//
// WHERE WINDOWS KEEPS IT, read on this PC (Windows 11, 26300, 2026-10-11) and in what Settings'
// own handler for the page names (SettingsHandlers_SharedExperiences_Rome.dll):
//
//   HKCU\Software\Microsoft\Windows\CurrentVersion\CDP
//     NearShareChannelUserAuthzPolicy  DWORD  0 off, 1 my devices only, 2 everyone nearby
//     NearShareFileSaveLocation        SZ     where received files go; none: the Downloads folder
//   ...\CDP\SettingsPage
//     NearShareChannelUserAuthzPolicy  DWORD  the mode last on, which turning it on again restores
//
// The policy read 0 here while the tile's handler said off, and the remembered one 1. The Connected
// Devices Platform reads the policy when a device asks (cdp.dll's
// CDPGetNearShareAuthorizationPolicyOfInteractiveUser), and the Settings handler writes these same
// values; Windows tells no app when they change, so the key is watched (RegistryWatch).
//
// SETTING IT goes the way Windows' own tile does where it can: off and on through the tile's
// Settings handler (SettingHandlers.cs, NearShareQuickAction), which also turns Bluetooth on for it
// where it is off, with Windows' toast. Which of the two modes it is on in is the remembered value,
// written before the handler turns it on, and the policy itself where it is on already. Where the
// handler is not there, the values are written alone. No test reaches any of it: a test hands the
// line a SampleNearby, and the probe that read the above wrote nothing.
//
// Everything that reads or writes runs on the pool; the line reads the last state it was told.

using Microsoft.Win32;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Who may share with this PC, in the order Windows lists them.</summary>
internal enum NearbyMode
{
    Off,
    MyDevices,
    Everyone,
}

/// <param name="Mode">Who may share with it; null until read, or where Windows says something this build does not know.</param>
/// <param name="SaveTo">The folder received files go to.</param>
/// <param name="Device">The name other devices find this PC by.</param>
/// <param name="Problem">Why the last change did not take, in the language now.</param>
/// <param name="Last">The mode turning it on brings back (<see cref="NearbyModes.Remembered"/>).</param>
internal sealed record NearbyState(NearbyMode? Mode, string SaveTo, string Device, string? Problem = null, NearbyMode Last = NearbyMode.MyDevices);

internal interface INearbySource : IDisposable
{
    void Start(Changes changes);

    NearbyState Read();

    /// <summary>Puts <paramref name="mode"/> in force, as Windows' page does. Never on the dock's thread.</summary>
    void Set(NearbyMode mode);

    /// <summary>Has received files saved to <paramref name="folder"/>.</summary>
    void SaveTo(string folder);

    /// <summary>Asks where to save received files (Windows' folder dialog) and saves there; on a thread of its own.</summary>
    void ChooseFolder();
}

internal static class NearbyNews
{
    public const int State = 1;
}

/// <summary>What Windows' values mean, and what each mode is called. Pure: the tests check it.</summary>
internal static class NearbyModes
{
    public static readonly NearbyMode[] All = [NearbyMode.Off, NearbyMode.MyDevices, NearbyMode.Everyone];

    /// <summary>The key Windows keeps nearby sharing under, in the current user's hive.</summary>
    internal const string Key = @"Software\Microsoft\Windows\CurrentVersion\CDP";

    /// <summary>The key of the mode last on.</summary>
    internal const string RememberedKey = Key + @"\SettingsPage";

    internal const string PolicyValue = "NearShareChannelUserAuthzPolicy";

    internal const string SaveValue = "NearShareFileSaveLocation";

    /// <summary>The mode a policy value names; null for none of the three (a value from a newer Windows, or none).</summary>
    public static NearbyMode? FromPolicy(object? value) => value switch
    {
        0 => NearbyMode.Off,
        1 => NearbyMode.MyDevices,
        2 => NearbyMode.Everyone,
        _ => null,
    };

    /// <summary>The policy value for <paramref name="mode"/>.</summary>
    public static int Policy(NearbyMode mode) => mode switch
    {
        NearbyMode.Off => 0,
        NearbyMode.MyDevices => 1,
        NearbyMode.Everyone => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Nearby sharing has no such mode."),
    };

    /// <summary>
    /// The mode turning it on brings back: the one last on, as Windows remembers it; my devices only
    /// where it remembers none, as Windows' tile does the first time.
    /// </summary>
    public static NearbyMode Remembered(object? value) =>
        FromPolicy(value) is NearbyMode.Everyone ? NearbyMode.Everyone : NearbyMode.MyDevices;

    /// <summary>Where received files go: the folder set, or <paramref name="downloads"/> where none is.</summary>
    public static string SaveTo(object? set, string downloads) =>
        set is string { Length: > 0 } path && path.Trim().Length > 0 ? Environment.ExpandEnvironmentVariables(path.Trim()) : downloads;

    public static string Title(NearbyMode mode) => mode switch
    {
        NearbyMode.Off => S.DockQuickNearbyOff,
        NearbyMode.MyDevices => S.DockQuickNearbyMyDevices,
        NearbyMode.Everyone => S.DockQuickNearbyEveryone,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Nearby sharing has no such mode."),
    };

    /// <summary>
    /// A Segoe Fluent Icons glyph for each, so the rows read as the other quick settings lists do:
    /// nobody (Cancel), this person's devices (Devices), everyone (People).
    /// </summary>
    public static string Glyph(NearbyMode mode) => char.ConvertFromUtf32(mode switch
    {
        NearbyMode.Off => 0xE711,
        NearbyMode.MyDevices => 0xE772,
        NearbyMode.Everyone => 0xE716,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Nearby sharing has no such mode."),
    });

    /// <summary>Who each mode lets share, the quieter line under its name.</summary>
    public static string Detail(NearbyMode mode) => mode switch
    {
        NearbyMode.Off => S.DockQuickNearbyOffDetail,
        NearbyMode.MyDevices => S.DockQuickNearbyMyDevicesDetail,
        NearbyMode.Everyone => S.DockQuickNearbyEveryoneDetail,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Nearby sharing has no such mode."),
    };

    /// <summary>
    /// What hovering the face says, and the switch's line under its name: "Nearby sharing: off", or
    /// "Nearby sharing: on" and who may share. Its name alone while Windows has not said.
    /// </summary>
    public static string Tip(NearbyMode? mode) => mode switch
    {
        NearbyMode.Off => S.WidgetSwitchOffTip(S.DockQuickNearbyTitle),
        NearbyMode.MyDevices or NearbyMode.Everyone => $"{S.WidgetSwitchOnTip(S.DockQuickNearbyTitle)}\n{Title(mode.Value)}",
        _ => S.DockQuickNearbyTitle,
    };

    /// <summary>
    /// What turning <paramref name="to"/> on from <paramref name="now"/> takes: the remembered mode to
    /// write first (null: none), whether the tile is switched, and the policy to write after it
    /// where the tile did not put it in force. Pure: the tests walk it.
    /// </summary>
    public static (int? Remember, bool? Tile, int Policy) Plan(NearbyMode? now, NearbyMode to) => to == NearbyMode.Off
        ? (null, now is NearbyMode.Off ? null : false, 0)
        : (Policy(to), now is NearbyMode.Off or null ? true : null, Policy(to));
}

/// <summary>Windows' own (see the top of this file).</summary>
internal sealed unsafe class WindowsNearby : INearbySource
{
    /// <summary>FOLDERID_Downloads.</summary>
    private static readonly Guid Downloads = new("374DE290-123F-4565-9164-39C4925E467B");

    private readonly object gate = new();
    private volatile NearbyState state = new(null, "", "");
    private Changes? changes;
    private RegistryWatch? watch;
    private SettingHandler? tile;
    private bool looked;
    private volatile bool disposed;

    public void Start(Changes news)
    {
        changes = news;
        _ = Task.Run(() => Troubles.Guard("Nearby sharing: reading it", () =>
        {
            Look();
            if (!disposed)
            {
                // The whole key: the remembered mode's under it too.
                watch = RegistryWatch.Open(RegistryHive.CurrentUser, NearbyModes.Key, subtree: true, () => Troubles.Guard("Nearby sharing: reading it again", () => Look()));
            }
        }));
    }

    public NearbyState Read() => state;

    public void Set(NearbyMode mode) => _ = Task.Run(() => Troubles.Guard("Nearby sharing: setting it", () =>
    {
        var (remember, switched, policy) = NearbyModes.Plan(state.Mode, mode);
        var problem = (string?)null;
        try
        {
            if (remember is { } last)
            {
                Write(NearbyModes.RememberedKey, NearbyModes.PolicyValue, last);
            }
            if (switched is { } on && Tile() is { } through && !through.Set(on))
            {
                Troubles.Write($"Nearby sharing: the tile's handler refused {(on ? "on" : "off")}");
            }
            // Where the tile put it in force, this writes what is there already; where there is no
            // tile, or it refused, or the mode changes while on, it is the change.
            if (NearbyModes.FromPolicy(Value(NearbyModes.Key, NearbyModes.PolicyValue)) != mode)
            {
                Write(NearbyModes.Key, NearbyModes.PolicyValue, policy);
            }
        }
        catch (Exception error) when (error is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Troubles.Note("Nearby sharing: setting it", error);
            problem = S.DockQuickNearbyFailed;
        }
        Look(problem);
    }));

    public void SaveTo(string folder) => _ = Task.Run(() => Troubles.Guard("Nearby sharing: setting where files go", () =>
    {
        using var key = Registry.CurrentUser.CreateSubKey(NearbyModes.Key, writable: true);
        key.SetValue(NearbyModes.SaveValue, folder, RegistryValueKind.String);
        Look();
    }));

    public void ChooseFolder()
    {
        var start = state.SaveTo;
        var thread = new Thread(() => Troubles.Guard("Nearby sharing: choosing where files go", () =>
        {
            if (FolderDialog.Pick(HWND.NULL, start, S.DockQuickNearbySaveChoose) is { } chosen)
            {
                SaveTo(chosen);
            }
        }))
        { IsBackground = true, Name = "Karpo nearby sharing folder" }; // not-ui: a thread's name
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Dispose()
    {
        disposed = true;
        watch?.Dispose();
        watch = null;
        lock (gate)
        {
            tile?.Release();
            tile = null;
        }
    }

    /// <summary>The tile's handler, opened the first time it is needed; null where it is not there.</summary>
    private SettingHandler? Tile()
    {
        lock (gate)
        {
            if (!looked)
            {
                looked = true;
                tile = SettingHandler.Open(SettingHandler.NearbySharing);
            }
            return tile;
        }
    }

    /// <summary>Reads it all again and says so; <paramref name="problem"/> is the last change's, where it failed.</summary>
    private void Look(string? problem = null)
    {
        if (disposed)
        {
            return;
        }
        state = new NearbyState(
            NearbyModes.FromPolicy(Value(NearbyModes.Key, NearbyModes.PolicyValue)),
            NearbyModes.SaveTo(Value(NearbyModes.Key, NearbyModes.SaveValue), DownloadsFolder()),
            DeviceName(),
            problem,
            NearbyModes.Remembered(Value(NearbyModes.RememberedKey, NearbyModes.PolicyValue)));
        changes?.Add(NearbyNews.State);
    }

    private static object? Value(string key, string name)
    {
        using var opened = Registry.CurrentUser.OpenSubKey(key);
        return opened?.GetValue(name);
    }

    private static void Write(string key, string name, int value)
    {
        using var opened = Registry.CurrentUser.CreateSubKey(key, writable: true);
        opened.SetValue(name, value, RegistryValueKind.DWord);
    }

    /// <summary>The Downloads folder, wherever the person moved it.</summary>
    private static string DownloadsFolder()
    {
        char* path = null;
        var id = Downloads;
        try
        {
            if (SHGetKnownFolderPath(&id, 0, HANDLE.NULL, &path).SUCCEEDED && path != null)
            {
                return new string(path);
            }
        }
        finally
        {
            CoTaskMemFree(path);
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); // not-ui: the folder's own name
    }

    /// <summary>The PC's name as other devices find it: its host name, in the case it was given.</summary>
    private static string DeviceName()
    {
        try
        {
            return System.Net.Dns.GetHostName();
        }
        catch (System.Net.Sockets.SocketException)
        {
            return Environment.MachineName;
        }
    }
}

/// <summary>A made-up PC: what the tests and the pictures use. It writes nothing and opens no dialog.</summary>
internal sealed class SampleNearby : INearbySource
{
    private Changes? changes;

    public SampleNearby(NearbyMode? mode = NearbyMode.MyDevices, string saveTo = @"C:\Users\Alex\Downloads", string device = "ALEX-LAPTOP", string? problem = null) // not-ui: a sample PC's folder and name
    {
        Mode = mode;
        Folder = saveTo;
        Device = device;
        Problem = problem;
        if (mode is NearbyMode.MyDevices or NearbyMode.Everyone)
        {
            Last = mode.Value;
        }
    }

    public NearbyMode? Mode { get; private set; }

    public string Folder { get; private set; }

    public string Device { get; }

    public string? Problem { get; }

    /// <summary>Every mode it was asked for, in order.</summary>
    public List<NearbyMode> Asked { get; } = [];

    /// <summary>How often the folder dialog was asked for.</summary>
    public int Choosing { get; private set; }

    public void Start(Changes news) => changes = news;

    /// <summary>The mode turning it on brings back.</summary>
    public NearbyMode Last { get; set; } = NearbyMode.MyDevices;

    public NearbyState Read() => new(Mode, Folder, Device, Problem, Last);

    public void Set(NearbyMode mode)
    {
        Asked.Add(mode);
        Mode = mode;
        if (mode != NearbyMode.Off)
        {
            Last = mode;
        }
        changes?.Add(NearbyNews.State);
    }

    public void SaveTo(string folder)
    {
        Folder = folder;
        changes?.Add(NearbyNews.State);
    }

    public void ChooseFolder() => Choosing++;

    public void Dispose()
    {
    }
}
