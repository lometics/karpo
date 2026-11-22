// The one dock of a session (Karpo.Dock\DockInstance.cs): the instance mutex a dock takes as it starts,
// and which its guard and the next start read (docs\widgets.md §393). Seen on 2026-10-29 at 11:13: the
// installed dock quit by `Karpo.exe --quit`, its guard said "a newer dock has taken over, and the taskbar is
// left to it" though none ran, and the logon task's start 6 s later ended without a word. A stage dock
// (`Karpo.exe --stage`, a recording's or a test's) had the instance open all along. Every test here uses
// a name of its own, never the person's dock's.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Docking;

public sealed unsafe class DockInstanceTests
{
    private static string Name() => $"Local\\karpo.test.{Guid.NewGuid():N}";

    private static void Let(InstanceTaken taken)
    {
        if (taken.Handle != HANDLE.NULL)
        {
            CloseHandle(taken.Handle);
        }
    }

    /// <summary>The first dock's own; a second start finds it held, and so does the guard.</summary>
    [Fact]
    public void TheFirstDockHoldsItAndASecondStartFindsItHeld()
    {
        var name = Name();
        Assert.False(DockInstance.Held(name));
        var dock = DockInstance.Take(stage: false, name);
        try
        {
            Assert.True(dock.Mine);
            Assert.True(DockInstance.Held(name));
            var second = DockInstance.Take(stage: false, name);
            Let(second);
            Assert.False(second.Mine);
        }
        finally
        {
            Let(dock);
        }
        Assert.False(DockInstance.Held(name));
    }

    /// <summary>
    /// A stage runs beside the person's dock and holds nothing of it. The dock quits while one runs: its guard
    /// puts the taskbar back and leaves the dock down, as for any quit Karpo asked; the logon task's start a
    /// few seconds later is the dock.
    /// </summary>
    [Fact]
    public void AStageRunningAsTheDockQuitsIsNoNewerDock()
    {
        var name = Name();
        var dock = DockInstance.Take(stage: false, name);
        var stage = DockInstance.Take(stage: true, name);
        InstanceTaken next = default;
        try
        {
            Assert.True(dock.Mine);
            Assert.True(stage.Mine);
            Let(dock);
            dock = default;

            var guard = GuardPolicy.Decide((uint)DockHost.QuitBy.Command, DockInstance.Held(name), sessionEnding: false, isolated: false, Restarts.None, 1_790_000_000);
            Assert.Equal(GuardStep.StayDown, guard.Step);
            next = DockInstance.Take(stage: false, name);
            Assert.True(next.Mine);
        }
        finally
        {
            Let(dock);
            Let(stage);
            Let(next);
        }
    }

    /// <summary>A stage started while no dock runs is no dock either: the dock started after it is the dock.</summary>
    [Fact]
    public void AStageStartedFirstLeavesTheDockItsInstance()
    {
        var name = Name();
        var stage = DockInstance.Take(stage: true, name);
        InstanceTaken dock = default;
        try
        {
            Assert.False(DockInstance.Held(name));
            dock = DockInstance.Take(stage: false, name);
            Assert.True(dock.Mine);
        }
        finally
        {
            Let(stage);
            Let(dock);
        }
    }
}
