// The mobile hotspot, for its page (a line of the mobile hotspot preset) and the
// hotspot face, each with a source of its own: whether this PC can share its
// connection at all and why not, the switch, and what Windows' own hotspot page
// shows under "Properties" — the network's name, its password, its band — and
// the devices on it now.
//
// All of it is the tethering manager's (Windows.Networking.NetworkOperators),
// the WinRT class the hotspot widget has switched the hotspot through from the
// unpackaged AOT dock all along (Switches.cs, HotspotSwitch); measured again
// unpackaged on 2026-10-08, it answers its capability, its state, the access
// point's name, password and band, which bands the adapter takes, and the client
// count and list. What it is found from is the connection this PC is on: no
// connection, nothing to share, and the page says so.
//
// Nothing here is asked on the dock's thread: the look is live network-stack
// queries (8-11 ms warm, 60 ms the first time in a process — Switches.cs), so it
// goes to the pool, and only the plain record it builds crosses back. There is
// no event for a device joining, so the page asks again on its own wake while it
// is open; the network's own changes come with NetworkStatusChanged, and the hotspot switched in
// Windows' own panel with its adapter and address coming and going (the switch's own news,
// HotspotSwitch.Follow, InterfaceWatch; docs\widgets.md §439): read again at once, and once more a
// moment after the last of a burst, so the hotspot face no longer waits for its 30 s look.
//
// The password is copied with the panel's window as the clipboard's owner
// (Windows refuses text from an owner of none), through the source, so a test
// that presses Copy reaches a made-up hotspot and never the person's clipboard.

using Karpo.Core;
using TerraFX.Interop.Windows;
using Connectivity = Windows.Networking.Connectivity;
using Operators = Windows.Networking.NetworkOperators;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>The bands Windows' hotspot page offers: "Any available", 2.4, 5 and 6 GHz.</summary>
internal enum HotspotBand
{
    Any,
    TwoPointFour,
    Five,
    Six,
}

/// <summary>Whether this PC can host a hotspot now, and if not, why — each said in its own words.</summary>
internal enum HotspotCan
{
    /// <summary>Not known yet: the look is still going.</summary>
    Looking,

    Yes,

    /// <summary>This PC is on no connection, so there is nothing to share.</summary>
    NoConnection,

    /// <summary>The Wi-Fi adapter can't host one, or there is no Wi-Fi adapter.</summary>
    NoAdapter,

    /// <summary>An administrator turned it off.</summary>
    Policy,

    /// <summary>The connection's operator, this edition of Windows or a missing app does not allow it.</summary>
    NotAllowed,

    /// <summary>Windows says no and not why.</summary>
    Unknown,
}

/// <summary>A device on the hotspot: its name where it gave one, and its hardware address.</summary>
internal sealed record HotspotClient(string Name, string Address);

/// <param name="On">On, off, or null while the look goes or where Windows says neither.</param>
/// <param name="Band">The band the hotspot is set to; null where Windows doesn't say (before Windows 11).</param>
/// <param name="Clients">How many devices are on it now, and how many it takes.</param>
/// <param name="Busy">Switching on or off: the switch waits for Windows' answer.</param>
/// <param name="Problem">What went wrong the last time it was switched, in the language now.</param>
internal sealed record HotspotState(
    HotspotCan Can,
    bool? On,
    string Name,
    string Password,
    HotspotBand? Band,
    int Clients,
    int MaxClients,
    IReadOnlyList<HotspotClient> Devices,
    bool Busy = false,
    string? Problem = null)
{
    public static HotspotState Unknown { get; } = new(HotspotCan.Looking, null, "", "", null, 0, 0, []);
}

internal interface IHotspotSource : IDisposable
{
    void Start(Changes changes);

    HotspotState Read();

    /// <summary>Asks again: who is on it now. The page calls it on its wake.</summary>
    void Poll();

    /// <summary>Turns the hotspot on or off.</summary>
    void Switch(bool on);

