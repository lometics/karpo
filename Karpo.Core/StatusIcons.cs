// The status icons' three places (the person, 2026-10-08: "for the status icons you should be able
// to set which status icons are visible and which should be hidden / hidden behind an extend arrow,
// (basically the same style as it is done with the tray icons.) as default all should show").
//
// Each status icon the Status icons entry can show (the dock's IndicatorKind, QuickSettings\
// Services\Indicators.cs) has one of the tray's three states (TrayState), per entry:
//
//   * Show on taskbar — its own cell on the bar, while it is so. Every icon's, unless somebody chose;
//   * Show behind the arrow — not on the bar: the entry's own chevron, after its icons, opens a small
//     flyout of them, as the tray's "^" opens its hidden icons. The chevron stands only while one of
//     these is so;
//   * Hide completely — nowhere at all.
//
// The microphone and the location in use together are one icon in Windows' tray; here each has its
// own state, and the one icon is made only of two that stand in the same place (on the bar, or
// behind the arrow). Set apart, each shows alone in its own place.
//
// The choices are the entry's own (BarItem.StatusIcons: "statusIcons": { "states": { "camera":
// "Overflow" } }), so two status icons entries on two bars may differ, as two clocks do. Only what
// is not "Show on taskbar" is written, so an entry nobody set writes nothing and a file from before
// this reads as every icon on the bar. The map merges key by key as the tray's does (ConfigMerge).

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>A status icon the Status icons entry shows, as its settings page lists them, in Windows' order.</summary>
public enum StatusIcon
{
    Microphone,
    Camera,
    Location,
    Presence,
    AirplaneMode,
    Hotspot,
    Metered,
    EnergySaver,
    DoNotDisturb,
    WindowsUpdate,
}

/// <summary>
/// The Status icons entry's one option: where each of its icons shows (<see cref="StatusIcons"/>).
/// Absent, or an icon it doesn't name, means on the taskbar; only the others are written.
/// </summary>
public sealed class StatusIconOptions
{
    /// <summary>Each icon's place by its key (<see cref="StatusIcons.Key"/>): Overflow or Hidden; Taskbar is never written.</summary>
    public Dictionary<string, TrayState> States { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// These options with only what the file needs: a place that is neither of the other two, a
    /// state this build doesn't know and an empty key go; a key this build doesn't know stays, for
    /// the build that wrote it. Null — the options dropped — where nothing is left.
    /// </summary>
    public StatusIconOptions? Normalized()
    {
        var kept = new Dictionary<string, TrayState>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, state) in States ?? [])
        {
            if (!string.IsNullOrWhiteSpace(key) && state is TrayState.Overflow or TrayState.Hidden)
            {
                kept[key.Trim()] = state;
            }
        }
        if (kept.Count == 0)
        {
            return null;
        }
        States = kept;
        return this;
    }
}

/// <summary>What the status icons are called and where each shows (see the top of this file).</summary>
public static class StatusIcons
{
    /// <summary>Every status icon, in the order the entry shows them and its page lists them.</summary>
    public static IReadOnlyList<StatusIcon> All { get; } = Enum.GetValues<StatusIcon>();

    /// <summary>The word the file names <paramref name="icon"/> by: "microphone", "airplaneMode".</summary>
    public static string Key(StatusIcon icon)
    {
        var name = icon.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Its glyph on the page, the one the bar draws for it (the dock's Indicators.Glyph, where each
    /// comes from is at the top of QuickSettings\Services\Indicators.cs).
    /// </summary>
    public static string Glyph(StatusIcon icon) => icon switch
    {
        StatusIcon.Microphone => "\uE720",
        StatusIcon.Camera => "\uE722",
        StatusIcon.Location => "\uE37A",
        StatusIcon.Presence => "\uE425",
        StatusIcon.AirplaneMode => "\uE709",
        StatusIcon.Hotspot => "\uE704",
        StatusIcon.Metered => "\uE8EC",
        StatusIcon.EnergySaver => "\uF1E8",
        StatusIcon.DoNotDisturb => "\uF285",
        StatusIcon.WindowsUpdate => "\uE895",
        _ => throw new ArgumentOutOfRangeException(nameof(icon)),
    };

    /// <summary>Its name on the page: Windows' word for what it stands for.</summary>
    public static string Title(StatusIcon icon) => icon switch
    {
        StatusIcon.Microphone => S.SettingsStatusIconsMicrophone,
        StatusIcon.Camera => S.SettingsStatusIconsCamera,
        StatusIcon.Location => S.SettingsStatusIconsLocation,
        StatusIcon.Presence => S.SettingsStatusIconsPresence,
        StatusIcon.AirplaneMode => S.SettingsStatusIconsAirplaneMode,
        StatusIcon.Hotspot => S.SettingsStatusIconsHotspot,
        StatusIcon.Metered => S.SettingsStatusIconsMetered,
        StatusIcon.EnergySaver => S.SettingsStatusIconsEnergySaver,
        StatusIcon.DoNotDisturb => S.SettingsStatusIconsDoNotDisturb,
        StatusIcon.WindowsUpdate => S.SettingsStatusIconsWindowsUpdate,
        _ => throw new ArgumentOutOfRangeException(nameof(icon)),
    };

    /// <summary>Where <paramref name="icon"/> shows by <paramref name="options"/>: on the taskbar unless they say otherwise.</summary>
    public static TrayState State(StatusIconOptions? options, StatusIcon icon)
    {
        if (options?.States is not { } states)
        {
            return TrayState.Taskbar;
        }
        // Asked by a key in any case, as the file's own dictionary may have been read without the comparer.
        var key = Key(icon);
        foreach (var (named, state) in states)
        {
            if (string.Equals(named, key, StringComparison.OrdinalIgnoreCase))
            {
                return state is TrayState.Overflow or TrayState.Hidden ? state : TrayState.Taskbar;
            }
        }
        return TrayState.Taskbar;
    }

    /// <summary>
    /// <paramref name="options"/> with <paramref name="icon"/> at <paramref name="state"/>, as the
    /// entry keeps them (<see cref="StatusIconOptions.Normalized"/>): null once every icon is on the
    /// taskbar again. <paramref name="options"/> itself is left as it was.
    /// </summary>
    public static StatusIconOptions? With(StatusIconOptions? options, StatusIcon icon, TrayState state)
    {
        var next = new StatusIconOptions();
        var key = Key(icon);
        foreach (var (named, was) in options?.States ?? [])
        {
            if (!string.Equals(named, key, StringComparison.OrdinalIgnoreCase))
            {
                next.States[named] = was;
            }
        }
        next.States[key] = state;
        return next.Normalized();
    }
}
