// A folder of one, two and three lists side by side (2026-10-09, "several lists in one folder"),
// at rest and arranged, over a bottom bar and under a top one: real folder windows on a desktop of
// their own, captured as the compositor draws them (WindowShots) — each list a panel of its own, no
// title, the lists the divider gap apart (6 DIP, §505), and
// the "+" buttons outside the panels included, on the window's room round them — to KARPO_PNG_DIR as
// folder-lists-<edge>-<look>-<scale>.png, each a sheet of the three folders at rest over the
// same three arranged. What it holds as numbers is ListFolderTests'; this is for looking at.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class MultiListPictures
{
    private const string Owner = "layout:default";

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>The lists of a folder of <paramref name="count"/>: switches split by a divider, a clock over a divider and the battery, the graphs.</summary>
    private static BarItem Folder(int count)
    {
        var island = BarKind.FolderList.New();
        island.Id = $"lists{count}";
        island.Folder.Lists = [];
        ListKind[][] lists =
        [
            [ListKind.Heading, ListKind.AirplaneMode, ListKind.Divider, ListKind.NightLight, ListKind.EnergySaver],
            [ListKind.Clock, ListKind.Divider, ListKind.Battery],
            [ListKind.Cpu, ListKind.Memory],
        ];
        for (var list = 0; list < count; list++)
        {
            foreach (var kind in lists[list])
            {
                var line = kind.New();
                if (line.Heading is { } heading)
                {
                    heading.Text = "Switches";
                }
                island.Folder.Put(line, list);
            }
        }
        return island;
    }

    [UiFact]
    public void FolderListSheets()
    {
        WindowShots.OnHiddenDesktop("folder-lists", (gfx, instance) =>
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                foreach (var (dark, scale) in new[] { (true, 1.5f), (false, 1f) })
                {
                    var config = new Config();
                    var bar = (int)MathF.Round(48 * scale);
                    var work = edge == Edge.Bottom
                        ? new RECT { left = 0, top = 0, right = 3200, bottom = 1600 }
                        : new RECT { left = 0, top = bar, right = 3200, bottom = 1600 + bar };
                    var rows = new List<(byte[] Canvas, int Width, int Height)>();
                    foreach (var editing in new[] { false, true })
                    {
                        var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                        foreach (var count in new[] { 1, 2, 3 })
                        {
                            var anchor = new POINT { x = 1600, y = edge == Edge.Bottom ? 1600 : bar };
                            using var folder = new FolderIsland($"{Owner}/lists{count}", Folder(count), gfx, config);
                            using var flyout = new FolderFlyout(gfx, instance, 0);
                            flyout.Show(gfx, LookFor(dark), scale, anchor, work, edge, folder, FlyoutAlign.Center, editing);
                            Assert.True(flyout.IsOpen);
                            WindowShots.Settle(200);
                            byte[]? pixels = null;
                            var width = 0;
                            var file = $"folder-lists-{edge.ToString().ToLowerInvariant()}-{count}-{(editing ? "edit" : "view")}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                            Assert.True(WindowShots.Save(flyout.Hwnd, file, seen: (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
                            pictures.Add((pixels!, width, pixels!.Length / 4 / width));
                            flyout.Hide();
                        }
                        rows.Add(Row(pictures));
                    }
                    WindowShots.SaveSideBySide(Stack(rows), $"folder-lists-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png", (0x80, 0x80, 0x80));
                }
            }
        });
    }

    /// <summary>
    /// Three lists of one folder, narrow, medium and wide (2026-10-10, a width per list), each holding
    /// the month, the switches, screen time and a note — the lines whose content must follow the
    /// list's width — at rest over arranged (the width button beside each list's "+"), over a bottom
    /// bar and under a top one: folder-list-widths-&lt;edge&gt;-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void ListWidthSheets()
    {
        WindowShots.OnHiddenDesktop("folder-list-widths", (gfx, instance) =>
        {
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                foreach (var (dark, scale) in new[] { (true, 1f), (false, 1.5f) })
                {
                    var config = new Config();
                    var bar = (int)MathF.Round(48 * scale);
                    var work = edge == Edge.Bottom
                        ? new RECT { left = 0, top = 0, right = 3600, bottom = 2000 }
                        : new RECT { left = 0, top = bar, right = 3600, bottom = 2000 + bar };
                    var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                    foreach (var editing in new[] { false, true })
                    {
                        var island = BarKind.FolderList.New();
                        island.Id = "widths";
                        island.Folder!.Lists = [];
                        for (var list = 0; list < 3; list++)
                        {
                            foreach (var kind in new[] { ListKind.Month, ListKind.Toggles, ListKind.ScreenTime, ListKind.Note })
                            {
                                island.Folder.Put(kind.New(), list);
                            }
                        }
                        island.Folder.Widths = [FolderListWidth.Narrow, FolderListWidth.Medium, FolderListWidth.Wide];
                        var anchor = new POINT { x = 1800, y = edge == Edge.Bottom ? 2000 : bar };
                        using var folder = new FolderIsland($"{Owner}/widths", island, gfx, config);
                        using var flyout = new FolderFlyout(gfx, instance, 0);
                        flyout.Show(gfx, LookFor(dark), scale, anchor, work, edge, folder, FlyoutAlign.Center, editing);
                        Assert.True(flyout.IsOpen);
                        WindowShots.Settle(200);
                        byte[]? pixels = null;
                        var width = 0;
                        var file = $"folder-list-widths-{edge.ToString().ToLowerInvariant()}-{(editing ? "edit" : "view")}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        Assert.True(WindowShots.Save(flyout.Hwnd, file, seen: (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
                        pictures.Add((pixels!, width, pixels!.Length / 4 / width));
                        flyout.Hide();
                    }
                    WindowShots.SaveSideBySide(Stack(pictures), $"folder-list-widths-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png", (0x80, 0x80, 0x80));
                }
            }
        });
    }

    /// <summary>
    /// Folders with an empty list (2026-10-11): the switches, an empty list, the clock and battery;
    /// an empty list first; the switches and an empty list last; and a folder with nothing in it — at
    /// rest (the empty list its width of room, nothing drawn) over arranged (its panel saying "Add
    /// widgets here", its "+", width and remove), over a bottom bar and under a top one:
    /// folder-empty-lists-&lt;edge&gt;-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void EmptyListSheets()
    {
        WindowShots.OnHiddenDesktop("folder-empty-lists", (gfx, instance) =>
        {
            ListKind[] switches = [ListKind.AirplaneMode, ListKind.NightLight, ListKind.EnergySaver];
            ListKind[][][] shapes =
            [
                [switches, [], [ListKind.Clock, ListKind.Divider, ListKind.Battery]],
                [[], switches],
                [switches, []],
                [[]],
            ];
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                foreach (var (dark, scale) in new[] { (true, 1.5f), (false, 1f) })
                {
                    var config = new Config();
                    var bar = (int)MathF.Round(48 * scale);
                    var work = edge == Edge.Bottom
                        ? new RECT { left = 0, top = 0, right = 3200, bottom = 1600 }
                        : new RECT { left = 0, top = bar, right = 3200, bottom = 1600 + bar };
                    var rows = new List<(byte[] Canvas, int Width, int Height)>();
                    foreach (var editing in new[] { false, true })
                    {
                        var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                        for (var shape = 0; shape < shapes.Length; shape++)
                        {
                            var island = BarKind.FolderList.New();
                            island.Id = $"empty{shape}";
                            island.Folder!.Lists = [.. shapes[shape].Select(_ => new List<FolderCell>())];
                            for (var list = 0; list < shapes[shape].Length; list++)
                            {
                                foreach (var kind in shapes[shape][list])
                                {
                                    island.Folder.Put(kind.New(), list);
                                }
                            }
                            var anchor = new POINT { x = 1600, y = edge == Edge.Bottom ? 1600 : bar };
                            using var folder = new FolderIsland($"{Owner}/empty{shape}", island, gfx, config);
                            using var flyout = new FolderFlyout(gfx, instance, 0);
                            flyout.Show(gfx, LookFor(dark), scale, anchor, work, edge, folder, FlyoutAlign.Center, editing);
                            Assert.True(flyout.IsOpen);
                            WindowShots.Settle(200);
                            byte[]? pixels = null;
                            var width = 0;
                            var file = $"folder-empty-lists-{edge.ToString().ToLowerInvariant()}-{shape}-{(editing ? "edit" : "view")}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                            Assert.True(WindowShots.Save(flyout.Hwnd, file, seen: (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
                            pictures.Add((pixels!, width, pixels!.Length / 4 / width));
                            flyout.Hide();
                        }
                        rows.Add(Row(pictures));
                    }
                    WindowShots.SaveSideBySide(Stack(rows), $"folder-empty-lists-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png", (0x80, 0x80, 0x80));
                }
            }
        });
    }

    /// <summary>Pictures side by side in one, 16 px of grey between them, their tops level.</summary>
    private static (byte[] Canvas, int Width, int Height) Row(IReadOnlyList<(byte[] Canvas, int Width, int Height)> pictures)
    {
        const int gap = 16;
        var width = pictures.Sum(picture => picture.Width) + (gap * (pictures.Count - 1));
        var height = pictures.Max(picture => picture.Height);
        var canvas = Grey(width, height);
        var x0 = 0;
        foreach (var (pixels, w, h) in pictures)
        {
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(pixels, y * w * 4, canvas, ((y * width) + x0) * 4, w * 4);
            }
            x0 += w + gap;
        }
        return (canvas, width, height);
    }

    /// <summary>Rows one over another, as one picture handed to <see cref="WindowShots.SaveSideBySide"/> alone.</summary>
    private static List<(byte[] Canvas, int Width, int Height)> Stack(IReadOnlyList<(byte[] Canvas, int Width, int Height)> rows)
    {
        const int gap = 16;
        var width = rows.Max(row => row.Width);
        var height = rows.Sum(row => row.Height) + (gap * (rows.Count - 1));
        var canvas = Grey(width, height);
        var y0 = 0;
        foreach (var (pixels, w, h) in rows)
        {
            for (var y = 0; y < h; y++)
            {
                Buffer.BlockCopy(pixels, y * w * 4, canvas, (((y0 + y) * width) * 4), w * 4);
            }
            y0 += h + gap;
        }
        return [(canvas, width, height)];
    }

    private static byte[] Grey(int width, int height)
    {
        var canvas = new byte[width * height * 4];
        for (var at = 0; at < canvas.Length; at += 4)
        {
            canvas[at] = canvas[at + 1] = canvas[at + 2] = 0x80;
            canvas[at + 3] = 255;
        }
        return canvas;
    }
}
