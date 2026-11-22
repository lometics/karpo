// A list folder's line under the pointer, as the compositor really draws it (backlog 21): the real
// FolderFlyout on a hidden desktop, the pointer moved onto each pin's line in turn â€” a pin of an app
// and a pin of a folder â€” and the window captured (WindowShots) at rest and with each line lit.
//
// The fill a line lights with was right (ListLineLookTests) and the flyout said the line was hot, yet
// on the screen the line stayed as it was: a list's line wore no brush at all at rest, and a sprite
// shape with neither fill nor stroke that is given a fill later is not drawn again by the compositor,
// except now and then for the first line touched. A line at rest wears a clear brush instead.
//
// Pictures go to KARPO_PNG_DIR as list-line-hover-<dark|light>-<100|150>-<rest|app|folder>.png.

using System.Reflection;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

public sealed unsafe class ListLineHoverShots
{
    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    private static CellItem Pin(string id, string target)
    {
        var pin = BarKind.Pin.New();
        pin.Id = id;
        pin.Pin!.Target = target;
        pin.Pin.Navigation = PinFolderNavigation.Contents;
        return pin.AsCell();
    }

    /// <summary>A list folder: a heading over a pin of an app and a pin of a folder.</summary>
    private static BarItem List()
    {
        var kind = Karpo.Tests.Core.Configs.FolderKind(FolderView.List);
        var island = kind.New();
        island.Id = "sub";
        island.Folder = new FolderOptions();
        var heading = ListKind.Heading.New();
        heading.Id = "head";
        heading.Heading!.Text = "Work";
        // notepad and Windows' folder by the paths Windows gives them, as every other test writes them:
        // a pin is resolved once a process, by its target whatever its case (PinItems).
        foreach (var cell in new[] { heading, Pin("app", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe")), Pin("folder", Environment.GetFolderPath(Environment.SpecialFolder.Windows)) })
        {
            Assert.NotNull(Karpo.Core.Folders.Add(kind, island.Folder, cell));
        }
        return island;
    }

    private static nint At(HWND window, int x, int y) => WindowShots.Client(window, x, y) is var at ? (nint)(((at.y & 0xFFFF) << 16) | (at.x & 0xFFFF)) : 0;

    /// <summary>The most any channel of a pixel inside <paramref name="row"/> (window px) differs between two captures.</summary>
    private static int Apart((byte[] Pixels, int Width) one, (byte[] Pixels, int Width) other, (int Left, int Top, int Right, int Bottom) row)
    {
        var width = one.Width;
        var most = 0;
        for (var y = row.Top; y < row.Bottom; y++)
        {
            for (var x = row.Left; x < row.Right; x++)
            {
                var at = ((y * width) + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    most = Math.Max(most, Math.Abs(one.Pixels[at + channel] - other.Pixels[at + channel]));
                }
            }
        }
        return most;
    }

    [UiFact]
    public void EveryPinsLineLightsOnTheScreen()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("list-line-hover", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1700, y = 1500 };
            using var flyout = new FolderFlyout(gfx, instance, 0);
            foreach (var dark in new[] { true, false })
            {
                foreach (var scale in new[] { 1f, 1.5f })
                {
                    var island = List();
                    var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Config());
                    try
                    {
                        flyout.Show(gfx, LookFor(dark), scale, anchor, work, Karpo.Core.Edge.Bottom, folder, FlyoutAlign.End);
                        WindowShots.Settle(300);
                        var name = $"{(dark ? "dark" : "light")}-{scale * 100:0}";
                        var rest = $"list-line-hover-{name}-rest.png";
                        (byte[], int) atRest = default;
                        Assert.True(WindowShots.Save(flyout.Hwnd, rest, default, (pixels, width) => atRest = (pixels, width)));
                        var plan = (FolderPlan)typeof(FolderFlyout).GetField("plan", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(flyout)!;
                        RECT box;
                        box = WindowShots.Extent(flyout.Hwnd);
                        var room = ((box.right - box.left) - plan.Width) / 2;
                        var ids = folder.Tiles.Select(tile => tile.Cell.Island.Id).ToList();
                        foreach (var id in new[] { "app", "folder" })
                        {
                            var card = plan.Cards[ids.IndexOf(id)];
                            var middle = (int)(room + ((card.top + card.bottom) / 2));
                            flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout.Hwnd, (int)(room + card.right) - 4, middle));
                            flyout.Message(WM.WM_MOUSEMOVE, 0, At(flyout.Hwnd, (int)(room + card.right) - 3, middle));
                            WindowShots.Settle(300);
                            var lit = $"list-line-hover-{name}-{id}.png";
                            (byte[], int) shown = default;
                            Assert.True(WindowShots.Save(flyout.Hwnd, lit, default, (pixels, width) => shown = (pixels, width)));
                            // The line's right half, where neither its icon nor its name stands.
                            var row = ((int)(room + ((card.left + card.right) / 2)), (int)(room + card.top) + 3, (int)(room + card.right) - 6, (int)(room + card.bottom) - 3);
                            var apart = Apart(atRest, shown, row);
                            if (apart < 5)
                            {
                                failures.Add($"{name}: the {id} pin's line under the pointer is {apart} levels from itself at rest");
                            }
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
