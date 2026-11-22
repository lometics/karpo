// What the widgets of one folder agree about while it is open: its channels (docs\widgets.md §9,
// "Channels").
//
// The person, 2026-10-09: "the widgets now need a way to synchronize, e.g. if you select a date on
// the calendar it should show the correct events for that day … as generic as possible so that
// other combinations are possible as well". So nothing here knows a month calendar or an agenda.
// A **channel** is a named, typed value a folder holds for as long as it is open: a widget
// **publishes** to it (the month calendar sets the day on a click) and a widget **follows** it (the
// agenda shows that day's events). Which widget does which is its list record's answer
// (ListWidget.Publishes, ListWidget.Follows), so a new pairing is two words in two records and no
// code that names both.
//
// Four rules, the shared-state seam's (Karpo.Dock\Islands\WidgetContainer.cs), which carries them:
//
//  * **Folder-scoped and transient.** The folder holds the values and clears them when it closes:
//    a day somebody clicked is about this look at the folder, so it opens at today again. Nothing
//    is written to the config.
//  * **Typed by the channel.** A Channel<DateOnly> is read as a DateOnly; a value of another type
//    reads as unset, never as a wrong one.
//  * **Unset is the channel's own default**, which the reader supplies (today, for the day): a
//    widget with nobody publishing beside it — on its own, in a picture, in a folder of one — draws
//    what it would draw anyway.
//  * **A follower always follows.** Each follower had a switch per channel it follows ("Follow the
//    folder's day") until 2026-10-19 (§240), the person: "always follow the folders day". A widget
//    that kept a day of its own beside a month that picked another only looked broken, so a kind
//    that follows a channel follows it, always; a file's "unfollowed" is read past and not written.

namespace Karpo.Core;

/// <summary>
/// A value the widgets of one folder agree about while it is open (see the top of Channels.cs). One
/// per meaning, in <see cref="Channels.All"/>; compared by <see cref="Id"/>.
/// </summary>
public abstract record Channel
{
    private protected Channel(string id) => Id = id;

    /// <summary>The channel's word, which never changes once released.</summary>
    public string Id { get; }

    /// <summary>What the value is.</summary>
    public abstract Type Type { get; }

    /// <summary>
    /// A choice somebody made in the folder, which Escape takes back before it closes the folder: the
    /// app picked. Not the day, which is where the folder is rather than something picked out of it.
    /// </summary>
    public bool Escapes { get; init; }
}

/// <summary>A channel whose value is a <typeparamref name="T"/>.</summary>
public sealed record Channel<T>(string Key) : Channel(Key)
{
    public override Type Type => typeof(T);
}

/// <summary>Every channel a folder's widgets can publish to and follow.</summary>
public static class Channels
{
    /// <summary>
    /// The day the folder is showing: set by a click on a month calendar, followed by an agenda.
    /// Unset is today.
    /// </summary>
    public static readonly Channel<DateOnly> Day = new("day");

    /// <summary>
    /// The day under the pointer, wherever in the folder it is: set while the pointer is over a day
    /// (a bar of screen time's fortnight), unset as it leaves, so a widget showing one day shows that
    /// one meanwhile and the folder's day again after. Unset is none.
    /// </summary>
    public static readonly Channel<DateOnly> DayHover = new("dayHover");

    /// <summary>
    /// The app picked in the folder: set by a click on an app (screen time's apps, a piece of its
    /// bars), taken back by a second click or Escape; followed by whatever shows apps. Unset is none.
    /// </summary>
    public static readonly Channel<AppId> App = new("app") { Escapes = true };

    /// <summary>
    /// The app under the pointer, wherever in the folder it is: set while the pointer is over an
    /// app, unset as it leaves, so every widget showing that app lights it. Unset is none.
    /// </summary>
    public static readonly Channel<AppId> AppHover = new("appHover");

    /// <summary>Every channel.</summary>
    public static readonly Channel[] All = [Day, DayHover, App, AppHover];

    /// <summary>The channel whose id is <paramref name="id"/>, or null for a word this build does not know.</summary>
    public static Channel? Find(string? id) => All.FirstOrDefault(channel => string.Equals(channel.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// The channels a widget of <paramref name="item"/>'s kind follows: its list record's, none on a
    /// bar.
    /// </summary>
    public static IReadOnlyList<Channel> FollowedBy(WidgetItem item) =>
        item is CellItem cell && ListWidgets.Find(cell.Kind) is { } line ? line.Follows : [];

    /// <summary>Whether <paramref name="item"/> follows <paramref name="channel"/>: its kind does, always (see the top of the file).</summary>
    public static bool Follows(WidgetItem item, Channel channel) => FollowedBy(item).Contains(channel);
}

/// <summary>
/// The values a folder's widgets agree about, for the life of one look at it (see the top of
/// Channels.cs). The folder holds one; nothing here is ever written to a file.
/// </summary>
public sealed class ChannelValues
{
    private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

    /// <summary>What <paramref name="channel"/> holds, or null when nobody has published to it.</summary>
    public T? Of<T>(Channel<T> channel)
        where T : struct =>
        values.TryGetValue(channel.Id, out var found) && found is T value ? value : null;

    /// <summary>Whether anybody has published to <paramref name="channel"/>.</summary>
    public bool Has(Channel channel) => values.ContainsKey(channel.Id);

    /// <summary>Publishes <paramref name="value"/>; true when that changed anything, which is when the folder draws again.</summary>
    public bool Set<T>(Channel<T> channel, T value)
        where T : struct
    {
        if (Of(channel) is { } now && EqualityComparer<T>.Default.Equals(now, value))
        {
            return false;
        }
        values[channel.Id] = value;
        return true;
    }

    /// <summary>Takes back what <paramref name="channel"/> holds: it reads as unset again. True when it held anything.</summary>
    public bool Unset(Channel channel) => values.Remove(channel.Id);

    /// <summary>Takes back every channel Escape takes back (<see cref="Channel.Escapes"/>). True when any held anything.</summary>
    public bool Escape()
    {
        var any = false;
        foreach (var channel in Channels.All.Where(channel => channel.Escapes))
        {
            any |= Unset(channel);
        }
        return any;
    }

    /// <summary>What every channel holds now, by its id: what a folder left keeps (docs\widgets.md §357).</summary>
    public IReadOnlyDictionary<string, object?> Snapshot() => new Dictionary<string, object?>(values, StringComparer.Ordinal);

    /// <summary>Every channel as <paramref name="snapshot"/> had it, and nothing else: a folder opened where it was left.</summary>
    public void Restore(IReadOnlyDictionary<string, object?> snapshot)
    {
        values.Clear();
        foreach (var (id, value) in snapshot)
        {
            values[id] = value;
        }
    }

    /// <summary>Everything published is forgotten: the folder closed. True when anything was set.</summary>
    public bool Clear()
    {
        if (values.Count == 0)
        {
            return false;
        }
        values.Clear();
        return true;
    }
}

/// <summary>
/// An app as a channel carries it (<see cref="Channels.App"/>): the key the app is known by — a
/// screen-time key, an executable's path or "aumid:" and the app's id (ScreenTimeApps.KeyOf).
/// Two keys that differ only in case are the one app, as Windows' paths are.
/// </summary>
public readonly record struct AppId(string Key)
{
    public bool Equals(AppId other) => string.Equals(Key, other.Key, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => Key is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Key);
}
