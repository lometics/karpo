// What the brightness worker sends each display next (ScreenWrites, docs\widgets.md §495), driven on
// a made-up clock against made-up displays (Wire) that are slow, need a rest after each command, drop
// what comes too soon or at random, refuse, or never answer. The person's case: a fast drag over two
// displays, and one of them left at another level. Nothing here reaches a real screen.

using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class ScreenWritesTests
{
    private const string Panel = @"\\.\DISPLAY1";
    private const string Monitor = @"\\.\DISPLAY2";

    private static readonly ScreenPace Pace = ScreenPace.Default;

    /// <summary>
    /// The person's case: a drag over the laptop's panel (quick, no rest) and a DDC/CI monitor (slow,
    /// 50 ms of rest after each command), a level asked every 8 ms. Both end at the drag's last level,
    /// nothing goes to the monitor inside its rest, the levels in between are skipped, and nothing is
    /// said in karpo.log.
    /// </summary>
    [Fact]
    public void AFastDragEndsWithEveryDisplayAtItsLastLevel()
    {
        var panel = new Wire(level: 50, latency: 5, gap: 0);
        var monitor = new Wire(level: 50, latency: 40, gap: 50);
        var run = new Run(new() { [Panel] = panel, [Monitor] = monitor });
        run.Drag(Walk(seed: 7, from: 50, to: 37, moves: 150), every: 8);
        run.Finish();

        Assert.Equal(37, panel.Level);
        Assert.Equal(37, monitor.Level);
        Assert.Empty(run.Troubles);
        Assert.Null(run.Writes.Wanted(Panel));
        Assert.Null(run.Writes.Wanted(Monitor));
        Assert.Equal(0, monitor.TooSoon);
        Assert.InRange(monitor.Written.Count, 2, 30);
        Assert.Equal(37, monitor.Written[^1]);
        Assert.Equal(37, run.Ended[Monitor]);
    }

    /// <summary>
    /// A monitor that needs more rest than the standard's 50 ms drops what comes sooner without a
    /// word, the drag's last level among them — what left a display behind. The read back once the
    /// drag stops finds it, and the level is sent again in the quiet, where it is taken.
    /// </summary>
    [Fact]
    public void AMonitorThatNeedsMoreRestIsCaughtByTheReadBack()
    {
        var panel = new Wire(level: 50, latency: 5, gap: 0);
        var monitor = new Wire(level: 50, latency: 20, gap: 50) { Needs = 150 };
        var run = new Run(new() { [Panel] = panel, [Monitor] = monitor });
        run.Drag(Walk(seed: 3, from: 50, to: 82, moves: 120), every: 8);
        run.Finish();

        Assert.True(monitor.TooSoon > 0, "the monitor dropped nothing: the case is not played");
        Assert.Equal(82, panel.Level);
        Assert.Equal(82, monitor.Level);
        Assert.Empty(run.Troubles);
    }

    /// <summary>
    /// Writes dropped at random, a third of them: over many drags, every display either ends at the
    /// drag's last level or is said in karpo.log — never left at another level without a word — and
    /// nearly every drag gets there.
    /// </summary>
    [Fact]
    public void DroppedWritesNeverLeaveADisplayStaleWithoutAWord()
    {
        var reached = 0;
        const int drags = 200;
        for (var seed = 1; seed <= drags; seed++)
        {
            var random = new Random(seed);
            var panel = new Wire(level: 50, latency: 5, gap: 0) { DropRate = 0.33, Random = random };
            var monitor = new Wire(level: 50, latency: 40, gap: 50) { DropRate = 0.33, Random = random };
            var run = new Run(new() { [Panel] = panel, [Monitor] = monitor });
            var last = random.Next(0, 101);
            run.Drag(Walk(seed, from: 50, to: last, moves: 60), every: 8);
            run.Finish();
            foreach (var (device, wire) in new[] { (Panel, panel), (Monitor, monitor) })
            {
                Assert.True(wire.Level == last || run.Troubles.Any(line => line.Contains(device, StringComparison.Ordinal)), $"seed {seed}: {device} at {wire.Level}, asked {last}, and nothing said");
                Assert.Null(run.Writes.Wanted(device));
            }
            reached += panel.Level == last && monitor.Level == last ? 1 : 0;
        }
        Assert.True(reached >= drags * 9 / 10, $"only {reached} of {drags} drags got there");
    }

    /// <summary>
    /// An outcome counts only while it is its display's errand on its way: a write's news coming back
    /// after a newer level was asked does not settle the display, the newest is sent next, and the
    /// same news told again — or a read back of a level since replaced — changes nothing.
    /// </summary>
    [Fact]
    public void ALateOutcomeCannotWin()
    {
        var writes = new ScreenWrites(Pace);
        writes.Want(Monitor, 10, now: 0);
        var first = writes.Next(0, out _);
        Assert.Equal(new Errand(Monitor, ErrandKind.Write, 10, first!.Ticket), first);
        // Asked while the first is on its way: nothing more is sent to the display meanwhile.
        writes.Want(Monitor, 50, now: 5);
        writes.Want(Monitor, 30, now: 6);
        Assert.Null(writes.Next(7, out var wake));
        Assert.Equal(long.MaxValue, wake);

        Assert.Null(writes.Wrote(first, took: true, now: 20, gap: 50));
        // Told again, late: nothing.
        Assert.Null(writes.Wrote(first, took: true, now: 21, gap: 0));
        Assert.Null(writes.Next(69, out wake));
        Assert.Equal(70, wake);
        var second = writes.Next(70, out _);
        Assert.Equal((ErrandKind.Write, 30), (second!.Kind, second.Level));
        Assert.Null(writes.Verified(first, said: 10, now: 80, gap: 0, shows: (10, 10)));
        Assert.Equal(30, writes.Wanted(Monitor));

        Assert.Null(writes.Wrote(second, took: true, now: 90, gap: 50));
        var check = writes.Next(90 + Pace.SettleMs, out _);
        Assert.Equal((ErrandKind.Verify, 30), (check!.Kind, check.Level));
        Assert.Equal(new Landed(30, null), writes.Verified(check, said: 30, now: 400, gap: 50, shows: (30, 30)));
        Assert.Null(writes.Wanted(Monitor));
        Assert.Null(writes.Next(10_000, out wake));
        Assert.Equal(long.MaxValue, wake);
    }

    /// <summary>A write the display said it took and never applied: the read back says the old level, and the level is sent again, after a wait.</summary>
    [Fact]
    public void ADroppedWriteIsReadBackAndSentAgain()
    {
        var writes = new ScreenWrites(Pace);
        writes.Want(Monitor, 70, now: 0);
        var write = writes.Next(0, out _)!;
        Assert.Null(writes.Wrote(write, took: true, now: 30, gap: 50));
        // Not read back before the quiet is over.
        Assert.Null(writes.Next(30 + Pace.SettleMs - 1, out var wake));
        Assert.Equal(30 + Pace.SettleMs, wake);
        var check = writes.Next(wake, out _)!;
        Assert.Equal(ErrandKind.Verify, check.Kind);
        Assert.Null(writes.Verified(check, said: 40, now: 400, gap: 50, shows: (70, 70)));
        Assert.Equal(70, writes.Wanted(Monitor));

        Assert.Null(writes.Next(400 + Pace.RetryMs - 1, out wake));
        Assert.Equal(400 + Pace.RetryMs, wake);
        var again = writes.Next(wake, out _)!;
        Assert.Equal((ErrandKind.Write, 70), (again.Kind, again.Level));
        Assert.Null(writes.Wrote(again, took: true, now: 650, gap: 50));
        var recheck = writes.Next(650 + Pace.SettleMs, out _)!;
        Assert.Equal(new Landed(70, null), writes.Verified(recheck, said: 70, now: 1000, gap: 50, shows: (70, 70)));
    }

    /// <summary>
    /// A display in steps — a monitor whose luminance runs to 20, a panel's eleven levels — reads back
    /// the step the caller says it shows for the level asked: that is what it took, after one write,
    /// and nothing is said. Another level — the one before, a write dropped twice in a row — is no
    /// step, however close: sent again to the bound, and said.
    /// </summary>
    [Fact]
    public void ADisplaysOwnStepIsWhatItTook()
    {
        var stepped = new Wire(level: 50, latency: 10, gap: 50) { Step = 5 };
        var run = new Run(new() { [Monitor] = stepped });
        run.Drag([37], every: 8);
        run.Finish();
        Assert.Equal(35, stepped.Level);
        Assert.Equal([37], stepped.Written);
        Assert.Equal(35, run.Ended[Monitor]);
        Assert.Empty(run.Troubles);

        // An answer that is not the display's step, even one close by: never taken for one.
        var near = new ScreenWrites(Pace);
        near.Want(Monitor, 37, now: 0);
        var write = near.Next(0, out _)!;
        Assert.Null(near.Wrote(write, took: true, now: 10, gap: 50));
        var check = near.Next(10 + Pace.SettleMs, out _)!;
        Assert.Null(near.Verified(check, said: 36, now: 400, gap: 50, shows: (35, 35)));
        Assert.Equal(ErrandKind.Write, near.Next(10_000, out _)!.Kind);
        // A panel whose levels are tens, asked 37: 30 or 40, whichever its driver takes.
        Assert.Equal(new Landed(40, null), Settle(40));
        Assert.Equal(new Landed(30, null), Settle(30));

        // Stuck far off: sent again to the bound, and said.
        var stuck = new Wire(level: 50, latency: 10, gap: 50) { Stuck = true };
        run = new Run(new() { [Monitor] = stuck });
        run.Drag([90], every: 8);
        run.Finish();
        Assert.Equal(Pace.Attempts, stuck.Written.Count);
        Assert.Contains(run.Troubles, line => line.Contains(Monitor, StringComparison.Ordinal) && line.Contains("90", StringComparison.Ordinal));
        Assert.Equal(50, run.Ended[Monitor]);

        static Landed? Settle(int said)
        {
            var panel = new ScreenWrites(Pace);
            panel.Want(Panel, 37, now: 0);
            var write = panel.Next(0, out _)!;
            _ = panel.Wrote(write, took: true, now: 5, gap: 0);
            return panel.Verified(panel.Next(5 + Pace.SettleMs, out _)!, said, now: 400, gap: 0, shows: (30, 40));
        }
    }

    /// <summary>
    /// A display that refuses every write — asleep, its handle stale, DDC/CI off — is sent the level
    /// a bounded number of times with growing waits, said once in karpo.log, and then left alone: no
    /// loop. A new level asked tries again, without saying it twice; once it takes one, a later
    /// failure is said again.
    /// </summary>
    [Fact]
    public void ADisplayThatKeepsRefusingIsSaidOnceAndLeftAlone()
    {
        var monitor = new Wire(level: 50, latency: 10, gap: 50) { Refusing = true };
        var run = new Run(new() { [Monitor] = monitor });
        run.Drag([80], every: 8);
        run.Finish();
        Assert.Equal(Pace.Attempts, monitor.Attempts);
        Assert.Single(run.Troubles);
        Assert.Null(run.Writes.Wanted(Monitor));
        Assert.Null(run.Writes.Next(long.MaxValue / 2, out var wake));
        Assert.Equal(long.MaxValue, wake);
        // The waits grow: 200, 400, 800 ms between the tries.
        Assert.Equal([Pace.RetryMs, Pace.RetryMs * 2, Pace.RetryMs * 4], monitor.Starts.Zip(monitor.Starts.Skip(1), (a, b) => b - a - monitor.Latency));

        run.Drag([60], every: 8);
        run.Finish();
        Assert.Equal(Pace.Attempts * 2, monitor.Attempts);
        Assert.Single(run.Troubles);

        monitor.Refusing = false;
        run.Drag([65], every: 8);
        run.Finish();
        Assert.Equal(65, monitor.Level);
        monitor.Refusing = true;
        run.Drag([70], every: 8);
        run.Finish();
        Assert.Equal(2, run.Troubles.Count);
    }

    /// <summary>
    /// A display that takes a write and never answers the read back: read again a bounded number of
    /// times — never written again, since it took the level — and then the level asked stands, said.
    /// </summary>
    [Fact]
    public void ADisplayThatNeverAnswersKeepsTheLevelAsked()
    {
        var monitor = new Wire(level: 50, latency: 10, gap: 50) { Silent = true };
        var run = new Run(new() { [Monitor] = monitor });
        run.Drag([25], every: 8);
        run.Finish();
        Assert.Equal([25], monitor.Written);
        Assert.Equal(Pace.Reads + 1, monitor.Reads);
        Assert.Equal(25, run.Ended[Monitor]);
        Assert.Single(run.Troubles);
    }

    /// <summary>
    /// Nothing goes to a display before its rest after the last command is over, nor is read back
    /// before the person stopped for SettleMs; a display with no rest is sent the next level at once.
    /// </summary>
    [Fact]
    public void NothingGoesBeforeTheRestOrTheQuiet()
    {
        var writes = new ScreenWrites(Pace);
        writes.Want(Monitor, 10, now: 0);
        writes.Want(Panel, 10, now: 0);
        var a = writes.Next(0, out _)!;
        Assert.Null(writes.Wrote(a, took: true, now: 10, gap: a.Device == Monitor ? 50 : 0));
        var b = writes.Next(10, out _)!;
        Assert.NotEqual(a.Device, b.Device);
        Assert.Null(writes.Wrote(b, took: true, now: 20, gap: b.Device == Monitor ? 50 : 0));

        writes.Want(Monitor, 20, now: 25);
        writes.Want(Panel, 20, now: 25);
        var panel = writes.Next(25, out _)!;
        Assert.Equal((Panel, ErrandKind.Write, 20), (panel.Device, panel.Kind, panel.Level));
        Assert.Null(writes.Wrote(panel, took: true, now: 30, gap: 0));
        var monitorFree = (a.Device == Monitor ? 10 : 20) + 50;
        Assert.Null(writes.Next(monitorFree - 1, out var wake));
        Assert.Equal(monitorFree, wake);
        var monitor = writes.Next(monitorFree, out _)!;
        Assert.Equal((Monitor, ErrandKind.Write, 20), (monitor.Device, monitor.Kind, monitor.Level));

        // Levels asked every 100 ms on the panel: never read back while they come.
        for (var at = 100L; at <= 1000; at += 100)
        {
            writes.Want(Panel, (int)(at / 20), at);
            var errand = writes.Next(at, out _);
            Assert.Equal(ErrandKind.Write, errand!.Kind);
            Assert.Null(writes.Wrote(errand, took: true, now: at + 5, gap: 0));
            Assert.Null(writes.Next(at + 99, out _));
        }
        Assert.Null(writes.Next(1005 + Pace.SettleMs - 1, out wake));
        Assert.Equal(1005 + Pace.SettleMs, wake);
        Assert.Equal(ErrandKind.Verify, writes.Next(wake, out _)!.Kind);
    }

    /// <summary>The displays found again: one gone is forgotten; one still there is sent its level again, its old handle's news ignored.</summary>
    [Fact]
    public void DisplaysFoundAgainForgetTheGoneAndSendAgain()
    {
        var writes = new ScreenWrites(Pace);
        writes.Want(Monitor, 40, now: 0);
        writes.Want(Panel, 40, now: 0);
        var flight = writes.Next(0, out _)!;
        writes.Keep([Monitor]);
        Assert.Null(writes.Wanted(Panel));
        Assert.Null(writes.Wrote(flight, took: true, now: 10, gap: 0));
        var again = writes.Next(10, out _)!;
        Assert.Equal((Monitor, ErrandKind.Write, 40), (again.Device, again.Kind, again.Level));
    }

    // --- The made-up displays -----------------------------------------------------------------------

    /// <summary>A random walk of <paramref name="moves"/> levels from <paramref name="from"/>, ending at <paramref name="to"/>.</summary>
    private static int[] Walk(int seed, int from, int to, int moves)
    {
        var random = new Random(seed * 7919);
        var levels = new int[moves];
        var at = from;
        for (var move = 0; move < moves - 1; move++)
        {
            at = Math.Clamp(at + random.Next(-6, 7), 0, 100);
            levels[move] = at;
        }
        levels[^1] = to;
        return levels;
    }

    /// <summary>
    /// A display as a monitor's wire has it: each command takes <see cref="Latency"/>; one that starts
    /// within <see cref="Needs"/> ms of the last's end is dropped without a word (a write said taken,
    /// a read unanswered), and a write may be dropped at random as well.
    /// </summary>
    private sealed class Wire(int level, int latency, int gap)
    {
        public int Level = level;
        public readonly int Latency = latency;

        /// <summary>The rest the display says it needs (what the caller is told).</summary>
        public readonly int Gap = gap;

        /// <summary>The rest it really needs: the one it says, unless set.</summary>
        public int Needs = gap;

        public double DropRate;
        public Random? Random;
        public bool Refusing;
        public bool Silent;
        public bool Stuck;
        public int Step = 1;

        public int Stepped(int level) => (int)Math.Round((double)level / Step, MidpointRounding.AwayFromZero) * Step;
        public int TooSoon;
        public int Attempts;
        public int Reads;
        public readonly List<int> Written = [];
        public readonly List<long> Starts = [];
        private long end = long.MinValue / 2;

        public bool Write(int asked, long now)
        {
            Attempts++;
            Starts.Add(now);
            var soon = now < end + Needs;
            end = now + Latency;
            if (Refusing)
            {
                return false;
            }
            Written.Add(asked);
            if (soon)
            {
                TooSoon++;
                return true;
            }
            if (Stuck || (Random is { } random && random.NextDouble() < DropRate))
            {
                return true;
            }
            Level = Stepped(asked);
            return true;
        }

        public int? Read(long now)
        {
            Reads++;
            var soon = now < end + Needs;
            end = now + Latency;
            if (soon)
            {
                TooSoon++;
            }
            return Silent || soon ? null : Level;
        }
    }

    /// <summary>The worker's loop, on a made-up clock: levels asked at their times, each errand done on its wire.</summary>
    private sealed class Run(Dictionary<string, Wire> wires)
    {
        public ScreenWrites Writes { get; } = new(Pace);

        public List<string> Troubles { get; } = [];

        public Dictionary<string, int?> Ended { get; } = [];

        private readonly Queue<(long At, int Level)> asks = new();
        private long now;

        /// <summary>A drag asking every display each of <paramref name="levels"/>, <paramref name="every"/> ms apart, from now.</summary>
        public void Drag(IReadOnlyList<int> levels, int every)
        {
            for (var move = 0; move < levels.Count; move++)
            {
                asks.Enqueue((now + (move * every), levels[move]));
            }
        }

        /// <summary>Runs until nothing is asked and nothing is due, ever.</summary>
        public void Finish()
        {
            for (var turn = 0; turn < 100_000; turn++)
            {
                while (asks.TryPeek(out var ask) && ask.At <= now)
                {
                    _ = asks.Dequeue();
                    foreach (var device in wires.Keys)
                    {
                        Writes.Want(device, ask.Level, ask.At);
                    }
                }
                var errand = Writes.Next(now, out var wake);
                if (errand is null)
                {
                    var next = Math.Min(wake, asks.TryPeek(out var ask) ? ask.At : long.MaxValue);
                    if (next == long.MaxValue)
                    {
                        return;
                    }
                    now = Math.Max(now, next);
                    continue;
                }
                var wire = wires[errand.Device];
                Landed? landed;
                if (errand.Kind == ErrandKind.Write)
                {
                    var took = wire.Write(errand.Level, now);
                    now += wire.Latency;
                    landed = Writes.Wrote(errand, took, now, wire.Gap);
                }
                else
                {
                    var said = wire.Read(now);
                    now += wire.Latency;
                    landed = Writes.Verified(errand, said, now, wire.Gap, (wire.Stepped(errand.Level), wire.Stepped(errand.Level)));
                }
                if (landed is not null)
                {
                    Ended[errand.Device] = landed.Level;
                    if (landed.Trouble is { } trouble)
                    {
                        Troubles.Add(trouble);
                    }
                }
            }
            Assert.Fail("the writes never came to rest");
        }
    }
}
