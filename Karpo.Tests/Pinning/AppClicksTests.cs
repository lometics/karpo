// The one rule for a click on an app, wherever it is (AppClicks), and what the keys held and a
// right click add to it, as on Windows 11's taskbar. Decisions, so nothing is started, elevated or
// shown: the dock does what these say (Dock.Pins.cs, Dock.AppClicks.cs).

using Karpo.Dock;

namespace Karpo.Tests.Pinning;

public sealed class AppClicksTests
{
    private static AppWindow Open(nint handle) => new(handle, Minimized: false);

    private static AppWindow Minimized(nint handle) => new(handle, Minimized: true);

    [Fact]
    public void NoWindowStartsIt() =>
        Assert.Equal(new AppClick(AppClickKind.Launch), AppClicks.Decide([], front: 7));

    [Fact]
    public void ANewOneIsAlwaysStarted() =>
        Assert.Equal(AppClickKind.Launch, AppClicks.Decide([Open(1)], front: 1, newInstance: true).Kind);

    /// <summary>Regression: a pin clicked again in a folder brought its window forward instead of minimizing it.</summary>
    [Fact]
    public void TheOneWindowInFrontIsMinimized() =>
        Assert.Equal(new AppClick(AppClickKind.Minimize, 0), AppClicks.Decide([Open(1)], front: 1));

