// The PC's VPN connections, as Windows' own VPN tile and page list them, connected and
// disconnected the way Windows does: through RAS, the phonebook of connections and the service that
// dials them. It is Windows' own road — the tile's handler and the page are built on it, and
// nothing of Windows' uses Windows.Networking.Vpn's VpnManagementAgent, which an unpackaged app
// cannot use anyway (it needs a restricted capability, and lists nothing to a desktop app).
//
// What is listed is every VPN entry (RASET_Vpn) of the two phonebooks RasEnumEntries reads with no
// phonebook named — the person's own and the one for all users — which is where Windows' page adds
// them, built-in and third-party (plug-in) profiles alike. Which are connected is RasEnumConnections
// with RasGetConnectStatus. Nothing polls: RasConnectionNotification signals a connection made or
// ended anywhere, Windows' own tile included, and a change to either phonebook's folder (a profile
// added or removed in Settings) is watched; each is read again on the pool.
//
// Connecting is RasDial with what Windows saved for the entry: RasGetEntryDialParams hands back the
// user name and a handle to the saved password, which RasDial knows how to use, and for an entry
// that signs in with EAP, RasGetEapUserIdentity without asking the person. Where Windows needs the
// person — no saved sign-in, a certificate to choose, a third-party profile's own prompt — the
// connection is not dialled blind: Windows' VPN settings open, where its own sign-in asks. Windows'
// modern sign-in is its own and cannot be called; the legacy dial dialog could, and is not the
// Windows 11 way.
//
// The structures are ras.h's, which packs them to four bytes on x64 too, so the handle in a
// connection is at 4 and not 8. The offsets below were checked against the SDK's (26100) and on this
// machine: RasEnumEntries takes 1048 for an entry name, RasEnumConnections 1388 for a connection,
// RasGetEntryProperties 6724 for an entry, whose default names its type at 3496.
//
// Every call is on the pool: RAS talks to a service, and dialling takes as long as the network does.

using System.Runtime.InteropServices;
using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>Where a VPN connection stands.</summary>
internal enum VpnState
{
    Disconnected,
    Connecting,
    Connected,
}

/// <summary>One VPN connection as Windows' page lists it.</summary>
/// <param name="Name">The entry's name, which is what Windows shows.</param>
/// <param name="Phonebook">The phonebook it is in: the person's own, or the one for all users.</param>
/// <param name="ThirdParty">A plug-in's profile, whose sign-in is the plug-in's own.</param>
/// <param name="Eap">It signs in with EAP, whose saved identity is asked for rather than a password.</param>
/// <param name="Problem">Why the last attempt did not connect, as Windows words it; null where nothing went wrong.</param>
internal sealed record VpnConnection(string Name, string Phonebook, VpnState State, bool ThirdParty = false, bool Eap = false, string? Problem = null);

/// <summary>How an attempt to connect ended.</summary>
internal enum VpnOutcome
{
    Connected,

    /// <summary>Windows needs the person — a sign-in, a choice — and its own settings were opened for it.</summary>
    Handed,

    /// <summary>It did not connect; the connection says why (<see cref="VpnConnection.Problem"/>).</summary>
    Failed,
}

/// <summary>The VPN connections, as the switch asks them: Windows' through RAS, or made-up ones in the tests.</summary>
internal interface IVpnSource : IDisposable
{
    /// <summary>Whether the connections have been read once.</summary>
    bool Looked { get; }

    /// <summary>What was last read, by name. Any thread may ask.</summary>
    IReadOnlyList<VpnConnection> Connections { get; }

    /// <summary>Reads the connections and follows them; <paramref name="changed"/> comes from any thread. Blocks: on the pool.</summary>
    void Start(Action changed);

    /// <summary>Connects <paramref name="connection"/>. Blocks for as long as dialling takes: on the pool.</summary>
    VpnOutcome Connect(VpnConnection connection);

    /// <summary>Disconnects <paramref name="connection"/>; false where it would not. Blocks: on the pool.</summary>
    bool Disconnect(VpnConnection connection);
}

