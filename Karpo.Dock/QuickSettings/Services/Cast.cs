// The wireless displays, for the cast preset (docs\widgets.md §114): the
// Miracast receivers in range, for its line (CastPage), and the one this PC is
// showing on now, for that line and for the cast face, which only asks that.
//
// WHAT AN UNPACKAGED APP CAN AND CANNOT DO HERE, measured on this PC from an
// unpackaged process (2026-10-08). Finding them works: ProjectionManager's own
// device selector answers, and its Miracast half is the query below — Wi-Fi
// Direct association endpoints that offer the Miracast service. It is written
// out rather than asked for because the selector ORs in every wired monitor as
// well, which is not something to cast to. Connecting does not: the one
// connecting call Windows publishes, ProjectionManager.StartProjectingAsync,
// takes two application view ids, which only a packaged app's CoreWindow views
// have, and Windows.Media.Casting casts a media element's stream, not the
// screen. Disconnecting has no public call at all. Checked again 2026-10-11 for
// the cast preset: Windows.Media.Miracast is the receiving end (this PC as a
// display), Windows.Devices.Display.Core drives displays already attached, and
// a Wi-Fi Direct connection (WiFiDirectDevice) is the radio link alone, without
// the Miracast session and the display path Windows' Connect broker sets up.
// So connecting and disconnecting are Windows' own cast panel's (Win+K), which
// the line opens — from a display's row, and from its "Open Cast" button — and
// the list is what Karpo adds: what is in range, and what is connected, before
// the panel. How the screens are shown once connected is Win+P's, which is
// Karpo's own (Projection.cs, the preset's other line).
//
// The display being cast to is read where Windows keeps it, not guessed from the
// list: it is an active display path whose output technology is Miracast
// (QueryDisplayConfig), named by its target's friendly name. One that is
// connected but no longer advertising still shows, as connected.
//
// The watcher's events arrive on the pool, where the record is built; the dock
// reads plain records only (Bluetooth.cs explains why, and why no list of
// properties is handed to CreateWatcher).

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Enumeration = Windows.Devices.Enumeration;

namespace Karpo.Dock.QuickSettings.Services;

/// <summary>A wireless display as the flyout lists it.</summary>
internal sealed record CastDisplay(string Id, string Name, bool Connected);

/// <param name="Looking">Still looking: the watcher has not finished its first pass.</param>
/// <param name="Problem">Why it can't look at all, in the language now.</param>
/// <param name="Known">Whether the display cast to has been read yet: until then none is not "none".</param>
internal sealed record CastState(bool Looking, IReadOnlyList<CastDisplay> Displays, string? Problem = null, bool Known = true)
{
    /// <summary>The displays this PC shows on now, by name: one, nearly always, or none.</summary>
    public IEnumerable<string> CastTo => Displays.Where(display => display.Connected).Select(display => display.Name);
}

internal interface ICastSource : IDisposable
{
    void Start(Changes changes);

    CastState Read();

    /// <summary>Opens Windows' own cast panel (Win+K), where a display is connected and disconnected.</summary>
    void OpenCast();

    /// <summary>Reads again, off the dock's thread, which display is cast to: the face's tick.</summary>
    void Poll();
}

internal static class CastNews
{
    public const int Displays = 1;
}

internal static class CastDisplays
{
    /// <summary>
    /// The Miracast half of ProjectionManager.GetDeviceSelector(): Wi-Fi Direct association
    /// endpoints that offer the Miracast service (see the top of this file).
    /// </summary>
    internal const string Query =
        "System.Devices.Aep.ProtocolId:=\"{0407d24e-53de-4c9a-9ba1-9ced54641188}\" AND System.Devices.WiFiDirect.Services:~=\"Miracast\""; // not-ui: a device query

    /// <summary>
    /// The rows: what was found, the ones connected now marked so and first, then by name; a display
    /// connected that is not advertising any more still listed, as connected. Pure: the tests check it.
    /// </summary>
    public static List<CastDisplay> Merge(IEnumerable<CastDisplay> found, IReadOnlyCollection<string> connected)
    {
        var rows = new List<CastDisplay>();
        var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var display in found)
        {
            if (string.IsNullOrWhiteSpace(display.Name))
            {
                continue;
            }
            var on = connected.Contains(display.Name, StringComparer.OrdinalIgnoreCase);
            rows.Add(display with { Connected = on });
            named.Add(display.Name);
        }
        foreach (var name in connected.Where(name => !string.IsNullOrWhiteSpace(name) && !named.Contains(name)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new CastDisplay("connected:" + name, name, true));
        }
        return [.. rows.OrderByDescending(row => row.Connected).ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}

/// <summary>
/// Windows' own: the Miracast receivers in range, and the one connected now. The face's
/// (<paramref name="discover"/> false) looks for no receivers: a radio scan for as long as the bar
/// runs would be a cost for a dot.
/// </summary>
internal sealed unsafe class WindowsCast(bool discover = true) : ICastSource
{
    /// <summary>DISPLAYCONFIG_OUTPUT_TECHNOLOGY_MIRACAST.</summary>
    private const int Miracast = 15;

    private readonly object gate = new();
    private readonly Dictionary<string, CastDisplay> found = [];
    private volatile IReadOnlyCollection<string> connected = [];
    private Enumeration.DeviceWatcher? watcher;
    private Changes? changes;
    private volatile bool looking = true;
    private volatile string? problem;

    // Whether the display cast to has been read once.
    private volatile bool read;

