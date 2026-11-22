// Which heights a folder widget may have (Karpo.Core\ListWidgets.cs: ListWidget.Grows and Heights;
// Karpo.Core\LineHeight.cs: Limits, Within), and a file from before the limits read in place. A line's
// menu offers them (Karpo.Dock\LineHeightRows.cs; §228: the settings app's card is gone).
//
// The person, 2026-10-11: "widgets that cannot grow / shrink naturally (because they dont have a
// variable amount of items in them) should not have an automatic height option, just the fixed one.
// also widgets where it makes not sense to show the bigger / smaller than a certain value should have
// limits as well". The most went again on 2026-10-16 (§192): "remove the max height for widgets.
// because then they cannot actually grow to the full height of the page" — every kind is one row to
// LineHeight.Most, past any screen, and the folder's room is the real most.

using Karpo.Core;

namespace Karpo.Tests.Core;

public sealed class HeightLimitsTests
{
    private static IEnumerable<ListWidget> Cards => ListWidgets.All.Where(line => line.Line == ListLine.Card);

    /// <summary>
    /// Every kind's limits are a row to <see cref="LineHeight.Most"/> (no chart has a most of its own since §603,
    /// its charts sized), its unmeasured rows inside them, and where a new line starts is a height the kind
    /// allows: fixed for a kind whose content does not vary.
    /// </summary>
    [Fact]
    public void EveryKindStartsInsideItsLimits()
    {
        foreach (var line in Cards)
        {
            var (least, most) = LineHeight.Limits(line.Kind);
            Assert.Equal((LineHeight.Fewest, LineHeight.Most), (least, most));
            Assert.InRange(least, LineHeight.Fewest, most);
            Assert.InRange(most, least, LineHeight.Most);
            Assert.InRange(line.Cells, least, most);
            var start = LineHeight.StartFor(line.Kind);
            Assert.Equal(start, start.Within(line.Kind));
            if (!line.Grows)
            {
                Assert.True(start.Fixed, $"{line.Kind} does not grow, but starts {start}");
                Assert.Equal(line.Cells, start.Cells);
            }
            Assert.Equal(start, LineHeight.For(line.Kind.New()));
        }
    }

