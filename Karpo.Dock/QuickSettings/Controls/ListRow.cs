// A row of a quick-settings list: a network, a Bluetooth device, an output, a
// power mode. A glyph (with its faint whole shape under a level glyph, and a
// small badge such as a lock) or an app's picture, the name and, under it, where
// it stands; a chevron or a check at the right, and a short tail before it (a
// device's battery: its glyph filled to the level, and the percentage). The chosen one of a list (the
// output in use) gets WinUI's selected look: a subtle fill and the 3 × 16 px
// accent pill at its left. So does a row opened in place onto what it offers (a Bluetooth device onto
// Connect and Remove, a network onto its key, §605): it is one item with what it offers, its band the
// whole of both (Below), so the whole of it lights under the pointer — behind its buttons too, which
// light themselves over it — and its pill stands in the middle of all of it.
//
// A row whose page gave it a menu of its own (ContextClicked) carries the "..."
// mark at its end while the pointer or the keyboard is on it. Its room is kept
// on every row whether the mark is drawn or not: a mark that appeared on hover
// and pushed the name along would move the row under a pointer that was only
// passing over it.

using System.Numerics;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock.QuickSettings.Controls;

internal sealed unsafe class ListRow : Widget
{
    /// <summary>
    /// Its glyph's column from where its row stands, and where its words begin: the list's own columns (§388; the
    /// person, 2026-10-29: one design for the folder's lines and the quick settings). A row of a flyout stands
    /// <see cref="PadDip"/> in from the panel's rows; a line's has its glyph on the gutter, its words at the text
    /// edge where every name begins (PageLayout.ListLead).
    /// </summary>
    internal const float PadDip = 8;

    /// <summary>Where its words begin from its glyph's column: the column and the gap after it, at the text's size.</summary>
    internal static float TextAfter(QuickContext context) => context.T(20) + context.T(12);

    /// <summary>
    /// Where its glyph's column begins from its left edge, px; its <see cref="PadDip"/> where none was set. Its
    /// bounds are its band (LineItem, §428): the list's whole width, and half the room to the rows over and under
    /// it (<see cref="Over"/>, <see cref="Under"/>), so the rows of a list touch; what it shows stands where it did.
    /// </summary>
    public float? Lead { get; set; }

    /// <summary>The band over what it shows, px: half the room between it and the row over it.</summary>
    public float Over { get; set; }

    /// <summary>The band under what it shows, px: half the room between it and the row under it.</summary>
    public float Under { get; set; }

    /// <summary>
    /// The room of its own under what it shows, px, over <see cref="Under"/>: what an opened row offers (its Connect,
    /// its key), which stands over its band (PageLayout.Opened). 0 for a row that is not open.
    /// </summary>
    public float Below { get; set; }

    /// <summary>Opened in place onto what it offers (PageLayout.Opened): drawn chosen, its pill in the middle of its whole band.</summary>
    public bool Open { get => open; set => Set(ref open, value); }

    /// <summary>
    /// A control of its list standing over its band at its end (the microphones' mute), placed beside it in its
    /// parent's px as it is: its words end a button's gap short of the control, wherever the control stands.
    /// </summary>
    public Widget? Control { get; set; }

    /// <summary>
    /// Where it last drew its glyph (or picture) and its words, its own px: what it shows of its band, which a
    /// test holds against what stands over it. A row is painted before it can be looked at, as the menu's mark is.
    /// </summary>
    internal (D2D_RECT_F Glyph, D2D_RECT_F Words) Shown { get; private set; }

    /// <summary>Segoe Fluent Icons: More, the three dots a thing's own menu is behind.</summary>
    private const string MoreGlyph = "\uE712";

    // Where the mark was last drawn, for the click that opens the menu with the left button: a row
    // is painted before it can be pointed at, so the box is always the one on screen.
    private D2D_RECT_F menuMark;

