// edit: the selection's acts, one way whatever asks for them (the person, 2026-11-03: "when selecting multiple widgets
// ctrl x does not work. maybe other shortcuts wont work too"; "when the context menu is open i should still be able to
// perform the same keyboard shortcuts like cut copy etc.").
//
//  - **What the keys act on** (EditSelection.KeysOn): every selected widget, unless the keys walked the focus to a widget
//    outside the selection, which they act on alone; with none selected, the widget last pressed. The focus a press leaves
//    on a bar is no reason to act on that widget alone: a lasso or a Ctrl+click after it selected others, and Ctrl+X cut
//    the one pressed — or, Ctrl+clicked out of the selection, the one no longer selected.
//  - **One path** (Dock.SelectionAct): the bar's keys, an arranged folder's (FolderFlyoutAction.Selection), a menu's
//    Cut and Remove, and a key a menu passes on all call it.
//  - **A menu open** passes the selection's shortcuts it has no row for (Ctrl+V, Ctrl+Z, Ctrl+Y, Ctrl+A, and Ctrl+X,
//    Ctrl+C, Delete in a menu without the buttons) to the dock and closes (FlyoutEventKind.PassedKey); the dock gives the
//    key to the surface that has the keys with the menu closed. A row's own shortcut still chooses the row.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed class SelectionKeysTests
{
    private static readonly string Owner = Config.LayoutOwner("only");

    private static string Key(string id) => Config.IslandKey(Owner, id);

    /// <summary>A layout of four widgets on its bar, a to d.</summary>
    private static Config Four()
    {
        var config = new Config();
        config.Layouts.Clear();
        config.Layouts.Add(new TaskbarLayout
        {
            Id = "only",
            Name = "Default",
            Islands = [.. new[] { "a", "b", "c", "d" }.Select(id => new BarItem { Id = id, Kind = BarKind.Search })],
        });
        return config;
    }

    private static string[] Order(Config config) => [.. config.ListFor(Owner).Select(island => Config.IslandKey(Owner, island.Id))];

    [Theory]
    [InlineData(VK.VK_DELETE, false, false, true)]
    [InlineData('X', true, false, true)]
    [InlineData('C', true, false, true)]
    [InlineData('V', true, false, true)]
    [InlineData('A', true, false, true)]
    [InlineData('Z', true, false, true)]
    [InlineData('Z', true, true, true)]
    [InlineData('Y', true, false, true)]
    [InlineData('X', false, false, false)]
    [InlineData(VK.VK_LEFT, true, false, false)]
    [InlineData(VK.VK_DOWN, false, true, false)]
    [InlineData(VK.VK_RETURN, false, false, false)]
    [InlineData(VK.VK_ESCAPE, false, false, false)]
    [InlineData(VK.VK_F2, false, false, false)]
    public void TheSelectionsShortcutsAreDeleteCutCopyPasteSelectAllUndoAndRedo(int key, bool control, bool shift, bool selection) =>
        Assert.Equal(selection, EditKeys.OfTheSelection(EditKeys.Of(key, control, shift, repeat: false).Act));

    /// <summary>
    /// Regression: several selected after a press left the bar's focus on another widget — a lasso, a Ctrl+click taking
    /// it out — and Ctrl+X, Ctrl+C and Delete acted on that one alone. The keys act on the selection; on the one the keys
    /// walked to only where it is not one of them; on the one pressed only with none selected.
    /// </summary>
    [Fact]
    public void TheKeysActOnTheSelectionNotOnTheWidgetLastPressed()
    {
        var order = Order(Four());
        var selection = new EditSelection();
        // None selected: the one pressed, or nothing.
        Assert.Equal([Key("d")], selection.ActedOn(selection.KeysOn(walked: null, pressed: Key("d")), order));
        Assert.Empty(selection.ActedOn(selection.KeysOn(walked: null, pressed: null), order));

        selection.Toggle(Key("a"));
        selection.Toggle(Key("b"));
        selection.Toggle(Key("c"));
        // d pressed before, then a, b and c selected: all three, never d.
        Assert.Equal([Key("a"), Key("b"), Key("c")], selection.ActedOn(selection.KeysOn(walked: null, pressed: Key("d")), order));
        // c Ctrl+clicked out (pressed last): a and b, never c.
        selection.Toggle(Key("c"));
        Assert.Equal([Key("a"), Key("b")], selection.ActedOn(selection.KeysOn(walked: null, pressed: Key("c")), order));
        // The keys walked to b, one of them: all of them; to d, outside them: d alone, as Tab then Delete in Explorer.
        Assert.Equal([Key("a"), Key("b")], selection.ActedOn(selection.KeysOn(walked: Key("b"), pressed: Key("b")), order));
        Assert.Equal([Key("d")], selection.ActedOn(selection.KeysOn(walked: Key("d"), pressed: Key("d")), order));
    }

    /// <summary>Ctrl+X with a, b and c selected, d pressed last: all three on the clipboard's list and out of the file, d stays.</summary>
    [Fact]
    public void CtrlXWithSeveralSelectedCutsThemAll()
    {
        var config = Four();
        var selection = new EditSelection();
        selection.Set([Key("a"), Key("b"), Key("c")], []);
        var cut = TheDock.Clipped(config, selection.ActedOn(selection.KeysOn(walked: null, pressed: Key("d")), Order(config)));
        Assert.Equal([Key("a"), Key("b"), Key("c")], cut);
        Assert.Equal(cut, TheDock.RemoveFrom(config, cut));
        Assert.Equal([Key("d")], Order(config));
    }

    /// <summary>
    /// One path for the selection's acts: the bar's keys, an arranged folder's, a menu's Cut and Remove and a key a menu
    /// passes on all go through Dock.SelectionAct; the bar's keys on what EditSelection.KeysOn says.
    /// </summary>
    [Fact]
    public void EveryWayToTheSelectionsActsIsOne()
    {
        var dock = RepoSource.Read("Karpo.Dock", "Dock.cs");
        Assert.Contains("case var act when EditKeys.OfTheSelection(act):", dock);
        Assert.Contains("SelectionAct(act, window, editSelection.KeysOn(window.WalkedKey, window.FocusedKey));", dock);
        Assert.Contains("return SelectionAct(EditKeyAct.Remove, window, what.Target);", dock);
        Assert.Contains("case FlyoutEventKind.PassedKey:", dock);
        Assert.Contains("flyout.PassesKey = PassedFromMenu;", dock);
        var folders = RepoSource.Read("Karpo.Dock", "Dock.Folders.cs");
        Assert.Contains("case FolderFlyoutAction.Selection:", folders);
        Assert.Contains("SelectionAct(happened.Act, opened.Window, happened.Widget?.Key, opened);", folders);
        Assert.Contains("return SelectionAct(EditKeyAct.Cut, window, key, inFolder ? folder : null);", RepoSource.Read("Karpo.Dock", "Dock.EditCopy.cs"));
        // No other way to them.
        Assert.DoesNotContain("CutSelected(window.FocusedKey)", dock);
        Assert.DoesNotContain("case FolderFlyoutAction.Cut:", folders);
    }

    /// <summary>
    /// A menu open passes the selection's shortcuts it has no row for to the dock, closing as a row's choice closes it; a
    /// row's own shortcut still chooses the row; a key of nobody's, or a menu that passes nothing, stays the menu's.
    /// </summary>
    [UiFact]
    public void AnOpenMenuPassesTheSelectionsShortcutsOn()
    {
        WindowShots.OnHiddenDesktop("menu-passes-keys", (gfx, instance) =>
        {
            var held = KeyState.Held;
            KeyState.Held = key => key == VK.VK_CONTROL;
            try
            {
                var work = new RECT { right = 1920, bottom = 1040 };
                Flyout Shown(bool passes)
                {
                    var menu = new Flyout(gfx, instance, 0);
                    if (passes)
                    {
                        menu.PassesKey = key => EditKeys.OfTheSelection(EditKeys.Of(key, KeyState.Down(VK.VK_CONTROL), KeyState.Down(VK.VK_SHIFT), repeat: false).Act);
                    }
                    menu.Show(gfx, AddListPictures.LookFor(true), 1f, [.. TheDock.WidgetButtons(), new(7, "Own")], FlyoutLayout.List, new POINT { x = 960, y = 1040 }, work, Edge.Bottom, null);
                    WindowShots.Settle(30);
                    return menu;
                }

                using (var menu = Shown(passes: true))
                {
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.PassedKey, 'V'), menu.Message(WM.WM_KEYDOWN, 'V', 0));
                    Assert.False(menu.IsOpen);
                }
                using (var menu = Shown(passes: true))
                {
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.PassedKey, 'Z'), menu.Message(WM.WM_KEYDOWN, 'Z', 0));
                }
                using (var menu = Shown(passes: true))
                {
                    // The row's own: Ctrl+C is its Copy.
                    var chosen = menu.Message(WM.WM_KEYDOWN, 'C', 0);
                    Assert.Equal(new FlyoutEvent(FlyoutEventKind.Invoke, TheDock.WidgetButtons().Single(row => row.Keys == MenuChord.Copy).Id), chosen);
                }
                using (var menu = Shown(passes: true))
                {
                    Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, 'Q', 0).Kind);
                    Assert.True(menu.IsOpen);
                    menu.Hide();
                }
                using (var menu = Shown(passes: false))
                {
                    Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, 'V', 0).Kind);
                    Assert.True(menu.IsOpen);
                    menu.Hide();
                }
            }
            finally
            {
                KeyState.Held = held;
            }
        });
    }
}
