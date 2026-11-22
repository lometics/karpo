// What is open on each virtual desktop, on one display: the app icons the
// desktop buttons carry, and the window list the desktop flyout shows.
//
// Which desktop a window is on comes from IVirtualDesktopManager, the one
// documented virtual desktop interface. It answers per window rather than per
// desktop, so a single pass over the top-level windows serves every desktop at
// once. The shell cloaks what is on another desktop, and those windows count
// here; otherwise a window has to pass the taskbar's rules, the ones Apps
// applies, so a preview shows what the dock would list on that desktop.
//
// A read has to stay within a few milliseconds, and the dock reads often (on
// window events, desktop changes, reveals). The costly answers come from other
// processes: the desktop of a window and the app ID the shell groups it by,
// each about a tenth of a millisecond. So the cheap tests, the display among
// them, come first, and a window's app key is kept for as long as the window,
// its process and its declared app ID stay the same.

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>
/// One window of a desktop preview, in the order it is stacked.
/// <see cref="Key"/> is the key <see cref="Win.Key"/> would have, so the scene
/// can draw the app's icon; <see cref="Aumid"/> and <see cref="Exe"/> are what
/// it was made from, as <see cref="Win"/> has them, for resolving the app the
/// way the taskbar's tiles are. <see cref="Active"/> marks the foreground
/// window on the current desktop, and on any other desktop the topmost window
/// that isn't minimised, which is the one Windows activates on switching there.
/// <see cref="Minimized"/> is why a window can be on the desktop and nowhere on
/// its screen.
/// </summary>
/// <remarks>
/// Where on the display each window stood used to be here as well, for a small
/// map the flyout drew above its list. The user asked for that map to be
/// removed, so the places went with it; nothing reads them any more.
/// </remarks>
public readonly record struct PreviewWindow(
    string Key,
    bool Active,
    string Title = "",
    nint Hwnd = 0,
    bool Minimized = false,
    string? Aumid = null,
    string? Exe = null);

public sealed class DesktopPreview
{
    public Guid Desktop;

    /// <summary>Bottom to top, as they are stacked, minimised windows included.</summary>
    public List<PreviewWindow> Windows = [];
}

/// <summary>
/// What a desktop's row in the switcher's list shows of the apps open on it (Switchers.Desktops).
/// Pure, for the dock and its tests.
/// </summary>
public static class DesktopApps
{
    /// <summary>
    /// One window per app, the app's topmost, apps in the order of those
    /// windows from the top of the stack down: what was used last comes first.
    /// <paramref name="bottomToTop"/> is a <see cref="DesktopPreview"/>'s list.
    /// A window without an app key has no app to show and is left out.
    /// </summary>
    public static List<PreviewWindow> Distinct(IReadOnlyList<PreviewWindow> bottomToTop)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var apps = new List<PreviewWindow>();
        for (var index = bottomToTop.Count - 1; index >= 0; index--)
        {
            var window = bottomToTop[index];
            if (!string.IsNullOrEmpty(window.Key) && seen.Add(window.Key))
            {
                apps.Add(window);
            }
        }
        return apps;
    }

    /// <summary>The first <paramref name="shown"/> of <paramref name="apps"/>, and how many more there are.</summary>
    public static (IReadOnlyList<T> Icons, int More) Split<T>(IReadOnlyList<T> apps, int shown)
    {
        shown = Math.Max(shown, 0);
        return apps.Count <= shown ? (apps, 0) : (apps.Take(shown).ToArray(), apps.Count - shown);
    }

}

public static unsafe class DesktopPreviews
{
    // The same hosts Apps leaves out; keep the two lists alike.
    private static readonly string[] ShellExes =
    [
        "searchhost.exe",
        "searchapp.exe",
        "searchui.exe",
        "startmenuexperiencehost.exe",
        "shellexperiencehost.exe",
        "textinputhost.exe",
        "peopleexperiencehost.exe",
        "lockapp.exe",
        "widgets.exe",
        "widgetboard.exe",
        "systemsettingsbroker.exe",
        "runtimebroker.exe",
        "dwm.exe",
    ];

