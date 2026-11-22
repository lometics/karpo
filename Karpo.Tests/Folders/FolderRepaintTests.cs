// An open folder that changes repaints in place (the person, 2026-10-10: "sometimes when changing the
// day in the calendar view the whole folder where i am currently in refreshes so that it disappears
// very briefly ... and then appears again").
//
// The "sometimes" was a day whose agenda is another length: the agenda's line grows or shrinks, the
// folder is laid out again (FolderFlyout.LayOut), and that made every visual of the folder again and
// every panel's backdrop, shadow and rim with it (FlyoutHost.MakePieces), which the compositor shows a
// frame late. Held here: a day picked, a month stepped and a line's own height change leave the
// folder's window, its visuals and its panels' chrome where they are — moved and repainted, never
// made again — and edit mode's "+" buttons are made alone, the lines kept and no new panels (§357:
// every element is kept by its key, RetainedFolderTests). Needs a window station: KARPO_UI_TESTS=1.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace Karpo.Tests.Folders;

public sealed unsafe class FolderRepaintTests
{
    private static readonly DateTime Frozen = new(2026, 9, 22, 14, 40, 7, DateTimeKind.Local);
    private static readonly DateOnly Thursday = new(2026, 9, 24);

    private static Look Look => new(Glass: true, Dark: true, Accent: Windows.UI.Color.FromArgb(255, 0, 0x78, 0xD4));

    /// <summary>
    /// What says the folder was made again: its window, whether it shows, its elements and composition
    /// objects made, its panels' chrome made. Not its surfaces: a line of another height is drawn on a
    /// surface of that height.
    /// </summary>
    private static (HWND Window, bool Shown, FolderFlyout.FolderMade Visuals, int Pieces) Made(FolderFlyout flyout) =>
        (flyout.Hwnd, IsWindowVisible(flyout.Hwnd), flyout.Made with { Surfaces = 0 }, flyout.HostMade.Pieces);

