// §491: the one way out of Karpo. Everything Karpo hands Windows to open — a program, an app, a
// page in the browser, a mailto: or ms-settings: link, a file or a folder in File Explorer, a
// file's Properties, a jump list's line, a notification's app — goes through here, and nothing
// else in Karpo calls the shell to start anything (LaunchTests.NothingButTheLauncherStartsAnything).
//
// Before it hands anything over, everything of Karpo's that is open closes (the dock's Leave,
// Dock.Leave.cs): the open folder — arranged or not; edit mode itself stays —, a menu, quick
// settings, the window list. Windows' taskbar does the same: what it starts
// comes up over nothing of the taskbar's (the person, 2026-11-02: "as soon as something opens an
// external program like the settings for example or a website or something like that it should
// close the current folder that is open"). Closing first is also what keeps the focus sane: a
// folder that closes gives the keys back to the window it had them from, and only then does the
// launched program, handed the right to come to the front, take them.
//
// Not a launch, so nothing closes for it: Windows' own menus opened by their keys (§466: quick
// settings, the notification center, Win+X, clipboard history, emoji, cast — one at a time by their
// own rule) and the few that open by an address, which come through here all the same
// (IsWindowsPanel: the Wi-Fi list, the pen menu, the virtual touchpad, the touch keyboard); Task view
// and Show desktop; and everything that stays in Karpo — a toggle, media
// transport, a copy to the clipboard, typing, a refresh. A notification's button that runs its app
// in the background (ToastActivation.Closes false) shows nothing, so it closes nothing either.
//
// Off the dock's thread (a Wi-Fi network's dialog, a sign-in's browser, a jump list's activation)
// the closing is posted to the dock, which does it as soon as it can: a folder that closes then
// gives the focus back only where it still has it, so it never takes it from what was opened.
//
// §665: nothing fails in silence (the person, 2026-11-10: "when i click the join button on a meeting nothing
// happens. it should at least show an error message if something does not work"). What does not open is
// written to karpo.log and said to the person (Failed): by the shell's own message box where ShellExecuteEx
// shows one, else by Karpo — Windows' task dialog with the error icon, as a failed export says it
// (TaskDialogs.Tell) — in the caller's words ("Couldn't open the meeting") or its own ("Couldn't open a.txt"),
// with Windows' reason. A launch the person cancelled is no failure.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>What a launch asks of Windows (<see cref="Launched"/>).</summary>
internal enum LaunchKind
{
    /// <summary>A target opened with a verb ("open" when none), a program handed parameters.</summary>
    Open,

    /// <summary>As administrator, as Ctrl+Shift+click starts an app on Windows' taskbar.</summary>
    Elevated,

    /// <summary>Files opened with a program, as dropping them on it in File Explorer does.</summary>
    With,

    /// <summary>Files opened with a store app (by its AUMID).</summary>
    WithApp,

    /// <summary>Windows' own Properties of a file, a folder, a drive.</summary>
    Properties,

    /// <summary>File Explorer at the folder holding the target, with it selected.</summary>
    Select,

    /// <summary>File Explorer at a folder, in a window of its own.</summary>
    Explorer,

    /// <summary>A jump list's line: a target with its arguments and its folder.</summary>
    Jump,

    /// <summary>A packaged app activated with arguments (a jump list's task).</summary>
    Packaged,

    /// <summary>A program of Karpo's own, started and waited for (a Wi-Fi network's dialog).</summary>
    Program,

    /// <summary>A notification's app, brought forward.</summary>
    Toast,

    /// <summary>One of Windows' own menus or panels by its address (§466): handed over, but no launch — nothing closes.</summary>
    Panel,

    /// <summary>§557: a drive ejected with the shell's own Eject — nothing opens, so nothing closes.</summary>
    Eject,

    /// <summary>§655: Windows' own Open with dialog for a file (SHOpenWithDialog), the file opened in the app picked.</summary>
    OpenWith,
}

