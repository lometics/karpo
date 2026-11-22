// A bar holding the open apps twice and the tray twice (the person, 2026-10-09: "if i place the tray
// icons widget twice they only show once. (in the first one)", and the same of the apps), on the
// dock's real scene, shown on a desktop of its own and captured as the compositor draws it
// (WindowShots), to KARPO_PNG_DIR:
//   twice-over-<look>-<scale>.png       Start, the two Apps widgets, the two trays with their chevrons;
//   twice-over-lit-<look>-<scale>.png   the same, the second tray's second icon lit under the pointer;
// dark and light, at 100 % and 150 %. Every part is measured by the real Layout.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Start; the open apps twice in the middle; the tray twice, at the end.</summary>
    private static List<PartSpec> TwiceOverBar() =>
    [
        Spec("start", BarKind.Start),
        Spec("apps", BarKind.Apps) with { Section = BarSection.Middle },
        Spec("apps-2", BarKind.Apps) with { Section = BarSection.Middle },
        Spec("tray", BarKind.Tray) with { Section = BarSection.End },
        Spec("tray-2", BarKind.Tray) with { Section = BarSection.End },
    ];

    private static readonly string[] TwiceOverApps = ["\uE715", "\uE8A5", "\uE774"];

    /// <summary>The three open apps, a copy for each Apps widget, as Dock.TilesFor names them.</summary>
    private static Content TwiceOverContent(List<PartSpec> specs, List<TrayIcon> tray)
    {
        var owners = new[] { specs[1].Key, specs[2].Key };
        var tiles = owners.SelectMany(_ => TwiceOverApps.Select((glyph, index) => (new Item
        {
            Target = $@"C:\Karpo\apps\app{index}.exe",
            Name = $"App {index}",
            Kind = ItemKind.Shell,
            Keys = [],
            Picture = glyph,
        }, new Mark(index == 0 ? 2 : 1, Active: index == 1, Attention: false)))).ToList();
        return new Content
        {
            Tiles = tiles,
            TileParts = [.. owners.SelectMany(owner => TwiceOverApps.Select(_ => owner))],
            Tray = tray,
            TrayOverflow = true,
        };
    }

    [UiFact]
    public void TwiceOverSheets()
    {
        WindowShots.OnHiddenDesktop("twice-over", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var area = new RECT { left = 0, top = 0, right = (int)(1000 * dpi / 96), bottom = (int)(dpi * 10) };
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var specs = TwiceOverBar();
                    var icons = DraggedTrayIcons(3, (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config)));
                    var content = TwiceOverContent(specs, icons);
                    var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);

                    var (apps, apps2) = (layout.PartIndex(specs[1].Key), layout.PartIndex(specs[2].Key));
                    var trays = layout.TrayParts;
                    Assert.Equal(3, layout.PartAt(apps).Count);
                    Assert.Equal(3, layout.PartAt(apps2).Count);
                    Assert.Equal(2, trays.Count);
                    Assert.All(trays, part => Assert.Equal(3, layout.PartAt(part).Count));
                    Assert.Empty(layout.Overflowed);

                    TrayShot(gfx, layout, content, look, scene =>
                    {
                        // Every button and every icon drawn in its own widget.
                        foreach (var (part, owner) in new[] { (apps, specs[1].Key), (apps2, specs[2].Key) })
                        {
                            for (var index = 0; index < TwiceOverApps.Length; index++)
                            {
                                Assert.NotNull(scene.Registered($@"tile:{owner}:C:\Karpo\apps\app{index}.exe#0"));
                            }
                        }
                        foreach (var part in trays)
                        {
                            Assert.All(icons, icon => Assert.NotNull(scene.Registered($"tray:{layout.Specs[part].Key}|{icon.Key}")));
                            Assert.NotNull(scene.Registered($"tray:{layout.Specs[part].Key}|chevron"));
                        }
                    }, $"twice-over-{name}.png");
                    TrayShot(gfx, layout, content, look, scene => scene.Highlight(Hit.Tray(1).In(trays[1]), false), $"twice-over-lit-{name}.png");
                }
            }
        });
    }

    /// <summary>
    /// §347, the person: "scope these settings to the widget even". The two trays of one bar, each its
    /// widget's own (Content.Trays): the first all three icons and no chevron, the second the last icon
    /// alone and its chevron for the two it keeps behind it — drawn so, and repainted in place each its
    /// own (Scene.SetTray). twice-own-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void TwoTraysDrawTheirOwnIcons()
    {
        WindowShots.OnHiddenDesktop("twice-own", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var area = new RECT { left = 0, top = 0, right = (int)(1000 * dpi / 96), bottom = (int)(dpi * 10) };
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var specs = TwiceOverBar();
                    var icons = DraggedTrayIcons(3, (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config)));
                    var (first, second) = (specs[3].Key, specs[4].Key);
                    var content = new Content
                    {
                        Trays = new Dictionary<string, BarTray>
                        {
                            [first] = new(icons, Overflow: false),
                            [second] = new([icons[2]], Overflow: true),
                        },
                    };
                    var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                    var (one, two) = (layout.PartIndex(first), layout.PartIndex(second));

                    Assert.Equal(3, layout.PartAt(one).Count);
                    Assert.Equal(0f, layout.TrayChevronOf(one));
                    Assert.Equal(1, layout.PartAt(two).Count);
                    Assert.True(layout.TrayChevronOf(two) > 0f);

                    TrayShot(gfx, layout, content, look, scene =>
                    {
                        Assert.All(icons, icon => Assert.NotNull(scene.Registered($"tray:{first}|{icon.Key}")));
                        Assert.Null(scene.Registered($"tray:{first}|chevron"));
                        Assert.NotNull(scene.Registered($"tray:{second}|{icons[2].Key}"));
                        Assert.Null(scene.Registered($"tray:{second}|{icons[0].Key}"));
                        Assert.Null(scene.Registered($"tray:{second}|{icons[1].Key}"));
                        Assert.NotNull(scene.Registered($"tray:{second}|chevron"));
                        // New pictures, each tray its own list, painted in place.
                        scene.SetTray(gfx, content.TrayOf);
                    }, $"twice-own-{name}.png");
                }
            }
        });
    }
}
