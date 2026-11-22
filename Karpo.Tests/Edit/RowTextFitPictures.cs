// edit, §604: a row as tall as its words — Karpo Pro's "What counts as a widget", its name over its four lines, and "Free
// version" over its note wrapped in two —, as Karpo draws the Karpo page on the display EditPlacePictures.EditedDisplay stands
// up (1920 × 1080 DIP, edit mode's dim, Karpo's taskbar), the flyout the real one (EditDeck), Karpo Pro landed on and so open.
// To KARPO_PNG_DIR as panel-604-karpo-pro-<100|150>-<dark|light>.png. What the rows' heights are is RowTextFitTests'.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void RowTextFitSheets()
    {
        WindowShots.OnHiddenDesktop("row-text-fit", (gfx, instance) =>
        {
            foreach (var dpi in new uint[] { 96, 144 })
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var file = $"panel-604-karpo-pro-{dpi * 100 / 96}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var content = SettingsPanelTests.Content(dark: dark);
                        var counts = PanelRows.All(content.Of(PanelPage.Karpo)!.Rows).Single(row => row.Text == S.SettingsLicenseCountsHeader).Key;
                        deck.Memory = new PanelMemory();
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content, PanelPage.Karpo);
                        Assert.True(deck.FlyoutOpen);
                        deck.Land(PanelPage.Karpo, counts, light: false);
                    });
                }
            }
        });
    }
}
