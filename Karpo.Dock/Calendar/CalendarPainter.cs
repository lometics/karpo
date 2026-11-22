// The calendar's day, week and year views drawn (CalendarLayout, CalendarViewTile): the part that
// stays put (the page's title and its arrows, the notes, the all-day label), the scrolling part (the
// year's months, the hours of the day and week views), the day and week views' head band
// (headings, all-day row), which slides with the hours, and the gutter's labels, which scroll with
// them but don't slide — each clipped to its window and moved as the line shows it (Picture).
// They were the calendar flyout's surfaces; the line draws them in one pass.
//
// Events are marked with their calendar's colour: a stripe beside blocks, a tinted fill under
// blocks and all-day bars, dots under days. High contrast draws them in the system colours instead.

using System.Globalization;
using System.Numerics;
using TerraFX.Interop.DirectX;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

internal static unsafe class CalendarPainter
{
    /// <summary>Everything but the scrolling part, in line px.</summary>
    public static void PaintFrame(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, FlyoutTheme theme)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var divider = PopupPaint.Ink(theme.Divider);
        foreach (var y in plan.Dividers)
        {
            PopupPaint.Divider(dc, 0, plan.Width, y, plan.Hairline, divider);
        }

        // The page's title, its week number, and the arrows.
        var titleRoom = MathF.Max(1, plan.TitleEnd - plan.Pad);
        var title = PopupPaint.Measure(gfx, plan.NavTitle, plan.TextPx, Weight.Semibold, titleRoom);
        Row(dc, gfx, plan.NavTitle, plan.TextPx, Weight.Semibold, theme.Text, plan.Pad, plan.NavRow, titleRoom);
        if (plan.NavNote.Length > 0)
        {
            var note = PopupPaint.Measure(gfx, plan.NavNote, plan.SmallPx, Weight.Regular);
            var left = plan.Pad + title.X + D(8);
            if (left + note.X <= plan.TitleEnd)
            {
                Row(dc, gfx, plan.NavNote, plan.SmallPx, Weight.Regular, theme.TextSecondary, left, plan.NavRow, note.X + 1);
            }
        }
        PopupPaint.Glyph(dc, gfx, plan.PreviousGlyph, plan.GlyphPx, theme.Text, plan.Previous);
        PopupPaint.Glyph(dc, gfx, plan.NextGlyph, plan.GlyphPx, theme.Text, plan.Next);

