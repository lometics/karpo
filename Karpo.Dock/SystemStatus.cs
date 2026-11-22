// The network, volume and Bluetooth state the Windows 11 taskbar shows on its
// system button, which Karpo's Wi-Fi, sound and Bluetooth faces show a glyph
// each of (StatusFaceDrawing, SystemGlyph), kept current by the events each source raises,
// never by polling: the network list manager, WLAN signal notifications, the
// audio endpoint's volume and default-device notifications, and the radios'
// own events. The battery's glyph and line are worked out here too (Describe),
// for the battery widget, which follows the power broadcasts itself.
//
// Callbacks come on the system's threads. They only mark what is stale and wake
// a message-only window of this class's own, which folds a burst (a volume
// slider being dragged) into one WM_KARPO_SYSTEM for the dock, at most one per
// 50 ms. The state is read again on the dock's thread, when the dock asks for
// it. Like the tray spy, nothing here runs dock code from a callback, and a
// WM_KARPO_SYSTEM the dock drops (busy in a menu) only delays the next one.
//
// Glyphs are Segoe Fluent Icons, checked by rendering them: Wi-Fi bars
// E872 E873 E874 E701 (with a warning: EB60 to EB63), Ethernet E839 (EB56),
// no connection F384, volume E992 to E995, muted E74F, battery E850 to E859
// and E83F, charging E85A to E862, E83E, EA93, saver E863 to E86B, EA94, EA95.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;
// Aliased: TerraFX has C enums named RadioKind and RadioState too.
using Foundation = Windows.Foundation;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock;

/// <summary>
/// What the Wi-Fi, sound and Bluetooth widgets show. <c>NetworkBackGlyph</c>, when set, is
/// drawn under <c>NetworkGlyph</c> at reduced opacity: Windows 11 shows the Wi-Fi arcs a weak
/// signal doesn't reach that way. <c>BluetoothGlyph</c> is null without a Bluetooth radio; a
/// radio that is off (<c>BluetoothOn</c> false) is drawn dimmed. <c>NetworkTip</c> and
/// <c>VolumeTip</c> are the Wi-Fi and sound widgets' tooltips. <c>NetworkBadge</c>, when set, is
/// drawn over <c>NetworkGlyph</c> at the same size and place: the shield Windows puts on its network
/// icon while a VPN is connected, whose names <c>Vpn</c> holds.
/// </summary>
public readonly record struct SystemState(
    string NetworkGlyph,
    string VolumeGlyph,
    string? NetworkBackGlyph = null,
    string? BluetoothGlyph = null,
    bool BluetoothOn = false, string NetworkTip = "", string VolumeTip = "",
    string? NetworkBadge = null, string? Vpn = null);

public sealed unsafe class SystemStatus : IDisposable
{
    /// <summary>Posted to the dock when <see cref="State"/> may have changed.</summary>
    public const uint WM_KARPO_SYSTEM = 0x8000 + 4;

    private const uint WM_WAKE = 0x8000 + 1;
    private const nuint BatchTimer = 1;
    private const uint BatchMs = 50;
    private const nuint RadiosTimer = 2;
    private const uint RadiosDelayMs = 1000;

    internal const int NetworkPart = 1;
    internal const int VolumePart = 2;
    internal const int DevicePart = 4;
    internal const int BluetoothPart = 16;
    internal const int VpnPart = 32;
    private const int AllParts = NetworkPart | VolumePart | DevicePart | BluetoothPart | VpnPart;

    private const string BluetoothRune = "\uE702";

    private const string NetworkOffline = "\uF384";
    private const string Ethernet = "\uE839";
    private const string EthernetWarning = "\uEB56";
    private const string Cellular = "\uE908";
    private const string Muted = "\uE74F";
    private const string BatteryUnknown = "\uE996";
    private static readonly string[] WifiBars = ["\uE872", "\uE873", "\uE874", "\uE701"];
    private static readonly string[] WifiWarningBars = ["\uEB60", "\uEB61", "\uEB62", "\uEB63"];

    // The VPN's shield, and the network glyphs Windows draws under it: NetworkIcon.dll's
    // NetworkIconVPNOverlay and the "TopLeftCutout" forms its table pairs with it (Wifi2Bars- to
    // Wifi4BarsTopLeftCutout, EthernetTopLeftCutout), read from that table and checked by rendering
    // them. The shield is in the top-left of the em square, and is drawn at the network glyph's own
    // size and place; each cutout is its glyph with the room the shield and its gap take cut away.
    internal const string VpnBadge = "\uF8C0";
    private static readonly string[] WifiBarsUnderVpn = ["\uE872", "\uF8C2", "\uF8C3", "\uF8C4"];
    private const string EthernetUnderVpn = "\uF8CC";
    private static readonly string[] VolumeLevels = ["\uE992", "\uE993", "\uE994", "\uE995"];
    private static readonly string[] BatteryLevels =
        ["\uE850", "\uE851", "\uE852", "\uE853", "\uE854", "\uE855", "\uE856", "\uE857", "\uE858", "\uE859", "\uE83F"];
    private static readonly string[] ChargingLevels =
        ["\uE85A", "\uE85B", "\uE85C", "\uE85D", "\uE85E", "\uE85F", "\uE860", "\uE861", "\uE862", "\uE83E", "\uEA93"];
    private static readonly string[] SaverLevels =
        ["\uE863", "\uE864", "\uE865", "\uE866", "\uE867", "\uE868", "\uE869", "\uE86A", "\uE86B", "\uEA94", "\uEA95"];

    private static readonly Guid NetworkListManagerClass = new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");
    private static readonly Guid NetworkListManagerEvents = new("DCB00001-570F-4A9B-8D69-199FDBA5723B");
    private static readonly Guid NetworkConnectionEvents = new("DCB00007-570F-4A9B-8D69-199FDBA5723B");

    // PKEY_Device_FriendlyName, which TerraFX does not carry.
    private static readonly PROPERTYKEY DeviceFriendlyName = new()
    {
        fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        pid = 14,
    };

    // Callbacks on any thread write these, so they are statics behind
    // Interlocked; there is one SystemStatus at a time.
    internal static int stale;
    private static int woken;
    internal static HWND listener;
    internal static HWND target;
    private static int shownBars = -1;
    private static int shownVolume = -1;

    // The listener's timer is set. Only its window procedure uses it.
    private static bool batching;

