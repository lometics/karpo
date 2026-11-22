// edit, §529: a widget's and a folder's page of the settings panel as Karpo draws it, the scenes of the person's pick
// (docs\widgets.md §529: the flow study's concept 3 header, concept 2 body), on the display EditPlacePictures.EditedDisplay
// stands up (1920 × 1080 DIP at 150 %, edit mode's dim, Karpo's taskbar), the panel the real one (EditDeck) folded out with
// the pages the dock builds. To KARPO_PNG_DIR as panel-529-<scene>-<dark|light>.png:
//
//  - calendar: a calendar in a folder: the header, What it shows (an agenda, Days ahead under Style, the accounts' line
//    under Show from), §645 Size (Height, Max rows under it), each a section; §567 no Look;
//  - calendar-fill: §660 the calendar Fill high: Size its Height alone, no rows under it;
//  - clock: a clock, its seconds on;
//  - weather: the current weather, §630 Open-Meteo's credit under its Place;
//  - kinds: a clock, the weather and a pin: their header ("3 widgets"), what they have in common;
//  - pins: three pins in a folder, two compact and one full: their page in common, each mixed row how they split;
//  - winsearch: §655 Windows search: Search in (the way to Searching Windows its last row), When nothing is typed, How
//    many each shows;
//  - folder: a folder of a clock face and an icon face: §552 each face a section of its own, its Remove at its head's end,
//    Add a face under them, Alignment a dropdown.
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
    private static readonly string[] WidgetPageScenes = ["calendar", "calendar-fill", "clock", "weather", "kinds", "pins", "folder", "winsearch"];

    [UiFact]
    public void WidgetPageSheets()
    {
        WindowShots.OnHiddenDesktop("widget-page", (gfx, instance) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                foreach (var scene in WidgetPageScenes)
                {
                    var layout = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
                    var file = $"panel-529-{scene}-{(dark ? "dark" : "light")}.png";
                    EditedDisplay(gfx, instance, dpi, new Config(), look, layout, file, dips: new Vector2(1920f, 1080f), panel: deck =>
                    {
                        var room = deck.State is { } state ? state : PictureState();
                        var (content, page, open) = WidgetPageScene(scene, dark);
                        deck.Memory = new PanelMemory();
                        deck.Memory.Open(open);
                        deck.Show(gfx, look, dpi / 96f, deck.Room!, room, content, page);
                        Assert.True(deck.FlyoutOpen);
                        Assert.Equal(page, deck.Page);
                        Assert.False(deck.Layout!.HeaderCard.IsEmpty);
                    });
                }
            }
        });
    }

    /// <summary>A scene of §529: the panel's pages with the scene's widget's or folder's, the page shown, the keys opened there.</summary>
    internal static (SettingsContent Content, PanelPage Page, IReadOnlyList<string> Open) WidgetPageScene(string scene, bool dark)
    {
        var config = MenuCorpus.Fresh();
        var place = new OptionPlace(config, Dark: dark);
        var basis = SettingsPanelTests.Content(dark: dark);
        List<PanelPageView> pages = [.. basis.Pages];
        PanelPageView One(WidgetItem item, bool inFolder) =>
            PanelGroups.Page(PanelPage.Widget, IslandEdits.Kind(item), item.Info?.Glyph ?? "", MenuCorpus.Arranged(item, place),
                header: PanelRows.HeaderOf([(item.Info?.Glyph ?? "", IslandEdits.Kind(item))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder)));
        PanelPageView Several(IReadOnlyList<WidgetItem> items)
        {
            List<OptionTarget> targets = [.. items.Select(item => new OptionTarget(item, MenuCorpus.Sources, place))];
            var kinds = items.Select(IslandEdits.Kind).Distinct().ToList();
            var title = kinds.Count == 1 ? S.DockEditSelectedKind(count: items.Count, kind: kinds[0]) : S.WidgetFolderWidgets(items.Count);
            var header = PanelRows.HeaderOf([.. items.Select(item => (item.Info?.Glyph ?? "", IslandEdits.Kind(item)))], PanelRows.PlaceOf(LayoutType.Taskbar, inFolder: true));
            return PanelGroups.Page(PanelPage.Widget, title, PanelRows.GlyphOf(PanelPage.Widget),
                [.. TheDock.WidgetButtons(), .. OptionMenu.Settings(targets, [], FolderSide.Face, LineHeight.Most)], title, header);
        }
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [ClockFace.New(), FolderFace.Of(FolderFaces.IconId)];
        switch (scene)
        {
            case "calendar":
            {
                var calendar = ListKind.Calendar.New();
                pages.Add(SettingsPanelTests.FolderPage(folder, place));
                pages.Add(One(calendar, inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget, []);
            }
            case "calendar-fill":
            {
                // §660: Fill ignores the rows, so none are shown under its Height.
                var calendar = ListKind.Calendar.New();
                calendar.Height = LineHeight.Automatic(12).As(LineHeightMode.Fill);
                pages.Add(One(calendar, inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget, []);
            }
            case "clock":
            {
                var clock = ListKind.Clock.New();
                (clock.Clock ??= new ClockOptions()).Seconds = true;
                pages.Add(One(clock, inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget, []);
            }
            case "weather":
                pages.Add(One(ListKind.WeatherNow.New(), inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget, []);
            case "winsearch":
                // §655: Search in with the way to Searching Windows, When nothing is typed, Items per section.
                pages.Add(One(ListKind.WinSearch.New(), inFolder: true));
                return (new SettingsContent(pages), PanelPage.Widget, []);
            case "kinds":
            {
                var clock = ListKind.Clock.New();
                var pin = ListKind.Pin.New();
                pin.Pin!.Target = @"C:\Users\Ada\Documents";
                var page = Several([clock, ListKind.WeatherNow.New(), pin]);
                var mixed = PanelRows.All(page.Rows).FirstOrDefault(row => row.Theirs.Count > 0);
                pages.Add(page);
                return (new SettingsContent(pages), PanelPage.Widget, mixed is null ? [] : [PanelGroups.EachOf(mixed.Key)]);
            }
            case "pins":
            {
                WidgetItem Pin(string target, LineSize size)
                {
                    var pin = ListKind.Pin.New();
                    pin.Pin!.Target = target;
                    _ = LineHeightRows.Resize(pin, size);
                    return pin;
                }
                pages.Add(Several([Pin(@"C:\Users\Ada\Documents", LineSize.Compact), Pin(@"C:\Users\Ada\Downloads", LineSize.Compact), Pin("https://example.com", LineSize.Full)]));
                return (new SettingsContent(pages), PanelPage.Widget, []);
            }
            default:
                pages.Add(SettingsPanelTests.FolderPage(folder, place));
                return (new SettingsContent(pages), PanelPage.Folder, []);
        }
    }
}
