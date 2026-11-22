// menus: a widget's menu tidied (2026-10-19; docs\widgets.md §225). Held here:
//
//  - **A widget's menu is one width whatever it says** (§230, Configures): Flyout.SettingsWidthDip, its
//    submenus too, a long label cut; a menu that doesn't configure (a jump list) keeps Windows' one width.
//  - **No rule on top**: a widget's settings where nothing stands over them begin with their heading; a
//    folder's faces have none of their own (§288) and begin with their first face.
//  - **On a real menu** (KARPO_UI_TESTS=1), pictures in KARPO_PNG_DIR at 150 %, dark:
//    menu-clean-toggles.png (the toggles shown, their submenu open), menu-clean-alarm.png (Repeat on
//    open), menu-clean-pin.png (a pin to a folder), menu-clean-face.png (a face's submenu with its
//    "Show the name beside it" whole) and menu-clean-faces.png (the faces alone, no rule on top).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class MenuCleanupTests
{
    /// <summary>A new widget whose options are <paramref name="kind"/>'s, a taskbar entry first.</summary>
    private static WidgetItem Of(WidgetOptionsKind kind) =>
        Enum.GetValues<BarKind>().Select(each => (WidgetItem)each.New()).Concat(Enum.GetValues<ListKind>().Select(each => each.New()))
            .First(item => item.Info?.Options == kind);

    /// <summary>A menu's rules on top go; the rest stay as they are.</summary>
    [Fact]
    public void AMenuNeverBeginsWithARule()
    {
        var section = OptionMenu.Section(OptionRows.Of(ListKind.Clock.New()), []);
        Assert.True(section[0].Separator);
        var shown = Flyout.WithoutLeadingRules(section);
        Assert.False(shown[0].Separator);
        Assert.Equal(section.Skip(1), shown);
        IReadOnlyList<FlyoutItem> plain = [new(1, "One"), new(0, "", Separator: true), new(2, "Two")];
        Assert.Same(plain, Flyout.WithoutLeadingRules(plain));
    }

    private static bool Save(Flyout menu, string name) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, ((byte)0, (byte)0, (byte)0));

    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 80 && menu.KeysOn != id; step++)
        {
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_DOWN, 0);
        }
        Assert.Equal(id, menu.KeysOn);
    }

    private static int Wide(Flyout menu, int panel = 0) => menu.OpenPanels[panel].Panel.right - menu.OpenPanels[panel].Panel.left;

    /// <summary>
    /// A widget's menu and its submenus are one width whatever their rows say (§230), a jump list keeps
    /// Windows'; the pictures of the tidied menus.
    /// </summary>
    [UiFact]
    public void AWidgetsMenuIsOneWidthWhateverItSaysAndThePicturesAreDrawn()
    {
        WindowShots.OnHiddenDesktop("menu-clean", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var look = AddListPictures.LookFor(dark: true);
            var work = new RECT { right = (int)(1920 * scale), bottom = (int)(1040 * scale) };
            var anchor = new POINT { x = work.right / 3, y = work.bottom };
            var one = (int)MathF.Round(Flyout.MenuWidthDip * scale);
            var settings = (int)MathF.Round(Flyout.SettingsWidthDip * scale);

            int Shown(IReadOnlyList<FlyoutItem> items, bool configures)
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = configures };
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                WindowShots.Settle();
                return Wide(menu);
            }
            List<FlyoutItem> named = [new(1, "Clock"), new(2, S.SettingsWidgetSwitchLabelHeader + " and a few more words", Control: new FlyoutControl.Toggle(true))];
            List<FlyoutItem> plain = [new(1, "Clock"), new(2, "Seconds", Control: new FlyoutControl.Toggle(true))];
            List<FlyoutItem> endless = [new(1, string.Concat(Enumerable.Repeat("A very long label ", 12)))];
            Assert.Equal(one, Shown(named, configures: false));
            Assert.Equal(settings, Shown(named, configures: true));
            Assert.Equal(settings, Shown(plain, configures: true));
            Assert.Equal(settings, Shown(endless, configures: true));

            var config = new Config();
            var place = new OptionPlace(config, Dark: true, Exists: path => path == @"C:\Work");
            List<FlyoutItem> Menu(WidgetItem item, List<OptionPick> picks) => [.. TheDock.WidgetButtons(), .. OptionMenu.Section(OptionRows.Of(item, null, place), picks)];

            // A menu, a row of it opened (its submenu's picture), as the dock shows a widget's.
            void Picture(List<FlyoutItem> items, string? open, string name, Action<Flyout>? check = null)
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Bottom, null);
                WindowShots.Settle();
                if (open is not null)
                {
                    KeysTo(menu, items.First(item => item.Text == open && item.Children is not null).Id);
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_RIGHT, 0);
                    WindowShots.Settle();
                    Assert.Equal(1, menu.InlineDepth);
                }
                check?.Invoke(menu);
                Assert.True(Save(menu, name));
                menu.Hide();
            }

            // A day flipped in the group's submenu: set, and the menu shown again with the submenu open (§219).
            var alarm = Of(WidgetOptionsKind.Alarm);
            alarm.Alarm = new AlarmOptions { Time = "07:30", On = true };
            List<OptionPick> dayPicks = [];
            Picture(Menu(alarm, dayPicks), S.DockEditRepeatOn, "menu-clean-alarm.png", menu =>
            {
                var lit = menu.HotId!.Value;
                var flipped = menu.Message(WM.WM_KEYDOWN, VK.VK_SPACE, 0);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, lit), flipped);
                var was = alarm.Alarm.Days;
                Assert.True(OptionMenu.Apply(alarm, OptionMenu.PickOf(dayPicks, flipped.Id)!, null, place));
                Assert.NotEqual(was, alarm.Alarm.Days);
                menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.Section(OptionRows.Of(alarm, null, place), [])));
                WindowShots.Settle();
                Assert.Equal(1, menu.InlineDepth);
                Assert.Equal(lit, menu.HotId);
                // Back as it was, for the picture.
                Assert.True(OptionMenu.Apply(alarm, OptionMenu.PickOf(dayPicks, flipped.Id)!, null, place));
                menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.Section(OptionRows.Of(alarm, null, place), [])));
                WindowShots.Settle();
            });
            // A toggles row's toggles: a list on the menu's own level, each with its grip (§228).
            Picture(Menu(Of(WidgetOptionsKind.Toggles), []), null, "menu-clean-toggles.png");
            var pin = ListKind.FolderContents.New();
            pin.Pin!.Target = @"C:\Work";
            Picture(Menu(pin, []), null, "menu-clean-pin.png");

            // An icon face alone on its button: its level in place the menu's one width, its icons six to a line still.
            var folder = BarKind.FolderList.New();
            folder.Folder!.Faces = [FolderFace.Of(FolderFaces.IconId)];
            folder.Folder.Face.Glyph = "";
            List<OptionPick> facePicks = [];
            List<FlyoutItem> faced = [.. TheDock.WidgetButtons(), .. OptionMenu.Section(OptionRows.Of(folder, null, place), facePicks), .. OptionMenu.FaceSection(folder.Folder, facePicks)];
            Picture(faced, TheDock.TitleOf(folder.Folder.Faces[0]), "menu-clean-face.png", menu => Assert.Equal(settings, Wide(menu)));
            // The faces alone: their first face first, no rule or heading over it (§288).
            Picture(OptionMenu.FaceSection(folder.Folder, []), null, "menu-clean-faces.png");
        });
    }
}
