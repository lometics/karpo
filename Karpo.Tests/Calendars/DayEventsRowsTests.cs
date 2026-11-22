// The day's events look as the agenda's (the person, 2026-10-31: "for the days style, events should look similar as in
// agenda view"; docs\widgets.md §447): the calendar's "Day's events" style (EventsTile) draws its events with the
// agenda's own row (AgendaRows) — its calendar's colour in the leading column, its title at the text edge and whose
// holiday it is after it, when it is ("09:00–10:00", "All day") against the trailing gutter, a meeting's Join at its
// end — one compact row each, lit edge to edge and opening the event. Picture: day-events-<dark|light>.png in
// KARPO_PNG_DIR, the agenda, the day's events and the day beside each other. Needs Direct2D: KARPO_UI_TESTS=1.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Calendars;

public sealed unsafe class DayEventsRowsTests
{
    private const string Teams = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_abc%40thread.v2/0";

    /// <summary>The frozen Tuesday afternoon (ListFormCases.Frozen): 14:40.</summary>
    private static readonly DateTime Now = ListFormCases.Frozen;

    /// <summary>
    /// Today: a holiday of two countries all day, a focus block on now, a meeting coming (its Join lit), a long-titled
    /// call and an evening concert somewhere — every one still to come or going on, so the agenda lists them all too.
    /// </summary>
    internal static CalendarStore Store()
    {
        var day = Now.Date;
        var store = new CalendarStore();
        store.Add(new ListCalendar("work", "Work", CalendarColours.Of("#0F6CBD"),
        [
            new CalendarEvent(day, day.AddDays(1), true, "Autumn equinox", null, Whose: "Switzerland, United Kingdom"),
            new CalendarEvent(day.AddHours(14), day.AddHours(15), false, "Focus time", null),
            new CalendarEvent(day.AddHours(14.75), day.AddHours(15.25), false, "Design review", "Room 3", Join: Teams),
            new CalendarEvent(day.AddHours(16), day.AddHours(17), false, "Call with the bank about the mortgage and the new account", null),
            new CalendarEvent(day.AddHours(19), day.AddHours(22.5), false, "Concert", "Tonhalle", Colour: CalendarColours.Palette[2]),
        ]));
        return store;
    }

    private static T Line<T>(Gfx gfx, ListKind kind)
        where T : CellIsland
    {
        var item = kind.New();
        item.Id = kind.Id();
        var line = (T)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", item.Id), item, kind, gfx, new Karpo.Core.Config())!;
        _ = line.Refresh(gfx);
        return line;
    }

    private static TileStyle StyleFor(bool dark, float scale = 1.5f, float textScale = 1f)
    {
        var look = ListFormPictures.LookFor(dark, textScale);
        return FolderFlyout.StyleFor(scale, look, FlyoutTheme.For(look));
    }

    private static void With(Action<Gfx> body) => OnStaThread(gfx =>
    {
        Clock.Now = () => Now;
        CalendarIsland.Made = Store();
        CalendarApp.Stand = () => "outlookcal:";
        try
        {
            body(gfx);
        }
        finally
        {
            Clock.Now = Clock.Real;
            CalendarIsland.Made = null;
            CalendarApp.Stand = null;
        }
    });

    private static string Say(LinePiece piece) => $"{piece.Name} \"{piece.Text}\" {piece.Box.left:0},{piece.Box.top:0}..{piece.Box.right:0},{piece.Box.bottom:0}";

    /// <summary>An event's pieces on a line, by its title: its colour, its title, whose it is, when it is and its Join, each where there is one.</summary>
    private static Dictionary<string, LinePiece> Of(IReadOnlyList<LinePiece> pieces, string title)
    {
        var name = pieces.FirstOrDefault(piece => piece.Name.StartsWith("event ", StringComparison.Ordinal) && piece.Text == title);
        if (name.Name is null)
        {
            return [];
        }
        var index = name.Name["event ".Length..];
        return pieces.Where(piece => piece.Name.EndsWith($" {index}", StringComparison.Ordinal) && !piece.Name.StartsWith("day ", StringComparison.Ordinal))
            .ToDictionary(piece => piece.Name[..piece.Name.LastIndexOf(' ')]);
    }

