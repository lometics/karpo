// The taskbar's keys, while Karpo's bars are the taskbar: Win+1..9 and Win+0 for the app buttons,
// Win+T for the keyboard on them, Win+B for the keyboard on the tray — what Windows 11's taskbar
// answers, and what Explorer answers only for its own taskbar, which is hidden then.
//
//   Win+N            the Nth app button, as a click on it does (AppClicks): started, brought
//                    forward, or minimized when it is the one in front
//   Win+Shift+N      another one of it
//   Win+Ctrl+Shift+N another one of it, as administrator
//   Win+Ctrl+N       its windows one after another, the most recent first; never minimized
//   Win+Alt+N        its jump list
//   Win+T            the keyboard on the app buttons (Win+Shift+T from the end); again, the next
//   Win+B            the keyboard on the tray
//
// N counts as Windows counts: the app buttons along the bar of the main display, an app each —
// a pin standing as a button per window, or an app's windows a button each, is one number — pins
// and open apps alike, and the ones behind "…" too. 0 is the tenth.
//
// **How the keys are taken.** Explorer takes these chords for its own taskbar even while it is
// hidden, so Karpo has to hear them first and keep them: a low-level keyboard hook, on a thread
// of its own that does nothing else, so an answer is never late behind the dock's own work
// (Windows takes a hook away that answers too slowly). It is only put in while Karpo's bars are
// the taskbar (Dock.taskbar: Explorer's is hidden) and taken out when Explorer's comes back. It
// keeps exactly the chords above — the key that makes the chord, down and up, and its repeats —
// and lets everything else go by, the Windows key itself included. What a chord does is decided
// on the dock's thread, from a posted message: the hook only says which.
//
// **The Start menu.** Windows opens Start when the Windows key comes up with nothing pressed
// since it went down, and a key the hook kept was never pressed as far as Windows knows. So the
// first chord kept in a press of the Windows key is followed by a key that means nothing (0xE8,
// unassigned, which AutoHotkey sends for the same reason), injected and marked as Karpo's, which
// the hook lets by.
//
// **Start itself is Windows'.** The Windows key alone and Ctrl+Esc are never taken: they go to
// Windows, whose Start opens.
//
// **Releases the hook never hears.** It hears nothing while another desktop has the input (Win+L's
// lock screen, UAC's prompt, Ctrl+Alt+Del) or while Windows has taken it away for a slow answer, so
// before each key it lets go of every modifier the keyboard itself no longer holds
// (KeyChords.IsDown): a Windows key let go on the lock screen does not keep a plain 1, T or B as a
// chord afterwards.
//
// Everything that decides is here and pure (KeyChords, AppNumbers, AppKeys, KeyStops), so the
// tests call it with key codes and bar plans and nothing is hooked, pressed or started by them.

using System.Runtime.InteropServices;
using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What a chord of the taskbar's asks for.</summary>
public enum ChordKind
{
    /// <summary>Win+N: the Nth app, as a click on its button.</summary>
    App,

    /// <summary>Win+Shift+N: another one of the Nth app.</summary>
    NewApp,

    /// <summary>Win+Ctrl+Shift+N: another one of the Nth app, as administrator.</summary>
    ElevatedApp,

    /// <summary>Win+Ctrl+N: the Nth app's windows, one after another.</summary>
    CycleApp,

    /// <summary>Win+Alt+N: the Nth app's jump list.</summary>
    JumpList,

    /// <summary>Win+T: the keyboard on the app buttons, the first of them; again, the next.</summary>
    FocusApps,

    /// <summary>Win+Shift+T: the keyboard on the app buttons, the last of them; again, the one before.</summary>
    FocusAppsBack,

    /// <summary>Win+B: the keyboard on the tray.</summary>
    FocusTray,
}

/// <summary>A chord: what it asks for, and for the app chords which app button (1 to 10).</summary>
public readonly record struct TaskbarChord(ChordKind Kind, int Number = 0)
{
    /// <summary>As the posted message carries it: the kind in the high half, the number in the low.</summary>
    public nuint Packed => ((nuint)Kind << 16) | (uint)Number;

    public static TaskbarChord Unpack(nuint packed) => new((ChordKind)(int)(packed >> 16), (int)(packed & 0xFFFF));
}

