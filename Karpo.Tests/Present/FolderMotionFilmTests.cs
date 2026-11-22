// A folder's coming in and going, filmed (docs\widgets.md §446, §489; the person, 2026-10-31: "add animations ... for when
// opening / closing folders"; 2026-11-02: "only one default animation for everything"): animations on, the one popup motion,
// a folder over a bottom bar opened and closed as the dock's handler does it (FrameFilm). The opening has frames on the way, each further along than the one before — never the whole
// folder for a moment and then gone (a flash), never a frame of it higher or wider than where it ends (at a wrong place) —
// and ends exactly where the folder stands shown still; the close goes the same way back to nothing; a close in the middle
// of an opening goes back from where the opening had got to, never up to the whole folder first. Strips to KARPO_PNG_DIR
// as film-folder-motion-<open|close|reverse>.png. The hidden desktop's compositor can stall under load: each film is
// shot again (at most three times) where it caught no frame on the way.

using Karpo.Core;
using Karpo.Dock;
using Karpo.Tests.Edit;
using Karpo.Tests.Islands;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;
using Edge = Karpo.Core.Edge;
using Film = Karpo.Tests.Present.FrameFilm.Film;
using Shot = Karpo.Tests.Present.FrameFilm.Shot;

namespace Karpo.Tests.Present;

public sealed unsafe partial class FolderMotionFilmTests
{
    private const string Owner = "layout:default";

    private static readonly Look Moving = AddListPictures.LookFor(dark: true) with { Animations = true };

    private static readonly RECT Display = new() { right = 1280, bottom = 720 };

    private static readonly POINT Anchor = new() { x = 640, y = 720 };

    private static readonly PopupMotion Slide = PopupMotion.Of(Motion.Normal);

    private const string RoutedClass = "karpo.test.folder.motion";

    // The folder whose window hears its messages, as the dock's window procedure hands them over: its host's timers (the
    // close's end, the opening's) run in its Message.
    [ThreadStatic]
    private static FolderFlyout? routed;

