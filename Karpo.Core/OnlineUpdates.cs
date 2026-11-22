// How often everything Karpo fetches from the internet is fetched again: one interval for all of it.
//
// The person (2026-10-05): "there should be only one counter for online updates, e.g. the stock
// widgets should follow it, weather should update in that duration, and other online stuff should
// also just update in that intervall." So there is one setting, Config.OnlineUpdateMinutes, on the
// General page, and no widget has an interval of its own any more: the weather's and the stocks'
// "Update" went, and the accounts' calendars, mail and contacts, which had fixed cadences of their
// own (10, 5 and 5 minutes), follow it too.
//
// HOW A SERVICE FOLLOWS IT (a new online widget — a feed, a rate — does exactly this):
//
//   * Take an OnlineUpdates in the constructor, defaulting to OnlineUpdates.Shared, so a test can
//     hand in one of its own and set it without reaching any other test.
//   * When deciding whether a fetch is due, ask Every(floor) — the interval, held to the provider's
//     own floor — at that moment. Asking each time is what makes a change apply at once: the
//     stores the dock polls once a minute (weather, stocks) need nothing else.
//   * A service that arms a timer of its own (the calendars, the mail) listens to Changed and arms
//     it again. Changed is raised on the thread that set the value, which in the dock is the dock's
//     own thread (Dock.Apply), so a handler that does real work hands it to its own thread or the
//     pool, as those stores already do with their timers.
//   * A floor, where a provider has one, is the service's to declare and to say why, beside it in a
//     comment: Every(floor) never asks more often than the floor, whatever the setting.
//   * The person's own Refresh — a button on a flyout or a card — is not held to the interval, nor
//     to any floor but what the provider itself asked for (a 429's back-off).
//   * The network coming back (docs/resilience.md §4): after a wake the first fetch often runs before
//     the Wi-Fi is up, and fails. A failed fetch is not left to its retry wait: note Comebacks as a
//     fetch starts, and ask RetryNow(failed, noted) where the retry wait is decided — true once the
//     network came back since. A store the dock polls needs nothing else (the dock polls every widget
//     as it hears it, Dock.Network.cs); one with a timer of its own also listens to Back and arms it.
//     A provider's own back-off (a 429) is still waited out.
//
// The dock sets the shared one from the config when it starts and whenever the config changes
// (Dock.Apply), before the bars are arranged again, so the islands' ticks (IntervalMs) are read
// afresh with it. The settings app only edits the number in the file.

namespace Karpo.Core;

/// <summary>
/// The one interval online widgets and services update at (<see cref="Config.OnlineUpdateMinutes"/>),
/// live: <see cref="Shared"/> is the dock's, set from the config, and <see cref="Changed"/> says
/// when it moves. See the note at the top of this file for how a service follows it.
/// </summary>
public sealed class OnlineUpdates
{
    /// <summary>
    /// The choices the General page offers: the ones the weather's and the stocks' own "Update" had
    /// between them, so whatever somebody had chosen on either is still one of them.
    /// </summary>
    public static IReadOnlyList<int> Choices { get; } = [5, 15, 30, 60, 120, 180];

    /// <summary>
    /// What a new config says, and an older one whose widgets did not all agree on something else:
    /// a quarter of an hour. It was the stocks' default; it sits between the accounts' 5 and 10
    /// minutes and the weather's 30, so nobody's mail slows by more than ten minutes and nobody's
    /// forecast is asked for more than twice as often as it was; and it is the weather's own floor.
    /// </summary>
    public const int DefaultMinutes = 15;

    /// <summary>The least the setting may say: as often as the stocks could be asked before.</summary>
    public const int LeastMinutes = 5;

    /// <summary>The most it may say: as seldom as either widget could be asked before.</summary>
    public const int MostMinutes = 180;

    private int minutes = DefaultMinutes;

    /// <summary>The dock's own, which <c>Dock.Apply</c> sets from the config. Tests make their own.</summary>
    public static OnlineUpdates Shared { get; } = new();

    /// <summary>The interval was set to another value. Raised on the thread that set it, after it is set.</summary>
    public event Action? Changed;

    /// <summary>How many minutes apart online updates are now, <see cref="LeastMinutes"/> to <see cref="MostMinutes"/>.</summary>
    public int Minutes => Volatile.Read(ref minutes);

    /// <summary>The interval now, as a span.</summary>
    public TimeSpan Interval => TimeSpan.FromMinutes(Minutes);

    /// <summary>A value in the file held to what the setting may say.</summary>
    public static int Clamp(int value) => Math.Clamp(value, LeastMinutes, MostMinutes);

    /// <summary>
    /// The interval a service with a floor of <paramref name="floorMinutes"/> updates at: the
    /// setting, but never more often than the floor. The floor is the service's own and says why
    /// beside it.
    /// </summary>
    public TimeSpan Every(int floorMinutes = 0) => TimeSpan.FromMinutes(Math.Max(Minutes, floorMinutes));

    private int comebacks;

    /// <summary>The network came back (an address arrived, after a wake or a reconnect). Raised on the thread that said so.</summary>
    public event Action? Back;

    /// <summary>How many times the network has come back: noted as a fetch starts, for <see cref="RetryNow"/>.</summary>
    public int Comebacks => Volatile.Read(ref comebacks);

    /// <summary>A fetch that <paramref name="failed"/>, started when <see cref="Comebacks"/> was <paramref name="noted"/>, is tried again now: the network came back since.</summary>
    public bool RetryNow(bool failed, int noted) => failed && Comebacks != noted;

    /// <summary>Says the network came back: every failed fetch is due (<see cref="RetryNow"/>), and <see cref="Back"/> is raised.</summary>
    public void CameBack()
    {
        Interlocked.Increment(ref comebacks);
        Back?.Invoke();
    }

    /// <summary>
    /// Makes <paramref name="value"/> the interval, held to <see cref="LeastMinutes"/> to
    /// <see cref="MostMinutes"/>; raises <see cref="Changed"/> when that is a different one.
    /// </summary>
    public void Set(int value)
    {
        var held = Clamp(value);
        if (Interlocked.Exchange(ref minutes, held) != held)
        {
            Changed?.Invoke();
        }
    }
}
