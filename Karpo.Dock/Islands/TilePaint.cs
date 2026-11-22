// What the folder's tiles draw with: text that fits its room, glyphs, bars,
// pictures, and for an island without a tile of its own, its bar cells centred
// on the card. Everything is drawn with Direct2D into the folder's surfaces, in
// px, like the flyouts' own painting (PopupPaint).

using System.Numerics;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace Karpo.Dock;

internal static unsafe class TilePaint
{
    /// <summary>The card's room for content: <paramref name="box"/> less the tile's padding.</summary>
    public static D2D_RECT_F Inner(D2D_RECT_F box, in TileStyle style)
    {
        var pad = style.Pad;
        return new D2D_RECT_F(box.left + pad, box.top + pad, MathF.Max(box.left + pad, box.right - pad), MathF.Max(box.top + pad, box.bottom - pad));
    }

    public static float Width(in D2D_RECT_F rect) => rect.right - rect.left;

    public static float Height(in D2D_RECT_F rect) => rect.bottom - rect.top;

    /// <summary>
    /// Whether <paramref name="text"/> at <paramref name="px"/> still says anything in
    /// <paramref name="room"/> px: the room must hold the cut mark <b>and a letter before it</b>,
    /// or else the whole text, which a short one can manage in less.
    /// <para>
    /// "S…" where September was meant says less than nothing — it is not a word anybody wrote, it
    /// is the shape a card takes when it has run out of room, and it leaves the person guessing at
    /// a month. Such a line is left out instead, and what is left is honest and shorter. Every
    /// card's lines go through the three helpers below, so this is one rule for all of them rather
    /// than a check each painter remembers or forgets.
    /// </para>
    /// <para>
    /// The floor is measured from the mark and a letter at the line's own size, so it means the
    /// same thing at 100, 125, 150 and 200 % and at every text size, and the second arm is what
    /// lets a card's big number through a room narrower than a cut word of that size would need —
    /// a day's "22" at 34 DIP is far shorter than "m…" at 34 DIP.
    /// </para>
    /// <para>
    /// This is a <b>drawing</b> decision and never a sizing one: the card's box comes from the
    /// folder's plan and nothing here can move it. Inside a card, lines already come and go with
    /// what there is to say — a calendar with no events, a forecast with no hours — and this is
    /// that same freedom, used to leave out a line that would say nothing.
    /// </para>
    /// </summary>
    public static bool Readable(Gfx gfx, string text, float px, Weight weight, float room) =>
        room >= 1
        && (room >= PopupPaint.Measure(gfx, "m…", px, weight).X
            || PopupPaint.Measure(gfx, text, px, weight).X <= room);

    /// <summary>One line at (<paramref name="x"/>, <paramref name="y"/>), cut with an ellipsis at <paramref name="room"/>; its height.</summary>
    /// <summary>
    /// quick: a line centred across <paramref name="room"/> rather than started at its left, for a
    /// card whose content is a column down the middle (a one-button switch's glyph and name).
    /// </summary>
    public static float Centred(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float x, float y, float room)
    {
        if (text.Length == 0 || !Readable(gfx, text, px, weight, room))
        {
            return 0;
        }
        var size = PopupPaint.Measure(gfx, text, px, weight, room);
        PopupPaint.Text(dc, gfx, text, px, weight, color, x + MathF.Max(0, (room - size.X) / 2), y, room);
        return size.Y;
    }

    public static float Line(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float x, float y, float room)
    {
        if (text.Length == 0 || !Readable(gfx, text, px, weight, room))
        {
            return 0;
        }
        var size = PopupPaint.Measure(gfx, text, px, weight, room);
        PopupPaint.Text(dc, gfx, text, px, weight, color, x, y, room);
        return size.Y;
    }

    /// <summary>One line ending at <paramref name="right"/>; its width.</summary>
    public static float LineRight(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float right, float y, float room)
    {
        if (text.Length == 0 || !Readable(gfx, text, px, weight, room))
        {
            return 0;
        }
        var size = PopupPaint.Measure(gfx, text, px, weight, room);
        PopupPaint.Text(dc, gfx, text, px, weight, color, right - size.X, y, room);
        return size.X;
    }

    /// <summary>
    /// How wide a line of figures in fields is (<see cref="Fields"/>): each field as wide as its
    /// shape — the widest its text can be — whatever the text in it now.
    /// </summary>
    public static float FieldsWidth(Gfx gfx, IReadOnlyList<(string Text, string Shape)> fields, float px, Weight weight)
    {
        var width = 0f;
        foreach (var (_, shape) in fields)
        {
            width += PopupPaint.Measure(gfx, shape, px, weight).X;
        }
        return width;
    }

