// Wired connections for the network flyout: every Ethernet adapter that is this
// PC's own hardware, with the network Windows has identified on it (its name,
// whether it reaches the internet, its profile) and, where they are cheap to
// read, its link speed and its IPv4 address. Read only: nothing here changes a
// connection.
//
//   * The adapters are the interface table's (GetIfTable2Ex, without its
//     statistics): Ethernet by type, backed by hardware, and not a filter's
//     second entry for one, nor a Bluetooth or wireless one that says it is
//     Ethernet. A VPN or a Hyper-V switch is Ethernet by type too, and no
//     hardware.
//   * The network on each is the Network List Manager's, by the adapter's id:
//     the name Windows' own list shows ("Network 2", a domain's name), its
//     connectivity and its category.
//   * The address is the unicast address table's, by the adapter's LUID. An
//     adapter with no address and no network on it carries nothing of this PC's
//     own (its cable is bridged into a virtual switch, or it came up a moment
//     ago and its address follows), and has no row.
//
// While the flyout is open, an interface coming up or going down (a cable
// plugged in or pulled out), an address coming or going and Windows' own
// connectivity hint changing are heard on the IP helper's threads and only
// posted on.
//
// Windows offers no Disconnect for a cable: what it has is turning the adapter
// off, which asks for an administrator. So a wired row offers its properties and
// nothing that changes it.

using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Whether a wired connection reaches the internet, as the Network List Manager says.</summary>
internal enum WiredInternet
{
    /// <summary>Windows' list could not be read: the cable is in, and that is all that is known.</summary>
    Unknown,

    /// <summary>The cable is in and Windows is still finding out what is on it.</summary>
    Identifying,

    /// <summary>A network, and no internet through it.</summary>
    NoInternet,

    Internet,
}

/// <summary>The network's profile in Windows (NLM_NETWORK_CATEGORY), and none where it is not known.</summary>
internal enum NetworkCategory
{
    Unknown,
    Public,
    Private,
    Domain,
}

/// <summary>One row of the interface table (MIB_IF_ROW2), reduced to what the flyout asks of it.</summary>
/// <param name="Flags">InterfaceAndOperStatusFlags: HardwareInterface the lowest bit, FilterInterface the next.</param>
internal readonly record struct WiredInterface(
    ulong Luid,
    Guid Id,
    string Alias,
    uint Type,
    int PhysicalMedium,
    byte Flags,
    int AdminStatus,
    int OperStatus,
    int MediaConnectState,
    ulong ReceiveSpeed,
    ulong TransmitSpeed);

/// <summary>A network the Network List Manager has on an adapter: its name, its NLM_CONNECTIVITY flags and its category.</summary>
internal readonly record struct WiredNetwork(Guid Adapter, string? Name, int Connectivity, NetworkCategory Category);

/// <summary>A wired adapter as the flyout shows it.</summary>
/// <param name="Name">The network's name in Windows (its profile), else the adapter's ("Ethernet 2").</param>
/// <param name="Adapter">The adapter's own name, its alias.</param>
/// <param name="Plugged">Whether a cable is in and the link is up.</param>
/// <param name="Speed">The link speed in bits a second; 0 where it is not known.</param>
/// <param name="Address">The IPv4 address, where it has one.</param>
internal sealed record WiredAdapter(
    Guid Id,
    string Name,
    string Adapter,
    bool Plugged,
    WiredInternet Internet,
    ulong Speed = 0,
    string? Address = null,
    NetworkCategory Category = NetworkCategory.Unknown);

internal sealed record WiredState(IReadOnlyList<WiredAdapter> Adapters);

internal interface IWiredSource : IDisposable
{
    /// <summary>Starts listening: WiredNews.Adapters follows whenever a cable, an address or the connectivity changes.</summary>
    void Start(Changes changes);

    WiredState Read();
}

internal static class WiredNews
{
    /// <summary>After the Wi-Fi page's own (WifiNews), in the same Changes.</summary>
    public const int Adapters = 8;
}

/// <summary>What a wired adapter is, and what its row says: the rules, apart from the reading.</summary>
internal static class Wired
{
    /// <summary>Windows' own page for a wired connection: its properties, its profile, its addresses.</summary>
    public const string PropertiesPage = "ms-settings:network-ethernet";

    /// <summary>The flyout's foot: Windows' Network and Internet settings.</summary>
    public const string SettingsPage = "ms-settings:network";

