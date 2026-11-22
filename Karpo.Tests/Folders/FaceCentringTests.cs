// folders: a face of text stands in the middle of its button on the bar (2026-10-11: "the taskbar
// weather face should be centerd, right now it does not appear centered but left aligned"). The
// button's length is the face's settings', never what it shows (FolderFaceTests), so a reading
// shorter than the widest it could be leaves room; that room is shared at both ends, not left after
// the reading. Measured in the pixels: the ink's middle is the cell's, across the bar and along it,
// within a pixel, at every bar size, at 100 and 150 %.
//
// The sheet (face-centring.png) goes to KARPO_PNG_DIR: each face on its cell with the cell's middle
// marked, so a face off it shows at a glance.

using Karpo.Dock;
using Karpo.Dock.Native;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

public sealed unsafe class FaceCentringTests
{
    private static readonly int[] Heights = [32, 40, 48, 56, 64];

    /// <summary>A folder wearing <paramref name="face"/> alone, its weather the saved forecast's place.</summary>
    internal static BarItem Wearing(FolderFace face)
    {
        var island = BarKind.FolderList.New();
        island.Id = "face";
        var folder = island.Folder!;
        folder.Face = face;
        if (face.Kind == FolderFaces.WeatherId)
        {
            face.Weather = SavedWeather.Options();
        }
        var line = ListKind.Clock.New();
        line.Id = "clock";
        folder.Put(line);
        island.Normalize();
        return island;
    }

    /// <summary>The faces of text this holds to the middle: each weather look, read and before a place is set.</summary>
    private static List<(string Name, BarItem Folder)> Samples()
    {
        var unset = WeatherFace.New();
        var noPlace = Wearing(unset);
        noPlace.Folder!.Face.Weather = new WeatherOptions();
        return
        [
            ("weather", Wearing(WeatherFace.New())),
            ("weather detailed", Wearing(WeatherFace.New(WeatherFace.Detailed))),
            ("weather forecast", Wearing(WeatherFace.New(WeatherFace.Forecast))),
            ("weather, no place", noPlace),
        ];
    }

    internal static FolderIsland Make(Gfx gfx, BarItem folder)
    {
        FolderIsland.WeatherStand = SavedWeather.Cache();
        try
        {
            return new(Config.IslandKey(null, folder.Id), folder, gfx, new Config());
        }
        finally
        {
            FolderIsland.WeatherStand = null;
        }
    }

