// What the Wi-Fi and Bluetooth switches' lines say beside their names (once under them, as Windows 11's quick settings
// tiles say it on their second line) (2026-10-16, the person: "the wifi and bluetooth should show some
// information about the device or network as well in the overview. name and number or names of
// connected devices for example"): the network Wi-Fi is connected to, the Bluetooth device or
// devices connected, "Not connected", or "Off".
//
// It is read from the pages' own sources (QuickSources: the Wi-Fi networks the Wi-Fi page lists, the
// paired devices the Bluetooth page lists), so a line and its page never disagree, and only while
// the line is shown — the folder open — as the pages follow theirs: started when the line is shown,
// stopped when it is not. What is said is nothing at all until the source has answered: a line
// that is briefly short, never one that is briefly wrong. Where Windows lists no networks to Karpo
// (no location access, 24H2 and later) Wi-Fi's network is not known, and nothing is said.

using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Which radio a line is.</summary>
internal enum RadioLineKind
{
    Wifi,
    Bluetooth,
}

/// <summary>The words beside a radio switch's name, followed while its line is shown (see the top of the file).</summary>
internal sealed class RadioLine(RadioLineKind kind) : IDisposable
{
    /// <summary>How long Bluetooth's devices are waited for before "Not connected" may be said: its watchers report the paired ones in a few ms.</summary>
    private const int SettleMs = 400;

    private IWifiSource? wifi;
    private IBluetoothSource? bluetooth;
    private Changes? changes;
    private volatile bool settled;
    private int started;

    internal RadioLineKind Kind => kind;

    /// <summary>Whether it is following its source.</summary>
    internal bool Following => wifi is not null || bluetooth is not null;

    /// <summary>
    /// Starts following the radio's source from <paramref name="from"/>; <paramref name="changed"/>
    /// comes from any thread when there is something new to say. Nothing where it already follows.
    /// </summary>
    public void Start(QuickSources from, Action changed)
    {
        if (Following)
        {
            return;
        }
        var made = new Changes(_ => changed());
        changes = made;
        settled = false;
        var token = Interlocked.Increment(ref started);
        if (kind == RadioLineKind.Wifi)
        {
            wifi = from.Wifi();
            wifi.Start(made);
            return;
        }
        bluetooth = from.Bluetooth();
        bluetooth.Start(made);
        _ = Task.Delay(SettleMs).ContinueWith(
            _ => Troubles.Guard("Bluetooth: the line's devices", () =>
            {
                if (Volatile.Read(ref started) == token && Following)
                {
                    settled = true;
                    changed();
                }
            }),
            TaskScheduler.Default);
    }

    /// <summary>Stops following: the source and its watchers go.</summary>
    public void Stop()
    {
        Interlocked.Increment(ref started);
        var (gone, goneToo) = (wifi, bluetooth);
        wifi = null;
        bluetooth = null;
        changes = null;
        gone?.Dispose();
        goneToo?.Dispose();
    }

    public void Dispose() => Stop();

    /// <summary>
    /// What the line says beside its name for a radio that is <paramref name="on"/>, fullest first:
    /// the forms a narrower line falls back to. Empty for nothing. On the dock's thread.
    /// </summary>
    public IReadOnlyList<string> Says(bool? on)
    {
        _ = changes?.Take();
        return kind == RadioLineKind.Wifi
            ? Wifi(on, wifi?.Read())
            : Bluetooth(on, bluetooth?.Read(), settled);
    }

    /// <summary>
    /// What Wi-Fi's line says: "Off" while the radio is off; the network it is connected to; "Not
    /// connected" where the networks are listed and none is; nothing while the source has not said or
    /// lists no networks to Karpo. Pure.
    /// </summary>
    internal static IReadOnlyList<string> Wifi(bool? on, WifiState? state)
    {
        if (on == false)
        {
            return [S.WidgetSwitchOff];
        }
        if (on is null || state is not { RadioOn: true, Access: WifiAccess.Allowed } known)
        {
            return [];
        }
        if (known.Networks.FirstOrDefault(network => network.Connected) is { } joined)
        {
            return [joined.Ssid];
        }
        // A scan that has listed nothing yet is not "nothing is connected".
        return known.Networks.Count == 0 && known.Scanning ? [] : [S.DockStatusNotConnected];
    }

    /// <summary>
    /// What Bluetooth's line says: "Off" while the radio is off; the connected devices' names, or how
    /// many where the names do not fit; "Not connected" once the devices have been looked for and none
    /// is. Pure.
    /// </summary>
    internal static IReadOnlyList<string> Bluetooth(bool? on, BluetoothState? state, bool settled)
    {
        if (on == false)
        {
            return [S.WidgetSwitchOff];
        }
        if (on is null || state is null)
        {
            return [];
        }
        var connected = state.Devices.Where(device => device.Connected).Select(device => device.Name).ToList();
        return connected.Count switch
        {
            0 => settled ? [S.DockStatusNotConnected] : [],
            1 => [connected[0]],
            _ => [string.Join(", ", connected), S.DockQuickBluetoothConnectedCount(connected.Count)],
        };
    }
}
