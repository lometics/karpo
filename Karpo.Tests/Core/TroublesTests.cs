// karpo.log (Karpo.Core\Troubles.cs): how often a repeating error may be written (TroubleGate),
// what a fatal entry carries, and the bound on an entry.

using System.Runtime.CompilerServices;
using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class TroubleGateTests
{
    private static readonly DateTime Start = new(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);

    [Fact]
    public void The_first_of_a_trouble_is_written()
    {
        var gate = new TroubleGate(Minute, burst: 20);

        Assert.Equal((true, 0), gate.Let("redraw failed", Start));
    }

    [Fact]
    public void The_same_trouble_again_within_the_window_is_held_back()
    {
        var gate = new TroubleGate(Minute, burst: 20);
        gate.Let("redraw failed", Start);

        Assert.Equal((false, 0), gate.Let("redraw failed", Start.AddSeconds(1)));
        Assert.Equal((false, 0), gate.Let("redraw failed", Start.AddSeconds(59)));
    }

    [Fact]
    public void After_the_window_it_is_written_again_saying_how_many_were_held_back()
    {
        var gate = new TroubleGate(Minute, burst: 20);
        gate.Let("redraw failed", Start);
        gate.Let("redraw failed", Start.AddSeconds(10));
        gate.Let("redraw failed", Start.AddSeconds(20));

        Assert.Equal((true, 2), gate.Let("redraw failed", Start.AddSeconds(60)));
        Assert.Equal((false, 0), gate.Let("redraw failed", Start.AddSeconds(61)));
    }

    /// <summary>The case it is for: an error on every frame, 60 a second for five minutes, writes five lines, and they account for every one.</summary>
    [Fact]
    public void An_error_on_every_frame_writes_a_line_a_minute_that_counts_the_rest()
    {
        var gate = new TroubleGate(Minute, burst: 20);
        var lines = 0;
        var counted = 0;
        var calls = 0;
        for (var at = TimeSpan.Zero; at < TimeSpan.FromMinutes(5); at += TimeSpan.FromMilliseconds(1000.0 / 60))
        {
            calls++;
            var (write, held) = gate.Let("redraw failed", Start + at);
            if (write)
            {
                lines++;
                counted += held;
            }
        }

        Assert.Equal(5, lines);
        // Every call is either a line or counted in a later one, except those after the last line.
        Assert.InRange(calls - lines - counted, 0, 60 * 60);
    }

    [Fact]
    public void Different_troubles_are_written_until_the_burst_is_spent()
    {
        var gate = new TroubleGate(Minute, burst: 3);

        Assert.True(gate.Let("a", Start).Write);
        Assert.True(gate.Let("b", Start.AddSeconds(1)).Write);
        Assert.True(gate.Let("c", Start.AddSeconds(2)).Write);
        Assert.False(gate.Let("d", Start.AddSeconds(3)).Write);
    }

    [Fact]
    public void The_burst_comes_back_with_the_next_window()
    {
        var gate = new TroubleGate(Minute, burst: 2);
        gate.Let("a", Start);
        gate.Let("b", Start);
        Assert.False(gate.Let("c", Start.AddSeconds(30)).Write);

        Assert.True(gate.Let("d", Start.AddSeconds(61)).Write);
    }

    /// <summary>Held back only because the burst was spent: its first line comes in the next window, and says how many came before it.</summary>
    [Fact]
    public void A_trouble_first_met_over_the_burst_is_written_in_the_next_window_with_its_count()
    {
        var gate = new TroubleGate(Minute, burst: 1);
        gate.Let("a", Start);
        Assert.False(gate.Let("b", Start.AddSeconds(5)).Write);
        Assert.False(gate.Let("b", Start.AddSeconds(6)).Write);

        Assert.Equal((true, 2), gate.Let("b", Start.AddSeconds(61)));
    }

    [Fact]
    public void Only_a_bounded_number_of_troubles_is_kept_in_mind()
    {
        var gate = new TroubleGate(Minute, burst: 1000, remembered: 4);
        for (var n = 0; n < 10; n++)
        {
            gate.Let($"trouble {n}", Start.AddSeconds(n));
        }

        Assert.Equal(4, gate.Remembered);
        // The one written longest ago was forgotten, so it counts as new again.
        Assert.Equal((true, 0), gate.Let("trouble 0", Start.AddSeconds(11)));
        // One still in mind is still quiet.
        Assert.False(gate.Let("trouble 9", Start.AddSeconds(12)).Write);
    }
}

