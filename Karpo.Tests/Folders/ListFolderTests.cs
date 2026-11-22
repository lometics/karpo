// A list folder: the one view that sizes itself to what it holds (docs\widgets.md §9).
//
// The person asked for it by name — "the folder if it is a list view should be able to shrink and
// grow to that of the content... the list view folder should also have a configurable width" — and
// it is what a converted panel becomes, because a panel is a column of things and a list is a
// column. It is the one folder there is since the grid went (2026-10-09).

using Karpo.Core;
using Karpo.Dock;

namespace Karpo.Tests.Folders;

public sealed class ListFolderTests
{
    private static Look Look => new(Glass: false, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    private static FolderOptions List(params ListKind[] kinds)
    {
        var options = new FolderOptions();
        for (var row = 0; row < kinds.Length; row++)
        {
            options.Put(kinds[row].New());
        }
        return options;
    }

    private static FolderPlan Plan(FolderOptions options, float scale = 1f, bool editing = false) =>
        FolderFlyout.Build(Look, scale, FolderView.List, options, options.Cells, 2000, editing: editing);

    /// <summary>
    /// A list is as wide as its preset, and medium — the width every panel of an island's content
    /// has — until somebody chooses, so a panel converted into a list folder is the width of the
    /// panel it replaces.
    /// </summary>
    [Fact]
    public void AListIsAsWideAsItsPreset()
    {
        Assert.Equal(360, FolderListWidths.MediumDip);
        Assert.Equal(FolderListWidths.MediumDip, Plan(List(ListKind.EnergySaver)).Width, 1);
        foreach (var width in FolderListWidths.All)
        {
            var options = List(ListKind.EnergySaver);
            options.Widths = [width];
            Assert.Equal(FolderListWidths.Dip(width), Plan(options).Width, 1);
            Assert.Equal(PopupPaint.Round(FolderListWidths.Dip(width) * 1.5f), Plan(options, 1.5f).Width, 1);
        }
        Assert.True(FolderListWidths.NarrowDip < FolderListWidths.MediumDip && FolderListWidths.MediumDip < FolderListWidths.WideDip);
    }

    /// <summary>
    /// A widget's height in a list is the one number it declares, and its columns mean nothing
    /// there: a list is one column and the width is the folder's. An ordinary kind asks for one
    /// row and gets the line a list always drew.
    /// </summary>
    [Fact]
    public void AWidgetsHeightIsTheRowsItDeclares()
    {
        Assert.Equal(LineHeight.Row, ListKind.EnergySaver.ListCellsOf());
        Assert.Equal(LineHeight.Row, ListKind.AirplaneMode.ListCellsOf());
        // The pieces lifted out of panels, which say what they need. The sound panel's volume row
        // is one line of controls (ListWidget.Rows).
        Assert.Equal(LineHeight.Row, ListKind.SoundVolume.ListCellsOf());
        Assert.Equal(8, ListKind.SoundInput.ListCellsOf());
        Assert.Equal(16, ListKind.SoundMixer.ListCellsOf());
        // The columns they declare mean nothing in a list, and they declare four.

        var plan = Plan(List(ListKind.EnergySaver, ListKind.SoundInput));
        var line = plan.Cards[0].bottom - plan.Cards[0].top;
        var graph = plan.Cards[1].bottom - plan.Cards[1].top;

        Assert.Equal(40, line, 1);
        Assert.Equal(4 * LineHeight.Row * FolderOptions.CellDip, graph, 1);
        Assert.True(graph > line, "a piece that asked for four rows is taller than a line");
    }

    /// <summary>
    /// The list's height is what is in it: adding a widget makes it taller by that widget's own
    /// height, and taking one away makes it shorter. This is the whole of "shrink and grow to that
    /// of the content".
    /// </summary>
    [Fact]
    public void TheListIsAsTallAsWhatIsInIt()
    {
        var one = Plan(List(ListKind.EnergySaver)).Height;
        var two = Plan(List(ListKind.EnergySaver, ListKind.AirplaneMode)).Height;
        var withPiece = Plan(List(ListKind.EnergySaver, ListKind.SoundInput)).Height;

        Assert.True(two > one, "another widget, another line");
        Assert.True(withPiece > two, "and a widget that asked for four rows adds four rows");
        // A line, exactly, edge to edge with the one over it (§410): nothing rounds the list's own arithmetic away.
        Assert.Equal(40, two - one, 1);
    }

    /// <summary>
    /// Every card is as wide as the list and reaches its border, at every width: a list is one
    /// column, so nothing in it has a width of its own, and an item touches the panel's own edge.
    /// <para>
    /// The folder's pad used to stand outside every card, and the row's own inset stands inside
    /// it, so there were two twelves between the border and an icon — "a double spacing for its
    /// contents", which is what the person saw. The outer one is gone; the inner is the card's own
    /// padding, and text does not touch the border.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(FolderListWidth.Narrow)]
    [InlineData(FolderListWidth.Medium)]
    [InlineData(FolderListWidth.Wide)]
    public void EveryCardIsAsWideAsTheList(FolderListWidth width)
    {
        var options = List(ListKind.EnergySaver, ListKind.SoundVolume, ListKind.AirplaneMode);
        options.Widths = [width];

        var plan = Plan(options);

        foreach (var card in plan.Cards)
        {
            Assert.Equal(0, card.left, 1);
            Assert.Equal(plan.Width, card.right, 1);
        }
        // On every side: "the widgets own spacing should be the only one that is applied". The
        // first card starts at the panel's own top edge and the last ends at the content's foot,
        // with nothing of the folder's at either end. A card flush with the top takes the panel's
        // rounded corner rather than being cut by it — the panel clips its content to that shape
        // (FlyoutHost's body carries a geometric clip of its own rounded rectangle).
        Assert.Equal(0, plan.Cards[0].top, 1);
        var last = plan.Cards[^1];
        Assert.Equal(plan.Height, last.bottom, 1);
    }

    /// <summary>
    /// <b>Which drawing a line takes is not how tall the line is.</b> A component draws its own
    /// card on its line; an ordinary widget draws the list's icon-and-name row. That is a question
    /// about the kind — whether it has a bar drawing at all — and not about its height.
    /// <para>
    /// It used to be <c>ListCellsOf(kind) &gt; 1</c>, which meant a component could only keep its
    /// own drawing by claiming two rows. The screen-time day switcher's content was one line of
    /// chrome, so it was padded to about twice the height of anything in it, and the user saw the
    /// band under it.
    /// The two partitions coincide today, so nothing else moved — what changed is that a component
    /// can now ask for the height it wants and still be drawn as itself.
    /// </para>
    /// </summary>
    [Fact]
    public void WhichDrawingIsNotAHeight()
    {
        // A component draws itself whatever height it asks for — one row included.
        Assert.True(ListKind.SoundVolume.CardInList());
        Assert.Equal(LineHeight.Row, ListKind.SoundVolume.ListCellsOf());
        Assert.True(ListKind.SoundInput.CardInList());
        Assert.True(ListKind.NotificationsList.CardInList());
        // So is a shell: its glyph and its title are its card until its kind is built.
        Assert.True(ListKind.Timer.CardInList());
        Assert.Equal(LineHeight.Row, ListKind.Timer.ListCellsOf());

        // A widget that is in a list for its click draws its own line too, now that it has more to
        // show as the line grows: a switch's state and what a click does, what a pin opens.
        Assert.True(ListKind.EnergySaver.CardInList());
        Assert.True(ListKind.Pin.CardInList());

        // A taskbar entry with a list form of its own draws it: the question is the widget's line,
        // not whether it can stand on the bar.
        Assert.True(ListKind.Security.CardInList());
        Assert.True(ListKind.Clock.CardInList());

        // The rule that is left once the two are untied, and the one worth holding: **a widget
        // that draws the list's own row is one line tall**. The row is an icon and a name; drawn
        // into a box three rows high it would sit at the top of a band, which is the very defect
        // this untangling was found by.
        foreach (var kind in ListWidgets.All.Select(widget => widget.Kind))
        {
            Assert.True(
                kind.CardInList() || kind.ListCellsOf() == LineHeight.Row,
                $"{kind} draws the list's own icon-and-name row and asks for {kind.ListCellsOf()} rows of height");
        }
    }

    /// <summary>
    /// A component of one row is one line, and one line is what it is given: the card the list lays
    /// out for the sound panel's volume row is no taller than any other line, so there is no band
    /// under its content.
    /// </summary>
    [Fact]
    public void AOneRowComponentIsOneLineTall()
    {
        var plan = Plan(List(ListKind.EnergySaver, ListKind.SoundVolume));

        var line = plan.Cards[0].bottom - plan.Cards[0].top;
        var volume = plan.Cards[1].bottom - plan.Cards[1].top;

        Assert.Equal(40, line, 1);
        Assert.Equal(line, volume, 1);
    }

    /// <summary>
    /// <b>Every row of a list keeps one set of edges.</b> One leading icon column whatever stands
    /// in it — a pin's icon, a kind's glyph centred in it, an app's logo — so every name begins in
    /// the same place, and one inset at each end so every row ends in the same place.
    /// <para>
    /// They did not. A pin's icon was 20 and a glyph 16, each followed by the same inset, so a
    /// pin's name began 4 px further in than everything else's; and a converted panel, laying its
    /// rows out on its own numbers, began 14 further in than either. Nobody wrote those
    /// differences down — they are what three places arriving at a layout separately produces, and
    /// the user reads them off a screenshot straight away.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void EveryListRowKeepsTheSameEdges(float scale)
    {
        var textPx = 14 * scale;

        var inset = FolderFlyout.RowInset(textPx);
        var column = FolderFlyout.RowIcon(textPx);
        var text = FolderFlyout.RowText(textPx);

        Assert.True(inset > 0 && column > 0);
        // The column is wide enough for the widest thing that stands in it: a pin's own icon.
        Assert.True(column >= PopupPaint.Round(textPx * 16f / 14f), "a kind's glyph fits the column");
        // One formula, so the text's left edge cannot be worked out two ways.
        Assert.Equal(inset + column + inset, text, 3);
    }

    /// <summary>
    /// No line of the list's own between two widgets (2026-10-05: "the divider in between widgets
    /// should in list view should not be there per default"): two lines stand edge to edge (§410)
    /// and nothing else, whatever they are. A line between them is a divider somebody put
    /// there (<see cref="ADividerIsAThinLineWhereItWasPut"/>). The regression: the list drew a
    /// hairline between every two.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    public void AListDrawsNoLineOfItsOwnBetweenItsWidgets(float scale)
    {
        foreach (var kinds in new[]
        {
            new[] { ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.NightLight },
            [ListKind.EnergySaver, ListKind.SoundInput],
            [ListKind.WeatherNow, ListKind.SoundInput, ListKind.SoundVolume],
        })
        {
            var plan = Plan(List(kinds), scale);
            for (var index = 0; index + 1 < plan.Cards.Count; index++)
            {
                Assert.Equal(0, plan.Cards[index + 1].top - plan.Cards[index].bottom, 3);
            }
        }
        // The plan has no list of lines to draw at all any more: nothing to draw them from.
        Assert.Null(typeof(FolderPlan).GetProperty("Lines"));
    }

    /// <summary>
    /// A divider is a split of the list where somebody put it: the gap every folder separator takes
    /// (<see cref="FolderFlyout.SeparatorRoom"/>), whatever its range says, between the line over it and
    /// the line under it — the whole room between them — and the list's card ends over it and starts
    /// again under it. It is quiet: it never lights under the pointer.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void ADividerSplitsTheListWhereItWasPut(float scale)
    {
        var options = List(ListKind.EnergySaver, ListKind.Divider, ListKind.NightLight, ListKind.AirplaneMode);
        var plan = Plan(options, scale);

        var gap = FolderFlyout.SeparatorRoom(scale);
        var divider = plan.Cards[1];
        Assert.Equal(gap, divider.bottom - divider.top, 3);
        Assert.Equal(plan.Cards[0].bottom, divider.top, 3);
        Assert.Equal(plan.Cards[2].top, divider.bottom, 3);
        Assert.Equal(0, divider.left, 3);
        Assert.Equal(plan.Width, divider.right, 3);
        Assert.Equal([plan.Cards[0], PopupPaint.Rect(0, plan.Cards[2].top, plan.Width, plan.Cards[3].bottom - plan.Cards[2].top)], plan.Lists[0].Runs);
        Assert.True(FolderFlyout.Quiet(options.Cells[1]));
        Assert.True(FolderFlyout.Divides(options.Cells[1]));
        Assert.False(FolderFlyout.Quiet(options.Cells[0]));

        // Given a height of its own somebody wrote by hand, it is still the gap.
        options.Cells[1].Island.Height = LineHeight.Exactly(8);
        Assert.Equal(gap, Plan(options, scale).Cards[1].bottom - Plan(options, scale).Cards[1].top, 3);

        // First, it stands where it was put, and the card starts under it.
        var first = Plan(List(ListKind.Divider, ListKind.EnergySaver), scale);
        Assert.Equal(first.Lists[0].Top, first.Cards[0].top, 3);
        Assert.Equal(gap, first.Cards[0].bottom - first.Cards[0].top, 3);
        Assert.Equal([first.Cards[1]], first.Lists[0].Runs);
    }

    /// <summary>
    /// Every line the folder draws is the one hairline: an empty list's, a line's edit outline, a
    /// divider, at every scale. Two rules for the thickness of a line is a panel where they land
    /// a pixel apart at 150 % and nowhere else.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.25f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void EveryLineThePanelDrawsIsTheOneHairline(float scale)
    {
        var plan = Plan(List(ListKind.EnergySaver, ListKind.AirplaneMode), scale);

        Assert.Equal(SeparatorInk.Hairline(scale), plan.Hairline, 3);
        Assert.True(plan.Hairline >= 1, "a hairline is at least one device pixel");
        Assert.Equal(MathF.Round(plan.Hairline), plan.Hairline, 3);
    }

    /// <summary>A folder of lists, each its widgets of <paramref name="lists"/>, in order.</summary>
    private static FolderOptions Lists(params ListKind[][] lists)
    {
        var options = new FolderOptions();
        for (var list = 0; list < lists.Length; list++)
        {
            foreach (var kind in lists[list])
            {
                options.Put(kind.New(), list);
            }
        }
        return options;
    }

    /// <summary><paramref name="options"/> with its lists <paramref name="widths"/> wide, in order.</summary>
    private static FolderOptions Sized(FolderOptions options, params FolderListWidth[] widths)
    {
        options.Widths = [.. widths];
        return options;
    }

    /// <summary>
    /// A folder's height follows what is in it, and nothing else: there is no height to set
    /// (2026-10-09, "folder height = automatic only"). It grows with a line and is capped at the room
    /// beside the bar less an arranged list's "+" (§185).
    /// </summary>
    [Fact]
    public void AFolderIsAsTallAsItsContentsUpToThePanelsMost()
    {
        var grew = Plan(List(ListKind.EnergySaver, ListKind.AirplaneMode)).Height;
        Assert.True(Plan(List(ListKind.EnergySaver)).Height < grew, "automatic follows what is in it");

        var giant = List([.. Enumerable.Repeat(ListKind.EnergySaver, 40)]);
        var capped = FolderFlyout.Build(Look, 1f, FolderView.List, giant, giant.Cells, 600);
        Assert.True(capped.Height <= FolderFlyout.Reach(600, 0, 1) + 1, $"a panel {capped.Height} tall on a 600 px display");
        Assert.NotEmpty(capped.Lists[0].Hidden);
    }

    /// <summary>
    /// Each list of a folder is as wide as its own preset (2026-10-10: "change the width of
    /// individual columns"), a list with none medium, and they stand side by side, each a panel of
    /// its own, the divider gap (6 DIP by default) apart; every card as wide as its list, and the
    /// folder as wide as all of them. A list's hit-testing follows its own width.
    /// </summary>
    [Theory]
    [InlineData(1f, new[] { FolderListWidth.Medium, FolderListWidth.Medium, FolderListWidth.Medium })]
    [InlineData(1f, new[] { FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide })]
    [InlineData(1.5f, new[] { FolderListWidth.Wide, FolderListWidth.Narrow, FolderListWidth.Medium })]
    [InlineData(2f, new[] { FolderListWidth.Narrow })]
    [InlineData(1.25f, new FolderListWidth[0])]
    public void EachListIsAsWideAsItsOwnPreset(float scale, FolderListWidth[] widths)
    {
        var options = Lists([ListKind.EnergySaver], [ListKind.AirplaneMode, ListKind.NightLight], [ListKind.Cpu]);
        options.Widths = [.. widths];

        var plan = Plan(options, scale);

        float Each(int list) => PopupPaint.Round(FolderListWidths.Dip(list < widths.Length ? widths[list] : FolderListWidth.Medium) * scale);
        Assert.Equal(3, plan.Lists.Count);
        Assert.Equal(FolderFlyout.SeparatorRoom(scale), plan.ListGap, 3);
        var left = 0f;
        for (var list = 0; list < plan.Lists.Count; list++)
        {
            Assert.Equal(left, plan.Lists[list].Left, 3);
            Assert.Equal(Each(list), plan.Lists[list].Width, 3);
            foreach (var run in plan.Lists[list].Runs)
            {
                Assert.Equal(Each(list), run.right - run.left, 3);
            }
            Assert.Equal(list, plan.ListAt(left + 1));
            Assert.Equal(list, plan.ListAt(left + Each(list) - 1));
            left += Each(list) + plan.ListGap;
        }
        Assert.Equal(left - plan.ListGap, plan.Width, 3);
        Assert.Equal(-1, plan.ListAt(plan.Lists[0].Box.right + (plan.ListGap / 2)));
        for (var index = 0; index < plan.Cards.Count; index++)
        {
            var card = plan.Cards[index];
            Assert.Equal(Each(plan.CardList[index]), card.right - card.left, 3);
            Assert.Equal(plan.Lists[plan.CardList[index]].Left, card.left, 3);
        }
        Assert.Equal([0, 1, 1, 2], plan.CardList);
    }

    /// <summary>
    /// The person, 2026-10-10: "the spacing between list views should be the same as the divider
    /// spacing". Two lists stand exactly as far apart as two cards a divider splits a list into,
    /// at every scale, in edit mode as at rest.
    /// </summary>
    [Theory]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(1.25f)]
    [InlineData(2f)]
    public void TwoListsStandTheDividerGapApart(float scale)
    {
        var options = Lists([ListKind.EnergySaver, ListKind.Divider, ListKind.AirplaneMode], [ListKind.Cpu], [ListKind.Memory]);
        foreach (var editing in new[] { false, true })
        {
            var plan = FolderFlyout.Build(Look, scale, FolderView.List, options, options.Cells, 1040, editing: editing);

            Assert.Equal(FolderFlyout.SeparatorRoom(scale), plan.ListGap, 3);
            // The gap between the two cards the divider makes, and between every two lists.
            var cards = plan.Lists[0].Runs;
            Assert.Equal(plan.ListGap, cards[1].top - cards[0].bottom, 3);
            for (var list = 1; list < plan.Lists.Count; list++)
            {
                Assert.Equal(plan.ListGap, plan.Lists[list].Box.left - plan.Lists[list - 1].Box.right, 3);
            }
            Assert.Equal(plan.Lists[^1].Box.right, plan.Width, 3);
        }
    }

