// "Start with Windows" for the Microsoft Store's copy (docs\widgets.md §710; Karpo.Core\PackagedStartup.cs):
// the package's startup task read as the switch reads a Run entry, turned only where it is Karpo's to
// turn, and a start by it told apart from a person's. Windows' side (Karpo.Dock\PackagedStartup.cs) is
// not asked here: a test has no package.

namespace Karpo.Tests.Startup;

public sealed class PackagedStartupTests
{
    private const string Exe = @"C:\Program Files\WindowsApps\NathanWalser.Karpo_1.0.0.0_x64__s8mm2zcbg0yjw\Karpo.exe";

    /// <summary>The numbers are Windows.ApplicationModel.StartupTaskState's: the dock casts one to the other.</summary>
    [Fact]
    public void TheStatesAreWindowsOwnByNumber()
    {
        Assert.Equal((int)Windows.ApplicationModel.StartupTaskState.Disabled, (int)PackagedStartupState.Disabled);
        Assert.Equal((int)Windows.ApplicationModel.StartupTaskState.DisabledByUser, (int)PackagedStartupState.DisabledByUser);
        Assert.Equal((int)Windows.ApplicationModel.StartupTaskState.Enabled, (int)PackagedStartupState.Enabled);
        Assert.Equal((int)Windows.ApplicationModel.StartupTaskState.DisabledByPolicy, (int)PackagedStartupState.DisabledByPolicy);
        Assert.Equal((int)Windows.ApplicationModel.StartupTaskState.EnabledByPolicy, (int)PackagedStartupState.EnabledByPolicy);
    }

    /// <summary>
    /// The switch says of the task what it says of a Run entry: on while it is enabled; "turned off in Task
    /// Manager" where the person (or the administrator) switched it off; "sets this up the next time it
    /// starts" while Karpo has not enabled it yet; and nothing against the setting before Windows answered.
    /// </summary>
    [Fact]
    public void TheSwitchReadsTheTaskAsItReadsARunEntry()
    {
        var on = new Config { StartWithWindows = true };

        foreach (var enabled in new[] { PackagedStartupState.Enabled, PackagedStartupState.EnabledByPolicy })
        {
            var state = StartupRegistration.PackagedState(enabled, Exe);
            Assert.True(StartupSwitch.IsOn(on, state));
            Assert.Null(StartupSwitch.Why(on, state));
        }
        foreach (var off in new[] { PackagedStartupState.DisabledByUser, PackagedStartupState.DisabledByPolicy })
        {
            var state = StartupRegistration.PackagedState(off, Exe);
            Assert.True(state.TurnedOffInTaskManager);
            Assert.False(StartupSwitch.IsOn(on, state));
            Assert.Equal(S.SettingsGeneralStartupTurnedOff, StartupSwitch.Why(on, state));
        }
        var notYet = StartupRegistration.PackagedState(PackagedStartupState.Disabled, Exe);
        Assert.False(notYet.Registered);
        Assert.Equal(S.SettingsGeneralStartupNotYet, StartupSwitch.Why(on, notYet));

        var unknown = StartupRegistration.PackagedState(null, Exe);
        Assert.True(StartupSwitch.IsOn(on, unknown));
        Assert.Null(StartupSwitch.Why(on, unknown));
        // The setting off is off, whatever the task is.
        Assert.False(StartupSwitch.IsOn(new Config { StartWithWindows = false }, StartupRegistration.PackagedState(PackagedStartupState.Enabled, Exe)));
    }

    /// <summary>
    /// The dock makes the task follow the setting, and only where the task is Karpo's to turn: one the
    /// person or the administrator switched is left as they left it, either way.
    /// </summary>
    [Theory]
    [InlineData(true, PackagedStartupState.Disabled, PackagedStartupAct.Enable)]
    [InlineData(true, PackagedStartupState.Enabled, PackagedStartupAct.None)]
    [InlineData(true, PackagedStartupState.DisabledByUser, PackagedStartupAct.None)]
    [InlineData(true, PackagedStartupState.DisabledByPolicy, PackagedStartupAct.None)]
    [InlineData(true, PackagedStartupState.EnabledByPolicy, PackagedStartupAct.None)]
    [InlineData(false, PackagedStartupState.Enabled, PackagedStartupAct.Disable)]
    [InlineData(false, PackagedStartupState.Disabled, PackagedStartupAct.None)]
    [InlineData(false, PackagedStartupState.DisabledByUser, PackagedStartupAct.None)]
    [InlineData(false, PackagedStartupState.EnabledByPolicy, PackagedStartupAct.None)]
    public void TheTaskFollowsTheSettingWhereItIsKarposToTurn(bool setting, PackagedStartupState state, PackagedStartupAct act)
    {
        Assert.Equal(act, StartupRegistration.PackagedAct(setting, state));
    }

