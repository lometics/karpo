// §593 (the person, 2026-11-09: "i want the add menu to be shown on the top in the same width as the edit bar all the time, now
// when i hover over a group or widget to add it should show the whole group and widget on the taskbar as a ghost preview. when i
// click it should place it there and it should also open the folder"; "same behaviour should be implemented for the widgets
// placed inside the folder"): a display 1920 × 1080 DIP at 150 % in edit mode, Windows' taskbar along its bottom, the edit bar at
// its top (EditPlacePictures.EditedDisplay), as the dock shows them. To KARPO_PNG_DIR as addghost-<scene>-<dark|light>.png:
//
//  - page: the add page at the edit bar's top, as wide as it, no beak; where it adds — the bar's last "+", the end section's
//    start — §617 the empty ghost, one cell, the end section making room for it;
//  - widget: Keep awake under the pointer on the page, its ghost there on the bar: the widget laid out where it goes, faint,
//    the carried block's ghost round it, the end section making room;
//  - group: the world clock under the pointer, the whole of its folder a ghost there, §606 open over its face with its lines,
//    all of it faint, §621 the ghost's outline round each of its lines;
//  - apps: §621 the open apps under the pointer, three buttons: the ghost's outline round each, the gaps between them clear;
//  - air: §617 the air quality's ready-made folder the same, its four lines in it;
//  - folder-empty: §617 the add page of the world clock's column, nothing under the pointer: the empty ghost, one line;
//  - folder: the clock folder open arranged, the add page of its list widgets, a clock under the pointer, its ghost the
//    folder's line where it goes in;
//  - placed: the world clock clicked: on the bar where its ghost stood, its folder open, its face selected.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void AddGhostSheets()
    {
        WindowShots.OnHiddenDesktop("add-ghost", (gfx, instance) =>
        {
            const uint dpi = 144;
            var dips = new Vector2(1920f, 1080f);
            var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
            var awake = Catalogue.On(WidgetType.Bar).Single(entry => entry.Bar == BarKind.KeepAwake);
            var clock = Catalogue.On(WidgetType.List).Single(entry => entry.List == ListKind.Clock);
            var world = Presets.Find(Presets.WorldClockId)!;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var mode = dark ? "dark" : "light";

                // The add page at the top, its place the bar's last "+" (Dock.AddAt): §617 the empty ghost there, one cell, the
                // bar laid out round it (AddList.Empty).
                var room = BarKind.TaskView.New();
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(room), $"addghost-page-{mode}.png", dips: dips,
                    panel: deck => AddPage(gfx, dpi, deck, look, WidgetType.Bar, null),
                    marks: layout => Ghosted(layout, room) is { } view ? view with { GhostEmpty = true } : null);

                // A widget under the pointer: its ghost on the bar where it goes.
                var widget = BarKind.KeepAwake.New();
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(widget), $"addghost-widget-{mode}.png", dips: dips,
                    panel: deck => AddPage(gfx, dpi, deck, look, WidgetType.Bar, awake.Title),
                    marks: layout => Ghosted(layout, widget));

                // A group under the pointer: the whole of its folder a ghost there, §606 open over its face as its click opens it,
                // all of it faint in the ghost's outline.
                var group = Presets.Folder(world, machine);
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(group), $"addghost-group-{mode}.png", dips: dips,
                    panel: deck => AddPage(gfx, dpi, deck, look, WidgetType.Bar, world.Title),
                    marks: layout => Ghosted(layout, group), folder: true, folderFace: group.Folder!.Face.Kind, folderGhost: true,
                    inFolder: (open, deck) =>
                    {
                        Assert.True(open.Ghosted);
                        // §677: none of its "+" and width buttons.
                        Assert.False(open.AddButtonsShown);
                        // §625: whole, as it will open.
                        Assert.Equal(1f, open.Shows);
                        Assert.NotEmpty(open.LineKeys);
                        // §621: an outline round each line, at its card, never one round the folder.
                        Assert.Equal(EachLine(open), open.AddGhostsShown);
                        SeeGhost(deck!, open);
                    });

                // §621: a pick of several cards — the open apps, three buttons — under the pointer: an outline round each button,
                // the gaps between them clear, not one round them all.
                var apps = BarKind.Apps.New();
                var appsEntry = Catalogue.On(WidgetType.Bar).Single(entry => entry.Bar == BarKind.Apps);
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(apps, alone: true), $"addghost-apps-{mode}.png", dips: dips,
                    panel: deck => AddPage(gfx, dpi, deck, look, WidgetType.Bar, appsEntry.Title),
                    marks: layout => Ghosted(layout, apps) is { Ghost: int part } view && layout.GhostRects(part) is { Count: 3 } rects
                        && rects.SequenceEqual(layout.Plates(part)) ? view : throw new InvalidOperationException("the open apps' ghost is not three buttons"));

                // §617: the air quality under the pointer: its face's ghost and its folder open over it, its four lines in it.
                var air = Presets.Find(Presets.AirQualityId)!;
                var airs = Presets.Folder(air, machine);
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(airs), $"addghost-air-{mode}.png", dips: dips,
                    panel: deck => AddPage(gfx, dpi, deck, look, WidgetType.Bar, air.Title),
                    marks: layout => Ghosted(layout, airs), folder: true, folderFace: airs.Folder!.Face.Kind, folderGhost: true,
                    inFolder: (open, deck) =>
                    {
                        Assert.True(open.Ghosted);
                        // §677: none of its "+" and width buttons.
                        Assert.False(open.AddButtonsShown);
                        Assert.Equal(air.Items.Count, open.LineKeys.Count);
                        Assert.Equal(EachLine(open), open.AddGhostsShown);
                        SeeGhost(deck!, open);
                    });

                // §617: the add page open on the world clock's column "+", nothing under the pointer: the empty ghost, one line
                // where the page adds.
                var emptyHolder = Presets.Folder(world, machine);
                var emptyInside = With(emptyHolder);
                var taskView = Catalogue.On(WidgetType.List).Single(entry => entry.Id == WidgetInfos.TaskView.Id);
                var emptyPath = AddList.Add(emptyInside.Islands, AddSpot.InFolder("pictures", emptyHolder.Id, new FolderPlace(0, 0)), taskView)!;
                EditedDisplay(gfx, instance, dpi, new Config(), look, emptyInside, $"addghost-folder-empty-{mode}.png", dips: dips, folder: true, folderFace: emptyHolder.Folder!.Face.Kind,
                    inFolder: (open, deck) =>
                    {
                        var key = open.Keys.Single(each => each.EndsWith(emptyPath[emptyPath.IndexOf('/')..], StringComparison.Ordinal));
                        open.GhostEmpty = true;
                        open.Ghost = key;
                        // §677: adding into it, none of its "+" and width buttons.
                        Assert.False(open.AddButtonsShown);
                        AddPage(gfx, dpi, deck!, look, WidgetType.List, null, PictureState(folder: S.CoreWidgetFolderListTitle), open.ContentOnScreen);
                        SeeGhost(deck!, open);
                    });

                // A list widget under the pointer, the world clock open: its ghost the folder's line where the page adds — its first
                // list's end away from the bar (FolderFlyout.AddPlace), the top over a bar at the foot.
                var holder = Presets.Folder(world, machine);
                var inside = With(holder);
                var path = AddList.Add(inside.Islands, AddSpot.InFolder("pictures", holder.Id, new FolderPlace(0, 0)), clock)!;
                EditedDisplay(gfx, instance, dpi, new Config(), look, inside, $"addghost-folder-{mode}.png", dips: dips, folder: true, folderFace: holder.Folder!.Face.Kind,
                    inFolder: (open, deck) =>
                    {
                        var key = open.Keys.Single(each => each.EndsWith(path[path.IndexOf('/')..], StringComparison.Ordinal));
                        open.Ghost = key;
                        Assert.Equal(key, open.Ghost);
                        Assert.False(open.AddButtonsShown);
                        // §621: one line, one outline, at its card.
                        Assert.Contains(Assert.Single(open.AddGhostsShown), EachLine(open));
                        AddPage(gfx, dpi, deck!, look, WidgetType.List, clock.Title, PictureState(folder: S.CoreWidgetFolderListTitle), open.ContentOnScreen);
                        SeeGhost(deck!, open);
                    });

                // The group clicked: on the bar where its ghost stood, its folder open, its face selected.
                var placed = Presets.Folder(world, machine);
                EditedDisplay(gfx, instance, dpi, new Config(), look, With(placed), $"addghost-placed-{mode}.png", dips: dips, folder: true, folderFace: placed.Folder!.Face.Kind,
                    inFolder: (open, _) => Assert.Equal((false, 1f, true), (open.Ghosted, open.Shows, open.AddButtonsShown)));
            }
        });
    }

    /// <summary>The add page of <paramref name="type"/>'s widgets at the top of the bar, as wide, <paramref name="hovered"/>'s tile under the pointer.</summary>
    private static void AddPage(Gfx gfx, uint dpi, EditDeck deck, Look look, WidgetType type, string? hovered, EditDeckState? state = null, RECT? clear = null)
    {
        var (rows, _, _) = AddList.Rows(type, turns: false, 1000);
        var at = PanelPage.Add.ToString();
        var page = new PanelPageView(PanelPage.Add, S.DockEditAdd, PanelRows.GlyphOf(PanelPage.Add), PanelRows.Picks(rows, at), []) { Filter = typed => PanelRows.Picks(rows, at, typed) };
        var content = SettingsPanelTests.Content(dark: look.Dark);
        deck.Memory = new PanelMemory();
        deck.Show(gfx, look, dpi / 96f, deck.Room!, state ?? deck.State ?? PictureState(), content with { Pages = [.. content.Pages, page] }, PanelPage.Add, clear);
        HungFromTheBar(deck);
        if (hovered is null)
        {
            return;
        }
        // Its folder of the add tree opened, it in sight (EditDeck.Land).
        var row = PanelRows.All(page.Rows).First(each => each is { Kind: PanelRowKind.Pick, Greyed: false } && each.Text == hovered);
        deck.Land(PanelPage.Add, row.Key, light: false);
        var tile = deck.Layout!.Blocks.SelectMany(block => block.Lines).First(each => each.Row.Key == row.Key);
        _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, EditDeckClickTests.At(deck, deck.Layout.OnPanel(tile.Rect).Center, flyout: true));
        Assert.Equal(tile.Row.Item.Id, deck.Proposed);
    }

    /// <summary>§629: the add page of <paramref name="deck"/> shows the ghost in <paramref name="open"/> through itself, as the dock has it (Dock.SeeGhosts).</summary>
    private static void SeeGhost(EditDeck deck, FolderFlyout open) => deck.SeeThrough(open.GhostSight is { } sight ? [sight] : []);

    // The layout of the pictures with <paramref name="item"/> where the add page adds: before the end section's first widget.
    // <paramref name="alone"/>: the only one of its kind, the bar's own taken off (the open apps: the picture's apps all its).
    private static Picture With(BarItem item, bool alone = false)
    {
        var layout = PictureLayout(LayoutType.Taskbar, Edge.Bottom);
        if (alone)
        {
            layout.Islands.RemoveAll(each => each.Kind == item.Kind);
        }
        item.Section = BarSection.End;
        var at = layout.Islands.FindIndex(each => Sections.Of(each) == BarSection.End);
        layout.Islands.Insert(at < 0 ? layout.Islands.Count : at, item);
        return layout;
    }

    // §621: every line's card of the folder <paramref name="open"/> as it is planned (panel px): what the ghost outlines, each.
    private static List<RectF> EachLine(FolderFlyout open) =>
        [.. open.Plan!.Cards.Select(card => new RectF(card.left, card.top, card.right - card.left, card.bottom - card.top))];

    // The ghost of <paramref name="item"/> on the bar laid out with it, as the dock marks it (DockWindow.EditView).
    private static EditView? Ghosted(Layout layout, BarItem item) =>
        layout.PartIndex(Config.IslandKey(null, item.Id)) is var part and >= 0 ? new EditView(Ghost: part) : null;
}
