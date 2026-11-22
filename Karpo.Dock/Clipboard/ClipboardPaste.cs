// clipboard: pasting a chosen item where the person was, as Win+V does (the backlog, item 13).
//
// A click on an item in Win+V puts it on the clipboard, gives the focus back to the window that had
// it before the panel opened, and pastes it there. Karpo's clipboard line does the same: the store puts the
// item on the clipboard (ClipboardStore.Choose), the folder's closing gives the focus back to the
// window in front when it opened (FlyoutHost's Menu mode), and the dock then sends Ctrl+V — only
// where that window is in front again, and only where it is safe to:
//
//   * never into Karpo itself: the dock, its settings app, any window of Karpo's process;
//   * never into the shell: the desktop, a taskbar, Start or Search (Apps.ShellClasses);
//   * never into a console — conhost, Windows Terminal and the like (Consoles) — where Ctrl+V is a
//     key of the program running in it as often as a paste (vim's block select, a shell's quoted
//     insert, a terminal whose paste is bound elsewhere), so it would type a ^V rather than paste;
//   * never into a program running at a higher integrity than Karpo's (an elevated one): Windows
//     drops input sent to it without a word (UIPI), so the paste would do nothing and seem done;
//   * never into a password box that says it is one (an edit control with ES_PASSWORD). A browser's
//     or a XAML app's password box does not say so to user32, and reading it through UI Automation
//     would turn on the app's accessibility tree for the sake of one paste; it is not done, so a
//     paste there goes in as Win+V's would;
//   * never where the window has gone, or another window took the front in the meantime.
//
// Anywhere else the item is on the clipboard, as before, and Ctrl+V pastes it by hand.
//
// **Once the focus has settled.** The window given the focus back puts its keyboard focus back on
// its own control as it handles its activation, on its own thread, a moment later: read at once,
// the focus may still be the window itself, or nothing, and the password box it is about to be
// would not be seen. So the dock reads the window again on a short timer (PasteWait) and pastes
// only once two reads in a row find it in front with the same control focused, and the checks
// above hold for that control; a window that never settles within a few hundred milliseconds gets
// nothing but the clipboard.
//
// **A Store app** (Calculator, Settings, any CoreWindow app) is two windows to user32: the frame
// in front (ApplicationFrameWindow, of ApplicationFrameHost's process) and, inside it, the app's
// own Windows.UI.Core.CoreWindow on the app's thread. The frame's thread has no focus of its own,
// so read there the focus is none, every time, and the paste used to give up. Its focus is read on
// the CoreWindow's thread instead; and where that too reads none, the CoreWindow is the focus while
// it or its frame is in front — XAML keeps its controls' focus inside it, and it is the one window
// of the app that ever takes a key (FocusOf). Two reads finding it so is the settled focus.
//
// The facts about the window are read here; what they mean is pure (Judge, PasteWait, Above,
// IsConsole), which the tests read.

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What is known of the window a chosen item would be pasted into.</summary>
/// <param name="Exists">It is still a window.</param>
/// <param name="Ours">Karpo's: of this process, or of Karpo's own exes.</param>
/// <param name="Shell">The desktop, a taskbar, Start or Search.</param>
/// <param name="Password">The control with its keyboard focus is a password box.</param>
/// <param name="InFront">It is the window in front now.</param>
/// <param name="Console">A console's window: conhost's, Windows Terminal's (<see cref="ClipboardPaste.IsConsole"/>).</param>
/// <param name="Higher">Its program runs at a higher integrity than Karpo's, which Windows sends no input to.</param>
/// <param name="Focus">The control with its keyboard focus, 0 for none.</param>
internal readonly record struct PasteTarget(
    bool Exists,
    bool Ours,
    bool Shell,
    bool Password,
    bool InFront,
    bool Console = false,
    bool Higher = false,
    nint Focus = 0);

/// <summary>What the dock does next about a chosen item (<see cref="PasteWait"/>).</summary>
internal enum PasteStep
{
    /// <summary>Ctrl+V, now.</summary>
    Paste,

    /// <summary>Read the window again in a moment: its focus has not settled yet.</summary>
    Wait,

    /// <summary>Nothing more: the item stays on the clipboard alone.</summary>
    Leave,
}

/// <summary>
/// The wait for the window given the focus back to settle (see the top of the file): read on a
/// timer every <see cref="StepMs"/>, it pastes once two reads in a row find the window in front
/// with the same control focused, and gives up after <see cref="MostReads"/>.
/// </summary>
internal sealed class PasteWait
{
    public const uint StepMs = 25;

    public const int MostReads = 12;

    private nint lastFocus;
    private int reads;

