// The bar's two numbers: a taskbar entry's room along the bar is its kind's, the fewest cells and
// the most, each 1 to 32, the most never below the fewest, and the content decides between them.
// Nobody sets them (2026-10-08). A folder widget's height was once the same pair; it is LineHeight
// now (ListBoundsTests). The guard is a sweep over the numbers WidgetBounds.Of can be handed,
// asserting what must be true of all of them, and counting the cases that make the sweep mean
// something.

namespace Karpo.Tests.Sizing;

public sealed class WidgetBoundsTests
{
    /// <summary>Ends worth trying: missing, nonsense, inside 1 to 32, and past it at both ends.</summary>
    private static readonly int?[] Ends = [null, -1, 0, 1, 2, 3, 8, 12, 20, 32, 33, 99];

    /// <summary>
    /// Any two ends a file can hold come out as a range that exists: each 1 to 32, the most never
    /// below the fewest; an end inside that is kept exactly, whatever the kind would have started
    /// at; a missing end is where the kind starts; and where the two disagree the fewest wins.
    /// </summary>
    [Fact]
    public void AnyTwoEndsAreARangeThatExists()
    {
        var kept = 0;
        var started = 0;
        var crossed = 0;
        foreach (var fallback in new[] { new WidgetBounds(1, 1), new WidgetBounds(1, 8), new WidgetBounds(2, 2) })
        {
            foreach (var atLeast in Ends)
            {
                foreach (var atMost in Ends)
                {
                    var bounds = WidgetBounds.Of(atLeast, atMost, fallback);
                    var by = $"{Show(atLeast)}..{Show(atMost)} over {fallback.Least}..{fallback.Most}";

                    Assert.True(bounds.Least >= 1 && bounds.Most <= WidgetItem.MostCells, $"{by} gave {bounds.Least}..{bounds.Most}");
                    Assert.True(bounds.Least <= bounds.Most, $"{by} gave {bounds.Least}..{bounds.Most}");

                    var least = atLeast is { } chosenLeast ? Math.Clamp(chosenLeast, 1, WidgetItem.MostCells) : fallback.Least;
                    var most = atMost is { } chosenMost ? Math.Clamp(chosenMost, 1, WidgetItem.MostCells) : fallback.Most;
                    Assert.Equal(least, bounds.Least);
                    if (most >= least)
                    {
                        Assert.Equal(most, bounds.Most);
                    }
                    else
                    {
                        // The fewest wins: it is the promise that the widget can be read at all.
                        Assert.Equal(least, bounds.Most);
                        crossed++;
                    }

                    if (atLeast is >= 1 and <= 32 && (atLeast < fallback.Least || atLeast > fallback.Most))
                    {
                        // An end outside where the kind starts is kept, not narrowed to it.
                        kept++;
                    }
                    if (atLeast is null)
                    {
                        started++;
                    }
                }
            }
        }
        Assert.True(kept > 0, "no end outside where its kind starts was ever tried");
        Assert.True(started > 0, "no missing end was ever tried");
        Assert.True(crossed > 0, "the two ends never disagreed");

        static string Show(int? cells) => cells is { } chosen ? chosen.ToString() : "missing";
    }

    /// <summary>On the bar there are no numbers of anybody's (2026-10-08): an entry's room is its kind's, whatever it was handed.</summary>
    [Fact]
    public void AnEntrysRoomIsItsKinds()
    {
        Assert.Equal(new WidgetBounds(1, 1), WidgetBounds.DefaultFor(BarKind.TouchKeyboard));
        Assert.Equal(new WidgetBounds(1, 1), WidgetBounds.For(new BarItem { Kind = BarKind.TouchKeyboard, Height = LineHeight.Exactly(6) }));
        Assert.Equal(WidgetBounds.DefaultFor(BarKind.NightLight), WidgetBounds.For(new BarItem { Kind = BarKind.NightLight, Height = LineHeight.Exactly(40) }));
    }

