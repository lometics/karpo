// Airplane mode where Windows keeps it: the system-wide radio state of the Radio Management
// service (RmSvc), read and set through IRadioManager, the COM interface Windows' own Settings
// page and quick-settings tile use. It is one switch the service owns, not the sum of the
// radios: turned on it takes every radio down and remembers which were up, turned off it brings
// back what was, and a radio turned on again while it is on (Wi-Fi on a plane) leaves it on. The
// radios one at a time (RadiosAirplane below) can only imitate that, and it is kept only for a PC
// where the interface is not there.
//
// IRadioManager is undocumented. What is called here is what Windows' Settings calls, and the
// shape is the one open tools have used since Windows 8, checked on this machine (Windows 11 25H2,
// 26200) with the read-only half: the class is registered out of process only (CLSCTX_LOCAL_SERVER
// answers, CLSCTX_INPROC_SERVER is "class not registered"), IsRMSupported says 1, and
// GetSystemRadioState says 1 with airplane mode off. SetSystemRadioState was not called to check
// it: no test and no probe may switch the person's radios.
//
//   CLSID RadioManagementAPI  {581333F6-28DB-41BE-BC7A-FF201F12F3F6}
//   IID   IRadioManager       {DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7}
//   after IUnknown's three:
//     3 IsRMSupported(DWORD* supported)
//     4 GetUIRadioInstances(IUnknown** instances)
//     5 GetSystemRadioState(BOOL* enabled, BOOL* hardwareEnabled, DWORD* changeReason)
//     6 SetSystemRadioState(BOOL enabled)
//     7 Refresh()
//     8 OnHardwareSliderChange(BOOL, BOOL)
//
// "Enabled" is the radios', so airplane mode is its opposite: 1 is airplane mode off.
//
// The state is followed through the registry value the service writes when it changes
// (HKLM\SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState), which a standard
// user may read and watch: the change arrives on the pool, and the state is asked of the service
// again there. So a flip in Windows' own panel redraws the widget with no polling, and so does
// one made here.
//
// Every call to the service is on the pool (an MTA), never on the dock's thread: it is a round
// trip to another process, and the object belongs to the apartment it was made in.

using Microsoft.Win32;
using Karpo.Core;
using static TerraFX.Interop.Windows.Windows;
using Foundation = Windows.Foundation;
using Radios = Windows.Devices.Radios;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>
/// What airplane mode is read and switched through: the service's system state, or the radios
/// one by one where that is not there. The switch asks nothing else, which is what lets a test
/// hand it made-up ones.
/// </summary>
internal interface IAirplaneSource : IDisposable
{
    /// <summary>Whether there is anything to switch: the service always, the radios where there are some.</summary>
    bool Available { get; }

    /// <summary>On, off, or null while it has not been read. Answered from what was last read, so any thread may ask.</summary>
    bool? On { get; }

    /// <summary>What a click does, for the tooltip.</summary>
    string Note { get; }

    /// <summary>
    /// Reads where it stands and follows it; <paramref name="changed"/> comes from any thread.
    /// Blocks: called on the pool.
    /// </summary>
    void Start(Action changed);

    /// <summary>Turns airplane mode on or off; false where Windows refused. Blocks: called on the pool.</summary>
    bool Set(bool on);
}

/// <summary>
/// Airplane mode through the Radio Management service (see the top of this file). Made by
/// <see cref="Open"/>, which answers null where the service or its interface is not there.
/// </summary>
internal sealed unsafe class SystemAirplane : IAirplaneSource
{
    internal static readonly Guid ManagerClass = new("581333F6-28DB-41BE-BC7A-FF201F12F3F6");
    internal static readonly Guid ManagerInterface = new("DB3AFBFB-08E6-46C6-AA70-BF9A34C30AB7");

    /// <summary>The key the service writes the system radio state to, under HKLM.</summary>
    internal const string StateKey = @"SYSTEM\CurrentControlSet\Control\RadioManagement\SystemRadioState";

    private const uint LocalServer = 0x4;
    private const int SlotSupported = 3;
    private const int SlotGetState = 5;
    private const int SlotSetState = 6;

    private readonly object gate = new();
    private void* manager;
    private RegistryWatch? watch;
    private Action? changed;

    // Where it stands, as last read on the pool: 1 on, 0 off, -1 not known.
    private volatile int state = -1;

    private SystemAirplane(void* manager)
    {
        this.manager = manager;
    }

    /// <summary>
    /// The service's interface, or null where it is not there: an older Windows, a PC without the
    /// service, or one that says it does not manage radios. Blocks: called on the pool.
    /// </summary>
    public static SystemAirplane? Open()
    {
        void* made = null;
        var classId = ManagerClass;
        var interfaceId = ManagerInterface;
        try
        {
            if (CoCreateInstance(&classId, null, LocalServer, &interfaceId, &made).FAILED || made == null)
            {
                return null;
            }
            uint supported = 0;
            var asked = ((delegate* unmanaged[Stdcall]<void*, uint*, int>)Slot(made, SlotSupported))(made, &supported);
            if (asked < 0 || supported == 0)
            {
                Release(made);
                return null;
            }
            return new SystemAirplane(made);
        }
        catch (Exception error)
        {
            Troubles.Note("Airplane mode: the radio management service", error);
            if (made != null)
            {
                Release(made);
            }
            return null;
        }
    }

    public bool Available => true;