/// <summary>
/// What the hook does with one key: let it by, or keep it — and when it keeps the first chord of a
/// press of the Windows key, <see cref="Mask"/> says the Start menu has to be told a key came
/// between (see the top of the file). <see cref="Act"/> is the chord made, once per press.
/// </summary>
public readonly record struct KeyHeard(bool Swallow, bool Mask = false, TaskbarChord? Act = null);

/// <summary>
/// Which keys make a chord of the taskbar's, key by key as the hook hears them. It follows the
/// modifiers itself, from the same keys, so it answers from what it was told and nothing else.
/// </summary>
public sealed class KeyChords
{
    private readonly HashSet<uint> held = [];
    private readonly HashSet<uint> kept = [];

    // Whether the Start menu was already masked in this press of the Windows key.
    private bool masked;

    /// <summary>
    /// Whether a key is down this moment, as the keyboard has it (GetAsyncKeyState); null where only
    /// what is heard counts. The hook hears nothing while another desktop has the input (Win+L's lock
    /// screen, UAC's prompt, Ctrl+Alt+Del) or while Windows has taken it away for a slow answer, so a
    /// release it never heard would leave a modifier held for good: a plain 1, T or B kept as Win+1,
    /// Win+T or Win+B. A modifier the keyboard no longer holds is let go before each key is decided.
    /// </summary>
    public Func<uint, bool>? IsDown { get; set; }

    private bool Win => held.Contains(VK.VK_LWIN) || held.Contains(VK.VK_RWIN);

    private bool Shift => held.Contains(VK.VK_SHIFT) || held.Contains(VK.VK_LSHIFT) || held.Contains(VK.VK_RSHIFT);

    private bool Control => held.Contains(VK.VK_CONTROL) || held.Contains(VK.VK_LCONTROL) || held.Contains(VK.VK_RCONTROL);

    private bool Alt => held.Contains(VK.VK_MENU) || held.Contains(VK.VK_LMENU) || held.Contains(VK.VK_RMENU);

    /// <summary>Key <paramref name="vk"/> went down, or came up: what the hook does with it.</summary>
    public KeyHeard Hear(uint vk, bool down)
    {
        ForgetReleased(vk, down);
        if (Modifier(vk))
        {
            if (down)
            {
                held.Add(vk);
            }
            else
            {
                held.Remove(vk);
                if (!Win)
                {
                    masked = false;
                }
            }
            // The modifiers always go by: Windows has to know they are held, and let go.
            return default;
        }
        if (!down)
        {
            // The key of a chord comes up kept too, whatever was let go before it.
            return new KeyHeard(kept.Remove(vk));
        }
        if (kept.Contains(vk))
        {
            // Held down: its repeats are kept, and make nothing more.
            return new KeyHeard(true);
        }
        if (!Win || ChordOf(vk, Shift, Control, Alt) is not { } chord)
        {
            return default;
        }
        kept.Add(vk);
        var mask = !masked;
        masked = true;
        return new KeyHeard(true, mask, chord);
    }

    /// <summary>
    /// The modifiers held as far as the hook heard that the keyboard no longer holds (<see cref="IsDown"/>),
    /// let go. The keyboard has not taken <paramref name="vk"/>, the key being heard, yet: going down
    /// while the keyboard has it up, it is a new press and not a repeat, so a release of it that was
    /// never heard is let go too; coming up, it is left to the rules below. A Windows key let go
    /// unheard ends its press: nothing masked.
    /// Their presses always went by, so the keyboard knows of every one that is really down.
    /// </summary>
    private void ForgetReleased(uint vk, bool down)
    {
        if (IsDown is not { } isDown || held.Count == 0)
        {
            return;
        }
        var win = Win;
        held.RemoveWhere(key => (key != vk || down) && !isDown(key));
        if (win && !Win)
        {
            masked = false;
        }
    }

