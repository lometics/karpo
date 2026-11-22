// The readings' lines in a list folder at every height they may be given (docs\widgets.md §9, "A
// line at every height"): the five graphs, the battery, a drive and a network drive, removable
// devices, the weather now and a stock — over a day, and over five years with a holding.
//
// The user, of the list folders: "for the list view widgets they have not been optimized ad all.
// they should also be optimized to look good from 1 to 6 cell heights (maybe even higher depending
// on the type of widget). they should keep their functionality for every size if possible." So
// each of these lines is held, at every height from one row to its most, at 100, 150 and 200 %:
//
//   * nothing it draws leaves its line, and no two of its words or glyphs are drawn over each other;
//   * one row tall, it still says its reading — the figure, the temperature, the price, how much is
//     free — not only its name;
//
// and each is pictured at 150 %, every height side by side, to %TEMP%\list-<kind>.png
// (KARPO_LISTS_DIR where it is set), so the person can see every height at once. The pieces every
// such line shares (ListTiers) are held on their own: a name giving way — whole, shorter, cut,
// gone — a one-row line's reading winning its row, and the whole widget's figure fitting what its
// height leaves it.
//
// Everything they show is made up and fixed: fed graphs, a sample battery, drives with no letter
// and a stick with one whose picture is a stand-in, the saved forecast at a frozen time, and a
// stock whose price is already kept. It runs in the shared samplers' collection, which runs alone:
// the graphs are fed through the samplers every graph widget shares, and the drives are stood in
// for through the one static every drive widget reads (DriveWatch.Source).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Dock.Weather;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Perf;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

[Collection(SamplerState.Collection)]
public sealed unsafe class ListLineReadingsTests
{
    /// <summary>The scales a person's display runs at, from 100 % to 200 %.</summary>
    private static readonly float[] Scales = [1f, 1.5f, 2f];

    private const string SystemVolume = @"\\?\Volume{11111111-1111-1111-1111-111111111111}\";
    private const string ShareVolume = @"\\?\Volume{44444444-4444-4444-4444-444444444444}\";
    private const string StickVolume = @"\\?\Volume{22222222-2222-2222-2222-222222222222}\";
    private const string CardVolume = @"\\?\Volume{66666666-6666-6666-6666-666666666666}\";
    private const string PhoneVolume = @"\\?\Volume{77777777-7777-7777-7777-777777777777}\";

    private static ulong Gb(double gigabytes) => (ulong)(gigabytes * 1024 * 1024 * 1024);

    /// <summary>The last sample each graph is fed, so its reading is known: a share, or a rate down and up.</summary>
    private const float Share = 0.42f;

    private const float Down = 400_000;

    private const float Up = 100_000;

    /// <summary>
    /// Each kind of these lines: what its sheet is called, its kind, and the words its line one row
    /// tall must still say — worked out the way the line works them out, where it can be.
    /// </summary>
    private static (string Id, ListKind Kind, Func<string> Main)[] Kinds() =>
    [
        ("cpu", ListKind.Cpu, () => "42%"),
        ("gpu", ListKind.Gpu, () => "42%"),
        ("memory", ListKind.Memory, () => "42%"),
        ("disk", ListKind.Disk, () => "42%"),
        ("network", ListKind.Network, () => Units.ShortRate(Down + Up)),
        ("battery", ListKind.Battery, () => "76%"),
        ("drive", ListKind.Drive, () => Units.Size(Gb(419))),
        ("networkDrive", ListKind.NetworkDrive, () => Units.Size(Gb(1200))),
        ("removableDevices", ListKind.RemovableDevices, () => "SANDISK"),
        ("weatherNow", ListKind.WeatherNow, () => WeatherReading.From(SavedWeather.Cache(), SavedWeather.Options(), Clock.Now().ToUniversalTime(), null).Temperature),
        ("stock", ListKind.Markets, () => "412.34"),
        // Five years, the default, and an amount owned: the currency, the range and — §494 — the holding's
        // worth as the main figure, the price of one where the worth stood, on the line where they fit (§25.1).
        ("stock-holding", ListKind.Markets, () => "5,154.25"),
    ];

