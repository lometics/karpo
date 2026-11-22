// A layout may hold the same kind more than once: two trays or two Apps widgets on one bar (the
// person, 2026-10-09: "if i place the tray icons widget twice they only show once. (in the first
// one)", and the same of the apps). Each tray is the whole tray — its own widget's icons in its own
// order (§347), its own chevron, its own place for a click, a drop and the keyboard —, and each Apps
// widget shows every open app. Measured on a real layout without a Gfx (Layout.ForTests): these are numbers.

using System.Numerics;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Sizing;

public sealed class TwiceOverTests
{
    private const uint Dpi = 96;

    private static Config Bar() => new() { Height = 48, Style = BarStyle.Islands };

    private static PartSpec Spec(string id, BarKind kind, BarSection section = BarSection.Start) =>
        new(Config.IslandKey(null, id), kind) { Section = section };

    private static Layout Lay(IReadOnlyList<PartSpec> specs, int length = 2560, int tray = 0, bool chevron = false, IReadOnlyList<int>? counts = null,
        IReadOnlyDictionary<string, (int, bool)>? trays = null)
    {
        var config = Bar();
        var area = new RECT { left = 0, top = 0, right = length, bottom = 1440 };
        return Layout.ForTests(Dpi, area, Edge.Bottom, config, specs, Layout.CellPx(Dpi, config), tray: tray, trayOverflow: chevron, tileCounts: counts, trays: trays);
    }

    /// <summary>Two trays in the end section, side by side, the first <paramref name="cells"/> cells wide where set.</summary>
    private static List<PartSpec> TwoTrays(int cells = 0) =>
    [
        Spec("start", BarKind.Start),
        cells > 0 ? new PartSpec(Config.IslandKey(null, "tray"), BarKind.Tray, cells) { Section = BarSection.End } : Spec("tray", BarKind.Tray, BarSection.End),
        Spec("tray-2", BarKind.Tray, BarSection.End),
    ];

