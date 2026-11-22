// Every colour picked reaches every surface that wears it, live (docs\widgets.md §396, §405, §459; the person, 2026-10-29:
// "flyout background does not properly update"): a real open folder and a real open menu, and the bar's tokens, each part's
// colours of the style the layout wears (§464) changed in turn and the look worn again as the dock wears it (Look.Wearing) — the folder
// dressed again where it stands (FolderFlyout.Relook), the menu shown again in it (Flyout.Refresh, as Dock.MenuAgain does)
// — and each surface asked what it draws now: what its parts dress it in (ToneRules.Dress). To KARPO_PNG_DIR as
// theme-live-<folder|menu>-<look>.png, the folder and the menu with all set.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Reflection;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// §405, §459, §464: each part's colours of the style the layout wears, set while a folder and a menu stand open, and another
    /// style picked, are what every surface that wears that part draws at once: the bar's tokens (its Taskbar, its widgets'
    /// cards), the open folder's lines and rim — no panel — (its Widgets in folders, its Flyouts' outline), the open menu's
    /// panel, text and rim (the Widgets in folders' over Windows' flyout, their outline, §512), the widgets' ink and accent
    /// (§524: a divider is striped in the folder's text, no colour of its own). Nothing is opened again.
    /// </summary>
    [UiFact]
    public void EveryColourPickedReachesItsSurfacesLive()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("theme-live", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var mode = dark ? "dark" : "light";
                var config = LookShorthand.Wearing(taskbarOpacity: 100);
                var windows = TestLooks.LookFor(dark) with { Animations = false };
                Look Worn() => windows.Wearing(config);

                var island = BarKind.FolderList.New();
                island.Id = "live";
                foreach (var kind in new[] { ListKind.EnergySaver, ListKind.NightLight, ListKind.Divider, ListKind.AirplaneMode, ListKind.Battery })
                {
                    island.Folder!.Put(kind.New());
                }
                var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config);
                using var flyout = new FolderFlyout(gfx, instance, 0);
                using var menu = new Flyout(gfx, instance, 0) { Configures = true };
                try
                {
                    var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
                    flyout.Show(gfx, Worn(), 1.5f, new POINT { x = 1700, y = 1500 }, work, Edge.Bottom, folder, FlyoutAlign.End);
                    List<FlyoutItem> rows = [new FlyoutItem(1, "Background"), new FlyoutItem(2, "Accent color", Value: new FlyoutValue("Inherited")), new FlyoutItem(3, "Text color")];
                    menu.Show(gfx, Worn(), 1.5f, rows, FlyoutLayout.List, new POINT { x = 500, y = 1500 }, work, Edge.Bottom, null);
                    WindowShots.Settle(200);
                    var host = (FlyoutHost)typeof(FolderFlyout).GetField("host", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                    var washes = (List<CompositionColorBrush>)typeof(FolderFlyout).GetField("washes", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                    var rims = (List<CompositionColorBrush>)typeof(FolderFlyout).GetField("rims", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;

                    // §507, §509: the default layout's sections, one value each, the same in both modes.
                    void Set(LookPart part, Tone tone, string colour) =>
                        Assert.True(LookShorthand.Both(Looks.Section(config, part), tone, new Karpo.Core.Paint(A(colour))));
                    (string Step, Action Pick)[] steps =
                    [
                        ("taskbar", () => Set(LookPart.Taskbar, Tone.Background, "#FF5E1F1F")),
                        ("lines", () =>
                        {
                            Set(LookPart.FolderWidgets, Tone.Background, "#B3693434");
                            Set(LookPart.FolderWidgets, Tone.Text, "#FFFFB900");
                        }),
                        ("widgets", () =>
                        {
                            Set(LookPart.Widgets, Tone.Background, "#B3693434");
                            Set(LookPart.Widgets, Tone.Accent, "#FF00CC6A");
                        }),
                        ("outlines", () =>
                        {
                            Set(LookPart.Flyouts, Tone.Outline, "#66FFB900");
                            Set(LookPart.Widgets, Tone.Outline, "#52FFB900");
                        }),
                        // A palette applied to the layout.
                        ("picked", () => Assert.True(LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.HarborId)!))),
                    ];
                    foreach (var (step, pick) in steps)
                    {
                        pick();
                        var look = Worn();
                        flyout.Relook(look);
                        menu.Refresh(rows, look: look);
                        var paints = Looks.Of(config, dark: true);
                        // §512: the menus the Widgets in folders' background; §624 the menus and an open folder the Flyouts' outline (§514).
                        var menus = new PartPaints(paints.FolderWidgets.Background, paints.Flyouts.Outline);
                        var folderRim = menus;
                        var (onBar, _) = ToneRules.Dress(new Dressing(paints.Taskbar, paints.Widgets, menus, paints.FolderWidgets), windows.Dark);
                        var (inFolder, _) = ToneRules.Dress(new Dressing(default, paints.FolderWidgets, folderRim, paints.FolderWidgets), windows.Dark);
                        var (inMenu, _) = ToneRules.Dress(new Dressing(default, paints.FolderWidgets, menus, paints.FolderWidgets), windows.Dark);
                        var bar = Scene.Theme(look, 1f, 1f);
                        void Expect(bool held, string what)
                        {
                            if (!held)
                            {
                                failures.Add($"{mode}, {step}: {what}");
                            }
                        }
                        static Color Solid(Argb color) => Color.FromArgb(255, color.R, color.G, color.B);
                        if (onBar.Taskbar is { } ground)
                        {
                            Expect(bar.Base == Solid(ground), $"the bar is {bar.Base}");
                        }
                        // §464: the folder no panel; the menus the lines' background over Windows' own flyout.
                        Expect(host.Theme.Bare, "the folder draws a panel");
                        if (inFolder.Flyout is { } surface)
                        {
                            var own = FlyoutTheme.For(look with { Colors = look.Colors with { Flyout = null } }).Base;
                            var over = ToneRules.Blend(surface, new Argb(255, own.R, own.G, own.B));
                            Expect(menu.Theme.Base == Solid(over), $"the menu's panel is {menu.Theme.Base}, not {Solid(over)}");
                        }
                        // §546: without transparency effects (the tests' look) a see-through card at its opacity all the same.
                        if (onBar.Card is { } card)
                        {
                            Expect(bar.CardFill == Ink(card), $"the bar's cards are {bar.CardFill}");
                        }
                        if (inFolder.Card is { } line)
                        {
                            Expect(washes.Count(wash => wash.Color == Ink(line)) == 4, $"the folder's lines are {string.Join(", ", washes.Select(wash => wash.Color))}");
                        }
                        if (onBar.Text is { } ink)
                        {
                            Expect(bar.Text == Ink(ink), $"the bar's text is {bar.Text}");
                            Expect(new IslandInk(look).Text.Equals(FlyoutTheme.Ink(Ink(ink))), $"a widget's text is {new IslandInk(look).Text}");
                        }
                        if (onBar.PanelText is { } said)
                        {
                            Expect(menu.Theme.Text.Equals(FlyoutTheme.Ink(Ink(said))), $"the menu's text is {menu.Theme.Text}");
                        }
                        // §464: a folder's own look: no ground under it, its menus' text read on Windows' flyout of Windows' mode.
                        if (inFolder.PanelText is { } inside)
                        {
                            Expect(host.Theme.Text.Equals(FlyoutTheme.Ink(Ink(inside))), $"the folder's text is {host.Theme.Text}");
                            Expect(host.Theme.SubtleHover == Ink(ToneRules.HighlightOf(inside)), $"the folder lights {host.Theme.SubtleHover}");
                        }
                        if (onBar.Accent is { } accent)
                        {
                            Expect(bar.Active == Solid(accent), $"the bar's accent is {bar.Active}");
                            Expect(menu.Theme.Accent == Solid(accent), $"the menu's accent is {menu.Theme.Accent}");
                            Expect(new IslandInk(look).Accent.Equals(FlyoutTheme.Ink(Solid(accent))), $"a widget's accent is {new IslandInk(look).Accent}");
                        }
                        if (inFolder.FlyoutOutline is { } rim)
                        {
                            Expect(host.Theme.Stroke == Ink(rim), $"the folder's rim is {host.Theme.Stroke}");
                        }
                        if (inMenu.FlyoutOutline is { } menuRim)
                        {
                            Expect(menu.Theme.Stroke == Ink(menuRim), $"the menu's rim is {menu.Theme.Stroke}, not {Ink(menuRim)}");
                        }
                        if (onBar.CardOutline is { } outline)
                        {
                            Expect(bar.CardStroke == Ink(outline), $"the bar's outlines are {bar.CardStroke}");
                        }
                        // §624: a folder's lines never ringed: its border is the folder's.
                        Expect(inFolder.CardOutline is null, $"the folder's lines are ringed {inFolder.CardOutline}");
                        Expect(rims.All(each => each.Color.A == 0), $"the folder's lines are ringed {string.Join(", ", rims.Select(each => each.Color))}");
                    }
                    WindowShots.Settle(250);
                    Assert.True(WindowShots.Save(flyout.Hwnd, $"theme-live-folder-{mode}-150.png"));
                    Assert.True(WindowShots.Save(menu.Hwnd, $"theme-live-menu-{mode}-150.png"));
                    flyout.Hide();
                    menu.Hide();
                }
                finally
                {
                    folder.Dispose();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
