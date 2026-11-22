// The scenarios (docs/resilience.md §3): each does one thing Windows or the person does to the
// desktop, and says what Karpo must do about it. Every change is undone in the scenario's own
// finally, and the runner puts the displays back after every scenario again, whatever happened.

using System.Diagnostics;

namespace Karpo.Resilience;

/// <param name="CpuLimit">The most of one core Karpo may use over the scenario (0.005 is half a percent); null: only reported.</param>
/// <param name="EndsDock">The scenario ends the dock on purpose: what comes after is judged against the dock that took its place.</param>
internal sealed record Scenario(string Name, string Says, Action<Run> Body, int SettleSeconds = 10, double? CpuLimit = null, bool EndsDock = false);

/// <summary>One scenario's run: what it may do, and what it found.</summary>
internal sealed class Run(Watcher watcher, Baseline baseline)
{
    public Watcher Watcher { get; } = watcher;
    public Baseline Baseline { get; } = baseline;
    public List<string> Said { get; } = [];
    public List<string> Failed { get; } = [];

    public string Primary => Baseline.Seen.Displays.First(d => d.Primary).Device;

    /// <summary>A display other than the primary; null on a machine with one.</summary>
    public string? Second => Baseline.Seen.Displays.FirstOrDefault(d => !d.Primary)?.Device;

    public void Say(string text)
    {
        Said.Add($"+{Watcher.Now.TotalSeconds:0.0} s  {text}");
        Console.WriteLine($"    {text}");
    }

    public void Fail(string text)
    {
        Failed.Add(text);
        Say("FAIL " + text);
    }

    /// <summary>Waits for every rule to hold <paramref name="steady"/> on end; a failure when it does not within <paramref name="limit"/> s.</summary>
    public TimeSpan? Holds(string after, int limit = 10, double steady = 2, bool fullscreenAllowed = true)
    {
        var took = Watcher.Settle(TimeSpan.FromSeconds(limit), TimeSpan.FromSeconds(steady), fullscreenAllowed);
        if (took is { } held)
        {
            Say($"holds {held.TotalMilliseconds:0} ms after {after}");
        }
        else
        {
            Fail($"still broken {limit} s after {after}: {string.Join("; ", Watcher.Open.Select(b => b.Says))}");
        }
        return took;
    }

    /// <summary>Waits until <paramref name="condition"/> on a fresh probe, up to <paramref name="limit"/> s; how long it took, or null.</summary>
    public TimeSpan? Until(Func<Snapshot, bool> condition, double limit)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed.TotalSeconds < limit)
        {
            Watcher.Look();
            if (Watcher.Last is { } last && condition(last))
            {
                return watch.Elapsed;
            }
            Thread.Sleep(50);
        }
        return null;
    }

    public void Need(string? device, string why)
    {
        if (device is null)
        {
            throw new SkipException(why);
        }
    }
}

internal sealed class SkipException(string why) : Exception(why);