    /// <summary>
    /// Each list is a panel as tall as its own widgets — the person, 2026-10-09: "they should
    /// actually be shown separately, also have their separate heights" — and the lists line up on
    /// the bar's side: their bottoms level over a bottom bar, their tops level under a top bar. A
    /// list's lines start at its own panel's top.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void EachListIsItsOwnPanelAsTallAsItsWidgetsLinedUpOnTheBarsSide(Edge edge)
    {
        var one = Plan(List(ListKind.EnergySaver));
        var three = Plan(List(ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.NightLight));
        var options = Lists([ListKind.EnergySaver], [ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.NightLight]);
        var plan = FolderFlyout.Build(Look, 1f, FolderView.List, options, options.Cells, 1040, edge: edge);

        // Each panel as tall as its own widgets: the short one is not stretched to the tall one.
        Assert.Equal(one.Height, plan.Lists[0].Height, 3);
        Assert.Equal(three.Height, plan.Lists[1].Height, 3);
        Assert.True(plan.Lists[0].Height < plan.Lists[1].Height);
        Assert.Equal(three.Height, plan.Height, 3);
        if (edge == Edge.Top)
        {
            Assert.Equal(plan.Lists[0].Box.top, plan.Lists[1].Box.top, 3);
        }
        else
        {
            Assert.Equal(plan.Lists[0].Box.bottom, plan.Lists[1].Box.bottom, 3);
        }
        // A list's lines from its own panel's top, as a list of its own has them from its own.
        Assert.Equal(one.Cards[0].top - one.Lists[0].Top, plan.Cards[0].top - plan.Lists[0].Top, 3);
        Assert.Equal(one.Cards[0].bottom - one.Cards[0].top, plan.Cards[0].bottom - plan.Cards[0].top, 3);
        for (var index = 1; index < 4; index++)
        {
            Assert.Equal(three.Cards[index - 1].top - three.Lists[0].Top, plan.Cards[index].top - plan.Lists[1].Top, 3);
        }
        // Every line inside its own list's panel.
        for (var index = 0; index < plan.Cards.Count; index++)
        {
            var (card, panel) = (plan.Cards[index], plan.Lists[plan.CardList[index]].Box);
            Assert.InRange(card.top, panel.top, panel.bottom);
            Assert.InRange(card.bottom, panel.top, panel.bottom);
        }
    }

