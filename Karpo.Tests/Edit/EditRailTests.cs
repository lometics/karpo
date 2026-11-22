// The edit rail's plan (Karpo.Dock\EditRailPlan.cs), and the quick adds that left it: a band at the
// top of the add list (2026-10-07), then each a button of the display's edit bar (2026-10-08), the
// band gone again; the buttons went too (§310), the folder and the separator picks of the add list.
//
// The person: the sections' "+" and their grid, list and separator buttons go; "any remaining
// per-section strip [is] one row or one column, never 2×2". So a section's edit bar holds what the
// section has no room to show, and nothing else, on one line along the bar — and a section that
// shows everything has none. The plan is pure, so these are ordinary tests: no window, no
// compositor, no text to measure.

using System.Numerics;
using System.Reflection;
using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class EditRailTests
{
    /// <summary>A section showing everything has no edit bar: no button, no panel.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WithNothingHiddenThereIsNoEditBar(bool vertical)
    {
        var plan = EditRailPlan.For(1.5f, vertical, notShown: false);

        Assert.Empty(plan.Buttons);
        Assert.Equal(Vector2.Zero, plan.Panel);
        Assert.Empty(EditRailPlan.Parts());
    }

    /// <summary>
    /// With a widget hidden the edit bar holds the one button that names it — no "+", no folders, no
    /// separator: those are the add button's and its list's now.
    /// </summary>
    [Fact]
    public void TheEditBarHoldsOnlyWhatTheSectionCannotShow()
    {
        var parts = EditRailPlan.Parts(notShown: true);

        var only = Assert.Single(parts);
        Assert.Equal(EditRailAction.NotShown, only.Action);
        Assert.False(string.IsNullOrWhiteSpace(only.Title));
        Assert.DoesNotContain(parts, button => button.Action == EditRailAction.Close);
    }

    /// <summary>
    /// One line along the bar, never a square: a row beside a horizontal bar, a column beside a
    /// vertical one, every button inside the panel and found where it is drawn.
    /// </summary>
    [Theory]
    [InlineData(false, 1f)]
    [InlineData(false, 1.5f)]
    [InlineData(true, 1f)]
    [InlineData(true, 2f)]
    public void TheEditBarIsOneLineAlongTheBar(bool vertical, float scale)
    {
        var plan = EditRailPlan.For(scale, vertical, notShown: true);
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var thick = Px(EditRailPlan.MarginDip) * 2 + Px(EditRailPlan.ButtonDip);

        Assert.Equal(thick, vertical ? plan.Panel.X : plan.Panel.Y);
        Assert.All(plan.Buttons, button => Assert.Equal(Px(EditRailPlan.MarginDip), vertical ? button.Rect.X : button.Rect.Y));
        for (var index = 0; index < plan.Buttons.Count; index++)
        {
            var rect = plan.Buttons[index].Rect;
            Assert.True(rect.X >= 0 && rect.Y >= 0 && rect.Right <= plan.Panel.X && rect.Bottom <= plan.Panel.Y);
            Assert.Equal(index, plan.At(rect.Center));
        }
        Assert.Equal(-1, plan.At(new Vector2(1, 1)));
    }

    /// <summary>Two calls with the same arguments give the same plan.</summary>
    [Fact]
    public void ThePlanIsAFunctionOfItsArguments()
    {
        var a = EditRailPlan.For(1.25f, vertical: true, notShown: true);
        var b = EditRailPlan.For(1.25f, vertical: true, notShown: true);

        Assert.Equal(a.Panel, b.Panel);
        Assert.Equal(a.Buttons, b.Buttons);
    }

    // --- The folder ----------------------------------------------------------------

    /// <summary>
    /// The add list's highlighted band is gone (2026-10-08), and so are the edit bar's own "Folder" and
    /// "Separator" buttons that followed it (the person, 2026-10-23: "folder and separator should go
    /// away from the edit bar"; §310): no flyout item is a quick add, there are no band rows or
    /// commands, and the bar's list opens on its first group's heading. Nothing is lost: the bar's add
    /// list offers a folder as a pick of its own, in Popular, a kind the bar takes — which the search finds
    /// as any other (§511: the separator is gone from the bar).
    /// </summary>
    [Fact]
    public void TheAddListOffersTheFolder()
    {
        Assert.Null(typeof(FlyoutItem).GetProperty("Quick"));
        Assert.Null(typeof(AddList).GetMethod("QuickRows"));
        Assert.Null(typeof(AddList).GetField("Quick"));
        Assert.Null(typeof(AddList).GetMethod("AddQuick"));
        Assert.Null(typeof(TheDock).GetField("CMD_ADD_QUICK", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
        var (rows, entries, _) = AddList.Rows(WidgetType.Bar, turns: false, TheDock.CMD_ADD_WIDGET);

        Assert.True(rows[0].Header);
        foreach (var kind in new[] { BarKind.FolderList })
        {
            var entry = Assert.Single(entries, entry => entry.Bar == kind);
            Assert.True(Sections.CanAdd([], kind), $"{kind} is offered but cannot be added");
            var row = Assert.Single(AddList.Picks(rows), item => item.Id == TheDock.CMD_ADD_WIDGET + (uint)entries.IndexOf(entry));
            Assert.True(row.Enabled);
            Assert.Contains(AddList.Results(rows, entry.Title), item => item.Id == row.Id);
        }
        Assert.Empty(AddList.Results(rows, "zzzz-nothing"));
    }
}
