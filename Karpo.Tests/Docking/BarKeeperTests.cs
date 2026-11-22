// The one reconcile (Karpo.Dock\BarKeeper.cs, §272) on a made-up desktop (BarWorld): every scenario the
// user's "bullet proof in every scenario" names, Explorer misbehaving as the real one has, and full
// screen apps — Anno 1800's exclusive full screen first. Each test ends with the world as the person
// should see it (BarWorld.Wrong) and with none of the keeper's rules broken on the way (BarWorld.Broken:
// nothing sent for a display with a full screen app, no bar shown over one, no message loop).

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;
using static Karpo.Tests.Docking.BarWorld;

namespace Karpo.Tests.Docking;

public sealed class BarKeeperTests
{
    private static RECT Whole1 => Rect(0, 0, 3840, 2160);

    /// <summary>Started and given time to settle, all well.</summary>
    private static BarWorld Started(bool two = true)
    {
        var world = new BarWorld(two: two);
        world.Start();
        world.Settle();
        Assert.Empty(world.Wrong());
        return world;
    }

    private static int Sent(BarWorld world) => world.Calls.Count(call => call.Call is "setpos" or "register" or "workarea");

    // --- Starting ---------------------------------------------------------------------------------

    /// <summary>Explorer ready: its taskbar hidden and auto-hidden, each display's strip held on the edge.</summary>
    [Fact]
    public void AStartTakesOverAndHoldsEveryStrip()
    {
        var world = Started();
        Assert.True(world.AutoHidden);
        Assert.False(world.TaskbarShown);
        Assert.Equal(1, world.Explorer.Takes);
        Assert.Equal(Rect(0, 0, 3840, 2160 - Band), world.Of(One).Work);
        Assert.Equal(Rect(-3840, 0, 0, 2160 - Band), world.Of(Two).Work);
    }

    /// <summary>
    /// Nothing runs while all is well: no timer, no look, no message to Explorer, for as long as
    /// nothing happens — the two-second look of before is gone.
    /// </summary>
    [Fact]
    public void AnIdleDockDoesNothingAtAll()
    {
        var world = Started();
        var calls = world.Calls.Count;
        var settles = world.Settles;
        world.Wait(3_600_000);
        Assert.Equal(calls, world.Calls.Count);
        Assert.Equal(settles, world.Settles);
        Assert.Null(world.SettleAt);
    }

    /// <summary>A window coming to the front asks Explorer nothing and sends nothing while the strips are held.</summary>
    [Fact]
    public void TheFrontChangingSendsNothingWhileAllIsHeld()
    {
        var world = Started();
        var calls = world.Calls.Count;
        var notepad = world.Open("Notepad", One, Rect(100, 100, 900, 700));
        world.Activate(notepad);
        world.Changed(notepad);
        Assert.Equal(calls, world.Calls.Count);
        Assert.Null(world.SettleAt);
    }

    /// <summary>
    /// At sign-in, before Explorer: the bars stand on their own (nothing to register with), and the
    /// moment Explorer's TaskbarCreated comes its taskbar is hidden and the strips held.
    /// </summary>
    [Fact]
    public void ASignInBeforeExplorerTakesOverWhenItComes()
    {
        var world = new BarWorld { Running = false };
        world.Start();
        world.Settle();
        Assert.Equal(0, world.Explorer.Takes);

        world.ExplorerRestarts();
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(1, world.Explorer.Takes);
    }

    /// <summary>
    /// A Karpo killed with its guard left Explorer's taskbar auto-hidden, and Windows keeps that. The
    /// next start takes over from there, and quitting shows the taskbar, not auto-hidden (§465).
    /// </summary>
    [Fact]
    public void AStartAfterAKillTakesOverAndQuittingShowsTheTaskbar()
    {
        var world = new BarWorld();
        ((ITaskbarShell)world).SetState(1);
        world.Start();
        world.Settle();
        Assert.Empty(world.Wrong());

        world.Quit();
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
    }

    /// <summary>
    /// §591: Windows' taskbar on a side (the person's choice, or an earlier Karpo's quit before §600). It can't auto-hide
    /// there, so the takeover moves it to the bottom: the bar holds that edge itself, no empty strip of the taskbar's
    /// beside it. §600, the person: "dont do this then. because it moves it to the right on all screens." — quitting,
    /// or a crash through the guard, shows it again where it waited, at the bottom; no way of ending moves it.
    /// </summary>
    [Theory]
    [InlineData(Edge.Left, false)]
    [InlineData(Edge.Left, true)]
    [InlineData(Edge.Right, false)]
    [InlineData(Edge.Top, true)]
    public void WindowsTaskbarWaitsAtTheBottomAndStaysThereWhenKarpoEnds(Edge side, bool crash)
    {
        var world = new BarWorld { TaskbarEdge = side };
        foreach (var display in world.Displays)
        {
            world.AddBar(display.Device).Edge = display.Device == BarWorld.One ? side : Edge.Bottom;
        }
        world.Stirred(Stir.Start);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(Edge.Bottom, world.TaskbarEdge);

        if (crash)
        {
            world.Crash();
        }
        else
        {
            world.Quit();
        }
        Assert.Equal(Edge.Bottom, world.TaskbarEdge);
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
    }

