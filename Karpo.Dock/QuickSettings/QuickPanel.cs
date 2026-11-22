// What a quick-settings flyout shows, managed: the header, the body and the
// footer, each a list of controls (Widget) its page placed. The panel gives each
// control a composition surface of its own, paints it when it changed, routes
// the pointer, the wheel and the keys to it, moves the focus with Tab and the
// arrows, and scrolls the body with the wheel once it grows past the panel's
// most (then a thin indicator shows how far). It asks the host for the size its
// content needs and fills what it is given (Fit), at once whatever the pointer
// is doing; what it is given beyond that (its whole px) goes to the body.
//
// Nothing is animated: every change sets its final value, so a DWM that runs no
// composition animations shows the same thing.

using System.Numerics;
using Karpo.Dock.Native;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Dock.QuickSettings;

/// <summary>
/// What a page shows, placed: each widget's <see cref="Widget.Bounds"/> is in its layer's px (the body's
/// from the top of its content). <paramref name="MaxHeight"/> is the most the whole panel may be.
/// </summary>
/// <param name="Over">
/// What is drawn over the rest, in the panel's own px: a dropdown's list, which hangs over the
/// page rather than growing it, so opening one moves nothing. It takes no part in the layout, is
/// never scrolled, and the pointer and the keyboard find it before anything under it.
/// </param>
internal sealed record PanelContent(
    float Width,
    IReadOnlyList<Widget> Header,
    float HeaderHeight,
    IReadOnlyList<Widget> Body,
    float BodyHeight,
    IReadOnlyList<Widget> Footer,
    float FooterHeight,
    float MaxHeight,
    IReadOnlyList<Widget>? Over = null)
{
    /// <summary>What hangs over the page, never null.</summary>
    public IReadOnlyList<Widget> Above => Over ?? [];
}

internal sealed unsafe class QuickPanel
{
    /// <summary>A control's surface reaches this far past its rectangle, for the focus rectangle.</summary>
    private const float MarginDip = 4;

    /// <summary>The panel's own timer id on the flyout's window: the text caret's blink.</summary>
    internal const nuint CaretTimer = 0x51;

    private readonly FlyoutHost host;
    private readonly Dictionary<Widget, Shown> shown = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<Widget> dirty = new(ReferenceEqualityComparer.Instance);
    private PanelContent content = new(0, [], 0, [], 0, [], 0, 0);

    // How much of the body the content itself asks to show, and how much shows: more than it asked
    // where the host gave the panel more (its whole px, Fit).
    private float natural;
    private float viewport;
    private float scroll;
    // §363: its layers, made once and kept through its showings (Attach), and what they are made of.
    private readonly List<IDisposable> owned = [];
    private ContainerVisual? root;
    private ContainerVisual? headerLayer;
    private ContainerVisual? bodyWindow;
    private ContainerVisual? bodyLayer;
    private ContainerVisual? footerLayer;
    private ContainerVisual? overLayer;
    private SpriteVisual? indicator;
    private CompositionRoundedRectangleGeometry? indicatorShape;
    private Widget? hot;
    private Widget? pressed;
    private Widget? focused;
    private bool capturing;
    private uint lastPress;
    private Vector2 lastPressAt;
    private int clicks;
    private bool blinking;

    public QuickPanel(FlyoutHost host, QuickContext context)
    {
        this.host = host;
        Context = context;
        madeFor = host.Hwnd;
        // narrator: a screen reader finds the panel's controls (Accessibility\FlyoutUia.cs).
        Uia.Register(host.Hwnd, new PanelAccess(this));
    }

    // The window it was made in: one made again after Alt+F4 has a panel of its own (§363).
    private readonly HWND madeFor;

    /// <summary>§363: whether its host still has the window it was made in, so it may be kept for the next showing.</summary>
    internal bool OnItsWindow => host.Hwnd != HWND.NULL && host.Hwnd == madeFor;

    /// <summary>The popup the panel is in.</summary>
    internal FlyoutHost Host => host;

    public QuickContext Context { get; }

    public HWND Hwnd => host.Hwnd;

    /// <summary>
    /// The panel's size for what it shows now: the header, the body up to what fits under the most,
    /// the footer. What it asks the host for; the host may give a fraction of a px more (<see cref="Fit"/>).
    /// </summary>
    public Vector2 Size => new(content.Width, content.HeaderHeight + natural + content.FooterHeight);

    /// <summary>The widget with the keyboard, if any.</summary>
    public Widget? Focused => focused;

    /// <summary>How far the body is scrolled, in px.</summary>
    public float Scroll => scroll;

