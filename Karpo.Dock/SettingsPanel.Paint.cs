// edit, §520: the edit bar and §575 its flyouts drawn, from their layout (PanelLayout), with the dock's Direct2D and
// DirectWrite helpers (PopupPaint, Gfx) in the flyouts' WinUI tokens (FlyoutTheme): the bar one surface, the flyout open
// another, each drawn whole whenever what it shows changes — a page, a control, the pointer over another thing — and shown
// in the commit that brings it, so nothing between two pictures is ever seen. §548: where a thing stands is the layout's,
// every size PanelMetrics' — the painter adds no offset of its own; one style for one thing (one pill, one chevron place,
// words 12 before their control). The type ramp is WinUI's: Body 14 for every name and every control's words, Body Strong
// 14 for a section's heading, Caption 12 for every second line and value at a row's end, Subtitle 20 for a header card's name.

using System.Numerics;
using Karpo.Core;
using Karpo.Dock.QuickSettings.Controls;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>
/// §520: what the pointer and the keys are on, besides the layout: lit, pressed, with the keys, a search's pick lit for a
/// moment; §551 a row being dragged into order (<paramref name="Drag"/>), the others making room for it; §670 a colour being
/// mixed in an inline picker, its body's key (<paramref name="Mixing"/>).
/// </summary>
internal readonly record struct PanelMarks(PanelHit Hot, PanelHit Pressed, PanelHit Focus, string? Lit, bool Keys, PanelDrag? Drag = null, (string Key, Argb Color, Hsv Hsv)? Mixing = null);

/// <summary>§520: paints the bar and its flyout (see the top of the file); §521 a text or a number box being typed in (<paramref name="editing"/>: its row's key and its field).</summary>
internal sealed unsafe partial class PanelPainter(Gfx gfx, FlyoutTheme theme, PanelLayout layout, PanelMarks marks, (string Key, MenuTextField Field)? editing = null)
{
    private const string TitleFamily = "Segoe UI Variable Display"; // not-ui: a font

    private ID2D1DeviceContext* dc;

    private float P(float dip) => layout.P(dip);

    private float Hair => layout.Hair;

    private static DXGI_RGBA Ink(Color color) => PopupPaint.Ink(color);

