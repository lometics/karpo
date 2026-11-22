// Open windows as the taskbar would list them, kept current from shell hook
// messages rather than by polling.
//
// The filter rules and the lists of shell hosts follow Aero Dock's
// `running.rs` (MIT), which follows the taskbar's own behaviour.

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

public sealed class Win
{
    public HWND Hwnd;

    /// <summary>The AUMID key when the window has one, otherwise the exe key.</summary>
    public string Key = "";

    public string? Aumid;
    public string Exe = "";
    public string Title = "";

    /// <summary>signals: flashing for attention, from the first flash until it stops or the window is activated (<see cref="AttentionFlash"/>).</summary>
    public bool Attention;

    /// <summary>signals: when the attention began (Environment.TickCount64), which the button's blinking counts from.</summary>
    public long AttentionAt;

    /// <summary>signals: when a redraw said the flashing may have stopped (0 for none): it has, unless a flash comes within <see cref="AttentionFlash.SettleMs"/>.</summary>
    public long AttentionOffAt;

    /// <summary>desktops: on another virtual desktop — listed only where a layout shows every desktop's windows (§362).</summary>
    public bool Elsewhere;

    public IEnumerable<string> Keys()
    {
        yield return Key;
        yield return Items.ExeKey(Exe);
    }
}

/// <summary>
/// Ordered like the Rust enum it ports: a larger value includes the smaller
/// ones, so two changes combine by taking the larger.
/// </summary>
public enum Change
{
    None = 0,

    /// <summary>Titles, activity or attention: redraw indicators, keep the layout.</summary>
    States = 1,

    /// <summary>A window came or went: the tiles may change.</summary>
    Set = 2,
}

public sealed unsafe class Apps : IDisposable
{
    /// <summary><c>HSHELL_REDRAW | HSHELL_HIGHBIT</c>: the window is flashing for attention.</summary>
    public const uint HSHELL_FLASH = 0x8006;

    /// <summary>
    /// <c>HSHELL_WINDOWACTIVATED | HSHELL_HIGHBIT</c>: activated while a
    /// full-screen window was up.
    /// </summary>
    public const uint HSHELL_RUDEAPPACTIVATED = 0x8004;

    // Not in TerraFX: the C:\ path form, as opposed to PROCESS_NAME_NATIVE's
    // \Device\HarddiskVolumeN form.
    private const uint PROCESS_NAME_WIN32 = 0;

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

    /// <summary>Whether a window of class <paramref name="className"/> is the shell's: the desktop, a taskbar, Start, Search.</summary>
    internal static bool IsShellClass(string className) => ShellClasses.Contains(className.ToLowerInvariant());

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

    /// <summary>
    /// In the order they were first seen, which keeps unpinned tiles still.
    /// Always the same list instance: a scan refills it rather than replacing it.
    /// </summary>
    public List<Win> All { get; } = [];

    // Most recently active first.
    private readonly List<HWND> recent = [];
    private readonly uint own = GetCurrentProcessId();
    private readonly Dictionary<uint, string> exes = [];
    private readonly Resolver resolver = new();
    private IVirtualDesktopManager* desktopManager;
    private readonly Dictionary<HWND, (uint Pid, string? Explicit, Win Window)> catalog = [];

