// One interval for everything online (Karpo.Core\OnlineUpdates.cs): every service that fetches on
// a timer takes it, held to its own provider's floor, and follows a change at once. The person,
// 2026-10-05: "there should be only one counter for online updates, e.g. the stock widgets should
// follow it, weather should update in that duration, and other online stuff should also just update
// in that intervall."
//
// Every test makes an OnlineUpdates of its own and hands it to the store it makes, so none of them
// touches the dock's shared one, and none reaches the network, an account or the disk.

using System.Text.Json;
using Karpo.Accounts;
using Karpo.Dock;
using Karpo.Dock.Pim;
using Karpo.Dock.Markets;
using Karpo.Dock.Weather;
using Karpo.Tests.Pim;

namespace Karpo.Tests.Forecasts;

public sealed class OnlineUpdatesTests
{
    private static OnlineUpdates Every(int minutes)
    {
        var updates = new OnlineUpdates();
        updates.Set(minutes);
        return updates;
    }

    // --- The interval itself ----------------------------------------------------------------------

    [Fact]
    public void ItStartsAtAQuarterOfAnHourAndOffersWhatTheWidgetsOfferedBetweenThem()
    {
        var updates = new OnlineUpdates();

        Assert.Equal(15, updates.Minutes);
        Assert.Equal(TimeSpan.FromMinutes(15), updates.Interval);
        Assert.Equal(OnlineUpdates.DefaultMinutes, new Config().OnlineUpdateMinutes);
        // The weather offered 15 to 180, the stocks 5 to 180: the one list is both.
        Assert.Equal([5, 15, 30, 60, 120, 180], OnlineUpdates.Choices);
        Assert.Contains(OnlineUpdates.DefaultMinutes, OnlineUpdates.Choices);
    }

    [Fact]
    public void ItIsHeldToFiveMinutesAtLeastAndThreeHoursAtMost()
    {
        Assert.Equal(5, Every(1).Minutes);
        Assert.Equal(5, Every(-30).Minutes);
        Assert.Equal(180, Every(10_000).Minutes);
        Assert.Equal(45, Every(45).Minutes);
    }

    [Fact]
    public void AChangeIsSaidOnceAndTheSameValueIsNoChange()
    {
        var updates = new OnlineUpdates();
        var said = 0;
        updates.Changed += () => said++;

        updates.Set(15);
        Assert.Equal(0, said);
        updates.Set(60);
        Assert.Equal(1, said);
        Assert.Equal(60, updates.Minutes);
        updates.Set(60);
        // Held to the least it may be, it is the same as what it was held to before.
        updates.Set(5);
        updates.Set(1);
        Assert.Equal(2, said);
    }

