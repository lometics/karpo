// A font changed is measured and drawn again everywhere at once (docs\widgets.md §499, §507; the person, 2026-11-03:
// "changing the font does not change anything"): a real bar with a folder standing open over it and the Appearance page's
// Taskbar open beside it, drawn as the dock draws them — the default layout's bar (Karpo's own), and a bar of another layout
// with the Appearance in Graphite —; then the Appearance's Taskbar widgets' and Widgets in folders' font changed to Georgia, as the
// person changes it, the look read again as the dock reads it (Look.Configured — Dock.ReadLook — and Look.Wearing) and the
// same bar, folder and menu updated where they stand (Scene.Update, FolderFlyout.Relook, Flyout.Refresh as Dock.MenuAgain).
// Every line of text drawn again is in Georgia — the bar's widgets in their section's font, the folder's lines and the menu
// in the Widgets in folders' —, none is left in Segoe UI Variable, and as many lines are drawn as at first: nothing kept
// from before the change (the bar's kept elements, the islands' surfaces, the menu's rows). To KARPO_PNG_DIR as
// font-change-<default|other>-<before|after>.png.
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
    /// <summary>§499, §507: the Appearance's font changed while a bar, a folder and a menu stand: every one of their lines drawn again in it; see the top of the file.</summary>
    [UiFact]
    public void AFontChangedIsDrawnAgainEverywhere()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("font-change", (gfx, instance) =>
        {
            FontChange(gfx, instance, other: false, failures);
            FontChange(gfx, instance, other: true, failures);
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>The default layout's bar, or with <paramref name="other"/> a bar of another layout, the Appearance in Graphite, before and after its font changed.</summary>
    private void FontChange(Gfx gfx, HINSTANCE instance, bool other, List<string> failures)
    {
        const float scale = 1.5f;
        var name = other ? "other" : "default";
        var config = new Config { Height = 48 };
        var home = config.DefaultLayout;
        var shown = other ? BarLooks.Add(config, "Work") : home;
        Assert.Same(home, config.DefaultLayout);
        Assert.True(!other || LookPalettes.Wear(config, LookPalettes.Of(LookPalettes.GraphiteId)!));
        config.SelectLayout("display", shown.Id, "One");
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        var start = Math.Max(0, bar.FindIndex(item => Sections.Known(item.Section) == BarSection.Start));
        var folder = BarKind.FolderList.New();
        folder.Id = "fonted";
        folder.Section = BarSection.Start;
        foreach (var kind in new[] { ListKind.NightLight, ListKind.AirplaneMode, ListKind.EnergySaver, ListKind.Battery })
        {
            _ = folder.Folder!.Put(kind.New());
        }
        bar.Insert(start, folder);
        shown.Islands = bar;
        var windows = TestLooks.LookFor(dark: false) with { Animations = false };
        // As the dock: the look read (ReadLook), its font put on the Gfx (Rebuild), the Appearance worn (TaskbarLook).
        Look Read()
        {
            var read = windows.Configured(config);
            gfx.Typeface = read.Typeface;
            return read.Wearing(config);
        }
        var look = Read();
        var (specs, islands) = Made(gfx, config, bar);
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        FolderIsland? open = null;
        var families = new List<string>();
        PaintWatch.Styled = piece => _ = piece.What == PaintedKind.Text && piece.Family.Length > 0 && Add(families, piece.Family);
        try
        {
            var area = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = 1440 };
            var content = new Content { Islands = islands };
            Layout Laid() => new(gfx, (uint)MathF.Round(96 * scale), area, area, Edge.Bottom, config, look, specs, content, editing: false);
            var layout = Laid();
            var panelRect = layout.PanelRect;
            var windowTop = ThemeScreenH - (int)panelRect.Bottom;
            var top = windowTop + (int)panelRect.Y;
            _ = SetWindowPos(window, HWND.NULL, 0, windowTop, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            var icons = new Icons();
            scene = Scenes.Build(gfx, target, layout, content, look, icons, shown: true);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            open = new FolderIsland($"layout:{shown.Id}/{folder.Id}", folder, gfx, config);
            using var flyout = new FolderFlyout(gfx, instance, 0);
            var work = new RECT { left = 0, top = 0, right = ThemeScreenW, bottom = top };
            var face = layout.CardRect(specs.FindIndex(spec => spec.Key.EndsWith("/fonted", StringComparison.Ordinal)));
            flyout.Show(gfx, look, scale, new POINT { x = (int)(face.X + (face.Width / 2)), y = top }, work, Edge.Bottom, open);
            List<OptionPick> picks = [];
            List<FlyoutItem> Items() => Karpo.Tests.Menus.MenuCorpus.Part(TheDock.LooksItems(config, new BarSpot("display", "One"), false, picks, scope: LooksScope.Appearance), LookPart.Widgets);
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
            menu.Show(gfx, look, scale, Items(), FlyoutLayout.List, new POINT { x = 1700, y = top }, work, Edge.Bottom, null);
            WindowShots.Settle(350);
            var barPart = new RECT { left = (int)panelRect.X, top = top, right = (int)panelRect.Right, bottom = ThemeScreenH };
            List<(HWND, RECT)> Parts() => [(window, barPart), (flyout.Hwnd, flyout.PanelOnScreen), .. menu.OpenPanels];
            Assert.True(WindowShots.SaveTogether(Parts(), $"font-change-{name}-before.png", ((byte)0x60, (byte)0x60, (byte)0x60)));
            var first = families.ToList();
            if (!first.Contains("Segoe UI Variable Text"))
            {
                failures.Add($"{name}, before: drawn in {string.Join(", ", first.Distinct())}, not Segoe UI Variable");
            }

            // The person's change: the Appearance's Taskbar widgets' and Widgets in folders' font.
            Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.Widgets), new TextStyle("Georgia")));
            Assert.True(Looks.WriteFont(Looks.Section(config, LookPart.FolderWidgets), new TextStyle("Georgia")));
            families.Clear();
            var before = look;
            look = Read();
            if (look == before)
            {
                failures.Add($"{name}: a font changed is the same look: nothing is built again");
            }
            foreach (var island in islands.Values)
            {
                island.Rebuilding();
            }
            layout = Laid();
            scene.Update(layout, content, look, icons, true, null, default);
            foreach (var island in islands.Values)
            {
                island.Rebuilt();
            }
            flyout.Relook(look);
            menu.Refresh(Items(), look: look);
            WindowShots.Settle(350);
            Assert.True(WindowShots.SaveTogether(Parts(), $"font-change-{name}-after.png", ((byte)0x60, (byte)0x60, (byte)0x60)));
            var again = families.ToList();
            if (again.Any(family => family != "Georgia"))
            {
                failures.Add($"{name}, after: drawn in {string.Join(", ", again.Distinct())}, not Georgia alone");
            }
            // Every line drawn again: as many as at first, none kept in the font before.
            if (again.Count < first.Count)
            {
                failures.Add($"{name}, after: {again.Count} lines drawn again of {first.Count}");
            }
            // Measured again: Georgia's words are not Segoe UI Variable's width.
            gfx.Typeface = default;
            var plain = gfx.Measure("Karpo 12:34", 18, Weight.Regular).X;
            gfx.Typeface = look.Typeface;
            if (gfx.Measure("Karpo 12:34", 18, Weight.Regular).X == plain)
            {
                failures.Add($"{name}: Georgia measured as Segoe UI Variable");
            }
            flyout.Hide();
            menu.Hide();
        }
        finally
        {
            PaintWatch.Styled = null;
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

    private static bool Add(List<string> into, string what)
    {
        into.Add(what);
        return true;
    }
}
