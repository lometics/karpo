// The PC's own displays for Brightness (see Brightness.cs): each found through Karpo.Core's
// Monitors, and its brightness reached the way Windows reaches it.
//
//  - A built-in screen: WMI, the class Settings' own slider uses. ROOT\WMI's WmiMonitorBrightness
//    lists every panel whose brightness Windows can set, with its CurrentBrightness; the
//    WmiMonitorBrightnessMethods instance of the same name has WmiSetBrightness(Timeout, Brightness).
//    An instance is matched to its display by the monitor's device path, which both carry.
//  - An external monitor: DDC/CI, the Monitor Configuration API (dxva2) — the monitor's physical
//    handle, and VCP code 0x10, luminance, read with its range and set within it. A monitor that
//    does not answer it (DDC/CI off in its menu, a TV, a dock that does not pass it) has none.
//
// What each display turned out to have is kept by its monitor handle until Forget: handles and
// device names are handed out again whenever the displays change, and one display's is dropped when
// it fails (Forget(screen)), so a stale physical handle is opened afresh. Every call here is on the
// brightness worker, one at a time, never on the dock's thread. No DDC/CI command goes to a monitor
// within DdcGapMs of its last (Rested): ScreenWrites already keeps that rest, and this is the floor
// under it for what it does not see — the probe that opens a handle again right before a write.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings.Services;

internal sealed unsafe class ScreenMachine : IScreenMachine
{
    /// <summary>VCP code 0x10: luminance, the monitor's brightness.</summary>
    private const byte Luminance = 0x10;

    // CLSID_WbemLocator; TerraFX names the interface but not its class.
    private static readonly Guid WbemLocator = new(0x4590F811, 0x1D3A, 0x11D0, 0x89, 0x1F, 0x00, 0xAA, 0x00, 0x4B, 0x2E, 0x24);

    // What each display has, by its monitor handle, until the displays change.
    private readonly Dictionary<nint, Way> ways = [];

    // When each display was last sent a DDC/CI command, in Environment.TickCount64 ms, by its monitor
    // handle — a physical handle opened afresh is the same wire: the floor under every command to it
    // (Rested), whoever asks, the probe that opens a handle just before a write included.
    private readonly Dictionary<nint, long> spoke = [];
    private IWbemServices* wmi;
    private bool connected;
    private bool com;

    /// <summary>How a display's brightness is reached.</summary>
    private abstract record Way;

    /// <summary>Through WMI, by the instance's name; <paramref name="Steps"/> the levels the panel has (empty: every level).</summary>
    private sealed record ByWmi(string Instance, byte[] Steps) : Way;

    /// <summary>Over DDC/CI, by the physical monitor's handle; its luminance runs 0 to <paramref name="Most"/>.</summary>
    private sealed record ByDdc(nint Handle, uint Most) : Way;

    /// <summary>Not at all.</summary>
    private sealed record NoWay : Way;

    public IReadOnlyList<ScreenFound> Displays()
    {
        Com();
        return [.. Monitors.List().Select(monitor => new ScreenFound(
            monitor.Handle,
            monitor.DeviceName,
            monitor.BuiltIn && monitor.Name.StartsWith("Display ", StringComparison.Ordinal) ? S.DockQuickBrightnessBuiltIn : monitor.Name, // not-ui: "Display " is the name Windows gives
            monitor.BuiltIn)
        {
            Path = monitor.Id,
        })];
    }

    public int? Read(ScreenFound screen) => WayOf(screen) switch
    {
        ByWmi wmi => WmiLevels().TryGetValue(wmi.Instance, out var level) ? level : null,
        ByDdc ddc => DdcLevel(screen.Monitor, ddc.Handle) is { } now ? Percent(now.Current, now.Most) : null,
        _ => null,
    };

    public bool Write(ScreenFound screen, int level) => WayOf(screen) switch
    {
        ByWmi wmi => WmiSet(wmi.Instance, Math.Clamp(level, 0, 100)),
        ByDdc ddc => DdcSet(screen.Monitor, ddc.Handle, Raw(level, ddc.Most)),
        _ => false,
    };

