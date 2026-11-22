// menus: text and search fields on a real menu (Karpo.Dock\Flyout.Fields.cs, docs\widgets.md §219),
// on a desktop of the test's own (KARPO_UI_TESTS=1). Held here:
//
//  - **A line of text is a level of its own** (§246). Opened by a click or the keys, its field has
//    the keys at once, its words selected; the menu's shortcut (Delete, "Remove") and its arrows Left
//    and Right never act then; Escape puts back and shuts the level, and only the next closes the
//    menu; Enter keeps what was typed, the menu saying so (Adjust) and staying open, the level shut.
//    A stock's amount owned too, words that are no number changing nothing.
//  - **A search's results are rows.** A zone's show as the field takes the keys and as it is typed in;
//    Down goes into them and Enter picks, the menu staying open; an answer off the menu's thread
//    shows "Searching…" first and its rows once it is in.
//  - **Pictures** in KARPO_PNG_DIR, at 150 %, dark and light: a clock face's time zone search with its
//    results (menu-field-zone-<look>.png), a weather face's place search under its device switch
//    (menu-field-place-<look>.png; §240), a timer's name's row (menu-field-text-rest-<look>.png), its
//    level just opened (menu-field-text-open-<look>.png) and typed in (menu-field-text-<look>.png);
//    and in Arabic, mirrored (menu-field-rtl.png).

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using Karpo.Tests.Localization;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Menus;

[Collection(InLanguage.Collection)]
public sealed unsafe class MenuFieldTests
{
    private const float Scale = 1.5f;

    private static readonly RECT Work = new() { right = (int)(1920 * Scale), bottom = (int)(1040 * Scale) };

    private static readonly POINT Anchor = new() { x = Work.right / 3, y = Work.bottom };

    /// <summary>Places for a picture, as Open-Meteo answers "bern": no network in a test.</summary>
    private sealed class CannedPlaces : OptionFinder
    {
        public override int LeastLetters => 2;

        public override bool Remote => true;

        /// <summary>§230: what it offers before anything is typed: the place's own.</summary>
        public IReadOnlyList<OptionFound> Own { get; init; } = [];

        public override IReadOnlyList<OptionFound> Suggested => Own;

        public override Task<IReadOnlyList<OptionFound>> FindAsync(string query, CancellationToken cancel) =>
            Task.Delay(60, cancel).ContinueWith(_ => Find(query), cancel, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.Default);

        private static IReadOnlyList<OptionFound> Find(string query)
        {
            Place[] places =
            [
                new("Bern", "Bern, Switzerland", 46.948, 7.447),
                new("Berne", "Indiana, United States", 40.658, -84.952),
                new("Bernalillo", "New Mexico, United States", 35.300, -106.551),
            ];
            return [.. places.Where(place => place.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase)).Select(place => new OptionFound(place.Name, place.Detail, place))];
        }