    /// <summary>
    /// Every event of the day's events is the agenda's row (2026-10-31: "for the days style, events should look similar as in
    /// agenda view"): one compact row each (32 DIP, §418) under the day's heading — the line as tall as its heading and its
    /// events — its calendar's colour in the leading column, its title at the text edge, whose holiday it is after the title,
    /// when it is against the trailing gutter in the very words the agenda says it ("All day", "14:45–15:15"), a meeting's
    /// Join at its end: each piece where the agenda's same event has it, across, and as high in its row. Today's events at
    /// the frozen afternoon, every one listed by both, at 100 and 150 % and 100 and 225 % text, dark.
    /// </summary>
    [UiFact]
    public void TheDaysEventsAreTheAgendasRows()
    {
        With(gfx =>
        {
            using var agenda = Line<CalendarIsland>(gfx, ListKind.Calendar);
            using var events = Line<EventsTile>(gfx, ListKind.Events);
            var titles = CalendarStore.OnDay(Store().EventsIn(Now.Date, Now.Date.AddDays(1)), DateOnly.FromDateTime(Now)).Select(item => item.Title).ToList();
            var wrong = new List<string>();
            foreach (var scale in new[] { 1f, 1.5f })
            {
                foreach (var textScale in new[] { 1f, 2.25f })
                {
                    var style = StyleFor(dark: true, scale, textScale);
                    var at = $"{scale * 100:0} %, text {textScale * 100:0} %";
                    var width = FolderFlyout.ListWidth(scale);
                    var compact = LineRow.Compact(style);
                    var header = LineRow.Height(style, caption: false);
                    var tall = ((IListContent)events).ListHeight(width, style);
                    if (MathF.Abs(tall - (header + (compact * titles.Count))) > 0.5f)
                    {
                        wrong.Add($"{at}: the day's events {tall:0} px tall, not its heading's {header:0} and {titles.Count} rows of {compact:0}");
                    }
                    var theirs = ((ILinePieces)agenda).LinePieces(PopupPaint.Rect(0, 0, width, ((IListContent)agenda).ListHeight(width, style)), style);
                    var ours = ((ILinePieces)events).LinePieces(PopupPaint.Rect(0, 0, width, tall), style);
                    float? top = null;
                    foreach (var (title, index) in titles.Select((title, index) => (title, index)))
                    {
                        var agendas = Of(theirs, title);
                        var days = Of(ours, title);
                        if (days.Count == 0)
                        {
                            wrong.Add($"{at}: \"{title}\" is no event of the day's events: {string.Join(", ", ours.Select(Say))}");
                            continue;
                        }
                        // Each a compact row under the one before.
                        top ??= days["event"].Box.top - (index * compact);
                        if (MathF.Abs(days["event"].Box.top - (top.Value + (index * compact))) > 0.5f)
                        {
                            wrong.Add($"{at}: {Say(days["event"])} not a compact row ({compact:0} px) under the one before");
                        }
                        foreach (var (part, piece) in agendas)
                        {
                            if (!days.TryGetValue(part, out var mine))
                            {
                                wrong.Add($"{at}: \"{title}\" has no {part} in the day's events, the agenda's {Say(piece)}");
                                continue;
                            }
                            var shift = days["event"].Box.top - agendas["event"].Box.top;
                            if (MathF.Abs(mine.Box.left - piece.Box.left) > 0.5f || MathF.Abs(mine.Box.right - piece.Box.right) > 0.5f
                                || MathF.Abs(mine.Box.top - shift - piece.Box.top) > 0.5f || MathF.Abs(mine.Box.bottom - shift - piece.Box.bottom) > 0.5f)
                            {
                                wrong.Add($"{at}: \"{title}\"'s {Say(mine)} not where the agenda's {Say(piece)} is in its row");
                            }
                            if (mine.Text != piece.Text)
                            {
                                wrong.Add($"{at}: \"{title}\"'s {part} says \"{mine.Text}\", the agenda's \"{piece.Text}\"");
                            }
                        }
                        foreach (var part in days.Keys.Except(agendas.Keys))
                        {
                            wrong.Add($"{at}: \"{title}\" has a {part} the agenda's has not: {Say(days[part])}");
                        }
                    }
                }
            }
            Assert.True(wrong.Count == 0, $"{wrong.Count} wrong:\n{string.Join("\n", wrong.Take(40))}");
        });
    }

