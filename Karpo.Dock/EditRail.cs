// A section's edit bar (the edit rail): while edit mode is on, a strip beside a section of a bar
// or a place — on its inner side, towards the middle of the screen, never on it — that holds what
// the section has no room to show, and is there only while it has any (EditRailPlan). The adds
// that stood on it went to every display's add button and the add list's band (2026-10-07,
// Dock.EditDeck.cs); the bar's settings and the way out are not here either: they are the display's
// edit bar's — its crumbs' menus, and "Done" or Escape ends edit mode (Dock.EditDeck.cs).
//
// It is a Tool host (FlyoutHost): the Windows 11 flyout chrome, WS_POPUP with
// WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP,
// a DesktopWindowTarget of its own, no appbar, and — the point — never closed by
// itself. A Flyout would not do: light dismiss ends a menu-mode popup on any
// press outside it, the bar's own presses included, and the whole use of the rail
// is that you click the bar while it is open. The dock makes it with edit mode,
// hides it when edit mode ends, and routes its window's messages here
// (Dock.EditRail.cs).
//
//  - **Where it sits.** Beside its section on the bar's inner side, centred along it, 8 DIP clear
//    of the blocks' remove badges (whose outer halves stand past the bar, and whose clicks the
//    rail's window must not take) and 8 DIP off that, inside the work area. Bar at the top: under
//    it. At the bottom: over it. At the left: to its right. At the right: to its left. Where two
//    meet at a corner the later gives way along its section (EditRails.Clear), and none stands on
//    another surface's bar or on a display's add button and tray.
//  - **One line.** Its buttons run along the bar: a row beside a horizontal bar, a column beside a
//    vertical one, never a square.
//  - **Following the bar.** The dock shows the rail again at the end of every build of a bar being
//    edited, and <see cref="Show"/> does nothing when nothing about the placement changed — so the
//    rail follows a bar that grew, shrank, changed height or came out of hiding, at the cost of a
//    comparison. A change of displays or of scale ends edit mode, and the rail goes with it. Moved, its
//    buttons are kept and moved with it in composition, none drawn again (KeptButtons, §363).
//
// What it says: the bar drops a widget it has no room for and says nothing, so the rail is where
// that appears, its glyph the hidden widget's own (or a warning for several), its click that
// widget's menu or a list of them.
//
// Nothing here is drawn by an animation: a hover, a press and the focus ring are brush colours and
// a visual's offset, set the moment they change. The system can stop animations, and a button whose
// lit state never arrived is a button nobody can use.
//
// WinUI's Button measures: 32 DIPs square, corners of 4, a 16-DIP glyph in the middle, 8 DIPs in
// from the panel's edges.

using System.Numerics;
using TerraFX.Interop.DirectX;
using Karpo.Core;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;
using Edge = Karpo.Core.Edge;

namespace Karpo.Dock;

/// <summary>What the edit rail's buttons do. The dock does them (Dock.EditRail.cs).</summary>
public enum EditRailAction
{
    None,

    /// <summary>
    /// Leaves edit mode, by the path Escape takes. No edit bar has a button for it — "Close edit
    /// mode" beside the add button and Escape end it.
    /// </summary>
    Close,

    /// <summary>
    /// The widgets the bar holds but has no room to show (<see cref="Layout.NotShown"/>). The bar
    /// itself says nothing about them on purpose — a badge on a bar already too crowded to hold
    /// its widgets is the last thing that bar needs — so this is the only place they appear, and
    /// the only way to reach one: a widget with no block cannot be clicked, right-clicked or
    /// tabbed to, so without this it is in the config and out of everybody's reach.
    /// </summary>
    NotShown,
}

/// <param name="Anchor">The middle of the button's side away from the bar, on screen: where what it opens goes.</param>
public readonly record struct EditRailEvent(EditRailAction Action, POINT Anchor);

public sealed unsafe class EditRail : IDisposable
{
    private const float RadiusDip = 4;
    private const float GapDip = 8;
    private const float FocusRingDip = 2;
    private const float FocusGapDip = 1;

    // Half the largest remove badge (Layout's 16 DIPs), which stands past the bar.
    private const float BadgeRoomDip = 8;

