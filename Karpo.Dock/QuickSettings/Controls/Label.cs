// Text in a quick-settings flyout: a title, a section heading, a line of body
// text, a caption under a control, a message that went wrong. One line is cut
// with an ellipsis; more lines wrap, the last one cut when there is more.

using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal enum LabelStyle
{
    /// <summary>14 px regular: body text.</summary>
    Body,

    /// <summary>14 px semibold: a page's title, a section's heading.</summary>
    Strong,

    /// <summary>14 px regular, secondary: a quieter line.</summary>
    Secondary,

    /// <summary>12 px regular, secondary: what a control means, where something stands.</summary>
    Caption,

    /// <summary>12 px regular, in the critical colour: what went wrong.</summary>
    Critical,

    /// <summary>28 px semibold: the battery's percentage.</summary>
    Display,
}

internal sealed unsafe class Label : Widget
{
    private string text;
    private LabelStyle style;

    public Label(string text, LabelStyle style = LabelStyle.Body, int lines = 1, bool centred = false)
    {
        this.text = text;
        this.style = style;
        Lines = Math.Max(1, lines);
        Centred = centred;
    }

    public string Text
    {
        get => text;
        set
        {
            if (text != value)
            {
                text = value;
                Invalidate();
            }
        }
    }

    public LabelStyle Style
    {
        get => style;
        set
        {
            if (style != value)
            {
                style = value;
                Invalidate();
            }
        }
    }

    /// <summary>The most lines it wraps into.</summary>
    public int Lines { get; }

    public bool Centred { get; }

    /// <summary>One line set against its right end: a figure whose width changes keeps its edge.</summary>
    public bool Trailing { get; init; }

    public override bool HitTestVisible => false;

    /// <summary>The height <paramref name="text"/> takes at <paramref name="width"/> px in <paramref name="lines"/> lines at most.</summary>
    public static float Measure(QuickContext context, string text, LabelStyle style, float width, int lines = 1)
    {
        if (text.Length == 0)
        {
            return 0;
        }
        var (px, weight, _) = Look(context, style);
        return lines <= 1
            ? PopupPaint.Measure(context.Gfx, text, px, weight, width).Y
            : PopupPaint.Fit(context.Gfx, text, px, weight, width, lines).Height;
    }

    /// <summary>How wide one line of <paramref name="text"/> is.</summary>
    public static float WidthOf(QuickContext context, string text, LabelStyle style) =>
        PopupPaint.Measure(context.Gfx, text, Look(context, style).Px, Look(context, style).Weight).X;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        if (text.Length == 0)
        {
            return;
        }
        var (px, weight, color) = Look(context, style);
        if (!Enabled)
        {
            color = context.Theme.TextDisabled;
        }
        if (Lines <= 1)
        {
            var size = PopupPaint.Measure(context.Gfx, text, px, weight, Width);
            var x = Centred ? PopupPaint.Round((Width - size.X) / 2) : Trailing ? PopupPaint.Round(Width - size.X) : 0;
            PopupPaint.Text(dc, context.Gfx, text, px, weight, color, x, PopupPaint.Round((Height - size.Y) / 2), Width);
            return;
        }
        Wrapped(dc, context, text, px, weight, color, Width, Lines, Centred);
    }

    /// <summary>Text wrapped in <paramref name="width"/> px, <paramref name="lines"/> lines at most, from the top.</summary>
    internal static void Wrapped(ID2D1DeviceContext* dc, QuickContext context, string text, float px, Weight weight, DXGI_RGBA color, float width, int lines, bool centred)
    {
        var (shown, _) = PopupPaint.Fit(context.Gfx, text, px, weight, width, lines);
        if (shown.Length == 0)
        {
            return;
        }
        var layout = context.Gfx.TextLayout(shown, px, weight, null);
        try
        {
            ComUtil.Check(layout->SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_WRAP));
            ComUtil.Check(layout->SetMaxWidth(width));
            if (centred)
            {
                ComUtil.Check(layout->SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER));
            }
            var brush = Gfx.Brush(dc, color);
            try
            {
                dc->DrawTextLayout(new D2D_POINT_2F(0, 0), layout, (ID2D1Brush*)brush, D2D1_DRAW_TEXT_OPTIONS.D2D1_DRAW_TEXT_OPTIONS_ENABLE_COLOR_FONT);
            }
            finally
            {
                brush->Release();
            }
        }
        finally
        {
            layout->Release();
        }
    }

    internal static (float Px, Weight Weight, DXGI_RGBA Color) Look(QuickContext context, LabelStyle style) => style switch
    {
        LabelStyle.Strong => (context.TextPx, Weight.Semibold, context.Theme.Text),
        LabelStyle.Secondary => (context.TextPx, Weight.Regular, context.Theme.TextSecondary),
        LabelStyle.Caption => (context.SmallPx, Weight.Regular, context.Theme.TextSecondary),
        LabelStyle.Critical => (context.SmallPx, Weight.Regular, context.Theme.Critical),
        LabelStyle.Display => (28 * context.Scale * context.TextScale, Weight.Semibold, context.Theme.Text),
        _ => (context.TextPx, Weight.Regular, context.Theme.Text),
    };
}

