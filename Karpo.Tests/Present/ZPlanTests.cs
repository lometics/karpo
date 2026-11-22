// The z-order plan's rules (docs\widgets.md §359, ZPlan): every stratum where it stands against the
// others, whatever order the windows came in, and in a stratum the rank, then the latest to the front.
// Each rule is a behaviour the hand ordering it replaced kept (EditStack.KeepOver, UnderOthers, the lift
// of the bars over the dim, FlyoutHost.Stand's own computing, every Lay's raise).

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Present;

public sealed class ZPlanTests
{
    private static HWND W(int n) => (HWND)(nint)n;

    private static Layered L(int n, Stratum stratum, int rank = 0, long front = 0) => new(W(n), stratum, rank, front);

    /// <summary>Whether <paramref name="over"/> stands over <paramref name="under"/> in <paramref name="plan"/> (top first).</summary>
    private static bool Over(List<HWND> plan, int over, int under) => plan.IndexOf(W(over)) is var a and >= 0 && plan.IndexOf(W(under)) is var b and >= 0 && a < b;

    [Fact]
    public void TheStrataStandInTheirOrderWhateverCameLast()
    {
        // Every stratum once, each the latest to come of all in turn: the order is the strata's.
        var strata = Enum.GetValues<Stratum>();
        for (var last = 0; last < strata.Length; last++)
        {
            var layered = strata.Select((stratum, index) => L(index + 1, stratum, front: index == last ? 100 : index)).ToList();
            var plan = ZPlan.Of(layered);
            Assert.Equal(strata.Reverse().Select(stratum => W(Array.IndexOf(strata, stratum) + 1)), plan);
        }
        Assert.Equal([Stratum.Dim, Stratum.ArrangedFolder, Stratum.Bar, Stratum.EditBar, Stratum.Popup, Stratum.Menu, Stratum.Tip, Stratum.Carry], strata);
    }

    [Fact]
    public void EditModesWindowsStandAsEditModeWantsThem()
    {
        // The dim (1), an arranged folder (2), a bar (3), the edit bar (4), a rail (5), the dim shown last.
        var plan = ZPlan.Of([L(1, Stratum.Dim, front: 9), L(2, Stratum.ArrangedFolder, front: 1), L(3, Stratum.Bar, front: 2), L(4, Stratum.EditBar, front: 3), L(5, Stratum.EditBar, front: 4)]);
        Assert.True(Over(plan, 2, 1), "the folder over its dim");
        Assert.True(Over(plan, 3, 2), "the bars over the arranged folder (§279)");
        Assert.True(Over(plan, 4, 2), "the edit bar over the folder (§297, §329)");
        Assert.True(Over(plan, 3, 1) && Over(plan, 4, 1) && Over(plan, 5, 1), "nothing under its dim, however late it came (EditStack's lift)");
        Assert.True(Over(plan, 4, 3) && Over(plan, 5, 3), "the edit bars over the bars");
    }

    [Fact]
    public void PopupsMenusTipsAndWhatIsCarriedStandOverTheBars()
    {
        // A folder at rest (1), a menu over it (2), its submenu (3), a tooltip (4), a carried block (5), a bar raised last (6).
        var plan = ZPlan.Of([L(1, Stratum.Popup, front: 1), L(2, Stratum.Menu, front: 2), L(3, Stratum.Menu, front: 3), L(4, Stratum.Tip, front: 4), L(5, Stratum.Carry, front: 5), L(6, Stratum.Bar, front: 99)]);
        Assert.Equal([W(5), W(4), W(3), W(2), W(1), W(6)], plan);
    }

    [Fact]
    public void InAStratumTheRankThenTheLatest()
    {
        // A place with only its edge's band (rank 0) under its display's places that hold something (rank 1), shown last or not.
        var plan = ZPlan.Of([L(1, Stratum.Bar, rank: 0, front: 9), L(2, Stratum.Bar, rank: 1, front: 1), L(3, Stratum.Bar, rank: 1, front: 2)]);
        Assert.Equal([W(3), W(2), W(1)], plan);
    }

    [Fact]
    public void WhatAWindowGoesUnder()
    {
        var plan = ZPlan.Of([L(1, Stratum.Dim), L(2, Stratum.ArrangedFolder), L(3, Stratum.Bar)]);
        // An arranged folder activated goes just under the bar planned over it (FlyoutHost.Stand).
        Assert.Equal(W(3), ZPlan.After(plan, W(2)));
        Assert.Equal(W(2), ZPlan.After(plan, W(1)));
        Assert.Equal(HWND.HWND_TOPMOST, ZPlan.After(plan, W(3)));
        Assert.Equal(HWND.HWND_TOPMOST, ZPlan.After(plan, W(7)));
        Assert.True(ZPlan.Same(plan, [W(3), W(2), W(1)]));
        Assert.False(ZPlan.Same(plan, [W(2), W(3), W(1)]));
    }
}
