// "Start with Windows", sooner: a Task Scheduler task that starts Karpo the moment the person signs
// in, beside the Run entry (StartupRegistration.cs).
//
// Explorer starts the Run entries late and one after another: on this PC it began them 25 to 35 s
// after sign-in, and Windows Security's tray icon, first in line, held them up another 0 to 19 s, so
// Karpo's bar came 26 to 52 s after sign-in while Karpo itself needs about a second. A logon task
// isn't throttled that way: Task Scheduler starts it as the session begins, and the dock then waits
// for Explorer's taskbar (Dock.Open) as a Run start always did.
//
// The Run entry stays what Windows and the person see and switch: Task Manager's Startup apps, the
// Settings app's Startup page and the "Start with Windows" card all read it. The task is only how it
// starts sooner, so a copy the task started (TaskArgument) goes on only while that entry would start
// Karpo too (TaskMayStart): there, not switched off in Task Manager. Switched off there, the task's
// copy ends at once and nothing starts. The Run entry's own start, later, finds the task's copy
// running and ends on the instance mutex, as any second Karpo does.
//
// The task is the person's own: a logon trigger for their account alone, run as them with the least
// privileges, so it is registered without elevation (schtasks.exe, measured on 2026-10-19 from a
// medium-integrity shell). Normal priority (Task Scheduler's default, 7, starts a program below
// normal with low I/O priority), no time limit (the default ends a task after 72 hours), and no
// battery conditions. The dock keeps it as it keeps the Run entry: made, or remade for this copy, at
// every start and when the setting changes, off the dock's thread (schtasks takes 50 to 150 ms); gone
// when the setting is off. A copy on a test config, or with a test root, never touches Task Scheduler.

using System.Diagnostics;
using System.Security.Principal;
using System.Text;
using System.Xml.Linq;

namespace Karpo.Core;

/// <summary>What <see cref="StartupRegistration.SyncTask"/> did.</summary>
public enum LogonTaskChange
{
    None,
    Added,
    /// <summary>The task started another copy, differently, or was made by an older Karpo: it is made again.</summary>
    Repaired,
    Removed,
    /// <summary>Task Scheduler refused.</summary>
    Failed,
}

/// <summary>The person's tasks in Task Scheduler, by name: <see cref="Schtasks"/>, or a stand-in in tests.</summary>
public interface ILogonTasks
{
    /// <summary>The task's definition as Task Scheduler gives it back; null when there is none.</summary>
    string? Read(string name);

    /// <summary>Makes the task, or replaces one by that name. False when Task Scheduler refused.</summary>
    bool Write(string name, string xml);

    /// <summary>Removes the task. False when Task Scheduler refused.</summary>
    bool Delete(string name);
}

public static partial class StartupRegistration
{
    /// <summary>Added to <see cref="Argument"/> when the logon task starts the dock (<see cref="TaskMayStart"/>).</summary>
    public const string TaskArgument = "--logon-task";

    /// <summary>The definition's version: a task an older Karpo made, with another, is made again.</summary>
    public const string TaskVersion = "1";

    /// <summary>
    /// What the task's RegistrationInfo version says: <see cref="TaskVersion"/> and a fingerprint of
    /// the program's path. schtasks hands the definition back in the console's code page, which loses
    /// letters outside it, so a path is compared by this ASCII stamp rather than as read.
    /// </summary>
    public static string TaskStamp(string exe)
    {
        var hash = 2166136261u;
        foreach (var letter in Canonical(exe).ToUpperInvariant())
        {
            hash = (hash ^ letter) * 16777619u;
        }
        return $"{TaskVersion} {hash:X8}";
    }

    /// <summary>What the task starts the dock with.</summary>
    public const string TaskArguments = Argument + " " + TaskArgument;

    private const string TaskNamespace = "http://schemas.microsoft.com/windows/2004/02/mit/task";

    /// <summary>
    /// The task's name: Task Scheduler's root folder is the machine's, so each person's has their
    /// account's SID in it, as OneDrive's and Edge's per-person tasks do.
    /// </summary>
    public static string TaskName(string sid) => $"Karpo sign-in {sid}"; // not-ui

