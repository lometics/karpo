// Which tray icons stand on the bar, which wait in the hidden icon menu behind the chevron and which
// show nowhere, in what order, and what a drag between the two writes (TrayLayout): the state per
// icon, Windows' own choice where there is none, and the order dragging leaves behind — each tray
// widget its own (§347).

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayLayoutTests
{
    private static TrayEntry Icon(string identity, bool appHidden = false) => new(identity, appHidden);

    private static TrayIconOptions Fresh() => new();

    /// <summary>A tray widget of its own id with <paramref name="icons"/> as its own.</summary>
    private static BarItem Tray(string id, TrayIconOptions? icons = null)
    {
        var tray = BarKind.Tray.New();
        tray.Id = id;
        tray.TrayIcons = icons;
        return tray;
    }

    /// <summary>A config whose one layout holds <paramref name="trays"/>.</summary>
    private static Config With(params BarItem[] trays) => Karpo.Tests.Core.Configs.With(trays);

    /// <summary>What the bar shows and what waits in the hidden icon menu: "a,b | c".</summary>
    private static string Split(TrayIconOptions? tray, params TrayEntry[] icons)
    {
        var (shown, overflow) = TrayLayout.Split(tray, icons);
        return string.Join(",", shown.Select(icon => icon.Identity)) + " | " + string.Join(",", overflow.Select(icon => icon.Identity));
    }

    // --- What shows -----------------------------------------------------------------

    [Fact]
    public void AnIconNobodyChoseForGoesWhereWindowsSays()
    {
        // New icons appear where Windows' own choice (IsPromoted) puts them: promoted on the
        // taskbar, the rest in the hidden icon menu. Never hidden completely by default.
        var config = Fresh();

        Assert.Equal("a | b", Split(config, Icon("a"), Icon("b", appHidden: true)));
        Assert.Equal("a | b", Split(null, Icon("a"), Icon("b", appHidden: true)));
        Assert.Empty(TrayLayout.Off(config, [Icon("a"), Icon("b", appHidden: true)]));
        Assert.Equal(TrayState.Taskbar, TrayLayout.State(config, Icon("a")));
        Assert.Equal(TrayState.Overflow, TrayLayout.State(config, Icon("b", appHidden: true)));
    }

    [Fact]
    public void TheStateChosenWinsOverWindowsChoice()
    {
        var config = Fresh();
        config.States["b"] = TrayState.Taskbar;
        config.States["a"] = TrayState.Overflow;

        Assert.Equal("b | a", Split(config, Icon("a"), Icon("b", appHidden: true)));
    }

    [Fact]
    public void AnIconHiddenCompletelyShowsNowhere()
    {
        // "you should also be able to completely hide a system tray icon so that it does not even
        // show up behind the arrow": not on the bar, not in the hidden icon menu, whatever Windows says.
        var config = Fresh();
        config.States["a"] = TrayState.Hidden;
        config.States["b"] = TrayState.Hidden;
        TrayEntry[] icons = [Icon("a"), Icon("b", appHidden: true), Icon("c"), Icon("d", appHidden: true)];

        Assert.Equal("c | d", Split(config, icons));
        Assert.Equal(["a", "b"], TrayLayout.Off(config, icons).Select(icon => icon.Identity));
    }

    [Fact]
    public void ThereIsNoMostIconsShown()
    {
        // "most icons shown should not be a configuration option": twenty icons all stand on the bar.
        var config = Fresh();
        var icons = Enumerable.Range(0, 20).Select(index => Icon($"i{index}")).ToArray();

        var (shown, overflow) = TrayLayout.Split(config, icons);

        Assert.Equal(20, shown.Count);
        Assert.Empty(overflow);
    }

    [Fact]
    public void AStateThisBuildHasNoWordForIsNoChoice()
    {
        var options = Fresh();
        options.States["a"] = (TrayState)42;
        var config = With(Tray("t", options));

        Assert.Null(TrayLayout.Choice(options, "a"));
        Assert.Equal("a | ", Split(options, Icon("a")));
        Assert.True(TrayLayout.Repair(config));
        Assert.Null(TrayLayout.Trays(config).Single().TrayIcons);
    }

    // --- Each tray its own (§347) ---------------------------------------------------------

    [Fact]
    public void TwoTraysShowTheSameIconsEachItsOwnWay()
    {
        // The person: "scope these settings to the widget even".
        var home = Fresh();
        home.States["a"] = TrayState.Overflow;
        var work = Fresh();
        work.States["b"] = TrayState.Hidden;
        work.Order = ["c", "a"];
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c")];

        Assert.Equal("b,c | a", Split(home, icons));
        Assert.Equal("c,a | ", Split(work, icons));
    }

    [Fact]
    public void ADropWritesItsOwnTrayAlone()
    {
        var one = Tray("one");
        var two = Tray("two");
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c")];

        Assert.True(TrayLayout.Apply(one.TrayIcons ??= new(), icons, "a", new TrayDrop(TrayArea.Overflow, 0)));

        Assert.Equal("b,c | a", Split(one.TrayIcons, icons));
        Assert.Null(two.TrayIcons);
        Assert.Equal("a,b,c | ", Split(two.TrayIcons, icons));
    }

    // --- The order ------------------------------------------------------------------

    [Fact]
    public void ThePlacedIconsComeFirstAndNewOnesFollowInTheOrderTheyArrived()
    {
        var config = Fresh();
        config.Order = ["c", "a"];

        Assert.Equal("c,a,b,d | ", Split(config, Icon("a"), Icon("b"), Icon("c"), Icon("d")));
    }

    [Fact]
    public void AnOrderForIconsThatArentHereLeavesTheRestAlone()
    {
        var config = Fresh();
        config.Order = ["gone", "b"];

        Assert.Equal("b,a | ", Split(config, Icon("a"), Icon("b")));
    }

    // --- What a drop writes -----------------------------------------------------------

    [Fact]
    public void DraggingOntoTheBarShowsTheIconWhereItWasDropped()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c", appHidden: true)];

        Assert.True(TrayLayout.Apply(config, icons, "c", new TrayDrop(TrayArea.Shown, 1)));

        Assert.Equal("a,c,b | ", Split(config, icons));
        Assert.Equal(TrayState.Taskbar, config.States["c"]);
    }

    [Fact]
    public void DraggingIntoTheOverflowPutsItInTheHiddenIconMenu()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c", appHidden: true)];

        Assert.True(TrayLayout.Apply(config, icons, "a", new TrayDrop(TrayArea.Overflow, 0)));

        Assert.Equal("b | a,c", Split(config, icons));
        Assert.Equal(TrayState.Overflow, config.States["a"]);
    }

    [Fact]
    public void ADropOnTheBarRightOfAnIconPutsItAfterThatIcon()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c")];

        Assert.True(TrayLayout.Apply(config, icons, "a", new TrayDrop(TrayArea.Shown, 3)));

        Assert.Equal("b,c,a | ", Split(config, icons));
    }

    [Fact]
    public void ReorderingTheHiddenIconMenuKeepsItsIconsThere()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a", appHidden: true), Icon("b", appHidden: true), Icon("c", appHidden: true)];

        Assert.True(TrayLayout.Apply(config, icons, "c", new TrayDrop(TrayArea.Overflow, 0)));

        Assert.Equal(" | c,a,b", Split(config, icons));
        Assert.Equal(TrayState.Overflow, config.States["c"]);
    }

    [Fact]
    public void ADropThatChangesNothingSaysSo()
    {
        var config = Fresh();
        config.States["a"] = TrayState.Taskbar;
        TrayEntry[] icons = [Icon("a"), Icon("b")];

        Assert.False(TrayLayout.Apply(config, icons, "a", new TrayDrop(TrayArea.Shown, 0)));
        // An icon the tray doesn't have is no drop at all.
        Assert.False(TrayLayout.Apply(config, icons, "gone", new TrayDrop(TrayArea.Shown, 0)));
        Assert.False(TrayLayout.Apply(config, icons, "", new TrayDrop(TrayArea.Shown, 0)));
    }

    [Fact]
    public void AnIconThatIsntHereKeepsItsPlaceBehindTheRest()
    {
        var config = Fresh();
        config.Order = ["away", "a"];
        TrayEntry[] icons = [Icon("a"), Icon("b")];

        Assert.True(TrayLayout.Apply(config, icons, "b", new TrayDrop(TrayArea.Shown, 0)));

        Assert.Equal(["b", "a", "away"], config.Order);
    }

    [Fact]
    public void AnIconHiddenCompletelyKeepsItsPlaceWhileOthersAreDragged()
    {
        var config = Fresh();
        config.States["off"] = TrayState.Hidden;
        config.Order = ["a", "off", "b"];
        TrayEntry[] icons = [Icon("a"), Icon("off"), Icon("b")];

        Assert.True(TrayLayout.Apply(config, icons, "b", new TrayDrop(TrayArea.Shown, 0)));

        Assert.Equal("b,a | ", Split(config, icons));
        Assert.Contains("off", config.Order);
        Assert.Equal(TrayState.Hidden, config.States["off"]);
    }

    [Fact]
    public void APlaceOutsideTheListLandsAtItsEnd()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b")];

        Assert.True(TrayLayout.Apply(config, icons, "a", new TrayDrop(TrayArea.Shown, 99)));

        Assert.Equal("b,a | ", Split(config, icons));
    }

    // --- What the tray's menu sets -------------------------------------------------------

    [Fact]
    public void TheMenuSetsEachOfTheThreeStates()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c")];

        Assert.True(TrayLayout.Set(config, "a", TrayState.Overflow));
        Assert.True(TrayLayout.Set(config, "b", TrayState.Hidden));
        Assert.Equal("c | a", Split(config, icons));
        // Hidden completely comes back only from here: on the taskbar again.
        Assert.True(TrayLayout.Set(config, "b", TrayState.Taskbar));
        Assert.Equal("b,c | a", Split(config, icons));
        // The same again is no change, and nothing is made up.
        Assert.False(TrayLayout.Set(config, "b", TrayState.Taskbar));
        Assert.False(TrayLayout.Set(config, "", TrayState.Taskbar));
        Assert.False(TrayLayout.Set(config, "x", (TrayState)42));
    }

    // --- Icons coming and going -------------------------------------------------------

    [Fact]
    public void AnIconThatTurnsUpLaterJoinsTheEndAndNothingElseMoves()
    {
        var config = Fresh();
        config.Order = ["b", "a"];

        Assert.Equal("b,a | ", Split(config, Icon("a"), Icon("b")));
        // Its app started afterwards: it goes last, and b and a keep their places.
        Assert.Equal("b,a,late | ", Split(config, Icon("a"), Icon("b"), Icon("late")));
    }

    [Fact]
    public void TwoIconsOfOneAppAreBothPlaced()
    {
        // Two windows of one app, each with the same id: the tray reports two icons that the config
        // can only name once. Both must still be placed, one after the other.
        var config = Fresh();

        Assert.Equal("dup,dup,other | ", Split(config, Icon("dup"), Icon("dup"), Icon("other")));
    }

    [Fact]
    public void TwoIconsOfOneAppFollowTheStateChosenForIt()
    {
        var config = Fresh();
        config.States["dup"] = TrayState.Overflow;

        Assert.Equal("other | dup,dup", Split(config, Icon("dup"), Icon("dup"), Icon("other")));
    }

    [Fact]
    public void TheSameIdentityTwiceInTheOrderDoesNotLoseAnIcon()
    {
        var config = Fresh();
        config.Order = ["b", "b", "a"];

        Assert.Equal("b,a | ", Split(config, Icon("a"), Icon("b")));
    }

    // --- A drag from the pointer to the config (TrayDrags read by TrayLayout.Apply) -----------

    // One bar's tray as TrayDragsTests lays it out: the chevron from 100 to 132, then an icon every 32 px.
    private static TrayDrop Along(float along, int shown, int hidden, bool held) =>
        Karpo.Dock.TrayDrags.OnBar(along, 100, 400, 32, 32, shown, hidden, held)!.Value;

    [Fact]
    public void AnIconDraggedRightOntoAnotherTakesItsPlace()
    {
        // The person's report, "reordering tray icons does not properly work": a, dragged onto c's
        // right half, went after d.
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c"), Icon("d")];

        Assert.True(TrayLayout.Apply(config, icons, "a", Along(210, 4, 0, held: true)));

        Assert.Equal("b,c,a,d | ", Split(config, icons));
    }

    [Fact]
    public void AnIconDraggedLeftOntoAnotherTakesItsPlace()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c"), Icon("d")];

        // d onto b's right half: b's place, not after b.
        Assert.True(TrayLayout.Apply(config, icons, "d", Along(190, 4, 0, held: true)));

        Assert.Equal("a,d,b,c | ", Split(config, icons));
    }

    [Fact]
    public void AnIconLetGoOnItsOwnPlaceChangesNothingButItsChoice()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c")];

        _ = TrayLayout.Apply(config, icons, "b", Along(170, 3, 0, held: true));

        Assert.Equal("a,b,c | ", Split(config, icons));
    }

    [Fact]
    public void AnIconGoesIntoTheOverflowAndComesBackWhereItWasDropped()
    {
        // Into the hidden icon menu by the chevron, then out of it onto the bar between a and c.
        var config = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c"), Icon("x")];
        config.States["x"] = TrayState.Overflow;
        Assert.Equal("a,b,c | x", Split(config, icons));

        Assert.True(TrayLayout.Apply(config, icons, "b", Along(110, 3, 1, held: true)));
        Assert.Equal("a,c | x,b", Split(config, icons));
        Assert.Equal(TrayState.Overflow, config.States["b"]);

        // From the overflow the pointer takes the nearest boundary: 150 is past a's middle (148).
        Assert.True(TrayLayout.Apply(config, icons, "b", Along(150, 2, 2, held: false)));
        Assert.Equal("a,b,c | x", Split(config, icons));
        Assert.Equal(TrayState.Taskbar, config.States["b"]);
    }

    [Fact]
    public void AnIconMovesWithinTheOverflow()
    {
        var config = Fresh();
        TrayEntry[] icons = [Icon("a", true), Icon("b", true), Icon("c", true), Icon("d", true)];

        // a, row 0, let go just before d (row 3 as the flyout reads it): third of the other three.
        Assert.True(TrayLayout.Apply(config, icons, "a", Karpo.Dock.TrayDrags.InOverflow(3, 4, from: 0)));
        Assert.Equal(" | b,c,a,d", Split(config, icons));

        // And d back to the front.
        Assert.True(TrayLayout.Apply(config, icons, "d", Karpo.Dock.TrayDrags.InOverflow(0, 4, from: 3)));
        Assert.Equal(" | d,b,c,a", Split(config, icons));
    }

    [Fact]
    public void WhatADragWroteIsWhatTheNextStartReads()
    {
        var options = Fresh();
        TrayEntry[] icons = [Icon("a"), Icon("b"), Icon("c")];
        Assert.True(TrayLayout.Apply(options, icons, "c", new TrayDrop(TrayArea.Shown, 0)));
        Assert.True(TrayLayout.Apply(options, icons, "a", new TrayDrop(TrayArea.Overflow, 0)));
        options.States["b"] = TrayState.Hidden;

        var read = TrayLayout.Trays(Karpo.Tests.Core.Configs.RoundTrip(With(Tray("t", options), Tray("other")), out _)).ToList();

        Assert.Equal("c | a", Split(read[0].TrayIcons, icons));
        Assert.Equal(TrayState.Hidden, read[0].TrayIcons!.States["b"]);
        // And the icons arriving in another order at the next start change nothing.
        Assert.Equal("c | a", Split(read[0].TrayIcons, Icon("b"), Icon("a"), Icon("c")));
        // The other tray said nothing and says nothing.
        Assert.Null(read[1].TrayIcons);
    }

    // --- Windows' own choice (IsPromoted) ---------------------------------------------------

    [Fact]
    public void EachStateTellsWindowsWhatItHasAWordFor()
    {
        // Show on taskbar = promoted; the hidden icon menu = not promoted; hidden completely = not
        // promoted too, as Windows has no such thing (Karpo's own state keeps it hidden).
        Assert.True(TrayLayout.Promoted(TrayState.Taskbar));
        Assert.False(TrayLayout.Promoted(TrayState.Overflow));
        Assert.False(TrayLayout.Promoted(TrayState.Hidden));
    }

    [Fact]
    public void WindowsNewChoiceBecomesEveryTraysState()
    {
        // Chosen for the bar in Karpo, then switched off in Windows' Settings: Windows' is newer, in
        // every tray, one that said nothing too.
        var chose = Fresh();
        chose.States["a"] = TrayState.Taskbar;
        var config = With(Tray("one", chose), Tray("two"));
        TrayEntry[] icons = [Icon("a"), Icon("b")];

        Assert.True(TrayLayout.FollowWindows(config, "a", promoted: false));
        Assert.All(TrayLayout.Trays(config), tray => Assert.Equal("b | a", Split(tray.TrayIcons, icons)));
        // Saying the same again changes nothing.
        Assert.False(TrayLayout.FollowWindows(config, "a", promoted: false));
        Assert.True(TrayLayout.FollowWindows(config, "a", promoted: true));
        Assert.All(TrayLayout.Trays(config), tray => Assert.Equal(TrayState.Taskbar, tray.TrayIcons!.States["a"]));
    }

    [Fact]
    public void WindowsCannotBringBackAnIconHiddenCompletely()
    {
        // Completely hidden icons come back only from the tray's menu: Windows promoting the icon,
        // or Karpo's own write of "not promoted" read back, leaves it hidden.
        var off = Fresh();
        off.States["a"] = TrayState.Hidden;
        var config = With(Tray("t", off));

        Assert.False(TrayLayout.FollowWindows(config, "a", promoted: true));
        Assert.False(TrayLayout.FollowWindows(config, "a", promoted: false));
        Assert.Equal(TrayState.Hidden, off.States["a"]);
        Assert.Equal(" | ", Split(off, Icon("a")));
    }

    [Fact]
    public void OnlyNewStatesAreToldToWindows()
    {
        var was = Fresh();
        was.States["a"] = TrayState.Taskbar;
        was.States["b"] = TrayState.Overflow;
        was.States["d"] = TrayState.Taskbar;
        var before = With(Tray("t", was));
        var after = before.Clone();
        var now = TrayLayout.Trays(after).Single().TrayIcons!;
        now.States["b"] = TrayState.Taskbar;
        now.States["c"] = TrayState.Overflow;
        now.States["d"] = TrayState.Hidden;

        // b goes on the taskbar, c into the hidden icon menu, d is hidden completely (not promoted);
        // a is as it was, and tells Windows nothing.
        (string Identity, bool Promoted)[] expected = [("b", true), ("c", false), ("d", false)];
        Assert.Equal(expected, TrayLayout.NewChoices(before, after, _ => null).OrderBy(change => change.Identity).ToArray());
        Assert.Empty(TrayLayout.NewChoices(after, after.Clone(), _ => null));
    }

    [Fact]
    public void AChoiceInOneTrayLeavesTheOtherWhereItShowsTheIcon()
    {
        // Windows keeps one choice per icon: told "not promoted" for a, the other tray — which chose
        // nothing for a and showed it on its bar, as Windows said — keeps it on its bar.
        var before = With(Tray("one"), Tray("two"));
        var after = before.Clone();
        var (one, two) = (TrayLayout.Trays(after).First(), TrayLayout.Trays(after).Last());
        Assert.True(TrayLayout.Apply(one.TrayIcons ??= new(), [Icon("a"), Icon("b")], "a", new TrayDrop(TrayArea.Overflow, 0)));

        var told = TrayLayout.NewChoices(before, after, identity => identity == "a" ? false : null);

        Assert.Equal([("a", false)], told);
        Assert.Equal(TrayState.Taskbar, two.TrayIcons!.States["a"]);
        Assert.Equal("a,b | ", Split(two.TrayIcons, Icon("a", appHidden: true), Icon("b")));
        Assert.Equal("b | a", Split(one.TrayIcons, Icon("a", appHidden: true), Icon("b")));
    }

    // --- What a repair writes -----------------------------------------------------------

    [Fact]
    public void AFreshTrayHasNoStatesAndNeedsNoRepair()
    {
        var config = With(Tray("t"));

        Assert.Null(TrayLayout.Trays(config).Single().TrayIcons);
        Assert.False(TrayLayout.Repair(config));
    }

    [Fact]
    public void AStateSetBeforeAnAppUpdatedIsCarriedOver()
    {
        const string old = @"c:\program files\windowsapps\spotifyab.spotifymusic_1.300.277.0_x64__zpdnekdrzrea0\spotify.exe|0";
        var now = TrayIdentity.Stable(old);
        var config = With(Tray("t", new TrayIconOptions { States = { [old] = TrayState.Overflow }, Order = ["a", old] }));

        Assert.True(TrayLayout.Repair(config));

        var options = TrayLayout.Trays(config).Single().TrayIcons!;
        Assert.Equal(new Dictionary<string, TrayState> { [now] = TrayState.Overflow }, options.States);
        Assert.Equal(["a", now], options.Order);
        // And the icon of the version that runs now is the one it applies to.
        Assert.Equal("a | " + now, Split(options, Icon("a"), Icon(now)));
    }

    [Fact]
    public void TwoVersionsOfOneAppInAFileBecomeOneIcon()
    {
        const string old = @"c:\program files\windowsapps\spotifyab.spotifymusic_1.300.277.0_x64__zpdnekdrzrea0\spotify.exe|0";
        const string newer = @"c:\program files\windowsapps\spotifyab.spotifymusic_1.301.234.0_x64__zpdnekdrzrea0\spotify.exe|0";
        var config = With(Tray("t", new TrayIconOptions { States = { [old] = TrayState.Taskbar, [newer] = TrayState.Hidden }, Order = ["a", old, newer] }));

        Assert.True(TrayLayout.Repair(config));

        // The first word on it stands, and the order holds one place for the app.
        var options = TrayLayout.Trays(config).Single().TrayIcons!;
        Assert.Equal(TrayState.Taskbar, options.States[TrayIdentity.Stable(old)]);
        Assert.Equal(["a", TrayIdentity.Stable(old)], options.Order);
    }
}
