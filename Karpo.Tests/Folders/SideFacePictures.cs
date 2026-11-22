// A bar on a side (the person, 2026-10-13: "the clock widget when on the side overflows, it should show
// minutes and hours on a different row. also make sure that all the widgets look good on the side as
// well. if possible dont try to flip text"): every face of words down a bar standing on the left or the
// right — the clock in each of its formats, the time over the date, the calendar, the weather, the
// timers, the stock, the mail's count, the input indicator — at every thickness from the thinnest a
// side bar is to a wide one, at 100 and 150 %, dark and light, on an islands bar and in a floating
// dock's panel. Down a side bar a widget is as wide as the bar is thick and grows along it: its text
// stacks onto rows, or is shortened, or set smaller, but is never turned, never cut and never runs
// past its cell; and it stands in the middle of the bar.
//
// The sheets (side-faces-<islands|floating>-<dark|light>-<100|150>.png) go to KARPO_PNG_DIR: a column
// per thickness, each a strip of the bar with the face's cell in it, a row per face.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

public sealed unsafe class SideFacePictures
{
    /// <summary>The thicknesses a side bar is drawn at: the thinnest the icon allows, the taskbar's, and wide ones.</summary>
    private static readonly int[] Thicknesses = [32, 40, 48, 64, 96, 128];

    private static readonly DateTime Evening = new(2026, 9, 30, 22, 58, 59);

    private static IslandStyle Side(int thickness, uint dpi, Look look, bool floating)
    {
        var config = new Config { Height = thickness, Style = floating ? BarStyle.Floating : BarStyle.Islands };
        return new IslandStyle(dpi / 96f, Vertical: true, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look) { Square = floating };
    }

    private static float BarPx(int thickness, uint dpi, bool floating) =>
        Layout.ThicknessPx(dpi, new Config { Height = thickness, Style = floating ? BarStyle.Floating : BarStyle.Islands });

