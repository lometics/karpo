// Every widget of the bar's own buttons, the app buttons and the folders on a bar down a side of the
// display (the person, 2026-10-13: "make sure that all the widgets look good on the side as well. if
// possible dont try to flip text"): the widgets board, Start, search as its box and as its button,
// Task view, two pins (an app and a site), three open apps titled ("Never combine",
// the labels a side bar has no room across for) with a badge and a progress bar, a folder wearing
// its 2x2 preview, a folder showing its icon, one wearing three
// faces, the mail, contacts, clipboard, desktop switcher, layout switcher and project folders, the
// snipping tool, Windows' settings and show desktop. On the left and on the right, as a bar
// (islands) and as a dock (floating), at 28, 40, 48, 64 and 96 DIPs side by side, each at rest and
// in edit mode; 100 % and 150 %, dark and light. To KARPO_PNG_DIR as
// side-kinds-<islands|floating>-<left|right>-<dark|light>-<100|150>.png; and the same bar 720 DIPs
// long with 30 apps open as side-kinds-busy-*.png.
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
    /// The thicknesses a side bar is drawn at, the least a bar may be to a wide one.
    /// </summary>
    private static readonly int[] SideThicknesses = [28, 40, 48, 64, 96];

    /// <summary>The side bar's widgets, by section, each made as a new bar makes it.</summary>
    private static List<BarItem> SideKinds()
    {
        var machine = new PresetMachine(HasBattery: false, FixedDrives: []);
        BarItem Preset(string id) => Presets.Folder(Presets.Find(id)!, machine);
        BarItem Folder(string id, string face)
        {
            var island = BarKind.FolderList.New();
            island.Id = id;
            var folder = island.Folder!;
            folder.Face = FolderFace.Of(face);
            folder.Lists = [[]];
            foreach (var kind in new[] { BarKind.Accessibility, BarKind.EnergySaver, BarKind.GameMode, BarKind.NightLight })
            {
                folder.Put(kind.New().AsCell());
            }
            return island;
        }
        BarItem Faces(string id, params string[] faces)
        {
            var island = Folder(id, faces[0]);
            island.Folder!.Faces = [.. faces.Select(FolderFace.Of)];
            return island;
        }
        BarItem Pin(string id)
        {
            var pin = BarKind.Pin.New();
            pin.Id = id;
            return pin;
        }
        var box = BarKind.Search.New();
        box.Id = "search-box";
        box.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
        var button = BarKind.Search.New();
        button.Id = "search-button";
        var list = new List<(BarItem Item, BarSection Section)>
        {
            (BarKind.WidgetsBoard.New(), BarSection.Start),
            (BarKind.Start.New(), BarSection.Start),
            (box, BarSection.Start),
            (button, BarSection.Start),
            (BarKind.TaskView.New(), BarSection.Start),
            (Pin("pin-app"), BarSection.Middle),
            (Pin("pin-site"), BarSection.Middle),
            (BarKind.Apps.New(), BarSection.Middle),
            (Folder("folder-icon", FolderFaces.IconId), BarSection.End),
            (Faces("folder-faces", FolderFaces.MailId, FolderFaces.ContactsId, FolderFaces.DesktopsId), BarSection.End),
            (Preset(Presets.MailId), BarSection.End),
            (Preset(Presets.ContactsId), BarSection.End),
            (Preset(Presets.ClipboardId), BarSection.End),
            (Preset(Presets.DesktopsId), BarSection.End),
            (Preset(Presets.LayoutsId), BarSection.End),
            (Preset(Presets.ProjectId), BarSection.End),
            (BarKind.SnippingTool.New(), BarSection.End),
            (BarKind.WindowsSettings.New(), BarSection.End),
            (BarKind.ShowDesktop.New(), BarSection.End),
        };
        foreach (var (item, section) in list)
        {
            item.Section = section;
        }
        var islands = list.ConvertAll(entry => entry.Item);
        Sections.Order(islands);
        return islands;
    }

    /// <summary>
    /// The pins' tiles (an app's, and a site's, which wears its letter) and three open apps, titled:
    /// the first with a badge, the second focused with a download half done.
    /// </summary>
    private static Content SideContent(IslandCollection islands, int more = 0) => new()
    {
        Islands = islands,
        Tiles =
        [
            AppTile(0, new Mark(0, false, false)),
            (BusyApp(1).Item1, new Mark(0, false, false)),
            AppTile(2, new Mark(1, false, false, new TileSignal(Badge: Badge.Of("3")))),
            AppTile(3, new Mark(1, Active: true, Attention: false, new TileSignal(ProgressKind.Normal, 0.5f))),
            AppTile(4, new Mark(2, false, false)),
            .. Enumerable.Range(5, more).Select(index => AppTile(index, new Mark(1, false, false))),
        ],
        Labels = [null, null, "Inbox - Mail", "Quarterly report - final version (2) - Word", "Downloads - File Explorer", .. Enumerable.Range(5, more).Select(index => $"Window {index}")],
    };

    [UiFact]
    public void SideKindSheets()
    {
        var problems = new List<string>();
        WindowShots.OnHiddenDesktop("side-kinds", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var backdrop = dark ? ((byte)0x40, (byte)0x30, (byte)0x20) : ((byte)0xF0, (byte)0xD8, (byte)0xC0);
                foreach (var dpi in new uint[] { 96, 144 })
                {
                    foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                    {
                        foreach (var edge in new[] { Edge.Left, Edge.Right })
                        {
                            var shots = new List<(byte[] Canvas, int Width, int Height)>();
                            foreach (var height in SideThicknesses)
                            {
                                foreach (var editing in new[] { false, true })
                                {
                                    shots.Add(SideBar(gfx, dpi, height, style, edge, look, editing, backdrop, problems));
                                }
                            }
                            var file = $"side-kinds-{style.ToString().ToLowerInvariant()}-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png";
                            WindowShots.SaveSideBySide(shots, file, backdrop, gap: (int)(16 * dpi / 96));
                        }
                    }
                }
            }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    /// <summary>
    /// The same bar down a side 720 DIPs long with 30 apps open, more than it has room for: the rest
    /// behind "…", which stands inside the bar as a button does. At 28, 48 and 96 DIPs, at rest and in
    /// edit mode, at 150 %. To KARPO_PNG_DIR as side-kinds-busy-<islands|floating>-<left|right>-<dark|light>-150.png.
    /// </summary>
    [UiFact]
    public void BusySideKindSheets()
    {
        var problems = new List<string>();
        WindowShots.OnHiddenDesktop("side-kinds-busy", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var backdrop = dark ? ((byte)0x40, (byte)0x30, (byte)0x20) : ((byte)0xF0, (byte)0xD8, (byte)0xC0);
                foreach (var style in new[] { BarStyle.Islands, BarStyle.Floating })
                {
                    foreach (var edge in new[] { Edge.Left, Edge.Right })
                    {
                        var shots = new List<(byte[] Canvas, int Width, int Height)>();
                        foreach (var height in new[] { 28, 48, 96 })
                        {
                            foreach (var editing in new[] { false, true })
                            {
                                shots.Add(SideBar(gfx, 144, height, style, edge, look, editing, backdrop, problems, more: 25));
                            }
                        }
                        WindowShots.SaveSideBySide(shots, $"side-kinds-busy-{style.ToString().ToLowerInvariant()}-{edge.ToString().ToLowerInvariant()}-{(dark ? "dark" : "light")}-150.png", backdrop, gap: 24);
                    }
                }
            }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    /// <summary>
    /// The side bar of <see cref="SideKinds"/> at <paramref name="height"/> DIPs: every widget placed,
    /// inside the bar's thickness and centred across it, no app button labelled, search a button; drawn by the real
    /// scene and kept for the sheet.
    /// </summary>
    private static (byte[] Canvas, int Width, int Height) SideBar(Gfx gfx, uint dpi, int height, BarStyle style, Edge edge, Look look, bool editing, (byte, byte, byte) backdrop, List<string> problems, int more = 0)
    {
        var config = new Config { Height = height, Style = style, DefaultLayout = { Combine = ButtonCombine.Never } };
        var screen = new RECT { left = 0, top = 0, right = (int)(1280 * dpi / 96), bottom = (int)((more > 0 ? 720 : 2400) * dpi / 96) };
        var items = SideKinds();
        var (specs, islands) = Made(gfx, config, items);
        var window = MakeWindow();
        DesktopWindowTarget? target = null;
        Scene? scene = null;
        try
        {
            var content = SideContent(islands, more);
            var layout = new Layout(gfx, dpi, screen, screen, edge, config, look, specs, content, editing);
            var what = $"{style} {edge} {height} DIPs at {dpi * 100 / 96} %{(editing ? ", edited" : "")}";
            Assert.True(layout.Vertical);
            Check(more > 0 == layout.Overflowed.Count > 0, $"{what}: \"…\" {(more > 0 ? "missing from a full" : "on a")} bar");
            if (more > 0)
            {
                var overflow = layout.OverflowCell();
                Check(!overflow.IsEmpty && overflow.X >= layout.PanelRect.X - 0.5f && overflow.Right <= layout.PanelRect.Right + 0.5f, $"{what}: \"…\" runs out of the bar");
            }
            var panel = layout.PanelRect;
            void Check(bool ok, string problem)
            {
                if (!ok)
                {
                    problems.Add(problem);
                }
            }
            for (var part = 0; part < specs.Count; part++)
            {
                // On a full bar what has no room gives way, as on any bar, and past Windows' own
                // buttons, which never do, the tail falls off the end (Layout's "tail falls off").
                if (more > 0 && layout.BlockRect(part).IsEmpty)
                {
                    continue;
                }
                Check(layout.Placed(part), $"{what}: {specs[part].Kind} {specs[part].Key} is not on the bar");
                var block = layout.BlockRect(part);
                Check(block.X >= panel.X - 0.5f && block.Right <= panel.Right + 0.5f, $"{what}: {specs[part].Kind} {specs[part].Key} runs out of the bar across ({block.X}..{block.Right} in {panel.X}..{panel.Right})");
                Check(MathF.Abs((block.X + block.Right) - (panel.X + panel.Right)) <= 2f, $"{what}: {specs[part].Kind} {specs[part].Key} is not centred across the bar");
                Check(!(layout.Boxed(part)), $"{what}: the search box on a side bar");
            }
            for (var tile = 0; tile < content.Tiles.Count; tile++)
            {
                if (layout.Overflowed.Contains(tile))
                {
                    continue;
                }
                Check(!(layout.Labelled(tile)), $"{what}: app button {tile} labelled across a side bar");
                var cell = layout.TileCell(tile);
                Check(cell.X >= panel.X - 0.5f && cell.Right <= panel.Right + 0.5f, $"{what}: app button {tile} runs out of the bar");
            }
            _ = SetWindowPos(window, HWND.NULL, 0, 0, (int)layout.Width, (int)layout.Height, SWP.SWP_NOZORDER | SWP.SWP_NOACTIVATE);
            target = Target(gfx, window);
            scene = Scenes.Build(gfx, target, layout, content, look, new Icons(), shown: true, edit: editing ? new EditView() : null);
            _ = ShowWindow(window, SW.SW_SHOWNOACTIVATE);
            WindowShots.Settle(200);
            // The bar and, on a floating dock, the room off the screen's edge it stands in.
            var margin = (int)(8 * dpi / 96);
            var frame = new RECT
            {
                left = edge == Edge.Left ? 0 : (int)panel.X - margin,
                top = (int)panel.Y - margin,
                right = edge == Edge.Left ? (int)panel.Right + margin : (int)layout.Width,
                bottom = (int)panel.Bottom + margin,
            };
            var (canvas, every) = WindowShots.Together([(window, frame)], frame, "side-kinds", backdrop);
            Assert.True(every, $"{what} came back blank");
            return (canvas, frame.right - frame.left, frame.bottom - frame.top);
        }
        finally
        {
            scene?.Dispose();
            if (target is not null)
            {
                target.Root = null;
            }
            _ = DestroyWindow(window);
            foreach (var island in islands.Values)
            {
                island.Dispose();
            }
        }
    }
}
