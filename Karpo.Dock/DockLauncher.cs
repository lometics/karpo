// How the guard starts a dock again: outside every job the guard is in.
//
// A job is ended whole by whoever holds it (a terminal, a debugger, a test runner, the app that
// started the first dock), and a dock started inside the guard's job would go with it, taking its
// own guard along, and then nobody would put the taskbar back. Two ways out, tried in order:
//
//   1. CreateProcessW with CREATE_BREAKAWAY_FROM_JOB, suspended. A job that refuses breaking away
//      fails the call (ERROR_ACCESS_DENIED). With jobs inside jobs, breaking away leaves only the
//      ones that allow it, so the call can also succeed with the new process still inside an outer
//      job (measured 2026-09-27: a job that allowed it, nested in the Claude app's, which doesn't).
//      So before it runs, the process is asked whether it is in any job at all; if it is, it is
//      ended before its first instruction and the second way is taken. A dock that Windows or
//      Explorer started sits in the Program Compatibility Assistant's job, which allows breaking
//      away, and its guard broke away from it (Dock.StartGuard): there this is a plain start.
//   2. Explorer: the desktop's Shell.Application (IShellDispatch2::ShellExecute), which starts the
//      program as Explorer's own child: in none of the guard's jobs (Windows puts it in the Program
//      Compatibility Assistant's, which only allows breaking away and never ends anything), and
//      outside any app package the guard runs in. (A program that a packaged app starts, as
//      everything a Claude session starts is, sees that package's copy of %APPDATA% and
//      %LOCALAPPDATA%; one Explorer starts sees the real folders.)

using System.Runtime.InteropServices;
using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;
using IServiceProvider = TerraFX.Interop.Windows.IServiceProvider;

namespace Karpo.Dock;

internal static unsafe class DockLauncher
{
    /// <summary>How a program was started, if it was.</summary>
    internal enum Route
    {
        Direct,
        Shell,
        None,
    }

    /// <summary>How it was started, and the words for karpo.log.</summary>
    internal readonly record struct Launch(Route Route, string Says);

    private const uint CreateSuspended = 0x00000004;

