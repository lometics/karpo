// An attached folder as one surface with its bar (§651; the person, 2026-11-09: "the background should always span from edge
// to edge … you can redesign the shell around so that it matches the style"): the clock preset's folder at the end of a
// Taskbar along each edge of a display, open, Attached, in one column and in two, at 100 % and 150 %, dark and light — its
// ground the bar grown out from end to end, the bar's paint on the same desktop, the columns flush on the bar, a seam between
// two, the outline along its far side. To KARPO_PNG_DIR as
//
//   attached3-<bottom|left|right|top>-<one|two>-<dark|light>-<100|150>.png
//
// §656: on a Dock the same folder opens as cards (attached5-dock-bottom-dark-150.png).
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void AttachedDrawerSheets()
    {
        WindowShots.OnHiddenDesktop("attached-drawer-sheets", (gfx, instance) =>
        {
            var config = new Config();
            foreach (var dpi in new uint[] { 96, 144 })
            {
                foreach (var dark in new[] { true, false })
                {
                    var look = AddListPictures.LookFor(dark).Wearing(config) with { Animations = false };
                    foreach (var edge in new[] { Edge.Bottom, Edge.Left, Edge.Right, Edge.Top })
                    {
                        foreach (var two in new[] { false, true })
                        {
                            var layout = two ? StyledClock(edge, FolderStyle.Attached) : OneColumnClock(edge);
                            EditedDisplay(gfx, instance, dpi, config, look, layout,
                                $"attached3-{edge.ToString().ToLowerInvariant()}-{(two ? "two" : "one")}-{(dark ? "dark" : "light")}-{dpi * 100 / 96}.png",
                                editing: false, folder: true, folderFace: FolderFaces.ClockId);
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// §656: on a Dock along the bottom the clock preset's folder, its style Attached, opens as cards (a dock's folder is never
    /// attached), dark, at 150 %. To KARPO_PNG_DIR as <c>attached5-dock-bottom-dark-150.png</c>.
    /// </summary>
    [UiFact]
    public void AttachedOnADockSheet()
    {
        WindowShots.OnHiddenDesktop("attached-dock-sheet", (gfx, instance) =>
        {
            var config = new Config();
            var look = AddListPictures.LookFor(dark: true).Wearing(config) with { Animations = false };
            var layout = PictureLayout(LayoutType.Dock, Edge.Bottom);
            layout.Islands.First(island => island.Kind == BarKind.FolderList).Folder!.Style = FolderStyle.Attached;
            EditedDisplay(gfx, instance, 144, config, look, layout, "attached5-dock-bottom-dark-150.png",
                editing: false, folder: true, folderFace: FolderFaces.ClockId);
        });
    }

    /// <summary>A Taskbar along <paramref name="edge"/> with the clock preset's folder at its end, attached, its lines in one column.</summary>
    private static Picture OneColumnClock(Edge edge)
    {
        var layout = PictureLayout(LayoutType.Taskbar, edge);
        var clock = ClockFolder();
        clock.Section = BarSection.End;
        clock.Folder!.Style = FolderStyle.Attached;
        layout.Islands.Insert(layout.Islands.FindIndex(island => island.Kind == BarKind.ShowDesktop) is var at and >= 0 ? at : layout.Islands.Count, clock);
        Sections.Order(layout.Islands);
        return layout;
    }
}
