// The now-playing face beside other faces, hidden while nothing plays (docs\widgets.md §237), on a
// first start's bar as the scene draws it: a quick settings button wearing Wi-Fi, sound and now
// playing set to hide while nothing plays, as the person's is, before the clock button. Playing, and
// paused — only the now-playing face gone, the button shorter by it, Wi-Fi and sound kept — at 150 %
// in the dark, now-playing-beside-<playing|idle>.png, to KARPO_PNG_DIR.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void NowPlayingBesideOtherFacesSheets()
    {
        WindowShots.OnHiddenDesktop("now-playing-beside", (gfx, _) =>
        {
            Clock.Now = () => new DateTime(2026, 10, 19, 21, 30, 0);
            WindowsClock.SecondsStand = false;
            IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
            try
            {
                var widths = new Dictionary<bool, float>();
                foreach (var playing in new[] { true, false })
                {
                    widths[playing] = NowPlayingBesideBar(gfx, playing, $"now-playing-beside-{(playing ? "playing" : "idle")}.png");
                }
                // Paused, the button keeps Wi-Fi and sound: shorter, and still on the bar.
                Assert.True(widths[false] > 0, "the quick settings button left the bar with its now-playing face");
                Assert.True(widths[false] < widths[true] - 20, $"the button is {widths[false]} px paused against {widths[true]} px playing");
            }
            finally
            {
                IndicatorSources.Made = null;
                WindowsClock.SecondsStand = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>The bar with the quick settings button over a track playing or paused; how long its block is along it.</summary>
    private static float NowPlayingBesideBar(Gfx gfx, bool playing, string file)
    {
        const uint dpi = 144;
        var config = new Config { Height = 48 };
        var look = AddListPictures.LookFor(dark: true) with { Animations = false };
        var area = new RECT { left = 0, top = 0, right = 2560, bottom = (int)(dpi * 10) };
        var bar = FreshBar();
        var clock = bar.FindIndex(island => island.Folder?.Face.Kind == FolderFaces.DateTimeId);
        var quick = Presets.Folder(Presets.Find(Presets.MediaId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
        var media = quick.Folder!.Face;
        media.Set(FolderFaces.NowPlaying.Option(NowPlayingFace.HideIdle)!, true);
        quick.Folder.Faces = [new FolderFace { Kind = FolderFaces.WifiId }, new FolderFace { Kind = FolderFaces.SoundId }, media];
        bar.Insert(clock, quick);
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
            var at = bar.IndexOf(quick);
            OverADesktop(gfx, layout, look, content, null, file, (int)layout.BlockRect(Math.Max(0, at - 1)).X - (int)(24 * dpi / 96f));
            return layout.BlockRect(at).Width;
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
