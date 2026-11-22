// edit, §520, §575: the edit bar and its flyouts as real windows (EditDeck, Karpo.Dock\Dock.EditDeck.cs), driven as Windows
// hands them the pointer and the keys, on a desktop of its own (KARPO_UI_TESTS=1). Held here:
//
//  - **The bar**: its entries, Add, Done and the errors ask for their own; §630 nothing drags it.
//  - **A flyout**: an entry opens its flyout hung from it, its entry selected; another switches it, the same one closes it;
//    a toggle sends its row's id, and the dock's way of setting it changes the config; an expander opens and shuts in its
//    card, remembered; a dropdown asks for its popup under its box; a landing opens its entry's flyout at its row, its
//    expanders open, its row in sight; Tab goes from the bar's things into the flyout's, Left and Right along the bar, Enter
//    opens an entry.
//  - **Its trees** for a screen reader: the bar a tool bar whose entries expand and collapse; a flyout a pane of groups, a
//    toggle that toggles, an expander that expands.

using System.Numerics;
using Karpo.Dock;
using Karpo.Tests.Islands;
using Karpo.Tests.Menus;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Edit;

public sealed unsafe class EditDeckClickTests
{
    private static readonly RectI Display = new(0, 0, 2880, 1620);

    private static readonly RectI Work = new(0, 0, 2880, 1548);

    private static EditDeckRoom Room() => new(Display, 0, 72, Work: Work);

    /// <summary>The window's extent on the screen.</summary>
    internal static RECT WindowOf(HWND hwnd) => WindowShots.Extent(hwnd);

    /// <summary>Where <paramref name="point"/> of <paramref name="deck"/>'s bar — §575 or its flyout's (<paramref name="flyout"/>) — is in its window, as a mouse message's lParam.</summary>
    internal static LPARAM At(EditDeck deck, Vector2 point, bool flyout = false)
    {
        var hwnd = flyout ? deck.FlyoutHwnd : deck.Hwnd;
        var panel = flyout ? deck.FlyoutOnScreen : deck.PanelOnScreen;
        var window = WindowOf(hwnd);
        var at = WindowShots.Client(hwnd, panel.left - window.left + (int)point.X, panel.top - window.top + (int)point.Y);
        return (LPARAM)(nint)((at.y << 16) | (at.x & 0xffff));
    }

    /// <summary>
    /// A press and a release at the middle of <paramref name="hit"/>, as Windows hands them to the window it stands in; what it
    /// asked for.
    /// </summary>
    internal static EditDeckEvent Click(EditDeck deck, PanelHit hit)
    {
        var rect = deck.Layout!.RectOf(hit);
        Assert.False(rect.IsEmpty, $"{hit} is not shown");
        var bar = PanelLayout.OnBar(hit.Kind);
        var hwnd = bar ? deck.Hwnd : deck.FlyoutHwnd;
        var down = deck.Message(hwnd, WM.WM_LBUTTONDOWN, 1, At(deck, rect.Center, !bar)).Happened;
        var up = deck.Message(hwnd, WM.WM_LBUTTONUP, 0, At(deck, rect.Center, !bar)).Happened;
        return up.Action != EditDeckAction.None ? up : down;
    }

    internal static EditDeckEvent Click(EditDeck deck, PanelHitKind kind) => Click(deck, new PanelHit(kind));

    private static PanelHit Entry(PanelPage page) => new(PanelHitKind.Nav, Page: page);

    private static EditDeckEvent Key(EditDeck deck, int key) => deck.Message(deck.Hwnd, WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0).Happened;

