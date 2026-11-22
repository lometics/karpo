// The month scrolls smoothly (the person, 2026-10-30: "the month calendar should scroll smoothly"; docs\widgets.md
// §424): a folder of one month, animations on, the wheel turned a notch over its weeks, a touchpad's small moves, and
// the › arrow clicked — each filmed (FrameFilm) over the weeks' window. In every film the weeks pass through
// positions between where they were and where they land (no jump), never back, and land aligned: a whole number of
// weeks on, the month's first week at the top. Strips to KARPO_PNG_DIR as film-month-<wheel|touchpad|arrow>.png.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Dock.Panels;
using Karpo.Tests.Edit;
using Karpo.Tests.Folders;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using Edge = Karpo.Core.Edge;

namespace Karpo.Tests.Present;

public sealed class MonthScrollFilmTests
{
    // 30 September 2026, a Wednesday; the weeks start on Monday, so September's first week starts on 31 August
    // and October's on 28 September, four weeks on.
    private static readonly DateTime Frozen = new(2026, 9, 30, 10, 0, 0);

    [UiFact]
    public void AWheelNotchGlidesToTheNextMonth() =>
        Filmed("month-wheel", (flyout, month, at) =>
            _ = flyout.Message(WM.WM_MOUSEWHEEL, Turned(-120), Lparam(at)),
            weeks: 4, next: new DateOnly(2026, 10, 1));

    [UiFact]
    public void ATouchpadMovesTheWeeksAndTheyComeBackAligned() =>
        Filmed("month-touchpad", (flyout, month, at) =>
        {
            // A precision touchpad's run of small moves down, a week and a bit in all: September is nearer than October.
            for (var step = 0; step < 6; step++)
            {
                _ = flyout.Message(WM.WM_MOUSEWHEEL, Turned(-18), Lparam(at));
            }
        }, weeks: 0, next: new DateOnly(2026, 9, 1), settles: true);

    [UiFact]
    public void TheArrowGlidesAsTheWheelDoes() =>
        Filmed("month-arrow", (flyout, month, at) =>
        {
            var card = flyout.ScreenRectOfCard(0);
            var plan = Assert.NotNull(month.Laid).Plan;
            var arrow = new POINT { x = card.left + (int)((plan.Next.left + plan.Next.right) / 2), y = card.top + (int)((plan.Next.top + plan.Next.bottom) / 2) };
            var client = FolderEditClickTests.Client(flyout.Hwnd, arrow);
            _ = flyout.Message(WM.WM_MOUSEMOVE, 0, client);
            _ = flyout.Message(WM.WM_LBUTTONDOWN, 1, client);
            _ = flyout.Message(WM.WM_LBUTTONUP, 0, client);
        }, weeks: 4, next: new DateOnly(2026, 10, 1));

