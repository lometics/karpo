// edit, §551 (the person, 2026-11-07: "taskbar menu should show a preview"): the Taskbar menu page's preview, the taskbar's
// right-click menu as the bar draws it now — its rows the very rows a right click on the bar shows (Dock.BarRows, from the
// person's entries, the layouts' names, Lock the taskbar's tick), measured as a menu's rows are (MenuMetrics: Explorer's own,
// which Flyout lays every menu by), in the tokens a menu of the bar wears (FlyoutTheme.For of the bar's look: the layout's,
// never the settings' Windows look), on the look previews' checker (§536: what of it is see-through shows).
//
// A menu is a window of its own (Flyout); a sample is a picture in the panel, drawn once a menu (its key every entry, the
// layouts, which is shown, the lock, the look: LookShown.Values): so the menu's plate, rim, rows, glyphs, ticks and rules are
// drawn here by Direct2D from the same rows and the same measures, and Explorer's two-colour pictures (ExplorerIcons) where a
// row's glyph has one, as the menu draws them.

using Karpo.Core;
using TerraFX.Interop.DirectX;

namespace Karpo.Dock;

/// <summary>
/// menus: a list menu's measures (DIP), Explorer's own (Windows 11 26H2, at 150 %), which Flyout lays every menu's rows by
/// and §551's preview of the taskbar's menu draws it by: 28 DIP fills 32 apart, 5 DIP in from the menu's sides, 3 DIP above
/// the first and under the last, 9 DIP a rule; the icon 16 DIP and the words 44 DIP in from the menu's edge.
/// </summary>
internal static class MenuMetrics
{
    public const float Fill = 28;
    public const float Gap = 2;
    public const float Inset = 5;
    public const float PadLeft = 11;
    public const float PadRight = 11;
    public const float Column = 28;
    public const float Pad = 3;
    public const float Rule = 9;
    public const float Header = 32;
    public const float Glyph = 16;
    public const float Corner = 8;

    /// <summary>A row's second line (a greyed shortcut's why), Caption 12 under its words.</summary>
    public const float Detail = 16;
}

/// <summary>
/// §633: what a list's rows stand by (DIP): a menu's, Explorer's (<see cref="Menu"/>, MenuMetrics), or a settings popup's, the
/// settings pages' own (<see cref="PanelMetrics.Popup"/>). <paramref name="Pad"/> over the first row and under the last,
/// <paramref name="Fill"/> a row's plate, <paramref name="Gap"/> over and under each plate, <paramref name="Inset"/> the plates
/// in from the sides, the icon <paramref name="Inset"/> + <paramref name="PadLeft"/> in and the words <paramref name="Column"/>
/// further, <paramref name="PadRight"/> the room at a plate's end, <paramref name="Rule"/> a rule's row, <paramref name="Header"/>
/// a heading's row in <paramref name="HeaderSize"/> semibold.
/// </summary>
internal readonly record struct RowMetrics(float Pad, float Fill, float Gap, float Inset, float PadLeft, float PadRight, float Column, float Rule, float Header, float HeaderSize)
{
    /// <summary>A menu's rows: Explorer's (MenuMetrics), a heading Caption 12.</summary>
    public static readonly RowMetrics Menu = new(MenuMetrics.Pad, MenuMetrics.Fill, MenuMetrics.Gap, MenuMetrics.Inset, MenuMetrics.PadLeft, MenuMetrics.PadRight, MenuMetrics.Column, MenuMetrics.Rule, MenuMetrics.Header, PanelMetrics.Caption);
}

/// <summary>§551: the taskbar's menu as a picture: where each of its rows stands, and drawn so (see the top of the file).</summary>
internal static class MenuSample
{
    /// <summary>A row of the sample: its menu row, its top and its height (DIP, from the menu's top).</summary>
    internal readonly record struct Line(FlyoutItem Item, float Top, float Height);

    /// <summary>Where each of <paramref name="items"/> stands, as a menu lays them (DIP), and how tall the menu is.</summary>
    public static (List<Line> Lines, float Height) Plan(IReadOnlyList<FlyoutItem> items)
    {
        List<Line> lines = [];
        var y = MenuMetrics.Pad;
        foreach (var item in items)
        {
            var tall = item.Separator ? MenuMetrics.Rule
                : item.Header ? MenuMetrics.Header
                : MenuMetrics.Fill + (2 * MenuMetrics.Gap) + (item.Detail is { Length: > 0 } ? MenuMetrics.Detail : 0);
            lines.Add(new Line(item, y, tall));
            y += tall;
        }
        return (lines, y + MenuMetrics.Pad);
    }

