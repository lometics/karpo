// §635: the samples of what the person's accounts hold and of a feed: a full week of meetings round now (the next one always still
// to come, with its Join), an inbox of five with three unread, five contacts, and a feed of headlines. Made up, no account
// asked: the stores are filled as a read would leave them (Answered, Add), and nothing is due after. What a --stage recording
// shows too (Stage.Samples), and the Appearance's preview its own two events at its still clock (PreviewCalendar).

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.Pim;
using Karpo.Dock.Rss;

namespace Karpo.Dock;

internal static partial class Samples
{
    // --- The calendar --------------------------------------------------------------------------------

    /// <summary>A store of the samples' two calendars, opened: a work week of meetings round now, and a personal one.</summary>
    public static CalendarStore Calendar()
    {
        var store = new CalendarStore();
        foreach (var calendar in Calendars())
        {
            store.Add(calendar);
        }
        store.Open();
        return store;
    }

    /// <summary>
    /// The samples' calendars: a full week of meetings on the other days, Monday to Sunday, whichever day it is; today's
    /// round the hour, two behind it, one later, and the next one some 25 minutes ahead, so a next meeting is always still to
    /// come. None of the week's stands on today, where it would lie over one of those.
    /// </summary>
    public static IReadOnlyList<ListCalendar> Calendars()
    {
        var now = Clock.Now();
        var today = now.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        CalendarEvent At(int day, double hour, double hours, string title, string? where, string? join = null) =>
            new(monday.AddDays(day).AddHours(hour), monday.AddDays(day).AddHours(hour + hours), false, title, where, Join: join);
        IEnumerable<CalendarEvent> OtherDays(params CalendarEvent[] week) => week.Where(each => each.Start.Date != today);
        var hour = today.AddHours(now.Hour);
        CalendarEvent Near(double hours, double length, string title, string? where, string? join = null) =>
            new(hour.AddHours(hours), hour.AddHours(hours + length), false, title, where, Join: join);
        var five = TimeSpan.FromMinutes(5).Ticks;
        var soon = new DateTime((now.Ticks + five - 1) / five * five).AddMinutes(25);
        // Made-up meetings' links, of the shapes Teams and Zoom give, for the next meeting's Join (§205).
        const string Teams = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_stage%40thread.v2/0";
        const string Zoom = "https://us02web.zoom.us/j/81234567890";
        return
        [
            new ListCalendar("sample:work", "Work", CalendarColours.Of("#0F6CBD"),
            [
                Near(-4, 0.5, "Stand-up", null, Teams),
                Near(-2.5, 1, "Design review", "Room 3", Teams),
                new(soon, soon.AddMinutes(30), false, "Team check-in", null, Join: Teams),
                Near(2.5, 1.5, "Q4 planning", "Board room"),
                .. OtherDays(
                    At(0, 9, 0.5, "Stand-up", null, Teams),
                    At(0, 11, 1, "Product sync", "Room 1"),
                    At(0, 14, 1, "Sprint retro", "Room 2"),
                    At(1, 10, 1, "1:1 with Anna", null, Zoom),
                    At(1, 11.5, 1, "Roadmap", null, Teams),
                    At(1, 14, 1, "Budget review", null, Zoom),
                    At(1, 15.5, 0.5, "Coffee with Lena", null),
                    At(2, 9, 0.5, "Stand-up", null, Teams),
                    At(2, 10.5, 1, "Interview", "Room 3"),
                    At(2, 13, 2, "Customer workshop", "Zurich office"),
                    At(3, 9, 0.5, "Stand-up", null, Teams),
                    At(3, 15, 1, "Design sync", null, Teams),
                    At(4, 9, 0.5, "Stand-up", null, Teams),
                    At(4, 10, 1.5, "Release planning", "Board room"),
                    At(4, 13.5, 1.5, "Sprint demo", "Room 2")),
            ]),
            new ListCalendar("sample:home", "Personal", CalendarColours.Of("#C239B3"),
            [
                Near(5, 1.5, "Climbing", "Minimum Zürich"),
                .. OtherDays(
                    At(0, 18, 1, "Gym", null),
                    At(1, 19, 2, "Dinner with Sam", "Zeughauskeller"),
                    At(2, 17.5, 1, "Yoga", "Studio 8"),
                    At(3, 12, 1, "Lunch with Mia", "Café Rosso"),
                    At(4, 16.5, 1, "Dentist", "Dr. Keller")),
                new(monday.AddDays(5), monday.AddDays(7), true, "Weekend in Ticino", null),
            ]),
        ];
    }

    /// <summary>§536: the Appearance's preview's calendar, at its clock still at 9:41: a meeting at 10:00 and lunch at 12:30 today, a meeting again tomorrow.</summary>
    public static CalendarStore PreviewCalendar()
    {
        var today = Clock.Real().Date;
        var store = new CalendarStore();
        store.Add(new ListCalendar("preview:events", S.DockEditPreview, default,
        [
            new CalendarEvent(today.AddHours(10), today.AddHours(10.5), false, S.DockEditPreviewMeeting, null),
            new CalendarEvent(today.AddHours(12.5), today.AddHours(13.5), false, S.DockEditPreviewLunch, null),
            new CalendarEvent(today.AddDays(1).AddHours(10), today.AddDays(1).AddHours(10.5), false, S.DockEditPreviewMeeting, null),
        ]));
        return store;
    }