    private static readonly string[] ShellClasses =
    [
        "windows.ui.core.corewindow",
        "xaml_windowedpopupclass",
        "multitaskingviewframe",
        "foregroundstaging",
        "windows.internal.shell.tabproxywindow",
        "progman",
        "workerw",
        "shell_traywnd",
        "shell_secondarytraywnd",
        "notifyiconoverflowwindow",
    ];

    // Kept per thread, as COM objects of an apartment must be. The resolver
    // lives as long as Apps keeps its own; the desktop manager is made again
    // when a call says Explorer went away.
    [ThreadStatic]
    private static IVirtualDesktopManager* manager;

    [ThreadStatic]
    private static Resolver? resolver;

    // A window's app key; null Key when the taskbar's rules leave it out.
    [ThreadStatic]
    private static Dictionary<HWND, Known>? keys;

    /// <summary>A window's app, worked out once: its key, and the AUMID and exe it came from.</summary>
    private readonly record struct Known(uint Pid, string? ExplicitAumid, string? Key, string? Aumid = null, string? Exe = null);

    private readonly record struct Candidate(uint Pid, string Title, bool Cloaked, bool Topmost);

    private readonly record struct Placed(HWND Hwnd, Known App, string Title, bool Cloaked, bool Topmost, bool Minimized);

    /// <summary>
    /// The windows on each of <paramref name="desktops"/> that stand on the
    /// display holding <paramref name="monitor"/> (its bounds or its work area,
    /// in screen pixels). One preview per id, in the same order. A window pinned
    /// to every desktop has no desktop of its own among the ids and is left out.
    /// </summary>
    public static List<DesktopPreview> Read(RECT monitor, IReadOnlyList<Guid> desktops)
    {
        var previews = new List<DesktopPreview>(desktops.Count);
        var slots = new Dictionary<Guid, List<Placed>>();
        foreach (var id in desktops)
        {
            previews.Add(new DesktopPreview { Desktop = id });
            slots.TryAdd(id, []);
        }
        float width = monitor.right - monitor.left;
        float height = monitor.bottom - monitor.top;
        if (previews.Count == 0 || width <= 0 || height <= 0 || Manager() == null)
        {
            return previews;
        }
        resolver ??= new Resolver();
        keys ??= [];

        var display = MonitorFromRect(&monitor, (uint)MONITOR.MONITOR_DEFAULTTONEAREST);
        var own = GetCurrentProcessId();
        var exes = new Dictionary<uint, string?>();
        var all = TopLevel();
        foreach (var hwnd in all)
        {
            if (Listed(hwnd, own) is not { } found)
            {
                continue;
            }
            var minimized = IsIconic(hwnd);
            if (minimized)
            {
                // A minimised window has no place on screen any more;
                // MonitorFromWindow still answers with the display it would
                // be restored on.
                if (MonitorFromWindow(hwnd, (uint)MONITOR.MONITOR_DEFAULTTONEAREST) != display)
                {
                    continue;
                }
            }
            else
            {
                // The frame as drawn: the window rectangle also holds the
                // invisible resize borders, which would make a window that only
                // grazes this display look as though it belonged to it.
                RECT frame;
                RECT overlap;
                if (DwmGetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_EXTENDED_FRAME_BOUNDS, &frame, (uint)sizeof(RECT)).FAILED
                    && !GetWindowRect(hwnd, &frame))
                {
                    continue;
                }
                // The display a window belongs to is the one Windows would
                // pick for it: the one it overlaps most.
                if (MonitorFromRect(&frame, (uint)MONITOR.MONITOR_DEFAULTTONEAREST) != display
                    || !IntersectRect(&overlap, &frame, &monitor))
                {
                    continue;
                }
            }
            if (App(hwnd, found.Pid, exes) is not { Key: not null } app)
            {
                continue;
            }
            if (DesktopOf(hwnd) is not { } id || !slots.TryGetValue(id, out var slot))
            {
                continue;
            }
            slot.Add(new Placed(hwnd, app, found.Title, found.Cloaked, found.Topmost, minimized));
        }
        Prune(all);

