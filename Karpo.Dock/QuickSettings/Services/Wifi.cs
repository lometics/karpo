// Wi-Fi for the network flyout, through the Native Wifi API (wlanapi.dll): the
// first wireless interface, a scan, the networks in range, joining one with the
// profile Windows has for it or with a new WPA2/WPA3-Personal profile made from
// the key the person typed (WifiProfiles), and leaving. While the flyout is open
// the WLAN notifications (a scan done, a connection made or failed, the radio)
// arrive on the WLAN service's thread and are only posted on.
//
// As Windows' own list does, it also joins:
//
//   * a hidden network — one that does not say its name — by the name the person
//     types: the networks in range that say none are one "Hidden network" entry, and
//     the profile made for the name is marked as one that is not broadcast;
//   * an enterprise (802.1X) network, where Windows can with a user name and a
//     password: with the profile Windows has for it and the credentials it saved,
//     else with a PEAP-MSCHAPv2 profile, the protected-password sign-in Windows'
//     list sets up for such a network, and the user name and password the person
//     typed, saved as that profile's credentials. The server's certificate is
//     Windows' to show and the person's to trust, in Windows' own prompt, as it is
//     when Windows' list joins the network. A network that signs in only with a
//     certificate (WPA3-Enterprise 192-bit) is still left to Windows' list.
//
// A network's properties are Windows' own page for it, the one its profile's
// dialog shows (WlanUIEditProfile): its connection and its security. The dialog
// runs in a Karpo process of its own, so nothing it does can end the dock
// (WifiProperties.cs).
//
// Windows 11 24H2 and later list networks to a desktop app only while it may
// use the location ("Let desktop apps access your location"): the network list
// is refused with ERROR_ACCESS_DENIED then, and the page says so and links to
// the privacy page and to Windows' own list.
//
// The radio is switched through Windows.Devices.Radios. Should Windows refuse
// that to an unpackaged app, the page hides its switch and links to Settings.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>A network in range, merged over the entries Windows lists for it (one per profile, one without).</summary>
/// <param name="Profile">The profile Windows would join it with; null when it has none.</param>
/// <param name="Cipher">The network's default cipher (DOT11_CIPHER_ALGORITHM), for a new profile.</param>
/// <param name="Hidden">
/// A network that does not say its name: in the list, the one entry for every such network in
/// range, named <see cref="WifiProfiles.HiddenName"/>; handed to <see cref="IWifiSource.Connect"/>,
/// one under the name the person typed for it.
/// </param>
/// <param name="Auth">The network's default authentication (DOT11_AUTH_ALGORITHM), which says which enterprise sign-in it takes.</param>
internal sealed record WifiNetwork(string Ssid, byte[] SsidBytes, int Bars, WifiSecurity Security, int Cipher, bool Connected, string? Profile, bool Hidden = false, int Auth = 0)
{
    public bool Secured => Security is not (WifiSecurity.Open);
}

internal enum WifiAccess
{
    Allowed,

    /// <summary>No wireless adapter (or the WLAN service isn't running).</summary>
    NoAdapter,

    /// <summary>Windows won't list networks to Karpo without location access (24H2 and later).</summary>
    Denied,
}

internal enum WifiOutcome
{
    Connecting,
    Connected,

    /// <summary>A secured network without a profile, or whose profile's key failed: the key is asked for.</summary>
    NeedsKey,

    /// <summary>The key typed didn't work.</summary>
    WrongKey,

    /// <summary>An enterprise network without credentials Windows has: a user name and a password are asked for.</summary>
    NeedsSignIn,

    /// <summary>The user name and password typed didn't work.</summary>
    WrongSignIn,

    Failed,
}

/// <summary>What the person typed to sign in to an enterprise network: a user name — "DOMAIN\user" where it has one — and a password.</summary>
internal sealed record WifiSignIn(string User, string Password);

/// <summary>The last attempt to join a network, and how it went.</summary>
internal sealed record WifiAttempt(string Ssid, WifiOutcome Outcome, string? Problem = null);

/// <param name="RadioOn">Null when the radio can't be read (no adapter).</param>
/// <param name="CanSwitch">Whether the switch is offered: false once Windows refused to switch the radio for Karpo.</param>
internal sealed record WifiState(bool? RadioOn, bool CanSwitch, WifiAccess Access, IReadOnlyList<WifiNetwork> Networks, bool Scanning, WifiAttempt? Attempt, string? Problem);

internal interface IWifiSource : IDisposable
{
    void Start(Changes changes);

    WifiState Read();

