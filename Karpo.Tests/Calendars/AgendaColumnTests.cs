// The agenda in one column (2026-10-28: "the agenda should not have the date on the side, it should
// have it on top. (only one column)"; docs\widgets.md §378): the header stands over the events at
// every height taller than one row, never in a column beside them. On the kit's blocks (§401) the header
// is the calendar's name (40 DIP, §419) and each event one compact row of one line (§413, 32 DIP since §418) — its colour bar in the leading column, its title at the
// text edge, whose holiday it is after the title, when it is on the right (§414) — and the events are batched by day: every day, today too, a heading
// of its own ("Today", "Tomorrow") over its events, none of which says its day again (§401 had put it in each caption; 2026-10-29: "the
// agenda view should show the date batched and not for each entry"; 2026-10-30: "the agenda view should show todays date just
// normally and not this special as it does now"). On the made-up busy week (ListFormCases.Busy) at the frozen
// Tuesday afternoon, at every height from one row to the line's most and half-way between, at 100 and
// 150 % and at ordinary and the largest text. Needs Direct2D: KARPO_UI_TESTS=1.

using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using System.Numerics;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Calendars;

public sealed class AgendaColumnTests
{
    private static readonly float[] Scales = [1f, 1.5f];
    private static readonly float[] TextScales = [1f, 2.25f];

    /// <summary>The header's name: the calendar's, no date (§419).</summary>
    private static readonly string[] Page = ["head"];

    private static bool Listed(LinePiece piece) =>
        piece.Name.StartsWith("event ", StringComparison.Ordinal) || piece.Name.StartsWith("when ", StringComparison.Ordinal)
        || piece.Name.StartsWith("colour ", StringComparison.Ordinal) || piece.Name.StartsWith("day ", StringComparison.Ordinal)
        || piece.Name.StartsWith("time ", StringComparison.Ordinal);

    private static IEnumerable<(float Height, string Said)> Heights(TileStyle style)
    {
        var most = ListWidgets.Of(ListKind.Calendar).Range.Most;
        for (var rows = 1; rows <= most; rows++)
        {
            yield return (LineRows.Px(rows, style), $"{rows} rows");
            if (rows < most)
            {
                yield return (PopupPaint.Round((LineRows.Px(rows, style) + LineRows.Px(rows + 1, style)) / 2), $"{rows}½ rows");
            }
        }
    }

