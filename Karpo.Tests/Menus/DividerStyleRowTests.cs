// menus: a folder's divider's Style (docs\widgets.md §435, §505): its fixed room — empty space — or Grow (the bar's
// separator never grew, the person, 2026-10-30: "the taskbar should not get a spacer that can grow"; §426 withdrawn;
// and is gone since §511). It has no look, so nothing else: no inherited look, no way to Layout ▾ (§505; §478 to §502
// the look was the layout's style's).

namespace Karpo.Tests.Menus;

public sealed class DividerStyleRowTests
{
    private static OptionRow Row(WidgetItem item, Config config) =>
        OptionRows.Of(item, place: new OptionPlace(config, Dark: true)).Single(row => row.Key == OptionRows.DividerStyle);

    /// <summary>Its row is Fixed, then Grow; each with its own icon; Fixed while nothing is chosen.</summary>
    [Fact]
    public void ItsStyleIsFixedOrGrow()
    {
        var row = Row(ListKind.Divider.New(), new Config());

        Assert.Equal("Style", row.Title);
        Assert.Equal(["Fixed", "Grow"], row.Choices);
        Assert.Equal(0, row.Chosen);
        Assert.Equal(2, row.ChoiceGlyphs.Distinct().Count());
    }

    /// <summary>Grow is written as the file keeps it; Fixed takes it away again.</summary>
    [Fact]
    public void GrowIsWrittenAndFixedTakesItAway()
    {
        var divider = ListKind.Divider.New();
        var config = new Config();

        Assert.True(Row(divider, config).Set!(1));
        Assert.Equal(DividerStyle.Grow, divider.Divider?.Style);
        Assert.Equal(1, Row(divider, config).Chosen);
        Assert.False(Row(divider, config).Set!(1));
        Assert.True(Row(divider, config).Set!(0));
        Assert.Null(divider.Divider);
    }

    /// <summary>
    /// §505: the row is the same whatever palette the Appearance wears — a divider has no look a palette could give it —
    /// and it is the divider's whole menu, in every layout.
    /// </summary>
    [Fact]
    public void ItsStyleIsItsWholeMenuInEveryLayout()
    {
        var config = new Config();
        var work = config.DefaultLayout;
        var home = BarLooks.Add(config, "Home");
        IReadOnlyList<OptionRow> Menu(TaskbarLayout layout) =>
            OptionRows.Of(ListKind.Divider.New(), place: new OptionPlace(config, Dark: true) { Layout = layout });

        foreach (var palette in new[] { LookPalettes.PaperId, LookPalettes.HarborId })
        {
            Assert.True(LookPalettes.Wear(config, LookPalettes.Of(palette)!));
            foreach (var layout in new[] { work, home })
            {
                var row = Assert.Single(Menu(layout));
                Assert.Equal(OptionRows.DividerStyle, row.Key);
                Assert.Equal(["Fixed", "Grow"], row.Choices);
            }
        }
    }
}
