// The system widgets down a bar on the left or the right (the person, 2026-10-13: "the clock widget
// when on the side overflows ... also make sure that all the widgets look good on the side as well.
// if possible dont try to flip text. go ahead and check every taskbar widget").
//
// Down a side bar a widget is as wide as the bar is thick, and its content has to fit that width:
// stacked, shortened or left out, never turned (docs\widgets.md §12.4, §163). These hold every system
// widget — the sound and the brightness with their sliders, the status icons, the switches, the
// security shield, and the system presets on their faces: the network, Bluetooth, the sound, the
// battery and the power button, the hotspot, casting and nearby sharing, the removable devices, the
// drives, the notifications, the performance readings, now playing and the combined ones — at every
// thickness from the thinnest Karpo allows to a wide one, at 100 and 150 %, dark and light, on an
// islands bar and on a dock's square panel: each cell exactly the bar's width, its ink inside its
// cell and centred across it.
//
// The sheets go to KARPO_PNG_DIR as side-system-<thickness>-<dpi>-<dark|light>-<islands|connected>.png
// before anything is asserted, each widget on a strip of the bar's thickness with its name beside it.
//
// In the samplers' collection because the performance faces hold the process-wide samplers.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;

namespace Karpo.Tests.Sizing;

[Collection(Perf.SamplerState.Collection)]
public sealed unsafe class SideSystemTests
{
    /// <summary>The thicknesses held: the thinnest a bar may be, the taskbar's own, and wide ones.</summary>
    private static readonly int[] Thicknesses = [Config.MinHeight, 40, 48, 72, 120];

    /// <summary>The system kinds that stand on the bar as widgets of their own.</summary>
    private static bool System(BarKind kind) =>
        kind is BarKind.Indicators or BarKind.Volume or BarKind.Brightness or BarKind.Security
        || BarEntries.Of(kind).Drawing == WidgetSource.Switch;

    /// <summary>The system presets, on the faces they come with.</summary>
    private static readonly string[] SystemPresets =
    [
        Presets.WifiId, Presets.BluetoothId, Presets.SoundId, Presets.VpnId, Presets.NetworkHubId,
        Presets.BatteryId, Presets.PowerId, Presets.PowerBatteryId, Presets.QuickPanelId,
        Presets.HotspotId, Presets.CastId, Presets.NearbyId, Presets.ProjectId,
        Presets.RemovableId, Presets.DevicesId, Presets.DriveId, Presets.NetworkDriveId,
        Presets.NotificationsId, Presets.InboxId, Presets.ClipboardId,
        Presets.CpuId, Presets.GpuId, Presets.MemoryId, Presets.DiskId, Presets.NetworkActivityId,
        Presets.MediaId, Presets.MediaCenterId,
        "connectivityGroup", "networkGroup", "quickSettingsGroup", "systemGroup", "storageGroup",
    ];

