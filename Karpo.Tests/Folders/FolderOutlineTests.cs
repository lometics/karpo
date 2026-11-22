// An open folder's outline (§514: the Flyouts', one setting for an open folder and a widget's flyout alike). §624 (the
// person, 2026-11-09: "i dont like borders around the individual widgets (only inside a folder i mean) so the border should
// always be defined for the folder itself"): a folder's lines have no outline of their own — §376's and §463's tests of a
// line's outline running all the way round it, and over a picture filling it, went with it —; the folder's border is its own.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Reflection;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed class FolderOutlineTests
{
    /// <summary>
    /// §514 (the person, 2026-11-05: "open widget flyouts and open folders should be the same setting and not treated
    /// differently"): the Flyouts' outline, set in that one section, is round an open folder and round a widget's open flyout (the
    /// tray's, a taskbar flyout, as the dock shows it: <see cref="Look.Flyout"/>), both — §567 no widget has one of its own.
    /// Read off the rim at the panel's top, in both themes at 150 %: flyouts-outline-&lt;look&gt;-&lt;folder|widget&gt;-150.png.
    /// </summary>
    [UiFact]
    public void TheFlyoutsOutlineIsRoundAnOpenFolderAndAWidgetsFlyout()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("flyouts-outline", (gfx, instance) =>
        {
            const float scale = 1.5f;
            var work = new RECT { left = 0, top = 0, right = 2400, bottom = 1500 };
            var anchor = new POINT { x = 1200, y = 1500 };
            var orange = new Argb(0xFF, 0xF7, 0x63, 0x0C);
            List<FlyoutItem> icons = [new(1, "Wi-Fi", new FlyoutIcon.Glyph("")), new(2, "Bluetooth", new FlyoutIcon.Glyph("")), new(3, "Sound", new FlyoutIcon.Glyph(""))];
            foreach (var dark in new[] { true, false })
            {
                var config = new Config();
                Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, new Karpo.Core.Paint(orange)));
                var island = BarKind.FolderList.New();
                island.Id = "outlined";
                foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.Battery })
                {
                    island.Folder!.Put(kind.New());
                }
                var tray = BarKind.Tray.New();
                config.DefaultLayout.Islands = [island, tray];
                {
                    var look = TestLooks.LookFor(dark).Wearing(config) with { Animations = false };
                    var name = $"flyouts-outline-{(dark ? "dark" : "light")}";
                    var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, config);
                    try
                    {
                        using var open = new FolderFlyout(gfx, instance, 0);
                        open.Show(gfx, look, scale, anchor, work, Edge.Bottom, folder, FlyoutAlign.Center);
                        WindowShots.Settle(300);
                        Check($"{name}-folder-150", open.Hwnd, open.PanelOnScreen, orange, failures);
                        open.Hide();
                    }
                    finally
                    {
                        folder.Dispose();
                    }
                    using var flyout = new Flyout(gfx, instance, 0) { Taskbar = true };
                    flyout.Show(gfx, look.Flyout(), scale, icons, FlyoutLayout.Grid, anchor, work, Edge.Bottom);
                    WindowShots.Settle(300);
                    Check($"{name}-widget-150", flyout.Hwnd, flyout.OpenPanels[0].Panel, orange, failures);
                    flyout.Hide();
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));

        // The panel's top rim, at its middle, read off the window: some pixel of its first few rows is the outline.
        static void Check(string name, HWND window, RECT panel, Argb outline, List<string> failures)
        {
            var extent = WindowShots.Extent(window);
            const int margin = 6;
            var part = new RECT { left = panel.left - extent.left - margin, top = panel.top - extent.top - margin, right = panel.right - extent.left + margin, bottom = panel.bottom - extent.top + margin };
            byte[] pixels = [];
            var width = 0;
            Assert.True(WindowShots.Save(window, $"{name}.png", part, (seen, w) => (pixels, width) = (seen, w)));
            Argb Pixel(int x, int y)
            {
                var at = ((y * width) + x) * 4;
                return new Argb(0xFF, pixels[at + 2], pixels[at + 1], pixels[at]);
            }
            var middle = width / 2;
            var nearest = Enumerable.Range(0, 2 * margin).Select(y => Pixel(middle, y)).MinBy(seen => Apart(seen, outline));
            if (Apart(nearest, outline) > 48)
            {
                failures.Add($"{name}: its rim is {nearest} at its top, not its outline {outline}");
            }
        }
    }

    private static int Apart(Argb one, Argb other) => Math.Abs(one.R - other.R) + Math.Abs(one.G - other.G) + Math.Abs(one.B - other.B);
}
