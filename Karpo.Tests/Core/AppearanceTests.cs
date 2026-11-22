// How Karpo looks (Karpo.Core\SurfaceLook.cs, Looks.cs, docs\widgets.md §405, §464, §507, §567): one look, the file's
// "appearance"; the bar's opacity, its Taskbar section's, mapped onto how it is made; a folder no panel; the menus the
// Widgets in folders' over Windows' flyout; the folder room's right click; opening on hover.

using Karpo.Dock;
using Color = Windows.UI.Color;

namespace Karpo.Tests.Core;

public sealed class AppearanceTests
{
    /// <summary>
    /// §405: one opacity per surface is how it is made: 100 % solid, 0 % no plate, anything between the blur under the
    /// colour at that opacity; §546 without Windows' transparency effects a colour chosen at its opacity over the desktop,
    /// Windows' own (70 %) solid, as Windows draws its own; high contrast solid.
    /// </summary>
    [Theory]
    [InlineData(100, true, false, Material.Opaque)]
    [InlineData(70, true, false, Material.Acrylic)]
    [InlineData(30, true, false, Material.Acrylic)]
    [InlineData(1, true, false, Material.Acrylic)]
    [InlineData(0, true, false, Material.Clear)]
    [InlineData(70, false, false, Material.Translucent)]
    [InlineData(100, false, false, Material.Opaque)]
    [InlineData(0, false, false, Material.Clear)]
    [InlineData(30, true, true, Material.Opaque)]
    [InlineData(0, true, true, Material.Opaque)]
    public void AnOpacityIsHowASurfaceIsMade(int opacity, bool glass, bool high, Material made) =>
        Assert.Equal(made, SurfaceStyle.Made(new Karpo.Core.Paint(new Argb(0, 0x1E, 0x3A, 0x5F).At(opacity / 100.0)), glass, high));

    /// <summary>§546: Windows' own background, nothing chosen, is its 70 %: the blur with transparency effects, solid without; None no plate.</summary>
    [Theory]
    [InlineData(true, Material.Acrylic)]
    [InlineData(false, Material.Opaque)]
    public void WindowsOwnIsMadeAsWindowsDoes(bool glass, Material made)
    {
        Assert.Equal(made, SurfaceStyle.Made(default, glass, high: false));
        Assert.Equal(Material.Clear, SurfaceStyle.Made(Karpo.Core.Paint.Nothing, glass, high: false));
    }

    /// <summary>
    /// §464, §507: the bar wears the opacity of the Appearance's Taskbar section's background, in the mode drawn; Karpo's own 85
    /// (§547). A folder has no panel at all: its outline alone, no plate, no shadow — its lines wear their own, Windows'
    /// taskbar flyout's dark surface for Karpo's own, solid: §547 the taskbar takes no blur of Windows' transparency effects,
    /// the bar's see-through colour is laid over the wallpaper Karpo blurs itself (Material.Translucent).
    /// </summary>
    [Fact]
    public void TheBarWearsItsOpacityAndAFolderNoPanel()
    {
        var config = new Config();
        var bar = Looks.Section(config, LookPart.Taskbar);
        var navy = new Argb(255, 0x1E, 0x3A, 0x5F);
        Assert.True(LookShorthand.Both(bar, Tone.Background, new Karpo.Core.Paint(navy.At(0.3))));
        // A folder's panel has no background to set.
        Assert.False(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Background, new Karpo.Core.Paint(navy)));
        var windows = Karpo.Tests.Folders.TestLooks.LookFor(dark: true) with { Glass = true };

        var worn = windows.Wearing(config);

