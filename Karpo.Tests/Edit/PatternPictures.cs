// The patterns, seen (docs\widgets.md §462; the person, 2026-10-31: "you should be able to set a background pattern for the
// folder widgets and for the taskbar ... continuous between widgets inside the same group but not between groups of
// widgets"): each of ours in both modes at 100 % and 150 %, Graphite applied, on its Taskbar widgets' cards (a
// folder's face among them) and its folders' lines, on a real bar with a real folder standing open over it — two
// columns, the first split in two sections by a separator, so three sections side by side and one over another —, a line of it lit, and Layout ▾ › Look ›
// open at Widgets in folders' Pattern, as one picture (WindowShots.SaveTogether) to KARPO_PNG_DIR as
// pattern-<name>-<light|dark>-<100|150>.png; the same with the pattern on the Taskbar alone, the bar's one sheet,
// pattern-<name>-taskbar-<light|dark>-150.png; and each palette with Contours, look-<palette>-contours-<light|dark>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The sections whose cards stand in groups: the bar's widgets' (the folders' faces among them, §499) and the folders' lines.</summary>
    private static readonly LookPart[] CardParts = [LookPart.Widgets, LookPart.FolderWidgets];

    /// <summary>§517: the Appearance's Widgets in folders, its Pattern choice in it (§583 under its heading, no level).</summary>
    private static (List<FlyoutItem> Items, uint Level, uint? Deeper) PatternMenu(Config config, List<OptionPick> picks, bool dark)
    {
        var items = TheDock.LooksItems(config, new BarSpot("display", "One"), dark, picks, scope: LooksScope.Appearance);
        return (items, Karpo.Tests.Menus.MenuCorpus.Part(items, LookPart.FolderWidgets).First(item => item.Text == S.SettingsAppearanceStylesPattern).Id, null);
    }

    /// <summary>Two columns, the first split by a separator: three sections, two side by side, two one over the other.</summary>
    internal static void TwoColumns(FolderOptions folder)
    {
        foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.Divider, ListKind.EnergySaver })
        {
            _ = folder.Put(kind.New());
        }
        foreach (var kind in new[] { ListKind.Battery, ListKind.NightLight, ListKind.AirplaneMode })
        {
            _ = folder.Put(kind.New(), list: 1);
        }
    }

    /// <summary>§507: the default layout in palette <paramref name="palette"/> (Windows' own for none), <paramref name="pattern"/> on <paramref name="parts"/>.</summary>
    internal static void WearingPattern(Config config, string? palette, Pattern pattern, IEnumerable<LookPart> parts)
    {
        if (LookPalettes.Of(palette) is { } designed)
        {
            _ = LookPalettes.Wear(config, designed);
        }
        foreach (var part in parts)
        {
            Assert.True(Looks.WritePattern(Looks.Section(config, part), pattern));
        }
    }

    /// <summary>Every pattern of ours, in both modes, at 100 % and 150 %, on the bar's cards and in a folder of three sections; and on the bar alone.</summary>
    [UiFact]
    public void EveryPatternOnABarAndInAFolder()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("patterns", (gfx, instance) =>
        {
            foreach (var pattern in Patterns.All)
            {
                var name = pattern.ToString().ToLowerInvariant();
                foreach (var dark in new[] { false, true })
                {
                    var mode = dark ? "dark" : "light";
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var config = new Config { Height = 48 };
                        WearingPattern(config, LookPalettes.GraphiteId, pattern, CardParts);
                        ThemeSheet(gfx, instance, config, dark, $"pattern-{name}-{mode}-{scale * 100:0}",
                            failures, (config, picks) => PatternMenu(config, picks, dark), scale: scale, lines: TwoColumns);
                    }
                    var bar = new Config { Height = 48 };
                    WearingPattern(bar, LookPalettes.GraphiteId, pattern, [LookPart.Taskbar]);
                    ThemeSheet(gfx, instance, bar, dark, $"pattern-{name}-taskbar-{mode}-150",
                        failures, (config, picks) => PatternMenu(config, picks, dark), lines: TwoColumns);
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Each palette with Contours on its cards and its folders' lines, in both modes: the pattern in every palette's own ink.</summary>
    [UiFact]
    public void OursWearingAPattern()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("patterns-ours", (gfx, instance) =>
        {
            foreach (var ours in LookShorthand.Palettes)
            {
                foreach (var dark in new[] { false, true })
                {
                    var config = new Config { Height = 48 };
                    WearingPattern(config, ours, Pattern.Contours, CardParts);
                    ThemeSheet(gfx, instance, config, dark, $"look-{ours ?? "windows"}-contours-{(dark ? "dark" : "light")}",
                        failures, (config, picks) => PatternMenu(config, picks, dark), lines: TwoColumns);
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
