// Bluetooth for the Bluetooth flyout: the radio (Windows.Devices.Radios), and
// the paired devices (DeviceWatcher over association endpoints, which covers
// classic Bluetooth and Bluetooth LE and reports connections as they change).
// While the flyout is open it watches them; every watcher stops when it closes.
// It looks for no new devices: finding and pairing one is Windows' Bluetooth
// settings', which the preset's "Add device" opens (SettingsActions.AddDeviceId). Removing a
// device is unpairing. A paired audio device is connected and disconnected as
// Windows' own page does it, through its audio driver (BluetoothAudio.cs); other
// devices have no such request in Windows, and the flyout offers none.
//
// NO MANAGED COLLECTION IS EVER HANDED TO A WinRT CALL HERE. That is the rule
// this file exists to keep, and it is why the queries below look the way they do.
//
// This is the bug the user reported as "it just shows no devices" on a PC with a
// headset connected. CreateWatcher(aqs, additionalProperties, kind) takes an
// IIterable<string>, and handing it any managed collection means asking the
// runtime to wrap that collection for COM on the spot. The published bar is
// native AOT: there is no wrapper to make and none was generated, so the call
// threw before a single device was listed —
//
//   InvalidCastException: Failed to create a CCW for object of type
//   'System.String[]' for interface with IID 'E2FCC7C1-3BFC-5A0B-B2B0-72E769D1CB7E'
//   (HResult 0x80004002)
//
// — and it threw for every shape of collection, measured one after another in a
// published smoke run: a string[] field, an inline new[], a List<string>, and a
// collection expression (which is its own compiler-generated list type and fails
// with that type's name). Only null works, and the reason none of them work is
// that nothing is generated for any of them: build with
// -p:EmitCompilerGeneratedFiles=true and the CsWinRT AOT generator has written no
// files at all for this project, while the Json and Regex generators have written
// hundreds. None of it reproduces under the JIT, which builds the wrapper on
// demand, so every test and every probe was green while the shipped bar showed
// nothing. docs\widgets.md section 10 is the short version for the rest of the dock.
//
// So the properties are not asked for. What they used to say, the QUERY says
// instead: one watcher per answer (paired and connected, paired and not), so
// which watcher found a device tells us what the property would have. An
// endpoint carries its name, whether it is paired and its container without
// being asked, and those are the rest of what a row needs.
//
// Where a fact has no query to hide behind, the way out is to leave WinRT rather
// than to argue with it. Two of them do: what kind of thing a device is (which was
// Cod.Major and the LE appearance) and its battery percentage. Both come from the
// device's own PnP nodes through SetupAPI instead (BluetoothNodes.cs), which has
// no ABI edge to fall off, in one walk costing 0.42 ms a device. A device whose
// nodes name nothing keeps the plain Bluetooth rune rather than being guessed at,
// and the made-up devices the offscreen pictures draw go through the same GlyphFor
// as the real ones, so a picture cannot show a row the panel could not.
//
// The other rule, kept from the round before: every WinRT object is read on the
// thread it arrived on, and nothing but plain records crosses to the dock. The
// watcher raises its events on the pool; the dock's own thread is an STA
// (Program.Main is [STAThread]). Added/Updated build the record where the object
// is, the lock holds records only, Read() copies them, and pairing hops to the
// pool. Failures are reported (BluetoothState.Problem) and logged with their type
// and HResult (Karpo.Core\Troubles.cs) rather than swallowed, which is how this
// one was finally caught.

using System.Collections.Immutable;
using Karpo.Core;
using Enumeration = Windows.Devices.Enumeration;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>A device as the flyout lists it.</summary>
/// <param name="Battery">
/// Its charge as a percentage, or null where it does not say — which most devices do not
/// (BluetoothBattery). Null is not zero, and a row that shows "0%" for a headset that said
/// nothing is worse than a row that shows nothing.
/// </param>
/// <param name="Audio">
/// Whether it is an audio device — its nodes are sound's — which is what Windows connects and
/// disconnects from its page (BluetoothAudio). False until its nodes have been read.
/// </param>
internal sealed record BluetoothDevice(string Id, string Name, string Glyph, bool Paired, bool Connected, int? Battery = null, bool Audio = false);

