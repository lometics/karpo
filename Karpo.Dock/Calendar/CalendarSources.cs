// Where the calendar's events come from. A source is one calendar: one of the
// calendars of the person's accounts (Pim\AccountCalendars.cs), which is what
// a real Karpo shows, or one a test or --smoke made up. The store
// (CalendarStore) merges what every source has, and the views only ever see
// CalendarEvents, each naming its source and colour, so a new kind of source
// needs no change to them.
//
// A Microsoft calendar comes already expanded (Graph's calendarView); a
// CalDAV calendar comes as iCalendar objects, which ICalendar expands here,
// in the zone the calendar is shown in (docs\accounts.md).
//
// A source works off the dock's thread and says so through Changed; asked for
// a range, it answers at once with what it knows, and a source that fetches by
// range (the accounts' window of events) starts fetching and raises Changed
// when it has more.

using System.Globalization;
using Karpo.Accounts;
using Karpo.Core;
using Color = Windows.UI.Color;

namespace Karpo.Dock;

/// <summary>
/// One occurrence of an event, in the wall-clock time of the zone it was asked for (this PC's
/// unless said otherwise). An all-day event starts at midnight and ends at the midnight after
/// its last day; an event without a length ends where it starts.
/// </summary>
/// <param name="Source">The calendar it comes from; null for one made by hand.</param>
/// <param name="Colour">Its calendar's colour; transparent means the accent.</param>
/// <param name="Join">Where its online meeting is joined (MeetingLinks); null for none.</param>
/// <param name="Free">Shown as free, or declined: the next meeting passes it by (docs\widgets.md §205).</param>
/// <param name="Whose">
/// A public holiday's calendars where several are listed ("Switzerland, United Kingdom", §265), which the
/// agenda says after when it is; null for every other event.
/// </param>
/// <param name="Link">
/// Where the event itself opens on its account's site (Outlook on the web's page of it, §434); null where the
/// account names none — a click on it then opens its calendar on its day (<see cref="EventLinks"/>).
/// </param>
public readonly record struct CalendarEvent(
    DateTime Start,
    DateTime End,
    bool AllDay,
    string Title,
    string? Location,
    ICalendarSource? Source = null,
    Color Colour = default,
    string? Join = null,
    bool Free = false,
    string? Whose = null,
    string? Link = null)
{
    /// <summary>The event's colour, or <paramref name="accent"/> for a calendar without one of its own.</summary>
    public Color ColourOr(Color accent) => Colour.A == 0 ? accent : Colour;
}

/// <summary>One calendar whose events Karpo shows.</summary>
public interface ICalendarSource : IDisposable
{
    /// <summary>Unique among the sources and the same from run to run (the account's id and the calendar's).</summary>
    string Id { get; }

    /// <summary>The calendar's name as its owner knows it.</summary>
    string Name { get; }

    /// <summary>The colour its events are marked with; transparent means the accent.</summary>
    Color Colour { get; }

    /// <summary>It has neither events nor a problem yet: its first read is still on its way.</summary>
    bool Loading { get; }

    /// <summary>Why it shows nothing new, in words for the user; null while it is fine.</summary>
    string? Problem { get; }

    /// <summary>Starts whatever work is due (a download, a sync) and returns at once; news comes through <see cref="Changed"/>.</summary>
    void Refresh();

    /// <summary>
    /// The events that overlap [<paramref name="from"/>, <paramref name="to"/>), wall-clock times in
    /// <paramref name="zone"/>, as wall-clock times there: what the source knows now, never waiting.
    /// </summary>
    IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone);

    /// <summary>
    /// §455: what a widget picks it by in "Show from" (Karpo.Core's WidgetSources.Key): an account's calendar by its
    /// account's address and its id; null for one no widget picks — a test's, the smoke's —, which every widget shows.
    /// </summary>
    string? Part => null;

    /// <summary>
    /// §455: its events as <see cref="EventsIn(DateTime, DateTime, TimeZoneInfo)"/>, where a widget that picked
    /// <paramref name="shown"/> shows it; none where not. The public holidays pick by country.
    /// </summary>
    IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone, SourceFilter shown) =>
        shown.Shows(Part) ? EventsIn(from, to, zone) : [];

    /// <summary>The source has news. Raised on any thread.</summary>
    event Action? Changed;

    /// <summary>
    /// Its web calendar on <paramref name="day"/>, an https address (Karpo.Accounts' CalendarLinks): where one of its
    /// events with no address of its own opens (§434); null for a calendar with no site — the public holidays, a test's.
    /// </summary>
    string? Web(DateOnly day) => null;
}

