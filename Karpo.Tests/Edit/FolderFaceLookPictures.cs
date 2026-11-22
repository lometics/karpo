// A folder's face on the bar (docs\widgets.md §376): the dock's real scene at 150 %, a group of three
// folders, captured as the compositor draws
// it (WindowShots), to KARPO_PNG_DIR as folder-face-<what>-<look>.png.
//
// The person, 2026-10-28: "if a folder face is on the edge of a section its corners are rounded. this
// should not be, or at least they should have the same roundness of corners everywhere", and "the folder
// that is open in karpo should be highlighted in some way (maybe different background or so)".
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    /// <summary>
    /// The open folder's face wears the focused app's plate until it closes (<see cref="Scene.Open"/>): lit
    /// at rest where it was not, edged where a face under the pointer is not, and lit further under the
    /// pointer and less pressed, so hover and press still read — in the bar's colours (§567: no face has a look of its own).
    /// folder-face-open-&lt;state&gt;-bar-&lt;look&gt;.png.
    /// </summary>
    [UiFact]
    public void TheOpenFolderFaceIsLit()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("folder-face-open", (gfx, _) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var name = dark ? "dark" : "light";
                var look = AddListPictures.LookFor(dark) with { Animations = false, Material = Material.Opaque };
                WithFolderBar(gfx, dark, (layout, content, parts) =>
                {
                    foreach (var (part, which) in new[] { (parts[1], "bar") })
                    {
                        var key = layout.Specs[part].Key;
                        var card = layout.CardRect(part);
                        // Beside the glyph, mid-height; and on the card's side, where the plate's edge is.
                        var inside = new POINT { x = (int)card.X + 5, y = (int)(card.Y + (card.Height / 2)) };
                        var edge = new POINT { x = (int)card.X + 1, y = (int)(card.Y + (card.Height / 2)) };
                        var hit = Hit.Cell(part, 0);
                        Dictionary<string, (Argb Inside, Argb Edge)> seen = [];
                        void Shoot(string state, Action<Scene> then) =>
                            ShootLooks(gfx, layout, content, look, $"folder-face-open-{state}-{which}-{name}.png", scene =>
                            {
                                then(scene);
                                if (state.StartsWith("open", StringComparison.Ordinal) && scene.OpenShown is null)
                                {
                                    failures.Add($"{name}, {which}: open, no plate shows");
                                }
                            }, pixel => seen[state] = (pixel(inside), pixel(edge)));
                        Shoot("rest", _ => { });
                        Shoot("hover", scene => scene.Highlight(hit, pressed: false));
                        Shoot("open", scene => scene.Open(key));
                        Shoot("open-hover", scene =>
                        {
                            scene.Open(key);
                            scene.Highlight(hit, pressed: false);
                        });
                        Shoot("open-pressed", scene =>
                        {
                            scene.Open(key);
                            scene.Highlight(hit, pressed: true);
                        });
                        Shoot("closed", scene =>
                        {
                            scene.Open(key);
                            scene.Open(null);
                        });
                        void Differ(string one, string other, bool edges = false)
                        {
                            var (a, b) = edges ? (seen[one].Edge, seen[other].Edge) : (seen[one].Inside, seen[other].Inside);
                            if (Apart(a, b) < 4)
                            {
                                failures.Add($"{name}, {which}: {(edges ? "the edge" : "the face")} {one} ({a}) is {other} ({b})");
                            }
                        }
                        Differ("open", "rest");
                        Differ("open-hover", "open");
                        Differ("open-pressed", "open-hover");
                        Differ("open", "hover", edges: true);
                        if (Apart(seen["closed"].Inside, seen["rest"].Inside) > 1)
                        {
                            failures.Add($"{name}, {which}: closed again, the face is {seen["closed"].Inside}, not as at rest {seen["rest"].Inside}");
                        }
                    }
                });
            }
        });
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    /// <summary>
    /// The default bar at 150 % with three folders at its start wearing their icon, one group of their own: the bar's
    /// own buttons of the start stand in the middle (§511: a section parts a group from the next).
    /// </summary>
    private static void WithFolderBar(Gfx gfx, bool dark, Action<Layout, Content, int[]> body)
    {
        var config = new Config { Height = 48 };
        var area = new RECT { left = 0, top = 0, right = 2400, bottom = 1440 };
        var bar = Sections.DefaultLayout(pins: [], inputs: NoPenNoTouch, windows: WindowsTaskbar.OutOfTheBox);
        foreach (var item in bar.Where(item => Sections.Known(item.Section) == BarSection.Start))
        {
            item.Section = BarSection.Middle;
        }
        const int start = 0;
        for (var at = 0; at < 3; at++)
        {
            var item = BarKind.FolderList.New();
            item.Id = $"face-{at}";
            item.Section = BarSection.Start;
            item.Folder!.Face = new FolderFace();
            bar.Insert(start + at, item);
        }
        var (specs, islands) = Made(gfx, config, bar);
        try
        {
            var content = new Content { Islands = islands };
            var layout = new Layout(gfx, 144, area, area, Edge.Bottom, config, AddListPictures.LookFor(dark), specs, content, editing: false);
            int[] parts = [.. Enumerable.Range(0, 3).Select(at => specs.FindIndex(spec => spec.Key.EndsWith($"face-{at}", StringComparison.Ordinal)))];
            body(layout, content, parts);
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
