// keys: the keyboard's ring on a widget of the bar (§470), which the keys reach now as they reach the app buttons:
// keys-bar-<dark|light>-<100|150>.png in KARPO_PNG_DIR, the ring on the second widget along a bar of Start,
// search, task view and three widgets. Needs a desktop: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The look a picture of the bar in edit mode is drawn in: dark, light, or high contrast.</summary>
    private static Look MarkLook(string theme) => theme switch
    {
        "contrast" => AddListPictures.LookFor(true) with
        {
            HighContrast = true,
            HcWindow = Windows.UI.Color.FromArgb(255, 0, 0, 0),
            HcText = Windows.UI.Color.FromArgb(255, 255, 255, 255),
            HcHighlight = Windows.UI.Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
            HcHighlightText = Windows.UI.Color.FromArgb(255, 0, 0, 0),
            Animations = false,
        },
        _ => AddListPictures.LookFor(theme == "dark") with { Animations = false },
    };

    [UiFact]
    public void TheKeysRingAWidgetOnTheBarPictures()
    {
        WindowShots.OnHiddenDesktop("keys-bar", (gfx, _) =>
        {
            List<BarItem> bar = [BarKind.Start.New(), BarKind.Search.New(), BarKind.TaskView.New(), BarKind.NightLight.New(), BarKind.DarkMode.New(), BarKind.DoNotDisturb.New()];
            for (var at = 0; at < bar.Count; at++)
            {
                bar[at].Id = $"keys-{at}";
                bar[at].Section = BarSection.Middle;
            }
            foreach (var theme in new[] { "dark", "light" })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = MarkLook(theme);
                    var config = new Config { Height = 48 };
                    var area = new RECT { left = 0, top = 0, right = (int)(700 * dpi / 96), bottom = (int)(dpi * 10) };
                    var (specs, islands) = Made(gfx, config, bar);
                    try
                    {
                        var content = new Content { Islands = islands };
                        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                        var widgets = KeyStops.Of(layout, KeyRegion.Bar, 0).Where(hit => hit.Kind == HitKind.Island).ToList();
                        Assert.True(widgets.Count >= 3, $"{widgets.Count} widgets are stops");
                        Lit(gfx, layout, look, null, $"keys-bar-{theme}-{dpi * 100 / 96}.png", focus: widgets[1], content: content);
                    }
                    finally
                    {
                        foreach (var island in islands.Values)
                        {
                            island.Dispose();
                        }
                    }
                }
            }
        });
    }
}
