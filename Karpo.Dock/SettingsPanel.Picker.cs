// edit, §670 (the person, 2026-11-12: "i want the pickers to be inlined. so that they dont show up as pickers that popover
// but basically as expandable sections that have the picker inside ... design it leaned on win11 style"): the Appearance
// page's dropdowns and colour buttons as Windows 11's SettingsExpander — the card's head says what it is set to (its swatch
// or its choice's picture, its words) before its chevron; open, the picker itself stands in the card under it, on the
// expander's content ground (CardBackgroundFillColorSecondary), ruled off from the head, from the head's words' column
// (48 DIP in) to the card's controls' end:
//
//  - **A choice** (a palette, a font): WinUI's RadioButtons, 36 DIP a choice — the 20 DIP radio (ControlAltFillColorSecondary
//    in ControlStrongStrokeColorDefault; the one in force AccentFillColorDefault round a 12 DIP TextOnAccent dot, 14 under the
//    pointer, 10 pressed), the choice's own picture, its words (a font's in its face) —, in up to three columns of at least
//    180 DIP where there are more than six, down the first column first. One with a search (a font's) its box over them across
//    the column, filtering them as it is typed; at most 18 shown, the one in force among them, "Type to find more" under them.
//  - **A colour**: Windows' colour grid (Settings › Personalization › Colors) — 36 DIP swatches 4 apart, None first, slashed;
//    the one in force ringed 2 DIP in TextFillColorPrimary, 2 DIP off it — then WinUI's ColorPicker: the spectrum 96 DIP tall
//    across the column, the hue and the opacity on 12 DIP tracks round at their ends, a 20 DIP thumb on each; the hex box a
//    row of its own under the picker, as any text box. Dragged, the bars wear the colour (FlyoutEventKind.Mix, Dock.PanelMixed)
//    and let go it is kept (MixKept) — the popup picker's own events, so a colour is mixed one way wherever it is.
//  - **A pattern**: its tiles, 72 DIP pieces of the card they would be drawn on, its name under each, the one in force ringed
//    in the accent.
//
// §672 (the person, 2026-11-12: "go ahead and add the extendable dropdowns to the other pages of the configuration"): every
// page's, not the Appearance page's alone. The page's every other row stays as it was (PanelRows.One). This file is
// the whole of it — the row kinds (PanelRowKind.Picker, PickerBody), the cells' places and their painting —, so taking it out
// is taking this file and its few calls out.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>§670: what a cell of an inline picker is.</summary>
internal enum PickerCellKind
{
    /// <summary>A choice of a list: its radio, its picture, its words.</summary>
    Option,

    /// <summary>A colour of the grid (None among them).</summary>
    Swatch,

    /// <summary>A look's tile (a pattern's): a piece of the card, its name under it.</summary>
    Tile,

    /// <summary>The colour picker's spectrum, hue track or opacity track (<see cref="PickerCell.Part"/>).</summary>
    Mix,

    /// <summary>"and 180 more" under a list cut short: words, nothing to press.</summary>
    More,

    /// <summary>The name of a choice standing in another's card (a font's weight), over its choices.</summary>
    Title,

    /// <summary>§682: an icon of the gallery: its glyph alone in a small square, its name its tooltip's.</summary>
    Glyph,
}

/// <summary>§670: one cell of an inline picker where it stands (page px), what a click or the keys reach on it.</summary>
internal readonly record struct PickerCell(PickerCellKind Kind, FlyoutItem Item, RectF Rect, PanelHit Hit)
{
    public MixPart Part => Item.Control is FlyoutMix mix ? mix.Part : default;
}

/// <summary>§670: which rows of a page are inline pickers, and the rows standing in one (see the top of the file).</summary>
internal static class PanelPickers
{
    /// <summary>The last part of the keys of a picker's rows: its body, its search, its hex box.</summary>
    public const string BodyKey = "#picker", SearchKey = "#search", HexKey = "#hex";

    /// <summary>
    /// Whether <paramref name="item"/>, a choice of one (a dropdown's or a colour button's level), is an inline picker: one with
    /// choices or a colour to mix; never a way to add one, nor one standing beside another (a font's weight beside its family:
    /// its choices go into that one's card, <see cref="Beside"/>).
    /// </summary>
    public static bool Takes(FlyoutItem item, IReadOnlyList<FlyoutItem> children) =>
        item.PickOne && !item.Adds && !item.Beside && children.Any(kid => Choice(kid) || kid.Control is FlyoutMix);

    /// <summary>
    /// §676: the lookup of a choice found by its search (a time zone, a language, a place: its field's finder), whose picker is
    /// its search over what it finds; null for one of choices of its own.
    /// </summary>
    public static FlyoutItem? Lookup(IReadOnlyList<FlyoutItem> children) =>
        children.FirstOrDefault(kid => kid.Control is FlyoutField { Filters: false, Finder: not null, Finds: true });

    /// <summary>§682: whether a dropdown's level <paramref name="children"/> unfolds inline under its line: choices of its own, or a colour's mixer.</summary>
    public static bool Unfolds(IReadOnlyList<FlyoutItem> children) => children.Any(kid => Choice(kid) || kid.Control is FlyoutMix);

    /// <summary>§682: whether <paramref name="row"/> is a dropdown whose picker unfolds under its line (<see cref="PanelRow.Rows"/>) rather than popping up.</summary>
    public static bool Inline(PanelRow row) => row.Kind is PanelRowKind.Choice or PanelRowKind.Color && row.Rows.Count > 0;

    /// <summary>A choice of a picker's level: no rule, heading, field, mixer nor level of its own.</summary>
    public static bool Choice(FlyoutItem kid) =>
        !kid.Separator && !kid.Header && kid.Control is null && kid.Children is null && kid.Checked is not null;

    /// <summary>Whether <paramref name="item"/>'s level mixes a colour.</summary>
    public static bool Colours(FlyoutItem item) => item.Children?.Any(kid => kid.Control is FlyoutMix) == true;