    [Fact]
    public void TheOneWindowBehindOthersComesToTheFront() =>
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([Open(1)], front: 9));

    [Fact]
    public void AMinimizedWindowIsNeverTheOneInFront()
    {
        // Windows can leave a minimized window the foreground one; a click must restore it.
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([Minimized(1)], front: 1));
    }

    [Fact]
    public void NothingInFrontBringsTheWindow() =>
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([Open(1)], front: 0));

    [Fact]
    public void OfSeveralTheMostRecentComesWhenNoneIsInFront() =>
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([Open(1), Open(2), Open(3)], front: 9));

    [Fact]
    public void OfSeveralOneInFrontGoesOnToTheLeastRecent()
    {
        Assert.Equal(new AppClick(AppClickKind.Activate, 2), AppClicks.Decide([Open(1), Open(2), Open(3)], front: 1));
        Assert.Equal(new AppClick(AppClickKind.Activate, 2), AppClicks.Decide([Open(1), Open(2), Open(3)], front: 2));
    }

    /// <summary>Clicking again and again walks through every window, as the order of use changes with each.</summary>
    [Fact]
    public void ClickingAgainWalksThroughEveryWindow()
    {
        var order = new List<nint> { 1, 2, 3 };
        var shown = new List<nint>();
        var front = order[0];
        for (var click = 0; click < 3; click++)
        {
            var decision = AppClicks.Decide([.. order.Select(Open)], front);
            Assert.Equal(AppClickKind.Activate, decision.Kind);
            front = order[decision.Window];
            order.Remove(front);
            order.Insert(0, front);
            shown.Add(front);
        }
        Assert.Equal([3, 2, 1], shown);
    }

    // --- app clicks: the keys held ------------------------------------------------------------

    /// <summary>
    /// Windows 11's taskbar: Shift+click, or a middle click, starts another; Ctrl+Shift+click starts
    /// another as administrator; Ctrl alone walks a button's windows rather than showing their list.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, AppAsk.Usual)]
    [InlineData(false, false, true, AppAsk.Cycle)]
    [InlineData(true, false, false, AppAsk.New)]
    [InlineData(false, true, false, AppAsk.New)]
    [InlineData(true, true, false, AppAsk.New)]
    [InlineData(true, false, true, AppAsk.New)]
    [InlineData(false, true, true, AppAsk.Elevated)]
    [InlineData(true, true, true, AppAsk.Elevated)]
    public void TheKeysHeldSayWhatAClickAsks(bool middle, bool shift, bool control, AppAsk asked) =>
        Assert.Equal(asked, AppClicks.Asked(middle, shift, control));

    /// <summary>Asked for another, the click starts one whatever is open and in front — never as administrator.</summary>
    [Fact]
    public void AnotherIsStartedWhateverIsOpen()
    {
        Assert.Equal(new AppClick(AppClickKind.Launch), AppClicks.Decide([Open(1)], front: 1, AppAsk.New));
        Assert.Equal(new AppClick(AppClickKind.Launch), AppClicks.Decide([Open(1), Open(2)], front: 9, AppAsk.New));
        Assert.Equal(new AppClick(AppClickKind.Launch), AppClicks.Decide([], front: 0, AppAsk.New));
    }

    /// <summary>Ctrl+Shift starts one as administrator, open windows or none; the click says so and the dock does it.</summary>
    [Fact]
    public void CtrlShiftStartsOneAsAdministrator()
    {
        Assert.Equal(new AppClick(AppClickKind.Launch, Elevated: true), AppClicks.Decide([Open(1)], front: 1, AppAsk.Elevated));
        Assert.Equal(new AppClick(AppClickKind.Launch, Elevated: true), AppClicks.Decide([], front: 0, AppAsk.Elevated));
    }

    /// <summary>
    /// Windows 11's default (§362): a plain click on a button of several windows shows their list and
    /// brings none forward, whichever is in front; Ctrl+click walks them, the most recent first, then on
    /// from the one in front. A button of one keeps the one rule, and a new one is still a new one.
    /// </summary>
    [Fact]
    public void APlainClickOnSeveralShowsTheirListAndCtrlWalksThem()
    {
        Assert.Equal(new AppClick(AppClickKind.ShowList), AppClicks.Decide([Open(1), Open(2)], front: 9, AppAsk.Usual, list: true));
        Assert.Equal(new AppClick(AppClickKind.ShowList), AppClicks.Decide([Open(1), Open(2)], front: 1, AppAsk.Usual, list: true));
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([Open(1), Open(2)], front: 9, AppAsk.Cycle, list: true));
        Assert.Equal(new AppClick(AppClickKind.Activate, 1), AppClicks.Decide([Open(1), Open(2)], front: 1, AppAsk.Cycle, list: true));
        Assert.Equal(new AppClick(AppClickKind.Minimize, 0), AppClicks.Decide([Open(1)], front: 1, AppAsk.Usual, list: true));
        Assert.Equal(new AppClick(AppClickKind.Activate, 0), AppClicks.Decide([Minimized(1)], front: 1, AppAsk.Usual, list: true));
        Assert.Equal(new AppClick(AppClickKind.Launch), AppClicks.Decide([], front: 0, AppAsk.Usual, list: true));
        Assert.Equal(new AppClick(AppClickKind.Launch), AppClicks.Decide([Open(1), Open(2)], front: 1, AppAsk.New, list: true));
        // Where no list can be shown (a pin in a folder, Win+N), a plain click walks them as before.
        Assert.Equal(new AppClick(AppClickKind.Activate, 1), AppClicks.Decide([Open(1), Open(2)], front: 1, AppAsk.Usual));
    }

    /// <summary>Windows' own LastActiveClick = 1 has every click on several walk them; unset, or anything else, shows the list.</summary>
    [Theory]
    [InlineData(null, true)]
    [InlineData(0u, true)]
    [InlineData(1u, false)]
    [InlineData(2u, true)]
    public void LastActiveClickSaysWhetherAClickShowsTheList(uint? value, bool lists) =>
        Assert.Equal(lists, AppClicks.Lists(value));

    /// <summary>Asking nothing more is the one rule, exactly as before any key was read.</summary>
    [Fact]
    public void AskingNothingIsTheOneRule()
    {
        List<AppWindow>[] cases = [[], [Open(1)], [Minimized(1)], [Open(1), Open(2), Open(3)]];
        foreach (var windows in cases)
        {
            foreach (var front in new nint[] { 0, 1, 2, 9 })
            {
                Assert.Equal(AppClicks.Decide(windows, front), AppClicks.Decide(windows, front, AppAsk.Usual));
                Assert.False(AppClicks.Decide(windows, front, AppAsk.Usual).Elevated);
            }
        }
    }

    /// <summary>
    /// What can run as administrator: a program, a shortcut, a script, an app of the Apps folder. A
    /// document, a folder or a web address opens as usual instead of asking for nothing.
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\System32\notepad.exe", true)]
    [InlineData(@"C:\Program Files\App\app.EXE", true)]
    [InlineData(@"C:\Users\me\Desktop\Tool.lnk", true)]
    [InlineData(@"C:\tools\build.cmd", true)]
    [InlineData(@"C:\tools\build.bat", true)]
    [InlineData(@"C:\Windows\System32\compmgmt.msc", true)]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", true)]
    [InlineData(@"C:\Users\me\notes.txt", false)]
    [InlineData(@"C:\Users\me\Documents", false)]
    [InlineData("https://example.com/app.exe?x=1", false)]
    [InlineData("", false)]
    public void WhatCanRunAsAdministrator(string target, bool can) =>
        Assert.Equal(can, AppClicks.CanElevate(target));

    /// <summary>
    /// A right click opens the jump list; with Shift, on a button with windows open, the window's own
    /// menu for one and its windows' for several; with none open, Shift changes nothing.
    /// </summary>
    [Theory]
    [InlineData(false, 0, AppMenuKind.JumpList)]
    [InlineData(false, 1, AppMenuKind.JumpList)]
    [InlineData(false, 3, AppMenuKind.JumpList)]
    [InlineData(true, 0, AppMenuKind.JumpList)]
    [InlineData(true, 1, AppMenuKind.Window)]
    [InlineData(true, 2, AppMenuKind.Windows)]
    [InlineData(true, 7, AppMenuKind.Windows)]
    public void WhichMenuARightClickOpens(bool shift, int windows, AppMenuKind menu) =>
        Assert.Equal(menu, AppClicks.MenuFor(shift, windows));
}
