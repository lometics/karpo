// What the bar's ground really shows (docs\widgets.md §546): the dock's real scene, worn as the dock wears a layout's look
// (Look.Wearing), with Windows' transparency effects off as on this PC (EnableTransparency 0), over a desktop drawn in its
// window's own tree under it — bands of a wallpaper blue and gold, so what shows through shows —, captured as the
// compositor draws it (WindowShots) to KARPO_PNG_DIR as bar-ground-<colour>-<mode>.png. A background chosen is its colour
// at its own opacity over the desktop, the same in Windows' light mode and dark; §547 nothing chosen is Karpo's own,
// #202020 at 85 %, the same in both too (over the desktop here: no wallpaper strip is given, BarWallpaperPictures has it).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>The desktop under the bar in these pictures: bands this many px wide, blue and gold by turns.</summary>
    private const int DeskBand = 64;

    private static readonly Argb DeskBlue = new(255, 0x3A, 0x6E, 0xA5);
    private static readonly Argb DeskGold = new(255, 0xE9, 0xB8, 0x2A);

    /// <summary>
    /// §546: the bar's ground as it is drawn — Graphite dark's #1B1C1F at 88 % (the person's), an orange at 50 % (which the
    /// eye tells from solid at once), and nothing chosen (§547 Karpo's own) — in Windows' dark mode and light: each its colour
    /// laid over the desktop at its alpha, the same in both modes.
    /// </summary>
    [UiFact]
    public void TheBarsGroundIsItsColourAtItsOpacityInBothModes()
    {
        (string Name, Argb? Background)[] grounds = [("graphite-88", new(0xE0, 0x1B, 0x1C, 0x1F)), ("orange-50", new(0x80, 0xFF, 0x8C, 0x00)), ("karpo", null)];
        // Each in both modes, dark first: every picture taken before anything is asserted.
        var shot = new Dictionary<string, List<(Argb OnBlue, Argb OnGold)>>();
        WindowShots.OnHiddenDesktop("bar-ground", (gfx, _) =>
        {
            foreach (var (name, background) in grounds)
            {
                var seen = shot[name] = [];
                foreach (var dark in new[] { true, false })
                {
                    var config = new Config();
                    if (background is { } chosen)
                    {
                        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Taskbar), Tone.Background, new Karpo.Core.Paint(chosen)));
                    }
                    var look = (AddListPictures.LookFor(dark) with { Animations = false, Glass = false }).Wearing(config);
                    WithColorBar(gfx, look, (layout, content) => seen.Add(ShootGround(gfx, layout, content, look, $"bar-ground-{name}-{(dark ? "dark" : "light")}.png")));
                }
            }
        });
        foreach (var (name, background) in grounds)
        {
            var seen = shot[name];
            // Its colour at its alpha over each band, Karpo's own #202020 at 85 % where none is chosen, in both modes.
            var said = background ?? KarpoLook.Ground;
            (Argb OnBlue, Argb OnGold)[] expected =
                [(ToneRules.Blend(said, DeskBlue), ToneRules.Blend(said, DeskGold)), (ToneRules.Blend(said, DeskBlue), ToneRules.Blend(said, DeskGold))];
            for (var at = 0; at < 2; at++)
            {
                Assert.True(Near(seen[at].OnBlue, expected[at].OnBlue) && Near(seen[at].OnGold, expected[at].OnGold),
                    $"{name} ({(at == 0 ? "dark" : "light")}): the ground came out {seen[at]}, not {expected[at]}");
            }
        }

        static bool Near(Argb a, Argb b) => Math.Abs(a.R - b.R) <= 2 && Math.Abs(a.G - b.G) <= 2 && Math.Abs(a.B - b.B) <= 2;
    }

    /// <summary>
    /// The scene of <paramref name="layout"/> in <paramref name="look"/> over the bands of the desktop, drawn in its window's
    /// tree under it, captured and saved to <paramref name="file"/>: the bar's ground as it shows over a blue band and over a
    /// gold one — the colour most of the bar's pixels over each are, the widgets standing on a small part of it.
    /// </summary>
    private static (Argb OnBlue, Argb OnGold) ShootGround(Gfx gfx, Layout layout, Content content, Look look, string file)
    {
        var window = MakeWindow();
        var c = gfx.Compositor;
        var desk = c.CreateContainerVisual();
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            var target = Target(gfx, window);
            for (var x = 0; x < layout.Width; x += DeskBand)
            {
                var band = c.CreateSpriteVisual();
                band.Offset = new Vector3(x, 0f, 0f);
                band.Size = new Vector2(DeskBand, layout.Height);
                var colour = x / DeskBand % 2 == 0 ? DeskBlue : DeskGold;
                band.Brush = c.CreateColorBrush(Windows.UI.Color.FromArgb(255, colour.R, colour.G, colour.B));
                desk.Children.InsertAtTop(band);
            }
            scene = new Scene(gfx, target, desk);
            scene.Update(layout, content, look, new Icons(), shown: true, null, default);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(300);
            var panel = layout.PanelRect;
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            byte[] pixels = [];
            var width = 0;
            Assert.True(WindowShots.Save(window, file, part, (shown, w) => (pixels, width) = (shown, w)), $"{file} came back blank");
            Argb MostOn(int stripe) => Enumerable.Range(0, pixels.Length / 4)
                // A px clear of a band's edge, whose band is the one asked for.
                .Where(at => ((int)panel.X + (at % width)) % DeskBand is > 2 and < DeskBand - 2 && ((int)panel.X + (at % width)) / DeskBand % 2 == stripe)
                .Select(at => new Argb(255, pixels[(at * 4) + 2], pixels[(at * 4) + 1], pixels[at * 4]))
                .GroupBy(pixel => pixel)
                .MaxBy(group => group.Count())!.Key;
            return (MostOn(0), MostOn(1));
        }
        finally
        {
            scene?.Dispose();
            desk.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
