// The pointer over the tray's icons (Karpo.Dock\Tray.cs, TrayHover): which
// icon hears a move, when a version 4 app may open its popup in place of the
// tooltip, and that every popup opened is closed again, once, and only after
// the pointer rests.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class TrayHoverTests
{
    private static readonly TrayIcon A = Icon("{a}");
    private static readonly TrayIcon B = Icon("{b}");

    private static TrayIcon Icon(string key, string tip = "") =>
        new(key, HWND.NULL, 1, Guid.Empty, 0x8001, 4, null, tip, false);

    private static (string Key, TrayGesture Gesture)[] Sent(IReadOnlyList<(TrayIcon Icon, TrayGesture Gesture)> sends) =>
        sends.Select(send => (send.Icon.Key, send.Gesture)).ToArray();

    [Fact]
    public void EveryMoveOverAnIconIsSentAndOnlyTheFirstEntersIt()
    {
        var hover = new TrayHover();

        Assert.Equal([("{a}", TrayGesture.Move)], Sent(hover.Moved(A, out var first)));
        Assert.True(first);
        Assert.Equal([("{a}", TrayGesture.Move)], Sent(hover.Moved(A, out var again)));
        Assert.False(again);
        Assert.Same(A, hover.Over);
    }

    [Fact]
    public void RestingOpensThePopupOncePerVisit()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);

        Assert.Equal([("{a}", TrayGesture.PopupOpen)], Sent(hover.Rested()));
        Assert.Empty(hover.Rested());
        // Moving on the same icon keeps the popup; the app hears the moves.
        Assert.Equal([("{a}", TrayGesture.Move)], Sent(hover.Moved(A, out _)));
    }

    [Fact]
    public void AnotherIconClosesThePopupBeforeItsFirstMove()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);
        hover.Rested();

        Assert.Equal([("{a}", TrayGesture.PopupClose), ("{b}", TrayGesture.Move)], Sent(hover.Moved(B, out var entered)));
        Assert.True(entered);
        Assert.Equal([("{b}", TrayGesture.PopupOpen)], Sent(hover.Rested()));
    }

    [Fact]
    public void LeavingClosesAnOpenPopupOnce()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);
        hover.Rested();

        Assert.Equal([("{a}", TrayGesture.PopupClose)], Sent(hover.Left()));
        Assert.Empty(hover.Left());
        Assert.Null(hover.Over);
    }

    [Fact]
    public void NoPopupNoClose()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);

        Assert.Empty(hover.Left());
        hover.Moved(A, out _);
        Assert.Equal([("{b}", TrayGesture.Move)], Sent(hover.Moved(B, out _)));
    }

    [Fact]
    public void RestingOffTheIconsOpensNothing()
    {
        var hover = new TrayHover();

        Assert.Empty(hover.Moved(null, out var entered));
        Assert.False(entered);
        Assert.Empty(hover.Rested());
        hover.Moved(A, out _);
        hover.Moved(null, out _);
        Assert.Empty(hover.Rested());
    }

    [Fact]
    public void APressClosesThePopupAndItStaysClosedUntilThePointerLeavesTheIcon()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);
        hover.Rested();

        Assert.Equal([("{a}", TrayGesture.PopupClose)], Sent(hover.Pressed()));
        Assert.Empty(hover.Rested());
        Assert.Equal([("{a}", TrayGesture.Move)], Sent(hover.Moved(A, out _)));
        Assert.Empty(hover.Rested());

        // Off it and back: a new visit.
        hover.Moved(B, out _);
        hover.Moved(A, out var entered);
        Assert.True(entered);
        Assert.Equal([("{a}", TrayGesture.PopupOpen)], Sent(hover.Rested()));
    }

    [Fact]
    public void APressBeforeTheRestKeepsThePopupAway()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);

        Assert.Empty(hover.Pressed());
        Assert.Empty(hover.Rested());
    }

    [Fact]
    public void APressOffTheIconsChangesNothing()
    {
        var hover = new TrayHover();

        Assert.Empty(hover.Pressed());
        hover.Moved(A, out _);
        Assert.Equal([("{a}", TrayGesture.PopupOpen)], Sent(hover.Rested()));
    }

    [Fact]
    public void AnIconWithANewPictureIsTheSameIcon()
    {
        var hover = new TrayHover();
        hover.Moved(A, out _);
        hover.Rested();
        var redrawn = Icon("{a}", "Syncing");

        Assert.Equal([("{a}", TrayGesture.Move)], Sent(hover.Moved(redrawn, out var entered)));
        Assert.False(entered);
        Assert.Same(redrawn, hover.Over);
        Assert.Empty(hover.Rested());
        Assert.Equal([("{a}", TrayGesture.PopupClose)], Sent(hover.Left()));
    }
}
