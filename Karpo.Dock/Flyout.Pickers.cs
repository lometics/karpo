// menus: the pickers' parts of a menu (2026-10-19, docs\widgets.md §224): tiles, and a stepper held down.
// What they stand for is MenuPickers' (MenuPickers.cs); this is how the menu lays them out, draws them,
// and takes the pointer and the keys on them.
//
//  - **Tiles** (FlyoutTile): rows of a menu drawn as a square each, side by side, as many to a line as
//    the menu's width holds and spread across it from where a row starts to where it ends (§302: no
//    margin of their own beside the rows' inset), a run of them wrapping into lines as Windows' accent
//    colour grid does (Settings > Personalization > Colors): a colour's swatch, 24 DIP with its
//    corners 4 DIP round and a hairline round it; an icon's glyph, 20 px in its tile. Each cell is a
//    menu row's height square, 32 DIP (TileCellDip), so a line of them is one row of the menu (§240,
//    the person: "all of them should not be bigger than a single entry"). Each is a row in all but its drawing — lit under the pointer and pressed as
//    a row is, chosen by a click, Enter or Space, its name what a screen reader says — and the one in
//    force is ringed: 2 DIP round the swatch, 2 DIP off it, in the words' colour; an icon's tile in the
//    accent. Left and Right walk the line, Up and Down go to the tile above or below, and from a row
//    over or under a run the keys land on the first tile of its nearest line, as onto the menu's row
//    of buttons. A FlyoutTile not a cell (Cell false) is a row's own small swatch where its icon stands, the one painter of
//    every such swatch (§509: a colour row's — quieter for what an unset colour comes to, slashed for None, over a checker
//    where it is see-through —, a style's card, a pattern's).
//  - **An icon picker** (§249): its tiles are cells of their own, 40 DIP (FlyoutTile.CellDip), as
//    Windows' emoji picker's, each named in a tooltip as the pointer or the keys rest on it (an icon
//    button's, Flyout.Primary.cs, on any level). Its head and its field (Flyout.Fields.cs, a filter) stand
//    at its top while the tiles scroll under them, in a configuration menu's one height (Flyout.Inline.cs, §288), and it opens on the tile in force (ChosenTileInView).
//  - **A look's tiles** (§634, a Pattern's: OptionMenu.TileOf): cells of their own, 72 DIP, three to a line, each a 60 DIP
//    piece of the card it would be drawn on — its colour, its rim, the pattern as the card draws it; None slashed —, named in
//    a tooltip, the one in force ringed in the accent.
//  - **A stepper held** (FlyoutControl.Stepper, Flyout.Controls.cs): a press on its button steps at
//    once, and held steps again after 400 ms and every 80 ms while the pointer stays on it (Windows'
//    RepeatButton's delay and interval), each step a FlyoutEventKind.Adjust; a switch (AM/PM) steps once.
//    §224 to §239 had a time's or a length's steppers side by side in one row under its name, a row of
//    steppers of its own kind (FlyoutSpin); since §240 each part is a row of its own, its name at its
//    start and a stepper at its end, every stepper of every menu the one control.

using System.Numerics;
using Karpo.Core;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Windows.UI.Composition;
using static TerraFX.Interop.Windows.Windows;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>
/// menus: a row drawn as a tile (<see cref="Cell"/>): a swatch of <paramref name="Color"/>, or
/// <paramref name="Code"/> (a glyph), <paramref name="SideDip"/> square in its cell, at most
/// <paramref name="Across"/> to a line (Windows' grid's eight colours); or, not a cell, a row's small
/// swatch in its icon's place — an empty one for Automatic (null). Its cell is a menu row's height
/// square, or <paramref name="CellDip"/> where it has one of its own (an icon picker's, §249). Flyout.Pickers.cs.
/// </summary>
public sealed record FlyoutTile(Argb? Color = null, string Code = "", float SideDip = 24, bool Cell = true, int Across = 8, float CellDip = 0) : FlyoutIcon
{
    /// <summary>§429: a row's swatch's rim, a style's outline; null for the menu's own hairline.</summary>
    public Argb? Rim { get; init; }

