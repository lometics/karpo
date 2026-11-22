// The controls of Karpo's quick-settings flyouts: small retained objects that
// know their rectangle, their state (under the pointer, pressed, focused) and
// how to paint themselves with Direct2D. QuickPanel gives each one a composition
// surface of its own and paints it again only when it says it changed, so a
// slider being dragged repaints the slider and nothing else; the same painting
// goes into a bitmap for an offscreen picture.
//
// Sizes follow WinUI's Windows 11 controls at 100 %: text 14 px (12 for
// captions), controls 32 px high with radius 4, the focus rectangle 2 + 1 px
// three px outside the control.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

/// <summary>Everything a control paints and measures with: the drawing device, the colours, the scale.</summary>
internal sealed class QuickContext
{
    public required Gfx Gfx { get; init; }

    public required QuickTheme Theme { get; init; }

    /// <summary>DIPs to px.</summary>
    public required float Scale { get; init; }

    /// <summary>Windows' text size setting: text and what holds text grow with it.</summary>
    public required float TextScale { get; init; }

    /// <summary>
    /// Where the busy ring stands, in degrees. The flyout advances it on its own timer while
    /// something is busy (never a composition animation, which DWM can stop), and everything that
    /// draws a ring reads it, so every ring on a panel turns together.
    /// </summary>
    public float Spin { get; set; }

    /// <summary>Whether <paramref name="other"/> draws as this does: a panel kept through its showings is kept for the next (§363).</summary>
    public bool Same(QuickContext other) =>
        ReferenceEquals(Gfx, other.Gfx) && Theme == other.Theme && Scale == other.Scale && TextScale == other.TextScale;

    /// <summary>A length in px on whole pixels.</summary>
    public float D(float dip) => Round(dip * Scale);

    /// <summary>A length that holds text, in px: grows with the text size.</summary>
    public float T(float dip) => Round(dip * Scale * TextScale);

    public float TextPx => 14 * Scale * TextScale;

    public float SmallPx => 12 * Scale * TextScale;

    public float TitlePx => 14 * Scale * TextScale;

    /// <summary>Glyphs stay at their size as text grows, as WinUI's FontIcons do.</summary>
    public float GlyphPx => 16 * Scale;

    /// <summary>A hairline: one px, or two at 200 %.</summary>
    public float Hairline => MathF.Max(1, Round(Scale));

    public static float Round(float value) => MathF.Round(value, MidpointRounding.AwayFromZero);
}

/// <summary>The modifier keys held with a key.</summary>
[Flags]
internal enum Mods
{
    None = 0,
    Shift = 1,
    Control = 2,
}

internal abstract unsafe class Widget
{
    /// <summary>
    /// Between a panel's edge and its content: the gutter the whole panel keeps, so everything
    /// stacked down it lines up down one column.
    /// <para>
    /// One number, here, rather than one per feature. The weather's header, a share's header, the
    /// drives' and the day graph each declared their own <c>PadDip = 16</c> and each said in its
    /// own words that it was the panel's gutter — four homes for one quantity, agreeing until one
    /// of them did not. A panel that genuinely keeps a different gutter says so by passing one
    /// (screen time's components take theirs as an argument and pass nothing for a list, which is
    /// how a list folder's items reach the border); that is a caller, not a copy.
    /// </para>
    /// </summary>
    public const float PanelPadDip = 16;

    private bool enabled = true;
    private bool visible = true;

    /// <summary>Where it is, in its layer's px (the panel's header or footer, or the body's content), set by its page.</summary>
    public D2D_RECT_F Bounds { get; set; }

    public float Width => Bounds.right - Bounds.left;

    public float Height => Bounds.bottom - Bounds.top;

    /// <summary>What it is called, for a screen reader's sake and a test's.</summary>
    public string Name { get; init; } = "";

    /// <summary>A Tab stop.</summary>
    public virtual bool Focusable => false;

    /// <summary>Whether the pointer finds it; a background or a label lets it through to what is under it.</summary>
    public virtual bool HitTestVisible => Focusable || Clicked is not null || ContextClicked is not null;

    /// <summary>
    /// It draws the busy ring, so it needs painting again while the ring turns. Its flyout ticks a
    /// timer only while something says true here, and stops the moment nothing does.
    /// </summary>
    public virtual bool Spinning => false;

    public bool Enabled
    {
        get => enabled;
        set
        {
            if (enabled != value)
            {
                enabled = value;
                Invalidate();
            }
        }
    }

    /// <summary>Hidden, it is neither painted nor found; its page keeps its state.</summary>
    public bool Visible
    {
        get => visible;
        set
        {
            if (visible != value)
            {
                visible = value;
                Invalidate();
            }
        }
    }

    /// <summary>The pointer is over it.</summary>
    public bool Hot { get; internal set; }

    /// <summary>A button went down on it and is still down.</summary>
    public bool Pressed { get; internal set; }

    /// <summary>It has the keyboard.</summary>
    public bool Focused { get; internal set; }

    /// <summary>It got the keyboard from the keyboard: it shows the focus rectangle.</summary>
    public bool FocusVisible { get; internal set; }

