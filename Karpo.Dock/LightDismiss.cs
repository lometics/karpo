// What closes Karpo's popups, and what a press that closes one does besides:
// the rules every light-dismissed popup follows, kept apart from the windows so
// they can be tested (Karpo.Tests\Menus).
//
// A popup closes on a press anywhere outside it, Karpo's own bars included (the
// bars never take the activation, so losing it can't tell the popup), on Escape
// and on the Windows key. What the closing press does besides is the taskbar's:
// a press elsewhere on the bar goes on to what it was on — with one app's jump list
// open, a right click on another app's button opens that one's at once, and a click
// on it switches to it; with the calendar open, a click on Wi-Fi opens Wi-Fi — while
// a press on what opened the popup only closes it, but for a right click on what
// opened a flyout of content, which still opens its menu; a popup that opened on
// hover never takes a press.
//
// A menu took every press that closed it until 2026-10-11, so going from one
// widget's menu to the next took two right clicks (the person: "when i have a
// context menu open and i right click onto another widget i have to right click
// twice to open the context menu. is this normal win11 behaviour? if not fix.").
// It isn't: Windows 11's taskbar goes from one jump list to the next in one.

using TerraFX.Interop.Windows;

namespace Karpo.Dock;

/// <summary>What kind of popup a press closed, for <see cref="LightDismiss.Consumes(PopupRole, bool, PressButton)"/>.</summary>
public enum PopupRole
{
    /// <summary>A context menu, a jump list, a block's options, the Start button's quick links.</summary>
    Menu,

    /// <summary>
    /// Content opened by a click: an island's list, the tray's hidden icons, a pin group's members,
    /// the calendar, the notification list, quick settings, a folder.
    /// </summary>
    Flyout,

    /// <summary>Opened by the pointer resting: the window list, the desktop flyout, the media controls.</summary>
    Hover,
}

/// <summary>The mouse button of a press.</summary>
public enum PressButton
{
    Left,
    Middle,
    Right,
}

/// <summary>A popup a press on a bar closed: its <paramref name="Role"/>, and whether the press was on what opened it.</summary>
public readonly record struct ClosedPopup(PopupRole Role, bool OnOpener);

/// <summary>What the mouse hook makes of a button going down while a menu-mode popup is open.</summary>
public enum HookPress
{
    /// <summary>On the popup, or on what counts as part of it (a submenu): the popup handles it.</summary>
    Inside,

    /// <summary>On a bar of the dock's: the bar closes the popup itself and decides what the press does besides.</summary>
    Bar,

    /// <summary>On another window of Karpo's: the popup closes and gives the focus back.</summary>
    Ours,

    /// <summary>On another app or the desktop: the popup closes, and whatever was clicked takes the focus.</summary>
    Elsewhere,
}

public static class LightDismiss
{
    /// <summary>
    /// Whether a press on a bar that closed a popup of <paramref name="role"/> is used up by that,
    /// so that neither the press nor its release does anything else. Only a press on what opened
    /// it: a menu's opener, any button — it closes, and does not open again; a flyout of content's,
    /// a click (left or middle), so a second click there closes it instead of opening it again,
    /// while a right click there still opens its menu; a hover popup's never. A press on anything
    /// else goes on to it, so another widget's menu or flyout opens with the one click.
    /// </summary>
    public static bool Consumes(PopupRole role, bool onOpener, PressButton button) => role switch
    {
        PopupRole.Menu => onOpener,
        PopupRole.Flyout => onOpener && button != PressButton.Right,
        _ => false,
    };

    /// <summary>Whether a press that closed all of <paramref name="closed"/> is used up (any one of them takes it).</summary>
    public static bool Consumes(IEnumerable<ClosedPopup> closed, PressButton button) =>
        closed.Any(popup => Consumes(popup.Role, popup.OnOpener, button));

    /// <summary>
    /// Whether a key closes a popup that has the keys: Escape, and the Windows keys, whose Start
    /// (or whatever the shortcut opens) takes over, as it closes Windows' own menus.
    /// </summary>
    public static bool ClosesOnKey(int virtualKey) => virtualKey is VK.VK_ESCAPE or VK.VK_LWIN or VK.VK_RWIN;

    /// <summary>
    /// §525: whether a key opens a dropdown's popup from its control, and closes it again from the popup, as WinUI's
    /// ComboBox: F4 (not Alt+F4, which closes a window), Alt+Down, Alt+Up (<paramref name="alt"/>: Alt held).
    /// </summary>
    public static bool TogglesDrop(int virtualKey, bool alt) =>
        alt ? virtualKey is VK.VK_DOWN or VK.VK_UP : virtualKey == VK.VK_F4;