    /// <summary>Every face of words, and every format of each, made on this thread over stand-ins.</summary>
    private static List<(string Name, FolderIsland Island)> Faces(Gfx gfx)
    {
        var faces = new List<(string, FolderIsland)>();
        void Add(string name, BarItem folder) => faces.Add((name, FolderFaceTests.Make(gfx, folder)));
        Add("clock", FolderFaceTests.Folder(FolderFaces.ClockId));
        Add("clock 12h", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve)));
        Add("clock 24h", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.TwentyFour)));
        Add("clock 12h + s", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true")));
        Add("clock 24h + s", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.TwentyFour), (ClockFace.Seconds, "true")));
        Add("clock, Tokyo", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Label, "Tokyo")));
        Add("clock, timer", FolderFaceTests.Wearing(FolderFaces.ClockId, timer: true));
        Add("clock, label + timer", FolderFaceTests.Wearing(FolderFaces.ClockId, timer: true, options: [(ClockFace.Hours, ClockFace.Twelve), (ClockFace.Label, "San Francisco")]));
        Add("time and date", FolderFaceTests.Folder(FolderFaces.DateTimeId));
        Add("time and date + s", FolderFaceTests.Folder(FolderFaces.DateTimeId, (DateTimeFace.Seconds, DateTimeFace.Shown)));
        Add("calendar", FolderFaceTests.Folder(FolderFaces.CalendarId));
        Add("calendar day + date", FolderFaceTests.Folder(FolderFaces.CalendarId, (CalendarFace.Form, CalendarFace.DayAndDate)));
        Add("calendar page", FolderFaceTests.Folder(FolderFaces.CalendarId, (CalendarFace.Look, CalendarFace.Page)));
        Add("timer", FolderFaceTests.Wearing(FolderFaces.TimerId, timer: true));
        faces.Add(("timer, running", Running(gfx, FolderFaceTests.Wearing(FolderFaces.TimerId, timer: true))));
        faces.Add(("clock + timer, running", Running(gfx, FolderFaceTests.Wearing(FolderFaces.ClockId, timer: true, options: [(ClockFace.Label, "Tea")]))));
        Add("focus", FolderFaceTests.Folder(FolderFaces.FocusId));
        Add("stopwatch", FolderFaceTests.Folder(FolderFaces.StopwatchId));
        Add("mail", FolderFaceTests.Folder(FolderFaces.MailId));
        foreach (var look in new[] { WeatherFace.Simple, WeatherFace.Detailed, WeatherFace.Forecast })
        {
            faces.Add(($"weather {look}", WeatherFaceTests.Make(gfx, WeatherFaceTests.Folder(look), SavedWeather.Cache())));
        }
        foreach (var change in new[] { true, false })
        {
            faces.Add((change ? "stock" : "stock, no change", Stock(gfx, change)));
        }
        faces.Add(("keyboard", Keyboard(gfx)));
        return faces;
    }

    /// <summary><paramref name="folder"/> with its timer running, 4:59 left at the sheets' moment.</summary>
    private static FolderIsland Running(Gfx gfx, BarItem folder)
    {
        var countdowns = new Countdowns();
        countdowns.Start("timer", CountdownKind.Timer, TimeSpan.FromMinutes(5), Evening.AddSeconds(-1));
        return new FolderIsland(Config.IslandKey(null, folder.Id), folder, gfx, new Config(), countdowns);
    }

    private static FolderIsland Stock(Gfx gfx, bool change)
    {
        var holder = Presets.Folder(Presets.Find(Presets.MarketId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        holder.Id = change ? "stock" : "stock-plain";
        holder.Folder!.Face.Market = MarketOffscreenTests.Held("MSFT");
        if (!change)
        {
            holder.Folder.Face.Market.Style = MarketStyle.Compact;
        }
        holder.Normalize();
        MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 4123.34, percent: -11.24, previous: 4646.3) });
        try
        {
            var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            MarketIsland.Made = null;
        }
    }

    private static FolderIsland Keyboard(Gfx gfx)
    {
        QuickPageTile.Sources = new QuickSources
        {
            Wifi = static () => new SampleWifi(),
            Bluetooth = static () => new SampleBluetooth(),
            Battery = static () => new SampleBattery(),
            Desktops = static () => new SampleDesktopNames(),
            Keyboard = static () => new SampleKeyboard(),
        };
        var holder = Presets.Folder(Presets.Find(Presets.KeyboardId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        return new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
    }

    /// <summary>
    /// Down a side bar every face of words draws inside its cell, never past it across the bar, and
    /// in its middle, at every thickness, at 100 and 150 %, on an islands bar and in a floating dock.
    /// </summary>
    [UiFact]
    public void EveryFaceFitsAcrossASideBar()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Evening;
            var faces = Faces(gfx);
            try
            {
                foreach (var (name, island) in faces)
                {
                    foreach (var floating in new[] { false, true })
                    {
                        foreach (var dpi in new uint[] { 96, 144 })
                        {
                            foreach (var thickness in Thicknesses)
                            {
                                var where = $"{name} at {thickness} DIP, {dpi} DPI{(floating ? ", floating" : "")}";
                                failures.AddRange(Check(island, Side(thickness, dpi, LookFor(dark: true), floating), where, badged: name == "mail" || name.StartsWith("stock", StringComparison.Ordinal)));
                            }
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
                QuickPageTile.Sources = null;
                foreach (var (_, island) in faces)
                {
                    island.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} faces do not fit across a side bar:\n{string.Join("\n", failures.Take(60))}");
    }

    private static List<string> Check(FolderIsland island, IslandStyle style, string where, bool badged = false)
    {
        var failures = new List<string>();
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        if (MathF.Abs(width - style.Cell) > 0.5f)
        {
            failures.Add($"{where}: {width:0.#} px across a {style.Cell:0.#} px bar");
        }
        const float margin = 24f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, style, width, height)),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
        if (right <= left)
        {
            failures.Add($"{where}: draws nothing");
            return failures;
        }
        if (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f)
        {
            failures.Add($"{where}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
        }
        // A count on a glyph's corner (the mail's) and a chart behind the words (the stock's) stand off
        // the middle by design: what is centred is the glyph, the words.
        var off = ((left + right) / 2) - (width / 2);
        if (!badged && MathF.Abs(off) > MathF.Max(1.5f, 0.05f * width))
        {
            failures.Add($"{where}: its ink's middle is {off:+0.0;-0.0} px off the bar's ({left:0.#}-{right:0.#} of {width:0.#})");
        }
        return failures;
    }

    /// <summary>
    /// Down a side bar the clock's rows — its time's parts, its label's words, a countdown — and the
    /// time-and-date's are each measured to fit across the room a line has, in every format, at every
    /// thickness, at 100 and 150 % and at 100 and 150 % text; and the clock has hours and minutes on
    /// rows of their own, never one turned line (2026-10-13: "it should show minutes and hours on a
    /// different row").
    /// </summary>
    [UiFact]
    public void TheClocksRowsFitAcrossEverySideBar()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var faces = new List<(string Name, BarItem Folder)>
            {
                ("clock", FolderFaceTests.Folder(FolderFaces.ClockId)),
                ("12h + s", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true"))),
                ("24h + s", FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.TwentyFour), (ClockFace.Seconds, "true"))),
                ("label + timer", FolderFaceTests.Wearing(FolderFaces.ClockId, timer: true, options: [(ClockFace.Label, "New York")])),
            };
            var late = new DateTime(2026, 12, 30, 23, 59, 59);
            foreach (var text in new[] { 1f, 1.5f })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var thickness in Thicknesses)
                    {
                        var style = Side(thickness, dpi, LookFor(dark: true, text), floating: false);
                        var fit = new Fit(style);
                        var room = VerticalFit.Room(fit);
                        foreach (var (name, folder) in faces)
                        {
                            var where = $"{name} at {thickness} DIP, {dpi} DPI, text ×{text}";
                            var plan = ClockFaceDrawing.Side(new FaceContext(gfx, style, folder.Folder!));
                            var parts = VerticalFit.Parts(ClockFaceDrawing.Time(ClockFace.Read(folder.Folder!.Face), late));
                            if (plan.TimeRows < 2 || parts.Length != plan.TimeRows)
                            {
                                failures.Add($"{where}: {plan.TimeRows} rows for {string.Join(" | ", parts)}");
                            }
                            failures.AddRange(parts
                                .Where(part => TextLine.Template(gfx, part, plan.TimePx, Weight.Regular).Width > room)
                                .Select(part => $"{where}: \"{part}\" is wider than the {room:0.#} px room at {plan.TimePx:0.#} px"));
                            // A word wider than the bar at 9 DIP is the one thing a line may cut; rows of words never are.
                            failures.AddRange(plan.Label
                                .Where(row => row.Contains(' ', StringComparison.Ordinal) && TextLine.Template(gfx, row, plan.TailPx, Weight.Regular).Width > room)
                                .Select(row => $"{where}: the label's row \"{row}\" is wider than the {room:0.#} px room"));
                            if (plan.TailPx > plan.TimePx)
                            {
                                failures.Add($"{where}: the time at {plan.TimePx:0.#} px, what follows it at {plan.TailPx:0.#} px");
                            }
                        }
                        var both = DateTimeFaceDrawing.Plan(gfx, style, FolderFace.Of(FolderFaces.DateTimeId));
                        var rows = VerticalFit.Parts(ClockFaceDrawing.Time(DateTimeFaceDrawing.Settings(FolderFace.Of(FolderFaces.DateTimeId)), late));
                        failures.AddRange(rows
                            .Where(part => TextLine.Template(gfx, part, both.TimePx, Weight.Regular).Width > room)
                            .Select(part => $"time and date at {thickness} DIP, {dpi} DPI, text ×{text}: \"{part}\" is wider than the {room:0.#} px room"));
                        if (both.TimeRows != rows.Length)
                        {
                            failures.Add($"time and date at {thickness} DIP: {both.TimeRows} rows for {string.Join(" | ", rows)}");
                        }
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} rows do not fit:\n{string.Join("\n", failures.Take(40))}");
    }

    /// <summary>A time's parts are its rows: the separators go, a designator is a row of its own.</summary>
    [Theory]
    [InlineData("22:58", "22|58")]
    [InlineData("22∶58∶59", "22|58|59")]
    [InlineData("10:58 PM", "10|58|PM")]
    [InlineData("午后10:58", "午后|10|58")]
    [InlineData("9.05 a.m.", "9|05|a|m")]
    public void ATimesPartsAreItsRows(string time, string rows) =>
        Assert.Equal(rows.Split('|'), VerticalFit.Parts(time));

    /// <summary>The sheets: a column per thickness, a strip of the bar each, a row per face.</summary>
    [UiFact]
    public void SideFaceSheets()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Evening;
            var faces = Faces(gfx);
            try
            {
                foreach (var floating in new[] { false, true })
                {
                    foreach (var dark in new[] { true, false })
                    {
                        foreach (var dpi in new uint[] { 96, 144 })
                        {
                            Sheet(gfx, faces, floating, dark, dpi, $"side-faces-{(floating ? "floating" : "islands")}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png");
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
                QuickPageTile.Sources = null;
                foreach (var (_, island) in faces)
                {
                    island.Dispose();
                }
            }
        });
    }

    private static void Sheet(Gfx gfx, List<(string Name, FolderIsland Island)> faces, bool floating, bool dark, uint dpi, string file)
    {
        var scale = dpi / 96f;
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var pad = 10f * scale;
        var label = 130f * scale;
        var styles = Thicknesses.Select(thickness => (Thickness: thickness, Style: Side(thickness, dpi, look, floating), Bar: BarPx(thickness, dpi, floating))).ToList();
        var rows = faces.Select(face => styles.Max(side =>
        {
            face.Island.Forget();
            return face.Island.CellSize(0, side.Style).Height;
        })).ToList();
        var width = label + styles.Sum(side => side.Bar + pad) + pad;
        var height = (18f * scale) + pad + rows.Sum(row => row + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var x = label;
            foreach (var (thickness, style, bar) in styles)
            {
                PopupPaint.Text(dc, gfx, $"{thickness}", 11f * scale, Weight.Regular, theme.TextSecondary, x, pad / 2, bar);
                // The bar's strip, the whole sheet down.
                PopupPaint.Fill(dc, PopupPaint.Rect(x, 18f * scale, bar, height - (18f * scale)), 0, PopupPaint.Ink(theme.CardBackground) with { a = 0.35f });
                var y = (18f * scale) + pad;
                for (var index = 0; index < faces.Count; index++)
                {
                    var island = faces[index].Island;
                    island.Forget();
                    var (w, h) = island.CellSize(0, style);
                    var left = x + ((bar - w) / 2);
                    var top = y + ((rows[index] - h) / 2);
                    if (!floating)
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(left, top, w, h), 4f * scale, PopupPaint.Ink(theme.CardBackground));
                    }
                    var copy = style;
                    Offscreen.Shifted(dc, left, top, () => island.PaintCell(dc, 0, copy, w, h));
                    y += rows[index] + pad;
                }
                x += bar + pad;
            }
            var labelY = (18f * scale) + pad;
            for (var index = 0; index < faces.Count; index++)
            {
                PopupPaint.Text(dc, gfx, faces[index].Name, 11f * scale, Weight.Regular, theme.TextSecondary, pad, labelY + (rows[index] / 2) - (7f * scale), label - pad);
                labelY += rows[index] + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
