// The network widget: the Wi-Fi widget renamed "Network", and the wired connections its flyout
// shows at the head of the Wi-Fi list (Wired.cs). The rules are held here apart from the reading — which
// interfaces are a cable's, what each row says, which rows show — on made-up interface rows,
// networks and addresses, so nothing here reads or touches this PC's adapters. The one native
// check is the layout of the address table's rows against the SDK's.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;

namespace Karpo.Tests.QuickSettings;

public sealed class WiredTests
{
    private static readonly Guid Nic = new("11111111-0000-0000-0000-000000000001");
    private static readonly Guid Usb = new("11111111-0000-0000-0000-000000000002");

    /// <summary>An onboard Ethernet port with a cable in, at a gigabit, unless told otherwise.</summary>
    private static WiredInterface Port(
        Guid? id = null,
        ulong luid = 1,
        string alias = "Ethernet",
        uint type = 6,
        int medium = 14,
        byte flags = 0x01,
        int admin = 1,
        int oper = 1,
        int media = 1,
        ulong receive = 1_000_000_000,
        ulong transmit = 1_000_000_000) =>
        new(luid, id ?? Nic, alias, type, medium, flags, admin, oper, media, receive, transmit);

    private static readonly Dictionary<ulong, string?> Addressed = new() { [1] = "192.168.1.24", [2] = "10.0.0.17" };

    // --- Which interfaces are a cable's --------------------------------------------------------

    /// <summary>
    /// Only real hardware: Ethernet by type, a hardware interface, not a filter's second entry for
    /// one, turned on, and not a wireless adapter that says it is Ethernet (a phone's Bluetooth
    /// network is one). A VPN or a Hyper-V switch is Ethernet by type and no hardware.
    /// </summary>
    [Fact]
    public void OnlyRealWiredHardwareIsACable()
    {
        Assert.True(Wired.IsCable(Port()));
        // A USB adapter whose driver says nothing of its medium.
        Assert.True(Wired.IsCable(Port(medium: 0)));
        Assert.False(Wired.IsCable(Port(type: 71)), "a Wi-Fi adapter");
        Assert.False(Wired.IsCable(Port(flags: 0x00)), "a Hyper-V switch or a VPN, no hardware");
        Assert.False(Wired.IsCable(Port(flags: 0x03)), "a filter's entry for the same port");
        Assert.False(Wired.IsCable(Port(medium: 10)), "Bluetooth's network");
        Assert.False(Wired.IsCable(Port(medium: 1)), "a wireless LAN that says it is Ethernet");
        Assert.False(Wired.IsCable(Port(medium: 8)), "a cellular modem");
        Assert.False(Wired.IsCable(Port(admin: 2)), "an adapter turned off");
        Assert.False(Wired.IsCable(Port(type: 24)), "loopback");
    }

    /// <summary>A cable is in when the media says so; where the adapter can't say, when the link is up.</summary>
    [Fact]
    public void ACableIsInWhenTheMediaIsConnected()
    {
        Assert.True(Wired.IsPlugged(Port(media: 1)));
        Assert.False(Wired.IsPlugged(Port(media: 2, oper: 2)));
        // The media's word wins over the link's.
        Assert.False(Wired.IsPlugged(Port(media: 2, oper: 1)));
        Assert.True(Wired.IsPlugged(Port(media: 0, oper: 1)));
        Assert.False(Wired.IsPlugged(Port(media: 0, oper: 7)));
    }

    // --- What each adapter is ------------------------------------------------------------------