/// <summary>The VPN connections through RAS (see the top of this file).</summary>
internal sealed unsafe class RasVpn : IVpnSource
{
    // ras.h, packed to four bytes (see the top of this file).
    private const uint EntryNameSize = 1048;
    private const int EntryNameName = 4;
    private const int EntryNameBook = 524;
    private const uint ConnectionSize = 1388;
    private const int ConnectionHandle = 4;
    private const int ConnectionName = 12;
    private const int ConnectionBook = 818;
    private const uint EntrySize = 6724;
    private const int EntryOptions = 4;
    private const int EntryType = 3496;
    private const int EntryOptions2 = 4048;
    private const uint StatusSize = 608;
    private const int StatusState = 4;
    private const uint DialParamsSize = 2120;
    private const int DialParamsName = 4;
    private const int DialParamsUser = 1034;
    private const uint ExtensionsSize = 60;
    private const int ExtensionsEapSize = 32;
    private const int ExtensionsEapData = 36;
    private const int IdentitySizeOfEap = 516;
    private const int IdentityEap = 520;

    private const uint TypeVpn = 2;
    private const uint RequiresEap = 0x00020000;
    private const uint ThirdPartyProfile = 0x08000000;
    private const uint StateConnected = 0x2000;
    private const uint StateDisconnected = 0x2001;
    private const uint NotifyConnection = 1;
    private const uint NotifyDisconnection = 2;
    private const uint EapNonInteractive = 0x2;
    private const uint BufferTooSmall = 603;
    private const uint InvalidHandle = 6;

    /// <summary>What RasDial says when an entry needs the person: a sign-in, a certificate, a choice.</summary>
    private static readonly uint[] NeedsThePerson =
    [
        691,   // ERROR_AUTHENTICATION_FAILURE
        703,   // ERROR_INTERACTIVE_MODE
        718,   // ERROR_PPP_TIMEOUT: a sign-in that never came
        798,   // ERROR_NO_EAPTLS_CERTIFICATE
        812,   // ERROR_UNACCEPTABLE_AUTHENTICATION
        13801, // IKE authentication credentials are unacceptable
    ];

    private readonly object gate = new();
    private readonly AutoResetEvent signal = new(false);
    private readonly List<FileSystemWatcher> books = [];
    private RegisteredWaitHandle? waiting;
    private VpnConnection[] connections = [];
    private readonly Dictionary<string, string> problems = new(StringComparer.OrdinalIgnoreCase);
    private Action? changed;
    private volatile bool looked;
    private bool disposed;

    public bool Looked => looked;

    public IReadOnlyList<VpnConnection> Connections => Volatile.Read(ref connections);

