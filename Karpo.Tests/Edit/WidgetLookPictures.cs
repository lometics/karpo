// The bar's cards and outlines (docs\widgets.md §311, §325, §376, §405): the dock's real scene at 150 % in colours
// of the bar's, captured as the compositor draws it (WindowShots), to KARPO_PNG_DIR. §567: no widget has a look of its own.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// §515: every card on the bar stands on whole px, its outline crisp — each side's outer px row its ink whole, the px past it
    /// the bar's — whatever is measured before it in its section (a clock's words, a folder's faces) and wherever its section
    /// stands (the middle centred, the end from the bar's end), at 125, 150 and 175 %. widget-look-crisp-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void EveryCardOutlineLandsOnWholePixels()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("widget-look-crisp", (gfx, _) =>
        {
            var bar = new Palette(Taskbar: A("#FF101010"), Card: A("#FF444444"), CardOutline: A("#FFFFFFFF"));
            var (ground, ink) = (bar.Taskbar!.Value, bar.CardOutline!.Value);
            var look = AddListPictures.LookFor(true) with { Animations = false, Material = Material.Opaque, Colors = bar };
            foreach (var dpi in new uint[] { 120, 144, 168 })
            {
                WithLookBar(gfx, true, (layout, content, _) =>
                    ShootLooks(gfx, layout, content, look, $"widget-look-crisp-{dpi * 100 / 96}.png", _ => { }, pixel =>
                    {
                        for (var part = 0; part < layout.Parts.Count; part++)
                        {
                            if (layout.Specs[part].Bare || layout.SearchBoxed(part) || layout.Plate(part) is not { IsEmpty: false } plate)
                            {
                                continue;
                            }
                            var (midX, midY) = ((int)(plate.X + (plate.Width / 2)), (int)(plate.Y + (plate.Height / 2)));
                            var (left, top, right, bottom) = ((int)MathF.Floor(plate.X), (int)MathF.Floor(plate.Y), (int)MathF.Ceiling(plate.Right) - 1, (int)MathF.Ceiling(plate.Bottom) - 1);
                            foreach (var (side, on, off) in new[]
                            {
                                ("top", new POINT { x = midX, y = top }, new POINT { x = midX, y = top - 1 }),
                                ("bottom", new POINT { x = midX, y = bottom }, new POINT { x = midX, y = bottom + 1 }),
                                ("left", new POINT { x = left, y = midY }, new POINT { x = left - 1, y = midY }),
                                ("right", new POINT { x = right, y = midY }, new POINT { x = right + 1, y = midY }),
                            })
                            {
                                if (Apart(pixel(on), ink) > 24 || Apart(pixel(off), ground) > 24)
                                {
                                    failures.Add($"{dpi * 100 / 96} %: {layout.Parts[part].Kind} at {plate}: its {side} edge is {pixel(on)}, past it {pixel(off)}");
                                }
                            }
                        }
                    }), dpi: dpi);
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// A card outline runs all the way round what it is the outline of (the person, 2026-10-23: "card
    /// outline just changes color of top and bottom line and not all the way around"): round every card (§512), and none
    /// across the widget's gap between two.
    /// widget-look-outlines-&lt;look&gt;.png.
    /// </summary>
    [UiFact]
    public void ACardOutlineRunsAllTheWayRound()
    {
        WindowShots.OnHiddenDesktop("widget-look-outlines", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var bar = dark
                    ? new Palette(Taskbar: A("#FF101010"), Card: A("#FF444444"), CardOutline: A("#FF9A9A9A"))
                    : new Palette(Taskbar: A("#FFEEEEEE"), Card: A("#FFFFFFFF"), CardOutline: A("#FF777777"));
                var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque, Colors = bar };
                WithLookBar(gfx, dark, (layout, content, parts) =>
                {
                    var start = Enumerable.Range(0, layout.Parts.Count).First(part => layout.Parts[part].Kind == BarKind.Start);
                    var taskView = Enumerable.Range(0, layout.Parts.Count).First(part => layout.Parts[part].Kind == BarKind.TaskView && layout.Placed(part));
                    ShootLooks(gfx, layout, content, look, $"widget-look-outlines-{(dark ? "dark" : "light")}.png", _ => { }, pixel =>
                    {
                        // The outline is smoothed over two pixels, so a side is one where it is at least a third of the way from the card to its ink.
                        Argb Rim(int part, string side, Argb ink) => RimOf(layout, pixel, part, side, ink);
                        // §512: every card its own, outlined all round.
                        foreach (var part in new[] { parts[0], parts[1], parts[2], taskView, start })
                        {
                            var ink = bar.CardOutline!.Value;
                            foreach (var side in new[] { "top", "bottom", "left", "right" })
                            {
                                var rim = Rim(part, side, ink);
                                Assert.True(3 * Apart(rim, bar.Card!.Value) > Apart(ink, bar.Card!.Value), $"{(dark ? "dark" : "light")}: {layout.Parts[part].Kind}'s {side} side is {rim}, not its outline {ink}");
                            }
                        }
                        // The widget's gap between two is the bar's: no card, no outline across it.
                        var (one, next) = (layout.Plate(parts[1]), layout.Plate(parts[2]));
                        var gap = pixel(new POINT { x = (int)((one.Right + next.X) / 2), y = (int)(one.Y + (one.Height / 2)) });
                        Assert.Equal(bar.Taskbar!.Value, gap);
                    });
                });
                // And arranged, where the outline is picked (Appearance is an arranged widget's): to look at.
                WithLookBar(gfx, dark, (layout, content, _) =>
                    ShootLooks(gfx, layout, content, look, $"widget-look-outlines-edit-{(dark ? "dark" : "light")}.png", _ => { }, _ => { }), editing: true);
            }
        });
    }

    /// <summary>
    /// A widget's outline keeps its colour however the widget is lit (§325; the person: "a widget's
    /// highlight covers its outline, muddying the colour"): the outline is drawn over the highlight,
    /// the pressed shade and edit mode's wash. Its own outline, under a strong highlight of its own,
    /// at rest, under the pointer and pressed, and arranged at rest, under the pointer and selected:
    /// along each side the outline's pixel is as near its ink lit as at rest (the selected ring, the
    /// accent's, goes round over it, to look at). The bar's outline on the widget beside it the same.
    /// widget-look-lit-&lt;state&gt;-&lt;look&gt;.png.
    /// </summary>
    [UiFact]
    public void ALitWidgetKeepsItsOutline()
    {
        WindowShots.OnHiddenDesktop("widget-look-lit", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var bar = dark
                    ? new Palette(Taskbar: A("#FF101010"), Card: A("#FF444444"), CardOutline: A("#FFFFB900"), Highlight: A("#C0FFFFFF"))
                    : new Palette(Taskbar: A("#FFEEEEEE"), Card: A("#FFFFFFFF"), CardOutline: A("#FF8764B8"), Highlight: A("#C0000000"));
                var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque, Colors = bar };
                var name = dark ? "dark" : "light";
                foreach (var editing in new[] { false, true })
                {
                    WithLookBar(gfx, dark, (layout, content, parts) =>
                    {
                        var (own, beside) = (parts[0], parts[1]);
                        var outline = bar.CardOutline!.Value;
                        var where = editing ? "arranged" : "at rest";
                        // Per side, the pixel the outline covers most at rest, and how much of it the outline is there.
                        Dictionary<(int, string), (POINT At, float Share)> rest = [];
                        void Sides(Func<POINT, Argb> pixel, string state, bool keep)
                        {
                            foreach (var (part, ink, sides) in new[] { (own, outline, new[] { "top", "bottom", "left", "right" }), (beside, bar.CardOutline!.Value, ["top", "bottom", "left"]) })
                            {
                                // The card under the outline as it is now: lit, pressed or washed, or at rest.
                                var under = pixel(CardInside(layout, part));
                                foreach (var side in sides)
                                {
                                    if (keep)
                                    {
                                        var at = RimAt(layout, pixel, part, side, ink);
                                        rest[(part, side)] = (at, Share(pixel(at), under, ink));
                                        continue;
                                    }
                                    // The outline's smoothed edge shows the card through it, so it is weighed against the
                                    // card as it is now: drawn over the highlight it is as much of the pixel as at rest;
                                    // under it, the highlight took most of it (a quarter left under this one's).
                                    var (point, was) = rest[(part, side)];
                                    var seen = pixel(point);
                                    var share = Share(seen, under, ink);
                                    Assert.True(share >= 0.8f * was,
                                        $"{name}, {where}, {state}: {layout.Parts[part].Kind}'s {side} outline is {seen} over {under}, {share:0.00} of its ink {ink}; at rest {was:0.00}");
                                }
                            }
                        }
                        var prefix = $"widget-look-lit-{(editing ? "edit-" : "")}";
                        ShootLooks(gfx, layout, content, look, $"{prefix}rest-{name}.png", _ => { }, pixel => Sides(pixel, "rest", keep: true));
                        if (!editing)
                        {
                            foreach (var (part, what) in new[] { (own, "own"), (beside, "bar") })
                            {
                                var hit = Hit.Cell(part, 0);
                                ShootLooks(gfx, layout, content, look, $"{prefix}hover-{what}-{name}.png", scene => scene.Highlight(hit, pressed: false), pixel => Sides(pixel, $"{what} lit", keep: false));
                                ShootLooks(gfx, layout, content, look, $"{prefix}pressed-{what}-{name}.png", scene =>
                                {
                                    scene.Highlight(hit, pressed: true);
                                    scene.Press(hit, down: true);
                                }, pixel => Sides(pixel, $"{what} pressed", keep: false));
                            }
                            return;
                        }
                        foreach (var part in new[] { own, beside })
                        {
                            ShootLooks(gfx, layout, content, look, $"{prefix}hover-{(part == own ? "own" : "bar")}-{name}.png", _ => { },
                                pixel => Sides(pixel, $"{layout.Parts[part].Kind} hovered", keep: false), new EditView { Hovered = part });
                        }
                        ShootLooks(gfx, layout, content, look, $"{prefix}selected-{name}.png", _ => { }, _ => { }, new EditView { Hovered = own, Selected = own });
                    }, editing: editing);
                }
            }
        });
    }

    /// <summary>
    /// The search box is its part's card, so it wears the card, the card outline and the highlight
    /// somebody chose, the bar's (Appearance) and its own over them (§311), as every card does (§325;
    /// the person: "the search box ignores card and outline colours"); with none chosen it keeps
    /// Windows' own look, lighting by its own fill. search-box-colors-&lt;which&gt;-&lt;state&gt;-&lt;look&gt;.png.
    /// </summary>
    [UiFact]
    public void TheSearchBoxWearsTheCardsColours()
    {
        WindowShots.OnHiddenDesktop("search-box-colors", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var name = dark ? "dark" : "light";
                var bar = dark
                    ? new Palette(Taskbar: A("#FF101010"), Card: A("#FF444444"), CardOutline: A("#FF9A9A9A"), Highlight: A("#60D13438"))
                    : new Palette(Taskbar: A("#FFEEEEEE"), Card: A("#FFFFFFFF"), CardOutline: A("#FF777777"), Highlight: A("#60D13438"));
                foreach (var (which, colors) in new (string, Palette)[]
                {
                    ("bar", bar),
                    ("automatic", new Palette(Taskbar: bar.Taskbar)),
                })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque, Colors = colors };
                    WithLookBar(gfx, dark, (layout, content, _) =>
                    {
                        var search = layout.PartOf(Hit.Search);
                        Assert.True(layout.SearchBoxed(search), "no search box");
                        var hit = Hit.Search.In(search);
                        var box = layout.SearchBoxRect(hit);
                        // Deep inside, past the words, and on its top edge half way along.
                        var inside = new POINT { x = (int)(box.Right - layout.SearchBoxRadius - (3 * layout.Stroke) - 6), y = (int)(box.Y + (box.Height / 2)) };
                        var (card, outline, glow) = (colors.Card, colors.CardOutline, colors.Highlight);
                        foreach (var (state, pressed) in new (string, bool?)[] { ("rest", null), ("hover", false), ("pressed", true) })
                        {
                            ShootLooks(gfx, layout, content, look, $"search-box-colors-{which}-{state}-{name}.png", scene =>
                            {
                                scene.Highlight(pressed is null ? null : hit, pressed ?? false);
                                if (pressed is not null)
                                {
                                    // Lit by the highlight chosen, as a card is; with none chosen, by its own fill, no shade.
                                    var shade = scene.LitColor;
                                    if (glow is not { } lit)
                                    {
                                        Assert.Equal(0, shade.A);
                                    }
                                    else
                                    {
                                        Assert.Equal((lit.R, lit.G, lit.B), (shade.R, shade.G, shade.B));
                                        Assert.True(pressed.Value ? shade.A > 0 && shade.A < lit.A : shade.A == lit.A, $"{name}, {which}, {state}: the shade is {shade}");
                                    }
                                }
                            }, pixel =>
                            {
                                if (card is not { } fill || outline is not { } ink)
                                {
                                    return;
                                }
                                if (pressed is null)
                                {
                                    Assert.Equal(fill, pixel(inside));
                                }
                                // Its edge, smoothed over two pixels, is mostly the outline over the box as it is now, lit or not.
                                var under = pixel(inside);
                                foreach (var side in new[] { "top", "bottom", "left", "right" })
                                {
                                    var rim = RimOf(layout, pixel, search, side, ink, box);
                                    Assert.True(Share(rim, under, ink) >= 0.25f, $"{name}, {which}, {state}: the box's {side} edge is {rim} over {under}, not its outline {ink}");
                                }
                            });
                        }
                    });
                }
            }
        });
    }

    /// <summary>
    /// The nearest to <paramref name="ink"/> of the few pixels across <paramref name="side"/>'s band of
    /// part <paramref name="part"/>'s card (<paramref name="rect"/>, else where the scene puts it,
    /// Scene.PlaceCard: the card's room, else the block's), half way along: its outline there.
    /// </summary>
    private static Argb RimOf(Layout layout, Func<POINT, Argb> pixel, int part, string side, Argb ink, RectF? rect = null) =>
        pixel(RimAt(layout, pixel, part, side, ink, rect));

    /// <summary>Where <see cref="RimOf"/> reads.</summary>
    private static POINT RimAt(Layout layout, Func<POINT, Argb> pixel, int part, string side, Argb ink, RectF? rect = null)
    {
        var room = rect ?? (layout.CardRect(part) is { IsEmpty: false } card ? card : layout.BlockRect(part));
        var stroke = (int)MathF.Ceiling(layout.Stroke);
        var (midX, midY) = ((int)(room.X + (room.Width / 2)), (int)(room.Y + (room.Height / 2)));
        var (left, top, right, bottom) = ((int)room.X, (int)room.Y, (int)MathF.Ceiling(room.Right) - 1, (int)MathF.Ceiling(room.Bottom) - 1);
        return Enumerable.Range(-1, stroke + 2).Select(step => side switch
        {
            "top" => new POINT { x = midX, y = top + step },
            "bottom" => new POINT { x = midX, y = bottom - step },
            "left" => new POINT { x = left + step, y = midY },
            _ => new POINT { x = right - step, y = midY },
        }).MinBy(at => Apart(pixel(at), ink));
    }

    /// <summary>How much of <paramref name="seen"/> is <paramref name="ink"/> laid over <paramref name="under"/>: 0 none, 1 all.</summary>
    private static float Share(Argb seen, Argb under, Argb ink)
    {
        var (r, g, b) = (ink.R - under.R, ink.G - under.G, ink.B - under.B);
        var along = ((seen.R - under.R) * r) + ((seen.G - under.G) * g) + ((seen.B - under.B) * b);
        return along / (float)Math.Max(1, (r * r) + (g * g) + (b * b));
    }

    private static Color Ink(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static int Apart(Argb one, Argb other) => Math.Abs(one.R - other.R) + Math.Abs(one.G - other.G) + Math.Abs(one.B - other.B);

    /// <summary>
    /// The default bar at <paramref name="dpi"/> (150 %) with three widgets at its start, in the bar's look (§567: no widget has
    /// one of its own); a show desktop as Windows 11's bare corner (§302) after the first, a widget's gap from either as every
    /// widget (§515).
    /// </summary>
    private static void WithLookBar(Gfx gfx, bool dark, Action<Layout, Content, int[]> body, bool editing = false, uint dpi = 144)
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = 2400, bottom = 1440 };
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        var start = Math.Max(0, bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start));
        var kinds = new[] { BarKind.NightLight, BarKind.AirplaneMode, BarKind.EnergySaver };
        for (var at = 0; at < kinds.Length; at++)
        {
            var item = kinds[at].New();
            item.Id = $"look-{at}";
            item.Section = BarSection.Start;
            bar.Insert(start + at, item);
        }
        // A bare corner after the first; every widget on its own card (§512).
        var corner = BarKind.ShowDesktop.New();
        corner.Id = "look-corner";
        corner.Section = BarSection.Start;
        corner.ShowDesktop = new ShowDesktopOptions { Style = ShowDesktopStyle.Corner };
        bar.Insert(start + 1, corner);
        var (specs, islands) = Made(gfx, config, bar);
        try
        {
            var content = new Content { Islands = islands };
            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, AddListPictures.LookFor(dark), specs, content, editing);
            int[] parts = [.. kinds.Select((_, at) => specs.FindIndex(spec => spec.Key.EndsWith($"look-{at}", StringComparison.Ordinal)))];
            body(layout, content, parts);
        }
        finally
        {
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>Inside a card's start, past its corner and outline, mid-height: the card's own colour where its content is not.</summary>
    private static POINT CardInside(Layout layout, int part)
    {
        var rect = layout.CardRect(part);
        return new POINT { x = (int)(rect.X + layout.CardRadius + 3), y = (int)(rect.Y + (rect.Height / 2)) };
    }

    /// <summary>On a card's top edge, half way along: its outline.</summary>
    private static POINT OnCardEdge(Layout layout, int part)
    {
        var rect = layout.CardRect(part);
        return new POINT { x = (int)(rect.X + (rect.Width / 2)), y = (int)MathF.Floor(rect.Y + (layout.Stroke / 2)) };
    }

    /// <summary>The scene of <paramref name="layout"/> in <paramref name="look"/>, <paramref name="then"/> done on it, shot to <paramref name="file"/>, its pixels read by <paramref name="check"/> (window px, opaque).</summary>
    private static void ShootLooks(Gfx gfx, Layout layout, Content content, Look look, string file, Action<Scene> then, Action<Func<POINT, Argb>> check, EditView? edit = null)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true, edit: edit ?? (layout.Editing ? new EditView() : null));
            then(scene);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            var panel = layout.PanelRect;
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            byte[] pixels = [];
            var width = 0;
            Assert.True(WindowShots.Save(window, file, part, (seen, w) => (pixels, width) = (seen, w)), $"{file} came back blank");
            check(at =>
            {
                var i = (((at.y - part.top) * width) + (at.x - part.left)) * 4;
                return new Argb(0xFF, pixels[i + 2], pixels[i + 1], pixels[i]);
            });
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
