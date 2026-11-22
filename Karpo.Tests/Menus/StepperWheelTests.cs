// §502: the wheel over a stepper steps it (the person, 2026-11-03: "scrolling over the rows height widget should
// actually change the value too."). It scrolled the menu, or did nothing where the menu does not scroll. Held here:
//
//  - **A notch is a step** (Flyout.WheelNotch): up is "+", down "−"; a fine wheel's small turns add up to a
//    notch, a turn the other way starts again, one step a message at most.
//  - **Over the height's stepper** — its "−", its value, its "+" — the wheel steps it as its buttons do (the same
//    Adjust, so the same limits and undo), the buttons standing still (§456); at its limit it does nothing and the
//    menu does not scroll under it.
//  - **Anywhere else the wheel scrolls**: over a plain row of a menu too long for its room, the menu scrolls and
//    no stepper steps; the height's stepper standing still at the menu's foot still steps, by the wheel and by its
//    buttons, the menu scrolled (its buttons' boxes went with the scroll, Row.Shift; they stood where they were).
//
// Needs Direct2D and a window station for the [UiFact]s: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Menus;

public sealed unsafe class StepperWheelTests
{
    private static readonly RECT Work = new() { right = 1920 * 3 / 2, bottom = 1040 * 3 / 2 };

    [Theory]
    [InlineData(0, 120, 1, 0)]
    [InlineData(0, -120, -1, 0)]
    [InlineData(0, 60, 0, 60)]
    [InlineData(60, 60, 1, 0)]
    [InlineData(-60, -60, -1, 0)]
    [InlineData(100, 50, 1, 30)]
    [InlineData(60, -60, 0, -60)]
    [InlineData(-90, 120, 1, 0)]
    [InlineData(0, 240, 1, 0)]
    [InlineData(0, -360, -1, 0)]
    [InlineData(0, 0, 0, 0)]
    public void ANotchIsAStep(int before, int delta, int step, int after)
    {
        var turned = before;
        Assert.Equal(step, Flyout.WheelNotch(ref turned, delta));
        Assert.Equal(after, turned);
    }

    private static LineHeightChoice Choice(int cells) => new(ListKind.Calendar, LineHeight.Exactly(cells), true, LineHeight.Fewest, 40);

    /// <summary>The line's height rows as a menu of their own (as StepperSlotTests' Height), at <paramref name="cells"/> of a fixed height.</summary>
    private static List<FlyoutItem> Height(int cells) =>
        [new(2, "Open calendar"), .. LineHeightRows.Rows(Choice(cells)).Select(item => item with { Foot = false })];

    private static LPARAM At(Flyout menu, POINT client)
    {
        var screen = client;
        _ = ClientToScreen(menu.Hwnd, &screen);
        return (LPARAM)(nint)((screen.y << 16) | (screen.x & 0xFFFF));
    }

    private static WPARAM Turned(int delta) => (WPARAM)(nuint)(unchecked((uint)(ushort)(short)delta) << 16);

    private static POINT Between(POINT fewer, POINT more) => new() { x = (fewer.x + more.x) / 2, y = (fewer.y + more.y) / 2 };