    /// <summary>Whether a key message (<paramref name="message"/>, <paramref name="lParam"/>) came with Alt held: a system key's context code.</summary>
    public static bool AltOf(uint message, LPARAM lParam) => message == WM.WM_SYSKEYDOWN && ((nint)lParam & (1 << 29)) != 0;

    /// <summary>The button a press or a release (<paramref name="message"/>) is of: the right's, the middle's, else the left's.</summary>
    public static PressButton ButtonOf(uint message) => message switch
    {
        WM.WM_RBUTTONDOWN or WM.WM_RBUTTONUP => PressButton.Right,
        WM.WM_MBUTTONDOWN or WM.WM_MBUTTONUP => PressButton.Middle,
        _ => PressButton.Left,
    };

    /// <summary>
    /// Whether closing by <paramref name="virtualKey"/> hands the foreground back to the window
    /// that had it before the popup: Escape does; after a Windows key, Start is about to take it.
    /// </summary>
    public static bool RestoresFocus(int virtualKey) => virtualKey == VK.VK_ESCAPE;

    /// <summary>
    /// What a button going down means to an open menu-mode popup's mouse hook: on the popup (or
    /// <paramref name="alsoInside"/>, its submenus), nothing; on a bar, the bar's to decide; on
    /// another window of this process, close and give the focus back; else close. A press the
    /// hook used up for the popup (<paramref name="usedUp"/>, a menu over a folder) reached no
    /// bar and no other window: close and give the focus back, wherever it was.
    /// </summary>
    public static HookPress Classify(bool inside, bool alsoInside, bool onBar, bool ours, bool usedUp = false) =>
        inside || alsoInside ? HookPress.Inside
        : usedUp ? HookPress.Ours
        : onBar ? HookPress.Bar
        : ours ? HookPress.Ours
        : HookPress.Elsewhere;
}

/// <summary>
/// §430: what a press went down on, as the popup it opens remembers it: the window (<paramref name="Window"/>),
/// the thing in it (<paramref name="Part"/>: a bar's hit, an edit bar's button, a folder's line by its widget's key
/// or its "+"; null for the window's room) and the button (<paramref name="Button"/>).
/// </summary>
public readonly record struct PopupOpener(nint Window, object? Part, PressButton Button)
{
    /// <summary>Whether <paramref name="other"/> is on the same thing, whichever button.</summary>
    public bool On(PopupOpener other) => Window == other.Window && Equals(Part, other.Part);
}

/// <summary>
/// §430 (the person, 2026-10-30: "when the menu is already open and i click again it reopens it and flickers. it
/// should just close it / toggle. this should be for all the menus"): every popup remembers what opened it
/// (<see cref="PopupOpener"/>), and one click on that again closes it and opens nothing — whoever closed it (the
/// popups' hook before the press reached its window, or the window itself) and whoever opens what (the bar, the
/// edit bar, a folder's line or "+"). A press on another opener closes it and its click opens its own.
/// <para>
/// The click being handled: what it went down on (<see cref="Press"/>, which what opens meanwhile is stamped with)
/// and the openers of the popups its press closed (<see cref="Shut"/>), from the press until its release is handled.
/// </para>
/// </summary>
public sealed class Openers
{
    private readonly List<PopupOpener> shut = [];

    /// <summary>What the press being handled went down on; null between clicks.</summary>
    public PopupOpener? Press { get; private set; }

    /// <summary>
    /// A button went down on <paramref name="press"/>; <paramref name="closed"/>: the opener of the popup the hook
    /// closed for this very press before it got here (null for none).
    /// </summary>
    public void Pressed(PopupOpener press, PopupOpener? closed)
    {
        Press = press;
        shut.Clear();
        Shut(closed);
    }

    /// <summary>The press closed the popup <paramref name="opener"/> opened (null: one that knows no opener).</summary>
    public void Shut(PopupOpener? opener)
    {
        if (opener is { } closed && Press is not null)
        {
            shut.Add(closed);
        }
    }

    /// <summary>Whether the press closed a popup the thing it went down on had opened, whichever button: it closes it and does nothing else.</summary>
    public bool ClosedItsOwn => Press is { } press && shut.Exists(closed => closed.On(press));

    /// <summary>
    /// Whether a popup <paramref name="opener"/> opens is one the click being handled closed with its press: the
    /// same thing, the same button. It stays closed; it never closes and opens again.
    /// </summary>
    public bool Reopens(PopupOpener? opener) => opener is { } opening && shut.Contains(opening);

    /// <summary>The release was handled (with what it opened): the click is over.</summary>
    public void Released()
    {
        Press = null;
        shut.Clear();
    }
}
