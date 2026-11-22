// WinUI's buttons as Windows 11 draws them: the standard one (a control fill
// with a thin outline, darker along the bottom), the accent one (Connect, Save),
// the subtle one (a glyph or text on nothing until the pointer comes: the mute
// button, the header's refresh), and the hyperlink button ("More Wi-Fi
// settings"). 32 px high, radius 4, text 14 px.

using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal enum ButtonStyle
{
    Standard,
    Accent,
    Subtle,
    Link,
}

internal sealed unsafe class Button : Widget
{
    private string text;
    private string? glyph;
    private string? glyphBack;
    private string? badge;
    private Pixels? picture;
    private bool busy;
    private bool dimmed;

    public Button(string text, ButtonStyle style = ButtonStyle.Standard, string? glyph = null)
    {
        this.text = text;
        Style = style;
        this.glyph = glyph;
    }

    public ButtonStyle Style { get; }

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

    /// <summary>A Segoe Fluent Icons glyph instead of (or before) the text.</summary>
    public string? Glyph
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

    /// <summary>
    /// The glyph's size in px, where it is not WinUI's 16 DIPs: a list line's slider button, whose
    /// glyph is the size its list's glyphs are, and a line's ">" (SliderLine, SoundPartTile).
    /// </summary>
    public float? GlyphSize { get; set; }

    /// <summary>Between the glyph and the word in px, where it is not 8 DIPs: a line's buttons (LineButton).</summary>
    public float? GlyphGap { get; set; }

    /// <summary>The word's size in px, where it is not the body's 14 DIPs: a line's buttons, the small 12 (LineButton).</summary>
    public float? TextSize { get; set; }

    /// <summary>The padding either side of the content in px, where it is not WinUI's 11 DIPs: a line's buttons, 8 (LineButton).</summary>
    public float? Padding { get; set; }

    /// <summary>An app's icon's side in px, where it is not 20 DIPs: a line's mixer button (LineButton).</summary>
    public float? PictureSize { get; set; }

    /// <summary>The whole shape of a level glyph, drawn faint under it (a volume's missing waves).</summary>
    public string? GlyphBack
    {
        get => glyphBack;
        set
        {
            if (glyphBack != value)
            {
                glyphBack = value;
                Invalidate();
            }
        }
    }

    /// <summary>An app's icon in place of the glyph: the volume mixer's rows are one per app.</summary>
    public Pixels? Picture
    {
        get => picture;
        set
        {
            if (!ReferenceEquals(picture, value))
            {
                picture = value;
                Invalidate();
            }
        }
    }