    /// <summary>How much of the body shows.</summary>
    public float Viewport => viewport;

    public PanelContent Content => content;

    /// <summary>
    /// Takes what a page placed: widgets that stay keep their surface and state, new ones are painted, gone ones
    /// closed. <paramref name="repaint"/> false (§363): the widgets that stay are painted again only where their
    /// owner said they changed (<see cref="Widget.Invalidate"/>) or their size did.
    /// </summary>
    public void Set(PanelContent next, bool repaint = true)
    {
        var before = content;
        content = next;
        var room = MathF.Max(0, next.MaxHeight - next.HeaderHeight - next.FooterHeight);
        // At least one row of the body shows, however small the room.
        var wanted = next.BodyHeight;
        natural = MathF.Min(wanted, MathF.Max(room, MathF.Min(wanted, Context.D(48))));
        natural = MathF.Ceiling(natural);
        viewport = natural;
        scroll = Math.Clamp(scroll, 0, MathF.Max(0, next.BodyHeight - viewport));
        var now = new HashSet<Widget>(ReferenceEqualityComparer.Instance);
        foreach (var widget in All(next))
        {
            now.Add(widget);
            widget.Panel = this;
        }
        foreach (var widget in All(before))
        {
            if (!now.Contains(widget))
            {
                Forget(widget);
            }
        }
        if (hot is not null && !now.Contains(hot))
        {
            hot = null;
        }
        if (pressed is not null && !now.Contains(pressed))
        {
            pressed = null;
            EndCapture();
        }
        if (focused is not null && !now.Contains(focused))
        {
            focused = null;
            Blinking(false);
        }
        var margin = Margin;
        foreach (var widget in now)
        {
            if (repaint || !shown.TryGetValue(widget, out var entry)
                || entry.Size != new Vector2(MathF.Ceiling(widget.Width) + 2 * margin, MathF.Ceiling(widget.Height) + 2 * margin))
            {
                dirty.Add(widget);
            }
        }
        if (bodyLayer is not null)
        {
            Arrange();
            Flush();
        }
    }

    /// <summary>
    /// The open panel sized for what it shows now (<see cref="FlyoutHost.Fit"/>), after a
    /// <see cref="Set"/>: grown and shrunk at once, the pointer on it or not. What the host gives
    /// beyond what was asked (its whole px) goes to the body's window, so the footer stays at the
    /// panel's foot. A press stays on the control it went down on wherever that control is now, and
    /// a control that went away takes its press with it (<see cref="Set"/>).
    /// </summary>
    public void Fit()
    {
        var given = host.Fit(Size);
        var window = MathF.Max(natural, given.Y - content.HeaderHeight - content.FooterHeight);
        if (window == viewport)
        {
            return;
        }
        viewport = window;
        scroll = Math.Clamp(scroll, 0, MathF.Max(0, content.BodyHeight - viewport));
        Arrange();
    }

    /// <summary>
    /// Its layers in the host's content: call after every <see cref="FlyoutHost.Show"/>. Made the first time, and
    /// kept (§363): a showing again puts them back as they were, every widget with its surface, and paints only
    /// what changed meanwhile.
    /// </summary>
    public void Attach()
    {
        var c = host.Compositor;
        if (root is null)
        {
            root = Own(c.CreateContainerVisual());
            headerLayer = Own(c.CreateContainerVisual());
            bodyWindow = Own(c.CreateContainerVisual());
            bodyWindow.Clip = Own(c.CreateInsetClip());
            bodyLayer = Own(c.CreateContainerVisual());
            bodyWindow.Children.InsertAtTop(bodyLayer);
            footerLayer = Own(c.CreateContainerVisual());
            indicator = Own(c.CreateSpriteVisual());
            indicatorShape = Own(c.CreateRoundedRectangleGeometry());
            indicator.Brush = Own(c.CreateColorBrush(host.Theme.ScrollIndicator));
            indicator.Clip = Own(c.CreateGeometricClip(indicatorShape));
            root.Children.InsertAtTop(footerLayer);
            root.Children.InsertAtTop(bodyWindow);
            root.Children.InsertAtTop(headerLayer);
            root.Children.InsertAtTop(indicator);
            // Over everything, including the scroll indicator: a dropdown covers what it hangs over.
            overLayer = Own(c.CreateContainerVisual());
            root.Children.InsertAtTop(overLayer);
            foreach (var widget in All(content))
            {
                dirty.Add(widget);
            }
        }
        if (root.Parent != host.Content)
        {
            try
            {
                root.Parent?.Children.Remove(root);
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ObjectDisposedException)
            {
                // The window it stood in was let go of: its tree with it.
            }
            host.Content.Children.InsertAtTop(root);
        }
        Arrange();
        Flush();
    }

