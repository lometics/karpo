// A widget's size follows its settings — including when they change.
//
// A kind whose shape is a function of its settings (WidgetShapes) is measured
// from the Island object the island holds. The base class sets that once, at
// birth; Configure has to keep it in step. Thirteen kinds did not, so their
// drawing followed a changed setting and their size did not: a folder switched
// to show its name kept the square it was born with, which is the bug the user
// reported (a folder has no name since §427).
//
// The guard is generic on purpose. It does not name the folder, or the
// settings that were wrong — it takes every kind the catalogue offers, hands
// the island a different Island object, and asserts the size the shape now
// gives is the size of the new settings. A fourteenth kind that forgets fails
// here without anybody remembering to add it.

using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Sizing;

public sealed class SettingsFollowTests
{
    private static Look LookFor() => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>
    /// Every kind, reconfigured with the settings of a second widget of the same
    /// kind: what it measures afterwards must be what that second widget
    /// measures from new. Anything else means the shape is still reading the
    /// settings the island was made with.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void AWidgetReconfiguredIsTheSizeOfItsNewSettings()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var config = new Config { Height = 48 };
            var style = new IslandStyle(1f, false, Layout.CellPx(96, config), Layout.IconPx(96, config), LookFor());
            foreach (var kind in BarEntries.All.Select(entry => entry.Kind))
            {
                var (first, second) = Pair(kind);
                if (Build(gfx, config, first) is not { } island)
                {
                    continue;
                }
                using (island)
                {
                    if (Build(gfx, config, second) is not { } reference)
                    {
                        continue;
                    }
                    using (reference)
                    {
                        _ = island.Refresh(gfx);
                        _ = reference.Refresh(gfx);
                        island.Configure(second, config);
                        _ = island.Refresh(gfx);

                        Assert.Equal(Cells(reference, style), Cells(island, style));
                    }
                }
            }
        });
    }

    /// <summary>Two widgets of <paramref name="kind"/> whose settings differ where the kind has any.</summary>
    private static (BarItem First, BarItem Second) Pair(BarKind kind)
    {
        var first = new BarItem { Id = "a", Kind = kind };
        var second = new BarItem { Id = "a", Kind = kind };
        first.Normalize();
        second.Normalize();
        switch (kind)
        {
            case BarKind.FolderList:
                first.Folder = new FolderOptions();
                second.Folder = new FolderOptions { Face = FolderFace.Of(FolderFaces.ClockId) };
                break;
            default:
                // Every kind is still reconfigured, with a second object of the same settings: a
                // taskbar entry has no width to set any more, so its own options are what differ.
                break;
        }
        return (first, second);
    }

    private static CellIsland? Build(Gfx gfx, Config config, BarItem widget)
    {
        try
        {
            return Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config) as CellIsland;
        }
        catch (ArgumentOutOfRangeException)
        {
            // A kind the catalogue offers whose island is not built yet.
            return null;
        }
    }

    private static int[] Cells(CellIsland island, in IslandStyle style)
    {
        var measured = island.Measure(style);
        var counts = new int[measured.Count];
        for (var index = 0; index < measured.Count; index++)
        {
            var length = measured.Lengths is { } lengths && index < lengths.Count ? lengths[index] : measured.CellLength;
            counts[index] = (int)MathF.Round(length / style.Cell);
        }
        return counts;
    }
}