    /// <summary>Turning the switch off disables the task at once; turning it on against the person's own switch changes the setting and nothing of Windows'.</summary>
    [Fact]
    public void TurningTheSwitchAsksWindowsOnlyToTurnOff()
    {
        var (unregistered, approved) = (0, 0);
        var state = PackagedStartupState.Enabled;
        var windows = new StartupSwitch(() => StartupRegistration.PackagedState(state, Exe), () => { unregistered++; return true; }, () => { approved++; return false; });

        var config = new Config { StartWithWindows = true };
        Assert.True(windows.Turn(config, false));
        Assert.False(config.StartWithWindows);
        Assert.Equal((1, 0), (unregistered, approved));

        // Switched off in Windows by the person, then turned on in Karpo: asked, refused, and it goes on saying so.
        state = PackagedStartupState.DisabledByUser;
        Assert.True(windows.Turn(config, true));
        Assert.True(config.StartWithWindows);
        Assert.Equal((1, 1), (unregistered, approved));
        Assert.Equal(S.SettingsGeneralStartupTurnedOff, StartupSwitch.Why(config, windows.Read()));
    }

    /// <summary>A start by the task has no command line of its own: "--startup" is added, once, and nothing where a person started it.</summary>
    [Fact]
    public void AStartByTheTaskSaysStartupAsARunEntrysDoes()
    {
        Assert.Equal([StartupRegistration.Argument], StartupRegistration.PackagedArguments([], byStartupTask: true));
        Assert.Equal(["--gpu", StartupRegistration.Argument], StartupRegistration.PackagedArguments(["--gpu"], byStartupTask: true));
        Assert.Equal([StartupRegistration.Argument], StartupRegistration.PackagedArguments([StartupRegistration.Argument], byStartupTask: true));
        Assert.Empty(StartupRegistration.PackagedArguments([], byStartupTask: false));
        // What the dock makes of it: a start at sign-in, which waits for Explorer's taskbar and asks nothing of a running dock.
        Assert.True(Karpo.Dock.Options.FromArgs(StartupRegistration.PackagedArguments([], byStartupTask: true)).Startup);
    }

    /// <summary>
    /// The package's manifest and the code name the same things: the task the dock asks Windows for, the
    /// application the guard starts again by, the exe, and Partner Center's identity for "Karpo".
    /// </summary>
    [Fact]
    public void TheManifestNamesWhatTheCodeAsksFor()
    {
        var manifest = RepoSource.Read("package", "AppxManifest.xml");

        Assert.Contains($"TaskId=\"{StartupRegistration.PackagedTaskId}\"", manifest);
        Assert.Contains($"<Application Id=\"{PackageIdentity.ApplicationId}\" Executable=\"{StartupRegistration.ExeName}\"", manifest);
        Assert.Contains("Name=\"NathanWalser.Karpo\"", manifest);
        Assert.Contains("Publisher=\"CN=C470EED8-37EC-4C04-9586-6B249062A51A\"", manifest);
        // The real registry and files, and the capability that allows asking for them.
        Assert.Contains("<desktop6:RegistryWriteVirtualization>disabled</desktop6:RegistryWriteVirtualization>", manifest);
        Assert.Contains("<desktop6:FileSystemWriteVirtualization>disabled</desktop6:FileSystemWriteVirtualization>", manifest);
        Assert.Contains("<rescap:Capability Name=\"unvirtualizedResources\" />", manifest);
    }

    /// <summary>Every language Karpo speaks is one the package declares, by its Store listing's tag: a listing in a language the package lacks is refused.</summary>
    [Fact]
    public void ThePackageDeclaresEveryListingLanguage()
    {
        var manifest = RepoSource.Read("package", "AppxManifest.xml");
        var listings = Directory.GetFiles(Path.Combine(RepoSource.Root(), "store", "listing"), "*.json");

        Assert.Equal(40, listings.Length);
        foreach (var listing in listings)
        {
            using var read = System.Text.Json.JsonDocument.Parse(File.ReadAllText(listing));
            var tag = read.RootElement.GetProperty("language").GetString()!;
            Assert.Contains($"<Resource Language=\"{tag}\"", manifest, StringComparison.OrdinalIgnoreCase);
        }
    }
}
