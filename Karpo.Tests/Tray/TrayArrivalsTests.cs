// Tray icons that stay where they are when their app adds them again, restarts,
// or Karpo starts again (TrayArrivals), and Windows' own choice for them heard
// only when it changes (WindowsChoices).

// Not Karpo.Tests.Tray: a local named "tray" elsewhere in the tests would shadow a namespace of that name.
namespace Karpo.Tests.TrayIcons;

public sealed class TrayArrivalsTests
{
    private static string Order(TrayArrivals arrivals, params string[] icons) =>
        string.Join(",", arrivals.Order(icons, icon => icon));

    [Fact]
    public void IconsComeInTheOrderTheyFirstArrived()
    {
        var arrivals = new TrayArrivals();

        Assert.Equal("a,b,c", Order(arrivals, "a", "b", "c"));
    }

    [Fact]
    public void AnIconItsAppAddsAgainKeepsItsPlace()
    {
        // The defect: an app taking its icon away and adding it again (or restarting) came in last,
        // so it jumped to the end of the bar, or past the maximum into the overflow.
        var arrivals = new TrayArrivals();
        _ = Order(arrivals, "a", "b", "c");

        // b's add now comes after c's.
        Assert.Equal("a,b,c", Order(arrivals, "a", "c", "b"));
        // Gone for a while and back: the same.
        Assert.Equal("a,c", Order(arrivals, "a", "c"));
        Assert.Equal("a,b,c", Order(arrivals, "c", "b", "a"));
    }

    [Fact]
    public void ANewIconGoesAfterEveryIconSeenBefore()
    {
        var arrivals = new TrayArrivals();
        _ = Order(arrivals, "a", "b");

        Assert.Equal("a,b,new", Order(arrivals, "new", "b", "a"));
    }

    [Fact]
    public void TheLastSessionsOrderStandsAtTheNextStart()
    {
        // Every start asks every app to add its icon again, and they answer in any order.
        var arrivals = new TrayArrivals();
        arrivals.Seed(["c", "a", "b"]);

        Assert.Equal("c,a,b,late", Order(arrivals, "a", "late", "b", "c"));
    }

    [Fact]
    public void TwoIconsOfOneIdentityKeepTheirOrder()
    {
        // Two windows of one app, one identity: both placed where it first came, one after the other.
        var arrivals = new TrayArrivals();
        string[] icons = ["dup1", "other", "dup2"];

        var ordered = arrivals.Order(icons, icon => icon.StartsWith("dup", StringComparison.Ordinal) ? "dup" : icon);

        Assert.Equal("dup1,dup2,other", string.Join(",", ordered));
    }

    [Fact]
    public void IdentitiesAreTheSameWhateverTheirCase()
    {
        var arrivals = new TrayArrivals();
        arrivals.Seed([@"c:\b.exe|0", @"c:\a.exe|0"]);

        Assert.Equal(@"C:\B.exe|0,c:\a.exe|0", Order(arrivals, @"c:\a.exe|0", @"C:\B.exe|0"));
    }

    // --- Windows' own choice ------------------------------------------------------------------

    [Fact]
    public void NothingChangesTheFirstTimeTheIconsAreSeen()
    {
        var choices = new WindowsChoices();

        Assert.Empty(choices.Changes(1, [("k1", "a", true), ("k2", "b", false)]));
    }

    [Fact]
    public void ASwitchInWindowsSettingsIsAChange()
    {
        var choices = new WindowsChoices();
        _ = choices.Changes(1, [("k1", "a", true), ("k2", "b", false)]);

        // The records were read again (generation 2): b switched on, a off.
        var changes = choices.Changes(2, [("k1", "a", false), ("k2", "b", true)]);

        Assert.Equal("a=False,b=True", string.Join(",", changes.Select(change => $"{change.Identity}={change.Promoted}")));
    }

    [Fact]
    public void WithoutTheRecordsReadAgainNothingIsAChange()
    {
        // An icon whose app added it again from another window is decided again on the same
        // records: what it comes out as is no word from the person.
        var choices = new WindowsChoices();
        _ = choices.Changes(1, [("k1", "a", true)]);

        Assert.Empty(choices.Changes(1, [("k1", "a", false)]));
        // And that is what the next reading is compared with.
        Assert.Empty(choices.Changes(2, [("k1", "a", false)]));
    }

    [Fact]
    public void AnIconSeenForTheFirstTimeIsNoChange()
    {
        var choices = new WindowsChoices();
        _ = choices.Changes(1, [("k1", "a", true)]);

        // A new window of the app (a new key), or a new app, after the records changed.
        Assert.Empty(choices.Changes(2, [("k1", "a", true), ("k9", "b", false)]));
    }

    [Fact]
    public void TwoIconsOfOneIdentityChangeItOnce()
    {
        var choices = new WindowsChoices();
        _ = choices.Changes(1, [("k1", "dup", true), ("k2", "dup", true)]);

        var changes = choices.Changes(2, [("k1", "dup", false), ("k2", "dup", false)]);

        Assert.Equal("dup=False", string.Join(",", changes.Select(change => $"{change.Identity}={change.Promoted}")));
    }

    /// <summary>
    /// §347: what Karpo itself told Windows for one tray's choice, read back, is no word from the person:
    /// every tray would follow it (TrayLayout.FollowWindows), the others' own places lost. Heard back
    /// once; a later switch in Windows' Settings to the same is the person's again.
    /// </summary>
    [Fact]
    public void KarposOwnWordReadBackIsNoChange()
    {
        var choices = new WindowsChoices();
        _ = choices.Changes(1, [("k1", "a", true), ("k2", "b", true)]);

        choices.Told("a", false);
        // Read before the write was: nothing yet.
        Assert.Empty(choices.Changes(2, [("k1", "a", true), ("k2", "b", true)]));
        // Read back, beside a switch the person made to b in Windows' Settings.
        var changes = choices.Changes(3, [("k1", "a", false), ("k2", "b", false)]);
        Assert.Equal("b=False", string.Join(",", changes.Select(change => $"{change.Identity}={change.Promoted}")));

        // On, then off again in Settings: the person's both times.
        Assert.Single(choices.Changes(4, [("k1", "a", true), ("k2", "b", false)]));
        Assert.Equal("a=False", string.Join(",", choices.Changes(5, [("k1", "a", false), ("k2", "b", false)]).Select(change => $"{change.Identity}={change.Promoted}")));
    }
}
