// Edit mode's list edits (Karpo.Dock\EditMode.cs): drops, Ctrl+arrow steps,
// the add list's adds and the options flyout's choices.
//
// A bar is three sections (2026-10-05), kept in one list — the start's entries,
// then the middle's, then the end's — so a place is a section and an index
// (BarSlot). A drop into another section changes the widget's section, and
// stepping past a section's end with the keys carries it into the next one.

using Karpo.Dock;

namespace Karpo.Tests.Edit;

public sealed class IslandEditsTests
{
    private static BarItem At(BarKind kind, string id, BarSection section = BarSection.Start) => new() { Id = id, Kind = kind, Section = section };

    /// <summary>The list as ids, each section's after a bar: "a b | c | d".</summary>
    private static string Order(List<BarItem> list) =>
        string.Join(" | ", new[] { BarSection.Start, BarSection.Middle, BarSection.End }
            .Select(section => string.Join(" ", list.Where(island => island.Section == section).Select(island => island.Id))));

    private static BarSlot In(BarSection section, int index) => new(section, index);

    [Fact]
    public void MoveGoesBeforeTheIslandAtTheIndex()
    {
        var list = new List<BarItem> { At(BarKind.Start, "a"), At(BarKind.NightLight, "x"), At(BarKind.WidgetsBoard, "b"), At(BarKind.FolderList, "c") };
        var x = list[1];

        Assert.True(IslandEdits.Move(list, x, In(BarSection.Start, 2)));

        Assert.Equal("a b x c |  | ", Order(list));
    }

    [Fact]
    public void MovePastTheLastIslandAppendsToItsSection()
    {
        var list = new List<BarItem> { At(BarKind.Start, "a"), At(BarKind.NightLight, "x"), At(BarKind.WidgetsBoard, "b") };

        Assert.True(IslandEdits.Move(list, list[0], In(BarSection.Start, 99)));

        Assert.Equal("x b a |  | ", Order(list));
    }

    /// <summary>
    /// A drop into another section moves the widget there and changes its section; the index on the
    /// boundary between two sections is the one the section says, and the list stays in order.
    /// </summary>
    [Fact]
    public void MoveIntoAnotherSectionChangesItsSection()
    {
        var list = new List<BarItem> { At(BarKind.WidgetsBoard, "w"), At(BarKind.Start, "s", BarSection.Middle), At(BarKind.NightLight, "c", BarSection.End) };
        var weather = list[0];

        // Index 1 among the others (s, c) is the boundary between the middle and the end.
        Assert.True(IslandEdits.Move(list, weather, In(BarSection.Middle, 1)));
        Assert.Equal(" | s w | c", Order(list));
        Assert.Equal(BarSection.Middle, weather.Section);

        Assert.True(IslandEdits.Move(list, weather, In(BarSection.End, 1)));
        Assert.Equal(" | s | w c", Order(list));
        Assert.Equal(["s", "w", "c"], list.Select(island => island.Id));
    }

    [Fact]
    public void MoveToWhereItIsChangesNothing()
    {
        var list = new List<BarItem> { At(BarKind.Start, "a"), At(BarKind.NightLight, "x"), At(BarKind.WidgetsBoard, "b") };

        Assert.False(IslandEdits.Move(list, list[1], In(BarSection.Start, 1)));
        Assert.False(IslandEdits.Move(list, new BarItem { Kind = BarKind.NightLight }, In(BarSection.Start, 0)));

        Assert.Equal("a x b |  | ", Order(list));
    }

    [Fact]
    public void StepMovesPastTheNeighbourInItsSection()
    {
        var list = new List<BarItem>
        {
            At(BarKind.Start, "s", BarSection.Middle),
            At(BarKind.Apps, "a", BarSection.Middle),
            At(BarKind.NightLight, "x", BarSection.Middle),
            At(BarKind.Tray, "t", BarSection.Middle),
        };
        var x = list[2];

        Assert.Equal(In(BarSection.Middle, 3), IslandEdits.Step(list, x, +1));
        Assert.Equal(In(BarSection.Middle, 1), IslandEdits.Step(list, x, -1));
    }

    [Fact]
    public void StepStopsAtTheBarsEnds()
    {
        var list = new List<BarItem> { At(BarKind.Start, "s"), At(BarKind.NightLight, "x", BarSection.End) };

        Assert.Null(IslandEdits.Step(list, list[0], -1));
        Assert.Null(IslandEdits.Step(list, list[1], +1));
        // An island not in the list has no place to step from.
        Assert.Null(IslandEdits.Step(list, At(BarKind.WidgetsBoard, "m"), +1));
    }