    public (int Low, int High) ShowsFor(ScreenFound screen, int level) => WayOf(screen) switch
    {
        ByWmi wmi => Between(wmi.Steps, level),
        ByDdc ddc => (Percent(Raw(level, ddc.Most), ddc.Most), Percent(Raw(level, ddc.Most), ddc.Most)),
        _ => (Math.Clamp(level, 0, 100), Math.Clamp(level, 0, 100)),
    };

    /// <summary>A level, 0 to 100, in a monitor's luminance running 0 to <paramref name="most"/>. Pure.</summary>
    internal static uint Raw(int level, uint most) => (uint)Math.Round(Math.Clamp(level, 0, 100) * most / 100.0, MidpointRounding.AwayFromZero);

    /// <summary>A monitor's luminance, running 0 to <paramref name="most"/>, as a level 0 to 100. Pure.</summary>
    internal static int Percent(uint raw, uint most) => (int)Math.Round(raw * 100.0 / Math.Max(1, most), MidpointRounding.AwayFromZero);

    /// <summary>
    /// The panel's levels on either side of <paramref name="level"/>: its own where it has it, the
    /// one below and the one above where it does not — which of the two a driver takes is its own
    /// affair. The level itself where WMI says of no levels. Pure.
    /// </summary>
    internal static (int Low, int High) Between(IReadOnlyList<byte> steps, int level)
    {
        level = Math.Clamp(level, 0, 100);
        int? low = null;
        int? high = null;
        foreach (int step in steps)
        {
            if (step <= level && (low is null || step > low))
            {
                low = step;
            }
            if (step >= level && (high is null || step < high))
            {
                high = step;
            }
        }
        return steps.Count == 0 ? (level, level) : (low ?? high ?? level, high ?? low ?? level);
    }

    public void Forget()
    {
        foreach (var way in ways.Values)
        {
            Close(way);
        }
        ways.Clear();
        spoke.Clear();
    }

    public void Forget(ScreenFound screen)
    {
        if (ways.Remove(screen.Monitor, out var way))
        {
            Close(way);
        }
    }

    /// <summary>
    /// A DDC/CI monitor's rest: DDC/CI (VESA, v1.1) has the host wait 50 ms after a Set VCP Feature
    /// before its next message, and a monitor sent one sooner may drop it without a word. WMI's
    /// panel takes its level through the driver, and needs none.
    /// </summary>
    public int Gap(ScreenFound screen) => ways.TryGetValue(screen.Monitor, out var way) && way is ByDdc ? DdcGapMs : 0;

    /// <summary>The rest after a DDC/CI command before the next to the same monitor, in ms (see <see cref="Gap"/>).</summary>
    internal const int DdcGapMs = 50;

    private static void Close(Way way)
    {
        if (way is ByDdc ddc)
        {
            var handle = new PhysicalMonitor { Handle = ddc.Handle };
            _ = DestroyPhysicalMonitors(1, &handle);
        }
    }

    /// <summary>What <paramref name="screen"/> has: learnt once, then kept until <see cref="Forget"/>.</summary>
    private Way WayOf(ScreenFound screen)
    {
        if (ways.TryGetValue(screen.Monitor, out var known))
        {
            return known;
        }
        Way way = WmiInstanceOf(screen) is { } instance ? new ByWmi(instance, WmiSteps(instance))
            : DdcOf(screen.Monitor) is { } ddc ? ddc
            : new NoWay();
        ways[screen.Monitor] = way;
        return way;
    }

    // --- WMI ---------------------------------------------------------------------------------

    /// <summary>
    /// The WMI instance that is <paramref name="screen"/>'s panel: its name, "DISPLAY\BOE0A9C\4&amp;…&amp;UID265988_0",
    /// is the monitor's device path, "\\?\DISPLAY#BOE0A9C#4&amp;…&amp;UID265988#{…}", said another way.
    /// </summary>
    private string? WmiInstanceOf(ScreenFound screen)
    {
        if (screen.Path.Length == 0)
        {
            return null;
        }
        foreach (var instance in WmiLevels().Keys)
        {
            if (screen.Path.Contains(DevicePart(instance), StringComparison.OrdinalIgnoreCase))
            {
                return instance;
            }
        }
        return null;
    }

