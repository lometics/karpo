// The dock's hidden host window, the one window of the dock that other processes talk to:
// `Karpo.exe --quit`, and a second start of Karpo.exe asking for ⚙ Settings (§340); the dock's own Quit
// Karpo (the edit bar's ⚙ menu) goes the same way. Its class is its own, so FindWindow finds the host and
// never a bar: the bars share a class of their own, and a close sent to a bar quits nothing (the dock's
// Closing.cs).

namespace Karpo.Core;

public static class DockHost
{
    /// <summary>The host's window class. The bars' is "karpo.dock".</summary>
    public const string ClassName = "karpo.host";

    /// <summary>
    /// The registered message a second start of Karpo.exe posts to the host (§340): edit mode on the main
    /// display, ⚙ Settings open (the dock's Dock.EditEntries.cs).
    /// </summary>
    public const string SettingsMessage = "karpo.settings";

    /// <summary>
    /// Who asks the dock to quit, as wParam of the WM_CLOSE posted to the host. A close without one of
    /// these (another program's, or DefWindowProc's for SC_CLOSE) quits nothing. The same value is the
    /// dock's exit code when it quits so asked, the one exit its guard leaves down.
    /// </summary>
    public enum QuitBy : uint
    {
        /// <summary>The settings app's Quit Karpo.</summary>
        Settings = 0x4B510001,

        /// <summary><c>Karpo.exe --quit</c>.</summary>
        Command = 0x4B510002,

        /// <summary>The edit bar's ⚙ Settings menu's Quit Karpo (§335), the dock's own, posted to its host.</summary>
        Menu = 0x4B510003,
    }
}
