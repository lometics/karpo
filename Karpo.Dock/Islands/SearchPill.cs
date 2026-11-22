// search: the search box Windows 11's taskbar shows, drawn for Windows' own search button
// (BarKind.Search, which the scene draws) so it looks like the one on Windows' taskbar; the
// contacts' search field takes the same numbers.
//
// The numbers are Windows' own, read from the taskbar's resources (Taskbar.View's SearchBoxButton
// and its "_SearchBox_Wave3" visual states in MicrosoftWindows.Client.Core's resources.pri, and the
// WinUI 2.8 colours they name, on Windows 11 25H2, build 26200), re-read for backlog 38's follow-up:
//
//   * 220 DIPs long (SearchBoxButtonExtent, the box's MinWidth) with 2 either side
//     (SearchBoxButtonMargin) — Karpo's is as long as what it holds, at most that (§510, below);
//   * 32 DIPs tall on the 48-DIP bar: the button's 8 DIPs of padding above and below
//     (SearchBoxButtonNormalPadding), so its top is 8 DIPs down the bar;
//   * its corners Karpo's own, not Windows' pill (SearchBoxRoundCornerRadius, 16, half its height):
//     a control's 4 DIPs (PopupPaint.ControlRadiusDip), as the bar's highlights and every other
//     box Karpo draws have them, so the box is of a piece with the rest of the bar;
//   * the magnifier 11 DIPs in (SearchBoxIconMargin), left-aligned; the words 37 DIPs in and kept 16
//     DIPs from the far end (SearchBoxTextMargin), centred across, in BodyTextBlockStyle: Segoe UI
//     Variable Text, 14, regular — Karpo keeps them the magnifier's 11 from the far end (§510);
//   * the box is a text box at rest, not a taskbar button (SearchBoxTextControl*3):
//
//       state            fill                          edge                         words
//       rest             ControlFillColorDefault       ControlElevationBorderBrush  TextFillColorSecondary
//       pointer over     ControlFillColorSecondary     ControlElevationBorderBrush  TextFillColorSecondary
//       pressed, open    ControlFillColorInputActive   ControlStrokeColorDefault    TextFillColorTertiary
//
//     light #B3FFFFFF / #80F9F9F9 / #FFFFFFFF, edge #0F000000 with #29000000 along its bottom, words
//     #9E000000 / #72000000; dark #0FFFFFFF / #15FFFFFF / #B31E1E1E, edge #12FFFFFF with #18FFFFFF
//     along its bottom, words #C5FFFFFF / #87FFFFFF. ControlElevationBorderBrush is the edge's colour
//     with its bottom 3 DIPs shading to ControlStrokeColorSecondary (from a third of the way up);
//     "open" is the state Windows keeps while its Search is open, which Karpo's bar does not follow.
//
// Karpo's bars are not all 48 DIPs, so the numbers are for the 48-DIP bar's 38.4-DIP card and grow
// and shrink with the cell, as every widget's content does (Fit). Along the bar the box is as long
// as the magnifier, the words and the same 11 DIPs either end (Length), never over Windows' 220 DIPs
// on that bar, in proportion on any other, and not whole cells (docs 56). It was the 220 whatever
// its words, and so had 11 DIPs before the magnifier and some 140 after "Search" (the person,
// 2026-11-05: "the windows search box somehow has a different spacing left and right"); Windows'
// fills that room with its search highlight's picture, which Karpo has not.
// Only a horizontal bar thick enough for a 24-DIP box shows one — 40 DIPs and
// up —, as Windows' small taskbar shows only the icon.
//
// On the bar the box's fill and edge are the scene's (Scene.SearchPlate), so they change with the
// pointer as Windows' do, and the surface holds only the magnifier and the words (Paint, plate off).
// There the box is not Windows' 32 DIPs kept in by its margins but its whole slot, the card's height
// and length, with the card's corners: it is the part's card (Layout.SearchBoxed). Kept in, the card
// round it showed as a faint second edge on all four sides (the person, 2026-10-11). The margins
// and In are for a box drawn off the bar (a folder's card, the flyout's field).

using Karpo.Core;
using Karpo.Dock.Native;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

internal static unsafe class SearchPill
{
    /// <summary>Segoe Fluent Icons: Search, as the taskbar's search box has it.</summary>
    internal const string Glyph = "\uE721";

    /// <summary>What Windows' own box says when nothing is typed.</summary>
    internal static string WindowsPlaceholder => S.WidgetSearchPlaceholder;

    /// <summary>How long the box is along the bar on the 48-DIP bar: Windows' 220 DIPs (SearchBoxButtonExtent).</summary>
    internal const float LengthDip = 220;

    /// <summary>The 48-DIP bar's card (the bar less the room its cards keep off it), which Windows' numbers below are for.</summary>
    internal const float BaseCellDip = 38.4f;

    internal const float HeightDip = 32;
    internal const float MarginDip = 2;
    /// <summary>The room at either end of the box: before the magnifier (Windows' SearchBoxIconMargin) and after the words, so both ends are alike (§510).</summary>
    internal const float EndDip = 11;
    internal const float GlyphDip = 16;
    internal const float TextLeftDip = 37;
    internal const float TextDip = 14;

