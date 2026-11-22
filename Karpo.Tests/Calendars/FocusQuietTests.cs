// A focus session turns on Windows' do not disturb while it runs, as Windows 11's Focus does
// (Karpo.Dock\Clock\FocusQuiet.cs): on when it starts, off when it ends — runs out or is stopped —
// left alone where it was on before or the person switched it during the session, and held by
// several sessions together until the last one ends. A per-widget setting, "Turn on do not
// disturb", on unless somebody turned it off.
//
// Always through the service (DoNotDisturb) on its made-up source (FakeQuiet, DoNotDisturbTests),
// which records what it was asked and answers only when the test says Windows did — never Windows'
// own: nothing here switches the person's do not disturb.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Core;
using Karpo.Tests.QuickSettings;

namespace Karpo.Tests.Calendars;

public sealed class FocusQuietTests
{
    private static readonly DateTime Start = new(2026, 9, 27, 9, 0, 0, DateTimeKind.Local);

    private static readonly TimeSpan Length = TimeSpan.FromMinutes(25);

    /// <summary>
    /// A dock's part of it: its countdowns, Windows as the made-up source says it — off, on, or not
    /// yet answered (null) — the service over it, and the hold, with <paramref name="wanted"/> the
    /// sessions whose setting turns it on.
    /// </summary>
    private static (Countdowns Countdowns, FakeQuiet Windows, DoNotDisturb Quiet, FocusQuiet Focus) Made(bool? on, params string[] wanted)
    {
        var countdowns = new Countdowns();
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        if (on is { } known)
        {
            quiet.Look();
            windows.Answer(new DndState(known));
        }
        var focus = new FocusQuiet(countdowns, quiet);
        focus.Want(wanted.Length == 0 ? ["focus"] : wanted);
        return (countdowns, windows, quiet, focus);
    }

    /// <summary>Windows did what it was last asked.</summary>
    private static void Complies(FakeQuiet windows) => windows.Answer(new DndState(windows.Asked[^1]));

    /// <summary>
    /// A session turns it on when it starts and off when it runs out; started again, it turns it on
    /// again.
    /// </summary>
    [Fact]
    public void ASessionTurnsItOnAndItsEndTurnsItOff()
    {
        var (countdowns, windows, quiet, focus) = Made(on: false);

        countdowns.Start("focus", CountdownKind.Focus, Length, Start);
        Assert.Equal([true], windows.Asked);
        Assert.True(quiet.On);
        Complies(windows);
        Assert.True(focus.Holding && focus.TurnedOn);

        countdowns.Tick(Start + Length - TimeSpan.FromSeconds(1));
        Assert.Equal([true], windows.Asked);
        countdowns.Tick(Start + Length);
        Assert.Equal(CountdownPhase.Done, countdowns.Of("focus").Phase);
        Assert.Equal([true, false], windows.Asked);
        Complies(windows);
        Assert.False(quiet.On);
        Assert.False(focus.Holding);

        // Over and started again: a session of its own.
        countdowns.Act("focus", CountdownKind.Focus, Length, Start + Length + TimeSpan.FromMinutes(1));
        Assert.Equal([true, false, true], windows.Asked);
    }

    /// <summary>A pause is part of the session and keeps it on; stopping the session turns it off.</summary>
    [Fact]
    public void APauseKeepsItAndStoppingTurnsItOff()
    {
        var (countdowns, windows, _, _) = Made(on: false);
        countdowns.Start("focus", CountdownKind.Focus, Length, Start);
        Complies(windows);

        countdowns.Pause("focus", Start.AddMinutes(3));
        countdowns.Resume("focus", Start.AddMinutes(10));
        Assert.Equal([true], windows.Asked);

        countdowns.Clear("focus");
        Assert.Equal([true, false], windows.Asked);
    }

    /// <summary>On before the session started, it is the person's: the session neither sets it nor turns it off.</summary>
    [Fact]
    public void OnBeforeTheSessionItStaysOn()
    {
        var (countdowns, windows, quiet, focus) = Made(on: true);

        countdowns.Start("focus", CountdownKind.Focus, Length, Start);
        Assert.Empty(windows.Asked);
        Assert.True(focus.Holding);
        Assert.False(focus.TurnedOn);

        countdowns.Tick(Start + Length);
        Assert.Empty(windows.Asked);
        Assert.True(quiet.On);
    }

