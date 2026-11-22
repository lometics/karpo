// §521: where a popup of the settings panel stands (PopupPlace, Karpo.Dock\FlyoutHost.cs): as WinUI's ComboBox hangs its
// list — directly under its control, flush with its left edge, over it where there is no room under it — whichever way
// the panel opened, wherever the page is scrolled, inside an expander or not, in the screen px of the control's display.
// The person, 2026-11-07: "the dropdowns also appear at the wrong location." The real windows: PanelPopupPlaceTests.

using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Edit;

public sealed class PopupPlaceTests
{
    /// <summary>A 2560 × 1440 display at 150 %, its bar 72 px along its foot.</summary>
    private static readonly RECT Work = new() { left = -2560, top = 0, right = 0, bottom = 1368 };

    private const int Gap = 6;
    private const int Keep = 18;

    private static RECT Box(int left, int top, int width = 360, int height = 48) => new() { left = left, top = top, right = left + width, bottom = top + height };

    /// <summary>Room under it: directly under it, its gap off it, flush with its left; never centred on it.</summary>
    [Fact]
    public void UnderItsControlFlushWithItsLeft()
    {
        var control = Box(-1118, 450);
        var (rect, above) = PopupPlace.Of(control, 480, 600, Work, Gap, Keep);
        Assert.False(above);
        Assert.Equal((control.left, control.bottom + Gap, control.left + 480, control.bottom + Gap + 600), (rect.left, rect.top, rect.right, rect.bottom));
    }

    /// <summary>No room under it and room over it: over it, its gap off it, flush with its left.</summary>
    [Fact]
    public void OverItsControlWithNoRoomUnderIt()
    {
        var control = Box(-1118, 1000);
        var (rect, above) = PopupPlace.Of(control, 360, 600, Work, Gap, Keep);
        Assert.True(above);
        Assert.Equal((control.left, control.top - Gap - 600, control.top - Gap), (rect.left, rect.top, rect.bottom));
    }

    /// <summary>
    /// Room on neither side: the roomier, the popup no taller than that side's room (what it shows scrolls in it), directly
    /// under or over its control — §559: never pushed back over it (a colour's picker low on a display covered its button).
    /// </summary>
    [Fact]
    public void RoomOnNeitherSideTakesTheRoomierAndNeverCoversItsControl()
    {
        var high = Box(-1118, 300);
        var (down, overHigh) = PopupPlace.Of(high, 360, 1200, Work, Gap, Keep);
        Assert.False(overHigh);
        Assert.Equal((high.bottom + Gap, Work.bottom - Keep), (down.top, down.bottom));
        Assert.Equal(PopupPlace.Below(high, Work, Gap, Keep), PopupPlace.Room(high, Work, Gap, Keep));
        var low = Box(-1118, 1000);
        var (up, overLow) = PopupPlace.Of(low, 360, 1200, Work, Gap, Keep);
        Assert.True(overLow);
        Assert.Equal((Work.top + Keep, low.top - Gap), (up.top, up.bottom));
        Assert.Equal(PopupPlace.Above(low, Work, Gap, Keep), PopupPlace.Room(low, Work, Gap, Keep));
        // Neither side's room enough, wherever the control stands: the popup off it, on the work area.
        for (var top = 0; top <= Work.bottom - 48; top += 37)
        {
            var control = Box(-1118, top);
            var (rect, _) = PopupPlace.Of(control, 360, 1300, Work, Gap, Keep);
            Assert.True(rect.bottom <= control.top - Gap || rect.top >= control.bottom + Gap, $"a popup {rect.top}..{rect.bottom} covers its control at {control.top}..{control.bottom}");
            Assert.True(rect.top >= Work.top + Keep && rect.bottom <= Work.bottom - Keep, $"a popup {rect.top}..{rect.bottom} off the work area");
            Assert.Equal(PopupPlace.Room(control, Work, Gap, Keep), rect.bottom - rect.top);
        }
    }

