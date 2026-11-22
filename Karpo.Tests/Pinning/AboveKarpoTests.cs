// §662: a click on the button of an app run above Karpo — Task Manager, run as administrator while
// Karpo is not — minimizes and restores it as it does every other app's. Windows (UIPI) keeps a
// program from most of what it might do to the windows of one above it: ShowWindow, ShowWindowAsync
// and SetWindowPlacement are refused (ERROR_ACCESS_DENIED), and so is nearly every posted message —
// but not a system command's minimize, restore or close (WM_SYSCOMMAND SC_MINIMIZE, SC_RESTORE,
// SC_CLOSE), which is how Windows' own taskbar reaches them. Karpo used to do exactly the wrong thing
// for them: it never posted to an app above it and did it without asking (ShowWindowAsync), which
// Windows refuses.
//
// The tests cannot run anything as administrator, but UIPI is one rule for any two integrity levels:
// here the window is the test's own, at medium integrity, on a hidden desktop, and the click's calls
// (Apps.Minimize, Apps.Restore) are made by this assembly run as a program at low integrity
// (AboveWorker), below the window as Karpo is below Task Manager. Needs a desktop and a second
// process, so it runs only with KARPO_UI_TESTS=1.

using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Pinning;

public sealed unsafe class AboveKarpoTests
{
    private const string Class = "karpo.tests.app.above";
    private const uint Timeout = 30_000;

    /// <summary>
    /// A maximized window clicked from below it, as Karpo clicks Task Manager's button: minimized, then
    /// restored — maximized again —, twice over.
    /// </summary>
    [UiFact]
    public void AWindowAboveTheClickerMinimizesAndComesBackMaximized() => OnLabelledDesktop((desktop, instance) =>
    {
        var window = Make(instance);
        try
        {
            _ = ShowWindow(window, SW.SW_SHOWMAXIMIZED);
            for (var round = 0; round < 2; round++)
            {
                Assert.Equal(0, FromBelow(desktop, window, AboveWorker.Minimize));
                Pump(() => IsIconic(window));
                Assert.True(IsIconic(window), $"round {round + 1}: not minimized from below");

                Assert.Equal(0, FromBelow(desktop, window, AboveWorker.Restore));
                Pump(() => !IsIconic(window));
                Assert.False(IsIconic(window), $"round {round + 1}: not restored from below");
                Assert.True(IsZoomed(window), $"round {round + 1}: not maximized again");
            }
        }
        finally
        {
            _ = DestroyWindow(window);
        }
    });

    /// <summary>
    /// What the rest of Karpo's handling of an app above it stands on: from below, the window is
    /// read as from anywhere (minimized or not, its placement), and the calls that do it without
    /// asking are refused — so asking is the one way.
    /// </summary>
    [UiFact]
    public void FromBelowTheWindowIsReadButNotMovedWithoutAsking() => OnLabelledDesktop((desktop, instance) =>
    {
        var window = Make(instance);
        try
        {
            _ = ShowWindow(window, SW.SW_SHOWNORMAL);
            Assert.Equal(AboveWorker.Refused, FromBelow(desktop, window, AboveWorker.Without));
            Pump(() => IsIconic(window));
            Assert.False(IsIconic(window));
            Assert.Equal(0, FromBelow(desktop, window, AboveWorker.ReadNormal));
        }
        finally
        {
            _ = DestroyWindow(window);
        }
    });