    /// <summary>
    /// The rows of the picker <paramref name="item"/> keyed <paramref name="key"/>: its search's box where it has one, its body
    /// (the cells: <see cref="PanelLayout.PickerCells"/>), its hex box where it has one.
    /// </summary>
    public static List<PanelRow> Rows(FlyoutItem item, IReadOnlyList<FlyoutItem> children, string key)
    {
        List<PanelRow> rows = [];
        if (children.FirstOrDefault(kid => kid.Control is FlyoutField { Finds: true }) is { } search)
        {
            rows.Add(new PanelRow(PanelRowKind.Text, search, key + "/" + SearchKey));
        }
        rows.Add(new PanelRow(PanelRowKind.PickerBody, item, key + "/" + BodyKey));
        if (children.FirstOrDefault(kid => kid.Control is FlyoutField { Hex: true }) is { Control: FlyoutField hex } typed)
        {
            rows.Add(new PanelRow(PanelRowKind.Text, typed, key + "/" + HexKey) { Value = hex.Text.Length > 0 ? hex.Text : null });
        }
        return rows;
    }

    /// <summary>
    /// A choice standing beside a picker (a font's weight beside its family: FlyoutItem.Beside) as a body of that picker's
    /// own, under the picker's: its name over its choices.
    /// </summary>
    public static PanelRow Beside(PanelRow choice) => new(PanelRowKind.PickerBody, choice.Item, choice.Key + "/" + BodyKey);

    /// <summary>The key of the search over the picker whose body is keyed <paramref name="body"/>.</summary>
    public static string SearchOf(string body) => body[..^BodyKey.Length] + SearchKey;

    /// <summary>Whether <paramref name="row"/> is a picker's search: its box across the column, its words filtering the choices.</summary>
    public static bool Searches(PanelRow row) => row.Kind == PanelRowKind.Text && row.Typed is { Finds: true };

    /// <summary>§676: whether <paramref name="row"/> is a lookup's search: what it finds asked of its finder as it is typed.</summary>
    public static bool LooksUp(PanelRow row) => Searches(row) && row.Typed is { Filters: false, Finder: not null };

    /// <summary>§676: the key of the body under the search keyed <paramref name="search"/>.</summary>
    public static string BodyOf(string search) => search[..^SearchKey.Length] + BodyKey;

    /// <summary>Whether <paramref name="item"/> matches what is typed in its picker's search.</summary>
    public static bool Matches(FlyoutItem item, string typed) =>
        typed.Length == 0
        || item.Text.Contains(typed, StringComparison.CurrentCultureIgnoreCase)
        || item.Words?.Any(word => word.Contains(typed, StringComparison.CurrentCultureIgnoreCase)) == true;
}

/// <summary>§670: an inline picker's metrics, in DIP (see the top of the file).</summary>
internal static class PickerMetrics
{
    public const float Pad = 12;
    public const float Option = 36;
    public const float OptionLeast = 180;
    public const int OptionColumns = 3;
    public const int OptionsInColumn = 6;
    public const int OptionsMost = 18;

    /// <summary>§676: how many of a lookup's finds its body stands tall for, a line for what it says under them (searching, nothing found): its height whatever it finds; §682 the rest scrolled to.</summary>
    public const int FoundMost = 8;

    /// <summary>
    /// §682 (the person, 2026-11-12: "the inline selectors should be limited to a maximum height after which they have to
    /// scroll. this can be fairly large but should give an upper bound that not the whole screen is filled"): the tallest a
    /// picker's body stands, DIP; what is past it scrolled to inside the body.
    /// </summary>
    public const float MostTall = 400;

    /// <summary>§682: a picker's scroll bar: its width, and its room at the body's right edge.</summary>
    public const float ScrollBar = 4;
    public const float Radio = 20;
    public const float Dot = 12;
    public const float Swatch = 36;
    public const float SwatchRing = 2;
    public const float TileSide = 72;

    /// <summary>§693: a changed palette's badge after its name (WinUI's InfoBadge with an icon: 16 DIP round), its glyph's size.</summary>
    public const float Badge = 16;

    public const float BadgeGlyph = 9;

    /// <summary>§682: an icon's square in the gallery, dense (the person, 2026-11-12: "icon selector should show the icons way denser, without title").</summary>
    public const float GlyphSide = 40;

    /// <summary>§682: the narrowest a colour picker's body stands its swatches beside its mixer, DIP; narrower, the mixer under them.</summary>
    public const float ColourBeside = 420;
    public const float TileName = 24;

    public const float Spectrum = 96;
    public const float Slider = 28;
    public const float Track = 12;
    public const float Thumb = 20;
}

internal sealed partial class PanelLayout
{
    /// <summary>§670: what is typed in the search keyed <paramref name="key"/> (its box being typed in, or left with words in it).</summary>
    public string SearchText(string key) => Input.Searches?.GetValueOrDefault(key) ?? "";

    /// <summary>§670: how wide what an inline picker's head says stands (px): its words, at most a control's width, its picture 8 before them.</summary>
    public float PickerSaid(PanelRow row)
    {
        var words = MathF.Min(MathF.Ceiling(text.Width(row.Value ?? "", P(PanelMetrics.Body))), P(PanelMetrics.ControlWide));
        var pictured = (row.Swatch is not null && PanelPickers.Colours(row.Item)) || row.ValueIcon is not null;
        return words + (pictured ? P(PanelMetrics.Gap) + P(PanelMetrics.Icon + 4) : 0);
    }

    /// <summary>§670: where what an inline picker's head says starts (page px): its picture, its words, a label's room before its chevron.</summary>
    public float PickerValueLeft(PanelLine line) => ControlEnd(line) - P(PanelMetrics.ChevronRoom) - P(PanelMetrics.Label) - PickerSaid(line.Row);

    /// <summary>
    /// §670: how tall a picker's body stands across <paramref name="w"/> px from <paramref name="left"/> (its cells' foot, a pad
    /// under them). §672 (the person, 2026-11-12: "make sure that you handle layout shift properly there. especially when it is a
    /// setting that is on the bottom of a page, i dont want it moving around then"): as tall as its whole list however few what
    /// is typed leaves, so a search never moves the card, the rows under it or the page's foot.
    /// </summary>
    private float PickerTall(PanelRow row, float left, float w)
    {
        // §676: a lookup's as tall as its most and a line under them, whatever it finds.
        if (PanelPickers.Lookup(row.Item.Children ?? []) is not null)
        {
            return P(PanelMetrics.Tight) + ((PickerMetrics.FoundMost + 1) * P(PickerMetrics.Option)) + P(PickerMetrics.Pad);
        }
        var line = new PanelLine(row, new RectF(left, 0, w, 0), Inner: true);
        // §682: at most so tall, the rest scrolled to.
        return MathF.Min(MathF.Max(Foot(Natural(line)), Foot(Natural(line, typed: ""))), P(PickerMetrics.MostTall)) + P(PickerMetrics.Pad);
    }