    /// <summary>
    /// <paramref name="items"/> drawn as a menu in <paramref name="theme"/> at <paramref name="scale"/>, <paramref name="width"/>
    /// px wide, its top-left at the origin: its plate (the material's tint, see-through where it is), its rim, each row's glyph
    /// — none where Explorer's picture stands (<paramref name="pictured"/>) —, words, tick and second line, each rule, each
    /// heading.
    /// </summary>
    public static unsafe void Paint(ID2D1DeviceContext* dc, Gfx gfx, FlyoutTheme theme, float scale, float width, IReadOnlyList<FlyoutItem> items, Func<string, bool> pictured)
    {
        float P(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var (lines, height) = Plan(items);
        var box = PopupPaint.Rect(0, 0, width, P(height));
        var plate = theme.Glass ? theme.Tint : theme.Base;
        PopupPaint.Fill(dc, box, P(MenuMetrics.Corner), PopupPaint.Ink(plate));
        if (theme.Stroke.A > 0)
        {
            var stroke = MathF.Max(1, MathF.Round(theme.StrokeDip * scale));
            PopupPaint.Stroke(dc, PopupPaint.Rect(stroke / 2, stroke / 2, width - stroke, P(height) - stroke), P(MenuMetrics.Corner) - (stroke / 2), stroke, PopupPaint.Ink(theme.Stroke));
        }
        var anyIcon = items.Any(item => !item.Separator && !item.Header && item.Icon is not null);
        var lead = P(MenuMetrics.Inset + MenuMetrics.PadLeft);
        var words = P(MenuMetrics.Inset + MenuMetrics.PadLeft + (anyIcon ? MenuMetrics.Column : 0));
        var trail = P(MenuMetrics.PadRight + MenuMetrics.Inset);
        var anyTick = items.Any(item => item.Checked is not null);
        var room = width - words - trail - (anyTick ? P(24) + P(MenuMetrics.Glyph) : 0);
        foreach (var line in lines)
        {
            var item = line.Item;
            var top = P(line.Top);
            var tall = P(line.Height);
            if (item.Separator)
            {
                // Inset as the fills are, a hairline in the rule's middle.
                PopupPaint.Divider(dc, P(MenuMetrics.Inset), width - P(MenuMetrics.Inset), top + MathF.Round((tall - P(1)) / 2), P(1), PopupPaint.Ink(theme.Divider));
                continue;
            }
            if (item.Header)
            {
                var head = PopupPaint.Measure(gfx, item.Text, P(12), Weight.Semibold, width - lead - trail);
                PopupPaint.Text(dc, gfx, item.Text, P(12), Weight.Semibold, theme.TextSecondary, lead, top + MathF.Round((tall - head.Y) / 2), width - lead - trail);
                continue;
            }
            var ink = item.Enabled ? theme.Text : theme.TextDisabled;
            var fill = P(MenuMetrics.Fill);
            var mid = top + P(MenuMetrics.Gap) + (fill / 2);
            var glyphBox = P(MenuMetrics.Glyph);
            if (item.Icon is FlyoutIcon.Glyph glyph && !pictured(glyph.Code))
            {
                var em = P(MenuMetrics.Glyph);
                PopupPaint.Glyph(dc, gfx, glyph.Code, em, ink, PopupPaint.Rect(lead - (em * 0.25f), mid - (em * 0.75f), em * 1.5f, em * 1.5f));
            }
            else if (item.Icon is FlyoutIcon.Picture picture)
            {
                PopupPaint.Picture(dc, picture.Pixels, PopupPaint.Rect(lead, mid - (glyphBox / 2), glyphBox, glyphBox), 0);
            }
            var said = PopupPaint.Measure(gfx, item.Text, P(14), Weight.Regular, room);
            PopupPaint.Text(dc, gfx, item.Text, P(14), Weight.Regular, ink, words, mid - (said.Y / 2), room);
            if (item.Detail is { Length: > 0 } detail)
            {
                PopupPaint.Text(dc, gfx, detail, P(12), Weight.Regular, theme.TextSecondary, words, mid + (fill / 2) - P(2), room);
            }
            if (item.Checked == true)
            {
                // The tick a small glyph (12 DIP) in the far slot, 16 DIP in from the fill's end, as a menu ticks a row.
                var em = P(12);
                var x = width - P(MenuMetrics.Inset) - P(16);
                PopupPaint.Glyph(dc, gfx, "", em, ink, PopupPaint.Rect(x - (em * 0.75f), mid - (em * 0.75f), em * 1.5f, em * 1.5f));
            }
        }
    }

    /// <summary>Where Explorer's picture of a row's glyph stands in the menu (px, from its top-left): the glyph's own box.</summary>
    public static IEnumerable<(string Glyph, RectF Box)> Pictured(IReadOnlyList<FlyoutItem> items, float scale, Func<string, bool> pictured)
    {
        float P(float dip) => MathF.Round(dip * scale, MidpointRounding.AwayFromZero);
        var lead = P(MenuMetrics.Inset + MenuMetrics.PadLeft);
        var side = P(MenuMetrics.Glyph);
        foreach (var line in Plan(items).Lines)
        {
            if (line.Item is { Separator: false, Header: false, Icon: FlyoutIcon.Glyph glyph } && pictured(glyph.Code))
            {
                var mid = P(line.Top) + P(MenuMetrics.Gap) + (P(MenuMetrics.Fill) / 2);
                yield return (glyph.Code, new RectF(lead, mid - (side / 2), side, side));
            }
        }
    }
}