/// <summary>
/// A calendar whose events are simply given: what <c>--smoke</c> draws and what a test owns. It
/// is the seam that keeps the views testable — a picture of the calendar is made from one of
/// these, so it reaches no account.
/// </summary>
internal sealed class ListCalendar(string id, string name, Color colour, IReadOnlyList<CalendarEvent> events)
    : ICalendarSource
{
    public event Action? Changed;

    public string Id { get; } = id;

    public string Name { get; } = name;

    public Color Colour { get; } = colour;

    public bool Loading => false;

    public string? Problem => null;

    /// <summary>How often it was asked to start work, which it never has any of.</summary>
    public int Refreshed { get; private set; }

    /// <summary>Whether the store let it go.</summary>
    public bool Disposed { get; private set; }

    public void Refresh() => Refreshed++;

    public IReadOnlyList<CalendarEvent> EventsIn(DateTime from, DateTime to, TimeZoneInfo zone)
    {
        var found = new List<CalendarEvent>();
        foreach (var item in events)
        {
            if (CalendarStore.Overlaps(item, from, to))
            {
                found.Add(item with { Source = this, Colour = Colour });
            }
        }
        return found;
    }

    /// <summary>Says it has news, as a source that had fetched something would.</summary>
    public void Raise() => Changed?.Invoke();

    public void Dispose() => Disposed = true;
}

/// <summary>
/// The colours of calendars: the one the person picked on the Accounts page, else the account's own
/// (Outlook's, CalDAV's calendar-color), else one of the palette's that no calendar wears yet, in
/// the order the accounts list their calendars — worked out by Karpo.Accounts' SourceColours, as
/// the settings app works them out, so both say the same colour.
/// </summary>
internal static class CalendarColours
{
    /// <summary>The palette a calendar without a colour of its own takes one from: Outlook's, as Fluent's shared colours have them.</summary>
    public static readonly Color[] Palette = [.. SourceColours.Palette.Select(Of)];

    /// <summary>The colour of <c>#RRGGBB</c>, opaque.</summary>
    public static Color Of(string hex)
    {
        var (r, g, b) = SourceColours.Rgb(hex);
        return Color.FromArgb(255, r, g, b);
    }

    /// <summary><c>#RRGGBB</c> of <paramref name="colour"/>; null for none, or transparent, which is no colour of its own.</summary>
    public static string? Hex(Color? colour) =>
        colour is { A: > 0 } some ? SourceColours.Hex(some.R, some.G, some.B) : null;

    /// <summary>"#RGB", "#RRGGBB", "#RRGGBBAA" (Apple's), or a CSS colour name (RFC 7986); null for anything else.</summary>
    public static Color? Parse(string? value)
    {
        var text = value?.Trim().Trim('"') ?? "";
        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            if (hex.Length == 3)
            {
                hex = new string([hex[0], hex[0], hex[1], hex[1], hex[2], hex[2]]);
            }
            if (hex.Length is 6 or 8 && uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number))
            {
                var rgb = hex.Length == 8 ? number >> 8 : number;
                // The alpha is left out: a calendar colour is a colour, and a faint one would vanish.
                return Color.FromArgb(255, (byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            }
            return null;
        }
        return Named.TryGetValue(text, out var named) ? Color.FromArgb(255, (byte)(named >> 16), (byte)(named >> 8), (byte)named) : null;
    }

    /// <summary>The CSS colour names calendars are likely to use.</summary>
    private static readonly Dictionary<string, uint> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = 0x000000,
        ["white"] = 0xFFFFFF,
        ["gray"] = 0x808080,
        ["grey"] = 0x808080,
        ["silver"] = 0xC0C0C0,
        ["red"] = 0xFF0000,
        ["crimson"] = 0xDC143C,
        ["maroon"] = 0x800000,
        ["tomato"] = 0xFF6347,
        ["coral"] = 0xFF7F50,
        ["salmon"] = 0xFA8072,
        ["orange"] = 0xFFA500,
        ["darkorange"] = 0xFF8C00,
        ["gold"] = 0xFFD700,
        ["yellow"] = 0xFFFF00,
        ["olive"] = 0x808000,
        ["lime"] = 0x00FF00,
        ["green"] = 0x008000,
        ["forestgreen"] = 0x228B22,
        ["seagreen"] = 0x2E8B57,
        ["teal"] = 0x008080,
        ["turquoise"] = 0x40E0D0,
        ["aqua"] = 0x00FFFF,
        ["cyan"] = 0x00FFFF,
        ["skyblue"] = 0x87CEEB,
        ["dodgerblue"] = 0x1E90FF,
        ["royalblue"] = 0x4169E1,
        ["blue"] = 0x0000FF,
        ["navy"] = 0x000080,
        ["indigo"] = 0x4B0082,
        ["purple"] = 0x800080,
        ["violet"] = 0xEE82EE,
        ["orchid"] = 0xDA70D6,
        ["fuchsia"] = 0xFF00FF,
        ["magenta"] = 0xFF00FF,
        ["pink"] = 0xFFC0CB,
        ["hotpink"] = 0xFF69B4,
        ["brown"] = 0xA52A2A,
        ["chocolate"] = 0xD2691E,
        ["tan"] = 0xD2B48C,
    };
}