    // --- The inbox and the contacts ------------------------------------------------------------------

    /// <summary>The samples' inbox: five messages, the newest minutes old, three unread.</summary>
    public static IReadOnlyList<MailItem> Inbox()
    {
        var now = Clock.RealOffset();
        return
        [
            new MailItem("s1", "Anna Keller", "anna@example.com", "Slides for tomorrow", "Here is the deck for the design review, with the new numbers on slide 4.", now.AddMinutes(-7), Unread: true),
            new MailItem("s2", "GitHub", "noreply@github.com", "[karpo] Pull request approved", "Your pull request was approved and can be merged.", now.AddMinutes(-41), Unread: true),
            new MailItem("s3", "Sam Weber", "sam@example.com", "Dinner tomorrow?", "Zeughauskeller at 7 works for me. Shall I book?", now.AddHours(-2), Unread: true),
            new MailItem("s4", "Swiss", "news@example.com", "Your trip to Lugano", "Check in now for your flight on Friday.", now.AddHours(-5), Unread: false),
            new MailItem("s5", "Lena Brunner", "lena@example.com", "Re: Q4 numbers", "Thanks, looks great.", now.AddDays(-1), Unread: false),
        ];
    }

    [ThreadStatic]
    private static MailStore? mail;

    /// <summary>
    /// A store of the samples' inbox, as a read leaves it: no account, nothing due. One per thread (a store follows the online
    /// interval for good), its messages made again each time so they are as old as they say.
    /// </summary>
    public static MailStore Mail()
    {
        mail ??= new MailStore(new AccountSet([]));
        mail.Answered(Inbox(), unread: 3);
        return mail;
    }

    /// <summary>The samples' contacts: five people, an address, a number, an organization.</summary>
    public static IReadOnlyList<Person> People() =>
    [
        new("a", "Adrian Hager", "Contoso", ["adrian.hager@contoso.com"], ["+41 79 123 45 67"]),
        new("b", "Beatrice Keller", null, ["bea@example.com"], []),
        new("c", "Carlos Mendes", null, [], ["+41 44 555 01 02"]),
        new("d", "Dana Schläpfer", "Fabrikam", ["dana@fabrikam.com"], []),
        new("e", "Elif Yilmaz", null, ["elif.yilmaz@fabrikam.com"], []),
    ];

    [ThreadStatic]
    private static ContactStore? contacts;

    /// <summary>A store of the samples' contacts, as a read leaves it: no account, nothing due; one per thread, as the inbox's.</summary>
    public static ContactStore Contacts()
    {
        contacts ??= new ContactStore(new AccountSet([]));
        contacts.Answered(People());
        return contacts;
    }

    // --- A feed --------------------------------------------------------------------------------------

    /// <summary>What a news widget follows in the samples where it follows nothing yet.</summary>
    public const string FeedAddress = "https://news.example.com/rss";

    /// <summary>A store whose every feed is the samples' headlines, read the moment it is asked for: nothing fetched, no picture, no site's icon.</summary>
    public static RssStore Feeds() => new(new RssSource { Ask = (_, _) => Task.FromResult(new RssAnswer(Feed(), null, null)) }, RssDisk.None, sites: static () => null, settled: Feed);

    /// <summary>The samples' feed as the store keeps a read, its two newest unseen.</summary>
    private static RssCache Feed(string address)
    {
        var feed = Feed();
        return new RssCache
        {
            Address = address,
            FetchedUtc = Clock.RealUtc(),
            Saved = RssSaved.Of(feed),
            Seen = [.. feed.Items.Skip(2).Select(item => item.Key)],
        };
    }

    /// <summary>The samples' headlines: six, the newest a quarter of an hour old.</summary>
    private static Feed Feed()
    {
        var now = Clock.RealOffset();
        FeedItem Item(string key, string title, double hoursAgo) =>
            new() { Key = key, Title = title, Link = $"https://news.example.com/{key}", Published = now.AddHours(-hoursAgo) };
        return new Feed
        {
            Title = "World News",
            Site = "https://news.example.com",
            Items =
            [
                Item("h1", "Alpine rail tunnel opens a year ahead of schedule", 0.25),
                Item("h2", "Central bank holds rates steady as inflation cools", 1.2),
                Item("h3", "Researchers map the oldest glacier ice yet found", 2.5),
                Item("h4", "City adds 40 km of protected bike lanes", 4),
                Item("h5", "Solar output hits a record over the long weekend", 6.5),
                Item("h6", "Lake Zurich swim draws thousands despite the chill", 9),
            ],
        };
    }
}
