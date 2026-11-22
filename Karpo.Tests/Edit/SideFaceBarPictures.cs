// The faces of words on a whole bar down a side of a display (2026-10-13, "make sure that all the
// widgets look good on the side as well"): a Taskbar (islands) and a Dock (floating) on the left and
// on the right, holding the clock in two formats, the time and date, the calendar, a running timer
// under its label, the weather, the stock, the mail and the input indicator — at rest, at 32, 48 and
// 96 DIP, at 150 %, dark (and light at 48). To KARPO_PNG_DIR as
// side-faces-bar-<taskbar|dock>-<left|right>-<thickness>-<dark|light>-150.png. The sheets per face are
// Folders\SideFacePictures.cs.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The faces a side bar shows in these pictures, on its middle section.</summary>
    private static List<BarItem> SideFaces()
    {
        var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
        var stock = Presets.Folder(Presets.Find(Presets.MarketId)!, machine);
        stock.Folder!.Face.Market = (MarketOffscreenTests.Held("MSFT"));
        var weather = WeatherFaceTests.Folder(WeatherFace.Simple);
        List<BarItem> faces =
        [
            FolderFaceTests.Folder(FolderFaces.ClockId),
            FolderFaceTests.Folder(FolderFaces.ClockId, (ClockFace.Hours, ClockFace.Twelve), (ClockFace.Seconds, "true")),
            FolderFaceTests.Folder(FolderFaces.DateTimeId),
            FolderFaceTests.Folder(FolderFaces.CalendarId),
            FolderFaceTests.Wearing(FolderFaces.ClockId, timer: true, options: [(ClockFace.Label, "Tea")]),
            weather,
            stock,
            FolderFaceTests.Folder(FolderFaces.MailId),
            Presets.Folder(Presets.Find(Presets.KeyboardId)!, machine),
        ];
        for (var index = 0; index < faces.Count; index++)
        {
            faces[index].Id = $"face{index}";
            faces[index].Section = BarSection.Middle;
            faces[index].Normalize();
        }
        return faces;
    }

    [UiFact]
    public void SideFaceBarSheets()
    {
        WindowShots.OnHiddenDesktop("side-faces-bar", (gfx, instance) =>
        {
            const uint dpi = 144;
            Clock.Now = () => new DateTime(2026, 9, 30, 22, 58, 59);
            var config = new Config();
            FolderIsland.WeatherStand = SavedWeather.Cache();
            Karpo.Dock.Pim.MailIsland.Stand = FolderFaceTests.Letters;
            MarketIsland.Made = MarketOffscreenTests.Settled(new() { ["MSFT"] = MarketOffscreenTests.Answer("MSFT", "Microsoft Corporation", price: 412.34, percent: 1.24, previous: 407.3) });
            try
            {
                foreach (var (thickness, themes) in new[] { (32, new[] { true }), (48, new[] { true, false }), (96, new[] { true }) })
                {
                    foreach (var dark in themes)
                    {
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        foreach (var edge in new[] { Edge.Left, Edge.Right })
                        {
                            foreach (var (name, type, show) in new[] { ("taskbar", LayoutType.Taskbar, EdgeMode.Reserve), ("dock", LayoutType.Dock, EdgeMode.Float) })
                            {
                                List<BarItem> islands = [BarKind.Start.New(), .. SideFaces()];
                                islands[0].Section = BarSection.Start;
                                Sections.Order(islands);
                                var layout = new Picture(new TaskbarLayout { Id = "pictures", Name = "Pictures", Type = type, Show = show, Size = thickness, Islands = islands }, edge);
                                var file = $"side-faces-bar-{name}-{(edge == Edge.Left ? "left" : "right")}-{thickness}-{(dark ? "dark" : "light")}-150.png";
                                EditedDisplay(gfx, instance, dpi, config, look, layout, file, editing: false, dips: new System.Numerics.Vector2(1280f, 1440f));
                            }
                        }
                    }
                }
            }
            finally
            {
                FolderIsland.WeatherStand = null;
                Karpo.Dock.Pim.MailIsland.Stand = null;
                MarketIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });
    }
}
