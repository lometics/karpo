// "Start with Windows": Karpo's entry in the per-user Run key, which Explorer
// starts at sign-in, and Task Manager's switch for it. A logon task starts the
// same entry sooner (StartupTask.cs).
//
// The dock owns the entry. At every start, and when the setting changes, it makes
// the value start this copy of Karpo.exe (so switching from a development build to
// the published one moves the entry along, and an entry left pointing at a copy
// that is gone is mended), or removes it when the setting is off. A copy that can't
// be the one Windows starts (not Karpo.exe, or run from the temporary folder where
// test builds live) doesn't register itself, and a Karpo on a test config
// (KARPO_CONFIG_DIR) never writes the person's registry.
//
// Task Manager's Startup apps switch (also in Settings > Apps > Startup) lives in
// Explorer\StartupApproved\Run: a binary value named like the Run value, whose
// first byte is even while the entry is enabled and odd once it was switched off
// (the time it was switched off follows). Karpo respects it: switched off there,
// the entry stays and Windows doesn't start it, and the settings app says so.
// Turning "Start with Windows" on in Karpo's settings is the person asking for it,
// so that clears Task Manager's "disabled" by deleting the value, which is what an
// entry nobody has switched looks like (enabled). Writing Task Manager's own bytes
// would work as well, but leans on more of a format Windows doesn't document.

using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Karpo.Core;

public sealed partial class Config
{
    /// <summary>
    /// Start Karpo when the person signs in: the dock keeps a Run entry for itself while this
    /// is on (<see cref="StartupRegistration"/>). On for new configs, and for files from before
    /// the setting existed, which don't have it.
    /// </summary>
    public bool StartWithWindows { get; set; } = true;
}

/// <summary>Task Manager's switch for a Run entry.</summary>
public enum StartupApproval
{
    /// <summary>Never switched: the entry runs.</summary>
    NotSet,
    Enabled,
    /// <summary>Switched off in Task Manager's Startup apps or in Settings: Windows doesn't start it.</summary>
    Disabled,
}

/// <summary>What <see cref="StartupRegistration.Sync"/> did.</summary>
public enum StartupChange
{
    None,
    Added,
    /// <summary>The entry started another program, or this one differently: it starts this one now.</summary>
    Repaired,
    Removed,
}

/// <summary>What the registry says about starting Karpo at sign-in.</summary>
/// <param name="Command">The Run value as written; null when there is none.</param>
/// <param name="Program">The program it starts; null when there is no value or it doesn't read.</param>
/// <param name="ProgramExists">Whether that program is there.</param>
/// <param name="Approval">Task Manager's switch for it.</param>
public sealed record StartupState(string? Command, string? Program, bool ProgramExists, StartupApproval Approval)
{
    public bool Registered => Command is not null;

    /// <summary>Switched off in Task Manager (or in Settings > Apps > Startup).</summary>
    public bool TurnedOffInTaskManager => Approval == StartupApproval.Disabled;
}

public static partial class StartupRegistration
{
    /// <summary>The Run value's name, which Task Manager also shows.</summary>
    public const string ValueName = "Karpo";

    /// <summary>What the dock is started with at sign-in.</summary>
    public const string Argument = "--startup";

    public const string ExeName = "Karpo.exe";

    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>Tests and harnesses name a key under HKCU\Software\Karpo.Tests here to stand in for HKCU.</summary>
    private const string TestRootVariable = "KARPO_STARTUP_ROOT";

    private const string TestRootPrefix = @"Software\Karpo.Tests\";

    /// <summary>The Run value for <paramref name="exe"/>: the path quoted, since it may have spaces, then <see cref="Argument"/>.</summary>
    public static string Command(string exe) => $"\"{exe}\" {Argument}";

