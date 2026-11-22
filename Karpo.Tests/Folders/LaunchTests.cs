// §491: the one launcher (Karpo.Dock\Launch.cs; the person, 2026-11-02: "as soon as something opens an external program
// like the settings for example or a website or something like that it should close the current folder that is open").
// Every way Karpo hands something to Windows closes what of Karpo's is open first, then hands it over; Windows' own
// panels and a notification's background activation close nothing; a launch closes the folder arranged or not; a
// launch off the dock's thread never runs the dock's closing there; and nothing in Karpo but the launcher calls the
// shell to start anything. Windows is stood in for throughout (LaunchStand): nothing is opened.

using System.Diagnostics;
using System.Text.RegularExpressions;
using Karpo.Dock;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Folders;

public sealed partial class LaunchTests
{
    /// <summary>Every way out, each with what the stand-in should see it as.</summary>
    public static TheoryData<string> Ways() =>
    [
        "open", "open with verb", "aside", "elevated", "with", "with app", "properties", "select", "explorer", "jump", "packaged", "program", "open with dialog",
    ];

    private static (LaunchKind Kind, string Target) Run(string way)
    {
        switch (way)
        {
            case "open":
                _ = Launch.Open("https://example.com/story");
                return (LaunchKind.Open, "https://example.com/story");
            case "open with verb":
                _ = Launch.Open(@"C:\Windows\System32\cmd.exe", "runas");
                return (LaunchKind.Open, @"C:\Windows\System32\cmd.exe");
            case "aside":
                Launch.Aside("msteams:/l/meetup-join/1", "https://teams.microsoft.com/l/meetup-join/1");
                return (LaunchKind.Open, "msteams:/l/meetup-join/1");
            case "elevated":
                Launch.Elevated(@"C:\Windows\System32\notepad.exe");
                return (LaunchKind.Elevated, @"C:\Windows\System32\notepad.exe");
            case "with":
                Launch.With(@"C:\Windows\System32\notepad.exe", [@"C:\a.txt", @"C:\b.txt"]);
                return (LaunchKind.With, @"C:\Windows\System32\notepad.exe");
            case "with app":
                _ = Launch.WithApp("Microsoft.Photos_8wekyb3d8bbwe!App", [@"C:\a.png"]);
                return (LaunchKind.WithApp, "Microsoft.Photos_8wekyb3d8bbwe!App");
            case "properties":
                Launch.Properties(@"C:\");
                return (LaunchKind.Properties, @"C:\");
            case "select":
                Launch.Select(@"C:\Users\Public\Documents\a.txt");
                return (LaunchKind.Select, @"C:\Users\Public\Documents\a.txt");
            case "explorer":
                Launch.Explorer(@"C:\Users\Public");
                return (LaunchKind.Explorer, @"C:\Users\Public");
            case "jump":
                Launch.Jump(@"C:\Program Files\App\app.exe", "--new-window", @"C:\Program Files\App");
                return (LaunchKind.Jump, @"C:\Program Files\App\app.exe");
            case "packaged":
                Launch.Packaged("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", "new-tab");
                return (LaunchKind.Packaged, "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App");
            case "open with dialog":
                Launch.OpenWith(@"C:\Users\Public\Documents\a.txt");
                return (LaunchKind.OpenWith, @"C:\Users\Public\Documents\a.txt");
            case "program":
                Assert.Null(Launch.Program(new ProcessStartInfo(@"C:\Karpo\Karpo.exe") { UseShellExecute = false }));
                return (LaunchKind.Program, @"C:\Karpo\Karpo.exe");
        }
        throw new ArgumentException(way);
    }

    /// <summary>
    /// Every way Karpo hands something to Windows — a page, a setting, a program as administrator, files with a
    /// program or an app, Properties, a file's place, a folder in File Explorer, a jump list's line or task, Karpo's
    /// own Wi-Fi dialog, a meeting's link off the dock's thread — closes what of Karpo's is open first, then hands it
    /// over, once.
    /// </summary>
    [Theory]
    [MemberData(nameof(Ways))]
    public void EveryLaunchClosesWhatIsOpenFirst(string way)
    {
        using var stand = Launch.Stand();
        var (kind, target) = Run(way);
        Assert.Equal(2, stand.Said.Count);
        Assert.Equal("leave", stand.Said[0]);
        var launched = Assert.IsType<Launched>(stand.Said[1]);
        Assert.Equal((kind, target), (launched.Kind, launched.Target));
    }

    /// <summary>What a stand-in says Windows answered is what the caller hears: a meeting app's address that does not open falls back.</summary>
    [Fact]
    public void TheCallerHearsWhetherItOpened()
    {
        using var stand = Launch.Stand();
        Assert.True(Launch.Open("ms-settings:sound"));
        stand.Opens = false;
        Assert.False(Launch.Open("ms-settings:sound"));
    }

    /// <summary>
    /// §665: no launch fails in silence. Where the shell says nothing of its own — a launch off the dock's thread (a
    /// meeting's Join, an event), a file's place, a store app — Karpo says it, once, in the words the caller gave (the
    /// meeting's) or its own ("Couldn't open a.txt"), with Windows' reason. Where the shell has said it already (its own
    /// message box for an Open), Karpo only writes it down; and a launch the person cancelled is no failure.
    /// </summary>
    [Fact]
    public void ALaunchThatFailsIsSaidToThePerson()
    {
        using var stand = Launch.Stand();
        stand.Opens = false;
        stand.Error = 1155; // ERROR_NO_ASSOCIATION

        Launch.Aside("msteams:/l/meetup-join/1", "https://teams.microsoft.com/l/meetup-join/1", S.DockMeetingJoinFailed);
        var meeting = Assert.Single(stand.Failed);
        Assert.True(meeting.Told);
        Assert.Equal(S.DockMeetingJoinFailed, meeting.Title);
        Assert.Equal(new System.ComponentModel.Win32Exception(1155).Message, meeting.Why);

        stand.Failed.Clear();
        Launch.Aside("https://outlook.office365.com/owa/?itemid=1");
        Assert.Equal(S.DockLaunchFailed("outlook.office365.com"), Assert.Single(stand.Failed).Title);

        stand.Failed.Clear();
        Launch.Select(@"C:\Users\Public\Documents\a.txt");
        var place = Assert.Single(stand.Failed);
        Assert.True(place.Told);
        Assert.Equal(S.DockLaunchFailed("a.txt"), place.Title);

        // The shell's own message box said it: written down, not said twice.
        stand.Failed.Clear();
        Assert.False(Launch.Open(@"C:\Users\Public\Documents\a.txt"));
        Assert.False(Assert.Single(stand.Failed).Told);

        // Cancelled (Windows' own dialog, UAC's No): nothing to say.
        stand.Failed.Clear();
        stand.Error = 1223; // ERROR_CANCELLED
        Launch.Aside("msteams:/l/meetup-join/1", null, S.DockMeetingJoinFailed);
        Assert.False(Assert.Single(stand.Failed).Told);

        // What opens says nothing.
        stand.Failed.Clear();
        stand.Opens = true;
        Launch.Aside("msteams:/l/meetup-join/1", null, S.DockMeetingJoinFailed);
        Assert.Empty(stand.Failed);
    }

    /// <summary>
    /// §665: off the dock's thread, on a thread that ends as soon as the shell returns, the launch is waited for
    /// (SEE_MASK_NOASYNC — a packaged app's protocol, Teams' msteams:, is activated through a DelegateExecute handler,
    /// which the shell may finish after it returns: on a thread that has ended, never) and the shell shows no box of its
    /// own (SEE_MASK_FLAG_NO_UI): the fallback is tried, and Karpo says what failed.
    /// </summary>
    [Fact]
    public void AnAsideWaitsForTheShellAndLeavesTheTellingToKarpo()
    {
        const uint noAsync = 0x00000100, noUi = 0x00000400;
        Assert.Equal(noAsync | noUi, Launch.Shell.Mask("open", waits: true, quiet: true) & (noAsync | noUi));
        Assert.Equal(0u, Launch.Shell.Mask("open") & (noAsync | noUi));
    }

    /// <summary>§665: an aside tries the app's address, then the page; what failed last is why.</summary>
    [Fact]
    public void AnAsideTriesTheAppThenThePageThenSaysWhy()
    {
        var tried = new List<string>();
        bool Fails(string target)
        {
            tried.Add(target);
            System.Runtime.InteropServices.Marshal.SetLastSystemError(target.StartsWith("msteams:", StringComparison.Ordinal) ? 1155 : 2);
            return false;
        }
        Assert.Equal((false, 2), Launch.Tried("msteams:/l/meetup-join/1", "https://teams.microsoft.com/l/meetup-join/1", Fails));
        Assert.Equal(["msteams:/l/meetup-join/1", "https://teams.microsoft.com/l/meetup-join/1"], tried);

        tried.Clear();
        Assert.Equal((false, 1155), Launch.Tried("msteams:/l/meetup-join/1", null, Fails));

        tried.Clear();
        Assert.Equal((true, 0), Launch.Tried("msteams:/l/meetup-join/1", "https://teams.microsoft.com/l/meetup-join/1", target =>
        {
            tried.Add(target);
            return true;
        }));
        Assert.Single(tried);
    }

    /// <summary>
    /// Windows' own menus that open by an address — the Wi-Fi list, the pen menu, the virtual touchpad, the touch
    /// keyboard's host — are no launch: they come one at a time by their own rule (§466), and nothing of Karpo's closes
    /// for them. Settings, the Snipping Tool and the Store are apps: theirs is.
    /// </summary>
    [Theory]
    [InlineData(WindowsButtons.Networks)]
    [InlineData(WindowsButtons.PenMenu)]
    [InlineData(WindowsButtons.VirtualTouchpad)]
    [InlineData(@"C:\Program Files\Common Files\microsoft shared\ink\TabTip.exe")]
    public void WindowsOwnPanelsCloseNothing(string address)
    {
        using var stand = Launch.Stand();
        _ = Launch.Open(address);
        var launched = Assert.IsType<Launched>(Assert.Single(stand.Said));
        Assert.Equal(LaunchKind.Panel, launched.Kind);
    }

    [Theory]
    [InlineData(WindowsButtons.SettingsApp)]
    [InlineData(WindowsButtons.Snip)]
    [InlineData(WindowsButtons.SnippingStore)]
    [InlineData("ms-settings:network-wifi")]
    public void WindowsAppsAreLaunches(string address)
    {
        using var stand = Launch.Stand();
        _ = Launch.Open(address);
        Assert.Equal("leave", stand.Said[0]);
        Assert.Equal(LaunchKind.Open, Assert.IsType<Launched>(stand.Said[1]).Kind);
    }

    /// <summary>
    /// A notification's activation that brings its app forward (a click on the card, a protocol button) is a launch;
    /// one that runs it in the background (a "Like") shows nothing and closes nothing. Both reach the app.
    /// </summary>
    [Fact]
    public void AToastBroughtForwardLeavesOneInTheBackgroundDoesNot()
    {
        var activator = new FakeActivator();
        ToastActivator.Made = activator;
        try
        {
            using var stand = Launch.Stand();
            Launch.Toast(new ToastActivation("chat", "action=view", ToastActivationKind.Foreground, []));
            Assert.Equal("leave", stand.Said[0]);
            stand.Said.Clear();
            Launch.Toast(new ToastActivation("chat", "action=like", ToastActivationKind.Background, []));
            Assert.Equal(LaunchKind.Toast, Assert.IsType<Launched>(Assert.Single(stand.Said)).Kind);
            Assert.Equal(["action=view", "action=like"], activator.Activated.Select(activation => activation.Arguments));
        }
        finally
        {
            ToastActivator.Made = null;
        }
    }

    /// <summary>A launch closes the open folder whether its bar is arranged or not; edit mode itself is not the folder's to end.</summary>
    [Fact]
    public void ALaunchClosesTheFolderArrangedOrNot()
    {
        Assert.True(OneFolder.Closes(FolderCloser.Launch, arranging: false));
        Assert.True(OneFolder.Closes(FolderCloser.Launch, arranging: true));
    }

    /// <summary>
    /// The dock's closing runs on the dock's thread only: a launch there runs it at once, a launch off it (a sign-in's
    /// browser, the Wi-Fi dialog's thread) leaves it to the dock's post, never touching the dock's windows itself.
    /// </summary>
    [Fact]
    public void TheDocksClosingRunsOnItsThreadOnly()
    {
        var closed = 0;
        Action closing = () => closed++;
        Launch.Leaves(closing, HWND.NULL);
        try
        {
            Launch.Leave();
            Assert.Equal(1, closed);
            var elsewhere = new Thread(Launch.Leave);
            elsewhere.Start();
            elsewhere.Join();
            Assert.Equal(1, closed);
        }
        finally
        {
            Launch.LeavesNot(closing);
        }
        Launch.Leave();
        Assert.Equal(1, closed);
    }

    /// <summary>
    /// Nothing in Karpo but the launcher calls the shell to start anything: no ShellExecute, Process.Start, app
    /// activation, CreateProcess or WinRT Launcher anywhere else, so no new way out can skip closing what is open.
    /// The exceptions say why: the guard starting Karpo itself again, and the scheduled task's hidden schtasks.
    /// The launcher's own shell calls are made elsewhere only by a notification's roads, after Launch.Toast.
    /// </summary>
    [Fact]
    public void NothingButTheLauncherStartsAnything()
    {
        var root = RepoSource.Root();
        if (root.Length == 0)
        {
            return;
        }
        var allowed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [Path.Combine("Karpo.Dock", "Launch.cs")] = "the launcher itself",
            [Path.Combine("Karpo.Dock", "DockLauncher.cs")] = "the guard starting the dock again, outside every job: Karpo itself, nothing shown",
            [Path.Combine("Karpo.Dock", "Dock.Guard.cs")] = "the dock starting its guard: Karpo itself, nothing shown",
            [Path.Combine("Karpo.Core", "StartupTask.cs")] = "schtasks.exe, hidden, for Karpo's start with Windows",
        };
        var wrong = new List<string>();
        foreach (var project in new[] { "Karpo.Dock", "Karpo.Core", "Karpo.Accounts" })
        {
            foreach (var path in RepoSource.CSharp(project))
            {
                var relative = Path.GetRelativePath(root, path);
                var lines = File.ReadAllLines(path);
                for (var at = 0; at < lines.Length; at++)
                {
                    var line = lines[at];
                    if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    if (Starts().IsMatch(line) && !allowed.ContainsKey(relative))
                    {
                        wrong.Add($"{relative}:{at + 1}: {line.Trim()}");
                    }
                    if (line.Contains("Launch.Shell.", StringComparison.Ordinal)
                        && !relative.EndsWith(Path.Combine("Karpo.Dock", "ToastActivation.cs"), StringComparison.OrdinalIgnoreCase))
                    {
                        wrong.Add($"{relative}:{at + 1}: {line.Trim()}");
                    }
                }
            }
        }
        Assert.True(wrong.Count == 0, "A launch outside the launcher (Karpo.Dock\\Launch.cs):" + Environment.NewLine + string.Join(Environment.NewLine, wrong));
    }

    [GeneratedRegex(@"\bShellExecute(Ex)?W\s*\(|->ShellExecute|\bProcess\.Start\(|(?<!Launch\.Shell\.)\bActivateApplication\(|(?<!Launch\.Shell\.)\bActivateForFile\(|\bSHOpenFolderAndSelectItems\(|\bLauncher\.Launch|\bCreateProcess(W)?\(|\bWinExec\(")]
    private static partial Regex Starts();
}
