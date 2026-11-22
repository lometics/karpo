// app buttons drawn, on the dock's real tree (SceneTreeTests' harness): a window's own button says
// its title after its icon, cut short inside the room its button gives it; an icon alone says
// nothing; and what the bar has no room for stands behind "…", drawn after the last button shown,
// while the buttons behind it draw nothing.
//
// And sheets of the same bars — with room, squeezed, and too short — drawn from the layout the way
// the scene draws them, to KARPO_PNG_DIR (app-buttons-<bar>-<look>.png), so the labels can be looked
// at; every label drawn alone keeps its ink inside its room.
//
// Nothing is shown and nothing is launched: the window is never made visible, and the buttons'
// pictures are glyphs, so no app's icon is even asked for.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private static readonly string AppsKey = Config.IslandKey(null, "apps");

    /// <summary>Windows of four apps, a window each: two titles too long for four cells, one window with none, a short one.</summary>
    private static readonly (string Target, string? Title)[] OpenWindows =
    [
        (@"C:\Karpo\apps\mail.exe", "Inbox - Mail"),
        (@"C:\Karpo\apps\word.exe", "Quarterly report - final version (2) - Word"),
        (@"C:\Karpo\apps\calc.exe", null),
        (@"C:\Karpo\apps\notes.exe", "Notes"),
        (@"C:\Karpo\apps\word.exe", "Letter to the landlord about the heating - Word"),
    ];

    /// <summary>The bar's buttons for <see cref="OpenWindows"/>, a window each, as the dock hands them over (Dock.Build).</summary>
    private static Content AppContent() => new()
    {
        Tiles = [.. OpenWindows.Select(window => (new Item
        {
            Target = window.Target,
            Name = Path.GetFileNameWithoutExtension(window.Target),
            Kind = ItemKind.Shell,
            Keys = [],
            Picture = "\uE737",
        }, new Mark(1, Active: false, Attention: false)))],
        TileParts = [.. OpenWindows.Select(_ => (string?)null)],
        Labels = [.. OpenWindows.Select(window => window.Title)],
    };

    /// <summary>The Start button and the open apps on a 48 DIP bar <paramref name="length"/> px long, measured for real.</summary>
    private static Layout AppBar(Gfx gfx, int length, Content content, Look look)
    {
        // The sheet paints a card per button, which is the islands look.
        var config = new Config { Height = 48, Style = BarStyle.Islands };
        var area = new RECT { left = 0, top = 0, right = length, bottom = ScreenHeight };
        return new Layout(gfx, 96, area, area, Edge.Bottom, config, look, [Spec("start", BarKind.Start), new PartSpec(AppsKey, BarKind.Apps)], content, editing: false);
    }

    /// <summary>What the scene calls tile <paramref name="tile"/>'s label: its key, the same target counted as twins, and "/label".</summary>
    private static string LabelKey(int tile)
    {
        var target = OpenWindows[tile].Target;
        var twin = OpenWindows.Take(tile).Count(window => window.Target == target);
        return $"tile:{AppsKey}:{target}#{twin}/label";
    }

    /// <summary>Where <paramref name="visual"/> stands in the window, its containers counted with it.</summary>
    private static Vector3 At(Visual visual) => ShiftOf(visual) + visual.Offset;

    [UiFact]
    public void AWindowsButtonSaysItsTitleAndMoreStandsAfterTheLast()
    {
        var content = AppContent();
        OnItsOwnThread(gfx =>
        {
            var window = MakeWindow();
            DesktopWindowTarget? target = null;
            try
            {
                target = Target(gfx, window);

                // Room for every button: each titled one says its title, inside its room.
                var wide = AppBar(gfx, ScreenWidth, content, Dressed);
                Assert.Empty(wide.Overflowed);
                using (var scene = Scenes.Build(gfx, target, wide, content, Dressed, new Icons(), shown: true))
                {
                    for (var tile = 0; tile < OpenWindows.Length; tile++)
                    {
                        var words = scene.Registered(LabelKey(tile));
                        if (OpenWindows[tile].Title is null)
                        {
                            Assert.Null(words);
                            Assert.False(wide.Labelled(tile));
                            continue;
                        }
                        Assert.NotNull(words);
                        var box = wide.TileLabel(tile);
                        var at = At(words!);
                        var icon = wide.IconBox(Hit.Tile(tile), wide.Icon);
                        Assert.True(words!.IsVisible);
                        Assert.True(words.Size.X > 0f, $"tile {tile}'s label is empty");
                        Assert.InRange(at.X, box.X - 0.5f, box.X + 0.5f);
                        Assert.True(at.X >= icon.Right, $"tile {tile}'s label starts at {at.X}, inside its icon {icon}");
                        Assert.True(at.X + words.Size.X <= box.Right + 1f, $"tile {tile}'s label runs to {at.X + words.Size.X}, past its room {box}");
                        Assert.True(at.Y >= box.Y - 0.5f && at.Y + words.Size.Y <= box.Bottom + 0.5f, $"tile {tile}'s label stands at {at.Y}, outside {box}");
                    }
                    // A title longer than four cells hold is cut, not given more.
                    Assert.InRange(wide.TileCell(1).Width, (4f * wide.Slot) - 0.5f, (4f * wide.Slot) + 0.5f);
                    Assert.True(scene.Registered(LabelKey(1))!.Size.X <= wide.TileLabel(1).Width + 1f);
                    Assert.Null(scene.Registered("apps:more"));
                }

                // Room for a few: the rest stand behind "…", drawn after the last one shown, and
                // draw nothing themselves.
                var narrow = AppBar(gfx, 300, content, Dressed);
                Assert.NotEmpty(narrow.Overflowed);
                using (var scene = Scenes.Build(gfx, target, narrow, content, Dressed, new Icons(), shown: true))
                {
                    var more = scene.Registered("apps:more");
                    Assert.NotNull(more);
                    var button = narrow.ItemRect(Hit.AppOverflow);
                    var at = At(more!);
                    Assert.True(at.X >= button.X - 0.5f && at.X + more!.Size.X <= button.Right + 0.5f, $"\"…\" drawn at {at.X}, outside its button {button}");
                    var last = narrow.Overflowed[0] - 1;
                    Assert.True(last < 0 || at.X > narrow.ItemRect(Hit.Tile(last)).Right, "\"…\" is not after the last button shown");
                    foreach (var hidden in narrow.Overflowed)
                    {
                        Assert.Null(scene.Registered(LabelKey(hidden)));
                    }
                }
            }
            finally
            {
                _ = DestroyWindow(window);
            }
        });
    }

    /// <summary>
    /// Sheets of the app buttons a window each: with room for their titles, squeezed to fewer cells,
    /// and too short for them, "…" at the end — dark and light, drawn from the layout the way the
    /// scene draws them. Each label drawn alone, in its room, keeps its ink inside it.
    /// </summary>
    [UiFact]
    public void AppButtonSheets()
    {
        var content = AppContent();
        Offscreen.OnStaThread(gfx =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var (name, length) in new[] { ("roomy", 1000), ("squeezed", 600), ("short", 300) })
                {
                    var look = LookFor(dark);
                    var layout = AppBar(gfx, length, content, look);
                    var theme = FlyoutTheme.For(look);
                    var panel = layout.PanelRect;
                    RectF Moved(RectF rect) => rect with { X = rect.X - panel.X, Y = rect.Y - panel.Y };
                    Offscreen.Bitmap(panel.Width, panel.Height, dc =>
                    {
                        PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, panel.Width, panel.Height), 0, PopupPaint.Ink(theme.Base));
                        for (var tile = 0; tile < OpenWindows.Length; tile++)
                        {
                            if (layout.TileCell(tile) is not { IsEmpty: false } cell)
                            {
                                continue;
                            }
                            cell = Moved(cell);
                            PopupPaint.Fill(dc, PopupPaint.Rect(cell.X, cell.Y, cell.Width, cell.Height), layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                            var icon = Moved(layout.IconBox(Hit.Tile(tile), layout.Icon));
                            PopupPaint.Glyph(dc, gfx, "\uE737", MathF.Round(layout.Icon * 0.8f), theme.Text, PopupPaint.Rect(icon.X, icon.Y, icon.Width, icon.Height));
                            if (OpenWindows[tile].Title is { } title && layout.TileLabel(tile) is { IsEmpty: false } box)
                            {
                                box = Moved(box);
                                var size = PopupPaint.Measure(gfx, title, layout.LabelPx, Weight.Regular, box.Width);
                                PopupPaint.Text(dc, gfx, title, layout.LabelPx, Weight.Regular, theme.Text, box.X, box.Y + MathF.Floor((box.Height - size.Y) / 2f), box.Width);
                            }
                        }
                        if (layout.OverflowCell() is { IsEmpty: false } moreCell)
                        {
                            moreCell = Moved(moreCell);
                            PopupPaint.Fill(dc, PopupPaint.Rect(moreCell.X, moreCell.Y, moreCell.Width, moreCell.Height), layout.CardRadius, PopupPaint.Ink(theme.CardBackground));
                            var more = Moved(layout.IconBox(Hit.AppOverflow, layout.SmallIcon));
                            PopupPaint.Glyph(dc, gfx, "\uE712", layout.SmallIcon, theme.Text, PopupPaint.Rect(more.X, more.Y, more.Width, more.Height));
                        }
                    }, bitmap => Offscreen.Save(bitmap, Path.Combine(Offscreen.Pictures, $"app-buttons-{name}-{(dark ? "dark" : "light")}.png")));

                    for (var tile = 0; tile < OpenWindows.Length; tile++)
                    {
                        if (OpenWindows[tile].Title is not { } title || layout.TileLabel(tile) is not { IsEmpty: false } box)
                        {
                            continue;
                        }
                        var ink = (Left: 0f, Top: 0f, Right: 0f, Bottom: 0f);
                        var (width, height) = ((uint)MathF.Ceiling(box.Width), (uint)MathF.Ceiling(box.Height));
                        Offscreen.Bitmap(
                            width + 40,
                            height,
                            dc => PopupPaint.Text(dc, gfx, title, layout.LabelPx, Weight.Regular, theme.Text, 0f, 0f, box.Width),
                            bitmap => ink = Offscreen.InkOf(bitmap, width + 40, height));
                        Assert.True(ink.Right > ink.Left, $"{name}: tile {tile}'s label drew nothing");
                        Assert.True(ink.Right <= box.Width + 1f, $"{name}: tile {tile}'s label inks {ink.Right} px of a {box.Width} px room");
                    }
                }
            }
        });
    }
}
