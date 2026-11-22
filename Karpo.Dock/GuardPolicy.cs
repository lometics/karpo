// What the guard does once its dock has ended (DockGuard.cs), as a table the tests can hold whole.
//
//   a newer dock holds the instance mutex   leave everything to it, the taskbar too
//   the exit code could not be read          put the taskbar back, stay down
//   it quit, as Karpo asked                  put the taskbar back, stay down: only the exit codes
//                                            of the dock's one way out (DockHost.QuitBy, DockExit);
//                                            0 is a dock ended from outside, and starts again
//   Windows is signing out or shutting down  put the taskbar back, stay down
//   it ran on a test config                  put the taskbar back, stay down (a restart through
//                                            Explorer would come up on the person's own config)
//   anything else                            put the taskbar back, then start it again:
//                                            after 2 s, then 20 s, then 120 s, and never a
//                                            fourth time within 10 minutes of the first
//
// The count of restarts travels with the dock, not the guard: a guard lives only as long as its
// dock, and the restarted dock starts a guard of its own. So the guard starts the dock with
// `--restarts n@first` (how many restarts so far, and when the first of them was, in Unix seconds)
// and the dock hands the same to its guard. Past the window the count starts again.

using System.Globalization;

namespace Karpo.Dock;

/// <summary>The restarts made within the current window: how many, and when the first was (Unix seconds, UTC).</summary>
internal readonly record struct Restarts(int Count, long First)
{
    public const string Switch = "--restarts";

    /// <summary>None yet: a dock that was started, not restarted.</summary>
    public static Restarts None => default;

    /// <summary>As it is written on a command line: <c>n@first</c>.</summary>
    public string Text => string.Create(CultureInfo.InvariantCulture, $"{Count}@{First}");

    /// <summary><see cref="Text"/> read back; anything malformed reads as <see cref="None"/>.</summary>
    public static Restarts Parse(string? text)
    {
        if (text is null)
        {
            return None;
        }
        var at = text.IndexOf('@');
        return at > 0
            && int.TryParse(text.AsSpan(0, at), NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            && long.TryParse(text.AsSpan(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var first)
            && count is > 0 and <= 100
            && first > 0
            ? new Restarts(count, first)
            : None;
    }

    /// <summary>The value after <see cref="Switch"/> in <paramref name="args"/>; <see cref="None"/> without one.</summary>
    public static Restarts In(IReadOnlyList<string> args)
    {
        for (var at = 0; at + 1 < args.Count; at++)
        {
            if (args[at] == Switch)
            {
                return Parse(args[at + 1]);
            }
        }
        return None;
    }
}

internal enum GuardStep
{
    /// <summary>A newer dock has taken over; the guard does nothing more.</summary>
    LeaveToNewer,

    /// <summary>The taskbar goes back and the dock stays down.</summary>
    StayDown,

    /// <summary>The taskbar goes back, and the dock is started again after <see cref="GuardDecision.Wait"/>.</summary>
    Restart,

    /// <summary>The taskbar goes back and the dock stays down: it was restarted as often as the window allows.</summary>
    GiveUp,
}

/// <summary>A decision, the count the restarted dock is handed, and the words for karpo.log.</summary>
internal readonly record struct GuardDecision(GuardStep Step, TimeSpan Wait, Restarts Next, string Says);

internal static class GuardPolicy
{
    /// <summary>The most restarts within <see cref="Window"/>.</summary>
    public const int Most = 3;

    /// <summary>How long restarts count against <see cref="Most"/>, from the first of them.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    /// <summary>The wait before restart <paramref name="restart"/> (1, 2, 3): 2 s, 20 s, 120 s.</summary>
    public static TimeSpan Backoff(int restart) => restart switch
    {
        <= 1 => TimeSpan.FromSeconds(2),
        2 => TimeSpan.FromSeconds(20),
        _ => TimeSpan.FromSeconds(120),
    };

    /// <param name="code">The dock's exit code; null when it could not be read.</param>
    /// <param name="newerDock">A dock holds the instance mutex.</param>
    /// <param name="sessionEnding">Windows is signing out or shutting down.</param>
    /// <param name="isolated">The dock ran on a test config (KARPO_CONFIG_DIR).</param>
    /// <param name="before">What the dock was handed: the restarts before it.</param>
    /// <param name="now">Unix seconds, UTC.</param>
    public static GuardDecision Decide(uint? code, bool newerDock, bool sessionEnding, bool isolated, Restarts before, long now)
    {
        static GuardDecision Stay(string says) => new(GuardStep.StayDown, TimeSpan.Zero, Restarts.None, says);
        if (newerDock)
        {
            return new(GuardStep.LeaveToNewer, TimeSpan.Zero, Restarts.None, "a newer dock has taken over, and the taskbar is left to it");
        }
        if (code is not uint exit)
        {
            return Stay("putting the Windows taskbar back; not starting it again without knowing how it ended");
        }
        if (DockExit.Kind(exit) == ExitKind.Quit)
        {
            return Stay("putting the Windows taskbar back; Karpo asked it to quit, so it stays down");
        }
        if (sessionEnding)
        {
            return Stay("putting the Windows taskbar back; Windows is signing out or shutting down, so it stays down");
        }
        if (isolated)
        {
            return Stay("putting the Windows taskbar back; it ran on a test config (KARPO_CONFIG_DIR), so it stays down");
        }
        var current = before.Count > 0 && now - before.First < Window.TotalSeconds ? before : Restarts.None;
        if (current.Count >= Most)
        {
            return new(GuardStep.GiveUp, TimeSpan.Zero, current,
                $"putting the Windows taskbar back; it was started again {Most} times since {Clock(current.First)}, so it stays down");
        }
        var next = new Restarts(current.Count + 1, current.Count == 0 ? now : current.First);
        var wait = Backoff(next.Count);
        return new(GuardStep.Restart, wait, next,
            $"putting the Windows taskbar back, and starting the dock again in {DockExit.Span(wait)} ({next.Count} of {Most} in {Window.TotalMinutes:0} minutes)");
    }

    /// <summary>Unix seconds as a local time of day.</summary>
    private static string Clock(long unix) =>
        DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