    /// <summary>
    /// A box shorter than this has no room for its words: the widget is the glyph alone, as on
    /// Windows' small taskbar. The 40-DIP bar's box is 26 DIPs; the 36-DIP bar's would be 23.
    /// </summary>
    private const float LeastHeightDip = 24;

    /// <summary>Windows' <paramref name="dips"/> on a bar whose cell is <paramref name="cellPx"/> px, in whole px.</summary>
    private static float Px(float dips, float cellPx) => PopupPaint.Round(dips * cellPx / BaseCellDip);

    /// <summary>The box's height on a bar whose cell is <paramref name="cellPx"/> px.</summary>
    internal static float Height(float cellPx) => Px(HeightDip, cellPx);

    /// <summary>
    /// The box's corners on a bar whose cell is <paramref name="cellPx"/> px: a control's 4 DIPs
    /// (<see cref="PopupPaint.ControlRadiusDip"/>) on the 48-DIP bar, growing and shrinking with the
    /// box. Its plate, its highlight and the keyboard's ring round it all take these.
    /// </summary>
    internal static float Radius(float cellPx) => Px(PopupPaint.ControlRadiusDip, cellPx);

    /// <summary>The cell whose box is <paramref name="heightPx"/> tall: what a box drawn off the bar (a folder's card) passes as its cell.</summary>
    internal static float CellFor(float heightPx) => heightPx * BaseCellDip / HeightDip;

    /// <summary>
    /// Whether a bar shows the box: a horizontal one whose cell (<paramref name="cellPx"/> px at
    /// <paramref name="scale"/> px per DIP) makes a box at least 24 DIPs tall. Else the glyph alone.
    /// </summary>
    internal static bool Fits(bool vertical, float cellPx, float scale) =>
        !vertical && cellPx > 0 && Height(cellPx) >= PopupPaint.Round(LeastHeightDip * (scale > 0 ? scale : 1));

    /// <summary>
    /// The room the box takes along the bar, whose words (<see cref="WordsPx"/>) are <paramref name="wordsPx"/> long: the
    /// words 37 DIPs in, the same 11 after them as before the magnifier (§510), never over Windows' 220 DIPs on the
    /// 48-DIP bar, in proportion on any other. Not whole cells.
    /// </summary>
    internal static float Length(float cellPx, float wordsPx) =>
        MathF.Min(Px(LengthDip, cellPx), Px(TextLeftDip, cellPx) + MathF.Ceiling(wordsPx) + Px(EndDip, cellPx));

    /// <summary>How long Windows' words (<see cref="WindowsPlaceholder"/>) are in the box on a bar whose cell is <paramref name="cellPx"/> px, laid as <see cref="Paint"/> lays them, in the font <paramref name="gfx"/> wears.</summary>
    internal static float WordsPx(Gfx gfx, float cellPx, in Look look)
    {
        var words = TextLine.Lay(gfx, WindowsPlaceholder, TextPx(cellPx, look), Weight.Regular, null);
        try
        {
            return words.Width;
        }
        finally
        {
            words.Release();
        }
    }

    /// <summary>The box inside the room it stands in: as long as the room less its margins, centred across.</summary>
    internal static D2D_RECT_F In(D2D_RECT_F room, float cellPx)
    {
        var margin = Px(MarginDip, cellPx);
        var height = MathF.Min(Height(cellPx), room.bottom - room.top);
        var top = PopupPaint.Round(room.top + (room.bottom - room.top - height) / 2);
        var right = MathF.Max(room.left + margin + 1, room.right - margin);
        return new D2D_RECT_F(room.left + margin, top, right, top + height);
    }

    /// <summary><see cref="In(D2D_RECT_F, float)"/> in the bar's own rectangles, window px.</summary>
    internal static RectF In(RectF room, float cellPx)
    {
        var box = In(new D2D_RECT_F(room.X, room.Y, room.Right, room.Bottom), cellPx);
        return new RectF(box.left, box.top, box.right - box.left, box.bottom - box.top);
    }

    /// <summary>The words' size: 14 DIPs on the 48-DIP bar, with Windows' text size on top.</summary>
    internal static float TextPx(float cellPx, in Look look) => TextDip * cellPx / BaseCellDip * TextLine.Scale(look);

    /// <summary>The height of the box's bottom edge that shades to the darker stroke (ControlElevationBorderBrush), in DIPs.</summary>
    internal const float ElevationDip = 3;

