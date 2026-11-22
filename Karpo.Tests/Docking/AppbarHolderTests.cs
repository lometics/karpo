// One bar's hold on its strip of screen (Karpo.Dock\AppbarHolder.cs): what it sends the shell, what a
// look at the work area finds, and how a repair goes — against the made-up desktop (BarWorld), so
// nothing here reaches the real shell or the real work area. When to look and when to repair is the
// reconcile's, and is BarKeeperTests'.
//
// The regression all of this is for: some starts ended with no space reserved on any display — the
// work area the whole screen, maximized windows under the bar — and nothing noticed or tried again
// until Karpo was started again.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Docking.BarWorld;

namespace Karpo.Tests.Docking;

public sealed class AppbarHolderTests
{
    private static RECT Primary => Rect(0, 0, 3840, 2160);

    /// <summary>A desktop with one display and Explorer's taskbar auto-hidden already, and one bar registered on it.</summary>
    private static (BarWorld World, KeptBar Bar) One_()
    {
        var world = new BarWorld(two: false) { KeptAutoHide = true };
        ((ITaskbarShell)world).SetState(1);
        var bar = world.AddBar(One);
        return (world, bar);
    }

    private static bool Hold(KeptBar bar, BarWorld world, Edge edge = Edge.Bottom, int thickness = Band) =>
        bar.Holder.Hold(edge, thickness, world.Of(One).Bounds, world.Of(One).Monitor);

    private static int Count(BarWorld world, string call) => world.Calls.Count(made => made.Call == call);

    // --- Holding it --------------------------------------------------------------------------------

    [Fact]
    public void AStripTheShellTakesIsHeld()
    {
        var (world, bar) = One_();
        Assert.True(Hold(bar, world));
        Assert.Equal(2160 - Band, world.Of(One).Work.bottom);
        Assert.Equal(AppbarState.Held, bar.Holder.Verify().State);
    }

    /// <summary>Only a change is sent: each makes the shell work every work area out again.</summary>
    [Fact]
    public void OnlyAChangeIsSent()
    {
        var (world, bar) = One_();
        Hold(bar, world);
        var setpos = Count(world, "setpos");
        Assert.False(Hold(bar, world));
        Assert.False(Hold(bar, world));
        Assert.Equal(setpos, Count(world, "setpos"));
        Assert.True(Hold(bar, world, thickness: 72));
        Assert.Equal(setpos + 1, Count(world, "setpos"));
    }

    /// <summary>The regression's first way in: ABM_NEW refused. Nothing is held, and the look says so — the repair registers it again.</summary>
    [Fact]
    public void ARefusedRegistrationIsSeenAndRepaired()
    {
        var world = new BarWorld(two: false) { RefuseNew = 1 };
        var bar = world.AddBar(One);
        Assert.False(bar.Holder.Registered);
        // A bar the shell does not know keeps what it is to hold, and asks nothing of the shell for it.
        Assert.False(Hold(bar, world));
        Assert.Equal(AppbarState.Unregistered, bar.Holder.Verify().State);

        bar.Holder.Repair(world.Now, "not registered", AppbarState.Unregistered);
        Assert.True(bar.Holder.Registered);
        Assert.Equal(AppbarState.Held, bar.Holder.Verify().State);
    }

    /// <summary>The regression's second way in: ABM_SETPOS answered TRUE and did nothing. The look sees it; the repair sends it again.</summary>
    [Fact]
    public void AnIgnoredPositionIsSeenAndSentAgain()
    {
        var (world, bar) = One_();
        world.DropsEverything = true;
        Hold(bar, world);
        Assert.Equal(AppbarState.Missing, bar.Holder.Verify().State);

        world.DropsEverything = false;
        bar.Holder.Repair(world.Now, "missing");
        Assert.Equal(AppbarState.Held, bar.Holder.Verify().State);
        // The cheap repair first: no registration again.
        Assert.Equal(1, Count(world, "register"));
    }

    /// <summary>
    /// A shell that keeps the windows on the strip however it is asked (a dark screen, §212): the
    /// second repair in a row registers again and sets the work area directly — what the shell would
    /// have set, the work area as it is less the strip.
    /// </summary>
    [Fact]
    public void TheSecondRepairInARowSetsTheWorkAreaDirectly()
    {
        var (world, bar) = One_();
        Hold(bar, world);
        world.Stall(240_000);
        world.ResetsWorkArea(One, says: false);
        bar.Holder.Repair(world.Now, "missing");
        Assert.Equal(AppbarState.Missing, bar.Holder.Verify().State);
        Assert.Equal(0, Count(world, "workarea"));

        bar.Holder.Repair(world.Now, "missing");
        Assert.Equal(1, Count(world, "workarea"));
        Assert.Equal(Rect(0, 0, 3840, 2160 - Band), world.Of(One).Work);
        Assert.Equal(AppbarState.Held, bar.Holder.Verify().State);
        bar.Holder.Settled();
        Assert.Equal(0, bar.Holder.Failures);
    }