    public void Start(Action onChanged)
    {
        changed = onChanged;
        Read();
        looked = true;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            // A connection made or ended anywhere signals this, whoever made it.
            var handle = (nint)signal.SafeWaitHandle.DangerousGetHandle();
            if (RasConnectionNotificationW(-1, handle, NotifyConnection | NotifyDisconnection) == 0)
            {
                waiting = ThreadPool.RegisterWaitForSingleObject(signal, static (state, _) => ((RasVpn)state!).Heard(), this, Timeout.Infinite, executeOnlyOnce: false);
            }
            // A profile added or removed in Settings changes a phonebook, which nothing else says.
            foreach (var folder in Phonebooks())
            {
                try
                {
                    if (!Directory.Exists(folder))
                    {
                        continue;
                    }
                    var watcher = new FileSystemWatcher(folder, "*.pbk")
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                        IncludeSubdirectories = false,
                    };
                    watcher.Changed += (_, _) => Heard();
                    watcher.Created += (_, _) => Heard();
                    watcher.Deleted += (_, _) => Heard();
                    watcher.EnableRaisingEvents = true;
                    books.Add(watcher);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
                {
                    // A folder that cannot be watched is read again on the next connection's news.
                }
            }
        }
    }

    public VpnOutcome Connect(VpnConnection connection)
    {
        lock (gate)
        {
            problems.Remove(connection.Name);
        }
        var result = Dial(connection);
        if (result is 0)
        {
            Read();
            return VpnOutcome.Connected;
        }
        if (connection.ThirdParty || Array.IndexOf(NeedsThePerson, result) >= 0)
        {
            // Windows' own sign-in asks for what it needs; this does not dial blind.
            Launch.Open("ms-settings:network-vpn");
            Read();
            return VpnOutcome.Handed;
        }
        var said = ErrorText(result);
        Troubles.Note($"VPN: connecting {connection.Name}", new InvalidOperationException($"RasDial {result}: {said}"));
        lock (gate)
        {
            problems[connection.Name] = said;
        }
        Read();
        return VpnOutcome.Failed;
    }

    public bool Disconnect(VpnConnection connection)
    {
        var any = false;
        // One buffer for every connection's status, out of the loop: a stackalloc in it would take
        // another 608 bytes of stack for each connection hung up.
        var status = stackalloc byte[(int)StatusSize];
        foreach (var (handle, name, book) in Live())
        {
            if (!Same(name, book, connection))
            {
                continue;
            }
            any = true;
            _ = RasHangUpW(handle);
            // RAS asks this of whoever hangs up: the connection is gone once its status says the
            // handle is no longer one, which takes a moment and no longer.
            var until = Environment.TickCount64 + 3000;
            while (Environment.TickCount64 < until)
            {
                *(uint*)status = StatusSize;
                if (RasGetConnectStatusW(handle, status) == InvalidHandle)
                {
                    break;
                }
                Thread.Sleep(50);
            }
        }
        Read();
        return any;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            waiting?.Unregister(null);
            waiting = null;
            foreach (var watcher in books)
            {
                watcher.Dispose();
            }
            books.Clear();
        }
    }

    /// <summary>On the pool: something changed; read again and say so.</summary>
    private void Heard()
    {
        if (disposed)
        {
            return;
        }
        // Called from the pool's wait and from a file watcher's thread, where anything thrown ends
        // the process: whatever RAS does is caught and noted, and the next news reads again.
        try
        {
            Read();
            changed?.Invoke();
        }
        catch (Exception error)
        {
            Troubles.Note("VPN: reading the connections", error);
        }
    }

    /// <summary>Reads the VPN entries of both phonebooks, and which of them are connected.</summary>
    private void Read()
    {
        var live = Live();
        var found = new List<VpnConnection>();
        foreach (var (name, book) in Entries())
        {
            if (Properties(name, book) is not { } entry)
            {
                continue;
            }
            var state = VpnState.Disconnected;
            foreach (var (handle, liveName, liveBook) in live)
            {
                if (string.Equals(liveName, name, StringComparison.OrdinalIgnoreCase)
                    && (liveBook.Length == 0 || string.Equals(liveBook, book, StringComparison.OrdinalIgnoreCase)))
                {
                    state = Status(handle) is StateConnected ? VpnState.Connected : state == VpnState.Connected ? state : VpnState.Connecting;
                }
            }
            string? problem;
            lock (gate)
            {
                problems.TryGetValue(name, out problem);
            }
            found.Add(new VpnConnection(name, book, state, entry.ThirdParty, entry.Eap, state == VpnState.Connected ? null : problem));
        }
        Volatile.Write(ref connections, [.. VpnChoices.Sorted(found)]);
    }

    /// <summary>Every entry of both phonebooks, its name and the phonebook it is in.</summary>
    private static List<(string Name, string Book)> Entries()
    {
        var entries = new List<(string, string)>();
        uint size = EntryNameSize;
        uint count = 0;
        var buffer = (byte*)NativeMemory.AllocZeroed(size);
        try
        {
            *(uint*)buffer = EntryNameSize;
            var result = RasEnumEntriesW(null, null, buffer, &size, &count);
            if (result == BufferTooSmall)
            {
                NativeMemory.Free(buffer);
                buffer = (byte*)NativeMemory.AllocZeroed(size);
                for (uint at = 0; at + EntryNameSize <= size; at += EntryNameSize)
                {
                    *(uint*)(buffer + at) = EntryNameSize;
                }
                result = RasEnumEntriesW(null, null, buffer, &size, &count);
            }
            if (result != 0)
            {
                return entries;
            }
            for (uint index = 0; index < count; index++)
            {
                var entry = buffer + (index * EntryNameSize);
                entries.Add((new string((char*)(entry + EntryNameName)), new string((char*)(entry + EntryNameBook))));
            }
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
        return entries;
    }

    /// <summary>What an entry is, where it is a VPN; null for one that is not (a dial-up, a broadband).</summary>
    private static (bool ThirdParty, bool Eap)? Properties(string name, string book)
    {
        uint size = 0;
        _ = RasGetEntryPropertiesW(book, name, null, &size, null, null);
        size = Math.Max(size, EntrySize);
        var entry = (byte*)NativeMemory.AllocZeroed(size);
        try
        {
            *(uint*)entry = EntrySize;
            if (RasGetEntryPropertiesW(book, name, entry, &size, null, null) != 0 || *(uint*)(entry + EntryType) != TypeVpn)
            {
                return null;
            }
            return ((*(uint*)(entry + EntryOptions2) & ThirdPartyProfile) != 0, (*(uint*)(entry + EntryOptions) & RequiresEap) != 0);
        }
        finally
        {
            NativeMemory.Free(entry);
        }
    }

    /// <summary>The connections RAS has now: each one's handle, its entry's name and its phonebook.</summary>
    private static List<(nint Handle, string Name, string Book)> Live()
    {
        var live = new List<(nint, string, string)>();
        uint size = ConnectionSize;
        uint count = 0;
        var buffer = (byte*)NativeMemory.AllocZeroed(size);
        try
        {
            *(uint*)buffer = ConnectionSize;
            var result = RasEnumConnectionsW(buffer, &size, &count);
            if (result == BufferTooSmall)
            {
                NativeMemory.Free(buffer);
                buffer = (byte*)NativeMemory.AllocZeroed(size);
                for (uint at = 0; at + ConnectionSize <= size; at += ConnectionSize)
                {
                    *(uint*)(buffer + at) = ConnectionSize;
                }
                result = RasEnumConnectionsW(buffer, &size, &count);
            }
            if (result != 0)
            {
                return live;
            }
            for (uint index = 0; index < count; index++)
            {
                var connection = buffer + (index * ConnectionSize);
                live.Add((*(nint*)(connection + ConnectionHandle), new string((char*)(connection + ConnectionName)), new string((char*)(connection + ConnectionBook))));
            }
        }
        finally
        {
            NativeMemory.Free(buffer);
        }
        return live;
    }

    /// <summary>A connection's RASCONNSTATE, or the disconnected state where RAS no longer knows it.</summary>
    private static uint Status(nint handle)
    {
        var status = stackalloc byte[(int)StatusSize];
        *(uint*)status = StatusSize;
        return RasGetConnectStatusW(handle, status) == 0 ? *(uint*)(status + StatusState) : StateDisconnected;
    }

    private static bool Same(string name, string book, VpnConnection connection) =>
        string.Equals(name, connection.Name, StringComparison.OrdinalIgnoreCase)
        && (book.Length == 0 || string.Equals(book, connection.Phonebook, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Dials <paramref name="connection"/> with what Windows saved for it, and waits for the answer:
    /// 0 when connected, else RAS's error. A handle RasDial hands back is hung up again whatever it
    /// answered, as RAS asks.
    /// </summary>
    private static uint Dial(VpnConnection connection)
    {
        var parameters = (byte*)NativeMemory.AllocZeroed(DialParamsSize);
        var extensions = (byte*)NativeMemory.AllocZeroed(ExtensionsSize);
        byte* identity = null;
        try
        {
            *(uint*)parameters = DialParamsSize;
            Copy(connection.Name, (char*)(parameters + DialParamsName), 257);
            var saved = 0;
            var asked = RasGetEntryDialParamsW(connection.Phonebook, parameters, &saved);
            if (asked != 0)
            {
                return asked;
            }
            var withEap = false;
            if (connection.Eap)
            {
                // The identity Windows saved for EAP, without asking: where it has none, RAS says it
                // needs the person (703), and the person is asked by Windows, not by this.
                var got = RasGetEapUserIdentityW(connection.Phonebook, connection.Name, EapNonInteractive, 0, &identity);
                if (got != 0)
                {
                    return got;
                }
                Copy(new string((char*)identity), (char*)(parameters + DialParamsUser), 257);
                *(uint*)extensions = ExtensionsSize;
                *(uint*)(extensions + ExtensionsEapSize) = *(uint*)(identity + IdentitySizeOfEap);
                *(nint*)(extensions + ExtensionsEapData) = *(nint*)(identity + IdentityEap);
                withEap = true;
            }
            nint handle = 0;
            var result = RasDialW(withEap ? extensions : null, connection.Phonebook, parameters, 0, null, &handle);
            if (result != 0 && handle != 0)
            {
                _ = RasHangUpW(handle);
            }
            return result;
        }
        finally
        {
            // The saved password's handle goes with the parameters: nothing of it is kept.
            NativeMemory.Clear(parameters, DialParamsSize);
            NativeMemory.Free(parameters);
            NativeMemory.Free(extensions);
            if (identity != null)
            {
                RasFreeEapUserIdentityW(identity);
            }
        }
    }

    /// <summary>RAS's own words for <paramref name="error"/>.</summary>
    private static string ErrorText(uint error)
    {
        var text = stackalloc char[512];
        return RasGetErrorStringW(error, text, 512) == 0 ? new string(text).Trim() : S.DockQuickVpnError(error);
    }

    private static void Copy(string text, char* into, int room)
    {
        var length = Math.Min(text.Length, room - 1);
        text.AsSpan(0, length).CopyTo(new Span<char>(into, length));
        into[length] = '\0';
    }

    /// <summary>The folders of the two phonebooks RasEnumEntries reads: the person's own, and all users'.</summary>
    private static IEnumerable<string> Phonebooks()
    {
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Network\Connections\Pbk");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Network\Connections\Pbk");
    }

    [DllImport("rasapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasEnumEntriesW(char* reserved, char* phonebook, byte* entries, uint* size, uint* count);

    [DllImport("rasapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasGetEntryPropertiesW(string phonebook, string entry, byte* properties, uint* size, byte* device, uint* deviceSize);

    [DllImport("rasapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasEnumConnectionsW(byte* connections, uint* size, uint* count);

    [DllImport("rasapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasGetConnectStatusW(nint connection, byte* status);

    [DllImport("rasapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasGetEntryDialParamsW(string phonebook, byte* parameters, int* passwordSaved);

    [DllImport("rasapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasDialW(byte* extensions, string phonebook, byte* parameters, uint notifierType, void* notifier, nint* connection);

    [DllImport("rasapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasHangUpW(nint connection);

    [DllImport("rasapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasConnectionNotificationW(nint connection, nint signal, uint flags);

    [DllImport("rasapi32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasGetEapUserIdentityW(string phonebook, string entry, uint flags, nint window, byte** identity);

    [DllImport("rasapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern void RasFreeEapUserIdentityW(byte* identity);

    [DllImport("rasapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint RasGetErrorStringW(uint error, char* text, uint size);
}

/// <summary>Made-up connections for the tests and the pictures: connecting and disconnecting only pretend.</summary>
internal sealed class SampleVpn : IVpnSource
{
    private readonly object gate = new();
    private VpnConnection[] connections;

    public SampleVpn(params VpnConnection[] connections)
    {
        this.connections = connections;
    }

    /// <summary>What the next connect answers: a sign-in Windows asks for, or a failure.</summary>
    public VpnOutcome Answer { get; set; } = VpnOutcome.Connected;

    public List<string> Asked { get; } = [];

    public bool Looked { get; private set; }

    public IReadOnlyList<VpnConnection> Connections => Volatile.Read(ref connections);

    public void Start(Action changed) => Looked = true;

    public VpnOutcome Connect(VpnConnection connection)
    {
        lock (gate)
        {
            Asked.Add("connect " + connection.Name);
        }
        if (Answer == VpnOutcome.Connected)
        {
            Put(connection.Name, VpnState.Connected, null);
        }
        else if (Answer == VpnOutcome.Failed)
        {
            Put(connection.Name, VpnState.Disconnected, S.DockQuickVpnNotMade);
        }
        return Answer;
    }

    public bool Disconnect(VpnConnection connection)
    {
        lock (gate)
        {
            Asked.Add("disconnect " + connection.Name);
        }
        Put(connection.Name, VpnState.Disconnected, null);
        return true;
    }

    private void Put(string name, VpnState state, string? problem) =>
        Volatile.Write(ref connections, [.. Volatile.Read(ref connections).Select(connection => connection.Name == name ? connection with { State = state, Problem = problem } : connection)]);

    public void Dispose()
    {
    }
}

/// <summary>What the VPN switch shows and does, apart from any source: the tests hold these.</summary>
internal static class VpnChoices
{
    /// <summary>
    /// §404: Windows' VPN page, whose "Add VPN" adds a connection: what the line's "Add" opens with none set up.
    /// Settings has no address for its add dialog itself; the page is a click from it.
    /// </summary>
    internal const string AddLink = "ms-settings:network-vpn";

    /// <summary>The connections as Windows' page orders them: by name.</summary>
    internal static IEnumerable<VpnConnection> Sorted(IEnumerable<VpnConnection> connections) =>
        connections.OrderBy(connection => connection.Name, StringComparer.CurrentCultureIgnoreCase);

    /// <summary>What a click on the widget in a folder, or on the bar with one connection, does.</summary>
    internal enum Move
    {
        /// <summary>Disconnect what is connected.</summary>
        Disconnect,

        /// <summary>Connect <see cref="Plan(IReadOnlyList{VpnConnection}, string?)"/>'s connection.</summary>
        Connect,

        /// <summary>Open Windows' VPN settings: there is nothing to connect, or no way to know which.</summary>
        Settings,
    }

    /// <summary>
    /// What a click that cannot open a list does, as Windows' split toggle does for one connection:
    /// what is connected, disconnected; else the only connection, or the last one connected here,
    /// connected; else — none at all, or several and no way to know which the person meant —
    /// Windows' VPN settings, where they choose.
    /// </summary>
    internal static (Move Move, VpnConnection? Connection) Plan(IReadOnlyList<VpnConnection> connections, string? last)
    {
        if (connections.FirstOrDefault(connection => connection.State != VpnState.Disconnected) is { } live)
        {
            return (Move.Disconnect, live);
        }
        if (connections.Count == 1)
        {
            return (Move.Connect, connections[0]);
        }
        if (last is not null && connections.FirstOrDefault(connection => string.Equals(connection.Name, last, StringComparison.OrdinalIgnoreCase)) is { } used)
        {
            return (Move.Connect, used);
        }
        return (Move.Settings, null);
    }

    /// <summary>
    /// The connections' line, as Windows' page has them: each connection with where it stands, the
    /// ones up marked; one connection is a row too. With none, a row that says so and does nothing, and where one
    /// is added (§404: the line's "Add"). Windows' VPN settings are the preset's footer under the line.
    /// </summary>
    internal static IReadOnlyList<SwitchChoice> List(IReadOnlyList<VpnConnection> connections)
    {
        if (connections.Count == 0)
        {
            return [new SwitchChoice(S.DockQuickVpnNone, Enabled: false) { Add = AddLink }];
        }
        return [.. connections.Select(connection => new SwitchChoice(connection.Name, Words(connection), Checked: connection.State == VpnState.Connected))];
    }

    /// <summary>What a connection's row says under its name, as Windows' page words it.</summary>
    internal static string? Words(VpnConnection connection) => connection.State switch
    {
        VpnState.Connected => S.DockQuickConnected,
        VpnState.Connecting => S.DockQuickConnecting,
        _ => connection.Problem is { Length: > 0 } problem ? S.DockQuickVpnCantConnect(problem) : null,
    };

    /// <summary>What the widget says it is on as: the connection it is on, or how many.</summary>
    internal static string? Detail(IReadOnlyList<VpnConnection> connections)
    {
        var on = connections.Where(connection => connection.State == VpnState.Connected).ToList();
        return on.Count switch
        {
            0 => null,
            1 => on[0].Name,
            _ => S.DockQuickVpnConnected(on.Count),
        };
    }
}

/// <summary>
/// VPN as Windows' tile has it (see the top of this file): on while a connection is up, and named
/// by it. On the bar a click opens the list of connections, as the tile's chevron opens its page —
/// or, where there is one connection, switches it, as the tile's split toggle does. In a folder,
/// which opens no list, a click switches what it can (<see cref="VpnChoices.Plan"/>).
/// </summary>
internal sealed class VpnSwitch : IQuickSwitch
{
    private readonly Func<IVpnSource> source;
    private readonly object gate = new();
    private IVpnSource? vpn;
    private IReadOnlyList<VpnConnection> shown = [];
    private Action? changed;
    private volatile bool looked;
    private volatile string? last;
    private bool disposed;

    public VpnSwitch()
        : this(static () => new RasVpn())
    {
    }

    /// <param name="source">What the connections are read from and dialled through; made on the pool.</param>
    internal VpnSwitch(Func<IVpnSource> source)
    {
        this.source = source;
    }

    public string Name => S.DockQuickVpnName;

    public bool Available => true;

    /// <summary>
    /// Whether a click switches a connection without the person: one that is up is let go, the only
    /// one or the one last connected here is dialled — unless it is a plug-in's, whose sign-in is its
    /// own. Where the click would open Windows' VPN settings instead, it is not a switch.
    /// </summary>
    public bool Switches => looked && VpnChoices.Plan(Connections, last) switch
    {
        (VpnChoices.Move.Disconnect, _) => true,
        (VpnChoices.Move.Connect, { ThirdParty: false }) => true,
        _ => false,
    };

    public bool? On => looked ? Connections.Any(connection => connection.State == VpnState.Connected) : null;

    public string? Detail => VpnChoices.Detail(Connections);

    public string? Note => looked && Connections.Count == 0
        ? S.DockQuickVpnNoneNote
        : S.DockQuickVpnNote;

    /// <summary>What was last read; nothing before the first read.</summary>
    private IReadOnlyList<VpnConnection> Connections => Volatile.Read(ref vpn)?.Connections ?? [];

    /// <summary>How many connections were read: for the smoke pass.</summary>
    internal int Count => Connections.Count;

    /// <summary>
    /// The connections' line (the VPN preset's), as it was last built: each connection's row
    /// switches that one. Before the first read it says it is looking, so a click that fast does
    /// nothing rather than switch something it has not seen.
    /// </summary>
    public IReadOnlyList<SwitchChoice>? Choices
    {
        get
        {
            if (!looked)
            {
                shown = [];
                return [new SwitchChoice(S.DockQuickVpnLooking, Enabled: false)];
            }
            var connections = Connections;
            shown = connections;
            return VpnChoices.List(connections);
        }
    }

    /// <summary>The row <paramref name="index"/> was clicked: that connection, connected or disconnected; on the pool.</summary>
    public void Choose(int index)
    {
        var connections = shown;
        if (index >= 0 && index < connections.Count)
        {
            var chosen = connections[index];
            _ = Task.Run(() => Switch(chosen, connect: chosen.State == VpnState.Disconnected));
        }
    }

    public void Start(Action onChanged)
    {
        changed = onChanged;
        // On the pool: RAS talks to a service (see the top of this file).
        _ = Task.Run(() =>
        {
            var made = source();
            try
            {
                made.Start(() => changed?.Invoke());
            }
            catch (Exception error)
            {
                Troubles.Note("VPN", error);
            }
            lock (gate)
            {
                if (disposed)
                {
                    made.Dispose();
                    return;
                }
                vpn = made;
            }
            looked = true;
            changed?.Invoke();
        });
    }

    /// <summary>A click that opens no list: what <see cref="VpnChoices.Plan"/> says.</summary>
    public void Act()
    {
        var (move, connection) = VpnChoices.Plan(Connections, last);
        if (!looked || move == VpnChoices.Move.Settings || connection is null)
        {
            Launch.Open("ms-settings:network-vpn");
            return;
        }
        _ = Task.Run(() => Switch(connection, connect: move == VpnChoices.Move.Connect));
    }

    /// <summary>Connects or disconnects <paramref name="connection"/>; on the pool.</summary>
    internal void Switch(VpnConnection connection, bool connect)
    {
        if (Volatile.Read(ref vpn) is not { } through)
        {
            return;
        }
        try
        {
            if (!connect)
            {
                _ = through.Disconnect(connection);
            }
            else if (through.Connect(connection) == VpnOutcome.Connected)
            {
                last = connection.Name;
            }
        }
        catch (Exception error)
        {
            Troubles.Note($"VPN: {(connect ? "connecting" : "disconnecting")} {connection.Name}", error);
        }
        changed?.Invoke();
    }

    public void Dispose()
    {
        IVpnSource? made;
        lock (gate)
        {
            disposed = true;
            made = vpn;
            vpn = null;
        }
        made?.Dispose();
    }
}
