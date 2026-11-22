// The window, material and placement every Karpo popup shares: the jump list
// and context menus (Flyout), the calendar, the notifications, the desktop
// flyout and the edit strip. An owner asks for a panel of some size beside an
// anchor and draws its content into Content; the host keeps the window, the
// Windows 11 flyout chrome for the Look (material, 1 px stroke, radius 8,
// shadow), where the panel goes, how it opens and closes, and whether it takes
// the focus.
//
// And how tall it is. A flyout is as tall as what it shows, up to one most
// (FlyoutPlacement.MostHeight, past which its own overflow scrolls), and it
// follows what it shows while it stands: it grows and shrinks at once, whether
// or not the pointer is on it (Fit). The person asked for it ("they should only
// take up as much space as their contents and there should be a max height"),
// and then for a shrink not to wait for the pointer either ("it should always
// update the layout. not holding back when i am there with my mouse"). A click
// that straddles a change still lands on what it was pressed on: each owner
// keeps the thing pressed by what it is, not by where it stood.
//
// The window never moves while it shows (§355, the flicker design's rule R2): it spans its display,
// and where the panel stands, and how large it is, is composition, which lands whole with the content
// in one commit — a window moved at once round content that followed a frame later was a frame of the
// new window round the old rows. What takes the pointer, and what DWM shows, is the window's region:
// where the window itself stood before.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using Windows.UI.Composition.Desktop;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;
using ICompositorDesktopInterop = TerraFX.Interop.WinRT.ICompositorDesktopInterop;
using IDesktopWindowTarget = TerraFX.Interop.WinRT.IDesktopWindowTarget;
using Material = Karpo.Core.Material;

namespace Karpo.Dock;

/// <summary>How a popup behaves.</summary>
public enum FlyoutMode
{
    /// <summary>
    /// Takes the focus. Escape, the Windows key, losing the activation and a click anywhere else on
    /// its own desktop end it, and the focus goes back to the window that had it: the taskbar's
    /// menus and flyouts.
    /// A click on a bar is the bar's to handle (<see cref="FlyoutHost.PressesHandledBy"/>).
    /// </summary>
    Menu,

    /// <summary>Never takes the focus and has no hook; reports the pointer coming and going, and its owner closes it.</summary>
    Hover,

    /// <summary>Never takes the focus and never closes by itself: the edit strip.</summary>
    Tool,
}

/// <summary>Where along the edge a popup goes.</summary>
public enum FlyoutAlign
{
    /// <summary>Centred on the anchor.</summary>
    Center,

    /// <summary>Flush with the work area's start (its left, or its top on a vertical bar).</summary>
    Start,

    /// <summary>Flush with the work area's end, as Windows' calendar is for a clock at the end.</summary>
    End,

    /// <summary>§385: centred in the work area, wherever the anchor stands: a folder set to the screen's middle.</summary>
    Middle,
}

/// <summary>
/// §521: where a popup hung from a control stands, as WinUI's ComboBox hangs its list (the person, 2026-11-07: "the
/// dropdowns also appear at the wrong location"): directly under the control, <c>gap</c> px off it, flush with its left
/// edge; over it where the room under it is short of the popup and the room over it is not, else on the roomier side. §531:
/// flush with its right edge instead where flush with its left it would run past the side of what the control stands in
/// (<c>within</c>: the settings panel, the folded edit bar) or the work area's, as a flyout of a button at a window's end
/// (WinUI's BottomEdgeAlignedLeft, else BottomEdgeAlignedRight); pushed in along x only where it still runs past the work
/// area's side, never nearer it than <c>keep</c> px. Never hung from a bar's edge, never moved off anything else
/// (FlyoutHost.ClearOf): one rule for every popup the edit bar and its panel open — a dropdown's list, a colour's swatches, a
/// button's level, the errors' (<see cref="FlyoutPlacement.Control"/>). §578: an edit bar entry's flyout, and the add page a
/// "+" hangs, <c>centred</c> on it instead (the person, 2026-11-09: "the flyouts should be centered under the button"),
/// pushed in only where it would pass the work area's side — §584 its <see cref="Beak"/> pointing at it from there. All in the
/// screen px of the control's own display.
/// </summary>
public static class PopupPlace
{
    /// <summary>WinUI's air between a control and its popup (a ComboBox's list, a button's flyout): 4 DIP.</summary>
    public const float GapDip = 4;

    /// <summary>
    /// The popup <paramref name="width"/> × <paramref name="height"/> px under (or over) <paramref name="control"/>, inside
    /// <paramref name="work"/>, and inside <paramref name="within"/>'s sides where it fits — §578 or <paramref name="centred"/> on
    /// it —; and whether it stands over it.
    /// </summary>
    public static (RECT Rect, bool Above) Of(RECT control, int width, int height, RECT work, int gap, int keep, RECT? within = null, bool centred = false)
    {
        var below = Below(control, work, gap, keep);
        var above = Above(control, work, gap, keep);
        var over = height > below && (height <= above || above > below);
        // §559: taller than either side's room, the roomier side, the popup no taller than its room (what it shows scrolls in
        // it, laid out against Room) — never pushed back over its control, which it then hid (a colour's picker low on a
        // display stood over its own button).
        var room = over ? above : below;
        var tall = room > 0 ? Math.Min(height, room) : height;
        var top = over ? control.top - gap - tall : control.bottom + gap;
        // Only a control with no room on either side (as tall as the work area) has its popup pushed onto the work area over it.
        top = Math.Clamp(top, work.top + keep, Math.Max(work.top + keep, work.bottom - keep - tall));
        height = tall;
        var (least, most) = (work.left + keep, work.right - keep);
        if (within is { } sides)
        {
            (least, most) = (Math.Max(least, sides.left), Math.Min(most, sides.right));
        }
        var left = centred ? ((control.left + control.right) / 2) - (width / 2)
            : control.left + width <= most || control.right - width < least ? control.left : control.right - width;
        left = Math.Clamp(left, work.left + keep, Math.Max(work.left + keep, work.right - keep - width));
        return (new RECT { left = left, top = top, right = left + width, bottom = top + height }, over);
    }

    /// <summary>The room under <paramref name="control"/> for a popup, its gap and the work area's margin kept (px).</summary>
    public static int Below(RECT control, RECT work, int gap, int keep) => Math.Max(0, work.bottom - keep - (control.bottom + gap));

    /// <summary>The room over <paramref name="control"/> for a popup (px).</summary>
    public static int Above(RECT control, RECT work, int gap, int keep) => Math.Max(0, control.top - gap - (work.top + keep));

    /// <summary>The most a popup of <paramref name="control"/> may be tall: the roomier side's room (px).</summary>
    public static int Room(RECT control, RECT work, int gap, int keep) => Math.Max(Below(control, work, gap, keep), Above(control, work, gap, keep));
}

/// <summary>
/// §584 (the person, 2026-11-09: "the topbar flyouts should have a little distance to the topbar and also an angle that points
/// to the button"): the beak of a popup hung by <see cref="FlyoutPlacement.Beak"/> — an edit bar entry's flyout, the add page a
/// "+" hangs. A triangle of the panel's own outline, its material, rim and shadow the panel's (FlyoutHost.PlacePanels), standing
/// out of the side that faces what it hangs from into the gap the panel keeps from it, its tip over that thing's middle — on the
/// panel's top under it, on its foot over it; where the panel was pushed in at the work area's side, still over that middle.
/// Pure: where it stands. Drawn as one outline with the panel's rounded rectangle (CutPath.Beaked).
/// </summary>
public static class Beak
{
    /// <summary>How far the beak stands out of the panel, its sides at 45° (WinUI's TeachingTip's tail): 8 DIP, 16 across its foot.</summary>
    public const float TallDip = 8;

    /// <summary>
    /// The panel this far off what it hangs from: the beak's 8 and 4 of air before its tip — as Windows 11's flyouts stand
    /// 12 off the taskbar.
    /// </summary>
    public const float GapDip = 12;

    /// <summary>
    /// Where along the panel's side the tip stands, in panel px from its left: over <paramref name="control"/>'s middle, never
    /// nearer an end than the corner's <paramref name="corner"/> and the beak's half foot (<paramref name="tall"/>), so its foot
    /// stays on the straight of the side; the side's middle where it is too short for that. <paramref name="panel"/> in the
    /// control's px.
    /// </summary>
    public static float TipAt(RECT control, RECT panel, float corner, float tall)
    {
        var wide = panel.right - panel.left;
        var (least, most) = (corner + tall, wide - corner - tall);
        return most < least ? wide / 2f : Math.Clamp(((control.left + control.right) / 2f) - panel.left, least, most);
    }

    /// <summary>
    /// The beak's foot's two ends and its tip, in the order the outline runs (clockwise: left to right on the top, right to left
    /// on the foot), in px from the top left of a panel <paramref name="height"/> px tall: on its top (<paramref name="side"/>
    /// Top) or its foot (Bottom), the tip <paramref name="tip"/> px along it, <paramref name="tall"/> out of it. The outline
    /// <paramref name="inset"/> px inside that one (the rim's middle; the material inside a rim drawn outside it): the side that
    /// far in, the tip <paramref name="inset"/> × √2, the sides as steep.
    /// </summary>
    public static (Vector2 From, Vector2 Tip, Vector2 To) Triangle(float height, Edge side, float tip, float tall, float inset)
    {
        var diagonal = inset * MathF.Sqrt(2);
        var half = tall + inset - diagonal;
        return side == Edge.Bottom
            ? (new Vector2(tip + half, height - inset), new Vector2(tip, height + tall - diagonal), new Vector2(tip - half, height - inset))
            : (new Vector2(tip - half, inset), new Vector2(tip, diagonal - tall), new Vector2(tip + half, inset));
    }
}

/// <param name="Anchor">The middle of the clicked thing's inner side, in screen px.</param>
/// <param name="Work">The rectangle to stay inside.</param>
/// <param name="Edge">The bar's edge: the popup opens away from it.</param>
/// <param name="GapDip">Between the anchor and the panel.</param>
/// <param name="MarginDip">Kept from <paramref name="Work"/>'s ends along the edge.</param>
/// <param name="Over">
/// Neither side of the anchor has the room the panel needs (<see cref="Roomiest"/>): it stands over
/// the anchor, centred on it across the edge as it is along it, inside the work area.
/// </param>
/// <param name="Control">
/// §521: the control the popup hangs from (screen px): it stands under it, flush with its left edge, or over it with no room
/// under it (<see cref="PopupPlace"/>), whatever <paramref name="Anchor"/>, <paramref name="Edge"/> and
/// <paramref name="Align"/> say. Null for a popup of a bar.
/// </param>
/// <param name="Within">§531: what <paramref name="Control"/> stands in (screen px), whose sides the popup keeps inside where it fits (<see cref="PopupPlace"/>).</param>
/// <param name="Beak">
/// §578: the popup centred on <paramref name="Control"/> rather than flush with it (an edit bar entry's flyout, the add page of
/// a "+"); §584 its <see cref="Karpo.Dock.Beak"/> pointing at it.
/// </param>
public readonly record struct FlyoutPlacement(
    POINT Anchor,
    RECT Work,
    Edge Edge,
    float GapDip = 12,
    FlyoutAlign Align = FlyoutAlign.Center,
    float? Margin = null,
    bool Over = false,
    RECT? Control = null,
    RECT? Within = null,
    bool Beak = false)
{
    /// <summary>
    /// Kept from the work area's ends along the edge, at either end and wherever a panel is pushed
    /// against one: Windows 11's, its Start, quick settings, calendar and notifications standing 12 DIP
    /// in from the screen's sides as they stand 12 off the taskbar (<see cref="GapDip"/>).
    /// </summary>
    private const float EdgeMarginDip = 12;

    /// <summary>
    /// Kept from <see cref="Work"/>'s ends along the edge: one margin, so a panel pushed against an end
    /// stands where one placed at that end does (§391: an Automatic folder at the screen's right kept 8 DIP,
    /// a Right one 12, and the two did not line up).
    /// <para>
    /// The rule lives here rather than at every call. Twelve flyouts each wrote their own margin for
    /// themselves — one quantity with twelve homes, which is the same shape of defect as a card that
    /// padded itself.
    /// </para>
    /// </summary>
    public float MarginDip => Margin ?? EdgeMarginDip;

    /// <summary>
    /// §651: this placement flush with its bar and with the work area's ends — no gap off the anchor, no margin kept —: an
    /// attached folder's, whose panel stands on its ground, which is the bar grown out (FlyoutHost.Grounded).
    /// </summary>
    public FlyoutPlacement Flush => this with { GapDip = 0, Margin = 0 };

    /// <summary>
    /// Where a <paramref name="width"/> by <paramref name="height"/> px panel starts along the edge:
    /// centred on the anchor, or pushed to the work area's start or end, or centred in it (<see cref="Align"/>), and
    /// kept <paramref name="keep"/> px inside its ends. Along x on a top or bottom bar, along y on a
    /// left or right one.
    /// </summary>
    internal int Along(int width, int height, int keep)
    {
        var vertical = Edge is Edge.Left or Edge.Right;
        var length = vertical ? height : width;
        var low = vertical ? Work.top : Work.left;
        var high = vertical ? Work.bottom : Work.right;
        var anchor = vertical ? Anchor.y : Anchor.x;
        var start = Align switch
        {
            FlyoutAlign.Start => low + keep,
            FlyoutAlign.End => high - keep - length,
            FlyoutAlign.Middle => low + ((high - low - length) / 2),
            _ => anchor - (length / 2),
        };
        // Never before low + keep; never past high - keep - length, unless the panel is longer than
        // the room, when it starts at low + keep and the far end is what gives.
        var least = low + keep;
        return Math.Clamp(start, least, Math.Max(high - keep - length, least));
    }

    /// <summary>
    /// §359: where a <paramref name="width"/> by <paramref name="height"/> px panel hung from the anchor stands
    /// on the screen at <paramref name="scale"/>, as a popup's host places it (FlyoutHost.Arrange) — known
    /// before it is shown: the sections' edit bars keep clear of each other by it, each shown once, where it
    /// stands. The plain placement: no core, nowhere it was held, room beside the anchor.
    /// </summary>
    internal RECT PanelAt(int width, int height, float scale)
    {
        int Px(float dip) => (int)MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var apart = Px(GapDip);
        if (Control is { } control)
        {
            return PopupPlace.Of(control, width, height, Work, apart, Px(MarginDip), Within, Beak).Rect;
        }
        var along = Along(width, height, Px(MarginDip));
        var (x, top) = Edge switch
        {
            Edge.Top => (along, Math.Min(Math.Max(Anchor.y + apart, Work.top), Work.bottom - height)),
            Edge.Left => (Anchor.x + apart, along),
            Edge.Right => (Anchor.x - apart - width, along),
            _ => (along, Math.Max(Math.Min(Anchor.y - apart, Work.bottom) - height, Work.top)),
        };
        return new RECT { left = x, top = top, right = x + width, bottom = top + height };
    }

    /// <summary>
    /// The share of the work area's height a panel may take. Past it the panel stops growing and its
    /// own overflow takes over: the part of it that scrolls, scrolls (<see cref="MostHeight(float)"/>).
    /// <para>
    /// Seventy per cent, because that is what every panel already stopped at — seven flyouts each
    /// kept a copy of the arithmetic, and the calendar, the notifications and screen time each wrote
    /// the share into their own layout — so giving the rule one home changes no panel's most on a
    /// common screen. And it is the right number on its own terms. On a 1080p screen it is some
    /// 730 px, room for a month with a few events under it or ten Wi-Fi networks, so a panel is
    /// nearly always simply as tall as what it shows and only a long list meets the most. On a small
    /// laptop it still leaves the top third of the desktop in sight, so a flyout stays a glance over
    /// what is behind it rather than a window over the screen, and its far edge stays well clear of
    /// the top of the screen, as Windows' own flyouts do.
    /// </para>
    /// </summary>
    public const float MostShare = 0.7f;

    /// <summary>
    /// The least <see cref="MostHeight(float)"/> comes to, on a work area too short for its share to
    /// hold a header and a couple of rows.
    /// </summary>
    public const float MostFloorDip = 160;

    /// <summary>
    /// The most a panel placed here may be, in px at <paramref name="scale"/>: <see cref="MostShare"/>
    /// of the work area's height, and never more than fits between the anchor (with the gap) and the
    /// work area's far end. Every flyout of a widget lays itself out against this one number, and
    /// <see cref="FlyoutHost.Fit"/> sizes the panel for what it then asks for.
    /// <para>
    /// It had seven homes, one per flyout, each <c>MaxHeight()</c> the same lines with its own floor
    /// (96, 120 or 160 DIP) — the shape of defect <see cref="MarginDip"/> was moved here to end.
    /// </para>
    /// </summary>
    public float MostHeight(float scale) => MostHeight(Work.bottom - Work.top, Room(scale), scale);

    /// <summary>
    /// The whole room a panel placed here has, in px at <paramref name="scale"/>: from the anchor
    /// (with the gap) to the work area's far end less the margin, or the work area's height less a
    /// margin at either end beside a side bar — what Windows' notification center reaches up to. A
    /// folder with a Fill line is this tall (§173); every other panel stops at <see cref="MostHeight(float)"/>.
    /// </summary>
    public float Room(float scale)
    {
        var gap = Round(GapDip * scale);
        var margin = Round(MarginDip * scale);
        if (Control is { } control)
        {
            return PopupPlace.Room(control, Work, (int)gap, (int)margin);
        }
        return Edge switch
        {
            _ when Over => Work.bottom - Work.top - 2 * margin,
            Edge.Top => Work.bottom - margin - Math.Max(Anchor.y + gap, Work.top),
            Edge.Left or Edge.Right => Work.bottom - Work.top - 2 * margin,
            _ => Math.Min(Anchor.y - gap, Work.bottom) - (Work.top + margin),
        };
    }

    /// <summary>
    /// The room away from the anchor across the edge, in px at <paramref name="scale"/>: down from a
    /// top edge's anchor, up from a bottom one's (<see cref="Room(float)"/>), right from a left
    /// edge's, left from a right one's; the work area's whole extent, less a margin at either end,
    /// where the panel stands <see cref="Over"/> the anchor.
    /// </summary>
    public float Depth(float scale)
    {
        if (Edge is Edge.Top or Edge.Bottom)
        {
            return Room(scale);
        }
        var gap = Round(GapDip * scale);
        var margin = Round(MarginDip * scale);
        return Edge switch
        {
            _ when Over => Work.right - Work.left - 2 * margin,
            Edge.Left => Work.right - margin - Math.Max(Anchor.x + gap, Work.left),
            _ => Math.Min(Anchor.x - gap, Work.right) - (Work.left + margin),
        };
    }

    /// <summary>
    /// folders: the widest a folder's lists with widgets (FolderPlan.Core) may be, in px at
    /// <paramref name="scale"/>, before the far one runs past the work area's edge (§320, §328), as
    /// <see cref="FlyoutHost"/> places them: along a top or bottom bar the work area's width less the
    /// margin they start in by once they are longer than the room (<see cref="Along"/>); across a side
    /// bar from the anchor and its gap to the work area's far side.
    /// </summary>
    public float CoreRoom(float scale)
    {
        var gap = Round(GapDip * scale);
        return Edge switch
        {
            Edge.Left when !Over => Work.right - (Anchor.x + gap),
            Edge.Right when !Over => Anchor.x - gap - Work.left,
            _ => Work.right - Work.left - Round(MarginDip * scale),
        };
    }

    /// <summary>
    /// Where a panel that wants <paramref name="wanted"/> px across the edge (its height off a top or
    /// bottom edge, its width off a side one), and needs <paramref name="least"/> of them, opens (the
    /// person, 2026-10-16: "when the add button is all the way on the top of a screen the size of the
    /// flyout add widgets is squished"): away from <see cref="Edge"/> where that side has what it
    /// wants; else the other side, where that has it; else whichever side has more, where that has
    /// the least it needs; else <see cref="Over"/> the anchor, as much of the work area as there is.
    /// A placement already over stays so.
    /// </summary>
    public FlyoutPlacement Roomiest(float wanted, float least, float scale)
    {
        // §521: a control's popup takes the roomier side of it as it is placed (PopupPlace).
        if (Over || Control is not null)
        {
            return this;
        }
        var here = Depth(scale);
        if (here >= wanted)
        {
            return this;
        }
        var flipped = this with { Edge = Opposite(Edge) };
        var there = flipped.Depth(scale);
        if (there >= wanted)
        {
            return flipped;
        }
        if (MathF.Max(here, there) >= MathF.Min(least, wanted))
        {
            return there > here ? flipped : this;
        }
        return this with { Over = true };
    }

    /// <summary>
    /// Where a <paramref name="length"/> px panel standing <see cref="Over"/> the anchor starts across
    /// the edge (its top off a top or bottom edge, its left off a side one): centred on the anchor,
    /// kept <paramref name="keep"/> px inside the work area, its start what stays where it is longer.
    /// </summary>
    internal int OverStart(int length, int keep)
    {
        var vertical = Edge is Edge.Top or Edge.Bottom;
        var low = vertical ? Work.top : Work.left;
        var high = vertical ? Work.bottom : Work.right;
        var anchor = vertical ? Anchor.y : Anchor.x;
        var least = low + keep;
        return Math.Clamp(anchor - (length / 2), least, Math.Max(high - keep - length, least));
    }

    private static Edge Opposite(Edge edge) => edge switch
    {
        Edge.Top => Edge.Bottom,
        Edge.Bottom => Edge.Top,
        Edge.Left => Edge.Right,
        _ => Edge.Left,
    };

    /// <summary>
    /// <see cref="Room(float)"/> for a layout drawn without a placement (a picture, a test): a
    /// <paramref name="workHeight"/> px work area, the anchor at its bar side, the far end's margin
    /// kept, as the clock's flyout at the bar's end is placed.
    /// </summary>
    public static float Room(float workHeight, float scale) => workHeight - Round(12 * scale) - Round(EdgeMarginDip * scale);

    /// <summary>
    /// The rule of <see cref="MostHeight(float)"/> itself, for a work area <paramref name="workHeight"/>
    /// px tall with <paramref name="room"/> px free beside the anchor: for a layout drawn without a
    /// placement (a picture, a test), which still has to stop where a shown panel would.
    /// </summary>
    public static float MostHeight(float workHeight, float room, float scale) =>
        MathF.Max(Round(MostFloorDip * scale), MathF.Min(MathF.Floor(MostShare * workHeight), room));

    // Rust's f32::round, as everywhere in Karpo.
    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);
}

