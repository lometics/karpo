// Show desktop's styles on the dock's real scene (docs\widgets.md §302; the person: "show desktop
// widget should get another style as well where no icon is shown. in edit mode it should still show
// an icon but also in the greyed out color"): its icon, and Windows 11's bare corner — nothing drawn at
// rest, no card, the same room and hit; arranged, its icon in the disabled ink. Pictures in
// KARPO_PNG_DIR: show-desktop-<icon|corner>-<rest|hover|edit>-<look>-<scale>.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>Start in the middle, Show desktop at the end, as its icon or as the bare corner.</summary>
    private static List<PartSpec> DesktopCorner(bool bare) =>
    [
        Spec("start", BarKind.Start) with { Section = BarSection.Middle },
        Spec("desktop", BarKind.ShowDesktop) with { Bare = bare, Section = BarSection.End },
    ];

    /// <summary>The corner is written as one word and read back; the icon, a word this build does not know, or another kind's options are nothing.</summary>
    [Fact]
    public void TheCornerIsKeptInTheFileAndTheIconIsNothing()
    {
        var corner = BarKind.ShowDesktop.New();
        corner.ShowDesktop = new ShowDesktopOptions { Style = ShowDesktopStyle.Corner };
        var json = System.Text.Json.JsonSerializer.Serialize(corner, ConfigJson.Default.BarItem);
        using (var file = System.Text.Json.JsonDocument.Parse(json))
        {
            Assert.Equal("corner", file.RootElement.GetProperty("showDesktop").GetProperty("style").GetString());
        }
        var read = System.Text.Json.JsonSerializer.Deserialize(json, ConfigJson.Default.BarItem)!;
        read.Normalize();
        Assert.Equal(ShowDesktopStyle.Corner, read.ShowDesktop?.Style);
        Assert.True(PartSpec.For("k", read).Bare);
        var unknown = System.Text.Json.JsonSerializer.Deserialize(json.Replace("\"corner\"", "\"sliver\""), ConfigJson.Default.BarItem)!;
        unknown.Normalize();
        Assert.Null(unknown.ShowDesktop);
        Assert.False(PartSpec.For("k", unknown).Bare);
        var icon = BarKind.ShowDesktop.New();
        icon.ShowDesktop = new ShowDesktopOptions { Style = ShowDesktopStyle.Icon };
        icon.Normalize();
        using (var file = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(icon, ConfigJson.Default.BarItem)))
        {
            Assert.False(file.RootElement.TryGetProperty("showDesktop", out _));
        }
        var search = BarKind.Search.New();
        search.ShowDesktop = new ShowDesktopOptions { Style = ShowDesktopStyle.Corner };
        search.Normalize();
        Assert.Null(search.ShowDesktop);
    }

    /// <summary>The bare corner draws no icon and wears no card at rest, in the icon's room; arranged it draws its icon.</summary>
    [UiFact]
    public void TheBareCornerDrawsNoIconAtRestAndItsIconArranged()
    {
        float Room(Layout layout) => layout.Parts[layout.PartOf(Hit.ShowDesktop)].Width;
        var icon = 0f;
        OnABar(48, DesktopCorner(bare: false), (layout, scene) =>
        {
            icon = Room(layout);
            Assert.NotNull(scene.Registered("showdesktop"));
        }, editing: false, style: BarStyle.Islands);
        OnABar(48, DesktopCorner(bare: true), (layout, scene) =>
        {
            Assert.Null(scene.Registered("showdesktop"));
            Assert.Null(scene.Registered($"card:{layout.Specs[layout.PartOf(Hit.ShowDesktop)].Key}"));
            Assert.Equal(icon, Room(layout));
            Assert.False(layout.ItemRect(Hit.ShowDesktop).IsEmpty);
        }, editing: false, style: BarStyle.Islands);
        OnABar(48, DesktopCorner(bare: true), (layout, scene) => Assert.NotNull(scene.Registered("showdesktop")), editing: true, style: BarStyle.Islands);
    }

    /// <summary>
    /// The pictures, and the ink: at rest the corner's room is the bar's (nothing brighter than it), lit
    /// under the pointer; arranged its icon is the disabled ink's, fainter than the icon's.
    /// </summary>
    [UiFact]
    public void ShowDesktopSheets()
    {
        WindowShots.OnHiddenDesktop("show-desktop", (gfx, _) =>
        {
            foreach (var theme in new[] { "dark", "light" })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = MarkLook(theme);
                    var config = new Config { Height = 48, Style = BarStyle.Islands };
                    var area = new RECT { left = 0, top = 0, right = (int)(560 * dpi / 96), bottom = (int)(dpi * 10) };
                    var name = $"{theme}-{dpi * 100 / 96}";
                    var stand = new Dictionary<string, int>();
                    foreach (var bare in new[] { false, true })
                    {
                        var style = bare ? "corner" : "icon";
                        foreach (var editing in new[] { false, true })
                        {
                            var layout = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, DesktopCorner(bare), new Content(), editing);
                            var state = editing ? "edit" : "rest";
                            Lit(gfx, layout, look, null, $"show-desktop-{style}-{state}-{name}.png", seen: (pixels, width) => stand[$"{style}-{state}"] = StandsOut(layout, pixels, width));
                            if (!editing)
                            {
                                Lit(gfx, layout, look, Hit.ShowDesktop, $"show-desktop-{style}-hover-{name}.png");
                            }
                        }
                    }
                    Assert.True(stand["corner-rest"] <= 8, $"{name}: the corner draws {stand["corner-rest"]} at rest");
                    Assert.True(stand["corner-edit"] > 40, $"{name}: the arranged corner shows no icon ({stand["corner-edit"]})");
                    Assert.True(stand["corner-edit"] < stand["icon-edit"] - 40, $"{name}: the arranged corner's icon ({stand["corner-edit"]}) is not greyed beside the icon's ({stand["icon-edit"]})");
                }
            }
        });
    }

    /// <summary>How far the most outstanding pixel of Show desktop's well stands out from the bar beside it, in the brightest channel.</summary>
    private static int StandsOut(Layout layout, byte[] pixels, int width)
    {
        var panel = layout.PanelRect;
        var box = layout.IconBox(Hit.ShowDesktop, layout.Icon);
        int At(int x, int y) => ((y * width) + x) * 4;
        // The bar itself, a little before the part: what nothing drawn there looks like.
        var part = layout.BlockRect(layout.PartOf(Hit.ShowDesktop));
        var back = At((int)(part.X - panel.X) - 3, (int)(box.Y + (box.Height / 2) - panel.Y));
        var most = 0;
        for (var y = (int)(box.Y - panel.Y); y < (int)(box.Bottom - panel.Y); y++)
        {
            for (var x = (int)(box.X - panel.X); x < (int)(box.Right - panel.X); x++)
            {
                var at = At(x, y);
                for (var channel = 0; channel < 3; channel++)
                {
                    most = Math.Max(most, Math.Abs(pixels[at + channel] - pixels[back + channel]));
                }
            }
        }
        return most;
    }
}
