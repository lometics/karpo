// Which notification icons stand on the bar, which wait in the hidden icon
// menu behind the chevron, which show nowhere, and in what order — the rules
// both apps follow, apart from any window so they can be tested.
//
// §347: each tray widget its own (the person, 2026-10-25: "scope these settings to the widget even").
// A tray's icons are its options (BarItem.TrayIcons, TrayIconOptions): two trays — on two layouts, or
// two on one bar — show the same icons each its own way, and a drag or the tray's menu writes the one
// it was made in.
//
// Each icon has one of three states (TrayIconOptions.States), as the person asked: "show on
// taskbar, show in extended or something like that, and hide completely".
//   1. The state chosen for the icon itself: dragged onto the bar or into the
//      hidden icon menu, or picked in the tray's menu in edit mode (OptionTray,
//      §261) — the only place that hides one completely, and so the only way
//      back for it.
//   2. Where nothing was chosen, Windows' own choice for the icon (IsPromoted:
//      on the taskbar, or in the hidden icon menu), as Windows 11 shows a new
//      icon. Never hidden completely by default: Windows has no such thing.
// There are no modes and no most icons a bar shows (the person, 2026-10-05: "most
// icons shown should not be a configuration option", "icons on the taskbar should
// also be removed as it is not needed anymore").
//
// The order is one list over every icon that has been placed (TrayIconOptions.Order), the
// ones on the bar first and the hidden ones after them. Icons the list doesn't
// name follow the ones it does, in the order the tray reports them, so an icon
// that has just appeared goes to the end and nothing else moves.
//
// Windows keeps one choice per icon (IsPromoted), however many trays Karpo has. A choice made in a
// tray is told Windows, as before (NewChoices); every other tray that had made none for the icon keeps
// it where it stood — that place written down as its own —, so it does not move with Windows' word
// for a choice made elsewhere. A choice made in Windows' Settings is the person's word on the icon,
// and every tray follows it (FollowWindows).

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>One notification icon as the rules see it: how the config tells it apart, and whether Windows hides it.</summary>
/// <param name="Identity">What <see cref="TrayIconOptions.States"/> and <see cref="TrayIconOptions.Order"/> name it by.</param>
/// <param name="AppHidden">Whether Windows' own choice for the icon is "hidden" (the taskbar would put it in the hidden icon menu).</param>
public readonly record struct TrayEntry(string Identity, bool AppHidden);

/// <summary>
/// The three places a notification icon can be in, one per icon (<see cref="TrayIconOptions.States"/>).
/// Windows keeps only the first two (IsPromoted 1 and 0); hiding completely is Karpo's own, and
/// tells Windows "not promoted" (<see cref="TrayLayout.Promoted"/>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TrayState>))]
public enum TrayState
{
    /// <summary>"Show on taskbar": on the bar.</summary>
    Taskbar,

    /// <summary>"Show in hidden icon menu": behind the chevron, in the overflow.</summary>
    Overflow,

    /// <summary>"Hide completely": on neither. The app and its icon go on; Karpo shows it nowhere.</summary>
    Hidden,
}

