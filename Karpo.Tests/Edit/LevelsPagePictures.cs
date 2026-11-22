// edit, §594: the settings' pages with their levels back and no third level (docs\widgets.md §594; the person, 2026-11-09,
// of §588: "i basically just did not like the third level. but not that i did not like any level"), as Karpo draws them on
// the display EditPlacePictures.EditedDisplay stands up (1920 × 1080 DIP at 150 %, edit mode's dim, Karpo's taskbar), the
// flyout the real one (EditDeck). To KARPO_PNG_DIR as panel-594-<scene>-<dark|light>.png:
//
//  - karpo: the Karpo page, its levels expanders again, Online landed on and so open;
//  - holidays: Public holidays landed on and open, Switzerland with its region Zürich (a row of the card, no well) and France;
//  - tray: the tray icons' page, every icon's place in sight, nothing to expand;
//  - folder: a folder of a clock face and an icon face, its Alignment straight under the heading Folder;
//  - calendar: a calendar line, its Show from an expander in What it shows again, landed on and so open.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private static readonly string[] LevelsPageScenes = ["karpo", "holidays", "tray", "folder", "calendar"];

    [UiFact]
    public void LevelsPageSheets()
    {
        WindowShots.OnHiddenDesktop("levels-page", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var scene in LevelsPageScenes)
                {
                    var file = $"panel-594-{scene}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var (content, page, land) = LevelsPageScene(scene, dark);
                        deck.Memory = new PanelMemory();
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content, page);
                        Assert.True(deck.FlyoutOpen);
                        Assert.Equal(page, deck.Page);
                        if (land is not null)
                        {
                            // §594: a level landed on opens.
                            deck.Land(page, land, light: false);
                            Assert.Contains(land, deck.Memory.Opened);
                        }
                    });
                }
            }
        });
    }

    /// <summary>A scene of §594: the panel's pages, the page shown, the level landed on and so opened (none for the page's top).</summary>
    private static (SettingsContent Content, PanelPage Page, string? Land) LevelsPageScene(string scene, bool dark)
    {
        static void Holidays(Config config) => config.Holidays = new HolidayOptions
        {
            Calendars = [new HolidayCountry { Country = "CH", Region = "CH-ZH" }, new HolidayCountry { Country = "FR", Colour = "#0078D4" }],
        };
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: dark);
        PanelPageView One(WidgetItem item, bool inFolder) =>
            PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(item), item.Info?.Glyph ?? "", MenuCorpus.Arranged(item, place),
                header: PanelRows.HeaderOf([(item.Info?.Glyph ?? "", IslandEdits.Kind(item))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder)));
        List<PanelPageView> pages = [.. SettingsPanelTests.Content(dark: dark, edit: scene == "holidays" ? Holidays : null).Pages];
        var services = "Karpo/" + S.SettingsSectionsServices + "/";
        switch (scene)
        {
            case "karpo":
                return (new SettingsContent(pages), PanelPage.Karpo, services + S.SettingsOnlineSection);
            case "holidays":
                return (new SettingsContent(pages), PanelPage.Karpo, services + S.CoreHolidaysName);
            case "tray":
                pages.Add(One(BarKind.Tray.New(), inFolder: false));
                return (new SettingsContent(pages), PanelPage.Widget, null);
            case "folder":
            {
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [ClockFace.New(), FolderFace.Of(FolderFaces.IconId)];
                pages.Add(SettingsPanelTests.FolderPage(folder, place));
                return (new SettingsContent(pages), PanelPage.Folder, null);
            }
            default:
            {
                var calendar = One(ListKind.Calendar.New(), inFolder: true);
                pages.Add(calendar);
                return (new SettingsContent(pages), PanelPage.Widget, PanelRows.All(calendar.Rows).Single(row => row.Text == S.DockMenuShowFromTitle).Key);
            }
        }
    }
}
