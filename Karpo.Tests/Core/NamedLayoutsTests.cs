// Named layouts, each of a type (the person, 2026-10-08): a layout's widgets live in its one bar's three
// sections, and its type says how the bar looks: a Taskbar one bar, a Dock three docks. Changing the
// type moves and deletes nothing. Where the bar stands is the display's edge, not the layout's. Every
// display shows one layout, or None, and at least one display attached shows Karpo. Two apps' edits
// merge three ways. (§488: the Top and bottom type, a second bar with a second row of three sections,
// is gone; a file that names it reads as a Taskbar, every widget on the one bar.)

using System.Text.Json.Nodes;
using static Karpo.Core.BarKind;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Core;

public sealed class NamedLayoutsTests
{
    private static readonly BarSection[] Three = [BarSection.Start, BarSection.Middle, BarSection.End];

    private static BarItem Entry(string id, BarKind kind, BarSection section) =>
        new() { Id = id, Kind = kind, Section = section };

    /// <summary>A layout of <paramref name="type"/> with a widget in each of its three sections, two in the middle.</summary>
    private static TaskbarLayout Four(LayoutType type = LayoutType.Taskbar, string id = "L") => new()
    {
        Id = id,
        Name = id,
        Type = type,
        Islands =
        [
            Entry("ms", BarKind.WidgetsBoard, BarSection.Start),
            Entry("mm", Start, BarSection.Middle),
            Entry("mm2", Apps, BarSection.Middle),
            Entry("me", Tray, BarSection.End),
        ],
    };

    private static List<string> Ids(IEnumerable<BarItem> list) => [.. list.Select(island => island.Id)];

    /// <summary>The layout as the file would hold it: every widget, section and order, byte for byte.</summary>
    private static string Written(TaskbarLayout layout)
    {
        var config = new Config { Layouts = [layout], DefaultLayoutId = layout.Id };
        config.Layouts[0].Type = layout.Type;
        return string.Join("\n", layout.Islands.Select(island => $"{island.Id} {island.Section}")) + "\n"
            + JsonNode.Parse(config.ToJson())!["layouts"]![0]!["islands"]!.ToJsonString();
    }

    // --- One bar, at the display's edge --------------------------------------------------------------