    /// <summary>A small glyph over the content's bottom-right corner: the muted speaker on a mix's icon.</summary>
    public string? Badge
    {
        get => badge;
        set
        {
            if (badge != value)
            {
                badge = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// The content is drawn faint although the button still works: a muted mix, the way Windows
    /// shows one. It is not <see cref="Widget.Enabled"/>: a click unmutes it again.
    /// </summary>
    public bool Dimmed
    {
        get => dimmed;
        set
        {
            if (dimmed != value)
            {
                dimmed = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// Something it started is still going (a Wi-Fi scan): the ring takes the glyph's place, so the
    /// button itself says it is working. It stays usable: pressing it again asks again.
    /// </summary>
    public bool Busy
    {
        get => busy;
        set
        {
            if (busy != value)
            {
                busy = value;
                Invalidate();
            }
        }
    }

    public override bool Focusable => Enabled;

    /// <summary>The ring in place of its glyph turns while it is busy.</summary>
    public override bool Spinning => busy && picture is null;

    /// <summary>The width the button's content needs, with WinUI's 11 px of padding either side.</summary>
    public float NaturalWidth(QuickContext context)
    {
        var width = 2 * context.D(11);
        if (glyph is not null)
        {
            width += context.D(16) + (text.Length > 0 ? context.D(8) : 0);
        }
        if (text.Length > 0)
        {
            width += Label.WidthOf(context, text, LabelStyle.Body);
        }
        return MathF.Ceiling(width);
    }

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var radius = context.D(4);
        var down = Pressed && Hot;
        PaintWatch.Space(Spacing.Control, Local);
        DXGI_RGBA ink;
        switch (Style)
        {
            case ButtonStyle.Accent:
            {
                var fill = !Enabled ? theme.AccentDisabled : down ? theme.AccentPressed : Hot ? theme.AccentHover : theme.Accent;
                PopupPaint.Fill(dc, Local, radius, fill);
                ink = !Enabled ? theme.TextDisabled : down ? theme.TextOnAccentSecondary : theme.TextOnAccent;
                break;
            }
            case ButtonStyle.Standard:
            {
                var fill = !Enabled ? theme.ControlFillDisabled : down ? theme.ControlFillPressed : Hot ? theme.ControlFillHover : theme.ControlFill;
                PopupPaint.Fill(dc, Local, radius, fill);
                Outline(dc, context, Local, radius);
                ink = !Enabled ? theme.TextDisabled : down ? theme.TextSecondary : Hot && theme.HighContrast ? theme.TextOnHover : theme.Text;
                break;
            }
            case ButtonStyle.Link:
            {
                if (Enabled && (Hot || down))
                {
                    PopupPaint.Fill(dc, Local, radius, down ? theme.SubtlePressed : theme.SubtleHover);
                }
                ink = !Enabled ? theme.TextDisabled : down ? theme.TextSecondary : Hot ? theme.HighContrast ? theme.TextOnHover : theme.LinkHover : theme.Link;
                break;
            }
            default:
            {
                if (Enabled && (Hot || down))
                {
                    PopupPaint.Fill(dc, Local, radius, down ? theme.SubtlePressed : theme.SubtleHover);
                }
                ink = !Enabled ? theme.TextDisabled : down ? theme.TextSecondary : Hot && theme.HighContrast ? theme.TextOnHover : theme.Text;
                break;
            }
        }
        Content(dc, context, ink);
        if (FocusVisible)
        {
            FocusRing(dc, context, Local, radius, context.D(3));
        }
    }

    /// <summary>The glyph and the text, side by side, centred.</summary>
    private void Content(ID2D1DeviceContext* dc, QuickContext context, DXGI_RGBA ink)
    {
        if (dimmed)
        {
            ink = QuickTheme.WithAlpha(ink, 0.45f);
        }
        if (picture is { } art)
        {
            // An app's icon stands alone in the button (the mixer's rows have no text of their own).
            var side = PictureSize is { } own ? PopupPaint.Round(own) : context.D(20);
            var artBox = PopupPaint.Rect(Local.left + PopupPaint.Round((Width - side) / 2), Local.top + PopupPaint.Round((Height - side) / 2), side, side);
            PopupPaint.Picture(dc, art, artBox, 0, dimmed ? 0.45f : 1f);
            Mark(dc, context, artBox, ink);
            return;
        }
        var glyphPx = GlyphSize ?? context.GlyphPx;
        var glyphBox = glyph is null ? 0 : GlyphSize is { } size ? PopupPaint.Round(size) : context.D(16);
        var gap = glyph is not null && text.Length > 0 ? GlyphGap ?? context.D(8) : 0;
        var textPx = TextSize ?? context.TextPx;
        var room = MathF.Max(1, Width - 2 * (Padding ?? context.D(11)) - glyphBox - gap);
        var textSize = text.Length > 0 ? PopupPaint.Measure(context.Gfx, text, textPx, Weight.Regular, room) : default;
        var total = glyphBox + gap + textSize.X;
        var x = Local.left + PopupPaint.Round((Width - total) / 2);
        if (glyph is { } code)
        {
            var box = PopupPaint.Rect(x, Local.top + PopupPaint.Round((Height - glyphBox) / 2), glyphBox, glyphBox);
            if (busy)
            {
                // The ring turns while the work goes on, from the phase its flyout's timer keeps.
                Progress.Ring(dc, context, box);
            }
            else
            {
                if (glyphBack is { } back && back != code)
                {
                    // The arcs a signal reaches sit inside the faint full set: one origin for both.
                    PopupPaint.Glyph(dc, context.Gfx, back, glyphPx, QuickTheme.WithAlpha(ink, 0.3f), box);
                    PopupPaint.GlyphOver(dc, context.Gfx, code, back, glyphPx, ink, box);
                }
                else
                {
                    PopupPaint.Glyph(dc, context.Gfx, code, glyphPx, ink, box);
                }
                Mark(dc, context, box, ink);
            }
        }
        if (text.Length > 0)
        {
            PopupPaint.Text(dc, context.Gfx, text, textPx, Weight.Regular, ink, x + glyphBox + gap, Local.top + PopupPaint.Round((Height - textSize.Y) / 2), room);

        }
    }

    /// <summary>The badge over the content's bottom-right corner, on a disc so it reads over any icon.</summary>
    private void Mark(ID2D1DeviceContext* dc, QuickContext context, D2D_RECT_F box, DXGI_RGBA ink)
    {
        if (badge is not { Length: > 0 } code)
        {
            return;
        }
        var small = context.D(11);
        var at = PopupPaint.Rect(box.right - small + context.D(2), box.bottom - small + context.D(2), small, small);
        var centre = new System.Numerics.Vector2((at.left + at.right) / 2, (at.top + at.bottom) / 2);
        PopupPaint.Circle(dc, centre, small / 2 + context.D(1), context.Theme.ControlSolid);
        PopupPaint.Glyph(dc, context.Gfx, code, 9 * context.Scale, ink, at);
    }

    /// <summary>WinUI's control border: ControlStrokeColorDefault, darker (ControlStrokeColorSecondary) along the bottom.</summary>
    internal static void Outline(ID2D1DeviceContext* dc, QuickContext context, D2D_RECT_F rect, float radius)
    {
        var line = context.Hairline;
        PopupPaint.Stroke(dc, rect, radius, line, context.Theme.ControlStroke);
        if (!context.Theme.HighContrast)
        {
            // Only the straight part of the bottom edge, between the corners.
            PopupPaint.Fill(dc, new D2D_RECT_F(rect.left + radius, rect.bottom - line, rect.right - radius, rect.bottom), 0, context.Theme.ControlStrokeBottom);
        }
    }
}