    private static float Foot(List<PickerCell> cells) => cells.Count == 0 ? 0 : cells.Max(cell => cell.Rect.Bottom);

    /// <summary>§682: where picker body <paramref name="line"/> shows its cells (page px): its line less the pad under them.</summary>
    public RectF PickerBand(PanelLine line) => line.Rect with { Height = MathF.Max(0, line.Rect.Height - P(PickerMetrics.Pad)) };

    /// <summary>§682: how far past its band picker body <paramref name="line"/>'s cells reach (px): how far it scrolls; 0 where they all show.</summary>
    public float PickerOverflow(PanelLine line) =>
        line.Row.Kind == PanelRowKind.PickerBody ? MathF.Max(0, Foot(Natural(line)) - PickerBand(line).Bottom) : 0;

    /// <summary>§682: how far picker body <paramref name="line"/> is scrolled (px), as the panel was told, kept within how far it scrolls.</summary>
    public float PickerScroll(PanelLine line) => Math.Clamp(Input.Scrolls?.GetValueOrDefault(line.Row.Key) ?? 0, 0, PickerOverflow(line));

    /// <summary>§682: the picker body under <paramref name="point"/> (panel px) that scrolls; null for none.</summary>
    public PanelLine? ScrollerAt(Vector2 point)
    {
        if (!IsOpen || !Viewport.Contains(point.X, point.Y))
        {
            return null;
        }
        var (x, y) = (point.X - Viewport.X, point.Y - Viewport.Y + Scroll);
        return Blocks.SelectMany(block => block.Lines).FirstOrDefault(line => line.Row.Kind == PanelRowKind.PickerBody && line.Rect.Contains(x, y) && PickerOverflow(line) > 0);
    }

    /// <summary>
    /// §670: the cells of picker body <paramref name="line"/> where they stand (page px), in the order the keys reach them. §682
    /// scrolled as far as it is (<see cref="PickerScroll"/>), those out of its band left out.
    /// </summary>
    public List<PickerCell> PickerCells(PanelLine line, string? typed = null)
    {
        var cells = Natural(line, typed);
        if (line.Row.Kind != PanelRowKind.PickerBody || PickerOverflow(line) <= 0)
        {
            return cells;
        }
        var by = PickerScroll(line);
        var band = PickerBand(line);
        return [.. cells.Select(cell => cell with { Rect = cell.Rect with { Y = cell.Rect.Y - by } }).Where(cell => cell.Rect.Bottom > band.Y && cell.Rect.Y < band.Bottom)];
    }

