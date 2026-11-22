// The app buttons' own setting, as Windows 11 keeps it under Taskbar behaviors: whether a bar
// combines an app's windows into one button and hides their labels (the rules it makes are in
// Karpo.Dock\AppButtons.cs).
//
// One setting for every bar. Windows' "…on other taskbars" is not one (the person, 2026-10-05:
// "should not be a setting. it should follow the default one for the main taskbar"): every bar,
// the main display's and the others, combines as "Combine taskbar buttons and hide labels" says.
//
// Nor is Windows' "Show my taskbar apps on" (the person, 2026-10-05: "should not be a
// setting. if the apps widget is on a layout it should always behave the same way"): the open apps
// widget shows every open window on every bar it stands on, which is Windows' "All taskbars".
//
// A new config takes Windows' own answer for its main taskbar (Config.Defaults reads it, as a new
// bar reads which of Windows' buttons to show), so the bar starts the way the person's taskbar is.

using System.Text.Json.Serialization;

namespace Karpo.Core;

/// <summary>
/// "Combine taskbar buttons and hide labels": an app's windows as one button with its icon alone,
/// only once the bar has no room for them each, or never. A word this build does not know reads as
/// a value outside the enum, and <see cref="Config.Repair"/> makes it <see cref="Always"/>.
/// </summary>
[JsonConverter(typeof(TolerantEnumConverter<ButtonCombine>))]
public enum ButtonCombine
{
    /// <summary>One button per app, its icon alone: what Windows 11 does unless somebody changes it.</summary>
    [JsonStringEnumMemberName("always")] Always,

    /// <summary>A button per window, with its title, until the bar is full; then as <see cref="Always"/>.</summary>
    [JsonStringEnumMemberName("whenFull")] WhenFull,

    /// <summary>A button per window, with its title, always; what the bar has no room for goes behind "…".</summary>
    [JsonStringEnumMemberName("never")] Never,
}

/// <summary>What Windows' own taskbar says of the app buttons' setting.</summary>
public static class TaskbarApps
{
    /// <summary>Windows' <c>TaskbarGlomLevel</c>: 0 always, 1 when full, 2 never; none is always.</summary>
    public static ButtonCombine CombineOf(int? level) => level switch
    {
        1 => ButtonCombine.WhenFull,
        2 => ButtonCombine.Never,
        _ => ButtonCombine.Always,
    };

    /// <summary>
    /// A new config's answer (its first layout's, §345), read from Windows' own main taskbar (Explorer\Advanced). Never under the
    /// tests: an isolated config keeps Windows' defaults, so what a new config holds does not
    /// depend on the PC it is made on.
    /// </summary>
    internal static void FromWindows(Config config)
    {
        if (Config.Isolated is not null)
        {
            return;
        }
        config.DefaultLayout.Combine = CombineOf(Sections.ReadAdvanced("TaskbarGlomLevel"));
    }
}
