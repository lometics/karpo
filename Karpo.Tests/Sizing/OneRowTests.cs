// A taskbar entry is one row of content at any bar height (docs\widgets.md §12.5).
//
// The user: "also make sure that the taskbar widgets layouts are designed to take up one row only".
// A stacked form — a glyph over its value, a symbol over its price, the weekday over the day, a
// name over a capacity bar — was a way of using a thick bar's height, and a list folder's line
// is where content that wants height goes now. Across a horizontal bar every entry is one row, at
// every height from the thinnest to 128 DIP. Down a vertical bar one line often has no room, so
// the stacked forms stay there (§12.5); nothing here draws a vertical bar.
//
// Two entries are two lines where they fit, at the person's asking (2026-10-08): now playing sets
// its title over its artist on a bar with room for two lines at its own text size
// (MediaIsland.TwoLines), and the input indicator its language over its region in its square, as
// Windows' does (KeyboardIsland.Room). Where they do, the one-row rule is not theirs — MediaRowTests
// and KeyboardSquareTests hold their two lines — and here each is held to one row at every height
// where two do not fit.
//
// Held twice.
//
// - On the drawing: every taskbar kind is painted at 28, 36, 48, 72 and 128 DIP with a watcher on
//   what it draws (PaintWatch). Every line of text, every glyph and every shape that is a piece of
//   its own (a capacity bar, a graph, a track's art) must share one band across the bar: things side
//   by side do, and two things one over the other share none.
// - In the pixels: a sheet of every entry at those heights, dark and light, with each entry measured
//   on its own. Its ink is one unbroken band — a blank row between two pieces is two rows, while one
//   inside a piece's own box is the piece's design (the gap in a person glyph between head and
//   shoulders) — its text ink is no taller than one line of its own text, and the band is in the
//   middle of the bar.
//   The sheet and its numbers go to KARPO_PNG_DIR (one-row-<height>-<look>.png and .txt) before any
//   assertion, so a failing run still leaves its pictures.
//
// In the samplers' collection because the five performance graphs hold the process-wide samplers
// (Perf.SamplerState says why that matters).

using System.Globalization;
using System.Text;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Sizing;

[Collection(Perf.SamplerState.Collection)]
public sealed unsafe class OneRowTests
{
    /// <summary>The bar heights the rule is held at: the thinnest, the taskbar's own, and tall ones.</summary>
    private static readonly int[] Heights = [28, 36, 48, 72, 128];

    /// <summary>
    /// The taskbar kinds the scene draws itself, each one glyph or one icon — a button, the running
    /// apps, the tray's icons, a pin — or a line across the bar (a separator). None has a second
    /// line to lose. A new kind the scene draws has to be put here, so it is decided
    /// rather than missed.
    /// </summary>
    private static readonly BarKind[] SceneDrawn =
    [
        BarKind.Start, BarKind.Search, BarKind.TaskView, BarKind.ShowDesktop,
        BarKind.PenMenu, BarKind.TouchKeyboard, BarKind.VirtualTouchpad,
        BarKind.SnippingTool, BarKind.WindowsSettings,
        BarKind.QuickLink, BarKind.WindowsSound,
        BarKind.WindowsNetworks, BarKind.WindowsCast, BarKind.WindowsClipboard, BarKind.EmojiPanel,
        BarKind.Pin, BarKind.Apps, BarKind.Tray,
    ];

    private static readonly SystemState Radios = new(
        char.ConvertFromUtf32(0xE873),
        char.ConvertFromUtf32(0xE994),
        NetworkBackGlyph: char.ConvertFromUtf32(0xE701),
        BluetoothGlyph: char.ConvertFromUtf32(0xE702),
        BluetoothOn: true);

