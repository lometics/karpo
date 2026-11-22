// Files dragged over the bar and dropped on it, as Windows 11's taskbar takes them (AppDrops): what a
// drop on an app's button, a folder or the bar does, and when resting on a button brings its window
// forward. Decisions and a clock, so nothing is opened, pinned or brought forward.

using Karpo.Dock;

namespace Karpo.Tests.Pinning;

public sealed class AppDropTests
{
    private const string Notepad = @"C:\Windows\System32\notepad.exe";
    private const string Frame = @"C:\Windows\System32\ApplicationFrameHost.exe";
    private const string Photos = "Microsoft.Windows.Photos_8wekyb3d8bbwe!App";

    /// <summary>Onto a folder that takes pins, into it — whatever else is there.</summary>
    [Fact]
    public void OntoAFolderTheyGoIntoIt() =>
        Assert.Equal(new DropAction(DropKind.IntoFolder), AppDrops.Decide(new DropSpot(Folder: true, Button: true, Program: Notepad)));

    /// <summary>Onto the bar between buttons and widgets, they are pinned where the line shows, as Karpo's bar always pinned them.</summary>
    [Fact]
    public void OntoTheBarTheyArePinned() =>
        Assert.Equal(new DropAction(DropKind.Pin), AppDrops.Decide(new DropSpot()));

    /// <summary>Onto a program's pin — its .exe, or the one its shortcut points at —, opened with that program.</summary>
    [Fact]
    public void OntoAProgramsPinTheyOpenWithIt() =>
        Assert.Equal(new DropAction(DropKind.OpenWith, Notepad), AppDrops.Decide(new DropSpot(Button: true, Pinned: true, Program: Notepad, WindowExe: @"C:\Other\other.exe")));

    /// <summary>Onto a store app's pin, opened with that app by its ID, as Windows opens a file with one.</summary>
    [Fact]
    public void OntoAStoreAppsPinTheyOpenWithIt() =>
        Assert.Equal(new DropAction(DropKind.OpenApp, Photos), AppDrops.Decide(new DropSpot(Button: true, Pinned: true, Aumid: Photos)));

    /// <summary>
    /// Onto an open app's button, opened with its program — which the bar used to pin beside it
    /// instead —, and a store app's through its ID, never through the frame its windows belong to.
    /// </summary>
    [Fact]
    public void OntoAnOpenAppsButtonTheyOpenWithIt()
    {
        Assert.Equal(new DropAction(DropKind.OpenWith, Notepad), AppDrops.Decide(new DropSpot(Button: true, WindowExe: Notepad)));
        Assert.Equal(new DropAction(DropKind.OpenApp, Photos), AppDrops.Decide(new DropSpot(Button: true, Aumid: Photos, WindowExe: Frame)));
    }

    /// <summary>An open app nothing here can start them with takes nothing, rather than pinning them beside it.</summary>
    [Fact]
    public void AnOpenAppThatCannotOpenThemTakesNothing()
    {
        Assert.Equal(new DropAction(DropKind.Nothing), AppDrops.Decide(new DropSpot(Button: true, WindowExe: Frame)));
        Assert.Equal(new DropAction(DropKind.Nothing), AppDrops.Decide(new DropSpot(Button: true)));
        // A desktop app's own ID is no store app's: its program is what opens them.
        Assert.Equal(new DropAction(DropKind.OpenWith, Notepad), AppDrops.Decide(new DropSpot(Button: true, Aumid: "Microsoft.Windows.Notepad", WindowExe: Notepad)));
    }

    /// <summary>A pin that is not an app — a document, a folder, a link — still takes them by pinning them beside it.</summary>
    [Fact]
    public void APinThatIsNoAppPinsThemBesideIt() =>
        Assert.Equal(new DropAction(DropKind.Pin), AppDrops.Decide(new DropSpot(Button: true, Pinned: true)));

    [Theory]
    [InlineData("Microsoft.WindowsNotepad_8wekyb3d8bbwe!App", true)]
    [InlineData("A!B", true)]
    [InlineData("Microsoft.Windows.Explorer", false)]
    [InlineData("!App", false)]
    [InlineData("Family!", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AStoreAppsIdIsAFamilyAndAnApp(string? aumid, bool packaged) =>
        Assert.Equal(packaged, AppDrops.Packaged(aumid));

    [Theory]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsNotepad_8wekyb3d8bbwe!App", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App")]
    [InlineData(@"  SHELL:appsfolder\Chrome  ", "Chrome")]
    [InlineData(@"shell:AppsFolder\", null)]
    [InlineData(@"C:\Windows\System32\notepad.exe", null)]
    [InlineData("https://example.com", null)]
    public void AnAppsFolderTargetNamesItsApp(string target, string? aumid) =>
        Assert.Equal(aumid, AppDrops.AumidIn(target));

    // --- Resting on a button ------------------------------------------------------------------------

    /// <summary>Resting on a button for the hover time brings its window forward, once, however long it rests after.</summary>
    [Fact]
    public void RestingForTheHoverTimeBringsTheWindowOnce()
    {
        var hover = new DragHover(400);

        Assert.Equal(400u, hover.Over(3, now: 1000));
        Assert.Equal(-1, hover.Due(1399));
        Assert.Equal(150u, hover.Over(3, now: 1250));
        Assert.Equal(3, hover.Due(1400));
        Assert.Equal(-1, hover.Due(1500));
        Assert.Null(hover.Over(3, now: 2000));
        Assert.Equal(-1, hover.Due(5000));
    }

    /// <summary>Moving to another button starts the wait again, for that one.</summary>
    [Fact]
    public void MovingToAnotherButtonStartsOver()
    {
        var hover = new DragHover(400);

        hover.Over(3, now: 1000);
        Assert.Equal(400u, hover.Over(5, now: 1300));
        Assert.Equal(-1, hover.Due(1400));
        Assert.Equal(5, hover.Due(1700));
        // And back to the first, which comes again: the drag left it.
        Assert.Equal(400u, hover.Over(3, now: 1800));
        Assert.Equal(3, hover.Due(2200));
    }

    /// <summary>Off every button, leaving the bar, or dropping: nothing comes forward.</summary>
    [Fact]
    public void OffTheButtonsNothingComes()
    {
        var hover = new DragHover(400);

        Assert.Null(hover.Over(-1, now: 1000));
        Assert.Equal(-1, hover.Due(9000));
        hover.Over(2, now: 1000);
        hover.Leave();
        Assert.Equal(-1, hover.Due(9000));
        Assert.Equal(400u, hover.Over(2, now: 9000));
    }
}
