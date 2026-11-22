// The order tray icons came in, kept for longer than the icons themselves, and
// Windows' own choice for them watched for changes — the two things that kept a
// tray from standing still.
//
// Arrivals. Icons nobody has placed (a tray's order doesn't name them) follow the
// order the tray reports them in, and that was the order their adds came in.
// But an app that takes its icon away and adds it again (Steam, Docker, a sync
// client changing state; any app restarting) came in last again, so its icon
// jumped to the end of the bar, or past the maximum into the overflow; and every
// start of Karpo asks every app to add its icon again (TaskbarCreated), in
// whatever order they answer, so the bar was shuffled at every start. An icon
// keeps the place it first came in at now: for the session, whatever its app
// does, and across starts through the list of icons the dock writes for the
// settings app (tray-icons.json), which seeds the next session.
//
// Windows' choice. HKCU\Control Panel\NotifyIconSettings\<icon>\IsPromoted is
// what Settings writes when an icon is switched on or off for the taskbar. A
// change to it while Karpo runs is the person's newest word on the icon, and
// becomes Karpo's choice for it (TrayLayout.FollowWindows); WindowsChoices tells
// such a change from the same icon merely being seen again, and from Karpo's own
// word read back (§347: one tray's choice told Windows is no word for every tray).

namespace Karpo.Core;

/// <summary>The place each tray icon first came in at, by its identity (<see cref="TrayIdentity"/>).</summary>
public sealed class TrayArrivals
{
    private readonly Dictionary<string, long> ranks = new(StringComparer.OrdinalIgnoreCase);
    private long next;

    /// <summary>
    /// The order a session before this one left (the dock's tray-icons.json): these come first, in
    /// this order, ahead of anything new. Identities already ranked keep their rank.
    /// </summary>
    public void Seed(IEnumerable<string> identities)
    {
        foreach (var identity in identities)
        {
            _ = Rank(identity);
        }
    }

    /// <summary>Where <paramref name="identity"/> came in: the rank it was given the first time it was asked for.</summary>
    public long Rank(string identity)
    {
        if (!ranks.TryGetValue(identity, out var rank))
        {
            ranks[identity] = rank = next++;
        }
        return rank;
    }

    /// <summary>
    /// <paramref name="items"/> in the order their identities first came in; two of one identity (two
    /// windows of one app) keep the order they had. Stable, and every identity not yet seen is ranked
    /// after everything seen before it.
    /// </summary>
    public List<T> Order<T>(IReadOnlyList<T> items, Func<T, string> identity) =>
        [.. items.Select((item, at) => (item, at, rank: Rank(identity(item))))
            .OrderBy(entry => entry.rank)
            .ThenBy(entry => entry.at)
            .Select(entry => entry.item)];
}

/// <summary>
/// Changes to Windows' own choice for the icons the tray has (IsPromoted), told apart from icons
/// being seen for the first time: only a live icon whose record said one thing and now says the
/// other, after Windows' records were read again, is a change.
/// </summary>
public sealed class WindowsChoices
{
    private readonly Dictionary<string, bool> seen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> told = new(StringComparer.OrdinalIgnoreCase);
    private int reading = int.MinValue;

    /// <summary>
    /// Karpo wrote <paramref name="promoted"/> into Windows' record of <paramref name="identity"/> (a
    /// tray's choice, TrayLayout.NewChoices): read back, it is Karpo's own word and no change.
    /// </summary>
    public void Told(string identity, bool promoted) => told[identity] = promoted;

    /// <summary>
    /// What <paramref name="icons"/> say now — each by its key (one live icon: its window and id, or
    /// its GUID), its identity and whether Windows keeps it on the taskbar — after the records were
    /// read for the <paramref name="generation"/>th time. The identities whose choice changed since
    /// the last call come back, with the new choice. Nothing changes on the first call, for an icon
    /// not seen before, or while the records were not read again; nor for one that reads as Karpo
    /// told it (<see cref="Told"/>), which a read that finds it so has heard back.
    /// </summary>
    public List<(string Identity, bool Promoted)> Changes(int generation, IEnumerable<(string Key, string Identity, bool Promoted)> icons)
    {
        var changes = new List<(string, bool)>();
        var reread = reading != int.MinValue && generation != reading;
        reading = generation;
        var now = new Dictionary<string, bool>(StringComparer.Ordinal);
        var heard = new List<string>();
        foreach (var (key, identity, promoted) in icons)
        {
            now[key] = promoted;
            if (reread && told.TryGetValue(identity, out var said) && said == promoted)
            {
                heard.Add(identity);
                continue;
            }
            if (reread && seen.TryGetValue(key, out var before) && before != promoted
                && !changes.Exists(change => string.Equals(change.Item1, identity, StringComparison.OrdinalIgnoreCase)))
            {
                changes.Add((identity, promoted));
            }
        }
        foreach (var identity in heard)
        {
            told.Remove(identity);
        }
        seen.Clear();
        foreach (var (key, promoted) in now)
        {
            seen[key] = promoted;
        }
        return changes;
    }
}
