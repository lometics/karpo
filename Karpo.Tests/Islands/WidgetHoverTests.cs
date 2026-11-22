// quick: opening a widget's flyout by pointing at it (WidgetHover), which is off unless somebody
// asks for it. The rules are pure, so they are all here: what a pointer arriving somewhere does,
// and what keeps an open one open when the grace runs out.
//
// The shapes worth holding: pointing at a widget with no flyout does nothing, a click's flyout is
// never closed by the pointer wandering off (it is pinned, and so is not "shown" here), a bar being
// arranged opens nothing, and nothing opens over a popup that already has the screen.

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Islands;

public sealed class WidgetHoverTests
{
    private static readonly Hit Wifi = Hit.Cell(2, 0);
    private static readonly Hit Weather = Hit.Cell(5, 0);

    [Fact]
    public void Opening_flyouts_by_pointing_is_off_until_it_is_asked_for()
    {
        Assert.False(new Config().DefaultLayout.OpenOnHover);
        // With it off nothing waits and nothing opens, wherever the pointer is.
        Assert.Equal(WidgetHoverAct.Nothing, WidgetHover.Pointer(on: false, editing: false, opens: true, Wifi, shown: null, otherPopup: false));
    }

    [Fact]
    public void A_pointer_resting_on_a_widget_with_a_flyout_waits_and_then_opens()
    {
        Assert.Equal(WidgetHoverAct.Arm, WidgetHover.Pointer(on: true, editing: false, opens: true, Wifi, shown: null, otherPopup: false));
        // The wait is long enough that crossing the bar on the way somewhere else opens nothing,
        // and the grace is shorter than the wait, so nothing lingers longer than it took to open.
        Assert.True(WidgetHover.OpenMs >= 300);
        Assert.InRange(WidgetHover.CloseMs, 1u, WidgetHover.OpenMs);
    }

    [Fact]
    public void A_widget_with_no_flyout_of_its_own_does_nothing()
    {
        // A switch, the volume slider, a separator: pointing at one opens nothing and closes nothing.
        Assert.Equal(WidgetHoverAct.Nothing, WidgetHover.Pointer(on: true, editing: false, opens: false, Wifi, shown: null, otherPopup: false));
        // With one of ours open, moving onto it is leaving, as moving off the bar is.
        Assert.Equal(WidgetHoverAct.Leave, WidgetHover.Pointer(on: true, editing: false, opens: false, Wifi, shown: Weather, otherPopup: true));
    }

    [Fact]
    public void The_pointer_sliding_to_the_next_widget_swaps_the_flyout_at_once()
    {
        Assert.Equal(WidgetHoverAct.Switch, WidgetHover.Pointer(on: true, editing: false, opens: true, Weather, shown: Wifi, otherPopup: true));
        // On the one that is already up, nothing happens: it stays.
        Assert.Equal(WidgetHoverAct.Nothing, WidgetHover.Pointer(on: true, editing: false, opens: true, Wifi, shown: Wifi, otherPopup: true));
    }

    [Fact]
    public void Leaving_the_bar_lets_what_pointing_opened_go()
    {
        Assert.Equal(WidgetHoverAct.Leave, WidgetHover.Pointer(on: true, editing: false, opens: false, hit: null, shown: Wifi, otherPopup: true));
        // Nothing of ours open: leaving is nothing at all, and no timer is left running.
        Assert.Equal(WidgetHoverAct.Nothing, WidgetHover.Pointer(on: true, editing: false, opens: false, hit: null, shown: null, otherPopup: false));
    }

    [Fact]
    public void Nothing_opens_over_a_popup_that_already_has_the_screen()
    {
        // A menu, a jump list, or a flyout a click opened and pinned: pointing at the next widget
        // must not open a second popup over it, as on the taskbar.
        Assert.Equal(WidgetHoverAct.Nothing, WidgetHover.Pointer(on: true, editing: false, opens: true, Weather, shown: null, otherPopup: true));
    }

    [Fact]
    public void A_bar_being_arranged_opens_nothing_and_closes_what_was_open()
    {
        Assert.Equal(WidgetHoverAct.Nothing, WidgetHover.Pointer(on: true, editing: true, opens: true, Wifi, shown: null, otherPopup: false));
        Assert.Equal(WidgetHoverAct.Leave, WidgetHover.Pointer(on: true, editing: true, opens: true, Wifi, shown: Wifi, otherPopup: true));
        // And switching the setting off with one open closes it the same way.
        Assert.Equal(WidgetHoverAct.Leave, WidgetHover.Pointer(on: false, editing: false, opens: true, Wifi, shown: Wifi, otherPopup: true));
    }

    [Fact]
    public void It_stays_while_the_pointer_is_on_the_panel_or_back_on_its_widget()
    {
        Assert.True(WidgetHover.Stays(insidePanel: true, onOpener: false));
        Assert.True(WidgetHover.Stays(insidePanel: false, onOpener: true));
        Assert.False(WidgetHover.Stays(insidePanel: false, onOpener: false));
    }
}
