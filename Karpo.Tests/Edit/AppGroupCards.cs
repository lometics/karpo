// The pins and the open apps stand like every other widget (§515: a widget's gap apart, each part on its own card,
// §512); §602 every app button wears a card of its own, an open app's as a pin's, so the open apps stand the gap apart
// as the pins do; the pointer lights the button's own cell, and the focused app keeps the taskbar's plate inside it.
//
// Held on the dock's real tree, and drawn by the real scene on a desktop of its own and captured as
// the compositor draws it (WindowShots), to KARPO_PNG_DIR as
// app-group-<height>-<look>-<scale>-<rest|hover-pin|hover-app|hover-more>.png: Start, three pins (the
// second running), three open apps (the first focused), then widgets in the middle
// section — at 40 and 48 DIPs, dark and light, at 100 % and 150 %.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>A button for the bar: its picture a glyph, so nothing is read from the shell.</summary>
    private static (Item, Mark) AppTile(int index, Mark mark) => (new Item
    {
        Target = $@"C:\Karpo\apps\app{index}.exe",
        Name = $"App {index}",
        Kind = ItemKind.Shell,
        Keys = [],
        Picture = index % 2 == 0 ? "\uE737" : "\uE8A5",
    }, mark);

    /// <summary>Two pins (the second running) and three open apps — or <paramref name="more"/> more —, the first of them focused.</summary>
    private static Content AppGroupContent(IslandCollection? islands = null, int more = 0) => new()
    {
        Islands = islands,
        Tiles =
        [
            AppTile(0, new Mark(0, false, false)),
            AppTile(1, new Mark(1, false, false)),
            AppTile(2, new Mark(1, Active: true, Attention: false)),
            AppTile(3, new Mark(2, false, false)),
            AppTile(4, new Mark(1, false, false)),
            .. Enumerable.Range(5, more).Select(index => AppTile(index, new Mark(1, false, false))),
        ],
    };

    /// <summary>
    /// A card a widget (§512) and §602 one an app button: a pin's over its cell, and each open app's over its own cell, none
    /// over the Apps widget's whole block, so the open apps stand the gap apart as the pins do; and the pointer over a button
    /// lights exactly that button's cell, inside its part's block. A card colour is chosen, since a bar draws none until
    /// somebody chooses one (§246).
    /// </summary>
    [UiFact]
    public void EveryAppButtonWearsACardOfItsOwn()
    {
        foreach (var edge in new[] { Edge.Bottom, Edge.Left })
        {
            var config = new Config { Height = 48, Style = BarStyle.Islands };
            var area = new RECT { left = 0, top = 0, right = ScreenWidth, bottom = ScreenHeight };
            List<PartSpec> specs = [Spec("s", BarKind.Start), Spec("p1", BarKind.Pin), Spec("p2", BarKind.Pin), Spec("apps", BarKind.Apps), Spec("c", BarKind.NightLight)];
            var layout = Layout.ForTests(96, area, edge, config, specs, Layout.CellPx(96, config), tiles: 5);
            var seen = 0;

            OnItsOwnThread(gfx =>
            {
                var window = MakeWindow();
                DesktopWindowTarget? target = null;
                try
                {
                    target = Target(gfx, window);
                    var look = LookFor(dark: true) with { Colors = new Palette(Card: new Argb(0x0D, 0xFF, 0xFF, 0xFF)) };
                    using var scene = Scenes.Build(gfx, target, layout, AppGroupContent(), look, new Icons(), shown: true);
                    var root = target.Root;
                    var pinCard = Assert.IsAssignableFrom<ShapeVisual>(scene.Registered($"card:{specs[1].Key}"));
                    var cardColour = FillOf(pinCard);
                    Assert.NotNull(cardColour);
                    Assert.NotNull(scene.Registered($"card:{specs[3].Key}"));

                    // The cards: Start's, the widget's, and §602 one an app button, over its own cell — none over the open apps' block.
                    var cards = Shown(root, Vector2.Zero).Where(item => item.Visual is ShapeVisual shape && FillOf(shape) == cardColour).ToList();
                    Assert.Equal(1 + 5 + 1, cards.Count);
                    foreach (var tile in Enumerable.Range(0, 5))
                    {
                        var cell = layout.TileCell(tile);
                        Assert.Contains(cards, card => Same(card.At, card.Size, cell));
                    }
                    Assert.DoesNotContain(cards, card => Same(card.At, card.Size, layout.BlockRect(3)));

                    // The pointer over a pin and over an open app: its own cell lights, on the card.
                    foreach (var (tile, part) in new[] { (1, 2), (3, 3) })
                    {
                        scene.Highlight(Hit.Tile(tile), pressed: false);
                        var cell = layout.TileCell(tile);
                        var lit = Shown(root, Vector2.Zero)
                            .Where(item => item.Visual is ShapeVisual shape && FillOf(shape) is { } fill && fill != cardColour && Same(item.At, item.Size, cell))
                            .ToList();
                        Assert.True(lit.Count > 0, $"{edge}: nothing lights button {tile}'s cell");
                        var block = layout.BlockRect(part);
                        Assert.True(
                            cell.X >= block.X - 0.01f && cell.Y >= block.Y - 0.01f && cell.Right <= block.Right + 0.01f && cell.Bottom <= block.Bottom + 0.01f,
                            $"{edge}: button {tile}'s cell runs off its part's card");
                    }
                    seen++;
                }
                finally
                {
                    _ = DestroyWindow(window);
                }
            });
            Assert.Equal(1, seen);
        }
    }

    private static Color? FillOf(ShapeVisual shape) =>
        shape.Shapes.Count > 0 && shape.Shapes[0] is CompositionSpriteShape { FillBrush: CompositionColorBrush brush } ? brush.Color : null;

    private static bool Same(Vector2 at, Vector2 size, RectF rect) =>
        MathF.Abs(at.X - rect.X) < 0.01f && MathF.Abs(at.Y - rect.Y) < 0.01f && MathF.Abs(size.X - rect.Width) < 0.01f && MathF.Abs(size.Y - rect.Height) < 0.01f;

    [UiFact]
    public void AppGroupSheets()
    {
        WindowShots.OnHiddenDesktop("app-group", (gfx, _) =>
        {
            foreach (var height in new[] { 40, 48 })
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var name = $"{height}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        var config = new Config { Height = height, Style = BarStyle.Islands };
                        var area = new RECT { left = 0, top = 0, right = (int)(760 * dpi / 96), bottom = (int)(dpi * 10) };
                        List<BarItem> bar = [BarKind.Start.New(), Pin("p1"), Pin("p2"), BarKind.Apps.New(), BarKind.WidgetsBoard.New(), BarKind.TouchKeyboard.New(), BarKind.WidgetsBoard.New()];
                        foreach (var item in bar)
                        {
                            item.Section = item.Kind is BarKind.WidgetsBoard or BarKind.TouchKeyboard ? BarSection.Middle : BarSection.Start;
                        }
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var content = AppGroupContent(islands);
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                            var apps = specs.FindIndex(spec => spec.Kind == BarKind.Apps);
                            OverADesktop(gfx, layout, look, content, null, $"app-group-{name}-rest.png");
                            OverADesktop(gfx, layout, look, content, null, $"app-group-{name}-hover-pin.png", pose: scene => scene.Highlight(Hit.Tile(1), false));
                            OverADesktop(gfx, layout, look, content, null, $"app-group-{name}-hover-app.png", pose: scene => scene.Highlight(Hit.Tile(3), false));

                            // Too many to show: "…" after the last button shown, on a card of its own (§602).
                            var crowded = AppGroupContent(islands, more: 20);
                            var short_ = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, crowded);
                            Assert.True(short_.Overflowed.Count > 0, $"{name}: nothing went behind \"…\"");
                            OverADesktop(gfx, short_, look, crowded, null, $"app-group-{name}-hover-more.png", pose: scene => scene.Highlight(Hit.AppOverflow, false));
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
            }
        });

        static BarItem Pin(string id)
        {
            var pin = BarKind.Pin.New();
            pin.Id = id;
            return pin;
        }
    }
}