/// <summary>A Segoe Fluent Icons glyph on its own: a notice's big picture, the battery's level.</summary>
internal sealed unsafe class GlyphIcon : Widget
{
    private readonly float dip;
    private readonly bool tertiary;
    private string glyph;

    public GlyphIcon(string glyph, float dip, bool tertiary = false)
    {
        this.glyph = glyph;
        this.dip = dip;
        this.tertiary = tertiary;
    }

    public string Glyph
    {
        get => glyph;
        set
        {
            if (glyph != value)
            {
                glyph = value;
                Invalidate();
            }
        }
    }

    /// <summary>Its size in px where it is not its DIPs: a list line's glyph, which grows with the text (ListForm.Glyph).</summary>
    public float? Px { get; set; }

    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var color = !Enabled ? context.Theme.TextDisabled : tertiary ? context.Theme.TextTertiary : context.Theme.Text;
        PopupPaint.Glyph(dc, context.Gfx, glyph, Px ?? (dip * context.Scale), color, Local);
    }
}

/// <summary>
/// What sits behind other widgets: the footer's band (its fill and the hairline over it), a divider, a
/// dropdown's list. An opened row's card went (§605): the row is the item, its band all it offers.
/// </summary>
internal sealed unsafe class Backdrop : Widget
{
    private readonly BackdropKind kind;

    public Backdrop(BackdropKind kind)
    {
        this.kind = kind;
    }

    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        switch (kind)
        {
            case BackdropKind.Dropdown:
            {
                // A list hanging over the page: the flyout's own solid base with its stroke, so
                // nothing under it shows through, as WinUI's own drop-down does.
                var radius = context.D(8);
                PopupPaint.Fill(dc, Local, radius, PopupPaint.Ink(theme.Popup.Base));
                PopupPaint.Stroke(dc, Local, radius, MathF.Max(context.Hairline, context.D(theme.Popup.StrokeDip)), PopupPaint.Ink(theme.Popup.Stroke));
                break;
            }
            case BackdropKind.Footer:
                PopupPaint.Divider(dc, 0, Width, 0, context.Hairline, theme.FooterLine);
                PopupPaint.Fill(dc, Local with { top = Local.top + context.Hairline }, 0, theme.Footer);
                break;
            default:
                PopupPaint.Divider(dc, 0, Width, PopupPaint.Round((Height - context.Hairline) / 2), context.Hairline, theme.Divider);
                break;
        }
    }
}

internal enum BackdropKind
{
    Footer,
    Divider,

    /// <summary>quick: what a dropdown's list stands on, over the page: solid, so nothing shows through.</summary>
    Dropdown,
}

/// <summary>
/// A line showing how far along something is, with nothing to drag: the media flyout's timeline
/// where the app allows no seeking. Still, never moving of itself — its page paints it again.
/// </summary>
internal sealed unsafe class Meter : Widget
{
    private float share;

