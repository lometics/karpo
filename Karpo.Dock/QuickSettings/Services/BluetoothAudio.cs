// Connecting and disconnecting a paired Bluetooth audio device, the way Windows' own Bluetooth page,
// its quick settings and the Sound control panel do it: a one-shot request to the Bluetooth audio
// driver.
//
// There is no Bluetooth API for it, but there is a documented audio one. The Bluetooth A2DP and
// hands-free drivers answer KSPROPSETID_BtAudio (ksmedia.h: {7FA06C40-B8F6-4C7E-8556-E8C33A12E54D})
// on their kernel-streaming filters, and a GET of KSPROPERTY_ONESHOT_RECONNECT (0) or
// KSPROPERTY_ONESHOT_DISCONNECT (1) asks the driver to connect or let go of the headset. It carries
// no value and returns at once; the connection follows a few seconds later, and the device watcher
// reports it as it does any other (Bluetooth.cs). Windows' Settings handler, its quick-settings
// broker and mmsys.cpl carry the same property set; ToothTray, AirPodsDesktop and earshot reach it
// this way from an app.
//
// The way to a filter is Core Audio's: every render and capture endpoint, whatever its state — a
// paired headset that is not connected is UNPLUGGED, and is the one this is for — through its
// topology to the device its connector is connected to, which is the driver's filter, and from
// there its IKsControl. An endpoint belongs to the Bluetooth device whose container it is in
// (PKEY_Device_ContainerId), and only endpoints Bluetooth enumerated (BTHENUM, BTHHFENUM) are asked.
// Disconnecting asks every filter the device has — the music profile and the hands-free one —
// because Windows 11 keeps the microphone connected otherwise; connecting asks the first, which
// brings both back.
//
// Windows offers this for audio devices only, and so does the panel. A mouse, a keyboard or a phone
// has no such request: Windows' page lists them with "Remove device" and nothing else, and the ways
// round that — turning the device's services off and on, or the radio driver's disconnect — are not
// Windows' own and are not done here.
//
// Everything here runs on the pool: an endpoint's topology is a round trip to the audio service.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal static unsafe class BluetoothAudio
{
    /// <summary>KSPROPSETID_BtAudio.</summary>
    internal static readonly Guid PropertySet = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");

    /// <summary>KSPROPERTY_ONESHOT_RECONNECT.</summary>
    internal const uint Reconnect = 0;

    /// <summary>KSPROPERTY_ONESHOT_DISCONNECT.</summary>
    internal const uint Disconnect = 1;

    /// <summary>KSPROPERTY_TYPE_GET: the one-shot requests are gets with nothing to get.</summary>
    private const uint Get = 0x1;

    /// <summary>DEVICE_STATEMASK_ALL: a paired headset that is not connected is UNPLUGGED, and it is the one to connect.</summary>
    private const uint AnyState = 0xF;

    private const int SlotKsProperty = 3;

    private static readonly PROPERTYKEY Container = new() { fmtid = new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), pid = 2 };

    private static readonly PROPERTYKEY Enumerator = new() { fmtid = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), pid = 24 };

    /// <summary>KSPROPERTY: a set, a request in it and its flags; 24 bytes.</summary>
    private struct KsProperty
    {
        public Guid Set;
        public uint Id;
        public uint Flags;
    }

    /// <summary>
    /// Asks the device in <paramref name="container"/> to connect or to let go (see the top of this
    /// file): the number of filters that took the request — none where it has no audio filter at all.
    /// Blocks: on the pool.
    /// </summary>
    public static int Ask(Guid container, bool connect)
    {
        if (container == Guid.Empty)
        {
            return 0;
        }
        var classId = CLSID.CLSID_MMDeviceEnumerator;
        IMMDeviceEnumerator* devices = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IMMDeviceEnumerator>(), (void**)&devices).FAILED || devices == null)
        {
            return 0;
        }
        try
        {
            var filters = Filters(devices, container);
            var taken = 0;
            foreach (var filter in filters)
            {
                if (Send(devices, filter, connect ? Reconnect : Disconnect))
                {
                    taken++;
                    if (connect)
                    {
                        // One reconnect brings the headset back whole; a second would only race it.
                        break;
                    }
                }
            }
            return taken;
        }
        catch (Exception error)
        {
            Troubles.Note("Bluetooth audio", error);
            return 0;
        }
        finally
        {
            devices->Release();
        }
    }

    /// <summary>The distinct filters of the Bluetooth endpoints in <paramref name="container"/>, the music profile's first.</summary>
    private static List<string> Filters(IMMDeviceEnumerator* devices, Guid container)
    {
        var found = new List<string>();
        IMMDeviceCollection* endpoints = null;
        if (devices->EnumAudioEndpoints(EDataFlow.eAll, AnyState, &endpoints).FAILED || endpoints == null)
        {
            return found;
        }
        try
        {
            uint count = 0;
            _ = endpoints->GetCount(&count);
            for (uint index = 0; index < count; index++)
            {
                IMMDevice* endpoint = null;
                if (endpoints->Item(index, &endpoint).FAILED || endpoint == null)
                {
                    continue;
                }
                try
                {
                    var (inContainer, bluetooth) = Describe(endpoint, container);
                    if (inContainer && bluetooth && FilterOf(endpoint) is { } filter && !found.Contains(filter, StringComparer.OrdinalIgnoreCase))
                    {
                        found.Add(filter);
                    }
                }
                finally
                {
                    endpoint->Release();
                }
            }
        }
        finally
        {
            endpoints->Release();
        }
        // A2DP's filter (BTHENUM) before the hands-free one (BTHHFENUM): the one whose reconnect
        // brings the whole headset back on Windows 11.
        return [.. found.OrderBy(filter => filter.Contains("bthhfenum", StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>Whether an endpoint is in <paramref name="container"/>, and whether Bluetooth enumerated it.</summary>
    private static (bool InContainer, bool Bluetooth) Describe(IMMDevice* endpoint, Guid container)
    {
        IPropertyStore* store = null;
        if (endpoint->OpenPropertyStore(STGM.STGM_READ, &store).FAILED || store == null)
        {
            return (false, false);
        }
        try
        {
            var inContainer = false;
            PROPVARIANT value = default;
            var key = Container;
            if (store->GetValue(&key, &value).SUCCEEDED)
            {
                inContainer = value.vt == (ushort)VARENUM.VT_CLSID && value.Anonymous.Anonymous.Anonymous.puuid != null
                    && *value.Anonymous.Anonymous.Anonymous.puuid == container;
                _ = PropVariantClear(&value);
            }
            var bluetooth = false;
            key = Enumerator;
            value = default;
            if (store->GetValue(&key, &value).SUCCEEDED)
            {
                char* text = null;
                if (PropVariantToStringAlloc(&value, &text).SUCCEEDED)
                {
                    var name = Native.ComUtil.FromTaskMem(text);
                    bluetooth = IsBluetooth(name);
                }
                _ = PropVariantClear(&value);
            }
            return (inContainer, bluetooth);
        }
        finally
        {
            store->Release();
        }
    }

    /// <summary>Whether an endpoint's enumerator is one of Bluetooth's audio profiles.</summary>
    internal static bool IsBluetooth(string? enumerator) =>
        string.Equals(enumerator, "BTHENUM", StringComparison.OrdinalIgnoreCase)
        || string.Equals(enumerator, "BTHHFENUM", StringComparison.OrdinalIgnoreCase);

    /// <summary>The device an endpoint's connector is connected to: the driver's filter, by its Core Audio id.</summary>
    private static string? FilterOf(IMMDevice* endpoint)
    {
        IDeviceTopology* topology = null;
        if (endpoint->Activate(__uuidof<IDeviceTopology>(), (uint)CLSCTX_ALL, null, (void**)&topology).FAILED || topology == null)
        {
            return null;
        }
        IConnector* connector = null;
        try
        {
            if (topology->GetConnector(0, &connector).FAILED || connector == null)
            {
                return null;
            }
            char* id = null;
            return connector->GetDeviceIdConnectedTo(&id).SUCCEEDED ? Native.ComUtil.FromTaskMem(id) : null;
        }
        finally
        {
            if (connector != null)
            {
                connector->Release();
            }
            topology->Release();
        }
    }

    /// <summary>Sends the one-shot request <paramref name="request"/> to the filter <paramref name="filter"/>.</summary>
    private static bool Send(IMMDeviceEnumerator* devices, string filter, uint request)
    {
        IMMDevice* device = null;
        fixed (char* id = filter)
        {
            if (devices->GetDevice(id, &device).FAILED || device == null)
            {
                return false;
            }
        }
        void* control = null;
        try
        {
            if (device->Activate(__uuidof<IKsControl>(), (uint)CLSCTX_ALL, null, &control).FAILED || control == null)
            {
                return false;
            }
            var property = new KsProperty { Set = PropertySet, Id = request, Flags = Get };
            uint returned = 0;
            var result = ((delegate* unmanaged[Stdcall]<void*, KsProperty*, uint, void*, uint, uint*, int>)(*(void***)control)[SlotKsProperty])(
                control, &property, (uint)sizeof(KsProperty), null, 0, &returned);
            return result >= 0;
        }
        finally
        {
            if (control != null)
            {
                _ = ((IUnknown*)control)->Release();
            }
            device->Release();
        }
    }
}
