// §642, §652: Windows search's samples: what its line asks where it is made showing the samples (§635: the add page's ghost, a
// preview) — every source made up, so the ghost shows what the line does: §655 its home before anything is typed (the
// sample searches, Start's sample apps used and pinned, a week's files and folders, the samples' week's events, mail and a
// note), and with "team" typed (a picture) a best match, then the groups of several sources, each row with its icon. Nothing of this PC's is asked: the index is a made-up folder's files, which wear
// their kinds' icons (WinSearchLine.IconOf: no file is read); the apps are Start's samples', by their real Apps folder ids,
// so their icons are the PC's where it has them (as Start's samples, §641); the mail, the notes and the people made up; the
// calendar the samples' week (Samples.Calendars); Karpo's own the config's. Everything answers at once, on the caller's thread.

using Karpo.Accounts;
using Karpo.Core;
using Karpo.Dock.StartWidgets;

namespace Karpo.Dock.WinSearch;

/// <summary>§642, §652: what a ghost on the add page or a preview asks — "team" typed, made-up sources, no question of this PC.</summary>
internal static class WinSearchSamples
{
    public const string Typed = "team";

    /// <summary>Microsoft Teams, by its Apps folder id: the app opened for "team" before, the best match.</summary>
    private const string Teams = StartApp.AppsFolder + "MSTeams_8wekyb3d8bbwe!MSTeams";

    /// <summary>The made-up Public folder the index's files are in.</summary>
    private const string Public = @"C:\Users\Public\Documents";

    /// <summary>The made-up index: the files and the folders it would answer "team" with.</summary>
    private sealed class Index : IWinSearchIndex
    {
        public WinSearchIndexAnswer Query(string sql, CancellationToken cancel)
        {
            var when = Clock.RealUtc();
            return new(
            [
                new("Offsite team agenda.pptx", Public + @"\Offsite team agenda.pptx", "file:" + Public + "/Offsite team agenda.pptx", ".pptx", when.AddHours(-3), 2_400_000, 900), // not-ui: a sample's file
                new("Q4 team goals.docx", Public + @"\Q4 team goals.docx", "file:" + Public + "/Q4 team goals.docx", ".docx", when.AddDays(-2), 48_128, 800), // not-ui: a sample's file
                new("Team budget.xlsx", Public + @"\Team budget.xlsx", "file:" + Public + "/Team budget.xlsx", ".xlsx", when.AddDays(-9), 36_864, 700), // not-ui: a sample's file
                new("Marketing team", Public + @"\Marketing team", "file:" + Public + "/Marketing team", "Directory", when.AddDays(-20), null, 600), // not-ui: a sample's folder
                new("Team photo.jpg", @"C:\Users\Public\Pictures\Team photo.jpg", "file:C:/Users/Public/Pictures/Team photo.jpg", ".jpg", when.AddDays(-40), 3_400_000, 500), // not-ui: a sample's file
            ]);
        }
    }

    private sealed class Apps : IWinSearchApps
    {
        public IReadOnlyList<WinSearchApp> List() =>
            [.. StartSamples.SampleApps.Select(app => new WinSearchApp(app.Name, app.Target)), new("Microsoft Teams", Teams)]; // not-ui: an app's name
    }

    /// <summary>The made-up mail an account synced.</summary>
    private static IReadOnlyList<MailItem> Mail()
    {
        var now = Clock.RealOffset();
        return
        [
            new MailItem("w1", "Anna Keller", "anna@example.com", "Team offsite: agenda and rooms", "The agenda for Thursday, and who sits where.", now.AddMinutes(-25), Unread: true), // not-ui: a sample's mail
            new MailItem("w2", "Lena Brunner", "lena@example.com", "Re: Team lunch on Friday", "Count me in.", now.AddDays(-1), Unread: false), // not-ui: a sample's mail
        ];
    }

    /// <summary>The made-up notes.</summary>
    private static IReadOnlyList<Note> Notes() =>
    [
        new Note { Id = "sample-team", Title = "Team ideas", Body = "- A shared playlist for Fridays\n- Rotate the stand-up host", Modified = Clock.RealOffset().AddDays(-3) }, // not-ui: a sample's note
    ];

    /// <summary>§655: the apps the samples opened most from Start, and how often: the home's Top apps.</summary>
    private static readonly (string Name, int Times)[] Used = [("Microsoft Edge", 9), ("Word", 6), ("Outlook", 5), ("Excel", 4), ("Spotify", 3), ("Terminal", 2)]; // not-ui: apps' names

    /// <summary>§655: Start's samples with the samples' Top apps opened as often as <see cref="Used"/> says.</summary>
    private static StartSamples Start()
    {
        var start = new StartSamples(newApp: null);
        foreach (var (name, times) in Used)
        {
            var app = StartSamples.SampleApps.First(each => each.Name == name);
            for (var time = 0; time < times; time++)
            {
                start.Uses.Opened(app.Target, StartSamples.Now.AddHours(-time));
            }
        }
        return start;
    }

    /// <summary>The sources a ghost asks, answering on the caller's thread, the index at once; §655 its home the samples'.</summary>
    public static WinSearchSources Sources(Config config)
    {
        static void Now(Action work) => work();
        var calendar = Samples.Calendar();
        Func<DateTime, DateTime, IReadOnlyList<CalendarEvent>> events = (from, to) => calendar.EventsIn(from, to);
        return WinSearchSources.Windows(
            new Index(),
            new Apps(),
            Now,
            static (_, _) => true,
            static () => Clock.RealUtc(),
            NumberMarks.Invariant,
            more:
            [
                new WinSearchMailSource(Mail, static () => Clock.RealUtc()),
                new WinSearchCalendarSource(events, static (_, _) => null, static () => Clock.Real()),
                new WinSearchContactsSource(Samples.People),
                new WinSearchNotesSource(Notes),
                new WinSearchKarpoSource(() => WinSearchKarpoSource.Of(config)),
            ],
            home: WinSearchHomes.Of(Start(), () => config, Mail, events, static (_, _) => null, Notes, static () => Clock.Real()));
    }

    /// <summary>
    /// A ghost's history, its own and nowhere saved: Teams opened for "team" before, so it is the best match, as it would be;
    /// §655 and a few searches made lately, the home's.
    /// </summary>
    public static WinSearchHistory History()
    {
        var history = new WinSearchHistory();
        history.Open(new WinSearchHit(WinSearchCategory.Apps, "Microsoft Teams", Teams), Typed, Clock.RealUtc().AddDays(-1)); // not-ui: an app's name
        history.Searched("holiday photos"); // not-ui: a sample's search
        history.Searched("budget"); // not-ui: a sample's search
        return history;
    }
}
