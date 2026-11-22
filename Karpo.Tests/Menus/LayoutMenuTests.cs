// edit: the settings panel's Display, Taskbar and Look pages' rows (docs\widgets.md §517, §520, Karpo.Core\BarLooks.cs;
// §400 to §513 one Layout ▾, §517 to §519 the edit bar's Display ▾, Taskbar ▾ and Look ▾). Held here:
//
//  - **Each one page under headings** in a fixed order, nothing standing still at its foot; the folder room's right click
//    Open folders and the way to Look.
//  - **Each setting in one of them alone**; Taskbar's Behavior the taskbar's behaviour and the folders' opening.
//  - **A level deep in a menu opens with its levels above** (Flyout.OpenLevel): where a way in lands —
//    a separator's "Taskbar separators ↗" on Appearance › Taskbar separators.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed class LayoutMenuTests
{
    private const string Display = "\\\\.\\DISPLAY1";

    private static Config Fresh(LayoutType type = LayoutType.Taskbar)
    {
        var config = new Config();
        config.EditScreen(Display, "One");
        Assert.True(type == LayoutType.Taskbar || config.ActiveLayout(Display).Become(type));
        return config;
    }

    private static BarSpot Spot() => new(Display, "One");

    private static string[] Texts(IEnumerable<FlyoutItem> items) =>
        [.. items.Select(item => item.Separator ? "" : item.Header ? "h:" + item.Text : item.Text)];

    /// <summary>
    /// §517 (the person, 2026-11-05: "1. sections"; the top bar "split ... into more logical groups like display settings,
    /// taskbar settings"): the edit bar's menus, each one page under headings in a fixed order, nothing standing still at a
    /// foot. Taskbar ▾: Bar — Type, Size, Spacing, Show, §534 Lock the taskbar —, Behavior — combine, previews, desktops, flyouts, folders. Look ▾:
    /// §577 Palette, its Reset to default, the two sections. §585 (§624) Layouts: Displays, each display an expander — Layout,
    /// Position —; Layouts, the layouts in order (§595 no Default layout row over them), Add a layout. The right click on the bar: Taskbar ▾'s rows and the ways to Look ▾ and Display ▾; the folder room's:
    /// Open folders and the way to Look ▾. A Dock's the same.
    /// </summary>
    [Fact]
    public void TheEditBarsMenusAreSectionsInAFixedOrder()
    {
        string[] taskbar =
        [
            "h:" + S.SettingsSectionsBar, S.SettingsLayoutsTypeHeader, S.SettingsLayoutsSizeHeader, S.SettingsLayoutsSpacingHeader, S.SettingsLayoutsShowHeader,
            // §534: Lock the taskbar, every bar's, under Bar.
            S.CoreBarMenuLockTaskbar,
            "h:" + S.DockTaskbarBehaviour, S.SettingsGeneralCombineHeader, S.SettingsGeneralPreviewsHeader, S.SettingsGeneralDesktopsHeader,
            S.SettingsGeneralHoverFlyoutsHeader,
            // §585: the taskbar menu, Karpo's one, Behavior's last row, an expander of its entries, Add an entry and Reset to
            // default (§553 to §584 a section of its own).
            S.SettingsBarMenuHeader,
        ];
        foreach (var type in LayoutTypes.All)
        {
            var config = Fresh(type);
            Assert.Equal(taskbar, Texts(TheDock.LooksItems(config, Spot(), dark: true, [], scope: LooksScope.Taskbar)));
            // §613 Mode, §592: Palette, its reset, then a level of each part, the panel's tabs; no heading.
            var appearance = TheDock.LooksItems(config, Spot(), dark: true, [], scope: LooksScope.Appearance);
            // §673: the palette's reset after the parts' tabs, at their strip's end.
            // §682: Animations after Palette.
            Assert.Equal([S.SettingsAppearanceMode, S.SettingsLookPalette, S.SettingsGeneralAnimationsHeader, .. LookParts.All.Select(LookParts.Name), S.SettingsLookReset], Texts(appearance));
            Assert.All(appearance.Skip(3).Take(LookParts.All.Length), tab => Assert.NotEmpty(tab.Children!));

            Assert.Equal(
                ["h:" + S.SettingsSectionsDisplays, "One", "h:" + S.CoreBarMenuLayouts, .. config.Layouts.Select(layout => layout.Name), "", S.SettingsLayoutsAdd],
                Texts(TheDock.LooksItems(config, Spot(), dark: true, [], scope: LooksScope.Layouts)));
            Assert.Equal([S.SettingsGeneralHoverFlyoutsHeader, "", S.SettingsAppearanceHeader], Texts(TheDock.LooksItems(config, Spot(), dark: true, [], scope: LooksScope.Folders)));
            // No forced foot.
            foreach (var scope in Enum.GetValues<LooksScope>())
            {
                Assert.DoesNotContain(TheDock.LooksItems(config, Spot(), dark: true, [], scope: scope), item => item.Foot);
            }
        }
    }

    /// <summary>
    /// §517: Taskbar ▾'s Behavior holds what it says — combine, previews, desktops, the widgets' flyouts and the folders on
    /// hover — and every setting of the three menus stands in one of them alone.
    /// </summary>
    [Fact]
    public void EachSettingHasOneOfTheMenusAlone()
    {
        var config = Fresh();
        var taskbar = BarLooks.Rows(config, Spot(), dark: true, scope: LooksScope.Taskbar);
        Assert.DoesNotContain(taskbar, row => row.Kind == OptionRowKind.Link);
        var behaviour = taskbar.SkipWhile(row => row.Key != BarLooks.BehaviourHeading).Skip(1).Select(row => row.Key);
        Assert.Equal([BarLooks.CombineRow, BarLooks.PreviewsRow, BarLooks.DesktopsRow, BarLooks.HoverRow], behaviour);
        IEnumerable<string> Keys(LooksScope scope) => OptionRows.Flat(BarLooks.Rows(config, Spot(), dark: true, scope: scope)).Where(row => row.Kind != OptionRowKind.Heading).Select(row => row.Key);
        var keys = new[] { LooksScope.Layouts, LooksScope.Taskbar, LooksScope.Appearance }.SelectMany(Keys).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    /// <summary>
    /// A level deep in a menu opened as a way in opens it (Dock.OpenShownLevel → Flyout.OpenLevel): the levels over it
    /// first, each as a click opens it, Back going up one at a time; nothing for no such row, nor from a level.
    /// </summary>
    [UiFact]
    public void ALevelDeepInTheMenuOpensWithItsLevelsAbove()
    {
        WindowShots.OnHiddenDesktop("layout-menu-deep", (gfx, instance) =>
        {
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            List<FlyoutItem> items =
            [
                new FlyoutItem(1, "Type"),
                new FlyoutItem(10, "Appearance", Children:
                [
                    new FlyoutItem(11, "Material"),
                    new FlyoutItem(20, "Taskbar separators", Children: [new FlyoutItem(21, "Style"), new FlyoutItem(22, "Width")]),
                ]),
            ];
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true, TypeTitle = "Layout" };
            menu.Show(gfx, look, 1f, items, FlyoutLayout.List, new POINT { x = 640, y = 720 }, new RECT { right = 1280, bottom = 720 }, Edge.Bottom, null);
            menu.OpenLevel(99);
            Assert.Equal(0, menu.InlineDepth);
            menu.OpenLevel(20);
            Assert.Equal((2, "Taskbar separators"), (menu.InlineDepth, menu.InlineTitle));
            Assert.Contains(menu.RowsShown, item => item.Id == 22);
            // On a level already: nothing.
            menu.OpenLevel(10);
            Assert.Equal(2, menu.InlineDepth);
            _ = menu.Message(WM.WM_KEYDOWN, VK.VK_ESCAPE, 0);
            Assert.Equal((1, "Appearance"), (menu.InlineDepth, menu.InlineTitle));
        });
    }
}
