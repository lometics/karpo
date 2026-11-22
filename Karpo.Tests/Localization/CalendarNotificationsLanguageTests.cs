// The calendar, clock, notification, mail and contacts flyouts in another language: their words
// come from the strings table when they are asked for, so a language change reaches them, and a
// count picks its form by the language's own rule.

using System.Globalization;
using Karpo.Dock;
using Karpo.Dock.Pim;

namespace Karpo.Tests.Localization;

[Collection(InLanguage.Collection)]
public sealed class CalendarNotificationsLanguageTests
{
    private static readonly DateOnly Sep17 = new(2026, 9, 17);

    private static string Time(DateTime at) => at.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Day(DateOnly day) => day.ToString("MM-dd", CultureInfo.InvariantCulture);

    private static CalendarEvent Timed(int day, int hour, int hours) =>
        new(new DateTime(2026, 9, day, hour, 0, 0), new DateTime(2026, 9, day, hour, 0, 0).AddHours(hours), false, "x", null);

    [Fact]
    public void AnEventsSpanAndTheDaysNamesAreGerman()
    {
        using var _ = InLanguage.Use("de");

        Assert.Equal("Ganztägig", CalendarText.Span(new CalendarEvent(new DateTime(2026, 9, 17), new DateTime(2026, 9, 18), true, "x", null), Sep17, Time));
        Assert.Equal("Ab 22:00", CalendarText.Span(Timed(17, 22, 4), Sep17, Time));
        Assert.Equal("Bis 02:00", CalendarText.Span(Timed(16, 22, 4), Sep17, Time));
        Assert.Equal("09:00–10:00", CalendarText.Span(Timed(17, 9, 1), Sep17, Time));
        Assert.Equal("Heute", CalendarText.DayName(Sep17, Sep17));
        Assert.Equal("Morgen", CalendarText.DayName(Sep17.AddDays(1), Sep17));
        Assert.Equal("Gestern", CalendarText.DayName(Sep17.AddDays(-1), Sep17));
    }

    [Fact]
    public void ANotificationsTimeIsFrenchWithItsSpace()
    {
        using var _ = InLanguage.Use("fr");
        var at = new DateTime(2026, 9, 17, 18, 0, 0);

        Assert.Equal("Maintenant", NotificationText.Relative(at.AddSeconds(-30), at, Day, Day));
        Assert.Equal("5 min", NotificationText.Relative(at.AddMinutes(-5), at, Day, Day));
        Assert.Equal("2 h", NotificationText.Relative(at.AddHours(-2), at, Day, Day));
        Assert.Equal("Hier", NotificationText.Relative(at.AddDays(-1), at, Day, Day));
        Assert.Equal("09-10", NotificationText.Relative(new DateTime(2026, 9, 10, 12, 0, 0), at, Day, Day));
    }

    [Fact]
    public void TheNotificationListsWordsFollowTheLanguage()
    {
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("全部清除", S.WidgetNotificationsClearAll);
        }
        Assert.Equal("Clear all", S.WidgetNotificationsClearAll);
    }

    [Fact]
    public void ACountdownSaysWhatItDoesInGerman()
    {
        using var _ = InLanguage.Use("de");
        var start = new DateTime(2026, 9, 22, 14, 0, 0);
        var timer = Countdown.Start(CountdownKind.Timer, TimeSpan.FromMinutes(5), start);
        var focus = Countdown.Start(CountdownKind.Focus, TimeSpan.FromMinutes(25), start);

        Assert.Equal("Timer", timer.Says(start));
        Assert.Equal("Angehalten", timer.Pause(start.AddSeconds(30)).Says(start));
        Assert.Equal("Konzentrationszeit", focus.Says(start));
        Assert.Equal("1 Std. 30 Min.", Countdown.Words(TimeSpan.FromMinutes(90)));
        Assert.Equal(["Konzentrationszeit", "Konzentration"], CountdownLine.NamesOf(CountdownKind.Focus, ""));
    }

    [Fact]
    public void CountsPickTheirFormByTheLanguage()
    {
        using (InLanguage.Use("fr"))
        {
            // French says nought in the singular.
            Assert.Equal("0 message non lu", S.DockMailUnreadCount(0));
            Assert.Equal("1 message non lu", S.DockMailUnreadCount(1));
        }
        using (InLanguage.Use("de"))
        {
            Assert.Equal("1 ungelesene Nachricht", S.DockMailUnreadCount(1));
            Assert.Equal("4 ungelesene Nachrichten", S.DockMailUnreadCount(4));
        }
        using (InLanguage.Use("zh-Hans"))
        {
            Assert.Equal("1 封未读邮件", S.DockMailUnreadCount(1));
            Assert.Equal("2 封未读邮件", S.DockMailUnreadCount(2));
        }
        Assert.Equal("1 unread message", S.DockMailUnreadCount(1));
        Assert.Equal("2 unread messages", S.DockMailUnreadCount(2));
    }

    [Fact]
    public void WhereToAddAnAccountIsSaidInTheLanguageNow()
    {
        Assert.Equal("No mail account yet. Add one in Karpo's settings, under Accounts.", MailStore.NoAccount);
        using var _ = InLanguage.Use("de");

        Assert.StartsWith("Noch kein Kalenderkonto.", AccountCalendars.NoAccount);
        Assert.StartsWith("Noch kein E-Mail-Konto.", MailStore.NoAccount);
        Assert.StartsWith("Noch kein Kontaktekonto.", ContactStore.NoAccount);
    }
}