    /// <summary>
    /// Over the height's stepper the wheel steps it — over its value and over either button — as the buttons do,
    /// the buttons standing still; at its most it does nothing, the menu unscrolled.
    /// </summary>
    [UiFact]
    public void TheWheelOverTheHeightStepsIt()
    {
        WindowShots.OnHiddenDesktop("stepper-wheel-height", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                using var menu = new Flyout(gfx, instance, 0);
                menu.Show(gfx, AddListPictures.LookFor(true), scale, Height(12), FlyoutLayout.List, new POINT { x = Work.right / 2, y = Work.bottom }, Work, Edge.Bottom, null);
                WindowShots.Settle();
                var fewer = menu.StepperButton(LineHeightRows.FewerId, more: false)!.Value;
                var more = menu.StepperButton(LineHeightRows.FewerId, more: true)!.Value;
                var value = Between(fewer, more);

                var cells = 12;
                foreach (var (over, delta, then) in new[] { (value, 120, 13), (value, 120, 14), (value, -120, 13), (fewer, -120, 12), (more, 120, 13) })
                {
                    var heard = menu.Message(WM.WM_MOUSEWHEEL, Turned(delta), At(menu, over));
                    Assert.Equal(FlyoutEventKind.Adjust, heard.Kind);
                    Assert.Equal(delta > 0 ? LineHeightRows.MoreId : LineHeightRows.FewerId, heard.Id);
                    // The dock does as for a press on the button: the height the row asks for, and the rows shown again.
                    var next = Assert.IsType<LineHeight>(LineHeightRows.Act(Choice(cells), heard.Id));
                    cells = next.Cells;
                    Assert.Equal(then, cells);
                    menu.Refresh(Height(cells));
                    Assert.Equal(fewer, menu.StepperButton(LineHeightRows.FewerId, more: false));
                    Assert.Equal(more, menu.StepperButton(LineHeightRows.FewerId, more: true));
                }

                // A fine wheel: half a notch is nothing yet, the other half the step.
                Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_MOUSEWHEEL, Turned(60), At(menu, value)).Kind);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.MoreId), menu.Message(WM.WM_MOUSEWHEEL, Turned(60), At(menu, value)));

                // At its most, up does nothing.
                menu.Refresh(Height(40));
                Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_MOUSEWHEEL, Turned(120), At(menu, value)).Kind);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.FewerId), menu.Message(WM.WM_MOUSEWHEEL, Turned(-120), At(menu, value)));
                Assert.Equal(0f, menu.Scrolls[0]);

                // Over the row's words, off the stepper: no step.
                var words = menu.RowFill(LineHeightRows.FewerId)!.Value.Middle;
                words.x = fewer.x - (int)(40 * scale);
                Assert.NotEqual(FlyoutEventKind.Adjust, menu.Message(WM.WM_MOUSEWHEEL, Turned(120), At(menu, words)).Kind);
                menu.Hide();
            }
        });
    }

    /// <summary>
    /// A menu too long for its room, the height's rows at its foot as a line's menu has them: the wheel over a plain
    /// row scrolls it, and steps nothing; over the stepper at the foot it steps, and the menu stays where it was.
    /// </summary>
    [UiFact]
    public void ElsewhereTheWheelStillScrolls()
    {
        WindowShots.OnHiddenDesktop("stepper-wheel-scroll", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var small = new RECT { right = Work.right, bottom = 600 };
            List<FlyoutItem> items = [.. Enumerable.Range(0, 40).Select(at => new FlyoutItem((uint)(100 + at), $"Row {at}")), .. LineHeightRows.Rows(Choice(12))];
            using var menu = new Flyout(gfx, instance, 0);
            menu.Show(gfx, AddListPictures.LookFor(true), scale, items, FlyoutLayout.List, new POINT { x = small.right / 2, y = small.bottom }, small, Edge.Bottom, null);
            WindowShots.Settle();
            Assert.True(menu.ContentHeight > menu.ShownHeight, "the menu fits its room: nothing to scroll");

            var plain = menu.RowFill(101)!.Value.Middle;
            var heard = menu.Message(WM.WM_MOUSEWHEEL, Turned(-120), At(menu, plain));
            Assert.NotEqual(FlyoutEventKind.Adjust, heard.Kind);
            var scrolled = menu.Scrolls[0];
            Assert.True(scrolled > 0, "the wheel over a plain row did not scroll the menu");

            var fewer = menu.StepperButton(LineHeightRows.FewerId, more: false)!.Value;
            var more = menu.StepperButton(LineHeightRows.FewerId, more: true)!.Value;
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.FewerId), menu.Message(WM.WM_MOUSEWHEEL, Turned(-120), At(menu, Between(fewer, more))));
            Assert.Equal(scrolled, menu.Scrolls[0]);
            // Its "+" pressed, scrolled as it is: the foot's buttons went with the scroll (red first: they stood
            // where they were before it, a press on them nothing, and the wheel over them a scroll).
            var plus = (LPARAM)(nint)((more.y << 16) | (more.x & 0xFFFF));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, LineHeightRows.MoreId), menu.Message(WM.WM_LBUTTONDOWN, 1, plus));
            _ = menu.Message(WM.WM_LBUTTONUP, 0, plus);
            menu.Hide();
        });
    }
}
