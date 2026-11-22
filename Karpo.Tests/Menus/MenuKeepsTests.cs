// menus: what keeps a menu open (the person, 2026-10-19: "when i change a setting, choose something or
// do another action the submenu it should not close the whole menu ... when a tree node is extended and
// i click it again it should close again. if i click somewhere away from the node ... it should close
// the menu completely"; Karpo.Dock\MenuKeeps.cs, docs\widgets.md §219). Held here:
//
//  - **Which rows stay.** Every setting and face change of a widget's menu but "More settings…"; a
//    widget's own switches and picks, never what takes the person elsewhere or is done once; a tick
//    shown again flipped or moved.
//  - **A row with a submenu** (§246). The pointer resting on it only lights it; a click opens it,
//    the pointer going elsewhere leaves it open, a second click shuts it and the menu over it stays;
//    another parent's click opens its own instead.
//  - **The open path found again** by id and words, or by the words alone where they are one row's.
//  - **On a real menu** (KARPO_UI_TESTS=1): a choice picked on a settings menu's level in place keeps
//    the level and the tick moves, the panel standing still; a Refresh finds the levels again however
//    their parents changed, keeps the menu's scroll and lights a moved face's row where it went (§271).
//    A menu that cascades (the add list's): a parent clicked twice shuts; the keys go a level at a time;
//    a press outside closes everything.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class MenuKeepsTests
{
    private static BarItem Folder(params string[] faces)
    {
        var island = BarKind.FolderList.New();
        island.Id = "box";
        island.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
        return island;
    }

    // --- Which rows stay ---------------------------------------------------------------------------

    /// <summary>The action table: in a widget's menu every setting, every move and every face change stays; an action, asked first, closes.</summary>
    [Fact]
    public void EverySettingAndFaceChangeStaysButAnAction()
    {
        Assert.Equal(
            [OptionPickKind.Set, OptionPickKind.Flip, OptionPickKind.Step, OptionPickKind.Move, OptionPickKind.AddFace, OptionPickKind.RemoveFace, OptionPickKind.MoveFace, OptionPickKind.Field],
            Enum.GetValues<OptionPickKind>().Where(OptionMenu.KeepsOpen));

        // Each row a pick is: it stays where its kind keeps the menu open; a toggle, a stepper and a field by their controls.
        var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId);
        List<OptionPick> picks = [];
        var rows = OptionMenu.FaceSection(folder.Folder!, picks);
        static IEnumerable<FlyoutItem> All(IEnumerable<FlyoutItem> items) => items.SelectMany(item => (IEnumerable<FlyoutItem>)[item, .. All(item.Children ?? [])]);
        var checkedAny = 0;
        foreach (var row in All(rows))
        {
            if (OptionMenu.PickOf(picks, row.Id) is not { } pick || row.Control is not null)
            {
                continue;
            }
            Assert.Equal(OptionMenu.KeepsOpen(pick.Kind), row.Stays);
            checkedAny++;
        }
        Assert.True(checkedAny > 10);
        List<OptionPick> own = [];
        // An action, asked first in a dialog of its own, closes it (a line's list deleted, §228).
        var act = OptionMenu.Rows([new OptionRow("delete", OptionRowKind.Action, "Delete this list?") { Ask = new DeleteQuestion("Delete this list?", "", "Delete", "Cancel", 0) }], own).Single();
        Assert.False(act.Stays);
        Assert.False(OptionMenu.KeepsOpen(OptionPickKind.Act));
        // One that asks nothing — Appearance's "Reset all" (§311) — is done where the menu stands.
        Assert.True(OptionMenu.Rows([new OptionRow("reset", OptionRowKind.Action, "Reset all") { Set = _ => true }], own).Single().Stays);
    }

    /// <summary>The action table for a widget's own acts: switches and picks stay; what is done once or takes the person elsewhere closes.</summary>
    [Fact]
    public void AWidgetsSwitchesAndPicksStayAndTheRestClose()
    {
        WidgetAct[] stay =
        [
            WidgetAct.ChooseDevice, WidgetAct.Mute, WidgetAct.MuteMicrophone, WidgetAct.MuteApp, WidgetAct.FollowSession,
            // Energy saver and do not disturb say what a pick does, "Turn … on", as the radios do, and close (§308).
            WidgetAct.Shuffle, WidgetAct.PowerMode,
            // keep awake's lengths (§269).
            WidgetAct.AwakeFor,
        ];
        Assert.Equal(stay.Order(), Enum.GetValues<WidgetAct>().Where(MenuKeeps.Stays).Order());
        // Nothing that hands the screen away stays.
        Assert.All(stay, act => Assert.False(TheDock.HandsAway(act)));

        // A "Clear all" with nothing to clear is left out (§308).
        var (items, acts) = TheDock.ActRows([WidgetAct.ClockApp, WidgetAct.ClearAll, WidgetAct.WindowsSettings], null, TheDock.WindowsLinkOf(WidgetInfos.SoundVolume));
        Assert.All(items, item => Assert.False(item.Stays));
        Assert.Equal(2, acts.Count);
    }

    /// <summary>A switch's tick flips, a pick's moves to it from the others of its act, in a submenu too; the rest as it was.</summary>
    [Fact]
    public void ATickIsShownAgainWhereItWent()
    {
        var base0 = TheDock.CMD_WIDGET_ACT;
        List<WidgetActRow> acts =
        [
            new(base0, WidgetAct.ChooseDevice, "a"),
            new(base0 + 1, WidgetAct.ChooseDevice, "b"),
            new(base0 + 2, WidgetAct.Mute),
            new(base0 + 3, WidgetAct.ClockApp),
        ];
        List<FlyoutItem> items =
        [
            new(1, "Copy", Primary: true),
            new(9, "Devices", Children: [new(base0, "A", Checked: true, Stays: true), new(base0 + 1, "B", Checked: false, Stays: true)]),
            new(base0 + 2, "Mute", Checked: false, Stays: true),
            new(base0 + 3, "Open Clock"),
        ];
        var picked = MenuKeeps.Ticked(items, acts, base0 + 1);
        Assert.Equal([false, true], picked[1].Children!.Select(child => child.Checked == true));
        Assert.Equal(items[2], picked[2]);
        var muted = MenuKeeps.Ticked(items, acts, base0 + 2);
        Assert.True(muted[2].Checked);
        Assert.Equal(items[1].Children, muted[1].Children);
        // A row that does not stay changes nothing.
        Assert.Equal(items, MenuKeeps.Ticked(items, acts, base0 + 3));
    }

    // --- The open path found again ---------------------------------------------------------------------

    /// <summary>
    /// A row found again by its id and words, else by its words where they are one row's, else (§365) by its id
    /// where its words changed; never a heading's or a rule's.
    /// </summary>
    [Fact]
    public void ARowIsFoundAgainByIdAndWordsOrItsWordsAloneOrItsIdAlone()
    {
        List<FlyoutItem> rows = [new(0, "", Separator: true), new(5, "Faces", Header: true), new(6, "Weather"), new(7, "Clock"), new(8, "Clock")];
        Assert.Equal(3, MenuKeeps.Again(rows, new FlyoutItem(7, "Clock")));
        Assert.Equal(2, MenuKeeps.Again(rows, new FlyoutItem(7, "Weather")));
        Assert.Equal(-1, MenuKeeps.Again(rows, new FlyoutItem(9, "Clock")));
        Assert.Equal(-1, MenuKeeps.Again(rows, new FlyoutItem(5, "Faces")));
        // Renamed where it stands (a layout named in its own level): by its id.
        Assert.Equal(2, MenuKeeps.Again(rows, new FlyoutItem(6, "Battery")));
        // Its words two rows', its id a third's: its id.
        Assert.Equal(2, MenuKeeps.Again(rows, new FlyoutItem(6, "Clock")));
        Assert.Equal(-1, MenuKeeps.Again(rows, new FlyoutItem(9, "Battery")));
    }

    // --- On a real menu ------------------------------------------------------------------------------

    private static readonly RECT Work = new() { right = 2880, bottom = 1560 };

    private static Flyout Shown(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, RECT? work = null, bool configures = true)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = configures };
        var room = work ?? Work;
        menu.Show(gfx, AddListPictures.LookFor(true), 1.5f, items, FlyoutLayout.List, new POINT { x = room.right / 3, y = room.bottom }, room, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    /// <summary>The pointer resting where it is for longer than Windows' submenu delay, every timer and post of the menu's window handed to it, as the dock's procedure hands them.</summary>
    private static unsafe void Rest(Flyout menu, int ms = 600)
    {
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            // Its timers and its own posts; not the leave Windows says for a pointer the test only told it of.
            while (PeekMessageW(&msg, menu.Hwnd, WM.WM_TIMER, WM.WM_TIMER, PM.PM_REMOVE) || PeekMessageW(&msg, menu.Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE))
            {
                _ = menu.Message(msg.message, msg.wParam, msg.lParam);
            }
            Thread.Sleep(10);
        }
    }

    /// <summary>
    /// A choice picked in a face's settings, a level of its own again since §226, in place since §271: an
    /// Adjust, the menu open, and the panel where it stood on the screen; the Refresh for it goes back to the
    /// face's level (§392: a choice is one), what is under the pointer lit (§409), the tick moved under it.
    /// </summary>
    [UiFact]
    public void AChoicePickedKeepsTheMenuOpenAndTheTickMoves()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-choice", (gfx, instance) =>
        {
            var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId);
            List<OptionPick> picks = [];
            List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.FaceSection(folder.Folder!, picks)];
            using var menu = Shown(gfx, instance, items);
            var clock = items.Single(item => item.Text == "Clock");
            Assert.Equal(default, menu.Click(clock.Id));
            WindowShots.Settle();
            var faces = Assert.Single(menu.OpenPanels).Panel;
            // The clock's hours (an alignment is its buttons in its row since §253).
            var hours = clock.Children!.Single(row => OptionMenu.PickOf(picks, row.Id)?.Key == ClockFace.Hours);
            Assert.Equal(default, menu.Click(hours.Id));
            Assert.Equal((2, hours.Text), (menu.InlineDepth, menu.InlineTitle));

            var twelve = hours.Children![1];
            var at = menu.RowMiddle(twelve.Id)!.Value;
            var picked = menu.Click(twelve.Id);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, twelve.Id), picked);
            Assert.True(menu.IsOpen);
            Assert.Equal(2, menu.InlineDepth);

            Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, picked.Id)!));
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.FaceSection(folder.Folder!, [])));
            WindowShots.Settle();
            Assert.Equal(faces, Assert.Single(menu.OpenPanels).Panel);
            // §409: picked by the pointer, what is under it lit.
            Assert.Equal((1, menu.IdAt(at)), (menu.InlineDepth, menu.HotId));
            var now = menu.ShownItems.Single(item => item.Text == "Clock").Children!.Single(row => !row.Header && row.Text == hours.Text).Children!;
            Assert.Equal([false, true, false], now.Select(choice => choice.Checked == true));
            menu.Hide();
        });
    }

    /// <summary>
    /// No layout shift (§226: "when the setting changed changes the contents of the previous menu it
    /// should not shift the submenu"): a Refresh whose rows put another row over the open level's parent
    /// and a row far longer leaves the level in place exactly where it stood; a level whose row went
    /// shuts, the menu standing on its foot.
    /// </summary>
    [UiFact]
    public void OpenLevelsStandStillThroughARefreshThatChangesTheirParent()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-still", (gfx, instance) =>
        {
            List<FlyoutItem> Items(bool more) =>
            [
                new(1, more ? "A row that says a good deal more than it did before it changed" : "Open"),
                .. more ? [new FlyoutItem(4, "Something new")] : Array.Empty<FlyoutItem>(),
                new(2, "Style", Children:
                [
                    new(21, "Plain", Checked: !more, Stays: true),
                    new(22, "Fancy", Checked: more, Stays: true),
                    new(23, "Colour", Children: [new(231, "Red", Checked: true, Stays: true), new(232, "Blue", Stays: true)]),
                ]),
                new(3, "View", Children: [new(31, "Large"), new(32, "Small")]),
                .. more ? Array.Empty<FlyoutItem>() : [new FlyoutItem(5, "Something old")],
            ];
            using var menu = new Flyout(gfx, instance, 0) { Configures = true };
            var room = Work;
            menu.Show(gfx, AddListPictures.LookFor(true), 1.5f, Items(false), FlyoutLayout.List, new POINT { x = room.right / 3, y = room.bottom }, room, Edge.Bottom, null);
            WindowShots.Settle();
            _ = menu.Click(2);
            _ = menu.Click(23);
            Assert.Equal(2, menu.InlineDepth);
            var before = Assert.Single(menu.OpenPanels).Panel;

            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, 232), menu.Click(232));
            menu.Refresh(Items(true));
            WindowShots.Settle();
            // The level one width however long the menu's rows (§230), where it was: it did not move at all.
            Assert.Equal(("Colour", before), (menu.InlineTitle, Assert.Single(menu.OpenPanels).Panel));
            Assert.Equal(22u, menu.ShownItems.Single(item => item.Id == 2).Children!.Single(child => child.Checked == true).Id);

            // A Refresh without the row a level hangs off: that level shuts, the menu on the foot it stood on.
            menu.Refresh([.. Items(true).Where(item => item.Id != 2)]);
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal((before.left, before.right, before.bottom), (menu.OpenPanels[0].Panel.left, menu.OpenPanels[0].Panel.right, menu.OpenPanels[0].Panel.bottom));
            menu.Hide();
        });
    }

    /// <summary>A panel kept where it stood moves only as far as it must to stay inside its room, from its start where it is longer than the room.</summary>
    [Fact]
    public void AHeldPanelMovesOnlyToStayOnTheScreen()
    {
        Assert.Equal(100, FlyoutHost.Held(100, 50, 0, 1000));
        Assert.Equal(950, FlyoutHost.Held(990, 50, 0, 1000));
        Assert.Equal(10, FlyoutHost.Held(-5, 50, 10, 1000));
        Assert.Equal(0, FlyoutHost.Held(300, 2000, 0, 1000));
    }

    /// <summary>
    /// §246 (the person: "the sub menus should [not] open on hover, only when i click them
    /// deliberatly"): the pointer on a parent, however long, only lights it; a click opens its submenu,
    /// which stays open while the pointer goes over other rows, and a second click shuts it, the menu
    /// staying; another parent's click opens its own in place of the open one.
    /// </summary>
    [UiFact]
    public void ASubmenuOpensOnlyByAClick()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-toggle", (gfx, instance) =>
        {
            List<FlyoutItem> items =
            [
                new(1, "Open"),
                new(2, "Sort by", Children: [new(21, "Name"), new(22, "Date")]),
                new(3, "View", Children: [new(31, "Large"), new(32, "Small")]),
            ];
            using var menu = Shown(gfx, instance, items, configures: false);
            // The pointer on a parent, then waiting longer than any submenu delay: lit, nothing opens.
            menu.PointTo(2);
            Rest(menu);
            WindowShots.Settle();
            Assert.Equal((2u, 1), (menu.HotId!.Value, menu.OpenPanels.Count));

            Assert.Equal(default, menu.Click(2));
            Assert.Equal(2, menu.OpenPanels.Count);
            // The pointer over another parent and a plain row: the open one stays, the other opens nothing.
            menu.PointTo(3);
            Rest(menu);
            menu.PointTo(1);
            Rest(menu);
            WindowShots.Settle();
            Assert.Equal(2, menu.OpenPanels.Count);
            menu.PointTo(21);
            Assert.Equal(21u, menu.HotIdInSubmenu);
            Assert.Equal(default, menu.Click(2));
            Assert.True(menu.IsOpen);
            Assert.Single(menu.OpenPanels);
            Assert.Equal(2u, menu.HotId);

            // Another parent's click opens its own in place of the open one; its own click shuts it.
            _ = menu.Click(2);
            _ = menu.Click(3);
            Assert.Equal(2, menu.OpenPanels.Count);
            Assert.Equal(3u, menu.HotId);
            menu.PointTo(31);
            Assert.Equal(31u, menu.HotIdInSubmenu);
            _ = menu.Click(3);
            Assert.Single(menu.OpenPanels);
            menu.Hide();
        });
    }

    /// <summary>Right and Enter open a level, Left and Escape shut one, the menu staying till Escape at its top.</summary>
    [UiFact]
    public void TheKeysGoALevelAtATime()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-keys", (gfx, instance) =>
        {
            List<FlyoutItem> items =
            [
                new(1, "View", Children: [new(11, "Size", Children: [new(111, "Large"), new(112, "Small")]), new(12, "Other")]),
                new(2, "Open"),
            ];
            using var menu = Shown(gfx, instance, items, configures: false);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
            Assert.Equal(1u, menu.KeysOn);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
            Assert.Equal(2, menu.OpenPanels.Count);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0);
            Assert.Equal(3, menu.OpenPanels.Count);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
            Assert.Equal(2, menu.OpenPanels.Count);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
            Assert.Equal(3, menu.OpenPanels.Count);
            Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0).Kind);
            Assert.Equal(2, menu.OpenPanels.Count);
            Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0).Kind);
            Assert.Single(menu.OpenPanels);
            Assert.True(menu.IsOpen);
            Assert.Equal(FlyoutEventKind.Dismissed, menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0).Kind);
            Assert.False(menu.IsOpen);
        });
    }

    /// <summary>A press outside the menu, its submenus open, closes all of it.</summary>
    [UiFact]
    public void APressOutsideClosesEveryLevel()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-outside", (gfx, instance) =>
        {
            List<FlyoutItem> items = [new(1, "View", Children: [new(11, "Size", Children: [new(111, "Large")])]), new(2, "Open")];
            using var menu = Shown(gfx, instance, items, configures: false);
            _ = menu.Click(1);
            _ = menu.Click(11);
            Assert.Equal(3, menu.OpenPanels.Count);
            // In the root's shadow margin, outside its panel: outside the whole menu.
            Assert.Equal(FlyoutEventKind.Dismissed, menu.Message(WM.WM_LBUTTONDOWN, 1, 0).Kind);
            Assert.False(menu.IsOpen);
            // No submenu is left open behind it (the root's own panel is all there is to list).
            Assert.Single(menu.OpenPanels);
        });
    }

    /// <summary>
    /// A face moved by its row on the first level (Ctrl+Up, §220): the menu shown again with its row lit
    /// where it went, its level opening from there; a face taken off by its row's bin (Delete, §240):
    /// its row gone, the menu staying.
    /// </summary>
    [UiFact]
    public void AMovedFacesRowIsLitWhereItWentAndARemovedOneGoes()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-faces", (gfx, instance) =>
        {
            var folder = Folder(FolderFaces.ClockId, FolderFaces.WeatherId, FolderFaces.BatteryId);
            List<OptionPick> picks = [];
            List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.FaceSection(folder.Folder!, picks)];
            using var menu = Shown(gfx, instance, items);
            var weather = items.Single(item => item.Text == "Weather");
            menu.PointTo(weather.Id);
            KeyState.Held = key => key == VK.VK_CONTROL;
            FlyoutEvent moved;
            try
            {
                moved = menu.Message(WM.WM_KEYDOWN, VK.VK_UP, 0);
            }
            finally
            {
                KeyState.Held = null;
            }
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Reorder, weather.Id, To: 0), moved);
            Assert.True(OptionMenu.Apply(folder, OptionMenu.Moved(OptionMenu.PickOf(picks, moved.Id), moved.To)!));
            picks = [];
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.FaceSection(folder.Folder!, picks)), OptionMenu.FaceRowOf(picks, 0));
            var weatherNow = menu.ShownItems.Single(item => item.Text == "Weather");
            Assert.Equal(weatherNow.Id, menu.HotId);
            var faces = menu.ShownItems.Where(item => item.Reorder != 0).Select(item => item.Text);
            Assert.Equal(["Weather", "Clock", FolderFaces.Battery.Title], faces);
            // Its level opens from where it went, and goes back onto it.
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
            Assert.Equal((1, "Weather"), (menu.InlineDepth, menu.InlineTitle));
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
            Assert.Equal((0, weatherNow.Id), (menu.InlineDepth, menu.HotId!.Value));

            // Its bin, on its row before the chevron (§240), by Delete while the row is lit.
            var remove = ((FlyoutControl.Tools)weatherNow.Control!).Buttons.Single(tool => !tool.Grip);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, remove.Id), menu.Message(WM.WM_KEYDOWN, VK.VK_DELETE, 0));
            Assert.True(OptionMenu.Apply(folder, OptionMenu.PickOf(picks, remove.Id)!));
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.FaceSection(folder.Folder!, [])));
            Assert.True(menu.IsOpen);
            Assert.DoesNotContain(menu.ShownItems, item => item.Text == "Weather");
            menu.Hide();
        });
    }

    /// <summary>A long menu scrolled, a choice picked on a level at its end: the level shown again, and the menu scrolled where it was once it goes back.</summary>
    [UiFact]
    public void ARefreshKeepsTheScroll()
    {
        WindowShots.OnHiddenDesktop("menu-keeps-scroll", (gfx, instance) =>
        {
            List<FlyoutItem> Items(int chosen) =>
            [
                .. Enumerable.Range(1, 40).Select(at => new FlyoutItem((uint)at, $"Row {at}")),
                new(100, "Pace", Children: [new(101, "1 s", Checked: chosen == 0, Stays: true), new(102, "2 s", Checked: chosen == 1, Stays: true)]),
            ];
            using var menu = Shown(gfx, instance, Items(0), new RECT { right = 2880, bottom = 900 });
            for (var step = 0; step < 50 && menu.KeysOn != 100; step++)
            {
                _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
            }
            Assert.Equal(100u, menu.KeysOn);
            var scrolled = menu.Scrolls[0];
            Assert.True(scrolled > 0);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, 102), menu.Message(WM.WM_KEYDOWN, VK.VK_RETURN, 0));
            menu.Refresh(Items(1));
            Assert.Equal((1, 102u), (menu.InlineDepth, menu.HotId!.Value));
            Assert.True(menu.ShownItems[^1].Children![1].Checked);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            Assert.Equal((0, 100u), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal(scrolled, menu.Scrolls[0]);
            menu.Hide();
        });
    }
}
