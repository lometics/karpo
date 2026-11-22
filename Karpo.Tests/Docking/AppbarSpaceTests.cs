// The strip of screen the bar holds for the windows: how thick it is for each
// way an edge shows, where it lies on a display's edge, and which messages the shell
// has to be sent to get from one to the next (Karpo.Dock\AppbarSpace.cs).
// The Win32 side of it (ABM_QUERYPOS, ABM_SETPOS) is DockWindow.Reserve's.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Docking;

public sealed class AppbarSpaceTests
{
    // A 2560x1440 display at 150%, with a 48-DIP bar: 72 px of it.
    private static readonly RECT Display = new() { left = 0, top = 0, right = 2560, bottom = 1440 };

    private static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    [Fact]
    public void ABarThatIsAlwaysThereAndReservesHoldsItsWholeThickness()
    {
        Assert.Equal(72, AppbarSpace.Thickness(EdgeMode.Reserve, 72f));
        // A fraction of a pixel counts as a whole one: nothing may show through under it.
        Assert.Equal(73, AppbarSpace.Thickness(EdgeMode.Reserve, 72.2f));
    }

    /// <summary>A bar shown all the time over the windows, and an edge that comes in on hover, hold nothing.</summary>
    [Theory]
    [InlineData(EdgeMode.Float)]
    [InlineData(EdgeMode.Hover)]
    public void ABarThatHoldsNoSpaceHoldsNothing(EdgeMode mode)
    {
        Assert.Equal(0, AppbarSpace.Thickness(mode, 72f));
    }

    [Theory]
    [InlineData(Edge.Bottom, 0, 1368, 2560, 1440)]
    [InlineData(Edge.Top, 0, 0, 2560, 72)]
    [InlineData(Edge.Left, 0, 0, 72, 1440)]
    [InlineData(Edge.Right, 2488, 0, 2560, 1440)]
    public void TheStripLiesAlongItsEdgeOfTheDisplay(Edge edge, int left, int top, int right, int bottom)
    {
        var strip = AppbarSpace.Fit(Display, edge, 72);

        Assert.Equal((left, top, right, bottom), (strip.left, strip.top, strip.right, strip.bottom));
    }

    [Fact]
    public void AStripIsFittedAgainWhereTheShellMovedIt()
    {
        // What the shell answers when another bar already stands at the bottom: the
        // rectangle moved up, and its old thickness. The thickness is applied again
        // from the side it was moved to, which is where the bar ends up standing.
        var moved = new RECT { left = 0, top = 1296, right = 2560, bottom = 1368 };

        var strip = AppbarSpace.Fit(moved, Edge.Bottom, 72);

        Assert.Equal((1296, 1368), (strip.top, strip.bottom));
    }

    [Fact]
    public void ADisplayThatIsNotTheFirstKeepsItsOwnCoordinates()
    {
        var second = new RECT { left = 2560, top = -240, right = 4480, bottom = 840 };

        var strip = AppbarSpace.Fit(second, Edge.Bottom, 48);

        Assert.Equal((2560, 792, 4480, 840), (strip.left, strip.top, strip.right, strip.bottom));
    }

    // --- The empty strip, which is how space is given back ------------------------

    /// <summary>
    /// A bar that holds nothing asks for a strip of no thickness, still flush with its edge.
    /// That is what ABM_SETPOS is given while the bar hides, and the shell works the work area
    /// out again without the registration being touched.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 0, 1440, 2560, 1440)]
    [InlineData(Edge.Top, 0, 0, 2560, 0)]
    [InlineData(Edge.Left, 0, 0, 0, 1440)]
    [InlineData(Edge.Right, 2560, 0, 2560, 1440)]
    public void NoThicknessIsAnEmptyStripOnThatSide(Edge edge, int left, int top, int right, int bottom)
    {
        var strip = AppbarSpace.Fit(Display, edge, 0);

        Assert.Equal((left, top, right, bottom), (strip.left, strip.top, strip.right, strip.bottom));
        Assert.True(AppbarSpace.IsEmpty(strip));
    }

    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void AStripWithThicknessIsNotEmpty(Edge edge) =>
        Assert.False(AppbarSpace.IsEmpty(AppbarSpace.Fit(Display, edge, 72)));

    // --- What the shell is told, per transition -----------------------------------

    /// <summary>
    /// The regression this file exists for. Switching to auto-hide used to be a full
    /// re-registration (ABM_REMOVE then ABM_NEW), because removing gives the strip back for
    /// certain. It does — but ABM_NEW is refused now and then, and a bar the shell would not have
    /// back holds nothing and has no appbar to set a position on, so switching <b>back</b> to
    /// always-there sent no message at all: the work area was never worked out again and maximized
    /// windows kept the height they had while the bar was hiding, with the bar lying over them.
    /// Giving the space back is a position now, so the round trip never touches the registration
    /// and there is nothing to be refused.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void HidingAndComingBackNeverTouchesTheRegistration(Edge edge)
    {
        // It holds its whole strip: the bar is always there.
        var hiding = AppbarSpace.Plan(registered: true, positioned: true, edge, edge, thickness: 0);
        // Which leaves it on the empty strip, and then it is always there again.
        var back = AppbarSpace.Plan(registered: true, positioned: true, edge, edge, thickness: 72);

        Assert.Equal([AppbarStep.Position], hiding);
        Assert.Equal([AppbarStep.Position], back);
        Assert.DoesNotContain(AppbarStep.Register, hiding);
        Assert.DoesNotContain(AppbarStep.Register, back);
    }