    private static HWND Make(HINSTANCE instance)
    {
        fixed (char* type = Class)
        fixed (char* title = "Above Karpo test window")
        {
            var window = CreateWindowExW(0, type, title, WS.WS_OVERLAPPEDWINDOW, 0, 0, 400, 300, HWND.NULL, HMENU.NULL, instance, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            return window;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Plain(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);

    /// <summary>This thread's messages handled until <paramref name="done"/>, or two seconds.</summary>
    private static void Pump(Func<bool> done)
    {
        var until = Environment.TickCount64 + 2000;
        while (Environment.TickCount64 < until)
        {
            PumpOnce();
            if (done())
            {
                return;
            }
            Thread.Sleep(5);
        }
    }

    private static void PumpOnce()
    {
        MSG msg;
        while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
        {
            _ = TranslateMessage(&msg);
            _ = DispatchMessageW(&msg);
        }
    }

    /// <summary>
    /// Runs <paramref name="body"/> on a thread of its own on a hidden desktop that a program at low
    /// integrity may run on too (only this user may use it; its label lets low integrity write), which
    /// is closed after.
    /// </summary>
    private static void OnLabelledDesktop(Action<string, HINSTANCE> body)
    {
        var name = "karpo-app-above-" + Guid.NewGuid().ToString("N");
        HDESK hidden = default;
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            void* descriptor = null;
            try
            {
                var user = WindowsIdentity.GetCurrent().User!.Value;
                fixed (char* sddl = $"D:(A;;GA;;;{user})S:(ML;;NW;;;LW)")
                {
                    Assert.True(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, &descriptor, null), "no descriptor");
                }
                var attributes = new SECURITY_ATTRIBUTES { nLength = (uint)sizeof(SECURITY_ATTRIBUTES), lpSecurityDescriptor = descriptor };
                fixed (char* chars = name)
                {
                    hidden = CreateDesktopW(chars, null, null, 0, 0x10000000u /* GENERIC_ALL */, &attributes);
                }
                Assert.True(hidden != HDESK.NULL, $"no hidden desktop: {Marshal.GetLastSystemError()}");
                Assert.True(SetThreadDesktop(hidden));
                var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
                fixed (char* type = Class)
                {
                    var register = new WNDCLASSW { lpfnWndProc = &Plain, hInstance = instance, lpszClassName = type };
                    _ = RegisterClassW(&register);
                }
                body(name, instance);
            }
            catch (Exception error)
            {
                failed = error;
            }
            finally
            {
                _ = LocalFree((HLOCAL)descriptor);
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(hidden);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the hidden desktop: " + failed);
        }
    }

    /// <summary>
    /// This assembly run as a program at low integrity on <paramref name="desktop"/>, doing
    /// <paramref name="what"/> to <paramref name="window"/>; this thread's messages handled meanwhile
    /// (the window is its). What it ended with.
    /// </summary>
    private static int FromBelow(string desktop, HWND window, string what)
    {
        var low = LowToken();
        // Its temporary folder where low integrity may write (the module initializer makes one).
        var temp = Path.Combine(LocalLow(), "karpo-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        PROCESS_INFORMATION started = default;
        try
        {
            var line = $"\"{DotNet()}\" exec \"{typeof(AboveWorker).Assembly.Location}\" {AboveWorker.Switch} {(nint)window:X} {what}";
            var environment = EnvironmentBlock(temp);
            var where = "WinSta0\\" + desktop;
            fixed (char* command = line)
            fixed (char* on = where)
            fixed (char* block = environment)
            {
                var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW), lpDesktop = on };
                Assert.True(
                    CreateProcessAsUserW(low, null, command, null, null, BOOL.FALSE, 0x08000000u /* CREATE_NO_WINDOW */ | 0x400u /* CREATE_UNICODE_ENVIRONMENT */, block, null, &startup, &started),
                    $"CreateProcessAsUserW failed: {Marshal.GetLastSystemError()}");
            }
            var process = started.hProcess;
            var until = Environment.TickCount64 + Timeout;
            while (true)
            {
                var woke = MsgWaitForMultipleObjects(1, &process, BOOL.FALSE, 100, QS.QS_ALLINPUT);
                PumpOnce();
                if (woke == WAIT.WAIT_OBJECT_0)
                {
                    break;
                }
                if (Environment.TickCount64 > until)
                {
                    _ = TerminateProcess(process, 1);
                    throw new Xunit.Sdk.XunitException("The worker below did not finish in time.");
                }
            }
            uint code;
            Assert.True(GetExitCodeProcess(process, &code));
            return (int)code;
        }
        finally
        {
            if (started.hProcess != HANDLE.NULL)
            {
                _ = CloseHandle(started.hProcess);
                _ = CloseHandle(started.hThread);
            }
            _ = CloseHandle(low);
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>This process's token, made again at low integrity (Microsoft's way to start a program at low integrity).</summary>
    private static HANDLE LowToken()
    {
        HANDLE own, low;
        Assert.True(OpenProcessToken(GetCurrentProcess(), TOKEN.TOKEN_DUPLICATE | TOKEN.TOKEN_QUERY | TOKEN.TOKEN_ADJUST_DEFAULT | TOKEN.TOKEN_ASSIGN_PRIMARY, &own));
        try
        {
            Assert.True(DuplicateTokenEx(own, 0x02000000u /* MAXIMUM_ALLOWED */, null, SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, TOKEN_TYPE.TokenPrimary, &low));
        }
        finally
        {
            _ = CloseHandle(own);
        }
        void* sid = null;
        fixed (char* text = "S-1-16-4096")
        {
            Assert.True(ConvertStringSidToSidW(text, &sid));
        }
        try
        {
            var label = new TOKEN_MANDATORY_LABEL { Label = new SID_AND_ATTRIBUTES { Sid = sid, Attributes = 0x20 /* SE_GROUP_INTEGRITY */ } };
            Assert.True(SetTokenInformation(low, TOKEN_INFORMATION_CLASS.TokenIntegrityLevel, &label, (uint)sizeof(TOKEN_MANDATORY_LABEL) + GetLengthSid(sid)),
                $"SetTokenInformation failed: {Marshal.GetLastSystemError()}");
        }
        finally
        {
            _ = LocalFree((HLOCAL)sid);
        }
        return low;
    }

    /// <summary>This process's environment, its temporary folder <paramref name="temp"/>, as a block for CreateProcess.</summary>
    private static string EnvironmentBlock(string temp)
    {
        var variables = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => (string?)entry.Value ?? "", StringComparer.OrdinalIgnoreCase);
        variables["TMP"] = temp;
        variables["TEMP"] = temp;
        var block = new StringBuilder();
        foreach (var (key, value) in variables.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(key).Append('=').Append(value).Append('\0');
        }
        return block.Append('\0').ToString();
    }

    private static string LocalLow()
    {
        char* path = null;
        // FOLDERID_LocalAppDataLow.
        var id = new Guid("A520A1A4-1780-4FF6-BD18-167343C5AF16");
        Assert.True(SHGetKnownFolderPath(&id, 0, HANDLE.NULL, &path).SUCCEEDED);
        try
        {
            return new string(path);
        }
        finally
        {
            CoTaskMemFree(path);
        }
    }

    /// <summary>The dotnet host, as the stress test finds it.</summary>
    private static string DotNet()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host && File.Exists(host))
        {
            return host;
        }
        var runtime = RuntimeEnvironment.GetRuntimeDirectory();
        return Path.GetFullPath(Path.Combine(runtime, "..", "..", "..", "dotnet.exe"));
    }
}