    /// <summary>§429: a short line across a row's swatch, a style's text; null for none.</summary>
    public Argb? Ink { get; init; }

    /// <summary>§462: the pattern on a row's swatch, a style's part's; null for none.</summary>
    public Patterned? Pattern { get; init; }

    /// <summary>§509: a row's swatch of no colour (None): empty, a slash across it.</summary>
    public bool Slashed { get; init; }

    /// <summary>§509: a row's swatch of what a colour left unset comes to: smaller, quieter than one set.</summary>
    public bool Quiet { get; init; }

    /// <summary>§528: a row's swatch of several widgets selected wearing several colours: empty, a dash across it, as a check box's third state.</summary>
    public bool Mixed { get; init; }
}

public sealed unsafe partial class Flyout
{
    /// <summary>menus: a tile's cell, square, in DIP: a menu row's height, so a line of tiles is one row (§240).</summary>
    internal const float TileCellDip = 32;

    /// <summary>§462: how much stronger a pattern is drawn on a row's swatch than on its card (<see cref="PatternPaint.Fill"/>), and how much smaller.</summary>
    internal const float SwatchPatternBoost = 2.5f, SwatchPatternScale = 0.6f;

    /// <summary>§509: how far a quiet swatch (an unset colour's) stands in from its slot on each side, of the slot's side.</summary>
    internal const float QuietSwatchInset = 0.1875f;

    /// <summary>§509: the checker under a see-through swatch: the quieter text's colour at this alpha, in cells of 4 px at 100 %.</summary>
    internal const byte CheckerAlpha = 96;

    /// <summary>menus: an icon tile's glyph, in px at 100 %.</summary>
    internal const float TileGlyphPx = 20;

    /// <summary>menus: a held stepper's first repeat and the rest, in ms (Windows' RepeatButton).</summary>
    internal const uint SpinDelayMs = 400;
    internal const uint SpinIntervalMs = 80;

    private const nuint StepperTimer = 8;

    // The stepper's button held down, by the id it sends; 0 for none.
    private uint stepperHeld;

    /// <summary>menus: whether <paramref name="item"/> is drawn as a tile.</summary>
    internal static bool IsTile(FlyoutItem item) => item.Icon is FlyoutTile { Cell: true } && !item.Header && !item.Separator && !item.Primary;

    /// <summary>menus: where a tile stands in its run: its column and line, how many columns the run has, its cell's side in px, and whether it ends a line.</summary>
    internal readonly record struct TileSpot(int Column, int Line, int Columns, float Cell, bool EndsLine);

    /// <summary>
    /// menus: where each tile of <paramref name="items"/> stands (default for every other row): each run
    /// of tiles of one side in as many columns as <paramref name="room"/> px holds, up to its
    /// <see cref="FlyoutTile.Across"/>, a line after another, each line <paramref name="cell"/> px tall
    /// (a menu row's), or its tiles' own cell at <paramref name="scale"/> (<see cref="FlyoutTile.CellDip"/>);
    /// the last of a line, or of the run, ends it.
    /// </summary>
    internal static TileSpot[] TileSpots(IReadOnlyList<FlyoutItem> items, float room, float cell, float scale = 1) =>
        TileSpots(items, _ => room, cell, scale);

    /// <summary>§627: <see cref="TileSpots(IReadOnlyList{FlyoutItem}, float, float, float)"/>, each run in the room of its column (<paramref name="room"/> of its first tile).</summary>
    internal static TileSpot[] TileSpots(IReadOnlyList<FlyoutItem> items, Func<int, float> room, float cell, float scale = 1)
    {
        var spots = new TileSpot[items.Count];
        var at = 0;
        while (at < items.Count)
        {
            if (!IsTile(items[at]))
            {
                at++;
                continue;
            }
            var first = (FlyoutTile)items[at].Icon!;
            var (side, own) = (first.SideDip, first.CellDip);
            var square = own > 0 ? Round(own * scale) : cell;
            var columns = Math.Clamp((int)MathF.Floor(room(at) / square), 1, Math.Max(1, first.Across));
            var end = at;
            while (end < items.Count && IsTile(items[end]) && ((FlyoutTile)items[end].Icon!) is var next && next.SideDip == side && next.CellDip == own)
            {
                end++;
            }
            for (var i = at; i < end; i++)
            {
                var n = i - at;
                spots[i] = new TileSpot(n % columns, n / columns, columns, square, n % columns == columns - 1 || i == end - 1);
            }
            at = end;
        }
        return spots;
    }