    /// <summary>
    /// The chord key <paramref name="vk"/> makes with the Windows key and these modifiers; null for
    /// every key and combination that is not the taskbar's, which the hook lets by.
    /// </summary>
    public static TaskbarChord? ChordOf(uint vk, bool shift, bool control, bool alt)
    {
        if (vk is >= '0' and <= '9')
        {
            var number = vk == '0' ? 10 : (int)(vk - '0');
            return (shift, control, alt) switch
            {
                (false, false, false) => new TaskbarChord(ChordKind.App, number),
                (true, false, false) => new TaskbarChord(ChordKind.NewApp, number),
                (true, true, false) => new TaskbarChord(ChordKind.ElevatedApp, number),
                (false, true, false) => new TaskbarChord(ChordKind.CycleApp, number),
                (false, false, true) => new TaskbarChord(ChordKind.JumpList, number),
                _ => null,
            };
        }
        return (vk, shift, control, alt) switch
        {
            ('T', false, false, false) => new TaskbarChord(ChordKind.FocusApps),
            ('T', true, false, false) => new TaskbarChord(ChordKind.FocusAppsBack),
            ('B', false, false, false) => new TaskbarChord(ChordKind.FocusTray),
            _ => null,
        };
    }

    private static bool Modifier(uint vk) => vk is VK.VK_LWIN or VK.VK_RWIN
        or VK.VK_SHIFT or VK.VK_LSHIFT or VK.VK_RSHIFT
        or VK.VK_CONTROL or VK.VK_LCONTROL or VK.VK_RCONTROL
        or VK.VK_MENU or VK.VK_LMENU or VK.VK_RMENU;
}

/// <summary>Which app buttons a number of Win+N stands for.</summary>
public static class AppNumbers
{
    /// <summary>
    /// The numbered apps of a bar whose buttons are, in order along it, <paramref name="identities"/>
    /// (an app's or a pin's, the same for every button it stands as): each run of one identity is one
    /// number, as Windows counts an app whose windows are buttons apart once. Each is the first
    /// button of the run and how many there are. An app counted already keeps its number where it
    /// stands again further on — a bar holding the open apps twice shows each of them twice —, so
    /// the second copy takes none and the numbers go on with the next app after it.
    /// </summary>
    public static List<(int First, int Count)> Numbered(IReadOnlyList<string> identities)
    {
        var numbered = new List<(int First, int Count)>();
        var counted = new HashSet<string>();
        var run = -1;
        for (var index = 0; index < identities.Count; index++)
        {
            if (index > 0 && identities[index] == identities[index - 1])
            {
                if (run >= 0)
                {
                    numbered[run] = (numbered[run].First, numbered[run].Count + 1);
                }
                continue;
            }
            run = counted.Add(identities[index]) ? numbered.Count : -1;
            if (run >= 0)
            {
                numbered.Add((index, 1));
            }
        }
        return numbered;
    }

    /// <summary>The buttons of number <paramref name="number"/> (1 to 10); null past the last app, where Win+N does nothing.</summary>
    public static (int First, int Count)? Nth(IReadOnlyList<string> identities, int number)
    {
        var numbered = Numbered(identities);
        return number >= 1 && number <= numbered.Count ? numbered[number - 1] : null;
    }
}

/// <summary>What Win+Ctrl+N does with an app's windows.</summary>
public static class AppKeys
{
    /// <summary>
    /// Win+Ctrl+N on an app with <paramref name="windows"/> (most recently used first), the window in
    /// front being <paramref name="front"/>: none open, it is started; none of them in front, the
    /// most recent comes; one of them in front, the least recent, so pressing again walks through
    /// them all. It never minimizes: the one in front of an app with one window stays.
    /// </summary>
    public static AppClick Cycle(IReadOnlyList<AppWindow> windows, nint front)
    {
        if (windows.Count == 0)
        {
            return new AppClick(AppClickKind.Launch);
        }
        for (var index = 0; index < windows.Count; index++)
        {
            if (front != 0 && windows[index].Handle == front && !windows[index].Minimized)
            {
                return new AppClick(AppClickKind.Activate, windows.Count - 1);
            }
        }
        return new AppClick(AppClickKind.Activate, 0);
    }
}

