// edit, §589: what reads along a side bar and the spacing a number, on the settings panel as Karpo draws it — a display
// 1920 × 1080 DIP at 150 % in edit mode (EditPlacePictures.EditedDisplay). To KARPO_PNG_DIR:
//
//  - align-side-<dark|light>.png: the taskbar along the display's left, a folder's page open: its Alignment Automatic, Top,
//    Center and Bottom, Word's align glyphs a quarter turn;
//  - spacing-number-<dark|light>.png: the Taskbar page, Spacing after Size a number box, − 6 px +.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void SideAlignmentAndSpacingSheets()
    {
        WindowShots.OnHiddenDesktop("side-pictures", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var mode = dark ? "dark" : "light";

                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Left), $"align-side-{mode}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                {
                    var place = new OptionPlace(MenuCorpus.Fresh(), Dark: dark) { Side = true };
                    var page = SettingsPanelTests.FolderPage(BarKind.FolderList.New(), place);
                    deck.Memory = new PanelMemory();
                    deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), new SettingsContent([page]), PanelPage.Folder);
                    var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).First(each => each.Row.Text == S.CoreAlignTitle);
                    deck.Land(PanelPage.Folder, line.Row.Key, light: false);
                    var segments = Assert.IsType<FlyoutControl.Segments>(line.Row.Item.Control).Buttons;
                    Assert.Equal([S.DockFolderAlignAuto, S.CoreEdgeTop, S.CoreAlignCenter, S.CoreEdgeBottom], segments.Select(button => button.Name));
                    Assert.Equal([OptionGlyphs.Automatic, OptionGlyphs.AlignTop, OptionGlyphs.AlignMiddle, OptionGlyphs.AlignBottom], segments.Select(button => button.Glyph));
                },
                // Its dropdown open: the four, each its icon (Dock.PanelPopup's list of a row of segments).
                panelPopup: deck =>
                {
                    var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).First(each => each.Row.Text == S.CoreAlignTitle);
                    var box = deck.OnScreen(deck.Layout.Controls(line).Single(each => each.Hit.Kind == PanelHitKind.Pop).Hit);
                    var menu = new Flyout(gfx, instance, 0);
                    var anchor = new POINT { x = (box.left + box.right) / 2, y = box.bottom };
                    var screen = new RECT { left = 0, top = 0, right = (int)(1920 * 1.5f), bottom = (int)(1080 * 1.5f) };
                    var choices = PanelRows.ChoicesOf((FlyoutControl.Segments)line.Row.Item.Control!);
                    menu.Show(gfx, look, dpi / 96f, choices, FlyoutLayout.List, anchor, screen, Edge.Top, control: box, within: deck.FlyoutOnScreen);
                    Assert.True(menu.IsOpen);
                    return menu;
                });

                EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), $"spacing-number-{mode}.png", dips: new Vector2(1920f, 1080f), panel: deck =>
                {
                    deck.Memory = new PanelMemory();
                    deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), SettingsPanelTests.Content(dark: dark), PanelPage.Taskbar);
                    var line = deck.Layout!.Blocks.SelectMany(block => block.Lines).First(each => each.Row.Text == S.SettingsLayoutsSpacingHeader);
                    Assert.Equal(PanelRowKind.Stepper, line.Row.Kind);
                });
            }
        });
    }
}