    /// <summary>The fill, the edge and its bottom, the magnifier and the words, in <paramref name="look"/> and <paramref name="state"/>.</summary>
    internal static SearchInks Inks(in Look look, SearchBoxState state = SearchBoxState.Rest)
    {
        if (look.HighContrast)
        {
            // High contrast: the window's colour edged in its text's, as a text box is drawn there;
            // pressed, the highlight's colours.
            var window = PopupPaint.Ink(look.HcWindow);
            var text = PopupPaint.Ink(look.HcText);
            if (state == SearchBoxState.Pressed)
            {
                var lit = PopupPaint.Ink(look.HcHighlight);
                var on = PopupPaint.Ink(look.HcHighlightText);
                return new SearchInks(lit, lit, lit, on, on);
            }
            return new SearchInks(window, state == SearchBoxState.Hover ? PopupPaint.Ink(look.HcHighlight) : text,
                state == SearchBoxState.Hover ? PopupPaint.Ink(look.HcHighlight) : text, text, text);
        }
        var pressed = state == SearchBoxState.Pressed;
        if (look.Dark)
        {
            var fill = state switch
            {
                SearchBoxState.Hover => Argb(0x15FFFFFF), // ControlFillColorSecondary
                SearchBoxState.Pressed => Argb(0xB31E1E1E), // ControlFillColorInputActive
                _ => Argb(0x0FFFFFFF), // ControlFillColorDefault
            };
            // ControlStrokeColorDefault, its bottom ControlStrokeColorSecondary but while pressed.
            return new SearchInks(fill, Argb(0x12FFFFFF), pressed ? Argb(0x12FFFFFF) : Argb(0x18FFFFFF),
                Argb(0xFFFFFFFF), pressed ? Argb(0x87FFFFFF) : Argb(0xC5FFFFFF));
        }
        var light = state switch
        {
            SearchBoxState.Hover => Argb(0x80F9F9F9),
            SearchBoxState.Pressed => Argb(0xFFFFFFFF),
            _ => Argb(0xB3FFFFFF),
        };
        return new SearchInks(light, Argb(0x0F000000), pressed ? Argb(0x0F000000) : Argb(0x29000000),
            Argb(0xE4000000), pressed ? Argb(0x72000000) : Argb(0x9E000000));
    }

    private static DXGI_RGBA Argb(uint argb) =>
        new(((argb >> 16) & 0xFF) / 255f, ((argb >> 8) & 0xFF) / 255f, (argb & 0xFF) / 255f, (argb >> 24) / 255f);

    /// <summary><see cref="ElevationDip"/> on a bar whose cell is <paramref name="cellPx"/> px.</summary>
    internal static float ElevationPx(float cellPx) => Px(ElevationDip, cellPx);

    /// <summary>
    /// Draws the box <paramref name="box"/> (<see cref="In"/>) with <paramref name="text"/> in it, on
    /// a bar whose cell is <paramref name="cellPx"/> px and whose hairline is <paramref name="hairline"/> px,
    /// in <paramref name="state"/>. Without <paramref name="plate"/> only the magnifier and the words:
    /// on the bar the scene lays the fill and the edge under them (Scene.SearchPlate).
    /// </summary>
    internal static void Paint(ID2D1DeviceContext* dc, Gfx gfx, D2D_RECT_F box, float cellPx, float hairline, in Look look, string text,
        SearchBoxState state = SearchBoxState.Rest, bool plate = true)
    {
        var inks = Inks(look, state);
        var height = box.bottom - box.top;
        var radius = Radius(cellPx);
        if (plate)
        {
            PopupPaint.Fill(dc, box, radius, inks.Fill);
            PopupPaint.ElevationStroke(dc, box, radius, hairline, inks.Stroke, inks.StrokeBottom, ElevationPx(cellPx));
            // The box is one piece, its faint dark fill too (PaintWatch): the rows between its edge
            // and its words are the box, not a gap between two rows.
            PaintWatch.Shape(box);
        }
        ID2D1SolidColorBrush* brush = null;
        TextLine glyph = default;
        TextLine words = default;
        try
        {
            brush = Gfx.Brush(dc, inks.Text);
            var middle = box.top + height / 2;
            glyph = TextLine.Lay(gfx, Glyph, Px(GlyphDip, cellPx), Weight.Regular, null, gfx.IconFamily);
            glyph.Draw(dc, brush, inks.Glyph, box.left + Px(EndDip, cellPx), middle - glyph.Height / 2);
            var left = box.left + Px(TextLeftDip, cellPx);
            var room = box.right - Px(EndDip, cellPx) - left;
            if (room >= 1 && text.Length > 0)
            {
                words = TextLine.Lay(gfx, text, TextPx(cellPx, look), Weight.Regular, room);
                words.Draw(dc, brush, inks.Text, left, middle - words.Height / 2);
            }
        }
        finally
        {
            words.Release();
            glyph.Release();
            ComUtil.Release(ref brush);
        }
    }
}

/// <summary>search: Windows' search box's three looks (see the top of SearchPill.cs).</summary>
public enum SearchBoxState
{
    Rest,
    Hover,
    Pressed,
}

/// <summary>search: the search box's colours in one state: its fill, its edge and the edge's bottom, the magnifier and the words.</summary>
internal readonly record struct SearchInks(DXGI_RGBA Fill, DXGI_RGBA Stroke, DXGI_RGBA StrokeBottom, DXGI_RGBA Glyph, DXGI_RGBA Text);