    /// <summary>A folder whose window's messages reach it (<see cref="Routed"/>).</summary>
    private static FolderFlyout Folder(Gfx gfx, HINSTANCE instance)
    {
        fixed (char* className = RoutedClass)
        {
            var type = new WNDCLASSW { lpfnWndProc = &Routed, hInstance = instance, lpszClassName = className };
            _ = RegisterClassW(&type);
        }
        return routed = new FolderFlyout(gfx, instance, 0, RoutedClass);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    private static LRESULT Routed(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (routed is { } flyout && flyout.Hwnd == hwnd)
        {
            if (msg == WM.WM_WINDOWPOSCHANGING)
            {
                Surfaces.Keep(hwnd, (WINDOWPOS*)(nint)lParam);
            }
            _ = flyout.Message(msg, wParam, lParam);
        }
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }

    [UiFact]
    public void AFolderSlidesInFromItsBarAndGoesBackTheSameWay()
    {
        FlickerFilmTests.OnHiddenDesktop("folder-motion", (gfx, instance) =>
        {
            var island = FlickerFilmTests.Readings("motion");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = Folder(gfx, instance);
            void Open(PopupMotion? motion) => flyout.Show(gfx, Moving, 1f, Anchor, Display, Edge.Bottom, folder, motion: motion);

            // Where it stands shown still: what the opening must end on, exactly.
            Open(null);
            FrameFilm.Settle();
            var panel = flyout.PanelOnScreen;
            var area = new RECT { left = panel.left - 48, top = panel.top - 48, right = panel.right + 48, bottom = Display.bottom };
            var still = FrameFilm.Snap(area, [flyout.Hwnd], 0, "still");
            Shut(flyout);

            // A take whose film ran out before the folder had landed (the hidden desktop's captures far apart under load) is shot again.
            var opening = Shoot("folder-motion-open", gfx, area, flyout, () => Open(Slide), () => Shut(flyout), caught: film => film.Differs(film.Frames[^1], film.End) < 0.002);
            OnTheWay(opening, up: true);
            NowhereElse(opening, Bounds(opening, still));
            Assert.True(opening.Differs(opening.End, still) < 0.002, $"the opening ended {opening.Differs(opening.End, still):P2} off the folder shown still");
            Assert.True(opening.Differs(opening.Frames[^1], still) < 0.002, $"its last frame is {opening.Differs(opening.Frames[^1], still):P2} off the folder shown still");

            var closing = Shoot("folder-motion-close", gfx, area, flyout, flyout.Hide, () =>
            {
                Shut(flyout);
                Open(Slide);
                FrameFilm.Settle();
                Assert.True(Patience.Until(() => Pumped(() => Differs(area, flyout, still) < 0.002)), "the folder never stood open");
            });
            OnTheWay(closing, up: false);
            NowhereElse(closing, Bounds(closing, still));
            Assert.True(closing.Shows(closing.End, panel) < 0.01, $"the close ended with {closing.Shows(closing.End, panel):P1} of the folder in sight");
        });
    }

    /// <summary>
    /// A close as the folder is still coming in goes back from where it is: it never stands whole first (a close started
    /// from the whole folder would jump up, then go), and once it goes it only goes; it ends with nothing in sight.
    /// </summary>
    [UiFact]
    public void ACloseWhileItComesInGoesBackFromWhereItIs()
    {
        FlickerFilmTests.OnHiddenDesktop("folder-motion-reverse", (gfx, instance) =>
        {
            var island = FlickerFilmTests.Readings("reverse");
            using var folder = new FolderIsland($"{Owner}/{island.Id}", island, gfx, new Config());
            using var flyout = Folder(gfx, instance);
            flyout.Show(gfx, Moving, 1f, Anchor, Display, Edge.Bottom, folder);
            FrameFilm.Settle();
            var panel = flyout.PanelOnScreen;
            var area = new RECT { left = panel.left - 48, top = panel.top - 48, right = panel.right + 48, bottom = Display.bottom };
            var still = FrameFilm.Snap(area, [flyout.Hwnd], 0, "still");
            Shut(flyout);

            // Slow (two seconds), so the close is sure to catch it half way however the hidden desktop runs; coming in, the
            // film's settling before its first picture (60 ms and more) is all it gets.
            // A stalled compositor shows the folder whole, its animation not run (as DWM may anywhere): shot again, at most
            // three times; a close that starts from the whole folder stands whole in every take.
            var slow = Slide with { Enter = TimeSpan.FromSeconds(2) };
            var wrong = "";
            for (var take = 0; take < 3; take++)
            {
                if (take > 0)
                {
                    Shut(flyout);
                    Thread.Sleep(500);
                }
                using (Surfaces.Begin(gfx.Compositor))
                {
                    flyout.Show(gfx, Moving, 1f, Anchor, Display, Edge.Bottom, folder, motion: slow);
                }
                var film = FrameFilm.Shoot("folder-motion-reverse", gfx, area, () => [flyout.Hwnd], flyout.Hide, filmMs: 400);
                // Rising while the opening still runs (until the close's commit lands), then only sinking; never as high as
                // the whole folder stands.
                var whole = Bounds(film, still).top;
                var last = Bounds(film, film.Before).top;
                var (highest, going) = (last, false);
                // How far from the whole folder it stood as the close began, and the nearest it came after: no nearer than
                // half of that (the opening runs on a little until the close's commit lands; a close from the whole folder
                // stands at it). A capture lays the see-through folder over black, so a fainter folder stands further off.
                var from = film.Distance(film.Before, still, panel);
                var nearest = from;
                foreach (var (shot, index) in film.Frames.Append(film.End).Select((shot, index) => (shot, index)))
                {
                    var top = Bounds(film, shot).top;
                    nearest = Math.Min(nearest, film.Distance(shot, still, panel));
                    highest = Math.Min(highest, top);
                    going |= top > last + 1;
                    Assert.True(!going || top >= last - 1, $"f{index} at {shot.At:F0} ms rises again, its top at {top} after {last}, once the close had begun to go{Environment.NewLine}{film.Report()}");
                    last = top;
                }
                Assert.True(film.Shows(film.End, panel) < 0.01, $"the close ended with {film.Shows(film.End, panel):P1} of the folder in sight");
                if (highest > whole + 3 && going && nearest >= from / 2)
                {
                    return;
                }
                wrong = $"the folder stood whole (its top at {highest}, the whole folder's {whole}; {nearest:F1} off it at the nearest, {from:F1} as the close began) before it went, or never went ({going}): the close did not go back from where it was{Environment.NewLine}{film.Report()}";
            }
            Assert.Fail(wrong);
        });
    }

    /// <summary>
    /// <paramref name="act"/> filmed after <paramref name="prepare"/>, until a frame on the way is caught (at most three takes;
    /// the hidden desktop's compositor can stall) — and, where <paramref name="caught"/> says what else a take must have caught, that.
    /// </summary>
    private static Film Shoot(string name, Gfx gfx, RECT area, FolderFlyout flyout, Action act, Action prepare, int filmMs = 400, Func<Film, bool>? caught = null)
    {
        Film? film = null;
        for (var take = 0; take < 3; take++)
        {
            film = FrameFilm.Shoot(name, gfx, area, () => [flyout.Hwnd], act, filmMs: filmMs);
            var whole = film.Differs(film.Before, film.End);
            if (film.Frames.Any(shot => film.Differs(shot, film.Before) > whole * 0.05 && film.Differs(shot, film.End) > whole * 0.05)
                && (caught?.Invoke(film) ?? true))
            {
                return film;
            }
            // A stalled compositor shows the end at once, its animation not run: a moment for it, then again.
            Thread.Sleep(500);
            prepare();
        }
        Assert.Fail($"{name}: no frame on the way between the before and the end in three takes — nothing moved:{Environment.NewLine}{film!.Report()}");
        return film;
    }

    /// <summary>
    /// Every frame at least as far along as the frames before it: the top of what is drawn (the folder and its shadow) only
    /// rises as it comes in (<paramref name="up"/>) and only sinks as it goes — a px's give. The way is read off where the
    /// folder stands, not its colours: a capture lays a part see-through over black, not over what is behind it.
    /// </summary>
    private static void OnTheWay(Film film, bool up)
    {
        var last = Bounds(film, film.Before).top;
        foreach (var (shot, index) in film.Frames.Append(film.End).Select((shot, index) => (shot, index)))
        {
            var top = Bounds(film, shot).top;
            Assert.True(up ? top <= last + 1 : top >= last - 1, $"{film.Name}: f{index} at {shot.At:F0} ms goes back, its top at {top} after {last} (a flash){Environment.NewLine}{film.Report()}");
            last = top;
        }
    }

    /// <summary>Nothing drawn above the folder's top, nor past its sides, in any frame: it comes and goes along its own way to its bar, never anywhere else.</summary>
    private static void NowhereElse(Film film, RECT open)
    {
        var area = film.Area;
        RECT[] outside =
        [
            new() { left = area.left, top = area.top, right = area.right, bottom = open.top - 2 },
            new() { left = area.left, top = area.top, right = open.left - 2, bottom = area.bottom },
            new() { left = open.right + 2, top = area.top, right = area.right, bottom = area.bottom },
        ];
        foreach (var (shot, index) in new[] { film.Before }.Concat(film.Frames).Append(film.End).Select((shot, index) => (shot, index)))
        {
            foreach (var rect in outside.Where(rect => rect.right > rect.left && rect.bottom > rect.top))
            {
                Assert.True(film.Shows(shot, rect) < 0.002, $"{film.Name}: frame {index} at {shot.At:F0} ms draws {film.Shows(shot, rect):P1} outside where the folder stands, at {rect.left},{rect.top}-{rect.right},{rect.bottom}");
            }
        }
    }

    /// <summary>Where <paramref name="shot"/> draws anything (its shadow too), in screen px.</summary>
    private static RECT Bounds(Film film, Shot shot)
    {
        var area = film.Area;
        bool Row(int y) => film.Shows(shot, new RECT { left = area.left, top = y, right = area.right, bottom = y + 1 }) > 0;
        bool Column(int x) => film.Shows(shot, new RECT { left = x, top = area.top, right = x + 1, bottom = area.bottom }) > 0;
        var top = area.top;
        while (top < area.bottom && !Row(top))
        {
            top++;
        }
        var left = area.left;
        while (left < area.right && !Column(left))
        {
            left++;
        }
        var right = area.right;
        while (right > left && !Column(right - 1))
        {
            right--;
        }
        return new RECT { left = left, top = top, right = right, bottom = area.bottom };
    }

    private static double Differs(RECT area, FolderFlyout flyout, Shot still)
    {
        var film = new Film("probe", area) { Before = still };
        return film.Differs(FrameFilm.Snap(area, [flyout.Hwnd], 0, "probe"), still);
    }

    private static bool Pumped(Func<bool> done)
    {
        FrameFilm.Pump();
        Thread.Sleep(5);
        return done();
    }

    /// <summary>The folder shut and out of sight, its closing run out.</summary>
    private static void Shut(FolderFlyout flyout)
    {
        if (flyout.IsOpen)
        {
            flyout.Hide();
        }
        Assert.True(Patience.Until(() => Pumped(() => !IsWindowVisible(flyout.Hwnd))), "the folder's window never went out of sight");
        FrameFilm.Settle();
    }
}
