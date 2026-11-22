// "Start with Windows": the Run value's text, reading it back, Task Manager's
// switch, and keeping the value right. The registry tests work under a key of
// their own (HKCU\Software\Karpo.Tests\<guid>), deleted afterwards; the person's
// own Run and StartupApproved keys are never written.

using Microsoft.Win32;

namespace Karpo.Tests.Startup;

public sealed class StartupRegistrationTests : IDisposable
{
    private readonly string name = $@"Software\Karpo.Tests\{Guid.NewGuid():N}";
    private readonly RegistryKey root;

    public StartupRegistrationTests() => root = Registry.CurrentUser.CreateSubKey(name, writable: true);

    public void Dispose()
    {
        root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(name, throwOnMissingSubKey: false);
    }

    private string? RunValue()
    {
        using var run = root.OpenSubKey(StartupRegistration.RunKey);
        return run?.GetValue(StartupRegistration.ValueName) as string;
    }

    private void SetApproval(params byte[] data)
    {
        using var approved = root.CreateSubKey(StartupRegistration.ApprovedKey, writable: true);
        approved.SetValue(StartupRegistration.ValueName, data, RegistryValueKind.Binary);
    }

    // --- The value's text -------------------------------------------------------------

    [Fact]
    public void TheCommandQuotesThePathAndAsksForStartup()
    {
        Assert.Equal("\"C:\\Program Files\\Karpo\\Karpo.exe\" --startup", StartupRegistration.Command(@"C:\Program Files\Karpo\Karpo.exe"));
        Assert.Equal("\"D:\\Karpo.exe\" --startup", StartupRegistration.Command(@"D:\Karpo.exe"));
    }

    [Theory]
    [InlineData("\"C:\\Program Files\\Karpo\\Karpo.exe\" --startup", @"C:\Program Files\Karpo\Karpo.exe", "--startup")]
    [InlineData("\"C:\\Program Files\\Karpo\\Karpo.exe\"", @"C:\Program Files\Karpo\Karpo.exe", "")]
    [InlineData("  \"C:\\Tools\\Karpo.exe\"   --startup  ", @"C:\Tools\Karpo.exe", "--startup")]
    [InlineData(@"C:\Tools\Karpo.exe --startup", @"C:\Tools\Karpo.exe", "--startup")]
    [InlineData(@"C:\Program Files\Karpo\Karpo.exe --startup", @"C:\Program Files\Karpo\Karpo.exe", "--startup")]
    [InlineData(@"C:\Tools\app.exe.old\Karpo.exe", @"C:\Tools\app.exe.old\Karpo.exe", "")]
    [InlineData(@"C:\Tools\run", @"C:\Tools\run", "")]
    public void ACommandReadsBackAsWindowsReadsIt(string command, string program, string arguments)
    {
        Assert.True(StartupRegistration.TryParse(command, out var parsed, out var rest));
        Assert.Equal(program, parsed);
        Assert.Equal(arguments, rest);
    }

