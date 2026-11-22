// What closes Karpo's popups and what the press that closes one does besides
// (LightDismiss): the rules the dock's bars and the popups' mouse hook follow.
// The regression is the user's "I right-clicked, then clicked into the taskbar
// again, and the context menu stayed open".
//
// And which rules each popup follows (Dock.RoleOf), which is the other half of
// the same question: the user's "sometimes I have to click twice" was Karpo's
// clock filing itself under the clock's *menu* when it had been opened from the
// bar's own clock, so it ate the click meant for the next widget.

using Karpo.Dock;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class LightDismissTests
{
    [Theory]
    [InlineData(PressButton.Left)]
    [InlineData(PressButton.Middle)]
    [InlineData(PressButton.Right)]
    public void A_menu_takes_the_press_that_closes_it_only_on_what_opened_it(PressButton button)
    {
        // The regression (the person, 2026-10-11: "when i have a context menu open and i right click
        // onto another widget i have to right click twice"): elsewhere on the bar the press goes on,
        // so another widget's menu opens, or it switches, at once, as Windows 11's jump lists do.
        Assert.False(LightDismiss.Consumes(PopupRole.Menu, onOpener: false, button));
        // On what opened it: it closes, and doesn't open again.
        Assert.True(LightDismiss.Consumes(PopupRole.Menu, onOpener: true, button));
    }

    [Fact]
    public void One_right_click_goes_from_one_widgets_menu_to_the_next()
    {
        foreach (var topic in new[] { TheDock.Topic.LayoutEditor, TheDock.Topic.WidgetOwn, TheDock.Topic.Desktops, TheDock.Topic.App, TheDock.Topic.Panel })
        {
            var open = new ClosedPopup(TheDock.RoleOf(topic), OnOpener: false);
            Assert.False(LightDismiss.Consumes([open], PressButton.Right), $"{topic} swallowed a right click on another widget");
            Assert.False(LightDismiss.Consumes([open], PressButton.Left), $"{topic} swallowed a click on another widget");
        }
    }

    [Fact]
    public void A_flyout_lets_a_press_elsewhere_go_on_and_toggles_on_its_opener()
    {
        // The calendar open, a click on Wi-Fi opens Wi-Fi at once, as on the taskbar.
        Assert.False(LightDismiss.Consumes(PopupRole.Flyout, onOpener: false, PressButton.Left));
        Assert.False(LightDismiss.Consumes(PopupRole.Flyout, onOpener: false, PressButton.Middle));
        Assert.False(LightDismiss.Consumes(PopupRole.Flyout, onOpener: false, PressButton.Right));
        // A second click on the clock closes the calendar and doesn't open it again.
        Assert.True(LightDismiss.Consumes(PopupRole.Flyout, onOpener: true, PressButton.Left));
        Assert.True(LightDismiss.Consumes(PopupRole.Flyout, onOpener: true, PressButton.Middle));
        // A right click on the clock still opens the clock's menu.
        Assert.False(LightDismiss.Consumes(PopupRole.Flyout, onOpener: true, PressButton.Right));
    }

    [Theory]
    [InlineData(false, PressButton.Left)]
    [InlineData(true, PressButton.Left)]
    [InlineData(true, PressButton.Right)]
    public void A_hover_popup_never_takes_a_press(bool onOpener, PressButton button)
    {
        // The window list or the desktop flyout goes, and the click switches, activates or launches.
        Assert.False(LightDismiss.Consumes(PopupRole.Hover, onOpener, button));
    }

    [Fact]
    public void Nothing_closed_takes_nothing()
    {
        Assert.False(LightDismiss.Consumes([], PressButton.Left));
    }

    [Fact]
    public void Any_popup_among_those_a_press_closed_on_its_opener_takes_it()
    {
        ClosedPopup[] menuAndList = [new(PopupRole.Hover, false), new(PopupRole.Menu, false)];
        Assert.False(LightDismiss.Consumes(menuAndList, PressButton.Left));
        ClosedPopup[] menuOnItsOpener = [new(PopupRole.Hover, false), new(PopupRole.Menu, true)];
        Assert.True(LightDismiss.Consumes(menuOnItsOpener, PressButton.Right));
        ClosedPopup[] flyoutAndList = [new(PopupRole.Hover, false), new(PopupRole.Flyout, false)];
        Assert.False(LightDismiss.Consumes(flyoutAndList, PressButton.Left));
        ClosedPopup[] flyoutOnItsOpener = [new(PopupRole.Hover, false), new(PopupRole.Flyout, true)];
        Assert.True(LightDismiss.Consumes(flyoutOnItsOpener, PressButton.Left));
    }

    [Fact]
    public void Escape_and_the_Windows_keys_close_and_only_Escape_gives_the_focus_back()
    {
        Assert.True(LightDismiss.ClosesOnKey(VK.VK_ESCAPE));
        Assert.True(LightDismiss.ClosesOnKey(VK.VK_LWIN));
        Assert.True(LightDismiss.ClosesOnKey(VK.VK_RWIN));
        Assert.True(LightDismiss.RestoresFocus(VK.VK_ESCAPE));
        Assert.False(LightDismiss.RestoresFocus(VK.VK_LWIN));
        Assert.False(LightDismiss.RestoresFocus(VK.VK_RWIN));
    }

    [Fact]
    public void Every_popup_says_which_rules_it_follows()
    {
        // No fall-back arm: a new topic that forgets to say throws here rather than becoming a menu
        // quietly, which is how Karpo's clock came to eat the click meant for the widget beside it.
        foreach (var topic in Enum.GetValues<TheDock.Topic>())
        {
            Assert.Contains(TheDock.RoleOf(topic), new[] { PopupRole.Menu, PopupRole.Flyout });
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => TheDock.RoleOf((TheDock.Topic)999));
    }

    [Fact]
    public void A_popup_a_click_on_the_bar_opens_is_a_flyout()
    {
        // Every one of them files itself under this one topic when it is dismissed, so there is one
        // answer to check: the calendar, a countdown's panel, the notification list, the quick-settings
        // pages, the weather, a drive, a folder, the search box, screen time and an island's own
        // list of items (Dock.OpenedByClick), and the tray's hidden icons beside them.
        Assert.Equal(PopupRole.Flyout, TheDock.RoleOf(TheDock.OpenedByClick));
        Assert.Equal(PopupRole.Flyout, TheDock.RoleOf(TheDock.Topic.Overflow));
    }

    [Fact]
    public void One_click_closes_the_open_flyout_and_opens_the_next()
    {
        // The user's "sometimes I have to click twice": with one widget's flyout open, a click on
        // another widget must close the first and open the second — one click, whichever pair it
        // is. The press is used up only where it landed on what opened the flyout, which is the
        // second-click-closes rule.
        var flyout = new ClosedPopup(TheDock.RoleOf(TheDock.OpenedByClick), OnOpener: false);
        var overflow = new ClosedPopup(TheDock.RoleOf(TheDock.Topic.Overflow), OnOpener: false);
        foreach (var open in new[] { flyout, overflow })
        {
            foreach (var button in new[] { PressButton.Left, PressButton.Middle, PressButton.Right })
            {
                Assert.False(LightDismiss.Consumes([open], button), $"{open.Role} swallowed a {button} press on another widget");
            }
        }
        // The same widget: the click closes it and does not open it again, and a right click on it
        // still opens its menu.
        Assert.True(LightDismiss.Consumes([new ClosedPopup(TheDock.RoleOf(TheDock.OpenedByClick), OnOpener: true)], PressButton.Left));
        Assert.False(LightDismiss.Consumes([new ClosedPopup(TheDock.RoleOf(TheDock.OpenedByClick), OnOpener: true)], PressButton.Right));
    }

    [Theory]
    [InlineData(VK.VK_RETURN)]
    [InlineData(VK.VK_SPACE)]
    [InlineData(VK.VK_DOWN)]
    [InlineData(VK.VK_TAB)]
    [InlineData(VK.VK_MENU)]
    [InlineData(VK.VK_APPS)]
    public void Other_keys_leave_the_popup_open(int key)
    {
        Assert.False(LightDismiss.ClosesOnKey(key));
    }

    /// <summary>§525: a dropdown's keys, as WinUI's ComboBox: F4, Alt+Down and Alt+Up open it and close it; Alt+F4 closes a window, plain arrows move.</summary>
    [Theory]
    [InlineData(VK.VK_F4, false, true)]
    [InlineData(VK.VK_DOWN, true, true)]
    [InlineData(VK.VK_UP, true, true)]
    [InlineData(VK.VK_F4, true, false)]
    [InlineData(VK.VK_DOWN, false, false)]
    [InlineData(VK.VK_UP, false, false)]
    [InlineData(VK.VK_RETURN, false, false)]
    [InlineData(VK.VK_LEFT, true, false)]
    public void A_dropdown_toggles_by_F4_and_Alt_with_an_arrow(int key, bool alt, bool toggles)
    {
        Assert.Equal(toggles, LightDismiss.TogglesDrop(key, alt));
    }

    /// <summary>§525: Alt is held where a system key's context code says so; never for a plain key.</summary>
    [Fact]
    public void Alt_is_read_from_a_system_keys_context_code()
    {
        Assert.True(LightDismiss.AltOf(WM.WM_SYSKEYDOWN, (LPARAM)(nint)(1 << 29)));
        Assert.False(LightDismiss.AltOf(WM.WM_SYSKEYDOWN, 0));
        Assert.False(LightDismiss.AltOf(WM.WM_KEYDOWN, (LPARAM)(nint)(1 << 29)));
    }

    [Fact]
    public void The_hook_leaves_a_press_on_a_bar_to_the_bar()
    {
        // The bug: the hook closed the menu before the bar heard the press, so the bar couldn't
        // tell the press was the one that closed it. A bar is always one of ours.
        Assert.Equal(HookPress.Bar, LightDismiss.Classify(inside: false, alsoInside: false, onBar: true, ours: true));
    }

    [Fact]
    public void The_hook_closes_for_a_press_anywhere_else()
    {
        Assert.Equal(HookPress.Ours, LightDismiss.Classify(inside: false, alsoInside: false, onBar: false, ours: true));
        Assert.Equal(HookPress.Elsewhere, LightDismiss.Classify(inside: false, alsoInside: false, onBar: false, ours: false));
    }

    /// <summary>
    /// A press the hook used up for a menu over a folder reached nothing else, a bar included: it
    /// closes the menu and gives the focus back, wherever it was (docs\widgets.md §185).
    /// </summary>
    [Fact]
    public void A_used_up_press_closes_the_menu_wherever_it_was()
    {
        foreach (var (onBar, ours) in new[] { (true, true), (false, true), (false, false) })
        {
            Assert.Equal(HookPress.Ours, LightDismiss.Classify(inside: false, alsoInside: false, onBar, ours, usedUp: true));
        }
        Assert.Equal(HookPress.Inside, LightDismiss.Classify(inside: true, alsoInside: false, onBar: false, ours: true, usedUp: true));
    }

    [Theory]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    public void A_press_on_the_popup_or_its_submenus_is_inside(bool inside, bool alsoInside, bool onBar, bool ours)
    {
        Assert.Equal(HookPress.Inside, LightDismiss.Classify(inside, alsoInside, onBar, ours));
    }
}
