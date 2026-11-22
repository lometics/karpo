// What apps say about their windows, kept per window and shown per button (Karpo.Dock\AppSignals.cs):
// progress as the taskbar list's messages set it, the overlay's stack as ITaskbarList3's
// documentation describes it, a button of several windows, a window going, and the badges packaged
// apps set.

using Karpo.Dock;

namespace Karpo.Tests.Signals;

public sealed class AppSignalsTests
{
    private const nint One = 0x1A0D56;
    private const nint Two = 0x220A62;
    private const nint Three = 0x6B0C6C;

    private static Pixels Picture(byte shade) => new() { Data = [shade, shade, shade, 255], Width = 1, Height = 1 };

    // --- Progress, one window ------------------------------------------------------------------

    [Fact]
    public void ANewWindowShowsNothing()
    {
        var signals = new AppSignals();

        Assert.Equal(default, signals.For([One]));
        Assert.False(signals.For([One]).Any);
        Assert.Equal(0, signals.Count);
    }

    [Theory]
    [InlineData(0x1u, ProgressKind.Indeterminate)]
    [InlineData(0x2u, ProgressKind.Normal)]
    [InlineData(0x4u, ProgressKind.Error)]
    [InlineData(0x8u, ProgressKind.Paused)]
    // Several flags at once: the most urgent.
    [InlineData(0x6u, ProgressKind.Error)]
    [InlineData(0xAu, ProgressKind.Paused)]
    [InlineData(0x3u, ProgressKind.Normal)]
    [InlineData(0x0u, ProgressKind.None)]
    [InlineData(0x10u, ProgressKind.None)]
    public void EachStateIsDrawnAsTheTaskbarDrawsIt(uint flags, ProgressKind kind)
    {
        Assert.Equal(kind, AppSignals.KindOf(flags));
    }

    [Fact]
    public void ValuesArriveScaledToFFFE()
    {
        var signals = new AppSignals();

        // 30 of 100, as the probe saw it arrive: 0x4CCC.
        Assert.True(signals.ProgressValue(One, 0x4CCC));
        Assert.Equal(0.3f, signals.For([One]).Value, 3);
        // 150 of 100 arrives as the whole: 0xFFFE.
        signals.ProgressValue(One, 0xFFFE);
        Assert.Equal(1f, signals.For([One]).Value);
        // Nothing past the whole, whatever arrives.
        signals.ProgressValue(One, 0xFFFF_FF);
        Assert.Equal(1f, signals.For([One]).Value);
        signals.ProgressValue(One, -5);
        Assert.Equal(0f, signals.For([One]).Value);
    }

    [Fact]
    public void AValueAloneShowsProgress()
    {
        var signals = new AppSignals();

        signals.ProgressValue(One, 0x7FFF);

        Assert.Equal(ProgressKind.Normal, signals.For([One]).Progress);
    }

    [Fact]
    public void AValueEndsARunWithoutAnEnd()
    {
        var signals = new AppSignals();
        signals.ProgressState(One, 0x1);
        Assert.Equal(ProgressKind.Indeterminate, signals.For([One]).Progress);

        signals.ProgressValue(One, 0x2000);

        Assert.Equal(ProgressKind.Normal, signals.For([One]).Progress);
    }

