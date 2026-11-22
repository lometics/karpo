// menus: a settings menu whose levels open in place (Karpo.Dock\Flyout.Inline.cs, docs\widgets.md §268,
// every widget's since §271), on a desktop of the test's own (KARPO_UI_TESTS=1). Held here:
//
//  - **Every widget's menu.** Edit mode's menu of every bar widget and every folder line (its settings),
//    and a folder's with its faces: every row with a submenu, and every such row on its level, opens in
//    place, the one window showing the level's head (its back arrow and name) and its rows; the head
//    goes back. A menu that cascades (the add list's) still opens a window beside it.
//  - **A level in place.** Where the menu stood, kept from its foot over a bar at the bottom; a choice
//    picked there keeps the level, and a Refresh shows it again with the tick moved.
//  - **The keys and the mouse.** Right, Enter and Space go in, onto the level's first row; Left,
//    Backspace, Escape and the mouse's back button go back, a level at a time, and only then does Escape
//    close the menu; three levels deep and back, a Refresh keeping the deepest and a level whose row
//    went falling back.
//  - **The fields.** The symbol's search level has the keys at once, its rows before anything is typed and
//    the service's answer once it is in, in the one window; Escape gives the field's words back, lets the
//    keys go, then goes back. The amount owned is kept by Enter, which goes back.
//  - **Every result, one room.** A zone's level lists every zone Windows has, no "Show more", in the room
//    its level keeps however much is typed; the wheel scrolls them under the head and the field, which
//    stand still and alone take the pointer there. An icon picker's head stands over its field and tiles.
//  - **One height, edit mode's** (§288): every widget's configuration menu and every level of it, a
//    field's too, the one height, every widget's the same, its foot over the bar, its sides kept; the room
//    on its side caps it.
//  - **As tall as its rows, any other** (§286): a menu at rest and every level of it its rows' height, its
//    foot over the bar, its sides and its width kept on every level and back; a Refresh with more rows or
//    fewer grows or shrinks it; a level a field searches or filters its field and ten rows' room; a long
//    level scrolls; the room on its side caps it.
//  - **The foot** (§276): a folder line's "Height ›" the root's last row, standing still at the menu's foot
//    as the rows over it scroll, in one place on the screen in a short menu and a long one; its own level
//    fits. A folder's "Add a face ›" likewise (§280), over the height were both in one menu.
//  - **Pictures** in KARPO_PNG_DIR, at 150 %, dark and light (see InlinePictures, InlineHeightPictures).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using Karpo.Tests.Markets;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using TheDock = Karpo.Dock.Dock;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class InlineMenuTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>A markets line on CHF/USD's menu as edit mode has it, its search answering from <see cref="MarketRowsTests.EurAnswer"/>.</summary>
    private static (List<FlyoutItem> Items, List<OptionPick> Picks, CellItem Line) Markets()
    {
        var line = ListKind.Markets.New();
        line.Market = new MarketOptions { Symbol = "CHFUSD=X" };
        List<Listing> used = [new("AAPL", "Apple Inc.", "", ""), new("BTC-USD", "", "", "")];
        List<OptionPick> picks = [];
        List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(line), .. OptionMenu.Section(OptionRows.Of(line, new OptionDevices { Shares = used }), picks)];
        var symbol = items.Single(item => item.Text == S.SettingsWidgetMarketSymbolHeader);
        var field = symbol.Children!.Single(item => item.Control is FlyoutField);
        var finder = new MarketFinder(MarketRows.Share(line.Market), used, "CHF", (_, cancel) =>
            Task.Delay(60, cancel).ContinueWith(_ => SymbolQuery.Listings(MarketRowsTests.EurAnswer), cancel, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default));
        items[items.IndexOf(symbol)] = symbol with { Children = [.. symbol.Children!.Select(item => item == field ? field with { Control = ((FlyoutField)field.Control!) with { Finder = finder } } : item)] };
        return (items, picks, line);
    }

    /// <summary>A clock's menu as edit mode has it, its zone's search offering every zone Windows has (§230).</summary>
    private static (List<FlyoutItem> Items, FlyoutItem Zone) Clock()
    {
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(clock), .. OptionMenu.Section(OptionRows.Of(clock, new OptionDevices { Zones = ["Tokyo Standard Time"] }), [])];
        return (items, items.Single(item => item.Text == S.SettingsWidgetClockZoneHeader));
    }

    /// <summary>A folder wearing a clock, the weather and its icon, its menu on an arranged bar: its own rows and its faces'.</summary>
    private static (List<FlyoutItem> Items, List<OptionPick> Picks, BarItem Folder) Folder()
    {
        var folder = BarKind.FolderList.New();
        folder.Id = "box";
        folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.IconId)];
        List<OptionPick> picks = [];
        List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.Section(OptionRows.Of(folder), picks), .. OptionMenu.FaceSection(folder.Folder, picks)];
        return (items, picks, folder);
    }

    /// <summary>Three levels: Style, its Colour, and the colour's Shade.</summary>
    private static List<FlyoutItem> Deep(bool withStyle = true) =>
    [
        new(1, "Open"),
        .. withStyle
            ? [new FlyoutItem(2, "Style", Children:
                [
                    new(21, "Plain", Checked: true, Stays: true),
                    new(22, "Fancy", Checked: false, Stays: true),
                    new(23, "Colour", Children:
                    [
                        new(231, "Red", Checked: true, Stays: true),
                        new(232, "Shade", Children: [new(2321, "Light", Stays: true), new(2322, "Dark", Stays: true)]),
                    ]),
                ])]
            : Array.Empty<FlyoutItem>(),
        new(3, "View", Children: [new(31, "Large"), new(32, "Small")]),
    ];

    /// <summary>
    /// A widget's menu, which Configures (Dock.ShowFlyout: every menu of a widget's); <paramref name="configures"/>
    /// false, one that cascades; <paramref name="configuration"/>, edit mode's, one height (§288).
    /// </summary>
    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, bool configures = true, bool dark = true, RECT? work = null, POINT? anchor = null, bool configuration = false)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = configures, Configuration = configuration };
        menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, anchor ?? Anchor, work ?? Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    /// <summary>The menu's own posted messages and timers handed to it for <paramref name="ms"/>, as the dock's window procedure hands them: what it said.</summary>
    private static List<FlyoutEvent> Pump(Flyout menu, int ms = 250)
    {
        List<FlyoutEvent> said = [];
        var until = Environment.TickCount64 + ms;
        MSG msg;
        while (Environment.TickCount64 < until)
        {
            while (PeekMessageW(&msg, menu.Hwnd, WM.WM_USER, WM.WM_USER + 0xFF, PM.PM_REMOVE)
                || PeekMessageW(&msg, menu.Hwnd, WM.WM_TIMER, WM.WM_TIMER, PM.PM_REMOVE))
            {
                said.Add(menu.Message(msg.message, msg.wParam, msg.lParam));
            }
            // Everything else dispatched too, the caret's blink (WM_SYSTIMER) among it, which left at the
            // queue's head held a search's answer back (MenuFieldTests.Pump). Bounded, for a WM_PAINT nobody validates.
            for (var left = 64; left > 0 && PeekMessageW(&msg, HWND.NULL, 0, 0, PM.PM_REMOVE); left--)
            {
                if (msg.hwnd == menu.Hwnd && (msg.message is >= WM.WM_USER and <= WM.WM_USER + 0xFF || msg.message == WM.WM_TIMER))
                {
                    said.Add(menu.Message(msg.message, msg.wParam, msg.lParam));
                    continue;
                }
                _ = TranslateMessage(&msg);
                _ = DispatchMessageW(&msg);
            }
            Thread.Sleep(10);
        }
        return [.. said.Where(happened => happened.Kind != FlyoutEventKind.Nothing)];
    }

    /// <summary>Pumped (<see cref="Pump"/>) until <paramref name="done"/>, the search's wait and its canned answer in, or <paramref name="most"/> ms: a busy machine answers late.</summary>
    private static void PumpUntil(Flyout menu, Func<bool> done, int most = 4000)
    {
        var until = Environment.TickCount64 + most;
        while (!done() && Environment.TickCount64 < until)
        {
            _ = Pump(menu, 50);
        }
        // What the answer changed, shown.
        _ = Pump(menu, 100);
    }

    /// <summary>Down until the keys are on row <paramref name="id"/>.</summary>
    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 80 && menu.HotId != id; step++)
        {
            _ = Key(menu, VK.VK_DOWN);
        }
        Assert.Equal(id, menu.HotId);
    }

    private static RECT Panel(Flyout menu) => Assert.Single(menu.OpenPanels).Panel;

    /// <summary>§286: where row <paramref name="id"/>'s fill stands on the screen.</summary>
    private static (POINT Middle, float W, float H) OnScreen(Flyout menu, uint id)
    {
        var (middle, w, h) = menu.RowFill(id)!.Value;
        _ = ClientToScreen(menu.Hwnd, &middle);
        return (middle, w, h);
    }

    /// <summary>
    /// §286: the menu where it <paramref name="opened"/>, its sides and its foot over the bar, as tall as its
    /// rows — or, longer than the room, the room's height, scrolling; a <paramref name="level"/> a field searches
    /// or filters, its head, its field and ten rows' room, whatever it lists.
    /// </summary>
    private static void AsTallAsItsRows(Flyout menu, RECT opened, string where, IReadOnlyList<FlyoutItem>? level = null)
    {
        var now = Panel(menu);
        Assert.True((opened.left, opened.right, opened.bottom) == (now.left, now.right, now.bottom), $"{where}: moved the menu from {opened.left},{opened.bottom} to {now.left},{now.bottom}");
        var field = level is not null && (Flyout.Searchable(level) || level.Any(item => item.Control is FlyoutField { Filters: true } or FlyoutField { Finder.Finds: true }));
        Assert.True(
            field ? menu.ShownHeight >= (Flyout.ListFilterFrom + 2) * 32 * Scale && menu.ShownHeight <= (Flyout.ListFilterFrom + 5) * 32 * Scale
            : menu.ContentHeight > menu.ShownHeight ? now.top - Work.top < 48 * Scale
            : MathF.Abs(menu.ShownHeight - menu.ContentHeight) < 1,
            $"{where}: {menu.ShownHeight} px shown for {menu.ContentHeight} px of rows, its top {now.top}");
    }

    /// <summary>A configuration menu's one height in px here (Flyout.SettingsHeight, §288).</summary>
    private static int OneHeight => (int)Flyout.SettingsHeight(Scale, 1);

    /// <summary>
    /// §288: a configuration menu where it <paramref name="opened"/>, its sides and its foot over the bar, its
    /// one height — <paramref name="tall"/> where the room caps it — whatever its rows, a field's level's too.
    /// </summary>
    private static void OneHeightAt(Flyout menu, RECT opened, string where, int? tall = null)
    {
        var now = Panel(menu);
        Assert.True((opened.left, opened.right, opened.bottom) == (now.left, now.right, now.bottom), $"{where}: moved the menu from {opened.left},{opened.bottom} to {now.left},{now.bottom}");
        Assert.True(Math.Abs(now.bottom - now.top - (tall ?? OneHeight)) <= 1, $"{where}: {now.bottom - now.top} px tall, not {tall ?? OneHeight}");
    }

    private static nint At(POINT point) => (nint)((point.y << 16) | (point.x & 0xFFFF));

    // --- Every widget's menu ------------------------------------------------------------------------

    /// <summary>Edit mode's menu of every widget there is (Dock.WidgetMenu): its own rows and its settings, and a folder's faces.</summary>
    private static IEnumerable<(string Name, List<FlyoutItem> Items)> EveryWidgetsMenu()
    {
        IEnumerable<WidgetItem> widgets = [.. Enum.GetValues<BarKind>().Select(kind => (WidgetItem)kind.New()), .. Enum.GetValues<ListKind>().Select(kind => kind.New())];
        foreach (var widget in widgets)
        {
            List<OptionPick> picks = [];
            List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(widget), .. OptionMenu.Section(OptionRows.Of(widget, new OptionDevices()), picks)];
            if (widget is BarItem { Folder: { } folder })
            {
                folder.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.WeatherId), FolderFace.Of(FolderFaces.IconId)];
                items = [.. items, .. OptionMenu.FaceSection(folder, picks)];
            }
            yield return (widget is BarItem bar ? bar.Kind.ToString() : ((CellItem)widget).Kind.ToString(), items);
        }
    }

    /// <summary>
    /// §271: every widget's menu, every row with a submenu on its own rows and on the level it opens:
    /// opened in place in the one window, its head the row's name and a rule, then its rows; the head goes
    /// back, the row it opened from lit. §288: every widget's and every level of it one height.
    /// </summary>
    [UiFact]
    public void EveryWidgetsMenuOpensItsLevelsInPlace()
    {
        WindowShots.OnHiddenDesktop("inline-every", (gfx, instance) =>
        {
            var levels = 0;
            using var menu = new Flyout(gfx, instance, 0) { Configures = true, Configuration = true };
            foreach (var (name, items) in EveryWidgetsMenu())
            {
                menu.Show(gfx, AddListPictures.LookFor(true), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
                var window = menu.Hwnd;
                // §288: every widget's menu and every level of it, a field's too, the one height, where the
                // menu stood; back, the menu as it opened.
                var opened = Panel(menu);
                void Same(string where) => OneHeightAt(menu, opened, $"{name}: {where}");
                Same("its own rows");
                foreach (var row in items.Where(item => item.Children is { Count: > 0 } && item.Enabled))
                {
                    Assert.Equal(default, menu.Click(row.Id));
                    Assert.True((1, row.Text) == (menu.InlineDepth, menu.InlineTitle), $"{name}: {row.Text} opened no level in place");
                    Assert.Equal(window, Assert.Single(menu.OpenPanels).Window);
                    Assert.Equal((Flyout.InlineBackId, row.Text), (menu.RowsShown[0].Id, menu.RowsShown[0].Text));
                    Same(row.Text);
                    levels++;
                    foreach (var deeper in row.Children!.Where(item => item.Children is { Count: > 0 } && item.Enabled))
                    {
                        Assert.Equal(default, menu.Click(deeper.Id));
                        Assert.True((2, deeper.Text) == (menu.InlineDepth, menu.InlineTitle), $"{name}: {row.Text} › {deeper.Text} opened no level in place");
                        Assert.Single(menu.OpenPanels);
                        Same($"{row.Text} › {deeper.Text}");
                        Assert.Equal(default, menu.Click(Flyout.InlineBackId));
                        Assert.Equal(1, menu.InlineDepth);
                        Same($"back to {row.Text}");
                        levels++;
                    }
                    // §409: back by the pointer, what is under it lights.
                    var head = menu.RowMiddle(Flyout.InlineBackId)!.Value;
                    Assert.Equal(default, menu.Click(Flyout.InlineBackId));
                    Assert.True((0, menu.IdAt(head)) == (menu.InlineDepth, menu.HotId), $"{name}: back from {row.Text}");
                    Assert.True(menu.IsOpen);
                    Assert.True(opened.Equals(Panel(menu)), $"{name}: back from {row.Text} is not the menu as it opened");
                }
            }
            // Every kind's settings were walked, not none.
            Assert.True(levels > 100, $"{levels} levels");
        });
    }

    /// <summary>§271: a menu that is no widget's (the add list's subjects, Windows' "New ›") cascades as ever: a second window beside it.</summary>
    [UiFact]
    public void AMenuThatIsNoWidgetsStillCascades()
    {
        WindowShots.OnHiddenDesktop("inline-cascade", (gfx, instance) =>
        {
            using var menu = Show(gfx, instance, Deep(), configures: false);
            _ = menu.Click(2);
            Assert.Equal((2, 0), (menu.OpenPanels.Count, menu.InlineDepth));
            Assert.DoesNotContain(menu.RowsShown, item => item.Id == Flyout.InlineBackId);
            // The pointer into the submenu gives it the keys; Escape shuts it, the menu staying.
            menu.PointTo(21);
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Single(menu.OpenPanels);
            Assert.True(menu.IsOpen);
        });
    }

    // --- A level in place ---------------------------------------------------------------------------

    /// <summary>
    /// §268: a click on the chart range opens no window: the one window shows the range's head and its
    /// choices, kept from its foot, its left where it was; a click on the head goes back, what is under the
    /// pointer lit (§409), the menu as it was; a pick and the Refresh for it go back the same, the tick moved (§392).
    /// </summary>
    [UiFact]
    public void AClickSwapsTheRowsInPlaceAndTheHeadGoesBack()
    {
        WindowShots.OnHiddenDesktop("inline-click", (gfx, instance) =>
        {
            var (items, picks, line) = Markets();
            using var menu = Show(gfx, instance, items);
            var window = menu.Hwnd;
            var before = Panel(menu);
            var range = items.Single(item => item.Text == S.SettingsWidgetMarketRangeHeader);

            Assert.Equal(default, menu.Click(range.Id));
            WindowShots.Settle();
            var level = Panel(menu);
            Assert.Equal(window, menu.Hwnd);
            Assert.Equal((1, range.Text), (menu.InlineDepth, menu.InlineTitle));
            var shown = menu.RowsShown;
            Assert.Equal(Flyout.InlineBackId, shown[0].Id);
            Assert.Equal(range.Text, shown[0].Text);
            Assert.Equal(range.Children!.Where(item => item.Enabled && !item.Header && !item.Separator).Select(item => item.Id), shown.Skip(1).Select(item => item.Id));
            // Where the menu stood, one width (§230), as tall as its rows (§286).
            AsTallAsItsRows(menu, before, range.Text);

            // The head goes back, what is under the pointer lit (§409), the menu as it was.
            var head = menu.RowMiddle(Flyout.InlineBackId)!.Value;
            Assert.Equal(default, menu.Click(Flyout.InlineBackId));
            WindowShots.Settle();
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal(menu.IdAt(head), menu.HotId);
            Assert.Equal(before, Panel(menu));
            Assert.True(menu.IsOpen);

            // A choice picked: shown again with the setting as it is now, back on the menu's own rows (§392),
            // what is under the pointer lit (§409), the tick moved under it, the menu as it was.
            _ = menu.Click(range.Id);
            WindowShots.Settle();
            Assert.Equal(level, Panel(menu));
            var pick = range.Children!.First(item => item.Checked != true && item.Stays);
            var picked = menu.RowMiddle(pick.Id)!.Value;
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, pick.Id), menu.Click(pick.Id));
            Assert.True(OptionMenu.Apply(line, OptionMenu.PickOf(picks, pick.Id)!));
            List<OptionPick> again = [];
            menu.Refresh(OptionMenu.Updated(menu.ShownItems, OptionMenu.Section(OptionRows.Of(line), again)));
            WindowShots.Settle();
            Assert.Equal((0, menu.IdAt(picked)), (menu.InlineDepth, menu.HotId));
            Assert.True(menu.ShownItems.Single(item => item.Text == range.Text).Children!.Single(item => item.Text == pick.Text).Checked);
            Assert.Equal(before, Panel(menu));
        });
    }

    /// <summary>
    /// §268: Right, Enter and Space go in, onto the level's first row; Left, Backspace, Escape and the
    /// mouse's back button go back, the row it opened from lit by the keys (the back button's, what is under
    /// the pointer, §409); Escape on the menu's own rows closes it.
    /// </summary>
    [UiFact]
    public void TheKeysAndTheBackButtonGoInAndBack()
    {
        WindowShots.OnHiddenDesktop("inline-keys", (gfx, instance) =>
        {
            var (items, _, _) = Markets();
            using var menu = Show(gfx, instance, items);
            var range = items.Single(item => item.Text == S.SettingsWidgetMarketRangeHeader);
            var first = range.Children!.First(item => item.Enabled && !item.Header && !item.Separator);
            KeysTo(menu, range.Id);
            foreach (var (into, back) in new[] { (VK.VK_RIGHT, VK.VK_LEFT), (VK.VK_RETURN, VK.VK_BACK), (VK.VK_SPACE, VK.VK_ESCAPE) })
            {
                Assert.Equal(FlyoutEventKind.Nothing, Key(menu, into).Kind);
                Assert.Equal((1, first.Id), (menu.InlineDepth, menu.HotId!.Value));
                Assert.Single(menu.OpenPanels);
                Assert.Equal(FlyoutEventKind.Nothing, Key(menu, back).Kind);
                Assert.Equal((0, range.Id), (menu.InlineDepth, menu.HotId!.Value));
                Assert.True(menu.IsOpen);
            }
            // The mouse's back button (XBUTTON1).
            _ = Key(menu, VK.VK_RIGHT);
            // The pointer's: at the window's corner, on no row, nothing lit (§409).
            Assert.Equal(FlyoutEventKind.Nothing, menu.Message(WM.WM_XBUTTONUP, (WPARAM)(nuint)(XBUTTON1 << 16), 0).Kind);
            Assert.Equal((0, (uint?)null), (menu.InlineDepth, menu.HotId));
            // Up onto the head and Enter: back too.
            KeysTo(menu, range.Id);
            _ = Key(menu, VK.VK_RIGHT);
            _ = Key(menu, VK.VK_UP);
            Assert.Equal(Flyout.InlineBackId, menu.HotId);
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal((0, range.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal(FlyoutEventKind.Dismissed, Key(menu, VK.VK_ESCAPE).Kind);
            Assert.False(menu.IsOpen);
        });
    }

    /// <summary>
    /// §268: three levels deep in place and back a level at a time, each lit where it was left; a Refresh
    /// keeps the deepest, and one without the row a level opened from falls back to the menu's own rows.
    /// </summary>
    [UiFact]
    public void LevelsGoAsDeepAsTheyGoAndARefreshKeepsThem()
    {
        WindowShots.OnHiddenDesktop("inline-deep", (gfx, instance) =>
        {
            using var menu = Show(gfx, instance, Deep());
            _ = menu.Click(2);
            _ = menu.Click(23);
            _ = menu.Click(232);
            Assert.Equal((3, "Shade"), (menu.InlineDepth, menu.InlineTitle));
            Assert.Equal([Flyout.InlineBackId, 2321u, 2322u], menu.RowsShown.Select(item => item.Id));
            Assert.Single(menu.OpenPanels);

            menu.PointTo(2322);
            menu.Refresh(Deep());
            Assert.Equal((3, 2322u), (menu.InlineDepth, menu.HotId!.Value));

            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal((2, 232u), (menu.InlineDepth, menu.HotId!.Value));
            _ = Key(menu, VK.VK_LEFT);
            Assert.Equal((1, 23u), (menu.InlineDepth, menu.HotId!.Value));
            _ = Key(menu, VK.VK_BACK);
            Assert.Equal((0, 2u), (menu.InlineDepth, menu.HotId!.Value));

            _ = menu.Click(2);
            _ = menu.Click(23);
            menu.Refresh(Deep(withStyle: false));
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal([1u, 3u], menu.RowsShown.Select(item => item.Id));
            Assert.True(menu.IsOpen);
        });
    }

    /// <summary>
    /// §286: every level is as tall as its rows, its foot over the bar — three levels deep and back, the
    /// menu as it opened —; a two-row menu is shorter than a three-row one; a Refresh with more rows or
    /// fewer makes the level taller or shorter from its foot; eighty are searched, ten rows' room, scrolling; back, the menu as
    /// it opened.
    /// </summary>
    [UiFact]
    public void EveryLevelIsAsTallAsItsRows()
    {
        WindowShots.OnHiddenDesktop("inline-height", (gfx, instance) =>
        {
            using var menu = Show(gfx, instance, Deep());
            var opened = Panel(menu);
            AsTallAsItsRows(menu, opened, "its own rows");
            using (var other = Show(gfx, instance, [new FlyoutItem(1, "Open"), new FlyoutItem(2, "Close")]))
            {
                var two = Panel(other);
                Assert.True(two.bottom - two.top < opened.bottom - opened.top);
            }
            var heights = new List<int>();
            foreach (var id in new uint[] { 2, 23, 232 })
            {
                _ = menu.Click(id);
                AsTallAsItsRows(menu, opened, $"level {id}");
                heights.Add(Panel(menu).bottom - Panel(menu).top);
            }
            // Style (three rows), its Colour (two), the Shade (two): a level of more rows is taller.
            Assert.True(heights[0] > heights[1] && heights[1] == heights[2], string.Join(", ", heights));
            foreach (var back in new[] { VK.VK_ESCAPE, VK.VK_LEFT })
            {
                _ = Key(menu, back);
                AsTallAsItsRows(menu, opened, $"back by {back}");
            }
            _ = Key(menu, VK.VK_BACK);
            Assert.Equal(opened, Panel(menu));
            _ = menu.Click(3);
            AsTallAsItsRows(menu, opened, "View");
            var view = Panel(menu);

            IReadOnlyList<FlyoutItem> Sizes(int count) =>
                [.. Deep().Select(item => item.Id == 3 ? item with { Children = [.. Enumerable.Range(0, count).Select(n => new FlyoutItem(40 + (uint)n, $"Size {n}", Stays: true))] } : item)];
            menu.Refresh(Sizes(9));
            AsTallAsItsRows(menu, opened, "nine sizes");
            var nine = Panel(menu);
            Assert.True(nine.top < view.top);
            menu.Refresh(Sizes(1));
            Assert.Equal(1, menu.InlineDepth);
            AsTallAsItsRows(menu, opened, "one size");
            Assert.True(Panel(menu).top > view.top);
            menu.Refresh(Sizes(80));
            // A long list: searched, its field and ten rows' room (§279).
            AsTallAsItsRows(menu, opened, "eighty sizes", Sizes(80).Single(item => item.Id == 3).Children);
            Assert.True(menu.ContentHeight > menu.ShownHeight);
            menu.ScrollForTest(10_000);
            Assert.True(menu.Scrolls[0] > 0);
            _ = menu.Click(Flyout.InlineBackId);
            Assert.Equal((0, opened), (menu.InlineDepth, Panel(menu)));
        });
    }

    /// <summary>§276, §286: where the room over the bar is shorter than the menu's rows, the menu is the room's height and scrolls; a short level is its rows'.</summary>
    [UiFact]
    public void TheRoomOverTheBarCapsTheHeight()
    {
        WindowShots.OnHiddenDesktop("inline-capped", (gfx, instance) =>
        {
            var low = Work with { top = Work.bottom - (int)(300 * Scale) };
            List<FlyoutItem> items = [.. Deep(), .. Enumerable.Range(0, 20).Select(n => new FlyoutItem(100 + (uint)n, $"Row {n}"))];
            using var menu = Show(gfx, instance, items, work: low);
            var opened = Panel(menu);
            Assert.True(opened.top >= low.top && opened.bottom <= low.bottom, $"{opened.top}-{opened.bottom} out of {low.top}-{low.bottom}");
            Assert.True(menu.ContentHeight > menu.ShownHeight);
            Assert.True(opened.top - low.top < 48 * Scale, $"{opened.top - low.top} px of the room left over it");
            _ = menu.Click(2);
            var level = Panel(menu);
            Assert.Equal((1, opened.bottom), (menu.InlineDepth, level.bottom));
            Assert.True(MathF.Abs(menu.ShownHeight - menu.ContentHeight) < 1);

            // §288: a configuration menu the room's height too, and its short level the same.
            using var settings = Show(gfx, instance, items, work: low, configuration: true);
            var capped = Panel(settings);
            Assert.True(capped.bottom - capped.top < OneHeight && capped.top - low.top < 48 * Scale, $"{capped.top}-{capped.bottom} in {low.top}-{low.bottom}");
            _ = settings.Click(2);
            OneHeightAt(settings, capped, "Style", capped.bottom - capped.top);
            Assert.True(settings.ShownHeight > settings.ContentHeight);
        });
    }

    /// <summary>A folder line's menu as edit mode has it (Dock.WidgetMenu): its rows, its settings and its height, with <paramref name="more"/> rows over the height.</summary>
    private static List<FlyoutItem> LineMenu(int more)
    {
        var line = ListKind.Calendar.New();
        return
        [
            .. TheDock.WidgetRowsFor(line),
            .. OptionMenu.Section(OptionRows.Of(line, new OptionDevices()), []),
            .. Enumerable.Range(0, more).Select(n => new FlyoutItem(900 + (uint)n, $"Row {n}")),
            .. OptionMenu.Foot([.. LineHeightRows.Rows(LineHeightRows.For(line)!.Value)]),
        ];
    }

    /// <summary>A folder's menu on an arranged bar (Dock.WidgetMenu) wearing <paramref name="clocks"/> clocks: its rows, its settings and its faces; and "Add a face", its last row.</summary>
    private static (List<FlyoutItem> Items, uint Add) FacesMenu(int clocks)
    {
        var folder = BarKind.FolderList.New();
        folder.Id = "box";
        folder.Folder!.Faces = [.. Enumerable.Range(0, clocks).Select(_ => FolderFace.Of(FolderFaces.ClockId))];
        List<OptionPick> picks = [];
        List<FlyoutItem> items = [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.Section(OptionRows.Of(folder), picks), .. OptionMenu.FaceSection(folder.Folder, picks)];
        return (items, items[^1].Id);
    }

    /// <summary>
    /// §314, the person: "add a face should not be bottom aligned anymore but right under the faces list".
    /// A folder's "Add a face ›" is the faces' last row, a row's pitch under the last face and no rule
    /// between, under two faces (the room under it empty) and under forty (a scroll away, with them); the
    /// keys go down to it and a click opens its level, back to it where it stood. §280 to §313 stood it still
    /// at the menu's foot.
    /// </summary>
    [UiFact]
    public void AddAFaceStandsRightUnderTheFaces()
    {
        WindowShots.OnHiddenDesktop("inline-add-face-under", (gfx, instance) =>
        {
            foreach (var count in new[] { 2, 40 })
            {
                var (items, add) = FacesMenu(count);
                Assert.Equal(items.Count, Flyout.FootFrom(items));
                var faces = items.Where(item => item.Reorder == OptionMenu.FaceGroup).Select(item => item.Id).ToList();
                Assert.Equal(faces[^1], items[^2].Id);
                using var menu = Show(gfx, instance, items, configuration: true);
                var opened = Panel(menu);
                Assert.Equal(count > 2, menu.ContentHeight > menu.ShownHeight);
                menu.ScrollForTest(10_000);
                var pitch = menu.RowFill(faces[1])!.Value.Middle.y - menu.RowFill(faces[0])!.Value.Middle.y;
                var last = menu.RowFill(faces[^1])!.Value;
                var under = menu.RowFill(add)!.Value;
                Assert.Equal(last.Middle.y + pitch, under.Middle.y);
                // Under few faces, the room under it is the menu's one height's, empty (§288).
                Assert.True(count > 2 || menu.ShownHeight > menu.ContentHeight);

                // From the top, the keys go down to it, and a click opens its level, back to it.
                menu.ScrollForTest(0);
                KeysTo(menu, add);
                var keyed = menu.RowFill(add);
                Assert.Equal(default, menu.Click(add));
                Assert.Equal(1, menu.InlineDepth);
                OneHeightAt(menu, opened, "Add a face");
                // Back by the keys: lit as the keys lit it (§409); §411: the first Escape lets its search field's keys go.
                _ = Key(menu, VK.VK_ESCAPE);
                _ = Key(menu, VK.VK_ESCAPE);
                Assert.Equal((0, add), (menu.InlineDepth, menu.HotId ?? 0));
                Assert.Equal(keyed, menu.RowFill(add));
            }
        });
    }

    /// <summary>
    /// §314, the person: "height and appearance should not have a divider between them in the menus". The
    /// foot is its rows under one rule (OptionMenu.Foot): a line's Height and Appearance, the rule over
    /// the two and none between; a rule at the end alone is no foot, and a rule between two foot rows is
    /// where the foot begins, the rows over it scrolling (§281's stacking is gone).
    /// </summary>
    [Fact]
    public void TheFootIsItsRowsUnderOneRule()
    {
        var line = ListKind.Calendar.New();
        var place = new OptionPlace(new Config(), true);
        var rows = OptionMenu.Settings(line, [], new OptionDevices(), place, FolderSide.Face, roomCells: LineHeight.Most);
        var from = Flyout.FootFrom(rows);
        Assert.Equal(rows.Count - 3, from);
        Assert.True(rows[from].Separator);
        // §567: no Look › beside its Height any more.
        Assert.Equal([LineHeightRows.HeightId, LineHeightRows.FewerId], rows.Skip(from + 1).Select(row => row.Id));
        Assert.Single(rows.Skip(from), row => row.Separator);

        Assert.Empty(OptionMenu.Foot(null, null));
        Assert.Equal(2, Flyout.FootFrom([new(1, "Open"), new(0, "", Separator: true)]));
        List<FlyoutItem> ruled = [new(1, "Open"), new(0, "", Separator: true), new(2, "Height", Foot: true), new(0, "", Separator: true), new(3, "Look", Foot: true)];
        Assert.Equal(3, Flyout.FootFrom(ruled));
    }

    /// <summary>
    /// §276: a folder line's "Height ›" is the menu's last row and stands still at its foot — in one place
    /// on the screen in a short menu and in a long one, the configuration menu's one height (§288), which
    /// scrolls its other rows over it and never moves it —, the
    /// pointer and the keys find it there, and its own level fits the menu without scrolling.
    /// </summary>
    [UiFact]
    public void TheHeightRowStandsStillAtTheMenusFoot()
    {
        WindowShots.OnHiddenDesktop("inline-foot", (gfx, instance) =>
        {
            (POINT Middle, float W, float H) shortFoot;
            using (var brief = Show(gfx, instance, LineMenu(0), configuration: true))
            {
                Assert.True(brief.ShownHeight > brief.ContentHeight);
                shortFoot = OnScreen(brief, LineHeightRows.HeightId);
            }
            using var menu = Show(gfx, instance, LineMenu(40), configuration: true);
            var opened = Panel(menu);
            Assert.Equal(LineHeightRows.FewerId, menu.RowsShown[^1].Id);
            Assert.True(menu.ContentHeight > menu.ShownHeight);
            var foot = menu.RowFill(LineHeightRows.HeightId)!.Value;
            Assert.Equal(shortFoot, OnScreen(menu, LineHeightRows.HeightId));
            var last = menu.RowFill(939)!.Value;
            Assert.True(last.Middle.y > foot.Middle.y, "the last row over the height is out of view under it");

            // Scrolled to the end: the height where it stood, the last row over it.
            menu.ScrollForTest(10_000);
            Assert.True(menu.Scrolls[0] > 0);
            Assert.Equal(foot, menu.RowFill(LineHeightRows.HeightId));
            last = menu.RowFill(939)!.Value;
            Assert.True(last.Middle.y + (last.H / 2) <= foot.Middle.y - (foot.H / 2), $"the last row ends at {last.Middle.y + (last.H / 2)}, under the height's top {foot.Middle.y - (foot.H / 2)}");

            // From the top, the keys go down to it without scrolling it away, and a click opens its level.
            menu.ScrollForTest(0);
            KeysTo(menu, LineHeightRows.HeightId);
            Assert.Equal(foot, menu.RowFill(LineHeightRows.HeightId));
            Assert.Equal(default, menu.Click(LineHeightRows.HeightId));
            Assert.Equal(1, menu.InlineDepth);
            OneHeightAt(menu, opened, "Height");
            Assert.True(menu.ContentHeight <= menu.ShownHeight);
            // Back by the keys: lit as the keys lit it (§409).
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal((0, LineHeightRows.HeightId), (menu.InlineDepth, menu.HotId ?? 0));
            Assert.Equal(foot, menu.RowFill(LineHeightRows.HeightId));
        });
    }

    /// <summary>§271: a folder's face opens its own settings in place, under its name; a face's settings' own levels in place again.</summary>
    [UiFact]
    public void AFolderFaceOpensItsSettingsInPlace()
    {
        WindowShots.OnHiddenDesktop("inline-faces", (gfx, instance) =>
        {
            var (items, _, _) = Folder();
            using var menu = Show(gfx, instance, items);
            var clock = items.Single(item => item.Reorder != 0 && item.Text == "Clock");
            _ = menu.Click(clock.Id);
            Assert.Equal((1, "Clock"), (menu.InlineDepth, menu.InlineTitle));
            // Its 12 or 24 hours, a choice's level (its zone, first since §292, is a search's).
            var hours = clock.Children!.First(item => item.Children is { Count: > 0 } level && !level.Any(row => row.Control is FlyoutField));
            _ = menu.Click(hours.Id);
            Assert.Equal((2, hours.Text), (menu.InlineDepth, menu.InlineTitle));
            Assert.Single(menu.OpenPanels);
            _ = Key(menu, VK.VK_ESCAPE);
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal((0, clock.Id), (menu.InlineDepth, menu.HotId!.Value));
        });
    }

    // --- The fields ------------------------------------------------------------------------------------

    /// <summary>
    /// §268: the symbol's search level, in place, has the keys at once and its rows before anything is
    /// typed; "eur" typed, the service's answer comes into the same window, its size kept; Escape gives the
    /// field's words back, lets the keys go, and then goes back to the menu's rows, the symbol's lit.
    /// </summary>
    [UiFact]
    public void TheSymbolsSearchGetsItsAnswerInPlace()
    {
        WindowShots.OnHiddenDesktop("inline-search", (gfx, instance) =>
        {
            var (items, _, _) = Markets();
            var symbol = items.Single(item => item.Text == S.SettingsWidgetMarketSymbolHeader);
            var field = symbol.Children!.Single(item => item.Control is FlyoutField);
            using var menu = Show(gfx, instance, items);
            _ = menu.Click(symbol.Id);
            WindowShots.Settle();
            Assert.Equal((1, field.Id), (menu.InlineDepth, menu.FieldKeys));
            var search = menu.FieldSearchOf(field.Id)!;
            // §411: then well-known ones.
            Assert.Equal(["CHF/USD", "Apple Inc.", "BTC/USD", "Microsoft Corporation"], search.Rows.Take(4).Select(row => row.Title));
            var opened = Panel(menu);

            Type(menu, "eur");
            PumpUntil(menu, () => search.Rows.Count == 7);
            Assert.Equal(7, search.Rows.Count);
            // Every one a row (no "Show more"), the panel as it opened.
            Assert.Equal(search.Rows.Select(row => row.Title), menu.RowsShown.Skip(2).Select(item => item.Text));
            Assert.Equal(opened, Panel(menu));
            AsTallAsItsRows(menu, opened, "the answer", symbol.Children);
            Assert.Equal(1, menu.InlineDepth);

            var escapes = 0;
            while (menu.InlineDepth > 0 && escapes < 5)
            {
                Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_ESCAPE).Kind);
                escapes++;
            }
            Assert.Equal((0, 3), (menu.InlineDepth, escapes));
            Assert.Equal((symbol.Id, 0u), (menu.HotId!.Value, menu.FieldKeys));
            Assert.True(menu.IsOpen);
        });
    }

    /// <summary>§268, §246: the amount owned, a line of text's level in place: Enter keeps what was typed and goes back, the menu saying so.</summary>
    [UiFact]
    public void EnterKeepsTheAmountAndGoesBack()
    {
        WindowShots.OnHiddenDesktop("inline-amount", (gfx, instance) =>
        {
            var line = ListKind.Markets.New();
            line.Market = new MarketOptions { Symbol = "AAPL" };
            List<OptionPick> picks = [];
            var items = OptionMenu.Section(OptionRows.Of(line), picks);
            var amount = items.Single(item => item.Text == S.SettingsWidgetMarketHoldingHeader);
            var field = Assert.Single(amount.Children!);
            using var menu = Show(gfx, instance, items);
            _ = menu.Click(amount.Id);
            Assert.Equal((1, field.Id), (menu.InlineDepth, menu.FieldKeys));
            Type(menu, "2,5");
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_RETURN).Kind);
            Assert.Equal((0, amount.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Assert.Single(Pump(menu)));
            var kept = menu.TakeFieldCommit(field.Id)!;
            Assert.True(FieldPicks.Keep(line, OptionMenu.PickOf(picks, field.Id)!, kept));
            Assert.Equal(2.5, line.Market!.Holding);
            Assert.True(menu.IsOpen);
        });
    }

    // --- Every result, one room -------------------------------------------------------------------------

    /// <summary>
    /// §271: a zone's level lists every zone Windows has, each a row and no "Show more", in the room its
    /// level keeps — the configuration menu's one height (§288) — while it is typed in and finds fewer; the wheel
    /// scrolls the zones under the head and the field, which stand where they were and alone take the
    /// pointer there.
    /// </summary>
    [UiFact]
    public void AZonesLevelShowsEveryZoneInTheRoomItKeeps()
    {
        WindowShots.OnHiddenDesktop("inline-zones", (gfx, instance) =>
        {
            var (items, zoneRow) = Clock();
            var zone = Assert.Single(zoneRow.Children!);
            using var menu = Show(gfx, instance, items, configuration: true);
            var top = Panel(menu);
            _ = menu.Click(zoneRow.Id);
            WindowShots.Settle();
            Assert.Equal((1, zone.Id), (menu.InlineDepth, menu.FieldKeys));
            var search = menu.FieldSearchOf(zone.Id)!;
            Assert.Equal(TimeZoneSearch.Windows.Count + 1, search.Rows.Count);
            Assert.Equal(search.Rows.Select(row => row.Title), menu.RowsShown.SkipWhile(item => item.Id != zone.Id).Skip(1).Select(item => item.Text));
            var opened = Panel(menu);
            Assert.True(menu.ContentHeight > menu.ShownHeight);
            // The configuration menu's one height, from its foot (§271, §288).
            OneHeightAt(menu, top, "the zones");

            var head = menu.RowFill(Flyout.InlineBackId)!.Value.Middle;
            var field = menu.FieldBoxCentre(zone.Id)!.Value;
            var firstZone = menu.RowsShown[3].Id;
            var before = menu.RowFill(firstZone)!.Value.Middle;
            var screen = head;
            _ = ClientToScreen(menu.Hwnd, &screen);
            _ = menu.Message(WM.WM_MOUSEWHEEL, (WPARAM)(nuint)(unchecked((uint)(ushort)-240) << 16), At(screen));
            Assert.True(menu.Scrolls[0] > 0);
            Assert.True(menu.RowFill(firstZone)!.Value.Middle.y < before.y);
            Assert.Equal(head, menu.RowFill(Flyout.InlineBackId)!.Value.Middle);
            Assert.Equal(field, menu.FieldBoxCentre(zone.Id));
            // Over the field only it, never a zone scrolled under it.
            _ = menu.Message(WM.WM_MOUSEMOVE, 0, At(field));
            Assert.Equal(zone.Id, menu.HotId);

            // Typed in: fewer found, from the top, in the same room.
            Type(menu, "tok");
            Assert.Equal(0, menu.Scrolls[0]);
            Assert.True(search.Rows.Count < TimeZoneSearch.Windows.Count);
            Assert.Equal(opened, Panel(menu));
            Type(menu, "qqq");
            Assert.Equal(SearchPhase.Nothing, search.Phase);
            Assert.Equal(opened, Panel(menu));
        });
    }

    /// <summary>§271: an icon picker's level, in place: its head over its field and its tiles, both standing still as the tiles scroll.</summary>
    [UiFact]
    public void AnIconPickersHeadAndFieldStandOverItsTiles()
    {
        WindowShots.OnHiddenDesktop("inline-picker", (gfx, instance) =>
        {
            var (items, picks, _) = Folder();
            using var menu = Show(gfx, instance, items);
            var face = items.Single(item => item.Reorder != 0 && item.Children?.Any(child => OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon) == true);
            var icon = face.Children!.Single(child => OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon);
            _ = menu.Click(face.Id);
            _ = menu.Click(icon.Id);
            WindowShots.Settle();
            var field = icon.Children![0];
            Assert.Equal((2, field.Id), (menu.InlineDepth, menu.FieldKeys));
            Assert.Equal([Flyout.InlineBackId, field.Id], menu.RowsShown.Take(2).Select(item => item.Id));
            var head = menu.RowFill(Flyout.InlineBackId)!.Value.Middle;
            var box = menu.FieldBoxCentre(field.Id)!.Value;
            Assert.True(head.y < box.y);
            menu.ScrollForTest(10_000);
            Assert.True(menu.Scrolls[0] > 0);
            Assert.Equal(head, menu.RowFill(Flyout.InlineBackId)!.Value.Middle);
            Assert.Equal(box, menu.FieldBoxCentre(field.Id));
        });
    }

    // --- Pictures ---------------------------------------------------------------------------------------

    /// <summary>
    /// §271's pictures in KARPO_PNG_DIR, at 150 %, dark and light: a markets line's menu and its chart
    /// range's level (inline-markets-top-, inline-markets-range-<look>.png), its symbol's search with "eur"
    /// typed (inline-markets-search-), a clock's zone level scrolled half way (inline-zones-scrolled-), a
    /// folder's menu with its faces and its clock face's level (inline-folder-top-, inline-folder-clock-),
    /// and the icon face's picker (inline-folder-picker-).
    /// </summary>
    [UiFact]
    public void InlinePictures()
    {
        WindowShots.OnHiddenDesktop("inline-pictures", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var backdrop = dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243);
                bool Save(Flyout menu, string name)
                {
                    WindowShots.Settle(300);
                    return WindowShots.SaveTogether([.. menu.OpenPanels], $"{name}-{tone}.png", backdrop);
                }
                var (items, _, _) = Markets();
                var range = items.Single(item => item.Text == S.SettingsWidgetMarketRangeHeader);
                var symbol = items.Single(item => item.Text == S.SettingsWidgetMarketSymbolHeader);
                using (var menu = Show(gfx, instance, items, dark: dark))
                {
                    Assert.True(Save(menu, "inline-markets-top"));
                    _ = menu.Click(range.Id);
                    menu.PointTo(range.Children!.First(item => item.Checked != true && item.Stays).Id);
                    Assert.True(Save(menu, "inline-markets-range"));
                }
                using (var menu = Show(gfx, instance, items, dark: dark))
                {
                    _ = menu.Click(symbol.Id);
                    Type(menu, "eur");
                    var search = menu.FieldSearchOf(symbol.Children!.Single(item => item.Control is FlyoutField).Id)!;
                    PumpUntil(menu, () => search.Rows.Count == 7);
                    Assert.True(Save(menu, "inline-markets-search"));
                }
                var (clock, zoneRow) = Clock();
                using (var menu = Show(gfx, instance, clock, dark: dark))
                {
                    _ = menu.Click(zoneRow.Id);
                    menu.ScrollForTest(menu.ContentHeight / 2);
                    Assert.True(Save(menu, "inline-zones-scrolled"));
                }
                var (faces, picks, _) = Folder();
                using (var menu = Show(gfx, instance, faces, dark: dark))
                {
                    Assert.True(Save(menu, "inline-folder-top"));
                    var face = faces.Single(item => item.Reorder != 0 && item.Text == "Clock");
                    _ = menu.Click(face.Id);
                    Assert.True(Save(menu, "inline-folder-clock"));
                }
                using (var menu = Show(gfx, instance, faces, dark: dark))
                {
                    var face = faces.Single(item => item.Reorder != 0 && item.Children?.Any(child => OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon) == true);
                    _ = menu.Click(face.Id);
                    _ = menu.Click(face.Children!.Single(child => OptionMenu.PickOf(picks, child.Id)?.Key == OptionPickers.FolderIcon).Id);
                    Assert.True(Save(menu, "inline-folder-picker"));
                }
            }
        });
    }

    /// <summary>
    /// §276's and §288's pictures in KARPO_PNG_DIR, at 150 %, dark: three widgets' configuration menus side
    /// by side, one height — a folder's with its faces, a clock line's, a markets line's
    /// (inline-height-widgets-dark.png); a folder line's menu short and long, its height at the foot of
    /// both, the long one scrolled to its end beside it (inline-height-foot-), and the height's own level
    /// (inline-height-level-); a folder's menu under two faces and forty, at their top and end, "Add a face"
    /// at the foot of all (inline-add-face-foot-, §280).
    /// </summary>
    [UiFact]
    public void InlineHeightPictures()
    {
        WindowShots.OnHiddenDesktop("inline-height-pictures", (gfx, instance) =>
        {
            var black = ((byte)0, (byte)0, (byte)0);
            POINT At(int third) => Anchor with { x = Work.right * third / 8 };
            bool Save(IEnumerable<Flyout> menus, string name)
            {
                WindowShots.Settle(300);
                return WindowShots.SaveTogether([.. menus.SelectMany(menu => menu.OpenPanels)], $"{name}-dark.png", black);
            }
            using (var folder = Show(gfx, instance, Folder().Items, anchor: At(1), configuration: true))
            using (var clock = Show(gfx, instance, Clock().Items, anchor: At(3), configuration: true))
            using (var markets = Show(gfx, instance, Markets().Items, anchor: At(5), configuration: true))
            {
                Assert.True(Save([folder, clock, markets], "inline-height-widgets"));
            }
            using var brief = Show(gfx, instance, LineMenu(0), anchor: At(1), configuration: true);
            using var top = Show(gfx, instance, LineMenu(40), anchor: At(3), configuration: true);
            using var end = Show(gfx, instance, LineMenu(40), anchor: At(5), configuration: true);
            end.ScrollForTest(10_000);
            Assert.True(Save([brief, top, end], "inline-height-foot"));
            _ = brief.Click(LineHeightRows.HeightId);
            Assert.True(Save([brief], "inline-height-level"));
            // §280: a folder's menu under two faces and under forty, at their top and their end.
            using var few = Show(gfx, instance, FacesMenu(2).Items, anchor: At(1), configuration: true);
            using var faces = Show(gfx, instance, FacesMenu(40).Items, anchor: At(3), configuration: true);
            using var facesEnd = Show(gfx, instance, FacesMenu(40).Items, anchor: At(5), configuration: true);
            facesEnd.ScrollForTest(10_000);
            Assert.True(Save([few, faces, facesEnd], "inline-add-face-foot"));
        });
    }

    /// <summary>
    /// §286, the person: "the right click context menus on the folders should not have a fixed height, these
    /// should change with their contents". A folder's right click at rest, of one face and of several, each
    /// as tall as its rows over the bar, the one of more rows the taller. §288: "basically only the context
    /// menus in view mode should have a variable / automatic height": arranged, its configuration menu is
    /// the one height with one face or three, a face's level too, and a clock's the same. Pictures, 150 %,
    /// dark: menu-height-folders-rest-dark (one face, three), and menu-height-folders-edit-dark (one face,
    /// three, a clock face's level, a clock line's menu).
    /// </summary>
    [UiFact]
    public void AFoldersMenuIsAsTallAsItsRowsAtRestAndOneHeightArranged()
    {
        WindowShots.OnHiddenDesktop("menu-height-folders", (gfx, instance) =>
        {
            var black = ((byte)0, (byte)0, (byte)0);
            POINT At(int eighth) => Anchor with { x = Work.right * eighth / 8 };
            bool Save(IEnumerable<Flyout> menus, string name)
            {
                WindowShots.Settle(300);
                return WindowShots.SaveTogether([.. menus.SelectMany(menu => menu.OpenPanels)], $"{name}-dark.png", black);
            }
            BarItem Folder(params string[] faces) => new() { Id = "f", Kind = BarKind.FolderList, Folder = new FolderOptions { Faces = [.. faces.Select(FolderFace.Of)] } };
            List<FlyoutItem> Arranged(BarItem folder)
            {
                List<OptionPick> picks = [];
                return [.. TheDock.WidgetRowsFor(folder), .. OptionMenu.Section(OptionRows.Of(folder), picks), .. OptionMenu.FaceSection(folder.Folder!, picks)];
            }
            int Tall(Flyout menu) => Panel(menu).bottom - Panel(menu).top;
            var one = Folder(FolderFaces.IconId);
            var three = Folder(FolderFaces.ClockId, FolderFaces.CalendarId, FolderFaces.SoundId);

            using (var few = Show(gfx, instance, TheDock.BarMenuRows(one).Items, anchor: At(1)))
            using (var more = Show(gfx, instance, TheDock.BarMenuRows(three).Items, anchor: At(4)))
            {
                AsTallAsItsRows(few, Panel(few), "one face at rest");
                AsTallAsItsRows(more, Panel(more), "three faces at rest");
                Assert.True(Tall(few) < Tall(more), $"{Tall(few)} px, {Tall(more)} px");
                Assert.True(Save([few, more], "menu-height-folders-rest"));
            }

            var clock = Arranged(three).Single(item => item.Reorder != 0 && item.Text == "Clock");
            using (var few = Show(gfx, instance, Arranged(one), anchor: At(1), configuration: true))
            using (var more = Show(gfx, instance, Arranged(three), anchor: At(3), configuration: true))
            using (var level = Show(gfx, instance, Arranged(three), anchor: At(5), configuration: true))
            using (var line = Show(gfx, instance, Clock().Items, anchor: At(7), configuration: true))
            {
                OneHeightAt(few, Panel(few), "one face arranged");
                OneHeightAt(more, Panel(more), "three faces arranged");
                OneHeightAt(line, Panel(line), "a clock line");
                var opened = Panel(level);
                _ = level.Click(clock.Id);
                Assert.Equal((1, "Clock"), (level.InlineDepth, level.InlineTitle));
                OneHeightAt(level, opened, "the clock face's level");
                Assert.True(Save([few, more, level, line], "menu-height-folders-edit"));
            }
        });
    }
}
