// menus: a colour, an icon, a target, a time of day and a length as rows of a widget's menu (2026-10-19,
// the person: "inline every setting of every bar widget and folder widget into its edit menu"; docs\
// widgets.md §224). The rows are the core's (Karpo.Core\OptionPickers.cs), read from the widget and
// set there as Settings sets them; this file is how a menu shows them (OptionMenu.Rows hands it these
// kinds), and the menu draws the tiles (Flyout.Pickers.cs). Every row is one row of the menu, a line of
// tiles too (§240, the person: "all of them should not be bigger than a single entry"):
//
//  - **A colour** is a row with a swatch of the colour in force in its icon's place (§509; until then at its end before
//    the chevron, §228) — quieter where it is what the row comes to unset, slashed for None, over a checker where it is
//    see-through — and in words at its end what it is ("Default (#202020)", "None", "#1E3A5F, 80%", §518), opening its
//    picker, one level, the same wherever a colour is chosen (§523): its "Default (…)" ticked as a menu ticks its choice;
//    the recent colours and the page's grid as swatches in lines of a row each, a group ruled off from the one before
//    (§411: no headings in a settings menu), the one in force ringed — §627 six to a line, one line of recent colours
//    and two of presets (ColorPalette) —; then, a column beside them (Flyout.Columns.cs; §523 to §626 ruled off under
//    them), a colour of one's own mixed right there (Flyout.Mixer.cs): the spectrum, the hue, the opacity where the value
//    takes one (OptionRow.Alpha), and the hex box with a swatch of what it says. A swatch picked sets it and the menu stays (§219), back on the row it opened from,
//    a colour being one choice (§392); a colour mixed is looked at as it is dragged and set as it is let go, the level
//    staying (Dock.MenuPickers.cs). §224 to §522 had "Custom color…", Windows' own colour dialog, and §459 to §522 the
//    opacity a stepper of tens under the swatches.
//  - **An icon** is a row with the icon in force at its end before the chevron (§228), opening a
//    picker as Windows' emoji and icon pickers are (§249, the person: "improve the icon picker"): a
//    search field at its top, which has the keys as it opens; under it the cross as the first tile,
//    which takes the icon chosen away (§306: one way to clear it in every picker), then the whole
//    gallery (Karpo.Core\MenuIcons.cs) in one grid with no headings (§306), 40 DIP tiles of a 20 DIP
//    glyph, lit under the pointer, the one in force ringed in the accent and scrolled into view, each
//    named in its tooltip. Typing filters the tiles by their words, the matches in place of
//    the grid; the level keeps its height meanwhile and scrolls (Flyout.Pickers.cs). §224 to §245 had
//    48 icons a row tall and the gallery only as search results under them.
//  - **In a submenu already** (a folder's face's) the same: a colour's or an icon's own submenu, a
//    third level (§226; §224's heading and tiles inline in the face's submenu are gone).
//  - **A target** is "Browse…" over the field of what a pin opens (§292: one row, "What it opens", its
//    name at its end; Karpo.Core\OptionFields.OfPinTarget); a click opens Windows' open dialog
//    (TargetDialog), a file or a folder — §425 a pin's Folder a folder alone, its File a file alone —, and the menu
//    comes back after it.
//  - **A time of day or a length** is a row saying it at its end ("7:30 AM", "1 h 25 min"), opening a
//    level of a row for each part, its name at its start and a stepper at its end: "Hours − 07 +",
//    "Minutes − 30 +" and, where the clock Karpo shows is a 12-hour one, "AM or PM" with a button of
//    its own; "Hours − 1 h +", "Minutes − 25 min +" for a length. A step sets it and the menu stays,
//    showing it as it is now; a button held steps again and again, as Windows' repeat buttons do.
//    §224 to §239 had its name as a heading and the steppers side by side in one row under it: two
//    rows' height, and a row of a kind of its own (FlyoutSpin); it is the menu's one stepper now.

using Karpo.Core;

namespace Karpo.Dock;

