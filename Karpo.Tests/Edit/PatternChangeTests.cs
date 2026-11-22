// §560 (the person, 2026-11-08: "when changing the pattern of a widget it does not apply immediately"): the look changed
// while it shows is on screen at once — its pattern, and its outline with it —: a widget's card on the bar as the scene is
// updated in the new look, a line of a folder standing open as the folder takes the new look (FolderFlyout.Relook), with
// nothing else changed that would lay the folder out again. §567: the Appearance's, no widget's own.
//
// Needs a compositor and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>§560: the Taskbar widgets' pattern changed: the bar updated shows the new one on its card, at rest and arranged.</summary>
    [UiFact]
    public void AWidgetsPatternChangedShowsOnItsCardAtOnce()
    {
        foreach (var editing in new[] { true, false })
        {
            var config = new Config { Height = 48 };
            var area = new RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
            var card = $"card:{Config.IslandKey(null, "a")}";
            Layout LaidOut() => Layout.ForTests(96, area, Edge.Bottom, config,
                [Spec("a", BarKind.NightLight), Spec("b", BarKind.EnergySaver)],
                Layout.CellPx(96, config), editing: editing);
            Look Wearing(Pattern pattern)
            {
                _ = Looks.WritePattern(Looks.Section(config, LookPart.Widgets), pattern);
                return (Dressed with { Dark = false }).Wearing(config);
            }
            OnItsOwnThread(gfx =>
            {
                var window = MakeWindow();
                Scene? scene = null;
                try
                {
                    scene = Scenes.Build(gfx, Target(gfx, window), LaidOut(), new Content(), Wearing(Pattern.Dots), new Icons(), shown: true, edit: editing ? new EditView() : null);
                    Assert.Equal(Pattern.Dots, scene.PatternOf(card)?.Showing?.Pattern.Pattern);
                    scene.Update(LaidOut(), new Content(), Wearing(Pattern.Grid), new Icons(), shown: true, editing ? new EditView() : null);
                    Assert.Equal(Pattern.Grid, scene.PatternOf(card)?.Showing?.Pattern.Pattern);
                }
                finally
                {
                    scene?.Dispose();
                    _ = DestroyWindow(window);
                }
            });
        }
    }

    /// <summary>
    /// §560: the Widgets in folders' pattern changed while a folder stands open, nothing else: the folder takes the
    /// new look, its plan the same, and its line wears its pattern at once; §624 the lines have no outline to set, and none is
    /// drawn round them.
    /// </summary>
    [UiFact]
    public void ALinesPatternChangedWhileItsFolderStandsOpenShowsAtOnceNeverAnOutline()
    {
        WindowShots.OnHiddenDesktop("line-look-change", (gfx, instance) =>
        {
            var config = new Config { Height = 48 };
            var folder = BarKind.FolderList.New();
            folder.Id = "changed";
            TwoColumns(folder.Folder!);
            config.DefaultLayout.Islands = [folder];
            var look = (TestLooks.LookFor(dark: true) with { Animations = false }).Wearing(config);
            using var open = new FolderIsland("layout:default/changed", folder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            flyout.Show(gfx, look, 1.5f, new POINT { x = 700, y = 1200 }, new RECT { right = 2400, bottom = 1200 }, Edge.Bottom, open);
            WindowShots.Settle();
            Assert.Null(flyout.LinePatterns[0].Pattern.Showing);
            var plan = flyout.Plan;

            var slot = Looks.Section(config, LookPart.FolderWidgets);
            Assert.True(Looks.WritePattern(slot, Pattern.Dots));
            Assert.False(LookShorthand.Both(slot, Tone.Outline, Karpo.Core.Paint.Parse("#E74856")));
            flyout.Relook((TestLooks.LookFor(dark: true) with { Animations = false }).Wearing(config));
            WindowShots.Settle();

            Assert.Same(plan, flyout.Plan);
            Assert.Equal(Pattern.Dots, flyout.LinePatterns[0].Pattern.Showing?.Pattern.Pattern);
            flyout.Hide();
        });
    }
}