    /// <summary>What to do after reading <paramref name="target"/> now.</summary>
    public PasteStep Next(PasteTarget target)
    {
        reads++;
        if (!target.Exists || target.Ours || target.Shell || target.Console || target.Higher)
        {
            // Never, whatever the focus does.
            return PasteStep.Leave;
        }
        var steady = target.InFront && target.Focus != 0 && target.Focus == lastFocus;
        lastFocus = target.InFront ? target.Focus : 0;
        if (!steady)
        {
            return reads < MostReads ? PasteStep.Wait : PasteStep.Leave;
        }
        return ClipboardPaste.Judge(target) ? PasteStep.Paste : PasteStep.Leave;
    }
}

internal static unsafe class ClipboardPaste
{
    private const int ES_PASSWORD = 0x0020;

    /// <summary>A Store app's frame, of ApplicationFrameHost, and the app's own window inside it.</summary>
    private const string FrameClass = "ApplicationFrameWindow";
    private const string CoreWindowClass = "Windows.UI.Core.CoreWindow";
    private const ushort VK_V = 0x56;

    /// <summary>The integrity of an elevated program (SECURITY_MANDATORY_HIGH_RID).</summary>
    internal const uint HighIntegrity = 0x3000;

    /// <summary>Karpo's own programs, by their exe's name: the dock and its settings app.</summary>
    private static readonly string[] OwnExes = ["karpo.exe", "karposettings.exe"];

    /// <summary>
    /// The window classes of consoles, lowercase: conhost's, Windows Terminal's, and the terminals
    /// people run in their place (mintty — Git Bash, Cygwin, MSYS2 —, PuTTY, ConEmu).
    /// </summary>
    private static readonly string[] Consoles =
    [
        "consolewindowclass",
        "cascadia_hosting_window_class",
        "mintty",
        "putty",
        "virtualconsoleclass",
    ];

    /// <summary>Whether a chosen item is pasted into <paramref name="target"/>, or only left on the clipboard.</summary>
    public static bool Judge(PasteTarget target) =>
        target is { Exists: true, Ours: false, Shell: false, Password: false, InFront: true, Console: false, Higher: false };

    /// <summary>Whether a program running <paramref name="exe"/> is one of Karpo's own.</summary>
    internal static bool IsKarpo(string? exe) =>
        exe is { Length: > 0 } && OwnExes.Contains(Path.GetFileName(exe).ToLowerInvariant());

    /// <summary>Whether a window of class <paramref name="className"/> is a console's (<see cref="Consoles"/>).</summary>
    internal static bool IsConsole(string className) => Consoles.Contains(className.ToLowerInvariant());

    /// <summary>
    /// Whether a program at integrity <paramref name="target"/> is above Karpo's <paramref name="own"/>,
    /// so Windows sends it no input of Karpo's. Unread (null) — an elevated program's token is not
    /// Karpo's to read — it counts as above unless Karpo is elevated itself.
    /// </summary>
    internal static bool Above(uint? target, uint own) => target is { } level ? level > own : own < HighIntegrity;

    /// <summary>
    /// On the dock's thread, once the folder has closed and handed the focus back to
    /// <paramref name="back"/>: it is asked to the front once more where it is not, and the dock then
    /// reads it on a timer (<see cref="Read"/>, <see cref="PasteWait"/>) until it pastes or leaves it.
    /// False where nothing is to be pasted there whatever its focus does.
    /// </summary>
    public static bool Begin(HWND back)
    {
        var target = Read(back);
        if (!target.Exists || target.Ours || target.Shell || target.Console || target.Higher)
        {
            return false;
        }
        if (!target.InFront)
        {
            // The folder gave the focus back only where it still had it; ask once more.
            _ = SetForegroundWindow(back);
        }
        return true;
    }

    /// <summary>The facts about <paramref name="window"/>, read now.</summary>
    public static PasteTarget Read(HWND window)
    {
        if (window == HWND.NULL || !IsWindow(window))
        {
            return default;
        }
        uint process = 0;
        var thread = GetWindowThreadProcessId(window, &process);
        var ours = process == 0 || process == GetCurrentProcessId() || IsKarpo(Apps.ImagePath(process));
        var className = ClassOf(window);
        // A Store app's CoreWindow, the one its frame hosts. (A CoreWindow standing on its own is
        // Start's or Search's, and the shell's: never pasted into.)
        var core = string.Equals(className, FrameClass, StringComparison.Ordinal) ? Hosted(window) : HWND.NULL;
        var coreThread = core != HWND.NULL ? GetWindowThreadProcessId(core, null) : 0;
        var foreground = GetForegroundWindow();
        var focus = (HWND)FocusOf(
            window,
            thread != 0 ? Focused(thread) : HWND.NULL,
            core,
            coreThread != 0 ? Focused(coreThread) : HWND.NULL,
            foreground,
            (parent, child) => IsChild((HWND)parent, (HWND)child));
        return new PasteTarget(
            Exists: true,
            Ours: ours,
            Shell: Apps.IsShellClass(className),
            Password: focus != HWND.NULL && IsPassword(focus),
            InFront: foreground == window || (core != HWND.NULL && foreground == core),
            Console: IsConsole(className),
            Higher: !ours && Above(IntegrityOf(process), IntegrityOf(GetCurrentProcessId()) ?? 0x2000),
            Focus: focus);
    }