    /// <summary>
    /// §460: every kind's default lies within its least and most — what it is unmeasured, the range a new line starts
    /// in, and where a new line starts, before anything holds it — so its menu never says a height its stepper cannot
    /// reach (the tides started "7 rows" with a most of 3½, the sun's arc "8 rows" with a most of 3).
    /// </summary>
    [Fact]
    public void EveryKindsDefaultLiesWithinItsLeastAndMost()
    {
        var failures = new List<string>();
        foreach (var line in Cards)
        {
            var (least, most) = LineHeight.Limits(line.Kind);
            var range = line.Kind.ListRange();
            var start = LineHeight.StartFor(line.Kind);
            (string What, int Cells)[] defaults =
            [
                ("unmeasured cells", line.Cells), ("range's least", line.Range.Least), ("range's most", line.Range.Most),
                ("start", start.Cells), ("new line", LineHeight.For(line.Kind.New()).Cells),
            ];
            foreach (var (what, cells) in defaults)
            {
                if (cells < least || cells > most)
                {
                    failures.Add($"{line.Kind}: its {what} is {cells} cells, outside {least} to {most}");
                }
            }
            if (line.Kind.HasHeight() && line.Grows && line.Forms is null && start != LineHeight.Automatic(range.Most))
            {
                failures.Add($"{line.Kind}: starts {start}, not automatic up to its range's most, {range.Most}");
            }
        }
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// Only a kind whose content is a number of things has Automatic; one of a fixed content is
    /// fixed whatever it is given, and every height is held to the kind's limits.
    /// </summary>
    [Fact]
    public void AHeightIsHeldToWhatItsKindAllows()
    {
        foreach (var line in Cards.Where(line => line.Kind.HasHeight()))
        {
            var (least, most) = LineHeight.Limits(line.Kind);
            for (var rows = 0; rows <= LineHeight.Most + 1; rows++)
            {
                var fixedHeld = LineHeight.Exactly(rows).Within(line.Kind);
                Assert.Equal(LineHeight.Exactly(Math.Clamp(rows, least, most)), fixedHeld);
                var automatic = new LineHeight(false, rows).Within(line.Kind);
                Assert.Equal(line.Grows
                    ? LineHeight.Automatic(Math.Clamp(rows, least, most))
                    : LineHeight.Exactly(Math.Clamp(Math.Min(Math.Max(rows, 1), line.Cells), least, most)), automatic);
            }
        }
    }

    /// <summary>
    /// Every card has a height to choose but the ones that are always the same (§192): a heading, a
    /// divider and the actions footer; and a kind of one form or a few (§432), which has its sizes.
    /// </summary>
    [Fact]
    public void EveryCardButTheFixedOnesHasAHeight()
    {
        foreach (var line in Cards)
        {
            Assert.Equal(line.Kind is not (ListKind.Divider or ListKind.Heading) && !line.FixedHeight && !line.Kind.Sized(), line.Kind.HasHeight());
        }
        Assert.Equal([ListKind.Actions], Cards.Where(line => line.FixedHeight).Select(line => line.Kind));
    }

    /// <summary>Every ready-made folder holds its lines at heights their kinds allow, so a preset is never set right on read.</summary>
    [Fact]
    public void EveryPresetsHeightIsAllowed()
    {
        foreach (var preset in Presets.All)
        {
            foreach (var (item, _) in Presets.ItemsOn(preset, Karpo.Tests.Core.PresetsTests.Laptop))
            {
                if (item.Height is { } height && ListWidgets.Find(item.Form) is { } line)
                {
                    Assert.True(height == height.Within(line.Kind), $"{preset.Id}: {item.Form} at {height}, which its kind holds to {height.Within(line.Kind)}");
                    var (least, most) = LineHeight.Limits(line.Kind);
                    Assert.True(height.Cells >= least && height.Cells <= most, $"{preset.Id}: {item.Form} at {height}, outside its kind's {least} to {most}");
                }
            }
        }
    }

    /// <summary>
    /// A file from before the limits reads in place, and everything else in it with it: an automatic
    /// month is fixed at its rows (or the fewer it said), an automatic agenda stays automatic; a month
    /// of one row is one row since no kind has a fewest of its own (§166), and one of eight, from
    /// before, still eight; a day view or air quality past the most its kind had is that tall, since no kind
    /// has a most of its own (§192), and only a typo past any screen is held to <see cref="LineHeight.Most"/>.
    /// Written back as read, with no reset. (A sized kind has no height at all, §432: LineSizeTests.)
    /// </summary>
    [Theory]
    [InlineData("month", false, 18, true, -1)]
    [InlineData("month", false, 2, true, 2)]
    [InlineData("dayView", true, 99, true, 99)]
    [InlineData("dayView", true, 999, true, -2)]
    [InlineData("month", true, 1, true, -3)]
    [InlineData("month", true, 8, true, 8)]
    [InlineData("contacts", true, 3, true, 3)]
    [InlineData("todo", true, 9, true, 9)]
    [InlineData("rss", true, 40, true, 40)]
    [InlineData("calendar", false, 6, false, 6)]
    [InlineData("calendar", false, 60, false, 60)]
    public void AFilesHeightOutsideTheRulesReadsInPlace(string kind, bool fixedThen, int rowsThen, bool fixedNow, int rowsNow)
    {
        var line = ListWidgets.Find(kind)!;
        var (least, most) = LineHeight.Limits(line.Kind);
        // -1: the kind's own rows; -2: its most; -3: its fewest.
        var rows = rowsNow switch { -1 => line.Cells, -2 => most, -3 => least, _ => rowsNow };
        var config = Karpo.Tests.Core.Configs.Repaired($$"""
            { "version": {{Config.Shape}}, "layouts": [ { "id": "default", "name": "Default", "islands": [
              { "id": "f", "kind": "folderList",
                "folder": { "face": { "kind": "icon", "options": {} }, "lists": [ [
                  { "island": { "id": "a", "kind": "{{kind}}", "height": { "fixed": {{(fixedThen ? "true" : "false")}}, "cells": {{rowsThen}} } } },
                  { "island": { "id": "d", "kind": "divider" } },
                  { "island": { "id": "m", "kind": "mail", "height": { "fixed": false, "cells": 14 } } } ] ] } } ] } ] }
            """);
        var folder = config.Layouts[0].Islands.Single(island => island.Id == "f").Folder!;
        Assert.Equal(["a", "d", "m"], folder.Cells.Select(cell => cell.Island.Id));
        Assert.Equal(new LineHeight(fixedNow, rows), folder.Cells[0].Island.Height);
        Assert.Equal(new LineHeight(fixedNow, rows), LineHeight.For(folder.Cells[0].Island));
        Assert.Equal(LineHeight.Automatic(14), folder.Cells[2].Island.Height);

        var again = Karpo.Tests.Core.Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(new LineHeight(fixedNow, rows), again.Layouts[0].Islands.Single(island => island.Id == "f").Folder!.Cells[0].Island.Height);
    }
}
