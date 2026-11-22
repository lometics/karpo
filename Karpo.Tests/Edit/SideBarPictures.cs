// Bars down the sides of a display (the person, 2026-10-12: "the taskbar at the moment i can only show
// at the top or bottom, but i want to be able to show it on the left and right as well, same for dock
// and two row dock"; the dock with two rows went in §438, the Top and bottom in §488): a Taskbar whose
// display's edge is the left or the right and a Dock there; each at rest
// and in edit mode, and at rest with the clock's folder open, which opens sideways, away from its
// bar, over no other. A display 1280 DIPs wide at 150 %, another display against its right edge, dark
// and light. To KARPO_PNG_DIR as side-<type>-<left|right>-<rest|edit|folder>-<dark|light>-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TheDock = Karpo.Dock.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void SideBarSheets()
    {
        WindowShots.OnHiddenDesktop("side-bars", (gfx, instance) =>
        {
            const uint dpi = 144;
            var config = new Config();
            foreach (var dark in new[] { true, false })
            {
                var look = AddListPictures.LookFor(dark) with { Animations = false };
                var theme = dark ? "dark" : "light";
                foreach (var edge in new[] { Edge.Left, Edge.Right })
                {
                    var side = edge == Edge.Left ? "left" : "right";
                    foreach (var (name, layout) in new[]
                    {
                        ("taskbar", PictureLayout(LayoutType.Taskbar, edge)),
                        ("dock", PictureLayout(LayoutType.Dock, edge, EdgeMode.Float)),
                    })
                    {
                        EditedDisplay(gfx, instance, dpi, config, look, layout, $"side-{name}-{side}-rest-{theme}-150.png", editing: false);
                        EditedDisplay(gfx, instance, dpi, config, look, layout, $"side-{name}-{side}-edit-{theme}-150.png", editing: true);
                        EditedDisplay(gfx, instance, dpi, config, look, layout, $"side-{name}-{side}-folder-{theme}-150.png", editing: false, folder: true);
                    }
                }
            }
        });
    }

    /// <summary>
    /// The clock's folder of the bar shown all the time (a place's where none is), opened as the dock
    /// opens one (DockWindow.BlockAnchor, WorkArea): from the middle of the clock's inner side, away
    /// from the bar's edge, inside the display less every other bar shown all the time — the full
    /// height, at rest as arranged, the edit bar (<paramref name="deck"/>) over it arranged (§329). It
    /// must open away from its bar — right of a bar on the left, left of one on the right — inside the
    /// display and over nothing in <paramref name="taken"/> but its own bar's room and the edit bar,
    /// none of its buttons under the edit bar. The folder, which the caller disposes.
    /// </summary>
    private static FolderFlyout FolderFrom(Gfx gfx, HINSTANCE instance, Look look, float scale, List<(Layout Layout, IslandCollection Islands, bool Standing)> surfaces, RectI bounds, RECT? deck, List<RECT> taken, string file, bool editing, string? face = null)
    {
        // The clock's: the folder wearing the clock face, or the time over the date (a new bar's, 2026-10-13); §472: or the
        // one whose first face is <paramref name="face"/>.
        bool Clock(IslandCollection islands, PartSpec spec) =>
            spec.Kind == BarKind.FolderList && islands.TryGetValue(spec.Key, out var island) && island is FolderIsland { Options.Face.Kind: var kind }
            && (face is null ? kind is FolderFaces.ClockId or FolderFaces.DateTimeId : kind == face);
        var (from, islands, _) = surfaces
            .Where(entry => entry.Layout.Specs.Any(spec => Clock(entry.Islands, spec)))
            .OrderBy(entry => entry.Standing ? 0 : 1)
            .First();
        var part = from.Specs.ToList().FindIndex(spec => Clock(islands, spec));
        var folder = Assert.IsType<FolderIsland>(islands[from.Specs[part].Key]);
        var r = from.BlockRect(part);
        var (x, y) = from.Edge switch
        {
            Edge.Top => (r.X + (r.Width / 2f), r.Bottom),
            Edge.Left => (r.Right, r.Y + (r.Height / 2f)),
            Edge.Right => (r.X, r.Y + (r.Height / 2f)),
            _ => (r.X + (r.Width / 2f), r.Y),
        };
        var anchor = new POINT { x = from.X + (int)x, y = from.Y + (int)y };
        var screen = new RECT { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
        var own = Strip(from, bounds);
        // The dock's (DockWindow.FolderWork): less every other bar shown all the time, and its own (§287).
        var work = TheDock.PopupRoom(screen, surfaces.Where(entry => entry.Standing && entry.Layout != from).Select(entry => (entry.Layout.Edge, Strip(entry.Layout, bounds))).Append((from.Edge, own)));
        // Arranged, the edit bar over it (Dock.ShowDecks, §329), the popups kept off it.
        var editBar = editing && deck is { } panel ? panel : default;
        FlyoutHost.Avoided = editing && deck is { } avoided ? [avoided] : [];
        // §651: an attached folder's ground on the desktop the bars stand on, as the dock lays it on its wallpaper.
        var flyout = new FolderFlyout(gfx, instance, 0)
        {
            Covered = editBar,
            Paper = ground => Wash(gfx, look, new Vector2(bounds.Width, bounds.Height), new Vector2(ground.left - bounds.Left, ground.top - bounds.Top)),
        };
        try
        {
            // The dock's: arranged, the window reaches the display's edges, over its bars (§246).
            flyout.Show(gfx, look, scale, anchor, work, from.Edge, folder, editing: editing, display: screen, dock: from.Floating);
        }
        finally
        {
            FlyoutHost.Avoided = [];
        }
        var window = EditDeckClickTests.WindowOf(flyout.Hwnd);
        var shown = flyout.PanelOnScreen;
        // Away from its bar: nothing of it over the bar it was opened from.
        var away = from.Edge switch
        {
            Edge.Left => shown.left >= anchor.x,
            Edge.Right => shown.right <= anchor.x,
            Edge.Top => shown.top >= anchor.y,
            _ => shown.bottom <= anchor.y,
        };
        Assert.True(away, $"{file}: the folder's panel {shown.left},{shown.top}..{shown.right},{shown.bottom} runs over its bar (anchor {anchor.x},{anchor.y})");
        Assert.True(shown.left >= bounds.Left && shown.top >= bounds.Top && shown.right <= bounds.Right && shown.bottom <= bounds.Bottom, $"{file}: the folder runs off the display");
        // Over no other bar; the edit bar stands over it (§329), none of its buttons under it.
        foreach (var other in taken.Where(other => !other.Equals(own) && !other.Equals(editBar)))
        {
            Assert.False(Overlaps(shown, other), $"{file}: the folder's panel runs over {other.left},{other.top}..{other.right},{other.bottom}");
        }
        for (var index = 0; editing && index < flyout.Plan!.Adds.Count; index++)
        {
            if (flyout.Plan.Adds[index].Box is var add && add.right > add.left && flyout.ScreenOfAdd(index) is var at)
            {
                Assert.False(at.x > editBar.left && at.x < editBar.right && at.y > editBar.top && at.y < editBar.bottom, $"{file}: the folder's button {index} at {at.x},{at.y} is under the edit bar");
            }
        }
        _ = window;
        return flyout;
    }
}
