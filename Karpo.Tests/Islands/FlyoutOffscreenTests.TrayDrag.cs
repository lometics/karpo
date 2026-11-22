// tray: an icon dragged out of the overflow onto a bottom bar and back, through the real message
// path: the overflow flyout shown on a hidden desktop, its icon pressed, moved and let go with the
// mouse messages the flyout window gets, the screen point the flyout tells the dock read against a
// real bar layout as the dock reads it (TrayDrags.OnLayout, Dock.TrayTargetAt), and the drop written
// into a config as the dock writes it (TrayLayout.Apply). And the overflow as large as its icons.
//
// The person: "dragging tray icons between the bar and the overflow still doesn't work". The drop
// was read across the bar from the top of the bar's window, which keeps room for tooltips over a
// bottom bar: the bar itself was never under the pointer (TrayDropOnBarTests).

using Karpo.Dock;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Islands;

public sealed unsafe partial class FlyoutOffscreenTests
{
    /// <summary>
    /// Out of the overflow onto the bar's tray, between its first and second icon: the drop is the
    /// bar's, the icon goes on the taskbar there; then from the bar onto the chevron, and into the open
    /// overflow at a place of its own: the icon is in the hidden icon menu again, where it was let go.
    /// </summary>
    [UiFact]
    public void AnIconIsDraggedOutOfTheOverflowOntoABottomBarAndBack()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            const uint dpi = 96;
            var config = new Config { Height = 48, Style = BarStyle.Islands };
            var area = new RECT { left = 0, top = 0, right = 1920, bottom = 1080 };
            var bar = Layout.ForTests(dpi, area, Edge.Bottom, config,
                [new(Config.IslandKey(null, "battery"), BarKind.EnergySaver), new(Config.IslandKey(null, "tray"), BarKind.Tray)],
                Layout.CellPx(dpi, config), tray: 3, trayOverflow: true);
            // The bar's window is deeper than the bar: what the drag tripped over.
            Assert.True(bar.Height > bar.Thickness);

            // What the tray holds: three icons on the bar, two in the overflow.
            List<TrayEntry> entries =
            [
                new("a|1", false), new("b|1", false), new("c|1", false),
                new("hidden1|1", true), new("hidden2|1", true),
            ];
            var settings = new TrayIconOptions();
            var (shown, hidden) = TrayLayout.Split(settings, entries);
            Assert.Equal(3, shown.Count);
            Assert.Equal(2, hidden.Count);

            // The overflow as the dock opens it over the chevron.
            var chevron = bar.ItemRect(Hit.TrayOverflow);
            var work = area with { bottom = area.bottom - (int)bar.Thickness };
            using var flyout = new Flyout(gfx, instance, 0)
            {
                StripIconSize = bar.TrayIconSize,
                StripCell = bar.TrayOverflowCell,
                GridColumns = Layout.TrayOverflowColumns,
                FitsItsIcons = true,
                GroupContent = true,
                ContextClicks = true,
                DragItems = true,
                TellsPointer = true,
            };
            FlyoutItem[] icons =
            [
                new(2000, "Hidden 1", new FlyoutIcon.Picture(Solid((int)bar.TrayIconSize, 0xFF0F6CBD))),
                new(2001, "Hidden 2", new FlyoutIcon.Picture(Solid((int)bar.TrayIconSize, 0xFFE3008C))),
            ];
            var anchor = new POINT { x = bar.X + (int)(chevron.X + chevron.Width / 2f), y = work.bottom };
            flyout.Show(gfx, LookFor(true), dpi / 96f, icons, FlyoutLayout.Grid, anchor, work, Karpo.Core.Edge.Bottom);
            Assert.True(flyout.IsOpen);

            // A point on the screen as the flyout's window hears it.
            nint Client(POINT screen)
            {
                var at = screen;
                Assert.True(ScreenToClient(flyout.Hwnd, &at));
                return Point(at.x, at.y);
            }