/// <summary>A menu's rows for the core's pickers; see the top of the file.</summary>
internal static class MenuPickers
{
    // Segoe Fluent Icons.
    private const string GlyphOpen = "";
    private const string GlyphPin = "";

    /// <summary>A swatch's side in a colour's grid, in a cell a row tall (Flyout.TileCellDip); an icon's glyph, in a cell of its own (§249).</summary>
    internal const float SwatchDip = 24;
    internal const float IconTileDip = 20;

    /// <summary>§249: an icon's cell, square, in DIP: Windows' emoji picker's.</summary>
    internal const float IconCellDip = 40;

    /// <summary>
    /// The most swatches and icons to a line: §627 a colour's six (ColorPalette.Columns: its swatches' column beside its mixer
    /// in its picker of 400 DIP, PanelMetrics.PickerWide; §613's twelve across the whole picker, Windows' grid's eight before),
    /// and as many icons as the menu holds.
    /// </summary>
    internal const int SwatchesAcross = ColorPalette.Columns;
    internal const int IconsAcross = 8;

    /// <summary>
    /// §634: a look's tile (a pattern's, OptionMenu.TileOf): its square, its cell, three to a line — None and the eight patterns
    /// three lines of three, the grid 3 × 72 DIP wide, in a popup a value's box wide (PanelMetrics.ControlWide, 240 DIP).
    /// </summary>
    internal const float LookTileDip = 60;

    internal const float LookCellDip = 72;
    internal const int LookTilesAcross = 3;

    /// <summary>Whether a row of <paramref name="kind"/> is shown here rather than by OptionMenu.</summary>
    public static bool Shows(OptionRowKind kind) => kind is OptionRowKind.Color or OptionRowKind.Glyph or OptionRowKind.Target or OptionRowKind.Time or OptionRowKind.Duration;

    /// <summary>
    /// The rows for <paramref name="option"/>, their commands from <paramref name="next"/> (OptionMenu's:
    /// what a command does, to which setting, with what value; see the top of the file).
    /// </summary>
    public static List<FlyoutItem> Rows(OptionRow option, Func<OptionPickKind, string, int, uint> next)
    {
        var key = option.Key;
        switch (option.Kind)
        {
            case OptionRowKind.Color:
            {
                // The row first, so its command stays the same however many tiles it has.
                var row = next(OptionPickKind.None, key, 0);
                var children = Tiles(option, next, SwatchDip, SwatchesAcross);
                // §523: a colour of one's own, ruled off: the spectrum, the hue, the opacity, the hex box; §627 a column beside the
                // swatches (Flyout.Columns.cs), no longer under them.
                children.Add(new FlyoutItem(0, "", Separator: true) { Column = true });
                children.AddRange(Mixer(option, next));
                // §509: the colour in force in the row's icon's place (Swatch), and what it is in words at its end (§228): the
                // colour's name, "None" (§311), or — greyed — what an unset one comes to, "Default (…)" (§518).
                return [new FlyoutItem(row, option.Title, Swatch(option), Children: children, Value: new FlyoutValue(Said(option), Implied: Inherits(option)), PickOne: true)];
            }
            case OptionRowKind.Glyph:
            {
                var row = next(OptionPickKind.None, key, 0);
                // §249: the field first, filtering the tiles under it (Flyout.Fields.cs); it keeps nothing of its own.
                List<FlyoutItem> children = [new FlyoutItem(next(OptionPickKind.None, key, 0), option.Placeholder, Control: new FlyoutField("", option.Placeholder, Filters: true, NoMatch: S.SettingsIconPickerNoMatch))];
                children.AddRange(Tiles(option, next, IconTileDip, IconsAcross, IconCellDip));
                // What it is drawn with at the row's end: the icon chosen, else its default (never the cross).
                var shown = option.Text.Length > 0 ? option.Text : option.Fallback.Length > 0 ? option.Fallback : GlyphPin;
                // §528: several widgets selected wearing several icons: "Mixed".
                return [new FlyoutItem(row, option.Title, OptionMenu.IconOf(option.Glyph), Children: children, Value: option.Mixed ? new FlyoutValue(S.SettingsValueMixed) : new FlyoutValue(Icon: new FlyoutIcon.Glyph(shown)), PickOne: true)];
            }
            // "Browse…": over the field of what a pin opens, which says the path (§292).
            case OptionRowKind.Target:
                return [new FlyoutItem(next(OptionPickKind.Browse, key, 0), option.Title, OptionMenu.IconOf(option.Glyph) ?? new FlyoutIcon.Glyph(GlyphOpen))];
            case OptionRowKind.Time or OptionRowKind.Duration:
            {
                // The row first, so its command stays the same; its parts its own level, a row each.
                var row = next(OptionPickKind.None, key, 0);
                var time = option.Kind == OptionRowKind.Time;
                var parts = time ? ClockParts(option, next) : LengthParts(option, next);
                return [new FlyoutItem(row, option.Title, OptionMenu.IconOf(option.Glyph), Children: parts, Value: new FlyoutValue(option.Mixed ? S.SettingsValueMixed : time ? ClockSaid(option) : LengthSaid(option.Value)))];
            }
            default:
                return [];
        }
    }