        if (plan.Grid is { } grid)
        {
            GridFrame(dc, gfx, plan, grid, theme);
        }
        foreach (var row in plan.Notes)
        {
            Note(dc, gfx, plan, row, theme);
        }
    }

    /// <summary>
    /// The scrolling part, all of it, in its own px (0 at its top). In the day and week views that is
    /// the strip's hours, x along the strip: the gutter's labels are <see cref="PaintGutter"/>'s.
    /// </summary>
    public static void PaintContent(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, FlyoutTheme theme)
    {
        if (plan.Year is { } year)
        {
            Year(dc, gfx, plan, year, theme);
        }
        if (plan.Grid is { } grid)
        {
            Hours(dc, gfx, plan, grid, theme);
        }
    }

    /// <summary>The day and week views' head band, in its own px (x along the strip): the headings and the all-day row.</summary>
    public static void PaintHead(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, FlyoutTheme theme)
    {
        if (plan.Grid is { } grid)
        {
            Head(dc, gfx, plan, grid, theme);
        }
    }

    /// <summary>The day and week views' gutter, in px of the scrolling part: the hours' labels, which stay put as the days slide.</summary>
    public static void PaintGutter(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, FlyoutTheme theme)
    {
        if (plan.Grid is { } grid)
        {
            Gutter(dc, gfx, plan, grid, theme);
        }
    }

    /// <summary>
    /// The panel as it shows with the scrolling part at <paramref name="scroll"/> and the days slid
    /// <paramref name="slide"/> px along their strip (null: at rest): the frame, and each part in its
    /// window, as the flyout's visuals put them.
    /// </summary>
    public static void Picture(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, FlyoutTheme theme, float scroll, float? slide = null)
    {
        PaintFrame(dc, gfx, plan, theme);
        if (plan.Grid is { } grid)
        {
            var along = slide ?? grid.RestSlide;
            var right = grid.Left + grid.WindowWidth;
            if (grid.Head.bottom > grid.Head.top)
            {
                Clipped(dc, grid.Head, grid.Left - along, grid.Head.top, () => PaintHead(dc, gfx, plan, theme));
            }
            if (plan.Viewport.Y > 0)
            {
                var top = plan.ViewportTop;
                var bottom = plan.ViewportTop + plan.Viewport.Y;
                Clipped(dc, new D2D_RECT_F(0, top, grid.Left, bottom), 0, top - scroll, () => PaintGutter(dc, gfx, plan, theme));
                Clipped(dc, new D2D_RECT_F(grid.Left, top, right, bottom), grid.Left - along, top - scroll, () => PaintContent(dc, gfx, plan, theme));
            }
            return;
        }
        if (plan.Viewport.Y <= 0)
        {
            return;
        }
        var window = new D2D_RECT_F(0, plan.ViewportTop, plan.Width, plan.ViewportTop + plan.Viewport.Y);
        Clipped(dc, window, 0, plan.ViewportTop - scroll, () => PaintContent(dc, gfx, plan, theme));
    }

    /// <summary><paramref name="paint"/> clipped to <paramref name="window"/> (line px) and moved by (<paramref name="x"/>, <paramref name="y"/>), as a visual of its own is.</summary>
    private static void Clipped(ID2D1DeviceContext* dc, D2D_RECT_F window, float x, float y, Action paint)
    {
        dc->PushAxisAlignedClip(&window, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        D2D_MATRIX_3X2_F before;
        dc->GetTransform(&before);
        var moved = before;
        moved._31 = x * before._11 + y * before._21 + before._31;
        moved._32 = x * before._12 + y * before._22 + before._32;
        dc->SetTransform(&moved);
        try
        {
            paint();
        }
        finally
        {
            dc->SetTransform(&before);
            dc->PopAxisAlignedClip();
        }
    }
    /// <summary>The colour an event is marked with: its calendar's, the accent for one without, the highlight in high contrast.</summary>
    public static DXGI_RGBA Solid(Color colour, FlyoutTheme theme) =>
        PopupPaint.Ink(colour.A == 0 || theme.HighContrast ? theme.Accent : FlyoutTheme.Opaque(colour));

    /// <summary>The fill under an event's block: its colour, faint enough for text on it.</summary>
    public static DXGI_RGBA Tint(Color colour, FlyoutTheme theme)
    {
        if (theme.HighContrast)
        {
            return PopupPaint.Ink(theme.Base);
        }
        var solid = colour.A == 0 ? theme.Accent : FlyoutTheme.Opaque(colour);
        return PopupPaint.Ink(FlyoutTheme.WithAlpha(solid, theme.Dark ? 0.34f : 0.22f));
    }

    // --- The frame's parts ---------------------------------------------------

    /// <summary>The day and week views' part of the frame: the all-day row's label in the gutter.</summary>
    private static void GridFrame(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, GridPart grid, FlyoutTheme theme)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        if (grid.AllDayRow is { } row)
        {
            var label = PopupPaint.Measure(gfx, grid.AllDayLabel, plan.SmallPx, Weight.Regular);
            PopupPaint.Text(dc, gfx, grid.AllDayLabel, plan.SmallPx, Weight.Regular, theme.TextSecondary,
                grid.Left - D(8) - label.X, row.top + D(4) + PopupPaint.Round((PopupPaint.Round(22 * plan.TextPx / 14) - label.Y) / 2));
        }
    }

    /// <summary>The head band: each day's heading, today's number on the accent, the selected day's ringed; the all-day bars.</summary>
    private static void Head(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, GridPart grid, FlyoutTheme theme)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var accent = PopupPaint.Ink(theme.Accent);
        foreach (var heading in grid.Headings)
        {
            var box = heading.Box;
            var weekday = PopupPaint.Measure(gfx, heading.Weekday, plan.SmallPx, Weight.Regular);
            PopupPaint.Text(dc, gfx, heading.Weekday, plan.SmallPx, Weight.Regular, theme.TextSecondary,
                PopupPaint.Round((box.left + box.right - weekday.X) / 2), box.top + D(4), box.right - box.left);
            var size = D(28) * plan.TextPx / (14 * plan.Scale);
            var centre = new Vector2((box.left + box.right) / 2, box.bottom - D(4) - size / 2);
            var number = PopupPaint.Rect(centre.X - size / 2, centre.Y - size / 2, size, size);
            var isToday = heading.Day == plan.Today;
            if (isToday)
            {
                PopupPaint.Circle(dc, centre, size / 2, accent);
                if (heading.Day == plan.Selected)
                {
                    PopupPaint.Ring(dc, centre, size / 2 - plan.Inset, plan.Ring, PopupPaint.Ink(theme.Base));
                }
            }
            else if (heading.Day == plan.Selected)
            {
                PopupPaint.Ring(dc, centre, size / 2, plan.Ring, accent);
            }
            PopupPaint.Centred(dc, gfx, heading.Number, plan.TextPx, isToday ? Weight.Semibold : Weight.Regular,
                isToday ? theme.TextOnAccent : theme.Text, number, ink: true);
        }
        foreach (var bar in grid.Bars)
        {
            if (bar.More)
            {
                Line(dc, gfx, bar.Title, plan.SmallPx, Weight.Regular, theme.TextSecondary, bar.Box.left + D(6), bar.Box, bar.Box.right - bar.Box.left - D(6));
                continue;
            }
            PopupPaint.Fill(dc, bar.Box, D(4), Tint(bar.Colour, theme));
            if (theme.HighContrast)
            {
                PopupPaint.Stroke(dc, bar.Box, D(4), MathF.Max(1, PopupPaint.Round(plan.Scale)), theme.Text);
            }
            var stripe = new D2D_RECT_F(bar.Box.left, bar.Box.top, bar.Box.left + plan.Bar, bar.Box.bottom);
            PopupPaint.Fill(dc, stripe, plan.Bar / 2, Solid(bar.Colour, theme));
            var left = bar.Box.left + plan.Bar + D(6);
            Line(dc, gfx, bar.Title, plan.SmallPx, Weight.Regular, theme.Text, left, bar.Box, MathF.Max(1, bar.Box.right - left - D(4)));
        }
    }
    /// <summary>A note under a view: a message, or a small line.</summary>
    private static void Note(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, AgendaRow row, FlyoutTheme theme)
    {
        var room = plan.Width - 2 * plan.Pad;
        var (px, ink) = row.Kind == AgendaKind.Message ? (plan.TextPx, theme.TextSecondary) : (plan.SmallPx, theme.TextSecondary);
        Row(dc, gfx, row.Title, px, Weight.Regular, ink, plan.Pad, row.Box, room);
    }

    // --- The scrolling part's views -----------------------------------------

    /// <summary>The year's twelve small months: name, weekday initials, days, today, the selection, a dot under days with events.</summary>
    private static void Year(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, YearPart year, FlyoutTheme theme)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var accent = PopupPaint.Ink(theme.Accent);
        var radius = MathF.Min(year.CellWidth, year.CellHeight) / 2 - MathF.Max(1, PopupPaint.Round(plan.Scale / 2));
        foreach (var month in year.Months)
        {
            // A month's name is a label: never the accent (§388), today's circle says which month it is.
            Line(dc, gfx, month.Name, plan.SmallPx, Weight.Semibold, theme.Text, month.Title.left, month.Title, month.Title.right - month.Title.left);
            for (var i = 0; i < 7; i++)
            {
                var column = PopupPaint.Rect(month.Weekdays.left + i * year.CellWidth, month.Weekdays.top, year.CellWidth, month.Weekdays.bottom - month.Weekdays.top);
                // The first letter or two of the day's name: the column is narrow.
                var name = year.Weekdays[i].Length > 2 ? year.Weekdays[i][..2] : year.Weekdays[i];
                PopupPaint.Centred(dc, gfx, name, plan.SmallPx, Weight.Regular, theme.TextSecondary, column, ink: false);
            }
            for (var i = 0; i < 42; i++)
            {
                var date = month.First.AddDays(i);
                if (date.Month != month.Month || date.Year != year.Year)
                {
                    continue;
                }
                var cell = PopupPaint.Rect(month.Days.left + i % 7 * year.CellWidth, month.Days.top + i / 7 * year.CellHeight, year.CellWidth, year.CellHeight);
                var centre = new Vector2((cell.left + cell.right) / 2, (cell.top + cell.bottom) / 2);
                var isToday = date == plan.Today;
                if (isToday)
                {
                    PopupPaint.Circle(dc, centre, radius, accent);
                }
                else if (date == plan.Selected)
                {
                    PopupPaint.Ring(dc, centre, radius, MathF.Max(1, plan.Ring * 0.75f), accent);
                }
                PopupPaint.Centred(dc, gfx, date.Day.ToString(CultureInfo.InvariantCulture), plan.SmallPx, isToday ? Weight.Semibold : Weight.Regular,
                    isToday ? theme.TextOnAccent : theme.Text, cell, ink: true);
                if (year.Busy.TryGetValue(date, out var colour))
                {
                    var dot = MathF.Max(1, D(1.5f));
                    PopupPaint.Circle(dc, new Vector2(centre.X, cell.bottom - dot - MathF.Max(1, D(1))), dot, isToday ? theme.TextOnAccent : Solid(colour, theme));
                }
            }
        }
    }

    /// <summary>
    /// The hours' labels in the gutter, right-aligned against the strip: each hour of the day on its
    /// line, midnight at the top.
    /// </summary>
    private static void Gutter(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, GridPart grid, FlyoutTheme theme)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var map = grid.Map;
        void Label(int hour, float y)
        {
            var label = PopupPaint.Measure(gfx, grid.Hours[hour], plan.SmallPx, Weight.Regular);
            PopupPaint.Text(dc, gfx, grid.Hours[hour], plan.SmallPx, Weight.Regular, theme.TextSecondary,
                grid.Left - D(8) - label.X, y - PopupPaint.Round(label.Y / 2));
        }
        for (var hour = 0; hour < 24; hour++)
        {
            Label(hour, PopupPaint.Round(map.Y(hour * 60)));
        }
    }

    /// <summary>
    /// The strip's hours, x along the strip: the hours' lines, the days' columns, the events' blocks,
    /// the now-line.
    /// </summary>
    private static void Hours(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, GridPart grid, FlyoutTheme theme)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var line = MathF.Max(1, PopupPaint.Round(plan.Scale));
        var divider = PopupPaint.Ink(theme.Divider);
        var map = grid.Map;
        var right = grid.StripWidth;
        // Every hour's line, midnight to midnight.
        for (var hour = 0; hour <= 24; hour++)
        {
            PopupPaint.Divider(dc, 0, right, PopupPaint.Round(map.Y(hour * 60)), line, divider);
        }
        for (var day = 0; day <= grid.Days; day++)
        {
            var x = grid.Column(day);
            PopupPaint.Fill(dc, new D2D_RECT_F(x, map.Top, x + line, map.Bottom), 0, divider);
        }
        foreach (var block in grid.Blocks)
        {
            Block(dc, gfx, plan, block, theme, words: false);
        }
        if (grid.NowY is { } now && grid.NowDay >= 0 && grid.NowDay < grid.Days)
        {
            // Now, across today's column, with a dot where it starts.
            var left = grid.Column(grid.NowDay);
            var end = grid.Column(grid.NowDay + 1);
            var thickness = MathF.Max(2, D(2));
            var accent = PopupPaint.Ink(theme.Accent);
            var (top, bottom) = (now - thickness / 2, now + thickness / 2);
            // It crosses an event's block but never its words: it breaks off where a line of them is,
            // with a little air either side, and goes on after it.
            var gaps = grid.Blocks
                .SelectMany(block => WordBoxes(gfx, plan, block))
                .Where(words => words.top < bottom + D(1) && words.bottom > top - D(1) && words.right > left && words.left < end)
                .Select(words => (From: words.left - D(2), To: words.right + D(2)))
                .OrderBy(gap => gap.From);
            var from = left;
            foreach (var (gapFrom, gapTo) in gaps)
            {
                if (gapFrom > from)
                {
                    PopupPaint.Fill(dc, new D2D_RECT_F(from, top, MathF.Min(gapFrom, end), bottom), 0, accent);
                }
                from = MathF.Max(from, gapTo);
            }
            if (from < end)
            {
                PopupPaint.Fill(dc, new D2D_RECT_F(from, top, end, bottom), 0, accent);
            }
            PopupPaint.Circle(dc, new Vector2(left + line + D(3), now), D(4), accent);
        }
        // What the blocks say goes over the now-line.
        foreach (var block in grid.Blocks)
        {
            Block(dc, gfx, plan, block, theme, words: true);
        }
    }
    /// <summary>
    /// An event's block: tinted in its calendar's colour with a stripe, then what the layout gave it to
    /// say (EventText) at the view's block size: the title's lines, then its time and place. In two
    /// passes, the block (<paramref name="words"/> false) and then its words, so the now-line goes between.
    /// </summary>
    private static void Block(ID2D1DeviceContext* dc, Gfx gfx, CalendarPlan plan, EventBlock block, FlyoutTheme theme, bool words)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var box = block.Box;
        if (box.right - box.left < 2 || box.bottom - box.top < 2)
        {
            return;
        }
        if (!words)
        {
            PopupPaint.Fill(dc, box, D(4), Tint(block.Colour, theme));
            if (theme.HighContrast)
            {
                PopupPaint.Stroke(dc, box, D(4), MathF.Max(1, PopupPaint.Round(plan.Scale)), theme.Text);
            }
            var stripe = new D2D_RECT_F(box.left, box.top, box.left + plan.Bar, box.bottom);
            PopupPaint.Fill(dc, stripe, plan.Bar / 2, Solid(block.Colour, theme));
            return;
        }
        var left = box.left + plan.Bar + D(5);
        var room = box.right - left - D(3);
        // A block too small to say anything says nothing.
        if (room < 4 || block.Text is not { } says || says.Title.Count + says.Details.Count == 0)
        {
            return;
        }
        dc->PushAxisAlignedClip(&box, D2D1_ANTIALIAS_MODE.D2D1_ANTIALIAS_MODE_ALIASED);
        try
        {
            var y = box.top + D(2);
            foreach (var line in says.Title)
            {
                PopupPaint.Text(dc, gfx, line, says.Px, Weight.Semibold, theme.Text, left, y, room);
                y += says.Line;
            }
            foreach (var line in says.Details)
            {
                PopupPaint.Text(dc, gfx, line, says.Px, Weight.Regular, theme.TextSecondary, left, y, room);
                y += says.Line;
            }
        }
        finally
        {
            dc->PopAxisAlignedClip();
        }
    }

    /// <summary>
    /// Where a block's words stand, a box per line as wide as its ink goes (within the block), as
    /// <see cref="Block"/> writes them: what the now-line breaks off for.
    /// </summary>
    private static IEnumerable<D2D_RECT_F> WordBoxes(Gfx gfx, CalendarPlan plan, EventBlock block)
    {
        float D(float dip) => PopupPaint.Round(dip * plan.Scale);
        var box = block.Box;
        var left = box.left + plan.Bar + D(5);
        var room = box.right - left - D(3);
        if (box.right - box.left < 2 || box.bottom - box.top < 2 || room < 4 || block.Text is not { } says)
        {
            yield break;
        }
        var y = box.top + D(2);
        foreach (var (line, weight) in says.Title.Select(line => (line, Weight.Semibold)).Concat(says.Details.Select(line => (line, Weight.Regular))))
        {
            if (y >= box.bottom)
            {
                yield break;
            }
            var width = MathF.Min(room, PopupPaint.Measure(gfx, line, says.Px, weight, room).X);
            yield return new D2D_RECT_F(left, y, left + width, MathF.Min(box.bottom, y + says.Line));
            y += says.Line;
        }
    }

    /// <summary>One line at <paramref name="left"/>, centred across <paramref name="row"/>.</summary>
    private static void Row(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float left, D2D_RECT_F row, float room) =>
        Line(dc, gfx, text, px, weight, color, left, row, room);

    private static void Line(ID2D1DeviceContext* dc, Gfx gfx, string text, float px, Weight weight, DXGI_RGBA color, float left, D2D_RECT_F row, float room)
    {
        var size = PopupPaint.Measure(gfx, text, px, weight, room);
        PopupPaint.Text(dc, gfx, text, px, weight, color, left, row.top + PopupPaint.Round((row.bottom - row.top - size.Y) / 2), room);
    }
}