    private readonly FlyoutHost host;

    /// <summary>The tooltip, kept (§359), as the bar's own says it.</summary>
    private readonly TipView tip;
    private readonly List<CompositionColorBrush?> fills = [];

    // What the bar holds and cannot show, and the button that says so while there is any. Its
    // glyph is a sprite whose brush is swapped when what it says changes (one widget to another,
    // one to several), so a change of message that is not a change of presence re-opens nothing.
    // The surfaces are kept by what they draw, because the same message comes back again and
    // again while a widget is dragged about.
    private readonly List<PartSpec> hidden = [];
    private readonly Dictionary<string, CompositionSurfaceBrush> marks = [];

    /// <summary>§363: its buttons and the focus ring, kept by their keys.</summary>
    private readonly KeptButtons kept;
    private SpriteVisual? notShownMark;
    private int notShownAt = -1;
    private EditRailPlan plan = EditRailPlan.For(1, vertical: false);
    private int hot = -1;
    private int pressed = -1;
    private int focus = -1;
    private int tipFor = -1;
    private Placement? placed;

    // The focus ring, one visual moved to whichever button has the focus.
    private ShapeVisual? ring;
    private CompositionRoundedRectangleGeometry? ringOuter;
    private CompositionRoundedRectangleGeometry? ringInner;

    /// <param name="dock">Goes into both windows' GWLP_USERDATA, so the dock's window procedure finds the dock.</param>
    public EditRail(Gfx gfx, HINSTANCE instance, nint dock)
    {
        // Over the bars it stands beside (§359).
        host = new FlyoutHost(gfx, instance, dock, FlyoutMode.Tool) { Stratum = Stratum.EditBar };
        tip = new TipView(gfx, instance, dock);
        kept = new KeptButtons(host);
    }

    /// <summary>The rail's window; a new one if the last was destroyed, so read it afresh.</summary>
    public HWND Hwnd => host.Hwnd;

    public bool IsOpen => host.IsOpen;

    /// <summary>The rail's own window, or its tooltip's: both come to the dock's procedure.</summary>
    public bool Owns(HWND window) =>
        window != HWND.NULL && (window == host.Hwnd || window == tip.Hwnd);

    /// <summary>How many buttons the rail has, which is the keyboard's ring past the bar's blocks.</summary>
    public int Count => plan.Buttons.Count;

    /// <summary>Which button has the keyboard focus, or -1 when the bar has it.</summary>
    public int Focus => focus;

    /// <summary>Whether the bar is holding widgets it has no room to show.</summary>
    private bool AnyHidden => hidden.Count > 0;

    /// <summary>Where the buttons are, for a test that asks what the open rail is showing.</summary>
    internal EditRailPlan Plan => plan;

    private sealed record Placement(Gfx Gfx, Look Look, float Scale, RECT Bar, Edge Edge, RECT Work, int Shift);

    /// <summary>
    /// How far along its section the rail was last moved off its middle, in px, to keep clear of
    /// another section's (<see cref="EditRails.Clear"/>).
    /// </summary>
    public int Shift => placed?.Shift ?? 0;

    /// <summary>Where the rail's panel stands on the screen; null while it is not open.</summary>
    public RECT? Panel => IsOpen ? host.PanelOnScreen : null;

    /// <summary>
    /// Opens the rail beside <paramref name="bar"/> (its section on screen) on its inner side, inside
    /// <paramref name="work"/>, saying what the bar holds and could not show
    /// (<paramref name="notShown"/>, <see cref="Layout.NotShown"/>). The dock calls it after every
    /// build of the bar it belongs to, so it does as little as the change needs: nothing when
    /// nothing changed; a new glyph and tooltip when only what the rail says changed; and a new
    /// plan only when the placement changed or the "not shown" button came or went — which opens
    /// the rail, or closes it: a section showing everything has none.
    /// </summary>
    /// <param name="shift">How far along the section to move it off its middle, in px (<see cref="EditRails.Clear"/>).</param>
    public void Show(Gfx gfx, Look look, float scale, RECT bar, Edge edge, RECT work, IReadOnlyList<PartSpec> notShown, int shift = 0)
    {
        var next = new Placement(gfx, look, scale, bar, edge, work, shift);
        var had = AnyHidden;
        var said = hidden.Count == notShown.Count && hidden.SequenceEqual(notShown);
        if (!said)
        {
            hidden.Clear();
            hidden.AddRange(notShown);
        }
        if (IsOpen && placed is { } last && Same(last, next) && had == AnyHidden)
        {
            if (!said)
            {
                MarkNotShown();
                if (tipFor >= 0 && tipFor == notShownAt)
                {
                    // What the tip says has changed under it, so it is said again.
                    tipFor = -1;
                    ShowTip(hot);
                }
            }
            return;
        }
        placed = next;
        Open();
    }