    /// <summary>
    /// The same round trip written as the person does it, twice over, so a bar that has hidden
    /// and come back is in the state a bar that never hid is in.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Right)]
    public void TheRoundTripEndsWhereItStarted(Edge edge)
    {
        // What Reserve keeps: the strip the shell was last given, and how thick it was.
        var positioned = true;
        var held = 72;
        var sent = new List<AppbarStep>();
        foreach (var thickness in new[] { 0, 72, 0, 72 })
        {
            var steps = AppbarSpace.Plan(registered: true, positioned, edge, edge, thickness);
            sent.AddRange(steps);
            if (steps.Contains(AppbarStep.Register))
            {
                // Registering again gives the strip back and forgets it.
                (positioned, held) = (false, 0);
            }
            if (steps.Contains(AppbarStep.Position))
            {
                (positioned, held) = (true, thickness);
            }
        }

        Assert.Equal(72, held);
        Assert.Equal([AppbarStep.Position, AppbarStep.Position, AppbarStep.Position, AppbarStep.Position], sent);
    }

    /// <summary>
    /// A bar that holds nothing and wants nothing has nothing to say: the shell was told when it
    /// gave the space back, and saying it again would make the shell announce a new work area,
    /// which arranges every dock.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    public void ABarThatHasNeverBeenPositionedAndHidesSaysNothing(Edge edge) =>
        Assert.Empty(AppbarSpace.Plan(registered: true, positioned: false, edge, edge, thickness: 0));

    /// <summary>
    /// The regression the re-registration was written for: the shell keeps an appbar on the side
    /// it registered on, so a bar moved from the bottom to the right left a gap along the bottom
    /// and let maximized windows run underneath it on the right.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, Edge.Right)]
    [InlineData(Edge.Bottom, Edge.Top)]
    [InlineData(Edge.Bottom, Edge.Left)]
    [InlineData(Edge.Right, Edge.Bottom)]
    [InlineData(Edge.Left, Edge.Top)]
    public void MovingToAnotherSideRegistersAgainAndThenTakesTheNewStrip(Edge held, Edge edge) =>
        Assert.Equal(
            [AppbarStep.Register, AppbarStep.Position],
            AppbarSpace.Plan(registered: true, positioned: true, held, edge, thickness: 72));

    /// <summary>
    /// A bar that moves while it hides registers again too. It holds nothing either side of the
    /// move, so there is no strip to give back — but the shell keeps an appbar on the side it
    /// registered on, and a bar that came back on the new side would find its position refused.
    /// </summary>
    [Fact]
    public void MovingWhileItHidesStillRegistersAgain() =>
        Assert.Equal(
            [AppbarStep.Register],
            AppbarSpace.Plan(registered: true, positioned: true, Edge.Bottom, Edge.Right, thickness: 0));

    /// <summary>A new thickness on the same side is only a new rectangle: ABM_SETPOS does that.</summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Right)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    public void GrowingOnTheSameSideIsOnlyANewRectangle(Edge edge) =>
        Assert.Equal(
            [AppbarStep.Position],
            AppbarSpace.Plan(registered: true, positioned: true, edge, edge, thickness: 96));

    /// <summary>
    /// A bar the shell doesn't know yet registers first — a start at sign-in, before Explorer was
    /// there to register with, and an Explorer that has just restarted.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void ABarWithNoRegistrationRegistersFirst(Edge edge) =>
        Assert.Equal(
            [AppbarStep.Register, AppbarStep.Position],
            AppbarSpace.Plan(registered: false, positioned: false, edge, edge, thickness: 72));

    /// <summary>
    /// And registers even while it hides and holds nothing, because that registration is also how
    /// the bar hears that a full-screen app came or went (ABN_FULLSCREENAPP).
    /// </summary>
    [Fact]
    public void AHidingBarWithNoRegistrationStillRegisters() =>
        Assert.Equal(
            [AppbarStep.Register],
            AppbarSpace.Plan(registered: false, positioned: false, Edge.Bottom, Edge.Bottom, thickness: 0));

    /// <summary>
    /// Whatever it is asked, a plan never sets a position before the bar is registered, and never
    /// registers twice: the steps are in the order the messages go out.
    /// </summary>
    [Fact]
    public void EveryPlanIsInTheOrderTheMessagesGoOut()
    {
        foreach (var registered in new[] { false, true })
        {
            foreach (var positioned in new[] { false, true })
            {
                foreach (var held in Edges)
                {
                    foreach (var edge in Edges)
                    {
                        foreach (var thickness in new[] { 0, 72 })
                        {
                            var steps = AppbarSpace.Plan(registered, positioned, held, edge, thickness);

                            Assert.Equal(steps.Length, steps.Distinct().Count());
                            if (steps.Length == 2)
                            {
                                Assert.Equal([AppbarStep.Register, AppbarStep.Position], steps);
                            }
                            // A bar the shell doesn't know may not be given a position first.
                            if (!registered && steps.Contains(AppbarStep.Position))
                            {
                                Assert.Contains(AppbarStep.Register, steps);
                            }
                            // Everything that wants a strip ends up asking for one.
                            if (thickness > 0)
                            {
                                Assert.Contains(AppbarStep.Position, steps);
                            }
                        }
                    }
                }
            }
        }
    }

    /// <summary>The strip a move asks for is the new side's, in the display's own coordinates.</summary>
    [Fact]
    public void TheNewSidesStripIsTheOneAskedFor()
    {
        var right = AppbarSpace.Fit(Display, Edge.Right, 72);
        var bottom = AppbarSpace.Fit(Display, Edge.Bottom, 72);

        Assert.Equal((2488, 0, 2560, 1440), (right.left, right.top, right.right, right.bottom));
        Assert.Equal((0, 1368, 2560, 1440), (bottom.left, bottom.top, bottom.right, bottom.bottom));
        // Nothing of the old side is left in the new strip.
        Assert.True(right.top < bottom.top);
    }
}