public enum FlyoutHostEventKind
{
    Nothing,

    /// <summary>The popup closed by itself (Menu: Escape, the Windows key, deactivation, a click elsewhere; any: its window went).</summary>
    Dismissed,

    /// <summary>The pointer came over the window.</summary>
    Entered,

    /// <summary>The pointer left the window.</summary>
    Left,

    /// <summary>A Tool popup's display changed scale or layout: show it again.</summary>
    DpiChanged,
}

public readonly record struct FlyoutHostEvent(FlyoutHostEventKind Kind);

/// <summary>
/// WinUI's Windows 11 colour tokens for a popup's content, for a <see cref="Look"/>. High contrast
/// uses the system colours. Text colours come as <see cref="DXGI_RGBA"/> for Gfx, fills as
/// <see cref="Color"/> for composition brushes.
/// </summary>
public sealed record FlyoutTheme
{
    public required bool Dark { get; init; }
    public required bool HighContrast { get; init; }

    /// <summary>Host backdrop under a tint, rather than a solid base.</summary>
    public required bool Glass { get; init; }

    /// <summary>AcrylicBackgroundFillColorDefault's fallback: the solid base.</summary>
    public required Color Base { get; init; }

    /// <summary>The tint over the host backdrop.</summary>
    public required Color Tint { get; init; }

    /// <summary>SurfaceStrokeColorFlyout.</summary>
    public required Color Stroke { get; init; }

    public required float StrokeDip { get; init; }
    public required float ShadowOpacity { get; init; }

    /// <summary>
    /// The panel casts its shadow (<see cref="ShadowOpacity"/>). A taskbar flyout does only with
    /// transparency effects on (<see cref="Taskbar"/>); what lifts off it in edit mode still does.
    /// </summary>
    public bool Casts { get; init; } = true;

    /// <summary>
    /// The rim lies over what is behind the panel, the material inside it, as Windows' taskbar
    /// flyouts draw theirs (<see cref="Taskbar"/>): a see-through <see cref="Stroke"/> lighter over a
    /// light desktop and darker over a dark one. Otherwise it lies over the material.
    /// </summary>
    public bool RimOutside { get; init; }

    public required DXGI_RGBA Text { get; init; }
    public required DXGI_RGBA TextSecondary { get; init; }
    public required DXGI_RGBA TextTertiary { get; init; }
    public required DXGI_RGBA TextDisabled { get; init; }

    /// <summary>TextOnAccentFillColorPrimary: text on <see cref="Accent"/>.</summary>
    public required DXGI_RGBA TextOnAccent { get; init; }

    /// <summary>Text over a lit item: the selection's text colour in high contrast, the usual one otherwise.</summary>
    public required DXGI_RGBA TextOnHover { get; init; }

    /// <summary>SubtleFillColorSecondary: an item under the pointer.</summary>
    public required Color SubtleHover { get; init; }

    /// <summary>SubtleFillColorTertiary: a pressed item.</summary>
    public required Color SubtlePressed { get; init; }

    /// <summary>DividerStrokeColorDefault.</summary>
    public required Color Divider { get; init; }

    /// <summary>CardBackgroundFillColorDefault.</summary>
    public required Color CardBackground { get; init; }

    /// <summary>CardStrokeColorDefault.</summary>
    public required Color CardStroke { get; init; }

    /// <summary>ControlFillColorDefault: a standard button.</summary>
    public required Color ControlFill { get; init; }

    /// <summary>ControlFillColorSecondary: a standard button under the pointer.</summary>
    public required Color ControlFillHover { get; init; }

    /// <summary>ControlFillColorTertiary: a pressed standard button.</summary>
    public required Color ControlFillPressed { get; init; }

    /// <summary>ControlStrokeColorDefault.</summary>
    public required Color ControlStroke { get; init; }

    /// <summary>ControlAltFillColorSecondary: the track a segmented choice's segments stand on (§253).</summary>
    public required Color ControlAltFill { get; init; }

    /// <summary>AccentFillColorDefault.</summary>
    public required Color Accent { get; init; }

    /// <summary>AccentFillColorSecondary: an accent button under the pointer.</summary>
    public required Color AccentHover { get; init; }

    /// <summary>AccentFillColorTertiary: a pressed accent button.</summary>
    public required Color AccentPressed { get; init; }

    /// <summary>FocusStrokeColorOuter.</summary>
    public required Color FocusOuter { get; init; }

    /// <summary>FocusStrokeColorInner.</summary>
    public required Color FocusInner { get; init; }

    /// <summary>ControlStrongFillColorDefault: a scroll indicator.</summary>
    public required Color ScrollIndicator { get; init; }

    // §544: the rest of the tokens Windows' Settings app draws with, WinUI 3's own (Common_themeresources_any.xaml), so the
    // settings surface and its popups take every colour from here (docs\widgets.md §544 has the table and the sources).

    /// <summary>
    /// SolidBackgroundFillColorBase: the Settings window's ground where Mica falls back — an inactive window, transparency
    /// effects off —, which the settings panel always is (§544: it never takes the activation); a ContentDialog's.
    /// </summary>
    public required Color Ground { get; init; }

    /// <summary>LayerFillColorAlt: a ContentDialog's top over its ground.</summary>
    public required Color LayerAlt { get; init; }

    /// <summary>CardBackgroundFillColorSecondary: an open expander's content under its rows (ExpanderContentBackground).</summary>
    public required Color CardBackgroundSecondary { get; init; }

    /// <summary>ControlFillColorDisabled: a disabled control's, a disabled card's fill.</summary>
    public required Color ControlFillDisabled { get; init; }

    /// <summary>ControlFillColorInputActive: a text box with the keys.</summary>
    public required Color ControlFillInputActive { get; init; }

    /// <summary>
    /// ControlStrokeColorSecondary: the edge of ControlElevationBorderBrush — a control's top in the dark theme, its bottom in
    /// the light one (<see cref="ElevationAtTop"/>).
    /// </summary>
    public required Color ControlStrokeSecondary { get; init; }

    /// <summary>
    /// ControlElevationBorderBrush's edge stands at the top (the dark theme's brush is not flipped) rather than the bottom
    /// (the light theme's ScaleY −1); AccentControlElevationBorderBrush's is the bottom in both.
    /// </summary>
    public bool ElevationAtTop => Dark && !HighContrast;

    /// <summary>ControlStrokeColorOnAccentDefault: an accent button's rim.</summary>
    public required Color AccentStroke { get; init; }

    /// <summary>ControlStrokeColorOnAccentSecondary: an accent button's bottom edge (AccentControlElevationBorderBrush).</summary>
    public required Color AccentStrokeBottom { get; init; }

    /// <summary>ControlStrongStrokeColorDefault: a switch's rim when off, a text box's bottom line (TextControlElevationBorderBrush).</summary>
    public required Color ControlStrongStroke { get; init; }

    /// <summary>ControlStrongStrokeColorDisabled.</summary>
    public required Color ControlStrongStrokeDisabled { get; init; }

    /// <summary>ControlAltFillColorTertiary: a switch that is off, under the pointer.</summary>
    public required Color ControlAltFillHover { get; init; }

    /// <summary>ControlAltFillColorQuarternary: a switch that is off, pressed.</summary>
    public required Color ControlAltFillPressed { get; init; }

    /// <summary>
    /// AccentTextFillColorPrimary: a link's words (HyperlinkButtonForeground) — SystemAccentColorLight3 dark, Dark2 light;
    /// high contrast the hyperlink colour.
    /// </summary>
    public required Color AccentText { get; init; }

    /// <summary>AccentTextFillColorSecondary: a link under the pointer — Light3 dark, Dark3 light.</summary>
    public required Color AccentTextHover { get; init; }

    /// <summary>AccentTextFillColorTertiary: a pressed link — Light2 dark, Dark1 light.</summary>
    public required Color AccentTextPressed { get; init; }

    /// <summary>AccentFillColorDisabled: a switch that is on, disabled; a disabled accent button.</summary>
    public required Color AccentDisabled { get; init; }

    /// <summary>TextOnAccentFillColorSecondary: a pressed accent button's text.</summary>
    public required DXGI_RGBA TextOnAccentSecondary { get; init; }

    /// <summary>TextOnAccentFillColorDisabled: a disabled switch's knob when on.</summary>
    public required DXGI_RGBA TextOnAccentDisabled { get; init; }

    /// <summary>TextFillColorInverse: the glyph on an InfoBar's error icon.</summary>
    public required DXGI_RGBA TextInverse { get; init; }

    /// <summary>SystemFillColorCritical: what went wrong (an InfoBar's error icon).</summary>
    public required Color Critical { get; init; }

    /// <summary>SystemFillColorCaution: a meter at its limit (a ProgressBar paused).</summary>
    public required Color Caution { get; init; }

    /// <summary>ToolTipBackground: SystemChromeMediumLowColor, a WinUI ToolTip's solid plate.</summary>
    public required Color ToolTip { get; init; }

    /// <summary>
    /// The tokens for <paramref name="look"/>: Windows' own, with the flyout background somebody chose
    /// (<see cref="Look.Colors"/>, never in high contrast) over them — the colour as it is over the
    /// blur with transparency effects on, and without its alpha where the flyout is solid.
    /// </summary>
    public static FlyoutTheme For(Look look) => Chosen(look, Automatic(look));

    /// <summary>
    /// The tokens for a taskbar flyout — a folder, quick settings — in <paramref name="look"/>: the
    /// surface Windows' own taskbar flyouts have (quick settings, the notification center, the
    /// calendar), measured off them on 2026-10-16 (docs\widgets.md §9, "Flyout surface"), the
    /// content's tokens as <see cref="For"/>'s. Dark: the solid body #242424, where WinUI's acrylic
    /// fallback would be #2C2C2C. The rim, both themes: <see cref="TaskbarStroke"/> over what is
    /// behind the panel (<see cref="RimOutside"/>), whole px. No shadow without transparency
    /// effects: Windows casts none then, over a white window or a black one. High contrast as
    /// <see cref="For"/>'s, the rim outside.
    /// </summary>
    public static FlyoutTheme Taskbar(Look look)
    {
        var theme = Automatic(look);
        if (theme.HighContrast)
        {
            return theme with { RimOutside = true };
        }
        var body = theme.Dark ? TaskbarDark : theme.Base;
        return Chosen(look, theme with
        {
            Base = body,
            Tint = WithAlpha(body, theme.Dark ? 0.90f : 0.85f),
            Stroke = TaskbarStroke,
            Casts = look.Glass,
            RimOutside = true,
        });
    }

    /// <summary>A dark taskbar flyout's body without transparency effects, as Windows' measure.</summary>
    internal static readonly Color TaskbarDark = Color.FromArgb(255, 0x24, 0x24, 0x24);

    /// <summary>
    /// A taskbar flyout's rim, over what is behind it: #757575 at 40 %, fitted to Windows' quick
    /// settings rim over four backdrops (white C8, #1C1C1D 40, #131419 3A3B3E, #04050B 313236).
    /// </summary>
    internal static readonly Color TaskbarStroke = Color.FromArgb(0x66, 0x75, 0x75, 0x75);

    /// <summary>
    /// §334, §464: the tokens for a folder's panel and its lines in <paramref name="look"/>: a taskbar flyout's
    /// (<see cref="Taskbar"/>), but that the panel draws nothing but its rim (<see cref="Bare"/>; the person: "folder flyout
    /// should not have a background color because it is never visible"), nor casts a shadow, whose shape would show between
    /// its lines: each line wears its own background over the desktop (FolderFlyout.LineFill). <see cref="Base"/>,
    /// <see cref="Tint"/> and <see cref="Glass"/> are Windows' own taskbar flyout's surface, which a line of Windows' own
    /// background wears. High contrast as <see cref="Taskbar"/>'s.
    /// </summary>
    public static FlyoutTheme Folder(Look look)
    {
        var theme = Taskbar(look);
        if (theme.HighContrast)
        {
            return theme;
        }
        var windows = Taskbar(look with { Colors = look.Colors with { Flyout = null } });
        return theme with { Base = windows.Base, Tint = windows.Tint, Glass = look.Glass, Bare = true, Casts = false };
    }

    /// <summary>§464: the panel draws its rim alone, no plate: a folder's, whose lines wear their backgrounds each.</summary>
    public bool Bare { get; init; }

    /// <summary>
    /// §704: the Liquid glass a panel is drawn in where its palette is worn — its glow and lens over its plate, its rim the
    /// glass's, its corners the glass's (<see cref="CornerDip"/>); null for another look and in high contrast.
    /// </summary>
    public Karpo.Core.GlassLook? Liquid { get; init; }

    /// <summary>A panel's corners, DIP: Windows 11's 8 for its flyouts; §704 Liquid glass's own.</summary>
    public float CornerDip => Liquid?.RadiusDip ?? 8;

    // §396: the layout's colours over the theme's, never in high contrast, the same for every panel — a menu, a
    // widget's flyout, a folder: the flyouts' background, their outline, and the highlight of what lights in them.
    private static FlyoutTheme Chosen(Look look, FlyoutTheme theme)
    {
        if (look.HighContrast)
        {
            return theme;
        }
        var colors = look.Colors;
        // §704: Liquid glass's light on every panel, where its palette is worn.
        theme = Chosen(colors.Flyout, theme) with { Liquid = look.Liquid };
        if (colors.FlyoutOutline is { } rim)
        {
            theme = theme with { Stroke = Color.FromArgb(rim.A, rim.R, rim.G, rim.B) };
        }
        // §459: the highlight of what is drawn on the panels: their text's.
        if (colors.PanelText is { } ink && Karpo.Core.ToneRules.HighlightOf(ink) is var hover)
        {
            var lit = Color.FromArgb(hover.A, hover.R, hover.G, hover.B);
            theme = theme with { SubtleHover = lit, SubtlePressed = WithAlpha(lit, hover.A / 255f * (2f / 3f)) };
        }
        return theme;
    }

    // §464: the menus' background somebody chose, laid over Windows' own flyout, never in its place: solid over its solid
    // base, over its tint where it is see-through — a menu is never less than Windows' flyout, so it reads over any desktop,
    // a background of None or 0 % too (that is Windows' flyout itself).
    private static FlyoutTheme Chosen(Karpo.Core.Argb? chosen, FlyoutTheme theme) =>
        chosen is { A: > 0 } color
            ? theme with
            {
                Base = ColorOf(Karpo.Core.ToneRules.Blend(color, ArgbOf(theme.Base) with { A = 255 })),
                Tint = ColorOf(Karpo.Core.ToneRules.Over(color, ArgbOf(theme.Tint))),
            }
            : theme;