    /// <summary>
    /// A line of figures in fields, its line box's top-left at (<paramref name="x"/>,
    /// <paramref name="y"/>): each field as wide as its shape, and its text right-aligned in it, as
    /// figures in a column are — so "R 0K  W 88K" and "R 8.8M  W 0K" put their W in one place, and a
    /// figure changing moves nothing after it. A line of one field starts at its left.
    /// </summary>
    public static void Fields(ID2D1DeviceContext* dc, Gfx gfx, IReadOnlyList<(string Text, string Shape)> fields, float px, Weight weight, DXGI_RGBA color, float x, float y)
    {
        foreach (var (text, shape) in fields)
        {
            var field = PopupPaint.Measure(gfx, shape, px, weight).X;
            var drawn = PopupPaint.Measure(gfx, text, px, weight).X;
            PopupPaint.Text(dc, gfx, text, px, weight, color, fields.Count == 1 ? x : x + field - drawn, y);
            x += field;
        }
    }

    /// <summary>A line's height at <paramref name="px"/>, for laying rows out before drawing them.</summary>
    public static float LineHeight(Gfx gfx, float px, Weight weight = Weight.Regular) => PopupPaint.Measure(gfx, "Ag", px, weight).Y;

    /// <summary>
    /// The largest text size from <paramref name="most"/> down to <paramref name="least"/> px at
    /// which <paramref name="text"/> fits <paramref name="width"/> × <paramref name="height"/>.
    /// </summary>
    public static float Fitting(Gfx gfx, string text, Weight weight, float most, float least, float width, float height)
    {
        var px = MathF.Max(most, least);
        while (px > least)
        {
            var size = PopupPaint.Measure(gfx, text, px, weight);
            if (size.X <= width && size.Y <= height)
            {
                return px;
            }
            px = MathF.Max(least, MathF.Floor(px * 0.9f));
        }
        return least;
    }

    /// <summary>A Segoe Fluent Icons glyph, centred by its ink in the <paramref name="size"/> px square at (<paramref name="x"/>, <paramref name="y"/>).</summary>
    public static void Glyph(ID2D1DeviceContext* dc, Gfx gfx, string code, float size, DXGI_RGBA color, float x, float y) =>
        PopupPaint.Glyph(dc, gfx, code, size, color, PopupPaint.Rect(x, y, size, size));

    /// <summary>A rounded bar: the track, and <paramref name="fraction"/> of it filled.</summary>
    public static void Bar(ID2D1DeviceContext* dc, D2D_RECT_F rect, float fraction, DXGI_RGBA track, DXGI_RGBA fill)
    {
        var height = Height(rect);
        if (Width(rect) < 1 || height < 1)
        {
            return;
        }
        PaintWatch.Space(Spacing.Meter, rect);
        PopupPaint.Fill(dc, rect, height / 2, track);
        if (fraction > 0)
        {
            // At least as long as it is thick, so a nearly empty bar still shows a dot.
            var filled = rect with { right = rect.left + MathF.Max(Width(rect) * Math.Clamp(fraction, 0, 1), height) };
            PopupPaint.Fill(dc, filled, height / 2, fill);
        }
    }

    /// <summary>The theme's colour for a usage bar's empty part.</summary>
    public static DXGI_RGBA Track(in TileStyle style) => style.Theme.Dark ? Gfx.Ink(1, 1, 1, 0.14f) : Gfx.Ink(0, 0, 0, 0.12f);

    /// <summary>The accent as text and bars take it.</summary>
    public static DXGI_RGBA Accent(in TileStyle style) => PopupPaint.Ink(style.Theme.Accent);

