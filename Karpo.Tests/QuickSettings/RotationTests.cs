// Rotation lock as Windows' own tile has it (QuickSettings\Services\Rotation.cs): what the PC's
// rotation state means, read the way Windows' handler reads it, and the switch that follows it.
// Every state here is made up; nothing reads or turns this PC's screen.

using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;

namespace Karpo.Tests.QuickSettings;

public sealed class RotationTests
{
    /// <summary>
    /// GetAutoRotationState's answer, as Windows' handler reads it: whether the screen turns at all,
    /// whether it may be switched now, and whether it is locked. The four "not now" states read as
    /// locked, as they do on Windows' tile; being suppressed (0x2) is not one of them.
    /// </summary>
    [Theory]
    [InlineData(0x00u, true, true, false)]
    [InlineData(0x01u, true, true, true)]
    [InlineData(0x02u, true, true, false)]
    [InlineData(0x04u, true, false, true)]
    [InlineData(0x08u, true, false, true)]
    [InlineData(0x40u, true, false, true)]
    [InlineData(0x80u, true, false, true)]
    [InlineData(0x81u, true, false, true)]
    [InlineData(0x10u, false, false, null)]
    [InlineData(0x20u, false, false, null)]
    [InlineData(0x31u, false, false, null)]
    public void TheStateReadsAsWindowsReadsIt(uint state, bool turns, bool switchable, bool? locked)
    {
        Assert.Equal(turns, RotationState.Turns(state));
        Assert.Equal(switchable, RotationState.Switchable(state));
        Assert.Equal(locked, RotationState.Locked(state));
    }

    /// <summary>A PC that gave no answer at all is one whose screen does not turn.</summary>
    [Fact]
    public void NoAnswerIsAScreenThatDoesNotTurn()
    {
        Assert.False(RotationState.Turns(null));
        Assert.False(RotationState.Switchable(null));
        Assert.Null(RotationState.Locked(null));
    }

    /// <summary>A made-up PC: its state, every switch asked of it, and the watch it was given.</summary>
    private sealed class Screen(uint? state)
    {
        public uint? State { get; set; } = state;

        public List<bool> Asked { get; } = [];

        public Action? Changed { get; private set; }

        public RotationSwitch Switch() => new(
            () => State,
            turn =>
            {
                Asked.Add(turn);
                State = turn ? State & ~1u : State | 1u;
                return true;
            },
            heard =>
            {
                Changed = heard;
                return null;
            });
    }

    [Fact]
    public void ATabletSwitchesItsLock()
    {
        var screen = new Screen(0x00);
        using var action = screen.Switch();

        Assert.True(action.Offered);
        Assert.True(action.Available);
        Assert.False(action.On);

        action.Act();
        Assert.Equal([false], screen.Asked);
        Assert.True(action.On);

        action.Act();
        Assert.Equal([false, true], screen.Asked);
        Assert.False(action.On);
    }

    /// <summary>A click is a switch wherever the lock may be switched now, and nowhere else.</summary>
    [Theory]
    [InlineData(0x00u, true)]
    [InlineData(0x01u, true)]
    [InlineData(0x80u, false)]
    [InlineData(0x10u, false)]
    public void AClickIsASwitchWhereItMayBe(uint state, bool switches)
    {
        using var action = new Screen(state).Switch();
        Assert.Equal(switches, action.Switches);
    }

    /// <summary>A convertible folded as a laptop shows the tile greyed and locked, and a click asks nothing.</summary>
    [Fact]
    public void ALaptopShowsItGreyed()
    {
        var screen = new Screen(0x80);
        using var action = screen.Switch();

        Assert.True(action.Offered);
        Assert.False(action.Available);
        Assert.True(action.On);
        action.Act();
        Assert.Empty(screen.Asked);
    }

    /// <summary>Where the screen does not turn, Windows has no tile, and neither has the bar.</summary>
    [Fact]
    public void AScreenThatDoesNotTurnHasNone()
    {
        using var action = new Screen(0x10).Switch();

        Assert.False(action.Offered);
        Assert.False(action.Available);
        Assert.Null(action.On);
    }