    private static void Type(EditDeck deck, string words)
    {
        foreach (var c in words)
        {
            _ = deck.Message(deck.Hwnd, WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    private static Look Still => AddListPictures.LookFor(dark: true) with { Animations = false };

    /// <summary>§575: the dock's answer to an entry's event: its flyout open (Open), none (Close), as Dock.OpenPanel and Dock.ClosePanel show it.</summary>
    private static void Answer(Gfx gfx, EditDeck deck, SettingsContent content, EditDeckEvent asked)
    {
        if (asked.Action == EditDeckAction.Open)
        {
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, asked.Page);
        }
        else if (asked.Action == EditDeckAction.Close)
        {
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content);
        }
    }

    /// <summary>
    /// §575: the bar's things ask for their own — an entry its flyout, Add its list's, the errors their list, Done the end —;
    /// a press that leaves its button before the release asks for nothing; a press
    /// on the grip asks nothing either. With nowhere to add to (a display showing None), Add asks nothing.
    /// </summary>
    [UiFact]
    public void ItsThingsAskForTheirOwn()
    {
        WindowShots.OnHiddenDesktop("edit-deck-click", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain with { Errors = 2 }, SettingsPanelTests.Entries());
            var panel = deck.PanelOnScreen;
            var layout = deck.Layout!;
            Assert.False(deck.FlyoutOpen);
            // §531: the errors' list hangs from its button: the button on the screen.
            Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Taskbar), Click(deck, Entry(PanelPage.Taskbar)));
            Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Add), Click(deck, PanelHitKind.Add));
            // §689: the errors a page of the flyout.
            Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Errors), Click(deck, PanelHitKind.Errors));
            Assert.Equal(new EditDeckEvent(EditDeckAction.Done), Click(deck, PanelHitKind.Done));
            _ = deck.Message(deck.Hwnd, WM.WM_LBUTTONDOWN, 1, At(deck, layout.RectOf(Entry(PanelPage.Karpo)).Center));
            Assert.Equal(EditDeckAction.None, deck.Message(deck.Hwnd, WM.WM_LBUTTONUP, 0, At(deck, layout.RectOf(new PanelHit(PanelHitKind.Done)).Center)).Happened.Action);
            // What a press is on, as the dock asks before it hands the press on (Dock.DeckPressed).
            Assert.Equal(new PanelHit(PanelHitKind.Add), deck.PartAt(deck.Hwnd, At(deck, layout.RectOf(new PanelHit(PanelHitKind.Add)).Center)));
            Assert.Equal(Entry(PanelPage.Layouts), deck.PartAt(deck.Hwnd, At(deck, layout.RectOf(Entry(PanelPage.Layouts)).Center)));
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain with { Layout = null }, SettingsPanelTests.Entries());
            Assert.Null(deck.PartAt(deck.Hwnd, At(deck, deck.Layout!.RectOf(new PanelHit(PanelHitKind.Add)).Center)));
        });
    }

    /// <summary>
    /// §630 (the person, 2026-11-09: "the topbar should not be movable anymore"): no grip; a press at the bar's start, the
    /// pointer moved with the button held and let go, asks for nothing and the bar stands where it stood (§395 to §629 a drag
    /// by its grip moved it).
    /// </summary>
    [UiFact]
    public void NothingDragsIt()
    {
        WindowShots.OnHiddenDesktop("edit-deck-drag", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain);
            var before = deck.PanelOnScreen;
            var layout = deck.Layout!;
            Assert.DoesNotContain(layout.Header, part => part.Kind is not (PanelHitKind.Nav or PanelHitKind.Errors or PanelHitKind.Add or PanelHitKind.Done));
            var start = new Vector2(2, 2);
            Assert.True(layout.HitAt(start).IsNone);
            Assert.Equal(EditDeckAction.None, deck.Message(deck.Hwnd, WM.WM_LBUTTONDOWN, 1, At(deck, start)).Happened.Action);
            _ = deck.Message(deck.Hwnd, WM.WM_MOUSEMOVE, 1, At(deck, start + new Vector2(200, 300)));
            Assert.Equal(before, deck.PanelOnScreen);
            Assert.Equal(EditDeckAction.None, deck.Message(deck.Hwnd, WM.WM_LBUTTONUP, 0, At(deck, start + new Vector2(200, 300))).Happened.Action);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain);
            Assert.Equal(before, deck.PanelOnScreen);
        });
    }

    /// <summary>
    /// §575 (the person, 2026-11-08: "they open a flyout individually with their settings in it"): an entry clicked opens its
    /// flyout, hung from it — under it, §578 centred on it, §584 12 DIP off the bar, its beak over the entry —, the entry
    /// selected, the bar standing as it stood; another entry switches it, its flyout hung from it; the open one clicked again closes it; Add's opens the add
    /// list in one of the same look, centred under Add, its own search with the keys.
    /// </summary>
    [UiFact]
    public void AnEntryOpensItsFlyoutAnotherSwitchesItItselfClosesIt()
    {
        WindowShots.OnHiddenDesktop("edit-deck-entries", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var content = SettingsPanelTests.Content();
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content);
            var bar = deck.PanelOnScreen;
            Assert.False(deck.FlyoutOpen);
            Assert.Null(deck.Page);
            foreach (var page in new[] { PanelPage.Taskbar, PanelPage.Appearance })
            {
                var asked = Click(deck, Entry(page));
                Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: page), asked);
                Answer(gfx, deck, content, asked);
                Assert.True(deck.FlyoutOpen);
                Assert.Equal(page, deck.Page);
                Assert.Equal(bar, deck.PanelOnScreen);
                Assert.True(deck.Layout!.Opened(Entry(page)));
                var entry = deck.OnScreen(Entry(page));
                var flyout = deck.FlyoutOnScreen;
                // §584: 12 DIP under the bar, its beak out of its top over the entry's middle.
                Assert.Equal(bar.bottom + (int)MathF.Round(Beak.GapDip * 1.5f), flyout.top);
                Assert.InRange((flyout.left + flyout.right) - (entry.left + entry.right), -2, 2);
                Assert.Equal((Karpo.Core.Edge.Top, 12f), (deck.FlyoutBeak!.Value.Side, deck.FlyoutBeak.Value.Tall));
                Assert.InRange(flyout.left + deck.FlyoutBeak.Value.Tip - ((entry.left + entry.right) / 2f), -1, 1);
                Assert.Equal((int)MathF.Round(580 * 1.5f), flyout.right - flyout.left);
            }
            var shut = Click(deck, Entry(PanelPage.Appearance));
            Assert.Equal(EditDeckAction.Close, shut.Action);
            Answer(gfx, deck, content, shut);
            Assert.False(deck.FlyoutOpen);
            Assert.Null(deck.Page);
            // Add: the add list's flyout, its search with the keys.
            var adding = content with { Pages = [.. content.Pages, SettingsPanelTests.AddPage()] };
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, adding, PanelPage.Add);
            Assert.Equal(PanelPage.Add, deck.Page);
            // §593: hung from the whole bar, as wide.
            SceneTreeTests.HungFromTheBar(deck);
            // §593: a tile under the pointer is the pick the dock shows a ghost of; a folder's head none, the pointer gone none.
            List<uint> proposed = [];
            deck.Proposing = proposed.Add;
            var lines = deck.Layout!.Blocks.SelectMany(block => block.Lines).ToList();
            var tile = lines.First(line => line.Row is { Kind: PanelRowKind.Pick, Greyed: false });
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, At(deck, deck.Layout.OnPanel(tile.Rect).Center, flyout: true));
            Assert.Equal(tile.Row.Item.Id, deck.Proposed);
            var head = lines.First(line => line.Row.Kind == PanelRowKind.Expander);
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, At(deck, deck.Layout.OnPanel(head.Rect).Center, flyout: true));
            Assert.Equal(0u, deck.Proposed);
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSEMOVE, 0, At(deck, deck.Layout.OnPanel(tile.Rect).Center, flyout: true));
            _ = deck.Message(deck.FlyoutHwnd, WM.WM_MOUSELEAVE, 0, 0);
            Assert.Equal(0u, deck.Proposed);
            Assert.Equal([tile.Row.Item.Id, 0u, tile.Row.Item.Id, 0u], proposed);
            Assert.True(deck.Typing);
            Type(deck, SettingsPanelTests.AddWord);
            Assert.Equal(SettingsPanelTests.AddWord, deck.Filtered);
            Assert.All(deck.Layout!.Blocks, block => Assert.Equal(PanelRowKind.Pick, block.Row.Kind));
            var clock = deck.Layout.Blocks.First(block => !block.Row.Greyed).Row;
            var picked = Click(deck, new PanelHit(PanelHitKind.Act, clock.Item.Id, Key: clock.Key));
            Assert.Equal((EditDeckAction.Act, PanelPage.Add, clock.Item.Id), (picked.Action, picked.Page, picked.Id));
            Assert.Equal(EditDeckAction.Close, Click(deck, PanelHitKind.Add).Action);
        });
    }

    /// <summary>
    /// §650 (the person, 2026-11-09: "when clicking add widget i cannot immeadiately type to find widgets. i first have to
    /// click again"): the add page opening takes the keys to its window, whatever had them — a bar's "+" leaves them on the
    /// bar, a folder's "+" on the folder, the edit bar's Add on the bar —, so the first letter typed lands in its search, its
    /// caret blinking there; no second click. A page with no search leaves the keys where they are.
    /// </summary>
    [UiFact]
    public void TheAddPagesSearchHasTheKeysAsItOpens()
    {
        WindowShots.OnHiddenDesktop("edit-deck-add-keys", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var content = SettingsPanelTests.Content();
            var adding = content with { Pages = [.. content.Pages, SettingsPanelTests.AddPage()] };
            // What had the keys before: another window of Karpo's (the bar being edited, a folder).
            HWND other;
            fixed (char* kind = "STATIC")
            fixed (char* name = "Karpo test keys")
            {
                other = CreateWindowExW(WS.WS_EX_TOOLWINDOW, kind, name, WS.WS_POPUP | WS.WS_VISIBLE, 0, 0, 40, 40, HWND.NULL, HMENU.NULL, instance, null);
            }
            Assert.NotEqual(HWND.NULL, other);
            try
            {
                _ = SetFocus(other);
                Assert.Equal(other, GetFocus());
                deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content);
                // A page with no search: the keys stay where they were.
                Answer(gfx, deck, content, Click(deck, Entry(PanelPage.Taskbar)));
                Assert.Equal(other, GetFocus());
                Answer(gfx, deck, content, Click(deck, Entry(PanelPage.Taskbar)));
                Assert.False(deck.FlyoutOpen);
                // Add: its window has the keys, its search them in it, the caret on.
                var asked = Click(deck, PanelHitKind.Add);
                Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Add), asked);
                Answer(gfx, deck, adding, asked);
                Assert.Equal(PanelPage.Add, deck.Page);
                Assert.Equal(deck.FlyoutHwnd, GetFocus());
                Assert.True(deck.Typing);
                // Typed where Windows sends it: the window with the keys.
                var word = SettingsPanelTests.AddWord;
                foreach (var c in word)
                {
                    var keys = GetFocus();
                    Assert.True(deck.Owns(keys));
                    _ = deck.Message(keys, WM.WM_CHAR, (WPARAM)(nuint)c, 0);
                }
                Assert.Equal(word, deck.Filtered);
                // The keys back elsewhere (a click on another app), the page standing: drawn again, it takes nothing back.
                _ = SetFocus(other);
                deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain with { Errors = 1 }, adding, PanelPage.Add);
                Assert.Equal(other, GetFocus());
                // Opened again, it takes them again.
                deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content);
                deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, adding, PanelPage.Add);
                Assert.Equal(deck.FlyoutHwnd, GetFocus());
                Assert.Equal("", deck.Filtered);
            }
            finally
            {
                _ = DestroyWindow(other);
            }
        });
    }

    /// <summary>
    /// A flyout's controls do what their rows did: the Start with Windows toggle sends its row; §549 Animations' Off, set the
    /// dock's way, turns animations off in the config; Public holidays opens in its card and stays open for the next showing;
    /// Language asks for its popup under its box.
    /// </summary>
    [UiFact]
    public void AFlyoutsControlsDoWhatTheirRowsDid()
    {
        WindowShots.OnHiddenDesktop("edit-deck-open", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var config = MenuCorpus.Fresh();
            var services = MenuCorpus.Services();
            List<OptionPick> picks = [];
            var content = SettingsPanelTests.Content();
            var karpo = TheDock.SettingsItems(config, services, picks);
            content = content with { Pages = [PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), karpo), .. content.Pages.Skip(1)] };
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Karpo);
            Assert.Equal(PanelPage.Karpo, deck.Page);
            // A toggle (Start with Windows): its row's id, as the dock is asked (Dock.SettingsPicked); not set here, which
            // would write Windows' Run key.
            var startup = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsGeneralStartupHeader);
            var flipped = Click(deck, deck.Layout.Controls(startup)[0].Hit);
            Assert.Equal((EditDeckAction.Act, PanelPage.Karpo, startup.Row.Item.Id), (flipped.Action, flipped.Page, flipped.Id));
            // §682: Animations the Appearance page's now (KarpoMenuGeneralTests sets it).
            Assert.DoesNotContain(deck.Layout.Blocks.SelectMany(block => block.Lines), line => line.Row.Text == S.SettingsGeneralAnimationsHeader);
            // Public holidays, §594 landed on, opens in its card, its calendars inset in it; remembered; its head's click shuts it.
            deck.Land(PanelPage.Karpo, deck.Layout.Blocks.Single(block => block.Row.Text == S.CoreHolidaysName).Row.Key, light: false);
            var opened = deck.Layout!.Blocks.Single(block => block.Row.Text == S.CoreHolidaysName);
            Assert.Contains(opened.Lines, line => line.Inner);
            Assert.True(deck.Memory.IsOpen(opened.Row.Key));
            Assert.Equal(EditDeckAction.None, Click(deck, deck.Layout.Controls(opened.Lines[0])[0].Hit).Action);
            Assert.Single(deck.Layout.Blocks.Single(block => block.Row.Text == S.CoreHolidaysName).Lines);
            Assert.False(deck.Memory.IsOpen(opened.Row.Key));
            // Language: §676 its picker inline — a click opens it in its card, again shuts it; no popup.
            deck.Land(PanelPage.Karpo, deck.Layout.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsLanguageSection).Row.Key, light: false);
            var language = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsLanguageSection);
            Assert.Equal(EditDeckAction.None, Click(deck, deck.Layout.Controls(language)[0].Hit).Action);
            Assert.True(deck.Memory.IsOpen(language.Row.Key));
            Assert.Equal(EditDeckAction.None, Click(deck, deck.Layout.Controls(language)[0].Hit).Action);
            Assert.False(deck.Memory.IsOpen(language.Row.Key));
        });
    }

    /// <summary>
    /// §520 decision 11: Tab from nothing to the bar's first entry and on through the bar's things (§578: no search among
    /// them) into the flyout's; Left and Right along the bar's entries, Enter opening one; a landing (a link's, a way in from
    /// outside) opens its entry's flyout at its row, its expanders open, its row in sight.
    /// </summary>
    [UiFact]
    public void TheKeysFindTheirWay()
    {
        WindowShots.OnHiddenDesktop("edit-deck-keys", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var content = SettingsPanelTests.Content(widget: true);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain with { Errors = 1 }, content);
            Key(deck, VK.VK_TAB);
            Assert.Equal(Entry(PanelPage.Karpo), deck.Focused);
            Key(deck, VK.VK_RIGHT);
            Assert.Equal(Entry(PanelPage.Accounts), deck.Focused);
            Key(deck, VK.VK_LEFT);
            Assert.Equal(Entry(PanelPage.Karpo), deck.Focused);
            var stops = deck.Layout!.Stops();
            Assert.Equal([.. Enumerable.Repeat(PanelHitKind.Nav, 7), PanelHitKind.Errors, PanelHitKind.Add, PanelHitKind.Done], stops.Select(stop => stop.Kind));
            // §596: the folder's entry and the widget's two stops of the arrows after Appearance, as the pages' entries are.
            for (var step = 0; step < 5; step++)
            {
                Key(deck, VK.VK_RIGHT);
            }
            Assert.Equal(Entry(PanelPage.Folder), deck.Focused);
            Key(deck, VK.VK_RIGHT);
            Assert.Equal(Entry(PanelPage.Widget), deck.Focused);
            for (var step = 0; step < 6; step++)
            {
                Key(deck, VK.VK_LEFT);
            }
            Assert.Equal(Entry(PanelPage.Karpo), deck.Focused);
            Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Karpo), Key(deck, VK.VK_RETURN));

            // A landing: Appearance's flyout at a row in an expander, opened, in sight.
            var page = content.Of(PanelPage.Appearance)!;
            var deep = PanelRows.All(page.Rows).Last(row => PanelRows.PathTo(page.Rows, row.Key) is { Count: > 0 } && row.Kind != PanelRowKind.Preview);
            deck.Land(PanelPage.Appearance, deep.Key);
            Assert.Equal(PanelPage.Appearance, deck.Page);
            Assert.All(PanelRows.PathTo(page.Rows, deep.Key)!, key => Assert.True(PanelGroups.IsGroup(key) ? !deck.Memory.IsOpen(key) : deck.Memory.IsOpen(key), key));
            var line = deck.Layout!.LineOf(deep.Key)!;
            var shown = deck.Layout.OnPanel(line.Rect);
            Assert.True(shown.Y >= deck.Layout.Viewport.Y && shown.Bottom <= deck.Layout.Viewport.Bottom, "the row landed on is out of sight");
        });
    }

    /// <summary>
    /// §525: a dropdown pressed has the keys: F4, Alt+Down and Alt+Up ask for
    /// its popup again, as a ComboBox's; Alt+F4 does not, nor F4 on a toggle, which it never toggles.
    /// </summary>
    [UiFact]
    public void APressTakesTheKeysAndADropdownOpensByItsKeys()
    {
        WindowShots.OnHiddenDesktop("edit-deck-drop-keys", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var karpo = TheDock.SettingsItems(MenuCorpus.Fresh(), MenuCorpus.Services(), []);
            var content = SettingsPanelTests.Content();
            content = content with { Pages = [PanelRows.Page(PanelPage.Karpo, PanelRows.SideOf(PanelPage.Karpo), PanelRows.GlyphOf(PanelPage.Karpo), karpo), .. content.Pages.Skip(1)] };
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Karpo);

            // Language pressed: §676 its picker opened inline; §689 its keys (F4, Alt+Down, Alt+Up) shut it and open it again, as
            // a ComboBox's open its list; Alt+F4 nothing of its.
            var language = deck.Layout!.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsLanguageSection);
            var box = deck.Layout.Controls(language)[0].Hit;
            Assert.Equal(EditDeckAction.None, Click(deck, box).Action);
            Assert.True(deck.Memory.IsOpen(language.Row.Key));
            EditDeckEvent Keyed(int key, bool alt) => deck.Message(deck.FlyoutHwnd, (uint)(alt ? WM.WM_SYSKEYDOWN : WM.WM_KEYDOWN), (WPARAM)(nuint)key, alt ? (LPARAM)(nint)(1 << 29) : 0).Happened;
            _ = Keyed(VK.VK_F4, alt: false);
            Assert.False(deck.Memory.IsOpen(language.Row.Key));
            _ = Keyed(VK.VK_DOWN, alt: true);
            Assert.True(deck.Memory.IsOpen(language.Row.Key));
            _ = Keyed(VK.VK_UP, alt: true);
            Assert.False(deck.Memory.IsOpen(language.Row.Key));
            Assert.Equal(EditDeckAction.None, Keyed(VK.VK_F4, alt: true).Action);
            Assert.False(deck.Memory.IsOpen(language.Row.Key));
            // A toggle pressed has the keys: F4 is nothing of its.
            var startup = deck.Layout.Blocks.SelectMany(block => block.Lines).Single(line => line.Row.Text == S.SettingsGeneralStartupHeader);
            Assert.Equal(EditDeckAction.Act, Click(deck, deck.Layout.Controls(startup)[0].Hit).Action);
            Assert.Equal(EditDeckAction.None, Keyed(VK.VK_F4, alt: false).Action);
        });
    }

    /// <summary>
    /// §592: the Appearance's tabs as the real windows take them: a click chooses one, its rows under the strip; Tab reaches the
    /// chosen one alone, Right and Left choose the next and the one before (held at the ends), End and Home the last and the
    /// first, the keys going with them; the tab chosen kept as the flyout closes and opens again; a screen reader's Select
    /// chooses one too, its tab list saying which is selected.
    /// </summary>
    [UiFact]
    public void TheAppearanceTabsAreChosenByAClickTheArrowsAndAScreenReader()
    {
        WindowShots.OnHiddenDesktop("edit-deck-tabs", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var content = SettingsPanelTests.Content();
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Appearance);
            var strip = content.Of(PanelPage.Appearance)!.Rows.Single(row => row.Kind == PanelRowKind.Tabs);
            PanelHit Tab(int at) => new(PanelHitKind.Tab, 0, at, Key: strip.Rows[at].Key);
            int Chosen() => deck.Layout!.Chosen(strip);
            bool Shows(int at) => strip.Rows[at].Rows.All(row => deck.Layout!.LineOf(row.Key) is not null);
            Assert.Equal(0, Chosen());
            Assert.True(Shows(0));

            // A click: that tab, its rows; the first's gone.
            Assert.Equal(EditDeckAction.None, Click(deck, Tab(2)).Action);
            Assert.Equal(2, Chosen());
            Assert.True(Shows(2));
            Assert.False(Shows(0));

            // Tab: from the bar into the flyout, on to the chosen tab, never another.
            EditDeckEvent Keyed(int key) => deck.Message(deck.FlyoutHwnd, WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0).Happened;
            for (var step = 0; step < 40 && deck.Focused.Kind != PanelHitKind.Tab; step++)
            {
                _ = Keyed(VK.VK_TAB);
                Assert.False(deck.Focused.Kind == PanelHitKind.Tab && deck.Focused.Index != 2, $"the keys on a tab not chosen: {deck.Focused}");
            }
            Assert.Equal(Tab(2), deck.Focused);
            _ = Keyed(VK.VK_RIGHT);
            Assert.Equal((3, Tab(3)), (Chosen(), deck.Focused));
            _ = Keyed(VK.VK_RIGHT);
            Assert.Equal((3, Tab(3)), (Chosen(), deck.Focused));
            _ = Keyed(VK.VK_LEFT);
            Assert.Equal((2, Tab(2)), (Chosen(), deck.Focused));
            _ = Keyed(VK.VK_HOME);
            Assert.Equal((0, Tab(0)), (Chosen(), deck.Focused));
            _ = Keyed(VK.VK_END);
            Assert.Equal((3, Tab(3)), (Chosen(), deck.Focused));
            Assert.True(Shows(3));

            // Closed and opened again: the tab chosen kept.
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, null);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Appearance);
            Assert.Equal(3, Chosen());

            // A screen reader: a tab list, its tabs, the chosen one selected; its Select on another (Uia.SelectItem: an Invoke) chooses that one.
            var tree = deck.AccessTree(bar: false);
            static IEnumerable<UiaNode> Nodes(UiaNode node) => node.Children.SelectMany(Nodes).Prepend(node);
            var list = Assert.Single(Nodes(tree.Root), node => node.Role == UiaRole.Tab);
            Assert.Equal(LookParts.All.Select(LookParts.Name), list.Children.Select(node => node.Name));
            Assert.Equal([false, false, false, true], list.Children.Select(node => node.On == true));
            deck.Act(UiaAct.Invoke, list.Children[1].Id, bar: false);
            Assert.Equal(1, Chosen());
            Assert.Equal([false, true, false, false], Nodes(deck.AccessTree(bar: false).Root).Single(node => node.Role == UiaRole.Tab).Children.Select(node => node.On == true));
        });
    }

    /// <summary>
    /// narrator, §520 decision 11, §575: the bar a tool bar — each entry a button that expands and collapses, the one open
    /// expanded —; the flyout a pane named for its page: a toggle that toggles, an expander that expands,
    /// sections its groups, a number box a range. A screen reader's Expand on an entry asks for its flyout.
    /// </summary>
    [UiFact]
    public void ItsTreesSayWhatEachThingIs()
    {
        WindowShots.OnHiddenDesktop("edit-deck-tree", (gfx, instance) =>
        {
            using var deck = new EditDeck(gfx, instance, 0);
            var content = SettingsPanelTests.Content();
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Karpo);
            var bar = deck.AccessTree(bar: true);
            Assert.Equal(UiaRole.ToolBar, bar.Root.Role);
            var entries = bar.Root.Children.Where(node => node.Pattern == UiaPattern.Expand).ToList();
            Assert.Equal([S.SettingsGeneralHeader, S.SettingsNavAccounts, S.CoreBarMenuLayouts, S.DockEditTaskbar, S.SettingsAppearanceHeader, S.DockEditAdd], entries.Select(node => node.Name));
            Assert.Equal([true, false, false, false, false, false], entries.Select(node => node.On == true));
            Assert.DoesNotContain(bar.Root.Children, node => node.Role == UiaRole.Edit);
            var tree = deck.AccessTree(bar: false);
            Assert.Equal((UiaRole.Pane, S.SettingsGeneralHeader), (tree.Root.Role, tree.Root.Name));
            Assert.Contains(tree.All(), node => node.Name == S.SettingsGeneralStartupHeader && node.Pattern == UiaPattern.Toggle);
            Assert.Contains(tree.All(), node => node.Name == S.CoreHolidaysName && node.Pattern == UiaPattern.Expand);
            Assert.Contains(tree.All(), node => node.Role == UiaRole.Group && node.Name == S.SettingsGeneralHeader);
            // A screen reader's Invoke on Taskbar's entry asks for its flyout; its Toggle on Start with Windows for its row.
            EditDeckEvent? asked = null;
            deck.Invoked = happened => asked = happened;
            deck.Act(UiaAct.Invoke, entries[3].Id, bar: true);
            Assert.Equal(new EditDeckEvent(EditDeckAction.Open, Page: PanelPage.Taskbar), asked);
            var toggle = deck.AccessTree(bar: false).All().First(node => node.Name == S.SettingsGeneralStartupHeader);
            deck.Act(UiaAct.Toggle, toggle.Id, bar: false);
            Assert.Equal(EditDeckAction.Act, asked?.Action);
            deck.Show(gfx, Still, 1.5f, Room(), EditDeckState.Plain, content, PanelPage.Taskbar);
            Assert.Contains(deck.AccessTree(bar: false).All(), node => node.Role == UiaRole.Spinner && node.Range is not null);
        });
    }
}
