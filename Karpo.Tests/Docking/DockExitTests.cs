// How the guard tells how a dock ended (Karpo.Dock\DockExit.cs): the exit code's kind and words,
// the one line it writes, and what it reads off a real process that has ended.

using System.Diagnostics;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class DockExitTests
{
    [Theory]
    [InlineData(0x4B510001u, "Quit")]
    [InlineData(0x4B510002u, "Quit")]
    [InlineData(0u, "Killed")]
    [InlineData(2u, "Fatal")]
    [InlineData(1u, "Killed")]
    [InlineData(0xFFFFFFFFu, "Killed")]
    [InlineData(0x40010004u, "Killed")]
    [InlineData(0xC000013Au, "Killed")]
    [InlineData(0xC0000005u, "Crashed")]
    [InlineData(0xC00000FDu, "Crashed")]
    [InlineData(0xC0000374u, "Crashed")]
    [InlineData(0xC0000409u, "Crashed")]
    [InlineData(0xC000027Bu, "Crashed")]
    [InlineData(0xE0434352u, "Crashed")]
    [InlineData(259u, "Unknown")]
    [InlineData(3u, "Unknown")]
    public void Each_exit_code_says_which_kind_of_end_it_was(uint code, string kind)
    {
        Assert.Equal(kind, DockExit.Kind(code).ToString());
        Assert.False(string.IsNullOrWhiteSpace(DockExit.Says(code)));
    }

    /// <summary>
    /// Regression (2026-10-22 14:34:39 and 2026-10-23 05:00:14): the app the dock was started from,
    /// updating itself, ended it from outside with exit code 0, and the guard took that for a quit and
    /// left Karpo down for hours. Only the codes of Karpo's own quit are a quit, and they are who asked.
    /// </summary>
    [Fact]
    public void Only_Karpo_s_own_quit_is_a_quit_and_0_is_an_end_from_outside()
    {
        foreach (var by in Enum.GetValues<Karpo.Core.DockHost.QuitBy>())
        {
            Assert.Equal(ExitKind.Quit, DockExit.Kind((uint)by));
            Assert.StartsWith("it quit (asked by ", DockExit.Says((uint)by), StringComparison.Ordinal);
        }
        Assert.Equal(ExitKind.Killed, DockExit.Kind(0));
        Assert.StartsWith("ended without quitting", DockExit.Says(0), StringComparison.Ordinal);
    }

    /// <summary>The dock's own error exit can't be mistaken for a quit or for what a kill leaves.</summary>
    [Fact]
    public void The_dock_s_error_exit_is_neither_a_quit_nor_a_kill()
    {
        Assert.NotEqual(0, DockExit.Fatal);
        Assert.NotEqual(1, DockExit.Fatal);
        Assert.Equal("Fatal", DockExit.Kind(DockExit.Fatal).ToString());
    }

    [Fact]
    public void Kinds_that_differ_read_differently()
    {
        uint[] codes = [0x4B510001, 0x4B510002, 0, 2, 1, 0xFFFFFFFF, 0xC0000005, 0xC0000409, 3];
        Assert.Equal(codes.Length, codes.Select(DockExit.Says).Distinct().Count());
    }

    [Theory]
    [InlineData(0u, "0")]
    [InlineData(2u, "2")]
    [InlineData(0x4B510001u, "0x4B510001")]
    [InlineData(0xFFFFFFFFu, "0xFFFFFFFF")]
    [InlineData(0xC0000409u, "0xC0000409")]
    public void Small_codes_read_in_decimal_and_status_codes_in_hex(uint code, string reads) =>
        Assert.Equal(reads, DockExit.Code(code));

    [Fact]
    public void The_guard_s_line_says_when_after_how_long_and_how()
    {
        var end = new DockEnd(31096, 1, new DateTime(2026, 9, 26, 22, 37, 29, 961), new DateTime(2026, 9, 27, 1, 0, 24, 123));

        var line = DockExit.Line(end);

        Assert.StartsWith("guard: dock 31096 ended at 01:00:24.123 after 2 h 22 min, exit code 1: ended from outside", line);
    }

    [Fact]
    public void A_line_without_the_code_or_the_start_says_so()
    {
        var line = DockExit.Line(new DockEnd(7, null, null, new DateTime(2026, 9, 27, 1, 0, 24, 5)));

        Assert.Equal("guard: dock 7 ended at 01:00:24.005, its exit code could not be read", line);
    }

    [Theory]
    [InlineData(0.4, "under a second")]
    [InlineData(41, "41 s")]
    [InlineData(60, "1 min")]
    [InlineData(185, "3 min 5 s")]
    [InlineData(3600, "1 h")]
    [InlineData(8574, "2 h 22 min")]
    [InlineData(86400 + 7200 + 59, "1 d 2 h")]
    public void Uptime_reads_in_its_two_largest_units(double seconds, string reads) =>
        Assert.Equal(reads, DockExit.Span(TimeSpan.FromSeconds(seconds)));

    /// <summary>
    /// The guard's own reading, on a real process: the exit code it left, and times that bracket
    /// its run. A code that looks like a crash comes through as it is.
    /// </summary>
    [Theory]
    [InlineData(7, 7u)]
    [InlineData(-1073741819, 0xC0000005u)]
    public void The_guard_reads_how_a_real_process_ended(int exits, uint read)
    {
        var before = DateTime.Now;
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), $"/c exit {exits}")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var pid = (uint)process.Id;

        var handle = DockExit.Open(pid);
        Assert.True(handle != default, "the guard could not open the process");
        var end = DockExit.Wait(handle, pid);

        Assert.Equal(pid, end.Pid);
        Assert.Equal(read, end.Code);
        Assert.NotNull(end.Started);
        Assert.InRange(end.Started!.Value, before.AddSeconds(-5), end.Ended);
        Assert.InRange(end.Ended, before.AddSeconds(-5), DateTime.Now.AddSeconds(5));
    }

    [Fact]
    public void There_is_nothing_to_wait_for_without_a_process() =>
        Assert.True(DockExit.Open(0) == default);
}
