// Karpo's band between a real app and a stand-in for Explorer, end to end (Karpo.Dock\Taskband.cs).
//
// On a desktop of its own, where nothing of the person's can see or be seen: a second process plays
// Explorer (its own Shell_TrayWnd, whose TaskbandHWND is a window that writes down everything it
// gets), this test puts a Shell_TrayWnd of Karpo's in front of it carrying Karpo's band, and a third
// process plays an app calling ITaskbarList3 through Windows' own taskbar list. What the band kept
// must be what the app said; what reached "Explorer" must be every call, in the shape Explorer
// expects — the data atoms made again for its process, readable there, and let go afterwards so its
// process is left holding nothing.
//
// Needs windows and three processes, so it runs only with KARPO_UI_TESTS=1.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Signals;

public sealed unsafe class TaskbandRoundTripTests
{
    private const uint Timeout = 30_000;

    [UiFact]
    public void EverythingTheAppSaysIsKeptAndReachesExplorerAsExplorerTakesIt()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "band-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var name = "karpo-band-" + Guid.NewGuid().ToString("N");
        var desktop = HDESK.NULL;
        fixed (char* desktopName = name)
        {
            desktop = CreateDesktopW(desktopName, null, null, 0, 0x10000000u /* GENERIC_ALL */, null);
        }
        Assert.True(desktop != HDESK.NULL, $"CreateDesktopW failed: {Marshal.GetLastSystemError()}");
        var signals = new AppSignals();
        nint app = 0;
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                app = RoundTrip(desktop, name, folder, signals);
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        HiddenDesktop.Close(desktop);
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the band's desktop: " + failed);
        }

        // What the band kept of it: the progress, the overlay and its description.
        var kept = signals.For([app]);
        Assert.Equal(ProgressKind.Paused, kept.Progress);
        Assert.Equal(0.3f, kept.Value, 3);
        Assert.NotNull(kept.Overlay);
        Assert.Equal("3 unread messages", kept.OverlayText);

        // And what reached "Explorer", line by line (BandWorker.Explorer writes them).
        var heard = File.ReadAllLines(Path.Combine(folder, "explorer.log"));
        string Line(uint msg, string how) =>
            heard.FirstOrDefault(line => line.StartsWith($"{msg - (uint)WM.WM_USER} {how} ", StringComparison.Ordinal))
            ?? throw new Xunit.Sdk.XunitException($"WM_USER+{msg - (uint)WM.WM_USER} never arrived {how}:\n{string.Join('\n', heard)}");
        Assert.Contains($"w={app:X} l=2", Line(BandMessage.ProgressState, "posted"));
        Assert.Contains($"w={app:X} l=4CCC", Line(BandMessage.ProgressValue, "posted"));
        Assert.Contains("icon ok", Line(BandMessage.OverlayIcon, "sent"));
        Assert.Contains("text \"3 unread messages\"", Line(BandMessage.OverlayText, "sent"));
        Assert.Contains("text \"Tooltip text\"", Line(BandMessage.ThumbnailTooltip, "sent"));
        // The RECT, as its bytes: 10, 20, 110, 220.
        Assert.Contains("data 0A000000140000006E000000DC000000", Line(BandMessage.ThumbnailClip, "sent"));
        // A count of two buttons, then the first button's mask (bitmap, tooltip, flags) and id 101.
        Assert.Contains("data 020000000D00000065000000", Line(BandMessage.ThumbBarAdd, "sent"));
        // A tooltip taken away comes without data, sent without waiting, and goes on as it came.
        Assert.Contains($"w={app:X} l=0", Line(BandMessage.ThumbnailTooltip, "notified"));
        // AddTab: the shell hook message itself.
        Assert.Contains(heard, line => line.StartsWith("shellhook ", StringComparison.Ordinal) && line.Contains($"w=1 l={app:X}"));
        // Every atom "Explorer" was handed named memory made for its own process...
        Assert.DoesNotContain(heard, line => line.Contains("not for this process", StringComparison.Ordinal));
        // ...and every handle to it was closed again once the call was answered.
        Assert.Contains("left open 0", heard);
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>On a thread on the desktop: Explorer, Karpo's band and tray, then the app. The app's window.</summary>
    private static nint RoundTrip(HDESK desktop, string name, string folder, AppSignals signals)
    {
        Assert.True(SetThreadDesktop(desktop), $"SetThreadDesktop failed: {Marshal.GetLastSystemError()}");
        var ready = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\" + name + "-ready");
        var stop = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\" + name + "-stop");
        var explorer = Start(name, BandWorker.ExplorerSwitch, Path.Combine(folder, "explorer.log"), name);
        HWND sink = default, spy = default;
        Taskband? band = null;
        try
        {
            Assert.True(ready.WaitOne(TimeSpan.FromMilliseconds(Timeout)), "the stand-in for Explorer never came up");
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            sink = Window("karpo.tests.band.sink", HWND.NULL, instance);
            band = Taskband.Start(sink, instance, signals);
            Assert.NotNull(band);
            // Karpo's tray window: first of its class, carrying Karpo's band.
            spy = Window("Shell_TrayWnd", HWND.HWND_TOPMOST, instance);
            fixed (char* prop = "TaskbandHWND")
            {
                Assert.True(SetPropW(spy, prop, (HANDLE)band!.Hwnd.Value));
            }
            var app = Start(name, BandWorker.AppSwitch, Path.Combine(folder, "app.log"), "");
            try
            {
                Pump(app.hProcess);
            }
            finally
            {
                CloseHandle(app.hProcess);
                CloseHandle(app.hThread);
            }
            var said = File.ReadAllLines(Path.Combine(folder, "app.log"));
            var window = said.FirstOrDefault(line => line.StartsWith("window ", StringComparison.Ordinal))
                ?? throw new Xunit.Sdk.XunitException("The app said nothing:\n" + string.Join('\n', said));
            Assert.DoesNotContain(said, line => line.StartsWith("failed", StringComparison.Ordinal));
            stop.Set();
            Pump(explorer.hProcess);
            return (nint)Convert.ToInt64(window["window ".Length..], 16);
        }
        finally
        {
            stop.Set();
            _ = WaitForSingleObject(explorer.hProcess, Timeout);
            CloseHandle(explorer.hProcess);
            CloseHandle(explorer.hThread);
            band?.Dispose();
            if (spy != HWND.NULL)
            {
                DestroyWindow(spy);
            }
            if (sink != HWND.NULL)
            {
                DestroyWindow(sink);
            }
            ready.Dispose();
            stop.Dispose();
        }
    }

    /// <summary>Handles this thread's messages — the band's among them — until <paramref name="process"/> ends.</summary>
    private static void Pump(HANDLE process)
    {
        var until = Environment.TickCount64 + Timeout;
        while (Environment.TickCount64 < until)
        {
            var woke = MsgWaitForMultipleObjects(1, &process, BOOL.FALSE, 100, QS.QS_ALLINPUT);
            MSG msg;
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
            if (woke == WAIT.WAIT_OBJECT_0)
            {
                return;
            }
        }
        throw new Xunit.Sdk.XunitException("A worker did not finish in time.");
    }

    private static HWND Window(string className, HWND after, HINSTANCE instance)
    {
        fixed (char* type = className)
        {
            var register = new WNDCLASSW { lpfnWndProc = &Plain, hInstance = instance, lpszClassName = type };
            _ = RegisterClassW(&register);
            var window = CreateWindowExW((uint)(WS.WS_EX_TOOLWINDOW | (after == HWND.HWND_TOPMOST ? WS.WS_EX_TOPMOST : 0)), type, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
            Assert.True(window != HWND.NULL, $"CreateWindowExW failed: {Marshal.GetLastSystemError()}");
            return window;
        }
    }

    [UnmanagedCallersOnly]
    private static LRESULT Plain(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);

    /// <summary>This test assembly as a worker, on the desktop <paramref name="desktop"/>.</summary>
    private static PROCESS_INFORMATION Start(string desktop, string role, string log, string events)
    {
        var line = $"\"{DotNet()}\" exec \"{typeof(BandWorker).Assembly.Location}\" {role} \"{log}\" \"{events}\"";
        var where = "WinSta0\\" + desktop;
        PROCESS_INFORMATION started;
        fixed (char* command = line)
        fixed (char* on = where)
        {
            var startup = new STARTUPINFOW { cb = (uint)sizeof(STARTUPINFOW), lpDesktop = on };
            Assert.True(
                CreateProcessW(null, command, null, null, BOOL.FALSE, 0x08000000u, null, null, &startup, &started),
                $"CreateProcessW failed: {Marshal.GetLastSystemError()}");
        }
        return started;
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
/// The two other processes of <see cref="TaskbandRoundTripTests"/>: an app calling ITaskbarList3, and
/// a stand-in for Explorer's taskbar writing down what its band gets.
/// </summary>
internal static unsafe class BandWorker
{
    public const string AppSwitch = "--band-app";
    public const string ExplorerSwitch = "--band-explorer";

    private static StreamWriter log = null!;
    private static uint shellHook;

    /// <summary>Every handle a data atom handed this process, with a copy of this process's own to tell it apart from a later handle of the same value.</summary>
    private static readonly List<(nint Handed, HANDLE Kept)> Handed = [];

    /// <summary>An app: a window, and every kind of call the band reads or passes on with data.</summary>
    public static int App(string[] args)
    {
        using var writer = new StreamWriter(args[0]) { AutoFlush = true };
        try
        {
            _ = CoInitializeEx(null, (uint)COINIT.COINIT_APARTMENTTHREADED);
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            HWND window;
            fixed (char* type = "karpo.tests.band.app")
            fixed (char* title = "Band test app")
            {
                var register = new WNDCLASSW { lpfnWndProc = &Plain, hInstance = instance, lpszClassName = type };
                _ = RegisterClassW(&register);
                window = CreateWindowExW(0, type, title, WS.WS_OVERLAPPEDWINDOW | WS.WS_VISIBLE, 0, 0, 300, 200, HWND.NULL, HMENU.NULL, instance, null);
            }
            writer.WriteLine($"window {(nint)window:X}");
            ITaskbarList4* made = null;
            var id = CLSID.CLSID_TaskbarList;
            Check(writer, "CoCreateInstance", CoCreateInstance(&id, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<ITaskbarList4>(), (void**)&made));
            var list = made;
            Check(writer, "HrInit", list->HrInit());
            Check(writer, "SetProgressState", list->SetProgressState(window, TBPFLAG.TBPF_NORMAL));
            Check(writer, "SetProgressValue", list->SetProgressValue(window, 30, 100));
            Check(writer, "SetProgressState paused", list->SetProgressState(window, TBPFLAG.TBPF_PAUSED));
            var icon = LoadIconW(HINSTANCE.NULL, IDI.IDI_INFORMATION);
            fixed (char* text = "3 unread messages")
            {
                Check(writer, "SetOverlayIcon", list->SetOverlayIcon(window, icon, text));
            }
            fixed (char* tip = "Tooltip text")
            {
                Check(writer, "SetThumbnailTooltip", list->SetThumbnailTooltip(window, tip));
            }
            var clip = new RECT { left = 10, top = 20, right = 110, bottom = 220 };
            Check(writer, "SetThumbnailClip", list->SetThumbnailClip(window, &clip));
            var buttons = stackalloc THUMBBUTTON[2];
            buttons[0] = default;
            buttons[0].dwMask = THUMBBUTTONMASK.THB_BITMAP | THUMBBUTTONMASK.THB_TOOLTIP | THUMBBUTTONMASK.THB_FLAGS;
            buttons[0].iId = 101;
            "Previous".AsSpan().CopyTo(buttons[0].szTip);
            buttons[1] = default;
            buttons[1].dwMask = THUMBBUTTONMASK.THB_ICON | THUMBBUTTONMASK.THB_FLAGS;
            buttons[1].iId = 102;
            buttons[1].hIcon = icon;
            Check(writer, "ThumbBarAddButtons", list->ThumbBarAddButtons(window, 2, buttons));
            // The tooltip taken away again: that comes without data, sent without waiting.
            Check(writer, "SetThumbnailTooltip none", list->SetThumbnailTooltip(window, null));
            Check(writer, "AddTab", list->AddTab(window));
            list->Release();
            // Hold the window until the band has had everything: the last call was sent and answered.
            DestroyWindow(window);
            return 0;
        }
        catch (Exception error)
        {
            writer.WriteLine("failed " + error);
            return 1;
        }
    }

    private static void Check(StreamWriter writer, string what, HRESULT result)
    {
        writer.WriteLine($"{what} {result.Value:X8}");
        if (result.FAILED)
        {
            writer.WriteLine($"failed {what}");
        }
    }

    /// <summary>
    /// Explorer's taskbar, as far as the band is concerned: a Shell_TrayWnd whose TaskbandHWND writes
    /// down each message it gets, reading every data atom the way Explorer must — as memory made for
    /// its own process — until told to stop.
    /// </summary>
    public static int Explorer(string[] args)
    {
        using var writer = new StreamWriter(args[0]) { AutoFlush = true };
        log = writer;
        var name = args[1];
        using var ready = EventWaitHandle.OpenExisting("Local\\" + name + "-ready");
        using var stop = EventWaitHandle.OpenExisting("Local\\" + name + "-stop");
        var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
        fixed (char* hook = "SHELLHOOK")
        {
            shellHook = RegisterWindowMessageW(hook);
        }
        HWND tray, band;
        fixed (char* trayType = "Shell_TrayWnd")
        fixed (char* bandType = "MSTaskSwWClass")
        {
            var one = new WNDCLASSW { lpfnWndProc = &Plain, hInstance = instance, lpszClassName = trayType };
            _ = RegisterClassW(&one);
            var two = new WNDCLASSW { lpfnWndProc = &Heard, hInstance = instance, lpszClassName = bandType };
            _ = RegisterClassW(&two);
            tray = CreateWindowExW((uint)WS.WS_EX_TOOLWINDOW, trayType, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
            band = CreateWindowExW(0, bandType, null, WS.WS_POPUP, 0, 0, 0, 0, HWND.NULL, HMENU.NULL, instance, null);
        }
        fixed (char* prop = "TaskbandHWND")
        {
            SetPropW(tray, prop, (HANDLE)band.Value);
        }
        ready.Set();
        var until = Environment.TickCount64 + 60_000;
        var waited = (HANDLE)stop.SafeWaitHandle.DangerousGetHandle();
        while (Environment.TickCount64 < until)
        {
            var woke = MsgWaitForMultipleObjects(1, &waited, BOOL.FALSE, 100, QS.QS_ALLINPUT);
            MSG msg;
            while (PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE))
            {
                TranslateMessage(&msg);
                DispatchMessageW(&msg);
            }
            if (woke == WAIT.WAIT_OBJECT_0)
            {
                break;
            }
        }
        writer.WriteLine($"left open {Handed.Count(pair => Open(pair.Handed, pair.Kept))}");
        DestroyWindow(band);
        DestroyWindow(tray);
        return 0;
    }

    /// <summary>Whether the handle handed over still names the section it named then (not merely a handle of that value).</summary>
    private static bool Open(nint handed, HANDLE kept)
    {
        uint flags;
        return GetHandleInformation((HANDLE)handed, &flags) && CompareObjectHandles((HANDLE)handed, kept);
    }

    [DllImport("kernelbase.dll", ExactSpelling = true)]
    private static extern BOOL CompareObjectHandles(HANDLE first, HANDLE second);

    [UnmanagedCallersOnly]
    private static LRESULT Heard(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            var how = InSendMessageEx(null);
            var call = (how & 0x2) != 0 ? "notified" : (how & 0x1) != 0 ? "sent" : "posted";
            if (msg == shellHook)
            {
                log.WriteLine($"shellhook {call} w={(nuint)wParam:X} l={(nint)lParam:X}");
                return 0;
            }
            if (msg is < WM.WM_USER or >= WM.WM_APP)
            {
                return DefWindowProcW(hwnd, msg, wParam, lParam);
            }
            var line = new StringBuilder($"{msg - WM.WM_USER} {call} w={(nuint)wParam:X} l={(nint)lParam:X}");
            if (msg == BandMessage.OverlayIcon && (nint)lParam != 0)
            {
                ICONINFO info;
                if (GetIconInfo((HICON)(nint)lParam, &info))
                {
                    line.Append(" icon ok");
                    DeleteObject((HGDIOBJ)info.hbmColor.Value);
                    DeleteObject((HGDIOBJ)info.hbmMask.Value);
                }
            }
            if (call == "sent" && (nint)lParam is >= 0xC000 and <= 0xFFFF)
            {
                var name = stackalloc char[64];
                var length = GlobalGetAtomNameW((ushort)(nint)lParam, name, 64);
                if (DataAtom.TryDecode(new ReadOnlySpan<char>(name, (int)length), out var handle, out var process))
                {
                    if (process != GetCurrentProcessId())
                    {
                        line.Append(" not for this process");
                    }
                    else
                    {
                        HANDLE kept;
                        _ = DuplicateHandle(GetCurrentProcess(), (HANDLE)handle, GetCurrentProcess(), &kept, 0, BOOL.FALSE, DUPLICATE_SAME_ACCESS);
                        Handed.Add((handle, kept));
                        Marshal.SetLastSystemError(0);
                        var view = MapViewOfFile((HANDLE)handle, FILE.FILE_MAP_READ, 0, 0, 0);
                        if (view == null)
                        {
                            uint flags;
                            line.Append($" unmappable {Marshal.GetLastSystemError()} handle {handle:X} valid {(bool)GetHandleInformation((HANDLE)handle, &flags)}");
                        }
                        else
                        {
                            MEMORY_BASIC_INFORMATION region;
                            VirtualQuery(view, &region, (nuint)sizeof(MEMORY_BASIC_INFORMATION));
                            var data = BandData.Data(new ReadOnlySpan<byte>(view, (int)Math.Min((long)(nuint)region.RegionSize, 1 << 20)));
                            UnmapViewOfFile(view);
                            line.Append(data is null ? " unreadable" : $" data {Convert.ToHexString(data)} text \"{BandData.Text(data)}\"");
                        }
                    }
                }
            }
            log.WriteLine(line.ToString());
        }
        catch (Exception error)
        {
            log.WriteLine("error " + error.Message);
        }
        return 0;
    }

    [UnmanagedCallersOnly]
    private static LRESULT Plain(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam) => DefWindowProcW(hwnd, msg, wParam, lParam);
}
