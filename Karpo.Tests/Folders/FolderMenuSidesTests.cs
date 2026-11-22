// §481: a folder in edit mode has two menus, as the person asked (2026-11-02: "when in edit mode and i left click a
// folder ... it should not open the folder, it should open the configuration menu like it does for the other taskbar
// widgets and then it should show only the faces and face appearance settings ... and an additional open button";
// "then when i open the folder it should show the folder options with the menu on top like it does already"):
//
//  - its face's, its right click's (beside it, as every widget's; §527 a click opens the folder, no "Open"): its faces, each
//    face's own rows, "Add a face" — what is drawn on the bar;
//  - its panel's, Folder ▾ once it stands open: where it stands as it opens — what changes only the folder opened.
//
// Every setting of a folder is in one of them alone, but its look (§507): Look › is the foot of both, the same values — its
// face's colours and font, and its open flyout's outline, set while the folder stands open.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Folders;

public sealed class FolderMenuSidesTests
{
    private static readonly Config Fresh = new();

    private static OptionPlace Place => new(Fresh, Dark: false);

    /// <summary>A folder's menu of <paramref name="side"/> as the dock builds it (Dock.WidgetMenu), its picks into <paramref name="picks"/>.</summary>
    private static List<FlyoutItem> Menu(BarItem folder, FolderSide side, List<OptionPick>? picks = null) =>
        [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.Settings(folder, picks ?? [], new OptionDevices(), Place, side, roomCells: null)];

    private static BarItem Folder(params string[] faces)
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
        return folder;
    }

    private static string[] Buttons(IEnumerable<FlyoutItem> menu) => [.. menu.Where(Flyout.IsButton).Select(item => item.Text)];

    private static string[] Rows(IEnumerable<FlyoutItem> menu) => [.. menu.Where(item => !item.Primary && !item.Separator).Select(item => item.Text)];

    /// <summary>
    /// Its face's menu: Cut, Copy and Remove in the top row, no "Open" (§527: a click on its face opens it); then its faces in their
    /// order, "Add a face" (§567: no look of its own). Nothing of the folder opened: no alignment.
    /// </summary>
    [Fact]
    public void AFoldersFaceMenuIsItsFaces()
    {
        var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId);
        var menu = Menu(folder, FolderSide.Face);
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], Buttons(menu));
        var rows = Rows(menu);
        var faces = menu.Where(item => item.Reorder == OptionMenu.FaceGroup).Select(item => item.Text).ToList();
        Assert.Equal(2, faces.Count);
        Assert.Equal([.. faces, S.SettingsFolderFacesAddHeader], rows);
        Assert.DoesNotContain(S.CoreAlignTitle, rows);
    }

    /// <summary>
    /// Folder ▾'s menu, the folder open: Cut, Copy and Remove in its top row, no "Open"; where it stands as
    /// it opens. None of its faces, no "Add a face", and no Look › (§517: the folder's look is its face's menu's alone).
    /// </summary>
    [Fact]
    public void AFoldersPanelMenuIsWhatChangesTheFolderOpened()
    {
        var folder = Folder(FolderFaces.ClockId);
        var menu = Menu(folder, FolderSide.Panel);
        Assert.Equal([S.DockMenuCut, S.DockMenuCopy, S.DockMenuRemove], Buttons(menu));
        var rows = Rows(menu);
        Assert.Equal([S.DockFolderStyleTitle, S.CoreAlignTitle], rows[..2]);
        Assert.DoesNotContain(menu, item => item.Reorder == OptionMenu.FaceGroup);
        Assert.DoesNotContain(S.SettingsFolderFacesAddHeader, rows);
        Assert.DoesNotContain(S.SettingsAppearanceHeader, rows);
    }

    /// <summary>
    /// Every setting of a folder has one home (§411 R15): of every ready-made folder and of a folder wearing each face, the
    /// settings the two menus set — each row's key, the faces' moves, removals and additions — are in one of them and never
    /// both, and between them they are all of the folder's: its own rows, each face's, its faces.
    /// </summary>
    [Fact]
    public void EveryFolderSettingHasOneHome()
    {
        var machine = new PresetMachine(HasBattery: true, FixedDrives: []);
        IEnumerable<BarItem> folders = [.. Presets.All.Select(preset => Presets.Folder(preset, machine)), .. FolderFaces.All.Select(face => Folder(face.Id))];
        foreach (var folder in folders)
        {
            HashSet<string> Homes(FolderSide side)
            {
                List<OptionPick> picks = [];
                _ = Menu(folder, side, picks);
                return [.. picks.Select(Setting).OfType<string>()];
            }
            var face = Homes(FolderSide.Face);
            var panel = Homes(FolderSide.Panel);
            var what = $"{folder.Folder!.Face.Kind}: ";
            // §517: nothing in both.
            Assert.True(face.Intersect(panel).ToHashSet().Count == 0, what + string.Join(", ", face.Intersect(panel)));
            var options = folder.Folder!;
            HashSet<string> all =
            [
                .. OptionRows.Flat(OptionRows.Of(folder, new OptionDevices(), Place)).Where(row => row.Kind != OptionRowKind.Link).Select(row => $"-1:{row.Key}"),
                .. Enumerable.Range(0, options.Faces.Count).SelectMany(at => OptionRows.Flat(OptionRows.OfFace(options, at, new OptionDevices(), Place)).Where(row => row.Kind != OptionRowKind.Link).Select(row => $"{at}:{row.Key}")),
                "faces",
            ];
            Assert.True(all.SetEquals(face.Union(panel)), what + $"missing {string.Join(", ", all.Except(face.Union(panel)))}; more {string.Join(", ", face.Union(panel).Except(all))}");
        }
    }

    /// <summary>What setting a pick sets: its face's and its key, or the faces themselves; null for a row of nothing, or a link setting nothing.</summary>
    private static string? Setting(OptionPick pick) =>
        pick.Kind is OptionPickKind.Link ? null
        : pick.Kind is OptionPickKind.AddFace or OptionPickKind.RemoveFace or OptionPickKind.MoveFace ? "faces"
        : pick.Key.Length > 0 ? $"{pick.Face}:{pick.Key}"
        : null;

    /// <summary>Every other widget has one menu: the same of either side, and no "Open" in its top row.</summary>
    [Fact]
    public void AWidgetHasOneMenu()
    {
        foreach (var kind in Enum.GetValues<BarKind>().Where(kind => !kind.IsFolder()))
        {
            var item = kind.New();
            Assert.Equal(Rows(Menu(item, FolderSide.Face)), Rows(Menu(item, FolderSide.Panel)));
            Assert.DoesNotContain(S.DockWidgetMenuOpen, Buttons(TheDock.WidgetRowsFor(item)));
        }
    }
}
