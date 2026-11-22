// The looks, seen (docs\widgets.md §507, which replaces §459's styles; the person, 2026-11-05: "styles should not exist
// anymore, everywhere where we use styles there should be the values to configure yourself again"): Karpo in each palette
// applied to its layout, in both modes, on a real bar at 150 % — its widgets, a folder's face, a widget with a look of its own
// (another palette's Taskbar widgets) — with a real folder standing open over it, a line of it lit, and Layout ▾ › Look ›
// open beside it, Palette's choice open, as one picture (WindowShots.SaveTogether) to KARPO_PNG_DIR as
// look-<palette>-<light|dark>.png; a layout whose Flyouts alone are outlined, look-panel-outline-<light|dark>.png; the
// look editor — a section's values and a colour's picker open: Default (…), None, the grid and the opacity —,
// look-editor-<light|dark>.png; Paper's Sitka at 225 % text, look-text-225.png; §518 a widget's own Look ›,
// widget-look-defaults-<light|dark>.png. Each widget is drawn in its own look's font (the bar's widgets in the layout's Taskbar widgets', the lines in its Widgets in folders'); the text drawn is read back.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    private const int ThemeScreenW = 2900;
    private const int ThemeScreenH = 1300;

    /// <summary>§567: the Appearance page, and in it its Palette's choice: every palette's swatch.</summary>
    private static (List<FlyoutItem> Items, uint Level, uint? Deeper) LookMenu(Config config, List<OptionPick> picks, bool dark)
    {
        var items = TheDock.LooksItems(config, new BarSpot("display", "One"), dark, picks, scope: LooksScope.Appearance);
        return (items, items.Single(item => item.Text == S.SettingsLookPalette).Id, null);
    }

    /// <summary>
    /// Each palette and Karpo's own, in both modes: the bar, its widgets' cards, a folder's face, the folder open with a line
    /// lit, and the Appearance page's Palette in it. The text drawn is the palette's font.
    /// </summary>
    [UiFact]
    public void PalettesOnABarWithAFolderAndAMenu()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("look-palettes", (gfx, instance) =>
        {
            foreach (var id in LookShorthand.Palettes)
            {
                foreach (var dark in new[] { false, true })
                {
                    var config = new Config { Height = 48 };
                    if (LookPalettes.Of(id) is { } palette)
                    {
                        _ = LookPalettes.Wear(config, palette);
                    }
                    var families = new HashSet<string>();
                    PaintWatch.Styled = piece => _ = piece.What == PaintedKind.Text && families.Add(piece.Family);
                    try
                    {
                        ThemeSheet(gfx, instance, config, dark, $"look-{id ?? "windows"}-{(dark ? "dark" : "light")}",
                            failures, (config, picks) => LookMenu(config, picks, dark));
                    }
                    finally
                    {
                        PaintWatch.Styled = null;
                    }
                    var family = Looks.Of(config, dark: true).Widgets.Type.Family;
                    if (!families.Contains(family))
                    {
                        failures.Add($"{id}: its text drawn in {string.Join(", ", families)}, not {family}");
                    }
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>Graphite with its Flyouts alone outlined: the bar's cards, the face and the lines without.</summary>
    [UiFact]
    public void OnlyTheFlyoutsOutlined()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("look-panel-outline", (gfx, instance) =>
        {
            foreach (var dark in new[] { false, true })
            {
                var config = new Config { Height = 48 };
                _ = LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.GraphiteId)!);
                var panel = Looks.Section(config, LookPart.Flyouts);
                Assert.True(LookShorthand.Both(panel, Tone.Outline, new Karpo.Core.Paint(Argb.Parse(dark ? "#59FFFFFF" : "#40000000"))));
                ThemeSheet(gfx, instance, config, dark, $"look-panel-outline-{(dark ? "dark" : "light")}", failures, (config, picks) => LookMenu(config, picks, dark));
                var look = TestLooks.LookFor(dark).Wearing(config);
                if (look.Colors.CardOutline is not null || look.Flyout().Colors.CardOutline is not null || look.Flyout().Colors.FlyoutOutline is not { A: > 0 })
                {
                    failures.Add($"{(dark ? "dark" : "light")}: only the panel is to be outlined");
                }
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The look editor: Paper's Widgets in folders' values — §509 each colour's swatch in its row's icon's place: the text set,
    /// the background half see-through over its checker, the outline None slashed, the accent unset and quiet —, and its
    /// Background's picker — Default, None, the grid, the opacity.
    /// </summary>
    [UiFact]
    public void TheLookEditor()
    {
        WindowShots.OnHiddenDesktop("look-editor", (gfx, instance) =>
        {
            foreach (var dark in new[] { false, true })
            {
                var config = new Config();
                _ = LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.PaperId)!);
                var lines = Looks.Section(config, LookPart.FolderWidgets);
                Assert.True(LookShorthand.Both(lines, Tone.Background, new Karpo.Core.Paint(Looks.Of(config, dark: true).FolderWidgets.Background.Color!.Value.At(0.5))));
                Assert.True(LookShorthand.Both(lines, Tone.Accent, null));
                List<OptionPick> picks = [];
                var items = TheDock.LooksItems(config, new BarSpot("display", "One"), dark, picks, scope: LooksScope.Appearance);
                var work = new RECT { right = 2900, bottom = 1500 };
                var look = (TestLooks.LookFor(dark) with { Animations = false }).Wearing(config);
                // §583: Widgets in folders' rows, under its heading on the page.
                var section = Karpo.Tests.Menus.MenuCorpus.Part(items, LookPart.FolderWidgets);
                using var level = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                level.Show(gfx, look, 1.5f, section, FlyoutLayout.List, new POINT { x = 500, y = 1500 }, work, Edge.Bottom, null);
                using var choice = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                choice.Show(gfx, look, 1.5f, section, FlyoutLayout.List, new POINT { x = 1800, y = 1500 }, work, Edge.Bottom, null);
                _ = choice.Click(section.First(item => item.Text == S.SettingsAppearanceCustomBackgroundHeader).Id);
                WindowShots.Settle(350);
                Assert.True(WindowShots.SaveTogether([.. level.OpenPanels, .. choice.OpenPanels], $"look-editor-{(dark ? "dark" : "light")}.png", ((byte)0x60, (byte)0x60, (byte)0x60)));
            }
        });
    }

    /// <summary>Paper's Sitka at Windows' 225 % text: every line on Segoe UI Variable's line, so nothing is cut.</summary>
    [UiFact]
    public void AFontAt225PercentText()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("look-text", (gfx, instance) =>
        {
            foreach (var font in InstalledFonts.Shipped.Select(each => new TextStyle(each.Family)))
            {
                gfx.Typeface = font;
                var sized = gfx.Measure("Karpo Agy", 27, Weight.Regular);
                gfx.Typeface = default;
                var plain = gfx.Measure("Karpo Agy", 27, Weight.Regular);
                if (sized.Y != plain.Y)
                {
                    failures.Add($"{font}: a line {sized.Y} px tall, Segoe UI Variable's {plain.Y}");
                }
            }
            gfx.Typeface = new TextStyle("Cascadia Mono");
            var mono = gfx.Measure("iiiii", 12, Weight.Regular);
            gfx.Typeface = default;
            if (mono.X <= gfx.Measure("iiiii", 12, Weight.Regular).X)
            {
                failures.Add("a mono font measured as Segoe UI Variable");
            }
            var config = new Config { Height = 48 };
            _ = LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.PaperId)!);
            ThemeSheet(gfx, instance, config, false, "look-text-225", failures, (config, picks) => LookMenu(config, picks, false), textScale: 2.25f);
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The default bar of <paramref name="config"/> with a list folder ("themed") and a widget ("themed-widget") at its start,
    /// dressed by <paramref name="dress"/>, the folder standing open over it, a line of it lit, and the menu <paramref name="menuOf"/>
    /// gives opened on its level (and a level in it), worn as the dock wears them, in the style's font, in one picture
    /// <paramref name="name"/>.png. The folder's panel is read back as its style dresses it.
    /// </summary>
    private void ThemeSheet(Gfx gfx, HINSTANCE instance, Config config, bool dark, string name, List<string> failures,
        Func<Config, List<OptionPick>, (List<FlyoutItem> Items, uint Level, uint? Deeper)> menuOf, bool glass = false, float textScale = 1f,
        float scale = 1.5f, Action<FolderOptions>? lines = null)
    {
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        var start = Math.Max(0, bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start));
        var folder = BarKind.FolderList.New();
        folder.Id = "themed";
        folder.Section = BarSection.Start;
        if (lines is not null)
        {
            lines(folder.Folder!);
        }
        else
        {
            foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.Divider, ListKind.EnergySaver, ListKind.Battery })
            {
                _ = folder.Folder!.Put(kind.New());
            }
        }
        var widget = BarKind.NightLight.New();
        widget.Id = "themed-widget";
        widget.Section = BarSection.Start;
        bar.Insert(start, folder);
        bar.Insert(start + 1, widget);
        config.DefaultLayout.Islands = bar;
        gfx.Typeface = Looks.MenuText(config);
        var look = (TestLooks.LookFor(dark, textScale) with { Animations = false, Glass = glass }).Wearing(config);
        var (specs, islands) = Made(gfx, config, bar);
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        FolderIsland? open = null;
        try
        {
            var area = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = 1440 };
            var content = new Content { Islands = islands };
            var layout = new Layout(gfx, (uint)MathF.Round(96 * scale), area, area, Edge.Bottom, config, look, specs, content, editing: false);
            // The window as the bar's, its panel's foot on the screen's.
            var panelRect = layout.PanelRect;
            var windowTop = ThemeScreenH - (int)panelRect.Bottom;
            var top = windowTop + (int)panelRect.Y;
            _ = SetWindowPos(window, HWND.NULL, 0, windowTop, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);

            open = new FolderIsland($"layout:default/{folder.Id}", folder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var work = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = top };
            var face = layout.CardRect(specs.FindIndex(spec => spec.Key.EndsWith("/themed", StringComparison.Ordinal)));
            flyout.Show(gfx, look, scale, new POINT { x = (int)(face.X + (face.Width / 2)), y = top }, work, Edge.Bottom, open);
            // A line lit under the pointer: the first column's second, a little below the first's middle.
            RECT frame;
            _ = GetWindowRect(flyout.Hwnd, &frame);
            var shown = flyout.PanelOnScreen;
            var lit = new POINT { x = shown.left + ((shown.right - shown.left) / 4) - frame.left, y = shown.top - frame.top + (int)(scale * 64 * textScale) };
            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, (LPARAM)(nint)((lit.y << 16) | (lit.x & 0xFFFF)));
            List<OptionPick> picks = [];
            var (items, level, deeper) = menuOf(config, picks);
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
            menu.Show(gfx, look, scale, items, FlyoutLayout.List, new POINT { x = 1700, y = top }, work, Edge.Bottom, null);
            _ = menu.Click(level);
            WindowShots.Settle(350);
            var barPart = new RECT { left = (int)panelRect.X, top = top, right = (int)panelRect.Right, bottom = ThemeScreenH };
            List<(HWND, RECT)> parts = [(window, barPart), (flyout.Hwnd, flyout.PanelOnScreen), .. menu.OpenPanels];
            if (deeper is { } inner)
            {
                using var second = new Flyout(gfx, instance, 0) { Configures = true, FitsContent = true };
                second.Show(gfx, look, scale, items, FlyoutLayout.List, new POINT { x = 2400, y = top }, work, Edge.Bottom, null);
                _ = second.Click(level);
                _ = second.Click(inner);
                WindowShots.Settle(250);
                parts.AddRange(second.OpenPanels);
                Assert.True(WindowShots.SaveTogether(parts, $"{name}.png", ((byte)0x60, (byte)0x60, (byte)0x60)), $"{name} came back blank");
            }
            else
            {
                Assert.True(WindowShots.SaveTogether(parts, $"{name}.png", ((byte)0x60, (byte)0x60, (byte)0x60)), $"{name} came back blank");
            }
            // §464: the folder has no panel: its outline alone.
            var host = (FlyoutHost)typeof(FolderFlyout).GetField("host", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(flyout)!;
            if (!host.Theme.Bare)
            {
                failures.Add($"{name}: the folder draws a panel");
            }
            flyout.Hide();
        }
        finally
        {
            gfx.Typeface = default;
            open?.Dispose();
            scene?.Dispose();
            _ = DestroyWindow(window);
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }
}
