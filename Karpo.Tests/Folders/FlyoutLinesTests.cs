// The list widgets a folder's flyout is made of (docs\widgets.md §9): the toggles row, the actions
// line, the month and the time and date — and the folder's channels between them, the month picking
// the day an agenda beside it shows. Each is held to what every list form keeps (ListFormCases:
// drawn inside its line, as tall as it says, its range holding it at the largest text) and pictured
// at 100 % and 225 % text, dark and light. Needs Direct2D, so it runs only with KARPO_UI_TESTS=1;
// the sheets go to KARPO_PNG_DIR. Nothing reaches this PC's radios, calendars or Settings.

using System.Numerics;
using Karpo.Dock;
using Karpo.Dock.QuickSettings.Services;
using Karpo.Tests.Islands;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;
using Xunit.Abstractions;
using static Karpo.Tests.Folders.ListFormPictures;
using static Karpo.Tests.Islands.Offscreen;

namespace Karpo.Tests.Folders;

public sealed class FlyoutLinesTests(ITestOutputHelper output)
{
    /// <summary>Made-up switches: Wi-Fi, Bluetooth, night light, game mode and sticky keys on, energy saver missing on this "PC", the rest off.</summary>
    // The microphone muted: off, its glyph struck through, as MuteSwitch draws it.
    private static IQuickSwitch Sample(Toggle toggle) =>
        new SampleSwitch(Toggles.Find(toggle)!.Name,
            on: toggle is Toggle.Wifi or Toggle.Bluetooth or Toggle.NightLight or Toggle.Sound or Toggle.GameMode or Toggle.StickyKeys,
            available: toggle != Toggle.EnergySaver,
            switches: true,
            glyph: toggle == Toggle.Microphone ? MuteSwitch.MicrophoneMuted : null);

    private static CellItem Row(params Toggle[] toggles) => Line(ListKind.Toggles, line => line.Toggles!.Shown = [.. toggles]);

    private static CellItem Actions(string? left, string? right) => Line(ListKind.Actions, line =>
    {
        line.Actions!.Left = left;
        line.Actions.Right = right;
    });

    private static readonly DateOnly Thursday = new(2026, 9, 24);

    // --- Pictures ---------------------------------------------------------------------------------

    [UiFact]
    public void TheTogglesRowIsWindowsQuickSettingsWithoutTheWords() =>
        Both("list-form-toggles", () => ToggleSwitches.Source = Sample,
        [
            new("The new row's toggles", () => [Line(ListKind.Toggles)]),
            new("Every toggle, wrapped", () => [Row(Enum.GetValues<Toggle>())]),
            new("Game mode and the accessibility switches", () => [Row(Toggle.GameMode, Toggle.ColorFilters, Toggle.MonoAudio, Toggle.Magnifier, Toggle.Narrator, Toggle.StickyKeys)]),
            new("Two toggles", () => [Row(Toggle.DoNotDisturb, Toggle.Wifi)]),
            new("None chosen", () => [Row()]),
        ]);

    [UiFact]
    public void TheActionsLineIsAFlyoutsFooter() =>
        Both("list-form-actions", null,
        [
            new("Both", () => [Actions("dateTime", "notifications")]),
            new("The left under the pointer", () => [Actions("dateTime", "notifications")], Ready: (_, folder) => Hover(folder, right: false)),
            new("The right under the pointer", () => [Actions("dateTime", "notifications")], Ready: (_, folder) => Hover(folder, right: true)),
            new("The left alone", () => [Actions("wifi", null)]),
            new("The right alone", () => [Actions(null, "bluetooth")]),
            new("None chosen", () => [Actions(null, null)]),
        ]);