    [Fact]
    public void AFloorIsNeverUndercutAndNeverRaisesALongerInterval()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), Every(5).Every(15));
        Assert.Equal(TimeSpan.FromMinutes(60), Every(60).Every(15));
        Assert.Equal(TimeSpan.FromMinutes(5), Every(5).Every());
    }

    // --- Each service takes it --------------------------------------------------------------------

    /// <summary>The weather and the stocks take it, each held to its own floor: 15 minutes and 5.</summary>
    [Fact]
    public void TheWeatherAndTheStocksTakeTheIntervalHeldToTheirOwnFloors()
    {
        var updates = Every(60);
        var weather = new WeatherStore((_, _) => throw new InvalidOperationException("nothing is fetched here"), updates: updates);
        var stocks = new MarketStore(new MarketAnswers((_, _) => throw new InvalidOperationException("nothing is fetched here")), MarketDisk.None, updates);

        Assert.Equal(TimeSpan.FromMinutes(60), weather.Every);
        Assert.Equal(TimeSpan.FromMinutes(60), stocks.Every);
        Assert.Equal(60, WeatherReading.Refresh(updates));
        Assert.Equal(60, MarketReading.Refresh(updates));

        // Five minutes: the stocks go with it, the weather stops at its floor (Open-Meteo's
        // quarter-hourly current conditions).
        updates.Set(5);
        Assert.Equal(TimeSpan.FromMinutes(WeatherStore.LeastMinutes), weather.Every);
        Assert.Equal(TimeSpan.FromMinutes(15), weather.Every);
        Assert.Equal(TimeSpan.FromMinutes(5), stocks.Every);
        Assert.Equal(15, WeatherReading.Refresh(updates));
        Assert.Equal(5, MarketReading.Refresh(updates));
    }

    /// <summary>
    /// A price fetched twenty minutes ago is not due on an hour, and is due the moment the interval
    /// is set to a quarter of an hour — no restart, no reconfiguring the widget.
    /// </summary>
    [Fact]
    public void AShorterIntervalMakesAPriceDueAtOnce()
    {
        var updates = Every(60);
        var asked = 0;
        var file = JsonSerializer.Serialize(
            new MarketFile
            {
                Prices =
                [
                    new MarketCache
                    {
                        FetchedUtc = DateTime.UtcNow.AddMinutes(-20),
                        Key = "stock:MSFT 1d",
                        Saved = MarketSaved.Of(MarketQuotes.Parse(MarketOffscreenTests.Answer("MSFT"))),
                    },
                ],
            },
            MarketJson.Default.MarketFile);
        var store = new MarketStore(
            new MarketAnswers((_, cancel) =>
            {
                Interlocked.Increment(ref asked);
                return new TaskCompletionSource<MarketQuote>().Task.WaitAsync(cancel);
            }),
            new MarketDisk { Read = () => file, Write = _ => { } },
            updates);
        var widget = new object();
        store.Use(widget, MarketAsk.Of(new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day }));

        store.Poll(widget);
        Assert.Equal(0, Volatile.Read(ref asked));

        updates.Set(15);
        store.Poll(widget);
        // The fetch is started on the pool; it is asked for there.
        SpinWait.SpinUntil(() => Volatile.Read(ref asked) == 1, TimeSpan.FromSeconds(10));
        Assert.Equal(1, Volatile.Read(ref asked));
        store.Release(widget);
    }

    /// <summary>The person's own Refresh is not held to the interval, however long it is.</summary>
    [Fact]
    public void RefreshByHandIsNotHeldToTheInterval()
    {
        var updates = Every(180);
        var asked = 0;
        var store = new MarketStore(
            new MarketAnswers((_, _) =>
            {
                Interlocked.Increment(ref asked);
                return Task.FromResult(MarketQuotes.Parse(MarketOffscreenTests.Answer("MSFT")));
            }),
            MarketDisk.None,
            updates);
        var widget = new object();
        store.Use(widget, MarketAsk.Of(new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day }));
        store.Poll(widget);
        SpinWait.SpinUntil(() => store.Read(widget) is { Shown: not null, Fetching: false }, TimeSpan.FromSeconds(10));

        store.Poll(widget);
        Assert.Equal(1, Volatile.Read(ref asked));
        Assert.True(store.Ask(widget));
        SpinWait.SpinUntil(() => Volatile.Read(ref asked) == 2, TimeSpan.FromSeconds(10));
        Assert.Equal(2, Volatile.Read(ref asked));
        store.Release(widget);
    }

    /// <summary>The mail waits the interval after a read, and a new interval moves the wait at once.</summary>
    [Fact]
    public async Task TheMailIsReadAgainAtTheIntervalAndFollowsAChangeAtOnce()
    {
        var updates = Every(15);
        var account = new FakeReader(AccountKind.Imap);
        using var store = new MailStore(new AccountSet([account]), updates);

        store.Refresh();
        await store.Idle();
        Assert.Equal(TimeSpan.FromMinutes(15), store.Armed);

        updates.Set(60);
        Assert.InRange(store.Armed!.Value, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(60));

        // The read was a moment ago: on five minutes it is due in a little under five.
        updates.Set(5);
        Assert.InRange(store.Armed!.Value, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));
        Assert.Equal(1, account.InboxReads);
    }

    /// <summary>A failure rests its minute, whatever the interval says; the interval is for reads that went well.</summary>
    [Fact]
    public async Task AMailFailureRestsItsMinuteWhateverTheInterval()
    {
        var updates = Every(60);
        var account = new FakeReader(AccountKind.Imap) { Failure = new HttpRequestException("no route") };
        using var store = new MailStore(new AccountSet([account]), updates);

        store.Refresh();
        await store.Idle();
        Assert.Equal(TimeSpan.FromMinutes(1), store.Armed);

        updates.Set(5);
        Assert.Equal(TimeSpan.FromMinutes(1), store.Armed);
    }

    /// <summary>The calendars wait the interval after a read too, and follow a change at once.</summary>
    [Fact]
    public async Task TheCalendarsAreReadAgainAtTheIntervalAndFollowAChangeAtOnce()
    {
        var updates = Every(30);
        var account = new FakeReader();
        account.Calendars.Add(new CalendarRead(new RemoteCalendar("a", "A", null), [], []));
        using var calendars = new AccountCalendars(new AccountSet([account]), updates: updates);

        calendars.Refresh();
        await calendars.Idle();
        Assert.Equal(TimeSpan.FromMinutes(30), calendars.Armed);

        updates.Set(120);
        Assert.InRange(calendars.Armed!.Value, TimeSpan.FromMinutes(119), TimeSpan.FromMinutes(120));
        Assert.Equal(1, account.CalendarReads);
    }

    /// <summary>Nobody has looked at the calendars: a new interval starts nothing.</summary>
    [Fact]
    public void ANewIntervalStartsNoCalendarNobodyLookedAt()
    {
        var updates = Every(30);
        var account = new FakeReader();
        using var calendars = new AccountCalendars(new AccountSet([account]), updates: updates);

        updates.Set(5);

        Assert.Null(calendars.Armed);
        Assert.Equal(0, account.CalendarReads);
    }

    /// <summary>The contacts are read when the panel opens, and not again while the read is younger than the interval.</summary>
    [Fact]
    public async Task TheContactsStayFreshForTheInterval()
    {
        var updates = Every(180);
        var account = new FakeReader(AccountKind.Dav);
        var store = new ContactStore(new AccountSet([account]), updates);

        store.Refresh();
        await store.Idle();
        updates.Set(5);
        store.Refresh();
        await store.Idle();

        Assert.Equal(1, account.ContactReads);
    }

    /// <summary>The contacts wait the interval after a read as the mail does, and follow a change at once.</summary>
    [Fact]
    public async Task TheContactsAreReadAgainAtTheIntervalAndFollowAChangeAtOnce()
    {
        var updates = Every(15);
        var account = new FakeReader(AccountKind.Dav);
        using var store = new ContactStore(new AccountSet([account]), updates);

        store.Refresh();
        await store.Idle();
        Assert.Equal(TimeSpan.FromMinutes(15), store.Armed);

        updates.Set(60);
        Assert.InRange(store.Armed!.Value, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(60));
        Assert.Equal(1, account.ContactReads);
    }

    /// <summary>A contacts failure rests its minute, whatever the interval says.</summary>
    [Fact]
    public async Task AContactsFailureRestsItsMinuteWhateverTheInterval()
    {
        var updates = Every(60);
        var account = new FakeReader(AccountKind.Dav) { Failure = new HttpRequestException("no route") };
        using var store = new ContactStore(new AccountSet([account]), updates);

        store.Refresh();
        await store.Idle();
        Assert.Equal(TimeSpan.FromMinutes(1), store.Armed);

        updates.Set(5);
        Assert.Equal(TimeSpan.FromMinutes(1), store.Armed);
    }
    // --- The network coming back (docs/resilience.md §4) --------------------------------------------

    /// <summary>Only a failure is tried again when the network comes back, and only one that started before it did.</summary>
    [Fact]
    public void TheNetworkComingBackMakesOnlyAFailureStartedBeforeItDue()
    {
        var updates = new OnlineUpdates();
        var said = 0;
        updates.Back += () => said++;
        var before = updates.Comebacks;

        Assert.False(updates.RetryNow(true, before));
        updates.CameBack();
        Assert.Equal(1, said);
        Assert.True(updates.RetryNow(true, before));
        Assert.False(updates.RetryNow(false, before));
        // A fetch that started after it came back and failed rests as ever.
        Assert.False(updates.RetryNow(true, updates.Comebacks));
    }

    /// <summary>
    /// A wake: the mail read before the Wi-Fi was up failed and rests its minute — until the network
    /// is back, when it is read at once, and then rests the interval as a good read does.
    /// </summary>
    [Fact]
    public async Task AMailFailureIsReadAgainTheMomentTheNetworkIsBack()
    {
        var updates = Every(15);
        var account = new FakeReader(AccountKind.Imap) { Failure = new HttpRequestException("no route") };
        using var store = new MailStore(new AccountSet([account]), updates);
        store.Refresh();
        await store.Idle();
        Assert.Equal(TimeSpan.FromMinutes(1), store.Armed);

        account.Failure = null;
        updates.CameBack();
        await store.Idle();

        Assert.Equal(2, account.InboxReads);
        Assert.Equal(TimeSpan.FromMinutes(15), store.Armed);
    }

    /// <summary>The network back with every read gone well, or with nobody looking: nothing is read.</summary>
    [Fact]
    public async Task TheNetworkBackReadsNothingThatIsFreshOrUnlooked()
    {
        var updates = Every(15);
        var fresh = new FakeReader(AccountKind.Imap);
        using var store = new MailStore(new AccountSet([fresh]), updates);
        store.Refresh();
        await store.Idle();
        var unlooked = new FakeReader();
        using var calendars = new AccountCalendars(new AccountSet([unlooked]), updates: updates);

        updates.CameBack();
        await store.Idle();

        Assert.Equal(1, fresh.InboxReads);
        Assert.Equal(TimeSpan.FromMinutes(15), store.Armed);
        Assert.Equal(0, unlooked.CalendarReads);
        Assert.Null(calendars.Armed);
    }

    [Fact]
    public async Task ACalendarsFailureIsReadAgainTheMomentTheNetworkIsBack()
    {
        var updates = Every(30);
        var account = new FakeReader { Failure = new HttpRequestException("no route") };
        account.Calendars.Add(new CalendarRead(new RemoteCalendar("a", "A", null), [], []));
        using var calendars = new AccountCalendars(new AccountSet([account]), updates: updates);
        calendars.Refresh();
        await calendars.Idle();
        Assert.Equal(TimeSpan.FromMinutes(1), calendars.Armed);

        account.Failure = null;
        updates.CameBack();
        await calendars.Idle();

        Assert.Equal(2, account.CalendarReads);
        Assert.Equal(TimeSpan.FromMinutes(30), calendars.Armed);
    }

    [Fact]
    public async Task AContactsFailureIsReadAgainTheMomentTheNetworkIsBack()
    {
        var updates = Every(60);
        var account = new FakeReader(AccountKind.Dav) { Failure = new HttpRequestException("no route") };
        using var store = new ContactStore(new AccountSet([account]), updates);
        store.Refresh();
        await store.Idle();

        account.Failure = null;
        updates.CameBack();
        await store.Idle();

        Assert.Equal(2, account.ContactReads);
        Assert.Equal(TimeSpan.FromMinutes(60), store.Armed);
    }

    /// <summary>A polled store: the share whose fetch failed is fetched at the next poll after the network is back, not five minutes on.</summary>
    [Fact]
    public void AFailedQuoteIsFetchedAtTheFirstPollAfterTheNetworkIsBack()
    {
        var updates = Every(15);
        var asked = 0;
        var store = new MarketStore(
            new MarketAnswers((_, _) => Interlocked.Increment(ref asked) == 1
                ? throw new HttpRequestException("no route")
                : Task.FromResult(MarketQuotes.Parse(MarketOffscreenTests.Answer("MSFT")))),
            MarketDisk.None,
            updates);
        var widget = new object();
        store.Use(widget, MarketAsk.Of(new MarketOptions { Symbol = "MSFT", Range = MarketRange.Day }));
        store.Poll(widget);
        SpinWait.SpinUntil(() => store.Read(widget) is { Problem: not null, Fetching: false }, TimeSpan.FromSeconds(10));

        store.Poll(widget);
        Assert.Equal(1, Volatile.Read(ref asked));
        updates.CameBack();
        store.Poll(widget);
        SpinWait.SpinUntil(() => store.Read(widget) is { Shown: not null, Fetching: false }, TimeSpan.FromSeconds(10));

        Assert.Equal(2, Volatile.Read(ref asked));
        Assert.NotNull(store.Read(widget).Shown);
        store.Release(widget);
    }
}
