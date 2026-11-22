// menus: a stepper's value has one slot for its whole range (§456, the person, 2026-10-31: "when changing row
// height the minus button always shifts with every half step. this should be fixed"; Flyout.Controls.cs). Held here:
//
//  - **The height's "−" stays put** from "6 rows" to "6½ rows" and back, the menu the same size.
//  - **Every stepper knows its range** (FlyoutControl.Stepper.Range): each of the menu corpus's, its value among it.
//  - **Every stepper's "−" stays put over its range** on a real menu, a switch's button too.
//
// Needs Direct2D and a window station for the [UiFact]s: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Menus;

public sealed class StepperSlotTests
{
    private static readonly RECT Work = new() { right = 1920 * 3 / 2, bottom = 1040 * 3 / 2 };

    /// <summary>The line's height rows as a menu of their own, at <paramref name="cells"/> of a fixed height.</summary>
    private static List<FlyoutItem> Height(int cells) =>
        [new(2, "Open calendar"), .. LineHeightRows.Rows(new LineHeightChoice(ListKind.Calendar, LineHeight.Exactly(cells), true, LineHeight.Fewest, 40)).Select(item => item with { Foot = false })];

    /// <summary>
    /// §456: the height's stepper from "6 rows" to "6½ rows", "7 rows" and back: its "−" where it was each step,
    /// its "+" too, and the menu the same size (red first: the "−" stood further left at "6½ rows").
    /// </summary>
    [UiFact]
    public void TheHeightsMinusStaysPutEveryHalfStep()
    {
        WindowShots.OnHiddenDesktop("stepper-slot-height", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                using var menu = new Flyout(gfx, instance, 0);
                menu.Show(gfx, AddListPictures.LookFor(true), scale, Height(12), FlyoutLayout.List, new POINT { x = Work.right / 2, y = Work.bottom }, Work, Edge.Bottom, null);
                WindowShots.Settle();
                var fewer = menu.StepperButton(LineHeightRows.FewerId, more: false);
                var more = menu.StepperButton(LineHeightRows.FewerId, more: true);
                var panel = menu.OpenPanels[0].Panel;
                Assert.NotNull(fewer);
                foreach (var cells in new[] { 13, 14, 13, 12, 3, 2, 19, 20 })
                {
                    menu.Refresh(Height(cells));
                    var said = Assert.IsType<FlyoutControl.Stepper>(menu.ShownItems.First(item => item.Id == LineHeightRows.FewerId).Control).Value;
                    Assert.True(fewer == menu.StepperButton(LineHeightRows.FewerId, more: false), $"at {scale}: the \"−\" moved at \"{said}\": x {fewer?.x} → {menu.StepperButton(LineHeightRows.FewerId, more: false)?.x}");
                    Assert.Equal(more, menu.StepperButton(LineHeightRows.FewerId, more: true));
                    Assert.Equal(panel, menu.OpenPanels[0].Panel);
                }
                menu.Hide();
            }
        });
    }

    /// <summary>Every stepper of the menu corpus, at every level, with the menu and the row it stands on.</summary>
    private static IEnumerable<(string Where, FlyoutControl.Stepper Stepper)> Steppers()
    {
        static IEnumerable<(string, FlyoutControl.Stepper)> Walk(string where, IReadOnlyList<FlyoutItem> items)
        {
            foreach (var item in items)
            {
                if (item.Control is FlyoutControl.Stepper stepper)
                {
                    yield return ($"{where} › {item.Text}", stepper);
                }
                foreach (var each in Walk($"{where} › {item.Text}", item.Children ?? []))
                {
                    yield return each;
                }
            }
        }
        // A time on a 12-hour clock too, whatever the machine's: its AM/PM switch.
        var twelve = new OptionRow("time", OptionRowKind.Time, "Time") { Value = 7 * 60, Least = 0, Most = AlarmTimes.Day - 1, TwelveHour = true, Morning = "AM", Evening = "PM" };
        var clock = MenuPickers.Rows(twelve, (_, _, value) => (uint)(9000 + value + AlarmTimes.Day));
        return MenuCorpus.All().SelectMany(menu => Walk(menu.Name, menu.Items)).Concat(Walk("a 12-hour time", clock));
    }

    /// <summary>
    /// §456: every stepper of the menu corpus says its range, its value among it — more than its value, both words of a
    /// switch —; the height's every count of its cells, "1 row" to its most.
    /// </summary>
    [Fact]
    public void EveryStepperKnowsItsRange()
    {
        var steppers = Steppers().ToList();
        Assert.NotEmpty(steppers);
        Assert.Contains(steppers, each => each.Stepper.Switch);
        foreach (var (where, stepper) in steppers)
        {
            Assert.True(stepper.Range.Contains(stepper.Value), $"{where}: \"{stepper.Value}\" is not in its range [{string.Join(", ", stepper.Range)}]");
            Assert.True(stepper.Range.Count > 1, $"{where}: its range is its value alone");
        }
        var height = Assert.IsType<FlyoutControl.Stepper>(Height(12).Single(item => item.Id == LineHeightRows.FewerId).Control);
        Assert.Equal(Enumerable.Range(LineHeight.Fewest, 40 - LineHeight.Fewest + 1).Select(LineHeightRows.Count), height.Range);
        Assert.Contains("6½ rows", height.Range);
    }

    /// <summary>
    /// §456: on a real menu, every stepper of the menu corpus — a number's, a time's, a length's, the height's, AM/PM — shown
    /// at each value of its range: its "−" (a switch's button) where it was at the first, its "+" too, the menu the same size.
    /// </summary>
    [UiFact]
    public void EverySteppersMinusStaysPutOverItsRange()
    {
        var steppers = Steppers().DistinctBy(each => string.Join("\n", each.Stepper.Range.Prepend(each.Stepper.Switch.ToString()))).ToList();
        WindowShots.OnHiddenDesktop("stepper-slot-corpus", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var (where, stepper) in steppers)
            {
                List<FlyoutItem> Shown(string value) => [new(2, "Open"), new(77, "Stepped", Control: stepper with { Value = value })];
                using var menu = new Flyout(gfx, instance, 0);
                menu.Show(gfx, AddListPictures.LookFor(true), scale, Shown(stepper.Range[0]), FlyoutLayout.List, new POINT { x = Work.right / 2, y = Work.bottom }, Work, Edge.Bottom, null);
                WindowShots.Settle();
                var fewer = menu.StepperButton(77, more: false);
                var more = menu.StepperButton(77, more: true);
                var panel = menu.OpenPanels[0].Panel;
                Assert.NotNull(fewer);
                // Every value up to 60 of them; past that, 60 spread over it and its last.
                var every = Math.Max(1, stepper.Range.Count / 60);
                foreach (var value in stepper.Range.Where((_, at) => at % every == 0 || at == stepper.Range.Count - 1))
                {
                    menu.Refresh(Shown(value));
                    Assert.True(fewer == menu.StepperButton(77, more: false), $"{where}: the \"−\" moved at \"{value}\": x {fewer?.x} → {menu.StepperButton(77, more: false)?.x}");
                    Assert.Equal(more, menu.StepperButton(77, more: true));
                    Assert.Equal(panel, menu.OpenPanels[0].Panel);
                }
                menu.Hide();
            }
        });
    }
}