            // Down on the first hidden icon, and out onto the bar: the middle of its second icon.
            var held = Centre(flyout, 2000);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Press, 2000), flyout.Message(WM.WM_LBUTTONDOWN, 1, held));
            var second = bar.ItemRect(Hit.Tray(1));
            var onBar = new POINT { x = bar.X + (int)(second.X + second.Width / 2f), y = bar.Y + (int)(second.Y + second.Height / 2f) };
            var moved = flyout.Message(WM.WM_MOUSEMOVE, 1, Client(onBar));
            Assert.Equal((FlyoutEventKind.DragMove, 2000u, onBar.x, onBar.y), (moved.Kind, moved.Id, moved.At.x, moved.At.y));
            // Not the overflow's, which a drop there would be checked against first.
            Assert.False(flyout.OnPanel(moved.At));
            var aimed = Assert.IsType<TrayDrop>(TrayDrags.OnLayout(bar, moved.At, shown.Count, hidden.Count));
            Assert.Equal(new TrayDrop(TrayArea.Shown, 1), aimed);
            // The tray grows by the cell it brings, laid out again as the dock lays it out mid-drag
            // (Dock.AimTrayDrag), and reads the same pointer the same: the gap doesn't run off.
            var trayKey = Config.IslandKey(null, "tray");
            var more = TrayDrags.MoreFor(aimed, aimedHere: true, draws: false);
            Assert.Equal(1, more);
            var grown = Layout.ForTests(dpi, area, Edge.Bottom, config,
                [new(Config.IslandKey(null, "battery"), BarKind.EnergySaver), new(trayKey, BarKind.Tray)],
                Layout.CellPx(dpi, config), tray: 3, trayOverflow: true, trayMore: new Dictionary<string, int> { [trayKey] = more });
            Assert.Equal(bar.PartAt(1).Width + bar.TrayStep, grown.PartAt(1).Width, 0.5f);
            Assert.Equal(aimed, TrayDrags.OnLayout(grown, moved.At, shown.Count, hidden.Count));

            var dropped = flyout.Message(WM.WM_LBUTTONUP, 0, Client(onBar));
            Assert.Equal((FlyoutEventKind.DragDrop, 2000u), (dropped.Kind, dropped.Id));
            var landed = Assert.IsType<TrayDrop>(TrayDrags.OnLayout(bar, dropped.At, shown.Count, hidden.Count));
            Assert.True(TrayLayout.Apply(settings, entries, "hidden1|1", landed));
            Assert.Equal(TrayState.Taskbar, TrayLayout.State(settings, entries[3]));
            (shown, hidden) = TrayLayout.Split(settings, entries);
            Assert.Equal(["a|1", "hidden1|1", "b|1", "c|1"], shown.Select(entry => entry.Identity));
            Assert.Equal(["hidden2|1"], hidden.Select(entry => entry.Identity));

            // And back: from its place on the bar onto the chevron, which hides it at the end.
            var onChevron = new POINT { x = bar.X + (int)(chevron.X + chevron.Width / 2f), y = bar.Y + (int)(chevron.Y + chevron.Height / 2f) };
            var back = Assert.IsType<TrayDrop>(TrayDrags.OnLayout(bar, onChevron, shown.Count, hidden.Count, heldAt: 1));
            Assert.Equal(new TrayDrop(TrayArea.Overflow, 1), back);
            Assert.True(TrayLayout.Apply(settings, entries, "hidden1|1", back));
            Assert.Equal(TrayState.Overflow, TrayLayout.State(settings, entries[3]));
            (shown, hidden) = TrayLayout.Split(settings, entries);
            Assert.Equal(["a|1", "b|1", "c|1"], shown.Select(entry => entry.Identity));
            Assert.Equal(["hidden2|1", "hidden1|1"], hidden.Select(entry => entry.Identity));

            // Or into the open overflow, before its first icon: the overflow's place, not the bar's.
            var first = Assert.NotNull(flyout.ItemOnScreen(2000));
            var inOverflow = new POINT { x = first.left + 1, y = (first.top + first.bottom) / 2 };
            Assert.True(flyout.OnPanel(inOverflow));
            Assert.Null(TrayDrags.OnLayout(bar, inOverflow, shown.Count, hidden.Count));
            var into = TrayDrags.InOverflow(flyout.DropIndex(inOverflow), hidden.Count);
            Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), into);
            Assert.True(TrayLayout.Apply(settings, entries, "hidden1|1", into));
            Assert.Equal(["hidden1|1", "hidden2|1"], TrayLayout.Split(settings, entries).Overflow.Select(entry => entry.Identity));

            flyout.Hide();
            Pump();
        });
    }

    /// <summary>
    /// tray: the overflow is as large as its icons' squares and the room around them, and no larger,
    /// for one icon, a full row and two rows: Windows 11's overflow fits its icons. Laid out as any
    /// grid it was at least 120 DIP wide, twice and more one icon's square.
    /// <para>
    /// Opened by a drag from the bar it has a square more than its icons (<see cref="Flyout.SpareTiles"/>),
    /// so the place after the last is inside it, and with none at all it is that one empty square: the
    /// drop there is the overflow's first place (§172). Empty, it showed no panel at all.
    /// </para>
    /// </summary>
    [UiFact]
    public void TheOverflowFitsItsIcons()
    {
        OnStaThread(desktop: true, gfx =>
        {
            var instance = (HINSTANCE)(void*)GetModuleHandleW(null);
            Register(instance);
            foreach (var scale in new[] { 1f, 1.5f })
            {
                var cell = MathF.Round(44 * scale);
                foreach (var (count, spare) in new[] { (1, 0), (2, 0), (5, 0), (7, 0), (0, 1), (1, 1), (4, 1), (5, 1) })
                {
                    using var flyout = new Flyout(gfx, instance, 0)
                    {
                        StripIconSize = MathF.Round(24 * scale),
                        StripCell = cell,
                        GridColumns = Layout.TrayOverflowColumns,
                        FitsItsIcons = true,
                        GroupContent = true,
                        DragItems = true,
                        TellsPointer = true,
                        SpareTiles = spare,
                    };
                    var items = Enumerable.Range(0, count)
                        .Select(index => new FlyoutItem(2000u + (uint)index, $"Icon {index}", new FlyoutIcon.Picture(Solid((int)MathF.Round(24 * scale), 0xFF0F6CBD))))
                        .ToArray();
                    var work = new RECT { left = 0, top = 0, right = (int)(1920 * scale), bottom = (int)(1032 * scale) };
                    flyout.Show(gfx, LookFor(true), scale, items, FlyoutLayout.Grid, new POINT { x = work.right - 200, y = work.bottom }, work, Karpo.Core.Edge.Bottom);
                    Assert.True(flyout.IsOpen);

                    var tiles = count + spare;
                    var columns = Math.Min(tiles, Layout.TrayOverflowColumns);
                    var rows = (tiles + Layout.TrayOverflowColumns - 1) / Layout.TrayOverflowColumns;
                    var size = Flyout.FittedSize(columns, rows, cell, scale);
                    Assert.Equal(size, flyout.PanelSize);
                    // The room around the squares: the same on every side, and small.
                    var pad = MathF.Round(Flyout.FittedPadDip * scale);
                    Assert.Equal(columns * cell + 2 * pad, size.X);
                    Assert.Equal(rows * cell + 2 * pad, size.Y);
                    if (count == 0)
                    {
                        // The empty square takes the drop: first among the hidden icons.
                        var panel = flyout.PanelsOnScreen.Panel;
                        var middle = new POINT { x = (panel.left + panel.right) / 2, y = (panel.top + panel.bottom) / 2 };
                        Assert.True(flyout.OnPanel(middle));
                        Assert.Equal(new TrayDrop(TrayArea.Overflow, 0), TrayDrags.InOverflow(flyout.DropIndex(middle), count));
                    }
                    flyout.Hide();
                    Pump();
                }
            }
        });
    }
}
