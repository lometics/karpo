// The tick Windows plays when the volume moves.
//
// Windows' own volume UI plays it — the media keys, the tray slider — not Core
// Audio, so a level set through the endpoint (which is how Karpo's slider and
// its widget set it) is silent. This plays the same sound for Karpo's own
// controls, and only for those: a level that moved somewhere else (another app,
// a media key Windows handled itself, the endpoint's own callback) already
// ticked, and a second one would double it.
//
// It is played at the level just set, which is what makes it useful — you hear
// how loud you have made it ("the sound that plays when you move the volume
// slider always has the same volume"). Two things make sure of that:
//
//   - The tick carries the level it is for, and is only asked for once the
//     endpoint has taken that level: every caller sets the level first and
//     passes the one the output now reports (VolumeWatch.Level, which moves only
//     when the set succeeded), or the slider's value just handed to the source.
//   - Karpo plays it itself, from the dock's thread after the set returned:
//     the system's Default Beep through PlaySound into the system sounds'
//     session on the default output, which the master level scales — the
//     session Windows' own slider plays its tick in. It was MessageBeep, which
//     hands the sound to the system to play when and in whatever session it
//     likes, and was heard at the same loudness whatever the slider said.
//
// PlaySound with SND_ASYNC returns at once, so the dock's one thread is never
// held. Someone whose sound scheme has no Default Beep (the "No Sounds" scheme)
// hears nothing, without Karpo asking anything (SND_NODEFAULT).
//
// Not the volume mixer's rows: Windows doesn't tick for an app's own level, and
// a tick through the default device would say nothing about the app's.
//
// A wheel turned over a volume control (the Sound icon on the bar, a slider in a
// flyout, the volume card and its list line) and a key held on a slider are a run
// of notches, not one level: the tick waits for the run to stop, and plays once
// QuietMs after its last notch, at the level that notch left, as Windows' own
// does. It played on every notch, thinned to one each GapMs, which on a spun wheel
// was a burst of ticks. The wait is a one-shot DispatcherQueueTimer on the dock's
// thread, armed again by each notch; the tests hand over a clock and settle it
// themselves.

using System.Runtime.InteropServices;
using Karpo.Core;

namespace Karpo.Dock.QuickSettings.Services;

internal static class Feedback
{
    /// <summary>
    /// The shortest gap between two ticks. A wheel spun hard sends notches faster than a sound can
    /// play; the system cuts the last one off to start the next, which is the stutter Windows' own
    /// slider makes, but past a few a second it is noise rather than feedback.
    /// </summary>
    internal const int GapMs = 60;

    // Per thread: the dock plays these on its own thread, and a test that swaps the sound out
    // can't be heard by another test beside it.
    [ThreadStatic]
    private static Action<int>? play;

    [ThreadStatic]
    private static long last;

    /// <summary>
    /// How long a run of wheel notches must rest before its one tick: long enough that a wheel turned
    /// notch after notch is one run, short enough that the tick still answers the turn.
    /// </summary>
    internal const int QuietMs = 300;

    // A run of notches waiting to settle: when its tick is due (0 for none), whether the output was
    // muted at its last notch, and the level that notch left.
    [ThreadStatic]
    private static long due;

    [ThreadStatic]
    private static bool dueMuted;

    [ThreadStatic]
    private static int dueLevel;

    [ThreadStatic]
    private static Action<int>? arm;

    [ThreadStatic]
    private static Windows.System.DispatcherQueueTimer? timer;

    /// <summary>
    /// Asks for <see cref="Settle"/> in so many ms. The dock's is a one-shot timer on its own
    /// thread's queue; a test hands over its own and settles the run on its own clock.
    /// </summary>
    internal static Action<int>? Arm
    {
        get => arm ?? Wait;
        set => arm = value;
    }

    /// <summary>
    /// What a tick is, given the level (0 to 100) the output now has: the system's Default Beep,
    /// played by Karpo at that level — except on a run that may not reach the person's world, where
    /// it is nothing unless a test hands one over to count.
    /// <para>
    /// The guarantee lives here, in the thing being reached (docs\widgets.md §11). A tick was a real
    /// beep on every run, so any test that dragged a volume slider and let go of it played one out of
    /// the person's speakers, and every new host of the sound rows was one more way to do it. A test
    /// that wants to know a tick happened sets <see cref="Play"/> and counts, as FeedbackTests does,
    /// and can hold the level it was played at against the output's.
    /// </para>
    /// </summary>
    internal static Action<int>? Play
    {
        get => play ?? (Karpo.Core.Config.IsIsolated ? null : Beep);
        set => play = value;
    }