    public void Hide()
    {
        hot = pressed = -1;
        focus = -1;
        tipFor = -1;
        if (host.Hwnd != HWND.NULL && GetCapture() == host.Hwnd)
        {
            _ = ReleaseCapture();
        }
        tip.Hide();
        host.Hide();
    }


    /// <summary>
    /// A message of <see cref="Hwnd"/>, which the dock routes here first: hover and press states,
    /// the tooltip, and a click's action. A press acts on its release and only over the button it
    /// went down on, as every button in Windows does.
    /// </summary>
    public EditRailEvent Message(uint msg, WPARAM wParam, LPARAM lParam)
    {
        var happened = host.Message(msg, wParam, lParam);
        if (happened.Kind == FlyoutHostEventKind.DpiChanged && placed is { } last && Hwnd != HWND.NULL)
        {
            // The display changed scale under the rail: shown again at the new one.
            placed = last with { Scale = GetDpiForWindow(Hwnd) / 96f };
            Open();
            return default;
        }
        switch (msg)
        {
            case WM.WM_MOUSEMOVE:
                Hot(host.ToPanel(lParam, out var point) ? plan.At(point) : -1);
                break;
            case WM.WM_MOUSELEAVE:
                Hot(-1);
                break;
            case WM.WM_LBUTTONDOWN:
                if (host.ToPanel(lParam, out var down) && plan.At(down) is var button and >= 0)
                {
                    pressed = button;
                    ShowTip(-1);
                    _ = SetCapture(Hwnd);
                    Paint();
                }
                break;
            case WM.WM_LBUTTONUP:
            {
                var was = pressed;
                pressed = -1;
                if (GetCapture() == Hwnd)
                {
                    _ = ReleaseCapture();
                }
                var up = host.ToPanel(lParam, out var released) ? plan.At(released) : -1;
                Hot(up);
                Paint();
                if (was >= 0 && was == up)
                {
                    // A click puts the keyboard's focus where the pointer acted, so Tab carries on
                    // from the button just used rather than from the bar.
                    SetFocus(up);
                    return new EditRailEvent(plan.Buttons[was].Action, AnchorOf(plan.Buttons[was]));
                }
                break;
            }
            case WM.WM_CAPTURECHANGED:
                if (pressed >= 0 && (HWND)(nint)lParam != Hwnd)
                {
                    pressed = -1;
                    Paint();
                }
                break;
        }
        return default;
    }

    /// <summary>
    /// The keyboard's focus one place along the rail. False when the step runs off either end,
    /// which is where it goes back to the bar's own blocks: the rail and the bar are one Tab ring,
    /// and the rail is the last stretch of it. With <paramref name="clamp"/> the focus stays on the
    /// end button instead and the answer is still false — that is the arrows, which stay where they
    /// are rather than crossing from the rail to the bar sideways.
    /// </summary>
    public bool MoveFocus(int step, bool clamp = false)
    {
        if (plan.Buttons.Count == 0 || step == 0)
        {
            return false;
        }
        var to = focus < 0 ? (step > 0 ? 0 : plan.Buttons.Count - 1) : focus + Math.Sign(step);
        if (to < 0 || to >= plan.Buttons.Count)
        {
            if (!clamp)
            {
                SetFocus(-1);
            }
            return false;
        }
        SetFocus(to);
        return true;
    }