    /// <summary>
    /// The network on a plugged adapter, as the Network List Manager has it: its name, whether it
    /// reaches the internet over IPv4 or IPv6, and its profile; with the link speed and the address.
    /// </summary>
    [Fact]
    public void APluggedAdapterIsItsNetwork()
    {
        var adapter = Assert.Single(Wired.Adapters(
            [Port()],
            [new WiredNetwork(Nic, "Network 2", 0x40 | 0x10, NetworkCategory.Private)],
            Addressed));
        Assert.Equal(new WiredAdapter(Nic, "Network 2", "Ethernet", true, WiredInternet.Internet, 1_000_000_000, "192.168.1.24", NetworkCategory.Private), adapter);

        // IPv6 alone reaches the internet too.
        Assert.Equal(WiredInternet.Internet, Wired.Adapters([Port()], [new WiredNetwork(Nic, "Home", 0x400, NetworkCategory.Public)], Addressed)[0].Internet);
    }

    /// <summary>Every state a plugged adapter can be in, and what Windows' list says for each.</summary>
    [Theory]
    [InlineData(0x40, (int)WiredInternet.Internet, "Connected")]
    [InlineData(0x20, (int)WiredInternet.NoInternet, "No internet")]
    [InlineData(0x10 | 0x100, (int)WiredInternet.NoInternet, "No internet")]
    [InlineData(0x01, (int)WiredInternet.NoInternet, "No internet")]
    [InlineData(0, (int)WiredInternet.Identifying, "Identifying…")]
    public void TheConnectivityIsWhatTheRowSays(int connectivity, int internet, string said)
    {
        var adapter = Wired.Adapters([Port()], [new WiredNetwork(Nic, "Network", connectivity, NetworkCategory.Public)], Addressed)[0];
        Assert.Equal((WiredInternet)internet, adapter.Internet);
        Assert.Equal(said, Wired.StateText(adapter));
    }

    /// <summary>
    /// A cable that is in with an address and no network yet is being identified; with neither,
    /// nothing of this PC's own is on it (its port is bridged into a virtual switch, or it came up a
    /// moment ago) and it has no row. Where Windows' list can't be read at all, the cable is in and
    /// that is what the row says.
    /// </summary>
    [Fact]
    public void WithoutANetworkTheAddressDecides()
    {
        var identifying = Assert.Single(Wired.Adapters([Port()], [], Addressed));
        Assert.Equal(WiredInternet.Identifying, identifying.Internet);
        Assert.Equal("Ethernet", identifying.Name);
        Assert.Equal("Identifying…", Wired.StateText(identifying));

        Assert.Empty(Wired.Adapters([Port()], [], new Dictionary<ulong, string?>()));

        // A network on it is enough, address or not.
        Assert.Single(Wired.Adapters([Port()], [new WiredNetwork(Nic, "Network", 0x20, NetworkCategory.Public)], new Dictionary<ulong, string?>()));

        var unread = Assert.Single(Wired.Adapters([Port()], null, Addressed));
        Assert.Equal(WiredInternet.Unknown, unread.Internet);
        Assert.Equal(NetworkCategory.Unknown, unread.Category);
        Assert.Equal("Connected", Wired.StateText(unread));

        // An address with no IPv4 one (IPv6 only, or link-local before DHCP): bound, and no IPv4 to say.
        var six = Assert.Single(Wired.Adapters([Port()], [], new Dictionary<ulong, string?> { [1] = null }));
        Assert.Null(six.Address);
    }

    /// <summary>A network Windows has not named yet is the adapter's own name; an adapter with none is "Ethernet".</summary>
    [Fact]
    public void ANamelessNetworkIsTheAdapters()
    {
        Assert.Equal("Ethernet 3", Wired.Adapters([Port(alias: "Ethernet 3")], [new WiredNetwork(Nic, "", 0x40, NetworkCategory.Private)], Addressed)[0].Name);
        Assert.Equal("Ethernet 3", Wired.Adapters([Port(alias: "Ethernet 3")], [new WiredNetwork(Nic, null, 0x40, NetworkCategory.Private)], Addressed)[0].Name);
        Assert.Equal("Ethernet", Wired.Adapters([Port(alias: "")], [new WiredNetwork(Nic, null, 0x40, NetworkCategory.Private)], Addressed)[0].Name);
    }