    /// <summary>How much of it is filled, 0 to 1.</summary>
    public float Share
    {
        get => share;
        set
        {
            var next = float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;
            if (Math.Abs(next - share) > 0.0005f)
            {
                share = next;
                Invalidate();
            }
        }
    }

    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var thickness = MathF.Max(2, context.D(4));
        var top = PopupPaint.Round((Height - thickness) / 2);
        var track = new D2D_RECT_F(0, top, Width, top + thickness);
        PopupPaint.Fill(dc, track, thickness / 2, context.Theme.ControlAltFill);
        var filled = MathF.Round(Width * share);
        if (filled > 0)
        {
            PopupPaint.Fill(dc, new D2D_RECT_F(0, top, filled, top + thickness), thickness / 2, context.Theme.Accent);
        }
    }
}

/// <summary>An album's art, or the note that stands in for it where a session has none.</summary>
internal sealed unsafe class ArtBox : Widget
{
    private Pixels? art;

    /// <summary>The Segoe Fluent note drawn when there is no art.</summary>
    public string Glyph { get; init; } = "\uEC4F";

    public Pixels? Art
    {
        get => art;
        set
        {
            if (!ReferenceEquals(art, value))
            {
                art = value;
                Invalidate();
            }
        }
    }

    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var radius = context.D(6);
        if (art is { } picture)
        {
            PopupPaint.Picture(dc, picture, Local, radius);
            return;
        }
        PopupPaint.Fill(dc, Local, radius, context.Theme.ControlAltFill);
        var size = MathF.Round(MathF.Min(Width, Height) * 0.42f);
        PopupPaint.Glyph(dc, context.Gfx, Glyph, size, context.Theme.TextSecondary, Local);
    }
}

/// <summary>
/// A small picture as it is — an app's icon, a site's favicon — fitted into the box, square corners,
/// nothing behind it; nothing at all where there is none.
/// </summary>
internal sealed unsafe class IconBox : Widget
{
    private Pixels? icon;

    public Pixels? Icon
    {
        get => icon;
        set
        {
            if (!ReferenceEquals(icon, value))
            {
                icon = value;
                Invalidate();
            }
        }
    }

    public override bool HitTestVisible => false;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        if (icon is { } picture)
        {
            PopupPaint.Picture(dc, picture, Local, 0);
        }
    }
}

/// <summary>
/// How far a busy ring has turned, and how fast. Pure, so the tests can step it: the flyout's timer
/// advances the phase and repaints the rings, rather than handing an animation to DWM, which can
/// stop running animations for the whole system.
/// </summary>
internal static class Spinner
{
    /// <summary>How often the flyout ticks while something is busy: 30 a second.</summary>
    public const int TickMs = 33;

    /// <summary>How far the ring turns in a tick: a whole turn in 1.5 seconds, as WinUI's ring takes.</summary>
    public const float StepDegrees = 8;

    /// <summary>The phase <paramref name="step"/> degrees on from <paramref name="angle"/>, kept in [0, 360).</summary>
    public static float Next(float angle, float step = StepDegrees)
    {
        if (!float.IsFinite(angle) || !float.IsFinite(step))
        {
            return 0;
        }
        var next = (angle + step) % 360f;
        return next < 0 ? next + 360f : next;
    }
}

/// <summary>
/// Something is going on: a ring, a quarter open, turning at the phase its flyout keeps, and what
/// is happening beside it.
/// </summary>
internal sealed unsafe class Progress : Widget
{
    private string text;

    public Progress(string text)
    {
        this.text = text;
    }

    public string Text
    {
        get => text;
        set
        {
            if (text != value)
            {
                text = value;
                Invalidate();
            }
        }
    }

    public override bool HitTestVisible => false;

