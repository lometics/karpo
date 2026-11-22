// Where a click on an event opens (docs\widgets.md §434; the person, 2026-10-30: "an event should light up anyways and
// open a calendar"). Every event a line lists — the agenda's, the day's events', the next meetings' — is an item: lit
// edge to edge under the pointer (LineItem, §428), and a click on it opens the event in a calendar once the folder has
// gone, as every other line's opening does (IslandView.App, §380). The first of these there is:
//
//  1. the event's own page on its account's site: Outlook on the web's for a Microsoft event (Graph's webLink);
//  2. its calendar's web calendar on the day it is listed under: Google Calendar's day view for a Google CalDAV
//     account, iCloud's calendar for an iCloud one (Karpo.Accounts' CalendarLinks);
//  3. the person's web calendar on that day — the first of their calendars that has one — for an event of a calendar
//     with no site: a public holiday, a CalDAV server Karpo knows no site of;
//  4. the calendar app Windows has (CalendarApp: outlookcal:, ms-calendar:), at today: neither takes a day it can be
//     sent to;
//  5. nothing: the row is no item, and lights nothing (§379).
//
// A meeting's Join stays a button on the row (§206, §428: a control on an item keeps its own hit and light); the rest
// of the row is the event's.

namespace Karpo.Dock;

internal static class EventLinks
{
    /// <summary>
    /// What a click on <paramref name="item"/>, listed under <paramref name="day"/>, opens: its own page, its
    /// calendar's web calendar on the day, the person's (<paramref name="store"/>'s), else the calendar app; null where
    /// there is nothing to open.
    /// </summary>
    public static string? Target(in CalendarEvent item, DateOnly day, CalendarStore store) =>
        item.Link ?? item.Source?.Web(day) ?? store.Web(day) ?? CalendarApp.Link();

    /// <summary>Opens <paramref name="target"/> (<see cref="Target"/>) through the shell, off the dock's thread (a test's <see cref="MeetingJoin.Opening"/>).</summary>
    public static void Open(string target) => MeetingJoin.Start(target);
}
