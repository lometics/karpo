// §506: on a bar whose style draws no card, every widget and folder button wears a soft plate at rest (its text's colour
// at Scene.RestPlateAlpha, over the bar as the highlight is), its own all the way round; the pointer, a press and an open
// folder draw that one plate stronger rather than laying a second surface over it. A style with a card of its own wears
// its card and no plate; an arranged bar wears edit mode's wash instead. The dock's real scene at 150 %, captured as the
// compositor draws it (WindowShots), to KARPO_PNG_DIR as rest-plate-<state>-<look>.png, and the person's bar's end as
// rest-plates-<100|150>[-hover|-open].png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Folders;
using Karpo.Tests.Forecasts;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>How strong a plate at rest is, in a colour's alpha byte (Scene.RestPlateAlpha).</summary>
    private static readonly byte RestAlpha = (byte)MathF.Round(Scene.RestPlateAlpha * 255f, MidpointRounding.AwayFromZero);

    /// <summary>
    /// A widget whose style draws no card wears a plate at rest, the card's rectangle in its corners, and no hole is cut in
    /// the bar for it; under the pointer, pressed and with its folder open it is that plate drawn stronger — the highlight
    /// lays nothing over it and the open plate only its edge — and its neighbour's stays at rest. Arranged, there is none:
    /// edit mode's wash is the block's surface.
    /// </summary>
    [UiFact]
    public void AWidgetWithNoCardWearsAPlateThatLightsStronger()
    {
        WindowShots.OnHiddenDesktop("rest-plate", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var name = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with
                {
                    Animations = false,
                    Material = Material.Opaque,
                    Colors = new Palette(Taskbar: A(dark ? "#FF101010" : "#FFEEEEEE")),
                };
                WithLookBar(gfx, dark, (layout, content, parts) =>
                {
                    var (one, other) = (parts[1], parts[2]);
                    var hit = Hit.Cell(one, 0);
                    // In the room after the first widget: the bar.
                    var gap = layout.CardRect(parts[0]);
                    var bare = new POINT { x = (int)gap.Right + 3, y = (int)(gap.Y + (gap.Height / 2)) };
                    byte restA = 0;
                    int restApart = 0;
                    ShootLooks(gfx, layout, content, look, $"rest-plate-rest-{name}.png", scene =>
                    {
                        Assert.False(scene.Cuts, $"{name}: a hole is cut in the bar for a plate");
                        foreach (var part in parts)
                        {
                            var plate = scene.RestPlateOf(part) ?? throw new Xunit.Sdk.XunitException($"{name}: {layout.Parts[part].Kind} wears no plate");
                            Assert.Equal(layout.CardRect(part), plate.Rect);
                            Assert.Equal(RestAlpha, plate.Fill.A);
                        }
                        restA = scene.RestPlateOf(one)!.Value.Fill.A;
                    }, pixel => restApart = Apart(pixel(CardInside(layout, one)), pixel(bare)));
                    Assert.True(restApart > 0, $"{name}: the plate does not show over the bar");

                    foreach (var (state, pressed) in new[] { ("hover", false), ("pressed", true) })
                    {
                        byte litA = 0;
                        ShootLooks(gfx, layout, content, look, $"rest-plate-{state}-{name}.png", scene =>
                        {
                            scene.Highlight(hit, pressed);
                            Assert.Equal(0, scene.LitColor.A);
                            litA = scene.RestPlateOf(one)!.Value.Fill.A;
                            Assert.Equal(restA, scene.RestPlateOf(other)!.Value.Fill.A);
                            scene.Highlight(null, false);
                            Assert.Equal(restA, scene.RestPlateOf(one)!.Value.Fill.A);
                            scene.Highlight(hit, pressed);
                        }, pixel => Assert.True(Apart(pixel(CardInside(layout, one)), pixel(bare)) > restApart, $"{name}, {state}: the plate is no stronger lit"));
                        Assert.True(litA > restA, $"{name}, {state}: the plate is {litA} lit, {restA} at rest");
                    }

                    ShootLooks(gfx, layout, content, look, $"rest-plate-open-{name}.png", scene =>
                    {
                        scene.Open(layout.Specs[one].Key);
                        var open = scene.RestPlateOf(one)!.Value.Fill.A;
                        Assert.True(open > restA, $"{name}: open, the plate is {open}, at rest {restA}");
                        Assert.Equal(0, scene.OpenShown?.Fill.A ?? 0);
                        scene.Highlight(hit, false);
                        Assert.True(scene.RestPlateOf(one)!.Value.Fill.A > open, $"{name}: open and lit, the plate is no stronger than open");
                        Assert.Equal(0, scene.LitColor.A);
                    }, _ => { });
                });

                WithLookBar(gfx, dark, (layout, content, parts) =>
                    ShootLooks(gfx, layout, content, look, $"rest-plate-edit-{name}.png", scene =>
                    {
                        foreach (var part in parts)
                        {
                            Assert.Null(scene.RestPlateOf(part));
                        }
                    }, _ => { }), editing: true);
            }
        });
    }

    /// <summary>A style that sets a card colour wears its card, as ever, and no plate: the style's colours win (§506).</summary>
    [UiFact]
    public void AStylesCardWinsOverThePlate()
    {
        WindowShots.OnHiddenDesktop("rest-plate-card", (gfx, _) =>
        {
            var look = AddListPictures.LookFor(dark: true) with
            {
                Animations = false,
                Material = Material.Opaque,
                Colors = new Palette(Taskbar: A("#FF101010"), Card: A("#FF444444")),
            };
            WithLookBar(gfx, dark: true, (layout, content, parts) =>
                ShootLooks(gfx, layout, content, look, "rest-plate-card-dark.png", scene =>
                {
                    for (var part = 0; part < layout.Parts.Count; part++)
                    {
                        Assert.Null(scene.RestPlateOf(part));
                    }
                    scene.Highlight(Hit.Cell(parts[1], 0), false);
                    Assert.NotEqual(0, scene.LitColor.A);
                }, _ => { }));
        });
    }

    /// <summary>The person's bar's end as their config.json had it (2026-11-05), the folders' lines left out (they never size a face), its look's styles gone.</summary>
    private const string PersonsEnd = """
        {
          "layouts": [ { "id": "default", "name": "Default", "type": "taskbar", "size": 42, "islands": [
            { "id": "ca6ae2a1", "kind": "tray", "section": "end" },
            { "id": "a9fb7102", "kind": "indicators", "section": "end", "statusIcons": { "states": { "doNotDisturb": "Hidden" } } },
            { "id": "f8260326", "kind": "folderList", "section": "end", "folder": { "face": { "kind": "icon", "options": {} }, "lists": [ [] ] } },
            { "id": "77da4d1f", "kind": "folderList", "section": "end", "folder": { "face": { "kind": "icon", "options": {}, "glyph": "" }, "lists": [ [] ] } },
            { "id": "8a8a99d1", "kind": "folderList", "section": "end", "folder": { "faces": [
              { "kind": "weather", "options": {}, "weather": { "place": "", "latitude": null, "longitude": null, "useDeviceLocation": true, "isSet": true } },
              { "kind": "airQuality", "options": { "width": "compact" }, "weather": { "place": "", "latitude": null, "longitude": null, "useDeviceLocation": true, "isSet": true } } ],
              "lists": [ [] ] } },
            { "id": "1563f251", "kind": "folderList", "section": "end", "folder": { "faces": [ { "kind": "wifi", "options": {} }, { "kind": "sound", "options": {} } ], "lists": [ [] ] } },
            { "id": "2d5256f7", "kind": "folderList", "section": "end", "folder": { "faces": [ { "kind": "dateTime", "options": { "seconds": "windows" } }, { "kind": "notifications", "options": { "count": false } } ], "lists": [ [] ] } }
          ] } ],
          "defaultLayoutId": "default"
        }
        """;

    /// <summary>
    /// The person's bar's end, its widgets on no card as their Follow Windows style had them (2026-11-05: its "appearance" no
    /// longer reads, and since §547 Karpo's own of each mode is a solid card, which wears no plate — so the look is given no
    /// card, as <see cref="AWidgetWithNoCardWearsAPlateThatLightsStronger"/>'s), over a desktop in the dark, at 100 and 150 %: every
    /// folder's button its plate; the rhythm faces on one button no further apart than buttons, buttons far closer than sections; at 150 % with
    /// the weather's folder under the pointer and open too.
    /// </summary>
    [UiFact]
    public void ThePersonsBarWearsRestPlates()
    {
        WindowShots.OnHiddenDesktop("rest-plates", (gfx, _) =>
        {
            Clock.Now = () => new DateTime(2026, 11, 5, 9, 28, 0);
            WindowsClock.SecondsStand = false;
            IndicatorSources.Made = () => new SampleIndicators(new IndicatorState());
            FolderIsland.WeatherStand = SavedWeather.Cache();
            FolderIsland.AirStand = SavedAir.Cache(30, 30);
            FolderIsland.DevicesStand = [];
            VolumeWatch.Stand = new SampleVolume { Level = 60 };
            try
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var config = Config.Parse(PersonsEnd);
                    var layoutOptions = config.Layouts[0];
                    config.Height = layoutOptions.Size;
                    var bar = layoutOptions.Islands;
                    gfx.Typeface = Looks.MenuText(config);
                    var look = (TestLooks.LookFor(dark: true) with { Animations = false }).Wearing(config);
                    look = look with { Colors = look.Colors with { Card = null } };
                    var area = new RECT { left = 0, top = 0, right = (int)(900 * dpi / 96), bottom = (int)(dpi * 10) };
                    var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                    var (specs, islands) = Made(gfx, config, bar);
                    try
                    {
                        var content = new Content { Islands = islands, Tray = DraggedTrayIcons(2, px) };
                        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                        var folders = Enumerable.Range(0, specs.Count).Where(part => specs[part].Kind == BarKind.FolderList).ToList();
                        Assert.Equal(5, folders.Count);
                        var fit = new Fit(layout.StyleFor(folders[0], default));
                        // The sections' and the widgets' gaps scale with the icon, up to their 14 and 6 DIP: on this 42 DIP bar a
                        // widget's comes to about the faces' 5 (8 px at 150 %), never under it.
                        Assert.True(FolderIsland.Between(fit) <= layout.WidgetGap && 2 * layout.WidgetGap < layout.SectionGap,
                            $"faces {FolderIsland.Between(fit)}, buttons {layout.WidgetGap}, sections {layout.SectionGap} px apart");
                        for (var next = 1; next < folders.Count; next++)
                        {
                            Assert.Equal(layout.WidgetGap, layout.CardRect(folders[next]).X - layout.CardRect(folders[next - 1]).Right);
                        }
                        var from = layout.Width - (int)(560 * dpi / 96f);
                        var name = $"rest-plates-{dpi * 100 / 96}";
                        OverADesktop(gfx, layout, look, content, null, $"{name}.png", from, pose: scene =>
                        {
                            foreach (var part in folders)
                            {
                                Assert.Equal(RestAlpha, (scene.RestPlateOf(part) ?? throw new Xunit.Sdk.XunitException($"{specs[part].Key} wears no plate")).Fill.A);
                            }
                        });
                        if (dpi == 96)
                        {
                            continue;
                        }
                        var weather = specs.FindIndex(spec => spec.Key.EndsWith("/8a8a99d1", StringComparison.Ordinal));
                        var at = layout.BlockRect(weather).Center;
                        var hit = layout.HitTest(at.X, at.Y) ?? throw new Xunit.Sdk.XunitException("nothing under the weather");
                        OverADesktop(gfx, layout, look, content, null, $"{name}-hover.png", from, pose: scene => scene.Highlight(hit, false));
                        OverADesktop(gfx, layout, look, content, null, $"{name}-open.png", from, pose: scene => scene.Open(specs[weather].Key));
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
            finally
            {
                VolumeWatch.Stand = null;
                FolderIsland.DevicesStand = null;
                FolderIsland.AirStand = null;
                FolderIsland.WeatherStand = null;
                IndicatorSources.Made = null;
                WindowsClock.SecondsStand = null;
                Clock.Now = Clock.Real;
                gfx.Typeface = default;
            }
        });
    }
}