    /// <summary>Its ring turns for as long as it shows.</summary>
    public override bool Spinning => true;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var size = context.D(16);
        var top = PopupPaint.Round((Height - size) / 2);
        Ring(dc, context, new D2D_RECT_F(0, top, size, top + size));
        if (text.Length > 0)
        {
            var gap = context.D(10);
            var line = PopupPaint.Measure(context.Gfx, text, context.SmallPx, Weight.Regular, Width - size - gap);
            PopupPaint.Text(dc, context.Gfx, text, context.SmallPx, Weight.Regular, context.Theme.TextSecondary, size + gap, PopupPaint.Round((Height - line.Y) / 2), Width - size - gap);
        }
    }

    /// <summary>
    /// Three quarters of a circle in the accent colour, over a faint whole one, starting where
    /// <see cref="QuickContext.Spin"/> stands: the flyout's timer moves that on, so the ring turns.
    /// </summary>
    internal static void Ring(ID2D1DeviceContext* dc, QuickContext context, D2D_RECT_F box) =>
        Ring(dc, box, context.Spin, context.Scale, context.Theme.Accent, QuickTheme.WithAlpha(context.Theme.StrongFill, 0.35f));

    /// <summary>
    /// The ring in <paramref name="box"/> at the phase <paramref name="spin"/>, its arc in
    /// <paramref name="arc"/> over a whole one in <paramref name="track"/>: on the accent, a pending
    /// toggle's ring is drawn in the ink on it (SwitchPending).
    /// </summary>
    internal static void Ring(ID2D1DeviceContext* dc, D2D_RECT_F box, float spin, float scale, DXGI_RGBA arc, DXGI_RGBA track)
    {
        var stroke = MathF.Max(1.5f, 2 * scale);
        var centre = new System.Numerics.Vector2((box.left + box.right) / 2, (box.top + box.bottom) / 2);
        var radius = (box.right - box.left) / 2;
        PopupPaint.Ring(dc, centre, radius, stroke, track);
        ID2D1Factory* factory = null;
        ID2D1PathGeometry* path = null;
        ID2D1GeometrySink* sink = null;
        try
        {
            dc->GetFactory(&factory);
            if (factory == null || factory->CreatePathGeometry(&path).FAILED || path->Open(&sink).FAILED)
            {
                return;
            }
            var r = radius - stroke / 2;
            // Three quarters of a turn, clockwise, beginning at the phase the flyout keeps: twelve
            // o'clock is 0, and the ring turns as the phase grows.
            var from = (spin - 90) * MathF.PI / 180;
            var to = from + 1.5f * MathF.PI;
            sink->BeginFigure(new D2D_POINT_2F(centre.X + r * MathF.Cos(from), centre.Y + r * MathF.Sin(from)), D2D1_FIGURE_BEGIN.D2D1_FIGURE_BEGIN_HOLLOW);
            var segment = new D2D1_ARC_SEGMENT
            {
                point = new D2D_POINT_2F(centre.X + r * MathF.Cos(to), centre.Y + r * MathF.Sin(to)),
                size = new D2D_SIZE_F(r, r),
                rotationAngle = 0,
                sweepDirection = D2D1_SWEEP_DIRECTION.D2D1_SWEEP_DIRECTION_CLOCKWISE,
                arcSize = D2D1_ARC_SIZE.D2D1_ARC_SIZE_LARGE,
            };
            sink->AddArc(&segment);
            sink->EndFigure(D2D1_FIGURE_END.D2D1_FIGURE_END_OPEN);
            if (sink->Close().FAILED)
            {
                return;
            }
            var brush = Gfx.Brush(dc, arc);
            try
            {
                ID2D1StrokeStyle* round = null;
                var properties = new D2D1_STROKE_STYLE_PROPERTIES
                {
                    startCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                    endCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                    dashCap = D2D1_CAP_STYLE.D2D1_CAP_STYLE_ROUND,
                    lineJoin = D2D1_LINE_JOIN.D2D1_LINE_JOIN_ROUND,
                    miterLimit = 10,
                    dashStyle = D2D1_DASH_STYLE.D2D1_DASH_STYLE_SOLID,
                };
                _ = factory->CreateStrokeStyle(&properties, null, 0, &round);
                dc->DrawGeometry((ID2D1Geometry*)path, (ID2D1Brush*)brush, stroke, round);
                if (round != null)
                {
                    round->Release();
                }
            }
            finally
            {
                brush->Release();
            }
        }
        finally
        {
            if (sink != null)
            {
                sink->Release();
            }
            if (path != null)
            {
                path->Release();
            }
            if (factory != null)
            {
                factory->Release();
            }
        }
    }
}