    /// <summary>
    /// A volume Karpo's own control just settled on, already set: the tick at that level, unless it
    /// is muted (there would be nothing to hear anyway) or one has just played.
    /// </summary>
    /// <param name="level">The level the output has now, 0 to 100: set before this is asked, never after.</param>
    /// <param name="muted">The output is muted: no tick.</param>
    /// <param name="now">The clock, for the gap between ticks; the tests hand one over.</param>
    public static void Volume(int level, bool muted, long now)
    {
        // A level settled here answers any run of notches still waiting: one tick, not two.
        due = 0;
        if (muted || (last != 0 && now - last < GapMs))
        {
            return;
        }
        last = now;
        Play?.Invoke(Math.Clamp(level, 0, 100));
    }

    /// <summary>The same, on the system's own clock.</summary>
    public static void Volume(int level, bool muted) => Volume(level, muted, Environment.TickCount64);

    /// <summary>
    /// A wheel notch or a key moved the level, one of a run that may go on: nothing now, and the one
    /// tick <see cref="QuietMs"/> after the last notch of the run (<see cref="Settle"/>), at the level
    /// that notch left.
    /// </summary>
    /// <param name="level">The level the output has now that the notch is applied, 0 to 100.</param>
    /// <param name="muted">The output is muted: the run ends silent, unless a later notch says otherwise.</param>
    /// <param name="now">The clock; the tests hand one over.</param>
    public static void Notch(int level, bool muted, long now)
    {
        due = now + QuietMs;
        dueLevel = level;
        dueMuted = muted;
        Arm?.Invoke(QuietMs);
    }

    /// <summary>The same, on the system's own clock.</summary>
    public static void Notch(int level, bool muted) => Notch(level, muted, Environment.TickCount64);

    /// <summary>
    /// The wait for a run of notches is over: its tick, once, if the run has rested
    /// <see cref="QuietMs"/> since its last notch; else the wait goes on for the rest of it.
    /// </summary>
    public static void Settle(long now)
    {
        if (due == 0)
        {
            return;
        }
        if (now < due)
        {
            Arm?.Invoke((int)Math.Min(QuietMs, due - now));
            return;
        }
        due = 0;
        Volume(dueLevel, dueMuted, now);
    }

    /// <summary>Whether a run of notches is waiting for its tick; for the tests.</summary>
    internal static bool Waiting => due != 0;

    /// <summary>Forgets when the last tick was, and any run waiting for one; the tests start from a clean slate.</summary>
    internal static void Forget()
    {
        last = 0;
        due = 0;
        timer?.Stop();
    }

    /// <summary>
    /// The dock's wait: a one-shot timer on this thread's dispatcher queue, started again by every
    /// notch, so it goes off once the wheel has rested. A thread without a queue has nobody to wait
    /// for it, and the run settles at once.
    /// </summary>
    private static void Wait(int ms)
    {
        if (timer is null)
        {
            Windows.System.DispatcherQueue? queue = null;
            try
            {
                queue = Windows.System.DispatcherQueue.GetForCurrentThread();
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            {
                // No WinRT on this thread.
            }
            if (queue is null)
            {
                Settle(due);
                return;
            }
            timer = queue.CreateTimer();
            timer.IsRepeating = false;
            timer.Tick += (_, _) => Troubles.Guard("Volume: its tick", () => Settle(Environment.TickCount64));
        }
        timer.Stop();
        timer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, ms));
        timer.Start();
    }

    // PlaySound's flags: the alias is a number (SND_ALIAS_ID), it returns at once (SND_ASYNC), a
    // scheme without the sound plays nothing rather than a stand-in (SND_NODEFAULT), and it is the
    // system sounds' session, as Windows' own tick is (SND_SYSTEM).
    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_ALIAS_ID = 0x00110000;
    private const uint SND_SYSTEM = 0x00200000;

    /// <summary>The Default Beep's alias: sndAlias('S', 'D'), SND_ALIAS_SYSTEMDEFAULT.</summary>
    private const nint SND_ALIAS_SYSTEMDEFAULT = 'S' | ('D' << 8);

    /// <summary>
    /// The Default Beep, played by Karpo into the system sounds' session on the default output, whose
    /// level the caller has just set: the master level scales it, so it is as loud as that level. The
    /// level itself is the caller's promise, not something to apply again here.
    /// </summary>
    private static void Beep(int level)
    {
        try
        {
            _ = PlaySoundW(SND_ALIAS_SYSTEMDEFAULT, 0, SND_ALIAS_ID | SND_ASYNC | SND_NODEFAULT | SND_SYSTEM);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException)
        {
            // No sound system: no tick, as for a scheme without one.
        }
    }

    [DllImport("winmm", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PlaySoundW(nint pszSound, nint hmod, uint fdwSound);
}
