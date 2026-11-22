// No card behind the widgets unless one is chosen (§246): a
// bar drawn by the dock's real scene with real widgets over a desktop, as it looked before — Windows'
// card under every widget — and as it looks now, dark and light at 150 %, to
// KARPO_PNG_DIR as cards-divider-<before|after>-<look>-150.png. The bar is the default layout of the
// config at KARPO_BAR_CONFIG where one is given (read, never written), else a new bar with three
// widgets more among its own.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The cards the automatic looks drew before §246: CardBackgroundFillColorDefault and CardStrokeColorDefault.</summary>
    private static Palette CardsBefore(bool dark, Palette chosen) => chosen with
    {
        Card = chosen.Card ?? (dark ? new Argb(0x0D, 0xFF, 0xFF, 0xFF) : new Argb(0xB3, 0xFF, 0xFF, 0xFF)),
        CardOutline = chosen.CardOutline ?? (dark ? new Argb(0x19, 0, 0, 0) : new Argb(0x0F, 0, 0, 0)),
    };

    [UiFact]
    public void CardsAndDividerSheets()
    {
        var (config, bar) = CardsDividerBar();
        WindowShots.OnHiddenDesktop("cards-divider", (gfx, _) =>
        {
            const uint dpi = 144;
            foreach (var dark in new[] { true, false })
            {
                var chosen = AddListPictures.LookFor(dark).Wearing(config).Colors;
                foreach (var after in new[] { false, true })
                {
                    var drawn = config.Clone();
                    drawn.Height = 48;
                    var look = AddListPictures.LookFor(dark) with { Animations = false, Colors = after ? chosen : CardsBefore(dark, chosen) };
                    var (specs, islands) = Made(gfx, drawn, bar);
                    try
                    {
                        var content = new Content { Islands = islands };
                        var along = (int)(1920 * dpi / 96);
                        var area = new RECT { left = 0, top = 0, right = along, bottom = (int)(dpi * 10) };
                        var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, drawn, look, specs, content);
                        OverADesktop(gfx, layout, look, content, null, $"cards-divider-{(after ? "after" : "before")}-{(dark ? "dark" : "light")}-150.png");
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
        });
    }

    /// <summary>The config at KARPO_BAR_CONFIG and its default layout's widgets, or a new bar with three widgets more.</summary>
    private static (Config Config, List<BarItem> Bar) CardsDividerBar()
    {
        if (Environment.GetEnvironmentVariable("KARPO_BAR_CONFIG") is { Length: > 0 } path && File.Exists(path))
        {
            var read = Karpo.Tests.Core.Configs.Repaired(File.ReadAllText(path));
            var layout = read.Layouts.FirstOrDefault(layout => layout.Id == read.DefaultLayoutId) ?? read.Layouts[0];
            return (read, [.. layout.Islands]);
        }
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        var start = Math.Max(0, bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start));
        foreach (var (kind, at) in new[] { BarKind.WidgetsBoard, BarKind.TouchKeyboard, BarKind.NightLight }.Select((kind, at) => (kind, at)))
        {
            var item = kind.New();
            item.Id = $"cards-{at}";
            item.Section = BarSection.Start;
            bar.Insert(start + at, item);
        }
        return (new Config(), bar);
    }
}