/// <summary>
/// §347: a tray widget's own icons (<see cref="BarItem.TrayIcons"/>, the file's "trayIcons"): where each
/// shows and the order they were dragged into. Absent while it says nothing: every icon where Windows
/// puts it, in the order the tray reports them.
/// </summary>
public sealed class TrayIconOptions
{
    /// <summary>
    /// The state of each notification icon (its identity, <see cref="TrayIdentity"/>): on the taskbar, in
    /// the hidden icon menu behind the chevron, or hidden completely (<see cref="TrayState"/>). An icon it
    /// doesn't name goes where Windows' own choice for it says (IsPromoted), as Windows 11 shows a new icon.
    /// </summary>
    public Dictionary<string, TrayState> States { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The order the icons were dragged into: every icon that has been placed, the ones on the bar first
    /// and then the hidden ones, by the same identity <see cref="States"/> uses. Icons it doesn't name
    /// follow in the order the tray reports them (see <see cref="TrayLayout"/>).
    /// </summary>
    public List<string> Order { get; set; } = [];

    /// <summary>A deep copy.</summary>
    public TrayIconOptions Copy() => new() { States = new(States ?? [], StringComparer.OrdinalIgnoreCase), Order = [.. Order ?? []] };

    /// <summary>These options as the file keeps them (read case aside); null — none written — where they say nothing.</summary>
    public TrayIconOptions? Normalized()
    {
        States = new(States ?? [], StringComparer.OrdinalIgnoreCase);
        Order ??= [];
        return States.Count == 0 && Order.Count == 0 ? null : this;
    }
}

/// <summary>
/// The place an icon dragged lands: on the bar, or in the hidden icon menu behind the chevron.
/// </summary>
public enum TrayArea
{
    Shown,
    Overflow,
}

/// <summary>
/// Where a dragged icon would land: the area, and its place among that area's icons <b>without the
/// dragged one</b> — 0 before the first of them, their count after the last. An icon moved along the
/// bar or within the overflow is counted out of its own area first, so a place past its old one is
/// not one too far (TrayDrags works the place out that way, and <see cref="TrayLayout.Apply"/> reads it so).
/// </summary>
public readonly record struct TrayDrop(TrayArea Area, int Index);

/// <summary>How a tray's <see cref="TrayIconOptions"/> split the icons, and how the trays of a config keep them.</summary>
public static class TrayLayout
{
    /// <summary>The icons in the order the bar and the tray's menu show them: the placed ones first, then the rest.</summary>
    public static List<TrayEntry> InOrder(TrayIconOptions? tray, IReadOnlyList<TrayEntry> icons)
    {
        var order = tray?.Order ?? [];
        var places = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < order.Count; index++)
        {
            if (!string.IsNullOrWhiteSpace(order[index]))
            {
                _ = places.TryAdd(order[index], index);
            }
        }
        return [.. icons
            .Select((icon, arrived) => (icon, arrived))
            .OrderBy(entry => places.TryGetValue(entry.icon.Identity, out var place) ? place : int.MaxValue)
            .ThenBy(entry => entry.arrived)
            .Select(entry => entry.icon)];
    }

    /// <summary>
    /// Where <paramref name="icon"/> is: the state chosen for it, or where nothing was, Windows' own
    /// choice — on the taskbar when it promotes the icon, in the hidden icon menu when not. Never
    /// <see cref="TrayState.Hidden"/> unless that was chosen.
    /// </summary>
    public static TrayState State(TrayIconOptions? tray, TrayEntry icon) =>
        Choice(tray, icon.Identity) ?? Default(icon.AppHidden);

    /// <summary>Where an icon nobody chose for goes: Windows' word on it (IsPromoted), as Windows 11 shows a new icon.</summary>
    public static TrayState Default(bool appHidden) => appHidden ? TrayState.Overflow : TrayState.Taskbar;

    /// <summary>The state chosen for <paramref name="identity"/>, or null when Windows' own choice stands.</summary>
    public static TrayState? Choice(TrayIconOptions? tray, string identity) =>
        tray?.States is { } states && states.TryGetValue(identity, out var chosen) && Enum.IsDefined(chosen) ? chosen : null;

    /// <summary>
    /// What Windows' own record of the icon (IsPromoted) says for <paramref name="state"/>: promoted on
    /// the taskbar, not promoted in the hidden icon menu — and not promoted hidden completely, which
    /// Windows has no word for.
    /// </summary>
    public static bool Promoted(TrayState state) => state == TrayState.Taskbar;

    /// <summary>
    /// The icons of the bar and of the hidden icon menu, in order. Icons hidden completely are in
    /// neither (<see cref="Off"/>).
    /// </summary>
    public static (List<TrayEntry> Shown, List<TrayEntry> Overflow) Split(TrayIconOptions? tray, IReadOnlyList<TrayEntry> icons)
    {
        var shown = new List<TrayEntry>();
        var overflow = new List<TrayEntry>();
        foreach (var icon in InOrder(tray, icons))
        {
            switch (State(tray, icon))
            {
                case TrayState.Taskbar:
                    shown.Add(icon);
                    break;
                case TrayState.Overflow:
                    overflow.Add(icon);
                    break;
            }
        }
        return (shown, overflow);
    }

    /// <summary>The icons hidden completely: shown neither on the bar nor in the hidden icon menu, in order.</summary>
    public static List<TrayEntry> Off(TrayIconOptions? tray, IReadOnlyList<TrayEntry> icons) =>
        [.. InOrder(tray, icons).Where(icon => State(tray, icon) == TrayState.Hidden)];

    /// <summary>
    /// <paramref name="identity"/> set to <paramref name="state"/>, as the tray's menu sets it (OptionTray): its
    /// place in the order is left alone. True when that changed the choice.
    /// </summary>
    public static bool Set(TrayIconOptions tray, string identity, TrayState state)
    {
        if (string.IsNullOrWhiteSpace(identity) || !Enum.IsDefined(state) || Choice(tray, identity) == state)
        {
            return false;
        }
        tray.States ??= new(StringComparer.OrdinalIgnoreCase);
        tray.States[identity] = state;
        return true;
    }

