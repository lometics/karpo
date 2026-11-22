// The dock's part of the timers, the focus sessions and the stopwatches: what happens when a
// countdown runs out, when a length is stepped, and when one changes.
//
// The countdowns are the dock's own (Countdowns): one per timer and focus widget, kept by its id,
// in a folder. The dock makes the store, hands it to what it makes, ticks it every
// second while one runs, drops the countdown of a widget that is gone, and writes the config once a
// stepped length has settled. A rebuild of the bar keeps them; a restart of Karpo clears them.
//
// Karpo has no packaged identity, so it cannot raise a Windows toast: a countdown that runs out
// rings once, with the sound Windows gives notifications, and its own face says it is over — by its
// name, where it has one. Nothing opens for it. One in a folder that is shut marks the folder's
// button until it is opened (FolderIsland.Due), since nothing in a folder opens a popup.
//
// A focus session turns on Windows' do not disturb while it runs and hands it back when it ends,
// as Windows 11's Focus does (FocusQuiet, over the countdowns and the service the bell shares).
//
// A stopwatch is in the countdowns' store too (Countdowns.Stopwatches), and its changes reach a
// clock face, a stopwatch face and an open folder's quick tick as a countdown's do (docs\widgets.md §84).
//
// Karpo's clock flyout was here until the clock became a folder (the clock preset, 2026-10-09), and
// a countdown's and a stopwatch's own panels, opened at their entries on the bar, until those became
// the timer, focus and stopwatch presets (2026-10-10, §92): their lines are the folders' widgets
// now, and the folders' faces show what they have left or have counted (FaceDrawings).

using Karpo.Core;

namespace Karpo.Dock;

public sealed unsafe partial class Dock
{
    /// <summary>Whether the dock is listening for a countdown running out.</summary>
    private bool countdownsHooked;

    /// <summary>
    /// The countdowns this dock runs: every timer's and focus session's. The dock's own, never one the
    /// whole process shares (<see cref="Countdowns"/>).
    /// </summary>
    private readonly Countdowns countdowns = new();

    /// <summary>A stepped length is waiting to be written once the steps settle (<see cref="COUNTDOWN_SAVE"/>).</summary>
    private bool countdownSaveDue;

    /// <summary>The host's timer that writes a stepped length once the steps have settled.</summary>
    private const uint COUNTDOWN_SAVE = 43;

    /// <summary>Rings for a countdown that ran out: once, and once for several in the same second.</summary>
    private readonly Ringer ringer = new(ClockSound.Ring);

    /// <summary>Windows' do not disturb, held on while a focus session that turns it on runs (<see cref="FocusQuiet"/>).</summary>
    private FocusQuiet? focusQuiet;

    /// <summary>The countdowns' hooks go with the dock, and do not disturb is handed back. A stepped length not yet written is written now.</summary>
    private void DisposeCountdowns()
    {
        if (countdownsHooked)
        {
            countdowns.Ended -= TimerEnded;
            countdowns.Changed -= TimersChanged;
            countdowns.Stepped -= CountdownStepped;
            countdowns.Stopwatches.Changed -= TimersChanged;
            countdownsHooked = false;
        }
        // Every focus session ends with the dock: do not disturb is handed back as their end would.
        focusQuiet?.Dispose();
        focusQuiet = null;
        // alarm: an alarm ringing stops with the dock.
        DisposeAlarms();
        if (countdownSaveDue)
        {
            Kill(COUNTDOWN_SAVE);
            countdownSaveDue = false;
            SaveConfig();
        }
    }

    /// <summary>
    /// The dock follows its countdowns from the moment it starts: they are widgets in folders, so one
    /// can be set going from a face's menu without its folder ever having been opened — and a clock
    /// face and the folder's own face still show what is left, and the ring still rings.
    /// </summary>
    private void HookCountdowns()
    {
        if (countdownsHooked)
        {
            return;
        }
        countdowns.Ended += TimerEnded;
        countdowns.Changed += TimersChanged;
        countdowns.Stepped += CountdownStepped;
        // A stopwatch started, paused or reset: its face shows it, and the tick follows it.
        countdowns.Stopwatches.Changed += TimersChanged;
        countdownsHooked = true;
        // A focus session turns on do not disturb while it runs. Nothing is asked of Windows until
        // one starts.
        focusQuiet = new FocusQuiet(countdowns, QuickSettings.Services.DoNotDisturb.Shared);
        focusQuiet.Want(FocusQuiet.KeysIn(config));
        // alarm: and its alarms, which ring with no folder open (Dock.Alarms.cs).
        HookAlarms();
    }

    /// <summary>
    /// A countdown ran out: Windows' notification sound, once — several running out in the same
    /// second ring once between them. Nothing is opened: its face says it is over (by its name,
    /// where it has one) until somebody clears or restarts it, and one in a folder that is shut marks
    /// the folder's button until it is opened.
    /// </summary>
    private void TimerEnded(string key, Countdown state)
    {
        _ = state;
        _ = ringer.Ended(Environment.TickCount64);
        foreach (var island in islands.Values)
        {
            if (island is FolderIsland { Showing: false } folder && folder.Holds(key))
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

    /// <summary>
    /// A length was stepped — on a line, from a menu — and set in its widget's options: a timer or
    /// focus face shows it, and the config is written once the steps have settled, not once a notch.
    /// </summary>
    private void CountdownStepped(string key)
    {
        _ = key;
        FaceTicks();
        countdownSaveDue = true;
        Arm(COUNTDOWN_SAVE, CountdownCard.SettleMs);
    }

    /// <summary>The steps have settled: the lengths they set are written.</summary>
    private void CountdownSettled()
    {
        Kill(COUNTDOWN_SAVE);
        if (countdownSaveDue)
        {
            countdownSaveDue = false;
            SaveConfig();
        }
    }

    /// <summary>
    /// A countdown changed: a clock face shows what a timer in its folder has left, a timer, focus or
    /// stopwatch face what its line has left or has counted, and the tick follows it (a running
    /// countdown keeps the second's tick going).
    /// </summary>
    private void TimersChanged(string key)
    {
        _ = key;
        // folders: an open folder ticks a tenth while a stopwatch in it counts — started from its
        // menu, its panel or its entry as much as from its line.
        foreach (var open in openFolders)
        {
            open.Flyout.ArmQuick();
        }
        // folders: a clock face shows what a timer in its folder has left, and a timer's, a focus
        // session's and a stopwatch's face what their line has (FaceDrawings).
        FaceTicks();
        TickClock();
    }

    /// <summary>
    /// Whether the dock ticks every second: a folder's clock face shows its seconds, or a timer or a
    /// focus session is running. Both announce themselves — a setting change rebuilds the bar, and
    /// <see cref="Countdowns"/> raises Changed — so the tick is armed again whenever this answer
    /// changes. A running stopwatch ticks it too: a clock face or a stopwatch face shows what it has
    /// counted.
    /// </summary>
    private bool SecondsTick =>
        countdowns.Ticking || countdowns.Stopwatches.Running || islands.Values.Any(island => island is FolderIsland { FaceSeconds: true });
}