    /// <summary>The task that starts <paramref name="exe"/> at <paramref name="sid"/>'s sign-in, as schtasks /create /xml reads it.</summary>
    public static string TaskXml(string exe, string sid)
    {
        XNamespace ns = TaskNamespace;
        var task = new XElement(ns + "Task", new XAttribute("version", "1.2"),
            new XElement(ns + "RegistrationInfo",
                new XElement(ns + "Author", "Karpo"),
                new XElement(ns + "Version", TaskStamp(exe)),
                new XElement(ns + "URI", "\\" + TaskName(sid))),
            new XElement(ns + "Triggers",
                new XElement(ns + "LogonTrigger",
                    new XElement(ns + "Enabled", "true"),
                    new XElement(ns + "UserId", sid))),
            new XElement(ns + "Principals",
                new XElement(ns + "Principal", new XAttribute("id", "Author"),
                    new XElement(ns + "UserId", sid),
                    new XElement(ns + "LogonType", "InteractiveToken"),
                    new XElement(ns + "RunLevel", "LeastPrivilege"))),
            new XElement(ns + "Settings",
                new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                new XElement(ns + "DisallowStartIfOnBatteries", "false"),
                new XElement(ns + "StopIfGoingOnBatteries", "false"),
                new XElement(ns + "AllowHardTerminate", "true"),
                new XElement(ns + "StartWhenAvailable", "false"),
                new XElement(ns + "RunOnlyIfNetworkAvailable", "false"),
                new XElement(ns + "IdleSettings",
                    new XElement(ns + "StopOnIdleEnd", "false"),
                    new XElement(ns + "RestartOnIdle", "false")),
                new XElement(ns + "AllowStartOnDemand", "true"),
                new XElement(ns + "Enabled", "true"),
                new XElement(ns + "Hidden", "false"),
                new XElement(ns + "RunOnlyIfIdle", "false"),
                new XElement(ns + "ExecutionTimeLimit", "PT0S"),
                new XElement(ns + "Priority", "4")),
            new XElement(ns + "Actions", new XAttribute("Context", "Author"),
                new XElement(ns + "Exec",
                    new XElement(ns + "Command", exe),
                    new XElement(ns + "Arguments", TaskArguments),
                    new XElement(ns + "WorkingDirectory", Path.GetDirectoryName(exe) ?? ""))));
        return "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" + task;
    }