    /// <summary>
    /// The calendar preset's folder open over a bottom bar, its events the calendar sample's, at the
    /// sample's Tuesday; its lines first changed by <paramref name="adjust"/> where given.
    /// </summary>
    private static void WithTheCalendarOpen(string what, Action<FolderFlyout, FolderIsland> body, Action<FolderOptions>? adjust = null) =>
        Karpo.Tests.Edit.WindowShots.OnHiddenDesktop(what, (gfx, instance) =>
        {
            Clock.Now = () => Frozen;
            var store = new CalendarStore();
            foreach (var calendar in CalendarWeatherFolderTests.Sample.GroupBy(item => item.Colour))
            {
                store.Add(new ListCalendar($"test:{calendar.Key}", calendar.Key.ToString(), calendar.Key, [.. calendar]));
            }
            CalendarIsland.Made = store;
            Nls.Formats = new LocalizationOptions { FirstDayOfWeek = DayOfWeek.Monday };
            try
            {
                var holder = Presets.Folder(Presets.Find(Presets.CalendarId)!, new PresetMachine(HasBattery: false, FixedDrives: []));
                adjust?.Invoke(holder.Folder!);
                using var folder = new FolderIsland($"layout:default/{holder.Id}", holder, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                var work = new RECT { left = 0, top = 0, right = 1600, bottom = 1600 };
                flyout.Show(gfx, Look, 1f, new POINT { x = 800, y = 1600 }, work, Edge.Bottom, folder);
                Assert.True(flyout.IsOpen);
                // The events are read once the agenda is first drawn, and arrive as the store's news,
                // which the dock hands the folder as a refresh: here at once.
                ((IIsland)folder).Refresh(gfx);
                // Every line shows (the work area is tall enough for all of them), so a panel
                // more or less is never the news: one card, no divider (2026-10-11).
                Assert.Empty(flyout.Plan!.Hidden);
                Assert.Equal(1, flyout.PiecesShown!.Count);
                body(flyout, folder);
                flyout.Hide();
            }
            finally
            {
                Nls.Formats = null;
                CalendarIsland.Made = null;
                Clock.Now = Clock.Real;
            }
        });

    private static T Tile<T>(FolderIsland folder) => folder.Tiles.Select(tile => tile.Island).OfType<T>().Single();

    /// <summary>
    /// A day picked in the month whose events are fewer than today's: the agenda's line is shorter
    /// and the folder with it, laid out again where it stands — and nothing of it made again: the
    /// same window, shown throughout, the same visuals and the same panels, moved. The preset's
    /// agenda is fixed (the person's, 2026-10-11, below); here it is set to its content's height,
    /// so it changes.
    /// </summary>
    [UiFact]
    public void PickingADayWithAnotherAgendaRepaintsInPlace() =>
        WithTheCalendarOpen("day-picked", (flyout, folder) =>
        {
            var month = Tile<MonthTile>(folder);
            var events = Tile<EventsTile>(folder);
            var eventsAt = folder.Tiles.Select(tile => tile.Island).ToList().IndexOf(events);
            var monthAt = folder.Tiles.Select(tile => tile.Island).ToList().IndexOf(month);
            var before = flyout.Plan!;
            var made = Made(flyout);
            Assert.True(made.Shown);

            month.Click(MonthTile.Target(Thursday));

            Assert.Equal(Thursday, events.Day);
            var after = flyout.Plan!;
            // The path the blink took: the agenda's line changed height and the folder was laid out again.
            Assert.NotEqual(before.Cards[eventsAt].bottom - before.Cards[eventsAt].top, after.Cards[eventsAt].bottom - after.Cards[eventsAt].top);
            Assert.NotEqual(before.Height, after.Height);
            Assert.Equal(before.Cards[monthAt].bottom - before.Cards[monthAt].top, after.Cards[monthAt].bottom - after.Cards[monthAt].top, 3);
            Assert.Equal(made, Made(flyout));
            Assert.Equal(after.Pieces, flyout.PiecesShown);

            // And back to today: as long as it was, and still nothing made.
            month.Click(MonthTile.Target(DateOnly.FromDateTime(Frozen)));
            Assert.Equal(before.Height, flyout.Plan!.Height);
            Assert.Equal(before.Cards, flyout.Plan!.Cards);
            Assert.Equal(made, Made(flyout));
        }, adjust: folder => folder.Cells.Single(cell => cell.Island.List == ListKind.Events).Island.Height = LineHeight.Automatic(FolderOptions.MostCells));

    /// <summary>
    /// The preset as the person arranged it (2026-10-11): the month over the day's events, which are
    /// six rows fixed, so a day picked whose agenda is another length moves nothing — the folder's
    /// height, every card and the month under the pointer stay where they are.
    /// </summary>
    [UiFact]
    public void PickingADayInThePresetMovesNothing() =>
        WithTheCalendarOpen("day-picked-fixed", (flyout, folder) =>
        {
            var month = Tile<MonthTile>(folder);
            var events = Tile<EventsTile>(folder);
            var before = flyout.Plan!;
            var made = Made(flyout);

            month.Click(MonthTile.Target(Thursday));

            Assert.Equal(Thursday, events.Day);
            Assert.Equal(before.Height, flyout.Plan!.Height);
            Assert.Equal(before.Cards, flyout.Plan!.Cards);
            Assert.Equal(made, Made(flyout));
        });

    /// <summary>
    /// A month stepped, and a day picked whose agenda is as long: nothing is laid out again, so the
    /// window is not even moved.
    /// </summary>
    [UiFact]
    public void SteppingTheMonthMovesNothing() =>
        WithTheCalendarOpen("month-stepped", (flyout, folder) =>
        {
            var month = Tile<MonthTile>(folder);
            var made = Made(flyout);
            var moves = flyout.HostMade.Moves;
            var plan = flyout.Plan!;

            month.Click(MonthTile.NextTarget);
            month.Click(MonthTile.PreviousTarget);
            month.Click(MonthTile.PreviousTarget);
            month.Click(MonthTile.NextTarget);
            Assert.Equal(made, Made(flyout));
            Assert.Equal(moves, flyout.HostMade.Moves);
            // 29 September has one event (Planning), as 21 September has one (Gym).
            month.Click(MonthTile.Target(new DateOnly(2026, 9, 29)));
            var oneEvent = flyout.Plan!;
            month.Click(MonthTile.Target(new DateOnly(2026, 9, 21)));

            Assert.Equal(new DateOnly(2026, 9, 21), Tile<EventsTile>(folder).Day);
            Assert.Equal(oneEvent.Height, flyout.Plan!.Height, 3);
            Assert.Equal(oneEvent.Cards, flyout.Plan!.Cards);
            Assert.Equal(made, Made(flyout));
            // One move at most: to the one-event day's height, and none for the second one-event day.
            Assert.True(flyout.HostMade.Moves - moves <= 1, $"the window was put somewhere {flyout.HostMade.Moves - moves} times");
            Assert.Equal(plan.Width, flyout.Plan!.Width, 3);
        });

    /// <summary>
    /// Edit mode's "+" buttons are visuals the folder did not have, so they are made — and nothing else:
    /// the lines are kept (§357), and the lists stand where they stood (nothing moves edit↔view), so the
    /// panels' chrome is the same chrome.
    /// </summary>
    [UiFact]
    public void ArrangingMakesTheAddsAndKeepsThePanels() =>
        WithTheCalendarOpen("arranged", (flyout, folder) =>
        {
            var made = flyout.Made;
            var chrome = flyout.HostMade.Pieces;
            var pieces = flyout.PiecesShown;

            flyout.Arrange();

            Assert.Equal(made.Lines, flyout.Made.Lines);
            Assert.Equal(made.Views, flyout.Made.Views);
            Assert.Equal(made.Buttons + flyout.Plan!.Adds.Count, flyout.Made.Buttons);
            Assert.Equal(chrome, flyout.HostMade.Pieces);
            Assert.Equal(pieces, flyout.PiecesShown);
            Assert.True(IsWindowVisible(flyout.Hwnd));
        });
}
