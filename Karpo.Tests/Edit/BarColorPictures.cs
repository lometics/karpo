// The bar in colours of one's own (Karpo.Core\Tones.cs, docs\widgets.md §57, §405): the dock's real
// scene with real widgets, shown on a desktop of its own and captured as the
// compositor draws it (WindowShots), to KARPO_PNG_DIR as bar-colors-<palette>-<look>.png, one widget
// lit as under the pointer so the highlight shows. "automatic" is Windows' own colours, to set the
// others beside.
//
// And a colour picked while the bar stands: the scene sets its brushes in place (Scene.Recolour) and
// shows what a scene built in those colours shows, bar-colors-recoloured-<look>.png, with nothing
// built again.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// §405: the colours the sheets show, each derived from three in the mode drawn in: none, two solid bars, one over
    /// the desktop's blur, and the grid's presets (Karpo.Core\ColorPalette.cs), each one click.
    /// </summary>
    private static readonly (string Name, Material Material, Dressing Tones)[] Palettes =
    [
        ("automatic", Material.Opaque, default),
        ("navy", Material.Opaque, Bar("#FF0F1B2D", "#B31E3A5F", "#FFDCE8F5", outline: "#52DCE8F5")),
        ("warm", Material.Opaque, Bar("#FF2A1F1A", "#B3463A33", accent: "#FFFF8C00")),
        ("see-through", Material.Acrylic, Bar("#B3203040", "#B3364656")),
        ("presets", Material.Acrylic, Bar(ColorPalette.Presets[10].ToString(), "#B3264A75", accent: ColorPalette.Blue.ToString())),
    ];

    /// <summary>§459: a bar of <paramref name="ground"/>, its widgets' cards <paramref name="card"/>, their text, accent and outline.</summary>
    private static Dressing Bar(string ground, string card, string? text = null, string? accent = null, string? outline = null) =>
        new(new PartPaints(new Karpo.Core.Paint(A(ground))), new PartPaints(new Karpo.Core.Paint(A(card)), outline is null ? default : new Karpo.Core.Paint(A(outline)),
            text is null ? default : new Karpo.Core.Paint(A(text)), accent is null ? default : new Karpo.Core.Paint(A(accent))));

    private static Argb A(string text) => Argb.Parse(text)!.Value;

    [UiFact]
    public void BarColorSheets()
    {
        WindowShots.OnHiddenDesktop("bar-colors", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var (name, material, tones) in Palettes)
                {
                    var look = (AddListPictures.LookFor(dark) with { Animations = false, Material = material, Opacity = 0.7f }).Dressed(tones);
                    WithColorBar(gfx, look, (layout, content) =>
                        ShootColors(gfx, layout, content, look, null, $"bar-colors-{name}-{(dark ? "dark" : "light")}.png"));
                }
            }
        });
    }

    /// <summary>
    /// A colour picked while the bar stands: its brushes take it in place, and it shows what a bar built in it shows.
    /// The bar it starts from has cards in other colours of one's own: a bar with none chosen draws no card
    /// (§246), and a card made is a build (Dock.OnlyColorsDiffer), never a recolour.
    /// </summary>
    [UiFact]
    public void RecolouringShowsWhatABuildInTheColorsShows()
    {
        WindowShots.OnHiddenDesktop("bar-colors-recoloured", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var (_, _, tones) = Palettes[1];
                var colored = (AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque }).Dressed(tones);
                var automatic = colored with { Colors = colored.Colors with { Card = A("#FF333333"), CardOutline = A("#FF555555"), Taskbar = A("#FF101010") } };
                WithColorBar(gfx, colored, (layout, content) =>
                {
                    var built = ShootColors(gfx, layout, content, colored, null, null);
                    var recoloured = ShootColors(gfx, layout, content, automatic, colored, $"bar-colors-recoloured-{(dark ? "dark" : "light")}.png");
                    Assert.Equal(built.Swatches, recoloured.Swatches);
                    Assert.Equal(built.Pixels.Length, recoloured.Pixels.Length);
                    var differing = Enumerable.Range(0, built.Pixels.Length).Count(at => Math.Abs(built.Pixels[at] - recoloured.Pixels[at]) > 2);
                    Assert.True(differing < built.Pixels.Length / 200, $"{differing} of {built.Pixels.Length} channels differ from a bar built in the colours");
                });
            }
        });
    }

    /// <summary>The default bar with a few widgets, laid out for <paramref name="look"/>.</summary>
    private static void WithColorBar(Gfx gfx, Look look, Action<Layout, Content> body)
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = 1600, bottom = 960 };
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        var start = Math.Max(0, bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start));
        foreach (var (kind, at) in new[] { BarKind.WidgetsBoard, BarKind.TouchKeyboard, BarKind.NightLight }.Select((kind, at) => (kind, at)))
        {
            var item = kind.New();
            item.Id = $"color-{at}";
            item.Section = BarSection.Start;
            bar.Insert(start + at, item);
        }
        var (specs, islands) = Made(gfx, config, bar);
        try
        {
            var content = new Content { Islands = islands };
            body(new Layout(gfx, 96, area, area, Edge.Bottom, config, look, specs, content), content);
        }
        finally
        {
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }

    /// <summary>
    /// The scene of <paramref name="layout"/> in <paramref name="look"/>, recoloured to <paramref name="then"/> where
    /// given, its clock lit as under the pointer, captured: its panel's pixels, saved to <paramref name="file"/> where
    /// given, and its brushes' colours.
    /// </summary>
    private static (byte[] Pixels, IReadOnlyList<Windows.UI.Color> Swatches) ShootColors(Gfx gfx, Layout layout, Content content, Look look, Look? then, string? file)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
            if (then is { } next)
            {
                scene.Recolour(next);
            }
            scene.Highlight(Hit.Start, pressed: false);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            var panel = layout.PanelRect;
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            byte[] pixels = [];
            Assert.True(WindowShots.Save(window, file ?? "bar-colors-scratch.png", part, (seen, _) => pixels = seen), $"{file} came back blank");
            return (pixels, scene.SwatchColors);
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
