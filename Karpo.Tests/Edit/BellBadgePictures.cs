// The bell's count on the bar (2026-10-14: "for the notifications widget in the taskbar the number of
// notifications badge gets clipped off"): the count in its accent pill stands whole inside the
// button, on the default clock button (the time over the date, the bell to their right), the bell
// alone and the bell before another face, at 1, 9, 10, 99 and "99+", at 100 to 175 %, on bars from
// the thinnest to the thickest, across a bar and down one. The cells are painted with and without
// the count; what differs is the count's ink, which must be its whole pill, inside the cell and
// inside the room the face keeps (FaceSize.Trail). The sheet bell-badge-cells.png and the bar's
// corner as the scene draws it, islands and connected, at the bottom and down the left, with the
// hover pill on the button (bell-badge-bar-*.png), go to KARPO_PNG_DIR (docs\widgets.md §169).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The counts the bell is shown with: one digit, two, and past 99 ("99+").</summary>
    private static readonly int[] BellCounts = [1, 9, 10, 99, 100];

    /// <summary>A store of <paramref name="count"/> notifications, every one new.</summary>
    private static NotificationStore BellStore(string folder, int count) =>
        NotificationsPresetTests.Store(folder,
            Enumerable.Range(0, count).Select(at => (1000L + at, 2, at + 1,
                $"<toast><visual><binding template=\"ToastGeneric\"><text>Note {at}</text><text>Body.</text></binding></visual></toast>")),
            $"bell-{count}");

    /// <summary>A folder of <paramref name="faces"/> over <paramref name="store"/>.</summary>
    private static FolderIsland BellFolder(Gfx gfx, BarItem holder, NotificationStore store)
    {
        NotificationPartTile.Stand = store;
        try
        {
            return FolderFaceTests.Make(gfx, holder);
        }
        finally
        {
            NotificationPartTile.Stand = null;
        }
    }

    private static BarItem Wearing(string id, params string[] faces)
    {
        var island = BarKind.FolderList.New();
        island.Id = id;
        island.Folder.Faces = [.. faces.Select(FolderFace.Of)];
        island.Normalize();
        return island;
    }

    private static IslandStyle BellStyle(int height, uint dpi, Look look, bool vertical)
    {
        var config = new Config { Height = height };
        return new IslandStyle(dpi / 96f, vertical, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
    }

    /// <summary>The cell painted alone into a picture its size, read back as pixels.</summary>
    private static uint[] BellCell(FolderIsland island, in IslandStyle style, int width, int height)
    {
        uint[] pixels = [];
        var shown = style;
        island.Forget();
        Offscreen.Bitmap(width, height, dc => island.PaintCell(dc, 0, shown, width, height), bitmap => pixels = Read(bitmap, width, height));
        return pixels;
    }

    /// <summary>
    /// The count's ink — the pixels that differ from the bell with none — is its whole pill, a pixel
    /// or more inside the cell, inside the room its face keeps along the bar; on every count, scale,
    /// height and side, the default clock button's, the bell alone's and the bell before another face's.
    /// </summary>
    [UiFact]
    public void TheBellsCountStaysWholeInsideItsButton()
    {
        var folder = NotificationsPresetTests.Temp();
        try
        {
            Offscreen.OnStaThread(gfx =>
            {
                Clock.Now = () => new DateTime(2026, 10, 13, 22, 19, 0);
                WindowsClock.SecondsStand = false;
                var holders = new (string Name, BarItem Holder)[]
                {
                    ("clock button", FreshBar().Single(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId)),
                    ("bell", Wearing("bell", FolderFaces.NotificationsId)),
                    ("bell + time", Wearing("bell-time", FolderFaces.NotificationsId, FolderFaces.DateTimeId)),
                };
                var stores = new[] { 0 }.Concat(BellCounts).ToDictionary(count => count, count => BellStore(folder, count));
                var made = new List<FolderIsland>();
                var failures = new List<string>();
                var sheet = new List<(string Name, FolderIsland Island, IslandStyle Style)>();
                try
                {
                    foreach (var (name, holder) in holders)
                    {
                        var none = BellFolder(gfx, holder, stores[0]);
                        made.Add(none);
                        var bell = holder.Folder!.Faces.FindIndex(face => face.Kind == FolderFaces.NotificationsId);
                        foreach (var count in BellCounts)
                        {
                            var island = BellFolder(gfx, holder, stores[count]);
                            made.Add(island);
                            _ = ((IIsland)island).Refresh(gfx);
                            var badge = GlyphBadge.Text(count)!;
                            foreach (var vertical in new[] { false, true })
                            {
                                foreach (var height in new[] { Config.MinHeight, 32, 40, 48, 64, 96, Config.MaxHeight })
                                {
                                    foreach (var dpi in new uint[] { 96, 120, 144, 168 })
                                    {
                                        var style = BellStyle(height, dpi, AddListPictures.LookFor(dark: true), vertical);
                                        var (w, h) = island.CellSize(0, style);
                                        var (width, tall) = ((int)MathF.Round(w), (int)MathF.Round(h));
                                        var where = $"{name}, {badge}, {height} DIP at {dpi * 100 / 96} %{(vertical ? ", vertical" : "")}";
                                        var box = FolderIsland.FaceBoxes(gfx, holder.Folder!, style, width, tall, folder: island)[bell];
                                        var size = FolderIsland.FaceSizes(gfx, holder.Folder!, style, folder: island)[bell];
                                        var pill = GlyphBadge.Pill(gfx, style, badge, box, rise: 0.35f, room: new D2D_RECT_F(0, 0, width, tall))!.Value;
                                        // The count's room is kept only while it shows (docs\widgets.md §244): beside
                                        // another face the bell with none is a narrower button, its faces elsewhere, so
                                        // the pixels are read against it only for the bell alone; beside another face
                                        // the pill is held to the room as it is placed.
                                        var (left, top, right, bottom) = ((int)MathF.Floor(pill.left), (int)MathF.Floor(pill.top), (int)MathF.Ceiling(pill.right), (int)MathF.Ceiling(pill.bottom));
                                        if (holder.Folder!.Faces.Count == 1)
                                        {
                                            var with = BellCell(island, style, width, tall);
                                            var without = BellCell(none, style, width, tall);
                                            (left, top, right, bottom) = (width, tall, 0, 0);
                                            for (var y = 0; y < tall; y++)
                                            {
                                                for (var x = 0; x < width; x++)
                                                {
                                                    if (with[(y * width) + x] != without[(y * width) + x])
                                                    {
                                                        (left, top) = (Math.Min(left, x), Math.Min(top, y));
                                                        (right, bottom) = (Math.Max(right, x + 1), Math.Max(bottom, y + 1));
                                                    }
                                                }
                                            }
                                        }
                                        if (right <= left)
                                        {
                                            failures.Add($"{where}: no count drawn");
                                            continue;
                                        }
                                        // Its whole pill: not cut by the cell on any side.
                                        if (right - left < pill.right - pill.left - 1.5f || bottom - top < pill.bottom - pill.top - 1.5f)
                                        {
                                            failures.Add($"{where}: the count's ink is {right - left} × {bottom - top} px of a {pill.right - pill.left} × {pill.bottom - pill.top} pill");
                                        }
                                        // A pixel inside the cell, so no card's edge nor the cell's clip meets it.
                                        if (pill.left < 0.5f || pill.top < 0.5f || pill.right > width - 0.5f || pill.bottom > tall - 0.5f)
                                        {
                                            failures.Add($"{where}: the pill {pill.left:0.#},{pill.top:0.#} to {pill.right:0.#},{pill.bottom:0.#} leaves the {width} × {tall} cell");
                                        }
                                        // Along the bar, in the room the face keeps: never on the next face or past the button's end.
                                        if (!vertical && holder.Folder!.Faces.Count > 1 && right > box.right + size.Trail + 1)
                                        {
                                            failures.Add($"{where}: the count's ink ends at {right}, past the bell's room at {box.right + size.Trail}");
                                        }
                                        if (dpi is 96 or 144 && height is Config.MinHeight or 48 or 96 && (!vertical || height == 48))
                                        {
                                            sheet.Add(($"{name} {badge} {height}{(vertical ? " v" : "")} @{dpi * 100 / 96}%", island, style));
                                        }
                                    }
                                }
                            }
                        }
                        sheet.Add(($"{name} 99+ light 48", made[^1], BellStyle(48, 96, AddListPictures.LookFor(dark: false), false)));
                    }
                    BellSheet(gfx, sheet, "bell-badge-cells.png");
                }
                finally
                {
                    foreach (var island in made)
                    {
                        island.Dispose();
                    }
                    Clock.Now = Clock.Real;
                    WindowsClock.SecondsStand = null;
                }
                Assert.True(failures.Count == 0, string.Join('\n', failures.Take(40)) + $"\n({failures.Count} in all)");
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }

    /// <summary>
    /// A first start's bar with the bell at 9 and at "99+", as the scene draws it: islands and
    /// connected, at the bottom and down the left, on the thinnest, the taskbar's and a thick bar, at
    /// 100 % and 150 %; light, and with the hover on the clock button, at the taskbar's. The button's
    /// block stands inside the bar's panel. bell-badge-bar-&lt;count&gt;-&lt;style&gt;-&lt;edge&gt;-&lt;height&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void BellBadgeBarSheets()
    {
        var folder = NotificationsPresetTests.Temp();
        try
        {
            WindowShots.OnHiddenDesktop("bell-badge", (gfx, _) =>
            {
                Clock.Now = () => new DateTime(2026, 10, 13, 22, 19, 0);
                FolderIsland.WeatherStand = SavedWeather.Cache();
                var probe = BatteryIsland.Probe;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
                IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
                try
                {
                    foreach (var count in new[] { 9, 100 })
                    {
                        var store = BellStore(folder, count);
                        foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                        {
                            foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                            {
                                foreach (var height in new[] { Config.MinHeight, 48, 72 })
                                {
                                    foreach (var dpi in new uint[] { 96, 144 })
                                    {
                                        var shots = new List<(bool Dark, bool Hover)> { (true, false) };
                                        if (height == 48 && dpi == 96)
                                        {
                                            shots.Add((false, false));
                                            shots.Add((true, true));
                                            shots.Add((false, true));
                                        }
                                        foreach (var (dark, hover) in shots)
                                        {
                                            var name = $"bell-badge-bar-{GlyphBadge.Text(count)!.Replace("+", "plus")}-{(style == BarStyle.Islands ? "islands" : "connected")}-"
                                                + $"{edge.ToString().ToLowerInvariant()}-{height}-{dpi * 100 / 96}{(dark ? "" : "-light")}{(hover ? "-hover" : "")}.png";
                                            BellBar(gfx, store, dpi, edge, new Config { Height = height, Style = style }, AddListPictures.LookFor(dark) with { Animations = false }, hover, name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                finally
                {
                    FolderIsland.WeatherStand = null;
                    BatteryIsland.Probe = probe;
                    IndicatorSources.Made = null;
                    Clock.Now = Clock.Real;
                }
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }

    private static void BellBar(Gfx gfx, NotificationStore store, uint dpi, Edge edge, Config config, Look look, bool hover, string file)
    {
        var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
        var area = edge == Edge.Bottom
            ? new RECT { left = 0, top = 0, right = 2560, bottom = (int)(dpi * 10) }
            : new RECT { left = 0, top = 0, right = (int)(dpi * 10), bottom = 2560 };
        var bar = FreshBar();
        NotificationPartTile.Stand = store;
        List<PartSpec> specs;
        IslandCollection islands;
        try
        {
            (specs, islands) = Made(gfx, config, bar);
        }
        finally
        {
            NotificationPartTile.Stand = null;
        }
        try
        {
            var content = new Content
            {
                Islands = islands,
                Tiles = [.. Enumerable.Range(0, 3).Select(BusyApp)],
                Tray = DraggedTrayIcons(4, px),
                TrayOverflow = true,
            };
            var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
            var at = bar.FindIndex(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
            var block = layout.BlockRect(at);
            var panel = layout.PanelRect;
            Assert.True(block.X >= panel.X - 0.5f && block.Y >= panel.Y - 0.5f
                && block.X + block.Width <= panel.X + panel.Width + 0.5f && block.Y + block.Height <= panel.Y + panel.Height + 0.5f,
                $"{file}: the clock button's block {block} leaves the bar's panel {panel}");
            var tray = layout.Parts.Single(part => part.Kind == BarKind.Tray);
            OverADesktop(gfx, layout, look, content, null, file, edge == Edge.Bottom ? (int)tray.Left - (int)(24 * dpi / 96f) : 0,
                hover ? scene => scene.Highlight(Hit.Cell(at, 0), false) : null);
        }
        finally
        {
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }

    private static void BellSheet(Gfx gfx, List<(string Name, FolderIsland Island, IslandStyle Style)> items, string file)
    {
        const float pad = 14f;
        const float label = 16f;
        const float most = 1500f;
        var places = new List<(float X, float Y, float W, float H)>();
        var (x, y, rowHeight, widest) = (pad, pad, 0f, 0f);
        foreach (var (_, island, style) in items)
        {
            var (w, h) = island.CellSize(0, style);
            var slot = MathF.Max(w, 170f);
            if (x > pad && x + slot > most)
            {
                (x, y, rowHeight) = (pad, y + label + rowHeight + pad, 0);
            }
            places.Add((x, y, MathF.Round(w), MathF.Round(h)));
            rowHeight = MathF.Max(rowHeight, h);
            x += slot + pad;
            widest = MathF.Max(widest, x);
        }
        var total = y + label + rowHeight + pad;
        var dark = FlyoutTheme.For(AddListPictures.LookFor(dark: true));
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(dark.Base));
            for (var index = 0; index < items.Count; index++)
            {
                var (name, island, style) = items[index];
                var (left, top, w, h) = places[index];
                PopupPaint.Text(dc, gfx, name, 11f, Weight.Regular, dark.TextSecondary, left, top, 220f);
                PopupPaint.Fill(dc, PopupPaint.Rect(left, top + label, w, h), 4f * style.Scale, PopupPaint.Ink(FlyoutTheme.For(style.Look).CardBackground));
                island.Forget();
                Offscreen.Shifted(dc, left, top + label, () => island.PaintCell(dc, 0, style, w, h));
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
