// Two Start buttons on one bar (the person, 2026-10-07: "a second start menu button on the same
// taskbar does not show"): the layout gave both their room and the pointer found both, but the
// scene drew Windows' logo for the first of the kind alone, so the second stood empty. Every
// Start, search, Task view and Show desktop the bar holds is drawn now (Scene), each pressed as
// its own; Start opens on the first one's display as before (StartPlacement.First).
//
// The scene's half needs a compositor: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private static List<PartSpec> TwoOfEach() =>
    [
        Spec("start", BarKind.Start) with { Section = BarSection.Middle },
        Spec("search", BarKind.Search) with { Section = BarSection.Middle },
        Spec("taskview", BarKind.TaskView) with { Section = BarSection.Middle },
        Spec("start2", BarKind.Start) with { Section = BarSection.End },
        Spec("search2", BarKind.Search) with { Section = BarSection.End },
        Spec("taskview2", BarKind.TaskView) with { Section = BarSection.End },
    ];

    /// <summary>Each Start has its own room and its own hit, and an untagged Start is still the first's.</summary>
    [Fact]
    public void EachOfTwoStartButtonsIsItsOwnButton()
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
        var layout = Layout.ForTests(96, area, Edge.Bottom, config, TwoOfEach(), Layout.CellPx(96, config));
        var first = layout.ItemRect(Hit.Start.In(0));
        var second = layout.ItemRect(Hit.Start.In(3));
        Assert.False(first.IsEmpty);
        Assert.False(second.IsEmpty);
        Assert.True(second.X > first.Right);
        Assert.Equal(0, layout.PartOf(Hit.Start));
        Assert.Equal(Hit.Start.In(3), layout.HitTest(second.Center.X, second.Center.Y, false));
    }

    /// <summary>The scene draws a logo for each Start, and a glyph for each search and Task view.</summary>
    [UiFact]
    public void TheSceneDrawsEveryStartButton()
    {
        OnABar(48, TwoOfEach(), (layout, scene) =>
        {
            Assert.NotNull(scene.Registered("start"));
            Assert.NotNull(scene.Registered($"start:{layout.Specs[3].Key}"));
            Assert.NotNull(scene.Registered("search"));
            Assert.NotNull(scene.Registered($"search:{layout.Specs[4].Key}"));
            Assert.NotNull(scene.Registered("taskview"));
            Assert.NotNull(scene.Registered($"taskview:{layout.Specs[5].Key}"));
        }, editing: false);
    }
}
