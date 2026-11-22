// Screen time's one source: how long the session was unlocked, a display on and somebody at the
// keyboard or mouse, and which app's window was in front — counted from the dock's start whether or
// not a widget shows it, as the performance readings are (docs\widgets.md §55, "one service that
// always runs"), so a line added in the afternoon has the morning.
//
// It is Windows' half; the counting is ScreenTimeCount's. A pool-thread timer looks once a minute
// at the last input and whether the session is locked; the window in front comes from a WinEvent
// hook on the dock's thread; the lock, the sleep and the display going dark come from the messages
// the dock forwards (System), whatever the dock is doing at the time. A look costs a few
// microseconds and allocates nothing when there is nothing to write.
//
// The days are kept in the local folder (Config.LocalFolder) in the files Karpo has always written
// (ScreenTimeFiles): today's every five minutes it changed and when the session locks, the PC
// sleeps or the dock stops; the history when a day other than today changed, the old file kept as
// a backup. One count for the process, made when the dock starts it (Start) and saved and let go
// when it stops (Stop); an isolated run — a test, --smoke — never starts it, so it never reads or
// writes anybody's files.

using Karpo.Core;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock;

internal sealed unsafe class ScreenTimeTracker
{
    private static readonly TimeSpan SaveEvery = TimeSpan.FromMinutes(5);

    /// <summary>EVENT_SYSTEM_FOREGROUND: another window came to the front.</summary>
    private const uint EventSystemForeground = 3;

    // Everything below is shared with the timer thread and the hook, under this lock.
    private readonly Lock gate = new();
    private ScreenTimeCount? count;
    private Timer? timer;
    private HWINEVENTHOOK foregroundHook;
    private HPOWERNOTIFY displayNotify;
    private string folder = "";

    /// <summary>The day the history file was last written or read for: it holds every day but that one.</summary>
    private DateOnly historyFor;

    private DateTime savedAt;
    private long savedSeconds = -1;

    private ScreenTimeTracker()
    {
    }

    public static ScreenTimeTracker Shared { get; } = new();

    /// <summary>Whether it is counting: the dock started it and has not stopped it.</summary>
    public bool Counting
    {
        get
        {
            lock (gate)
            {
                return count is not null;
            }
        }
    }

