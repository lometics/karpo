// menus: the row of icon buttons a Windows 11 context menu has (the person, 2026-10-11: "when right
// clicking a widget it should show the remove, settings, and duplicate entries in the context menu as
// well, but they should be shown in a row like win11 does").
//
// Explorer's menu has Cut, Copy, Rename, Share and Delete as icons in a row of their own, ruled off
// from the rows of text. Here the row is always the menu's top, whichever way the menu opens (the
// person, 2026-10-11: "the edit row should be on top of the context menu action"): it stood at the
// end nearest the pointer for a day, the bottom of a bottom bar's menus, and moved with the bar's
// edge. The items say only that they are of the row (FlyoutItem.Primary); the menu puts them first
// (WithPrimaryRow), in their order: a widget's Remove last, as Explorer's Delete.
//
// It is drawn as Explorer's (2026-10-16, the person: "right now the top row does not match the
// design"; docs\widgets.md §178), a hairline between two buttons and the rule under the row the menu's
// full width — icons alone (§214, 2026-10-19; §481's names under them gone again 2026-11-03): a square
// button each, the name in its tooltip and its spoken name. A row of few buttons doesn't stretch them across a wider menu: each keeps its
// own width (§186), and they stand together at the row's far end, a hairline between each two, its far
// left in a mirrored menu (§350, the person, 2026-10-25: "the top edit row in the context menu should
// show all of the icons right aligned. now some of the icons are shown in the middle somewhere"). One
// place for them in every menu (PrimarySpans): §220 and §323 stood Remove 12 DIP apart from Cut and
// Copy, which put those two in the row's middle, and a row with no type packed them from its start.
//
// Each button is a row of the menu in all but its drawing: lit under the pointer and pressed as a
// row is, chosen by a click, Enter or Space, greyed and passed over where it can't act. Left and
// Right walk the row, Down leaves it for the rows of text and Up from them comes back onto its first
// button, as WinUI's CommandBarFlyout goes. Its tooltip is its name and its shortcut, as Explorer's
// ("Copy (Ctrl+C)") — after the pointer or the keys have rested on it a moment, at once from one
// button to the next, as along a toolbar — and a screen reader says it is a button.
//
// edit: a configuration menu says what it is of at the row's start (§323, the person, 2026-10-23:
// "actually move the title into the toprow inside the menu again"; §314 stood it on a plate over the
// menu): its type alone (Flyout.TypeTitle — IslandEdits.Kind's "Clock", "Weather", "Folder"; BarLooks.Kind's
// "Taskbar · Work", "Layout · Work", §364), in a flyout title's 14 DIP semibold, where Windows' flyouts have theirs, 16 DIP in, upright
// in the row's middle, cut with an ellipsis short of the buttons, which stand at the row's far end as in
// every menu. A menu with no buttons (the bar's looks) has the row for its type alone, ruled off the
// same. On its own rows only: a level shown in place has its head ("← Height"), which names it. Nothing
// to light, press or walk to; Narrator names the menu by it.

using System.Numerics;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

public sealed unsafe partial class Flyout
{
    // menus: the row as icons alone (2026-10-19, the person: "the settings row should only have icons not
    // descriptions of the icons"), as Explorer's command bar was before its words came, and WinUI's
    // CommandBarFlyout's compact buttons are: each a 40 DIP square, its 16 DIP icon in the middle, a
    // hairline 20 DIP tall between two and 1 DIP at the row's ends. The plate under the pointer is 4
    // DIP in from the button's every side, its corners 4 DIP. The name and the shortcut are the tooltip's
    // (the person, 2026-11-03: "remove the text under the menu row icons again, it should also not be configurable").

    /// <summary>menus: a button of the row, square: its width, and the row's height.</summary>
    internal const float PrimaryButtonDip = 40;

    /// <summary>menus: the row's room at its two ends, before the first button and after the last.</summary>
    internal const float PrimaryEdgeDip = 1;