    /// <summary>
    /// A list taller than the most a list may be leaves its last lines out on its own: the others
    /// show whole, and the folder is no taller than a panel may be.
    /// </summary>
    [Fact]
    public void AListTallerThanThePanelLeavesOutItsOwnLastLines()
    {
        var options = Lists([ListKind.EnergySaver], [.. Enumerable.Repeat(ListKind.AirplaneMode, 30)], [ListKind.Cpu, ListKind.Memory]);

        var plan = FolderFlyout.Build(Look, 1f, FolderView.List, options, options.Cells, 800);

        Assert.True(plan.Height <= FolderFlyout.Reach(800, 0, 1) + 1);
        Assert.Empty(plan.Lists[0].Hidden);
        Assert.Empty(plan.Lists[2].Hidden);
        Assert.NotEmpty(plan.Lists[1].Hidden);
        Assert.Equal(plan.Lists[1].Hidden, plan.Hidden);
        // The short ones keep their own heights beside the one that is cut.
        Assert.True(plan.Lists[0].Height < plan.Lists[1].Height);
    }

    /// <summary>
    /// Regression guard for "no layout shift" (2026-10-09: list spacing "must be identical in edit
    /// and view mode"): arranging a folder adds its "+" buttons and changes nothing else — the
    /// same size, the same panels, every line in the same place — whatever the lists, the scale, the
    /// text size, the divider gap (the gap between the lists too) or the bar's edge.
    /// </summary>
    [Theory]
    [InlineData(1f, 1f, Edge.Bottom)]
    [InlineData(1.25f, 1f, Edge.Top)]
    [InlineData(1.5f, 1.5f, Edge.Left)]
    [InlineData(2f, 2.25f, Edge.Right)]
    public void ArrangingAFolderMovesNothing(float scale, float text, Edge edge)
    {
        var look = Look with { TextScale = text };
        foreach (var options in new[]
        {
            new FolderOptions(),
            List(ListKind.EnergySaver),
            List(ListKind.EnergySaver, ListKind.Divider, ListKind.AirplaneMode, ListKind.Cpu),
            Lists([ListKind.EnergySaver, ListKind.AirplaneMode], [ListKind.Cpu], [ListKind.Memory, ListKind.NightLight, ListKind.Divider, ListKind.Battery]),
            Lists([.. Enumerable.Repeat(ListKind.EnergySaver, 30)], [ListKind.Cpu]),
            // Each list its own width: the width buttons come and go with the "+", nothing else.
            Sized(Lists([ListKind.EnergySaver, ListKind.AirplaneMode], [ListKind.Cpu], [ListKind.Memory, ListKind.Divider, ListKind.Battery]),
                FolderListWidth.Narrow, FolderListWidth.Wide, FolderListWidth.Medium),
            Sized(List(ListKind.EnergySaver), FolderListWidth.Wide),
        })
        {
            var view = FolderFlyout.Build(look, scale, FolderView.List, options, options.Cells, 1000, editing: false, edge: edge);
            var edit = FolderFlyout.Build(look, scale, FolderView.List, options, options.Cells, 1000, editing: true, edge: edge);

            Assert.Empty(view.Adds);
            Assert.NotEmpty(edit.Adds);
            // A folder with no widget at all: its panel saying so at rest; arranged its one list
            // is an empty one, a slot of its width with no panel, no words (2026-10-12) and no
            // height (§297).
            if (options.Cells.Count == 0)
            {
                Assert.Equal((view.Width, 0f, view.ListGap), (edit.Width, edit.Height, edit.ListGap));
                Assert.Empty(edit.Pieces);
                Assert.Empty(edit.Empty);
                continue;
            }
            Assert.Equal((view.Width, view.Height, view.ListGap), (edit.Width, edit.Height, edit.ListGap));
            Assert.Equal(view.Cards, edit.Cards);
            Assert.Equal(view.CardList, edit.CardList);
            Assert.Equal(view.Lists.Select(list => list.Box), edit.Lists.Select(list => list.Box));
            Assert.Equal(view.Lists, edit.Lists);
            Assert.Equal(view.Pieces, edit.Pieces);
            Assert.Equal(view.Empty, edit.Empty);
        }
    }

