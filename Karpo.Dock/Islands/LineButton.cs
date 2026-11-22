// The one button of a folder's lines and of a face's cards. The person, of a notifications line
// and a focus line side by side (2026-10-25): "there are multiple buttons in this image, the ones on
// the bottom + - and clear and the notification button and focus time button. i would like all of
// them to have the size of the clear button. in general."
//
// So every button drawn inside a line or a card is the notifications' "Clear all" (docs\widgets.md §353;
// §185, Windows' PlatedButtonStyle, a compact standard button): 24 DIP tall, or the small text's line
// and 3 DIP over and under it where the text is larger; 8 DIP of padding either side of a word; the
// corners 4; the small text, 12; a glyph the small text's size, 4 from its word; 8 between two
// buttons. A glyph alone is a square of that height. A toggle is the same button, the accent's while
// on; a stepper is two glyph buttons with the value between; a subtle button (the transport, a row's
// pin) has no plate until the pointer comes, but is the same size.
//
// The painting is the quick settings' own WinUI button (Controls.Button) given these sizes, so the
// states — hot, pressed, disabled, the focus rectangle, high contrast — are one drawing everywhere.
//
// A button drawn smaller than WinUI's 32 DIP keeps a hit area of 32 (Hit): grown evenly round the
// button, so a 24 DIP glyph button 8 from the next has its 32 and they meet without overlapping.
//
// Not this: the quick-settings tiles, sliders' own icon column and switches, the note's toolbar (a
// toolbar), whole rows.