    // Segoe Fluent Icons: Ethernet, with a warning (no internet), with a cross (unplugged).
    public const string Glyph = "";
    public const string WarningGlyph = "";
    public const string UnpluggedGlyph = "";

    private const uint IfTypeEthernet = 6;
    private const byte HardwareInterface = 0x01;
    private const byte FilterInterface = 0x02;
    private const int AdminUp = 1;
    private const int OperUp = 1;
    private const int MediaUnknown = 0;
    private const int MediaConnected = 1;
    private const int IPv4Internet = 0x40;
    private const int IPv6Internet = 0x400;

    /// <summary>Whether <paramref name="row"/> is a cable's: Ethernet, this PC's hardware, turned on, and not a wireless adapter that says it is Ethernet.</summary>
    public static bool IsCable(in WiredInterface row) =>
        row.Type == IfTypeEthernet
        && (row.Flags & (HardwareInterface | FilterInterface)) == HardwareInterface
        && row.AdminStatus == AdminUp
        && !Wireless(row.PhysicalMedium);

    /// <summary>
    /// NDIS_PHYSICAL_MEDIUM's wireless ones: a wireless LAN or WAN, Bluetooth's network, WiMAX,
    /// ultra-wideband, infrared and 802.15.4. A phone's Bluetooth network is an Ethernet interface,
    /// and no cable.
    /// </summary>
    private static bool Wireless(int medium) => medium is 1 or 8 or 9 or 10 or 12 or 13 or 16 or 20;

    /// <summary>Whether a cable is in: the media connected, or, where the adapter can't say, the link up.</summary>
    public static bool IsPlugged(in WiredInterface row) =>
        row.MediaConnectState == MediaConnected || (row.MediaConnectState == MediaUnknown && row.OperStatus == OperUp);

