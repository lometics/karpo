// menus: an alignment's choices are buttons in its row, never a submenu (§253, the person: "alignment
// left center right should not have a submenu, it should just show three buttons on the same level";
// OptionRow.Segmented, FlyoutControl.Segments, Flyout.Controls.cs). The one alignment left is a folder's
// (§385): no line (2026-10-29) and no face (§277) has one. Held here:
//
//  - **Which rows.** A folder's alignment is segmented, its four choices each with its icon; no other
//    row of any widget or face is.
//  - **The menu's row.** One row, its title and icon, the four as buttons at its end, the one in force
//    chosen, no submenu; each button sets its choice.
//  - **Keys.** Left and Right go along the buttons as they stand — mirrored in a mirrored menu — and
//    stop at either end.
//  - **On a real menu** (KARPO_UI_TESTS=1): a click on a button picks it, the one in force picks
//    nothing, a click elsewhere on the row does nothing; with the row lit Right goes on and Enter picks,
//    and the menu stays. Pictures in KARPO_PNG_DIR at 150 %, dark and light: menu-align-folder-<look>.png
//    (a folder's menu) and menu-align-folder-keys-<look>.png (the keys on its second button).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class AlignSegmentsTests
{
    /// <summary>A folder wearing <paramref name="faces"/>.</summary>
    private static BarItem Folder(params string[] faces)
    {
        var folder = BarKind.FolderList.New();
        folder.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
        return folder;
    }

    [Fact]
    public void TheFoldersAlignmentIsSegmentedAndNothingElse()
    {
        List<OptionRow> rows = [];
        foreach (var item in Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()).Concat(Enum.GetValues<ListKind>().Select(kind => kind.New())))
        {
            rows.AddRange(OptionRows.Of(item));
        }
        foreach (var face in FolderFaces.All)
        {
            rows.AddRange(OptionRows.OfFace(Folder(face.Id).Folder!, 0));
        }
        rows.AddRange(OptionRows.Of(BarKind.FolderList.New()));
        // §411 (R3): a place or a direction alone: the folder's alignment and the volume slider's direction.
        Assert.Equal([OptionRows.FolderAlign, OptionRows.VolumeOrientation], rows.Where(row => row.Segmented).Select(row => row.Key).Distinct().Order(StringComparer.Ordinal));
        var segmented = rows.First(row => row.Key == OptionRows.FolderAlign);
        // Automatic before Left, Center and Right, on the screen as they stand.
        Assert.Equal((OptionRows.FolderAlign, OptionRowKind.Choice), (segmented.Key, segmented.Kind));
        Assert.Equal([OptionGlyphs.Automatic, OptionGlyphs.AlignLeft, OptionGlyphs.AlignCenter, OptionGlyphs.AlignRight], segmented.ChoiceGlyphs);
    }

    /// <summary>A folder's alignment in its menu: one row, its four buttons at its end, no submenu; each sets its choice.</summary>
    [Fact]
    public void TheRowHoldsItsButtons()
    {
        var folder = BarKind.FolderList.New();
        List<OptionPick> picks = [];
        var items = OptionMenu.Rows(OptionRows.Of(folder), picks);
        var row = Assert.Single(items, item => item.Control is FlyoutControl.Segments);
        Assert.Equal(S.CoreAlignTitle, row.Text);
        Assert.Null(row.Children);
        Assert.Null(row.Value);
        Assert.Equal(new FlyoutIcon.Glyph(OptionGlyphs.AlignLeft), row.Icon);
        var segments = (FlyoutControl.Segments)row.Control!;
        Assert.Equal(0, segments.Chosen);
        Assert.Equal([S.DockFolderAlignAuto, S.CoreEdgeLeft, S.CoreAlignCenter, S.CoreEdgeRight], segments.Buttons.Select(button => button.Name));
        Assert.Equal([OptionGlyphs.Automatic, OptionGlyphs.AlignLeft, OptionGlyphs.AlignCenter, OptionGlyphs.AlignRight], segments.Buttons.Select(button => button.Glyph));

        var right = OptionMenu.PickOf(picks, segments.Buttons[3].Id)!;
        Assert.Equal(OptionPickKind.Set, right.Kind);
        Assert.True(OptionMenu.KeepsOpen(right.Kind));
        Assert.True(OptionMenu.Apply(folder, right));
        Assert.Equal(FolderAlign.Right, folder.Folder!.Align);
        Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, segments.Buttons[0].Id)!));
        Assert.Equal(FolderAlign.Auto, folder.Folder.Align);
    }

    [Theory]
    [InlineData(0, true, false, 1)]
    [InlineData(1, true, false, 2)]
    [InlineData(2, true, false, 2)]
    [InlineData(1, false, false, 0)]
    [InlineData(0, false, false, 0)]
    // Mirrored, the first stands on the right: Right goes back towards it, Left on.
    [InlineData(1, true, true, 0)]
    [InlineData(0, true, true, 0)]
    [InlineData(1, false, true, 2)]
    [InlineData(2, false, true, 2)]
    public void LeftAndRightGoAlongThemAsTheyStand(int from, bool right, bool mirrored, int to)
    {
        Assert.Equal(to, Flyout.SegmentStep(from, 3, right, mirrored));
    }

    private static LPARAM At(POINT point) => (LPARAM)(nint)((point.y << 16) | (point.x & 0xFFFF));

    private static FlyoutEvent Click(Flyout menu, POINT point)
    {
        _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(point));
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(point));
        return menu.Message(WM.WM_LBUTTONUP, 0, At(point));
    }

    private static void KeysTo(Flyout menu, uint id, Func<uint?> hot)
    {
        for (var step = 0; step < 80 && hot() != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, hot());
    }

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    [UiFact]
    public void OnARealMenuAClickOrTheKeysPickInTheRow()
    {
        WindowShots.OnHiddenDesktop("menu-align", (gfx, instance) =>
        {
            const float scale = 1.5f;
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark);
                var tone = dark ? "dark" : "light";
                var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
                var anchor = new POINT { x = work.right / 4, y = work.bottom };

                // A folder's menu: the row at the root.
                using (var menu = new Flyout(gfx, instance, 0) { Configures = true })
                {
                    List<OptionPick> picks = [];
                    List<FlyoutItem> items = [.. TheDock.WidgetButtons(), .. OptionMenu.Section(OptionRows.Of(BarKind.FolderList.New()), picks)];
                    menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                    WindowShots.Settle();
                    var row = items.Single(item => item.Control is FlyoutControl.Segments);
                    var segments = (FlyoutControl.Segments)row.Control!;
                    // The three stand in the row, inside the menu, after the words.
                    var looks = menu.Looks.Single(each => each.Id == row.Id);
                    var middle = menu.RowMiddle(row.Id)!.Value;
                    var toScreen = middle.x - (looks.Plate.X + (looks.Plate.W / 2));
                    var centres = Enumerable.Range(0, 4).Select(at => menu.ToolMiddle(row.Id, at)!.Value).ToList();
                    Assert.Null(menu.ToolMiddle(row.Id, 4));
                    Assert.True(centres[0].x < centres[1].x && centres[1].x < centres[2].x && centres[2].x < centres[3].x);
                    Assert.True(centres[0].x - toScreen > looks.Words.X + looks.Words.W);
                    Assert.True(centres[3].x - toScreen < looks.Plate.X + looks.Plate.W);
                    Assert.All(centres, centre => Assert.InRange(centre.y, middle.y - 1, middle.y + 1));
                    Assert.True(Save(menu, $"menu-align-folder-{tone}.png", dark));

                    // A click on one picks it, the menu staying; on the one in force, or the words, nothing.
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, segments.Buttons[1].Id), Click(menu, centres[1]));
                    Assert.True(menu.IsOpen);
                    Assert.Equal(default, Click(menu, centres[0]));
                    Assert.Equal(default, Click(menu, new POINT((int)(looks.Words.X + toScreen + 2), middle.y)));
                    Assert.Equal(0, menu.InlineDepth);

                    // The keys: Right goes on from the one in force, lighting it, Enter picks; Left back to it picks nothing.
                    _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(new POINT(1, 1)));
                    KeysTo(menu, row.Id, () => menu.HotId);
                    Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0).Kind);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-align-folder-keys-{tone}.png", dark));
                    Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0).Kind);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, segments.Buttons[2].Id), menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0));
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                    Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_SPACE, 0).Kind);
                    Assert.Equal(row.Id, menu.HotId);
                    menu.Hide();
                }
            }
        });
    }
}