    /// <summary>"DISPLAY\BOE0A9C\4&amp;…&amp;UID265988_0" as a device path has it: "DISPLAY#BOE0A9C#4&amp;…&amp;UID265988".</summary>
    internal static string DevicePart(string instance)
    {
        var cut = instance.LastIndexOf('_');
        var name = cut > 0 && instance[(cut + 1)..].All(char.IsDigit) ? instance[..cut] : instance;
        return name.Replace('\\', '#');
    }

    /// <summary>Every panel WMI can set, by instance name, with its brightness now; empty where WMI has none.</summary>
    private Dictionary<string, int> WmiLevels()
    {
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var services = Services();
        if (services == null)
        {
            return levels;
        }
        foreach (var handle in RowsOf(services, "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active = TRUE")) // not-ui: a WMI query
        {
            var row = (IWbemClassObject*)handle;
            try
            {
                if (Text(row, "InstanceName") is { Length: > 0 } name && Number(row, "CurrentBrightness") is { } level)
                {
                    levels[name] = Math.Clamp(level, 0, 100);
                }
            }
            finally
            {
                _ = row->Release();
            }
        }
        return levels;
    }

    /// <summary>The levels the panel <paramref name="instance"/> names has (WmiMonitorBrightness.Level); empty where WMI does not say.</summary>
    private byte[] WmiSteps(string instance)
    {
        var services = Services();
        if (services == null)
        {
            return [];
        }
        byte[]? steps = null;
        foreach (var handle in RowsOf(services, "SELECT InstanceName, Level FROM WmiMonitorBrightness WHERE Active = TRUE")) // not-ui: a WMI query
        {
            var row = (IWbemClassObject*)handle;
            try
            {
                if (steps is null && string.Equals(Text(row, "InstanceName"), instance, StringComparison.OrdinalIgnoreCase))
                {
                    steps = Bytes(row, "Level");
                }
            }
            finally
            {
                _ = row->Release();
            }
        }
        return steps ?? [];
    }

    /// <summary>WmiSetBrightness on the panel <paramref name="instance"/> names, at once (no fade).</summary>
    private bool WmiSet(string instance, int level)
    {
        var services = Services();
        if (services == null)
        {
            return false;
        }
        string? path = null;
        foreach (var handle in RowsOf(services, "SELECT __PATH, InstanceName FROM WmiMonitorBrightnessMethods WHERE Active = TRUE")) // not-ui: a WMI query
        {
            var row = (IWbemClassObject*)handle;
            try
            {
                if (path is null && string.Equals(Text(row, "InstanceName"), instance, StringComparison.OrdinalIgnoreCase))
                {
                    path = Text(row, "__PATH");
                }
            }
            finally
            {
                _ = row->Release();
            }
        }
        if (path is null)
        {
            return false;
        }
        IWbemClassObject* type = null;
        IWbemClassObject* signature = null;
        IWbemClassObject* arguments = null;
        try
        {
            fixed (char* className = "WmiMonitorBrightnessMethods")
            fixed (char* method = "WmiSetBrightness")
            {
                if (services->GetObjectW(className, 0, null, &type, null).FAILED
                    || type->GetMethod(method, 0, &signature, null).FAILED
                    || signature->SpawnInstance(0, &arguments).FAILED)
                {
                    return false;
                }
            }
            var timeout = new VARIANT();
            timeout.vt = (ushort)VARENUM.VT_I4;
            timeout.lVal = 0;
            var brightness = new VARIANT();
            brightness.vt = (ushort)VARENUM.VT_UI1;
            brightness.bVal = (byte)level;
            fixed (char* timeoutName = "Timeout")
            fixed (char* brightnessName = "Brightness")
            {
                if (arguments->Put(timeoutName, 0, &timeout, 0).FAILED || arguments->Put(brightnessName, 0, &brightness, 0).FAILED)
                {
                    return false;
                }
            }
            var objectPath = Bstr(path);
            var methodName = Bstr("WmiSetBrightness");
            try
            {
                return services->ExecMethod(objectPath, methodName, 0, null, arguments, null, null).SUCCEEDED;
            }
            finally
            {
                SysFreeString(objectPath);
                SysFreeString(methodName);
            }
        }
        finally
        {
            if (arguments != null)
            {
                _ = arguments->Release();
            }
            if (signature != null)
            {
                _ = signature->Release();
            }
            if (type != null)
            {
                _ = type->Release();
            }
        }
    }

