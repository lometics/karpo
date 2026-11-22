// Bars along the top of a display (the person, 2026-10-09: "the topbar implementation is also
// wrong"): a Taskbar whose display's edge is the top and a Dock there, each at rest and in edit mode,
// with the edit bar across from the bar (§296: at the bottom); and a menu opened from the Taskbar (its
// clock at rest, a widget right under the edit bar in edit mode), which opens downwards, under its bar,
// never over another (Dock.PopupRoom) nor over the edit bar (FlyoutHost.Avoided). A display 1280 DIPs
// wide at 150 %, dark, another display against its right edge. To KARPO_PNG_DIR as
// top-<taskbar|dock>-<rest|edit>-dark-150.png and top-taskbar-<rest|edit>-menu-dark-150.png.
//
// Needs Direct2D and a window station: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Islands;
using TheDock = Karpo.Dock.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Edit;

public sealed unsafe partial class SceneTreeTests
{
    [UiFact]
    public void TopBarSheets()
    {
        WindowShots.OnHiddenDesktop("top-bars", (gfx, instance) =>
        {
            const uint dpi = 144;
            var look = AddListPictures.LookFor(dark: true) with { Animations = false };
            var config = new Config();
            var taskbar = PictureLayout(LayoutType.Taskbar, Edge.Top);
            var dock = PictureLayout(LayoutType.Dock, Edge.Top, EdgeMode.Float);
            foreach (var (name, layout) in new[] { ("taskbar", taskbar), ("dock", dock) })
            {
                foreach (var editing in new[] { false, true })
                {
                    var mode = editing ? "edit" : "rest";
                    EditedDisplay(gfx, instance, dpi, config, look, layout, $"top-{name}-{mode}-dark-150.png", editing: editing);
                    if (name != "dock")
                    {
                        EditedDisplay(gfx, instance, dpi, config, look, layout, $"top-{name}-{mode}-menu-dark-150.png", editing: editing, popup: true);
                    }
                }
            }
        });
    }

    /// <summary>
    /// A menu opened from the clock of the bar along the top of the display (<paramref name="surfaces"/>,
    /// each a bar shown all the time or a place), as the dock opens one: from the middle of the clock's
    /// inner side, away from the top (Edge.Top), inside what popups stay inside — the display less every
    /// bar shown all the time and, in edit mode, the edit bar (<paramref name="deck"/>; Dock.PopupRoom).
    /// It must stand under its bar and every other, and under the edit bar, over nothing in
    /// <paramref name="taken"/>. The menu, which the caller disposes.
    /// </summary>
    private static Flyout PoppedFromTheTop(Gfx gfx, HINSTANCE instance, Look look, float scale, List<(Layout Layout, bool Standing)> surfaces, RectI bounds, RECT? deck, List<RECT> taken, string file)
    {
        var (from, _) = surfaces
            .Where(entry => entry.Layout.Edge == Edge.Top && entry.Layout.Specs.Any(spec => spec.Kind == BarKind.FolderList))
            .OrderBy(entry => entry.Standing ? 0 : 1)
            .First();
        // At rest the clock's; in edit mode a widget's right under the edit bar, which it must keep clear of.
        // The clock is the bar's last folder: the first is the keyboard's since it became a preset, and
        // a menu from it reached the edit bar's corner with its shadow once the battery face before it
        // showed its greyed 100 % (§247).
        var part = from.Specs.ToList().FindLastIndex(spec => spec.Kind == BarKind.FolderList);
        if (deck is { } over)
        {
            var under = Enumerable.Range(0, from.Specs.Count).FirstOrDefault(index =>
                from.BlockRect(index) is { IsEmpty: false } rect && from.X + rect.X + (rect.Width / 2f) is var middle && middle > over.left && middle < over.right, -1);
            part = under >= 0 ? under : part;
        }
        var block = from.BlockRect(part);
        var anchor = new POINT { x = from.X + (int)(block.X + (block.Width / 2f)), y = from.Y + (int)block.Bottom };
        var screen = new RECT { left = bounds.Left, top = bounds.Top, right = bounds.Right, bottom = bounds.Bottom };
        var work = TheDock.PopupRoom(screen, surfaces.Where(entry => entry.Standing).Select(entry => (entry.Layout.Edge, Strip(entry.Layout, bounds))));
        // edit: the edit bar is what every popup keeps clear of (Dock.ShowDecks).
        FlyoutHost.Avoided = deck is { } panel ? [panel] : [];
        var menu = new Flyout(gfx, instance, 0);
        List<FlyoutItem> items =
        [
            new(1, "Adjust date and time", new FlyoutIcon.Glyph("")),
            new(2, "Open Calendar", new FlyoutIcon.Glyph("")),
            new(0, "", Separator: true),
            new(3, "Notifications", new FlyoutIcon.Glyph("")),
            new(4, "Taskbar settings", new FlyoutIcon.Glyph("")),
        ];
        try
        {
            menu.Show(gfx, look, scale, items, FlyoutLayout.List, anchor, work, Edge.Top, "Clock");
        }
        finally
        {
            FlyoutHost.Avoided = [];
        }
        Assert.True(menu.IsOpen);
        var window = EditDeckClickTests.WindowOf(menu.Hwnd);
        // Under the clock's bar, and down from it: nothing of it over the bar it was opened from.
        Assert.True(window.top >= anchor.y, $"{file}: the menu's window starts at {window.top}, over its bar ({anchor.y})");
        Assert.True(window.bottom <= bounds.Bottom, $"{file}: the menu runs off the display");
        // Over no other bar, place or edit bar. Its own bar it reaches only up to the clock's inner side,
        // as a menu from a bottom bar does (FlyoutHost: the window ends at the anchor on the bar's side).
        var own = Strip(from, bounds);
        foreach (var other in taken.Where(other => !other.Equals(own)))
        {
            Assert.False(Overlaps(window, other), $"{file}: the menu's window {window.top}..{window.bottom} runs over {other.top}..{other.bottom}");
        }
        return menu;
    }
}