    [Fact]
    public void EnvironmentVariablesAreExpanded()
    {
        Assert.True(StartupRegistration.TryParse("\"%WINDIR%\\notepad.exe\"", out var program, out _));
        Assert.Equal(Path.Combine(Environment.GetEnvironmentVariable("WINDIR")!, "notepad.exe"), program);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"C:\\Karpo.exe")]
    [InlineData("\"\" --startup")]
    public void WhatIsNoCommandSaysSo(string? command) => Assert.False(StartupRegistration.TryParse(command, out _, out _));

    [Fact]
    public void TheSameProgramWhateverTheSpelling()
    {
        Assert.True(StartupRegistration.SameProgram(@"C:\Program Files\Karpo\Karpo.exe", @"c:\program files\karpo\KARPO.EXE"));
        Assert.True(StartupRegistration.SameProgram(@"C:\Program Files\Karpo\..\Karpo\Karpo.exe", "\"C:\\Program Files\\Karpo\\Karpo.exe\""));
        Assert.False(StartupRegistration.SameProgram(@"C:\Karpo\Karpo.exe", @"D:\Karpo\Karpo.exe"));
    }

    [Fact]
    public void ShortNamesAreMadeLong()
    {
        // The temporary folder is often given with an 8.3 name (NATHAN~1): the same folder either way.
        var temporary = Path.GetTempPath();
        Assert.True(StartupRegistration.SameProgram(temporary, StartupRegistration.Canonical(temporary)));
        Assert.DoesNotContain("~", StartupRegistration.Canonical(Isolation.Folder));
    }

    // --- Which copies may register ------------------------------------------------------

    [Fact]
    public void OnlyAKarpoOutsideTheTemporaryFolderRegistersItself()
    {
        Assert.Null(StartupRegistration.Unregistrable(@"C:\Program Files\Karpo\Karpo.exe"));
        Assert.Null(StartupRegistration.Unregistrable(@"C:\Users\someone\Repositories\karpo-app\artifacts\publish\Karpo\Karpo.exe"));
        Assert.NotNull(StartupRegistration.Unregistrable(Path.Combine(Path.GetTempPath(), "kb", "config", "Karpo.exe")));
        Assert.NotNull(StartupRegistration.Unregistrable(@"C:\Program Files\dotnet\dotnet.exe"));
        Assert.NotNull(StartupRegistration.Unregistrable(null));
        Assert.NotNull(StartupRegistration.Unregistrable(""));
    }

    /// <summary>A Karpo on a test config (KARPO_CONFIG_DIR, as every test run has) never writes the person's startup apps.</summary>
    [Fact]
    public void ATestConfigNeverWritesThePersonsRegistry()
    {
        Assert.NotNull(Environment.GetEnvironmentVariable("KARPO_CONFIG_DIR"));
        Assert.Null(StartupRegistration.Root(writing: true));
        Assert.Null(StartupRegistration.SyncThisCopy(true));
        Assert.False(StartupRegistration.Unregister());
        Assert.False(StartupRegistration.ApproveHere());
    }

    [Fact]
    public void ATestRootMustBeUnderKarposTestKey()
    {
        var before = Environment.GetEnvironmentVariable("KARPO_STARTUP_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("KARPO_STARTUP_ROOT", @"Software\Microsoft\Windows\CurrentVersion");
            Assert.Null(StartupRegistration.Root(writing: true));
            Environment.SetEnvironmentVariable("KARPO_STARTUP_ROOT", name);
            using var test = StartupRegistration.Root(writing: true);
            Assert.NotNull(test);
            Assert.EndsWith(name, test.Name, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("KARPO_STARTUP_ROOT", before);
        }
    }

    // --- Task Manager's switch ----------------------------------------------------------------

    [Theory]
    [InlineData(new byte[] { 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, StartupApproval.Enabled)]
    [InlineData(new byte[] { 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, StartupApproval.Enabled)]
    [InlineData(new byte[] { 3, 0, 0, 0, 0x10, 0x3E, 0x9C, 0x5A, 0x2B, 0x2A, 0xDC, 0x01 }, StartupApproval.Disabled)]
    [InlineData(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, StartupApproval.Disabled)]
    [InlineData(new byte[] { 7, 0, 0, 0 }, StartupApproval.Disabled)]
    [InlineData(new byte[0], StartupApproval.NotSet)]
    public void TaskManagersSwitchReads(byte[] data, StartupApproval approval) =>
        Assert.Equal(approval, StartupRegistration.ParseApproval(data));

    [Fact]
    public void ValuesThatArentTaskManagersReadAsNotSet()
    {
        Assert.Equal(StartupApproval.NotSet, StartupRegistration.ParseApproval(null));
        Assert.Equal(StartupApproval.NotSet, StartupRegistration.ParseApproval("02"));
        Assert.Equal(StartupApproval.NotSet, StartupRegistration.ParseApproval(2));
    }

    // --- Keeping the value right ------------------------------------------------------------

    [Fact]
    public void TurnedOnItIsAddedThenLeftAlone()
    {
        const string exe = @"C:\Program Files\Karpo\Karpo.exe";

        Assert.Equal(StartupChange.Added, StartupRegistration.Sync(true, exe, root, out var previous));
        Assert.Null(previous);
        Assert.Equal(StartupRegistration.Command(exe), RunValue());
        Assert.Equal(StartupChange.None, StartupRegistration.Sync(true, exe, root, out _));
    }

    /// <summary>After switching from the development build to the published one, the entry follows the copy that runs.</summary>
    [Fact]
    public void AnEntryForAnotherCopyIsRepaired()
    {
        StartupRegistration.Sync(true, @"C:\Repositories\karpo-app\Karpo.Dock\bin\Release\Karpo.exe", root, out _);

        Assert.Equal(StartupChange.Repaired, StartupRegistration.Sync(true, @"C:\Karpo\Karpo.exe", root, out var previous));

        Assert.Equal("\"C:\\Repositories\\karpo-app\\Karpo.Dock\\bin\\Release\\Karpo.exe\" --startup", previous);
        Assert.Equal(StartupRegistration.Command(@"C:\Karpo\Karpo.exe"), RunValue());
    }

    [Fact]
    public void AnEntryWithoutTheArgumentOrQuotesIsRepaired()
    {
        using (var run = root.CreateSubKey(StartupRegistration.RunKey, writable: true))
        {
            run.SetValue(StartupRegistration.ValueName, @"C:\Karpo\Karpo.exe");
        }

        Assert.Equal(StartupChange.Repaired, StartupRegistration.Sync(true, @"C:\Karpo\Karpo.exe", root, out _));
        Assert.Equal("\"C:\\Karpo\\Karpo.exe\" --startup", RunValue());
    }

    [Fact]
    public void TurnedOffKarposEntryGoes()
    {
        StartupRegistration.Sync(true, @"C:\Karpo\Karpo.exe", root, out _);

        Assert.Equal(StartupChange.Removed, StartupRegistration.Sync(false, @"C:\Other\Karpo.exe", root, out _));
        Assert.Null(RunValue());
        Assert.Equal(StartupChange.None, StartupRegistration.Sync(false, @"C:\Other\Karpo.exe", root, out _));
    }

    [Fact]
    public void TurnedOffAnotherProgramsValueByTheSameNameStays()
    {
        using (var run = root.CreateSubKey(StartupRegistration.RunKey, writable: true))
        {
            run.SetValue(StartupRegistration.ValueName, "\"C:\\Tools\\karpo-sync.exe\" /quiet");
        }

        Assert.Equal(StartupChange.None, StartupRegistration.Sync(false, @"C:\Karpo\Karpo.exe", root, out _));
        Assert.NotNull(RunValue());
    }

    [Fact]
    public void TurningItOnLeavesTaskManagersSwitchAlone()
    {
        SetApproval(3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8);

        StartupRegistration.Sync(true, @"C:\Karpo\Karpo.exe", root, out _);

        Assert.True(StartupRegistration.Read(root).TurnedOffInTaskManager);
    }

    [Fact]
    public void ApprovingClearsOnlyADisabledSwitch()
    {
        Assert.False(StartupRegistration.Approve(root));
        SetApproval(2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.False(StartupRegistration.Approve(root));
        Assert.Equal(StartupApproval.Enabled, StartupRegistration.Read(root).Approval);

        SetApproval(3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8);
        Assert.True(StartupRegistration.Approve(root));

        Assert.Equal(StartupApproval.NotSet, StartupRegistration.Read(root).Approval);
        Assert.False(StartupRegistration.Read(root).TurnedOffInTaskManager);
    }

    [Fact]
    public void TheStateSaysWhatIsRegisteredAndWhetherItIsThere()
    {
        Assert.False(StartupRegistration.Read(root).Registered);

        var here = Path.Combine(Isolation.Folder, "Karpo.exe");
        File.WriteAllText(here, "");
        StartupRegistration.Sync(true, here, root, out _);
        var state = StartupRegistration.Read(root);
        Assert.True(state.Registered);
        Assert.Equal(here, state.Program);
        Assert.True(state.ProgramExists);

        File.Delete(here);
        Assert.False(StartupRegistration.Read(root).ProgramExists);
    }

    // --- The logon task that starts it sooner --------------------------------------------------

    private const string Sid = "S-1-5-21-1-2-3-1001";

    /// <summary>Task Scheduler as a dictionary; what it gives back is what was written.</summary>
    private sealed class FakeTasks : ILogonTasks
    {
        public readonly Dictionary<string, string> Tasks = [];
        public bool Refuse;
        public int Writes;

        public string? Read(string name) => Tasks.GetValueOrDefault(name);

        public bool Write(string name, string xml)
        {
            if (Refuse)
            {
                return false;
            }
            Writes++;
            Tasks[name] = xml;
            return true;
        }

        public bool Delete(string name) => !Refuse && Tasks.Remove(name);
    }

    [Fact]
    public void TheTaskStartsThisCopyAtThisPersonsSignInAsSoonAsItCan()
    {
        var xml = StartupRegistration.TaskXml(@"C:\Program Files\Karpo & Co\Karpo.exe", Sid);
        var task = System.Xml.Linq.XDocument.Parse(xml).Root!;
        System.Xml.Linq.XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        string Of(params string[] path) => path.Aggregate(task, (at, name) => at.Element(ns + name)!).Value;

        Assert.Equal(Sid, Of("Triggers", "LogonTrigger", "UserId"));
        Assert.Null(task.Element(ns + "Triggers")!.Element(ns + "LogonTrigger")!.Element(ns + "Delay"));
        Assert.Equal(Sid, Of("Principals", "Principal", "UserId"));
        Assert.Equal("InteractiveToken", Of("Principals", "Principal", "LogonType"));
        Assert.Equal("LeastPrivilege", Of("Principals", "Principal", "RunLevel"));
        // Normal priority, not Task Scheduler's below-normal 7; never ended after 72 hours; on battery too.
        Assert.Equal("4", Of("Settings", "Priority"));
        Assert.Equal("PT0S", Of("Settings", "ExecutionTimeLimit"));
        Assert.Equal("false", Of("Settings", "DisallowStartIfOnBatteries"));
        Assert.Equal("false", Of("Settings", "StopIfGoingOnBatteries"));
        Assert.Equal("IgnoreNew", Of("Settings", "MultipleInstancesPolicy"));
        Assert.Equal(@"C:\Program Files\Karpo & Co\Karpo.exe", Of("Actions", "Exec", "Command"));
        Assert.Equal("--startup --logon-task", Of("Actions", "Exec", "Arguments"));
        Assert.Equal(@"C:\Program Files\Karpo & Co", Of("Actions", "Exec", "WorkingDirectory"));
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-16\"?>", xml);
    }

    [Fact]
    public void ATaskReadsBack()
    {
        Assert.True(StartupRegistration.TryReadTask(StartupRegistration.TaskXml(@"C:\Karpo\Karpo.exe", Sid), out var program, out var arguments, out var version));
        Assert.Equal(@"C:\Karpo\Karpo.exe", program);
        Assert.Equal(StartupRegistration.TaskArguments, arguments);
        Assert.Equal(StartupRegistration.TaskStamp(@"C:\Karpo\Karpo.exe"), version);

        Assert.False(StartupRegistration.TryReadTask(null, out _, out _, out _));
        Assert.False(StartupRegistration.TryReadTask("ERROR: The system cannot find the file specified.", out _, out _, out _));
        Assert.False(StartupRegistration.TryReadTask("<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"/>", out _, out _, out _));
    }

    /// <summary>schtasks gives a definition back in the console's code page: the stamp, all ASCII, survives where a path may not.</summary>
    [Fact]
    public void TheStampIsAsciiAndFollowsThePathNotItsSpelling()
    {
        var stamp = StartupRegistration.TaskStamp(@"C:\Users\Zoë Müller\AppData\Local\Programs\Karpo\Karpo.exe");
        Assert.All(stamp, letter => Assert.True(letter < 128));
        Assert.StartsWith(StartupRegistration.TaskVersion + " ", stamp);
        Assert.Equal(StartupRegistration.TaskStamp(@"C:\Karpo\Karpo.exe"), StartupRegistration.TaskStamp(@"c:\KARPO\karpo.EXE"));
        Assert.NotEqual(StartupRegistration.TaskStamp(@"C:\Karpo\Karpo.exe"), StartupRegistration.TaskStamp(@"D:\Karpo\Karpo.exe"));
    }

    /// <summary>A Run-key person's next start adds the task, once; later starts leave it be.</summary>
    [Fact]
    public void TurnedOnTheTaskIsAddedOnceThenLeftAlone()
    {
        var tasks = new FakeTasks();

        Assert.Equal(LogonTaskChange.Added, StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks));
        Assert.True(tasks.Tasks.ContainsKey(StartupRegistration.TaskName(Sid)));
        Assert.Contains(Sid, StartupRegistration.TaskName(Sid));
        Assert.Equal(LogonTaskChange.None, StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks));
        Assert.Equal(1, tasks.Writes);
    }

    [Fact]
    public void ATaskForAnotherCopyOrAnOlderKarpoIsMadeAgain()
    {
        var tasks = new FakeTasks();
        var name = StartupRegistration.TaskName(Sid);
        StartupRegistration.SyncTask(true, @"C:\Repositories\karpo-app\Karpo.Dock\bin\Release\Karpo.exe", Sid, tasks);

        Assert.Equal(LogonTaskChange.Repaired, StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks));
        Assert.True(StartupRegistration.TryReadTask(tasks.Tasks[name], out var program, out _, out _));
        Assert.Equal(@"C:\Karpo\Karpo.exe", program);

        tasks.Tasks[name] = tasks.Tasks[name].Replace(StartupRegistration.TaskStamp(@"C:\Karpo\Karpo.exe"), "0 00000000");
        Assert.Equal(LogonTaskChange.Repaired, StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks));

        tasks.Tasks[name] = "not a task";
        Assert.Equal(LogonTaskChange.Repaired, StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks));
    }

    [Fact]
    public void TurnedOffTheTaskGoes()
    {
        var tasks = new FakeTasks();
        StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks);

        Assert.Equal(LogonTaskChange.Removed, StartupRegistration.SyncTask(false, "", Sid, tasks));
        Assert.Empty(tasks.Tasks);
        Assert.Equal(LogonTaskChange.None, StartupRegistration.SyncTask(false, "", Sid, tasks));
    }

    /// <summary>Refused (a locked-down PC): said so, and the Run entry still starts Karpo, later.</summary>
    [Fact]
    public void ARefusalIsSaid()
    {
        var tasks = new FakeTasks { Refuse = true };
        Assert.Equal(LogonTaskChange.Failed, StartupRegistration.SyncTask(true, @"C:\Karpo\Karpo.exe", Sid, tasks));
    }

    /// <summary>The task's copy goes on only where the Run entry would start Karpo: Task Manager's switch still decides.</summary>
    [Fact]
    public void TheTasksCopyFollowsTheRunEntryAndTaskManager()
    {
        Assert.False(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));

        StartupRegistration.Sync(true, @"C:\Karpo\Karpo.exe", root, out _);
        Assert.True(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));

        SetApproval(2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        Assert.True(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));

        SetApproval(3, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8);
        Assert.False(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));

        StartupRegistration.Approve(root);
        Assert.True(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));

        StartupRegistration.Sync(false, "", root, out _);
        Assert.False(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));

        using (var run = root.CreateSubKey(StartupRegistration.RunKey, writable: true))
        {
            run.SetValue(StartupRegistration.ValueName, "\"C:\\Tools\\karpo-sync.exe\" /quiet");
        }
        Assert.False(StartupRegistration.TaskMayStart(StartupRegistration.Read(root)));
    }

    /// <summary>Only the task's own start is gated: a start from the Run entry or by hand never reads it.</summary>
    [Fact]
    public void OnlyTheTasksStartIsGated()
    {
        var before = Environment.GetEnvironmentVariable("KARPO_STARTUP_ROOT");
        try
        {
            Environment.SetEnvironmentVariable("KARPO_STARTUP_ROOT", name);
            Assert.True(StartupRegistration.StartedByTaskUnwanted(["--startup", "--logon-task"]));
            Assert.False(StartupRegistration.StartedByTaskUnwanted(["--startup"]));
            Assert.False(StartupRegistration.StartedByTaskUnwanted([]));

            StartupRegistration.Sync(true, @"C:\Karpo\Karpo.exe", root, out _);
            Assert.False(StartupRegistration.StartedByTaskUnwanted(["--startup", "--logon-task"]));
        }
        finally
        {
            Environment.SetEnvironmentVariable("KARPO_STARTUP_ROOT", before);
        }
    }
}