    private static Vector2 Middle(RectF rect) => new(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

    private static POINT Screen(Layout layout, RectF rect) =>
        new() { x = layout.X + (int)Middle(rect).X, y = layout.Y + (int)Middle(rect).Y };

    // --- The tray, twice ---------------------------------------------------------------------------

    /// <summary>
    /// Both trays show every icon, each in its own room: an icon of the second is drawn, lit and
    /// pressed there, and a press on it is that tray's. It was the first alone that drew them.
    /// </summary>
    [Fact]
    public void EachTrayOfABarShowsTheWholeTray()
    {
        var layout = Lay(TwoTrays(), tray: 4, chevron: true);
        var trays = layout.TrayParts;

        Assert.Equal(2, trays.Count);
        Assert.All(trays, part => Assert.Equal(4, layout.PartAt(part).Count));
        Assert.All(trays, part => Assert.True(layout.TrayChevronOf(part) > 0f));
        var first = Enumerable.Range(0, 4).Select(icon => layout.ItemRect(Hit.Tray(icon).In(trays[0]))).ToList();
        var second = Enumerable.Range(0, 4).Select(icon => layout.ItemRect(Hit.Tray(icon).In(trays[1]))).ToList();
        Assert.All(first.Concat(second), rect => Assert.False(rect.IsEmpty));
        // The second stands after the first, nothing of it over the first's.
        Assert.True(second[0].X >= first[^1].Right - 0.5f);
        for (var icon = 0; icon < 4; icon++)
        {
            var at = Middle(second[icon]);
            Assert.Equal(Hit.Tray(icon).In(trays[1]), layout.HitTest(at.X, at.Y));
        }
        var chevron = Middle(layout.ItemRect(Hit.TrayOverflow.In(trays[1])));
        Assert.Equal(Hit.TrayOverflow.In(trays[1]), layout.HitTest(chevron.X, chevron.Y));
        // An icon's picture in each.
        Assert.False(layout.TrayIconRect(3, trays[1]).IsEmpty);
        Assert.NotEqual(layout.TrayIconRect(3, trays[0]), layout.TrayIconRect(3, trays[1]));
    }

    /// <summary>
    /// Each tray has the chevron it needs: one set to three cells with eight icons to show puts
    /// what its room holds on the bar and the rest behind its chevron; the other, of no set size,
    /// shows all eight and needs none.
    /// </summary>
    [Fact]
    public void EachTrayHasItsOwnChevron()
    {
        var layout = Lay(TwoTrays(cells: 3), tray: 8);
        var (narrow, wide) = (layout.TrayParts[0], layout.TrayParts[1]);

        Assert.True(layout.TrayOverflow);
        Assert.True(layout.TrayChevronOf(narrow) > 0f);
        Assert.Equal(0f, layout.TrayChevronOf(wide));
        Assert.Equal((int)MathF.Floor(layout.PartAt(narrow).Width / layout.TrayStep) - 1, layout.PartAt(narrow).Count);
        Assert.InRange(layout.PartAt(narrow).Count, 1, 7);
        Assert.Equal(8, layout.PartAt(wide).Count);
        Assert.True(layout.ItemRect(Hit.TrayOverflow.In(wide)).IsEmpty);
        // The wide one's first cell is its first icon, no chevron before it.
        var first = Middle(layout.ItemRect(Hit.Tray(0).In(wide)));
        Assert.Equal(layout.PartAt(wide).Left, layout.Vertical ? layout.ItemRect(Hit.Tray(0).In(wide)).Y : layout.ItemRect(Hit.Tray(0).In(wide)).X, 1f);
        Assert.Equal(Hit.Tray(0).In(wide), layout.HitTest(first.X, first.Y));
    }

    /// <summary>The overflow opens from the chevron clicked; a hit of no tray (or none) is the first tray's.</summary>
    [Fact]
    public void TheOverflowOpensFromTheTrayClicked()
    {
        var layout = Lay([.. TwoTrays(), Spec("battery", BarKind.EnergySaver, BarSection.End)], tray: 3, chevron: true);
        var (first, second) = (layout.TrayParts[0], layout.TrayParts[1]);

        Assert.Equal(Hit.TrayOverflow.In(second), Karpo.Dock.Dock.ChevronOf(layout, Hit.Tray(2).In(second)));
        Assert.Equal(Hit.TrayOverflow.In(second), Karpo.Dock.Dock.ChevronOf(layout, Hit.TrayOverflow.In(second)));
        Assert.Equal(Hit.TrayOverflow.In(first), Karpo.Dock.Dock.ChevronOf(layout, null));
        Assert.Equal(Hit.TrayOverflow.In(first), Karpo.Dock.Dock.ChevronOf(layout, Hit.TrayOverflow));
        // Kept from a layout where that part was a tray, and is the battery now.
        Assert.Equal(Hit.TrayOverflow.In(first), Karpo.Dock.Dock.ChevronOf(layout, Hit.TrayOverflow.In(layout.PartIndex(Config.IslandKey(null, "battery")))));
    }

    /// <summary>
    /// §347: a dragged icon lands only in the tray it was taken from (Dock.TrayTargetAt reads that one):
    /// over its own icons and its own chevron, and the other tray's room is no drop for it — though its
    /// own takes a new icon a cell past its last (TrayDrags.OnBar).
    /// </summary>
    [Fact]
    public void ADropReadsOnlyItsOwnTray()
    {
        var layout = Lay(TwoTrays(), tray: 3, chevron: true);
        var (first, second) = (layout.TrayParts[0], layout.TrayParts[1]);
        TrayDrop? Over(Hit hit, int part, int heldAt = -1) => TrayDrags.OnLayout(layout, Screen(layout, layout.ItemRect(hit)), 3, 2, heldAt, part: part);

        // From the overflow, onto the second tray's first icon.
        Assert.Equal(TrayArea.Shown, Over(Hit.Tray(0).In(second), second)?.Area);
        // Onto the first tray's second icon, held from the bar.
        Assert.Equal(new TrayDrop(TrayArea.Shown, 1), Over(Hit.Tray(1).In(first), first, heldAt: 0));
        // Onto the second's chevron: hidden in the second.
        Assert.Equal(TrayArea.Overflow, Over(Hit.TrayOverflow.In(second), second, heldAt: 0)?.Area);
        // The first's icon over the second's: nothing; nor over Start.
        Assert.Null(Over(Hit.Tray(1).In(second), first, heldAt: 0));
        Assert.Null(Over(Hit.TrayOverflow.In(second), first, heldAt: 0));
        Assert.Null(Over(Hit.Start, first));
    }

    /// <summary>
    /// §347: two trays of one bar whose widgets show the icons each its own way: one with five on the bar
    /// and none behind its chevron, the other two and its chevron — each laid out, drawn, reached by the
    /// keys and a screen reader as its own.
    /// </summary>
    [Fact]
    public void TwoTraysOfTheirOwnStatesLayOutApart()
    {
        var specs = TwoTrays();
        var layout = Lay(specs, trays: new Dictionary<string, (int, bool)> { [specs[1].Key] = (5, false), [specs[2].Key] = (2, true) });
        var (first, second) = (layout.TrayParts[0], layout.TrayParts[1]);

        Assert.Equal(5, layout.PartAt(first).Count);
        Assert.Equal(0f, layout.TrayChevronOf(first));
        Assert.Equal(2, layout.PartAt(second).Count);
        Assert.True(layout.TrayChevronOf(second) > 0f);
        Assert.True(layout.PartAt(first).Width > layout.PartAt(second).Width);
        Assert.True(layout.ItemRect(Hit.Tray(2).In(second)).IsEmpty);
        Assert.False(layout.ItemRect(Hit.Tray(4).In(first)).IsEmpty);
        var stops = KeyStops.Of(layout, KeyRegion.Tray, 0);
        // The first's five, the second's chevron and two: the first has no chevron to stop at.
        Assert.Equal(5 + 1 + 2, stops.Count);
        Assert.Equal(2, stops.Count(hit => hit.Kind == HitKind.Tray && hit.Part == second));
        Assert.Equal(2, BarUia.Entries(layout, 0).Count(hit => hit.Kind == HitKind.Tray && hit.Part == second));
        // Carried off and back (edit mode), each keeps its own.
        var left = layout.Without([first]);
        Assert.Equal(2, left.PartAt(left.PartIndex(specs[2].Key)).Count);
    }

    /// <summary>Taking one of two trays off the bar (edit mode carrying it) leaves the other its icons.</summary>
    [Fact]
    public void TakingOneTrayOffLeavesTheOtherItsIcons()
    {
        var layout = Lay(TwoTrays(), tray: 4);
        var left = layout.Without([layout.TrayParts[0]]);

        Assert.Equal(4, left.PartAt(left.PartIndex(Config.IslandKey(null, "tray-2"))).Count);
    }

    /// <summary>
    /// The keyboard stops at every icon and chevron of both trays, along the bar, and a screen reader
    /// finds each with an id of its own; a bar with one tray keeps the bare hits and ids it had.
    /// </summary>
    [Fact]
    public void TheKeyboardAndAScreenReaderReachBothTrays()
    {
        var layout = Lay(TwoTrays(), tray: 3, chevron: true);

        var stops = KeyStops.Of(layout, KeyRegion.Tray, 0);
        Assert.Equal(8, stops.Count);
        Assert.Equal(8, stops.Distinct().Count());
        Assert.All(stops.Zip(stops.Skip(1)), pair => Assert.True(layout.ItemRect(pair.First).X < layout.ItemRect(pair.Second).X));
        var entries = BarUia.Entries(layout, 0).Where(hit => hit.Kind is HitKind.Tray or HitKind.TrayOverflow).ToList();
        Assert.Equal(stops.OrderBy(hit => layout.ItemRect(hit).X), entries);
        Assert.Equal(8, entries.Select(BarUia.AutomationIdOf).Distinct().Count());

        var one = Lay([Spec("start", BarKind.Start), Spec("tray", BarKind.Tray, BarSection.End)], tray: 3, chevron: true);
        Assert.Equal([Hit.TrayOverflow, Hit.Tray(0), Hit.Tray(1), Hit.Tray(2)], KeyStops.Of(one, KeyRegion.Tray, 0));
        Assert.Contains("Tray0", BarUia.Entries(one, 0).Select(BarUia.AutomationIdOf));
    }

    // --- The open apps, twice ----------------------------------------------------------------------

    /// <summary>
    /// Each button stands in the part it names: each Apps widget its own copies of the open apps, a
    /// pin its own. One that names none is the first Apps widget's — the second used to be given none
    /// at all, which is why the open apps showed in the first alone.
    /// </summary>
    [Fact]
    public void EachButtonStandsInThePartItNames()
    {
        List<PartSpec> parts = [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps), Spec("pin", BarKind.Pin), Spec("apps-2", BarKind.Apps)];
        var (apps, pin, apps2) = (parts[1].Key, parts[2].Key, parts[3].Key);

        Assert.Equal([0, 2, 1, 2], Layout.TileCountsOf(parts, [apps, apps, pin, apps2, apps2]));
        Assert.Equal([0, 2, 1, 0], Layout.TileCountsOf(parts, [null, null, pin]));
        // No Apps widget: an open app has nowhere to stand.
        Assert.Equal([0, 1], Layout.TileCountsOf([parts[0], parts[2]], [pin, null]));
    }