    // --- Pictures -----------------------------------------------------------------------------------

    /// <summary>
    /// The agenda, the day's events and the day beside each other on a folder's ground at 150 %, as tall as the agenda
    /// asks for, the meeting's row under the pointer in the agenda and the day's events: day-events-<dark|light>.png.
    /// </summary>
    [UiFact]
    public void TheyArePictured()
    {
        With(gfx =>
        {
            using var agenda = Line<CalendarIsland>(gfx, ListKind.Calendar);
            using var events = Line<EventsTile>(gfx, ListKind.Events);
            using var day = Line<CalendarViewTile>(gfx, ListKind.DayView);
            foreach (var dark in new[] { true, false })
            {
                var style = StyleFor(dark);
                var width = FolderFlyout.ListWidth(1.5f);
                var tall = ((IListContent)agenda).ListHeight(width, style);
                (string Name, CellIsland Line)[] lines = [("Agenda", agenda), ("Day's events", events), ("Day", day)];
                var pad = PopupPaint.Round(16 * 1.5f);
                var label = PopupPaint.Round(11 * 1.5f);
                var labelLine = TilePaint.LineHeight(gfx, label);
                var sheetWidth = pad + (lines.Length * (width + pad));
                var sheetHeight = (2 * pad) + labelLine + tall;
                var look = ListFormPictures.LookFor(dark);
                Bitmap(sheetWidth, sheetHeight, dc =>
                {
                    PopupPaint.Fill(dc, PopupPaint.Rect(0, 0, sheetWidth, sheetHeight), 0,
                        PopupPaint.Ink(dark ? Windows.UI.Color.FromArgb(255, 0x1C, 0x1C, 0x1C) : Windows.UI.Color.FromArgb(255, 0xE8, 0xE8, 0xE8)));
                    for (var index = 0; index < lines.Length; index++)
                    {
                        var (name, line) = lines[index];
                        var x = pad + (index * (width + pad));
                        PopupPaint.Text(dc, gfx, name, label, Weight.Regular, style.Theme.TextSecondary, x, pad);
                        var plate = PopupPaint.Rect(x, pad + labelLine, width, tall);
                        PopupPaint.Fill(dc, plate, PopupPaint.Round(8 * 1.5f), PopupPaint.Ink(style.Theme.Base));
                        PopupPaint.Stroke(dc, plate, PopupPaint.Round(8 * 1.5f), 2, PopupPaint.Ink(style.Theme.Stroke));
                        var box = PopupPaint.Rect(0, 0, width, tall);
                        if (line is ILinePieces pieced && pieced.LinePieces(box, style).FirstOrDefault(piece => piece.Text == "Design review") is { } review)
                        {
                            line.CardHover(new Vector2(review.Box.left + 2, (review.Box.top + review.Box.bottom) / 2), box, style, default);
                        }
                        var context = dc;
                        PopupPaint.Clipped(context, plate, PopupPaint.Round(8 * 1.5f), () => Shifted(context, plate.left, plate.top, () =>
                            FolderFlyout.PaintContent(context, line, box, style)));
                        line.CardHover(null, box, style, default);
                    }
                }, bitmap => Save(bitmap, Path.Combine(Pictures, $"day-events-{(dark ? "dark" : "light")}.png")));
            }
        });
    }
}
