// The displays' brightness (Brightness): one worker off the dock's thread, a drag's levels coalesced
// to the newest, each display's last level read back once it is quiet and sent again where it did not
// take (§495; the rules alone in ScreenWritesTests), what was learnt about the displays forgotten when they
// change, and a display no way reaches never offered; a level moved elsewhere — the keyboard's keys,
// a monitor's buttons — followed (§439). All of it through a stand-in for the displays
// (SampleScreens): nothing here reads or sets a real screen's brightness.

using Karpo.Dock.QuickSettings.Services;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.QuickSettings;

public sealed class BrightnessTests
{
    private const string Samsung = @"\\.\DISPLAY1";
    private const string Other = @"\\.\DISPLAY2";
    private const string Television = @"\\.\DISPLAY3";

    private static SampleScreens TwoMonitorsAndATelevision() => new SampleScreens()
        .Add(0x10, Samsung, "SAMSUNG", 60)
        .Add(0x20, Other, "DELL U2720Q", 35)
        .Add(0x30, Television, "TV", null);

    /// <summary>The displays with a brightness are offered, with their levels; one no way reaches is not.</summary>
    [Fact]
    public void OnlyTheDisplaysWithABrightnessAreOffered()
    {
        using var brightness = new Brightness(TwoMonitorsAndATelevision());
        var screens = Settled(brightness, list => list.Count == 2);
        Assert.Equal([Samsung, Other], screens.Select(screen => screen.Device));
        Assert.Equal([60, 35], screens.Select(screen => screen.Level));
        Assert.Equal("SAMSUNG", brightness.On(0x10)?.Name);
        Assert.Null(brightness.On(0x30));
    }

    /// <summary>A PC whose displays have no brightness to set offers none: the flyout says so, as Windows shows no slider.</summary>
    [Fact]
    public void APcWithNoBrightnessOffersNone()
    {
        var machine = new SampleScreens().Add(0x10, Samsung, "SAMSUNG", null);
        using var brightness = new Brightness(machine);
        Wait(() => machine.Listed > 0);
        Thread.Sleep(50);
        Assert.Empty(brightness.Screens);
        Assert.Null(brightness.On(0));
    }