    /// <summary>
    /// §578 (the person, 2026-11-09: "the flyouts should be centered under the button"): an edit bar entry's flyout centred
    /// on its entry, under it or over it as for any control; what the entry stands in (the bar) no bound; pushed in only at
    /// the work area's sides, its margin kept; the same through a placement (FlyoutPlacement.Beak).
    /// </summary>
    [Fact]
    public void AnEntrysFlyoutIsCentredOnIt()
    {
        var entry = Box(-1500, 20, 120, 48);
        var bar = new RECT { left = -1600, top = 12, right = -1200, bottom = 76 };
        var (rect, above) = PopupPlace.Of(entry, 870, 600, Work, Gap, Keep, bar, centred: true);
        Assert.False(above);
        Assert.Equal(((entry.left + entry.right) / 2, entry.bottom + Gap), ((rect.left + rect.right) / 2, rect.top));
        // Odd widths: within a px of its middle.
        var oddBox = Box(-1501, 20, 121, 48);
        var odd = PopupPlace.Of(oddBox, 871, 600, Work, Gap, Keep, centred: true).Rect;
        Assert.InRange((odd.left + odd.right) - (oddBox.left + oddBox.right), -2, 2);
        // Low on the display: over it, centred all the same.
        var low = Box(-1500, 1300, 120, 48);
        var up = PopupPlace.Of(low, 870, 600, Work, Gap, Keep, centred: true);
        Assert.True(up.Above);
        Assert.Equal((low.left + low.right) / 2, (up.Rect.left + up.Rect.right) / 2);
        // At the work area's sides: pushed in, its margin kept.
        Assert.Equal(Work.right - Keep - 870, PopupPlace.Of(Box(-200, 20, 120, 48), 870, 600, Work, Gap, Keep, centred: true).Rect.left);
        Assert.Equal(Work.left + Keep, PopupPlace.Of(Box(-2540, 20, 120, 48), 870, 600, Work, Gap, Keep, centred: true).Rect.left);
        // Through a placement, as the edit bar hangs it.
        var placement = new FlyoutPlacement(new POINT { x = entry.left, y = entry.bottom }, Work, Karpo.Core.Edge.Top, GapDip: 4, Control: entry, Beak: true);
        Assert.Equal(rect.left, placement.PanelAt(870, 600, 1.5f).left);
    }

    /// <summary>
    /// §584 (the person, 2026-11-09: "the topbar flyouts should have a little distance to the topbar and also an angle that
    /// points to the button"): the beak's tip over its entry's middle — in the panel's middle where it is centred on it, and
    /// still over the entry where the panel was pushed in at the work area's side, either side —; kept off the rounded corners
    /// (the corner and its half foot from an end) where the entry stands past them; the side's middle on a panel too narrow.
    /// </summary>
    [Fact]
    public void TheBeaksTipIsOverItsEntrysMiddle()
    {
        const float corner = 12, tall = 12;
        var entry = Box(-1500, 20, 120, 48);
        var centred = PopupPlace.Of(entry, 870, 600, Work, Gap, Keep, centred: true).Rect;
        Assert.Equal(435, Beak.TipAt(entry, centred, corner, tall));
        // Pushed in at the work area's right and at its left: the tip moves along the panel, over the entry still.
        foreach (var near in new[] { Box(-300, 20, 120, 48), Box(-2500, 20, 120, 48) })
        {
            var pushed = PopupPlace.Of(near, 870, 600, Work, Gap, Keep, centred: true).Rect;
            Assert.NotEqual((near.left + near.right) / 2, (pushed.left + pushed.right) / 2);
            Assert.Equal((near.left + near.right) / 2f, pushed.left + Beak.TipAt(near, pushed, corner, tall));
        }
        // An entry past the corner: the tip where the straight of the side ends, its foot whole on it.
        var panel = new RECT { left = 0, top = 100, right = 400, bottom = 500 };
        Assert.Equal(corner + tall, Beak.TipAt(Box(-50, 0, 40, 40), panel, corner, tall));
        Assert.Equal(400 - corner - tall, Beak.TipAt(Box(420, 0, 40, 40), panel, corner, tall));
        // Too narrow for the corners and the foot: its middle.
        Assert.Equal(15, Beak.TipAt(Box(0, 0, 4, 4), new RECT { left = 0, right = 30 }, corner, tall));
    }