    /// <summary>ROOT\WMI, connected once; null where it cannot be (no WMI, or it refused).</summary>
    private IWbemServices* Services()
    {
        if (connected)
        {
            return wmi;
        }
        connected = true;
        IWbemLocator* locator = null;
        try
        {
            var classId = WbemLocator;
            if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWbemLocator>(), (void**)&locator).FAILED || locator == null)
            {
                return null;
            }
            IWbemServices* services = null;
            var root = Bstr(@"ROOT\WMI");
            try
            {
                if (locator->ConnectServer(root, null, null, null, 0, null, null, &services).FAILED || services == null)
                {
                    return null;
                }
            }
            finally
            {
                SysFreeString(root);
            }
            // RPC_C_AUTHN_WINNT, RPC_C_AUTHZ_NONE, RPC_C_AUTHN_LEVEL_CALL, RPC_C_IMP_LEVEL_IMPERSONATE, EOAC_NONE.
            _ = CoSetProxyBlanket((IUnknown*)services, 10, 0, null, 3, 3, null, 0);
            wmi = services;
            return wmi;
        }
        finally
        {
            if (locator != null)
            {
                _ = locator->Release();
            }
        }
    }

    /// <summary>The rows of a WQL query; each for the caller to release.</summary>
    private static List<nint> RowsOf(IWbemServices* services, string query)
    {
        var rows = new List<nint>();
        IEnumWbemClassObject* results = null;
        var language = Bstr("WQL");
        var text = Bstr(query);
        try
        {
            const int flags = (int)(WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_FORWARD_ONLY | WBEM_GENERIC_FLAG_TYPE.WBEM_FLAG_RETURN_IMMEDIATELY);
            if (services->ExecQuery(language, text, flags, null, &results).FAILED || results == null)
            {
                return rows;
            }
            while (true)
            {
                IWbemClassObject* row = null;
                uint returned = 0;
                // Five seconds for a row: WMI answers at once, or something is wrong with it.
                if (results->Next(5000, 1, &row, &returned).FAILED || returned == 0 || row == null)
                {
                    break;
                }
                rows.Add((nint)row);
            }
            return rows;
        }
        finally
        {
            SysFreeString(language);
            SysFreeString(text);
            if (results != null)
            {
                _ = results->Release();
            }
        }
    }

    /// <summary>A BSTR of <paramref name="text"/>, for the caller to free (SysFreeString).</summary>
    private static char* Bstr(string text)
    {
        fixed (char* chars = text)
        {
            return SysAllocString(chars);
        }
    }

    private static string? Text(IWbemClassObject* row, string name)
    {
        var value = new VARIANT();
        try
        {
            fixed (char* property = name)
            {
                return row->Get(property, 0, &value, null, null).SUCCEEDED && value.vt == (ushort)VARENUM.VT_BSTR && value.bstrVal != null
                    ? new string(value.bstrVal)
                    : null;
            }
        }
        finally
        {
            _ = VariantClear(&value);
        }
    }

    /// <summary>A uint8[] property, as WMI hands one (a SAFEARRAY of VT_UI1); null where it is not one.</summary>
    private static byte[]? Bytes(IWbemClassObject* row, string name)
    {
        var value = new VARIANT();
        try
        {
            fixed (char* property = name)
            {
                if (row->Get(property, 0, &value, null, null).FAILED || value.vt != (ushort)(VARENUM.VT_ARRAY | VARENUM.VT_UI1) || value.parray == null)
                {
                    return null;
                }
            }
            var array = value.parray;
            int lower, upper;
            void* data;
            if (SafeArrayGetLBound(array, 1, &lower).FAILED || SafeArrayGetUBound(array, 1, &upper).FAILED || upper < lower || SafeArrayAccessData(array, &data).FAILED)
            {
                return null;
            }
            try
            {
                return new ReadOnlySpan<byte>(data, upper - lower + 1).ToArray();
            }
            finally
            {
                _ = SafeArrayUnaccessData(array);
            }
        }
        finally
        {
            _ = VariantClear(&value);
        }
    }

    private static int? Number(IWbemClassObject* row, string name)
    {
        var value = new VARIANT();
        try
        {
            fixed (char* property = name)
            {
                if (row->Get(property, 0, &value, null, null).FAILED)
                {
                    return null;
                }
                return value.vt switch
                {
                    (ushort)VARENUM.VT_UI1 => value.bVal,
                    (ushort)VARENUM.VT_I4 => value.lVal,
                    (ushort)VARENUM.VT_I2 => value.iVal,
                    _ => null,
                };
            }
        }
        finally
        {
            _ = VariantClear(&value);
        }
    }

    /// <summary>COM on the worker, once: WMI needs it.</summary>
    private void Com()
    {
        if (com)
        {
            return;
        }
        com = true;
        _ = CoInitializeEx(null, (uint)COINIT.COINIT_MULTITHREADED);
    }

    // --- DDC/CI ------------------------------------------------------------------------------

    /// <summary>Waits out the rest of <paramref name="monitor"/>'s wire after its last DDC/CI command, then marks this one's end.</summary>
    private T Rested<T>(nint monitor, Func<T> command)
    {
        if (spoke.TryGetValue(monitor, out var last) && last + DdcGapMs - Environment.TickCount64 is > 0 and var rest)
        {
            Thread.Sleep((int)rest);
        }
        try
        {
            return command();
        }
        finally
        {
            spoke[monitor] = Environment.TickCount64;
        }
    }

    /// <summary>VCP 0x10 set to <paramref name="value"/>, in the monitor's own range; false where the monitor refused it.</summary>
    private bool DdcSet(nint monitor, nint handle, uint value) => Rested(monitor, () => SetVCPFeature(handle, Luminance, value) != 0);

    /// <summary>The monitor's physical handle and its luminance's range, where it answers VCP 0x10; else null.</summary>
    private ByDdc? DdcOf(nint monitor)
    {
        uint count = 0;
        if (GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, &count) == 0 || count == 0)
        {
            return null;
        }
        var physical = new PhysicalMonitor[count];
        fixed (PhysicalMonitor* all = physical)
        {
            if (GetPhysicalMonitorsFromHMONITOR(monitor, count, all) == 0)
            {
                return null;
            }
            ByDdc? found = null;
            for (var index = 0; index < count; index++)
            {
                // The first that answers is the display's; a monitor answers for itself only.
                if (found is null && DdcLevel(monitor, all[index].Handle) is { Most: > 0 } level)
                {
                    found = new ByDdc(all[index].Handle, level.Most);
                    continue;
                }
                _ = DestroyPhysicalMonitors(1, &all[index]);
            }
            return found;
        }
    }

    /// <summary>The luminance and its most, as the monitor says them now; null where it does not.</summary>
    private (uint Current, uint Most)? DdcLevel(nint monitor, nint handle) => Rested(monitor, () =>
    {
        uint current = 0;
        uint most = 0;
        return GetVCPFeatureAndVCPFeatureReply(handle, Luminance, null, &current, &most) != 0 ? ((uint, uint)?)(current, most) : null;
    });

    [StructLayout(LayoutKind.Sequential)]
    private struct PhysicalMonitor
    {
        public nint Handle;
        public fixed char Description[128];
    }

    [DllImport("dxva2.dll", ExactSpelling = true)]
    private static extern int GetNumberOfPhysicalMonitorsFromHMONITOR(nint monitor, uint* count);

    [DllImport("dxva2.dll", ExactSpelling = true)]
    private static extern int GetPhysicalMonitorsFromHMONITOR(nint monitor, uint count, PhysicalMonitor* monitors);

    [DllImport("dxva2.dll", ExactSpelling = true)]
    private static extern int DestroyPhysicalMonitors(uint count, PhysicalMonitor* monitors);

    [DllImport("dxva2.dll", ExactSpelling = true)]
    private static extern int GetVCPFeatureAndVCPFeatureReply(nint monitor, byte code, int* type, uint* current, uint* most);

    [DllImport("dxva2.dll", ExactSpelling = true)]
    private static extern int SetVCPFeature(nint monitor, byte code, uint value);
}