    /// <summary>Runs <paramref name="paint"/> with (0, 0) moved to (<paramref name="x"/>, <paramref name="y"/>) and sizes times <paramref name="scale"/>.</summary>
    public static void Placed(ID2D1DeviceContext* dc, float x, float y, float scale, Action paint)
    {
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        // This move and scale first, then whatever was set already (the surface's own offset).
        var placed = new D2D_MATRIX_3X2_F
        {
            _11 = scale * before._11,
            _12 = scale * before._12,
            _21 = scale * before._21,
            _22 = scale * before._22,
            _31 = x * before._11 + y * before._21 + before._31,
            _32 = x * before._12 + y * before._22 + before._32,
        };
        dc->SetTransform(&placed);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
        }
    }

    // --- Is there room for a card at all? ---------------------------------------------------
    //
    // A folder's cell is 36 DIP (FolderOptions.CellPx), about the size of a cell of the taskbar's
    // own grid, and the card drawings were laid out for roughly twice that. Rather than thirteen
    // painters each learning to shrink - which the drive's card had already started doing by hand,
    // one kind's answer to everyone's problem - there is one question asked once: has this card
    // the room a card drawing needs? Where it has not, the widget is drawn the way the bar draws
    // it, which is a drawing every kind already has and already reads at a cell this size. The
    // card behind it supplies the background the bar's own card supplies.
    //
    // The threshold is measured, not chosen. Every card is the same template - a caption naming
    // the card, and the value under it (or beside it, on a card wider than it is tall) - so the
    // room a card needs is that template's own two lines and the card's own padding, laid out at
    // the scale and text size in force. It therefore holds at 100/125/150/200 % and at every text
    // size without a second number: at 36 DIP every arrangement falls below it, at the 72-128 DIP
    // the cells used to be every arrangement clears it, and the crossover is around 55 DIP at
    // 100 %, which is where the card drawings were in fact designed to start.
    //
    // It reads the style and the card's rectangle and nothing else: no live value ever reaches
    // it, so the treatment a widget gets cannot change while somebody is looking at it.

    /// <summary>
    /// The side, in px, a card must have both ways for a card drawing: its padding and the two
    /// lines of the template (<see cref="TileStyle.CaptionPx"/>, <see cref="TileStyle.BodyPx"/>).
    /// <para>
    /// One number for both directions, because the template is two lines whichever way it is
    /// arranged: stacked it needs the height, side by side it needs the width, and a box that
    /// holds neither holds no card. A one-cell-wide column is the case that makes the across half
    /// earn its keep - three cells down of a 36-DIP column leaves eight px of usable width, which
    /// is room for an ellipsis and nothing else.
    /// </para>
    /// <para>
    /// Measured through <see cref="TextLine.Template"/> rather than laid out afresh, because this
    /// is asked on every paint, every hit test and every move of the pointer: the two line heights
    /// come from the cache after the first card of a given style.
    /// </para>
    /// </summary>
    public static float CardRoom(Gfx gfx, in TileStyle style) =>
        (2 * style.Pad)
        + TextLine.Template(gfx, "Ag", style.CaptionPx, Weight.Semibold).Height
        + TextLine.Template(gfx, "Ag", style.BodyPx, Weight.Semibold).Height;

    /// <summary>Whether <paramref name="box"/> (px) has that room; else the widget is drawn as the bar draws it.</summary>
    public static bool CardFits(Gfx gfx, D2D_RECT_F box, in TileStyle style)
    {
        var room = CardRoom(gfx, style);
        return Width(box) >= room && Height(box) >= room;
    }

    // --- Islands without a tile of their own -----------------------------------------------

    /// <summary>
    /// Where the bar cells of <paramref name="island"/> go on its card: the style they are drawn
    /// in, each cell's size, where the row starts and how much it is shrunk to fit the card.
    ///
    /// The style's thickness is the card's own square — the smaller of what the card leaves
    /// across and along — because that thickness is the grid cell a widget measures itself in
    /// (<see cref="Layout.CellPx"/>). Given the card's square, a widget sizes its glyphs and text
    /// for the card and fills it, and a one-cell widget is one card, exactly as on the bar.
    /// </summary>
    private static (IslandStyle Style, List<(float Width, float Height)> Cells, float X, float Y, float Scale) CellsOnCard(CellIsland island, D2D_RECT_F box, in TileStyle tile, bool padded)
    {
        // Unpadded, the cells are given the whole box, as the bar gives them its whole button.
        var inner = padded ? Inner(box, tile) : box;
        var thickness = MathF.Max(1, MathF.Min(Height(inner), Width(inner)));
        // No width is passed: the content sets it, as it does on the bar.
        var style = new IslandStyle(tile.Scale, false, thickness, tile.Px(24), tile.Look);
        var cells = new List<(float, float)>();
        var length = 0f;
        for (var index = 0; index < island.CellCount; index++)
        {
            var size = island.CellSize(index, style);
            cells.Add(size);
            length += size.Width;
        }
        var scale = length > Width(inner) && length > 0 ? Width(inner) / length : 1;
        // Along the card: where the row of cells starts in the room it was given.
        //
        // A **card** starts its content at the card's inset, because that is what a card's inset
        // is: every other card's content begins at TileStyle.Pad, and a row centred in the room
        // instead begins at the inset plus half of whatever is left over — a number nobody chose,
        // and one that grows with the card. That is what this line used to do, so every kind with
        // no card painter of its own inherited a centred inset: the weather's parts, a share, the
        // contacts button all began 16 px in on a card two cells wide and 38 px in on one four
        // cells wide. The now-playing card was the same fault wearing its own coat, having written
        // the centring out by hand instead of inheriting it.
        //
        // The **bar's own drawing** keeps centring, and that is not an oversight: without the
        // padding this is the taskbar's drawing (a card too small for a card drawing is drawn as
        // the bar draws it), where a widget's content sits in the middle of the button it was
        // given. A glyph centred in its cell is what the bar looks like, and that spacing is
        // settled. The distinction is the point — a card's inset and a button's centre are two
        // different quantities, and this is the one place that has to tell them apart.
        var place = padded ? CellPlace.Start : CellPlace.Centre;
        var x = inner.left + GlyphAndText.Place(place, Width(inner), length * scale);
        var y = inner.top + (Height(inner) - thickness * scale) / 2;
        return (style, cells, MathF.Round(x), MathF.Round(y), scale);
    }

    /// <summary>
    /// The island's bar cells in a row, shrunk to fit the card: started at the card's inset where
    /// it is <paramref name="padded"/>, and centred in <paramref name="box"/> where it is not,
    /// which is the bar's own drawing (see <see cref="CellsOnCard"/>).
    /// </summary>
    public static void Cells(ID2D1DeviceContext* dc, CellIsland island, D2D_RECT_F box, in TileStyle tile, bool padded = true)
    {
        var (style, cells, x, y, scale) = CellsOnCard(island, box, tile, padded);
        var at = x;
        for (var index = 0; index < cells.Count; index++)
        {
            var (width, height) = cells[index];
            var cell = index;
            Placed(dc, at, y, scale, () => Clipped(dc, island, cell, style, width, height));
            at += width * scale;
        }
    }

    /// <summary>
    /// One bar cell, drawn into the room it was given and cut off at its edge.
    /// <para>
    /// On the bar a cell <b>is</b> a surface of exactly that size, so a drawing that runs over is
    /// cut there and the question never comes up. A card has no surface per cell, so the same
    /// drawing ran over its neighbours — a clock's time under the card below it — and the folder
    /// clipped the whole card to hide it. The cut belongs here, where the cell's room is known,
    /// and then a folder's card means the same thing the bar's does. Six kinds had already
    /// written this line inside their own <c>PaintCell</c>; the ones that had not are the ones
    /// that ran over.
    /// </para>
    /// </summary>
    private static void Clipped(ID2D1DeviceContext* dc, CellIsland island, int index, IslandStyle style, float width, float height)
    {
        var room = PopupPaint.Rect(0, 0, width, height);
        dc->PushAxisAlignedClip(&room, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            island.PaintCell(dc, index, style, width, height);
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>The bar cell a point on the card is over (see <see cref="Cells"/>); the first when it is over none.</summary>
    public static int CellAt(CellIsland island, Vector2 point, D2D_RECT_F box, in TileStyle tile, bool padded = true)
    {
        var (_, cells, x, _, scale) = CellsOnCard(island, box, tile, padded);
        var at = x;
        for (var index = 0; index < cells.Count; index++)
        {
            var width = cells[index].Width * scale;
            if (point.X >= at && point.X < at + width)
            {
                return index;
            }
            at += width;
        }
        return 0;
    }

    /// <summary>
    /// The bar cell a point is over (see <see cref="Cells"/>), with the point in that cell's own
    /// px, the cell's size and the style it was drawn in: what a widget that takes the pointer
    /// itself is handed (<see cref="CellIsland.PressOn"/>). The cells are drawn shrunk to fit the
    /// card, so the point is divided by that scale — the widget works in the px it drew in.
    /// A point past the last cell belongs to the last, and one before the first to the first, so a
    /// drag that ran off the end still moves the cell it took hold of. <c>Size</c> is zero when the
    /// widget has no cells at all.
    /// </summary>
    public static (int Index, Vector2 Point, Vector2 Size, IslandStyle Style) CellUnder(
        CellIsland island, Vector2 point, D2D_RECT_F box, in TileStyle tile, bool padded = true)
    {
        var (style, cells, x, y, scale) = CellsOnCard(island, box, tile, padded);
        var at = x;
        for (var index = 0; index < cells.Count; index++)
        {
            var (width, height) = cells[index];
            if (point.X < at + width * scale || index == cells.Count - 1)
            {
                var local = scale > 0 ? new Vector2((point.X - at) / scale, (point.Y - y) / scale) : Vector2.Zero;
                return (index, local, new Vector2(width, height), style);
            }
            at += width * scale;
        }
        return (0, Vector2.Zero, Vector2.Zero, style);
    }

    /// <summary>"3 h 5 min", "42 min": a duration as a card says it.</summary>
    public static string Duration(TimeSpan time)
    {
        var minutes = (int)Math.Max(0, time.TotalMinutes);
        return minutes < 60 ? S.WidgetTileMinutes(minutes) : S.WidgetTileHoursMinutes(hours: minutes / 60, minutes: minutes % 60);
    }
}
