// A list folder grouped by headings (HeadingTile, 2026-10-11): each heading its own height, a
// section heading's rather than a row's, one of them never named and saying "Heading" — real folder
// windows on a desktop of their own, at rest and arranged, light and dark, to KARPO_PNG_DIR as
// folder-headings-<scale>.png. What it holds as numbers is ListLineLookTests'; this is for looking at.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class HeadingPictures
{
    private static Look LookFor(bool dark) => new(
        Glass: false,
        Dark: dark,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    /// <summary>Connections under a heading, then an unnamed heading over the display's switches, a divider, and the sound under one more.</summary>
    private static BarItem Folder()
    {
        var island = BarKind.FolderList.New();
        island.Id = "headings";
        (ListKind Kind, string? Heading)[] lines =
        [
            (ListKind.Heading, "Connections"), (ListKind.WifiSwitch, null), (ListKind.BluetoothSwitch, null), (ListKind.AirplaneMode, null),
            (ListKind.Heading, ""), (ListKind.NightLight, null), (ListKind.EnergySaver, null),
            (ListKind.Divider, null),
            (ListKind.Heading, "Sound"), (ListKind.SoundSwitch, null), (ListKind.MicrophoneSwitch, null),
        ];
        foreach (var (kind, words) in lines)
        {
            var line = kind.New();
            if (line.Heading is { } heading)
            {
                heading.Text = words ?? "";
            }
            island.Folder.Put(line, 0);
        }
        return island;
    }

    [UiFact]
    public void HeadingSheets()
    {
        WindowShots.OnHiddenDesktop("folder-headings", (gfx, instance) =>
        {
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var config = new Config();
                var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1600 };
                var pictures = new List<(byte[] Canvas, int Width, int Height)>();
                foreach (var dark in new[] { false, true })
                {
                    foreach (var editing in new[] { false, true })
                    {
                        using var folder = new FolderIsland("layout:default/headings", Folder(), gfx, config);
                        using var flyout = new FolderFlyout(gfx, instance, 0);
                        flyout.Show(gfx, LookFor(dark), scale, new POINT { x = 1200, y = 1600 }, work, Edge.Bottom, folder, FlyoutAlign.Center, editing);
                        Assert.True(flyout.IsOpen);
                        WindowShots.Settle(200);
                        byte[]? pixels = null;
                        var width = 0;
                        var file = $"folder-headings-{(editing ? "edit" : "view")}-{(dark ? "dark" : "light")}-{(int)(scale * 100)}.png";
                        Assert.True(WindowShots.Save(flyout.Hwnd, file, seen: (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
                        pictures.Add((pixels!, width, pixels!.Length / 4 / width));
                        flyout.Hide();
                    }
                }
                WindowShots.SaveSideBySide(pictures, $"folder-headings-{(int)(scale * 100)}.png", (0x80, 0x80, 0x80));
            }
        });
    }
}
