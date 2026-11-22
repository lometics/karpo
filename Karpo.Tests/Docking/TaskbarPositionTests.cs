// Windows' own taskbar's edge (Karpo.Dock\TaskbarPosition.cs, docs/widgets.md §591): how this Windows keeps it
// (HKCU ...\Explorer\Advanced, TaskbarLocation: ABE_*'s numbers), whether it has edges at all (Settings' position
// setting, the DisableTaskbarPosition policy), and how it is moved (written, then Explorer told as Settings tells it).
// Against a fake store: no test reads or writes this PC's registry, and the live store
// refuses to on a test's run.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Docking;

public sealed class TaskbarPositionTests
{
    /// <summary>This Windows as the store sees it, and what was asked of it.</summary>
    private sealed class FakeStore : ITaskbarPositionStore
    {
        public bool Has { get; set; } = true;
        public uint? Locked { get; set; }
        public uint? Value { get; set; }
        public bool Writes { get; set; } = true;
        public bool Hears { get; set; } = true;
        public List<string> Asked { get; } = [];

        public bool Offered() => Has;

        public uint? Policy() => Locked;

        public uint? Location() => Value;

        public bool Write(uint location)
        {
            Asked.Add($"write {location}");
            if (Writes)
            {
                Value = location;
            }
            return Writes;
        }

        public bool Tell(uint location)
        {
            Asked.Add($"tell {location}");
            return Hears;
        }
    }

    /// <summary>TaskbarLocation's numbers are ABE_*'s: 0 left, 1 top, 2 right, 3 bottom; none, or one unknown, the bottom.</summary>
    [Fact]
    public void TheEdgesAreTaskbarLocationsNumbers()
    {
        Assert.Equal(0u, TaskbarPosition.ValueOf(Edge.Left));
        Assert.Equal(1u, TaskbarPosition.ValueOf(Edge.Top));
        Assert.Equal(2u, TaskbarPosition.ValueOf(Edge.Right));
        Assert.Equal(3u, TaskbarPosition.ValueOf(Edge.Bottom));
        foreach (var edge in BarLooks.Edges)
        {
            Assert.Equal(edge, TaskbarPosition.EdgeOf(TaskbarPosition.ValueOf(edge)));
        }
        Assert.Equal(Edge.Bottom, TaskbarPosition.EdgeOf(null));
        Assert.Equal(Edge.Bottom, TaskbarPosition.EdgeOf(7));
    }

    /// <summary>
    /// A Windows whose Settings has no taskbar position (before 26300.8493), or whose policy holds the taskbar at the
    /// bottom, has no edge to read or move: nothing is written, nothing told.
    /// </summary>
    [Fact]
    public void AWindowsWithNoPositionOrAPolicyIsNeverMoved()
    {
        var older = new FakeStore { Has = false, Value = 0 };
        Assert.Null(TaskbarPosition.Current(older));
        Assert.Equal(TaskbarPosition.Moved.NotOffered, TaskbarPosition.Move(older, Edge.Left));

        var held = new FakeStore { Locked = 1 };
        Assert.Null(TaskbarPosition.Current(held));
        Assert.Equal(TaskbarPosition.Moved.NotOffered, TaskbarPosition.Move(held, Edge.Right));

        Assert.Empty(older.Asked);
        Assert.Empty(held.Asked);
        // A policy set to 0 holds nothing.
        Assert.Equal(Edge.Bottom, TaskbarPosition.Current(new FakeStore { Locked = 0 }));
    }

    [Fact]
    public void ATaskbarIsMovedAsSettingsMovesItWrittenThenTold()
    {
        var store = new FakeStore();
        Assert.Equal(Edge.Bottom, TaskbarPosition.Current(store));
        Assert.Equal(TaskbarPosition.Moved.Done, TaskbarPosition.Move(store, Edge.Left));
        Assert.Equal(["write 0", "tell 0"], store.Asked);
        Assert.Equal(Edge.Left, TaskbarPosition.Current(store));
        Assert.Equal("Windows' taskbar moved to the left", TaskbarPosition.Says(TaskbarPosition.Moved.Done, Edge.Left));
    }

    /// <summary>
    /// §600: TaskbarLocation saying left, top or right (the person's choice, or a Karpo before §600 that put it on its
    /// bar's edge at its quit): the takeover's move to the bottom writes 3 and tells Explorer.
    /// </summary>
    [Theory]
    [InlineData(0u)]
    [InlineData(1u)]
    [InlineData(2u)]
    public void ATaskbarOnAnyOtherEdgeIsParkedAtTheBottom(uint value)
    {
        var store = new FakeStore { Value = value };
        Assert.Equal(TaskbarPosition.Moved.Done, TaskbarPosition.Move(store, TaskbarPosition.Parked));
        Assert.Equal(["write 3", "tell 3"], store.Asked);
        Assert.Equal(Edge.Bottom, TaskbarPosition.Current(store));
    }

    /// <summary>Where it stands already nothing is asked: a start, a quit, a reconcile each ask, and Explorer lays its taskbar out at each word.</summary>
    [Fact]
    public void ATaskbarWhereItStandsIsNotMovedAgain()
    {
        var store = new FakeStore { Value = 2 };
        Assert.Equal(TaskbarPosition.Moved.Already, TaskbarPosition.Move(store, Edge.Right));
        Assert.Equal(TaskbarPosition.Moved.Already, TaskbarPosition.Move(new FakeStore(), Edge.Bottom));
        Assert.Empty(store.Asked);
        Assert.Null(TaskbarPosition.Says(TaskbarPosition.Moved.Already, Edge.Right));
    }

    [Fact]
    public void ARefusedValueIsNotToldAndAnUnheardOneIsSaid()
    {
        var refused = new FakeStore { Writes = false };
        Assert.Equal(TaskbarPosition.Moved.Refused, TaskbarPosition.Move(refused, Edge.Top));
        Assert.Equal(["write 1"], refused.Asked);

        var unheard = new FakeStore { Hears = false };
        Assert.Equal(TaskbarPosition.Moved.Untold, TaskbarPosition.Move(unheard, Edge.Top));
        Assert.Contains("next start", TaskbarPosition.Says(TaskbarPosition.Moved.Untold, Edge.Top), StringComparison.Ordinal);
    }

    /// <summary>The tests run on a config of their own: the live store says there is no position, and writes nothing.</summary>
    [Fact]
    public void ATestsRunNeverTouchesWindowsOwnTaskbarPosition()
    {
        Assert.True(Config.IsIsolated, "The tests are meant to run on a config of their own.");
        var live = new LiveTaskbarPosition();
        Assert.False(live.Offered());
        Assert.False(live.Write(0));
        Assert.Null(TaskbarPosition.Current(live));
        Assert.Equal(TaskbarPosition.Moved.NotOffered, TaskbarPosition.Move(live, Edge.Left));
    }
}