    /// <summary>
    /// §523: a colour row's own colour, the rows of its picker under the swatches: the spectrum, the hue, the opacity where the
    /// value takes an alpha, each a pick of <see cref="OptionPickKind.Mix"/> (the colour the menu says as it is dragged), and the
    /// hex box, a field kept as a colour typed (<see cref="OptionPickKind.Field"/>, FieldPicks.KeepRow), its swatch the colour its
    /// words say (Flyout.Mixer.cs).
    /// </summary>
    internal static IEnumerable<FlyoutItem> Mixer(OptionRow option, Func<OptionPickKind, string, int, uint> next)
    {
        var key = option.Key;
        var shown = Mixed(option);
        List<FlyoutItem> rows =
        [
            new(next(OptionPickKind.Mix, key, 0), S.DockEditColorSpectrum, Control: new FlyoutMix(MixPart.Spectrum, shown)),
            new(next(OptionPickKind.Mix, key, 0), S.DockEditColorHue, Control: new FlyoutMix(MixPart.Hue, shown)),
        ];
        if (option.Alpha)
        {
            rows.Add(new(next(OptionPickKind.Mix, key, 0), S.SettingsAppearanceStylesOpacity, Control: new FlyoutMix(MixPart.Opacity, shown)));
        }
        rows.Add(new(next(OptionPickKind.Field, key, 0), S.DockEditColorHex,
            Control: new FlyoutField(ColorMix.Hex(shown, option.Alpha), option.Alpha ? "#RRGGBBAA" : "#RRGGBB", Longest: 9) { Hex = true }));
        return rows;
    }

    /// <summary>
    /// §523: the colour a row's picker mixes from: the one in force; for None, or where none is said, Windows' blue — opaque,
    /// so nothing mixed from it is unseen. Opaque where the value takes no alpha.
    /// </summary>
    internal static Argb Mixed(OptionRow option)
    {
        var start = option.Color is { A: > 0 } color ? color : ColorPalette.Blue;
        return option.Alpha ? start : start with { A = 255 };
    }

    /// <summary>
    /// §509: whether a colour row wears what it comes to unpicked: its first choice, Automatic ("Default (…)", §518: an
    /// account's colour, PartColours).
    /// </summary>
    internal static bool Inherits(OptionRow option) => option.Chosen == 0 && option.Tiles is [{ Plain: true, Glyph: OptionGlyphs.Automatic }, ..];