    private static Color ColorOf(Karpo.Core.Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static Karpo.Core.Argb ArgbOf(Color color) => new(color.A, color.R, color.G, color.B);

    /// <summary>
    /// Windows' own tokens for <paramref name="look"/>, and §396, §459 the text its panels' style sets (<see cref="Palette.PanelText"/>:
    /// the Widgets in folders') in place of Windows' white or black: the quieter texts it, fainter by
    /// Windows' own steps.
    /// </summary>
    private static FlyoutTheme Automatic(Look look)
    {
        var theme = Windows(look);
        if (look.HighContrast || look.Colors.PanelText is not { } ink)
        {
            return theme;
        }
        DXGI_RGBA Faded(float share) => Gfx.Ink(ink.R / 255f, ink.G / 255f, ink.B / 255f, ink.A / 255f * share);
        return theme with
        {
            Text = Faded(1),
            TextOnHover = Faded(1),
            TextSecondary = Faded(0xC5 / 255f),
            TextTertiary = Faded(0x87 / 255f),
            TextDisabled = Faded(0x5D / 255f),
            // §544: a switch's and a radio's rim (ControlStrongStroke, Windows' white at 0x8B or black at 0x72) in that text too.
            ControlStrongStroke = Color.FromArgb((byte)MathF.Round(ink.A * (theme.Dark ? 0x8B : 0x72) / 255f), ink.R, ink.G, ink.B),
        };
    }

    /// <summary>
    /// §544: the tokens for a tooltip (TipView) in <paramref name="look"/>: <see cref="For"/>'s, on WinUI's ToolTip plate —
    /// SystemChromeMediumLowColor, solid (a ToolTip wears no acrylic), SurfaceStrokeColorFlyout round it —, the colours
    /// somebody chose over it as over every panel.
    /// </summary>
    public static FlyoutTheme Tip(Look look)
    {
        var theme = Automatic(look);
        return theme.HighContrast ? theme : Chosen(look, theme with { Base = theme.ToolTip, Tint = theme.ToolTip, Glass = false });
    }

    private static FlyoutTheme Windows(Look look)
    {
        if (look.HighContrast)
        {
            var window = Opaque(look.HcWindow);
            var text = Opaque(look.HcText);
            var highlight = Opaque(look.HcHighlight);
            var highlightText = Opaque(look.HcHighlightText);
            uint gray = GetSysColor(COLOR.COLOR_GRAYTEXT);
            var grayText = Color.FromArgb(255, (byte)(gray & 0xFF), (byte)((gray >> 8) & 0xFF), (byte)((gray >> 16) & 0xFF));
            uint hot = GetSysColor(COLOR.COLOR_HOTLIGHT);
            var hotLight = Color.FromArgb(255, (byte)(hot & 0xFF), (byte)((hot >> 8) & 0xFF), (byte)((hot >> 16) & 0xFF));
            return new FlyoutTheme
            {
                Dark = look.Dark,
                HighContrast = true,
                Glass = false,
                Base = window,
                Tint = window,
                Stroke = text,
                StrokeDip = 2,
                ShadowOpacity = 0,
                Text = Ink(text),
                TextSecondary = Ink(text),
                TextTertiary = Ink(text),
                TextDisabled = Ink(grayText),
                TextOnAccent = Ink(highlightText),
                TextOnHover = Ink(highlightText),
                SubtleHover = highlight,
                SubtlePressed = highlight,
                Divider = text,
                CardBackground = window,
                CardStroke = text,
                ControlFill = window,
                ControlFillHover = highlight,
                ControlFillPressed = highlight,
                ControlStroke = text,
                ControlAltFill = window,
                Accent = highlight,
                AccentHover = highlight,
                AccentPressed = highlight,
                FocusOuter = text,
                FocusInner = window,
                ScrollIndicator = text,
                Ground = window,
                LayerAlt = window,
                CardBackgroundSecondary = window,
                ControlFillDisabled = window,
                ControlFillInputActive = window,
                ControlStrokeSecondary = text,
                AccentStroke = text,
                AccentStrokeBottom = text,
                ControlStrongStroke = text,
                ControlStrongStrokeDisabled = grayText,
                ControlAltFillHover = window,
                ControlAltFillPressed = window,
                AccentDisabled = grayText,
                TextOnAccentSecondary = Ink(highlightText),
                TextOnAccentDisabled = Ink(grayText),
                TextInverse = Ink(window),
                Critical = text,
                Caution = text,
                ToolTip = window,
                AccentText = hotLight,
                AccentTextHover = hotLight,
                AccentTextPressed = hotLight,
            };
        }
        if (look.Dark)
        {
            var surface = Color.FromArgb(255, 0x2C, 0x2C, 0x2C);
            var accent = Opaque(Pick(look.AccentLight2, look.Accent, Color.FromArgb(255, 0x99, 0xEB, 0xFF)));
            return new FlyoutTheme
            {
                Dark = true,
                HighContrast = false,
                Glass = look.Glass,
                Base = surface,
                Tint = WithAlpha(surface, 0.90f),
                Stroke = Color.FromArgb(0x33, 0, 0, 0),
                StrokeDip = 1,
                ShadowOpacity = 0.28f,
                Text = Gfx.Ink(1, 1, 1, 1),
                TextSecondary = Gfx.Ink(1, 1, 1, 0xC5 / 255f),
                TextTertiary = Gfx.Ink(1, 1, 1, 0x87 / 255f),
                TextDisabled = Gfx.Ink(1, 1, 1, 0x5D / 255f),
                TextOnAccent = Gfx.Ink(0, 0, 0, 1),
                TextOnHover = Gfx.Ink(1, 1, 1, 1),
                SubtleHover = Color.FromArgb(0x0F, 255, 255, 255),
                SubtlePressed = Color.FromArgb(0x0A, 255, 255, 255),
                Divider = Color.FromArgb(0x15, 255, 255, 255),
                CardBackground = Color.FromArgb(0x0D, 255, 255, 255),
                CardStroke = Color.FromArgb(0x19, 0, 0, 0),
                ControlFill = Color.FromArgb(0x0F, 255, 255, 255),
                ControlFillHover = Color.FromArgb(0x15, 255, 255, 255),
                ControlFillPressed = Color.FromArgb(0x08, 255, 255, 255),
                ControlStroke = Color.FromArgb(0x12, 255, 255, 255),
                ControlAltFill = Color.FromArgb(0x19, 0, 0, 0),
                Accent = accent,
                AccentHover = WithAlpha(accent, 0.9f),
                AccentPressed = WithAlpha(accent, 0.8f),
                FocusOuter = Color.FromArgb(255, 255, 255, 255),
                FocusInner = Color.FromArgb(0xB3, 0, 0, 0),
                ScrollIndicator = Color.FromArgb(0x8B, 255, 255, 255),
                Ground = Color.FromArgb(255, 0x20, 0x20, 0x20),
                LayerAlt = Color.FromArgb(0x0D, 255, 255, 255),
                CardBackgroundSecondary = Color.FromArgb(0x08, 255, 255, 255),
                ControlFillDisabled = Color.FromArgb(0x0B, 255, 255, 255),
                ControlFillInputActive = Color.FromArgb(0xB3, 0x1E, 0x1E, 0x1E),
                ControlStrokeSecondary = Color.FromArgb(0x18, 255, 255, 255),
                AccentStroke = Color.FromArgb(0x14, 255, 255, 255),
                AccentStrokeBottom = Color.FromArgb(0x23, 0, 0, 0),
                ControlStrongStroke = Color.FromArgb(0x8B, 255, 255, 255),
                ControlStrongStrokeDisabled = Color.FromArgb(0x28, 255, 255, 255),
                ControlAltFillHover = Color.FromArgb(0x0B, 255, 255, 255),
                ControlAltFillPressed = Color.FromArgb(0x12, 255, 255, 255),
                AccentDisabled = Color.FromArgb(0x28, 255, 255, 255),
                TextOnAccentSecondary = Gfx.Ink(0, 0, 0, 0x80 / 255f),
                TextOnAccentDisabled = Gfx.Ink(1, 1, 1, 0x87 / 255f),
                TextInverse = Gfx.Ink(0, 0, 0, 0xE4 / 255f),
                Critical = Color.FromArgb(255, 0xFF, 0x99, 0xA4),
                Caution = Color.FromArgb(255, 0xFC, 0xE1, 0x00),
                ToolTip = Color.FromArgb(255, 0x2B, 0x2B, 0x2B),
                AccentText = Opaque(Pick(look.AccentLight3, look.Accent, Color.FromArgb(255, 0x99, 0xEB, 0xFF))),
                AccentTextHover = Opaque(Pick(look.AccentLight3, look.Accent, Color.FromArgb(255, 0x99, 0xEB, 0xFF))),
                AccentTextPressed = accent,
            };
        }
        var light = Color.FromArgb(255, 0xF9, 0xF9, 0xF9);
        var accentLight = Opaque(Pick(look.AccentDark1, look.Accent, Color.FromArgb(255, 0x00, 0x5F, 0xB8)));
        return new FlyoutTheme
        {
            Dark = false,
            HighContrast = false,
            Glass = look.Glass,
            Base = light,
            Tint = WithAlpha(light, 0.85f),
            Stroke = Color.FromArgb(0x0F, 0, 0, 0),
            StrokeDip = 1,
            ShadowOpacity = 0.14f,
            Text = Gfx.Ink(0, 0, 0, 0xE4 / 255f),
            TextSecondary = Gfx.Ink(0, 0, 0, 0x9E / 255f),
            TextTertiary = Gfx.Ink(0, 0, 0, 0x72 / 255f),
            TextDisabled = Gfx.Ink(0, 0, 0, 0x5C / 255f),
            TextOnAccent = Gfx.Ink(1, 1, 1, 1),
            TextOnHover = Gfx.Ink(0, 0, 0, 0xE4 / 255f),
            SubtleHover = Color.FromArgb(0x09, 0, 0, 0),
            SubtlePressed = Color.FromArgb(0x06, 0, 0, 0),
            Divider = Color.FromArgb(0x0F, 0, 0, 0),
            CardBackground = Color.FromArgb(0xB3, 255, 255, 255),
            CardStroke = Color.FromArgb(0x0F, 0, 0, 0),
            ControlFill = Color.FromArgb(0xB3, 255, 255, 255),
            ControlFillHover = Color.FromArgb(0x80, 0xF9, 0xF9, 0xF9),
            ControlFillPressed = Color.FromArgb(0x4D, 0xF9, 0xF9, 0xF9),
            ControlStroke = Color.FromArgb(0x0F, 0, 0, 0),
            ControlAltFill = Color.FromArgb(0x06, 0, 0, 0),
            Accent = accentLight,
            AccentHover = WithAlpha(accentLight, 0.9f),
            AccentPressed = WithAlpha(accentLight, 0.8f),
            FocusOuter = Color.FromArgb(0xE4, 0, 0, 0),
            FocusInner = Color.FromArgb(0xB3, 255, 255, 255),
            ScrollIndicator = Color.FromArgb(0x72, 0, 0, 0),
            Ground = Color.FromArgb(255, 0xF3, 0xF3, 0xF3),
            LayerAlt = Color.FromArgb(255, 255, 255, 255),
            CardBackgroundSecondary = Color.FromArgb(0x80, 0xF6, 0xF6, 0xF6),
            ControlFillDisabled = Color.FromArgb(0x4D, 0xF9, 0xF9, 0xF9),
            ControlFillInputActive = Color.FromArgb(255, 255, 255, 255),
            ControlStrokeSecondary = Color.FromArgb(0x29, 0, 0, 0),
            AccentStroke = Color.FromArgb(0x14, 255, 255, 255),
            AccentStrokeBottom = Color.FromArgb(0x66, 0, 0, 0),
            ControlStrongStroke = Color.FromArgb(0x72, 0, 0, 0),
            ControlStrongStrokeDisabled = Color.FromArgb(0x37, 0, 0, 0),
            ControlAltFillHover = Color.FromArgb(0x0F, 0, 0, 0),
            ControlAltFillPressed = Color.FromArgb(0x18, 0, 0, 0),
            AccentDisabled = Color.FromArgb(0x37, 0, 0, 0),
            TextOnAccentSecondary = Gfx.Ink(1, 1, 1, 0xB3 / 255f),
            TextOnAccentDisabled = Gfx.Ink(1, 1, 1, 1),
            TextInverse = Gfx.Ink(1, 1, 1, 1),
            Critical = Color.FromArgb(255, 0xC4, 0x2B, 0x1C),
            Caution = Color.FromArgb(255, 0x9D, 0x5D, 0x00),
            ToolTip = Color.FromArgb(255, 0xF2, 0xF2, 0xF2),
            AccentText = Opaque(Pick(look.AccentDark2, look.Accent, Color.FromArgb(255, 0x00, 0x4C, 0x87))),
            AccentTextHover = Opaque(Pick(look.AccentDark3, look.Accent, Color.FromArgb(255, 0x00, 0x3E, 0x92))),
            AccentTextPressed = accentLight,
        };
    }

    // The accent shades are left out of older Looks: the base accent stands in,
    // and failing that Windows' default blue.
    private static Color Pick(Color shade, Color accent, Color fallback) =>
        shade.A != 0 ? shade : accent.A != 0 ? accent : fallback;

    // System colours come from COLORREFs, which carry no alpha.
    internal static Color Opaque(Color color) => Color.FromArgb(255, color.R, color.G, color.B);

    internal static Color WithAlpha(Color color, float alpha) =>
        Color.FromArgb((byte)MathF.Round(Math.Clamp(alpha, 0, 1) * 255, MidpointRounding.AwayFromZero), color.R, color.G, color.B);

    internal static DXGI_RGBA Ink(Color color) => Gfx.Ink(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
}

public sealed unsafe class FlyoutHost : IDisposable
{
    /// <summary>
    /// Posted to a Menu host by its mouse hook for a click outside it: wParam is the showing it
    /// ends, lParam 1 when the focus should go back.
    /// </summary>
    internal const uint WM_KARPO_HOST_DISMISS = WM.WM_USER + 0x6D;

    // Owners use small timer ids; the host's are above these.
    private const nuint HideTimer = 0xF001;

    // §446, §489: a popup's coming in should be over (Arrived).
    private const nuint MotionTimer = 0xF002;

    // §446: how long after its coming in should be over a panel is put where it ends, its animations let go of, in ms:
    // past a frame or two the commit may land late by under load, so a running animation is never cut off. Not a motion's
    // time: a guard's.
    private const int MotionGraceMs = 250;

    // The open Menu hosts, for the hook, which has no other way to find them — this thread's. Per
    // thread because a host's window belongs to the thread that made it: the hook (PressHook.cs)
    // posts each press to every thread with a menu open, and each acts for the hosts it opened.
    //
    // The dock has one UI thread and so one list, as before. A process with several — the
    // offscreen tests, whose classes run in parallel, each on a thread of its own — had one List
    // mutated from all of them. List<T>.Add publishes the new size before it writes the slot, and
    // OpenMenus reads the backing array raw with no version check, so another thread's Add left a
    // counted slot still null here: a NullReferenceException in OpenMenus's own predicate, an Add
    // lost outright, or a Remove at a stale index — and a slot lost during a resize stayed null
    // for the rest of the process, failing every later reader. A lock would have made the list
    // consistent and kept the wrong model: one hook for the process, which any thread's Hook()
    // took away from the thread that had put it in.
    [ThreadStatic]
    private static List<FlyoutHost>? showing;

    /// <summary>This thread's open Menu hosts; made on first use, as a thread-static has no initializer on any other thread.</summary>
    private static List<FlyoutHost> Showing => showing ??= [];

    [ThreadStatic]
    private static RECT[]? avoided;

    /// <summary>
    /// edit: what no popup opened from a bar may stand over, in screen px: every display's edit bar
    /// while edit mode is on (EditChrome), which stands under a bar along the top, right where a
    /// popup of that bar opens. Not a folder (<see cref="UnderEditBar"/>), which the edit bar stands
    /// over (§329). This thread's, as the rest of the hosts' state is.
    /// </summary>
    internal static IReadOnlyList<RECT> Avoided
    {
        get => avoided ?? [];
        set => avoided = [.. value];
    }

    /// <summary>
    /// edit: where a panel <paramref name="w"/> by <paramref name="h"/> px at
    /// (<paramref name="x"/>, <paramref name="top"/>), opened away from <paramref name="edge"/>,
    /// stands clear of <paramref name="avoid"/>: one that would stand over any of them moves on away
    /// from its bar, past it by <paramref name="apart"/> — under it from a bar along the top, over it
    /// from one along the bottom; from a bar down a side, under it, as the edit bar stands at the top
    /// of the display. One clear of all of them stays where it is: a popup beside the edit bar keeps
    /// its place by its bar. Its top.
    /// </summary>
    internal static int ClearOf(int x, int top, int w, int h, Edge edge, int apart, IReadOnlyList<RECT> avoid)
    {
        foreach (var r in avoid)
        {
            if (x < r.right && x + w > r.left && top < r.bottom && top + h > r.top)
            {
                top = edge == Edge.Bottom ? r.top - apart - h : r.bottom + apart;
            }
        }
        return top;
    }

    private readonly HINSTANCE instance;
    private readonly nint userData;
    private readonly string className;
    private DesktopWindowTarget? target;
    private Compositor? compositor;
    private bool open;
    private bool disposed;
    private bool tracking;
    private bool animate;

    private uint generation;
    private HWND previous;
    private Spot spot;
    private Area panel;

    /// <summary>The work area the popup was placed in, as its owner handed it over (screen px).</summary>
    internal RECT Work => spot.Placement.Work;

    /// <summary>
    /// Where the popup was placed, as <see cref="Show"/> was handed it; §651 on a ground flush with its bar and the work area's
    /// ends (<see cref="FlyoutPlacement.Flush"/>).
    /// </summary>
    internal FlyoutPlacement Placement => Grounded ? spot.Placement.Flush : spot.Placement;

    // §355: the window stands still on its display while it shows — its frame, the display less a px on
    // its far sides — and where the popup is in it, and how large, is composition: the box, which is
    // where the window itself used to stand, clipped as the window clipped it. The chrome is made once
    // for the window's life and dressed in place (Wear): a showing, a refresh, a look changed make no
    // material, no shadow and no rim again, which DWM showed a frame late — the see-through flash.
    //
    //   stage (the window's root, the frame's size)
    //     box (where the popup stands in the frame, clipped to it)
    //       root (what fades and slides as it comes and goes: Move, §489)
    //         under (each panel's shadow and plate), body (the content, clipped to the one panel's
    //         shape), over (each panel's rim), outside (what stands outside the panel)
    private RECT frame;
    private ContainerVisual? stage;
    private ContainerVisual? box;
    private ContainerVisual? root;
    private ContainerVisual? under;
    private ContainerVisual? body;
    private CompositionRoundedRectangleGeometry? bodyShape;
    private CompositionGeometricClip? bodyClip;
    private ContainerVisual? content;
    private ContainerVisual? over;
    private ContainerVisual? outside;

    // The window's chrome, closed with it.
    private readonly List<IDisposable> chrome = [];

    // Whether a showing is up, its content the owner's to draw into (Content), from Open until it is let go of.
    private bool shown;

    // Whether the panel is several (Pieces) as it shows: only a piece takes a press, and the content is the owner's to clip.
    private bool pieced;

    // The panels, each its own shadow, material and rim (PanelChrome): the one panel of a popup, or a
    // folder's pieces — made once each and moved after (PlacePanels), and where they stand from.
    private (float Left, float Top, float Blur, float Drop, float Away) panelsPlace;
    private readonly List<PanelChrome> panels = [];

    // §584: the one panel's beak as it was last placed (Arrange) — the side it stands out of, its tip along it and how far out,
    // in px —; none for a popup without one.
    private (Edge Side, float Tip, float Tall)? beak;

    /// <summary>Tests only: §584 the beak placed — its side, its tip along it from the panel's left and how far out it stands (px).</summary>
    internal (Edge Side, float Tip, float Tall)? BeakPlaced => beak;

    /// <summary>One panel's chrome — its shadow's caster (none without a shadow), its plate in the material and its rim — and what to close with them.</summary>
    private sealed class PanelChrome
    {
        public SpriteVisual? Caster;
        public ShapeVisual? CastShape;
        public CompositionRoundedRectangleGeometry? CastGeometry;
        public CompositionVisualSurface? CastPicture;
        public CompositionColorBrush? CastFill;
        public DropShadow? Shadow;
        public List<IDisposable> CastMade = [];
        public required SpriteVisual Plate;
        public required CompositionRoundedRectangleGeometry Outline;
        public Plating Wears;
        public CompositionColorBrush? Fill;
        public List<IDisposable> MaterialMade = [];
        public required ShapeVisual Rim;
        public required CompositionRoundedRectangleGeometry RimShape;
        public required CompositionSpriteShape RimLine;
        public required CompositionColorBrush RimBrush;
        public readonly List<IDisposable> Made = [];

        // §704: Liquid glass's light, where the theme has it: its glow and lens in a view of their own over the plate, the
        // rim's gradient on the rim's line, the glass they were made for, and what to close with them.
        public ShapeVisual? GlazeView;
        public Glaze? Glaze;
        public CompositionLinearGradientBrush? RimGradient;
        public Karpo.Core.GlassLook? GlazedIn;
        public List<IDisposable> GlassMade = [];

        // §584: the caster's shape (its geometry the rounded rectangle, or the outline with the beak).
        public CompositionSpriteShape? CastSprite;

        // §584: the plate's own clip, its rounded rectangle, worn again when the beak goes.
        public required CompositionClip RoundClip;

        // §584: the outline with the beak — the plate's clip, the rim's line and the caster's shape — made the first time a beak
        // is placed and kept, given a path afresh only where the beak or the panel changed (BeakMadeFor).
        public CompositionPathGeometry? BeakPlate;
        public CompositionGeometricClip? BeakClip;
        public CompositionPathGeometry? BeakRim;
        public CompositionPathGeometry? BeakCast;
        public (float W, float H, Edge Side, float Tip, float Tall, float Inset, float Stroke, float Radius)? BeakMadeFor;
        public readonly List<IDisposable> BeakMade = [];

        public void Close()
        {
            CloseAll(CastMade);
            CloseAll(MaterialMade);
            CloseAll(BeakMade);
            CloseAll(GlassMade);
            CloseAll(Made);
        }
    }

    /// <summary>What a plate is made of: the host backdrop under a tint, nothing (§464: a folder's, <see cref="FlyoutTheme.Bare"/>), or the solid base.</summary>
    private enum Plating
    {
        None,
        Glass,
        Bare,
        Solid,
    }

    /// <summary>
    /// Tests only: how many panels' chrome was made since the window was made — a piece more, or the
    /// window's first, and never a panel that only moved, changed size or changed its look.
    /// </summary>
    internal int PiecesMade { get; private set; }

    /// <summary>Tests only (§649): each panel's rim's colour now, in the order of its pieces: clear where none is drawn.</summary>
    internal IReadOnlyList<Color> RimsShown => [.. panels.Select(panel => panel.RimBrush.Color)];

    /// <summary>Tests only: how many materials were made since the window was made — a backdrop or a fill, for a panel made or one whose material changed kind.</summary>
    internal int MaterialsMade { get; private set; }

    /// <summary>Tests only: how many times the window itself was placed — its frame, once per display it shows on.</summary>
    internal int FramesPlaced { get; private set; }

    /// <summary>Tests only: the window's frame on the screen (§355).</summary>
    internal RECT Frame => frame;

    /// <summary>Where the popup stands on the screen — the panel and the room round it, what its window was before §355: its region.</summary>
    internal RECT BoxOnScreen { get; private set; }

    // Every composition object of the showing, the owner's; closed when it ends rather than whenever
    // the GC gets to it (see Preview).
    private List<IDisposable> made = [];

    public FlyoutHost(Gfx gfx, HINSTANCE instance, nint dock, FlyoutMode mode)
        : this(gfx, instance, dock, mode, Flyout.ClassName)
    {
    }

    /// <summary>A host whose window has a class (and window procedure) of its caller's own.</summary>
    internal FlyoutHost(Gfx gfx, HINSTANCE instance, nint userData, FlyoutMode mode, string className)
    {
        this.instance = instance;
        this.userData = userData;
        this.className = className;
        Mode = mode;
        EnsureWindow(gfx);
    }

    /// <summary>The window. A new one if Alt+F4 destroyed the last, so read it afresh.</summary>
    public HWND Hwnd { get; private set; }

    public FlyoutMode Mode { get; }

    public bool IsOpen => open;

    /// <summary>The pointer is over the window.</summary>
    public bool Inside { get; private set; }

    public Look Look { get; private set; }

    public float Scale { get; private set; } = 1;

    public FlyoutTheme Theme { get; private set; } = FlyoutTheme.For(default);

    public Compositor Compositor => compositor ?? throw new ObjectDisposedException(nameof(FlyoutHost));

    /// <summary>The panel's content: (0, 0) is its top-left corner, and it is clipped to the panel's rounded shape.</summary>
    public ContainerVisual Content => shown && content is not null ? content : throw new InvalidOperationException("The popup is not shown.");

    /// <summary>
    /// folders: what stands outside the panel, over everything and unclipped, in the room the window
    /// keeps round it (<see cref="OutsideDip"/>): (0, 0) is the panel's top-left corner, as it is for
    /// <see cref="Content"/>, so a thing beside the panel has a negative place or one past its size.
    /// </summary>
    public ContainerVisual Outside => shown && outside is not null ? outside : throw new InvalidOperationException("The popup is not shown.");

    /// <summary>
    /// folders: room the box (<see cref="BoxOnScreen"/>) keeps round its panel on every side but the bar's, in DIPs, where it
    /// needs more than the shadow's (<see cref="RoomDip"/>) — an arranged folder's "+" buttons
    /// outside it. Read when the panel is placed (a showing, <see cref="Resize"/>): the panel stands
    /// where it would anyway, only the box round it grows.
    /// </summary>
    public float OutsideDip { get; set; }

    /// <summary>
    /// folders: the display (screen px) whose edges the box (<see cref="BoxOnScreen"/>) reaches on every side, where it is
    /// placed beside an anchor — under the bars too, which stand in front of it (§279): an arranged
    /// folder's guide lines run down the screen to the bars' edges (§287) and its empty slots out to
    /// the work area's edges (FolderFlyout.Slots, §246). Null: the box is the panel's and its shadow's. The panel stands where it would anyway; the owner lets a press on the room
    /// outside its own things through (FolderFlyout.PassesThrough).
    /// </summary>
    internal RECT? Spread { get; set; }

    /// <summary>
    /// folders, §647, §651: an attached folder's ground, the colour of its bar (FolderFlyout.AttachedGround), under its panels:
    /// the bar grown out, along the bar the work area's whole length, edge to edge, across it from the bar's inner side to the
    /// panel's far side (<see cref="GroundOf"/>); the panel on it flush with the bar (<see cref="FlyoutPlacement.Flush"/>). Its
    /// panels cast no shadow on it, a press on it is a press on the folder (<see cref="Contains"/>), and it comes out of the
    /// bar and goes back into it as a drawer does (<see cref="Move"/>). Null: none — a folder of cards, every other popup. Read
    /// when the panel is placed (a showing, <see cref="Resize"/>).
    /// </summary>
    internal Color? Ground { get; set; }

    /// <summary>§647: where the ground stands now, in panel px (it reaches past the panel); empty where there is none.</summary>
    internal D2D_RECT_F GroundBox { get; private set; }

    /// <summary>
    /// §651: where the folder's columns meet (FolderPlan.Seams, panel px across): a line of <see cref="Rim"/> px drawn in each,
    /// across the ground's whole depth, from the bar to its far side — the columns parted by a line, not by air.
    /// </summary>
    internal IReadOnlyList<(float Left, float Right)> Seams { get; set; } = [];

    /// <summary>
    /// §651: what a see-through ground stands on, as its bar stands on it: the wallpaper Karpo blurs under the bar, for the
    /// ground's rectangle on the screen (the dock's, DockWindow.Paper); null where it has none (yet): the ground is then
    /// drawn solid. Asked as the ground is placed and again on <see cref="Repaper"/>.
    /// </summary>
    internal Func<RECT, CompositionBrush?>? Paper { get; set; }

    /// <summary>
    /// §647: whether the panel stands on a ground: a folder attached to its bar, placed beside its anchor. §649 its panels then
    /// draw no rim and keep no room for one: the folder's lines stand flat on the ground (FolderFlyout.Flat).
    /// </summary>
    internal bool Grounded => Ground is not null && !spot.Beside && spot.Placement.Control is null;

    /// <summary>
    /// §647, §651: the ground of a <paramref name="w"/> by <paramref name="h"/> px panel at <paramref name="x"/>,
    /// <paramref name="top"/> (screen px) beside a bar along <paramref name="edge"/> of <paramref name="work"/>, in panel px: along
    /// the bar the work area's whole length, end to end; across it from the bar's inner side — the work area's edge there — to
    /// the panel's far side. Never past the work area. Pure.
    /// </summary>
    internal static D2D_RECT_F GroundOf(Edge edge, RECT work, int x, int top, int w, int h)
    {
        var (left, right) = (work.left - x, work.right - x);
        var (upper, lower) = (work.top - top, work.bottom - top);
        return edge switch
        {
            Edge.Top => new D2D_RECT_F { left = left, top = upper, right = right, bottom = MathF.Min(h, lower) },
            Edge.Left => new D2D_RECT_F { left = left, top = upper, right = MathF.Min(w, right), bottom = lower },
            Edge.Right => new D2D_RECT_F { left = MathF.Max(0, left), top = upper, right = right, bottom = lower },
            _ => new D2D_RECT_F { left = left, top = MathF.Max(0, upper), right = right, bottom = lower },
        };
    }

    /// <summary>
    /// §647, §651: the ground's visuals, made the first time a folder stands on one, kept for the window's life: one clipped
    /// holder of the wallpaper under it (its paper, held still on the screen while the drawer slides), its colour and the seams
    /// between its columns; the line along its far side over the lines (in <see cref="over"/>).
    /// </summary>
    private ContainerVisual? groundView;

    private SpriteVisual? groundPaper;
    private SpriteVisual? groundTint;
    private CompositionColorBrush? groundFill;
    private SpriteVisual? groundEdge;
    private CompositionColorBrush? groundEdgeInk;
    private CompositionColorBrush? groundSeamInk;
    private readonly List<SpriteVisual> groundSeams = [];

    /// <summary>§651: the ground's rectangle on the screen as it was last placed, which its paper is asked for.</summary>
    private RECT groundOnScreen;

    /// <summary>§651: whether the ground stands on its paper now (the wallpaper under its colour), else solid.</summary>
    private bool groundPapered;

    /// <summary>§647: whether the panels were last dressed for a ground (no shadow), so a style changed in place dresses them again.</summary>
    private bool dressedGrounded;

    /// <summary>
    /// §647: the ground where <see cref="GroundBox"/> has it, the panel's top-left corner at <paramref name="left"/>,
    /// <paramref name="top"/> of the box and at <paramref name="screen"/> on the screen, under every panel; hidden where there
    /// is none. §651 on its paper where it is see-through and has one, its seams and its far side's line with it.
    /// </summary>
    private void PlaceGround(float left, float top, POINT screen)
    {
        if (compositor is not { } c || under is null)
        {
            return;
        }
        if (Grounded != dressedGrounded)
        {
            Wear(c);
        }
        if (!Grounded || Ground is null)
        {
            if (groundView is not null)
            {
                (groundView.IsVisible, groundEdge!.IsVisible) = (false, false);
            }
            return;
        }
        if (groundView is null)
        {
            groundView = Chromed(c.CreateContainerVisual());
            // The paper is held still against the drawer's slide (below): what of it would show past the ground does not.
            groundView.Clip = Chromed(c.CreateInsetClip());
            groundPaper = Chromed(c.CreateSpriteVisual());
            var still = Chromed(c.CreateExpressionAnimation("-shown.Offset"));
            still.SetReferenceParameter("shown", root!);
            groundPaper.StartAnimation("Offset", still);
            groundView.Children.InsertAtTop(groundPaper);
            groundFill = Chromed(c.CreateColorBrush());
            groundTint = Chromed(c.CreateSpriteVisual());
            groundTint.Brush = groundFill;
            groundTint.RelativeSizeAdjustment = Vector2.One;
            groundView.Children.InsertAtTop(groundTint);
            groundSeamInk = Chromed(c.CreateColorBrush());
            groundEdgeInk = Chromed(c.CreateColorBrush());
            groundEdge = Chromed(c.CreateSpriteVisual());
            groundEdge.Brush = groundEdgeInk;
            // Over the lines: one lit at the far side lights up to the line, not over it.
            over!.Children.InsertAtBottom(groundEdge);
            under.Children.InsertAtBottom(groundView);
        }
        var g = GroundBox;
        var size = new Vector2(MathF.Max(0, g.right - g.left), MathF.Max(0, g.bottom - g.top));
        groundView.Offset = new Vector3(left + g.left, top + g.top, 0);
        groundView.Size = size;
        groundOnScreen = new RECT { left = screen.x + (int)g.left, top = screen.y + (int)g.top, right = screen.x + (int)g.right, bottom = screen.y + (int)g.bottom };
        groundPaper!.Size = size;
        // Its line along the side away from the bar, the folder's outline's weight; the bar's side is the bar.
        var rim = Rim;
        var (edgeAt, edgeSize) = spot.Placement.Edge switch
        {
            Edge.Top => (new Vector2(0, size.Y - rim), new Vector2(size.X, rim)),
            Edge.Left => (new Vector2(size.X - rim, 0), new Vector2(rim, size.Y)),
            Edge.Right => (Vector2.Zero, new Vector2(rim, size.Y)),
            _ => (Vector2.Zero, new Vector2(size.X, rim)),
        };
        groundEdge!.Offset = new Vector3(left + g.left + edgeAt.X, top + g.top + edgeAt.Y, 0);
        groundEdge.Size = edgeSize;
        groundEdge.IsVisible = true;
        // A seam across the ground's whole depth wherever two columns meet.
        while (groundSeams.Count < Seams.Count)
        {
            var seam = Chromed(c.CreateSpriteVisual());
            seam.Brush = groundSeamInk;
            groundView.Children.InsertAtTop(seam);
            groundSeams.Add(seam);
        }
        for (var at = 0; at < groundSeams.Count; at++)
        {
            var seam = groundSeams[at];
            seam.IsVisible = at < Seams.Count;
            if (seam.IsVisible)
            {
                seam.Offset = new Vector3(Seams[at].Left - g.left, 0, 0);
                seam.Size = new Vector2(Seams[at].Right - Seams[at].Left, size.Y);
            }
        }
        groundView.IsVisible = true;
        Repaper();
    }

    /// <summary>
    /// §651: the ground's paper asked for again (<see cref="Paper"/>): the wallpaper composed for it since, or changed. Where
    /// the ground is see-through and has its paper it stands on it in its colour as said; else its colour solid.
    /// </summary>
    internal void Repaper()
    {
        if (groundView is not { IsVisible: true } || groundPaper is null || Ground is not { } color)
        {
            return;
        }
        var paper = color.A < 255 && Paper is { } ask ? ask(groundOnScreen) : null;
        groundPaper.Brush = paper;
        groundPaper.IsVisible = paper is not null;
        groundPapered = paper is not null;
        DressGround();
    }

    /// <summary>
    /// §651: the ground's colours as the theme and its colour are now: its colour — as said on its paper, solid without one —,
    /// the line along its far side the folder's outline (none where that is None, as the bar has none), its seams the
    /// dividers' ink (FolderFlyout.RuleInk).
    /// </summary>
    private void DressGround()
    {
        if (groundFill is null || Ground is not { } color)
        {
            return;
        }
        groundFill.Color = groundPapered ? color : FlyoutTheme.Opaque(color);
        groundEdgeInk!.Color = Theme.Stroke;
        groundSeamInk!.Color = FolderFlyout.RuleInk(Theme);
    }

    /// <summary>tests, §651: the ground's colour as drawn, whether it stands on its paper, its far side's line's ink and its seams'.</summary>
    internal (Color Fill, bool Papered, Color Edge, Color Seam)? GroundDressed =>
        groundView is { IsVisible: true } && groundFill is not null ? (groundFill.Color, groundPapered, groundEdgeInk!.Color, groundSeamInk!.Color) : null;

    /// <summary>
    /// A taskbar flyout's surface (<see cref="FlyoutTheme.Taskbar"/>) rather than a menu's: a folder, quick settings, §514 a
    /// widget's flyout (a menu's host shows one now and a menu the next: <see cref="Flyout.Taskbar"/>). Read when the panel
    /// is shown.
    /// </summary>
    internal bool Taskbar { get; set; }

    /// <summary>
    /// §489: what this popup is, which says how it comes in and goes (<see cref="PopupMotion.Of"/>): a popup's rise, a tooltip's
    /// fade, the edit bar's larger rise.
    /// </summary>
    internal PopupEntrance Entrance { get; init; }

    /// <summary>
    /// §446, §489: how the panel comes in as it shows where it was shut, and goes as it closes, for this showing: the owner's
    /// plan (a folder's: at once in a switch, a fade where it stands arranged, none for a picture). Null: its
    /// <see cref="Entrance"/>'s, by the look it shows in (<see cref="Look.Motion"/>). Read as it shows and as it closes.
    /// </summary>
    internal PopupMotion? Moves { get; set; }

    /// <summary>§489: the plan this showing comes in and goes by: the owner's, or its entrance's under its look's motion.</summary>
    private PopupMotion Plan => Moves ?? PopupMotion.Of(animate ? Look.Motion : Motion.Still, Entrance);

    /// <summary>§489: whether this showing set off coming in (the edit bar counts its entrances by it).</summary>
    internal bool Entered { get; private set; }

    /// <summary>§334: a folder's panel, in the folders' own look (<see cref="FlyoutTheme.Folder"/>) over a taskbar flyout's surface.</summary>
    internal bool Folder { get; init; }

    /// <summary>
    /// edit: a folder, which takes the full height whether the edit bar is there or not and which the
    /// edit bar stands over (the person, 2026-10-23: "show full height, in edit mode just display the
    /// edit bar over the folder"; §329): never moved off it (<see cref="Avoided"/>).
    /// </summary>
    internal bool UnderEditBar { get; init; }

    /// <summary>The rim's width, in whole px, as Windows' (2 px at 150 %, 1 at 125 %).</summary>
    internal float Rim => MathF.Max(Px(Theme.StrokeDip), 1);

    /// <summary>
    /// How far the material and the content stand in from the panel's edge: the rim's width where
    /// the rim lies outside them (<see cref="FlyoutTheme.RimOutside"/>), else none; §649 none on a ground, which draws no rim.
    /// </summary>
    internal float RimInset => Theme.RimOutside && !Grounded ? Rim : 0;

    /// <summary>
    /// folders: the part of the panel, in panel px, placed beside the anchor as a whole panel would
    /// be — centred on it, against an end, kept inside the work area — the rest of the panel hanging
    /// off its sides wherever that puts it — past a screen's edge, or a side bar's inner side, under
    /// the bar (§303): an arranged folder's empty lists outside the ones with widgets
    /// (FolderPlan.Core), so those stand where they stand at rest. None: the whole panel. Read when
    /// the panel is placed.
    /// </summary>
    internal D2D_RECT_F? Core { get; set; }

    /// <summary>
    /// What the popup is for the z-order plan (§359, <see cref="ZPlan"/>): a menu's stratum for a Menu host, a
    /// popup's for the others, unless its owner says (the edit bars, the tooltips, a submenu). Spread — an
    /// arranged folder — it stands at <see cref="Stratum.ArrangedFolder"/>: over its display's dim, under the
    /// bars and the edit bars (§279, §329; the person, 2026-10-12: "the overlay that shows the individual
    /// lines is infront of the configuration window"). Its window is clear while out of sight, so it comes
    /// into sight with its content in one commit (<see cref="WindowTable"/>).
    /// </summary>
    internal Stratum Stratum
    {
        get => stratum ?? (Mode == FlyoutMode.Menu ? Stratum.Menu : Stratum.Popup);
        init => stratum = value;
    }

    private readonly Stratum? stratum;

    /// <summary>Where the window stands in the plan now: an arranged folder at its own stratum, else its own.</summary>
    private Stratum Standing => Spread is not null ? Stratum.ArrangedFolder : Stratum;

    /// <summary>
    /// folders: the panel as several panels, each its own material, rounded corners, rim and
    /// shadow — a folder's lists, side by side and apart — in panel px, the panel being the box
    /// round all of them; null for the one panel every other popup is, none for no panel at all (an
    /// arranged folder whose lists are all empty). Read when a showing opens
    /// (which way the chrome is made) and whenever the panel is placed (<see cref="Resize"/>).
    /// <see cref="Content"/> is then not clipped: the owner clips what it draws to each piece. Only
    /// a piece is the panel for a press: one in the room between them, or beside a shorter one, is
    /// outside (<see cref="Contains"/>), though <see cref="ToPanel(LPARAM, out Vector2)"/> still
    /// answers for the whole box.
    /// </summary>
    internal IReadOnlyList<D2D_RECT_F>? Pieces { get; set; }

    /// <summary>The room the window keeps round the panel now, on each side, in px.</summary>
    internal (float Left, float Top, float Right, float Bottom) Room { get; private set; }

    /// <summary>
    /// preview: the whole tree — shadow, panel, rim and content — for an offscreen test to render
    /// with Windows.Graphics.Capture (GraphicsCaptureItem.CreateFromVisual), which is the only way
    /// to see what a composition popup draws without showing it to anyone.
    /// </summary>
    internal Visual? Captured => shown ? root : null;

    /// <summary>
    /// §629: the whole window's tree (its target's root, window px) and the window's top-left corner on the screen, for the add
    /// page to show a ghost in it through itself (SeeThroughView); null while it shows nothing.
    /// </summary>
    internal (Visual Tree, POINT Origin)? Whole => shown && stage is not null ? (stage, Origin) : null;

    /// <summary>preview: the panel's top-left corner in the window's client px (the shadow's room around it).</summary>
    internal Vector2 PanelAt => new(panel.X, panel.Y);

    public Vector2 PanelSize => new(panel.W, panel.H);

    public RECT PanelOnScreen => new()
    {
        left = Origin.x + (int)panel.X,
        top = Origin.y + (int)panel.Y,
        right = Origin.x + (int)(panel.X + panel.W),
        bottom = Origin.y + (int)(panel.Y + panel.H),
    };

    /// <summary>The window's top-left corner on screen.</summary>
    internal POINT Origin { get; private set; }

    // Where the window was last put on the screen, and how large: x, y, width, height.
    private (int X, int Y, int W, int H) windowPlaced;

    /// <summary>Tests only: how many times the popup was put somewhere — shown, or its box moved or sized (§355: in composition) — since its window was made.</summary>
    internal int WindowMoves { get; private set; }

    /// <summary>The panel, in window px.</summary>
    internal Area Panel => panel;

    /// <summary>Counts showings, so a late post can tell it belongs to an earlier one.</summary>
    internal uint Generation => generation;

    /// <summary>Whether this showing animates.</summary>
    internal bool Animate => animate;

    /// <summary>More that counts as inside for the hook: a menu's open submenus.</summary>
    internal Func<POINT, bool>? AlsoInside { get; set; }

    /// <summary>
    /// The windows that close the open popups themselves when a button goes down on them: the
    /// dock's bars (<see cref="Dock"/> sets it). The hook leaves their presses alone, so the bar
    /// finds the popup still open and can tell what its press closed (<see cref="LightDismiss"/>).
    /// <para>
    /// This thread's, as the open menus are (see <c>showing</c>): what the hook heard is asked
    /// about on the thread whose menu is open, and the dock sets it on its own UI thread, so the dock
    /// sees exactly what it always did. It was one static for the process, and a process with
    /// several UI threads — the offscreen tests, each class on a thread of its own — had one test's
    /// temporary answer ("this window is the bar") read by another thread's hook for as long as it
    /// stood, which is the same hazard the open menus had.
    /// </para>
    /// </summary>
    internal static Func<HWND, bool>? PressesHandledBy
    {
        get => pressesHandledBy;
        set => pressesHandledBy = value;
    }

    [ThreadStatic]
    private static Func<HWND, bool>? pressesHandledBy;

    /// <summary>
    /// search: what runs as a Menu popup opens — a flyout, a menu, a folder, the search box, any of
    /// Karpo's that a click or a key opens — with the window in front as it does: the dock closes
    /// Windows Search and Windows' Start there (<see cref="WindowsPanels.CloseFor"/>), so every
    /// flyout does it on its way in, from this one place. True when that window was one of them,
    /// and the focus then doesn't go back to it when the popup closes. Not for a popup that is
    /// open already and only changes, nor for the ones that never take the focus (a hover's, the
    /// edit strip). This thread's, as <see cref="PressesHandledBy"/> is.
    /// </summary>
    internal static Func<HWND, bool>? Opening
    {
        get => opening;
        set => opening = value;
    }

    [ThreadStatic]
    private static Func<HWND, bool>? opening;

    /// <summary>This thread's open Menu-mode popups: what a press on a bar closes.</summary>
    internal static FlyoutHost[] OpenMenus => showing is null ? [] : [.. showing.Where(host => host.open)];

    /// <summary>A menu of this thread's is showing, so the presses hook is in for it (PressHook.Also).</summary>
    internal static bool Listening => showing is { Count: > 0 };

    /// <summary>
    /// (Re)opens with the chrome of <paramref name="look"/>: a <paramref name="panelSize"/> px panel
    /// placed at <paramref name="placement"/>. <see cref="Content"/> starts empty each time; add the
    /// content after the call. A Menu host takes the focus. <paramref name="at"/>: the panel's top-left
    /// corner kept where it stands on the screen (<see cref="Held"/>), a menu shown again in place.
    /// </summary>
    /// <remarks>
    /// §356: <paramref name="keep"/>, the open popup shown again (a menu's Refresh, a level in place): its
    /// content stays — the owner reconciles it in place — and the window stands as it stands, neither
    /// shown nor raised nor focused again. Nothing for a popup not open.
    /// </remarks>
    public void Show(Gfx gfx, Look look, float scale, Vector2 panelSize, FlyoutPlacement placement, POINT? at = null, bool keep = false) =>
        Open(gfx, look, scale, panelSize, new Spot(placement, default, 0, Beside: false, Leftward: false, at), keep);

    /// <summary>
    /// As <see cref="Show"/>, beside <paramref name="item"/> (screen px) with the panel's first row
    /// level with <paramref name="rowTop"/> after <paramref name="lead"/> px: a submenu's place. To
    /// the right where there is room, else to the left; <paramref name="leftward"/> asks for the left
    /// first, as the submenu of a submenu that opened to the left goes on the way it went
    /// (<see cref="BesideX"/>), and <see cref="BesideLeft"/> says which side it took.
    /// </summary>
    internal void ShowBeside(Gfx gfx, Look look, float scale, Vector2 panelSize, RECT item, int rowTop, float lead, RECT work, bool leftward = false, POINT? at = null, bool keep = false) =>
        Open(gfx, look, scale, panelSize, new Spot(new FlyoutPlacement(default, work, default), item, rowTop - (int)lead, Beside: true, leftward, at), keep);

    /// <summary>A panel shown beside an item (<see cref="ShowBeside"/>) stands to its left: there was no room on the right, or the left was asked for.</summary>
    internal bool BesideLeft { get; private set; }

    /// <summary>
    /// Where across the screen a <paramref name="width"/> px panel beside <paramref name="item"/>
    /// starts, overlapping it by <paramref name="overlap"/> px, inside <paramref name="work"/> less
    /// <paramref name="keep"/>, and whether that is to its left. To the right where it fits, else to
    /// the left — or, <paramref name="leftward"/>, to the left where it fits, else to the right: once a
    /// cascade has turned left it goes on to the left, as Windows' menus do, rather than coming back
    /// over the lists it has opened. Where neither side fits it is the side asked for, pushed inside.
    /// </summary>
    internal static (int X, bool Left) BesideX(RECT item, int width, RECT work, int keep, int overlap, bool leftward)
    {
        var right = item.right - overlap;
        var left = item.left + overlap - width;
        var fitsRight = right + width <= work.right - keep;
        var fitsLeft = left >= work.left + keep;
        var toLeft = leftward ? fitsLeft || !fitsRight : !fitsRight;
        return (AlongEdge(toLeft ? left : right, work.left, work.right, width, keep), toLeft);
    }

    /// <summary>
    /// A new panel size for the open popup, at the same place; the content stays. As it is asked,
    /// at once, whatever the pointer is doing: a folder, which works its own size out
    /// (FolderFlyout.FolderChanged). A flyout whose content changed calls <see cref="Fit"/>.
    /// §385: <paramref name="align"/>, where along the edge it stands from now on (a folder's alignment
    /// changed while it stands open); the one it has where none is given.
    /// </summary>
    public void Resize(Vector2 panelSize, FlyoutAlign? align = null)
    {
        if (!open || root is null)
        {
            return;
        }
        if (align is { } along && !spot.Beside)
        {
            spot = spot with { Placement = spot.Placement with { Align = along } };
        }
        Arrange(panelSize, activate: false);
    }

    /// <summary>
    /// The open popup moved to hang off <paramref name="anchor"/> instead, its size and content kept:
    /// tray: the overflow over a chevron that a drag's growing or shrinking tray has moved (Dock.Tray.cs).
    /// </summary>
    public void Reanchor(POINT anchor)
    {
        if (!open || root is null || spot.Beside || (spot.Placement.Anchor.x == anchor.x && spot.Placement.Anchor.y == anchor.y))
        {
            return;
        }
        spot = spot with { Placement = spot.Placement with { Anchor = anchor } };
        Arrange(PanelSize, activate: false);
    }

    /// <summary>
    /// The room the window keeps around its panel for the shadow, in DIPs, on every side but the
    /// bar's. It is part of the box, and it takes the pointer like the rest of it (the window's region): nothing here
    /// answers a hit test to let a press in the room through.
    /// </summary>
    internal const float RoomDip = 24;

    /// <summary>
    /// §580: <paramref name="room"/> round a <paramref name="w"/> by <paramref name="h"/> panel at <paramref name="x"/>,
    /// <paramref name="top"/> (screen px), each side cut to what is left of <paramref name="work"/> on it — none where the
    /// panel reaches past it — so the box, which takes the pointer, never stands over a bar beside the room. Pure.
    /// </summary>
    internal static (float Left, float Top, float Right, float Bottom) RoomInside((float Left, float Top, float Right, float Bottom) room, int x, int top, int w, int h, RECT work) => (
        MathF.Min(room.Left, Math.Max(0, x - work.left)),
        MathF.Min(room.Top, Math.Max(0, top - work.top)),
        MathF.Min(room.Right, Math.Max(0, work.right - (x + w))),
        MathF.Min(room.Bottom, Math.Max(0, work.bottom - (top + h))));

    /// <summary>The open window to the front of its stratum (§359): a look over another popup, a folder come to rest.</summary>
    internal void Front()
    {
        if (open && Hwnd != HWND.NULL)
        {
            Enlist();
            Surfaces.Front(Hwnd);
        }
    }

    /// <summary>What the window is for the plan now (§359): its stratum, clear while out of sight.</summary>
    private void Enlist()
    {
        if (Hwnd != HWND.NULL)
        {
            Surfaces.Enlist(Hwnd, Standing, clear: true);
        }
    }

    /// <summary>The keys to the open window, as it takes them when it opens (a folder kept open as edit mode ends, §348).</summary>
    internal void Focus()
    {
        if (open && Hwnd != HWND.NULL)
        {
            Surfaces.Focus(Hwnd);
        }
    }

    /// <summary>
    /// The open panel sized for content that changed while it stands, <paramref name="size"/> being
    /// what the content asks for now (laid out against <see cref="FlyoutPlacement.MostHeight(float)"/>, so
    /// past the most its own overflow has already taken over). Returns the size the panel has.
    /// <para>
    /// Growing and shrinking both happen at once, whether or not the pointer is on the panel. A
    /// shrink used to wait for the pointer to leave, so that nothing moved under it; the person
    /// asked for the layout to follow at once whatever the pointer is doing ("not holding back when
    /// i am there with my mouse"). A panel hangs off its bar and is laid out from its far edge, so a
    /// shrink moves what is in it under the pointer; what keeps a click honest is the owner's,
    /// which resolves a press and its release to the same thing by what it is (the notification,
    /// the calendar's day, the folder's widget, the quick settings' control), never by where it
    /// stood.
    /// </para>
    /// <para>
    /// The answer is the size the panel has, in whole px, which the owner fills: a fraction of a
    /// px more than it asked, at most.
    /// </para>
    /// <para>
    /// Never a cut: the host gives what is asked and never less. An owner whose fixed parts alone
    /// are taller than the most — the calendar at the largest text size on a small screen — is as
    /// tall as those parts, as it always was.
    /// </para>
    /// </summary>
    public Vector2 Fit(Vector2 size)
    {
        if (!open || root is null)
        {
            return size;
        }
        var next = new Vector2(MathF.Ceiling(size.X), MathF.Ceiling(size.Y));
        if (next != PanelSize)
        {
            Arrange(next, activate: false);
        }
        return PanelSize;
    }

    public void Hide() => Close(restore: true);


    /// <summary>The window in front when a Menu-mode popup opened, which closing it gives the focus back to.</summary>
    internal HWND Previous => previous;

    /// <summary>
    /// What a message to the window means for the popup. Owners call it first for every message of
    /// <see cref="Hwnd"/>, then handle their content.
    /// </summary>
    public FlyoutHostEvent Message(uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM.WM_MOUSEMOVE:
                if (!Inside)
                {
                    Inside = true;
                    Track();
                    return new FlyoutHostEvent(FlyoutHostEventKind.Entered);
                }
                if (!tracking)
                {
                    Track();
                }
                return default;
            case WM.WM_MOUSELEAVE:
                tracking = false;
                if (Inside)
                {
                    Inside = false;
                    return new FlyoutHostEvent(FlyoutHostEventKind.Left);
                }
                return default;
            case WM.WM_LBUTTONDOWN:
            case WM.WM_RBUTTONDOWN:
            case WM.WM_MBUTTONDOWN:
                // The shadow's margin is outside as far as anyone can see.
                return open && Mode == FlyoutMode.Menu && !Holds && !OnPanel(X(lParam), Y(lParam))
                    ? Dismiss(restore: true, OnScreen(X(lParam), Y(lParam)))
                    : default;
            case WM.WM_KEYDOWN:
            {
                // Escape, and the Windows key, as Windows' own menus close for it.
                int key = (int)(nuint)wParam;
                return open && Mode == FlyoutMode.Menu && LightDismiss.ClosesOnKey(key)
                    ? Dismiss(restore: LightDismiss.RestoresFocus(key))
                    : default;
            }
            case WM.WM_TIMER:
                if ((nuint)wParam == HideTimer)
                {
                    _ = KillTimer(Hwnd, HideTimer);
                    if (!open)
                    {
                        HideNow();
                    }
                }
                else if ((nuint)wParam == MotionTimer)
                {
                    Arrived();
                }
                return default;
            case WM.WM_ACTIVATE:
                // Another window took the focus: it keeps it.
                return open && Mode == FlyoutMode.Menu && !Holds && ((nuint)wParam & 0xFFFF) == WA_INACTIVE
                    ? Dismiss(restore: false)
                    : default;
            case WM.WM_CANCELMODE:
                return open && Mode == FlyoutMode.Menu && !Holds ? Dismiss(restore: false) : default;
            case WM.WM_DISPLAYCHANGE:
            case WM.WM_DPICHANGED:
                if (!open)
                {
                    return default;
                }
                // A tool stays: its owner shows it again at the new scale.
                return Mode == FlyoutMode.Tool
                    ? new FlyoutHostEvent(FlyoutHostEventKind.DpiChanged)
                    : Dismiss(restore: false);
            case WM.WM_CLOSE:
                // Alt+F4, or another program's close: it closes as Escape closes it. Its window
                // stays for the next showing; the close goes no further (Closing.Default).
                return open ? Dismiss(restore: true) : default;
            case WM.WM_DESTROY:
            {
                bool was = open;
                Forget();
                return was ? new FlyoutHostEvent(FlyoutHostEventKind.Dismissed) : default;
            }
            case WM_KARPO_HOST_DISMISS:
                // A press elsewhere, heard by the hook or by the bar it landed on: where the pointer
                // was when it was sent is where the press was.
                return open && !Holds && (uint)(nuint)wParam == generation
                    ? Dismiss(restore: (nint)lParam != 0, PressPoint())
                    : default;
            default:
                return default;
        }
    }

    /// <summary>
    /// folders: while it is set, an outside press or another window taking the focus leaves the
    /// popup open — a folder being edited stays open through the drags on the bar itself. Escape,
    /// Alt+F4 and its owner's Hide still close it.
    /// </summary>
    public bool StaysOpen { get; set; }

    /// <summary>
    /// menus: a menu over a folder: a press outside it (and <see cref="AlsoKeeps"/>) is used up by
    /// the hook to close it, and nothing under it hears of it — the folder, a bar, another app
    /// (PressHook.UseUp). Set before it shows; read as it shows.
    /// </summary>
    internal bool UsesUpPresses { get; set; }

    /// <summary>The windows that are part of a menu that uses up presses besides its own: its submenus'.</summary>
    internal Func<IEnumerable<HWND>>? AlsoKeeps { get; set; }

    /// <summary>
    /// The hook told what of this open menu uses up presses (<see cref="UsesUpPresses"/>): as it
    /// shows, and again as a submenu of it opens, whose window may be new.
    /// </summary>
    internal void Keeps()
    {
        if (open && Mode == FlyoutMode.Menu && UsesUpPresses && Hwnd != HWND.NULL)
        {
            PressHook.UseUp(this, [Hwnd, .. AlsoKeeps?.Invoke() ?? []]);
        }
        else
        {
            PressHook.LetThrough(this);
        }
    }

    /// <summary>menus: the popup is a context menu, whose rim in a dark theme is Explorer's (<see cref="MenuRimDark"/>) rather than a flyout's.</summary>
    internal bool MenuRim { get; set; }

    /// <summary>menus: the rim of Explorer's dark context menu, as measured at 100 % and 150 %.</summary>
    internal static readonly Color MenuRimDark = Color.FromArgb(255, 0x3B, 0x3B, 0x3B);

    /// <summary>
    /// Set by the dock around opening a flyout for the settings app's look at it, while a widget's
    /// flyout height is being changed there (Dock.FlyoutPreview.cs): the popup that opens then takes
    /// no focus, closes nothing of Windows', and stays through presses elsewhere and another window
    /// having the focus — the settings app keeps the keyboard, and its spin buttons pressed leave the
    /// look standing — until its owner closes it. This thread's, as <see cref="PressesHandledBy"/> is.
    /// </summary>
    internal static bool Previewing
    {
        get => previewing;
        set => previewing = value;
    }

    [ThreadStatic]
    private static bool previewing;

    // Whether this showing opened for a look (Previewing): kept while it stays open, so the owner's
    // own updates of it — a list that changed, shown again — do not take the focus either.
    private bool preview;

    /// <summary>Whether this showing is a look, opened while <see cref="Previewing"/> was set.</summary>
    internal bool Previewed => open && preview;

    /// <summary>
    /// §606: this showing, opened as a look (<see cref="Previewed"/>), taken on as a showing of its own — the add page's ghost
    /// of a folder its click puts in, open where it stood: from now on it is any Menu popup's, the focus its, as it would have
    /// taken it opening, handed back as it closes. Nothing for one not a look.
    /// </summary>
    internal void Own()
    {
        if (!open || !preview || Hwnd == HWND.NULL)
        {
            return;
        }
        preview = false;
        if (Mode == FlyoutMode.Menu)
        {
            previous = GetForegroundWindow();
            Surfaces.Focus(Hwnd);
        }
    }

    /// <summary>
    /// §606: how much of the whole popup shows, over its coming in and going (which move a visual under this one): 1 whole
    /// (§625 the add page's ghost too), 0 out of sight while a ghost takes its place. A showing
    /// opened where it was shut starts whole.
    /// </summary>
    internal float Opacity
    {
        get => opacity;
        set
        {
            opacity = value;
            if (box is not null)
            {
                Try(() => box.Opacity = value);
            }
        }
    }

    private float opacity = 1;

    /// <summary>
    /// edit: this thread's looks open are brought in front of the other topmost windows again: the
    /// settings app, over everything while edit mode is on, came to the front over the selected
    /// widget's flyout (Dock.FlyoutPreview.cs). Nothing is activated.
    /// </summary>
    internal static void RaiseLooks()
    {
        foreach (var host in OpenMenus)
        {
            if (host.Previewed)
            {
                host.Front();
            }
        }
    }

    // What keeps it open through what would close a menu: its owner's say (StaysOpen), or a look.
    private bool Holds => StaysOpen || preview;

    /// <summary>
    /// Where the press that closed the popup the last time was, on the screen; null where
    /// something else closed it — a key, another window taking the focus, its owner.
    /// </summary>
    public POINT? DismissedAt { get; private set; }

    /// <summary>A point of the window's client area on the screen.</summary>
    private POINT OnScreen(int x, int y)
    {
        var at = new POINT { x = x, y = y };
        _ = ClientToScreen(Hwnd, &at);
        return at;
    }

    /// <summary>Where the pointer was when the message being handled was sent.</summary>
    private static POINT PressPoint()
    {
        var sent = GetMessagePos();
        return new POINT { x = (short)(sent & 0xFFFF), y = (short)((sent >> 16) & 0xFFFF) };
    }

    /// <summary>A mouse message's point, in panel px; false when it lies outside the panel.</summary>
    public bool ToPanel(LPARAM lParam, out Vector2 point)
    {
        point = new Vector2(X(lParam) - panel.X, Y(lParam) - panel.Y);
        return open && panel.Contains(X(lParam), Y(lParam));
    }

    /// <summary>A screen point in panel px; false when it lies outside the panel (or nothing is open).</summary>
    public bool ToPanel(POINT screen, out Vector2 point)
    {
        point = default;
        if (!open || Hwnd == HWND.NULL)
        {
            return false;
        }
        var local = screen;
        if (!ScreenToClient(Hwnd, &local))
        {
            return false;
        }
        point = new Vector2(local.x - panel.X, local.y - panel.Y);
        return panel.Contains(local.x, local.y);
    }

    /// <summary>Whether <paramref name="screen"/> lies on the open panel.</summary>
    public bool Contains(POINT screen)
    {
        if (!open || Hwnd == HWND.NULL)
        {
            return false;
        }
        var local = screen;
        return ScreenToClient(Hwnd, &local) && OnPanel(local.x, local.y);
    }

    /// <summary>Whether a point of the window (client px) is on the panel — on one of its <see cref="Pieces"/>, where it has them.</summary>
    private bool OnPanel(float x, float y)
    {
        // §647: an attached folder's ground is the folder's: a press on its empty room closes nothing.
        if (Grounded && GroundBox is var ground && x - panel.X >= ground.left && x - panel.X < ground.right && y - panel.Y >= ground.top && y - panel.Y < ground.bottom)
        {
            return true;
        }
        if (!panel.Contains(x, y))
        {
            return false;
        }
        if (!pieced || Pieces is not { } pieces)
        {
            return true;
        }
        var (px, py) = (x - panel.X, y - panel.Y);
        foreach (var piece in pieces)
        {
            if (px >= piece.left && px < piece.right && py >= piece.top && py < piece.bottom)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Keeps <paramref name="value"/> for this showing, and closes it when the showing ends.</summary>
    public T Keep<T>(T value)
        where T : IDisposable
    {
        made.Add(value);
        return value;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }
        disposed = true;
        // A showing that fades out as it closes is let go of once the fade is over, and the window
        // and its tree with it — never cut off, and never gone before the frame's content is on the
        // screen: a folder switched for another has one of the two in sight all along (Surfaces).
        var fading = open && animate && root is not null && Hwnd != HWND.NULL && Surfaces.Visible(Hwnd);
        Close(restore: true);
        Leave();
        if (Hwnd != HWND.NULL)
        {
            var window = Hwnd;
            // Its last messages have nothing to tell an owner that may be going too.
            _ = SetWindowLongPtrW(window, GWLP.GWLP_USERDATA, 0);
            _ = KillTimer(window, HideTimer);
            var close = TakeShowing();
            var gone = target;
            var chromeGone = TakeChrome();
            target = null;
            open = false;
            Inside = false;
            tracking = false;
            Hwnd = HWND.NULL;
            Surfaces.Destroy(
                window,
                () =>
                {
                    close();
                    chromeGone();
                    if (gone is not null)
                    {
                        CloseAll([gone]);
                    }
                },
                afterMs: fading ? closingMs + 17 : 0);
        }
    }

    /// <summary>
    /// The showing let go of: the owner's things are no longer the host's, and what closes them is handed
    /// back, to run once the window no longer shows them (Surfaces) — taken out of the content then,
    /// unless a showing since has put its own there. The chrome stays, for the next showing.
    /// </summary>
    private Action TakeShowing()
    {
        var showing = generation;
        var old = made;
        made = [];
        shown = false;
        return () =>
        {
            if (generation == showing && !open)
            {
                Try(() =>
                {
                    content?.Children.RemoveAll();
                    outside?.Children.RemoveAll();
                });
            }
            CloseAll(old);
        };
    }

    /// <summary>The window's chrome let go of, for good: nothing of it is the host's any more, and what closes it is handed back.</summary>
    private Action TakeChrome()
    {
        var gone = chrome.ToList();
        var panelsGone = panels.ToList();
        chrome.Clear();
        panels.Clear();
        (pieceSlide2, pieceSlide3, pieceEase) = (null, null, null);
        (stage, box, root, under, body, bodyShape, bodyClip) = (null, null, null, null, null, null, null);
        (content, over, outside) = (null, null, null);
        (groundView, groundPaper, groundTint, groundFill, groundEdge, groundEdgeInk, groundSeamInk, dressedGrounded, groundPapered) = (null, null, null, null, null, null, null, false, false);
        groundSeams.Clear();
        (shown, pieced, frame) = (false, false, default);
        return () =>
        {
            foreach (var panel in panelsGone)
            {
                panel.Close();
            }
            CloseAll(gone);
        };
    }

    /// <summary>
    /// §650: the keys to this open popup's window, as a Menu takes them as it opens — a Tool's whose own field has them as
    /// it opens (the edit bar's add page's search): the window in front kept, the keys handed back to it as it closes while
    /// it still has them (<see cref="Close"/>), as a Menu's are. Once it shows (Surfaces): a window not in sight yet takes no
    /// focus.
    /// </summary>
    internal void TakeKeys()
    {
        if (!open || Hwnd == HWND.NULL)
        {
            return;
        }
        if (GetForegroundWindow() is var front && front != Hwnd)
        {
            previous = front;
        }
        Surfaces.Focus(Hwnd);
    }

    /// <summary>
    /// Ends the showing: at once for the state, after the Windows 11 fade for the window. With
    /// <paramref name="restore"/>, and while a popup that took the keys (a Menu, §650 a Tool by <see cref="TakeKeys"/>)
    /// still has the foreground, the foreground goes back to the window that had it, rather than to whichever window
    /// Windows picks next.
    /// </summary>
    internal void Close(bool restore)
    {
        if (!open)
        {
            return;
        }
        open = false;
        Inside = false;
        Leave();
        if (Hwnd == HWND.NULL)
        {
            return;
        }
        if (GetCapture() == Hwnd)
        {
            _ = ReleaseCapture();
        }
        if (restore && GetForegroundWindow() == Hwnd
            && previous != HWND.NULL && previous != Hwnd && IsWindow(previous))
        {
            _ = SetForegroundWindow(previous);
        }
        _ = KillTimer(Hwnd, MotionTimer);
        if (animate && root is not null && compositor is not null && Surfaces.Visible(Hwnd))
        {
            try
            {
                // §446, §489: it goes as its motion says, back the way it came from wherever its coming in had got to;
                // at once where it says none.
                var ms = Move(Plan, toOpen: false);
                // A timer, not the animation's batch: it fires whether or not
                // the compositor is drawing this window.
                if (ms > 0 && SetTimer(Hwnd, HideTimer, (uint)ms + 17, null) != 0)
                {
                    closingMs = ms;
                    return;
                }
            }
            catch (Exception)
            {
            }
        }
        HideNow();
    }

    // How long the closing that runs takes, in ms: the window and its tree are let go of once it is over (Dispose).
    private int closingMs;

    // §446: where between shut and open the panel stands, by its motion (Move).
    private MotionClock clock;

    private static double Now() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    /// <summary>
    /// §446, §489: the panel set going toward open (<paramref name="toOpen"/>) or shut by <paramref name="plan"/>, from where it
    /// stands now (<see cref="MotionClock"/>): its opacity, and its offset from its side where it moves (<see cref="PopupMotion.Away"/>;
    /// a submenu's from the item it hangs from, <see cref="PopupMotion.Beside"/>), run by the compositor on the plan's curve. How
    /// long it takes, in ms; 0: put there at once.
    /// </summary>
    private int Move(PopupMotion plan, bool toOpen)
    {
        var c = compositor!;
        var shown = root!;
        var (from, ms) = clock.Go(Now(), toOpen, plan);
        var to = toOpen ? 1f : 0f;
        var away = new Vector3(spot.Beside ? plan.Beside(spot.Leftward, Scale) : plan.Away(spot.Placement.Edge, Scale), 0);
        // §647: an attached folder comes out of its bar as a drawer, its whole depth, never fading: the box ends at the bar,
        // so what is still in the bar is out of sight. Where it moves at all (arranged it fades where it stands).
        var drawer = Grounded && away != Vector3.Zero;
        if (drawer)
        {
            away = new Vector3(Drawer(spot.Placement.Edge, GroundBox), 0);
        }
        shown.StopAnimation("Opacity");
        shown.StopAnimation("Offset");
        if (ms < 1)
        {
            clock.Snap(toOpen);
            shown.Opacity = to;
            shown.Offset = toOpen ? Vector3.Zero : away;
            return 0;
        }
        var curve = toOpen ? plan.EnterCurve : plan.ExitCurve;
        var ease = Keep(MotionEase.Of(c, curve));
        var time = Motion.Span(ms);
        if (drawer)
        {
            shown.Opacity = 1;
        }
        else
        {
            var fade = Keep(c.CreateScalarKeyFrameAnimation());
            fade.InsertKeyFrame(0, from);
            fade.InsertKeyFrame(1, to, ease);
            fade.Duration = time;
            shown.StartAnimation("Opacity", fade);
        }
        if (away != Vector3.Zero)
        {
            var slide = Keep(c.CreateVector3KeyFrameAnimation());
            slide.InsertKeyFrame(0, away * (1 - from));
            slide.InsertKeyFrame(1, away * (1 - to), ease);
            slide.Duration = time;
            shown.StartAnimation("Offset", slide);
        }
        return (int)Math.Ceiling(ms);
    }

    /// <summary>
    /// §647: where an attached folder stands shut, from where it stands open: in its bar along <paramref name="edge"/>, as far
    /// as its <paramref name="ground"/> (panel px) reaches out of it — wholly behind the box's edge at the bar. Pure.
    /// </summary>
    internal static Vector2 Drawer(Edge edge, D2D_RECT_F ground) => edge switch
    {
        Edge.Top => new Vector2(0, -(ground.bottom - ground.top)),
        Edge.Left => new Vector2(-(ground.right - ground.left), 0),
        Edge.Right => new Vector2(ground.right - ground.left, 0),
        _ => new Vector2(0, ground.bottom - ground.top),
    };

    /// <summary>
    /// §446: the panel where its coming in ends, its animations let go of: what the opening's timer does once it should be
    /// over, so a panel whose animation DWM never ran (it can stop them for every app at once) stands there all the same.
    /// </summary>
    private void Arrived()
    {
        _ = KillTimer(Hwnd, MotionTimer);
        if (!open || root is not { } shown)
        {
            return;
        }
        clock.Snap(true);
        Try(() =>
        {
            shown.StopAnimation("Opacity");
            shown.StopAnimation("Offset");
            shown.Opacity = 1;
            shown.Offset = Vector3.Zero;
        });
    }

    private FlyoutHostEvent Dismiss(bool restore, POINT? pressedAt = null)
    {
        DismissedAt = pressedAt;
        Close(restore);
        return new FlyoutHostEvent(FlyoutHostEventKind.Dismissed);
    }

    private void Track()
    {
        // §697: on a desktop the pointer is not on (a stage test's own: GetCursorPos refuses there) nothing can leave — Windows
        // would say it left at once, and a choice rested on under a pointer moved there would come off as it came.
        POINT pointer;
        if (!GetCursorPos(&pointer))
        {
            tracking = true;
            return;
        }
        var track = new TRACKMOUSEEVENT
        {
            cbSize = (uint)sizeof(TRACKMOUSEEVENT),
            dwFlags = TME.TME_LEAVE,
            hwndTrack = Hwnd,
        };
        tracking = TrackMouseEvent(&track);
    }

    /// <summary>Where a popup goes: beside an anchor on a bar's edge, or beside an item of its parent.</summary>
    private readonly record struct Spot(FlyoutPlacement Placement, RECT Item, int Top, bool Beside, bool Leftward, POINT? At = null);

    /// <summary>
    /// menus: where a panel <paramref name="size"/> px long, kept at <paramref name="at"/>, stands
    /// between <paramref name="low"/> and <paramref name="high"/>: where it was, moved only as far as
    /// it must to stay inside them, from <paramref name="low"/> where it is longer than the room (§226:
    /// a menu shown again for a change made in it never moves under the pointer).
    /// </summary>
    internal static int Held(int at, int size, int low, int high) => Math.Max(low, Math.Min(at, high - size));

    private void Open(Gfx gfx, Look look, float scale, Vector2 panelSize, Spot where, bool keep = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        // §356: the open showing shown again, its content the owner's to keep.
        keep &= open && shown && Hwnd != HWND.NULL;
        DismissedAt = null;
        EnsureWindow(gfx);
        _ = KillTimer(Hwnd, HideTimer);
        if (Previewing)
        {
            preview = true;
        }
        else if (!open)
        {
            preview = false;
        }
        // The window in front is a showing's own, none kept from the last (§650: a Tool's only where it took the keys).
        if (!open)
        {
            previous = HWND.NULL;
        }
        if (!open && Mode == FlyoutMode.Menu && !preview)
        {
            previous = GetForegroundWindow();
            if (Opening?.Invoke(previous) == true)
            {
                previous = HWND.NULL;
            }
        }
        Look = look;
        Scale = scale;
        Theme = ThemeFor(look);
        // menus: Explorer's menu has a light rim on its dark surface (#3B3B3B on #2C2C2C, measured).
        if (MenuRim && Theme is { Dark: true, HighContrast: false } && look.Colors is { Flyout: null, FlyoutOutline: null })
        {
            Theme = Theme with { Stroke = MenuRimDark };
        }
        animate = look.Animations;
        spot = where;
        var c = gfx.Compositor;
        compositor = c;
        var old = made;
        made = keep ? made : [];
        try
        {
            Chrome(c);
            // A showing starts with nothing of the owner's — a new one, or the open one shown again —
            // but where its owner keeps it (§356).
            if (!keep)
            {
                content!.Children.RemoveAll();
                outside!.Children.RemoveAll();
            }
            // §606: a showing where it was shut starts whole; the open one shown again keeps what its owner made it.
            if (!open)
            {
                opacity = 1;
            }
            box!.Opacity = opacity;
            // Where a closing fade or an entrance left it.
            root!.StopAnimation("Opacity");
            root.StopAnimation("Offset");
            root.Opacity = 1;
            root.Offset = Vector3.Zero;
            // folders: several panels (Pieces) wear their chrome each, and the content is the owner's to
            // clip; one panel clips it to its shape.
            pieced = Pieces is not null;
            body!.Clip = pieced ? null : bodyClip;
            Wear(c);
            shown = true;
            _ = Arrange(panelSize, activate: Mode == FlyoutMode.Menu && !preview && !keep);
            // §446, §489: every popup comes in as its motion says where it was shut or going — from where its closing had
            // got to — and is put where it ends on a timer whether or not DWM ran the animation (Arrived): DWM can stop
            // running animations for every app at once (why §355 had popups come up at once), and a popup is never left
            // waiting on one to be seen. Input finds it where it ends from the first frame (its region and its hit tests
            // are where it stands). The open one shown again stands where it stands.
            _ = KillTimer(Hwnd, MotionTimer);
            Entered = false;
            if (animate && !open && Move(Plan, toOpen: true) is var ms and > 0)
            {
                Entered = true;
                _ = SetTimer(Hwnd, MotionTimer, (uint)(ms + MotionGraceMs), null);
            }
            else
            {
                clock.Snap(true);
            }
        }
        catch
        {
            if (!keep)
            {
                CloseAll(made);
                made = old;
            }
            throw;
        }
        if (!keep)
        {
            CloseAll(old);
        }
        generation++;
        open = true;
        if (Mode == FlyoutMode.Menu)
        {
            if (!Showing.Contains(this))
            {
                Showing.Add(this);
            }
            Hook();
            Keeps();
            // The dock received the click, which is what lets it hand the
            // foreground over. Should it be refused, the hook still closes the
            // popup on a click elsewhere. A look takes nothing; a showing again has it already (§356).
            if (!preview && !keep)
            {
                // Once it shows (Surfaces): a window not in sight yet takes no focus.
                Surfaces.Focus(Hwnd);
            }
        }
    }

    private float Px(float dip) => Round(dip * Scale);

    /// <summary>
    /// Places a <paramref name="size"/> panel for the spot and sizes the chrome to it — in composition:
    /// the box moves in the window's frame, which stands still (§355) — and shows the window; returns where
    /// the opening slide starts from.
    /// </summary>
    private Vector3 Arrange(Vector2 size, bool activate)
    {
        // §651: on a ground, the panel flush with its bar and free to reach the work area's ends.
        var placement = Placement;
        var work = placement.Work;
        float width = MathF.Ceiling(size.X);
        float height = MathF.Ceiling(size.Y);
        // folders: more room where an arranged folder's "+" buttons stand outside its panel.
        float room = Px(MathF.Max(RoomDip, OutsideDip));
        float away = Px(placement.GapDip);
        float blur = Px(16);
        float drop = Px(8);
        int w = (int)width;
        int h = (int)height;
        int keep = (int)Px(placement.MarginDip);
        int x;
        int top;
        beak = null;
        float roomLeft, roomTop, roomRight, roomBottom;
        Vector3 from;
        float lift = Px(12);
        if (spot.Beside)
        {
            // Beside the parent's item, overlapping its padding; to the left
            // when the right has no room (BesideX).
            int overlap = (int)Px(4);
            (x, var toLeft) = BesideX(spot.Item, w, work, keep, overlap, spot.Leftward);
            from = new Vector3(toLeft ? Px(8) : -Px(8), 0, 0);
            top = AlongEdge(spot.Top, work.top, work.bottom, h, keep);
            // menus: a submenu shown again stays where it stood, on the side it took (§226).
            if (spot.At is { } held)
            {
                x = Held(held.x, w, work.left + keep, work.right - keep);
                top = Held(held.y, h, work.top + keep, work.bottom - keep);
            }
            else
            {
                BesideLeft = toLeft;
            }
            (roomLeft, roomTop, roomRight, roomBottom) = (room, room, room, room);
        }
        else
        {
            var anchor = placement.Anchor;
            int apart = (int)away;
            // Where it stands along the edge: centred on the anchor or against an end, inside the
            // work area as the rest of the placement keeps it.
            // folders: what is placed is the core (Core), the panel round it where that puts it.
            var core = Core is { } part && part.right > part.left && part.bottom > part.top
                ? (Left: (int)MathF.Round(part.left), Top: (int)MathF.Round(part.top), W: (int)MathF.Round(part.right - part.left), H: (int)MathF.Round(part.bottom - part.top))
                : (Left: 0, Top: 0, W: w, H: h);
            int along = placement.Along(core.W, core.H, keep) - (placement.Edge is Edge.Left or Edge.Right ? core.Top : core.Left);
            switch (placement.Edge)
            {
                case Edge.Top:
                    x = along;
                    top = Math.Min(Math.Max(anchor.y + apart, work.top), work.bottom - h);
                    from = new Vector3(0, -lift, 0);
                    break;
                case Edge.Left:
                    // Across a side bar too the core stands where it would alone, what hangs off its
                    // bar side under the bar, as past a screen's edge along a bottom bar (§303). The
                    // same on the right. Wider than the room beside the bar, it is its far end that
                    // gives, as along (Along) — never the near one, moved in over the bar (§320); a
                    // panel with no room either side stands over the anchor instead (Roomiest, below).
                    x = anchor.x + apart - core.Left;
                    top = along;
                    from = new Vector3(-lift, 0, 0);
                    break;
                case Edge.Right:
                    x = anchor.x - apart - core.W - core.Left;
                    top = along;
                    from = new Vector3(lift, 0, 0);
                    break;
                default:
                    x = along;
                    top = Math.Max(Math.Min(anchor.y - apart, work.bottom) - h, work.top);
                    from = new Vector3(0, lift, 0);
                    break;
            }
            // menus: a menu shown again stays where it stood, never nearer its anchor's bar than it may
            // stand, growing down and to the right while there is room (§226).
            if (spot.At is { } held)
            {
                var (left, upper, right, lower) = (work.left, work.top, work.right, work.bottom);
                if (!placement.Over)
                {
                    switch (placement.Edge)
                    {
                        case Edge.Top: upper = Math.Max(upper, anchor.y + apart); break;
                        case Edge.Left: left = Math.Max(left, anchor.x + apart); break;
                        case Edge.Right: right = Math.Min(right, anchor.x - apart); break;
                        default: lower = Math.Min(lower, anchor.y - apart); break;
                    }
                }
                x = Held(held.x, w, left, right);
                top = Held(held.y, h, upper, lower);
            }
            // Room on neither side of the anchor: over it, centred on it, inside the work area (Roomiest).
            else if (placement.Over)
            {
                if (placement.Edge is Edge.Left or Edge.Right)
                {
                    x = placement.OverStart(w, keep);
                }
                else
                {
                    top = placement.OverStart(h, keep);
                }
            }
            // §521: a control's popup under it, flush with its left, or over it where there is no room under it (PopupPlace),
            // and nowhere else: the side it took is its edge from here on, which its shadow's room and its slide follow.
            if (placement.Control is { } control)
            {
                var (under, above) = PopupPlace.Of(control, w, h, work, apart, keep, placement.Within, placement.Beak);
                placement = placement with { Edge = above ? Edge.Bottom : Edge.Top, Anchor = new POINT { x = control.left, y = above ? control.top : control.bottom } };
                spot = spot with { Placement = placement };
                (x, top) = (under.left, under.top);
                // §559: no taller than its side's room, where it asked for more (its owner lays it out against that room,
                // FlyoutPlacement.Room, so this is at most a px of rounding): never over its control.
                (h, height) = (under.bottom - under.top, under.bottom - under.top);
                from = new Vector3(0, above ? lift : -lift, 0);
                // §584: its beak on the side facing the control, over the control's middle.
                if (placement.Beak && !pieced)
                {
                    beak = (above ? Edge.Bottom : Edge.Top, MathF.Round(Beak.TipAt(control, under, Px(8), Px(Beak.TallDip))), Px(Beak.TallDip));
                }
            }
            // edit: never over a display's edit bar (Avoided), which stands at the top, under a bar along
            // it; the edit bars and their own tips are Tool hosts. A bottom bar's popups end far short of
            // it; a side bar's, centred on their anchor, can reach up to it. A folder takes the full
            // height under it, the edit bar over it (UnderEditBar, §329): it is never moved here. §521: nor
            // a control's popup, which hangs from its control, the settings panel's own (the edit bar's).
            else if (Mode != FlyoutMode.Tool && !UnderEditBar && placement.Edge != Edge.Bottom && !placement.Over)
            {
                top = ClearOf(x, top, w, h, placement.Edge, apart, Avoided);
            }
            // The box reaches only as far as the anchor on the edge's side:
            // it is topmost, and anything of it over the bar would take the
            // bar's mouse. The other three sides leave the shadow its room.
            (roomLeft, roomTop, roomRight, roomBottom) = placement.Edge switch
            {
                Edge.Top => (room, away, room, room),
                Edge.Left => (away, room, room, room),
                Edge.Right => (room, room, away, room),
                _ => (room, room, room, away),
            };
        }
        // §580: the room never past the room the popup was placed in, where a bar stands: it takes the pointer, and a
        // panel pushed against an end of its work area, 12 DIP in, held the bar's next 12 DIP under its shadow's 24.
        (roomLeft, roomTop, roomRight, roomBottom) = RoomInside((roomLeft, roomTop, roomRight, roomBottom), x, top, w, h, work);
        if (!spot.Beside)
        {
            // folders: an arranged folder's window to the display's edges, over its bars too (§246).
            if (Spread is { } screen)
            {
                roomLeft = MathF.Max(roomLeft, x - screen.left);
                roomRight = MathF.Max(roomRight, screen.right - (x + w));
                roomTop = MathF.Max(roomTop, top - screen.top);
                roomBottom = MathF.Max(roomBottom, screen.bottom - (top + h));
            }
        }
        // folders, §647: an attached folder's ground, from its bar to the air past its far side, inside the box.
        GroundBox = Grounded ? GroundOf(placement.Edge, work, x, top, w, h) : default;
        if (Grounded)
        {
            // No shadow to keep room for: at rest the box is the ground, a press past it a press elsewhere; arranged it
            // reaches the display's edges as ever (Spread).
            var wide = Spread is not null;
            roomLeft = MathF.Max(wide ? roomLeft : 0, -GroundBox.left);
            roomTop = MathF.Max(wide ? roomTop : 0, -GroundBox.top);
            roomRight = MathF.Max(wide ? roomRight : 0, GroundBox.right - w);
            roomBottom = MathF.Max(wide ? roomBottom : 0, GroundBox.bottom - h);
        }
        Room = (roomLeft, roomTop, roomRight, roomBottom);
        float boxW = width + roomLeft + roomRight;
        float boxH = height + roomTop + roomBottom;
        // Where the window stood before §355 — the panel and the room round it — on the screen.
        int boxX = x - (int)roomLeft;
        int boxY = top - (int)roomTop;
        // The window: its display less a px on its far sides, placed once — while it is out of sight, as
        // it is made or before it shows on another display (a popup shown again on another display while
        // it stands, the one case it moves in sight).
        var onDisplay = FrameOn(DisplayOf(Spread is { } spread ? Union(work, spread) : work));
        if (!Same(onDisplay, frame))
        {
            if (!Surfaces.Place(Hwnd, onDisplay.left, onDisplay.top, onDisplay.right - onDisplay.left, onDisplay.bottom - onDisplay.top))
            {
                throw Failed("SetWindowPos");
            }
            frame = onDisplay;
            FramesPlaced++;
        }
        Origin = new POINT { x = frame.left, y = frame.top };
        var (inX, inY) = (boxX - frame.left, boxY - frame.top);
        panel = new Area(inX + roomLeft, inY + roomTop, width, height);
        Try(() =>
        {
            stage!.Size = new Vector2(frame.right - frame.left, frame.bottom - frame.top);
            box!.Offset = new Vector3(inX, inY, 0);
            box.Size = new Vector2(boxW, boxH);
            root!.Size = new Vector2(boxW, boxH);
            body!.Size = new Vector2(width, height);
            body.Offset = new Vector3(roomLeft, roomTop, 0);
            bodyShape!.Size = new Vector2(MathF.Max(width - (2 * RimInset), 1), MathF.Max(height - (2 * RimInset), 1));
            content!.Size = new Vector2(width, height);
            outside!.Offset = new Vector3(roomLeft, roomTop, 0);
            outside.Size = new Vector2(width, height);
            panelsPlace = (roomLeft, roomTop, blur, drop, away);
            PlaceGround(roomLeft, roomTop, new POINT { x = x, y = top });
            PlacePanels();
        });
        // What takes the pointer, and what DWM shows: where the window stood (Surfaces grows it at once and
        // shrinks it once the content is there).
        BoxOnScreen = new RECT { left = boxX, top = boxY, right = boxX + (int)boxW, bottom = boxY + (int)boxH };
        Surfaces.Region(Hwnd, new RECT { left = inX, top = inY, right = inX + (int)boxW, bottom = inY + (int)boxH });
        // Shown in its stratum (§359): as the frame ends, its window clear until the commit brings its
        // content, so it never comes up empty and comes with whatever else the frame changed; an arranged
        // folder under the bars (Stand). Shown without activating it: Open's focus activates it once it
        // shows (Surfaces.Focus), as a click would. One that stands keeps its place in its stratum.
        var placed = (boxX, boxY, (int)boxW, (int)boxH);
        var stands = open && !activate && Surfaces.Visible(Hwnd);
        Enlist();
        if (!stands)
        {
            Surfaces.Show(Hwnd);
            Surfaces.Front(Hwnd);
        }
        if (!stands || placed != windowPlaced)
        {
            WindowMoves++;
        }
        windowPlaced = placed;
        return from;
    }

    /// <summary>
    /// folders: the panel's <see cref="Pieces"/> changed where it stands — a list scrolled, its cards
    /// with it — and their chrome follows them, in place; nothing where they are the same.
    /// </summary>
    /// <para>
    /// <paramref name="glide"/>: the pieces glide there from where they were last put, in a move's
    /// time with the Windows easing (FolderDrag.MoveMs) — the cards of a list giving way for a line
    /// carried in it, as the bar's panels do for a block. Only where there are as many as before;
    /// a piece more or less is put at once.
    /// </para>
    internal void SetPieces(IReadOnlyList<D2D_RECT_F> pieces, bool glide = false)
    {
        if (Pieces is { } now && now.SequenceEqual(pieces))
        {
            return;
        }
        piecesGlide = glide && panels.Count == pieces.Count;
        Pieces = pieces;
        try
        {
            if (shown && pieced)
            {
                Try(PlacePanels);
            }
        }
        finally
        {
            piecesGlide = false;
        }
    }

    // folders: the pieces being placed glide there (SetPieces), and what they glide with.
    private bool piecesGlide;
    private Vector2KeyFrameAnimation? pieceSlide2;
    private Vector3KeyFrameAnimation? pieceSlide3;
    private CompositionEasingFunction? pieceEase;

    /// <summary>folders: <paramref name="target"/>'s <paramref name="property"/> to <paramref name="to"/>, gliding there from <paramref name="from"/> while pieces glide.</summary>
    private void PieceTo(CompositionObject target, string property, Vector2 from, Vector2 to, Action<Vector2> set)
    {
        target.StopAnimation(property);
        set(to);
        if (!piecesGlide || from == to || compositor is not { } c)
        {
            return;
        }
        var slide = pieceSlide2 ??= Chromed(c.CreateVector2KeyFrameAnimation());
        slide.InsertKeyFrame(0, from);
        slide.InsertKeyFrame(1, to, PieceEase(c));
        slide.Duration = Look.Motion.Time(MotionKind.Move);
        try
        {
            target.StartAnimation(property, slide);
        }
        catch (ArgumentException)
        {
        }
    }

    /// <inheritdoc cref="PieceTo(CompositionObject, string, Vector2, Vector2, Action{Vector2})"/>
    private void PieceTo(Visual target, Vector3 to)
    {
        var from = target.Offset;
        target.StopAnimation("Offset");
        target.Offset = to;
        if (!piecesGlide || from == to || compositor is not { } c)
        {
            return;
        }
        var slide = pieceSlide3 ??= Chromed(c.CreateVector3KeyFrameAnimation());
        slide.InsertKeyFrame(0, from);
        slide.InsertKeyFrame(1, to, PieceEase(c));
        slide.Duration = Look.Motion.Time(MotionKind.Move);
        target.StartAnimation("Offset", slide);
    }

    private CompositionEasingFunction PieceEase(Compositor c) =>
        pieceEase ??= Chromed(MotionEase.Of(c, MotionKind.Move));

    /// <summary>Keeps <paramref name="value"/> for the window's life, and closes it with the window.</summary>
    private T Chromed<T>(T value)
        where T : IDisposable
    {
        chrome.Add(value);
        return value;
    }

    /// <summary>The window's chrome, made once for its life (see the fields): what every showing dresses (<see cref="Wear"/>) and places (<see cref="PlacePanels"/>).</summary>
    private void Chrome(Compositor c)
    {
        if (stage is not null)
        {
            return;
        }
        stage = Chromed(c.CreateContainerVisual());
        box = Chromed(c.CreateContainerVisual());
        // Clipped where the window was: what rises in comes in from its edge (Move, §489), and the
        // shadow ends where it ended.
        box.Clip = Chromed(c.CreateInsetClip());
        stage.Children.InsertAtTop(box);
        root = Chromed(c.CreateContainerVisual());
        box.Children.InsertAtTop(root);
        under = Chromed(c.CreateContainerVisual());
        root.Children.InsertAtTop(under);
        body = Chromed(c.CreateContainerVisual());
        bodyShape = Chromed(c.CreateRoundedRectangleGeometry());
        bodyClip = Chromed(c.CreateGeometricClip(bodyShape));
        root.Children.InsertAtTop(body);
        content = Chromed(c.CreateContainerVisual());
        body.Children.InsertAtTop(content);
        over = Chromed(c.CreateContainerVisual());
        root.Children.InsertAtTop(over);
        // folders: over the rims, unclipped, for what stands outside the panel (Outside).
        outside = Chromed(c.CreateContainerVisual());
        root.Children.InsertAtTop(outside);
        target!.Root = stage;
    }

    /// <summary>
    /// Every panel in the theme as it is now, in place: its colours, its shadow, its rim and its corners
    /// at this scale. Only a material that changes kind (the backdrop under a tint, a tint alone, the
    /// solid base) or a shadow that comes or goes is made.
    /// </summary>
    private void Wear(Compositor c)
    {
        var inside = RimInset;
        var radius = Px(Theme.CornerDip);
        bodyShape!.CornerRadius = new Vector2(radius - inside, radius - inside);
        bodyShape.Offset = new Vector2(inside, inside);
        dressedGrounded = Grounded;
        // §647: the ground in its bar's colour as it is now (Windows' mode, the Appearance's Taskbar); §651 its lines with it.
        DressGround();
        foreach (var panel in panels)
        {
            Dress(c, panel);
        }
    }

    /// <summary>
    /// <paramref name="panel"/> in the theme: the host backdrop under the tint (<see cref="FlyoutTheme.Glass"/>),
    /// nothing (<see cref="FlyoutTheme.Bare"/>, §464: a folder's lines wear their own), or the solid base;
    /// a shadow where the theme casts one; the rim. A rim outside the material (<see cref="FlyoutTheme.RimOutside"/>):
    /// the plate and the shape casting the shadow stand inside it, so what shows through the rim is what is
    /// behind the panel.
    /// </summary>
    private void Dress(Compositor c, PanelChrome panel)
    {
        var theme = Theme;
        var inside = RimInset;
        var radius = Px(Theme.CornerDip);
        var stroke = Rim;
        // §647: cards on a ground cast none: they stand on it as the bar's widgets stand on the bar.
        var casts = theme.ShadowOpacity > 0 && theme.Casts && !Grounded;
        if (casts && panel.Caster is null)
        {
            MakeCaster(c, panel);
        }
        else if (!casts && panel.Caster is { } gone)
        {
            under!.Children.Remove(gone);
            CloseAll(panel.CastMade);
            (panel.Caster, panel.CastShape, panel.CastGeometry, panel.CastPicture, panel.CastFill, panel.Shadow) = (null, null, null, null, null, null);
            panel.CastSprite = null;
        }
        if (panel is { CastGeometry: { } cast, CastFill: { } solid, Shadow: { } shadow })
        {
            cast.CornerRadius = new Vector2(radius - inside, radius - inside);
            cast.Offset = new Vector2(inside, inside);
            solid.Color = theme.Base;
            shadow.BlurRadius = Px(16);
            shadow.Offset = new Vector3(0, Px(8), 0);
            shadow.Opacity = theme.ShadowOpacity;
        }
        panel.Outline.CornerRadius = new Vector2(radius - inside, radius - inside);
        panel.Outline.Offset = new Vector2(inside, inside);
        var kind = theme.Bare ? Plating.Bare : theme.Glass ? Plating.Glass : Plating.Solid;
        if (kind != panel.Wears || panel.Fill is null)
        {
            panel.Plate.Children.RemoveAll();
            CloseAll(panel.MaterialMade);
            var fill = c.CreateColorBrush();
            panel.MaterialMade.Add(fill);
            if (kind == Plating.Glass)
            {
                var backdrop = c.CreateHostBackdropBrush();
                panel.MaterialMade.Add(backdrop);
                panel.Plate.Brush = backdrop;
                var tint = c.CreateSpriteVisual();
                panel.MaterialMade.Add(tint);
                tint.RelativeSizeAdjustment = new Vector2(1, 1);
                tint.Brush = fill;
                panel.Plate.Children.InsertAtTop(tint);
            }
            else
            {
                panel.Plate.Brush = fill;
            }
            panel.Fill = fill;
            panel.Wears = kind;
            MaterialsMade++;
        }
        panel.Fill.Color = kind switch
        {
            Plating.Solid => theme.Base,
            Plating.Bare => default,
            _ => theme.Tint,
        };
        // §649: on a ground no rim: the folder's lines stand flat on it, no card round them.
        panel.RimBrush.Color = Grounded ? default : theme.Stroke;
        Glazed(c, panel, Grounded ? null : theme.Liquid);
        panel.RimLine.StrokeThickness = stroke;
        panel.RimShape.Offset = new Vector2(stroke / 2, stroke / 2);
        panel.RimShape.CornerRadius = new Vector2(radius - stroke / 2, radius - stroke / 2);
    }

    /// <summary>
    /// §704: <paramref name="panel"/> in <paramref name="glass"/>'s light (none for null): its glow and lens over its plate,
    /// under the content, and its rim the glass's in place of the theme's outline. Made where the glass changed, else kept.
    /// </summary>
    private void Glazed(Compositor c, PanelChrome panel, Karpo.Core.GlassLook? glass)
    {
        if (!ReferenceEquals(glass, panel.GlazedIn))
        {
            if (panel.GlazeView is { } gone)
            {
                under!.Children.Remove(gone);
            }
            CloseAll(panel.GlassMade);
            (panel.GlazeView, panel.Glaze, panel.RimGradient, panel.GlazedIn) = (null, null, null, glass);
            if (glass is not null)
            {
                var view = c.CreateShapeVisual();
                panel.GlassMade.Add(view);
                panel.Glaze = new Glaze(c, panel.GlassMade, glass, Scale);
                foreach (var shape in panel.Glaze.Shapes)
                {
                    view.Shapes.Add(shape);
                }
                under!.Children.InsertAbove(view, panel.Plate);
                panel.GlazeView = view;
                panel.RimGradient = Glaze.Over(c, panel.GlassMade, glass);
            }
        }
        panel.RimLine.StrokeBrush = panel.RimGradient is { } rim ? rim : panel.RimBrush;
    }

    /// <summary>
    /// <paramref name="panel"/>'s shadow: a rounded shape to cast from (the default policy casts from the
    /// visual's square bounds), drawn in the panel's own colour, under its plate, in case it ever shows.
    /// </summary>
    private void MakeCaster(Compositor c, PanelChrome panel)
    {
        var made = panel.CastMade;
        T Own<T>(T value)
            where T : IDisposable
        {
            made.Add(value);
            return value;
        }
        var shape = Own(c.CreateShapeVisual());
        var geometry = Own(c.CreateRoundedRectangleGeometry());
        var solid = Own(c.CreateColorBrush());
        var sprite = Own(c.CreateSpriteShape(geometry));
        sprite.FillBrush = solid;
        shape.Shapes.Add(sprite);
        var picture = Own(c.CreateVisualSurface());
        picture.SourceVisual = shape;
        var shadow = Own(c.CreateDropShadow());
        shadow.SourcePolicy = CompositionDropShadowSourcePolicy.InheritFromVisualContent;
        shadow.Color = Color.FromArgb(255, 0, 0, 0);
        var caster = Own(c.CreateSpriteVisual());
        caster.Brush = Own(c.CreateSurfaceBrush(picture));
        caster.Shadow = shadow;
        under!.Children.InsertBelow(caster, panel.Plate);
        (panel.Caster, panel.CastShape, panel.CastGeometry, panel.CastPicture, panel.CastFill, panel.Shadow) = (caster, shape, geometry, picture, solid, shadow);
        panel.CastSprite = sprite;
    }

    /// <summary>A panel's chrome, made, dressed and put in its place in the tree (on top: the last panel).</summary>
    private PanelChrome MakePanel(Compositor c)
    {
        var made = new List<IDisposable>();
        T Own<T>(T value)
            where T : IDisposable
        {
            made.Add(value);
            return value;
        }
        PiecesMade++;
        var plate = Own(c.CreateSpriteVisual());
        var outline = Own(c.CreateRoundedRectangleGeometry());
        var roundClip = Own(c.CreateGeometricClip(outline));
        plate.Clip = roundClip;
        under!.Children.InsertAtTop(plate);
        var rim = Own(c.CreateShapeVisual());
        var rimShape = Own(c.CreateRoundedRectangleGeometry());
        var line = Own(c.CreateSpriteShape(rimShape));
        var stroke = Own(c.CreateColorBrush());
        line.StrokeBrush = stroke;
        rim.Shapes.Add(line);
        over!.Children.InsertAtTop(rim);
        var panel = new PanelChrome
        {
            Plate = plate,
            Outline = outline,
            Rim = rim,
            RimShape = rimShape,
            RimLine = line,
            RimBrush = stroke,
            RoundClip = roundClip,
        };
        panel.Made.AddRange(made);
        Dress(c, panel);
        return panel;
    }

    /// <summary>
    /// Each panel — the one panel of a popup, or each of a folder's <see cref="Pieces"/> — where the box
    /// keeps the panel's top-left corner (<see cref="panelsPlace"/>): its shadow, cast and cut on the
    /// bar's side so what spills past it has faded before the box ends, its plate in its rounded shape,
    /// and its rim over the content.
    /// <para>
    /// <b>In place.</b> A panel's chrome is made once, for a piece more than there were, and after that
    /// only moved, sized and dressed: a list grown by a line, a day picked whose agenda is longer, a
    /// scroll, a menu shown again. Making it afresh closed the old backdrops and shadows and made new
    /// ones, which the compositor shows a frame late — the whole folder blinked (the person, 2026-10-10:
    /// "it disappears very briefly ... and then appears again"), the edit bar flashed see-through.
    /// </para>
    /// </summary>
    private void PlacePanels()
    {
        var (left, top, blur, drop, away) = panelsPlace;
        if (compositor is not { } c || under is null || over is null)
        {
            return;
        }
        IReadOnlyList<D2D_RECT_F> shapes = pieced ? Pieces ?? [] : [new D2D_RECT_F { left = 0, top = 0, right = panel.W, bottom = panel.H }];
        while (panels.Count > shapes.Count)
        {
            var gone = panels[^1];
            panels.RemoveAt(panels.Count - 1);
            if (gone.Caster is not null)
            {
                under.Children.Remove(gone.Caster);
            }
            under.Children.Remove(gone.Plate);
            over.Children.Remove(gone.Rim);
            gone.Close();
        }
        while (panels.Count < shapes.Count)
        {
            panels.Add(MakePanel(c));
        }
        var stroke = Rim;
        Edge? edge = spot.Beside ? null : spot.Placement.Edge;
        var spill = edge switch
        {
            null => 0,
            Edge.Top => blur - drop,
            Edge.Left or Edge.Right => blur,
            _ => blur + drop,
        };
        for (var index = 0; index < shapes.Count; index++)
        {
            var piece = shapes[index];
            var views = panels[index];
            var (x, y) = (left + piece.left, top + piece.top);
            var (w, h) = (MathF.Max(1, piece.right - piece.left), MathF.Max(1, piece.bottom - piece.top));
            // §584: the one panel's beak, in the gap on the side it hangs from: its plate, rim and caster reach over it too.
            var pointed = !pieced && beak is { } at && Beaked(c, views, w, h, at) ? at : ((Edge Side, float Tip, float Tall)?)null;
            var reach = pointed?.Tall ?? 0;
            var (bx, by, bw, bh) = pointed?.Side == Edge.Top ? (x, y - reach, w, h + reach) : (x, y, w, h + reach);
            if (views.Caster is { } caster)
            {
                // Cut on the bar's side by as much as its shadow would spill past the room there; the beak stands that much nearer.
                var cut = edge is null ? 0 : MathF.Max(0, spill - (away - reach));
                var (castX, castY, castW, castH) = edge switch
                {
                    Edge.Top => (bx, by + cut, bw, bh - cut),
                    Edge.Left => (bx + cut, by, bw - cut, bh),
                    Edge.Right => (bx, by, bw - cut, bh),
                    Edge.Bottom => (bx, by, bw, bh - cut),
                    _ => (bx, by, bw, bh),
                };
                (castW, castH) = (MathF.Max(castW, 1), MathF.Max(castH, 1));
                var cast = new Vector2(castW, castH);
                var (castShape, castGeometry, castPicture) = (views.CastShape!, views.CastGeometry!, views.CastPicture!);
                PieceTo(castShape, "Size", castShape.Size, cast, size => castShape.Size = size);
                var castInside = Vector2.Max(cast - new Vector2(2 * RimInset), Vector2.One);
                PieceTo(castGeometry, "Size", castGeometry.Size, castInside, size => castGeometry.Size = size);
                PieceTo(castPicture, "SourceSize", castPicture.SourceSize, cast, size => castPicture.SourceSize = size);
                PieceTo(caster, "Size", caster.Size, cast, size => caster.Size = size);
                PieceTo(caster, new Vector3(castX, castY, 0));
                if (views.CastSprite is { } sprite)
                {
                    // With the beak, the whole outline from where it starts, cut where the caster is.
                    sprite.Geometry = pointed is null ? castGeometry : views.BeakCast;
                    sprite.Offset = pointed is null ? Vector2.Zero : new Vector2(bx - castX, by - castY);
                }
            }
            var (plate, outline, rimOf, rimShape) = (views.Plate, views.Outline, views.Rim, views.RimShape);
            var plateInside = Vector2.Max(new Vector2(w, h) - new Vector2(2 * RimInset), Vector2.One);
            PieceTo(outline, "Size", outline.Size, plateInside, size => outline.Size = size);
            PieceTo(plate, "Size", plate.Size, new Vector2(bw, bh), size => plate.Size = size);
            PieceTo(plate, new Vector3(bx, by, 0));
            PieceTo(rimOf, "Size", rimOf.Size, new Vector2(bw, bh), size => rimOf.Size = size);
            PieceTo(rimOf, new Vector3(bx, by, 0));
            plate.Clip = pointed is null ? views.RoundClip : views.BeakClip;
            views.RimLine.Geometry = pointed is null ? rimShape : views.BeakRim;
            // Never less than nothing: an arranged folder whose lists are all empty has no height (§297),
            // and a size below zero throws, leaving what stands outside it unplaced.
            var rimSize = Vector2.Max(new Vector2(w - stroke, h - stroke), Vector2.Zero);
            PieceTo(rimShape, "Size", rimShape.Size, rimSize, size => rimShape.Size = size);
            // §704: Liquid glass's light where the panel stands now, inside its rim as its plate is.
            if (views is { GlazeView: { } glazed, Glaze: { } glaze })
            {
                glazed.Offset = new Vector3(x, y, 0);
                glazed.Size = new Vector2(w, h);
                var inside = RimInset;
                glaze.Place(plateInside, MathF.Max(0, Px(Theme.CornerDip) - inside), new Vector2(inside, inside));
            }
            if (views.RimGradient is { } rimGradient)
            {
                GlassPaint.Down(rimGradient, new Vector2(w, h));
            }
        }
    }

    /// <summary>
    /// §584: <paramref name="views"/>' outline with the beak <paramref name="at"/> for a <paramref name="w"/> × <paramref name="h"/>
    /// panel — the plate's (the material inside its rim where the rim lies outside it), the rim's (its line's middle) and the
    /// caster's, each a path of panel and beak in one (CutPath.Beaked) —, made the first time and given new paths only where
    /// the panel's size, the beak or the rim changed; false where Direct2D made none, and the panel keeps its rounded rectangle.
    /// </summary>
    private bool Beaked(Compositor c, PanelChrome views, float w, float h, (Edge Side, float Tip, float Tall) at)
    {
        var (inset, stroke, radius) = (RimInset, Rim, Px(Theme.CornerDip));
        var key = (w, h, at.Side, at.Tip, at.Tall, inset, stroke, radius);
        if (views.BeakMadeFor == key)
        {
            return true;
        }
        if (CutPath.Beaked(w, h, radius, inset, at.Side, at.Tip, at.Tall) is not { } plate
            || CutPath.Beaked(w, h, radius, stroke / 2, at.Side, at.Tip, at.Tall) is not { } rim)
        {
            return false;
        }
        T Own<T>(T value)
            where T : IDisposable
        {
            views.BeakMade.Add(value);
            return value;
        }
        views.BeakPlate ??= Own(c.CreatePathGeometry());
        views.BeakClip ??= Own(c.CreateGeometricClip(views.BeakPlate));
        views.BeakRim ??= Own(c.CreatePathGeometry());
        views.BeakCast ??= Own(c.CreatePathGeometry());
        // Never a Path of null (§476: dcomp.dll crashed on one): each made path set, the last kept where none was made.
        views.BeakPlate.Path = plate;
        views.BeakRim.Path = rim;
        views.BeakCast.Path = plate;
        views.BeakMadeFor = key;
        return true;
    }

    /// <summary>
    /// The tokens this popup is drawn in for <paramref name="look"/>: a folder's (<see cref="FlyoutTheme.Folder"/>,
    /// §334), a taskbar flyout's (<see cref="FlyoutTheme.Taskbar"/>), or a menu's (<see cref="FlyoutTheme.For"/>).
    /// </summary>
    internal FlyoutTheme ThemeFor(Look look) =>
        Folder ? FlyoutTheme.Folder(look) : Taskbar ? FlyoutTheme.Taskbar(look) : Entrance == PopupEntrance.Tip ? FlyoutTheme.Tip(look) : FlyoutTheme.For(look);

    /// <summary>
    /// §334: the open folder drawn in <paramref name="look"/> where it stands: its panels dressed in the
    /// folders' look as it is now (a material, an opacity or a colour changed in its menu), in place, their
    /// places and the content kept. Only a folder's pieced panel (<see cref="Pieces"/>); nothing for another.
    /// </summary>
    internal void Restyle(Look look)
    {
        if (!open || Pieces is null || compositor is not { } c)
        {
            return;
        }
        Look = look;
        Theme = ThemeFor(look);
        Wear(c);
    }

    /// <summary>
    /// The display <paramref name="work"/> is on, in screen px: what a popup placed in it shows on, its
    /// window's frame (<see cref="FrameOn"/>). Grown to hold <paramref name="work"/> where that runs past
    /// it: a made-up screen of a test, larger than the display it is drawn on — never the dock's, whose
    /// work areas are its displays'.
    /// </summary>
    private static RECT DisplayOf(RECT work)
    {
        var monitor = MonitorFromRect(&work, MONITOR.MONITOR_DEFAULTTONEAREST);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        return GetMonitorInfoW(monitor, &info) ? Union(info.rcMonitor, work) : work;
    }

    private static RECT Union(RECT a, RECT b) => b.right <= b.left || b.bottom <= b.top
        ? a
        : new RECT { left = Math.Min(a.left, b.left), top = Math.Min(a.top, b.top), right = Math.Max(a.right, b.right), bottom = Math.Max(a.bottom, b.bottom) };

    /// <summary>
    /// §355: a popup's window on <paramref name="display"/>: all of it but a px along its right and its
    /// bottom. Windows takes a window that covers a whole display for a full screen app — do not disturb
    /// turns itself on, a taskbar steps back — and a popup's window, nearly all of it clear, is none; the
    /// px it leaves out is one no popup reaches into (they keep 8 to 12 px from the work area's ends).
    /// </summary>
    internal static RECT FrameOn(RECT display) =>
        display with { right = Math.Max(display.left, display.right - 1), bottom = Math.Max(display.top, display.bottom - 1) };

    private static bool Same(RECT a, RECT b) => a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;

    private void HideNow()
    {
        // Closed means nothing of the owner's kept: the next showing draws afresh. What it drew goes once
        // the window is out of sight (Surfaces), never before it, so what was shown stays until the window
        // goes; the chrome stays for the next showing.
        var close = TakeShowing();
        clock.Snap(false);
        if (Hwnd != HWND.NULL)
        {
            _ = KillTimer(Hwnd, HideTimer);
            _ = KillTimer(Hwnd, MotionTimer);
            // Clear in the frame's commit, out of sight at its fence (§359): it goes with whatever else the
            // frame changed, and shows nothing if it comes into sight again before its next content.
            Try(() =>
            {
                if (root is not null)
                {
                    root.StopAnimation("Opacity");
                    root.Opacity = 0;
                }
            });
            Surfaces.Hide(Hwnd, close);
        }
        else
        {
            close();
        }
    }

    /// <summary>The window is gone (Alt+F4 or a teardown): drop what belonged to it.</summary>
    private void Forget()
    {
        open = false;
        Inside = false;
        tracking = false;
        Leave();
        CloseAll(made);
        TakeChrome()();
        if (target is { } old)
        {
            CloseAll([old]);
            target = null;
        }
        Hwnd = HWND.NULL;
    }

    private void EnsureWindow(Gfx gfx)
    {
        compositor = gfx.Compositor;
        if (Hwnd != HWND.NULL && IsWindow(Hwnd) && target is not null)
        {
            return;
        }
        HWND hwnd;
        // Only a menu activates, for the keys.
        uint style = (uint)(WS.WS_EX_TOOLWINDOW | WS.WS_EX_TOPMOST | WS.WS_EX_NOREDIRECTIONBITMAP)
            | (Mode == FlyoutMode.Menu ? 0u : (uint)WS.WS_EX_NOACTIVATE);
        fixed (char* name = className)
        fixed (char* title = "Karpo")
        {
            hwnd = CreateWindowExW(style, name, title, WS.WS_POPUP, 0, 0, 1, 1, HWND.NULL, HMENU.NULL, instance, null);
        }
        if (hwnd == HWND.NULL)
        {
            // Named, because the one way this fails in practice is a class nobody registered, and
            // "CreateWindowExW failed: 0x8007057F" says neither which class nor what to do. A
            // popup is given its class name by whoever builds it (a test hands in one of its own),
            // so the name is the whole of the answer.
            throw Failed($"{nameof(CreateWindowExW)} for the window class \"{className}\"");
        }
        try
        {
            _ = SetWindowLongPtrW(hwnd, GWLP.GWLP_USERDATA, userData);
            BOOL on = BOOL.TRUE;
            // Without it the host backdrop brush has nothing to show.
            _ = DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_USE_HOSTBACKDROPBRUSH, &on, (uint)sizeof(BOOL));
            // The popup animates itself, and its corners are its own: a
            // rounded or animated window would cut off the shadow.
            _ = DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &on, (uint)sizeof(BOOL));
            int square = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            _ = DwmSetWindowAttribute(hwnd, (uint)DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &square, sizeof(int));
            target = CreateTarget(gfx.Compositor, hwnd);
        }
        catch
        {
            _ = SetWindowLongPtrW(hwnd, GWLP.GWLP_USERDATA, 0);
            Surfaces.Destroy(hwnd);
            throw;
        }
        Hwnd = hwnd;
        tracking = false;
        Inside = false;
    }

