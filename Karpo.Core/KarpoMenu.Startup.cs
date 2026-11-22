// §335, §337: "Start with Windows", the menu's first row: a toggle writing "startWithWindows", read and
// turned as the General page's card does (StartupSwitch: off removes the Run entry at once, on clears
// Task Manager's "disabled"; the dock writes the entry as it applies the setting), with Windows' own
// state as its Why line where it is not plainly on or off ("Turned off in Task Manager's Startup apps",
// "Karpo sets this up the next time it starts"). Windows' side comes in KarpoServices.Startup, the real
// keys unless a test says otherwise.
//
// §465: no "Hide the Windows taskbar" row after it any more: Karpo hides Windows' taskbar whenever it
// runs, and nothing turns that off (Taskbar.cs).

namespace Karpo.Core;

/// <summary>§337: Windows' side of "Start with Windows" (<see cref="StartupSwitch.Windows"/> unless given another).</summary>
public sealed partial record KarpoServices
{
    /// <summary>How "Start with Windows" reads and turns Windows' side: the real keys, or a test's.</summary>
    public StartupSwitch Startup { get; init; } = StartupSwitch.Windows;
}

public static partial class KarpoMenu
{
    /// <summary>"Start with Windows"'s row.</summary>
    public const string StartupRow = "StartWithWindows";

    /// <summary>
    /// "Start with Windows": a toggle, on as Windows starts Karpo, why not under it where Windows' side
    /// says.
    /// </summary>
    public static IReadOnlyList<OptionRow> Startup(Config config, KarpoServices services)
    {
        var state = services.Startup.Read();
        return
        [
            new OptionRow(StartupRow, OptionRowKind.Toggle, S.SettingsGeneralStartupHeader)
            {
                Glyph = OptionGlyphs.Windows,
                Slot = OptionSlot.Behaviour,
                On = StartupSwitch.IsOn(config, state),
                Why = StartupSwitch.Why(config, state),
                Set = on => services.Startup.Turn(config, on != 0),
            },
        ];
    }
}