    /// <summary>§249: whether <paramref name="items"/> are an icon picker's level: a field filtering tiles (<see cref="FlyoutField.Filters"/>).</summary>
    internal static bool IsPicker(IReadOnlyList<FlyoutItem> items) => items.Any(item => item.Control is FlyoutField { Filters: true });

    /// <summary>
    /// §249: the tile in force (ringed) scrolled into view, in the middle of the level where it was out
    /// of it, as a picker opens on what is chosen; §556 a filtered list's row in force too (a font's), the
    /// keys staying in its filter.
    /// </summary>
    internal void ChosenTileInView()
    {
        var filtered = IsPicker(rows.Select(row => row.Item).ToList());
        var at = rows.FindIndex(row => (IsTile(row.Item) || (filtered && Listed(row.Item))) && row.Item.Checked == true);
        if (at < 0)
        {
            return;
        }
        var bounds = rows[at].Bounds;
        if (bounds.Y < scroll + pinnedH || bounds.Y + bounds.H > scroll + viewport.H)
        {
            ScrollQuietly(bounds.Y + (bounds.H / 2) - ((viewport.H + pinnedH) / 2));
        }
    }

    /// <summary>
    /// §249: whether <paramref name="item"/> says its name in a tooltip when lit: an icon's tile, whose name its glyph alone does
    /// not say; §634 a look's (a pattern's), whose picture does not either.
    /// </summary>
    internal static bool TipsTile(FlyoutItem item) => IsTile(item) && item.Icon is FlyoutTile { Code.Length: > 0 } or FlyoutTile { CellDip: > 0 };

    /// <summary>
    /// menus: a tile's fill, its cell less a pixel of air, its line's top at <paramref name="top"/>:
    /// a line spread across the rows' own <paramref name="room"/> px from <paramref name="left"/>, its
    /// first fill where a row's starts, its last where a row's ends, the rest evenly between (§302,
    /// the person: "just the normal padding that all the other selectors have").
    /// </summary>
    internal static Area TileFill(TileSpot spot, float left, float room, float top, float scale)
    {
        var air = MathF.Round(scale);
        var side = spot.Cell - (2 * air);
        var x = spot.Columns > 1 ? left + MathF.Round(spot.Column * (room - side) / (spot.Columns - 1)) : left;
        return new Area(x, top + air, side, side);
    }

    /// <summary>menus: the tile beside row <paramref name="from"/> in its line, <paramref name="by"/> along; where it is at the line's end.</summary>
    private int AlongTiles(int from, int by)
    {
        var next = from + by;
        return next >= 0 && next < rows.Count && IsTile(rows[next].Item) && rows[next].Bounds.Y == rows[from].Bounds.Y ? next : from;
    }

    /// <summary>
    /// menus: from row <paramref name="from"/>, which isn't a tile, onto tile <paramref name="best"/>'s
    /// line: its first tile, as the keys come onto a run of them.
    /// </summary>
    private int IntoTiles(int from, int best)
    {
        if (best < 0 || from < 0 || !IsTile(rows[best].Item) || IsTile(rows[from].Item))
        {
            return best;
        }
        var line = rows[best].Bounds.Y;
        return rows.FindIndex(row => IsTile(row.Item) && row.Bounds.Y == line);
    }