/// <summary>
/// Where the keyboard is on a bar: its app buttons, its tray, or the rest of what stands along it —
/// Start, search, task view, Windows' buttons, every widget's cells, show desktop.
/// </summary>
public enum KeyRegion
{
    Apps,
    Tray,
    Bar,
}

/// <summary>The things of a bar the keyboard stops at, by region (docs\widgets.md, "One keyboard").</summary>
internal static class KeyStops
{
    /// <summary>
    /// The stops of <paramref name="region"/> on the bar <paramref name="layout"/> plans, in their
    /// order along it: the entries a screen reader lists (<see cref="BarUia.Entries"/>) that are in
    /// that region. The app buttons it shows of its <paramref name="tiles"/> and "…"; the tray icons
    /// each of its trays shows and the chevron of the hidden ones; everything else along the bar. What
    /// the bar has no room for is no stop: "…" is. Edit mode's "+" buttons are edit mode's.
    /// </summary>
    public static List<Hit> Of(Layout layout, KeyRegion region, int tiles) =>
        [.. BarUia.Entries(layout, tiles).Where(hit => RegionOf(hit) == region)];

    /// <summary>The region <paramref name="hit"/> is a stop of; null for none (a "+", the bar itself).</summary>
    public static KeyRegion? RegionOf(Hit hit) => hit.Kind switch
    {
        HitKind.Tile or HitKind.AppOverflow => KeyRegion.Apps,
        HitKind.Tray or HitKind.TrayOverflow => KeyRegion.Tray,
        HitKind.Start or HitKind.Search or HitKind.TaskView or HitKind.WindowsButton or HitKind.Island or HitKind.ShowDesktop => KeyRegion.Bar,
        _ => null,
    };

    /// <summary>
    /// The regions of the bar that have stops, in the order they start along it: what Tab and
    /// Shift+Tab go round.
    /// </summary>
    public static List<KeyRegion> Regions(Layout layout, int tiles) =>
        [.. BarUia.Entries(layout, tiles).Select(RegionOf).OfType<KeyRegion>().Distinct()];

    /// <summary>
    /// The region Tab (<paramref name="step"/> 1) or Shift+Tab (-1) goes to from <paramref name="from"/>,
    /// round; <paramref name="from"/> itself where it is the only one.
    /// </summary>
    public static KeyRegion Next(IReadOnlyList<KeyRegion> regions, KeyRegion from, int step)
    {
        var at = regions.ToList().IndexOf(from);
        return FocusNav.Step(regions.Count, at, step) is var next and >= 0 ? regions[next] : from;
    }

    /// <summary>The step an arrow key takes along a bar: back for left and up, on for right and down; 0 for any other key.</summary>
    public static int Arrow(uint vk) => vk switch
    {
        VK.VK_LEFT or VK.VK_UP => -1,
        VK.VK_RIGHT or VK.VK_DOWN => 1,
        _ => 0,
    };
}

/// <summary>
/// The low-level keyboard hook that takes the taskbar's chords (see the top of the file), on a
/// thread of its own. The dock starts it while its bars are the taskbar and stops it when
/// Explorer's comes back; a chord made is posted to <c>tell</c> as <see cref="WM_KARPO_KEYS"/>.
/// </summary>
internal sealed unsafe class TaskbarKeys : IDisposable
{
    /// <summary>A chord was made: wParam is <see cref="TaskbarChord.Packed"/>.</summary>
    public const uint WM_KARPO_KEYS = WM.WM_APP + 0x26;

    /// <summary>The key that tells the Start menu a key came between (see the top of the file): unassigned.</summary>
    internal const ushort MaskKey = 0xE8;

    /// <summary>What Karpo's own injected keys carry, so the hook lets them by (Desktops' too).</summary>
    internal const nuint Ours = 0x4B617270; // "Karp"

    // The hook thread's message that puts the hook in again.
    private const uint WM_REARM = WM.WM_APP + 1;

    // The one the hook calls. There is one dock, and one hook in it.
    private static TaskbarKeys? current;

