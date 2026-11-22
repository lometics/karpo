// What a Bluetooth device's own PnP nodes say about it: its battery, and what kind
// of thing it is. Two facts the association endpoint the flyout lists cannot give.
//
// Windows keeps a classic device's level in a device property,
// {104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2, which is what its own Settings page
// reads. The catch, measured on this machine rather than assumed: the property is
// NOT on the endpoint. It sits on one of the device nodes inside the endpoint's
// container — for a WH-1000XM4 the node called "WH-1000XM4 Hands-Free AG" — so
// the way to it is the container:
//
//   AEP -> System.Devices.Aep.ContainerId -> the device nodes of that container
//       -> the first that answers the property at all.
//
// Twelve nodes came back for that headset and exactly one carried a level (20, as
// a Byte). Anything else — a mouse, a keyboard, a speaker that never reports —
// answers nothing, and nothing is what the row then shows. Never 0 %: a device
// that says nothing has not said zero.
//
// The same walk hands back the nodes' setup classes, which is how a row knows to
// draw a headset rather than the plain Bluetooth rune. That headset's nodes are
// Media, System and Bluetooth; a mouse's are Mouse and HIDClass. WindowsBluetooth
// picks the first that names something (GlyphFor), and a device whose nodes name
// nothing keeps the plain rune rather than being guessed at.
//
// THE NODES ARE READ THROUGH SETUPAPI, NOT WinRT, and that is the point of this
// file's shape. The WinRT way of it — DeviceInformation.FindAllAsync(aqs,
// additionalProperties, kind) — takes the property list as an IIterable<string>,
// and no managed collection can cross the WinRT ABI in this app: the bar is
// published native AOT, no CCW is generated for one, and the call throws before it
// starts (see the rule at the top of Bluetooth.cs, and docs\widgets.md section 10).
// SetupAPI has no such edge. It is also less work than the query was: 0.42 ms a
// device on this PC, 3.4 ms for eight, measured — the panel never waits on it
// either way, because it runs on the pool.
//
// Bluetooth LE devices carry the GATT Battery Service instead. That is a
// different road (GattDeviceService, a connection, a characteristic read) and it
// is not taken here: on this machine the LE devices are unpaired ones the panel
// only offers to pair, and opening a GATT connection to something the person has
// not paired to read its battery is not a thing a taskbar should do. A paired LE
// device that keeps the level in its device node is found by the pass below like
// any other, because BTHLEDEVICE is one of the enumerators it walks.
//
// Everything here runs off the dock's thread, like everything else the panel's
// services do (see the top of Bluetooth.cs).

using System.Runtime.InteropServices;
using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>
/// What one device's nodes said. <paramref name="Kinds"/> is their setup classes in the order
/// they were found, and empty for a device whose nodes name nothing.
/// </summary>
internal readonly record struct NodeFacts(int? Level, IReadOnlyList<Guid> Kinds);

internal static class BluetoothNodes
{
    /// <summary>The device property Windows' own Settings reads for a classic device's level.</summary>
    internal const string Level = "{104EA319-6EE2-4701-BD47-8DDBF425BBE5} 2";

    /// <summary>The AEP property that says which container the device's nodes are in.</summary>
    internal const string Container = "System.Devices.Aep.ContainerId";

    /// <summary>
    /// What the nodes of <paramref name="container"/> say: a percentage as Windows gives it (a
    /// whole number, clamped to 0-100 because a byte can say anything) or null where nothing in
    /// the container answers, and the classes of the nodes that were there.
    /// <para>
    /// Synchronous, and meant for the pool: it is a registry read per node and a dozen nodes, not
    /// a radio round trip. Never call it on the dock's thread.
    /// </para>
    /// </summary>
    public static NodeFacts Read(Guid container)
    {
        if (container == Guid.Empty)
        {
            return new NodeFacts(null, []);
        }
        int? level = null;
        var kinds = new List<Guid>();
        try
        {
            foreach (var family in Families)
            {
                Walk(family, container, ref level, kinds);
            }
        }
        catch (Exception error)
        {
            // A device that goes mid-read is the ordinary case for Bluetooth.
            Troubles.Note("Bluetooth nodes", error);
        }
        return new NodeFacts(level, kinds);
    }

