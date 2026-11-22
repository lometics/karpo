// The bar's three looks (Config.Style: connected, islands, floating) on the dock's real scene, with
// a bar in all three sections (CentredBar) and its real widgets, over a desktop — a wallpaper of the tests' own
// laid under the scene, as the hidden desktop has none — so a floating bar's panels can be seen to
// float: the desktop between and around them, their corners, their outline and their shadow. To
// KARPO_PNG_DIR as bar-style-<look>-<edge>-<theme>-<scale>.png, along the bottom and down the left,
// dark and light, at 100 % and 150 %; and the floating bar in edit mode (its "+" buttons, the
// hatched free room between the panels) as bar-style-floating-edit-<edge>-…, and with the weather
// carried to the right, its panel gone with it, as bar-style-floating-drag-bottom-….
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Vector2 = System.Numerics.Vector2;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void BarStyleSheets()
    {
        WindowShots.OnHiddenDesktop("bar-styles", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                    {
                        var config = new Config { Height = 48, Style = style };
                        var (specs, islands) = Made(gfx, config, CentredBar());
                        try
                        {
                            var content = new Content { Islands = islands, Tiles = [.. Enumerable.Range(0, 3).Select(BusyApp)] };
                            foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                            {
                                var along = (int)(1600 * dpi / 96);
                                var area = edge.IsVertical()
                                    ? new RECT { left = 0, top = 0, right = (int)(dpi * 10), bottom = along }
                                    : new RECT { left = 0, top = 0, right = along, bottom = (int)(dpi * 10) };
                                var where = edge.Title().ToLowerInvariant();
                                var word = style.ToString().ToLowerInvariant();
                                var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content);
                                Assert.Equal(style == BarStyle.Floating, layout.Floating);
                                OverADesktop(gfx, layout, look, content, null, $"bar-style-{word}-{where}-{name}.png");
                                if (style != BarStyle.Floating)
                                {
                                    continue;
                                }
                                var edit = new Layout(gfx, dpi, area, area, edge, config, look, specs, content, editing: true);
                                OverADesktop(gfx, edit, look, content, new EditView(), $"bar-style-floating-edit-{where}-{name}.png");
                                if (edge == Edge.Bottom)
                                {
                                    // The weather folder carried to the right: the start has nothing left, so no panel.
                                    var weather = edit.Parts.FindIndex(part => part.Kind == BarKind.FolderList);
                                    var pointer = edit.Length * 0.85f;
                                    var slot = edit.DropSlot(pointer, weather);
                                    Assert.Equal(BarSection.End, slot.Section);
                                    Assert.True(edit.SectionPanels(weather, slot)[(int)BarSection.Start].IsEmpty);
                                    OverADesktop(gfx, edit, look, content, new EditView(Dragging: weather, PointerAlong: pointer, GrabOffset: 10f, DropIndex: slot), $"bar-style-floating-drag-{where}-{name}.png");
                                }
                            }
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
    }

    /// <summary>
    /// The scene of <paramref name="layout"/> on a window of its own, over a wallpaper laid under it,
    /// shown on the hidden desktop: the bar's strip and the room off the screen's edge, with a little
    /// of the desktop over it for the shadow, saved to <paramref name="file"/>; along the bottom from
    /// <paramref name="start"/> px on, for a picture of the bar's end, with <paramref name="pose"/> done to its scene.
    /// </summary>
    private static void OverADesktop(Gfx gfx, Layout layout, Look look, Content content, EditView? view, string file, int start = 0, Action<Scene>? pose = null)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        ContainerVisual? desktop = null;
        SpriteVisual? wallpaper = null;
        CompositionLinearGradientBrush? wash = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, layout.Width, layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true, edit: view);
            pose?.Invoke(scene);
            // The wallpaper under the scene: the window's root becomes a container holding it, then the scene.
            var c = gfx.Compositor;
            var size = new Vector2(layout.Width, layout.Height);
            wash = c.CreateLinearGradientBrush();
            wash.StartPoint = new Vector2(0f, 0f);
            wash.EndPoint = new Vector2(1f, 1f);
            var (from, to) = look.Dark
                ? (Color.FromArgb(255, 0x0B, 0x1E, 0x46), Color.FromArgb(255, 0x2A, 0x5C, 0xB0))
                : (Color.FromArgb(255, 0x8F, 0xBC, 0xEE), Color.FromArgb(255, 0xE6, 0xEF, 0xFA));
            wash.ColorStops.Add(c.CreateColorGradientStop(0f, from));
            wash.ColorStops.Add(c.CreateColorGradientStop(1f, to));
            wallpaper = c.CreateSpriteVisual();
            wallpaper.Size = size;
            wallpaper.Brush = wash;
            desktop = c.CreateContainerVisual();
            desktop.Size = size;
            var bar = target.Root;
            target.Root = desktop;
            desktop.Children.InsertAtBottom(wallpaper);
            desktop.Children.InsertAtTop(bar);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            var room = (int)MathF.Round(16f * layout.Scale);
            var strip = layout.PanelRect;
            var part = layout.Edge switch
            {
                Edge.Left => new RECT { left = 0, top = 0, right = (int)strip.Right + room, bottom = layout.Height },
                Edge.Right => new RECT { left = (int)strip.X - room, top = 0, right = layout.Width, bottom = layout.Height },
                Edge.Top => new RECT { left = 0, top = 0, right = layout.Width, bottom = (int)strip.Bottom + room },
                _ => new RECT { left = Math.Clamp(start, 0, layout.Width - 1), top = (int)strip.Y - room, right = layout.Width, bottom = layout.Height },
            };
            Assert.True(WindowShots.Save(window, file, part), $"{file} came back blank");
        }
        finally
        {
            scene?.Dispose();
            if (target is not null)
            {
                target.Root = null;
            }
            desktop?.Dispose();
            wallpaper?.Dispose();
            wash?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
