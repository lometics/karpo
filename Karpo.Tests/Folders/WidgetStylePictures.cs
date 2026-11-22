// The person's own folders before and after their lines are written as widgets with styles
// (docs\widgets.md §209): each folder of the config file KARPO_USER_CONFIG names — a copy of theirs,
// never the file itself — drawn as read in its own words and again as read back in today's, dark, at
// 150 %, one beside the other, and the two compared pixel for pixel (live content aside). To KARPO_PNG_DIR as
// widget-styles-<folder>.png (before | after). Nothing to do where the variable is not set.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Folders;

public sealed unsafe class WidgetStylePictures(ITestOutputHelper output)
{
    private const string Owner = "layout:default";
    private const float Scale = 1.5f;

    private static readonly Look Dark = new(
        Glass: false,
        Dark: true,
        Accent: Color.FromArgb(255, 0x00, 0x78, 0xD4),
        AccentLight2: Color.FromArgb(255, 0x99, 0xEB, 0xFF),
        AccentDark1: Color.FromArgb(255, 0x00, 0x5F, 0xB8));

    [UiFact]
    public void ThePersonsFoldersLookTheSameInTodaysWords()
    {
        if (Environment.GetEnvironmentVariable("KARPO_USER_CONFIG") is not { Length: > 0 } path || !File.Exists(path))
        {
            return;
        }
        var before = Configs.Repaired(File.ReadAllText(path));
        var after = Configs.Repaired(before.ToJson(), out var again);
        Assert.False(again);
        var folders = before.Layouts.SelectMany(layout => layout.Islands).Where(island => island.Folder is not null).Select(island => island.Id).Distinct().ToList();
        Assert.NotEmpty(folders);
        WindowShots.OnHiddenDesktop("widget-styles", (gfx, instance) =>
        {
            var work = new RECT { left = 0, top = 0, right = 3840, bottom = 2100 };
            foreach (var id in folders)
            {
                var shots = new List<(byte[] Pixels, int Width, int Height)>();
                // A first look at the folder warms what it reads live (the notifications come a moment
                // after it opens), so the two compared show the same; it is not kept.
                foreach (var (config, kept) in new[] { (before, false), (before, true), (after, true) })
                {
                    var island = config.Layouts.SelectMany(layout => layout.Islands).First(island => island.Id == id);
                    using var folder = new FolderIsland($"{Owner}/{id}", island, gfx, config);
                    using var flyout = new FolderFlyout(gfx, instance, 0);
                    flyout.Show(gfx, Dark, Scale, new POINT { x = 1920, y = 2100 }, work, Edge.Bottom, folder);
                    Assert.True(flyout.IsOpen);
                    WindowShots.Settle(400);
                    byte[]? pixels = null;
                    var width = 0;
                    Assert.True(WindowShots.Save(flyout.Hwnd, $"widget-styles-{id}-{shots.Count}.png", seen: (shown, w) => (pixels, width) = (shown, w)), $"{id} came back blank");
                    if (kept)
                    {
                        shots.Add((pixels!, width, pixels!.Length / 4 / width));
                    }
                    flyout.Hide();
                }
                WindowShots.SaveSideBySide(shots, $"widget-styles-{id}.png", (0x20, 0x20, 0x20), gap: 24);
                var (first, second) = (shots[0], shots[1]);
                Assert.Equal((first.Width, first.Height), (second.Width, second.Height));
                var differ = 0;
                for (var at = 0; at < first.Pixels.Length; at += 4)
                {
                    if (first.Pixels[at] != second.Pixels[at] || first.Pixels[at + 1] != second.Pixels[at + 1] || first.Pixels[at + 2] != second.Pixels[at + 2])
                    {
                        differ++;
                    }
                }
                output.WriteLine($"{id}: {first.Width}x{first.Height}, {differ} pixels differ");
                // What moves with the clock between two looks — the day view's now-line, a notification's
                // age — may differ by a few pixels; a line drawn otherwise would differ by far more.
                Assert.True(differ <= first.Pixels.Length / 4 / 200, $"{id}: {differ} pixels differ");
            }
        });
    }
}