    /// <summary>
    /// The control with <paramref name="window"/>'s keyboard focus, 0 for none (see the top of the
    /// file): <paramref name="focus"/>, read on the window's thread, where it is a control of this
    /// window — only that counts as its focus; else, for a Store app whose CoreWindow is
    /// <paramref name="core"/> (0 for any other window), <paramref name="coreFocus"/>, read on the
    /// CoreWindow's thread, where it is the CoreWindow or inside it; else the CoreWindow itself while
    /// it or the window is the <paramref name="foreground"/> one. <paramref name="within"/> says
    /// whether its second window is a child of its first.
    /// </summary>
    internal static nint FocusOf(nint window, nint focus, nint core, nint coreFocus, nint foreground, Func<nint, nint, bool> within)
    {
        if (focus != 0 && (focus == window || within(window, focus)))
        {
            return focus;
        }
        if (core == 0)
        {
            return 0;
        }
        if (coreFocus != 0 && (coreFocus == core || within(core, coreFocus)))
        {
            return coreFocus;
        }
        return foreground == window || foreground == core ? core : 0;
    }

    /// <summary>Ctrl+V, pressed and let go, as typed.</summary>
    public static void Send()
    {
        var inputs = stackalloc INPUT[4];
        ushort[] keys = [(ushort)VK.VK_CONTROL, VK_V];
        for (var index = 0; index < 2; index++)
        {
            inputs[index].type = INPUT.INPUT_KEYBOARD;
            inputs[index].ki.wVk = keys[index];
            inputs[3 - index].type = INPUT.INPUT_KEYBOARD;
            inputs[3 - index].ki.wVk = keys[index];
            inputs[3 - index].ki.dwFlags = KEYEVENTF_KEYUP;
        }
        _ = SendInput(4, inputs, sizeof(INPUT));
    }

    /// <summary>The CoreWindow a Store app's <paramref name="frame"/> hosts; null where it hosts none (the app suspended, or gone).</summary>
    private static HWND Hosted(HWND frame)
    {
        fixed (char* name = CoreWindowClass)
        {
            return FindWindowExW(frame, HWND.NULL, name, null);
        }
    }

    /// <summary>The control with the keyboard focus on <paramref name="thread"/>; null where it has none.</summary>
    private static HWND Focused(uint thread)
    {
        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        return GetGUIThreadInfo(thread, &info) ? info.hwndFocus : HWND.NULL;
    }

    /// <summary>Whether <paramref name="control"/> is an edit control's password box.</summary>
    private static bool IsPassword(HWND control) =>
        ClassOf(control).Contains("edit", StringComparison.OrdinalIgnoreCase)
        && (GetWindowLongW(control, GWL.GWL_STYLE) & ES_PASSWORD) != 0;

    /// <summary>The integrity level of process <paramref name="process"/> (its mandatory label's RID); null where its token cannot be read.</summary>
    internal static uint? IntegrityOf(uint process)
    {
        var handle = OpenProcess(PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, false, process);
        if (handle == HANDLE.NULL)
        {
            return null;
        }
        HANDLE token = default;
        try
        {
            if (!OpenProcessToken(handle, TOKEN.TOKEN_QUERY, &token))
            {
                return null;
            }
            uint size = 0;
            _ = GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, null, 0, &size);
            if (size == 0)
            {
                return null;
            }
            var buffer = stackalloc byte[(int)size];
            if (!GetTokenInformation(token, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, buffer, size, &size))
            {
                return null;
            }
            var sid = ((TOKEN_MANDATORY_LABEL*)buffer)->Label.Sid;
            var count = *GetSidSubAuthorityCount(sid);
            return count == 0 ? null : *GetSidSubAuthority(sid, (uint)(count - 1));
        }
        finally
        {
            if (token != HANDLE.NULL)
            {
                _ = CloseHandle(token);
            }
            _ = CloseHandle(handle);
        }
    }

    private static string ClassOf(HWND window)
    {
        var name = stackalloc char[128];
        var length = GetClassNameW(window, name, 128);
        return length > 0 ? new string(name, 0, length) : "";
    }
}