internal static class Scenarios
{
    public static readonly Scenario[] All =
    [
        new("idle", "nothing happens for 20 s: every rule holds the whole time, and the dock wakes for nothing", run =>
        {
            run.Watcher.For(TimeSpan.FromSeconds(20));
        }, CpuLimit: 0.01),
        new("mode-second", "the second display goes to 1920x1080 and back (a projector, a game, a remote session)", run =>
        {
            run.Need(run.Second, "needs a second display");
            ModeAndBack(run, run.Second!, 1920, 1080);
        }),
        new("mode-primary", "the primary display goes to 1920x1080 and back", run => ModeAndBack(run, run.Primary, 1920, 1080)),
        new("mode-tiny", "the second display goes to 800x600, smaller than the bar's content, and back", run =>
        {
            run.Need(run.Second, "needs a second display");
            ModeAndBack(run, run.Second!, 800, 600);
        }),
        new("rotate-second", "the second display turns to portrait and back", run =>
        {
            run.Need(run.Second, "needs a second display");
            var (width, height) = Displays.CurrentMode(run.Second!);
            try
            {
                run.Say(Displays.Mode(run.Second!, (int)width, (int)height, TerraFX.Interop.Windows.Windows.DMDO_90));
                run.Holds("turning to portrait");
            }
            finally
            {
                run.Say(Displays.ModeBack(run.Second!));
            }
            run.Holds("turning back");
        }),
        new("scale-second", "the second display's scale goes up a step and back", run =>
        {
            run.Need(run.Second, "needs a second display");
            ScaleAndBack(run, run.Second!);
        }),
        new("scale-primary", "the primary display's scale goes up a step and back", run => ScaleAndBack(run, run.Primary)),
        new("unplug-second", "the second display is taken away (its cable pulled) and comes back", run =>
        {
            run.Need(run.Second, "needs a second display");
            try
            {
                run.Say(Displays.Detach(run.Second!));
                run.Holds("taking the display away");
            }
            finally
            {
                run.Say(Displays.RestoreSaved());
            }
            run.Holds("bringing it back", limit: 15);
        }, SettleSeconds: 15),
        new("unplug-primary", "the primary display is taken away, so the other becomes primary, and comes back", run =>
        {
            run.Need(run.Second, "needs a second display");
            try
            {
                run.Say(Displays.Detach(run.Primary));
                run.Holds("taking the primary away");
            }
            finally
            {
                run.Say(Displays.RestoreSaved());
            }
            run.Holds("bringing it back", limit: 15);
        }, SettleSeconds: 15),
        new("duplicate", "the displays duplicate (Win+P Duplicate) and extend again", run =>
        {
            run.Need(run.Second, "needs a second display");
            try
            {
                run.Say(Displays.Clone());
                run.Holds("duplicating");
            }
            finally
            {
                run.Say(Displays.RestoreSaved());
            }
            run.Holds("extending again", limit: 15);
        }, SettleSeconds: 15),
        new("swap-primary", "the second display becomes primary, then the first again", run =>
        {
            run.Need(run.Second, "needs a second display");
            try
            {
                run.Say(Displays.MakePrimary(run.Second!));
                run.Holds("the primary changing");
            }
            finally
            {
                run.Say(Displays.RestoreSaved());
            }
            run.Holds("the primary changing back", limit: 15);
        }, SettleSeconds: 15),
        new("displays-off", "every display is turned off for 15 s, then lit by the pointer", run =>
        {
            run.Say(Machine.DisplaysOff(15));
            run.Holds("the displays lit again", limit: 20);
        }, SettleSeconds: 20),
        new("fullscreen-borderless", "a borderless full screen window on the second display: its bar makes way, and comes back when it ends", run =>
        {
            var on = run.Second ?? run.Primary;
            FullScreenApp(run, "borderless", on);
        }),
        new("fullscreen-exclusive", "an exclusive Direct3D full screen app at 1280x720 on the second display (the mode changes): nothing of Karpo's in front of it, everything back after", run =>
        {
            var on = run.Second ?? run.Primary;
            FullScreenApp(run, "exclusive", on);
        }, SettleSeconds: 15),
        new("maximized", "a maximized window: the bars stay, and the window keeps out from under them", run =>
        {
            var on = run.Second ?? run.Primary;
            using var app = TestWindow.Start("maximized", on);
            try
            {
                Thread.Sleep(1500);
                run.Holds("a window maximized");
                var last = run.Watcher.Last!;
                var display = last.DisplayOf(on)!;
                if (last.Front is { } front && front.Display == on && !display.Work.Contains(Inset(front.Rect, 16)))
                {
                    run.Fail($"the maximized window at {front.Rect} reaches past the work area {display.Work}");
                }
            }
            finally
            {
                app.Kill();
            }
            run.Holds("the window closing");
        }),
        new("explorer-restart", "Explorer ends as in a crash and winlogon starts it again: its new taskbar is hidden, the strips held again", run =>
        {
            run.Say(Machine.EndExplorer());
            run.Holds("Explorer's restart", limit: 20);
        }, SettleSeconds: 20),
        new("guard-killed", "Karpo's guard is ended from outside (Task Manager): the dock starts another at once", run =>
        {
            if (run.Baseline.Guard is not { } guard)
            {
                throw new SkipException("no guard is running");
            }
            Machine.Kill(guard);
            run.Say($"guard {guard} ended");
            if (run.Until(now => now.Guard is { } next && next != guard, limit: 5) is { } took)
            {
                run.Say($"a new guard after {took.TotalMilliseconds:0} ms");
            }
            else
            {
                run.Fail("no new guard within 5 s: should the dock end now, Windows' taskbar stays hidden");
            }
            run.Holds("the guard's restart");
        }),
        new("dock-killed", "the dock is ended from outside, as a crash or End task: Windows' taskbar shows at once, not auto-hidden, and the guard starts Karpo again, which hides it", run =>
        {
            if (run.Baseline.Dock is not { } dock || run.Baseline.Guard is null)
            {
                throw new SkipException("no dock with a guard is running");
            }
            Machine.Kill(dock);
            run.Say($"dock {dock} ended");
            // §465: no moment without a taskbar of either.
            if (run.Until(now => now.Taskbars.Any(bar => bar.InSight) && Machine.TaskbarAutoHides() == false, limit: 2) is { } shown)
            {
                run.Say($"Windows' taskbar shown, not auto-hidden, {shown.TotalMilliseconds:0} ms after");
            }
            else
            {
                run.Fail($"Windows' taskbar not shown within 2 s of the dock's end (auto-hide {Machine.TaskbarAutoHides()?.ToString() ?? "unknown"})");
            }
            if (run.Until(now => now.Dock is { } next && next != dock && now.Guard is not null && now.Taskbars.All(bar => !bar.Visible) && Baseline.From(now).Stands.Count >= run.Baseline.Stands.Count, limit: 40) is { } back)
            {
                run.Say($"Karpo started again, with its guard, its bars up and Windows' taskbar hidden, {back.TotalSeconds:0.0} s after");
            }
            else
            {
                run.Fail("Karpo not back with its bars and its guard within 40 s of its end");
            }
        }, SettleSeconds: 20, EndsDock: true),
        new("graphics-reset", "the graphics driver restarts (Win+Ctrl+Shift+B): the bars still draw and answer", run =>
        {
            run.Say(Machine.ResetGraphics());
            Thread.Sleep(3000);
            run.Holds("the graphics driver's restart", limit: 15);
        }, SettleSeconds: 15),
        new("storm", "changes in a burst, faster than any settle: two modes, a scale, a display away and back, all within 3 s", run =>
        {
            run.Need(run.Second, "needs a second display");
            var second = run.Second!;
            try
            {
                run.Say(Displays.Mode(second, 1920, 1080));
                Thread.Sleep(300);
                run.Say(Displays.Mode(second, 1280, 720));
                Thread.Sleep(300);
                run.Say(Displays.ModeBack(second));
                if (Displays.Scale(second) is { } scale && scale.Current < scale.Max)
                {
                    Displays.SetScale(second, Step(scale.Current, +1));
                }
                Thread.Sleep(300);
                run.Say(Displays.Detach(second));
                Thread.Sleep(500);
            }
            finally
            {
                run.Say(Displays.RestoreSaved());
            }
            run.Holds("the burst", limit: 20);
        }, SettleSeconds: 20),
        // --- The pointer over the bar -----------------------------------------------------------------
        new("hover-leave", "the pointer rests on buttons until their tooltip or window list shows, then leaves every way a pointer can: each is gone within a second", run => HoverAway(run, busy: false), SettleSeconds: 10),
        new("hover-leave-busy", "the same with every core busy", run =>
        {
            var burn = Load.Child("burn", "120");
            try
            {
                HoverAway(run, busy: true);
            }
            finally
            {
                Load.End(burn);
            }
        }),

        // --- A slow machine, and apps that misbehave -----------------------------------------------
        new("cpu-storm", "every core busy for 30 s (two busy threads a core): Karpo still answers, holds its bars, and adds nothing to the load", run =>
        {
            var burn = Load.Child("burn", "30");
            try
            {
                run.Watcher.For(TimeSpan.FromSeconds(30));
            }
            finally
            {
                Load.End(burn);
            }
        }, CpuLimit: 0.01),
        new("cpu-storm-changes", "every core busy while a display changes mode and is taken away and back: the bars still come back", run =>
        {
            run.Need(run.Second, "needs a second display");
            var burn = Load.Child("burn", "60");
            try
            {
                ModeAndBack(run, run.Second!, 1920, 1080);
                try
                {
                    run.Say(Displays.Detach(run.Second!));
                    run.Holds("taking the display away, every core busy", limit: 20);
                }
                finally
                {
                    run.Say(Displays.RestoreSaved());
                }
                run.Holds("bringing it back, every core busy", limit: 20);
            }
            finally
            {
                Load.End(burn);
            }
        }, SettleSeconds: 20),
        new("memory-pressure", "most of the free memory taken for 25 s, so what Karpo holds is paged out: it still answers and holds", run =>
        {
            var megabytes = (int)Math.Max(512, Load.FreeMegabytes() * 85 / 100);
            run.Say($"taking {megabytes} MB of {Load.FreeMegabytes()} MB free");
            var hog = Load.Child("hog", megabytes.ToString(System.Globalization.CultureInfo.InvariantCulture), "25");
            try
            {
                run.Watcher.For(TimeSpan.FromSeconds(25));
            }
            finally
            {
                Load.End(hog);
            }
        }, SettleSeconds: 20),
        new("hung-app", "an app stops answering (and Windows ghosts it): Karpo, which reads its title and icon, does not hang, and closes nothing", run =>
        {
            var app = TestWindow.Start("hung", run.Second ?? run.Primary);
            try
            {
                run.Watcher.For(TimeSpan.FromSeconds(12));
                if (app.HasExited)
                {
                    run.Fail($"the hung app ended (code {app.ExitCode}): something closed it");
                }
            }
            finally
            {
                Load.End(app);
            }
            run.Holds("the hung app ending");
        }),
        new("title-storm", "an app renames its window a hundred times a second for 15 s: Karpo follows without spending a core on it", run =>
        {
            var app = TestWindow.Start("titles", run.Second ?? run.Primary);
            try
            {
                run.Watcher.For(TimeSpan.FromSeconds(15));
            }
            finally
            {
                Load.End(app);
            }
        }, CpuLimit: 0.05),
        new("progress-storm", "an app's button shows progress changing twenty times a second, with an overlay coming and going, while its display changes scale: no fault, little CPU", run =>
        {
            var on = run.Second ?? run.Primary;
            var app = TestWindow.Start("progress", on);
            try
            {
                run.Watcher.For(TimeSpan.FromSeconds(6));
                ScaleAndBack(run, on);
                ModeAndBack(run, on, 1920, 1080);
                run.Watcher.For(TimeSpan.FromSeconds(4));
            }
            finally
            {
                Load.End(app);
            }
        }, CpuLimit: 0.08),
        new("window-flood", "an app opens 150 windows at once and closes them 4 s later: Karpo keeps up and lets every one go", run =>
        {
            var before = run.Watcher.Last ?? Probe.Take();
            var app = TestWindow.Start("flood", run.Second ?? run.Primary);
            app.WaitForExit(30_000);
            Load.End(app);
            run.Holds("the windows going", limit: 15);
            var after = Probe.Take(ask: false);
            if (after.User > before.User + 30 || after.Handles > before.Handles + 200)
            {
                run.Fail($"Karpo held on: USER {before.User} → {after.User}, handles {before.Handles} → {after.Handles}");
            }
        }, SettleSeconds: 15),
        new("sleep", "the machine sleeps (S3) for 45 s and a timer wakes it", run =>
        {
            run.Say(Machine.Suspend(hibernate: false, 45));
            run.Holds("waking", limit: 30);
        }, SettleSeconds: 30),
        new("hibernate", "the machine hibernates (S4) and a timer wakes it after 90 s", run =>
        {
            run.Say(Machine.Suspend(hibernate: true, 90));
            run.Holds("coming back from hibernation", limit: 45);
        }, SettleSeconds: 45),
        new("start-placement", "Windows' Start opened by each bar's Start button (and with --keys by the Windows key and Ctrl+Esc), measured against the bar, the button and where Karpo places it (widgets.md §666); by name only", StartScenario.Run),
    ];