    internal static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8))
    { TextScale = textScale };

    /// <summary>One entry of the bar: what it is called on the sheet, and the widget.</summary>
    internal sealed record Entry(string Name, BarItem Widget);

    /// <summary>
    /// Every taskbar kind a widget class draws, each with what it needs to show its fullest self —
    /// a place for the weather, a track playing, a battery, a label beside a switch — at the room
    /// the bar gives it, since nobody sets an entry's width.
    /// </summary>
    internal static List<Entry> Entries()
    {
        var entries = new List<Entry>();
        foreach (var kind in BarEntries.All.Where(entry => entry.Drawing != WidgetSource.None).Select(entry => entry.Kind))
        {
            var widget = kind.New();
            widget.Id = kind.ToString().ToLowerInvariant();
            switch (kind)
            {
                case BarKind.FolderList:
                    widget.Folder = new FolderOptions();
                    break;
                default:
                    if (BarEntries.Of(kind).Drawing is WidgetSource.Switch or WidgetSource.Security)
                    {
                        widget.Switch = new SwitchOptions { ShowLabel = true };
                    }
                    break;
            }
            entries.Add(new Entry(kind.ToString(), widget));
        }
        return entries;
    }

    /// <summary>
    /// The entries' widgets, made on this thread with the stand-ins the tests use for what is
    /// live: a track, a charge, a level, the radios, a reading with a history behind it, and a
    /// frozen clock, so a sheet is the same picture on every machine as nearly as it can be.
    /// </summary>
    internal static List<(Entry Entry, CellIsland Island)> Make(Gfx gfx)
    {
        Clock.Now = () => new DateTime(2026, 12, 23, 10, 30, 0);
        // A title longer than any column, so the line has to share its room with the artist.
        MediaIsland.Stand = new MediaStand(Title: "Air on the G String, from the Orchestral Suite No. 3 in D major");
        BatteryIsland.Probe = () => new SystemStatus.Battery(88, Plugged: false, Saver: false);
        VolumeWatch.Stand = new SampleVolume { Level = 60 };
        var config = new Config();
        var made = new List<(Entry, CellIsland)>();
        foreach (var entry in Entries())
        {
            var built = Karpo.Dock.Islands.Create(Config.IslandKey(null, entry.Widget.Id), entry.Widget, gfx, config) as CellIsland;
            if (built is not { } island)
            {
                throw new Xunit.Sdk.XunitException($"{entry.Name}: its descriptor names a widget class, and none was made.");
            }
            if (island is FolderIsland { ShowsStatus: true } folder)
            {
                folder.SetStatus(Radios, gfx);
            }
            _ = island.Refresh(gfx);
            if (island is PerformanceIsland graph)
            {
                // A reading with a minute of history behind it, fed after the refresh so the figure
                // is this one and not whatever the machine said.
                for (var at = 0; at < 90; at++)
                {
                    var wave = 0.45f + (0.3f * MathF.Sin(at / 7f));
                    graph.Feed(wave, down: 400_000 + (300_000 * wave), up: 20_000);
                }
            }
            made.Add((entry, island));
        }
        return made;
    }

    internal static void Dispose(List<(Entry Entry, CellIsland Island)> made)
    {
        foreach (var (_, island) in made)
        {
            island.Dispose();
        }
    }

    /// <summary>
    /// Whether <paramref name="island"/> is the input indicator in a square with room for its language
    /// over its region: the entry the one-row rule does not hold there (docs\widgets.md §12.5). Now
    /// playing was the other, until it became the media preset's face (§96, MediaPresetTests). §474: and Windows' own clock,
    /// the time over the date as Windows' taskbar sets them where the bar has room (FolderFaces.DateTime).
    /// </summary>
    private static bool TwoLinesAllowed(Gfx gfx, CellIsland island, in IslandStyle style) =>
        (island is KeyboardIsland && KeyboardIsland.Room(gfx, style, withRegion: true).TwoLines)
        || island is FolderIsland { IsFolder: false } windows && windows.Options.Wears(FolderFaces.DateTime);

    /// <summary>A widget on a horizontal bar of <paramref name="config"/>, with the room it was given.</summary>
    internal static IslandStyle StyleFor(uint dpi, Config config, Look look, BarItem widget) =>
        new(dpi / 96f, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look) { Room = WidgetBounds.For(widget) };

    // --- Every kind is accounted for ------------------------------------------------------------

    /// <summary>
    /// Every taskbar kind is drawn by a widget class (held below on its drawing) or by the scene as
    /// one glyph or icon. A new kind lands in one or the other on purpose.
    /// </summary>
    [Fact]
    public void EveryTaskbarKindIsAWidgetOrOneOfTheScenesIcons()
    {
        foreach (var kind in BarEntries.All.Select(entry => entry.Kind))
        {
            var island = BarEntries.Of(kind).Drawing != WidgetSource.None;
            Assert.True(
                island || SceneDrawn.Contains(kind),
                $"{kind} is on the bar and drawn by the scene, and nobody has said whether it is one row: put it in SceneDrawn or give it a test of its own.");
        }
    }

    // --- On the drawing ---------------------------------------------------------------------------

    /// <summary>
    /// Every line of text, every glyph and every shape of its own a taskbar entry draws shares one
    /// band across a horizontal bar, at every height, at 100 % and 150 %, and at the ordinary and the
    /// largest text size.
    /// </summary>
    [UiFact]
    public void EveryTaskbarEntryDrawsOneRowAtEveryHeight()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var made = Make(gfx);
            try
            {
                foreach (var height in Heights)
                {
                    var config = new Config { Height = height };
                    foreach (var dpi in (uint[])[96, 144])
                    {
                        foreach (var textScale in new[] { 1f, 2.25f })
                        {
                            var where = $"{height} DIP, {dpi} DPI, text ×{textScale}";
                            var look = LookFor(dark: true, textScale);
                            foreach (var (entry, island) in made)
                            {
                                var style = StyleFor(dpi, config, look, entry.Widget);
                                if (TwoLinesAllowed(gfx, island, style))
                                {
                                    continue;
                                }
                                for (var cell = 0; cell < island.CellCount; cell++)
                                {
                                    if (Apart(Watched(island, cell, style, tint: false).Boxes) is { } why)
                                    {
                                        failures.Add($"{entry.Name} at {where}, cell {cell}: {why}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                Dispose(made);
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} entries are not one row:\n{string.Join("\n", failures)}");
    }

    /// <summary>
    /// What one cell draws: the box of every line of text, glyph and shape of its own, and — where
    /// <paramref name="tint"/> — the pixels with the text drawn in a colour nothing else uses, so its
    /// ink can be told apart.
    /// </summary>
    private static (List<(D2D_RECT_F Box, Painted What)> Boxes, Rows Rows) Watched(CellIsland island, int cell, in IslandStyle style, bool tint)
    {
        var (width, height) = island.CellSize(cell, style);
        var boxes = new List<(D2D_RECT_F, Painted)>();
        var shown = style;
        Rows rows = default;
        PaintWatch.Watching = (box, what, color) =>
        {
            if (box.bottom > box.top)
            {
                boxes.Add((box, what));
            }
            return tint && what == Painted.Text ? Marker : color;
        };
        try
        {
            Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, cell, shown, width, height), bitmap => rows = RowsOf(bitmap, width, height));
        }
        finally
        {
            PaintWatch.Watching = null;
        }
        return (boxes, rows);
    }

    /// <summary>
    /// Why the boxes are not one row, or null when they are: every one of them must cross one band
    /// with every other, and the band they share must be at least half as tall as the smallest of
    /// them — two lines that merely touch, or a glyph whose box reaches a pixel into the line under
    /// it, share next to nothing.
    /// <para>
    /// A piece drawn <b>on</b> another is not a piece of the row: a warning badge on a drive's icon,
    /// a note glyph in the art's square. It is told by where its middle is — inside a larger piece's
    /// box — so a glyph over a line, whose middle is its own, still counts, and so does a line
    /// beside a tall icon.
    /// </para>
    /// </summary>
    private static string? Apart(List<(D2D_RECT_F Box, Painted What)> boxes)
    {
        boxes = boxes.Where((item, at) => !OnAnother(boxes, at)).ToList();
        if (boxes.Count < 2)
        {
            return null;
        }
        var lowest = boxes.MaxBy(item => item.Box.top);
        var highest = boxes.MinBy(item => item.Box.bottom);
        var shared = highest.Box.bottom - lowest.Box.top;
        var smallest = boxes.Min(item => item.Box.bottom - item.Box.top);
        if (shared >= 0.5f * smallest)
        {
            return null;
        }
        return $"{Word(highest.What)} at y {highest.Box.top:0}–{highest.Box.bottom:0} and {Word(lowest.What)} at y {lowest.Box.top:0}–{lowest.Box.bottom:0} are one over the other";

        static string Word(Painted what) => what switch
        {
            Painted.Glyph => "a glyph",
            Painted.Shape => "a shape",
            _ => "a line",
        };
    }

    /// <summary>Whether piece <paramref name="at"/>'s middle is inside a larger piece's box: drawn on it.</summary>
    private static bool OnAnother(List<(D2D_RECT_F Box, Painted What)> boxes, int at)
    {
        var box = boxes[at].Box;
        var (x, y) = ((box.left + box.right) / 2f, (box.top + box.bottom) / 2f);
        for (var other = 0; other < boxes.Count; other++)
        {
            var under = boxes[other].Box;
            if (other != at
                && Area(under) > Area(box)
                && x >= under.left && x <= under.right && y >= under.top && y <= under.bottom)
            {
                return true;
            }
        }
        return false;

        static float Area(in D2D_RECT_F box) => (box.right - box.left) * (box.bottom - box.top);
    }

    // --- In the pixels ----------------------------------------------------------------------------

    /// <summary>What text is drawn in when a test wants to find it: a magenta nothing on the bar uses.</summary>
    private static readonly DXGI_RGBA Marker = new(1f, 0f, 1f, 1f);

    /// <summary>Which rows of a picture have ink, and which have text ink.</summary>
    private readonly record struct Rows(bool[] Ink, bool[] Text);

    /// <summary>
    /// The rows with ink — anything drawn with some opacity — and the rows with text ink: pixels that
    /// are the marker's magenta, which no widget draws in, so text and nothing else.
    /// </summary>
    private static Rows RowsOf(IWICBitmap* bitmap, float width, float height)
    {
        var w = (uint)MathF.Ceiling(width);
        var h = (uint)MathF.Ceiling(height);
        var ink = new bool[h];
        var text = new bool[h];
        IWICBitmapLock* locked = null;
        try
        {
            var all = new WICRect { X = 0, Y = 0, Width = (int)w, Height = (int)h };
            ComUtil.Check(bitmap->Lock(&all, (uint)WICBitmapLockFlags.WICBitmapLockRead, &locked));
            uint stride = 0;
            ComUtil.Check(locked->GetStride(&stride));
            uint size = 0;
            byte* data = null;
            ComUtil.Check(locked->GetDataPointer(&size, &data));
            for (var y = 0u; y < h; y++)
            {
                for (var x = 0u; x < w; x++)
                {
                    // Premultiplied BGRA.
                    var at = data + (y * stride) + (x * 4);
                    var (b, g, r, a) = (at[0], at[1], at[2], at[3]);
                    if (a <= 24)
                    {
                        continue;
                    }
                    ink[y] = true;
                    // Magenta and nothing else: red and blue near full, green near none, whatever the
                    // coverage — premultiplied, so the ratios hold at a letter's soft edge too.
                    if (a > 64 && r * 4 >= a * 3 && b * 4 >= a * 3 && g * 5 <= a)
                    {
                        text[y] = true;
                    }
                }
            }
        }
        finally
        {
            ComUtil.Release(ref locked);
        }
        return new Rows(ink, text);
    }

    /// <summary>
    /// The sheets: every entry at each height, dark and light, each measured on its own, and the
    /// sheet and the numbers written before anything is asserted.
    /// </summary>
    [UiFact]
    public void OneRowSheets()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var made = Make(gfx);
            try
            {
                foreach (var height in Heights)
                {
                    foreach (var dark in new[] { true, false })
                    {
                        failures.AddRange(Sheet(gfx, made, height, dark));
                    }
                }
            }
            finally
            {
                Dispose(made);
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} entries are not one row:\n{string.Join("\n", failures)}");
    }

    /// <summary>One entry measured: where its ink is, across the bar, and what that says.</summary>
    private readonly record struct Measured(string Name, int Cells, float Width, int First, int Last, int Gaps, int TextFirst, int TextLast, float Line, float Offset, string? Why)
    {
        public int Band => Last >= First ? Last - First + 1 : 0;

        public int TextBand => TextLast >= TextFirst ? TextLast - TextFirst + 1 : 0;
    }

    /// <summary>
    /// Measures one entry from its rows: one unbroken band, text one line tall, in the middle. A blank
    /// row counts as a break only outside every piece's own box (<paramref name="covered"/>): the gap
    /// between a glyph's parts is the glyph, and the one between two rows is the defect.
    /// </summary>
    private static Measured Measure(string name, int cells, float width, float height, Rows rows, bool[] covered, float line)
    {
        var first = Array.IndexOf(rows.Ink, true);
        var last = Array.LastIndexOf(rows.Ink, true);
        var gaps = 0;
        for (var y = first; first >= 0 && y <= last; y++)
        {
            gaps += rows.Ink[y] || (y < covered.Length && covered[y]) ? 0 : 1;
        }
        var textFirst = Array.IndexOf(rows.Text, true);
        var textLast = Array.LastIndexOf(rows.Text, true);
        var offset = first < 0 ? 0 : ((first + last + 1) / 2f) - (height / 2f);
        var measured = new Measured(name, cells, width, first, last, gaps, textFirst, textLast, line, offset, null);
        string? why = null;
        if (first < 0)
        {
            why = null;
        }
        else if (gaps > 0)
        {
            why = $"its ink is two bands: {gaps} blank rows outside every piece between y {first} and y {last}";
        }
        else if (line > 0 && measured.TextBand > line + 2)
        {
            why = $"its text is {measured.TextBand} px tall, and one line of it is {line:0}";
        }
        else if (MathF.Abs(offset) > MathF.Max(2f, 0.1f * height))
        {
            why = $"its ink is {offset:+0.0;-0.0} px off the middle of the bar";
        }
        return measured with { Why = why };
    }

    /// <summary>One sheet: every entry on its card, measured, and the picture and the numbers written.</summary>
    private static List<string> Sheet(Gfx gfx, List<(Entry Entry, CellIsland Island)> made, int height, bool dark)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var thickness = Layout.ThicknessPx(dpi, config);
        var failures = new List<string>();
        var drawn = new List<(string Name, float Width, float Height, Action<IntPtr> Paint)>();
        var measured = new List<Measured>();

        foreach (var (entry, island) in made)
        {
            var style = StyleFor(dpi, config, look, entry.Widget);
            var sizes = Enumerable.Range(0, island.CellCount).Select(cell => island.CellSize(cell, style)).ToList();
            if (sizes.Count == 0)
            {
                continue;
            }
            var along = sizes.Sum(size => size.Width);
            var across = sizes.Max(size => size.Height);
            var tall = (int)MathF.Ceiling(across);
            var rows = new Rows(new bool[tall], new bool[tall]);
            var covered = new bool[tall];
            var line = 0f;
            for (var cell = 0; cell < sizes.Count; cell++)
            {
                var (boxes, cellRows) = Watched(island, cell, style, tint: true);
                line = MathF.Max(line, boxes.Where(item => item.What == Painted.Text).Select(item => item.Box.bottom - item.Box.top).DefaultIfEmpty(0f).Max());
                Cover(covered, boxes.Select(item => item.Box));
                for (var y = 0; y < tall && y < cellRows.Ink.Length; y++)
                {
                    rows.Ink[y] |= cellRows.Ink[y];
                    rows.Text[y] |= cellRows.Text[y];
                }
            }
            var result = Measure(entry.Name, sizes.Count, along, across, rows, covered, line);
            if (TwoLinesAllowed(gfx, island, style))
            {
                result = result with { Name = $"{entry.Name}, two lines", Why = null };
            }
            measured.Add(result);
            if (result.Why is { } why)
            {
                failures.Add($"{entry.Name} at {height} DIP, {(dark ? "dark" : "light")}: {why}");
            }
            var painted = island;
            var shown = style;
            drawn.Add((entry.Name, along, across, pointer =>
            {
                var dc = (ID2D1DeviceContext*)pointer;
                var at = 0f;
                for (var cell = 0; cell < sizes.Count; cell++)
                {
                    var (w, h) = sizes[cell];
                    var index = cell;
                    Offscreen.Shifted(dc, at, 0, () => painted.PaintCell(dc, index, shown, w, h));
                    at += w;
                }
            }));
        }

        var name = $"one-row-{height}-{(dark ? "dark" : "light")}";
        Picture(gfx, theme, thickness, drawn, Path.Combine(Offscreen.Pictures, name + ".png"));
        File.WriteAllText(Path.Combine(Offscreen.Pictures, name + ".txt"), Table(height, dark, measured));
        return failures;
    }

    /// <summary>Marks the rows <paramref name="boxes"/> stand in: blank rows there are a piece's own.</summary>
    private static void Cover(bool[] covered, IEnumerable<D2D_RECT_F> boxes)
    {
        foreach (var box in boxes)
        {
            var from = Math.Max(0, (int)MathF.Floor(box.top));
            var to = Math.Min(covered.Length, (int)MathF.Ceiling(box.bottom));
            for (var y = from; y < to; y++)
            {
                covered[y] = true;
            }
        }
    }

    /// <summary>The numbers of one sheet, one entry to a line, in the order the sheet draws them.</summary>
    private static string Table(int height, bool dark, List<Measured> measured)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"One row: {height} DIP bar, {(dark ? "dark" : "light")}, 96 DPI. Rows are px from the cell's top.");
        text.AppendLine("entry                      cells  width  ink rows    band  gaps  text band  one line  off middle  verdict");
        foreach (var item in measured)
        {
            var rows = item.First < 0 ? "none" : $"{item.First}-{item.Last}";
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{item.Name,-26} {item.Cells,5}  {item.Width,5:0}  {rows,-10}  {item.Band,4}  {item.Gaps,4}  {item.TextBand,9}  {item.Line,8:0}  {item.Offset,10:+0.0;-0.0;0.0}  {item.Why ?? "one row"}");
        }
        return text.ToString();
    }

    /// <summary>
    /// The sheet: each entry on its card, its name over it, in lines as wide as a screen, on the
    /// bar's own colour so the cards are seen as the bar shows them.
    /// </summary>
    private static void Picture(Gfx gfx, FlyoutTheme theme, float thickness, List<(string Name, float Width, float Height, Action<IntPtr> Paint)> drawn, string path)
    {
        const float pad = 12f;
        const float label = 16f;
        const float most = 1900f;
        var places = new List<(float X, float Y)>();
        var x = pad;
        var y = pad;
        var widest = 0f;
        foreach (var (_, width, _, _) in drawn)
        {
            if (x > pad && x + width > most)
            {
                x = pad;
                y += label + thickness + pad;
            }
            places.Add((x, y));
            x += MathF.Max(width, 60f) + pad;
            widest = MathF.Max(widest, x);
        }
        var total = y + label + thickness + pad;
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(theme.Base));
            for (var index = 0; index < drawn.Count; index++)
            {
                var (name, width, height, paint) = drawn[index];
                var (left, top) = places[index];
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, theme.TextSecondary, left, top, MathF.Max(width, 60f));
                var card = top + label + MathF.Round((thickness - height) / 2f);
                PopupPaint.Fill(dc, PopupPaint.Rect(left, card, width, height), 4f, PopupPaint.Ink(theme.CardBackground));
                Offscreen.Shifted(dc, left, card, () => paint((IntPtr)dc));
            }
        }, bitmap => Offscreen.Save(bitmap, path));
    }
}