    /// <summary>
    /// The program, arguments and version of a task definition as Task Scheduler gives it back;
    /// false when it doesn't read as one that starts a program.
    /// </summary>
    public static bool TryReadTask(string? xml, out string program, out string arguments, out string version)
    {
        program = arguments = version = "";
        if (string.IsNullOrWhiteSpace(xml))
        {
            return false;
        }
        XDocument document;
        try
        {
            document = XDocument.Parse(xml.Trim());
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
        XNamespace ns = TaskNamespace;
        var exec = document.Root?.Element(ns + "Actions")?.Element(ns + "Exec");
        var command = exec?.Element(ns + "Command")?.Value.Trim().Trim('"');
        if (string.IsNullOrEmpty(command))
        {
            return false;
        }
        program = Environment.ExpandEnvironmentVariables(command);
        arguments = exec!.Element(ns + "Arguments")?.Value.Trim() ?? "";
        version = document.Root!.Element(ns + "RegistrationInfo")?.Element(ns + "Version")?.Value.Trim() ?? "";
        return true;
    }

    /// <summary>
    /// With <paramref name="enabled"/>, makes the logon task start <paramref name="exe"/> with
    /// <see cref="TaskArguments"/>, unless it does already; without, removes it. The task's name is
    /// Karpo's alone, so whatever is there by that name is Karpo's to replace or remove.
    /// </summary>
    public static LogonTaskChange SyncTask(bool enabled, string exe, string sid, ILogonTasks tasks)
    {
        var name = TaskName(sid);
        var existing = tasks.Read(name);
        if (!enabled)
        {
            return existing is null ? LogonTaskChange.None
                : tasks.Delete(name) ? LogonTaskChange.Removed
                : LogonTaskChange.Failed;
        }
        if (TryReadTask(existing, out _, out var arguments, out var version)
            && arguments == TaskArguments
            && version == TaskStamp(exe))
        {
            return LogonTaskChange.None;
        }
        return !tasks.Write(name, TaskXml(exe, sid)) ? LogonTaskChange.Failed
            : existing is null ? LogonTaskChange.Added
            : LogonTaskChange.Repaired;
    }

    /// <summary>
    /// Whether a dock the logon task started goes on: only while the Run entry would start Karpo as
    /// well, which is what Task Manager, Settings and Karpo's own card show and switch.
    /// </summary>
    public static bool TaskMayStart(StartupState state) =>
        state.Program is { } program && IsKarpo(program) && !state.TurnedOffInTaskManager;

    /// <summary>
    /// For the dock's first line: started by the logon task (<see cref="TaskArgument"/>) while the
    /// Run entry is gone or switched off in Task Manager, it ends before it does anything else.
    /// </summary>
    public static bool StartedByTaskUnwanted(IReadOnlyCollection<string> args) =>
        args.Contains(TaskArgument) && !TaskMayStart(Current());

    /// <summary>Whether Task Scheduler is Karpo's to change from this process: not on a test config or with a test root.</summary>
    private static bool TasksAreOurs =>
        Environment.GetEnvironmentVariable("KARPO_CONFIG_DIR") is not { Length: > 0 }
        && Environment.GetEnvironmentVariable(TestRootVariable) is not { Length: > 0 };

    private static readonly object taskGate = new();
    private static Task taskQueue = Task.CompletedTask;

    /// <summary>
    /// The dock, at start and when the setting changes, and the settings app when it turns it off:
    /// the logon task follows <paramref name="enabled"/> for this copy, off the caller's thread and in
    /// the order asked. <paramref name="log"/> hears a line when it changed something or couldn't.
    /// </summary>
    public static void SyncTaskSoon(bool enabled, Action<string>? log = null)
    {
        if (!TasksAreOurs)
        {
            return;
        }
        lock (taskGate)
        {
            taskQueue = taskQueue.ContinueWith(_ =>
            {
                try
                {
                    if (SyncTaskOfThisCopy(enabled) is { } line)
                    {
                        log?.Invoke(line);
                    }
                }
                catch (Exception)
                {
                    // The log is the caller's; nothing here may end the process.
                }
            }, TaskScheduler.Default);
        }
    }

    private static string? SyncTaskOfThisCopy(bool enabled)
    {
        try
        {
            var exe = Environment.ProcessPath is { } path ? Canonical(path) : null;
            if (enabled && Unregistrable(exe) is not null)
            {
                return null;
            }
            var sid = WindowsIdentity.GetCurrent().User?.Value;
            if (sid is null)
            {
                return null;
            }
            return SyncTask(enabled, exe ?? "", sid, new Schtasks()) switch
            {
                LogonTaskChange.Added => "starts at sign-in through Task Scheduler too, before Windows' startup apps",
                LogonTaskChange.Repaired => $"the sign-in task was made again for {exe}",
                LogonTaskChange.Removed => "the sign-in task is gone",
                LogonTaskChange.Failed => "the sign-in task was refused by Task Scheduler: Karpo starts with Windows' startup apps",
                _ => null,
            };
        }
        catch (Exception error)
        {
            return $"sign-in task not updated: {error.Message}";
        }
    }
}

/// <summary>Task Scheduler through schtasks.exe, which a native AOT build can call without COM.</summary>
public sealed class Schtasks : ILogonTasks
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(15);

    public string? Read(string name) =>
        Run(out var output, "/query", "/tn", name, "/xml", "ONE") ? output : null;

    public bool Write(string name, string xml)
    {
        var file = Path.Combine(Path.GetTempPath(), $"karpo-task-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(file, xml, Encoding.Unicode);
            return Run(out _, "/create", "/tn", name, "/xml", file, "/f");
        }
        finally
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
            }
        }
    }

    public bool Delete(string name) => Run(out _, "/delete", "/tn", name, "/f");

    private static bool Run(out string output, params string[] arguments)
    {
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "schtasks.exe"))
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start);
        if (process is null)
        {
            output = "";
            return false;
        }
        var reading = process.StandardOutput.ReadToEndAsync();
        _ = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(Patience))
        {
            try
            {
                process.Kill();
            }
            catch (InvalidOperationException)
            {
            }
            output = "";
            return false;
        }
        output = reading.Result;
        return process.ExitCode == 0;
    }
}