    /// <summary>
    /// §509: a colour row's swatch in its icon's place (Flyout.Pickers.cs, the one painter of a row's swatch): the colour in
    /// force; quieter where it is what the row comes to unpicked (an account's Automatic); empty with a slash across it for None.
    /// </summary>
    internal static FlyoutTile Swatch(OptionRow option)
    {
        // §528: several widgets selected wearing several colours: the swatch says so.
        if (option.Mixed)
        {
            return new FlyoutTile(null, Cell: false) { Mixed = true };
        }
        var inherits = Inherits(option);
        var shown = option.Color;
        var none = shown is { A: 0 } || !inherits && option.Chosen >= 0 && option.Chosen < option.Tiles.Count && option.Tiles[option.Chosen].Plain;
        return new FlyoutTile(none ? null : shown, Cell: false) { Slashed = none, Quiet = inherits };
    }

    /// <summary>
    /// §509: what a colour row says it is at its end, and to a screen reader: unset "Default (…)" with what it comes to
    /// (§518), "None", the colour's name where it is one of the row's ("Blue"), else its value ("#1E3A5F, 80%").
    /// </summary>
    internal static string Said(OptionRow option)
    {
        if (option.Mixed)
        {
            return S.SettingsValueMixed;
        }
        var chosen = option.Chosen >= 0 && option.Chosen < option.Tiles.Count ? option.Tiles[option.Chosen] : null;
        return chosen is { Plain: true } || chosen is not null && chosen.Color == option.Color ? chosen.Name
            : option.Color is { } color ? OptionPickers.Say(color) : "";
    }

    /// <summary>
    /// A colour's or an icon's choices: a colour's plain one (Automatic) a row ticked as a menu's
    /// choice, the rest tiles of <paramref name="side"/> DIP, <paramref name="across"/> to a line at
    /// most, each group ruled off from the one before (§411), the one in force ringed. An icon's (<paramref name="cell"/>
    /// its cells' side, §249) are all tiles in one grid, the cross the first (§306), each with the words
    /// it is found by.
    /// </summary>
    private static List<FlyoutItem> Tiles(OptionRow option, Func<OptionPickKind, string, int, uint> next, float side, int across, float cell = 0)
    {
        var items = new List<FlyoutItem>();
        string? group = null;
        for (var at = 0; at < option.Tiles.Count; at++)
        {
            var tile = option.Tiles[at];
            var id = next(OptionPickKind.Set, option.Key, at);
            if (tile.Plain && cell <= 0)
            {
                // §396: "Default (…)" (§518) with a swatch of what it comes to at its end.
                var inherits = tile.Color is { A: > 0 } above ? new FlyoutValue(Icon: new FlyoutTile(above, Cell: false)) : null;
                items.Add(new FlyoutItem(id, tile.Name, OptionMenu.IconOf(tile.Glyph), Checked: at == option.Chosen, Stays: true, Value: inherits));
                continue;
            }
            // §411 (R10): no headings in a settings menu; a group of swatches is ruled off from the one before.
            if (tile.Group != group && tile.Group.Length > 0 && items.Count > 0)
            {
                items.Add(new FlyoutItem(0, "", Separator: true));
            }
            group = tile.Group;
            items.Add(new FlyoutItem(id, tile.Name, new FlyoutTile(tile.Color, tile.Glyph, side, Across: across, CellDip: cell), Checked: at == option.Chosen, Words: tile.Words, Stays: true));
        }
        return items;
    }