    private static DesktopWindowTarget CreateTarget(Compositor compositor, HWND hwnd)
    {
        var desktop = ComUtil.Query<ICompositorDesktopInterop>(compositor);
        try
        {
            IDesktopWindowTarget* raw = null;
            ComUtil.Check(desktop->CreateDesktopWindowTarget(hwnd, BOOL.TRUE, &raw));
            return ComUtil.Adopt<DesktopWindowTarget>(raw);
        }
        finally
        {
            desktop->Release();
        }
    }

    /// <summary>
    /// A low-level mouse hook while a menu is open. A click on another app takes the activation
    /// away and ends the menu that way, but a click on the bar does not (the bar never activates),
    /// and neither does anything if the menu never got the foreground. Put in afresh for every
    /// showing: Windows takes a low-level hook away, without a word, once it answered too slowly.
    /// It hears the presses on the desktop that has the input, which need not be the menu's
    /// (<see cref="Pressed"/>). It runs on a thread of its own and only posts here (PressHook.cs):
    /// on this thread, anything slow done while a menu is open froze the pointer of the whole machine.
    /// </summary>
    private static void Hook() => PressHook.Listen();

    /// <summary>This host is no longer open: it uses up no press any more, and the hook goes with the last open one.</summary>
    private void Leave()
    {
        PressHook.LetThrough(this);
        if (showing is not null && showing.Remove(this) && showing.Count == 0)
        {
            PressHook.Stop();
        }
    }

