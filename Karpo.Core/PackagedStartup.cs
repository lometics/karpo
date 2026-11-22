// "Start with Windows" for the Microsoft Store's copy (docs\widgets.md §710). A packaged app may keep
// neither of the two things StartupRegistration.cs and StartupTask.cs keep: the Run entry it writes
// stays in the package's own view of the registry unless that is turned off, and names an exe whose
// folder changes with every update; the logon task is real, outlives an uninstall (Store policy
// 10.2.7) and names the same folder. What a package has instead is a startup task of its manifest
// (package\AppxManifest.xml, windows.startupTask, TaskId "KarpoStartup"): Windows starts the app at
// sign-in while the task is enabled, lists it in Task Manager's Startup apps and Settings › Apps ›
// Startup like any other, and removes it with the package.
//
// The rules are here, pure, so the tests hold them; Windows' side is asked by Karpo.Dock\
// PackagedStartup.cs (Windows.ApplicationModel.StartupTask), which Karpo.Core has no projection for.
//
// - The setting stays Config.StartWithWindows, and the switch the menu shows stays StartupSwitch: the
//   task's state is read as the Run entry's is (PackagedState), so "turned off in Task Manager" says
//   the same thing it always did.
// - The dock follows the setting (PackagedAct): on, a task Karpo itself left off is enabled; off, an
//   enabled one is disabled. A task the PERSON switched off in Windows is never switched on again by
//   Karpo, and can't be: Windows refuses, and the person turns it on where they turned it off.
// - A start by the task comes with no "--startup" on its command line (a manifest's task has no
//   arguments); the dock asks Windows how it was started and adds it (PackagedStartup.Arguments).

namespace Karpo.Core;

/// <summary>
/// A packaged copy's startup task, as Windows says it is: Windows.ApplicationModel.StartupTaskState's
/// values, by number, so the dock casts one to the other.
/// </summary>
public enum PackagedStartupState
{
    /// <summary>Off, and not by the person: never enabled, or disabled by Karpo itself. Karpo may enable it.</summary>
    Disabled = 0,

    /// <summary>Switched off by the person in Task Manager or Settings. Only they can switch it on again.</summary>
    DisabledByUser = 1,

    Enabled = 2,

    /// <summary>Switched off by the PC's administrator.</summary>
    DisabledByPolicy = 3,

    /// <summary>Switched on by the PC's administrator.</summary>
    EnabledByPolicy = 4,
}

/// <summary>What the dock does to the task so it follows the setting.</summary>
public enum PackagedStartupAct
{
    None,
    Enable,
    Disable,
}

public static partial class StartupRegistration
{
    /// <summary>The startup task's id in package\AppxManifest.xml (desktop:StartupTask TaskId).</summary>
    public const string PackagedTaskId = "KarpoStartup";

    /// <summary>What stands for the Run value in a packaged copy's <see cref="StartupState"/>: there is no command, the task is the package's.</summary>
    public const string PackagedCommand = "package:" + PackagedTaskId; // not-ui: a marker, never shown

    /// <summary>
    /// The task's state as the switch reads a Run entry's (<see cref="StartupSwitch"/>): enabled is an
    /// entry that runs; switched off by the person or the administrator is one "turned off in Task
    /// Manager"; off by Karpo's own doing is no entry yet, which the dock makes when the setting is on.
    /// Not known (<paramref name="state"/> null: Windows has not answered) holds nothing against the
    /// setting, so the switch shows what the setting says.
    /// </summary>
    public static StartupState PackagedState(PackagedStartupState? state, string exe) => state switch
    {
        PackagedStartupState.Disabled => new StartupState(null, null, false, StartupApproval.NotSet),
        PackagedStartupState.DisabledByUser or PackagedStartupState.DisabledByPolicy => new StartupState(PackagedCommand, exe, true, StartupApproval.Disabled),
        PackagedStartupState.Enabled or PackagedStartupState.EnabledByPolicy => new StartupState(PackagedCommand, exe, true, StartupApproval.Enabled),
        _ => new StartupState(PackagedCommand, exe, true, StartupApproval.NotSet),
    };

    /// <summary>
    /// What makes the task follow the setting: nothing where it already does, and nothing against the
    /// person's or the administrator's own switch, which is not Karpo's to turn.
    /// </summary>
    public static PackagedStartupAct PackagedAct(bool enabled, PackagedStartupState state) => state switch
    {
        PackagedStartupState.Disabled when enabled => PackagedStartupAct.Enable,
        PackagedStartupState.Enabled when !enabled => PackagedStartupAct.Disable,
        _ => PackagedStartupAct.None,
    };

    /// <summary>
    /// A packaged copy's command line with <see cref="Argument"/> added where Windows' startup task
    /// started it (<paramref name="byStartupTask"/>) and it isn't there: what a Run entry's start says
    /// for itself, so the dock waits for Explorer's taskbar and a second start asks nothing.
    /// </summary>
    public static string[] PackagedArguments(string[] args, bool byStartupTask) =>
        byStartupTask && !args.Contains(Argument) ? [.. args, Argument] : args;
}