/// <param name="RadioOn">Null when there is no Bluetooth radio.</param>
/// <param name="CanSwitch">False once Windows refused to switch the radio for Karpo.</param>
/// <param name="Busy">The device being removed, connected or disconnected.</param>
/// <param name="Connecting">What is being asked of <paramref name="Busy"/>: to connect, to disconnect, or null for removing it.</param>
internal sealed record BluetoothState(bool? RadioOn, bool CanSwitch, IReadOnlyList<BluetoothDevice> Devices, string? Busy, string? Problem, bool? Connecting = null);

internal interface IBluetoothSource : IDisposable
{
    void Start(Changes changes);

    BluetoothState Read();

    void SetRadio(bool on);

    void Remove(string id);

    /// <summary>Connects or disconnects a paired audio device, as Windows' page does (BluetoothAudio).</summary>
    void Connect(string id, bool connect);

    /// <summary>Asks the connected devices for their battery again; the page calls it on its wake.</summary>
    void Poll();
}

internal static class BluetoothNews
{
    public const int Devices = 1;
    public const int Radio = 2;
}

internal sealed class WindowsBluetooth : IBluetoothSource
{
    // Association-endpoint protocols: classic Bluetooth, and Bluetooth LE.
    private const string Classic = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string LowEnergy = "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";

    private const string IsConnected = "System.Devices.Aep.IsConnected";
    private const string IsPaired = "System.Devices.Aep.IsPaired";

    /// <summary>
    /// What each watcher looks for. The facts a row needs are in the query rather than in a list
    /// of properties, because a list of properties cannot cross to WinRT here (see the top of this
    /// file): which watcher found a device says whether it is paired and whether it is connected.
    /// </summary>
    /// <param name="Paired">What the devices it finds are; the row takes it from here.</param>
    /// <param name="Connected">The same for the connection, and null where the query does not say.</param>
    internal readonly record struct Search(string Query, bool Paired, bool? Connected);

    internal static readonly Search[] Searches =
    [
        // Paired, split by connection: a headset connecting leaves one watcher and joins the other,
        // which is how the row learns it without anybody asking for a property.
        new($"({Both}) AND {IsPaired}:=System.StructuredQueryType.Boolean#True AND {IsConnected}:=System.StructuredQueryType.Boolean#True", true, true),
        new($"({Both}) AND {IsPaired}:=System.StructuredQueryType.Boolean#True AND {IsConnected}:=System.StructuredQueryType.Boolean#False", true, false),
        // Nothing unpaired: the flyout lists paired devices only, as the person asked, and a new
        // one is found and paired in Windows' own "Add a device" dialog. Not looking also keeps
        // the radio from scanning for as long as the flyout is open.
    ];

    /// <summary>Either kind of Bluetooth: classic, and low energy.</summary>
    private const string Both =
        $"System.Devices.Aep.ProtocolId:=\"{Classic}\" OR System.Devices.Aep.ProtocolId:=\"{LowEnergy}\""; // not-ui: a device query

    private readonly object gate = new();

    /// <summary>What the dock is told: plain records, built on the thread the device arrived on.</summary>
    private readonly BluetoothList list = new();

    /// <summary>
    /// The WinRT objects behind them, for pairing and for the battery. Only ever touched off the
    /// dock's thread (see the top of this file); the lock guards the dictionary, not the objects.
    /// </summary>
    private readonly Dictionary<string, Enumeration.DeviceInformation> found = [];

    /// <summary>Which container each device's nodes are in, which is where its own facts live.</summary>
    private readonly Dictionary<string, Guid> containers = [];

    /// <summary>The devices a read of their nodes is already going for, so a rebuild does not ask twice.</summary>
    private readonly HashSet<string> asking = [];
    private Changes? changes;
    private readonly List<Enumeration.DeviceWatcher> watchers = [];
    private RadioSwitch? radio;
    private volatile bool canSwitch = true;