    /// <summary>
    /// The person switching it during the session has the last word: turned off on the bell, it stays
    /// off; turned off and on again, the session's end leaves it on; switched to alarms only in
    /// Windows, the session's end leaves that too.
    /// </summary>
    [Fact]
    public void ThePersonSwitchingItDuringTheSessionHasTheLastWord()
    {
        {
            var (countdowns, windows, quiet, _) = Made(on: false);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            Complies(windows);
            // The bell's toggle, on the same service.
            quiet.Set(false);
            Complies(windows);
            quiet.Set(true);
            Complies(windows);
            countdowns.Clear("focus");
            Assert.Equal([true, false, true], windows.Asked);
            Assert.True(quiet.On);
        }
        {
            var (countdowns, windows, quiet, _) = Made(on: false);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            Complies(windows);
            windows.Answer(new DndState(On: false));
            countdowns.Tick(Start + Length);
            Assert.Equal([true], windows.Asked);
            Assert.False(quiet.On);
        }
        {
            var (countdowns, windows, quiet, _) = Made(on: false);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            Complies(windows);
            windows.Answer(new DndState(On: true, AlarmsOnly: true));
            countdowns.Clear("focus");
            Assert.Equal([true], windows.Asked);
            Assert.True(quiet.State.AlarmsOnly);
        }
    }

    /// <summary>
    /// Several sessions at once hold it together: the first turns it on, the others find it held, and
    /// it goes off when the last of them ends — whichever that is.
    /// </summary>
    [Fact]
    public void SeveralSessionsHoldItTogetherUntilTheLastEnds()
    {
        var (countdowns, windows, _, focus) = Made(false, "deep", "reading", "afternoon");
        countdowns.Start("deep", CountdownKind.Focus, Length, Start);
        Complies(windows);
        countdowns.Start("reading", CountdownKind.Focus, TimeSpan.FromMinutes(50), Start.AddMinutes(5));
        countdowns.Start("afternoon", CountdownKind.Focus, TimeSpan.FromMinutes(60), Start.AddMinutes(6));
        Assert.Equal([true], windows.Asked);

        countdowns.Tick(Start + Length);
        Assert.Equal(CountdownPhase.Done, countdowns.Of("deep").Phase);
        countdowns.Clear("afternoon");
        Assert.Equal([true], windows.Asked);
        Assert.True(focus.Holding);

        countdowns.Pause("reading", Start.AddMinutes(30));
        Assert.Equal([true], windows.Asked);
        countdowns.Clear("reading");
        Assert.Equal([true, false], windows.Asked);
        Assert.False(focus.Holding);
    }

    /// <summary>
    /// Where Windows has not said yet what it is, the session waits for the answer rather than guess
    /// what it was before: off, and it turns it on; on, and it leaves it be. A session over before the
    /// answer came asks nothing at all.
    /// </summary>
    [Fact]
    public void WhatItWasBeforeIsWaitedFor()
    {
        {
            var (countdowns, windows, quiet, _) = Made(on: null);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            Assert.Equal(1, windows.Starts);
            Assert.Empty(windows.Asked);
            Assert.Null(quiet.On);
            windows.Answer(new DndState(On: false));
            Assert.Equal([true], windows.Asked);
            Complies(windows);
            countdowns.Clear("focus");
            Assert.Equal([true, false], windows.Asked);
        }
        {
            var (countdowns, windows, _, _) = Made(on: null);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            windows.Answer(new DndState(On: true));
            countdowns.Clear("focus");
            Assert.Empty(windows.Asked);
        }
        {
            var (countdowns, windows, _, _) = Made(on: null);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            countdowns.Clear("focus");
            windows.Answer(new DndState(On: false));
            Assert.Empty(windows.Asked);
        }
    }

    /// <summary>Where Windows will not let Karpo switch it, a session leaves it where it is.</summary>
    [Fact]
    public void WhereWindowsWillNotLetKarpoSwitchItNothingIsAsked()
    {
        var countdowns = new Countdowns();
        var windows = new FakeQuiet();
        var quiet = new DoNotDisturb(windows);
        quiet.Look();
        windows.Answer(new DndState(On: false, CanSwitch: false));
        using var focus = new FocusQuiet(countdowns, quiet);
        focus.Want(["focus"]);

        countdowns.Start("focus", CountdownKind.Focus, Length, Start);
        countdowns.Clear("focus");
        Assert.Empty(windows.Asked);
    }

    /// <summary>
    /// Only a focus session whose setting is on turns it on — a timer never does — and switching the
    /// setting while it runs takes hold of it or lets it go at once.
    /// </summary>
    [Fact]
    public void OnlyAFocusSessionWhoseSettingIsOnTurnsItOn()
    {
        var (countdowns, windows, _, focus) = Made(false, "focus", "timer");
        countdowns.Start("timer", CountdownKind.Timer, TimeSpan.FromMinutes(5), Start);
        countdowns.Start("other", CountdownKind.Focus, Length, Start);
        Assert.Empty(windows.Asked);

        // Its setting turned on while it runs, then off.
        focus.Want(["focus", "other"]);
        Assert.Equal([true], windows.Asked);
        Complies(windows);
        focus.Want(["focus"]);
        Assert.Equal([true, false], windows.Asked);
    }