    /// <summary>
    /// §591: the person moves Windows' taskbar to the right in Windows' settings while Karpo runs. There it holds its
    /// strip, auto-hide on or not: the next reconcile moves it back to the bottom, and every bar holds its own edge again.
    /// </summary>
    [Fact]
    public void WindowsTaskbarMovedInWindowsSettingsWhileKarpoRunsGoesBackToTheBottom()
    {
        var world = new BarWorld { TaskbarEdge = Edge.Bottom };
        foreach (var display in world.Displays)
        {
            world.AddBar(display.Device).Edge = Edge.Right;
        }
        world.Stirred(Stir.Start);
        world.Settle();
        Assert.Empty(world.Wrong());

        world.SettingsMoveTaskbar(Edge.Right);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(Edge.Bottom, world.TaskbarEdge);
    }

    /// <summary>
    /// A slow start (two in five on the user's PC): Explorer takes every position and works nothing out
    /// for 8 s, its own taskbar's strip included. No window is ever under a bar meanwhile; once
    /// Explorer acts, everything is as it should be.
    /// </summary>
    [Fact]
    public void AStartWhileExplorerActsOnNothingNeverLeavesABarOverTheWindows()
    {
        var world = new BarWorld();
        world.Stall(8_000);
        world.Start();
        for (var waited = 0; waited < 8_000; waited += 250)
        {
            world.Wait(250);
            Assert.Empty(world.Overlapping());
        }
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// Explorer stalling with the work areas whole (a dark screen as Karpo starts): the strips are held
    /// within two settles — the work area set directly — and Explorer, once it acts, agrees with what
    /// was set: nothing more is sent.
    /// </summary>
    [Fact]
    public void AStartWithTheWorkAreasWholeAndExplorerStalledIsHeldWithinTwoSeconds()
    {
        var world = new BarWorld { KeptAutoHide = true };
        ((ITaskbarShell)world).SetState(1);
        world.Stall(30_000);
        world.Start();
        world.Wait(2_100);
        Assert.Empty(world.Wrong());
        var sent = Sent(world);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(sent, Sent(world));
    }

    /// <summary>ABM_NEW refused at the start: registered again by the repairs, within the budget, and held.</summary>
    [Fact]
    public void RefusedRegistrationsAreRepaired()
    {
        var world = new BarWorld { RefuseNew = 3 };
        world.Start();
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// Taking over on the second display (the user's PC): Explorer counts its taskbar there for a while
    /// after it let go, puts a strip beside it, and drops one asked for on the edge. The strip is held
    /// beside it meanwhile — never no strip — and on the edge once Explorer lets go.
    /// </summary>
    [Fact]
    public void TheSecondDisplayEndsOnTheEdgeAfterExplorerLetsGo()
    {
        var world = new BarWorld { SecondCountsTaskbarFor = 300, SecondDropsEdgeFor = 860 };
        world.Start();
        Assert.True(AppbarGuard.Excludes(world.Of(Two).Work, AppbarSpace.Fit(Rect(-3840, 0, 0, 2160), Edge.Bottom, Band), Edge.Bottom));
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>The same, with Explorer dropping the edge for longer than one settle: asked for again until it is held there.</summary>
    [Fact]
    public void AStripDroppedOnTheEdgeForLongerThanASettleIsAskedForAgain()
    {
        var world = new BarWorld { SecondCountsTaskbarFor = 0, SecondDropsEdgeFor = 2_500 };
        world.Start();
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    // --- Explorer -----------------------------------------------------------------------------------

    /// <summary>
    /// Explorer restarts: it knows no appbar, and its new taskbar comes up shown and holding its strip.
    /// TaskbarCreated: every bar registered and held again, the taskbar hidden and auto-hidden again,
    /// and the person's own state is still what goes back.
    /// </summary>
    [Fact]
    public void AnExplorerRestartIsTakenOverAgain()
    {
        var world = Started();
        world.ExplorerRestarts();
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(1, world.Explorer.Takes);

        world.Quit();
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
    }

    /// <summary>
    /// The session ending (Dock.SessionEnding): Explorer's taskbar shown at once, the bars beside it;
    /// the sign-out cancelled: hidden again, the strips on the edge.
    /// </summary>
    [Fact]
    public void TheSessionEndingShowsTheTaskbarAndACancelHidesItAgain()
    {
        var world = Started();
        world.HideTaskbar = false;
        world.Stirred(Stir.Settings);
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
        world.Settle();
        Assert.Empty(world.Wrong());
        // The bar's strip beside the taskbar's.
        Assert.Equal(2160 - TaskbarThickness - Band, world.Of(One).Work.bottom);

        world.HideTaskbar = true;
        world.Stirred(Stir.Settings);
        Assert.True(world.AutoHidden);
        Assert.False(world.TaskbarShown);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §465, the user's PC: Explorer auto-hidden before Karpo started — Windows kept Karpo's own
    /// auto-hide of an earlier run. Quitting shows the taskbar, holding its strip, and Windows keeps it
    /// so for the next Explorer.
    /// </summary>
    [Fact]
    public void AnAutoHideKeptFromAnEarlierKarpoIsNeverPutBack()
    {
        var world = new BarWorld { KeptAutoHide = true };
        ((ITaskbarShell)world).SetState(1);
        world.Start();
        world.Settle();
        Assert.Empty(world.Wrong());
        world.Quit();
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
        Assert.Equal(Rect(0, 0, 3840, 2160 - TaskbarThickness), world.Of(One).Work);
        // What Windows keeps, which a new Explorer comes up with, should Karpo not start again.
        Assert.False(world.KeptAutoHide);
    }

    /// <summary>Quitting: Explorer's taskbar shown, holding its own strip, and no appbar left with Explorer.</summary>
    [Fact]
    public void QuittingPutsEverythingBack()
    {
        var world = Started();
        world.Quit();
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
        Assert.Equal(Rect(0, 0, 3840, 2160 - TaskbarThickness), world.Of(One).Work);
    }

    /// <summary>A crash or a kill: the guard shows the taskbar, not auto-hidden, whatever it was before Karpo.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACrashIsShownByTheGuard(bool autoHiddenBefore)
    {
        var world = new BarWorld { KeptAutoHide = autoHiddenBefore };
        if (autoHiddenBefore)
        {
            ((ITaskbarShell)world).SetState(1);
        }
        world.Start();
        world.Settle();
        world.Crash();
        Assert.False(world.AutoHidden);
        Assert.True(world.TaskbarShown);
    }

    // --- Displays ------------------------------------------------------------------------------------

    /// <summary>
    /// The dark screen of §212: both work areas whole, Explorer acting on nothing for four minutes.
    /// Held within two settles by setting the work area directly; when Explorer comes round it agrees.
    /// </summary>
    [Fact]
    public void AfterADarkScreenTheStripsAreHeldWithinTwoSeconds()
    {
        var world = Started();
        world.DarkScreen(240_000);
        world.Wait(2_100);
        Assert.Empty(world.Wrong());
        var sent = Sent(world);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(sent, Sent(world));
    }

    /// <summary>A display gone and back (DisplayPort asleep), Explorer stalling as it comes back: its new bar held.</summary>
    [Fact]
    public void ADisplayUnpluggedAndBackIsHeldAgain()
    {
        var world = Started();
        world.Unplug(Two);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Single(world.Bars);

        world.Stall(30_000);
        world.Plug(Two, 103, Rect(-3840, 0, 0, 2160));
        world.Wait(2_100);
        Assert.Empty(world.Wrong());
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>Another resolution: the strip follows the display's new bottom.</summary>
    [Fact]
    public void AResolutionChangeMovesTheStrip()
    {
        var world = Started();
        world.Mode(One, Rect(0, 0, 2560, 1440));
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(1440 - Band, world.Of(One).Work.bottom);
    }

    /// <summary>Another scale: a thicker bar holds a thicker strip.</summary>
    [Fact]
    public void AScaleChangeHoldsTheNewThickness()
    {
        var world = Started();
        world.Bars[0].Thickness = 72;
        world.Stirred(Stir.Scale);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(2160 - 72, world.Of(One).Work.bottom);

        // And thinner again: the work area comes back out to it.
        world.Bars[0].Thickness = Band;
        world.Stirred(Stir.Scale);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>Lock and unlock, sleep and resume, a display off and on with nothing lost: nothing sent.</summary>
    [Theory]
    [InlineData(Stir.Unlocked)]
    [InlineData(Stir.Resumed)]
    [InlineData(Stir.DisplayLit)]
    public void ADisturbanceThatTookNothingSendsNothing(Stir stir)
    {
        var world = Started();
        var sent = Sent(world);
        world.Stirred(stir);
        world.Settle();
        Assert.Equal(sent, Sent(world));
        Assert.Empty(world.Wrong());
    }

    /// <summary>A resume that came back with every work area whole, without a word from Explorer: held again by the resume's reconcile.</summary>
    [Fact]
    public void AResumeWithTheWorkAreasWholeIsRepaired()
    {
        var world = Started();
        world.ResetsWorkArea(One, says: false);
        world.ResetsWorkArea(Two, says: false);
        world.Stirred(Stir.Resumed);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    // --- Explorer misbehaving -------------------------------------------------------------------------

    /// <summary>Explorer acting on each position a second and a half late: held once it has, without a fight.</summary>
    [Fact]
    public void AnExplorerThatAppliesLateIsHeld()
    {
        var world = new BarWorld { ApplyDelay = 1_500 };
        world.Start();
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.True(Sent(world) < 30, $"{Sent(world)} sent");
    }

    /// <summary>Explorer putting the whole screen back on its own, and saying so: held again at once.</summary>
    [Fact]
    public void AWorkAreaPutBackAndSaidIsHeldAgainAtOnce()
    {
        var world = Started();
        world.ResetsWorkArea(One, says: true);
        Assert.Empty(world.Wrong());
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// Explorer putting the whole screen back without a word: nobody can know until something happens —
    /// the next window that comes to the front looks, and holds it again.
    /// </summary>
    [Fact]
    public void AWorkAreaPutBackSilentlyIsHeldAgainAtTheNextWindow()
    {
        var world = Started();
        world.ResetsWorkArea(One, says: false);
        Assert.NotEmpty(world.Wrong());
        world.Activate(world.Open("Notepad", One, Rect(100, 100, 900, 700)));
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// An Explorer that drops every position and puts the whole screen back at every work area set: the
    /// keeper is never drawn into a loop — at most the budget, then quiet until something changes — and
    /// once Explorer behaves again, the next window to the front holds the strips.
    /// </summary>
    [Fact]
    public void AnExplorerThatFightsIsNeverFoughtInALoop()
    {
        var world = Started();
        world.DropsEverything = true;
        world.FightsDirectSets = true;
        world.ResetsWorkArea(One, says: true);
        world.Settle();
        Assert.Empty(world.Broken);
        var sent = Sent(world);
        Assert.True(sent < 40, $"{sent} sent");
        // Quiet now, however long nothing happens.
        world.Wait(600_000);
        Assert.Equal(sent, Sent(world));

        world.DropsEverything = false;
        world.FightsDirectSets = false;
        world.Activate(world.Open("Notepad", One, Rect(100, 100, 900, 700)));
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    // --- The bar's own settings -------------------------------------------------------------------------

    /// <summary>The bar moved to the top: registered again (the shell keeps an appbar on its side), the top held, the bottom given back.</summary>
    [Fact]
    public void TheBarMovedToTheTopHoldsTheTop()
    {
        var world = Started();
        world.Bars[0].Edge = Edge.Top;
        world.Stirred(Stir.Settings);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(Rect(0, Band, 3840, 2160), world.Of(One).Work);
    }

    /// <summary>The bar set to come in on hover: its strip given back, the work area the whole display again.</summary>
    [Fact]
    public void ABarThatHidesGivesItsStripBack()
    {
        var world = Started();
        world.Bars[0].Thickness = 0;
        world.Stirred(Stir.Settings);
        world.Settle();
        Assert.Empty(world.Wrong());
        Assert.Equal(Whole1, world.Of(One).Work);
        Assert.True(world.Bars[0].Holder.Registered);
    }

    // --- Full screen apps -------------------------------------------------------------------------------

    /// <summary>A borderless full screen game: its display's bars hidden and left alone, the other display's not; back when it goes.</summary>
    [Fact]
    public void ABorderlessFullScreenGameHasItsDisplayToItself()
    {
        var world = Started();
        var calls = world.Calls.Count;
        var game = world.Open("UnityWndClass", One, Whole1, captioned: false);
        world.Activate(game);
        Assert.True(world.Bars[0].MadeWay);
        Assert.False(world.Bars[1].MadeWay);
        Assert.Equal(calls, world.Calls.Count);

        // A display change elsewhere, Explorer's own work: still nothing for the game's display.
        world.Stirred(Stir.Unlocked);
        world.Settle();
        Assert.DoesNotContain(world.Calls.Skip(calls), call => call.Display == One && call.Call is "setpos" or "register" or "workarea");

        // Windowed again: the bars come back, the strip held.
        game.Rect = Rect(100, 100, 1700, 1000);
        game.Captioned = true;
        world.Changed(game);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// Anno 1800's exclusive full screen (§272): the game comes to the front windowed, then switches the
    /// display to 640x480 for itself. Its display's bar makes way the moment the mode changes — before
    /// anything is laid out on the game's mode — and nothing is asked of the shell for that display while
    /// the game runs, nor is its work area touched, however often the mode changes. When the game ends,
    /// the bar is back and holds its strip.
    /// </summary>
    [Fact]
    public void AnExclusiveFullScreenGameIsNeverDisturbed()
    {
        var world = Started();
        var game = world.Open("Anno1800", One, Rect(0, 0, 1920, 1080), captioned: false);
        world.Activate(game);
        Assert.False(world.Bars[0].MadeWay);

        game.Rect = Rect(0, 0, 640, 480);
        world.Direct3DFullScreen = true;
        var before = world.Calls.Count;
        world.Mode(One, Rect(0, 0, 640, 480));
        Assert.True(world.Bars[0].MadeWay);
        // Explorer works the work areas out again for the new mode; the bar's display is left to the game.
        world.Mode(One, Rect(0, 0, 1680, 1050));
        game.Rect = Rect(0, 0, 1680, 1050);
        world.Changed(game);
        world.Settle();
        Assert.True(world.Bars[0].MadeWay);
        Assert.DoesNotContain(world.Calls.Skip(before), call => call.Display == One && call.Call is "setpos" or "register" or "workarea" or "query");
        Assert.Empty(world.Broken);

        // The game ends: its mode goes, its window with it.
        world.Direct3DFullScreen = false;
        world.Mode(One, Whole1);
        world.Close(game);
        world.Settle();
        Assert.False(world.Bars[0].MadeWay);
        Assert.Empty(world.Wrong());
    }

    /// <summary>The same game, with no word from Windows about Direct3D: the display as it is now is enough.</summary>
    [Fact]
    public void AModeChangeIsMeasuredAgainstTheModeItself()
    {
        var world = Started();
        var game = world.Open("Anno1800", One, Rect(0, 0, 640, 480), captioned: false);
        world.Activate(game);
        Assert.False(world.Bars[0].MadeWay);
        world.Mode(One, Rect(0, 0, 640, 480));
        Assert.True(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>A full screen video on the second display while the person works on the first: it keeps its display until it leaves full screen.</summary>
    [Fact]
    public void AFullScreenAppKeepsItsDisplayWhileThePersonWorksOnTheOther()
    {
        var world = Started();
        var video = world.Open("Chrome_WidgetWin_1", Two, Rect(-3840, 0, 0, 2160), captioned: false);
        world.Activate(video);
        Assert.True(world.Bars[1].MadeWay);

        world.Activate(world.Open("Notepad", One, Rect(100, 100, 900, 700)));
        Assert.True(world.Bars[1].MadeWay);
        Assert.False(world.Bars[0].MadeWay);

        // The video ends full screen: heard though it is not in front, since it is watched.
        video.Rect = Rect(-3000, 200, -1000, 1400);
        video.Captioned = true;
        world.Changed(video);
        Assert.False(world.Bars[1].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// The user (2026-10-25): a video full screen, the person clicks away into another app, "after some
    /// times karpo shows itself over the fullscreen video". An app in the background took the
    /// foreground with a window nobody sees — hidden, minimized, parked at -32000 (MonitorFromWindow
    /// names the leftmost display for it, the video's) — which became the video's display's front, so
    /// the display read as full screen no more. None of those is a display's front.
    /// </summary>
    [Fact]
    public void AWindowInFrontThatNobodySeesLeavesTheVideoItsDisplay()
    {
        var world = Started();
        var video = world.Open("Chrome_WidgetWin_1", Two, Rect(-3840, 0, 0, 2160), captioned: false);
        world.Activate(video);
        world.Activate(world.Open("Notepad", One, Rect(100, 100, 900, 700)));
        Assert.True(world.Bars[1].MadeWay);

        var parked = world.Open("Chrome_WidgetWin_1", Two, Rect(-32000, -32000, -31840, -31973));
        world.Activate(parked);
        Assert.True(world.Bars[1].MadeWay);
        var hidden = world.Open("HiddenHelper", Two, Rect(-2000, 500, -1000, 900));
        hidden.Visible = false;
        world.Activate(hidden);
        Assert.True(world.Bars[1].MadeWay);
        var minimized = world.Open("Notepad", Two, Rect(-2000, 500, -1000, 900));
        minimized.Minimized = true;
        world.Activate(minimized);
        Assert.True(world.Bars[1].MadeWay);

        // Each of them went where it is seen, on the other display, and the person back to work there.
        parked.Display = One;
        parked.Rect = Rect(200, 200, 800, 600);
        world.Changed(parked);
        world.Activate(world.Open("Notepad", One, Rect(100, 100, 900, 700)));
        Assert.True(world.Bars[1].MadeWay);
        Assert.False(world.Bars[0].MadeWay);

        // Restored where the video is, it is in front there: the bars come back.
        minimized.Minimized = false;
        world.Activate(minimized);
        Assert.False(world.Bars[1].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §702, the person (2026-11-15): "the karpo taskbar sometimes clips infront of a fullscreen video… when i go into
    /// edit mode in themia". Themia's widgets lie on the desktop, under every app, and take the foreground in edit mode
    /// without rising: the video stays over them, and keeps its display. A window that does rise takes it back.
    /// </summary>
    [Fact]
    public void AWidgetTakingTheKeysUnderTheVideoLeavesTheVideoItsDisplay()
    {
        var world = Started();
        var widgets = world.Open("WinUIDesktopWin32WindowClass", One, Rect(0, 0, 3840, 2159), captioned: false);
        widgets.Bottom = true;
        world.Stack.Remove(widgets.Handle);
        world.Stack.Add(widgets.Handle);
        var video = world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        world.Activate(video);
        Assert.True(world.Bars[0].MadeWay);

        world.Activate(widgets);
        Assert.True(world.Bars[0].MadeWay);

        // Raised over the video, it is in front there: the bars come back.
        widgets.Bottom = false;
        world.Activate(widgets);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §706, the person (2026-11-19): "in fullscreen mode when clicking themia sometimes karpo comes to front". appbar.log,
    /// 2026-11-18 13:32: "full screen app gone: its bars come back (explorer.exe (Progman) came to the front at
    /// -3840,0..3840,2160)". The desktop window spans both displays and is read as the primary's: a click on the desktop of
    /// the other display took the video's display. Clicked, it stays at the bottom, under the video; raised over it (Show
    /// desktop), it takes the display.
    /// </summary>
    [Fact]
    public void ADesktopClickOnTheOtherDisplayLeavesTheVideoItsDisplay()
    {
        var world = Started();
        var desktop = world.Open("Progman", One, Rect(-3840, 0, 3840, 2160), captioned: false);
        desktop.Bottom = true;
        world.Stack.Remove(desktop.Handle);
        world.Stack.Add(desktop.Handle);
        var video = world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        world.Activate(video);
        Assert.True(world.Bars[0].MadeWay);

        world.Activate(desktop);
        Assert.True(world.Bars[0].MadeWay);
        Assert.False(world.Bars[1].MadeWay);
        world.Activate(video);
        world.Activate(desktop);
        Assert.True(world.Bars[0].MadeWay);

        // Show desktop raising the desktop over the video: the display is the bars' again.
        desktop.Bottom = false;
        world.Activate(desktop);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §706: a flyout, menu or popup opened from a widget under the video (a top-level window of its own, captionless,
    /// owned by the widgets' window) is where its owner is, wherever it stands in the z-order: the video keeps its display.
    /// A window with a caption of its own (a dialog) owned by the same widgets is a window in front.
    /// </summary>
    [Fact]
    public void AFlyoutOfAWidgetUnderTheVideoLeavesTheVideoItsDisplay()
    {
        var world = Started();
        var widgets = world.Open("WinUIDesktopWin32WindowClass", One, Rect(0, 0, 3840, 2159), captioned: false);
        widgets.Bottom = true;
        world.Stack.Remove(widgets.Handle);
        world.Stack.Add(widgets.Handle);
        var video = world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        world.Activate(video);

        var flyout = world.Open("Microsoft.UI.Content.PopupWindowSiteBridge", One, Rect(1000, 600, 1600, 1200), captioned: false);
        flyout.Owner = widgets.Handle;
        world.Activate(flyout);
        Assert.True(world.Bars[0].MadeWay);

        var dialog = world.Open("#32770", One, Rect(1000, 600, 1600, 1200));
        dialog.Owner = widgets.Handle;
        world.Activate(dialog);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §707, the person (2026-11-19): "right now karpo is in the front again. try to fix the issue again". appbar.log
    /// 14:17:51: WhatsApp came to the front over a video full screen on DISPLAY1 and the bars came back, as they should;
    /// WhatsApp went (minimized, closed, hidden) while the person worked on the other display, and the bars stayed over the
    /// video. The window left on top of the display is its front: the video, full screen, and its bars make way again.
    /// </summary>
    [Fact]
    public void AWindowOverTheVideoGoingGivesTheVideoItsDisplayBack()
    {
        var world = Started();
        var video = world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        world.Activate(video);
        Assert.True(world.Bars[0].MadeWay);

        var chat = world.Open("WinUIDesktopWin32WindowClass", One, Rect(1081, 241, 3581, 1998));
        world.Activate(chat);
        Assert.False(world.Bars[0].MadeWay);

        // Minimized, the foreground on the other display.
        chat.Minimized = true;
        world.Activate(world.Open("Chrome_WidgetWin_1", Two, Rect(-3000, 100, -1000, 1500)));
        Assert.True(world.Bars[0].MadeWay);

        chat.Minimized = false;
        world.Activate(chat);
        Assert.False(world.Bars[0].MadeWay);
        world.Close(chat);
        Assert.True(world.Bars[0].MadeWay);

        // Hidden.
        var other = world.Open("WinUIDesktopWin32WindowClass", One, Rect(1081, 241, 3581, 1998));
        world.Activate(other);
        Assert.False(world.Bars[0].MadeWay);
        other.Visible = false;
        world.Changed(other);
        Assert.True(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §707: Karpo started (or restarted) while a video already stands full screen on a display, the foreground on the other:
    /// the display has no front yet, and the window on top of it is its front: the bars there make way at once.
    /// </summary>
    [Fact]
    public void AVideoAlreadyFullScreenAtTheStartHasItsDisplay()
    {
        var world = Started();
        world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        world.Activate(world.Open("Chrome_WidgetWin_1", Two, Rect(-3000, 100, -1000, 1500)));
        Assert.True(world.Bars[0].MadeWay);
        Assert.False(world.Bars[1].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §708, the person (2026-11-20): "karpo is currently moving over my fullscreen video sometimes. the last 5mins like 100
    /// times". appbar.log 18:09–18:12, a video full screen in Brave on DISPLAY1: "full screen app gone: its bars come back
    /// (its window left full screen: at 0,0..3840,2159)" thirty-five times. Chromium makes its full screen window one pixel
    /// shorter at the bottom whenever a window on the same display takes the foreground — the desktop clicked on the other
    /// display (it counts as the primary's), a widget under the video, a hidden helper — and whole again once it is
    /// activated. Still the display's front, it keeps the display; leaving full screen for good gives it back.
    /// </summary>
    [Fact]
    public void AChromiumVideoAPixelShortOutOfTheForegroundKeepsItsDisplay()
    {
        var world = Started();
        var desktop = world.Open("Progman", One, Rect(-3840, 0, 3840, 2160), captioned: false);
        desktop.Bottom = true;
        world.Stack.Remove(desktop.Handle);
        world.Stack.Add(desktop.Handle);
        var video = world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        world.Activate(video);
        Assert.True(world.Bars[0].MadeWay);

        // The desktop clicked on the other display, again and again: the video's window a row shorter, heard before the
        // foreground or after it, and whole again as the video is clicked.
        for (var click = 0; click < 4; click++)
        {
            if (click % 2 == 0)
            {
                world.Activate(desktop);
                video.Rect = Rect(0, 0, 3840, 2159);
                world.Changed(video);
            }
            else
            {
                video.Rect = Rect(0, 0, 3840, 2159);
                world.Changed(video);
                world.Activate(desktop);
            }
            Assert.True(world.Bars[0].MadeWay);
            Assert.False(world.Bars[1].MadeWay);
            world.Settle();
            Assert.True(world.Bars[0].MadeWay);

            video.Rect = Whole1;
            world.Activate(video);
            Assert.True(world.Bars[0].MadeWay);
        }

        // A hidden helper window takes the foreground on the video's display.
        var helper = world.Open("ConsoleWindowClass", One, Rect(200, 200, 1200, 800));
        helper.Visible = false;
        video.Rect = Rect(0, 0, 3840, 2159);
        world.Activate(helper);
        Assert.True(world.Bars[0].MadeWay);

        // A window over the video takes the display, as ever; gone again with the foreground on the other display, the
        // video — a row short still, never activated since — has it back (§707).
        var chat = world.Open("WinUIDesktopWin32WindowClass", One, Rect(1081, 241, 3581, 1998));
        world.Activate(chat);
        Assert.False(world.Bars[0].MadeWay);
        chat.Minimized = true;
        world.Activate(world.Open("Notepad", Two, Rect(-3000, 100, -1000, 1500)));
        Assert.True(world.Bars[0].MadeWay);

        // The video leaves full screen: the bars come back.
        video.Rect = Rect(400, 200, 2400, 1400);
        video.Captioned = true;
        world.Changed(video);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §708: Karpo started while a Chromium video stands full screen a row short, out of the foreground: the window on top
    /// of the display is its front (§707), and full screen.
    /// </summary>
    [Fact]
    public void AChromiumVideoAPixelShortAtTheStartHasItsDisplay()
    {
        var world = new BarWorld();
        world.Open("Chrome_WidgetWin_1", One, Rect(0, 0, 3840, 2159), captioned: false);
        world.Start();
        Assert.True(world.Bars[0].MadeWay);
        Assert.False(world.Bars[1].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// §709, the person (2026-11-20, §708 installed an hour before): "karpo again is moving itself over a fullscreen view".
    /// appbar.log 18:58–19:08: a video full screen in Brave on DISPLAY1, "full screen app gone: its bars come back (its
    /// window left full screen: at 0,0..3840,2159)" over fifty times, each time a window nobody sees took the foreground
    /// for a second or three: Themia's, rendering its widgets parked off the screen (watched: "foreground Themia
    /// (WinUIDesktopWin32WindowClass) at -32000,-32000..-30518,-31325", the video at 0,0..3840,2159 the while). The browser
    /// was maximized when the video went full screen, and its window stays zoomed all through (Chromium leaves WS_MAXIMIZE
    /// on it): a row short it keeps its display as any other, and back out of full screen, maximized with its caption, it
    /// gives it up.
    /// </summary>
    [Fact]
    public void AMaximizedBrowsersVideoAPixelShortOutOfTheForegroundKeepsItsDisplay()
    {
        var world = Started();
        var video = world.Open("Chrome_WidgetWin_1", One, Whole1, captioned: false);
        video.Maximized = true;
        world.Activate(video);
        Assert.True(world.Bars[0].MadeWay);

        var parked = world.Open("WinUIDesktopWin32WindowClass", One, Rect(-32000, -32000, -30518, -31325));
        var hidden = world.Open("ConsoleWindowClass", One, Rect(200, 200, 1200, 800));
        hidden.Visible = false;
        for (var run = 0; run < 4; run++)
        {
            var helper = run % 2 == 0 ? parked : hidden;
            // Heard as the window's move first, then as the foreground.
            video.Rect = Rect(0, 0, 3840, 2159);
            world.Changed(video);
            Assert.True(world.Bars[0].MadeWay);
            world.Activate(helper);
            Assert.True(world.Bars[0].MadeWay);
            world.Settle();
            Assert.True(world.Bars[0].MadeWay);

            video.Rect = Whole1;
            world.Activate(video);
            Assert.True(world.Bars[0].MadeWay);
        }

        // Out of full screen: maximized again, its caption back, past the display's edges.
        video.Rect = Rect(-11, -11, 3851, 2171);
        video.Captioned = true;
        world.Changed(video);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>Minimized, or closed: the display is the bars' again.</summary>
    [Fact]
    public void AFullScreenAppMinimizedOrClosedGivesTheDisplayBack()
    {
        var world = Started();
        var game = world.Open("UnityWndClass", One, Whole1, captioned: false);
        world.Activate(game);
        game.Minimized = true;
        world.Changed(game);
        Assert.False(world.Bars[0].MadeWay);

        game.Minimized = false;
        world.Activate(game);
        Assert.True(world.Bars[0].MadeWay);
        world.Close(game);
        Assert.False(world.Bars[0].MadeWay);
        world.Settle();
        Assert.Empty(world.Wrong());
    }

    /// <summary>
    /// Show desktop over a full screen app: the desktop comes to the front and the bars come back (it
    /// is not a full screen app, though it covers the display). Task view over it changes nothing.
    /// </summary>
    [Fact]
    public void TheDesktopBringsTheBarsBackAndTaskViewDoesNot()
    {
        var world = Started();
        var game = world.Open("UnityWndClass", One, Whole1, captioned: false);
        world.Activate(game);
        world.Activate(world.Open("XamlExplorerHostIslandWindow", One, Whole1));
        Assert.True(world.Bars[0].MadeWay);
        world.Activate(world.Open("WorkerW", One, Whole1));
        Assert.False(world.Bars[0].MadeWay);
    }

    /// <summary>A maximized window with no strip held covers the display, and is no full screen app.</summary>
    [Fact]
    public void AMaximizedWindowIsNoFullScreenApp()
    {
        var world = Started();
        world.Bars[0].Thickness = 0;
        world.Stirred(Stir.Settings);
        var maximized = world.Open("CabinetWClass", One, Rect(-8, -8, 3848, 2168));
        maximized.Maximized = true;
        world.Activate(maximized);
        Assert.False(world.Bars[0].MadeWay);
    }

    /// <summary>A flyout or folder open on a bar closes as its display goes full screen.</summary>
    [Fact]
    public void WhatABarHadOpenClosesAsItMakesWay()
    {
        var world = Started();
        world.Bars[0].PopupOpen = true;
        world.Activate(world.Open("UnityWndClass", One, Whole1, captioned: false));
        Assert.False(world.Bars[0].PopupOpen);
    }

    /// <summary>Edit mode shows every bar, full screen app or not, and hides them again as it ends.</summary>
    [Fact]
    public void EditModeShowsTheBarsOverAFullScreenAppAndHidesThemAfter()
    {
        var world = Started();
        world.Activate(world.Open("UnityWndClass", One, Whole1, captioned: false));
        world.BeginEdit();
        Assert.False(world.Bars[0].MadeWay);
        world.EndEdit();
        Assert.True(world.Bars[0].MadeWay);
        Assert.Empty(world.Broken);
    }

    /// <summary>Karpo's own windows in front — a flyout opened on the other display's bar — change nothing.</summary>
    [Fact]
    public void KarposOwnWindowInFrontLeavesTheFullScreenAppItsDisplay()
    {
        var world = Started();
        world.Activate(world.Open("UnityWndClass", One, Whole1, captioned: false));
        var flyout = world.Open("karpo.flyout", Two, Rect(-400, 1500, 0, 2100));
        flyout.Own = true;
        world.Activate(flyout);
        Assert.True(world.Bars[0].MadeWay);
    }

    // --- The trace -------------------------------------------------------------------------------------

    /// <summary>One line per change of state and per thing done; none while nothing changes.</summary>
    [Fact]
    public void TheTraceSaysEachChangeOnceAndIsQuietOtherwise()
    {
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "keeper-" + Guid.NewGuid().ToString("N"));
        var file = Path.Combine(folder, "appbar.log");
        try
        {
            var world = new BarWorld(new AppbarTrace(file));
            world.Start();
            world.Settle();
            var game = world.Open("Anno1800", One, Rect(0, 0, 640, 480), captioned: false);
            world.Activate(game);
            world.Mode(One, Rect(0, 0, 640, 480));
            world.Settle();
            var lines = File.ReadAllLines(file);
            Assert.Contains(lines, line => line.Contains("woke: started"));
            Assert.Contains(lines, line => line.Contains("took over"));
            Assert.Contains(lines, line => line.Contains("[DISPLAY1] setpos Bottom"));
            Assert.Single(lines, line => line.Contains("[DISPLAY1] full screen app in front: game.exe (Anno1800) at 0,0..640,480 on 0,0..640,480"));

            // Quiet while nothing changes.
            var count = File.ReadAllLines(file).Length;
            world.Activate(game);
            world.Wait(600_000);
            Assert.Equal(count, File.ReadAllLines(file).Length);

            world.Mode(One, Whole1);
            world.Close(game);
            world.Settle();
            Assert.Contains(File.ReadAllLines(file), line => line.Contains("[DISPLAY1] full screen app gone: its bars come back"));
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
}