using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>The one size of a button on a line or a card, in px at one scale and text size (<see cref="Of(Gfx, float, float)"/>).</summary>
/// <param name="Height">How tall every button is; a glyph alone is a square of it.</param>
/// <param name="Pad">The padding either side of what a button holds.</param>
/// <param name="Radius">The corners.</param>
/// <param name="TextPx">A button's word.</param>
/// <param name="GlyphPx">A button's glyph.</param>
/// <param name="GlyphGap">Between a glyph and its word.</param>
/// <param name="Gap">Between two buttons side by side.</param>
/// <param name="Reach">The least a button's hit area is across and down (<see cref="Hit"/>).</param>
internal readonly record struct LineButton(float Height, float Pad, float Radius, float TextPx, float GlyphPx, float GlyphGap, float Gap, float Reach,
    float Scale, float TextScale)
{
    /// <summary>The sizes, in DIP before the text size: "Clear all"'s.</summary>
    public const float HeightDip = 24;

    public const float PadDip = 8;
    public const float RadiusDip = 4;
    public const float TextDip = 12;
    public const float GlyphGapDip = 4;
    public const float GapDip = 8;
    public const float ReachDip = 32;

    /// <summary>The air over and under a word where the word is taller than <see cref="HeightDip"/>.</summary>
    private const float AirDip = 3;

    public static LineButton Of(Gfx gfx, float scale, float textScale)
    {
        float D(float dip) => PopupPaint.Round(dip * scale);
        var textPx = TextDip * scale * textScale;
        var line = TilePaint.LineHeight(gfx, textPx);
        return new LineButton(
            Height: MathF.Max(D(HeightDip), PopupPaint.Round(line + D(2 * AirDip))),
            Pad: D(PadDip),
            Radius: D(RadiusDip),
            TextPx: textPx,
            GlyphPx: PopupPaint.Round(textPx),
            GlyphGap: D(GlyphGapDip),
            Gap: D(GapDip),
            Reach: D(ReachDip),
            Scale: scale,
            TextScale: textScale);
    }

    public static LineButton Of(Gfx gfx, in TileStyle style) => Of(gfx, style.Scale, style.TextScale);

    /// <summary>How wide a button holding <paramref name="text"/> is, after a glyph where <paramref name="glyph"/>; a glyph alone is square.</summary>
    public float Width(Gfx gfx, string text, bool glyph = false) =>
        text.Length == 0 ? Height
        : MathF.Max(Height, MathF.Ceiling((2 * Pad) + (glyph ? GlyphPx + GlyphGap : 0) + PopupPaint.Measure(gfx, text, TextPx, Weight.Regular).X));

    /// <summary>A button <paramref name="width"/> px wide from <paramref name="left"/>, its middle on <paramref name="middle"/>.</summary>
    public D2D_RECT_F At(float left, float middle, float width) => PopupPaint.Rect(left, PopupPaint.Round(middle - (Height / 2)), width, Height);

    /// <summary>A glyph button from <paramref name="left"/>, its middle on <paramref name="middle"/>.</summary>
    public D2D_RECT_F Square(float left, float middle) => At(left, middle, Height);

    /// <summary>Where a press lands on <paramref name="box"/>: the box grown evenly to <see cref="Reach"/> where it is smaller.</summary>
    public D2D_RECT_F Hit(D2D_RECT_F box)
    {
        if (box.right <= box.left)
        {
            return box;
        }
        var across = MathF.Max(0, (Reach - (box.right - box.left)) / 2);
        var down = MathF.Max(0, (Reach - (box.bottom - box.top)) / 2);
        return new D2D_RECT_F(box.left - across, box.top - down, box.right + across, box.bottom + down);
    }

    /// <summary>The same, kept between <paramref name="top"/> and <paramref name="bottom"/>: the line's own edges.</summary>
    public D2D_RECT_F Hit(D2D_RECT_F box, float top, float bottom)
    {
        var hit = Hit(box);
        return hit with { top = MathF.Max(hit.top, MathF.Min(box.top, top)), bottom = MathF.Min(hit.bottom, MathF.Max(box.bottom, bottom)) };
    }

    /// <summary>The button control at these sizes, for a caller that sets more of it (a picture, a badge) before <see cref="Paint(ID2D1DeviceContext*, Gfx, FlyoutTheme, Button)"/>.</summary>
    public Button Control(D2D_RECT_F box, string text, ButtonStyle style, string? glyph) =>
        Dress(new Button(text, style, glyph is { Length: > 0 } ? glyph : null) { Bounds = box });

    /// <summary>
    /// <paramref name="control"/> given these sizes — its word's, its padding, its glyph's, its picture's —
    /// for a line that places the quick settings' own buttons (the sound line's): its box is the caller's.
    /// </summary>
    public Button Dress(Button control)
    {
        control.TextSize = TextPx;
        control.Padding = Pad;
        control.GlyphSize = GlyphPx;
        control.GlyphGap = GlyphGap;
        control.PictureSize = MathF.Max(1, Height - PopupPaint.Round(4 * Scale));
        return control;
    }

    /// <summary>
    /// A button in <paramref name="box"/>: <paramref name="text"/> after <paramref name="glyph"/>, in
    /// <paramref name="style"/>, under the pointer where <paramref name="hot"/>, held down where
    /// <paramref name="pressed"/>. The keys' ring is the folder's (FolderFlyout.Keys.cs).
    /// </summary>
    public unsafe void Paint(ID2D1DeviceContext* dc, Gfx gfx, FlyoutTheme theme, D2D_RECT_F box, string text, string? glyph, ButtonStyle style,
        bool hot = false, bool pressed = false, bool enabled = true, bool dimmed = false)
    {
        if (box.right <= box.left || dc == null)
        {
            return;
        }
        var control = Control(box, text, style, glyph);
        control.Enabled = enabled;
        control.Dimmed = dimmed;
        control.Hot = hot;
        control.Pressed = pressed && hot;
        Paint(dc, gfx, theme, control);
    }

    /// <summary>A button control drawn where its <see cref="Widget.Bounds"/> say, with no transform, at these sizes.</summary>
    public unsafe void Paint(ID2D1DeviceContext* dc, Gfx gfx, FlyoutTheme theme, Button control)
    {
        control.AtBounds = true;
        control.Paint(dc, new QuickContext { Gfx = gfx, Theme = QuickTheme.For(theme), Scale = Scale, TextScale = TextScale });
    }
}
