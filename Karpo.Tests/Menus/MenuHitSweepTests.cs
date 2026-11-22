// menus: every pixel of a menu that stands for a row is that row's, for the pointer and for a click
// (the person, 2026-10-28: "the size menu item is not clickable in the whole area as the other ones are",
// "sometimes also just areas of the settings menu header you cant click to go back. fix all of those click
// box issues"; docs\widgets.md §379). Swept on a real menu on a desktop of the test's own
// (KARPO_UI_TESTS=1), the pointer moved and pressed over every third pixel as Windows would send it:
//
//  - **A row is its whole slot.** Its plate, the air either side of it to the panel's edges and the gap
//    to the rows over and under it light that row and a click there is its — a menu's own rows, a level's,
//    the rows standing still at its foot ("Height", "Appearance"), scrolled or not. No pixel between two
//    rows is nobody's.
//  - **A level's head is its whole band.** From the panel's top to its rule, edge to edge, the pointer
//    lights the head and one press and release go back.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class MenuHitSweepTests
{
    private const float Scale = 1.5f;

    private const int Step = 3;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>Edit mode's menus of the person's kinds of lines and widgets, with their foot: "Height" and "Appearance".</summary>
    private static IEnumerable<(string Name, List<FlyoutItem> Items)> Menus()
    {
        WidgetItem[] widgets = [ListKind.WeatherNow.New(), ListKind.Calendar.New(), ListKind.Pin.New(), ListKind.Markets.New(), BarKind.Volume.New(), BarKind.FolderList.New()];
        foreach (var widget in widgets)
        {
            List<OptionPick> picks = [];
            List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(widget), .. OptionMenu.Settings(widget, picks, new OptionDevices(), new OptionPlace(new Config(), Dark: true), FolderSide.Face, roomCells: 8)];
            yield return (widget is BarItem bar ? bar.Kind.ToString() : ((CellItem)widget).Kind.ToString(), items);
        }
    }

    private static nint At(int x, int y) => (nint)((y << 16) | (x & 0xFFFF));

    /// <summary>The panel's top left in the menu window's own px.</summary>
    private static POINT Origin(Flyout menu)
    {
        var panel = menu.PanelOnScreen;
        var corner = new POINT { x = panel.left, y = panel.top };
        _ = ScreenToClient(menu.Hwnd, &corner);
        return corner;
    }

    /// <summary>The row the pointer at (<paramref name="x"/>, <paramref name="y"/>), panel px, lights.</summary>
    private static uint? LitAt(Flyout menu, POINT origin, int x, int y)
    {
        _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(origin.x + x, origin.y + y));
        return menu.HotId;
    }

    /// <summary>
    /// Every row's slot on what <paramref name="menu"/> shows now: down its plate's height and the gap to its
    /// neighbours, edge to edge for a row across the menu; each point lights its row. The misses, said.
    /// </summary>
    private static void SweepRows(Flyout menu, string where, List<string> failures)
    {
        var origin = Origin(menu);
        var panel = menu.PanelOnScreen;
        var width = panel.right - panel.left;
        var looks = menu.Looks.Where(look => look.Plate.W > 0 && look.Plate.H > 0 && look.Id != Flyout.InlineBackId).ToList();
        var listed = looks.Where(look => look.Plate.W > width / 2f).OrderBy(look => look.Plate.Y).ToList();
        foreach (var look in looks)
        {
            var plate = look.Plate;
            // Not one of the rows on the screen now (scrolled away, or under what stands still).
            if (plate.Y < 0 || plate.Y + plate.H > panel.bottom - panel.top)
            {
                continue;
            }
            var across = listed.Contains(look);
            var at = listed.IndexOf(look);
            // A list row's slot: the menu's width, and down to halfway to the rows over and under it,
            // where they are near (a rule or a heading between two keeps its own band).
            var top = plate.Y;
            var bottom = plate.Y + plate.H;
            if (across && at > 0 && plate.Y - (listed[at - 1].Plate.Y + listed[at - 1].Plate.H) is var over and > 0 and <= 8 * Scale)
            {
                top -= over / 2 - 1;
            }
            if (across && at + 1 < listed.Count && listed[at + 1].Plate.Y - bottom is var under and > 0 and <= 8 * Scale)
            {
                bottom += under / 2 - 1;
            }
            var left = across ? 1 : plate.X;
            var right = across ? width - 1 : plate.X + plate.W;
            var missed = 0;
            (int X, int Y, uint? Lit) first = default;
            for (var y = (int)MathF.Ceiling(top); y < bottom; y += Step)
            {
                for (var x = (int)MathF.Ceiling(left); x < right; x += Step)
                {
                    if (LitAt(menu, origin, x, y) is var lit && lit != look.Id)
                    {
                        if (missed++ == 0)
                        {
                            first = (x, y, lit);
                        }
                    }
                }
            }
            if (missed > 0)
            {
                failures.Add($"{where}: row {look.Id} ({menu.ShownItems.Concat(menu.RowsShown).FirstOrDefault(item => item.Id == look.Id)?.Text}) plate {plate} lights something else at {missed} points, first ({first.X}, {first.Y}) lit {first.Lit?.ToString() ?? "nothing"}");
            }
        }
    }

    /// <summary>
    /// Every row of every menu, its own rows and each level one deep: the whole slot of each lights it; the
    /// rows at its foot too, the menu scrolled as far as it goes as well.
    /// </summary>
    [UiFact]
    public void EveryRowLightsOverItsWholeSlot()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("menu-hit-rows", (gfx, instance) =>
        {
            foreach (var (name, items) in Menus())
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
                menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
                WindowShots.Settle(50);
                SweepRows(menu, $"{name}", failures);
                // Scrolled to its end: the foot still its own, the rows over it theirs.
                _ = menu.Message(WM.WM_MOUSEWHEEL, unchecked((nuint)(uint)(-120 * 20) << 16), At(Origin(menu).x + 20, Origin(menu).y + 20));
                SweepRows(menu, $"{name} scrolled", failures);
                foreach (var row in items.Where(item => item.Children is { Count: > 0 } && item.Enabled))
                {
                    Assert.Equal(default, menu.Click(row.Id));
                    SweepRows(menu, $"{name} › {row.Text}", failures);
                    Assert.Equal(default, menu.Click(Flyout.InlineBackId));
                }
                menu.Hide();
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(40)) + $"{Environment.NewLine}({failures.Count} in all)");
    }

    /// <summary>
    /// A level's head, from the panel's top to its rule and edge to edge: the pointer lights it everywhere,
    /// and one press and release anywhere on it go back — no hover first.
    /// </summary>
    [UiFact]
    public void TheHeadGoesBackFromAnywhereOnIt()
    {
        var failures = new List<string>();
        WindowShots.OnHiddenDesktop("menu-hit-head", (gfx, instance) =>
        {
            foreach (var (name, items) in Menus().Take(3))
            {
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
                menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
                WindowShots.Settle(50);
                var row = items.First(item => item.Children is { Count: > 0 } && item.Enabled);
                Assert.Equal(default, menu.Click(row.Id));
                var origin = Origin(menu);
                var panel = menu.PanelOnScreen;
                var width = panel.right - panel.left;
                var looks = menu.Looks.Where(look => look.Plate.W > 0).OrderBy(look => look.Plate.Y).ToList();
                var head = looks.Single(look => look.Id == Flyout.InlineBackId).Plate;
                // Down to the rule under it: the first row's plate under the head, less its own gap.
                var next = looks.First(look => look.Plate.Y > head.Y + head.H).Plate;
                var band = (int)MathF.Floor(head.Y + head.H + ((next.Y - head.Y - head.H) / 3));
                var missed = new List<(int X, int Y)>();
                for (var y = 0; y < band; y += Step)
                {
                    for (var x = 0; x < width; x += Step)
                    {
                        if (LitAt(menu, origin, x, y) != Flyout.InlineBackId)
                        {
                            missed.Add((x, y));
                        }
                    }
                }
                if (missed.Count > 0)
                {
                    failures.Add($"{name} › {row.Text}: the head (plate {head}, band 0..{band}) lights something else at {missed.Count} points, e.g. {string.Join(" ", missed.Take(6))}");
                }
                // One press and release at its corners and its middle go back, the pointer never there before.
                foreach (var (x, y) in new[] { (1, 1), (width - 2, 1), (1, band - 1), (width - 2, band - 1), (width / 2, band / 2) })
                {
                    if (menu.InlineDepth == 0)
                    {
                        Assert.Equal(default, menu.Click(row.Id));
                    }
                    _ = menu.Message(WM.WM_MOUSELEAVE, 0, 0);
                    _ = menu.Message(WM.WM_LBUTTONDOWN, 1, At(origin.x + x, origin.y + y));
                    _ = menu.Message(WM.WM_LBUTTONUP, 0, At(origin.x + x, origin.y + y));
                    if (menu.InlineDepth != 0)
                    {
                        failures.Add($"{name} › {row.Text}: a click at ({x}, {y}) on the head did not go back");
                    }
                }
                menu.Hide();
            }
        });
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(40)) + $"{Environment.NewLine}({failures.Count} in all)");
    }

    /// <summary>
    /// A menu's tooltip — an icon button's name, a level head's "Back" — takes no press: where it stands over
    /// the menu's own rows (the buttons' over the rows beside them, the head's over the first rows where the
    /// menu reaches the work area's top), the pointer there is on the menu, so the first click is the row's
    /// and not the tooltip's (the person: "sometimes you need to click twice until your input it taken").
    /// Asked of Windows from the menu's own thread, as its input is.
    /// </summary>
    [UiFact]
    public void AMenusTooltipTakesNoPress()
    {
        var failures = new List<string>();
        var over = 0;
        WindowShots.OnHiddenDesktop("menu-hit-tips", (gfx, instance) =>
        {
            var (_, items) = Menus().First();
            foreach (var edge in new[] { Edge.Bottom, Edge.Top })
            {
                var anchor = edge == Edge.Bottom ? Anchor : new POINT { x = Work.right / 3, y = Work.top };
                using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
                menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, anchor, Work, edge, null);
                WindowShots.Settle(50);
                void Check(string what)
                {
                    menu.TipDue();
                    WindowShots.Settle(50);
                    var tip = menu.TipHwnd;
                    RECT box;
                    if (menu.TipShown is null || tip == HWND.NULL || GetWindowRgnBox(tip, &box) == 0)
                    {
                        failures.Add($"{edge} {what}: no tooltip");
                        return;
                    }
                    var frame = default(RECT);
                    _ = GetWindowRect(tip, &frame);
                    var panel = menu.PanelOnScreen;
                    var taken = 0;
                    for (var y = Math.Max(panel.top, frame.top + box.top); y < Math.Min(panel.bottom, frame.top + box.bottom); y += 2)
                    {
                        for (var x = Math.Max(panel.left, frame.left + box.left); x < Math.Min(panel.right, frame.left + box.right); x += 2)
                        {
                            over++;
                            if (GetAncestor(WindowFromPoint(new POINT { x = x, y = y }), GA.GA_ROOT) != menu.Hwnd)
                            {
                                taken++;
                            }
                        }
                    }
                    if (taken > 0)
                    {
                        failures.Add($"{edge} {what}: the tooltip \"{menu.TipShown}\" took the pointer at {taken} points over the menu");
                    }
                }
                foreach (var button in menu.ShownItems.Where(item => item.Primary && item.Enabled))
                {
                    menu.PointTo(button.Id);
                    Check(button.Text);
                }
                var row = items.First(item => item.Children is { Count: > 0 } && item.Enabled);
                Assert.Equal(default, menu.Click(row.Id));
                menu.PointTo(Flyout.InlineBackId);
                Check($"{row.Text}'s head");
                menu.Hide();
            }
        });
        Assert.True(over > 0, "no tooltip stood over its menu: the sweep asked nothing");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(40)) + $"{Environment.NewLine}({failures.Count} in all)");
    }
}