    /// <summary>The focus leaves the rail (the bar took it, or edit mode is ending).</summary>
    public void ClearFocus() => SetFocus(-1);

    /// <summary>What the focused button does, and where what it opens goes; <see cref="EditRailAction.None"/> for no focus.</summary>
    public EditRailEvent Chosen() =>
        focus >= 0 && focus < plan.Buttons.Count
            ? new EditRailEvent(plan.Buttons[focus].Action, AnchorOf(plan.Buttons[focus]))
            : default;

    public void Dispose()
    {
        tip.Dispose();
        kept.Release();
        host.Dispose();
    }

    /// <summary>Tests only (§363): the elements made, and surfaces drawn for them, since it was made.</summary>
    internal (int Elements, int Surfaces) Made => kept.Made;

    /// <summary>Plans the buttons, opens the panel and draws them into it.</summary>
    private void Open()
    {
        if (placed is not { } at)
        {
            return;
        }
        var (gfx, look, scale, bar, edge, work, shift) = at;
        var keptFocus = focus >= 0 && focus < plan.Buttons.Count ? plan.Buttons[focus].Action : EditRailAction.None;
        hot = pressed = -1;
        focus = -1;
        tipFor = -1;
        tip.Hide();
        var vertical = edge is Edge.Left or Edge.Right;
        // One line along the bar, never a square (EditRailPlan).
        plan = EditRailPlan.For(scale, vertical, AnyHidden);
        fills.Clear();
        if (plan.Buttons.Count == 0)
        {
            // Nothing hidden: no edit bar.
            host.Hide();
            return;
        }
        // §363: moved with its section while it stands, its content stays — the kept buttons are reconciled in place.
        host.Show(gfx, look, scale, plan.Panel, Hung(scale, bar, edge, work, shift), keep: host.IsOpen);
        Draw(gfx, scale);
        if (keptFocus != EditRailAction.None)
        {
            SetFocus(IndexOf(keptFocus));
        }
        // The pointer may be resting on the button when the rail is opened again under it: it stays
        // lit and its tooltip stays up, rather than going dark until the pointer next moves.
        POINT cursor;
        if (GetCursorPos(&cursor) && host.ToPanel(cursor, out var over))
        {
            Hot(plan.At(over));
        }
    }

    /// <summary>
    /// Where the rail beside <paramref name="bar"/> (its section on screen) hangs from, <paramref name="shift"/>
    /// px along it off its middle: on its inner side, clear of the blocks' remove badges, whose outer halves
    /// stand past the bar — the rail's window must not take their clicks.
    /// </summary>
    private static FlyoutPlacement Hung(float scale, RECT bar, Edge edge, RECT work, int shift)
    {
        var vertical = edge is Edge.Left or Edge.Right;
        var clear = (int)MathF.Round(BadgeRoomDip * scale, MidpointRounding.AwayFromZero);
        // Along the bar: centred on its section, moved off it where it would stand on another.
        var along = (vertical ? (bar.top + bar.bottom) / 2 : (bar.left + bar.right) / 2) + shift;
        var anchor = edge switch
        {
            Edge.Top => new POINT { x = along, y = bar.bottom + clear },
            Edge.Left => new POINT { x = bar.right + clear, y = along },
            Edge.Right => new POINT { x = bar.left - clear, y = along },
            _ => new POINT { x = along, y = bar.top - clear },
        };
        return new FlyoutPlacement(anchor, work, edge, GapDip: GapDip);
    }

    /// <summary>
    /// §359: where the rail's panel would stand on the screen beside <paramref name="bar"/>, shown with
    /// <paramref name="shift"/> — null where it would have no button (nothing hidden) — worked out before it
    /// is shown, so the rails keep clear of each other and each is shown once, where it stands
    /// (<see cref="EditRails.Clear"/>).
    /// </summary>
    internal static RECT? PanelFor(float scale, RECT bar, Edge edge, RECT work, bool anyHidden, int shift)
    {
        var plan = EditRailPlan.For(scale, edge is Edge.Left or Edge.Right, anyHidden);
        return plan.Buttons.Count == 0 ? null
            : Hung(scale, bar, edge, work, shift).PanelAt((int)MathF.Ceiling(plan.Panel.X), (int)MathF.Ceiling(plan.Panel.Y), scale);
    }

