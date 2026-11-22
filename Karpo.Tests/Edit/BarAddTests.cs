// The bar's "+" in edit mode (the person, 2026-10-23: "there should be add buttons for widgets in the
// taskbar edit mode as well, they should be left and right in the center bar. (if no widget is there
// only show one add button) and they should be on the right of the left group and on the left of the
// right group. then they should also be added for every other group in the same fashion"), on layouts
// measured without a Gfx (Layout.ForTests):
//   - one rule over a section's place (Layout.AddEnds): its inner ends — after the start's last widget,
//     before the end's first, either side of the middle's — and one alone in an empty section's room;
//   - the "+" take room of their own beside the widgets, so they cover none, and only in edit mode;
//     the widgets stand where they stood at rest;
//   - a "+" adds before its section's first entry or after its last, in that section.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class BarAddTests
{
    private const float Cell = 40f;
    private const string Owner = "default";

    private static RECT Area(int length, Edge edge) => edge.IsVertical()
        ? new RECT { left = 0, top = 0, right = 48, bottom = length }
        : new RECT { left = 0, top = 0, right = length, bottom = 48 };

    private static BarItem Item(string id, BarKind kind, BarSection section)
    {
        var item = kind.New();
        item.Id = id;
        item.Section = section;
        return item;
    }

    /// <summary>A widget or two in each section: the weather at the start, Start and search in the middle, the clock and show desktop at the end.</summary>
    private static List<BarItem> Full() =>
    [
        Item("weather", BarKind.WidgetsBoard, BarSection.Start),
        Item("start", BarKind.Start, BarSection.Middle),
        Item("search", BarKind.Search, BarSection.Middle),
        Item("clock", BarKind.NightLight, BarSection.End),
        Item("desktop", BarKind.ShowDesktop, BarSection.End),
    ];

    /// <summary>The same with nothing in the middle.</summary>
    private static List<BarItem> NoMiddle() => [.. Full().Where(item => item.Section != BarSection.Middle)];

    private static Layout Bar(IReadOnlyList<BarItem> list, Edge edge = Edge.Bottom, bool editing = true, BarStyle style = BarStyle.Islands, BarSection? only = null) =>
        Layout.ForTests(96, Area(1600, edge), edge, new Config { Style = style },
            [.. list.Select(item => PartSpec.For(Config.IslandKey(Owner, item.Id), item, new InputHardware(false, false, false)))], Cell, editing: editing, only: only);

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float AlongEnd(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    private static IEnumerable<int> PartsIn(Layout layout, BarSection section) =>
        Enumerable.Range(0, layout.Specs.Count).Where(part => layout.Specs[part].Section == section && layout.Placed(part));

    // --- The rule --------------------------------------------------------------------

    /// <summary>A section's "+" stand at its ends facing the bar's middle; an empty section has one, alone.</summary>
    [Fact]
    public void ASectionsPlusStandsAtItsInnerEndsAndAloneWhereItIsEmpty()
    {
        Assert.Equal([AddEnd.After], Layout.AddEnds(BarSection.Start, held: true));
        Assert.Equal([AddEnd.Before, AddEnd.After], Layout.AddEnds(BarSection.Middle, held: true));
        Assert.Equal([AddEnd.Before], Layout.AddEnds(BarSection.End, held: true));
        foreach (var section in new[] { BarSection.Start, BarSection.Middle, BarSection.End })
        {
            Assert.Equal([AddEnd.Alone], Layout.AddEnds(section, held: false));
        }
    }

    /// <summary>
    /// On every edge and in both looks: the start's "+" after its last widget, the middle's before its
    /// first and after its last, the end's before its first — along the bar, so down a side bar the
    /// top is the start — each on the bar, apart from every widget, round, in order along it.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, BarStyle.Islands)]
    [InlineData(Edge.Top, BarStyle.Islands)]
    [InlineData(Edge.Left, BarStyle.Islands)]
    [InlineData(Edge.Right, BarStyle.Islands)]
    [InlineData(Edge.Bottom, BarStyle.Floating)]
    [InlineData(Edge.Left, BarStyle.Floating)]
    public void EverySectionHasItsPlusBesideItsWidgets(Edge edge, BarStyle style)
    {
        var layout = Bar(Full(), edge, style: style);
        var adds = layout.Adds();

        Assert.Equal(
            [(BarSection.Start, AddEnd.After), (BarSection.Middle, AddEnd.Before), (BarSection.Middle, AddEnd.After), (BarSection.End, AddEnd.Before)],
            adds.Select(add => (add.Section, add.End)));
        for (var index = 0; index < adds.Count; index++)
        {
            var add = adds[index];
            Assert.Equal(add.Rect.Width, add.Rect.Height);
            Assert.True(add.Rect.Width > 0f);
            Assert.Equal(Hit.Add(index), layout.HitTest(add.Rect.Center.X, add.Rect.Center.Y, editing: true));
            var parts = PartsIn(layout, add.Section).Select(layout.BlockRect).ToList();
            if (add.End == AddEnd.Before)
            {
                Assert.True(AlongEnd(layout, add.Rect) < parts.Min(rect => Along(layout, rect)), $"{add} stands before its section's first widget");
            }
            else
            {
                Assert.True(Along(layout, add.Rect) > parts.Max(rect => AlongEnd(layout, rect)), $"{add} stands after its section's last widget");
            }
            // On no widget, of any section.
            Assert.All(Enumerable.Range(0, layout.Specs.Count).Where(layout.Placed).Select(layout.BlockRect), block =>
                Assert.True(AlongEnd(layout, block) <= Along(layout, add.Rect) || Along(layout, block) >= AlongEnd(layout, add.Rect), $"{add} is over a widget"));
            if (index > 0)
            {
                Assert.True(Along(layout, add.Rect) > AlongEnd(layout, adds[index - 1].Rect));
            }
        }
        // floating: beside its dock, not on it.
        Assert.All(layout.SectionPanels().Where(dock => !dock.IsEmpty), dock => Assert.All(adds, add =>
            Assert.True(AlongEnd(layout, dock) <= Along(layout, add.Rect) || Along(layout, dock) >= AlongEnd(layout, add.Rect), $"{add} is on a dock")));
    }

    /// <summary>A section with nothing in it has one "+", in the middle of its room; the middle's stays on the bar's centre.</summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Left)]
    public void AnEmptySectionHasOnePlusInItsRoom(Edge edge)
    {
        var layout = Bar(NoMiddle(), edge);
        var room = Assert.Single(layout.EmptyRooms());

        var alone = Assert.Single(layout.Adds(), add => add.Section == BarSection.Middle);
        Assert.Equal(AddEnd.Alone, alone.End);
        Assert.Equal((Along(layout, room) + AlongEnd(layout, room)) / 2f, (Along(layout, alone.Rect) + AlongEnd(layout, alone.Rect)) / 2f, 1f);
        Assert.Equal(layout.Length / 2f, (Along(layout, alone.Rect) + AlongEnd(layout, alone.Rect)) / 2f, 1f);
        Assert.Equal(3, layout.Adds().Count);
    }

    /// <summary>A place along an edge shows its one section, and only its "+".</summary>
    [Fact]
    public void APlaceHasItsOwnSectionsPlusAlone()
    {
        var start = Full().Where(item => item.Section == BarSection.Start).ToList();
        var layout = Bar(start, only: BarSection.Start);

        var add = Assert.Single(layout.Adds());
        Assert.Equal((BarSection.Start, AddEnd.After), (add.Section, add.End));
    }

    // --- Room, and nothing at rest ------------------------------------------------------

    /// <summary>
    /// At rest there is no "+" and no room for one, and no press finds one. In edit mode they take
    /// room beside the widgets, which stand where they stood at rest: the start's run from the bar's
    /// start, the end's to its end, and the middle stays centred.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, BarStyle.Islands)]
    [InlineData(Edge.Right, BarStyle.Islands)]
    [InlineData(Edge.Bottom, BarStyle.Floating)]
    public void ThePlusTakesRoomInEditModeAloneAndMovesNoWidget(Edge edge, BarStyle style)
    {
        var rest = Bar(Full(), edge, editing: false, style: style);
        var edit = Bar(Full(), edge, editing: true, style: style);

        Assert.Empty(rest.Adds());
        foreach (var add in edit.Adds())
        {
            Assert.NotEqual(HitKind.Add, rest.HitTest(add.Rect.Center.X, add.Rect.Center.Y)?.Kind);
            Assert.NotEqual(HitKind.Add, edit.HitTest(add.Rect.Center.X, add.Rect.Center.Y, editing: false)?.Kind);
        }
        for (var part = 0; part < rest.Specs.Count; part++)
        {
            Assert.Equal(rest.PartAt(part).Left, edit.PartAt(part).Left, 1f);
        }
        foreach (var section in new[] { BarSection.Start, BarSection.Middle, BarSection.End })
        {
            var restRect = rest.SectionRect(section);
            var editRect = edit.SectionRect(section);
            Assert.True(edit.Vertical ? editRect.Height > restRect.Height : editRect.Width > restRect.Width, $"the {section} makes no room for its \"+\"");
        }
    }

    // --- Where a "+" adds ------------------------------------------------------------------

    /// <summary>
    /// Each "+" adds in its section, before its first entry or after its last, as the dock reads its
    /// place in the list (DockWindow.ListSlot, IslandEdits.ToList) and the add list puts the pick there
    /// (AddList.Add); an empty section's "+" into that section.
    /// </summary>
    [Fact]
    public void EachPlusAddsAtItsEndOfItsSection()
    {
        var weather = Catalogue.On(WidgetType.Bar).Single(entry => entry.Bar == BarKind.WidgetsBoard);
        foreach (var made in new Func<List<BarItem>>[] { Full, NoMiddle })
        {
            var adds = Bar(made()).Adds();
            for (var index = 0; index < adds.Count; index++)
            {
                var list = made();
                var add = adds[index];
                var to = IslandEdits.ToList(list, list, add.Slot, null, add.Section);
                var id = AddList.Add(list, new AddSpot(Owner, add.Slot, To: to), weather);

                Assert.NotNull(id);
                var section = list.Where(item => item.Section == add.Section).ToList();
                var landed = section.FindIndex(item => item.Id == id);
                Assert.True(landed >= 0, $"the \"+\" {index} added outside its section");
                Assert.Equal(add.End == AddEnd.After ? section.Count - 1 : 0, landed);
                // Everything else as it was, in its order.
                Assert.Equal(made().Select(item => item.Id), list.Where(item => item.Id != id).Select(item => item.Id));
            }
        }
    }
}
