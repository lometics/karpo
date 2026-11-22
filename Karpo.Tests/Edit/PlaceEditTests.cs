// Edit mode across a display's places (Dock.DockWindow, IslandEdits.ToList, Layout.Only): a place along
// the bar's edge, coming in on hover, shows only its own section, so its parts are not the list's own and
// a drop or an add there is put into the list by the entry beside it, in that entry's section. A place's
// bar is one section with one "+"; and edit mode gives every place a window, a shared one too.
// Pure: no window is made.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class PlaceEditTests
{
    private static BarItem At(string id, BarSection section, BarKind kind = BarKind.EnergySaver) =>
        new() { Id = id, Kind = kind, Section = section };

    private static string Order(List<BarItem> list) =>
        string.Join(" ", list.Select(island => $"{island.Id}:{island.Section}"));

    // A bar with a widget at its start, two in its middle and one at its end.
    private static List<BarItem> Display() =>
    [
        At("s", BarSection.Start),
        At("m1", BarSection.Middle),
        At("m2", BarSection.Middle),
        At("e", BarSection.End),
    ];

    private static List<BarItem> Shows(List<BarItem> list, BarSection section) =>
        Sections.Along(list).FindAll(island => island.Section == section);

    [Fact]
    public void AnAddInAPlaceLandsAmongItsOwnEntriesNotAtTheListsStart()
    {
        var list = Display();
        var middle = Shows(list, BarSection.Middle);

        // The place's "+" between its two: its parts' index 1, the list's 2.
        var slot = IslandEdits.ToList(list, middle, new BarSlot(BarSection.Middle, 1), null, BarSection.Middle);
        Assert.Equal(new BarSlot(BarSection.Middle, 2), slot);
        var added = IslandEdits.Add(list, BarKind.NightLight, slot)!;

        Assert.Equal($"s:Start m1:Middle {added.Id}:Middle m2:Middle e:End", Order(list));
    }

    [Fact]
    public void ADropInAPlaceMovesWithinIt()
    {
        var list = Display();
        var m2 = list[2];

        var to = IslandEdits.ToList(list, Shows(list, BarSection.Middle), new BarSlot(BarSection.Middle, 0), m2, BarSection.Middle);
        Assert.True(IslandEdits.Move(list, m2, to));

        Assert.Equal("s:Start m2:Middle m1:Middle e:End", Order(list));
    }

    [Fact]
    public void ABlockCarriedFromTheBarIntoAPlaceTakesThePlacesSection()
    {
        var list = Display();
        var s = list[0];

        // Let go after m2 in the middle's place: the block is not one of that window's parts.
        var to = IslandEdits.ToList(list, Shows(list, BarSection.Middle), new BarSlot(BarSection.Middle, 2), s, BarSection.Middle);
        Assert.True(IslandEdits.Move(list, s, to));

        Assert.Equal("m1:Middle m2:Middle s:Middle e:End", Order(list));
    }

    [Fact]
    public void ABlockCarriedOutOfAPlaceOntoTheBarComesBackToTheBar()
    {
        var list = Display();
        var m1 = list[1];
        var bar = Sections.Along(list);

        // Before e, at the start of the bar's end section: the bar's part 2 without m1.
        var to = IslandEdits.ToList(list, bar, new BarSlot(BarSection.End, 2), m1, BarSection.End);
        Assert.True(IslandEdits.Move(list, m1, to));

        Assert.Equal("s:Start m2:Middle m1:End e:End", Order(list));
    }

    [Fact]
    public void AnEmptyPlaceTakesWhatIsDroppedOnItsPlus()
    {
        List<BarItem> list = [At("s", BarSection.Start), At("e", BarSection.End)];
        var e = list[1];

        var to = IslandEdits.ToList(list, [], new BarSlot(BarSection.Middle, 0), e, BarSection.Middle);
        Assert.Equal(new BarSlot(BarSection.Middle, 1), to);
        Assert.True(IslandEdits.Move(list, e, to));

        Assert.Equal("s:Start e:Middle", Order(list));
    }

    [Fact]
    public void APlacesBarIsItsOneSection()
    {
        var config = new Config { Height = 48, Style = BarStyle.Floating };
        var area = new RECT { left = 640, top = 0, right = 1280, bottom = 1080 };
        PartSpec[] specs = [new(Config.IslandKey(null, "t1"), BarKind.EnergySaver) { Section = BarSection.Middle }];
        var layout = Layout.ForTests(96, area, Edge.Top, config, specs, 40f, editing: true, only: BarSection.Middle);

        Assert.Equal(BarSection.Middle, layout.Only);
        Assert.False(layout.SectionRect(BarSection.Middle).IsEmpty);
        Assert.True(layout.SectionRect(BarSection.Start).IsEmpty);
        Assert.True(layout.SectionRect(BarSection.End).IsEmpty);
        // Anywhere along it, even its very ends, is the middle: a drop never lands in a section the
        // place does not have.
        Assert.Equal(BarSection.Middle, layout.SectionAt(0f));
        Assert.Equal(BarSection.Middle, layout.SectionAt(layout.Length));
        Assert.Equal(BarSection.Middle, layout.DropSlot(0f, -1).Section);

        // A bar keeps its three, an empty one as its own room while it is arranged.
        var bar = Layout.ForTests(96, area, Edge.Top, config, specs, 40f, editing: true);
        Assert.All((BarSection[])[BarSection.Start, BarSection.Middle, BarSection.End], section => Assert.False(bar.SectionRect(section).IsEmpty));
        // An empty place is its empty room alone.
        var empty = Layout.ForTests(96, area, Edge.Top, config, [], 40f, editing: true, only: BarSection.Middle);
        Assert.Single(empty.EmptyRooms());
    }

    [Fact]
    public void InEditModeEveryPlaceHasAWindowAnInnerOneToo()
    {
        var display = new RectI(0, 0, 1920, 1080);
        // A display above: the top is an inner edge.
        var places = HoverPlaces.For(display, [Edge.Bottom], (_, _) => false, [new RectI(0, -1080, 1920, 0)]);
        var inner = places.Single(place => place.Edge == Edge.Top && place.Section == BarSection.Start);

        Assert.All(places, place => Assert.False(HoverPlaces.Wanted(place, filled: false)));
        Assert.All(places, place => Assert.True(HoverPlaces.Wanted(place, filled: false, editing: true)));
        Assert.False(inner.Available);
        Assert.Equal(0, inner.SharedWith);
    }
}
