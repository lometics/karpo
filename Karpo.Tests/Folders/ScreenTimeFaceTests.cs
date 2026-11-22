// The screen-time face (Karpo.Dock\ScreenTime\ScreenTimeFaceDrawing.cs, docs\widgets.md §504): today's
// total by the screen-time glyph, read from the tracker every screen-time widget reads — here a stand-in
// fortnight (ScreenTimeFaceDrawing.Stand), never this PC's count or files. Its total is today's whatever
// else the days hold; across a bar the glyph and the words, as long as they read; down one on its side the
// glyph over them, inside the bar; a new minute that changes the words a new Reading. The ready-made
// screen-time folder wears it, and the gallery lists it with the system's faces.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Folders;

public sealed unsafe class ScreenTimeFaceTests
{
    private static readonly DateOnly Today = new(2026, 11, 5);

    /// <summary>Today 3 h 42 min so far, yesterday a long day: the face must say today's.</summary>
    private static ScreenTimeSnapshot Days(TimeSpan today)
    {
        var log = new ScreenTimeLog();
        log.Ensure(Today.AddDays(-1)).Total = TimeSpan.FromHours(9.5);
        if (today > TimeSpan.Zero)
        {
            log.Ensure(Today).Total = today;
        }
        return new ScreenTimeSnapshot(Today, log);
    }

    /// <summary>Runs <paramref name="body"/> with the face reading <paramref name="days"/>.</summary>
    private static void Standing(Func<ScreenTimeSnapshot> days, Action body)
    {
        ScreenTimeFaceDrawing.Stand = days;
        try
        {
            body();
        }
        finally
        {
            ScreenTimeFaceDrawing.Stand = null;
        }
    }

    [Fact]
    public void ItSaysTodaysTotal()
    {
        Standing(() => Days(TimeSpan.FromMinutes(222)), () =>
        {
            Assert.Equal(TimeSpan.FromMinutes(222), ScreenTimeFaceDrawing.Today());
            Assert.Equal(S.WidgetScreenTimeHoursMinutes(hours: 3, minutes: 42), ScreenTimeFaceDrawing.Said(ScreenTimeFaceDrawing.Today()));
        });
        // Nothing counted today yet: none, in the line's words.
        Standing(() => Days(TimeSpan.Zero), () => Assert.Equal(S.WidgetScreenTimeMinutes(0), ScreenTimeFaceDrawing.Said(ScreenTimeFaceDrawing.Today())));
    }

    [Fact]
    public void TheReadyMadeFolderWearsItAndTheGalleryListsIt()
    {
        var made = Presets.Folder(Presets.Find("screenTimeGroup")!, new PresetMachine(false, []));
        Assert.Equal(FolderFaces.ScreenTimeId, made.Folder!.Face.Kind);
        Assert.Equal(FaceGroup.System, FaceGallery.GroupOf(FolderFaces.ScreenTime));
        Assert.True(FaceGallery.Finds(WidgetInfos.ScreenTime.Title, FolderFaces.ScreenTime));
        Assert.Empty(FolderFaces.ScreenTime.Options);
    }

    /// <summary>
    /// On the bar, at 100 % and 150 %, a bottom bar and a side one: the glyph and the words, all inside the cell;
    /// across a bar the words after the glyph and the face as long as they read ("3 h 42 min" longer than "5 min"),
    /// down one the words under the glyph. A minute that changes the words is a new Reading.
    /// </summary>
    [UiFact]
    public void ItDrawsTheGlyphAndTheTotal()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var total = TimeSpan.FromMinutes(222);
            Standing(() => Days(total), () =>
            {
                var holder = Presets.Folder(Presets.Find("screenTimeGroup")!, new PresetMachine(false, []));
                using var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        var style = new IslandStyle(scale, vertical, MathF.Round(48 * scale), MathF.Round(24 * scale), Look());
                        var where = $"{scale * 100:0} %{(vertical ? ", vertical" : "")}";
                        total = TimeSpan.FromMinutes(222);
                        var (width, height) = island.CellSize(0, style);
                        var pieces = Watched(island, style, width, height);
                        var glyph = Assert.Single(pieces, piece => piece.What == Painted.Glyph).Box;
                        // Down a side bar the total may stand on rows of its own ("3 h" over "42 min", §598).
                        var rows = pieces.Where(piece => piece.What == Painted.Text).Select(piece => piece.Box).ToList();
                        Assert.NotEmpty(rows);
                        var words = rows[0];
                        Assert.All(pieces, piece => Assert.True(
                            piece.Box.left >= -0.5f && piece.Box.top >= -0.5f && piece.Box.right <= width + 0.5f && piece.Box.bottom <= height + 0.5f,
                            $"{where}: a piece at {piece.Box.left},{piece.Box.top}-{piece.Box.right},{piece.Box.bottom} outside {width}x{height}"));
                        if (vertical)
                        {
                            Assert.True(words.top >= glyph.bottom - 0.5f, $"{where}: the words at {words.top} are not under the glyph ending at {glyph.bottom}");
                        }
                        else
                        {
                            Assert.Single(rows);
                            Assert.True(words.left >= glyph.right - 0.5f, $"{where}: the words at {words.left} do not follow the glyph ending at {glyph.right}");
                            var reading = island.FaceShown;
                            Assert.Contains(S.WidgetScreenTimeHoursMinutes(hours: 3, minutes: 42), reading, StringComparison.Ordinal);
                            total = TimeSpan.FromMinutes(5);
                            var shorter = island.CellSize(0, style).Width;
                            Assert.True(shorter < width, $"{where}: \"5 min\" is no shorter than \"3 h 42 min\"");
                            _ = Watched(island, style, shorter, height);
                            Assert.NotEqual(reading, island.FaceShown);
                        }
                    }
                }
            });
        });
    }

    private static Look Look() => new(
        Glass: false,
        Dark: true,
        Accent: Windows.UI.Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Windows.UI.Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Windows.UI.Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>What the face's cell draws, piece by piece (PaintWatch).</summary>
    private static List<(D2D_RECT_F Box, Painted What)> Watched(CellIsland island, IslandStyle style, float width, float height)
    {
        var pieces = new List<(D2D_RECT_F, Painted)>();
        PaintWatch.Watching = (box, what, color) =>
        {
            pieces.Add((box, what));
            return color;
        };
        try
        {
            Offscreen.Bitmap(MathF.Ceiling(width), MathF.Ceiling(height), dc => island.PaintCell(dc, 0, style, width, height), _ => { });
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return pieces;
    }
}
