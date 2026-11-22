// What closing a window of Karpo's does, and the one way the dock quits.
//
// On 2026-10-22 the dock quit cleanly (exit code 0, so its guard left it down) six seconds before the
// PC went to sleep, and karpo.log said nothing about why. A close reached every window of the dock's
// class as a quit: the settings app's Quit Karpo found "the dock" by class and title, which was the
// primary display's bar as often as the host, so every bar took WM_CLOSE for quit. That made Alt+F4
// on a bar (DefWindowProc turns it into SC_CLOSE, then WM_CLOSE) and any program that closes the
// windows it finds a quit of all of Karpo. Now, as Windows' own taskbar has it:
//
//   the host's WM_CLOSE        quits, when wParam says who asked (DockHost.QuitBy): the settings
//                              app's Quit Karpo, Karpo.exe --quit, the edit bar's ⚙ menu's Quit
//                              Karpo (§335, posted by the dock to its own host); anything else is ignored
//   Alt+F4 on a bar (SC_CLOSE) Windows' Shut Down Windows dialog, as Alt+F4 on Explorer's taskbar
//   a bar's WM_CLOSE           ignored
//   any other window's close   its own handler's (a menu, a folder, quick settings close), and never
//                              on to DefWindowProc, which would destroy the window behind its owner
//
// Each way out is one line in karpo.log (Line), the session's end too, so "why is Karpo not running"
// is answered there; a close that was ignored is a line as well.
//
// The quit is also the dock's exit code (Asked, ExitCode): who asked, DockHost.QuitBy's own value, set
// here and nowhere else, so the guard keeps the bar down only for a quit Karpo asked for. Exit code 0
// is what a dock ended whole from outside leaves (on 2026-10-22 and -23 the app it was started from,
// updating itself, ended every process it had started), and the guard starts that one again.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>Why the dock left by its own way out.</summary>
internal enum QuitWay
{
    /// <summary>The settings app's Quit Karpo.</summary>
    Settings,

    /// <summary><c>Karpo.exe --quit</c>.</summary>
    Command,

    /// <summary>The edit bar's ⚙ Settings menu's Quit Karpo (§335).</summary>
    Menu,

    /// <summary>Windows signing out, restarting or shutting down.</summary>
    SessionEnd,
}

internal static unsafe class Closing
{
    /// <summary>Which of the dock's windows a close came to.</summary>
    internal enum Role
    {
        Host,
        Bar,

        /// <summary>A menu, a folder, a preview, an edit bar: its own handler closes it, or nothing does.</summary>
        Other,
    }

    /// <summary>The system command a close is (SC_CLOSE), as WM_SYSCOMMAND carries it.</summary>
    internal const uint CloseCommand = WindowMenus.CloseCommand;

    /// <summary>
    /// Windows' Shut Down Windows dialog, asked of Explorer. The tests put their own here: no dialog
    /// may open on the person's screen from a test.
    /// </summary>
    internal static Action ShutDownDialog = AskExplorer;

    /// <summary>
    /// Who asked the dock to quit, once the host's marked close has come; null while it runs, and for
    /// a dock that ends any other way. Set only by <see cref="Answer"/>, just before the one way out.
    /// </summary>
    internal static DockHost.QuitBy? Asked { get; set; }

    /// <summary>
    /// The dock's exit code: who asked it to quit, where Karpo did, else <paramref name="otherwise"/>.
    /// The guard takes only these codes for a quit (DockExit.Kind); never 0, which a process ended from
    /// outside can be left with.
    /// </summary>
    public static int ExitCode(int otherwise) => Asked is { } by ? unchecked((int)(uint)by) : otherwise;

    /// <summary>
    /// What a close does to the dock's window <paramref name="role"/>: the answer to return, or null
    /// where it is not a close the dock answers (the window's own handler has it). <paramref name="quit"/>
    /// is called for the host's close from Karpo itself, after its line is written.
    /// </summary>
    public static LRESULT? Answer(Role role, uint msg, WPARAM wParam, Action quit)
    {
        switch (role, msg)
        {
            case (Role.Host, WM.WM_CLOSE):
                if (Way(wParam) is { } way)
                {
                    Troubles.Write(Line(way));
                    Asked = (DockHost.QuitBy)(uint)(nuint)wParam;
                    quit();
                }
                else
                {
                    Troubles.Write("dock: a close sent to the dock's host by another program was ignored"); // not-ui: karpo.log
                }
                return 0;
            case (Role.Bar, WM.WM_CLOSE):
                Troubles.Write("dock: a close sent to a bar by another program was ignored"); // not-ui: karpo.log
                return 0;
            case (Role.Bar, WM.WM_SYSCOMMAND) when IsClose(wParam):
                ShutDownDialog();
                return 0;
            default:
                return null;
        }
    }

    /// <summary>Whether a WM_SYSCOMMAND's wParam is SC_CLOSE (its last four bits are Windows' own).</summary>
    public static bool IsClose(WPARAM wParam) => ((uint)(nuint)wParam & 0xFFF0u) == CloseCommand;

    /// <summary>Who asked, from the host's WM_CLOSE; null for a close that is not Karpo's own.</summary>
    public static QuitWay? Way(WPARAM wParam) =>
        (nuint)wParam == (uint)DockHost.QuitBy.Settings ? QuitWay.Settings
        : (nuint)wParam == (uint)DockHost.QuitBy.Command ? QuitWay.Command
        : (nuint)wParam == (uint)DockHost.QuitBy.Menu ? QuitWay.Menu
        : null;

    /// <summary>The dock's line in karpo.log as it leaves. Pure.</summary>
    public static string Line(QuitWay way) => way switch
    {
        QuitWay.Settings => "dock: quitting: the settings app's Quit Karpo asked", // not-ui: karpo.log
        QuitWay.Command => "dock: quitting: Karpo.exe --quit asked", // not-ui: karpo.log
        QuitWay.Menu => "dock: quitting: the edit bar's Quit Karpo asked", // not-ui: karpo.log
        _ => "dock: Windows is signing out or shutting down: the taskbar is put back, and the dock ends with the session", // not-ui: karpo.log
    };

    /// <summary>
    /// DefWindowProc for every window of Karpo's, but a WM_CLOSE goes no further: DefWindowProc's
    /// answer to it is DestroyWindow, and no window of Karpo's is destroyed by somebody else's close.
    /// </summary>
    public static LRESULT Default(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) =>
        msg == WM.WM_CLOSE ? 0 : DefWindowProcW(hwnd, msg, wParam, lParam);

    /// <summary>
    /// What Alt+F4 on Explorer's taskbar does: SC_CLOSE to Explorer's own taskbar window, which opens
    /// the Shut Down Windows dialog — hidden or not, the window is there while Explorer runs. Posted,
    /// so the dock never waits on the dialog; Explorer may bring it to the front, as the bar that had
    /// the keys could.
    /// </summary>
    private static void AskExplorer()
    {
        var tray = Taskbar.ExplorerTray();
        if (tray == HWND.NULL)
        {
            Troubles.Write("dock: Alt+F4 on a bar: no Explorer to show the Shut Down Windows dialog"); // not-ui: karpo.log
            return;
        }
        uint explorer = 0;
        _ = GetWindowThreadProcessId(tray, &explorer);
        _ = AllowSetForegroundWindow(explorer);
        _ = PostMessageW(tray, WM.WM_SYSCOMMAND, CloseCommand, 0);
    }
}