    /// <summary>
    /// What the hook heard, posted to the thread whose hosts these are (see showing): a
    /// button going down at <paramref name="at"/> is handed to each of this thread's open menus
    /// (<see cref="PointerDown"/>) when it was pressed on the desktop they are on. The hook hears
    /// the presses of whichever desktop has the input, and a menu on another one — the smoke
    /// pass's, a test's — would close for every click made on the person's (<see cref="InputDesktop"/>).
    /// </summary>
    /// <param name="usedUp">The hook used the press up for a menu (<see cref="UsesUpPresses"/>): nothing under it heard of it.</param>
    /// <param name="staged">A stage test's press, on the stage's own desktop (PressHook.StagedBit).</param>
    internal static void Pressed(uint message, POINT at, bool usedUp = false, bool staged = false)
    {
        if (showing is not { Count: > 0 } hosts || !PressHook.IsPress(message) || !staged && !InputDesktop.Holds())
        {
            return;
        }
        Heard = (at, GetTickCount());
        var under = WindowFromPoint(at);
        foreach (var host in hosts.ToArray())
        {
            if (host.open)
            {
                host.PointerDown(at, under, usedUp);
            }
        }
    }

    /// <summary>
    /// §430: the last press the hook told this thread of, where and when (<see cref="Pressed"/>): the popups it closed
    /// went before the press reached the window it was on, which takes it (Dock.Pressing) to know what its press closed.
    /// This thread's, as <see cref="showing"/> is.
    /// </summary>
    internal static (POINT At, uint Tick)? Heard
    {
        get => heard;
        set => heard = value;
    }