    /// <summary>
    /// The keys carry a widget across the whole bar: along its section, into the next one at its
    /// first place — an empty middle included, so it can be reached — and on to the end.
    /// </summary>
    /// <summary>
    /// A Dock has its start, middle and end, each a dock of its own, as a Taskbar has its three sections:
    /// the step carries a widget from one to the next.
    /// </summary>
    [Fact]
    public void StepCarriesAWidgetFromOneDockToTheNext()
    {
        var list = new List<BarItem> { At(BarKind.Start, "s", BarSection.Middle), At(BarKind.NightLight, "x", BarSection.Middle) };

        Assert.Equal(In(BarSection.Middle, 1), IslandEdits.Step(list, list[0], +1));
        Assert.Equal(In(BarSection.End, 1), IslandEdits.Step(list, list[1], +1));
        Assert.Equal(In(BarSection.Start, 0), IslandEdits.Step(list, list[0], -1));
    }

    [Fact]
    public void StepsWalkAnIslandAcrossEverySection()
    {
        var list = new List<BarItem>
        {
            At(BarKind.Start, "s"),
            At(BarKind.WidgetsBoard, "w"),
            At(BarKind.Tray, "t", BarSection.End),
            At(BarKind.NightLight, "c", BarSection.End),
        };
        var w = list[1];
        var seen = new List<string>();
        while (IslandEdits.Step(list, w, +1) is { } next)
        {
            IslandEdits.Move(list, w, next);
            seen.Add(Order(list));
        }

        Assert.Equal(
            new[]
            {
                "s | w | t c",
                "s |  | w t c",
                "s |  | t w c",
                "s |  | t c w",
            },
            seen);
        // And back again, the same way.
        var back = new List<string>();
        while (IslandEdits.Step(list, w, -1) is { } next)
        {
            IslandEdits.Move(list, w, next);
            back.Add(Order(list));
        }
        Assert.Equal("w s |  | t c", back[^1]);
    }

    [Fact]
    public void AddPutsANewIslandAtItsPlaceOrTheOneItWasGiven()
    {
        var list = new List<BarItem>
        {
            At(BarKind.Start, "s", BarSection.Middle),
            At(BarKind.Apps, "a", BarSection.Middle),
            At(BarKind.Tray, "t", BarSection.End),
            At(BarKind.NightLight, "c", BarSection.End),
        };

        var media = IslandEdits.Add(list, BarKind.Indicators);
        var weather = IslandEdits.Add(list, BarKind.WidgetsBoard, In(BarSection.Middle, 1));

        Assert.NotNull(media);
        Assert.NotNull(weather);
        // The status icons belong at the end, before the tray; the weather went where it was asked, in the middle.
        Assert.Equal(list.IndexOf(list.First(island => island.Id == "t")) - 1, list.IndexOf(media!));
        Assert.Equal(BarSection.End, media!.Section);
        Assert.Equal(1, list.IndexOf(weather!));
        Assert.Equal(BarSection.Middle, weather!.Section);
    }

    /// <summary>The "+" of an empty section adds into it: the widget is that section's first.</summary>
    [Fact]
    public void AddIntoAnEmptySection()
    {
        var list = new List<BarItem> { At(BarKind.WidgetsBoard, "w"), At(BarKind.NightLight, "c", BarSection.End) };

        var added = IslandEdits.Add(list, BarKind.WidgetsBoard, In(BarSection.Middle, 1));

        Assert.Equal(BarSection.Middle, added!.Section);
        Assert.Equal(["w", added.Id, "c"], list.Select(island => island.Id));
    }

    /// <summary>An index past the end appends rather than throwing: the dock's count may be stale.</summary>
    [Fact]
    public void AddClampsAPlacePastTheEnd()
    {
        var list = new List<BarItem> { At(BarKind.Start, "s") };

        var added = IslandEdits.Add(list, BarKind.NightLight, In(BarSection.End, 99));

        Assert.NotNull(added);
        Assert.Equal(1, list.IndexOf(added!));
    }

    [Fact]
    public void AddTakesASecondOfAnyKind()
    {
        var list = new List<BarItem> { At(BarKind.Start, "s"), At(BarKind.NightLight, "c") };

        // Two Start buttons are odd, and nobody's business but the person's.
        Assert.NotNull(IslandEdits.Add(list, BarKind.Start));
        Assert.NotNull(IslandEdits.Add(list, BarKind.NightLight));
        Assert.Equal(4, list.Count);
        Assert.Equal(4, list.Select(island => island.Id).Distinct().Count());
    }

    [Fact]
    public void HeaderNamesTheKindAndWhatTellsItApart()
    {
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        var weather = ListKind.WeatherNow.New();
        // A place picked: a new one is for the device's location (§243), whose name is the dock's (§255, DevicePlaceTests).
        weather.Weather!.UseDeviceLocation = false;
        weather.Weather.Place = "Zurich";

        Assert.Equal("Clock · Tokyo", IslandEdits.Header(clock, _ => "Tokyo"));
        clock.Clock.Label = "Home";
        Assert.Equal("Clock · Home", IslandEdits.Header(clock, _ => "Tokyo"));
        Assert.Equal("Current weather · Zurich", IslandEdits.Header(weather));
        Assert.Equal("Start button", IslandEdits.Header(BarKind.Start.New()));
    }
}