    /// <summary>
    /// On the bar the widget is one cell like any switch's, a screen that does not turn included:
    /// placed there, it is greyed rather than taken off (docs\widgets.md §247).
    /// </summary>
    [UiFact]
    public void OnTheBarItIsOneCellWhereTheScreenDoesNotTurnToo()
    {
        Karpo.Tests.Islands.Offscreen.OnStaThread(gfx =>
        {
            var config = new Config();
            var style = new Karpo.Dock.IslandStyle(1f, false, Karpo.Dock.Layout.CellPx(96, new Config { Height = 48 }), Karpo.Dock.Layout.IconPx(96, new Config { Height = 48 }),
                Karpo.Tests.Folders.TestLooks.LookFor(true));
            foreach (var (state, cells) in new (uint State, int Cells)[] { (0x10, 1), (0x00, 1), (0x80, 1) })
            {
                QuickSwitches.Source = which => which == QuickSwitch.RotationLock ? new Screen(state).Switch() : null;
                try
                {
                    var entry = BarKind.RotationLock.New();
                    entry.Id = "rotationLock";
                    using var island = Assert.IsType<Karpo.Dock.SwitchIsland>(Karpo.Dock.Islands.Create(Config.IslandKey(null, entry.Id), entry, gfx, config));
                    Assert.Equal(cells, island.Measure(style).Count);
                    // Greyed where it can't be switched: the screen does not turn (0x10), or not now (0x80).
                    Assert.Equal(state != 0x00, island.UnavailableOn(0) is not null);
                }
                finally
                {
                    QuickSwitches.Source = null;
                }
            }
        });
    }

    /// <summary>A change of Windows' key — the lock switched in its own panel — reaches the widget.</summary>
    [Fact]
    public void AChangeElsewhereIsHeard()
    {
        var screen = new Screen(0x00);
        using var action = screen.Switch();
        var heard = new SemaphoreSlim(0);
        action.Start(() => heard.Release());
        // The watch is set up on the pool: waited for itself, not for a few seconds of it.
        Assert.True(Patience.Until(() => screen.Changed is not null), "the switch never started watching");

        screen.State = 0x01;
        screen.Changed!();

        Assert.True(heard.Wait(Patience.Ceiling));
        Assert.True(action.On);
    }

    /// <summary>What its tooltip says, for each of the three: before anything is asked, a tablet, a laptop, none.</summary>
    [Fact]
    public void TheLineSaysWhy()
    {
        var tablet = new Screen(0x00);
        using var action = tablet.Switch();
        Assert.Equal("Keeps the screen from turning with the PC.", action.Note);
        action.Start(() => { });
        Assert.Equal("Keeps the screen from turning with the PC.", action.Note);

        tablet.State = 0x80;
        Assert.Equal("Windows switches rotation only while the PC is used as a tablet, on one screen.", action.Note);

        tablet.State = 0x10;
        Assert.Equal("This PC's screen doesn't turn.", action.Note);
    }

    /// <summary>
    /// The library greys rotation lock on a PC whose screen does not turn, with the reason, as
    /// Windows leaves its tile out; everything else, and rotation lock where the screen turns, is
    /// offered.
    /// </summary>
    [Fact]
    public void TheLibraryOffersItOnlyWhereTheScreenTurns()
    {
        Assert.Equal("This screen doesn't turn", Catalogue.WhyNotHere(WidgetInfos.RotationLock, turns: false));
        Assert.Null(Catalogue.WhyNotHere(WidgetInfos.RotationLock, turns: true));
        Assert.All(WidgetInfos.All.Where(info => info != WidgetInfos.RotationLock), info => Assert.Null(Catalogue.WhyNotHere(info, turns: false)));
        Assert.Null(Catalogue.WhyNotHere(null, turns: false));
    }
}