    /// <summary>A focus-time widget removed while its session runs ends the session, and hands it back.</summary>
    [Fact]
    public void RemovingTheWidgetEndsItsSession()
    {
        var (countdowns, windows, _, _) = Made(on: false);
        countdowns.Start("focus", CountdownKind.Focus, Length, Start);
        Complies(windows);

        countdowns.Keep([]);
        Assert.Equal([true, false], windows.Asked);
    }

    /// <summary>
    /// The dock going ends every session with it: what a session turned on is turned off again, and
    /// what was the person's is left.
    /// </summary>
    [Fact]
    public void TheDockGoingHandsItBack()
    {
        {
            var (countdowns, windows, _, focus) = Made(on: false);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            Complies(windows);
            focus.Dispose();
            Assert.Equal([true, false], windows.Asked);
            // Gone, it hears nothing more.
            countdowns.Start("focus", CountdownKind.Focus, Length, Start.AddHours(1));
            Assert.Equal([true, false], windows.Asked);
        }
        {
            var (countdowns, windows, _, focus) = Made(on: true);
            countdowns.Start("focus", CountdownKind.Focus, Length, Start);
            focus.Dispose();
            Assert.Empty(windows.Asked);
        }
    }

    /// <summary>
    /// The sessions that turn it on, as the config has them: every focus-time widget, wherever it
    /// stands — on unless somebody turned it off — and never a
    /// timer.
    /// </summary>
    [Fact]
    public void TheConfigSaysWhichSessionsTurnItOn()
    {
        var plain = ListKind.Focus.New();
        var set = ListKind.Focus.New();
        set.Countdown = new CountdownOptions { Minutes = 50, DoNotDisturb = true };
        var off = ListKind.Focus.New();
        off.Countdown = new CountdownOptions { Minutes = 25, DoNotDisturb = false };
        var timer = ListKind.Timer.New();
        var list = Configs.FolderKind(FolderView.List).New();
        var line = ListKind.Focus.New();
        list.Folder!.Put(line);
        var config = Configs.With(Configs.Holding(plain, set, off, timer), list);

        Assert.Equal(
            new[] { plain.Id, set.Id, line.Id }.Order(),
            FocusQuiet.KeysIn(config).Order());
    }

    /// <summary>
    /// The setting is written only where somebody turned it off: the Settings page keeps nothing for
    /// on, a file's "on" is read as nothing, and a timer carries none.
    /// </summary>
    [Fact]
    public void TheSettingIsWrittenOnlyWhereItWasTurnedOff()
    {
        Assert.Null(CountdownLength.DoNotDisturb(true));
        Assert.False(CountdownLength.DoNotDisturb(false));

        var focus = ListKind.Focus.New();
        focus.Countdown = new CountdownOptions { Minutes = 25, DoNotDisturb = true };
        focus.Normalize();
        Assert.Null(focus.Countdown.DoNotDisturb);
        focus.Countdown.DoNotDisturb = false;
        focus.Normalize();
        Assert.False(focus.Countdown.DoNotDisturb);

        var timer = ListKind.Timer.New();
        timer.Countdown = new CountdownOptions { Minutes = 5, DoNotDisturb = false };
        timer.Normalize();
        Assert.Null(timer.Countdown.DoNotDisturb);
    }
}

/// <summary>"Turn on do not disturb" through a real save and load of config.json.</summary>
[Collection(ConfigFile.Collection)]
public sealed class FocusQuietFileTests : IDisposable
{
    public FocusQuietFileTests() => ConfigFile.Delete();

    public void Dispose() => ConfigFile.Delete();

    /// <summary>Turned off, it survives a save and a load; on, nothing is written.</summary>
    [Fact]
    public void TurnedOffItSurvivesASaveAndALoad()
    {
        var off = ListKind.Focus.New();
        off.Countdown = new CountdownOptions { Minutes = 25, DoNotDisturb = false };
        var on = ListKind.Focus.New();
        on.Countdown = new CountdownOptions { Minutes = 50, Name = "Deep work" };
        Configs.With(Configs.Holding(off), Configs.Holding(on)).Save();

        var loaded = Configs.Islands(Config.Load()).Select(folder => Assert.Single(folder.Folder!.Cells).Island).ToList();
        Assert.False(loaded[0].Countdown!.DoNotDisturb);
        Assert.Null(loaded[1].Countdown!.DoNotDisturb);

        using var document = System.Text.Json.JsonDocument.Parse(ConfigFile.Text());
        var islands = document.RootElement.GetProperty("layouts")[0].GetProperty("islands");
        Assert.False(Configs.Find(islands[0], "countdown")!.Value.GetProperty("doNotDisturb").GetBoolean());
        Assert.False(Configs.Find(islands[1], "countdown")!.Value.TryGetProperty("doNotDisturb", out _));
    }
}