    public Apps()
    {
        var classId = new Guid("AA509086-5CA9-4C25-8F95-589D3C07B48A");
        IVirtualDesktopManager* manager = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IVirtualDesktopManager>(), (void**)&manager).SUCCEEDED) desktopManager = manager;
        Scan();
    }

    /// <summary>DWM_CLOAKED_SHELL: the shell cloaked the window — another desktop's, or a store app's put away.</summary>
    private const uint DWM_CLOAKED_SHELL = 2;

    /// <summary>
    /// desktops: windows on other virtual desktops are listed too (<see cref="Win.Elsewhere"/>): a
    /// layout shows every desktop's windows (§362). Set before a <see cref="Scan"/>.
    /// </summary>
    public bool AllDesktops { get; set; }

    /// <summary>desktops: whether the virtual desktops say <paramref name="hwnd"/> stands on one that is not this.</summary>
    private bool OnAnotherDesktop(HWND hwnd)
    {
        if (desktopManager == null)
        {
            return false;
        }
        BOOL on;
        Guid id;
        return desktopManager->IsWindowOnCurrentVirtualDesktop(hwnd, &on).SUCCEEDED && !on
            && desktopManager->GetWindowDesktopId(hwnd, &id).SUCCEEDED && id != Guid.Empty;
    }

    /// <summary>Releases the shell's application resolver.</summary>
    public void Dispose()
    {
        resolver.Dispose();
        if (desktopManager != null) { desktopManager->Release(); desktopManager = null; }
    }

    /// <summary>
    /// Everything again, keeping known windows in their places: after a
    /// desktop switch, when cloaking changed which windows count.
    /// </summary>
    public Change Scan(Guid? desktop = null)
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
        exes.Clear();
        // desktops: which were on another desktop, before probing says it again of the same windows.
        var before = All.Select(win => (win.Hwnd, win.Elsewhere)).ToList();
        foreach (var gone in catalog.Keys.Where(hwnd => !all.Contains(hwnd)).ToArray()) catalog.Remove(gone);
        var probed = new List<Win>();
        foreach (var hwnd in all)
        {
            if (Probe(hwnd, desktop) is { } win)
            {
                probed.Add(win);
            }
        }
        var seen = before.Select(win => win.Hwnd).ToList();
        // OrderBy, not List.Sort: the sort has to be stable, or windows new to
        // the list would lose the z-order they arrived in among themselves.
        var found = probed.OrderBy(win => Position(seen, win.Hwnd)).ToList();
        foreach (var win in found)
        {
            if (All.Find(known => known.Hwnd == win.Hwnd) is { } old)
            {
                (win.Attention, win.AttentionAt, win.AttentionOffAt) = (old.Attention, old.AttentionAt, old.AttentionOffAt);
            }
        }
        var after = found.Select(win => win.Hwnd).ToList();
        All.Clear();
        All.AddRange(found);
        recent.RemoveAll(hwnd => !after.Contains(hwnd));
        if (Find(GetForegroundWindow()) is int front)
        {
            Touch(All[front].Hwnd);
        }
        return before.SequenceEqual(found.Select(win => (win.Hwnd, win.Elsewhere))) ? Change.States : Change.Set;
    }

    /// <summary>Applies one shell hook message.</summary>
    public Change Shell(uint code, HWND hwnd)
    {
        var change = Prune();
        var known = Find(hwnd);
        Change step;
        // Each section names its own index: pattern variables declared in an
        // if condition share the whole switch block's scope.
        switch (code)
        {
            case (uint)HSHELL.HSHELL_WINDOWDESTROYED:
                if (known is int gone)
                {
                    All.RemoveAt(gone);
                    Forget(hwnd);
                    step = Change.Set;
                }
                else
                {
                    step = Change.None;
                }
                break;
            case (uint)HSHELL.HSHELL_WINDOWCREATED:
            case (uint)HSHELL.HSHELL_WINDOWREPLACED:
                step = Recheck(hwnd, known);
                break;
            case (uint)HSHELL.HSHELL_REDRAW:
                step = Recheck(hwnd, known);
                // signals: a redraw that changed nothing of the window's, while it flashes, is a flash
                // going off — or the flashing stopped (FLASHW_STOP), which says no more than that. It
                // has stopped unless the next flash comes in time (AttentionFlash).
                if (step == Change.None && Find(hwnd) is int redrawn && All[redrawn] is { Attention: true, AttentionOffAt: 0 } off)
                {
                    off.AttentionOffAt = Math.Max(1, Environment.TickCount64);
                }
                break;
            case (uint)HSHELL.HSHELL_WINDOWACTIVATED:
            case HSHELL_RUDEAPPACTIVATED:
                step = Recheck(hwnd, known);
                if (Find(hwnd) is int active)
                {
                    All[active].Attention = false;
                    All[active].AttentionOffAt = 0;
                    Touch(hwnd);
                }
                step = Max(step, Change.States);
                break;
            case HSHELL_FLASH:
                step = Recheck(hwnd, known);
                if (Find(hwnd) is int flashing)
                {
                    var win = All[flashing];
                    win.AttentionOffAt = 0;
                    if (!win.Attention)
                    {
                        win.Attention = true;
                        win.AttentionAt = Environment.TickCount64;
                        step = Max(step, Change.States);
                    }
                }
                break;
            default:
                step = Change.None;
                break;
        }
        return Max(change, step);
    }

    /// <summary>
    /// signals: the windows whose flashing stopped by <paramref name="now"/> — a redraw said it may have,
    /// and no flash came since for <see cref="AttentionFlash.SettleMs"/> — ask for attention no more.
    /// </summary>
    public Change SettleAttention(long now)
    {
        var change = Change.None;
        foreach (var win in All)
        {
            if (win.Attention && AttentionFlash.Stopped(win.AttentionOffAt, now))
            {
                win.Attention = false;
                win.AttentionOffAt = 0;
                change = Change.States;
            }
        }
        return change;
    }

    public int? Find(HWND hwnd)
    {
        int index = All.FindIndex(win => win.Hwnd == hwnd);
        return index < 0 ? null : index;
    }

    public Win? Get(HWND hwnd) => Find(hwnd) is int index ? All[index] : null;

    /// <summary>
    /// How recently <paramref name="hwnd"/> was active: lower is more recent,
    /// <see cref="int.MaxValue"/> for a window never seen active.
    /// </summary>
    public int Recency(HWND hwnd) => Position(recent, hwnd);

    /// <summary>
    /// pins: the window that was in front last, of the apps the taskbar lists. What a click on an app
    /// is about while a popup of Karpo's holds the focus, which is no app's window (see Dock.Front).
    /// </summary>
    public HWND MostRecent => recent.Count > 0 ? recent[0] : HWND.NULL;

    private void Touch(HWND hwnd)
    {
        Forget(hwnd);
        recent.Insert(0, hwnd);
    }

    private void Forget(HWND hwnd)
    {
        recent.RemoveAll(h => h == hwnd);
    }

    /// <summary>
    /// A title change can also mean a window just became one the taskbar
    /// would show, or stopped being one.
    /// </summary>
    private Change Recheck(HWND hwnd, int? known)
    {
        var win = Probe(hwnd);
        if (known is not int index)
        {
            if (win is null)
            {
                return Change.None;
            }
            All.Add(win);
            return Change.Set;
        }
        if (win is null)
        {
            All.RemoveAt(index);
            Forget(hwnd);
            return Change.Set;
        }
        var old = All[index];
        if (old.Key != win.Key)
        {
            (win.Attention, win.AttentionAt, win.AttentionOffAt) = (old.Attention, old.AttentionAt, old.AttentionOffAt);
            All[index] = win;
            return Change.Set;
        }
        if (old.Title != win.Title)
        {
            old.Title = win.Title;
            return Change.States;
        }
        return Change.None;
    }

    /// <summary>
    /// Drops windows that are gone without a destroy message having arrived,
    /// which happens when one was missed during a modal loop.
    /// </summary>
    private Change Prune()
    {
        if (All.RemoveAll(win => !IsWindow(win.Hwnd)) == 0)
        {
            return Change.None;
        }
        recent.RemoveAll(hwnd => !All.Exists(win => win.Hwnd == hwnd));
        return Change.Set;
    }

    /// <summary>
    /// Whether Explorer's taskbar gives a visible top-level window a button, by its extended style,
    /// whether it has an owner and whether its app took the button away (§204, measured against
    /// Explorer's own list): a tool window never, WS_EX_APPWINDOW or not (Raycast's launcher);
    /// otherwise an app window always, owned or WS_EX_NOACTIVATE; otherwise one neither owned
    /// nor WS_EX_NOACTIVATE. <paramref name="deleted"/>: ITaskbarList::DeleteTab, until AddTab.
    /// </summary>
    internal static bool Listed(uint extended, bool owned, bool deleted)
    {
        if (deleted || (extended & (uint)WS.WS_EX_TOOLWINDOW) != 0)
        {
            return false;
        }
        return (extended & (uint)WS.WS_EX_APPWINDOW) != 0
            || (!owned && (extended & (uint)WS.WS_EX_NOACTIVATE) == 0);
    }

    /// <summary><see cref="Listed(uint, bool, bool)"/> read off <paramref name="hwnd"/>, its visibility aside.</summary>
    internal static bool Listed(HWND hwnd)
    {
        uint extended = (uint)GetWindowLongPtrW(hwnd, GWL.GWL_EXSTYLE);
        bool owned = GetWindow(hwnd, GW_OWNER) != HWND.NULL;
        return Listed(extended, owned, Deleted(hwnd));
    }

    /// <summary>
    /// ITaskbarList::DeleteTab was called on <paramref name="hwnd"/>: Explorer marks the window with
    /// this property and takes it off again on AddTab, the only trace the call leaves.
    /// </summary>
    private static bool Deleted(HWND hwnd)
    {
        fixed (char* name = "ITaskList_Deleted")
        {
            return GetPropW(hwnd, name) != HANDLE.NULL;
        }
    }

    /// <summary>Not null when the taskbar would show a button for <paramref name="hwnd"/>.</summary>
    private Win? Probe(HWND hwnd, Guid? desktop = null)
    {
        // stage: none of the person's windows on a recording's bar (Stage.cs).
        if (Stage.On)
        {
            return null;
        }
        if (!IsWindowVisible(hwnd))
        {
            return null;
        }
        if (!Listed(hwnd))
        {
            return null;
        }
        var matchesDesktop = false;
        var elsewhere = false;
        if (desktop is { } requested && desktopManager != null)
        {
            Guid assigned;
            if (desktopManager->GetWindowDesktopId(hwnd, &assigned).SUCCEEDED)
            {
                elsewhere = assigned != Guid.Empty && assigned != requested;
                matchesDesktop = assigned == requested;
            }
        }
        uint cloaked = 0;
        HRESULT read = DwmGetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        // desktops: a window the shell cloaked may be on another desktop, which only a layout showing
        // every desktop's windows lists (§362); asked only of those, as it takes the shell's word.
        if (desktop is null && read.SUCCEEDED && (cloaked & DWM_CLOAKED_SHELL) != 0)
        {
            elsewhere = OnAnotherDesktop(hwnd);
        }
        if (elsewhere && !AllDesktops)
        {
            return null;
        }
        // Cloaked covers suspended store apps and windows on other desktops: the shell's cloak alone
        // is allowed on a window of this desktop (a switch under way) or of another one listed.
        if (read.SUCCEEDED && (matchesDesktop || elsewhere ? (cloaked & ~DWM_CLOAKED_SHELL) != 0 : cloaked != 0))
        {
            return null;
        }
        char* text = stackalloc char[512];
        int length = GetWindowTextW(hwnd, text, 512);
        string title = new string(text, 0, length);
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
        char* name = stackalloc char[128];
        length = GetClassNameW(hwnd, name, 128);
        string className = new string(name, 0, length).ToLowerInvariant();
        if (ShellClasses.Contains(className))
        {
            return null;
        }
        if (ExePath(pid) is not { } exe)
        {
            return null;
        }
        string file = exe[(exe.LastIndexOf('\\') + 1)..].ToLowerInvariant();
        if (ShellExes.Contains(file))
        {
            return null;
        }
        string? explicitAumid = WindowAumid(hwnd);
        if (catalog.TryGetValue(hwnd, out var cached) && cached.Pid == pid && cached.Explicit == explicitAumid)
        {
            cached.Window.Title = title;
            cached.Window.Elsewhere = elsewhere;
            return cached.Window;
        }
        // A store app's frame without an AUMID is an empty husk.
        if (file == "applicationframehost.exe" && explicitAumid is null)
        {
            return null;
        }
        // What the taskbar groups by, derived when the app set nothing.
        string? aumid = resolver.Window(hwnd) ?? explicitAumid;
        string key = aumid is null ? Items.ExeKey(exe) : Items.AumidKey(aumid);
        var found = new Win { Hwnd = hwnd, Key = key, Aumid = aumid, Exe = exe, Title = title, Attention = false, Elsewhere = elsewhere };
        catalog[hwnd] = (pid, explicitAumid, found); return found;
    }

    /// <summary>Cached per process id; every scan starts the cache over.</summary>
    private string? ExePath(uint pid)
    {
        if (exes.TryGetValue(pid, out var cached))
        {
            return cached;
        }
        if (ImagePath(pid) is not { } path)
        {
            return null;
        }
        exes[pid] = path;
        return path;
    }

    /// <summary>The executable of process <paramref name="pid"/>; null when the process can't be opened.</summary>
    internal static string? ImagePath(uint pid)
    {
        HANDLE process = OpenProcess((uint)PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, BOOL.FALSE, pid);
        if (process == HANDLE.NULL)
        {
            return null;
        }
        char* buffer = stackalloc char[1024];
        uint size = 1024;
        BOOL read = QueryFullProcessImageNameW(process, PROCESS_NAME_WIN32, buffer, &size);
        CloseHandle(process);
        return read ? new string(buffer, 0, (int)size) : null;
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
            Troubles.Report("Apps: listing windows", error);
            return BOOL.FALSE;
        }
    }

    /// <summary>The AUMID an app set on this window explicitly, if it did.</summary>
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
            string? aumid = ComUtil.FromTaskMem(text);
            return string.IsNullOrEmpty(aumid) ? null : aumid;
        }
        finally
        {
            store->Release();
        }
    }

    /// <summary>
    /// §619: Brings <paramref name="hwnd"/> forward as Windows' taskbar switches to a window
    /// (SwitchToThisWindow): restored where it is minimized, asked of its app as its own button asks it
    /// (<see cref="Restore"/>, SC_RESTORE: a window maximized before comes back maximized), and its last
    /// active popup activated where that is the one with the keys — a modal dialog over a disabled owner
    /// (<see cref="WindowOwners.ActivationTarget(HWND)"/>). The click on the dock gives this process
    /// the right to do that; when Windows still refuses, a key that means nothing, pressed and let go,
    /// gives this process the last input and lets it through. It used to be Alt, the long-known way,
    /// which put the window in front into its menu bar's keyboard mode (a classic menu, Firefox's).
    /// </summary>
    public static void Activate(HWND hwnd) => SwitchTo(hwnd, nudge: true);

    /// <summary>
    /// §619: <see cref="Activate"/>, the key that gets the last input pressed only with
    /// <paramref name="nudge"/> (tests switch on a hidden desktop, where nothing may be typed). Whether
    /// Windows took the window to the front.
    /// </summary>
    internal static bool SwitchTo(HWND hwnd, bool nudge)
    {
        // A modal dialog over a minimized owner is hidden with it, and the restore activates the owner: it
        // is shown and activated again once the restore is done (queued to the same thread after it).
        var modal = HWND.NULL;
        if (IsIconic(hwnd))
        {
            modal = WindowOwners.ModalOf(hwnd);
            Restore(hwnd);
        }
        uint pid = 0;
        _ = GetWindowThreadProcessId(hwnd, &pid);
        var target = pid == GetCurrentProcessId() ? hwnd : WindowOwners.ActivationTarget(hwnd);
        var taken = SetForegroundWindow(target);
        if (!taken && nudge)
        {
            INPUT* taps = stackalloc INPUT[2];
            taps[0] = Tap(0);
            taps[1] = Tap(KEYEVENTF_KEYUP);
            _ = SendInput(2, taps, sizeof(INPUT));
            taken = SetForegroundWindow(target);
        }
        if (modal != HWND.NULL)
        {
            Surfaces.ActivateOther(modal);
        }
        return taken;

        // Unassigned (TaskbarKeys' mask key), and marked as Karpo's own, so its keyboard hook lets it by.
        static INPUT Tap(uint flags)
        {
            INPUT input = default;
            input.type = INPUT.INPUT_KEYBOARD;
            input.ki.wVk = TaskbarKeys.MaskKey;
            input.ki.dwFlags = flags;
            input.ki.dwExtraInfo = TaskbarKeys.Ours;
            return input;
        }
    }

    /// <summary>
    /// <paramref name="hwnd"/> minimized as Windows' taskbar does it — the click on the button of the
    /// window in front, Win+N, a window menu's Minimize, "Minimize all windows": never a window without
    /// a minimize button (<see cref="WindowMenus.Minimizes"/>), and asked of its app as its own
    /// button asks it (WM_SYSCOMMAND, SC_MINIMIZE), its own minimizing (an animation, a tray app's
    /// hiding) with it — an app run as administrator too, which Windows lets the ask reach (§662).
    /// Where the app can't answer (hung, or a modal dialog's owner) it is put away without asking,
    /// without waiting on its app either (<see cref="Command"/>).
    /// </summary>
    public static void Minimize(HWND hwnd)
    {
        if (!IsWindow(hwnd) || !WindowMenus.Minimizes(WindowMenus.ShapeOf(hwnd)))
        {
            return;
        }
        Command(hwnd, WindowMenus.MinimizeCommand);
    }

    /// <summary>
    /// §619: a minimized <paramref name="hwnd"/> restored as Windows' taskbar restores it — asked of its
    /// app (WM_SYSCOMMAND, SC_RESTORE), its own restoring and the system's animation with it, back to
    /// where it was (maximized, if it was) —, or without asking where the ask can't reach it or would
    /// wait (<see cref="Command"/>). The one restore of a click, a card, the keys and "Restore all windows".
    /// </summary>
    public static void Restore(HWND hwnd)
    {
        if (IsWindow(hwnd) && IsIconic(hwnd))
        {
            _ = Command(hwnd, WindowMenus.RestoreCommand);
        }
    }

    /// <summary>
    /// §619: whether a system command is asked of a window (posted, its app doing it) rather than done
    /// without it: never of a hung one (<paramref name="hung"/>), which would do it only once it answers
    /// again — Windows' taskbar does that one without asking too (ShowWindowAsync), its ghost window
    /// minimizing —, nor of a disabled one (<paramref name="disabled"/>: the owner of a modal dialog,
    /// Save As over Notepad), whose DefWindowProc let a posted SC_RESTORE go by doing nothing (measured,
    /// AppToggleTests), a system command as SC_MINIMIZE is: the app would not come back from its button.
    /// §662: an app run above Karpo (Task Manager, as administrator) is asked as any other: UIPI lets
    /// a minimize, a restore and a close through to it, and refuses it everything done without asking.
    /// </summary>
    internal static bool Asks(bool hung, bool disabled) => !hung && !disabled;

    /// <summary>
    /// System command <paramref name="command"/> asked of <paramref name="hwnd"/> (WM_SYSCOMMAND), its app
    /// doing it, as Windows' taskbar asks. §662: the same for an app run above Karpo — Windows (UIPI)
    /// lets SC_MINIMIZE, SC_RESTORE and SC_CLOSE through to it while it refuses nearly everything else,
    /// ShowWindowAsync among it (measured from low integrity to medium, AboveKarpoTests). Where the
    /// app is hung or the window disabled (<see cref="Asks"/>), or Windows refused the post (a
    /// maximize to an app above Karpo, or a window gone), minimize, maximize and restore are done
    /// without it (Surfaces' calls for other apps' windows), and close goes through <see cref="Close"/>.
    /// False only where nothing could be done.
    /// </summary>
    internal static bool Command(HWND hwnd, uint command)
    {
        var close = (command & 0xFFF0u) == WindowMenus.CloseCommand;
        // A hung app's close is still asked (it gets it once it answers, as Windows' close button's does).
        if ((close || Asks(IsHungAppWindow(hwnd), !IsWindowEnabled(hwnd))) && PostMessageW(hwnd, WM.WM_SYSCOMMAND, command, 0))
        {
            return true;
        }
        switch (command & 0xFFF0u)
        {
            case WindowMenus.MinimizeCommand:
                Surfaces.MinimizeOther(hwnd);
                return true;
            case WindowMenus.MaximizeCommand:
                Surfaces.MaximizeOther(hwnd);
                return true;
            case WindowMenus.RestoreCommand:
                Surfaces.RestoreOther(hwnd);
                return true;
            case WindowMenus.CloseCommand:
                Close(hwnd);
                return true;
        }
        return false;
    }

    /// <summary>
    /// app clicks: Posted to the dock's host when a window above Karpo would not be closed (wParam the
    /// window): it says so at the window's button (Dock.CantClose).
    /// </summary>
    public const uint WM_KARPO_REFUSED = WM.WM_APP + 0x62;

    /// <summary>The dock's host, told of a close refused (<see cref="WM_KARPO_REFUSED"/>).</summary>
    internal static HWND Refusals;

    /// <summary>
    /// Asks, the way the close button does, so unsaved work gets its prompt. An app run as
    /// administrator never hears Karpo's WM_CLOSE (UIPI refuses the post, ERROR_ACCESS_DENIED), so it
    /// is asked through Windows' own EndTask instead, gently — WM_CLOSE, never ending it —, off the
    /// dock's thread, as EndTask may wait on the app; where Windows refuses that too, the dock is told
    /// (<see cref="WM_KARPO_REFUSED"/>) and says so.
    /// </summary>
    public static void Close(HWND hwnd)
    {
        if (PostMessageW(hwnd, WM.WM_CLOSE, default, default))
        {
            return;
        }
        var window = (nint)hwnd;
        var tell = Refusals;
        _ = ThreadPool.QueueUserWorkItem(_ =>
        {
            if (!EndTaskW((HWND)window, BOOL.FALSE, BOOL.FALSE) && IsWindow((HWND)window) && tell != HWND.NULL)
            {
                _ = PostMessageW(tell, WM_KARPO_REFUSED, (WPARAM)(nuint)window, 0);
            }
        });
    }

    /// <summary>
    /// user32's EndTask, which TerraFX leaves out: with fForce false, the window is asked to close
    /// (WM_CLOSE) by Windows itself, and nothing is ended. False where Windows refused.
    /// </summary>
    [DllImport("user32.dll", EntryPoint = "EndTask", ExactSpelling = true)]
    private static extern BOOL EndTaskW(HWND hwnd, BOOL shutDown, BOOL force);

    public static HWND Foreground() => GetForegroundWindow();

    /// <summary>
    /// The icon a window shows for itself, as the taskbar finds it when nothing
    /// else has one: its answer to WM_GETICON, then its class's icon. The window's
    /// process owns the handle; it is drawn, never destroyed. Asked with a short
    /// timeout and without handling other messages meanwhile, so a hung app costs
    /// the dock a moment and nothing re-enters it.
    /// </summary>
    internal static HICON WindowIcon(HWND hwnd, bool small)
    {
        int[] kinds = small ? [ICON_SMALL2, ICON_SMALL, ICON_BIG] : [ICON_BIG, ICON_SMALL2, ICON_SMALL];
        foreach (var kind in kinds)
        {
            nuint answer = 0;
            var sent = SendMessageTimeoutW(hwnd, WM.WM_GETICON, (nuint)kind, default, (uint)(SMTO_ABORTIFHUNG | SMTO_BLOCK), 100, &answer);
            if (sent.Value != 0 && answer != 0)
            {
                return (HICON)(nint)answer;
            }
        }
        var classIcon = GetClassLongPtrW(hwnd, small ? GCLP_HICONSM : GCLP_HICON);
        if (classIcon == 0)
        {
            classIcon = GetClassLongPtrW(hwnd, small ? GCLP_HICON : GCLP_HICONSM);
        }
        return (HICON)(nint)classIcon;
    }

    private static int Position(List<HWND> list, HWND hwnd)
    {
        int index = list.IndexOf(hwnd);
        return index < 0 ? int.MaxValue : index;
    }

    private static Change Max(Change a, Change b) => a > b ? a : b;
}