    /// <summary>
    /// §670: every cell of picker body <paramref name="line"/> where it stands unscrolled (page px), in the order the keys reach
    /// them: its swatches or its tiles or its choices, then its mixer (see the top of the file).
    /// </summary>
    private List<PickerCell> Natural(PanelLine line, string? typed = null)
    {
        var item = line.Row.Item;
        var kids = item.Children ?? [];
        var x = NameX(line);
        var w = ControlEnd(line) - x;
        // Under its search, close: the search and its choices one control.
        var y = line.Rect.Y + P(kids.Any(kid => kid.Control is FlyoutField { Finds: true }) ? PanelMetrics.Tight : PickerMetrics.Pad);
        var gap = P(PanelMetrics.Tight);
        var key = line.Row.Key;
        List<PickerCell> cells = [];
        PanelHit Act(FlyoutItem kid, int at) => new(PanelHitKind.Act, kid.Id, at, Key: key);
        // A choice standing in another's card its name over its choices.
        if (item.Beside)
        {
            cells.Add(new PickerCell(PickerCellKind.Title, item, new RectF(x, y, w, P(PanelMetrics.BodyLine)), PanelHit.None));
            y += P(PanelMetrics.BodyLine) + P(PanelMetrics.Tight);
        }
        // §676: a lookup's what its search finds, each a choice of the field's: at most so many, what it says under them.
        if (PanelPickers.Lookup(kids) is { Control: FlyoutField field } lookup)
        {
            var finds = Input.Finds?.GetValueOrDefault(PanelPickers.SearchOf(key)) ?? PanelFinds.Before(field.Finder!);
            var tall = P(PickerMetrics.Option);
            var lead = P(PanelMetrics.Gap);
            // §682 (the person, 2026-11-12: "show two columns as well"): two columns down, every one found, scrolled to.
            var (columns, each) = Columns(w, 2);
            var shown = finds.Rows.Count;
            var down = Math.Max(1, (int)Math.Ceiling(shown / (double)columns));
            for (var at = 0; at < shown; at++)
            {
                var found = finds.Rows[at];
                var kid = new FlyoutItem(lookup.Id, found.Title, found.Glyph.Length > 0 ? new FlyoutIcon.Glyph(found.Glyph) : null, Detail: found.Detail, Checked: found.Chosen);
                var r = new RectF(x - lead + (at / down * (each + P(PanelMetrics.Gap))), y + (at % down * tall), each + lead, tall);
                cells.Add(new PickerCell(PickerCellKind.Option, kid, r, new PanelHit(PanelHitKind.Act, lookup.Id, at, Key: key)));
            }
            var said = finds.Said ?? (finds.Rows.Count == 0 && SearchText(PanelPickers.SearchOf(key)).Length > 0 ? (field.NoMatch.Length > 0 ? field.NoMatch : S.DockMenuNoMatch)
                : finds.Rows.Count > shown ? S.SettingsAboutImportMore(finds.Rows.Count - shown) : null);
            if (said is not null)
            {
                cells.Add(new PickerCell(PickerCellKind.More, new FlyoutItem(0, said), new RectF(x, y + (Math.Min(shown, PickerMetrics.FoundMost) * tall), w, tall), PanelHit.None));
            }
            return cells;
        }
        var colours = PanelPickers.Colours(item);
        var choices = kids.Where(PanelPickers.Choice).ToList();
        // §682 (the person, 2026-11-12: "the inline color selector is in a row layout, it should be more in a column layout"):
        // the swatches in the left part, the mixer beside them where the body is wide enough.
        var mixes = kids.Where(kid => kid.Control is FlyoutMix).ToList();
        var beside = mixes.Count > 0 && choices.Count > 0 && w >= P(PickerMetrics.ColourBeside);
        var full = w;
        if (beside)
        {
            w = MathF.Floor((full - P(PickerMetrics.Pad)) * 0.5f);
        }
        var top = y;
        // §682: the icon gallery a dense grid of glyphs, no names, filtered by its search.
        if (choices.Count > 0 && choices.All(kid => kid.Icon is FlyoutTile { Code.Length: > 0 }))
        {
            typed ??= SearchText(PanelPickers.SearchOf(key));
            var side = P(PickerMetrics.GlyphSide);
            var step = side + P(PanelMetrics.Tight);
            var across = Math.Max(1, (int)MathF.Floor((w + P(PanelMetrics.Tight)) / step));
            var at = 0;
            foreach (var kid in choices.Where(kid => PanelPickers.Matches(kid, typed)))
            {
                cells.Add(new PickerCell(PickerCellKind.Glyph, kid, new RectF(x + (at % across * step), y + (at / across * step), side, side), Act(kid, choices.IndexOf(kid))));
                at++;
            }
            if (at == 0 && typed.Length > 0)
            {
                cells.Add(new PickerCell(PickerCellKind.More, new FlyoutItem(0, S.DockMenuNoMatch), new RectF(x, y, w, P(PickerMetrics.Option)), PanelHit.None));
            }
            return cells;
        }
        // A colour's grid, None first; a look's tiles.
        bool Tiled(FlyoutItem kid) => kid.Icon is FlyoutTile { Cell: true, CellDip: > 0 };
        bool Swatched(FlyoutItem kid) => colours && (kid.Icon is FlyoutTile { Cell: true, CellDip: 0 } || kid.Icon is not FlyoutTile);
        // §675: a choice whose picture is what it draws (a widget's style, a palette) a tile of that picture.
        if (choices.Count > 0 && (choices.All(kid => Tiled(kid) || Swatched(kid)) || choices.All(kid => kid.Shows is not null)))
        {
            var pictured = choices[0].Shows is not null;
            // §679: a sketch as big as what it shows needs (LookPreview.PictureDip).
            var picture = choices[0].Shows is { } shows ? LookPreview.PictureDip(shows) : default;
            var tiled = pictured || choices.Any(Tiled);
            // §681: a bar's app buttons the whole width, one to a line.
            var side = pictured && LookPreview.FullWide(choices[0].Shows!) ? MathF.Floor(w)
                : P(pictured ? picture.X : tiled ? PickerMetrics.TileSide : PickerMetrics.Swatch);
            var tall = P(pictured ? picture.Y : tiled ? PickerMetrics.TileSide : PickerMetrics.Swatch) + (tiled ? P(PickerMetrics.TileName) : 0);
            var step = side + (tiled ? P(PanelMetrics.Gap) : gap);
            // As many to a line as fit, the lines shared out evenly (13 colours 7 and 6, never 12 and a lone one).
            var most = Math.Max(1, (int)MathF.Floor((w + step - side) / step));
            var across = (int)Math.Ceiling(choices.Count / Math.Ceiling(choices.Count / (double)most));
            var down = tall + (tiled ? P(PanelMetrics.Gap) : gap);
            // §690 (the person, 2026-11-12: "the colors of the color picker on the side should be fixed, there should be one row
            // that is filled with the most recent color choices"): a colour's swatches in their groups, each from a line of its
            // own — None and the recent colours one line, however few, the presets under it always where they stand.
            var groups = colours ? Groups(kids) : [choices];
            if (colours)
            {
                across = Math.Min(most, Math.Max(ColorPalette.Columns, groups.Max(group => Math.Min(group.Count, most))));
            }
            // A colour with neither None nor a recent colour yet keeps that line all the same, empty: its presets never move.
            var lines = colours && groups.Count == 1 ? 1 : 0;
            foreach (var group in groups)
            {
                for (var at = 0; at < group.Count; at++)
                {
                    var r = new RectF(x + (at % across * step), y + ((lines + (at / across)) * down), side, tall);
                    cells.Add(new PickerCell(tiled ? PickerCellKind.Tile : PickerCellKind.Swatch, group[at], r, Act(group[at], choices.IndexOf(group[at]))));
                }
                lines += Math.Max(1, (int)Math.Ceiling(group.Count / (double)across));
            }
            y = cells.Count > 0 ? cells.Max(cell => cell.Rect.Bottom) : y;
        }
        else if (choices.Count > 0)
        {
            // A list of choices: filtered by its search; at most so many, the one in force among them.
            var searched = PanelPickers.SearchOf(key);
            typed ??= SearchText(searched);
            var shown = choices.Where(kid => PanelPickers.Matches(kid, typed)).ToList();
            // §684 (the person, 2026-11-12: "right now it is not possible to browse all the fonts anymore"): every one, the body
            // scrolled to past its most (§682), none left out.
            var most = int.MaxValue;
            var more = Math.Max(0, shown.Count - most);
            if (more > 0)
            {
                var chosen = shown.Find(kid => kid.Checked == true);
                shown = [.. shown.Take(most)];
                if (chosen is not null && !shown.Contains(chosen))
                {
                    shown[^1] = chosen;
                }
            }
            // §682 (the person, 2026-11-12: "generally the inline dropdowns without special contents should be two column ones"):
            // two columns down where two fit, three for a long list.
            var (columns, each) = Columns(w, shown.Count < 2 ? 1 : shown.Count > 3 * PickerMetrics.OptionsInColumn ? PickerMetrics.OptionColumns : 2);
            var down = Math.Max(1, (int)Math.Ceiling(shown.Count / (double)columns));
            var tall = P(PickerMetrics.Option);
            // Its radios stand in the head's words' column: the row's room starts a radio's half before it, so a lit row's
            // fill reaches round it.
            var lead = P(PanelMetrics.Gap);
            for (var at = 0; at < shown.Count; at++)
            {
                var r = new RectF(x - lead + (at / down * (each + P(PanelMetrics.Gap))), y + (at % down * tall), each + lead, tall);
                cells.Add(new PickerCell(PickerCellKind.Option, shown[at], r, Act(shown[at], choices.IndexOf(shown[at]))));
            }
            y += down * tall;
            if (more > 0 || (shown.Count == 0 && typed.Length > 0))
            {
                var note = new FlyoutItem(0, shown.Count == 0 ? S.DockMenuNoMatch : S.SettingsAboutImportMore(more));
                cells.Add(new PickerCell(PickerCellKind.More, note, new RectF(x, y, w, tall), PanelHit.None));
                y += tall;
            }
        }
        // The mixer under them, ruled apart by a gap; §682 beside them where there is room.
        if (mixes.Count > 0)
        {
            var mixX = x;
            var mixW = w;
            if (beside)
            {
                mixX = x + w + P(PickerMetrics.Pad);
                mixW = full - w - P(PickerMetrics.Pad);
                y = top;
            }
            else
            {
                y += cells.Count > 0 ? P(PickerMetrics.Pad) : 0;
            }
            foreach (var mix in mixes)
            {
                var part = ((FlyoutMix)mix.Control!).Part;
                var tall = P(part == MixPart.Spectrum ? PickerMetrics.Spectrum : PickerMetrics.Slider);
                var r = new RectF(mixX, y, mixW, tall);
                cells.Add(new PickerCell(PickerCellKind.Mix, mix, r, new PanelHit(PanelHitKind.Mix, mix.Id, (int)part, Key: key)));
                y += tall + (part == MixPart.Spectrum ? P(PanelMetrics.Gap) : 0);
            }
        }
        return cells;
    }