    /// <summary>Asks for a scan; WifiNews.Networks follows when it is done.</summary>
    void Scan();

    /// <summary>Switches the radio; WifiNews.Radio follows with the outcome.</summary>
    void SetRadio(bool on);

    /// <summary>
    /// Joins <paramref name="network"/>, with <paramref name="key"/> when one was typed, or
    /// <paramref name="signIn"/> for an enterprise network; WifiNews.Connection follows. A hidden
    /// network is handed over under the name the person typed for it.
    /// </summary>
    void Connect(WifiNetwork network, string? key, bool automatic, WifiSignIn? signIn = null);

    /// <summary>
    /// Opens Windows' own properties of <paramref name="network"/>: its profile's page, its
    /// connection and its security. False where it has none to open, and Windows' Wi-Fi settings
    /// are the place instead.
    /// </summary>
    bool ShowProperties(WifiNetwork network);

    void Disconnect();

    /// <summary>Forgets the last attempt (the person closed the network's card).</summary>
    void Forget();
}

internal static class WifiNews
{
    public const int Networks = 1;
    public const int Connection = 2;
    public const int Radio = 4;
}

internal sealed unsafe class WindowsWifi : IWifiSource
{
    private const uint NotificationSourceAcm = 0x08;
    private const uint NotificationSourceMsm = 0x10;
    private const uint AcmScanComplete = 7;
    private const uint AcmScanFail = 8;
    private const uint AcmConnectionComplete = 10;
    private const uint AcmConnectionAttemptFail = 11;
    private const uint AcmInterfaceArrival = 13;
    private const uint AcmInterfaceRemoval = 14;
    private const uint AcmScanListRefresh = 26;
    private const uint AcmDisconnected = 21;
    private const uint MsmConnected = 4;
    private const uint MsmRadioStateChange = 7;
    private const uint MsmSignalQualityChange = 8;
    private const uint MsmDisconnected = 10;
    private const int RadioStateOpcode = 4;
    private const uint ProfileUser = 2;
    private const uint ErrorAccessDenied = 5;
    private const int SecurityReasonsLow = 0x00048000;
    private const int SecurityReasonsHigh = 0x0004FFFF;

    // The Changes and the service each registration reports to, by the number its context carries.
    private static readonly ConcurrentDictionary<nint, WindowsWifi> Targets = new();
    private static long nextNumber;

    private HANDLE client;
    private Guid adapter;
    private bool hasAdapter;
    private Changes? changes;
    private nint number;
    private RadioSwitch? radio;
    private volatile bool canSwitch = true;
    private volatile bool scanning;
    private long scanStarted;
    private volatile string? problem;
    private bool disposed;

    // The attempt in flight: written by the dock's thread, finished by the WLAN thread.
    private readonly object gate = new();
    private WifiAttempt? attempt;
    private string? attemptProfile;
    private bool attemptWithKey;
    private bool attemptEnterprise;
    private bool attemptWithSignIn;
    private string? madeProfile;
    private int finishedReason = -1;

    public void Start(Changes news)
    {
        changes = news;
        radio = new RadioSwitch(Radios.RadioKind.WiFi, () => news.Add(WifiNews.Radio));
        radio.Find();
        uint negotiated = 0;
        HANDLE opened = HANDLE.NULL;
        if (Wlan.WlanOpenHandle(2, null, &negotiated, &opened) != 0)
        {
            return;
        }
        client = opened;
        number = (nint)Interlocked.Increment(ref nextNumber);
        Targets[number] = this;
        uint previous = 0;
        _ = Wlan.WlanRegisterNotification(client, NotificationSourceAcm | NotificationSourceMsm, BOOL.FALSE, &Notified, (void*)number, null, &previous);
        FindAdapter();
        Scan();
    }

    public WifiState Read()
    {
        if (client == HANDLE.NULL || !hasAdapter)
        {
            if (client != HANDLE.NULL)
            {
                FindAdapter();
            }
            if (!hasAdapter)
            {
                return new WifiState(null, false, WifiAccess.NoAdapter, [], false, null, problem);
            }
        }
        Finish();
        var on = RadioOn();
        // A scan that never reported back is over after a few seconds.
        if (scanning && Environment.TickCount64 - scanStarted > 8000)
        {
            scanning = false;
        }
        WifiAttempt? now;
        lock (gate)
        {
            now = attempt;
        }
        if (on == false)
        {
            return new WifiState(false, canSwitch, WifiAccess.Allowed, [], false, now, problem);
        }
        var adapterId = adapter;
        void* list = null;
        var result = Wlan.WlanGetAvailableNetworkList(client, &adapterId, 0, null, &list);
        if (result == ErrorAccessDenied)
        {
            return new WifiState(on, canSwitch, WifiAccess.Denied, [], false, now, problem);
        }
        if (result != 0 || list == null)
        {
            return new WifiState(on, canSwitch, WifiAccess.Allowed, [], scanning, now, problem);
        }
        try
        {
            return new WifiState(on, canSwitch, WifiAccess.Allowed, Networks((byte*)list), scanning, now, problem);
        }
        finally
        {
            Wlan.WlanFreeMemory(list);
        }
    }