    private volatile string? busy;
    private volatile string? problem;
    private volatile bool disposed;

    public void Start(Changes news)
    {
        changes = news;
        radio = new RadioSwitch(Radios.RadioKind.Bluetooth, () => news.Add(BluetoothNews.Radio));
        radio.Find();
        foreach (var search in Searches)
        {
            if (Watch(search) is { } watcher)
            {
                watchers.Add(watcher);
            }
        }
    }

    /// <summary>
    /// What the dock draws. Plain records, copied out of the lock: nothing here reads a WinRT
    /// object, because this runs on the dock's own thread and those objects belong to another
    /// apartment (see the top of this file).
    /// </summary>
    public BluetoothState Read()
    {
        // Still looking for the radio: shown as on until it says otherwise.
        bool? on = radio is { Looked: false } ? true : radio?.On;
        List<BluetoothDevice> devices;
        lock (gate)
        {
            devices = list.Read(Environment.TickCount64);
        }
        Settle(devices);
        return new BluetoothState(on, canSwitch && radio is { Found: true }, Sort(devices), busy, problem, pending?.Connect);
    }

    /// <summary>How long a connection asked for is waited for before the row says it did not come.</summary>
    private const int ConnectMs = 20_000;

    /// <summary>The connection asked of a device, and until when it is waited for: the row says "Connecting…" meanwhile.</summary>
    private volatile Pending? pending;

    private sealed record Pending(string Id, bool Connect, long Until);

    /// <summary>
    /// A connection asked for is over once the device is where it was asked to be — the watcher
    /// says so, as for any other connection — or once it has been waited for long enough.
    /// </summary>
    private void Settle(List<BluetoothDevice> devices)
    {
        if (pending is not { } asked)
        {
            return;
        }
        var device = devices.FirstOrDefault(row => row.Id == asked.Id);
        var arrived = device is null || device.Connected == asked.Connect;
        if (!arrived && Environment.TickCount64 < asked.Until)
        {
            return;
        }
        pending = null;
        if (busy == asked.Id)
        {
            busy = null;
        }
        if (!arrived && asked.Connect)
        {
            problem = S.DockQuickBluetoothNotArrived;
        }
    }

    public void Connect(string id, bool connect)
    {
        Guid container;
        lock (gate)
        {
            containers.TryGetValue(id, out container);
        }
        problem = null;
        busy = id;
        pending = new Pending(id, connect, Environment.TickCount64 + ConnectMs);
        changes?.Add(BluetoothNews.Devices);
        // On the pool: the audio driver is reached through the audio service (BluetoothAudio).
        _ = Task.Run(() => Troubles.Guard("Bluetooth: connecting or disconnecting a device", () =>
        {
            if (BluetoothAudio.Ask(container, connect) == 0)
            {
                pending = null;
                busy = null;
                problem = connect ? S.DockQuickBluetoothConnectRefused : S.DockQuickBluetoothDisconnectRefused;
                changes?.Add(BluetoothNews.Devices);
                return;
            }
            // The watcher hears the connection come or go; this is for one that never does.
            _ = Task.Delay(ConnectMs + 250).ContinueWith(_ => Troubles.Guard("Bluetooth: looking again after a connection", () => changes?.Add(BluetoothNews.Devices)), TaskScheduler.Default);
        }));
    }

    /// <summary>Connected first, then the other paired ones, by name; then the ones that can be paired, by name.</summary>
    internal static List<BluetoothDevice> Sort(List<BluetoothDevice> devices) =>
        [.. devices.OrderByDescending(device => device.Paired).ThenByDescending(device => device.Connected).ThenBy(device => device.Name, StringComparer.OrdinalIgnoreCase)];

    public void SetRadio(bool on)
    {
        problem = null;
        _ = Switch(on);
    }

    private async Task Switch(bool on)
    {
        if (radio is not { } switcher || !await switcher.Set(on))
        {
            Refused(on);
            return;
        }
        changes?.Add(BluetoothNews.Radio);
    }