    /// <summary>
    /// The showing ended, the panel kept for the next (§363): nothing under the pointer, pressed or with the keys any
    /// more, at its top again — a popup at rest opens at its start, as Windows' do —, every widget and its surface
    /// kept; what changed is painted as it shows again.
    /// </summary>
    public void Rest()
    {
        Blinking(false);
        if (capturing)
        {
            EndCapture();
        }
        foreach (var widget in All(content))
        {
            if (widget.Hot || widget.Pressed || widget.Focused || widget.FocusVisible)
            {
                widget.Hot = widget.Pressed = widget.Focused = widget.FocusVisible = false;
                dirty.Add(widget);
            }
        }
        hot = pressed = focused = null;
        scroll = 0;
        if (bodyLayer is not null)
        {
            bodyLayer.Offset = Vector3.Zero;
            PlaceIndicator();
        }
    }

    /// <summary>The panel goes for good (its owner does, or shows its pages at another scale or in another look): every surface to the host, which closes it once its window no longer shows it.</summary>
    public void Release()
    {
        Rest();
        foreach (var entry in shown.Values)
        {
            host.Keep(entry.Sprite);
            host.Keep(entry.Brush);
            if (entry.Surface is { } surface)
            {
                host.Keep(surface);
            }
        }
        shown.Clear();
        dirty.Clear();
        foreach (var value in owned)
        {
            host.Keep(value);
        }
        owned.Clear();
        foreach (var widget in All(content))
        {
            widget.Panel = null;
        }
        content = new(0, [], 0, [], 0, [], 0, 0);
        root = headerLayer = bodyWindow = bodyLayer = footerLayer = overLayer = null;
        indicator = null;
        indicatorShape = null;
        natural = 0;
        viewport = 0;
    }

    private T Own<T>(T value)
        where T : IDisposable
    {
        owned.Add(value);
        return value;
    }

    public void Invalidate(Widget widget)
    {
        if (widget.Panel == this)
        {
            dirty.Add(widget);
        }
    }

    /// <summary>Whether anything on it draws a busy ring: its flyout ticks only while this is true.</summary>
    internal bool Spinning => All(content).Any(widget => widget.Visible && widget.Spinning);

    /// <summary>
    /// Turns every busy ring on by <paramref name="step"/> degrees and paints only those widgets
    /// again: no layout, no other surface. False when nothing is busy any more, and the flyout's
    /// timer then stops.
    /// </summary>
    internal bool Spin(float step = Spinner.StepDegrees)
    {
        var turning = All(content).Where(widget => widget.Visible && widget.Spinning).ToList();
        if (turning.Count == 0)
        {
            return false;
        }
        Context.Spin = Spinner.Next(Context.Spin, step);
        foreach (var widget in turning)
        {
            Invalidate(widget);
        }
        Flush();
        return true;
    }

    /// <summary>Paints every widget that changed since the last flush.</summary>
    public void Flush()
    {
        if (bodyLayer is null || dirty.Count == 0)
        {
            return;
        }
        var list = dirty.ToList();
        dirty.Clear();
        foreach (var widget in list)
        {
            try
            {
                Draw(widget);
            }
            catch (Exception error) when (error is NativeException or System.Runtime.InteropServices.COMException or ObjectDisposedException)
            {
                // A device lost mid-draw or a popup mid-teardown: the next change draws it again.
            }
        }
    }

    // --- Focus -----------------------------------------------------------------

    /// <summary>Gives <paramref name="widget"/> the keyboard; <paramref name="visible"/> shows the focus rectangle (the keyboard moved it).</summary>
    public void Focus(Widget? widget, bool visible)
    {
        if (widget is not null && (!widget.Focusable || !widget.Enabled || !widget.Visible))
        {
            return;
        }
        if (focused == widget)
        {
            if (widget is not null && widget.FocusVisible != visible)
            {
                widget.FocusVisible = visible;
                widget.Invalidate();
            }
            return;
        }
        if (focused is { } old)
        {
            old.Focused = false;
            old.FocusVisible = false;
            old.FocusChanged();
            old.Invalidate();
        }
        focused = widget;
        if (widget is not null)
        {
            widget.Focused = true;
            widget.FocusVisible = visible;
            widget.FocusChanged();
            widget.Invalidate();
            Reveal(widget);
        }
        // narrator: a screen reader says which control has the keyboard now — or, where a click on
        // nothing in particular took it from one, that the panel itself has it.
        Uia.FocusMoved(host.Hwnd);
        Blinking(widget is TextField);
    }