    /// <summary>
    /// Every type has one bar, on the display's edge, whichever of the four it is, with the three sections;
    /// a Dock's floats, a Taskbar's wears islands; no other edge has anything.
    /// </summary>
    [Fact]
    public void EachTypeHasOneBarAtTheDisplaysEdge()
    {
        Assert.Equal([LayoutType.Taskbar, LayoutType.Dock], LayoutTypes.All);
        foreach (var type in LayoutTypes.All)
        {
            foreach (var screen in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
            {
                var edges = new TaskbarLayout { Type = type }.Edges(screen);
                Assert.Equal([screen], edges.Shown);
                Assert.Equal(type == LayoutType.Dock ? EdgeLayout.Dock : EdgeLayout.Taskbar, edges[screen].Layout);
                Assert.All(Three, section => Assert.True(edges.Has(screen, section)));
            }
        }
    }

    /// <summary>
    /// A window shows the widgets of its bar: the whole of it on the display's edge, a section of it in a
    /// place coming in on hover there, nothing on any other edge.
    /// </summary>
    [Fact]
    public void AWindowShowsItsBarsWidgets()
    {
        var taskbar = Four();
        Assert.Equal(["ms", "mm", "mm2", "me"], Ids(TheDock.Shows(taskbar, Edge.Bottom, null, Edge.Bottom)));
        Assert.Empty(TheDock.Shows(taskbar, Edge.Bottom, null, Edge.Top));
        Assert.Empty(TheDock.Shows(taskbar, Edge.Bottom, new EdgePlace(Edge.Top, BarSection.Start), null));

        // A Dock, its bar standing or each of its docks coming in on hover.
        var dock = Four(LayoutType.Dock);
        Assert.Equal(["ms", "mm", "mm2", "me"], Ids(TheDock.Shows(dock, Edge.Bottom, null, Edge.Bottom)));
        Assert.Equal(["ms"], Ids(TheDock.Shows(dock, Edge.Bottom, new EdgePlace(Edge.Bottom, BarSection.Start), null)));
        Assert.Equal(["mm", "mm2"], Ids(TheDock.Shows(dock, Edge.Bottom, new EdgePlace(Edge.Bottom, BarSection.Middle), null)));
        Assert.Equal(["me"], Ids(TheDock.Shows(dock, Edge.Bottom, new EdgePlace(Edge.Bottom, BarSection.End), null)));
        Assert.Empty(TheDock.Shows(dock, Edge.Bottom, null, Edge.Top));
        Assert.Equal(4, dock.Islands.Count);
    }

    /// <summary>
    /// A layout's bar shows as it says, at its size, held to what a bar may be: a dock floats, a bar wears
    /// islands.
    /// </summary>
    [Fact]
    public void TheBarShowsAsItsLayoutSaysAtItsSize()
    {
        var taskbar = new TaskbarLayout { Type = LayoutType.Taskbar, Show = EdgeMode.Hover, Size = 40 }.Edges(Edge.Bottom);
        Assert.Equal((EdgeMode.Hover, 40), (taskbar.Bottom.Mode, taskbar.Bottom.Size));
        Assert.Equal(BarStyle.Islands, taskbar.Bottom.Style);
        Assert.Empty(taskbar.Standing);

        var dock = new TaskbarLayout { Type = LayoutType.Dock, Show = EdgeMode.Float, Size = 56 }.Edges(Edge.Top);
        Assert.Equal(EdgeMode.Float, dock.Top.Mode);
        Assert.Equal(56, dock.Top.Size);
        Assert.Equal(BarStyle.Floating, dock.Top.Style);
        Assert.Equal([Edge.Top], dock.Shown);

        Assert.Equal(Config.MaxHeight, new TaskbarLayout { Size = 900 }.Edges(Edge.Left).Left.Size);
        Assert.Equal(Config.MinHeight, new TaskbarLayout { Size = 5 }.BarSize);
    }

    // --- Changing a layout's type moves nothing --------------------------------------------------

    /// <summary>
    /// Switching the type and back leaves every widget's section and place in the list, and the file's
    /// bytes, exactly as they were: Taskbar to Dock to Taskbar, and Dock to Taskbar to Dock.
    /// </summary>
    [Theory]
    [InlineData(LayoutType.Taskbar, LayoutType.Dock)]
    [InlineData(LayoutType.Dock, LayoutType.Taskbar)]
    public void ASwitchOfTypeAndBackLeavesEveryWidgetExactlyWhereItWas(LayoutType from, LayoutType to)
    {
        var layout = Four(from);
        layout.Islands[3].Countdown = new CountdownOptions { Minutes = 7 };
        var before = Written(layout);

        Assert.True(layout.Become(to));
        Assert.Equal(4, layout.Islands.Count);
        Assert.Equal(before.Split("\n")[0..4], Written(layout).Split("\n")[0..4]);
        Assert.True(layout.Become(from));

        Assert.Equal(before, Written(layout));
        Assert.Equal(7, layout.Islands[3].Countdown!.Minutes);
    }

    /// <summary>Making it the type it is changes nothing and says so; an unknown type reads as a Taskbar.</summary>
    [Fact]
    public void BecomingTheSameTypeChangesNothing()
    {
        var layout = Four(LayoutType.Dock);
        Assert.False(layout.Become(LayoutType.Dock));
        Assert.True(layout.Become((LayoutType)99));
        Assert.Equal(LayoutType.Taskbar, layout.Type);
    }

    /// <summary>
    /// From any type to any other, in any number of steps, no widget is ever deleted, moved or
    /// reordered: the list is exactly the one the layout started with.
    /// </summary>
    [Fact]
    public void NoChangeOfTypeEverMovesOrDeletesAWidget()
    {
        var start = Written(Four());
        foreach (var a in LayoutTypes.All)
        {
            foreach (var b in LayoutTypes.All)
            {
                foreach (var c in LayoutTypes.All)
                {
                    var layout = Four();
                    _ = layout.Become(a);
                    _ = layout.Become(b);
                    _ = layout.Become(c);

                    Assert.Equal(start.Split("\n")[0..4], Written(layout).Split("\n")[0..4]);
                    var ordered = new List<BarItem>(layout.Islands);
                    Assert.False(Sections.Order(ordered), $"{a} {b} {c}: the list is out of its sections' order");
                }
            }
        }
    }

    // --- The edge is the display's -----------------------------------------------------------------

    /// <summary>
    /// One layout on two displays whose edges differ: each display's bar at its own edge, the same
    /// widgets in both.
    /// </summary>
    [Fact]
    public void OneLayoutOnTwoDisplaysStandsAtEachDisplaysOwnEdge()
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Dell");
        config.AddDisplay("D2", "LG");
        config.EditScreen("D2", "").Edge = Edge.Top;

        Assert.Equal([Edge.Bottom], config.EdgesOf("D1").Shown);
        Assert.Equal([Edge.Top], config.EdgesOf("D2").Shown);
        Assert.Same(config.IslandsFor("D1"), config.IslandsFor("D2"));
        var layout = config.ActiveLayout("D2");
        var onBottom = TheDock.Shows(layout, config.EdgeOfScreen("D1"), null, Edge.Bottom);
        var onTop = TheDock.Shows(layout, config.EdgeOfScreen("D2"), null, Edge.Top);
        Assert.NotEmpty(onBottom);
        Assert.Equal(Ids(onBottom), Ids(onTop));
        Assert.Empty(TheDock.Shows(layout, Edge.Top, null, Edge.Bottom));
    }

