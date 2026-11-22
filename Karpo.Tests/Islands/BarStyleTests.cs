// The two looks a bar can wear, and the line between "what this bar has room for" and "what this
// bar looks like".
//
// The looks are no choice any more (the person, 2026-10-08: "docks are Floating; bars are Islands";
// the connected look is gone): a layout's type decides them (TaskbarLayout.Edges, EdgeLayouts.Style).
// Layout.Connected is what a floating dock's panel does inside it — its widgets flush — and
// Layout.Thin is the room and still follows the height. Everything that genuinely follows the
// room — the text, the cell, the grip, the desktop buttons — keeps doing so in both looks.

using Karpo.Dock;
using Karpo.Tests.Core;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class BarStyleTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind) => new(Config.IslandKey(null, id), kind);

    private static readonly PartSpec[] Bar =
    [
        Spec("w", BarKind.WidgetsBoard),
        Spec("c", BarKind.NightLight),
        Spec("b", BarKind.EnergySaver) with { Section = BarSection.End },
    ];

    private static Layout For(int height, BarStyle style, Edge edge = Edge.Top, uint dpi = 96)
    {
        var config = new Config { Height = height, Style = style };
        return Layout.ForTests(dpi, Area(1920, 1080), edge, config, Bar, Layout.CellPx(dpi, config));
    }

    // --- The look is the bar's, at every height --------------------------------------

    /// <summary>
    /// The islands look is a bar's at every height: a card each, a gap between the widgets, a corner — a thin one's too
    /// since §533, which is flush only where its layout spaced it 0 (WidgetSpacingTests).
    /// </summary>
    [Theory]
    [InlineData(28)]
    [InlineData(32)]
    [InlineData(Config.ThinHeight)]
    [InlineData(41)]
    [InlineData(72)]
    public void TheIslandsLookIsTheSameAtEveryHeight(int height)
    {
        var layout = For(height, BarStyle.Islands);

        Assert.False(layout.Connected);
        Assert.True(layout.Cards, "the islands look always wears its cards");
        Assert.True(layout.SectionGap > 0f, $"a {height} DIP islands bar has a section gap of {layout.SectionGap} px");
        Assert.True(layout.CardRadius > 0f, $"a {height} DIP islands bar has a corner of {layout.CardRadius} px");
        // §515: neighbours a widget's gap apart; the wider gap is between sections.
        Assert.Equal(layout.PartAt(0).Right + layout.WidgetGap, layout.PartAt(1).Left);
        Assert.True(layout.WidgetGap > 0f && layout.WidgetGap <= layout.SectionGap);
    }

    /// <summary>A dock floats at any height, and inside its panel the widgets stand flush, with no card each.</summary>
    [Theory]
    [InlineData(28)]
    [InlineData(48)]
    [InlineData(72)]
    public void ADockFloatsAtEveryHeight(int height)
    {
        var layout = For(height, BarStyle.Floating);

        Assert.True(layout.Floating);
        Assert.True(layout.Connected, "inside the panel the widgets are flush");
        Assert.False(layout.Cards);
        Assert.Equal(layout.WidgetGap, layout.PartAt(1).Left - layout.PartAt(0).Right);
    }

    /// <summary>A thin bar keeps its islands: the height does not overrule the look.</summary>
    [Fact]
    public void AThinBarKeepsItsIslands()
    {
        var layout = For(28, BarStyle.Islands);

        Assert.True(layout.Thin, "28 DIP is still a thin bar");
        Assert.False(layout.Connected, "but the look is the bar's");
    }

    // --- What follows the room, and must keep following it --------------------------

    /// <summary>
    /// The room a bar has is its height and nothing else — §688 a dock's its height less the room it floats over (the strip
    /// it keeps its height, as a bar's). The other look must not move a single measurement that is about room: the cell, the
    /// icon, the thickness and the grip are the same on both.
    /// </summary>
    [Theory]
    [InlineData(28)]
    [InlineData(72)]
    public void TheLookMovesNothingThatBelongsToTheRoom(int height)
    {
        var floating = For(height, BarStyle.Floating);
        var islands = For(Math.Max(Config.MinHeight, height - (int)(floating.Lift / floating.Scale)), BarStyle.Islands);

        Assert.Equal(islands.Thin, floating.Thin);
        Assert.Equal(islands.Thickness, floating.Thickness);
        // The cell is the look's own: an islands card keeps room off the bar's edge that grows with
        // it (Layout.CardInsetPx), the dock's widgets keep the 2 DIPs of breath inside the panel; a
        // flush bar's card is the bar's whole thickness.
        Assert.True(
            islands.Flush ? islands.SmallWell == islands.Thickness : islands.SmallWell <= floating.SmallWell,
            "an islands card is not taller than a dock's cell, but where it is flush");
        // §688: the icon the size set's, a bar's of that size.
        Assert.Equal(For(height, BarStyle.Islands).Icon, floating.Icon);
        Assert.Equal(islands.GripRect.Height, floating.GripRect.Height);
    }

    // --- The looks are the types' (2026-10-08) -----------------------------------------

    /// <summary>
    /// A config made in code, with no edge to lay a bar out along, wears a bar's look at every height:
    /// islands. Nothing writes a look down.
    /// </summary>
    [Theory]
    [InlineData(28)]
    [InlineData(Config.ThinHeight)]
    [InlineData(48)]
    [InlineData(72)]
    public void NoEdgeIsTheIslandsLookAtEveryHeight(int height)
    {
        var config = new Config { Height = height };
        Assert.Null(config.Style);
        Assert.Equal(BarStyle.Islands, config.StyleOrDefault);

        _ = config.Repair();

        Assert.Null(config.Style);
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Top, config, Bar, Layout.CellPx(96, config));
        Assert.True(layout.Cards, $"a {height} DIP bar wears islands");
    }

    /// <summary>
    /// Each type's bars wear its look along their edges (TaskbarLayout.Edges): a Taskbar's and a Top
    /// and bottom's are islands, a Dock floats; laid out along an edge (Config.WithEdge), the bar
    /// wears it.
    /// </summary>
    [Theory]
    [InlineData(LayoutType.Taskbar, Edge.Bottom, BarStyle.Islands)]
    [InlineData(LayoutType.Dock, Edge.Top, BarStyle.Floating)]
    public void EachTypesBarsWearItsLook(LayoutType type, Edge edge, BarStyle style)
    {
        var config = new Config();
        var layout = new TaskbarLayout { Type = type, Size = 56 };

        var bar = layout.Edges(edge)[edge];
        Assert.Equal(style, bar.Style);
        var sized = config.WithEdge(bar);
        Assert.Equal(style, sized.StyleOrDefault);
        Assert.Equal(56, sized.Height);
        Assert.Null(config.Style);
    }

    /// <summary>The look is never in the file: neither the config's nor a layout's.</summary>
    [Fact]
    public void TheLookIsNeverInTheFile()
    {
        var config = new Config();
        _ = config.Repair();
        config.AddDisplay("D1", "Dell");
        _ = config.Layouts[0].Become(LayoutType.Dock);

        var json = config.ToJson();
        // Neither the config's nor a layout's; a folder's line writes its own "style", which is the
        // widget's (§209), not the bar's look.
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("style", out _));
        Assert.All(document.RootElement.GetProperty("layouts").EnumerateArray(), layout => Assert.False(layout.TryGetProperty("style", out _)));
        Assert.DoesNotContain("connected", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Idempotent, like every repair here: the first read of a new config makes the layout a new
    /// install gets, and the second has nothing to write.
    /// </summary>
    [Fact]
    public void TheSecondReadHasNothingToWrite()
    {
        var config = new Config { Height = 48 };

        Assert.True(config.Repair(), "a new config gets its first layout");
        Assert.False(config.Repair(), "the second read has nothing left to do");
    }

    /// <summary>
    /// Either side of <see cref="Config.ThinHeight"/> the room changes and the look does not: the
    /// height is no copy of the look's rule, so nothing here can drift apart.
    /// </summary>
    [Theory]
    [InlineData(Config.ThinHeight - 1)]
    [InlineData(Config.ThinHeight)]
    [InlineData(Config.ThinHeight + 1)]
    public void AtTheThinBoundaryOnlyTheRoomChanges(int height)
    {
        var config = new Config { Height = height };

        _ = config.Repair();
        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Top, config, Bar, Layout.CellPx(96, config));

        Assert.True(layout.Cards);
        Assert.Equal(config.BarThickness < Config.ThinHeight, layout.Thin);
    }

    /// <summary>
    /// A new install's bar (Sections.DefaultLayout, Windows' taskbar entry by entry) is a Taskbar along
    /// the bottom at Windows' 48 DIP, wearing islands: a card each, gaps between them.
    /// </summary>
    [Fact]
    public void ANewInstallsBarWearsIslands()
    {
        var config = new Config();
        _ = config.Repair();
        var first = config.DefaultLayout;
        Assert.Equal(LayoutType.Taskbar, first.Type);
        Assert.Equal(EdgeMode.Reserve, first.Show);
        Assert.Equal(48, first.Size);
        var sized = config.WithEdge(first.Edges(Edge.Bottom)[Edge.Bottom]);
        var bar = Sections.DefaultLayout(pins: [], inputs: new InputHardware(Pen: false, Touch: false, Slate: false), windows: WindowsTaskbar.OutOfTheBox);
        var specs = bar.Select(item => new PartSpec(Config.IslandKey(null, item.Id), item.Kind) with { Section = item.Section }).ToList();

        var layout = Layout.ForTests(96, Area(1920, 1080), Edge.Bottom, sized, specs, Layout.CellPx(96, sized));

        Assert.True(layout.Cards);
        Assert.False(layout.Connected);
        Assert.False(layout.Thin, "48 DIP is a bar with room; the look is not the room's");
        Assert.True(layout.SectionGap > 0f);
    }

    /// <summary>
    /// A graph in a dock's panel runs into its neighbour square-cornered, as the panel holds its
    /// widgets flush; on an islands bar it takes its card's.
    /// </summary>
    [Theory]
    [InlineData(BarStyle.Floating, true)]
    [InlineData(BarStyle.Islands, false)]
    public void ABarGraphsCornersFollowTheLook(BarStyle style, bool square)
    {
        var layout = For(48, style);
        var look = new Look(Glass: false, Dark: true, Accent: default);

        var islandStyle = layout.StyleFor(0, look);

        Assert.Equal(square, islandStyle.Square);
        Assert.Equal(square, Karpo.Dock.Graphs.GraphBackdrop.BarRadius(islandStyle) == 0f);
    }

    // §123's flush look on a thin islands bar is the layout's choice since §533 (Spacing 0, at any height): WidgetSpacingTests.
}