/// <summary>One launch, as the launcher hands it over: what a stand-in sees in the tests.</summary>
/// <param name="Target">What is opened: a path, a parsing name, a link, a program, an AUMID.</param>
/// <param name="Files">The files a program or an app opens (<see cref="LaunchKind.With"/>, <see cref="LaunchKind.WithApp"/>).</param>
/// <param name="Flags">§655: what Windows' Open with dialog is asked with (OPENASINFO.oaifInFlags, <see cref="LaunchKind.OpenWith"/>); 0 for anything else.</param>
/// <param name="Otherwise">§665: what <see cref="Launch.Aside"/> opens where the target does not: a meeting's page where its app's address does not open.</param>
/// <param name="Failure">§665: what the person is told where it does not open ("Couldn't open the meeting"); null: "Couldn't open" and its name.</param>
internal sealed record Launched(LaunchKind Kind, string Target, string? Verb = null, string? Parameters = null, string? Folder = null, IReadOnlyList<string>? Files = null, uint Flags = 0,
    string? Otherwise = null, string? Failure = null);

/// <summary>§665: a launch that did not open, as the launcher said it (<see cref="Launch.Failed"/>).</summary>
/// <param name="Title">What the person is told failed.</param>
/// <param name="Why">Windows' reason, in its words.</param>
/// <param name="Told">Whether Karpo said it: not where the shell said it already, nor where the person cancelled.</param>
internal sealed record LaunchFailure(Launched Launched, string Title, string Why, bool Told);

/// <summary>
/// tests: Windows stood in for, in this flow of the test only (an AsyncLocal): nothing is opened and
/// nothing of the dock's closes; <see cref="Said"/> has what would have been, in order — "leave",
/// then the launch.
/// </summary>
internal sealed class LaunchStand : IDisposable
{
    private readonly LaunchStand? before;

    internal LaunchStand(LaunchStand? before) => this.before = before;

    /// <summary>"leave", and each launch's <see cref="Launched"/>, as they came.</summary>
    public List<object> Said { get; } = [];

    /// <summary>What the stand-in answers: whether Windows opened it.</summary>
    public bool Opens { get; set; } = true;

    /// <summary>§665: why Windows did not open it, where it does not (<see cref="Opens"/>): a Win32 error; 0 for none said.</summary>
    public int Error { get; set; }

    /// <summary>§665: each launch that did not open, as the launcher said it (<see cref="Launch.Failed"/>), in order.</summary>
    public List<LaunchFailure> Failed { get; } = [];

    public void Dispose() => Launch.Stood = before;
}

public static unsafe class Launch
{
    private static readonly AsyncLocal<LaunchStand?> stood = new();

    /// <summary>tests: the stand-in for this flow, if any.</summary>
    internal static LaunchStand? Stood
    {
        get => stood.Value;
        set => stood.Value = value;
    }

    /// <summary>tests: Windows stood in for until the stand is disposed (<see cref="LaunchStand"/>).</summary>
    internal static LaunchStand Stand() => Stood = new LaunchStand(Stood);

    // The dock's: what closes everything of Karpo's that is open, the thread it runs on and the window
    // a launch off that thread posts to (Dock.Leave.cs).
    private static Action? leave;
    private static int leaveThread;
    private static HWND leaveWindow;

    /// <summary>
    /// Posted to the dock's host by a launch off its thread: everything of Karpo's that is open
    /// closes there (<see cref="Leaves"/>).
    /// </summary>
    internal const uint WM_KARPO_LEAVE = WM.WM_APP + 0x63;

    /// <summary>The dock: <paramref name="closing"/> closes what it has open, on this thread; a launch elsewhere posts to <paramref name="window"/>.</summary>
    internal static void Leaves(Action closing, HWND window)
    {
        leave = closing;
        leaveThread = Environment.CurrentManagedThreadId;
        leaveWindow = window;
    }

    /// <summary>The dock going: nothing closes for a launch any more.</summary>
    internal static void LeavesNot(Action closing)
    {
        if (leave == closing)
        {
            leave = null;
            leaveWindow = HWND.NULL;
        }
    }

