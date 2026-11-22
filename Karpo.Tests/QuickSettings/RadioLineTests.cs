// What the Wi-Fi and Bluetooth switches' lines say under their names (QuickSettings\Services\RadioLine.cs;
// 2026-10-16, the person: "the wifi and bluetooth should show some information about the device or
// network as well in the overview"): the network, the devices, "Not connected", "Off", or nothing while
// it is not known. On the made-up sources; nothing here reads this PC's radios.

using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class RadioLineTests
{
    private static WifiNetwork Network(string ssid, bool connected) =>
        new(ssid, System.Text.Encoding.UTF8.GetBytes(ssid), 3, WifiSecurity.Wpa2Personal, 4, connected, null);

    private static WifiState Wifi(params WifiNetwork[] networks) =>
        new(true, true, WifiAccess.Allowed, networks, Scanning: false, null, null);

    private static BluetoothDevice Device(string name, bool connected) => new(name, name, "", true, connected);

    private static BluetoothState Bluetooth(params BluetoothDevice[] devices) => new(true, true, devices, null, null);

    [Fact]
    public void WifiSaysTheNetworkItIsConnectedTo()
    {
        Assert.Equal(["Rabbit Hole"], RadioLine.Wifi(true, Wifi(Network("Café", false), Network("Rabbit Hole", true))));
        Assert.Equal([S.DockStatusNotConnected], RadioLine.Wifi(true, Wifi(Network("Café", false))));
        Assert.Equal([S.WidgetSwitchOff], RadioLine.Wifi(false, null));
    }

    /// <summary>Nothing is made up: before the source has read, while a first scan has listed nothing, and where Windows lists no networks to Karpo.</summary>
    [Fact]
    public void WifiSaysNothingItDoesNotKnow()
    {
        Assert.Empty(RadioLine.Wifi(true, null));
        Assert.Empty(RadioLine.Wifi(null, Wifi(Network("Rabbit Hole", true))));
        Assert.Empty(RadioLine.Wifi(true, new WifiState(null, false, WifiAccess.Allowed, [], true, null, null)));
        Assert.Empty(RadioLine.Wifi(true, Wifi() with { Scanning = true }));
        Assert.Empty(RadioLine.Wifi(true, Wifi(Network("Rabbit Hole", true)) with { Access = WifiAccess.Denied }));
    }

    [Fact]
    public void BluetoothSaysTheDevicesConnected()
    {
        Assert.Equal(["WH-1000XM5"], RadioLine.Bluetooth(true, Bluetooth(Device("WH-1000XM5", true), Device("Pixel 8", false)), settled: true));
        // Two or more: their names, and how many where the names do not fit.
        Assert.Equal(
            ["WH-1000XM5, MX Master 3S", S.DockQuickBluetoothConnectedCount(2)],
            RadioLine.Bluetooth(true, Bluetooth(Device("WH-1000XM5", true), Device("MX Master 3S", true), Device("Pixel 8", false)), settled: true));
        Assert.Equal([S.DockStatusNotConnected], RadioLine.Bluetooth(true, Bluetooth(Device("Pixel 8", false)), settled: true));
        Assert.Equal([S.WidgetSwitchOff], RadioLine.Bluetooth(false, Bluetooth(Device("Pixel 8", true)), settled: true));
    }

    /// <summary>"Not connected" waits for the devices to have been looked for; a device already heard is said at once.</summary>
    [Fact]
    public void BluetoothWaitsBeforeSayingNoneIsConnected()
    {
        Assert.Empty(RadioLine.Bluetooth(true, Bluetooth(), settled: false));
        Assert.Equal(["Pixel 8"], RadioLine.Bluetooth(true, Bluetooth(Device("Pixel 8", true)), settled: false));
        Assert.Empty(RadioLine.Bluetooth(null, Bluetooth(Device("Pixel 8", true)), settled: true));
    }

    /// <summary>Followed while shown: started on the sources given, read through them, and let go when stopped.</summary>
    [Fact]
    public void ItFollowsTheSourceOnlyWhileShown()
    {
        var wifi = new SampleWifi();
        var sources = new QuickSources
        {
            Wifi = () => wifi,
            Bluetooth = static () => new SampleBluetooth(),
            Battery = static () => new SampleBattery(),
            Desktops = static () => new SampleDesktopNames(),
        };
        using var line = new RadioLine(RadioLineKind.Wifi);
        Assert.Empty(line.Says(true));
        line.Start(sources, () => { });
        Assert.True(line.Following);
        Assert.Equal(["Rabbit Hole"], line.Says(true));
        line.Stop();
        Assert.False(line.Following);
        Assert.Empty(line.Says(true));
    }
}
