// Pictures of the faces of words down a bar on its side, at the Sizes a side bar is set to — its
// narrowest, the person's ~42 DIP, the taskbar's 48 and up to its widest (docs\widgets.md §598; the
// person, 2026-11-09: "the screen time face does not show nicely when the taskbar is shown on the side
// and not so wide"; "same with the datetime face. at some point the month and year does not show
// anymore"). A column per Size, the bar's strip in it with each face's cell, a row per face:
// side-<dark|light>-<100|150>[-text150].png in KARPO_PNG_DIR. Nothing is asserted here: the fit tests
// (SideFaceFitTests) hold what these show.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using static Karpo.Tests.Sizing.OneRowTests;

namespace Karpo.Tests.Folders;

public sealed unsafe class SideFaceSheets
{
    /// <summary>The Sizes drawn: the thinnest the icons allow, the person's, the taskbar's, and up to the widest.</summary>
    private static readonly int[] Sizes = [32, 42, 48, 64, 96, 160];

    /// <summary>A long day's evening, late in a month with a long name.</summary>
    private static readonly DateTime Evening = new(2026, 12, 28, 22, 58, 59);

    private static IslandStyle Side(int size, uint dpi, Look look)
    {
        var config = new Config { Height = size };
        return new IslandStyle(dpi / 96f, Vertical: true, Layout.CellPx(dpi, config), Layout.IconPx(dpi, config), look);
    }

    private static List<(string Name, FolderIsland Island)> Faces(Gfx gfx)
    {
        var faces = new List<(string, FolderIsland)>();
        void Add(string name, BarItem folder) => faces.Add((name, FolderFaceTests.Make(gfx, folder)));
        Add("screen time", FolderFaceTests.Folder(FolderFaces.ScreenTimeId));
        Add("time and date", FolderFaceTests.Folder(FolderFaces.DateTimeId));
        Add("time and date + s", FolderFaceTests.Folder(FolderFaces.DateTimeId, (DateTimeFace.Seconds, DateTimeFace.Shown)));
        Add("calendar", FolderFaceTests.Folder(FolderFaces.CalendarId));
        Add("calendar day + date", FolderFaceTests.Folder(FolderFaces.CalendarId, (CalendarFace.Form, CalendarFace.DayAndDate)));
        Add("sun", Sun());
        Add("battery", FolderFaceTests.Folder(FolderFaces.BatteryId));
        Add("focus", FolderFaceTests.Folder(FolderFaces.FocusId));
        return faces;
    }

    /// <summary>A folder wearing the sun face for Zürich: its next sunrise's time.</summary>
    private static BarItem Sun()
    {
        var island = BarKind.FolderList.New();
        island.Id = "sun";
        var face = SunFace.New();
        face.Weather = new WeatherOptions { Place = "Zürich, Zurich, Switzerland", Latitude = 47.3769, Longitude = 8.5417 };
        island.Folder!.Face = face;
        island.Normalize();
        return island;
    }

    [UiFact]
    public void SideFaceSheet()
    {
        Offscreen.OnStaThread(gfx =>
        {
            Clock.Now = () => Evening;
            var log = new ScreenTimeLog();
            log.Ensure(DateOnly.FromDateTime(Evening)).Total = TimeSpan.FromMinutes((13 * 60) + 42);
            ScreenTimeFaceDrawing.Stand = () => new ScreenTimeSnapshot(DateOnly.FromDateTime(Evening), log);
            var faces = Faces(gfx);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var (dpi, text) in new (uint, float)[] { (96, 1f), (144, 1f), (144, 1.5f) })
                    {
                        Sheet(gfx, faces, dark, dpi, text, $"side-{(dark ? "dark" : "light")}-{dpi * 100 / 96}{(text > 1 ? "-text150" : "")}.png");
                    }
                }
            }
            finally
            {
                Clock.Now = Clock.Real;
                ScreenTimeFaceDrawing.Stand = null;
                foreach (var (_, island) in faces)
                {
                    island.Dispose();
                }
            }
        });
    }

    private static void Sheet(Gfx gfx, List<(string Name, FolderIsland Island)> faces, bool dark, uint dpi, float text, string file)
    {
        var scale = dpi / 96f;
        var look = LookFor(dark, text);
        var theme = FlyoutTheme.For(look);
        var pad = 10f * scale;
        var label = 120f * scale;
        var head = 18f * scale;
        var styles = Sizes.Select(size =>
        {
            var config = new Config { Height = size };
            return (Size: size, Style: Side(size, dpi, look), Bar: Layout.ThicknessPx(dpi, config));
        }).ToList();
        var rows = faces.Select(face => styles.Max(side =>
        {
            face.Island.Forget();
            return face.Island.CellSize(0, side.Style).Height;
        })).ToList();
        var width = label + styles.Sum(side => side.Bar + pad) + pad;
        var height = head + pad + rows.Sum(row => row + pad);
        Offscreen.Bitmap(width, height, dc =>
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
            var x = label;
            foreach (var (size, style, bar) in styles)
            {
                PopupPaint.Text(dc, gfx, $"{size}", 11f * scale, Weight.Regular, theme.TextSecondary, x, pad / 2, bar);
                PopupPaint.Fill(dc, PopupPaint.Rect(x, head, bar, height - head), 0, PopupPaint.Ink(theme.CardBackground) with { a = 0.35f });
                var y = head + pad;
                for (var index = 0; index < faces.Count; index++)
                {
                    var island = faces[index].Island;
                    island.Forget();
                    var (w, h) = island.CellSize(0, style);
                    var left = x + ((bar - w) / 2);
                    var top = y + ((rows[index] - h) / 2);
                    PopupPaint.Fill(dc, PopupPaint.Rect(left, top, w, h), 4f * scale, PopupPaint.Ink(theme.CardBackground));
                    var copy = style;
                    Offscreen.Shifted(dc, left, top, () => island.PaintCell(dc, 0, copy, w, h));
                    y += rows[index] + pad;
                }
                x += bar + pad;
            }
            var labelY = head + pad;
            for (var index = 0; index < faces.Count; index++)
            {
                PopupPaint.Text(dc, gfx, faces[index].Name, 11f * scale, Weight.Regular, theme.TextSecondary, pad, labelY + (rows[index] / 2) - (7f * scale), label - pad);
                labelY += rows[index] + pad;
            }
        }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, file)));
    }
}
