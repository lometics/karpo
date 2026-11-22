// The PC's network interfaces and their addresses, watched, for a switch whose state Windows tells
// no app of in any other way: the mobile hotspot (HotspotSwitch, docs\widgets.md §439). The tethering
// manager has no event, and NetworkStatusChanged — the one the switch had — is about the connection
// to the internet, which turning the hotspot on or off in Windows' own panel does not always touch.
// What it does touch is an interface of its own: the hotspot's adapter comes up and is given its
// address (192.168.137.1, Internet Connection Sharing's) as it starts, and loses both as it stops.
//
// NotifyIpInterfaceChange and NotifyUnicastIpAddressChange (iphlpapi, as the wired line follows its
// cable: Wired.cs) call back on the IP helper's thread for any interface or address that came, went
// or changed; this only passes that on, and the owner reads what it shows. Nothing polls: between
// changes of the network nothing is called at all.

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal sealed unsafe class InterfaceWatch : IDisposable
{
    private const ushort AfUnspec = 0;
    private const uint NoError = 0;

    // The watch each registration reports to, by the number its context carries.
    private static readonly ConcurrentDictionary<nint, InterfaceWatch> Targets = new();
    private static long nextNumber;

    private readonly Action changed;
    private readonly nint number;
    private HANDLE interfaceNotice;
    private HANDLE addressNotice;

    /// <param name="changed">On the IP helper's thread: an interface or an address came, went or changed.</param>
    public InterfaceWatch(Action changed)
    {
        this.changed = changed;
        number = (nint)Interlocked.Increment(ref nextNumber);
        Targets[number] = this;
        HANDLE handle = default;
        if (NotifyIpInterfaceChange(AfUnspec, &Heard, (void*)number, 0, &handle) == NoError)
        {
            interfaceNotice = handle;
        }
        handle = default;
        if (NotifyUnicastIpAddressChange(AfUnspec, &Heard, (void*)number, 0, &handle) == NoError)
        {
            addressNotice = handle;
        }
    }

    /// <summary>Whether Windows took either registration: false where nothing will be heard.</summary>
    public bool Watching => interfaceNotice != HANDLE.NULL || addressNotice != HANDLE.NULL;

    public void Dispose()
    {
        Targets.TryRemove(number, out _);
        // Each waits for a callback in flight, which only passes the news on.
        foreach (var notice in new[] { interfaceNotice, addressNotice })
        {
            if (notice != HANDLE.NULL)
            {
                _ = CancelMibChangeNotify2(notice);
            }
        }
        interfaceNotice = addressNotice = HANDLE.NULL;
    }

    /// <summary>On the IP helper's thread: an interface or an address changed.</summary>
    [UnmanagedCallersOnly]
    private static void Heard(void* context, void* row, int type)
    {
        try
        {
            if (Targets.TryGetValue((nint)context, out var watch))
            {
                watch.changed();
            }
        }
        catch (Exception)
        {
            // Nothing may escape into the IP helper's callback.
        }
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint NotifyIpInterfaceChange(ushort family, delegate* unmanaged<void*, void*, int, void> callback, void* context, byte initialNotification, HANDLE* handle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint NotifyUnicastIpAddressChange(ushort family, delegate* unmanaged<void*, void*, int, void> callback, void* context, byte initialNotification, HANDLE* handle);

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint CancelMibChangeNotify2(HANDLE handle);
}
