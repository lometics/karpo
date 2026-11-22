// The bar in edit mode on the dock's real scene, shown on a desktop of its own and captured as the
// compositor draws it (WindowShots), to KARPO_PNG_DIR as add-buttons-<state>-<look>-<scale>.png: the
// bar at rest, in edit mode and full. The
// sections' "+" are on their edit bars beside them since 2026-10-07 (EditRail), not on the bar.
// Every part is measured by the real Layout; the widgets have no content, so each shows the glyph
// edit mode gives a part with nothing to show.
//
// And files dragged over the bar at rest (DropLandingSheets): the new pin's cell where it will land,
// at the start's end, the middle's start, between two of the middle's and at the end's start, as
// drop-landing-<place>-<look>-<scale>.png.
//
// And the three sections themselves (SectionSheets): the bar a new config gets, with real widgets,
// at rest and in edit mode, across the bottom and down the left, with no app open, a few and so many
// that they fold into "…" — the start and the end standing still while the middle stays centred — as
// sections-<edge>-<state>-<apps>-<look>-<scale>.png.
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
    /// <summary>A bar as a new one is made: the weather at the left, the taskbar's own buttons in the middle, the system icons at the right.</summary>
    internal static List<PartSpec> DefaultBar() =>
    [
        Spec("weather", BarKind.WidgetsBoard) with { Section = BarSection.Start },
        Spec("start", BarKind.Start) with { Section = BarSection.Middle },
        Spec("search", BarKind.Search) with { Section = BarSection.Middle },
        Spec("taskview", BarKind.TaskView) with { Section = BarSection.Middle },
        Spec("media", BarKind.WidgetsBoard) with { Section = BarSection.End },
        Spec("tray", BarKind.Tray) with { Section = BarSection.End },
        Spec("wifi", BarKind.TouchKeyboard) with { Section = BarSection.End },
        Spec("sound", BarKind.DoNotDisturb) with { Section = BarSection.End },
        Spec("clock", BarKind.NightLight) with { Section = BarSection.End },
        Spec("desktop", BarKind.ShowDesktop) with { Section = BarSection.End },
    ];

    /// <summary>The default bar with more at its right: the weather and a clock again past the system icons.</summary>
    private static List<PartSpec> FullBar() =>
    [
        .. DefaultBar(),
        Spec("weather-2", BarKind.WidgetsBoard) with { Section = BarSection.End },
        Spec("clock-2", BarKind.NightLight) with { Section = BarSection.End },
    ];

    [UiFact]
    public void AddButtonSheets()
    {
        WindowShots.OnHiddenDesktop("add-buttons", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var width = dpi == 96 ? 1600 : 2400;
                    var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                    var rest = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, DefaultBar(), new Content());
                    var edit = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, DefaultBar(), new Content(), editing: true);
                    Assert.Empty(rest.EmptyRooms());
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    Shoot(gfx, rest, look, null, $"add-buttons-rest-{name}.png");
                    Shoot(gfx, edit, look, new EditView(), $"add-buttons-edit-{name}.png");
                    // A full bar.
                    var full = new Layout(gfx, dpi, area with { right = (int)(width * 0.42f) }, area with { right = (int)(width * 0.42f) }, Edge.Bottom, config, look, FullBar(), new Content(), editing: true);
                    Shoot(gfx, full, look, new EditView(), $"add-buttons-full-{name}.png");
                }
            }
        });
    }

    /// <summary>
    /// Files dragged over the bar as a new one is made, at rest: the mark is the new pin's own cell,
    /// one app button, at the very place the pin will land — after the weather at the left, before
    /// Start in the middle, between Start and search, where the middle opens round it and stays
    /// centred, and before the media widget at the right. To KARPO_PNG_DIR as
    /// drop-landing-{start-end,middle-first,middle-between,end-first}-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void DropLandingSheets()
    {
        WindowShots.OnHiddenDesktop("drop-landing", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var width = dpi == 96 ? 1600 : 2400;
                    var area = new RECT { left = 0, top = 0, right = width, bottom = (int)(dpi * 10) };
                    var specs = DefaultBar();
                    var rest = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, new Content());
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    foreach (var (place, slot) in new[]
                    {
                        ("start-end", new BarSlot(BarSection.Start, 1)),
                        ("middle-first", new BarSlot(BarSection.Middle, 1)),
                        ("middle-between", new BarSlot(BarSection.Middle, 2)),
                        ("end-first", new BarSlot(BarSection.End, 4)),
                    })
                    {
                        var (cell, lefts) = rest.Landing(slot);
                        Assert.False(cell.IsEmpty, $"no room for a pin {place} at {name}");
                        Assert.Equal(rest.Slot, cell.Width, 0.5f);
                        Shoot(gfx, rest, look, null, $"drop-landing-{place}-{name}.png", drag: new EditView(Landing: cell, Lefts: lefts));
                    }
                }
            }
        });
    }

    /// <summary>An open app for the busy bar: one window, not pinned.</summary>
    private static (Item, Mark) BusyApp(int index) =>
        (new Item { Target = $"https://app{index}.example", Name = $"App {index}", Kind = ItemKind.Link, Keys = [] }, new Mark(1, false, false));

    /// <summary>
    /// A busy bar: the bar a new config gets, with real widgets, 1920 DIPs long and 40 apps open
    /// (flush in their group since 2026-10-09, so 30 no longer fill it) — at rest, and in edit mode, where the apps fold into "…" to make the sections' "+" room and every
    /// widget stays, Wi-Fi and Sound among them (V4). To KARPO_PNG_DIR as
    /// add-buttons-busy-{rest,edit}-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void BusyBarSheets()
    {
        WindowShots.OnHiddenDesktop("add-buttons-busy", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var area = new RECT { left = 0, top = 0, right = (int)(1920 * dpi / 96), bottom = (int)(dpi * 10) };
                    var (specs, islands) = Made(gfx, config, Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch));
                    try
                    {
                        var content = new Content
                        {
                            Islands = islands,
                            Tiles = [.. Enumerable.Range(0, 40).Select(BusyApp)],
                        };
                        var rest = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content);
                        var edit = new Layout(gfx, dpi, area, area, Edge.Bottom, config, look, specs, content, editing: true);
                        var lost = Enumerable.Range(0, specs.Count)
                            .Where(part => specs[part].Kind.IsIsland() && rest.Placed(part) && !edit.Placed(part))
                            .Select(part => specs[part].Kind)
                            .ToList();
                        Assert.True(lost.Count == 0, $"edit mode drops {string.Join(", ", lost)} at {name}");
                        Assert.True(edit.Overflowed.Count > 0, "the apps fold into …");
                        Shoot(gfx, rest, look, null, $"add-buttons-busy-rest-{name}.png", content);
                        Shoot(gfx, edit, look, new EditView(), $"add-buttons-busy-edit-{name}.png", content);
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

    /// <summary>
    /// The three sections, with real widgets: a bar in all three (CentredBar), across the bottom and down
    /// the left, at rest and in edit mode, with no app open, four, and thirty — so many that they fold
    /// into "…". As the apps come and go the left and the right stand still to the pixel and the
    /// middle stays centred, until it has to move off the centre or fold. To KARPO_PNG_DIR as
    /// sections-{bottom,left}-{rest,edit}-{0,4,30}-&lt;look&gt;-&lt;scale&gt;.png.
    /// </summary>
    [UiFact]
    public void SectionSheets()
    {
        WindowShots.OnHiddenDesktop("sections", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    var name = $"{(dark ? "dark" : "light")}-{dpi * 100 / 96}";
                    var look = AddListPictures.LookFor(dark) with { Animations = false };
                    var config = new Config { Height = 48 };
                    var (specs, islands) = Made(gfx, config, CentredBar());
                    try
                    {
                        foreach (var edge in new[] { Edge.Bottom, Edge.Left })
                        {
                            var along = (int)(1600 * dpi / 96);
                            var area = edge.IsVertical()
                                ? new RECT { left = 0, top = 0, right = (int)(dpi * 10), bottom = along }
                                : new RECT { left = 0, top = 0, right = along, bottom = (int)(dpi * 10) };
                            float? weatherAt = null;
                            float? clockAt = null;
                            foreach (var apps in new[] { 0, 4, 30 })
                            {
                                var content = new Content
                                {
                                    Islands = islands,
                                    Tiles = [.. Enumerable.Range(0, apps).Select(BusyApp)],
                                };
                                foreach (var editing in new[] { false, true })
                                {
                                    var layout = new Layout(gfx, dpi, area, area, edge, config, look, specs, content, editing);
                                    if (!editing)
                                    {
                                        // The left and the right stand still as the apps come and go.
                                        var weather = layout.Parts.First(part => part.Kind == BarKind.FolderList).Left;
                                        // The clock's end: its faces are as wide as they read (docs\widgets.md §244),
                                        // and its bell's count may come in between two layouts.
                                        var clock = layout.Parts.Last(part => part.Kind == BarKind.FolderList).Right;
                                        Assert.Equal(weatherAt ?? weather, weather);
                                        Assert.Equal(clockAt ?? clock, clock);
                                        (weatherAt, clockAt) = (weather, clock);
                                        if (apps < 30)
                                        {
                                            // And the middle stays centred on the bar while it fits, with the
                                            // real widgets' own widths at both ends.
                                            var middle = Enumerable.Range(0, specs.Count)
                                                .Where(part => specs[part].Section == BarSection.Middle && layout.Parts[part].Width > 0f)
                                                .Select(part => layout.Parts[part])
                                                .ToList();
                                            var centre = (middle.Min(part => part.Left) + middle.Max(part => part.Right)) / 2f;
                                            Assert.True(
                                                Math.Abs(centre - layout.Length / 2f) <= 1f,
                                                $"the middle is centred on {centre}, not {layout.Length / 2f}, with {apps} apps at {edge} {name}");
                                        }
                                    }
                                    var state = editing ? "edit" : "rest";
                                    Shoot(gfx, layout, look, editing ? new EditView() : null, $"sections-{edge.Title().ToLowerInvariant()}-{state}-{apps}-{name}.png", content);
                                }
                            }
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
        });
    }

    /// <summary>
    /// The scene of <paramref name="layout"/> on a window of its own, shown on the hidden desktop, its
    /// bar saved to <paramref name="file"/>; with <paramref name="drag"/>, what something dragged over
    /// the bar at rest shows (<see cref="Scene.Drag"/>).
    /// </summary>
    private static void Shoot(Gfx gfx, Layout layout, Look look, EditView? view, string file, Content? content = null, EditView? drag = null)
    {
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content ?? new Content(), look, new Icons(), shown: true, edit: view);
            if (drag is not null)
            {
                scene.Drag(gfx, drag);
            }
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(250);
            var panel = layout.PanelRect;
            var part = new RECT { left = (int)panel.X, top = (int)panel.Y, right = (int)panel.Right, bottom = (int)panel.Bottom };
            Assert.True(WindowShots.Save(window, file, part), $"{file} came back blank");
        }
        finally
        {
            scene?.Dispose();
            _ = DestroyWindow(window);
        }
    }
}
