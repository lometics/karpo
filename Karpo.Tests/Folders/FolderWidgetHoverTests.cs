// A pin showing its folder's contents under the pointer (Karpo.Dock\Islands\FolderContentsView.cs, PinTile;
// CellIsland.LightsItsRows) — the "Folder contents" widget it went into, which G25 made light its rows
// only. The person: "the folder contents widget when hovered over shows the whole widget in the hover
// state, this should not be, just the folders should have the hover over state." So only the item's
// row under the pointer lights: the line of a list folder never does, under the pointer or pressed — the "double light" of a lit line with a lit row on it is gone. A click
// beside the rows still opens the folder (FolderPinContentsTests).
//
// The rule is plain (FolderFlyout.FillOf), tested first; then the real folder on a desktop of its own,
// pointer beside the rows and on a row, captured as the compositor draws it (WindowShots) to
// KARPO_PNG_DIR as folder-widget-hover-list-<dark|light>-<100|150>-<rest|beside|row>.png, and
// measured: beside the rows the widget's fill does not change at all; on a row, the row changes and
// the fill above it does not. The items' icons are left out of the measure (FillApart says why).
//
// The window runs need Direct2D and a window station: KARPO_UI_TESTS=1. Nothing reads a real folder:
// the folder source is FakeFolders'.

using System.Reflection;
using Karpo.Dock;
using Karpo.Dock.Drives;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using CardFill = Karpo.Dock.FolderFlyout.CardFill;
using Color = Windows.UI.Color;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

[Collection(FolderSourceState.Collection)]
public sealed unsafe class FolderWidgetHoverTests
{
    private const string Projects = @"C:\Users\Ann\Projects";

    /// <summary>
    /// A widget whose rows light themselves keeps its line as it is at rest, under the pointer and
    /// pressed; everything else lights as it always has.
    /// </summary>
    [Fact]
    public void AWidgetWhoseRowsLightNeverLightsAsAWhole()
    {
        foreach (var hot in new[] { false, true })
        {
            foreach (var pressed in new[] { false, true })
            {
                Assert.Equal(CardFill.None, FolderFlyout.FillOf(hot, pressed, quiet: false, still: true));
            }
        }
        // Without it, as before.
        Assert.Equal(CardFill.LineHover, FolderFlyout.FillOf(hot: true, pressed: false, quiet: false, still: false));
    }

    /// <summary>A folder's contents, or a pin, pointed at Projects.</summary>
    internal static T ShowingContents<T>(T item)
        where T : WidgetItem
    {
        item.Pin!.Target = Projects;
        return item;
    }

    /// <summary>
    /// A folder's contents say their rows light, as a line; a pin to the same folder, and a widget that
    /// is one thing to press, do not.
    /// </summary>
    [UiFact]
    public void AFoldersContentsLightTheirRows()
    {
        using var stand = new FolderSourceState(FakeFolders.Projects().Source);
        Offscreen.OnStaThread(gfx =>
        {
            var row =(CellIsland)Karpo.Dock.Islands.CreateLine("layout:default/folder/files", ShowingContents(ListKind.FolderContents.New()), ListKind.FolderContents, gfx, new Config())!;
            var plain = (CellIsland)Karpo.Dock.Islands.CreateLine("layout:default/folder/plain", ShowingContents(ListKind.Pin.New()), ListKind.Pin, gfx, new Config())!;
            var heading = (CellIsland)Karpo.Dock.Islands.CreateLine("layout:default/folder/head", ListKind.Heading.New(), ListKind.Heading, gfx, new Config())!;
            Assert.True(row.LightsItsRows);
            Assert.False(plain.LightsItsRows);
            Assert.False(heading.LightsItsRows);
            row.Dispose();
            plain.Dispose();
            heading.Dispose();
        });
    }

    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>A list folder holding Projects' contents: a line of five rows.</summary>
    private static BarItem Holding()
    {
        var island = BarKind.FolderList.New();
        island.Id = "files";
        island.Folder = new FolderOptions();
        var widget = ShowingContents(ListKind.FolderContents.New());
        // Tall enough for all of Projects' items: held shorter it scrolls (§461), and the pointer on it shows the overlay scrollbar.
        widget.Height = LineHeight.Exactly(24);
        widget.Id = "contents";
        _ = Karpo.Core.Folders.Add(BarKind.FolderList, island.Folder, widget) ?? throw new Xunit.Sdk.XunitException("the widget found no place");
        return island;
    }