    private static TileStyle StyleFor(float scale, float textScale)
    {
        var look = ListFormPictures.LookFor(dark: true, textScale);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static string Say(LinePiece piece) => $"{piece.Name} \"{piece.Text}\" {piece.Box.left:0},{piece.Box.top:0}..{piece.Box.right:0},{piece.Box.bottom:0}";

    private static float Middle(LinePiece piece) => (piece.Box.top + piece.Box.bottom) / 2;

    /// <summary>The kit's compact row, in px: an event's and a day's heading's (§418, 2026-10-30: "make the agenda view a little more dense").</summary>
    private static float Compact(TileStyle style) => PopupPaint.Round(32 * style.Scale * style.TextScale);

    /// <summary>
    /// Every event is one row of one line (2026-10-29: "the entries contents should be shown in one line"): the kit's
    /// compact row, 32 DIP, as a day's heading, under the header's 40 (§418), the line as tall as its header, its events
    /// and its headings, every heading's name inside its own row; in the row its title
    /// and, after it on the same line, whose holiday it is (the caption that stood under it), cut at the trailing
    /// gutter, and when it is on that line too (§414). On the sample week with a holiday of two
    /// countries today and a long-titled one on Thursday, at 100 and 150 % and 100 and 225 % text.
    /// </summary>
    [UiFact]
    public void EveryEventIsOneRowOfOneLine()
    {
        OnStaThread(gfx =>
        {
            var now = ListFormCases.Frozen;
            const string Whose = "Switzerland, United Kingdom";
            CalendarIsland.Made = ListFormCases.Store(
            [
                .. ListFormCases.Week.Select(item => item.AllDay ? item with { Whose = Whose } : item),
                new(new DateTime(2026, 9, 24), new DateTime(2026, 9, 25), true, "A holiday whose name is far longer than any agenda is wide", null, Whose: Whose),
            ]);
            Clock.Now = () => now;
            try
            {
                using var island = new CalendarIsland("test/agenda", ListKind.Calendar.New(), gfx, new Karpo.Core.Config());
                var events = island.Agenda(now);
                // To look at: agenda-one-line-<dark|light>.png, at 150 %.
                foreach (var dark in new[] { true, false })
                {
                    ListLinesTests.Sheet(gfx, island, ListKind.Calendar, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"agenda-one-line-{(dark ? "dark" : "light")}.png"));
                }
                var wrong = new List<string>();
                foreach (var scale in Scales)
                {
                    foreach (var textScale in TextScales)
                    {
                        var style = StyleFor(scale, textScale);
                        var form = ListForm.Of(gfx, style);
                        var header = LineRows.Px(1, style);
                        var row = Compact(style);
                        var width = FolderFlyout.ListWidth(scale);
                        var at = $"{scale * 100:0} %, text {textScale * 100:0} %";
                        var headed = events.Select(entry => entry.Day).Distinct().Count();
                        var height = ((IListContent)island).ListHeight(width, style);
                        if (MathF.Abs(height - (header + (row * (events.Count + headed)))) > 0.5f)
                        {
                            wrong.Add($"{at}: the line {height:0} px, not the header's {header:0} and {events.Count} events and {headed} headings of {row:0} px each");
                        }
                        var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        var y = header;
                        for (var index = 0; index < events.Count; index++)
                        {
                            var (day, item) = events[index];
                            if (index == 0 || events[index - 1].Day != day)
                            {
                                var heading = pieces.Single(piece => piece.Name == $"day {day:yyyy-MM-dd}");
                                if (heading.Box.top < y - 0.5f || heading.Box.bottom > y + row + 0.5f)
                                {
                                    wrong.Add($"{at}: {Say(heading)} outside its row {y:0}..{y + row:0}");
                                }
                                y += row;
                            }
                            var own = pieces.Where(piece => piece.Name.EndsWith($" {index}", StringComparison.Ordinal) && !piece.Name.StartsWith("day ", StringComparison.Ordinal)).ToList();
                            foreach (var piece in own.Where(piece => piece.Box.top < y - 0.5f || piece.Box.bottom > y + row + 0.5f))
                            {
                                wrong.Add($"{at}: {Say(piece)} outside its row {y:0}..{y + row:0}");
                            }
                            // The content: every piece of words on the title's line.
                            var title = pieces.Single(piece => piece.Name == $"event {index}");
                            if (MathF.Abs(Middle(title) - (y + (row / 2))) > 1.5f)
                            {
                                wrong.Add($"{at}: {Say(title)} not on its row's middle {y + (row / 2):0}");
                            }
                            foreach (var piece in own.Where(piece => piece.Text.Length > 0 && (piece.Name.StartsWith("when ", StringComparison.Ordinal) || piece.Name.StartsWith("time ", StringComparison.Ordinal))))
                            {
                                if (MathF.Abs(Middle(piece) - Middle(title)) > 1.5f || piece.Box.left < title.Box.right - 0.5f)
                                {
                                    wrong.Add($"{at}: {Say(piece)} not after {Say(title)} on its line");
                                }
                                if (piece.Box.right > width - form.Inset + 0.5f)
                                {
                                    wrong.Add($"{at}: {Say(piece)} past the trailing gutter");
                                }
                            }
                            var said = own.Any(piece => piece.Name == $"when {index}");
                            if (item.Whose is { Length: > 0 } && !said && title.Text.Length > 0 && title.Box.right < width / 2)
                            {
                                wrong.Add($"{at}: {Say(title)} does not say whose it is after its title");
                            }
                            y += row;
                        }
                    }
                }
                Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
            }
            finally
            {
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// Taller than one row, the header stands on top: every event, its colour, its time and its day's heading stand
    /// under it; the titles begin at one edge, the colours in the leading column before it — one column, nothing beside it.
    /// </summary>
    [UiFact]
    public void TheHeaderStandsOnTopInOneColumn()
    {
        OnStaThread(gfx =>
        {
            using var stage = new ListLinesTests.Stage(gfx);
            var line = (ILinePieces)stage[ListKind.Calendar];
            var wrong = new List<string>();
            foreach (var scale in Scales)
            {
                foreach (var textScale in TextScales)
                {
                    var style = StyleFor(scale, textScale);
                    var width = FolderFlyout.ListWidth(scale);
                    foreach (var (height, said) in Heights(style))
                    {
                        var pieces = line.LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        if (pieces.Any(piece => piece.Name == "next"))
                        {
                            continue;
                        }
                        var at = $"{said}, {scale * 100:0} %, text {textScale * 100:0} %";
                        var page = pieces.Where(piece => Page.Contains(piece.Name)).ToList();
                        if (page.Count == 0)
                        {
                            wrong.Add($"{at}: no header");
                            continue;
                        }
                        var foot = page.Max(piece => piece.Box.bottom);
                        foreach (var piece in pieces.Where(Listed))
                        {
                            if (piece.Box.top < foot - 0.5f)
                            {
                                wrong.Add($"{at}: {Say(piece)} beside the header, whose foot is at {foot:0}");
                            }
                        }
                        // The titles begin at one edge, past the times and the colours (§408); a caption follows its title (§413).
                        var words = pieces.Where(piece => piece.Name.StartsWith("event ", StringComparison.Ordinal)).ToList();
                        var start = words.Count > 0 ? words.Min(piece => piece.Box.left) : 0;
                        foreach (var piece in words)
                        {
                            if (MathF.Abs(piece.Box.left - start) > 0.5f)
                            {
                                wrong.Add($"{at}: {Say(piece)} does not begin where the other events do, at {start:0}");
                            }
                        }
                        foreach (var piece in pieces.Where(piece => piece.Name.StartsWith("colour ", StringComparison.Ordinal)))
                        {
                            if (piece.Box.right > start - 0.5f)
                            {
                                wrong.Add($"{at}: {Say(piece)} is not before the titles, at {start:0}");
                            }
                        }
                        if (!pieces.Any(piece => piece.Name.StartsWith("event ", StringComparison.Ordinal)) && height > LineRows.Px(2, style) + 0.5f)
                        {
                            wrong.Add($"{at}: no event under the header");
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
        });
    }

    /// <summary>
    /// The whole agenda, every event shown, its events batched by day (2026-10-29: "the agenda view should show the
    /// date batched and not for each entry"): every day, today too (§419), a heading of its own on the gutter — "Today",
    /// "Tomorrow", then the long date — over its events, in order, and no event says its day again;
    /// its caption keeps only whose holiday it is. On the sample week (three days: today, Thursday, Saturday) and the
    /// busy one, drawn as tall as the line says it is, so every heading is counted in its height.
    /// </summary>
    [UiFact]
    public void EveryDayIsAHeadingOverItsEvents()
    {
        OnStaThread(gfx =>
        {
            foreach (var few in new[] { true, false })
            {
                using var stage = new ListLinesTests.Stage(gfx, few: few);
                var island = (CalendarIsland)stage[ListKind.Calendar];
                var now = ListFormCases.Frozen;
                var today = DateOnly.FromDateTime(now);
                var events = island.Agenda(now);
                var later = events.Select(entry => entry.Day).Distinct().ToList();
                Assert.True(later.Count >= 3 && later[0] == today);
                // To look at: agenda-days-<week|busy>-<dark|light>.png, at 150 %.
                foreach (var dark in new[] { true, false })
                {
                    ListLinesTests.Sheet(gfx, island, ListKind.Calendar, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"agenda-days-{(few ? "week" : "busy")}-{(dark ? "dark" : "light")}.png"));
                }
                // And the largest text at 100 %: agenda-days-<week|busy>-text-225.png.
                ListLinesTests.Sheet(gfx, island, ListKind.Calendar, 1f, ListFormPictures.LookFor(dark: true, 2.25f), Path.Combine(Pictures, $"agenda-days-{(few ? "week" : "busy")}-text-225.png"));
                foreach (var scale in Scales)
                {
                    foreach (var textScale in TextScales)
                    {
                        var style = StyleFor(scale, textScale);
                        var form = ListForm.Of(gfx, style);
                        var width = FolderFlyout.ListWidth(scale);
                        var height = ((IListContent)island).ListHeight(width, style);
                        var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        var at = $"{(few ? "week" : "busy week")}, {scale * 100:0} %, text {textScale * 100:0} %";
                        var headings = pieces.Where(piece => piece.Name.StartsWith("day ", StringComparison.Ordinal)).OrderBy(piece => piece.Box.top).ToList();
                        Assert.True(
                            later.Select(day => $"day {day:yyyy-MM-dd} \"{CalendarText.DayName(day, today)}\"").SequenceEqual(headings.Select(piece => $"{piece.Name} \"{piece.Text}\"")),
                            $"{at}: headings [{string.Join(", ", headings.Select(Say))}], not one for each of {string.Join(", ", later)}");
                        foreach (var heading in headings)
                        {
                            Assert.True(MathF.Abs(heading.Box.left - form.Inset) <= 0.5f, $"{at}: {Say(heading)} not on the gutter at {form.Inset:0}");
                            // A day reads as a group: the heading nearer its own first event than the day before's last (§418).
                            var over = pieces.Where(piece => (piece.Name == "head" || piece.Name.StartsWith("event ", StringComparison.Ordinal)) && piece.Box.bottom <= heading.Box.top + 0.5f).Max(piece => piece.Box.bottom);
                            var under = pieces.Where(piece => piece.Name.StartsWith("event ", StringComparison.Ordinal) && piece.Box.top >= heading.Box.bottom - 0.5f).Min(piece => piece.Box.top);
                            Assert.True(heading.Box.top - over > under - heading.Box.bottom + 0.5f,
                                $"{at}: {Say(heading)} {heading.Box.top - over:0} px under the words over it, not more than the {under - heading.Box.bottom:0} over its first event");
                        }
                        for (var index = 0; index < events.Count; index++)
                        {
                            var (day, item) = events[index];
                            var title = Assert.Single(pieces, piece => piece.Name == $"event {index}");
                            // The nearest day said over the event is its own, today's too.
                            var over = headings.Where(piece => piece.Box.bottom <= title.Box.top + 0.5f).MaxBy(piece => piece.Box.bottom);
                            var want = $"day {day:yyyy-MM-dd}";
                            Assert.True(over.Name == want, $"{at}: {Say(title)} stands under {Say(over)}, not under {want}");
                            if (item.Whose is not { Length: > 0 } whose)
                            {
                                if (pieces.FirstOrDefault(piece => piece.Name == $"when {index}") is { Name: not null } said)
                                {
                                    Assert.Fail($"{at}: {Say(said)} says its day again");
                                }
                                continue;
                            }
                            var when = Assert.Single(pieces, piece => piece.Name == $"when {index}");
                            Assert.True(MathF.Abs(Middle(when) - Middle(title)) <= 1.5f && when.Box.left >= title.Box.right - 0.5f, $"{at}: {Say(when)} is not after {Say(title)} on its line");
                            Assert.EndsWith(whose, when.Text);
                        }
                    }
                }
            }
        });
    }

    /// <summary>
    /// Today is a day like any other (2026-10-30: "the agenda view should show todays date just normally and not this special
    /// as it does now", §419): the header says no date — it is the kit's plain Header, the calendar's name, its refresh at
    /// its end, as mail's and the feed's — and today has a heading of its own, "Today", worded as the others are
    /// (<see cref="CalendarText.DayName"/>) and standing as they do: as tall, on the same gutter, as far into its row.
    /// On the sample week and the busy one, at 100 and 150 % and 100 and 225 % text.
    /// </summary>
    [UiFact]
    public void TodayIsAHeadingLikeAnyOtherDay()
    {
        OnStaThread(gfx =>
        {
            foreach (var few in new[] { true, false })
            {
                using var stage = new ListLinesTests.Stage(gfx, few: few);
                var island = (CalendarIsland)stage[ListKind.Calendar];
                var now = ListFormCases.Frozen;
                var today = DateOnly.FromDateTime(now);
                var events = island.Agenda(now);
                var wrong = new List<string>();
                foreach (var scale in Scales)
                {
                    foreach (var textScale in TextScales)
                    {
                        var style = StyleFor(scale, textScale);
                        var width = FolderFlyout.ListWidth(scale);
                        var header = LineRows.Px(1, style);
                        var row = Compact(style);
                        var height = ((IListContent)island).ListHeight(width, style);
                        var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        var at = $"{(few ? "week" : "busy week")}, {scale * 100:0} %, text {textScale * 100:0} %";
                        // The header: the calendar's name and nothing that says a day.
                        var head = pieces.Where(piece => piece.Box.top < header - 0.5f && piece.Text.Length > 0 && piece.Name != "glyph").ToList();
                        if (head.Count != 1 || head[0].Name != "head" || head[0].Text != Karpo.Core.WidgetInfos.Calendar.Title)
                        {
                            wrong.Add($"{at}: the header says [{string.Join(", ", head.Select(Say))}], not \"{Karpo.Core.WidgetInfos.Calendar.Title}\" alone");
                        }
                        var weekday = Dates.Format(now, "dddd");
                        var month = Dates.Format(now, "MMMM");
                        foreach (var piece in head.Where(piece => piece.Text.Contains(weekday, StringComparison.Ordinal) || piece.Text.Contains(month, StringComparison.Ordinal)))
                        {
                            wrong.Add($"{at}: {Say(piece)}: a date in the header");
                        }
                        // Every day's heading, today's first: its words, its height, its start and its place in its row alike.
                        var y = header;
                        var laid = new List<(LinePiece Heading, float Into)>();
                        for (var index = 0; index < events.Count; index++)
                        {
                            var day = events[index].Day;
                            if (index == 0 || events[index - 1].Day != day)
                            {
                                if (pieces.FirstOrDefault(piece => piece.Name == $"day {day:yyyy-MM-dd}") is not { Name: not null } heading)
                                {
                                    wrong.Add($"{at}: no heading for {day:yyyy-MM-dd}{(day == today ? ", today" : "")}");
                                }
                                else
                                {
                                    if (heading.Text != CalendarText.DayName(day, today))
                                    {
                                        wrong.Add($"{at}: {Say(heading)}, not \"{CalendarText.DayName(day, today)}\"");
                                    }
                                    laid.Add((heading, heading.Box.top - y));
                                }
                                y += row;
                            }
                            y += row;
                        }
                        if (laid.Count > 0 && laid[0].Heading.Text != S.DockCalendarToday)
                        {
                            wrong.Add($"{at}: the first heading {Say(laid[0].Heading)}, not \"{S.DockCalendarToday}\"");
                        }
                        foreach (var (heading, into) in laid.Skip(1))
                        {
                            var (first, firstInto) = laid[0];
                            if (MathF.Abs((heading.Box.bottom - heading.Box.top) - (first.Box.bottom - first.Box.top)) > 0.5f
                                || MathF.Abs(heading.Box.left - first.Box.left) > 0.5f || MathF.Abs(into - firstInto) > 0.5f)
                            {
                                wrong.Add($"{at}: {Say(heading)} {into:0} px into its row, unlike {Say(first)} {firstInto:0} px into its");
                            }
                        }
                    }
                }
                Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
            }
        });
    }

    /// <summary>
    /// Today always stands first (2026-10-30: "in the agenda today should also appear if no event is there and then it
    /// should just say no event"): with nothing on today, its heading "Today" over one compact row that says "No events"
    /// at the text edge — the kit's Status row, no item: a press there is no event's — then tomorrow's heading and the
    /// later days as before; with nothing in the whole week, "Today" over "No events" and nothing else. The line's height
    /// counts the row. On the sample week without today's events and on an empty one, at 100 and 150 % and 100 and 225 % text.
    /// </summary>
    [UiFact]
    public void AnEmptyTodayIsItsHeadingOverNoEvents()
    {
        OnStaThread(gfx =>
        {
            var now = ListFormCases.Frozen;
            var today = DateOnly.FromDateTime(now);
            Clock.Now = () => now;
            try
            {
                foreach (var nothing in new[] { false, true })
                {
                    CalendarIsland.Made = ListFormCases.Store(nothing ? [] : [.. ListFormCases.Week.Where(item => item.Start.Date != now.Date)]);
                    using var island = new CalendarIsland("test/agenda", ListKind.Calendar.New(), gfx, new Karpo.Core.Config());
                    var events = island.Agenda(now);
                    Assert.Equal(nothing, events.Count == 0);
                    Assert.DoesNotContain(events, entry => entry.Day == today);
                    // To look at: agenda-empty-today-<dark|light>.png and agenda-nothing-<dark|light>.png, at 150 %.
                    foreach (var dark in new[] { true, false })
                    {
                        ListLinesTests.Sheet(gfx, island, ListKind.Calendar, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"agenda-{(nothing ? "nothing" : "empty-today")}-{(dark ? "dark" : "light")}.png"));
                    }
                    // Top to bottom: the header, "Today" over "No events", then every later day over its events.
                    string[] want =
                    [
                        $"head {Karpo.Core.WidgetInfos.Calendar.Title}",
                        $"day {today:yyyy-MM-dd} {S.DockCalendarToday}",
                        $"status {S.DockCalendarNoEvents}",
                        .. events.SelectMany((entry, index) => (index == 0 || events[index - 1].Day != entry.Day
                            ? new[] { $"day {entry.Day:yyyy-MM-dd} {CalendarText.DayName(entry.Day, today)}" }
                            : []).Append($"event {index} {entry.Event.Title}")),
                    ];
                    var wrong = new List<string>();
                    foreach (var scale in Scales)
                    {
                        foreach (var textScale in TextScales)
                        {
                            var style = StyleFor(scale, textScale);
                            var form = ListForm.Of(gfx, style);
                            var header = LineRows.Px(1, style);
                            var row = Compact(style);
                            var width = FolderFlyout.ListWidth(scale);
                            var at = $"{(nothing ? "nothing" : "nothing today")}, {scale * 100:0} %, text {textScale * 100:0} %";
                            var headed = events.Select(entry => entry.Day).Distinct().Count();
                            var height = ((IListContent)island).ListHeight(width, style);
                            if (MathF.Abs(height - (header + (row * (2 + events.Count + headed)))) > 0.5f)
                            {
                                wrong.Add($"{at}: the line {height:0} px, not the header's {header:0}, today's heading and its row and {events.Count} events and {headed} headings of {row:0} px each");
                            }
                            var box = PopupPaint.Rect(0, 0, width, height);
                            var pieces = ((ILinePieces)island).LinePieces(box, style);
                            var said = pieces.Where(piece => piece.Text.Length > 0 && (piece.Name is "head" or "status" || piece.Name.StartsWith("day ", StringComparison.Ordinal) || piece.Name.StartsWith("event ", StringComparison.Ordinal)))
                                .OrderBy(piece => piece.Box.top).Select(piece => $"{piece.Name} {piece.Text}").ToList();
                            if (!said.SequenceEqual(want))
                            {
                                wrong.Add($"{at}: says [{string.Join(", ", said)}], not [{string.Join(", ", want)}]");
                                continue;
                            }
                            // The row under today's heading: its words at the text edge on its middle, inside it.
                            var status = pieces.Single(piece => piece.Name == "status");
                            var top = header + row;
                            if (status.Box.top < top - 0.5f || status.Box.bottom > top + row + 0.5f || MathF.Abs(Middle(status) - (top + (row / 2))) > 1.5f)
                            {
                                wrong.Add($"{at}: {Say(status)} not on the middle of its row {top:0}..{top + row:0}");
                            }
                            if (MathF.Abs(status.Box.left - form.TextLeft) > 0.5f)
                            {
                                wrong.Add($"{at}: {Say(status)} not at the text edge {form.TextLeft:0}");
                            }
                            // No item: a press on it is no event's.
                            if (island.CardTarget(new Vector2(status.Box.left + 1, Middle(status)), box, style, WidgetSurface.List) != 0)
                            {
                                wrong.Add($"{at}: a press on {Say(status)} lands on an event");
                            }
                            // Tomorrow's heading a compact row under it, as any day's under the day before.
                            if (!nothing)
                            {
                                var next = pieces.Single(piece => piece.Name == $"day {events[0].Day:yyyy-MM-dd}");
                                var first = pieces.Single(piece => piece.Name == $"day {today:yyyy-MM-dd}");
                                if (MathF.Abs(next.Box.top - first.Box.top - (2 * row)) > 0.5f)
                                {
                                    wrong.Add($"{at}: {Say(next)} not two compact rows under {Say(first)}");
                                }
                            }
                        }
                    }
                    Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
                }
            }
            finally
            {
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// An event longer than a day stands under every day of it the agenda lists (one going on since yesterday under
    /// today): on its first day "From 09:00", on a day in the middle of it "All day", on its last "Until 16:00", each
    /// on the right of its row (§414); an all-day one of two days "All day" under both.
    /// </summary>
    [UiFact]
    public void ALongerEventStandsUnderEveryDayItSpans()
    {
        OnStaThread(gfx =>
        {
            var now = ListFormCases.Frozen;
            CalendarIsland.Made = ListFormCases.Store(
            [
                new(new DateTime(2026, 9, 21, 22, 0, 0), new DateTime(2026, 9, 22, 16, 0, 0), false, "Night shift", null),
                new(new DateTime(2026, 9, 23, 9, 0, 0), new DateTime(2026, 9, 25, 17, 0, 0), false, "Conference", null),
                new(new DateTime(2026, 9, 24), new DateTime(2026, 9, 26), true, "Trip", null),
            ]);
            Clock.Now = () => now;
            try
            {
                using var island = new CalendarIsland("test/agenda", ListKind.Calendar.New(), gfx, new Karpo.Core.Config());
                var events = island.Agenda(now);
                Assert.Equal(
                    [
                        $"22 Night shift {S.DockCalendarUntil("16:00")}",
                        $"23 Conference {S.DockCalendarFrom("09:00")}",
                        $"24 Conference {S.DockCalendarAllDay}",
                        $"24 Trip {S.DockCalendarAllDay}",
                        $"25 Trip {S.DockCalendarAllDay}",
                        $"25 Conference {S.DockCalendarUntil("17:00")}",
                    ],
                    events.Select(entry => $"{entry.Day.Day} {entry.Event.Title} {CalendarText.Span(entry.Event, entry.Day)}"));
                var style = StyleFor(1, 1);
                var width = FolderFlyout.ListWidth(1);
                var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, ((IListContent)island).ListHeight(width, style)), style);
                Assert.Equal(
                    [S.DockCalendarUntil("16:00"), S.DockCalendarFrom("09:00"), S.DockCalendarAllDay, S.DockCalendarAllDay, S.DockCalendarAllDay, S.DockCalendarUntil("17:00")],
                    Enumerable.Range(0, events.Count).Select(index => pieces.SingleOrDefault(piece => piece.Name == $"time {index}").Text));
                Assert.Equal(["day 2026-09-22", "day 2026-09-23", "day 2026-09-24", "day 2026-09-25"], pieces.Where(piece => piece.Name.StartsWith("day ", StringComparison.Ordinal)).Select(piece => piece.Name));
            }
            finally
            {
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// Every event says when it is, always (2026-10-29: "it should not be possible to not show the time in the agenda
    /// view"), on the right and on one line (2026-10-29: "start stop should be shown on the right side and not on the
    /// left. also in one row / line"): the row's reading, "09:00–10:00" (<see cref="CalendarText.Span"/>: "From 09:00" on a
    /// longer event's first day, "Until 17:00" on its last, "All day"), on the title's line, ending on the trailing
    /// gutter or a gap before a meeting's Join; nothing of the event left of its title but its colour bar in the leading
    /// column, every title at the text edge; a long title cut before the time is, but never to nothing: where a meeting's
    /// Join and its whole span would leave its title no room (225 % text) it says its start alone. An agenda line once
    /// told to hide its times shows them all the same. On the sample week with a meeting of a long title and a three-day
    /// conference.
    /// </summary>
    [UiFact]
    public void EveryEventSaysWhenOnItsTitlesLineOnTheRight()
    {
        OnStaThread(gfx =>
        {
            var now = ListFormCases.Frozen;
            CalendarIsland.Made = ListFormCases.Store(
            [
                .. ListFormCases.Week,
                new(new DateTime(2026, 9, 22, 16, 0, 0), new DateTime(2026, 9, 22, 17, 0, 0), false,
                    "Quarterly planning with the whole regional sales and marketing team", null, Join: "https://teams.microsoft.com/meet/2394857612345?p=AbCdEfGhIjKlMnOp"),
                new(new DateTime(2026, 9, 23, 9, 0, 0), new DateTime(2026, 9, 25, 17, 0, 0), false, "Conference", null),
            ]);
            Clock.Now = () => now;
            try
            {
                using var island = new CalendarIsland("test/agenda", ListKind.Calendar.New(), gfx, new Karpo.Core.Config());
                island.Settings.Items = new Karpo.Core.ItemStyleOptions { Time = false };
                var events = island.Agenda(now);
                Assert.Contains(events, entry => entry.Event.AllDay);
                // To look at: agenda-times-<dark|light>.png, at 150 %.
                foreach (var dark in new[] { true, false })
                {
                    ListLinesTests.Sheet(gfx, island, ListKind.Calendar, 1.5f, ListFormPictures.LookFor(dark), Path.Combine(Pictures, $"agenda-times-{(dark ? "dark" : "light")}.png"));
                }
                var wrong = new List<string>();
                foreach (var scale in Scales)
                {
                    foreach (var textScale in TextScales)
                    {
                        var style = StyleFor(scale, textScale);
                        var form = ListForm.Of(gfx, style);
                        var gap = style.Px(LineSpace.GapDip);
                        var line = TilePaint.LineHeight(gfx, form.TextPx);
                        var width = FolderFlyout.ListWidth(scale);
                        var height = ((IListContent)island).ListHeight(width, style);
                        var pieces = ((ILinePieces)island).LinePieces(PopupPaint.Rect(0, 0, width, height), style);
                        var at = $"{scale * 100:0} %, text {textScale * 100:0} %";
                        foreach (var piece in pieces.Where(piece => piece.Name.StartsWith("start ", StringComparison.Ordinal) || piece.Name.StartsWith("end ", StringComparison.Ordinal)))
                        {
                            wrong.Add($"{at}: {Say(piece)}: a time left of the titles");
                        }
                        for (var index = 0; index < events.Count; index++)
                        {
                            var (day, item) = events[index];
                            var bar = pieces.Single(piece => piece.Name == $"colour {index}");
                            if (pieces.FirstOrDefault(piece => piece.Name == $"event {index}") is not { Name: not null } title)
                            {
                                wrong.Add($"{at}: event {index} \"{item.Title}\" has no title shown: {string.Join(", ", pieces.Where(piece => piece.Name.EndsWith($" {index}", StringComparison.Ordinal)).Select(Say))}");
                                continue;
                            }
                            if (MathF.Abs(title.Box.left - form.TextLeft) > 0.5f)
                            {
                                wrong.Add($"{at}: {Say(title)} not at the text edge {form.TextLeft:0}");
                            }
                            if (bar.Box.left < form.Inset - 0.5f || bar.Box.right > form.TextLeft - 0.5f)
                            {
                                wrong.Add($"{at}: {Say(bar)} not in the leading column, {form.Inset:0}..{form.TextLeft:0}");
                            }
                            var own = pieces.Where(piece => piece.Name.EndsWith($" {index}", StringComparison.Ordinal) && piece.Name != $"colour {index}" && !piece.Name.StartsWith("day ", StringComparison.Ordinal)).ToList();
                            foreach (var piece in own.Where(piece => piece.Box.left < title.Box.left - 0.5f))
                            {
                                wrong.Add($"{at}: {Say(piece)} left of {Say(title)}");
                            }
                            var want = CalendarText.Span(item, day);
                            if (pieces.FirstOrDefault(piece => piece.Name == $"time {index}") is not { Name: not null } when)
                            {
                                wrong.Add($"{at}: event {index} \"{item.Title}\" says no time on the right, not \"{want}\"");
                                continue;
                            }
                            // Only where a meeting's Join takes its room at 225 % text may the time be its start alone, so the title still reads.
                            if (when.Text != want && !(textScale > 1 && item.Join is not null && when.Text == Dates.Time(item.Start)))
                            {
                                wrong.Add($"{at}: {Say(when)}, not \"{want}\"");
                            }
                            if (MathF.Abs(Middle(when) - Middle(title)) > 1.5f || when.Box.bottom - when.Box.top > line + 0.5f)
                            {
                                wrong.Add($"{at}: {Say(when)} not one line on the line of {Say(title)}");
                            }
                            var words = own.Where(piece => piece.Name.StartsWith("event ", StringComparison.Ordinal) || piece.Name.StartsWith("when ", StringComparison.Ordinal)).Max(piece => piece.Box.right);
                            if (when.Box.left < words + gap - 0.5f)
                            {
                                wrong.Add($"{at}: {Say(when)} not a gap past the title and its caption, ending at {words:0}");
                            }
                            var end = pieces.Where(piece => piece.Name == $"join {index}").Select(piece => piece.Box.left - gap).DefaultIfEmpty(width - form.Inset).First();
                            if (MathF.Abs(when.Box.right - end) > 0.5f)
                            {
                                wrong.Add($"{at}: {Say(when)} does not end at {end:0}, the trailing gutter or a gap before the Join");
                            }
                        }
                    }
                }
                Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
            }
            finally
            {
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });
    }

    /// <summary>
    /// A compact row is still the whole row to the pointer (§418): a meeting's Join lands from the row's top to its foot,
    /// its 24 DIP button grown to a button's reach (32 DIP, <see cref="LineButton.Hit(TerraFX.Interop.DirectX.D2D_RECT_F, float, float)"/>)
    /// and kept in its row; the rest of the row, its top edge over the title too, is the event's, which a click opens (§434);
    /// anywhere on the row it is the meeting a right click is about.
    /// On the sample week with a meeting today, at 100 and 150 % and 100 and 225 % text.
    /// </summary>
    [UiFact]
    public void AnEventsWholeRowIsItsHitArea()
    {
        OnStaThread(gfx =>
        {
            var now = ListFormCases.Frozen;
            CalendarIsland.Made = ListFormCases.Store(
            [
                .. ListFormCases.Week,
                new(new DateTime(2026, 9, 22, 16, 0, 0), new DateTime(2026, 9, 22, 17, 0, 0), false, "Weekly sync", null,
                    Join: "https://teams.microsoft.com/meet/2394857612345?p=AbCdEfGhIjKlMnOp"),
            ]);
            Clock.Now = () => now;
            // A calendar app to open: the sample calendar has no site.
            CalendarApp.Stand = () => "outlookcal:";
            try
            {
                using var island = new CalendarIsland("test/agenda", ListKind.Calendar.New(), gfx, new Karpo.Core.Config());
                var events = island.Agenda(now);
                var meeting = events.FindIndex(entry => entry.Event.Join is not null);
                Assert.True(meeting >= 0);
                var wrong = new List<string>();
                foreach (var scale in Scales)
                {
                    foreach (var textScale in TextScales)
                    {
                        var style = StyleFor(scale, textScale);
                        var row = Compact(style);
                        var width = FolderFlyout.ListWidth(scale);
                        var box = PopupPaint.Rect(0, 0, width, ((IListContent)island).ListHeight(width, style));
                        var pieces = ((ILinePieces)island).LinePieces(box, style);
                        var at = $"{scale * 100:0} %, text {textScale * 100:0} %";
                        var title = pieces.Single(piece => piece.Name == $"event {meeting}");
                        var join = pieces.Single(piece => piece.Name == $"join {meeting}");
                        // Under the header, every event and heading before it a compact row.
                        var top = LineRows.Px(1, style) + (row * (meeting + events.Take(meeting + 1).Select(entry => entry.Day).Distinct().Count()));
                        var x = (join.Box.left + join.Box.right) / 2;
                        foreach (var y in new[] { top + 0.5f, top + row - 0.5f })
                        {
                            if (island.CardTarget(new Vector2(x, y), box, style, WidgetSurface.List) != JoinButton.Target + meeting)
                            {
                                wrong.Add($"{at}: a press at {x:0},{y:0} in the row {top:0}..{top + row:0} misses {Say(join)}");
                            }
                        }
                        foreach (var y in new[] { top - 1.5f, top + row + 1.5f })
                        {
                            if (island.CardTarget(new Vector2(x, y), box, style, WidgetSurface.List) == JoinButton.Target + meeting)
                            {
                                wrong.Add($"{at}: a press at {x:0},{y:0} outside the row {top:0}..{top + row:0} lands on {Say(join)}");
                            }
                        }
                        // The rest of the row, its top edge over the title too, is the event's, which opens it (§434).
                        if (island.CardTarget(new Vector2(title.Box.left + 1, top + 0.5f), box, style, WidgetSurface.List) != meeting + 1)
                        {
                            wrong.Add($"{at}: the row's top edge over {Say(title)} is not its event's");
                        }
                        island.CardHover(new Vector2(title.Box.left + 1, top + 0.5f), box, style, WidgetSurface.List);
                        if (island.MenuMeeting()?.Title != events[meeting].Event.Title)
                        {
                            wrong.Add($"{at}: the row's top edge over {Say(title)} is not its meeting's");
                        }
                        island.CardHover(null, box, style, WidgetSurface.List);
                    }
                }
                Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
            }
            finally
            {
                CalendarIsland.Made = null;
                CalendarApp.Stand = null;
                Clock.Now = Clock.Real;
            }
        });
    }
}