    [ThreadStatic]
    private static (POINT At, uint Tick)? heard;

    /// <summary>
    /// A button went down somewhere: outside the panel (and whatever else counts as inside), the
    /// menu ends. Only a post, and only the posted message acts: posted messages are read before
    /// the click itself, so the dock sees the menu closed before it handles the click that closed
    /// it. The focus goes back only for a click on Karpo's own windows; any other app activates
    /// itself. A press on a bar is the bar's (<see cref="PressesHandledBy"/>): it closes the menu
    /// itself, knowing what else the press is for. <paramref name="under"/> is the window at
    /// <paramref name="screen"/>, or null. A press this menu used up (<see cref="UsesUpPresses"/>)
    /// reached nothing else, a bar included: the menu closes for it wherever it was, and the focus
    /// goes back to what had it, which nothing took.
    /// </summary>
    internal void PointerDown(POINT screen, HWND under, bool usedUp = false)
    {
        uint process = 0;
        bool ours = under != HWND.NULL && GetWindowThreadProcessId(under, &process) != 0 && process == GetCurrentProcessId();
        var press = LightDismiss.Classify(
            Contains(screen),
            AlsoInside?.Invoke(screen) ?? false,
            ours && (PressesHandledBy?.Invoke(under) ?? false),
            ours,
            usedUp && UsesUpPresses);
        if (press is HookPress.Ours or HookPress.Elsewhere)
        {
            _ = PostMessageW(Hwnd, WM_KARPO_HOST_DISMISS, (WPARAM)(nuint)generation, (LPARAM)(nint)(press == HookPress.Ours ? 1 : 0));
        }
    }