    /// <summary>
    /// A range and a fixed size are one setting: a range lets the content decide between its ends, a
    /// fixed size is a range whose ends meet, and clamping is how a natural size becomes the size it
    /// is given.
    /// </summary>
    [Fact]
    public void ARangeAndAFixedSizeAreTheSameSetting()
    {
        var range = new WidgetBounds(2, 5);
        Assert.False(range.IsFixed);
        Assert.Equal(2, range.Clamp(1));
        Assert.Equal(5, range.Clamp(99));
        Assert.Equal(3, range.Clamp(3));

        var exactly = new WidgetBounds(3, 3);
        Assert.True(exactly.IsFixed);
        Assert.Equal(3, exactly.Clamp(1));
        Assert.Equal(3, exactly.Clamp(99));

        // A most of the most there is holds nothing back (§259): the content is the measure, however
        // wide; its fewest still holds, and a fixed width is still that width.
        var open = new WidgetBounds(2, WidgetItem.MostCells);
        Assert.True(open.Open);
        Assert.Equal(99, open.Clamp(99));
        Assert.True(open.Allows(99));
        Assert.Equal(2, open.Clamp(1));
        Assert.False(new WidgetBounds(WidgetItem.MostCells, WidgetItem.MostCells).Open);
        Assert.Equal(WidgetItem.MostCells, WidgetBounds.Chosen(99).Clamp(99));
        Assert.True(WidgetBounds.DefaultFor(BarKind.FolderList).Open);
    }

    /// <summary>
    /// Where a new widget starts is its kind's measurement on the one surface it has a size on, and
    /// always a range that exists: a drive at its two cells, a component at one row to its most, the
    /// open apps with room for as many as there can be.
    /// </summary>
    [Fact]
    public void EveryKindStartsSomewhereThatExists()
    {
        foreach (var kind in BarEntries.All.Select(entry => entry.Kind))
        {
            var start = WidgetBounds.DefaultFor(kind);
            Assert.True(start.Least >= 1 && start.Least <= start.Most && start.Most <= WidgetItem.MostCells, $"{kind} starts at {start.Least}..{start.Most}");
        }
        foreach (var kind in ListWidgets.All.Select(line => line.Kind))
        {
            var start = kind.ListRange();
            Assert.True(start.Least >= 1 && start.Least <= start.Most && start.Most <= WidgetItem.MostCells, $"line {kind} starts at {start.Least}..{start.Most}");
        }
        Assert.Equal(LineHeight.Fewest, ListKind.NotificationsList.ListRange().Least);
        // The open apps' width is how many are open; where it starts must not cut them off at eight.
        Assert.Equal(WidgetItem.MostCells, WidgetBounds.DefaultFor(BarKind.Apps).Most);
    }

    /// <summary>
    /// The file's own normalising: a line with a height always ends up with one, held to 1 to LineHeight.Most;
    /// a taskbar entry of any kind ends up with none at all.
    /// </summary>
    [Fact]
    public void NormalizeWritesAHeightForALineAndNoneForAnEntry()
    {
        var fixedHigh = new CellItem { Kind = ListKind.NotificationsList.Id(), View = FolderView.List, Height = LineHeight.Exactly(198) };
        fixedHigh.Normalize();
        Assert.Equal(LineHeight.Exactly(198), fixedHigh.Height);

        var untouched = new CellItem { Kind = ListKind.NotificationsList.Id(), View = FolderView.List };
        untouched.Normalize();
        Assert.Equal(LineHeight.Automatic(ListKind.NotificationsList.ListRange().Most), untouched.Height);

        // Down a list, twelve rows and more are a height a person may pick, and they survive.
        var tall = new CellItem { Kind = ListKind.NotificationsList.Id(), View = FolderView.List, Height = LineHeight.Automatic(60) };
        tall.Normalize();
        Assert.Equal(LineHeight.Automatic(60), tall.Height);

        // On the bar, nothing: not for a clock, a folder, the tray, nor one nobody gave a width.
        foreach (var kind in new[] { BarKind.NightLight, BarKind.FolderList, BarKind.Tray, BarKind.TouchKeyboard })
        {
            var entry = new BarItem { Kind = kind, Height = LineHeight.Exactly(4) };
            entry.Normalize();
            Assert.Null(entry.Height);
        }
    }

    /// <summary>A size said as one number is that range with its ends together, and the old Automatic is any size at all.</summary>
    [Fact]
    public void ASizeSaidAsOneNumberIsARangeWithItsEndsTogether()
    {
        Assert.Equal(new WidgetBounds(4, 4), WidgetBounds.Chosen(4));
        Assert.Equal(new WidgetBounds(32, 32), WidgetBounds.Chosen(99));
        Assert.Equal(WidgetBounds.Any, WidgetBounds.Chosen(WidgetItem.AutomaticWidth));
        Assert.Equal(new WidgetBounds(1, WidgetItem.MostCells), WidgetBounds.Any);
    }
}