        Assert.Equal((Material.Translucent, 0.3f), (worn.Material, MathF.Round(worn.Opacity, 2)));
        var folder = FlyoutTheme.Folder(worn.Flyout());
        Assert.True(folder is { Bare: true, Casts: false, Glass: false });
        Assert.Equal(FlyoutTheme.Taskbar(windows).Tint, folder.Tint);
        Assert.True(FlyoutTheme.Folder((worn with { Glass = false }).Flyout()) is { Bare: true, Glass: false });
        // §509: what is set, the same in both modes.
        Assert.Equal((0.3f, 0.3f), (MathF.Round(windows.Wearing(config).Opacity, 2), MathF.Round((windows with { Dark = false }).Wearing(config).Opacity, 2)));
        // Karpo's own: the bar's 85 (§547), nothing written.
        var plain = new Config();
        Assert.Equal(85, plain.TaskbarOpacity);
        Assert.DoesNotContain("\"appearance\"", Configs.RoundTrip(plain, out _).ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// §567: Karpo's one look is the file's "appearance" (its four sections, each its values), read back as it was; a layout's
    /// "looks", a style, a style's id and a dark set of an older file are not read. §514: a layout's "openOnHover" says whether
    /// what its widgets open opens on hover.
    /// </summary>
    [Fact]
    public void TheFileKeepsTheAppearance()
    {
        var config = Configs.Repaired("""
            { "appearance": { "palette": "custom", "palettes": { "custom": { "taskbar": { "background": { "dark": "#4D202020", "light": "#4DF3F3F3" } }, "widgets": { "font": "Sitka Text/600", "accent": { "dark": "#0078D4", "light": "#0078D4" }, "outline": { "dark": "#52FFFFFF" }, "dark": { "accent": "#8FA6FF" } },
                "folderWidgets": { "background": { "dark": "#202020" } } }, "nowhere": { "taskbar": { "background": { "dark": "#FF0000" } } } }, "styles": [ { "id": "mine", "name": "Mine" } ] },
              "layouts": [ { "id": "L", "name": "L", "style": "mine", "openOnHover": true, "looks": { "taskbar": { "background": "#FF0000" } } },
                { "id": "M", "name": "M" } ],
              "defaultLayoutId": "L" }
            """);

        Assert.Equal(30, config.TaskbarOpacity);
        Assert.True(config.Layouts[0].OpenOnHover);
        Assert.Equal(new TextStyle("Sitka Text", 600), Looks.Of(config, dark: true).Widgets.Type);
        Assert.Equal(new Karpo.Core.Paint(Argb.Parse("#0078D4")), Looks.Of(config, dark: true).Widgets.Accent);
        // Its menus' background a solid dark colour in the dark mode (§568: its dark half): their text light.
        var windows = Karpo.Tests.Folders.TestLooks.LookFor(dark: true);
        Assert.True(FlyoutTheme.For(windows.Wearing(config)).Text.r > 0.9f);

        var again = Configs.RoundTrip(config, out var changed);
        Assert.False(changed);
        Assert.Equal(config.ToJson(), again.ToJson());
        var json = again.ToJson();
        Assert.Contains("\"appearance\"", json, StringComparison.Ordinal);
        Assert.Contains("\"font\": \"Sitka Text/600\"", json, StringComparison.Ordinal);
        // §577: the palette worn and its values kept with it; a palette this build does not know not read.
        Assert.Same(LookPalettes.Custom, LookPalettes.Worn(config));
        Assert.Contains("\"palette\": \"custom\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("nowhere", json, StringComparison.Ordinal);
        foreach (var gone in new[] { "\"looks\"", "\"styles\"", "\"style\": \"mine\"", "material", "#8FA6FF", "#FF0000" })
        {
            Assert.DoesNotContain(gone, json, StringComparison.Ordinal);
        }
        // The defaults write nothing of their own.
        Assert.DoesNotContain("\"appearance\"", Configs.RoundTrip(new Config(), out _).ToJson(), StringComparison.Ordinal);
    }

    /// <summary>
    /// §441, §514: the folder room's right click: whether what the widgets open — a folder among it — opens on hover, the one row
    /// Layout ▾ › Behavior has; §423 at its foot the way to Layout ▾, where the layout's look is.
    /// </summary>
    [Fact]
    public void TheFolderRoomsMenuIsOpenFlyouts()
    {
        var config = new Config();
        var rows = BarLooks.Rows(config, new BarSpot("display", "One"), dark: true, scope: LooksScope.Folders);

        Assert.Equal([BarLooks.HoverRow, BarLooks.AppearanceLinkRow + "-", BarLooks.AppearanceLinkRow], rows.Select(row => row.Key));
        Assert.Equal(S.SettingsGeneralHoverFlyoutsHeader, rows[0].Title);
    }

    /// <summary>
    /// §514 (the person, 2026-11-05: "open widget flyouts and open folders should be the same setting and not treated
    /// differently"): one setting, the layout's, opens on a rest whatever a widget opens — a folder with something to show and
    /// a widget's flyout alike —; Behavior has one row for it, and the folder room's menu that row. Each layout its own.
    /// </summary>
    [Fact]
    public void OneSettingOpensFoldersAndWidgetFlyoutsOnHover()
    {
        var config = new Config();
        var home = config.DefaultLayout;
        var work = BarLooks.Add(config, "Work");
        var behaviour = BarLooks.Behaviour(work);
        Assert.Single(behaviour, row => row.Title == S.SettingsGeneralHoverFlyoutsHeader);
        Assert.DoesNotContain(behaviour, row => row.Key == "FoldersOpenOnHover");
        // The folder room's row is Behavior's: set in one, it is set in the other.
        var room = BarLooks.Rows(config, new BarSpot("display", "One"), dark: true, scope: LooksScope.Folders).Single(row => row.Key == BarLooks.HoverRow);
        Assert.True(room.Set!(1));
        Assert.True(home.OpenOnHover);
        Assert.Equal(1, BarLooks.Behaviour(home).Single(row => row.Key == BarLooks.HoverRow).Chosen);
        Assert.True(BarLooks.Behaviour(home).Single(row => row.Key == BarLooks.HoverRow).Set!(0));
        Assert.Equal(0, room.Chosen);

        Assert.True(behaviour.Single(row => row.Key == BarLooks.HoverRow).Set!(1));
        Assert.False(home.OpenOnHover);
        Assert.False(WidgetHover.On(home));
        Assert.True(WidgetHover.On(work));
        // A folder with something to show and a widget's flyout: both by the one setting, on or off together.
        Assert.True(WidgetHover.Opens(work, folder: true, folderShows: true, hasFlyout: false));
        Assert.True(WidgetHover.Opens(work, folder: false, folderShows: false, hasFlyout: true));
        Assert.False(WidgetHover.Opens(home, folder: true, folderShows: true, hasFlyout: false));
        Assert.False(WidgetHover.Opens(home, folder: false, folderShows: false, hasFlyout: true));
        // An empty folder has nothing to show; a widget that opens nothing opens nothing.
        Assert.False(WidgetHover.Opens(work, folder: true, folderShows: false, hasFlyout: false));
        Assert.False(WidgetHover.Opens(work, folder: false, folderShows: false, hasFlyout: false));
        // A copy takes its source's; a new layout the defaults.
        Assert.True(BarLooks.Duplicate(config, work).OpenOnHover);
        Assert.False(BarLooks.Add(config).OpenOnHover);
        // The file: the layout's own "openOnHover", nothing else of hover's.
        var json = Configs.RoundTrip(config, out _).ToJson();
        Assert.Contains("\"openOnHover\": true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"folders\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"look\": {", json, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every popup the dock opens from a bar, or over a display, wears that bar's look (DockWindow.Look,
    /// Dock.LookOn): no popup is shown in the dock's bare look, which wears no appearance — §526 but Karpo's settings,
    /// in Windows' own (Dock.SettingsLook): the edit bar and its panel, the sections' edit bars, edit mode's menus.
    /// </summary>
    [Fact]
    public void NoPopupIsShownInTheDocksBareLook()
    {
        var repo = RepoSource.Root();
        if (repo.Length == 0)
        {
            return;
        }
        var files = Directory.GetFiles(Path.Combine(repo, "Karpo.Dock"), "Dock*.cs").Where(file => !file.EndsWith("Dock.EditDeck.cs", StringComparison.Ordinal));
        var dock = string.Join("\n", files.Select(File.ReadAllText));
        Assert.DoesNotMatch(@"\.Show\(\s*gfx,\s*look\b", dock);
        Assert.DoesNotMatch(@"\.Relook\(\s*look\s*\)", dock);
        Assert.DoesNotMatch(@"(dock|d)\.look\b(?!\.(Dark|TextScale))", dock);
        Assert.Contains("(SettingsLook, display.Dpi / 96f, DeckRoom(display))", File.ReadAllText(Path.Combine(repo, "Karpo.Dock", "Dock.EditDeck.cs")), StringComparison.Ordinal);
        Assert.Contains("new EditRailModel(window, section, SettingsLook,", dock, StringComparison.Ordinal);
        Assert.Contains(": editor is not null || what.Configuration ? SettingsLook", dock, StringComparison.Ordinal);
    }

    /// <summary>
    /// §464, §507: the menus opened from a bar wear its layout's Widgets in folders — their rows are lines too —: its background
    /// over Windows' own flyout, its text; §624 their outline the Flyouts', as a folder's (the lines have none); high contrast none.
    /// </summary>
    [Fact]
    public void TheMenusWearTheLayoutsLines()
    {
        var config = new Config();
        var rose = new Argb(255, 0x5E, 0x1F, 0x1F);
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.FolderWidgets), Tone.Background, new Karpo.Core.Paint(rose)));
        Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.Flyouts), Tone.Outline, new Karpo.Core.Paint(rose)));
        var windows = Karpo.Tests.Folders.TestLooks.LookFor(dark: true);

        var menu = FlyoutTheme.For(windows.Wearing(config));
        Assert.Equal(Color.FromArgb(255, rose.R, rose.G, rose.B), menu.Base);
        Assert.Equal(Color.FromArgb(255, rose.R, rose.G, rose.B), menu.Stroke);
        Assert.Equal(1f, menu.Text.r, 2);
        // §547: nothing set, Karpo's own: Windows' tokens of its mode (§568) in Karpo's blue, whatever Windows' accent.
        Assert.Equal(FlyoutTheme.For(windows.KarpoBase(dark: true)), FlyoutTheme.For(windows.Wearing(new Config())));
        var light = windows with { Dark = false };
        Assert.Equal(FlyoutTheme.For(light.KarpoBase(dark: false)), FlyoutTheme.For(light.Wearing(new Config())));
        Assert.NotEqual(FlyoutTheme.For(windows.Wearing(new Config())), FlyoutTheme.For(light.Wearing(new Config())));
        Assert.True((windows with { HighContrast = true }).Wearing(config).Colors.IsAutomatic);
    }

    /// <summary>
    /// §464: a menu reads over any desktop: its background is laid over Windows' own flyout, never in its place — None and
    /// 0 % are Windows' flyout itself, a see-through colour no more see-through than it —, and its text is made readable on
    /// what that comes to.
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("#00FFFFFF")]
    [InlineData("#1AFFFFFF")]
    [InlineData("#1A000000")]
    [InlineData("#80FFB900")]
    public void AMenuReadsOverAnyBackground(string background)
    {
        foreach (var dark in new[] { false, true })
        {
            var config = new Config();
            Assert.True(LookShorthand.Both(Looks.Section(config, LookPart.FolderWidgets), Tone.Background, Karpo.Core.Paint.Parse(background)));
            var windows = Karpo.Tests.Folders.TestLooks.LookFor(dark) with { Glass = true };
            var own = FlyoutTheme.For(windows);
            var menu = FlyoutTheme.For(windows.Wearing(config));

            Assert.True(menu.Tint.A >= own.Tint.A, $"{background}: the menu's tint {menu.Tint.A} is more see-through than Windows' {own.Tint.A}");
            Assert.Equal(255, menu.Base.A);
            var under = ToneRules.Blend(new Argb(menu.Tint.A, menu.Tint.R, menu.Tint.G, menu.Tint.B), new Argb(255, menu.Base.R, menu.Base.G, menu.Base.B));
            var text = new Argb(255, (byte)Math.Round(menu.Text.r * 255), (byte)Math.Round(menu.Text.g * 255), (byte)Math.Round(menu.Text.b * 255));
            Assert.True(ToneRules.Contrast(text, under) >= ToneRules.TextContrast, $"{background} {(dark ? "dark" : "light")}: text {ToneRules.Contrast(text, under):0.0}:1");
        }
    }
}