/// <summary>
/// <see cref="AboveKarpoTests"/>' program below the window: <c>--above-click &lt;window, hex&gt; &lt;what&gt;</c>.
/// Ends with 0 where it did what it was asked, <see cref="NotLow"/> where it does not run at low
/// integrity (then it proves nothing), <see cref="Refused"/> where Windows refused it.
/// </summary>
internal static unsafe class AboveWorker
{
    public const string Switch = "--above-click";

    /// <summary>The click's minimize (Apps.Minimize).</summary>
    public const string Minimize = "minimize";

    /// <summary>The click's restore (Apps.Restore).</summary>
    public const string Restore = "restore";

    /// <summary>Minimized without asking (ShowWindowAsync): what Karpo used to do to an app above it.</summary>
    public const string Without = "without";

    /// <summary>Read: shown, not minimized, its placement readable.</summary>
    public const string ReadNormal = "read-normal";

    public const int NotLow = 2;
    public const int Refused = 3;
    public const int Wrong = 4;

    public static int Run(string[] args)
    {
        try
        {
            Directory.Delete(Isolation.Folder);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
        }
        if (ClipboardPaste.IntegrityOf((uint)Environment.ProcessId) != 0x1000)
        {
            return NotLow;
        }
        var window = (HWND)(nint)Convert.ToInt64(args[0], 16);
        switch (args[1])
        {
            case Minimize:
                Apps.Minimize(window);
                return 0;
            case Restore:
                Apps.Restore(window);
                return 0;
            case Without:
                return ShowWindowAsync(window, SW.SW_MINIMIZE) ? 0 : Marshal.GetLastSystemError() == 5 /* ERROR_ACCESS_DENIED */ ? Refused : Wrong;
            case ReadNormal:
                var placement = new WINDOWPLACEMENT { length = (uint)sizeof(WINDOWPLACEMENT) };
                return !IsIconic(window) && IsWindowVisible(window) && GetWindowPlacement(window, &placement) && placement.showCmd == SW.SW_SHOWNORMAL ? 0 : Wrong;
            default:
                return Wrong;
        }
    }
}