    /// <summary>Every system widget, made with the stand-ins the sheets use for what is live.</summary>
    private static List<(string Name, CellIsland Island)> Make(Gfx gfx)
    {
        Clock.Now = () => new DateTime(2026, 12, 23, 10, 30, 0);
        MediaIsland.Stand = new MediaStand(Title: "Air on the G String, from the Orchestral Suite No. 3 in D major");
        BatteryIsland.Probe = () => new SystemStatus.Battery(88, Plugged: false, Saver: false);
        VolumeWatch.Stand = new SampleVolume { Level = 60 };
        FolderIsland.StatusStand = FolderFaceTests.Radios;
        FolderIsland.DevicesStand = FolderFaceTests.Sticks;
        Karpo.Dock.Pim.MailIsland.Stand = FolderFaceTests.Letters;
        // Three status icons: the microphone in use, a metered network and a restart due.
        IndicatorSources.Made = () => new SampleIndicators(new IndicatorState { Microphone = ["Teams"], Metered = true, Update = UpdateState.Due });
        var config = new Config();
        var made = new List<(string, CellIsland)>();
        try
        {
            foreach (var entry in OneRowTests.Entries().Where(entry => System(entry.Widget.Kind)))
            {
                var island = (CellIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, entry.Widget.Id), entry.Widget, gfx, config)!;
                if (island is FolderIsland { ShowsStatus: true } folder)
                {
                    folder.SetStatus(FolderFaceTests.Radios, gfx);
                }
                _ = island.Refresh(gfx);
                made.Add((entry.Name, island));
            }
            // This PC's fixed drives, so a drive face has a drive to read.
            var machine = PresetMachine.Read(hasBattery: true);
            foreach (var id in SystemPresets)
            {
                if (Presets.Find(id) is not { } preset)
                {
                    continue;
                }
                var holder = Presets.Folder(preset, machine);
                // The drive's line pointed at C:, as set up, so its face shows a drive and not "Set up".
                foreach (var (_, item) in Karpo.Core.Folders.All([holder]))
                {
                    if (id == Presets.DriveId && item is not BarItem && item.Pin is null)
                    {
                        item.Drive = new DriveTargetOptions { Letter = "C:", Label = "Local Disk" };
                    }
                }
                var island = (CellIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, holder.Id), holder, gfx, config)!;
                if (island is FolderIsland folder)
                {
                    // A reading with a minute of history behind it, so a performance face shows a figure.
                    foreach (var graph in folder.Tiles.Select(tile => tile.Island).OfType<PerformanceIsland>())
                    {
                        for (var at = 0; at < 90; at++)
                        {
                            var wave = 0.45f + (0.3f * MathF.Sin(at / 7f));
                            graph.Feed(wave, down: 400_000 + (300_000 * wave), up: 20_000);
                        }
                    }
                }
                _ = island.Refresh(gfx);
                made.Add((id.Replace("Group", "", StringComparison.Ordinal), island));
            }
            // Several faces on one button: Windows' network, sound and battery button.
            var three = BarKind.FolderList.New();
            three.Id = "three";
            three.Folder.Faces = [FolderFace.Of(FolderFaces.WifiId), FolderFace.Of(FolderFaces.SoundId), FolderFace.Of(FolderFaces.BatteryId)];
            three.Normalize();
            var faces = (CellIsland)Karpo.Dock.Islands.Create(Config.IslandKey(null, three.Id), three, gfx, config)!;
            _ = faces.Refresh(gfx);
            made.Add(("wifi + sound + battery", faces));
        }
        finally
        {
            FolderIsland.StatusStand = null;
            FolderIsland.DevicesStand = null;
            Karpo.Dock.Pim.MailIsland.Stand = null;
            IndicatorSources.Made = null;
        }
        return made;
    }

    private static void Done(List<(string Name, CellIsland Island)> made)
    {
        foreach (var (_, island) in made)
        {
            island.Dispose();
        }
        Clock.Now = Clock.Real;
        MediaIsland.Stand = null;
        VolumeWatch.Stand = null;
    }

    private static IslandStyle Style(int thickness, uint dpi, bool dark, bool square)
    {
        var config = new Config { Height = thickness };
        return new IslandStyle(dpi / 96f, true, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), OneRowTests.LookFor(dark)) { Square = square };
    }

    /// <summary>
    /// Every system widget down a side bar: every cell exactly the bar's width, its ink inside the
    /// cell and centred across it, at every thickness, at 100 and 150 %, dark and light, islands and
    /// connected. The sheets are written first.
    /// </summary>
    [UiFact]
    public void EverySystemWidgetFitsDownASideBar()
    {
        var failures = new List<string>();
        Offscreen.OnStaThread(gfx =>
        {
            var made = Make(gfx);
            try
            {
                foreach (var thickness in Thicknesses)
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        foreach (var dark in new[] { true, false })
                        {
                            foreach (var square in new[] { false, true })
                            {
                                var style = Style(thickness, dpi, dark, square);
                                var where = $"{thickness} DIP, {dpi} DPI, {(dark ? "dark" : "light")}, {(square ? "connected" : "islands")}";
                                Sheet(gfx, made, style, $"side-system-{thickness}-{dpi}-{(dark ? "dark" : "light")}-{(square ? "connected" : "islands")}.png");
                                foreach (var (name, island) in made)
                                {
                                    failures.AddRange(Check(island, style, $"{name} at {where}"));
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                Done(made);
            }
        });
        File.WriteAllLines(Path.Combine(Offscreen.Pictures, "side-system-failures.txt"), failures);
        Assert.True(failures.Count == 0, $"{failures.Count} cells do not fit a side bar:\n{string.Join("\n", failures.Take(60))}");
    }

    private static List<string> Check(CellIsland island, IslandStyle style, string where)
    {
        var failures = new List<string>();
        island.Forget();
        for (var cell = 0; cell < island.CellCount; cell++)
        {
            var (width, height) = island.CellSize(cell, style);
            if (MathF.Abs(width - style.Cell) > 0.5f)
            {
                failures.Add($"{where}, cell {cell}: {width:0.#} px wide on a {style.Cell:0.#} px bar");
            }
            const float margin = 16f;
            (float Left, float Top, float Right, float Bottom) ink = default;
            var index = cell;
            var pieces = new List<(D2D_RECT_F Box, Painted What)>();
            PaintWatch.Watching = (box, what, color) =>
            {
                if (box.right > box.left && box.bottom > box.top)
                {
                    pieces.Add((box, what));
                }
                return color;
            };
            try
            {
                Offscreen.Bitmap(
                    width + (2 * margin),
                    height + (2 * margin),
                    dc => Offscreen.Shifted(dc, margin, margin, () => island.PaintCell(dc, index, style, width, height)),
                    bitmap => ink = Offscreen.InkOf(bitmap, (uint)MathF.Ceiling(width + (2 * margin)), (uint)MathF.Ceiling(height + (2 * margin))));
            }
            finally
            {
                PaintWatch.Watching = null;
            }
            var (left, top, right, bottom) = (ink.Left - margin, ink.Top - margin, ink.Right - margin, ink.Bottom - margin);
            if (right <= left)
            {
                continue;
            }
            if (left < -0.5f || top < -0.5f || right > width + 0.5f || bottom > height + 0.5f)
            {
                failures.Add($"{where}, cell {cell}: draws {left:0.#},{top:0.#}-{right:0.#},{bottom:0.#} outside its {width:0.#} by {height:0.#} cell");
            }
            // Centred across the bar: the ink, but for a badge — a count beside a glyph rather than
            // under it, on its corner as the horizontal bar has it too — which is centred on its glyph.
            var badged = pieces.Any(piece => piece.What == Painted.Text && Beside(pieces, piece.Box));
            var (from, to) = badged
                ? (pieces.Where(piece => piece.What != Painted.Text || !Beside(pieces, piece.Box)).Min(piece => piece.Box.left),
                   pieces.Where(piece => piece.What != Painted.Text || !Beside(pieces, piece.Box)).Max(piece => piece.Box.right))
                : (left, right);
            var off = ((from + to) / 2f) - (width / 2f);
            if (MathF.Abs(off) > MathF.Max(1.5f, 0.06f * width))
            {
                failures.Add($"{where}, cell {cell}: {(badged ? "its pieces are" : "its ink is")} {off:+0.0;-0.0} px off the middle of the bar ({from:0.#}-{to:0.#} of {width:0.#})");
            }
        }
        return failures;
    }

    /// <summary>Whether a line of text stands beside a glyph down the bar, level with it, rather than over or under it: a badge's count.</summary>
    private static bool Beside(List<(D2D_RECT_F Box, Painted What)> pieces, D2D_RECT_F text) =>
        pieces.Any(piece => piece.What == Painted.Glyph
            && MathF.Min(text.bottom, piece.Box.bottom) - MathF.Max(text.top, piece.Box.top) > 0.5f * (text.bottom - text.top));

    /// <summary>
    /// One sheet: each widget on a strip of the bar's thickness, as the bar shows it — on a card on an
    /// islands bar, flush on a connected one — its name beside it, in columns down the sheet.
    /// </summary>
    private static void Sheet(Gfx gfx, List<(string Name, CellIsland Island)> made, IslandStyle style, string file)
    {
        var scale = style.Scale;
        var pad = 10f * scale;
        var label = 150f * scale;
        var most = 1100f * scale;
        var gap = 6f * scale;
        var theme = FlyoutTheme.For(style.Look);
        var places = new List<(float X, float Y, List<(float W, float H)> Cells)>();
        var x = pad;
        var y = pad;
        var widest = 0f;
        var tallest = 0f;
        foreach (var (_, island) in made)
        {
            island.Forget();
            var cells = Enumerable.Range(0, island.CellCount).Select(cell => island.CellSize(cell, style)).ToList();
            var length = cells.Sum(cell => cell.Height);
            if (y > pad && y + length > most)
            {
                y = pad;
                x += style.Cell + label + (2 * pad);
            }
            places.Add((x, y, cells));
            y += MathF.Max(length, 16f * scale) + gap;
            tallest = MathF.Max(tallest, y);
            widest = x + style.Cell + label + pad;
        }
        var total = tallest + pad;
        Offscreen.Bitmap(widest, total, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, widest, total), 0, PopupPaint.Ink(theme.Base));
            for (var index = 0; index < made.Count; index++)
            {
                var (name, island) = made[index];
                var (left, top, cells) = places[index];
                var length = cells.Sum(cell => cell.H);
                PopupPaint.Text(dc, gfx, name, 11f * scale, Weight.Regular, theme.TextSecondary, left + style.Cell + pad, top, label);
                PopupPaint.Fill(dc, PopupPaint.Rect(left, top, style.Cell, length), style.Square ? 0 : 4f * scale, PopupPaint.Ink(style.Square ? theme.Base : theme.CardBackground));
                var at = top;
                for (var cell = 0; cell < cells.Count; cell++)
                {
                    var (w, h) = cells[cell];
                    var shown = style;
                    var which = cell;
                    var painted = island;
                    Offscreen.Shifted(dc, left, at, () => painted.PaintCell(dc, which, shown, w, h));
                    at += h;
                }
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
