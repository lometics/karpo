// menus: a widget's menu at rest is its own acts alone (the person, 2026-10-20: "the right click
// context menu actions that are copy, remove, duplicate etc whatever, or change the appearance of a
// widget should only appear in edit mode. they should be removed from all other places"; §253). Over
// every kind there is — every line of a list, every entry of the bar, a folder wearing each face, and
// a folder of several — the menu Dock.RestMenuRows makes has no icon button, no submenu (a setting's
// choices), and no row that is not one of its acts: so nothing of edit mode's row (Cut, Copy, Remove),
// of a widget's settings (OptionMenu's rows, the tray's icons among them, §261) nor of a line's height
// (LineHeightRows). Its way into edit mode, "Edit folder", is the floor's. A kind added later is held to it unasked.
// Decisions, not drawings: the drawn menus are MenuModePictures'.

using Karpo.Dock;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

public sealed class RestMenusTests
{
    /// <summary>Every widget's menu at rest, by name: each line's in a folder, each bar entry's, a folder wearing each face, and one of several.</summary>
    private static IEnumerable<(string Name, WidgetItem Island, bool InFolder)> Widgets()
    {
        foreach (var kind in Enum.GetValues<ListKind>())
        {
            yield return ($"{kind} line", kind.New(), true);
        }
        foreach (var kind in Enum.GetValues<BarKind>().Where(kind => BarEntries.Find(kind) is not null))
        {
            yield return ($"{kind} entry", kind.New(), false);
        }
        foreach (var face in FolderFaces.All)
        {
            yield return ($"folder wearing {face.Id}", new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Faces = [FolderFace.Of(face.Id)] } }, false);
        }
        yield return ("folder of several", new BarItem { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Faces = [.. FolderFaces.All.Take(4).Select(face => FolderFace.Of(face.Id))] } }, false);
    }

    [Fact]
    public void NoMenuAtRestHasAnEditOrASettingsRow()
    {
        var installed = WindowsButtons.VersionOf;
        WindowsButtons.VersionOf = _ => new Version(11, 2607);
        try
        {
            foreach (var (name, island, inFolder) in Widgets())
            {
                var (items, acts) = TheDock.RestMenuRows(island, inFolder);
                Assert.NotEmpty(items);
                Assert.DoesNotContain(items, row => row.Primary);
                Assert.DoesNotContain(items, row => row.Children is { Count: > 0 });
                foreach (var row in items.Where(row => row is { Separator: false, Header: false }))
                {
                    Assert.True(TheDock.ActRowOf(acts, row.Id) is not null, $"{name}: \"{row.Text}\" is no act of its own");
                    Assert.False(OptionMenu.Holds(row.Id), $"{name}: \"{row.Text}\" is a setting");
                }
                // Nor any of edit mode's buttons by name, in case one ever comes by another command.
                foreach (var button in TheDock.WidgetButtons())
                {
                    Assert.DoesNotContain(items, row => row.Text == button.Text);
                }
            }
        }
        finally
        {
            WindowsButtons.VersionOf = installed;
        }
    }

    /// <summary>Edit mode's menu keeps them: its row of Cut, Copy and Remove for every widget, the tray's too (its Settings gone, §261).</summary>
    [Fact]
    public void EditModesMenuKeepsTheRow()
    {
        foreach (var (_, island, _) in Widgets())
        {
            Assert.Equal(["Cut", "Copy", "Remove"], TheDock.WidgetRowsFor(island).Where(row => row.Primary).Select(row => row.Text));
        }
        Assert.Equal(["Cut", "Copy", "Remove"], TheDock.WidgetRowsFor(BarKind.Tray.New()).Select(row => row.Text));
    }
}
