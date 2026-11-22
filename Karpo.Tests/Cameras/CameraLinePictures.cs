// camera: a camera line drawn (docs\widgets.md §331) at one row, two, five (about 16:9) and eight, in each of
// the ways it can be: a moving picture, a snapshot with its time, whole inside the line, connecting before a
// picture, failed before one and failed over the last one, and not set up — camera-lines-<dark|light>.png at
// 150 %, and the line's settings level (camera-menu-dark.png is the menu's rows as words in the output). The
// sheets go to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures) before anything is asserted. Runs only with
// KARPO_UI_TESTS=1, like the other picture tests.
//
// What is held: every drawing stays inside its line; a line of one row is a row. That a filling picture covers its
// whole line and one shown whole is letterboxed between the gutters is CameraFillTests' (§463).

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.CameraLines;

public sealed unsafe class CameraLinePictures
{
    private sealed record Shot(string Caption, CameraShot Held, CameraOptions Options);

    private static readonly int[] Heights = [1, 2, 5, 8];

    [UiFact]
    public void TheLineIsDrawnInEveryWayItCanBe()
    {
        OnStaThread(gfx =>
        {
            var frame = Karpo.Dock.CameraPictures.Decode(FakeCamera.Picture(640, 360), 640)!;
            var at = DateTime.UtcNow.AddMinutes(-3);
            var known = new CameraOptions { Url = "https://www.foto-webcam.eu/webcam/pizol/current/816.jpg" };
            var stream = new CameraOptions { Url = "http://garden.local/video.mjpg", Name = "Garden" };
            Shot[] shots =
            [
                new("Moving (MJPEG)", new CameraShot(frame, 40, CameraState.Live, null, CameraProtocol.Mjpeg, at), stream),
                new("Snapshot, its time", new CameraShot(frame, 2, CameraState.Live, null, CameraProtocol.Snapshot, at), known),
                new("Whole inside the line", new CameraShot(frame, 2, CameraState.Live, null, CameraProtocol.Snapshot, at), new CameraOptions { Url = known.Url, Fit = CameraFit.Whole }),
                new("Connecting", new CameraShot(null, 0, CameraState.Connecting, null, null, null), stream),
                new("Failed, no picture", new CameraShot(null, 0, CameraState.Failed, S.DockCameraRefused(404), null, null), stream),
                new("Failed over the last picture", new CameraShot(frame, 9, CameraState.Failed, S.DockCameraUnreachable, CameraProtocol.Mjpeg, at), stream),
                new("RTSP Windows can't play", new CameraShot(null, 0, CameraState.Failed, S.DockCameraRtsp, CameraProtocol.Rtsp, null), new CameraOptions { Url = "rtsp://192.168.1.20/stream1" }),
                new("Not set up", CameraShot.None, new CameraOptions()),
            ];
            var failures = new List<string>();
            foreach (var dark in new[] { true, false })
            {
                Sheet(gfx, shots, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"camera-lines-{(dark ? "dark" : "light")}.png"), failures);
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        });
    }

    private static CameraLine Line(Gfx gfx, Shot shot)
    {
        var item = ListKind.Camera.New();
        item.Id = "camera";
        item.Camera = shot.Options;
        item.Normalize();
        var line = (CameraLine)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "camera"), item, ListKind.Camera, gfx, new Config())!;
        line.Source = () => shot.Held;
        return line;
    }

    /// <summary>Each way in a row, at each height across, under their names; each line held inside its box.</summary>
    private static void Sheet(Gfx gfx, IReadOnlyList<Shot> shots, float scale, Look look, string path, List<string> failures)
    {
        var theme = FlyoutTheme.For(look);
        var style = FolderFlyout.StyleFor(scale, look, theme);
        var width = FolderFlyout.ListWidth(scale);
        var pad = PopupPaint.Round(16 * scale);
        var label = PopupPaint.Round(11 * scale);
        var labelLine = TilePaint.LineHeight(gfx, label);
        var tallest = LineRows.Px(Heights[^1], style);
        var sheetWidth = pad + (Heights.Length * (width + pad));
        var sheetHeight = pad + (shots.Count * (labelLine + tallest + pad));
        var lines = shots.Select(shot => Line(gfx, shot)).ToList();
        try
        {
            Bitmap(sheetWidth, sheetHeight, dc =>
            {
                PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                    PopupPaint.Ink(look.Dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
                var y = pad;
                for (var index = 0; index < shots.Count; index++)
                {
                    for (var slot = 0; slot < Heights.Length; slot++)
                    {
                        var rows = Heights[slot];
                        var x = pad + (slot * (width + pad));
                        var height = LineRows.Px(rows, style);
                        PopupPaint.Text(dc, gfx, $"{shots[index].Caption}, {rows} row{(rows == 1 ? "" : "s")}", label, Weight.Regular, theme.TextSecondary, x, y);
                        var plate = PopupPaint.Rect(x, y + labelLine, width, height);
                        PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * scale), PopupPaint.Ink(theme.Base));
                        PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * scale), MathF.Max(1, MathF.Round(scale)), PopupPaint.Ink(theme.Stroke));
                        var context = dc;
                        var line = lines[index];
                        PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * scale), () => Shifted(context, plate.left, plate.top, () =>
                            FolderFlyout.PaintContent(context, line, PopupPaint.Rect(0, 0, width, height), style)));
                        // Held: one row is a row; taller, the picture.
                        var box = PopupPaint.Rect(0, 0, width, height);
                        if (line.One(box, style) != (rows == 1))
                        {
                            failures.Add($"{shots[index].Caption}: {rows} rows drawn as {(line.One(box, style) ? "one row" : "a picture")}");
                        }
                    }
                    y += labelLine + tallest + pad;
                }
            }, bitmap => Save(bitmap, path));
        }
        finally
        {
            foreach (var line in lines)
            {
                line.Dispose();
            }
        }
    }
}
