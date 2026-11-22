// The top processes' figures (Karpo.Dock\Performance\ProcessTop.cs, docs\widgets.md §141): a
// process's processor time between two looks over the time that passed times the processors, an
// app's processes together, the System Idle Process left out, the busiest first and level ones in a
// steady order; and one real look at this PC, which reads every process without a handle.

using System.Globalization;
using Karpo.Dock.Performance;

namespace Karpo.Tests.Perf;

public sealed class ProcessTopTests
{
    /// <summary>One second, in the looks' 100-ns units.</summary>
    private const long Second = 10_000_000;

    private static ProcessLook Look(long at, int processors, params ProcessSample[] processes) => new(at, processors, processes);

    private static ProcessSample P(uint id, string image, double seconds, long created = 100) => new(id, created, (long)(seconds * Second), image);

    /// <summary>
    /// A share is the time a process used between the looks over the time that passed times the
    /// processors: two seconds of four processors' one second is half of the whole processor.
    /// </summary>
    [Fact]
    public void AShareIsTheTimeUsedOverTheTimeTimesTheProcessors()
    {
        var before = Look(0, 4, P(10, "busy.exe", 5), P(11, "quiet.exe", 1));
        var now = Look(Second, 4, P(10, "busy.exe", 7), P(11, "quiet.exe", 1.04));
        var ranked = ProcessTop.Rank(before, now);
        Assert.Equal(["busy.exe", "quiet.exe"], ranked.Select(group => group.Key));
        Assert.Equal(0.5f, ranked[0].Share, 4);
        Assert.Equal(0.01f, ranked[1].Share, 4);
        Assert.Equal("50.0%", Invariant(() => ProcessTop.Percent(ranked[0].Share)));
        Assert.Equal("1.0%", Invariant(() => ProcessTop.Percent(ranked[1].Share)));
    }

    /// <summary>
    /// An app's processes are one row, as Task Manager's Apps group them: by image, whatever its case,
    /// their shares summed, every process a member.
    /// </summary>
    [Fact]
    public void AnAppsProcessesAreOneRow()
    {
        var before = Look(0, 2, P(20, "chrome.exe", 1), P(21, "chrome.exe", 1), P(22, "Chrome.EXE", 1), P(30, "code.exe", 1));
        var now = Look(Second, 2, P(20, "chrome.exe", 1.2), P(21, "chrome.exe", 1.1), P(22, "Chrome.EXE", 1.1), P(30, "code.exe", 1.3));
        var ranked = ProcessTop.Rank(before, now);
        Assert.Equal(2, ranked.Count);
        var chrome = ranked[0];
        Assert.Equal("chrome.exe", chrome.Key);
        Assert.Equal(3, chrome.Count);
        Assert.Equal([20u, 21u, 22u], chrome.Members.Select(member => member.Id));
        Assert.Equal(0.2f, chrome.Share, 4);
        Assert.Equal(0.15f, ranked[1].Share, 4);
    }

    /// <summary>The System Idle Process is the processor doing nothing: never a row, however much time it has.</summary>
    [Fact]
    public void TheIdleProcessIsNoRow()
    {
        var before = Look(0, 1, P(ProcessTop.IdleId, "", 100), P(4, "System", 1));
        var now = Look(Second, 1, P(ProcessTop.IdleId, "", 100.9), P(4, "System", 1.1));
        var ranked = ProcessTop.Rank(before, now);
        Assert.Equal(["system"], ranked.Select(group => group.Key));
        Assert.Equal(0.1f, ranked[0].Share, 4);
    }

    /// <summary>
    /// A process the first look did not have started since: all its time is the interval's. An id
    /// used again by another process (another start) is that other process, not the first one going on.
    /// </summary>
    [Fact]
    public void ANewProcessCountsAllItsTimeAndAnIdUsedAgainIsAnother()
    {
        var before = Look(0, 1, P(40, "old.exe", 9, created: 1));
        var now = Look(Second, 1, P(40, "reused.exe", 0.3, created: 2), P(41, "fresh.exe", 0.2, created: 3));
        var ranked = ProcessTop.Rank(before, now);
        Assert.Equal(["reused.exe", "fresh.exe"], ranked.Select(group => group.Key));
        Assert.Equal(0.3f, ranked[0].Share, 4);
        Assert.Equal(0.2f, ranked[1].Share, 4);
    }

    /// <summary>
    /// Busiest first by the share as shown, to a tenth of a percent: apps that both say 0.0 % are
    /// level, and stand by the time used since they started, then by name — never by a difference
    /// nobody can see.
    /// </summary>
    [Fact]
    public void TheOrderIsTheShareAsShownThenTheTimeUsedThenTheName()
    {
        var before = Look(0, 1, P(1, "b.exe", 50), P(2, "a.exe", 10), P(3, "c.exe", 10), P(4, "d.exe", 1));
        // b used a hair more than a since, but both say 0.0 %; d is busy.
        var now = Look(Second, 1, P(1, "b.exe", 50.0002), P(2, "a.exe", 10.0004), P(3, "c.exe", 10), P(4, "d.exe", 1.5));
        var ranked = ProcessTop.Rank(before, now);
        Assert.Equal(["d.exe", "b.exe", "a.exe", "c.exe"], ranked.Select(group => group.Key));
        Assert.Equal(0, ProcessTop.Shown(ranked[1].Share));
    }