    /// <summary>
    /// Splits a Run value into the program and its arguments the way Windows reads it: a quoted
    /// path up to its closing quote; unquoted, up to the first ".exe" that ends a word, else the
    /// first space. Environment variables are expanded (a REG_EXPAND_SZ value may use them).
    /// </summary>
    public static bool TryParse(string? command, out string program, out string arguments)
    {
        program = "";
        arguments = "";
        var text = Environment.ExpandEnvironmentVariables(command ?? "").Trim();
        if (text.Length == 0)
        {
            return false;
        }
        if (text[0] == '"')
        {
            var close = text.IndexOf('"', 1);
            if (close < 0)
            {
                return false;
            }
            program = text[1..close].Trim();
            arguments = text[(close + 1)..].Trim();
            return program.Length > 0;
        }
        var end = -1;
        for (var at = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase); at >= 0; at = text.IndexOf(".exe", at + 1, StringComparison.OrdinalIgnoreCase))
        {
            if (at + 4 == text.Length || char.IsWhiteSpace(text[at + 4]))
            {
                end = at + 4;
                break;
            }
        }
        if (end < 0)
        {
            end = text.IndexOf(' ');
            end = end < 0 ? text.Length : end;
        }
        program = text[..end];
        arguments = text[end..].Trim();
        return true;
    }

    /// <summary>Whether two paths name the same file: full paths, 8.3 short names made long, letter case aside.</summary>
    public static bool SameProgram(string a, string b) =>
        string.Equals(Canonical(a), Canonical(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="program"/> is a Karpo.exe, wherever it is.</summary>
    public static bool IsKarpo(string program) =>
        string.Equals(Path.GetFileName(program.Trim().Trim('"')), ExeName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Task Manager's switch from its StartupApproved value: the first byte is even while enabled
    /// (2, or 6 after some Windows versions switched it back on) and odd once switched off (3, 1, 7).
    /// Anything else, or no value, is <see cref="StartupApproval.NotSet"/>, which runs.
    /// </summary>
    public static StartupApproval ParseApproval(object? data) =>
        data is byte[] { Length: > 0 } bytes
            ? (bytes[0] & 1) == 0 ? StartupApproval.Enabled : StartupApproval.Disabled
            : StartupApproval.NotSet;

    /// <summary>Why <paramref name="exe"/> can't be what Windows starts at sign-in; null when it can.</summary>
    public static string? Unregistrable(string? exe)
    {
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "its path is unknown";
        }
        if (!IsKarpo(exe))
        {
            return $"it runs as {Path.GetFileName(exe)}, not {ExeName}";
        }
        var temporary = Canonical(Path.GetTempPath()).TrimEnd('\\') + "\\";
        if (Canonical(exe).StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
        {
            return "it runs from the temporary folder, where test builds live";
        }
        return null;
    }

    /// <summary>The Run value and Task Manager's switch for it, under <paramref name="root"/> (HKCU, see <see cref="Root"/>).</summary>
    public static StartupState Read(RegistryKey root)
    {
        string? command = null;
        object? approval = null;
        try
        {
            using (var run = root.OpenSubKey(RunKey))
            {
                command = run?.GetValue(ValueName) as string;
            }
            using (var approved = root.OpenSubKey(ApprovedKey))
            {
                approval = approved?.GetValue(ValueName);
            }
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: startup entry not read: {error.Message}");
        }
        var program = TryParse(command, out var parsed, out _) ? parsed : null;
        return new StartupState(command, program, program is not null && File.Exists(program), ParseApproval(approval));
    }

    /// <summary>
    /// With <paramref name="enabled"/>, makes the Run value start <paramref name="exe"/> with
    /// <see cref="Argument"/>, whatever it started before; without, removes a Run value that starts a
    /// Karpo.exe (anything else by that name isn't Karpo's to remove). Task Manager's switch is left
    /// alone. <paramref name="previous"/> is the value before.
    /// </summary>
    public static StartupChange Sync(bool enabled, string exe, RegistryKey root, out string? previous)
    {
        if (enabled)
        {
            using var run = root.CreateSubKey(RunKey, writable: true);
            previous = run.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
            var command = Command(exe);
            if (previous == command)
            {
                return StartupChange.None;
            }
            run.SetValue(ValueName, command, RegistryValueKind.String);
            return previous is null ? StartupChange.Added : StartupChange.Repaired;
        }
        using var existing = root.OpenSubKey(RunKey, writable: true);
        previous = existing?.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        if (existing is null || previous is null || !TryParse(previous, out var program, out _) || !IsKarpo(program))
        {
            return StartupChange.None;
        }
        existing.DeleteValue(ValueName, throwOnMissingValue: false);
        return StartupChange.Removed;
    }

    /// <summary>
    /// Clears Task Manager's "disabled" for Karpo's entry, by removing its StartupApproved value (an
    /// entry nobody switched runs). True when it was switched off there.
    /// </summary>
    public static bool Approve(RegistryKey root)
    {
        using var approved = root.OpenSubKey(ApprovedKey, writable: true);
        if (approved is null || ParseApproval(approved.GetValue(ValueName)) != StartupApproval.Disabled)
        {
            return false;
        }
        approved.DeleteValue(ValueName, throwOnMissingValue: false);
        return true;
    }

    /// <summary>
    /// Where the Run and StartupApproved keys are looked for: HKEY_CURRENT_USER, or, for tests and
    /// harnesses, the key under HKCU\Software\Karpo.Tests that KARPO_STARTUP_ROOT names. Null when
    /// <paramref name="writing"/> while Karpo runs on a test config (KARPO_CONFIG_DIR) without such
    /// a key: nothing may change the person's own startup apps then. Disposing it is safe either way.
    /// </summary>
    public static RegistryKey? Root(bool writing)
    {
        if (Environment.GetEnvironmentVariable(TestRootVariable) is { Length: > 0 } test)
        {
            var name = test.Trim().Trim('\\');
            if (name.StartsWith(TestRootPrefix, StringComparison.OrdinalIgnoreCase) && name.Length > TestRootPrefix.Length)
            {
                return Registry.CurrentUser.CreateSubKey(name, writable: true);
            }
        }
        if (writing && Environment.GetEnvironmentVariable("KARPO_CONFIG_DIR") is { Length: > 0 })
        {
            return null;
        }
        return Registry.CurrentUser;
    }

    /// <summary>The state now, for the settings app's card.</summary>
    public static StartupState Current()
    {
        using var root = Root(writing: false)!;
        return Read(root);
    }

    /// <summary>
    /// The dock, at start and when the setting changes: the Run entry follows <paramref name="enabled"/>
    /// for this copy of Karpo. Returns a line for the log when it changed something or couldn't; null
    /// when all was as it should be.
    /// </summary>
    public static string? SyncThisCopy(bool enabled)
    {
        try
        {
            using var root = Root(writing: true);
            if (root is null)
            {
                return null;
            }
            var exe = Environment.ProcessPath is { } path ? Canonical(path) : null;
            if (enabled && Unregistrable(exe) is { } reason)
            {
                return $"not starting with Windows from here: {reason}";
            }
            return Sync(enabled, exe ?? "", root, out var previous) switch
            {
                StartupChange.Added => $"starts with Windows now ({exe})",
                StartupChange.Repaired => $"the startup entry started {previous}; it starts {exe} now",
                StartupChange.Removed => "doesn't start with Windows any more",
                _ => null,
            };
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            return $"startup entry not updated: {error.Message}";
        }
    }

    /// <summary>
    /// For the settings app when the person turns "Start with Windows" off: the entry goes at once,
    /// even with the dock not running (it would otherwise start once more at the next sign-in).
    /// </summary>
    public static bool Unregister()
    {
        try
        {
            using var root = Root(writing: true);
            if (root is null)
            {
                return false;
            }
            // And the logon task that starts it sooner (StartupTask.cs), off this thread.
            SyncTaskSoon(false);
            return Sync(false, "", root, out _) == StartupChange.Removed;
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: startup entry not removed: {error.Message}");
            return false;
        }
    }

    /// <summary>For the settings app when the person turns "Start with Windows" on: <see cref="Approve"/> on the real keys.</summary>
    public static bool ApproveHere()
    {
        try
        {
            using var root = Root(writing: true);
            return root is not null && Approve(root);
        }
        catch (Exception error) when (error is System.Security.SecurityException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Karpo: Task Manager's startup switch not changed: {error.Message}");
            return false;
        }
    }

    /// <summary>A full path with 8.3 short names made long (as far as the file exists), for comparing and for the Run value.</summary>
    public static string Canonical(string path)
    {
        var text = path.Trim().Trim('"');
        try
        {
            text = Path.GetFullPath(text);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return text;
        }
        return LongPath(text);
    }

    private static unsafe string LongPath(string path)
    {
        var buffer = new char[Math.Max(path.Length + 1, 260)];
        while (true)
        {
            uint length;
            fixed (char* chars = buffer)
            {
                length = GetLongPathNameW(path, chars, (uint)buffer.Length);
            }
            if (length == 0)
            {
                // Not there (yet), or no access: as given.
                return path;
            }
            if (length < buffer.Length)
            {
                return new string(buffer, 0, (int)length);
            }
            buffer = new char[length + 1];
        }
    }

    [LibraryImport("kernel32.dll", EntryPoint = "GetLongPathNameW", StringMarshalling = StringMarshalling.Utf16)]
    private static unsafe partial uint GetLongPathNameW(string shortPath, char* longPath, uint length);
}