    /// <summary>menus: how tall the hairline between two buttons is, in the row's middle.</summary>
    internal const float PrimaryRuleDip = 20;

    /// <summary>menus: a button's plate, in from its every side.</summary>
    internal const float PrimaryPlateDip = 4;

    /// <summary>edit: the least room between a type at the row's start and its first button (§323).</summary>
    internal const float PrimaryTitleGapDip = 12;

    /// <summary>edit: the id of the row's type (§323): past every command's, beside a level's head's.</summary>
    internal const uint TypeTitleId = 0xFFFF_FFF1;

    // edit: where the row's type stands in the rows (content px), mirrored where the menu is; empty for none.
    private Area typeWords;

    /// <summary>Tests only: the type at the start of the open menu's top row, its words on screen (§323); null for none.</summary>
    internal RECT? TypeTitleOnScreen
    {
        get
        {
            if (!IsOpen || typeWords.W <= 0)
            {
                return null;
            }
            var near = new POINT((int)MathF.Round(viewport.X + typeWords.X), (int)MathF.Round(viewport.Y + typeWords.Y - scroll));
            var far = new POINT((int)MathF.Round(viewport.X + typeWords.X + typeWords.W), (int)MathF.Round(viewport.Y + typeWords.Y + typeWords.H - scroll));
            _ = ClientToScreen(Hwnd, &near);
            _ = ClientToScreen(Hwnd, &far);
            return new RECT { left = near.x, top = near.y, right = far.x, bottom = far.y };
        }
    }

    /// <summary>The type this showing names its top row by: <see cref="TypeTitle"/> on a menu of its own, none on a submenu.</summary>
    private string? Typed => parent is null && TypeTitle is { Length: > 0 } typed ? typed : null;

    /// <summary>Whether <paramref name="item"/> is a button of the row: one of it, not its type (§323).</summary>
    internal static bool IsButton(FlyoutItem item) => item.Primary && item.Id != TypeTitleId;

    /// <summary>How far down a line of Segoe UI its baseline is: a row's shortcut stands on its words' baseline.</summary>
    internal const float BaselineShare = 0.8f;

    /// <summary>
    /// menus: where the row's buttons stand, in every menu: each its own <paramref name="least"/> wide, a
    /// <paramref name="rule"/> px hairline between two, as Explorer's are, packed together against the far
    /// end of a row <paramref name="width"/> px wide, <paramref name="edge"/> px in (§350); whatever the row
    /// has to spare is left empty at its start, for a type (§323) or nothing (mirrored where the menu is).
    /// Never before the row's start, <paramref name="edge"/> in, where the row has no room. Whole pixels.
    /// </summary>
    internal static List<(float X, float W)> PrimarySpans(IReadOnlyList<float> least, float edge, float rule, float width)
    {
        var near = MathF.Round(edge, MidpointRounding.AwayFromZero);
        var wides = least.Select(wide => MathF.Round(wide, MidpointRounding.AwayFromZero)).ToList();
        var need = wides.Sum() + (rule * Math.Max(0, wides.Count - 1));
        var at = MathF.Max(near, MathF.Round(width - edge, MidpointRounding.AwayFromZero) - need);
        var spans = new List<(float X, float W)>(wides.Count);
        foreach (var wide in wides)
        {
            spans.Add((at, wide));
            at += wide + rule;
        }
        return spans;
    }

    /// <summary>menus: what a button's tooltip says: its name, and its shortcut as Explorer's say theirs, "Copy (Ctrl+C)".</summary>
    internal static string TipText(FlyoutItem item) => item.Keys is { } keys ? keys.Tip(item.Text) : item.Text;

    // The tooltip: the edit rail's and a folder's kind (EditRail.ShowTip, FolderFlyout.Tips.cs), kept (TipView, §359).
    private const nuint TipTimer = 5;
    private const uint TipDelayMs = 400;