    /// <summary>
    /// The wired adapters of <paramref name="interfaces"/>, plugged ones first, then by the adapter's
    /// name. <paramref name="networks"/> is null where the Network List Manager could not be read;
    /// <paramref name="addresses"/> holds every adapter that has an address, with its IPv4 address
    /// where it has one.
    /// </summary>
    public static IReadOnlyList<WiredAdapter> Adapters(
        IEnumerable<WiredInterface> interfaces,
        IReadOnlyList<WiredNetwork>? networks,
        IReadOnlyDictionary<ulong, string?> addresses)
    {
        var found = new List<WiredAdapter>();
        foreach (var row in interfaces)
        {
            if (!IsCable(row))
            {
                continue;
            }
            if (!IsPlugged(row))
            {
                found.Add(new WiredAdapter(row.Id, Named(row.Alias), Named(row.Alias), Plugged: false, WiredInternet.Unknown));
                continue;
            }
            // The network on it: the one that reaches furthest, where Windows has two.
            WiredNetwork? on = networks?
                .Where(network => network.Adapter == row.Id)
                .OrderByDescending(network => Reach(network.Connectivity))
                .Select(network => (WiredNetwork?)network)
                .FirstOrDefault();
            var address = addresses.TryGetValue(row.Luid, out var ipv4);
            if (on is null && !address)
            {
                // Nothing of this PC's own on it: bridged into a virtual switch, or not addressed yet.
                continue;
            }
            found.Add(new WiredAdapter(
                row.Id,
                on?.Name is { Length: > 0 } name ? name : Named(row.Alias),
                Named(row.Alias),
                Plugged: true,
                InternetOf(networks is not null, on),
                Speed(row.ReceiveSpeed, row.TransmitSpeed),
                ipv4,
                on?.Category ?? NetworkCategory.Unknown));
        }
        return [.. found
            .OrderByDescending(adapter => adapter.Plugged)
            .ThenBy(adapter => adapter.Adapter, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The rows the flyout shows: each plugged adapter. An unplugged one shows nothing, as Windows'
    /// list has it, except on a PC without Wi-Fi with no cable in at all, where the page would
    /// otherwise not say where its connection went: there each adapter says its cable is unplugged.
    /// </summary>
    public static IReadOnlyList<WiredAdapter> Shown(IReadOnlyList<WiredAdapter> adapters, bool wifi)
    {
        List<WiredAdapter> plugged = [.. adapters.Where(adapter => adapter.Plugged)];
        return plugged.Count > 0 || wifi ? plugged : adapters;
    }

    /// <summary>The row's second line, in Windows' words.</summary>
    public static string StateText(WiredAdapter adapter) => !adapter.Plugged
        ? S.DockQuickWiredUnplugged
        : adapter.Internet switch
        {
            WiredInternet.Identifying => S.DockQuickWiredIdentifying,
            WiredInternet.NoInternet => S.DockQuickWiredNoInternet,
            _ => S.DockQuickConnected,
        };

    /// <summary>The row's glyph: Ethernet, with a warning without internet, with a cross unplugged.</summary>
    public static string GlyphOf(WiredAdapter adapter) => !adapter.Plugged
        ? UnpluggedGlyph
        : adapter.Internet == WiredInternet.NoInternet ? WarningGlyph : Glyph;

    /// <summary>A link speed as people say it: "1 Gbps", "2.5 Gbps", "100 Mbps"; null where it is not known.</summary>
    public static string? SpeedText(ulong bitsPerSecond) => bitsPerSecond switch
    {
        0 => null,
        >= 1_000_000_000 => Number(bitsPerSecond / 1e9) + " Gbps",
        >= 1_000_000 => Number(bitsPerSecond / 1e6) + " Mbps",
        _ => Number(bitsPerSecond / 1e3) + " Kbps",
    };

    /// <summary>The network's profile, as Windows' Settings names it; null where it is not known.</summary>
    public static string? CategoryText(NetworkCategory category) => category switch
    {
        NetworkCategory.Public => S.DockQuickWiredPublic,
        NetworkCategory.Private => S.DockQuickWiredPrivate,
        NetworkCategory.Domain => S.DockQuickWiredDomain,
        _ => null,
    };

    /// <summary>NLM_NETWORK_CATEGORY: 0 public, 1 private, 2 domain authenticated.</summary>
    public static NetworkCategory CategoryOf(int category) => category switch
    {
        0 => NetworkCategory.Public,
        1 => NetworkCategory.Private,
        2 => NetworkCategory.Domain,
        _ => NetworkCategory.Unknown,
    };

    /// <summary>An IPv4 address's four bytes, in the order they go over the wire, as dotted decimal.</summary>
    public static string Dotted(byte a, byte b, byte c, byte d) =>
        string.Create(CultureInfo.InvariantCulture, $"{a}.{b}.{c}.{d}");

    private static WiredInternet InternetOf(bool listed, WiredNetwork? on) => !listed
        ? WiredInternet.Unknown
        : on is not { } network || network.Connectivity == 0
            ? WiredInternet.Identifying
            : (network.Connectivity & (IPv4Internet | IPv6Internet)) != 0 ? WiredInternet.Internet : WiredInternet.NoInternet;

    private static int Reach(int connectivity) =>
        (connectivity & (IPv4Internet | IPv6Internet)) != 0 ? 2 : connectivity != 0 ? 1 : 0;

    /// <summary>The faster of the two directions; an adapter that can't say reports all ones.</summary>
    private static ulong Speed(ulong receive, ulong transmit)
    {
        var known = (receive == ulong.MaxValue ? 0 : receive, transmit == ulong.MaxValue ? 0 : transmit);
        return Math.Max(known.Item1, known.Item2);
    }

    private static string Named(string alias) => alias.Length > 0 ? alias : S.DockQuickWiredEthernet;

    private static string Number(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);
}

internal sealed unsafe class WindowsWired : IWiredSource
{
    private const ushort AfUnspec = 0;
    private const ushort AfInet = 2;
    private const uint NoError = 0;
    private const uint IfTypeEthernet = 6;
    private const int MibIfTableNormal = 0;
    private const int MibIfTableNormalWithoutStatistics = 2;
    private const int DadDuplicate = 2;
    private const int DadPreferred = 4;

    private static readonly Guid NetworkListManagerClass = new("DCB00C01-570F-4A9B-8D69-199FDBA5723B");

    // The Changes each registration reports to, by the number its context carries.
    private static readonly ConcurrentDictionary<nint, WindowsWired> Targets = new();
    private static long nextNumber;

    private Changes? changes;
    private nint number;
    private HANDLE interfaceNotice;
    private HANDLE addressNotice;
    private HANDLE hintNotice;
    private INetworkListManager* networks;
    private bool disposed;

    public void Start(Changes news)
    {
        changes = news;
        number = (nint)Interlocked.Increment(ref nextNumber);
        Targets[number] = this;
        HANDLE handle = default;
        if (NotifyIpInterfaceChange(AfUnspec, &InterfaceChanged, (void*)number, 0, &handle) == NoError)
        {
            interfaceNotice = handle;
        }
        handle = default;
        if (NotifyUnicastIpAddressChange(AfUnspec, &AddressChanged, (void*)number, 0, &handle) == NoError)
        {
            addressNotice = handle;
        }
        handle = default;
        try
        {
            // Windows 10 2004 and later: the internet found or lost on a connection that stays up.
            if (NotifyNetworkConnectivityHintChange(&HintChanged, (void*)number, 0, &handle) == NoError)
            {
                hintNotice = handle;
            }
        }
        catch (EntryPointNotFoundException)
        {
            // Without it the page still hears the cable and the addresses.
        }
        var classId = NetworkListManagerClass;
        var interfaceId = INetworkListManager.Id;
        INetworkListManager* created = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, &interfaceId, (void**)&created).SUCCEEDED)
        {
            networks = created;
        }
    }

    public WiredState Read()
    {
        var rows = Interfaces();
        if (!rows.Any(row => Wired.IsCable(row)))
        {
            return new WiredState([]);
        }
        return new WiredState(Wired.Adapters(rows, Networks(), Addresses()));
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Targets.TryRemove(number, out _);
        // Each waits for a callback in flight, which only posts.
        foreach (var notice in new[] { interfaceNotice, addressNotice, hintNotice })
        {
            if (notice != HANDLE.NULL)
            {
                _ = CancelMibChangeNotify2(notice);
            }
        }
        interfaceNotice = addressNotice = hintNotice = HANDLE.NULL;
        if (networks != null)
        {
            networks->Release();
            networks = null;
        }
    }

    // --- Inside ------------------------------------------------------------------

    private static List<WiredInterface> Interfaces()
    {
        NetworkSampler.MIB_IF_TABLE2* table = null;
        // The level without statistics is Windows 10 1703 and later.
        if ((GetIfTable2Ex(MibIfTableNormalWithoutStatistics, &table) != NoError || table == null)
            && (GetIfTable2Ex(MibIfTableNormal, &table) != NoError || table == null))
        {
            return [];
        }
        try
        {
            var rows = &table->Table;
            var found = new List<WiredInterface>((int)table->NumEntries);
            for (uint i = 0; i < table->NumEntries; i++)
            {
                var row = rows + i;
                if (row->Type != IfTypeEthernet)
                {
                    // The fifty-odd others (Wi-Fi, loopback, tunnels) are no cable: their names aren't read.
                    continue;
                }
                found.Add(new WiredInterface(
                    row->InterfaceLuid,
                    row->InterfaceGuid,
                    new string(row->Alias),
                    row->Type,
                    row->PhysicalMediumType,
                    row->InterfaceAndOperStatusFlags,
                    row->AdminStatus,
                    row->OperStatus,
                    row->MediaConnectState,
                    row->ReceiveLinkSpeed,
                    row->TransmitLinkSpeed));
            }
            return found;
        }
        finally
        {
            FreeMibTable(table);
        }
    }

    /// <summary>Every adapter with an address, and its IPv4 address where it has one (a preferred one first).</summary>
    private static Dictionary<ulong, string?> Addresses()
    {
        var found = new Dictionary<ulong, string?>();
        UnicastTable* table = null;
        if (GetUnicastIpAddressTable(AfUnspec, &table) != NoError || table == null)
        {
            return found;
        }
        try
        {
            var rows = &table->Table;
            var preferred = new HashSet<ulong>();
            for (uint i = 0; i < table->NumEntries; i++)
            {
                var row = rows + i;
                if (row->DadState == DadDuplicate)
                {
                    continue;
                }
                found.TryAdd(row->Luid, null);
                if (row->Family != AfInet || preferred.Contains(row->Luid))
                {
                    continue;
                }
                var bytes = (byte*)row + 4;
                if (found[row->Luid] is null || row->DadState == DadPreferred)
                {
                    found[row->Luid] = Wired.Dotted(bytes[0], bytes[1], bytes[2], bytes[3]);
                }
                if (row->DadState == DadPreferred)
                {
                    preferred.Add(row->Luid);
                }
            }
        }
        finally
        {
            FreeMibTable(table);
        }
        return found;
    }

    /// <summary>The networks Windows has on each adapter; null where its list can't be read.</summary>
    private List<WiredNetwork>? Networks()
    {
        if (networks == null)
        {
            return null;
        }
        IEnumNetworkConnections* list = null;
        if (networks->GetNetworkConnections(&list).FAILED || list == null)
        {
            return null;
        }
        var found = new List<WiredNetwork>();
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
                    var adapter = Guid.Empty;
                    int connectivity = 0;
                    if (item->GetAdapterId(&adapter).FAILED)
                    {
                        continue;
                    }
                    _ = item->GetConnectivity(&connectivity);
                    var (name, category) = NetworkOf(item);
                    found.Add(new WiredNetwork(adapter, name, connectivity, category));
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
        return found;
    }

    private static (string? Name, NetworkCategory Category) NetworkOf(INetworkConnection* item)
    {
        INetwork* network = null;
        if (item->GetNetwork(&network).FAILED || network == null)
        {
            return (null, NetworkCategory.Unknown);
        }
        try
        {
            string? name = null;
            char* text = null;
            if (network->GetName(&text).SUCCEEDED && text != null)
            {
                name = new string(text, 0, (int)SysStringLen(text));
                SysFreeString(text);
            }
            int category = -1;
            var kind = network->GetCategory(&category).SUCCEEDED ? Wired.CategoryOf(category) : NetworkCategory.Unknown;
            return (name, kind);
        }
        finally
        {
            network->Release();
        }
    }

    private static void Heard(void* context)
    {
        if (Targets.TryGetValue((nint)context, out var wired))
        {
            wired.changes?.Add(WiredNews.Adapters);
        }
    }

    /// <summary>On the IP helper's thread: an interface changed (its link came up or went down).</summary>
    [UnmanagedCallersOnly]
    private static void InterfaceChanged(void* context, void* row, int type)
    {
        try
        {
            Heard(context);
        }
        catch (Exception)
        {
            // Nothing may escape into the IP helper's callback.
        }
    }

    /// <summary>On the IP helper's thread: an address came or went.</summary>
    [UnmanagedCallersOnly]
    private static void AddressChanged(void* context, void* row, int type)
    {
        try
        {
            Heard(context);
        }
        catch (Exception)
        {
            // Nothing may escape into the IP helper's callback.
        }
    }

    /// <summary>On the IP helper's thread: Windows' connectivity hint changed (the internet found or lost).</summary>
    [UnmanagedCallersOnly]
    private static void HintChanged(void* context, ConnectivityHint hint)
    {
        try
        {
            Heard(context);
        }
        catch (Exception)
        {
            // Nothing may escape into the IP helper's callback.
        }
    }

    /// <summary>Null when the declarations below match netioapi.h (Windows SDK 10.0.26100, shared\); otherwise what is wrong.</summary>
    internal static string? LayoutProblem()
    {
        UnicastTable table = default;
        var row = &table.Table;
        (string Name, long Actual, long Expected)[] checks =
        [
            ("MIB_UNICASTIPADDRESS_ROW", sizeof(UnicastRow), 80),
            ("MIB_UNICASTIPADDRESS_TABLE.Table", (byte*)row - (byte*)&table, 8),
            ("MIB_UNICASTIPADDRESS_ROW.InterfaceLuid", (byte*)&row->Luid - (byte*)row, 32),
            ("MIB_UNICASTIPADDRESS_ROW.DadState", (byte*)&row->DadState - (byte*)row, 64),
            ("NL_NETWORK_CONNECTIVITY_HINT", sizeof(ConnectivityHint), 12),
        ];
        foreach (var (name, actual, expected) in checks)
        {
            if (actual != expected)
            {
                return $"{name} is {actual}, expected {expected}";
            }
        }
        return null;
    }

    // --- netioapi.h --------------------------------------------------------------------

    /// <summary>
    /// MIB_UNICASTIPADDRESS_ROW, which TerraFX does not carry: its SOCKADDR_INET (the family at 0, an
    /// IPv4 address's four bytes at 4), its LUID and its duplicate-address state.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    internal struct UnicastRow
    {
        [FieldOffset(0)]
        public ushort Family;

        [FieldOffset(32)]
        public ulong Luid;

        /// <summary>NL_DAD_STATE: 2 a duplicate, 4 preferred.</summary>
        [FieldOffset(64)]
        public int DadState;
    }

    /// <summary>MIB_UNICASTIPADDRESS_TABLE: the count, then the rows from 8.</summary>
    [StructLayout(LayoutKind.Explicit)]
    internal struct UnicastTable
    {
        [FieldOffset(0)]
        public uint NumEntries;

        /// <summary>The first of <see cref="NumEntries"/> rows (ANY_SIZE array).</summary>
        [FieldOffset(8)]
        public UnicastRow Table;
    }

    /// <summary>NL_NETWORK_CONNECTIVITY_HINT, handed to the callback by value.</summary>
    internal struct ConnectivityHint
    {
        public int Level;
        public int Cost;
        public byte ApproachingDataLimit;
        public byte OverDataLimit;
        public byte Roaming;
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetIfTable2Ex(int level, NetworkSampler.MIB_IF_TABLE2** table);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetUnicastIpAddressTable(ushort family, UnicastTable** table);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void FreeMibTable(void* memory);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint NotifyIpInterfaceChange(ushort family, delegate* unmanaged<void*, void*, int, void> callback, void* context, byte initialNotification, HANDLE* handle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint NotifyUnicastIpAddressChange(ushort family, delegate* unmanaged<void*, void*, int, void> callback, void* context, byte initialNotification, HANDLE* handle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint NotifyNetworkConnectivityHintChange(delegate* unmanaged<void*, ConnectivityHint, void> callback, void* context, byte initialNotification, HANDLE* handle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint CancelMibChangeNotify2(HANDLE handle);
}

/// <summary>
/// Made-up wired adapters for pictures and tests: none unless handed some. <see cref="Office"/> is a
/// cable on a private network with internet; the rest are made by the tests from it.
/// </summary>
internal sealed class SampleWired : IWiredSource
{
    /// <summary>A cable to a private network with internet, at a gigabit.</summary>
    public static WiredAdapter Office { get; } = new(
        new Guid("6f1c2a52-0d3e-4c55-9a35-3b1d1e2f4a01"), "Network 2", "Ethernet", Plugged: true, WiredInternet.Internet, // not-ui: a sample network's name
        1_000_000_000, "192.168.1.24", NetworkCategory.Private);

    /// <summary>A second adapter, a USB one on a dock, on a network without internet.</summary>
    public static WiredAdapter Dock { get; } = new(
        new Guid("6f1c2a52-0d3e-4c55-9a35-3b1d1e2f4a02"), "Lab", "Ethernet 2", Plugged: true, WiredInternet.NoInternet, // not-ui: a sample adapter's name
        2_500_000_000, "10.0.0.17", NetworkCategory.Public);

    private List<WiredAdapter> adapters;
    private Changes? changes;

    public SampleWired(params WiredAdapter[] adapters)
    {
        this.adapters = [.. adapters];
    }

    /// <summary>How often the page read it: a cable plugged in is read again at once.</summary>
    public int Reads { get; private set; }

    public void Start(Changes news) => changes = news;

    public WiredState Read()
    {
        Reads++;
        return new WiredState([.. adapters.Select(AsRead).OrderByDescending(adapter => adapter.Plugged).ThenBy(adapter => adapter.Adapter, StringComparer.OrdinalIgnoreCase)]);
    }

    // An adapter as Windows reads it: one whose cable is out is on no network, so it is its own
    // name alone (Wired.Adapters), whatever network it had while it was in.
    private static WiredAdapter AsRead(WiredAdapter adapter) =>
        adapter.Plugged ? adapter : new WiredAdapter(adapter.Id, adapter.Adapter, adapter.Adapter, Plugged: false, WiredInternet.Unknown);

    /// <summary>The cable of <paramref name="id"/> pulled out or plugged in, and the page told, as Windows tells it.</summary>
    public void Plug(Guid id, bool plugged)
    {
        adapters = [.. adapters.Select(adapter => adapter.Id == id ? adapter with { Plugged = plugged } : adapter)];
        changes?.Add(WiredNews.Adapters);
    }

    /// <summary>Every adapter replaced, and the page told.</summary>
    public void Set(params WiredAdapter[] now)
    {
        adapters = [.. now];
        changes?.Add(WiredNews.Adapters);
    }

    public void Dispose()
    {
    }
}
