// Every look value changes the one surface it names, and nothing else (docs\widgets.md §512; the person, 2026-11-05: "the
// widgets outline is not correct, the outline goes around the group and not the individual widgets, same for the background
// color. the taskbar outline also does not do anything on the normal taskbar. … every style setting should be properly
// checked so that it changes the correct thing."). Table-driven over the bar's kinds — docked at the bottom, at the top, on
// either side, floating —: a bar of two sections (Start, Task view and two widgets; a folder's clock and a folder's icon), at
// 150 % on the person's 42-DIP bar, drawn with nothing set and with each value set in turn to a colour of its own, and the
// pixels read where each surface is: each widget's card's sides and its inside, the bar between two widgets, the
// bar's edge facing the desktop, the bar's room where no widget stands. A value's own surface must change; the others must
// not. A card pattern is drawn inside each card, a sheet of its own (§515). §567: no widget has a look of its own.
// To KARPO_PNG_DIR as look-surface-<bar>-<setting>.png, and the person's kind of bar as look-surface-person-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The bar the look table is drawn on: the Windows buttons and two widgets at its start, two folders at its end.</summary>
    private const string SurfaceBar = """
        {
          "layouts": [ { "id": "default", "name": "Default", "type": "taskbar", "size": 42, "islands": [
            { "id": "s0", "kind": "start", "section": "start" },
            { "id": "s1", "kind": "taskView", "section": "start" },
            { "id": "s2", "kind": "nightLight", "section": "start" },
            { "id": "s3", "kind": "airplaneMode", "section": "start" },
            { "id": "s5", "kind": "folderList", "section": "end", "folder": { "face": { "kind": "dateTime", "options": { "seconds": "windows" } }, "lists": [ [] ] } },
            { "id": "s6", "kind": "folderList", "section": "end", "folder": { "face": { "kind": "icon", "options": {} }, "lists": [ [] ] } }
          ] } ],
          "defaultLayoutId": "default"
        }
        """;

    /// <summary>The ids of the bar's widgets, in order: four at the bar's start, two at its end.</summary>
    private static readonly string[] SurfaceParts = ["s0", "s1", "s2", "s3", "s5", "s6"];

    /// <summary>The sections of <see cref="SurfaceParts"/>, by index into it.</summary>
    private static readonly int[][] SurfaceSections = [[0, 1, 2, 3], [4, 5]];

    /// <summary>The widget whose own look the table sets: the night light, between two at the start.</summary>
    private const int OwnPart = 2;

    /// <summary>A bar's kind: where it stands and whether it floats.</summary>
    private sealed record SurfaceKind(string Name, Edge Edge, BarStyle Style);

    private static readonly SurfaceKind[] SurfaceKinds =
    [
        new("bottom", Edge.Bottom, BarStyle.Islands),
        new("top", Edge.Top, BarStyle.Islands),
        new("left", Edge.Left, BarStyle.Islands),
        new("right", Edge.Right, BarStyle.Islands),
        new("floating", Edge.Bottom, BarStyle.Floating),
    ];

    /// <summary>What a surface is, read off a picture of the bar (window px).</summary>
    private enum Seen
    {
        /// <summary>Every card's four sides, its outline's band.</summary>
        Rims,

        /// <summary>Every card's inside, by its corners, where its content is not.</summary>
        Insides,

        /// <summary>Every card's content: its middle.</summary>
        Contents,

        /// <summary>The bar between two cards of a section: the widget's gap.</summary>
        Gaps,

        /// <summary>The bar's room where no card stands, all of it.</summary>
        Ground,

        /// <summary>The bar's edge facing the desktop.</summary>
        Edge,

        /// <summary>The own widget's sides (<see cref="OwnPart"/>).</summary>
        OwnRim,

        /// <summary>The own widget's inside.</summary>
        OwnInside,

        /// <summary>The own widget's content.</summary>
        OwnContent,

        /// <summary>The other widgets' sides.</summary>
        OthersRims,

        /// <summary>The other widgets' insides.</summary>
        OthersInsides,

        /// <summary>The other widgets' content.</summary>
        OthersContents,

        /// <summary>The clock's folder's face's content (its text).</summary>
        ClockText,
    }

    /// <summary>One value of a look set, the surfaces it changes and those it must leave as they were.</summary>
    private sealed record SurfaceSetting(string Name, Action<Config> Set, Seen[] Changes, Seen[] Keeps);

    private static void Section(Config config, LookPart part, Action<LookValues> change) =>
        Assert.True(Looks.Write(Looks.Section(config, part), change));


    private static readonly SurfaceSetting[] SurfaceSettings =
    [
        new("taskbar-background", config => Section(config, LookPart.Taskbar, look => look.Background = LookShorthand.Pair("#6A1B9A")),
            [Seen.Gaps, Seen.Ground], []),
        new("taskbar-outline", config => Section(config, LookPart.Taskbar, look => look.Outline = LookShorthand.Pair("#FF6D00")),
            [Seen.Edge], [Seen.Rims, Seen.Insides, Seen.Gaps]),
        new("taskbar-pattern", config => Section(config, LookPart.Taskbar, look => look.Pattern = Pattern.Grid),
            [Seen.Ground], []),
        new("widgets-background", config => Section(config, LookPart.Widgets, look => look.Background = LookShorthand.Pair("#00897B")),
            [Seen.Insides], [Seen.Gaps, Seen.Ground, Seen.Edge]),
        new("widgets-outline", config => Section(config, LookPart.Widgets, look => look.Outline = LookShorthand.Pair("#FFEB3B")),
            [Seen.Rims], [Seen.Insides, Seen.Gaps, Seen.Ground, Seen.Edge]),
        new("widgets-text", config => Section(config, LookPart.Widgets, look => look.Text = LookShorthand.Pair("#FF4081")),
            [Seen.Contents], [Seen.Gaps, Seen.Edge]),
        new("widgets-pattern", config => Section(config, LookPart.Widgets, look =>
            {
                look.Background = LookShorthand.Pair("#37474F");
                look.Pattern = Pattern.Grid;
            }),
            [Seen.Insides], [Seen.Gaps, Seen.Ground, Seen.Edge]),
        new("widgets-font", config => Section(config, LookPart.Widgets, look => look.Font = new TextStyle("Cascadia Mono").Text),
            [Seen.ClockText], [Seen.Gaps, Seen.Edge]),
        new("widgets-weight", config => Section(config, LookPart.Widgets, look => look.Font = new TextStyle(null, 600).Text),
            [Seen.ClockText], [Seen.Gaps, Seen.Edge]),
    ];

    /// <summary>A picture of the bar: its layout, its pixels (window px, opaque) and its docks where it floats.</summary>
    private sealed class SurfaceShot(Layout layout, byte[] pixels, int width, RECT crop, IReadOnlyList<RectF> docks)
    {
        public Layout Layout { get; } = layout;

        public IReadOnlyList<RectF> Docks { get; } = docks;

        public bool Has(int x, int y) => x >= crop.left && x < crop.right && y >= crop.top && y < crop.bottom;

        public Argb At(int x, int y)
        {
            var i = (((y - crop.top) * width) + (x - crop.left)) * 4;
            return new Argb(0xFF, pixels[i + 2], pixels[i + 1], pixels[i]);
        }
    }

    /// <summary>
    /// §512: every look value of the bar changes its own surface and no other, on every kind of bar; a widget's own look its
    /// widget alone; a card's pattern a sheet of its own, inside each card.
    /// </summary>
    [UiFact]
    public void EveryLookValueChangesItsOwnSurface()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("look-surface", (gfx, instance) =>
        {
            Clock.Now = () => new DateTime(2026, 11, 5, 9, 28, 0);
            WindowsClock.SecondsStand = false;
            try
            {
                foreach (var kind in SurfaceKinds)
                {
                    var baseline = ShootSurface(gfx, kind, _ => { }, $"look-surface-{kind.Name}-none.png", null);
                    foreach (var setting in SurfaceSettings)
                    {
                        var shot = ShootSurface(gfx, kind, setting.Set, $"look-surface-{kind.Name}-{setting.Name}.png",
                            setting.Name == "widgets-pattern" ? (layout, scene) => SheetPerCard(layout, scene, kind, failures) : null);
                        foreach (var surface in setting.Changes)
                        {
                            foreach (var (where, moved) in Compare(baseline, shot, surface))
                            {
                                if (moved <= ChangedApart)
                                {
                                    failures.Add($"{kind.Name}, {setting.Name}: {where} did not change ({moved})");
                                }
                            }
                        }
                        foreach (var surface in setting.Keeps)
                        {
                            foreach (var (where, moved) in Compare(baseline, shot, surface))
                            {
                                if (moved > KeptApart)
                                {
                                    failures.Add($"{kind.Name}, {setting.Name}: {where} changed ({moved})");
                                }
                            }
                        }
                    }
                }
                // The person's kind of bar: docked at the bottom, 42 DIP at 150 %, Contours on the bar outlined in orange, the
                // widgets on a background outlined in white.
                _ = ShootSurface(gfx, SurfaceKinds[0], config =>
                {
                    Section(config, LookPart.Taskbar, look =>
                    {
                        look.Outline = LookShorthand.Pair("#F7630C");
                        look.Pattern = Pattern.Contours;
                    });
                    Section(config, LookPart.Widgets, look =>
                    {
                        look.Background = LookShorthand.Pair("#802E7D32");
                        look.Outline = LookShorthand.Pair("#FFFFFF");
                    });
                }, "look-surface-person-150.png", null);
            }
            finally
            {
                WindowsClock.SecondsStand = null;
                Clock.Now = Clock.Real;
                gfx.Typeface = default;
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Apart (the three channels' differences summed) above which a pixel changed.</summary>
    private const int ChangedApart = 40;

    /// <summary>Apart at or under which a pixel is as it was.</summary>
    private const int KeptApart = 12;

    /// <summary>
    /// §462, §512, §515: the widgets' pattern, inside each card — its layer the card's own size, run on into nothing — and a sheet
    /// of its own: every card's sheet's corner its own.
    /// </summary>
    private static void SheetPerCard(Layout layout, Scene scene, SurfaceKind kind, List<string> failures)
    {
        foreach (var id in SurfaceParts)
        {
            var part = Part(layout, id);
            var plate = SurfacePlate(layout, part);
            var key = $"card:{layout.Specs[part].Key}";
            if (scene.PatternOf(key) is not { Showing: not null } layer || scene.Registered(key) is not { } visual)
            {
                failures.Add($"{kind.Name}: {id}'s card has no pattern");
                continue;
            }
            var corner = SheetCorner(visual.Offset, layer);
            if (Vector2.Distance(corner, new Vector2(plate.X, plate.Y)) > 0.5f)
            {
                failures.Add($"{kind.Name}: {id}'s pattern's sheet starts at {corner}, its card at {plate.X},{plate.Y}");
            }
            if (visual.Size != plate.Size)
            {
                failures.Add($"{kind.Name}: {id}'s card is {visual.Size}, its rectangle {plate.Size}");
            }
        }
    }

    /// <summary>The part of the bar's widget <paramref name="id"/>.</summary>
    private static int Part(Layout layout, string id) => layout.Specs.ToList().FindIndex(spec => spec.Key.EndsWith($"/{id}", StringComparison.Ordinal));

    /// <summary>A part's card's rectangle: its card's room, else its block (Scene's card and plate both stand there).</summary>
    private static RectF SurfacePlate(Layout layout, int part) =>
        layout.CardRect(part) is { IsEmpty: false } card ? card : layout.BlockRect(part);

    /// <summary>
    /// The bar of <see cref="SurfaceBar"/> as <paramref name="kind"/>, with <paramref name="set"/> done to its config, in Windows'
    /// dark look without transparency (with it, <paramref name="glass"/>), at 150 %: shot to <paramref name="file"/>,
    /// <paramref name="pose"/> checked on its scene.
    /// </summary>
    private static SurfaceShot ShootSurface(Gfx gfx, SurfaceKind kind, Action<Config> set, string file, Action<Layout, Scene>? pose, bool glass = false)
    {
        var config = Config.Parse(SurfaceBar);
        config.Height = config.Layouts[0].Size;
        config.Style = kind.Style;
        set(config);
        var look = (TestLooks.LookFor(dark: true) with { Animations = false, Glass = glass }).Wearing(config);
        gfx.Typeface = Looks.MenuText(config);
        var area = new RECT { left = 0, top = 0, right = 2400, bottom = 1440 };
        var (specs, islands) = Made(gfx, config, config.DefaultLayout.Islands);
        var window = MakeWindow();
        Scene? scene = null;
        try
        {
            var content = new Content { Islands = islands };
            var layout = new Layout(gfx, 144, area, area, kind.Edge, config, look, specs, content);
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            DesktopWindowTarget target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true, edit: null);
            pose?.Invoke(layout, scene);
            var docks = scene.FloatingPanelRects();
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            var panel = layout.PanelRect;
            var crop = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            byte[] pixels = [];
            var width = 0;
            Assert.True(WindowShots.Save(window, file, crop, (seen, w) => (pixels, width) = (seen, w)), $"{file} came back blank");
            return new SurfaceShot(layout, pixels, width, crop, docks);
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>
    /// How far each place of <paramref name="surface"/> moved from <paramref name="before"/> to <paramref name="after"/>: the
    /// most any of its pixels did, where it is (read off <paramref name="before"/>'s layout but for the clock's text, which a
    /// font moves: read over both).
    /// </summary>
    private static IEnumerable<(string Where, int Moved)> Compare(SurfaceShot before, SurfaceShot after, Seen surface)
    {
        var layout = before.Layout;
        var stroke = (int)MathF.Ceiling(layout.Stroke);
        var parts = SurfaceParts.Select(id => Part(layout, id)).ToArray();
        int Most(IEnumerable<(int X, int Y)> points) =>
            points.Where(point => before.Has(point.X, point.Y) && after.Has(point.X, point.Y))
                .Select(point => Apart(before.At(point.X, point.Y), after.At(point.X, point.Y)))
                .DefaultIfEmpty(-1)
                .Max();
        IEnumerable<(int, int)> Area(RectF rect)
        {
            for (var y = (int)MathF.Ceiling(rect.Y); y < (int)rect.Bottom; y++)
            {
                for (var x = (int)MathF.Ceiling(rect.X); x < (int)rect.Right; x++)
                {
                    yield return (x, y);
                }
            }
        }
        // A card's sides: its outline's band, half way along each.
        IEnumerable<(string, int)> Rim(int index)
        {
            var room = SurfacePlate(layout, parts[index]);
            var (midX, midY) = ((int)(room.X + (room.Width / 2)), (int)(room.Y + (room.Height / 2)));
            var (left, top, right, bottom) = ((int)room.X, (int)room.Y, (int)MathF.Ceiling(room.Right) - 1, (int)MathF.Ceiling(room.Bottom) - 1);
            var band = Enumerable.Range(0, stroke + 1).ToArray();
            yield return ($"{SurfaceParts[index]}'s top side", Most(band.Select(step => (midX, top + step))));
            yield return ($"{SurfaceParts[index]}'s bottom side", Most(band.Select(step => (midX, bottom - step))));
            yield return ($"{SurfaceParts[index]}'s left side", Most(band.Select(step => (left + step, midY))));
            yield return ($"{SurfaceParts[index]}'s right side", Most(band.Select(step => (right - step, midY))));
        }
        // A card's inside: by its top-left and bottom-right corners, past the corner's curve and the outline, where no content is.
        (string, int) Inside(int index)
        {
            var room = SurfacePlate(layout, parts[index]);
            var corner = (int)MathF.Ceiling(MathF.Max(layout.CardRadius, layout.SoftPx(4f)) * 0.4f) + (2 * stroke) + 2;
            return ($"{SurfaceParts[index]}'s inside", Most([((int)room.X + corner, (int)room.Y + corner), ((int)room.Right - 1 - corner, (int)room.Bottom - 1 - corner)]));
        }
        (string, int) Content(int index, SurfaceShot shot)
        {
            var room = SurfacePlate(shot.Layout, Part(shot.Layout, SurfaceParts[index]));
            var inner = new RectF(room.X + (room.Width * 0.2f), room.Y + (room.Height * 0.2f), room.Width * 0.6f, room.Height * 0.6f);
            return ($"{SurfaceParts[index]}'s content", Most(Area(inner)));
        }
        IEnumerable<(string, int)> Contents(IEnumerable<int> indices) =>
            // Start's logo is Windows' own, in no look's colours.
            indices.Where(index => index != 0).Select(index => Content(index, before));
        switch (surface)
        {
            case Seen.Rims:
                return Enumerable.Range(0, parts.Length).SelectMany(Rim);
            case Seen.Insides:
                return Enumerable.Range(0, parts.Length).Select(Inside);
            case Seen.Contents:
                return Contents(Enumerable.Range(0, parts.Length));
            case Seen.OwnRim:
                return Rim(OwnPart);
            case Seen.OwnInside:
                return [Inside(OwnPart)];
            case Seen.OwnContent:
                return Contents([OwnPart]);
            case Seen.OthersRims:
                return Enumerable.Range(0, parts.Length).Where(index => index != OwnPart).SelectMany(Rim);
            case Seen.OthersInsides:
                return Enumerable.Range(0, parts.Length).Where(index => index != OwnPart).Select(Inside);
            case Seen.OthersContents:
                return Contents(Enumerable.Range(0, parts.Length).Where(index => index != OwnPart));
            case Seen.ClockText:
                return [Content(4, before), Content(4, after)];
            case Seen.Gaps:
                // A gap a font moved (the clock's text another length) is no longer where it was: only those that stand still.
                bool Still(int index) => SurfacePlate(layout, parts[index]) == SurfacePlate(after.Layout, Part(after.Layout, SurfaceParts[index]));
                return SurfaceSections.SelectMany(section => section.Skip(1).Where(index => Still(index - 1) && Still(index)).Select(index =>
                {
                    var (one, next) = (SurfacePlate(layout, parts[index - 1]), SurfacePlate(layout, parts[index]));
                    var gap = layout.Vertical
                        ? new RectF(one.X + (one.Width * 0.25f), one.Bottom + 1, one.Width * 0.5f, MathF.Max(0f, next.Y - one.Bottom - 2))
                        : new RectF(one.Right + 1, one.Y + (one.Height * 0.25f), MathF.Max(0f, next.X - one.Right - 2), one.Height * 0.5f);
                    return ($"the gap after {SurfaceParts[index - 1]}", Most(Area(gap)));
                }));
            case Seen.Ground:
            {
                // Where the bar shows and no card stands, two px clear of every card; a floating bar's inside its docks.
                var cards = parts.Select(part => SurfacePlate(layout, part)).Select(card => new RectF(card.X - 2, card.Y - 2, card.Width + 4, card.Height + 4)).ToArray();
                var bodies = before.Docks.Count > 0 ? before.Docks : [layout.PanelRect];
                var room = bodies.SelectMany(body => Area(new RectF(body.X + 3, body.Y + 3, body.Width - 6, body.Height - 6)))
                    .Where(point => !cards.Any(card => card.Contains(point.Item1, point.Item2)));
                var moved = room.Where(point => before.Has(point.Item1, point.Item2))
                    .Count(point => Apart(before.At(point.Item1, point.Item2), after.At(point.Item1, point.Item2)) > KeptApart);
                // As a share: changed, where a pattern's ink covers some of it; kept, where none of it moved.
                return [("the bar's room", moved > 20 ? ChangedApart + 1 : moved > 0 ? KeptApart + 1 : 0)];
            }
            default:
            {
                // The bar's edge facing the desktop, three quarters along it, where no widget stands; a floating bar's dock's.
                var body = before.Docks.Count > 0 ? before.Docks[0] : layout.PanelRect;
                var band = Enumerable.Range(0, stroke + 1).ToArray();
                var (alongX, alongY) = before.Docks.Count > 0
                    ? ((int)(body.X + (body.Width / 2)), (int)(body.Y + (body.Height / 2)))
                    : ((int)(body.X + (body.Width * 0.75f)), (int)(body.Y + (body.Height * 0.75f)));
                IEnumerable<(int, int)> points = layout.Edge switch
                {
                    Edge.Top => band.Select(step => (alongX, (int)MathF.Ceiling(body.Bottom) - 1 - step)),
                    Edge.Left => band.Select(step => ((int)MathF.Ceiling(body.Right) - 1 - step, alongY)),
                    Edge.Right => band.Select(step => ((int)body.X + step, alongY)),
                    _ => band.Select(step => (alongX, (int)body.Y + step)),
                };
                return [("the bar's edge", Most(points))];
            }
        }
    }
}
