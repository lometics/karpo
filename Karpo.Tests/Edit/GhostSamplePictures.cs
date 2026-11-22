// §635 (the person, 2026-11-09: "for the ghost preview they should take a place that certainly has the data to show
// available"): the add page's ghosts of the widgets of a place's data, made as the dock makes its ghost (Samples.Ghost), full
// of the samples: a display 1920 × 1080 DIP at 150 % in edit mode, as AddGhostSheets draws it. To KARPO_PNG_DIR as
// ghostdata-<pick>-dark.png:
//
//  - tides: the tides' ready-made group, Brest's sea on its face and in its lines, not "No tide data here";
//  - air: the air quality's, Zürich's index, pollutants and pollen;
//  - sports: the sports', a match on (Barcelona 2–1 Real Madrid, 67') and its fixtures, not "No team yet";
//  - weather: the weather's, Zürich's conditions, hours and days;
//  - markets: the Markets group (§648), the samples' four shares under the S&P 500's ticker.
//
// §643 (the person, 2026-11-09: "in preview it should just choose one best effort or even make one up so that the user can
// see how the widget looks configured"), as ghostset-<pick>-dark.png:
//
//  - drive: the drive's group, this PC's system drive and its root's contents, not "No drive chosen";
//  - networkdrive: the network drive's, the made-up share \\nas\media, its space and what it holds;
//  - notebook: the notes and to-do group, the notebook's own, else the samples' tasks and note;
//  - lines: a folder of a pin (File Explorer), a camera (the made-up lake) and a folder's contents (Documents);
//  - pin: a pin on the bar, File Explorer.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void GhostSampleSheets()
    {
        WindowShots.OnHiddenDesktop("ghost-samples", (gfx, instance) =>
        {
            const uint dpi = 144;
            var dips = new Vector2(1920f, 1080f);
            var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            // §635 the ready-made groups of a place's data; §643 the ghosts of widgets not set up yet, set up as the dock shows
            // them (AddGhost.Shown, Samples.Dress): the drive's and the network drive's groups, the notebook's, a folder of a pin,
            // a camera and a folder's contents, and a pin on the bar.
            List<(string File, BarItem Group, string Hovered)> sheets = [];
            foreach (var (name, id) in new[] { ("tides", Presets.TidesId), ("air", Presets.AirQualityId), ("sports", Presets.SportsId), ("weather", Presets.WeatherId), ("markets", Presets.MarketId) })
            {
                var preset = Presets.Find(id)!;
                sheets.Add(($"ghostdata-{name}-dark.png", Presets.Folder(preset, machine), preset.Title));
            }
            foreach (var (name, id) in new[] { ("drive", "driveGroup"), ("networkdrive", "networkDriveGroup"), ("notebook", "notebookGroup") })
            {
                var preset = Presets.Find(id)!;
                sheets.Add(($"ghostset-{name}-dark.png", Presets.Folder(preset, machine), preset.Title));
            }
            var lines = BarKind.FolderList.New();
            foreach (var kind in new[] { ListKind.Pin, ListKind.Camera, ListKind.FolderContents })
            {
                Assert.NotNull(Karpo.Core.Folders.Add(BarKind.FolderList, lines.Folder!, kind.New()));
            }
            sheets.Add(("ghostset-lines-dark.png", lines, WidgetInfos.FolderList.Title));
            sheets.Add(("ghostset-pin-dark.png", BarKind.Pin.New(), WidgetInfos.Pin.Title));
            var was = Samples.Ghost;
            try
            {
                foreach (var (file, group, hovered) in sheets)
                {
                    Samples.Dress(group);
                    var layout = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
                    group.Section = BarSection.End;
                    var at = layout.Islands.FindIndex(each => Sections.Of(each) == BarSection.End);
                    layout.Islands.Insert(at < 0 ? layout.Islands.Count : at, group);
                    // The dock's ghost: its widget and its lines made showing the samples (Dock.SyncIslands).
                    Samples.Ghost = Config.IslandKey(null, group.Id);
                    var folder = group.Folder is not null;
                    EditedDisplay(gfx, instance, dpi, new Config(), look, layout, file, dips: dips,
                        panel: deck => GhostPage(deck, hovered),
                        marks: laid => laid.PartIndex(Config.IslandKey(null, group.Id)) is var part and >= 0 ? new EditView(Ghost: part) : null,
                        folder: folder, folderFace: group.Folder?.Face.Kind, folderGhost: folder,
                        inFolder: folder ? (open, _) =>
                        {
                            Assert.True(open.Ghosted);
                            Assert.Equal(group.Folder!.Cells.Count, open.LineKeys.Count);
                        } : null);
                }
            }
            finally
            {
                Samples.Ghost = was;
            }

            // The add page of the bar's widgets at the top of the bar, <paramref name="hovered"/>'s tile under the pointer.
            void GhostPage(EditDeck deck, string hovered)
            {
                var (rows, _, _) = AddList.Rows(WidgetType.Bar, turns: false, 1000);
                var on = PanelPage.Add.ToString();
                var page = new PanelPageView(PanelPage.Add, S.DockEditAdd, PanelRows.GlyphOf(PanelPage.Add), PanelRows.Picks(rows, on), []) { Filter = typed => PanelRows.Picks(rows, on, typed) };
                var content = SettingsPanelTests.Content(dark: look.Dark);
                deck.Memory = new PanelMemory();
                deck.Show(gfx, look, dpi / 96f, deck.Room!, deck.State ?? PictureState(), content with { Pages = [.. content.Pages, page] }, PanelPage.Add, null);
                var picks = PanelRows.All(page.Rows).Where(each => each is { Kind: PanelRowKind.Pick, Greyed: false }).ToList();
                var row = picks.FirstOrDefault(each => each.Text == hovered) ?? picks[0];
                deck.Land(PanelPage.Add, row.Key, light: false);
                var tile = deck.Layout!.Blocks.SelectMany(block => block.Lines).First(each => each.Row.Key == row.Key);
                _ = deck.Message(deck.FlyoutHwnd, TerraFX.Interop.Windows.WM.WM_MOUSEMOVE, 0, EditDeckClickTests.At(deck, deck.Layout.OnPanel(tile.Rect).Center, flyout: true));
            }
        });
    }
}
