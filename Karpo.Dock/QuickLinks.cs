// The Start button's right-click menu: Windows 11's Quick Link menu (Win+X), as
// one of Karpo's own flyouts, with a Segoe Fluent Icons glyph on every line like
// the rest of Karpo's menus. Windows' own menu can't be given icons, and opened
// for a taskbar that is hidden it didn't close when the bar was clicked.
//
// The lines, their names and their order are Windows'. As there, Mobility Center
// shows only on a PC with a battery, Terminal is Windows PowerShell where Windows
// Terminal isn't installed, and Sleep and Hibernate follow what Windows' power
// menu shows ("Show in Power menu" in the power options).

using System.Runtime.InteropServices;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
// `using static ...Windows` also brings in a region constant named ERROR.
using ERROR = TerraFX.Interop.Windows.ERROR;

namespace Karpo.Dock;

/// <summary>A line of the Start button's quick links, in Windows' order.</summary>
public enum QuickLink
{
    InstalledApps = 1,
    MobilityCenter,
    PowerOptions,
    EventViewer,
    System,
    DeviceManager,
    NetworkConnections,
    DiskManagement,
    ComputerManagement,
    Terminal,
    TerminalAdmin,
    TaskManager,
    Settings,
    FileExplorer,
    Search,
    Run,
    ShutDownOrSignOut,
    SignOut,
    Sleep,
    Hibernate,
    ShutDown,
    Restart,
    Desktop,

    /// <summary>quick: locks the PC. Not a line of Windows' own Win+X menu; the power widget has it.</summary>
    Lock,

    // §641: Start's own, past Win+X's (Of reads none of them as a line of that menu).

    /// <summary>§641: the sign-in screen to switch to another account, this session kept: Start's account menu's "Switch user".</summary>
    SwitchUser,

    /// <summary>§641: Windows' updates installed, then the restart: Start's power menu's while a restart is due.</summary>
    UpdateRestart,

    /// <summary>§641: Windows' updates installed, then the PC off.</summary>
    UpdateShutDown,
}

/// <summary>What this PC offers, which decides some of the quick links.</summary>
/// <param name="Battery">It runs on a battery: Mobility Center is offered.</param>
/// <param name="WindowsTerminal">Windows Terminal is installed; else Windows PowerShell stands in.</param>
/// <param name="Sleep">Sleep is in Windows' power menu.</param>
/// <param name="Hibernate">Hibernate is in Windows' power menu.</param>
public readonly record struct QuickLinkMachine(bool Battery, bool WindowsTerminal, bool Sleep, bool Hibernate)
{
    private const string FlyoutMenuSettings = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\FlyoutMenuSettings";

    /// <summary>What this PC offers now: cheap reads, done as the menu opens.</summary>
    public static unsafe QuickLinkMachine Read()
    {
        SYSTEM_POWER_STATUS status;
        // 128: no system battery; 255: unknown.
        bool battery = GetSystemPowerStatus(&status) && status.BatteryFlag != 128 && status.BatteryFlag != 255
            && File.Exists(Path.Combine(Environment.SystemDirectory, "mblctr.exe"));
        SYSTEM_POWER_CAPABILITIES capabilities;
        bool known = QuickLinks.GetPwrCapabilities(&capabilities) != 0;
        bool sleep = (!known || capabilities.SystemS1 != 0 || capabilities.SystemS2 != 0 || capabilities.SystemS3 != 0 || capabilities.AoAc != 0)
            && QuickLinks.Dword(HKEY.HKEY_LOCAL_MACHINE, FlyoutMenuSettings, "ShowSleepOption") != 0;
        bool hibernate = known && capabilities.HiberFilePresent != 0
            && QuickLinks.Dword(HKEY.HKEY_LOCAL_MACHINE, FlyoutMenuSettings, "ShowHibernateOption") == 1;
        return new QuickLinkMachine(battery, File.Exists(QuickLinks.WindowsTerminal), sleep, hibernate);
    }
}

public static unsafe class QuickLinks
{
    /// <summary>The command id of <see cref="QuickLink"/> 0; a line's id is this plus its value.</summary>
    public const uint FirstId = 7000;