    private string title = "";
    private string? detail;
    private string? glyph;
    private string? glyphBack;
    private string? badge;
    private Pixels? picture;
    private string? trailing;
    private IReadOnlyList<Pixels> pictures = [];
    private string? tail;
    private string? tailRoom;
    private string? tailGlyph;
    private bool tailGlyphRoom;
    private bool strong;
    private bool selected;
    private bool open;

    public string Title { get => title; set => Set(ref title, value); }

    /// <summary>The second, quieter line: "Connected, secured".</summary>
    public string? Detail { get => detail; set => Set(ref detail, value); }

    public string? Glyph { get => glyph; set => Set(ref glyph, value); }

    /// <summary>A level glyph's whole shape, faint under it (the Wi-Fi arcs a weak signal doesn't reach).</summary>
    public string? GlyphBack { get => glyphBack; set => Set(ref glyphBack, value); }

    /// <summary>A small glyph over the glyph's bottom-right corner: a secured network's lock.</summary>
    public string? Badge { get => badge; set => Set(ref badge, value); }

    /// <summary>An app's icon in place of the glyph.</summary>
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

    /// <summary>A glyph at the right: a chevron to a page, a check.</summary>
    public string? Trailing { get => trailing; set => Set(ref trailing, value); }

    /// <summary>
    /// Pictures at the row's right end, in reading order: the apps open on a virtual desktop, which
    /// is what the desktop switcher's rows show ("the program icons on the right aligned"). They
    /// take their room from the name's, so a row with many of them shortens the name rather than
    /// running under it.
    /// </summary>
    public IReadOnlyList<Pixels> Pictures
    {
        get => pictures;
        set
        {
            if (!ReferenceEquals(pictures, value))
            {
                pictures = value;
                Invalidate();
            }
        }
    }

    /// <summary>
    /// A short piece of text at the row's right end: "+3" for the apps a switcher's row had no
    /// room to show, "72%" for a device that reports its battery. Empty where there is none.
    /// </summary>
    public string? Tail { get => tail; set => Set(ref tail, value); }

    /// <summary>
    /// The widest <see cref="Tail"/> this row will ever show, whose room is then kept whether there
    /// is a tail or not: a device that reports its battery sits beside one that does not, and a
    /// level arriving a second later must not shorten the name beside it. Null keeps no room and
    /// lets the tail take what it needs, which is what a count that is there or not wants.
    /// </summary>
    public string? TailRoom { get => tailRoom; set => Set(ref tailRoom, value); }

    /// <summary>
    /// A glyph before the <see cref="Tail"/>: a device's battery, filled to its level, beside the
    /// percentage, as Windows' Bluetooth page shows it. Drawn only with a tail.
    /// </summary>
    public string? TailGlyph { get => tailGlyph; set => Set(ref tailGlyph, value); }

    /// <summary>
    /// Whether the room <see cref="TailRoom"/> keeps has a <see cref="TailGlyph"/>'s in it too, so a
    /// row whose device says no level is no wider than one whose device does.
    /// </summary>
    public bool TailGlyphRoom { get => tailGlyphRoom; set => Set(ref tailGlyphRoom, value); }

    /// <summary>The name in semibold: the network or device in use.</summary>
    public bool Strong { get => strong; set => Set(ref strong, value); }

    /// <summary>The chosen one of its list.</summary>
    public bool Selected { get => selected; set => Set(ref selected, value); }

    public override bool Focusable => Enabled && Clicked is not null;

    /// <summary>
    /// Whether <paramref name="local"/> (the row's own px) is on the menu's mark: a left click
    /// there opens the row's menu instead of choosing the row, because a mark that only answered
    /// the right button would be a worse lie than no mark at all.
    /// </summary>
    protected internal override bool OnMenuMark(Vector2 local) =>
        ContextClicked is not null
        && menuMark.right > menuMark.left
        && local.X >= menuMark.left && local.X < menuMark.right
        && local.Y >= menuMark.top && local.Y < menuMark.bottom;

