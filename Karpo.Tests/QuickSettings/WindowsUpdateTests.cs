// Windows Update as the status icon for updates reads it (QuickSettings\Services\WindowsUpdate.cs):
// how the registry's values are read into a state, in every form the orchestrator writes a time,
// and that what is read is Windows Update's own. Every state is made up: nothing here reads this
// PC's Windows Update. The Windows Update widget that showed the restart on its own is gone
// (2026-10-08, WindowsUpdateRemovedTests); what the status icon says and draws is UpdateIndicatorTests'.

using Karpo.Dock.QuickSettings.Services;

namespace Karpo.Tests.QuickSettings;

public sealed class WindowsUpdateTests
{
    /// <summary>
    /// The registry's values into a state: no restart waiting is nothing, whatever times are left
    /// about; a restart waiting takes the person's schedule before Windows' own, and a deadline apart;
    /// a time gone by is no time.
    /// </summary>
    [Fact]
    public void TheOrchestratorsValuesAreReadIntoAState()
    {
        var utc = new DateTime(2026, 10, 4, 13, 0, 0, DateTimeKind.Utc);
        var later = utc.AddHours(14);
        var further = utc.AddDays(2);
        var gone = utc.AddHours(-1);
        Dictionary<string, object?> values = new()
        {
            ["AutoScheduledRebootTime"] = further.ToFileTimeUtc(),
            ["ScheduledRebootTime"] = later.ToFileTimeUtc(),
            ["DeadlineTime"] = gone.ToFileTimeUtc(),
            ["SecondChanceDeadlineTime"] = new DateTimeOffset(further).ToUnixTimeMilliseconds(),
        };
        object? Value(string name) => values.GetValueOrDefault(name);

        Assert.Equal(UpdateState.Nothing, WindowsUpdateRegistry.Of(false, Value, utc));
        var due = WindowsUpdateRegistry.Of(true, Value, utc);
        Assert.True(due is { Known: true, Restart: true });
        Assert.Equal(later.ToLocalTime(), due.Scheduled);
        // The first deadline is gone; the second is to come.
        Assert.Equal(further.ToLocalTime(), due.Deadline);

        // Only Windows' own schedule: that one.
        values.Remove("ScheduledRebootTime");
        Assert.Equal(further.ToLocalTime(), WindowsUpdateRegistry.Of(true, Value, utc).Scheduled);
        // Nothing at all: a restart waiting, no time.
        Assert.Equal(UpdateState.Due, WindowsUpdateRegistry.Of(true, _ => null, utc));
    }

    /// <summary>A time as the orchestrator writes one: a FILETIME, milliseconds or seconds since 1970, a date written out; nothing else.</summary>
    [Fact]
    public void ATimeIsReadInEveryFormTheOrchestratorWrites()
    {
        var at = new DateTime(2026, 10, 5, 1, 30, 0, DateTimeKind.Utc);
        Assert.Equal(at, WindowsUpdateRegistry.TimeOf(at.ToFileTimeUtc()));
        Assert.Equal(at, WindowsUpdateRegistry.TimeOf(BitConverter.GetBytes(at.ToFileTimeUtc())));
        Assert.Equal(at, WindowsUpdateRegistry.TimeOf(new DateTimeOffset(at).ToUnixTimeMilliseconds()));
        Assert.Equal(at, WindowsUpdateRegistry.TimeOf((int)new DateTimeOffset(at).ToUnixTimeSeconds()));
        Assert.Equal(at, WindowsUpdateRegistry.TimeOf("2026-10-05T01:30:00Z"));
        Assert.Null(WindowsUpdateRegistry.TimeOf(null));
        Assert.Null(WindowsUpdateRegistry.TimeOf(0L));
        Assert.Null(WindowsUpdateRegistry.TimeOf(3));
        Assert.Null(WindowsUpdateRegistry.TimeOf("soon"));
        Assert.Null(WindowsUpdateRegistry.TimeOf(new byte[] { 1, 2 }));
        Assert.Null(WindowsUpdateRegistry.TimeOf(ulong.MaxValue));
    }

    /// <summary>
    /// What is watched and read is Windows Update's own, all of it in HKLM's software keys: the
    /// RebootRequired key's parent, and the orchestrator's keys with what is under them. The status
    /// icon for a restart waiting reads the same (Indicators.cs).
    /// </summary>
    [Fact]
    public void WhatIsReadIsWindowsUpdatesOwn()
    {
        Assert.Contains((@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update", false), WindowsUpdateRegistry.Watched);
        Assert.All(WindowsUpdateRegistry.Watched, watched => Assert.StartsWith(@"SOFTWARE\Microsoft\", watched.Key, StringComparison.Ordinal));
        Assert.Contains(@"SOFTWARE\Microsoft\WindowsUpdate\UX", WindowsUpdateRegistry.StateVariables, StringComparison.Ordinal);
        Assert.Equal("ScheduledRebootTime", WindowsUpdateRegistry.ScheduledNames[0]);
    }
}
