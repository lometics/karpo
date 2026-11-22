// edit: the clicks and Escape that put down or call off a widget following the pointer
// (Dock.EditFollow.cs), and the Escape that leaves edit mode, heard wherever they land.
//
// The person, on the first build of it (2026-10-07): "the placing does not work after i selected the
// widget. it gets stuck. the esc also does not work and placing with left click does also not
// work." The bar carrying the widget could not hear any of it:
//
//  - **The add list had the foreground.** It is a menu, which takes it. It went with the pick, and
//    Windows gave the foreground to another app's window, which takes the mouse from a window that
//    holds it with no button down: the bar dropped the block it carried, the widget stayed in the
//    list, and nothing moved it, put it down or called it off any more.
//  - **No button is held.** A window that holds the mouse with no button down hears it only over
//    its own thread's windows: a click on the desktop or another app went there, and the click away
//    that edit mode had then ended it instead.
//  - **Nor the keys.** Escape went to whichever window had them.
//
// So while a widget follows, a low-level mouse hook and keyboard hook hear the left press (it is
// put down), the right press and Escape (it is called off), wherever they land, and keep them from
// what is under the pointer — and their releases, so nothing hears half a click. The pointer's
// moves are left alone: the drag's tick follows them (DockWindow.FollowTick). What a press means
// is acted on from the message posted to the dock's window, never inside the hook. A press on a
// desktop other than the bar's is let by (InputDesktop), as a menu's hook lets it.
//
// Escape out of edit mode (the person, 2026-10-07, round 6: Escape did not leave edit mode). The bar
// being edited heard it only while it held the keys, and often it did not: the dim and the edit
// bars take no activation, a click on them leaves the keys wherever they were — with the settings
// app, which edit mode stands over everything, or with nothing at all. So all edit mode long the
// keyboard hook is in, and Escape is heard wherever the keys are: while a widget follows it calls
// that off; else it ends a drag, or leaves edit mode. It is kept from the window with the keys —
// unless that is one of the dock's own popups (a menu, the add list, an open folder), which closes
// itself on it first, as Windows' popups do. The pointer is not heard while nothing follows.
//
// §490: a key Karpo sends itself is never the person's. As a flyout of Karpo's opens it closes Windows' menu in front
// with an Escape sent to it (WindowsPanels.CloseFor); in edit mode this hook heard that Escape as the person's — the add
// list opening over quick settings left edit mode, and closed with it, and quick settings stayed (the person,
// 2026-11-02: "when a windows menu was open i was not able to add a new taskbar widget as it always closed the add
// dialog"). Karpo's keys carry TaskbarKeys.Ours, and the hook lets them by.

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What a press or a key means to the widget following the pointer.</summary>
public enum FollowInput
{
    None,

    /// <summary>The left press: the widget is put down where it shows it would land.</summary>
    Place,

    /// <summary>The right press, or Escape: the widget is called off.</summary>
    Cancel,

    /// <summary>Escape in edit mode with nothing following: a drag ends, else edit mode does.</summary>
    Leave,

    /// <summary>Ctrl+Z in edit mode with nothing following: the last change undone (§352).</summary>
    Undo,

    /// <summary>Ctrl+Y or Ctrl+Shift+Z in edit mode with nothing following: the change undone last made again (§352).</summary>
    Redo,
}

/// <summary>
/// The rule, apart from any hook: which mouse and key messages a widget following the pointer
/// takes, what they mean, and which releases are kept because their press was. Pure.
/// </summary>
public sealed class FollowGesture
{
    private bool left;
    private bool right;
    private bool escape;

    /// <summary>The letter of an undo or a redo taken (Z or Y), whose release is still to be kept; 0 for none.</summary>
    private uint chord;

    /// <summary>A widget is following the pointer: its presses and Escape are taken.</summary>
    public bool Following { get; set; }

    /// <summary>Edit mode is on: Escape is taken where nothing follows, to leave it.</summary>
    public bool Editing { get; set; }

    /// <summary>A press or Escape taken has not come up yet: its release is still to be kept.</summary>
    public bool Owes => OwesMouse || OwesKey;

    /// <summary>A press taken has not come up yet.</summary>
    public bool OwesMouse => left || right;

    /// <summary>An Escape, or an undo's or a redo's letter, taken has not come up yet.</summary>
    public bool OwesKey => escape || chord != 0;