public sealed class TroublesTests
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FailsDeepDown() => throw new InvalidOperationException("the inner failure");

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Fails()
    {
        try
        {
            FailsDeepDown();
        }
        catch (Exception inner)
        {
            throw new ApplicationException("the outer failure", inner);
        }
    }

    private static Exception Caught(Action action)
    {
        try
        {
            action();
        }
        catch (Exception error)
        {
            return error;
        }
        throw new InvalidOperationException("nothing was thrown");
    }

    [Fact]
    public void An_error_is_explained_with_its_stack_and_its_inner_error()
    {
        var text = Troubles.Explain(Caught(Fails));

        var lines = text.Split(Environment.NewLine);
        Assert.StartsWith("ApplicationException: the outer failure (HResult 0x", lines[0]);
        Assert.Contains(lines, line => line.StartsWith("    ", StringComparison.Ordinal) && line.Contains(nameof(Fails), StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("  inner InvalidOperationException: the inner failure", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains(nameof(FailsDeepDown), StringComparison.Ordinal));
    }

    [Fact]
    public void An_error_never_thrown_is_explained_without_a_stack() =>
        Assert.Equal("TimeoutException: late (HResult 0x80131505)", Troubles.Explain(new TimeoutException("late")));

    [Fact]
    public void An_entry_is_cut_to_a_bounded_length()
    {
        var huge = new string('x', Troubles.MaxEntry * 3);

        var bounded = Troubles.Bounded(huge);

        Assert.Equal(Troubles.MaxEntry, bounded.Length);
        Assert.EndsWith(Troubles.Cut, bounded);
        Assert.Equal("short", Troubles.Bounded("short"));
    }

    /// <summary>Through the real file: a report that repeats at once is written once; a fatal entry, every time.</summary>
    [Fact]
    public void A_repeating_report_is_written_once_and_a_fatal_one_every_time()
    {
        var report = $"report {Guid.NewGuid():N}";
        var fatal = $"fatal {Guid.NewGuid():N}";
        var error = Caught(Fails);

        Troubles.Report(report, error);
        Troubles.Report(report, error);
        Troubles.Report(report, error);
        Troubles.Fatal(fatal, error);
        Troubles.Fatal(fatal, error);

        var log = Log();
        Assert.Equal(1, Count(log, report + ": ApplicationException: the outer failure"));
        Assert.Equal(2, Count(log, fatal + ": ApplicationException: the outer failure"));
        // The stack came with them.
        Assert.Contains(nameof(FailsDeepDown), log);
    }

    /// <summary>
    /// What a timer's callback or a watcher's event hands its work to: what the work throws is
    /// reported and goes no further, so the pool thread it ran on, and the process, carry on. The
    /// same for work that awaits.
    /// </summary>
    [Fact]
    public async Task A_guarded_start_reports_what_it_throws_and_carries_on()
    {
        var what = $"guarded {Guid.NewGuid():N}";
        var ran = false;
        Troubles.Guard(what, Fails);
        Troubles.Guard(what + " quiet", () => ran = true);
        await Troubles.Guard(what + " awaited", static async () =>
        {
            await Task.Yield();
            Fails();
        });

        // Nothing got out of either; the quiet one ran. (What is written passes Report's gate, which
        // the rest of a full run may have spent, so the log is not read here.)
        Assert.True(ran);
    }

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    /// <summary>karpo.log of this test run, and the one before it if it was started again meanwhile; read while others may be writing.</summary>
    private static string Log()
    {
        var text = "";
        foreach (var path in new[] { Troubles.Previous, Troubles.Path })
        {
            if (File.Exists(path))
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                text += reader.ReadToEnd();
            }
        }
        return text;
    }
}