    /// <summary>Where the button that does <paramref name="action"/> stands in the plan, or -1.</summary>
    private int IndexOf(EditRailAction action)
    {
        for (var index = 0; index < plan.Buttons.Count; index++)
        {
            if (plan.Buttons[index].Action == action)
            {
                return index;
            }
        }
        return -1;
    }

    /// <summary>What a button of the rail is drawn from, besides where it stands (§363): a button that only moved is kept.</summary>
    private readonly record struct RailSig(EditRailAction Action, string Glyph, bool Warns, Vector2 Size, float Radius, float Hair, float Box, Color Stroke, (float R, float G, float B, float A) Ink);

    /// <summary>What the focus ring is drawn from (§363).</summary>
    private readonly record struct RingSig(float Thickness, float Hair, Color Outer, Color Inner);

    /// <summary>
    /// The buttons drawn, only what changed (§363): each kept by its place and what it does (KeptButtons), moved
    /// with the rail, made again alone where what it is drawn from changed; the focus ring kept over them.
    /// </summary>
    private void Draw(Gfx gfx, float scale)
    {
        var theme = host.Theme;
        float Px(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var radius = Px(RadiusDip);
        var hair = MathF.Max(1f, MathF.Floor(scale));
        var box = Px(EditRailPlan.GlyphDip);
        kept.Begin();
        notShownAt = -1;
        notShownMark = null;
        for (var index = 0; index < plan.Buttons.Count; index++)
        {
            var each = plan.Buttons[index];
            var warns = each.Action == EditRailAction.NotShown;
            var sig = new RailSig(each.Action, warns ? "" : each.Glyph, warns, each.Rect.Size, radius, hair, box,
                warns ? Warning(theme).Stroke : theme.ControlStroke, (theme.Text.r, theme.Text.g, theme.Text.b, theme.Text.a));
            var button = kept.Place($"{index}:{each.Action}", each.Rect, sig, made =>
            {
                kept.Shape(new RectF(0f, 0f, sig.Size.X, sig.Size.Y), radius, sig.Stroke, hair);
                var inside = new RectF(MathF.Floor((sig.Size.X - box) / 2f), MathF.Floor((sig.Size.Y - box) / 2f), box, box);
                // The "not shown" button is a button like the others; only its glyph is the rail's own, because it
                // names the one hidden widget or warns of several (MarkNotShown), and a sprite whose brush is
                // swapped lets one message follow another without the button being drawn again.
                made.Tag = warns ? kept.Sprite(null, inside) : kept.Sprite(gfx.Glyph(sig.Glyph, box, theme.Text, box), inside);
            });
            fills.Add(button.Fill);
            if (warns)
            {
                notShownAt = index;
                notShownMark = button.Tag as SpriteVisual;
                MarkNotShown();
            }
        }
        // The focus ring, made once and moved: two strokes, as WinUI draws one.
        var ringSig = new RingSig(Px(FocusRingDip), hair, theme.FocusOuter, theme.FocusInner);
        var held = kept.Place("ring", new RectF(0f, 0f, plan.Panel.X, plan.Panel.Y), ringSig, made =>
        {
            var c = host.Compositor;
            var outer = kept.Own(c.CreateRoundedRectangleGeometry());
            var inner = kept.Own(c.CreateRoundedRectangleGeometry());
            var ringVisual = kept.Own(c.CreateShapeVisual());
            var outerShape = kept.Own(c.CreateSpriteShape(outer));
            outerShape.StrokeBrush = kept.Own(c.CreateColorBrush(ringSig.Outer));
            outerShape.StrokeThickness = ringSig.Thickness;
            var innerShape = kept.Own(c.CreateSpriteShape(inner));
            innerShape.StrokeBrush = kept.Own(c.CreateColorBrush(ringSig.Inner));
            innerShape.StrokeThickness = ringSig.Hair;
            ringVisual.Shapes.Add(outerShape);
            ringVisual.Shapes.Add(innerShape);
            ringVisual.IsVisible = false;
            made.Visual.Children.InsertAtTop(ringVisual);
            made.Tag = (ringVisual, outer, inner);
        }, on: true);
        (ring, ringOuter, ringInner) = ((ShapeVisual, CompositionRoundedRectangleGeometry, CompositionRoundedRectangleGeometry))held.Tag!;
        ring.IsVisible = false;
        kept.End();
        // Each button's fill as it stands now: at rest, under the pointer or pressed.
        Paint();
    }

    /// <summary>
    /// What the "not shown" button's glyph says now: the hidden widget's own glyph when there is
    /// one — naming it at a glance is worth more than a count — and a warning when there are
    /// several. The button is there only while something is hidden, so there is always something
    /// to say. The surfaces are kept by what they draw, with their brushes, because the same message
    /// comes back again and again while a widget is dragged about.
    /// </summary>
    private void MarkNotShown()
    {
        if (notShownMark is null || !AnyHidden || placed is not { } at)
        {
            return;
        }
        var box = MathF.Round(EditRailPlan.GlyphDip * at.Scale, MidpointRounding.AwayFromZero);
        var glyph = hidden.Count == 1 ? hidden[0].Kind.Glyph() : EditRailPlan.Glyphs.NotShown;
        var ink = Warning(host.Theme).Glyph;
        var key = $"{glyph}@{box}:{ink.r},{ink.g},{ink.b},{ink.a}";
        if (!marks.TryGetValue(key, out var brush))
        {
            brush = kept.Own(host.Compositor.CreateSurfaceBrush(kept.Own(at.Gfx.Glyph(glyph, box, ink, box))));
            brush.Stretch = CompositionStretch.None;
            brush.HorizontalAlignmentRatio = 0f;
            brush.VerticalAlignmentRatio = 0f;
            marks[key] = brush;
        }
        notShownMark.Brush = brush;
    }

    /// <summary>
    /// What the slot's tooltip says. One widget is named, because a name is what tells somebody
    /// which widget to make room for; several are counted, because seven names in a tooltip is a
    /// list, and the list is what a click opens.
    /// </summary>
    private string NotShownTitle() => hidden.Count == 1
        ? S.DockEditRailNotShownOne(hidden[0].Kind.Title())
        : S.DockEditRailNotShownMany(hidden.Count);

    /// <summary>A button's fill for its state: rest, under the pointer, pressed; the warning's own for the "not shown" button.</summary>
    private Color FillOf(bool warns, bool over, bool down)
    {
        var theme = host.Theme;
        if (warns)
        {
            var warning = Warning(theme);
            return down ? warning.Pressed : over ? warning.Hover : warning.Fill;
        }
        return down ? theme.ControlFillPressed : over ? theme.ControlFillHover : theme.ControlFill;
    }

    /// <summary>The "not shown" button's colours: its fill at rest, under the pointer and pressed, its edge and its glyph.</summary>
    internal readonly record struct WarningLook(Color Fill, Color Hover, Color Pressed, Color Stroke, DXGI_RGBA Glyph);

    /// <summary>
    /// The "not shown" button in Windows' warning orange (2026-10-11: "it should be more visible. it
    /// should be red or orange"): the orange of Windows Update's warning shield (#FC9A03,
    /// RestartDot.Orange), solid, the same on light and dark as Windows draws it, with the glyph in
    /// black on it as Windows' warning icons carry their mark — so it stands out from the grey
    /// buttons and from either panel. Lighter under the pointer and darker pressed, as an accent
    /// button goes. In high contrast the system's highlight with its highlight text, as every
    /// warning of Karpo's is there.
    /// </summary>
    internal static WarningLook Warning(FlyoutTheme theme) => theme.HighContrast
        ? new(theme.Accent, theme.AccentHover, theme.AccentPressed, theme.ControlStroke, theme.TextOnAccent)
        : new(
            Color.FromArgb(255, 0xFC, 0x9A, 0x03),
            Color.FromArgb(255, 0xFD, 0xAA, 0x33),
            Color.FromArgb(255, 0xE0, 0x88, 0x00),
            Color.FromArgb(255, 0xD0, 0x7E, 0x00),
            Gfx.Ink(0f, 0f, 0f, 0.9f));

    private void Hot(int button)
    {
        if (button == hot)
        {
            return;
        }
        hot = button;
        Paint();
        ShowTip(button);
    }

    private void Paint()
    {
        for (var index = 0; index < fills.Count; index++)
        {
            if (fills[index] is { } fill)
            {
                fill.Color = FillOf(index == notShownAt, index == hot, index == pressed && index == hot);
            }
        }
    }

    private void SetFocus(int button)
    {
        var to = button >= 0 && button < plan.Buttons.Count ? button : -1;
        if (to == focus)
        {
            return;
        }
        focus = to;
        if (ring is null || ringOuter is null || ringInner is null)
        {
            return;
        }
        if (focus < 0 || placed is not { } at)
        {
            ring.IsVisible = false;
            return;
        }
        float Px(float dip) => MathF.Round(dip * at.Scale, MidpointRounding.AwayFromZero);
        var thickness = Px(FocusRingDip);
        var room = thickness + Px(FocusGapDip);
        var rect = plan.Buttons[focus].Rect;
        var outer = new RectF(rect.X - room, rect.Y - room, rect.Width + 2f * room, rect.Height + 2f * room);
        ring.Offset = new Vector3(outer.X, outer.Y, 0f);
        ring.Size = outer.Size;
        var half = thickness / 2f;
        ringOuter.Offset = new Vector2(half, half);
        ringOuter.Size = new Vector2(MathF.Max(0f, outer.Width - thickness), MathF.Max(0f, outer.Height - thickness));
        ringOuter.CornerRadius = new Vector2(Px(RadiusDip) + room, Px(RadiusDip) + room);
        ringInner.Offset = new Vector2(thickness, thickness);
        ringInner.Size = new Vector2(MathF.Max(0f, outer.Width - 2f * thickness), MathF.Max(0f, outer.Height - 2f * thickness));
        ringInner.CornerRadius = new Vector2(Px(RadiusDip) + room / 2f, Px(RadiusDip) + room / 2f);
        ring.IsVisible = true;
    }

    /// <summary>
    /// The tooltip for <paramref name="button"/>, on the button's side away from the bar so it
    /// never lies under the pointer: a tip of its own (TipView, §359), as the rail's panel is clipped
    /// to its own rounded shape, so nothing of the rail can be drawn outside it.
    /// </summary>
    private void ShowTip(int button)
    {
        if (button == tipFor)
        {
            return;
        }
        tipFor = button;
        if (button < 0 || button >= plan.Buttons.Count || placed is not { } at || !host.IsOpen)
        {
            tip.Hide();
            return;
        }
        var (gfx, look, scale, _, edge, work, _) = at;
        var title = button == notShownAt ? NotShownTitle() : plan.Buttons[button].Title;
        tip.Show(gfx, look, scale, title, AnchorOf(plan.Buttons[button]), work, edge);
    }

    /// <summary>The middle of <paramref name="button"/>'s side away from the bar, on screen.</summary>
    private POINT AnchorOf(RailButton button)
    {
        var panel = host.PanelOnScreen;
        var r = button.Rect;
        var middleX = panel.left + (int)(r.X + r.Width / 2f);
        var middleY = panel.top + (int)(r.Y + r.Height / 2f);
        return placed?.Edge switch
        {
            Edge.Top => new POINT { x = middleX, y = panel.top + (int)r.Bottom },
            Edge.Left => new POINT { x = panel.left + (int)r.Right, y = middleY },
            Edge.Right => new POINT { x = panel.left + (int)r.X, y = middleY },
            _ => new POINT { x = middleX, y = panel.top + (int)r.Y },
        };
    }

    private static bool Same(Placement a, Placement b) =>
        ReferenceEquals(a.Gfx, b.Gfx) && a.Look == b.Look && a.Scale == b.Scale && a.Edge == b.Edge && a.Shift == b.Shift && Equal(a.Bar, b.Bar) && Equal(a.Work, b.Work);

    private static bool Equal(RECT a, RECT b) =>
        a.left == b.left && a.top == b.top && a.right == b.right && a.bottom == b.bottom;
}