    // CLSID_ShellWindows and SID_STopLevelBrowser (ExDisp.h, ShlGuid.h).
    private static readonly Guid ShellWindowsClass = new("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
    private static readonly Guid TopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");

    /// <summary>Starts <paramref name="exe"/> with <paramref name="arguments"/> in no job: directly where that can be done, else through Explorer.</summary>
    public static Launch Start(string exe, string arguments) =>
        Start(exe, arguments, Direct, PackageIdentity.AppUserModelId is { } app ? (_, asked) => ThroughPackage(app, asked) : ThroughShell);

    /// <summary>The order, with the two ways passed in, so the tests hold it without starting anything.</summary>
    internal static Launch Start(
        string exe,
        string arguments,
        Func<string, string, (bool Done, string Says)> direct,
        Func<string, string, (bool Done, string Says)> shell)
    {
        var first = direct(exe, arguments);
        if (first.Done)
        {
            return new Launch(Route.Direct, first.Says);
        }
        var second = shell(exe, arguments);
        return new Launch(second.Done ? Route.Shell : Route.None, $"{first.Says}; {second.Says}");
    }

    /// <summary>
    /// CreateProcessW, suspended and breaking away from this process's jobs, in the program's own
    /// folder; let run only when it is in no job at all. Nothing is left behind when it isn't.
    /// </summary>
    internal static (bool Done, string Says) Direct(string exe, string arguments)
    {
        var line = $"{Quote(exe)} {arguments}\0".ToCharArray();
        var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW) };
        PROCESS_INFORMATION started;
        BOOL done;
        fixed (char* application = exe)
        fixed (char* command = line)
        fixed (char* folder = Path.GetDirectoryName(exe))
        {
            done = CreateProcessW(
                application,
                command,
                null,
                null,
                BOOL.FALSE,
                (uint)CREATE.CREATE_BREAKAWAY_FROM_JOB | CreateSuspended,
                null,
                folder,
                &startup,
                &started);
        }
        if (!done)
        {
            return (false, $"breaking away from the guard's job was refused (error {Marshal.GetLastSystemError()})");
        }
        try
        {
            BOOL inJob;
            if (!IsProcessInJob(started.hProcess, HANDLE.NULL, &inJob) || inJob)
            {
                // Suspended since it was made: it has run nothing, so ending it undoes it whole.
                TerminateProcess(started.hProcess, 1);
                return (false, "broken away, it would still have been inside a job that refuses breaking away, so it was not let run");
            }
            ResumeThread(started.hThread);
            return (true, $"started as process {started.dwProcessId}, in no job");
        }
        finally
        {
            CloseHandle(started.hThread);
            CloseHandle(started.hProcess);
        }
    }

    /// <summary>
    /// §710: the Store's copy started again as Windows starts it, by its package's id
    /// (<paramref name="app"/>, PackageIdentity.AppUserModelId), in place of Explorer's ShellExecute: the
    /// exe started by its path from outside the package would run without the package, as another
    /// program — the free version, whatever was bought (LicenseBasis.StoreMissing), and with no Store to
    /// ask. Windows starts it in none of the guard's jobs, as Explorer would.
    /// </summary>
    internal static (bool Done, string Says) ThroughPackage(string app, string arguments)
    {
        var com = CoInitializeEx(null, (uint)COINIT.COINIT_APARTMENTTHREADED);
        try
        {
            HRESULT started = Karpo.Dock.Launch.Package(app, arguments);
            return started.SUCCEEDED
                ? (true, $"started by its package ({app})")
                : (false, $"its package did not start it: 0x{(uint)started.Value:X8}");
        }
        finally
        {
            if (com.SUCCEEDED)
            {
                CoUninitialize();
            }
        }
    }

    /// <summary>Explorer's own ShellExecute, the program shown as it normally is.</summary>
    internal static (bool Done, string Says) ThroughShell(string exe, string arguments) =>
        ThroughShell(exe, arguments, SW.SW_SHOWNORMAL);

    /// <summary>
    /// Explorer's own ShellExecute, through the desktop window's Shell.Application: what it starts is
    /// Explorer's child. Explorer doesn't say which process that was.
    /// </summary>
    internal static (bool Done, string Says) ThroughShell(string exe, string arguments, int show)
    {
        var com = CoInitializeEx(null, (uint)COINIT.COINIT_APARTMENTTHREADED);
        IShellWindows* windows = null;
        IDispatch* desktop = null;
        IServiceProvider* services = null;
        IShellBrowser* browser = null;
        IShellView* view = null;
        IDispatch* background = null;
        IShellFolderViewDual* folderView = null;
        IDispatch* application = null;
        IShellDispatch2* shell = null;
        char* file = null;
        VARIANT args = default, folder = default, verb = default, shown = default;
        try
        {
            var classId = ShellWindowsClass;
            var hr = CoCreateInstance(&classId, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, __uuidof<IShellWindows>(), (void**)&windows);
            if (hr.FAILED)
            {
                return (false, $"Explorer's shell windows could not be reached (0x{hr.Value:X8})"); // not-ui: the guard's log
            }
            VARIANT location = default;
            location.vt = (ushort)VARENUM.VT_I4;
            location.lVal = 0; // CSIDL_DESKTOP
            VARIANT root = default;
            int found;
            hr = windows->FindWindowSW(&location, &root, (int)ShellWindowTypeConstants.SWC_DESKTOP, &found, (int)ShellWindowFindWindowOptions.SWFO_NEEDDISPATCH, &desktop);
            if (hr != S_OK || desktop == null)
            {
                return (false, $"Explorer's desktop was not found (0x{hr.Value:X8})"); // not-ui: the guard's log
            }
            var topLevel = TopLevelBrowser;
            if ((hr = desktop->QueryInterface(__uuidof<IServiceProvider>(), (void**)&services)).FAILED
                || (hr = services->QueryService(&topLevel, __uuidof<IShellBrowser>(), (void**)&browser)).FAILED
                || (hr = browser->QueryActiveShellView(&view)).FAILED
                || (hr = view->GetItemObject((uint)SVGIO.SVGIO_BACKGROUND, __uuidof<IDispatch>(), (void**)&background)).FAILED
                || (hr = background->QueryInterface(__uuidof<IShellFolderViewDual>(), (void**)&folderView)).FAILED
                || (hr = folderView->get_Application(&application)).FAILED
                || (hr = application->QueryInterface(__uuidof<IShellDispatch2>(), (void**)&shell)).FAILED)
            {
                return (false, $"Explorer's Shell.Application could not be reached (0x{hr.Value:X8})"); // not-ui: the guard's log
            }
            fixed (char* path = exe)
            fixed (char* line = arguments)
            fixed (char* where = Path.GetDirectoryName(exe) ?? "")
            fixed (char* open = "open")
            {
                file = SysAllocString(path);
                args.vt = (ushort)VARENUM.VT_BSTR;
                args.bstrVal = SysAllocString(line);
                folder.vt = (ushort)VARENUM.VT_BSTR;
                folder.bstrVal = SysAllocString(where);
                verb.vt = (ushort)VARENUM.VT_BSTR;
                verb.bstrVal = SysAllocString(open);
            }
            shown.vt = (ushort)VARENUM.VT_I4;
            shown.lVal = show;
            hr = shell->ShellExecuteW(file, args, folder, verb, shown);
            return hr.SUCCEEDED
                ? (true, "started by Explorer, as its own child")
                : (false, $"Explorer's ShellExecute failed (0x{hr.Value:X8})"); // not-ui: the guard's log
        }
        finally
        {
            if (file != null)
            {
                SysFreeString(file);
            }
            _ = VariantClear(&args);
            _ = VariantClear(&folder);
            _ = VariantClear(&verb);
            ComUtil.Release(ref shell);
            ComUtil.Release(ref application);
            ComUtil.Release(ref folderView);
            ComUtil.Release(ref background);
            ComUtil.Release(ref view);
            ComUtil.Release(ref browser);
            ComUtil.Release(ref services);
            ComUtil.Release(ref desktop);
            ComUtil.Release(ref windows);
            if (com.SUCCEEDED)
            {
                CoUninitialize();
            }
        }
    }

    /// <summary>A command-line argument, in quotes when it needs them.</summary>
    internal static string Quote(string argument) =>
        argument.Length > 0 && !argument.Any(c => char.IsWhiteSpace(c) || c == '"') ? argument : $"\"{argument.Replace("\"", "\\\"")}\"";

    [DllImport("kernel32", ExactSpelling = true)]
    private static extern BOOL IsProcessInJob(HANDLE process, HANDLE job, BOOL* result);
}
