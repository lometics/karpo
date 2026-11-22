// §430: one click on what opened a popup closes it, and opens nothing (Openers, LightDismiss.cs): the click being
// handled remembers what it went down on and the openers of the popups its press closed.

using Karpo.Dock;

namespace Karpo.Tests.Menus;

public sealed class OpenersTests
{
    private static readonly PopupOpener Plus = new(1, "plus 0", PressButton.Left);

    private static readonly PopupOpener Line = new(2, "line", PressButton.Left);

    [Fact]
    public void AClickOnTheOpenerOfThePopupItsPressClosedOpensItNotAgain()
    {
        var openers = new Openers();
        // The press closed what the "+" opened (the bar closed it, or the hook before the press got here).
        openers.Pressed(Plus, closed: null);
        openers.Shut(Plus);
        Assert.True(openers.ClosedItsOwn);
        Assert.True(openers.Reopens(Plus));
        openers.Pressed(Line, closed: Line);
        Assert.True(openers.ClosedItsOwn);
        Assert.True(openers.Reopens(Line));
    }

    [Fact]
    public void AnotherOpenersClickOpensItsOwnInTheOneClick()
    {
        var openers = new Openers();
        openers.Pressed(Line, closed: Plus);
        Assert.False(openers.ClosedItsOwn);
        Assert.False(openers.Reopens(Line));
        // The same thing in another window is another opener.
        openers.Pressed(Plus with { Window = 3 }, closed: Plus);
        Assert.False(openers.ClosedItsOwn);
        Assert.False(openers.Reopens(Plus with { Window = 3 }));
    }

    /// <summary>A right click on what opened a flyout closes it and still opens its menu; the left click's own it closes.</summary>
    [Fact]
    public void TheOtherButtonOnTheSameThingOpensWhatItOpens()
    {
        var openers = new Openers();
        var right = Plus with { Button = PressButton.Right };
        openers.Pressed(right, closed: Plus);
        Assert.True(openers.ClosedItsOwn);
        Assert.False(openers.Reopens(right));
        Assert.True(openers.Reopens(Plus));
    }

    [Fact]
    public void ThePressStampsWhatOpensAndTheReleaseEndsTheClick()
    {
        var openers = new Openers();
        Assert.Null(openers.Press);
        openers.Pressed(Plus, closed: null);
        Assert.Equal(Plus, openers.Press);
        // Nothing closed: what the press goes on to open, opens.
        Assert.False(openers.ClosedItsOwn);
        Assert.False(openers.Reopens(Plus));
        openers.Shut(Plus);
        openers.Released();
        Assert.Null(openers.Press);
        Assert.False(openers.Reopens(Plus));
        Assert.False(openers.ClosedItsOwn);
        // A popup closed with no click under way (Escape, another app) is no click's.
        openers.Shut(Plus);
        Assert.False(openers.Reopens(Plus));
        // A popup that knows no opener.
        openers.Pressed(Plus, closed: null);
        openers.Shut(null);
        Assert.False(openers.ClosedItsOwn);
        Assert.False(openers.Reopens(null));
    }

    [Fact]
    public void AnOpenerIsItsWindowAndItsThingWhicheverButton()
    {
        Assert.True(Plus.On(Plus with { Button = PressButton.Middle }));
        Assert.False(Plus.On(Plus with { Part = "plus 1" }));
        Assert.False(Plus.On(Plus with { Window = 9 }));
        Assert.True(new PopupOpener(4, null, PressButton.Left).On(new PopupOpener(4, null, PressButton.Right)));
        // A bar's hit, boxed, is the same hit.
        Assert.True(new PopupOpener(4, new Hit(HitKind.Add, 2), PressButton.Left) == new PopupOpener(4, new Hit(HitKind.Add, 2), PressButton.Left));
    }
}
