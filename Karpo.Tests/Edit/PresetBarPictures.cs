// Each ready-made group as the bar shows it once added (Karpo.Core\Presets.cs): a new install's bar
// with the group added at the left section's "+" — after the weather — drawn by the dock's real
// scene with real widgets on a desktop of its own and captured as the compositor draws it
// (WindowShots), to KARPO_PNG_DIR as preset-bar-<id>-<dark|light>-<100|150>.png. Every group is one
// folder's button, showing the group's own icon (Connectivity's the network's, Stocks' a chart).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Core;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void PresetBarSheets()
    {
        WindowShots.OnHiddenDesktop("preset-bar", (gfx, _) =>
        {
            foreach (var preset in Karpo.Tests.Folders.PresetFolderPictures.Drawn())
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        var config = new Config { Height = 48 };
                        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch);
                        // The left section's "+": at its end.
                        var slot = new BarSlot(BarSection.Start, Sections.Range(bar, BarSection.Start).To);
                        var count = bar.Count;
                        var path = Presets.AddToBar(bar, slot, preset, PresetsTests.Laptop);
                        // One entry more, the folder, showing its icon.
                        Assert.Equal(count + 1, bar.Count);
                        var added = bar.Single(island => island.Id == path);
                        Assert.Equal(BarSection.Start, added.Section);
                        Assert.Equal(preset.Face?.Kind ?? FolderFaces.IconId, added.Folder!.Face.Kind);
                        var width = dpi == 96 ? 1600 : 2400;
                        var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            // Added at a "+", so in edit mode: arranged, as a face with nothing to show at rest
                            // (Removable devices with nothing plugged in, §291) still stands there.
                            foreach (var island in islands.Values.OfType<CellIsland>())
                            {
                                island.Arranged = true;
                            }
                            var content = new Content { Islands = islands };
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                            // What was added stands on the bar, shown.
                            var part = layout.PartIndex(Config.IslandKey(null, path));
                            Assert.True(part >= 0 && layout.Placed(part), $"{preset.Title}: {path} is not on the bar");
                            ShootWith(gfx, layout, content, look, $"preset-bar-{preset.Id}-{name}.png", default);
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
}