    // Bluetooth, from the radio callbacks: -1 no radio (or not known yet), 0 off, 1 on.
    private static int bluetoothCode = -1;

    // The Bluetooth radios being followed. Swapped whole under the lock and read
    // without it; no radio's event is added or removed while the lock is held,
    // since a radio may hold its own lock while it calls back.
    private static readonly object RadiosLock = new();
    private static Radios.Radio[] bluetoothRadios = [];
    private static int radioGeneration;
    private static readonly Foundation.TypedEventHandler<Radios.Radio, object> RadioChanged =
        static (_, _) => SummarizeRadios();

    private INetworkListManager* networks;
    private IConnectionPoint* listPoint;
    private uint listCookie;
    private IConnectionPoint* connectionPoint;
    private uint connectionCookie;
    private IMMDeviceEnumerator* devices;
    private IAudioEndpointVolume* volume;
    private Sink* listSink;
    private Sink* connectionSink;
    private Sink* deviceSink;
    private Sink* volumeSink;
    private HANDLE wlan;
    private HDEVNOTIFY bluetoothPorts;
    private string? deviceName;
    private Connection connection;
    private Audio audio;
    private SystemState state;

    // The language the hovers in state were written in: another one writes them again, with nothing re-read.
    private string? composedIn;
    private bool disposed;

    // The VPN connections, through RAS as the VPN switch reads them (QuickSettings\Services\Vpn.cs),
    // for the network icon's shield; null where this run may not read the person's world.
    private QuickSettings.Services.IVpnSource? vpn;
    private string? vpnNames;

    private SystemStatus(HWND dock)
    {
        target = dock;
        Interlocked.Exchange(ref stale, AllParts);
        Interlocked.Exchange(ref woken, 0);
        Interlocked.Exchange(ref shownBars, -1);
        Interlocked.Exchange(ref shownVolume, -1);
        Interlocked.Exchange(ref bluetoothCode, -1);
        batching = false;
        CreateListener();
        WatchNetwork();
        OpenWlan();
        WatchAudio();
        WatchBluetooth();
        WatchVpn();
        state = State;
    }

    internal enum Medium
    {
        None,
        Other,
        Cellular,
        WiFi,

        // Preferred, as Windows prefers a cable.
        Ethernet,
    }

    /// <summary>
    /// Starts listening. <paramref name="dock"/> is where
    /// <see cref="WM_KARPO_SYSTEM"/> goes; call it on the dock's thread, which
    /// must pump messages and be a COM apartment.
    /// </summary>
    public static SystemStatus Start(HWND dock) => new(dock);

    /// <summary>Read again only in the parts some event marked stale.</summary>
    public SystemState State
    {
        get
        {
            int parts = Interlocked.Exchange(ref stale, 0);
            if ((parts == 0 && composedIn == Karpo.Core.Strings.Language) || disposed)
            {
                return state;
            }
            if ((parts & NetworkPart) != 0)
            {
                connection = ReadConnection();
            }
            if ((parts & DevicePart) != 0)
            {
                AttachDevice();
            }
            if ((parts & (VolumePart | DevicePart)) != 0)
            {
                audio = ReadAudio();
            }
            if ((parts & VpnPart) != 0)
            {
                vpnNames = VpnNames(vpn?.Connections);
            }
            state = Compose(connection, audio, Volatile.Read(ref bluetoothCode), vpnNames);
            composedIn = Karpo.Core.Strings.Language;
            return state;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        target = HWND.NULL;
        vpn?.Dispose();
        vpn = null;
        if (bluetoothPorts != HDEVNOTIFY.NULL)
        {
            UnregisterDeviceNotification(bluetoothPorts);
            bluetoothPorts = HDEVNOTIFY.NULL;
        }
        ForgetRadios();
        if (wlan != HANDLE.NULL)
        {
            // Waits for a callback in flight, which only posts a message.
            WlanCloseHandle(wlan, null);
            wlan = HANDLE.NULL;
        }
        DetachDevice();
        if (devices != null)
        {
            devices->UnregisterEndpointNotificationCallback((IMMNotificationClient*)deviceSink);
            devices->Release();
            devices = null;
        }
        Unadvise(ref listPoint, listCookie);
        Unadvise(ref connectionPoint, connectionCookie);
        if (networks != null)
        {
            networks->Release();
            networks = null;
        }
        // COM may still hold a sink for a moment; each frees itself at its last release.
        Sink.Drop(listSink);
        Sink.Drop(connectionSink);
        Sink.Drop(deviceSink);
        Sink.Drop(volumeSink);
        listSink = connectionSink = deviceSink = volumeSink = null;
        if (listener != HWND.NULL)
        {
            Surfaces.Gone(listener);
            listener = HWND.NULL;
        }
    }

    // --- Waking the dock ----------------------------------------------------

    /// <summary>From any thread: marks <paramref name="parts"/> stale and wakes the listener once.</summary>
    internal static void Wake(int parts)
    {
        Interlocked.Or(ref stale, parts);
        if (Interlocked.Exchange(ref woken, 1) != 0)
        {
            return;
        }
        var window = listener;
        if (window == HWND.NULL || !PostMessageW(window, WM_WAKE, default, default))
        {
            Interlocked.Exchange(ref woken, 0);
        }
    }

    private static void CreateListener()
    {
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* name = "karpo.system")
        {
            var type = new WNDCLASSW { lpfnWndProc = &ListenerProcedure, hInstance = instance, lpszClassName = name };
            // Fails when an earlier instance registered it, which is fine: same procedure.
            RegisterClassW(&type);
            // Message-only: posted messages and power notifications reach it,
            // broadcasts don't.
            listener = CreateWindowExW(0, name, null, 0, 0, 0, 0, 0, HWND.HWND_MESSAGE, HMENU.NULL, instance, null);
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT ListenerProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // Nothing thrown may unwind into user32, where it ends the process.
        try
        {
            return Listen(hwnd, msg, wParam, lParam);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("System status: a message", error);
            return 0;
        }
    }

    private static LRESULT Listen(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM_WAKE:
                Interlocked.Exchange(ref woken, 0);
                Batch(hwnd);
                return 0;
            case WM.WM_TIMER when (nuint)wParam == BatchTimer:
                KillTimer(hwnd, BatchTimer);
                batching = false;
                Notify();
                return 0;
            case WM.WM_DEVICECHANGE:
                if ((uint)(nuint)wParam is DBT_DEVICEARRIVAL or DBT_DEVICEREMOVECOMPLETE)
                {
                    // A Bluetooth port came or went. The radio list may lag
                    // behind the port, and a dongle brings a burst of these, so
                    // one look a moment later serves both (the timer restarts).
                    SetTimer(hwnd, RadiosTimer, RadiosDelayMs, null);
                }
                return 1;
            case WM.WM_TIMER when (nuint)wParam == RadiosTimer:
                KillTimer(hwnd, RadiosTimer);
                EnumerateRadios();
                return 0;
            default:
                return DefWindowProcW(hwnd, msg, wParam, lParam);
        }
    }