    public void Remove(string id) => _ = Unpair(id);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        foreach (var watcher in watchers)
        {
            Stop(watcher);
        }
        watchers.Clear();
        radio?.Dispose();
        lock (gate)
        {
            found.Clear();
        }
    }

    // --- Inside ------------------------------------------------------------------

    private void Refused(bool on)
    {
        canSwitch = false;
        problem = on ? S.DockQuickBluetoothOnRefused : S.DockQuickBluetoothOffRefused;
        changes?.Add(BluetoothNews.Radio);
    }

    private async Task Unpair(string id)
    {
        Enumeration.DeviceInformation? device;
        lock (gate)
        {
            found.TryGetValue(id, out device);
        }
        if (device is null)
        {
            problem = S.DockQuickBluetoothGone;
            changes?.Add(BluetoothNews.Devices);
            return;
        }
        busy = id;
        problem = null;
        changes?.Add(BluetoothNews.Devices);
        // Off the dock's thread: this is asked for from a click, and the device belongs to the
        // apartment it arrived in (see the top of this file).
        await Task.Run(async () =>
        {
        try
        {
            var result = await device.Pairing.UnpairAsync();
            var gone = result.Status is Enumeration.DeviceUnpairingResultStatus.Unpaired or Enumeration.DeviceUnpairingResultStatus.AlreadyUnpaired;
            problem = gone ? null : S.DockQuickBluetoothRemoveFailed(Words(result.Status.ToString()));
            if (gone)
            {
                // Unpaired here: nothing to wait for, as for a device only moving between watchers.
                lock (gate)
                {
                    list.Forget(id);
                }
            }
        }
        catch (Exception error)
        {
            problem = error.Message;
        }
        }).ConfigureAwait(false);
        busy = null;
        changes?.Add(BluetoothNews.Devices);
    }

    /// <summary>"AuthenticationTimeout" → "authentication timeout".</summary>
    internal static string Words(string name) =>
        string.Concat(name.Select((c, index) => index > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    private Enumeration.DeviceWatcher? Watch(Search search)
    {
        try
        {
            // No properties: a managed collection cannot cross to WinRT here (see the top of this
            // file), and the query has already said what they would have.
            var watcher = Enumeration.DeviceInformation.CreateWatcher(search.Query, null, Enumeration.DeviceInformationKind.AssociationEndpoint);
            // Added, Updated and Removed all arrive on the pool, which is where the device's own
            // properties can be read. The record is built here and only the record is kept.
            watcher.Added += (_, device) =>
            {
                Keep(device, search);
                changes?.Add(BluetoothNews.Devices);
            };
            watcher.Updated += (_, update) =>
            {
                Enumeration.DeviceInformation? device;
                lock (gate)
                {
                    found.TryGetValue(update.Id, out device);
                }
                if (device is null)
                {
                    return;
                }
                try
                {
                    device.Update(update);
                }
                catch (Exception error)
                {
                    // A device that goes mid-update is the ordinary case for Bluetooth.
                    Trouble("reading a device", S.DockQuickBluetoothReadFailed, error);
                    return;
                }
                Keep(device, search);
                changes?.Add(BluetoothNews.Devices);
            };
            watcher.Removed += (_, update) =>
            {
                // Not a device gone: one connecting leaves this watcher for the other (BluetoothList),
                // and keeps its object and its container meanwhile. Looked at again once it has been
                // waited for, which is when one nobody took back leaves the list.
                lock (gate)
                {
                    list.Gone(search, update.Id, Environment.TickCount64);
                }
                changes?.Add(BluetoothNews.Devices);
                Task.Delay(BluetoothList.MovingMs + 50).ContinueWith(waited => Troubles.Guard("Bluetooth: looking again after a device left a watcher", () => changes?.Add(BluetoothNews.Devices)), TaskScheduler.Default);
            };
            watcher.Start();
            return watcher;
        }
        catch (Exception error)
        {
            // Silence here is what made the panel say "no devices" with no way to tell why.
            Trouble("watching the paired devices", S.DockQuickBluetoothWatchFailed, error);
            return null;
        }
    }

    /// <summary>
    /// The device as a row, built <b>on the thread it arrived on</b>, and kept. A device that
    /// answers nothing worth listing is dropped; one that throws while being read is a device that
    /// went away mid-read, which for Bluetooth is ordinary and not a fault.
    /// </summary>
    private void Keep(Enumeration.DeviceInformation device, Search search)
    {
        string id;
        BluetoothDevice? row;
        var container = Guid.Empty;
        try
        {
            id = device.Id;
            row = Row(device, search);
            if (device.Properties.TryGetValue(BluetoothNodes.Container, out var value) && value is Guid found)
            {
                container = found;
            }
        }
        catch (Exception error)
        {
            Trouble("reading a device", S.DockQuickBluetoothReadFailed, error);
            return;
        }
        var ask = false;
        lock (gate)
        {
            found[id] = device;
            if (container != Guid.Empty)
            {
                containers[id] = container;
            }
            if (list.Seen(search, id, row) is { } kept)
            {
                // Walked once for the kind, which a paired device has whether it is awake or not,
                // and again while it is connected for the level, which only a connected one gives.
                ask = kept.Paired && (!list.Walked(id) || (kept.Connected && kept.Battery is null));
            }
            else
            {
                containers.Remove(id);
            }
        }
        if (ask)
        {
            Ask(id);
        }
    }

    /// <summary>
    /// Reads <paramref name="id"/>'s own device nodes off the dock's thread — what kind of thing
    /// it is, and what its battery says — and keeps what they answer. Nothing is shown for a
    /// device that says nothing, and nothing is asked twice at once. Called when a device turns
    /// up or connects, and again while the panel is open (<see cref="Poll"/>).
    /// </summary>
    private void Ask(string id)
    {
        Guid container;
        lock (gate)
        {
            if (!containers.TryGetValue(id, out container) || !asking.Add(id))
            {
                return;
            }
        }
        _ = Task.Run(() => Troubles.Guard("Bluetooth: reading a device's battery and kind", () =>
        {
            var said = BluetoothNodes.Read(container);
            var glyph = GlyphFor(said.Kinds);
            var audio = IsAudio(said.Kinds);
            bool news;
            lock (gate)
            {
                asking.Remove(id);
                news = list.Learned(id, said.Level, glyph, audio);
            }
            if (news)
            {
                changes?.Add(BluetoothNews.Devices);
            }
        }));
    }

    /// <summary>
    /// Asks every connected device for its battery again. The page calls this on its own wake
    /// while it is open; nothing polls while it is closed, because the page is gone and its
    /// service with it.
    /// </summary>
    public void Poll()
    {
        List<string> connected;
        lock (gate)
        {
            connected = [.. list.Read(Environment.TickCount64).Where(row => row.Connected).Select(row => row.Id)];
        }
        foreach (var id in connected)
        {
            Ask(id);
        }
    }

    /// <summary>
    /// Something Windows refused. The panel gets a line it can show, and the log gets the whole of
    /// it — type, message and HResult (Karpo.Core\Troubles.cs) — because the one thing this
    /// service must never do again is fail where nobody can find out what failed.
    /// </summary>
    /// <param name="what">What failed, for the log, in English.</param>
    /// <param name="shown">The panel's line for it, in the language now, with Windows' message in it.</param>
    private void Trouble(string what, Func<string, string> shown, Exception error)
    {
        var said = shown(error.Message);
        if (problem != said)
        {
            problem = said;
            Troubles.Note($"Bluetooth: {what}", error);
            changes?.Add(BluetoothNews.Devices);
        }
    }

    private static void Stop(Enumeration.DeviceWatcher? watcher)
    {
        try
        {
            if (watcher is { Status: Enumeration.DeviceWatcherStatus.Started or Enumeration.DeviceWatcherStatus.EnumerationCompleted })
            {
                watcher.Stop();
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>A found device as a row; null for one not worth listing (no name, or an LE device that can't be connected to).</summary>
    /// <summary>
    /// A found device as a row, or null for one not worth listing (it has no name). What it is —
    /// paired, connected — comes from <paramref name="search"/>: the query asked for exactly those
    /// devices, so the answer is known without reading a property that cannot be asked for.
    /// </summary>
    private static BluetoothDevice? Row(Enumeration.DeviceInformation device, Search search)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(device.Name))
            {
                return null;
            }
            // Its name and whether it is paired come with an endpoint; the connection comes from
            // the query. The device's kind does not: that was Cod.Major, which needed the property
            // list, so every row wears the plain Bluetooth glyph for now.
            var paired = device.Properties.TryGetValue(IsPaired, out var value) && value is bool told ? told : search.Paired;
            return new BluetoothDevice(device.Id, device.Name, GlyphFor([]), paired, search.Connected ?? false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// A glyph for a device, from the setup classes of its own PnP nodes (BluetoothNodes.Read).
    /// The first class that names something wins; a device whose nodes name nothing keeps the
    /// plain Bluetooth rune, because a wrong picture of a thing is worse than no picture of it.
    /// <para>
    /// The made-up devices go through this too (SampleBluetooth), so an offscreen picture cannot
    /// quietly show a row the real panel could never draw.
    /// </para>
    /// </summary>
    internal static string GlyphFor(IReadOnlyList<Guid> classes)
    {
        foreach (var kind in classes)
        {
            foreach (var (named, glyph) in Kinds)
            {
                if (kind == named)
                {
                    return glyph;
                }
            }
        }
        return Glyph(0xE702);
    }

    /// <summary>
    /// Whether a device's nodes are sound's — Media, or an audio endpoint — which is what Windows
    /// connects and disconnects from its page (BluetoothAudio). A device whose nodes say nothing is
    /// not guessed to be one.
    /// </summary>
    internal static bool IsAudio(IReadOnlyList<Guid> classes) => classes.Contains(Media) || classes.Contains(AudioEndpoint);

    internal static readonly Guid Media = new("4D36E96C-E325-11CE-BFC1-08002BE10318");
    internal static readonly Guid AudioEndpoint = new("C166523C-FE0C-4A94-A586-F1A80CFBBF3E");
    internal static readonly Guid Mouse = new("4D36E96F-E325-11CE-BFC1-08002BE10318");
    internal static readonly Guid Keyboard = new("4D36E96B-E325-11CE-BFC1-08002BE10318");
    internal static readonly Guid Printer = new("4D36E979-E325-11CE-BFC1-08002BE10318");
    internal static readonly Guid Imaging = new("6BDD1FC6-810F-11D0-BEC7-08002BE2092F");

    /// <summary>What a Bluetooth device's nodes all carry, and so what none of them is named by.</summary>
    internal static readonly Guid Anything = new("E0CBF06C-CD8B-4647-BB8A-263B43F0F974");

    /// <summary>
    /// The device setup classes that say what a Bluetooth device is. A headset's nodes are Media,
    /// System and Bluetooth; a mouse's are Mouse and HIDClass. The ones left out are left out on
    /// purpose: System and Bluetooth are on everything, and HIDClass is on a mouse and a keyboard
    /// alike, so it names nothing on its own.
    /// <para>
    /// Below the fields it reads, and it has to stay there: a static field is still its type's
    /// default when a field declared earlier is initialised, so this list would be six empty
    /// GUIDs and every device would wear the plain rune.
    /// </para>
    /// </summary>
    private static readonly (Guid Class, string Glyph)[] Kinds =
    [
        (Media, Glyph(0xE7F6)),
        (AudioEndpoint, Glyph(0xE7F6)),
        (Mouse, Glyph(0xE962)),
        (Keyboard, Glyph(0xE765)),
        (Printer, Glyph(0xE749)),
        (Imaging, Glyph(0xE749)),
    ];

    private static string Glyph(int code) => char.ConvertFromUtf32(code);
}

/// <summary>
/// The paired devices as the watchers have reported them: plain records only, no WinRT object, so
/// the tests can feed it what a connection reports. Not locked: its owner holds the lock.
/// <para>
/// A device is listed while any watcher has it, and connected while the connected ones' has it.
/// The watchers are split by connection (WindowsBluetooth.Searches), so a device connecting leaves
/// one and joins the other, each on its own pool thread and in either order: what one watcher no
/// longer has is not a device gone. One that no watcher has is waited for (<see cref="MovingMs"/>)
/// as it was, in its place, before it leaves the list. That wait is what the user saw missing:
/// "when connecting a device it shortly disappears from the bluetooth device list" (§381).
/// </para>
/// </summary>
internal sealed class BluetoothList
{
    /// <summary>How long a device no watcher has is kept for one to take it back; the move takes a few ms.</summary>
    public const int MovingMs = 2_000;

    /// <param name="Held">The watchers that have it.</param>
    /// <param name="Left">When the last of them let it go, while none has it.</param>
    /// <param name="Walked">
    /// Whether its nodes have been read at least once. What that read gives is a kind, which never
    /// changes, and a level, which does: this is what stops a device being walked again for the
    /// kind it already has every time it reports anything at all.
    /// </param>
    private sealed record Entry(BluetoothDevice Row, ImmutableHashSet<WindowsBluetooth.Search> Held, long? Left = null, bool Walked = false);

    private readonly Dictionary<string, Entry> entries = [];

    /// <summary>
    /// <paramref name="search"/>'s watcher found or updated <paramref name="id"/>, as
    /// <paramref name="row"/> (null for a device not worth listing). Returns the row as it is
    /// listed now.
    /// </summary>
    public BluetoothDevice? Seen(WindowsBluetooth.Search search, string id, BluetoothDevice? row)
    {
        if (row is null)
        {
            entries.Remove(id);
            return null;
        }
        var entry = entries.TryGetValue(id, out var before)
            // Keep what a reading already found: the kind and the level are asked for on their own
            // schedule and must not be lost every time a device reports something else. A row
            // built from an event never knows either of them, so what was there is never worse.
            ? before with { Row = row with { Battery = before.Row.Battery, Glyph = before.Row.Glyph, Audio = before.Row.Audio }, Held = before.Held.Add(search), Left = null }
            : new Entry(row, [search]);
        return Keep(id, entry).Row;
    }

    /// <summary><paramref name="search"/>'s watcher no longer has <paramref name="id"/>, as of <paramref name="now"/>.</summary>
    public void Gone(WindowsBluetooth.Search search, string id, long now)
    {
        if (entries.TryGetValue(id, out var entry) && entry.Held.Contains(search))
        {
            var held = entry.Held.Remove(search);
            Keep(id, entry with { Held = held, Left = held.IsEmpty ? now : null });
        }
    }

    /// <summary>
    /// The entry with its connection as its watchers say it: connected while the connected ones'
    /// has it, and while none has it, as it was.
    /// </summary>
    private Entry Keep(string id, Entry entry)
    {
        if (!entry.Held.IsEmpty)
        {
            entry = entry with { Row = entry.Row with { Connected = entry.Held.Any(search => search.Connected == true) } };
        }
        entries[id] = entry;
        return entry;
    }

    /// <summary>What a read of the device's nodes said; true when that changes its row.</summary>
    public bool Learned(string id, int? level, string glyph, bool audio)
    {
        if (!entries.TryGetValue(id, out var entry))
        {
            return false;
        }
        var row = entry.Row;
        entries[id] = entry with { Walked = true };
        if (row.Battery == level && row.Glyph == glyph && row.Audio == audio)
        {
            return false;
        }
        // The level as it was read, null included: a device that has stopped saying is not a
        // device stuck at what it last said.
        entries[id] = entry with { Walked = true, Row = row with { Battery = level, Glyph = glyph, Audio = audio } };
        return true;
    }

    public bool Walked(string id) => entries.TryGetValue(id, out var entry) && entry.Walked;

    /// <summary>The device was unpaired here: it is gone, with nothing to wait for.</summary>
    public void Forget(string id) => entries.Remove(id);

    /// <summary>The devices listed at <paramref name="now"/>; one waited for long enough is dropped.</summary>
    public List<BluetoothDevice> Read(long now)
    {
        foreach (var (id, entry) in entries.Where(pair => now - pair.Value.Left >= MovingMs).ToList())
        {
            entries.Remove(id);
        }
        return [.. entries.Values.Select(entry => entry.Row)];
    }
}
/// <summary>Made-up devices for pictures and tests; removing and connecting only pretend.</summary>
internal sealed class SampleBluetooth : IBluetoothSource
{
    private bool on;

    /// <summary>
    /// A made-up PC. <paramref name="radio"/> null is one with no Bluetooth at all and false one
    /// with it switched off — two states that must not read the same as the third, a radio that is
    /// on with nothing paired to it (<paramref name="paired"/> false). <paramref name="devices"/>,
    /// where given, are the devices instead of the usual ones: a picture of several levels.
    /// </summary>
    public SampleBluetooth(bool? radio = true, bool paired = true, IEnumerable<BluetoothDevice>? devices = null)
    {
        Radio = radio;
        on = radio == true;
        if (devices is not null)
        {
            this.devices = [.. devices];
        }
        if (!paired)
        {
            this.devices.RemoveAll(device => device.Paired);
        }
    }

    /// <summary>Null for a PC with no radio, which no switch can turn on.</summary>
    public bool? Radio { get; private set; }

    private readonly List<BluetoothDevice> devices =
    [
        // A connected device that reports its level, one that reports nothing at all, and the
        // rest: the two states a row has to draw without either moving the other. The classes are
        // the ones such a device's nodes really carry, so what a picture shows is what the panel
        // would draw for the real thing — Pixel 8 among them, whose nodes name nothing the panel
        // can put a picture to, and which therefore wears the plain rune rather than a guess.
        //
        // All of them paired: the panel lists nothing else.
        new("1", "WH-1000XM5", WindowsBluetooth.GlyphFor([WindowsBluetooth.Media]), true, true, 72, WindowsBluetooth.IsAudio([WindowsBluetooth.Media])),
        new("2", "MX Master 3S", WindowsBluetooth.GlyphFor([WindowsBluetooth.Mouse]), true, true), // not-ui: a sample device's name
        new("3", "Keychron K2", WindowsBluetooth.GlyphFor([WindowsBluetooth.Keyboard]), true, false), // not-ui: a sample device's name
        new("4", "Pixel 8", WindowsBluetooth.GlyphFor([WindowsBluetooth.Anything]), true, false), // not-ui: a sample device's name
    ];
    private Changes? changes;

    public void Start(Changes news) => changes = news;

    public BluetoothState Read() =>
        new(Radio is null ? null : on, Radio is not null, on ? WindowsBluetooth.Sort([.. devices]) : [], null, null);

    public void SetRadio(bool value)
    {
        if (Radio is null)
        {
            return;
        }
        on = value;
        Radio = value;
        changes?.Add(BluetoothNews.Radio);
    }

    public void Remove(string id)
    {
        devices.RemoveAll(device => device.Id == id);
        changes?.Add(BluetoothNews.Devices);
    }

    /// <summary>What was asked, for the tests: "connect 1", "disconnect 1".</summary>
    public List<string> Asked { get; } = [];

    /// <summary>An audio device connects and lets go at once; anything else is refused, as Windows has no request for it.</summary>
    public void Connect(string id, bool connect)
    {
        Asked.Add((connect ? "connect " : "disconnect ") + id);
        var at = devices.FindIndex(device => device.Id == id);
        if (at >= 0 && devices[at] is { Paired: true, Audio: true })
        {
            devices[at] = devices[at] with { Connected = connect };
        }
        changes?.Add(BluetoothNews.Devices);
    }

    /// <summary>The made-up devices know their own levels; nothing is looked up.</summary>
    public void Poll()
    {
    }

    public void Dispose()
    {
    }
}
