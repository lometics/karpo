// The now-playing face at every size the bar can give it — a thin bar, a thick one, a narrow
// vertical one. It is what the now-playing entry on the bar was, and is drawn as it drew itself, so
// what held the entry holds the face: the media preset's folder wearing it (docs\widgets.md §96).
// Its line in a folder's list is held with the other lines (ListFormTests).
//
// What these hold down. Across a bar the face is one of three fixed widths (§503: Narrow, Medium,
// Wide), the art and the words inside it, whatever plays; the art and the padding are a share of the
// bar. Down a bar on its side it is the art alone, one square across the bar: no words, which would
// be three or four letters there, and never turned ones (the person, 2026-10-13: "dont try to flip text").
//
// On the bar it has no buttons (the person, 2026-10-08): one cell of art and words, whether the
// buttons are on for its line or not; and the title over the artist where the bar is tall enough
// for the time-and-date face's two lines, one line where it is not (MediaIsland.TwoLines, §497).
//
// And the two rules that do not move: the size comes from the settings and the bar's numbers, so
// nothing here may depend on what is playing, and nothing resizes when the track changes.
//
// Nothing plays on the machine running the tests, so the widget is given a stand-in track
// (MediaIsland.Stand). Needs Direct2D: KARPO_UI_TESTS=1; the pictures go to KARPO_PNG_DIR.

using Karpo.Core;
using Karpo.Dock;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Islands;