    /// <summary>
    /// <paramref name="identity"/> dropped where <paramref name="drop"/> says, written into
    /// <paramref name="tray"/>: the state (on the taskbar, or in the hidden icon menu) and the order
    /// within that area. <paramref name="icons"/> are the icons the tray has now. True when anything
    /// changed.
    /// <para>
    /// <paramref name="seen"/> are the area's icons as the drag showed them, the dragged one out —
    /// what <paramref name="drop"/>'s place counts. The icon then goes before the one it was shown
    /// before (after the one it was shown after, at the end), wherever that stands now: a place
    /// counted on a tray that has changed since (an icon come or gone) would land beside another.
    /// </para>
    /// </summary>
    public static bool Apply(TrayIconOptions tray, IReadOnlyList<TrayEntry> icons, string identity, TrayDrop drop, IReadOnlyList<string>? seen = null)
    {
        if (string.IsNullOrWhiteSpace(identity) || !icons.Any(icon => Same(icon.Identity, identity)))
        {
            return false;
        }
        var before = Signature(tray, icons);
        tray.States ??= new(StringComparer.OrdinalIgnoreCase);
        tray.States[identity] = drop.Area == TrayArea.Shown ? TrayState.Taskbar : TrayState.Overflow;
        var (shown, overflow) = Split(tray, icons);
        var target = (drop.Area == TrayArea.Shown ? shown : overflow).Select(icon => icon.Identity).ToList();
        var others = (drop.Area == TrayArea.Shown ? overflow : shown).Select(icon => icon.Identity).ToList();
        target.RemoveAll(entry => Same(entry, identity));
        others.RemoveAll(entry => Same(entry, identity));
        target.Insert(Place(target, drop.Index, seen), identity);
        var order = drop.Area == TrayArea.Shown ? target.Concat(others) : others.Concat(target);
        var placed = new HashSet<string>(order, StringComparer.OrdinalIgnoreCase);
        // Icons that aren't here now (their app isn't running), and the ones hidden completely, keep
        // their places behind the rest.
        tray.Order = [.. order, .. (tray.Order ?? []).Where(entry => !string.IsNullOrWhiteSpace(entry) && !placed.Contains(entry))];
        return Signature(tray, icons) != before;
    }

    /// <summary>The tray widgets of every layout of <paramref name="config"/> (a folder holds none).</summary>
    public static IEnumerable<BarItem> Trays(Config config) =>
        config.AllIslands.Where(island => island is { Kind: BarKind.Tray });

    /// <summary>
    /// Windows' own choice for <paramref name="identity"/> changed while Karpo ran (Settings,
    /// Personalization, Taskbar, Other system tray icons: IsPromoted): it is the person's newest word on
    /// the icon, so it becomes its state in every tray of <paramref name="config"/> — on the taskbar, or
    /// in the hidden icon menu. An icon a tray hides completely stays so there: Windows has no word for
    /// that, and only the tray's menu brings it back. True when that changed anything.
    /// </summary>
    public static bool FollowWindows(Config config, string identity, bool promoted)
    {
        var changed = false;
        foreach (var tray in Trays(config))
        {
            var chosen = Choice(tray.TrayIcons, identity);
            var state = promoted ? TrayState.Taskbar : TrayState.Overflow;
            if (string.IsNullOrWhiteSpace(identity) || chosen == TrayState.Hidden || chosen == state)
            {
                continue;
            }
            (tray.TrayIcons ??= new()).States[identity] = state;
            changed = true;
        }
        return changed;
    }