    // Segoe Fluent Icons. Where a line is also a kind of island or a command of
    // the bar's own menus, it has the same glyph there (Sections.Glyph, Dock).
    private const string GlyphInstalledApps = "\uE71D";
    private const string GlyphMobilityCenter = "\uE7F7";
    private const string GlyphPowerOptions = "\uE945";
    private const string GlyphEventViewer = "\uE9F9";
    private const string GlyphSystem = "\uE770";
    private const string GlyphDeviceManager = "\uE772";
    private const string GlyphNetwork = "\uE968";
    private const string GlyphDisks = "\uEDA2";
    private const string GlyphComputerManagement = "\uE912";
    private const string GlyphTerminal = "\uE756";
    private const string GlyphAdmin = "\uE7EF";
    private const string GlyphTaskManager = "\uE9D9";
    private const string GlyphSettings = "\uE713";
    private const string GlyphFileExplorer = "\uEC50";
    private const string GlyphSearch = "\uE721";
    private const string GlyphRun = "\uE768";
    private const string GlyphPower = "\uE7E8";
    private const string GlyphSignOut = "\uF3B1";
    private const string GlyphSleep = "\uE708";
    private const string GlyphHibernate = "\uE78C";
    private const string GlyphRestart = "\uE777";
    private const string GlyphDesktop = "\uE977";
    private const string GlyphLock = "\uE72E";

    /// <summary>Windows Terminal's app execution alias, there when it is installed.</summary>
    internal static string WindowsTerminal => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");

    public static uint IdOf(QuickLink link) => FirstId + (uint)link;

    /// <summary>The line a command id stands for; null for any other id.</summary>
    public static QuickLink? Of(uint id) =>
        id > FirstId && id - FirstId <= (uint)QuickLink.Lock ? (QuickLink)(id - FirstId) : null;

    /// <summary>The menu, as Windows' Win+X menu has it on <paramref name="machine"/>.</summary>
    public static List<FlyoutItem> Items(QuickLinkMachine machine)
    {
        var terminal = machine.WindowsTerminal ? S.DockQuickLinkTerminal : "Windows PowerShell"; // not-ui: the app's own name
        var items = new List<FlyoutItem> { Line(QuickLink.InstalledApps, S.DockQuickLinkInstalledApps, GlyphInstalledApps) };
        if (machine.Battery)
        {
            items.Add(Line(QuickLink.MobilityCenter, S.DockQuickLinkMobilityCenter, GlyphMobilityCenter));
        }
        items.AddRange(
        [
            Line(QuickLink.PowerOptions, S.DockQuickLinkPowerOptions, GlyphPowerOptions),
            Line(QuickLink.EventViewer, S.DockQuickLinkEventViewer, GlyphEventViewer),
            Line(QuickLink.System, S.DockQuickLinkSystem, GlyphSystem),
            Line(QuickLink.DeviceManager, S.DockQuickLinkDeviceManager, GlyphDeviceManager),
            Line(QuickLink.NetworkConnections, S.DockQuickLinkNetworkConnections, GlyphNetwork),
            Line(QuickLink.DiskManagement, S.DockQuickLinkDiskManagement, GlyphDisks),
            Line(QuickLink.ComputerManagement, S.DockQuickLinkComputerManagement, GlyphComputerManagement),
            Line(QuickLink.Terminal, terminal, GlyphTerminal),
            Line(QuickLink.TerminalAdmin, S.DockQuickLinkAdmin(terminal), GlyphAdmin),
            new FlyoutItem(0, "", Separator: true),
            Line(QuickLink.TaskManager, S.DockQuickLinkTaskManager, GlyphTaskManager),
            Line(QuickLink.Settings, S.DockMenuSettings, GlyphSettings),
            Line(QuickLink.FileExplorer, S.DockQuickLinkFileExplorer, GlyphFileExplorer),
            Line(QuickLink.Search, S.DockQuickLinkSearch, GlyphSearch),
            Line(QuickLink.Run, S.DockQuickLinkRun, GlyphRun),
            new FlyoutItem(0, "", Separator: true),
        ]);
        var power = new List<FlyoutItem> { Line(QuickLink.SignOut, S.DockQuickLinkSignOut, GlyphSignOut) };
        if (machine.Sleep)
        {
            power.Add(Line(QuickLink.Sleep, S.DockQuickLinkSleep, GlyphSleep));
        }
        if (machine.Hibernate)
        {
            power.Add(Line(QuickLink.Hibernate, S.DockQuickLinkHibernate, GlyphHibernate));
        }
        power.Add(Line(QuickLink.ShutDown, S.DockQuickLinkShutDown, GlyphPower));
        power.Add(Line(QuickLink.Restart, S.DockQuickLinkRestart, GlyphRestart));
        items.Add(new FlyoutItem(IdOf(QuickLink.ShutDownOrSignOut), S.DockQuickLinkShutDownOrSignOut, new FlyoutIcon.Glyph(GlyphPower), Children: power));
        items.Add(Line(QuickLink.Desktop, S.DockQuickLinkDesktop, GlyphDesktop));
        return items;
    }