    /// <summary>
    /// A folder of one month on 30 September 2026, animations on; <paramref name="act"/> over the middle of its weeks,
    /// filmed over them: the weeks move <paramref name="weeks"/> weeks on in the end, through places between —
    /// <paramref name="settles"/>: moved away first, then back.
    /// </summary>
    private static void Filmed(string name, Action<FolderFlyout, MonthTile, POINT> act, int weeks, DateOnly next, bool settles = false) =>
        WindowShots.OnHiddenDesktop(name, (gfx, instance) =>
        {
            Clock.Now = () => Frozen;
            Nls.Formats = new LocalizationOptions { FirstDayOfWeek = DayOfWeek.Monday };
            CalendarIsland.Made = new CalendarStore();
            try
            {
                var island = BarKind.FolderList.New();
                island.Id = "month-film";
                island.Folder!.Lists = [];
                island.Folder.Put(ListKind.Month.New(), 0);
                var look = TestLooks.LookFor(dark: true) with { Animations = true };
                using var folder = new FolderIsland($"layout:default/{island.Id}", island, gfx, new Config());
                using var flyout = new FolderFlyout(gfx, instance, 0);
                flyout.Show(gfx, look, 1f, new POINT { x = 800, y = 1000 }, new RECT { left = 0, top = 0, right = 1600, bottom = 1000 }, Edge.Bottom, folder, FlyoutAlign.Center);
                Assert.True(flyout.IsOpen);
                FrameFilm.Settle();
                var month = folder.Tiles.Select(tile => tile.Island).OfType<MonthTile>().Single();
                Assert.Equal(new DateOnly(2026, 9, 1), month.Shown);
                var card = flyout.ScreenRectOfCard(0);
                var (window, row) = Assert.NotNull(month.Laid) is var laid ? (laid.Window, laid.Row) : default;
                var area = new RECT { left = card.left + (int)window.left, top = card.top + (int)window.top, right = card.left + (int)window.right, bottom = card.top + (int)window.bottom };
                var middle = new POINT { x = (area.left + area.right) / 2, y = (area.top + area.bottom) / 2 };
                var line = FrameFilm.Snap(card, [flyout.Hwnd], 0, "before");
                // The folder's quick tick, as the dock hands it the timer: what settles a touchpad's weeks.
                var film =FrameFilm.Shoot(name, gfx, area, () => [flyout.Hwnd], () => act(flyout, month, middle), filmMs: 700,
                    pumped: () => _ = flyout.Quick ? flyout.Message(WM.WM_TIMER, (WPARAM)FolderFlyout.QuickTimer, 0) : default);

                // The whole line before and after, side by side, for the heading: month-<name>-line.png.
                var landed = FrameFilm.Snap(card, [flyout.Hwnd], 0, "after");
                var (cardWidth, cardHeight) = (card.right - card.left, card.bottom - card.top);
                var sheet = new byte[cardWidth * 2 * cardHeight * 4];
                for (var y = 0; y < cardHeight; y++)
                {
                    Buffer.BlockCopy(line.Pixels, y * cardWidth * 4, sheet, y * cardWidth * 8, cardWidth * 4);
                    Buffer.BlockCopy(landed.Pixels, y * cardWidth * 4, sheet, (y * cardWidth * 8) + (cardWidth * 4), cardWidth * 4);
                }
                WindowShots.WritePng(sheet, cardWidth * 2, cardHeight, Path.Combine(Offscreen.Pictures, $"{name}-line.png"));

                var shifts = film.Frames.Select(frame => Shift(film, frame)).ToList();
                var end = Shift(film, film.End);
                Console.WriteLine($"{name}: row {row} px; shifts {string.Join(' ', shifts)}; end {end}");
                // Lands aligned: a whole number of weeks on, at rest.
                Assert.InRange(end, (weeks * row) - 1, (weeks * row) + 1);
                Assert.Equal(next, month.Shown);
                // Through places between: at least two frames neither where it was nor where it went.
                var furthest = shifts.Max();
                var between = settles
                    ? shifts.Count(shift => shift > 3 && shift < furthest - 1)
                    : shifts.Count(shift => shift > 3 && shift < end - 3);
                Assert.True(between >= 2, $"{name}: {between} frames between; shifts {string.Join(' ', shifts)}");
                if (settles)
                {
                    // Moved on more than half a row, and came back.
                    Assert.True(furthest > row / 2, $"{name}: moved only {furthest} px");
                }
                else
                {
                    // Never back the way it came.
                    for (var index = 1; index < shifts.Count; index++)
                    {
                        Assert.True(shifts[index] >= shifts[index - 1] - 1, $"{name}: back from {shifts[index - 1]} to {shifts[index]} at f{index}");
                    }
                }
                flyout.Hide();
            }
            finally
            {
                Clock.Now = Clock.Real;
                Nls.Formats = null;
                CalendarIsland.Made = null;
            }
        });

    /// <summary>WM_MOUSEWHEEL's wParam for a turn of <paramref name="delta"/> (WHEEL_DELTA a notch; negative is down, on).</summary>
    private static WPARAM Turned(int delta) => (WPARAM)(nuint)unchecked((uint)(delta << 16));

    private static LPARAM Lparam(POINT screen) => (LPARAM)(nint)((screen.y << 16) | (screen.x & 0xFFFF));

    /// <summary>
    /// How far up <paramref name="shot"/> shows the weeks of the film's before, px: the shift whose rows of the
    /// before match the shot's best (at least a row's worth of the window overlapping).
    /// </summary>
    private static int Shift(FrameFilm.Film film, FrameFilm.Shot shot)
    {
        var (width, height) = (film.Width, film.Height);
        var (best, bestAt) = (double.MaxValue, 0);
        for (var shift = 0; shift <= height - 24; shift++)
        {
            long sum = 0;
            var rows = height - shift;
            for (var y = 0; y < rows; y++)
            {
                var a = y * width * 4;
                var b = (y + shift) * width * 4;
                for (var x = 0; x < width * 4; x += 4)
                {
                    sum += Math.Abs(shot.Pixels[a + x] - film.Before.Pixels[b + x]) + Math.Abs(shot.Pixels[a + x + 1] - film.Before.Pixels[b + x + 1]) + Math.Abs(shot.Pixels[a + x + 2] - film.Before.Pixels[b + x + 2]);
                }
            }
            var mean = sum / (double)(rows * width);
            if (mean < best - 1e-9)
            {
                (best, bestAt) = (mean, shift);
            }
        }
        return bestAt;
    }
}