    /// <summary>No time between the looks is no figures; a share is never more than the whole, nor less than none.</summary>
    [Fact]
    public void NoTimeIsNoFiguresAndASharesStaysInsideTheWhole()
    {
        var look = Look(Second, 2, P(1, "a.exe", 3));
        Assert.Empty(ProcessTop.Rank(look, look));
        Assert.Empty(ProcessTop.Rank(look, Look(0, 2, P(1, "a.exe", 4))));
        // More time than there was (a clock that lies), and time that went down.
        var ranked = ProcessTop.Rank(Look(0, 1, P(1, "a.exe", 3), P(2, "b.exe", 9)), Look(Second, 1, P(1, "a.exe", 9), P(2, "b.exe", 8)));
        Assert.Equal(1f, ranked[0].Share);
        Assert.Equal(0f, ranked[1].Share);
    }

    /// <summary>The figure is a tenth of a percent, and its room the widest one.</summary>
    [Fact]
    public void TheFigureIsATenthOfAPercent()
    {
        Assert.Equal("0.0%", Invariant(() => ProcessTop.Percent(0)));
        Assert.Equal("0.1%", Invariant(() => ProcessTop.Percent(0.0006f)));
        Assert.Equal("100.0%", Invariant(() => ProcessTop.Percent(1.2f)));
        Assert.Equal("100.0%", Invariant(() => ProcessTop.PercentRoom));
    }

    /// <summary>A name with nothing else to go on is the image without ".exe"; the kernel's are as they are.</summary>
    [Fact]
    public void AStemIsTheImageWithoutExe()
    {
        Assert.Equal("chrome", ProcessTop.StemOf("chrome.exe"));
        Assert.Equal("Memory Compression", ProcessTop.StemOf("Memory Compression"));
        Assert.Equal(".exe", ProcessTop.StemOf(".exe"));
    }

    /// <summary>The kernel's path of an image is the drive's letter, a share's UNC, or nothing known.</summary>
    [Fact]
    public void AKernelPathIsADriveOrAShare()
    {
        var map = new Dictionary<string, string> { [@"\Device\HarddiskVolume3"] = "C:", [@"\Device\HarddiskVolume12"] = "D:" };
        Assert.Equal(@"C:\Windows\explorer.exe", ProcessReader.DosPath(@"\Device\HarddiskVolume3\Windows\explorer.exe", map));
        Assert.Equal(@"D:\Tools\x.exe", ProcessReader.DosPath(@"\Device\HarddiskVolume12\Tools\x.exe", map));
        // Volume 1 is not volume 12's start.
        Assert.Null(ProcessReader.DosPath(@"\Device\HarddiskVolume1\Tools\x.exe", map));
        Assert.Equal(@"\\server\share\app.exe", ProcessReader.DosPath(@"\Device\Mup\server\share\app.exe", map));
        Assert.Equal(@"C:\a.exe", ProcessReader.DosPath(@"\??\C:\a.exe", map));
        Assert.Null(ProcessReader.DosPath(null, map));
    }

    /// <summary>Karpo's own process and the kernel's are never offered to end.</summary>
    [Fact]
    public void KarpoAndTheKernelAreNeverEnded()
    {
        var own = new ProcessMember((uint)Environment.ProcessId, 1);
        Assert.False(ProcessReader.Endable(new ProcessGroup("karpo.exe", "Karpo.exe", 0, 0, [own])));
        Assert.False(ProcessReader.Endable(new ProcessGroup("system", "System", 0, 0, [new ProcessMember(4, 0)])));
        Assert.False(ProcessReader.Endable(new ProcessGroup("none", "none", 0, 0, [])));
        Assert.True(ProcessReader.Endable(new ProcessGroup("x.exe", "x.exe", 0, 0, [new ProcessMember(123_456, 1)])));
    }

    /// <summary>
    /// This PC, read twice: every process, this one among them under its own image and path, the
    /// idle process in the reading and in no row, and the rows' shares together no more than the whole.
    /// </summary>
    [Fact]
    public void ThisPcIsReadWithoutAHandle()
    {
        var first = ProcessReader.Read();
        Assert.NotNull(first);
        Thread.Sleep(300);
        var second = ProcessReader.Read()!;
        Assert.True(second.Processors >= 1);
        Assert.True(second.At > first.At);
        Assert.Contains(second.Processes, process => process.Id == ProcessTop.IdleId);
        var own = Assert.Single(second.Processes, process => process.Id == (uint)Environment.ProcessId);
        Assert.Equal(Path.GetFileName(Environment.ProcessPath), own.Image, ignoreCase: true);
        Assert.True(own.Cpu > 0);

        var ranked = ProcessTop.Rank(first, second);
        Assert.DoesNotContain(ranked, group => group.Members.Any(member => member.Id == ProcessTop.IdleId));
        Assert.InRange(ranked.Sum(group => group.Share), 0f, 1.05f);
        var mine = Assert.Single(ranked, group => group.Members.Any(member => member.Id == (uint)Environment.ProcessId));
        // Its path is an image of its name: this one, or another run's of the same program.
        var path = ProcessReader.PathOf(mine);
        Assert.NotNull(path);
        Assert.Equal(Path.GetFileName(Environment.ProcessPath), Path.GetFileName(path), ignoreCase: true);
        Assert.True(File.Exists(path), path);
    }

    private static string Invariant(Func<string> said)
    {
        var was = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            return said();
        }
        finally
        {
            CultureInfo.CurrentCulture = was;
        }
    }
}
