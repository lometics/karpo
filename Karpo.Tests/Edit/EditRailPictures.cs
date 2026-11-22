// edit: the "not shown" warning on a section's edit bar (EditRail), the real window, in Windows'
// warning orange (2026-10-11: "when there is not enough space to show all the widget there is the
// warning showin up, i like that, but it should be more visible. it should be red or orange"):
// one hidden widget (its own glyph) and several (the warning glyph), at rest, under the pointer
// and pressed, dark, light and high contrast, at 100 % and 150 %. Pictures to KARPO_PNG_DIR as
// edit-rail-warning-<one|many>-<theme>-<scale>.png. The fill is checked in the picture: orange,
// not the grey of a plain button.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Edit;

public sealed unsafe class EditRailPictures
{
    private static Look LookFor(string theme) => theme switch
    {
        "contrast" => AddListPictures.LookFor(true) with
        {
            HighContrast = true,
            HcWindow = Color.FromArgb(255, 0, 0, 0),
            HcText = Color.FromArgb(255, 255, 255, 255),
            HcHighlight = Color.FromArgb(255, 0x1A, 0xEB, 0xFF),
            HcHighlightText = Color.FromArgb(255, 0, 0, 0),
            Animations = false,
        },
        _ => AddListPictures.LookFor(theme == "dark") with { Animations = false },
    };

    /// <summary>Windows' warning orange, and the high contrast highlight, as the warning wears them.</summary>
    [Fact]
    public void TheWarningIsOrangeAndTheHighlightInHighContrast()
    {
        foreach (var dark in new[] { true, false })
        {
            var warning = EditRail.Warning(FlyoutTheme.For(LookFor(dark ? "dark" : "light")));
            Assert.Equal(Color.FromArgb(255, 0xFC, 0x9A, 0x03), warning.Fill);
            Assert.NotEqual(warning.Fill, warning.Hover);
            Assert.NotEqual(warning.Fill, warning.Pressed);
            // Black on the orange: a mark that reads (WCAG well over 4.5:1).
            Assert.True(warning.Glyph.r < 0.1f && warning.Glyph.g < 0.1f && warning.Glyph.b < 0.1f);
        }
        var contrast = FlyoutTheme.For(LookFor("contrast"));
        var high = EditRail.Warning(contrast);
        Assert.Equal(contrast.Accent, high.Fill);
        Assert.Equal(contrast.TextOnAccent, high.Glyph);
    }

    [UiFact]
    public void TheNotShownWarningIsOrange()
    {
        WindowShots.OnHiddenDesktop("edit-rail-warning", (gfx, instance) =>
        {
            var work = new RECT { right = 1920, bottom = 1032 };
            var bar = new RECT { left = 600, top = 1032, right = 1320, bottom = 1080 };
            PartSpec One(string id, BarKind kind) => new(Config.IslandKey(null, id), kind, 2);
            foreach (var (name, hidden) in new (string, PartSpec[])[]
            {
                ("one", [One("weather", BarKind.WidgetsBoard)]),
                ("many", [One("weather", BarKind.WidgetsBoard), One("timer", BarKind.NightLight), One("quiet", BarKind.DoNotDisturb)]),
            })
            {
                foreach (var theme in new[] { "dark", "light", "contrast" })
                {
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var look = LookFor(theme);
                        using var rail = new EditRail(gfx, instance, 0);
                        rail.Show(gfx, look, scale, bar, Edge.Bottom, work, hidden);
                        Assert.True(rail.IsOpen);
                        WindowShots.Settle(200);
                        var button = rail.Plan.Buttons.Single();
                        RECT window;
                        window = WindowShots.Extent(rail.Hwnd);
                        var panel = rail.Panel!.Value;
                        // A pixel of the button's fill: inside its edge, clear of the glyph in its middle.
                        var x = panel.left - window.left + (int)(button.Rect.X + (3 * scale));
                        var y = panel.top - window.top + (int)(button.Rect.Y + (button.Rect.Height / 2));
                        var file = $"edit-rail-warning-{name}-{theme}-{(int)(scale * 100)}.png";
                        byte[] pixels = [];
                        var stride = 0;
                        Assert.True(WindowShots.Save(rail.Hwnd, file, seen: (seen, w) => (pixels, stride) = (seen, w)), $"{file} came back blank");
                        var at = ((y * stride) + x) * 4;
                        var (b, g, r) = (pixels[at], pixels[at + 1], pixels[at + 2]);
                        var expected = EditRail.Warning(FlyoutTheme.For(look)).Fill;
                        Assert.True(Math.Abs(r - expected.R) < 12 && Math.Abs(g - expected.G) < 12 && Math.Abs(b - expected.B) < 12, $"{file}: the warning's fill at {x},{y} is #{r:X2}{g:X2}{b:X2}, not #{expected.R:X2}{expected.G:X2}{expected.B:X2}");
                        rail.Hide();
                    }
                }
            }
        });
    }
}
