// What has gone wrong that Karpo knows of, in one place (docs\widgets.md §394; the person, 2026-10-29: "show the
// errors that happen in the main edit topbar and make them clickable so that they can be shown"). Each of
// Karpo's sources keeps its own failure where it happens — a widget's failed read its line's
// (IRefreshable.Problem: the stale mark), an account's last sync the account
// layer's (SyncReport, ⚙ › Accounts' notes), config.json's what the file said as it was read or written —
// and this is the one list of them the edit bar reads (its error indicator and its list, Dock.Faults.cs):
//
//   * a source says what is wrong of it now, all of it at once (Set, by its kind): what is not said any
//     more is gone — an error that resolves itself disappears —, what is said again keeps when it began;
//   * each fault says what failed (a widget by its kind, an account by its address, Karpo's settings),
//     its icon, a plain message, since when, and what it is of (the widget's key, the account's id), which
//     a click on it in the list opens;
//   * read newest first; the same message of the same thing once (an account's calendars and mail failing
//     for one reason are one row).
//
// It holds no error of its own making and takes nothing from the log (Troubles): karpo.log's lines are for
// whoever reads the log, in English, of faults Karpo carries on from with nothing to click.

using Karpo.Accounts;
using Karpo.Core;

namespace Karpo.Dock;

/// <summary>What a fault is of: a widget, an account, or Karpo's settings (config.json).</summary>
internal enum FaultKind
{
    Widget,
    Account,
    Settings,
}

/// <summary>
/// A fault as its source says it now (§394): by <paramref name="Key"/>, unique among its source's; what failed
/// (<paramref name="Name"/>, by its <paramref name="Glyph"/>), why (<paramref name="Message"/>), and what it is
/// of — a widget's key, an account's id, none for the settings — which its row opens (<paramref name="Of"/>).
/// </summary>
internal sealed record FaultNow(string Key, FaultKind Kind, string Name, string Glyph, string Message, string Of = "");

/// <summary>A fault held (<see cref="Faults"/>): as its source says it, and since when (UTC) it has been said.</summary>
internal sealed record Fault(string Key, FaultKind Kind, string Name, string Glyph, string Message, string Of, DateTime SinceUtc);

/// <summary>Every fault Karpo knows of now (see the top of the file); the dock's thread's.</summary>
internal sealed class Faults
{
    private readonly Dictionary<string, Fault> held = new(StringComparer.Ordinal);

    /// <summary>Every fault, newest first, the same message of the same thing once.</summary>
    public IReadOnlyList<Fault> All { get; private set; } = [];

    /// <summary>How many rows <see cref="All"/> has: what the edit bar's indicator counts.</summary>
    public int Count => All.Count;

    /// <summary>Goes up with every change to <see cref="All"/>: what a reader compares to know it changed.</summary>
    public int Version { get; private set; }

    /// <summary>
    /// What is wrong of <paramref name="kind"/> now, all of it, at <paramref name="utc"/>: what is not in
    /// <paramref name="now"/> any more is gone; what is said again keeps when it began, its words as said now.
    /// True where anything changed.
    /// </summary>
    public bool Set(FaultKind kind, IEnumerable<FaultNow> now, DateTime utc)
    {
        var said = new Dictionary<string, FaultNow>(StringComparer.Ordinal);
        foreach (var fault in now)
        {
            if (fault.Kind == kind)
            {
                said[fault.Key] = fault;
            }
        }
        var changed = false;
        foreach (var gone in held.Values.Where(fault => fault.Kind == kind && !said.ContainsKey(fault.Key)).Select(fault => fault.Key).ToList())
        {
            changed |= held.Remove(gone);
        }
        foreach (var fault in said.Values)
        {
            changed |= Hold(fault, utc);
        }
        return Changed(changed);
    }

    /// <summary><paramref name="fault"/> is wrong now, besides the others of its kind (a settings' file that does not read). True where that changed anything.</summary>
    public bool Raise(FaultNow fault, DateTime utc) => Changed(Hold(fault, utc));

    /// <summary>The fault keyed <paramref name="key"/> is over. True where there was one.</summary>
    public bool Clear(string key) => Changed(held.Remove(key));