    /// <summary>Where Windows has two networks on one adapter, the row is the one that reaches furthest.</summary>
    [Fact]
    public void OfTwoNetworksTheFurthestReachingIsTheRow()
    {
        var adapter = Wired.Adapters(
            [Port()],
            [
                new WiredNetwork(Nic, "Unidentified network", 0x20, NetworkCategory.Public),
                new WiredNetwork(Nic, "corp.example", 0x40, NetworkCategory.Domain),
                new WiredNetwork(Usb, "Elsewhere", 0x40, NetworkCategory.Private),
            ],
            Addressed)[0];
        Assert.Equal(("corp.example", WiredInternet.Internet, NetworkCategory.Domain), (adapter.Name, adapter.Internet, adapter.Category));
    }

    /// <summary>
    /// An unplugged adapter is kept, by its own name and with nothing of a network: it is the page
    /// that decides whether to show it (<see cref="Wired.Shown"/>).
    /// </summary>
    [Fact]
    public void AnUnpluggedAdapterIsItsNameAlone()
    {
        var adapter = Assert.Single(Wired.Adapters(
            [Port(media: 2, oper: 2)],
            [new WiredNetwork(Nic, "Network 2", 0x40, NetworkCategory.Private)],
            Addressed));
        Assert.Equal(new WiredAdapter(Nic, "Ethernet", "Ethernet", false, WiredInternet.Unknown), adapter);
        Assert.Equal("Network cable unplugged", Wired.StateText(adapter));
        Assert.Equal(Wired.UnpluggedGlyph, Wired.GlyphOf(adapter));
    }

    /// <summary>The faster direction is the link speed; an adapter that can't say reports all ones, which is no speed.</summary>
    [Fact]
    public void TheLinkSpeedIsTheFasterDirection()
    {
        Assert.Equal(2_500_000_000ul, Wired.Adapters([Port(receive: 2_500_000_000, transmit: 1_000_000_000)], null, Addressed)[0].Speed);
        Assert.Equal(100_000_000ul, Wired.Adapters([Port(receive: ulong.MaxValue, transmit: 100_000_000)], null, Addressed)[0].Speed);
        Assert.Equal(0ul, Wired.Adapters([Port(receive: ulong.MaxValue, transmit: ulong.MaxValue)], null, Addressed)[0].Speed);
    }

    /// <summary>Plugged ones first, then by the adapter's name; what is not a cable never shows.</summary>
    [Fact]
    public void TheAdaptersAreInOrder()
    {
        var adapters = Wired.Adapters(
            [
                Port(id: Usb, luid: 2, alias: "Ethernet 2"),
                Port(id: Guid.NewGuid(), luid: 3, alias: "Ethernet 3", media: 2, oper: 2),
                Port(id: Nic, luid: 1, alias: "Ethernet"),
                Port(id: Guid.NewGuid(), luid: 4, alias: "vEthernet (Default Switch)", flags: 0x00),
                Port(id: Guid.NewGuid(), luid: 5, alias: "Wi-Fi", type: 71, medium: 9),
            ],
            [],
            Addressed);
        Assert.Equal(["Ethernet", "Ethernet 2", "Ethernet 3"], adapters.Select(adapter => adapter.Adapter));
        Assert.Equal([true, true, false], adapters.Select(adapter => adapter.Plugged));
    }

    // --- Which rows show -----------------------------------------------------------------------

    /// <summary>
    /// Each plugged adapter has a row, and an unplugged one has none, as in Windows' list; but on a
    /// PC without Wi-Fi with no cable in at all, each says its cable is unplugged rather than the
    /// page going quiet.
    /// </summary>
    [Fact]
    public void TheRowsAreThePluggedCables()
    {
        var plugged = SampleWired.Office;
        var pulled = SampleWired.Dock with { Plugged = false };

        Assert.Equal([plugged], Wired.Shown([plugged, pulled], wifi: true));
        Assert.Equal([plugged], Wired.Shown([plugged, pulled], wifi: false));
        Assert.Empty(Wired.Shown([pulled], wifi: true));
        Assert.Equal([pulled], Wired.Shown([pulled], wifi: false));
        Assert.Empty(Wired.Shown([], wifi: false));
    }