    /// <summary>
    /// A low-level mouse message: whether it is kept from what is under the pointer, and what it
    /// means. A release whose press was not taken — the button that took a widget out of the tray,
    /// down before the follow began — goes on, as it went down.
    /// </summary>
    public (bool Keep, FollowInput Input) Mouse(uint message) => message switch
    {
        WM.WM_LBUTTONDOWN when Following => Take(ref left, FollowInput.Place),
        WM.WM_RBUTTONDOWN when Following => Take(ref right, FollowInput.Cancel),
        WM.WM_LBUTTONUP => Up(ref left),
        WM.WM_RBUTTONUP => Up(ref right),
        _ => (false, FollowInput.None),
    };

    /// <summary>
    /// A low-level key message, of virtual key <paramref name="key"/>: Escape calls the widget off at
    /// its first press; with none following, in edit mode, it leaves edit mode — unless
    /// <paramref name="theirs"/> says the window with the keys closes on it itself (a popup of the
    /// dock's own), which then has it. Held down, its repeats mean nothing more, and are kept all the
    /// same; so one Escape calls a widget off, and only the next leaves edit mode.
    /// §352: and in edit mode, with none following, Ctrl+Z undoes and Ctrl+Y or Ctrl+Shift+Z redoes, as
    /// edit mode's keymap reads them (<see cref="EditKeys.Of"/>, with <paramref name="control"/> and
    /// <paramref name="shift"/> held), wherever the keys are — unless <paramref name="menu"/> says one of
    /// the dock's menus stands open, which has them (a name being typed undoes its own). Held down, each
    /// repeat steps on.
    /// §490: a key Karpo sent itself (<paramref name="ours"/>, <see cref="TaskbarKeys.Ours"/>) is never the person's: the
    /// Escape that closes Windows' menu in front as a flyout of Karpo's opens over it goes by to that menu, untouched — it
    /// was leaving edit mode, the add list with it.
    /// </summary>
    public (bool Keep, FollowInput Input) Key(uint message, uint key, Func<bool>? theirs = null, bool control = false, bool shift = false, Func<bool>? menu = null, bool ours = false)
    {
        if (ours)
        {
            return (false, FollowInput.None);
        }
        if (key != VK.VK_ESCAPE)
        {
            return Step(message, key, control, shift, menu);
        }
        switch (message)
        {
            case WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN when escape:
                return (true, FollowInput.None);
            case WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN when Following:
                return Take(ref escape, FollowInput.Cancel);
            case WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN when Editing && theirs?.Invoke() != true:
                return Take(ref escape, FollowInput.Leave);
            case WM.WM_KEYUP or WM.WM_SYSKEYUP:
                return Up(ref escape);
            default:
                return (false, FollowInput.None);
        }
    }

    /// <summary>An undo's or a redo's key (<see cref="Key"/>): its press taken in edit mode, its release kept as its press was.</summary>
    private (bool Keep, FollowInput Input) Step(uint message, uint key, bool control, bool shift, Func<bool>? menu)
    {
        switch (message)
        {
            case WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN when Editing && !Following
                && EditKeys.Of((int)key, control, shift, repeat: false).Act is var act and (EditKeyAct.Undo or EditKeyAct.Redo)
                && menu?.Invoke() != true:
                chord = key;
                return (true, act == EditKeyAct.Undo ? FollowInput.Undo : FollowInput.Redo);
            case WM.WM_KEYUP or WM.WM_SYSKEYUP when chord != 0 && key == chord:
                chord = 0;
                return (true, FollowInput.None);
            default:
                return (false, FollowInput.None);
        }
    }

    /// <summary>Nothing is owed: the hooks are out.</summary>
    public void Reset()
    {
        ForgetMouse();
        ForgetKey();
    }

    /// <summary>No press is owed: the mouse hook is out.</summary>
    public void ForgetMouse() => left = right = false;

    /// <summary>No Escape, nor an undo's or a redo's letter, is owed: the keyboard hook is out.</summary>
    public void ForgetKey()
    {
        escape = false;
        chord = 0;
    }

    private static (bool, FollowInput) Take(ref bool down, FollowInput input)
    {
        down = true;
        return (true, input);
    }

    private static (bool, FollowInput) Up(ref bool down)
    {
        var kept = down;
        down = false;
        return (kept, FollowInput.None);
    }
}

