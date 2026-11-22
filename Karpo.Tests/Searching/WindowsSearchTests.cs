// search: Windows' own search button on the bar (backlog 34, 36 and 37).
//
//   * a click on Search while Windows Search is open closes it rather than opening it again, by
//     what Search was as the button went down and what it is as the click lands (SearchToggle);
//     the Start button the same over Start, a panel the press sent away before it was read
//     included (PanelWatch, 2026-10-10);
//   * every flyout of Karpo's closes Windows Search and Windows' Start as it opens
//     (WindowsPanels.CloseFor), and the notifications, quick settings and the quick link menu where
//     one is in front (§466), whose buttons close them on a second click as Start's does;
//   * the button may be Windows 11's search box (WindowsSearchOptions), which the bar lays out as
//     the box, five cells long, and lights up as a pill (Layout.SearchBoxRect).
//
// Nothing here reads or touches the person's real Search or Start: the panels are a fake.

using System.Text.Json;
using Karpo.Dock;
using TerraFX.Interop.Windows;

namespace Karpo.Tests.Searching;

public sealed class WindowsSearchTests
{
    private static readonly HWND SearchWindow = (HWND)(nint)0x5EA;
    private static readonly HWND StartWindow = (HWND)(nint)0x57A;
    private static readonly HWND SomeApp = (HWND)(nint)0xA99;
    private static readonly HWND MenuWindow = (HWND)(nint)0x3E7;

    /// <summary>The message time a test's press happens at.</summary>
    private const uint Press = 1_000_000;

    /// <summary>
    /// Windows' panels as a test says they are; a dismiss is recorded, and closes the panel where it
    /// would. When a panel went is the real bookkeeping (<see cref="Watch"/>), fed by the test.
    /// </summary>
    private sealed class Panels : IWindowsPanels
    {
        public bool SearchOpen { get; set; }

        public bool StartOpen { get; set; }

        /// <summary>windowsMenus: the one of Windows' menus in front (§466), which is open while it is; null for none.</summary>
        public WindowsPanel? MenuOpen { get; set; }

        public PanelWatch Watch { get; } = new();

        public bool WentSince(WindowsPanel panel, uint tick) => Watch.WentSince(panel, tick);

        /// <summary>Whether a dismiss closes the panel; false is a panel that could not be asked to.</summary>
        public bool Closes { get; set; } = true;

        public List<WindowsPanel> Dismissed { get; } = [];

        public int Reads { get; private set; }

        public bool IsOpen(WindowsPanel panel) => Window(panel) is not null;

        public HWND? Window(WindowsPanel panel)
        {
            Reads++;
            return panel switch
            {
                WindowsPanel.Search when SearchOpen => SearchWindow,
                WindowsPanel.Start when StartOpen => StartWindow,
                _ when panel == MenuOpen => MenuWindow,
                _ => null,
            };
        }

        public bool Owns(HWND window) => window == SearchWindow || window == StartWindow || window == MenuWindow;

        /// <summary>§490: what the real panels' dismiss sends (the Escape), where one is open to be sent it.</summary>
        public Action? Sent { get; init; }

        public bool Dismiss(WindowsPanel panel)
        {
            Dismissed.Add(panel);
            if (!Closes || !IsOpen(panel))
            {
                return false;
            }
            Sent?.Invoke();
            if (panel == WindowsPanel.Search)
            {
                SearchOpen = false;
            }
            else if (panel == WindowsPanel.Start)
            {
                StartOpen = false;
            }
            else
            {
                MenuOpen = null;
            }
            return true;
        }
    }

    // --- 36: the Search button's click -------------------------------------------------

    /// <summary>The whole rule: open unless it was open as the button went down; then close it if it still is.</summary>
    [Theory]
    [InlineData(false, false, SearchClick.Open)]
    [InlineData(false, true, SearchClick.Open)]
    [InlineData(true, true, SearchClick.Close)]
    [InlineData(true, false, SearchClick.Nothing)]
    public void WhatAClickDoes(bool openAtPress, bool openNow, SearchClick does) =>
        Assert.Equal(does, SearchToggle.Decide(openAtPress, openNow));

