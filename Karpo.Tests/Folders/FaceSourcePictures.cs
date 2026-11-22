// Pictures of §477 (docs\widgets.md): a face's source, Inherited or its own. On the bar, a news face with a feed of its
// own and no feed line, beside one Inherited in a folder with none (its glyph and a dash), and a weather face with a place
// of its own; in the folder's menu, the news face's source row saying "Default (Example News)" and its level, Inherited
// ticked first over the feed line's own picker. RssSamples' feed and SavedWeather's answer: nothing is fetched.
// face-source-bar-<dark|light>.png, face-source-row-<dark|light>.png (the menu), face-source-menu-<dark|light>.png (its level), to KARPO_PNG_DIR. Runs with KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.Rss;
using Karpo.Tests.Edit;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using Karpo.Tests.Rss;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed unsafe class FaceSourcePictures
{
    private static readonly DateTime Now = new(2026, 9, 26, 15, 0, 0);

    /// <summary>A news face with the sample feed of its own and no line; one Inherited with no line; a weather face with Zürich of its own.</summary>
    [UiFact]
    public void FaceSourceBarPictures()
    {
        foreach (var dark in new[] { true, false })
        {
            Offscreen.OnStaThread(gfx =>
            {
                Clock.Now = () => Now;
                var own = FaceSourceTests.FolderOf(FolderFaces.NewsId);
                own.Folder!.Faces[0].Rss = FaceSourceTests.Sample();
                own.Normalize();
                var inherited = FaceSourceTests.FolderOf(FolderFaces.NewsId);
                inherited.Id = "inherited";
                var weather = FaceSourceTests.FolderOf(FolderFaces.WeatherId);
                var place = FaceRows.PlaceKept(weather.Folder!.Faces[0]);
                place.UseDeviceLocation = false;
                Assert.True(OptionTexts.SetPlace(place, new OptionFound("Zürich", "Switzerland", new Place("Zürich", "Switzerland", 47.3667, 8.55))));
                weather.Normalize();
                RssIsland.Made = RssSamples.Settled(Now);
                FolderIsland.WeatherStand = SavedWeather.Cache();
                List<FolderIsland> islands = [];
                try
                {
                    foreach (var holder in new[] { own, inherited, weather })
                    {
                        var island = new FolderIsland(Config.IslandKey(null, holder.Id), holder, gfx, new Config());
                        _ = ((IIsland)island).Refresh(gfx);
                        islands.Add(island);
                    }
                    Assert.Empty(islands[0].Tiles);
                    Assert.NotNull(islands[0].Feed);
                    Assert.Null(islands[1].Feed);
                    var look = ListFormPictures.LookFor(dark);
                    var theme = FlyoutTheme.For(look);
                    var styles = new[] { 1f, 1.5f }.Select(scale => new IslandStyle(scale, false, 48 * scale, MathF.Round(24 * scale), look)).ToArray();
                    const float gap = 12;
                    var width = gap + styles.Max(style => islands.Sum(island => island.CellSize(0, style).Width + gap));
                    var height = gap + styles.Sum(style => islands.Max(island => island.CellSize(0, style).Height) + gap);
                    Offscreen.Bitmap(width, height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, width, height), 0, PopupPaint.Ink(theme.Base));
                        var y = gap;
                        foreach (var style in styles)
                        {
                            var x = gap;
                            foreach (var island in islands)
                            {
                                var (w, h) = island.CellSize(0, style);
                                PopupPaint.Fill(dc, PopupPaint.Rect(x, y, w, h), 4 * style.Scale, PopupPaint.Ink(theme.CardBackground));
                                var at = (x, y);
                                Offscreen.Shifted(dc, at.x, at.y, () => island.PaintCell(dc, 0, style, w, h));
                                x += w + gap;
                            }
                            y += islands.Max(island => island.CellSize(0, style).Height) + gap;
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"face-source-bar-{(dark ? "dark" : "light")}.png")));
                }
                finally
                {
                    foreach (var island in islands)
                    {
                        island.Dispose();
                    }
                    RssIsland.Made = null;
                    FolderIsland.WeatherStand = null;
                }
            });
        }
    }

    /// <summary>The news face's menu in a folder holding the sample feed: its source row "Default (Example News)", then its level.</summary>
    [UiFact]
    public void FaceSourceMenuPictures()
    {
        WindowShots.OnHiddenDesktop("face-source", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var holder = FaceSourceTests.FolderOf(FolderFaces.NewsId, FaceSourceTests.FeedOf());
                List<OptionPick> picks = [];
                var items = OptionMenu.Section(OptionRows.OfFace(holder.Folder!, 0), picks);
                var source = items.Single(item => OptionMenu.PickOf(picks, item.Id)?.Key == FaceSources.Key);
                Assert.Equal(S.SettingsValueDefault("Example News"), source.Value!.Text);
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, new POINT { x = work.right / 4, y = work.bottom }, work, Edge.Bottom, null);
                WindowShots.Settle();
                for (var step = 0; step < 40 && menu.HotId != source.Id; step++)
                {
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
                }
                Assert.Equal(source.Id, menu.HotId);
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"face-source-row-{(dark ? "dark" : "light")}.png", dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243)));
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                WindowShots.Settle();
                Assert.True(WindowShots.SaveTogether([.. menu.OpenPanels], $"face-source-menu-{(dark ? "dark" : "light")}.png", dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243)));
                menu.Hide();
            }
        });
    }
}
