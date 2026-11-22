// The now-playing widget's row as the bar draws it, and the room a range gives a widget reaching
// the drawing.
//
// The user, of a 36 DIP bar at 150 %: "the now playing widget looks off" — the words cut to a few
// letters ("We Playe… · Jet…"), then about a third of the widget empty, then the three transport
// glyphs 40 to 50 px apart at its far end. Two faults made that picture.
//
// - The room a range gives a widget did not reach its drawing. The layout measured and placed each
//   widget in its range — a minimum over what the content asks for hands it room it did not ask
//   for — but the scene drew it with only a width somebody had pinned (Layout.StyleFor), so the
//   widget painted itself at its natural size into the larger slot and the rest of the slot was
//   blank.
// - Each transport button was a whole grid cell, so the three stood a cell apart however small their
//   glyphs were.
//
// Since 2026-10-08 the taskbar entry has no transport buttons at all — the person had them taken
// off — and sets the title over the artist where the bar has room for two lines at its own text
// size (MediaIsland.TwoLines), "title · artist" on one line where it has not. Since 2026-10-10 it is
// the media preset's now-playing face (docs\widgets.md §96), held here as the folder wearing it: a
// face keeps its own length, centred in room a range gives it, so the long track three cells wider
// is the other rows' to show.
//
// Held here on real layouts, drawn as the scene draws them:
//
// - Every widget's surfaces are exactly the slots the layout gave its cells, with its range
//   narrowed, left alone, widened and pinned — the now-playing face. The other entries built as a
//   row of pieces (a share, a drive, a graph) went into presets' faces of their own lengths.
// - The now-playing face, measured from its pixels at 28, 36, 40, 48 and 72 DIP, at 100 % and
//   150 %: one cell and no buttons, nothing drawn as a glyph but the art's note; two lines, the
//   title over the artist, at the heights where they fit, one line under them; the words run to the
//   face's end and are cut only there, on one line the title keeping the larger share; a short track
//   and nothing playing stand centred in it.
//
// The sheets and their numbers (media-row-<dpi>-<look>) go to KARPO_PNG_DIR
// before anything is asserted.
//
// In the samplers' collection because a graph holds the process-wide samplers.

using System.Globalization;
using System.Text;
using Karpo.Tests.Sizing;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Islands;

[Collection(Perf.SamplerState.Collection)]
public sealed unsafe class MediaRowTests
{
    private static readonly int[] Heights = [28, 36, 40, 48, 72];

    private const string LongTitle = "The Longest Title Anybody Ever Gave a Song, Remastered";

    private const string LongArtist = "The Very Long Name Orchestra and Its Friends";

    private static readonly MediaStand Long = new(Title: LongTitle, Artist: LongArtist);

    /// <summary>How the row is drawn: a long track, a short one, nothing playing, and the long one given three cells more than it asks for.</summary>
    private static readonly (string Name, MediaStand Stand, int More)[] States =
    [
        ("long", Long, 0),
        ("short", new MediaStand(Title: "Hey", Artist: "Pixies"), 0),
        ("nothing playing", new MediaStand(Nothing: true), 0),
    ];

    /// <summary>What text is drawn in when its ink is looked for: a magenta the widget never uses.</summary>
    private static readonly DXGI_RGBA TextMark = new(1f, 0f, 1f, 1f);