    [UiFact]
    public void TheMonthAndTheTimeAreTheClocksFlyout() =>
        Both("list-form-month", () => CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week),
        [
            new("The clock's flyout", () => [Line(ListKind.TimeHeader), Line(ListKind.Month), Line(ListKind.Calendar), Actions("dateTime", null)], Overflows: true),
            new("A day picked", () => [Line(ListKind.Month), Line(ListKind.Calendar)], Overflows: true,
                Ready: (_, folder) => ((MonthTile)folder.Tiles[0].Island).Pick(Thursday)),
            new("A day with nothing", () => [Line(ListKind.Month), Line(ListKind.Calendar)], Overflows: true,
                Ready: (_, folder) => ((MonthTile)folder.Tiles[0].Island).Pick(Thursday.AddDays(1))),
        ]);

    /// <summary>The month starts its weeks where the region does: Monday, Sunday and Saturday, each a sheet.</summary>
    [UiFact]
    public void TheMonthStartsItsWeekOnTheRegionsDay()
    {
        foreach (var first in new[] { DayOfWeek.Monday, DayOfWeek.Sunday, DayOfWeek.Saturday })
        {
            OnStaThread(gfx =>
            {
                Karpo.Dock.Panels.Nls.Formats = new LocalizationOptions { FirstDayOfWeek = first };
                Clock.Now = () => ListFormCases.Frozen;
                try
                {
                    var look = LookFor(dark: false);
                    var folder = Folder(gfx, Line(ListKind.Month));
                    var (_, plan) = Plan(folder, look, 1);
                    var month = (MonthTile)folder.Tiles[0].Island;
                    var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
                    var card = plan.Cards[0];
                    var pieces = month.LinePieces(PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top), style);
                    // The first day cell is the week's first day, and the 1st stands under its weekday.
                    var days = pieces.Where(piece => piece.Name.StartsWith("day ", StringComparison.Ordinal)).ToList();
                    Assert.Equal(42, days.Count);
                    Assert.Equal(first, DateOnly.ParseExact(days[0].Name[4..], "yyyy-MM-dd").DayOfWeek);
                    Sheet(gfx, $"list-form-month-{first.ToString().ToLowerInvariant()}.png", 1, [($"Weeks from {first}: 100 % text", look, folder, plan)]);
                    folder.Closed();
                    folder.Dispose();
                }
                finally
                {
                    Karpo.Dock.Panels.Nls.Formats = null;
                }
            });
        }
    }

    // --- Toggles --------------------------------------------------------------------------------------

    /// <summary>A click on a button switches that toggle and no other; between them, nothing.</summary>
    [UiFact]
    public void AClickOnAToggleSwitchesThatToggle() =>
        OnStaThread(gfx =>
        {
            ToggleSwitches.Source = Sample;
            var look = LookFor(dark: true);
            var folder = Folder(gfx, Row(Toggle.Wifi, Toggle.AirplaneMode, Toggle.EnergySaver));
            var (_, plan) = Plan(folder, look, 1);
            var row = (TogglesTile)folder.Tiles[0].Island;
            Assert.Equal([Toggle.Wifi, Toggle.AirplaneMode, Toggle.EnergySaver], row.Shown);
            var card = plan.Cards[0];
            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
            var buttons = row.LinePieces(box, style);
            Assert.Equal(3, buttons.Count);

            Assert.Equal(2, row.CardTarget(ListFormCases.Centre(buttons[1].Box), box, style, WidgetSurface.List));
            row.Click(2);
            Assert.True(row.OnAt(1));
            Assert.True(row.OnAt(0), "Wi-Fi stayed as it was");
            row.Click(1);
            Assert.False(row.OnAt(0));
            // Energy saver is not on this PC: its click does nothing.
            row.Click(3);
            Assert.False(row.OnAt(2));
            // Between two buttons there is nothing to press.
            var between = new Vector2((buttons[0].Box.right + buttons[1].Box.left) / 2, ListFormCases.Centre(buttons[0].Box).Y);
            Assert.Equal(0, row.CardTarget(between, box, style, WidgetSurface.List));

            // The tooltip says the name and the state.
            Assert.Equal("Airplane mode: on", row.CardTip(ListFormCases.Centre(buttons[1].Box), box, style, WidgetSurface.List)?.Text);
            folder.Closed();
            folder.Dispose();
        });

    /// <summary>
    /// The keyboard walks the buttons as the folder walks every line's targets (§470, LineFocus): each button a stop,
    /// in their order, Enter on one its click, which switches it alone; the line keeps no keys of its own.
    /// </summary>
    [UiFact]
    public void TheKeyboardWalksTheToggles() =>
        OnStaThread(gfx =>
        {
            ToggleSwitches.Source = Sample;
            var look = LookFor(dark: true);
            var folder = Folder(gfx, Row(Toggle.AirplaneMode, Toggle.DoNotDisturb));
            var (_, plan) = Plan(folder, look, 1);
            var row = (TogglesTile)folder.Tiles[0].Island;
            var card = plan.Cards[0];
            var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
            var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));

            var stops = LineFocus.Parts(row, box, style, WidgetSurface.List);
            Assert.Equal([1, 2], stops.Select(stop => stop.Target));
            row.Click(stops[1].Target);
            Assert.True(row.OnAt(1));
            Assert.False(row.OnAt(0));
            Assert.False(row.KeyOn(VK.VK_RIGHT, WidgetSurface.List), "the arrows are the folder's");
            folder.Closed();
            folder.Dispose();
        });

    /// <summary>The buttons share the row evenly between the gutters, wrap in the same columns, and keep a least size.</summary>
    [UiFact]
    public void TheTogglesWrapOnOneGrid() =>
        OnStaThread(_ =>
        {
            var style = new TileStyle(1, LookFor(dark: true), FlyoutTheme.For(LookFor(dark: true)), 1, 1);
            var plan = TogglesTile.Lay(10, 360, style);
            Assert.Equal(10, plan.Buttons.Count);
            Assert.True(plan.PerRow is > 1 and < 10, $"{plan.PerRow} to a row");
            Assert.All(plan.Buttons, button => Assert.True(button.right - button.left >= 48 && button.bottom - button.top >= 48));
            // The second row's first button stands under the first's.
            Assert.Equal(plan.Buttons[0].left, plan.Buttons[plan.PerRow].left);
            Assert.True(plan.Buttons[plan.PerRow].top > plan.Buttons[0].bottom);
            // On the gutters: the first starts on one, the last of a full row ends on the other.
            var gutter = ListForm.Gutter(style);
            Assert.Equal(gutter, plan.Buttons[0].left);
            Assert.True(360 - gutter - plan.Buttons[plan.PerRow - 1].right is >= 0 and < 8);
            Assert.Equal(TogglesTile.Lay(1, 360, style).Height, TogglesTile.Lay(plan.PerRow, 360, style).Height);
            Assert.True(plan.Height > TogglesTile.Lay(plan.PerRow, 360, style).Height);
        });

    /// <summary>
    /// A line taller than its buttons holds them in its middle, the air shared above and below to a
    /// px (2026-10-12, the person: "the toggles should be vertically centered"); one just as tall has
    /// them where they were, and one shorter keeps the rows that fit from its top.
    /// </summary>
    [UiFact]
    public void TheTogglesStandInTheMiddleOfATallLine() =>
        OnStaThread(_ =>
        {
            var style = new TileStyle(1.5f, LookFor(dark: true), FlyoutTheme.For(LookFor(dark: true)), 1, 1);
            var laid = TogglesTile.Lay(6, 540, style);
            float Above(TogglesPlan plan, float top) => plan.Buttons[0].top - top;
            float Below(TogglesPlan plan, float bottom) => bottom - plan.Buttons[^1].bottom;
            foreach (var tall in new[] { laid.Height + 21, laid.Height + 100, laid.Height * 4 })
            {
                var plan = TogglesTile.Fit(laid, new D2D_RECT_F(0, 10, 540, 10 + tall), style);
                Assert.True(MathF.Abs(Above(plan, 10) - Below(plan, 10 + tall)) <= 1, $"{Above(plan, 10)} above, {Below(plan, 10 + tall)} below in a line {tall} tall");
                Assert.Equal(MathF.Floor(plan.Buttons[0].top), plan.Buttons[0].top);
            }
            var exact = TogglesTile.Fit(laid, new D2D_RECT_F(0, 10, 540, 10 + laid.Height), style);
            Assert.Equal(laid.Buttons[0].top + 10, exact.Buttons[0].top);
            // Rows that do not all fit: the whole ones that do, in the middle of what they leave.
            var many = TogglesTile.Lay(30, 540, style);
            var held = TogglesTile.Fit(many, new D2D_RECT_F(0, 0, 540, laid.Height + 10), style);
            Assert.Equal(many.PerRow, held.Buttons.Count);
            Assert.True(MathF.Abs(Above(held, 0) - Below(held, laid.Height + 10)) <= 1);
        });

    // --- Actions --------------------------------------------------------------------------------------

    /// <summary>
    /// Both slots are text buttons, each saying its page's name and opening it; the rest of the line
    /// opens nothing. Whole names need no tooltip.
    /// </summary>
    [UiFact]
    public void BothButtonsSayAndOpenTheirPages() =>
        OnStaThread(gfx =>
        {
            var opened = new List<string>();
            ActionsTile.Opener = opened.Add;
            try
            {
                var look = LookFor(dark: true);
                var folder = Folder(gfx, Actions("dateTime", "wifi"));
                var (_, plan) = Plan(folder, look, 1);
                var line = (ActionsTile)folder.Tiles[0].Island;
                var card = plan.Cards[0];
                var box = PopupPaint.Rect(0, 0, card.right - card.left, card.bottom - card.top);
                var style = FolderFlyout.StyleFor(1, look, FlyoutTheme.For(look));
                var pieces = line.LinePieces(box, style);
                var left = pieces.Single(piece => piece.Name == "left");
                var right = pieces.Single(piece => piece.Name == "right");
                Assert.Equal("Date & time", left.Text);
                Assert.Equal("Wi-Fi", right.Text);
                // The left button on the gutter, the right ending on the other: both as wide as their
                // words, far apart.
                var laid = line.Lay(box, style);
                Assert.False(laid.LeftCut || laid.RightCut);
                Assert.Equal(box.right - ListForm.Gutter(style), laid.Right.right);
                Assert.True(right.Box.left > left.Box.right);

                line.Click(line.CardTarget(ListFormCases.Centre(left.Box), box, style, WidgetSurface.List));
                line.Click(line.CardTarget(ListFormCases.Centre(right.Box), box, style, WidgetSurface.List));
                line.Click(line.CardTarget(new Vector2((left.Box.right + right.Box.left) / 2, ListFormCases.Centre(left.Box).Y), box, style, WidgetSurface.List));
                Assert.Equal(["ms-settings:dateandtime", "ms-settings:network-wifi"], opened);
                Assert.Null(line.CardTip(ListFormCases.Centre(right.Box), box, style, WidgetSurface.List));
                Assert.Null(line.CardTip(ListFormCases.Centre(left.Box), box, style, WidgetSurface.List));

                // The keyboard (§470, LineFocus): the two buttons are the line's stops, left then right; Enter on the
                // second is its click.
                var stops = LineFocus.Parts(line, box, style, WidgetSurface.List);
                Assert.Equal(2, stops.Count);
                Assert.True(stops[0].Box.left < stops[1].Box.left);
                line.Click(stops[1].Target);
                Assert.Equal("ms-settings:network-wifi", opened[^1]);
                folder.Closed();
                folder.Dispose();
            }
            finally
            {
                ActionsTile.Opener = null;
            }
        });

    /// <summary>Puts the pointer on the actions line's left or right button, for its fill in the sheet.</summary>
    private static void Hover(FolderIsland folder, bool right)
    {
        var line = (ActionsTile)folder.Tiles[0].Island;
        var style = new TileStyle(1, LookFor(dark: true), FlyoutTheme.For(LookFor(dark: true)), 1, 1);
        var box = PopupPaint.Rect(0, 0, 320, 40);
        var laid = line.Lay(box, style);
        line.CardHover(ListFormCases.Centre(right ? laid.Right : laid.Left), box, style, WidgetSurface.List);
    }

    // --- The folder's day -----------------------------------------------------------------------------

    /// <summary>
    /// A click on a day in the month publishes it; the agenda beside it shows that day's events, with
    /// the day on its page; the folder closing forgets it, and both are at today again.
    /// </summary>
    [UiFact]
    public void TheAgendaFollowsTheDayTheMonthPicks() =>
        OnStaThread(gfx =>
        {
            Clock.Now = () => ListFormCases.Frozen;
            CalendarIsland.Made = ListFormCases.Store(ListFormCases.Week);
            var folder = Folder(gfx, Line(ListKind.Month), Line(ListKind.Calendar));
            var month = (MonthTile)folder.Tiles[0].Island;
            var agenda = (CalendarIsland)folder.Tiles[1].Island;
            var now = ListFormCases.Frozen;
            Assert.Null(folder.ChannelValue(Channels.Day));
            Assert.Null(agenda.Picked(now));
            Assert.Equal(DateOnly.FromDateTime(now), month.Picked);

            // A click on Thursday's cell.
            month.Click(MonthTile.Target(Thursday));

            Assert.Equal(Thursday, folder.ChannelValue(Channels.Day));
            Assert.Equal(Thursday, agenda.Picked(now));
            Assert.Equal(["Dentist"], agenda.Agenda(now).Select(item => item.Event.Title));
            // The header is the calendar's name, the picked day a heading like any other's (§419) over its events.
            var today = DateOnly.FromDateTime(now);
            Assert.Equal(["head " + Karpo.Core.WidgetInfos.Calendar.Title, "day 2026-09-24 " + CalendarText.DayName(Thursday, today), "event 0 Dentist"], Said(agenda));

            // A day with nothing: its heading over "No events", as an empty today's.
            month.Click(MonthTile.Target(Thursday.AddDays(1)));
            Assert.Equal(["head " + Karpo.Core.WidgetInfos.Calendar.Title, "day 2026-09-25 " + CalendarText.DayName(Thursday.AddDays(1), today), "status " + S.DockCalendarNoEvents], Said(agenda));

            // The folder closes: the day goes with it.
            folder.Closed();
            Assert.Null(folder.ChannelValue(Channels.Day));
            Assert.Null(agenda.Picked(now));
            Assert.Equal(DateOnly.FromDateTime(now), month.Picked);
            folder.Dispose();
        });

    /// <summary>What the agenda says top to bottom, as tall as it is, at 100 %: its header, its days' headings, its titles and why it is empty.</summary>
    private static string[] Said(CalendarIsland agenda)
    {
        var look = LookFor(dark: true);
        var style = FolderFlyout.StyleFor(1f, look, FlyoutTheme.For(look));
        var width = FolderFlyout.ListWidth(1f);
        var pieces = ((ILinePieces)agenda).LinePieces(PopupPaint.Rect(0, 0, width, ((IListContent)agenda).ListHeight(width, style)), style);
        return [.. pieces.Where(piece => piece.Text.Length > 0 && (piece.Name is "head" or "date" or "status" || piece.Name.StartsWith("day ", StringComparison.Ordinal) || piece.Name.StartsWith("event ", StringComparison.Ordinal)))
            .OrderBy(piece => piece.Box.top).Select(piece => $"{piece.Name} {piece.Text}")];
    }

    /// <summary>
    /// The mechanism is the channel's, not the day's: another channel, of another type, is published
    /// and read through the same folder, apart from the day, and forgotten with it.
    /// </summary>
    [UiFact]
    public void AnotherChannelWorksTheSameWay() =>
        OnStaThread(gfx =>
        {
            var hour = new Channel<int>("test.hour");
            var folder = Folder(gfx, Line(ListKind.Month));
            IWidgetContainer container = folder;
            var drawn = 0;
            folder.Changed += () => drawn++;

            container.Publish(hour, 9);
            Assert.Equal(9, container.Read(hour));
            Assert.Null(container.Read(Channels.Day));
            Assert.Equal(1, drawn);
            container.Publish(hour, 9);
            Assert.Equal(1, drawn);

            folder.Closed();
            Assert.Null(container.Read(hour));
            folder.Dispose();
        });

    /// <summary>Two months in a folder agree: one picks, the other shows the same day and its month.</summary>
    [UiFact]
    public void TwoMonthsAgree() =>
        OnStaThread(gfx =>
        {
            Clock.Now = () => ListFormCases.Frozen;
            var folder = Folder(gfx, Line(ListKind.Month), Line(ListKind.Month));
            var one = (MonthTile)folder.Tiles[0].Island;
            var other = (MonthTile)folder.Tiles[1].Island;
            other.Step(1);

            one.Pick(new DateOnly(2026, 11, 3));

            Assert.Equal(new DateOnly(2026, 11, 3), other.Picked);
            Assert.Equal(new DateOnly(2026, 11, 1), other.Shown);
            folder.Closed();
            folder.Dispose();
        });

    /// <summary>The keyboard moves the day picked, and publishes it as a click does; the wheel steps the month.</summary>
    [UiFact]
    public void TheKeysAndTheWheelMoveTheMonth() =>
        OnStaThread(gfx =>
        {
            Clock.Now = () => ListFormCases.Frozen;
            var folder = Folder(gfx, Line(ListKind.Month));
            var month = (MonthTile)folder.Tiles[0].Island;
            var today = DateOnly.FromDateTime(ListFormCases.Frozen);

            Assert.True(month.CardKey(VK.VK_DOWN, WidgetSurface.List));
            Assert.Equal(today.AddDays(7), folder.ChannelValue(Channels.Day));
            Assert.True(month.CardKey(VK.VK_NEXT, WidgetSurface.List));
            Assert.Equal(today.AddDays(7).AddMonths(1), month.Picked);
            Assert.True(month.CardKey(VK.VK_HOME, WidgetSurface.List));
            Assert.Equal(today, month.Picked);
            Assert.False(month.CardKey(VK.VK_TAB, WidgetSurface.List));

            Assert.True(month.CardWheel(default, default, default, WidgetSurface.List, 1));
            Assert.Equal(new DateOnly(2026, 8, 1), month.Shown);
            // Stepping the month leaves the day.
            Assert.Equal(today, month.Picked);
            folder.Closed();
            folder.Dispose();
        });

    // --- The sheet ------------------------------------------------------------------------------------

    /// <summary>
    /// <paramref name="cases"/> held to every list form's rules and pictured dark (ListFormCases), and
    /// pictured light beside it, after <paramref name="fakes"/> sets the drawing thread's stand-ins.
    /// </summary>
    private void Both(string name, Action? fakes, IReadOnlyList<ListFormCases.Case> cases)
    {
        var withFakes = cases.Select(@case => @case with { Lines = () => { fakes?.Invoke(); return @case.Lines(); } }).ToList();
        ListFormCases.Pictured(output, $"{name}-dark.png", withFakes);
        OnStaThread(gfx =>
        {
            Clock.Now = () => ListFormCases.Frozen;
            var panels = new List<(string, Look, FolderIsland, FolderPlan)>();
            try
            {
                foreach (var @case in withFakes)
                {
                    foreach (var textScale in TextScales)
                    {
                        var look = LookFor(dark: false, textScale);
                        var folder = Folder(gfx, @case.Lines());
                        @case.Ready?.Invoke(gfx, folder);
                        var (_, plan) = Plan(folder, look, 1);
                        panels.Add(($"{@case.Name}: {textScale * 100:0} % text", look, folder, plan));
                    }
                }
                Sheet(gfx, $"{name}-light.png", 1, panels);
            }
            finally
            {
                foreach (var (_, _, folder, _) in panels)
                {
                    folder.Closed();
                    folder.Dispose();
                }
            }
        });
    }
}