    /// <summary>A throttle, not a debounce: a slider dragged for seconds still updates the icon as it goes.</summary>
    private static void Batch(HWND hwnd)
    {
        if (batching)
        {
            return;
        }
        batching = SetTimer(hwnd, BatchTimer, BatchMs, null) != 0;
        if (!batching)
        {
            Notify();
        }
    }

    private static void Notify()
    {
        var dock = target;
        if (dock != HWND.NULL)
        {
            PostMessageW(dock, WM_KARPO_SYSTEM, default, default);
        }
    }

    // --- The battery -------------------------------------------------------------

    /// <summary>The battery now; null without one.</summary>
    internal static Battery? ReadBatteryNow()
    {
        SYSTEM_POWER_STATUS status = default;
        // Flag 128 is "no system battery"; 255, "can't tell", has that bit too.
        if (!GetSystemPowerStatus(&status) || (status.BatteryFlag & 128) != 0)
        {
            return null;
        }
        int percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : -1;
        return new Battery(percent, status.ACLineStatus == 1, QuickSettings.Services.EnergySaver.Saving(status.SystemStatusFlag == 1, QuickSettings.Services.EnergySaver.Shared.On));
    }

    // --- VPN ------------------------------------------------------------------

    /// <summary>
    /// Follows the VPN connections for the network icon's shield, as Windows' own network icon
    /// wears one: RAS says when one is made or ended anywhere (Vpn.cs), on the pool, where it is read
    /// again. Not on a run that may not read the person's world (Config.IsIsolated, the samples).
    /// </summary>
    private void WatchVpn()
    {
        if (Karpo.Core.Config.IsIsolated || QuickSettings.QuickSources.UseSamples)
        {
            return;
        }
        var source = new QuickSettings.Services.RasVpn();
        vpn = source;
        // RAS talks to a service: the first read and the following are the pool's.
        _ = Task.Run(() =>
        {
            try
            {
                source.Start(static () => Wake(VpnPart));
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Note("System status: the VPN connections", error);
            }
            Wake(VpnPart);
        });
    }

    /// <summary>The VPN connections that are up, by name, as one line; null for none.</summary>
    internal static string? VpnNames(IReadOnlyList<QuickSettings.Services.VpnConnection>? connections)
    {
        var up = connections?.Where(connection => connection.State == QuickSettings.Services.VpnState.Connected).Select(connection => connection.Name).ToList();
        return up is { Count: > 0 } ? string.Join(", ", up) : null;
    }

    /// <summary>
    /// The network glyph Windows draws under the VPN's shield: its "TopLeftCutout" form where its
    /// table has one (Wi-Fi of two to four bars, the cable), the glyph itself where the shield's
    /// corner is empty anyway (Wi-Fi of one bar, cellular); null for the rest — no internet, not
    /// connected — which Windows' table pairs with no shield, so none is drawn over them.
    /// </summary>
    internal static string? UnderVpn(string glyph)
    {
        var bars = Array.IndexOf(WifiBars, glyph);
        return bars >= 0 ? WifiBarsUnderVpn[bars]
            : glyph == Ethernet ? EthernetUnderVpn
            : glyph == Cellular ? glyph
            : null;
    }

    /// <summary>
    /// The glyph a cut-away one was cut from (<see cref="UnderVpn"/> backwards), any other as it is: what
    /// the network icon is placed by with the shield on, so the arcs stay exactly where they were
    /// without it, as Windows keeps them. A cut-away's own ink is smaller, and centring it moved them.
    /// </summary>
    internal static string Uncut(string glyph)
    {
        var bars = Array.IndexOf(WifiBarsUnderVpn, glyph);
        return bars >= 0 ? WifiBars[bars] : glyph == EthernetUnderVpn ? Ethernet : glyph;
    }

    // --- Network --------------------------------------------------------------

