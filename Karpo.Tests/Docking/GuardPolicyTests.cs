// What the guard does once its dock has ended (Karpo.Dock\GuardPolicy.cs, DockGuard.cs): the
// decision table, the backoff and its cap, and the count and flags handed from guard to dock to
// guard on the command line.

using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class GuardPolicyTests
{
    /// <summary>A moment, in Unix seconds.</summary>
    private const long Now = 1_790_000_000;

    private static GuardDecision Ended(uint? code, Restarts before = default, long now = Now, bool newer = false, bool ending = false, bool isolated = false) =>
        GuardPolicy.Decide(code, newer, ending, isolated, before, now);

    // --- The count on the command line ---------------------------------------

    [Fact]
    public void Restarts_are_written_as_count_at_first_and_read_back()
    {
        var restarts = new Restarts(2, Now);

        Assert.Equal("2@1790000000", restarts.Text);
        Assert.Equal(restarts, Restarts.Parse(restarts.Text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3")]
    [InlineData("@5")]
    [InlineData("0@5")]
    [InlineData("-1@5")]
    [InlineData("3@0")]
    [InlineData("3@-5")]
    [InlineData("x@y")]
    [InlineData("101@5")]
    [InlineData("3@5@6")]
    [InlineData(" 3@5")]
    public void Anything_malformed_reads_as_no_restarts(string? text) =>
        Assert.Equal(Restarts.None, Restarts.Parse(text));

    [Fact]
    public void The_count_is_found_among_the_arguments()
    {
        Assert.Equal(new Restarts(1, 5), Restarts.In(["--gpu", "--restarts", "1@5", "--no-glass"]));
        Assert.Equal(Restarts.None, Restarts.In(["--gpu"]));
        Assert.Equal(Restarts.None, Restarts.In(["--restarts"]));
    }

    // --- The decision table ------------------------------------------------------

    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(0xC0000005u)]
    public void A_newer_dock_is_left_everything_the_taskbar_too(uint code) =>
        Assert.Equal(GuardStep.LeaveToNewer, Ended(code, newer: true).Step);

    /// <summary>The settings app's Quit Karpo, Karpo.exe --quit, the edit bar's Quit Karpo (§335): the exit codes of the dock's one way out.</summary>
    [Theory]
    [InlineData(0x4B510001u)]
    [InlineData(0x4B510002u)]
    [InlineData(0x4B510003u)]
    public void A_dock_Karpo_asked_to_quit_stays_down(uint code)
    {
        var decision = Ended(code);

        Assert.Equal(GuardStep.StayDown, decision.Step);
        Assert.Contains("asked it to quit", decision.Says, StringComparison.Ordinal);
    }

    /// <summary>
    /// Regression (2026-10-22 and -23): the app the dock was started from, updating itself, ended it
    /// with exit code 0, and the guard left Karpo down for hours. 0 without Karpo's quit is restarted.
    /// </summary>
    [Fact]
    public void A_dock_ended_with_0_without_quitting_is_started_again()
    {
        var decision = Ended(0);

        Assert.Equal(GuardStep.Restart, decision.Step);
        Assert.Equal(TimeSpan.FromSeconds(2), decision.Wait);
        Assert.Equal(GuardStep.GiveUp, Ended(0, new Restarts(3, Now - 60)).Step);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(0x4B510001u)]
    [InlineData(0xC0000005u)]
    public void While_Windows_signs_out_or_shuts_down_no_end_is_restarted(uint code) =>
        Assert.Equal(GuardStep.StayDown, Ended(code, ending: true).Step);

    [Fact]
    public void Without_its_exit_code_it_stays_down() =>
        Assert.Equal(GuardStep.StayDown, Ended(null).Step);

    [Fact]
    public void A_dock_on_a_test_config_stays_down() =>
        Assert.Equal(GuardStep.StayDown, Ended(1, isolated: true).Step);

    /// <summary>A crash, a kill from outside, an error, a code nothing gives: all started again, 2 s later, the first of the window.</summary>
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(0xFFFFFFFFu)]
    [InlineData(0x40010004u)]
    [InlineData(0xC0000005u)]
    [InlineData(0xC0000409u)]
    [InlineData(3u)]
    public void Every_other_end_is_restarted(uint code)
    {
        var decision = Ended(code);

        Assert.Equal(GuardStep.Restart, decision.Step);
        Assert.Equal(TimeSpan.FromSeconds(2), decision.Wait);
        Assert.Equal(new Restarts(1, Now), decision.Next);
    }

    [Fact]
    public void Restarts_come_after_2_then_20_then_120_seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(2), Ended(1).Wait);
        Assert.Equal(TimeSpan.FromSeconds(20), Ended(1, new Restarts(1, Now - 5)).Wait);
        Assert.Equal(TimeSpan.FromSeconds(120), Ended(1, new Restarts(2, Now - 30)).Wait);
        Assert.Equal([2, 20, 120], [.. Enumerable.Range(1, 3).Select(n => GuardPolicy.Backoff(n).TotalSeconds)]);
    }

    [Fact]
    public void The_first_restart_s_time_is_kept_along_the_window()
    {
        Assert.Equal(new Restarts(2, Now - 5), Ended(1, new Restarts(1, Now - 5)).Next);
        Assert.Equal(new Restarts(3, Now - 30), Ended(1, new Restarts(2, Now - 30)).Next);
    }

    [Fact]
    public void A_fourth_restart_within_ten_minutes_is_refused()
    {
        var decision = Ended(0xC0000005, new Restarts(3, Now - 599));

        Assert.Equal(GuardStep.GiveUp, decision.Step);
        Assert.Contains("stays down", decision.Says, StringComparison.Ordinal);
    }

    [Fact]
    public void Ten_minutes_after_the_first_restart_the_count_starts_again()
    {
        var decision = Ended(0xC0000005, new Restarts(3, Now - 600));

        Assert.Equal(GuardStep.Restart, decision.Step);
        Assert.Equal(TimeSpan.FromSeconds(2), decision.Wait);
        Assert.Equal(new Restarts(1, Now), decision.Next);
    }

    /// <summary>
    /// The case the cap is for: a dock that dies the moment it starts. Guard after guard, each
    /// handed the count by the dock before it: three restarts, 2, 20 and 120 s apart, then down.
    /// </summary>
    [Fact]
    public void A_dock_that_dies_at_once_every_time_is_restarted_three_times_then_left_down()
    {
        var now = Now;
        var handed = Restarts.None;
        var waits = new List<double>();
        GuardDecision decision;
        while ((decision = Ended(0xC0000409, handed, now)).Step == GuardStep.Restart)
        {
            waits.Add(decision.Wait.TotalSeconds);
            now += (long)decision.Wait.TotalSeconds + 1;
            handed = decision.Next;
            Assert.True(waits.Count <= GuardPolicy.Most, "restarted past the cap");
        }

        Assert.Equal([2, 20, 120], waits);
        Assert.Equal(GuardStep.GiveUp, decision.Step);
    }

    /// <summary>One that dies now and then, more than ten minutes apart, is always started again.</summary>
    [Fact]
    public void A_dock_that_lives_past_the_window_between_ends_is_always_restarted()
    {
        var now = Now;
        var handed = Restarts.None;
        for (var end = 0; end < 10; end++)
        {
            var decision = Ended(1, handed, now);
            Assert.Equal(GuardStep.Restart, decision.Step);
            Assert.Equal(TimeSpan.FromSeconds(2), decision.Wait);
            handed = decision.Next;
            now += 11 * 60;
        }
    }

    // --- Guard and dock on the command line --------------------------------------

    [Fact]
    public void A_first_dock_s_guard_gets_just_the_process_id() =>
        Assert.Equal(["--guard", "123"], DockGuard.Arguments(123, Restarts.None, []));

    [Fact]
    public void A_restarted_dock_s_guard_gets_the_count_and_the_kept_flags() =>
        Assert.Equal(
            ["--guard", "123", "--restarts", "2@1790000000", "--", "--gpu"],
            DockGuard.Arguments(123, new Restarts(2, Now), ["--gpu"]));

    [Fact]
    public void The_guard_reads_back_what_the_dock_gives_it()
    {
        var parsed = DockGuard.Parse(DockGuard.Arguments(40956, new Restarts(2, Now), ["--gpu", "--no-glass"]));

        Assert.NotNull(parsed);
        Assert.Equal(40956u, parsed.Value.Pid);
        Assert.Equal(new Restarts(2, Now), parsed.Value.Restarts);
        Assert.Equal(["--gpu", "--no-glass"], parsed.Value.Kept);
    }

    [Fact]
    public void The_guard_keeps_only_the_flags_a_restart_keeps()
    {
        var parsed = DockGuard.Parse(["--guard", "7", "--", "--gpu", "--smoke", "--startup", "--gpu"]);

        Assert.Equal(["--gpu"], parsed!.Value.Kept);
        Assert.Equal(Restarts.None, parsed.Value.Restarts);
    }

    [Theory]
    [InlineData("--guard")]
    [InlineData("--guard x")]
    [InlineData("--guard -1")]
    [InlineData("--quit 7")]
    public void Without_a_process_id_there_is_nothing_to_guard(string line) =>
        Assert.Null(DockGuard.Parse(line.Split(' ')));

    [Fact]
    public void The_restarted_dock_gets_the_count_and_the_kept_flags() =>
        Assert.Equal("--restarts 3@1790000000 --gpu --no-glass", DockGuard.DockArguments(new Restarts(3, Now), ["--gpu", "--no-glass"]));

    [Fact]
    public void The_dock_reads_its_count_and_the_flags_it_hands_on()
    {
        var options = Options.FromArgs(["--restarts", "2@5", "--gpu", "--startup", "--gpu"]);

        Assert.Equal(new Restarts(2, 5), options.Restarts);
        Assert.Equal(["--gpu"], options.Kept);
        Assert.Equal(Restarts.None, Options.FromArgs([]).Restarts);
    }
}