/// <summary>
/// The hooks a widget following the pointer keeps, and edit mode the keyboard's (see the top of the
/// file), feeding <see cref="FollowGesture"/> and posting <see cref="WM_KARPO_FOLLOW"/> to
/// <c>tell</c>'s window, <c>wParam</c> the <see cref="FollowInput"/>, <c>lParam</c> where the press was
/// (screen px, x in the low word, y in the high one). They run on the dock's own thread, which put
/// them in.
/// </summary>
internal sealed unsafe class EditFollowHook : IDisposable
{
    /// <summary>Posted to the dock's window for a press or a key a widget following the pointer took.</summary>
    public const uint WM_KARPO_FOLLOW = WM.WM_APP + 0x2C;

    // The callbacks are static: this is the instance they serve, on the thread that put them in.
    [ThreadStatic]
    private static EditFollowHook? armed;

    private readonly HINSTANCE instance;
    private readonly Func<HWND> tell;
    private readonly Func<bool>? theirs;
    private readonly Func<bool>? menu;
    private readonly FollowGesture gesture = new();
    private HHOOK mouse;
    private HHOOK keys;

    /// <param name="instance">The module the hooks are put in for.</param>
    /// <param name="tell">The window what was heard is posted to; read each time, because it may be made afresh.</param>
    /// <param name="theirs">Whether the window with the keys closes on Escape itself, so edit mode lets it have it (<see cref="FollowGesture.Key"/>).</param>
    /// <param name="menu">§352: whether one of the dock's menus stands open, which has Ctrl+Z and Ctrl+Y rather than edit mode.</param>
    public EditFollowHook(HINSTANCE instance, Func<HWND> tell, Func<bool>? theirs = null, Func<bool>? menu = null)
    {
        this.instance = instance;
        this.tell = tell;
        this.theirs = theirs;
        this.menu = menu;
    }

    /// <summary>A widget follows the pointer: the hooks are put in, afresh (Windows takes a slow one away without a word).</summary>
    public void Arm()
    {
        gesture.Following = true;
        HookMouse();
        HookKeys();
    }

    /// <summary>
    /// Edit mode began (<paramref name="on"/>): the keyboard hook goes in, afresh, so Escape leaves it
    /// wherever the keys are — on the desktop with the person's input only: a test's edit mode, on a
    /// desktop of its own, takes none of their keys. Or it ended: the hook comes out, once nothing
    /// follows and the Escape that ended it has come up.
    /// </summary>
    public void Edit(bool on)
    {
        gesture.Editing = on;
        if (on && InputDesktop.Holds())
        {
            HookKeys();
            return;
        }
        Settle();
    }

    /// <summary>
    /// Edit mode goes on through a change that took a while — a folder opened or closed arranged,
    /// which builds a window and its lists: the keyboard hook goes in afresh, as Windows takes a slow
    /// one away without a word, and then no Escape would leave edit mode any more. Not while an
    /// Escape it took is still to come up: it is in, it just heard it.
    /// </summary>
    public void Refresh()
    {
        if (gesture.Editing && !gesture.OwesKey && InputDesktop.Holds())
        {
            HookKeys();
        }
    }

    /// <summary>
    /// No widget follows the pointer any more. The hooks stay in until the press or the key that
    /// ended it has come up, so its release is kept as its press was; the keyboard's while edit mode
    /// is on.
    /// </summary>
    public void Release()
    {
        gesture.Following = false;
        Settle();
    }

    public void Dispose()
    {
        gesture.Following = false;
        gesture.Editing = false;
        Unhook();
    }

    /// <summary>Whether the keyboard hook is in (for a test).</summary>
    internal bool HearsKeys => keys != HHOOK.NULL;

    /// <summary>
    /// A low-level mouse message at <paramref name="at"/>: true where it is kept from what is under
    /// the pointer. What it means is posted, never acted on here.
    /// </summary>
    internal bool HearMouse(uint message, POINT at)
    {
        if (message is WM.WM_LBUTTONDOWN or WM.WM_RBUTTONDOWN && !InputDesktop.Holds())
        {
            return false;
        }
        var (keep, input) = gesture.Mouse(message);
        Tell(input, at);
        return keep;
    }

