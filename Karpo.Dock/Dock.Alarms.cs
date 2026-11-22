// alarm: the dock's part of the alarms (docs\widgets.md §106): handing the book every alarm widget's
// options, looking at it when something is due, ringing what rings, and writing what it switched.
//
// The book is the dock's own (Countdowns.Alarms, Karpo.Core\Alarms.cs), which every alarm line reads
// and works. It is handed the config's alarms where the dock starts and whenever the config changes
// (Sync), and looked at (Tick) on a timer of its own armed for the next thing due — a time coming, a
// snooze over, a ring run long enough — and at least once a minute, so a clock set forward or a PC
// woken from sleep is seen within the minute; and with the clock's tick, which runs on Windows'
// clock being set and on waking. What it does with a time that was missed is the book's rule.
//
// RINGING. Karpo has no packaged identity, so it cannot raise a Windows toast (as the timer cannot,
// Dock.Countdowns.cs): an alarm rings with the sound Windows gives an alarm, looped, while it rings —
// until it is snoozed or dismissed on its line, or has rung for five minutes (AlarmBook.RingFor) —
// and Windows' notification sound once where this PC has no alarm sound. Its line says "Ringing" in
// the accent with Snooze and Dismiss where its switch was; one in a folder that is shut marks the
// folder's button until it is opened, as a timer's end does.

using Karpo.Core;
using System.Runtime.InteropServices;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>The host's timer that looks at the alarms when the next thing is due.</summary>
    private const uint ALARM = 67;

    /// <summary>The longest the dock goes without looking at an alarm that is on: a minute.</summary>
    private const int AlarmLookMs = 60_000;

    private bool alarmsHooked;

    /// <summary>Whether the alarm's sound is playing.</summary>
    private bool alarmSounding;

    /// <summary>The book hears from the dock from its start: an alarm rings with no folder open.</summary>
    private void HookAlarms()
    {
        if (alarmsHooked)
        {
            return;
        }
        var alarms = countdowns.Alarms;
        alarms.Changed += AlarmChanged;
        alarms.Rang += AlarmRang;
        alarms.Switched += AlarmSwitched;
        alarmsHooked = true;
        SyncAlarms();
    }

    private void DisposeAlarms()
    {
        if (alarmsHooked)
        {
            var alarms = countdowns.Alarms;
            alarms.Changed -= AlarmChanged;
            alarms.Rang -= AlarmRang;
            alarms.Switched -= AlarmSwitched;
            alarmsHooked = false;
        }
        Kill(ALARM);
        if (alarmSounding)
        {
            alarmSounding = false;
            AlarmSound.Stop();
        }
    }

    /// <summary>
    /// The config's alarms handed to the book: one new, set to another time or switched in Settings
    /// is set from now, one removed goes. Then a look at them.
    /// </summary>
    private void SyncAlarms()
    {
        countdowns.Alarms.Sync(AlarmsIn(config), Clock.RealUtc());
        AlarmsDue();
    }

    /// <summary>Every alarm widget of <paramref name="config"/>, with its own options, wherever it stands.</summary>
    internal static IEnumerable<(string Id, AlarmOptions Options)> AlarmsIn(Config config)
    {
        foreach (var layout in config.Layouts)
        {
            foreach (var (_, island) in Folders.All(layout.Islands))
            {
                if (island.Info == WidgetInfos.Alarm && island.Alarm is { } options)
                {
                    yield return (island.Id, options);
                }
            }
        }
    }

    /// <summary>
    /// The alarms looked at now: what is due rings, stops or is missed; the sound follows whether one
    /// rings; and the timer is armed for the next thing due, a minute at the most.
    /// </summary>
    private void AlarmsDue()
    {
        Kill(ALARM);
        var alarms = countdowns.Alarms;
        var utc = Clock.RealUtc();
        alarms.Tick(utc);
        var ringing = alarms.Ringing;
        if (ringing != alarmSounding)
        {
            alarmSounding = ringing;
            if (ringing)
            {
                AlarmSound.Start();
            }
            else
            {
                AlarmSound.Stop();
            }
        }
        if (alarms.Due(utc) is { } due)
        {
            var ms = Math.Clamp((due - utc).TotalMilliseconds + 20, 100, AlarmLookMs);
            Arm(ALARM, (uint)ms);
        }
    }

    /// <summary>An alarm changed — switched, snoozed, dismissed: looked at again at once, out of the book's own call.</summary>
    private void AlarmChanged(string id)
    {
        _ = id;
        Arm(ALARM, 1);
    }

    /// <summary>An alarm began to ring: a folder that is shut holding it is marked until it is opened.</summary>
    private void AlarmRang(string id)
    {
        foreach (var island in islands.Values)
        {
            if (island is FolderIsland { Showing: false } folder && folder.Holds(id))
            {
                try
                {
                    _ = folder.SetDue(true);
                }
                catch (Exception error)
                {
                    Report(error);
                }
            }
        }
    }

    /// <summary>An alarm's switch changed in its options — on its line, or a ring that does not repeat over: written once it settles.</summary>
    private void AlarmSwitched(string id) => CountdownStepped(id);
}

/// <summary>The sound an alarm makes while it rings: Windows' own alarm sound, looped.</summary>
internal static class AlarmSound
{
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_LOOP = 0x0008;
    private const uint SND_ALIAS = 0x00010000;

    /// <summary>
    /// Windows' "Alarm 1" (the Notification.Looping.Alarm event of the sound scheme, what Windows'
    /// Clock rings with), looped until <see cref="Stop"/>; where the scheme has none, the
    /// notification sound once, as a timer's end.
    /// </summary>
    public static void Start()
    {
        try
        {
            if (!PlaySoundW("Notification.Looping.Alarm", 0, SND_ALIAS | SND_ASYNC | SND_LOOP | SND_NODEFAULT))
            {
                ClockSound.Ring();
            }
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    /// <summary>The sound stops.</summary>
    public static void Stop()
    {
        try
        {
            _ = PlaySoundW(null, 0, 0);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
        }
    }

    [DllImport("winmm", ExactSpelling = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySoundW(string? pszSound, nint hmod, uint fdwSound);
}