    private static readonly int[] Percents = [100, 125, 150, 175, 200, 225, 250, 300];

    private static int Step(int percent, int by) => Percents[Math.Clamp(Array.IndexOf(Percents, percent) + by, 0, Percents.Length - 1)];

    private static Box Inset(Box box, int by) => new(box.Left + by, box.Top + by, box.Right - by, box.Bottom - by);

    /// <summary>The ways a pointer leaves a button, each from a fresh hover.</summary>
    private static readonly string[] Aways = ["jump", "other display", "swipe", "across the tooltip", "slide off the end"];

    private static void HoverAway(Run run, bool busy)
    {
        var was = Hover.Where();
        try
        {
            var device = run.Second ?? run.Primary;
            var display = run.Baseline.Seen.DisplayOf(device)!;
            var other = run.Baseline.Seen.Displays.FirstOrDefault(d => d.Device != device);
            var targets = Hover.Targets(run, display, most: 6);
            run.Say($"{targets.Count} places on {device}'s bar show something: {string.Join(", ", targets.Select(t => t.Shows))}");
            if (targets.Count == 0)
            {
                run.Fail("resting the pointer on the bar showed no tooltip and no window list anywhere");
                return;
            }
            var away = (X: display.Bounds.Left + display.Bounds.Width / 2, Y: display.Bounds.Top + display.Bounds.Height / 3);
            var slowest = 0.0;
            foreach (var (x, y, shows) in targets)
            {
                foreach (var how in Aways)
                {
                    Hover.To(x, y);
                    if (run.Until(s => Hover.Shown(s) is not null, busy ? 4 : 2) is null)
                    {
                        continue;
                    }
                    var shown = Hover.Shown(run.Watcher.Last!)!;
                    switch (how)
                    {
                        case "jump":
                            // No move between: SetCursorPos alone, as a touchpad jump or a remote session's pointer.
                            TerraFX.Interop.Windows.Windows.SetCursorPos(away.X, away.Y);
                            break;
                        case "other display":
                            if (other is null)
                            {
                                continue;
                            }
                            TerraFX.Interop.Windows.Windows.SetCursorPos(other.Bounds.Left + other.Bounds.Width / 2, other.Bounds.Top + other.Bounds.Height / 2);
                            break;
                        case "swipe":
                            Hover.Swipe(0, -700);
                            break;
                        case "across the tooltip":
                            Hover.To((shown.Rect.Left + shown.Rect.Right) / 2, (shown.Rect.Top + shown.Rect.Bottom) / 2);
                            Thread.Sleep(150);
                            Hover.Swipe(0, -700);
                            break;
                        case "slide off the end":
                            // Along the bar and off its outer edge at the display's side, where no display is.
                            Hover.To(display.Bounds.Right - 2, y);
                            Thread.Sleep(80);
                            Hover.Swipe(400, 0);
                            break;
                    }
                    var gone = run.Until(s => Hover.Shown(s) is null || s.PointerOnKarpo, 3);
                    var last = run.Watcher.Last!;
                    if (gone is null || (Hover.Shown(last) is not null && !last.PointerOnKarpo))
                    {
                        run.Fail($"\"{shows}\" at {x},{y} still shown 3 s after the pointer left ({how})");
                        continue;
                    }
                    slowest = Math.Max(slowest, gone.Value.TotalMilliseconds);
                    if (gone.Value > TimeSpan.FromSeconds(1))
                    {
                        run.Fail($"\"{shows}\" at {x},{y} took {gone.Value.TotalMilliseconds:0} ms to go after the pointer left ({how})");
                    }
                }
            }
            run.Say($"every hover window gone after the pointer left; the slowest in {slowest:0} ms");
        }
        finally
        {
            TerraFX.Interop.Windows.Windows.SetCursorPos(was.X, was.Y);
        }
    }