    /// <summary>
    /// A list's buttons (the person, 2026-10-09): one round button of one size everywhere — none
    /// between two widgets of a list; its "+" (§246) and its width at the list's own end, outside its
    /// panel, wherever that end is (over it for a bottom bar, under it for a top bar), and none
    /// between two lists or beside them (the empty slots have theirs, FolderSlotTests). None stands
    /// on a line of a list.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    public void TheWidthButtonsStandAtEachListsEnd(Edge edge)
    {
        var options = Lists([ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.NightLight], [ListKind.Cpu], [ListKind.Memory, ListKind.Battery]);
        var plan = FolderFlyout.Build(Look, 1.5f, FolderView.List, options, options.Cells, 1000, editing: true, edge: edge);
        var side = PopupPaint.Round(FolderFlyout.AddDip * 1.5f);

        Assert.All(plan.Adds, add =>
        {
            Assert.Equal(side, add.Box.right - add.Box.left, 3);
            Assert.Equal(side, add.Box.bottom - add.Box.top, 3);
        });
        // Each list's "+" and width, no remove (2026-10-12); none for a new list (the slots beside the lists have theirs).
        Assert.Equal(6, plan.Adds.Count);

        // None between widgets: none stands over a line of a list.
        foreach (var add in plan.Adds)
        {
            var centre = new System.Numerics.Vector2((add.Box.left + add.Box.right) / 2, (add.Box.top + add.Box.bottom) / 2);
            Assert.DoesNotContain(plan.Cards, card => centre.X >= card.left && centre.X < card.right && centre.Y >= card.top && centre.Y < card.bottom);
        }

        var ends = plan.Adds.Where(add => add.Kind == FolderAddKind.Width).ToList();
        Assert.Equal(3, ends.Count);
        for (var list = 0; list < ends.Count; list++)
        {
            var (box, panel) = (ends[list].Box, plan.Lists[list].Box);
            Assert.Equal(list, ends[list].List);
            Assert.Equal(new FolderPlace(list, 0), ends[list].Place);
            // The pair centred on its list, the "+" first.
            var plus = plan.Adds.Single(add => add.Kind == FolderAddKind.Add && add.List == list).Box;
            Assert.Equal(box.top, plus.top);
            Assert.InRange((plus.left + box.right) / 2, plan.Lists[list].Left + (plan.Lists[list].Width / 2) - 1, plan.Lists[list].Left + (plan.Lists[list].Width / 2) + 1);
            if (edge == Edge.Bottom)
            {
                // At this list's own top, whatever the others' heights.
                Assert.True(box.bottom < panel.top, "a bottom bar's list end is over its list, outside it");
                Assert.True(panel.top - box.bottom <= side, "and right at it");
            }
            else
            {
                Assert.True(box.top > panel.bottom, "a top bar's list end is under its list, outside it");
                Assert.True(box.top - panel.bottom <= side, "and right at it");
            }
        }
        // The lists are of three heights, so their ends are on three lines.
        Assert.Equal(3, ends.Select(add => add.Box.top).Distinct().Count());
        Assert.DoesNotContain(plan.Adds, add => add.Place.New);
    }

