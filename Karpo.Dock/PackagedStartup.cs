// "Start with Windows" for the Microsoft Store's copy: Windows' side of it (docs\widgets.md §710). The
// rules are Karpo.Core\PackagedStartup.cs', pure; this asks Windows about the package's startup task
// (Windows.ApplicationModel.StartupTask, the manifest's "KarpoStartup") and turns it as they say.
//
// Windows answers asynchronously, and the menu's switch is read on the dock's thread while it draws,
// so what Windows last said is kept (known) and read from there: until its first answer the switch
// shows what the setting says. Every question runs on the pool, guarded, and none waits for another:
// each reads the task's state again before it turns anything.
//
// A copy without a package never comes here (ForThisProcess, and each entry asks IsPackaged): its
// start with Windows is the Run entry and the logon task, as before. A packaged copy on a test config
// (KARPO_CONFIG_DIR) reads the task and never turns it: the person's startup apps are not a test's.

using Karpo.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;

namespace Karpo.Dock;

internal static class PackagedStartup
{
    private static readonly object gate = new();
    private static PackagedStartupState? known;
    private static bool asked;

    /// <summary>The package's startup task as the menu's switch: read from what Windows last said, turned off by disabling it.</summary>
    public static StartupSwitch Switch { get; } = new(Read, Unregister, Approve);

    /// <summary>Windows' side of "Start with Windows" for this copy: the package's task, or the Run entry of a copy without a package.</summary>
    public static StartupSwitch ForThisProcess => PackageIdentity.IsPackaged ? Switch : StartupSwitch.Windows;

    /// <summary>
    /// The command line as the dock reads it: with "--startup" where Windows' startup task started this
    /// packaged copy (StartupRegistration.PackagedArguments). A copy without a package has its own.
    /// </summary>
    public static string[] Arguments(string[] args) =>
        PackageIdentity.IsPackaged ? StartupRegistration.PackagedArguments(args, ByStartupTask()) : args;

    /// <summary>The dock, at its start and when the setting changes: the task follows <paramref name="enabled"/> (StartupRegistration.PackagedAct).</summary>
    public static void Sync(bool enabled)
    {
        if (!PackageIdentity.IsPackaged)
        {
            return;
        }
        _ = Task.Run(() => Troubles.Guard("startup: the package's task follows the setting", async () =>
        {
            var task = await StartupTask.GetAsync(StartupRegistration.PackagedTaskId);
            var state = (PackagedStartupState)(int)task.State;
            if (!Config.IsIsolated)
            {
                switch (StartupRegistration.PackagedAct(enabled, state))
                {
                    case PackagedStartupAct.Enable:
                        state = (PackagedStartupState)(int)await task.RequestEnableAsync();
                        Troubles.Write($"startup: the package's task asked to start Karpo with Windows: {state}"); // not-ui: karpo.log
                        break;
                    case PackagedStartupAct.Disable:
                        task.Disable();
                        state = (PackagedStartupState)(int)task.State;
                        Troubles.Write($"startup: the package's task no longer starts Karpo with Windows: {state}"); // not-ui: karpo.log
                        break;
                }
            }
            Keep(state);
        }));
    }

    /// <summary>What Windows last said of the task, as the switch reads a Run entry; asked for the first time here if nobody has.</summary>
    private static StartupState Read()
    {
        PackagedStartupState? state;
        bool ask;
        lock (gate)
        {
            state = known;
            ask = !asked;
            asked = true;
        }
        if (ask)
        {
            _ = Task.Run(() => Troubles.Guard("startup: the package's task read", async () =>
                Keep((PackagedStartupState)(int)(await StartupTask.GetAsync(StartupRegistration.PackagedTaskId)).State)));
        }
        return StartupRegistration.PackagedState(state, Environment.ProcessPath ?? "");
    }

    /// <summary>"Start with Windows" turned off in Karpo: the task disabled at once, as the Run entry is removed at once.</summary>
    private static bool Unregister()
    {
        Sync(false);
        return true;
    }

    /// <summary>
    /// "Start with Windows" turned on in Karpo while the person switched the task off in Windows: not
    /// Karpo's to undo, and Windows would refuse. The switch goes on saying where it was turned off.
    /// </summary>
    private static bool Approve() => false;

    private static void Keep(PackagedStartupState state)
    {
        lock (gate)
        {
            known = state;
            asked = true;
        }
    }

    /// <summary>Whether Windows started this process for the package's startup task (a sign-in), rather than the person or the guard.</summary>
    private static bool ByStartupTask()
    {
        try
        {
            return AppInstance.GetActivatedEventArgs() is { Kind: ActivationKind.StartupTask };
        }
        catch (Exception error)
        {
            // Not known is a person's start: the dock shows at once instead of waiting for Explorer's taskbar.
            Troubles.Note("startup: how this copy was started", error);
            return false;
        }
    }
}