    /// <summary>Puts <paramref name="text"/> (the password) on the clipboard, with <paramref name="owner"/> as its owner.</summary>
    void Copy(HWND owner, string text);
}

internal static class HotspotNews
{
    public const int State = 1;
}

/// <summary>What the flyout says about the hotspot. Pure: the tests check the words.</summary>
internal static class HotspotText
{
    /// <summary>The tethering manager's capability, as one of the page's reasons.</summary>
    public static HotspotCan From(Operators.TetheringCapability capability) => capability switch
    {
        Operators.TetheringCapability.Enabled => HotspotCan.Yes,
        Operators.TetheringCapability.DisabledByGroupPolicy => HotspotCan.Policy,
        Operators.TetheringCapability.DisabledByHardwareLimitation or Operators.TetheringCapability.DisabledBySystemCapability => HotspotCan.NoAdapter,
        Operators.TetheringCapability.DisabledByOperator or Operators.TetheringCapability.DisabledBySku
            or Operators.TetheringCapability.DisabledByRequiredAppNotInstalled => HotspotCan.NotAllowed,
        _ => HotspotCan.Unknown,
    };

    /// <summary>The page's notice where it can't host one: a line and why. Null where it can, or doesn't know yet.</summary>
    public static (string Title, string Detail)? Why(HotspotCan can) => can switch
    {
        HotspotCan.NoConnection => (S.DockQuickHotspotCant, S.DockQuickHotspotNoConnection),
        HotspotCan.NoAdapter => (S.DockQuickHotspotCant, S.DockQuickHotspotNoAdapter),
        HotspotCan.Policy => (S.DockQuickHotspotCant, S.DockQuickHotspotPolicy),
        HotspotCan.NotAllowed => (S.DockQuickHotspotCant, S.DockQuickHotspotNotAllowed),
        HotspotCan.Unknown => (S.DockQuickHotspotCant, S.DockQuickHotspotUnknown),
        _ => null,
    };

    public static string Band(HotspotBand band) => band switch
    {
        HotspotBand.TwoPointFour => S.DockQuickHotspotBand24,
        HotspotBand.Five => S.DockQuickHotspotBand5,
        HotspotBand.Six => S.DockQuickHotspotBand6,
        _ => S.DockQuickHotspotBandAny,
    };

    public static HotspotBand Band(Operators.TetheringWiFiBand band) => band switch
    {
        Operators.TetheringWiFiBand.TwoPointFourGigahertz => HotspotBand.TwoPointFour,
        Operators.TetheringWiFiBand.FiveGigahertz => HotspotBand.Five,
        Operators.TetheringWiFiBand.SixGigahertz => HotspotBand.Six,
        _ => HotspotBand.Any,
    };

    /// <summary>The password while it is hidden: a dot a character, as a password box shows one; nothing for none.</summary>
    public static string Hidden(string password) => new('●', password.Length);

    /// <summary>How many devices are on it, of how many it takes: "2 of 8"; just the count where Windows gives no most.</summary>
    public static string Devices(int clients, int most) =>
        most > 0 ? S.DockQuickHotspotDevicesOf(Math.Max(0, clients), most) : S.DockQuickHotspotDevicesCount(Math.Max(0, clients));

    /// <summary>A device's row: the name it gave, else its address.</summary>
    public static string Title(HotspotClient client) => client.Name is { Length: > 0 } name ? name : client.Address;
}

/// <summary>Windows' own: the tethering manager of the connection this PC is on.</summary>
internal sealed class WindowsHotspot : IHotspotSource
{
    private Changes? changes;
    private volatile HotspotState state = HotspotState.Unknown;
    private volatile Operators.NetworkOperatorTetheringManager? manager;
    private volatile bool busy;
    private volatile string? problem;
    private volatile bool disposed;
    private readonly Func<Action, IDisposable?> follow;
    private readonly Func<HotspotState>? stand;
    private readonly ITimer again;
    private IDisposable? following;

    // 1 while a read is waiting to start: the news that comes meanwhile is what it will read.
    private int looking;