    [Theory]
    [InlineData(1, AppbarState.Missing, AppbarRepair.Position)]
    [InlineData(2, AppbarState.Missing, AppbarRepair.Direct)]
    [InlineData(4, AppbarState.Missing, AppbarRepair.Direct)]
    [InlineData(1, AppbarState.Unregistered, AppbarRepair.Position)]
    [InlineData(3, AppbarState.Unregistered, AppbarRepair.Direct)]
    [InlineData(1, AppbarState.Short, AppbarRepair.Position)]
    [InlineData(3, AppbarState.Short, AppbarRepair.Position)]
    public void TheRepairLadder(int attempt, AppbarState state, AppbarRepair expected) =>
        Assert.Equal(expected, AppbarGuard.Repair(attempt, state));

    /// <summary>At most the budget in its window, and the budget whole again at a disturbance or once the window passed.</summary>
    [Fact]
    public void RepairsAreBudgeted()
    {
        var (world, bar) = One_();
        Hold(bar, world);
        for (var repair = 0; repair < AppbarGuard.RepairsPerWindow; repair++)
        {
            Assert.True(bar.Holder.MayRepair(world.Now));
            bar.Holder.Repair(world.Now, "missing");
        }
        Assert.False(bar.Holder.MayRepair(world.Now));
        Assert.False(bar.Holder.MayRepair(world.Now + AppbarGuard.RepairWindowMs - 1));
        Assert.True(bar.Holder.MayRepair(world.Now + AppbarGuard.RepairWindowMs));

        bar.Holder.Repair(world.Now + AppbarGuard.RepairWindowMs, "missing");
        bar.Holder.Refill();
        Assert.True(bar.Holder.MayRepair(world.Now + AppbarGuard.RepairWindowMs));
    }

    /// <summary>A strip of nothing — the bar hides — holds nothing by design; it stays registered.</summary>
    [Fact]
    public void AHidingBarHoldsNothingAndStaysRegistered()
    {
        var (world, bar) = One_();
        Hold(bar, world);
        Hold(bar, world, thickness: 0);
        Assert.Equal(Primary, world.Of(One).Work);
        Assert.Equal(AppbarState.Idle, bar.Holder.Verify().State);
        Assert.True(bar.Holder.Registered);

        // And back: the one registration throughout.
        Hold(bar, world);
        Assert.Equal(2160 - Band, world.Of(One).Work.bottom);
        Assert.Equal(1, Count(world, "register"));
    }

    /// <summary>
    /// A strip moved out towards the edge — thinner, or from beside Explorer's taskbar — is held only
    /// once the work area comes out to it: a work area still ending where the old strip was is the
    /// move dropped, and is asked for again (never the work area set larger than the shell's).
    /// </summary>
    [Fact]
    public void AStripMovedOutIsHeldOnlyWhereTheWorkAreaEndsAtIt()
    {
        var (world, bar) = One_();
        Hold(bar, world, thickness: 72);
        world.DropsEverything = true;
        Hold(bar, world);
        Assert.Equal(2160 - 72, world.Of(One).Work.bottom);
        Assert.Equal(AppbarState.Short, bar.Holder.Verify().State);

        bar.Holder.Repair(world.Now, "short", AppbarState.Short);
        bar.Holder.Repair(world.Now, "short", AppbarState.Short);
        Assert.Equal(0, Count(world, "workarea"));
        Assert.Equal(AppbarState.Short, bar.Holder.Verify().State);

        world.DropsEverything = false;
        bar.Holder.Repair(world.Now, "short", AppbarState.Short);
        Assert.Equal(AppbarState.Held, bar.Holder.Verify().State);
        Assert.Equal(2160 - Band, world.Of(One).Work.bottom);
    }