        public override string NoneFound(string query) => S.SettingsWidgetWeatherPlaceNotFound(query);
    }

    private static FlyoutEvent Key(Flyout menu, int key) => menu.Message(WM.WM_KEYDOWN, (WPARAM)(nuint)key, 0);

    private static void Type(Flyout menu, string text)
    {
        foreach (var c in text)
        {
            _ = menu.Message(WM.WM_CHAR, (WPARAM)(nuint)c, 0);
        }
    }

    /// <summary>
    /// The menu's own posted messages and timers handed to it for <paramref name="ms"/>, as the dock's
    /// window procedure hands them (the test's window class hands them nowhere): what it said.
    /// </summary>
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
            // A submenu's window has its own procedure, which hands them on itself. Everything else
            // dispatched too, the root's caret blink (WM_SYSTIMER) among it: left at the queue's
            // head, it held back a submenu's search answer behind it. Bounded, for a WM_PAINT nobody
            // validates.
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

    /// <summary>Down until the keys are on row <paramref name="id"/>.</summary>
    private static void KeysTo(Flyout menu, uint id)
    {
        for (var step = 0; step < 40 && menu.HotId != id; step++)
        {
            _ = Key(menu, VK.VK_DOWN);
        }
        Assert.Equal(id, menu.HotId);
    }

    private static bool Save(Flyout menu, string name, bool dark) =>
        WindowShots.SaveTogether([.. menu.OpenPanels], name, dark ? ((byte)0, (byte)0, (byte)0) : ((byte)243, (byte)243, (byte)243));

    private static Flyout Show(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, bool dark = true)
    {
        // A settings menu, whose levels open in place: the only menus with fields (Flyout.Inline.cs, §271).
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    /// <summary>A menu with a label's row, "Label  Home ›", opening a level of its field alone (§246).</summary>
    private static FlyoutItem[] Labelled(string text) =>
    [
        new(1, "Remove", Keys: MenuChord.Delete),
        new(2, "Label", Value: new FlyoutValue(text), Children: [new(20, "Label", Control: new FlyoutField(text, "Tokyo, Home, …", 40))]),
        new(3, "Settings"),
    ];

    /// <summary>
    /// §246: a line of text's level, opened by a click or the keys, has its field with the keys at once,
    /// its words selected, the caret at their end. Every key is the field's then — Delete (the menu's
    /// "Remove" would close it), Home, End —; Escape puts its words back and shuts the level at once, the
    /// keys back on its row and nothing kept; only the next Escape closes the menu.
    /// </summary>
    [UiFact]
    public void ATextLevelOpensWithItsFieldTakingTheKeys()
    {
        WindowShots.OnHiddenDesktop("menu-field-keys", (gfx, instance) =>
        {
            using var menu = Show(gfx, instance, Labelled("Home"));
            Assert.Equal(default, menu.Click(2));
            Assert.Equal((1, 20u), (menu.InlineDepth, menu.FieldKeys));
            var field = menu.FieldOf(20)!;
            Assert.Equal((0, 4, 4), (field.Editing.SelectionStart, field.Editing.SelectionLength, field.Editing.Caret));

            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_HOME).Kind);
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_DELETE).Kind);
            Assert.Equal("ome", field.Text);
            Assert.True(menu.IsOpen);
            Type(menu, "H");
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_END).Kind);
            Type(menu, " office");
            Assert.Equal("Home office", field.Text);

            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_ESCAPE).Kind);
            Assert.Equal((0, 0u, 2u), (menu.InlineDepth, menu.FieldKeys, menu.HotId!.Value));
            Assert.Empty(Pump(menu));
            Assert.Null(menu.TakeFieldCommit(20));

            // Opened by the keys: the field has them again, as the setting says.
            _ = Key(menu, VK.VK_RIGHT);
            Assert.Equal((1, 20u, "Home"), (menu.InlineDepth, menu.FieldKeys, menu.FieldOf(20)!.Text));
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_ESCAPE).Kind);
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal(FlyoutEventKind.Dismissed, Key(menu, VK.VK_ESCAPE).Kind);
        });
    }

    /// <summary>
    /// §246: Enter keeps what was typed — the menu says so (Adjust) and stays — and shuts the level, the
    /// keys back on its row, which says the setting as it is now once shown again; Enter with nothing
    /// changed shuts it and keeps nothing; words left there as the menu closes are kept.
    /// </summary>
    [UiFact]
    public void EnterKeepsWhatWasTypedAndShutsTheLevel()
    {
        WindowShots.OnHiddenDesktop("menu-field-enter", (gfx, instance) =>
        {
            using var menu = Show(gfx, instance, Labelled("Home"));
            _ = menu.Click(2);
            _ = Key(menu, VK.VK_END);
            Type(menu, " office");
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_RETURN).Kind);
            Assert.Equal((0, 0u, 2u), (menu.InlineDepth, menu.FieldKeys, menu.HotId!.Value));
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, 20), Assert.Single(Pump(menu)));
            Assert.Equal(new FieldCommit(20, "Home office", null), menu.TakeFieldCommit(20));
            Assert.Null(menu.TakeFieldCommit(20));
            Assert.True(menu.IsOpen);
            // Whoever showed it shows it again with the setting as it is now: its row says it.
            menu.Refresh(Labelled("Home office"));
            Assert.Equal("Home office", menu.ShownItems[1].Value!.Text);
            Assert.Equal(0, menu.InlineDepth);

            // Nothing changed: Enter shuts the level and keeps nothing.
            _ = menu.Click(2);
            Assert.Equal("Home office", menu.FieldOf(20)!.Text);
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(0, menu.InlineDepth);
            Assert.Empty(Pump(menu));

            // Left there as the menu closes: kept for whoever showed it.
            _ = menu.Click(2);
            _ = Key(menu, VK.VK_END);
            Type(menu, "!");
            menu.Hide();
            Assert.Equal([new FieldCommit(20, "Home office!", null)], menu.TakeFieldsLeft());
            Assert.Empty(menu.TakeFieldsLeft());
        });
    }

    /// <summary>
    /// §246 (the person: "amount you own should also get an additional level"): a share's amount is a
    /// row saying what is owned, opening its field alone; what Enter keeps is set as the box set it — a
    /// comma its decimals' mark —, words that are no number change nothing.
    /// </summary>
    [UiFact]
    public void TheAmountYouOwnIsALevelOfItsOwn()
    {
        WindowShots.OnHiddenDesktop("menu-field-amount", (gfx, instance) =>
        {
            var stock = ListKind.Markets.New();
            List<OptionPick> picks = [];
            var items = OptionMenu.Rows(OptionRows.Of(stock), picks);
            var amount = items.Single(item => item.Text == S.SettingsWidgetMarketHoldingHeader);
            // §411 (R4): empty, it says what applies, greyed.
            Assert.Equal(new FlyoutValue(S.SettingsWidgetMarketHoldingNone, Implied: true), amount.Value);
            var field = Assert.Single(amount.Children!);
            Assert.Equal(S.SettingsWidgetMarketHoldingNone, ((FlyoutField)field.Control!).Placeholder);

            string? Kept(string typed)
            {
                using var menu = ShowConfiguring(gfx, instance, items);
                Assert.Equal(default, menu.Click(amount.Id));
                Assert.Equal(field.Id, menu.FieldKeys);
                Type(menu, typed);
                _ = Key(menu, VK.VK_RETURN);
                Assert.Equal(0, menu.InlineDepth);
                _ = Pump(menu);
                var kept = menu.TakeFieldCommit(field.Id)!;
                return FieldPicks.Keep(stock, OptionMenu.PickOf(picks, field.Id)!, kept) ? OptionMenu.Rows(OptionRows.Of(stock), []).Single(item => item.Text == amount.Text).Value?.Text : null;
            }
            Assert.Null(Kept("ten"));
            Assert.Equal(0, stock.Market?.Holding ?? 0);
            Assert.Equal("2.5", Kept("2,5"));
            Assert.Equal(2.5, stock.Market!.Holding);
        });
    }

    /// <summary>
    /// §333: a bar's size is stepped and typed. A click on its row off the stepper, or Enter, opens its level:
    /// its field, the size selected, over the sizes offered — the other bars', then the usual ones, its own
    /// ticked. A number typed is its own row first; Enter keeps it, the level shut, set as the dock sets it
    /// (Dock.LooksKept). On the row Left and Right still step.
    /// </summary>
    [UiFact]
    public void ABarsSizeIsSteppedAndTyped()
    {
        WindowShots.OnHiddenDesktop("menu-field-size", (gfx, instance) =>
        {
            const string display = "\\\\.\\DISPLAY1";
            var config = new Config();
            config.EditScreen(display, "One");
            BarLooks.Add(config, "Work").Size = 64;
            var bar = new BarSpot(display, "One");
            List<OptionPick> picks = [];
            var items = Karpo.Dock.Dock.LooksItems(config, bar, dark: true, picks, LooksScope.Taskbar);
            var size = items.Single(item => item.Text == S.SettingsLayoutsSizeHeader);
            var field = Assert.Single(size.Children!);
            var stepper = Assert.IsType<FlyoutControl.Stepper>(size.Control);
            using var menu = ShowConfiguring(gfx, instance, items);

            var middle = menu.RowMiddle(size.Id)!.Value;
            var at = (nint)((middle.y << 16) | (middle.x & 0xFFFF));
            _ = menu.Message(WM.WM_MOUSEMOVE, 0, at);
            _ = menu.Message(WM.WM_LBUTTONDOWN, 1, at);
            _ = menu.Message(WM.WM_LBUTTONUP, 0, at);
            Assert.Equal((1, S.SettingsLayoutsSizeHeader), (menu.InlineDepth, menu.InlineTitle));
            var offered = menu.FieldSearchOf(field.Id)!.Rows;
            // §411: the size it has first, ticked.
            Assert.Equal(["48", "64", "32", "40", "56", "80"], offered.Select(found => found.Title));
            Assert.Equal(["48"], offered.Where(found => found.Chosen).Select(found => found.Title));

            Type(menu, "72");
            Assert.Equal("72", menu.FieldSearchOf(field.Id)!.Rows[0].Title);
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Assert.Single(Pump(menu)));
            var kept = menu.TakeFieldCommit(field.Id)!;
            Assert.True(OptionRows.Find(BarLooks.Rows(config, bar, dark: true, LooksScope.Taskbar), BarLooks.SizeRow)!.SetText!(kept.Found?.Value as string ?? kept.Text));
            Assert.Equal(72, config.ActiveLayout(display).Size);

            // Back on its row: Enter opens its level, Escape shuts it; Right steps.
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(1, menu.InlineDepth);
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal(0, menu.InlineDepth);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, stepper.More), Key(menu, VK.VK_RIGHT));
        });
    }

    /// <summary>
    /// §332 (the person: "clearing a label in the clock widget does not work, it just keeps its old value"): a
    /// clock's label, offered the places of its zone (§304), is a line of text still. Emptied and Enter,
    /// emptied and the menu closed by a click away, emptied with its clear button (✕) and its level shut by
    /// its back row: each keeps it empty. Words typed and Escape put it back and keep nothing; a suggestion
    /// picked is kept and shuts the level as Enter does. A zone's words left in its field go back.
    /// </summary>
    [UiFact]
    public void ALineWithSuggestionsKeepsItsWordsHoweverItIsLeft()
    {
        WindowShots.OnHiddenDesktop("menu-field-line", (gfx, instance) =>
        {
            var clock = ListKind.Clock.New();
            clock.Clock!.TimeZone = "Tokyo Standard Time";
            List<OptionPick> picks = [];
            (FlyoutItem Row, FlyoutItem Field, List<FlyoutItem> Items) Labelled(string label)
            {
                clock.Clock.Label = label;
                picks.Clear();
                List<FlyoutItem> items = [.. OptionMenu.Rows(OptionRows.Of(clock, new OptionDevices { Zones = ["Tokyo Standard Time"] }), picks)];
                var row = items.Single(item => item.Text == S.SettingsWidgetCountdownNameHeader);
                return (row, Assert.Single(row.Children!), items);
            }
            string Kept(FieldCommit kept, uint field)
            {
                _ = FieldPicks.Keep(clock, OptionMenu.PickOf(picks, field)!, kept);
                return clock.Clock.Label;
            }

            // Emptied, and Enter: kept empty, the level shut.
            var (row, field, items) = Labelled("Home");
            using (var menu = ShowConfiguring(gfx, instance, items))
            {
                _ = menu.Click(row.Id);
                Assert.NotNull(menu.FieldSearchOf(field.Id));
                _ = Key(menu, VK.VK_DELETE);
                _ = Key(menu, VK.VK_RETURN);
                Assert.Equal(0, menu.InlineDepth);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Assert.Single(Pump(menu)));
                Assert.Equal("", Kept(menu.TakeFieldCommit(field.Id)!, field.Id));
            }

            // Emptied, and the menu closed by a click away: kept empty.
            (row, field, items) = Labelled("Home");
            using (var menu = ShowConfiguring(gfx, instance, items))
            {
                _ = menu.Click(row.Id);
                _ = Key(menu, VK.VK_BACK);
                menu.Hide();
                Assert.Equal("", Kept(Assert.Single(menu.TakeFieldsLeft()), field.Id));
            }

            // Emptied with its clear button, and its level shut by its back row: kept empty.
            (row, field, items) = Labelled("Home");
            using (var menu = ShowConfiguring(gfx, instance, items))
            {
                _ = menu.Click(row.Id);
                var (middle, w, _) = menu.RowFill(field.Id)!.Value;
                // ✕ at the box's end (MenuTextField.Buttons), at 150 %: a line of text has no magnifier (§411).
                var x = middle.x + (int)(w / 2) - 3 - 21;
                var at = (nint)((middle.y << 16) | (x & 0xFFFF));
                _ = menu.Message(WM.WM_LBUTTONDOWN, 1, at);
                _ = menu.Message(WM.WM_LBUTTONUP, 0, at);
                Assert.Equal("", menu.FieldOf(field.Id)!.Text);
                Assert.Equal(default, menu.Click(Flyout.InlineBackId));
                Assert.Equal(0, menu.InlineDepth);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Assert.Single(Pump(menu)));
                Assert.Equal("", Kept(menu.TakeFieldCommit(field.Id)!, field.Id));
            }

            // Words typed, and Escape: the label as it was, nothing kept.
            (row, field, items) = Labelled("Home");
            using (var menu = ShowConfiguring(gfx, instance, items))
            {
                _ = menu.Click(row.Id);
                Type(menu, "Work");
                _ = Key(menu, VK.VK_ESCAPE);
                Assert.Equal(0, menu.InlineDepth);
                Assert.Empty(Pump(menu));
                menu.Hide();
                Assert.Empty(menu.TakeFieldsLeft());
                Assert.Equal("Home", clock.Clock.Label);
            }

            // A suggestion picked: kept, the level shut.
            (row, field, items) = Labelled("Home");
            using (var menu = ShowConfiguring(gfx, instance, items))
            {
                _ = menu.Click(row.Id);
                Type(menu, "Osa");
                Assert.Equal(["Osa", "Osaka"], menu.FieldSearchOf(field.Id)!.Rows.Select(found => found.Title));
                _ = Key(menu, VK.VK_DOWN);
                _ = Key(menu, VK.VK_DOWN);
                _ = Key(menu, VK.VK_RETURN);
                Assert.Equal(0, menu.InlineDepth);
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field.Id), Assert.Single(Pump(menu)));
                Assert.Equal("Osaka", Kept(menu.TakeFieldCommit(field.Id)!, field.Id));
            }

            // A zone's words only find: left there, they go back and nothing is kept.
            var zone = Assert.Single(items.Single(item => item.Text == S.SettingsWidgetClockZoneHeader).Children!);
            using (var menu = ShowConfiguring(gfx, instance, items))
            {
                _ = menu.Click(items.Single(item => item.Text == S.SettingsWidgetClockZoneHeader).Id);
                Type(menu, "+9");
                menu.Hide();
                Assert.Empty(menu.TakeFieldsLeft());
            }
            Assert.NotNull(zone);
        });
    }

    [UiFact]
    public void AZonesResultsAreRowsDownGoesIntoThemAndEnterPicks()
    {
        WindowShots.OnHiddenDesktop("menu-field-zone", (gfx, instance) =>
        {
            var clock = ListKind.Clock.New();
            List<OptionPick> picks = [];
            // The zone's own level (§226), shown as a menu of its own: its field.
            var rows = OptionMenu.Rows(OptionRows.Of(clock, new OptionDevices { Zones = ["Tokyo Standard Time"] }), picks);
            var items = rows.Single(item => item.Text == S.SettingsWidgetClockZoneHeader).Children!;
            var zone = Assert.Single(items);
            using var menu = Show(gfx, instance, items);
            KeysTo(menu, zone.Id);
            // This PC's and the person's own, as it takes the keys.
            var search = menu.FieldSearchOf(zone.Id)!;
            Assert.True(search.Open);
            Assert.Equal(S.SettingsWidgetClockZoneLocal, search.Rows[0].Title);
            Type(menu, "+9");
            Assert.Equal("Tokyo Standard Time", Assert.IsType<ZoneEntry>(search.Rows[0].Value).Id);
            Assert.Contains(menu.FieldShowing, item => item.Text == search.Rows[0].Title);
            // Down: the first result, the field's rows staying; Enter picks it, and the menu stays.
            _ = Key(menu, VK.VK_DOWN);
            Assert.Equal(0u, menu.FieldKeys);
            Assert.True(search.Open);
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_RETURN).Kind);
            Assert.True(menu.IsOpen);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, zone.Id), Assert.Single(Pump(menu)));
            var kept = menu.TakeFieldCommit(zone.Id)!;
            Assert.Equal("Tokyo Standard Time", Assert.IsType<ZoneEntry>(kept.Found!.Value).Id);
            Assert.True(FieldPicks.Keep(clock, OptionMenu.PickOf(picks, zone.Id)!, kept));
            Assert.Equal("Tokyo Standard Time", clock.Clock!.TimeZone);
        });
    }

    /// <summary>
    /// §392: a zone is one choice. Its level opened from the clock's menu, "+9" typed, the first result picked
    /// by Down and Enter, kept, and the menu shown again for it: back on the clock's rows, the zone's row lit,
    /// saying Tokyo's zone at its end.
    /// </summary>
    [UiFact]
    public void AZonePickedGoesBackToTheClocksRowsSayingIt()
    {
        WindowShots.OnHiddenDesktop("menu-field-zone-back", (gfx, instance) =>
        {
            var clock = ListKind.Clock.New();
            var devices = new OptionDevices { Zones = ["Tokyo Standard Time"] };
            List<OptionPick> picks = [];
            List<FlyoutItem> Rows()
            {
                picks.Clear();
                return OptionMenu.Rows(OptionRows.Of(clock, devices), picks);
            }
            var items = Rows();
            using var menu = Show(gfx, instance, items);
            var row = items.Single(item => item.Text == S.SettingsWidgetClockZoneHeader);
            var zone = Assert.Single(row.Children!);
            _ = menu.Click(row.Id);
            Assert.Equal((1, zone.Id), (menu.InlineDepth, menu.FieldKeys));
            Type(menu, "+9");
            _ = Pump(menu);
            _ = Key(menu, VK.VK_DOWN);
            _ = Key(menu, VK.VK_RETURN);
            Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, zone.Id), Assert.Single(Pump(menu)));
            Assert.True(FieldPicks.Keep(clock, OptionMenu.PickOf(picks, zone.Id)!, menu.TakeFieldCommit(zone.Id)!));
            Assert.Equal("Tokyo Standard Time", clock.Clock!.TimeZone);
            Assert.Equal(1, menu.InlineDepth);
            menu.Refresh(Rows());
            WindowShots.Settle();
            var now = menu.ShownItems.Single(item => item.Text == S.SettingsWidgetClockZoneHeader);
            Assert.Equal((0, now.Id), (menu.InlineDepth, menu.HotId!.Value));
            Assert.NotEqual(row.Value?.Text, now.Value?.Text);
            menu.Hide();
        });
    }

    /// <summary>
    /// The person, 2026-10-21: "picking a country for public holidays does not work". ⚙'s Public holidays on a
    /// real menu, its rows the dock's (Dock.SettingsItems), a pick kept as the dock keeps it (SettingsKept: the row
    /// read again from a copy of the config; then the menu shown again, MenuAgain): "Add a country" opened in
    /// place, "united" typed, the answer in, Down to the United Kingdom and Enter; then "united st" and a click on
    /// the United States' row. Each pick is one more calendar, listed in the level shown again, and the holiday
    /// calendar, configured with what was kept as the dock's Apply configures it, fetches and shows its days.
    /// </summary>
    [UiFact]
    public void ACountryPickedInPublicHolidaysIsListedAndItsHolidaysShow()
    {
        WindowShots.OnHiddenDesktop("menu-field-holidays", (gfx, instance) =>
        {
            var config = new Config { Holidays = new HolidayOptions { Calendars = [new HolidayCountry { Country = "CH" }] } };
            var services = new KarpoServices(null, () => { });
            List<OptionPick> picks = [];
            List<FlyoutItem> Items()
            {
                picks.Clear();
                return Karpo.Dock.Dock.SettingsItems(config, services, picks);
            }
            using var menu = Show(gfx, instance, Items());
            FlyoutItem Holidays() => menu.ShownItems.Single(item => item.Text == S.CoreHolidaysName);
            List<string> Listed() => [.. Holidays().Children!.Select(child => child.Text)];
            // As the dock keeps a field's pick of ⚙ (Dock.FieldKept, SettingsKept) and shows the menu again (MenuAgain).
            void Kept(uint field)
            {
                Assert.Equal(new FlyoutEvent(FlyoutEventKind.Adjust, field), Assert.Single(Pump(menu)));
                var pick = OptionMenu.PickOf(picks, field)!;
                Assert.Equal((OptionPickKind.Field, OptionHolidays.Add), (pick.Kind, pick.Key));
                var next = config.Clone();
                Assert.True(FieldPicks.KeepRow(OptionRows.Find(KarpoMenu.Rows(next, services), pick.Key), menu.TakeFieldCommit(field)!));
                config = next;
                menu.Refresh(OptionMenu.Updated(menu.ShownItems, Items()));
                WindowShots.Settle();
                Assert.True(menu.IsOpen);
            }
            // "Add a country": its search's level, opened in place from the holidays' own.
            uint Adding()
            {
                if (menu.InlineDepth == 0)
                {
                    KeysTo(menu, Holidays().Id);
                    _ = Key(menu, VK.VK_RIGHT);
                }
                var add = Holidays().Children!.Single(child => child.Text == S.CoreHolidaysAdd);
                var field = Assert.Single(add.Children!);
                Assert.IsType<FlyoutField>(field.Control);
                _ = menu.Click(add.Id);
                WindowShots.Settle();
                Assert.Equal(field.Id, menu.FieldKeys);
                return field.Id;
            }

            var first = Adding();
            var search = menu.FieldSearchOf(first)!;
            Type(menu, "united");
            _ = Pump(menu);
            var britain = search.Rows.ToList().FindIndex(row => row.Value as string == "GB");
            Assert.True(britain >= 0, $"no United Kingdom among {string.Join(", ", search.Rows.Select(row => row.Title))}");
            for (var down = 0; down <= britain; down++)
            {
                _ = Key(menu, VK.VK_DOWN);
            }
            Assert.Equal(FlyoutEventKind.Nothing, Key(menu, VK.VK_RETURN).Kind);
            Kept(first);
            Assert.Equal(["CH", "GB"], config.Holidays!.Calendars.Select(calendar => calendar.Country));
            Assert.Equal([HolidayPlaces.Name("CH"), HolidayPlaces.Name("GB"), "", S.CoreHolidaysAdd], Listed());

            var second = Adding();
            Type(menu, "united st");
            _ = Pump(menu);
            var states = menu.FieldShowing.Single(item => item.Text == HolidayPlaces.Name("US"));
            Click(menu, states.Id);
            Kept(second);
            Assert.Equal(["CH", "GB", "US"], config.Holidays!.Calendars.Select(calendar => calendar.Country));
            Assert.Equal([HolidayPlaces.Name("CH"), HolidayPlaces.Name("GB"), HolidayPlaces.Name("US"), "", S.CoreHolidaysAdd], Listed());
            Assert.True(Save(menu, "menu-field-holidays-dark.png", dark: true));

            // Their holidays: what the calendar asks for and shows once it is configured with them.
            List<string> asked = [];
            using var holidays = new HolidayCalendar((country, year, _) =>
            {
                lock (asked)
                {
                    asked.Add(country);
                }
                var answer = country switch
                {
                    "GB" => Karpo.Tests.Calendars.HolidayAnswers.Britain2026,
                    "US" => Karpo.Tests.Calendars.HolidayAnswers.States2026,
                    _ => Karpo.Tests.Calendars.HolidayAnswers.Switzerland2026,
                };
                return Task.FromResult(answer.Replace("\"2026-", $"\"{year}-", StringComparison.Ordinal));
            }, utcNow: () => new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc));
            holidays.Configure(config.Holidays);
            holidays.Refresh();
            holidays.Idle().GetAwaiter().GetResult();
            Assert.Equal(["CH", "GB", "US"], asked.Distinct().Order());
            var titles = holidays.EventsIn(new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), TimeZoneInfo.Local).Select(day => day.Title).ToList();
            Assert.Contains(titles, title => title is "Swiss National Day" or "Bundesfeier");
            Assert.Contains("Early May Bank Holiday", titles);
            Assert.Contains("Thanksgiving Day", titles);
            menu.Hide();
        });
    }

    [UiFact]
    public void AnAnswerOffTheMenusThreadShowsOnceItIsIn()
    {
        WindowShots.OnHiddenDesktop("menu-field-async", (gfx, instance) =>
        {
            FlyoutItem[] items =
            [
                new(1, "Remove", Keys: MenuChord.Delete),
                new(2, S.SettingsWidgetWeatherPlaceHeader, Control: new FlyoutField("", S.SettingsWidgetWeatherPlacePlaceholder, Finder: new CannedPlaces())),
            ];
            using var menu = Show(gfx, instance, items);
            KeysTo(menu, 2);
            var search = menu.FieldSearchOf(2)!;
            Type(menu, "ber");
            Assert.Equal(SearchPhase.Searching, search.Phase);
            Assert.Contains(menu.FieldShowing, item => item.Text == S.SettingsWidgetSearching && !item.Enabled);
            _ = Pump(menu, 400);
            Assert.Equal(SearchPhase.Found, search.Phase);
            Assert.Equal(["Bern", "Berne", "Bernalillo"], search.Rows.Select(row => row.Title));
            // A key typed on a result goes back to the field and on in it.
            _ = Key(menu, VK.VK_DOWN);
            _ = Key(menu, VK.VK_DOWN);
            Assert.Equal(0u, menu.FieldKeys);
            Type(menu, "ne");
            Assert.Equal((2u, "berne"), (menu.FieldKeys, menu.FieldOf(2)!.Text));
            _ = Pump(menu, 400);
            Assert.Equal(["Berne"], search.Rows.Select(row => row.Title));
        });
    }

    // --- §230: a search's level opens whole, its field with the keys -------------------------------------

    /// <summary>A widget's menu (one width, §230) with a clock's zone and a weather's place, the place's search canned.</summary>
    private static (List<FlyoutItem> Items, FlyoutItem Zone, FlyoutItem Place) SearchLevels()
    {
        var clock = ListKind.Clock.New();
        clock.Clock!.TimeZone = "Tokyo Standard Time";
        var weather = ListKind.WeatherNow.New();
        weather.Weather = new WeatherOptions { Place = "Zürich, Switzerland", Latitude = 47.37, Longitude = 8.54 };
        List<OptionPick> picks = [];
        var devices = new OptionDevices { Zones = ["Tokyo Standard Time", "Pacific Standard Time"] };
        List<FlyoutItem> items = [new(1, "Remove", Keys: MenuChord.Delete), .. OptionMenu.Rows(OptionRows.Of(clock, devices), picks), .. OptionMenu.Rows(OptionRows.Of(weather, devices), picks)];
        var zone = items.Single(item => item.Text == S.SettingsWidgetClockZoneHeader);
        var place = items.Single(item => item.Text == S.SettingsWidgetWeatherPlaceHeader);
        var field = place.Children!.Single(item => item.Control is FlyoutField);
        var own = new OptionFound("Zürich, Switzerland", "", new Place("Zürich, Switzerland", "", 47.37, 8.54)) { Chosen = true };
        var canned = place with { Children = [.. place.Children!.Select(item => item == field ? field with { Control = ((FlyoutField)field.Control!) with { Finder = new CannedPlaces { Own = [own] } } } : item)] };
        items[items.IndexOf(place)] = canned;
        return (items, zone, canned);
    }

    private static Flyout ShowConfiguring(Gfx gfx, HINSTANCE instance, IReadOnlyList<FlyoutItem> items, bool dark = true)
    {
        var menu = new Flyout(gfx, instance, 0) { Configures = true };
        menu.Show(gfx, AddListPictures.LookFor(dark), Scale, items, FlyoutLayout.List, Anchor, Work, Edge.Bottom, null);
        WindowShots.Settle();
        return menu;
    }

    private static (int W, int H) Size(Flyout menu, int panel) =>
        (menu.OpenPanels[panel].Panel.right - menu.OpenPanels[panel].Panel.left, menu.OpenPanels[panel].Panel.bottom - menu.OpenPanels[panel].Panel.top);

    private static void Click(Flyout menu, uint id)
    {
        var middle = menu.RowFill(id)!.Value.Middle;
        var at = (nint)((middle.y << 16) | (middle.x & 0xFFFF));
        _ = menu.Message(WM.WM_LBUTTONDOWN, 1, at);
        _ = menu.Message(WM.WM_LBUTTONUP, 0, at);
    }

    /// <summary>
    /// A zone's level, opened by a click or by the keys, has its field with the keys at once, its words
    /// selected, and its rows — this PC's, the person's own, every zone a scroll away (§271) — before a key
    /// is typed; it keeps its size as it is typed in, finds, finds nothing; Down goes into the rows.
    /// </summary>
    [UiFact]
    public void AZonesLevelOpensWholeWithItsFieldTakingTheKeys()
    {
        WindowShots.OnHiddenDesktop("menu-field-zone-level", (gfx, instance) =>
        {
            var (items, zoneRow, _) = SearchLevels();
            var zone = Assert.Single(zoneRow.Children!);
            using var menu = ShowConfiguring(gfx, instance, items);
            Click(menu, zoneRow.Id);
            WindowShots.Settle();
            Assert.Equal(1, menu.InlineDepth);
            Assert.Equal(zone.Id, menu.FieldKeys);
            var field = menu.FieldOf(zone.Id)!;
            Assert.Equal((0, field.Text.Length, field.Text.Length), (field.Editing.SelectionStart, field.Editing.SelectionLength, field.Editing.Caret));
            var search = menu.FieldSearchOf(zone.Id)!;
            Assert.True(search.Open);
            // §411: the zone it shows first, ticked, then this PC's.
            Assert.Equal(["Osaka, Sapporo, Tokyo", S.SettingsWidgetClockZoneLocal], search.Rows.Take(2).Select(row => row.Title));
            Assert.Contains(menu.FieldShowing, item => item.Text == search.Rows[^1].Title);
            Assert.True(menu.ContentHeight > menu.ShownHeight);
            var opened = Size(menu, 0);
            // One width, the menu's (§230).
            Assert.Equal((int)MathF.Round(Flyout.SettingsWidthDip * Scale), opened.W);
            Assert.Equal(opened.W, Size(menu, 0).W);

            Type(menu, "tok");
            WindowShots.Settle();
            Assert.Equal("Tokyo Standard Time", Assert.IsType<ZoneEntry>(search.Rows[0].Value).Id);
            Assert.Equal(opened, Size(menu, 0));
            Type(menu, "qqq");
            WindowShots.Settle();
            Assert.Equal(SearchPhase.Nothing, search.Phase);
            Assert.Equal(opened, Size(menu, 0));

            // Down: into the rows; Escape back to the field, which puts its words back.
            _ = Key(menu, VK.VK_ESCAPE);
            Assert.Equal(opened, Size(menu, 0));
            _ = Key(menu, VK.VK_DOWN);
            Assert.Equal(0u, menu.FieldKeys);
            Assert.Equal("Osaka, Sapporo, Tokyo", menu.FieldShowing.Single(item => item.Id == menu.HotId).Text);

            // Shut, then opened by the keys: the field has them again at once.
            _ = Key(menu, VK.VK_LEFT);
            Assert.Equal(0, menu.InlineDepth);
            _ = Key(menu, VK.VK_RIGHT);
            WindowShots.Settle();
            Assert.Equal(zone.Id, menu.FieldKeys);
            Assert.Equal(opened, Size(menu, 0));
        });
    }

    /// <summary>
    /// A place's level opens with its rows — its own place, under the device's switch (§240) — nothing
    /// asked of the service, at the size it keeps while the service is asked and answers.
    /// </summary>
    [UiFact]
    public void APlacesLevelOpensWholeAndKeepsItsSize()
    {
        WindowShots.OnHiddenDesktop("menu-field-place-level", (gfx, instance) =>
        {
            var (items, _, placeRow) = SearchLevels();
            var place = placeRow.Children!.Single(item => item.Control is FlyoutField);
            using var menu = ShowConfiguring(gfx, instance, items);
            Click(menu, placeRow.Id);
            WindowShots.Settle();
            Assert.Equal(place.Id, menu.FieldKeys);
            var search = menu.FieldSearchOf(place.Id)!;
            Assert.Equal(SearchPhase.Idle, search.Phase);
            Assert.Equal(["Zürich, Switzerland"], search.Rows.Select(row => row.Title));
            var opened = Size(menu, 0);
            Type(menu, "ber");
            Assert.Equal(opened, Size(menu, 0));
            _ = Pump(menu, 400);
            Assert.Equal(["Bern", "Berne", "Bernalillo"], search.Rows.Select(row => row.Title));
            Assert.Equal(opened, Size(menu, 0));
        });
    }

    /// <summary>
    /// §230's pictures in KARPO_PNG_DIR, at 150 %, dark and light: a zone's level just opened
    /// (menu-search-zone-open-<look>.png), a place's (menu-search-place-open-<look>.png), and a clock
    /// face's settings with its zone short at the row's end (menu-search-face-zone-<look>.png).
    /// </summary>
    [UiFact]
    public void SearchLevelPictures()
    {
        WindowShots.OnHiddenDesktop("menu-search-png", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";
                var (items, zoneRow, placeRow) = SearchLevels();
                using (var menu = ShowConfiguring(gfx, instance, items, dark))
                {
                    Click(menu, zoneRow.Id);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-search-zone-open-{tone}.png", dark));
                }
                using (var menu = ShowConfiguring(gfx, instance, items, dark))
                {
                    Click(menu, placeRow.Id);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-search-place-open-{tone}.png", dark));
                }
                var folder = BarKind.FolderList.New();
                folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.WeatherId)];
                var face = folder.Folder.Faces[0];
                face.Set(face.Info.Options.Single(option => option.Key == ClockFace.Zone), "India Standard Time");
                var faces = OptionMenu.FaceSection(folder.Folder, []);
                using (var menu = ShowConfiguring(gfx, instance, faces, dark))
                {
                    KeysTo(menu, faces.First(item => item.Reorder != 0).Id);
                    _ = Key(menu, VK.VK_RIGHT);
                    WindowShots.Settle();
                    Assert.Contains(faces.First(item => item.Reorder != 0).Children!, item => item.Value?.Text == "UTC+5:30");
                    Assert.True(Save(menu, $"menu-search-face-zone-{tone}.png", dark));
                }
            }
        });
    }

    // --- Pictures --------------------------------------------------------------------------------------

    [UiFact]
    public void FieldPictures()
    {
        WindowShots.OnHiddenDesktop("menu-field-png", (gfx, instance) =>
        {
            foreach (var dark in new[] { true, false })
            {
                var tone = dark ? "dark" : "light";

                // A clock face's time zone, searched for "tok" in its submenu.
                {
                    var folder = BarKind.FolderList.New();
                    folder.Folder!.Faces = [FolderFace.Of(FolderFaces.ClockId), FolderFace.Of(FolderFaces.WeatherId)];
                    List<OptionPick> picks = [];
                    var items = OptionMenu.FaceSection(folder.Folder, picks, new OptionDevices { Zones = ["Tokyo Standard Time", "Pacific Standard Time"] });
                    using var menu = Show(gfx, instance, items, dark);
                    var clock = items.Single(item => item.Text == "Clock");
                    KeysTo(menu, clock.Id);
                    _ = Key(menu, VK.VK_RIGHT);
                    // Its own level under the face's (§226): its field and the results.
                    var zoneRow = clock.Children!.Single(item => item.Text == S.SettingsWidgetClockZoneHeader);
                    var zone = Assert.Single(zoneRow.Children!);
                    for (var step = 0; step < 6 && menu.HotId != zoneRow.Id; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    _ = Key(menu, VK.VK_RIGHT);
                    for (var step = 0; step < 3 && menu.FieldKeys != zone.Id; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    Assert.Equal(zone.Id, menu.FieldKeys);
                    Type(menu, "tok");
                    _ = Pump(menu, 100);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-field-zone-{tone}.png", dark));
                }

                // A weather face's place: its device switch over the field (§240) — on, the field and its
                // results' room greyed; off, the places found under the field.
                (int W, int H)? greyed = null;
                foreach (var device in new[] { true, false })
                {
                    var folder = BarKind.FolderList.New();
                    folder.Folder!.Faces = [FolderFace.Of(FolderFaces.WeatherId)];
                    folder.Folder.Faces[0].Weather = new WeatherOptions { UseDeviceLocation = device, Place = "Zürich, Switzerland", Latitude = 47.37, Longitude = 8.54 };
                    List<OptionPick> picks = [];
                    var items = OptionMenu.FaceSection(folder.Folder, picks);
                    var weather = items.Single(item => item.Children?.Any(child => child.Text == S.SettingsWidgetWeatherPlaceHeader) == true);
                    var placeRow = weather.Children!.Single(item => item.Text == S.SettingsWidgetWeatherPlaceHeader);
                    Assert.Equal(device ? S.DockEditDeviceLocation : "Zürich", placeRow.Value!.Text);
                    var toggle = placeRow.Children![0];
                    Assert.Equal((S.SettingsWidgetWeatherDeviceHeader, new FlyoutControl.Toggle(device)), (toggle.Text, toggle.Control));
                    var place = placeRow.Children!.Single(item => item.Control is FlyoutField);
                    // §411 (R6): whole while the device's location is on too.
                    Assert.True(place.Enabled);
                    var canned = placeRow with { Children = [.. placeRow.Children!.Select(item => item == place ? place with { Control = ((FlyoutField)place.Control!) with { Finder = new CannedPlaces() } } : item)] };
                    items = [.. items.Select(item => item.Id == weather.Id ? item with { Children = [.. weather.Children!.Select(child => child.Id == placeRow.Id ? canned : child)] } : item)];
                    using var menu = ShowConfiguring(gfx, instance, items, dark);
                    KeysTo(menu, weather.Id);
                    _ = Key(menu, VK.VK_RIGHT);
                    for (var step = 0; step < 6 && menu.HotId != placeRow.Id; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    _ = Key(menu, VK.VK_RIGHT);
                    WindowShots.Settle();
                    Assert.Equal(2, menu.InlineDepth);
                    if (device)
                    {
                        // §411 (R6): whole with the switch on too, the keys in it and its places listed; the level the
                        // size it has with the switch off.
                        Assert.Equal(place.Id, menu.FieldKeys);
                        Assert.True(menu.FieldSearchOf(place.Id)?.Open);
                        greyed = Size(menu, 0);
                        Assert.True(Save(menu, $"menu-field-place-device-{tone}.png", dark));
                        continue;
                    }
                    Assert.Equal(greyed, Size(menu, 0));
                    for (var step = 0; step < 3 && menu.FieldKeys != place.Id; step++)
                    {
                        _ = Key(menu, VK.VK_DOWN);
                    }
                    Assert.Equal(place.Id, menu.FieldKeys);
                    Type(menu, "bern");
                    _ = Pump(menu, 400);
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-field-place-{tone}.png", dark));
                }

                // A timer's name (§246): its row saying it, then its level just opened by a click, the
                // words selected; then typed in, part of it selected.
                {
                    var timer = ListKind.Timer.New();
                    timer.Countdown = new CountdownOptions { Minutes = 9, Name = "Pasta water" };
                    var items = OptionMenu.Section(OptionRows.Of(timer), []);
                    var nameRow = items.Single(item => item.Text == S.SettingsWidgetCountdownNameHeader);
                    Assert.Equal("Pasta water", nameRow.Value!.Text);
                    var name = Assert.Single(nameRow.Children!);
                    using var menu = ShowConfiguring(gfx, instance, [new FlyoutItem(1, "Remove", Keys: MenuChord.Delete), .. items], dark);
                    Assert.True(Save(menu, $"menu-field-text-rest-{tone}.png", dark));
                    Click(menu, nameRow.Id);
                    WindowShots.Settle();
                    Assert.Equal(name.Id, menu.FieldKeys);
                    Assert.True(Save(menu, $"menu-field-text-open-{tone}.png", dark));
                    Type(menu, "Tea for two");
                    _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                    KeyState.Held = key => key == VK.VK_SHIFT;
                    try
                    {
                        _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                        _ = menu.Message(WM.WM_KEYDOWN, VK.VK_LEFT, 0);
                    }
                    finally
                    {
                        KeyState.Held = null;
                    }
                    WindowShots.Settle();
                    Assert.True(Save(menu, $"menu-field-text-{tone}.png", dark));
                }
            }

            // Arabic: the menu mirrored, the field's words at its right and its buttons at its left.
            using (InLanguage.Use("ar"))
            {
                var clock = ListKind.Clock.New();
                clock.Clock!.Label = "الرياض";
                var items = OptionMenu.Section(OptionRows.Of(clock, new OptionDevices { Zones = ["Arab Standard Time"] }), []);
                using var menu = Show(gfx, instance, items, dark: true);
                // The zone's own level, opened leftward as a mirrored menu's are (§226).
                var zoneRow = items.Single(item => item.Text == S.SettingsWidgetClockZoneHeader);
                var zone = Assert.Single(zoneRow.Children!);
                KeysTo(menu, zoneRow.Id);
                _ = Key(menu, VK.VK_LEFT);
                for (var step = 0; step < 3 && menu.FieldKeys != zone.Id; step++)
                {
                    _ = Key(menu, VK.VK_DOWN);
                }
                Assert.Equal(zone.Id, menu.FieldKeys);
                _ = Pump(menu, 100);
                WindowShots.Settle();
                Assert.True(Save(menu, "menu-field-rtl.png", dark: true));
            }
        });
    }
}