    /// <summary>
    /// A drag asks for a level at every move, and the display is slow: while one write takes its time
    /// the rest pile up, and only the newest is written after it — never a queue of every value the
    /// drag passed through. Asking returns at once, whatever the display is doing.
    /// </summary>
    [Fact]
    public void ADragIsWrittenAsItsNewestLevel()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine);
        Settled(brightness, list => list.Count == 2);
        machine.Open.Reset();
        brightness.Set(Samsung, 61);
        // The first level is being written, and the display is taking its time over it.
        Wait(() => machine.Started == 1);
        var asked = System.Diagnostics.Stopwatch.StartNew();
        foreach (var level in Enumerable.Range(62, 30))
        {
            brightness.Set(Samsung, level);
        }
        Assert.True(asked.ElapsedMilliseconds < 1000, "asking waited for the display");
        machine.Open.Set();
        Settled(brightness, list => list.FirstOrDefault(screen => screen.Device == Samsung)?.Level == 91);
        Assert.Equal(91, machine.LevelOf(Samsung));
        // The level being written when the drag went on, then the drag's last: two writes, not thirty-one.
        Assert.Equal([61, 91], machine.Writes.Select(write => write.Level));
    }

    /// <summary>A written level is read back: a monitor that takes brightness in steps of five shows the step it took.</summary>
    [Fact]
    public void AWrittenLevelIsReadBack()
    {
        var machine = TwoMonitorsAndATelevision();
        machine.Step = 5;
        using var brightness = new Brightness(machine);
        Settled(brightness, list => list.Count == 2);
        brightness.Set(Other, 37);
        var screens = Settled(brightness, list => list.FirstOrDefault(screen => screen.Device == Other)?.Level == 35 && brightness.Asked(Other) is null && machine.Writes.Count == 1);
        Assert.Equal(35, screens.First(screen => screen.Device == Other).Level);
        Assert.Equal((Other, 37), machine.Writes.Single());
    }

    /// <summary>
    /// The person's case (§495): the last level of a drag said taken and never applied — a monitor
    /// sent it too soon. The read back once the drag stops finds the display elsewhere, and the level
    /// is sent again; the slider shows the level asked until the display has it.
    /// </summary>
    [Fact]
    public void ADroppedWriteIsReadBackAndSentAgain()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine, ready: true);
        machine.Drop(Samsung, writes: 1);
        brightness.Set(Samsung, 80);
        Assert.Equal(80, brightness.LevelOf(0x10, BrightnessScreens.Own));
        Settled(brightness, list => list.First(screen => screen.Device == Samsung).Level == 80 && brightness.Asked(Samsung) is null);
        Assert.Equal(80, machine.LevelOf(Samsung));
        Assert.Equal([(Samsung, 80), (Samsung, 80)], machine.Writes);
    }

    /// <summary>
    /// A display that refuses every write is tried a bounded number of times, its handle opened
    /// afresh each time, said once, and then left alone at what it last said; the other display is
    /// set all the same.
    /// </summary>
    [Fact]
    public void ADisplayThatKeepsRefusingIsSaidAndLeftAlone()
    {
        var machine = TwoMonitorsAndATelevision();
        var said = new System.Collections.Concurrent.ConcurrentQueue<string>();
        using var brightness = new Brightness(machine, ready: true, report: said.Enqueue);
        machine.Refuse(Other);
        Assert.True(brightness.Set(0x10, BrightnessScreens.All, 20));
        Wait(() => !said.IsEmpty && brightness.Asked(Other) is null && brightness.Asked(Samsung) is null);
        Assert.Contains(Other, Assert.Single(said), StringComparison.Ordinal);
        Assert.Equal(20, machine.LevelOf(Samsung));
        Assert.Equal(35, brightness.On(0x20)?.Level);
        var tries = machine.Started;
        Assert.Equal(ScreenPace.Default.Attempts + 1, tries);
        Assert.Equal(ScreenPace.Default.Attempts, machine.ForgottenOne);
        Thread.Sleep(ScreenPace.Default.RetryMs * 2);
        Assert.Equal(tries, machine.Started);
    }

    /// <summary>What a display may read back for a level it took: a panel's steps around it, a monitor's range.</summary>
    [Fact]
    public void ADisplaysStepsAroundALevel()
    {
        byte[] tens = [0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100];
        Assert.Equal((30, 40), ScreenMachine.Between(tens, 37));
        Assert.Equal((40, 40), ScreenMachine.Between(tens, 40));
        Assert.Equal((37, 37), ScreenMachine.Between([], 37));
        Assert.Equal((37, 37), ScreenMachine.Between([.. Enumerable.Range(0, 101).Select(level => (byte)level)], 37));
        Assert.Equal((40, 40), ScreenMachine.Between([20, 40], 90));
        Assert.Equal((20, 20), ScreenMachine.Between([20, 40], 5));
        Assert.Equal((100, 100), ScreenMachine.Between(tens, 140));
        // A monitor whose luminance runs to 20: 37 goes as 7, and reads back as 35.
        Assert.Equal(7u, ScreenMachine.Raw(37, 20));
        Assert.Equal(35, ScreenMachine.Percent(ScreenMachine.Raw(37, 20), 20));
        Assert.Equal(37, ScreenMachine.Percent(ScreenMachine.Raw(37, 100), 100));
    }

    /// <summary>The displays changing forgets what was learnt about them and finds them again.</summary>
    [Fact]
    public void ChangedDisplaysAreForgottenAndFoundAgain()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine);
        Settled(brightness, list => list.Count == 2);
        var listed = machine.Listed;
        brightness.DisplaysChanged();
        Wait(() => machine.Forgotten == 1 && machine.Listed > listed);
        Assert.Equal(1, machine.Forgotten);
    }

    /// <summary>Levels are 0 to 100 however they are asked; nothing is written to a display no way reaches.</summary>
    [Fact]
    public void LevelsAreClampedAndNothingReachesADisplayWithoutAWay()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine);
        Settled(brightness, list => list.Count == 2);
        brightness.Set(Samsung, 140);
        brightness.Set(Other, -20);
        brightness.Set(Television, 50);
        Settled(brightness, list => list.First(screen => screen.Device == Samsung).Level == 100 && list.First(screen => screen.Device == Other).Level == 0);
        Assert.DoesNotContain(machine.Writes, write => write.Device == Television);
        Assert.Null(machine.LevelOf(Television));
    }

    /// <summary>
    /// DDC/CI drops a reply now and then: a display that misses one keeps the level it last said, and
    /// a level it took without answering the read stands as asked — no slider vanishes or jumps back
    /// for it. Found afresh, a display that does not answer has none, until a later read hears it.
    /// </summary>
    [Fact]
    public void AMissedReplyKeepsTheLevelItLastSaid()
    {
        var machine = TwoMonitorsAndATelevision();
        // Found before this returns, so the one round that follows is the refresh's own.
        using var brightness = new Brightness(machine, ready: true);
        Assert.Equal(2, brightness.Screens.Count);
        machine.Miss(Samsung);
        using (var round = new ManualResetEventSlim())
        {
            brightness.Changed += round.Set;
            brightness.Refresh();
            Assert.True(round.Wait(5000), "the refresh did not come round");
            brightness.Changed -= round.Set;
        }
        Assert.Equal(60, brightness.On(0x10)?.Level);

        brightness.Set(Samsung, 80);
        Settled(brightness, list => list.FirstOrDefault(screen => screen.Device == Samsung)?.Level == 80 && brightness.Asked(Samsung) is null);
        Assert.Equal(80, machine.LevelOf(Samsung));

        // The displays changed while it was not answering: it is not offered.
        brightness.DisplaysChanged();
        Settled(brightness, list => list.Count == 1);
        Assert.Null(brightness.On(0x10));
        // It answers again, and the next read finds it.
        machine.Miss(Samsung, missed: false);
        brightness.Refresh();
        var screens = Settled(brightness, list => list.Count == 2);
        Assert.Equal(80, screens.First(screen => screen.Device == Samsung).Level);
    }

    // --- One slider ------------------------------------------------------------------------------

    /// <summary>
    /// One slider set to every display sets each that has a brightness to the same level, from
    /// whichever display the widget is on — one with none included — and skips the one with none.
    /// </summary>
    [Fact]
    public void EveryDisplayIsSetToTheSlidersLevelAndOneWithNoneIsSkipped()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine, ready: true);
        Assert.Equal([Samsung, Other], brightness.Dimmed(0x20, BrightnessScreens.All).Select(screen => screen.Device));

        Assert.True(brightness.Set(0x20, BrightnessScreens.All, 70));
        Settled(brightness, list => list.All(screen => screen.Level == 70) && brightness.Asked(Samsung) is null && brightness.Asked(Other) is null);
        Assert.Equal(70, machine.LevelOf(Samsung));
        Assert.Equal(70, machine.LevelOf(Other));

        // From the television's bar, which has no brightness: the others still follow.
        Assert.True(brightness.Set(0x30, BrightnessScreens.All, 25));
        Settled(brightness, list => list.All(screen => screen.Level == 25));
        Assert.DoesNotContain(machine.Writes, write => write.Device == Television);
        Assert.Null(machine.LevelOf(Television));
    }

    /// <summary>Set to the display it is on, the slider sets that one and no other.</summary>
    [Fact]
    public void OnlyItsOwnDisplayIsSetWhereThatIsChosen()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine, ready: true);
        Assert.Equal([Other], brightness.Dimmed(0x20, BrightnessScreens.Own).Select(screen => screen.Device));

        Assert.True(brightness.Set(0x20, BrightnessScreens.Own, 20));
        Settled(brightness, list => list.First(screen => screen.Device == Other).Level == 20);
        Assert.Equal(60, machine.LevelOf(Samsung));
        Assert.All(machine.Writes, write => Assert.Equal(Other, write.Device));

        // On the television, its own has none: nothing is set, and nothing is offered.
        Assert.Empty(brightness.Dimmed(0x30, BrightnessScreens.Own));
        Assert.False(brightness.Set(0x30, BrightnessScreens.Own, 90));
        Thread.Sleep(50);
        Assert.Equal(60, machine.LevelOf(Samsung));
        Assert.Equal(20, machine.LevelOf(Other));
        Assert.DoesNotContain(machine.Writes, write => write.Level == 90);
    }

    /// <summary>
    /// The slider shows the level of the display it is on; where that one has none, the first that
    /// has one when it sets every display, and nothing when it sets its own alone. With no display
    /// that has one, it shows nothing either way.
    /// </summary>
    [Fact]
    public void TheSliderShowsTheDisplayItIsOnOrTheFirstThatHasOne()
    {
        using var brightness = new Brightness(TwoMonitorsAndATelevision(), ready: true);
        Assert.Equal(Other, brightness.Shown(0x20, BrightnessScreens.All)?.Device);
        Assert.Equal(Other, brightness.Shown(0x20, BrightnessScreens.Own)?.Device);
        Assert.Equal(35, brightness.LevelOf(0x20, BrightnessScreens.All));
        Assert.Equal(Samsung, brightness.Shown(0x30, BrightnessScreens.All)?.Device);
        Assert.Equal(60, brightness.LevelOf(0x30, BrightnessScreens.All));
        Assert.Null(brightness.Shown(0x30, BrightnessScreens.Own));
        Assert.Null(brightness.LevelOf(0x30, BrightnessScreens.Own));

        using var none = new Brightness(new SampleScreens().Add(0x30, Television, "TV", null), ready: true);
        foreach (var screens in new[] { BrightnessScreens.All, BrightnessScreens.Own })
        {
            Assert.Null(none.Shown(0x30, screens));
            Assert.Empty(none.Dimmed(0x30, screens));
            Assert.False(none.Set(0x30, screens, 50));
        }
    }

    /// <summary>
    /// A drag on the one slider asks every display at every move while a slow monitor holds the
    /// worker: each display is written at most the level in flight and then the drag's last — the
    /// display after the slow one never the level the round began with — and every display ends
    /// where the drag let go. The slider shows the level asked meanwhile, not the one read before.
    /// </summary>
    [Fact]
    public void ADragOnEveryDisplayIsWrittenAsEachDisplaysNewest()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine, ready: true);
        machine.Open.Reset();
        Assert.True(brightness.Set(0x10, BrightnessScreens.All, 61));
        // The first display's level is being written, and the display is taking its time over it.
        Wait(() => machine.Started == 1);
        var asked = System.Diagnostics.Stopwatch.StartNew();
        foreach (var level in Enumerable.Range(62, 30))
        {
            Assert.True(brightness.Set(0x10, BrightnessScreens.All, level));
        }
        Assert.True(asked.ElapsedMilliseconds < 1000, "asking waited for the display");
        Assert.Equal(91, brightness.LevelOf(0x10, BrightnessScreens.All));
        machine.Open.Set();
        Settled(brightness, list => list.All(screen => screen.Level == 91) && brightness.Asked(Samsung) is null && brightness.Asked(Other) is null);

        var writes = machine.Writes;
        Assert.All(writes, write => Assert.Contains(write.Level, new[] { 61, 91 }));
        // The one write in flight when the drag went on, then each display's last: three, not sixty-two.
        Assert.Single(writes, write => write.Level == 61);
        Assert.Equal(91, writes.Last(write => write.Device == Samsung).Level);
        Assert.Equal(91, writes.Last(write => write.Device == Other).Level);
        Assert.Equal(3, writes.Count);
    }

    // --- Changed elsewhere (docs\widgets.md §439) ---------------------------------------------------

    private const string Panel = @"\\.\DISPLAY4";

    /// <summary>A laptop at a desk: its own screen, which Windows tells of, and two monitors and a television, which tell nobody.</summary>
    private static SampleScreens Laptop() => TwoMonitorsAndATelevision().Add(0x40, Panel, "Built-in display", 40, builtIn: true);

    /// <summary>
    /// The keyboard's keys move the built-in screen, and Windows says so with the level it shows now
    /// (GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS): the built-in screen is read again and the widgets are
    /// told — and nothing else is read, since a monitor's DDC/CI is slow and nobody moved it.
    /// </summary>
    [Fact]
    public void TheBuiltInScreenMovedElsewhereIsReadAgainWhenWindowsSaysSo()
    {
        var machine = Laptop();
        using var brightness = new Brightness(machine, ready: true);
        Assert.Equal(40, brightness.On(0x40)?.Level);
        var told = 0;
        brightness.Changed += () => Interlocked.Increment(ref told);

        // Moved by the keys: until Windows says so, nothing knows.
        machine.Turn(Panel, 75);
        machine.Turn(Samsung, 10);
        var reads = machine.Reads;
        Assert.Equal(40, brightness.On(0x40)?.Level);
        Assert.True(Notice(brightness, GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS, 75));
        Settled(brightness, _ => brightness.On(0x40)?.Level == 75);
        Wait(() => Volatile.Read(ref told) == 1);
        Assert.Equal(reads + 1, machine.Reads);
        // The monitor moved too, by its own buttons, and is not read for the panel's news.
        Assert.Equal(60, brightness.On(0x10)?.Level);
        Assert.Empty(machine.Writes);
    }

    /// <summary>
    /// A write of the widget's own comes back as the same notification: the level is the one the
    /// built-in screen was read back at, so nothing is read and nothing is told. A notice of another
    /// setting is not brightness's.
    /// </summary>
    [Fact]
    public void OurOwnWriteComingBackReadsNothing()
    {
        var machine = Laptop();
        using var brightness = new Brightness(machine, ready: true);
        brightness.Set(Panel, 55);
        Settled(brightness, _ => brightness.On(0x40)?.Level == 55 && brightness.Asked(Panel) is null);
        var told = 0;
        brightness.Changed += () => Interlocked.Increment(ref told);
        var reads = machine.Reads;
        Assert.True(Notice(brightness, GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS, 55));
        Assert.False(Notice(brightness, GUID.GUID_ENERGY_SAVER_STATUS, 1));
        Assert.False(Notice(brightness, GUID.GUID_DEVICE_POWER_POLICY_VIDEO_BRIGHTNESS, 20));
        Thread.Sleep(100);
        Assert.Equal(reads, machine.Reads);
        Assert.Equal(0, Volatile.Read(ref told));

        // The same level heard while a write is still on its way is not taken for it: read.
        machine.Open.Reset();
        brightness.Set(Panel, 90);
        Wait(() => machine.Started == 2);
        Assert.True(Notice(brightness, GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS, 55));
        machine.Open.Set();
        Settled(brightness, _ => brightness.On(0x40)?.Level == 90 && brightness.Asked(Panel) is null);
    }

    /// <summary>What the notification says, read as Windows lays it out: the level for brightness's own setting, clamped; nothing for another, or too short.</summary>
    [Fact]
    public unsafe void TheNotificationIsReadForItsLevel()
    {
        var raw = stackalloc byte[24];
        var setting = (POWERBROADCAST_SETTING*)raw;
        setting->PowerSetting = GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS;
        setting->DataLength = 4;
        *(uint*)(raw + 20) = 37;
        Assert.Equal(37, Brightness.Read(setting));
        *(uint*)(raw + 20) = 250;
        Assert.Equal(100, Brightness.Read(setting));
        setting->DataLength = 2;
        Assert.Null(Brightness.Read(setting));
        setting->DataLength = 4;
        setting->PowerSetting = GUID.GUID_ENERGY_SAVER_STATUS;
        Assert.Null(Brightness.Read(setting));
        Assert.Null(Brightness.Read(null));
        // The dock keeps it from the widgets that read again on any power setting (the battery, energy saver).
        setting->PowerSetting = GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS;
        Assert.True(global::Karpo.Dock.Dock.BrightnessOnly(PBT.PBT_POWERSETTINGCHANGE, (nint)setting));
        Assert.False(global::Karpo.Dock.Dock.BrightnessOnly(PBT.PBT_APMPOWERSTATUSCHANGE, (nint)setting));
        setting->PowerSetting = GUID.GUID_ENERGY_SAVER_STATUS;
        Assert.False(global::Karpo.Dock.Dock.BrightnessOnly(PBT.PBT_POWERSETTINGCHANGE, (nint)setting));
        Assert.False(global::Karpo.Dock.Dock.BrightnessOnly(PBT.PBT_POWERSETTINGCHANGE, 0));
    }

    /// <summary>A PC whose screen was not found built in reads every display on the news, rather than none.</summary>
    [Fact]
    public void WithNoScreenFoundBuiltInTheNewsReadsEveryDisplay()
    {
        var machine = TwoMonitorsAndATelevision();
        using var brightness = new Brightness(machine, ready: true);
        machine.Turn(Other, 80);
        Assert.True(Notice(brightness, GUID.GUID_VIDEO_CURRENT_MONITOR_BRIGHTNESS, 80));
        Settled(brightness, _ => brightness.On(0x20)?.Level == 80);
    }

    /// <summary>
    /// A monitor's own buttons tell nobody: every display is read again on a look — the widget coming
    /// into view, the pointer onto it — but not more often than every LookEveryMs, and not just after
    /// it was made, which read them all.
    /// </summary>
    [Fact]
    public void ALookReadsEveryDisplayAtMostEveryFewSeconds()
    {
        var machine = Laptop();
        var clock = new TestClock();
        using var brightness = new Brightness(machine, ready: true, clock: clock);
        machine.Turn(Samsung, 15);
        var reads = machine.Reads;
        brightness.Look();
        Thread.Sleep(100);
        Assert.Equal(reads, machine.Reads);

        clock.Advance(TimeSpan.FromMilliseconds(Brightness.LookEveryMs));
        brightness.Look();
        Settled(brightness, _ => brightness.On(0x10)?.Level == 15);
        // Every display with a way to it, the television included, which has none and is asked all the same.
        Wait(() => machine.Reads == reads + 4);
        brightness.Look();
        clock.Advance(TimeSpan.FromMilliseconds(Brightness.LookEveryMs - 1));
        brightness.Look();
        Thread.Sleep(100);
        Assert.Equal(reads + 4, machine.Reads);
    }

    /// <summary>The notification Windows sends, laid out as POWERBROADCAST_SETTING with a DWORD, heard by <paramref name="brightness"/>.</summary>
    private static unsafe bool Notice(Brightness brightness, Guid setting, uint level)
    {
        var raw = stackalloc byte[24];
        var notice = (POWERBROADCAST_SETTING*)raw;
        notice->PowerSetting = setting;
        notice->DataLength = 4;
        *(uint*)(raw + 20) = level;
        return brightness.Heard(notice);
    }

    /// <summary>The screens once <paramref name="done"/> holds, waited for off the worker; the test fails after five seconds.</summary>
    private static IReadOnlyList<ScreenLevel> Settled(Brightness brightness, Func<IReadOnlyList<ScreenLevel>, bool> done)
    {
        Wait(() => done(brightness.Screens));
        return brightness.Screens;
    }

    private static void Wait(Func<bool> done)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (!done())
        {
            Assert.True(DateTime.UtcNow < until, "the brightness worker did not get there");
            Thread.Sleep(5);
        }
    }
}