    /// <summary>The same for a strip given back: the work area must come out to the edge.</summary>
    [Fact]
    public void AStripGivenBackIsSeenGivenBack()
    {
        var (world, bar) = One_();
        Hold(bar, world);
        world.DropsEverything = true;
        Hold(bar, world, thickness: 0);
        Assert.Equal(AppbarState.Short, bar.Holder.Verify().State);
        world.DropsEverything = false;
        bar.Holder.Repair(world.Now, "short", AppbarState.Short);
        Assert.Equal(AppbarState.Idle, bar.Holder.Verify().State);
        Assert.Equal(Primary, world.Of(One).Work);
    }

    /// <summary>A work area smaller than needed — another appbar further in — is someone else's, and held.</summary>
    [Fact]
    public void AWorkAreaThatStopsShortOfTheStripCountsAsHeld()
    {
        var strip = Rect(0, 2106, 3840, 2160);
        Assert.True(AppbarGuard.Excludes(Rect(0, 0, 3840, 2106), strip, Edge.Bottom));
        Assert.True(AppbarGuard.Excludes(Rect(0, 0, 3840, 2000), strip, Edge.Bottom));
        Assert.False(AppbarGuard.Excludes(Rect(0, 0, 3840, 2160), strip, Edge.Bottom));
        Assert.False(AppbarGuard.Excludes(Rect(0, 0, 3840, 2107), strip, Edge.Bottom));
        Assert.True(AppbarGuard.EndsAt(Rect(0, 0, 3840, 2106), strip, Edge.Bottom));
        Assert.False(AppbarGuard.EndsAt(Rect(0, 0, 3840, 2000), strip, Edge.Bottom));
    }

    [Theory]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    [InlineData(Edge.Bottom)]
    public void EverySideIsHeld(Edge edge)
    {
        var (world, bar) = One_();
        Hold(bar, world, edge);
        Assert.True(AppbarGuard.Excludes(world.Of(One).Work, bar.Holder.Reserved!.Value, edge));
        Assert.True(AppbarGuard.EndsAt(world.Of(One).Work, bar.Holder.Reserved!.Value, edge));
        Assert.Equal(AppbarState.Held, bar.Holder.Verify().State);
    }

    [Theory]
    [InlineData(Edge.Bottom, 2120, 2106, true)]
    [InlineData(Edge.Bottom, 2034, 2106, false)]
    [InlineData(Edge.Top, 40, 54, true)]
    [InlineData(Edge.Left, 40, 54, true)]
    [InlineData(Edge.Right, 3800, 3786, true)]
    [InlineData(Edge.Right, 3700, 3786, false)]
    public void WhichWayIsOut(Edge edge, int inner, int before, bool outward)
    {
        RECT Strip(int at) => edge switch
        {
            Edge.Top => Rect(0, 0, 3840, at),
            Edge.Left => Rect(0, 0, at, 2160),
            Edge.Right => Rect(at, 0, 3840, 2160),
            _ => Rect(0, at, 3840, 2160),
        };
        Assert.Equal(outward, AppbarGuard.FurtherOut(Strip(inner), Strip(before), edge));
    }

    /// <summary>The runaway two bars once caused: each pushed the other up the screen, 2160 px to 850.</summary>
    [Fact]
    public void AStripPushedFarInIsNotTakenAndOneBesideATaskbarIs()
    {
        var asked = Rect(0, 2106, 3840, 2160);
        var beside = Rect(0, 2034, 3840, 2088);
        var far = Rect(0, 796, 3840, 850);
        Assert.Equal(2034, AppbarGuard.Stacked(asked, beside, Primary, Edge.Bottom).top);
        Assert.Equal(2106, AppbarGuard.Stacked(asked, far, Primary, Edge.Bottom).top);
        Assert.Equal(72, AppbarGuard.Inward(asked, beside, Edge.Bottom));
    }

    /// <summary>The work area set directly is the one there, less the strip on the bar's side, and only ever smaller.</summary>
    [Theory]
    [InlineData(Edge.Bottom, 0, 0, 3840, 2106)]
    [InlineData(Edge.Top, 0, 54, 3840, 2160)]
    [InlineData(Edge.Left, 54, 0, 3840, 2160)]
    [InlineData(Edge.Right, 0, 0, 3786, 2160)]
    public void TheWorkAreaSetIsTheOneThereLessTheStrip(Edge edge, int left, int top, int right, int bottom)
    {
        var strip = AppbarSpace.Fit(Primary, edge, Band);
        Assert.Equal(Rect(left, top, right, bottom), AppbarGuard.Carved(Primary, strip, edge));
        var further = Rect(100, 100, 3740, 2000);
        Assert.Equal(further, AppbarGuard.Carved(further, strip, edge));
    }

