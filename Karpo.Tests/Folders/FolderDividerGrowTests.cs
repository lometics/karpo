// A folder's divider that grows (the person, 2026-10-30: "spacers should also have a style grow, in which case they grow
// and take up the available space", then "the spacer should only be available in the folder"; docs\widgets.md §435):
// a divider's own Style, Fixed (its room, §505) or Grow, the file's "divider": { "style": "grow" } on its line.
// A growing divider is its gap made as tall as the room its column has free: the lines after it go to the
// column's foot, and it parts the column into two sections. Then (the person, 2026-11-02: "it only grows to the highest
// widget in the folder and not all the way to the top or bottom"; §482) its column's room is all the room the folder
// has, the room a Fill line fills to (§173), not the room beside the tallest column. Held here (the plan, pure):
//
//  - one grows its column to the folder's whole room, alone, the tallest or beside a taller; two share it evenly;
//  - with a Fill line in its column the two share that room, evenly (FillShares);
//  - the other columns keep their heights, lined up on the bar's side; on every bar, the room a placement gives;
//  - it parts its column into two sections; arranged nothing moves, its grab is its room;
//  - the file.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed class FolderDividerGrowTests
{
    private static readonly Look Dark = new(Glass: false, Dark: true, Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4));

    private enum L
    {
        /// <summary>A line of one row, 40 px at 100 %.</summary>
        Row,

        /// <summary>A calendar set to Fill (§168), six rows at its content.</summary>
        Fill,

        /// <summary>A divider as the folders' look has it.</summary>
        Divider,

        /// <summary>A divider that grows.</summary>
        Grow,
    }

    private static CellItem Line(L kind)
    {
        switch (kind)
        {
            case L.Fill:
                var fill = ListKind.Calendar.New();
                fill.Height = LineHeight.For(fill).As(LineHeightMode.Fill);
                return fill;
            case L.Divider:
                return ListKind.Divider.New();
            case L.Grow:
                var grow = ListKind.Divider.New();
                grow.Divider = new DividerOptions { Style = DividerStyle.Grow };
                return grow;
            default:
                return ListKind.EnergySaver.New();
        }
    }

    private static FolderOptions Lists(params L[][] lists)
    {
        var options = new FolderOptions { Lists = [] };
        for (var list = 0; list < lists.Length; list++)
        {
            options.Lists.Add([]);
            foreach (var kind in lists[list])
            {
                options.Put(Line(kind), list);
            }
        }
        return options;
    }

    private static L[] Rows(int count) => [.. Enumerable.Repeat(L.Row, count)];

    private static FolderPlan Plan(FolderOptions options, float scale = 1f, bool editing = false, float work = 1040) =>
        FolderFlyout.Build(Dark, scale, FolderView.List, options, options.Cells, work, editing: editing);

    private static float Tall(FolderPlan plan, int index) => plan.Cards[index].bottom - plan.Cards[index].top;

    private static List<int> Of(FolderPlan plan, int list) => [.. Enumerable.Range(0, plan.Cards.Count).Where(index => plan.CardList[index] == list)];

    /// <summary>A divider's room at <paramref name="scale"/>: what a growing divider is before it grows.</summary>
    private static float Gap(float scale) => FolderFlyout.SeparatorRoom(scale);

    /// <summary>All the room the folder has at <paramref name="scale"/> on the 1040 px work area: what a Fill line fills to (§173).</summary>
    private static float Room(float scale) => FolderFlyout.Reach(1040, 0, scale);

    /// <summary>
    /// One growing divider takes all the room the folder has (§482), the room a Fill line fills to: its column reaches
    /// the top on a bottom bar, the line after it at the column's foot, the divider all the room between. The taller
    /// column beside it keeps its height, standing on the bar as the shorter one did (§303).
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void OneGrowingDividerTakesTheFolderRoom(float scale)
    {
        var plan = Plan(Lists(Rows(10), [L.Row, L.Grow, L.Row]), scale);
        var lines = Of(plan, 1);
        var row = Tall(plan, Of(plan, 0)[0]);

        Assert.Equal(Room(scale), plan.Lists[1].Height, 1);
        Assert.Equal(Room(scale), plan.Height, 1);
        Assert.Equal(10 * row, plan.Lists[0].Height, 1);
        Assert.Equal(plan.Lists[1].Box.bottom, plan.Lists[0].Box.bottom, 1);
        Assert.Equal(plan.Lists[1].Box.bottom, plan.Cards[lines[2]].bottom, 1);
        Assert.Equal(plan.Lists[1].Height - (2 * row), Tall(plan, lines[1]), 1);
        Assert.Equal(plan.Cards[lines[0]].bottom, plan.Cards[lines[1]].top, 1);
        Assert.Equal(plan.Cards[lines[1]].bottom, plan.Cards[lines[2]].top, 1);
    }

    /// <summary>
    /// With no taller column it grows all the same (§482): the only column, and the tallest of two, reach the folder's
    /// room; the shorter column beside the tallest keeps its height.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void ItGrowsWithNoTallerColumn(float scale)
    {
        var alone = Plan(Lists([L.Row, L.Grow, L.Row]), scale);
        Assert.Equal(Room(scale), alone.Lists[0].Height, 1);
        Assert.Equal(alone.Lists[0].Box.bottom, alone.Cards[2].bottom, 1);
        Assert.Equal(Room(scale) - (2 * Tall(alone, 0)), Tall(alone, 1), 1);

        var tallest = Plan(Lists([L.Row, L.Grow, L.Row, L.Row, L.Row], Rows(2)), scale);
        var row = Tall(tallest, Of(tallest, 1)[0]);
        Assert.Equal(Room(scale), tallest.Lists[0].Height, 1);
        Assert.Equal(2 * row, tallest.Lists[1].Height, 1);
        Assert.Equal(Room(scale) - (4 * row), Tall(tallest, Of(tallest, 0)[1]), 1);
    }

    /// <summary>Two growing dividers in one column share the folder's room evenly, the odd px to the first.</summary>
    [Fact]
    public void TwoShareIt()
    {
        var plan = Plan(Lists(Rows(10), [L.Row, L.Grow, L.Row, L.Grow, L.Row]));
        var lines = Of(plan, 1);

        Assert.Equal(Room(1f), plan.Lists[1].Height, 1);
        Assert.Equal(Room(1f) - (3 * 40), Tall(plan, lines[1]) + Tall(plan, lines[3]), 1);
        Assert.InRange(Tall(plan, lines[1]) - Tall(plan, lines[3]), 0, 1);
        Assert.Equal(Room(1f), plan.Cards[lines[4]].bottom - plan.Lists[1].Box.top, 1);
    }

    /// <summary>
    /// On every bar a growing divider's column reaches the room the placement gives, as a Fill line's does (§173): the
    /// other column keeps its height, lined up on the bar's side — the bottoms on a bottom bar, the tops on a top bar,
    /// the middles beside a side bar (§303).
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void OnEveryBarItReachesTheRoomGiven(Edge edge)
    {
        var options = Lists([L.Row, L.Grow, L.Row], Rows(3));
        var plan = FolderFlyout.Build(Dark, 1f, FolderView.List, options, options.Cells, 1040, edge: edge, room: 700);

        Assert.Equal(700, plan.Height, 1);
        Assert.Equal(700, plan.Lists[0].Height, 1);
        Assert.Equal(700, plan.Cards[2].bottom - plan.Cards[0].top, 1);
        Assert.Equal(3 * 40, plan.Lists[1].Height, 1);
        var left = 700 - plan.Lists[1].Height;
        Assert.Equal(edge switch { Edge.Top => 0, Edge.Left or Edge.Right => MathF.Round(left / 2), _ => left }, plan.Lists[1].Box.top, 1);
    }

    /// <summary>
    /// A Fill line and a growing divider in one column share the room Fill fills to evenly: the column the folder's
    /// whole room (§173), each as much again past its own height.
    /// </summary>
    [Fact]
    public void AFillLineAndAGrowingDividerShareTheRoomEvenly()
    {
        var plan = Plan(Lists(Rows(4), [L.Fill, L.Grow, L.Row]));
        var lines = Of(plan, 1);
        var calendar = FolderOptions.CellDip * 12;

        Assert.Equal(1040 - 24, plan.Lists[1].Height, 1);
        var fillGrew = Tall(plan, lines[0]) - calendar;
        var dividerGrew = Tall(plan, lines[1]) - Gap(1f);
        Assert.True(fillGrew > 100, $"the Fill line grew {fillGrew}");
        // Evenly, the odd px to the first.
        Assert.InRange(fillGrew - dividerGrew, 0, 1);
        Assert.Equal(plan.Lists[1].Box.bottom, plan.Cards[lines[2]].bottom, 1);
    }

    /// <summary>A fixed divider grows nothing: its column stays as tall as its lines.</summary>
    [Fact]
    public void AFixedDividerDoesNotGrow()
    {
        var plan = Plan(Lists(Rows(10), [L.Row, L.Divider, L.Row]));

        Assert.Equal(80 + Gap(1f), plan.Lists[1].Height, 1);
    }

    /// <summary>
    /// A growing divider parts its column into two sections, rounded each (§410), and draws nothing at rest. Arranged
    /// nothing moves; its grab is its whole room.
    /// </summary>
    [Fact]
    public void ItPartsTheColumnIntoTwoSections()
    {
        var options = Lists(Rows(10), [L.Row, L.Grow, L.Row]);
        var rest = Plan(options);
        var editing = Plan(options, editing: true);
        var lines = Of(rest, 1);

        var runs = rest.Lists[1].Runs;
        Assert.Equal(2, runs.Count);
        Assert.Equal(rest.Cards[lines[0]], runs[0]);
        Assert.Equal(rest.Cards[lines[2]], runs[1]);
        Assert.Equal(-1, rest.CardRun[lines[1]]);
        Assert.True(rest.Bands[lines[1]].IsEmpty);
        Assert.Equal(rest.Cards, editing.Cards);
        Assert.Equal(editing.Cards[lines[1]], editing.Grab(lines[1]));
        Assert.False(editing.Bands[lines[1]].IsEmpty);
    }

    /// <summary>
    /// §487, the person (2026-11-02): "when changing the separator style from grow to only a space or vice versa it does not
    /// immediately update which it should". A divider's Style picked in its menu while its folder stands open — Fixed
    /// to Grow and back, at rest and arranged — lays the open folder out again at once, as the dock hands it
    /// the change (a copy of the config, the folder configured again): its column reaches the folder's room, its two
    /// sections that far apart, the panel as tall as that; back, the column as tall as its lines, its sections a divider's
    /// room apart, the panel too.
    /// </summary>
    [UiTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItsStylePickedWhileTheFolderIsOpenLaysItOutAtOnce(bool editing)
    {
        WindowShots.OnHiddenDesktop("folder-divider-restyle", (gfx, instance) =>
        {
            const string Owner = "layout:default";
            var work = new TerraFX.Interop.Windows.RECT { right = 1920, bottom = 1040 };
            var anchor = new TerraFX.Interop.Windows.POINT { x = 960, y = 1040 };
            var config = new Config();
            var holder = BarKind.FolderList.New();
            holder.Id = "box";
            holder.Folder = Lists(Rows(4), [L.Row, L.Divider, L.Row]);
            config.DefaultLayout.Islands.Add(holder);
            using var folder = new FolderIsland($"{Owner}/box", holder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, Dark, 1f, anchor, work, Edge.Bottom, folder, editing: editing);
            WindowShots.Settle();
            var plain = flyout.Plan!;
            var plainPanel = flyout.PanelOnScreen;
            // Fixed: two sections a divider's room apart, the column as tall as its lines.
            Assert.Equal(2, plain.Lists[1].Runs.Count);
            Assert.Equal(Gap(1f), plain.Lists[1].Runs[1].top - plain.Lists[1].Runs[0].bottom, 1);
            Assert.Equal(80 + Gap(1f), plain.Lists[1].Height, 1);
            Assert.Equal(4 * 40, plain.Lists[0].Height, 1);

            // The menu's way: its row set on a copy of the config, the folder configured from that copy (Dock.OptionPicked).
            void Pick(int style)
            {
                config = config.Clone();
                var box = (BarItem)config.DefaultLayout.Islands.First(island => island.Id == "box");
                var divider = box.Folder!.Cells.First(cell => FolderFlyout.Divides(cell)).Island;
                Assert.True(OptionRows.Of(divider, place: new OptionPlace(config, Dark: true)).Single(row => row.Key == OptionRows.DividerStyle).Set!(style));
                folder.Configure(box, config);
                WindowShots.Settle();
            }

            Pick(1);
            var grown = flyout.Plan!;
            Assert.Equal(FolderFlyout.Reach(1040, 0, 1f), grown.Lists[1].Height, 1);
            Assert.Equal(2, grown.Lists[1].Runs.Count);
            Assert.Equal(grown.Lists[1].Height - 80, grown.Lists[1].Runs[1].top - grown.Lists[1].Runs[0].bottom, 1);
            Assert.Equal(4 * 40, grown.Lists[0].Height, 1);
            Assert.True(flyout.PanelOnScreen.bottom - flyout.PanelOnScreen.top > plainPanel.bottom - plainPanel.top + 400,
                $"the panel stands {flyout.PanelOnScreen.bottom - flyout.PanelOnScreen.top} px tall with the divider grown, {plainPanel.bottom - plainPanel.top} before");

            Pick(0);
            Assert.Equal(plain.Lists[1].Height, flyout.Plan!.Lists[1].Height, 1);
            Assert.Equal(plain.Lists[1].Runs, flyout.Plan!.Lists[1].Runs);
            Assert.Equal(plainPanel.bottom - plainPanel.top, flyout.PanelOnScreen.bottom - flyout.PanelOnScreen.top);
            flyout.Hide();
        });
    }

    /// <summary>"divider": { "style": "grow" } on the line, read as it is; nothing written for one nobody set, nor kept by anything but a divider.</summary>
    [Fact]
    public void TheFileSaysGrowAndNothingElse()
    {
        var grow = Line(L.Grow);
        var json = System.Text.Json.JsonSerializer.Serialize(grow, ConfigJson.Default.CellItem);
        var plain = System.Text.Json.JsonSerializer.Serialize(Line(L.Divider), ConfigJson.Default.CellItem);
        var read = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJson.Default.CellItem)!;
        read.Normalize();

        using (var file = System.Text.Json.JsonDocument.Parse(json))
        {
            Assert.Equal("grow", file.RootElement.GetProperty("divider").GetProperty("style").GetString());
        }
        using (var file = System.Text.Json.JsonDocument.Parse(plain))
        {
            Assert.False(file.RootElement.TryGetProperty("divider", out _));
        }
        Assert.Equal(DividerStyle.Grow, read.Divider?.Style);

        read.Divider = new DividerOptions { Style = DividerStyle.Fixed };
        read.Normalize();
        Assert.Null(read.Divider);
        var other = Line(L.Row);
        other.Divider = new DividerOptions { Style = DividerStyle.Grow };
        other.Normalize();
        Assert.Null(other.Divider);
    }
}