    // What the tooltip's host is made with: the flyout's own window class and user data, a submenu's
    // its root's.
    private readonly nint tipDock;
    private readonly string tipClass = ClassName;
    private TipView? tip;

    // What the tooltip that stands says; null for none.
    private string? tipShown;

    /// <summary>What the icon buttons' tooltip, or a row's tool's, says now, for a test: null while none stands.</summary>
    internal string? TipShown => tipShown;

    /// <summary>The tooltip's window, for a test's picture of it; null before the first.</summary>
    internal HWND TipHwnd => tip?.Hwnd ?? HWND.NULL;

    /// <summary>
    /// menus: <paramref name="items"/> with their icon buttons (<see cref="FlyoutItem.Primary"/>) taken
    /// out to one row on top, in their order, ruled off from the rows under it, and
    /// <paramref name="type"/> at its start, the row its own where none is a button (§323). A rule their
    /// going leaves at an end of the rest, or beside another, goes with them. The items as they are where
    /// none is a button and there is no type.
    /// </summary>
    internal static IReadOnlyList<FlyoutItem> WithPrimaryRow(IReadOnlyList<FlyoutItem> items, string? type = null)
    {
        if (!items.Any(item => item.Primary) && type is null)
        {
            return items;
        }
        List<FlyoutItem> primaries =
        [
            .. type is null ? [] : new[] { new FlyoutItem(TypeTitleId, type, Enabled: false, Primary: true) },
            .. items.Where(item => item.Primary),
        ];
        var rest = new List<FlyoutItem>();
        foreach (var item in items.Where(item => !item.Primary))
        {
            if (item.Separator && (rest.Count == 0 || rest[^1].Separator))
            {
                continue;
            }
            rest.Add(item);
        }
        if (rest.Count > 0 && rest[^1].Separator)
        {
            rest.RemoveAt(rest.Count - 1);
        }
        if (rest.Count == 0)
        {
            return primaries;
        }
        FlyoutItem rule = new(0, "", Separator: true);
        return [.. primaries, rule, .. rest];
    }

    /// <summary>The icon button <paramref name="by"/> along from <paramref name="from"/> in its row; where it is at the row's end.</summary>
    private int AlongPrimaries(int from, int by)
    {
        var next = from + by;
        return next >= 0 && next < rows.Count && rows[next].Item.Primary && rows[next].Bounds.Y == rows[from].Bounds.Y ? next : from;
    }

    /// <summary>
    /// Row <paramref name="index"/> was lit: an icon button's tooltip after the hover time, or at once
    /// where another's stands, an icon tile's on whatever level (§249), and a row's whose words were cut
    /// short (CutWords); anything else has none.
    /// </summary>
    private void TipFor(int index)
    {
        _ = KillTimer(Hwnd, TipTimer);
        if (!IsOpen || TipOf(index) is null)
        {
            HideTip();
            return;
        }
        if (tipShown is not null)
        {
            ShowTip(index);
            return;
        }
        _ = SetTimer(Hwnd, TipTimer, TipDelayMs, null);
    }

    /// <summary>The pointer or the keys rested on an icon button: its tooltip, now.</summary>
    internal void TipDue()
    {
        _ = KillTimer(Hwnd, TipTimer);
        if (IsOpen && HotToolNow() is { } tool)
        {
            ShowTip(tool.Tool.Tip, tool.Box);
        }
        else if (IsOpen && TipOf(hot) is not null)
        {
            ShowTip(hot);
        }
    }

    /// <summary>
    /// Whether <paramref name="item"/> says its name in a tooltip when lit: whatever shows an icon and
    /// no words — an icon button of the row, an icon tile (§249), a level's head (§271) — in a menu
    /// and in its submenus alike, as a row's tools do (<see cref="ToolTipFor"/>).
    /// </summary>
    private static bool Tips(FlyoutItem item) => item.Primary || TipsTile(item) || item.Id == InlineBackId;

