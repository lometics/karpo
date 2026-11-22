// start: Windows' Start opened on the display of the Start button clicked (the person, 2026-10-28:
// "sometimes when clicking the start button the start just appears no the wrong screen").
//
// Start opens on the display Explorer's launcher of it, IImmersiveLauncher, is connected to. Windows
// connects it to the display of the taskbar clicked, and otherwise keeps it with the foreground window
// (read on 26300: GetMonitor named the foreground's display); Karpo's bars never take the foreground,
// so a click on a bar's Start button opened Start wherever the app in use was. So the launcher is
// connected to the button's display first (ConnectToMonitor, with the shell's own monitor for the
// HMONITOR, IImmersiveMonitorService.GetFromHandle) and asked to open Start there (ShowStartView), as
// Windows' own taskbar on that display does and ExplorerPatcher's "Start on the display with the
// pointer" does from inside Explorer. Nothing else of Start is steered: where on the display it stands
// is Windows' own (Â§26).
//
// Both interfaces are undocumented and reached from Karpo's process through the ImmersiveShell's
// service provider, as DesktopControl reaches the desktops; their proxies are registered
// (PCShellCommonProxyStub.dll, OneCoreUAPCommonProxyStub.dll). Slots are ExplorerPatcher's
// (IImmersiveLauncher10RS): checked on 26300 against the proxy's own table (14 methods, IsVisible at 7
// and GetMonitor at 11 answering as said). They are called only where QueryService hands out exactly
// these IIDs; anywhere else, and wherever a call fails, the Start button asks Explorer's taskbar as
// before (Desktops.OpenStart).

using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

/// <summary>start: the seam to Explorer's launcher of Windows' Start (see the top of the file).</summary>
internal interface IStartLauncher
{
    /// <summary>Connects Start to display <paramref name="monitor"/> (an HMONITOR): it opens there next. False where it could not.</summary>
    bool Connect(nint monitor);

    /// <summary>Opens Start on the display it is connected to. False where it could not.</summary>
    bool Show();
}

/// <summary>
/// start: Explorer's IImmersiveLauncher, opened afresh for every call as DesktopControl opens its
/// manager, so an Explorer that restarted is found again with nothing to drop.
/// </summary>
internal sealed unsafe class StartLauncher : IStartLauncher
{
    public static StartLauncher Instance { get; } = new();

    private static readonly Guid ImmersiveShell = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid ServiceProvider = new("6D5140C1-7436-11CE-8034-00AA006009FA");
    private static readonly Guid LauncherService = new("6F86E01C-C649-4D61-BE23-F1322DDECA9D");
    private static readonly Guid LauncherInterface = new("D8D60399-A0F1-F987-5551-321FD1B49864");
    private static readonly Guid MonitorService = new("47094E3A-0CF2-430F-806F-CF9E4F0F12DD");
    private static readonly Guid MonitorServiceInterface = new("4D4C1E64-E410-4FAA-BAFA-59CA069BFEC2");

    // IImmersiveLauncher10RS, after IUnknown's three: 3 ShowStartView(int how, int flags),
    // 10 ConnectToMonitor(IImmersiveMonitor*).
    private const int SlotShow = 3;
    private const int SlotConnect = 10;

    // IImmersiveMonitorService: 6 GetFromHandle(HMONITOR, IImmersiveMonitor**).
    private const int SlotFromHandle = 6;

    // ShowStartView's way in: Start as the Start button opens it (ExplorerPatcher's).
    private const int ShownFromButton = 11;

    // What was said about the launcher, so the log has it once, not per click.
    private static string? told;

    public bool Connect(nint monitor)
    {
        var service = Open(MonitorService, MonitorServiceInterface);
        if (service == null)
        {
            return false;
        }
        void* immersive = null;
        void* launcher = null;
        try
        {
            var fromHandle = (delegate* unmanaged[Stdcall]<void*, nint, void**, int>)Table(service)[SlotFromHandle];
            if (fromHandle(service, monitor, &immersive) < 0 || immersive == null)
            {
                Tell($"no shell monitor for display {monitor:X}"); // not-ui: a log
                return false;
            }
            launcher = Open(LauncherService, LauncherInterface);
            if (launcher == null)
            {
                return false;
            }
            var connected = ((delegate* unmanaged[Stdcall]<void*, void*, int>)Table(launcher)[SlotConnect])(launcher, immersive);
            if (connected < 0)
            {
                Tell($"ConnectToMonitor failed (0x{connected:X8})"); // not-ui: a log
            }
            return connected >= 0;
        }
        finally
        {
            Release(launcher);
            Release(immersive);
            Release(service);
        }
    }

    public bool Show()
    {
        var launcher = Open(LauncherService, LauncherInterface);
        if (launcher == null)
        {
            return false;
        }
        try
        {
            var shown = ((delegate* unmanaged[Stdcall]<void*, int, int, int>)Table(launcher)[SlotShow])(launcher, ShownFromButton, 0);
            if (shown < 0)
            {
                Tell($"ShowStartView failed (0x{shown:X8})"); // not-ui: a log
            }
            return shown >= 0;
        }
        finally
        {
            Release(launcher);
        }
    }

    /// <summary>One of the ImmersiveShell's services as exactly <paramref name="interfaceId"/>, or null (and why, in the log).</summary>
    private static void* Open(Guid service, Guid interfaceId)
    {
        void* provider = null;
        void* found = null;
        var shell = ImmersiveShell;
        var providerId = ServiceProvider;
        var hr = CoCreateInstance(&shell, null, (uint)CLSCTX.CLSCTX_LOCAL_SERVER, &providerId, &provider);
        if (hr.FAILED || provider == null)
        {
            // Explorer is not running, or restarting.
            Tell($"ImmersiveShell unavailable (0x{hr.Value:X8})"); // not-ui: a log
            return null;
        }
        try
        {
            // IServiceProvider::QueryService, slot 3.
            var query = (delegate* unmanaged[Stdcall]<void*, Guid*, Guid*, void**, int>)Table(provider)[3];
            var answer = query(provider, &service, &interfaceId, &found);
            if (answer < 0 || found == null)
            {
                // Another interface on this build: its slots are unknown, so nothing is called.
                Tell($"service {service:B} as {interfaceId:B} refused (0x{answer:X8})"); // not-ui: a log
                return null;
            }
            return found;
        }
        finally
        {
            Release(provider);
        }
    }

    private static void** Table(void* instance) => *(void***)instance;

    private static void Release(void* value)
    {
        if (value != null)
        {
            ((IUnknown*)value)->Release();
        }
    }

    /// <summary>Logs a state of the launcher once, until it changes.</summary>
    private static void Tell(string message)
    {
        if (told != message)
        {
            told = message;
            Console.Error.WriteLine($"Karpo: Windows' Start: {message}"); // not-ui: a log
        }
    }
}
