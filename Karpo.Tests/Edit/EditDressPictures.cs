// §612: edit mode's dress and the Appearance's preview, as Karpo draws them: a display 1920 × 1080 DIP at 150 %, Karpo's
// taskbar along its bottom, the edit bar at its top (EditPlacePictures.EditedDisplay). To KARPO_PNG_DIR as
// dress-<scene>-<dark|light>.png:
//
//  - edit-taskbar: edit mode in its dress, the Taskbar page's flyout open: the dim, every block outlined, the "+";
//  - preview-appearance: the Appearance page's flyout open: the bar as out of edit mode, no outline, no "+", no dim — the
//    edit bar and the flyout still there.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void DressSheets()
    {
        WindowShots.OnHiddenDesktop("edit-dress", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var mode = dark ? "dark" : "light";
                var layout = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
                foreach (var (name, page, preview) in new[] { ("edit-taskbar", PanelPage.Taskbar, false), ("preview-appearance", PanelPage.Appearance, true) })
                {
                    EditedDisplay(gfx, instance, dpi, new Config(), look, layout, $"dress-{name}-{mode}.png", dips: new Vector2(1920f, 1080f), editing: !preview, preview: preview, panel: deck =>
                    {
                        deck.Memory = new PanelMemory();
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), SettingsPanelTests.Content(dark: dark), page);
                        Assert.Equal(page, deck.Page);
                        if (preview)
                        {
                            deck.Land(page, AppearancePreview, light: false);
                        }
                    });
                }
            }
        });
    }
}
