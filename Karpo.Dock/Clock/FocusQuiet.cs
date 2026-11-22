// A focus session turns on Windows' do not disturb while it runs, as Windows 11's own
// Focus does: "Turn on do not disturb", a setting of each focus-time widget, on unless
// somebody turned it off (CountdownOptions.DoNotDisturb).
//
// Windows' Focus lends do not disturb for the length of the session and hands it back:
//
//   * It is turned on when a session starts and off when it ends — runs out, or is
//     stopped. A pause is part of the session and keeps it on.
//   * Where it was on already when the session started, it is the person's, and it is
//     left on when the session ends.
//   * Where the person switched it during the session — off, or to alarms only; on the
//     bell, in the notification list, in Windows — it is theirs from then on, and the end
//     of the session leaves it as they set it.
//   * Several sessions at once hold it together, and it goes when the last of them ends.
//
// Nothing here reaches Windows itself: it is set and read through the one service every
// widget shares (DoNotDisturb.Shared), so the bell and the list show what a session did,
// and a run that may not reach the person's world gets a service that reaches nothing.
// It asks Windows nothing until a session starts, and where Windows has not yet said what
// do not disturb is, it waits for the answer rather than guess what it was before.
//
// What a session is doing is the dock's Countdowns', heard as it changes; which of them
// turn it on is the config's (KeysIn), handed over whenever it is read.

using Karpo.Core;
using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Dock;

/// <summary>
/// Windows' do not disturb, held on while any focus session that turns it on runs, and handed back
/// when the last one ends (see the note at the top of this file). The dock keeps one, over its
/// countdowns and the shared service.
/// </summary>
internal sealed class FocusQuiet : IDisposable
{
    private readonly Countdowns countdowns;
    private readonly DoNotDisturb quiet;

    /// <summary>The keys of the focus sessions that turn it on (<see cref="KeysIn"/>).</summary>
    private readonly HashSet<string> wanted = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The sessions holding it now: running or paused, and turning it on.</summary>
    private readonly HashSet<string> holding = new(StringComparer.OrdinalIgnoreCase);

    private Hold hold;
    private bool disposed;

    /// <summary>Whose do not disturb it is while sessions hold it.</summary>
    private enum Hold
    {
        /// <summary>No session holds it.</summary>
        None,

        /// <summary>A session started before Windows said what it was: waiting for the answer to decide.</summary>
        Asking,

        /// <summary>It was off, and a session turned it on: it goes off again when the last one ends.</summary>
        Ours,

        /// <summary>It was on already, or the person switched it during the sessions: left as it is.</summary>
        Theirs,
    }

    public FocusQuiet(Countdowns countdowns, DoNotDisturb quiet)
    {
        this.countdowns = countdowns;
        this.quiet = quiet;
        countdowns.Changed += Review;
        quiet.Changed += Heard;
    }

    /// <summary>Whether a focus session holds do not disturb now.</summary>
    public bool Holding => holding.Count > 0;

    /// <summary>Whether a session turned it on and will turn it off again when the last one ends.</summary>
    public bool TurnedOn => hold == Hold.Ours;

    /// <summary>
    /// The key of every focus session <paramref name="config"/> has that turns do not disturb on:
    /// each focus-time widget's, wherever it stands, unless somebody turned the setting off.
    /// </summary>
    public static IEnumerable<string> KeysIn(Config config)
    {
        foreach (var layout in config.Layouts)
        {
            foreach (var (_, island) in Folders.All(layout.Islands))
            {
                if (island.Info == WidgetInfos.Focus && island.Countdown?.DoNotDisturb != false)
                {
                    yield return island.Id;
                }
            }
        }
    }

    /// <summary>
    /// Which sessions turn it on, from the config just read (<see cref="KeysIn"/>): a session whose
    /// setting was turned off lets go of it, and one whose setting was turned on takes hold of it.
    /// </summary>
    public void Want(IEnumerable<string> keys)
    {
        wanted.Clear();
        wanted.UnionWith(keys);
        foreach (var key in countdowns.Keys.Concat(holding).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
        {
            Review(key);
        }
    }

    /// <summary>
    /// The dock is going, and its countdowns with it: every session has ended, so do not disturb is
    /// handed back as the last one's end would. Windows is asked on the way out; the answer may come
    /// after the process has gone.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        countdowns.Changed -= Review;
        holding.Clear();
        Settle();
        quiet.Changed -= Heard;
    }

    /// <summary>A countdown changed: it holds do not disturb while it is a focus session that runs and turns it on.</summary>
    private void Review(string key)
    {
        var state = countdowns.Of(key);
        var runs = wanted.Contains(key)
            && state.Kind == CountdownKind.Focus
            && state.Phase is CountdownPhase.Running or CountdownPhase.Paused;
        if (runs ? holding.Add(key) : holding.Remove(key))
        {
            Settle();
        }
    }

    /// <summary>The first session takes hold of it, and the last one's end hands it back.</summary>
    private void Settle()
    {
        if (holding.Count > 0 && hold == Hold.None)
        {
            // What it was before the first session decides what the last one's end does, so it is
            // asked for first — and where Windows has not said yet, waited for (Heard).
            quiet.Look();
            hold = Hold.Asking;
            Decide();
        }
        else if (holding.Count == 0 && hold != Hold.None)
        {
            var ours = hold == Hold.Ours;
            hold = Hold.None;
            if (ours && quiet.On == true)
            {
                quiet.Set(false);
            }
        }
    }

    /// <summary>
    /// What it was before the first session, once Windows has said: on, and it is the person's; off,
    /// and the session turns it on — where Windows lets Karpo switch it at all.
    /// </summary>
    private void Decide()
    {
        if (hold != Hold.Asking || quiet.State is not { On: { } on } state)
        {
            return;
        }
        if (on || !state.CanSwitch)
        {
            hold = Hold.Theirs;
            return;
        }
        hold = Hold.Ours;
        quiet.Set(true);
    }

    /// <summary>
    /// Windows said something: the answer a session was waiting for, or the person switching it
    /// while sessions hold it — off, or to alarms only, anywhere — after which it is theirs.
    /// </summary>
    private void Heard()
    {
        switch (hold)
        {
            case Hold.Asking:
                Decide();
                break;
            case Hold.Ours when quiet.State is { On: false } or { AlarmsOnly: true }:
                hold = Hold.Theirs;
                break;
        }
    }
}