    /// <summary>
    /// A level as a percentage, or null for one that means "nothing said". Windows hands it over
    /// as a byte; anything else, and anything outside 0-100, is not a level.
    /// </summary>
    internal static int? Percent(object? value) => value switch
    {
        null => null,
        byte level => level <= 100 ? level : null,
        int level => level is >= 0 and <= 100 ? level : null,
        uint level => level <= 100 ? (int)level : null,
        _ => null,
    };

    /// <summary>
    /// The PnP enumerators a Bluetooth device's nodes live under: classic first, because that is
    /// where a level is nearly always found, then the two the LE stack has used.
    /// </summary>
    private static readonly string[] Families = ["BTHENUM", "BTHLEDEVICE", "BTHLE"];

    /// <summary>
    /// Reads every node of <paramref name="family"/> that belongs to <paramref name="container"/>:
    /// the first level any of them gives, and all of their classes.
    /// </summary>
    private static void Walk(string family, Guid container, ref int? level, List<Guid> kinds)
    {
        var set = SetupDiGetClassDevsW(0, family, 0, AllClasses | Present);
        if (set == -1)
        {
            return;
        }
        try
        {
            for (uint index = 0; ; index++)
            {
                var node = new DevInfo { Size = (uint)Marshal.SizeOf<DevInfo>() };
                if (!SetupDiEnumDeviceInfo(set, index, ref node))
                {
                    return;
                }
                // The container first: it is the cheap half of the pair, and most nodes are some
                // other device's.
                if (Bytes(set, ref node, ContainerKey, GuidType) is not { Length: 16 } id || new Guid(id) != container)
                {
                    continue;
                }
                if (!kinds.Contains(node.Class))
                {
                    kinds.Add(node.Class);
                }
                if (level is null && Bytes(set, ref node, LevelKey, ByteType) is { Length: > 0 } said)
                {
                    level = Percent(said[0]);
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(set);
        }
    }

    /// <summary>
    /// A node's property as Windows stores it, or null where the node does not have it or stores
    /// it as something other than <paramref name="wanted"/>. Asked for twice: once for the size,
    /// once for the value, which is how SetupAPI hands over anything.
    /// </summary>
    private static byte[]? Bytes(nint set, ref DevInfo node, DevPropertyKey key, uint wanted)
    {
        SetupDiGetDevicePropertyW(set, ref node, ref key, out var kind, null, 0, out var needed, 0);
        if (needed == 0 || kind != wanted)
        {
            return null;
        }
        var buffer = new byte[needed];
        return SetupDiGetDevicePropertyW(set, ref node, ref key, out _, buffer, needed, out _, 0) ? buffer : null;
    }

    private static DevPropertyKey LevelKey => new(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);

    private static DevPropertyKey ContainerKey => new(new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"), 2);

    private const uint Present = 0x2;
    private const uint AllClasses = 0x4;
    private const uint ByteType = 0x03;
    private const uint GuidType = 0x0D;

    /// <summary>SP_DEVINFO_DATA: what SetupAPI calls one device node.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DevInfo
    {
        public uint Size;
        public Guid Class;
        public uint Instance;
        public nint Reserved;
    }

    /// <summary>DEVPROPKEY: a property's category and its number within it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropertyKey(Guid format, uint id)
    {
        public Guid Format = format;
        public uint Id = id;
    }

    [DllImport("setupapi.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint SetupDiGetClassDevsW(nint classGuid, string? enumerator, nint parent, uint flags);

    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiEnumDeviceInfo(nint set, uint index, ref DevInfo node);

    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiGetDevicePropertyW(
        nint set, ref DevInfo node, ref DevPropertyKey key, out uint kind, byte[]? buffer, uint size, out uint needed, uint flags);

    [DllImport("setupapi.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetupDiDestroyDeviceInfoList(nint set);
}