    /// <summary>§690: a level's choices in the groups its rules part them into (a colour's: None and the recent ones; the presets).</summary>
    private static List<List<FlyoutItem>> Groups(IReadOnlyList<FlyoutItem> kids)
    {
        List<List<FlyoutItem>> groups = [[]];
        foreach (var kid in kids)
        {
            if (kid.Separator && groups[^1].Count > 0)
            {
                groups.Add([]);
            }
            else if (PanelPickers.Choice(kid))
            {
                groups[^1].Add(kid);
            }
        }
        return [.. groups.Where(group => group.Count > 0)];
    }

    /// <summary>
    /// §682: how many columns of choices stand across <paramref name="w"/> px — <paramref name="most"/>, fewer where they would
    /// be narrower than <see cref="PickerMetrics.OptionLeast"/> — and how wide each is.
    /// </summary>
    private (int Columns, float Each) Columns(float w, int most)
    {
        var columns = Math.Clamp((int)MathF.Floor((w + P(PanelMetrics.Gap)) / (P(PickerMetrics.OptionLeast) + P(PanelMetrics.Gap))), 1, Math.Max(1, most));
        return (columns, MathF.Floor((w - ((columns - 1) * P(PanelMetrics.Gap))) / columns));
    }

    /// <summary>§675: where the picture of tile <paramref name="cell"/> (its piece over its name) stands, a gap in: its sample's room.</summary>
    public RectF PictureIn(PickerCell cell)
    {
        var inset = P(PanelMetrics.Tight);
        var r = cell.Rect;
        return new RectF(r.X + inset, r.Y + inset, r.Width - (2 * inset), r.Height - P(PickerMetrics.TileName) - (2 * inset));
    }

    /// <summary>
    /// §670: where a colour track's thumb travels in mixer cell <paramref name="r"/> (page or panel px alike): a thumb's half in
    /// from either end, so the thumb never stands past the track; the spectrum the whole of it.
    /// </summary>
    public RectF MixTravel(MixPart part, RectF r)
    {
        if (part == MixPart.Spectrum)
        {
            return r;
        }
        var half = P(PickerMetrics.Thumb) / 2;
        return new RectF(r.X + half, r.Y + (r.Height / 2), MathF.Max(1, r.Width - (2 * half)), 0);
    }

    /// <summary>
    /// §670: the colour a pointer at <paramref name="point"/> (panel px) mixes on mixer <paramref name="hit"/>, from
    /// <paramref name="from"/> (hue held where a grey has none: <paramref name="held"/>); null off any mixer.
    /// </summary>
    public (Argb Color, Hsv Hsv)? MixAt(PanelHit hit, Vector2 point, Argb now, Hsv held)
    {
        if (hit.Kind != PanelHitKind.Mix)
        {
            return null;
        }
        var r = MixTravel((MixPart)hit.Index, RectOf(hit));
        var hsv = held;
        switch ((MixPart)hit.Index)
        {
            case MixPart.Spectrum:
                hsv = ColorMix.Spectrum(held.H, point.X - r.X, point.Y - r.Y, r.Width, r.Height);
                return (ColorMix.ToArgb(hsv, now.A), hsv);
            case MixPart.Hue:
                hsv = held with { H = ColorMix.Slider(point.X - r.X, r.Width, ColorMix.MostHue) };
                return (ColorMix.ToArgb(hsv, now.A), hsv);
            default:
                return (now with { A = ColorMix.Alpha(ColorMix.Slider(point.X - r.X, r.Width, ColorMix.MostOpacity)) }, hsv);
        }
    }
}

internal sealed unsafe partial class PanelPainter
{
    /// <summary>§682: Segoe Fluent Icons' Edit, a palette's mark that the person changed it.</summary>
    private const string ChangedGlyph = "\uE70F";

    /// <summary>§670: a picker's body: its cells (see the top of the file), moved with its line (<paramref name="at"/>: a line being dragged).</summary>
    private void Picker(PanelLine line, RectF at)
    {
        var dx = at.X - line.Rect.X;
        var dy = at.Y - line.Rect.Y;
        var greyed = line.Row.Greyed;
        // §682: cut to its band, scrolled; its scroll bar at its right edge.
        var band = layout.PickerBand(line);
        var over = layout.PickerOverflow(line);
        if (over > 0)
        {
            var cut = band with { X = band.X + dx, Y = band.Y + dy };
            PopupPaint.Clipped(dc, PopupPaint.Rect(cut.X, cut.Y, cut.Width, cut.Height), 0, () => Cells(line, dx, dy, greyed));
            var thick = P(PickerMetrics.ScrollBar);
            var tall = MathF.Max(P(24), cut.Height * cut.Height / (cut.Height + over));
            var y = cut.Y + ((cut.Height - tall) * (layout.PickerScroll(line) / over));
            PopupPaint.Fill(dc, PopupPaint.Rect(cut.Right - thick - P(2), y, thick, tall), thick / 2, theme.TextSecondary);
            return;
        }
        Cells(line, dx, dy, greyed);
    }

    /// <summary>§670: picker body <paramref name="line"/>'s cells, each moved by (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    private void Cells(PanelLine line, float dx, float dy, bool greyed)
    {
        foreach (var cell in layout.PickerCells(line))
        {
            var r = cell.Rect with { X = cell.Rect.X + dx, Y = cell.Rect.Y + dy };
            switch (cell.Kind)
            {
                case PickerCellKind.Option:
                    Radio(cell, r, greyed);
                    break;
                case PickerCellKind.Swatch:
                    GridSwatch(cell, r, greyed);
                    break;
                case PickerCellKind.Tile:
                    LookTile(cell, r, greyed);
                    break;
                case PickerCellKind.Glyph:
                    GlyphTile(cell, r, greyed);
                    break;
                case PickerCellKind.Mix:
                    Mixer(cell, r, line.Row.Key);
                    break;
                case PickerCellKind.Title:
                    Text(cell.Item.Text, r.X, r.Y + (r.Height / 2), PanelMetrics.Body, greyed ? theme.TextDisabled : theme.Text, Weight.Semibold, most: r.Width);
                    break;
                case PickerCellKind.More:
                    Text(cell.Item.Text, r.X, r.Y + (r.Height / 2), PanelMetrics.Caption, theme.TextSecondary, most: r.Width);
                    break;
            }
        }
    }