    /// <summary>
    /// Starts counting, on the dock's thread (the hook it sets calls back on it), with
    /// <paramref name="window"/> told when a display goes dark or lights again. Reads the files in
    /// <paramref name="local"/>; again does nothing.
    /// </summary>
    public void Start(HWND window, string local)
    {
        lock (gate)
        {
            if (count is not null)
            {
                return;
            }
            var now = Clock.RealUtc();
            folder = local;
            var today = ScreenTimeClock.LocalDate(now, TimeZoneInfo.Local);
            var read = ScreenTimeFiles.Read(ScreenTimeFiles.ReadLive(local), ScreenTimeFiles.ReadHistory(local), today, ScreenTimeCount.IdleMinutes);
            count = new ScreenTimeCount(read, TimeZoneInfo.Local, now, LogonTime(), LastInput(now), SessionLocked());
            historyFor = today;
            savedAt = now;
            savedSeconds = count.Seconds(today);
            count.Foreground(now, LastInput(now), AppKey(GetForegroundWindow()));
            foregroundHook = SetWinEventHook(EventSystemForeground, EventSystemForeground, default, &ForegroundChanged, 0, 0, 0);
            var displayStatus = GUID.GUID_SESSION_DISPLAY_STATUS;
            if (window != HWND.NULL)
            {
                displayNotify = RegisterPowerSettingNotification((HANDLE)window.Value, &displayStatus, DEVICE.DEVICE_NOTIFY_WINDOW_HANDLE);
            }
            timer = new Timer(static state => ((ScreenTimeTracker)state!).Observe(), this, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }
    }

    /// <summary>Stops counting: counted up to now and saved, the hook and the timer let go.</summary>
    public void Stop()
    {
        Timer? stopped;
        lock (gate)
        {
            if (count is not { } counting)
            {
                return;
            }
            var now = Clock.RealUtc();
            counting.Tally(now, LastInput(now));
            Save(counting, now, force: true);
            count = null;
            (stopped, timer) = (timer, null);
            if (foregroundHook != default)
            {
                _ = UnhookWinEvent(foregroundHook);
                foregroundHook = default;
            }
            if (displayNotify != HPOWERNOTIFY.NULL)
            {
                _ = UnregisterPowerSettingNotification(displayNotify);
                displayNotify = HPOWERNOTIFY.NULL;
            }
        }
        stopped?.Dispose();
    }

    /// <summary>
    /// Every day kept, today counted up to now: a copy, for a line to draw while the counting goes
    /// on. Nothing counted where it is not counting.
    /// </summary>
    public ScreenTimeSnapshot Snapshot()
    {
        var now = Clock.RealUtc();
        lock (gate)
        {
            return count is { } counting
                ? counting.Snapshot(now, LastInput(now))
                : ScreenTimeSnapshot.Empty(ScreenTimeClock.LocalDate(now, TimeZoneInfo.Local));
        }
    }

    /// <summary>
    /// The session's and the power's messages, as the dock's window hears them: the lock and the
    /// unlock, the PC going to sleep and waking, a display going dark and lighting again. Forwarded
    /// whatever the dock is doing, since one lost would leave the count wrong until the next look.
    /// </summary>
    public void System(uint msg, WPARAM wParam, LPARAM lParam)
    {
        var now = Clock.RealUtc();
        lock (gate)
        {
            if (count is not { } counting)
            {
                return;
            }
            var input = LastInput(now);
            switch (msg)
            {
                case WM.WM_WTSSESSION_CHANGE:
                    switch ((int)(nuint)wParam)
                    {
                        case WTS.WTS_SESSION_LOCK:
                            counting.Lock(now, input);
                            Save(counting, now, force: true);
                            break;
                        case WTS.WTS_SESSION_UNLOCK:
                            counting.Unlock(now, input);
                            break;
                        case WTS.WTS_SESSION_LOGOFF:
                            counting.Tally(now, input);
                            Save(counting, now, force: true);
                            break;
                    }
                    break;
                case WM.WM_POWERBROADCAST:
                    switch ((int)(nuint)wParam)
                    {
                        case PBT.PBT_APMSUSPEND:
                            counting.Sleep(now, input);
                            Save(counting, now, force: true);
                            break;
                        case PBT.PBT_APMRESUMEAUTOMATIC:
                        case PBT.PBT_APMRESUMESUSPEND:
                            counting.Wake(now, input);
                            break;
                        case PBT.PBT_POWERSETTINGCHANGE when (nint)lParam != 0:
                        {
                            var setting = (POWERBROADCAST_SETTING*)(nint)lParam;
                            if (setting->PowerSetting == GUID.GUID_SESSION_DISPLAY_STATUS && setting->DataLength >= sizeof(uint))
                            {
                                // 0 off, 1 on, 2 dimmed: a dimmed display is still looked at.
                                if (*(uint*)&setting->Data == 0)
                                {
                                    counting.Dark(now, input);
                                }
                                else
                                {
                                    counting.Lit(now, input);
                                }
                            }
                            break;
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>A look from the timer: count, and keep the files current.</summary>
    private void Observe()
    {
        try
        {
            var now = Clock.RealUtc();
            var locked = SessionLocked();
            lock (gate)
            {
                if (count is not { } counting)
                {
                    return;
                }
                counting.Look(now, LastInput(now), locked);
                Save(counting, now);
            }
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            // A pool thread's exception would end the process.
            Console.Error.WriteLine($"Karpo: screen time not counted: {error.Message}");
        }
    }

    /// <summary>
    /// Writes what changed: the history first when a day other than today changed or today is a new
    /// day (so a day never leaves today's file before it is in the history), then today's file, at
    /// most every <see cref="SaveEvery"/> unless <paramref name="force"/>d.
    /// </summary>
    private void Save(ScreenTimeCount counting, DateTime now, bool force = false)
    {
        var today = counting.Today(now);
        var history = counting.HistoryChanged || historyFor != today;
        var seconds = counting.Seconds(today);
        var live = force || (seconds != savedSeconds && now - savedAt >= SaveEvery);
        if (!history && !live)
        {
            // The minute's look ends here: nothing to write, nothing allocated.
            return;
        }
        if (history && ScreenTimeFiles.Write(folder, ScreenTimeFiles.HistoryName, ScreenTimeFiles.Serialize(ScreenTimeFiles.History(counting.Logs, today)), backup: true))
        {
            counting.HistoryChanged = false;
            historyFor = today;
        }
        if (live)
        {
            savedAt = now;
            savedSeconds = seconds;
            ScreenTimeFiles.Write(folder, ScreenTimeFiles.LiveName, ScreenTimeFiles.Serialize(ScreenTimeFiles.Live(counting.Logs, today)), backup: false);
        }
    }

    [global::System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static void ForegroundChanged(HWINEVENTHOOK hook, uint evt, HWND hwnd, int obj, int child, uint thread, uint time)
    {
        try
        {
            // Worked out before the lock: it asks other processes.
            var app = AppKey(hwnd);
            var tracker = Shared;
            var now = Clock.RealUtc();
            lock (tracker.gate)
            {
                tracker.count?.Foreground(now, LastInput(now), app);
            }
        }
        catch
        {
            // A window may disappear between the event and the query.
        }
    }

    /// <summary>
    /// The app <paramref name="hwnd"/> belongs to, as screen time counts it (<see cref="ScreenTimeApps.KeyOf"/>):
    /// a Store app's window is ApplicationFrameHost's, so the app inside the frame is looked for, and a
    /// packaged app counts by its AUMID rather than its executable.
    /// </summary>
    private static string? AppKey(HWND hwnd)
    {
        if (hwnd == HWND.NULL)
        {
            return null;
        }
        uint pid;
        _ = GetWindowThreadProcessId(hwnd, &pid);
        if (pid == 0)
        {
            return null;
        }
        var exe = ImagePath(pid);
        if (ScreenTimeApps.IsFrameHost(exe))
        {
            return ScreenTimeApps.KeyOf(exe, null, WindowAumid(hwnd) ?? HostedAumid(hwnd, pid));
        }
        return ScreenTimeApps.KeyOf(exe, ProcessAumid(pid), null);
    }

    private static string? ImagePath(uint pid)
    {
        var process = OpenProcess(PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == HANDLE.NULL)
        {
            return null;
        }
        try
        {
            var buffer = stackalloc char[32768];
            uint length = 32768;
            return QueryFullProcessImageNameW(process, 0, buffer, &length) ? new string(buffer, 0, (int)length) : null;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    /// <summary>The AUMID of a packaged process; null for an unpackaged one.</summary>
    private static string? ProcessAumid(uint pid)
    {
        var process = OpenProcess(PROCESS.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == HANDLE.NULL)
        {
            return null;
        }
        try
        {
            var buffer = stackalloc char[256];
            uint length = 256;
            return GetApplicationUserModelId(process, &length, buffer) == 0 && length > 1 ? new string(buffer, 0, (int)length - 1) : null;
        }
        finally
        {
            _ = CloseHandle(process);
        }
    }

    /// <summary>The AUMID the shell set on a window: a Store app's frame carries its app's.</summary>
    private static string? WindowAumid(HWND hwnd)
    {
        IPropertyStore* store = null;
        if (SHGetPropertyStoreForWindow(hwnd, __uuidof<IPropertyStore>(), (void**)&store).FAILED || store == null)
        {
            return null;
        }
        try
        {
            PROPERTYKEY key = PKEY.PKEY_AppUserModel_ID;
            PROPVARIANT value = default;
            if (store->GetValue(&key, &value).FAILED)
            {
                return null;
            }
            char* text = null;
            var converted = PropVariantToStringAlloc(&value, &text);
            _ = PropVariantClear(&value);
            if (converted.FAILED)
            {
                return null;
            }
            var aumid = Native.ComUtil.FromTaskMem(text);
            return string.IsNullOrEmpty(aumid) ? null : aumid;
        }
        finally
        {
            store->Release();
        }
    }

    // The frame's child that belongs to another process, found by HostedChild.
    [ThreadStatic] private static uint hostedPid;
    [ThreadStatic] private static uint framePid;

    /// <summary>The AUMID of the app a Store app frame shows: its CoreWindow child is the app's own process.</summary>
    private static string? HostedAumid(HWND frame, uint pid)
    {
        framePid = pid;
        hostedPid = 0;
        _ = EnumChildWindows(frame, &HostedChild, default);
        return hostedPid != 0 ? ProcessAumid(hostedPid) : null;
    }

    [global::System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static BOOL HostedChild(HWND child, LPARAM unused)
    {
        uint pid = 0;
        _ = GetWindowThreadProcessId(child, &pid);
        if (pid != 0 && pid != framePid)
        {
            hostedPid = pid;
            return BOOL.FALSE;
        }
        return BOOL.TRUE;
    }

    [global::System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetApplicationUserModelId(HANDLE process, uint* length, char* aumid);

    private static DateTime LastInput(DateTime now)
    {
        var info = new LASTINPUTINFO { cbSize = (uint)sizeof(LASTINPUTINFO) };
        if (!GetLastInputInfo(&info))
        {
            return now;
        }
        // Both are GetTickCount readings, which go on through sleep: the unsigned difference
        // survives the wrap after 49.7 days.
        var idleMs = unchecked(GetTickCount() - info.dwTime);
        return now - TimeSpan.FromMilliseconds(idleMs);
    }

    private static bool SessionLocked()
    {
        WTSINFOEXW* info = null;
        uint bytes;
        if (!WTSQuerySessionInformationW(HANDLE.NULL, WTS_CURRENT_SESSION, WTSSessionInfoEx, (char**)&info, &bytes) || info == null)
        {
            return false;
        }
        try
        {
            return bytes >= sizeof(WTSINFOEXW) && info->Level == 1 && info->Data.SessionFlags == WTS_SESSIONSTATE_LOCK;
        }
        finally
        {
            WTSFreeMemory(info);
        }
    }

    /// <summary>When this session's user logged on, in UTC.</summary>
    private static DateTime? LogonTime()
    {
        WTSINFOEXW* info = null;
        uint bytes;
        if (!WTSQuerySessionInformationW(HANDLE.NULL, WTS_CURRENT_SESSION, WTSSessionInfoEx, (char**)&info, &bytes) || info == null)
        {
            return null;
        }
        try
        {
            return bytes >= sizeof(WTSINFOEXW) && info->Level == 1 && info->Data.LogonTime > 0
                ? DateTime.FromFileTimeUtc(info->Data.LogonTime)
                : null;
        }
        finally
        {
            WTSFreeMemory(info);
        }
    }

    // wtsapi32.h, which TerraFX does not carry. The structs are only read, through pointers.
#pragma warning disable CS0649
    private const uint WTS_CURRENT_SESSION = 0xFFFFFFFF;
    private const int WTSSessionInfoEx = 25;
    private const int WTS_SESSIONSTATE_LOCK = 0;

    private struct WTSINFOEX_LEVEL1_W
    {
        public uint SessionId;
        public int SessionState;
        public int SessionFlags;
        public fixed char WinStationName[33];
        public fixed char UserName[21];
        public fixed char DomainName[18];
        public long LogonTime;
        public long ConnectTime;
        public long DisconnectTime;
        public long LastInputTime;
        public long CurrentTime;
        public uint IncomingBytes;
        public uint OutgoingBytes;
        public uint IncomingFrames;
        public uint OutgoingFrames;
        public uint IncomingCompressedBytes;
        public uint OutgoingCompressedBytes;
    }

    private struct WTSINFOEXW
    {
        public uint Level;

        /// <summary>WTSINFOEX_LEVEL_W: a union of level 1 alone.</summary>
        public WTSINFOEX_LEVEL1_W Data;
    }
#pragma warning restore CS0649

    [global::System.Runtime.InteropServices.DllImport("wtsapi32.dll", ExactSpelling = true)]
    [global::System.Runtime.InteropServices.DefaultDllImportSearchPaths(global::System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern BOOL WTSQuerySessionInformationW(HANDLE server, uint session, int infoClass, char** buffer, uint* bytes);

    [global::System.Runtime.InteropServices.DllImport("wtsapi32.dll", ExactSpelling = true)]
    [global::System.Runtime.InteropServices.DefaultDllImportSearchPaths(global::System.Runtime.InteropServices.DllImportSearchPath.System32)]
    private static extern void WTSFreeMemory(void* memory);
}