    /// <summary>A line of <paramref name="kind"/> with what it shows set: a place, a symbol, a drive; <paramref name="id"/> tells two stocks apart.</summary>
    private static CellItem Made(ListKind kind, string id = "") => Line(kind, island =>
    {
        if (island.Weather is not null)
        {
            island.Weather = SavedWeather.Options();
        }
        switch (kind)
        {
            case ListKind.Markets:
                island.Market = id == "stock-holding"
                    ? new MarketOptions { Symbol = "MSFT", Range = MarketRange.FiveYears, Holding = 12.5 }
                    : new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day };
                break;
            case ListKind.Drive:
                island.Drive = new DriveTargetOptions { Volume = SystemVolume, Letter = "C:", Label = "Windows" };
                break;
            case ListKind.NetworkDrive:
                island.Drive = new DriveTargetOptions { Volume = ShareVolume, Letter = "Z:", Label = "Team" };
                break;
        }
    });

    /// <summary>
    /// A list folder of one line of <paramref name="kind"/>, open, its line fed what it shows: the
    /// saved forecast before its first look, and a graph's history once it is open — opening a
    /// folder ticks its widgets, and a graph's tick reads this PC's counters, so it is forgotten
    /// and given a history of its own, long enough for the widest line drawn here.
    /// </summary>
    private static FolderIsland Folder(Gfx gfx, ListKind kind, string id = "")
    {
        var source = SavedWeather.Source();
        var folder = ListFormPictures.Folder(gfx, made =>
        {
            foreach (var tile in made.Tiles)
            {
                if (tile.Island is WeatherPartTile weather)
                {
                    weather.Source = source;
                }
            }
        }, Made(kind, id));
        if (folder.Tiles[0].Island is PerformanceIsland graph)
        {
            graph.Forgotten();
            for (var second = 0; second < 400; second++)
            {
                var wave = (MathF.Sin(second / 11f) + 1) / 2;
                if (kind == ListKind.Network)
                {
                    graph.Feed(down: 900_000 * wave, up: 120_000 * wave);
                }
                else
                {
                    graph.Feed(0.15f + (0.6f * wave));
                }
            }
            // The last sample, with what each reading says beside its figure: the memory held, the
            // disk's reads and writes.
            switch (kind)
            {
                case ListKind.Network:
                    graph.Feed(down: Down, up: Up);
                    break;
                case ListKind.Memory:
                    graph.Feed(Share, memory: new MemoryReading(42, 32UL << 30, 18UL << 30));
                    break;
                case ListKind.Disk:
                    graph.Feed(Share, read: 1_200_000, written: 310_000);
                    break;
                default:
                    graph.Feed(Share);
                    break;
            }
        }
        return folder;
    }

    /// <summary>
    /// Runs <paramref name="body"/> on a drawing thread with every stand-in these lines read set: the
    /// frozen clock, the drives, the battery, the stock's kept price.
    /// </summary>
    private static void Standing(Action<Gfx> body)
    {
        var drives = DriveWatch.Source;
        var icons = Karpo.Dock.Drives.DriveIcons.Source;
        // No letter, so no root, for the drives: a drive's picture is the shell's, and it would be
        // this PC's. The sticks keep their letters, for their eject buttons; their pictures are
        // stand-ins all the same.
        Drive[] attached =
        [
            new() { Letter = -1, Kind = DriveKind.Fixed, Volume = SystemVolume, Label = "Windows", Total = Gb(931), Free = Gb(419) },
            new() { Letter = -1, Kind = DriveKind.Remote, Volume = ShareVolume, Label = "Team", Total = Gb(4096), Free = Gb(1200) },
            new() { Letter = 4, Kind = DriveKind.Removable, Volume = StickVolume, Label = "SANDISK", Total = Gb(28.6), Free = Gb(11.9) },
            new() { Letter = 5, Kind = DriveKind.Removable, Volume = CardVolume, Label = "SD CARD", Total = Gb(59.4), Free = Gb(40.2) },
            new() { Letter = 6, Kind = DriveKind.Removable, Volume = PhoneVolume, Label = "Backup", Total = Gb(931), Free = Gb(610) },
        ];
        DriveWatch.Source = (_, remote) => [.. attached.Where(drive => remote || drive.Kind != DriveKind.Remote)];
        Karpo.Dock.Drives.DriveIcons.Source = (_, _) => null;
        try
        {
            OnStaThread(gfx =>
            {
                Clock.Now = () => SavedWeather.Fixed;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, false, false);
                BatteryIsland.Made = () => new SampleBattery();
                MarketIsland.Made = MarketOffscreenTests.Settled(new()
                {
                    ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: -1.24, previous: 417.5, nowHour: 15.9, tradedHour: 15.9),
                    ["MSFT 5y"] = MarketRangeTests.Ranged("MSFT", 261, 7 * 86400, 289, 412.34, 289.1, wander: 40),
                });
                body(gfx);
            });
        }
        finally
        {
            DriveWatch.Source = drives;
            Karpo.Dock.Drives.DriveIcons.Source = icons;
        }
    }

    /// <summary>How tall a line of <paramref name="rows"/> rows is, in px: the plan's own answer.</summary>
    private static float Tall(int rows, float scale, float textScale) => Rows(rows, scale, textScale);

    /// <summary>The most rows a line of <paramref name="kind"/> starts with (its kind's range): the heights it is shown at.</summary>
    private static int Most(ListKind kind) => ListWidgets.Find(kind)!.Range.Most;

    /// <summary>What one line drew into a box of its height: where its ink went, and its words, glyphs and pictures.</summary>
    private sealed record Drawn(
        float Width,
        float Height,
        (float Left, float Top, float Right, float Bottom) Ink,
        List<(string Text, D2D_RECT_F Box)> Said,
        List<D2D_RECT_F> Words,
        List<D2D_RECT_F> Glyphs);

    /// <summary><paramref name="line"/> drawn alone into a box <paramref name="rows"/> rows tall, watched.</summary>
    private static Drawn Draw(CellIsland line, int rows, float scale, float textScale, bool dark = true)
    {
        var look = LookFor(dark, textScale);
        var width = FolderFlyout.ListWidth(scale);
        var height = Tall(rows, scale, textScale);
        var style = FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
        var margin = PopupPaint.Round(24 * scale);
        var said = new List<(string, D2D_RECT_F)>();
        var words = new List<D2D_RECT_F>();
        var glyphs = new List<D2D_RECT_F>();
        (float, float, float, float) ink = default;
        try
        {
            Bitmap(
                width + (2 * margin),
                height + (2 * margin),
                dc =>
                {
                    // A piece's box is in its painter's own space — a panel's control is placed with a
                    // transform, and draws from its own corner — so each is brought into the line's by
                    // the transform in force when it is drawn.
                    var device = (nint)dc;
                    D2D_RECT_F Line(D2D_RECT_F box)
                    {
                        var at = Transform(device);
                        return new D2D_RECT_F(
                            (box.left * at._11) + (box.top * at._21) + at._31 - margin,
                            (box.left * at._12) + (box.top * at._22) + at._32 - margin,
                            (box.right * at._11) + (box.bottom * at._21) + at._31 - margin,
                            (box.right * at._12) + (box.bottom * at._22) + at._32 - margin);
                    }
                    PaintWatch.Said = (text, box) => said.Add((text, Line(box)));
                    PaintWatch.Watching = (box, what, color) =>
                    {
                        (what == Painted.Glyph ? glyphs : what == Painted.Text ? words : null)?.Add(Line(box));
                        return color;
                    };
                    Shifted(dc, margin, margin, () => line.PaintOn(dc, PopupPaint.Rect(0, 0, width, height), style, WidgetSurface.List));
                },
                bitmap => ink = InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
        }
        finally
        {
            PaintWatch.Watching = null;
            PaintWatch.Said = null;
        }
        var (left, top, right, bottom) = ink;
        return new Drawn(width, height, (left - margin, top - margin, right - margin, bottom - margin), said, words, glyphs);
    }

    /// <summary>The transform in force on the device context at <paramref name="device"/>.</summary>
    private static D2D_MATRIX_3X2_F Transform(nint device)
    {
        D2D_MATRIX_3X2_F at;
        ((ID2D1DeviceContext*)device)->GetTransform(&at);
        return at;
    }

    /// <summary>Whether two boxes share more than a sliver: a pixel each way is the anti-aliasing's.</summary>
    private static bool Over(D2D_RECT_F one, D2D_RECT_F two) =>
        MathF.Min(one.right, two.right) - MathF.Max(one.left, two.left) > 1
        && MathF.Min(one.bottom, two.bottom) - MathF.Max(one.top, two.top) > 1;

    // --- The rules ------------------------------------------------------------------------------

    /// <summary>
    /// At every height from one row to its most, at 100, 150 and 200 % and at the largest text,
    /// every line draws something, all of it inside its line, and none of its words or glyphs over
    /// another.
    /// </summary>
    [UiFact]
    public void EveryHeightStaysInItsLineAndNothingOverlaps()
    {
        var wrong = new List<string>();
        Standing(gfx =>
        {
            foreach (var (id, kind, _) in Kinds())
            {
                var folder = Folder(gfx, kind, id);
                try
                {
                    var line = (CellIsland)folder.Tiles[0].Island;
                    foreach (var scale in Scales)
                    {
                        foreach (var textScale in TextScales)
                        {
                            for (var rows = 1; rows <= Most(kind); rows++)
                            {
                                var drawn = Draw(line, rows, scale, textScale);
                                var at = $"{id} at {rows} row{(rows == 1 ? "" : "s")}, {scale * 100:0} %, text {textScale * 100:0} %";
                                if (drawn.Ink.Right <= drawn.Ink.Left)
                                {
                                    wrong.Add($"{at}: drew nothing");
                                    continue;
                                }
                                if (drawn.Ink.Left < -0.5f || drawn.Ink.Top < -0.5f || drawn.Ink.Right > drawn.Width + 0.5f || drawn.Ink.Bottom > drawn.Height + 0.5f)
                                {
                                    wrong.Add($"{at}: ink at {drawn.Ink.Left:0},{drawn.Ink.Top:0}..{drawn.Ink.Right:0},{drawn.Ink.Bottom:0} outside its {drawn.Width:0}x{drawn.Height:0} line");
                                }
                                var pieces = drawn.Words.Concat(drawn.Glyphs).ToList();
                                for (var one = 0; one < pieces.Count; one++)
                                {
                                    for (var two = one + 1; two < pieces.Count; two++)
                                    {
                                        if (Over(pieces[one], pieces[two]))
                                        {
                                            wrong.Add($"{at}: pieces at {pieces[one].left:0},{pieces[one].top:0} and {pieces[two].left:0},{pieces[two].top:0} overlap");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        Assert.True(wrong.Count == 0, $"{wrong.Count} lines are wrong:\n{string.Join("\n", wrong.Take(60))}");
    }

    /// <summary>
    /// A line one row tall still says its reading — the figure, how much is free, the temperature,
    /// the price, the device — at 100, 150 and 200 %: not only its glyph and its name.
    /// </summary>
    [UiFact]
    public void OneRowSaysTheReading()
    {
        var wrong = new List<string>();
        Standing(gfx =>
        {
            foreach (var (id, kind, main) in Kinds())
            {
                var folder = Folder(gfx, kind, id);
                try
                {
                    var line = (CellIsland)folder.Tiles[0].Island;
                    var reading = main();
                    foreach (var scale in Scales)
                    {
                        var drawn = Draw(line, 1, scale, 1f);
                        if (!drawn.Said.Any(said => said.Text.Contains(reading, StringComparison.Ordinal) && said.Box.bottom <= drawn.Height + 0.5f && said.Box.top >= -0.5f))
                        {
                            wrong.Add($"{id} at {scale * 100:0} %: one row does not say \"{reading}\" (it says {string.Join(", ", drawn.Said.Select(said => $"\"{said.Text}\""))})");
                        }
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    // --- The pieces every line shares ------------------------------------------------------------

    /// <summary>
    /// A name gives way as its room shrinks: whole while it fits, then its shorter form, then cut
    /// while its first four letters still show, then not at all — never cut to a letter or two, and a
    /// name of three letters whole or not at all.
    /// </summary>
    [UiFact]
    public void ANameIsWholeThenShorterThenCutThenGone()
    {
        OnStaThread(gfx =>
        {
            const float px = 14;
            float Width(string text) => PopupPaint.Measure(gfx, text, px, Weight.Regular).X;
            string[] place = ["Zürich, Switzerland", "Zürich"];
            Assert.Equal(place[0], ListTiers.Name(gfx, place, px, Weight.Regular, Width(place[0])));
            Assert.Equal(place[1], ListTiers.Name(gfx, place, px, Weight.Regular, Width(place[0]) - 1));
            Assert.Equal(place[1], ListTiers.Whole(gfx, place, px, Weight.Regular, Width(place[1])));
            Assert.Null(ListTiers.Whole(gfx, place, px, Weight.Regular, Width(place[1]) - 1));

            string[] word = ["Performance"];
            var cut = Width("Perf…");
            Assert.True(cut < Width(word[0]));
            Assert.Equal(word[0], ListTiers.Name(gfx, word, px, Weight.Regular, cut));
            Assert.Null(ListTiers.Name(gfx, word, px, Weight.Regular, cut - 1));

            Assert.Equal("CPU", ListTiers.Name(gfx, ["CPU"], px, Weight.Regular, Width("CPU")));
            Assert.Null(ListTiers.Name(gfx, ["CPU"], px, Weight.Regular, Width("CPU") - 1));
            Assert.Null(ListTiers.Name(gfx, [""], px, Weight.Regular, 500));
        });
    }

    /// <summary>
    /// A one-row line's reading wins its row: its field stands against the trailing gutter, and
    /// where the row is short of it, stops at the leading column rather than crossing it; the name
    /// takes what is left — its full form, its short one, or none, the room between then starting
    /// where the name would have.
    /// </summary>
    [UiFact]
    public void AOneRowLinesReadingWinsItsRow()
    {
        OnStaThread(gfx =>
        {
            var look = LookFor(true, 1f);
            var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
            var form = ListForm.Of(gfx, style);
            var box = PopupPaint.Rect(0, 0, 300, 40);
            string[] names = ["Processor", "CPU"];
            var end = box.right - form.Inset;

            var roomy = ListTiers.Row(gfx, box, style, names, Weight.Regular, 40);
            Assert.Equal("Processor", roomy.Name);
            Assert.Equal(end, roomy.FieldRight);
            Assert.Equal(40, roomy.Field, 3);
            Assert.True(roomy.Between.left > roomy.NameRight && roomy.Between.right < roomy.FieldLeft);

            var cpuWidth = PopupPaint.Measure(gfx, "CPU", form.TextPx, Weight.Regular).X;
            var tight = ListTiers.Row(gfx, box, style, names, Weight.Regular, end - form.TextLeft - cpuWidth - style.Gap - 0.5f);
            Assert.Equal("CPU", tight.Name);

            var none = ListTiers.Row(gfx, box, style, names, Weight.Regular, 1000);
            Assert.Null(none.Name);
            Assert.Equal(box.left + form.TextLeft, none.FieldLeft);
            Assert.Equal(end - form.TextLeft, none.Field, 3);

            var trailing = ListTiers.Row(gfx, box, style, names, Weight.Regular, 40, trailing: 32);
            Assert.Equal(end - 32, trailing.FieldRight);
        });
    }

    /// <summary>
    /// The whole widget's figure is the kit's Hero, 28 DIP, at every height (§388: a taller line shows
    /// more, never bigger type) where the rest leaves its line the room, and the compact line's Value
    /// where it does not — never a size between the two, at the list's text and at the largest.
    /// </summary>
    [UiFact]
    public void TheWholeWidgetsFigureIsTheHeroOrTheValue()
    {
        OnStaThread(gfx =>
        {
            foreach (var textScale in TextScales)
            {
                var look = LookFor(true, textScale);
                var style = FolderFlyout.StyleFor(1.5f, look, FlyoutTheme.For(look));
                var hero = ListTiers.HeroPx(style);
                Assert.Equal(style.Text(28), hero);
                foreach (var rows in new[] { 3, 6, 12 })
                {
                    Assert.Equal(hero, ListTiers.HeroPx(gfx, style, Tall(rows, 1.5f, textScale), 0));
                }

                var height = Tall(6, 1.5f, textScale);
                var line = TilePaint.LineHeight(gfx, hero, Weight.Semibold);
                Assert.Equal(hero, ListTiers.HeroPx(gfx, style, height, height - line));
                Assert.Equal(ListTiers.ValuePx(style), ListTiers.HeroPx(gfx, style, height, height - (line * 0.8f)));
                Assert.Equal(ListTiers.ValuePx(style), ListTiers.HeroPx(gfx, style, height, height));
            }
        });
    }
    // --- The pictures ---------------------------------------------------------------------------

    /// <summary>Where the sheets go: KARPO_LISTS_DIR, else %TEMP%.</summary>
    private static string Sheets
    {
        get
        {
            var folder = Environment.GetEnvironmentVariable("KARPO_LISTS_DIR") is { Length: > 0 } named ? named : Path.GetTempPath();
            Directory.CreateDirectory(folder);
            return folder;
        }
    }

    /// <summary>
    /// Each kind at 150 %, every height from one row to its most side by side, six to a row, each on
    /// the panel's own ground under its height: list-&lt;kind&gt;.png (dark) and list-&lt;kind&gt;-light.png.
    /// </summary>
    [UiFact]
    public void EveryHeightOfEachLine()
    {
        const float scale = 1.5f;
        Standing(gfx =>
        {
            foreach (var (id, kind, _) in Kinds())
            {
                var folder = Folder(gfx, kind, id);
                try
                {
                    var line = (CellIsland)folder.Tiles[0].Island;
                    foreach (var dark in new[] { true, false })
                    {
                        Sheet(gfx, line, kind, scale, dark, Path.Combine(Sheets, $"list-{id}{(dark ? "" : "-light")}.png"));
                    }
                }
                finally
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }

    /// <summary>One kind's sheet: its line at every height, six to a row, each under its height's name.</summary>
    private static void Sheet(Gfx gfx, CellIsland line, ListKind kind, float scale, bool dark, string path)
    {
        var look = LookFor(dark);
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        var most = Most(kind);
        var across = Math.Min(6, most);
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var rowsOfSheet = (most + across - 1) / across;
        var bands = Enumerable.Range(0, rowsOfSheet)
            .Select(band => Enumerable.Range((band * across) + 1, Math.Min(across, most - (band * across))).Max(rows => Tall(rows, scale, 1f)))
            .ToList();
        var sheetWidth = pad + (across * (width + pad));
        var sheetHeight = pad + bands.Sum(band => labelLine + band + pad);
        Bitmap(sheetWidth, sheetHeight, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0, PopupPaint.Ink(dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
            var y = pad;
            for (var band = 0; band < bands.Count; band++)
            {
                for (var slot = 0; slot < across; slot++)
                {
                    var rows = (band * across) + slot + 1;
                    if (rows > most)
                    {
                        break;
                    }
                    var x = pad + (slot * (width + pad));
                    var height = Tall(rows, scale, 1f);
                    PopupPaint.Text(dc, gfx, $"{rows} row{(rows == 1 ? "" : "s")}", label, Weight.Regular, theme.TextSecondary, x, y);
                    var box = PopupPaint.Rect(x, y + labelLine, width, height);
                    PopupPaint.Fill(dc, box, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                    PopupPaint.Stroke(dc, box, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                    var context = dc;
                    PopupPaint.Clipped(context, box, PopupPaint.Round(8 * scale), () => Shifted(context, box.left, box.top, () =>
                        FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style)));
                }
                y += labelLine + bands[band] + pad;
            }
        }, bitmap => Save(bitmap, path));
    }
}
