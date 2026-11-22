// edit, §645: a widget's and a folder's page in plain sections, as the other settings pages are (docs\widgets.md §645; the
// person, 2026-11-09: "the widget settings should have normal sections like the other settings pages not just the expanding
// sections. for all widgets"), as Karpo draws them on the display EditPlacePictures.EditedDisplay stands up (1920 × 1080 DIP
// at 150 %, edit mode's dim, Karpo's taskbar), the flyout the real one (EditDeck). To KARPO_PNG_DIR as
// panel-645-<scene>-<dark|light>.png:
//
//  - clock: a clock, its seconds on: What it shows a section, each of its rows a card;
//  - calendar: a calendar in a folder: What it shows (Show from an expander, a list of sources), Height a section;
//  - weather: the current weather: What it shows, Size;
//  - tray: the tray icons: every icon a card under its section's heading;
//  - folder: a folder of a clock face and an icon face: Faces (each face an expander of its settings), Folder.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private static readonly string[] WidgetSectionsScenes = ["clock", "calendar", "weather", "tray", "folder"];

    [UiFact]
    public void WidgetSectionsSheets()
    {
        WindowShots.OnHiddenDesktop("widget-sections", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var scene in WidgetSectionsScenes)
                {
                    var file = $"panel-645-{scene}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, PictureLayout(LayoutType.Taskbar, Edge.Bottom), file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var (content, page) = WidgetSectionsScene(scene, dark);
                        deck.Memory = new PanelMemory();
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content, page);
                        Assert.True(deck.FlyoutOpen);
                        Assert.Equal(page, deck.Page);
                        Assert.False(deck.Layout!.HeaderCard.IsEmpty);
                    });
                }
            }
        });
    }

    /// <summary>A scene of §645: the panel's pages with the scene's widget's or folder's, the page shown.</summary>
    private static (SettingsContent Content, PanelPage Page) WidgetSectionsScene(string scene, bool dark)
    {
        var place = new OptionPlace(MenuCorpus.Fresh(), Dark: dark);
        PanelPageView One(WidgetItem item, bool inFolder) =>
            PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(item), item.Info?.Glyph ?? "", MenuCorpus.Arranged(item, place),
                header: PanelRows.HeaderOf([(item.Info?.Glyph ?? "", IslandEdits.Kind(item))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder)));
        List<PanelPageView> pages = [.. SettingsPanelTests.Content(dark: dark).Pages];
        switch (scene)
        {
            case "clock":
            {
                var clock = ListKind.Clock.New();
                (clock.Clock ??= new ClockOptions()).Seconds = true;
                pages.Add(One(clock, inFolder: false));
                return (new SettingsContent(pages), PanelPage.Widget);
            }
            case "calendar":
                pages.Add(One(ListKind.Calendar.New(), inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget);
            case "weather":
                pages.Add(One(ListKind.WeatherNow.New(), inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget);
            case "tray":
                pages.Add(One(BarKind.Tray.New(), inFolder: false));
                return (new SettingsContent(pages), PanelPage.Widget);
            default:
            {
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [ClockFace.New(), FolderFace.Of(FolderFaces.IconId)];
                pages.Add(SettingsPanelTests.FolderPage(folder, place));
                return (new SettingsContent(pages), PanelPage.Folder);
            }
        }
    }
}