    private static void ModeAndBack(Run run, string device, int width, int height)
    {
        try
        {
            run.Say(Displays.Mode(device, width, height));
            run.Holds($"the mode change to {width}x{height}");
        }
        finally
        {
            run.Say(Displays.ModeBack(device));
        }
        run.Holds("the mode going back");
    }

    private static void ScaleAndBack(Run run, string device)
    {
        if (Displays.Scale(device) is not { } scale)
        {
            throw new SkipException("the scale cannot be read");
        }
        var up = scale.Current < scale.Max ? Step(scale.Current, +1) : Step(scale.Current, -1);
        try
        {
            run.Say(Displays.SetScale(device, up) ? $"{device} scale {scale.Current} → {up} %" : $"{device} scale NOT set");
            run.Holds($"the scale going to {up} %");
        }
        finally
        {
            run.Say(Displays.SetScale(device, scale.Current) ? $"{device} scale back to {scale.Current} %" : "scale NOT put back");
        }
        run.Holds("the scale going back");
    }

    private static void FullScreenApp(Run run, string kind, string on)
    {
        using var app = TestWindow.Start(kind, on);
        try
        {
            // The app in front, covering the display: no bar of Karpo's may be seen over it.
            var front = run.Until(s => s.Front is { } f && f.Process == app.Id && s.DisplayOf(on) is { } d && Rules.FullScreen(f, d), 10);
            if (front is null)
            {
                run.Fail($"the test window never came to the front full screen: front is {run.Watcher.Last?.Front} (the app is {app.Id}, {(app.HasExited ? $"ended {app.ExitCode}" : "running")})");
                return;
            }
            var gone = run.Until(s => !s.BarsOn(on).Any(), 3);
            if (gone is null)
            {
                run.Fail($"{on}'s bar still shows 3 s after a full screen app came to the front");
            }
            else
            {
                run.Say($"{on}'s bar made way {gone.Value.TotalMilliseconds:0} ms after the app came to the front");
            }
            run.Watcher.For(TimeSpan.FromSeconds(5));
            // The other display keeps its bar.
            foreach (var other in run.Baseline.Stands.Keys.Where(d => d != on))
            {
                if (run.Watcher.Last is { } last && !last.BarsOn(other).Any() && last.DisplayOf(other) is { } facts && !Rules.FullScreen(last.Front, facts))
                {
                    run.Fail($"{other}'s bar went too, while the full screen app is on {on}");
                }
            }
        }
        finally
        {
            app.Kill();
            app.WaitForExit(5000);
        }
        run.Holds("the full screen app ending");
    }
}