    public bool? On => state switch { 1 => true, 0 => false, _ => null };

    public string Note => S.DockQuickAirplaneNote;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        Read();
        // The service writes its state here when it changes, whoever changed it; a PC where the
        // key cannot be watched still reads it on every switch made here.
        watch = RegistryWatch.Open(RegistryHive.LocalMachine, StateKey, subtree: false, () =>
        {
            Read();
            changed?.Invoke();
        });
    }

    public bool Set(bool on)
    {
        int result;
        lock (gate)
        {
            if (manager == null)
            {
                return false;
            }
            // The radios' state, which is airplane mode's opposite.
            result = ((delegate* unmanaged[Stdcall]<void*, int, int>)Slot(manager, SlotSetState))(manager, on ? 0 : 1);
        }
        Read();
        return result >= 0 && On == on;
    }

    /// <summary>Asks the service where it stands; on the pool.</summary>
    private void Read()
    {
        lock (gate)
        {
            if (manager == null)
            {
                return;
            }
            int enabled = 0, hardware = 0, reason = 0;
            var asked = ((delegate* unmanaged[Stdcall]<void*, int*, int*, int*, int>)Slot(manager, SlotGetState))(manager, &enabled, &hardware, &reason);
            state = AirplaneState.From(asked, enabled);
        }
    }

    public void Dispose()
    {
        watch?.Dispose();
        watch = null;
        void* made;
        lock (gate)
        {
            made = manager;
            manager = null;
        }
        if (made != null)
        {
            // Let go on the pool: the object belongs to the apartment it was made in, and a
            // switch is disposed on the dock's thread.
            var address = (nint)made;
            _ = Task.Run(() => Troubles.Guard("Radios: letting go of a radio", () => Release((void*)address)));
        }
    }

    private static void* Slot(void* self, int slot) => (*(void***)self)[slot];

    private static void Release(void* self) => ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(self, 2))(self);
}

/// <summary>What the service's answers mean, apart from the service: the tests hold these.</summary>
internal static class AirplaneState
{
    /// <summary>
    /// Airplane mode from GetSystemRadioState: its HRESULT and the radios' "enabled", whose opposite
    /// it is — 1 on, 0 off, -1 where the service did not answer.
    /// </summary>
    internal static int From(int result, int radiosEnabled) =>
        result < 0 ? -1 : radiosEnabled == 0 ? 1 : 0;
}

/// <summary>
/// Airplane mode imitated where the service's interface is not there: every radio (Wi-Fi,
/// Bluetooth, mobile broadband) off and on, which is what the tile comes to on such a PC. Each
/// radio reports its own changes, so nothing is polled.
/// </summary>
internal sealed class RadiosAirplane : IAirplaneSource
{
    private readonly Foundation.TypedEventHandler<Radios.Radio, object> stateChanged;
    private Radios.Radio[] radios = [];
    private Action? changed;

    // Where the radios stand, read on their own thread: 1 all off, 0 one on, -1 none or not known.
    private volatile int state = -1;
    private volatile bool disposed;

    public RadiosAirplane()
    {
        // Read here, on the radio's own thread, and only then told about (RadioSwitch has the why).
        stateChanged = (_, _) =>
        {
            Read();
            changed?.Invoke();
        };
    }

    public bool Available => Volatile.Read(ref radios).Length > 0;

    public bool? On => state switch { 1 => true, 0 => false, _ => null };

    public string Note => S.DockQuickAirplaneRadiosNote;

    public void Start(Action onChanged)
    {
        changed = onChanged;
        try
        {
            var found = new List<Radios.Radio>();
            foreach (var radio in Radios.Radio.GetRadiosAsync().AsTask().GetAwaiter().GetResult())
            {
                if (radio.Kind is Radios.RadioKind.WiFi or Radios.RadioKind.Bluetooth or Radios.RadioKind.MobileBroadband)
                {
                    found.Add(radio);
                    if (!disposed)
                    {
                        radio.StateChanged += stateChanged;
                    }
                }
            }
            Volatile.Write(ref radios, [.. found]);
        }
        catch (Exception)
        {
            // No radio stack: nothing to switch.
        }
        Read();
    }

    public bool Set(bool on)
    {
        var wanted = on ? Radios.RadioState.Off : Radios.RadioState.On;
        try
        {
            if (Radios.Radio.RequestAccessAsync().AsTask().GetAwaiter().GetResult() != Radios.RadioAccessStatus.Allowed)
            {
                return false;
            }
            var allowed = true;
            foreach (var radio in Volatile.Read(ref radios))
            {
                allowed &= radio.SetStateAsync(wanted).AsTask().GetAwaiter().GetResult() == Radios.RadioAccessStatus.Allowed;
            }
            Read();
            return allowed;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Airplane mode is every radio off; read on the radios' own thread.</summary>
    private void Read()
    {
        try
        {
            var found = Volatile.Read(ref radios);
            state = found.Length == 0 ? -1 : found.All(radio => radio.State != Radios.RadioState.On) ? 1 : 0;
        }
        catch (Exception)
        {
            // A radio that went while it was being read, which is what a radio switching looks like.
            state = -1;
        }
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var radio in Volatile.Read(ref radios))
        {
            try
            {
                radio.StateChanged -= stateChanged;
            }
            catch (Exception)
            {
            }
        }
        Volatile.Write(ref radios, []);
    }
}