    /// <summary>A click (or Enter or Space while it has the keyboard). The page acts on it.</summary>
    public Action? Clicked { get; set; }

    /// <summary>
    /// Whether a left click at <paramref name="local"/> (this widget's own px) is on something of
    /// its own that is not the widget's click — a list row's menu mark. The panel asks before it
    /// raises <see cref="Clicked"/>; the default has nothing of its own and answers false.
    /// </summary>
    protected internal virtual bool OnMenuMark(Vector2 local) => false;

    /// <summary>
    /// A right-click on it, given where in the panel it landed: the widget's own menu. A widget
    /// that sets none answers no right-click at all, which is every one of them but a switcher's
    /// rows — a row names a virtual desktop, and a desktop's actions belong to the row that names
    /// it, as everything else in Karpo has its actions on its own right-click.
    /// </summary>
    public Action<Vector2>? ContextClicked { get; set; }

    internal QuickPanel? Panel { get; set; }

    /// <summary>
    /// The context a control measures its pointer against when no panel holds it: a folder's card
    /// that hosts the quick settings' own controls, so a component and the page it came from draw
    /// with one drawing (docs\widgets.md section 9, the sound conversion). A control in a panel
    /// never reads it, because the panel's own context wins; see <see cref="ContextNow"/>.
    /// <para>
    /// This is the whole of what a card host takes from the toolkit. It is a scale, not a panel:
    /// no focus, no keyboard and no hit-testing come with it — the folder routes the pointer, and
    /// the host hands each control the part of it that landed on the control.
    /// </para>
    /// </summary>
    internal QuickContext? Host { get; set; }

    /// <summary>What a control measures a pointer event against: its panel's context, else its host's.</summary>
    protected QuickContext? ContextNow => Panel?.Context ?? Host;

    /// <summary>It looks different now: its surface is painted again before the message returns.</summary>
    public void Invalidate() => Panel?.Invalidate(this);

    /// <summary>Paints it with (0, 0) at its top-left corner, <see cref="Width"/> × <see cref="Height"/> px.</summary>
    public abstract void Paint(ID2D1DeviceContext* dc, QuickContext context);

    /// <summary>The pointer moved over it (or, while it holds the pointer, anywhere), in its own px.</summary>
    public virtual void PointerMoved(Vector2 at)
    {
    }

    /// <summary>
    /// The left button went down on it; <paramref name="clicks"/> is 2 for the second press of a double click.
    /// True when it wants the pointer until the button goes up (a slider's thumb, a text selection).
    /// </summary>
    public virtual bool PointerPressed(Vector2 at, Mods mods, int clicks) => false;

    /// <summary>The button went up; <paramref name="inside"/> when over it.</summary>
    public virtual void PointerReleased(Vector2 at, bool inside)
    {
    }

    /// <summary>A wheel turn over it; true when it used it (a slider), else the body scrolls.</summary>
    public virtual bool Wheel(int delta) => false;

    /// <summary>A key while it has the keyboard; true when it used it.</summary>
    public virtual bool Key(int key, Mods mods) => false;

    /// <summary>A typed character while it has the keyboard; true when it used it.</summary>
    public virtual bool Character(char c) => false;

    /// <summary>It got or lost the keyboard.</summary>
    public virtual void FocusChanged()
    {
    }

    /// <summary>The panel's caret timer; true when it changed how it looks.</summary>
    public virtual bool Blink() => false;

    /// <summary>Whether (x, y) of its own px is on it.</summary>
    public virtual bool Contains(Vector2 at) => at.X >= 0 && at.Y >= 0 && at.X < Width && at.Y < Height;

    /// <summary>
    /// It paints where its <see cref="Bounds"/> are, not from (0, 0): a line that draws a button where it
    /// stands, with no transform, so what is drawn is written down where it is (LineButton, PaintWatch).
    /// </summary>
    internal bool AtBounds { get; set; }

    /// <summary>Its rectangle in its own px, or its bounds where it paints at them (<see cref="AtBounds"/>).</summary>
    protected D2D_RECT_F Local => AtBounds ? Bounds : new(0, 0, Width, Height);

    /// <summary>
    /// The Windows focus rectangle around <paramref name="rect"/>: 2 px of FocusStrokeColorOuter and
    /// 1 px of FocusStrokeColorInner inside it, <paramref name="margin"/> px outside the control.
    /// </summary>
    protected static void FocusRing(ID2D1DeviceContext* dc, QuickContext context, D2D_RECT_F rect, float radius, float margin)
    {
        var outer = context.D(2);
        var inner = MathF.Max(1, context.D(1));
        var ring = new D2D_RECT_F(rect.left - margin, rect.top - margin, rect.right + margin, rect.bottom + margin);
        PopupPaint.Stroke(dc, ring, radius + margin, outer, context.Theme.FocusOuter);
        var inside = new D2D_RECT_F(ring.left + outer, ring.top + outer, ring.right - outer, ring.bottom - outer);
        PopupPaint.Stroke(dc, inside, MathF.Max(0, radius + margin - outer), inner, context.Theme.FocusInner);
    }
}