    /// <summary>
    /// What row <paramref name="index"/>'s tooltip says: its own where it has one (<see cref="Tips"/>) — a level's head says
    /// what it does, "Back", as Windows' back button does (Flyout.Inline.cs) — and its words whole on the first line where
    /// they were cut short (<see cref="CutWords.Said"/>); null for none.
    /// </summary>
    private string? TipOf(int index) =>
        index < 0 || index >= rows.Count ? null
        : CutWords.Said(rows[index].Cut, rows[index].Item.Id == InlineBackId ? S.DockPageBack : Tips(rows[index].Item) ? TipText(rows[index].Item) : null);

    /// <summary>Tests only: what the row with <paramref name="id"/> says in its tooltip (<see cref="TipOf"/>); null for none.</summary>
    internal string? TipOfRow(uint id) => TipOf(rows.FindIndex(row => row.Item.Id == id));

    /// <summary>
    /// The tooltip of icon button <paramref name="index"/>, centred on it, over the menu's top where the
    /// work area has the room, and under the button where not.
    /// It takes no pointer.
    /// </summary>
    private void ShowTip(int index)
    {
        if (TipOf(index) is { } text)
        {
            ShowTip(text, rows[index].Bounds);
        }
    }

    /// <summary>A tooltip saying <paramref name="text"/> for what stands at <paramref name="bounds"/> (content px): an icon button, a row's tool.</summary>
    private void ShowTip(string text, Area bounds)
    {
        if (gfx is not { } drawer)
        {
            return;
        }
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var panel = TipView.Panel(drawer, look, scale, text);
        var near = new POINT((int)MathF.Floor(viewport.X + bounds.X), (int)MathF.Floor(viewport.Y + bounds.Y - scroll));
        var far = new POINT((int)MathF.Ceiling(viewport.X + bounds.X + bounds.W), (int)MathF.Ceiling(viewport.Y + bounds.Y + bounds.H - scroll));
        _ = ClientToScreen(Hwnd, &near);
        _ = ClientToScreen(Hwnd, &far);
        var middle = (near.x + far.x) / 2;
        var gap = Px(TipView.GapDip);
        var above = near.y - gap - panel.Y >= work.top;
        var (anchor, side) = above
            ? (new POINT { x = middle, y = near.y }, Edge.Bottom)
            : (new POINT { x = middle, y = far.y }, Edge.Top);
        // A tip kept (§359): from one button to the next it moves in place, its words drawn once.
        tip ??= new TipView(drawer, instance, tipDock, tipClass);
        tip.Show(drawer, look, scale, text, anchor, work, side);
        tipShown = text;
    }

    /// <summary>The tool the pointer is on (<see cref="hotTool"/>), where it is a button; null for none.</summary>
    private (Area Box, FlyoutTool Tool)? HotToolNow() =>
        hotTool is var (row, at) && row >= 0 && row < rows.Count && at >= 0 && at < rows[row].Tools.Count
            ? (rows[row].Tools[at].Box, rows[row].Tools[at].Tool)
            : null;

    /// <summary>The pointer came onto a row's tool: its tooltip after the hover time, or at once where another stands.</summary>
    private void ToolTipFor()
    {
        _ = KillTimer(Hwnd, TipTimer);
        if (!IsOpen || HotToolNow() is not { } tool)
        {
            return;
        }
        if (tipShown is not null)
        {
            ShowTip(tool.Tool.Tip, tool.Box);
            return;
        }
        _ = SetTimer(Hwnd, TipTimer, TipDelayMs, null);
    }

    /// <summary>No tooltip: the pointer or the keys left the button, the menu scrolled or closes.</summary>
    private void HideTip()
    {
        _ = KillTimer(Hwnd, TipTimer);
        if (tipShown is null)
        {
            return;
        }
        tipShown = null;
        tip?.Hide();
    }
}