    // Composition calls on a popup mid-teardown fail harmlessly.
    private static void Try(Action change)
    {
        try
        {
            change();
        }
        catch (Exception)
        {
        }
    }

    internal static void CloseAll(List<IDisposable> objects)
    {
        foreach (var value in objects)
        {
            try
            {
                value.Dispose();
            }
            catch (Exception)
            {
            }
        }
        objects.Clear();
    }

    private static int X(LPARAM lParam) => (short)((nint)lParam & 0xFFFF);

    private static int Y(LPARAM lParam) => (short)(((nint)lParam >> 16) & 0xFFFF);

    /// <summary>Where a <paramref name="length"/> long panel wanting to start at <paramref name="start"/> starts.</summary>
    private static int AlongEdge(int start, int low, int high, int length, int keep) =>
        Math.Max(Math.Min(start, Math.Max(high - keep - length, low)), low + keep);

    // Rust's f32::round, as everywhere in Karpo.
    private static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);

    private static NativeException Failed(string call)
    {
        var last = GetLastError();
        var hr = HRESULT_FROM_WIN32(last);
        // The one error worth spelling out: a window asked for a class that nobody registered.
        var why = last == TerraFX.Interop.Windows.ERROR.ERROR_CANNOT_FIND_WND_CLASS
            ? " — that class is not registered, so whoever registers it either did not run or registered a different name"
            : "";
        return new NativeException($"{call} failed: 0x{hr.Value:X8}{why}", hr.Value);
    }

    internal readonly record struct Area(float X, float Y, float W, float H)
    {
        public bool Contains(float x, float y) => x >= X && x < X + W && y >= Y && y < Y + H;
    }
}