    // --- What a row says -----------------------------------------------------------------------

    [Fact]
    public void TheGlyphSaysTheState()
    {
        Assert.Equal(Wired.Glyph, Wired.GlyphOf(SampleWired.Office));
        Assert.Equal(Wired.WarningGlyph, Wired.GlyphOf(SampleWired.Dock));
        Assert.Equal(Wired.Glyph, Wired.GlyphOf(SampleWired.Office with { Internet = WiredInternet.Identifying }));
        Assert.Equal(Wired.Glyph, Wired.GlyphOf(SampleWired.Office with { Internet = WiredInternet.Unknown }));
        Assert.Equal(Wired.UnpluggedGlyph, Wired.GlyphOf(SampleWired.Office with { Plugged = false }));
    }

    [Theory]
    [InlineData(1_000_000_000ul, "1 Gbps")]
    [InlineData(2_500_000_000ul, "2.5 Gbps")]
    [InlineData(10_000_000_000ul, "10 Gbps")]
    [InlineData(100_000_000ul, "100 Mbps")]
    [InlineData(10_000_000ul, "10 Mbps")]
    [InlineData(10_000ul, "10 Kbps")]
    [InlineData(0ul, null)]
    public void ALinkSpeedIsSaidAsPeopleSayIt(ulong bits, string? said) => Assert.Equal(said, Wired.SpeedText(bits));

    [Fact]
    public void TheProfileIsWindowsWord()
    {
        Assert.Equal(NetworkCategory.Public, Wired.CategoryOf(0));
        Assert.Equal(NetworkCategory.Private, Wired.CategoryOf(1));
        Assert.Equal(NetworkCategory.Domain, Wired.CategoryOf(2));
        Assert.Equal(NetworkCategory.Unknown, Wired.CategoryOf(-1));
        Assert.Equal("Public network", Wired.CategoryText(NetworkCategory.Public));
        Assert.Equal("Private network", Wired.CategoryText(NetworkCategory.Private));
        Assert.Equal("Domain network", Wired.CategoryText(NetworkCategory.Domain));
        Assert.Null(Wired.CategoryText(NetworkCategory.Unknown));
    }

    [Fact]
    public void AnAddressIsDottedInWireOrder() => Assert.Equal("192.168.1.24", Wired.Dotted(192, 168, 1, 24));

    /// <summary>The address table's rows are read through declarations of Karpo's own; they match the SDK's.</summary>
    [Fact]
    public void TheAddressTablesLayoutIsTheSdks() => Assert.Null(WindowsWired.LayoutProblem());

    /// <summary>The made-up adapters tell the page when a cable goes in or out, as Windows tells the real one.</summary>
    [Fact]
    public void APluggedCableIsNews()
    {
        var posted = new List<int>();
        var changes = new Changes(posted.Add);
        using var wired = new SampleWired(SampleWired.Office);
        wired.Start(changes);
        wired.Plug(SampleWired.Office.Id, false);
        Assert.Equal([WiredNews.Adapters], posted);
        Assert.False(Assert.Single(wired.Read().Adapters).Plugged);
        Assert.Equal(WiredNews.Adapters, changes.Take());
        wired.Plug(SampleWired.Office.Id, true);
        Assert.True(Assert.Single(wired.Read().Adapters).Plugged);
        // Its news is a bit of its own beside the Wi-Fi's, in the page's one Changes.
        Assert.Equal(0, WiredNews.Adapters & (WifiNews.Networks | WifiNews.Connection | WifiNews.Radio));
    }

    // --- The widget's name ---------------------------------------------------------------------