    /// <summary>
    /// A low-level key message, <paramref name="extra"/> its sender's mark (Karpo's own keys carry <see cref="TaskbarKeys.Ours"/>):
    /// true where it is kept from the window with the keys.
    /// </summary>
    internal bool HearKey(uint message, uint key, nuint extra = 0)
    {
        if (message is WM.WM_KEYDOWN or WM.WM_SYSKEYDOWN && !InputDesktop.Holds())
        {
            return false;
        }
        var (keep, input) = gesture.Key(message, key, theirs, KeyState.DownNow(VK.VK_CONTROL), KeyState.DownNow(VK.VK_SHIFT), menu, extra == TaskbarKeys.Ours);
        Tell(input, default);
        return keep;
    }

    /// <summary>Whether a press or key taken is still to come up (for a test).</summary>
    internal bool Owes => gesture.Owes;

    /// <summary>
    /// A widget follows, for a test, without the hooks going in: hooks on a test's thread would take
    /// the person's own clicks.
    /// </summary>
    internal void FollowWithoutHooks() => gesture.Following = true;

    /// <summary>Edit mode is on, for a test, without the keyboard hook going in.</summary>
    internal void EditWithoutHooks(bool on) => gesture.Editing = on;

    private void Tell(FollowInput input, POINT at)
    {
        if (input != FollowInput.None && tell() is var window && window != HWND.NULL)
        {
            _ = PostMessageW(window, WM_KARPO_FOLLOW, (WPARAM)(nuint)(uint)input, Pack(at));
        }
    }

    /// <summary><paramref name="at"/> as a message's lParam: x in the low word, y in the high one, each signed.</summary>
    internal static LPARAM Pack(POINT at) => (LPARAM)(nint)(int)((ushort)(short)at.x | ((uint)(ushort)(short)at.y << 16));

    /// <summary>The point <see cref="Pack"/> made.</summary>
    internal static POINT Unpack(LPARAM packed) => new() { x = (short)((nint)packed & 0xffff), y = (short)(((nint)packed >> 16) & 0xffff) };

    private void HookMouse()
    {
        UnhookMouse();
        armed = this;
        mouse = SetWindowsHookExW(WH.WH_MOUSE_LL, &Pointer, instance, 0);
    }

    private void HookKeys()
    {
        UnhookKeys();
        armed = this;
        keys = SetWindowsHookExW(WH.WH_KEYBOARD_LL, &Keyboard, instance, 0);
    }

    private void UnhookMouse()
    {
        if (mouse != HHOOK.NULL)
        {
            _ = UnhookWindowsHookEx(mouse);
            mouse = HHOOK.NULL;
        }
        gesture.ForgetMouse();
    }

    private void UnhookKeys()
    {
        if (keys != HHOOK.NULL)
        {
            _ = UnhookWindowsHookEx(keys);
            keys = HHOOK.NULL;
        }
        gesture.ForgetKey();
    }

    private void Unhook()
    {
        UnhookMouse();
        UnhookKeys();
        if (armed == this)
        {
            armed = null;
        }
    }

    /// <summary>
    /// What is no longer needed comes out: the mouse hook once nothing follows and no press is owed;
    /// the keyboard's once nothing follows, edit mode is off and no Escape is owed.
    /// </summary>
    private void Settle()
    {
        if (!gesture.Following && !gesture.OwesMouse && mouse != HHOOK.NULL)
        {
            UnhookMouse();
        }
        if (!gesture.Following && !gesture.Editing && !gesture.OwesKey && keys != HHOOK.NULL)
        {
            UnhookKeys();
        }
        if (mouse == HHOOK.NULL && keys == HHOOK.NULL && armed == this)
        {
            armed = null;
        }
    }

    /// <summary>The last release owed came up: what is no longer needed comes out.</summary>
    private void Done() => Settle();

    [UnmanagedCallersOnly]
    private static LRESULT Pointer(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && armed is { } hook)
        {
            try
            {
                var keep = hook.HearMouse((uint)(nuint)wParam, ((MSLLHOOKSTRUCT*)(nint)lParam)->pt);
                hook.Done();
                if (keep)
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

    [UnmanagedCallersOnly]
    private static LRESULT Keyboard(int code, WPARAM wParam, LPARAM lParam)
    {
        if (code >= 0 && armed is { } hook)
        {
            try
            {
                var key = (KBDLLHOOKSTRUCT*)(nint)lParam;
                var keep = hook.HearKey((uint)(nuint)wParam, key->vkCode, key->dwExtraInfo);
                hook.Done();
                if (keep)
                {
                    return (LRESULT)1;
                }
            }
            catch (Exception)
            {
            }
        }
        return CallNextHookEx(HHOOK.NULL, code, wParam, lParam);
    }
}
