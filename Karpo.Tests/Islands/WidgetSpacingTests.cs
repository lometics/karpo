// §533 (the person, 2026-11-07: "you should be able to choose the spacing of the widgets on the taskbar … the automatic
// switch to no space when the taskbar is small should not happen anymore") and §589 (2026-11-09: "spacing should be a pixel
// value that i can change not just the three states"): a layout's Spacing, a number of DIPs (0 to 24, 6 where nobody
// chose), is the room between its bars' widgets at every bar size, the gap that scales with the icon as it always did; 0
// alone makes an islands bar flush (its cards edge to edge, square), at any height, and a thin bar spaced more keeps its
// room and its corners; two sections never stand closer than two widgets. Held here without drawing: the gaps, the flush
// look, the file, the pointer's and a drop's places in the gap, and the Taskbar page's number box.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class WidgetSpacingTests
{
    private static RECT Area(Edge edge) =>
        edge.IsVertical() ? new RECT { right = 1600, bottom = 3000 } : new RECT { right = 3000, bottom = 1600 };

    private static PartSpec Widget(string id, BarKind kind = BarKind.DoNotDisturb, BarSection section = BarSection.Start) =>
        new(Config.IslandKey(null, id), kind) { Section = section };

    private static readonly PartSpec[] Three = [Widget("a"), Widget("b", BarKind.NightLight), Widget("c", BarKind.EnergySaver)];

    /// <summary>A config whose one layout is spaced <paramref name="spacing"/> DIPs, its bar <paramref name="height"/> DIP thick.</summary>
    internal static Config Spaced(int spacing, int height = 48, BarStyle style = BarStyle.Islands) =>
        new() { Height = height, Style = style, Layouts = [new TaskbarLayout { Spacing = spacing }] };

    private static Layout Bar(int spacing, int height = 48, BarStyle style = BarStyle.Islands, Edge edge = Edge.Bottom, uint dpi = 96, bool editing = false, IReadOnlyList<PartSpec>? specs = null)
    {
        var config = Spaced(spacing, height, style);
        return Layout.ForTests(dpi, Area(edge), edge, config, specs ?? Three, Layout.CellPx(dpi, config), editing);
    }

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float Reach(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    private static float Gap(Layout layout, int before, int after) =>
        Along(layout, layout.BlockRect(after)) - Reach(layout, layout.BlockRect(before));

    /// <summary>
    /// The gap of a spacing at a small, the default and a large bar, in px at 100 and 150 %: it scales with the icon (16 DIP
    /// on a 28- or 32-DIP bar, 24 from 48 up), never under a third of itself, and never goes to nothing by itself — 0 is the
    /// only 0. The room between two sections is at least 14 DIP, more than the gap of a spacing under it, and §589 never less
    /// than a widget's gap where the spacing is wider.
    /// </summary>
    [Theory]
    [InlineData(0, 28, 96u, 0f)]
    [InlineData(0, 32, 96u, 0f)]
    [InlineData(0, 48, 96u, 0f)]
    [InlineData(0, 80, 144u, 0f)]
    [InlineData(3, 48, 96u, 3f)]
    [InlineData(6, 28, 96u, 4f)]
    [InlineData(6, 32, 96u, 4f)]
    [InlineData(6, 32, 144u, 6f)]
    [InlineData(6, 48, 96u, 6f)]
    [InlineData(6, 48, 144u, 9f)]
    [InlineData(6, 80, 96u, 6f)]
    [InlineData(12, 28, 96u, 8f)]
    [InlineData(12, 32, 96u, 8f)]
    [InlineData(12, 32, 144u, 12f)]
    [InlineData(12, 48, 96u, 12f)]
    [InlineData(12, 48, 144u, 18f)]
    [InlineData(12, 80, 96u, 12f)]
    [InlineData(20, 48, 96u, 20f)]
    [InlineData(24, 48, 144u, 36f)]
    public void EachSpacingsGapAtEveryBarSize(int spacing, int height, uint dpi, float gap)
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Left })
        {
            var layout = Bar(spacing, height, edge: edge, dpi: dpi);

            Assert.Equal(spacing, layout.Spacing);
            Assert.Equal(gap, layout.WidgetGap);
            Assert.Equal(gap, Gap(layout, 0, 1));
            Assert.Equal(gap, Gap(layout, 1, 2));
            var where = $"{spacing} at {height} DIP: sections {layout.SectionGap} px, widgets {layout.WidgetGap}";
            Assert.True(spacing < 14 ? layout.SectionGap > layout.WidgetGap : layout.SectionGap == layout.WidgetGap, where);
        }
    }

    /// <summary>A dock's panel holds its widgets the spacing's gap apart too; 0 never makes it flush, its panel is what stands apart.</summary>
    [Theory]
    [InlineData(0, 0f)]
    [InlineData(6, 6f)]
    [InlineData(12, 12f)]
    public void ADocksWidgetsStandTheSpacingsGapApart(int spacing, float gap)
    {
        var layout = Bar(spacing, style: BarStyle.Floating);

        Assert.Equal(gap, Gap(layout, 0, 1));
        Assert.False(layout.Flush);
        Assert.True(layout.Lift > 0f && layout.EndRoom > 0f, "the dock floats off the screen's edge and ends as ever");
    }

    /// <summary>
    /// §533: the thin bar's own switch is gone — a 28-, 32- or 35-DIP islands bar spaced 1, 6 or 24 keeps its cards' room off
    /// the bar, its ends and its corners, as every taller bar; only 0 is flush, and at every height.
    /// </summary>
    [Theory]
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(Config.ThinHeight - 1)]
    [InlineData(48)]
    [InlineData(80)]
    public void OnlyZeroIsFlushAtAnyHeight(int height)
    {
        foreach (var dpi in new uint[] { 96, 144, 192 })
        {
            foreach (var spacing in new[] { 1, Config.DefaultSpacing, Config.MostSpacing })
            {
                var spaced = Bar(spacing, height, dpi: dpi);
                var where = $"{spacing} at {height} DIP, {dpi} DPI";
                Assert.False(spaced.Flush, where);
                Assert.True(spaced.CardRadius > 0f, $"{where}: a corner");
                Assert.True(spaced.EndRoom > 0f, $"{where}: room at the ends");
                Assert.True(Layout.CardInsetPx(dpi, Spaced(spacing, height)) > 0f, $"{where}: room off the bar");
                Assert.True(spaced.Corner(6f) > 0f, where);
            }
            foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
            {
                var flush = Bar(0, height, edge: edge, dpi: dpi);
                var where = $"0 at {height} DIP on the {edge} edge, {dpi} DPI";
                Assert.True(flush.Flush, where);
                Assert.True(flush.Cards, $"{where}: the cards stay");
                Assert.Equal(0f, flush.CardRadius);
                Assert.Equal(0f, flush.EndRoom);
                Assert.Equal(0f, flush.WidgetGap);
                Assert.Equal(0f, Layout.CardInsetPx(dpi, Spaced(0, height)));
                Assert.Equal(0f, flush.Corner(12f));
                Assert.Equal(0f, flush.SearchBoxRadius);
                Assert.True(flush.SectionGap > 0f, $"{where}: sections still stand apart");
                Assert.True(flush.StyleFor(0, new Look(Glass: false, Dark: true, Accent: default)).Square, where);
                var panel = flush.PanelRect;
                var vertical = edge.IsVertical();
                foreach (var block in Enumerable.Range(0, flush.Parts.Count).Select(flush.BlockRect).Where(block => !block.IsEmpty))
                {
                    var (near, far) = vertical ? (block.X - panel.X, panel.Right - block.Right) : (block.Y - panel.Y, panel.Bottom - block.Bottom);
                    Assert.True(MathF.Abs(near) <= 0.5f && MathF.Abs(far) <= 0.5f, $"{where}: a card {near} px and {far} px off the bar's sides");
                }
                var first = flush.BlockRect(0);
                Assert.True(MathF.Abs(vertical ? first.Y - panel.Y : first.X - panel.X) <= 0.5f, $"{where}: the first card at the bar's start");
                Assert.Equal(Reach(flush, flush.BlockRect(0)), Along(flush, flush.BlockRect(1)));
            }
        }
    }

    /// <summary>The thin bar is still thin — its text, cell and grip follow the room — whatever its spacing: the two questions stay apart.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(24)]
    public void TheRoomAndTheSpacingAreTwoQuestions(int spacing)
    {
        Assert.True(Bar(spacing, 32).Thin);
        Assert.False(Bar(spacing, 48).Thin);
    }

    /// <summary>
    /// §160 in every spacing: the pointer is each neighbour's up to the gap's middle — on 0's seam the one side or the other,
    /// no px of nobody's —; a drop's slot turns at the widgets' middles and the "Add" line stands in the gap's middle.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(21)]
    public void ThePointerAndADropFollowTheGap(int spacing)
    {
        var layout = Bar(spacing);
        var y = layout.BlockRect(0).Center.Y;
        var end = layout.BlockRect(0).Right;
        var start = layout.BlockRect(1).X;
        var middle = (end + start) / 2f;
        Assert.Equal(start - end, layout.WidgetGap);

        Assert.Equal(0, layout.HitTest(middle - 0.5f, y)?.Part);
        Assert.Equal(1, layout.HitTest(middle + 0.5f, y)?.Part);
        Assert.Equal(0, layout.HitTest(end - 0.5f, y)?.Part);
        Assert.Equal(1, layout.HitTest(start, y)?.Part);

        // A drop carried from the end section: before the second widget past the first's middle, after it past its own.
        Assert.Equal(new BarSlot(BarSection.Start, 1), layout.DropSlot(layout.BlockRect(0).Center.X + 1f, -1));
        Assert.Equal(new BarSlot(BarSection.Start, 2), layout.DropSlot(layout.BlockRect(1).Center.X + 1f, -1));

        var editing = Bar(spacing, editing: true);
        var seam = editing.InsertionRect(new BarSlot(BarSection.Start, 1));
        var join = (editing.BlockRect(0).Right + editing.BlockRect(1).X) / 2f;
        Assert.True(MathF.Abs(seam.X + (seam.Width / 2f) - join) <= 1f, $"{spacing}: the Add line in the gap's middle");
        var after = editing.InsertionRect(new BarSlot(BarSection.Start, 3));
        Assert.True(MathF.Abs(after.X + (after.Width / 2f) - (editing.BlockRect(2).Right + (editing.WidgetGap / 2f))) <= 1f, $"{spacing}: past the last where its gap will be");
    }

    /// <summary>Entering edit mode moves nothing in any spacing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    [InlineData(24)]
    public void EnteringEditModeMovesNothing(int spacing)
    {
        var rest = Bar(spacing);
        var editing = Bar(spacing, editing: true);
        for (var part = 0; part < Three.Length; part++)
        {
            Assert.Equal(rest.BlockRect(part), editing.BlockRect(part));
        }
    }

    /// <summary>The app buttons of one part stand the spacing's gap apart, as two widgets do (§515's TileGap).</summary>
    [Theory]
    [InlineData(0, 0f)]
    [InlineData(9, 9f)]
    [InlineData(12, 12f)]
    public void AppButtonsStandTheSpacingsGapApart(int spacing, float gap)
    {
        var config = Spaced(spacing);
        var layout = Layout.ForTests(96, Area(Edge.Bottom), Edge.Bottom, config, [Widget("apps", BarKind.Apps)], Layout.CellPx(96, config), tiles: 3);
        var first = layout.TileCell(0);
        var second = layout.TileCell(1);
        Assert.Equal(gap, second.X - first.Right);
    }

    // --- The setting ------------------------------------------------------------------

    /// <summary>A layout nobody spaced is 6 DIP (§515's gap), and so is a config with no layout at all, or a file that says none; it is always written.</summary>
    [Fact]
    public void AbsentIsSix()
    {
        var config = new Config();
        _ = config.Repair();
        Assert.Equal(6, Config.DefaultSpacing);
        Assert.Equal(Config.DefaultSpacing, config.DefaultLayout.Spacing);
        Assert.Equal(Config.DefaultSpacing, new Config().BarSpacing);
        Assert.Empty(new Config().Layouts);
        var json = config.ToJson();
        Assert.Contains("\"spacing\": 6,", json, StringComparison.Ordinal);
        Assert.Equal(6f, Layout.ForTests(96, Area(Edge.Bottom), Edge.Bottom, new Config(), Three, Layout.CellPx(96, new Config())).WidgetGap);

        var none = json.Replace("\"spacing\": 6,", "", StringComparison.Ordinal);
        Assert.DoesNotContain("\"spacing\"", none, StringComparison.Ordinal);
        Assert.Equal(Config.DefaultSpacing, Configs.Repaired(none).DefaultLayout.Spacing);
    }

    /// <summary>
    /// Each spacing is written as its number of DIPs, 0 and the default too, and read back as it was; a bar laid out along an
    /// edge wears its layout's; a copy its source's.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(13)]
    [InlineData(24)]
    public void ChosenItIsWrittenAndReadBack(int spacing)
    {
        var config = Configs.With(BarKind.Start.New());
        config.DefaultLayout.Spacing = spacing;
        var json = config.ToJson();

        Assert.Contains($"\"spacing\": {spacing},", json, StringComparison.Ordinal);
        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(spacing, again.DefaultLayout.Spacing);
        Assert.Equal(json, again.ToJson());
        var bar = again.WithEdge(again.DefaultLayout.Edges(Edge.Bottom)[Edge.Bottom]);
        Assert.Equal(spacing, bar.BarSpacing);
        Assert.Equal(spacing, again.DefaultLayout.CopyAs("copy").Spacing);
    }

    /// <summary>A number typed into the file past what is offered is held to it, 0 to 24, as a size is.</summary>
    [Theory]
    [InlineData(-4, 0)]
    [InlineData(25, 24)]
    [InlineData(400, 24)]
    public void ANumberPastTheRangeIsHeldToIt(int typed, int held)
    {
        var known = Configs.With(BarKind.Start.New()).ToJson();
        var json = known.Replace("\"spacing\": 6,", $"\"spacing\": {typed},", StringComparison.Ordinal);
        Assert.Contains($"\"spacing\": {typed},", json, StringComparison.Ordinal);

        Assert.Equal(held, Configs.Repaired(json).DefaultLayout.Spacing);
        Assert.Equal(held, new Config { Layouts = [new TaskbarLayout { Spacing = typed }] }.BarSpacing);
    }

    /// <summary>
    /// §589: the Taskbar page's Spacing, after Size: a number of pixels from 0 to 24, a pixel a step, the layout's in force; a
    /// value set writes the layout shown, held to the range.
    /// </summary>
    [Fact]
    public void TheTaskbarPageOffersANumberOfPixels()
    {
        var config = new Config();
        _ = config.Repair();
        var bar = new BarSpot("D1", "One");
        OptionRow Row() => BarLooks.TaskbarRows(config, bar).Single(row => row.Key == BarLooks.SpacingRow);
        var keys = BarLooks.TaskbarRows(config, bar).Select(row => row.Key).ToList();
        Assert.Equal(keys.IndexOf(BarLooks.SizeRow) + 1, keys.IndexOf(BarLooks.SpacingRow));
        var spacing = Row();
        Assert.Equal((OptionRowKind.Number, S.SettingsLayoutsSpacingHeader, OptionUnit.Pixels), (spacing.Kind, spacing.Title, spacing.Unit));
        Assert.Equal((6, 0, 24, 1), (spacing.Value, spacing.Least, spacing.Most, spacing.Step));
        Assert.Equal(S.SettingsLayoutsSizeValue(6), spacing.Shown);

        Assert.True(spacing.Set!(7));
        Assert.Equal(7, config.ActiveLayout(bar.Display).Spacing);
        Assert.False(Row().Set!(7), "already 7");
        Assert.True(Row().Set!(0));
        Assert.Equal(0, config.ActiveLayout(bar.Display).Spacing);
        Assert.True(Row().Set!(99));
        Assert.Equal(24, config.ActiveLayout(bar.Display).Spacing);
    }

    /// <summary>§589: on the settings panel it is a number box — − its value + —, a new layout's 6 px in it, a number typed too.</summary>
    [Fact]
    public void OnTheSettingsPanelItIsANumberBox()
    {
        var taskbar = SettingsPanelTests.Content().Of(PanelPage.Taskbar)!;
        var row = PanelRows.All(taskbar.Rows).Single(row => row.Item.Text == S.SettingsLayoutsSpacingHeader);

        Assert.Equal(PanelRowKind.Stepper, row.Kind);
        Assert.NotNull(row.Field);
        var stepper = Assert.IsType<FlyoutControl.Stepper>(row.Item.Control);
        Assert.Equal(S.SettingsLayoutsSizeValue(Config.DefaultSpacing), stepper.Value);
        Assert.True(stepper.CanFewer && stepper.CanMore);
    }
}