    /// <summary>
    /// §584: the beak's three points, in the order its outline runs: out of the top, its tip <c>tall</c> over it, its foot
    /// <c>tall</c> either side of the tip, left to right; out of the foot (a panel over its entry, an edit bar at the display's
    /// foot) flipped, right to left, its tip <c>tall</c> under it. Inset (the rim's middle), the side as far in and the tip
    /// √2 as far, the sides as steep (45°).
    /// </summary>
    [Fact]
    public void TheBeakPointsUpFromTheTopAndDownFromTheFoot()
    {
        var (from, tip, to) = Beak.Triangle(600, Karpo.Core.Edge.Top, 200, 12, 0);
        Assert.Equal((new System.Numerics.Vector2(188, 0), new System.Numerics.Vector2(200, -12), new System.Numerics.Vector2(212, 0)), (from, tip, to));
        var (up, down, back) = Beak.Triangle(600, Karpo.Core.Edge.Bottom, 200, 12, 0);
        Assert.Equal((new System.Numerics.Vector2(212, 600), new System.Numerics.Vector2(200, 612), new System.Numerics.Vector2(188, 600)), (up, down, back));
        foreach (var side in new[] { Karpo.Core.Edge.Top, Karpo.Core.Edge.Bottom })
        {
            var (a, b, c) = Beak.Triangle(600, side, 200, 12, 1);
            Assert.Equal(side == Karpo.Core.Edge.Top ? 1 : 599, a.Y, 3);
            Assert.Equal(a.Y, c.Y);
            Assert.Equal(side == Karpo.Core.Edge.Top ? -12 + MathF.Sqrt(2) : 612 - MathF.Sqrt(2), b.Y, 3);
            Assert.Equal(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y), 3);
            Assert.Equal(200, (a.X + c.X) / 2, 3);
        }
    }

    /// <summary>Near the work area's right: pushed in, its margin kept; never past its left either.</summary>
    [Fact]
    public void PushedInAtTheWorkAreasSides()
    {
        var right = Box(-300, 450);
        Assert.Equal(Work.right - Keep - 480, PopupPlace.Of(right, 480, 200, Work, Gap, Keep).Rect.left);
        var left = Box(-2560, 450);
        Assert.Equal(Work.left + Keep, PopupPlace.Of(left, 480, 200, Work, Gap, Keep).Rect.left);
    }

    /// <summary>
    /// §531: in what its control stands in (the open panel, the folded edit bar): flush with its control's left where that
    /// keeps it inside, else flush with its right — "+ Add"'s list at the edit bar's end, an Add button's at a card's —; one
    /// wider than that either way flush with its left, pushed in where it would pass the work area.
    /// </summary>
    [Fact]
    public void FlushWithItsRightWhereItsLeftWouldPassWhatItStandsIn()
    {
        var panel = new RECT { left = -1850, top = 12, right = -710, bottom = 1020 };
        var add = Box(-961, 24, 111, 48);
        var (list, _) = PopupPlace.Of(add, 726, 876, Work, Gap, Keep, panel);
        Assert.Equal((add.right - 726, add.bottom + Gap, add.right), (list.left, list.top, list.right));
        var fits = Box(-1800, 300);
        Assert.Equal(fits.left, PopupPlace.Of(fits, 480, 200, Work, Gap, Keep, panel).Rect.left);
        Assert.Equal(Work.right - Keep - 1400, PopupPlace.Of(add, 1400, 200, Work, Gap, Keep, panel).Rect.left);
        // No panel: the work area's side alone.
        Assert.Equal(add.left, PopupPlace.Of(add, 726, 876, Work, Gap, Keep).Rect.left);
    }

    /// <summary>
    /// The panel opening up from an edit bar at the display's foot, or down from one at its top: the popup follows its
    /// control alone — under it where there is room, whichever way the panel opened.
    /// </summary>
    [Fact]
    public void WhicheverWayThePanelOpened()
    {
        // The edit bar at the display's foot: the panel over it, a dropdown high in it has room under it.
        var upHigh = Box(-1118, 200);
        Assert.False(PopupPlace.Of(upHigh, 360, 700, Work, Gap, Keep).Above);
        // Low in it, near the edit bar: over it.
        var upLow = Box(-1118, 1200);
        Assert.True(PopupPlace.Of(upLow, 360, 700, Work, Gap, Keep).Above);
    }

    /// <summary>
    /// In a placement (FlyoutPlacement.Control): a control's popup at its control whatever its anchor, edge and alignment
    /// say; its room the roomier side's; Roomiest never flips it; at 100 % on a second display, in that display's px.
    /// </summary>
    [Fact]
    public void APlacementOfAControlIsItsControls()
    {
        var control = Box(-1118, 450);
        var placement = new FlyoutPlacement(new POINT { x = -938, y = 0 }, Work, Karpo.Core.Edge.Bottom, GapDip: 4, Align: FlyoutAlign.Center, Control: control);
        var rect = placement.PanelAt(480, 600, 1.5f);
        Assert.Equal((control.left, control.bottom + 6), (rect.left, rect.top));
        Assert.Equal(PopupPlace.Room(control, Work, 6, 18), (int)placement.Room(1.5f));
        Assert.Equal(placement, placement.Roomiest(5000, 100, 1.5f));
        // A second display at 100 %: its own px, 4 of air, 12 of margin.
        var second = new RECT { left = 0, top = 0, right = 1920, bottom = 1032 };
        var there = Box(1068, 300, 240, 32);
        var on = new FlyoutPlacement(default, second, Karpo.Core.Edge.Top, GapDip: 4, Control: there).PanelAt(320, 400, 1f);
        Assert.Equal((1068, 336), (on.left, on.top));
    }

    /// <summary>
    /// The control's rect the popup is placed by, as the panel lays it out (PanelLayout.RectOf, EditDeck.OnScreen): scrolled
    /// with the page, and inside an expander's inset rows as at a card's head — the control column's left.
    /// </summary>
    [Fact]
    public void TheControlsRectFollowsTheScrollAndTheExpanders()
    {
        var content = SettingsPanelTests.Content();
        PanelLayout Laid(float scroll, IReadOnlySet<string>? open = null) =>
            new(new SettingsPanelTests.Words(), 1.5f, new PanelInput(0, content, PanelPage.Appearance, open ?? new HashSet<string>(), scroll), PanelLayout.SizeOf(1.5f, 1368));
        // §592: the Taskbar widgets tab chosen, its Font under the tabs.
        var tab = PanelPage.Appearance + "/" + PanelTabs.Mark + LookParts.Name(LookPart.Widgets);
        var open = new HashSet<string> { tab };
        var font = Laid(0, open).LineOf(tab + "/" + S.SettingsAppearanceStylesFont) ?? throw new InvalidOperationException("no Font row");
        // §670: its picker's head, the whole line.
        var hit = Laid(0, open).Controls(font).First(each => each.Hit.Key == font.Row.Key).Hit;
        var still = Laid(0, open).RectOf(hit);
        var scrolled = Laid(120, open).RectOf(hit);
        Assert.Equal(still.Y - 120, scrolled.Y);
        Assert.Equal(still.X, scrolled.X);
        // Opened, its card's body under its head, following them both.
        open.Add(font.Row.Key);
        var body = Laid(0, open).LineOf(font.Row.Key + "/" + PanelPickers.BodyKey)!;
        Assert.Equal(Laid(120, open).LineOf(body.Row.Key)!.Rect.Y, body.Rect.Y);
        Assert.Equal(Laid(0, open).OnPanel(body.Rect).Y - 120, Laid(120, open).OnPanel(body.Rect).Y);
    }
}