    /// <summary>A bar the shell would not register sets no work area: on a desktop with no Explorer there is nothing to stand in for.</summary>
    [Fact]
    public void ABarTheShellRefusesSetsNoWorkArea()
    {
        var world = new BarWorld(two: false) { RefuseNew = 1_000 };
        var bar = world.AddBar(One);
        Hold(bar, world);
        for (var repair = 0; repair < 6; repair++)
        {
            bar.Holder.Repair(world.Now, "not registered", AppbarState.Unregistered);
        }
        Assert.Equal(0, Count(world, "workarea"));
    }

    // --- The rest of what a bar says ------------------------------------------------------------------

    [Fact]
    public void AMoveIsToldOncePerPlace()
    {
        var calls = new List<string>();
        var shell = new CountingShell(calls);
        var holder = new AppbarHolder(shell, (HWND)(nint)0x1000, 0x8001, AppbarTrace.None, "DISPLAY1");
        holder.Register();
        var place = Rect(0, 1999, 3840, 2160);
        holder.Moved(place);
        holder.Moved(place);
        holder.Moved(place with { top = 2000 });
        Assert.Equal(2, calls.Count(call => call == "moved"));
    }

    [Fact]
    public void AnUnregisteredBarTellsNothingOfItsMoves()
    {
        var calls = new List<string>();
        var holder = new AppbarHolder(new CountingShell(calls) { Refuses = true }, (HWND)(nint)0x1000, 0x8001, AppbarTrace.None, "DISPLAY1");
        holder.Register();
        holder.Moved(Rect(0, 1999, 3840, 2160));
        Assert.DoesNotContain("moved", calls);
    }

    [Fact]
    public void ARemovedBarGivesItsSpaceBackAndHoldsNothing()
    {
        var (world, bar) = One_();
        Hold(bar, world);
        bar.Holder.Release();
        Assert.Equal(Primary, world.Of(One).Work);
        Assert.False(bar.Holder.Registered);
        Assert.Equal(AppbarState.Idle, bar.Holder.Verify().State);
    }

    /// <summary>A shell that only counts what it is asked: for the moves, which the desktop does not keep.</summary>
    private sealed class CountingShell(List<string> calls) : IAppbarShell
    {
        public bool Refuses { get; init; }

        public bool Register(HWND bar, uint callback) => !Refuses;

        public void Remove(HWND bar) => calls.Add("remove");

        public RECT Query(HWND bar, Edge edge, RECT proposed) => proposed;

        public RECT SetPos(HWND bar, Edge edge, RECT strip) => strip;

        public void Moved(HWND bar) => calls.Add("moved");

        public RECT? WorkArea(nint monitor) => null;

        public bool SetWorkArea(RECT work) => false;

        public string LastCall => "";

        public string Describe() => "";
    }

    // --- The first arrange ---------------------------------------------------------------------------

    /// <summary>
    /// The first arrange with Explorer's taskbar holding its strip builds the bars asking for nothing —
    /// the taskbar keeps its strip and stays in view — and reconciles (the takeover) straight after,
    /// just before the bars show. Any other arrange asks for a new bar's strip before the build.
    /// </summary>
    [Fact]
    public void TheFirstArrangeTakesOverAfterTheBuildAndJustBeforeTheBarsShow()
    {
        Assert.Equal(
            new[] { Karpo.Dock.Dock.ArrangeStep.Build, Karpo.Dock.Dock.ArrangeStep.Reconcile, Karpo.Dock.Dock.ArrangeStep.Show },
            Karpo.Dock.Dock.ArrangeSteps(waiting: true));
        Assert.Equal(
            new[] { Karpo.Dock.Dock.ArrangeStep.ReserveNew, Karpo.Dock.Dock.ArrangeStep.Build, Karpo.Dock.Dock.ArrangeStep.Reconcile, Karpo.Dock.Dock.ArrangeStep.Show },
            Karpo.Dock.Dock.ArrangeSteps(waiting: false));
        // ABM_GETSTATE's answer: ABS_AUTOHIDE (1) with or without ABS_ALWAYSONTOP (2).
        Assert.True(Taskbar.AutoHides(1));
        Assert.True(Taskbar.AutoHides(3));
        Assert.False(Taskbar.AutoHides(2));
        Assert.False(Taskbar.AutoHides(0));
    }

    // --- Displays lit, and the trace ---------------------------------------------------------------------