    /// <summary>
    /// What a line opens, for the lines that open something: a file or address and the shell
    /// verb (null: the default one). Null for the lines that are keys (Search, Run), the power
    /// lines and Desktop, which the dock does itself.
    /// </summary>
    public static (string File, string? Verb)? Target(QuickLink link, QuickLinkMachine machine)
    {
        string InSystem(string file) => Path.Combine(Environment.SystemDirectory, file);
        var terminal = machine.WindowsTerminal ? "wt.exe" : "powershell.exe";
        return link switch
        {
            QuickLink.InstalledApps => ("ms-settings:appsfeatures", null),
            QuickLink.MobilityCenter => (InSystem("mblctr.exe"), null),
            QuickLink.PowerOptions => ("ms-settings:powersleep", null),
            QuickLink.EventViewer => (InSystem("eventvwr.msc"), null),
            QuickLink.System => ("ms-settings:about", null),
            QuickLink.DeviceManager => (InSystem("devmgmt.msc"), null),
            QuickLink.NetworkConnections => ("ms-settings:network", null),
            QuickLink.DiskManagement => (InSystem("diskmgmt.msc"), null),
            QuickLink.ComputerManagement => (InSystem("compmgmt.msc"), null),
            QuickLink.Terminal => (terminal, null),
            QuickLink.TerminalAdmin => (terminal, "runas"),
            QuickLink.TaskManager => ("taskmgr.exe", null),
            QuickLink.Settings => ("ms-settings:", null),
            QuickLink.FileExplorer => ("explorer.exe", null),
            _ => null,
        };
    }

    /// <summary>
    /// quick: the power commands this PC offers, for the power widget's flyout — the same lines,
    /// names and glyphs the quick-link menu's power submenu has, with Lock, which Windows keeps in
    /// its account menu instead. Sleep and Hibernate only where Windows' own power menu offers them.
    /// </summary>
    public static List<FlyoutItem> PowerLines(QuickLinkMachine machine)
    {
        var items = new List<FlyoutItem>();
        if (machine.Sleep)
        {
            items.Add(Line(QuickLink.Sleep, S.DockQuickLinkSleep, GlyphSleep));
        }
        if (machine.Hibernate)
        {
            items.Add(Line(QuickLink.Hibernate, S.DockQuickLinkHibernate, GlyphHibernate));
        }
        items.Add(Line(QuickLink.ShutDown, S.DockQuickLinkShutDown, GlyphPower));
        items.Add(Line(QuickLink.Restart, S.DockQuickLinkRestart, GlyphRestart));
        items.Add(Line(QuickLink.SignOut, S.DockQuickLinkSignOut, GlyphSignOut));
        items.Add(Line(QuickLink.Lock, S.DockQuickLinkLock, GlyphLock));
        return items;
    }

    /// <summary>Whether <paramref name="link"/> is one of the lines that end the session, lock it or put the PC to sleep.</summary>
    public static bool IsPower(QuickLink link) =>
        link is QuickLink.SignOut or QuickLink.Sleep or QuickLink.Hibernate or QuickLink.ShutDown or QuickLink.Restart or QuickLink.Lock
            or QuickLink.SwitchUser or QuickLink.UpdateRestart or QuickLink.UpdateShutDown;