    /// <summary>A time of day's rows: the hour's stepper, the minute's, and before or after noon on a 12-hour clock.</summary>
    private static List<FlyoutItem> ClockParts(OptionRow option, Func<OptionPickKind, string, int, uint> next)
    {
        var key = option.Key;
        List<FlyoutItem> parts =
        [
            Part(S.DockEditHours, OptionGlyphs.Clock, TimeSteps.HourText(option.Value, option.TwelveHour), next(OptionPickKind.Step, key, -TimeSteps.Hour), next(OptionPickKind.Step, key, TimeSteps.Hour), true, true,
                FlyoutControl.Stepper.Over(0, 0, 23, hour => TimeSteps.HourText(hour * 60, option.TwelveHour))),
            Part(S.DockEditMinutes, OptionGlyphs.Seconds, TimeSteps.MinuteText(option.Value), next(OptionPickKind.Step, key, -TimeSteps.Minute), next(OptionPickKind.Step, key, TimeSteps.Minute), true, true,
                FlyoutControl.Stepper.Over(0, 0, 59, TimeSteps.MinuteText)),
        ];
        if (option.TwelveHour)
        {
            // §249: the sun before noon, the moon after.
            var noon = next(OptionPickKind.Step, key, TimeSteps.Noon);
            var glyph = TimeSteps.Afternoon(option.Value) ? OptionGlyphs.Moon : OptionGlyphs.Sun;
            parts.Add(new FlyoutItem(noon, S.DockEditMeridiem, new FlyoutIcon.Glyph(glyph), Control: new FlyoutControl.Stepper(Meridiem(option), noon, noon, true, true, S.DockEditMeridiem, S.DockEditMeridiem, Switch: true) { Range = [option.Morning, option.Evening] }));
        }
        return parts;
    }

    /// <summary>A length's rows: its hours' stepper and its minutes', each greyed where the length is at its end.</summary>
    private static List<FlyoutItem> LengthParts(OptionRow option, Func<OptionPickKind, string, int, uint> next)
    {
        var key = option.Key;
        var (value, least, most) = (option.Value, option.Least, option.Most);
        return
        [
            Part(S.DockEditHours, OptionGlyphs.Clock, S.WidgetCommonHours(value / 60), next(OptionPickKind.Step, key, -TimeSteps.Hour), next(OptionPickKind.Step, key, TimeSteps.Hour),
                TimeSteps.CanStep(value, -TimeSteps.Hour, least, most), TimeSteps.CanStep(value, TimeSteps.Hour, least, most), FlyoutControl.Stepper.Over(value / 60, least / 60, most / 60, S.WidgetCommonHours)),
            Part(S.DockEditMinutes, OptionGlyphs.Seconds, S.WidgetCommonMinutes(value % 60), next(OptionPickKind.Step, key, -TimeSteps.Minute), next(OptionPickKind.Step, key, TimeSteps.Minute),
                TimeSteps.CanStep(value, -TimeSteps.Minute, least, most), TimeSteps.CanStep(value, TimeSteps.Minute, least, most), FlyoutControl.Stepper.Over(value % 60, 0, 59, S.WidgetCommonMinutes)),
        ];
    }

    /// <summary>
    /// A part's row: its <paramref name="name"/> and <paramref name="glyph"/>, and its stepper at its end, the row's command its "−";
    /// §456: <paramref name="range"/> every text its value may say, its slot as wide as the widest.
    /// </summary>
    private static FlyoutItem Part(string name, string glyph, string value, uint fewer, uint more, bool canFewer, bool canMore, IReadOnlyList<string> range) =>
        new(fewer, name, new FlyoutIcon.Glyph(glyph), Control: new FlyoutControl.Stepper(value, fewer, more, canFewer, canMore, S.DockEditLessOf(name), S.DockEditMoreOf(name)) { Range = range });

    /// <summary>Before or after noon, in the clock's words.</summary>
    private static string Meridiem(OptionRow option) => TimeSteps.Afternoon(option.Value) ? option.Evening : option.Morning;

    /// <summary>A time of day as its row says it: "07:30", "7:30 AM" on a 12-hour clock.</summary>
    internal static string ClockSaid(OptionRow option) =>
        $"{TimeSteps.HourText(option.Value, option.TwelveHour)}:{TimeSteps.MinuteText(option.Value)}{(option.TwelveHour ? $" {Meridiem(option)}" : "")}";

    /// <summary>A length as its row says it: "25 min", "1 h", "1 h 25 min".</summary>
    internal static string LengthSaid(int minutes) => (minutes / 60, minutes % 60) switch
    {
        (0, var rest) => S.WidgetCommonMinutes(rest),
        (var hours, 0) => S.WidgetCommonHours(hours),
        var (hours, rest) => $"{S.WidgetCommonHours(hours)} {S.WidgetCommonMinutes(rest)}",
    };
}