        var foreground = GetForegroundWindow();
        foreach (var preview in previews)
        {
            // Top to bottom here, as EnumWindows gave them.
            var slot = slots[preview.Desktop];
            int active;
            if (slot.Exists(placed => !placed.Cloaked))
            {
                // The current desktop: only what really has the focus.
                active = slot.FindIndex(placed => placed.Hwnd == foreground && !placed.Minimized);
            }
            else
            {
                // An always-on-top window stays above whatever was last used.
                active = slot.FindIndex(placed => !placed.Topmost && !placed.Minimized);
                if (active < 0)
                {
                    active = slot.FindIndex(placed => !placed.Minimized);
                }
            }
            for (var index = slot.Count - 1; index >= 0; index--)
            {
                var placed = slot[index];
                var app = placed.App;
                preview.Windows.Add(new PreviewWindow(
                    app.Key!,
                    !placed.Minimized && index == active,
                    placed.Title,
                    placed.Hwnd,
                    Minimized: placed.Minimized,
                    Aumid: app.Aumid,
                    Exe: app.Exe));
            }
        }
        return previews;
    }

    private static IVirtualDesktopManager* Manager()
    {
        if (manager == null)
        {
            var classId = CLSID.CLSID_VirtualDesktopManager;
            IVirtualDesktopManager* made = null;
            if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IVirtualDesktopManager>(), (void**)&made).SUCCEEDED)
            {
                manager = made;
            }
        }
        return manager;
    }

    /// <summary>The desktop a window is on; null when Windows can't say.</summary>
    private static Guid? DesktopOf(HWND hwnd)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var current = Manager();
            if (current == null)
            {
                return null;
            }
            Guid id;
            var asked = current->GetWindowDesktopId(hwnd, &id);
            if (asked.SUCCEEDED)
            {
                return id == Guid.Empty ? null : id;
            }
            if (!Disconnected(asked))
            {
                return null;
            }
            // Explorer restarted under the manager: make a new one and ask again.
            current->Release();
            manager = null;
        }
        return null;
    }

    /// <summary>The errors of a COM object whose server went away (RPC_E_*, RPC_S_*, CO_E_OBJNOTCONNECTED).</summary>
    private static bool Disconnected(HRESULT result)
    {
        var code = (uint)result.Value;
        return (code & 0xFFFF0000) == 0x80010000
            || code is >= 0x800706A4 and <= 0x80070727
            || code == 0x800401FD;
    }

    /// <summary>Every top-level window, topmost first.</summary>
    private static List<HWND> TopLevel()
    {
        var all = new List<HWND>();
        var handle = GCHandle.Alloc(all);
        try
        {
            EnumWindows(&Collect, GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }
        return all;
    }

    [UnmanagedCallersOnly]
    private static BOOL Collect(HWND hwnd, LPARAM found)
    {
        // Nothing thrown may unwind into user32: the list ends where it broke.
        try
        {
            ((List<HWND>)GCHandle.FromIntPtr(found).Target!).Add(hwnd);
            return BOOL.TRUE;
        }
        catch (Exception error)
        {
            Troubles.Report("Desktop previews: listing windows", error);
            return BOOL.FALSE;
        }
    }

    /// <summary>
    /// The first, cheap half of Apps' test for a taskbar button, except that a
    /// window the shell cloaked passes: that is how a window on another desktop
    /// looks. The half that asks other processes is <see cref="AppKey"/>.
    /// </summary>
    private static Candidate? Listed(HWND hwnd, uint own)
    {
        if (!IsWindowVisible(hwnd))
        {
            return null;
        }
        if (!Apps.Listed(hwnd))
        {
            return null;
        }
        var extended = (uint)GetWindowLongPtrW(hwnd, GWL.GWL_EXSTYLE);
        uint cloaked = 0;
        if (DwmGetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).FAILED)
        {
            cloaked = 0;
        }
        // Anything cloaked otherwise, a suspended store app most often, is not
        // on any desktop to be seen.
        if (cloaked != 0 && cloaked != (uint)DWM.DWM_CLOAKED_SHELL)
        {
            return null;
        }
        var text = stackalloc char[512];
        var length = GetWindowTextW(hwnd, text, 512);
        var title = new string(text, 0, length);
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }
        uint pid = 0;
        GetWindowThreadProcessId(hwnd, &pid);
        if (pid == 0 || pid == own)
        {
            return null;
        }
        var name = stackalloc char[128];
        length = GetClassNameW(hwnd, name, 128);
        if (ShellClasses.Contains(new string(name, 0, length).ToLowerInvariant()))
        {
            return null;
        }
        return new Candidate(pid, title, cloaked != 0, (extended & (uint)WS.WS_EX_TOPMOST) != 0);
    }

    /// <summary>
    /// The app the window's tile would stand for: its key, null when the
    /// taskbar leaves the window out (a shell host, a store app's empty
    /// frame), and the AUMID and exe the key was made from. Worked out once per
    /// window; the declared app ID is read every time, since an app may set it
    /// after its window is shown.
    /// </summary>
    private static Known App(HWND hwnd, uint pid, Dictionary<uint, string?> exes)
    {
        var explicitAumid = WindowAumid(hwnd);
        if (keys!.TryGetValue(hwnd, out var known) && known.Pid == pid && known.ExplicitAumid == explicitAumid)
        {
            return known;
        }
        if (!exes.TryGetValue(pid, out var exe))
        {
            exe = Apps.ImagePath(pid);
            exes[pid] = exe;
        }
        string? key = null;
        string? aumid = null;
        if (exe is not null)
        {
            var file = exe[(exe.LastIndexOf('\\') + 1)..].ToLowerInvariant();
            // A store app's frame without an AUMID is an empty husk.
            if (!ShellExes.Contains(file) && !(file == "applicationframehost.exe" && explicitAumid is null))
            {
                aumid = resolver!.Window(hwnd) ?? explicitAumid;
                key = aumid is null ? Items.ExeKey(exe) : Items.AumidKey(aumid);
            }
        }
        known = new Known(pid, explicitAumid, key, aumid, exe);
        keys[hwnd] = known;
        return known;
    }

    /// <summary>Forgets the windows that are gone.</summary>
    private static void Prune(List<HWND> all)
    {
        if (keys is not { Count: > 0 })
        {
            return;
        }
        var alive = new HashSet<HWND>(all);
        List<HWND>? gone = null;
        foreach (var hwnd in keys.Keys)
        {
            if (!alive.Contains(hwnd))
            {
                (gone ??= []).Add(hwnd);
            }
        }
        if (gone is not null)
        {
            foreach (var hwnd in gone)
            {
                keys.Remove(hwnd);
            }
        }
    }

    /// <summary>The AUMID an app set on this window explicitly, if it did (as Apps reads it).</summary>
    private static string? WindowAumid(HWND hwnd)
    {
        IPropertyStore* store = null;
        if (SHGetPropertyStoreForWindow(hwnd, __uuidof<IPropertyStore>(), (void**)&store).FAILED || store == null)
        {
            return null;
        }
        try
        {
            PROPERTYKEY key = PKEY.PKEY_AppUserModel_ID;
            PROPVARIANT value = default;
            if (store->GetValue(&key, &value).FAILED)
            {
                return null;
            }
            char* text = null;
            HRESULT converted = PropVariantToStringAlloc(&value, &text);
            PropVariantClear(&value);
            if (converted.FAILED)
            {
                return null;
            }
            var aumid = ComUtil.FromTaskMem(text);
            return string.IsNullOrEmpty(aumid) ? null : aumid;
        }
        finally
        {
            store->Release();
        }
    }
}