    /// <summary>Search closed: a press and its click open it, as they always did.</summary>
    [Fact]
    public void AClickOpensSearchWhenItIsClosed()
    {
        var panels = new Panels();
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Open, toggle.Clicked());
    }

    /// <summary>Search open, and the press doesn't close it: the click does.</summary>
    [Fact]
    public void AClickClosesSearchWhenItIsOpen()
    {
        var panels = new Panels { SearchOpen = true };
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Close, toggle.Clicked());
    }

    /// <summary>
    /// Search open, and the press itself closes it before the click lands: the click must not open
    /// it again — the fault the person saw, Search gone and straight back.
    /// </summary>
    [Fact]
    public void APressThatClosedSearchIsNotFollowedByOpeningIt()
    {
        var panels = new Panels { SearchOpen = true };
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: true, Press);
        panels.SearchOpen = false;
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
    }

    /// <summary>Search opened between the press and the click (Win+S on the keyboard): the press's reading decides, so it opens.</summary>
    [Fact]
    public void ThePressDecidesNotTheClick()
    {
        var panels = new Panels();
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: true, Press);
        panels.SearchOpen = true;
        Assert.Equal(SearchClick.Open, toggle.Clicked());
    }

    /// <summary>A click with no press — the keyboard's, Narrator's — reads Search as it is then.</summary>
    [Theory]
    [InlineData(false, SearchClick.Open)]
    [InlineData(true, SearchClick.Close)]
    public void AClickWithoutAPressReadsSearchAsItIs(bool open, SearchClick does)
    {
        var toggle = new SearchToggle(new Panels { SearchOpen = open });
        Assert.Equal(does, toggle.Clicked());
    }

    /// <summary>
    /// A press is used once: the click after it consumes it, and a press anywhere else on the bar
    /// forgets one that never got its click, so a later click is never decided by an old press.
    /// </summary>
    [Fact]
    public void APressIsUsedOnce()
    {
        var panels = new Panels { SearchOpen = true };
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: true, Press);
        panels.SearchOpen = false;
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
        // The next click comes with no press of its own: Search is closed, so it opens.
        Assert.Equal(SearchClick.Open, toggle.Clicked());

        panels.SearchOpen = true;
        toggle.Pressed(onSearch: true, Press);
        // Released somewhere else; then a press on another widget.
        toggle.Pressed(onSearch: false, Press);
        panels.SearchOpen = false;
        Assert.Equal(SearchClick.Open, toggle.Clicked());
    }

    /// <summary>Only a press on Search reads Windows' windows; a press anywhere else costs nothing.</summary>
    [Fact]
    public void APressElsewhereReadsNothing()
    {
        var panels = new Panels { SearchOpen = true };
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: false, Press);
        Assert.Equal(0, panels.Reads);
    }

    // --- The Start button's click (2026-10-10) -----------------------------------------

    /// <summary>A toggle over Start, with Start open and in front since well before the press.</summary>
    private static (Panels Panels, SearchToggle Toggle) StartOpenInFront()
    {
        var panels = new Panels { StartOpen = true };
        panels.Watch.Foreground(WindowsPanel.Start, Press - 5_000);
        return (panels, new SearchToggle(panels, WindowsPanel.Start));
    }

    /// <summary>Start closed: a press on the Start button and its click open it.</summary>
    [Fact]
    public void AClickOpensStartWhenItIsClosed()
    {
        var panels = new Panels();
        // Opened and closed again a while ago: long before the press, so no part of this click.
        panels.Watch.Foreground(WindowsPanel.Start, Press - 9_000);
        panels.Watch.Foreground(null, Press - 4_000);
        var toggle = new SearchToggle(panels, WindowsPanel.Start);
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Open, toggle.Clicked());
    }

    /// <summary>Start open, and the press leaves it be: the click closes it.</summary>
    [Fact]
    public void AClickClosesStartWhenItIsOpen()
    {
        var (_, toggle) = StartOpenInFront();
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Close, toggle.Clicked());
    }

    /// <summary>
    /// Start open on Windows 11 26xxx, its search box (Search's host) in front the whole time: the
    /// press on the bar leaves both be, and the click closes Start (2026-10-11, seen on 26300). The
    /// search box losing the foreground as Start goes is Search's going, which is no click's on Start.
    /// </summary>
    [Fact]
    public void AClickClosesStartWithItsSearchBoxInFront()
    {
        var panels = new Panels { StartOpen = true };
        panels.Watch.Foreground(WindowsPanel.Search, Press - 3_000);
        var toggle = new SearchToggle(panels, WindowsPanel.Start);
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Close, toggle.Clicked());

        // Closed by the click: the next one opens it.
        panels.StartOpen = false;
        panels.Watch.Foreground(null, Press + 300);
        panels.Watch.Cloaked(WindowsPanel.Start, Press + 330);
        toggle.Pressed(onSearch: true, Press + 2_000);
        Assert.Equal(SearchClick.Open, toggle.Clicked());
    }

    /// <summary>
    /// The fault the person saw: Start open, and the shell sends it away as the pointer goes down —
    /// gone already when the dock reads the press. It went after the press, so it was open at the
    /// press, and the click must not open it again.
    /// </summary>
    [Fact]
    public void APressThatSentStartAwayBeforeItWasReadDoesNotOpenItAgain()
    {
        var (panels, toggle) = StartOpenInFront();
        panels.Watch.Foreground(null, Press + 8);
        panels.StartOpen = false;
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
    }

    /// <summary>The same where Start was cloaked rather than losing the foreground first.</summary>
    [Fact]
    public void AStartCloakedByThePressCountsAsOpenAtThePress()
    {
        var (panels, toggle) = StartOpenInFront();
        panels.Watch.Cloaked(WindowsPanel.Start, Press + 3);
        panels.StartOpen = false;
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
    }

    /// <summary>
    /// Start open at the press, which sent it away; as the click lands it still shows, on its way
    /// out (not cloaked yet): it is not closed a second time — nor handed the foreground back,
    /// which could bring it back — and nothing is opened.
    /// </summary>
    [Fact]
    public void AStartOnItsWayOutIsLeftToGo()
    {
        var (panels, toggle) = StartOpenInFront();
        toggle.Pressed(onSearch: true, Press);
        panels.Watch.Foreground(null, Press + 20);
        Assert.True(panels.StartOpen);
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
    }

    /// <summary>
    /// The press's time and the event's are stamped apart, so a going a tick before the press is
    /// still the press's; one well before it was something else's, and the click opens Start.
    /// </summary>
    [Theory]
    [InlineData(30u, SearchClick.Nothing)]
    [InlineData(SearchToggle.SlackMs, SearchClick.Nothing)]
    [InlineData(SearchToggle.SlackMs + 1, SearchClick.Open)]
    [InlineData(400u, SearchClick.Open)]
    public void AGoingJustBeforeThePressIsThePresss(uint before, SearchClick does)
    {
        var (panels, toggle) = StartOpenInFront();
        panels.Watch.Foreground(null, Press - before);
        panels.StartOpen = false;
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(does, toggle.Clicked());
    }

    /// <summary>Search is the same: sent away by the press before it was read, it is not opened again.</summary>
    [Fact]
    public void APressThatSentSearchAwayBeforeItWasReadDoesNotOpenItAgain()
    {
        var panels = new Panels();
        panels.Watch.Foreground(WindowsPanel.Search, Press - 2_000);
        panels.Watch.Foreground(null, Press + 5);
        var toggle = new SearchToggle(panels);
        toggle.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
    }

    /// <summary>Start going has nothing to say about a click on Search, nor the other way round.</summary>
    [Fact]
    public void OnePanelsGoingIsNotTheOthers()
    {
        var panels = new Panels();
        panels.Watch.Foreground(WindowsPanel.Start, Press - 2_000);
        panels.Watch.Foreground(null, Press + 5);
        var search = new SearchToggle(panels);
        search.Pressed(onSearch: true, Press);
        Assert.Equal(SearchClick.Open, search.Clicked());
    }

    /// <summary>The keyboard's click (no press) reads Start as it is: a going it has no press to set against.</summary>
    [Fact]
    public void AClickWithoutAPressIgnoresWhenStartWent()
    {
        var (panels, toggle) = StartOpenInFront();
        panels.Watch.Foreground(null, Press + 5);
        panels.StartOpen = false;
        Assert.Equal(SearchClick.Open, toggle.Clicked());
    }

    // --- When a panel went (PanelWatch) ------------------------------------------------

    /// <summary>A panel goes when the foreground leaves it, for another window or the other panel; not when it moves between other windows.</summary>
    [Fact]
    public void APanelGoesWhenTheForegroundLeavesIt()
    {
        var watch = new PanelWatch();
        watch.Foreground(null, 100);
        watch.Foreground(null, 200);
        Assert.False(watch.WentSince(WindowsPanel.Start, 0));
        Assert.False(watch.WentSince(WindowsPanel.Search, 0));

        watch.Foreground(WindowsPanel.Start, 300);
        Assert.False(watch.WentSince(WindowsPanel.Start, 0));
        // Again in front of itself (a second foreground event): still there.
        watch.Foreground(WindowsPanel.Start, 350);
        Assert.False(watch.WentSince(WindowsPanel.Start, 0));

        watch.Foreground(WindowsPanel.Search, 400);
        Assert.True(watch.WentSince(WindowsPanel.Start, 400));
        Assert.False(watch.WentSince(WindowsPanel.Start, 401));
        Assert.False(watch.WentSince(WindowsPanel.Search, 0));

        watch.Foreground(null, 500);
        Assert.True(watch.WentSince(WindowsPanel.Search, 500));
    }

    /// <summary>A panel goes when it is cloaked, in front or not; a cloak of any other window says nothing.</summary>
    [Fact]
    public void APanelGoesWhenItIsCloaked()
    {
        var watch = new PanelWatch();
        watch.Cloaked(null, 100);
        Assert.False(watch.WentSince(WindowsPanel.Start, 0));

        watch.Foreground(WindowsPanel.Start, 200);
        watch.Cloaked(WindowsPanel.Start, 300);
        Assert.True(watch.WentSince(WindowsPanel.Start, 300));
        // The foreground then moves on from a Start already gone: that is no second going.
        watch.Foreground(null, 900);
        Assert.False(watch.WentSince(WindowsPanel.Start, 301));

        watch.Cloaked(WindowsPanel.Search, 1_000);
        Assert.True(watch.WentSince(WindowsPanel.Search, 1_000));
    }

    /// <summary>
    /// The clock wraps every 49.7 days: a going just after the wrap is since a press just before it,
    /// and one from weeks before is not since a press now.
    /// </summary>
    [Fact]
    public void TheClockWrapping()
    {
        var watch = new PanelWatch();
        watch.Foreground(WindowsPanel.Start, uint.MaxValue - 100);
        watch.Foreground(null, 20);
        Assert.True(watch.WentSince(WindowsPanel.Start, uint.MaxValue - 30));
        Assert.False(watch.WentSince(WindowsPanel.Start, 21));

        var old = new PanelWatch();
        old.Foreground(WindowsPanel.Start, 0);
        old.Foreground(null, 1_000);
        // Thirty days on: the subtraction wraps to a number under 2^31, which a plain signed
        // comparison would read as a going to come.
        Assert.False(old.WentSince(WindowsPanel.Start, 1_000 + (30u * 24 * 3600 * 1000)));
        Assert.True(old.WentSince(WindowsPanel.Start, unchecked(1_000 - PanelWatch.RecentMs)));
        Assert.False(old.WentSince(WindowsPanel.Start, unchecked(1_000 - PanelWatch.RecentMs - 1)));
    }

    // --- 37: every flyout closes Search and Start --------------------------------------

    /// <summary>Nothing of Windows' open: nothing is dismissed, and the focus may go back where it was.</summary>
    [Fact]
    public void AFlyoutOverNothingClosesNothing()
    {
        var panels = new Panels();
        Assert.False(WindowsPanels.CloseFor(panels, SomeApp));
        Assert.Empty(panels.Dismissed);
    }

    /// <summary>Search open in front: it is dismissed, and the flyout won't hand the focus back to it.</summary>
    [Fact]
    public void AFlyoutClosesSearch()
    {
        var panels = new Panels { SearchOpen = true };
        Assert.True(WindowsPanels.CloseFor(panels, SearchWindow));
        Assert.Equal([WindowsPanel.Search], panels.Dismissed);
        Assert.False(panels.SearchOpen);
    }

    /// <summary>Start open in front: the same.</summary>
    [Fact]
    public void AFlyoutClosesStart()
    {
        var panels = new Panels { StartOpen = true };
        Assert.True(WindowsPanels.CloseFor(panels, StartWindow));
        Assert.Equal([WindowsPanel.Start], panels.Dismissed);
        Assert.False(panels.StartOpen);
    }

    /// <summary>
    /// Start open on Windows 11 26xxx: its search box, Search's host window, has the foreground. Start
    /// is dismissed, not Search, and the flyout won't hand the focus back to the search box.
    /// </summary>
    [Fact]
    public void AFlyoutOverStartsSearchBoxClosesStart()
    {
        var panels = new Panels { StartOpen = true };
        Assert.True(WindowsPanels.CloseFor(panels, SearchWindow));
        Assert.Equal([WindowsPanel.Start], panels.Dismissed);
        Assert.False(panels.StartOpen);
    }

    /// <summary>
    /// A panel open but not in front is still dismissed, and the focus may go back to what was in
    /// front, which is not the panel.
    /// </summary>
    [Fact]
    public void APanelBehindSomethingIsClosedAndTheFocusStays()
    {
        var panels = new Panels { SearchOpen = true, StartOpen = true };
        Assert.False(WindowsPanels.CloseFor(panels, SomeApp));
        Assert.Equal([WindowsPanel.Search, WindowsPanel.Start], panels.Dismissed);
    }

    /// <summary>
    /// windowsMenus: the notifications, quick settings or the quick link menu in front (§466): it is
    /// closed, and the flyout won't hand the focus back to it — one thing open at a time.
    /// </summary>
    [Theory]
    [InlineData(WindowsPanel.Notifications)]
    [InlineData(WindowsPanel.QuickSettings)]
    [InlineData(WindowsPanel.ExplorerMenu)]
    [InlineData(WindowsPanel.WidgetsBoard)]
    public void AFlyoutClosesWindowsMenuInFront(WindowsPanel menu)
    {
        var panels = new Panels { MenuOpen = menu };
        Assert.True(WindowsPanels.CloseFor(panels, MenuWindow));
        Assert.Equal([menu], panels.Dismissed);
        Assert.Null(panels.MenuOpen);
    }

    /// <summary>
    /// §490: the add list opening in edit mode over Windows' menu in front (the person, 2026-11-02: "when a windows menu
    /// was open i was not able to add a new taskbar widget as it always closed the add dialog"). The menu is closed by
    /// the Escape Karpo sends it, which edit mode's keyboard hook hears as every key; it is Karpo's own, so the hook
    /// lets it by to the menu and edit mode — and the add list with it — stays. A person's Escape still leaves edit
    /// mode. Stand-ins: the panels, and the gesture the hook feeds; no key is sent.
    /// </summary>
    [Theory]
    [InlineData(WindowsPanel.Notifications)]
    [InlineData(WindowsPanel.QuickSettings)]
    [InlineData(WindowsPanel.Start)]
    public void TheEscapeThatClosesWindowsMenuForAFlyoutIsNotThePersons(WindowsPanel menu)
    {
        var gesture = new FollowGesture { Editing = true };
        var heard = new List<(bool Keep, FollowInput Input)>();
        var panels = new Panels
        {
            MenuOpen = menu is WindowsPanel.Start ? null : menu,
            StartOpen = menu is WindowsPanel.Start,
            Sent = () =>
            {
                foreach (var tap in WindowsPanels.EscapeTaps())
                {
                    Assert.Equal(VK.VK_ESCAPE, tap.ki.wVk);
                    Assert.Equal(TaskbarKeys.Ours, tap.ki.dwExtraInfo);
                    var up = (tap.ki.dwFlags & (uint)TerraFX.Interop.Windows.Windows.KEYEVENTF_KEYUP) != 0;
                    heard.Add(gesture.Key((uint)(up ? WM.WM_KEYUP : WM.WM_KEYDOWN), tap.ki.wVk, theirs: () => false, ours: tap.ki.dwExtraInfo == TaskbarKeys.Ours));
                }
            },
        };
        Assert.True(WindowsPanels.CloseFor(panels, menu is WindowsPanel.Start ? StartWindow : MenuWindow));
        Assert.Equal([menu], panels.Dismissed);
        Assert.Equal([(false, FollowInput.None), (false, FollowInput.None)], heard);
        Assert.False(gesture.Owes);
        // The person's own Escape is edit mode's, as before.
        Assert.Equal((true, FollowInput.Leave), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE, theirs: () => false));
    }

    /// <summary>
    /// §490: edit mode coming in closes every one of Windows' menus open (the person, 2026-11-02: "when entering edit mode
    /// it should therefore just out of default close all the windows menus open"), as a flyout of Karpo's does — Search,
    /// Start, and the one in front —, and the keys go back at its end to none of them; over an app nothing is closed, and
    /// the keys go back to the app.
    /// </summary>
    [Theory]
    [InlineData(WindowsPanel.Notifications)]
    [InlineData(WindowsPanel.QuickSettings)]
    [InlineData(WindowsPanel.ExplorerMenu)]
    [InlineData(WindowsPanel.WidgetsBoard)]
    public void EditModeComingInClosesWindowsMenus(WindowsPanel menu)
    {
        var panels = new Panels { MenuOpen = menu, SearchOpen = true };
        Assert.Equal(HWND.NULL, WindowsPanels.ForEditMode(panels, MenuWindow));
        Assert.Equal([WindowsPanel.Search, menu], panels.Dismissed);
        Assert.Null(panels.MenuOpen);
        Assert.False(panels.SearchOpen);

        var none = new Panels();
        Assert.Equal(SomeApp, WindowsPanels.ForEditMode(none, SomeApp));
        Assert.Empty(none.Dismissed);
    }

    /// <summary>
    /// §490: Karpo's own Escape is let by whatever edit mode is doing — a widget following the pointer is not called off
    /// by it, and an undo's chord is not touched — so the keys go on to Windows' menu, never to Karpo.
    /// </summary>
    [Fact]
    public void KarposOwnEscapeIsNeverEditModes()
    {
        var gesture = new FollowGesture { Editing = true, Following = true };
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE, ours: true));
        Assert.Equal((false, FollowInput.None), gesture.Key(WM.WM_KEYUP, VK.VK_ESCAPE, ours: true));
        Assert.False(gesture.Owes);
        Assert.Equal((true, FollowInput.Cancel), gesture.Key(WM.WM_KEYDOWN, VK.VK_ESCAPE));
    }

    /// <summary>A panel that could not be asked to close leaves the flyout to open all the same.</summary>
    [Fact]
    public void APanelThatWontCloseDoesNotStopTheFlyout()
    {
        var panels = new Panels { SearchOpen = true, Closes = false };
        Assert.True(WindowsPanels.CloseFor(panels, SearchWindow));
        Assert.Equal([WindowsPanel.Search], panels.Dismissed);
    }

    // --- Which window is which panel ---------------------------------------------------

    /// <summary>
    /// Start is open where its own window shows; Search where its window shows and Start's does not:
    /// on Windows 11 26xxx Search's window over an open Start is Start's search box (2026-10-11), so
    /// a click on the Start button reads Start open, and one on Search reads Search closed and opens it.
    /// </summary>
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, false, true)]
    public void WhichPanelIsOpen(bool searchShows, bool startShows, bool search, bool start)
    {
        Assert.Equal(search, PanelRules.Open(WindowsPanel.Search, searchShows, startShows));
        Assert.Equal(start, PanelRules.Open(WindowsPanel.Start, searchShows, startShows));
    }

    /// <summary>
    /// A window is a panel by its class and the process it belongs to, whatever the path's case;
    /// nothing else is. The classes are Windows 11 26300's (2026-11-01, §466).
    /// </summary>
    [Theory]
    [InlineData(@"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe", WindowsPanel.Search)]
    [InlineData(@"C:\WINDOWS\SYSTEMAPPS\MICROSOFTWINDOWS.CLIENT.CBS_CW5N1H2TXYEWY\SEARCHHOST.EXE", WindowsPanel.Search)]
    [InlineData(@"C:\Windows\SystemApps\Microsoft.Windows.Search_cw5n1h2txyewy\SearchApp.exe", WindowsPanel.Search)]
    [InlineData(@"C:\Windows\SystemApps\Microsoft.Windows.Cortana_cw5n1h2txyewy\SearchUI.exe", WindowsPanel.Search)]
    [InlineData(@"C:\Windows\SystemApps\Microsoft.Windows.StartMenuExperienceHost_cw5n1h2txyewy\StartMenuExperienceHost.exe", WindowsPanel.Start)]
    [InlineData(@"C:\Windows\explorer.exe", null)]
    [InlineData(@"C:\Windows\ImmersiveControlPanel\SystemSettings.exe", null)]
    [InlineData(@"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\TextInputHost.exe", null)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void WhichPanelAWindowIs(string? image, WindowsPanel? panel) =>
        Assert.Equal(panel, WindowsPanels.PanelOf(image, "Windows.UI.Core.CoreWindow"));

    /// <summary>windowsMenus: the notifications, quick settings and the quick link menu, by class and process (§466).</summary>
    [Theory]
    [InlineData(@"C:\Windows\SystemApps\ShellExperienceHost_cw5n1h2txyewy\ShellExperienceHost.exe", "Windows.UI.Core.CoreWindow", WindowsPanel.Notifications)]
    [InlineData(@"C:\Windows\System32\ShellHost.exe", "ControlCenterWindow", WindowsPanel.QuickSettings)]
    [InlineData(@"C:\WINDOWS\EXPLORER.EXE", "Xaml_WindowedPopupClass", WindowsPanel.ExplorerMenu)]
    // §473: the Widgets board, WidgetBoard.exe's WindowsDashboard (26300); its other windows (the IME's) are none.
    [InlineData(@"C:\Program Files\WindowsApps\MicrosoftWindows.Client.WebExperience_526.21100.40.0_x64__cw5n1h2txyewy\WidgetBoard.exe", "WindowsDashboard", WindowsPanel.WidgetsBoard)]
    [InlineData(@"C:\Program Files\WindowsApps\MicrosoftWindows.Client.WebExperience_526.21100.40.0_x64__cw5n1h2txyewy\WidgetBoard.exe", "IME", null)]
    [InlineData(@"C:\Program Files\App\App.exe", "WindowsDashboard", null)]
    // The class without the process, or the process without the class, is no panel: Explorer's own
    // windows, a CoreWindow of ShellHost's, ShellExperienceHost's other classes.
    [InlineData(@"C:\Windows\explorer.exe", "Shell_TrayWnd", null)]
    [InlineData(@"C:\Windows\System32\ShellHost.exe", "Windows.UI.Core.CoreWindow", null)]
    [InlineData(@"C:\Windows\SystemApps\ShellExperienceHost_cw5n1h2txyewy\ShellExperienceHost.exe", "ControlCenterWindow", null)]
    [InlineData(@"C:\Windows\SystemApps\MicrosoftWindows.Client.CBS_cw5n1h2txyewy\SearchHost.exe", "Xaml_WindowedPopupClass", null)]
    [InlineData(@"C:\Program Files\App\App.exe", "ControlCenterWindow", null)]
    public void WhichOfWindowsMenusAWindowIs(string? image, string type, WindowsPanel? panel) =>
        Assert.Equal(panel, WindowsPanels.PanelOf(image, type));

    /// <summary>
    /// windowsMenus: the panel each button opens, whose second click closes it (§466): quick settings'
    /// four pages are one panel; the clipboard's and the emoji panel's never take the foreground and
    /// are none.
    /// </summary>
    [Theory]
    [InlineData(BarKind.QuickLink, WindowsPanel.ExplorerMenu)]
    [InlineData(BarKind.WindowsNotifications, WindowsPanel.Notifications)]
    [InlineData(BarKind.WindowsQuickSettings, WindowsPanel.QuickSettings)]
    [InlineData(BarKind.WindowsSound, WindowsPanel.QuickSettings)]
    [InlineData(BarKind.WindowsNetworks, WindowsPanel.QuickSettings)]
    [InlineData(BarKind.WindowsCast, WindowsPanel.QuickSettings)]
    [InlineData(BarKind.WindowsClipboard, null)]
    [InlineData(BarKind.EmojiPanel, null)]
    [InlineData(BarKind.WidgetsBoard, WindowsPanel.WidgetsBoard)]
    [InlineData(BarKind.PenMenu, null)]
    public void WhichPanelAButtonOpens(BarKind kind, WindowsPanel? panel) =>
        Assert.Equal(panel, WindowsPanels.PanelOf(kind));

    // --- §474: Windows' own widgets, wearing Windows' faces, toggle as their buttons --------------

    /// <summary>
    /// A click on the clock opens the notifications (Win+N, its target); a second click while they are open closes them by
    /// Escape — Win+N is not sent again, so nothing reopens — and the same for quick settings' button and the Widgets
    /// button (§466's toggle, §473's second-click question). Nothing real is pressed: the panels are a stand-in.
    /// </summary>
    [Theory]
    [InlineData(BarKind.WindowsNotifications, WindowsPanel.Notifications)]
    [InlineData(BarKind.WindowsQuickSettings, WindowsPanel.QuickSettings)]
    [InlineData(BarKind.WidgetsBoard, WindowsPanel.WidgetsBoard)]
    public void AWindowsFacesSecondClickClosesTheMenuItOpened(BarKind button, WindowsPanel panel)
    {
        Assert.True(button.WearsWindowsFace());
        var panels = new Panels();
        var menus = new WindowsMenus(panels);
        menus.Pressed(button, Press);
        Assert.False(menus.Clicked(button));
        // Its shortcut opened the menu, which took the foreground.
        panels.MenuOpen = panel;
        panels.Watch.Foreground(panel, Press + 100);

        menus.Pressed(button, Press + 2_000);
        Assert.True(menus.Clicked(button));
        Assert.Equal([panel], panels.Dismissed);
        Assert.Null(panels.MenuOpen);
    }

    /// <summary>
    /// A press on the clock that sent the notifications away before it was read leaves them closed: not reopened — the
    /// notifications opened from the keyboard (Win+N), not by the bar, their one button's page all the same.
    /// </summary>
    [Fact]
    public void AWindowsFacesPressThatClosedItsMenuLeavesItClosed()
    {
        var panels = new Panels { MenuOpen = WindowsPanel.Notifications };
        var menus = new WindowsMenus(panels);
        panels.Watch.Foreground(WindowsPanel.Notifications, Press - 500);
        menus.Pressed(BarKind.WindowsNotifications, Press);
        panels.MenuOpen = null;
        panels.Watch.Foreground(null, Press + 5);
        Assert.True(menus.Clicked(BarKind.WindowsNotifications));
        Assert.Empty(panels.Dismissed);
    }

    /// <summary>Quick settings is the one panel of several buttons (its pages); every other is one button's.</summary>
    [Theory]
    [InlineData(WindowsPanel.Notifications, true)]
    [InlineData(WindowsPanel.ExplorerMenu, true)]
    [InlineData(WindowsPanel.WidgetsBoard, true)]
    [InlineData(WindowsPanel.QuickSettings, false)]
    public void WhichPanelsAreOneButtons(WindowsPanel panel, bool one) =>
        Assert.Equal(one, WindowsMenus.OneButton(panel));

    /// <summary>
    /// Quick settings opened from the keyboard (Win+A): its page is not known, so the Sound button goes to its page over it
    /// as before (§466), rather than closing it.
    /// </summary>
    [Fact]
    public void QuickSettingsFromTheKeyboardGoesToTheSoundPage()
    {
        var panels = new Panels { MenuOpen = WindowsPanel.QuickSettings };
        var menus = new WindowsMenus(panels);
        menus.Pressed(BarKind.WindowsSound, Press);
        Assert.False(menus.Clicked(BarKind.WindowsSound));
        Assert.Empty(panels.Dismissed);
    }

    /// <summary>
    /// Quick settings' button, wearing the network, the volume and the battery, and its pages' glyph buttons are one toggle:
    /// the Sound button over quick settings it opened goes to its page, a second Sound click closes it; a press elsewhere
    /// reads nothing, and a click with no press of its own (the keyboard's) reads the menu as it is now.
    /// </summary>
    [Fact]
    public void QuickSettingsButtonAndItsPagesAreOneToggle()
    {
        var panels = new Panels();
        var menus = new WindowsMenus(panels);
        menus.Pressed(BarKind.WindowsQuickSettings, Press);
        Assert.False(menus.Clicked(BarKind.WindowsQuickSettings));
        panels.MenuOpen = WindowsPanel.QuickSettings;

        menus.Pressed(BarKind.WindowsSound, Press + 1_000);
        Assert.False(menus.Clicked(BarKind.WindowsSound));
        Assert.Empty(panels.Dismissed);

        menus.Pressed(BarKind.WindowsSound, Press + 2_000);
        Assert.True(menus.Clicked(BarKind.WindowsSound));
        Assert.Equal([WindowsPanel.QuickSettings], panels.Dismissed);

        menus.Pressed(null, Press + 3_000);
        Assert.False(menus.Clicked(BarKind.WindowsQuickSettings));
    }

    /// <summary>
    /// §474: the button of the menu in front is lit while it is, as Windows lights its own: the one that opened it, else the
    /// one wearing Windows' face for it (quick settings' over its pages'), else its first; none for nothing in front, a panel
    /// none of the bar's buttons opens, or Start.
    /// </summary>
    [Fact]
    public void TheButtonOfTheMenuInFrontIsLit()
    {
        BarKind[] bar = [BarKind.Start, BarKind.WindowsSound, BarKind.WidgetsBoard, BarKind.WindowsQuickSettings, BarKind.WindowsNotifications, BarKind.FolderList];
        Assert.Equal(-1, WindowsMenus.Lit(null, null, bar));
        Assert.Equal(4, WindowsMenus.Lit(WindowsPanel.Notifications, null, bar));
        Assert.Equal(2, WindowsMenus.Lit(WindowsPanel.WidgetsBoard, null, bar));
        Assert.Equal(3, WindowsMenus.Lit(WindowsPanel.QuickSettings, null, bar));
        Assert.Equal(1, WindowsMenus.Lit(WindowsPanel.QuickSettings, BarKind.WindowsSound, bar));
        Assert.Equal(3, WindowsMenus.Lit(WindowsPanel.QuickSettings, BarKind.WindowsCast, bar));
        Assert.Equal(-1, WindowsMenus.Lit(WindowsPanel.ExplorerMenu, null, bar));
        Assert.Equal(-1, WindowsMenus.Lit(WindowsPanel.Start, null, bar));
        Assert.Equal(-1, WindowsMenus.Lit(WindowsPanel.Notifications, null, [BarKind.FolderList]));

        // The toggle's own record: the button that opened the panel is the one lit.
        var panels = new Panels();
        var menus = new WindowsMenus(panels);
        menus.Pressed(BarKind.WindowsSound, Press);
        Assert.False(menus.Clicked(BarKind.WindowsSound));
        Assert.Equal(1, menus.Lit(WindowsPanel.QuickSettings, bar));
    }

    /// <summary>§474: the panel in front, by the foreground hook's events: a change is said once, the same again is none.</summary>
    [Fact]
    public void ThePanelInFrontIsFollowed()
    {
        var watch = new PanelWatch();
        Assert.Null(watch.Front);
        Assert.True(watch.Foreground(WindowsPanel.QuickSettings, Press));
        Assert.Equal(WindowsPanel.QuickSettings, watch.Front);
        Assert.False(watch.Foreground(WindowsPanel.QuickSettings, Press + 10));
        Assert.False(watch.Cloaked(WindowsPanel.Notifications, Press + 20));
        Assert.True(watch.Cloaked(WindowsPanel.QuickSettings, Press + 30));
        Assert.Null(watch.Front);
        Assert.True(watch.Foreground(WindowsPanel.Notifications, Press + 40));
        Assert.True(watch.Foreground(null, Press + 50));
        Assert.True(watch.WentSince(WindowsPanel.Notifications, Press + 45));
    }
    /// <summary>
    /// windowsMenus: quick settings open on the sound page: the sound button closes it, as Start's
    /// does; the cast button goes to the cast page over it instead (§466).
    /// </summary>
    [Theory]
    [InlineData(SearchClick.Close, true, SearchClick.Close)]
    [InlineData(SearchClick.Nothing, true, SearchClick.Nothing)]
    [InlineData(SearchClick.Open, true, SearchClick.Open)]
    [InlineData(SearchClick.Close, false, SearchClick.Open)]
    [InlineData(SearchClick.Nothing, false, SearchClick.Open)]
    [InlineData(SearchClick.Open, false, SearchClick.Open)]
    public void AnotherPageOfTheSamePanelOpens(SearchClick click, bool samePage, SearchClick does) =>
        Assert.Equal(does, WindowsPanels.ForPage(click, samePage));

    /// <summary>
    /// windowsMenus: a menu's going is noted as Start's is (§466), so a press that sent quick settings
    /// away reads as having closed it, and the click does nothing.
    /// </summary>
    [Fact]
    public void APressThatClosedQuickSettingsLeavesItClosed()
    {
        var panels = new Panels { MenuOpen = WindowsPanel.QuickSettings };
        var toggle = new SearchToggle(panels, WindowsPanel.QuickSettings);
        panels.Watch.Foreground(WindowsPanel.QuickSettings, Press - 500);
        toggle.Pressed(true, Press);
        panels.MenuOpen = null;
        panels.Watch.Foreground(null, Press + 5);
        Assert.Equal(SearchClick.Nothing, toggle.Clicked());
    }

    /// <summary>Open is visible and not cloaked: the shell cloaks a closed panel (DWM_CLOAKED_SHELL) or hides it.</summary>
    [Theory]
    [InlineData(true, 0u, true)]
    [InlineData(true, 2u, false)]
    [InlineData(true, 1u, false)]
    [InlineData(false, 0u, false)]
    public void WhenAPanelShows(bool visible, uint cloaked, bool shows) =>
        Assert.Equal(shows, WindowsPanels.Shows(visible, cloaked));

    // --- 34: the option ----------------------------------------------------------------

    /// <summary>The box is written for a button set to it, and nothing at all for the icon or any other kind.</summary>
    [Fact]
    public void TheOptionIsWrittenOnlyWhereSomebodyChoseTheBox()
    {
        var icon = BarKind.Search.New();
        icon.WindowsSearch = new WindowsSearchOptions { ShowBox = false };
        icon.Normalize();
        Assert.Null(icon.WindowsSearch);
        Assert.DoesNotContain("windowsSearch", JsonSerializer.Serialize(icon, ConfigJson.Default.BarItem), StringComparison.Ordinal);

        var box = BarKind.Search.New();
        box.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
        box.Normalize();
        var json = JsonSerializer.Serialize(box, ConfigJson.Default.BarItem);
        Assert.Contains("\"windowsSearch\"", json, StringComparison.Ordinal);
        var read = JsonSerializer.Deserialize(json, ConfigJson.Default.BarItem)!;
        read.Normalize();
        Assert.True(read.WindowsSearch?.ShowBox);

        var other = BarKind.TaskView.New();
        other.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
        other.Normalize();
        Assert.Null(other.WindowsSearch);
    }

    /// <summary>A part is asked to be the box by its own option: Windows' button by its box.</summary>
    [Fact]
    public void APartIsTheBoxByItsOwnOption()
    {
        var search = BarKind.Search.New();
        Assert.False(PartSpec.For("search", search).Box);
        search.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
        Assert.True(PartSpec.For("search", search).Box);

        var other = BarKind.TaskView.New();
        other.WindowsSearch = new WindowsSearchOptions { ShowBox = true };
        Assert.False(PartSpec.For("taskview", other).Box);
    }


    // --- 34: on the bar ----------------------------------------------------------------

    private static Layout Lay(int height, Edge edge, bool box, uint dpi = 96, WidgetBounds? room = null)
    {
        var config = new Config { Height = height };
        var area = edge.IsVertical()
            ? new RECT { left = 0, top = 0, right = 1920, bottom = 4000 }
            : new RECT { left = 0, top = 0, right = 8000, bottom = 1080 };
        var key = Config.IslandKey(null, "search");
        return Layout.ForTests(
            dpi,
            area,
            edge,
            config,
            [new PartSpec(key, BarKind.Search, room ?? WidgetBounds.DefaultFor(BarKind.Search)) { Box = box }],
            Layout.CellPx(dpi, config));
    }

    /// <summary>The box's length on <paramref name="layout"/>'s bar, its words measured as Layout.ForTests measures text (§510).</summary>
    private static float BoxLength(Layout layout) =>
        SearchPill.Length(layout.CellSide, MathF.Ceiling(SearchPill.WindowsPlaceholder.Length * SearchPill.TextPx(layout.CellSide, default) * 0.6f));

    /// <summary>
    /// On the 48-DIP bar the box is as long as its magnifier and words (§510; Windows' 220 DIPs at most) and fills its slot: the whole part,
    /// the card's height (Layout.SearchBoxed) — at 100 % and at 150 %. Its one-cell width doesn't
    /// hold it, and the icon's part is still the one cell.
    /// </summary>
    [Theory]
    [InlineData(96u)]
    [InlineData(144u)]
    public void OnTheTaskbarsBarTheBoxIsWindowsOwn(uint dpi)
    {
        var scale = dpi / 96f;
        var layout = Lay(48, Edge.Bottom, box: true, dpi);
        var index = layout.PartOf(Hit.Search);
        var cell = layout.CellSide;
        Assert.True(layout.Boxed(index));
        Assert.Equal(BoxLength(layout), layout.Parts[index].Width, 0.01f);
        var pill = layout.ItemRect(Hit.Search);
        Assert.Equal(layout.IslandThickness, pill.Height, 0.01f);
        Assert.Equal(layout.Parts[index].Width, pill.Width, 0.01f);
        Assert.Equal(layout.BlockRect(index), pill);
        Assert.Equal(pill, layout.SearchBoxRect(Hit.Search));

        var icon = Lay(48, Edge.Bottom, box: false, dpi);
        // Centred across the bar as the icon's highlight is, to the pixel.
        Assert.InRange(MathF.Abs(pill.Center.Y - icon.ItemRect(Hit.Search).Center.Y), 0, 1f);
        Assert.False(icon.Boxed(icon.PartOf(Hit.Search)));
        Assert.Equal(icon.CellSide, icon.Parts[icon.PartOf(Hit.Search)].Width, 0.01f);
        Assert.True(icon.SearchBoxRect(Hit.Search).IsEmpty);
    }

    /// <summary>
    /// A vertical bar, or one whose cell is too small for a 24-DIP box, shows the icon whatever the
    /// option says: a 32- or 36-DIP bar's cards keep room off it, and its cell is too small (§533: the 32-DIP bar was flush by
    /// itself before, its cell its whole thickness, and showed the box; a bar spaced None still does, at any height).
    /// </summary>
    [Theory]
    [InlineData(48, Edge.Left, false)]
    [InlineData(48, Edge.Right, false)]
    [InlineData(28, Edge.Bottom, false)]
    [InlineData(32, Edge.Bottom, false)]
    [InlineData(36, Edge.Bottom, false)]
    [InlineData(40, Edge.Top, true)]
    [InlineData(64, Edge.Bottom, true)]
    public void OnlyAHorizontalBarWithRoomShowsTheBox(int height, Edge edge, bool boxed)
    {
        var layout = Lay(height, edge, box: true);
        var index = layout.PartOf(Hit.Search);
        Assert.Equal(boxed, layout.Boxed(index));
        Assert.Equal(boxed ? BoxLength(layout) : layout.CellSide, layout.Parts[index].Width, 0.01f);
    }

    /// <summary>The box grows with the bar, as every widget's content does, and keeps Windows' proportions.</summary>
    [Fact]
    public void TheBoxGrowsWithTheBar()
    {
        var tall = Lay(64, Edge.Bottom, box: true);
        var pill = tall.ItemRect(Hit.Search);
        Assert.Equal(tall.IslandThickness, pill.Height, 0.01f);
        Assert.True(pill.Height > Lay(48, Edge.Bottom, box: true).ItemRect(Hit.Search).Height);
    }

    /// <summary>
    /// §510: the box keeps the same room either side of what it holds (the person, 2026-11-05: "the windows search box
    /// somehow has a different spacing left and right"). The magnifier stands Windows' 11 DIPs in from the box's start
    /// and the words 37; the room after the words to the box's end is the same 11, to the px, on the 40-, 48- and
    /// 64-DIP bars at 100 to 200 %. It was Windows' 220 DIPs long whatever the words, so "Search" left some 140 DIPs
    /// of box after it and 11 before the magnifier. (The words measured as Layout.ForTests measures text.)
    /// </summary>
    [Theory]
    [InlineData(40, 96u)]
    [InlineData(48, 96u)]
    [InlineData(48, 120u)]
    [InlineData(48, 144u)]
    [InlineData(48, 168u)]
    [InlineData(48, 192u)]
    [InlineData(64, 144u)]
    [InlineData(40, 168u)]
    public void TheBoxKeepsTheSameRoomBeforeTheMagnifierAsAfterItsWords(int height, uint dpi)
    {
        var layout = Lay(height, Edge.Bottom, box: true, dpi);
        var cell = layout.CellSide;
        float Px(float dips) => MathF.Round(dips * cell / SearchPill.BaseCellDip);
        var words = MathF.Ceiling(SearchPill.WindowsPlaceholder.Length * SearchPill.TextPx(cell, default) * 0.6f);
        var box = layout.ItemRect(Hit.Search);

        var before = Px(SearchPill.EndDip);
        var after = box.Width - Px(SearchPill.TextLeftDip) - words;

        Assert.InRange(after - before, -1f, 1f);
    }

    /// <summary>A width somebody set for the icon — one cell, or six — doesn't hold the box.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void TheIconsWidthDoesNotHoldTheBox(int cells)
    {
        var layout = Lay(48, Edge.Bottom, box: true, room: WidgetBounds.Chosen(cells));
        Assert.Equal(BoxLength(layout), layout.Parts[layout.PartOf(Hit.Search)].Width, 0.01f);
    }
}
