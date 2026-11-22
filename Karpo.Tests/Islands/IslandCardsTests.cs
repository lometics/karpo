// The cards are one answer for the whole bar, and the look's: in the islands look every part of it
// wears a card — the pins and the app buttons as much as Karpo's own widgets — and in the connected
// and floating looks none does (the "Island backgrounds" switch went, 2026-10-05; §511: the separator, the
// one kind that wore none, is gone, so no kind is excepted). These hold the two things that can quietly
// break that: two cards running into one slab because the parts under them touch, and a look that wears
// cards it should not, or none where it should.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Islands;

public sealed class IslandCardsTests
{
    /// <summary>Every kind that can stand on the bar, in one long row, so neighbours of every sort meet.</summary>
    private static IReadOnlyList<BarKind> OnBar =>
        [.. BarEntries.All.Select(entry => entry.Kind), BarKind.Pin, BarKind.Pin];

    private static Layout Bar(int height, Edge edge, IReadOnlyList<BarKind> kinds, uint dpi = 96, BarStyle style = BarStyle.Islands)
    {
        var config = new Config { Height = height, Style = style };
        List<PartSpec> specs = [.. kinds.Select((kind, i) => new PartSpec(Config.IslandKey(null, $"k{i}"), kind))];
        var area = edge.IsVertical() ? new RECT { right = 1600, bottom = 4000 } : new RECT { right = 4000, bottom = 1600 };
        return Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tray: 3, trayOverflow: true);
    }

    /// <summary>
    /// A card is drawn over a part's whole block, so two parts that touch would draw one slab.
    /// Every neighbouring pair keeps room between them, whatever the kinds are — before this,
    /// Start, Search and Task View sat flush and their three cards read as one.
    /// <para>
    /// A bar below <see cref="Layout.ThinDip"/> is left out: it has no room for gaps and gives its
    /// cards no corners either, so the whole row is one flush band. That is its own look and it is
    /// the same for every kind, which is what the setting promises. <see cref="AThinBarIsOneBand"/>
    /// holds it.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(48)]
    [InlineData(72)]
    public void NeighbouringCardsNeverTouch(int height)
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Left })
        {
            foreach (var dpi in new uint[] { 96, 144, 192 })
            {
                var layout = Bar(height, edge, OnBar, dpi);
                var vertical = edge.IsVertical();
                RectF? last = null;
                var lastKind = BarKind.Start;
                for (var part = 0; part < layout.Parts.Count; part++)
                {
                    if (layout.BlockRect(part) is not { IsEmpty: false } block)
                    {
                        continue;
                    }
                    if (last is { } before)
                    {
                        var gap = vertical ? block.Y - before.Bottom : block.X - before.Right;
                        // §515: every two neighbours of a section a widget's gap apart, whatever their kinds.
                        Assert.True(
                            gap == layout.WidgetGap && gap > 0f,
                            $"{height} DIP at {dpi} DPI on the {edge} edge: {lastKind}'s card runs into {layout.Parts[part].Kind}'s ({gap:0.0} px between them)");
                    }
                    last = block;
                    lastKind = layout.Parts[part].Kind;
                }
            }
        }
    }

    /// <summary>
    /// A dock's panel is one band for everyone: its parts sit a widget's gap apart (§501) with no card and square
    /// corners, so the row reads as one band rather than as some widgets on cards and others not. Uniform is
    /// the promise.
    /// </summary>
    [Theory]
    [InlineData(28, BarStyle.Floating)]
    [InlineData(48, BarStyle.Floating)]
    public void ADocksPanelIsOneBand(int height, BarStyle style)
    {
        var layout = Bar(height, Edge.Bottom, OnBar, style: style);

        Assert.True(layout.Connected);
        Assert.False(layout.Cards);
        Assert.Equal(0f, layout.CardRadius);
        var shown = Enumerable.Range(0, layout.Parts.Count).Where(part => !layout.BlockRect(part).IsEmpty).ToList();
        // Inside each dock: a section's (DockGroupsTests), with the desktop between two.
        var docks = layout.SectionPanels().Where(dock => !dock.IsEmpty).ToList();
        int DockOf(int part) => docks.FindIndex(dock => layout.BlockRect(part).X >= dock.X && layout.BlockRect(part).Right <= dock.Right);
        var checkedPairs = 0;
        for (var at = 1; at < shown.Count; at++)
        {
            if (DockOf(shown[at - 1]) is var dock and >= 0 && dock == DockOf(shown[at]))
            {
                Assert.Equal(layout.BlockRect(shown[at - 1]).Right + layout.WidgetGap, layout.BlockRect(shown[at]).X);
                checkedPairs++;
            }
        }
        Assert.True(checkedPairs > 0);
    }

    /// <summary>
    /// The cards are the look's, and nothing else's: a bar's islands always wear them, a dock's
    /// floating panel never — there is no switch left to say otherwise.
    /// </summary>
    [Theory]
    [InlineData(BarStyle.Islands, true)]
    [InlineData(BarStyle.Floating, false)]
    public void TheCardsAreTheLooks(BarStyle style, bool cards)
    {
        foreach (var height in new[] { 28, 48, 72 })
        {
            Assert.Equal(cards, Bar(height, Edge.Bottom, OnBar, style: style).Cards);
        }
    }

    /// <summary>
    /// §515: every card stands on whole px — its plate, its background and its outline, half a stroke in from its edge, drawn
    /// there (Scene's Rounded.Place) — in every section, along every edge, at every scale Windows offers, in both looks.
    /// </summary>
    [Theory]
    [InlineData(42)]
    [InlineData(48)]
    public void EveryCardStandsOnWholePixels(int height)
    {
        var sections = new[] { BarSection.Start, BarSection.Middle, BarSection.End };
        var kinds = OnBar;
        foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Top, Edge.Left, Edge.Right })
            {
                foreach (var dpi in new uint[] { 96, 120, 144, 168, 192, 240 })
                {
                    var config = new Config { Height = height, Style = style };
                    List<PartSpec> specs = [.. kinds.Select((kind, i) => new PartSpec(Config.IslandKey(null, $"k{i}"), kind) { Section = sections[i % 3] })];
                    var area = edge.IsVertical() ? new RECT { right = 1600, bottom = 4000 } : new RECT { right = 4000, bottom = 1600 };
                    var layout = Layout.ForTests(dpi, area, edge, config, specs, Layout.CellPx(dpi, config), tray: 3, trayOverflow: true);
                    for (var part = 0; part < layout.Parts.Count; part++)
                    {
                        if (layout.Plate(part) is not { IsEmpty: false } plate)
                        {
                            continue;
                        }
                        Assert.True(Whole(plate.X) && Whole(plate.Y) && Whole(plate.Width) && Whole(plate.Height),
                            $"{style} {height} DIP at {dpi} DPI on the {edge} edge: {layout.Parts[part].Kind} in the {specs[part].Section} stands at {plate}");
                    }
                }
            }
        }

        static bool Whole(float value) => MathF.Abs(value - MathF.Round(value)) < 0.001f;
    }
}