    [Fact]
    public void PausedAndFailedStaySoAndShowTheirNewValue()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x4CCC);

        signals.ProgressState(One, 0x8);
        signals.ProgressValue(One, 0x7FFF);
        Assert.Equal(new TileSignal(ProgressKind.Paused, 0x7FFF / 65534f), signals.For([One]));

        signals.ProgressState(One, 0x4);
        signals.ProgressValue(One, 0xFFFE);
        Assert.Equal(new TileSignal(ProgressKind.Error, 1f), signals.For([One]));
    }

    [Fact]
    public void AStateKeepsTheValueSetBefore()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x4CCC);

        signals.ProgressState(One, 0x8);

        Assert.Equal(0.3f, signals.For([One]).Value, 3);
    }

    [Fact]
    public void NoProgressClearsItAndItsValue()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x4CCC);

        Assert.True(signals.ProgressState(One, 0x0));

        Assert.Equal(default, signals.For([One]));
        // And a window that says nothing any more is not kept.
        Assert.Equal(0, signals.Count);
        // A value after it starts over from nothing.
        signals.ProgressState(One, 0x2);
        Assert.Equal(0f, signals.For([One]).Value);
    }

    // --- Nothing done (Spotify's pin) ------------------------------------------------------------

    /// <summary>
    /// The regression: Spotify's pin had a hairline the width of the button where its running
    /// indicator should be — the progress's bare track, as Spotify keeps its window at TBPF_NORMAL
    /// with nothing done. An empty fill is no fill on Windows' taskbar, so every way of reporting a
    /// determinate progress with nothing done shows nothing, and the running indicator stays.
    /// </summary>
    [Theory]
    [InlineData("normal", new[] { 0x2 }, new int[0])]
    [InlineData("a value of 0", new int[0], new[] { 0 })]
    [InlineData("normal, then 0 of 100", new[] { 0x2 }, new[] { 0 })]
    [InlineData("0 of 100, then normal", new int[0], new[] { 0 }, true)]
    [InlineData("paused", new[] { 0x8 }, new int[0])]
    [InlineData("failed", new[] { 0x4 }, new int[0])]
    [InlineData("busy, then normal", new[] { 0x1, 0x2 }, new int[0])]
    public void NothingDoneShowsNothingAndTheIndicatorStays(string how, int[] states, int[] values, bool valuesFirst = false)
    {
        var signals = new AppSignals();
        void States()
        {
            foreach (var flags in states)
            {
                signals.ProgressState(One, (uint)flags);
            }
        }
        void Values()
        {
            foreach (var scaled in values)
            {
                signals.ProgressValue(One, scaled);
            }
        }
        if (valuesFirst)
        {
            Values();
            States();
        }
        else
        {
            States();
            Values();
        }

        var shown = signals.For([One]);
        Assert.True(shown.Progress == ProgressKind.None, $"{how}: {shown.Progress} at {shown.Value} shows");
        Assert.False(shown.ShowsProgress, how);
        Assert.False(shown.Any, how);
        Assert.Equal("", shown.Spoken);
    }

    [Fact]
    public void TheFirstShareDoneShowsAndBackToNothingHidesIt()
    {
        var signals = new AppSignals();
        signals.ProgressState(One, 0x2);
        Assert.False(signals.For([One]).Any);

        // The smallest share the taskbar list sends: 1 of 0xFFFE.
        signals.ProgressValue(One, 1);
        Assert.Equal(ProgressKind.Normal, signals.For([One]).Progress);

        // Back to nothing done (a new download starting over): the indicator again.
        signals.ProgressValue(One, 0);
        Assert.False(signals.For([One]).Any);

        // The state is kept all the while: the next share done shows, paused as it was set.
        signals.ProgressState(One, 0x8);
        signals.ProgressValue(One, 0x7FFF);
        Assert.Equal(new TileSignal(ProgressKind.Paused, 0x7FFF / 65534f), signals.For([One]));
    }

    [Fact]
    public void NothingDoneDoesNotHideAnotherWindowsProgress()
    {
        var signals = new AppSignals();
        // One window failed with nothing done, which would outrank the other's run.
        signals.ProgressState(One, 0x4);
        signals.ProgressState(Two, 0x1);
        Assert.Equal(ProgressKind.Indeterminate, signals.For([One, Two]).Progress);

        signals.ProgressValue(Three, 0x4000);
        Assert.Equal(new TileSignal(ProgressKind.Normal, 0x4000 / 65534f), signals.For([One, Three]));
    }

    [Theory]
    [InlineData(ProgressKind.None, 0.5f, false)]
    [InlineData(ProgressKind.Indeterminate, 0f, true)]
    [InlineData(ProgressKind.Normal, 0f, false)]
    [InlineData(ProgressKind.Normal, 0.0001f, true)]
    [InlineData(ProgressKind.Paused, 0f, false)]
    [InlineData(ProgressKind.Paused, 0.7f, true)]
    [InlineData(ProgressKind.Error, 0f, false)]
    [InlineData(ProgressKind.Error, 1f, true)]
    [InlineData(ProgressKind.Normal, float.NaN, false)]
    public void WhatShowsInTheIndicatorsPlace(ProgressKind kind, float value, bool shows)
    {
        Assert.Equal(shows, new TileSignal(kind, value).ShowsProgress);
        Assert.Equal(shows, new TileSignal(kind, value).Any);
    }

    [Fact]
    public void OnlyAChangeCounts()
    {
        var signals = new AppSignals();

        Assert.True(signals.ProgressState(One, 0x2));
        Assert.False(signals.ProgressState(One, 0x2));
        Assert.True(signals.ProgressValue(One, 0x100));
        Assert.False(signals.ProgressValue(One, 0x100));
        Assert.False(signals.ProgressState(Two, 0x0));
    }

    // --- The overlay ----------------------------------------------------------------------------

    [Fact]
    public void AnOverlayShowsWithItsDescription()
    {
        var signals = new AppSignals();
        var picture = Picture(200);

        signals.Overlay(One, picture);
        signals.OverlayText(One, "3 unread messages");

        var shown = signals.For([One]);
        Assert.Same(picture, shown.Overlay);
        Assert.Equal("3 unread messages", shown.OverlayText);
        Assert.Equal("3 unread messages", shown.Spoken);
    }

    [Fact]
    public void TakingTheOverlayAwayTakesItsDescription()
    {
        var signals = new AppSignals();
        signals.Overlay(One, Picture(1));
        signals.OverlayText(One, "Busy");

        Assert.True(signals.Overlay(One, null));

        Assert.Equal(default, signals.For([One]));
        Assert.Equal(0, signals.Count);
    }

    [Fact]
    public void ADescriptionWithoutAPictureIsNotKept()
    {
        var signals = new AppSignals();

        Assert.False(signals.OverlayText(One, "Busy"));
        Assert.Equal(0, signals.Count);
    }

    [Fact]
    public void AGroupShowsTheLastOverlayAndThenTheOneItCovered()
    {
        // ITaskbarList3::SetOverlayIcon's own example: windows 1, 2 and 3 set A, B and C in that
        // order; C shows. 2 takes B away, then 3 takes C away: A shows, as 1 still has it.
        var signals = new AppSignals();
        var (a, b, c) = (Picture(1), Picture(2), Picture(3));
        signals.Overlay(One, a);
        signals.Overlay(Two, b);
        signals.Overlay(Three, c);
        Assert.Same(c, signals.For([One, Two, Three]).Overlay);

        signals.Overlay(Two, null);
        Assert.Same(c, signals.For([One, Two, Three]).Overlay);
        signals.Overlay(Three, null);
        Assert.Same(a, signals.For([One, Two, Three]).Overlay);
    }

    [Fact]
    public void AnOverlaySetAgainIsTheLastOneAgain()
    {
        var signals = new AppSignals();
        var (a, b) = (Picture(1), Picture(2));
        signals.Overlay(One, a);
        signals.Overlay(Two, b);

        var again = Picture(9);
        signals.Overlay(One, again);

        Assert.Same(again, signals.For([Two, One]).Overlay);
    }

    // --- A button of several windows --------------------------------------------------------------

    [Fact]
    public void AGroupShowsTheProgressThatNeedsThePersonMost()
    {
        var signals = new AppSignals();
        signals.ProgressState(One, 0x1);
        signals.ProgressValue(Two, 0x4000);
        // Paused with something done: paused with nothing done shows nothing (below).
        signals.ProgressValue(Three, 0x2000);
        signals.ProgressState(Three, 0x8);

        Assert.Equal(ProgressKind.Paused, signals.For([One, Two, Three]).Progress);

        signals.ProgressState(Two, 0x4);
        Assert.Equal(ProgressKind.Error, signals.For([One, Two, Three]).Progress);
        Assert.Equal(0x4000 / 65534f, signals.For([One, Two, Three]).Value);
    }

    [Fact]
    public void AmongEqualsTheWindowUsedLastShows()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x1000);
        signals.ProgressValue(Two, 0x8000);

        // The button's windows come most recently used first.
        Assert.Equal(0x8000 / 65534f, signals.For([Two, One]).Value);
        Assert.Equal(0x1000 / 65534f, signals.For([One, Two]).Value);
    }

    [Fact]
    public void AButtonShowsOnlyItsOwnWindows()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x1000);
        signals.Overlay(Two, Picture(5));

        Assert.Equal(ProgressKind.Normal, signals.For([One]).Progress);
        Assert.Null(signals.For([One]).Overlay);
        Assert.Equal(default, signals.For([Three]));
        Assert.Equal(default, signals.For([]));
    }

    // --- Windows going ------------------------------------------------------------------------------

    [Fact]
    public void AWindowThatGoesTakesEverythingItSaidWithIt()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x1000);
        signals.Overlay(One, Picture(5));
        signals.OverlayText(One, "Away");
        signals.ProgressValue(Two, 0x2000);

        Assert.True(signals.Forget(One));

        Assert.Equal(default, signals.For([One]));
        Assert.Equal(ProgressKind.Normal, signals.For([Two]).Progress);
        Assert.False(signals.Forget(One));
    }

    [Fact]
    public void WindowsThatWentUnannouncedArePruned()
    {
        var signals = new AppSignals();
        signals.ProgressValue(One, 0x1000);
        signals.ProgressValue(Two, 0x2000);

        Assert.True(signals.Prune(hwnd => hwnd == Two));

        Assert.Equal(default, signals.For([One]));
        Assert.Equal(ProgressKind.Normal, signals.For([Two]).Progress);
        Assert.False(signals.Prune(hwnd => hwnd == Two));
    }

    // --- Badges -------------------------------------------------------------------------------------

    [Theory]
    [InlineData("<badge value=\"1\"/>", 1, "1")]
    [InlineData("<badge value=\"11\"/>", 11, "11")]
    [InlineData("<badge value=\"99\"/>", 99, "99")]
    [InlineData("<badge value=\"100\"/>", 100, "99+")]
    [InlineData("<badge value=\"123456789012\"/>", 100, "99+")]
    [InlineData("<badge value=\" 7 \"/>", 7, "7")]
    public void ACountShowsAsWindowsShowsIt(string xml, int count, string text)
    {
        var badge = Badge.Parse(xml);

        Assert.NotNull(badge);
        Assert.Equal(count, badge!.Value.Count);
        Assert.Equal(text, badge.Value.Text);
        Assert.False(badge.Value.IsGlyph);
    }

    [Theory]
    [InlineData("<badge value=\"0\"/>")]
    [InlineData("<badge value=\"none\"/>")]
    [InlineData("<badge value=\"None\"/>")]
    [InlineData("<badge value=\"\"/>")]
    [InlineData("<badge/>")]
    [InlineData("<badge value=\"-3\"/>")]
    [InlineData("<badge value=\"sparkles\"/>")]
    [InlineData("<tile value=\"3\"/>")]
    [InlineData("<badge value=\"3\"")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElseShowsNoBadge(string? xml)
    {
        Assert.Null(Badge.Parse(xml));
    }

    [Theory]
    [InlineData("activity")]
    [InlineData("alarm")]
    [InlineData("alert")]
    [InlineData("attention")]
    [InlineData("available")]
    [InlineData("away")]
    [InlineData("busy")]
    [InlineData("error")]
    [InlineData("newMessage")]
    [InlineData("paused")]
    [InlineData("playing")]
    [InlineData("unavailable")]
    public void EveryGlyphWindowsKnowsIsAGlyph(string value)
    {
        var badge = Badge.Parse($"<badge value=\"{value}\"/>");

        Assert.NotNull(badge);
        Assert.True(badge!.Value.IsGlyph);
        Assert.Equal(value, badge.Value.Glyph);
        // One character of the icon font's private area.
        Assert.Single(badge.Value.Text);
        Assert.InRange(badge.Value.Text[0], '', '');
        Assert.False(string.IsNullOrWhiteSpace(badge.Value.Spoken));
    }

    [Fact]
    public void GlyphsAreReadAsWindowsReadsThem()
    {
        Assert.Equal("newMessage", Badge.Of("NEWMESSAGE")?.Glyph);
    }

    [Theory]
    [InlineData(1, "1 notification")]
    [InlineData(5, "5 notifications")]
    [InlineData(100, "more than 99 notifications")]
    public void ACountIsSaid(int count, string spoken)
    {
        Assert.Equal(spoken, Badge.Of(count.ToString())!.Value.Spoken);
    }

    [Fact]
    public void AButtonCarriesItsAppsBadge()
    {
        var signals = new AppSignals();
        var badge = Badge.Of("3");

        Assert.Equal(badge, signals.For([One], badge).Badge);
        Assert.True(signals.For([], badge).Any);
    }

    /// <summary>
    /// A folder's button shows the badges of the apps pinned in it together, so a count is seen with
    /// the folder shut: the counts added up, "99+" past 99; a glyph only where nothing counts.
    /// </summary>
    [Fact]
    public void AFoldersBadgeIsItsAppsBadgesTogether()
    {
        Assert.Null(Badge.Together([]));
        Assert.Null(Badge.Together([null, null]));
        Assert.Equal(Badge.Of("5"), Badge.Together([Badge.Of("3"), null, Badge.Of("2")]));
        Assert.Equal(Badge.Of("5"), Badge.Together([Badge.Of("busy"), Badge.Of("3"), Badge.Of("2")]));
        Assert.Equal(Badge.Of("busy"), Badge.Together([null, Badge.Of("busy"), Badge.Of("alert")]));
        Assert.Equal("99+", Badge.Together([Badge.Of("60"), Badge.Of("60")])!.Value.Text);
    }

    [Fact]
    public void EverythingIsSaidInOrder()
    {
        var signals = new AppSignals();
        signals.Overlay(One, Picture(1));
        signals.OverlayText(One, "Do not disturb");
        signals.ProgressValue(One, 0x7FFF);

        Assert.Equal("Do not disturb, 50 % done", signals.For([One]).Spoken);
        // The badge takes the overlay's corner, and its description goes with it.
        Assert.Equal("2 notifications, 50 % done", signals.For([One], Badge.Of("2")).Spoken);
        signals.ProgressState(One, 0x1);
        Assert.Equal("Do not disturb, working", signals.For([One]).Spoken);
        signals.ProgressState(One, 0x4);
        Assert.Equal("Do not disturb, stopped by an error at 50 %", signals.For([One]).Spoken);
        Assert.Equal("", new TileSignal().Spoken);
    }

    // --- What a window's preview shows -----------------------------------------------------------

    private static ThumbButtonData Sent(uint id, string tip, uint flags = 0) => new(0x4 | 0x8, id, 0, 0, tip, flags);

    [Fact]
    public void AToolbarIsKeptPerWindow()
    {
        var signals = new AppSignals();

        Assert.True(signals.ThumbButtons(One, [Sent(1, "Previous"), Sent(2, "Play")], [], update: false));

        Assert.Equal(["Previous", "Play"], signals.ThumbOf(One)!.Buttons.Select(button => button.Tip));
        Assert.Null(signals.ThumbOf(Two));
        // A toolbar is not something the button draws.
        Assert.Equal(default, signals.For([One]));
    }

    [Fact]
    public void AnUpdateBeforeAnyToolbarChangesNothing()
    {
        var signals = new AppSignals();

        Assert.False(signals.ThumbButtons(One, [Sent(1, "Pause")], [], update: true));
        Assert.Null(signals.ThumbOf(One));
        Assert.Equal(0, signals.Count);
    }

    [Fact]
    public void AnUpdateChangesTheKeptToolbarAndOnlyAChangeCounts()
    {
        var signals = new AppSignals();
        signals.ThumbButtons(One, [Sent(1, "Previous"), Sent(2, "Play")], [], update: false);

        Assert.True(signals.ThumbButtons(One, [Sent(2, "Pause")], [], update: true));
        Assert.False(signals.ThumbButtons(One, [Sent(2, "Pause")], [], update: true));

        Assert.Equal(["Previous", "Pause"], signals.ThumbOf(One)!.Buttons.Select(button => button.Tip));
    }

    [Fact]
    public void TheTooltipAndTheClipAreKeptAndTakenAway()
    {
        var signals = new AppSignals();

        Assert.True(signals.ThumbnailTooltip(One, "Tooltip text"));
        Assert.True(signals.ThumbnailClip(One, new ThumbClip(10, 20, 110, 220)));
        Assert.Equal(new ThumbInfo([], "Tooltip text", new ThumbClip(10, 20, 110, 220)), signals.ThumbOf(One));

        Assert.True(signals.ThumbnailTooltip(One, null));
        Assert.True(signals.ThumbnailClip(One, null));
        Assert.Null(signals.ThumbOf(One));
        Assert.Equal(0, signals.Count);
        // Taking away what was never there is no change.
        Assert.False(signals.ThumbnailTooltip(Two, null));
        Assert.False(signals.ThumbnailClip(Two, null));
    }

    [Fact]
    public void AWindowThatGoesTakesItsToolbarWithIt()
    {
        var signals = new AppSignals();
        signals.ThumbButtons(One, [Sent(1, "Play")], [], update: false);
        signals.ThumbnailTooltip(One, "Tip");

        Assert.True(signals.Forget(One));

        Assert.Null(signals.ThumbOf(One));
    }

    // --- Tabs ----------------------------------------------------------------------------------------

    private const nint TabA = 0x1B0DA4;
    private const nint TabB = 0x180D9C;
    private const nint TabC = 0x70DC8;

    [Fact]
    public void TabsBelongToTheirWindowInTheOrderRegistered()
    {
        var signals = new AppSignals();

        Assert.True(signals.RegisterTab(TabA, One));
        Assert.True(signals.RegisterTab(TabB, One));
        Assert.False(signals.RegisterTab(TabB, One));

        Assert.Equal([TabA, TabB], signals.TabsOf(One));
        Assert.Equal(One, signals.MainOf(TabB));
        Assert.Equal(0, signals.MainOf(One));
        Assert.Empty(signals.TabsOf(Two));
    }

    [Fact]
    public void ATabMovesBeforeAnotherOrToTheEnd()
    {
        var signals = new AppSignals();
        signals.RegisterTab(TabA, One);
        signals.RegisterTab(TabB, One);
        signals.RegisterTab(TabC, One);

        // SetTabOrder(C, A): C before A, as the probe's "tab2 before tab1".
        Assert.True(signals.TabOrder(TabC, TabA));
        Assert.Equal([TabC, TabA, TabB], signals.TabsOf(One));
        // Before nothing: to the end.
        Assert.True(signals.TabOrder(TabC, 0));
        Assert.Equal([TabA, TabB, TabC], signals.TabsOf(One));
        Assert.False(signals.TabOrder(0x999, TabA));
    }

    [Fact]
    public void ATabRegisteredWithAnotherWindowMovesThere()
    {
        var signals = new AppSignals();
        signals.RegisterTab(TabA, One);

        Assert.True(signals.RegisterTab(TabA, Two));

        Assert.Empty(signals.TabsOf(One));
        Assert.Equal([TabA], signals.TabsOf(Two));
    }

    [Fact]
    public void UnregisteredOrGoneTabsLeaveTheirWindow()
    {
        var signals = new AppSignals();
        signals.RegisterTab(TabA, One);
        signals.RegisterTab(TabB, One);

        Assert.True(signals.UnregisterTab(TabA));
        Assert.Equal([TabB], signals.TabsOf(One));
        Assert.True(signals.Forget(TabB));
        Assert.Empty(signals.TabsOf(One));
        Assert.Equal(0, signals.Count);
        Assert.False(signals.UnregisterTab(TabA));
    }

    [Fact]
    public void AWindowThatGoesTakesItsTabsWithIt()
    {
        var signals = new AppSignals();
        signals.RegisterTab(TabA, One);
        signals.RegisterTab(TabB, One);

        Assert.True(signals.Prune(hwnd => hwnd != One));

        Assert.Empty(signals.TabsOf(One));
        Assert.Equal(0, signals.MainOf(TabA));
    }

    // --- Badge rows ---------------------------------------------------------------------------------

    private const string Mail = "microsoft.windowscommunicationsapps_8wekyb3d8bbwe!microsoft.windowslive.mail";
    private const string Todo = "Microsoft.Todos_8wekyb3d8bbwe!App";

    [Fact]
    public void EachAppShowsItsNewestRow()
    {
        var now = 1_000_000L;
        var rows = new List<WpnBadge>
        {
            new(Mail, 900, 0, Badge.Of("4")),
            new(Todo, 800, 0, Badge.Of("2")),
            new(Mail, 700, 0, Badge.Of("9")),
        };

        var badges = WpnBadges.Current(rows, now);

        Assert.Equal(Badge.Of("4"), badges[Mail]);
        Assert.Equal(Badge.Of("2"), badges[Todo]);
        // AUMIDs compared as Windows compares them.
        Assert.True(badges.ContainsKey(Todo.ToUpperInvariant()));
    }

    [Fact]
    public void ANewestRowThatClearsTheBadgeLeavesNone()
    {
        var rows = new List<WpnBadge>
        {
            new(Mail, 900, 0, null),
            new(Mail, 700, 0, Badge.Of("9")),
        };

        Assert.Empty(WpnBadges.Current(rows, 1_000));
    }

    [Fact]
    public void AnExpiredBadgeShowsNoMore()
    {
        var rows = new List<WpnBadge>
        {
            new(Mail, 900, 950, Badge.Of("1")),
            new(Todo, 900, 2_000, Badge.Of("1")),
        };

        var badges = WpnBadges.Current(rows, 1_000);

        Assert.False(badges.ContainsKey(Mail));
        Assert.True(badges.ContainsKey(Todo));
        Assert.Empty(WpnBadges.Current(null, 1_000));
    }
}
