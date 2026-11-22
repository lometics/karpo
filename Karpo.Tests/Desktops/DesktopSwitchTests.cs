// A switch to another virtual desktop from the switcher's list (Desktops.Go), and where the
// foreground goes with it (Desktops.FrontOf, QuickFlyout.GivesFocusBack). The shell is a
// stand-in: no test here moves the person's desktops or their foreground.
//
// The bug these were written for: choosing another desktop did nothing. The list's popup gave
// the foreground back to the window it had come from, a window of the desktop being left, as
// the switch went, and Windows switched straight back to it.

using Karpo.Dock;
using Karpo.Dock.QuickSettings;

// Not Karpo.Tests.Desktops: see PeekAndPagingTests.
namespace Karpo.Tests.DesktopsArea;

public sealed class DesktopSwitchTests
{
    private static readonly Guid One = new("B310E7D9-A466-4571-BF9D-69E89EC14758");
    private static readonly Guid Two = new("CF3462BA-5DC9-458B-BD4B-13CFC5BCCB59");
    private static readonly Guid Three = new("0F0E0D0C-0B0A-0908-0706-050403020100");
    private static readonly Guid[] Ids = [One, Two, Three];

    /// <summary>What a switch asked of the shell, in order.</summary>
    private sealed class Shell(bool straight) : Desktops.ISwitchShell
    {
        public List<string> Said { get; } = [];

        public bool Switch(Guid desktop)
        {
            Said.Add($"switch {Name(desktop)}");
            return straight;
        }

        public void Keys(int from, int to) => Said.Add($"keys {from}->{to}");

        public void Front(Guid? desktop) => Said.Add($"front {(desktop is { } on ? Name(on) : "showing")}");

        private static string Name(Guid id) => id == One ? "one" : id == Two ? "two" : id == Three ? "three" : "?";
    }

    [Fact]
    public void AStraightSwitchGivesTheForegroundToTheDesktopArrivedAtAfterwards()
    {
        var shell = new Shell(straight: true);

        Assert.Equal(Desktops.SwitchWay.Straight, Desktops.Go(shell, Ids, current: 0, count: 3, to: 2));

        // Nothing of the desktop being left is activated: that is what switched straight back.
        Assert.Equal(["switch three", "front three"], shell.Said);
    }

    [Fact]
    public void WithoutTheShellsInterfaceTheKeysStartFromTheDesktopShowing()
    {
        var shell = new Shell(straight: false);

        Assert.Equal(Desktops.SwitchWay.Keys, Desktops.Go(shell, Ids, current: 2, count: 3, to: 0));

        // Windows moves the foreground along with its own keys, as for a person typing them.
        Assert.Equal(["switch one", "front three", "keys 2->0"], shell.Said);
    }

    [Fact]
    public void TheDesktopShowingOnlyGetsItsForegroundBack()
    {
        var shell = new Shell(straight: true);

        Assert.Equal(Desktops.SwitchWay.None, Desktops.Go(shell, Ids, current: 1, count: 3, to: 1));

        Assert.Equal(["front two"], shell.Said);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void ADesktopThatIsNotThereIsNoSwitch(int to)
    {
        var shell = new Shell(straight: true);

        Assert.Equal(Desktops.SwitchWay.None, Desktops.Go(shell, Ids, current: 0, count: 3, to: to));

        Assert.Empty(shell.Said);
    }

    [Fact]
    public void DesktopsTheRegistryDoesNotNameAreReachedByTheKeys()
    {
        // Three desktops counted, none listed yet (a registry behind): no id to switch straight to.
        var shell = new Shell(straight: true);

        Assert.Equal(Desktops.SwitchWay.Keys, Desktops.Go(shell, [], current: 0, count: 3, to: 2));

        Assert.Equal(["front showing", "keys 0->2"], shell.Said);
    }

    [Fact]
    public void TheForegroundGoesToTheWindowLastUsedOnTheDesktop()
    {
        Desktops.Candidate[] zOrder =
        [
            // Karpo's own and a tool window: never.
            new(1, Eligible: false, Desktop: null, Hidden: false, Elsewhere: false),
            // The window just left, on top still.
            new(2, Eligible: true, Desktop: One, Hidden: false, Elsewhere: false),
            // A suspended Store app on the desktop arrived at: cloaked by itself, so no.
            new(3, Eligible: true, Desktop: Two, Hidden: true, Elsewhere: true),
            // The desktop's own top window, still cloaked by the switch: this one.
            new(4, Eligible: true, Desktop: Two, Hidden: false, Elsewhere: true),
            new(5, Eligible: true, Desktop: Two, Hidden: false, Elsewhere: false),
        ];

        Assert.Equal(4, Desktops.FrontOf(zOrder, Two));
        Assert.Equal(2, Desktops.FrontOf(zOrder, One));
        // An empty desktop has none: the desktop itself is given it instead.
        Assert.Equal(0, Desktops.FrontOf(zOrder, Three));
    }

    [Fact]
    public void TheDesktopShowingIsTheTopWindowNotCloaked()
    {
        Desktops.Candidate[] zOrder =
        [
            new(1, Eligible: true, Desktop: Two, Hidden: false, Elsewhere: true),
            // On every desktop: Windows names none for it.
            new(2, Eligible: true, Desktop: null, Hidden: false, Elsewhere: false),
            new(3, Eligible: true, Desktop: One, Hidden: false, Elsewhere: false),
        ];

        Assert.Equal(2, Desktops.FrontOf(zOrder, null));
        Assert.Equal(0, Desktops.FrontOf([], null));
    }

    /// <summary>
    /// A desktop chosen in the desktops' line is the folder's act, which goes once the folder has
    /// gone (FolderFlyout.Forwards) — the folder closing without handing the foreground back to the
    /// window it came from, which is on the desktop being left (DesktopsPresetTests).
    /// </summary>
    [Fact]
    public void ADesktopChosenInTheLineIsTheFoldersAct()
    {
        Assert.True(FolderFlyout.Forwards(new IslandAction(View: IslandView.Desktops), CellAct.Action));
        Assert.False(FolderFlyout.Forwards(new IslandAction(View: IslandView.Desktops), CellAct.Nothing));
        Assert.Equal(CellAct.Action, ListWidgets.Of(ListKind.Desktops).Act);
    }
}