    private void Fill(RectF r, float radius, Color color) => PopupPaint.Fill(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), radius, Ink(color));

    private void Rim(RectF r, float radius, Color color) => PopupPaint.Stroke(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), radius, Hair, Ink(color));

    /// <summary>
    /// §544: a control's rim as WinUI's ControlElevationBorderBrush draws it — ControlStrokeColorDefault, its edge in
    /// ControlStrokeColorSecondary over 3 DIP (the top in the dark theme, the bottom in the light one) —; <paramref name="flat"/>
    /// (pressed, disabled) ControlStrokeColorDefault alone, as Button's and ComboBox's pressed and disabled borders are.
    /// </summary>
    private void Elevated(RectF r, float radius, bool flat = false)
    {
        if (flat || theme.HighContrast)
        {
            Rim(r, radius, theme.ControlStroke);
            return;
        }
        PopupPaint.ElevationStroke(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), radius, Hair, Ink(theme.ControlStroke), Ink(theme.ControlStrokeSecondary), P(3), top: theme.ElevationAtTop);
    }

    /// <summary>
    /// §544: a text box's or a number box's rim, TextControlElevationBorderBrush: ControlStrokeColorDefault, its bottom
    /// ControlStrongStrokeColorDefault, the 1 DIP of a 2 DIP band; disabled ControlStrokeColorDefault alone.
    /// </summary>
    private void TextRim(RectF r, float radius, bool greyed)
    {
        if (greyed || theme.HighContrast)
        {
            Rim(r, radius, theme.ControlStroke);
            return;
        }
        PopupPaint.ElevationStroke(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), radius, Hair, Ink(theme.ControlStroke), Ink(theme.ControlStrongStroke), P(2), share: 0.5f);
    }

    /// <summary>
    /// §544: a standard button's fill (Button, ComboBox, the colour button): ControlFillColorDefault, Secondary under the
    /// pointer, Tertiary pressed, ControlFillColorDisabled greyed.
    /// </summary>
    private Color ControlOf(PanelHit hit, bool greyed) =>
        greyed ? theme.ControlFillDisabled
        : hit.IsNone ? theme.ControlFill
        : marks.Pressed == hit && marks.Hot == hit ? theme.ControlFillPressed
        : marks.Hot == hit ? theme.ControlFillHover
        : theme.ControlFill;

    /// <summary>§544: a standard button's words: TextFillColorPrimary, Secondary pressed, Disabled greyed.</summary>
    private DXGI_RGBA WordsOf(PanelHit hit, bool greyed) =>
        greyed ? theme.TextDisabled : !hit.IsNone && marks.Pressed == hit && marks.Hot == hit ? theme.TextSecondary : theme.Text;

    private bool IsPressed(PanelHit hit) => !hit.IsNone && marks.Pressed == hit && marks.Hot == hit;

    private float Measure(string text, float dip, Weight weight = Weight.Regular) => PopupPaint.Measure(gfx, text, P(dip), weight).X;

    /// <summary>One line, its middle at <paramref name="mid"/>, cut at <paramref name="most"/> px; how wide it came.</summary>
    private float Text(string text, float x, float mid, float dip, DXGI_RGBA ink, Weight weight = Weight.Regular, float? most = null, string? family = null)
    {
        if (text.Length == 0 || most is <= 1)
        {
            return 0;
        }
        var size = PopupPaint.Measure(gfx, text, P(dip), weight, most, family);
        PopupPaint.Text(dc, gfx, text, P(dip), weight, ink, x, mid - (size.Y / 2), most, family);
        return size.X;
    }

    /// <summary>One line, its right end at <paramref name="right"/>; where it starts.</summary>
    private float TextBefore(string text, float right, float mid, float dip, DXGI_RGBA ink, float most)
    {
        var w = MathF.Min(Measure(text, dip), most);
        Text(text, right - w, mid, dip, ink, most: most);
        return right - w;
    }

    private void Glyph(string code, float cx, float cy, float dip, DXGI_RGBA ink)
    {
        var box = P(dip * 1.5f);
        PopupPaint.Glyph(dc, gfx, code, P(dip), ink, PopupPaint.Rect(cx - (box / 2), cy - (box / 2), box, box));
    }

    /// <summary>A row's or a side item's icon: its glyph, or its picture (a search engine's logo, Karpo's).</summary>
    private void Icon(FlyoutIcon? icon, float cx, float cy, DXGI_RGBA ink)
    {
        var side = P(PanelMetrics.Icon);
        switch (icon)
        {
            case FlyoutIcon.Glyph glyph:
                Glyph(glyph.Code, cx, cy, PanelMetrics.Icon, ink);
                break;
            case FlyoutIcon.Picture picture:
                PopupPaint.Picture(dc, picture.Pixels, PopupPaint.Rect(cx - (side / 2), cy - (side / 2), side, side), 0);
                break;
            case FlyoutTile tile:
                Swatch(tile, cx, cy);
                break;
        }
    }

    /// <summary>
    /// The focus ring round <paramref name="r"/>, while the keys move it (§520 decision 11): WinUI's system focus visual,
    /// §544 both its rings — 2 DIP of FocusStrokeColorOuter, then 1 DIP of FocusStrokeColorInner against the control (its
    /// FocusVisualMargin −3).
    /// </summary>
    private void FocusRing(RectF r, float radius)
    {
        var outer = new RectF(r.X - P(3), r.Y - P(3), r.Width + P(6), r.Height + P(6));
        PopupPaint.Stroke(dc, PopupPaint.Rect(outer.X, outer.Y, outer.Width, outer.Height), radius + P(3), P(2), Ink(theme.FocusOuter));
        var inner = new RectF(r.X - P(1), r.Y - P(1), r.Width + P(2), r.Height + P(2));
        PopupPaint.Stroke(dc, PopupPaint.Rect(inner.X, inner.Y, inner.Width, inner.Height), radius + P(1), P(1), Ink(theme.FocusInner));
    }

    private bool Focused(PanelHit hit) => marks.Keys && marks.Focus == hit;

    /// <summary>A button's glyph and words, from its start: 12, the glyph, 8, the words (cut at its end less 12).</summary>
    private void GlyphAndWords(RectF r, string glyph, string words, DXGI_RGBA ink)
    {
        var mid = r.Y + (r.Height / 2);
        var g = P(PanelMetrics.Icon);
        var x = r.X + P(PanelMetrics.Label);
        Glyph(glyph, x + (g / 2), mid, PanelMetrics.Icon, ink);
        Text(words, x + g + P(PanelMetrics.Gap), mid, PanelMetrics.Body, ink, most: r.Right - P(PanelMetrics.Label) - (x + g + P(PanelMetrics.Gap)));
    }

    /// <summary>§548: a chevron, its middle 20 in from <paramref name="end"/> (a control's end, a card's controls' end), as every chevron.</summary>
    private void Chevron(string glyph, float end, float mid, DXGI_RGBA ink) =>
        Glyph(glyph, end - P(PanelMetrics.ChevronIn), mid, PanelMetrics.Chevron, ink);

    private Color SubtleOf(PanelHit hit) =>
        marks.Pressed == hit && marks.Hot == hit ? theme.SubtlePressed : marks.Hot == hit ? theme.SubtleHover : Color.FromArgb(0, 0, 0, 0);

    // --- The whole ----------------------------------------------------------------------------------

    /// <summary>§575: the bar: its ground, its entries and buttons, §596 the selection's among them.</summary>
    public void PaintBar(ID2D1DeviceContext* into)
    {
        dc = into;
        // §544: the Settings window's ground as Windows shows it on a window without the activation: Mica's fallback,
        // SolidBackgroundFillColorBase.
        Fill(new RectF(0, 0, layout.Size.X, layout.Size.Y), 0, theme.Ground);
        Bar();
    }

    /// <summary>
    /// §575: the flyout: the page open — its ground, the add list's search, its cards, its scroll indicator. §629 without its
    /// <paramref name="ground"/> where it stands on a ground of its own (the edit bar's: SeeThroughView, the ghost over it).
    /// </summary>
    public void PaintFlyout(ID2D1DeviceContext* into, MenuTextField? filter = null, bool ground = true)
    {
        dc = into;
        if (ground)
        {
            Fill(new RectF(0, 0, layout.FlyoutSize.X, layout.FlyoutSize.Y), 0, theme.Ground);
        }
        if (!layout.IsOpen)
        {
            return;
        }
        if (filter is not null && !layout.Filter.IsEmpty)
        {
            Field(filter, layout.Filter, FilterLook(layout));
            if (Focused(new PanelHit(PanelHitKind.Filter)) && !filter.Focused)
            {
                FocusRing(layout.Filter, P(PanelMetrics.Corner));
            }
        }
        if (!layout.BackHead.IsEmpty)
        {
            BackHead();
        }
        var view = layout.Viewport;
        var clip = PopupPaint.Rect(view.X, view.Y, view.Width, view.Height);
        dc->PushAxisAlignedClip(&clip, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            Page();
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
        if (layout.MostScroll > 0)
        {
            // The page's scroll indicator, as tall as the share shown, where the share is: 4 in from the edge, 8 off the
            // viewport's ends.
            var room = view.Height - (2 * P(PanelMetrics.Gap));
            var bar = MathF.Max(P(PanelMetrics.ScrollLeast), room * view.Height / layout.ContentHeight);
            var top = view.Y + P(PanelMetrics.Gap) + ((room - bar) * layout.Scroll / layout.MostScroll);
            var w = P(PanelMetrics.ScrollBar);
            Fill(new RectF(layout.FlyoutSize.X - P(PanelMetrics.Tight) - w, top, w, bar), w / 2, theme.ScrollIndicator);
        }
    }

    /// <summary>
    /// §628: a page under another's entry its head (BackHead's, as Windows 11's quick settings head a page shown in place of
    /// theirs): one plate, no fill nor rim until the pointer is on it (SubtleFillColorSecondary, Tertiary pressed), Windows' back
    /// arrow at its start and the page's title in Body Strong after it, both in the text's ink.
    /// </summary>
    private void BackHead()
    {
        var hit = new PanelHit(PanelHitKind.Back);
        var r = layout.BackHead;
        Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
        if (Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner));
        }
        var arrow = layout.BackArrow;
        var mid = r.Y + (r.Height / 2);
        var ink = IsPressed(hit) ? theme.TextSecondary : theme.Text;
        Glyph(Mirror.On ? BackForwardGlyph : BackHeadGlyph, arrow.X + (arrow.Width / 2), mid, PanelMetrics.Icon, ink);
        var title = layout.BackTitle;
        Text(layout.Shown?.Title ?? "", title.X, mid, PanelMetrics.Body, ink, Weight.Semibold, most: title.Width);
    }

    /// <summary>§628: Windows' back arrow (Segoe Fluent Icons' Back), as BackHead's; the forward one where mirrored, as it points back there.</summary>
    private const string BackHeadGlyph = Karpo.Dock.QuickSettings.Controls.BackHead.Glyph;

    private const string BackForwardGlyph = Karpo.Dock.QuickSettings.Controls.BackHead.ForwardGlyph;

    // --- The bar ------------------------------------------------------------------------------------

    private void Bar()
    {
        // §608: the divider between the pages' entries and the selection's, AppBarSeparator's DividerStrokeColorDefault.
        if (!layout.Divider.IsEmpty)
        {
            Fill(layout.Divider, 0, theme.Divider);
        }
        foreach (var part in layout.Header)
        {
            var r = part.Rect;
            var hit = PanelLayout.HitOf(part);
            var mid = r.Y + (r.Height / 2);
            // §575: the entry whose flyout stands open, selected: SubtleFillColorSecondary under its words, Tertiary under the
            // pointer, and the accent pill at its foot (WinUI's SelectorBar).
            var selected = layout.Opened(hit);
            if (part.Kind == PanelHitKind.Done)
            {
                // §544: an AccentButton: its fills, and AccentControlElevationBorderBrush round it (none pressed).
                Fill(r, P(PanelMetrics.Corner), IsPressed(hit) ? theme.AccentPressed : marks.Hot == hit ? theme.AccentHover : theme.Accent);
                if (!IsPressed(hit) && !theme.HighContrast)
                {
                    PopupPaint.ElevationStroke(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), P(PanelMetrics.Corner), Hair, Ink(theme.AccentStroke), Ink(theme.AccentStrokeBottom), P(3));
                }
            }
            else if (selected)
            {
                Fill(r, P(PanelMetrics.Corner), IsPressed(hit) ? theme.SubtleHover : marks.Hot == hit ? theme.SubtlePressed : theme.SubtleHover);
                var w = P(PanelMetrics.PillTall);
                var h = P(PanelMetrics.PillWide);
                Fill(new RectF(r.X + MathF.Round((r.Width - w) / 2), r.Bottom - h, w, h), h / 2, theme.Accent);
            }
            else
            {
                Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
            }
            if (Focused(hit))
            {
                FocusRing(r, P(PanelMetrics.Corner));
            }
            switch (part.Kind)
            {
                // §575: an entry showing its icon alone (a narrow display): its icon in its middle.
                case PanelHitKind.Nav when part.Compact:
                    Glyph(part.Glyph, r.X + (r.Width / 2), mid, PanelMetrics.Icon, IsPressed(hit) ? theme.TextSecondary : theme.Text);
                    break;
                // §578: the errors an icon button (their count its tooltip and its name), in the room kept for them.
                case PanelHitKind.Errors:
                {
                    var gx = r.X + (r.Width / 2);
                    // §544: an InfoBar's error icon: SystemFillColorCritical, its glyph TextFillColorInverse.
                    Glyph(PanelGlyphs.ErrorRing, gx, mid, PanelMetrics.Icon, Ink(theme.Critical));
                    Glyph(PanelGlyphs.ErrorX, gx, mid, PanelMetrics.Icon, theme.TextInverse);
                    break;
                }
                case PanelHitKind.Done:
                    Words(part, IsPressed(hit) ? theme.TextOnAccentSecondary : theme.TextOnAccent);
                    break;
                case PanelHitKind.Add:
                    Words(part, !layout.Input.CanAdd ? theme.TextDisabled : IsPressed(hit) ? theme.TextSecondary : theme.Text);
                    break;
                default:
                    Words(part, IsPressed(hit) ? theme.TextSecondary : theme.Text);
                    break;
            }
        }

        // A button's glyph and words centred in it; §596 one narrower than its words (a selection's entry in its room) its
        // glyph and words from its start, cut short 12 from its end.
        void Words(PanelPart part, DXGI_RGBA ink)
        {
            var r = part.Rect;
            var mid = r.Y + (r.Height / 2);
            var g = P(PanelMetrics.Icon);
            var words = Measure(part.Words, PanelMetrics.Body);
            var inset = MathF.Floor((r.Width - (g + P(PanelMetrics.Gap) + words)) / 2);
            var cut = inset < P(PanelMetrics.Label);
            var left = r.X + (cut ? P(PanelMetrics.Label) : inset);
            Glyph(part.Glyph, left + (g / 2), mid, PanelMetrics.Icon, ink);
            var at = left + g + P(PanelMetrics.Gap);
            Text(part.Words, at, mid, PanelMetrics.Body, ink, most: cut ? r.Right - P(PanelMetrics.Label) - at : null);
        }
    }

    /// <summary>Karpo's one text box (<paramref name="field"/>) drawn at <paramref name="r"/> as <paramref name="look"/> says.</summary>
    private void Field(MenuTextField field, RectF r, in FieldLook look)
    {
        var shift = D2D_MATRIX_3X2_F.Translation(r.X, r.Y);
        D2D_MATRIX_3X2_F was;
        dc->GetTransform(&was);
        var moved = shift * was;
        dc->SetTransform(&moved);
        try
        {
            field.Paint(dc, gfx, QuickTheme.For(theme), look);
        }
        finally
        {
            dc->SetTransform(&was);
        }
    }

    /// <summary>§521: how a text or a number box being typed in is drawn: its box's size, Body's words, its placeholder.</summary>
    internal static FieldLook BoxLook(PanelLayout layout, RectF box, string placeholder) =>
        new(new Vector2(box.Width, box.Height), layout.Scale, layout.P(PanelMetrics.Body), placeholder, Mirror.On, Search: false);

    /// <summary>§575: how the add list's own search is drawn: a search box, across its flyout.</summary>
    internal static FieldLook FilterLook(PanelLayout layout) =>
        new(new Vector2(layout.Filter.Width, layout.Filter.Height), layout.Scale, layout.P(PanelMetrics.Body), S.DockSearchWidgets, Mirror.On, Search: true);

    // --- The page -----------------------------------------------------------------------------------

    private void Page()
    {
        if (layout.Shown is not { } page)
        {
            return;
        }
        if (page.Header is { } header && !layout.HeaderCard.IsEmpty)
        {
            Header(header);
        }
        else
        {
            // §575: no title: its entry on the bar names the page; its buttons where it has some.
            foreach (var (item, at) in layout.Actions)
            {
                var r = layout.OnPanel(at);
                var hit = new PanelHit(PanelHitKind.TitleAct, item.Id, Key: item.Text);
                Button(r, hit, !item.Enabled);
                GlyphAndWords(r, item.Icon is FlyoutIcon.Glyph g ? g.Code : "", item.Text, WordsOf(hit, !item.Enabled));
            }
        }
        foreach (var block in layout.Blocks)
        {
            // §551: a card a row dragged into order passes over drawn where it makes room; the dragged one's own lifted, last.
            var shift = ShiftOf(block.Lines.Count > 0 ? block.Lines[0] : null);
            if (float.IsNaN(shift))
            {
                continue;
            }
            var r = layout.OnPanel(block.Rect);
            r = r with { Y = r.Y + shift };
            if (r.Bottom < layout.Viewport.Y || r.Y > layout.Viewport.Bottom)
            {
                continue;
            }
            if (block.Row.Kind == PanelRowKind.Heading)
            {
                Text(block.Row.Text, r.X, r.Y + (P(PanelMetrics.Heading) / 2), PanelMetrics.Body, theme.Text, Weight.Semibold, most: r.Width);
                // §553: its caption under it, as a row's description is, §604 every line of it.
                if (block.Row.Detail is { } caption)
                {
                    _ = PopupPaint.Wrapped(dc, gfx, caption, P(PanelMetrics.Caption), Weight.Regular, theme.TextSecondary, r.X, r.Y + P(PanelMetrics.Heading), r.Width, int.MaxValue);
                }
                continue;
            }
            // §592: a tab strip on the page's ground, no card.
            if (block.Row.Kind == PanelRowKind.Tabs && block.Lines is [var strip])
            {
                Tabs(strip, r);
                continue;
            }
            // §578: a row that only acts its button alone on the page's ground, no card.
            if (block.Lines is [{ Row.Foot: true, Inner: false } foot])
            {
                One(foot, r);
                continue;
            }
            Card(block, r);
        }
        if (marks.Drag is { } drag)
        {
            Lifted(drag);
        }
    }

    /// <summary>
    /// §551: how far <paramref name="line"/> is drawn from where it stands while a row is dragged into order: one of its list
    /// the dragged one passes over by its room, the rest not at all; NaN for the dragged one's own lines (drawn lifted).
    /// </summary>
    private float ShiftOf(PanelLine? line)
    {
        if (marks.Drag is not { } drag || line is null)
        {
            return 0;
        }
        var at = drag.Of(line.Rect.Y);
        return at < 0 ? 0 : at == drag.From ? float.NaN : drag.Shift(at);
    }

    /// <summary>
    /// §551: the row dragged into order, lifted where the pointer has it: its lines (with what is shown under it) on a card of
    /// their own, raised as WinUI's ListView raises an item it drags — a soft shadow under it, its elevation's rim.
    /// </summary>
    private void Lifted(PanelDrag drag)
    {
        var span = layout.OnPanel(drag.Spans[drag.From]);
        span = span with { Y = span.Y + drag.Lift };
        for (var step = 3; step >= 1; step--)
        {
            var spread = P(2 * step);
            Fill(new RectF(span.X - spread, span.Y - spread + P(4), span.Width + (2 * spread), span.Height + (2 * spread)), P(PanelMetrics.Corner) + spread, Color.FromArgb((byte)(theme.Dark ? 0x22 : 0x12), 0, 0, 0));
        }
        var lines = layout.Blocks.SelectMany(block => block.Lines).Where(line => drag.Of(line.Rect.Y) == drag.From).ToList();
        PopupPaint.Clipped(dc, PopupPaint.Rect(span.X, span.Y, span.Width, span.Height), P(PanelMetrics.Corner), () =>
        {
            // Over the panel's ground first: a card's own fill is see-through, and what it is carried over must not show.
            Fill(span, 0, theme.Ground);
            Fill(span, 0, theme.CardBackground);
            foreach (var line in lines.Skip(1).Where(line => line.Inner))
            {
                var at = layout.OnPanel(line.Rect);
                at = at with { Y = at.Y + drag.Lift };
                Fill(at, 0, theme.CardBackgroundSecondary);
                Fill(at, 0, theme.CardBackground);
            }
        });
        Elevated(span, P(PanelMetrics.Corner));
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var at = layout.OnPanel(line.Rect);
            at = at with { Y = at.Y + drag.Lift };
            if (index > 0)
            {
                PopupPaint.Divider(dc, at.X, at.Right, at.Y, Hair, Ink(theme.CardStroke));
            }
            One(line, at);
        }
    }

    /// <summary>
    /// §529: the header card: a card as every card, its tile (ControlAltFill, 4 DIP corners, its rim) with the icon on it —
    /// several side by side on it, the first the text's ink and the others quieter —, its name in Subtitle over where it stands in
    /// Caption, the page's buttons as a command bar's icon buttons (no fill nor rim until the pointer is on one).
    /// </summary>
    private void Header(PanelHeader header)
    {
        var card = layout.OnPanel(layout.HeaderCard);
        Fill(card, P(PanelMetrics.Corner), theme.CardBackground);
        Rim(card, P(PanelMetrics.Corner), theme.CardStroke);
        var tile = layout.OnPanel(layout.HeaderTile);
        var side = P(PanelMetrics.HeaderTile);
        var shown = Math.Min(header.Glyphs.Count, PanelMetrics.FannedMost);
        // One tile, each icon on it side by side, a step apart; the first the text's ink, the others quieter.
        Fill(tile, P(PanelMetrics.Corner), theme.ControlAltFill);
        Rim(tile, P(PanelMetrics.Corner), theme.ControlStroke);
        for (var at = 0; at < shown; at++)
        {
            Glyph(header.Glyphs[at], tile.X + (side / 2) + (at * P(PanelMetrics.FanStep)), tile.Y + (side / 2), shown > 1 ? PanelMetrics.FannedGlyph : PanelMetrics.HeaderGlyph, at == 0 ? theme.Text : theme.TextSecondary);
        }
        var title = layout.OnPanel(layout.Title);
        Text(header.Name, title.X, title.Y + (title.Height / 2), PanelMetrics.Subtitle, theme.Text, Weight.Semibold, most: title.Width, family: TitleFamily);
        var place = layout.OnPanel(layout.Place);
        Text(header.Place, place.X, place.Y + (place.Height / 2), PanelMetrics.Caption, theme.TextSecondary, most: place.Width);
        foreach (var (item, at) in layout.Actions)
        {
            var r = layout.OnPanel(at);
            var hit = new PanelHit(PanelHitKind.TitleAct, item.Id, Key: item.Text);
            Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
            if (Focused(hit))
            {
                FocusRing(r, P(PanelMetrics.Corner));
            }
            Glyph(item.Icon is FlyoutIcon.Glyph g ? g.Code : "", r.X + (r.Width / 2), r.Y + (r.Height / 2), PanelMetrics.Icon, WordsOf(hit, !item.Enabled));
        }
    }

    /// <summary>A button (WinUI's Button): 32 tall, its fill by its state (<see cref="ControlOf"/>), its elevation rim.</summary>
    private void Button(RectF r, PanelHit hit, bool greyed = false)
    {
        Fill(r, P(PanelMetrics.Corner), ControlOf(hit, greyed));
        Elevated(r, P(PanelMetrics.Corner), flat: greyed || IsPressed(hit));
        if (Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner));
        }
    }

    /// <summary>
    /// §521: a row's button <paramref name="row"/> acts by, in <paramref name="r"/>: its words in its middle; §610 one too narrow
    /// for them (beside a dropdown, short of room) its icon alone, its words its tooltip.
    /// </summary>
    private void Act(RectF r, PanelHit hit, PanelRow row, bool greyed)
    {
        // §613: a subtle one (a value's reset beside it) an icon button, as a thing's own actions are.
        if (row.Item.Subtle)
        {
            IconButton(r, hit, row.Glyph, greyed);
            return;
        }
        Button(r, hit, greyed);
        var mid = r.Y + (r.Height / 2);
        if (r.Width <= P(PanelMetrics.Control) && row.Glyph.Length > 0)
        {
            Glyph(row.Glyph, r.X + (r.Width / 2), mid, PanelMetrics.Icon, WordsOf(hit, greyed));
            return;
        }
        var label = P(PanelMetrics.Label);
        var most = r.Width - (2 * label);
        var tw = MathF.Min(Measure(row.Text, PanelMetrics.Body), most);
        Text(row.Text, r.X + MathF.Round((r.Width - tw) / 2), mid, PanelMetrics.Body, WordsOf(hit, greyed), most: most);
    }

    /// <summary>The one thing a line is, where it is one (an expander's head, a link; not words): what lights it whole; none otherwise.</summary>
    private PanelHit WholeOf(PanelLine line) =>
        layout.Controls(line) is [{ Hit.Kind: PanelHitKind.Head or PanelHitKind.Link or PanelHitKind.Act } whole] && !line.Row.Greyed && line.Row.Kind is not (PanelRowKind.Words or PanelRowKind.Action or PanelRowKind.PickerBody) ? whole.Hit : PanelHit.None;

    /// <summary>
    /// §544: a line's own fill as the Community Toolkit's SettingsCard has it — CardBackgroundFillColorDefault, a clickable
    /// card's (a link, an expander's head) ControlFillColorSecondary under the pointer and Tertiary pressed, a disabled card's
    /// ControlFillColorDisabled.
    /// </summary>
    private Color LineFill(PanelLine line)
    {
        var whole = WholeOf(line);
        return !whole.IsNone && marks.Hot == whole ? (IsPressed(whole) ? theme.ControlFillPressed : theme.ControlFillHover)
            : line.Row.Greyed && line.Row.Kind != PanelRowKind.Preview ? theme.ControlFillDisabled
            : theme.CardBackground;
    }

    /// <summary>
    /// A card (a SettingsCard): 1 DIP rim, 4 DIP corners, its icon at 16, its name at 48, its control at its end; an open
    /// expander one card (a SettingsExpander), its rows inset under its head, each apart by a 1 DIP line. §544: each line its
    /// own fill (<see cref="LineFill"/>), an expander's rows over ExpanderContentBackground (CardBackgroundFillColorSecondary)
    /// as the Toolkit lays its items on the Expander's content; the rows apart by CardStrokeColorDefault (an item's top
    /// border, SettingsCardBorderBrush); the rim CardStrokeColorDefault, a lone clickable card's ControlElevationBorderBrush
    /// under the pointer and ControlStrokeColorDefault pressed or disabled.
    /// </summary>
    private void Card(PanelBlock block, RectF r)
    {
        // §671: a bare preview its sample's room alone, no card round it.
        if (block.Lines is [{ Row.Bare: true } bare])
        {
            Preview(bare, layout.OnPanel(bare.Rect));
            return;
        }
        // §551: each line where it makes room for a row dragged into order (a whole card moved with its first line); none of
        // the dragged one's own, drawn lifted.
        var shown = block.Lines.Where(line => !float.IsNaN(ShiftOf(line))).ToList();
        RectF At(PanelLine line)
        {
            var at = layout.OnPanel(line.Rect);
            return at with { Y = at.Y + ShiftOf(line) };
        }
        PopupPaint.Clipped(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), P(PanelMetrics.Corner), () =>
        {
            // §578: an add page's folder's tiles on its rows' ground, the space round them too.
            if (shown.Count > 1 && shown.Any(line => line.Tile))
            {
                var head = At(shown[0]);
                Fill(new RectF(r.X, head.Bottom, r.Width, r.Bottom - head.Bottom), 0, theme.CardBackgroundSecondary);
            }
            foreach (var line in shown)
            {
                var at = At(line);
                if (line is { Inner: true, Tile: true })
                {
                    continue;
                }
                if (line.Inner)
                {
                    Fill(at, 0, theme.CardBackgroundSecondary);
                }
                Fill(at, 0, LineFill(line));
            }
        });
        // §578: each tile in a folder's card a card of its own: its fill, its rim (its elevation's under the pointer).
        foreach (var line in shown.Where(line => line is { Inner: true, Tile: true }))
        {
            var at = At(line);
            Fill(at, P(PanelMetrics.Corner), LineFill(line));
            var whole = WholeOf(line);
            if (!whole.IsNone && marks.Hot == whole)
            {
                Elevated(at, P(PanelMetrics.Corner), flat: IsPressed(whole));
            }
            else
            {
                Rim(at, P(PanelMetrics.Corner), line.Row.Greyed ? theme.ControlStroke : theme.CardStroke);
            }
        }
        if (block.Lines is [var only] && WholeOf(only) is { IsNone: false } lone && marks.Hot == lone)
        {
            Elevated(r, P(PanelMetrics.Corner), flat: IsPressed(lone));
        }
        else if (block.Lines is [{ Row.Greyed: true }])
        {
            Rim(r, P(PanelMetrics.Corner), theme.ControlStroke);
        }
        else
        {
            Rim(r, P(PanelMetrics.Corner), theme.CardStroke);
        }
        foreach (var line in shown)
        {
            var at = At(line);
            // §670: an inline picker's choices under its search, no rule between: one control.
            if (line is { Inner: true, Tile: false } && !(line.Row.Foot && block.Lines.Any(each => each.Tile))
                && !(line.Row.Kind == PanelRowKind.PickerBody && block.Lines.Any(each => each.Row.Key == PanelPickers.SearchOf(line.Row.Key))))
            {
                PopupPaint.Divider(dc, at.X, at.Right, at.Y, Hair, Ink(theme.CardStroke));
            }
            if (WholeOf(line) is { IsNone: false } whole && Focused(whole))
            {
                FocusRing(at, P(PanelMetrics.Corner));
            }
            if (marks.Lit == line.Row.Key)
            {
                // A search's pick, lit a moment where it is (§520 decision 8).
                var accent = theme.Accent;
                PopupPaint.Fill(dc, PopupPaint.Rect(at.X + Hair, at.Y + Hair, at.Width - (2 * Hair), at.Height - (2 * Hair)), P(PanelMetrics.Corner), Ink(Color.FromArgb(0x30, accent.R, accent.G, accent.B)));
                PopupPaint.Stroke(dc, PopupPaint.Rect(at.X, at.Y, at.Width, at.Height), P(PanelMetrics.Corner), P(2), Ink(accent));
            }
            One(line, at);
        }
    }

    /// <summary>
    /// A row's name — and its icon — in TextFillColorPrimary, a greyed row's TextFillColorDisabled; §574 every value alike,
    /// nothing muted for where it comes from.
    /// </summary>
    internal static DXGI_RGBA NameInk(PanelRow row, FlyoutTheme theme) => row.Greyed ? theme.TextDisabled : theme.Text;

    private void One(PanelLine line, RectF at)
    {
        var row = line.Row;
        if (row.Kind == PanelRowKind.Preview)
        {
            Preview(line, at);
            return;
        }
        // §628: a tile's picture over its words: its room's ground and rim, the sample over it (LookPreviews); its words in the
        // line under it, as a tile of words alone has them.
        if (layout.PictureOf(line) is { IsEmpty: false } picture)
        {
            var dy = at.Y - line.Rect.Y;
            var pictured = picture with { X = picture.X + at.X - line.Rect.X, Y = picture.Y + dy };
            Fill(pictured, P(LookPreview.CornerDip), LookPreview.Ground(theme.Dark));
            Rim(pictured, P(LookPreview.CornerDip), theme.CardStroke);
            line = layout.WordsOf(line);
            at = at with { Y = line.Rect.Y + dy, Height = line.Rect.Height };
        }
        var mid = at.Y + (at.Height / 2);
        var ink = NameInk(row, theme);
        var weight = row.Kind is PanelRowKind.Group or PanelRowKind.Heading ? Weight.Semibold : Weight.Regular;
        // §670: an inline picker's cells.
        if (row.Kind == PanelRowKind.PickerBody)
        {
            Picker(line, at);
            return;
        }
        // §578: a row that only acts its button alone, its glyph on it.
        if (row.Foot)
        {
            Foot(line, at);
            return;
        }
        Control(line, at, mid);
        // §670: a picker's search its box alone, across its column.
        if (PanelPickers.Searches(row))
        {
            return;
        }
        // §578: a tile its widget's icon, in its card's start. §644: a line of a card in its icon's column only its own colour
        // (an account's line in Show from: its inbox's), never a glyph.
        // §685: a group's head in a card (an account's Calendars, Mail) its glyph too.
        if (!line.Inner || line.Tile || row is { Kind: PanelRowKind.Note, Item.Icon: FlyoutTile } || row.Kind == PanelRowKind.Heading)
        {
            Icon(row.Item.Icon is FlyoutTile && row.Kind == PanelRowKind.Color ? null : row.Item.Icon, layout.IconX(line) - line.Rect.X + at.X, mid, ink);
        }
        var nx = layout.NameX(line) - line.Rect.X + at.X;
        var room = layout.NameRoom(line);
        if (row.Kind == PanelRowKind.Words)
        {
            // §529: its words in Caption, the link at their end in the accent, cut short before it.
            var link = layout.Controls(line) is [var only] ? only : default;
            var r = layout.OnPanel(link.Rect);
            Text(row.Detail ?? "", nx, mid, PanelMetrics.Caption, theme.TextSecondary, most: r.X - P(PanelMetrics.Gap) - nx);
            // §544: a HyperlinkButton's words: AccentTextFillColorPrimary, Secondary under the pointer, Tertiary pressed.
            var lit = IsPressed(link.Hit) ? theme.AccentTextPressed : marks.Hot == link.Hit ? theme.AccentTextHover : theme.AccentText;
            Text(row.Text, r.X, mid, PanelMetrics.Caption, Ink(lit), most: r.Width + 1);
            if (Focused(link.Hit))
            {
                FocusRing(r, P(2));
            }
            return;
        }
        if (row.Kind == PanelRowKind.Action || row.Adds)
        {
            // §521: a row that acts says what it does before its button, the button its name (WinUI's card and Button); §548
            // those words the card's header, in the header's ink as every card's (secondary read as greyed).
            Text(row.Detail ?? "", nx, mid, PanelMetrics.Body, ink, most: room);
        }
        else if (row.Tall)
        {
            // Body over Caption in the card's middle (12 from a card's top and foot, §578 8 from a tile's), §529 a group's head
            // its name in Body Strong over its sentence; §604 the second line every line of it, wrapped where the layout
            // measured it (PanelLayout.WordsAt), the line as tall as they are.
            var (name, second) = layout.WordsAt(line);
            var dy = at.Y - line.Rect.Y;
            Name(row, nx, name.Y + dy + (name.Height / 2), ink, weight, room);
            _ = PopupPaint.Wrapped(dc, gfx, row.Second!, P(PanelMetrics.Caption), Weight.Regular, theme.TextSecondary, nx, second.Y + dy, room, int.MaxValue);
        }
        else
        {
            Name(row, nx, mid, ink, weight, room);
        }
    }

    /// <summary>A row's name, cut at <paramref name="room"/>; §595 its badge after it, the name cut short for it.</summary>
    private void Name(PanelRow row, float x, float mid, DXGI_RGBA ink, Weight weight, float room)
    {
        if (row.Badge is not { } badge)
        {
            Text(row.Text, x, mid, PanelMetrics.Body, ink, weight, most: room);
            return;
        }
        var wide = MathF.Min(Measure(badge, PanelMetrics.Caption) + (2 * P(PanelMetrics.BadgeInset)), room / 2);
        var said = Text(row.Text, x, mid, PanelMetrics.Body, ink, weight, most: room - P(PanelMetrics.Gap) - wide);
        Badge(badge, new RectF(x + said + P(PanelMetrics.Gap), mid - (P(PanelMetrics.BadgeTall) / 2), wide, P(PanelMetrics.BadgeTall)), row.Greyed);
    }

    /// <summary>
    /// §595: a badge (the default layout's "Default"), WinUI's InfoBadge as a word: a pill of AccentFillColorDefault, its word in
    /// Caption, TextOnAccentFillColorPrimary; greyed AccentFillColorDisabled under TextOnAccentFillColorDisabled.
    /// </summary>
    private void Badge(string word, RectF r, bool greyed)
    {
        Fill(r, r.Height / 2, greyed ? theme.AccentDisabled : theme.Accent);
        var inset = P(PanelMetrics.BadgeInset);
        Text(word, r.X + inset, r.Y + (r.Height / 2), PanelMetrics.Caption, greyed ? theme.TextOnAccentDisabled : theme.TextOnAccent, most: r.Width - (2 * inset) + 1);
    }

    /// <summary>
    /// §578: a row that only acts (PanelRow.Foot) as Windows' Settings draws a command under a list, a hyperlink button's: no
    /// fill nor rim until the pointer is on it (SubtleFillColorSecondary, Tertiary pressed), its glyph and its words in
    /// AccentTextFillColorPrimary (Secondary under the pointer, Tertiary pressed; TextFillColorDisabled greyed), a way to add
    /// one's chevron after them.
    /// </summary>
    private void Foot(PanelLine line, RectF at)
    {
        var row = line.Row;
        var (hit, cell) = layout.Controls(line)[0];
        var r = cell with { X = cell.X + at.X - line.Rect.X, Y = cell.Y + at.Y - line.Rect.Y };
        var mid = r.Y + (r.Height / 2);
        var greyed = row.Greyed;
        if (!greyed)
        {
            Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
        }
        if (Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner));
        }
        var ink = greyed ? theme.TextDisabled : Ink(IsPressed(hit) ? theme.AccentTextPressed : marks.Hot == hit ? theme.AccentTextHover : theme.AccentText);
        var x = r.X + P(PanelMetrics.Label);
        if (PanelLayout.FootGlyph(row) is { Length: > 0 } glyph)
        {
            Glyph(glyph, x + (P(PanelMetrics.Icon) / 2), mid, PanelMetrics.Icon, ink);
            x += P(PanelMetrics.Icon) + P(PanelMetrics.Gap);
        }
        var trail = row.Adds ? P(PanelMetrics.Gap) + P(PanelMetrics.ChevronIn) + (P(PanelMetrics.Chevron) / 2) : P(PanelMetrics.Label);
        Text(row.Text, x, mid, PanelMetrics.Body, ink, most: r.Right - trail - x);
        if (row.Adds)
        {
            // §628: one leading to a page of its own (Add a layout) its chevron pointing on, as a row leading to a page does.
            Chevron(row.Kind != PanelRowKind.Link ? PanelGlyphs.Down : Mirror.On ? PanelGlyphs.Left : PanelGlyphs.Right, r.Right, mid, ink);
        }
    }

    /// <summary>
    /// §592: a tab strip as WinUI's SelectorBar draws its items: no fill nor rim until the pointer is on one
    /// (SubtleFillColorSecondary, Tertiary pressed), its words in Body — the chosen one's in Body Strong and
    /// TextFillColorPrimary with the accent pill (16 × 3, fully round) at its foot, the others' TextFillColorSecondary
    /// (Primary under the pointer) —, cut short where the strip narrows them.
    /// </summary>
    private void Tabs(PanelLine line, RectF at)
    {
        var chosen = layout.Chosen(line.Row);
        var tabs = layout.TabsAt(line);
        for (var index = 0; index < tabs.Count; index++)
        {
            var (tab, cell, _) = tabs[index];
            var r = cell with { X = cell.X + at.X - line.Rect.X, Y = cell.Y + at.Y - line.Rect.Y };
            var hit = new PanelHit(PanelHitKind.Tab, 0, index, Key: tab.Key);
            var on = index == chosen;
            Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
            if (Focused(hit))
            {
                FocusRing(r, P(PanelMetrics.Corner));
            }
            var ink = on || marks.Hot == hit ? (IsPressed(hit) ? theme.TextSecondary : theme.Text) : theme.TextSecondary;
            var most = r.Width - (2 * P(PanelMetrics.Label));
            var words = MathF.Min(Measure(tab.Text, PanelMetrics.Body, on ? Weight.Semibold : Weight.Regular), most);
            var mid = r.Y + (r.Height / 2);
            Text(tab.Text, r.X + MathF.Floor((r.Width - words) / 2), mid, PanelMetrics.Body, ink, on ? Weight.Semibold : Weight.Regular, most: most + 1);
            if (on)
            {
                var w = P(PanelMetrics.PillTall);
                var h = P(PanelMetrics.PillWide);
                Fill(new RectF(r.X + MathF.Round((r.Width - w) / 2), r.Bottom - h, w, h), h / 2, theme.Accent);
            }
        }
        // §673: its button (the palette's reset) at its right end, greyed by its own row.
        if (line.Row.Button is { } beside && layout.Controls(line).FirstOrDefault(each => each.Hit.Key == beside.Key) is { Hit.IsNone: false } reset)
        {
            Act(reset.Rect with { X = reset.Rect.X + at.X - line.Rect.X, Y = reset.Rect.Y + at.Y - line.Rect.Y }, reset.Hit, beside, beside.Greyed);
        }
    }

    /// <summary>
    /// §536: a look's preview: "Preview" in Caption over its sample's room, that room's ground and rim. The sample itself is
    /// Karpo's own drawing, in visuals of its own over this surface where the room stands (LookPreviews, EditDeck), drawn
    /// once a look and only moved as the page scrolls.
    /// </summary>
    private void Preview(PanelLine line, RectF at)
    {
        if (!line.Row.Bare)
        {
            Text(line.Row.Text, at.X + P(PanelMetrics.Gutter), at.Y + P(LookPreview.CaptionMidDip), PanelMetrics.Caption, theme.TextSecondary, most: at.Width - (2 * P(PanelMetrics.Gutter)));
        }
        var box = layout.PreviewBox(line);
        box = box with { X = box.X + at.X - line.Rect.X, Y = box.Y + at.Y - line.Rect.Y };
        Fill(box, P(LookPreview.CornerDip), LookPreview.Ground(theme.Dark));
        Rim(box, P(LookPreview.CornerDip), theme.CardStroke);
    }

    // --- Controls -----------------------------------------------------------------------------------

    private void Control(PanelLine line, RectF at, float mid)
    {
        var row = line.Row;
        var dx = at.X - line.Rect.X;
        var dy = at.Y - line.Rect.Y;
        RectF Moved(RectF r) => r with { X = r.X + dx, Y = r.Y + dy };
        // §529: a mixed row's widgets' own values' button stands first, before the control: the control's own cells are the
        // rest, and what is said before the control is said before that button.
        var all = layout.Controls(line);
        // §610, §613: a button standing beside the control (the palette's reset, a value's) drawn first, greyed by its own row;
        // the control's own cells are the rest.
        var beside = row.Button;
        if (beside is not null && all.FirstOrDefault(each => each.Hit.Key == beside.Key) is { Hit.IsNone: false } reset)
        {
            Act(Moved(reset.Rect), reset.Hit, beside, beside.Greyed);
        }
        var controls = all.Where(each => each.Hit.Kind is not (PanelHitKind.Each or PanelHitKind.Handle) && each.Hit.Key != beside?.Key).ToList();
        var lead = all.Where(each => each.Hit.Kind is PanelHitKind.Each).Select(each => Moved(each.Rect).X).DefaultIfEmpty(float.MaxValue).Min();
        // Where the layout puts them, moved as the page is scrolled: the controls' end, the value's one box.
        var right = layout.ControlEnd(line) + dx;
        var box = Moved(layout.ValueBox(line));
        var wide = box.Width;
        // §548: what is said before a control stands 12 off it (WinUI: between a control and its label).
        var label = P(PanelMetrics.Label);
        var greyed = row.Greyed;
        var words = greyed ? theme.TextDisabled : theme.Text;
        switch (row.Kind)
        {
            // §586: an add page's tile how many of its widget the layout has, at its end in Caption (TextFillColorSecondary,
            // as an expander's value); nothing for none.
            case PanelRowKind.Pick when line.Tile && row.Value is { } placed:
                TextBefore(placed, right, mid, PanelMetrics.Caption, greyed ? theme.TextDisabled : theme.TextSecondary, P(PanelMetrics.ControlWide));
                break;
            case PanelRowKind.Toggle:
                Switch(controls[0].Hit, right, mid, row.On, greyed, row.Mixed);
                // §644 what it says before its "On"/"Off" (a Show from folder's "Not synced", "Syncing…"), §646 what it
                // holds (a mail folder's counts), as the menu's row.
                if (row.Value is { } says)
                {
                    TextBefore(says, layout.SummaryRight(right), mid, PanelMetrics.Caption, greyed ? theme.TextDisabled : theme.TextSecondary, P(PanelMetrics.ControlWide));
                }
                break;
            // §521: a way to add one a button, its words and a chevron, its list under it.
            case PanelRowKind.Choice when row.Adds && controls.Count > 0:
            {
                var (hit, cell) = controls[0];
                var c = Moved(cell);
                Button(c, hit, greyed);
                // Its words 12 in, its chevron 8 after them, 20 in from its end as every chevron.
                Text(row.Text, c.X + label, mid, PanelMetrics.Body, WordsOf(hit, greyed), most: c.Width - label - P(PanelMetrics.Gap) - P(PanelMetrics.ChevronIn) - (P(PanelMetrics.Chevron) / 2));
                Chevron(PanelGlyphs.Down, c.Right, mid, greyed ? theme.TextDisabled : theme.TextSecondary);
                break;
            }
            // §638: its dropdown where it stands, a choice standing beside it (a font's weight) its own after it.
            case PanelRowKind.Choice:
                foreach (var (hit, cell) in controls.Where(each => each.Hit.Kind == PanelHitKind.Pop))
                {
                    Dropdown(row.Own(hit.Key), Moved(cell), hit, mid);
                }
                break;
            // §521: a text box, typed in place: its words, or its placeholder quieter; being typed in, Karpo's text box there.
            case PanelRowKind.Text:
            {
                if (editing is { } typing && typing.Key == row.Key)
                {
                    Field(typing.Field, box, BoxLook(layout, box, row.Typed?.Placeholder ?? ""));
                    break;
                }
                var hit = controls.Count > 0 ? controls[0].Hit : PanelHit.None;
                Box(box, hit, greyed, text: true);
                // §670: a picker's search what was typed in it, its magnifier at its end (WinUI's AutoSuggestBox).
                var said = PanelPickers.Searches(row) ? layout.SearchText(row.Key) is { Length: > 0 } typed ? typed : null : row.Value;
                if (PanelPickers.Searches(row))
                {
                    Glyph(PanelGlyphs.Search, box.Right - P(PanelMetrics.ChevronIn), mid, PanelMetrics.Chevron, theme.TextSecondary);
                }
                Text(said ?? row.Typed?.Placeholder ?? "", box.X + label, mid, PanelMetrics.Body, said is null || greyed ? theme.TextSecondary : words, most: box.Width - (2 * label) - (PanelPickers.Searches(row) ? P(PanelMetrics.ChevronIn) : 0));
                break;
            }

            // §535: a colour button showing the colour in force, what it is set to ("Default (Blue)", "Automatic") before it.
            case PanelRowKind.Color when row.Colour is { } colour:
            {
                var (hit, cell) = controls[0];
                var c = Moved(cell);
                ColourButton(c, hit, colour, greyed);
                if (row.Value is { } said)
                {
                    // §529: before its mixed widgets' button where it has one, §613 before a button beside it, inside the value
                    // column all the same.
                    var end = MathF.Min(MathF.Min(c.X, lead), beside is null ? float.MaxValue : Moved(all.First(each => each.Hit.Key == beside.Key).Rect).X) - label;
                    TextBefore(said, end, mid, PanelMetrics.Caption, greyed ? theme.TextDisabled : theme.TextSecondary, end - box.X);
                }
                break;
            }
            case PanelRowKind.Stepper when row.Stepper is { } stepper:
            {
                // A NumberBox with its spin buttons inline: the value (§521: typed in place), then − and + in 32 DIP cells.
                var number = controls.FirstOrDefault(each => each.Hit.Kind == PanelHitKind.Number);
                Box(box, number.Hit, greyed, text: true);
                if (editing is { } typing && typing.Key == row.Key && !number.Hit.IsNone)
                {
                    Field(typing.Field, Moved(number.Rect), BoxLook(layout, Moved(number.Rect), ""));
                }
                else
                {
                    // §528: several widgets' numbers apart: "Mixed", quieter, as a box's placeholder. Its words 12 in, cut 12
                    // before − and +.
                    var spin = controls.Where(each => each.Hit.Kind == PanelHitKind.Fewer).Select(each => Moved(each.Rect).X).DefaultIfEmpty(box.Right).First();
                    Text(stepper.Value, box.X + label, mid, PanelMetrics.Body, row.Mixed && !greyed ? theme.TextSecondary : words, most: spin - label - (box.X + label));
                }
                var inset = P(PanelMetrics.SegmentInset);
                foreach (var (hit, cell) in controls.Where(each => each.Hit.Kind is PanelHitKind.Fewer or PanelHitKind.More))
                {
                    var c = Moved(cell);
                    var can = !greyed && (hit.Kind == PanelHitKind.Fewer ? stepper.CanFewer : stepper.CanMore);
                    if (can)
                    {
                        Fill(new RectF(c.X + inset, c.Y + inset, c.Width - (2 * inset), c.Height - (2 * inset)), P(PanelMetrics.InnerCorner), SubtleOf(hit));
                    }
                    Glyph(hit.Kind == PanelHitKind.Fewer ? PanelGlyphs.Minus : PanelGlyphs.Add, c.X + (c.Width / 2), mid, PanelMetrics.Chevron, can ? theme.Text : theme.TextDisabled);
                    if (Focused(hit))
                    {
                        FocusRing(c, P(PanelMetrics.Corner));
                    }
                }
                break;
            }
            case PanelRowKind.Item:
            {
                // §535: its switch at its end (an account's calendar), its buttons before it.
                if (row.Switches && controls.FirstOrDefault(each => each.Hit.Kind == PanelHitKind.Toggle) is { Hit.IsNone: false } toggle)
                {
                    Switch(toggle.Hit, right, mid, row.On, greyed);
                }
                var start = Buttons(right);
                // §609: its one choice's dropdown before its buttons (a holiday calendar's region).
                if (row.Pair is { } choice && controls.FirstOrDefault(each => each.Hit.Kind == PanelHitKind.Pop) is { Hit.IsNone: false } pop)
                {
                    Dropdown(choice, Moved(pop.Rect), pop.Hit, mid);
                    start = Moved(pop.Rect).X;
                }
                if (row.Value is { } chosen && row.Detail is null)
                {
                    TextBefore(chosen, start - label, mid, PanelMetrics.Caption, theme.TextSecondary, P(PanelMetrics.ControlWide));
                }
                break;
            }
            // §529: a group's head its chevron alone: its sentence says what it holds.
            case PanelRowKind.Group:
                // §544: ExpanderChevronForeground, TextFillColorPrimary. §552: none for a face with nothing to set, §583 nor
                // for a fixed group (an Appearance part's); a face's Remove before it.
                if (row.Folds)
                {
                    Chevron(layout.StandsOpen(row) ? PanelGlyphs.Up : PanelGlyphs.Down, right, mid, theme.Text);
                }
                _ = Buttons(right);
                break;
            case PanelRowKind.Picker:
                PickerHead(line, right, mid, right - P(PanelMetrics.ChevronRoom), greyed);
                break;
            case PanelRowKind.Expander:
            {
                var open = layout.StandsOpen(row);
                // §544: ExpanderChevronForeground, TextFillColorPrimary.
                Chevron(open ? PanelGlyphs.Up : PanelGlyphs.Down, right, mid, greyed ? theme.TextDisabled : theme.Text);
                // §521: a thing's buttons before its chevron (a layout's Delete).
                var start = Buttons(right - P(PanelMetrics.ChevronRoom));
                // Shut, what it is set to or the accounts it holds; nothing while open: its rows say it.
                if (!open && row.Value is { } summary)
                {
                    TextBefore(summary, start - label, mid, PanelMetrics.Caption, theme.TextSecondary, P(PanelMetrics.ControlWide));
                }
                break;
            }
            case PanelRowKind.Link:
                // §544: a clickable SettingsCard's action icon, in the card's foreground (Primary, Secondary pressed).
                Chevron(Mirror.On ? PanelGlyphs.Left : PanelGlyphs.Right, right, mid, controls.Count > 0 ? WordsOf(controls[0].Hit, greyed) : theme.Text);
                TextBefore(row.Value ?? "", right - P(PanelMetrics.ChevronRoom) - label, mid, PanelMetrics.Caption, theme.TextSecondary, P(PanelMetrics.ControlWide));
                break;
            // §521: a row that acts a button at its end, its words the row's name.
            case PanelRowKind.Action when controls.Count > 0:
            {
                var (hit, cell) = controls[0];
                var c = Moved(cell);
                Act(c, hit, row, greyed);
                if (row.Value is { } value)
                {
                    TextBefore(value, c.X - label, mid, PanelMetrics.Caption, theme.TextSecondary, P(PanelMetrics.ControlWide));
                }
                break;
            }
            case PanelRowKind.Note when row.Value is { } said:
                TextBefore(said, right, mid, PanelMetrics.Caption, theme.TextSecondary, P(PanelMetrics.ControlWide));
                break;
        }

        // §551: a row put in order its handle at its start: a grip's six dots (the menus' own, PopupPaint.Gripper), quieter
        // until the pointer is on its row; the focus ring round it while the keys are on it.
        foreach (var (hit, cell) in all.Where(each => each.Hit.Kind == PanelHitKind.Handle))
        {
            var c = Moved(cell);
            var lit = marks.Hot.Key == row.Key || marks.Pressed == hit;
            PopupPaint.Gripper(dc, PopupPaint.Rect(c.X, c.Y, c.Width, c.Height), layout.Scale, greyed ? theme.TextDisabled : lit ? theme.Text : theme.TextSecondary);
            if (Focused(hit))
            {
                FocusRing(c, P(PanelMetrics.Corner));
            }
        }

        // §529: its widgets' own values' chevron: a command bar's icon button before its control.
        foreach (var (hit, cell) in all.Where(each => each.Hit.Kind is PanelHitKind.Each))
        {
            var c = Moved(cell);
            Fill(c, P(PanelMetrics.Corner), SubtleOf(hit));
            var glyph = layout.Input.Open?.Contains(PanelGroups.EachOf(row.Key)) == true ? PanelGlyphs.Up : PanelGlyphs.Down;
            Glyph(glyph, c.X + (c.Width / 2), mid, PanelMetrics.Chevron, greyed ? theme.TextDisabled : theme.Text);
            if (Focused(hit))
            {
                FocusRing(c, P(PanelMetrics.Corner));
            }
        }

        // A row's buttons, §551 each its icon; where the first starts (<paramref name="none"/> for none).
        float Buttons(float none)
        {
            var start = none;
            foreach (var (hit, cell) in controls.Where(each => each.Hit.Kind == PanelHitKind.Tool))
            {
                var c = Moved(cell);
                var tool = row.Tools[hit.Index];
                var off = !tool.Enabled || greyed;
                // §535: a colour's button shows its colour, no words ("Color" was all it said).
                if (PanelSwatch.Of(tool) is { } colour)
                {
                    ColourButton(c, hit, colour, off);
                }
                else
                {
                    IconButton(c, hit, tool.Glyph, off);
                }
                start = MathF.Min(start, c.X);
            }
            return start;
        }
    }

    /// <summary>
    /// §551: an icon button, a command bar's (WinUI's subtle button: no fill nor rim until the pointer is on it), its glyph alone
    /// in its middle; its name its tooltip and what a screen reader says. A thing's own actions (Remove, Rename), §613 a value's
    /// reset beside it.
    /// </summary>
    private void IconButton(RectF c, PanelHit hit, string glyph, bool off)
    {
        Fill(c, P(PanelMetrics.Corner), off ? Color.FromArgb(0, 0, 0, 0) : SubtleOf(hit));
        if (Focused(hit))
        {
            FocusRing(c, P(PanelMetrics.Corner));
        }
        Glyph(glyph, c.X + (c.Width / 2), c.Y + (c.Height / 2), PanelMetrics.Icon, off ? theme.TextDisabled : IsPressed(hit) ? theme.TextSecondary : theme.Text);
    }

    /// <summary>
    /// A dropdown (a ComboBox) of <paramref name="row"/>'s choice in <paramref name="box"/>, its words' middle at
    /// <paramref name="mid"/>: what it is set to, its picture before it, its mark after it, its chevron; §609 a choice's row's own
    /// and the one a thing's row holds before its buttons (a holiday calendar's region) alike.
    /// </summary>
    private void Dropdown(PanelRow row, RectF box, PanelHit hit, float mid)
    {
        var greyed = row.Greyed;
        Box(box, hit, greyed);
        // ComboBoxForeground: Primary, Secondary pressed, Disabled.
        var valueInk = greyed ? theme.TextDisabled : IsPressed(hit) ? theme.TextSecondary : theme.Text;
        var said = row.Value ?? "";
        // §521: what it is set to as a picture (an icon picker's icon) before its words.
        var from = box.X + P(PanelMetrics.Label);
        if (row.ValueIcon is { } picture)
        {
            Icon(picture, from + (P(PanelMetrics.Icon) / 2), mid, valueInk);
            from += P(PanelMetrics.Icon) + P(PanelMetrics.Gap);
        }
        // §583: the choice's mark after its words, quieter (a palette holding the person's edits: "Changed").
        var room = layout.ValueRoom(row);
        var mark = row.ValueMark;
        var markWide = mark is null ? 0 : MathF.Min(Measure(mark, PanelMetrics.Body), room / 2);
        var saidRoom = mark is null ? room : room - markWide - P(PanelMetrics.Gap);
        // A font's dropdown says its font in it (§520: "a font dropdown"), §556 its family in its weight; §587 cut short in its
        // one box, whole in its tooltip.
        using (gfx.Wearing(row.ValueFace ?? gfx.Typeface))
        {
            Text(said, from, mid, PanelMetrics.Body, valueInk, most: saidRoom);
            if (mark is not null)
            {
                from += MathF.Min(Measure(said, PanelMetrics.Body), saidRoom) + P(PanelMetrics.Gap);
            }
        }
        if (mark is not null)
        {
            Text(mark, from, mid, PanelMetrics.Body, greyed ? theme.TextDisabled : theme.TextSecondary, most: markWide);
        }
        Chevron(PanelGlyphs.Down, box.Right, mid, greyed ? theme.TextDisabled : theme.TextSecondary);
    }

    /// <summary>
    /// A box of the one control width: a dropdown (a ComboBox: <see cref="ControlOf"/>'s fills in ControlElevationBorderBrush),
    /// or §544 <paramref name="text"/> a text box or a number box (a TextBox: ControlFillColorDefault, Secondary under the
    /// pointer, in TextControlElevationBorderBrush, its bottom line ControlStrongStrokeColorDefault).
    /// </summary>
    private void Box(RectF box, PanelHit hit, bool greyed, bool text = false)
    {
        var corner = P(PanelMetrics.Corner);
        if (text)
        {
            Fill(box, corner, greyed ? theme.ControlFillDisabled : !hit.IsNone && marks.Hot == hit ? theme.ControlFillHover : theme.ControlFill);
            TextRim(box, corner, greyed);
        }
        else
        {
            Fill(box, corner, ControlOf(hit, greyed));
            Elevated(box, corner, flat: greyed || IsPressed(hit));
        }
        if (!hit.IsNone && Focused(hit))
        {
            FocusRing(box, corner);
        }
    }

    /// <summary>
    /// A ToggleSwitch ending at <paramref name="right"/>: 40 × 20, "On" / "Off" 12 before it; a toggle row's, §535 a row of
    /// buttons' with its switch (an account's calendar). §528 <paramref name="mixed"/>: several widgets, some on and some
    /// off: its rim, a dash for its knob, "Mixed" before it.
    /// </summary>
    private void Switch(PanelHit hit, float right, float mid, bool on, bool greyed, bool mixed = false)
    {
        var w = P(PanelMetrics.SwitchWide);
        var h = P(PanelMetrics.SwitchTall);
        var sw = new RectF(right - w, mid - (h / 2), w, h);
        // §544: WinUI's ToggleSwitch. Off: ToggleSwitchFillOff — ControlAltFillColorSecondary, Tertiary under the pointer,
        // Quarternary pressed, none disabled — in ControlStrongStrokeColorDefault (Disabled), its knob TextFillColorSecondary
        // (Disabled). On: AccentFillColorDefault, Secondary under the pointer, Tertiary pressed, AccentFillColorDisabled, its
        // knob TextOnAccentFillColorPrimary (Disabled). §528 mixed as off, a dash for its knob.
        var hot = !greyed && marks.Hot == hit;
        var down = !greyed && IsPressed(hit);
        if (on && !mixed)
        {
            Fill(sw, h / 2, greyed ? theme.AccentDisabled : down ? theme.AccentPressed : hot ? theme.AccentHover : theme.Accent);
        }
        else
        {
            if (!greyed)
            {
                Fill(sw, h / 2, down ? theme.ControlAltFillPressed : hot ? theme.ControlAltFillHover : theme.ControlAltFill);
            }
            PopupPaint.Stroke(dc, PopupPaint.Rect(sw.X, sw.Y, sw.Width, sw.Height), h / 2, Hair, Ink(greyed ? theme.ControlStrongStrokeDisabled : theme.ControlStrongStroke));
        }
        if (mixed)
        {
            PopupPaint.Fill(dc, PopupPaint.Rect(sw.X + (sw.Width / 2) - P(6), mid - P(1), P(12), P(2)), P(1), greyed ? theme.TextDisabled : theme.TextSecondary);
            TextBefore(S.SettingsValueMixed, sw.X - P(PanelMetrics.Label), mid, PanelMetrics.Body, greyed ? theme.TextDisabled : theme.Text, P(80));
            if (Focused(hit))
            {
                FocusRing(sw, h / 2);
            }
            return;
        }
        var knob = P(hot ? 14 : 12);
        PopupPaint.Circle(dc, new Vector2(on ? sw.Right - P(4) - (P(12) / 2) : sw.X + P(4) + (P(12) / 2), mid), knob / 2, on ? (greyed ? theme.TextOnAccentDisabled : theme.TextOnAccent) : greyed ? theme.TextDisabled : theme.TextSecondary);
        TextBefore(on ? S.WidgetSwitchOn : S.WidgetSwitchOff, sw.X - P(PanelMetrics.Label), mid, PanelMetrics.Body, greyed ? theme.TextDisabled : theme.Text, P(PanelMetrics.SwitchWords));
        if (Focused(hit))
        {
            FocusRing(sw, h / 2);
        }
    }

    /// <summary>
    /// §535: a colour button (the Community Toolkit's ColorPickerButton): a button whose start is the colour itself, a swatch
    /// 32 × 20 inset 6 (<see cref="Swatch(RectF, float, PanelSwatch)"/>), then its chevron, §548 20 in from its end as every
    /// chevron; its popup the §523 picker.
    /// </summary>
    private void ColourButton(RectF r, PanelHit hit, PanelSwatch colour, bool greyed)
    {
        // §544: its button a Button's (ButtonBackground, ButtonBorderBrush); its chevron DropDownButtonForegroundSecondary —
        // TextFillColorSecondary, Tertiary under the pointer and pressed.
        Fill(r, P(PanelMetrics.Corner), ControlOf(hit, greyed));
        Elevated(r, P(PanelMetrics.Corner), flat: greyed || IsPressed(hit));
        var inset = P(PanelMetrics.ColourInset);
        var start = r.X + inset;
        // §577: the swatch fills the button up to its chevron's room, 32 DIP on a colour button alone.
        var end = r.Right - P(PanelMetrics.ChevronIn) - (P(PanelMetrics.Chevron) / 2) - P(PanelMetrics.Gap);
        var swatch = new RectF(start, r.Y + inset, MathF.Max(P(PanelMetrics.ColourSwatch), end - start), r.Height - (2 * inset));
        Swatch(swatch, P(PanelMetrics.SwatchCorner), colour);
        if (greyed)
        {
            // Greyed: the colour seen through the control's own fill, as WinUI dims a disabled button's content.
            Fill(swatch, P(PanelMetrics.SwatchCorner), Color.FromArgb(0x99, theme.ControlFill.R, theme.ControlFill.G, theme.ControlFill.B));
        }
        var chevron = greyed ? theme.TextDisabled : !hit.IsNone && marks.Hot == hit ? theme.TextTertiary : theme.TextSecondary;
        Chevron(PanelGlyphs.Down, r.Right, r.Y + (r.Height / 2), chevron);
        if (!hit.IsNone && Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner));
        }
    }

    /// <summary>A swatch in an icon's place, 16 DIP (a choice's own picture): <see cref="Swatch(RectF, float, PanelSwatch)"/>.</summary>
    private void Swatch(FlyoutTile tile, float cx, float cy)
    {
        var side = P(PanelMetrics.Icon);
        Swatch(new RectF(cx - (side / 2), cy - (side / 2), side, side), P(PanelMetrics.Corner), PanelSwatch.Of(tile));
    }

    /// <summary>
    /// A colour filling <paramref name="r"/>, the one painter of the panel's swatches: set, its colour (§535: over a checker
    /// where it is see-through, so its opacity reads); unset, Windows' own (the accent and the base, half and half); none, a
    /// slash across it; §528 several widgets wearing several colours, a dash across it, as a check box's third state. Rimmed,
    /// so it shows on a card or a button of its own colour.
    /// </summary>
    private void Swatch(RectF r, float radius, PanelSwatch colour)
    {
        if (colour.Mixed)
        {
            var dash = MathF.Min(r.Width - P(8), P(12));
            PopupPaint.Fill(dc, PopupPaint.Rect(r.X + ((r.Width - dash) / 2), r.Y + (r.Height / 2) - P(1), dash, P(2)), P(1), theme.TextSecondary);
        }
        else if (colour.Slashed)
        {
            PopupPaint.Divider(dc, r.X + P(2), r.Right - P(2), r.Y + (r.Height / 2), Hair, theme.TextSecondary);
        }
        else if (colour.Color is { } own)
        {
            if (own.A < 255)
            {
                var secondary = theme.TextSecondary;
                var ink = new Argb(Flyout.CheckerAlpha, (byte)MathF.Round(secondary.r * 255), (byte)MathF.Round(secondary.g * 255), (byte)MathF.Round(secondary.b * 255));
                Fill(r, radius, theme.Ground);
                PatternPaint.Fill(dc, new Patterned(Pattern.Checks, ink), layout.Scale, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), new Vector2(r.X, r.Y), radius);
            }
            PopupPaint.Fill(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), radius, Gfx.Ink(own.R / 255f, own.G / 255f, own.B / 255f, own.A / 255f));
        }
        else
        {
            Fill(r, radius, theme.Ground);
            var half = PopupPaint.Rect(r.X + (r.Width / 2), r.Y, r.Width / 2, r.Height);
            dc->PushAxisAlignedClip(&half, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
            Fill(r, radius, theme.Accent);
            dc->PopAxisAlignedClip();
        }
        // §544: the Toolkit's ColorPickerButton rims its preview in ButtonBorderBrush (ControlElevationBorderBrush).
        Elevated(r, radius);
    }
}

/// <summary>
/// §520: the panel's widths measured with Gfx's text, as it is drawn; §571 a preview's folder's lines as its sample lays them
/// out over Windows' look <paramref name="windows"/> (LookPreviews.LinesDip).
/// </summary>
internal sealed class GfxPanelText(Gfx gfx, Look windows) : IPanelText
{
    public float Width(string text, float px, bool strong = false) => PopupPaint.Measure(gfx, text, px, strong ? Weight.Semibold : Weight.Regular).X;

    public float Tall(string text, float px, float width) => PopupPaint.WrappedTall(gfx, text, px, Weight.Regular, width);

    public float LinesDip(LookShown shows, float scale) => LookPreviews.LinesDip(gfx, shows, windows, scale);
}
