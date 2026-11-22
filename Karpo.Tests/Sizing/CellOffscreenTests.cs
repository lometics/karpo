// The widget grid seen and measured with real islands: every island's Measure
// at the bar heights, scales and edges the dock allows lands on whole cells,
// and a contact sheet of a laid-out bar is drawn to a PNG at each height, dark
// and light, horizontal and vertical, with the grid drawn over it so a cell
// that a widget does not fill is visible at a glance.
//
// Both need Direct2D and a dispatcher queue, so they run only with
// KARPO_UI_TESTS=1; the sheets go to KARPO_PNG_DIR. Nothing is ever shown on
// the person's desktop.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.DirectX.DirectX;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using IDispatcherQueueController = TerraFX.Interop.WinRT.IDispatcherQueueController;

namespace Karpo.Tests.Sizing;

public sealed unsafe class CellOffscreenTests
{
    /// <summary>The bar heights the sheets are drawn at: the ends of the range and the sizes between.</summary>
    private static readonly int[] Heights = [28, 48, 72, 115, 160];

    private static string Pictures
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } named
                ? named
                : Path.Combine(Path.GetTempPath(), "karpo-tests", "pictures");
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    private static Look LookFor(bool dark, float textScale = 1f) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8))
    { TextScale = textScale };

    /// <summary>Windows' own "High Contrast Black": only the system's colours, so nothing blends.</summary>
    private static Look HighContrast() => LookFor(dark: true) with
    {
        HighContrast = true,
        HcWindow = Color.FromArgb(255, 0, 0, 0),
        HcText = Color.FromArgb(255, 255, 255, 255),
        HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
        HcHighlightText = Color.FromArgb(255, 0, 0, 0),
    };

    /// <summary>The themes a sheet is drawn in, and what its file is called.</summary>
    private static (string Name, Look Look)[] Themes =>
        [("dark", LookFor(true)), ("light", LookFor(false)), ("contrast", HighContrast()), ("bigtext", LookFor(true, 1.5f))];

    private static readonly SystemState Radios = new(
        char.ConvertFromUtf32(0xE873),
        char.ConvertFromUtf32(0xE994),
        NetworkBackGlyph: char.ConvertFromUtf32(0xE701),
        BluetoothGlyph: char.ConvertFromUtf32(0xE702),
        BluetoothOn: true);

    /// <summary>The widgets a sheet shows, in the order they stand on the bar, each with the width it was given.</summary>
    private static (string Id, BarKind Kind, int Width)[] Kinds =>
    [
        ("weather", BarKind.WidgetsBoard, WidgetItem.AutomaticWidth),
        ("folder", BarKind.FolderList, WidgetItem.AutomaticWidth),
        ("keyboard", BarKind.NightLight, WidgetItem.AutomaticWidth),
        ("sound", BarKind.FolderList, WidgetItem.AutomaticWidth),
        ("battery", BarKind.FolderList, WidgetItem.AutomaticWidth),
    ];

    /// <summary>
    /// The same widgets with a width chosen for each, to see a chosen width
    /// against the automatic one: a widget that measures wide squeezed into one,
    /// and small ones stretched.
    /// </summary>
    private static (string Id, BarKind Kind, int Width)[] Widths =>
    [
        ("weather", BarKind.WidgetsBoard, 1),
        ("folder", BarKind.FolderList, 2),
        ("keyboard", BarKind.NightLight, 1),
        ("sound", BarKind.FolderList, 2),
        ("battery", BarKind.FolderList, 3),
    ];

    /// <summary>
    /// The widget itself. Its width is not on it — a taskbar entry carries none (2026-10-08) — but on
    /// the part the layout is handed (PartSpec) and the style it is drawn with.
    /// </summary>
    private static BarItem Widget(string id, BarKind kind) => kind switch
    {
        // The sound: the sound preset's folder, wearing the speaker the sound button was.
        BarKind.FolderList when id == "sound" => Sound(id),
        // The battery: the battery preset's folder, wearing the charge the battery was.
        BarKind.FolderList when id == "battery" => Battery(id),
        BarKind.FolderList => new BarItem { Id = id, Kind = kind, Folder = new FolderOptions() },
        _ => new BarItem { Id = id, Kind = kind },
    };

    /// <summary>The sound preset's folder, its id <paramref name="id"/>.</summary>
    private static BarItem Sound(string id)
    {
        var folder = Presets.Folder(Presets.Find(Presets.SoundId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        folder.Id = id;
        return folder;
    }

    /// <summary>The notifications preset's folder, wearing the bell, its id <paramref name="id"/>.</summary>
    private static BarItem Bell(string id)
    {
        var folder = Presets.Folder(Presets.Find(Presets.NotificationsId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        folder.Id = id;
        return folder;
    }

    /// <summary>The battery preset's folder, its id <paramref name="id"/>.</summary>
    private static BarItem Battery(string id)
    {
        var folder = Presets.Folder(Presets.Find(Presets.BatteryId)!, new PresetMachine(HasBattery: true, FixedDrives: []));
        folder.Id = id;
        return folder;
    }

    private static IslandCollection Build(Gfx gfx, Config config, List<PartSpec> specs, (string Id, BarKind Kind, int Width)[] widgets)
    {
        var islands = new IslandCollection();
        foreach (var (id, kind, width) in widgets)
        {
            var key = Config.IslandKey(null, id);
            if (Karpo.Dock.Islands.Create(key, Widget(id, kind), gfx, config) is not { } island)
            {
                continue;
            }
            if (island is FolderIsland { ShowsStatus: true } folder)
            {
                folder.SetStatus(Radios, gfx);
            }
            // Read once, so a drives or screen-time cell shows what it will show.
            _ = island.Refresh(gfx);
            islands[key] = island;
            specs.Add(new PartSpec(key, kind, width));
        }
        return islands;
    }

    // --- Measuring -------------------------------------------------------------

    [Karpo.Tests.Islands.UiFact]
    public void EveryIslandMeasuresOneCellAtLeastOrItsExactContentInWholePixels()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in Heights)
            {
                var config = new Config { Height = height };
                foreach (var dpi in (uint[])[96, 120, 144])
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        foreach (var textScale in new[] { 1f, 2.25f })
                        {
                            var cell = Layout.CellPx(dpi, config);
                            var style = new IslandStyle(dpi / 96f, vertical, cell, Layout.IconPx(dpi, config), LookFor(true, textScale));
                            foreach (var (id, kind, width) in Kinds)
                            {
                                if (Karpo.Dock.Islands.Create(Config.IslandKey(null, id), Widget(id, kind), gfx, config) is not { } island)
                                {
                                    continue;
                                }
                                using (island)
                                {
                                    if (island is FolderIsland { ShowsStatus: true } folder)
                                    {
                                        folder.SetStatus(Radios, gfx);
                                    }
                                    _ = island.Refresh(gfx);
                                    var where = $"{kind} at {height} DIP, {dpi} DPI, text ×{textScale}{(vertical ? ", vertical" : "")}";
                                    var cells = island.Measure(style);
                                    for (var index = 0; index < cells.Count; index++)
                                    {
                                        var length = cells.Lengths is { } lengths && index < lengths.Count ? lengths[index] : cells.CellLength;
                                        var steps = length / cell;
                                        // A folder's button is exactly its faces and its ends (cells.Exact, §275); every
                                        // other widget is a cell at the least.
                                        Assert.True(cells.Exact == island is FolderIsland, $"{where}: exact {cells.Exact}");
                                        Assert.True(cells.Exact ? length >= 1f : steps >= 1f, $"{where}: cell {index} is {steps} cells");
                                        // As long as its content needs, not whole cells: whole pixels (docs 56).
                                        Assert.Equal(MathF.Round(length), length, 3);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });
    }

    // --- Contact sheets --------------------------------------------------------

    [Karpo.Tests.Islands.UiFact]
    public void BarContactSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in Heights)
            {
                foreach (var (name, look) in Themes)
                {
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                    {
                        Sheet(gfx, height, name, look, edge, Kinds);
                    }
                }
            }
        });
    }

    /// <summary>
    /// The same widgets with a width chosen for each, beside the automatic ones
    /// of <see cref="BarContactSheets"/>, at two bar heights: a chosen width is
    /// exactly that many cells, and its content fills them or shortens.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void ChosenWidthContactSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in new[] { 48, 96 })
            {
                foreach (var dark in new[] { true, false })
                {
                    Sheet(gfx, height, dark ? "widths-dark" : "widths-light", LookFor(dark), Edge.Bottom, Widths);
                }
            }
        });
    }

    /// <summary>
    /// The widgets whose state can be driven, drawn in each state side by side
    /// at the same size: a widget must not change size because its state
    /// changed, so each pair must be the same width.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void StateContactSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in new[] { 48, 96 })
            {
                foreach (var dark in new[] { true, false })
                {
                    StateSheet(gfx, height, dark);
                }
            }
        });
    }

    private static void StateSheet(Gfx gfx, int height, bool dark)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = new IslandStyle(dpi / 96f, false, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
        var folder = Path.Combine(Path.GetTempPath(), "karpo-tests", "sheet-" + Guid.NewGuid().ToString("N"));
        var made = new List<CellIsland>();
        try
        {
            // The bell with nothing waiting, with a few, and with more than it can count.
            foreach (var count in new[] { 0, 5, 150 })
            {
                var database = Path.Combine(folder, $"db{count}", "wpndatabase.db");
                Karpo.Tests.Islands.FlyoutOffscreenTests.BuildDatabase(database, count);
                var store = new NotificationStore(database, Path.Combine(folder, $"copy{count}"), Path.Combine(folder, $"state{count}"), app => (app, null));
                store.Prepare();
                var bell = Karpo.Tests.Islands.BadgeSizeTests.BellFace(gfx, store, $"/n{count}", config);
                _ = bell.Refresh(gfx);
                made.Add(bell);
            }
            // Wi-Fi and volume in a weak state and a strong one.
            SystemState[] states =
            [
                new("\uE871", "\uE992", NetworkBackGlyph: "\uE701", BluetoothGlyph: "\uE702", BluetoothOn: false),
                new("\uE701", "\uE995", NetworkBackGlyph: "\uE701", BluetoothGlyph: "\uE702", BluetoothOn: true),
            ];
            foreach (var state in states)
            {
                var sound = new FolderIsland($"/sound{state.BluetoothOn}", Sound("x"), gfx, config);
                sound.SetStatus(state, gfx);
                made.Add(sound);
            }

            var gap = MathF.Round(6 * style.Scale);
            var sizes = made.Select(island => island.CellSize(0, style)).ToList();
            var along = sizes.Sum(size => size.Width) + gap * (made.Count + 1);
            var across = layoutThickness(dpi, config);
            Picture(along, across, theme, $"states-{height}-{(dark ? "dark" : "light")}.png", dc =>
            {
                var at = gap;
                for (var index = 0; index < made.Count; index++)
                {
                    var (width, cellHeight) = sizes[index];
                    var top = MathF.Round((across - cellHeight) / 2);
                    PopupPaint.Fill(dc, PopupPaint.Rect(at, top, width, cellHeight), MathF.Round(6 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                    var island = made[index];
                    var x = at;
                    Shifted(dc, x, top, () => island.PaintCell(dc, 0, style, width, cellHeight));
                    var line = dark ? Gfx.Ink(1, 0.4f, 0.4f, 0.7f) : Gfx.Ink(0.8f, 0, 0, 0.6f);
                    PopupPaint.Fill(dc, PopupPaint.Rect(at, top, 1, cellHeight), 0, line);
                    PopupPaint.Fill(dc, PopupPaint.Rect(at + width - 1, top, 1, cellHeight), 0, line);
                    at += width + gap;
                }
            });
        }
        finally
        {
            foreach (var island in made)
            {
                island.Dispose();
            }
        }

        static float layoutThickness(uint dpi, Config config) => Layout.ThicknessPx(dpi, config);
    }

    /// <summary>
    /// What Automatic costs now, beside a chosen width: the weather in each
    /// unit at its automatic size (glyph and temperature) and at four cells
    /// (the words come back and fill them), the calendar's day alone and at
    /// four cells, and their neighbours for the bar's rhythm. Offscreen there
    /// is no network, so the weather reads its not-yet-fetched state; the
    /// widths are what the sheet is for.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void AutomaticSizeSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in new[] { 48, 72 })
            {
                foreach (var dark in new[] { true, false })
                {
                    MinimalSheet(gfx, height, dark);
                }
            }
        });
    }

    private static void MinimalSheet(Gfx gfx, int height, bool dark)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var cell = Layout.CellPx(dpi, config);
        (string Id, BarItem Widget, int Width)[] wanted =
        [
            ("c", new BarItem { Id = "c", Kind = BarKind.WidgetsBoard, Weather = new WeatherOptions { Place = "Lisbon", Latitude = 38.7, Longitude = -9.1 } }, 0),
            ("f", new BarItem { Id = "f", Kind = BarKind.WidgetsBoard, Weather = new WeatherOptions { Place = "Boston", Latitude = 42.4, Longitude = -71.1 } }, 0),
            ("w4", new BarItem { Id = "w4", Kind = BarKind.WidgetsBoard, Weather = new WeatherOptions { Place = "Lisbon", Latitude = 38.7, Longitude = -9.1 } }, 4),
            ("bat", Presets.Folder(Presets.Find(Presets.BatteryId)!, new PresetMachine(HasBattery: true, FixedDrives: [])), 0),
            ("kb", new BarItem { Id = "kb", Kind = BarKind.NightLight }, 0),
        ];
        var made = new List<(CellIsland Island, IslandStyle Style)>();
        try
        {
            foreach (var (id, widget, width) in wanted)
            {
                if (Karpo.Dock.Islands.Create(Config.IslandKey(null, id), widget, gfx, config) is CellIsland island)
                {
                    _ = island.Refresh(gfx);
                    made.Add((island, new IslandStyle(dpi / 96f, false, cell, Layout.IconPx(dpi, config), look) { Width = width }));
                }
            }
            var gap = MathF.Round(6 * (dpi / 96f));
            var lengths = made.Select(entry =>
            {
                var along = 0f;
                for (var at = 0; at < entry.Island.CellCount; at++)
                {
                    along += entry.Island.CellSize(at, entry.Style).Width;
                }
                return along;
            }).ToList();
            var total = lengths.Sum() + gap * (made.Count + 1);
            var across = cell + 2 * gap;

            Picture(total, across, theme, $"automatic-{height}-{(dark ? "dark" : "light")}.png", dc =>
            {
                var at = gap;
                for (var index = 0; index < made.Count; index++)
                {
                    var (island, style) = made[index];
                    var along = lengths[index];
                    PopupPaint.Fill(dc, PopupPaint.Rect(at, gap, along, cell), MathF.Round(6 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                    var step = at;
                    for (var part = 0; part < island.CellCount; part++)
                    {
                        var (width, cellHeight) = island.CellSize(part, style);
                        var x = step;
                        Shifted(dc, x, gap, () => island.PaintCell(dc, part, style, width, cellHeight));
                        step += width;
                    }
                    var line = dark ? Gfx.Ink(1, 0.4f, 0.4f, 0.7f) : Gfx.Ink(0.8f, 0, 0, 0.6f);
                    for (var mark = 0f; mark <= along + 0.5f; mark += cell)
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(at + MathF.Min(mark, along - 1), gap, 1, cell), 0, line);
                    }
                    at += along + gap;
                }
            });
        }
        finally
        {
            foreach (var (island, _) in made)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>
    /// The timer's shell at one, two and three cells, and beside it the widgets
    /// that are a glyph in a cell — a folder showing no name, a switch, the
    /// Wi-Fi button — which must each be exactly one cell square.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void WidthAndSquareSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in new[] { 28, 48, 72 })
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var textScale in new[] { 1f, 2f })
                    {
                        SquareSheet(gfx, height, dark, textScale);
                    }
                }
            }
        });
    }

    private static void SquareSheet(Gfx gfx, int height, bool dark, float textScale)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark, textScale);
        var theme = FlyoutTheme.For(look);
        var cell = Layout.CellPx(dpi, config);
        (string Id, BarItem Widget, int Width)[] wanted =
        [
            ("s1", new BarItem { Id = "s1", Kind = BarKind.NightLight }, 1),
            ("s2", new BarItem { Id = "s2", Kind = BarKind.NightLight }, 2),
            ("s3", new BarItem { Id = "s3", Kind = BarKind.NightLight }, 3),
            ("fo", new BarItem { Id = "fo", Kind = BarKind.FolderList, Folder = new FolderOptions() }, 0),
            ("sw", new BarItem { Id = "sw", Kind = BarKind.NightLight }, 0),
            ("so", Sound("so"), 0),
            ("no", Bell("no"), 0),
        ];
        var made = new List<(CellIsland Island, IslandStyle Style)>();
        try
        {
            foreach (var (id, widget, width) in wanted)
            {
                if (Karpo.Dock.Islands.Create(Config.IslandKey(null, id), widget, gfx, config) is CellIsland island)
                {
                    _ = island.Refresh(gfx);
                    made.Add((island, new IslandStyle(dpi / 96f, false, cell, Layout.IconPx(dpi, config), look) { Width = width }));
                }
            }
            var gap = MathF.Round(6 * (dpi / 96f));
            var lengths = made.Select(entry =>
            {
                var along = 0f;
                for (var at = 0; at < entry.Island.CellCount; at++)
                {
                    along += entry.Island.CellSize(at, entry.Style).Width;
                }
                return along;
            }).ToList();
            var total = lengths.Sum() + (gap * (made.Count + 1));
            var across = cell + (2 * gap);
            var name = $"widths-{height}-{(dark ? "dark" : "light")}{(textScale > 1 ? "-bigtext" : "")}.png";

            Picture(total, across, theme, name, dc =>
            {
                var at = gap;
                for (var index = 0; index < made.Count; index++)
                {
                    var (island, style) = made[index];
                    var along = lengths[index];
                    PopupPaint.Fill(dc, PopupPaint.Rect(at, gap, along, cell), MathF.Round(6 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                    var step = at;
                    for (var part = 0; part < island.CellCount; part++)
                    {
                        var (cellWidth, cellHeight) = island.CellSize(part, style);
                        var x = step;
                        Shifted(dc, x, gap, () => island.PaintCell(dc, part, style, cellWidth, cellHeight));
                        step += cellWidth;
                    }
                    // A tick at every cell step: a square widget shows as one step.
                    var line = dark ? Gfx.Ink(1, 0.4f, 0.4f, 0.7f) : Gfx.Ink(0.8f, 0, 0, 0.6f);
                    for (var mark = 0f; mark <= along + 0.5f; mark += cell)
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(at + MathF.Min(mark, along - 1), gap, 1, cell), 0, line);
                    }
                    at += along + gap;
                }
            });
        }
        finally
        {
            foreach (var (island, _) in made)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>
    /// Every kind the catalogue offers, on a vertical bar, at the widths and
    /// text scales a bar can be: the evidence for which widgets read on a
    /// vertical taskbar and which are the horizontal design squeezed into a
    /// narrow column. One row per kind, so a widget whose content runs out of
    /// the bar's width shows it.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void VerticalAuditSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var width in new[] { 28, 48, 72, 160 })
            {
                foreach (var textScale in new[] { 1f, 2f })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        if (textScale > 1 && !dark)
                        {
                            continue;
                        }
                        AuditSheet(gfx, width, dark, textScale);
                    }
                }
            }
        });
    }

    private static void AuditSheet(Gfx gfx, int width, bool dark, float textScale)
    {
        const uint dpi = 96;
        var config = new Config { Height = width };
        var look = LookFor(dark, textScale);
        var theme = FlyoutTheme.For(look);
        var cell = Layout.CellPx(dpi, config);
        var style = new IslandStyle(dpi / 96f, true, cell, Layout.IconPx(dpi, config), look);
        var made = new List<(BarKind Kind, CellIsland Island)>();
        try
        {
            foreach (var kind in BarEntries.All.Select(entry => entry.Kind))
            {
                var widget = new BarItem { Id = kind.ToString(), Kind = kind };
                widget.Normalize();
                if (kind == BarKind.WidgetsBoard)
                {
                    widget.Weather = new WeatherOptions { Place = "Lisbon", Latitude = 38.7, Longitude = -9.1 };
                }
                // A kind the catalogue offers whose island is not built yet is
                // a real state while a kind is being added: the audit skips it
                // rather than failing, since it is a picture and not a check.
                CellIsland? island = null;
                try
                {
                    island = Karpo.Dock.Islands.Create(Config.IslandKey(null, widget.Id), widget, gfx, config) as CellIsland;
                }
                catch (ArgumentOutOfRangeException)
                {
                    continue;
                }
                if (island is not null)
                {
                    _ = island.Refresh(gfx);
                    if (island.CellCount > 0)
                    {
                        made.Add((kind, island));
                    }
                }
            }
            var gap = MathF.Round(8 * (dpi / 96f));
            // One column per kind, each the bar's own width, the widget down it.
            var lengths = made.Select(entry =>
            {
                var along = 0f;
                for (var at = 0; at < entry.Island.CellCount; at++)
                {
                    along += entry.Island.CellSize(at, style).Height;
                }
                return along;
            }).ToList();
            var tallest = lengths.Count > 0 ? lengths.Max() : cell;
            var across = ((cell + gap) * made.Count) + gap;
            var down = tallest + (2 * gap);

            Picture(across, down, theme, $"vertical-audit-{width}{(textScale > 1 ? "-bigtext" : "")}-{(dark ? "dark" : "light")}.png", dc =>
            {
                var at = gap;
                for (var index = 0; index < made.Count; index++)
                {
                    var (_, island) = made[index];
                    var along = lengths[index];
                    PopupPaint.Fill(dc, PopupPaint.Rect(at, gap, cell, along), MathF.Round(6 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                    var step = gap;
                    for (var part = 0; part < island.CellCount; part++)
                    {
                        var (cellWidth, cellHeight) = island.CellSize(part, style);
                        var y = step;
                        Shifted(dc, at, y, () => island.PaintCell(dc, part, style, cellWidth, cellHeight));
                        step += cellHeight;
                    }
                    var line = dark ? Gfx.Ink(1, 0.4f, 0.4f, 0.7f) : Gfx.Ink(0.8f, 0, 0, 0.6f);
                    for (var mark = 0f; mark <= along + 0.5f; mark += cell)
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(at, gap + MathF.Min(mark, along - 1), cell, 1), 0, line);
                    }
                    at += cell + gap;
                }
            });
        }
        finally
        {
            foreach (var (_, island) in made)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>
    /// The kinds that reserve more than they draw, each in its cell with a line
    /// down the cell's centre: what is drawn must straddle the line whatever it
    /// says, so a short value and a long one both read as centred.
    /// </summary>
    [Karpo.Tests.Islands.UiFact]
    public void CentringSheets()
    {
        OnStaThread(gfx =>
        {
            foreach (var height in new[] { 28, 48 })
            {
                foreach (var dark in new[] { true, false })
                {
                    CentringSheet(gfx, height, dark);
                }
            }
        });
    }

    private static void CentringSheet(Gfx gfx, int height, bool dark)
    {
        const uint dpi = 96;
        var config = new Config { Height = height };
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var cell = Layout.CellPx(dpi, config);
        var style = new IslandStyle(dpi / 96f, false, cell, Layout.IconPx(dpi, config), look);
        (string Id, BarKind Kind)[] kinds =
        [
            ("bat", BarKind.EnergySaver),
            ("kb", BarKind.NightLight),
            ("we", BarKind.WidgetsBoard),
        ];
        var made = new List<CellIsland>();
        try
        {
            foreach (var (id, kind) in kinds)
            {
                var widget = new BarItem { Id = id, Kind = kind };
                widget.Normalize();
                if (kind == BarKind.WidgetsBoard)
                {
                    widget.Weather = new WeatherOptions { Place = "Lisbon", Latitude = 38.7, Longitude = -9.1 };
                }
                if (Karpo.Dock.Islands.Create(Config.IslandKey(null, id), widget, gfx, config) is CellIsland island)
                {
                    _ = island.Refresh(gfx);
                    made.Add(island);
                }
            }
            var gap = MathF.Round(8 * (dpi / 96f));
            var lengths = made.Select(island =>
            {
                var along = 0f;
                for (var at = 0; at < island.CellCount; at++)
                {
                    along += island.CellSize(at, style).Width;
                }
                return along;
            }).ToList();
            var total = lengths.Sum() + (gap * (made.Count + 1));
            var across = cell + (2 * gap);

            Picture(total, across, theme, $"centring-{height}-{(dark ? "dark" : "light")}.png", dc =>
            {
                var at = gap;
                for (var index = 0; index < made.Count; index++)
                {
                    var island = made[index];
                    var along = lengths[index];
                    PopupPaint.Fill(dc, PopupPaint.Rect(at, gap, along, cell), MathF.Round(6 * style.Scale), PopupPaint.Ink(theme.CardBackground));
                    var step = at;
                    for (var part = 0; part < island.CellCount; part++)
                    {
                        var (cellWidth, cellHeight) = island.CellSize(part, style);
                        var x = step;
                        Shifted(dc, x, gap, () => island.PaintCell(dc, part, style, cellWidth, cellHeight));
                        // The cell's own centre line: the ink must straddle it.
                        var line = dark ? Gfx.Ink(1, 0.4f, 0.4f, 0.8f) : Gfx.Ink(0.8f, 0, 0, 0.7f);
                        PopupPaint.Fill(dc, PopupPaint.Rect(x + MathF.Round(cellWidth / 2), gap, 1, cell), 0, line);
                        step += cellWidth;
                    }
                    at += along + gap;
                }
            });
        }
        finally
        {
            foreach (var island in made)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>One sheet: the bar at <paramref name="height"/> DIP with the cell grid drawn over it.</summary>
    private static void Sheet(Gfx gfx, int height, string themeName, Look look, Edge edge, (string Id, BarKind Kind, int Width)[] widgets)
    {
        const uint dpi = 96;
        // The sheet paints a card per widget, which is the islands look.
        var config = new Config { Height = height, Style = BarStyle.Islands };
        var dark = look.Dark;
        var vertical = edge.IsVertical();
        var specs = new List<PartSpec>();
        var islands = Build(gfx, config, specs, widgets);
        var area = vertical
            ? new RECT { left = 0, top = 0, right = 1200, bottom = 2600 }
            : new RECT { left = 0, top = 0, right = 2600, bottom = 1200 };
        var content = new Content { Islands = islands };
        var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
        var theme = FlyoutTheme.For(look);

        // Only the stretch the widgets take, so a sheet is the widgets' size.
        var first = layout.Parts.Where(part => part.Width > 0).Min(part => part.Left);
        var last = layout.Parts.Where(part => part.Width > 0).Max(part => part.Right);
        var along = last - first;
        var across = layout.Thickness;
        var name = $"bar-{height}-{themeName}{(vertical ? "-vertical" : "")}.png";

        Picture(vertical ? across : along, vertical ? along : across, theme, name, dc =>
        {
            // The bar's own corner in the window (it keeps room for tooltips on
            // its inner side), and the first widget's start along it.
            var origin = new Vector2(
                -(layout.PanelRect.X + (vertical ? 0 : first)),
                -(layout.PanelRect.Y + (vertical ? first : 0)));
            RectF Moved(RectF rect) => rect with { X = rect.X + origin.X, Y = rect.Y + origin.Y };

            for (var index = 0; index < layout.Parts.Count; index++)
            {
                var part = layout.Parts[index];
                if (part.Width <= 0f)
                {
                    continue;
                }
                var card = layout.CardRect(index) is { IsEmpty: false } rect ? rect : layout.BlockRect(index);
                if (card.IsEmpty)
                {
                    continue;
                }
                card = Moved(card);
                PopupPaint.Fill(dc, PopupPaint.Rect(card.X, card.Y, card.Width, card.Height), layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                if (!islands.TryGetValue(part.Key, out var island) || island is not CellIsland cells)
                {
                    continue;
                }
                var style = layout.StyleFor(index, look);
                for (var cell = 0; cell < part.Count; cell++)
                {
                    if (layout.ItemRect(Hit.Cell(index, cell)) is not { IsEmpty: false } box)
                    {
                        continue;
                    }
                    box = Moved(box);
                    var (width, cellHeight) = cells.CellSize(cell, style);
                    Shifted(dc, box.X, box.Y, () => cells.PaintCell(dc, cell, style, width, cellHeight));
                }
            }

            // The grid, per widget: a hairline at every cell step from the
            // widget's own start, the last of them on its end. A widget that is
            // not a whole number of cells ends between two marks, which is the
            // thing to look for. The room between widgets is not on the grid —
            // each widget starts its own.
            var line = dark ? Gfx.Ink(1, 0.4f, 0.4f, 0.7f) : Gfx.Ink(0.8f, 0, 0, 0.6f);
            for (var index = 0; index < layout.Parts.Count; index++)
            {
                var part = layout.Parts[index];
                if (part.Width <= 0f || !part.Kind.IsIsland())
                {
                    continue;
                }
                var block = Moved(layout.BlockRect(index));
                if (block.IsEmpty)
                {
                    continue;
                }
                var from = vertical ? block.Y : block.X;
                for (var step = 0f; step <= part.Width + 0.5f; step += layout.CellSide)
                {
                    var at = from + step;
                    var mark = vertical
                        ? PopupPaint.Rect(block.X, at, block.Width, 1)
                        : PopupPaint.Rect(at, block.Y, 1, block.Height);
                    PopupPaint.Fill(dc, mark, 0, line);
                }
            }
        });

        foreach (var island in islands.Values)
        {
            island.Dispose();
        }
    }

    /// <summary>
    /// The desktop selectors: each button's fill (the one in front in the
    // --- Drawing and threading -------------------------------------------------

    private delegate void Painter(ID2D1DeviceContext* dc);

    private static void Picture(float width, float height, FlyoutTheme theme, string file, Painter content)
    {
        const int margin = 12;
        var w = (uint)MathF.Ceiling(width) + 2 * margin;
        var h = (uint)MathF.Ceiling(height) + 2 * margin;
        ID2D1Factory1* factory = null;
        IWICImagingFactory* wic = null;
        IWICBitmap* bitmap = null;
        ID2D1RenderTarget* target = null;
        ID2D1DeviceContext* dc = null;
        try
        {
            ComUtil.Check(D2D1CreateFactory(D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED, __uuidof<ID2D1Factory1>(), (void**)&factory));
            var clsid = CLSID.CLSID_WICImagingFactory2;
            ComUtil.Check(CoCreateInstance(&clsid, null, (uint)CLSCTX.CLSCTX_INPROC_SERVER, __uuidof<IWICImagingFactory>(), (void**)&wic));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(wic->CreateBitmap(w, h, &format, WICBitmapCreateCacheOption.WICBitmapCacheOnLoad, &bitmap));
            var properties = new D2D1_RENDER_TARGET_PROPERTIES
            {
                type = D2D1_RENDER_TARGET_TYPE.D2D1_RENDER_TARGET_TYPE_SOFTWARE,
                pixelFormat = new D2D1_PIXEL_FORMAT { format = DXGI_FORMAT.DXGI_FORMAT_B8G8R8A8_UNORM, alphaMode = D2D1_ALPHA_MODE.D2D1_ALPHA_MODE_PREMULTIPLIED },
                dpiX = 96,
                dpiY = 96,
            };
            ComUtil.Check(((ID2D1Factory*)factory)->CreateWicBitmapRenderTarget(bitmap, &properties, &target));
            ComUtil.Check(target->QueryInterface(__uuidof<ID2D1DeviceContext>(), (void**)&dc));
            dc->BeginDraw();
            var backdrop = theme.Dark ? new DXGI_RGBA(0.10f, 0.12f, 0.16f, 1) : new DXGI_RGBA(0.80f, 0.84f, 0.90f, 1);
            dc->Clear(&backdrop);
            // The bar itself, so a widget's card is seen against it.
            PopupPaint.Fill(dc, new D2D_RECT_F(margin, margin, margin + width, margin + height), 0, PopupPaint.Ink(theme.Base));
            var context = dc;
            Shifted(context, margin, margin, () => content(context));
            ComUtil.Check(dc->EndDraw(null, null));
            Save(wic, bitmap, Path.Combine(Pictures, file));
        }
        finally
        {
            ComUtil.Release(ref dc);
            ComUtil.Release(ref target);
            ComUtil.Release(ref bitmap);
            ComUtil.Release(ref wic);
            ComUtil.Release(ref factory);
        }
    }

    private static void Shifted(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = x * before._11 + y * before._21 + before._31;
        moved._32 = x * before._12 + y * before._22 + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    private static void Save(IWICImagingFactory* wic, IWICBitmap* bitmap, string path)
    {
        IWICStream* stream = null;
        IWICBitmapEncoder* encoder = null;
        IWICBitmapFrameEncode* frame = null;
        try
        {
            ComUtil.Check(wic->CreateStream(&stream));
            fixed (char* name = path)
            {
                ComUtil.Check(stream->InitializeFromFilename(name, GENERIC_WRITE));
            }
            var png = GUID.GUID_ContainerFormatPng;
            ComUtil.Check(wic->CreateEncoder(&png, null, &encoder));
            ComUtil.Check(encoder->Initialize((IStream*)stream, WICBitmapEncoderCacheOption.WICBitmapEncoderNoCache));
            ComUtil.Check(encoder->CreateNewFrame(&frame, null));
            ComUtil.Check(frame->Initialize(null));
            uint width;
            uint height;
            ComUtil.Check(bitmap->GetSize(&width, &height));
            ComUtil.Check(frame->SetSize(width, height));
            var format = GUID.GUID_WICPixelFormat32bppPBGRA;
            ComUtil.Check(frame->SetPixelFormat(&format));
            ComUtil.Check(frame->WriteSource((IWICBitmapSource*)bitmap, null));
            ComUtil.Check(frame->Commit());
            ComUtil.Check(encoder->Commit());
        }
        finally
        {
            ComUtil.Release(ref frame);
            ComUtil.Release(ref encoder);
            ComUtil.Release(ref stream);
        }
    }

    /// <summary>Runs <paramref name="body"/> on a thread with a dispatcher queue (the compositor needs one) and a Gfx.</summary>
    private static void OnStaThread(Action<Gfx> body)
    {
        Exception? failed = null;
        var thread = new Thread(() =>
        {
            try
            {
                var options = new DispatcherQueueOptions
                {
                    dwSize = (uint)sizeof(DispatcherQueueOptions),
                    threadType = DISPATCHERQUEUE_THREAD_TYPE.DQTYPE_THREAD_CURRENT,
                    apartmentType = DISPATCHERQUEUE_THREAD_APARTMENTTYPE.DQTAT_COM_STA,
                };
                IDispatcherQueueController* controller = null;
                ComUtil.Check(CreateDispatcherQueueController(options, &controller));
                try
                {
                    using var gfx = new Gfx(gpu: false);
                    body(gfx);
                }
                finally
                {
                    controller->Release();
                }
            }
            catch (Exception error)
            {
                failed = error;
            }
        });
        thread.Start();
        thread.Join();
        if (failed is not null)
        {
            throw new Xunit.Sdk.XunitException("On the offscreen thread: " + failed);
        }
    }
}