    /// <summary>A display lit again reconciles; one dimmed or going dark does not.</summary>
    [Theory]
    [InlineData(1u, true)]
    [InlineData(0u, false)]
    [InlineData(2u, false)]
    public unsafe void ADisplayLitAgainIsHeard(uint state, bool lit)
    {
        var setting = stackalloc byte[sizeof(POWERBROADCAST_SETTING) + sizeof(uint)];
        var notice = (POWERBROADCAST_SETTING*)setting;
        notice->PowerSetting = GUID.GUID_SESSION_DISPLAY_STATUS;
        notice->DataLength = sizeof(uint);
        *(uint*)&notice->Data = state;
        Assert.Equal(lit, Karpo.Dock.Dock.Lit(WM.WM_POWERBROADCAST, PBT.PBT_POWERSETTINGCHANGE, (nint)notice));

        // Another setting, another message: nothing.
        notice->PowerSetting = GUID.GUID_ACDC_POWER_SOURCE;
        Assert.False(Karpo.Dock.Dock.Lit(WM.WM_POWERBROADCAST, PBT.PBT_POWERSETTINGCHANGE, (nint)notice));
        Assert.False(Karpo.Dock.Dock.Lit(WM.WM_POWERBROADCAST, PBT.PBT_APMRESUMEAUTOMATIC, 0));
    }

    [Fact]
    public void TheTraceSaysWhatFailedAndHowItWasRepaired()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "appbar-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "appbar.log");
        try
        {
            var world = new BarWorld(new AppbarTrace(file), two: false) { RefuseNew = 1 };
            var bar = world.AddBar(One);
            Hold(bar, world);
            var (_, why) = bar.Holder.Verify();
            bar.Holder.Repair(world.Now, why, AppbarState.Unregistered);
            bar.Holder.Settled();
            var written = File.ReadAllLines(file);
            Assert.Contains(written, line => line.Contains("[DISPLAY1] register -> REFUSED"));
            Assert.Contains(written, line => line.Contains("[DISPLAY1] NOT held: not registered (repair 1) -> Position"));
            Assert.Contains(written, line => line.Contains("[DISPLAY1] register -> ok"));
            Assert.Contains(written, line => line.Contains("[DISPLAY1] held again after 1 repair"));

            // Held and looked at again and again: not a line more.
            for (var look = 0; look < 20; look++)
            {
                bar.Holder.Verify();
                bar.Holder.Settled();
            }
            Assert.Equal(written.Length, File.ReadAllLines(file).Length);
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void TheTraceRollsOverRatherThanGrowing()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "appbar-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "appbar.log");
        try
        {
            var trace = new AppbarTrace(file, maxBytes: 1024);
            for (var line = 0; line < 200; line++)
            {
                trace.Write($"line {line} " + new string('x', 40));
            }
            Assert.True(new FileInfo(file).Length <= 1024 + 100);
            Assert.True(File.Exists(file + ".1"));
            Assert.False(File.Exists(file + ".2"));
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    [Fact]
    public void ATraceWithNowhereToWriteWritesNothing()
    {
        AppbarTrace.None.Write("nothing");
        Assert.Null(AppbarTrace.None.FilePath);
    }

    // --- The tray's spy, which every appbar message passes through ------------------------------------

    /// <summary>
    /// The root of the silent failure: while Karpo's tray runs, SHAppBarMessage reaches Explorer
    /// through Karpo's own Shell_TrayWnd, which answered a message it could not deliver with success.
    /// An appbar message that never arrived must say so.
    /// </summary>
    [Fact]
    public void AnAppbarMessageThatNeverArrivedFails()
    {
        Assert.Equal(0, Tray.Unreached(WM.WM_COPYDATA, appbar: true, noExplorer: true));
        Assert.Equal(0, Tray.Unreached(WM.WM_COPYDATA, appbar: true, noExplorer: false));
    }

    /// <summary>The other messages keep the answers they always had.</summary>
    [Fact]
    public void OtherLostMessagesKeepTheirOldAnswers()
    {
        Assert.Equal(1, Tray.Unreached(WM.WM_COPYDATA, appbar: false, noExplorer: true));
        Assert.Equal(1, Tray.Unreached(WM.WM_COPYDATA, appbar: false, noExplorer: false));
        Assert.Equal(1, Tray.Unreached(WM.WM_USER + 1, appbar: false, noExplorer: true));
        Assert.Equal(0, Tray.Unreached(WM.WM_USER + 1, appbar: false, noExplorer: false));
    }
}