    public void Scan()
    {
        if (client == HANDLE.NULL || !hasAdapter)
        {
            return;
        }
        var adapterId = adapter;
        if (Wlan.WlanScan(client, &adapterId, null, null, null) == 0)
        {
            scanning = true;
            scanStarted = Environment.TickCount64;
        }
    }

    public void SetRadio(bool on)
    {
        problem = null;
        if (radio is not { } switcher)
        {
            Refused(on);
            return;
        }
        switcher.Set(on).ContinueWith(
            done => Troubles.Guard("Wi-Fi: switching the radio", () =>
            {
                if (done.IsCompletedSuccessfully && done.Result)
                {
                    changes?.Add(WifiNews.Radio);
                }
                else
                {
                    Refused(on);
                }
            }),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    /// <summary>Windows didn't switch the radio for Karpo: the switch goes, and Settings is offered.</summary>
    private void Refused(bool on)
    {
        canSwitch = false;
        problem = on ? S.DockQuickWifiOnRefused : S.DockQuickWifiOffRefused;
        changes?.Add(WifiNews.Radio);
    }

    public void Connect(WifiNetwork network, string? key, bool automatic, WifiSignIn? signIn = null)
    {
        if (client == HANDLE.NULL || !hasAdapter)
        {
            return;
        }
        // A hidden network's own profile, where Windows has one for the name typed.
        string? profile = network.Profile ?? (network.Hidden && HasProfile(WifiProfiles.ProfileName(network.SsidBytes, network.Ssid)) ? WifiProfiles.ProfileName(network.SsidBytes, network.Ssid) : null);
        var withKey = key is not null;
        var enterprise = network.Security == WifiSecurity.Enterprise;
        lock (gate)
        {
            finishedReason = -1;
        }
        if (WifiProfiles.Foreseen(network, key, signIn, profile is not null) is { } foreseen)
        {
            Settle(foreseen);
            return;
        }
        if (enterprise)
        {
            if (signIn is not null)
            {
                var name = WifiProfiles.ProfileName(network.SsidBytes, network.Ssid);
                profile = SetProfile(WifiProfiles.EnterpriseXml(network.SsidBytes, network.Ssid, network.Auth, network.Cipher, automatic, network.Hidden), name);
                if (profile is null)
                {
                    Settle(new WifiAttempt(network.Ssid, WifiOutcome.Failed, S.DockQuickWifiProfileRefused));
                    return;
                }
                madeProfile = profile;
                if (!SetCredentials(profile, WifiProfiles.EapUserXml(signIn)))
                {
                    Unmake();
                    Settle(new WifiAttempt(network.Ssid, WifiOutcome.Failed, S.DockQuickWifiSignInRefused));
                    return;
                }
            }
        }
        else if (key is not null || profile is null)
        {
            var xml = WifiProfiles.Xml(network.SsidBytes, network.Ssid, network.Security, network.Cipher, key, automatic, network.Hidden);
            profile = SetProfile(xml, WifiProfiles.ProfileName(network.SsidBytes, network.Ssid));
            if (profile is null)
            {
                Settle(new WifiAttempt(network.Ssid, WifiOutcome.Failed, S.DockQuickWifiProfileRefused));
                return;
            }
            madeProfile = profile;
        }
        var parameters = new ConnectionParameters { Mode = 0, BssType = 1 };
        var ssid = default(Dot11Ssid);
        ssid.Length = (uint)Math.Min(32, network.SsidBytes.Length);
        for (var i = 0; i < ssid.Length; i++)
        {
            ssid.Bytes[i] = network.SsidBytes[i];
        }
        var adapterId = adapter;
        uint result;
        fixed (char* name = profile)
        {
            parameters.Profile = name;
            parameters.Ssid = ssid.Length > 0 ? &ssid : null;
            lock (gate)
            {
                attempt = new WifiAttempt(network.Ssid, WifiOutcome.Connecting);
                attemptProfile = profile;
                attemptWithKey = withKey;
                attemptEnterprise = enterprise;
                attemptWithSignIn = signIn is not null;
            }
            result = Wlan.WlanConnect(client, &adapterId, &parameters, null);
        }
        if (result != 0)
        {
            Settle(new WifiAttempt(network.Ssid, WifiOutcome.Failed, S.DockQuickWifiStartFailed));
            Unmake();
        }
        changes?.Add(WifiNews.Connection);
    }

    public bool ShowProperties(WifiNetwork network)
    {
        var profile = network.Profile;
        if (client == HANDLE.NULL || !hasAdapter || profile is null)
        {
            return false;
        }
        // Windows' dialog, in a process of its own and waited for on a thread of its own, so
        // nothing it does can stop the flyout or end the dock (WifiProperties.cs).
        return WifiProperties.Start(profile, adapter);
    }

    public void Disconnect()
    {
        if (client == HANDLE.NULL || !hasAdapter)
        {
            return;
        }
        var adapterId = adapter;
        _ = Wlan.WlanDisconnect(client, &adapterId, null);
    }

    public void Forget()
    {
        lock (gate)
        {
            attempt = null;
            attemptProfile = null;
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        Targets.TryRemove(number, out _);
        radio?.Dispose();
        if (client != HANDLE.NULL)
        {
            // Waits for a callback in flight, which only posts.
            _ = Wlan.WlanCloseHandle(client, null);
            client = HANDLE.NULL;
        }
    }

    // --- Inside ------------------------------------------------------------------

    private void FindAdapter()
    {
        void* list = null;
        if (Wlan.WlanEnumInterfaces(client, null, &list) != 0 || list == null)
        {
            hasAdapter = false;
            return;
        }
        try
        {
            var count = *(uint*)list;
            hasAdapter = false;
            for (uint i = 0; i < count; i++)
            {
                var info = (InterfaceInfo*)((byte*)list + 8 + i * sizeof(InterfaceInfo));
                // A connected interface first; else the first there is.
                if (!hasAdapter || info->State == 1)
                {
                    adapter = info->Id;
                    hasAdapter = true;
                }
            }
        }
        finally
        {
            Wlan.WlanFreeMemory(list);
        }
    }

    /// <summary>The software and hardware switches of the interface's radio: on when any radio is on in both.</summary>
    private bool? RadioOn()
    {
        var adapterId = adapter;
        uint size = 0;
        void* data = null;
        int kind = 0;
        if (Wlan.WlanQueryInterface(client, &adapterId, RadioStateOpcode, null, &size, &data, &kind) != 0 || data == null)
        {
            return null;
        }
        try
        {
            var phys = *(uint*)data;
            for (uint i = 0; i < Math.Min(phys, 64u); i++)
            {
                var state = (PhyRadioState*)((byte*)data + 4 + i * sizeof(PhyRadioState));
                if (state->Software == 1 && state->Hardware == 1)
                {
                    return true;
                }
            }
            return false;
        }
        finally
        {
            Wlan.WlanFreeMemory(data);
        }
    }

    /// <summary>The list's entries merged per SSID: the connected entry, else one with a profile, with the strongest signal.</summary>
    private static List<WifiNetwork> Networks(byte* list)
    {
        var count = *(uint*)list;
        var merged = new Dictionary<string, WifiNetwork>(StringComparer.Ordinal);
        var order = new List<string>();
        WifiNetwork? hidden = null;
        for (uint i = 0; i < count; i++)
        {
            var entry = (AvailableNetwork*)(list + 8 + i * sizeof(AvailableNetwork));
            var length = (int)Math.Min(entry->Ssid.Length, 32u);
            if (length == 0)
            {
                // A network that does not say its name: every one in range is one "Hidden network",
                // as Windows' list has them, with the strongest one's signal and security.
                var bars = WifiProfiles.Bars(entry->SignalQuality);
                if (hidden is null || bars > hidden.Bars)
                {
                    hidden = WifiProfiles.Hidden(bars, WifiProfiles.SecurityOf(entry->AuthAlgorithm, entry->CipherAlgorithm), entry->CipherAlgorithm, entry->AuthAlgorithm);
                }
                continue;
            }
            var bytes = new ReadOnlySpan<byte>(entry->Ssid.Bytes, length).ToArray();
            var key = Convert.ToHexString(bytes);
            var connected = (entry->Flags & 1) != 0;
            var profile = (entry->Flags & 2) != 0 ? new string(entry->ProfileName) : null;
            var network = new WifiNetwork(
                WifiProfiles.NameOf(bytes),
                bytes,
                WifiProfiles.Bars(entry->SignalQuality),
                WifiProfiles.SecurityOf(entry->AuthAlgorithm, entry->CipherAlgorithm),
                entry->CipherAlgorithm,
                connected,
                string.IsNullOrEmpty(profile) ? null : profile,
                Auth: entry->AuthAlgorithm);
            if (merged.TryGetValue(key, out var seen))
            {
                merged[key] = seen with
                {
                    Bars = Math.Max(seen.Bars, network.Bars),
                    Connected = seen.Connected || network.Connected,
                    Profile = network.Connected && network.Profile is not null ? network.Profile : seen.Profile ?? network.Profile,
                };
            }
            else
            {
                merged[key] = network;
                order.Add(key);
            }
        }
        List<WifiNetwork> networks = [.. order.Select(key => merged[key])];
        if (hidden is not null)
        {
            networks.Add(hidden);
        }
        return Sort(networks);
    }

    /// <summary>The connected network first, then by signal, then by name, as Windows' list has them.</summary>
    internal static List<WifiNetwork> Sort(List<WifiNetwork> networks) =>
        [.. networks.OrderByDescending(network => network.Connected).ThenByDescending(network => network.Bars).ThenBy(network => network.Ssid, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Saves <paramref name="xml"/> as a profile for all users, else for this user; its name, or null.</summary>
    private string? SetProfile(string xml, string name)
    {
        var adapterId = adapter;
        uint reason = 0;
        uint result;
        fixed (char* text = xml)
        {
            result = Wlan.WlanSetProfile(client, &adapterId, 0, text, null, BOOL.TRUE, null, &reason);
            if (result == ErrorAccessDenied)
            {
                result = Wlan.WlanSetProfile(client, &adapterId, ProfileUser, text, null, BOOL.TRUE, null, &reason);
            }
        }
        return result == 0 ? name : null;
    }

    /// <summary>Whether Windows has a profile named <paramref name="name"/> on the interface.</summary>
    private bool HasProfile(string name)
    {
        var adapterId = adapter;
        char* xml = null;
        uint flags = 0;
        uint access = 0;
        uint result;
        fixed (char* text = name)
        {
            result = Wlan.WlanGetProfile(client, &adapterId, text, null, &xml, &flags, &access);
        }
        if (xml != null)
        {
            Wlan.WlanFreeMemory(xml);
        }
        return result == 0;
    }

    /// <summary>Saves <paramref name="xml"/> as the credentials of the profile named <paramref name="profile"/>, for this person.</summary>
    private bool SetCredentials(string profile, string xml)
    {
        var adapterId = adapter;
        fixed (char* name = profile)
        fixed (char* text = xml)
        {
            return Wlan.WlanSetProfileEapXmlUserData(client, &adapterId, name, 0, text, null) == 0;
        }
    }

    /// <summary>A profile made for a key that didn't work goes again, so the wrong key isn't kept.</summary>
    private void Unmake()
    {
        if (madeProfile is not { } name || client == HANDLE.NULL)
        {
            return;
        }
        madeProfile = null;
        var adapterId = adapter;
        fixed (char* text = name)
        {
            _ = Wlan.WlanDeleteProfile(client, &adapterId, text, null);
        }
    }

    private void Settle(WifiAttempt outcome)
    {
        lock (gate)
        {
            attempt = outcome;
        }
        changes?.Add(WifiNews.Connection);
    }

    /// <summary>On the dock's thread: what the WLAN thread reported of the attempt in flight.</summary>
    private void Finish()
    {
        int reason;
        WifiAttempt? now;
        bool withKey;
        bool enterprise;
        bool withSignIn;
        lock (gate)
        {
            reason = finishedReason;
            finishedReason = -1;
            now = attempt;
            withKey = attemptWithKey;
            enterprise = attemptEnterprise;
            withSignIn = attemptWithSignIn;
        }
        if (reason < 0 || now is not { Outcome: WifiOutcome.Connecting })
        {
            return;
        }
        if (reason == 0)
        {
            madeProfile = null;
            Settle(new WifiAttempt(now.Ssid, WifiOutcome.Connected));
            return;
        }
        var security = reason is >= SecurityReasonsLow and <= SecurityReasonsHigh;
        Unmake();
        if (enterprise)
        {
            Settle(WifiProfiles.SignInEnded(now.Ssid, security, withSignIn));
            return;
        }
        Settle(new WifiAttempt(now.Ssid, security ? withKey ? WifiOutcome.WrongKey : WifiOutcome.NeedsKey : WifiOutcome.Failed,
            security && withKey ? S.DockQuickWifiWrongKey : security ? null : S.DockQuickWifiCantConnect));
    }

    /// <summary>On the WLAN service's thread.</summary>
    private void Heard(uint source, uint code, void* data, uint size)
    {
        if (source == NotificationSourceAcm)
        {
            switch (code)
            {
                case AcmScanComplete:
                case AcmScanFail:
                    scanning = false;
                    changes?.Add(WifiNews.Networks);
                    return;
                case AcmScanListRefresh:
                case AcmInterfaceArrival:
                case AcmInterfaceRemoval:
                case AcmDisconnected:
                    changes?.Add(WifiNews.Networks);
                    return;
                case AcmConnectionComplete:
                case AcmConnectionAttemptFail:
                {
                    // WLAN_CONNECTION_NOTIFICATION_DATA: the profile's name at 4, the reason code at 560.
                    if (data == null || size < 564)
                    {
                        return;
                    }
                    var profile = new string((char*)((byte*)data + 4));
                    var reason = *(int*)((byte*)data + 560);
                    lock (gate)
                    {
                        if (attempt is { Outcome: WifiOutcome.Connecting } && attemptProfile == profile)
                        {
                            finishedReason = reason;
                        }
                    }
                    changes?.Add(WifiNews.Connection | WifiNews.Networks);
                    return;
                }
            }
            return;
        }
        if (source == NotificationSourceMsm && code is MsmConnected or MsmDisconnected or MsmRadioStateChange or MsmSignalQualityChange)
        {
            changes?.Add(code == MsmRadioStateChange ? WifiNews.Radio : WifiNews.Networks);
        }
    }

    [UnmanagedCallersOnly]
    private static void Notified(SystemStatus.WlanNotification* data, void* context)
    {
        if (data == null || !Targets.TryGetValue((nint)context, out var wifi))
        {
            return;
        }
        try
        {
            wifi.Heard(data->Source, data->Code, data->Data, data->DataSize);
        }
        catch (Exception)
        {
            // Nothing may escape into the WLAN service's callback.
        }
    }

    // --- wlanapi.h -----------------------------------------------------------------

    /// <summary>WLAN_INTERFACE_INFO: 532 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 532)]
    internal struct InterfaceInfo
    {
        [FieldOffset(0)]
        public Guid Id;

        [FieldOffset(528)]
        public int State;
    }

    /// <summary>DOT11_SSID: 36 bytes.</summary>
    internal struct Dot11Ssid
    {
        public uint Length;
        public fixed byte Bytes[32];
    }

    /// <summary>WLAN_AVAILABLE_NETWORK: 628 bytes.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 628)]
    internal struct AvailableNetwork
    {
        [FieldOffset(0)]
        public fixed char ProfileName[256];

        [FieldOffset(512)]
        public Dot11Ssid Ssid;

        [FieldOffset(604)]
        public uint SignalQuality;

        [FieldOffset(608)]
        public int SecurityEnabled;

        [FieldOffset(612)]
        public int AuthAlgorithm;

        [FieldOffset(616)]
        public int CipherAlgorithm;

        [FieldOffset(620)]
        public uint Flags;
    }

    /// <summary>WLAN_PHY_RADIO_STATE: the PHY's index, then its software and hardware DOT11_RADIO_STATE (1 = on).</summary>
    internal struct PhyRadioState
    {
        public uint Index;
        public int Software;
        public int Hardware;
    }

    /// <summary>WLAN_CONNECTION_PARAMETERS: 40 bytes on x64.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    internal struct ConnectionParameters
    {
        [FieldOffset(0)]
        public int Mode;

        [FieldOffset(8)]
        public char* Profile;

        [FieldOffset(16)]
        public Dot11Ssid* Ssid;

        [FieldOffset(24)]
        public void* Bssids;

        [FieldOffset(32)]
        public int BssType;

        [FieldOffset(36)]
        public uint Flags;
    }
}

/// <summary>The Native Wifi calls the network flyout makes, which TerraFX does not carry.</summary>
internal static unsafe class Wlan
{
    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanOpenHandle(uint clientVersion, void* reserved, uint* negotiatedVersion, HANDLE* client);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanCloseHandle(HANDLE client, void* reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanRegisterNotification(HANDLE client, uint sources, BOOL ignoreDuplicate,
        delegate* unmanaged<SystemStatus.WlanNotification*, void*, void> callback, void* context, void* reserved, uint* previousSources);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanEnumInterfaces(HANDLE client, void* reserved, void** list);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanScan(HANDLE client, Guid* interfaceGuid, void* ssid, void* informationElement, void* reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanGetAvailableNetworkList(HANDLE client, Guid* interfaceGuid, uint flags, void* reserved, void** list);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanQueryInterface(HANDLE client, Guid* interfaceGuid, int opCode, void* reserved, uint* dataSize, void** data, int* valueType);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanConnect(HANDLE client, Guid* interfaceGuid, WindowsWifi.ConnectionParameters* parameters, void* reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanDisconnect(HANDLE client, Guid* interfaceGuid, void* reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanSetProfile(HANDLE client, Guid* interfaceGuid, uint flags, char* xml, char* security, BOOL overwrite, void* reserved, uint* reasonCode);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanDeleteProfile(HANDLE client, Guid* interfaceGuid, char* profileName, void* reserved);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanGetProfile(HANDLE client, Guid* interfaceGuid, char* profileName, void* reserved, char** xml, uint* flags, uint* grantedAccess);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanSetProfileEapXmlUserData(HANDLE client, Guid* interfaceGuid, char* profileName, uint flags, char* eapXmlUserData, void* reserved);

    /// <summary>The version WlanUIEditProfile is asked for (WLAN_UI_API_VERSION).</summary>
    public const uint UiVersion = 1;

    /// <summary>
    /// Windows' own properties dialog of a profile (WL_DISPLAY_PAGE: 0 connection, 1 security).
    /// wlanui.dll's, not wlanapi.dll's: wlanapi.dll does not export it, and asking it there threw
    /// EntryPointNotFoundException (WifiProperties.cs). Called only from Karpo.exe --wifi-properties.
    /// </summary>
    [DllImport("wlanui.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern uint WlanUIEditProfile(uint clientVersion, char* profileName, Guid* interfaceGuid, HWND owner, int startPage, void* reserved, uint* reasonCode);

    [DllImport("wlanapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static extern void WlanFreeMemory(void* memory);
}

/// <summary>
/// Made-up networks for pictures and tests: joining only pretends. "Rabbit Hole" takes "carrots!";
/// the hidden network, where there is one, is "Burrow" and takes "carrots!" too; "eduroam" signs
/// in as "alice" with "wonderland".
/// </summary>
internal sealed class SampleWifi : IWifiSource
{
    /// <summary>The name the made-up hidden network answers to.</summary>
    public const string HiddenSsid = "Burrow";

    /// <summary>Whose properties were asked for, in order.</summary>
    public List<string> PropertiesShown { get; } = [];

    /// <summary>Whether a network that does not say its name is in range.</summary>
    public bool Hidden { get; init; }

    private bool on = true;
    private string connected = "Rabbit Hole"; // not-ui: a sample network's name
    private WifiAttempt? attempt;
    private Changes? changes;
    private int scanning;

    /// <summary>The access the samples report, for a picture of the location message or of a PC without Wi-Fi.</summary>
    public WifiAccess Access { get; init; } = WifiAccess.Allowed;

    /// <summary>Whether the made-up scan finds anything: an empty list must look the same, scanning or not.</summary>
    public bool Networks { get; init; } = true;

    /// <summary>Only two networks, for the picture of a page whose floor holds it open.</summary>
    public bool Few { get; init; }

    /// <summary>
    /// Exactly this many networks, the connected one first, for the pictures and the measures of a
    /// list from one row to past the height it shows (FlyoutHeight); null for the usual five.
    /// </summary>
    public int? Many { get; init; }

    // The made-up networks Many takes its first so many of.
    private static readonly string[] ManyNames =
    [
        "Rabbit Hole", "Café Central Guest", "FRITZ!Box 7590 KL", "Vodafone-5G-2C1A", "Bibliothek", // not-ui: sample networks' names
        "Hotel Lobby", "Pixel 8 Hotspot", "Upstairs", "Garden", "Workshop", "Studio", "Bakery", // not-ui: sample networks' names
    ];

    public void Start(Changes news) => changes = news;

    /// <summary>Whether the made-up scan is still going (it ends on the second read, as a real one ends on its notification).</summary>
    public bool IsScanning => scanning > 0;

    public WifiState Read()
    {
        if (scanning > 0)
        {
            scanning--;
        }
        return ReadState();
    }

    // A PC without Wi-Fi has no radio to read or switch, as the real one reports it.
    private WifiState ReadState() => Access == WifiAccess.NoAdapter ? new(null, false, Access, [], false, null, null) : new(on, true, Access, on && Access == WifiAccess.Allowed && Networks
        ? Many is { } many
        ? [.. Enumerable.Range(0, many).Select(index => Sample(index < ManyNames.Length ? ManyNames[index] : $"Network {index + 1}", 4 - (index % 4), index % 3 == 1 ? WifiSecurity.Open : WifiSecurity.Wpa2Personal, index == 0 ? ManyNames[0] : null))] // not-ui: a sample network's name
        : Few
        ?
        [
            Sample("Rabbit Hole", 4, WifiSecurity.Wpa2Personal, "Rabbit Hole"), // not-ui: a sample network's name
            Sample("Café Central Guest", 3, WifiSecurity.Open, null), // not-ui: a sample network's name
        ]
        :
        [
            Sample("Rabbit Hole", 4, WifiSecurity.Wpa2Personal, "Rabbit Hole"), // not-ui: a sample network's name
            Sample("Café Central Guest", 3, WifiSecurity.Open, null), // not-ui: a sample network's name
            Sample("FRITZ!Box 7590 KL", 2, WifiSecurity.Wpa3Personal, null), // not-ui: a sample network's name
            Sample("eduroam", 2, WifiSecurity.Enterprise, null, auth: 6),
            Sample("DIRECT-4F-HP OfficeJet", 1, WifiSecurity.Wpa2Personal, null), // not-ui: a sample network's name
            .. Hidden && connected != HiddenSsid ? [WifiProfiles.Hidden(3, WifiSecurity.Wpa2Personal, 4, 7)] : (WifiNetwork[])[],
            .. Hidden && connected == HiddenSsid ? [Sample(HiddenSsid, 3, WifiSecurity.Wpa2Personal, HiddenSsid)] : (WifiNetwork[])[],
        ]
        : [], scanning > 0, attempt, null);

    private WifiNetwork Sample(string ssid, int bars, WifiSecurity security, string? profile, int auth = 0) =>
        new(ssid, System.Text.Encoding.UTF8.GetBytes(ssid), bars, security, 4, connected == ssid, profile, Auth: auth);

    public void Scan()
    {
        scanning = 2;
        changes?.Add(WifiNews.Networks);
    }

    public void SetRadio(bool value)
    {
        on = value;
        changes?.Add(WifiNews.Radio);
    }

    public void Connect(WifiNetwork network, string? key, bool automatic, WifiSignIn? signIn = null)
    {
        if (!WifiProfiles.CanJoin(network.Security, network.Auth))
        {
            attempt = new WifiAttempt(network.Ssid, WifiOutcome.Failed, WifiProfiles.CantJoin);
        }
        else if (network.Hidden && network.Ssid != HiddenSsid)
        {
            // No network in range answers to that name.
            attempt = new WifiAttempt(network.Ssid, WifiOutcome.Failed, S.DockQuickWifiCantConnect);
        }
        else if (network.Security == WifiSecurity.Enterprise)
        {
            attempt = signIn is null
                ? new WifiAttempt(network.Ssid, WifiOutcome.NeedsSignIn)
                : WifiProfiles.SignInProblem(signIn) is { } missing
                    ? new WifiAttempt(network.Ssid, WifiOutcome.WrongSignIn, missing)
                    : signIn is { User: "alice", Password: "wonderland" }
                        ? Joined(network.Ssid)
                        : WifiProfiles.SignInEnded(network.Ssid, security: true, withSignIn: true);
        }
        else if (key is null && network.Profile is null && WifiProfiles.NeedsKey(network.Security))
        {
            attempt = new WifiAttempt(network.Ssid, WifiOutcome.NeedsKey);
        }
        else if (key is not null && (WifiProfiles.KeyProblem(network.Security, key) ?? (key == "carrots!" ? null : S.DockQuickWifiWrongKey)) is { } wrong)
        {
            attempt = new WifiAttempt(network.Ssid, WifiOutcome.WrongKey, wrong);
        }
        else
        {
            attempt = Joined(network.Ssid);
        }
        changes?.Add(WifiNews.Connection);
    }

    private WifiAttempt Joined(string ssid)
    {
        connected = ssid;
        return new WifiAttempt(ssid, WifiOutcome.Connected);
    }

    public bool ShowProperties(WifiNetwork network)
    {
        if (network.Profile is null)
        {
            return false;
        }
        PropertiesShown.Add(network.Ssid);
        return true;
    }

    public void Disconnect()
    {
        connected = "";
        changes?.Add(WifiNews.Networks);
    }

    public void Forget() => attempt = null;

    public void Dispose()
    {
    }
}