    /// <summary>
    /// The Wi-Fi widget is "Network" wherever it is seen, and found by every word for a connection;
    /// its id is still "wifi", and its settings link is Windows' Network and internet page. Its
    /// page alone does not say "Network": its one title is Windows' "Wi-Fi" (2026-10-05). It is a
    /// line of a list since 2026-10-10, the Wi-Fi preset's.
    /// </summary>
    [Fact]
    public void TheWifiWidgetIsTheNetworkWidget()
    {
        var info = WidgetInfos.Wifi;
        Assert.Equal("wifi", info.Id);
        Assert.Equal("Network", info.Title);
        Assert.Equal("Network", ListWidgets.Of(ListKind.Wifi).Info.Title);
        foreach (var word in (string[])["wifi", "wi-fi", "ethernet", "network", "internet", "lan", "cable"])
        {
            Assert.Contains(word, info.Keywords);
        }
        Assert.Equal("ms-settings:network", info.Settings?.Uri);
        Assert.Equal("Wi-Fi", WifiPage.Title);
    }

    /// <summary>
    /// The flyout's title row is "Wi-Fi" wherever there is Wi-Fi to title, whatever the cables; a PC
    /// without Wi-Fi whose cables are the page has no title row; a PC with neither keeps "Wi-Fi" over
    /// its "No Wi-Fi adapter" notice.
    /// </summary>
    [Fact]
    public void TheFlyoutIsTitledWiFiWhereThereIsWiFi()
    {
        foreach (var access in Enum.GetValues<WifiAccess>().Where(access => access != WifiAccess.NoAdapter))
        {
            foreach (var cables in (int[])[0, 1, 2])
            {
                Assert.True(WifiPage.Titled(access, cables), $"{access} with {cables} cables");
            }
        }
        Assert.True(WifiPage.Titled(WifiAccess.NoAdapter, 0));
        Assert.False(WifiPage.Titled(WifiAccess.NoAdapter, 1));
        Assert.False(WifiPage.Titled(WifiAccess.NoAdapter, 2));
    }

    /// <summary>
    /// No two widgets share a name: the bandwidth graph that was "Network" too is "Network activity"
    /// in the catalogue — "Network activity graph" since its details are a widget of their own — and
    /// still says "Network" beside its reading on a card or a line. A folder's divider is "Separator" (2026-10-11), the
    /// name the bar's separator shared with it until it went (§511).
    /// </summary>
    [Fact]
    public void NoTwoWidgetsShareAName()
    {
        var shared = WidgetInfos.All
            .GroupBy(info => info.Title, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key);
        Assert.Empty(shared);
        Assert.Equal("Network activity graph", WidgetInfos.Network.Title);
        Assert.Equal("Network details", WidgetInfos.NetworkDetails.Title);
        Assert.Equal("Network", PerformanceIsland.Drawn(Metric.Network));
        Assert.Equal("Processor", PerformanceIsland.Drawn(Metric.Cpu));
    }

    /// <summary>
    /// A folder's network line reads as the network widget and saves as it was; the network button
    /// on the bar is gone (the Wi-Fi preset is a folder of these), and a file's is read without it.
    /// </summary>
    [Fact]
    public void ASavedWifiLineIsTheNetworkWidget()
    {
        var read = Configs.Repaired("""
            { "layouts": [ { "id": "L", "islands": [
              { "id": "w", "kind": "wifi" },
              { "id": "f", "kind": "folderList", "folder": { "lists": [ [ { "island": { "id": "l", "kind": "wifi" } } ] ] } } ] } ] }
            """, out var changed);
        Assert.True(changed);
        var folder = Assert.Single(Configs.Islands(read));
        Assert.Equal("f", folder.Id);
        var line = Assert.Single(folder.Folder!.Cells).Island;
        Assert.Equal(ListKind.Wifi, line.List);
        Assert.Equal("Network", line.Info!.Title);

        var again = Configs.RoundTrip(read, out var twice);
        Assert.False(twice);
        Assert.Equal(ListKind.Wifi, Assert.Single(Assert.Single(Configs.Islands(again)).Folder!.Cells).Island.List);
        Assert.Contains("\"wifi\"", again.ToJson(), StringComparison.Ordinal);
    }
}