    private void WatchNetwork()
    {
        var classId = NetworkListManagerClass;
        var interfaceId = INetworkListManager.Id;
        INetworkListManager* created = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, &interfaceId, (void**)&created).FAILED || created == null)
        {
            return;
        }
        networks = created;
        IConnectionPointContainer* container = null;
        if (((IUnknown*)networks)->QueryInterface(__uuidof<IConnectionPointContainer>(), (void**)&container).FAILED
            || container == null)
        {
            return;
        }
        try
        {
            // The overall connectivity, and each connection's: a cable plugged in
            // beside Wi-Fi changes the icon without changing the first.
            listSink = Sink.Create(Sink.NetworkListTable, NetworkListManagerEvents);
            listPoint = Advise(container, NetworkListManagerEvents, listSink, out listCookie);
            connectionSink = Sink.Create(Sink.NetworkConnectionTable, NetworkConnectionEvents);
            connectionPoint = Advise(container, NetworkConnectionEvents, connectionSink, out connectionCookie);
        }
        finally
        {
            container->Release();
        }
    }

    private static IConnectionPoint* Advise(IConnectionPointContainer* container, Guid events, Sink* sink, out uint cookie)
    {
        cookie = 0;
        IConnectionPoint* point = null;
        if (container->FindConnectionPoint(&events, &point).FAILED || point == null)
        {
            return null;
        }
        uint registered = 0;
        if (point->Advise((IUnknown*)sink, &registered).FAILED)
        {
            point->Release();
            return null;
        }
        cookie = registered;
        return point;
    }

    private static void Unadvise(ref IConnectionPoint* point, uint cookie)
    {
        if (point != null)
        {
            point->Unadvise(cookie);
            point->Release();
            point = null;
        }
    }

    /// <summary>
    /// The connection the icon stands for: one with internet access before one
    /// without, then a cable before Wi-Fi before cellular before the rest
    /// (VPNs, virtual switches).
    /// </summary>
    private Connection ReadConnection()
    {
        if (networks == null)
        {
            return default;
        }
        if (wlan == HANDLE.NULL)
        {
            // The WLAN service may have started since.
            OpenWlan();
        }
        IEnumNetworkConnections* list = null;
        if (networks->GetNetworkConnections(&list).FAILED || list == null)
        {
            return default;
        }
        var best = default(Connection);
        var bestAdapter = Guid.Empty;
        int bestRank = -1;
        try
        {
            while (true)
            {
                INetworkConnection* item = null;
                uint fetched = 0;
                if (list->Next(1, &item, &fetched).Value != S_OK || item == null)
                {
                    break;
                }
                try
                {
                    int connectivity = 0;
                    var adapter = Guid.Empty;
                    if (item->GetConnectivity(&connectivity).FAILED || connectivity == 0
                        || item->GetAdapterId(&adapter).FAILED)
                    {
                        continue;
                    }
                    var medium = MediumOf(adapter);
                    bool internet = (connectivity & (IPv4Internet | IPv6Internet)) != 0;
                    int rank = (internet ? 10 : 0) + (int)medium;
                    if (rank > bestRank)
                    {
                        bestRank = rank;
                        bestAdapter = adapter;
                        best = new Connection(medium, internet, NameOf(item), 0);
                    }
                }
                finally
                {
                    item->Release();
                }
            }
        }
        finally
        {
            list->Release();
        }
        if (best.Medium == Medium.WiFi)
        {
            int bars = Bars(SignalOf(bestAdapter));
            Interlocked.Exchange(ref shownBars, bars);
            best = best with { Bars = bars };
        }
        return best;
    }

    internal static Medium MediumOf(Guid adapter)
    {
        InterfaceRow row = default;
        if (ConvertInterfaceGuidToLuid(&adapter, &row.Luid) != NO_ERROR || GetIfEntry2(&row) != NO_ERROR)
        {
            return Medium.Other;
        }
        if (row.Type == IF_TYPE_IEEE80211 || row.PhysicalMediumType is NdisPhysicalMediumWirelessLan or NdisPhysicalMediumNative802_11)
        {
            return Medium.WiFi;
        }
        if (row.Type is IF_TYPE_WWANPP or IF_TYPE_WWANPP2 || row.PhysicalMediumType == NdisPhysicalMediumWirelessWan)
        {
            return Medium.Cellular;
        }
        // A VPN or a virtual switch is Ethernet by type too; only hardware is a cable.
        return row.Type == IF_TYPE_ETHERNET_CSMACD && (row.Flags & HardwareInterface) != 0 ? Medium.Ethernet : Medium.Other;
    }

    private static string? NameOf(INetworkConnection* item)
    {
        INetwork* network = null;
        if (item->GetNetwork(&network).FAILED || network == null)
        {
            return null;
        }
        try
        {
            char* name = null;
            if (network->GetName(&name).FAILED || name == null)
            {
                return null;
            }
            try
            {
                return new string(name, 0, (int)SysStringLen(name));
            }
            finally
            {
                SysFreeString(name);
            }
        }
        finally
        {
            network->Release();
        }
    }

    private void OpenWlan()
    {
        uint negotiated = 0;
        HANDLE client = HANDLE.NULL;
        // Fails without the WLAN service, which means no Wi-Fi to show.
        if (WlanOpenHandle(2, null, &negotiated, &client) != NO_ERROR)
        {
            return;
        }
        wlan = client;
        uint previous = 0;
        WlanRegisterNotification(wlan, WLAN_NOTIFICATION_SOURCE_MSM, BOOL.FALSE, &WlanNotified, null, null, &previous);
    }

    internal uint? SignalOf(Guid adapter)
    {
        if (wlan == HANDLE.NULL)
        {
            return null;
        }
        uint size = 0;
        void* data = null;
        int valueType = 0;
        if (WlanQueryInterface(wlan, &adapter, WlanCurrentConnection, null, &size, &data, &valueType) != NO_ERROR || data == null)
        {
            return null;
        }
        try
        {
            var current = (WlanConnection*)data;
            return size >= sizeof(WlanConnection) && current->State == WlanConnected ? current->SignalQuality : null;
        }
        finally
        {
            WlanFreeMemory(data);
        }
    }

    /// <summary>
    /// Wi-Fi bars for a WLAN signal quality (0 to 100, linear in dBm from -100
    /// to -50). An even split: Windows' own table is not published. No reading
    /// shows full bars rather than an alarming empty fan.
    /// </summary>
    internal static int Bars(uint? quality) => quality switch
    {
        null => 4,
        >= 75 => 4,
        >= 50 => 3,
        >= 25 => 2,
        _ => 1,
    };

    [UnmanagedCallersOnly]
    private static void WlanNotified(WlanNotification* data, void* context)
    {
        // Nothing may escape into the WLAN service's callback.
        try
        {
            WlanHeard(data);
        }
        catch (Exception error)
        {
            Karpo.Core.Troubles.Report("System status: a Wi-Fi notification", error);
        }
    }

    private static void WlanHeard(WlanNotification* data)
    {
        if (data == null || data->Source != WLAN_NOTIFICATION_SOURCE_MSM)
        {
            return;
        }
        switch (data->Code)
        {
            case MsmSignalQualityChange:
                // Most of these don't move a bar, and cost nothing then.
                if (data->DataSize >= sizeof(uint) && data->Data != null)
                {
                    int bars = Bars(*(uint*)data->Data);
                    if (Interlocked.Exchange(ref shownBars, bars) == bars)
                    {
                        return;
                    }
                }
                Wake(NetworkPart);
                return;
            case MsmConnected:
            case MsmRoamingEnd:
            case MsmRadioStateChange:
            case MsmDisconnected:
            case MsmAdapterRemoval:
                Wake(NetworkPart);
                return;
        }
    }

    // --- Bluetooth ------------------------------------------------------------

    /// <summary>
    /// Reads the radios now and again whenever a Bluetooth port comes or goes;
    /// each radio then reports its own switching on and off.
    /// </summary>
    private void WatchBluetooth()
    {
        EnumerateRadios();
        if (listener == HWND.NULL)
        {
            return;
        }
        var filter = new DeviceInterfaceFilter
        {
            Size = (uint)sizeof(DeviceInterfaceFilter),
            DeviceType = DBT_DEVTYP_DEVICEINTERFACE,
            ClassGuid = BluetoothPortInterface,
        };
        bluetoothPorts = RegisterDeviceNotificationW((HANDLE)listener.Value, &filter, (uint)DEVICE.DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    /// <summary>
    /// Asynchronous: the radio list arrives on a pool thread, which then follows
    /// the Bluetooth radios in it and wakes the dock if that changed anything.
    /// </summary>
    internal static void EnumerateRadios()
    {
        int generation;
        lock (RadiosLock)
        {
            generation = ++radioGeneration;
        }
        try
        {
            Radios.Radio.GetRadiosAsync().AsTask().ContinueWith(
                static (task, state) =>
                {
                    try
                    {
                        if (task.IsCompletedSuccessfully)
                        {
                            TakeRadios((int)state!, task.Result);
                        }
                        else
                        {
                            // Observed, so a failed look is not reported as unobserved.
                            _ = task.Exception;
                        }
                    }
                    catch (Exception error)
                    {
                        Karpo.Core.Troubles.Report("System status: taking the radios", error);
                    }
                },
                generation,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch (Exception)
        {
            // No radio stack at all: Bluetooth stays as it was.
        }
    }

    private static void TakeRadios(int generation, IReadOnlyList<Radios.Radio> found)
    {
        var bluetooth = new List<Radios.Radio>();
        foreach (var radio in found)
        {
            try
            {
                if (radio.Kind == Radios.RadioKind.Bluetooth)
                {
                    bluetooth.Add(radio);
                }
            }
            catch (Exception)
            {
                // Gone while being read.
            }
        }
        Radios.Radio[] previous;
        lock (RadiosLock)
        {
            // A later look, or Dispose, has the say.
            if (generation != radioGeneration)
            {
                return;
            }
            previous = bluetoothRadios;
            bluetoothRadios = [.. bluetooth];
        }
        foreach (var radio in previous)
        {
            Follow(radio, false);
        }
        foreach (var radio in bluetooth)
        {
            Follow(radio, true);
        }
        SummarizeRadios();
    }

    private static void ForgetRadios()
    {
        Radios.Radio[] previous;
        lock (RadiosLock)
        {
            radioGeneration++;
            previous = bluetoothRadios;
            bluetoothRadios = [];
        }
        foreach (var radio in previous)
        {
            Follow(radio, false);
        }
        Interlocked.Exchange(ref bluetoothCode, -1);
    }

    private static void Follow(Radios.Radio radio, bool on)
    {
        try
        {
            if (on)
            {
                radio.StateChanged += RadioChanged;
            }
            else
            {
                radio.StateChanged -= RadioChanged;
            }
        }
        catch (Exception)
        {
            // A radio that went away takes its event with it.
        }
    }

    /// <summary>On whatever thread a radio reports from: on when any Bluetooth radio is.</summary>
    private static void SummarizeRadios()
    {
        var radios = Volatile.Read(ref bluetoothRadios);
        int code = radios.Length == 0 ? -1 : 0;
        foreach (var radio in radios)
        {
            try
            {
                if (radio.State == Radios.RadioState.On)
                {
                    code = 1;
                    break;
                }
            }
            catch (Exception)
            {
                // Gone while being read: counts as off.
            }
        }
        if (Interlocked.Exchange(ref bluetoothCode, code) != code)
        {
            Wake(BluetoothPart);
        }
    }

    // --- Audio ----------------------------------------------------------------

    private void WatchAudio()
    {
        var classId = CLSID.CLSID_MMDeviceEnumerator;
        IMMDeviceEnumerator* created = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IMMDeviceEnumerator>(), (void**)&created).FAILED
            || created == null)
        {
            return;
        }
        devices = created;
        deviceSink = Sink.Create(Sink.DeviceTable, *(Guid*)__uuidof<IMMNotificationClient>());
        volumeSink = Sink.Create(Sink.VolumeTable, *(Guid*)__uuidof<IAudioEndpointVolumeCallback>());
        devices->RegisterEndpointNotificationCallback((IMMNotificationClient*)deviceSink);
    }

    /// <summary>Follows the default output device, which is what the taskbar's volume shows.</summary>
    private void AttachDevice()
    {
        DetachDevice();
        deviceName = null;
        if (devices == null)
        {
            return;
        }
        IMMDevice* device = null;
        if (devices->GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, &device).FAILED || device == null)
        {
            return;
        }
        try
        {
            deviceName = FriendlyName(device);
            IAudioEndpointVolume* endpoint = null;
            if (device->Activate(__uuidof<IAudioEndpointVolume>(), (uint)CLSCTX_ALL, null, (void**)&endpoint).SUCCEEDED
                && endpoint != null)
            {
                volume = endpoint;
                volume->RegisterControlChangeNotify((IAudioEndpointVolumeCallback*)volumeSink);
            }
        }
        finally
        {
            device->Release();
        }
    }

    private void DetachDevice()
    {
        if (volume != null)
        {
            volume->UnregisterControlChangeNotify((IAudioEndpointVolumeCallback*)volumeSink);
            volume->Release();
            volume = null;
        }
    }

    private Audio ReadAudio()
    {
        if (volume == null)
        {
            return default;
        }
        float level = 0;
        BOOL muted = BOOL.FALSE;
        volume->GetMasterVolumeLevelScalar(&level);
        volume->GetMute(&muted);
        int percent = Percent(level);
        Interlocked.Exchange(ref shownVolume, VolumeCode(percent, muted));
        return new Audio(true, deviceName, percent, muted);
    }

    private static string? FriendlyName(IMMDevice* device)
    {
        IPropertyStore* store = null;
        if (device->OpenPropertyStore(STGM.STGM_READ, &store).FAILED || store == null)
        {
            return null;
        }
        try
        {
            var key = DeviceFriendlyName;
            PROPVARIANT value = default;
            if (store->GetValue(&key, &value).FAILED)
            {
                return null;
            }
            char* text = null;
            var converted = PropVariantToStringAlloc(&value, &text);
            PropVariantClear(&value);
            return converted.SUCCEEDED ? ComUtil.FromTaskMem(text) : null;
        }
        finally
        {
            store->Release();
        }
    }

    internal static int Percent(float level) =>
        (int)MathF.Round(Math.Clamp(level, 0f, 1f) * 100f, MidpointRounding.AwayFromZero);

    private static int VolumeCode(int percent, bool muted) => muted ? -2 : percent;

    // --- The picture ----------------------------------------------------------

    /// <summary>
    /// <paramref name="bluetooth"/>: -1 no radio, 0 off, 1 on. <paramref name="vpn"/>: the VPN connections
    /// that are up, by name, or null for none: the network icon then wears Windows' shield over the
    /// glyph Windows draws under it (<see cref="UnderVpn"/>), and its hover names them.
    /// </summary>
    internal static SystemState Compose(Connection network, Audio sound, int bluetooth = -1, string? vpn = null)
    {
        var lines = new List<string>(3);
        string networkGlyph;
        string? back = null;
        switch (network.Medium)
        {
            case Medium.None:
                networkGlyph = NetworkOffline;
                lines.Add(S.DockStatusNotConnected);
                break;
            case Medium.WiFi:
                int bars = Math.Clamp(network.Bars, 1, 4) - 1;
                var set = network.Internet ? WifiBars : WifiWarningBars;
                networkGlyph = set[bars];
                back = bars < 3 ? set[3] : null;
                lines.Add(network.Name ?? S.DockStatusWifi);
                break;
            case Medium.Cellular:
                networkGlyph = Cellular;
                lines.Add(network.Name ?? S.DockStatusCellular);
                break;
            default:
                networkGlyph = network.Internet ? Ethernet : EthernetWarning;
                lines.Add(network.Name ?? S.DockStatusNetwork);
                break;
        }
        if (network.Medium != Medium.None)
        {
            lines.Add(network.Internet ? S.DockStatusInternet : S.DockStatusNoInternet);
        }
        // A VPN connected: the shield, in Windows' place over the glyph cut away for it, where Windows
        // has the pair; the hover, and so what Narrator says, names it either way.
        string? badge = null;
        if (vpn is { Length: > 0 })
        {
            lines.Add(S.DockStatusVpn(vpn));
            if (UnderVpn(networkGlyph) is { } under && (back is null || UnderVpn(back) is not null))
            {
                networkGlyph = under;
                back = back is null ? null : UnderVpn(back);
                badge = VpnBadge;
            }
        }
        var networkTip = string.Join('\n', lines);

        string volumeGlyph;
        string volumeTip;
        if (!sound.Present)
        {
            volumeGlyph = Muted;
            volumeTip = S.DockStatusNoAudio;
        }
        else
        {
            volumeGlyph = sound.Muted ? Muted : VolumeLevels[sound.Percent switch
            {
                <= 0 => 0,
                <= 33 => 1,
                <= 66 => 2,
                _ => 3,
            }];
            volumeTip = sound.Muted ? S.DockStatusMuted(sound.Name ?? S.DockQuickSoundSpeakers) : S.DockStatusVolume(sound.Name ?? S.DockQuickSoundSpeakers, sound.Percent);
        }

        return new SystemState(
            networkGlyph,
            volumeGlyph,
            back,
            bluetooth >= 0 ? BluetoothRune : null,
            bluetooth == 1, networkTip, volumeTip,
            badge, vpn is { Length: > 0 } ? vpn : null);
    }

    /// <summary>
    /// The battery's glyph (charging, saver or plain, in tenths) and its line, as the taskbar's
    /// system button shows them, for the battery widget.
    /// </summary>
    internal static (string Glyph, string Tip) Describe(Battery charge)
    {
        if (charge.Percent < 0)
        {
            return (BatteryUnknown, S.DockStatusBatteryUnknown);
        }
        int level = Math.Min(10, (charge.Percent + 5) / 10);
        var glyph = (charge.Plugged ? ChargingLevels : charge.Saver ? SaverLevels : BatteryLevels)[level];
        return (glyph, charge.Plugged
            ? charge.Percent >= 100
                ? S.DockStatusBatteryFull
                : S.DockStatusBatteryCharging(charge.Percent)
            : S.DockStatusBatteryRemaining(charge.Percent));
    }

    internal readonly record struct Connection(Medium Medium, bool Internet, string? Name, int Bars);

    internal readonly record struct Audio(bool Present, string? Name, int Percent, bool Muted);

    internal readonly record struct Battery(int Percent, bool Plugged, bool Saver);

    // --- Callback objects -----------------------------------------------------

    /// <summary>
    /// A COM object implementing one callback interface, whose methods only mark
    /// something stale. Built by hand, a table of unmanaged function pointers
    /// over native memory, so native AOT needs no COM interop; free-threaded,
    /// since audio and network callbacks come on the system's threads.
    /// </summary>
    internal struct Sink
    {
        public static readonly void** NetworkListTable = Build(new void*[]
        {
            (void*)(delegate* unmanaged<Sink*, Guid*, void**, int>)&QueryInterface,
            (void*)(delegate* unmanaged<Sink*, uint>)&AddRef,
            (void*)(delegate* unmanaged<Sink*, uint>)&Release,
            (void*)(delegate* unmanaged<Sink*, int, int>)&ConnectivityChanged,
        });

        public static readonly void** NetworkConnectionTable = Build(new void*[]
        {
            (void*)(delegate* unmanaged<Sink*, Guid*, void**, int>)&QueryInterface,
            (void*)(delegate* unmanaged<Sink*, uint>)&AddRef,
            (void*)(delegate* unmanaged<Sink*, uint>)&Release,
            (void*)(delegate* unmanaged<Sink*, Guid, int, int>)&ConnectionConnectivityChanged,
            (void*)(delegate* unmanaged<Sink*, Guid, int, int>)&ConnectionPropertyChanged,
        });

        public static readonly void** VolumeTable = Build(new void*[]
        {
            (void*)(delegate* unmanaged<Sink*, Guid*, void**, int>)&QueryInterface,
            (void*)(delegate* unmanaged<Sink*, uint>)&AddRef,
            (void*)(delegate* unmanaged<Sink*, uint>)&Release,
            (void*)(delegate* unmanaged<Sink*, AUDIO_VOLUME_NOTIFICATION_DATA*, int>)&VolumeNotified,
        });

        public static readonly void** DeviceTable = Build(new void*[]
        {
            (void*)(delegate* unmanaged<Sink*, Guid*, void**, int>)&QueryInterface,
            (void*)(delegate* unmanaged<Sink*, uint>)&AddRef,
            (void*)(delegate* unmanaged<Sink*, uint>)&Release,
            (void*)(delegate* unmanaged<Sink*, char*, uint, int>)&DeviceStateChanged,
            (void*)(delegate* unmanaged<Sink*, char*, int>)&DeviceAdded,
            (void*)(delegate* unmanaged<Sink*, char*, int>)&DeviceRemoved,
            (void*)(delegate* unmanaged<Sink*, EDataFlow, ERole, char*, int>)&DefaultDeviceChanged,
            (void*)(delegate* unmanaged<Sink*, char*, PROPERTYKEY, int>)&PropertyValueChanged,
        });

        public void** Table;
        public int References;
        public Guid Interface;

        /// <summary>A new sink holding one reference, the caller's.</summary>
        public static Sink* Create(void** table, Guid iid)
        {
            var sink = (Sink*)NativeMemory.AllocZeroed((nuint)sizeof(Sink));
            sink->Table = table;
            sink->References = 1;
            sink->Interface = iid;
            return sink;
        }

        /// <summary>Gives up the caller's reference.</summary>
        public static void Drop(Sink* sink)
        {
            if (sink != null)
            {
                Decrement(sink);
            }
        }

        private static void** Build(void*[] entries)
        {
            var table = (void**)NativeMemory.Alloc((nuint)(entries.Length * sizeof(void*)));
            for (int i = 0; i < entries.Length; i++)
            {
                table[i] = entries[i];
            }
            return table;
        }

        private static uint Decrement(Sink* self)
        {
            int left = Interlocked.Decrement(ref self->References);
            if (left == 0)
            {
                NativeMemory.Free(self);
            }
            return (uint)left;
        }

        [UnmanagedCallersOnly]
        private static int QueryInterface(Sink* self, Guid* iid, void** result)
        {
            if (result == null)
            {
                return E.E_POINTER;
            }
            if (iid != null && (*iid == IID.IID_IUnknown || *iid == self->Interface))
            {
                Interlocked.Increment(ref self->References);
                *result = self;
                return S_OK;
            }
            *result = null;
            return E.E_NOINTERFACE;
        }

        [UnmanagedCallersOnly]
        private static uint AddRef(Sink* self) => (uint)Interlocked.Increment(ref self->References);

        [UnmanagedCallersOnly]
        private static uint Release(Sink* self) => Decrement(self);

        [UnmanagedCallersOnly]
        private static int ConnectivityChanged(Sink* self, int connectivity)
        {
            try
            {
                Wake(NetworkPart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the network", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int ConnectionConnectivityChanged(Sink* self, Guid connection, int connectivity)
        {
            try
            {
                Wake(NetworkPart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the network", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int ConnectionPropertyChanged(Sink* self, Guid connection, int flags)
        {
            try
            {
                Wake(NetworkPart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the network", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int VolumeNotified(Sink* self, AUDIO_VOLUME_NOTIFICATION_DATA* data)
        {
            try
            {
                // Balance and per-channel changes arrive here too, and change nothing drawn.
                if (data != null)
                {
                    int code = VolumeCode(Percent(data->fMasterVolume), data->bMuted);
                    if (Interlocked.Exchange(ref shownVolume, code) == code)
                    {
                        return S_OK;
                    }
                }
                Wake(VolumePart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the volume", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int DeviceStateChanged(Sink* self, char* id, uint newState)
        {
            try
            {
                Wake(DevicePart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the audio devices", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int DeviceAdded(Sink* self, char* id)
        {
            try
            {
                Wake(DevicePart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the audio devices", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int DeviceRemoved(Sink* self, char* id)
        {
            try
            {
                Wake(DevicePart);
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the audio devices", error);
                return S_OK;
            }
        }

        [UnmanagedCallersOnly]
        private static int DefaultDeviceChanged(Sink* self, EDataFlow flow, ERole role, char* id)
        {
            try
            {
                if (flow == EDataFlow.eRender && role == ERole.eMultimedia)
                {
                    Wake(DevicePart);
                }
                return S_OK;
            }
            catch (Exception error)
            {
                Karpo.Core.Troubles.Report("System status: Windows telling of the audio devices", error);
                return S_OK;
            }
        }

        // Every property of every device reports here, many times a second at
        // times, and none of them is drawn.
        [UnmanagedCallersOnly]
        private static int PropertyValueChanged(Sink* self, char* id, PROPERTYKEY key) => S_OK;
    }

    // --- Declarations TerraFX does not carry ------------------------------------

    private const uint WLAN_NOTIFICATION_SOURCE_MSM = 0x10;
    private const uint MsmConnected = 4;
    private const uint MsmRoamingEnd = 6;
    private const uint MsmRadioStateChange = 7;
    private const uint MsmSignalQualityChange = 8;
    private const uint MsmDisconnected = 10;
    private const uint MsmAdapterRemoval = 13;
    private const int WlanCurrentConnection = 7;
    private const int WlanConnected = 1;
    private const uint IF_TYPE_ETHERNET_CSMACD = 6;
    private const uint IF_TYPE_IEEE80211 = 71;
    private const uint IF_TYPE_WWANPP = 243;
    private const uint IF_TYPE_WWANPP2 = 244;
    private const int NdisPhysicalMediumWirelessLan = 1;
    private const int NdisPhysicalMediumWirelessWan = 8;
    private const int NdisPhysicalMediumNative802_11 = 9;
    private const byte HardwareInterface = 0x01;
    private const int IPv4Internet = 0x40;
    private const int IPv6Internet = 0x400;
    private const uint DBT_DEVICEARRIVAL = 0x8000;
    private const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const uint DBT_DEVTYP_DEVICEINTERFACE = 5;

    // GUID_BTHPORT_DEVICE_INTERFACE: the port of a Bluetooth radio.
    private static readonly Guid BluetoothPortInterface = new("0850302A-B344-4FDA-9BE9-90576B8D46F0");

    /// <summary>dbt.h's DEV_BROADCAST_DEVICEINTERFACE_W with an empty name: 32 bytes.</summary>
    internal struct DeviceInterfaceFilter
    {
        public uint Size;
        public uint DeviceType;
        public uint Reserved;
        public Guid ClassGuid;
        public char Name;
    }

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanOpenHandle(uint clientVersion, void* reserved, uint* negotiatedVersion, HANDLE* client);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanCloseHandle(HANDLE client, void* reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanRegisterNotification(
        HANDLE client,
        uint sources,
        BOOL ignoreDuplicate,
        delegate* unmanaged<WlanNotification*, void*, void> callback,
        void* context,
        void* reserved,
        uint* previousSources);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint WlanQueryInterface(
        HANDLE client,
        Guid* interfaceGuid,
        int opCode,
        void* reserved,
        uint* dataSize,
        void** data,
        int* valueType);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void WlanFreeMemory(void* memory);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint ConvertInterfaceGuidToLuid(Guid* interfaceGuid, ulong* luid);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetIfEntry2(InterfaceRow* row);

    /// <summary>wlanapi.h's WLAN_NOTIFICATION_DATA, which only wlanapi fills in.</summary>
    internal struct WlanNotification
    {
#pragma warning disable CS0649
        public uint Source;
        public uint Code;
        public Guid Interface;
        public uint DataSize;
        public void* Data;
#pragma warning restore CS0649
    }

    /// <summary>
    /// wlanapi.h's WLAN_CONNECTION_ATTRIBUTES, 604 bytes: the interface state
    /// first, and wlanAssociationAttributes.wlanSignalQuality after the 256-char
    /// profile name, the SSID, the BSS type, the BSSID and the PHY fields.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 604)]
    internal struct WlanConnection
    {
        [FieldOffset(0)]
        public int State;

        [FieldOffset(8)]
        public fixed char ProfileName[256];

        [FieldOffset(576)]
        public uint SignalQuality;
    }

    /// <summary>
    /// netioapi.h's MIB_IF_ROW2 (1352 bytes on x64), reduced to what is read
    /// here, at the offsets the performance island checks for the full struct.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 1352)]
    internal struct InterfaceRow
    {
        /// <summary>NET_LUID: a union around one ULONG64.</summary>
        [FieldOffset(0)]
        public ulong Luid;

        /// <summary>IFTYPE.</summary>
        [FieldOffset(1128)]
        public uint Type;

        /// <summary>NDIS_PHYSICAL_MEDIUM.</summary>
        [FieldOffset(1140)]
        public int PhysicalMediumType;

        /// <summary>InterfaceAndOperStatusFlags: HardwareInterface is the lowest bit.</summary>
        [FieldOffset(1152)]
        public byte Flags;
    }
}

/// <summary>
/// netlistmgr.h's INetworkListManager, which TerraFX does not carry, laid out as
/// TerraFX lays out COM interfaces. It derives from IDispatch, so its own
/// methods start at slot 7. Only the slots used here are named.
/// </summary>
internal unsafe struct INetworkListManager
{
    public static readonly Guid Id = new("DCB00000-570F-4A9B-8D69-199FDBA5723B");

    // Filled in by the native object; C# only ever reads it through a pointer.
#pragma warning disable CS0649
    public void** lpVtbl;
#pragma warning restore CS0649

    public uint Release() =>
        ((delegate* unmanaged[MemberFunction]<INetworkListManager*, uint>)lpVtbl[2])(
            (INetworkListManager*)Unsafe.AsPointer(ref this));

    public HRESULT GetNetworkConnections(IEnumNetworkConnections** list) =>
        ((delegate* unmanaged[MemberFunction]<INetworkListManager*, IEnumNetworkConnections**, int>)lpVtbl[9])(
            (INetworkListManager*)Unsafe.AsPointer(ref this), list);
}

/// <summary>netlistmgr.h's IEnumNetworkConnections (IDispatch-based).</summary>
internal unsafe struct IEnumNetworkConnections
{
#pragma warning disable CS0649
    public void** lpVtbl;
#pragma warning restore CS0649

    public uint Release() =>
        ((delegate* unmanaged[MemberFunction]<IEnumNetworkConnections*, uint>)lpVtbl[2])(
            (IEnumNetworkConnections*)Unsafe.AsPointer(ref this));

    public HRESULT Next(uint count, INetworkConnection** items, uint* fetched) =>
        ((delegate* unmanaged[MemberFunction]<IEnumNetworkConnections*, uint, INetworkConnection**, uint*, int>)lpVtbl[8])(
            (IEnumNetworkConnections*)Unsafe.AsPointer(ref this), count, items, fetched);
}

/// <summary>netlistmgr.h's INetworkConnection (IDispatch-based).</summary>
internal unsafe struct INetworkConnection
{
#pragma warning disable CS0649
    public void** lpVtbl;
#pragma warning restore CS0649

    public uint Release() =>
        ((delegate* unmanaged[MemberFunction]<INetworkConnection*, uint>)lpVtbl[2])(
            (INetworkConnection*)Unsafe.AsPointer(ref this));

    public HRESULT GetNetwork(INetwork** network) =>
        ((delegate* unmanaged[MemberFunction]<INetworkConnection*, INetwork**, int>)lpVtbl[7])(
            (INetworkConnection*)Unsafe.AsPointer(ref this), network);

    /// <summary>NLM_CONNECTIVITY flags.</summary>
    public HRESULT GetConnectivity(int* connectivity) =>
        ((delegate* unmanaged[MemberFunction]<INetworkConnection*, int*, int>)lpVtbl[10])(
            (INetworkConnection*)Unsafe.AsPointer(ref this), connectivity);

    public HRESULT GetAdapterId(Guid* adapter) =>
        ((delegate* unmanaged[MemberFunction]<INetworkConnection*, Guid*, int>)lpVtbl[12])(
            (INetworkConnection*)Unsafe.AsPointer(ref this), adapter);
}

/// <summary>netlistmgr.h's INetwork (IDispatch-based).</summary>
internal unsafe struct INetwork
{
#pragma warning disable CS0649
    public void** lpVtbl;
#pragma warning restore CS0649

    public uint Release() =>
        ((delegate* unmanaged[MemberFunction]<INetwork*, uint>)lpVtbl[2])(
            (INetwork*)Unsafe.AsPointer(ref this));

    /// <summary>A BSTR the caller frees.</summary>
    public HRESULT GetName(char** name) =>
        ((delegate* unmanaged[MemberFunction]<INetwork*, char**, int>)lpVtbl[7])(
            (INetwork*)Unsafe.AsPointer(ref this), name);

    /// <summary>NLM_NETWORK_CATEGORY: 0 public, 1 private, 2 domain authenticated (QuickSettings\Services\Wired.cs).</summary>
    public HRESULT GetCategory(int* category) =>
        ((delegate* unmanaged[MemberFunction]<INetwork*, int*, int>)lpVtbl[18])(
            (INetwork*)Unsafe.AsPointer(ref this), category);
}
