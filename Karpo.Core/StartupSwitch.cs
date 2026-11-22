// "Start with Windows" as a switch: whether it shows on, what it says about Windows' side, and what
// turning it does — the settings app's General page's card (SettingsModel.Startup.cs) and the edit
// bar's ⚙ › Start with Windows (KarpoMenu.Startup.cs, §337) alike.
//
// On is the setting on and Task Manager not having turned the entry off. Turning it on clears Task
// Manager's "disabled", since the person asked for it; turning it off removes the entry at once, so that
// holds even while the dock isn't running. The dock writes the entry itself when it sees the setting
// change (Dock.SyncStartup). Windows' side is a seam (Windows: the real keys, StartupRegistration.cs),
// so the tests turn the switch without touching the registry or the logon task.

namespace Karpo.Core;

/// <summary>
/// "Start with Windows", read and turned through Windows' side: <paramref name="Read"/> the Run entry
/// and Task Manager's switch for it, <paramref name="Unregister"/> the entry removed at once,
/// <paramref name="Approve"/> Task Manager's "disabled" cleared.
/// </summary>
public sealed record StartupSwitch(Func<StartupState> Read, Func<bool> Unregister, Func<bool> Approve)
{
    /// <summary>The real keys of the person signed in (StartupRegistration.Current, Unregister, ApproveHere).</summary>
    public static StartupSwitch Windows { get; } = new(StartupRegistration.Current, StartupRegistration.Unregister, StartupRegistration.ApproveHere);

    /// <summary>On when Karpo starts at sign-in: the setting is on and Task Manager hasn't turned the entry off.</summary>
    public static bool IsOn(Config config, StartupState state) => config.StartWithWindows && !state.TurnedOffInTaskManager;

    /// <summary>What happens at sign-in, and why not when it doesn't: the General page's card's line.</summary>
    public static string Said(Config config, StartupState state) => Why(config, state) ?? (config.StartWithWindows ? S.SettingsGeneralStartupOn : S.SettingsGeneralStartupOff);

    /// <summary>
    /// What Windows' side holds against the setting where it isn't plainly on or off: turned off in Task
    /// Manager, not set up yet, to be set up again; null where the switch says it all.
    /// </summary>
    public static string? Why(Config config, StartupState state) =>
        !config.StartWithWindows ? null
        : state.TurnedOffInTaskManager ? S.SettingsGeneralStartupTurnedOff
        : !state.Registered ? S.SettingsGeneralStartupNotYet
        : !state.ProgramExists ? S.SettingsGeneralStartupAgain
        : null;

    /// <summary>
    /// Turns it <paramref name="on"/> or off in <paramref name="config"/>: off removes the entry at once,
    /// on clears Task Manager's "disabled". Nothing where it already shows so. True where the setting
    /// changed, so it is saved; Windows' side alone (on again after Task Manager) changes no setting.
    /// </summary>
    public bool Turn(Config config, bool on)
    {
        var state = Read();
        if (on == IsOn(config, state))
        {
            return false;
        }
        if (!on)
        {
            Unregister();
        }
        else if (state.TurnedOffInTaskManager)
        {
            Approve();
        }
        var changed = config.StartWithWindows != on;
        config.StartWithWindows = on;
        return changed;
    }
}
