// An attached folder flat on its ground (§649): the clock preset's folder at the end of a Taskbar on the bottom and the left
// edge of a display at 150 %, open, Attached, its lines in two columns and a divider after its first line — its lines on the
// ground with no card, the divider a line of the folder's outline —, dark and light, on the bar's own colour and on a colour of
// the Appearance's Flyouts › Attached folder background (a dark blue, a pale one); and the Appearance page's Flyouts tab,
// its preview's folder attached on that colour. To KARPO_PNG_DIR as
//
//   attached2-<bar|custom>-<bottom|left>-<dark|light>-150.png
//   attached2-appearance-<dark|light>.png
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The colour said for each mode: a dark blue and a pale one, of the backgrounds' swatches.</summary>
    private static Argb CustomGround(bool dark) => dark ? new Argb(255, 0x1A, 0x3A, 0x63) : new Argb(255, 0xC5, 0xDC, 0xF5);

    [UiFact]
    public void AttachedFlatSheets()
    {
        WindowShots.OnHiddenDesktop("attached-flat-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            const float scale = dpi / 96f;
            foreach (var dark in new[] { true, false })
            {
                var theme = dark ? "dark" : "light";
                foreach (var custom in new[] { false, true })
                {
                    var config = new Config();
                    if (custom)
                    {
                        Assert.True(Looks.WritePaint(Looks.Section(config, LookPart.Flyouts), Tone.Ground, dark, new Karpo.Core.Paint(CustomGround(dark))));
                    }
                    var look = AddListPictures.LookFor(dark).Wearing(config) with { Animations = false };
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                    {
                        EditedDisplay(gfx, instance, dpi, config, look, DividedClock(edge),
                            $"attached2-{(custom ? "custom" : "bar")}-{edge.ToString().ToLowerInvariant()}-{theme}-150.png",
                            editing: false, folder: true, folderFace: FolderFaces.ClockId);
                    }
                }

                // The Appearance page's Flyouts tab: its preview's folder attached on the colour said.
                var wallpaper = DuskWallpaper();
                var source = new WallpaperSource(wallpaper, File.GetLastWriteTimeUtc(wallpaper).Ticks, WallpaperFit.Fill, new Argb(255, 0, 0, 0));
                var said = MenuCorpus.Fresh();
                Assert.True(Looks.WritePaint(Looks.Section(said, LookPart.Flyouts), Tone.Ground, dark, new Karpo.Core.Paint(CustomGround(dark))));
                var windows = AddListPictures.LookFor(dark) with { Animations = false };
                var spot = new BarSpot(MenuCorpus.Display, "One") { Attached = [MenuCorpus.Display] };
                var page = PanelRows.Page(PanelPage.Appearance, S.SettingsAppearanceHeader, PanelRows.GlyphOf(PanelPage.Appearance), TheDock.LooksItems(said, spot, dark, [], LooksScope.Appearance));
                var flyoutsTab = page.Rows.Single(row => row.Kind == PanelRowKind.Tabs).Rows[2].Key;
                var preview = page.Rows.Single(row => row.Kind == PanelRowKind.Preview).Key;
                EditedDisplay(gfx, instance, dpi, new Config(), windows, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"attached2-appearance-{theme}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                {
                    deck.Wallpaper = () => source;
                    deck.Memory = new PanelMemory();
                    deck.Memory.Show(PanelPage.Appearance);
                    deck.Memory.Open([flyoutsTab]);
                    deck.Show(gfx, windows, scale, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]));
                    deck.Land(PanelPage.Appearance, preview, light: false);
                    // The preview's folder stands attached, on a ground of the colour said.
                    var ground = Assert.Single(deck.Previews!.Grounds).Value;
                    Assert.False(ground.IsEmpty);
                });
            }
        });
    }

    /// <summary>The clock preset's folder at the end of a Taskbar along <paramref name="edge"/>, attached, in two columns, a divider after its first line.</summary>
    private static Picture DividedClock(Edge edge)
    {
        var layout = StyledClock(edge, FolderStyle.Attached);
        var folder = layout.Islands.Single(island => island.Folder is { Style: FolderStyle.Attached }).Folder!;
        var divider = ListKind.Divider.New();
        divider.Id = "divider";
        folder.Lists[0].Insert(1, new FolderCell { Island = divider });
        return layout;
    }
}