    public override void Paint(ID2D1DeviceContext* dc, QuickContext context)
    {
        var theme = context.Theme;
        var radius = context.D(4);
        var down = Pressed && Hot && Clicked is not null;
        var lit = Hot && Clicked is not null && Enabled;
        // Its band lit, edge to edge (LineItem) — an opened row's what it offers too: the chosen one's fill, a step
        // quieter under the pointer, else the pointer's.
        var chosen = selected || open;
        if (chosen)
        {
            LineItem.Chosen(dc, Local, theme, lit || down);
        }
        else if (down || lit)
        {
            LineItem.Light(dc, Local, theme, down);
        }
        // What it shows, where it stood: its band less the room it took from its neighbours and what it offers under it.
        var pad = Lead ?? context.D(PadDip);
        var top = Over;
        var tall = MathF.Max(1, Height - Over - Under - Below);
        float Middle(float side) => top + PopupPaint.Round((tall - side) / 2);
        if (chosen)
        {
            // In the middle of the item: what it shows and what it offers under it.
            LineItem.Pill(dc, PopupPaint.Rect(0, top, Width, MathF.Max(1, Height - Over - Under)), MathF.Max(0, pad - context.D(PadDip)), context.Scale, 1, theme.Accent);
        }
        var bright = (lit || down || chosen) && theme.HighContrast;
        var ink = !Enabled ? theme.TextDisabled : bright ? theme.TextOnHover : theme.Text;
        var quiet = !Enabled ? theme.TextDisabled : bright ? theme.TextOnHover : theme.TextSecondary;

        var box = context.T(20);
        var boxRect = PopupPaint.Rect(pad, Middle(box), box, box);
        if (picture is { } icon)
        {
            PopupPaint.Picture(dc, icon, boxRect, 0);
        }
        else if (glyph is { } code)
        {
            if (glyphBack is { } back && back != code)
            {
                // The arcs a signal reaches sit inside the faint full set: one origin for both.
                PopupPaint.Glyph(dc, context.Gfx, back, context.GlyphPx, QuickTheme.WithAlpha(ink, 0.3f), boxRect);
                PopupPaint.GlyphOver(dc, context.Gfx, code, back, context.GlyphPx, ink, boxRect);
            }
            else
            {
                PopupPaint.Glyph(dc, context.Gfx, code, context.GlyphPx, ink, boxRect);
            }
        }
        if (badge is { } mark)
        {
            var small = context.D(10);
            var at = PopupPaint.Rect(boxRect.right - small + context.D(2), boxRect.bottom - small + context.D(1), small, small);
            PopupPaint.Glyph(dc, context.Gfx, mark, 9 * context.Scale, ink, at);
        }

        var textLeft = pad + TextAfter(context);
        var right = Width - pad;
        if (Control is { } over)
        {
            right = MathF.Min(right, over.Bounds.left - Bounds.left - context.D(LineButton.GapDip));
        }
        // The row's own menu has a mark at the end, and its room is kept whether it is drawn or
        // not: a mark that appeared on hover and pushed the name along would move the row under
        // the pointer that was only passing over it. It is drawn while the pointer is on the row
        // or the keyboard is, which is the "..." every list of this kind uses.
        if (ContextClicked is not null)
        {
            var side = context.D(16);
            menuMark = PopupPaint.Rect(right - side, Middle(side), side, side);
            if ((lit || down || Focused) && Enabled)
            {
                PopupPaint.Glyph(dc, context.Gfx, MoreGlyph, 12 * context.Scale, quiet, menuMark);
            }
            right -= side + context.D(6);
        }
        else
        {
            menuMark = default;
        }
        if (trailing is { } end)
        {
            var endBox = context.D(16);
            PopupPaint.Glyph(dc, context.Gfx, end, 12 * context.Scale, quiet, PopupPaint.Rect(right - endBox, Middle(endBox), endBox, endBox));
            right -= endBox + context.D(8);
        }
        // The tail first, then the pictures right to left, so they read left to right and the last
        // of them is the one nearest the tail. Where a room was named, that room is taken whether
        // there is a tail to put in it or not, so the name beside it never moves.
        // A glyph before the tail (a battery) takes a box of its own and a small gap, kept in the
        // room too where the page asks for it.
        var glyphSide = context.D(16);
        var glyphGap = context.D(4);
        var tailWidth = tailRoom is { Length: > 0 } widest
            ? PopupPaint.Measure(context.Gfx, widest, context.SmallPx, Weight.Regular, MathF.Max(1, right - textLeft)).X
                + (tailGlyphRoom ? glyphSide + glyphGap : 0)
            : 0;
        if (tail is { Length: > 0 } said)
        {
            var size = PopupPaint.Measure(context.Gfx, said, context.SmallPx, Weight.Regular, MathF.Max(1, right - textLeft));
            var drawn = size.X;
            var textAt = right - size.X;
            if (tailGlyph is { Length: > 0 } level)
            {
                drawn += glyphGap + glyphSide;
                // In the room kept for it, the glyph starts the room and the text follows, so the
                // glyphs of a list stand in one column whatever their percentages; without a room,
                // the two sit at the end.
                var at = tailGlyphRoom && tailWidth >= drawn ? right - tailWidth : right - drawn;
                PopupPaint.Glyph(dc, context.Gfx, level, 16 * context.Scale, quiet, PopupPaint.Rect(at, Middle(glyphSide), glyphSide, glyphSide));
                textAt = at + glyphSide + glyphGap;
            }
            PopupPaint.Text(dc, context.Gfx, said, context.SmallPx, Weight.Regular, quiet, textAt, Middle(size.Y), size.X);
            right -= MathF.Max(drawn, tailWidth) + context.D(6);
        }
        else if (tailWidth > 0)
        {
            right -= tailWidth + context.D(6);
        }
        if (pictures.Count > 0)
        {
            var side = context.D(16);
            for (var index = pictures.Count - 1; index >= 0; index--)
            {
                right -= side;
                PopupPaint.Picture(dc, pictures[index], PopupPaint.Rect(right, Middle(side), side, side), 0);
                right -= context.D(6);
            }
            right -= context.D(6);
        }
        var room = MathF.Max(1, right - textLeft);
        var weight = strong ? Weight.Semibold : Weight.Regular;
        var first = PopupPaint.Measure(context.Gfx, title, context.TextPx, weight, room);
        if (detail is { Length: > 0 } second)
        {
            var under = PopupPaint.Measure(context.Gfx, second, context.SmallPx, Weight.Regular, room);
            var lines = Middle(first.Y + under.Y);
            PopupPaint.Text(dc, context.Gfx, title, context.TextPx, weight, ink, textLeft, lines, room);
            PopupPaint.Text(dc, context.Gfx, second, context.SmallPx, Weight.Regular, quiet, textLeft, lines + first.Y, room);
            Shown = (picture is not null || glyph is not null ? boxRect : default, PopupPaint.Rect(textLeft, lines, MathF.Max(first.X, under.X), first.Y + under.Y));
        }
        else
        {
            PopupPaint.Text(dc, context.Gfx, title, context.TextPx, weight, ink, textLeft, Middle(first.Y), room);
            Shown = (picture is not null || glyph is not null ? boxRect : default, PopupPaint.Rect(textLeft, Middle(first.Y), first.X, first.Y));
        }
        if (FocusVisible)
        {
            FocusRing(dc, context, PopupPaint.Rect(pad - context.D(PadDip), top, Width - (2 * (pad - context.D(PadDip))), tall), radius, 0);
        }
    }

    private void Set<T>(ref T field, T value)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            Invalidate();
        }
    }
}