    internal static IslandStyle Style(int height, uint dpi, bool vertical = false)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), LookFor(dark: true));
    }

    /// <summary>The ink of <paramref name="island"/>'s cell, in the cell's px, and the cell's size.</summary>
    internal static ((float Left, float Top, float Right, float Bottom) Ink, float Width, float Height) InkOf(FolderIsland island, in IslandStyle style, byte least = 24)
    {
        island.Forget();
        var (width, height) = island.CellSize(0, style);
        const float margin = 12f;
        (float Left, float Top, float Right, float Bottom) ink = default;
        var drawn = style;
        Offscreen.Bitmap(
            width + (2 * margin),
            height + (2 * margin),
            dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintCell(dc, 0, drawn, width, height)),
            bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin)), least));
        return ((ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin), width, height);
    }

    /// <summary>
    /// The weather face's ink stands in the middle of its cell, along the bar and across it, within a
    /// pixel: every look, read and before a place is set, on bars of 32 to 64 DIP, at 100 and 150 %,
    /// across a bar and down one.
    /// </summary>
    [UiFact]
    public void TheWeatherFaceIsInTheMiddleOfItsCell()
    {
        var failures = new List<string>();
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx =>
        {
            foreach (var (name, folder) in Samples())
            {
                using var island = Make(gfx, folder);
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var vertical in new[] { false, true })
                    {
                        foreach (var height in Heights)
                        {
                            var ((left, top, right, bottom), width, tall) = InkOf(island, Style(height, dpi, vertical));
                            var along = ((left + right) / 2) - (width / 2);
                            var across = ((top + bottom) / 2) - (tall / 2);
                            if (MathF.Abs(along) > 1f || MathF.Abs(across) > 1f)
                            {
                                failures.Add($"{name} at {height} DIP, {dpi} DPI{(vertical ? ", vertical" : "")}: its ink's middle is {along:+0.0;-0.0} px across and {across:+0.0;-0.0} px down from the cell's ({width:0.#} by {tall:0.#})");
                            }
                        }
                    }
                }
            }
        }));
        Assert.True(failures.Count == 0, $"{failures.Count} faces off the middle:\n{string.Join("\n", failures.Take(40))}");
    }

    /// <summary>A share's ticker on its own face, its price come: the store the test's, never the network.</summary>
    internal static FolderIsland Ticker(Gfx gfx)
    {
        var holder = BarKind.FolderList.New();
        holder.Id = "ticker";
        holder.Folder!.Face = FolderFace.Of(FolderFaces.MarketsId);
        holder.Folder.Face.Market = (MarketOffscreenTests.Held("MSFT"));
        var line = ListKind.Clock.New();
        line.Id = "clock";
        holder.Folder.Put(line);
        holder.Normalize();
        MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corp.", price: 512.4, percent: 0.86, previous: 508.03) });
        try
        {
            var folder = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
            Assert.True(Patience.Until(() => !folder.MarketQuote.Fetching), "the face's fetch never came back");
            _ = ((IIsland)folder).Refresh(gfx);
            return folder;
        }
        finally
        {
            MarketIsland.Made = null;
        }
    }

    /// <summary>
    /// The other faces of text whose reading is shorter than the room they keep stand in the middle
    /// too, across a bar: the timer and the focus session at rest ("5m" in the room of "04:59"), the
    /// stopwatch, and a share's ticker whose price is shorter than the widest it keeps room for.
    /// </summary>
    [UiFact]
    public void TheOtherFacesOfTextAreInTheMiddleToo()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var faces = FolderFaceTests.Samples()
                .Where(sample => sample.Name is "timer" or "focus" or "stopwatch")
                .Select(sample => (sample.Name, Island: FolderFaceTests.Make(gfx, sample.Folder)))
                .Append(("stock", Ticker(gfx)))
                .ToList();
            try
            {
                foreach (var (name, island) in faces)
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        foreach (var height in Heights)
                        {
                            // The ticker's day is faded behind its words across the whole button: its words alone are measured.
                            var ((left, _, right, _), width, tall) = InkOf(island, Style(height, dpi), least: name == "stock" ? (byte)160 : (byte)24);
                            var along = ((left + right) / 2) - (width / 2);
                            // Half a pixel more than the weather's: the countdowns' glyphs (the timer's
                            // dot, the focus target) carry their own ink off their box's middle.
                            if (MathF.Abs(along) > 1.5f)
                            {
                                failures.Add($"{name} at {height} DIP, {dpi} DPI: its ink's middle is {along:+0.0;-0.0} px across from the cell's ({width:0.#} by {tall:0.#})");
                            }
                        }
                    }
                }
            }
            finally
            {
                foreach (var (_, island) in faces)
                {
                    island.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, $"{failures.Count} faces off the middle:\n{string.Join("\n", failures.Take(40))}");
    }

    /// <summary>
    /// The mail's envelope stands in the middle of its button, its count out over the button's end as
    /// an app's badge stands on its icon (2026-11-07: "the email face looks like it is off center
    /// (with the count)"): the room the widest count keeps after it is no part of the centring.
    /// </summary>
    [UiFact]
    public void TheEnvelopeIsInTheMiddleOfItsCellBesideItsCount()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var sample = FolderFaceTests.Samples().Single(sample => sample.Name == "mail");
            using var island = FolderFaceTests.Make(gfx, sample.Folder);
            foreach (var dpi in new uint[] { 96, 144 })
            {
                foreach (var height in Heights.Append(72))
                {
                    var style = Style(height, dpi);
                    island.Forget();
                    var (width, tall) = island.CellSize(0, style);
                    var box = FolderIsland.FaceBoxes(gfx, island.Options, style, width, tall, folder: island)[0];
                    var along = ((box.left + box.right) / 2) - (width / 2);
                    if (MathF.Abs(along) > 0.5f)
                    {
                        failures.Add($"{height} DIP, {dpi} DPI: the envelope's middle is {along:+0.0;-0.0} px across from the cell's ({width:0.#} px)");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Each face on its cell, the cell's middle marked across and down it: face-centring.png.</summary>
    [UiFact]
    public void CentringSheet()
    {
        SavedWeather.AtAFixedTime(() => Offscreen.OnStaThread(gfx =>
        {
            var made = Samples().Select(sample => (sample.Name, Island: Make(gfx, sample.Folder)))
                .Concat(FolderFaceTests.Samples()
                    .Where(sample => sample.Name is "timer" or "focus" or "stopwatch")
                    .Select(sample => (sample.Name, Island: FolderFaceTests.Make(gfx, sample.Folder))))
                .Append(("stock", Ticker(gfx)))
                .ToList();
            try
            {
                var items = new List<(string Name, FolderIsland Island, IslandStyle Style)>();
                foreach (var (name, island) in made)
                {
                    foreach (var height in new[] { 32, 48, 64 })
                    {
                        items.Add(($"{name} {height}", island, Style(height, 96)));
                    }
                    items.Add(($"{name} 48 @150%", island, Style(48, 144)));
                    items.Add(($"{name} vertical", island, Style(48, 96, vertical: true)));
                }
                Sheet(gfx, items, "face-centring.png");
            }
            finally
            {
                foreach (var (_, island) in made)
                {
                    island.Dispose();
                }
            }
        }));
    }

    internal static void Sheet(Gfx gfx, List<(string Name, FolderIsland Island, IslandStyle Style)> items, string file)
    {
        const float pad = 14f;
        const float label = 16f;
        const float most = 1200f;
        var places = new List<(float X, float Y, float W, float H)>();
        var x = pad;
        var y = pad;
        var rowHeight = 0f;
        var widest = 0f;
        foreach (var (_, island, style) in items)
        {
            island.Forget();
            var (w, h) = island.CellSize(0, style);
            var slot = MathF.Max(w, 150f);
            if (x > pad && x + slot > most)
            {
                x = pad;
                y += label + rowHeight + pad;
                rowHeight = 0;
            }
            places.Add((x, y, w, h));
            rowHeight = MathF.Max(rowHeight, h);
            x += slot + pad;
            widest = MathF.Max(widest, x);
        }
        var total = y + label + rowHeight + pad;
        var theme = FlyoutTheme.For(LookFor(dark: true));
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(theme.Base));
            var mark = new DXGI_RGBA(1, 0.2f, 0.2f, 0.7f);
            for (var index = 0; index < items.Count; index++)
            {
                var (name, island, style) = items[index];
                var (left, top, w, h) = places[index];
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, theme.TextSecondary, left, top, 220f);
                var cardTop = top + label;
                PopupPaint.Fill(dc, PopupPaint.Rect(left, cardTop, w, h), 4f * style.Scale, PopupPaint.Ink(theme.CardBackground));
                var drawn = style;
                Offscreen.Shifted(dc, left, cardTop, () => island.PaintCell(dc, 0, drawn, w, h));
                // The cell's middle, along and across: a tick at each edge, clear of the ink.
                PopupPaint.Fill(dc, PopupPaint.Rect(left + (w / 2) - 0.5f, cardTop - 4, 1, 3), 0, mark);
                PopupPaint.Fill(dc, PopupPaint.Rect(left + (w / 2) - 0.5f, cardTop + h + 1, 1, 3), 0, mark);
                PopupPaint.Fill(dc, PopupPaint.Rect(left - 4, cardTop + (h / 2) - 0.5f, 3, 1), 0, mark);
                PopupPaint.Fill(dc, PopupPaint.Rect(left + w + 1, cardTop + (h / 2) - 0.5f, 3, 1), 0, mark);
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