    public WindowsHotspot()
        : this(HotspotSwitch.Follow, null, TimeProvider.System)
    {
    }

    /// <param name="follow">Starts following what tells of the hotspot (HotspotSwitch.Follow): its news comes from any thread.</param>
    /// <param name="stand">Tests only: what a read finds, instead of asking Windows.</param>
    /// <param name="clock">What the read once more after the news (<see cref="HotspotSwitch.AgainMs"/>) is timed by.</param>
    internal WindowsHotspot(Func<Action, IDisposable?> follow, Func<HotspotState>? stand, TimeProvider clock)
    {
        this.follow = follow;
        this.stand = stand;
        again = clock.CreateTimer(static self => ((WindowsHotspot)self!).Poll(), this, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Start(Changes news)
    {
        changes = news;
        Poll();
        following = follow(Heard);
    }

    /// <summary>
    /// The network said something changed, from any thread — the connection, or an interface or an
    /// address, which is all a hotspot switched in Windows' own panel tells: read again, and once more
    /// <see cref="HotspotSwitch.AgainMs"/> after the last of a burst, when the hotspot has settled.
    /// </summary>
    internal void Heard()
    {
        Poll();
        _ = again.Change(TimeSpan.FromMilliseconds(HotspotSwitch.AgainMs), Timeout.InfiniteTimeSpan);
    }

    public HotspotState Read() => state with { Busy = busy, Problem = problem };

    /// <summary>Read on the pool, and said; one read for a burst of asks, since one waiting to start reads what the rest would.</summary>
    public void Poll()
    {
        if (Interlocked.Exchange(ref looking, 1) == 1)
        {
            return;
        }
        _ = Task.Run(() => Troubles.Guard("Quick settings: reading the mobile hotspot", () =>
        {
            // What is heard from here on is after what this reads: it asks for a read of its own.
            Volatile.Write(ref looking, 0);
            if (disposed)
            {
                return;
            }
            state = stand?.Invoke() ?? Look();
            changes?.Add(HotspotNews.State);
        }));
    }

    public void Switch(bool on)
    {
        if (busy)
        {
            return;
        }
        busy = true;
        problem = null;
        changes?.Add(HotspotNews.State);
        _ = Task.Run(async () =>
        {
            try
            {
                if (manager is { } tethering)
                {
                    var result = on ? await tethering.StartTetheringAsync() : await tethering.StopTetheringAsync();
                    if (result.Status != Operators.TetheringOperationStatus.Success)
                    {
                        problem = S.DockQuickHotspotFailed(WindowsBluetooth.Words(result.Status.ToString()));
                    }
                }
            }
            catch (Exception error)
            {
                problem = S.DockQuickHotspotRefused;
                Troubles.Note("Quick settings: switching the mobile hotspot", error);
            }
            busy = false;
            if (!disposed)
            {
                state = stand?.Invoke() ?? Look();
                changes?.Add(HotspotNews.State);
            }
        });
    }

    public void Copy(HWND owner, string text) => Controls.Clipboard.SetText(owner, text);

    public void Dispose()
    {
        disposed = true;
        following?.Dispose();
        following = null;
        again.Dispose();
        manager = null;
    }

    /// <summary>Everything the page shows, asked of Windows. Never on the dock's thread.</summary>
    private HotspotState Look()
    {
        Connectivity.ConnectionProfile? profile;
        try
        {
            profile = Connectivity.NetworkInformation.GetInternetConnectionProfile();
        }
        catch (Exception)
        {
            profile = null;
        }
        if (profile is null)
        {
            manager = null;
            return HotspotState.Unknown with { Can = HotspotCan.NoConnection };
        }
        HotspotCan can;
        try
        {
            can = HotspotText.From(Operators.NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(profile));
        }
        catch (Exception)
        {
            can = HotspotCan.Unknown;
        }
        if (can != HotspotCan.Yes)
        {
            manager = null;
            return HotspotState.Unknown with { Can = can };
        }
        Operators.NetworkOperatorTetheringManager tethering;
        try
        {
            tethering = Operators.NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        }
        catch (Exception error)
        {
            manager = null;
            Troubles.Note("Quick settings: finding the mobile hotspot", error);
            return HotspotState.Unknown with { Can = HotspotCan.Unknown };
        }
        manager = tethering;
        bool? on = null;
        try
        {
            on = tethering.TetheringOperationalState switch
            {
                Operators.TetheringOperationalState.On => true,
                Operators.TetheringOperationalState.Off => false,
                _ => null,
            };
        }
        catch (Exception)
        {
        }
        string name = "";
        string password = "";
        HotspotBand? band = null;
        try
        {
            var point = tethering.GetCurrentAccessPointConfiguration();
            name = point.Ssid ?? "";
            password = point.Passphrase ?? "";
            try
            {
                // Windows 11's; an older Windows has no band to say.
                band = HotspotText.Band(point.Band);
            }
            catch (Exception)
            {
            }
        }
        catch (Exception error)
        {
            Troubles.Note("Quick settings: reading the mobile hotspot's network", error);
        }
        var clients = 0;
        var most = 0;
        List<HotspotClient> devices = [];
        try
        {
            clients = (int)tethering.ClientCount;
            most = (int)tethering.MaxClientCount;
            if (on == true && clients > 0)
            {
                foreach (var client in tethering.GetTetheringClients())
                {
                    var host = "";
                    try
                    {
                        host = client.HostNames.Select(each => each.DisplayName).FirstOrDefault(each => !string.IsNullOrWhiteSpace(each)) ?? "";
                    }
                    catch (Exception)
                    {
                    }
                    devices.Add(new HotspotClient(host, client.MacAddress ?? ""));
                }
            }
        }
        catch (Exception error)
        {
            Troubles.Note("Quick settings: listing the mobile hotspot's devices", error);
        }
        return new HotspotState(HotspotCan.Yes, on, name, password, band, clients, most, devices);
    }
}

/// <summary>A made-up hotspot: what the tests and the pictures use. It shares nothing and copies nothing.</summary>
internal sealed class SampleHotspot : IHotspotSource
{
    private Changes? changes;

    public SampleHotspot(
        HotspotCan can = HotspotCan.Yes,
        bool on = true,
        IEnumerable<HotspotClient>? devices = null,
        HotspotBand? band = HotspotBand.Five,
        string? problem = null)
    {
        Can = can;
        On = on;
        Band = band;
        Problem = problem;
        Devices = devices is null
            ?
            [
                new("Pixel-8", "a4:c3:f0:12:7e:01"), // not-ui: a sample device's name
                new("", "3c:22:fb:8a:10:9d"), // not-ui: a sample device's address
            ]
            : [.. devices];
    }

    public HotspotCan Can { get; }

    public bool On { get; private set; }

    public HotspotBand? Band { get; }

    public string? Problem { get; }

    public List<HotspotClient> Devices { get; }

    public string Name { get; init; } = "DESKTOP-KARPO 4821"; // not-ui: a sample network's name

    public string Password { get; init; } = "7x#Lq92m"; // not-ui: a sample password

    /// <summary>Every switch it was asked for, in order.</summary>
    public List<bool> Switched { get; } = [];

    /// <summary>Every text it was asked to copy, in order: what the person's clipboard would have got.</summary>
    public List<string> Copied { get; } = [];

    public int Polls { get; private set; }

    public void Start(Changes news) => changes = news;

    public HotspotState Read() => Can == HotspotCan.Yes
        ? new HotspotState(Can, On, Name, Password, Band, On ? Devices.Count : 0, 8, On ? Devices : [], Problem: Problem)
        : HotspotState.Unknown with { Can = Can };

    public void Poll() => Polls++;

    public void Switch(bool on)
    {
        Switched.Add(on);
        On = on;
        changes?.Add(HotspotNews.State);
    }

    public void Copy(HWND owner, string text) => Copied.Add(text);

    public void Dispose()
    {
    }
}