    /// <summary>Both Apps widgets show the open apps, each button lit and pressed in its own widget.</summary>
    [Fact]
    public void EachAppsWidgetShowsTheOpenApps()
    {
        List<PartSpec> specs = [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps, BarSection.Middle), Spec("tray", BarKind.Tray, BarSection.End), Spec("apps-2", BarKind.Apps, BarSection.End)];
        var layout = Lay(specs, counts: [0, 3, 0, 3]);
        var (first, second) = (layout.PartIndex(specs[1].Key), layout.PartIndex(specs[3].Key));

        Assert.Empty(layout.Overflowed);
        Assert.Equal(3, layout.PartAt(first).Count);
        Assert.Equal(3, layout.PartAt(second).Count);
        for (var tile = 0; tile < 6; tile++)
        {
            var rect = layout.ItemRect(Hit.Tile(tile));
            Assert.False(rect.IsEmpty);
            var owner = tile < 3 ? first : second;
            Assert.Equal(owner, layout.PartOf(Hit.Tile(tile)));
            var at = Middle(rect);
            Assert.Equal(Hit.Tile(tile).In(owner), layout.HitTest(at.X, at.Y));
        }
    }

    /// <summary>
    /// A bar too short for both copies gives up the second's buttons first, and "…" stands in their
    /// place, at the end of the widget that lost them — not at the first's end, where nothing went.
    /// </summary>
    [Fact]
    public void ABarTooShortGivesUpTheSecondCopyFirst()
    {
        List<PartSpec> specs = [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps), Spec("apps-2", BarKind.Apps)];
        var roomy = Lay(specs, counts: [0, 6, 6]);
        var cell = roomy.ItemRect(Hit.Tile(1)).X - roomy.ItemRect(Hit.Tile(0)).X;
        // Room for Start, the first's six and three more.
        var layout = Lay(specs, length: (int)(roomy.StartCell + 9.5f * cell), counts: [0, 6, 6]);

        Assert.NotEmpty(layout.Overflowed);
        Assert.All(layout.Overflowed, tile => Assert.True(tile >= 6, $"tile {tile} of the first copy went behind \"…\""));
        Assert.All(Enumerable.Range(0, 6), tile => Assert.False(layout.ItemRect(Hit.Tile(tile)).IsEmpty));
        Assert.Equal(layout.PartIndex(specs[2].Key), layout.PartOf(Hit.AppOverflow));
    }

    /// <summary>
    /// One Apps widget and pins after it: "…" stays at the end of the open apps, as before, whatever
    /// went behind it.
    /// </summary>
    [Fact]
    public void OneAppsWidgetKeepsMoreAtItsEnd()
    {
        List<PartSpec> specs = [Spec("start", BarKind.Start), Spec("apps", BarKind.Apps), Spec("pin", BarKind.Pin), Spec("pin-2", BarKind.Pin)];
        var layout = Lay(specs, length: 400, counts: [0, 12, 1, 1]);

        Assert.NotEmpty(layout.Overflowed);
        Assert.Equal(1, layout.PartOf(Hit.AppOverflow));
    }

    /// <summary>
    /// Win+N counts an app once on a bar: the open apps a bar holds twice take their numbers where
    /// they first stand, and the copy takes none, so the numbers go on with the next app.
    /// </summary>
    [Fact]
    public void WinNumbersCountAnAppOnce()
    {
        string[] buttons = ["True|pin|a", "True|pin|a", "False||b", "False||c", "False||b", "False||c", "False||d"];

        Assert.Equal([(0, 2), (2, 1), (3, 1), (6, 1)], AppNumbers.Numbered(buttons));
        Assert.Equal((6, 1), AppNumbers.Nth(buttons, 4));
        Assert.Null(AppNumbers.Nth(buttons, 5));
    }
}
