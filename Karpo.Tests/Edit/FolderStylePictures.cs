// A folder's style (§647): the clock preset's folder at the end of a Taskbar along each edge of a display, open, Attached —
// its columns on a ground in the bar's colour that adjoins the bar, down a side bar the whole height — and as Cards for
// comparison, its lines in two columns; dark and light, in Karpo's own Appearance. To KARPO_PNG_DIR as
// folderstyle-<attached|cards>-<edge>-<dark|light>-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Tests.Islands;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void FolderStyleSheets()
    {
        WindowShots.OnHiddenDesktop("folder-style-sheets", (gfx, instance) =>
        {
            const uint dpi = 144;
            var config = new Config();
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark).Wearing(config) with { Animations = false };
                var theme = dark ? "dark" : "light";
                foreach (var edge in new[] { Edge.Bottom, Edge.Left, Edge.Right, Edge.Top })
                {
                    foreach (var style in new[] { FolderStyle.Attached, FolderStyle.Cards })
                    {
                        EditedDisplay(gfx, instance, dpi, config, look, StyledClock(edge, style),
                            $"folderstyle-{style.ToString().ToLowerInvariant()}-{edge.ToString().ToLowerInvariant()}-{theme}-150.png",
                            editing: false, folder: true, folderFace: FolderFaces.ClockId);
                    }
                }
            }
        });
    }

    /// <summary>A Taskbar along <paramref name="edge"/> with the clock preset's folder at its end in <paramref name="style"/>, its last two lines a second column.</summary>
    private static Picture StyledClock(Edge edge, FolderStyle style)
    {
        var layout = PictureLayout(LayoutType.Taskbar, edge);
        var clock = ClockFolder();
        clock.Section = BarSection.End;
        var folder = clock.Folder!;
        folder.Style = style;
        var first = folder.Lists[0];
        folder.Lists.Add(first.GetRange(first.Count - 2, 2));
        first.RemoveRange(first.Count - 2, 2);
        layout.Islands.Insert(layout.Islands.FindIndex(island => island.Kind == BarKind.ShowDesktop) is var at and >= 0 ? at : layout.Islands.Count, clock);
        Sections.Order(layout.Islands);
        return layout;
    }
}