    /// <summary>What glyphs are drawn in when their ink is looked for: a cyan it never uses either.</summary>
    private static readonly DXGI_RGBA GlyphMark = new(0f, 1f, 1f, 1f);

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>
    /// The media preset's folder, wearing the now-playing face, laid out with <paramref name="least"/>
    /// to <paramref name="most"/> cells: what the now-playing entry on the bar is now.
    /// </summary>
    private static BarItem NowPlaying(int least = WidgetItem.MinWidth, int most = WidgetItem.MostCells)
    {
        var widget = Presets.Folder(Presets.Find(Presets.MediaId)!, new PresetMachine(HasBattery: false, FixedDrives: [])).Given(least, most);
        widget.Id = "media";
        // Drawn idle too: set to stay while nothing plays, which hides it by default (§319).
        widget.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, false);
        return widget;
    }

    /// <summary>The widget's island, made on this thread, with <paramref name="stand"/> playing where it is the now-playing one.</summary>
    private static CellIsland Make(Gfx gfx, BarItem widget, MediaStand? stand)
    {
        MediaIsland.Stand = stand;
        try
        {
            if (Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, new Config()) is not CellIsland island)
            {
                throw new Xunit.Sdk.XunitException($"{widget.Kind}: its descriptor names a widget class, and none was made.");
            }
            _ = island.Refresh(gfx);
            return island;
        }
        finally
        {
            MediaIsland.Stand = null;
        }
    }

    /// <summary>A bar of <paramref name="height"/> DIP along the bottom with the one widget on it, laid out the dock's way.</summary>
    private static Layout Bar(Gfx gfx, CellIsland island, BarItem widget, uint dpi, int height, Look look)
    {
        var config = new Config { Height = height };
        var key = Config.IslandKey(null, widget.Id);
        var islands = new IslandCollection { [key] = island };
        var area = new RECT { right = 3840, bottom = 2160 };
        return new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, [GivenRoom.Part(key, widget)], new Content { Islands = islands });
    }

    /// <summary>
    /// The widget's own rectangle on the bar: along it, the part's block; across it, its cells'
    /// thickness, which is what the scene draws them in.
    /// </summary>
    private static RectF Box(Layout layout)
    {
        var block = layout.BlockRect(0);
        var first = layout.ItemRect(Hit.Cell(0, 0));
        return new RectF(block.X, first.Y, block.Width, first.Height);
    }

    /// <summary>How many cells the widget works out to when its range leaves its content to decide.</summary>
    private static int NaturalCells(Gfx gfx, BarItem widget, uint dpi, int height, Look look, MediaStand? stand)
    {
        using var island = Make(gfx, widget, stand);
        var layout = Bar(gfx, island, widget, dpi, height, look);
        return (int)MathF.Round(layout.BlockRect(0).Width / layout.CellSide);
    }

    // --- The room reaches the drawing ------------------------------------------------------------

    /// <summary>
    /// A widget's surfaces are the slots the layout gave its cells, whatever its range does to its
    /// size — widened past its content, narrowed under it, left alone or pinned — at every height,
    /// at 100 % and 150 %. The scene draws each surface into its slot from the slot's start, so a
    /// surface shorter than its slot leaves the rest of the slot blank: the third of the now-playing
    /// widget the user saw empty.
    /// </summary>
    [UiFact]
    public void EveryCellIsDrawnAsLongAsTheSlotTheLayoutGaveIt()
    {
        var failures = new List<string>();
        var seen = 0;
        Offscreen.OnStaThread(gfx =>
        {
            var look = LookFor(dark: true);
            // The other entries built as a row of pieces went into presets' faces, which keep their
            // own length (a drive's two cells: Storage\DriveRowTests); the now-playing face is left.
            foreach (var kind in new[] { BarKind.FolderList })
            {
                // The folder is the media preset's, wearing what the now-playing entry was.
                var stand = Long;
                BarItem Widget(int least = WidgetItem.MinWidth, int most = WidgetItem.MostCells) => NowPlaying(least, most);
                foreach (var height in Heights)
                {
                    foreach (var dpi in (uint[])[96, 144])
                    {
                        var natural = NaturalCells(gfx, Widget(), dpi, height, look, stand);
                        var ranges = new List<(string Name, int Least, int Most)>
                        {
                            ("left to its content", WidgetItem.MinWidth, WidgetItem.MostCells),
                            ("three cells more than its content", natural + 3, WidgetItem.MostCells),
                            ("pinned two cells wider", natural + 2, natural + 2),
                        };
                        if (natural > 1)
                        {
                            ranges.Add(("a cell narrower than its content", WidgetItem.MinWidth, natural - 1));
                        }
                        foreach (var (name, least, most) in ranges)
                        {
                            var widget = Widget(least, most);
                            using var island = Make(gfx, widget, stand);
                            var layout = Bar(gfx, island, widget, dpi, height, look);
                            var style = layout.StyleFor(0, look);
                            for (var cell = 0; cell < island.CellCount; cell++)
                            {
                                if (layout.ItemRect(Hit.Cell(0, cell)) is not { IsEmpty: false } slot)
                                {
                                    continue;
                                }
                                seen++;
                                var (length, _) = island.CellSize(cell, style);
                                if (MathF.Abs(length - slot.Width) > 0.5f)
                                {
                                    failures.Add($"{kind} at {height} DIP, {dpi} DPI, {name}: cell {cell} is drawn {length:0} px long in a slot of {slot.Width:0}");
                                }
                            }
                        }
                    }
                }
            }
        });
        Assert.True(seen > 30, $"only {seen} cells were looked at");
        Assert.True(failures.Count == 0, $"{failures.Count} cells are drawn another length than their slot:\n{string.Join("\n", failures.Take(40))}");
    }

    // --- The row ---------------------------------------------------------------------------------

    /// <summary>
    /// What one drawn row's pixels say, in px along the widget from its start: its length, its grid
    /// cell and the display's scale, each cell's slot and surface, the size of its text and the
    /// widget's edge, where the words' ink starts and ends (NaN without words), whether they were cut
    /// and how much room the title and the artist were drawn in, how many lines the words were set on
    /// and whether the bar has room for two, how many glyphs were drawn, and where the art's square
    /// and the words reach along it.
    /// </summary>
    private sealed record Measured(
        float Width,
        float Cell,
        float Scale,
        float[] Slots,
        float[] Surfaces,
        float Em,
        float Edge,
        float TextStart,
        float TextEnd,
        bool Cut,
        float TitleDrawn,
        float ArtistDrawn,
        int Lines,
        bool TwoFit,
        int Glyphs,
        (float Left, float Right) Ink,
        (float Left, float Right) Art,
        float Gap)
    {
        /// <summary>From the words' ink to the art after them (2026-10-11: the art on the right).</summary>
        public float Tail => float.IsNaN(TextEnd) ? float.NaN : Art.Left - TextEnd;

        /// <summary>How far the middle of the art and the words is from the widget's middle.</summary>
        public float OffMiddle => ((Ink.Left + Ink.Right) / 2f) - (Width / 2f);
    }

    /// <summary>
    /// Paints the widget's cells the way the scene shows them: each cell's surface — the size the
    /// island makes it for the style the layout hands the scene — drawn 1:1 from the start of the
    /// slot the layout gave that cell, and clipped to the slot.
    /// </summary>
    private static void AsTheSceneDraws(ID2D1DeviceContext* dc, CellIsland island, Layout layout, Look look, float x, float y, Action<int, float>? each = null)
    {
        var block = Box(layout);
        var style = layout.StyleFor(0, look);
        for (var cell = 0; cell < island.CellCount; cell++)
        {
            if (layout.ItemRect(Hit.Cell(0, cell)) is not { IsEmpty: false } slot)
            {
                continue;
            }
            var (width, height) = island.CellSize(cell, style);
            var left = x + slot.X - block.X;
            var top = y + slot.Y - block.Y;
            each?.Invoke(cell, slot.X - block.X);
            var clip = PopupPaint.Rect(left, top, slot.Width, slot.Height);
            dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            try
            {
                var index = cell;
                Offscreen.Shifted(dc, left, top, () => island.PaintCell(dc, index, style, width, height));
            }
            finally
            {
                dc->PopAxisAlignedClip();
            }
        }
    }

    /// <summary>The row drawn once more with its text and glyphs in marks of their own, and measured.</summary>
    private static Measured Measure(Gfx gfx, CellIsland island, Layout layout, Look look, MediaStand stand)
    {
        var block = Box(layout);
        var style = layout.StyleFor(0, look);
        var fit = new Fit(style);
        var count = island.CellCount;
        var slots = new float[count];
        var surfaces = new float[count];
        for (var cell = 0; cell < count; cell++)
        {
            slots[cell] = layout.ItemRect(Hit.Cell(0, cell)).Width;
            surfaces[cell] = island.CellSize(cell, style).Width;
        }
        var width = (int)MathF.Ceiling(block.Width);
        var height = (int)MathF.Ceiling(block.Height);
        var texts = new List<D2D_RECT_F>();
        var glyphs = 0;
        var shift = 0f;
        var hasText = new bool[width];
        var hasGlyph = new bool[width];
        var pieces = new List<D2D_RECT_F>();
        var art = (Left: float.NaN, Right: float.NaN);
        PaintWatch.Watching = (box, what, color) =>
        {
            if (what != Painted.Glyph)
            {
                pieces.Add(new D2D_RECT_F(box.left + shift, box.top, box.right + shift, box.bottom));
            }
            if (what == Painted.Shape)
            {
                art = (box.left + shift, box.right + shift);
            }
            if (what == Painted.Text)
            {
                texts.Add(new D2D_RECT_F(box.left + shift, box.top, box.right + shift, box.bottom));
                return TextMark;
            }
            if (what == Painted.Glyph)
            {
                glyphs++;
                return GlyphMark;
            }
            return color;
        };
        try
        {
            Offscreen.Bitmap(width, height, dc => AsTheSceneDraws(dc, island, layout, look, 0, 0, (_, at) => shift = at), bitmap => Columns(bitmap, width, height, hasText, hasGlyph));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        var textStart = float.NaN;
        var textEnd = float.NaN;
        for (var x = 0; x < width; x++)
        {
            if (hasText[x])
            {
                textStart = float.IsNaN(textStart) ? x : textStart;
                textEnd = x + 1;
            }
        }
        // Cut where a piece was drawn narrower than the whole of it measures: on two lines the artist
        // stands alone, on one it follows the title after " · ".
        var lines = texts.Select(box => MathF.Round(box.top)).Distinct().Count();
        var (title, artist) = stand.Nothing ? ("Nothing playing", "")
            : (stand.Title, stand.Artist.Length == 0 ? "" : lines > 1 ? stand.Artist : $" · {stand.Artist}");
        var titleDrawn = texts.Count > 0 ? texts[0].right - texts[0].left : 0f;
        var artistDrawn = texts.Count > 1 ? texts[1].right - texts[1].left : 0f;
        // A pixel of slack: the whole is measured rounded up, and the drawn box is not.
        // §497: two lines at the clock's two lines' size, one at the face's own.
        var two = MediaIsland.TwoLines(gfx, style);
        var px = lines > 1 && two is var (stacked, _) ? stacked : fit.Primary;
        var cut = titleDrawn + 1f < TextLine.Measure(gfx, title, px, Weight.Regular, null)
            || (artist.Length > 0 && artistDrawn + 1f < TextLine.Measure(gfx, artist, px, Weight.Regular, null));
        return new Measured(
            block.Width, layout.CellSide, style.Scale, slots, surfaces, fit.Primary, MathF.Max(fit.Edge, fit.Px(8)), textStart, textEnd, cut, titleDrawn, artistDrawn,
            lines, two is not null, glyphs, pieces.Count > 0 ? (pieces.Min(box => box.left), pieces.Max(box => box.right)) : (0f, 0f),
            art, fit.Gap);
    }

    /// <summary>Which columns of the picture hold text ink (the magenta) and which glyph ink (the cyan).</summary>
    private static void Columns(IWICBitmap* bitmap, int width, int height, bool[] text, bool[] glyph)
    {
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = width, Height = height };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    // Premultiplied BGRA, so the ratios hold at a soft edge as well.
                    var at = data + (y * stride) + (x * 4);
                    var (b, g, r, a) = (at[0], at[1], at[2], at[3]);
                    if (a <= 64)
                    {
                        continue;
                    }
                    text[x] |= r * 4 >= a * 3 && b * 4 >= a * 3 && g * 5 <= a;
                    glyph[x] |= g * 4 >= a * 3 && b * 4 >= a * 3 && r * 5 <= a;
                }
            }
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
    }

    /// <summary>
    /// The row as the bar draws it, at every height, at 150 % dark and light and at 100 % dark, with
    /// a long track, a short one, nothing playing, and the long one three cells wider.
    /// </summary>
    [UiFact]
    public void TheRowAsTheBarDrawsIt()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var (dpi, dark) in new[] { (144u, true), (144u, false), (96u, true) })
            {
                failures.AddRange(Sheet(gfx, dpi, dark));
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} rows are off:\n{string.Join("\n", failures)}");
    }

    /// <summary>One widget of a sheet: where it was drawn, and what was measured on it.</summary>
    private sealed record Drawn(int Height, string State, CellIsland Island, Layout Layout, Measured Measured);

    private static List<string> Sheet(Gfx gfx, uint dpi, bool dark)
    {
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var drawn = new List<Drawn>();
        try
        {
            foreach (var height in Heights)
            {
                foreach (var (state, stand, more) in States)
                {
                    var widget = NowPlaying();
                    var island = Make(gfx, widget, stand);
                    var layout = Bar(gfx, island, widget, dpi, height, look);
                    drawn.Add(new Drawn(height, state, island, layout, Measure(gfx, island, layout, look, stand)));
                }
            }
            var name = $"media-row-{dpi}-{(dark ? "dark" : "light")}";
            Picture(gfx, look, theme, drawn, Path.Combine(Offscreen.Pictures, name + ".png"));
            File.WriteAllText(Path.Combine(Offscreen.Pictures, name + ".txt"), Table(dpi, dark, drawn));
            return Rules(drawn, $"{dpi} DPI {(dark ? "dark" : "light")}");
        }
        finally
        {
            foreach (var item in drawn)
            {
                item.Island.Dispose();
            }
        }
    }

    /// <summary>What the row must be, read off the measurements of one sheet.</summary>
    private static List<string> Rules(List<Drawn> drawn, string sheet)
    {
        var failures = new List<string>();
        foreach (var height in Heights)
        {
            var at = drawn.Where(item => item.Height == height).ToDictionary(item => item.State, item => item.Measured);
            var where = $"{sheet}, {height} DIP";
            foreach (var (state, row) in at)
            {
                for (var cell = 0; cell < row.Slots.Length; cell++)
                {
                    if (MathF.Abs(row.Slots[cell] - row.Surfaces[cell]) > 0.5f)
                    {
                        failures.Add($"{where}, {state}: cell {cell} is drawn {row.Surfaces[cell]:0} px long in a slot of {row.Slots[cell]:0}");
                    }
                }
                // No transport on the taskbar entry: one cell, and no glyph but the note standing in
                // for the art the stand-in track has none of.
                if (row.Slots.Length != 1)
                {
                    failures.Add($"{where}, {state}: {row.Slots.Length} cells, and the entry is one with no buttons");
                }
                if (row.Glyphs > 1)
                {
                    failures.Add($"{where}, {state}: {row.Glyphs} glyphs drawn, and the art's note is the only one");
                }
                // Two lines where they fit, one where they do not; nothing playing says so on one.
                var lines = state == "nothing playing" || !row.TwoFit ? 1 : 2;
                if (row.Lines != lines)
                {
                    failures.Add($"{where}, {state}: the words are on {row.Lines} lines, and the bar has room for {(row.TwoFit ? "two" : "one")}");
                }
            }
            // The words on the left and the art on the right (2026-10-11), in every state: the art's
            // square starts after the words' last ink.
            foreach (var (state, row) in at)
            {
                if (float.IsNaN(row.Art.Left) || (!float.IsNaN(row.TextEnd) && row.TextEnd > row.Art.Left))
                {
                    failures.Add($"{where}, {state}: the words reach {row.TextEnd:0} px and the art starts at {row.Art.Left:0}, and the art is right of the words");
                }
            }
            var full = at["long"];
            // The words run to the art and are cut only there: no more than the gap before the art and
            // two letters' room (the title is cut at a letter and so is the artist) lie after them,
            // and the art ends the face, no more than the folder's edge before its end.
            foreach (var (state, row) in new[] { ("long", full) })
            {
                if (row.Cut && !(row.Tail <= row.Gap + (2 * row.Em)))
                {
                    failures.Add($"{where}, {state}: the words are cut {row.Tail:0} px before the art, and the gap is {row.Gap:0}");
                }
                if (row.Lines == 1 && row.Cut && row.ArtistDrawn > 0 && row.TitleDrawn < row.ArtistDrawn)
                {
                    failures.Add($"{where}, {state}: the title was drawn in {row.TitleDrawn:0} px and the artist in {row.ArtistDrawn:0}");
                }
            }
            // Never centred (2026-10-11): in every state, long or short, the words start on the
            // entry's leading edge and the art ends on its trailing one.
            foreach (var (state, row) in at)
            {
                if (MathF.Abs(row.Ink.Left - row.Edge) > 1.5f)
                {
                    failures.Add($"{where}, {state}: the words start {row.Ink.Left:0} px into the entry, and its edge is {row.Edge:0}");
                }
                if (MathF.Abs(row.Width - row.Art.Right - row.Edge) > 1.5f)
                {
                    failures.Add($"{where}, {state}: the art ends {row.Width - row.Art.Right:0} px before the entry's end, and its edge is {row.Edge:0}");
                }
            }
            if (at["short"].Cut)
            {
                failures.Add($"{where}: \"Hey\" by Pixies was cut");
            }
        }
        return failures;
    }

    /// <summary>The numbers of one sheet, one widget to a line.</summary>
    private static string Table(uint dpi, bool dark, List<Drawn> drawn)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Now playing as the bar draws it: {dpi} DPI, {(dark ? "dark" : "light")}. px along the widget from its start.");
        text.AppendLine("  lines: drawn / room for. tail: from the words' ink to the art. off: the ink's middle from the widget's.");
        text.AppendLine("height  state                 width  cell  slots   surfaces  words    cut  title/artist  lines  glyphs  tail   off");
        foreach (var item in drawn)
        {
            var row = item.Measured;
            var slots = string.Join(" ", row.Slots.Select(slot => slot.ToString("0", CultureInfo.InvariantCulture)));
            var surfaces = string.Join(" ", row.Surfaces.Select(surface => surface.ToString("0", CultureInfo.InvariantCulture)));
            var words = float.IsNaN(row.TextEnd) ? "none" : $"{row.TextStart:0}-{row.TextEnd:0}";
            var tail = float.IsNaN(row.Tail) ? "-" : row.Tail.ToString("0", CultureInfo.InvariantCulture);
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{item.Height,4}    {item.State,-20}  {row.Width,5:0}  {row.Cell,4:0}  {slots,-6}  {surfaces,-8}  {words,-7}  {(row.Cut ? "yes" : "no"),3}  {row.TitleDrawn,5:0}/{row.ArtistDrawn,-6:0}  {row.Lines}/{(row.TwoFit ? 2 : 1)}    {row.Glyphs,6}  {tail,4}  {row.OffMiddle,4:+0;-0;0}");
        }
        return text.ToString();
    }

    /// <summary>
    /// The sheet: a row per bar height, a column per state, each widget on the bar's own colour with
    /// its card behind it and its name over it.
    /// </summary>
    private static void Picture(Gfx gfx, Look look, FlyoutTheme theme, List<Drawn> drawn, string path)
    {
        const float pad = 16f;
        const float label = 18f;
        var columns = States.Select(state => drawn.Where(item => item.State == state.Name).Max(item => item.Measured.Width)).ToArray();
        var rows = Heights.Select(height => drawn.Where(item => item.Height == height).Max(item => Box(item.Layout).Height)).ToArray();
        var width = pad + columns.Sum(column => column + pad);
        var height = pad + rows.Sum(row => label + row + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var y = pad;
            for (var r = 0; r < Heights.Length; r++)
            {
                var x = pad;
                for (var c = 0; c < States.Length; c++)
                {
                    var item = drawn.First(each => each.Height == Heights[r] && each.State == States[c].Name);
                    var block = Box(item.Layout);
                    PopupPaint.Text(dc, gfx, $"{Heights[r]} DIP · {States[c].Name}", 12f, Weight.Regular, theme.TextSecondary, x, y, columns[c]);
                    var top = y + label;
                    var card = item.Layout.CardRect(0);
                    PopupPaint.Fill(dc, PopupPaint.Rect(x + card.X - block.X, top + card.Y - block.Y, card.Width, card.Height), item.Layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                    AsTheSceneDraws(dc, item.Island, item.Layout, look, x, top);
                    x += columns[c] + pad;
                }
                y += label + rows[r] + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }
}