    /// <summary>
    /// Everything of Karpo's that is open closes, before anything is handed over: on the dock's thread
    /// at once, from any other once the dock reads its post.
    /// </summary>
    internal static void Leave()
    {
        if (Stood is { } stand)
        {
            stand.Said.Add("leave");
            return;
        }
        if (leave is not { } closing)
        {
            return;
        }
        if (Environment.CurrentManagedThreadId == leaveThread)
        {
            closing();
        }
        else if (leaveWindow != HWND.NULL)
        {
            _ = PostMessageW(leaveWindow, WM_KARPO_LEAVE, 0, 0);
        }
    }

    /// <summary>
    /// The one road: Karpo's popups closed (unless it <paramref name="leaves"/> not), then <paramref name="launched"/> handed to
    /// Windows (or to the stand-in). §665: what does not open is never silent (<see cref="Failed"/>): where the person hears
    /// of it otherwise (<paramref name="says"/>) — ShellExecuteEx's own message box, a caller's own fallback — Karpo writes it
    /// down, else it says it too.
    /// </summary>
    private static bool Go(Launched launched, Func<bool> windows, bool leaves = true, bool says = false)
    {
        if (leaves)
        {
            Leave();
        }
        if (Stood is { } stand)
        {
            stand.Said.Add(launched);
            if (!stand.Opens)
            {
                Failed(launched, stand.Error, says);
            }
            return stand.Opens;
        }
        // Whoever it starts or wakes may come to the front, while Karpo still has the right from the click.
        _ = AllowSetForegroundWindow(ASFW_ANY);
        try
        {
            if (windows())
            {
                return true;
            }
            Failed(launched, Marshal.GetLastSystemError(), says);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Failed(launched, 0, says, error.Message);
        }
        return false;
    }

    /// <summary>
    /// §665: <paramref name="launched"/> did not open, with the Win32 <paramref name="error"/> (0: none said) or the
    /// <paramref name="reason"/> an exception gave. Always written down (karpo.log: its kind and only the host or the file's
    /// name, never a whole address); said to the person in a task dialog of its own (TaskDialogs.Tell, as a failed export
    /// is), on a thread of its own so the dock never waits on it — unless the shell <paramref name="shellSaid"/> it already
    /// or the person cancelled (ERROR_CANCELLED: Windows' own dialog closed, UAC's No).
    /// </summary>
    internal static void Failed(Launched launched, int error, bool shellSaid, string? reason = null)
    {
        var shown = Shown(launched.Target);
        var why = reason ?? (error != 0 ? new System.ComponentModel.Win32Exception(error).Message : shown);
        var title = launched.Failure ?? S.DockLaunchFailed(shown);
        var told = !shellSaid && error != (int)TerraFX.Interop.Windows.ERROR.ERROR_CANCELLED;
        if (Stood is { } stand)
        {
            stand.Failed.Add(new LaunchFailure(launched, title, why, told));
            return;
        }
        Karpo.Core.Troubles.Write($"Launch: {launched.Kind} {shown} did not open (error {error}): {why}"); // not-ui
        if (told)
        {
            Tell(title, why);
        }
    }