    /// <summary>
    /// The states a tray of <paramref name="after"/> holds that the same tray of <paramref name="before"/>
    /// (its layout's and its own id) did not, or held otherwise, as what Windows' own record (IsPromoted)
    /// is told, so the two agree: promoted for the taskbar, not promoted for the hidden icon menu and for
    /// hidden completely (<see cref="Promoted"/>); where two trays chose anew, the last one's.
    /// <para>
    /// Every other tray of <paramref name="after"/> that has made no choice for such an icon gets the
    /// place it shows it in now — Windows' word, <paramref name="appHidden"/> (null for an icon not
    /// there now, which is left alone) — as its own, so it stays there when Windows is told otherwise.
    /// </para>
    /// </summary>
    public static List<(string Identity, bool Promoted)> NewChoices(Config before, Config after, Func<string, bool?> appHidden)
    {
        var was = new Dictionary<(string, string), TrayIconOptions?>();
        foreach (var layout in before.Layouts.Where(layout => layout?.Islands is not null))
        {
            foreach (var tray in layout.Islands.Where(island => island is { Kind: BarKind.Tray }))
            {
                was[(layout.Id, tray.Id)] = tray.TrayIcons;
            }
        }
        var chosen = new List<(BarItem Tray, string Identity, TrayState State)>();
        foreach (var layout in after.Layouts.Where(layout => layout?.Islands is not null))
        {
            foreach (var tray in layout.Islands.Where(island => island is { Kind: BarKind.Tray }))
            {
                var old = was.GetValueOrDefault((layout.Id, tray.Id));
                foreach (var (identity, state) in tray.TrayIcons?.States ?? [])
                {
                    if (Enum.IsDefined(state) && Choice(old, identity) != state)
                    {
                        chosen.Add((tray, identity, state));
                    }
                }
            }
        }
        var trays = Trays(after).ToList();
        var changes = new List<(string Identity, bool Promoted)>();
        foreach (var (made, identity, state) in chosen)
        {
            if (appHidden(identity) is { } hidden)
            {
                foreach (var other in trays.Where(other => other != made && Choice(other.TrayIcons, identity) is null))
                {
                    (other.TrayIcons ??= new()).States[identity] = Default(hidden);
                }
            }
            changes.RemoveAll(change => Same(change.Identity, identity));
            changes.Add((identity, Promoted(state)));
        }
        return changes;
    }

    /// <summary>
    /// The identities in every tray's options written the way <see cref="TrayIdentity"/> writes them
    /// now, so a choice made for a packaged app is not left behind on the path of the version it had
    /// then, and a state this build has no word for dropped. True when something changed and the
    /// file should get it back.
    /// </summary>
    public static bool Repair(Config config)
    {
        var changed = false;
        foreach (var tray in Trays(config))
        {
            if (tray.TrayIcons is { } options)
            {
                changed |= Rename(options);
                tray.TrayIcons = options.Normalized();
            }
        }
        return changed;
    }

    /// <summary>
    /// The identities in <paramref name="tray"/>'s states and order written as <see cref="TrayIdentity"/>
    /// writes them now. Where two of them fall together (the same app under two of its versions), the
    /// one that came first wins, as it is the one whose place the order already holds.
    /// </summary>
    private static bool Rename(TrayIconOptions tray)
    {
        var changed = false;
        if (tray.States is { Count: > 0 } states)
        {
            var renamed = new Dictionary<string, TrayState>(states.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (identity, state) in states)
            {
                if (string.IsNullOrWhiteSpace(identity) || !Enum.IsDefined(state))
                {
                    changed = true;
                    continue;
                }
                var now = TrayIdentity.Stable(identity);
                var kept = renamed.TryAdd(now, state);
                changed |= now != identity || !kept;
            }
            tray.States = renamed;
        }
        if (tray.Order is { Count: > 0 } order)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var renamed = new List<string>(order.Count);
            foreach (var identity in order)
            {
                var now = TrayIdentity.Stable(identity ?? "");
                if (string.IsNullOrWhiteSpace(now) || !seen.Add(now))
                {
                    changed = true;
                    continue;
                }
                changed |= now != identity;
                renamed.Add(now);
            }
            tray.Order = renamed;
        }
        return changed;
    }

    /// <summary>
    /// Where among <paramref name="target"/> a drop at <paramref name="index"/> among
    /// <paramref name="seen"/> goes: before the icon it was shown before, else after the one it was
    /// shown after, else the index itself.
    /// </summary>
    private static int Place(List<string> target, int index, IReadOnlyList<string>? seen)
    {
        if (seen is not null)
        {
            if (index >= 0 && index < seen.Count && target.FindIndex(entry => Same(entry, seen[index])) is >= 0 and var before)
            {
                return before;
            }
            if (index > 0 && index - 1 < seen.Count && target.FindIndex(entry => Same(entry, seen[index - 1])) is >= 0 and var after)
            {
                return after + 1;
            }
        }
        return Math.Clamp(index, 0, target.Count);
    }

    /// <summary>Whether two identities name the same icon (case aside, as <see cref="TrayIconOptions.States"/> is read).</summary>
    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>What the bar would show, as one string: for telling a change that matters from one that doesn't.</summary>
    private static string Signature(TrayIconOptions tray, IReadOnlyList<TrayEntry> icons)
    {
        var (shown, overflow) = Split(tray, icons);
        return string.Join("\n", shown.Select(icon => icon.Identity)) + "\v" + string.Join("\n", overflow.Select(icon => icon.Identity));
    }
}