    /// <summary>A Dock stands at its display's edge too, whichever it is.</summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void ADockFollowsTheDisplaysEdge(Edge screen)
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Dell");
        config.EditScreen("D1", "").Edge = screen;
        var layout = config.ActiveLayout("D1");

        _ = layout.Become(LayoutType.Dock);
        Assert.Equal([screen], config.EdgesOf("D1").Shown);
        Assert.Equal(EdgeLayout.Dock, config.EdgesOf("D1")[screen].Layout);
    }

    /// <summary>A display's edge is any of the four (2026-10-12: the sides came back); a word this build does not know reads as the bottom.</summary>
    [Theory]
    [InlineData("top", Edge.Top)]
    [InlineData("bottom", Edge.Bottom)]
    [InlineData("left", Edge.Left)]
    [InlineData("right", Edge.Right)]
    [InlineData("diagonal", Edge.Bottom)]
    public void ADisplaysEdgeIsAnyOfTheFour(string word, Edge expected)
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "screens": [ { "id": "D1", "edge": "{{word}}" } ] }
            """, out var changed);

        Assert.Equal(expected, config.ScreenFor("D1").Edge);
        Assert.Equal(expected, config.EdgeOfScreen("D1"));
        // A value that was no choice is written back as the bottom.
        var json = config.ToJson();
        Assert.Equal(expected == Edge.Top, json.Contains("\"edge\": \"top\"", StringComparison.Ordinal));
        Assert.DoesNotContain($"\"edge\": \"{word}\"", word is "top" or "bottom" or "left" or "right" ? "" : json, StringComparison.Ordinal);
        Assert.Equal(expected, Configs.RoundTrip(config, out var twice).ScreenFor("D1").Edge);
        Assert.False(twice);
        Assert.Equal(Edge.Bottom, config.EdgeOfScreen("not-listed"));
    }

    // --- Its size ----------------------------------------------------------------------------------

    /// <summary>Repair clamps the size to 28..160, and gives a size the file left out the default.</summary>
    [Theory]
    [InlineData(5, Config.MinHeight)]
    [InlineData(900, Config.MaxHeight)]
    [InlineData(0, Config.DefaultHeight)]
    [InlineData(64, 64)]
    public void RepairClampsTheSize(int size, int expected)
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "L", "name": "L", "type": "taskbar", "size": {{size}} } ], "defaultLayoutId": "L" }
            """);

        Assert.Equal(expected, config.Layout("L")!.Size);
        var again = Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
        Assert.Equal(expected, again.Layout("L")!.Size);
    }

    // --- The file ------------------------------------------------------------------------------------

    /// <summary>
    /// The file says a layout's type, size and show and a widget's section; a type this build does not
    /// know reads as a Taskbar, written back. No widget moves for its type.
    /// </summary>
    [Fact]
    public void ALayoutGoesThroughTheFile()
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [
              { "id": "d", "name": "Dock", "type": "dock", "size": 56, "show": "hover",
                "islands": [ { "id": "a", "kind": "start", "section": "middle" },
                             { "id": "z", "kind": "widgetsBoard", "section": "start" } ] },
              { "id": "x", "name": "Future", "type": "spiral", "islands": [ { "id": "b", "kind": "tray", "section": "end" } ] } ],
              "defaultLayoutId": "d" }
            """, out var changed);

        Assert.True(changed);
        var dock = config.Layout("d")!;
        Assert.Equal((LayoutType.Dock, 56, EdgeMode.Hover), (dock.Type, dock.Size, dock.Show));
        Assert.Equal(["z", "a"], Ids(dock.Islands));
        var future = config.Layout("x")!;
        Assert.Equal(LayoutType.Taskbar, future.Type);
        Assert.Equal(BarSection.End, Sections.Of(future.Islands[0]));
        var again = Configs.RoundTrip(config, out var twice);
        Assert.False(twice);
        var json = again.ToJson();
        Assert.Contains("\"type\": \"dock\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"taskbar\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"edge\"", json.Replace("\"edge\": \"bottom\"", ""), StringComparison.Ordinal);
    }

    /// <summary>
    /// §488: a file from before the Top and bottom went, a layout of that type with widgets in its top row
    /// and its top bar's size and show: read as a Taskbar, every widget on the one bar in its section, in
    /// the order the sections have; the second bar's words leave the file when it is written.
    /// </summary>
    [Fact]
    public void ATopAndBottomFromBeforeReadsAsATaskbarItsWidgetsOnTheOneBar()
    {
        var config = Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [
              { "id": "L", "name": "Both", "type": "topBottom", "size": 48, "topSize": 32, "show": "reserve", "topShow": "hover",
                "islands": [ { "id": "s", "kind": "start", "row": "main", "section": "start" },
                             { "id": "c", "kind": "tray", "row": "main", "section": "end" },
                             { "id": "d", "kind": "widgetsBoard", "row": "top", "section": "start" },
                             { "id": "n", "kind": "nightLight", "row": "top", "section": "end" } ] } ],
              "defaultLayoutId": "L" }
            """, out var changed);

        Assert.True(changed);
        var layout = config.Layout("L")!;
        Assert.Equal((LayoutType.Taskbar, 48, EdgeMode.Reserve), (layout.Type, layout.Size, layout.Show));
        Assert.Equal(["s", "d", "c", "n"], Ids(layout.Islands));
        Assert.Equal([BarSection.Start, BarSection.Start, BarSection.End, BarSection.End], layout.Islands.Select(Sections.Of));
        var json = config.ToJson();
        foreach (var gone in new[] { "topBottom", "topSize", "topShow", "\"row\"" })
        {
            Assert.DoesNotContain(gone, json, StringComparison.Ordinal);
        }
    }

    /// <summary>A display's edge is in the file, and a layout has none of its own any more.</summary>
    [Fact]
    public void TheEdgeIsWrittenOnTheDisplayNotTheLayout()
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Dell");
        config.EditScreen("D1", "").Edge = Edge.Top;

        var json = JsonNode.Parse(config.ToJson())!;

        Assert.Equal("top", json["screens"]![0]!["edge"]!.GetValue<string>());
        Assert.Null(json["layouts"]![0]!["edge"]);
        Assert.Null(json["layouts"]![0]!["islands"]![0]!["row"]);
        Assert.Null(json["layouts"]![0]!["islands"]![0]!["edge"]);
        Assert.Equal(Edge.Top, Configs.RoundTrip(config, out _).ScreenFor("D1").Edge);
    }

    // --- Which layout each display shows ---------------------------------------------------------

    /// <summary>
    /// A display shows its own choice of layout, its widgets and its bars; a display Karpo has not seen
    /// shows the default layout; two displays showing one layout show the same widgets.
    /// </summary>
    [Fact]
    public void EachDisplayShowsItsLayout()
    {
        var config = new Config();
        config.Repair();
        var dock = config.AddLayout("Dock", [Entry("c", BarKind.NightLight, BarSection.End)], LayoutType.Dock);
        config.AddDisplay("D1", "Dell");
        config.AddDisplay("D2", "LG");
        config.EditScreen("D2", "").Edge = Edge.Top;

        Assert.True(config.SelectLayout("D2", dock.Id));

        Assert.Equal(config.DefaultLayout.Id, config.ScreenFor("D1").ActiveLayoutId);
        Assert.Same(config.DefaultLayout.Islands, config.IslandsFor("D1"));
        Assert.Same(dock.Islands, config.IslandsFor("D2"));
        Assert.Same(dock.Islands, config.ListFor(Config.DisplayOwner("D2")));
        Assert.Same(dock.Islands, config.ListFor(Config.LayoutOwner(dock.Id)));
        Assert.Equal([Edge.Top], config.EdgesOf("D2").Shown);
        Assert.Equal([Edge.Bottom], config.EdgesOf("D1").Shown);
        // The dock's widget stays where it was put, shown or not.
        Assert.Equal(BarSection.End, Sections.Of(dock.Islands[0]));
        Assert.Same(config.DefaultLayout.Islands, config.IslandsFor("never-seen"));
        Assert.False(config.SelectLayout("D2", "gone"));
        Assert.False(config.SelectLayout("D2", dock.Id), "showing it already changes nothing");
    }

    /// <summary>A display seen for the first time shows the layout for new displays, a shared one, nothing copied.</summary>
    [Fact]
    public void ANewDisplayShowsTheLayoutForNewDisplays()
    {
        var config = new Config();
        config.Repair();
        var work = config.AddLayout("Work", []);
        config.DefaultLayoutId = work.Id;

        Assert.True(config.AddDisplay("D9", "Projector"));

        var screen = config.ScreenFor("D9");
        Assert.True(screen.Enabled);
        Assert.Equal(Edge.Bottom, screen.Edge);
        Assert.Equal(work.Id, screen.ActiveLayoutId);
        Assert.Equal(2, config.Layouts.Count);
        Assert.False(config.AddDisplay("D9", "Projector"), "a display already known is not added again");
    }

    /// <summary>
    /// §636 (the person, 2026-11-09: "one of the layouts always has to be the default one"): a file naming no default, or one
    /// that is gone, is repaired to the first layout, and a new display always gets the default with Karpo on — §344's None
    /// for new displays is gone, so a file still saying <c>"newDisplaysHidden": true</c> reads as a word this build does not
    /// know: the display shows the default. Deleting the default makes the first layout left the default, its displays with it.
    /// </summary>
    [Fact]
    public void ThereIsAlwaysExactlyOneDefaultLayout()
    {
        foreach (var said in (string[])["", "\"defaultLayoutId\": null,", "\"defaultLayoutId\": \"gone\",", "\"defaultLayoutId\": \"\","])
        {
            var read = Configs.Repaired("{ " + said + """
                 "newDisplaysHidden": true,
                  "layouts": [ { "id": "A", "name": "Home", "islands": [] }, { "id": "B", "name": "Work", "islands": [] } ],
                  "screens": [ { "id": "D1", "name": "Dell", "enabled": true, "activeLayoutId": "B" } ] }
                """, out var changed);
            Assert.True(changed, said);
            Assert.Equal(("A", "A"), (read.DefaultLayoutId, read.DefaultLayout.Id));
            Assert.True(read.AddDisplay("D9", "Projector"));
            Assert.Equal((true, "A"), (read.ScreenFor("D9").Enabled, read.ScreenFor("D9").ActiveLayoutId));
            Assert.Equal("B", read.ScreenFor("D1").ActiveLayoutId);
            Assert.DoesNotContain("newDisplaysHidden", read.ToJson(), StringComparison.Ordinal);
        }

        // One that exists is kept as it is.
        var kept = Configs.Repaired("""{ "defaultLayoutId": "B", "layouts": [ { "id": "A", "name": "Home", "islands": [] }, { "id": "B", "name": "Work", "islands": [] } ] }""", out _);
        Assert.Equal("B", kept.DefaultLayoutId);

        // The default deleted: the first left is it, the displays that showed it show that.
        var config = new Config();
        config.Repair();
        var first = config.DefaultLayout;
        var work = config.AddLayout("Work", []);
        var play = config.AddLayout("Play", []);
        config.AddDisplay("D1", "Dell");
        Assert.Equal(first.Id, config.ScreenFor("D1").ActiveLayoutId);
        Assert.True(config.DeleteLayout(first.Id));
        Assert.Equal((work.Id, work.Id), (config.DefaultLayoutId, config.ScreenFor("D1").ActiveLayoutId));
        // Another deleted: the default stays.
        Assert.True(config.DeleteLayout(play.Id));
        Assert.Equal(work.Id, config.DefaultLayoutId);
    }

    /// <summary>
    /// Karpo may be hidden on a display ("None") only while another display attached now still shows
    /// it: never on the last one, and a display not attached does not count. Choosing a layout shows
    /// Karpo there again.
    /// </summary>
    [Fact]
    public void AtLeastOneDisplayShowsKarpo()
    {
        var config = new Config();
        config.Repair();
        config.AddDisplay("D1", "Dell");
        config.AddDisplay("D2", "LG");
        config.AddDisplay("D3", "Away");
        string[] attached = ["D1", "D2"];

        Assert.True(config.CanHide("D1", attached));
        config.EditScreen("D2", "").Enabled = false;
        Assert.False(config.CanHide("D1", attached), "D1 is the last attached display that shows Karpo");
        Assert.True(config.CanHide("D2", attached), "D2 shows nothing already, and D1 does");
        Assert.False(config.CanHide("D1", ["D1"]));

        Assert.True(config.SelectLayout("D2", config.DefaultLayout.Id));
        Assert.True(config.ScreenFor("D2").Enabled);
        Assert.True(config.CanHide("D1", attached));
    }

    /// <summary>
    /// The last layout cannot be deleted; deleting another sends the displays that showed it to the
    /// layout for new displays, and the first left becomes that where it was.
    /// </summary>
    [Fact]
    public void DeletingALayoutSendsItsDisplaysToTheDefault()
    {
        var config = new Config();
        config.Repair();
        var only = config.DefaultLayout.Id;
        Assert.False(config.DeleteLayout(only), "there is always one layout");
        var other = config.AddLayout("Other", []);
        config.AddDisplay("D1", "Dell");
        config.SelectLayout("D1", other.Id);
        config.DefaultLayoutId = other.Id;

        Assert.True(config.DeleteLayout(other.Id));

        Assert.Equal(only, config.DefaultLayoutId);
        Assert.Equal(only, config.ScreenFor("D1").ActiveLayoutId);
    }

    // --- Two apps at once ----------------------------------------------------------------------

    private static string Two() => JsonNode.Parse($$"""
        { "version": {{Config.Shape}},
          "layouts": [
            { "id": "L", "name": "Main", "type": "taskbar", "size": 48, "show": "reserve",
              "islands": [
                { "id": "w", "kind": "widgetsBoard", "section": "start" },
                { "id": "c", "kind": "volume", "section": "end" } ] },
            { "id": "G", "name": "Games", "type": "dock", "islands": [] } ],
          "defaultLayoutId": "L",
          "screens": [ { "id": "D1", "name": "Dell", "enabled": true, "activeLayoutId": "L", "edge": "bottom" } ] }
        """)!.ToJsonString();

    /// <summary>
    /// The settings app makes the layout a Dock while the dock labels its clock: both stay, and the
    /// clock, with its label, keeps its section.
    /// </summary>
    [Fact]
    public void ATypeChangedInOneAppAndAWidgetEditedInTheOtherBothStay()
    {
        var @base = Configs.Repaired(Two());
        var typed = @base.Clone();
        typed.Layout("L")!.Become(LayoutType.Dock);
        var labelled = @base.Clone();
        ((BarItem)labelled.FindIsland(Config.LayoutOwner("L"), "c")!).Volume!.Length = 77;

        var merged = Configs.Repaired(ConfigMerge.Merge(@base.ToJson(), typed.ToJson(), labelled.ToJson()));

        var layout = merged.Layout("L")!;
        Assert.Equal(LayoutType.Dock, layout.Type);
        var clock = layout.Islands.Single(island => island.Id == "c");
        Assert.Equal(BarSection.End, Sections.Of(clock));
        Assert.Equal(77, clock.Volume!.Length);
    }

    /// <summary>
    /// The bar's menu switches the display to another layout while the settings app sizes and renames
    /// layouts, sets how another's bar shows and moves the display to the top: every change stays.
    /// </summary>
    [Fact]
    public void ADisplaysChoiceAndALayoutsSettingsBothStay()
    {
        var @base = Configs.Repaired(Two());
        var switched = @base.Clone();
        switched.SelectLayout("D1", "G");
        var edited = @base.Clone();
        edited.Layout("G")!.Size = 64;
        edited.Layout("G")!.Name = "Play";
        edited.Layout("L")!.Show = EdgeMode.Hover;
        edited.Layout("L")!.Size = 80;
        edited.EditScreen("D1", "").Edge = Edge.Top;

        var merged = Configs.Repaired(ConfigMerge.Merge(@base.ToJson(), switched.ToJson(), edited.ToJson()));

        Assert.Equal("G", merged.ScreenFor("D1").ActiveLayoutId);
        Assert.Equal((64, "Play"), (merged.Layout("G")!.Size, merged.Layout("G")!.Name));
        Assert.Equal((EdgeMode.Hover, 80), (merged.Layout("L")!.Show, merged.Layout("L")!.Size));
        Assert.Equal(Edge.Top, merged.EdgeOfScreen("D1"));
        Assert.Equal(64, merged.EdgesOf("D1").Top.Size);
    }
}
