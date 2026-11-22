// tray: a notification icon dragged, on the dock's real scene and a real overflow flyout, shown on
// a desktop of its own and captured as the compositor draws them (WindowShots), to KARPO_PNG_DIR:
//   tray-drag-rest-<look>-<scale>.png     three icons and nothing hidden: no chevron, as on Windows 11;
//   tray-drag-chevron-<look>-<scale>.png  the same tray while one is dragged: the chevron stands there,
//                                         lit, for the icon (carried elsewhere) to be hidden;
//   tray-drag-along-<look>-<scale>.png    an icon moved along the tray: the chevron shows all the same;
//   tray-overflow-drag-<look>-<scale>.png the overflow mid-drag, its icons moved aside where the icon
//                                         would land in it;
//   tray-carry-<between|empty|into>-<edge>-<look>-<scale>.png
//                                         the bar, the overflow the drag opened and the icon carried
//                                         under the pointer, together (TrayCarrySheets);
// dark and light, at 100 % and 150 %. Every part is measured by the real Layout.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.Windows.S;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The tray in the end section, at the bar's end, as on Windows' taskbar.</summary>
    private static List<PartSpec> TrayBar() => [Spec("tray", BarKind.Tray) with { Section = BarSection.End }];

    /// <summary>Icons in shell32.dll that look like an app's: a network, a disc, a shield, a plug.</summary>
    private static readonly int[] TrayShellIcons = [13, 15, 18, 20, 23, 41];

    /// <summary>One of Windows' own icons at <paramref name="size"/> px, drawn as the tray draws an app's.</summary>
    private static Pixels? ShellIcon(int which, int size)
    {
        var icon = HICON.NULL;
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
        fixed (char* name = path)
        {
            if (SHDefExtractIconW(name, TrayShellIcons[which % TrayShellIcons.Length], 0, &icon, null, (uint)size).Value != S_OK
                || icon == HICON.NULL)
            {
                return null;
            }
        }
        try
        {
            return Tray.Render(icon, size);
        }
        finally
        {
            _ = DestroyIcon(icon);
        }
    }

    private static List<TrayIcon> DraggedTrayIcons(int count, int size) =>
        [.. Enumerable.Range(0, count).Select(index => new TrayIcon(
            $"pictures/{index}", HWND.NULL, (uint)index, Guid.Empty, 0x8000, 4, ShellIcon(index, size), $"App {index + 1}", Hidden: false, Identity: $"app{index}|0"))];

    [UiFact]
    public void TrayDragSheets()
    {
        WindowShots.OnHiddenDesktop("tray-drag", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    // The islands look: the tray's one card, its icons touching on it.
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var area = new RECT { left = 0, top = 0, right = (int)(480 * dpi / 96), bottom = (int)(dpi * 10) };
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                    var icons = DraggedTrayIcons(3, px);

                    // At rest, nothing hidden: no chevron.
                    var rest = new Content { Tray = icons, TrayOverflow = false };
                    var still = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, TrayBar(), rest);
                    Assert.False(still.TrayOverflow);
                    TrayShot(gfx, still, rest, look, null, $"tray-drag-rest-{name}.png");

                    // Dragged: the bars are laid out with the chevron (Content.TrayOverflow while trayDrag).
                    var dragging = new Content { Tray = icons, TrayOverflow = true };
                    var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, TrayBar(), dragging);
                    Assert.True(layout.TrayOverflow);
                    var chevron = layout.ItemRect(Hit.TrayOverflow);
                    Assert.False(chevron.IsEmpty);
                    // The second icon over the chevron, which lights: a drop there hides it.
                    TrayShot(gfx, layout, dragging, look, scene =>
                    {
                        scene.TrayDrag(new TrayDragView(icons[1].Key, -1));
                        scene.Highlight(Hit.TrayOverflow, false);
                    }, $"tray-drag-chevron-{name}.png");
                    // The first moved along the tray, past the second: the others close up, and the chevron stays.
                    TrayShot(gfx, layout, dragging, look, scene =>
                    {
                        scene.TrayDrag(new TrayDragView(icons[0].Key, 1));
                        // The second closes up into the first's place and the third stays: each moved
                        // from where it stands, on the bar's own line. The shift used to be set as the
                        // offset itself, and every icon left the tray for the bar's corner.
                        var first = layout.TrayIconRect(0);
                        var third = layout.TrayIconRect(2);
                        Assert.Equal(new System.Numerics.Vector3(first.X, first.Y, 0), scene.Registered($"tray:{TrayBar()[0].Key}|{icons[1].Key}")!.Offset);
                        Assert.Equal(new System.Numerics.Vector3(third.X, third.Y, 0), scene.Registered($"tray:{TrayBar()[0].Key}|{icons[2].Key}")!.Offset);
                        // No card per icon to move with it: the tray wears one under them all .
                        Assert.Null(scene.Registered($"tray:{TrayBar()[0].Key}|card:{icons[1].Key}"));
                    }, $"tray-drag-along-{name}.png");

                    // The overflow opened by resting there, the icons making room before its third.
                    var scale = dpi / 96f;
                    // As the dock opens it: the tray icons' size in the bar's squares, five to a row,
                    // the panel no larger than they are.
                    using var flyout = new Flyout(gfx, instance, 0)
                    {
                        StripIconSize = layout.TrayIconSize,
                        StripCell = layout.TrayOverflowCell,
                        GridColumns = Layout.TrayOverflowColumns,
                        FitsItsIcons = true,
                        GroupContent = true,
                        ContextClicks = true,
                        DragItems = true,
                        TellsPointer = true,
                    };
                    var hidden = DraggedTrayIcons(5, (int)MathF.Ceiling(layout.TrayIconSize));
                    List<FlyoutItem> items = [.. hidden.Select((icon, index) => new FlyoutItem(2000u + (uint)index, icon.Tip, icon.Icon is { } pixels ? new FlyoutIcon.Picture(pixels) : new FlyoutIcon.Glyph("")))];
                    var work = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1032 * scale) };
                    var anchor = new POINT { x = work.right - (int)(120 * scale), y = work.bottom };
                    flyout.Show(gfx, look, scale, items, FlyoutLayout.Grid, anchor, work, Edge.Bottom);
                    Assert.True(flyout.IsOpen);
                    // The place a drop there reads (DropIndex), taken before the room is made.
                    var third = Assert.NotNull(flyout.ItemOnScreen(2002));
                    Assert.Equal(2, flyout.DropIndex(new POINT { x = third.left + 2, y = (third.top + third.bottom) / 2 }));
                    flyout.DropGap(2);
                    WindowShots.Settle();
                    var file = $"tray-overflow-drag-{name}.png";
                    Assert.True(WindowShots.Save(flyout.Hwnd, file), $"{file} came back blank");
                    flyout.Hide();
                    WindowShots.Settle(50);
                }
            }
        });
    }

    /// <summary>
    /// tray: the tray grown and shrunk by a drag (Content.TrayMore), with Show desktop after it to make
    /// room, at 100 % and 150 %, along the bottom and down a side, to KARPO_PNG_DIR:
    ///   tray-grow-rest-…     three icons and the chevron, at rest;
    ///   tray-grow-nogrow-…   one from the overflow over the second, the block not grown: as it was;
    ///   tray-grow-in-…       the same with the cell it brings: the icons after the gap move on into it;
    ///   tray-grow-dropped-…  let go there: four icons, as long as the drag had it;
    ///   tray-grow-out-…      the second held over the chevron: the tray a cell shorter, closed up.
    /// </summary>
    [UiFact]
    public void TrayGrowSheets()
    {
        WindowShots.OnHiddenDesktop("tray-grow", (gfx, _) =>
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Left })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(true) with { Animations = false };
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var length = (int)(400 * dpi / 96);
                    var area = edge.IsVertical()
                        ? new RECT { right = (int)(dpi * 10), bottom = length }
                        : new RECT { right = length, bottom = (int)(dpi * 10) };
                    var name = $"{(edge.IsVertical() ? "side" : "bottom")}-{dpi * 100 / 96}";
                    var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                    var icons = DraggedTrayIcons(4, px);
                    List<PartSpec> specs =
                    [
                        Spec("tray", BarKind.Tray) with { Section = BarSection.Middle },
                        Spec("desktop", BarKind.ShowDesktop) with { Section = BarSection.Middle },
                    ];
                    var trayKey = specs[0].Key;
                    var three = icons.Take(3).ToList();
                    Content With(IReadOnlyList<TrayIcon> shown, int more) => new()
                    {
                        Tray = shown,
                        TrayOverflow = true,
                        TrayMore = more == 0 ? null : new Dictionary<string, int> { [trayKey] = more },
                    };
                    Layout Lay(Content content) => new(gfx, dpi, area, area, edge, config, look, specs, content);

                    var rest = With(three, 0);
                    var still = Lay(rest);
                    TrayShot(gfx, still, rest, look, null, $"tray-grow-rest-{name}.png");
                    TrayShot(gfx, still, rest, look, scene => scene.TrayDrag(new TrayDragView(null, 1)), $"tray-grow-nogrow-{name}.png");

                    var growing = With(three, 1);
                    var grown = Lay(growing);
                    var block = grown.PartAt(0);
                    Assert.Equal(still.PartAt(0).Width + grown.TrayStep, block.Width, 0.5f);
                    TrayShot(gfx, grown, growing, look, scene =>
                    {
                        scene.TrayDrag(new TrayDragView(null, 1, More: 1));
                        // The third moved on by a whole cell, into the cell the block grew by: inside it.
                        var third = grown.TrayIconRect(2);
                        var at = scene.Registered($"tray:{trayKey}|{three[2].Key}")!.Offset;
                        var end = grown.Vertical ? at.Y + third.Height : at.X + third.Width;
                        Assert.True(end <= block.Left + block.Width + 0.5f, $"the last icon ends at {end}, past the tray's end {block.Left + block.Width}");
                    }, $"tray-grow-in-{name}.png");

                    var four = With(icons, 0);
                    var dropped = Lay(four);
                    Assert.Equal(block.Width, dropped.PartAt(0).Width, 0.5f);
                    TrayShot(gfx, dropped, four, look, null, $"tray-grow-dropped-{name}.png");

                    var leaving = With(three, -1);
                    var shrunk = Lay(leaving);
                    Assert.Equal(still.PartAt(0).Width - shrunk.TrayStep, shrunk.PartAt(0).Width, 0.5f);
                    TrayShot(gfx, shrunk, leaving, look, scene =>
                    {
                        scene.TrayDrag(new TrayDragView(three[1].Key, -1, More: -1));
                        scene.Highlight(Hit.TrayOverflow, false);
                        // The third closed up into the second's place, inside the shorter block.
                        var second = shrunk.TrayIconRect(1);
                        Assert.Equal(new System.Numerics.Vector3(second.X, second.Y, 0), scene.Registered($"tray:{trayKey}|{three[2].Key}")!.Offset);
                    }, $"tray-grow-out-{name}.png");
                }
            }
        });
    }

    /// <summary>
    /// tray: an icon dragged off the bar, as the dock shows it (Dock.Tray.cs): the bar, the overflow
    /// the drag opened at once over the chevron and the icon carried under the pointer in its own
    /// window (TrayCarry), in one picture, to KARPO_PNG_DIR:
    ///   tray-carry-between-…  nothing hidden: the overflow one empty cell, the icon up off the bar
    ///                         beside it, its place on the bar open;
    ///   tray-carry-empty-…    the icon over the empty overflow's cell: the tray a cell shorter;
    ///   tray-carry-into-…     two icons hidden: the overflow has a cell more, the icon over it;
    /// along the bottom (dark and light) and down the left side (dark), at 100 % and 150 %.
    /// </summary>
    [UiFact]
    public void TrayCarrySheets()
    {
        WindowShots.OnHiddenDesktop("tray-carry", (gfx, instance) =>
        {
            Karpo.Tests.Signals.PreviewToolbarTests.Register(Preview.ClassName, instance);
            using var carry = new TrayCarry(gfx, instance);
            foreach (var (edge, dark) in new[] { (Edge.Bottom, true), (Edge.Bottom, false), (Edge.Left, true) })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var scale = dpi / 96f;
                    var area = edge.IsVertical()
                        ? new RECT { right = (int)(400 * scale), bottom = (int)(480 * scale) }
                        : new RECT { right = (int)(520 * scale), bottom = (int)(320 * scale) };
                    var name = $"{(edge.IsVertical() ? "side" : "bottom")}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var px = (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config));
                    var icons = DraggedTrayIcons(5, px);
                    List<PartSpec> specs = [Spec("tray", BarKind.Tray) with { Section = BarSection.Middle }];
                    var trayKey = specs[0].Key;
                    var shown = icons.Take(3).ToList();
                    Content With(int more) => new()
                    {
                        Tray = shown,
                        TrayOverflow = true,
                        TrayMore = more == 0 ? null : new Dictionary<string, int> { [trayKey] = more },
                    };
                    Layout Lay(Content content) => new(gfx, dpi, area, area, edge, config, look, specs, content);
                    var bar = Lay(With(0));
                    var work = edge.IsVertical()
                        ? area with { left = area.left + (int)bar.Thickness }
                        : area with { bottom = area.bottom - (int)bar.Thickness };
                    // Where the overflow opens: over the chevron's middle, on the bar's inner side.
                    POINT AnchorOf(Layout layout)
                    {
                        var chevron = layout.ItemRect(Hit.TrayOverflow);
                        Assert.False(chevron.IsEmpty);
                        return edge.IsVertical()
                            ? new POINT { x = work.left, y = layout.Y + (int)(chevron.Y + chevron.Height / 2f) }
                            : new POINT { x = layout.X + (int)(chevron.X + chevron.Width / 2f), y = work.bottom };
                    }
                    var cell = (int)bar.TrayOverflowCell;

                    // Nothing hidden: one empty cell; the second icon up off the bar, beside the overflow.
                    Shot(bar, With(0), new TrayDragView(shown[1].Key, -1), [], gap: -1, flyout =>
                    {
                        var panel = flyout.PanelsOnScreen.Panel;
                        Assert.Equal(1, Cells(panel, cell));
                        return edge.IsVertical()
                            ? new POINT { x = (work.left + panel.left) / 2 + cell / 2, y = panel.bottom + cell }
                            : new POINT { x = panel.left - cell, y = (panel.bottom + work.bottom) / 2 - cell / 2 };
                    }, $"tray-carry-between-{name}.png");
                    // Over the empty overflow's cell: a drop there hides it; the tray has closed up.
                    Shot(Lay(With(-1)), With(-1), new TrayDragView(shown[1].Key, -1, More: -1), [], gap: -1, flyout =>
                    {
                        var panel = flyout.PanelsOnScreen.Panel;
                        return new POINT { x = (panel.left + panel.right) / 2 + cell / 5, y = (panel.top + panel.bottom) / 2 - cell / 5 };
                    }, $"tray-carry-empty-{name}.png");
                    // Two hidden: a cell more than they need, the icon over it, where it would land.
                    var hidden = DraggedTrayIcons(5, (int)MathF.Ceiling(bar.TrayIconSize)).Skip(3).ToList();
                    Shot(Lay(With(-1)), With(-1), new TrayDragView(shown[1].Key, -1, More: -1), hidden, gap: 2, flyout =>
                    {
                        var panel = flyout.PanelsOnScreen.Panel;
                        Assert.Equal(3, Cells(panel, cell));
                        var last = Assert.NotNull(flyout.ItemOnScreen(2001));
                        return new POINT { x = last.right + (last.right - last.left) / 2, y = (last.top + last.bottom) / 2 };
                    }, $"tray-carry-into-{name}.png");

                    void Shot(Layout layout, Content content, TrayDragView view, IReadOnlyList<TrayIcon> overflow, int gap, Func<Flyout, POINT> pointer, string file)
                    {
                        var window = MakeWindow();
                        DesktopWindowTarget? target = null;
                        Scene? scene = null;
                        // As the dock opens the overflow for a drag from the bar (ShowFlyout).
                        using var flyout = new Flyout(gfx, instance, 0)
                        {
                            StripIconSize = layout.TrayIconSize,
                            StripCell = layout.TrayOverflowCell,
                            GridColumns = Layout.TrayOverflowColumns,
                            FitsItsIcons = true,
                            GroupContent = true,
                            ContextClicks = true,
                            DragItems = true,
                            TellsPointer = true,
                            SpareTiles = 1,
                        };
                        try
                        {
                            _ = SetWindowPos(window, HWND.NULL, layout.X, layout.Y, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
                            target = Target(gfx, window);
                            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
                            scene.TrayDrag(view);
                            // The icon taken from the bar is out of sight there: it is carried.
                            Assert.Equal(0f, scene.Registered($"tray:{trayKey}|{view.Key}")!.Opacity);
                            if (view.More < 0)
                            {
                                // The third closes up into the second's place at the bar's end too: its place
                                // at rest is past the bar there, and it wasn't drawn at all (Layout.TrayIconRect).
                                var second = layout.TrayIconRect(1);
                                var third = scene.Registered($"tray:{trayKey}|{shown[2].Key}");
                                Assert.NotNull(third);
                                Assert.Equal(new System.Numerics.Vector3(second.X, second.Y, 0), third.Offset);
                                Assert.Equal(1f, third.Opacity);
                            }
                            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
                            List<FlyoutItem> items = [.. overflow.Select((icon, index) => new FlyoutItem(2000u + (uint)index, icon.Tip, new FlyoutIcon.Picture(icon.Icon!)))];
                            // Opened over the chevron as the drag began, and moved with it where the tray
                            // has grown or shrunk since (Dock.TrayDragRebuilt).
                            var opened = AnchorOf(bar);
                            flyout.Show(gfx, look, scale, items, FlyoutLayout.Grid, opened, work, edge);
                            Assert.True(flyout.IsOpen);
                            var before = flyout.PanelsOnScreen.Panel;
                            var now = AnchorOf(layout);
                            flyout.Reanchor(now);
                            var after = flyout.PanelsOnScreen.Panel;
                            Assert.Equal((now.x - opened.x, now.y - opened.y), (after.left - before.left, after.top - before.top));
                            if (gap >= 0)
                            {
                                flyout.DropGap(gap);
                            }
                            var at = pointer(flyout);
                            carry.Show(icons[1].Icon, px, at);
                            WindowShots.Settle(250);
                            var panel = layout.PanelRect;
                            var strip = new RECT { left = layout.X + (int)panel.X, top = layout.Y + (int)panel.Y, right = layout.X + (int)panel.Right, bottom = layout.Y + (int)panel.Bottom };
                            var held = TrayCarrying.Corner(at, px);
                            var picture = new RECT { left = held.x, top = held.y, right = held.x + px, bottom = held.y + px };
                            var popup = flyout.PanelsOnScreen.Panel;
                            const int margin = 16;
                            var frame = new RECT
                            {
                                left = Math.Min(Math.Min(strip.left, popup.left), picture.left) - margin,
                                top = Math.Min(Math.Min(strip.top, popup.top), picture.top) - margin,
                                right = Math.Max(Math.Max(strip.right, popup.right), picture.right) + margin,
                                bottom = Math.Max(Math.Max(strip.bottom, popup.bottom), picture.bottom) + margin,
                            };
                            var (canvas, every) = WindowShots.Together([(window, strip), (flyout.Hwnd, popup)], frame, file, dark ? ((byte)32, (byte)32, (byte)32) : ((byte)243, (byte)243, (byte)243));
                            Assert.True(every, $"{file} came back blank");
                            // The carried icon over them: a capture has no alpha, so what it leaves black
                            // around the icon is what shows through on the screen.
                            byte[]? icon = null;
                            Assert.True(WindowShots.Save(carry.Hwnd, file, default, (pixels, _) => icon = pixels), $"{file}: the carried icon came back blank");
                            var wide = frame.right - frame.left;
                            for (var y = 0; y < px; y++)
                            {
                                for (var x = 0; x < px; x++)
                                {
                                    var from = (y * px + x) * 4;
                                    if (icon![from] == 0 && icon[from + 1] == 0 && icon[from + 2] == 0)
                                    {
                                        continue;
                                    }
                                    var to = ((picture.top - frame.top + y) * wide + picture.left - frame.left + x) * 4;
                                    Buffer.BlockCopy(icon, from, canvas, to, 4);
                                }
                            }
                            WindowShots.WritePng(canvas, wide, frame.bottom - frame.top, Path.Combine(Offscreen.Pictures, file));
                        }
                        finally
                        {
                            carry.Hide();
                            flyout.Hide();
                            scene?.Dispose();
                            _ = DestroyWindow(window);
                            WindowShots.Settle(50);
                        }
                    }
                }
            }
        });
    }

    /// <summary>tray: how many of the overflow's squares, <paramref name="cell"/> px, its panel holds across and down.</summary>
    private static int Cells(RECT panel, int cell) =>
        Math.Max(1, (panel.right - panel.left) / cell) * Math.Max(1, (panel.bottom - panel.top) / cell);

    /// <summary>The bar of <paramref name="layout"/> drawn with <paramref name="content"/>, <paramref name="pose"/> done to its scene, saved.</summary>
    private static void TrayShot(Gfx gfx, Layout layout, Content content, Look look, Action<Scene>? pose, string file)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
            pose?.Invoke(scene);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(250);
            var panel = layout.PanelRect;
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            Assert.True(WindowShots.Save(window, file, part), $"{file} came back blank");
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
