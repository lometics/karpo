// The colours of the Accounts page on the bar's lines (docs\accounts.md, "Colours"): the mail of two
// inboxes each message with its inbox's bar on the gutter, the day's events in their calendars'
// colours (one picked), and the people of two address books each glyph in its book's colour. The sheets
// go to KARPO_PNG_DIR (else %TEMP%\karpo-tests\pictures):
// part-colour-<mail|events|contacts>-lines-<dark|light>-<100|150>.png. Runs only with KARPO_UI_TESTS=1.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;

namespace Karpo.Tests.Pim;

public sealed class PartColourPictures
{
    private static readonly DateTime Frozen = new(2026, 10, 12, 9, 0, 0);

    /// <summary>Two inboxes' newest, newest first, each in its inbox's colour as the store paints them.</summary>
    private static List<MailItem> Inboxes() =>
    [
        new("1", "Sandra Gerber", "sandra@example.ch", "Lunch on Friday?", null, new DateTimeOffset(Frozen.AddMinutes(-4)), true) { Colour = SourceColours.Palette[5] },
        new("2", "GitHub", "noreply@github.com", "[karpo-app] CI passed on master", null, new DateTimeOffset(Frozen.AddMinutes(-52)), true) { Colour = SourceColours.Palette[4] },
        new("3", "Adrian Hager", "adrian@example.com", "Re: Quarterly review", null, new DateTimeOffset(Frozen.AddHours(-3)), false) { Colour = SourceColours.Palette[5] },
        new("4", "Swiss Post", "noreply@post.ch", "Your parcel is on its way", null, new DateTimeOffset(Frozen.AddHours(-5)), false) { Colour = SourceColours.Palette[4] },
    ];

    /// <summary>A calendar store of one account with three calendars: Outlook's own pink, one picked green, one the palette's.</summary>
    private static async Task<CalendarStore> Calendars()
    {
        var utc = Frozen.ToUniversalTime();
        var reader = new FakeReader(address: "home@example.com");
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("home", "Home", "#E3008C"),
            [new Occurrence("Dentist", null, false, false, utc.AddHours(1), utc.AddHours(2))], []));
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("work", "Work", null),
            [new Occurrence("Design review", "Room 4", false, false, utc.AddHours(3), utc.AddHours(4))], []));
        reader.Calendars.Add(new CalendarRead(new RemoteCalendar("club", "Club", null),
            [new Occurrence("Training", null, false, false, utc.AddHours(9), utc.AddHours(10))], []));
        var picked = reader.Coloured(SyncKeys.Calendar("work"), "#498205");
        var store = new CalendarStore(new AccountCalendars(new AccountSet([picked]), () => utc));
        store.Open();
        await store.Idle();
        return store;
    }

    [UiFact]
    public void TheLinesWearTheColoursOfTheAccountsPage()
    {
        var calendar = Calendars().GetAwaiter().GetResult();
        var contacts = new ContactStore(new AccountSet([]));
        contacts.Answered(
        [
            new Person("1", "Ann Keller", null, ["ann@example.com"], []) { Colour = SourceColours.Palette[5] },
            new Person("2", "Bob Meier", "Karpo", ["bob@work.example.com"], []) { Colour = SourceColours.Palette[4] },
            new Person("3", "Cyril Huber", null, [], ["+41 79 123 45 67"]) { Colour = SourceColours.Palette[5] },
        ]);
        Offscreen.OnStaThread(gfx =>
        {
            var real = Clock.Now;
            Clock.Now = () => Frozen;
            CalendarIsland.Made = calendar;
            try
            {
                using var mail = new MailStore(new AccountSet([]));
                mail.Answered(Inboxes(), unread: 2);
                foreach (var dark in new[] { true, false })
                {
                    var look = ListFormPictures.LookFor(dark);
                    foreach (var scale in new[] { 1f, 1.5f })
                    {
                        var name = $"{(dark ? "dark" : "light")}-{(int)(scale * 100)}";
                        var mailLine = ListKind.Mail.New();
                        mailLine.Id = "mail";
                        using (var island = new MailIsland(Config.IslandKey("layout:default", "mail"), mailLine, gfx, new Config(), mail, () => Frozen))
                        {
                            ListLinesTests.Sheet(gfx, island, ListKind.Mail, scale, look, Path.Combine(Offscreen.Pictures, $"part-colour-mail-lines-{name}.png"));
                        }

                        var events = ListKind.Events.New();
                        events.Id = "events";
                        using (var island = (CellIsland)Karpo.Dock.Islands.CreateLine(Config.IslandKey("layout:default", "events"), events, ListKind.Events, gfx, new Config())!)
                        {
                            ListLinesTests.Sheet(gfx, island, ListKind.Events, scale, look, Path.Combine(Offscreen.Pictures, $"part-colour-events-lines-{name}.png"));
                        }

                        var people = ListKind.Contacts.New();
                        people.Id = "contacts";
                        using (var island = new ContactsLine(Config.IslandKey("layout:default", "contacts"), people, gfx, new Config(), contacts))
                        {
                            island.StandIn(ListWidgets.Of(ListKind.Contacts));
                            island.Visible(true);
                            ListLinesTests.Sheet(gfx, island, ListKind.Contacts, scale, look, Path.Combine(Offscreen.Pictures, $"part-colour-contacts-lines-{name}.png"));
                        }
                    }
                }
            }
            finally
            {
                Clock.Now = real;
                CalendarIsland.Made = null;
            }
        });
    }
}
