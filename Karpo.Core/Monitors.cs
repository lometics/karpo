// The displays attached right now, with an identity that survives reboots.
//
// HMONITORs and \\.\DISPLAYn names are handed out again on every
// reconfiguration, so they can't key a setting. The monitor's device
// interface path (\\?\DISPLAY#DEL41E6#...#{e6f07b5f-...}) stays the same for a
// given monitor on a given port, and DisplayConfig also gives the name
// Windows Settings shows for it. The dock and the settings app both list
// displays through here, so they agree on the ids.

using System.Runtime.InteropServices;

namespace Karpo.Core;

public readonly record struct RectI(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed record MonitorInfo(
    string Id,
    string Name,
    string DeviceName,
    bool Primary,
    RectI Bounds,
    RectI Work,
    nint Handle,
    uint Dpi)
{
    /// <summary>
    /// The PC's own screen — a laptop's panel, joined inside it — rather than a monitor plugged into
    /// it, as DisplayConfig's output technology says. False where DisplayConfig said nothing.
    /// </summary>
    public bool BuiltIn { get; init; }
}

public static unsafe class Monitors
{
    /// <summary>Every active display, primary first, then left to right.</summary>
    public static List<MonitorInfo> List()
    {
        var names = FriendlyNames();
        var found = new List<MonitorInfo>();
        var handle = GCHandle.Alloc(found);
        try
        {
            EnumDisplayMonitors(0, null, &Visit, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        var numbered = new List<MonitorInfo>(found.Count);
        foreach (var monitor in found)
        {
            var (id, name, builtIn) = names.TryGetValue(monitor.DeviceName, out var known)
                ? known
                : (InterfacePath(monitor.DeviceName) ?? monitor.DeviceName, "", false);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = S.CoreMonitorDisplay(DisplayNumber(monitor.DeviceName));
            }
            numbered.Add(monitor with { Id = id.ToLowerInvariant(), Name = name, BuiltIn = builtIn });
        }
        return numbered
            .OrderByDescending(m => m.Primary)
            .ThenBy(m => m.Bounds.Left)
            .ThenBy(m => m.Bounds.Top)
            .ToList();
    }

    [UnmanagedCallersOnly]
    private static int Visit(nint monitor, nint dc, RECT* clip, nint state)
    {
        try
        {
            var info = new MONITORINFOEXW { cbSize = (uint)sizeof(MONITORINFOEXW) };
            if (GetMonitorInfoW(monitor, &info) == 0)
            {
                return 1;
            }
            uint dpiX = 96, dpiY = 96;
            _ = GetDpiForMonitor(monitor, 0 /* MDT_EFFECTIVE_DPI */, &dpiX, &dpiY);
            var list = (List<MonitorInfo>)GCHandle.FromIntPtr(state).Target!;
            list.Add(new MonitorInfo(
                Id: "",
                Name: "",
                DeviceName: new string(info.szDevice),
                Primary: (info.dwFlags & 1 /* MONITORINFOF_PRIMARY */) != 0,
                Bounds: info.rcMonitor.ToRect(),
                Work: info.rcWork.ToRect(),
                Handle: monitor,
                Dpi: dpiX));
            return 1;
        }
        catch (Exception error)
        {
            Troubles.Report("Monitors: listing the displays", error);
            return 1;
        }
    }

    /// <summary>
    /// \\.\DISPLAYn -> (device interface path, friendly name), from the active
    /// display paths. Empty when DisplayConfig fails, which only costs names.
    /// </summary>
    private static Dictionary<string, (string Id, string Name, bool BuiltIn)> FriendlyNames()
    {
        var result = new Dictionary<string, (string, string, bool)>(StringComparer.OrdinalIgnoreCase);
        const uint QDC_ONLY_ACTIVE_PATHS = 2;
        // The path count can change between the two calls; retry when it does.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out var pathCount, out var modeCount) != 0)
            {
                return result;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
            int status;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            {
                status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &pathCount, p, &modeCount, m, 0);
            }
            if (status == 122 /* ERROR_INSUFFICIENT_BUFFER */)
            {
                continue;
            }
            if (status != 0)
            {
                return result;
            }
            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var source = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                source.header.type = 1; // DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME
                source.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
                source.header.adapterId = path.sourceInfo.adapterId;
                source.header.id = path.sourceInfo.id;
                if (DisplayConfigGetDeviceInfo(&source.header) != 0)
                {
                    continue;
                }
                var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                target.header.type = 2; // DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME
                target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
                target.header.adapterId = path.targetInfo.adapterId;
                target.header.id = path.targetInfo.id;
                if (DisplayConfigGetDeviceInfo(&target.header) != 0)
                {
                    continue;
                }
                var gdiName = new string(source.viewGdiDeviceName);
                var devicePath = new string(target.monitorDevicePath);
                var friendly = new string(target.monitorFriendlyDeviceName);
                if (!string.IsNullOrEmpty(gdiName) && !string.IsNullOrEmpty(devicePath))
                {
                    result[gdiName] = (devicePath, friendly, BuiltIn(target.outputTechnology));
                }
            }
            return result;
        }
        return result;
    }

    /// <summary>
    /// Whether a DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY is a screen joined inside the PC: LVDS (6),
    /// embedded DisplayPort (11), embedded UDI (13), or one the driver calls internal (0x80000000).
    /// </summary>
    internal static bool BuiltIn(int technology) => technology is 6 or 11 or 13 or unchecked((int)0x80000000);

    /// <summary>The first monitor's interface path on an adapter, for when DisplayConfig had nothing.</summary>
    private static string? InterfacePath(string adapter)
    {
        var device = new DISPLAY_DEVICEW { cb = (uint)sizeof(DISPLAY_DEVICEW) };
        fixed (char* name = adapter)
        {
            const uint EDD_GET_DEVICE_INTERFACE_NAME = 1;
            if (EnumDisplayDevicesW(name, 0, &device, EDD_GET_DEVICE_INTERFACE_NAME) == 0)
            {
                return null;
            }
        }
        var id = new string(device.DeviceID);
        return string.IsNullOrEmpty(id) ? null : id;
    }

    private static string DisplayNumber(string deviceName)
    {
        var digits = new string(deviceName.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return digits.Length > 0 ? digits : "?";
    }

    private static RectI ToRect(this RECT rect) => new(rect.left, rect.top, rect.right, rect.bottom);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFOEXW
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        public fixed char szDevice[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAY_DEVICEW
    {
        public uint cb;
        public fixed char DeviceName[32];
        public fixed char DeviceString[128];
        public uint StateFlags;
        public fixed char DeviceID[128];
        public fixed char DeviceKey[128];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public int outputTechnology;
        public int rotation;
        public int scaling;
        public uint refreshRateNumerator;
        public uint refreshRateDenominator;
        public int scanLineOrdering;
        public int targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    /// <summary>Only its size matters here: 64 bytes.</summary>
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public int infoType;
        public uint id;
        public LUID adapterId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public int type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public fixed char viewGdiDeviceName[32];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public int outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        public fixed char monitorFriendlyDeviceName[64];
        public fixed char monitorDevicePath[128];
    }

    [DllImport("user32.dll")]
    private static extern int EnumDisplayMonitors(nint hdc, RECT* clip, delegate* unmanaged<nint, nint, RECT*, nint, int> callback, nint data);

    [DllImport("user32.dll")]
    private static extern int GetMonitorInfoW(nint monitor, MONITORINFOEXW* info);

    [DllImport("user32.dll")]
    private static extern int EnumDisplayDevicesW(char* device, uint index, DISPLAY_DEVICEW* info, uint flags);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(nint monitor, int type, uint* dpiX, uint* dpiY);

    [DllImport("user32.dll")]
    private static extern int GetDisplayConfigBufferSizes(uint flags, out uint pathCount, out uint modeCount);

    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, uint* pathCount, DISPLAYCONFIG_PATH_INFO* paths, uint* modeCount, DISPLAYCONFIG_MODE_INFO* modes, nint topology);

    [DllImport("user32.dll")]
    private static extern int DisplayConfigGetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_HEADER* request);
}
