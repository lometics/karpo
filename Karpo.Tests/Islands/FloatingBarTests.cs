// The floating look (BarStyle.Floating): each of the three sections a dock of its own. The person,
// 2026-10-05: "additionally a style where each of the three sections is displayed floating / on
// their own. like a floating dock."
//
// What the layout promises, on every edge and both scales:
//   - a panel per section that holds a widget, none for an empty one (Layout.SectionPanels);
//   - each panel the bar's thickness across, Layout.Lift (8 DIPs) off the screen's edge, the first
//     and the last 8 DIPs off the screen's ends, and 8 at least between two panels;
//   - inside a panel the widgets stand as on a connected bar — square, no card, a widget's gap apart (§501) — with room
//     in from the panel's ends;
//   - the strip under and between the panels is still the bar's: windows keep out of it
//     (Layout.Reserve), a press between two panels is the bar's own (its menu opens there), and a
//     press between a widget and the screen's edge is that widget's;
//   - in edit mode the hatched free room lies between the panels and on none of them.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class FloatingBarTests
{
    private static RECT Area(int width, int height) => new() { left = 0, top = 0, right = width, bottom = height };

    private static PartSpec Spec(string id, BarKind kind, BarSection section) => new(Config.IslandKey(null, id), kind) { Section = section };

    /// <summary>Something in every section: the weather at the start, two widgets in the middle, the battery and the clock at the end.</summary>
    private static readonly PartSpec[] Full =
    [
        Spec("w", BarKind.WidgetsBoard, BarSection.Start),
        Spec("m1", BarKind.TouchKeyboard, BarSection.Middle),
        Spec("m2", BarKind.Volume, BarSection.Middle),
        Spec("b", BarKind.EnergySaver, BarSection.End),
        Spec("c", BarKind.NightLight, BarSection.End),
    ];

    /// <summary>The middle left empty.</summary>
    private static readonly PartSpec[] NoMiddle =
    [
        Spec("w", BarKind.WidgetsBoard, BarSection.Start),
        Spec("c", BarKind.NightLight, BarSection.End),
    ];

    private static readonly Edge[] Edges = [Edge.Bottom, Edge.Top, Edge.Left, Edge.Right];

    private static Layout For(IReadOnlyList<PartSpec> specs, Edge edge = Edge.Bottom, uint dpi = 96, int height = 48, BarStyle style = BarStyle.Floating, bool editing = false)
    {
        var config = new Config { Height = height, Style = style };
        var area = edge.IsVertical() ? Area(1080 * (int)dpi / 96, 1920 * (int)dpi / 96) : Area(1920 * (int)dpi / 96, 1080 * (int)dpi / 96);
        return Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), editing: editing);
    }

    private static float Px(uint dpi, float dip) => MathF.Round(dip * dpi / 96f, MidpointRounding.AwayFromZero);

    private static float From(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float To(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    /// <summary>How far <paramref name="rect"/> stands off the screen's edge, across the bar.</summary>
    private static float OffTheEdge(Layout layout, RectF rect) => layout.Edge switch
    {
        Edge.Top => rect.Y,
        Edge.Left => rect.X,
        Edge.Right => layout.Width - rect.Right,
        _ => layout.Height - rect.Bottom,
    };

    private static float Across(Layout layout, RectF rect) => layout.Vertical ? rect.Width : rect.Height;

    // --- The panels -----------------------------------------------------------------------

    /// <summary>
    /// A panel per section, each round its own widgets and no other's, the bar's thickness across and
    /// 8 DIPs off the screen's edge, on every edge.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Top, 96u)]
    [InlineData(Edge.Left, 96u)]
    [InlineData(Edge.Right, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Left, 144u)]
    public void EachSectionIsAPanelOffTheEdge(Edge edge, uint dpi)
    {
        var layout = For(Full, edge, dpi);
        var panels = layout.SectionPanels();

        Assert.True(layout.Floating);
        Assert.Equal(3, panels.Count);
        Assert.Equal(Px(dpi, 8), layout.Lift);
        for (var section = 0; section < 3; section++)
        {
            var panel = panels[section];
            Assert.False(panel.IsEmpty, $"{edge}: section {section} has no panel");
            Assert.Equal(layout.Thickness, Across(layout, panel));
            Assert.Equal(layout.Lift, OffTheEdge(layout, panel));
            for (var part = 0; part < Full.Length; part++)
            {
                var block = layout.BlockRect(part);
                var inside = From(layout, block) >= From(layout, panel) && To(layout, block) <= To(layout, panel);
                Assert.True(inside == ((int)Full[part].Section == section), $"{edge} at {dpi}: {Full[part].Kind} and the panel of section {section}");
            }
        }
    }

    /// <summary>An empty section draws no panel, at rest and in edit mode, where its "+" stands on its own.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnEmptySectionHasNoPanel(bool editing)
    {
        foreach (var edge in Edges)
        {
            var panels = For(NoMiddle, edge, editing: editing).SectionPanels();

            Assert.False(panels[(int)BarSection.Start].IsEmpty);
            Assert.True(panels[(int)BarSection.Middle].IsEmpty, $"{edge}: the empty middle has a panel");
            Assert.False(panels[(int)BarSection.End].IsEmpty);
        }
        Assert.All(For([], editing: editing).SectionPanels(), panel => Assert.True(panel.IsEmpty));
    }

    /// <summary>
    /// The first panel and the last keep 8 DIPs off the screen's ends, two panels 8 at least between
    /// them, and the middle's is centred on the bar.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Right, 96u)]
    [InlineData(Edge.Top, 144u)]
    [InlineData(Edge.Left, 144u)]
    public void ThePanelsKeepOffTheEndsAndApart(Edge edge, uint dpi)
    {
        var layout = For(Full, edge, dpi);
        var panels = layout.SectionPanels();
        var margin = Px(dpi, 8);

        Assert.Equal(margin, From(layout, panels[0]));
        Assert.Equal(layout.Length - margin, To(layout, panels[2]));
        Assert.True(From(layout, panels[1]) - To(layout, panels[0]) >= margin);
        Assert.True(From(layout, panels[2]) - To(layout, panels[1]) >= margin);
        var middle = (From(layout, panels[1]) + To(layout, panels[1])) / 2f;
        Assert.Equal(layout.Length / 2f, middle, 1f);
    }

    /// <summary>
    /// Inside a panel the widgets stand as on a connected bar — square, no card each, a widget's gap
    /// apart (§501) — and
    /// keep room in from its ends: the room across the bar between a cell and the panel's edge, 4
    /// DIPs at least.
    /// </summary>
    [Theory]
    [InlineData(96u, 48)]
    [InlineData(144u, 48)]
    [InlineData(96u, 28)]
    [InlineData(96u, 72)]
    public void InsideAPanelTheWidgetsStandAWidgetsGapApart(uint dpi, int height)
    {
        var layout = For(Full, dpi: dpi, height: height);
        var panels = layout.SectionPanels();

        Assert.True(layout.Connected);
        Assert.False(layout.Cards);
        Assert.Equal(0f, layout.CardRadius);
        Assert.Equal(Px(dpi, 8), layout.PanelRadius);
        // The middle's two and the end's two a widget's gap apart.
        Assert.True(layout.WidgetGap > 0f);
        Assert.Equal(layout.BlockRect(1).Right + layout.WidgetGap, layout.BlockRect(2).X);
        Assert.Equal(layout.BlockRect(3).Right + layout.WidgetGap, layout.BlockRect(4).X);
        var room = MathF.Max(Px(dpi, 4), MathF.Floor((layout.Thickness - layout.SmallWell) / 2f));
        Assert.Equal(panels[0].X + room, layout.BlockRect(0).X);
        Assert.Equal(panels[1].X + room, layout.BlockRect(1).X);
        Assert.Equal(panels[2].Right - room, layout.BlockRect(4).Right);
    }

    /// <summary>
    /// §688 (the person, 2026-11-12: "the taskbar and dock even though both are set to 42px size take up a different amount of
    /// space, the windows move"): a dock of a size is a bar of that size less the room it floats over — its panel that much
    /// thinner, its icon the same.
    /// </summary>
    [Fact]
    public void FloatingTakesItsRoomOutOfItsSize()
    {
        var floating = For(Full);
        var islands = For(Full, style: BarStyle.Islands);

        Assert.Equal(islands.Thickness - floating.Lift, floating.Thickness);
        Assert.Equal(islands.Icon, floating.Icon);
    }

    // --- The strip it keeps --------------------------------------------------------------------

    /// <summary>
    /// Windows keep out of the panels and the room under them: the strip held is the thickness and the lift — §688 together
    /// the size, the same strip a bar of that size that does not float holds, so nothing moves as the one becomes the other.
    /// </summary>
    [Fact]
    public void TheStripKeptIsTheThicknessAndTheLift()
    {
        foreach (var dpi in new uint[] { 96, 144 })
        {
            var floating = For(Full, dpi: dpi);
            var islands = For(Full, dpi: dpi, style: BarStyle.Islands);

            Assert.Equal(floating.Thickness + Px(dpi, 8), floating.Reserve);
            Assert.Equal(islands.Thickness, islands.Reserve);
            Assert.Equal(islands.Reserve, floating.Reserve);
            Assert.Equal(0f, islands.Lift);
            Assert.Equal(islands.Height, floating.Height);
        }
    }

    /// <summary>
    /// The reveal strip of a hidden bar is at the screen's edge, where the pointer goes, and a hidden
    /// bar slides past the edge with the room under it.
    /// </summary>
    [Fact]
    public void AutohideStillReachesTheEdge()
    {
        foreach (var edge in Edges)
        {
            var layout = For(Full, edge);

            Assert.Equal(0f, OffTheEdge(layout, layout.HotStrip));
            var slide = layout.HiddenOffset();
            Assert.True(MathF.Abs(slide.X) + MathF.Abs(slide.Y) > layout.Reserve, $"{edge}: a hidden bar would still show");
        }
    }

    /// <summary>The bar's strip — what the window's region and the menu's anchor read — is the panels' band, lifted with them.</summary>
    [Fact]
    public void TheStripIsLiftedWithThePanels()
    {
        foreach (var edge in Edges)
        {
            var layout = For(Full, edge);

            Assert.Equal(layout.Lift, OffTheEdge(layout, layout.PanelRect));
            Assert.Equal(layout.Thickness, Across(layout, layout.PanelRect));
            Assert.Equal(layout.Length, layout.Vertical ? layout.PanelRect.Height : layout.PanelRect.Width);
        }
    }

    // --- Where a press lands -------------------------------------------------------------------

    /// <summary>
    /// Between two panels the desktop shows, and a press there is the bar's own, as anywhere else
    /// the bar has nothing: its menu opens there. So is the room past the first and the last panel.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void APressBetweenThePanelsIsTheBars(Edge edge)
    {
        var layout = For(Full, edge);
        var panels = layout.SectionPanels();
        var across = layout.Vertical ? panels[0].X + panels[0].Width / 2f : panels[0].Y + panels[0].Height / 2f;
        (float X, float Y) At(float along) => layout.Vertical ? (across, along) : (along, across);

        foreach (var along in new[]
        {
            (To(layout, panels[0]) + From(layout, panels[1])) / 2f,
            (To(layout, panels[1]) + From(layout, panels[2])) / 2f,
            From(layout, panels[0]) / 2f,
            layout.Length - 1f,
        })
        {
            var (x, y) = At(along);
            Assert.Equal(Hit.Panel, layout.HitTest(x, y));
        }
    }

    /// <summary>
    /// A press on a widget is the widget's; and between the widget and the screen's edge too, where
    /// the panel has left the desktop showing: the edge of the screen stays a target, as Windows'
    /// taskbar's does. Past the bar's inner side the pointer has left the bar.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom)]
    [InlineData(Edge.Top)]
    [InlineData(Edge.Left)]
    [InlineData(Edge.Right)]
    public void APressOnAWidgetOrUnderItIsTheWidgets(Edge edge)
    {
        var layout = For(Full, edge);
        var block = layout.BlockRect(0);
        var along = layout.Vertical ? block.Y + block.Height / 2f : block.X + block.Width / 2f;
        var panel = layout.SectionPanels()[0];

        var onIt = layout.HitTest(block.X + block.Width / 2f, block.Y + block.Height / 2f);
        Assert.NotNull(onIt);
        Assert.Equal(0, layout.PartOf(onIt.Value));

        // One pixel from the screen's edge, in the room under the panel.
        var (edgeX, edgeY) = edge switch
        {
            Edge.Top => (along, 0f),
            Edge.Left => (0f, along),
            Edge.Right => (layout.Width - 1f, along),
            _ => (along, layout.Height - 1f),
        };
        Assert.True(OffTheEdge(layout, panel) > 0f);
        var under = layout.HitTest(edgeX, edgeY);
        Assert.NotNull(under);
        Assert.Equal(0, layout.PartOf(under.Value));

        // Past the panel's inner side, in the tooltips' room.
        var (pastX, pastY) = edge switch
        {
            Edge.Top => (along, panel.Bottom + 2f),
            Edge.Left => (panel.Right + 2f, along),
            Edge.Right => (panel.X - 3f, along),
            _ => (along, panel.Y - 3f),
        };
        Assert.Null(layout.HitTest(pastX, pastY));
    }

    // --- Edit mode -----------------------------------------------------------------------------

    /// <summary>
    /// In edit mode an empty section's room stands on its own, between the panels and on none of
    /// them, clear of the screen's ends; a section with widgets has its panel and no empty room.
    /// </summary>
    [Theory]
    [InlineData(Edge.Bottom, 96u)]
    [InlineData(Edge.Left, 96u)]
    [InlineData(Edge.Bottom, 144u)]
    [InlineData(Edge.Right, 144u)]
    public void AnEmptySectionsRoomIsOffThePanels(Edge edge, uint dpi)
    {
        var layout = For(NoMiddle, edge, dpi, editing: true);
        var panels = layout.SectionPanels();
        var room = Assert.Single(layout.EmptyRooms());

        Assert.True(panels[(int)BarSection.Middle].IsEmpty);
        Assert.True(From(layout, room) >= Px(dpi, 8) && To(layout, room) <= layout.Length - Px(dpi, 8), $"{edge}: the room off the ends");
        foreach (var panel in panels.Where(panel => !panel.IsEmpty))
        {
            Assert.True(To(layout, room) <= From(layout, panel) || From(layout, room) >= To(layout, panel), $"{edge}: the room on a panel");
        }
        Assert.Empty(For(Full, edge, dpi, editing: true).EmptyRooms());
    }

    /// <summary>
    /// While a block is carried the panels follow the sections: the one it joins grows for it, and
    /// the one it leaves with nothing else in it has no panel.
    /// </summary>
    [Fact]
    public void ThePanelsFollowACarriedBlock()
    {
        var layout = For(NoMiddle, editing: true);
        var slot = new BarSlot(BarSection.Middle, 1);

        var carried = layout.SectionPanels(1, slot);

        Assert.True(carried[(int)BarSection.End].IsEmpty, "the end has nothing left in it");
        Assert.False(carried[(int)BarSection.Middle].IsEmpty, "the middle holds the clock now");
        Assert.Equal(layout.SectionPanels()[(int)BarSection.Start], carried[(int)BarSection.Start]);
        // The empty room follows the same sections: the middle holds the clock, the end is empty now.
        var room = Assert.Single(layout.EmptyRooms(1, slot));
        foreach (var panel in carried.Where(panel => !panel.IsEmpty))
        {
            Assert.True(room.Right <= panel.X || room.X >= panel.Right);
        }
    }

    /// <summary>A bar that does not float has no panels, and its widgets stand where they always did.</summary>
    [Theory]
    [InlineData(BarStyle.Islands)]
    public void ABarThatDoesNotFloatHasNoPanels(BarStyle style)
    {
        var layout = For(Full, style: style, editing: true);

        Assert.False(layout.Floating);
        Assert.Equal(0f, layout.Lift);
        Assert.All(layout.SectionPanels(), panel => Assert.True(panel.IsEmpty));
        // The cards' own room off the screen's start (docs 56), and no panel's.
        Assert.Equal(layout.EndRoom, From(layout, layout.BlockRect(0)));
    }
}