    /// <summary>The next (or previous) widget that takes the keyboard, in the order the page placed them; wraps with <paramref name="wrap"/>.</summary>
    public void MoveFocus(bool forward, bool wrap)
    {
        var stops = All(content).Where(widget => widget.Focusable && widget.Enabled && widget.Visible).ToList();
        if (stops.Count == 0)
        {
            return;
        }
        var at = focused is null ? -1 : stops.IndexOf(focused);
        int next;
        if (at < 0)
        {
            next = forward ? 0 : stops.Count - 1;
        }
        else
        {
            next = at + (forward ? 1 : -1);
            if (next < 0 || next >= stops.Count)
            {
                if (!wrap)
                {
                    return;
                }
                next = (next + stops.Count) % stops.Count;
            }
        }
        Focus(stops[next], visible: true);
    }

    // --- Messages --------------------------------------------------------------

    /// <summary>A message of the flyout's window, after the host's. True when the panel used it.</summary>
    public bool Message(uint msg, WPARAM wParam, LPARAM lParam)
    {
        switch (msg)
        {
            case WM.WM_MOUSEMOVE:
            {
                var at = PanelPoint(lParam);
                if (capturing && pressed is { } held)
                {
                    var (widget, local) = (held, Local(held, at));
                    var inside = widget.Contains(local);
                    if (widget.Hot != inside)
                    {
                        widget.Hot = inside;
                        widget.Invalidate();
                    }
                    widget.PointerMoved(local);
                }
                else
                {
                    var under = WidgetAt(at);
                    SetHot(under);
                    under?.PointerMoved(Local(under, at));
                }
                Flush();
                return true;
            }
            case WM.WM_MOUSELEAVE:
                if (!capturing)
                {
                    SetHot(null);
                }
                Flush();
                return true;
            case WM.WM_LBUTTONDOWN:
            {
                var at = PanelPoint(lParam);
                var under = WidgetAt(at);
                CountClicks(at);
                if (under is null || !under.Enabled)
                {
                    // A press on nothing in particular ends the editing, as in Windows 11: the box
                    // that had the keyboard shows no caret and no selection any more (backlog 35).
                    Focus(null, visible: false);
                    Flush();
                    return true;
                }
                SetHot(under);
                pressed = under;
                under.Pressed = true;
                under.Invalidate();
                // What takes no keyboard (a row with only a menu) takes it from whatever had it.
                Focus(under.Focusable ? under : null, visible: false);
                var mods = (((nuint)wParam & 0x0004) != 0 ? Mods.Shift : Mods.None) | (((nuint)wParam & 0x0008) != 0 ? Mods.Control : Mods.None);
                _ = under.PointerPressed(Local(under, at), mods, clicks);
                // Every press is held till the button goes up: a click counts only
                // when it ends on what it started on.
                capturing = true;
                _ = SetCapture(Hwnd);
                Flush();
                return true;
            }
            // A right-click is a widget's own menu, and only the widgets that have one answer it
            // (Widget.ContextClicked). It acts on the release, as Windows' own menus do, and it
            // never presses or focuses the widget: the menu is about the row, not a click on it.
            case WM.WM_RBUTTONUP:
            {
                var at = PanelPoint(lParam);
                if (WidgetAt(at) is { Enabled: true, ContextClicked: { } menu } under)
                {
                    SetHot(under);
                    Flush();
                    menu(at);
                }
                return true;
            }
            case WM.WM_LBUTTONUP:
            {
                var at = PanelPoint(lParam);
                var was = pressed;
                pressed = null;
                EndCapture();
                if (was is not null)
                {
                    var local = Local(was, at);
                    var inside = was.Contains(local) && IsOnScreen(was, at);
                    was.Pressed = false;
                    was.Invalidate();
                    was.PointerReleased(local, inside);
                    if (inside && was.Enabled)
                    {
                        // A click on the row's own menu mark opens that menu rather than choosing
                        // the row: the mark says a menu is there, so the left button must find it.
                        if (was.OnMenuMark(local) && was.ContextClicked is { } mark)
                        {
                            mark(at);
                        }
                        else
                        {
                            was.Clicked?.Invoke();
                        }
                    }
                }
                SetHot(WidgetAt(at));
                Flush();
                return true;
            }
            case WM.WM_CAPTURECHANGED:
                if (capturing && (HWND)(void*)(nint)lParam != Hwnd)
                {
                    capturing = false;
                    if (pressed is { } lost)
                    {
                        pressed = null;
                        lost.Pressed = false;
                        lost.PointerReleased(new Vector2(-1, -1), inside: false);
                        lost.Invalidate();
                        Flush();
                    }
                }
                return true;
            case WM.WM_MOUSEWHEEL:
            {
                int delta = (short)(((nuint)wParam >> 16) & 0xFFFF);
                var screen = new POINT { x = (short)((nint)lParam & 0xFFFF), y = (short)(((nint)lParam >> 16) & 0xFFFF) };
                var used = false;
                if (ScreenToClient(Hwnd, &screen))
                {
                    var at = new Vector2(screen.x - host.Panel.X, screen.y - host.Panel.Y);
                    if (WidgetAt(at) is { Enabled: true } under)
                    {
                        used = under.Wheel(delta);
                    }
                    if (!used)
                    {
                        ScrollTo(scroll - delta / 120f * Context.D(48));
                    }
                    SetHot(WidgetAt(at));
                }
                Flush();
                return true;
            }
            case WM.WM_KEYDOWN:
            case WM.WM_SYSKEYDOWN:
            {
                var key = (int)(nuint)wParam;
                var mods = (KeyState.Down(VK.VK_SHIFT) ? Mods.Shift : Mods.None) | (KeyState.Down(VK.VK_CONTROL) ? Mods.Control : Mods.None);
                var used = focused is { Enabled: true } widget && widget.Key(key, mods);
                if (!used)
                {
                    used = Navigate(key, mods);
                }
                Flush();
                return used;
            }
            case WM.WM_CHAR:
            {
                var used = focused is { Enabled: true } widget && widget.Character((char)(nuint)wParam);
                Flush();
                return used;
            }
            case WM.WM_TIMER when (nuint)wParam == CaretTimer:
                if (focused is { } blinker && blinker.Blink())
                {
                    blinker.Invalidate();
                    Flush();
                }
                return true;
            case WM.WM_SETFOCUS:
            case WM.WM_KILLFOCUS:
                Blinking(msg == WM.WM_SETFOCUS && focused is TextField);
                return false;
            default:
                return false;
        }
    }