    /// <summary>
    /// An empty folder offers its list's "+" and width; arranged its list is a slot of its width, no
    /// panel saying where widgets go. The slots beside it (FolderSlotTests) are where another list goes.
    /// </summary>
    [Fact]
    public void AnEmptyFolderOffersItsListsPlusAndWidthAndNewListsBeside()
    {
        var plan = Plan(new FolderOptions(), editing: true);

        Assert.Equal([FolderAddKind.Add, FolderAddKind.Width], plan.Adds.Select(add => add.Kind));
        Assert.All(plan.Adds, add => Assert.Equal(new FolderPlace(0, 0), add.Place));
        Assert.All(plan.Adds, add => Assert.Equal(0, add.List));
        Assert.Empty(plan.Empty);
    }

    /// <summary>
    /// edit: each list's "+" and width button (§246; no remove since 2026-10-12) are centred on
    /// its list together — at every width, for one, two and three
    /// lists, at 100, 150, 200 and 225 %, over a bottom bar and under a top one. No two buttons of the
    /// folder overlap, and none stands on a line or past its own list's edges.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 1f)]
    [InlineData(Edge.Top, 1f)]
    [InlineData(Edge.Bottom, 1.5f)]
    [InlineData(Edge.Top, 2f)]
    [InlineData(Edge.Bottom, 2.25f)]
    public void EachListsWidthButtonIsCentredOnIt(Edge edge, float scale)
    {
        FolderListWidth[][] widthsOf =
        [
            [FolderListWidth.Narrow],
            [FolderListWidth.Wide, FolderListWidth.Narrow],
            [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide],
            [FolderListWidth.Narrow, FolderListWidth.Narrow, FolderListWidth.Narrow],
        ];
        ListKind[][] all = [[ListKind.EnergySaver, ListKind.AirplaneMode, ListKind.NightLight], [ListKind.Cpu], [ListKind.Memory, ListKind.Battery]];
        foreach (var widths in widthsOf)
        {
            var options = Lists([.. all.Take(widths.Length)]);
            options.Widths = [.. widths];
            var plan = FolderFlyout.Build(Look, scale, FolderView.List, options, options.Cells, 1000, editing: true, edge: edge);
            var side = PopupPaint.Round(FolderFlyout.AddDip * scale);

            var sizes = plan.Adds.Where(add => add.Kind == FolderAddKind.Width).ToList();
            Assert.Equal(widths.Length, sizes.Count);
            for (var list = 0; list < widths.Length; list++)
            {
                var size = sizes[list];
                Assert.Equal(list, size.List);
                Assert.Equal(list, size.Place.List);
                Assert.Equal(side, size.Box.right - size.Box.left, 3);
                // The "+" and the width centred on its list (2026-10-11: "the group should be
                // centered"), inside its own width.
                var panel = plan.Lists[list].Box;
                var plus = plan.Adds.Single(add => add.Kind == FolderAddKind.Add && add.List == list).Box;
                Assert.True(MathF.Abs(((panel.left + panel.right) / 2) - ((plus.left + size.Box.right) / 2)) <= 1, $"list {list}'s buttons centred on it");
                Assert.True(size.Box.right < panel.right && plus.left > panel.left, $"list {list}'s buttons inside its own width");
                // Outside the panel, on its side away from the bar.
                Assert.True(edge == Edge.Top ? size.Box.top > panel.bottom : size.Box.bottom < panel.top, "outside its list, away from the bar");
            }
            for (var one = 0; one < plan.Adds.Count; one++)
            {
                for (var other = one + 1; other < plan.Adds.Count; other++)
                {
                    var (a, b) = (plan.Adds[one].Box, plan.Adds[other].Box);
                    Assert.False(a.left < b.right && b.left < a.right && a.top < b.bottom && b.top < a.bottom,
                        $"{plan.Adds[one].Kind} {plan.Adds[one].List} and {plan.Adds[other].Kind} {plan.Adds[other].List} overlap ({string.Join(",", widths)})");
                }
            }
            Assert.DoesNotContain(sizes, size => plan.Cards.Any(card =>
                size.Box.left < card.right && card.left < size.Box.right && size.Box.top < card.bottom && card.top < size.Box.bottom));
        }
    }

    /// <summary>
    /// A "+" outside the folder comes in over the folder's edge where the window or the display has
    /// no room beside it, moved as little as that takes, and stays where it was where there is room.
    /// </summary>
    [Fact]
    public void AnAddWithNoRoomOutsideComesInOverTheEdge()
    {
        var box = PopupPaint.Rect(-30, 40, 24, 24);

        Assert.Equal(box, FolderFlyout.KeepIn(box, PopupPaint.Rect(-50, -50, 500, 500)));
        Assert.Equal(PopupPaint.Rect(-12, 40, 24, 24), FolderFlyout.KeepIn(box, PopupPaint.Rect(-12, -50, 500, 500)));
        Assert.Equal(PopupPaint.Rect(-30, 0, 24, 24), FolderFlyout.KeepIn(box with { top = -40, bottom = -16 }, PopupPaint.Rect(-50, 0, 500, 500)));
        Assert.Equal(PopupPaint.Rect(426, 40, 24, 24), FolderFlyout.KeepIn(box with { left = 440, right = 464 }, PopupPaint.Rect(-50, -50, 500, 500)));
    }
}