    /// <summary>Holds <paramref name="fault"/>, since it began where it was held already. True where that is news.</summary>
    private bool Hold(FaultNow fault, DateTime utc)
    {
        var since = held.TryGetValue(fault.Key, out var was) ? was.SinceUtc : utc;
        var next = new Fault(fault.Key, fault.Kind, fault.Name, fault.Glyph, fault.Message, fault.Of, since);
        if (was == next)
        {
            return false;
        }
        held[fault.Key] = next;
        return true;
    }

    private bool Changed(bool changed)
    {
        if (changed)
        {
            All = [.. held.Values
                .OrderByDescending(fault => fault.SinceUtc)
                .ThenBy(fault => fault.Kind)
                .ThenBy(fault => fault.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(fault => fault.Key, StringComparer.Ordinal)
                .DistinctBy(fault => (fault.Kind, fault.Of, fault.Message))];
            Version++;
        }
        return changed;
    }
}

/// <summary>What each source says is wrong of it now (§394), as the board holds it (<see cref="Faults"/>).</summary>
internal static class FaultSources
{
    /// <summary>config.json does not read: a hand edit in progress, the settings from before kept.</summary>
    internal const string Broken = "settings:broken";

    /// <summary>config.json could not be written.</summary>
    internal const string NotSaved = "settings:notSaved";

    /// <summary>What reading config.json at the start had to do (Config.Recovery).</summary>
    internal const string Recovered = "settings:recovered";

    /// <summary>
    /// The widgets' faults: each of <paramref name="widgets"/> that reads what it shows from somewhere
    /// (<see cref="IRefreshable"/>) and has something to read, whose last read did not bring it up to date
    /// (its <see cref="IRefreshable.Problem"/>): keyed and opened by its key, named
    /// and drawn as <paramref name="subject"/> says (its kind, its icon); none <paramref name="subject"/> knows
    /// nothing of (not the person's: a preview's).
    /// </summary>
    public static IEnumerable<FaultNow> Widgets(IEnumerable<(string Key, object Widget)> widgets, Func<string, (string Name, string Glyph)?> subject)
    {
        foreach (var (key, widget) in widgets)
        {
            if (widget is IRefreshable { CanRefresh: true, Problem: { Length: > 0 } problem } && subject(key) is { } named)
            {
                yield return new FaultNow("widget:" + key, FaultKind.Widget, named.Name, named.Glyph, problem, key);
            }
        }
    }

    /// <summary>
    /// The accounts' faults, as the account layer last said them (Karpo.Accounts' SyncReport, what ⚙ ›
    /// Accounts shows): each part of each of <paramref name="accounts"/> whose last sync failed, in the
    /// Accounts' words ("Couldn't sync: …"), a kind waiting for the account's consent (To Do, §687 a kind unticked
    /// at Google), and §399 each list
    /// To Do refused alone. Named by the account's address, opened by its id.
    /// </summary>
    public static IEnumerable<FaultNow> Accounts(IEnumerable<Account> accounts, SyncReportFile report)
    {
        foreach (var account in accounts)
        {
            if (report.For(account.Id) is not { } said)
            {
                continue;
            }
            var glyph = OptionAccountRows.GlyphOf(AccountRows.Option(account.Kind));
            foreach (var kind in new[] { AccountData.Calendars, AccountData.Contacts, AccountData.Mail, AccountData.Tasks })
            {
                var part = said.Of(kind);
                var message = part?.Problem is { Length: > 0 } problem ? S.SettingsAccountsSyncFailed(problem)
                    : part?.Consent == true ? S.SettingsAccountsSyncConsent(ConsentException.Service(account.Kind, kind))
                    : null;
                if (message is not null)
                {
                    yield return new FaultNow($"account:{account.Id}:{kind}", FaultKind.Account, account.Address, glyph, message, account.Id);
                }
                // §399: a part that alone was not read (a To Do list To Do refused), in its own words, which name it.
                foreach (var item in part?.Items ?? [])
                {
                    if (item is { Off: false, Problem: { Length: > 0 } refused })
                    {
                        yield return new FaultNow($"account:{account.Id}:{kind}:{item.Id}", FaultKind.Account, account.Address, glyph, refused, account.Id);
                    }
                }
            }
        }
    }
}