    private readonly HWND tell;
    private readonly KeyChords chords = new() { IsDown = static key => GetAsyncKeyState((int)key) < 0 };
    private Thread? thread;
    private uint threadId;
    private HHOOK hook;

    public TaskbarKeys(HWND tell) => this.tell = tell;

    /// <summary>Whether the hook is in.</summary>
    public bool Running => thread is not null;

    /// <summary>
    /// Puts the hook in, on its thread, or — running already — in again: Windows takes a low-level
    /// hook away without a word once it has answered too slowly, so the dock asks again with every
    /// arrange of its bars.
    /// </summary>
    public void Start()
    {
        if (thread is not null)
        {
            _ = PostThreadMessageW(threadId, WM_REARM, 0, 0);
            return;
        }
        current = this;
        // Not disposed here: a thread slower than the wait below still sets it, and must not throw.
        var ready = new ManualResetEventSlim(false);
        thread = new Thread(() => Troubles.Guard("Taskbar keys", () => Run(ready))) { IsBackground = true, Name = "Karpo taskbar keys", Priority = ThreadPriority.Highest }; // not-ui: a thread's name
        thread.Start();
        ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>Takes the hook out, and its thread ends: Explorer's taskbar is back, or the dock is closing.</summary>
    public void Stop()
    {
        if (thread is not { } running)
        {
            return;
        }
        _ = PostThreadMessageW(threadId, WM.WM_QUIT, 0, 0);
        running.Join(TimeSpan.FromSeconds(2));
        thread = null;
        if (current == this)
        {
            current = null;
        }
    }

    public void Dispose() => Stop();

    private void Run(ManualResetEventSlim ready)
    {
        threadId = GetCurrentThreadId();
        MSG msg;
        // A message queue before anybody posts to it.
        _ = PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_NOREMOVE);
        Arm();
        ready.Set();
        while (GetMessageW(&msg, HWND.NULL, 0, 0) > 0)
        {
            if (msg.message == WM_REARM)
            {
                Arm();
            }
        }
        if (hook != HHOOK.NULL)
        {
            _ = UnhookWindowsHookEx(hook);
            hook = HHOOK.NULL;
        }
    }

    private void Arm()
    {
        if (hook != HHOOK.NULL)
        {
            _ = UnhookWindowsHookEx(hook);
        }
        hook = SetWindowsHookExW(WH.WH_KEYBOARD_LL, &Keyboard, GetModuleHandleW(null), 0);
    }

    [UnmanagedCallersOnly]
    private static LRESULT Keyboard(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && current is { } keys)
        {
            try
            {
                var key = (KBDLLHOOKSTRUCT*)(nint)lParam;
                // Karpo's own mask key goes by untouched.
                if (key->dwExtraInfo != Ours && keys.Heard(key->vkCode, ((uint)(nuint)wParam) is WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN))
                {
                    return (LRESULT)1;
                }
            }
            catch (Exception)
            {
                // Nothing may escape into the input system's callback.
            }
        }
        return CallNextHookEx(HHOOK.NULL, code, wParam, lParam);
    }

    /// <summary>One key, heard on the hook's thread: true to keep it. A chord made is posted, never acted on here.</summary>
    private bool Heard(uint vk, bool down)
    {
        var heard = chords.Hear(vk, down);
        if (heard.Mask)
        {
            MaskStart();
        }
        if (heard.Act is { } chord)
        {
            _ = PostMessageW(tell, WM_KARPO_KEYS, (WPARAM)chord.Packed, 0);
        }
        return heard.Swallow;
    }

    /// <summary>A key that means nothing, pressed and let go, so the Windows key's release opens no Start menu.</summary>
    private static void MaskStart()
    {
        INPUT* taps = stackalloc INPUT[2];
        for (var index = 0; index < 2; index++)
        {
            taps[index] = default;
            taps[index].type = INPUT.INPUT_KEYBOARD;
            taps[index].ki.wVk = MaskKey;
            taps[index].ki.dwFlags = index == 0 ? 0 : (uint)KEYEVENTF_KEYUP;
            taps[index].ki.dwExtraInfo = Ours;
        }
        _ = SendInput(2, taps, sizeof(INPUT));
    }
}