    /// <summary>§665: <paramref name="title"/> and <paramref name="why"/> in Windows' task dialog with the error icon, owned by nothing, on a thread of its own.</summary>
    private static void Tell(string title, string why)
    {
        var thread = new Thread(() =>
        {
            try
            {
                TaskDialogs.Tell(HWND.NULL, title, why);
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                Karpo.Core.Troubles.Note("Launch: telling a failure", error); // not-ui
            }
        })
        { IsBackground = true, Name = "Karpo launch failed" }; // not-ui: a thread's name
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    /// <summary>
    /// §665: what a failure names of <paramref name="target"/>: a web address's host, another address's scheme
    /// ("msteams:"), a path's file or folder name — never a whole address, which may carry a meeting's or a person's ids.
    /// </summary>
    internal static string Shown(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) && !uri.IsFile && !uri.IsUnc)
        {
            return uri.Host.Length > 0 ? uri.Host : uri.Scheme + ":";
        }
        var name = Path.GetFileName(target.TrimEnd('\\', '/'));
        return name.Length > 0 ? name : target;
    }

    /// <summary>
    /// Opens <paramref name="target"/> the way Explorer would — a folder in Explorer, a file in its
    /// program, a link in the default browser or whatever has its scheme — with <paramref name="verb"/>
    /// ("open" when null). False where the shell would not: the last error then says why, and the shell's own message box
    /// has said it to the person.
    /// </summary>
    /// <param name="parameters">What a program is handed, as a command's arguments; null for none.</param>
    public static bool Open(string target, string? verb = null, string? parameters = null) =>
        Go(new Launched(IsWindowsPanel(target) ? LaunchKind.Panel : LaunchKind.Open, target, verb, parameters),
            () => Shell.Execute(target, verb ?? "open", parameters), leaves: !IsWindowsPanel(target), says: true);

    /// <summary>
    /// Whether <paramref name="target"/> is one of Windows' own menus or panels that opens by an address — the
    /// Wi-Fi list (ms-availablenetworks:), the pen menu, the virtual touchpad, the touch keyboard's host
    /// (WindowsButtons) —, whose opening is no launch: they come and go one at a time by their own rule
    /// (§466, WindowsMenus), so nothing of Karpo's closes for them. Settings, the Snipping Tool, the Store
    /// are apps, and theirs is.
    /// </summary>
    internal static bool IsWindowsPanel(string target) =>
        target is WindowsButtons.Networks or WindowsButtons.PenMenu or WindowsButtons.VirtualTouchpad
        || string.Equals(Path.GetFileName(target), "TabTip.exe", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Opens <paramref name="target"/> as <see cref="Open"/> does, but off the dock's thread — the shell
    /// may take a moment to find a link's handler (a meeting's Join, an event) — and
    /// <paramref name="otherwise"/> where it does not open: a meeting app's own address, else its page.
    /// What is open of Karpo's closes first, on the dock's thread.
    /// </summary>
    /// <remarks>
    /// §665: the thread ends as soon as the shell returns, so the shell is asked to finish before it does
    /// (SEE_MASK_NOASYNC): Teams' msteams: is a packaged app's protocol, activated through a DelegateExecute handler,
    /// which the shell may complete after ShellExecuteEx has returned — on a thread that has ended, never, and the Join
    /// did nothing with nothing said. The shell shows no box of its own (SEE_MASK_FLAG_NO_UI): the fallback is tried,
    /// and what fails at last is said in <paramref name="failure"/>'s words (<see cref="Failed"/>).
    /// </remarks>
    public static void Aside(string target, string? otherwise = null, string? failure = null)
    {
        var launched = new Launched(LaunchKind.Open, target, Otherwise: otherwise, Failure: failure);
        _ = Go(launched, () =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    var (opened, error) = Tried(target, otherwise, static file => Shell.Execute(file, "open", null, waits: true, quiet: true));
                    if (!opened)
                    {
                        Failed(launched, error, shellSaid: false);
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Failed(launched, 0, shellSaid: false, error.Message);
                }
            })
            { IsBackground = true, Name = "Karpo open aside" }; // not-ui: a thread's name
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return true;
        });
    }

    /// <summary>
    /// §665: <paramref name="target"/> opened with <paramref name="open"/>, else <paramref name="otherwise"/>: whether one
    /// opened, and else the last error, the one that failed last (the page's, where there was one).
    /// </summary>
    internal static (bool Opened, int Error) Tried(string target, string? otherwise, Func<string, bool> open)
    {
        if (open(target))
        {
            return (true, 0);
        }
        var error = Marshal.GetLastSystemError();
        if (otherwise is null)
        {
            return (false, error);
        }
        return open(otherwise) ? (true, 0) : (false, Marshal.GetLastSystemError());
    }

    /// <summary>
    /// <paramref name="target"/> started as administrator, as Ctrl+Shift+click starts an app on
    /// Windows' taskbar; opened as usual where it has no way to run as one (a store app), and nothing
    /// at all when the person said no.
    /// </summary>
    public static void Elevated(string target) =>
        _ = Go(new Launched(LaunchKind.Elevated, target, "runas"), () =>
            Shell.Execute(target, "runas", null)
            || (Marshal.GetLastSystemError() != (int)TerraFX.Interop.Windows.ERROR.ERROR_CANCELLED && Shell.Execute(target, "open", null)), says: true);

    /// <summary>
    /// Opens <paramref name="files"/> with the program <paramref name="app"/> names (a program or a
    /// shortcut to one), as dropping them on it in File Explorer does: the paths quoted, one after another.
    /// </summary>
    public static void With(string app, IEnumerable<string> files)
    {
        var list = files.Where(file => file.Length > 0).ToList();
        var arguments = string.Join(" ", list.Select(file => $"\"{file.Trim('"')}\""));
        _ = Go(new Launched(LaunchKind.With, app, Files: list), () => Shell.Execute(app, "open", arguments.Length > 0 ? arguments : null), says: true);
    }

    /// <summary>
    /// <paramref name="files"/> opened with the store app <paramref name="aumid"/>, as Windows opens a
    /// file with one (IApplicationActivationManager.ActivateForFile). False where it would not take
    /// them — an app that does not open files of that type.
    /// </summary>
    public static bool WithApp(string aumid, IReadOnlyList<string> files) =>
        Go(new Launched(LaunchKind.WithApp, aumid, Files: files), () => Shell.ActivateForFile(aumid, files));

    /// <summary>
    /// Windows' own Properties of <paramref name="target"/> — a file, a folder, a drive — as the row
    /// in File Explorer's menu opens it. The shell runs the window on a thread of its own.
    /// </summary>
    public static void Properties(string target) =>
        _ = Go(new Launched(LaunchKind.Properties, target, "properties"), () => Shell.Execute(target, "properties", null), says: true);

    /// <summary>
    /// §557: the drive at <paramref name="root"/> ("E:\") ejected with the shell's own Eject, the row of its menu in File
    /// Explorer: Windows asks every program with something open on it to let go (the dock among them, DriveHolds),
    /// flushes it, stops the device and says how that went — its "Safe to remove hardware", or why it could not — as it
    /// does for Explorer's. Nothing opens, so nothing of Karpo's closes: a folder showing the removable devices stays,
    /// and the device leaves its line when Windows says it has gone. Off the dock's thread, on one of its own: the eject
    /// waits for every window it tells to answer, the dock's among them, and the dock's thread must be free to.
    /// </summary>
    public static void Eject(string root) =>
        _ = Go(new Launched(LaunchKind.Eject, root, "eject"), () =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    if (!Shell.Execute(root, "eject", null, waits: true))
                    {
                        Karpo.Core.Troubles.Write($"Drives: ejecting {root}: the shell would not (error {Marshal.GetLastSystemError()})"); // not-ui
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Karpo.Core.Troubles.Note($"Drives: ejecting {root}", error); // not-ui
                }
            })
            { IsBackground = true, Name = "Karpo eject" }; // not-ui: a thread's name
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return true;
        }, leaves: false);

    /// <summary>
    /// §655: Windows' own Open with dialog for <paramref name="file"/> (SHOpenWithDialog), as File Explorer's "Open with ›
    /// Choose another app": the apps that open it, "Always use this app" offered, the file opened in the app picked
    /// (<see cref="Shell.OpenWithFlags"/>). The shell's "openas" verb, which this replaced, opens nothing for most files on
    /// Windows 11. The dialog is modal to what owns it, so it is owned by nothing and runs on a thread of its own (an STA,
    /// as the shell's dialogs want): the dock's thread is never held while it stands, and the folder has gone before it shows.
    /// </summary>
    public static void OpenWith(string file) =>
        _ = Go(new Launched(LaunchKind.OpenWith, file, Flags: Shell.OpenWithFlags), () =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    if (!Shell.OpenWith(file, Shell.OpenWithFlags))
                    {
                        Karpo.Core.Troubles.Write($"Open with: {Path.GetFileName(file)}: the shell would not"); // not-ui
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Failed(new Launched(LaunchKind.OpenWith, file, Flags: Shell.OpenWithFlags), 0, shellSaid: false, error.Message);
                }
            })
            { IsBackground = true, Name = "Karpo open with" }; // not-ui: a thread's name
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return true;
        });

    /// <summary>
    /// File Explorer at the folder that holds <paramref name="target"/>, with it selected — the "Open
    /// file location" of Windows' own menus. Nothing happens for a path the shell can't parse.
    /// </summary>
    public static void Select(string target) =>
        _ = Go(new Launched(LaunchKind.Select, target), () => Shell.Select(target));

    /// <summary>
    /// §710: Karpo's own package started again by its id (<paramref name="appId"/>, PackageIdentity.AppUserModelId)
    /// with <paramref name="arguments"/>, for the guard (DockLauncher.ThroughPackage): its HRESULT. Nothing
    /// of the person's is launched and nothing is shown, so it is none of <see cref="Go"/>'s.
    /// </summary>
    internal static int Package(string appId, string arguments) => Shell.ActivateApplication(appId, arguments);

    /// <summary>
    /// File Explorer at <paramref name="path"/>, in a window of its own: a folder in File Explorer
    /// always, never another program that registered itself for folders.
    /// </summary>
    public static void Explorer(string path)
    {
        var windows = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        var explorer = Path.Combine(windows, "explorer.exe");
        var arguments = $"\"{path.TrimEnd('\\')}{(path.EndsWith(":\\", StringComparison.Ordinal) ? "\\" : "")}\"";
        _ = Go(new Launched(LaunchKind.Explorer, path), () => Shell.Execute(explorer, "open", arguments), says: true);
    }

    /// <summary>
    /// A jump list's line: <paramref name="target"/> with its <paramref name="arguments"/>, in its
    /// <paramref name="folder"/>, as clicking it in the taskbar does. Places in the shell namespace
    /// (Control Panel pages, libraries, apps) start from their item; a path works either way, and a
    /// URL or a command line only as text.
    /// </summary>
    public static void Jump(string target, string? arguments, string? folder) =>
        _ = Go(new Launched(LaunchKind.Jump, target, Parameters: arguments, Folder: folder), () => Shell.Jump(target, arguments, folder), says: true);

    /// <summary>
    /// A packaged app activated with <paramref name="arguments"/> (IApplicationActivationManager), as
    /// the taskbar does a jump list's task: the app started, or a running one told; opened by its
    /// AUMID where that fails. Off the dock's thread, which the activation holds until the app has
    /// taken it.
    /// </summary>
    public static void Packaged(string app, string arguments)
    {
        var launched = new Launched(LaunchKind.Packaged, app, Parameters: arguments);
        _ = Go(launched, () =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    // §665: the thread ends with the call, so the shell finishes before it returns; its own box says a failure.
                    if (Shell.ActivateApplication(app, arguments) < 0 && !Shell.Execute($@"shell:AppsFolder\{app}", "open", null, waits: true))
                    {
                        Failed(launched, Marshal.GetLastSystemError(), shellSaid: true);
                    }
                }
                catch (Exception error) when (error is not OutOfMemoryException)
                {
                    Failed(launched, 0, shellSaid: false, error.Message);
                }
            })
            {
                IsBackground = true,
                Name = "Karpo jump list activation", // not-ui: a thread's name
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return true;
        });
    }

    /// <summary>
    /// A program of Karpo's own, started with <paramref name="start"/>'s arguments, for its caller to
    /// wait for (a Wi-Fi network's dialog, WifiProperties). Null where it did not start, and under a
    /// stand-in.
    /// </summary>
    internal static Process? Program(ProcessStartInfo start)
    {
        Process? started = null;
        // §665: its caller opens Windows' own settings where it does not start: the person is not left with nothing.
        _ = Go(new Launched(LaunchKind.Program, start.FileName, Parameters: string.Join(' ', start.ArgumentList)), () =>
        {
            started = Process.Start(start);
            return started is not null;
        }, says: true);
        return started;
    }

    /// <summary>
    /// A notification's activation handed to its app (ToastActivator.Shared). One that brings the app
    /// forward is a launch; one that runs it in the background shows nothing, and closes nothing.
    /// </summary>
    internal static void Toast(ToastActivation activation)
    {
        if (activation.Closes)
        {
            Leave();
        }
        Stood?.Said.Add(new Launched(LaunchKind.Toast, activation.AppId, Parameters: activation.Arguments));
        ToastActivator.Shared.Activate(activation);
    }

    /// <summary>
    /// The shell's own calls, which only the launcher makes — and a notification's roads, which run
    /// off the dock's thread once <see cref="Toast"/> has closed what was open (ToastActivation.cs).
    /// </summary>
    internal static class Shell
    {
        /// <summary>
        /// The verbs that are the item's own menu's and none of the registry's: Properties, and a drive's Eject (§557).
        /// ShellExecuteEx finds them only where it is told to ask the item's menu (SEE_MASK_INVOKEIDLIST); without it,
        /// it looks for them in vain among the registry's and fails with ERROR_NO_ASSOCIATION — all an Eject did until §557.
        /// </summary>
        private static readonly string[] ItemVerbs = ["properties", "eject"];

        /// <summary>
        /// What ShellExecuteEx is asked with for <paramref name="verb"/>: the item's own menu for a verb that is its
        /// menu's (<see cref="ItemVerbs"/>), and, where the caller <paramref name="waits"/> (a thread of its own that ends
        /// with the call), the verb run through before the call returns (SEE_MASK_NOASYNC); §665 where it is
        /// <paramref name="quiet"/> (Launch.Aside, which tries a fallback and says what failed itself), no message box of the
        /// shell's own (SEE_MASK_FLAG_NO_UI). Pure.
        /// </summary>
        internal static uint Mask(string? verb, bool waits = false, bool quiet = false) =>
            (verb is not null && ItemVerbs.Contains(verb, StringComparer.OrdinalIgnoreCase) ? (uint)SEE.SEE_MASK_INVOKEIDLIST : 0)
            | (waits ? (uint)SEE.SEE_MASK_NOASYNC : 0)
            | (quiet ? (uint)SEE.SEE_MASK_FLAG_NO_UI : 0);

        /// <summary>ShellExecuteEx (<paramref name="verb"/> null: the item's default): false where the shell would not, the last error saying why.</summary>
        internal static bool Execute(string file, string? verb, string? parameters, string? folder = null, bool waits = false, bool quiet = false)
        {
            fixed (char* name = file)
            fixed (char* action = verb)
            fixed (char* handed = parameters)
            fixed (char* inside = folder)
            {
                var info = new SHELLEXECUTEINFOW
                {
                    cbSize = (uint)sizeof(SHELLEXECUTEINFOW),
                    fMask = Mask(verb, waits, quiet),
                    lpVerb = action,
                    lpFile = name,
                    lpParameters = parameters is { Length: > 0 } ? handed : null,
                    lpDirectory = folder is { Length: > 0 } ? inside : null,
                    nShow = SW.SW_SHOWNORMAL,
                };
                return ShellExecuteExW(&info);
            }
        }

        /// <summary>
        /// §655: what Windows' Open with dialog is asked with: "Always use this app" offered (OAIF_ALLOW_REGISTRATION) and the
        /// file opened in the app picked (OAIF_EXEC).
        /// </summary>
        internal const uint OpenWithFlags = (uint)(tagOPEN_AS_INFO_FLAGS.OAIF_ALLOW_REGISTRATION | tagOPEN_AS_INFO_FLAGS.OAIF_EXEC);

        /// <summary>§655: Windows' Open with dialog for <paramref name="file"/>, owned by nothing; false where the shell would not show it (or it was cancelled).</summary>
        internal static bool OpenWith(string file, uint flags)
        {
            fixed (char* name = file)
            {
                var info = new OPENASINFO
                {
                    pcszFile = name,
                    pcszClass = null,
                    oaifInFlags = (int)flags,
                };
                return SHOpenWithDialog(HWND.NULL, &info).SUCCEEDED;
            }
        }

        /// <summary>A jump list's line, from its shell item where it is one (see <see cref="Launch.Jump"/>).</summary>
        internal static bool Jump(string target, string? arguments, string? directory)
        {
            ITEMIDLIST* pidl = null;
            try
            {
                fixed (char* file = target)
                fixed (char* parameters = arguments)
                fixed (char* folder = directory)
                {
                    var info = new SHELLEXECUTEINFOW
                    {
                        cbSize = (uint)sizeof(SHELLEXECUTEINFOW),
                        lpFile = file,
                        lpParameters = parameters,
                        lpDirectory = folder,
                        nShow = SW.SW_SHOWNORMAL,
                    };
                    if (arguments is null
                        && !target.Contains("://", StringComparison.Ordinal)
                        && SHParseDisplayName(file, null, &pidl, 0, null).SUCCEEDED)
                    {
                        info.fMask = (uint)SEE.SEE_MASK_INVOKEIDLIST;
                        info.lpIDList = pidl;
                        info.lpFile = null;
                    }
                    return ShellExecuteExW(&info);
                }
            }
            finally
            {
                CoTaskMemFree(pidl);
            }
        }

        /// <summary>SHOpenFolderAndSelectItems on the one item: selected in its parent, opened for it.</summary>
        internal static bool Select(string target)
        {
            ITEMIDLIST* item = null;
            try
            {
                fixed (char* path = target)
                {
                    if (SHParseDisplayName(path, null, &item, 0, null).FAILED || item == null)
                    {
                        return false;
                    }
                }
                return SHOpenFolderAndSelectItems(item, 0, null, 0).SUCCEEDED;
            }
            finally
            {
                CoTaskMemFree(item);
            }
        }

        private static readonly Guid ActivationManagerClass = new("45BA127D-10A8-46EA-8AB7-56EA9078943C");

        /// <summary>A packaged app's own activation with <paramref name="arguments"/>: its HRESULT.</summary>
        internal static int ActivateApplication(string appId, string arguments)
        {
            var classId = ActivationManagerClass;
            IApplicationActivationManager* manager = null;
            HRESULT made = CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, __uuidof<IApplicationActivationManager>(), (void**)&manager);
            if (made.FAILED || manager == null)
            {
                return made.FAILED ? made : E.E_NOINTERFACE;
            }
            try
            {
                uint process;
                fixed (char* app = appId)
                fixed (char* text = arguments)
                {
                    return manager->ActivateApplication(app, arguments.Length > 0 ? text : null, ACTIVATEOPTIONS.AO_NONE, &process);
                }
            }
            finally
            {
                manager->Release();
            }
        }

        /// <summary>IApplicationActivationManager.ActivateForFile: false where the app would not take the files.</summary>
        internal static bool ActivateForFile(string aumid, IReadOnlyList<string> files)
        {
            var pidls = new List<nint>();
            IShellItemArray* array = null;
            IApplicationActivationManager* manager = null;
            try
            {
                foreach (var path in files)
                {
                    ITEMIDLIST* pidl = null;
                    fixed (char* name = path.Trim('"'))
                    {
                        if (SHParseDisplayName(name, null, &pidl, 0, null).SUCCEEDED && pidl != null)
                        {
                            pidls.Add((nint)pidl);
                        }
                    }
                }
                if (pidls.Count == 0)
                {
                    return false;
                }
                var list = stackalloc ITEMIDLIST*[pidls.Count];
                for (var index = 0; index < pidls.Count; index++)
                {
                    list[index] = (ITEMIDLIST*)pidls[index];
                }
                if (SHCreateShellItemArrayFromIDLists((uint)pidls.Count, list, &array).FAILED)
                {
                    return false;
                }
                var classId = ActivationManagerClass;
                if (CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, __uuidof<IApplicationActivationManager>(), (void**)&manager).FAILED || manager == null)
                {
                    return false;
                }
                uint process = 0;
                fixed (char* id = aumid)
                fixed (char* verb = "open")
                {
                    return manager->ActivateForFile(id, array, verb, &process).SUCCEEDED;
                }
            }
            finally
            {
                if (manager != null)
                {
                    _ = manager->Release();
                }
                if (array != null)
                {
                    _ = array->Release();
                }
                foreach (var pidl in pidls)
                {
                    ILFree((ITEMIDLIST*)pidl);
                }
            }
        }
    }
}