    /// <summary>
    /// menus: <paramref name="tile"/> drawn on <paramref name="row"/>: a cell's square in the middle of
    /// <paramref name="fill"/>, ringed where <paramref name="chosen"/>; a row's small swatch at
    /// <paramref name="at"/>, <paramref name="side"/> px.
    /// </summary>
    private void DrawTile(Compositor c, ContainerVisual into, Row row, FlyoutTile tile, Area fill, Vector2 at, float side, bool chosen, bool enabled)
    {
        T Keep<T>(T value)
            where T : IDisposable => Own(value);
        float Px(float dip) => MathF.Round(dip * scale);
        var hairline = Px(1);
        ShapeVisual Box(Vector2 where, Vector2 size, float radius, Color? body, Color? rim, float thick)
        {
            var visual = Keep(c.CreateShapeVisual());
            visual.Size = size;
            visual.Offset = new Vector3(where, 0);
            var box = Keep(c.CreateRoundedRectangleGeometry());
            box.Offset = new Vector2(thick / 2);
            box.Size = size - new Vector2(thick);
            box.CornerRadius = new Vector2(radius);
            var shape = Keep(c.CreateSpriteShape(box));
            if (body is { } inside)
            {
                shape.FillBrush = Keep(c.CreateColorBrush(inside));
            }
            if (rim is { } edge)
            {
                shape.StrokeBrush = Keep(c.CreateColorBrush(edge));
                shape.StrokeThickness = thick;
            }
            visual.Shapes.Add(shape);
            if (!enabled)
            {
                visual.Opacity = 0.4f;
            }
            into.Children.InsertAtTop(visual);
            row.Parts.Add((visual, visual.Offset));
            return visual;
        }
        var outline = theme.ControlStroke;
        if (!tile.Cell)
        {
            // A row's own swatch, the one painter of every swatch in an icon's place: the colour in force, or an empty square
            // for Automatic. §429: a style's, its rim its outline where it outlines, and a line of its text across it, as a card
            // with words on it. §509: a colour row's in its icon's place — quieter (smaller) where it is what an unset colour
            // comes to, empty with a slash across it for no colour, over a checker where it is see-through so its opacity reads.
            var (whole, slot) = (side, at);
            if (tile.Quiet)
            {
                var inset = MathF.Round(side * QuietSwatchInset);
                (side, at) = (side - (2 * inset), at + new Vector2(inset));
            }
            var (inner, round) = (side - (2 * hairline), MathF.Max(0, Px(3) - hairline));
            // A pattern drawn inside the swatch's rim, a tile's middle on the swatch's.
            void Sheet(Patterned pattern, float small, float boost)
            {
                if (gfx is null)
                {
                    return;
                }
                var (across, down) = Patterns.Period(pattern.Pattern, small);
                var middle = new Vector2(MathF.Round((inner - across) / 2), MathF.Round((inner - down) / 2));
                var sprite = Keep(c.CreateSpriteVisual());
                sprite.Brush = Keep(c.CreateSurfaceBrush(Keep(gfx.Draw(inner, inner, dc =>
                    PatternPaint.Fill(dc, pattern, small, new D2D_RECT_F(0, 0, inner, inner), middle, round, boost)))));
                sprite.Size = new Vector2(inner);
                sprite.Offset = new Vector3(at.X + hairline, at.Y + hairline, 0);
                sprite.Opacity = enabled ? 1 : 0.4f;
                into.Children.InsertAtTop(sprite);
                row.Parts.Add((sprite, sprite.Offset));
            }
            var secondary = Colour(theme.TextSecondary);
            if (!tile.Slashed && tile.Color is { A: < 255 })
            {
                Sheet(new Patterned(Pattern.Checks, new Argb(CheckerAlpha, secondary.R, secondary.G, secondary.B)), scale, 1);
            }
            var body = tile.Slashed ? null : tile.Color is { } own ? Ink(own) : (Color?)null;
            _ = Box(at, new Vector2(side), Px(3), body, tile.Rim is { } rim ? Ink(rim) : tile.Color is null || tile.Slashed ? secondary : outline, hairline);
            if (tile.Slashed)
            {
                // No colour: a slash from corner to corner, inside the rim.
                var visual = Keep(c.CreateShapeVisual());
                visual.Size = new Vector2(side);
                visual.Offset = new Vector3(at, 0);
                var inside = hairline + Px(2);
                var line = Keep(c.CreateLineGeometry());
                (line.Start, line.End) = (new Vector2(inside, side - inside), new Vector2(side - inside, inside));
                var stroke = Keep(c.CreateSpriteShape(line));
                stroke.StrokeBrush = Keep(c.CreateColorBrush(secondary));
                stroke.StrokeThickness = MathF.Max(1, Px(1.5f));
                visual.Shapes.Add(stroke);
                visual.Opacity = enabled ? 1 : 0.4f;
                into.Children.InsertAtTop(visual);
                row.Parts.Add((visual, visual.Offset));
            }
            if (tile.Pattern is { } pattern)
            {
                // §462: its pattern smaller and stronger than on a card — a swatch is too small to show more than a corner of a
                // quiet one.
                Sheet(pattern, scale * SwatchPatternScale, SwatchPatternBoost);
            }
            if (tile.Ink is { } words)
            {
                var thick = MathF.Max(1, Px(2));
                var inset = MathF.Round(side * 0.25f);
                _ = Box(new Vector2(at.X + inset, at.Y + MathF.Round((side - thick) / 2)), new Vector2(side - (2 * inset), thick), thick / 2, Ink(words), null, 0);
            }
            row.Icon = new Area(slot.X, slot.Y, whole, whole);
            return;
        }
        var square = Px(tile.SideDip);
        var corner = new Vector2(fill.X + MathF.Round((fill.W - square) / 2), fill.Y + MathF.Round((fill.H - square) / 2));
        if (tile.Color is { } color)
        {
            // §634: a look's tile (a cell of its own: a pattern's) its card's rim, its pattern as drawn on the card, None slashed.
            var look = tile.CellDip > 0;
            _ = Box(corner, new Vector2(square), Px(4), Ink(color), tile.Rim is { } rim ? Ink(rim) : outline, hairline);
            if (look && tile.Pattern is { } pattern && gfx is not null)
            {
                var inner = square - (2 * hairline);
                var sprite = Keep(c.CreateSpriteVisual());
                sprite.Brush = Keep(c.CreateSurfaceBrush(Keep(gfx.Draw(inner, inner, dc =>
                    PatternPaint.Fill(dc, pattern, scale, new D2D_RECT_F(0, 0, inner, inner), Vector2.Zero, MathF.Max(0, Px(4) - hairline))))));
                sprite.Size = new Vector2(inner);
                sprite.Offset = new Vector3(corner.X + hairline, corner.Y + hairline, 0);
                sprite.Opacity = enabled ? 1 : 0.4f;
                into.Children.InsertAtTop(sprite);
                row.Parts.Add((sprite, sprite.Offset));
            }
            else if (look && tile.Slashed)
            {
                var visual = Keep(c.CreateShapeVisual());
                visual.Size = new Vector2(square);
                visual.Offset = new Vector3(corner, 0);
                var inside = hairline + Px(8);
                var line = Keep(c.CreateLineGeometry());
                (line.Start, line.End) = (new Vector2(inside, square - inside), new Vector2(square - inside, inside));
                var stroke = Keep(c.CreateSpriteShape(line));
                stroke.StrokeBrush = Keep(c.CreateColorBrush(Colour(theme.TextSecondary)));
                stroke.StrokeThickness = MathF.Max(1, Px(1.5f));
                visual.Shapes.Add(stroke);
                into.Children.InsertAtTop(visual);
                row.Parts.Add((visual, visual.Offset));
            }
            // Lit — under the pointer, or the keys' — a ring hard round the swatch, as Windows' grid's.
            var thick = Px(2);
            var lit = Box(corner - new Vector2(thick), new Vector2(square + (2 * thick)), Px(5), null, Colour(theme.TextSecondary), thick);
            lit.Opacity = 0;
            row.Lit = on => lit.Opacity = on ? 1 : 0;
            if (chosen)
            {
                // Ringed, 2 DIP off the swatch, in the words' colour: it shows on every colour; §634 a look's in the accent, as an
                // icon's tile is (its card is no colour of its own to stand out from).
                var gap = Px(2);
                _ = Box(corner - new Vector2(gap + thick), new Vector2(square + (2 * (gap + thick))), Px(6), null, look ? theme.Accent : Colour(theme.Text), thick);
            }
        }
        else if (tile.Code.Length > 0)
        {
            if (chosen)
            {
                _ = Box(new Vector2(fill.X, fill.Y), new Vector2(fill.W, fill.H), Px(4), theme.SubtleHover, theme.Accent, Px(2));
            }
            var glyphBox = MathF.Round(TileGlyphPx * scale);
            var surface = Keep(gfx!.Glyph(tile.Code, TileGlyphPx * scale, enabled ? theme.Text : theme.TextDisabled, glyphBox));
            var sprite = Keep(c.CreateSpriteVisual());
            sprite.Brush = Keep(c.CreateSurfaceBrush(surface));
            sprite.Size = new Vector2(glyphBox);
            sprite.Offset = new Vector3(fill.X + MathF.Round((fill.W - glyphBox) / 2), fill.Y + MathF.Round((fill.H - glyphBox) / 2), 0);
            into.Children.InsertAtTop(sprite);
            row.Parts.Add((sprite, sprite.Offset));
        }
        row.Icon = new Area(corner.X, corner.Y, square, square);
    }

