// Virtual desktops, read from where Explorer keeps them in the registry, and
// switched, moved and removed through the shell's own interface where Karpo
// knows its shape (DesktopControl), else with the shortcuts a person would
// press.
//
// Windows has no public API to list or switch desktops. The COM interface
// Explorer itself uses changes between Windows releases, while these registry
// values and shortcuts have stayed put, so the list always comes from the
// registry, and the interface is only asked to act, on the builds it was
// checked for. The registry is watched with RegNotifyChangeKeyValue, so a
// switch or a new desktop wakes the dock without polling. Which desktop a
// window is on is the one thing the documented IVirtualDesktopManager
// answers, and Activate asks it.

using System.Runtime.InteropServices;
using System.Text;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
// `using static` also brings in the wingdi.h constant Windows.ERROR, which
// makes the plain name ambiguous; the alias wins over both.
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

public sealed unsafe class Desktops : IDisposable
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";

    // The Windows headers name no letter keys; their virtual-key codes are the
    // capital letters.
    private const ushort VK_D = 'D';
    private const ushort VK_N = 'N';

    private HKEY key;
    private HANDLE changed;
    private List<Guid> ids = [];
    private Guid? switchingTo;
    private long switchUntil;

    public List<string> Names { get; private set; } = [];

    public int Current { get; private set; }

    /// <summary>
    /// The desktops' ids, in the order <see cref="Names"/> has them; empty when
    /// the registry lists none. A refresh replaces the list, so one taken
    /// earlier stays as it was.
    /// </summary>
    public IReadOnlyList<Guid> Ids => ids;

    private Desktops(HKEY key, HANDLE changed)
    {
        this.key = key;
        this.changed = changed;
    }

    public static Desktops? Open()
    {
        HKEY key;
        fixed (char* path = KeyPath)
        {
            if (RegOpenKeyExW(HKEY.HKEY_CURRENT_USER, path, 0, (uint)(KEY.KEY_READ | KEY.KEY_NOTIFY), &key) != ERROR.ERROR_SUCCESS)
            {
                return null;
            }
        }
        var changed = CreateEventW(null, BOOL.FALSE, BOOL.FALSE, null);
        if (changed == HANDLE.NULL)
        {
            RegCloseKey(key);
            return null;
        }
        var desktops = new Desktops(key, changed);
        desktops.Refresh();
        return desktops;
    }

    /// <summary>Signalled once after anything under the key changes.</summary>
    public HANDLE Event => changed;

    /// <summary>
    /// Re-arms the watch, then reads, so nothing that changes in between is
    /// missed. True when the list, the names or the current desktop changed.
    /// For when <see cref="Event"/> was signalled.
    /// </summary>
    public bool Refresh()
    {
        _ = RegNotifyChangeKeyValue(
            key,
            BOOL.TRUE,
            (uint)(REG.REG_NOTIFY_CHANGE_NAME | REG.REG_NOTIFY_CHANGE_LAST_SET),
            changed,
            BOOL.TRUE);
        return Read();
    }

    /// <summary>
    /// Reads again without touching the watch; true when something changed.
    /// The watch alone is not enough to rely on: a dock once missed the
    /// notification for three new desktops and showed one button until it was
    /// restarted. A caller checks at moments of its own (after a switch or a
    /// new desktop it asked for, when the dock comes into view). Arming the
    /// watch here as well would add a registration per call while the last one
    /// is still waiting.
    /// </summary>
    public bool Check() => Read();

    private bool Read()
    {
        var (ids, current) = Order();
        if (switchingTo is { } desired)
        {
            var target = ids.IndexOf(desired);
            if (target == current || target < 0 || Environment.TickCount64 >= switchUntil) switchingTo = null;
            else current = target;
        }
        var names = new List<string>(ids.Count);
        for (var index = 0; index < ids.Count; index++)
        {
            names.Add(Name(key, ids[index]) ?? Switchers.Numbered(index));
        }
        var differs = !ids.SequenceEqual(this.ids) || !names.SequenceEqual(Names) || current != Current;
        this.ids = ids;
        Names = names;
        Current = current;
        return differs;
    }

    /// <summary>
    /// The desktops in order and the current one, as the registry has them
    /// right now.
    /// </summary>
    private (List<Guid> Ids, int Current) Order()
    {
        var raw = Binary(key, "VirtualDesktopIDs");
        var ids = new List<Guid>(raw.Length / 16);
        for (var at = 0; at + 16 <= raw.Length; at += 16)
        {
            ids.Add(Id(raw.AsSpan(at, 16)));
        }
        // Absent while there is only one desktop.
        var active = Binary(key, "CurrentVirtualDesktop");
        var current = active.Length == 16 ? Math.Max(ids.IndexOf(Id(active)), 0) : 0;
        return (ids, current);
    }

    public int Count => Math.Max(ids.Count, 1);

    /// <summary>
    /// Straight to desktop <paramref name="to"/> through the shell's interface,
    /// without passing the desktops between; where that can't be used (the log
    /// says why), one Ctrl+Win+arrow per step, as if typed. For a row of the
    /// switcher's list, whose popup closed leaving the foreground with Karpo
    /// (QuickFlyout's Chose): the foreground is handed on here, see <see cref="Go"/>.
    /// </summary>
    public void Switch(int to)
    {
        if (Go(Shell, ids, Current, Count, to) == SwitchWay.Straight)
        {
            // The registry follows in a moment; until then, and for at most a
            // second, the dock shows where it went.
            switchingTo = ids[to];
            switchUntil = Environment.TickCount64 + 1000;
            Current = to;
        }
    }

    /// <summary>How a switch went: not at all, straight through the shell, or by the keys.</summary>
    internal enum SwitchWay
    {
        None,
        Straight,
        Keys,
    }

    /// <summary>
    /// The shell's side of a switch. A test stands in for it, so no test moves the person's
    /// desktops or their foreground.
    /// </summary>
    internal interface ISwitchShell
    {
        /// <summary>Straight to the desktop through the shell's interface; false where that can't be used.</summary>
        bool Switch(Guid desktop);

        /// <summary>One Ctrl+Win+arrow per step from <paramref name="from"/> to <paramref name="to"/>.</summary>
        void Keys(int from, int to);

        /// <summary>
        /// The foreground to the window in front on <paramref name="desktop"/>, or to the desktop
        /// itself where nothing is open there; null for the desktop showing now.
        /// </summary>
        void Front(Guid? desktop);
    }

    /// <summary>What <see cref="Switch"/> acts through: Windows. A test puts a stand-in in its place.</summary>
    internal static ISwitchShell Shell = new WindowsShell();

    /// <summary>
    /// A switch, in the order it has to happen in.
    /// <para>
    /// The foreground decides it. A window on the desktop being left that is activated as the
    /// switch goes makes Windows switch straight back to it — which is what a popup closing does
    /// when it gives the foreground back to the window that had it, and was why choosing another
    /// desktop from the list did nothing. So the list closes leaving the foreground with Karpo,
    /// whose windows are on every desktop, and after a straight switch it goes to the window in
    /// front on the desktop arrived at, as Windows' own Ctrl+Win+arrow gives it. Left with Karpo,
    /// the popup's hiding would hand it to the next window down, which may be one of the
    /// desktop that was left.
    /// </para>
    /// <para>
    /// The keys go the other way: they are read after anything done here, the foreground is put
    /// back on the desktop they start from first, and Windows moves it along with them, as for a
    /// person typing them. The desktop already showing just gets its foreground back.
    /// </para>
    /// </summary>
    internal static SwitchWay Go(ISwitchShell shell, IReadOnlyList<Guid> ids, int current, int count, int to)
    {
        if (to < 0 || to >= count)
        {
            return SwitchWay.None;
        }
        Guid? from = current >= 0 && current < ids.Count ? ids[current] : null;
        if (to == current)
        {
            shell.Front(from);
            return SwitchWay.None;
        }
        if (to < ids.Count && shell.Switch(ids[to]))
        {
            shell.Front(ids[to]);
            return SwitchWay.Straight;
        }
        shell.Front(from);
        shell.Keys(current, to);
        return SwitchWay.Keys;
    }

    /// <summary>A top-level window as <see cref="FrontOf"/> weighs it.</summary>
    /// <param name="Eligible">Shown, not minimized, an app's window the taskbar would list, and not Karpo's.</param>
    /// <param name="Desktop">The desktop Windows has it on; null where it says none (on every desktop, or unknown).</param>
    /// <param name="Hidden">Cloaked by its app (a suspended Store app), which no switch uncovers.</param>
    /// <param name="Elsewhere">Cloaked by the shell: on another desktop, or on its way in during a switch.</param>
    internal readonly record struct Candidate(nint Window, bool Eligible, Guid? Desktop, bool Hidden, bool Elsewhere);

    /// <summary>
    /// The window to give the foreground to on <paramref name="desktop"/>: the first of
    /// <paramref name="zOrder"/>, top first, that is on it — the one last used there. Right after a
    /// switch the desktop's windows are still cloaked, so for a named desktop being on it is what
    /// counts; for the one showing (null), not being cloaked. 0 where there is none.
    /// </summary>
    internal static nint FrontOf(IEnumerable<Candidate> zOrder, Guid? desktop)
    {
        foreach (var window in zOrder)
        {
            if (!window.Eligible || window.Hidden)
            {
                continue;
            }
            if (desktop is { } on ? window.Desktop == on : !window.Elsewhere)
            {
                return window.Window;
            }
        }
        return 0;
    }

    /// <summary>The shell's side of a switch, on Windows.</summary>
    private sealed class WindowsShell : ISwitchShell
    {
        public bool Switch(Guid desktop) => DesktopControl.Switch(desktop);

        public void Keys(int from, int to) => Send(Steps(from, to));

        public void Front(Guid? desktop)
        {
            var window = (HWND)FrontOf(ZOrder(), desktop);
            if (window == HWND.NULL)
            {
                // An empty desktop: the desktop itself, which is on every one, as Windows leaves it.
                window = GetShellWindow();
            }
            if (window != HWND.NULL)
            {
                Apps.Activate(window);
            }
        }

        /// <summary>The top-level windows, top first, as <see cref="FrontOf"/> weighs them.</summary>
        private static IEnumerable<Candidate> ZOrder()
        {
            var classId = CLSID.CLSID_VirtualDesktopManager;
            IVirtualDesktopManager* manager = null;
            _ = CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IVirtualDesktopManager>(), (void**)&manager);
            var own = GetCurrentProcessId();
            var found = new List<Candidate>();
            try
            {
                // GetWindow rather than EnumWindows: no callback, and the order is the z-order. A
                // window closing under the walk ends it early, which only means less to choose from.
                var window = GetTopWindow(HWND.NULL);
                for (var guard = 0; window != HWND.NULL && guard < 10_000; guard++)
                {
                    found.Add(Weigh(manager, window, own));
                    window = GetWindow(window, GW_HWNDNEXT);
                }
            }
            finally
            {
                if (manager != null)
                {
                    manager->Release();
                }
            }
            return found;
        }

        private static Candidate Weigh(IVirtualDesktopManager* manager, HWND window, uint own)
        {
            if (!IsWindowVisible(window) || IsIconic(window))
            {
                return new Candidate(window, false, null, false, false);
            }
            // The taskbar's own rule (Apps.Listed, §204).
            var listed = Apps.Listed(window);
            uint pid = 0;
            _ = GetWindowThreadProcessId(window, &pid);
            Guid? desktop = null;
            Guid assigned;
            if (manager != null && manager->GetWindowDesktopId(window, &assigned).SUCCEEDED && assigned != Guid.Empty)
            {
                desktop = assigned;
            }
            uint cloaked = 0;
            if (DwmGetWindowAttribute(window, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).FAILED)
            {
                cloaked = 0;
            }
            const uint ByShell = 2; // DWM_CLOAKED_SHELL
            return new Candidate(window, listed && pid != own, desktop, (cloaked & ~ByShell) != 0, (cloaked & ByShell) != 0);
        }
    }

    /// <summary>
    /// Removes desktop <paramref name="index"/>; its windows go to the one
    /// before it (after it, for the first), as with Windows' own close. Without
    /// the shell's interface only the current desktop can be closed, with
    /// Ctrl+Win+F4; false for any other, with a line in the log.
    /// </summary>
    public bool Remove(int index)
    {
        if (ids.Count <= 1 || index < 0 || index >= ids.Count)
        {
            return false;
        }
        if (DesktopControl.Remove(ids[index], ids[index == 0 ? 1 : index - 1]))
        {
            return true;
        }
        if (index == Current)
        {
            var keys = new List<INPUT>();
            Chord(keys, VK.VK_CONTROL, VK.VK_LWIN, VK.VK_F4);
            Send(keys);
            return true;
        }
        Console.Error.WriteLine($"Karpo: virtual desktops: desktop {index + 1} not removed; without the shell's interface only the current one can be closed");
        return false;
    }

    /// <summary>
    /// Moves desktop <paramref name="from"/> to <paramref name="to"/> in Windows'
    /// order. Windows has no keys for it: without the shell's interface, false
    /// (and the log says why).
    /// <para>
    /// Nothing calls this today. The way to reorder desktops was dragging their buttons along the
    /// bar, and there are no buttons since the switcher became one: it is kept because the act is
    /// the shell's rather than the bar's, and the list the switcher opens is where a reorder would
    /// go if somebody asks for one.
    /// </para>
    /// </summary>
    public bool Move(int from, int to) =>
        from >= 0 && to >= 0 && from < ids.Count && to < ids.Count && from != to && DesktopControl.Move(ids[from], to);

    private static List<INPUT> Steps(int from, int to)
    {
        var arrow = to > from ? VK.VK_RIGHT : VK.VK_LEFT;
        var keys = new List<INPUT>();
        for (var step = Math.Abs(to - from); step > 0; step--)
        {
            Chord(keys, VK.VK_CONTROL, VK.VK_LWIN, arrow);
        }
        return keys;
    }

    // Long enough for Windows' switch animation, short enough that a switch
    // that never happens doesn't hold the dock up for long.
    private const int ShowTimeoutMs = 1500;

    /// <summary>
    /// Brings <paramref name="hwnd"/> forward on its own desktop. When that is
    /// another desktop, the shortcuts switch there first and the window is
    /// activated once it shows.
    /// </summary>
    /// <remarks>
    /// Keys first, activation after, never the other way round. Activating a
    /// window on another desktop can make Windows switch by itself, and the
    /// shortcuts move relative to whatever desktop is current when Explorer
    /// reads them: with both under way, the keys would carry on past the
    /// window's desktop. By the time the window is activated here, the keys
    /// have been read and the switch is done, or it never came (Explorer
    /// refused injected keys, or the registry is behind), and then the
    /// activation's own switch is the one wanted.
    /// </remarks>
    public void Activate(nint hwnd)
    {
        var window = (HWND)hwnd;
        if (!IsWindow(window))
        {
            return;
        }
        var classId = CLSID.CLSID_VirtualDesktopManager;
        IVirtualDesktopManager* manager = null;
        if (CoCreateInstance(&classId, null, (uint)CLSCTX_ALL, __uuidof<IVirtualDesktopManager>(), (void**)&manager).SUCCEEDED)
        {
            try
            {
                // Read fresh, but leave Names and Current alone: taking a
                // change in here would hide it from the dock's own Check.
                var (order, current) = Order();
                var target = SwitchTarget(Locate(manager, window), order, current);
                if (target >= 0)
                {
                    if (target >= order.Count || !DesktopControl.Switch(order[target])) Send(Steps(current, target));
                    WaitShown(manager, window, ShowTimeoutMs);
                }
            }
            finally
            {
                manager->Release();
            }
        }
        Apps.Activate(window);
    }

    /// <summary>Where a window is, as far as Windows says.</summary>
    internal readonly record struct Place(bool Known, bool OnCurrent, Guid Desktop);

    internal static Place Locate(IVirtualDesktopManager* manager, HWND window)
    {
        BOOL on;
        Guid desktop;
        if (manager->IsWindowOnCurrentVirtualDesktop(window, &on).FAILED
            || manager->GetWindowDesktopId(window, &desktop).FAILED)
        {
            return default;
        }
        return new Place(true, on, desktop);
    }

    /// <summary>
    /// The desktop to switch to before activating, or -1 to activate the
    /// window where it is: it is on this desktop (a window shown on all of them
    /// counts), Windows can't say where it is, its desktop isn't listed, or the
    /// registry still calls that desktop current. In the last cases activating
    /// it is the one way there, and Windows switches by itself if it will.
    /// </summary>
    internal static int SwitchTarget(Place place, IReadOnlyList<Guid> order, int current)
    {
        if (!place.Known || place.OnCurrent)
        {
            return -1;
        }
        var target = -1;
        for (var index = 0; index < order.Count; index++)
        {
            if (order[index] == place.Desktop)
            {
                target = index;
                break;
            }
        }
        return target == current ? -1 : target;
    }

    /// <summary>
    /// Until the switch has put the window on screen: on the current desktop
    /// and no longer cloaked. Waited out here, with no message handling in
    /// between, since it lasts about as long as Windows' own switch animation.
    /// <see cref="Event"/> is left alone: it belongs to the dock's loop, and
    /// taking its signal here would lose the change.
    /// </summary>
    private static void WaitShown(IVirtualDesktopManager* manager, HWND window, int timeoutMs)
    {
        var end = GetTickCount64() + (ulong)timeoutMs;
        while (IsWindow(window))
        {
            uint cloaked = 0;
            if (DwmGetWindowAttribute(window, (uint)DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint)).FAILED)
            {
                cloaked = 0;
            }
            if (cloaked == 0 && Locate(manager, window) is { Known: true, OnCurrent: true })
            {
                return;
            }
            if (GetTickCount64() >= end)
            {
                return;
            }
            Sleep(15);
        }
    }

    /// <summary>Ctrl+Win+D: a new desktop, which Windows also switches to.</summary>
    public void Add() => PressNewDesktop();

    /// <summary>
    /// Ctrl+Win+D, where no desktops of the dock's are to hand: a Task view widget's "New desktop"
    /// (Dock.WidgetMenus.cs), through its seam, <see cref="NewDesktopKeys"/>.
    /// </summary>
    public static void NewDesktop() => NewDesktopKeys();

    /// <summary>What <see cref="NewDesktop"/> presses: Ctrl+Win+D, sent to Windows. A test puts a count in its place.</summary>
    internal static Action NewDesktopKeys = PressNewDesktop;

    private static void PressNewDesktop()
    {
        var keys = new List<INPUT>();
        Chord(keys, VK.VK_CONTROL, VK.VK_LWIN, VK_D);
        Send(keys);
    }

    /// <summary>
    /// Win+Tab: Task view, wherever it is asked for — the bar's button, the desktop switcher's menu,
    /// and a folder's Task view card and line (TaskViewTile) — through the one seam,
    /// <see cref="TaskViewKeys"/>.
    /// </summary>
    public static void TaskView() => TaskViewKeys();

    /// <summary>
    /// What <see cref="TaskView"/> presses: Win+Tab, sent to Windows. A test puts a count in its
    /// place so that nothing is pressed on the machine; nothing else touches it.
    /// </summary>
    internal static Action TaskViewKeys = PressTaskView;

    private static void PressTaskView()
    {
        var keys = new List<INPUT>();
        Chord(keys, VK.VK_LWIN, VK.VK_TAB);
        Send(keys);
    }

    /// <summary>
    /// Win+N: notifications and the calendar, which the hidden taskbar's clock
    /// would otherwise open.
    /// </summary>
    public static void Notifications()
    {
        var keys = new List<INPUT>();
        Chord(keys, VK.VK_LWIN, VK_N);
        Send(keys);
    }

    /// <summary>
    /// Start, opened on display <paramref name="monitor"/> (the clicked button's) as a click on that
    /// display's taskbar Start button opens it: Explorer's launcher of Start is connected to the
    /// display and opens Start there (<see cref="StartLauncher"/>; left alone, it opens where the
    /// foreground window is). Where it can't, Explorer's taskbar is asked (SC_TASKLIST), and Windows
    /// picks the display. No key is pressed for either: a pressed Windows key is what a launcher that
    /// takes the key for itself (Raycast, 2026-10-25) swallows, and then Start never opened. Where
    /// Explorer has no taskbar, the Windows key alone, down and up.
    /// </summary>
    public static void StartMenu(nint monitor) => OpenStart(monitor, StartLauncher.Instance, Taskbar.ExplorerTray(), AskForStart, Send);

    /// <summary>
    /// <see cref="StartMenu"/> with its parts handed in, so a test holds it without opening Start:
    /// <paramref name="launcher"/> connected to <paramref name="monitor"/> and asked to show Start
    /// (nothing where there is no display); else <paramref name="tray"/> asked through
    /// <paramref name="ask"/> where there is one, and the keys pressed through <paramref name="press"/>
    /// only where it is not or will not take the message.
    /// </summary>
    internal static void OpenStart(nint monitor, IStartLauncher launcher, HWND tray, Func<HWND, bool> ask, Action<List<INPUT>> press)
    {
        if (monitor != 0 && launcher.Connect(monitor) && launcher.Show())
        {
            return;
        }
        if (tray == HWND.NULL || !ask(tray))
        {
            press(StartKeys());
        }
    }

    /// <summary>What the taskbar's Start button does: WM_SYSCOMMAND with SC_TASKLIST, posted to the taskbar.</summary>
    private static bool AskForStart(HWND tray) => PostMessageW(tray, WM.WM_SYSCOMMAND, TaskList, 0);

    /// <summary>SC_TASKLIST, the system command that opens Start (WinUser.h).</summary>
    private const uint TaskList = 0xF130;

    /// <summary>
    /// What <see cref="StartMenu"/> presses where there is no taskbar to ask: the left Windows key,
    /// down and up, marked Karpo's. Not a chord: with any other key down in between, Windows reads a
    /// shortcut and Start stays shut.
    /// </summary>
    internal static List<INPUT> StartKeys() => [Stroke(VK.VK_LWIN, up: false), Stroke(VK.VK_LWIN, up: true)];

    public void Dispose()
    {
        if (key != HKEY.NULL)
        {
            _ = RegCloseKey(key);
            key = HKEY.NULL;
        }
        if (changed != HANDLE.NULL)
        {
            _ = CloseHandle(changed);
            changed = HANDLE.NULL;
        }
    }

    // Presses `combo` in order and releases it in reverse. Another key goes down
    // while Win is held, so Windows doesn't also open Start.
    private static void Chord(List<INPUT> into, params ReadOnlySpan<int> combo)
    {
        foreach (var vk in combo)
        {
            into.Add(Stroke(vk, up: false));
        }
        for (var index = combo.Length - 1; index >= 0; index--)
        {
            into.Add(Stroke(combo[index], up: true));
        }
    }

    private static INPUT Stroke(int vk, bool up)
    {
        var flags = 0u;
        if (vk is VK.VK_LEFT or VK.VK_RIGHT or VK.VK_LWIN)
        {
            flags |= (uint)KEYEVENTF_EXTENDEDKEY;
        }
        if (up)
        {
            flags |= (uint)KEYEVENTF_KEYUP;
        }
        var input = new INPUT { type = INPUT.INPUT_KEYBOARD };
        // Marked Karpo's, so the keyboard hook lets them by: Karpo's own Windows key is no press of
        // the person's for the taskbar's chords to follow (TaskbarKeys.cs).
        input.ki = new KEYBDINPUT { wVk = (ushort)vk, wScan = 0, dwFlags = flags, time = 0, dwExtraInfo = TaskbarKeys.Ours };
        return input;
    }

    private static void Send(List<INPUT> keys)
    {
        var span = CollectionsMarshal.AsSpan(keys);
        fixed (INPUT* first = span)
        {
            _ = SendInput((uint)span.Length, first, sizeof(INPUT));
        }
    }

    private static byte[] Binary(HKEY key, string value)
    {
        fixed (char* name = value)
        {
            uint size = 0;
            if (RegGetValueW(key, null, name, (uint)RRF.RRF_RT_REG_BINARY, null, null, &size) != ERROR.ERROR_SUCCESS)
            {
                return [];
            }
            var data = new byte[size];
            fixed (byte* buffer = data)
            {
                if (RegGetValueW(key, null, name, (uint)RRF.RRF_RT_REG_BINARY, null, buffer, &size) != ERROR.ERROR_SUCCESS)
                {
                    return [];
                }
            }
            if (size < data.Length)
            {
                Array.Resize(ref data, (int)size);
            }
            return data;
        }
    }

    // A renamed desktop keeps its name under Desktops\{GUID}.
    private static string? Name(HKEY key, Guid id)
    {
        var subkey = @"Desktops\" + id.ToString("B").ToUpperInvariant();
        Span<char> text = stackalloc char[260];
        text.Clear();
        var size = (uint)(text.Length * 2);
        fixed (char* sub = subkey)
        fixed (char* value = "Name")
        fixed (char* buffer = text)
        {
            if (RegGetValueW(key, sub, value, (uint)RRF.RRF_RT_REG_SZ, null, buffer, &size) != ERROR.ERROR_SUCCESS)
            {
                return null;
            }
        }
        var length = text.IndexOf('\0');
        // String::from_utf16_lossy in the Rust: an unpaired surrogate becomes U+FFFD.
        var name = Encoding.Unicode.GetString(MemoryMarshal.AsBytes((ReadOnlySpan<char>)(length < 0 ? text : text[..length])));
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    // The value holds the GUID as it sits in memory: the first three fields
    // little-endian, the last eight bytes in order. That is Guid's own layout.
    private static Guid Id(ReadOnlySpan<byte> bytes) => new(bytes);
}