public sealed unsafe class MediaSizeTests
{
    private const string LongTitle = "Symphony No. 9 in D minor, Op. 125 — IV. Presto – Allegro assai (Live at the Musikverein)";

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>The media preset's folder, its face's art shown or not, at <paramref name="width"/> where one is given.</summary>
    private static BarItem Settings(bool art = true, string? width = null)
    {
        var island = Presets.Folder(Presets.Find(Presets.MediaId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        island.Id = "media";
        island.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.Art)!, art);
        // Drawn idle too: set to stay while nothing plays, which hides it by default (§319).
        island.Folder.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, false);
        if (width is not null)
        {
            island.Folder.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.Width)!, width);
        }
        return island;
    }

    private static IslandStyle Bar(float thickness, bool vertical = false, float textScale = 1, Look? look = null) =>
        new(1f, vertical, thickness, MathF.Round(thickness / 4) * 2, (look ?? Dark) with { TextScale = textScale });

    private static FolderIsland Island(Gfx gfx, MediaStand? stand, bool art = true, string? width = null)
    {
        MediaIsland.Stand = stand ?? new MediaStand(Nothing: true);
        try
        {
            return new FolderIsland("display:default/media", Settings(art, width), gfx, new Config());
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    /// <summary>The cells the widget asks for, in px along the bar.</summary>
    private static IReadOnlyList<float> Cells(CellIsland island, IslandStyle style)
    {
        var cells = island.Measure(style);
        return cells.Lengths is { Count: > 0 } lengths ? lengths : [cells.CellLength];
    }

    /// <summary>§503: the three widths in DIPs, Medium where nothing or a word this build does not know is set.</summary>
    [Fact]
    public void AWidthChoiceIsItsDips()
    {
        Assert.Equal(128, NowPlayingFace.Dips(NowPlayingFace.Narrow));
        Assert.Equal(192, NowPlayingFace.Dips(NowPlayingFace.Medium));
        Assert.Equal(256, NowPlayingFace.Dips(NowPlayingFace.Wide));
        var face = FolderFace.Of(FolderFaces.NowPlayingId);
        Assert.Equal(NowPlayingFace.Medium, NowPlayingFace.WidthOf(face));
        face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.Width)!, "enormous");
        Assert.Equal(NowPlayingFace.Medium, NowPlayingFace.WidthOf(face));
        face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.Width)!, NowPlayingFace.Wide);
        Assert.Equal(NowPlayingFace.Wide, NowPlayingFace.WidthOf(face));
        // Named as a folder's list widths are, in every language they are.
        var option = FolderFaces.NowPlaying.Option(NowPlayingFace.Width)!;
        Assert.Equal(
            [S.DockFolderWidthNarrow, S.DockFolderWidthMedium, S.DockFolderWidthWide],
            option.Choices.Select(choice => FolderFaces.NowPlaying.ChoiceTitle(option, choice)));
    }

    /// <summary>
    /// §503: across a bar the face is its width — its DIPs at the bar's scale and its text's — and the folder's ends,
    /// whatever plays: nothing, "Hey · A", a long title and artist cut to the room, with the art or without; Narrow
    /// shorter than Medium, Medium than Wide. Down a bar on its side the art alone whatever it is set to.
    /// </summary>
    [UiFact]
    public void ItIsItsWidthWhateverPlays()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var thickness in new float[] { 28, 48, 72 })
            {
                foreach (var textScale in new[] { 1f, 1.5f })
                {
                    var across = Bar(thickness, textScale: textScale);
                    var down = Bar(thickness, vertical: true, textScale: textScale);
                    var lengths = new List<float>();
                    foreach (var width in new[] { NowPlayingFace.Narrow, NowPlayingFace.Medium, NowPlayingFace.Wide })
                    {
                        var where = $"{thickness} DIP, text {textScale * 100:0} %, {width}";
                        var face = MediaIsland.FaceLength(across, width);
                        Assert.Equal(MathF.Round(NowPlayingFace.Dips(width) * textScale), face, 0.5f);
                        lengths.Add(face);
                        var wanted = Karpo.Tests.Folders.FaceWidthTests.Button(across, face);
                        foreach (var art in new[] { true, false })
                        {
                            using var quiet = Island(gfx, null, art, width);
                            using var shortly = Island(gfx, new MediaStand(Title: "Hey", Artist: "A"), art, width);
                            using var longly = Island(gfx, new MediaStand(Title: LongTitle, Artist: LongTitle, IsPlaying: false, CanNext: false), art, width);
                            foreach (var island in new[] { quiet, shortly, longly })
                            {
                                var cells = Cells(island, across);
                                Assert.Single(cells);
                                Assert.True(MathF.Abs(cells[0] - wanted) <= 0.5f, $"{where}, art {art}: the cell is {cells[0]} px long, its width and ends {wanted} px");
                                Assert.Equal(Cells(island, down), Cells(quiet, down));
                            }
                            // The long words are cut inside it, never past its end.
                            Assert.All(Watched(longly, across), piece => Assert.True(piece.Box.right <= wanted + 0.5f, $"{where}: a piece ends at {piece.Box.right}, past {wanted}"));
                        }
                    }
                    Assert.True(lengths[0] < lengths[1] && lengths[1] < lengths[2], string.Join(", ", lengths));
                    Assert.Equal(MediaIsland.ArtSize(down), MediaIsland.FaceLength(down, NowPlayingFace.Wide));
                }
            }
        });
    }

    [UiFact]
    public void DownASideBarItIsTheArtAloneAndNoWords()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var stand in new MediaStand?[] { new MediaStand(), new MediaStand(IsPlaying: false), null })
            {
                using var island = Island(gfx, stand);
                foreach (var thickness in new float[] { 28, 40, 48, 72, 96, 128, 160 })
                {
                    var style = Bar(thickness, vertical: true);
                    // The art's square across the bar and the button's ends, nothing more (§275).
                    var cells = Cells(island, style);
                    Assert.Single(cells);
                    var wanted = Karpo.Tests.Folders.FaceWidthTests.Button(style, MediaIsland.FaceLength(style, NowPlayingFace.Medium));
                    Assert.True(MathF.Abs(cells[0] - wanted) <= 0.5f, $"{thickness} DIP: the cell is {cells[0]} px long, the art and its ends {wanted} px");
                    var pieces = Watched(island, style);
                    Assert.DoesNotContain(pieces, piece => piece.What == Painted.Text);
                    Assert.All(pieces, piece => Assert.True(piece.Box.left >= -0.5f && piece.Box.right <= thickness + 0.5f, $"{thickness} DIP: a piece at {piece.Box.left}-{piece.Box.right}"));
                }
            }
        });
    }

    /// <summary>
    /// The face has no transport buttons, as the entry had none: one cell, across a bar and down one,
    /// at every thickness, playing or not — and nothing drawn as a glyph but the note standing in for
    /// art it has none of. The line keeps its own.
    /// </summary>
    [UiFact]
    public void TheTaskbarEntryHasNoTransportButtons()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var stand in new[] { new MediaStand(), new MediaStand(Nothing: true) })
            {
                using var island = Island(gfx, stand);
                foreach (var thickness in new float[] { 28, 36, 40, 48, 72 })
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        var style = Bar(thickness, vertical);
                        var where = $"{thickness} DIP{(vertical ? " vertical" : "")}{(stand.Nothing ? ", nothing playing" : "")}";
                        Assert.True(island.CellCount == 1, $"{where}: {island.CellCount} cells");
                        Assert.Single(Cells(island, style));
                        var glyphs = Watched(island, style).Count(piece => piece.What == Painted.Glyph);
                        Assert.True(glyphs <= 1, $"{where}: {glyphs} glyphs, and the art's note is the only one");
                    }
                }
            }
        });
    }

    /// <summary>
    /// §497: two lines wherever the time-and-date face sets its time over its date, at its text size and its lines'
    /// height — the person's 42 DIP bar and every one from 36 DIP at 100 % text, at 100 % and 150 % — and one line where
    /// it keeps one: the thinnest bars and the largest text. Never down a vertical bar, which shows the art alone.
    /// </summary>
    [UiFact]
    public void TwoLinesWhereTheClocksTwoLinesAre()
    {
        Offscreen.OnStaThread(gfx =>
        {
            var clock = new FolderFace { Kind = FolderFaces.DateTimeId };
            foreach (var dpi in (uint[])[96, 144])
            {
                foreach (var height in new[] { 28, 32, 36, 40, 42, 44, 48, 72, 128 })
                {
                    foreach (var textScale in new[] { 1f, 2.25f })
                    {
                        var config = new Config { Height = height };
                        var style = new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), Dark with { TextScale = textScale });
                        var plan = DateTimeFaceDrawing.Plan(gfx, style, clock);
                        var where = $"{height} DIP at {dpi} DPI, text {textScale * 100:0} %";
                        var two = MediaIsland.TwoLines(gfx, style);
                        Assert.True(plan.Stacked == two is not null, $"{where}: the clock is {(plan.Stacked ? "two lines" : "one line")}, now playing {(two is null ? "one" : "two")}");
                        if (two is var (px, line))
                        {
                            Assert.Equal((plan.TimePx, plan.TimeHeight), (px, line));
                        }
                    }
                }
                var mine = new Config { Height = 42 };
                Assert.NotNull(MediaIsland.TwoLines(gfx, new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, mine), Layout.IconPx(dpi, mine), Dark)));
            }
            Assert.Null(MediaIsland.TwoLines(gfx, Bar(128, vertical: true)));
        });
    }

    /// <summary>
    /// As drawn: on two lines the title stands over the artist, both from the same start, the artist
    /// under the title a clock's line under it (§497) and both inside the bar, the block in its middle; on one line the artist
    /// follows the title on it. A track with no artist is its title over its app where two lines fit, its title alone where not.
    /// </summary>
    [UiFact]
    public void TheTitleStandsOverTheArtistWhereTwoLinesFit()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dpi in (uint[])[96, 144])
            {
                foreach (var height in new[] { 28, 36, 40, 42, 48, 72 })
                {
                    var config = new Config { Height = height };
                    var style = new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), Dark);
                    var lines = MediaIsland.TwoLines(gfx, style);
                    var two = lines is not null;
                    var where = $"{height} DIP at {dpi} DPI";
                    using (var island = Island(gfx, new MediaStand(Title: "Hey", Artist: "Pixies")))
                    {
                        var text = Watched(island, style).Where(piece => piece.What == Painted.Text).Select(piece => piece.Box).ToList();
                        Assert.True(text.Count == 2, $"{where}: {text.Count} pieces of words, and the title and the artist are two");
                        var (title, artist) = (text[0], text[1]);
                        if (two)
                        {
                            Assert.True(artist.top >= title.bottom - 0.5f, $"{where}: the artist at y {artist.top} is not under the title, which ends at {title.bottom}");
                            Assert.True(MathF.Abs(artist.left - title.left) <= 0.5f, $"{where}: the title starts at {title.left} and the artist at {artist.left}");
                            Assert.True(MathF.Abs(artist.top - title.top - lines!.Value.Line) <= 0.5f, $"{where}: the artist is {artist.top - title.top} px under the title, the clock's line {lines.Value.Line}");
                            Assert.True(title.top >= 0 && artist.bottom <= style.Thickness, $"{where}: the two lines run off the bar");
                            var middle = (title.top + artist.bottom) / 2f;
                            Assert.True(MathF.Abs(middle - (style.Thickness / 2f)) <= 1f, $"{where}: the two lines' middle is {middle}, the bar's {style.Thickness / 2f}");
                        }
                        else
                        {
                            Assert.True(MathF.Abs(artist.top - title.top) <= 0.5f && artist.left >= title.right - 0.5f, $"{where}: the artist does not follow the title on its line");
                        }
                    }
                    using (var island = Island(gfx, new MediaStand(Title: "Hey", Artist: "")))
                    {
                        var text = Watched(island, style).Where(piece => piece.What == Painted.Text).ToList();
                        Assert.True(text.Count == (two ? 2 : 1), $"{where}: a track with no artist drew {text.Count} pieces of words");
                    }
                }
            }
        });
    }

    /// <summary>What the entry's one cell draws, piece by piece (PaintWatch), drawn as the bar would.</summary>
    private static List<(D2D_RECT_F Box, Painted What)> Watched(CellIsland island, IslandStyle style)
    {
        var length = MathF.Ceiling(Cells(island, style)[0]);
        var across = MathF.Ceiling(style.Thickness);
        var (width, height) = style.Vertical ? (across, length) : (length, across);
        var pieces = new List<(D2D_RECT_F, Painted)>();
        PaintWatch.Watching = (box, what, color) =>
        {
            pieces.Add((box, what));
            return color;
        };
        try
        {
            Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, style, width, height), _ => { });
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return pieces;
    }

    [UiFact]
    public void ASheetOfTheWidgetAtEverySize()
    {
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = dark ? Dark : Dark with { Dark = false };
                var suffix = dark ? "dark" : "light";
                Bars(gfx, look, new MediaStand(), $"media-bars-{suffix}.png");
                Bars(gfx, look, new MediaStand(Nothing: true), $"media-bars-idle-{suffix}.png");
                Bars(gfx, look, new MediaStand(Title: "Hey", Artist: "Pixies"), $"media-bars-short-{suffix}.png");

                Columns(gfx, look, new MediaStand(), $"media-vertical-{suffix}.png");
                Thin(gfx, look, $"media-thin-bars-{suffix}.png");
            }
        });
    }

    /// <summary>
    /// The thin bars somebody really runs, with a card behind the face: 28, 36 and 41 DIP, with a
    /// long title and a short one. A top edge draws the same row as a bottom one — the cells run along
    /// the bar either way — so one sheet answers for both.
    /// </summary>
    private static void Thin(Gfx gfx, Look look, string file)
    {
        float[] bars = [28, 36, 41];
        const float gap = 12;
        var theme = FlyoutTheme.For(look);
        var rows = new List<(FolderIsland Island, IslandStyle Style, IReadOnlyList<float> Cells)>();
        var made = new List<FolderIsland>();
        try
        {
            foreach (var thickness in bars)
            {
                foreach (var stand in new[] { new MediaStand(), new MediaStand(Title: "Hey", Artist: "Pixies") })
                {
                    var island = Island(gfx, stand);
                    made.Add(island);
                    var automatic = Bar(thickness, look: look);
                    var cells = Cells(island, automatic);
                    rows.Add((island, automatic, cells));
                }
            }
            var width = rows.Max(row => row.Cells.Sum()) + (2 * gap);
            var height = rows.Sum(row => row.Style.Thickness + gap) + gap;
            Offscreen.Bitmap(
                width,
                height,
                dc =>
                {
                    Backdrop(dc, look, width, height);
                    var y = gap;
                    foreach (var (island, style, cells) in rows)
                    {
                        var thickness = style.Thickness;
                        // The card the bar draws behind a widget, so the dead run at its end shows.
                        var card = PopupPaint.Rect(gap, y, cells.Sum(), thickness);
                        PopupPaint.Fill(dc, card, MathF.Min(8, thickness / 4), PopupPaint.Ink(theme.CardBackground));
                        var x = gap;
                        for (var cell = 0; cell < cells.Count; cell++)
                        {
                            var at = (x, y, cell, cells[cell], thickness, island, style);
                            Offscreen.Shifted(dc, at.x, at.y, () => at.island.PaintCell(dc, at.cell, at.style, at.Item4, at.thickness));
                            x += cells[cell];
                        }
                        y += thickness + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var island in made)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>The widget in 28, 36, 40, 48 and 72 DIP cells, one under the other: one line in the thinnest, two where they fit.</summary>
    private static void Bars(Gfx gfx, Look look, MediaStand stand, string file)
    {
        float[] bars = [28, 36, 40, 48, 72];
        const float gap = 10;
        var islands = bars.Select(_ => Island(gfx, stand)).ToList();
        try
        {
            var rows = bars.Select((thickness, index) => (Thickness: thickness, Cells: Cells(islands[index], Bar(thickness)))).ToList();
            var width = rows.Max(row => row.Cells.Sum()) + (2 * gap);
            var height = rows.Sum(row => row.Thickness + gap) + gap;
            Offscreen.Bitmap(
                width,
                height,
                dc =>
                {
                    Backdrop(dc, look, width, height);
                    var y = gap;
                    for (var index = 0; index < rows.Count; index++)
                    {
                        var (thickness, cells) = rows[index];
                        var x = gap;
                        for (var cell = 0; cell < cells.Count; cell++)
                        {
                            var island = islands[index];
                            var at = (x, y, cell, cells[cell], thickness);
                            Offscreen.Shifted(dc, at.x, at.y, () => island.PaintCell(dc, at.cell, Bar(at.thickness, look: look), at.Item4, at.thickness));
                            x += cells[cell];
                        }
                        y += thickness + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var island in islands)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>The widget down 28, 48 and 72 DIP vertical bars, side by side.</summary>
    private static void Columns(Gfx gfx, Look look, MediaStand stand, string file)
    {
        float[] bars = [28, 48, 72];
        const float gap = 10;
        var islands = bars.Select(_ => Island(gfx, stand)).ToList();
        try
        {
            var columns = bars.Select((thickness, index) => (Thickness: thickness, Cells: Cells(islands[index], Bar(thickness, vertical: true)))).ToList();
            var width = columns.Sum(column => column.Thickness + gap) + gap;
            var height = columns.Max(column => column.Cells.Sum()) + (2 * gap);
            Offscreen.Bitmap(
                width,
                height,
                dc =>
                {
                    Backdrop(dc, look, width, height);
                    var x = gap;
                    for (var index = 0; index < columns.Count; index++)
                    {
                        var (thickness, cells) = columns[index];
                        var y = gap;
                        for (var cell = 0; cell < cells.Count; cell++)
                        {
                            var island = islands[index];
                            var at = (x, y, cell, cells[cell], thickness);
                            Offscreen.Shifted(dc, at.x, at.y, () => island.PaintCell(dc, at.cell, Bar(at.thickness, vertical: true, look: look), at.thickness, at.Item4));
                            y += cells[cell];
                        }
                        x += thickness + gap;
                    }
                },
                bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
        }
        finally
        {
            foreach (var island in islands)
            {
                island.Dispose();
            }
        }
    }

    private static void Backdrop(ID2D1DeviceContext* dc, Look look, float width, float height)
    {
        var shade = look.Dark ? new DXGI_RGBA(0.13f, 0.13f, 0.13f, 1) : new DXGI_RGBA(0.96f, 0.96f, 0.96f, 1);
        dc->Clear(&shade);
        _ = width;
        _ = height;
    }
}
