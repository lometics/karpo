// The person: "right now it does not layout shift when i am in the flyout. this should not be. it
// should always update the layout. not holding back when i am there with my mouse." Every flyout
// and every folder lays out what changed at once now, the pointer on it or not; the flyouts' own
// tests hold each panel to that (FlyoutFitTests, and the "-fit-" tests of each flyout). This holds
// what keeps a click honest once nothing waits: a press and its release are matched by the thing
// pressed, never by its place, in each owner that numbers its targets afresh with every layout —
// and the one place that still waits for a button to come up, the overflows.
//
// None of it needs a window: these are the rules themselves.

using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Bar = Karpo.Dock.Dock;

namespace Karpo.Tests.Islands;

public sealed class LiveLayoutTests
{
    // --- A folder: by the widget ------------------------------------------------------------

    /// <summary>A stand-in for a widget, which is all a folder's press needs of it: to be itself.</summary>
    public class Stand : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => null;
    }

    private static IIsland Widget() => DispatchProxy.Create<IIsland, Stand>();

    /// <summary>
    /// A line pressed is the line of the same widget after the folder is laid out again — a widget
    /// before it gone, the list shorter at once with the pointer on it — and none where its widget
    /// has gone; an edit "+", a place and not a thing, lets go.
    /// </summary>
    [Fact]
    public void AFolderPressStaysWithItsWidget()
    {
        var (first, pressed, last) = (Widget(), Widget(), Widget());
        IReadOnlyList<FolderTile> after = [new(pressed, new FolderCell()), new(last, new FolderCell())];
        // It was line 1, behind the first; the first went, and it is line 0.
        Assert.Equal(0, FolderFlyout.PressedAfter(1, pressed, after));
        Assert.Equal(1, FolderFlyout.PressedAfter(2, last, after));
        // Its widget went: nothing is pressed, so the release clicks nothing.
        Assert.Equal(-1, FolderFlyout.PressedAfter(0, first, after));
        Assert.Equal(-1, FolderFlyout.PressedAfter(-1, null, after));
        // A "+" is a place a new layout may move: the press lets go.
        Assert.Equal(-1, FolderFlyout.PressedAfter(FolderFlyout.AddTarget + 2, null, after));
    }

    // --- The overflows: they follow, but not under a button that is down ---------------------

    /// <summary>
    /// "…" and the tray's overflow, standing open, follow the bar behind them: shown again when what
    /// their rows stand for changed, closed when nothing is left, and left alone when nothing
    /// changed. A button down in one, or a drag, waits: their rows are numbered by place.
    /// </summary>
    [Fact]
    public void AnOpenOverflowFollowsTheBarUnlessAButtonIsDownInIt()
    {
        List<string> shown = ["0", "Teams\u001fTeams", "OneDrive\u001fOneDrive - Personal"];
        Assert.Equal(Bar.OverflowFollow.Stays, Bar.OverflowAfter(shown, [.. shown], held: false));
        // An icon came, one went, one was renamed, or the order changed: shown again.
        Assert.Equal(Bar.OverflowFollow.ShowAgain, Bar.OverflowAfter(shown, [.. shown, "Zoom\u001fZoom"], held: false));
        Assert.Equal(Bar.OverflowFollow.ShowAgain, Bar.OverflowAfter(shown, ["0", "Teams\u001fTeams"], held: false));
        Assert.Equal(Bar.OverflowFollow.ShowAgain, Bar.OverflowAfter(shown, ["0", "Teams\u001fTeams - 2 new", "OneDrive\u001fOneDrive - Personal"], held: false));
        Assert.Equal(Bar.OverflowFollow.ShowAgain, Bar.OverflowAfter(shown, ["0", "OneDrive\u001fOneDrive - Personal", "Teams\u001fTeams"], held: false));
        // Nothing left: it closes.
        Assert.Equal(Bar.OverflowFollow.Close, Bar.OverflowAfter(shown, [], held: false));
        // A button down in it, or a drag: it waits, whatever changed, and follows as it comes up.
        Assert.Equal(Bar.OverflowFollow.Stays, Bar.OverflowAfter(shown, [.. shown, "Zoom\u001fZoom"], held: true));
        Assert.Equal(Bar.OverflowFollow.Stays, Bar.OverflowAfter(shown, [], held: true));
    }
}