    private static Color Ink(Argb color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    /// <summary>
    /// menus: a press at <paramref name="x"/>, <paramref name="y"/> (window px) on a stepper's button:
    /// its step, now, and again while it is held (<see cref="StepperRepeat"/>) where it is no switch;
    /// null where it is on none, Nothing on one that can't step.
    /// </summary>
    private FlyoutEvent? StepperPress(int x, int y)
    {
        var index = RowAt(x, y);
        if (index < 0 || StepperAt(rows[index], x, y) is not { } hit)
        {
            return null;
        }
        if (!hit.Enabled)
        {
            return new FlyoutEvent(FlyoutEventKind.Nothing);
        }
        if (rows[index].Item.Control is FlyoutControl.Stepper { Switch: false })
        {
            stepperHeld = hit.Id;
            _ = SetTimer(Hwnd, StepperTimer, SpinDelayMs, null);
        }
        return new FlyoutEvent(FlyoutEventKind.Adjust, hit.Id);
    }

    /// <summary>
    /// menus: the held stepper's button steps again, while the left button is down and the pointer on
    /// it and it can step; else the hold ends.
    /// </summary>
    private FlyoutEvent StepperRepeat()
    {
        POINT cursor;
        if (stepperHeld != 0 && IsOpen && GetAsyncKeyState(VK.VK_LBUTTON) < 0 && GetCursorPos(&cursor) && ScreenToClient(Hwnd, &cursor)
            && RowAt(cursor.x, cursor.y) is var index and >= 0 && StepperAt(rows[index], cursor.x, cursor.y) is { } hit && hit.Id == stepperHeld && hit.Enabled)
        {
            _ = SetTimer(Hwnd, StepperTimer, SpinIntervalMs, null);
            return new FlyoutEvent(FlyoutEventKind.Adjust, hit.Id);
        }
        StepperRelease();
        return default;
    }

    /// <summary>menus: no stepper held any more.</summary>
    private void StepperRelease()
    {
        if (stepperHeld != 0)
        {
            stepperHeld = 0;
            _ = KillTimer(Hwnd, StepperTimer);
        }
    }

    /// <summary>
    /// menus (§502): the wheel turned <paramref name="delta"/> at <paramref name="x"/>, <paramref name="y"/>
    /// (window px) over a stepper — its "−", its value or its "+" — steps it as its buttons do: up is its
    /// "+", down its "−", Nothing where it can't step that way, so the menu does not scroll under it
    /// either. Null where the wheel is over no stepper (the menu scrolls), or a switch (AM/PM), which a
    /// turn would flip back and forth.
    /// </summary>
    private FlyoutEvent? StepperWheel(int x, int y, int delta)
    {
        var index = RowAt(x, y);
        if (index < 0 || rows[index] is not { Item.Control: FlyoutControl.Stepper { Switch: false } stepper } row
            || !StepperSpan(row).Contains(x - viewport.X, y - viewport.Y + scroll))
        {
            wheelTurned = 0;
            wheelOn = 0;
            return null;
        }
        if (wheelOn != row.Item.Id)
        {
            wheelTurned = 0;
            wheelOn = row.Item.Id;
        }
        return WheelNotch(ref wheelTurned, delta) switch
        {
            > 0 when stepper.CanMore => new FlyoutEvent(FlyoutEventKind.Adjust, stepper.More),
            < 0 when stepper.CanFewer => new FlyoutEvent(FlyoutEventKind.Adjust, stepper.Fewer),
            _ => new FlyoutEvent(FlyoutEventKind.Nothing),
        };
    }

    /// <summary>menus (§502): how far the wheel has turned over the stepper <see cref="wheelOn"/> names, short of a notch.</summary>
    private int wheelTurned;

    /// <summary>menus (§502): the stepper row the wheel is turning over, 0 for none.</summary>
    private uint wheelOn;

    /// <summary>
    /// menus (§502): a wheel's turn of <paramref name="delta"/> over a stepper, <paramref name="turned"/>
    /// what was turned before short of a notch (WHEEL_DELTA, 120): +1 where a notch up is made, -1 down,
    /// 0 short of one — a fine wheel's or a touchpad's small turns add up to it. A turn the other way
    /// starts again; one step a message at most, what is past it let go, so a fast spin steps no further
    /// than the pointer's own messages.
    /// </summary>
    internal static int WheelNotch(ref int turned, int delta)
    {
        const int notch = 120;
        if (turned != 0 && delta != 0 && Math.Sign(turned) != Math.Sign(delta))
        {
            turned = 0;
        }
        turned += delta;
        if (Math.Abs(turned) < notch)
        {
            return 0;
        }
        var way = Math.Sign(turned);
        turned %= notch;
        return way;
    }

    /// <summary>menus (§502): where a stepper's row's stepper stands, from its "−" to its "+", its value between (content px).</summary>
    private static Area StepperSpan(Row row)
    {
        var left = MathF.Min(row.Fewer.X, row.More.X);
        var top = MathF.Min(row.Fewer.Y, row.More.Y);
        var right = MathF.Max(row.Fewer.X + row.Fewer.W, row.More.X + row.More.W);
        var bottom = MathF.Max(row.Fewer.Y + row.Fewer.H, row.More.Y + row.More.H);
        return new Area(left, top, right - left, bottom - top);
    }

    /// <summary>menus: the stepper's button of <paramref name="row"/> at <paramref name="x"/>, <paramref name="y"/> (window px): the id it sends and whether it can; null for none.</summary>
    private (uint Id, bool Enabled)? StepperAt(Row row, int x, int y)
    {
        if (row.Item.Control is not FlyoutControl.Stepper stepper)
        {
            return null;
        }
        var at = new Vector2(x - viewport.X, y - viewport.Y + scroll);
        return row.Fewer.Contains(at.X, at.Y) ? (stepper.Fewer, stepper.CanFewer)
            : row.More.Contains(at.X, at.Y) ? (stepper.More, stepper.CanMore)
            : null;
    }

    /// <summary>Tests only: the middle of row <paramref name="id"/>'s fill, in window px, and its size; null where there is none.</summary>
    internal (POINT Middle, float W, float H)? RowFill(uint id) =>
        rows.FirstOrDefault(row => row.Item.Id == id) is { } found
            ? (new POINT((int)MathF.Round(viewport.X + found.Bounds.X + (found.Bounds.W / 2)), (int)MathF.Round(viewport.Y + found.Bounds.Y + (found.Bounds.H / 2) - scroll)), found.Bounds.W, found.Bounds.H)
            : null;

    private sealed partial class Row
    {
        /// <summary>menus: what else the row shows as it is lit or no longer (a swatch's ring).</summary>
        public Action<bool>? Lit;
    }
}
