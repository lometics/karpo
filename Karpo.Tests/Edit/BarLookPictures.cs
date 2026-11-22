// The whole bar as a person sees it, to hold beside Windows 11's own taskbar: the weather with two
// readings after it (one group), Start, search, task view, two pins and three open apps, three tray
// icons and the chevron, the input indicator, the network, sound and battery, the date and the time,
// and the bell. Drawn by the dock's real scene on a desktop of its own and captured as the compositor
// draws it (WindowShots), to KARPO_PNG_DIR as bar-look-<height>-<look>-<scale>.png, with the sizes
// the layout gave each kind of icon beside it in bar-look-<height>-<look>-<scale>.txt.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Globalization;
using System.Text;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void BarLookSheets()
    {
        WindowShots.OnHiddenDesktop("bar-look", (gfx, _) =>
        {
            foreach (var height in new[] { 32, 40, 48, 64 })
            {
                foreach (var dark in new[] { true, false })
                {
                    foreach (var dpi in new uint[] { 96, 144 })
                    {
                        var name = $"{height}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                        var look = AddListPictures.LookFor(dark) with { Animations = false };
                        var config = new Config { Height = height, Style = BarStyle.Islands };
                        var area = new RECT { left = 0, top = 0, right = (int)(1500 * dpi / 96), bottom = (int)(dpi * 10) };
                        var bar = Sections.DefaultLayout(pins: [@"C:\Karpo\apps\app0.exe", @"C:\Karpo\apps\app1.exe"], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
                        var weather = bar.FindIndex(item => item.Kind == BarKind.WidgetsBoard);
                        // Three widgets in the start's group (§511: no separator parts them).
                        BarItem[] extra = [BarKind.WidgetsBoard.New(), BarKind.TouchKeyboard.New(), BarKind.NightLight.New()];
                        for (var at = 0; at < extra.Length; at++)
                        {
                            var item = extra[at];
                            item.Id = $"extra-{item.Kind}".ToLowerInvariant();
                            item.Section = BarSection.Start;
                            bar.Insert(weather + 1 + at, item);
                        }
                        var (specs, islands) = Made(gfx, config, bar);
                        try
                        {
                            var tray = DraggedTrayIcons(3, (int)MathF.Ceiling(Layout.TrayIconPx(dpi, config)));
                            var content = new Content { Islands = islands, Tiles = AppGroupContent().Tiles, Tray = tray, TrayOverflow = true };
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                            ShootWith(gfx, layout, content, look, $"bar-look-{name}.png", default);
                            var edit = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content, editing: true);
                            OverADesktop(gfx, edit, look, content, new EditView(), $"bar-look-edit-{name}.png");
                            if (Environment.GetEnvironmentVariable("KARPO_PNG_DIR") is { Length: > 0 } dir)
                            {
                                var style = layout.StyleFor(0, look);
                                var fit = new Fit(style);
                                var text = new StringBuilder();
                                void Line(string what, float px) => text.AppendLine(CultureInfo.InvariantCulture, $"{what}: {px:0.##} px = {px * 96 / dpi:0.##} DIP");
                                Line("bar", layout.PanelRect.Height);
                                Line("card", layout.CellSide);
                                Line("app icon", Layout.IconPx(dpi, config));
                                Line("tray icon", layout.TrayIconSize);
                                Line("tray cell", layout.TrayCell);
                                Line("status icon", fit.SystemIcon);
                                Line("widget glyph", fit.Glyph());
                                Line("text", fit.Primary);
                                File.WriteAllText(Path.Combine(dir, $"bar-look-{name}.txt"), text.ToString());
                            }
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