    /// <summary>§670: a choice as WinUI's RadioButton, its row lit under the pointer (see the top of the file).</summary>
    private void Radio(PickerCell cell, RectF r, bool greyed)
    {
        var hit = cell.Hit;
        var off = greyed || !cell.Item.Enabled;
        var on = cell.Item.Checked == true;
        if (!off)
        {
            Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
        }
        var mid = r.Y + (r.Height / 2);
        var radius = P(PickerMetrics.Radio) / 2;
        var centre = new Vector2(r.X + P(PanelMetrics.Gap) + radius, mid);
        var hot = !off && marks.Hot == hit;
        var down = !off && IsPressed(hit);
        if (on)
        {
            PopupPaint.Circle(dc, centre, radius, Ink(off ? theme.AccentDisabled : down ? theme.AccentPressed : hot ? theme.AccentHover : theme.Accent));
            PopupPaint.Circle(dc, centre, P(down ? PickerMetrics.Dot - 2 : hot ? PickerMetrics.Dot + 2 : PickerMetrics.Dot) / 2, off ? theme.TextOnAccentDisabled : theme.TextOnAccent);
        }
        else
        {
            if (!off)
            {
                PopupPaint.Circle(dc, centre, radius, Ink(down ? theme.ControlAltFillPressed : hot ? theme.ControlAltFillHover : theme.ControlAltFill));
            }
            PopupPaint.Ring(dc, centre, radius - (Hair / 2), Hair, Ink(off ? theme.ControlStrongStrokeDisabled : theme.ControlStrongStroke));
            if (down)
            {
                PopupPaint.Circle(dc, centre, P(PickerMetrics.Dot - 2) / 2, theme.TextSecondary);
            }
        }
        var x = centre.X + radius + P(PanelMetrics.Gap);
        if (cell.Item.Icon is FlyoutTile tile)
        {
            var side = P(PanelMetrics.Icon + 4);
            Tile(tile, new RectF(x, mid - (side / 2), side, side));
            x += side + P(PanelMetrics.Gap);
        }
        else if (cell.Item.Icon is { } icon)
        {
            Icon(icon, x + (P(PanelMetrics.Icon) / 2), mid, off ? theme.TextDisabled : theme.Text);
            x += P(PanelMetrics.Icon) + P(PanelMetrics.Gap);
        }
        var most = r.Right - P(PanelMetrics.Gap) - x;
        // Its mark after its words (a palette holding the person's edits): §693 the changed badge, as its tile's.
        var marked = cell.Item.Value?.Text is { Length: > 0 };
        var badge = marked ? P(PickerMetrics.Badge) + P(PanelMetrics.Gap) : 0;
        using (gfx.Wearing(cell.Item.Face ?? gfx.Typeface))
        {
            var wrote = Text(cell.Item.Text, x, mid, PanelMetrics.Body, off ? theme.TextDisabled : theme.Text, most: most - badge);
            if (marked)
            {
                ChangedBadge(new Vector2(x + wrote + P(PanelMetrics.Gap) + (P(PickerMetrics.Badge) / 2), mid), off);
            }
        }
        if (Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner));
        }
    }

    /// <summary>§670: a colour of the grid: its swatch filling the cell less its ring's room, ringed where it is in force (see the top of the file).</summary>
    private void GridSwatch(PickerCell cell, RectF r, bool greyed)
    {
        var hit = cell.Hit;
        var ring = P(PickerMetrics.SwatchRing);
        var inner = new RectF(r.X + (2 * ring), r.Y + (2 * ring), r.Width - (4 * ring), r.Height - (4 * ring));
        var colour = cell.Item.Icon is FlyoutTile tile ? PanelSwatch.Of(tile) : new PanelSwatch(null, Slashed: true);
        Swatch(inner, P(PanelMetrics.Corner), colour);
        if (cell.Item.Checked == true)
        {
            PopupPaint.Stroke(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), P(PanelMetrics.Corner) + (2 * ring), ring, greyed ? theme.TextDisabled : theme.Text);
        }
        else if (!greyed && marks.Hot == hit)
        {
            PopupPaint.Stroke(dc, PopupPaint.Rect(r.X + (ring / 2), r.Y + (ring / 2), r.Width - ring, r.Height - ring), P(PanelMetrics.Corner) + ring, Hair, theme.TextSecondary);
        }
        if (Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner) + (2 * ring));
        }
    }

    /// <summary>
    /// §693 (the person, 2026-11-12: "the edited icon should be nicer designed and win11 style"): a palette holding the person's
    /// edits, WinUI's InfoBadge with an icon — a round badge of AccentFillColorDefault, the pencil in TextOnAccentFillColorPrimary —
    /// centred on <paramref name="centre"/>.
    /// </summary>
    private void ChangedBadge(Vector2 centre, bool greyed)
    {
        PopupPaint.Circle(dc, centre, P(PickerMetrics.Badge) / 2, Ink(greyed ? theme.AccentDisabled : theme.Accent));
        Glyph(ChangedGlyph, centre.X, centre.Y, PickerMetrics.BadgeGlyph, greyed ? theme.TextOnAccentDisabled : theme.TextOnAccent);
    }

    /// <summary>§682: an icon of the gallery: its glyph in a square lit under the pointer, ringed in the accent where it is chosen.</summary>
    private void GlyphTile(PickerCell cell, RectF r, bool greyed)
    {
        var hit = cell.Hit;
        Fill(r, P(PanelMetrics.Corner), SubtleOf(hit));
        if (cell.Item.Checked == true)
        {
            PopupPaint.Stroke(dc, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), P(PanelMetrics.Corner), P(2), Ink(greyed ? theme.AccentDisabled : theme.Accent));
        }
        var code = ((FlyoutTile)cell.Item.Icon!).Code;
        Glyph(code, r.X + (r.Width / 2), r.Y + (r.Height / 2), 20, greyed || !cell.Item.Enabled ? theme.TextDisabled : theme.Text);
        if (Focused(hit))
        {
            FocusRing(r, P(PanelMetrics.Corner));
        }
    }

    /// <summary>§670: a look's tile: the piece of the card, ringed in the accent where it is in force, lit under the pointer; its name under it.</summary>
    private void LookTile(PickerCell cell, RectF r, bool greyed)
    {
        var hit = cell.Hit;
        var piece = new RectF(r.X, r.Y, r.Width, r.Height - P(PickerMetrics.TileName));
        Fill(piece, P(PanelMetrics.Corner), SubtleOf(hit));
        var inset = P(PanelMetrics.Tight);
        // §675: a picture of what it draws: its room's ground and rim, the sample over it (LookPreviews), as a layout's tile's.
        if (cell.Item.Shows is not null)
        {
            var room = layout.PictureIn(cell);
            var pictured = room with { X = room.X + r.X - cell.Rect.X, Y = room.Y + r.Y - cell.Rect.Y };
            Fill(pictured, P(LookPreview.CornerDip), LookPreview.Ground(theme.Dark));
            Rim(pictured, P(LookPreview.CornerDip), theme.CardStroke);
        }
        else if (cell.Item.Icon is FlyoutTile tile)
        {
            Tile(tile, new RectF(piece.X + inset, piece.Y + inset, piece.Width - (2 * inset), piece.Height - (2 * inset)));
        }
        if (cell.Item.Checked == true)
        {
            PopupPaint.Stroke(dc, PopupPaint.Rect(piece.X, piece.Y, piece.Width, piece.Height), P(PanelMetrics.Corner), P(2), Ink(greyed ? theme.AccentDisabled : theme.Accent));
        }
        var name = cell.Item.Text;
        // §682 (the person, 2026-11-12: "in the pallette selector you should show if the palette is changed from its default with
        // a little icon"): a choice holding the person's edits (its mark, "Changed") a pencil after its name.
        var marked = cell.Item.Value?.Text is { Length: > 0 };
        var pencil = marked ? P(PickerMetrics.Badge) + P(PanelMetrics.Tight) : 0;
        var wide = MathF.Min(Measure(name, PanelMetrics.Caption), r.Width - pencil);
        var left = r.X + MathF.Round((r.Width - wide - pencil) / 2);
        var ink = greyed ? theme.TextDisabled : cell.Item.Checked == true ? theme.Text : theme.TextSecondary;
        Text(name, left, piece.Bottom + (P(PickerMetrics.TileName) / 2), PanelMetrics.Caption, ink, most: wide + 1);
        if (marked)
        {
            ChangedBadge(new Vector2(left + wide + P(PanelMetrics.Tight) + (P(PickerMetrics.Badge) / 2), piece.Bottom + (P(PickerMetrics.TileName) / 2)), greyed);
        }
        if (Focused(hit))
        {
            FocusRing(piece, P(PanelMetrics.Corner));
        }
    }

    /// <summary>
    /// §670: a picture of a look in <paramref name="r"/>: its colour, its pattern as the card draws it, its rim, a line of its
    /// text across it; None slashed — the popup's swatches' and tiles' drawing (Flyout.Pickers.cs), on the panel.
    /// </summary>
    private void Tile(FlyoutTile tile, RectF r)
    {
        var radius = P(PanelMetrics.Corner);
        if (tile.Slashed || tile.Color is null)
        {
            Swatch(r, radius, PanelSwatch.Of(tile));
            return;
        }
        var colour = tile.Color.Value;
        Fill(r, radius, Color.FromArgb(colour.A, colour.R, colour.G, colour.B));
        if (tile.Pattern is { } pattern)
        {
            var big = tile.CellDip > 0;
            var small = big ? layout.Scale : layout.Scale * Flyout.SwatchPatternScale;
            var (across, down) = Patterns.Period(pattern.Pattern, small);
            var middle = new Vector2(r.X + MathF.Round((r.Width - across) / 2), r.Y + MathF.Round((r.Height - down) / 2));
            PatternPaint.Fill(dc, pattern, small, PopupPaint.Rect(r.X, r.Y, r.Width, r.Height), middle, radius, big ? 1 : Flyout.SwatchPatternBoost);
        }
        if (tile.Ink is { } words)
        {
            var thick = MathF.Max(1, P(2));
            var inset = MathF.Round(r.Width * 0.25f);
            PopupPaint.Fill(dc, PopupPaint.Rect(r.X + inset, r.Y + MathF.Round((r.Height - thick) / 2), r.Width - (2 * inset), thick), thick / 2, Gfx.Ink(words.R / 255f, words.G / 255f, words.B / 255f, words.A / 255f));
        }
        var rim = tile.Rim is { } own ? Color.FromArgb(own.A, own.R, own.G, own.B) : theme.ControlStroke;
        Rim(r, radius, rim);
    }

    /// <summary>
    /// §670: a part of the colour picker in cell <paramref name="r"/>: the spectrum, or a track of the hue or the opacity, its thumb
    /// where the colour stands — the colour being dragged where it is this picker's (<see cref="PanelMarks.Mixing"/>).
    /// </summary>
    private void Mixer(PickerCell cell, RectF r, string key)
    {
        var mix = (FlyoutMix)cell.Item.Control!;
        var (colour, hsv) = marks.Mixing is { } held && held.Key == key ? (held.Color, held.Hsv) : (mix.Color, ColorMix.ToHsv(mix.Color));
        var travel = layout.MixTravel(mix.Part, r);
        Vector2 thumb;
        if (mix.Part == MixPart.Spectrum)
        {
            var radius = P(PanelMetrics.Corner);
            var hue = ColorMix.Pure(hsv.H);
            Fill(r, radius, Color.FromArgb(255, hue.R, hue.G, hue.B));
            Gradient(r, radius, horizontal: true, (0, Gfx.Ink(1, 1, 1, 1)), (1, Gfx.Ink(1, 1, 1, 0)));
            Gradient(r, radius, horizontal: false, (0, Gfx.Ink(0, 0, 0, 0)), (1, Gfx.Ink(0, 0, 0, 1)));
            Rim(r, radius, theme.ControlStroke);
            var (px, py) = ColorMix.SpectrumPoint(hsv, r.Width, r.Height);
            thumb = new Vector2(r.X + (float)px, r.Y + (float)py);
        }
        else
        {
            var tall = P(PickerMetrics.Track);
            var track = new RectF(r.X, r.Y + MathF.Round((r.Height - tall) / 2), r.Width, tall);
            if (mix.Part == MixPart.Hue)
            {
                const int stops = 7;
                var rainbow = new (float, DXGI_RGBA)[stops];
                for (var at = 0; at < stops; at++)
                {
                    var pure = ColorMix.Pure(at * ColorMix.MostHue / (stops - 1));
                    rainbow[at] = (at / (float)(stops - 1), Gfx.Ink(pure.R / 255f, pure.G / 255f, pure.B / 255f, 1));
                }
                Gradient(track, tall / 2, horizontal: true, rainbow);
                thumb = new Vector2(travel.X + (float)ColorMix.SliderPoint(hsv.H, travel.Width, ColorMix.MostHue), travel.Y);
            }
            else
            {
                var secondary = theme.TextSecondary;
                var ink = new Argb(Flyout.CheckerAlpha, (byte)MathF.Round(secondary.r * 255), (byte)MathF.Round(secondary.g * 255), (byte)MathF.Round(secondary.b * 255));
                Fill(track, tall / 2, theme.Ground);
                PatternPaint.Fill(dc, new Patterned(Pattern.Checks, ink), layout.Scale, PopupPaint.Rect(track.X, track.Y, track.Width, track.Height), new Vector2(track.X, track.Y), tall / 2);
                var (cr, cg, cb) = (colour.R / 255f, colour.G / 255f, colour.B / 255f);
                Gradient(track, tall / 2, horizontal: true, (0, Gfx.Ink(cr, cg, cb, 0)), (1, Gfx.Ink(cr, cg, cb, 1)));
                thumb = new Vector2(travel.X + (float)ColorMix.SliderPoint(ColorMix.Opacity(colour), travel.Width, ColorMix.MostOpacity), travel.Y);
            }
            Rim(track, tall / 2, theme.ControlStroke);
        }
        // WinUI's ColorPicker thumb: a ring of the ground round the colour, a hairline either side so it reads on any colour.
        var hot = marks.Hot == cell.Hit || marks.Pressed == cell.Hit;
        var outer = P(PickerMetrics.Thumb) / 2;
        PopupPaint.Circle(dc, thumb, outer, Ink(theme.ControlStroke));
        PopupPaint.Circle(dc, thumb, outer - Hair, Gfx.Ink(1, 1, 1, 1));
        var dot = ColorMix.ToArgb(hsv);
        var dotInk = mix.Part == MixPart.Opacity ? Gfx.Ink(colour.R / 255f, colour.G / 255f, colour.B / 255f, 1) : Gfx.Ink(dot.R / 255f, dot.G / 255f, dot.B / 255f, 1);
        if (mix.Part == MixPart.Hue)
        {
            var pure = ColorMix.Pure(hsv.H);
            dotInk = Gfx.Ink(pure.R / 255f, pure.G / 255f, pure.B / 255f, 1);
        }
        PopupPaint.Circle(dc, thumb, outer - P(hot ? 4 : 5), dotInk);
        if (Focused(cell.Hit))
        {
            var ring = new RectF(thumb.X - outer, thumb.Y - outer, 2 * outer, 2 * outer);
            FocusRing(ring, outer);
        }
    }

    /// <summary>§670: <paramref name="r"/> filled with a linear gradient along its width or its height, its corners round.</summary>
    private void Gradient(RectF r, float radius, bool horizontal, params (float At, DXGI_RGBA Ink)[] stops)
    {
        D2D1_GRADIENT_STOP* each = stackalloc D2D1_GRADIENT_STOP[stops.Length];
        for (var at = 0; at < stops.Length; at++)
        {
            each[at] = new D2D1_GRADIENT_STOP { position = stops[at].At, color = stops[at].Ink };
        }
        ID2D1GradientStopCollection* collection = null;
        ID2D1LinearGradientBrush* brush = null;
        try
        {
            if (dc->CreateGradientStopCollection(each, (uint)stops.Length, D2D1_GAMMA.D2D1_GAMMA_2_2, D2D1_EXTEND_MODE.D2D1_EXTEND_MODE_CLAMP, &collection).FAILED)
            {
                return;
            }
            var along = new D2D1_LINEAR_GRADIENT_BRUSH_PROPERTIES
            {
                startPoint = new D2D_POINT_2F(r.X, r.Y),
                endPoint = horizontal ? new D2D_POINT_2F(r.Right, r.Y) : new D2D_POINT_2F(r.X, r.Bottom),
            };
            if (dc->CreateLinearGradientBrush(&along, null, collection, &brush).FAILED)
            {
                return;
            }
            var shape = new D2D1_ROUNDED_RECT { rect = new D2D_RECT_F(r.X, r.Y, r.Right, r.Bottom), radiusX = radius, radiusY = radius };
            dc->FillRoundedRectangle(&shape, (ID2D1Brush*)brush);
        }
        finally
        {
            if (brush != null)
            {
                brush->Release();
            }
            if (collection != null)
            {
                collection->Release();
            }
        }
    }

    /// <summary>
    /// §670: an inline picker's head's end: what it is set to — the colour's swatch or the choice's picture, its words (a
    /// font's in its face) — before its chevron, open or shut, as a SettingsExpander's header content stands.
    /// </summary>
    private void PickerHead(PanelLine line, float right, float mid, float start, bool greyed)
    {
        var row = line.Row;
        Chevron(layout.StandsOpen(row) ? PanelGlyphs.Up : PanelGlyphs.Down, right, mid, greyed ? theme.TextDisabled : theme.Text);
        var end = start - P(PanelMetrics.Label);
        var most = P(PanelMetrics.ControlWide);
        var said = row.Value ?? "";
        float x;
        using (gfx.Wearing(row.ValueFace ?? gfx.Typeface))
        {
            x = TextBefore(said, end, mid, PanelMetrics.Body, greyed ? theme.TextDisabled : theme.TextSecondary, most);
        }
        var side = P(PanelMetrics.Icon + 4);
        var picture = new RectF(x - P(PanelMetrics.Gap) - side, mid - (side / 2), side, side);
        if (row.Swatch is { } tile && PanelPickers.Colours(row.Item))
        {
            Swatch(picture, P(PanelMetrics.Corner), PanelSwatch.Of(tile));
        }
        else if (row.ValueIcon is FlyoutTile chosen)
        {
            Tile(chosen, picture);
        }
        else if (row.ValueIcon is { } icon)
        {
            Icon(icon, picture.X + (side / 2), mid, greyed ? theme.TextDisabled : theme.Text);
        }
    }
}
