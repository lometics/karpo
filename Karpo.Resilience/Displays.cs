// What the harness does to the displays, every change of it undone by Restore: the whole
// configuration (paths and modes, as QueryDisplayConfig gives them) is written to a file before the
// first change, so even a run cut short can be put back (`restore`). Mode and rotation changes are
// dynamic (ChangeDisplaySettingsEx without CDS_UPDATEREGISTRY), topology changes are applied without
// SDC_SAVE_TO_DATABASE: nothing the person set is overwritten. The scale is the one change Windows
// keeps in the registry, so the scale before is put back by the same call.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Resilience;

internal static unsafe class Displays
{
    private static readonly string SavedPath = Path.Combine(Path.GetTempPath(), "karpo-resilience-displays.bin");

    private static (DISPLAYCONFIG_PATH_INFO[] Paths, DISPLAYCONFIG_MODE_INFO[] Modes) Query()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            uint pathCount, modeCount;
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount) != 0)
            {
                Thread.Sleep(200);
                continue;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[Math.Max(1, pathCount)];
            var modes = new DISPLAYCONFIG_MODE_INFO[Math.Max(1, modeCount)];
            int status;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            {
                status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &pathCount, p, &modeCount, m, null);
            }
            if (status == ERROR.ERROR_INSUFFICIENT_BUFFER)
            {
                continue;
            }
            if (status != 0)
            {
                throw new InvalidOperationException($"QueryDisplayConfig failed: {status}");
            }
            return (paths[..(int)pathCount], modes[..(int)modeCount]);
        }
        throw new InvalidOperationException("QueryDisplayConfig kept changing");
    }

    private static int Apply(DISPLAYCONFIG_PATH_INFO[] paths, DISPLAYCONFIG_MODE_INFO[] modes)
    {
        fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
        fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
        {
            return SetDisplayConfig((uint)paths.Length, p, (uint)modes.Length, m, C.SDC_APPLY | C.SDC_USE_SUPPLIED_DISPLAY_CONFIG | C.SDC_ALLOW_CHANGES);
        }
    }

    /// <summary>
    /// Writes the configuration as it is now, once per run: a second call keeps the first, so what is put
    /// back is always the desktop as the run found it, never one a scenario left half changed.
    /// </summary>
    public static void Save()
    {
        if (File.Exists(SavedPath))
        {
            return;
        }
        var (paths, modes) = Query();
        var seen = Probe.Displays();
        using var file = File.Create(SavedPath);
        using var writer = new BinaryWriter(file);
        writer.Write(paths.Length);
        writer.Write(MemoryMarshal.AsBytes(paths.AsSpan()));
        writer.Write(modes.Length);
        writer.Write(MemoryMarshal.AsBytes(modes.AsSpan()));
        writer.Write(seen.Count);
        foreach (var display in seen)
        {
            writer.Write(Says(display));
        }
        foreach (var source in Sources())
        {
            writer.Write(source.Device);
            writer.Write(Scale(source.Device)?.Current ?? 0);
        }
    }

    private static string Says(DisplayFacts display) => $"{display.Device} {display.Bounds} {display.Dpi}";

    /// <summary>The saved configuration is the run's: forgotten when the run ends.</summary>
    public static void Forget() => File.Delete(SavedPath);

    /// <summary>Puts the saved configuration back, and the scales; what it did.</summary>
    public static string RestoreSaved()
    {
        if (!File.Exists(SavedPath))
        {
            return "nothing saved";
        }
        DISPLAYCONFIG_PATH_INFO[] paths;
        DISPLAYCONFIG_MODE_INFO[] modes;
        var scales = new List<(string Device, int Percent)>();
        var was = new List<string>();
        using (var file = File.OpenRead(SavedPath))
        using (var reader = new BinaryReader(file))
        {
            paths = new DISPLAYCONFIG_PATH_INFO[reader.ReadInt32()];
            reader.Read(MemoryMarshal.AsBytes(paths.AsSpan()));
            modes = new DISPLAYCONFIG_MODE_INFO[reader.ReadInt32()];
            reader.Read(MemoryMarshal.AsBytes(modes.AsSpan()));
            for (var count = reader.ReadInt32(); count > 0; count--)
            {
                was.Add(reader.ReadString());
            }
            while (file.Position < file.Length)
            {
                scales.Add((reader.ReadString(), reader.ReadInt32()));
            }
        }
        // Nothing to do when the desktop is as it was: a configuration applied again is itself a change of displays.
        if (Probe.Displays().Select(Says).Order().SequenceEqual(was.Order()) && scales.All(entry => entry.Percent == 0 || Scale(entry.Device)?.Current == entry.Percent))
        {
            return "displays as they were";
        }
        // Dynamic modes first: a display left at 1024x768 would be taken as the configuration's mode otherwise.
        _ = ChangeDisplaySettingsExW(null, null, HWND.NULL, 0, null);
        var status = Apply(paths, modes);
        if (status != 0)
        {
            // The saved paths can name modes the driver no longer offers; the database's extend is the person's own.
            status = SetDisplayConfig(0, null, 0, null, C.SDC_APPLY | C.SDC_TOPOLOGY_EXTEND);
        }
        Thread.Sleep(1500);
        var said = new List<string> { status == 0 ? "displays put back" : $"SetDisplayConfig failed: {status}" };
        foreach (var (device, percent) in scales.Where(entry => entry.Percent > 0))
        {
            if (Scale(device) is { } scale && scale.Current != percent)
            {
                said.Add(SetScale(device, percent) ? $"{device} scale back to {percent} %" : $"{device} scale NOT put back to {percent} %");
            }
        }
        return string.Join("; ", said);
    }

    /// <summary>The active sources: GDI device name, adapter and source id, and the index of its path.</summary>
    public static List<(string Device, LUID Adapter, uint Id, int Path)> Sources()
    {
        var (paths, _) = Query();
        var found = new List<(string, LUID, uint, int)>();
        for (var i = 0; i < paths.Length; i++)
        {
            var name = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
            name.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
            name.header.size = (uint)sizeof(DISPLAYCONFIG_SOURCE_DEVICE_NAME);
            name.header.adapterId = paths[i].sourceInfo.adapterId;
            name.header.id = paths[i].sourceInfo.id;
            if (DisplayConfigGetDeviceInfo(&name.header) == 0)
            {
                found.Add((new string((char*)&name.viewGdiDeviceName), paths[i].sourceInfo.adapterId, paths[i].sourceInfo.id, i));
            }
        }
        return found;
    }

    /// <summary>Takes <paramref name="device"/> out of the desktop, as unplugging its cable would.</summary>
    public static string Detach(string device)
    {
        Save();
        var (paths, modes) = Query();
        var index = Sources().FindIndex(source => source.Device == device);
        if (index < 0)
        {
            return $"{device} is not active";
        }
        var path = Sources()[index].Path;
        var kept = paths.Where((_, i) => i != path).ToArray();
        // The primary stands at 0,0; when it goes, the first one left is moved there (else SetDisplayConfig refuses, 87).
        var gone = modes[paths[path].sourceInfo.Anonymous.modeInfoIdx].Anonymous.sourceMode.position;
        if (gone.x == 0 && gone.y == 0 && kept.Length > 0)
        {
            var origin = modes[kept[0].sourceInfo.Anonymous.modeInfoIdx].Anonymous.sourceMode.position;
            for (var i = 0; i < modes.Length; i++)
            {
                if (modes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
                {
                    modes[i].Anonymous.sourceMode.position.x -= origin.x;
                    modes[i].Anonymous.sourceMode.position.y -= origin.y;
                }
            }
        }
        var status = Apply(kept, modes);
        return status == 0 ? $"{device} taken out" : $"SetDisplayConfig failed: {status}";
    }

    /// <summary>Every display showing the same picture (Win+P Duplicate).</summary>
    public static string Clone()
    {
        Save();
        var status = SetDisplayConfig(0, null, 0, null, C.SDC_APPLY | C.SDC_TOPOLOGY_CLONE);
        return status == 0 ? "displays duplicated" : $"SetDisplayConfig(clone) failed: {status}";
    }

    /// <summary>Makes <paramref name="device"/> the primary display: every source moved so it stands at 0,0.</summary>
    public static string MakePrimary(string device)
    {
        Save();
        var (paths, modes) = Query();
        var source = Sources().Find(s => s.Device == device);
        if (source.Device is null)
        {
            return $"{device} is not active";
        }
        var modeIndex = paths[source.Path].sourceInfo.Anonymous.modeInfoIdx;
        var origin = modes[modeIndex].Anonymous.sourceMode.position;
        for (var i = 0; i < modes.Length; i++)
        {
            if (modes[i].infoType == DISPLAYCONFIG_MODE_INFO_TYPE.DISPLAYCONFIG_MODE_INFO_TYPE_SOURCE)
            {
                modes[i].Anonymous.sourceMode.position.x -= origin.x;
                modes[i].Anonymous.sourceMode.position.y -= origin.y;
            }
        }
        var status = Apply(paths, modes);
        return status == 0 ? $"{device} made primary" : $"SetDisplayConfig failed: {status}";
    }

    /// <summary>A lower mode on <paramref name="device"/> for now (not written to the registry); null when it has none to offer.</summary>
    public static string Mode(string device, int width, int height, uint orientation = DMDO_DEFAULT)
    {
        Save();
        DEVMODEW mode;
        mode.dmSize = (ushort)sizeof(DEVMODEW);
        fixed (char* name = device)
        {
            if (!EnumDisplaySettingsW(name, C.ENUM_CURRENT_SETTINGS, &mode))
            {
                return $"{device}: no current mode";
            }
            var turned = orientation is DMDO_90 or DMDO_270;
            mode.dmPelsWidth = (uint)(turned ? height : width);
            mode.dmPelsHeight = (uint)(turned ? width : height);
            mode.dmFields = C.DM_PELSWIDTH | C.DM_PELSHEIGHT;
            if (orientation != DMDO_DEFAULT || mode.Anonymous1.Anonymous2.dmDisplayOrientation != DMDO_DEFAULT)
            {
                mode.Anonymous1.Anonymous2.dmDisplayOrientation = orientation;
                mode.dmFields |= C.DM_DISPLAYORIENTATION;
            }
            var status = ChangeDisplaySettingsExW(name, &mode, HWND.NULL, 0, null);
            return status == C.DISP_CHANGE_SUCCESSFUL ? $"{device} at {mode.dmPelsWidth}x{mode.dmPelsHeight}{(orientation != DMDO_DEFAULT ? $" turned {orientation * 90}°" : "")}" : $"{device}: ChangeDisplaySettingsEx said {status}";
        }
    }

    /// <summary>The device's mode back to the registry's.</summary>
    public static string ModeBack(string device)
    {
        fixed (char* name = device)
        {
            var status = ChangeDisplaySettingsExW(name, null, HWND.NULL, 0, null);
            return status == C.DISP_CHANGE_SUCCESSFUL ? $"{device} back to its own mode" : $"{device}: ChangeDisplaySettingsEx(null) said {status}";
        }
    }

    public static (uint Width, uint Height) CurrentMode(string device)
    {
        DEVMODEW mode;
        mode.dmSize = (ushort)sizeof(DEVMODEW);
        fixed (char* name = device)
        {
            return EnumDisplaySettingsW(name, C.ENUM_CURRENT_SETTINGS, &mode) ? (mode.dmPelsWidth, mode.dmPelsHeight) : (0, 0);
        }
    }

    // --- The scale: DisplayConfig's undocumented DPI types (as Settings itself uses them) -----------

    private static readonly int[] Percents = [100, 125, 150, 175, 200, 225, 250, 300, 350, 400, 450, 500];

    [StructLayout(LayoutKind.Sequential)]
    private struct GetDpi
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public int Min;
        public int Current;
        public int Max;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SetDpi
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER Header;
        public int Scale;
    }

    /// <summary>The source's scale: current percent, and the percents it may be set to.</summary>
    public static (int Current, int Recommended, int Min, int Max)? Scale(string device)
    {
        var source = Sources().Find(s => s.Device == device);
        if (source.Device is null)
        {
            return null;
        }
        var get = new GetDpi();
        get.Header.type = (DISPLAYCONFIG_DEVICE_INFO_TYPE)(-3);
        get.Header.size = (uint)sizeof(GetDpi);
        get.Header.adapterId = source.Adapter;
        get.Header.id = source.Id;
        if (DisplayConfigGetDeviceInfo(&get.Header) != 0)
        {
            return null;
        }
        // Relative to the recommended scale, whose index is -Min.
        var recommended = Math.Abs(get.Min);
        int At(int relative) => Percents[Math.Clamp(recommended + relative, 0, Percents.Length - 1)];
        return (At(get.Current), At(0), At(get.Min), At(get.Max));
    }

    public static bool SetScale(string device, int percent)
    {
        Save();
        var source = Sources().Find(s => s.Device == device);
        if (source.Device is null || Scale(device) is not { } scale)
        {
            return false;
        }
        var recommended = Array.IndexOf(Percents, scale.Recommended);
        var wanted = Array.IndexOf(Percents, percent);
        if (wanted < 0)
        {
            return false;
        }
        var set = new SetDpi();
        set.Header.type = (DISPLAYCONFIG_DEVICE_INFO_TYPE)(-4);
        set.Header.size = (uint)sizeof(SetDpi);
        set.Header.adapterId = source.Adapter;
        set.Header.id = source.Id;
        set.Scale = wanted - recommended;
        return DisplayConfigSetDeviceInfo(&set.Header) == 0;
    }
}
