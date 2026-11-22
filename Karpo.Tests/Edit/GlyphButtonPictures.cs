// §497: the Snipping Tool's and Settings' glyph buttons beside folders that wear one glyph (the clipboard's, the
// icon face), the now-playing face beside the time over the date — on the person's 42 DIP bar and on a 48 DIP one,
// at 100 % and 150 %, dark and light, drawn by the dock's real scene with stand-ins for the track and the time.
// To KARPO_PNG_DIR as bar-glyphs-<dark|light>-<42|48>-<100|150>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// §497 (the person, 2026-11-03: "the snipping tool taskbar widget icon seems too big"): the Snipping Tool's and
    /// Settings' glyphs are a face's icon — the very box a folder's icon face draws its glyph in (IconFace: Fit.Glyph),
    /// not a Start-row button's 20 DIP in 24 — on the person's 42 DIP bar, a 48 and a 64.
    /// </summary>
    [UiFact]
    public void AGlyphButtonsIconIsAFacesIcon()
    {
        foreach (var height in new[] { 42, 48, 64 })
        {
            List<PartSpec> specs = [Spec("snip", BarKind.SnippingTool), Spec("settings", BarKind.WindowsSettings)];
            OnABar(height, specs, (layout, scene) =>
            {
                for (var part = 0; part < specs.Count; part++)
                {
                    var face = new Fit(layout.StyleFor(part, Dressed)).Glyph();
                    var sprite = scene.Registered($"windows:{specs[part].Key}");
                    Assert.NotNull(sprite);
                    Assert.True(MathF.Abs(sprite!.Size.X - face) < 0.5f && MathF.Abs(sprite.Size.Y - face) < 0.5f,
                        $"{height} DIP: {specs[part].Kind}'s glyph is in a {sprite.Size.X} x {sprite.Size.Y} box, a face's icon {face}");
                }
            }, editing: false);
        }
    }

    [UiFact]
    public void GlyphButtonsBesideFacesSheets()
    {
        var machine = new LayoutMachine([], NoPenNoTouch, WindowsTaskbar.OutOfTheBox);
        WindowShots.OnHiddenDesktop("bar-glyphs", (gfx, _) =>
        {
            Clock.Now = () => new DateTime(2026, 11, 3, 9, 41, 0);
            try
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var height in new[] { 42, 48 })
                    {
                        foreach (var dpi in new uint[] { 96, 144 })
                        {
                            var config = new Config { Height = height };
                            var area = new RECT { left = 0, top = 0, right = dpi == 96 ? 1600 : 2400, bottom = (int)(dpi * 10) };
                            List<BarItem> bar = [];
                            void Add(BarItem item)
                            {
                                item.Section = BarSection.Middle;
                                bar.Add(item);
                            }
                            Add(BarKind.SnippingTool.New());
                            Add(Presets.Folder(Presets.Find(Presets.ClipboardId)!, machine.Folders));
                            Add(BarKind.WindowsSettings.New());
                            Add(BarKind.QuickLink.New());
                            Add(Presets.Folder(Presets.Find(Presets.MediaId)!, machine.Folders));
                            Add(Presets.Folder(Presets.Find(Presets.NotificationCenterId)!, machine.Folders));
                            config.DefaultLayout.Islands = bar;
                            var look = AddListPictures.LookFor(dark) with { Animations = false };
                            MediaIsland.Stand = new MediaStand(Title: "Air on the G String", Artist: "Johann Sebastian Bach");
                            List<PartSpec> specs;
                            IslandCollection islands;
                            try
                            {
                                (specs, islands) = Made(gfx, config, bar);
                            }
                            finally
                            {
                                MediaIsland.Stand = null;
                            }
                            try
                            {
                                var content = new Content { Islands = islands };
                                var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                                var panel = layout.PanelRect;
                                var first = layout.Parts.First();
                                var last = layout.Parts.Last();
                                var margin = (int)(12 * dpi / 96f);
                                ShootWith(gfx, layout, content, look, $"bar-glyphs-{(dark ? "dark" : "light")}-{height}-{dpi * 100 / 96}.png",
                                    new RECT { left = (int)(panel.X + first.Left) - margin, top = (int)panel.Y, right = (int)(panel.X + last.Left + last.Width) + margin, bottom = (int)panel.Bottom });
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
            }
            finally
            {
                Clock.Now = Clock.Real;
            }
        });
    }
}