    /// <summary>
    /// Signs out, sleeps, hibernates, shuts down or restarts, as Windows' own power menu does: apps
    /// are asked to close, never forced; shutting down is the fast-startup kind where that is on.
    /// </summary>
    internal static void Power(QuickLink link)
    {
        const uint planned = (uint)SHTDN.SHTDN_REASON_MAJOR_OTHER | (uint)SHTDN.SHTDN_REASON_MINOR_OTHER | SHTDN.SHTDN_REASON_FLAG_PLANNED;
        bool done;
        switch (link)
        {
            case QuickLink.SignOut:
                done = ExitWindowsEx(EWX.EWX_LOGOFF, planned);
                break;
            case QuickLink.Lock:
                // Windows' own lock: the session stays as it is, everything keeps running.
                done = LockWorkStation();
                break;
            case QuickLink.Sleep:
            case QuickLink.Hibernate:
                done = SetSuspendState((byte)(link == QuickLink.Hibernate ? 1 : 0), 0, 0) != 0;
                break;
            case QuickLink.ShutDown:
            {
                // Windows' "Shut down" is a hybrid one while fast startup is on.
                uint how = EWX.EWX_SHUTDOWN | EWX.EWX_POWEROFF;
                if (Dword(HKEY.HKEY_LOCAL_MACHINE, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled") == 1) // not-ui: a registry key
                {
                    how |= EWX.EWX_HYBRID_SHUTDOWN;
                }
                done = AllowShutdown() && ExitWindowsEx(how, planned);
                break;
            }
            case QuickLink.Restart:
                done = AllowShutdown() && ExitWindowsEx(EWX.EWX_REBOOT, planned);
                break;
            case QuickLink.SwitchUser:
                // §641: Windows' own "Switch user": the session left as it is, the sign-in screen up (WTSDisconnectSession).
                done = WTSDisconnectSession(0, WtsCurrentSession, 0) != 0;
                break;
            case QuickLink.UpdateRestart or QuickLink.UpdateShutDown:
            {
                // §641: as Start's "Update and restart" and "Update and shut down": InitiateShutdown with the updates installed.
                var how = ShutdownInstallUpdates | (link == QuickLink.UpdateRestart ? ShutdownRestart : ShutdownPowerOff);
                if (link == QuickLink.UpdateShutDown && Dword(HKEY.HKEY_LOCAL_MACHINE, @"SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled") == 1) // not-ui: a registry key
                {
                    how |= ShutdownHybrid;
                }
                done = AllowShutdown() && InitiateShutdownW(null, null, 0, how, planned) == 0;
                break;
            }
            default:
                return;
        }
        if (!done)
        {
            Console.Error.WriteLine($"Karpo: {link} failed: error {GetLastError()}");
        }
    }

    private static FlyoutItem Line(QuickLink link, string text, string glyph) => new(IdOf(link), text, new FlyoutIcon.Glyph(glyph));

    /// <summary>The shutdown privilege, which a user's token holds but keeps switched off.</summary>
    private static bool AllowShutdown()
    {
        HANDLE token;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN.TOKEN_ADJUST_PRIVILEGES | TOKEN.TOKEN_QUERY, &token))
        {
            return false;
        }
        try
        {
            var privileges = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
            fixed (char* name = SE.SE_SHUTDOWN_NAME)
            {
                if (!LookupPrivilegeValueW(null, name, &privileges.Privileges.e0.Luid))
                {
                    return false;
                }
            }
            privileges.Privileges.e0.Attributes = SE.SE_PRIVILEGE_ENABLED;
            // Succeeding is not enough: ERROR_NOT_ALL_ASSIGNED says the token lacks it.
            return AdjustTokenPrivileges(token, BOOL.FALSE, &privileges, 0, null, null) && GetLastError() == ERROR.ERROR_SUCCESS;
        }
        finally
        {
            _ = CloseHandle(token);
        }
    }

    /// <summary>A DWORD value, or -1 when it isn't there.</summary>
    internal static long Dword(HKEY root, string key, string name)
    {
        uint data = 0;
        uint size = sizeof(uint);
        fixed (char* path = key)
        fixed (char* value = name)
        {
            return RegGetValueW(root, path, value, (uint)RRF.RRF_RT_REG_DWORD, null, &data, &size) == ERROR.ERROR_SUCCESS ? data : -1;
        }
    }

    // powrprof.dll, which TerraFX leaves out. BOOLEANs are bytes.
    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern byte GetPwrCapabilities(SYSTEM_POWER_CAPABILITIES* capabilities);

    [DllImport("powrprof.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern byte SetSuspendState(byte hibernate, byte force, byte wakeupEventsDisabled);

    // §641: wtsapi32's and advapi32's, for Switch user and the updates' restart and shut down.
    private const uint WtsCurrentSession = uint.MaxValue;
    private const uint ShutdownRestart = 0x04;
    private const uint ShutdownPowerOff = 0x08;
    private const uint ShutdownInstallUpdates = 0x40;
    private const uint ShutdownHybrid = 0x200;

    [DllImport("wtsapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int WTSDisconnectSession(nint server, uint session, int wait);

    [DllImport("advapi32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint InitiateShutdownW(char* machine, char* message, uint grace, uint flags, uint reason);
}
