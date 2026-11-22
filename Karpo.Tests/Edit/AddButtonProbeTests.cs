// The sections' guarantees walked over two thousand bars rather than the few AddButtonTests and
// SectionLayoutTests draw by hand: every bar of every shape the person could make, at rest and in edit
// mode, on every edge, thin and tall, roomy and so full it runs past its end. Measured without a Gfx
// (Layout.ForTests), from a fixed seed, so a failure names a bar that fails again.
//
// What each bar is held to (2026-10-05, the three sections):
//
//   * in edit mode, every section's adds go to its end, and it stands over its blocks; an empty
//     section's room stands over no block on the bar, nor over another's; and the keys stop at
//     every block, in order (EditStops);
//   * where the bar has room for everything, the sections never overlap, the start begins at the
//     bar's start, the end ends at its end, and the middle is exactly centred unless that would put
//     it within a gap of the start or the end, in which case it is as near the centre as keeps it
//     clear;
//   * at rest a drop goes into the section under the pointer, and what it shows is one new pin's
//     cell (DropLandingTests).

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class AddButtonProbeTests
{
    private const int Bars = 2000;

    /// <summary>What a bar is made of in the probe: the kinds that stand on the bar.</summary>
    private static readonly BarKind[] Kinds =
    [
        BarKind.Start, BarKind.Search, BarKind.TaskView, BarKind.WidgetsBoard, BarKind.EnergySaver, BarKind.NightLight,
        BarKind.Pin, BarKind.ShowDesktop,
    ];

    private static readonly BarSection[] AllSections = [BarSection.Start, BarSection.Middle, BarSection.End];

    private sealed record Probe(List<PartSpec> Specs, uint Dpi, int Length, int Height, Edge Edge)
    {
        public Layout Lay(bool editing)
        {
            var config = new Config { Height = Height };
            var area = Edge.IsVertical()
                ? new RECT { right = 1920, bottom = Length }
                : new RECT { right = Length, bottom = 1080 };
            return Layout.ForTests(Dpi, area, Edge, config, Specs, Layout.CellPx(Dpi, config), editing);
        }

        public override string ToString() =>
            $"{string.Join(",", Specs.Select(spec => $"{spec.Kind}/{spec.Section}"))} at {Length} px, {Height} DIP, {Dpi} dpi, {Edge}";
    }

    /// <summary>Two thousand bars from a fixed seed: none to fourteen parts, each in a section, in their sections' order.</summary>
    private static IEnumerable<Probe> Probes()
    {
        var random = new Random(37);
        for (var bar = 0; bar < Bars; bar++)
        {
            var count = random.Next(0, 15);
            var specs = new List<PartSpec>(count);
            for (var at = 0; at < count; at++)
            {
                specs.Add(new PartSpec(Config.IslandKey(null, $"p{at}"), Kinds[random.Next(Kinds.Length)]) { Section = AllSections[random.Next(3)] });
            }
            yield return new Probe(
                [.. specs.OrderBy(spec => spec.Section)],
                random.Next(2) == 0 ? 96u : 144u,
                random.Next(400, 2600),
                new[] { 28, 41, 48, 72 }[random.Next(4)],
                random.Next(5) == 0 ? Edge.Left : Edge.Bottom);
        }
    }

    private static float Along(Layout layout, RectF rect) => layout.Vertical ? rect.Y : rect.X;

    private static float AlongEnd(Layout layout, RectF rect) => layout.Vertical ? rect.Bottom : rect.Right;

    /// <summary>Whether <paramref name="add"/> stands on any of <paramref name="block"/>'s stretch of the bar.</summary>
    private static bool Over(Layout layout, RectF add, Part block) =>
        block.Width > 0f && Along(layout, add) < block.Right && block.Left < AlongEnd(layout, add);

    /// <summary>The placed parts of <paramref name="section"/>, in order.</summary>
    private static List<Part> PartsOf(Layout layout, BarSection section) =>
        [.. Enumerable.Range(0, layout.Specs.Count).Where(part => layout.Specs[part].Section == section && layout.Placed(part)).Select(layout.PartAt)];

    [Fact]
    public void EverySectionsAddsGoToItsEndOnEveryBar()
    {
        foreach (var probe in Probes())
        {
            var layout = probe.Lay(editing: true);
            foreach (var section in AllSections)
            {
                var end = layout.EndSlot(section);
                Assert.True(end.Index == probe.Specs.Count(spec => spec.Section <= section), $"the {section} adds at {end.Index}: {probe}");
                // A bar too full for its sections runs them past its end, where nothing stands.
                if (PartsOf(layout, section) is [.., var last] && last.Right <= layout.Length && !layout.SectionRect(section).IsEmpty)
                {
                    Assert.True(AlongEnd(layout, layout.SectionRect(section)) >= last.Right, $"the {section} ends before its last block: {probe}");
                }
            }
        }
    }

    [Fact]
    public void NoEmptyRoomStandsOverABlockOrAnotherOnEveryBar()
    {
        foreach (var probe in Probes())
        {
            var layout = probe.Lay(editing: true);
            var blocks = Enumerable.Range(0, probe.Specs.Count).Where(layout.Placed).Select(part => (Part: part, Stretch: layout.PartAt(part))).ToList();
            var rooms = layout.EmptyRooms();
            foreach (var room in rooms)
            {
                foreach (var (part, stretch) in blocks)
                {
                    Assert.False(Over(layout, room, stretch), $"the empty room {room} stands over part {part} {stretch}: {probe}");
                }
                Assert.True(rooms.Count(other => Along(layout, other) < AlongEnd(layout, room) && Along(layout, room) < AlongEnd(layout, other)) == 1, $"two empty rooms share {room}: {probe}");
            }
        }
    }

    [Fact]
    public void TheKeysStopAtEveryBlockInOrderOnEveryBar()
    {
        foreach (var probe in Probes())
        {
            var layout = probe.Lay(editing: true);
            var stops = EditStops.Order(layout);
            Assert.Equal(Enumerable.Range(0, probe.Specs.Count).Where(layout.Placed).OrderBy(part => layout.PartAt(part).Left), stops);
            Assert.Empty(probe.Lay(editing: false).EmptyRooms());
        }
    }

    /// <summary>
    /// Where a bar at rest shows every part it holds, the sections stand apart, each at its place:
    /// the start from the bar's start, the end to its end, the middle centred or as near the centre as
    /// keeps it a gap clear of the other two.
    /// </summary>
    [Fact]
    public void TheSectionsKeepTheirPlacesOnEveryBar()
    {
        foreach (var probe in Probes())
        {
            var layout = probe.Lay(editing: false);
            if (layout.NotShown.Count > 0 || layout.Overflowed.Count > 0)
            {
                continue;
            }
            var start = PartsOf(layout, BarSection.Start);
            var middle = PartsOf(layout, BarSection.Middle);
            var end = PartsOf(layout, BarSection.End);
            var gap = layout.SectionGap;
            // A bar with nothing left to give runs its sections one after another past its end.
            var spans = new[] { start, middle, end }.Where(parts => parts.Count > 0).Select(parts => parts[^1].Right - parts[0].Left).ToList();
            if (spans.Sum() + gap * Math.Max(0, spans.Count - 1) > layout.Length - (2f * layout.EndRoom))
            {
                continue;
            }
            if (start is [var first, ..])
            {
                Assert.True(first.Left == layout.EndRoom, $"the start does not begin at the bar's start: {probe}");
            }
            if (end is [.., var last])
            {
                Assert.True(Math.Abs(last.Right - (layout.Length - layout.EndRoom)) <= 1f, $"the end does not end at the bar's end: {probe}");
            }
            if (middle is [var head, ..] && middle[^1] is var tail)
            {
                var least = start is [.., var startLast] ? startLast.Right + gap : layout.EndRoom;
                var most = end is [var endFirst, ..] ? endFirst.Left - gap : layout.Length - layout.EndRoom;
                Assert.True(head.Left >= least - 1f && tail.Right <= most + 1f, $"the middle overlaps a neighbour: {probe}");
                var centred = MathF.Round((layout.Length - (tail.Right - head.Left)) / 2f, MidpointRounding.AwayFromZero);
                var expected = Math.Clamp(centred, least, MathF.Max(least, most - (tail.Right - head.Left)));
                Assert.True(Math.Abs(head.Left - expected) <= 1f, $"the middle starts at {head.Left}, not {expected}: {probe}");
            }
            if (start is [.., var a] && end is [var b, ..] && middle.Count == 0)
            {
                Assert.True(a.Right <= b.Left, $"the start and the end overlap: {probe}");
            }
        }
    }

    /// <summary>
    /// At rest, a drop on a part goes into that part's section, and the mark it shows is one new
    /// pin's cell or, where the bar would not show the pin, nothing.
    /// </summary>
    [Fact]
    public void AtRestADropGoesIntoTheSectionUnderThePointerOnEveryBar()
    {
        foreach (var probe in Probes().Take(400))
        {
            var layout = probe.Lay(editing: false);
            foreach (var part in Enumerable.Range(0, probe.Specs.Count).Where(layout.Placed))
            {
                var block = layout.PartAt(part);
                if (block.Left < 0f || block.Right > layout.Length)
                {
                    continue;
                }
                var slot = layout.DropSlot(block.Left + block.Width / 2f, -1);
                Assert.True(slot.Section == probe.Specs[part].Section, $"a drop on part {part} went to {slot}: {probe}");
                var mark = layout.LandingRect(slot);
                Assert.True(mark.IsEmpty || MathF.Abs((layout.Vertical ? mark.Height : mark.Width) - layout.Slot) < 0.5f, $"the mark at {slot} is {mark}, not one pin's cell: {probe}");
            }
        }
    }
}