    /// <summary>What the panel does with a key no widget took: Tab and the arrows move the focus, Enter and Space click.</summary>
    private bool Navigate(int key, Mods mods)
    {
        switch (key)
        {
            case VK.VK_TAB:
                MoveFocus(forward: (mods & Mods.Shift) == 0, wrap: true);
                return true;
            // The Menu key and Shift+F10, which is how Windows asks anything for its own menu. It
            // opens at the middle of the row, where the mark that says the menu is there sits.
            case VK.VK_APPS:
            case VK.VK_F10 when (mods & Mods.Shift) != 0:
                if (focused is { Enabled: true, ContextClicked: { } menu } asked)
                {
                    var box = PanelRect(asked);
                    menu(new Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2));
                    return true;
                }
                return false;
            case VK.VK_DOWN:
                MoveFocus(forward: true, wrap: false);
                return true;
            case VK.VK_UP:
                MoveFocus(forward: false, wrap: false);
                return true;
            case VK.VK_RETURN:
            case VK.VK_SPACE:
                if (focused is { Enabled: true, Clicked: { } click } widget)
                {
                    if (!widget.FocusVisible)
                    {
                        widget.FocusVisible = true;
                        widget.Invalidate();
                    }
                    click();
                    return true;
                }
                return false;
            case VK.VK_PRIOR:
                ScrollTo(scroll - MathF.Max(Context.D(48), viewport - Context.D(48)));
                return true;
            case VK.VK_NEXT:
                ScrollTo(scroll + MathF.Max(Context.D(48), viewport - Context.D(48)));
                return true;
            case VK.VK_HOME when focused is null or { FocusVisible: true }:
                MoveFocusToEnd(first: true);
                return true;
            case VK.VK_END when focused is null or { FocusVisible: true }:
                MoveFocusToEnd(first: false);
                return true;
            default:
                return false;
        }
    }

    private void MoveFocusToEnd(bool first)
    {
        var stops = All(content).Where(widget => widget.Focusable && widget.Enabled && widget.Visible).ToList();
        if (stops.Count > 0)
        {
            Focus(first ? stops[0] : stops[^1], visible: true);
        }
    }

    // --- Offscreen -------------------------------------------------------------

    /// <summary>Everything, as the surfaces show it, into <paramref name="dc"/> with the panel's top-left at (0, 0).</summary>
    public void PaintAll(ID2D1DeviceContext* dc)
    {
        foreach (var widget in content.Header)
        {
            PaintAt(dc, widget, widget.Bounds.left, widget.Bounds.top);
        }
        var top = content.HeaderHeight;
        var clip = new D2D_RECT_F(0, top, content.Width, top + viewport);
        dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        foreach (var widget in content.Body)
        {
            PaintAt(dc, widget, widget.Bounds.left, top + widget.Bounds.top - scroll);
        }
        dc->PopAxisAlignedClip();
        var bottom = top + viewport;
        foreach (var widget in content.Footer)
        {
            PaintAt(dc, widget, widget.Bounds.left, bottom + widget.Bounds.top);
        }
        if (content.BodyHeight > viewport + 0.5f)
        {
            // ControlStrongFillColorDefault, as the live indicator.
            var (x, y, thickness, length) = IndicatorPlace();
            PopupPaint.Fill(dc, PopupPaint.Rect(x, y, thickness, length), thickness / 2, Context.Theme.StrongFill);
        }
        // Last, over all of it, and never scrolled: what a dropdown hangs over the page.
        foreach (var widget in content.Above)
        {
            PaintAt(dc, widget, widget.Bounds.left, widget.Bounds.top);
        }
    }

    private void PaintAt(ID2D1DeviceContext* dc, Widget widget, float x, float y)
    {
        if (!widget.Visible)
        {
            return;
        }
        Shift(dc, x, y, () => widget.Paint(dc, Context));
    }

    /// <summary>Where <paramref name="widget"/> is on the panel now (the body's scroll taken off), in panel px: for tests.</summary>
    internal D2D_RECT_F PanelRect(Widget widget)
    {
        var bounds = widget.Bounds;
        var shift = LayerOf(widget) switch
        {
            // What hangs over the page is already in the panel's own px.
            Layer.Header or Layer.Over => 0,
            Layer.Footer => content.HeaderHeight + viewport,
            _ => content.HeaderHeight - scroll,
        };
        return new D2D_RECT_F(bounds.left, bounds.top + shift, bounds.right, bounds.bottom + shift);
    }

    /// <summary>The first widget named <paramref name="name"/>, for tests.</summary>
    internal Widget? Find(string name) => All(content).FirstOrDefault(widget => widget.Name == name);

    /// <summary>Everything the panel is showing, for the tests: not everything carries a name.</summary>
    internal IReadOnlyList<Widget> Widgets => [.. All(content)];

    // --- Inside ----------------------------------------------------------------

    private static IEnumerable<Widget> All(PanelContent of) => of.Header.Concat(of.Body).Concat(of.Footer).Concat(of.Above);

    private sealed class Shown
    {
        public required SpriteVisual Sprite;
        public required CompositionSurfaceBrush Brush;
        public CompositionDrawingSurface? Surface;
        public Vector2 Size;
    }

    private enum Layer
    {
        Header,
        Body,
        Footer,
        Over,
    }

    private Layer LayerOf(Widget widget) =>
        content.Above.Contains(widget) ? Layer.Over
            : content.Header.Contains(widget) ? Layer.Header
            : content.Footer.Contains(widget) ? Layer.Footer
            : Layer.Body;

    private float Margin => MathF.Ceiling(MarginDip * Context.Scale);

    /// <summary>Paints <paramref name="widget"/> into its surface, a new one when its size changed.</summary>
    private void Draw(Widget widget)
    {
        if (widget.Panel != this || bodyLayer is null)
        {
            return;
        }
        var c = host.Compositor;
        if (!shown.TryGetValue(widget, out var entry))
        {
            var sprite = c.CreateSpriteVisual();
            var brush = c.CreateSurfaceBrush();
            sprite.Brush = brush;
            entry = new Shown { Sprite = sprite, Brush = brush };
            shown[widget] = entry;
            (LayerOf(widget) switch
            {
                Layer.Header => headerLayer!,
                Layer.Footer => footerLayer!,
                Layer.Over => overLayer!,
                _ => bodyLayer,
            }).Children.InsertAtTop(sprite);
        }
        var margin = Margin;
        var size = new Vector2(MathF.Ceiling(widget.Width) + 2 * margin, MathF.Ceiling(widget.Height) + 2 * margin);
        entry.Sprite.Offset = new Vector3(widget.Bounds.left - margin, widget.Bounds.top - margin, 0);
        entry.Sprite.Size = size;
        entry.Sprite.IsVisible = widget.Visible;
        if (!widget.Visible || widget.Width <= 0 || widget.Height <= 0)
        {
            return;
        }
        var context = Context;
        Paint paint = dc => Shift(dc, margin, margin, () => widget.Paint(dc, context));
        if (entry.Surface is { } surface && entry.Size == size)
        {
            context.Gfx.Redraw(surface, paint);
            return;
        }
        var fresh = context.Gfx.Draw(size.X, size.Y, paint);
        entry.Brush.Surface = fresh;
        var old = entry.Surface;
        entry.Surface = fresh;
        entry.Size = size;
        old?.Dispose();
    }

    private void Forget(Widget widget)
    {
        widget.Panel = null;
        widget.Hot = widget.Pressed = widget.Focused = widget.FocusVisible = false;
        dirty.Remove(widget);
        if (shown.Remove(widget, out var entry))
        {
            try
            {
                entry.Sprite.Parent?.Children.Remove(entry.Sprite);
                entry.Brush.Dispose();
                entry.Surface?.Dispose();
                entry.Sprite.Dispose();
            }
            catch (Exception error) when (error is System.Runtime.InteropServices.COMException or ObjectDisposedException)
            {
            }
        }
    }

    /// <summary>Puts the layers, the widgets and the scroll indicator where the content says.</summary>
    private void Arrange()
    {
        if (headerLayer is null || bodyWindow is null || bodyLayer is null || footerLayer is null)
        {
            return;
        }
        var width = content.Width;
        headerLayer.Size = new Vector2(width, content.HeaderHeight);
        bodyWindow.Offset = new Vector3(0, content.HeaderHeight, 0);
        bodyWindow.Size = new Vector2(width, viewport);
        bodyLayer.Offset = new Vector3(0, -scroll, 0);
        bodyLayer.Size = new Vector2(width, MathF.Max(content.BodyHeight, 1));
        footerLayer.Offset = new Vector3(0, content.HeaderHeight + viewport, 0);
        footerLayer.Size = new Vector2(width, content.FooterHeight);
        var margin = Margin;
        foreach (var (widget, entry) in shown)
        {
            entry.Sprite.Offset = new Vector3(widget.Bounds.left - margin, widget.Bounds.top - margin, 0);
        }
        PlaceIndicator();
    }

    private (float X, float Y, float Thickness, float Length) IndicatorPlace()
    {
        var thickness = MathF.Max(2, MathF.Round(Context.Scale * 2));
        var length = MathF.Max(thickness * 4, MathF.Round(viewport * viewport / MathF.Max(1, content.BodyHeight)));
        var travel = viewport - length;
        var most = MathF.Max(1, content.BodyHeight - viewport);
        var y = content.HeaderHeight + MathF.Round(travel * scroll / most);
        var x = content.Width - thickness - MathF.Round(Context.Scale * 3);
        return (x, y, thickness, length);
    }

    private void PlaceIndicator()
    {
        if (indicator is null || indicatorShape is null)
        {
            return;
        }
        var shows = content.BodyHeight > viewport + 0.5f;
        indicator.IsVisible = shows;
        if (!shows)
        {
            return;
        }
        var (x, y, thickness, length) = IndicatorPlace();
        indicator.Size = new Vector2(thickness, length);
        indicator.Offset = new Vector3(x, y, 0);
        indicatorShape.Size = new Vector2(thickness, length);
        indicatorShape.CornerRadius = new Vector2(thickness / 2, thickness / 2);
    }

    /// <summary>Scrolls the body to <paramref name="value"/> px, kept within its content.</summary>
    public void ScrollTo(float value)
    {
        var most = MathF.Max(0, content.BodyHeight - viewport);
        var next = MathF.Round(Math.Clamp(value, 0, most));
        if (next == scroll)
        {
            return;
        }
        scroll = next;
        if (bodyLayer is not null)
        {
            bodyLayer.Offset = new Vector3(0, -scroll, 0);
        }
        PlaceIndicator();
    }

    /// <summary>Scrolls a body widget with the keyboard into view.</summary>
    private void Reveal(Widget widget)
    {
        if (LayerOf(widget) != Layer.Body)
        {
            return;
        }
        var nudge = Context.D(4);
        if (widget.Bounds.top < scroll)
        {
            ScrollTo(widget.Bounds.top - nudge);
        }
        else if (widget.Bounds.bottom > scroll + viewport)
        {
            ScrollTo(widget.Bounds.bottom - viewport + nudge);
        }
    }

    private Vector2 PanelPoint(LPARAM lParam)
    {
        _ = host.ToPanel(lParam, out var at);
        return at;
    }

    /// <summary>Where <paramref name="at"/> (panel px) is in <paramref name="widget"/>'s own px.</summary>
    private Vector2 Local(Widget widget, Vector2 at)
    {
        var (x, y) = LayerOf(widget) switch
        {
            Layer.Header or Layer.Over => (at.X, at.Y),
            Layer.Footer => (at.X, at.Y - content.HeaderHeight - viewport),
            _ => (at.X, at.Y - content.HeaderHeight + scroll),
        };
        return new Vector2(x - widget.Bounds.left, y - widget.Bounds.top);
    }

    /// <summary>A body widget counts only where the body shows it.</summary>
    private bool IsOnScreen(Widget widget, Vector2 at) =>
        LayerOf(widget) != Layer.Body || (at.Y >= content.HeaderHeight && at.Y < content.HeaderHeight + viewport);

    /// <summary>The topmost widget the pointer finds at <paramref name="at"/> (panel px).</summary>
    public Widget? WidgetAt(Vector2 at)
    {
        IReadOnlyList<Widget> layer;
        Vector2 point;
        if (at.X < 0 || at.X >= content.Width || at.Y < 0)
        {
            return null;
        }
        // What hangs over the page answers first, wherever it hangs.
        if (Topmost(content.Above, at) is { } over)
        {
            return over;
        }
        if (at.Y < content.HeaderHeight)
        {
            (layer, point) = (content.Header, at);
        }
        else if (at.Y < content.HeaderHeight + viewport)
        {
            (layer, point) = (content.Body, new Vector2(at.X, at.Y - content.HeaderHeight + scroll));
        }
        else if (at.Y < content.HeaderHeight + viewport + content.FooterHeight)
        {
            (layer, point) = (content.Footer, new Vector2(at.X, at.Y - content.HeaderHeight - viewport));
        }
        else
        {
            return null;
        }
        return Topmost(layer, point);
    }

    /// <summary>The last widget of <paramref name="layer"/> that <paramref name="point"/> is in, in its own px.</summary>
    private static Widget? Topmost(IReadOnlyList<Widget> layer, Vector2 point)
    {
        for (var i = layer.Count - 1; i >= 0; i--)
        {
            var widget = layer[i];
            if (widget.Visible && widget.HitTestVisible
                && widget.Contains(new Vector2(point.X - widget.Bounds.left, point.Y - widget.Bounds.top)))
            {
                return widget;
            }
        }
        return null;
    }

    private void SetHot(Widget? widget)
    {
        if (hot == widget)
        {
            return;
        }
        if (hot is { } old)
        {
            old.Hot = false;
            old.Invalidate();
        }
        hot = widget;
        if (widget is not null)
        {
            widget.Hot = true;
            widget.Invalidate();
        }
    }

    /// <summary>A second press close to the first within the double-click time counts as a double click, a third as a triple.</summary>
    private void CountClicks(Vector2 at)
    {
        var now = (uint)Environment.TickCount;
        var near = MathF.Abs(at.X - lastPressAt.X) <= GetSystemMetrics(SM.SM_CXDOUBLECLK) && MathF.Abs(at.Y - lastPressAt.Y) <= GetSystemMetrics(SM.SM_CYDOUBLECLK);
        clicks = near && now - lastPress <= GetDoubleClickTime() ? Math.Min(clicks + 1, 3) : 1;
        lastPress = now;
        lastPressAt = at;
    }

    private void EndCapture()
    {
        if (capturing)
        {
            capturing = false;
            if (GetCapture() == Hwnd)
            {
                _ = ReleaseCapture();
            }
        }
    }

    /// <summary>The caret blinks while a text field has the keyboard and the window has the focus.</summary>
    private void Blinking(bool on)
    {
        var window = Hwnd;
        if (window == HWND.NULL)
        {
            blinking = false;
            return;
        }
        if (on == blinking)
        {
            return;
        }
        blinking = on;
        // INFINITE: the person turned blinking off; the caret then just stays.
        var time = GetCaretBlinkTime();
        if (on && time is > 0 and < INFINITE)
        {
            _ = SetTimer(window, CaretTimer, time, null);
        }
        else
        {
            _ = KillTimer(window, CaretTimer);
        }
    }

    /// <summary>Paints with the origin moved by (<paramref name="x"/>, <paramref name="y"/>) on top of the transform already set.</summary>
    internal static void Shift(ID2D1DeviceContext* dc, float x, float y, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = x * before._11 + y * before._21 + before._31;
        moved._32 = x * before._12 + y * before._22 + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }
}