    public void Start(Changes news)
    {
        changes = news;
        Poll();
        if (!discover)
        {
            looking = false;
            return;
        }
        try
        {
            // No properties: a managed collection cannot cross to WinRT in the AOT build (Bluetooth.cs).
            var watching = Enumeration.DeviceInformation.CreateWatcher(CastDisplays.Query, null, Enumeration.DeviceInformationKind.AssociationEndpoint);
            watching.Added += (_, device) =>
            {
                Keep(device);
                changes?.Add(CastNews.Displays);
            };
            watching.Updated += (_, update) =>
            {
                // Nothing a row shows changes in an update but the name, which comes with the next add.
                changes?.Add(CastNews.Displays);
            };
            watching.Removed += (_, update) =>
            {
                lock (gate)
                {
                    found.Remove(update.Id);
                }
                changes?.Add(CastNews.Displays);
            };
            watching.EnumerationCompleted += (_, _) =>
            {
                looking = false;
                changes?.Add(CastNews.Displays);
            };
            watching.Start();
            watcher = watching;
        }
        catch (Exception error)
        {
            looking = false;
            problem = S.DockQuickCastWatchFailed(error.Message);
            Troubles.Note("Quick settings: watching the wireless displays", error);
        }
    }

    public CastState Read()
    {
        List<CastDisplay> listed;
        lock (gate)
        {
            listed = [.. found.Values];
        }
        return new CastState(looking, CastDisplays.Merge(listed, connected), problem, read);
    }

    public void OpenCast() => QuickSwitches.Chord([VK.VK_LWIN, 'K']);

    public void Poll() => _ = Task.Run(() => Troubles.Guard("Quick settings: reading the cast display", () =>
    {
        var now = Connected();
        // The first read is news even where nothing is cast to: the face then knows it.
        var news = !read || !now.SequenceEqual(connected, StringComparer.OrdinalIgnoreCase);
        connected = now;
        read = true;
        if (news)
        {
            changes?.Add(CastNews.Displays);
        }
    }));

    public void Dispose()
    {
        var watching = watcher;
        watcher = null;
        try
        {
            if (watching is { Status: Enumeration.DeviceWatcherStatus.Started or Enumeration.DeviceWatcherStatus.EnumerationCompleted })
            {
                watching.Stop();
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>The display as a row, built on the thread it arrived on.</summary>
    private void Keep(Enumeration.DeviceInformation device)
    {
        try
        {
            var row = new CastDisplay(device.Id, device.Name ?? "", false);
            lock (gate)
            {
                found[row.Id] = row;
            }
        }
        catch (Exception)
        {
            // A display that went away while it was read is ordinary for a radio.
        }
    }

    /// <summary>The friendly names of the active paths Windows drives over Miracast: what this PC is casting to now.</summary>
    internal static List<string> Connected()
    {
        var names = new List<string>();
        for (var attempt = 0; attempt < 3; attempt++)
        {
            uint pathCount;
            uint modeCount;
            if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, &pathCount, &modeCount) != 0)
            {
                return names;
            }
            var paths = new DISPLAYCONFIG_PATH_INFO[Math.Max(1, pathCount)];
            var modes = new DISPLAYCONFIG_MODE_INFO[Math.Max(1, modeCount)];
            int status;
            fixed (DISPLAYCONFIG_PATH_INFO* p = paths)
            fixed (DISPLAYCONFIG_MODE_INFO* m = modes)
            {
                status = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, &pathCount, p, &modeCount, m, null);
            }
            if (status == 122 /* ERROR_INSUFFICIENT_BUFFER */)
            {
                continue;
            }
            if (status != 0)
            {
                return names;
            }
            for (var i = 0; i < pathCount; i++)
            {
                if ((int)paths[i].targetInfo.outputTechnology != Miracast)
                {
                    continue;
                }
                var target = new DISPLAYCONFIG_TARGET_DEVICE_NAME();
                target.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
                target.header.size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME);
                target.header.adapterId = paths[i].targetInfo.adapterId;
                target.header.id = paths[i].targetInfo.id;
                if (DisplayConfigGetDeviceInfo(&target.header) != 0)
                {
                    continue;
                }
                var name = new string((char*)&target.monitorFriendlyDeviceName);
                if (name.Length > 0)
                {
                    names.Add(name);
                }
            }
            return names;
        }
        return names;
    }
}

/// <summary>Made-up displays: what the tests and the pictures use. It connects nothing and presses no key.</summary>
internal sealed class SampleCast : ICastSource
{
    public SampleCast(IEnumerable<CastDisplay>? displays = null, bool looking = false, string? problem = null)
    {
        Displays = displays is null
            ?
            [
                new("tv", "Living Room TV", true), // not-ui: a sample display's name
                new("projector", "Meeting Room Projector", false), // not-ui: a sample display's name
                new("stick", "Fire TV Stick", false), // not-ui: a sample display's name
            ]
            : [.. displays];
        Looking = looking;
        Problem = problem;
    }

    public List<CastDisplay> Displays { get; }

    public bool Looking { get; }

    public string? Problem { get; }

    /// <summary>How often Windows' cast panel was asked for.</summary>
    public int Opened { get; private set; }

    /// <summary>How often it was asked to read again.</summary>
    public int Polled { get; private set; }

    public void Start(Changes changes)
    {
    }

    public CastState Read() => new(Looking, CastDisplays.Merge(Displays, [.. Displays.Where(display => display.Connected).Select(display => display.Name)]), Problem);

    public void OpenCast() => Opened++;

    public void Poll() => Polled++;

    public void Dispose()
    {
    }
}
