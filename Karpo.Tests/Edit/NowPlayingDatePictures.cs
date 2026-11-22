// The now-playing face hidden while nothing plays, and the date in a zone of its own (docs\widgets.md
// §230), on a first start's bar as the scene draws it: the media preset set to hide while nothing
// plays, before the clock button, and a calendar face; the clock button and the calendar face set to
// Tokyo's time at 23:30 here, so their date is tomorrow's. Playing, and paused — the folder gone from
// the bar, the bar shorter by it — at 150 % in the dark, now-playing-date-<playing|idle>.png, to
// KARPO_PNG_DIR.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void NowPlayingAndDateZoneSheets()
    {
        var folder = NotificationsPresetTests.Temp();
        try
        {
            WindowShots.OnHiddenDesktop("now-playing-date", (gfx, _) =>
            {
                Clock.Now = () => new DateTime(2026, 10, 19, 23, 30, 0);
                WindowsClock.SecondsStand = false;
                FolderIsland.WeatherStand = SavedWeather.Cache();
                NotificationPartTile.Stand = NotificationsPresetTests.Store(folder, NotificationsPresetTests.Centre);
                var probe = BatteryIsland.Probe;
                BatteryIsland.Probe = () => new SystemStatus.Battery(76, Plugged: false, Saver: false);
                IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
                try
                {
                    var widths = new Dictionary<bool, float>();
                    foreach (var playing in new[] { true, false })
                    {
                        widths[playing] = NowPlayingBar(gfx, playing, $"now-playing-date-{(playing ? "playing" : "idle")}.png");
                    }
                    // Idle, the folder takes no room: nothing of it is left on the bar.
                    Assert.True(widths[true] > 0, "the now-playing folder takes no room while it plays");
                    Assert.True(widths[false] < 0.5f, $"the now-playing folder keeps {widths[false]} px while nothing plays");
                }
                finally
                {
                    FolderIsland.WeatherStand = null;
                    NotificationPartTile.Stand = null;
                    BatteryIsland.Probe = probe;
                    IndicatorSources.Made = null;
                    WindowsClock.SecondsStand = null;
                    Clock.Now = Clock.Real;
                }
            });
        }
        finally
        {
            NotificationsPresetTests.Clean(folder);
        }
    }

    /// <summary>The bar with the media folder over a track playing or paused; how long the folder's block is along it.</summary>
    private static float NowPlayingBar(Gfx gfx, bool playing, string file)
    {
        const uint dpi = 144;
        var config = new Config { Height = 48 };
        var look = AddListPictures.LookFor(dark: true) with { Animations = false };
        var area = new RECT { left = 0, top = 0, right = 2560, bottom = (int)(dpi * 10) };
        var bar = FreshBar();
        var clock = bar.FindIndex(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
        bar[clock].Folder!.Face.Set(FolderFaces.DateTime.Option(DateTimeFace.Zone)!, "Tokyo Standard Time");
        var media = Presets.Folder(Presets.Find(Presets.MediaId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        media.Folder!.Face.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, true);
        var date = BarKind.FolderList.New();
        date.Id = "date-tokyo";
        date.Folder!.Faces = [CalendarFace.New(withDay: true)];
        date.Folder.Faces[0].Set(FolderFaces.Calendar.Option(CalendarFace.Zone)!, "Tokyo Standard Time");
        date.Normalize();
        bar.InsertRange(clock, [media, date]);
        MediaIsland.Stand = new MediaStand(Title: "Clair de lune", Artist: "Claude Debussy", AppName: "Spotify", IsPlaying: playing, Art: Karpo.Tests.QuickSettings.QuickFlyoutOffscreenTests.Cover());
        List<PartSpec> specs;
        IslandCollection islands;
        try
        {
            (specs, islands) = Made(gfx, config, bar);
        }
        finally
        {
            MediaIsland.Stand = null;
        }
        try
        {
            var content = new Content { Islands = islands, Tiles = [.. Enumerable.Range(0, 3).Select(BusyApp)] };
            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
            var at = bar.IndexOf(media);
            var block = layout.BlockRect(at);
            OverADesktop(gfx, layout, look, content, null, file, (int)layout.BlockRect(Math.Max(0, at - 1)).X - (int)(24 * dpi / 96f));
            return block.Width;
        }
        finally
        {
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }
}