    private static nint At(HWND window, int x, int y) => WindowShots.Client(window, x, y) is var at ? (nint)(((at.y & 0xFFFF) << 16) | (at.x & 0xFFFF)) : 0;

    /// <summary>The most any channel of a pixel inside the rows <paramref name="top"/> to <paramref name="bottom"/> and columns <paramref name="left"/> to <paramref name="right"/> differs between two captures.</summary>
    private static int Apart((byte[] Pixels, int Width) one, (byte[] Pixels, int Width) other, int left, int top, int right, int bottom)
    {
        var most = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var at = ((y * one.Width) + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    most = Math.Max(most, Math.Abs(one.Pixels[at + channel] - other.Pixels[at + channel]));
                }
            }
        }
        return most;
    }

    /// <summary>The colour most pixels inside the rectangle are.</summary>
    private static (byte B, byte G, byte R) Commonest((byte[] Pixels, int Width) one, int left, int top, int right, int bottom)
    {
        var counts = new Dictionary<int, int>();
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var at = ((y * one.Width) + x) * 4;
                var colour = one.Pixels[at] | (one.Pixels[at + 1] << 8) | (one.Pixels[at + 2] << 16);
                counts[colour] = counts.GetValueOrDefault(colour) + 1;
            }
        }
        var most = counts.MaxBy(pair => pair.Value).Key;
        return ((byte)most, (byte)(most >> 8), (byte)(most >> 16));
    }

    /// <summary>
    /// The most any pixel of the widget's own fill — those within two levels of <paramref name="plain"/>
    /// in <paramref name="one"/>, inside the rows <paramref name="top"/> to <paramref name="bottom"/>
    /// and columns <paramref name="left"/> to <paramref name="right"/> — differs in <paramref name="other"/>,
    /// and how many such pixels there are. What is drawn on the fill is left out: the items' icons are
    /// pictures scaled down from 40 px, and Direct2D's scaling comes out a few levels apart from one
    /// drawing to the next as the compositor hands the line a different place in its atlas each time
    /// (a pointer beside the rows draws the line again, the same line). A line that lit
    /// changes every pixel of its fill, and that is what this reads.
    /// </summary>
    private static (int Most, int Pixels) FillApart((byte[] Pixels, int Width) one, (byte[] Pixels, int Width) other, (byte B, byte G, byte R) plain, int left, int top, int right, int bottom)
    {
        var most = 0;
        var pixels = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                var at = ((y * one.Width) + x) * 4;
                if (Math.Abs(one.Pixels[at] - plain.B) > 2 || Math.Abs(one.Pixels[at + 1] - plain.G) > 2 || Math.Abs(one.Pixels[at + 2] - plain.R) > 2)
                {
                    continue;
                }
                pixels++;
                for (var channel = 0; channel < 3; channel++)
                {
                    most = Math.Max(most, Math.Abs(one.Pixels[at + channel] - other.Pixels[at + channel]));
                }
            }
        }
        return (most, pixels);
    }

    /// <summary>
    /// In a list folder, in both themes at 100 and 150 %: the pointer beside the
    /// rows changes nothing on the widget, and on a row it lights that row and nothing above it.
    /// </summary>
    [UiFact]
    public void OnlyTheRowUnderThePointerLights()
    {
        var failures = new List<string>();
        var folders = FakeFolders.Projects();
        using var stand = new FolderSourceState(folders.Source);
        WindowShots.OnHiddenDesktop("folder-widget-hover", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1700, y = 1500 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var island = Holding();
                    var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Config());
                    try
                    {
                        foreach (var tile in folder.Tiles)
                        {
                            _ = tile.Island.Refresh(gfx);
                        }
                        var widget = ((FolderContentsLine)folder.Tiles.Single().Island).Contents;
                        Assert.Equal(FolderListState.Listed, widget.Contents.State);
                        flyout.Show(gfx, LookFor(dark), scale, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
                        WindowShots.Settle(300);
                        var name = $"folder-widget-hover-list-{(dark ? "dark" : "light")}-{scale * 100:0}";
                        (byte[], int) rest = default;
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-rest.png", default, (pixels, width) => rest = (pixels, width)));
                        var plan = (FolderPlan)typeof(FolderFlyout).GetField("plan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                        RECT box;
                        box = WindowShots.Extent(flyout.Hwnd);
                        var room = (int)(((box.right - box.left) - plan.Width) / 2);
                        var card = plan.Cards[0];
                        var (left, top, right, bottom) = (room + (int)card.left, room + (int)card.top, room + (int)card.right, room + (int)card.bottom);

                        // Beside the rows: in the line's corner, the folder's own row of it.
                        var beside = (X: right - 6, Y: top + 6);
                        flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout.Hwnd, beside.X - 1, beside.Y));
                        flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout.Hwnd, beside.X, beside.Y));
                        WindowShots.Settle(300);
                        Assert.Equal(FolderContentsView.NoTarget, widget.Hot);
                        (byte[], int) besides = default;
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-beside.png", default, (pixels, width) => besides = (pixels, width)));
                        // The widget's own fill at rest: the colour most of it is.
                        var plain = Commonest(rest, left + 1, top + 1, right - 1, bottom - 1);
                        var (whole, fill) = FillApart(rest, besides, plain, left + 1, top + 1, right - 1, bottom - 1);
                        if (fill < (right - left) * (bottom - top) / 2)
                        {
                            failures.Add($"{name}: only {fill} pixels of the widget are its plain fill");
                        }
                        if (whole > 2)
                        {
                            failures.Add($"{name}: the pointer beside the rows changed the widget by {whole} levels");
                        }

                        // Down the middle of the widget to the first item's row, and to its end.
                        var middle = (left + right) / 2;
                        int rowTop = -1;
                        int rowBottom = -1;
                        for (var y = top + 1; y < bottom - 1; y++)
                        {
                            flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout.Hwnd, middle, y));
                            var on = widget.Hot == FolderContentsView.OpenTarget;
                            if (on && rowTop < 0)
                            {
                                rowTop = y;
                            }
                            else if (!on && rowTop >= 0)
                            {
                                rowBottom = y;
                                break;
                            }
                        }
                        Assert.True(rowTop > top && rowBottom > rowTop, $"{name}: no item's row found down the widget");
                        flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout.Hwnd, middle, (rowTop + rowBottom) / 2));
                        WindowShots.Settle(300);
                        Assert.Equal(FolderContentsView.OpenTarget, widget.Hot);
                        (byte[], int) lit = default;
                        Assert.True(WindowShots.Save(flyout.Hwnd, $"{name}-row.png", default, (pixels, width) => lit = (pixels, width)));
                        // The row lights: its right part, where neither its icon nor its name stands, changed.
                        var row = Apart(rest, lit, middle + ((right - middle) / 2), rowTop + 2, right - 8, rowBottom - 2);
                        if (row < 5)
                        {
                            failures.Add($"{name}: the row under the pointer is {row} levels from itself at rest");
                        }
                        // The widget above the row — its heading, its line — did not.
                        var (above, _) = FillApart(rest, lit, plain, left + 1, top + 1, right - 1, rowTop - 3);
                        if (above > 2)
                        {
                            failures.Add($"{name}: with the pointer on a row, the widget above it changed by {above} levels");
                        }
                        flyout.Hide();
                    }
                    finally
                    {
                        folder.Dispose();
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }
}
